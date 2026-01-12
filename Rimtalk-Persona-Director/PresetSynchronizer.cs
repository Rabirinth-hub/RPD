using Verse;
using RimTalk.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System;

namespace RimPersonaDirector
{
    // 不再需要 StaticConstructorOnStartup，因为我们会手动调用它
    public static class PresetSynchronizer
    {
        public static void SyncToRimTalk()
        {
            var settings = DirectorMod.Settings;
            if (settings == null || settings.userPresets == null) return;

            try
            {
                // 1. 获取 RimTalk 的字段
                FieldInfo personalitiesField = AccessTools.Field(typeof(Constant), "Personalities");
                if (personalitiesField == null) return;

                // 2. 将我们的 userPresets 转换为 RimTalk 需要的 PersonalityData 列表
                List<PersonalityData> syncList = new List<PersonalityData>();

                foreach (var preset in settings.userPresets)
                {
                    // Ensure chattiness is normalized to the expected 0..1 range before syncing
                    float chat = preset.chattiness;
                    try
                    {
                        chat = DirectorUtils.NormalizeChattiness(chat);
                    }
                    catch { }

                    syncList.Add(new PersonalityData(preset.personaText, chat));
                }

                // 3. ★★★ 暴力覆盖 ★★★

                object currentValue = personalitiesField.GetValue(null);
                object newValue = null;

                if (currentValue != null && currentValue.GetType().IsArray)
                {
                    // 目标是数组
                    newValue = syncList.ToArray();
                }
                else
                {
                    // 目标是 List (或者 null，默认为 List)
                    newValue = syncList;
                }

                // 4. 执行替换
                personalitiesField.SetValue(null, newValue);

                // Log.Message($"[Persona Director] Synced {syncList.Count} presets to RimTalk random pool.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Persona Director] Sync failed: {ex.Message}");
            }
        }
    }
}