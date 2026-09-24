using HarmonyLib;
using RimTalk.Data;
using RimTalk.UI;
using System;
using System.Reflection;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(PersonaEditorWindow), "DoWindowContents")]
    internal static class Patch_PersonaHistory
    {
        private static readonly FieldInfo PawnField =
            AccessTools.Field(typeof(PersonaEditorWindow), "_pawn");

        private sealed class HistoryState
        {
            public Pawn Pawn;
            public string Previous;
        }

        [HarmonyPrefix]
        private static void CapturePrevious(PersonaEditorWindow __instance, ref HistoryState __state)
        {
            try
            {
                Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
                if (!DirectorFeatureGate.ExperimentalEnabled
                    || DirectorHistoryService.IsSuppressed
                    || pawn == null
                    || pawn.Destroyed
                    || !pawn.Spawned
                    || pawn.Map == null
                    || DirectorUtils.UsesGlobalPlayerPersona(pawn))
                {
                    return;
                }

                string previous = PersonaService.GetPersonality(pawn) ?? "";
                __state = new HistoryState
                {
                    Pawn = pawn,
                    Previous = previous
                };
            }
            catch (Exception ex)
            {
                __state = null;
                Log.Error("[Persona Director] Could not capture persona history: " + ex);
            }
        }

        [HarmonyPostfix]
        private static void CommitHistory(HistoryState __state)
        {
            if (__state == null) return;

            try
            {
                Pawn pawn = __state.Pawn;
                if (!DirectorAutoEvolveTriggerService.IsPawnOnMap(pawn)) return;
                string applied = PersonaService.GetPersonality(pawn) ?? "";
                if (string.Equals(applied.Trim(), __state.Previous.Trim(), StringComparison.Ordinal)) return;

                DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
                if (world == null) return;
                world.MarkAsProcessed(pawn);

                string snapshot = DirectorUtils.BuildCustomCharacterData(pawn, true, false);
                string context = "RPD_ManualEdit".Translate() + "\n\n" + snapshot;
                if (!string.IsNullOrWhiteSpace(__state.Previous))
                {
                    world.AddHistory(pawn, __state.Previous, context);
                }
                world.SetTimestamp(
                    pawn,
                    snapshot);
            }
            catch (Exception ex)
            {
                Log.Error("[Persona Director] Could not commit persona history: " + ex);
            }
        }
    }
}
