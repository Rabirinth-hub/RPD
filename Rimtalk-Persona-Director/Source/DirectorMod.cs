using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq; // 确保引用 Linq
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class DirectorMod : Mod
    {
        public static DirectorSettings Settings;

        // 全局滚动条的位置状态
        private Vector2 mainScrollPosition = Vector2.zero;

        // 缓存 RimPsyche 加载状态
        private static bool _isRimPsycheLoaded = false;

        public DirectorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DirectorSettings>();
            _isRimPsycheLoaded = AccessTools.TypeByName("Maux36.RimPsyche.CompPsyche") != null;
            LongEventHandler.ExecuteWhenFinished(DirectorStartup.Initialize);
        }

        public override string SettingsCategory() => "RimTalk: Persona Director";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // --- 1. 计算内容总高度 (预估) ---
            // 标题(30) + 按钮(30) + 开关(24*4) + 过滤器(200+) + 模板选择(60) + Prompt编辑(300+)
            // 给一个足够大的高度，或者动态计算。为附加功能安全面板预留空间。
            float contentHeight = 1300f;
            Rect viewRect = new Rect(0, 0, inRect.width - 16f, contentHeight);

            // --- 2. 开始全局滚动视图 ---
            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            // ==========================================
            //  A. 标题与库按钮
            // ==========================================
            Rect titleRect = list.GetRect(30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect.LeftPart(0.7f), "RPD_Settings_Title".Translate());
            Text.Font = GameFont.Small;

            Rect libraryBtnRect = titleRect.RightPart(0.25f);
            if (Widgets.ButtonText(libraryBtnRect, "RPD_Settings_OpenLibrary".Translate()))
            {
                Find.WindowStack.Add(new Window_LibraryManager());
            }
            TooltipHandler.TipRegion(libraryBtnRect, "RPD_Tip_OpenLibrary".Translate());

            list.Gap(8f);

            // ==========================================
            //  B. 全局开关
            // ==========================================
            DrawExperimentalSafetySettings(list);

            if (DirectorFeatureGate.ExperimentalEnabled)
            {
                DrawAutoGenSettings(list);
                DrawAutoEvolveSettings(list);
            }

            list.GapLine();

            list.CheckboxLabeled("RPD_Settings_ShowMainButton".Translate(), ref Settings.ShowMainButton, "RPD_Settings_ShowMainButtonTip".Translate());

            list.CheckboxLabeled("RPD_Filter_DirectorNotes".Translate(), ref Settings.Context.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());
            list.CheckboxLabeled("RPD_Settings_EnableEvolve".Translate(), ref Settings.enableEvolveFeature, "RPD_Settings_EnableEvolveTip".Translate());
            list.CheckboxLabeled("RPD_Setting_DebugLog".Translate(), ref Settings.EnableDebugLog, "RPD_Setting_DebugLogDesc".Translate());


            list.GapLine();

            // ==========================================
            //  C. 数据过滤器 (三列布局)
            // ==========================================
            DrawContextFilterSettings(list);

            list.GapLine();

            // ==========================================
            //  D. RimTalk 模板集成 (新增)
            // ==========================================
            list.Label("<b>" + "RPD_Setting_RimTalkIntegration".Translate() + "</b>");
            list.Label("RPD_Setting_RimTalkIntegrationDesc".Translate());

            // 获取 RimTalk 所有预设名
            string noneLabel = "RPD_Setting_NoneInternal".Translate();

            List<string> rtPresets = new List<string> { noneLabel };
            try
            {
                // 使用反射或直接调用 API 获取预设列表
                // 假设你有 RimTalk.API 引用
                var presets = RimTalk.API.RimTalkPromptAPI.GetAllPresets();
                if (presets != null) rtPresets.AddRange(presets.Select(p => p.Name));
            }
            catch { }

            // 单体生成下拉
            Rect row1 = list.GetRect(24f);
            Widgets.Label(row1.LeftPart(0.4f), "RPD_Setting_ForSingleGen".Translate());
            string currentSingle = string.IsNullOrEmpty(Settings.rimTalkPreset_Single) ? noneLabel : Settings.rimTalkPreset_Single;
            if (Widgets.ButtonText(row1.RightPart(0.6f), currentSingle))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (var pName in rtPresets)
                {
                    string val = pName == noneLabel ? "" : pName;
                    opts.Add(new FloatMenuOption(pName, () => Settings.rimTalkPreset_Single = val));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            list.Gap(5f);

            // Evolve 下拉
            Rect row2 = list.GetRect(24f);
            Widgets.Label(row2.LeftPart(0.4f), "RPD_Setting_ForEvolve".Translate());

            string currentEvolve = string.IsNullOrEmpty(Settings.rimTalkPreset_Evolve) ? noneLabel : Settings.rimTalkPreset_Evolve;

            if (Widgets.ButtonText(row2.RightPart(0.6f), currentEvolve))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (var pName in rtPresets)
                {
                    string val = pName == noneLabel ? "" : pName;
                    opts.Add(new FloatMenuOption(pName, () => Settings.rimTalkPreset_Evolve = val));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            list.GapLine();

            // ==========================================
            //  E. 内置 Prompt 编辑器
            // ==========================================
            DrawPromptSection(list, viewRect.width); // 传入宽度

            list.End();
            Widgets.EndScrollView();
        }

        private void DrawExperimentalSafetySettings(Listing_Standard list)
        {
            bool currentValue = DirectorFeatureGate.ExperimentalEnabled;
            bool requestedValue = currentValue;

            GUI.enabled = !Settings.experimentalCircuitBroken;
            list.CheckboxLabeled(
                "RPD_Experimental_Enable".Translate(),
                ref requestedValue,
                "RPD_Experimental_EnableTip".Translate());
            GUI.enabled = true;

            if (requestedValue != currentValue)
            {
                if (requestedValue)
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RPD_Experimental_EnableConfirm".Translate(),
                        () => DirectorFeatureGate.SetEnabled(true),
                        destructive: false));
                }
                else
                {
                    DirectorFeatureGate.SetEnabled(false);
                }
            }

            if (!DirectorFeatureGate.ExperimentalEnabled)
            {
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                list.Label("RPD_Experimental_CoreMode".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            if (Settings.experimentalCircuitBroken)
            {
                string reason = Settings.experimentalCircuitBreakReason ?? "";
                if (reason.StartsWith("RPD_", StringComparison.Ordinal))
                {
                    reason = reason.Translate();
                }

                GUI.color = new Color(1f, 0.5f, 0.5f);
                list.Label("RPD_Experimental_FuseReason".Translate(reason));
                GUI.color = Color.white;

                if (list.ButtonText("RPD_Experimental_ClearError".Translate()))
                {
                    DirectorFeatureGate.ClearCircuitBreak();
                }
            }
        }

        private void DrawAutoGenSettings(Listing_Standard list)
        {
            Rect rowRect = list.GetRect(24f);
            Rect toggleRect = rowRect.LeftPart(0.72f);
            Rect buttonRect = rowRect.RightPart(0.25f);

            bool requestedValue = Settings.autoGenEnabled;
            Widgets.CheckboxLabeled(
                toggleRect,
                "RPD_Settings_EnableAutoGen".Translate(),
                ref requestedValue);
            TooltipHandler.TipRegion(toggleRect, "RPD_Settings_EnableAutoGenTip".Translate());

            if (requestedValue != Settings.autoGenEnabled)
            {
                Settings.autoGenEnabled = requestedValue;
                DirectorFeatureGate.ResetTransientWork();
                Settings.Write();
            }

            if (Widgets.ButtonText(buttonRect, "RPD_Settings_ConfigAutoGen".Translate()))
            {
                Settings.EnsureAutoGenCategories();
                Find.WindowStack.Add(new Window_AutoGenConfig());
            }
            TooltipHandler.TipRegion(buttonRect, "RPD_Settings_ConfigAutoGenTip".Translate());
        }

        private void DrawAutoEvolveSettings(Listing_Standard list)
        {
            Rect rowRect = list.GetRect(24f);
            Rect toggleRect = rowRect.LeftPart(0.72f);
            Rect buttonRect = rowRect.RightPart(0.25f);

            bool enabled = Settings.globalAutoEvolveEnabled;
            Widgets.CheckboxLabeled(
                toggleRect,
                "RPD_AutoEvolve_Enable".Translate(),
                ref enabled);
            TooltipHandler.TipRegion(toggleRect, "RPD_AutoEvolve_EnableTooltip".Translate());

            if (enabled != Settings.globalAutoEvolveEnabled)
            {
                Settings.globalAutoEvolveEnabled = enabled;
                DirectorFeatureGate.ResetTransientWork();
                Settings.Write();
            }

            if (Widgets.ButtonText(buttonRect, "RPD_Tab_AutoEvolution".Translate()))
            {
                Find.WindowStack.Add(new Window_AutoEvolveConfig());
            }
        }

        private void DrawPromptSection(Listing_Standard list, float width)
        {
            var settings = Settings;
            if (settings.presets == null || settings.presets.Count < 5) settings.InitPresets();
            settings.selectedPresetIndex = Mathf.Clamp(
                settings.selectedPresetIndex,
                0,
                settings.presets.Count - 1);

            var currentPreset = settings.presets[settings.selectedPresetIndex];

            // 标题 & 重置
            Rect headerRect = list.GetRect(24f);
            Widgets.Label(headerRect.LeftPart(0.7f), "RPD_Prompt_Label".Translate());
            if (Widgets.ButtonText(headerRect.RightPart(0.3f), "RPD_Button_Reset".Translate()))
            {
                // 重置逻辑
                if (settings.selectedPresetIndex == 0) { currentPreset.label = "Standard (3 Options)"; currentPreset.text = DirectorSettings.DefaultPrompt_Standard; }
                else if (settings.selectedPresetIndex == 1) { currentPreset.label = "Simple (One Shot)"; currentPreset.text = DirectorSettings.DefaultPrompt_Simple; }
                else if (settings.selectedPresetIndex == 2) { currentPreset.label = "Strict (Backstory)"; currentPreset.text = DirectorSettings.DefaultPrompt_Strict; }
                else if (settings.selectedPresetIndex == 3) { currentPreset.label = "Evolution (Update Only)"; currentPreset.text = DirectorSettings.DefaultPrompt_Evolve; }
                else if (settings.selectedPresetIndex == 4) { currentPreset.label = "Evolution (Overwrite)"; currentPreset.text = DirectorSettings.DefaultPrompt_Overwrite; }
            }

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            list.Label("RPD_Label_JsonTip".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            list.Gap(2f);

            // 控制行 (Token & Dropdown)
            Rect ctrlRect = list.GetRect(26f);
            int userChar = currentPreset.text?.Length ?? 0;
            int hiddenChar = DirectorSettings.HiddenTechnicalPrompt_Single.Length;
            int estTokens = (int)((userChar + hiddenChar) / 2.5f);

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(ctrlRect.LeftPart(0.4f), "RPD_Label_TokenEst".Translate(estTokens));

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleLeft;

            Rect rightPart = ctrlRect.RightPart(0.6f);
            float dropdownWidth = 100f;
            float labelWidth = rightPart.width - dropdownWidth - 5f;

            Rect labelRect = new Rect(rightPart.x, rightPart.y, labelWidth, 24f);
            string newLabel = Widgets.TextField(labelRect, currentPreset.label);
            if (newLabel != currentPreset.label) currentPreset.label = newLabel;

            Rect dropdownRect = new Rect(labelRect.xMax + 5f, rightPart.y, dropdownWidth, 24f);
            string slotLabel = "RPD_Setting_Slot".Translate(settings.selectedPresetIndex + 1) + " ▼";

            if (Widgets.ButtonText(dropdownRect, slotLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < settings.presets.Count; i++)
                {
                    int index = i;
                    string name = settings.presets[i].label;
                    if (string.IsNullOrEmpty(name)) name = "RPD_Setting_Slot".Translate(i + 1);
                    options.Add(new FloatMenuOption($"{i + 1}. {name}", () => settings.selectedPresetIndex = index));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Anchor = TextAnchor.UpperLeft;
            list.Gap(5f);

            // 大文本框 (固定高度，比如 400)
            Rect outRect = list.GetRect(600f);
            // 注意：这里不需要再嵌套 ScrollView 了，因为最外层已经有一个 ScrollView 了
            // 直接用 TextArea 即可
            currentPreset.text = Widgets.TextArea(outRect, currentPreset.text);
        }

        private void DrawContextFilterSettings(Listing_Standard listingStandard)
        {
            DirectorContextSettingsDrawer.Draw(
                listingStandard,
                Settings.Context,
                _isRimPsycheLoaded);
        }

    }
}
