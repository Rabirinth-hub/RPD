using System.Collections.Generic;
using Verse;
using RimWorld.Planet;

namespace RimPersonaDirector
{
    public class DirectorWorldComponent : WorldComponent
    {
        private Dictionary<int, int> _lastEvolveTicks = new Dictionary<int, int>();
        private Dictionary<int, long> _lastEvolveBioAgeTicks = new Dictionary<int, long>(); 
        private Dictionary<int, string> _dataSnapshots = new Dictionary<int, string>();
        public int lastRuleCheckTick = 0;
        private HashSet<int> _processedPawnIds = new HashSet<int>();
        public DirectorWorldComponent(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _lastEvolveTicks, "lastEvolveTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _lastEvolveBioAgeTicks, "lastEvolveBioAgeTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dataSnapshots, "dataSnapshots", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastRuleCheckTick, "lastRuleCheckTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_lastEvolveTicks == null) _lastEvolveTicks = new Dictionary<int, int>();
                if (_lastEvolveBioAgeTicks == null) _lastEvolveBioAgeTicks = new Dictionary<int, long>();
                if (_dataSnapshots == null) _dataSnapshots = new Dictionary<int, string>();

            }
        }

        public void SetTimestamp(Pawn p, string snapshotData)
        {
            if (p == null) return;
            _lastEvolveTicks[p.thingIDNumber] = GenTicks.TicksGame;
            _lastEvolveBioAgeTicks[p.thingIDNumber] = p.ageTracker.AgeBiologicalTicks;

            if (!string.IsNullOrEmpty(snapshotData))
            {
                _dataSnapshots[p.thingIDNumber] = snapshotData;
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