using HarmonyLib;
using RimTalk.Client;
using RimTalk.Data;
using RimTalk.Prompt;
using RimTalk.Service;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimPersonaDirector
{
    public static class DirectorUtils
    {
        // Helper: normalize chattiness to new 0..1 scale by halving values > 1.0
        public static float NormalizeChattiness(float chattiness)
        {
            // If upstream values were produced on 0..2 scale, divide values greater than 1 by 2
            if (chattiness > 1.0f) return chattiness / 2f;
            return chattiness;
        }

        public static string CurrentLanguage
        {
            get
            {
                try
                {
                    if (LanguageDatabase.activeLanguage != null)
                        return LanguageDatabase.activeLanguage.info.friendlyNameNative;
                }
                catch { }
                return "English"; // 保底默认值
            }
        }

        //  1. 生成逻辑 (异步 Task)

        public static string GetFinalPrompt(bool isBatch, string dataContent)
        {
            string userPrompt = DirectorMod.Settings.GetActivePrompt();
            if (string.IsNullOrEmpty(userPrompt)) userPrompt = DirectorSettings.DefaultPrompt_Standard;

            string technicalPrompt = isBatch
                ? DirectorSettings.HiddenTechnicalPrompt_Batch
                : DirectorSettings.HiddenTechnicalPrompt_Single;

            return userPrompt.Replace("{LANG}", CurrentLanguage) +
           "\n" + technicalPrompt +
           "\n\n[Character Data]\n" + dataContent;
        }

        // ★★★ 新增：尝试从 RimTalk 预设渲染 Prompt ★★★
        // ★★★ 纯反射版本：尝试从 RimTalk 预设渲染 ★★★
        private static string TryRenderFromRimTalkPreset(Pawn p, string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || targetName == "None") return null;

            try
            {
                // 1. 查找预设
                var presets = RimTalk.API.RimTalkPromptAPI.GetAllPresets();
                var targetPreset = presets.FirstOrDefault(x => x.Name == targetName);

                if (targetPreset == null)
                {
                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Warning($"[Director] Preset '{targetName}' not found in RimTalk. Available: {string.Join(", ", presets.Select(x => x.Name))}");
                    return null;
                }

                // 2. 获取模板内容
                StringBuilder templateSb = new StringBuilder();
                foreach (var entry in targetPreset.Entries)
                {
                    if (entry.Enabled) templateSb.AppendLine(entry.Content);
                }
                string rawTemplate = templateSb.ToString();

                if (string.IsNullOrWhiteSpace(rawTemplate))
                {
                    if (DirectorMod.Settings.EnableDebugLog) Log.Warning($"[Director] Preset '{targetName}' is empty or all entries are disabled.");
                    return null;
                }

                // 3. ★★★ 基于源码的精准反射 ★★★

                // A. 获取类型
                Type contextType = AccessTools.TypeByName("RimTalk.Prompt.PromptContext");
                Type varStoreType = AccessTools.TypeByName("RimTalk.Prompt.VariableStore"); // 参数类型
                Type parserType = AccessTools.TypeByName("RimTalk.Prompt.ScribanParser");

                if (contextType == null || parserType == null)
                {
                    Log.Error("[Director] Critical: RimTalk Context or Parser types not found.");
                    return null;
                }

                // B. 创建 Context 实例
                // 目标构造函数: public PromptContext(Pawn pawn, VariableStore variableStore = null)
                object contextObj = null;
                try
                {
                    // 我们显式传入 null 作为 variableStore，利用它内部的默认逻辑
                    contextObj = Activator.CreateInstance(contextType, new object[] { p, null });
                }
                catch (Exception createEx)
                {
                    Log.Warning($"[Director] Failed to create PromptContext(Pawn, null): {createEx.Message}");
                    // 尝试备用方案：无参构造 + 属性赋值
                    try
                    {
                        contextObj = Activator.CreateInstance(contextType);
                        AccessTools.Property(contextType, "CurrentPawn").SetValue(contextObj, p);
                        // 设置 AllPawns 为 List<Pawn>{p}
                        var listType = typeof(List<>).MakeGenericType(typeof(Pawn));
                        var list = Activator.CreateInstance(listType);
                        listType.GetMethod("Add").Invoke(list, new object[] { p });
                        AccessTools.Property(contextType, "AllPawns").SetValue(contextObj, list);
                    }
                    catch { }
                }

                if (contextObj == null) return null;

                // C. 注入我们的自定义变量逻辑 (如果之前没有全局注册)
                // 现在的架构是 DirectorApiAdapter 已经在全局注册了变量，
                // 所以 ScribanParser.Render 应该能自动调用到我们的变量，不需要额外操作。

                // D. 调用 Render
                // 签名: public static string Render(string templateText, PromptContext context, bool logErrors = true)
                MethodInfo renderMethod = AccessTools.Method(parserType, "Render", new[] { typeof(string), contextType, typeof(bool) });

                if (renderMethod == null)
                {
                    Log.Error("[Director] ScribanParser.Render method not found.");
                    return null;
                }

                string renderedText = (string)renderMethod.Invoke(null, new object[] { rawTemplate, contextObj, true });
                return renderedText;
            }
            catch (Exception ex)
            {
                // 捕获所有异常并打印，方便调试
                Log.Warning($"[Director] TryRenderFromRimTalkPreset Exception: {ex}");
                return null;
            }
        }

        /// 单体生成任务
        public static async Task<PersonalityData> GeneratePersonalityTask(string characterData, string pawnNameForLog, Pawn pawn)
        {
            try
            {
                // 1. 尝试使用 RimTalk 预设渲染
                string renderedPrompt = TryRenderFromRimTalkPreset(pawn, DirectorMod.Settings.rimTalkPreset_Single);
                TalkRequest request;

                if (!string.IsNullOrEmpty(renderedPrompt))
                {
                    // === 路径 A: 使用了 RimTalk 模板 ===
                    // 此时 renderedPrompt 已经包含了所有数据（因为变量被解析了）
                    // 我们只需要加上 JSON 协议即可
                    string finalInstruction = renderedPrompt + "\n\n" + DirectorSettings.HiddenTechnicalPrompt_Single;
                    string triggerPrompt = "Generate persona based on the instructions.";
                    request = new TalkRequest(triggerPrompt, pawn)
                    {
                        Context = finalInstruction
                    };

                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Message($"[Director] Using RimTalk Preset '{DirectorMod.Settings.rimTalkPresetName}' for {pawnNameForLog}");
                }
                else
                {
                    // === 路径 B: 回退到旧逻辑 (手动拼接) ===
                    // 获取 Mod 设置里的 Prompt
                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Message($"[Director] Using internal prompt for {pawnNameForLog}");

                    string userPrompt = DirectorMod.Settings.GetActivePrompt(false);
                    if (string.IsNullOrEmpty(userPrompt)) userPrompt = DirectorSettings.DefaultPrompt_Standard;

                    // 拼接
                    string finalInstruction = userPrompt.Replace("{LANG}", CurrentLanguage) + "\n" + DirectorSettings.HiddenTechnicalPrompt_Single;
                    string finalData = $"[Character Data]\n{characterData}";
                    
                    request = new TalkRequest(finalData, pawn)
                    {
                        Context = finalInstruction
                    };
                }
                return await AIService.Query<PersonalityData>(request);
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Generation failed: {e.Message}");
                return new PersonalityData("Error generating persona.", 0.5f);
            }
        }

        /// 批量生成任务
        public static async Task<PersonalityData> GenerateBatchPersonaTask(string combinedData, Pawn representative)
        {
            try
            {
                if (DirectorMod.Settings.EnableDebugLog)
                {
                    Log.Message($"[Director] Batch Gen Data:\n{combinedData}");
                }

                string userInstruction = DirectorMod.Settings.GetActivePrompt(false);
                if (string.IsNullOrEmpty(userInstruction)) userInstruction = DirectorSettings.DefaultPrompt_Standard;
                string finalInstruction = userInstruction.Replace("{LANG}", CurrentLanguage) + "\n" + DirectorSettings.HiddenTechnicalPrompt_Batch;
                string finalData = $"[Character Data]\n{combinedData}";

                var request = new TalkRequest(finalData, representative)
                {
                    Context = finalInstruction
                };

                return await AIService.Query<PersonalityData>(request);
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Batch Gen failed: {e.Message}");
                return null;
            }
        }

        //  2. 应用逻辑

        public static void ApplyPersonalityToPawn(Pawn pawn, PersonalityData data)
        {
            if (pawn == null || pawn.Destroyed || data == null) return;

            try
            {
                var hediff = Hediff_Persona.GetOrAddNew(pawn);
                if (hediff != null)
                {
                    hediff.Personality = data.Persona.Trim();
                    // 状态激活
                    hediff.Severity = 1.0f;

                    // 数值处理
                    // 如果 AI 还是因为某些原因（比如旧 Prompt 缓存）返回了 > 1 的数，Clamp 会把它修剪到 1.0

                    if (data.Chattiness < 0.05f)
                    {
                        hediff.TalkInitiationWeight = 0.5f;
                    }
                    else
                    {
                        // 钳位到 0.1 - 1.0
                        // 这样即使旧数据是 1.8，也会变成 1.0，不会出错
                        hediff.TalkInitiationWeight = Mathf.Clamp(data.Chattiness, 0.1f, 1.0f);
                    }

                    // 4. 刷新
                    pawn.health.Notify_HediffChanged(hediff);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Failed to apply personality: {e.Message}");
            }
        }

        public static int ParseAndApplyBatchResult(List<Pawn> pawns, string combinedPersona)
        {
            int appliedCount = 0;
            if (string.IsNullOrEmpty(combinedPersona)) return 0;

            var personaParts = combinedPersona.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in personaParts)
            {
                // 寻找闭合的方括号 ']' 作为分隔点。
                int bracketIndex = part.IndexOf(']');

                // 如果没找到方括号，说明格式彻底乱了，跳过
                if (bracketIndex == -1)
                {
                    if (DirectorMod.Settings.EnableDebugLog) Log.Warning($"[Director] Invalid format (no bracket found): {part.Trim()}");
                    continue;
                }
                
                // 1. 提取 Key，例如 "[ID:Human123]"
                // Substring(0, length) -> 从 0 开始，截取到 ']' 为止
                string keyPart = part.Substring(0, bracketIndex + 1).Trim();

                // 2. 提取内容。从 ']' 后面开始截取，并修 trimmed掉可能存在的冒号、空格、换行
                string text = part.Substring(bracketIndex + 1).TrimStart(':', ' ', '\n', '\r').Trim();

                Pawn target = null;

                // ★ 1. 优先尝试 ID 匹配 (最准确)
                if (keyPart.StartsWith("[ID:") && keyPart.EndsWith("]"))
                {
                    // 提取 ID: [ID:123] -> 123
                    // Substring(4) 跳过 "[ID:"，Length - 5 去掉头尾的 "[ID:" 和 "]"
                    if (keyPart.Length > 5)
                    {
                        string id = keyPart.Substring(4, keyPart.Length - 5);
                        target = pawns.FirstOrDefault(p => p.ThingID == id);
                    }
                }

                // 回退机制：尝试名字匹配 (兼容旧数据或 AI 格式错误)
                if (target == null)
                {
                    string cleanName = keyPart.TrimStart('[').TrimEnd(']').Trim();
                    // 去掉可能残留的 "ID:" 前缀 (万一代码走到这)
                    if (cleanName.StartsWith("ID:")) cleanName = cleanName.Substring(3);

                    target = pawns.FirstOrDefault(p => p.Name != null && p.Name.ToStringFull == cleanName)
                          ?? pawns.FirstOrDefault(p => p.LabelShortCap == cleanName);
                }

                if (target != null)
                {
                    ApplyPersonalityToPawn(target, new PersonalityData(text, 1.0f));
                    appliedCount++;
                }
                else
                {
                    if (DirectorMod.Settings.EnableDebugLog)
                        Log.Warning($"[Director] Could not match result key '{keyPart}' to any pawn.");
                }
            }
            return appliedCount;
        }

        //  3. 数据提取与辅助 (主线程)

        public static string GetCurrentPersonality(Pawn pawn)
        {
            try
            {
                var hediff = Hediff_Persona.GetOrAddNew(pawn);
                if (!string.IsNullOrEmpty(hediff?.Personality))
                {
                    return hediff.Personality;
                }
            }
            catch { }
            return "RPD_Batch_PersonalityNotSet".Translate();
        }

        // ★ 必须改为 public，因为 Window_BatchDirector 要调用它 ★
        public static string BuildCombinedCharacterData(List<Pawn> pawns)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Group Context ---");
            if (!string.IsNullOrEmpty(DirectorMod.Settings.directorNotes)) sb.AppendLine(DirectorMod.Settings.directorNotes);

            foreach (var p in pawns)
            {
                string nameStr = p.Name != null ? p.Name.ToStringFull : p.LabelShortCap;

                sb.AppendLine($"\n\n--- Character [ID:{p.ThingID}] Name: {nameStr} ---");
                sb.AppendLine(BuildCustomCharacterData(p));
            }
            return sb.ToString();
        }

        //  RimTalk 集成部分 (反射缓存)
        private static MethodInfo _rimTalkGetPlayerMethod;
        private static Type _rimTalkWindowType;

        public static void OpenRimTalkDialog(Pawn target)
        {
            try
            {
                // 1. 获取 RimTalk 的 Player Pawn (玩家代理)
                if (_rimTalkGetPlayerMethod == null)
                    _rimTalkGetPlayerMethod = AccessTools.Method("RimTalk.Data.Cache:GetPlayer");

                // 2. 获取 RimTalk 的对话窗口类型
                if (_rimTalkWindowType == null)
                    _rimTalkWindowType = AccessTools.TypeByName("RimTalk.UI.CustomDialogueWindow");

                if (_rimTalkGetPlayerMethod == null || _rimTalkWindowType == null)
                {
                    Messages.Message("RimTalk not found or incompatible.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 3. 决定发起者 (Initiator)
                Pawn initiator = null;

                // --- 修改点：极度宽松的判定 ---
                // 只要选中了一个 Pawn，且没死，且在地图上，就让他当发起人。
                Pawn selectedPawn = Find.Selector.SingleSelectedThing as Pawn;
                if (selectedPawn != null &&
                            !selectedPawn.Dead &&
                            selectedPawn.Spawned &&
                            selectedPawn != target &&
                            (!ModsConfig.AnomalyActive || !selectedPawn.def.race.IsAnomalyEntity)) //如果是自己，就跳到 else 用玩家身份
                {
                    initiator = selectedPawn;
                }
                else
                {
                    // 如果没选中任何 Pawn，或者选中的是石头/建筑，则使用玩家代
                    initiator = (Pawn)_rimTalkGetPlayerMethod.Invoke(null, null);
                }

                if (initiator == null || initiator == target)
                {
                    Messages.Message("Could not determine dialogue initiator.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 4. 打开窗口
                Window dialogWindow = (Window)Activator.CreateInstance(_rimTalkWindowType, initiator, target);
                Find.WindowStack.Add(dialogWindow);
            }
            catch (Exception ex)
            {
                Log.Error($"[Director] Failed to open RimTalk window: {ex}");
            }
        }

        private static FieldInfo contentField;
        private static FieldInfo timestampField;
        private static FieldInfo typeField;
        /// 辅助方法：读取记忆列表，并根据时间戳过滤“新”记忆
        /// </summary>
        /// <param name="lastTick">上次更新的时间 (TicksGame)。如果为 -1，则不进行时间过滤，直接取最新的。</param>
        private static void AppendMemories(StringBuilder sb, IEnumerable list, string header, int limit, int lastTick)
        {
            if (list == null) return;
            var rawEntries = list.Cast<object>().ToList();
            if (rawEntries.Count == 0) return;

            var firstEntryType = rawEntries[0].GetType();

            // 初始化反射字段 (保持不变)
            if (contentField == null) contentField = AccessTools.Field(firstEntryType, "content");
            if (timestampField == null) timestampField = AccessTools.Field(firstEntryType, "timestamp");
            if (typeField == null) typeField = AccessTools.Field(firstEntryType, "type");

            var newMemoryLines = new List<string>();

            foreach (var entry in rawEntries)
            {
                if (newMemoryLines.Count >= limit) break;

                // --- 时间戳过滤 ---
                int memTick = (timestampField != null) ? (int)timestampField.GetValue(entry) : 0;
                if (lastTick > 0 && memTick <= lastTick) break;

                string content = (string)contentField?.GetValue(entry);
                if (string.IsNullOrEmpty(content)) continue;

                // --- 类型名称 ---
                string typeName = "Memory";
                if (typeField != null)
                {
                    object typeEnum = typeField.GetValue(entry);
                    if (typeEnum != null) typeName = typeEnum.ToString();
                }

                // ★★★ 核心修复：自己计算时间，生成英文描述 ★★★
                string timeAgo = "";
                if (memTick > 0)
                {
                    int ticksElapsed = GenTicks.TicksGame - memTick;
                    int daysElapsed = ticksElapsed / GenDate.TicksPerDay;

                    if (daysElapsed < 1) timeAgo = "Today";
                    else if (daysElapsed == 1) timeAgo = "Yesterday";
                    else if (daysElapsed < 15) timeAgo = $"{daysElapsed} days ago";
                    else if (daysElapsed < 30) timeAgo = $"About a season ago ({daysElapsed} days)";
                    else if (daysElapsed < 60) timeAgo = $"Several seasons ago({daysElapsed} days)";
                    else if (daysElapsed < 120) timeAgo = $"About a year ago({daysElapsed} days)";
                    else timeAgo = $"A long time ago({daysElapsed} days)";
                }

                // --- 最终拼接 ---
                string line = $"[{typeName.CapitalizeFirst()}] {content}";
                if (!string.IsNullOrEmpty(timeAgo))
                {
                    line += $" ({timeAgo})";
                }
                newMemoryLines.Add($"- {line}");
            }

            if (newMemoryLines.Count > 0)
            {
                sb.AppendLine($"\n[{header}]:");
                // 翻转，让时间正序
                newMemoryLines.Reverse();
                foreach (var line in newMemoryLines)
                {
                    sb.AppendLine(line);
                }
            }
        }

        private static Type _memoryCompType;
        private static Type _memoryEntryType;
        private static PropertyInfo longTermProp, midTermProp, shortTermProp;
        private static FieldInfo _contentField;
        private static PropertyInfo _timeAgoProp; // TimeAgoString 是属性
        private static PropertyInfo _typeNameProp; // TypeName 是属性

        public static string GetExternalMemories(Pawn p, int lastTick)
        {
            if (!ModsConfig.IsActive("cj.rimtalk.expandmemory")) return null;

            try
            {
                // 1. 初始化反射 (只做一次)
                if (_memoryCompType == null)
                {
                    _memoryCompType = AccessTools.TypeByName("RimTalk.Memory.FourLayerMemoryComp");
                    _memoryEntryType = AccessTools.TypeByName("RimTalk.Memory.MemoryEntry");

                    if (_memoryCompType != null)
                    {
                        // 使用 Property (根据源码，它们是 public 属性)
                        longTermProp = AccessTools.Property(_memoryCompType, "ArchiveMemories");
                        midTermProp = AccessTools.Property(_memoryCompType, "EventLogMemories");
                        shortTermProp = AccessTools.Property(_memoryCompType, "SituationalMemories");
                    }

                    if (_memoryEntryType != null)
                    {
                        _contentField = AccessTools.Field(_memoryEntryType, "content");
                        _timeAgoProp = AccessTools.Property(_memoryEntryType, "TimeAgoString");
                        _typeNameProp = AccessTools.Property(_memoryEntryType, "TypeName");
                    }
                }

                if (_memoryCompType == null || _memoryEntryType == null)
                {
                    if (DirectorMod.Settings.EnableDebugLog) Log.Warning("[Director] Memory types not found via reflection.");
                    return null;
                }

                // 2. 获取组件
                // 使用 Find 以支持子类，这是最稳妥的
                var comp = p.AllComps.FirstOrDefault(c => _memoryCompType.IsAssignableFrom(c.GetType()));

                if (comp == null)
                {
                    // 很多新生成的小人确实没有这个 Comp，这是正常的
                    return null;
                }

                StringBuilder sb = new StringBuilder();

                // 3. 直接读取列表并格式化
                if (longTermProp != null)
                    AppendMemories(sb, longTermProp.GetValue(comp, null) as IEnumerable, "Long-Term (Archive)", 5, lastTick);

                if (midTermProp != null)
                    AppendMemories(sb, midTermProp.GetValue(comp, null) as IEnumerable, "Mid-Term (Recent Events)", 5, lastTick);

                if (shortTermProp != null)
                    AppendMemories(sb, shortTermProp.GetValue(comp, null) as IEnumerable, "Short-Term (Immediate)", 5, lastTick);

                return sb.Length > 0 ? sb.ToString().Trim() : null;
            }
            catch (Exception ex)
            {
                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Warning($"[Director] Critical error reading memories: {ex}");
                return null;
            }
        }

        // ★★★ 注入常识库 ★★★
        private static Type _knowledgeLibType;
        private static MethodInfo _injectDetailedMethod;
        private static Type _listScoreType;
        private static Type _memoryManagerType;

        public static string GetCommonKnowledge(string context, Pawn p)
        {
            // 1. 检查 Mod 是否激活
            if (!ModsConfig.IsActive("cj.rimtalk.expandmemory")) return null;

            try
            {
                // 2. 初始化反射信息 (只做一次)
                if (_injectDetailedMethod == null)
                {
                    _memoryManagerType = AccessTools.TypeByName("RimTalk.Memory.MemoryManager");
                    _knowledgeLibType = AccessTools.TypeByName("RimTalk.Memory.CommonKnowledgeLibrary");
                    Type scoreType = AccessTools.TypeByName("RimTalk.Memory.KnowledgeScore");

                    if (_knowledgeLibType != null && scoreType != null)
                    {
                        _listScoreType = typeof(List<>).MakeGenericType(scoreType);

                        // 目标签名：(string context, int maxEntries, out List<KnowledgeScore> scores, Pawn currentPawn, Pawn targetPawn)
                        _injectDetailedMethod = AccessTools.Method(_knowledgeLibType, "InjectKnowledgeWithDetails",
                            new Type[] {
                                typeof(string),
                                typeof(int),
                                _listScoreType.MakeByRefType(),
                                typeof(Pawn),
                                typeof(Pawn)
                            });
                    }
                }

                if (_injectDetailedMethod == null || _memoryManagerType == null) return null;

                // 3. ★★★ 核心修复：直接使用 GetComponent(Type) ★★★
                // 这是一个公开方法，不需要反射，也不需要遍历列表
                object manager = Find.World.GetComponent(_memoryManagerType);

                if (manager == null) return null;

                // 4. 获取 CommonKnowledgeLibrary 实例
                PropertyInfo libProp = AccessTools.Property(_memoryManagerType, "CommonKnowledge");
                object lib = libProp?.GetValue(manager, null);

                if (lib != null)
                {
                    // 5. 调用注入方法
                    object[] parameters = new object[] { context, 5, null, p, null };
                    string result = (string)_injectDetailedMethod.Invoke(lib, parameters);
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (DirectorMod.Settings.EnableDebugLog)
                    Log.Warning($"[Director] CK Injection Reflection failed: {ex.Message}");
            }
            return null;
        }

        private static MethodInfo _addRequestMethod;
        private static MethodInfo _addResponseMethod;
        private static bool _logReflectionInit = false;

        private static void InitLogReflection()
        {
            if (_logReflectionInit) return;
            _logReflectionInit = true;

            try
            {
                var apiHistoryType = AccessTools.TypeByName("RimTalk.Data.ApiHistory");
                if (apiHistoryType != null)
                {
                    // 1. 查找 AddRequest (尝试新版签名: TalkRequest, Channel)
                    var channelType = AccessTools.TypeByName("RimTalk.Source.Data.Channel"); // 注意命名空间
                    if (channelType != null)
                    {
                        _addRequestMethod = AccessTools.Method(apiHistoryType, "AddRequest", new[] { typeof(TalkRequest), channelType });
                    }
                    // 如果找不到新版，尝试找旧版 (TalkRequest) - 兼容旧版 RimTalk
                    if (_addRequestMethod == null)
                    {
                        _addRequestMethod = AccessTools.Method(apiHistoryType, "AddRequest", new[] { typeof(TalkRequest) });
                    }

                    // 2. 查找 AddResponse (尝试新版签名: Guid, string, string, string, Payload, int)
                    _addResponseMethod = AccessTools.Method(apiHistoryType, "AddResponse",
                        new[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload), typeof(int) });

                    // 如果找不到新版，尝试旧版 (Guid, string, string, string, Payload)
                    if (_addResponseMethod == null)
                    {
                        _addResponseMethod = AccessTools.Method(apiHistoryType, "AddResponse",
                            new[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload) });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[Director] Log reflection failed: {ex.Message}");
            }
        }

        public static (TalkRequest request, string currentPersona) PrepareEvolve(Pawn p, Window editorWindow)
        {
            try
            {
                // 1. 从窗口获取当前正在编辑的文本
                string currentPersona = GetWindowText(editorWindow) ?? "";

                if (string.IsNullOrEmpty(currentPersona))
            {
                Messages.Message("RPD_Msg_NoPersona".Translate(), MessageTypeDefOf.RejectInput, false);
                return (null, null);
            }

            // 2. 获取时间和数据
            var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
            string timeInfo = "No previous update record.";
            string comparisonBlock = "";
            int lastTick = -1;

            if (worldComp != null)
            {
                lastTick = worldComp.GetLastEvolveTick(p);
                if (lastTick > 0)
                {
                    // 1. 计算时间差
                    int daysPassed = (GenTicks.TicksGame - lastTick) / 60000;
                    long ageThen = worldComp.GetLastEvolveBioAgeTicks(p) / 3600000;
                    long ageNow = p.ageTracker.AgeBiologicalYears;

                    timeInfo = $"Time passed since last update: {daysPassed} days.";
                    if (ageNow > ageThen)
                    {
                        timeInfo += $" Character aged from {ageThen} to {ageNow}.";
                    }

                    // ★★★ 核心：数据对比逻辑 ★★★
                    if (DirectorMod.Settings.Context.Inc_DataComparison)
                    {
                        string oldSnapshot = worldComp.GetSnapshot(p);
                        if (!string.IsNullOrEmpty(oldSnapshot))
                        {
                            string currentSnapshot = BuildCustomCharacterData(p, true);

                            // ★★★ 调用差异比较器 ★★★
                            string diffReport = GenerateDiffReport(oldSnapshot, currentSnapshot);

                            // 构建对比块
                            comparisonBlock = $"\n[Status Changes (since last update)]:\n{diffReport}\n";
                        }
                    }
                }
            }

            // 3. 构建 Context (数据部分)
            StringBuilder contextSb = new StringBuilder();
            var ctx = DirectorMod.Settings.Context;

            // 基础信息
            contextSb.AppendLine("[Basic Info]");
            contextSb.AppendLine($"Name: {p.LabelShortCap}");
            contextSb.AppendLine($"Gender: {p.gender}");
            contextSb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
            contextSb.AppendLine($"Status: {GetPawnSocialStatus(p)}");
            contextSb.AppendLine();

            // [基础数据]
            contextSb.AppendLine($"[Previous Persona (The Starting Point)]\n{currentPersona}\n");
            contextSb.AppendLine($"[Time Context]\n{timeInfo}\n");

            // ★★★ 注入差异报告 ★★★
            if (!string.IsNullOrEmpty(comparisonBlock))
            {
                contextSb.AppendLine(comparisonBlock);
            }

            // [选项] 导演备注
            if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(DirectorMod.Settings.directorNotes))
                {
                    contextSb.AppendLine($"[Director's Notes]\n{DirectorMod.Settings.directorNotes}\n");
                }

                // [选项] 记忆 
                string memories = GetExternalMemories(p, lastTick);

                if (!string.IsNullOrEmpty(memories))
                {
                    contextSb.AppendLine($"[New Memories]\n{memories}\n");
                }
                else
                {
                    // 即使没有新记忆，也要告诉 AI，否则它会困惑
                    contextSb.AppendLine("[New Memories]\nNo new significant memories since last update.\n");
                }

            // [选项] 常识库
            if (ctx.Inc_CommonKnowledge)
            {
                // ★★★ 核心优化：构建全量检索源 ★★★
                StringBuilder searchSource = new StringBuilder();

                // 1. 加入基础物理信息 (最重要)
                searchSource.Append($"{p.LabelShort} {p.gender} Age:{p.ageTracker.AgeBiologicalYears} ");

                // 2. 加入当前状态和身份
                searchSource.Append($"{GetPawnSocialStatus(p)} ");

                // 3. 加入现有人格 (提供语义参考)
                searchSource.Append($"{currentPersona} ");

                // 4. 加入新发生的记忆 (匹配近期事件常识)
                if (!string.IsNullOrEmpty(memories)) searchSource.Append($"{memories} ");

                // 5. 加入导演备注 (匹配玩家自设常识)
                if (!string.IsNullOrEmpty(DirectorMod.Settings.directorNotes))
                    searchSource.Append($"{DirectorMod.Settings.directorNotes} ");

                // 6. 甚至可以加入特性标签 
                if (p.story?.traits != null)
                    foreach (var tr in p.story.traits.allTraits) searchSource.Append($"{tr.LabelCap} ");

                // 执行匹配
                string ck = GetCommonKnowledge(searchSource.ToString(), p);

                if (!string.IsNullOrEmpty(ck))
                {
                    contextSb.AppendLine($"[Common Knowledge]\n{ck}\n");
                }
            }
                // 设置临时缓存
                DirectorDataEngine.TempCurrentPersona = currentPersona;
                
                TalkRequest request;
                // 尝试渲染 (使用 Evolve 预设配置)
                string renderedPrompt = TryRenderFromRimTalkPreset(p, DirectorMod.Settings.rimTalkPreset_Evolve);
                // 清空缓存
                DirectorDataEngine.TempCurrentPersona = "";
                string dataPayload = contextSb.ToString();
                if (!string.IsNullOrEmpty(renderedPrompt))
                {
                    string instruction = renderedPrompt + "\n\n" + DirectorSettings.HiddenTechnicalPrompt_Single;
                    request = new TalkRequest("Generate development based on data.", p) { Context = instruction };
                }
                else
                {
                    // --- 组装最终 Prompt ---
                    string instruction = DirectorMod.Settings.presets[3].text.Replace("{LANG}", CurrentLanguage) + "\n" + DirectorSettings.HiddenTechnicalPrompt_Single;
                    string data = "[Update Data]\n" + dataPayload;
                    request = new TalkRequest(data, p) { Context = instruction };
                }
                return (request, currentPersona);
            }
            catch (Exception ex)
            {
                Log.Error($"[Director] Evolve preparation failed: {ex.Message}");
                return (null, null);
            }
        }

        // ★★★ 2. 新方法：应用结果 (主线程安全) ★★★
        public static string ExecuteEvolve(TalkRequest request, string originalPersona)
        {
            if (request == null) return null;

            try
            {
                // ★★★ 核心：在后台线程中阻塞等待 ★★★
                Task<PersonalityData> task = AIService.Query<PersonalityData>(request);
                PersonalityData result = task.Result; // 阻塞后台线程，不影响 UI

                if (result != null && !string.IsNullOrEmpty(result.Persona))
                {
                    return result.Persona.Trim();
                }
            }
            catch (Exception ex)
            {
                // 后台线程记录错误
                Log.Error($"[Director] Evolve execution failed: {ex.Message}");
                // (可选) 调用 TryLogErrorToApiHistory
            }
            return null;
        }

        // ★★★ 新增辅助方法：安全地向 RimTalk 写入错误日志 ★★★
        private static MethodInfo addRequestMethod;
        private static bool apiHistoryReflectionFailed = false;

        public static void TryLogErrorToApiHistory(TalkRequest request, Exception ex)
        {
            if (apiHistoryReflectionFailed) return;

            try
            {
                // 初始化
                if (addRequestMethod == null)
                {
                    var apiHistoryType = AccessTools.TypeByName("RimTalk.Data.ApiHistory");
                    if (apiHistoryType != null)
                    {
                        var channelType = AccessTools.TypeByName("RimTalk.Source.Data.Channel");
                        if (channelType != null)
                        {
                            addRequestMethod = AccessTools.Method(apiHistoryType, "AddRequest", new[] { typeof(TalkRequest), channelType });
                        }
                    }
                    if (addRequestMethod == null) { apiHistoryReflectionFailed = true; return; }
                }

                // 记录请求
                object channelQuery = Enum.Parse(AccessTools.TypeByName("RimTalk.Source.Data.Channel"), "Query");
                object apiLog = addRequestMethod.Invoke(null, new object[] { request, channelQuery });

                if (apiLog != null)
                {
                    // 设置错误状态
                    var logType = apiLog.GetType();
                    AccessTools.Property(logType, "IsError")?.SetValue(apiLog, true);
                    AccessTools.Property(logType, "Response")?.SetValue(apiLog, $"[Director] Task failed: {ex.Message}");
                }
            }
            catch { apiHistoryReflectionFailed = true; }
        }


        /// 智能差异比较器：按数据块分割，对比列表型和键值对型数据。
        /// </summary>
        public static string GenerateDiffReport(string oldSnapshot, string newSnapshot)
        {
            if (oldSnapshot == newSnapshot) return "No significant changes.";

            var oldBlocks = ParseSnapshotToBlocks(oldSnapshot);
            var newBlocks = ParseSnapshotToBlocks(newSnapshot);

            StringBuilder diffSb = new StringBuilder();

            var allBlockTitles = oldBlocks.Keys.Union(newBlocks.Keys).Distinct();

            foreach (var title in allBlockTitles)
            {
                if (title.Contains("Backstory"))
                {
                    continue; // 直接跳过，不进行对比
                }

                oldBlocks.TryGetValue(title, out var oldContent);
                newBlocks.TryGetValue(title, out var newContent);

                if (oldContent == newContent) continue;

                // ★★★ 核心修复：默认列表，特判键值 ★★★
                // 只有明确知道是 Key-Value 格式的块，才用 KeyValue 对比
                // 其他所有（包括未来新增的）都按安全的 List 方式对比
                if (title.Contains("Basic Info") || title.Contains("Skills"))
                {
                    // 使用“键值对对比”模式
                    CompareKeyValueBlock(diffSb, title, oldContent, newContent);
                }
                else
                {
                    // 其他所有块都使用安全的“列表对比”模式
                    CompareListBlock(diffSb, title, oldContent, newContent);
                }
            }

            string report = diffSb.ToString().Trim();
            return string.IsNullOrEmpty(report) ? "No significant changes." : report;
        }

        /// <summary>
        /// 辅助方法：将快照文本解析成 <标题, 内容> 的字典
        /// </summary>
        private static Dictionary<string, string> ParseSnapshotToBlocks(string snapshot)
        {
            var blocks = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(snapshot)) return blocks;

            var lines = snapshot.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string currentTitle = "General";
            StringBuilder currentContent = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("---") && line.EndsWith("---"))
                {
                    // 保存上一个块
                    if (currentContent.Length > 0)
                        blocks[currentTitle] = currentContent.ToString().Trim();

                    // 开始新块
                    currentTitle = line.Trim('-', ' ');
                    currentContent.Clear();
                }
                else
                {
                    currentContent.AppendLine(line);
                }
            }
            // 保存最后一个块
            if (currentContent.Length > 0)
                blocks[currentTitle] = currentContent.ToString().Trim();

            return blocks;
        }

        /// <summary>
        /// 对比列表型数据块 (如 Traits, Genes)
        /// </summary>
        private static void CompareListBlock(StringBuilder diffSb, string title, string oldContent, string newContent)
        {
            var oldItems = new HashSet<string>(string.IsNullOrEmpty(oldContent)
                ? new string[0]
                : oldContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

            var newItems = new HashSet<string>(string.IsNullOrEmpty(newContent)
                ? new string[0]
                : newContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

            var added = newItems.Except(oldItems).ToList();
            var removed = oldItems.Except(newItems).ToList();

            if (added.Any() || removed.Any())
            {
                diffSb.AppendLine($"Changes in {title}:");
                foreach (var item in added) diffSb.AppendLine($"- Added: {item}");
                foreach (var item in removed) diffSb.AppendLine($"- Removed: {item}");
            }
        }

        /// <summary>
        /// 对比键值对型数据块 (如 Basic Info, Skills)
        /// </summary>
        private static void CompareKeyValueBlock(StringBuilder diffSb, string title, string oldContent, string newContent)
        {
            var oldDict = (string.IsNullOrEmpty(oldContent) ? "" : oldContent).Split('\n')
                .Select(line => line.Trim().Split(new[] { ':' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

            var newDict = (string.IsNullOrEmpty(newContent) ? "" : newContent).Split('\n')
                .Select(line => line.Trim().Split(new[] { ':' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

            var changes = new List<string>();
            foreach (var kvp in newDict)
            {
                if (oldDict.TryGetValue(kvp.Key, out var oldValue) && oldValue != kvp.Value)
                {
                    changes.Add($"- {kvp.Key}: {oldValue} -> {kvp.Value}");
                }
            }

            if (changes.Any())
            {
                diffSb.AppendLine($"Changes in {title}:");
                foreach (var change in changes) diffSb.AppendLine(change);
            }
        }

        // --- 反射读写 UI ---

        private static FieldInfo windowTextField;

        public static string GetWindowText(Window window)
        {
            if (window == null) return null;
            if (windowTextField == null)
                windowTextField = AccessTools.Field(window.GetType(), "_editingPersonality");

            return (string)windowTextField?.GetValue(window);
        }

        public static void SetWindowText(Window window, string text)
        {
            if (window == null) return;
            if (windowTextField == null)
                windowTextField = AccessTools.Field(window.GetType(), "_editingPersonality");

            windowTextField?.SetValue(window, text);
        }

        public static string GetPawnSocialStatus(Pawn p)
        {
            string socialStatus = "Unknown";

            // 1. 第一优先级：与殖民地的直接关系 (囚犯/奴隶)
            if (p.IsPrisonerOfColony)
            {
                // 检查 Pawn 是否与任何正在进行的任务相关联
                bool isQuestInvolved = Find.QuestManager.QuestsListForReading
                    .Any(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                socialStatus = isQuestInvolved ? "Quest Prisoner" : "Prisoner";
            }
            else if (p.IsSlaveOfColony)
            {
                socialStatus = "Slave";
            }

            // 2. 第二优先级：异象身份
            if (socialStatus == "Unknown" && ModsConfig.AnomalyActive)
            {
                if (p.IsMutant) socialStatus = "Mutant";
                else if (p.IsCreepJoiner) socialStatus = "Creep Joiner (Mysterious Stranger)";
                else if (p.def.race.IsAnomalyEntity)
                {
                    bool isContained = p.ParentHolder is Building;
                    socialStatus = isContained ? "Anomaly Entity (Contained)" : "Anomaly Entity (Hostile)";
                }
            }

            // 3. 第三优先级：殖民地成员 (细分)
            if (socialStatus == "Unknown" && p.Faction == Faction.OfPlayer)
            {
                // 优先判断是否为临时成员
                if (p.HasExtraHomeFaction() || p.IsQuestLodger())
                {
                    socialStatus = GetTemporaryColonistDescription(p); // 调用下面更新过的辅助函数
                }
                else if (p.IsFreeColonist) socialStatus = "Colonist";
                else if (p.RaceProps.IsMechanoid) socialStatus = "Mechanoid (Colony Controlled)";
                else if (p.RaceProps.Animal) socialStatus = "Tame Animal";
                else socialStatus = "Colony Member";
            }

            // 4. 第四优先级：外部人员
            if (socialStatus == "Unknown" && p.Faction != null)
            {
                // A. 敌对派系人员
                if (p.Faction.HostileTo(Faction.OfPlayer))
                {
                    if (p.DevelopmentalStage == DevelopmentalStage.Baby || p.DevelopmentalStage == DevelopmentalStage.Child)
                    {
                        socialStatus = "Hostile Faction Child (Non-combatant, in colony, usually the child of prisoner in colony)";
                    }
                    else socialStatus = "Enemy / Raider";
                }
                // B. 中立/友好派系人员 (商人会被归入此类)
                else
                {
                    // ★★★ 核心修复：通过 LordJob 判断访客意图 ★★★

                    // 获取控制这个 Pawn 的领主任务 (LordJob)
                    var lord = p.GetLord();

                    if (p.IsSlave)
                    {
                        socialStatus = "Visitor (as a Slave)";
                    }
                    else if (lord != null && lord.LordJob != null)
                    {
                        // A. 商队 (Trader Caravan)
                        // LordJob_TradeWithColony 代表这群人是来做生意的
                        // 即使是保镖，只要在这个队里，也会被标记为 Trader Group
                        if (lord.LordJob is LordJob_TradeWithColony)
                        {
                            socialStatus = "Trader Caravan Member";
                        }
                        // B. 访客 (Guest / Visitor)
                        // LordJob_VisitColony 代表这群人是来串门的 (包括 Hospitality Mod)
                        else if (lord.LordJob is LordJob_VisitColony)
                        {
                            socialStatus = "Guest / Visitor";
                        }
                        // C. 路人 (Traveler)
                        // LordJob_TravelAndExit 代表他们只是路过地图，不打算停留
                        else if (lord.LordJob is LordJob_TravelAndExit)
                        {
                            socialStatus = "Traveler (Passing through)";
                        }
                        // D. 援军 (Ally)
                        // LordJob_AssistColony 代表他们是来帮忙打架的
                        else if (lord.LordJob is LordJob_AssistColony || lord.LordJob is LordJob_DefendPoint)
                        {
                            socialStatus = "Ally Reinforcement";
                        }
                        else
                        {
                            // 其他情况 (比如参加婚礼、参加仪式等)
                            socialStatus = "Visitor";
                        }
                    }
                    else
                    {
                        // 没有 Lord 的散人
                        socialStatus = "Visitor / Guest";
                    }
                }
            }

            // 5. 第五优先级：无派系/野生
            if (socialStatus == "Unknown")
            {
                if (p.RaceProps.Animal) socialStatus = "Wild Animal";
                else if (p.RaceProps.IsMechanoid) socialStatus = "Rogue Mechanoid";
                else socialStatus = "Wild / Independent";
            }

            return socialStatus;
        }

        // ★ 核心修改：无差别的通用数据提取器 ★
        public static string BuildCustomCharacterData(Pawn p, bool isSnapshot = false, bool simpleEquipment = false)
        {
            StringBuilder sb = new StringBuilder();
            var ctx = DirectorMod.Settings.Context;
            string data = DirectorDataEngine.BuildCompleteData(p, simpleEquipment);

            try
            {
                if (ctx.Inc_Basic)
                {
                    sb.AppendLine("--- Basic Info ---");
                    sb.AppendLine($"Name: {p.LabelShortCap}"); // 通用名字
                    sb.AppendLine($"Gender: {p.gender}");
                    sb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
                    sb.AppendLine($"Current Status: {GetPawnSocialStatus(p)}");
                    
                    if (p.Faction != null)
                    {
                        // 1. 玩家派系 (Player Faction)
                        // 包括：正常殖民者、被逮捕的自己人(内部囚犯)、奴隶
                        if (p.Faction.IsPlayer)
                        {
                            sb.Append($"Faction: {p.Faction.Name} (Player Colony)");

                            // 特例：奴隶虽然属于玩家派系，但如果有原派系信息，AI需要知道
                            if (p.IsSlave && p.guest?.SlaveFaction != null)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"Origin Faction (Enslaved from): {p.guest.SlaveFaction.Name}");

                                string originDesc = p.guest.SlaveFaction.def.description;
                                if (!string.IsNullOrEmpty(originDesc))
                                {
                                    sb.AppendLine($"Origin Description: {originDesc.StripTags().Replace('\n', ' ')}");
                                }
                            }
                            else
                            {
                                // 对于殖民者和内部囚犯，不需要发送派系描述
                                // AI 会根据 Current Status (如 "Prisoner (of your Colony)") 理解处境
                                sb.AppendLine();
                            }
                        }
                        // 2. 外部派系 (External Faction)
                        // 包括：未招募的外部囚犯、访客、袭击者
                        else
                        {
                            // 显示派系名和关系（如 Hostile, Ally）
                            string relStr = p.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" : "Neutral/Ally";
                            sb.AppendLine($"Faction: {p.Faction.Name} ({relStr})");

                            // ★★★ 关键：发送外部派系的详细描述 ★★★
                            // 这让 AI 能理解囚犯/访客的文化背景
                            string desc = p.Faction.def.description;
                            if (!string.IsNullOrEmpty(desc))
                            {
                                sb.AppendLine($"Faction Description: {desc.StripTags().Replace('\n', ' ')}");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine("Faction: None (Independent)");
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Race)
                {
                    sb.AppendLine("\n--- Race & Xenotype ---");
                    sb.Append($"Race: {p.def.label}");
                    if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.def.description}"); else sb.AppendLine();

                    if (p.genes != null && p.genes.Xenotype != null && p.genes.Xenotype.defName != "Archite")
                    {
                        sb.Append($"Xenotype: {p.genes.XenotypeLabel}");
                        if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.genes.Xenotype.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
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
            }
            catch { }

            try
            {
                if (ctx.Inc_Backstory && p.story != null)
                {
                    sb.AppendLine("\n--- Backstory ---");
                    if (p.story.Childhood != null)
                    {
                        // 总是先添加标题 (标签)
                        sb.Append($"Childhood: {p.story.Childhood.TitleCapFor(p.gender)}");

                        // 如果开关开启，再附加描述
                        if (ctx.Inc_Backstory_Desc)
                        {
                            // 获取原始描述，并清理一下格式
                            string desc = p.story.Childhood.FullDescriptionFor(p).Resolve();

                            string cleanDesc = desc.Replace(p.story.Childhood.title, "").Trim(); // 尝试移除标题
                            if (!string.IsNullOrWhiteSpace(cleanDesc))
                            {
                                sb.AppendLine($":\n{cleanDesc.StripTags().Trim()}");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            sb.AppendLine();
                        }
                    }
                    if (p.story.Adulthood != null)
                    {
                        // Adulthood 做同样的处理
                        sb.Append($"Adulthood: {p.story.Adulthood.TitleCapFor(p.gender)}");
                        if (ctx.Inc_Backstory_Desc)
                        {
                            string desc = p.story.Adulthood.FullDescriptionFor(p).Resolve();
                            string cleanDesc = desc.Replace(p.story.Adulthood.title, "").Trim();
                            if (!string.IsNullOrWhiteSpace(cleanDesc))
                            {
                                sb.AppendLine($":\n{cleanDesc.StripTags().Trim()}");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            sb.AppendLine();
                        }
                    }
                }
            }
            catch { }

            try
            {
                // 使用 RelatedPawns 以确保能获取到所有类型的关系
                if (ctx.Inc_Relations && p.relations != null && p.relations.RelatedPawns.Any())
                {
                    StringBuilder relationSb = new StringBuilder();

                    foreach (Pawn otherPawn in p.relations.RelatedPawns)
                    {
                        // 获取最重要关系
                        PawnRelationDef relation = p.GetMostImportantRelation(otherPawn);
                        if (relation == null) continue;

                        bool isRelevant =
                            // 1. 核心家庭
                            relation == PawnRelationDefOf.Parent ||
                            relation == PawnRelationDefOf.Child ||
                            relation == PawnRelationDefOf.Sibling ||
                            relation == PawnRelationDefOf.Spouse ||
                            relation == PawnRelationDefOf.Lover ||
                            relation == PawnRelationDefOf.Fiance ||
                            // 2. 牵绊 (人与动物)
                            relation == PawnRelationDefOf.Bond ||
                            // 3. 机械师主人 (Overseer) 
                            relation.defName == "Overseer";

                        if (!isRelevant) continue;

                        // --- 标签处理逻辑 ---
                        string relationLabel = relation.GetGenderSpecificLabelCap(otherPawn);

                        if (relation == PawnRelationDefOf.Child && relationLabel == "Child")
                            relationLabel = otherPawn.gender == Gender.Female ? "Daughter" : "Son";
                        else if (relation == PawnRelationDefOf.Sibling && relationLabel == "Sibling")
                            relationLabel = otherPawn.gender == Gender.Female ? "Sister" : "Brother";

                        // 兜底
                        if (string.IsNullOrEmpty(relationLabel)) relationLabel = relation.label;
                        relationLabel = relationLabel.CapitalizeFirst();

                        // --- 状态与名字 ---
                        string status = GetPawnShortStatus(otherPawn);

                        relationSb.AppendLine($"- {relationLabel}: {otherPawn.Name.ToStringShort} {status}".Trim());
                    }

                    if (relationSb.Length > 0)
                    {
                        sb.AppendLine("\n--- Key Relationships ---");
                        sb.Append(relationSb);
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Traits && p.story?.traits?.allTraits != null)
                {
                    sb.AppendLine("\n--- Traits ---");
                    foreach (var trait in p.story.traits.allTraits)
                    {
                        sb.Append(trait.Label);
                        if (ctx.Inc_Traits_Desc) sb.AppendLine($": {trait.CurrentData.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Ideology && p.Ideo != null)
                {
                    sb.AppendLine("\n--- Ideology ---");
                    sb.AppendLine($"Religion: {p.Ideo.name}");
                    foreach (var meme in p.Ideo.memes)
                    {
                        sb.Append($"Meme [{meme.LabelCap}]");
                        if (ctx.Inc_Ideology_Desc) sb.AppendLine($": {meme.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Skills && p.skills != null)
                {
                    sb.AppendLine("\n--- Skills ---");
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
                                    incapable = true; break;
                                }
                            }
                        }
                        if (incapable) sb.Append("[INCAPABLE]");
                        else
                        {
                            sb.Append(skill.Level);
                            if (ctx.Inc_Skills_Desc && skill.passion != Passion.None)
                            {
                                // 1. 优先尝试从 VSE 获取 PassionDef
                                Def vsePassionDef = GetVSEPassionDef(skill);

                                if (vsePassionDef != null)
                                {
                                    // 如果成功，使用 VSE 的数据
                                    sb.Append($" [{vsePassionDef.label.CapitalizeFirst()}]: {vsePassionDef.description}");
                                }
                                else
                                {
                                    // 2. 如果 VSE 不存在或失败，回退到硬编码方法
                                    sb.Append($" {GetPassionInfoHardcoded(skill.passion)}");
                                }
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                // 9. Health
                if (ctx.Inc_Health && p.health?.hediffSet != null)
                {
                    // 筛选出所有对玩家可见的健康状况
                    var visibleHediffs = p.health.hediffSet.hediffs.Where(h => h.Visible).ToList();

                    if (visibleHediffs.Any())
                    {
                        sb.AppendLine("\n--- Health ---");
                        foreach (var hediff in visibleHediffs)
                        {
                            // 安全地获取身体部位名称
                            string partName = hediff.Part != null ? hediff.Part.LabelCap : "Whole Body";

                            // 标签总是显示
                            sb.Append($"- {partName}: {hediff.LabelCap}");

                            // 根据开关添加描述
                            if (ctx.Inc_Health_Desc && !string.IsNullOrEmpty(hediff.def.description))
                            {
                                // 清理描述中的格式代码，并替换换行符，确保数据干净
                                string cleanDesc = hediff.def.description.StripTags().Replace('\n', ' ');
                                sb.AppendLine($" ({cleanDesc})");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (DirectorMod.Settings.EnableDebugLog) Log.Warning($"[Director] Error fetching health data for {p.LabelShort}: {ex.Message}");
            }

            try
            {
                if (ctx.Inc_Equipment)
                {
                    bool hasItems = false;
                    StringBuilder equipSb = new StringBuilder();
                    if (p.equipment != null)
                    {
                        foreach (var eq in p.equipment.AllEquipmentListForReading)
                        {
                            equipSb.AppendLine($"- [Weapon]: {eq.LabelCap}"); hasItems = true;
                        }
                    }
                    if (p.apparel != null)
                    {
                        foreach (var app in p.apparel.WornApparel)
                        {
                            equipSb.AppendLine($"- [Apparel]: {app.LabelCap}"); hasItems = true;
                        }
                    }
                    if (hasItems) { sb.AppendLine("\n--- Equipment ---"); sb.Append(equipSb); }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Inventory && p.inventory != null && p.inventory.innerContainer.Any)
                {
                    sb.AppendLine("\n--- Inventory ---");
                    foreach (var item in p.inventory.innerContainer)
                    {
                        sb.AppendLine($"- {item.LabelCap}");
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_RimPsyche)
                {
                    string psyData = GetMauxRimPsycheData(p);
                    if (!string.IsNullOrEmpty(psyData))
                    {
                        sb.AppendLine("\n--- RimPsyche ---");
                        sb.Append(psyData);
                    }
                }
            }
            catch { }

            if (!isSnapshot)
            {
                try
                {
                    if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(DirectorMod.Settings.directorNotes))
                    {
                        sb.AppendLine("\n--- Director's Notes (Custom Context) ---");
                        sb.AppendLine(DirectorMod.Settings.directorNotes);
                    }
                }
                catch { }

                if (ctx.Inc_Memories)
                {
                    // 传入 -1，表示不根据时间过滤，直接读取最新的几条
                    string mems = GetExternalMemories(p, -1);

                    if (!string.IsNullOrEmpty(mems))
                    {
                        sb.AppendLine("\n--- Memories ---");
                        sb.AppendLine(mems);
                    }
                }

                // 新增：常识注入 (最后执行) 
                if (ctx.Inc_CommonKnowledge)
                {
                    // ★ 传入 p ★
                    string ck = GetCommonKnowledge(sb.ToString(), p);
                    if (!string.IsNullOrEmpty(ck))
                    {
                        sb.AppendLine("\n--- Common Knowledge ---");
                        sb.AppendLine(ck);
                    }
                }
            }
            return sb.ToString();
        }

        public static string GetTemporaryColonistDescription(Pawn p)
        {
            // 1. 使用 QuestReserves 查找任务
            if (p.IsQuestLodger())
            {
                // 查找任何正在进行的、且“保留”了该 Pawn 的任务
                var foundQuest = Find.QuestManager.QuestsListForReading
                    .FirstOrDefault(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                if (foundQuest != null)
                {
                    // 获取任务名
                    string questName = foundQuest.name;

                    // 获取并清洗任务描述 (Resolve生成文本, StripTags去除颜色代码, Replace去除换行符保持整洁)
                    string questDesc = foundQuest.description.Resolve()
                                        .StripTags()
                                        .Replace("\n", " ")
                                        .Replace("\r", "")
                                        .Trim();

                    return $"Temporary member, here for quest (Quest: {questName})\n- Quest Context: {questDesc}";
                }
                return "Temporary member (reason unknown, likely quest-related)";
            }

            // 2. 检查盟友协助
            if (p.mindState?.duty?.def == DutyDefOf.Defend)
            {
                return "Ally Soldier, here to help defend the colony";
            }

            // 3. 兼容 Hospitality Mod (反射)
            Type hospitalityCompType = AccessTools.TypeByName("Hospitality.CompGuest");
            if (hospitalityCompType != null)
            {
                foreach (var comp in p.AllComps)
                {
                    if (hospitalityCompType.IsAssignableFrom(comp.GetType()))
                    {
                        return "Guest (Hospitality)";
                    }
                }
            }

            return "Temporary Colonist / Ally";
        }

        // 获取简短的 Pawn 状态
        public static string GetPawnShortStatus(Pawn p)
        {
            if (p.Dead) return "(Deceased)";
            if (p.IsPrisonerOfColony) return "(Prisoner in Colony)";
            if (p.IsSlaveOfColony) return "(Slave in Colony)";
            if (p.Faction == Faction.OfPlayer) return "(In Colony)";

            if (p.Faction != null)
            {
                if (p.Faction.HostileTo(Faction.OfPlayer))
                {
                    return "(Hostile)";
                }
                // 核心修正：添加对盟友和中立的判断
                if (p.Faction.PlayerRelationKind == FactionRelationKind.Ally)
                {
                    return "(Ally)";
                }
                if (p.Faction.PlayerRelationKind == FactionRelationKind.Neutral)
                {
                    return "(Neutral)";
                }
            }

            if (p.IsWorldPawn()) return "(Elsewhere)";

            // 如果以上都不是，则不加标签
            return "";
        }

        private static MethodInfo vseMethodCache;
        private static bool vseReflectionFailed = false;

        public static Def GetVSEPassionDef(SkillRecord skill)
        {
            // 1. 检查 VSE 是否激活
            if (!ModsConfig.IsActive("vanillaexpanded.skills") || vseReflectionFailed)
                return null;

            try
            {
                // 2. 初始化反射 (只执行一次)
                if (vseMethodCache == null)
                {
                    Type managerType = AccessTools.TypeByName("VSE.Passions.PassionManager");

                    if (managerType == null)
                    {
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "VSE");

                        if (assembly != null)
                        {
                            managerType = assembly.GetType("VSE.Passions.PassionManager");
                        }
                    }

                    if (managerType != null)
                    {
                        vseMethodCache = AccessTools.Method(managerType, "PassionToDef", new[] { typeof(Passion) });
                    }

                    if (vseMethodCache == null)
                    {
                        vseReflectionFailed = true;
                        return null;
                    }
                }

                // 3. 执行调用
                return (Def)vseMethodCache.Invoke(null, new object[] { skill.passion });
            }
            catch
            {
                // 生产环境保持静默，避免骚扰用户
                vseReflectionFailed = true;
            }

            return null;
        }

        public static string GetPassionInfoHardcoded(Passion passion)
        {
            int val = (int)passion;
            switch (val)
            {
                case 0: return "";
                case 1: return "[Minor]: Interested in this skill.";
                case 2: return "[Major]: Burning passion for this skill.";
                case 3: return "[Apathy]: Absolutely no interest in this skill.";
                case 4: return "[Natural]: Naturally good at this skill, but won't practice";
                case 5: return "[Critical]: Extremely important skill due to past events.";
                default: return $"[Level: {val}]";
            }
        }

        public static string GetMauxRimPsycheData(Pawn p)
        {
            StringBuilder psySb = new StringBuilder();
            object comp = p.AllComps.FirstOrDefault(c => c.GetType().FullName.Contains("RimPsyche") || c.GetType().Name.Contains("Psyche"));
            if (comp == null) return "";
            try
            {
                Type utilityType = AccessTools.TypeByName("Maux36.RimPsyche.Rimpsyche_Utility");
                if (utilityType != null)
                {
                    MethodInfo method = AccessTools.Method(utilityType, "GetPersonalityDescriptionNumber", new Type[] { typeof(Pawn), typeof(int) });
                    if (method != null)
                    {
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
            catch { }
            try
            {
                object interestsTracker = null;
                Type compType = comp.GetType();
                var interestsProp = AccessTools.Property(compType, "Interests");
                if (interestsProp != null) interestsTracker = interestsProp.GetValue(comp, null);
                else
                {
                    var interestsField = AccessTools.Field(compType, "Interests");
                    if (interestsField != null) interestsTracker = interestsField.GetValue(comp);
                }
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
            catch { }
            return psySb.ToString();
        }

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

    }
}