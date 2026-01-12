using HarmonyLib;
using RimTalk.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    // 新增：预设数据结构
    public class PromptPreset : IExposable
    {
        public string label;
        public string text;

        public PromptPreset() { }
        public PromptPreset(string label, string text)
        {
            this.label = label;
            this.text = text;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref text, "text");
        }
    }

    // 单个预设
    public class CustomPreset : IExposable
    {
        public string id;
        public string label;
        public string personaText;
        public float chattiness = 1.0f;
        public string category = "Default";
        public CustomPreset()
        {
            id = System.Guid.NewGuid().ToString();
        }

        public CustomPreset(string label, string text) : this()
        {
            this.label = label;
            this.personaText = text;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref personaText, "personaText");
            Scribe_Values.Look(ref chattiness, "chattiness", 1.0f);
            Scribe_Values.Look(ref category, "category", "Default");
        }
    }

    public enum RuleType { FactionDef, RaceDef, XenotypeDef }

    // 分配规则
    public class AssignmentRule : IExposable
    {
        public bool enabled = true;
        public string targetDefName; // 只存字符串，防崩坏
        public RuleType type;
        public int priority = 0;
        public List<string> allowedPresetIds = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref targetDefName, "targetDefName");
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref priority, "priority", 0);
            Scribe_Collections.Look(ref allowedPresetIds, "allowedPresetIds", LookMode.Value);
        }
    }

    public class DirectorSettings : ModSettings
    {
        // =============================================================
        // Prompt Templates (Default Constants)
        // =============================================================

        // 1. 标准模式 (原版三选一)
        public const string DefaultPrompt_Standard = @"# Role: Rimworld Persona Director
# Language: {LANG}

# Task:
Read the [Character Data] to generate 3 distinct 'System Instruction' options.

# PRIORITY: [Director's Notes]
The [Director's Notes] are the Absolute Anchor.They override game data. All options must align with them.

# PROHIBITIONS:
1. NO DATA DUMPING: NEVER mention specific numbers (e.g., ""Shooting 10""), skill levels, or raw gene/trait names.
2. TRANSLATE: Convert stats to narrative.

# CREATIVE RULES:
1. Extrapolate: If data is sparse, invent reasonable details based on Traits/Backstory.
2. Voice: Define their Speaking Style.
3. Context:
   - Skills (0-20): High = Professional habits/jargon. Low = Avoidance/Insecurity.
   - [INCAPABLE]: Trauma, disability, or arrogance.
   - Relations: Convert status (Deceased/Hostile) into emotional baggage.

# OUTPUT STRATEGY:
Generate 3 distinct personality interpretations.Each option is described in a paragraph.

# Content Template:
---
### Option 1: [2-4 word Style]
[Rich Description: Describe a story based on data and background about the character's past and why/how they became the current role. Invent a short-term psychological goal. Explicitly describe their speaking tempo, vocabulary, and attitude.]

---
### Option 2: [2-4 word Style]
[Different approach...]

---
### Option 3: [2-4 word Style]
[Different approach...]
";

        // 2. 故事模式 （3选1变单选）
        public const string DefaultPrompt_Simple = @"# Role: Rimworld Fiction Writer
# Language: {LANG}

# TASK:
Ignore the constraints of a simulation. Invent a backstory based on the [Character Data].
Your goal is to create a character with Depth and Dimension.

# PRIORITY: [Director's Notes]
The [Director's Notes] are the Absolute Anchor.They override game data. All options must align with them.

# PROHIBITIONS:
1. NO DATA DUMPING: NEVER mention specific numbers (e.g., ""Shooting 10""), skill levels, or raw gene/trait names.
2. TRANSLATE: Convert stats to narrative.

# CREATIVE RULES:
1. Tonal Agnostic : 
   - Do not force a specific style. Let the Data dictate the tone.
   - Goal: Reflect the full spectrum of humanity: from the tragic to the ridiculous, from the evil to the saintly.
2. The Hidden Dimension: 
   - Every character needs a layer that isn't immediately obvious. It could be a secret crime, a hidden talent, a petty grudge, or a soft spot.
   - It doesn't have to be dramatic; it just has to be human.
3. Grounded Reality: 
   - Unless the data implies high-tech origins, avoid sci-fi tropes (clones/amnesia). 
   - Focus on relatable human experiences: survival, ambition, family, laziness, loyalty, or greed.
4.  Blank Slate Protocol:
    -   Analyze the sociological and psychological evolution from the Childhood environment to the Adulthood role.
   -If [Backstory] or [Traits] are absent: Fabricate a personality. Anthropomorphize Lightly. Give them a distinct personality.

# OUTPUT STRATEGY:
Generate ONE single, fluid narrative profile.
Do not break it into sections. Blend the story, voice, and personality into one paragraph.

# Content Template:
[2-4 word Style]
[Start by revealing a unique story or secret that explains their past. Connect this story to why they became their current role. Invent a specific short-term psychological goal driven by this story. Explicitly describe how this story affects their speaking tempo, vocabulary, and attitude. Keep it all in one solid paragraph.]";

        // 3. 背景模式
        public const string DefaultPrompt_Strict = @"# Role: Rimworld Behavioral Profiler
# Language: {LANG}

# TASK
Perform a strict Logical Synthesis of the [Character Data].
CRITICAL: Construct a realistic biographical bridge between [Childhood] and [Adulthood]. Treat these not as separate tags, but as points on a continuous timeline.

# PROHIBITIONS:
1. NO DATA DUMPING: NEVER mention specific numbers (e.g., ""Shooting 10""), skill levels, or raw gene/trait names.
2. TRANSLATE: Convert stats to narrative.

# LOGIC RULES (The Connector)
1.  Internal Trajectory Analysis:
    -   Analyze the sociological and psychological evolution from the Childhood environment to the Adulthood role.
    -   Establish a realistic Turning Point that justifies this shift without relying on external sci-fi tropes unless explicitly present in the data.
2.  Psychological Residue:
    -   Determine how the Childhood background persists in the current personality.
    -   Identify specific habits, fears, or values formed in the early years that either support or conflict with the current Adulthood profession.
3.  Data as Evidence:
    -   Treat every Skill level and Trait as physical evidence of past experiences.
    -   Justify high skills as the result of survival necessity or intense training, and low skills as the result of environmental absence or avoidance.
4.  Standard Archetype Protocol:
    -   If [Backstory] or [Traits] are absent: Apply the Default Factory Settings for their Race or Age.

# OUTPUT STRATEGY
Generate ONE single, cohesive psychological profile.Focus entirely on Causality—explaining the result based strictly on the cause.
1. Absolute Certainty: Use definitive language. No ""might be"" or ""likely"".
2. Hidden Logic: NEVER use the words ""Turning Point"", ""Transition"", or ""Trajectory"".

# Content Template
[2-4 word Style]
[Describe the logical trajectory of their life based on the data. Explain the turning point that led to their current role. Define a short-term psychological goal consistent with their traits. Explicitly describe their speaking tempo, vocabulary, and attitude as a result of their lived experience. Keep it all in one solid paragraph.]";

        // 4. 演变/更新模式 (专用)
        public const string DefaultPrompt_Evolve = @"# Role: Rimworld Character Development Analyst
# Language: {LANG}

# Task:
Analyze the provided data and write a short development addendum. Focus on shifts in the character's mindset, speaking style, and behavioral tendencies.

# DATA HIERARCHY:
1. CORE: [Previous Persona], [Time Context] (Determines the scale and nature of growth).
2. CONTEXT: [Director's Notes], [New Memories], [Status Changes] (Provides specific triggers for change).

# CRITICAL RULES:
1. No Repetition: Never repeat phrases or words from the [Previous Persona].
2. Cause & Effect: Start with a concise summary of recent experiences/hardships, then describe the resulting shift in mindset and dialogue style.
3. Synthesis Only: Do not list events. Translate memories and skill changes into character traits.
4. Dialogue-Focused: Focus on how they now speak or think.
5. Age Logic: If [Time Context] shows significant aging, prioritize maturity and worldview shifts; if short, focus on immediate emotional reactions and fixations.
6. Length Limit: Strictly 1-2 sentences. Maximum 50 words.";

        // =============================================================
        // Technical Protocols (Hidden)
        // =============================================================

        public const string HiddenTechnicalPrompt_Single = @"
# SYSTEM PROTOCOL (JSON FORMAT ENFORCEMENT):
You must return a valid JSON object. DO NOT use Markdown code blocks.
The 'persona' field must be a SINGLE LINE string using \n for breaks.
Fields:
1. ""persona"":  The full text of the result (use \n for formatting).
2. ""chattiness"": Float (0.1 - 1.0).
";

        public const string HiddenTechnicalPrompt_Batch = @"
# SYSTEM PROTOCOL (BATCH JSON FORMAT ENFORCEMENT):
You are processing MULTIPLE characters.
The final output MUST be a valid JSON object with a SINGLE 'persona' field.
Inside the 'persona' string, list each character's personality.

CRITICAL FORMAT RULES:
1. Each character block MUST start with their ID exactly as provided in the input, e.g., '[ID:Human123]'.
2. Separate characters with '---'.
3. Use \n for line breaks.

Example for 'persona' field:
""[ID:Human101]: Description for John...\n---\n[ID:Human102]: Description for Jane...""

Fields:
1. ""persona"": The combined text for ALL characters.
2. ""chattiness"": Float (just use 0.5 as a default).
";

        // =============================================================
        // Settings Fields
        // =============================================================

        // 旧数据 (保留用于迁移)
        public string activePrompt = "";

        // 新数据：预设列表
        public List<PromptPreset> presets;
        public int selectedPresetIndex = 0;

        public bool EnableDebugLog = false;
        public string directorNotes = "";
        public bool ShowMainButton = true;
        public bool enableEvolveFeature = true;
        public ContextSettings Context = new ContextSettings();
        public Dictionary<string, bool> BatchFilters;

        //  新增字段：预设库和规则库 
        public List<CustomPreset> userPresets;
        public List<AssignmentRule> assignmentRules;
        //  新增：迁移标记 (默认为 false) 
        private bool _chattinessMigratedV2 = false;
        public override void ExposeData()
        {
            // 读取旧数据
            Scribe_Values.Look(ref activePrompt, "activePrompt", "", true);

            // 读取新数据
            Scribe_Values.Look(ref selectedPresetIndex, "selectedPresetIndex", 0);
            Scribe_Collections.Look(ref presets, "presets", LookMode.Deep);

            // 其他设置
            Scribe_Values.Look(ref EnableDebugLog, "EnableDebugLog", false);
            Scribe_Values.Look(ref directorNotes, "directorNotes", "");
            Scribe_Values.Look(ref ShowMainButton, "ShowMainButton", true);
            Scribe_Values.Look(ref enableEvolveFeature, "enableEvolveFeature", true);

            Scribe_Deep.Look(ref Context, "Context");
            if (Context == null) Context = new ContextSettings();

            Scribe_Collections.Look(ref BatchFilters, "BatchFilters", LookMode.Value, LookMode.Value);

            Scribe_Collections.Look(ref userPresets, "userPresets", LookMode.Deep);
            Scribe_Collections.Look(ref assignmentRules, "assignmentRules", LookMode.Deep);

            Scribe_Values.Look(ref _chattinessMigratedV2, "chattinessMigratedV2", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitPresets(); // Prompt 1-4 初始化 (这个是安全的，因为只涉及我们自己的类)
                InitFilters(); // 过滤器初始化 (安全)

                // ★★★ 核心修复：不要在这里调用 InitLibrary() ★★★
                // 我们只确保 List 对象不为 null，防止 UI 报错
                if (userPresets == null) userPresets = new List<CustomPreset>();
                if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();

                // ★★★ 也不要在这里做任何迁移逻辑 ★★★
                // 统统移到后面去
            }

            base.ExposeData();
        }

        public void MigrateChattinessValuesIfNeeded()
        {
            if (_chattinessMigratedV2) return;

            if (userPresets == null) return;

            int count = 0;
            foreach (var preset in userPresets)
            {
                // 旧版逻辑是 0-2.0，新版是 0-1.0
                // 直接除以 2，进行无损压缩
                if (preset.chattiness > 0)
                {
                    preset.chattiness = Mathf.Clamp(preset.chattiness / 2.0f, 0.1f, 1.0f);
                    count++;
                }
            }
            _chattinessMigratedV2 = true;
            Log.Message($"[Persona Director] Migrated {count} user presets to new chattiness scale (v2).");
        }

        public void InitLibrary()
        {
            if (userPresets == null) userPresets = new List<CustomPreset>();
            if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();

            // 1. 填充预设库
            if (userPresets.Count == 0)
            {
                // A. 导入我们的内置库 (智能提取标题)
                foreach (var def in PresetLibrary.Defaults)
                {
                    // def.personaText 已经是翻译过的文本了 (在 PresetLibrary 里调用的 Translate)
                    // 我们直接从里面提取标题，而不是用 def.label (那是英文硬编码的)
                    string smartLabel = ExtractLabelFromText(def.personaText);

                    // 如果提取失败(比如没有横杠)，就用原来的英文 Label 做保底
                    if (string.IsNullOrEmpty(smartLabel)) smartLabel = def.label;

                    userPresets.Add(new CustomPreset
                    {
                        label = smartLabel,
                        personaText = def.personaText,
                        chattiness = def.chattiness,
                        category = "Built-in" 
                    });
                }

                // B. 导入 RimTalk 原版库 (智能提取标题)
                try
                {
                    // 获取 Constant.Personalities 属性 (Property)
                    // 最新版 RimTalk 中，这是一个 Property，不再是 Field
                    var propInfo = AccessTools.Property(typeof(Constant), "Personalities");
                    if (propInfo != null)
                    {
                        var vanillaList = propInfo.GetValue(null) as IEnumerable<PersonalityData>;
                        if (vanillaList != null)
                        {
                            int count = 0;
                            foreach (var p in vanillaList)
                            {
                                if (!userPresets.Any(existing => existing.personaText == p.Persona))
                                {
                                    // 同样使用智能提取
                                    string smartLabel = ExtractLabelFromText(p.Persona);
                                    if (string.IsNullOrEmpty(smartLabel)) smartLabel = $"Vanilla {++count}";

                                    userPresets.Add(new CustomPreset
                                    {
                                        label = smartLabel,
                                        personaText = p.Persona,
                                        chattiness = p.Chattiness,
                                        category = "Vanilla"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Persona Director] Failed to read vanilla presets: {ex.Message}");
                }
                _chattinessMigratedV2 = true;
            }

            // 2. 填充规则库
            if (assignmentRules.Count == 0)
            {
                AddDefaultRules();
            }
            PresetSynchronizer.SyncToRimTalk();
        }

        // ★★★ 辅助方法：智能提取标题 ★★★
        private string ExtractLabelFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // ★★★ 支持更多分隔符: –, —, :, ： ★★★
            string[] separators = new[] { " - ", " – ", " — ", "：", ": " };

            foreach (var sep in separators)
            {
                int index = text.IndexOf(sep);
                if (index > 0 && index < 30) // 限制标题长度
                {
                    return text.Substring(0, index).Trim();
                }
            }
            return null;
        }

        private void AddDefaultRules()
        {
            // --- 规则 1: 海盗 (Pirate) ---
            // 对应预设：废土狂徒, 反社会, 强盗, 混乱邪恶类
            var pirateRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "Pirate", // 原版海盗
                priority = 10
            };
            // 查找合适的预设并加入池子
            AddIdsToRule(pirateRule, "Apocalypse", "Sociopath", "Machiavellian", "Narcissist", "Troll");
            assignmentRules.Add(pirateRule);

            // --- 规则 2: 部落 (Tribe) ---
            // 对应预设：原始本能, 萨满(Monk/Daoist代替), 猎人
            var tribeRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "TribeRough", // 狂暴部落
                priority = 10
            };
            AddIdsToRule(tribeRule, "Primal", "Monk", "Daoist", "Weary Survivor");
            assignmentRules.Add(tribeRule);

            // --- 规则 3: 帝国 (Empire - DLC) ---
            // 对应预设：贵族, 骑士, 官僚
            if (ModsConfig.RoyaltyActive)
            {
                var empireRule = new AssignmentRule
                {
                    enabled = false,
                    type = RuleType.FactionDef,
                    targetDefName = "Empire",
                    priority = 20
                };
                AddIdsToRule(empireRule, "Young Master", "Bureaucrat", "Noble", "Paladin", "Butler");
                assignmentRules.Add(empireRule);
            }

            // --- 规则 4: 污秽人 (Waster - DLC) ---
            // 对应预设：废土风
            if (ModsConfig.BiotechActive)
            {
                var wasterRule = new AssignmentRule
                {
                    enabled = false,
                    type = RuleType.XenotypeDef,
                    targetDefName = "Waster",
                    priority = 50 // 种族优先级高于派系
                };
                AddIdsToRule(wasterRule, "Apocalypse", "Doomer", "Grindset"); 
                assignmentRules.Add(wasterRule);
            }          
        }

        private void AddIdsToRule(AssignmentRule rule, params string[] searchLabels)
        {
            foreach (var label in searchLabels)
            {
                // 模糊匹配预设名称
                var preset = userPresets.FirstOrDefault(p => p.label.Contains(label));
                if (preset != null && !rule.allowedPresetIds.Contains(preset.id))
                {
                    rule.allowedPresetIds.Add(preset.id);
                }
            }
        }
        public void InitPresets()
        {
            if (presets == null) presets = new List<PromptPreset>();

            // 1. 确保至少有3个槽位
            while (presets.Count < 4)
            {
                presets.Add(new PromptPreset("", ""));
            }

            // 2. 数据迁移：如果旧 activePrompt 存在且不是默认值，迁移到 Slot 1
            if (!string.IsNullOrEmpty(activePrompt) && activePrompt != DefaultPrompt_Standard)
            {
                // 只有当 Slot 1 还没被初始化或被修改时才覆盖
                if (string.IsNullOrEmpty(presets[0].text) || presets[0].text == DefaultPrompt_Standard)
                {
                    presets[0].text = activePrompt;
                    presets[0].label = "Custom (Migrated)";
                }
                activePrompt = ""; // 清除旧数据标记完成
            }

            // 3. 填充默认值 (如果槽位为空)
            if (string.IsNullOrEmpty(presets[0].text))
            {
                presets[0].label = "Standard (3 Options)";
                presets[0].text = DefaultPrompt_Standard;
            }

            if (string.IsNullOrEmpty(presets[1].text))
            {
                presets[1].label = "Story-Driven";
                presets[1].text = DefaultPrompt_Simple;
            }

            if (string.IsNullOrEmpty(presets[2].text))
            {
                presets[2].label = "Data-Driven";
                presets[2].text = DefaultPrompt_Strict;
            }
            if (string.IsNullOrEmpty(presets[3].text))
            {
                presets[3].label = "Evolution (Update Only)";
                presets[3].text = DefaultPrompt_Evolve;
            }
        }

        // 获取当前激活的 Prompt 内容
        public string GetActivePrompt(bool isEvolveMode = false)
        {
            if (presets == null || presets.Count == 0) InitPresets();

            int indexToUse = selectedPresetIndex;

            // 如果不是 Evolve 调用，但用户不小心选中了 Evolve 专用槽位 (索引3)，
            // 那么强制使用标准槽位 (索引0) 来防止错误。
            if (!isEvolveMode && selectedPresetIndex == 3)
            {
                indexToUse = 0;
            }

            int safeIndex = Mathf.Clamp(indexToUse, 0, presets.Count - 1);
            return presets[safeIndex].text;
        }

        public void InitFilters()
        {
            if (BatchFilters == null) BatchFilters = new Dictionary<string, bool>();
            EnsureKey("Colonists", true);
            EnsureKey("Prisoners", true);
            EnsureKey("Slaves", true);
            EnsureKey("Visitors", false);
            EnsureKey("Enemies", false);
            EnsureKey("Animals", false);
            EnsureKey("Mechs", false);
            EnsureKey("Anomalies", false);
        }

        private void EnsureKey(string key, bool defaultValue)
        {
            if (!BatchFilters.ContainsKey(key)) BatchFilters[key] = defaultValue;
        }
    }

    public class ContextSettings : IExposable
    {
        public bool Inc_Basic = true;
        public bool Inc_Race = true; public bool Inc_Race_Desc = false;
        public bool Inc_Genes = true; public bool Inc_Genes_Desc = false;
        public bool Inc_Backstory = true; public bool Inc_Backstory_Desc = true;
        public bool Inc_Relations = true;
        public bool Inc_DirectorNotes = true;

        public bool Inc_Traits = true; public bool Inc_Traits_Desc = true;
        public bool Inc_Ideology = false; public bool Inc_Ideology_Desc = false;
        public bool Inc_Skills = true; public bool Inc_Skills_Desc = true;
        public bool Inc_Health = false; public bool Inc_Health_Desc = false;
        public bool Inc_Equipment = false;
        public bool Inc_Inventory = false;
        public bool Inc_RimPsyche = false; public bool Inc_RimPsyche_All = false;
        public bool Inc_Memories = false; 
        public bool Inc_CommonKnowledge = false;
        public bool Inc_DataComparison = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Inc_Basic, "Inc_Basic", true);
            Scribe_Values.Look(ref Inc_Race, "Inc_Race", true);
            Scribe_Values.Look(ref Inc_Race_Desc, "Inc_Race_Desc", false);
            Scribe_Values.Look(ref Inc_Genes, "Inc_Genes", true);
            Scribe_Values.Look(ref Inc_Genes_Desc, "Inc_Genes_Desc", false);
            Scribe_Values.Look(ref Inc_Backstory, "Inc_Backstory", true);
            Scribe_Values.Look(ref Inc_Backstory_Desc, "Inc_Backstory_Desc", true);
            Scribe_Values.Look(ref Inc_Relations, "Inc_Relations", true);
            Scribe_Values.Look(ref Inc_DirectorNotes, "Inc_DirectorNotes", true);
            Scribe_Values.Look(ref Inc_Traits, "Inc_Traits", true);
            Scribe_Values.Look(ref Inc_Traits_Desc, "Inc_Traits_Desc", true);
            Scribe_Values.Look(ref Inc_Ideology, "Inc_Ideology", false);
            Scribe_Values.Look(ref Inc_Ideology_Desc, "Inc_Ideology_Desc", false);
            Scribe_Values.Look(ref Inc_Skills, "Inc_Skills", true);
            Scribe_Values.Look(ref Inc_Skills_Desc, "Inc_Skills_Desc", true);
            Scribe_Values.Look(ref Inc_Health, "Inc_Health", false);
            Scribe_Values.Look(ref Inc_Health_Desc, "Inc_Health_Desc", false);
            Scribe_Values.Look(ref Inc_Equipment, "Inc_Equipment", false);
            Scribe_Values.Look(ref Inc_Inventory, "Inc_Inventory", false);
            Scribe_Values.Look(ref Inc_RimPsyche, "Inc_RimPsyche", false);
            Scribe_Values.Look(ref Inc_RimPsyche_All, "Inc_RimPsyche_All", false);
            Scribe_Values.Look(ref Inc_Memories, "Inc_Memories", false);
            Scribe_Values.Look(ref Inc_CommonKnowledge, "Inc_CommonKnowledge", false);
            Scribe_Values.Look(ref Inc_DataComparison, "Inc_DataComparison", false);
        }
    }

}