using HarmonyLib;
using RimTalk.Data;
using RimTalk.Prompt;
using RimTalk.Service;
using System;
using System.Collections.Generic;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(TalkService), nameof(TalkService.GenerateTalk))]
    internal static class Patch_LiveDialogueScope
    {
        [ThreadStatic]
        internal static bool BuildingLiveTalk;

        [HarmonyPrefix]
        private static void Prefix(out bool __state)
        {
            __state = BuildingLiveTalk;
            BuildingLiveTalk = true;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, bool __state)
        {
            BuildingLiveTalk = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PromptManager), nameof(PromptManager.BuildMessages))]
    internal static class Patch_DailyPortraitDialogue
    {
        internal sealed class Scope
        {
            internal bool PreviousRendering;
            internal Pawn PreviousPawn;
        }

        [HarmonyPrefix]
        private static void Prefix(out Scope __state)
        {
            __state = new Scope
            {
                PreviousRendering = DirectorApiAdapter.RenderingDialoguePortrait,
                PreviousPawn = DirectorApiAdapter.DialoguePortraitPawn
            };
            DirectorApiAdapter.RenderingDialoguePortrait = Patch_LiveDialogueScope.BuildingLiveTalk;
            DirectorApiAdapter.DialoguePortraitPawn = null;
        }

        [HarmonyPostfix]
        private static void Postfix(TalkRequest talkRequest, List<Pawn> pawns,
            List<(Role role, string content)> __result, Scope __state)
        {
            try
            {
                Pawn requested = DirectorApiAdapter.DialoguePortraitPawn;
                if (Patch_LiveDialogueScope.BuildingLiveTalk
                    && requested != null && pawns != null && pawns.Contains(requested))
                    DirectorPortraitService.AttachDailyDialoguePortrait(talkRequest, requested, __result);
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Daily portrait attachment failed: " + ex.Message);
            }
            finally
            {
                Restore(__state);
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, Scope __state)
        {
            Restore(__state);
            return __exception;
        }

        private static void Restore(Scope state)
        {
            if (state == null) return;
            DirectorApiAdapter.RenderingDialoguePortrait = state.PreviousRendering;
            DirectorApiAdapter.DialoguePortraitPawn = state.PreviousPawn;
        }
    }
}
