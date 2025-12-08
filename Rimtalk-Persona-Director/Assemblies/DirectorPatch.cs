using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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
            string customData = BuildCustomCharacterData(pawn);

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

            // 7. Ideology
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

            // 8. Skills
            if (ctx.Inc_Skills && p.skills != null)
            {
                sb.AppendLine("\n--- Skills ---");
                foreach (var skill in p.skills.skills)
                {
                    sb.Append($"{skill.def.label}: ");

                    bool isIncapable = false;

                    // 1. 检查技能本身的 WorkTag 禁用
                    if (p.WorkTagIsDisabled(skill.def.disablingWorkTags))
                    {
                        isIncapable = true;
                    }
                    else
                    {
                        // 2. 检查具体的工作类型 (WorkTypeDef)
                        var linkedWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                            .Where(w => w.relevantSkills.Contains(skill.def));

                        // 如果存在相关工作，并且它们全部被禁用了 -> 则技能不可用
                        if (linkedWorkTypes.Any() && linkedWorkTypes.All(w => p.WorkTypeIsDisabled(w)))
                        {
                            isIncapable = true;
                        }
                    }

                    if (isIncapable)
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

            // 9. Health 
            if (ctx.Inc_Health && p.health != null)
            {
                sb.AppendLine("\n--- Health ---");
                foreach (var hediff in p.health.hediffSet.hediffs)
                {
                    if (hediff.Visible)
                    {
                        string partName = hediff.Part != null ? hediff.Part.LabelCap : "Whole Body";
                        sb.Append($"- {partName}: {hediff.LabelCap}");
                        if (ctx.Inc_Health_Desc) sb.AppendLine($" ({hediff.def.description})");
                        else sb.AppendLine();
                    }
                }
            }

            // 10. Director's Notes
            if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(DirectorMod.Settings.directorNotes))
            {
                sb.AppendLine("\n--- Director's Notes (Custom Context) ---");
                sb.AppendLine(DirectorMod.Settings.directorNotes);
            }

            return sb.ToString();
        }

        private static string GetPassionInfoHardcoded(Passion passion)
        {
            int passionValue = (int)passion;
            switch (passionValue)
            {
                case 0: return "";
                case 1: return "[Minor]: Interested in this skill.";
                case 2: return "[Major]: Burning passion for this skill.";
                // VSE Skills:
                case 3: return "[Apathy]: Absolutely no interest in this skill.";
                case 4: return "[Natural]: Naturally good at this skill. However, this means they won’t keep up their practice.";
                case 5: return "[Critical]: This skill is extremely important in some way, most likely due to events in their past.";
                default: return $"[Unknown Passion Level: {passionValue}]";
            }
        }
    }
}