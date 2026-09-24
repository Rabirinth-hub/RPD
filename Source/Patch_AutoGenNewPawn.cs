using HarmonyLib;
using RimTalk.Data;
using RimWorld;
using System;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup),
        new[] { typeof(Map), typeof(bool) })]
    [HarmonyAfter(DirectorPatchRegistry.CoreHarmonyId)]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_AutoGenNewPawn
    {
        [HarmonyPostfix]
        private static void QueueAutoGeneration(Pawn __instance, bool respawningAfterLoad)
        {
            try
            {
                DirectorSettings settings = DirectorMod.Settings;
                DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
                if (DirectorFeatureGate.ExperimentalEnabled
                    && !respawningAfterLoad
                    && DirectorAutoEvolveTriggerService.IsPawnOnMap(__instance)
                    && DirectorAutoEvolveTriggerService.HasPlayerFaction()
                    && !__instance.Destroyed
                    && !__instance.Dead
                    && __instance.RaceProps != null
                    && __instance.RaceProps.Humanlike
                    && !DirectorUtils.UsesGlobalPlayerPersona(__instance))
                {
                    world?.QueueNewPawnRegistration(__instance);
                }

                if (!DirectorFeatureGate.ExperimentalEnabled
                    || respawningAfterLoad
                    || settings == null
                    || !settings.autoGenEnabled
                    || !DirectorAutoEvolveTriggerService.IsPawnOnMap(__instance)
                    || !DirectorAutoEvolveTriggerService.HasPlayerFaction()
                    || __instance.Destroyed
                    || __instance.Dead
                    || __instance.RaceProps == null
                    || !__instance.RaceProps.Humanlike
                    || world == null
                    || !world.HasInitialPersonaBaseline(__instance))
                {
                    return;
                }

                Find.World?.GetComponent<DirectorAutoGenManager>()?.EnqueuePawn(__instance);
            }
            catch (Exception ex)
            {
                DirectorFeatureGate.Trip("RPD_Experimental_AutoGenRuntimeFailed", ex);
            }
        }
    }
}
