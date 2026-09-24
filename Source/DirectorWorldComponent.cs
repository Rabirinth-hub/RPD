using RimWorld;
using RimWorld.Planet;
using RimTalk.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace RimPersonaDirector
{
    public class DirectorWorldComponent : WorldComponent
    {
        private const int HistoryBytesPerPawn = 262144;
        private const int HistoryBytesGlobal = 4194304;
        private const int InitialOrphanCleanupDelayTicks = 2500;
        private const int OrphanCleanupIntervalTicks = 60000;
        private const int NewPawnMapArrivalGraceTicks = 2500;

        private sealed class PendingNewPawnRegistration
        {
            public Pawn Pawn;
            public int DeadlineTick;
        }

        private Dictionary<int, int> _lastEvolveTicks = new Dictionary<int, int>();
        private Dictionary<int, long> _lastEvolveBioAgeTicks = new Dictionary<int, long>(); 
        private Dictionary<int, string> _dataSnapshots = new Dictionary<int, string>();
        private Dictionary<int, string> _dailySnapshots = new Dictionary<int, string>();
        private Dictionary<int, int> _dailySnapshotDays = new Dictionary<int, int>();
        private Dictionary<int, string> _initialPersonaBaselines = new Dictionary<int, string>();
        private Dictionary<int, AutoEvolveConfig> _autoConfigs = new Dictionary<int, AutoEvolveConfig>();
        private Dictionary<int, List<PersonaHistoryRecord>> _historyVault =
            new Dictionary<int, List<PersonaHistoryRecord>>();

        public int lastRuleCheckTick = 0;
        private HashSet<int> _processedPawnIds = new HashSet<int>();
        private readonly Dictionary<int, PendingNewPawnRegistration> _pendingNewPawnRegistrations =
            new Dictionary<int, PendingNewPawnRegistration>();
        private int _nextOrphanCleanupTick = -1;
        public DirectorWorldComponent(World world) : base(world) { }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            ProcessPendingNewPawnRegistrations();
            int now = GenTicks.TicksGame;
            if (_nextOrphanCleanupTick < 0)
            {
                _nextOrphanCleanupTick = now + InitialOrphanCleanupDelayTicks;
                return;
            }
            if (now < _nextOrphanCleanupTick) return;

            CleanupOrphanedPawnData();
            _nextOrphanCleanupTick = now + OrphanCleanupIntervalTicks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _lastEvolveTicks, "lastEvolveTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _lastEvolveBioAgeTicks, "lastEvolveBioAgeTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dataSnapshots, "dataSnapshots", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshots, "dailySnapshots", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshotDays, "dailySnapshotDays", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _initialPersonaBaselines, "initialPersonaBaselines", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _autoConfigs, "autoConfigs", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref _historyVault, "historyVault", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref _processedPawnIds, "processedPawnIds", LookMode.Value);
            Scribe_Values.Look(ref lastRuleCheckTick, "lastRuleCheckTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_lastEvolveTicks == null) _lastEvolveTicks = new Dictionary<int, int>();
                if (_lastEvolveBioAgeTicks == null) _lastEvolveBioAgeTicks = new Dictionary<int, long>();
                if (_dataSnapshots == null) _dataSnapshots = new Dictionary<int, string>();
                // 初始化
                if (_dailySnapshots == null) _dailySnapshots = new Dictionary<int, string>();
                if (_dailySnapshotDays == null) _dailySnapshotDays = new Dictionary<int, int>();
                if (_initialPersonaBaselines == null)
                    _initialPersonaBaselines = new Dictionary<int, string>();
                if (_autoConfigs == null) _autoConfigs = new Dictionary<int, AutoEvolveConfig>();
                if (_historyVault == null)
                    _historyVault = new Dictionary<int, List<PersonaHistoryRecord>>();
                if (_processedPawnIds == null) _processedPawnIds = new HashSet<int>();

                NormalizeAutoConfigs();
                PruneAllHistory();
            }
        }

        // --- A. 每日自动快照 (Daily) ---
        public void SaveDailySnapshot(Pawn p)
        {
            if (p == null) return;
            string snapshot = DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: true);

            _dailySnapshots[p.thingIDNumber] = snapshot;
            _dailySnapshotDays[p.thingIDNumber] = GenDate.DaysPassed;
        }

        public string GetOrUpdateDailyDiff(Pawn p)
        {
            if (p == null) return "";
            int id = p.thingIDNumber;
            int currentDay = GenDate.DaysPassed;

            // ★★★ 关键：获取当前状态时，也开启 simpleEquipment = true ★★★
            string currentSnapshot = DirectorUtils.BuildCustomCharacterData(p, true, true);

            // A. 初始化
            if (!_dailySnapshots.TryGetValue(id, out string storedSnapshot))
            {
                _dailySnapshots[id] = currentSnapshot;
                _dailySnapshotDays[id] = currentDay;
                return "Daily monitoring started just now.";
            }

            // B. 对比 (Simple vs Simple)
            string diff = DirectorUtils.GenerateDiffReport(storedSnapshot, currentSnapshot);

            // C. 换日逻辑
            int storedDay = _dailySnapshotDays.TryGetValue(id, out int val) ? val : -1;

            if (currentDay > storedDay)
            {
                _dailySnapshots[id] = currentSnapshot;
                _dailySnapshotDays[id] = currentDay;
                return diff == "No significant changes." ? "No changes since yesterday." : diff;
            }

            return diff == "No significant changes." ? "No changes today." : diff;
        }

        // --- B. 手动快照 (Evolve) ---
        public void SetTimestamp(Pawn p, string snapshotData = null) 
        {
            if (!DirectorAutoEvolveTriggerService.IsPawnOnMap(p)) return;
            string snapshot = snapshotData
                ?? DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: false);

            int id = p.thingIDNumber;
            _lastEvolveTicks[id] = GenTicks.TicksGame;
            _lastEvolveBioAgeTicks[id] = p.ageTracker.AgeBiologicalTicks;
            _dataSnapshots[id] = snapshot;

            AutoEvolveConfig config;
            if (_autoConfigs.TryGetValue(id, out config) && config != null && config.enabled)
            {
                config.ScheduleFromNow(GenTicks.TicksGame);
            }
        }

        public string GetSnapshot(Pawn p)
        {
            if (p != null && _dataSnapshots.TryGetValue(p.thingIDNumber, out string data))
            {
                return data;
            }
            return null;
        }

        public int GetLastEvolveTick(Pawn p)
        {
            if (p != null && _lastEvolveTicks.TryGetValue(p.thingIDNumber, out int tick)) return tick;
            return -1;
        }

        public long GetLastEvolveBioAgeTicks(Pawn p)
        {
            if (p != null && _lastEvolveBioAgeTicks.TryGetValue(p.thingIDNumber, out long ageTicks)) return ageTicks;
            return -1;
        }

        public void RecordInitialPersona(Pawn pawn, string persona)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(persona)) return;
            _initialPersonaBaselines[pawn.thingIDNumber] = persona.Trim();
            _processedPawnIds.Remove(pawn.thingIDNumber);
        }

        public bool HasInitialPersonaBaseline(Pawn pawn)
        {
            return pawn != null && _initialPersonaBaselines.ContainsKey(pawn.thingIDNumber);
        }

        public bool IsCurrentPersonaInitialAssignment(Pawn pawn)
        {
            if (pawn == null || HasBeenProcessed(pawn) || GetLastEvolveTick(pawn) >= 0) return false;

            string current = PersonaService.GetPersonality(pawn)?.Trim() ?? "";
            if (string.IsNullOrEmpty(current)) return false;

            string baseline;
            if (_initialPersonaBaselines.TryGetValue(pawn.thingIDNumber, out baseline))
            {
                return string.Equals(current, baseline?.Trim() ?? "", StringComparison.Ordinal);
            }

            if (!Patch_NewPawnPersona.IsPersonaFromCurrentAssignmentPools(pawn, current))
            {
                return false;
            }

            _initialPersonaBaselines[pawn.thingIDNumber] = current;
            return true;
        }

        public bool HasBeenProcessed(Pawn p)
        {
            return p != null && _processedPawnIds.Contains(p.thingIDNumber);
        }

        public void MarkAsProcessed(Pawn p)
        {
            if (p != null)
            {
                _processedPawnIds.Add(p.thingIDNumber);
            }
        }

        public AutoEvolveConfig GetAutoConfig(Pawn pawn)
        {
            if (pawn == null) return null;

            AutoEvolveConfig config;
            if (!_autoConfigs.TryGetValue(pawn.thingIDNumber, out config) || config == null)
            {
                config = CreateAutoConfig(false);
                _autoConfigs[pawn.thingIDNumber] = config;
            }

            return config;
        }

        public AutoEvolveConfig RegisterNewPawn(Pawn pawn)
        {
            if (!DirectorAutoEvolveTriggerService.IsAutoEvolveEligiblePawn(pawn)) return null;

            AutoEvolveConfig config;
            if (!_autoConfigs.TryGetValue(pawn.thingIDNumber, out config) || config == null)
            {
                bool defaultEnabled = DirectorMod.Settings?.autoEvolveDefaultEnabled ?? false;
                if (!defaultEnabled) return null;
                config = CreateAutoConfig(defaultEnabled);
                _autoConfigs[pawn.thingIDNumber] = config;
            }

            return config;
        }

        public void QueueNewPawnRegistration(Pawn pawn)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || !DirectorAutoEvolveTriggerService.IsAutoEvolveCandidatePawn(pawn))
            {
                return;
            }

            _pendingNewPawnRegistrations[pawn.thingIDNumber] = new PendingNewPawnRegistration
            {
                Pawn = pawn,
                DeadlineTick = GenTicks.TicksGame + NewPawnMapArrivalGraceTicks
            };
        }

        public bool TryGetAutoConfig(Pawn pawn, out AutoEvolveConfig config)
        {
            config = null;
            return pawn != null
                && _autoConfigs.TryGetValue(pawn.thingIDNumber, out config)
                && config != null;
        }

        public List<PersonaHistoryRecord> GetHistory(Pawn pawn)
        {
            var snapshot = new List<PersonaHistoryRecord>();
            if (pawn == null) return snapshot;

            List<PersonaHistoryRecord> records;
            if (!_historyVault.TryGetValue(pawn.thingIDNumber, out records) || records == null)
                return snapshot;

            foreach (PersonaHistoryRecord record in records)
            {
                if (record != null) snapshot.Add(record.Copy());
            }
            return snapshot;
        }

        public bool AddHistory(Pawn pawn, string persona, string diffSnapshot)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || pawn == null
                || !pawn.Spawned
                || pawn.Map == null
                || DirectorUtils.UsesGlobalPlayerPersona(pawn)
                || string.IsNullOrWhiteSpace(persona))
            {
                return false;
            }

            var record = new PersonaHistoryRecord
            {
                timestampTick = GenTicks.TicksGame,
                personaText = persona.Trim(),
                diffSnapshot = diffSnapshot ?? ""
            };
            if (EstimateBytes(record) > HistoryBytesPerPawn)
            {
                Log.Warning("[Persona Director] History record exceeded the per-pawn safety limit and was skipped.");
                return false;
            }

            List<PersonaHistoryRecord> records;
            if (!_historyVault.TryGetValue(pawn.thingIDNumber, out records) || records == null)
            {
                records = new List<PersonaHistoryRecord>();
                _historyVault[pawn.thingIDNumber] = records;
            }

            records.Insert(0, record);
            PrunePawnHistory(records);
            PruneGlobalHistory();
            return true;
        }

        public void ApplyHistoryLimits()
        {
            PruneAllHistory();
        }

        private void CleanupOrphanedPawnData()
        {
            var retainedPawnIds = new HashSet<int>();
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn != null) retainedPawnIds.Add(pawn.thingIDNumber);
            }

            RemoveMissingKeys(_lastEvolveTicks, retainedPawnIds);
            RemoveMissingKeys(_lastEvolveBioAgeTicks, retainedPawnIds);
            RemoveMissingKeys(_dataSnapshots, retainedPawnIds);
            RemoveMissingKeys(_dailySnapshots, retainedPawnIds);
            RemoveMissingKeys(_dailySnapshotDays, retainedPawnIds);
            RemoveMissingKeys(_initialPersonaBaselines, retainedPawnIds);
            RemoveMissingKeys(_autoConfigs, retainedPawnIds);
            RemoveMissingKeys(_historyVault, retainedPawnIds);
            _processedPawnIds.RemoveWhere(pawnId => !retainedPawnIds.Contains(pawnId));
        }

        private void ProcessPendingNewPawnRegistrations()
        {
            if (_pendingNewPawnRegistrations.Count == 0) return;
            if (!DirectorFeatureGate.ExperimentalEnabled)
            {
                _pendingNewPawnRegistrations.Clear();
                return;
            }

            int now = GenTicks.TicksGame;
            foreach (int pawnId in new List<int>(_pendingNewPawnRegistrations.Keys))
            {
                PendingNewPawnRegistration pending = _pendingNewPawnRegistrations[pawnId];
                Pawn pawn = pending?.Pawn;
                if (pawn == null || pawn.Destroyed || pawn.Dead)
                {
                    _pendingNewPawnRegistrations.Remove(pawnId);
                    continue;
                }
                if (DirectorAutoEvolveTriggerService.IsPawnOnMap(pawn))
                {
                    RegisterNewPawn(pawn);
                    _pendingNewPawnRegistrations.Remove(pawnId);
                    continue;
                }
                if (pawn.IsWorldPawn() || now > pending.DeadlineTick)
                {
                    _pendingNewPawnRegistrations.Remove(pawnId);
                }
            }
        }

        private static void RemoveMissingKeys<T>(Dictionary<int, T> values, HashSet<int> retainedPawnIds)
        {
            if (values == null) return;
            foreach (int pawnId in new List<int>(values.Keys))
            {
                if (!retainedPawnIds.Contains(pawnId)) values.Remove(pawnId);
            }
        }

        private void NormalizeAutoConfigs()
        {
            foreach (int pawnId in new List<int>(_autoConfigs.Keys))
            {
                AutoEvolveConfig config = _autoConfigs[pawnId];
                if (config == null)
                {
                    _autoConfigs.Remove(pawnId);
                    continue;
                }

                config.intervalDays = AutoEvolveConfig.NormalizeInterval(config.intervalDays);
                config.daysBuffer = config.intervalDays.ToString();
                if (!config.TimedUpdatesEnabled) config.nextUpdateTick = -1;
            }
        }

        private static AutoEvolveConfig CreateAutoConfig(bool enabled)
        {
            var config = new AutoEvolveConfig
            {
                enabled = enabled
            };
            config.ScheduleFromNow(Find.TickManager?.TicksGame ?? 0);
            return config;
        }

        private void PruneAllHistory()
        {
            foreach (int pawnId in new List<int>(_historyVault.Keys))
            {
                List<PersonaHistoryRecord> records = _historyVault[pawnId];
                if (records == null)
                {
                    _historyVault.Remove(pawnId);
                    continue;
                }

                records.RemoveAll(record => record == null || string.IsNullOrWhiteSpace(record.personaText));
                PrunePawnHistory(records);
                if (records.Count == 0) _historyVault.Remove(pawnId);
            }
            PruneGlobalHistory();
        }

        private static int EstimateBytes(PersonaHistoryRecord record)
        {
            if (record == null) return 0;
            return Encoding.UTF8.GetByteCount(record.personaText ?? "")
                + Encoding.UTF8.GetByteCount(record.diffSnapshot ?? "")
                + 64;
        }

        private static int TotalBytes(List<PersonaHistoryRecord> records)
        {
            int total = 0;
            foreach (PersonaHistoryRecord record in records) total += EstimateBytes(record);
            return total;
        }

        private static void PrunePawnHistory(List<PersonaHistoryRecord> records)
        {
            int maxRecords = DirectorMod.Settings?.personaHistoryMaxRecords ?? 5;
            int totalBytes = TotalBytes(records);
            while (records.Count > 0
                && ((maxRecords > 0 && records.Count > maxRecords)
                    || totalBytes > HistoryBytesPerPawn))
            {
                totalBytes -= EstimateBytes(records[records.Count - 1]);
                records.RemoveAt(records.Count - 1);
            }
        }

        private void PruneGlobalHistory()
        {
            int totalBytes = GetGlobalHistoryBytes();
            while (totalBytes > HistoryBytesGlobal)
            {
                List<PersonaHistoryRecord> oldestList = null;
                int oldestTick = int.MaxValue;

                foreach (List<PersonaHistoryRecord> records in _historyVault.Values)
                {
                    if (records == null || records.Count == 0) continue;
                    PersonaHistoryRecord candidate = records[records.Count - 1];
                    if (candidate != null && candidate.timestampTick < oldestTick)
                    {
                        oldestTick = candidate.timestampTick;
                        oldestList = records;
                    }
                }

                if (oldestList == null) return;
                totalBytes -= EstimateBytes(oldestList[oldestList.Count - 1]);
                oldestList.RemoveAt(oldestList.Count - 1);
            }
        }

        private int GetGlobalHistoryBytes()
        {
            int total = 0;
            foreach (List<PersonaHistoryRecord> records in _historyVault.Values)
            {
                if (records != null) total += TotalBytes(records);
            }
            return total;
        }
    }
}
