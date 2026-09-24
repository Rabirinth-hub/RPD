using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) })]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_AutoEvolveRoleFaction
    {
        [HarmonyPrefix]
        private static void CaptureOldRole(Pawn __instance, out DirectorPawnRoleState __state)
        {
            __state = DirectorAutoEvolveTriggerService.CaptureRoleState(__instance);
        }

        [HarmonyPostfix]
        private static void PublishRole(Pawn __instance, DirectorPawnRoleState __state)
        {
            DirectorAutoEvolveTriggerService.PublishRoleChange(
                __instance,
                __state,
                DirectorAutoEvolveTriggerService.CaptureRoleState(__instance));
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_AutoEvolveRoleGuestStatus
    {
        private static readonly FieldInfo PawnField =
            AccessTools.Field(typeof(Pawn_GuestTracker), "pawn");

        [HarmonyPrefix]
        private static void CaptureOldRole(
            Pawn_GuestTracker __instance,
            out DirectorPawnRoleState __state)
        {
            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            __state = DirectorAutoEvolveTriggerService.CaptureRoleState(pawn);
        }

        [HarmonyPostfix]
        private static void PublishRole(DirectorPawnRoleState __state)
        {
            DirectorAutoEvolveTriggerService.PublishRoleChange(
                __state?.Pawn,
                __state,
                DirectorAutoEvolveTriggerService.CaptureRoleState(__state?.Pawn));
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married))]
    internal static class Patch_AutoEvolveMarriage
    {
        [HarmonyPostfix]
        private static void Publish(Pawn firstPawn, Pawn secondPawn)
        {
            DirectorAutoEvolveTriggerService.PublishMarriage(firstPawn, secondPawn);
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.DoDivorce))]
    internal static class Patch_AutoEvolveDivorce
    {
        [HarmonyPostfix]
        private static void Publish(Pawn initiator, Pawn recipient)
        {
            DirectorAutoEvolveTriggerService.PublishBreakup(initiator, recipient);
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(InteractionWorker_Breakup), "Interacted")]
    internal static class Patch_AutoEvolveBreakup
    {
        [HarmonyPostfix]
        private static void Publish(Pawn initiator, Pawn recipient)
        {
            DirectorAutoEvolveTriggerService.PublishBreakup(initiator, recipient);
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), "Interacted")]
    internal static class Patch_AutoEvolveRejectedProposalBreakup
    {
        [HarmonyPrefix]
        private static void CaptureRelationship(Pawn initiator, Pawn recipient, out bool __state)
        {
            __state = initiator?.relations != null
                && recipient != null
                && (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)
                    || initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient));
        }

        [HarmonyPostfix]
        private static void Publish(Pawn initiator, Pawn recipient, bool __state)
        {
            if (!__state || initiator?.relations == null || recipient == null) return;
            bool stillTogether = initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)
                || initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient)
                || initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient);
            if (!stillTogether)
            {
                DirectorAutoEvolveTriggerService.PublishBreakup(initiator, recipient);
            }
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "BreakLoverAndFianceRelations")]
    internal static class Patch_AutoEvolveRomanceReplacementBreakup
    {
        [HarmonyPrefix]
        private static void CaptureCount(List<Pawn> oldLoversAndFiances, out int __state)
        {
            __state = oldLoversAndFiances?.Count ?? 0;
        }

        [HarmonyPostfix]
        private static void Publish(
            Pawn pawn,
            ref List<Pawn> oldLoversAndFiances,
            int __state)
        {
            if (pawn == null || oldLoversAndFiances == null) return;
            for (int i = Math.Max(0, __state); i < oldLoversAndFiances.Count; i++)
            {
                DirectorAutoEvolveTriggerService.PublishBreakup(
                    pawn,
                    oldLoversAndFiances[i]);
            }
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    internal static class Patch_AutoEvolveBirthOutcome
    {
        [HarmonyPostfix]
        private static void Publish(
            Thing __result,
            Pawn geneticMother,
            Thing birtherThing,
            Pawn father)
        {
            Pawn child = __result as Pawn;
            if (child == null || child.Dead) return;
            Pawn birther = birtherThing as Pawn;
            DirectorAutoEvolveTriggerService.PublishBirth(
                geneticMother ?? birther,
                father,
                child,
                geneticMother,
                birther);
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    internal static class Patch_AutoEvolveLegacyBirth
    {
        [HarmonyPostfix]
        private static void Publish(Pawn mother, Pawn father)
        {
            DirectorAutoEvolveTriggerService.PublishBirth(mother, father, null);
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) })]
    internal static class Patch_AutoEvolveDirectFamilyDeath
    {
        [HarmonyPrefix]
        private static void CaptureFamily(Pawn __instance, out List<Pawn> __state)
        {
            __state = null;
            if (!(DirectorMod.Settings?.autoEvolveOnDirectFamilyDeath ?? false)
                || __instance?.relations?.DirectRelations == null)
            {
                return;
            }

            var family = new HashSet<Pawn>();
            foreach (DirectPawnRelation relation in __instance.relations.DirectRelations)
            {
                if (relation?.otherPawn == null) continue;
                if (relation.def == PawnRelationDefOf.Parent
                    || relation.def == PawnRelationDefOf.Child
                    || relation.def == PawnRelationDefOf.Spouse)
                {
                    family.Add(relation.otherPawn);
                }
            }

            IEnumerable<Pawn> children = __instance.relations.Children;
            if (children != null)
            {
                foreach (Pawn child in children)
                {
                    if (child != null) family.Add(child);
                }
            }

            __state = new List<Pawn>(family);
        }

        [HarmonyPostfix]
        private static void Publish(Pawn __instance, List<Pawn> __state)
        {
            if (__instance == null || !__instance.Dead || __state == null) return;
            foreach (Pawn survivor in __state)
            {
                DirectorAutoEvolveTriggerService.PublishDirectFamilyDeath(
                    survivor,
                    __instance);
            }
        }
    }

    [HarmonyPatchCategory(DirectorPatchRegistry.ExperimentalCategory)]
    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    internal static class Patch_AutoEvolveTraitAdded
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(TraitSet), "pawn");

        [HarmonyPrefix]
        private static void CaptureExisting(TraitSet __instance, Trait trait, out bool __state)
        {
            __state = false;
            if (__instance?.allTraits == null || trait?.def == null) return;
            foreach (Trait existing in __instance.allTraits)
            {
                if (existing?.def == trait.def)
                {
                    __state = true;
                    return;
                }
            }
        }

        [HarmonyPostfix]
        private static void Publish(TraitSet __instance, Trait trait, bool __state)
        {
            if (__state || __instance?.allTraits == null || trait == null) return;
            if (!__instance.allTraits.Contains(trait)) return;
            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            DirectorAutoEvolveTriggerService.PublishTraitAdded(pawn, trait);
        }
    }
}
