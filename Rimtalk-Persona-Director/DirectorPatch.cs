using System;
using System.Threading.Tasks;
using RimTalk.Data;
using HarmonyLib;
using Verse;

namespace RimPersonaDirector
{
    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            var harmony = new Harmony("com.yourname.rimtalk.director");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(RimTalk.Data.PersonaService), "GeneratePersona")]
    public static class Patch_GeneratePersona
    {
        public static bool Prefix(Pawn pawn, ref Task<PersonalityData> __result)
        {
            // 1. 提取数据
            string preGeneratedData = DirectorUtils.BuildCustomCharacterData(pawn);
            string safeName = pawn.LabelShortCap;

            // 2. ★★★ 修改：直接调用新的 GeneratePersonalityTask ★★★
            __result = DirectorUtils.GeneratePersonalityTask(preGeneratedData, safeName, pawn);

            // 3. 拦截原版
            return false;
        }
    }
}