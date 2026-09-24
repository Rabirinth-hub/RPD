using RimTalk.UI;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public sealed class Window_AutoEvolveConfig : Window
    {
        private readonly PawnRosterList _roster;
        private string _batchIntervalBuffer = "0";
        private string _historyLimitBuffer;
        private string _initialNotes;
        private bool _changed;

        public Window_AutoEvolveConfig()
        {
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            _historyLimitBuffer = (DirectorMod.Settings?.personaHistoryMaxRecords ?? 5).ToString();
            _initialNotes = DirectorMod.Settings?.autoEvolveNotes ?? "";
            _roster = new PawnRosterList(
                () => PawnsFinder.AllMaps_Spawned,
                new[] { "Colonists", "Prisoners", "Slaves", "Visitors", "Enemies", "Other", "Anomalies" },
                IsVisiblePawn);
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled || DirectorMod.Settings == null)
            {
                Close();
                return;
            }

            DirectorSettings settings = DirectorMod.Settings;
            _roster.RefreshIfInvalid();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 170f, 30f),
                "RPD_Tab_AutoEvolution".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(
                new Rect(inRect.xMax - 160f, inRect.y, 160f, 28f),
                "RPD_Tab_ManualGeneration".Translate()))
            {
                Close();
                Find.WindowStack.Add(new Window_BatchDirector());
                return;
            }

            float y = inRect.y + 38f;
            DrawGlobalSettings(new Rect(inRect.x, y, inRect.width, 26f), settings);
            y += 34f;
            DrawTriggerSettings(new Rect(inRect.x, y, inRect.width, 88f), settings);
            y += 94f;
            DrawNotes(new Rect(inRect.x, y, inRect.width, 82f), settings);
            y += 88f;

            Rect listRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            DrawPawnList(listRect, settings);
            _roster.EndDragIfReleased();
        }

        public override void PostClose()
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings != null)
            {
                int limit;
                if (!int.TryParse(_historyLimitBuffer, out limit) || limit < 0)
                    limit = settings.personaHistoryMaxRecords;
                _historyLimitBuffer = limit.ToString();
                if (settings.personaHistoryMaxRecords != limit)
                {
                    settings.personaHistoryMaxRecords = limit;
                    Find.World?.GetComponent<DirectorWorldComponent>()?.ApplyHistoryLimits();
                    _changed = true;
                }

                if (!string.Equals(_initialNotes, settings.autoEvolveNotes, StringComparison.Ordinal))
                    _changed = true;

                if (_changed)
                {
                    DirectorFeatureGate.ResetTransientWork();
                    settings.Write();
                }
            }
            base.PostClose();
        }

        private void DrawGlobalSettings(Rect rect, DirectorSettings settings)
        {
            Rect firstRow = new Rect(rect.x, rect.y, rect.width, 26f);
            Rect enableRect = firstRow.LeftPartPixels(210f);
            bool enabled = settings.globalAutoEvolveEnabled;
            Widgets.CheckboxLabeled(
                enableRect,
                "RPD_AutoEvolve_Enable".Translate(),
                ref enabled);
            TooltipHandler.TipRegion(enableRect, "RPD_AutoEvolve_EnableTooltip".Translate());
            if (enabled != settings.globalAutoEvolveEnabled)
            {
                settings.globalAutoEvolveEnabled = enabled;
                _changed = true;
                DirectorFeatureGate.ResetTransientWork();
            }

            Rect defaultRect = new Rect(firstRow.x + 220f, firstRow.y, 260f, 24f);
            bool defaultEnabled = settings.autoEvolveDefaultEnabled;
            Widgets.CheckboxLabeled(
                defaultRect,
                "RPD_AutoEvolve_DefaultEnabled".Translate(),
                ref defaultEnabled);
            TooltipHandler.TipRegion(defaultRect, "RPD_AutoEvolve_DefaultEnabledTip".Translate());
            if (defaultEnabled != settings.autoEvolveDefaultEnabled)
            {
                settings.autoEvolveDefaultEnabled = defaultEnabled;
                _changed = true;
            }

            Rect notifyRect = new Rect(defaultRect.xMax + 8f, firstRow.y, 220f, 24f);
            string notify = settings.autoNotify == AutoEvolveNotify.Silent
                ? "RPD_AutoEvolve_NotifySilent".Translate()
                : "RPD_AutoEvolve_NotifyTopLeftMessage".Translate();
            if (Widgets.ButtonText(notifyRect, "RPD_AutoEvolve_Notify".Translate() + ": " + notify))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RPD_AutoEvolve_NotifySilent".Translate(), () => SetNotify(AutoEvolveNotify.Silent)),
                    new FloatMenuOption("RPD_AutoEvolve_NotifyTopLeftMessage".Translate(), () => SetNotify(AutoEvolveNotify.TopLeftMessage))
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Rect speedRect = new Rect(
                notifyRect.xMax + 8f,
                firstRow.y,
                rect.xMax - notifyRect.xMax - 8f,
                24f);
            if (Widgets.ButtonText(speedRect, GetSpeedLabel(settings.speedProtection)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RPD_AutoEvolve_SpeedDisabled".Translate(), () => SetSpeed(SpeedProtection.Disabled)),
                    new FloatMenuOption("RPD_AutoEvolve_Speed2X".Translate(), () => SetSpeed(SpeedProtection.Speed2X)),
                    new FloatMenuOption("RPD_AutoEvolve_Speed3X".Translate(), () => SetSpeed(SpeedProtection.Speed3X)),
                    new FloatMenuOption("RPD_AutoEvolve_Speed4X".Translate(), () => SetSpeed(SpeedProtection.Speed4X))
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(speedRect, "RPD_AutoEvolve_SpeedProtectTooltip".Translate());
        }

        private void DrawTriggerSettings(Rect rect, DirectorSettings settings)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            Widgets.Label(new Rect(inner.x, inner.y, 210f, 22f),
                "RPD_AutoEvolve_Triggers".Translate());
            DrawRightAlignedFootnote(
                new Rect(inner.x + 210f, inner.y, inner.width - 210f, 22f),
                "RPD_AutoEvolve_TriggersTip".Translate());

            settings.EnsureAutoGenCategories();
            float labelWidth = 120f;
            const int triggerColumnCount = 6;
            float roleY = inner.y + 26f;
            Widgets.Label(new Rect(inner.x, roleY, labelWidth, 24f),
                "RPD_AutoEvolve_RoleTriggers".Translate());
            float roleX = inner.x + labelWidth;
            float roleWidth = (inner.xMax - roleX) / triggerColumnCount;
            foreach (string categoryId in AutoGenCategoryCatalog.OrderedIds)
            {
                AutoGenCategory category = settings.autoGenCategories[categoryId];
                Rect toggleRect = new Rect(roleX, roleY, roleWidth, 24f);
                bool enabled = category.onlyOnRoleChange;
                DrawLeadingCheckbox(toggleRect,
                    ("RPD_AutoGen_Category" + categoryId).Translate(), ref enabled);
                TooltipHandler.TipRegion(toggleRect, "RPD_AutoEvolve_RoleChangeTip".Translate());
                if (enabled != category.onlyOnRoleChange)
                {
                    category.onlyOnRoleChange = enabled;
                    _changed = true;
                    DirectorFeatureGate.ResetTransientWork();
                }
                roleX += roleWidth;
            }

            float eventY = inner.y + 54f;
            Widgets.Label(new Rect(inner.x, eventY, labelWidth, 24f),
                "RPD_AutoEvolve_EventTriggers".Translate());
            float eventX = inner.x + labelWidth;
            float eventWidth = (inner.xMax - eventX) / triggerColumnCount;
            DrawTriggerCheckbox(new Rect(eventX, eventY, eventWidth, 24f),
                "RPD_AutoEvolve_EventMarriage", ref settings.autoEvolveOnMarriage);
            DrawTriggerCheckbox(new Rect(eventX + eventWidth, eventY, eventWidth, 24f),
                "RPD_AutoEvolve_EventBreakup", ref settings.autoEvolveOnBreakup);
            DrawTriggerCheckbox(new Rect(eventX + eventWidth * 2f, eventY, eventWidth, 24f),
                "RPD_AutoEvolve_EventBirth", ref settings.autoEvolveOnBirth);
            DrawTriggerCheckbox(new Rect(eventX + eventWidth * 3f, eventY, eventWidth, 24f),
                "RPD_AutoEvolve_EventDirectFamilyDeath", ref settings.autoEvolveOnDirectFamilyDeath);
            DrawTriggerCheckbox(new Rect(eventX + eventWidth * 4f, eventY, eventWidth, 24f),
                "RPD_AutoEvolve_EventTraitAdded", ref settings.autoEvolveOnTraitAdded);

        }

        private void DrawTriggerCheckbox(Rect rect, string key, ref bool value)
        {
            bool enabled = value;
            DrawLeadingCheckbox(rect, key.Translate(), ref enabled);
            TooltipHandler.TipRegion(rect, (key + "Tip").Translate());
            if (enabled == value) return;

            value = enabled;
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private static void DrawLeadingCheckbox(Rect rect, string label, ref bool value)
        {
            Widgets.Checkbox(new Vector2(rect.x, rect.y), ref value);
            Rect labelRect = new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height);
            Widgets.Label(labelRect, label);
            if (Widgets.ButtonInvisible(labelRect)) value = !value;
        }

        private void DrawAdvancedPresetSelector(Rect rect, DirectorSettings settings)
        {
            string noneLabel = "RPD_Setting_NoneInternal".Translate();
            string current = string.IsNullOrEmpty(settings.rimTalkPreset_Evolve)
                ? noneLabel
                : settings.rimTalkPreset_Evolve;
            string buttonLabel = "RPD_AutoEvolve_AdvancedPreset".Translate()
                + ": " + current;

            if (Widgets.ButtonText(rect, buttonLabel))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(noneLabel, () => SetAdvancedPreset(""))
                };

                try
                {
                    var presets = RimTalk.API.RimTalkPromptAPI.GetAllPresets();
                    if (presets != null)
                    {
                        foreach (var preset in presets)
                        {
                            if (preset == null || string.IsNullOrEmpty(preset.Name)) continue;
                            string presetName = preset.Name;
                            options.Add(new FloatMenuOption(
                                presetName,
                                () => SetAdvancedPreset(presetName)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[Persona Director] Could not list RimTalk presets: " + ex.Message);
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(
                rect,
                "RPD_AutoEvolve_AdvancedPresetTooltip".Translate());
        }

        private static void DrawNotes(Rect rect, DirectorSettings settings)
        {
            Widgets.Label(new Rect(rect.x, rect.y, 210f, 22f),
                "RPD_AutoEvolve_NotesLabel".Translate());
            DrawRightAlignedFootnote(
                new Rect(rect.x + 210f, rect.y, rect.width - 210f, 22f),
                "RPD_AutoEvolve_InternalPromptNotice".Translate());
            settings.autoEvolveNotes = Widgets.TextArea(
                new Rect(rect.x, rect.y + 24f, rect.width, rect.height - 24f),
                settings.autoEvolveNotes ?? "");
        }

        private static void DrawRightAlignedFootnote(Rect rect, string message)
        {
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.gray;
            Widgets.Label(rect, message);
            TooltipHandler.TipRegion(rect, message);
            GUI.color = previousColor;
            Text.Anchor = previousAnchor;
            Text.Font = previousFont;
        }

        private void DrawPawnList(Rect outRect, DirectorSettings settings)
        {
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            DirectorAutoEvolveManager manager = Find.World?.GetComponent<DirectorAutoEvolveManager>();
            _roster.DrawFilters(
                new Rect(outRect.x, outRect.y, outRect.width, 60f),
                rect => DrawListPromptControls(rect, settings),
                478f);
            DrawBatchToolbar(new Rect(outRect.x, outRect.y + 62f, outRect.width, 28f), world);
            _roster.DrawList(
                new Rect(outRect.x, outRect.y + 92f, outRect.width, outRect.height - 92f),
                350f,
                DrawAutoHeader,
                (pawn, row) => DrawPawnRow(row, pawn, world, manager));
        }

        private void DrawListPromptControls(Rect rect, DirectorSettings settings)
        {
            Rect presetRect = new Rect(rect.x, rect.y, rect.width - 158f, 24f);
            DrawAdvancedPresetSelector(presetRect, settings);

            Rect modeRect = new Rect(presetRect.xMax + 8f, rect.y, 150f, 24f);
            string mode = settings.autoMode == AutoEvolveMode.Append
                ? "RPD_AutoEvolve_ModeAppend".Translate()
                : "RPD_AutoEvolve_ModeOverwrite".Translate();
            if (Widgets.ButtonText(modeRect, "RPD_AutoEvolve_Mode".Translate() + ": " + mode))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RPD_AutoEvolve_ModeAppend".Translate(), () => SetMode(AutoEvolveMode.Append)),
                    new FloatMenuOption("RPD_AutoEvolve_ModeOverwrite".Translate(), () => SetMode(AutoEvolveMode.Overwrite))
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(modeRect, "RPD_AutoEvolve_ModeTooltip".Translate());
        }

        private static void DrawAutoHeader(Rect rect)
        {
            float x = rect.x;
            Rect autoHeader = new Rect(x, rect.y, 65f, 24f);
            Widgets.Label(autoHeader, "RPD_AutoEvolve_HeaderAuto".Translate());
            TooltipHandler.TipRegion(autoHeader, "RPD_AutoEvolve_HeaderAutoTip".Translate());
            x += 65f;
            Widgets.Label(new Rect(x, rect.y, 62f, 24f), "RPD_AutoEvolve_HeaderCycle".Translate()); x += 62f;
            Widgets.Label(new Rect(x, rect.y, 96f, 24f), "RPD_AutoEvolve_HeaderNext".Translate()); x += 96f;
            Widgets.Label(new Rect(x, rect.y, rect.xMax - x, 24f), "RPD_AutoEvolve_HeaderActions".Translate());
        }

        private void DrawPawnRow(
            Rect row,
            Pawn pawn,
            DirectorWorldComponent world,
            DirectorAutoEvolveManager manager)
        {
            if (world == null) return;
            AutoEvolveConfig config = world.GetAutoConfig(pawn);
            float x = row.x;
            bool enabled = config.enabled;
            Widgets.Checkbox(new Vector2(x + 10f, row.y + 2f), ref enabled);
            if (enabled != config.enabled)
            {
                config.enabled = enabled;
                config.ScheduleFromNow(GenTicks.TicksGame);
                _changed = true;
                DirectorFeatureGate.ResetTransientWork();
            }
            x += 65f;

            config.daysBuffer = Widgets.TextField(new Rect(x, row.y, 46f, 24f), config.daysBuffer ?? "0");
            int interval;
            if (int.TryParse(config.daysBuffer, out interval))
            {
                interval = AutoEvolveConfig.NormalizeInterval(interval);
                config.daysBuffer = interval.ToString();
                if (interval != config.intervalDays)
                {
                    config.intervalDays = interval;
                    config.ScheduleFromNow(GenTicks.TicksGame);
                    _changed = true;
                    DirectorFeatureGate.ResetTransientWork();
                }
            }
            x += 62f;

            string next = "--";
            if (config.enabled)
            {
                if (manager != null && manager.IsPending(pawn))
                    next = "RPD_AutoEvolve_Pending".Translate();
                else if (!config.TimedUpdatesEnabled)
                    next = "RPD_AutoEvolve_ScheduleDisabled".Translate();
                else if (config.nextUpdateTick <= 0)
                    next = "RPD_AutoEvolve_Pending".Translate();
                else
                    next = "RPD_AutoEvolve_NextIn".Translate(
                        Mathf.FloorToInt(Math.Max(0f, (config.nextUpdateTick - GenTicks.TicksGame) / 60000f)));
            }
            Widgets.Label(new Rect(x, row.y, 92f, 24f), next); x += 96f;

            float buttonWidth = (row.xMax - x - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(x, row.y, buttonWidth, 24f),
                "RPD_AutoEvolve_ButtonEdit".Translate()))
            {
                Find.WindowStack.Add(new PersonaEditorWindow(pawn));
            }
            x += buttonWidth + 6f;
            if (Widgets.ButtonText(new Rect(x, row.y, buttonWidth, 24f),
                "RPD_AutoEvolve_ButtonHistory".Translate()))
            {
                Find.WindowStack.Add(new Window_PersonaHistory(pawn));
            }
        }

        private void DrawBatchToolbar(
            Rect rect,
            DirectorWorldComponent world)
        {
            List<Pawn> selectedPawns = _roster.SelectedVisiblePawns;
            int selectedCount = selectedPawns.Count;
            float x = rect.x;
            Widgets.Label(
                new Rect(x, rect.y, 106f, 24f),
                "RPD_AutoEvolve_SelectedCount".Translate(selectedCount));
            x += 110f;

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = selectedCount > 0 && world != null;
            if (Widgets.ButtonText(new Rect(x, rect.y, 90f, 24f),
                "RPD_AutoEvolve_EnableSelected".Translate()))
            {
                ApplySelectedEnabled(selectedPawns, world, true);
            }
            x += 96f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 90f, 24f),
                "RPD_AutoEvolve_DisableSelected".Translate()))
            {
                ApplySelectedEnabled(selectedPawns, world, false);
            }
            x += 100f;

            GUI.enabled = previousGuiEnabled;
            float intervalStart = x;
            Widgets.Label(new Rect(x, rect.y, 88f, 24f),
                "RPD_AutoEvolve_BatchInterval".Translate());
            x += 90f;
            _batchIntervalBuffer = Widgets.TextField(
                new Rect(x, rect.y, 52f, 24f),
                _batchIntervalBuffer ?? "0");
            x += 58f;

            int interval;
            bool intervalValid = int.TryParse(_batchIntervalBuffer, out interval);
            GUI.enabled = selectedCount > 0 && world != null && intervalValid;
            Rect applyRect = new Rect(x, rect.y, 90f, 24f);
            if (Widgets.ButtonText(applyRect,
                "RPD_AutoEvolve_ApplyInterval".Translate()))
            {
                ApplySelectedInterval(selectedPawns, world, interval);
            }
            TooltipHandler.TipRegion(
                new Rect(intervalStart, rect.y, applyRect.xMax - intervalStart, 24f),
                "RPD_AutoEvolve_BatchIntervalTip".Translate());
            x += 96f;

            GUI.enabled = selectedCount > 0;
            Rect clearRect = new Rect(x, rect.y, 112f, 24f);
            if (Widgets.ButtonText(clearRect,
                "RPD_AutoEvolve_ClearSelection".Translate()))
            {
                _roster.ClearSelection();
            }
            GUI.enabled = previousGuiEnabled;

            Rect historyRect = new Rect(clearRect.xMax + 12f, rect.y, 150f, 24f);
            Widgets.Label(new Rect(historyRect.x, historyRect.y, 92f, 24f),
                "RPD_History_MaxRecords".Translate());
            _historyLimitBuffer = Widgets.TextField(
                new Rect(historyRect.x + 102f, historyRect.y, 48f, 24f),
                _historyLimitBuffer);
            TooltipHandler.TipRegion(historyRect, "RPD_History_MaxRecordsTip".Translate());
        }

        private void ApplySelectedEnabled(
            IEnumerable<Pawn> selectedPawns,
            DirectorWorldComponent world,
            bool enabled)
        {
            int now = GenTicks.TicksGame;
            foreach (Pawn pawn in selectedPawns)
            {
                AutoEvolveConfig config = world.GetAutoConfig(pawn);
                config.enabled = enabled;
                config.ScheduleFromNow(now);
            }
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private void ApplySelectedInterval(
            IEnumerable<Pawn> selectedPawns,
            DirectorWorldComponent world,
            int requestedInterval)
        {
            int interval = AutoEvolveConfig.NormalizeInterval(requestedInterval);
            _batchIntervalBuffer = interval.ToString();
            int now = GenTicks.TicksGame;
            foreach (Pawn pawn in selectedPawns)
            {
                AutoEvolveConfig config = world.GetAutoConfig(pawn);
                config.intervalDays = interval;
                config.ScheduleFromNow(now);
            }
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private static bool IsVisiblePawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && pawn.Spawned
                && pawn.Map != null
                && !DirectorUtils.UsesGlobalPlayerPersona(pawn)
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike;
        }

        private void SetMode(AutoEvolveMode mode)
        {
            DirectorMod.Settings.autoMode = mode;
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private void SetNotify(AutoEvolveNotify notify)
        {
            DirectorMod.Settings.autoNotify = notify;
            _changed = true;
        }

        private void SetAdvancedPreset(string presetName)
        {
            DirectorMod.Settings.rimTalkPreset_Evolve = presetName ?? "";
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private void SetSpeed(SpeedProtection speed)
        {
            DirectorMod.Settings.speedProtection = speed;
            _changed = true;
            DirectorFeatureGate.ResetTransientWork();
        }

        private static string GetSpeedLabel(SpeedProtection speed)
        {
            if (speed == SpeedProtection.Disabled) return "RPD_AutoEvolve_SpeedDisabled".Translate();
            if (speed == SpeedProtection.Speed2X) return "RPD_AutoEvolve_Speed2X".Translate();
            if (speed == SpeedProtection.Speed3X) return "RPD_AutoEvolve_Speed3X".Translate();
            return "RPD_AutoEvolve_Speed4X".Translate();
        }
    }
}
