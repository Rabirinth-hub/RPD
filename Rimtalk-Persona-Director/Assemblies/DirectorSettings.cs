using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class DirectorSettings : ModSettings
    {
        // 用户可见/可编辑的创意指令
        public const string DefaultUserPrompt = @"# Role: Rimworld Persona Director
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
        // 系统隐藏的、不可编辑的底层格式协议
        public const string HiddenTechnicalPrompt = @"
# SYSTEM PROTOCOL (JSON FORMAT ENFORCEMENT):
You must return a valid JSON object.
DO NOT use Markdown code blocks (no ```json).

CRITICAL FORMATTING RULE:
The 'persona' field must be a SINGLE LINE string.
- You MUST escape all line breaks as \n.
- REAL NEWLINES ARE FORBIDDEN inside the JSON string.

Example Valid JSON:
{
  ""persona"": ""Here is the generated personality description.\nIt can have multiple lines.\nBut it must be escaped."",
  ""chattiness"": 1.0
}

Fields:
1. ""persona"": The full text of the result (use \n for formatting).
2. ""chattiness"": Float (0.1 - 2.0).
";

        public string activePrompt = DefaultUserPrompt;
        public bool EnableDebugLog = true;
        public string directorNotes = ""; // 持久化导演备注
        public ContextSettings Context = new ContextSettings();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref activePrompt, "activePrompt", DefaultUserPrompt, true);
            Scribe_Values.Look(ref EnableDebugLog, "EnableDebugLog", true);
            Scribe_Values.Look(ref directorNotes, "directorNotes", "");
            Scribe_Deep.Look(ref Context, "Context");
            if (Context == null) Context = new ContextSettings();
            base.ExposeData();
        }
    }

    public class ContextSettings : IExposable
    {
        // 左栏 (6项)
        public bool Inc_Basic = true;
        public bool Inc_Race = true; public bool Inc_Race_Desc = false;
        public bool Inc_Genes = true; public bool Inc_Genes_Desc = false;
        public bool Inc_Backstory = true; public bool Inc_Backstory_Desc = true;
        public bool Inc_Relations = true;
        public bool Inc_DirectorNotes = true;

        // 右栏 (4项)
        public bool Inc_Traits = true; public bool Inc_Traits_Desc = true;
        public bool Inc_Ideology = false; public bool Inc_Ideology_Desc = false;
        public bool Inc_Skills = true; public bool Inc_Skills_Desc = true;
        public bool Inc_Health = false; public bool Inc_Health_Desc = false;

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
        }
    }
}