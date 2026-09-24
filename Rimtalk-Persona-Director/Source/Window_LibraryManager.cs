using RimTalk.Data;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class Window_LibraryManager : Window
    {
        private enum Tab { Presets, Rules }
        private Tab curTab = Tab.Presets;

        private Vector2 scrollLeft, scrollRight, poolScroll;
        private QuickSearchWidget searchWidget = new QuickSearchWidget();

        private CustomPreset selectedPreset;
        private AssignmentRule selectedRule;
        private string selectedCategoryFilter = "All";

        // 新增：规则界面右侧 PresetsPool 分类和全选功能
        private string selectedRulePresetCategory = "All";
        private bool IsAllPresetsSelected(List<CustomPreset> pool, AssignmentRule rule)
        {
            var filtered = pool.Where(p => selectedRulePresetCategory == "All" || p.category == selectedRulePresetCategory).Select(p => p.id);
            return filtered.All(id => rule.allowedPresetIds.Contains(id)) && filtered.Any();
        }

        public Window_LibraryManager()
        {
            doCloseX = true;
            resizeable = true;
            draggable = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public override void PostClose()
        {
            PresetSynchronizer.SyncToRimTalk();
            DirectorMod.Settings.Write();
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 在执行任何操作之前，确保列表对象已创建
            if (DirectorMod.Settings.userPresets == null)
            {
                DirectorMod.Settings.userPresets = new List<CustomPreset>();
            }
            if (DirectorMod.Settings.assignmentRules == null)
            {
                DirectorMod.Settings.assignmentRules = new List<AssignmentRule>();
            }
            // --- 1. 手动定义所有顶层区域 ---
            float topBarHeight = 35f; // 给重置按钮留一行
            float tabHeight = 30f;    // Tab 的高度

            // A. 顶部栏 (只放重置按钮)
            Rect topBarRect = new Rect(inRect.x, inRect.y, inRect.width, topBarHeight);

            // B. Tab 栏 (紧跟在顶部栏下方)
            Rect tabsRect = new Rect(inRect.x, topBarRect.yMax, inRect.width, tabHeight);

            // C. 主内容区起始 Y
            float contentY = tabsRect.yMax;
            // 计算内容区高度时要以 inRect.y 为基准：height = inRect.height - (contentY - inRect.y)
            float contentHeight = inRect.height - (contentY - inRect.y);
            Rect contentRect = new Rect(inRect.x, contentY, inRect.width, contentHeight);

            // --- 2. 绘制控件 ---

            // A. 绘制重置按钮
            float buttonWidth = 120f;
            Rect resetRect = new Rect(topBarRect.xMax - buttonWidth - 5f, topBarRect.y + 2f, buttonWidth, 30f);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, "RPD_Library_ResetDefaults".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RPD_Library_ResetConfirm".Translate(), () => {
                    DirectorMod.Settings.userPresets.Clear();
                    DirectorMod.Settings.assignmentRules.Clear();
                    DirectorMod.Settings.InitLibrary();
                    selectedPreset = null;
                    selectedRule = null;
                }));
            }
            GUI.color = Color.white;

            // 新增：导入/导出按钮 放在重置按钮左边
            Rect ioRect = new Rect(resetRect.x - 130f, resetRect.y, 120f, 30f);
            if (Widgets.ButtonText(ioRect, "RPD_Library_I_E".Translate()))
            {
                Find.WindowStack.Add(new Window_ImportExport());
            }

            // B. 绘制 Tab 栏
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("RPD_Library_TabPresets".Translate(), () =>
                {
                    if (curTab != Tab.Presets) { curTab = Tab.Presets; searchWidget.Reset(); }
                }, curTab == Tab.Presets),

                new TabRecord("RPD_Library_TabRules".Translate(), () =>
                {
                    if (curTab != Tab.Rules) { curTab = Tab.Rules; searchWidget.Reset(); }
                }, curTab == Tab.Rules)
            };
            TabDrawer.DrawTabs(tabsRect, tabs);

            // C. 绘制主内容区
            if (curTab == Tab.Presets) DrawPresetsTab(contentRect);
            else DrawRulesTab(contentRect);
        }

        private void DrawPresetsTab(Rect rect)
        {
            Rect leftRect = rect.LeftPart(0.4f).Rounded();
            Rect rightRect = rect.RightPart(0.58f).Rounded();

            // --- 左侧列表 ---
            Widgets.DrawMenuSection(leftRect);
            Rect innerLeft = leftRect.ContractedBy(5f);

            float topY = innerLeft.y;

            // 1. 搜索
            Rect searchRect = new Rect(innerLeft.x, topY, innerLeft.width, 24f);
            searchWidget.OnGUI(searchRect);
            topY += 30f;
            
            // 2. 分类筛选
            Rect filterRect = new Rect(innerLeft.x, topY, innerLeft.width, 24f);
            if (Widgets.ButtonText(filterRect, "RPD_Library_Category".Translate(TranslatePresetCategory(selectedCategoryFilter))))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption(TranslatePresetCategory("All"), () => selectedCategoryFilter = "All") };
                foreach (var cat in DirectorMod.Settings.userPresets.Select(p => p.category).Distinct())
                    opts.Add(new FloatMenuOption(TranslatePresetCategory(cat), () => selectedCategoryFilter = cat));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            topY += 30f;

            // 列表区：高度扣除搜索、分类和底部的 Add 按钮
            float bottomMargin = 70f; // ★ 两行按钮的高度 (30 + 5 + 30)
            Rect listRect = new Rect(innerLeft.x, topY, innerLeft.width, innerLeft.height - (topY - innerLeft.y) - bottomMargin);
            var filtered = DirectorMod.Settings.userPresets
                .Where(p => searchWidget.filter.Matches(p.label) && (selectedCategoryFilter == "All" || p.category == selectedCategoryFilter)).ToList();

            DrawLeftList(listRect, filtered,
                p => $"[{TranslatePresetCategory(p.category)}] {p.label}",
                p => selectedPreset = p,
                ref scrollLeft,
                selectedPreset,
                p => p.enabled,
                (p, v) => {
                    p.enabled = v;
                    // ★ 切换开关后，立即同步到 RimTalk ★
                    PresetSynchronizer.SyncToRimTalk();
                }
            );

            // 4. ★★★ 底部按钮行 1 (新增/删除) ★★★
            Rect bottomRow1 = new Rect(innerLeft.x, listRect.yMax + 5f, innerLeft.width, 30f);
            float btnWidth = (bottomRow1.width - 10f) / 2f;

            // 新增 (左)
            if (Widgets.ButtonText(new Rect(bottomRow1.x, bottomRow1.y, btnWidth, 30f), "RPD_Library_CreatePreset".Translate()))
            {
                var n = new CustomPreset("New Preset", "...") { category = "Custom", enabled = true };
                DirectorMod.Settings.userPresets.Add(n);
                selectedPreset = n;
            }

            // 删除 (右, 只有选中了才可用)
            if (selectedPreset != null)
            {
                Rect delRect = new Rect(bottomRow1.x + btnWidth + 10f, bottomRow1.y, btnWidth, 30f);
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(delRect, "RPD_Library_DeletePreset".Translate()))
                {
                    DirectorMod.Settings.userPresets.Remove(selectedPreset);
                    selectedPreset = null;
                }
                GUI.color = Color.white;
            }

            // 5. ★★★ 底部按钮行 2 (管理菜单) ★★★
            Rect bottomRow2 = new Rect(innerLeft.x, bottomRow1.yMax + 5f, innerLeft.width, 30f);
            float manageBtnWidth = (bottomRow2.width - 10f) / 2f;

            // 左: Manage Vanilla
            if (Widgets.ButtonText(new Rect(bottomRow2.x, bottomRow2.y, manageBtnWidth, 30f), "RPD_Library_ManageVanilla".Translate()))
            {
                OpenManageMenu("Vanilla", DirectorSettings.OriginalVanillaCache, null);
            }

            // 右: Manage Built-in
            if (Widgets.ButtonText(new Rect(bottomRow2.x + manageBtnWidth + 10f, bottomRow2.y, manageBtnWidth, 30f), "RPD_Library_ManageBuiltin".Translate()))
            {
                // 修正：内置预设直接传库
                OpenManageMenu("Built-in", null, PresetLibrary.Defaults);
            }

            // --- 右侧编辑器 ---
            Widgets.DrawMenuSection(rightRect);
            if (selectedPreset != null)
            {
                Listing_Standard editor = new Listing_Standard();
                editor.Begin(rightRect.ContractedBy(12f));
                editor.Label("RPD_Library_PresetName".Translate());
                selectedPreset.label = editor.TextEntry(selectedPreset.label);
                editor.Label("RPD_Library_PresetCategory".Translate());
                selectedPreset.category = editor.TextEntry(selectedPreset.category);
                editor.Label("RPD_Library_Chattiness".Translate(selectedPreset.chattiness.ToString("F2")));
                selectedPreset.chattiness = editor.Slider(selectedPreset.chattiness, 0f, 1f);
                editor.Label("RPD_Library_Description".Translate());
                float h = rightRect.height - editor.CurHeight - 80f;
                selectedPreset.personaText = Widgets.TextArea(editor.GetRect(h), selectedPreset.personaText);
                editor.Gap(10f);
                editor.End();
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightRect, "RPD_Library_SelectToEdit".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void OpenManageMenu(string targetCategory, List<RimTalk.Data.PersonalityData> vanillaSource = null, List<CustomPreset> builtInSource = null)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            var userPresets = DirectorMod.Settings.userPresets;

            // --- 1. 添加全部 ---
            opts.Add(new FloatMenuOption("RPD_Library_AddAll".Translate(TranslatePresetCategory(targetCategory)), () =>
            {
                int count = 0;
                if (vanillaSource != null)
                {
                    foreach (var p in vanillaSource)
                    {
                        string text = p.Persona;
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            string label = DirectorMod.Settings.CreateVanillaLabel(text, count + 1);
                            userPresets.Add(new CustomPreset
                            {
                                label = label,
                                personaText = text,
                                chattiness = p.Chattiness,
                                category = targetCategory,
                                localizationKey = p.Persona,
                                lastLocalizedText = text,
                                enabled = true
                            });
                            count++;
                        }
                    }
                }
                else if (builtInSource != null)
                {
                    foreach (var p in builtInSource)
                    {
                        // ★ 修正：确保内置内容在导入时被翻译 ★
                        string text = p.personaText.Translate(). Resolve();
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            userPresets.Add(new CustomPreset { label = p.label, personaText = text, chattiness = p.chattiness, category = targetCategory, enabled = true });
                            count++;
                        }
                    }
                }
                PresetSynchronizer.SyncToRimTalk();
                Messages.Message("RPD_Library_MsgAdded".Translate(count), MessageTypeDefOf.PositiveEvent, false);
            }));

            // --- 2. 移除全部 ---
            opts.Add(new FloatMenuOption("RPD_Library_RemoveAll".Translate(TranslatePresetCategory(targetCategory)), () =>
            {
                int removed = userPresets.RemoveAll(p => p.category == targetCategory);
                selectedPreset = null;
                PresetSynchronizer.SyncToRimTalk();
                Messages.Message("RPD_Library_MsgRemoved".Translate(removed), MessageTypeDefOf.NeutralEvent, false);
            }));

            // --- 3. 选择添加 (带悬浮窗预览) ---
            opts.Add(new FloatMenuOption("RPD_Library_AddSpecific".Translate(), () =>
            {
                List<FloatMenuOption> subOpts = new List<FloatMenuOption>();

                if (vanillaSource != null)
                {
                    int sourceIndex = 0;
                    foreach (var p in vanillaSource)
                    {
                        sourceIndex++;
                        string text = p.Persona;
                        string label = DirectorMod.Settings.CreateVanillaLabel(text, sourceIndex);
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            // ★ 增加 tooltip 属性 ★
                            var opt = new FloatMenuOption(label, () => {
                                userPresets.Add(new CustomPreset
                                {
                                    label = label,
                                    personaText = text,
                                    chattiness = p.Chattiness,
                                    category = targetCategory,
                                    localizationKey = p.Persona,
                                    lastLocalizedText = text,
                                    enabled = true
                                });
                                PresetSynchronizer.SyncToRimTalk();
                            });
                            opt.tooltip = new TipSignal(text);
                            subOpts.Add(opt);
                        }
                    }
                }
                else if (builtInSource != null)
                {
                    foreach (var p in builtInSource)
                    {
                        string text = p.personaText.Translate().Resolve();
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            // ★ 增加 tooltip 属性 ★
                            var opt = new FloatMenuOption(p.label, () => {
                                userPresets.Add(new CustomPreset { label = p.label, personaText = text, chattiness = p.chattiness, category = targetCategory, enabled = true });
                                PresetSynchronizer.SyncToRimTalk();
                            });
                            opt.tooltip = new TipSignal(text);
                            subOpts.Add(opt);
                        }
                    }
                }

                if (subOpts.Count == 0) subOpts.Add(new FloatMenuOption("RPD_Library_AllAdded".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(subOpts));
            }));

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void DrawRulesTab(Rect rect)
        {
            // Mirror Presets layout exactly for consistent alignment
            Rect leftRect = rect.LeftPart(0.4f).Rounded();
            Rect rightRect = rect.RightPart(0.58f).Rounded();

            // --- 左侧列表 ---
            Widgets.DrawMenuSection(leftRect);
            Rect innerLeft = leftRect.ContractedBy(5f);
            float topY = innerLeft.y;

            // 搜索
            Rect searchRect = new Rect(innerLeft.x, topY, innerLeft.width, 24f);
            searchWidget.OnGUI(searchRect);
            topY += 30f;

            // 列表区：高度扣除搜索、分类和底部的按钮
            float bottomMargin = 70f;
            Rect listRect = new Rect(innerLeft.x, topY, innerLeft.width, innerLeft.height - (topY - innerLeft.y) - bottomMargin);
            var filtered = DirectorMod.Settings.assignmentRules
                .Where(r => searchWidget.filter.Matches(r.targetDefName ?? "")).ToList();

            DrawLeftList(listRect, filtered,
                r => $"{TranslateRuleType(r.type)}: {r.targetDefName ?? "None"} (P:{r.priority})",
                r => selectedRule = r,
                ref scrollRight,
                selectedRule,
                r => r.enabled,         // Get
                (r, v) => r.enabled = v // Set
            );

            // 底部按钮行：与 Presets 一样平分左右
            Rect bottomRow1 = new Rect(innerLeft.x, listRect.yMax + 5f, innerLeft.width, 30f);
            float btnWidth = (bottomRow1.width - 10f) / 2f;

            // 新增规则 (左)
            Rect addRect = new Rect(bottomRow1.x, bottomRow1.y, btnWidth, 30f);
            if (Widgets.ButtonText(addRect, "RPD_Library_CreateRule".Translate()))
            {
                var n = new AssignmentRule { targetDefName = null, type = RuleType.FactionDef };
                DirectorMod.Settings.assignmentRules.Add(n);
                selectedRule = n;
            }

            // 删除规则 (右，只有选中了才可用)
            if (selectedRule != null)
            {
                Rect delRect = new Rect(bottomRow1.x + btnWidth + 10f, bottomRow1.y, btnWidth, 30f);
                GUI.color = Color.red;
                if (Widgets.ButtonText(delRect, "RPD_Library_DeleteRule".Translate()))
                {
                    DirectorMod.Settings.assignmentRules.Remove(selectedRule);
                    selectedRule = null;
                }
                GUI.color = Color.white;
            }

            // --- 右侧编辑器 ---
            Widgets.DrawMenuSection(rightRect);
            if (selectedRule != null)
            {
                Listing_Standard editor = new Listing_Standard();
                editor.Begin(rightRect.ContractedBy(12f));

                editor.CheckboxLabeled("RPD_Library_RuleEnabled".Translate(), ref selectedRule.enabled);
                editor.Label("RPD_Library_Priority".Translate(selectedRule.priority));
                selectedRule.priority = (int)editor.Slider(selectedRule.priority, 0, 100);

                if (editor.ButtonText("RPD_Library_TargetType".Translate(TranslateRuleType(selectedRule.type))))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (RuleType t in System.Enum.GetValues(typeof(RuleType)))
                        opts.Add(new FloatMenuOption(TranslateRuleType(t), () => { selectedRule.type = t; selectedRule.targetDefName = null; }));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }

                string btnLabel = selectedRule.targetDefName ?? "RPD_Library_TargetSelect".Translate();
                if (!IsDefExisting(selectedRule.type, selectedRule.targetDefName)) GUI.color = Color.red;
                if (selectedRule.type != RuleType.Age)
                {
                    if (editor.ButtonText("RPD_Library_TargetDef".Translate(btnLabel))) OpenDefSelector(selectedRule);
                }
                GUI.color = Color.white;

                // 年龄规则：显示两个数字输入框
                if (selectedRule.type == RuleType.Age)
                {
                    // int 类型不会为 null，无需判断
                    editor.Label("RPD_Library_AgeRange".Translate());
                    Rect ageRect = editor.GetRect(28f);
                    float labelW = 40f, fieldW = 60f;
                    // spacing 只声明一次
                    float spacingAge = 10f;
                    Widgets.Label(new Rect(ageRect.x, ageRect.y, labelW, 28f), "Min");
                    string minStr = selectedRule.minAge.ToString();
                    Widgets.TextFieldNumeric(new Rect(ageRect.x + labelW, ageRect.y, fieldW, 28f), ref selectedRule.minAge, ref minStr, 0, 999);
                    Widgets.Label(new Rect(ageRect.x + labelW + fieldW + spacingAge, ageRect.y, labelW, 28f), "Max");
                    string maxStr = selectedRule.maxAge.ToString();
                    Widgets.TextFieldNumeric(new Rect(ageRect.x + labelW * 2 + fieldW + spacingAge, ageRect.y, fieldW, 28f), ref selectedRule.maxAge, ref maxStr, 0, 999);
                }

                editor.GapLine();

                // --- 新增：PresetsPool 分类和全选按钮 ---
                Rect labelRect = editor.GetRect(24f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(labelRect.x, labelRect.y, 180f, 24f), "RPD_Library_PresetsPool".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                var presets = DirectorMod.Settings.userPresets;
                // 分类按钮
                float btnW = 180f;
                float btnH = 24f;
                float spacing = 8f;
                float rightBtnX = labelRect.xMax - btnW;
                float leftBtnX = labelRect.xMax - btnW * 2 - spacing;
                // 分类按钮
                if (Widgets.ButtonText(new Rect(leftBtnX, labelRect.y, btnW, btnH), "RPD_Library_Category".Translate(TranslatePresetCategory(selectedRulePresetCategory))))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption(TranslatePresetCategory("All"), () => selectedRulePresetCategory = "All") };
                    foreach (var cat in presets.Select(p => p.category).Distinct())
                        opts.Add(new FloatMenuOption(TranslatePresetCategory(cat), () => selectedRulePresetCategory = cat));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                // 全选/全不选按钮
                bool allSelected = IsAllPresetsSelected(presets, selectedRule);
                string selectLabel = allSelected ? "RPD_Library_UnselectAll".Translate() : "RPD_Library_SelectAll".Translate();
                if (Widgets.ButtonText(new Rect(rightBtnX, labelRect.y, btnW, btnH), selectLabel))
                {
                    var filteredIds = presets.Where(p => selectedRulePresetCategory == "All" || p.category == selectedRulePresetCategory).Select(p => p.id).ToList();
                    if (allSelected)
                    {
                        // 全不选
                        foreach (var id in filteredIds)
                            selectedRule.allowedPresetIds.Remove(id);
                    }
                    else
                    {
                        // 全选
                        foreach (var id in filteredIds)
                            if (!selectedRule.allowedPresetIds.Contains(id))
                                selectedRule.allowedPresetIds.Add(id);
                    }
                }

                float poolH = rightRect.height - editor.CurHeight - 80f;
                Rect poolRect = editor.GetRect(poolH);
                Widgets.DrawMenuSection(poolRect);

                // 只显示当前分类下的预设
                var filteredPresets = presets.Where(p => selectedRulePresetCategory == "All" || p.category == selectedRulePresetCategory).ToList();
                Rect viewRect = new Rect(0, 0, poolRect.width - 16f, filteredPresets.Count * 26f);
                Widgets.BeginScrollView(poolRect, ref poolScroll, viewRect);
                for (int i = 0; i < filteredPresets.Count; i++)
                {
                    Rect row = new Rect(0, i * 26f, viewRect.width, 24f);
                    bool active = selectedRule.allowedPresetIds.Contains(filteredPresets[i].id);
                    bool newActive = active;
                    Widgets.CheckboxLabeled(row, filteredPresets[i].label, ref newActive);
                    if (newActive != active)
                    {
                        if (newActive) selectedRule.allowedPresetIds.Add(filteredPresets[i].id);
                        else selectedRule.allowedPresetIds.Remove(filteredPresets[i].id);
                    }
                }
                Widgets.EndScrollView();

                editor.End();
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightRect, "RPD_Library_SelectToEdit".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawLeftList<T>(Rect rect, List<T> list,
            System.Func<T, string> labelFunc,
            System.Action<T> onSelect,
            ref Vector2 scrollPos,
            T selectedItem,
            System.Func<T, bool> getEnabled, // 读取开关状态
            System.Action<T, bool> setEnabled) // 写入开关状态
        {
            float viewHeight = list.Count * 30f;
            Rect viewRect = new Rect(0, 0, rect.width - 16f, viewHeight);

            Widgets.DrawMenuSection(rect);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                Rect rowRect = new Rect(0, i * 30f, viewRect.width, 30f);

                if (i % 2 == 1) Widgets.DrawLightHighlight(rowRect);
                if (selectedItem != null && item.Equals(selectedItem)) Widgets.DrawHighlightSelected(rowRect);

                // --- 1. 绘制开关 (Checkbox) ---
                bool isEnabled = getEnabled(item);
                bool newEnabled = isEnabled;

                // Checkbox 放在最左边 (24x24)
                Rect checkRect = new Rect(rowRect.x + 2f, rowRect.y + 3f, 24f, 24f);
                Widgets.Checkbox(checkRect.x, checkRect.y, ref newEnabled);

                if (newEnabled != isEnabled)
                {
                    setEnabled(item, newEnabled); // 触发回调
                }

                // --- 2. 绘制点击区域 (剩下的部分) ---
                Rect clickRect = new Rect(checkRect.xMax + 5f, rowRect.y, rowRect.width - 35f, rowRect.height);
                if (Widgets.ButtonInvisible(clickRect)) onSelect(item);

                // --- 3. 绘制 Label ---
                Rect labelRect = clickRect;
                string label = labelFunc(item);
                if (label.Length > 35) label = label.Substring(0, 32) + "...";

                // 如果禁用了，文字变灰
                if (!newEnabled) GUI.color = Color.gray;
                Widgets.Label(labelRect, label);
                GUI.color = Color.white;
            }

            Widgets.EndScrollView();
        }

        private bool IsDefExisting(RuleType type, string defName)
        {
            if (string.IsNullOrEmpty(defName)) return true; // 还没选，算存在

            // ★★★ 核心修复：改回旧的 switch-case 语法 ★★★
            switch (type)
            {
                case RuleType.FactionDef:
                    return DefDatabase<FactionDef>.GetNamedSilentFail(defName) != null;
                case RuleType.RaceDef:
                    return DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null;
                case RuleType.XenotypeDef:
                    // 确保在检查 Def 前先检查 Mod 是否激活，防止报错
                    return ModsConfig.BiotechActive && DefDatabase<XenotypeDef>.GetNamedSilentFail(defName) != null;
                default:
                    return false;
            }
        }

        private void OpenDefSelector(AssignmentRule rule)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            // ★★★ 核心修复：修复按钮失效 (闭包捕获) ★★★
            if (rule.type == RuleType.FactionDef)
            {
                foreach (var def in DefDatabase<FactionDef>.AllDefs.OrderBy(d => d.label))
                {
                    var currentDef = def; // 创建局部变量
                    opts.Add(new FloatMenuOption($"{currentDef.LabelCap} ({currentDef.defName})",
                        () => rule.targetDefName = currentDef.defName));
                }
            }
            else if (rule.type == RuleType.RaceDef)
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null && d.race.Humanlike).OrderBy(d => d.label))
                {
                    var currentDef = def; // 创建局部变量
                    opts.Add(new FloatMenuOption(currentDef.LabelCap,
                        () => rule.targetDefName = currentDef.defName));
                }
            }
            else if (rule.type == RuleType.XenotypeDef && ModsConfig.BiotechActive)
            {
                foreach (var def in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label))
                {
                    var currentDef = def; // 创建局部变量
                    opts.Add(new FloatMenuOption(currentDef.LabelCap,
                        () => rule.targetDefName = currentDef.defName));
                }
            }

            if (opts.Count == 0) opts.Add(new FloatMenuOption("RPD_Library_NoDefsFound".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenImportFloatMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            foreach (var p in PresetLibrary.Defaults)
            {
                // 菜单上只显示简短的标签，保持列表干净
                string menuLabel = p.label;

                // ★★★ 核心修改：创建一个带 Tooltip 的 FloatMenuOption ★★★
                var option = new FloatMenuOption(menuLabel, () =>
                {
                    // 点击后的行为 (保持不变)
                    if (selectedPreset != null)
                    {
                        selectedPreset.label = p.label;
                        selectedPreset.personaText = p.personaText;
                        selectedPreset.chattiness = p.chattiness;
                    }
                });

                // ★★★ 为选项添加详细的 Tooltip ★★★
                // Tooltip 的内容就是完整的、翻译过的 personaText
                option.tooltip = new TipSignal(p.personaText);

                opts.Add(option);
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }
        private static string TranslatePresetCategory(string category)
        {
            switch (category)
            {
                case "All": return "RPD_Library_CategoryAll".Translate();
                case "Vanilla": return "RPD_Library_CategoryVanilla".Translate();
                case "Built-in": return "RPD_Library_CategoryBuiltIn".Translate();
                default: return category;
            }
        }

        private string TranslateRuleType(RuleType type)
        {
            switch (type)
            {
                case RuleType.FactionDef:
                    return "RPD_RuleType_FactionDef".Translate();
                case RuleType.RaceDef:
                    return "RPD_RuleType_RaceDef".Translate();
                case RuleType.XenotypeDef:
                    return "RPD_RuleType_XenotypeDef".Translate();
                case RuleType.Age:
                    return "RPD_RuleType_Age".Translate();
                default:
                    return type.ToString(); // 保底
            }
        }
    }
}
