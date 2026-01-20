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
        }

        public override string SettingsCategory() => "RimTalk: Persona Director";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // --- 1. 计算内容总高度 (预估) ---
            // 标题(30) + 按钮(30) + 开关(24*4) + 过滤器(200+) + 模板选择(60) + Prompt编辑(300+)
            // 给一个足够大的高度，或者动态计算。这里给 1200f 足够了。
            float contentHeight = 1200f;
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

            Settings.Write();
        }

        private void DrawPromptSection(Listing_Standard list, float width)
        {
            var settings = Settings;
            if (settings.presets == null || settings.presets.Count < 4) settings.InitPresets();

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
            var ctx = Settings.Context;

            listingStandard.Label("RPD_Setting_FilterLabel".Translate());
            listingStandard.Gap(5f);

            // 3. 计算列宽
            const float colGap = 10f; // 稍微紧凑一点
            int colCount = 3;
            // 总宽度减去间隙，除以列数
            float colWidth = (listingStandard.ColumnWidth - (colGap * (colCount - 1))) / colCount;

            // 获取当前 Y 轴位置
            Rect positionRect = listingStandard.GetRect(0f);
            float startY = positionRect.y;

            // =================================================
            // 第一列：生物与背景 (Biology & Background) - 6项
            // =================================================
            Rect col1Rect = new Rect(positionRect.x, startY, colWidth, 9999f);
            Listing_Standard list1 = new Listing_Standard { ColumnWidth = colWidth };
            list1.Begin(col1Rect);

            DrawHeader(list1, "RPD_Group_Bio".Translate());
            DrawFilterRow(list1, "RPD_Filter_Basic".Translate(), ref ctx.Inc_Basic);
            DrawFilterRow(list1, "RPD_Filter_Race".Translate(), ref ctx.Inc_Race, ref ctx.Inc_Race_Desc);
            DrawFilterRow(list1, "RPD_Filter_Genes".Translate(), ref ctx.Inc_Genes, ref ctx.Inc_Genes_Desc, "RPD_Tip_GenesDesc".Translate());
            DrawFilterRow(list1, "RPD_Filter_Backstory".Translate(), ref ctx.Inc_Backstory, ref ctx.Inc_Backstory_Desc);
            DrawFilterRow(list1, "RPD_Filter_Relations".Translate(), ref ctx.Inc_Relations);
            DrawFilterRow(list1, "RPD_Filter_DirectorNotes".Translate(), ref ctx.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());

            list1.End();

            // =================================================
            // 第二列：特征与状态 (Traits & Status) - 6项
            // =================================================
            Rect col2Rect = new Rect(col1Rect.xMax + colGap, startY, colWidth, 9999f);
            Listing_Standard list2 = new Listing_Standard { ColumnWidth = colWidth };
            list2.Begin(col2Rect);

            DrawHeader(list2, "RPD_Group_Traits".Translate());
            DrawFilterRow(list2, "RPD_Filter_Traits".Translate(), ref ctx.Inc_Traits, ref ctx.Inc_Traits_Desc);
            DrawFilterRow(list2, "RPD_Filter_Ideology".Translate(), ref ctx.Inc_Ideology, ref ctx.Inc_Ideology_Desc);
            DrawFilterRow(list2, "RPD_Filter_Skills".Translate(), ref ctx.Inc_Skills, ref ctx.Inc_Skills_Desc);
            DrawFilterRow(list2, "RPD_Filter_Health".Translate(), ref ctx.Inc_Health, ref ctx.Inc_Health_Desc);
            DrawFilterRow(list2, "RPD_Filter_Equipment".Translate(), ref ctx.Inc_Equipment);
            DrawFilterRow(list2, "RPD_Filter_Inventory".Translate(), ref ctx.Inc_Inventory);

            list2.End();

            // =================================================
            // 第三列：外部数据源 (External Data) - 动态显示
            // =================================================
            Rect col3Rect = new Rect(col2Rect.xMax + colGap, startY, colWidth, 9999f);
            Listing_Standard list3 = new Listing_Standard { ColumnWidth = colWidth };
            list3.Begin(col3Rect);

            DrawHeader(list3, "RPD_Group_ExternalData".Translate());

            DrawFilterRow(list3, "RPD_Filter_DataComparison".Translate(), ref ctx.Inc_DataComparison);

            // 1. RimPsyche
            if (_isRimPsycheLoaded)
            {
                    DrawFilterRow(list3, "RPD_Filter_RimPsyche".Translate(), ref ctx.Inc_RimPsyche, ref ctx.Inc_RimPsyche_All, "RPD_Tip_RimPsyche".Translate());
            }

            // 2. Memory Mod
            if (ModsConfig.IsActive("cj.rimtalk.expandmemory"))
            {
                    DrawFilterRow(list3, "RPD_Filter_Memories".Translate(), ref ctx.Inc_Memories);
                    DrawFilterRow(list3, "RPD_Filter_CommonKnowledge".Translate(), ref ctx.Inc_CommonKnowledge);
            }


                list3.End();

            // =================================================
            // 布局收尾
            // =================================================
            // 计算三列中最高的一列，撑开主 Listing 的高度，防止内容重叠
            float maxHeight = Mathf.Max(list1.CurHeight, list2.CurHeight);
            maxHeight = Mathf.Max(maxHeight, list3.CurHeight);

            listingStandard.Gap(maxHeight);
        }

        private void DrawFilterRow(Listing_Standard list, string label, ref bool nameSwitch, ref bool descSwitch, string descTooltip = null)
        {
            Rect rowRect = list.GetRect(24f);

            float descCheckboxWidth = 24f;
            float descCheckboxPadding = 5f;

            Rect mainLabelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - descCheckboxWidth - descCheckboxPadding, rowRect.height);
            Widgets.CheckboxLabeled(mainLabelRect, label, ref nameSwitch);

            if (!nameSwitch) GUI.enabled = false;

            Rect descRect = new Rect(rowRect.xMax - descCheckboxWidth, rowRect.y, descCheckboxWidth, rowRect.height);
            Widgets.Checkbox(descRect.position, ref descSwitch, descCheckboxWidth, !nameSwitch);

            GUI.enabled = true;

            if (descTooltip != null)
            {
                TooltipHandler.TipRegion(rowRect, descTooltip);
            }
        }

        private void DrawFilterRow(Listing_Standard list, string label, ref bool nameSwitch, string tooltip = null)
        {
            Rect rowRect = list.GetRect(24f);
            Widgets.CheckboxLabeled(rowRect, label, ref nameSwitch);

            if (tooltip != null)
            {
                TooltipHandler.TipRegion(rowRect, tooltip);
            }
        }

        private void DrawHeader(Listing_Standard list, string text)
        {
            GUI.color = Color.yellow;
            list.Label($"━━ {text} ━━");
            GUI.color = Color.white;
            list.Gap(2f);
        }
    }
}