using HarmonyLib;
using System.Collections.Generic;
using Verse;
using RimTalk.Prompt;
using RimTalk.Service;
using RimTalk.Data;
using System;

namespace RimPersonaDirector
{
    // ★★★ 核心修复：全时段上下文捕获 ★★★
    // 我们需要在 RimTalk 处理数据的每一个关键节点，都把 Pawn 列表存下来
    public static class Patch_ContextCapture
    {
        // 1. 拦截 ScribanParser.Render (用于 {{p.profile}} 等动态变量)
        [HarmonyPatch(typeof(ScribanParser), "Render")]
        public static class Patch_Render
        {
            [HarmonyPrefix]
            public static void Prefix(PromptContext context)
            {
                if (context != null && context.AllPawns != null && context.AllPawns.Count > 0)
                {
                    DirectorContextTracker.SetPawns(context.AllPawns);
                }
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                // Render 可能是嵌套调用的，这里简单处理，
                // 如果你想更严谨，可以用计数器 (引用计数)，但通常这就够了
                // 为了防止清除掉外层的数据，这里可以选择不清除，
                // 或者依赖 DirectorContextTracker 自身的 ThreadStatic 特性
            }
        }

        // 2. ★★★ 新增：拦截 PromptService.BuildContext (用于 {{context}}) ★★★
        // 这是生成静态 {{context}} 字符串的地方
        [HarmonyPatch(typeof(PromptService), "BuildContext")]
        public static class Patch_BuildContext
        {
            [HarmonyPrefix]
            public static void Prefix(List<Pawn> pawns)
            {
                if (pawns != null)
                {
                    DirectorContextTracker.SetPawns(pawns);
                }
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                // 执行完后清理，保持干净
                DirectorContextTracker.Clear();
            }
        }

        // 3. ★★★ 新增：拦截 PromptService.DecoratePrompt (用于环境判断) ★★★
        // 确保在生成环境描述时，Tracker 也是有值的
        [HarmonyPatch(typeof(PromptService), "DecoratePrompt")]
        public static class Patch_DecoratePrompt
        {
            [HarmonyPrefix]
            public static void Prefix(List<Pawn> pawns)
            {
                if (pawns != null)
                {
                    DirectorContextTracker.SetPawns(pawns);
                }
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                DirectorContextTracker.Clear();
            }
        }
    }
}