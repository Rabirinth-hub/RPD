using RimWorld.Planet;

namespace RimPersonaDirector
{
    internal static class DirectorAiTaskCoordinator
    {
        internal sealed class Lease
        {
            internal readonly string Owner;
            internal readonly World World;

            internal Lease(string owner, World world)
            {
                Owner = owner;
                World = world;
            }
        }

        private static readonly object SyncRoot = new object();
        private static Lease _active;

        public static bool TryAcquire(string owner, World world, out Lease lease)
        {
            lease = null;
            if (world == null) return false;

            lock (SyncRoot)
            {
                if (_active != null && !ReferenceEquals(_active.World, world))
                    _active = null;
                if (_active != null) return false;

                lease = new Lease(owner, world);
                _active = lease;
                return true;
            }
        }

        public static void Release(Lease lease)
        {
            if (lease == null) return;
            lock (SyncRoot)
            {
                if (ReferenceEquals(_active, lease)) _active = null;
            }
        }
    }
}
