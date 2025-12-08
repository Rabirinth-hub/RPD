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
            // 1. 检查设置开关
            if (!DirectorMod.Settings.Context.Inc_DirectorNotes)
            {
                return;
            }

            // 2. 精确计算位置
            float footerY = inRect.y + 267f;
            float buttonWidth = 90f;
            float buttonHeight = 20f;
            Rect buttonRect = new Rect(inRect.x, footerY, buttonWidth, buttonHeight);

            // 3. 获取当前备注内容
            string currentNotes = DirectorMod.Settings.directorNotes;
            bool hasNotes = !string.IsNullOrEmpty(currentNotes);

            // 4. 绘制按钮
            Color oldColor = GUI.color;
            if (hasNotes) GUI.color = Color.cyan;

            if (Widgets.ButtonText(buttonRect, "RPD_Button_EditNotes".Translate()))
            {
                Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }

            GUI.color = oldColor;

            // 5. 悬停提示
            if (Mouse.IsOver(buttonRect))
            {
                string tooltip = hasNotes
                    ? $"{"RPD_Tip_CurrentNotes".Translate()}:\n{currentNotes}"
                    : "RPD_Tip_NoNotes".Translate().ToString(); 

                TooltipHandler.TipRegion(buttonRect, tooltip);
            }
        }
    }
}