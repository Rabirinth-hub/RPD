using RimTalk.Data;
using RimTalk.Service;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimPersonaDirector
{
    public sealed class DirectorAutoEvolveManager : WorldComponent
    {
        private const int ScanIntervalTicks = 2500;
        private const int TaskTimeoutTicks = 10000;
        private const string TaskOwner = "AutoEvolve";

        private sealed class PendingWork
        {
            public Pawn Pawn;
            public int PawnId;
            public long Epoch;
            public readonly List<string> TriggerContexts = new List<string>();
            public readonly HashSet<string> TriggerKeys = new HashSet<string>();
        }

        private sealed class ActiveWork
        {
            public Pawn Pawn;
            public int PawnId;
            public long Epoch;
            public int StartTick;
            public string OriginalPersona;
            public string HistoryContext;
            public Task<PersonalityData> Task;
            public DirectorAiTaskCoordinator.Lease Lease;
        }

        private readonly Queue<PendingWork> _queue = new Queue<PendingWork>();
        private readonly HashSet<int> _queuedPawnIds = new HashSet<int>();
        private readonly Dictionary<int, PendingWork> _queuedWorkByPawnId =
            new Dictionary<int, PendingWork>();
        private ActiveWork _active;
        private int _nextScanTick;

        public DirectorAutoEvolveManager(World world) : base(world)
        {
            DirectorAutoEvolveTriggerService.ResetForNewWorld();
            DirectorFeatureGate.RegisterTransientCleanup(ClearCurrentWorld);
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            DirectorAutoEvolveTriggerService.FlushRoleChanges();
            DirectorAutoEvolveTriggerService.FlushBirths();

            if (!IsOperational())
            {
                ClearRuntime();
                return;
            }

            try
            {
                if (_active != null) PollActiveTask();
                if (_active != null || !IsOperational() || IsSpeedProtected()) return;

                int now = GenTicks.TicksGame;
                if (_nextScanTick <= now)
                {
                    DirectorAutoEvolveTriggerService.ScanTraitChanges();
                    EnqueueDuePawns();
                    _nextScanTick = now + ScanIntervalTicks;
                }

                StartNextTask();
            }
            catch (Exception ex)
            {
                ClearRuntime();
                DirectorFeatureGate.Trip("RPD_Experimental_AutoEvolveRuntimeFailed", ex);
            }
        }

        public bool IsPending(Pawn pawn)
        {
            if (pawn == null) return false;
            int pawnId = pawn.thingIDNumber;
            return _queuedPawnIds.Contains(pawnId)
                || (_active != null && _active.PawnId == pawnId);
        }

        public bool EnqueueTriggeredPawn(Pawn pawn, string triggerKey, string triggerContext)
        {
            if (!IsOperational()
                || !IsValidPawn(pawn)
                || string.IsNullOrWhiteSpace(triggerKey)
                || string.IsNullOrWhiteSpace(triggerContext))
            {
                return false;
            }

            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            AutoEvolveConfig config;
            if (world == null
                || !world.TryGetAutoConfig(pawn, out config)
                || !config.enabled)
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            PendingWork pending;
            if (!_queuedWorkByPawnId.TryGetValue(pawnId, out pending))
            {
                pending = new PendingWork
                {
                    Pawn = pawn,
                    PawnId = pawnId,
                    Epoch = DirectorFeatureGate.CaptureEpoch()
                };
                _queue.Enqueue(pending);
                _queuedPawnIds.Add(pawnId);
                _queuedWorkByPawnId[pawnId] = pending;
            }

            if (pending.TriggerKeys.Add(triggerKey) && pending.TriggerContexts.Count < 8)
            {
                pending.TriggerContexts.Add(triggerContext.Trim());
            }
            return true;
        }

        internal static void ClearCurrentWorld()
        {
            Find.World?.GetComponent<DirectorAutoEvolveManager>()?.ClearRuntime();
        }

        private void EnqueueDuePawns()
        {
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            if (world == null) return;

            int now = GenTicks.TicksGame;
            foreach (Pawn pawn in PawnsFinder.AllMaps_Spawned)
            {
                if (!IsValidPawn(pawn)) continue;

                AutoEvolveConfig config;
                if (!world.TryGetAutoConfig(pawn, out config)
                    || !config.enabled
                    || !config.TimedUpdatesEnabled
                    || config.nextUpdateTick <= 0
                    || config.nextUpdateTick > now)
                {
                    continue;
                }

                int pawnId = pawn.thingIDNumber;
                if ((_active != null && _active.PawnId == pawnId)
                    || _queuedPawnIds.Contains(pawnId))
                {
                    continue;
                }

                var pending = new PendingWork
                {
                    Pawn = pawn,
                    PawnId = pawnId,
                    Epoch = DirectorFeatureGate.CaptureEpoch()
                };
                _queue.Enqueue(pending);
                _queuedPawnIds.Add(pawnId);
                _queuedWorkByPawnId[pawnId] = pending;
            }
        }

        private void StartNextTask()
        {
            DirectorAiTaskCoordinator.Lease lease;
            if (_queue.Count == 0
                || AIService.IsBusy()
                || !DirectorAiTaskCoordinator.TryAcquire(TaskOwner, world, out lease))
            {
                return;
            }

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

                    DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
                    AutoEvolveConfig config;
                    if (world == null
                        || !world.TryGetAutoConfig(pending.Pawn, out config)
                        || !config.enabled)
                    {
                        continue;
                    }

                    string triggerContext = pending.TriggerContexts.Count == 0
                        ? ""
                        : string.Join("\n", pending.TriggerContexts);
                    DirectorEvolveRequest package = DirectorEvolveRequestBuilder.Build(
                        pending.Pawn,
                        triggerContext);
                    if (package?.Request == null)
                    {
                        Reschedule(pending.Pawn);
                        continue;
                    }

                    Task<PersonalityData> task = AIService.Query<PersonalityData>(package.Request);
                    if (task == null)
                    {
                        Reschedule(pending.Pawn);
                        continue;
                    }

                    _active = new ActiveWork
                    {
                        Pawn = pending.Pawn,
                        PawnId = pending.PawnId,
                        Epoch = pending.Epoch,
                        StartTick = GenTicks.TicksGame,
                        OriginalPersona = package.OriginalPersona,
                        HistoryContext = package.HistoryContext,
                        Task = task,
                        Lease = lease
                    };
                    taskStarted = true;

                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Message("[Persona Director] Auto-Evolve request started for "
                            + pending.Pawn.LabelShortCap + ".");
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
                    DirectorFeatureGate.Trip("RPD_Experimental_AutoEvolveTimeout", null);
                }
                return;
            }

            _active = null;
            try
            {
                if (!DirectorFeatureGate.IsCurrentEpoch(work.Epoch)
                    || !IsOperational()
                    || !IsValidPawn(work.Pawn))
                {
                    ObserveFault(work.Task);
                    return;
                }

                if (work.Task.IsCanceled)
                {
                    Reschedule(work.Pawn);
                    return;
                }
                if (work.Task.IsFaulted)
                {
                    Exception error = work.Task.Exception?.GetBaseException();
                    Log.Warning("[Persona Director] Auto-Evolve request failed for "
                        + work.Pawn.LabelShortCap + ": " + error?.Message);
                    Reschedule(work.Pawn);
                    return;
                }

                PersonalityData result = work.Task.GetAwaiter().GetResult();
                string generated = result?.Persona?.Trim();
                if (string.IsNullOrEmpty(generated))
                {
                    Reschedule(work.Pawn);
                    return;
                }
                string current = PersonaService.GetPersonality(work.Pawn)?.Trim() ?? "";
                if (!string.Equals(current, work.OriginalPersona, StringComparison.Ordinal))
                {
                    Reschedule(work.Pawn);
                    return;
                }

                string finalPersona = BuildFinalPersona(work.OriginalPersona, generated);
                bool applied = DirectorHistoryService.ApplyWithHistory(
                    work.Pawn,
                    finalPersona,
                    work.HistoryContext,
                    true);
                Reschedule(work.Pawn);
                if (!applied) return;

                if (DirectorMod.Settings.autoNotify == AutoEvolveNotify.TopLeftMessage)
                {
                    Messages.Message(
                        "RPD_Msg_UpdatedSuccess".Translate(),
                        work.Pawn,
                        MessageTypeDefOf.PositiveEvent,
                        false);
                }
            }
            finally
            {
                DirectorAiTaskCoordinator.Release(work.Lease);
            }
        }

        private static string BuildFinalPersona(string original, string generated)
        {
            if (DirectorMod.Settings.autoMode == AutoEvolveMode.Overwrite) return generated;
            return original + "\n\n[Auto-Development]: " + generated;
        }

        private static void Reschedule(Pawn pawn)
        {
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            AutoEvolveConfig config;
            if (world == null || !world.TryGetAutoConfig(pawn, out config) || !config.enabled) return;

            config.ScheduleFromNow(GenTicks.TicksGame);
        }

        private static bool IsSpeedProtected()
        {
            if (Find.TickManager == null) return false;
            TimeSpeed speed = Find.TickManager.CurTimeSpeed;
            SpeedProtection protection = DirectorMod.Settings.speedProtection;
            if (protection == SpeedProtection.Speed2X && speed >= TimeSpeed.Fast) return true;
            if (protection == SpeedProtection.Speed3X && speed >= TimeSpeed.Superfast) return true;
            return protection == SpeedProtection.Speed4X && speed >= TimeSpeed.Ultrafast;
        }

        private static bool IsOperational()
        {
            DirectorSettings settings = DirectorMod.Settings;
            return DirectorFeatureGate.ExperimentalEnabled
                && settings != null
                && settings.globalAutoEvolveEnabled;
        }

        private static bool IsValidPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && !DirectorUtils.UsesGlobalPlayerPersona(pawn)
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && DirectorAutoEvolveTriggerService.IsAutoEvolveEligiblePawn(pawn)
                && !string.IsNullOrWhiteSpace(PersonaService.GetPersonality(pawn));
        }

        private void ClearRuntime()
        {
            ActiveWork work = _active;
            if (work != null) ObserveFault(work.Task);
            _active = null;
            _queue.Clear();
            _queuedPawnIds.Clear();
            _queuedWorkByPawnId.Clear();
            _nextScanTick = 0;
            DirectorAiTaskCoordinator.Release(work?.Lease);
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
