using HarmonyLib;
using Verse;
using RimTalk.Data;
using RimTalk.Service; // 引用 PromptService
using System;

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(PromptService), "CreatePawnContext")]
    public static class Patch_PromptService
    {
        // 递归卫士：防止 RenderScribanText 内部如果不小心又调用了 CreatePawnContext 导致死循环
        [ThreadStatic]
        private static bool _isPatching = false;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref string __result)
        {
            // 1. 安全检查
            if (_isPatching || string.IsNullOrEmpty(__result)) return;

            // 2. 获取原始人设
            string rawPersona = Hediff_Persona.GetOrAddNew(pawn)?.Personality;
            if (string.IsNullOrEmpty(rawPersona)) return;

            // 优化：如果原始人设不包含 {{ ，说明没有脚本，不需要渲染
            if (!rawPersona.Contains("{{")) return;

            // 3. 检查 __result 里是否真的包含这段原始文本
            // (RimTalk 可能会在前面加 "Personality: " 前缀，所以我们只查内容)
            if (!__result.Contains(rawPersona)) return;

            try
            {
                _isPatching = true;

                // 4. ★★★ 执行渲染 ★★★
                // 这里会利用我们刚才升级的 DirectorUtils.RenderScribanText
                // 它会自动读取 DirectorContextTracker 里的 pawns 列表
                string renderedPersona = DirectorUtils.RenderScribanText(rawPersona, pawn);

                // 5. ★★★ 替换文本 ★★★
                // 把 Context 字符串里的“源码”替换成“结果”
                __result = __result.Replace(rawPersona, renderedPersona);
            }
            catch (Exception ex)
            {
                // 不做处理，保持原样，避免破坏游戏
                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Warning($"[Director] Failed to patch context for {pawn.LabelShort}: {ex.Message}");
            }
            finally
            {
                _isPatching = false;
            }
        }
    }
}