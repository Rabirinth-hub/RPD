using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(RimTalk.UI.PersonaEditorWindow), "DoWindowContents")]
    public static class Patch_PersonaEditorWindow_AddNotesButton
    {
        public static void Postfix(Rect inRect)
        {
            float footerY = inRect.y + 267f;
            float buttonWidth = 90f;
            float buttonHeight = 20f;
            Rect buttonRect = new Rect(inRect.x, footerY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(buttonRect, "RPD_Button_EditNotes".Translate())) 
            {
                Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }
        }
    }
}