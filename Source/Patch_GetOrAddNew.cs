using HarmonyLib;
using Verse;
using RimTalk.Data;
using System.Linq;
using System.Collections.Generic;
using RimWorld;

namespace RimPersonaDirector
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup), new[] { typeof(Map), typeof(bool) })]
    public static class Patch_NewPawnPersona
    {
        // 最近分配记录：<预设ID, 分配时间>
        private static Dictionary<string, int> recentAssignments = new Dictionary<string, int>();
        private const int CACHE_DURATION_TICKS = 600; // 10秒 = 600 ticks (60 ticks/秒)
        private const int MAX_RETRY_ATTEMPTS = 2; // 最多重试2次，总共3次尝试

        [HarmonyPostfix]
        private static void AssignPersonaToSpawnedPawn(Pawn __instance, bool respawningAfterLoad)
        {
            Pawn pawn = __instance;
            if (respawningAfterLoad
                || pawn == null
                || pawn.Destroyed
                || pawn.Dead
                || pawn.RaceProps == null
                || !pawn.RaceProps.Humanlike
                || !pawn.Spawned
                || pawn.Map == null)
            {
                return;
            }

            HediffDef personaDef = DefDatabase<HediffDef>.GetNamed("RimTalk_PersonaData", false)
                ?? DefDatabase<HediffDef>.GetNamed("RimTalk_Persona", false);
            Hediff_Persona existingPersona = personaDef == null
                ? null
                : pawn.health?.hediffSet?.GetFirstHediffOfDef(personaDef) as Hediff_Persona;
            if (!string.IsNullOrWhiteSpace(existingPersona?.Personality)) return;

            CustomPreset preset = FindPresetFor(pawn);
            if (preset == null) return;

            DirectorUtils.ApplyPersonalityToPawn(
                pawn,
                new PersonalityData(preset.personaText, preset.chattiness),
                false);

            string applied = PersonaService.GetPersonality(pawn)?.Trim() ?? "";
            string initial = preset.personaText?.Trim() ?? "";
            if (!string.IsNullOrEmpty(initial)
                && string.Equals(applied, initial, System.StringComparison.Ordinal))
            {
                Find.World?.GetComponent<DirectorWorldComponent>()?.RecordInitialPersona(
                    pawn,
                    initial);
            }

            if (DirectorMod.Settings.EnableDebugLog)
                Log.Message($"[Director] Assigned initial persona '{preset.label}' to newly spawned pawn {pawn.Name}.");
        }

        /// <summary>
        /// 规则匹配和随机抽取的核心逻辑。
        /// </summary>
        public static CustomPreset FindPresetFor(Pawn p)
        {
            var settings = DirectorMod.Settings;
            if (settings?.userPresets == null || !settings.userPresets.Any()) return null;

            List<string> candidateIds = FindMatchingRulePresetIds(p);

            // 2. 如果无规则命中，使用全局池
            if (candidateIds.Count == 0)
            {
                // 只有被用户开启的预设才会随机给路人
                candidateIds.AddRange(settings.userPresets
                    .Where(pr => pr.enabled) // ★ 只取已启用的 ★
                    .Select(pr => pr.id));
            }

            if (candidateIds.Count == 0) return null;
            return PickPreset(candidateIds);
        }

        internal static bool IsPersonaFromCurrentAssignmentPools(Pawn pawn, string persona)
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings?.userPresets == null || string.IsNullOrWhiteSpace(persona)) return false;

            string current = persona.Trim();
            List<string> candidateIds = FindMatchingRulePresetIds(pawn);
            IEnumerable<CustomPreset> candidates = candidateIds.Count > 0
                ? settings.userPresets.Where(preset => candidateIds.Contains(preset.id))
                : settings.userPresets.Where(preset => preset.enabled);

            return candidates.Any(preset => string.Equals(
                preset.personaText?.Trim() ?? "",
                current,
                System.StringComparison.Ordinal));
        }

        private static List<string> FindMatchingRulePresetIds(Pawn pawn)
        {
            var settings = DirectorMod.Settings;
            if (settings?.assignmentRules == null || pawn == null) return new List<string>();

            var matchingRules = settings.assignmentRules.Where(r => r.enabled && IsMatch(pawn, r)).ToList();
            if (matchingRules.Count == 0) return new List<string>();

            int maxPriority = matchingRules.Max(r => r.priority);
            return matchingRules
                .Where(r => r.priority == maxPriority && r.allowedPresetIds != null)
                .SelectMany(r => r.allowedPresetIds)
                .Distinct()
                .Where(id => settings.userPresets.Any(p => p.id == id))
                .ToList();
        }

        private static CustomPreset PickPreset(List<string> candidateIds)
        {
            if (candidateIds == null || candidateIds.Count == 0) return null;

            // 清理过期的缓存记录
            CleanupExpiredCache();
            // 随机抽取（带重试机制避免短时间重复）
            string pickId = null;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            
            for (int attempt = 0; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                // ★ 添加随机扰动，确保RNG状态被推进 ★
                if (attempt > 0)
                {
                    // 在重试时，先做一些随机操作来"搅动"RNG状态
                    Rand.Range(0, 1000); // 推进RNG状态
                }
                pickId = candidateIds.RandomElement();
                
                // 检查是否在最近使用过
                if (!recentAssignments.ContainsKey(pickId))
                {
                    // 未被最近使用，可以分配
                    break;
                }
                
                // 如果是最后一次尝试，即使重复也接受
                if (attempt == MAX_RETRY_ATTEMPTS)
                {
                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Message($"[Director] Preset '{pickId}' was recently used, but accepting after {MAX_RETRY_ATTEMPTS + 1} attempts (pool size: {candidateIds.Count})");
                    break;
                }
                
                // 否则重试
                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Message($"[Director] Preset '{pickId}' was recently used, retrying... (attempt {attempt + 1}/{MAX_RETRY_ATTEMPTS + 1})");
            }
            
            // 记录本次分配
            recentAssignments[pickId] = currentTick;
            
            return DirectorMod.Settings.userPresets.Find(x => x.id == pickId);
        }

        /// <summary>
        /// 清理超过10秒的缓存记录
        /// </summary>
        private static void CleanupExpiredCache()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            var expiredKeys = recentAssignments
                .Where(kvp => currentTick - kvp.Value > CACHE_DURATION_TICKS)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredKeys)
            {
                recentAssignments.Remove(key);
            }
        }

        /// <summary>
        /// 判断 Pawn 是否符合规则。
        /// </summary>
        private static bool IsMatch(Pawn p, AssignmentRule rule)
        {
            if (rule.type == RuleType.Age)
            {
                // 年龄规则：targetDefName无意义，判断pawn年龄是否在区间
                int pawnAge = (int)p.ageTracker.AgeBiologicalYears;
                return pawnAge >= rule.minAge && pawnAge <= rule.maxAge;
            }
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
