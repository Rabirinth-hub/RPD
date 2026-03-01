using Verse;
using System;
using System.Collections.Generic;
using RimTalk.Data; // 引用以访问 Constant

namespace RimPersonaDirector
{
    public static class DirectorStartup
    {
        public static void Initialize()
        {
            try
            {
                var settings = DirectorMod.Settings;
                if (settings == null) return;

                // 防空
                if (settings.userPresets == null) settings.userPresets = new List<CustomPreset>();
                if (settings.assignmentRules == null) settings.assignmentRules = new List<AssignmentRule>();

                // ★★★ 核心修复 C：先备份真·原版数据 ★★★
                // 在我们做任何同步/覆盖之前，先看看 Constant.Personalities 里有什么
                // 此时游戏刚加载完，Constant 里肯定是干净的原版数据
                if (DirectorSettings.OriginalVanillaCache == null && Constant.Personalities != null)
                {
                    if (Constant.Personalities is IEnumerable<PersonalityData> list)
                    {
                        DirectorSettings.OriginalVanillaCache = new List<PersonalityData>(list);
                        Log.Message($"[Persona Director] Cached {DirectorSettings.OriginalVanillaCache.Count} original vanilla presets.");
                    }
                }

                // 2. 执行新用户初始化
                if (!settings._libraryInitialized)
                {
                    if (settings.userPresets.Count == 0 && settings.assignmentRules.Count == 0)
                    {
                        Log.Message("[Persona Director] First time setup detected. Initializing library...");
                        settings.InitLibrary();
                    }

                    settings._libraryInitialized = true;
                    settings.Write();
                }

                // 3. ★★★ 最后再同步 ★★★
                // 现在可以用我们的数据去覆盖原版了，因为原版已经备份过了
                PresetSynchronizer.SyncToRimTalk();
                Log.Message("[Persona Director] Sync to RimTalk completed.");
            }
            catch (Exception ex)
            {
                Log.Error($" Initialization Failed: {ex}");
            }
        }
    }
}