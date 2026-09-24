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
        public string localizationKey;
        public string lastLocalizedText;
        public bool enabled = true;
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
            Scribe_Values.Look(ref localizationKey, "localizationKey");
            Scribe_Values.Look(ref lastLocalizedText, "lastLocalizedText");
            Scribe_Values.Look(ref enabled, "enabled", true);
            // ★★★ 核心修复：防空 ID 补丁 ★★★
            // 当数据从 XML 读取完毕后，检查 id 是否因为缺失标签而变成了 null
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (string.IsNullOrEmpty(id))
                {
                    id = System.Guid.NewGuid().ToString();
                }
            }
        }
    }

    public enum RuleType { FactionDef, RaceDef, XenotypeDef, Age }

    // 分配规则
    public class AssignmentRule : IExposable
    {
        public bool enabled = true;
        public string targetDefName; // 只存字符串，防崩坏
        public RuleType type;
        public int priority = 0;
        public List<string> allowedPresetIds = new List<string>();
        // 年龄区间（仅 Age 类型用）
        public int minAge = 0;
        public int maxAge = 99;

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref targetDefName, "targetDefName");
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref priority, "priority", 0);
            Scribe_Collections.Look(ref allowedPresetIds, "allowedPresetIds", LookMode.Value);
            Scribe_Values.Look(ref minAge, "minAge", 0);
            Scribe_Values.Look(ref maxAge, "maxAge", 99);
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

        // 5. 演变/全覆盖模式 (专用)
        public const string DefaultPrompt_Overwrite = @"# Role: Master Persona Writer
# Language: {LANG}
# Task:
REWRITE the entire persona. Integrate the new experiences and status changes into the core personality.

