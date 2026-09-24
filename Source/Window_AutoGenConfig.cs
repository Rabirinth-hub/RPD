using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public sealed class Window_AutoGenConfig : Window
    {
        private Vector2 _scrollPosition;
        private readonly string _initialNotes;
        private bool _changed;

        public override Vector2 InitialSize => new Vector2(820f, 700f);

        public Window_AutoGenConfig()
        {
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            _initialNotes = DirectorMod.Settings?.autoGenNotes ?? "";
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled || DirectorMod.Settings == null)
            {
                Close();
                return;
            }

            DirectorSettings settings = DirectorMod.Settings;
            settings.EnsureAutoGenCategories();
            if (settings.presets == null || settings.presets.Count < 4)
            {
                settings.InitPresets();
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "RPD_AutoGen_ConfigTitle".Translate());
            Text.Font = GameFont.Small;

            Rect notesHeader = new Rect(inRect.x, inRect.y + 38f, inRect.width, 24f);
            Widgets.Label(notesHeader.LeftPart(0.62f), "RPD_AutoGen_NotesLabel".Translate());
            Rect templateButton = notesHeader.RightPart(0.36f);
            if (Widgets.ButtonText(templateButton, "RPD_AutoGen_InsertTemplate".Translate()))
                ShowTemplateMenu(settings);
            TooltipHandler.TipRegion(templateButton, "RPD_AutoGen_InsertTemplateTip".Translate());
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(inRect.x, inRect.y + 60f, inRect.width, 34f),
                "RPD_AutoGen_NotesHint".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            settings.autoGenNotes = Widgets.TextArea(
                new Rect(inRect.x, inRect.y + 96f, inRect.width, 64f),
                settings.autoGenNotes ?? "");

            Rect scrollRect = new Rect(
                inRect.x,
                inRect.y + 168f,
                inRect.width,
                inRect.height - 168f);
            float contentHeight = AutoGenCategoryCatalog.OrderedIds.Length * 110f;
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect);

            float y = 0f;
            foreach (string categoryId in AutoGenCategoryCatalog.OrderedIds)
            {
                AutoGenCategory category = settings.autoGenCategories[categoryId];
                DrawCategoryRow(new Rect(0f, y, viewRect.width, 102f), categoryId, category, settings);
                y += 110f;
            }

            Widgets.EndScrollView();
        }

        public override void PostClose()
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings != null)
            {
                if (!string.Equals(_initialNotes, settings.autoGenNotes, StringComparison.Ordinal))
                    _changed = true;
                if (_changed)
                {
                    DirectorFeatureGate.ResetTransientWork();
                    settings.Write();
                }
            }
            base.PostClose();
        }

        private void ShowTemplateMenu(DirectorSettings settings)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("RPD_AutoGen_InsertColonist".Translate(),
                    () => AppendTemplate(settings, "RPD_AutoGen_Template_Colonist".Translate())),
                new FloatMenuOption("RPD_AutoGen_InsertPrisoner".Translate(),
                    () => AppendTemplate(settings, "RPD_AutoGen_Template_Prisoner".Translate())),
                new FloatMenuOption("RPD_AutoGen_InsertSlave".Translate(),
                    () => AppendTemplate(settings, "RPD_AutoGen_Template_Slave".Translate())),
                new FloatMenuOption("─────────", null),
                new FloatMenuOption("RPD_AutoGen_InsertFactionTemplate".Translate(),
                    () => ShowFactionTemplateMenu(settings)),
                new FloatMenuOption("RPD_AutoGen_InsertRace".Translate(),
                    () => ShowRaceTemplateMenu(settings))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowFactionTemplateMenu(DirectorSettings settings)
        {
            List<FloatMenuOption> options = DefDatabase<FactionDef>.AllDefsListForReading
                .Where(def => def != null)
                .Select(def =>
                {
                    string defName = def.defName;
                    return new FloatMenuOption(def.LabelCap,
                        () => AppendTemplate(settings,
                            "RPD_AutoGen_Template_Faction".Translate().ToString()
                                .Replace("{0}", defName)));
                })
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowRaceTemplateMenu(DirectorSettings settings)
        {
            List<FloatMenuOption> options = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.race?.Humanlike == true)
                .Select(def =>
                {
                    string defName = def.defName;
                    return new FloatMenuOption(def.LabelCap,
                        () => AppendTemplate(settings,
                            "RPD_AutoGen_Template_Race".Translate().ToString()
                                .Replace("{0}", defName)));
                })
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AppendTemplate(DirectorSettings settings, string template)
        {
            if (settings == null || string.IsNullOrEmpty(template)) return;
            string notes = settings.autoGenNotes ?? "";
            if (notes.Length > 0 && !notes.EndsWith("\n", StringComparison.Ordinal))
                notes += Environment.NewLine;
            settings.autoGenNotes = notes + template;
            _changed = true;
        }

        private void DrawCategoryRow(
            Rect rowRect,
            string categoryId,
            AutoGenCategory category,
            DirectorSettings settings)
        {
            Widgets.DrawMenuSection(rowRect);
            Rect inner = rowRect.ContractedBy(8f);

            string categoryLabel = ("RPD_AutoGen_Category" + categoryId).Translate();
            Widgets.Label(new Rect(inner.x, inner.y, 130f, 24f), categoryLabel);

            Rect enabledRect = new Rect(inner.x + 135f, inner.y, 110f, 24f);
            bool previousEnabled = category.enabled;
            Widgets.CheckboxLabeled(
                enabledRect,
                "RPD_AutoGen_Enable".Translate(),
                ref category.enabled);
            TooltipHandler.TipRegion(enabledRect, "RPD_AutoGen_EnableTip".Translate());
            if (previousEnabled != category.enabled) _changed = true;

            Rect syncRect = new Rect(inner.x + 250f, inner.y, 130f, 24f);
            bool previousSync = category.syncWithModSettings;
            Widgets.CheckboxLabeled(
                syncRect,
                "RPD_AutoGen_SyncModSettingsShort".Translate(),
                ref category.syncWithModSettings);
            TooltipHandler.TipRegion(syncRect, "RPD_AutoGen_SyncModSettingsTip".Translate());
            if (previousSync != category.syncWithModSettings) _changed = true;

            Rect contextRect = new Rect(inner.xMax - 180f, inner.y, 180f, 24f);
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !category.syncWithModSettings;
            if (Widgets.ButtonText(contextRect, "RPD_AutoGen_EditContext".Translate()))
            {
                if (category.customContext == null) category.customContext = new ContextSettings();
                Find.WindowStack.Add(new Window_AutoGenContextSettings(
                    categoryId,
                    category.customContext,
                    () =>
                    {
                        _changed = true;
                        DirectorFeatureGate.ResetTransientWork();
                    }));
            }
            GUI.enabled = previousGuiEnabled;
            TooltipHandler.TipRegion(contextRect, "RPD_AutoGen_EditContextTip".Translate());

            Rect presetLabelRect = new Rect(inner.x, inner.y + 30f, 130f, 24f);
            Widgets.Label(presetLabelRect, "RPD_AutoGen_Preset".Translate());

            Rect presetButtonRect = new Rect(inner.x + 135f, inner.y + 30f, inner.width - 135f, 24f);
            int presetCount = Math.Min(3, settings.presets?.Count ?? 0);
            category.presetIndex = presetCount == 0
                ? 0
                : Mathf.Clamp(category.presetIndex, 0, presetCount - 1);

            string presetLabel;
            if (presetCount == 0)
            {
                presetLabel = "RPD_Setting_NoneInternal".Translate();
            }
            else
            {
                PromptPreset preset = settings.presets[category.presetIndex];
                presetLabel = preset?.label;
            }
            if (string.IsNullOrEmpty(presetLabel))
            {
                presetLabel = "RPD_Setting_Slot".Translate(category.presetIndex + 1);
            }

            if (Widgets.ButtonText(presetButtonRect, presetLabel) && presetCount > 0)
            {
                var options = new List<FloatMenuOption>();
                for (int i = 0; i < presetCount; i++)
                {
                    int index = i;
                    string label = settings.presets[index]?.label;
                    if (string.IsNullOrEmpty(label))
                    {
                        label = "RPD_Setting_Slot".Translate(index + 1);
                    }

                    options.Add(new FloatMenuOption(
                        label,
                        () => SetInternalPreset(category, index)));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            Rect advancedLabelRect = new Rect(inner.x, inner.y + 60f, 130f, 24f);
            Widgets.Label(advancedLabelRect, "RPD_AutoGen_AdvancedPreset".Translate());
            Rect advancedButtonRect = new Rect(
                inner.x + 135f,
                inner.y + 60f,
                inner.width - 135f,
                24f);
            DrawAdvancedPresetSelector(advancedButtonRect, category);
        }

        private void DrawAdvancedPresetSelector(Rect rect, AutoGenCategory category)
        {
            string noneLabel = "RPD_Setting_NoneInternal".Translate();
            string current = string.IsNullOrEmpty(category.advancedPreset)
                ? noneLabel
                : category.advancedPreset;
            if (Widgets.ButtonText(rect, current))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(noneLabel, () => SetAdvancedPreset(category, ""))
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
                                () => SetAdvancedPreset(category, presetName)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[Persona Director] Could not list RimTalk presets: " + ex.Message);
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(rect, "RPD_AutoGen_AdvancedPresetTooltip".Translate());
        }

        private void SetInternalPreset(AutoGenCategory category, int index)
        {
            if (category == null || category.presetIndex == index) return;
            category.presetIndex = index;
            _changed = true;
        }

        private void SetAdvancedPreset(AutoGenCategory category, string presetName)
        {
            if (category == null) return;
            string value = presetName ?? "";
            if (string.Equals(category.advancedPreset, value, StringComparison.Ordinal)) return;
            category.advancedPreset = value;
            _changed = true;
        }
    }
}
