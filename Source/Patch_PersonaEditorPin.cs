using HarmonyLib;
using RimTalk.UI;
using System.Runtime.CompilerServices;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorPersonaEditorPin
    {
        // RimTalk normally follows the map selection; a roster editor must keep its row's pawn.
        private sealed class Target
        {
            internal Pawn Pawn;
        }

        private static readonly ConditionalWeakTable<PersonaEditorWindow, Target> Targets =
            new ConditionalWeakTable<PersonaEditorWindow, Target>();

        internal static void Open(Pawn pawn)
        {
            var window = new PersonaEditorWindow(pawn);
            Targets.Add(window, new Target { Pawn = pawn });
            Find.WindowStack.Add(window);
        }

        internal static bool AllowTargetChange(PersonaEditorWindow window, Pawn pawn)
        {
            return !Targets.TryGetValue(window, out Target target)
                || target.Pawn == pawn;
        }
    }

    [HarmonyPatch(typeof(PersonaEditorWindow), "SetTargetPawn")]
    internal static class Patch_PersonaEditorPin
    {
        [HarmonyPrefix]
        private static bool Prefix(PersonaEditorWindow __instance, Pawn pawn)
        {
            return DirectorPersonaEditorPin.AllowTargetChange(__instance, pawn);
        }
    }
}