# DATA HIERARCHY:
1. CORE: [Previous Persona], [Time Context] (Determines the scale and nature of growth).
2. CONTEXT: [Director's Notes], [New Memories], [Status Changes] (Provides specific triggers for change).

# Rules:
1. Do not just append; weave the new traits organically into the text.
2. Follow the format and approximate word count of the original persona.
3. Output ONLY the new persona text.";

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

        public string rimTalkPreset_Single = ""; // 单体生成用的预设名
        public string rimTalkPreset_Evolve = ""; // 演变生成用的预设名

        //  新增字段：预设库和规则库 
        public List<CustomPreset> userPresets = new List<CustomPreset>();
        public List<AssignmentRule> assignmentRules = new List<AssignmentRule>();
        private string _libraryLanguage = "";
        // 初始化状态标记
        public bool _libraryInitialized = false;
        // 缓存 (不保存)
        public static List<PersonalityData> OriginalVanillaCache;
        //  新增：迁移标记 (默认为 false) 
        private bool _chattinessMigratedV2 = false;
        // ★★★ 新增：目标 RimTalk 预设名称 ★★★
        public string rimTalkPresetName = "Director";

        // 实验功能总门。新增功能必须显式启用，并在熔断时保持关闭。
        public bool experimentalFeaturesEnabled = false;
        public bool experimentalCircuitBroken = false;
        public string experimentalCircuitBreakReason = "";

        // AutoGen 配置仅保存用户选择；运行队列与任务绝不持久化。
        public bool autoGenEnabled = false;
        public Dictionary<string, AutoGenCategory> autoGenCategories;
        public string autoGenNotes = "";

        // 自动演变与历史属于实验层；0 条数表示仅受固定容量保险丝约束。
        public bool globalAutoEvolveEnabled = false;
        public bool autoEvolveDefaultEnabled = false;
        public bool autoEvolveOnMarriage = false;
        public bool autoEvolveOnBreakup = false;
        public bool autoEvolveOnBirth = false;
        public bool autoEvolveOnDirectFamilyDeath = false;
        public bool autoEvolveOnTraitAdded = false;
        public AutoEvolveMode autoMode = AutoEvolveMode.Append;
        public AutoEvolveNotify autoNotify = AutoEvolveNotify.Silent;
        public SpeedProtection speedProtection = SpeedProtection.Speed3X;
        public string autoEvolveNotes = "";
        public int personaHistoryMaxRecords = 5;
        // Per-installation release notice acknowledgement; not tied to a save.
        public string acknowledgedReleaseNotice = "";

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
            // 库数据           
            Scribe_Collections.Look(ref userPresets, "userPresets", LookMode.Deep);
            Scribe_Collections.Look(ref assignmentRules, "assignmentRules", LookMode.Deep);
            // 保存初始化标记 
            Scribe_Values.Look(ref _libraryInitialized, "libraryInitialized", false);
            Scribe_Values.Look(ref _libraryLanguage, "libraryLanguage", "");
            Scribe_Values.Look(ref _chattinessMigratedV2, "chattinessMigratedV2", false);

            Scribe_Values.Look(ref rimTalkPreset_Single, "rimTalkPreset_Single", "");
            Scribe_Values.Look(ref rimTalkPreset_Evolve, "rimTalkPreset_Evolve", "");

            Scribe_Values.Look(ref experimentalFeaturesEnabled, "experimentalFeaturesEnabled", false);
            Scribe_Values.Look(ref experimentalCircuitBroken, "experimentalCircuitBroken", false);
            Scribe_Values.Look(ref experimentalCircuitBreakReason, "experimentalCircuitBreakReason", "");

            Scribe_Values.Look(ref autoGenEnabled, "autoGenEnabled", false);
            Scribe_Collections.Look(ref autoGenCategories, "autoGenCategories", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref autoGenNotes, "autoGenNotes", "");

            Scribe_Values.Look(ref globalAutoEvolveEnabled, "globalAutoEvolveEnabled", false);
            Scribe_Values.Look(ref autoEvolveDefaultEnabled, "autoEvolveDefaultEnabled", false);
            Scribe_Values.Look(ref autoEvolveOnMarriage, "autoEvolveOnMarriage", false);
            Scribe_Values.Look(ref autoEvolveOnBreakup, "autoEvolveOnBreakup", false);
            Scribe_Values.Look(ref autoEvolveOnBirth, "autoEvolveOnBirth", false);
            Scribe_Values.Look(ref autoEvolveOnDirectFamilyDeath, "autoEvolveOnDirectFamilyDeath", false);
            Scribe_Values.Look(ref autoEvolveOnTraitAdded, "autoEvolveOnTraitAdded", false);
            Scribe_Values.Look(ref autoMode, "autoMode", AutoEvolveMode.Append);
            Scribe_Values.Look(ref autoNotify, "autoNotify", AutoEvolveNotify.Silent);
            Scribe_Values.Look(ref speedProtection, "speedProtection", SpeedProtection.Speed3X);
            Scribe_Values.Look(ref autoEvolveNotes, "autoEvolveNotes", "");
            Scribe_Values.Look(ref personaHistoryMaxRecords, "personaHistoryMaxRecords", 5);
            Scribe_Values.Look(ref acknowledgedReleaseNotice, "acknowledgedReleaseNotice", "");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitPresets(); // Prompt 1-5 初始化 (这个是安全的，因为只涉及我们自己的类)
                selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presets.Count - 1);
                InitFilters(); // 过滤器初始化 (安全)

                // ★★★ 核心修复：不要在这里调用 InitLibrary() ★★★
                // 我们只确保 List 对象不为 null，防止 UI 报错
                if (userPresets == null) userPresets = new List<CustomPreset>();
                if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();
                if (!_libraryInitialized && userPresets.Count > 0)
                {
                    _libraryInitialized = true;
                }

                if (experimentalCircuitBreakReason == null)
                {
                    experimentalCircuitBreakReason = "";
                }

                if (experimentalCircuitBroken)
                {
                    experimentalFeaturesEnabled = false;
                }

                if (autoGenNotes == null)
                {
                    autoGenNotes = "";
                }

                EnsureAutoGenCategories();

                if (autoEvolveNotes == null) autoEvolveNotes = "";
                if (personaHistoryMaxRecords < 0) personaHistoryMaxRecords = 5;
            }

            base.ExposeData();
        }

        public void EnsureAutoGenCategories()
        {
            if (autoGenCategories == null)
            {
                autoGenCategories = new Dictionary<string, AutoGenCategory>();
            }

            foreach (string categoryId in AutoGenCategoryCatalog.OrderedIds)
            {
                AutoGenCategory category;
                if (!autoGenCategories.TryGetValue(categoryId, out category) || category == null)
                {
                    autoGenCategories[categoryId] = AutoGenCategoryCatalog.CreateDefault(categoryId);
                    continue;
                }

                category.categoryName = categoryId;
                if (category.advancedPreset == null) category.advancedPreset = "";
                if (category.customContext == null) category.customContext = new ContextSettings();
            }
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

        private static string ActiveLanguageId()
        {
            try
            {
                return LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English";
            }
            catch
            {
                return "English";
            }
        }

        public bool RefreshLocalizedBuiltInsIfNeeded()
        {
            string currentLanguage = ActiveLanguageId();
            bool repairedVanillaLabels = RepairGeneratedVanillaLabels();
            if (string.Equals(_libraryLanguage, currentLanguage, StringComparison.Ordinal))
                return repairedVanillaLabels;

            bool changed = true; // Persist the new language marker even if no preset needed rewriting.
            List<CustomPreset> builtIns = userPresets.Where(p => p.category == "Built-in").ToList();
            bool canUseLegacyOrder = builtIns.Count == PresetLibrary.Defaults.Count;
            for (int builtInIndex = 0; builtInIndex < builtIns.Count; builtInIndex++)
            {
                CustomPreset preset = builtIns[builtInIndex];
                CustomPreset definition = null;
                if (!string.IsNullOrEmpty(preset.localizationKey))
                    definition = PresetLibrary.Defaults.FirstOrDefault(d => d.personaText == preset.localizationKey);
                if (definition == null)
                    definition = PresetLibrary.Defaults.FirstOrDefault(d => d.label == preset.label);
                if (definition == null && !string.IsNullOrEmpty(preset.personaText))
                    definition = PresetLibrary.Defaults.FirstOrDefault(d => d.personaText == preset.personaText);
                if (definition == null && canUseLegacyOrder)
                    definition = PresetLibrary.Defaults[builtInIndex];
                if (definition == null) continue;

                bool canReplaceText = string.IsNullOrEmpty(preset.lastLocalizedText)
                    || string.Equals(preset.personaText, preset.lastLocalizedText, StringComparison.Ordinal);
                preset.localizationKey = definition.personaText;
                if (!canReplaceText) continue;

                string translatedText = definition.personaText.Translate().Resolve();
                string translatedLabel = ExtractLabelFromText(translatedText) ?? definition.label;
                if (preset.personaText != translatedText || preset.label != translatedLabel)
                {
                    preset.personaText = translatedText;
                    preset.label = translatedLabel;
                    changed = true;
                }
                preset.lastLocalizedText = translatedText;
            }

            List<PersonalityData> vanillaDefinitions = OriginalVanillaCache;
            if (vanillaDefinitions != null)
            {
                foreach (CustomPreset preset in userPresets.Where(p => p.category == "Vanilla"))
                {
                    PersonalityData definition = null;
                    if (!string.IsNullOrEmpty(preset.localizationKey))
                        definition = vanillaDefinitions.FirstOrDefault(d => d.Persona == preset.localizationKey);

                    if (definition == null && preset.label != null && preset.label.StartsWith("Vanilla "))
                    {
                        int index;
                        if (int.TryParse(preset.label.Substring("Vanilla ".Length), out index)
                            && index > 0 && index <= vanillaDefinitions.Count)
                            definition = vanillaDefinitions[index - 1];
                    }
                    if (definition == null) continue;

                    bool canReplaceText = string.IsNullOrEmpty(preset.lastLocalizedText)
                        || string.Equals(preset.personaText, preset.lastLocalizedText, StringComparison.Ordinal);
                    preset.localizationKey = definition.Persona;
                    if (!canReplaceText) continue;

                    string translatedText = definition.Persona.Translate().Resolve();
                    string translatedLabel = ExtractLabelFromText(translatedText) ?? preset.label;
                    preset.personaText = translatedText;
                    preset.label = translatedLabel;
                    preset.lastLocalizedText = translatedText;
                }
            }

            _libraryLanguage = currentLanguage;
            return changed;
        }

        public void InitLibrary()
        {
            Log.Message("[Persona Director] -> InitLibrary: Starting...");
            if (userPresets == null) userPresets = new List<CustomPreset>();
            else userPresets.Clear();
            if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();
            else assignmentRules.Clear();
            Log.Message("[Persona Director] -> InitLibrary: Cleared existing lists. Loading built-in presets...");
            // 填充预设库
            // 1. 内置库
            int builtInCount = 0;
            foreach (var def in PresetLibrary.Defaults)
            {
                string translatedText = def.personaText.Translate().Resolve();
                string smartLabel = ExtractLabelFromText(translatedText) ?? def.label;

                // 如果提取失败(比如没有横杠)，就用原来的英文 Label 做保底
                if (string.IsNullOrEmpty(smartLabel)) smartLabel = def.label;

                userPresets.Add(new CustomPreset
                {
                    label = smartLabel,
                    personaText = translatedText,
                    chattiness = def.chattiness,
                    category = "Built-in",
                    localizationKey = def.personaText,
                    lastLocalizedText = translatedText,
                });
                builtInCount++;
            }
            Log.Message($"[Persona Director] -> InitLibrary: Loaded {builtInCount} built-in presets. Loading vanilla presets...");
            // 2. 填充原版
            int vanillaCount = 0;
            IEnumerable<RimTalk.Data.PersonalityData> sourceList = null;

            if (OriginalVanillaCache != null)
            {
                sourceList = OriginalVanillaCache;
            }
            else if (RimTalk.Data.Constant.Personalities != null)
            {
                var currentList = RimTalk.Data.Constant.Personalities as IEnumerable<RimTalk.Data.PersonalityData>;
                if (currentList != null)
                {
                    OriginalVanillaCache = currentList.ToList();
                    sourceList = OriginalVanillaCache;
                }
            }

            if (sourceList != null)
            {
                foreach (var p in sourceList)
                {
                    // 保护翻译和提取过程
                    string translatedText = p.Persona;
                    try { translatedText = p.Persona.Translate().Resolve(); } catch { }
                    bool isBuiltIn = PresetLibrary.Defaults.Any(d => d.personaText == translatedText);
                    if (!isBuiltIn && !userPresets.Any(existing => existing.personaText == translatedText))
                    {
                        string smartLabel = CreateVanillaLabel(translatedText, vanillaCount + 1);

                        userPresets.Add(new CustomPreset
                        {
                            label = smartLabel,
                            personaText = translatedText,
                            chattiness = p.Chattiness,
                            category = "Vanilla",
                            localizationKey = p.Persona,
                            lastLocalizedText = translatedText,
                        });
                        vanillaCount++;
                    }
                }
            }
            Log.Message($"[Persona Director] -> InitLibrary: Loaded {vanillaCount} vanilla presets. Loading default rules...");
            _chattinessMigratedV2 = true;

            // 3. 填充规则库
            AddDefaultRules();
            _libraryLanguage = ActiveLanguageId();
            Log.Message("[Persona Director] -> InitLibrary: Default rules loaded. Syncing to RimTalk...");
            PresetSynchronizer.SyncToRimTalk();
            Log.Message("[Persona Director] -> InitLibrary: Sync complete.");
            Log.Message("[Persona Director] Library reset/initialized to defaults.");
        
        }

        // ★★★ 辅助方法：智能提取标题 ★★★
        public string ExtractLabelFromText(string text)
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

        public string CreateVanillaLabel(string text, int fallbackIndex)
        {
            string extracted = ExtractLabelFromText(text);
            if (!string.IsNullOrWhiteSpace(extracted)) return extracted;

            string trimmed = text?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                int sentenceEnd = trimmed.IndexOfAny(new[] { '.', '。', '!', '！', '?', '？', ';', '；', '\r', '\n' });
                string summary = sentenceEnd >= 0 ? trimmed.Substring(0, sentenceEnd + 1) : trimmed;
                const int maxLabelLength = 42;
                if (summary.Length > maxLabelLength)
                    summary = summary.Substring(0, maxLabelLength).TrimEnd() + "…";
                if (!string.IsNullOrWhiteSpace(summary)) return summary;
            }

            return $"Vanilla {fallbackIndex}";
        }

        private bool RepairGeneratedVanillaLabels()
        {
            bool changed = false;
            int index = 0;
            foreach (CustomPreset preset in userPresets.Where(p => p.category == "Vanilla"))
            {
                index++;
                string label = preset.label ?? "";
                bool generatedLabel = label == "Vanilla Preset"
                    || (label.StartsWith("Vanilla ", StringComparison.Ordinal)
                        && int.TryParse(label.Substring("Vanilla ".Length), out _));
                if (!generatedLabel) continue;

                string repairedLabel = CreateVanillaLabel(preset.personaText, index);
                if (string.Equals(label, repairedLabel, StringComparison.Ordinal)) continue;
                preset.label = repairedLabel;
                changed = true;
            }
            return changed;
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

            // 1. 确保五个内置槽位存在；旧存档会只补缺失的第五槽。
            while (presets.Count < 5)
            {
                presets.Add(new PromptPreset("", ""));
            }

            for (int i = 0; i < 5; i++)
            {
                if (presets[i] == null) presets[i] = new PromptPreset("", "");
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
            if (string.IsNullOrEmpty(presets[4].text))
            {
                presets[4].label = "Evolution (Overwrite)";
                presets[4].text = DefaultPrompt_Overwrite;
            }
        }

        // 获取当前激活的 Prompt 内容
        public string GetActivePrompt(bool isEvolveMode = false)
        {
            if (presets == null || presets.Count == 0) InitPresets();

            int indexToUse = selectedPresetIndex;

            // 如果不是 Evolve 调用，但用户不小心选中了 Evolve 专用槽位，
            // 那么强制使用标准槽位 (索引0) 来防止错误。
            if (!isEvolveMode && (selectedPresetIndex == 3 || selectedPresetIndex == 4))
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
            EnsureKey("Other", false);
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

        public ContextSettings Copy()
        {
            return new ContextSettings
            {
                Inc_Basic = Inc_Basic,
                Inc_Race = Inc_Race,
                Inc_Race_Desc = Inc_Race_Desc,
                Inc_Genes = Inc_Genes,
                Inc_Genes_Desc = Inc_Genes_Desc,
                Inc_Backstory = Inc_Backstory,
                Inc_Backstory_Desc = Inc_Backstory_Desc,
                Inc_Relations = Inc_Relations,
                Inc_DirectorNotes = Inc_DirectorNotes,
                Inc_Traits = Inc_Traits,
                Inc_Traits_Desc = Inc_Traits_Desc,
                Inc_Ideology = Inc_Ideology,
                Inc_Ideology_Desc = Inc_Ideology_Desc,
                Inc_Skills = Inc_Skills,
                Inc_Skills_Desc = Inc_Skills_Desc,
                Inc_Health = Inc_Health,
                Inc_Health_Desc = Inc_Health_Desc,
                Inc_Equipment = Inc_Equipment,
                Inc_Inventory = Inc_Inventory,
                Inc_RimPsyche = Inc_RimPsyche,
                Inc_RimPsyche_All = Inc_RimPsyche_All,
                Inc_Memories = Inc_Memories,
                Inc_CommonKnowledge = Inc_CommonKnowledge,
                Inc_DataComparison = Inc_DataComparison
            };
        }

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
