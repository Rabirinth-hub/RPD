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

        public Window_LibraryManager()
        {
            doCloseX = true;
            resizeable = true;
            draggable = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);
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
            if (Widgets.ButtonText(filterRect, "RPD_Library_Category".Translate(selectedCategoryFilter)))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("All", () => selectedCategoryFilter = "All") };
                foreach (var cat in DirectorMod.Settings.userPresets.Select(p => p.category).Distinct())
                    opts.Add(new FloatMenuOption(cat, () => selectedCategoryFilter = cat));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            topY += 30f;

            // 列表区：高度扣除搜索、分类和底部的 Add 按钮
            float bottomMargin = 70f; // ★ 两行按钮的高度 (30 + 5 + 30)
            Rect listRect = new Rect(innerLeft.x, topY, innerLeft.width, innerLeft.height - (topY - innerLeft.y) - bottomMargin);
            var filtered = DirectorMod.Settings.userPresets
                .Where(p => searchWidget.filter.Matches(p.label) && (selectedCategoryFilter == "All" || p.category == selectedCategoryFilter)).ToList();

            DrawLeftList(listRect, filtered,
                p => $"[{p.category}] {p.label}",
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
            opts.Add(new FloatMenuOption("RPD_Library_AddAll".Translate(targetCategory.Translate()), () =>
            {
                int count = 0;
                if (vanillaSource != null)
                {
                    foreach (var p in vanillaSource)
                    {
                        string text = p.Persona.Translate().Resolve();
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            string label = ExtractLabelFromText(text) ?? $"{targetCategory} {++count}";
                            userPresets.Add(new CustomPreset { label = label, personaText = text, chattiness = p.Chattiness, category = targetCategory, enabled = true });
                            count++;
                        }
                    }
                }
                else if (builtInSource != null)
                {
                    foreach (var p in builtInSource)
                    {
                        // ★ 修正：确保内置内容在导入时被翻译 ★
                        string text = p.personaText.Translate().Resolve();
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
            opts.Add(new FloatMenuOption("RPD_Library_RemoveAll".Translate(targetCategory.Translate()), () =>
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
                    foreach (var p in vanillaSource)
                    {
                        string text = p.Persona.Translate().Resolve();
                        string label = ExtractLabelFromText(text) ?? "Vanilla Preset";
                        if (!userPresets.Any(x => x.personaText == text))
                        {
                            // ★ 增加 tooltip 属性 ★
                            var opt = new FloatMenuOption(label, () => {
                                userPresets.Add(new CustomPreset { label = label, personaText = text, chattiness = p.Chattiness, category = targetCategory, enabled = true });
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

        private string ExtractLabelFromText(string text)
        {
            // ... (复制之前的逻辑) ...
            if (string.IsNullOrEmpty(text)) return null;
            string[] separators = new[] { " - ", " – ", " — ", "：", ": " };
            foreach (var sep in separators)
            {
                int index = text.IndexOf(sep);
                if (index > 0 && index < 30) return text.Substring(0, index).Trim();
            }
            return null;
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
                r => $"{r.type}: {r.targetDefName ?? "None"} (P:{r.priority})",
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
                if (editor.ButtonText("RPD_Library_TargetDef".Translate(btnLabel))) OpenDefSelector(selectedRule);
                GUI.color = Color.white;

                editor.GapLine();
                editor.Label("RPD_Library_PresetsPool".Translate());

                float poolH = rightRect.height - editor.CurHeight - 80f;
                Rect poolRect = editor.GetRect(poolH);
                Widgets.DrawMenuSection(poolRect);

                var presets = DirectorMod.Settings.userPresets;
                Rect viewRect = new Rect(0, 0, poolRect.width - 16f, presets.Count * 26f);
                Widgets.BeginScrollView(poolRect, ref poolScroll, viewRect);
                for (int i = 0; i < presets.Count; i++)
                {
                    Rect row = new Rect(0, i * 26f, viewRect.width, 24f);
                    bool active = selectedRule.allowedPresetIds.Contains(presets[i].id);
                    bool newActive = active;
                    Widgets.CheckboxLabeled(row, presets[i].label, ref newActive);
                    if (newActive != active)
                    {
                        if (newActive) selectedRule.allowedPresetIds.Add(presets[i].id);
                        else selectedRule.allowedPresetIds.Remove(presets[i].id);
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
                default:
                    return type.ToString(); // 保底
            }
        }
    }
}