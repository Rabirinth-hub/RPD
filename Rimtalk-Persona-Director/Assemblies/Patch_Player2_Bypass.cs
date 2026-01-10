using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalk.Client;
using RimTalk.Client.Player2;
using RimTalk.Data;
using Verse;

namespace RimPersonaDirector
{
    [HarmonyPatch]
    public static class Patch_Player2Client_Bypass
    {
        // 告诉 Harmony 我们要 Patch 的目标方法
        [HarmonyPrepare]
        public static bool Prepare()
        {
            // 确保 Player2Client 类型存在，如果用户没用 Player2 或 RimTalk 更新了，就不要打补丁
            return AccessTools.TypeByName("RimTalk.Client.Player2.Player2Client") != null;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            // 目标：RimTalk.Client.Player2.Player2Client.GetChatCompletionAsync
            var type = AccessTools.TypeByName("RimTalk.Client.Player2.Player2Client");
            return AccessTools.Method(type, "GetChatCompletionAsync");
        }

        // Prefix 补丁，它会优先于原方法执行
        public static bool Prefix(Player2Client __instance, ref Task<Payload> __result, string instruction, List<(Role role, string message)> messages)
        {

            if (DirectorMod.Settings.EnableDebugLog)
                Log.Message("[Persona Director] Player2 Bypass: Intercepted non-streaming call, rerouting to streaming channel.");

            async Task<Payload> BypassAsync()
            {
                // 调用流式方法
                return await __instance.GetStreamingChatCompletionAsync<object>(instruction, messages, _ => { });
            }

            // 将我们的异步任务赋值给原方法的结果
            __result = BypassAsync();

            // 返回 false，阻止原版的非流式方法执行，避免 402 错误
            return false;
        }
    }
}