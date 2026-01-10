using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;

namespace RimPersonaDirector
{
    public class Window_PresetBrowser : Window
    {
        private Pawn pawnToApply;
        private Window editorWindow;

        private Vector2 categoryScroll, presetScroll;
        private string selectedCategory = "All";
        private CustomPreset selectedPreset;

        private bool _initialized = false;

        public Window_PresetBrowser(Pawn pawn, Window editor)
        {
            pawnToApply = pawn;
            editorWindow = editor;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            // 初始化
            if (!_initialized)
            {
                if (DirectorMod.Settings.userPresets.Count == 0)
                {
                    DirectorMod.Settings.InitLibrary();
                }
                _initialized = true;
            }

            // --- 主布局 ---
            Rect leftRect = inRect.LeftPart(0.3f).Rounded();
            Rect rightRect = inRect.RightPart(0.68f).Rounded();

            // --- 左侧：分类列表 ---
            DrawCategoryList(leftRect);

            // --- 右侧：预设列表与描述 ---
            DrawPresetArea(rightRect);
        }

        // ★★★ 重写：使用 Listing_Standard + 手动 ScrollView ★★★
        private void DrawCategoryList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);

            var allCategories = DirectorMod.Settings.userPresets
                .Select(p => p.category ?? "Default").Distinct().OrderBy(c => c).ToList();
            allCategories.Insert(0, "All");

            float rowHeight = 30f;
            float viewHeight = allCategories.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(innerRect, ref categoryScroll, viewRect);

            for (int i = 0; i < allCategories.Count; i++)
            {
                string cat = allCategories[i];
                Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);

                // 使用我们自己的高亮按钮
                if (DrawHighlightButton(rowRect, cat, selectedCategory == cat))
                {
                    selectedCategory = cat;
                    selectedPreset = null;
                }
            }

            Widgets.EndScrollView();
        }

        // ★★★ 辅助方法：手动绘制带高亮的按钮 ★★★
        private bool DrawHighlightButton(Rect rect, string label, bool highlighted)
        {
            if (highlighted)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            // 绘制文本
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.ContractedBy(5f), label);
            Text.Anchor = TextAnchor.UpperLeft;

            // 检测点击
            return Widgets.ButtonInvisible(rect);
        }

        // ★★★ 重写：同样使用 Listing_Standard + 手动 ScrollView ★★★
        private void DrawPresetArea(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(8f);

            // 主 Listing
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(innerRect);

            // 1. 预设列表区
            float listHeight = innerRect.height * 0.55f;
            Rect listOutRect = listing.GetRect(listHeight); // 用 Listing 预留空间

            var presetsToShow = DirectorMod.Settings.userPresets
                .Where(p => selectedCategory == "All" || (p.category ?? "Default") == selectedCategory)
                .ToList();

            float rowHeight = 28f;
            Rect viewRect = new Rect(0f, 0f, listOutRect.width - 16f, presetsToShow.Count * rowHeight);

            Widgets.BeginScrollView(listOutRect, ref presetScroll, viewRect);
            for (int i = 0; i < presetsToShow.Count; i++)
            {
                var p = presetsToShow[i];
                Rect row = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);
                if (selectedPreset == p) Widgets.DrawHighlightSelected(row);
                if (Widgets.ButtonInvisible(row)) selectedPreset = p;
                Widgets.Label(row.ContractedBy(4f), p.label);
            }
            Widgets.EndScrollView();

            listing.Gap(5f);

            // 2. 描述区
            float descHeight = innerRect.height - listing.CurHeight - 45f;
            Rect descRect = listing.GetRect(descHeight);
            Widgets.DrawMenuSection(descRect);
            if (selectedPreset != null)
            {
                Widgets.Label(descRect.ContractedBy(8f), $"{selectedPreset.personaText}");
            }

            listing.End();

            // 3. 底部按钮
            Rect bottomRow = new Rect(rect.x, rect.yMax - 40f, rect.width, 30f);
            float btnWidth = Mathf.Min(200f, (bottomRow.width - 10f) / 2f); // 按钮最大宽度 200px
            float startX = bottomRow.x + (bottomRow.width - (btnWidth * 2 + 10f)) / 2f; // 居中

            // 应用选中
            Rect applyRect = new Rect(startX, bottomRow.y, btnWidth, 30f);
            bool originalGUIState = GUI.enabled;
            if (selectedPreset == null) GUI.enabled = false;
            if (Widgets.ButtonText(applyRect, "RPD_Browser_ApplySelected".Translate()))
            {
                ApplyAndClose(selectedPreset);
            }
            GUI.enabled = originalGUIState;

            // 随机应用
            Rect randomRect = new Rect(applyRect.xMax + 10f, bottomRow.y, btnWidth, 30f);
            if (Widgets.ButtonText(randomRect, "RPD_Browser_ApplyRandom".Translate()))
            {
                // ★★★ 随机时也使用实时过滤的列表 ★★★
                if (presetsToShow.Any())
                {
                    ApplyAndClose(presetsToShow.RandomElement());
                }
            }
        }

        private void ApplyAndClose(CustomPreset preset)
        {
            // 1. 应用数据到 Hediff
            DirectorUtils.ApplyPersonalityToPawn(pawnToApply, new PersonalityData(preset.personaText, preset.chattiness));

            // 2. 刷新编辑器 UI
            DirectorUtils.SetWindowText(editorWindow, preset.personaText);

            LongEventHandler.ExecuteWhenFinished(() => this.Close());
        }
    }
}
