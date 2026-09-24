using System;
using HarmonyLib;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorPatchRegistry
    {
        public const string CoreHarmonyId = "com.yourname.rimtalk.director";
        public const string ExperimentalHarmonyId = "com.yourname.rimtalk.director.experimental";
        public const string ExperimentalCategory = "RimPersonaDirector.Experimental";

        private static readonly object SyncRoot = new object();
        private static bool _corePatchesApplied;
        private static bool _experimentalPatchesApplied;

        public static void ApplyCorePatches()
        {
            lock (SyncRoot)
            {
                if (_corePatchesApplied)
                {
                    return;
                }

                var harmony = new Harmony(CoreHarmonyId);
                harmony.PatchAllUncategorized(typeof(DirectorPatchRegistry).Assembly);
                _corePatchesApplied = true;
            }
        }

        public static bool TryApplyExperimentalPatches()
        {
            lock (SyncRoot)
            {
                if (_experimentalPatchesApplied)
                {
                    return true;
                }

                try
                {
                    var harmony = new Harmony(ExperimentalHarmonyId);
                    harmony.PatchCategory(
                        typeof(DirectorPatchRegistry).Assembly,
                        ExperimentalCategory);
                    _experimentalPatchesApplied = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error("[Persona Director] Experimental patch registration failed: " + ex);
                    return false;
                }
            }
        }

        public static bool RemoveExperimentalPatches()
        {
            lock (SyncRoot)
            {
                try
                {
                    var harmony = new Harmony(ExperimentalHarmonyId);
                    harmony.UnpatchAll(ExperimentalHarmonyId);
                    _experimentalPatchesApplied = false;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error("[Persona Director] Experimental unpatch failed: " + ex);
                    return false;
                }
            }
        }
    }
}
