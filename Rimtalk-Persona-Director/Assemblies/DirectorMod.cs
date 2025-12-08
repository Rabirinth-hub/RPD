using UnityEngine;
using Verse;
using System;

namespace RimPersonaDirector
{
    public class DirectorMod : Mod
    {
        public static DirectorSettings Settings;
        private Vector2 scrollPosition = Vector2.zero;

        public DirectorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DirectorSettings>();
        }

        public override string SettingsCategory() => "RimTalk: Persona Director";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            Text.Font = GameFont.Medium;
            list.Label("RPD_Settings_Title".Translate()); 
            Text.Font = GameFont.Small;
            list.Gap(8f);

            list.CheckboxLabeled("RPD_Setting_DebugLog".Translate(), ref Settings.EnableDebugLog, "RPD_Setting_DebugLogDesc".Translate()); 
            list.GapLine();

            DrawContextFilterSettings(list);

            list.GapLine();

            DrawPromptSection(list, inRect);

            list.End();
            Settings.Write();
        }

        private void DrawPromptSection(Listing_Standard list, Rect inRect)
        {
            Rect headerRect = list.GetRect(24f);
            Widgets.Label(headerRect.LeftPart(0.7f), "RPD_Prompt_Label".Translate()); 
            if (Widgets.ButtonText(headerRect.RightPart(0.3f), "RPD_Button_Reset".Translate())) 
            {
                Settings.activePrompt = DirectorSettings.DefaultUserPrompt;
            }
            list.Gap(2f);

            int userChar = Settings.activePrompt?.Length ?? 0;
            int hiddenChar = DirectorSettings.HiddenTechnicalPrompt.Length;
            int totalChar = userChar + hiddenChar;
            int estTokens = (int)(totalChar / 2.5f);

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            list.Label("RPD_Label_JsonTip".Translate()); 
            list.Label("RPD_Label_TokenEst".Translate(estTokens)); 
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            list.Gap(5f);

            float remainingHeight = inRect.height - list.CurHeight - 10f;
            Rect outRect = list.GetRect(remainingHeight);
            Rect viewRect = new Rect(0, 0, outRect.width - 16f, 1500f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Settings.activePrompt = Widgets.TextArea(viewRect, Settings.activePrompt);
            Widgets.EndScrollView();
        }

        private void DrawContextFilterSettings(Listing_Standard listingStandard)
        {
            var ctx = Settings.Context;

            listingStandard.Label("RPD_Setting_FilterLabel".Translate()); 
            listingStandard.Gap(5f);

            const float columnGap = 30f;
            float columnWidth = (listingStandard.ColumnWidth - columnGap) / 2;
            Rect positionRect = listingStandard.GetRect(0f);

            // --- 左栏 ---
            Rect leftColumnRect = new Rect(positionRect.x, positionRect.y, columnWidth, 9999f);
            Listing_Standard leftListing = new Listing_Standard { ColumnWidth = columnWidth };
            leftListing.Begin(leftColumnRect);

            DrawHeader(leftListing, "RPD_Group_Bio".Translate()); 
            DrawFilterRow(leftListing, "RPD_Filter_Basic".Translate(), ref ctx.Inc_Basic);
            DrawFilterRow(leftListing, "RPD_Filter_Race".Translate(), ref ctx.Inc_Race, ref ctx.Inc_Race_Desc);
            DrawFilterRow(leftListing, "RPD_Filter_Genes".Translate(), ref ctx.Inc_Genes, ref ctx.Inc_Genes_Desc, "RPD_Tip_GenesDesc".Translate()); 
            DrawFilterRow(leftListing, "RPD_Filter_Backstory".Translate(), ref ctx.Inc_Backstory, ref ctx.Inc_Backstory_Desc);
            DrawFilterRow(leftListing, "RPD_Filter_Relations".Translate(), ref ctx.Inc_Relations);
            DrawFilterRow(leftListing, "RPD_Filter_DirectorNotes".Translate(), ref ctx.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate()); 

            leftListing.End();

            // --- 右栏 ---
            Rect rightColumnRect = new Rect(leftColumnRect.xMax + columnGap, positionRect.y, columnWidth, 9999f);
            Listing_Standard rightListing = new Listing_Standard { ColumnWidth = columnWidth };
            rightListing.Begin(rightColumnRect);

            DrawHeader(rightListing, "RPD_Group_Traits".Translate()); 
            DrawFilterRow(rightListing, "RPD_Filter_Traits".Translate(), ref ctx.Inc_Traits, ref ctx.Inc_Traits_Desc);
            DrawFilterRow(rightListing, "RPD_Filter_Ideology".Translate(), ref ctx.Inc_Ideology, ref ctx.Inc_Ideology_Desc);
            DrawFilterRow(rightListing, "RPD_Filter_Skills".Translate(), ref ctx.Inc_Skills, ref ctx.Inc_Skills_Desc);
            DrawFilterRow(rightListing, "RPD_Filter_Health".Translate(), ref ctx.Inc_Health, ref ctx.Inc_Health_Desc);

            rightListing.End();

            listingStandard.Gap(Mathf.Max(leftListing.CurHeight, rightListing.CurHeight));
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
            if (descTooltip != null) TooltipHandler.TipRegion(descRect, descTooltip);

            GUI.enabled = true;
        }

        private void DrawFilterRow(Listing_Standard list, string label, ref bool nameSwitch, string tooltip = null)
        {
            list.CheckboxLabeled(label, ref nameSwitch, tooltip);
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