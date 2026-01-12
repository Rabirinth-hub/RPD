using System.Collections.Generic;
using Verse;

namespace RimPersonaDirector
{
    // 静态库：只用于提供模板，不保存数据
    [StaticConstructorOnStartup]
    public static class PresetLibrary
    {
        private static List<CustomPreset> _defaults;

        public static List<CustomPreset> Defaults
        {
            get
            {
                if (_defaults == null) Initialize();
                return _defaults;
            }
        }

        private static void Initialize()
        {
            _defaults = new List<CustomPreset>();

            // === 基础风格 ===
            Add("Observer", "RPD_Preset_Observer", 0.4f);
            Add("Pragmatist", "RPD_Preset_Pragmatist", 0.5f);
            Add("Weary Survivor", "RPD_Preset_Weary", 0.3f);

            // === 心理学 / 人格障碍 ===
            Add("Narcissist", "RPD_Preset_Narcissist", 0.7f);
            Add("Machiavellian", "RPD_Preset_Machiavellian", 0.4f);
            Add("Sociopath", "RPD_Preset_Sociopath", 0.6f);
            Add("Paranoid", "RPD_Preset_Paranoid", 0.5f);
            Add("Histrionic", "RPD_Preset_Histrionic", 0.9f);
            Add("Obsessive", "RPD_Preset_Obsessive", 0.4f);

            // === 创伤 / 精神状态 ===
            Add("Shell-Shocked", "RPD_Preset_ShellShocked", 0.1f);
            Add("Dissociated", "RPD_Preset_Dissociated", 0.2f);
            Add("Manic", "RPD_Preset_Manic", 1.0f);
            Add("Depressive", "RPD_Preset_Depressive", 0.1f);
            Add("Hyper-Vigilant", "RPD_Preset_HyperVigilant", 0.3f);
            Add("Fatalist", "RPD_Preset_Fatalist", 0.2f);
            Add("Nihilist", "RPD_Preset_Nihilist", 0.3f);

            // === 认知风格 ===
            Add("Literal", "RPD_Preset_Literal", 0.3f);
            Add("Over-Thinker", "RPD_Preset_OverThinking", 0.5f);
            Add("Socially Awkward", "RPD_Preset_SocialAwkward", 0.2f);
            Add("Cryptic", "RPD_Preset_Cryptic", 0.3f);
            Add("Mute", "RPD_Preset_MuteByChoice", 0.1f);
            Add("Gaslighter", "RPD_Preset_Gaslighter", 0.5f);

            // === MBTI 原型 ===
            Add("ENTJ Commander", "RPD_Preset_Commander_ENTJ", 0.7f);
            Add("ENTP Debater", "RPD_Preset_Debater_ENTP", 0.7f);
            Add("INFP Mediator", "RPD_Preset_Mediator_INFP", 0.3f);
            Add("INTP Logician", "RPD_Preset_Logician_INTP", 0.2f);

            // === ACG / 二次元 ===
            Add("Tsundere", "RPD_Preset_Tsundere", 0.2f);
            Add("Kuudere", "RPD_Preset_Kuudere", 0.1f);
            Add("Chuunibyou", "RPD_Preset_Chuunibyou", 0.6f);
            Add("Genki", "RPD_Preset_Genki", 0.7f);
            Add("Yandere", "RPD_Preset_Yandere", 0.4f);
            Add("Onee-San", "RPD_Preset_OneeSan", 0.5f);
            Add("Gyaru", "RPD_Preset_Gyaru", 0.6f);
            Add("Butler/Maid", "RPD_Preset_Butler", 0.4f);

            // === 中式仙侠 ===
            Add("Daoist", "RPD_Preset_Daoist", 0.3f);
            Add("Jianghu Hero", "RPD_Preset_Jianghu", 0.5f);
            Add("Young Master", "RPD_Preset_YoungMaster", 0.6f);
            Add("Scholar", "RPD_Preset_ScholarOfficial", 0.5f);
            Add("Monk", "RPD_Preset_Monk", 0.2f);

            // === 现代网络文化 ===
            Add("Influencer", "RPD_Preset_Influencer", 0.9f);
            Add("Hustler/Grindset", "RPD_Preset_Grindset", 0.6f);
            Add("Doomer", "RPD_Preset_Doomer", 0.2f);
            Add("Keyboard Warrior", "RPD_Preset_KeyboardWarrior", 0.7f);
            Add("Emoji User", "RPD_Preset_EmojiUser", 0.5f);
            Add("Tech Bro", "RPD_Preset_TechBro", 0.6f);
            Add("Shipper", "RPD_Preset_Shipper", 0.7f);
            Add("Mad Lit", "RPD_Preset_MadLiterature", 0.8f);
            Add("Reviewer", "RPD_Preset_Reviewer", 0.5f);
            Add("Gamer", "RPD_Preset_Gamer", 0.5f);

            // === 奇幻/科幻/异质 ===
            Add("Paladin", "RPD_Preset_Paladin", 0.5f);
            Add("Rogue", "RPD_Preset_Rogue", 0.3f);
            Add("Bard", "RPD_Preset_Bard", 0.7f);
            Add("Warlock", "RPD_Preset_Warlock", 0.3f);
            Add("AI Log", "RPD_Preset_AI_Log", 0.2f);
            Add("Corpo", "RPD_Preset_Corpo", 0.5f);
            Add("Hivemind", "RPD_Preset_Hivemind", 0.3f);
            Add("Primal", "RPD_Preset_Primal", 0.2f);
            Add("Glitch", "RPD_Preset_Glitch", 0.2f);
            Add("Prophet", "RPD_Preset_Prophet", 0.4f);
        }

        private static void Add(string label, string key, float chat)
        {
            _defaults.Add(new CustomPreset
            {
                label = label,
                personaText = key.Translate(), // 这一步直接读取翻译文件的内容
                chattiness = chat
            });
        }
    }
}