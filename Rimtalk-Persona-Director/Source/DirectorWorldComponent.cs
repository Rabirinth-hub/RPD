using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace RimPersonaDirector
{
    public class DirectorWorldComponent : WorldComponent
    {
        private Dictionary<int, int> _lastEvolveTicks = new Dictionary<int, int>();
        private Dictionary<int, long> _lastEvolveBioAgeTicks = new Dictionary<int, long>(); 
        private Dictionary<int, string> _dataSnapshots = new Dictionary<int, string>();
        private Dictionary<int, string> _dailySnapshots = new Dictionary<int, string>();
        private Dictionary<int, int> _dailySnapshotDays = new Dictionary<int, int>();

        public int lastRuleCheckTick = 0;
        private HashSet<int> _processedPawnIds = new HashSet<int>();
        public DirectorWorldComponent(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _lastEvolveTicks, "lastEvolveTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _lastEvolveBioAgeTicks, "lastEvolveBioAgeTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dataSnapshots, "dataSnapshots", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshots, "dailySnapshots", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshotDays, "dailySnapshotDays", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastRuleCheckTick, "lastRuleCheckTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_lastEvolveTicks == null) _lastEvolveTicks = new Dictionary<int, int>();
                if (_lastEvolveBioAgeTicks == null) _lastEvolveBioAgeTicks = new Dictionary<int, long>();
                if (_dataSnapshots == null) _dataSnapshots = new Dictionary<int, string>();
                // 初始化
                if (_dailySnapshots == null) _dailySnapshots = new Dictionary<int, string>();
                if (_dailySnapshotDays == null) _dailySnapshotDays = new Dictionary<int, int>();
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
            if (p == null) return;
            string snapshot = DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: false);

            int id = p.thingIDNumber;
            _lastEvolveTicks[id] = GenTicks.TicksGame;
            _lastEvolveBioAgeTicks[id] = p.ageTracker.AgeBiologicalTicks;
            _dataSnapshots[id] = snapshot;
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
    }
}