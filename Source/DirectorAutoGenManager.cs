using RimTalk.API;
using RimTalk.Data;
using RimTalk.Prompt;
using RimTalk.Service;
using RimTalk.Source.Data;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public sealed class DirectorAutoGenManager : WorldComponent
    {
        private const int TaskTimeoutTicks = 10000;
        private const int MapArrivalGraceTicks = 2500;
        private const string TaskOwner = "AutoGen";

        private sealed class PendingWork
        {
            public Pawn Pawn;
            public int PawnId;
            public long Epoch;
            public bool NewPawn;
            public bool RoleChange;
            public string TriggerContext;
            public string OriginalPersona;
            public int MapArrivalDeadline;
        }

        private sealed class ActiveWork
        {
            public Pawn Pawn;
            public int PawnId;
            public long Epoch;
            public int StartTick;
            public bool NewPawn;
            public bool RoleChange;
            public string CategoryId;
            public string OriginalPersona;
            public Task<PersonalityData> Task;
            public DirectorAiTaskCoordinator.Lease Lease;
        }

        private readonly Queue<PendingWork> _queue = new Queue<PendingWork>();
        private readonly HashSet<int> _queuedPawnIds = new HashSet<int>();
        private readonly Dictionary<int, PendingWork> _queuedWorkByPawnId =
            new Dictionary<int, PendingWork>();
        private ActiveWork _active;

        public DirectorAutoGenManager(World world) : base(world)
        {
            DirectorFeatureGate.RegisterTransientCleanup(ClearCurrentWorld);
        }

        public void EnqueuePawn(Pawn pawn)
        {
            if (!IsOperational() || !IsValidPawn(pawn)) return;

            DirectorSettings settings = DirectorMod.Settings;
            settings.EnsureAutoGenCategories();
            string categoryId = AutoGenCategoryCatalog.GetPawnCategory(pawn);
            AutoGenCategory category;
            if (categoryId == null
                || !settings.autoGenCategories.TryGetValue(categoryId, out category)
                || category == null
                || !category.enabled)
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            if ((_active != null && _active.PawnId == pawnId) || _queuedPawnIds.Contains(pawnId))
            {
                return;
            }

            var pending = new PendingWork
            {
                Pawn = pawn,
                PawnId = pawnId,
                Epoch = DirectorFeatureGate.CaptureEpoch(),
                NewPawn = true,
                OriginalPersona = CurrentPersonality(pawn),
                MapArrivalDeadline = GenTicks.TicksGame + MapArrivalGraceTicks
            };
            _queue.Enqueue(pending);
            _queuedPawnIds.Add(pawnId);
            _queuedWorkByPawnId[pawnId] = pending;
        }

        public bool EnqueueRoleChangePawn(
            Pawn pawn,
            string oldCategory,
            string newCategory,
            string triggerContext = null)
        {
            if (!IsOperational()
                || !IsValidPawn(pawn)
                || !ShouldAutoGenerateExistingPawn(pawn))
            {
                return false;
            }

            DirectorSettings settings = DirectorMod.Settings;
            settings.EnsureAutoGenCategories();
            AutoGenCategory category;
            if (string.IsNullOrEmpty(newCategory)
                || !settings.autoGenCategories.TryGetValue(newCategory, out category)
                || category == null
                || !IsRoleChangeAuthorized(category))
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            PendingWork existing;
            if (_queuedWorkByPawnId.TryGetValue(pawnId, out existing))
            {
                existing.Epoch = DirectorFeatureGate.CaptureEpoch();
                existing.RoleChange = true;
                existing.TriggerContext = string.IsNullOrWhiteSpace(triggerContext)
                    ? BuildRoleChangeContext(oldCategory, newCategory)
                    : triggerContext.Trim();
                existing.OriginalPersona = CurrentPersonality(pawn);
                existing.MapArrivalDeadline = GenTicks.TicksGame + MapArrivalGraceTicks;
                return true;
            }

            var pending = new PendingWork
            {
                Pawn = pawn,
                PawnId = pawnId,
                Epoch = DirectorFeatureGate.CaptureEpoch(),
                RoleChange = true,
                TriggerContext = string.IsNullOrWhiteSpace(triggerContext)
                    ? BuildRoleChangeContext(oldCategory, newCategory)
                    : triggerContext.Trim(),
                OriginalPersona = CurrentPersonality(pawn),
                MapArrivalDeadline = GenTicks.TicksGame + MapArrivalGraceTicks
            };
            _queue.Enqueue(pending);
            _queuedPawnIds.Add(pawnId);
            _queuedWorkByPawnId[pawnId] = pending;
            return true;
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (!IsOperational())
            {
                ClearRuntime();
                return;
            }

            try
            {
                if (_active != null) PollActiveTask();
                if (_active == null) StartNextTask();
            }
            catch (Exception ex)
            {
                ClearRuntime();
                DirectorFeatureGate.Trip("RPD_Experimental_AutoGenRuntimeFailed", ex);
            }
        }

        public int GetQueueSize()
        {
            return _queue.Count + (_active == null ? 0 : 1);
        }

        public int GetActiveTaskCount()
        {
            return _active == null ? 0 : 1;
        }

        internal static void ClearCurrentWorld()
        {
            Find.World?.GetComponent<DirectorAutoGenManager>()?.ClearRuntime();
        }

        private void StartNextTask()
        {
            DirectorAiTaskCoordinator.Lease lease;
            if (_queue.Count == 0
                || AIService.IsBusy()
                || !DirectorAiTaskCoordinator.TryAcquire(TaskOwner, world, out lease)) return;

            bool taskStarted = false;
            try
            {
            while (_queue.Count > 0 && IsOperational())
            {
                PendingWork pending = _queue.Dequeue();
                _queuedPawnIds.Remove(pending.PawnId);
                _queuedWorkByPawnId.Remove(pending.PawnId);

                if (!DirectorFeatureGate.IsCurrentEpoch(pending.Epoch)
                    || !IsValidPawn(pending.Pawn))
                {
                    continue;
                }
                if (!DirectorAutoEvolveTriggerService.IsPawnOnMap(pending.Pawn))
                {
                    if (GenTicks.TicksGame <= pending.MapArrivalDeadline
                        && !pending.Pawn.IsWorldPawn())
                    {
                        _queue.Enqueue(pending);
                        _queuedPawnIds.Add(pending.PawnId);
                        _queuedWorkByPawnId[pending.PawnId] = pending;
                        return;
                    }
                    continue;
                }
                if ((!pending.NewPawn && !ShouldAutoGenerateExistingPawn(pending.Pawn))
                    || !string.Equals(
                        CurrentPersonality(pending.Pawn),
                        pending.OriginalPersona,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                DirectorSettings settings = DirectorMod.Settings;
                settings.EnsureAutoGenCategories();
                string categoryId = AutoGenCategoryCatalog.GetPawnCategory(pending.Pawn);
                AutoGenCategory category;
                if (categoryId == null
                    || !settings.autoGenCategories.TryGetValue(categoryId, out category)
                    || category == null
                    || (pending.RoleChange
                        ? !IsRoleChangeAuthorized(category)
                        : !category.enabled))
                {
                    continue;
                }

                TalkRequest request = BuildRequest(
                    pending.Pawn,
                    category,
                    settings,
                    pending.TriggerContext);
                if (request == null) continue;
                Task<PersonalityData> task = AIService.Query<PersonalityData>(request);
                if (task == null) continue;

                _active = new ActiveWork
                {
                    Pawn = pending.Pawn,
                    PawnId = pending.PawnId,
                    Epoch = pending.Epoch,
                    StartTick = GenTicks.TicksGame,
                    NewPawn = pending.NewPawn,
                    RoleChange = pending.RoleChange,
                    CategoryId = categoryId,
                    OriginalPersona = pending.OriginalPersona,
                    Task = task,
                    Lease = lease
                };
                taskStarted = true;

                if (settings.EnableDebugLog)
                {
                    Log.Message("[Persona Director] AutoGen request started for "
                        + pending.Pawn.LabelShortCap + ".");
                }

                return;
            }
            }
            finally
            {
                if (!taskStarted) DirectorAiTaskCoordinator.Release(lease);
            }
        }

        private void PollActiveTask()
        {
            ActiveWork work = _active;
            if (!work.Task.IsCompleted)
            {
                if (GenTicks.TicksGame - work.StartTick > TaskTimeoutTicks)
                {
                    ObserveFault(work.Task);
                    _active = null;
                    DirectorAiTaskCoordinator.Release(work.Lease);
                    DirectorFeatureGate.Trip("RPD_Experimental_AutoGenTimeout", null);
                }
                return;
            }

            _active = null;
            try
            {
                if (!DirectorFeatureGate.IsCurrentEpoch(work.Epoch)
                    || !IsOperational()
                    || !IsValidPawn(work.Pawn)
                    || !DirectorAutoEvolveTriggerService.IsPawnOnMap(work.Pawn)
                    || (!work.NewPawn && !ShouldAutoGenerateExistingPawn(work.Pawn))
                    || !IsStillAuthorized(work))
                {
                    ObserveFault(work.Task);
                    return;
                }

                if (work.Task.IsCanceled) return;
                if (work.Task.IsFaulted)
                {
                    Exception error = work.Task.Exception?.GetBaseException();
                    Log.Warning("[Persona Director] AutoGen request failed for "
                        + work.Pawn.LabelShortCap + ": " + error?.Message);
                    return;
                }

                PersonalityData result = work.Task.GetAwaiter().GetResult();
                string persona = result?.Persona?.Trim();
                if (string.IsNullOrEmpty(persona)
                    || !string.Equals(
                        CurrentPersonality(work.Pawn),
                        work.OriginalPersona,
                        StringComparison.Ordinal))
                {
                    return;
                }

                string snapshot = DirectorUtils.BuildCustomCharacterData(work.Pawn, true, false);
                string context = "RPD_History_AutoGen".Translate() + "\n\n" + snapshot;
                if (!DirectorHistoryService.ApplyWithHistory(
                    work.Pawn,
                    persona,
                    context,
                    true))
                {
                    return;
                }
                if (DirectorMod.Settings.EnableDebugLog)
                {
                    Log.Message("[Persona Director] AutoGen applied to "
                        + work.Pawn.LabelShortCap + ".");
                }
            }
            finally
            {
                DirectorAiTaskCoordinator.Release(work.Lease);
            }
        }

        private static TalkRequest BuildRequest(
            Pawn pawn,
            AutoGenCategory category,
            DirectorSettings settings,
            string triggerContext)
        {
            // A selected advanced preset owns the full request, even when invalid.
            if (!string.IsNullOrWhiteSpace(category.advancedPreset)
                && !string.Equals(category.advancedPreset, "None (Use Internal)", StringComparison.OrdinalIgnoreCase))
            {
                return TryBuildAdvancedPresetRequest(pawn, category.advancedPreset, triggerContext);
            }

            int presetIndex = Mathf.Clamp(category.presetIndex, 0, 2);
            string prompt = GetPrompt(settings, presetIndex);
            string notes = settings.autoGenNotes ?? "";
            if (!string.IsNullOrEmpty(notes))
            {
                notes = DirectorUtils.RenderScribanText(notes, pawn);
            }

            ContextSettings context = category.GetEffectiveContext(settings.Context);
            string characterData = DirectorUtils.BuildCustomCharacterData(
                pawn,
                context,
                notes,
                false,
                false);
            if (!string.IsNullOrWhiteSpace(triggerContext))
            {
                characterData += "\n\n[Trigger Event]\n" + triggerContext.Trim();
            }

            string instruction = prompt.Replace("{LANG}", Constant.Lang)
                + "\n\n" + DirectorSettings.HiddenTechnicalPrompt_Single;

            return new TalkRequest(
                "[Character Data]\n" + characterData,
                pawn,
                null,
                TalkType.User)
            {
                Context = instruction
            };
        }

        private static TalkRequest TryBuildAdvancedPresetRequest(
            Pawn pawn,
            string presetName,
            string triggerContext)
        {
            try
            {
                var preset = RimTalkPromptAPI.GetAllPresets()
                    ?.FirstOrDefault(candidate => candidate.Name == presetName);
                if (preset == null)
                {
                    Log.Warning("[Persona Director] AutoGen RimTalk preset '"
                        + presetName + "' was not found; request skipped.");
                    return null;
                }

                var context = new PromptContext(
                    new List<Pawn> { pawn },
                    new VariableStore());
                context.CurrentPawn = pawn;
                var system = new StringBuilder();
                var user = new StringBuilder();

                string previousTriggerContext = DirectorApiAdapter.AdvancedTriggerContext;
                try
                {
                    DirectorApiAdapter.AdvancedTriggerContext = triggerContext?.Trim() ?? "";
                    foreach (var entry in preset.Entries)
                    {
                        if (entry == null || !entry.Enabled) continue;

                        string rendered = ScribanParser.Render(entry.Content, context, true);
                        if (string.IsNullOrWhiteSpace(rendered)) continue;

                        StringBuilder destination = entry.Role.ToString() == "System" ? system : user;
                        if (destination.Length > 0) destination.AppendLine().AppendLine();
                        destination.Append(rendered);
                    }
                }
                finally
                {
                    DirectorApiAdapter.AdvancedTriggerContext = previousTriggerContext;
                }

                if (user.Length == 0 && system.Length == 0)
                {
                    Log.Warning("[Persona Director] AutoGen RimTalk preset '"
                        + presetName + "' produced no prompt; request skipped.");
                    return null;
                }

                return new TalkRequest(
                    user.ToString(),
                    pawn,
                    null,
                    TalkType.User)
                {
                    Context = system.ToString()
                };
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Could not render AutoGen RimTalk preset '"
                    + presetName + "'; request skipped. " + ex.Message);
                return null;
            }
        }

        private static string GetPrompt(DirectorSettings settings, int presetIndex)
        {
            PromptPreset preset = settings.presets != null && settings.presets.Count > presetIndex
                ? settings.presets[presetIndex]
                : null;
            if (!string.IsNullOrEmpty(preset?.text)) return preset.text;

            if (presetIndex == 1) return DirectorSettings.DefaultPrompt_Simple;
            if (presetIndex == 2) return DirectorSettings.DefaultPrompt_Strict;
            return DirectorSettings.DefaultPrompt_Standard;
        }

        private static bool IsOperational()
        {
            DirectorSettings settings = DirectorMod.Settings;
            return DirectorFeatureGate.ExperimentalEnabled
                && settings != null
                && settings.autoGenEnabled;
        }

        private static bool IsStillAuthorized(ActiveWork work)
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings == null || work == null) return false;
            settings.EnsureAutoGenCategories();

            string currentCategoryId = AutoGenCategoryCatalog.GetPawnCategory(work.Pawn);
            AutoGenCategory category;
            if (!string.Equals(currentCategoryId, work.CategoryId, StringComparison.Ordinal)
                || string.IsNullOrEmpty(currentCategoryId)
                || !settings.autoGenCategories.TryGetValue(currentCategoryId, out category)
                || category == null)
            {
                return false;
            }

            return work.RoleChange
                ? IsRoleChangeAuthorized(category)
                : category.enabled;
        }

        private static bool IsRoleChangeAuthorized(AutoGenCategory category)
        {
            return category != null
                && category.onlyOnRoleChange
                && category.enabled;
        }

        private static bool IsValidPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && !DirectorUtils.UsesGlobalPlayerPersona(pawn)
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike;
        }

        internal static bool ShouldAutoGenerateExistingPawn(Pawn pawn)
        {
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            return pawn != null
                && world != null
                && world.IsCurrentPersonaInitialAssignment(pawn);
        }

        private static string CurrentPersonality(Pawn pawn)
        {
            return PersonaService.GetPersonality(pawn)?.Trim() ?? "";
        }

        private static string BuildRoleChangeContext(string oldCategory, string newCategory)
        {
            return "Role changed from " + (oldCategory ?? "Unknown")
                + " to " + newCategory + ".";
        }

        private void ClearRuntime()
        {
            ActiveWork work = _active;
            if (work != null) ObserveFault(work.Task);
            _active = null;
            DirectorAiTaskCoordinator.Release(work?.Lease);
            _queue.Clear();
            _queuedPawnIds.Clear();
            _queuedWorkByPawnId.Clear();
        }

        private static void ObserveFault(Task task)
        {
            if (task == null) return;
            task.ContinueWith(
                completed => { Exception ignored = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
