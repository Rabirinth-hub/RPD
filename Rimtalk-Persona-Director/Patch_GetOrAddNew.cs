using HarmonyLib;
using Verse;
using RimTalk.Data;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System; // 用于 Exception

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(Hediff_Persona), "GetOrAddNew")]
    public static class Patch_GetOrAddNew
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                // 1. 精确锁定目标方法 (更稳健的查找)
                MethodInfo targetMethod = null;

                // 首先尝试常规方式
                try
                {
                    targetMethod = AccessTools.Method(
                        typeof(GenCollection),
                        nameof(GenCollection.RandomElement),
                        generics: new[] { typeof(PersonalityData) },
                        parameters: new[] { typeof(IEnumerable<PersonalityData>) }
                    );
                }
                catch
                {
                    targetMethod = null;
                }

                // 如果常规方式失败，退回到手动扫描并构造泛型方法
                if (targetMethod == null)
                {
                    var methods = typeof(GenCollection).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    foreach (var m in methods)
                    {
                        if (m.Name != "RandomElement") continue;
                        if (!m.IsGenericMethodDefinition) continue;
                        var pars = m.GetParameters();
                        if (pars.Length != 1) continue;
                        var ptype = pars[0].ParameterType;
                        if (!ptype.IsGenericType) continue;
                        if (ptype.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;

                        try
                        {
                            targetMethod = m.MakeGenericMethod(typeof(PersonalityData));
                            break;
                        }
                        catch
                        {
                            // 忽略并继续查找
                        }
                    }
                }

                if (targetMethod == null)
                {
                    Log.Error("[Persona Director] Transpiler failed: Could not find target method GenCollection.RandomElement<PersonalityData>(IEnumerable). Auto-assignment will be disabled.");
                    return instructions;
                }

                // 2. 找到替换方法
                var replacementMethod = AccessTools.Method(typeof(Patch_GetOrAddNew), nameof(AssignViaRulesOrRandom));

                // 3. 遍历和替换
                var codes = new List<CodeInstruction>(instructions);
                bool patched = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(targetMethod))
                    {
                        // a. 插入 pawn 参数 (GetOrAddNew 的第一个参数)
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0)); // pawn

                        // b. 替换调用指令
                        codes[i + 1] = new CodeInstruction(OpCodes.Call, replacementMethod);

                        patched = true;
                        break;
                    }
                }

                if (!patched)
                {
                    Log.Warning("[Persona Director] Transpiler WARNING: Could not find call to RandomElement in Hediff_Persona.GetOrAddNew. Auto-assignment will not work.");
                }

                return codes.AsEnumerable();
            }
            catch (Exception ex)
            {
                Log.Error($"[Persona Director] Transpiler CRITICAL ERROR: {ex.Message}. Auto-assignment is disabled.");
                return instructions; // 发生任何错误都返回原始代码，保证游戏能运行
            }
        }

        /// <summary>
        /// 我们的替换方法。它接收原版随机池和 Pawn，返回一个我们选择的 PersonalityData。
        /// </summary>
        public static PersonalityData AssignViaRulesOrRandom(IEnumerable<PersonalityData> vanillaPool, Pawn pawn)
        {
            // 在我们的规则逻辑执行前，先做一个基础安全检查
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                // 对于非人类，直接使用原版逻辑
                return vanillaPool.RandomElement();
            }

            // 执行我们的规则
            CustomPreset preset = FindPresetFor(pawn);

            // 如果我们的规则找到了一个预设
            if (preset != null)
            {
                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Message($"[Director] Auto-assigned '{preset.label}' to {pawn.Name} via rule or global pool.");

                // 返回一个新的 PersonalityData 实例
                return new PersonalityData(preset.personaText, preset.chattiness);
            }

            // 如果我们的规则系统什么都没找到（比如库是空的），就回退到原版随机池
            if (DirectorMod.Settings.EnableDebugLog)
                Log.Message($"[Director] No presets found for {pawn.Name}. Falling back to vanilla random pool.");

            return vanillaPool.RandomElement();
        }

        /// <summary>
        /// 规则匹配和随机抽取的核心逻辑。
        /// </summary>
        private static CustomPreset FindPresetFor(Pawn p)
        {
            var settings = DirectorMod.Settings;
            if (settings.userPresets == null || !settings.userPresets.Any()) return null;

            List<string> candidateIds = new List<string>();

            // 1. 尝试匹配规则
            if (settings.assignmentRules != null && settings.assignmentRules.Any())
            {
                var matchingRules = settings.assignmentRules.Where(r => r.enabled && IsMatch(p, r)).ToList();
                if (matchingRules.Any())
                {
                    int maxPriority = matchingRules.Max(r => r.priority);
                    var activeRules = matchingRules.Where(r => r.priority == maxPriority);

                    // 合并池子
                    HashSet<string> ruleIds = new HashSet<string>();
                    foreach (var rule in activeRules)
                        if (rule.allowedPresetIds != null)
                            foreach (var id in rule.allowedPresetIds) ruleIds.Add(id);

                    candidateIds.AddRange(ruleIds);
                }
            }

            // 2. 如果无规则命中，使用全局池
            if (candidateIds.Count == 0)
            {
                candidateIds.AddRange(settings.userPresets.Select(pr => pr.id));
            }

            if (candidateIds.Count == 0) return null;

            // 3. 随机抽取
            string pickId = candidateIds.RandomElement();
            return settings.userPresets.Find(x => x.id == pickId);
        }

        /// <summary>
        /// 判断 Pawn 是否符合规则。
        /// </summary>
        private static bool IsMatch(Pawn p, AssignmentRule rule)
        {
            if (string.IsNullOrEmpty(rule.targetDefName)) return false;
            switch (rule.type)
            {
                case RuleType.FactionDef:
                    return p.Faction != null && p.Faction.def.defName == rule.targetDefName;
                case RuleType.RaceDef:
                    return p.def.defName == rule.targetDefName;
                case RuleType.XenotypeDef:
                    return p.genes != null && p.genes.Xenotype != null && p.genes.Xenotype.defName == rule.targetDefName;
                default:
                    return false;
            }
        }
    }
}