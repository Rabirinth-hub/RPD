
using HarmonyLib;
using RimPersonaDirector;
using RimTalk.Data;
using RimTalk.UI;
using RimTalk.Util;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace RimPersonaDirector
{
	public class HistoryLine
	{
		public string name { get; set; }
		public string target { get; set; }
		public string text { get; set; }
	}

	public static class DirectorDataEngine
	{
		private static MethodInfo getPawnSocialStatusMethod;

		private static MethodInfo getVSEPassionDefMethod;

		private static MethodInfo getPassionInfoHardcodedMethod;

		private static MethodInfo getPawnShortStatusMethod;

		public static string TempCurrentPersona = "";

		public static string BuildCompleteData(Pawn p, bool simpleEquipment = false)
		{
			StringBuilder stringBuilder = new StringBuilder();
			ContextSettings context = DirectorMod.Settings.Context;
			if (context.Inc_Basic)
			{
				stringBuilder.AppendLine(GetBasicInfo(p));
			}
			if (context.Inc_Race)
			{
				stringBuilder.AppendLine(GetRaceInfo(p, context.Inc_Race_Desc));
			}
			if (context.Inc_Genes)
			{
				stringBuilder.AppendLine(GetGenesInfo(p, context.Inc_Genes_Desc));
			}
			if (context.Inc_Backstory)
			{
				stringBuilder.AppendLine(GetBackstoryInfo(p, context.Inc_Backstory_Desc));
			}
			if (context.Inc_Relations)
			{
				stringBuilder.AppendLine(GetRelationsInfo(p));
			}
			if (context.Inc_Traits)
			{
				stringBuilder.AppendLine(GetTraitsInfo(p, context.Inc_Traits_Desc));
			}
			if (context.Inc_Ideology)
			{
				stringBuilder.AppendLine(GetIdeologyInfo(p, context.Inc_Ideology_Desc));
			}
			if (context.Inc_Skills)
			{
				stringBuilder.AppendLine(GetSkillsInfo(p, context.Inc_Skills_Desc));
			}
			if (context.Inc_Health)
			{
				stringBuilder.AppendLine(GetHealthInfo(p, context.Inc_Health_Desc));
			}
			if (context.Inc_Equipment) stringBuilder.AppendLine(GetEquipmentInfo(p, simpleEquipment));
			if (context.Inc_Equipment)
			{
				stringBuilder.AppendLine(GetEquipmentInfo(p));
			}
			if (context.Inc_Inventory)
			{
				stringBuilder.AppendLine(GetInventoryInfo(p));
			}
			if (context.Inc_RimPsyche)
			{
				stringBuilder.AppendLine(GetRimPsycheInfo(p));
			}
			if (context.Inc_Memories)
			{
				stringBuilder.AppendLine(GetMemoryInfo(p));
			}
			if (context.Inc_CommonKnowledge)
			{
				stringBuilder.AppendLine(GetCommonKnowledgeInfo(p, stringBuilder.ToString()));
			}
			return stringBuilder.ToString();
		}

		public static string GetBasicInfo(Pawn p)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("--- Basic Info ---");
			stringBuilder.AppendLine("Name: " + p.LabelShortCap);
			stringBuilder.AppendLine($"Gender: {p.gender}");
			stringBuilder.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
			if (getPawnSocialStatusMethod == null)
			{
				Type typeFromHandle = typeof(DirectorUtils);
				getPawnSocialStatusMethod = AccessTools.Method(typeFromHandle, "GetPawnSocialStatus", new Type[1] { typeof(Pawn) }, (Type[])null);
			}
			if (getPawnSocialStatusMethod != null)
			{
				string text = (string)getPawnSocialStatusMethod.Invoke(null, new object[1] { p });
				stringBuilder.AppendLine("Status: " + text);
			}
			if (p.Faction != null)
			{
				if (p.Faction.IsPlayer)
				{
					stringBuilder.Append("Faction: " + p.Faction.Name + " (Player Colony)");
					if (p.IsSlave && p.guest?.SlaveFaction != null)
					{
						stringBuilder.AppendLine();
						stringBuilder.AppendLine("Origin Faction (Enslaved from): " + p.guest.SlaveFaction.Name);
						string description = p.guest.SlaveFaction.def.description;
						if (!string.IsNullOrEmpty(description))
						{
							stringBuilder.AppendLine("Origin Description: " + description.StripTags().Replace('\n', ' '));
						}
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					string text2 = (p.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" : "Neutral/Ally");
					stringBuilder.AppendLine("Faction: " + p.Faction.Name + " (" + text2 + ")");
					string description2 = p.Faction.def.description;
					if (!string.IsNullOrEmpty(description2))
					{
						stringBuilder.AppendLine("Faction Description: " + description2.StripTags().Replace('\n', ' '));
					}
				}
			}
			else
			{
				stringBuilder.AppendLine("Faction: None (Independent)");
			}
			return stringBuilder.ToString();
		}

		public static string GetRaceInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Race: " + p.def.label);
			if (includeDesc)
			{
				stringBuilder.AppendLine(": " + p.def.description.StripTags());
			}
			else
			{
				stringBuilder.AppendLine();
			}
			if (p.genes?.Xenotype != null)
			{
				stringBuilder.Append("Xenotype: " + p.genes.XenotypeLabel);
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + p.genes.Xenotype.description.StripTags());
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		public static string GetGenesInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (p.genes.Endogenes.Any())
			{
				stringBuilder.Append("[Endogenes (Natural)]: ");
				foreach (Gene endogene in p.genes.Endogenes)
				{
					if (endogene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !endogene.Overridden)
					{
						stringBuilder.Append(endogene.LabelCap);
						if (includeDesc)
						{
							stringBuilder.Append("(" + endogene.def.description + ")");
						}
						stringBuilder.Append(", ");
					}
				}
				stringBuilder.AppendLine();
			}
			if (p.genes.Xenogenes.Any())
			{
				stringBuilder.Append("[Xenogenes (Artificial)]: ");
				foreach (Gene xenogene in p.genes.Xenogenes)
				{
					if (xenogene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !xenogene.Overridden)
					{
						stringBuilder.Append(xenogene.LabelCap);
						if (includeDesc)
						{
							stringBuilder.Append("(" + xenogene.def.description + ")");
						}
						stringBuilder.Append(", ");
					}
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		public static string GetBackstoryInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (p.story.Childhood != null)
			{
				stringBuilder.Append("Childhood: " + p.story.Childhood.TitleCapFor(p.gender));
				if (includeDesc)
				{
					string text = p.story.Childhood.FullDescriptionFor(p).Resolve();
					string text2 = text.Replace(p.story.Childhood.title, "").Trim();
					if (!string.IsNullOrWhiteSpace(text2))
					{
						stringBuilder.AppendLine(":\n" + text2.StripTags().Trim());
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			if (p.story.Adulthood != null)
			{
				stringBuilder.Append("Adulthood: " + p.story.Adulthood.TitleCapFor(p.gender));
				if (includeDesc)
				{
					string text3 = p.story.Adulthood.FullDescriptionFor(p).Resolve();
					string text4 = text3.Replace(p.story.Adulthood.title, "").Trim();
					if (!string.IsNullOrWhiteSpace(text4))
					{
						stringBuilder.AppendLine(":\n" + text4.StripTags().Trim());
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		public static string GetRelationsInfo(Pawn p)
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			foreach (Pawn relatedPawn in p.relations.RelatedPawns)
			{
				PawnRelationDef mostImportantRelation = p.GetMostImportantRelation(relatedPawn);
				if (mostImportantRelation != null && (mostImportantRelation == PawnRelationDefOf.Parent || mostImportantRelation == PawnRelationDefOf.Child || mostImportantRelation == PawnRelationDefOf.Sibling || mostImportantRelation == PawnRelationDefOf.Spouse || mostImportantRelation == PawnRelationDefOf.Lover || mostImportantRelation == PawnRelationDefOf.Fiance || mostImportantRelation == PawnRelationDefOf.Bond || mostImportantRelation.defName == "Overseer"))
				{
					string text = mostImportantRelation.GetGenderSpecificLabelCap(relatedPawn);
					if (mostImportantRelation == PawnRelationDefOf.Child && text == "Child")
					{
						text = ((relatedPawn.gender == Gender.Female) ? "Daughter" : "Son");
					}
					else if (mostImportantRelation == PawnRelationDefOf.Sibling && text == "Sibling")
					{
						text = ((relatedPawn.gender == Gender.Female) ? "Sister" : "Brother");
					}
					if (string.IsNullOrEmpty(text))
					{
						text = mostImportantRelation.label;
					}
					text = text.CapitalizeFirst();
					if (getPawnShortStatusMethod == null)
					{
						Type typeFromHandle = typeof(DirectorUtils);
						getPawnShortStatusMethod = AccessTools.Method(typeFromHandle, "GetPawnShortStatus", new Type[1] { typeof(Pawn) }, (Type[])null);
					}
					string text2 = "";
					if (getPawnShortStatusMethod != null)
					{
						text2 = (string)getPawnShortStatusMethod.Invoke(null, new object[1] { relatedPawn });
					}
					stringBuilder2.AppendLine(("- " + text + ": " + relatedPawn.Name.ToStringShort + " " + text2).Trim());
				}
			}
			if (stringBuilder2.Length > 0)
			{
				stringBuilder.AppendLine("\n--- Key Relationships ---");
				stringBuilder.Append(stringBuilder2);
			}
			return stringBuilder.ToString();
		}

		public static string GetTraitsInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Trait allTrait in p.story.traits.allTraits)
			{
				stringBuilder.Append(allTrait.Label);
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + allTrait.CurrentData.description);
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		public static string GetIdeologyInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Religion: " + p.Ideo.name);
			foreach (MemeDef meme in p.Ideo.memes)
			{
				stringBuilder.Append($"Meme [{meme.LabelCap}]");
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + meme.description);
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		public static string GetSkillsInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (SkillRecord skill in p.skills.skills)
			{
				stringBuilder.Append(skill.def.label + ": ");
				bool flag = p.WorkTagIsDisabled(WorkTags.AllWork);
				if (!flag)
				{
					foreach (WorkTypeDef item in DefDatabase<WorkTypeDef>.AllDefsListForReading)
					{
						if (item.relevantSkills.Contains(skill.def) && p.WorkTypeIsDisabled(item))
						{
							flag = true;
							break;
						}
					}
				}
				if (flag)
				{
					stringBuilder.Append("[INCAPABLE]");
				}
				else
				{
					stringBuilder.Append(skill.Level);
					if (includeDesc && skill.passion != Passion.None)
					{
						if (getVSEPassionDefMethod == null)
						{
							getVSEPassionDefMethod = AccessTools.Method(typeof(DirectorUtils), "GetVSEPassionDef", new Type[1] { typeof(SkillRecord) }, (Type[])null);
						}
						Def def = null;
						if (getVSEPassionDefMethod != null)
						{
							def = (Def)getVSEPassionDefMethod.Invoke(null, new object[1] { skill });
						}
						if (def != null)
						{
							stringBuilder.Append(" [" + def.label.CapitalizeFirst() + "]: " + def.description);
						}
						else
						{
							if (getPassionInfoHardcodedMethod == null)
							{
								getPassionInfoHardcodedMethod = AccessTools.Method(typeof(DirectorUtils), "GetPassionInfoHardcoded", new Type[1] { typeof(Passion) }, (Type[])null);
							}
							if (getPassionInfoHardcodedMethod != null)
							{
								string text = (string)getPassionInfoHardcodedMethod.Invoke(null, new object[1] { skill.passion });
								stringBuilder.Append(" " + text);
							}
						}
					}
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		public static string GetHealthInfo(Pawn p, bool includeDesc)
		{
			StringBuilder stringBuilder = new StringBuilder();
			List<Hediff> list = p.health.hediffSet.hediffs.Where((Hediff h) => h.Visible).ToList();
			if (list.Any())
			{
				stringBuilder.AppendLine("\n--- Health ---");
				foreach (Hediff item in list)
				{
					string text = ((item.Part != null) ? item.Part.LabelCap : "Whole Body");
					stringBuilder.Append("- " + text + ": " + item.LabelCap);
					if (includeDesc && !string.IsNullOrEmpty(item.def.description))
					{
						string text2 = item.def.description.StripTags().Replace('\n', ' ');
						stringBuilder.AppendLine(" (" + text2 + ")");
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
			}
			return stringBuilder.ToString();
		}

		public static string GetEquipmentInfo(Pawn p, bool simpleMode = false)
		{
			var sb = new StringBuilder();
			bool hasContent = false;

			// 武器
			if (p.equipment != null)
			{
				foreach (var eq in p.equipment.AllEquipmentListForReading)
				{
					if (!hasContent) { sb.AppendLine("--- Equipment ---"); hasContent = true; }

					// 根据模式选择标签生成方式
					string label = simpleMode ? GetStableThingLabel(eq) : eq.LabelCap;
					sb.AppendLine($"- [Weapon]: {label}");
				}
			}

			// 服装
			if (p.apparel != null)
			{
				foreach (var app in p.apparel.WornApparel)
				{
					if (!hasContent) { sb.AppendLine("--- Equipment ---"); hasContent = true; }

					string label = simpleMode ? GetStableThingLabel(app) : app.LabelCap;
					sb.AppendLine($"- [Apparel]: {label}");
				}
			}

			return sb.ToString();
		}

		// ★★★ 核心：构建不受耐久度/磨损影响的稳定标签 ★★★
		// 只包含：材质 + 物品名 + 品质 (例如：传奇级 合成纤维T恤衫)
		private static string GetStableThingLabel(Thing t)
		{
			// GenLabel.ThingLabel 基础生成 (材质+名字)
			string baseLabel = GenLabel.ThingLabel(t.def, t.Stuff).CapitalizeFirst();

			// 手动拼接品质 (如果存在)
			if (t.TryGetComp<CompQuality>() is CompQuality qc)
			{
				baseLabel += $" ({qc.Quality.GetLabel()})";
			}

			// 忽略耐久度 (HitPoints) 和 磨损前缀 (Tattered/Worn out)
			return baseLabel;
		}

		public static string GetInventoryInfo(Pawn p)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("\n--- Inventory ---");
			foreach (Thing item in p.inventory.innerContainer)
			{
				stringBuilder.AppendLine("- " + item.LabelCap);
			}
			return stringBuilder.ToString();
		}

		public static string GetRimPsycheInfo(Pawn p)
		{
			return DirectorUtils.GetMauxRimPsycheData(p);
		}

		public static string GetMemoryInfo(Pawn p)
		{
			return DirectorUtils.GetExternalMemories(p, -1) ?? "";
		}

		public static string GetCommonKnowledgeInfo(Pawn p, string context)
		{
			return DirectorUtils.GetCommonKnowledge(context, p) ?? "";
		}

		public static string GetEvolveStatusDiff(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			if (worldComp == null) return "";

			string oldSnapshot = worldComp.GetSnapshot(p); // 这是一个 Detailed 快照
			if (string.IsNullOrEmpty(oldSnapshot)) return "No previous snapshot.";

			// ★ 生成当前的 Detailed 快照进行对比 ★
			string currentSnapshot = DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: false);

			return DirectorUtils.GenerateDiffReport(oldSnapshot, currentSnapshot);
		}

		// GetDailyStatusDiff 逻辑已经在 WorldComponent 里封装好了，直接调用即可
		public static string GetDailyStatusDiff(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			return worldComp?.GetOrUpdateDailyDiff(p) ?? "";
		}

		public static string GetEvolveMemories(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			int lastTick = worldComp?.GetLastEvolveTick(p) ?? -1;

			// 传入 lastTick，DirectorUtils.GetExternalMemories 会自动过滤掉旧记忆
			// 如果 lastTick 是 -1，它会返回最近的几条（作为保底）
			return DirectorUtils.GetExternalMemories(p, lastTick) ?? "No new memories.";
		}

		public static string GetTimeInfo(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			if (worldComp == null) return "No records.";
			int lastTick = worldComp.GetLastEvolveTick(p);
			if (lastTick <= 0) return "No previous update record.";

			int days = (GenTicks.TicksGame - lastTick) / 60000;
			long ageThen = worldComp.GetLastEvolveBioAgeTicks(p) / 3600000;
			long ageNow = p.ageTracker.AgeBiologicalYears;

			string info = $"{days} days passed.";
			if (ageNow > ageThen) info += $" Aged {ageThen}->{ageNow}.";
			return info;
		}

        private static FieldInfo _rawHistoryDictField;

        public static string GetSmartHistory(Pawn currentPawn, List<Pawn> allPawns, bool isMonologue)
        {
            if (currentPawn == null) return "";

            // 1. 获取预算
            int totalBudget = 5;
            try { totalBudget = RimTalk.Settings.Get().Context.ConversationHistoryCount; } catch { }
            if (isMonologue) totalBudget = Math.Min(totalBudget, 3);

            // 2. 确定人员
            var participants = allPawns ?? new List<Pawn>();
            if (!participants.Contains(currentPawn)) participants.Insert(0, currentPawn);

            // 3. 配额分配
            Dictionary<Pawn, int> pawnQuotas = new Dictionary<Pawn, int>();
            int pawnCount = participants.Count;

            if (totalBudget >= pawnCount)
            {
                foreach (var p in participants) pawnQuotas[p] = 1;
                pawnQuotas[currentPawn] += (totalBudget - pawnCount);
            }
            else
            {
                pawnQuotas[currentPawn] = 1;
                int remaining = totalBudget - 1;
                foreach (var p in participants)
                {
                    if (p == currentPawn) continue;
                    if (remaining > 0) { pawnQuotas[p] = 1; remaining--; }
                    else { pawnQuotas[p] = 0; }
                }
            }

            // 4. 执行提取
            StringBuilder finalSb = new StringBuilder();
            bool hasContent = false;

            // ★★★ 新增：全局去重池 (存储已处理过的原始 JSON 字符串) ★★★
            // 这样就能跨 Pawn 去重了
            HashSet<string> processedDialogues = new HashSet<string>();

            foreach (var p in participants)
            {
                if (!pawnQuotas.TryGetValue(p, out int quota) || quota <= 0) continue;

                // ★★★ 传入去重池 ★★★
                List<string> historyLines = ExtractHistoryForPawn(p, participants, quota, processedDialogues);

                if (historyLines != null && historyLines.Count > 0)
                {
                    if (hasContent) finalSb.AppendLine();
                    finalSb.AppendLine($"[History: {p.LabelShort}]");
                    for (int i = 0; i < historyLines.Count; i++)
                    {
                        finalSb.AppendLine($"[{i + 1}] {historyLines[i]}");
                    }
                    hasContent = true;
                }
            }

            if (hasContent)
            {
                if (isMonologue) return finalSb.ToString().TrimEnd();
                return "[Relevant Conversation History]:\n" + finalSb.ToString().TrimEnd();
            }
            return "";
        }

        // ★★★ 修改：返回 List<string> 而不是 string ★★★
                private static List<string> ExtractHistoryForPawn(Pawn p, List<Pawn> contextPawns, int limit, HashSet<string> processedDialogues)
        {
            var activeNames = new HashSet<string>();
            foreach (var cp in contextPawns) activeNames.Add(cp.LabelShort);

            // ... (反射获取 historyDict 和 fullList 的代码，保持不变) ...
            if (_rawHistoryDictField == null)
                _rawHistoryDictField = AccessTools.Field(typeof(TalkHistory), "MessageHistory");
            if (_rawHistoryDictField == null) return null;
            
            var historyDict = _rawHistoryDictField.GetValue(null) as IDictionary;
            if (historyDict == null || !historyDict.Contains(p.thingIDNumber)) return null;
            var fullList = historyDict[p.thingIDNumber] as IEnumerable;
            if (fullList == null) return null;

            List<object> snapshotList;
            lock (fullList) { snapshotList = fullList.Cast<object>().ToList(); }

            var resultLines = new List<string>();

            // 倒序扫描
            for (int i = snapshotList.Count - 1; i >= 0; i--)
            {
                if (resultLines.Count >= limit) break;

                object entry = snapshotList[i];
                var type = entry.GetType();
                
                // 这里的反射稍微优化一下，避免重复获取 FieldInfo，但这里先保持原样
                string rawContent = (string)type.GetField("Item2").GetValue(entry);
                string roleName = type.GetField("Item1").GetValue(entry).ToString();

                if (roleName == "AI")
                {
                    // ★★★ 核心修复：去重检查 ★★★
                    // 如果这段 JSON 已经在之前的 Pawn (比如 Initiator) 那里处理过了，
                    // 就直接跳过，继续往回找更早的记录。
                    if (processedDialogues.Contains(rawContent))
                    {
                        continue; 
                    }

                    try
                    {
                        var lines = JsonUtil.DeserializeFromJson<List<HistoryLine>>(rawContent);
                        if (lines != null && lines.Count > 0)
                        {
                            StringBuilder sessionSb = new StringBuilder();
                            bool isRelevant = false;

                            foreach (var line in lines)
                            {
                                bool nameInContext = activeNames.Contains(line.name);
                                bool targetInContext = !string.IsNullOrEmpty(line.target) && activeNames.Contains(line.target);

                                if (nameInContext || targetInContext)
                                {
                                    isRelevant = true;
                                }

                                string targetInfo = "";
                                if (!string.IsNullOrEmpty(line.target) && line.target != "None" && line.target != "自己" && line.target != line.name)
                                {
                                    targetInfo = $" (to {line.target})";
                                }
                                sessionSb.AppendLine($"{line.name}{targetInfo}: {line.text}");
                            }

                            if (isRelevant)
                            {
                                // 插入结果
                                resultLines.Insert(0, sessionSb.ToString().TrimEnd());
                                
                                // ★★★ 标记为已处理 ★★★
                                processedDialogues.Add(rawContent);
                            }
                        }
                    }
                    catch { }
                }
            }

            return resultLines;
        }

        public static string GetActiveUIText(Pawn p)
        {
            // 1. 优先读取我们手动设置的缓存 (给 Evolve 功能用)
            if (!string.IsNullOrEmpty(TempCurrentPersona)) return TempCurrentPersona;

            // 2. 如果缓存为空，尝试从当前打开的窗口里抓取
            try
            {
                // 获取当前最顶层的窗口
                var window = Find.WindowStack.WindowOfType<PersonaEditorWindow>();

                if (window != null)
                {
                    // 检查这个窗口是不是正在编辑当前的 Pawn
                    // (防止多个人物窗口重叠时的混淆，虽然 RimTalk 通常只开一个)
                    var windowPawn = AccessTools.Field(typeof(PersonaEditorWindow), "_pawn").GetValue(window) as Pawn;

                    if (windowPawn == p)
                    {
                        // 反射读取文本框字段 _editingPersonality
                        var text = AccessTools.Field(typeof(PersonaEditorWindow), "_editingPersonality").GetValue(window) as string;
                        return text ?? "";
                    }
                }
            }
            catch 
            {
                // 静默失败，不要崩
            }

            // 3. 如果 UI 没打开，或者读不到，回退到读取 Hediff (已保存的数据)
            var hediff = Hediff_Persona.GetOrAddNew(p);
            return hediff?.Personality ?? "";
        }

        public static string GetDataByKey(Pawn p, string key)
		{
			if (p == null) return "";

			switch (key)
			{
				case "full_profile": return DirectorDataEngine.BuildCompleteData(p);
				case "basic.name": return p.LabelShortCap;
				case "basic.fullname": return p.Name?.ToStringFull ?? p.LabelShortCap;
				case "basic.gender": return p.gender.ToString();
				case "basic.age": return p.ageTracker.AgeBiologicalYears.ToString();
				case "basic.status": return DirectorUtils.GetPawnSocialStatus(p);
				case "basic.faction.label": return p.Faction?.Name ?? "None";
				case "basic.faction.desc": return p.Faction?.def?.description?.StripTags() ?? "";
				case "race.label": return p.def.label;
				case "race.desc": return p.def.description.StripTags();
				case "race.xenotype.label": return p.genes?.Xenotype?.label ?? "Baseliner";
				case "race.xenotype.desc": return p.genes?.Xenotype?.description.StripTags() ?? "";
				case "genes.list": return DirectorDataEngine.GetGenesInfo(p, includeDesc: false);
				case "genes.list_with_desc": return DirectorDataEngine.GetGenesInfo(p, includeDesc: true);
				case "backstory.childhood.title": return p.story?.Childhood?.TitleCapFor(p.gender) ?? "";
				case "backstory.childhood.desc": return p.story?.Childhood?.FullDescriptionFor(p).Resolve().StripTags() ?? "";
				case "backstory.adulthood.title": return p.story?.Adulthood?.TitleCapFor(p.gender) ?? "";
				case "backstory.adulthood.desc": return p.story?.Adulthood?.FullDescriptionFor(p).Resolve().StripTags() ?? "";
				case "traits.list": return DirectorDataEngine.GetTraitsInfo(p, includeDesc: false);
				case "traits.list_with_desc": return DirectorDataEngine.GetTraitsInfo(p, includeDesc: true);
				case "ideology.list": return DirectorDataEngine.GetIdeologyInfo(p, includeDesc: false);
				case "ideology.list_with_desc": return DirectorDataEngine.GetIdeologyInfo(p, includeDesc: true);
				case "skills.list": return DirectorDataEngine.GetSkillsInfo(p, includeDesc: false);
				case "skills.list_with_desc": return DirectorDataEngine.GetSkillsInfo(p, includeDesc: true);
				case "health.list": return DirectorDataEngine.GetHealthInfo(p, includeDesc: false);
				case "health.list_with_desc": return DirectorDataEngine.GetHealthInfo(p, includeDesc: true);
				case "relations": return DirectorDataEngine.GetRelationsInfo(p);
				case "equipment": return DirectorDataEngine.GetEquipmentInfo(p);
				case "inventory": return DirectorDataEngine.GetInventoryInfo(p);
				case "rimpsyche": return DirectorDataEngine.GetRimPsycheInfo(p);
				case "memories": return DirectorDataEngine.GetMemoryInfo(p);
				case "common_knowledge": return DirectorDataEngine.GetCommonKnowledgeInfo(p, DirectorDataEngine.BuildCompleteData(p));
				default:
					return ""; // 或者返回 $"{{Unknown: {key}}}"
			}
		}
	}
}
