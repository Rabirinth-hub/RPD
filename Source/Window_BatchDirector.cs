using RimTalk.Data;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class Window_BatchDirector : Window
    {
        private readonly PawnRosterList _roster;
        private readonly Dictionary<Pawn, Task<PersonalityData>> _generationTasks = new Dictionary<Pawn, Task<PersonalityData>>();
        private Task<PersonalityData> _batchTask;
        private List<Pawn> _batchTaskPawns;
        private bool _batchSendMode;

        public Window_BatchDirector()
        {
            doCloseX = true;
            draggable = true;
            resizeable = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            if (DirectorMod.Settings.BatchFilters == null) DirectorMod.Settings.InitFilters();
            _roster = new PawnRosterList(GetCurrentMapPawns);
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public override void PostClose()
        {
            _generationTasks.Clear();
            _batchTask = null;
            _batchTaskPawns = null;
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            _roster.RefreshIfInvalid();
            UpdateAsyncTasks();
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            Rect top = list.GetRect(30f);
            Widgets.Label(top.LeftPart(0.48f), "RPD_Batch_Title".Translate());
            Rect simpleButton = new Rect(top.xMax - 245f, top.y, 120f, 28f);
            if (Widgets.ButtonText(simpleButton, "RPD_Mode_SwitchToSimple".Translate()))
            {
                Close();
                Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }
            if (DirectorFeatureGate.ExperimentalEnabled)
            {
                Rect evolveButton = new Rect(top.xMax - 120f, top.y, 120f, 28f);
                if (Widgets.ButtonText(evolveButton, "RPD_Tab_AutoEvolution".Translate()))
                {
                    Close();
                    Find.WindowStack.Add(new Window_AutoEvolveConfig());
                }
            }
            list.GapLine();

            DrawNotesSection(list);
            list.GapLine();
            _roster.DrawFilters(list.GetRect(60f), DrawPromptSelector, 150f);
            list.Gap(5f);

            Rect pawnList = list.GetRect(inRect.height - list.CurHeight - 40f);
            _roster.DrawList(
                pawnList,
                250f,
                rect => Widgets.Label(rect, "RPD_Batch_Header_Actions".Translate()),
                DrawPawnActions);

            list.Gap(5f);
            DrawGlobalActions(list);
            list.End();
            _roster.EndDragIfReleased();
        }

        private void DrawNotesSection(Listing_Standard list)
        {
            Rect header = list.GetRect(24f);
            Widgets.Label(header.LeftPart(0.7f), "RPD_Batch_SceneNotes".Translate());
            if (Widgets.ButtonText(header.RightPart(0.3f), "RPD_Button_Clear".Translate()))
                DirectorMod.Settings.directorNotes = "";
            DirectorMod.Settings.directorNotes = Widgets.TextArea(list.GetRect(60f), DirectorMod.Settings.directorNotes);
        }

        private static IEnumerable<Pawn> GetCurrentMapPawns()
        {
            Map map = Find.CurrentMap;
            if (map?.mapPawns == null) return Enumerable.Empty<Pawn>();
            return map.mapPawns.AllPawns.Where(pawn =>
                pawn != null && !pawn.Dead && pawn.def != null && pawn.Spawned
                && (pawn.Faction == Faction.OfPlayer || !pawn.Map.fogGrid.IsFogged(pawn.Position)));
        }

        private void DrawPromptSelector(Rect rect)
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings.presets == null) settings.InitPresets();
            settings.selectedPresetIndex = Mathf.Clamp(settings.selectedPresetIndex, 0, settings.presets.Count - 1);
            string label = settings.presets[settings.selectedPresetIndex].label;
            if (label.Length > 15) label = label.Substring(0, 12) + "...";
            if (Widgets.ButtonText(rect, label))
            {
                var options = new List<FloatMenuOption>();
                for (int i = 0; i < settings.presets.Count; i++)
                {
                    if (i == 3) continue;
                    int index = i;
                    string name = settings.presets[i].label;
                    options.Add(new FloatMenuOption(name, () => settings.selectedPresetIndex = index));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(rect, "RPD_Tip_PromptSwitch".Translate());
        }

        private void DrawPawnActions(Pawn pawn, Rect rect)
        {
            bool singleGenerating = _generationTasks.ContainsKey(pawn) && !_generationTasks[pawn].IsCompleted;
            bool batchGenerating = _batchTask != null && !_batchTask.IsCompleted
                && _batchTaskPawns != null && _batchTaskPawns.Contains(pawn);
            if (singleGenerating || batchGenerating)
            {
                Widgets.Label(rect, "RPD_Batch_Status_Generating".Translate());
                return;
            }

            float buttonWidth = (rect.width - 10f) / 3f;
            Rect quick = new Rect(rect.x, rect.y, buttonWidth, 24f);
            Rect edit = new Rect(quick.xMax + 5f, rect.y, buttonWidth, 24f);
            Rect talk = new Rect(edit.xMax + 5f, rect.y, buttonWidth, 24f);
            if (Widgets.ButtonText(talk, "RPD_Batch_Button_Talk".Translate()))
                DirectorUtils.OpenRimTalkDialog(pawn);
            if (Widgets.ButtonText(quick, "RPD_Batch_Button_QuickGen".Translate()))
            {
                string data = DirectorUtils.BuildCustomCharacterData(pawn);
                _generationTasks[pawn] = DirectorUtils.GeneratePersonalityTask(data, pawn.LabelShortCap, pawn);
            }
            if (Widgets.ButtonText(edit, "RPD_Batch_Button_DeepEdit".Translate()))
                Find.WindowStack.Add(new RimTalk.UI.PersonaEditorWindow(pawn));
        }

        private void DrawGlobalActions(Listing_Standard list)
        {
            Rect rect = list.GetRect(30f);
            HashSet<Pawn> selected = _roster.SelectedPawns;
            if (Widgets.ButtonText(rect.LeftPart(0.7f), "RPD_Batch_Button_BatchGen".Translate(selected.Count)))
            {
                if (_batchSendMode)
                {
                    if (_batchTask == null && selected.Any())
                    {
                        _batchTaskPawns = selected.ToList();
                        string data = DirectorUtils.BuildCombinedCharacterData(_batchTaskPawns);
                        _batchTask = DirectorUtils.GenerateBatchPersonaTask(data, _batchTaskPawns.FirstOrDefault());
                    }
                }
                else
                {
                    foreach (Pawn pawn in selected)
                    {
                        if (_generationTasks.ContainsKey(pawn)) continue;
                        string data = DirectorUtils.BuildCustomCharacterData(pawn);
                        _generationTasks[pawn] = DirectorUtils.GeneratePersonalityTask(data, pawn.LabelShortCap, pawn);
                    }
                }
            }

            Rect mode = rect.RightPart(0.25f);
            string key = _batchSendMode ? "RPD_Batch_Button_ModeBatch" : "RPD_Batch_Button_ModeSingle";
            if (Widgets.ButtonText(mode, key.Translate())) _batchSendMode = !_batchSendMode;
            TooltipHandler.TipRegion(mode, "RPD_Batch_Button_ModeTooltip".Translate());
        }

        private void UpdateAsyncTasks()
        {
            foreach (Pawn pawn in _generationTasks.Keys.ToList())
            {
                Task<PersonalityData> task = _generationTasks[pawn];
                if (!task.IsCompleted) continue;
                _generationTasks.Remove(pawn);
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    PersonalityData result = task.Result;
                    if (pawn != null && !pawn.Destroyed && result != null && !string.IsNullOrEmpty(result.Persona))
                    {
                        DirectorUtils.ApplyPersonalityToPawn(pawn, result);
                        Messages.Message("RPD_Message_GeneratedSuccess".Translate(pawn.LabelShortCap), MessageTypeDefOf.PositiveEvent, false);
                    }
                    else if (pawn != null)
                    {
                        Messages.Message("RPD_Message_GeneratedFail".Translate(pawn.LabelShortCap), MessageTypeDefOf.NegativeEvent, false);
                    }
                }
                else Log.Error("[Director] Task failed for " + pawn.LabelShortCap + ": " + task.Exception);
            }

            if (_batchTask == null || !_batchTask.IsCompleted) return;
            if (_batchTask.Status == TaskStatus.RanToCompletion)
            {
                PersonalityData result = _batchTask.Result;
                if (result != null && !string.IsNullOrEmpty(result.Persona))
                {
                    int count = DirectorUtils.ParseAndApplyBatchResult(_batchTaskPawns, result.Persona);
                    Messages.Message(
                        count > 0 ? "RPD_Message_BatchGeneratedSuccess".Translate(count) : "RPD_Message_BatchGeneratedFail".Translate(),
                        count > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent,
                        false);
                }
                else Messages.Message("RPD_Message_BatchGeneratedFail".Translate(), MessageTypeDefOf.NegativeEvent, false);
            }
            else Log.Error("[Director] Batch Task failed: " + _batchTask.Exception);
            _batchTask = null;
            _batchTaskPawns = null;
        }
    }
}
