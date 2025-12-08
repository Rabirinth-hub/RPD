using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Service;
using Verse;
using RimWorld;

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

    [HarmonyPatch(typeof(PersonaService), "GeneratePersona")]
    public static class Patch_GeneratePersona
    {
        public static bool Prefix(Pawn pawn, ref Task<PersonalityData> __result)
        {
            __result = GenerateWithDirectorPrompt(pawn);
            return false;
        }

        private static async Task<PersonalityData> GenerateWithDirectorPrompt(Pawn pawn)
        {
            // 1. 构建数据
            string customData = BuildCustomCharacterData(pawn);

            // 2. 打印调试日志 (这里是你检查数据的地方)
            if (DirectorMod.Settings.EnableDebugLog)
            {
                Log.Message($"[Director Debug] Data Sent to AI:\n{customData}");
            }

            try
            {
                AIService.UpdateContext($"[Character Data]\n{customData}");

                string userPrompt = DirectorMod.Settings.activePrompt;
                if (string.IsNullOrEmpty(userPrompt)) userPrompt = DirectorSettings.DefaultUserPrompt;
                string finalPrompt = userPrompt.Replace("{LANG}", Constant.Lang);

                finalPrompt += "\n" + DirectorSettings.HiddenTechnicalPrompt;

                var request = new TalkRequest(finalPrompt, pawn);
                PersonalityData personalityData = await AIService.Query<PersonalityData>(request);

                if (personalityData?.Persona != null)
                {
                    personalityData.Persona = personalityData.Persona.Trim();
                }

                return personalityData;
            }
            catch (Exception e)
            {
                string errorMsg = $"[Director Error]: {e.Message}";
                Log.Error(errorMsg);
                return new PersonalityData(errorMsg, 1.0f);
            }
        }

        private static string BuildCustomCharacterData(Pawn p)
        {
            StringBuilder sb = new StringBuilder();
            var ctx = DirectorMod.Settings.Context;

            // 1. Basic Info
            if (ctx.Inc_Basic)
            {
                sb.AppendLine("--- Basic Info ---");
                sb.AppendLine($"Name: {p.Name.ToStringFull}");
                sb.AppendLine($"Gender: {p.gender}");
                sb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
            }

            // 2. Race & Xenotype
            if (ctx.Inc_Race)
            {
                sb.AppendLine("\n--- Race & Xenotype ---");
                sb.Append($"Race: {p.def.label}");
                if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.def.description}");
                else sb.AppendLine();

                if (p.genes != null && p.genes.Xenotype != null && p.genes.Xenotype.defName != "Archite")
                {
                    sb.Append($"Xenotype: {p.genes.XenotypeLabel}");
                    if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.genes.Xenotype.description}");
                    else sb.AppendLine();
                }
            }

            // 3. Genes
            if (ctx.Inc_Genes && p.genes != null)
            {
                sb.AppendLine("\n--- Genes ---");
                if (p.genes.Endogenes.Any())
                {
                    sb.Append("[Endogenes (Natural)]: ");
                    foreach (var gene in p.genes.Endogenes)
                    {
                        if (gene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !gene.Overridden)
                        {
                            sb.Append(gene.LabelCap);
                            if (ctx.Inc_Genes_Desc) sb.Append($"({gene.def.description})");
                            sb.Append(", ");
                        }
                    }
                    sb.AppendLine();
                }
                if (p.genes.Xenogenes.Any())
                {
                    sb.Append("[Xenogenes (Artificial)]: ");
                    foreach (var gene in p.genes.Xenogenes)
                    {
                        if (gene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !gene.Overridden)
                        {
                            sb.Append(gene.LabelCap);
                            if (ctx.Inc_Genes_Desc) sb.Append($"({gene.def.description})");
                            sb.Append(", ");
                        }
                    }
                    sb.AppendLine();
                }
            }

            // 4. Backstory
            if (ctx.Inc_Backstory && p.story != null)
            {
                sb.AppendLine("\n--- Backstory ---");
                if (p.story.Childhood != null)
                {
                    sb.Append("Childhood: ");
                    if (ctx.Inc_Backstory_Desc) sb.AppendLine(p.story.Childhood.FullDescriptionFor(p));
                    else sb.AppendLine(p.story.Childhood.title);
                }
                if (p.story.Adulthood != null)
                {
                    sb.Append("Adulthood: ");
                    if (ctx.Inc_Backstory_Desc) sb.AppendLine(p.story.Adulthood.FullDescriptionFor(p));
                    else sb.AppendLine(p.story.Adulthood.title);
                }
            }

            // 5. Relationships
            if (ctx.Inc_Relations && p.relations != null && p.relations.DirectRelations.Any())
            {
                StringBuilder relationSb = new StringBuilder();
                foreach (var relation in p.relations.DirectRelations)
                {
                    if (relation.otherPawn == null) continue;

                    string status = "";
                    if (relation.otherPawn.Dead) status = "(Deceased)";
                    else if (relation.otherPawn.Faction == p.Faction) status = "(In Colony)";
                    else if (relation.otherPawn.Faction != null)
                    {
                        if (relation.otherPawn.Faction.HostileTo(p.Faction)) status = "(Hostile Faction)";
                        else if (relation.otherPawn.Faction.AllyOrNeutralTo(p.Faction)) status = "(Neutral/Ally Faction)";
                    }
                    else status = "(Factionless)";

                    string relationLabel = relation.def.label;
                    if (relation.otherPawn.gender == Gender.Female && !relation.def.labelFemale.NullOrEmpty())
                    {
                        relationLabel = relation.def.labelFemale;
                    }

                    relationSb.AppendLine($"- {relationLabel}: {relation.otherPawn.Name.ToStringShort} {status}");
                }

                if (relationSb.Length > 0)
                {
                    sb.AppendLine("\n--- Relationships (Direct Family) ---");
                    sb.Append(relationSb);
                }
            }

            // 6. Traits
            if (ctx.Inc_Traits && p.story?.traits?.allTraits != null)
            {
                sb.AppendLine("\n--- Traits ---");
                foreach (var trait in p.story.traits.allTraits)
                {
                    sb.Append(trait.Label);
                    if (ctx.Inc_Traits_Desc) sb.AppendLine($": {trait.CurrentData.description}");
                    else sb.AppendLine();
                }
            }

            // 7. ★ RimPsyche (修复版) ★
            if (ctx.Inc_RimPsyche)
            {
                string psycheData = GetMauxRimPsycheData(p);
                if (!string.IsNullOrEmpty(psycheData))
                {
                    sb.AppendLine("\n--- RimPsyche ---");
                    sb.Append(psycheData);
                }
            }

            // 8. Ideology
            if (ctx.Inc_Ideology && p.Ideo != null)
            {
                sb.AppendLine("\n--- Ideology ---");
                sb.AppendLine($"Religion: {p.Ideo.name}");
                foreach (var meme in p.Ideo.memes)
                {
                    sb.Append($"Meme [{meme.LabelCap}]");
                    if (ctx.Inc_Ideology_Desc) sb.AppendLine($": {meme.description}");
                    else sb.AppendLine();
                }
            }

            // 9. Skills
            if (ctx.Inc_Skills && p.skills != null)
            {
                sb.AppendLine("\n--- Skills & Work Incapabilities ---");
                foreach (var skill in p.skills.skills)
                {
                    sb.Append($"{skill.def.label}: ");

                    bool incapable = p.WorkTagIsDisabled(WorkTags.AllWork);
                    if (!incapable)
                    {
                        foreach (var workDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                        {
                            if (workDef.relevantSkills.Contains(skill.def) && p.WorkTypeIsDisabled(workDef))
                            {
                                incapable = true;
                                break;
                            }
                        }
                    }

                    if (incapable)
                    {
                        sb.Append("[INCAPABLE]");
                    }
                    else
                    {
                        sb.Append(skill.Level);
                        if (ctx.Inc_Skills_Desc) sb.Append($" {GetPassionInfoHardcoded(skill.passion)}");
                    }
                    sb.AppendLine();
                }
            }

            // 10. Health
            if (ctx.Inc_Health && p.health != null)
            {
                sb.AppendLine("\n--- Health/Implants ---");
                foreach (var hediff in p.health.hediffSet.hediffs)
                {
                    if (hediff.Visible && (hediff.def.isBad || hediff.def.countsAsAddedPartOrImplant))
                    {
                        string partName = hediff.Part != null ? hediff.Part.LabelCap : "Whole Body";
                        sb.Append($"- {partName}: {hediff.LabelCap}");
                        if (ctx.Inc_Health_Desc) sb.AppendLine($" ({hediff.def.description})");
                        else sb.AppendLine();
                    }
                }
            }

            // 11. Director's Notes
            if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(DirectorMod.Settings.directorNotes))
            {
                sb.AppendLine("\n--- Director's Notes (Custom Context) ---");
                sb.AppendLine(DirectorMod.Settings.directorNotes);
            }

            return sb.ToString();
        }

        // RimPsyche 适配
        private static string GetMauxRimPsycheData(Pawn p)
        {
            StringBuilder psySb = new StringBuilder();

            // 1. 查找组件
            object comp = p.AllComps.FirstOrDefault(c => c.GetType().FullName.Contains("RimPsyche") || c.GetType().Name.Contains("Psyche"));
            if (comp == null) return "";

            // --- Part A: 性格 (Personality) ---
            // 放在独立的 try-catch 里，防止它报错影响后面
            try
            {
                Type utilityType = AccessTools.TypeByName("Maux36.RimPsyche.Rimpsyche_Utility");
                if (utilityType != null)
                {
                    MethodInfo method = AccessTools.Method(utilityType, "GetPersonalityDescriptionNumber", new Type[] { typeof(Pawn), typeof(int) });
                    if (method != null)
                    {
                        // limit: 5 (Short) 或 0 (All)
                        int limit = DirectorMod.Settings.Context.Inc_RimPsyche_All ? 0 : 5;
                        object result = method.Invoke(null, new object[] { p, limit });
                        if (result != null)
                        {
                            psySb.AppendLine("- Personality Facets (Range: -1.0 to 1.0):");
                            psySb.AppendLine((string)result);
                        }
                    }
                }
            }
            catch { /* Personality 读取失败，忽略 */ }

            // --- Part B: 兴趣话题 (Interests) ---
            // 放在独立的 try-catch 里，优先尝试从 Interests 属性读取
            try
            {
                object interestsTracker = null;
                Type compType = comp.GetType();

                // 优先尝试读取 Interests 属性
                var interestsProp = AccessTools.Property(compType, "Interests");
                if (interestsProp != null)
                {
                    interestsTracker = interestsProp.GetValue(comp, null);
                }
                else
                {
                    // 备选：尝试字段
                    var interestsField = AccessTools.Field(compType, "Interests");
                    if (interestsField != null) interestsTracker = interestsField.GetValue(comp);
                }

                // 如果没找到 Tracker，尝试从 PsycheData 里找 (老版本兼容)
                if (interestsTracker == null)
                {
                    var psycheField = compType.GetField("psyche") ?? compType.GetField("personality");
                    if (psycheField != null) interestsTracker = psycheField.GetValue(comp);
                }

                if (interestsTracker != null)
                {
                    var interestField = AccessTools.Field(interestsTracker.GetType(), "interestScore");
                    if (interestField != null)
                    {
                        var interestDict = interestField.GetValue(interestsTracker) as IDictionary;
                        if (interestDict != null && interestDict.Count > 0)
                        {
                            StringBuilder interestSb = new StringBuilder();
                            foreach (DictionaryEntry entry in interestDict)
                            {
                                string key = entry.Key.ToString();
                                float score = Convert.ToSingle(entry.Value);

                                // 阈值过滤: >20 或 <-20
                                bool isSignificant = score > 20f || score < -20f;
                                bool showAll = DirectorMod.Settings.Context.Inc_RimPsyche_All;

                                if (showAll || isSignificant)
                                {
                                    string detail = GetInterestDetails(key);
                                    interestSb.Append($"{detail}: {score:F1}, ");
                                }
                            }

                            if (interestSb.Length > 0)
                            {
                                psySb.AppendLine("\n- Interests (Topics, Range: -35.0 to 35.0):");
                                psySb.AppendLine(interestSb.ToString().TrimEnd(',', ' '));
                            }
                        }
                    }
                }
            }
            catch { /* Interests 读取失败，忽略 */ }

            return psySb.ToString();
        }

        // 缓存部分
        private static Dictionary<string, string> _interestCache = null;

        private static string GetInterestDetails(string key)
        {
            if (_interestCache == null) BuildInterestCache();
            if (_interestCache.TryGetValue(key, out string val)) return val;
            return key;
        }

        private static void BuildInterestCache()
        {
            _interestCache = new Dictionary<string, string>();
            try
            {
                Type domainType = AccessTools.TypeByName("Maux36.RimPsyche.InterestDomainDef");
                if (domainType != null)
                {
                    Type dbType = typeof(DefDatabase<>).MakeGenericType(domainType);
                    PropertyInfo allDefsProp = dbType.GetProperty("AllDefs");
                    if (allDefsProp != null)
                    {
                        IEnumerable allDomains = allDefsProp.GetValue(null) as IEnumerable;
                        if (allDomains != null)
                        {
                            foreach (object domain in allDomains)
                            {
                                FieldInfo interestsField = domain.GetType().GetField("interests");
                                if (interestsField == null) continue;
                                IEnumerable interestsList = interestsField.GetValue(domain) as IEnumerable;
                                if (interestsList == null) continue;

                                foreach (object interest in interestsList)
                                {
                                    Type intType = interest.GetType();
                                    string name = (string)intType.GetField("name")?.GetValue(interest);
                                    string label = (string)intType.GetField("label")?.GetValue(interest);
                                    string desc = (string)intType.GetField("description")?.GetValue(interest);

                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        string display = !string.IsNullOrEmpty(label) ? label : name;
                                        if (!string.IsNullOrEmpty(desc)) display += $" ({desc})";
                                        _interestCache[name] = display;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static string GetPassionInfoHardcoded(Passion passion)
        {
            int passionValue = (int)passion;
            switch (passionValue)
            {
                case 0: return "";
                case 1: return "[Minor]: Interested in this skill.";
                case 2: return "[Major]: Burning passion for this skill.";
                case 3: return "[Apathy]: Absolutely no interest in this skill.";
                case 4: return "[Natural]: Naturally good at this skill, but won't practice.";
                case 5: return "[Critical]: Extremely important skill due to past events.";
                default: return $"[Unknown Passion Level: {passionValue}]";
            }
        }
    }
}