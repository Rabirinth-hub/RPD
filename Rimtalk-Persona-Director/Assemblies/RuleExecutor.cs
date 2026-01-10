using HarmonyLib;
using Verse;
using RimWorld;
using RimTalk.Data;
using System.Linq;
using System.Collections.Generic;

namespace RimPersonaDirector
{
    // 这个类现在既包含执行逻辑，又包含 Harmony 补丁
    [HarmonyPatch]
    public static class RuleExecutor
    {
        // ==========================================
        // Harmony Patches
        // ==========================================

        // ★ Patch 1: PawnGenerator.GeneratePawn ★
        [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new[] { typeof(PawnGenerationRequest) })]
        [HarmonyPostfix]
        public static void GeneratePawn_Postfix(Pawn __result)
        {
            // 在 Pawn 生成后立刻尝试应用规则
            TryAssign(__result);
        }

        // ★ Patch 2: Pawn.SpawnSetup ★
        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        [HarmonyPostfix]
        public static void SpawnSetup_Postfix(Pawn __instance)
        {
            // 在 Pawn 第一次出现在地图上时再次尝试 (兜底)
            TryAssign(__instance);
        }

        // ==========================================
        // Execution Logic (核心逻辑)
        // ==========================================

        public static void TryAssign(Pawn p)
        {
            // 1. 安全检查 (防止 WorldGen 崩溃)
            if (Current.ProgramState != ProgramState.Playing) return;
            if (p == null || !p.RaceProps.Humanlike || p.Dead || p.health == null) return;

            // 2. 检查是否已有人格
            HediffDef personaDef = DefDatabase<HediffDef>.GetNamed("RimTalk_Persona", false);
            if (personaDef != null && p.health.hediffSet.GetFirstHediffOfDef(personaDef) != null)
                return;

            // 3. 检查设置
            var settings = DirectorMod.Settings;
            if (settings.userPresets == null || !settings.userPresets.Any()) return;

            // 4. 匹配规则
            List<string> candidateIds = new List<string>();
            if (settings.assignmentRules != null && settings.assignmentRules.Any())
            {
                var matchingRules = settings.assignmentRules
                    .Where(r => r.enabled && IsMatch(p, r))
                    .ToList();

                if (matchingRules.Any())
                {
                    int maxPriority = matchingRules.Max(r => r.priority);
                    var activeRules = matchingRules.Where(r => r.priority == maxPriority);

                    HashSet<string> ruleIds = new HashSet<string>();
                    foreach (var rule in activeRules)
                        if (rule.allowedPresetIds != null)
                            foreach (var id in rule.allowedPresetIds) ruleIds.Add(id);

                    candidateIds.AddRange(ruleIds);
                }
            }

            // 5. 如果无规则命中，使用全局池
            if (candidateIds.Count == 0)
            {
                candidateIds.AddRange(settings.userPresets.Select(pr => pr.id));
            }

            // 6. 最终抽取与应用
            if (candidateIds.Count == 0) return;

            string pickId = candidateIds.RandomElement();
            CustomPreset preset = settings.userPresets.Find(x => x.id == pickId);

            if (preset != null)
            {
                DirectorUtils.ApplyPersonalityToPawn(p, new PersonalityData(preset.personaText, preset.chattiness));

                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Message($"[Director] Auto-assigned preset '{preset.label}' to {p.Name}. ({(candidateIds.Count == settings.userPresets.Count ? "Global Pool" : "Rule-based Pool")})");
            }
        }

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