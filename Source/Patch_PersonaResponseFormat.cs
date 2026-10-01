using HarmonyLib;
using RimTalk.Client.OpenAI;
using RimTalk.Client.Player2;
using RimTalk.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RimPersonaDirector
{
    internal static class DirectorPersonaResponseFormat
    {
        private static readonly AsyncLocal<bool> Current = new AsyncLocal<bool>();

        internal static async Task<T> Run<T>(Func<Task<T>> request)
        {
            bool previous = Current.Value;
            Current.Value = true;
            try
            {
                return await request().ConfigureAwait(false);
            }
            finally
            {
                Current.Value = previous;
            }
        }

        internal static string Adjust(string json)
        {
            if (!Current.Value || string.IsNullOrEmpty(json)) return json;

            try
            {
                var payload = JsonUtil.ParseJsonValue(json, out _) as Dictionary<string, object>;
                if (payload == null || !payload.ContainsKey("response_format"))
                    return json;

                // RimTalk's custom response format is shared with dialogue requests,
                // but RPD persona requests supply their own output instructions.
                payload.Remove("response_format");
                return JsonUtil.SerializeJsonValue(payload);
            }
            catch (Exception)
            {
                return json;
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_PersonaResponseFormat
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase openAi = AccessTools.Method(typeof(OpenAIClient), "BuildRequestJson");
            if (openAi != null) yield return openAi;
            MethodBase player2 = AccessTools.Method(typeof(Player2Client), "BuildRequestJson");
            if (player2 != null) yield return player2;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter("assssssqwww.DeepSeekJsonMode")]
        private static void Postfix(ref string __result)
        {
            __result = DirectorPersonaResponseFormat.Adjust(__result);
        }
    }
}
