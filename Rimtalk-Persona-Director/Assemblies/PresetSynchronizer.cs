using Verse;
using RimTalk.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System;
using UnityEngine;

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
                // 1. ★★★ 核心修复：寻找写入目标 ★★★
                // 优先级 A: 新版 RimTalk 的私有幕后字段 _personalities
                FieldInfo targetField = AccessTools.Field(typeof(Constant), "_personalities");

                // 优先级 B: 旧版 RimTalk 的公有字段 Personalities
                if (targetField == null)
                {
                    targetField = AccessTools.Field(typeof(Constant), "Personalities");
                }

                if (targetField == null)
                {
                    // 如果都找不到，说明 RimTalk 结构彻底变了，放弃同步
                    return;
                }

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
                
                if (syncList.Count == 0) return;

                // 3. ★★★ 类型适配与覆盖 ★★★
                object newValue = null;

                // 检查目标字段是 数组 还是 List
                if (targetField.FieldType.IsArray)
                {
                    // 如果是数组 (PersonalityData[])，转为数组
                    newValue = syncList.ToArray();
                }
                else
                {
                    // 如果是 List (List<PersonalityData>)，直接用 List
                    newValue = syncList;
                }

                // 4. 执行写入
                // 静态字段，实例传 null
                targetField.SetValue(null, newValue);

                // Log.Message($"[Persona Director] Synced {syncList.Count} presets to RimTalk.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Persona Director] Sync failed: {ex.Message}");
            }
        }
    }
}