using System;
using System.Collections.Generic;
using System.Threading;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorFeatureGate
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<Action> TransientCleanups = new List<Action>();
        private static long _runtimeEpoch;

        public static bool ExperimentalEnabled
        {
            get
            {
                var settings = DirectorMod.Settings;
                return settings != null
                    && settings.experimentalFeaturesEnabled
                    && !settings.experimentalCircuitBroken;
            }
        }

        public static long CaptureEpoch()
        {
            return Interlocked.Read(ref _runtimeEpoch);
        }

        public static bool IsCurrentEpoch(long capturedEpoch)
        {
            return ExperimentalEnabled && capturedEpoch == CaptureEpoch();
        }

        public static void InvalidateRuntime()
        {
            Interlocked.Increment(ref _runtimeEpoch);
        }

        public static void ResetTransientWork()
        {
            InvalidateAndCleanup();
        }

        public static void RegisterTransientCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            lock (SyncRoot)
            {
                if (!TransientCleanups.Contains(cleanup))
                {
                    TransientCleanups.Add(cleanup);
                }
            }
        }

        public static void ReconcileOnStartup()
        {
            if (!ExperimentalEnabled)
            {
                var settings = DirectorMod.Settings;
                if (!DirectorPatchRegistry.RemoveExperimentalPatches() && settings != null)
                {
                    RecordUnpatchFailure(settings);
                    settings.Write();
                }
                return;
            }

            if (!DirectorPatchRegistry.TryApplyExperimentalPatches())
            {
                Trip("RPD_Experimental_PatchRegistrationFailed", null);
            }
        }

        public static void SetEnabled(bool enabled)
        {
            var settings = DirectorMod.Settings;
            if (settings == null)
            {
                return;
            }

            if (enabled && settings.experimentalCircuitBroken)
            {
                return;
            }

            settings.experimentalFeaturesEnabled = enabled;
            InvalidateAndCleanup();

            if (enabled)
            {
                if (!DirectorPatchRegistry.TryApplyExperimentalPatches())
                {
                    Trip("RPD_Experimental_PatchRegistrationFailed", null);
                    return;
                }
            }
            else
            {
                if (!DirectorPatchRegistry.RemoveExperimentalPatches())
                {
                    RecordUnpatchFailure(settings);
                }
            }

            settings.Write();
        }

        public static void EnterSafeMode(string reason)
        {
            var settings = DirectorMod.Settings;
            if (settings == null)
            {
                return;
            }

            settings.experimentalFeaturesEnabled = false;
            InvalidateAndCleanup();
            if (!DirectorPatchRegistry.RemoveExperimentalPatches())
            {
                RecordUnpatchFailure(settings);
            }
            settings.Write();

            if (!string.IsNullOrEmpty(reason))
            {
                Log.Message("[Persona Director] Experimental features disabled: " + reason);
            }
        }

        public static void Trip(string reason, Exception error)
        {
            var settings = DirectorMod.Settings;
            if (settings == null)
            {
                return;
            }

            settings.experimentalCircuitBroken = true;
            settings.experimentalCircuitBreakReason = reason ?? "Experimental feature failure.";
            settings.experimentalFeaturesEnabled = false;

            InvalidateAndCleanup();
            if (!DirectorPatchRegistry.RemoveExperimentalPatches())
            {
                RecordUnpatchFailure(settings);
            }
            settings.Write();

            string detail = error == null ? "" : " " + error;
            Log.Error("[Persona Director] Experimental feature circuit opened: "
                + settings.experimentalCircuitBreakReason + detail);
        }

        public static void ClearCircuitBreak()
        {
            var settings = DirectorMod.Settings;
            if (settings == null)
            {
                return;
            }

            settings.experimentalCircuitBroken = false;
            settings.experimentalCircuitBreakReason = "";
            settings.experimentalFeaturesEnabled = false;

            InvalidateAndCleanup();
            if (!DirectorPatchRegistry.RemoveExperimentalPatches())
            {
                RecordUnpatchFailure(settings);
            }
            settings.Write();
        }

        private static void RecordUnpatchFailure(DirectorSettings settings)
        {
            settings.experimentalCircuitBroken = true;
            settings.experimentalFeaturesEnabled = false;
            settings.experimentalCircuitBreakReason = "RPD_Experimental_UnpatchFailed";
        }

        private static void InvalidateAndCleanup()
        {
            InvalidateRuntime();

            Action[] cleanups;
            lock (SyncRoot)
            {
                cleanups = TransientCleanups.ToArray();
            }

            foreach (var cleanup in cleanups)
            {
                try
                {
                    cleanup();
                }
                catch (Exception ex)
                {
                    Log.Error("[Persona Director] Experimental cleanup failed: " + ex);
                }
            }
        }
    }
}
