using HarmonyLib;
using RimTalk.UI;
using UnityEngine;
using Verse;
using System.Reflection;

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(PersonaEditorWindow), "DoWindowContents")]
    public static class Patch_OverrideRollGen
    {
        // 状态标志：用于告诉 Postfix 我们是否已经处理了这次点击
        private static bool _hijackNextClick = false;

        // Prefix: 在原方法执行前运行
        [HarmonyPrefix]
        public static bool Prefix(Window __instance, Rect inRect)
        {
            // 1. 计算按钮位置 (与之前一样)
            float buttonWidth = 90f;
            float buttonHeight = 28f;
            float spacing = 10f;
            float buttonY = inRect.y + 365f; // 精确 Y 坐标
            float totalWidth = (buttonWidth * 4f) + (spacing * 3f);
            float startX = inRect.center.x - (totalWidth / 2f);
            Rect rollGenButtonRect = new Rect(startX + (buttonWidth + spacing) * 2, buttonY, buttonWidth, buttonHeight);

            // 2. 检查鼠标事件
            if (Mouse.IsOver(rollGenButtonRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // ★★★ 核心逻辑 ★★★
                // 1. 设置标志位，告诉 Postfix “嘿，这次点击归我了！”
                _hijackNextClick = true;

                // 2. 消耗事件，防止原版按钮响应
                Event.current.Use();

                // 3. 返回 true！让原方法继续执行，窗口不会闪烁
                return true;
            }

            // 如果不是我们要拦截的点击，就正常执行
            _hijackNextClick = false;
            return true;
        }

        // Postfix: 在原方法执行后运行
        [HarmonyPostfix]
        public static void Postfix(Window __instance, Rect inRect)
        {
            if (_hijackNextClick)
            {
                _hijackNextClick = false;

                Pawn pawn = (Pawn)AccessTools.Field(typeof(PersonaEditorWindow), "_pawn").GetValue(__instance);
                if (pawn != null)
                {
                    Find.WindowStack.Add(new Window_PresetBrowser(pawn, __instance));
                }

                // ★★★ 核心修复：防止文本框变空 ★★★
                // 检查 _editingPersonality 是否为空，如果是，给它一个空格
                var textField = AccessTools.Field(typeof(PersonaEditorWindow), "_editingPersonality");
                if (textField != null)
                {
                    string currentText = (string)textField.GetValue(__instance);
                    if (string.IsNullOrEmpty(currentText))
                    {
                        textField.SetValue(__instance, " ");
                    }
                }
            }

        }
    }
}