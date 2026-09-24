using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorContextSettingsDrawer
    {
        private static bool? _rimPsycheLoaded;
        private static bool? _expandMemoryLoaded;

        private static bool RimPsycheLoaded
        {
            get
            {
                if (!_rimPsycheLoaded.HasValue)
                    _rimPsycheLoaded = AccessTools.TypeByName("Maux36.RimPsyche.CompPsyche") != null;
                return _rimPsycheLoaded.Value;
            }
        }

        private static bool ExpandMemoryLoaded
        {
            get
            {
                if (!_expandMemoryLoaded.HasValue)
                    _expandMemoryLoaded = ModsConfig.IsActive("cj.rimtalk.expandmemory");
                return _expandMemoryLoaded.Value;
            }
        }

        public static void Draw(Listing_Standard listing, ContextSettings context)
        {
            Draw(listing, context, RimPsycheLoaded, false, true);
        }

        public static void Draw(
            Listing_Standard listing,
            ContextSettings context,
            bool rimPsycheLoaded,
            bool includeDataComparison = true,
            bool includeRimPsycheAll = true)
        {
            if (context == null) return;
            listing.Label("RPD_Setting_FilterLabel".Translate());
            listing.Gap(5f);

            const float gap = 10f;
            float columnWidth = (listing.ColumnWidth - gap * 2f) / 3f;
            Rect origin = listing.GetRect(0f);

            Listing_Standard biology = BeginColumn(origin.x, origin.y, columnWidth);
            DrawHeader(biology, "RPD_Group_Bio".Translate());
            DrawFilterRow(biology, "RPD_Filter_Basic".Translate(), ref context.Inc_Basic);
            DrawFilterRow(biology, "RPD_Filter_Race".Translate(), ref context.Inc_Race, ref context.Inc_Race_Desc);
            DrawFilterRow(biology, "RPD_Filter_Genes".Translate(), ref context.Inc_Genes, ref context.Inc_Genes_Desc, "RPD_Tip_GenesDesc".Translate());
            DrawFilterRow(biology, "RPD_Filter_Backstory".Translate(), ref context.Inc_Backstory, ref context.Inc_Backstory_Desc);
            DrawFilterRow(biology, "RPD_Filter_Relations".Translate(), ref context.Inc_Relations);
            DrawFilterRow(biology, "RPD_Filter_DirectorNotes".Translate(), ref context.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());
            biology.End();

            Listing_Standard traits = BeginColumn(origin.x + columnWidth + gap, origin.y, columnWidth);
            DrawHeader(traits, "RPD_Group_Traits".Translate());
            DrawFilterRow(traits, "RPD_Filter_Traits".Translate(), ref context.Inc_Traits, ref context.Inc_Traits_Desc);
            DrawFilterRow(traits, "RPD_Filter_Ideology".Translate(), ref context.Inc_Ideology, ref context.Inc_Ideology_Desc);
            DrawFilterRow(traits, "RPD_Filter_Skills".Translate(), ref context.Inc_Skills, ref context.Inc_Skills_Desc);
            DrawFilterRow(traits, "RPD_Filter_Health".Translate(), ref context.Inc_Health, ref context.Inc_Health_Desc);
            DrawFilterRow(traits, "RPD_Filter_Equipment".Translate(), ref context.Inc_Equipment);
            DrawFilterRow(traits, "RPD_Filter_Inventory".Translate(), ref context.Inc_Inventory);
            traits.End();

            Listing_Standard external = BeginColumn(origin.x + (columnWidth + gap) * 2f, origin.y, columnWidth);
            DrawHeader(external, "RPD_Group_ExternalData".Translate());
            if (includeDataComparison)
                DrawFilterRow(external, "RPD_Filter_DataComparison".Translate(), ref context.Inc_DataComparison);
            if (rimPsycheLoaded)
            {
                if (includeRimPsycheAll)
                    DrawFilterRow(external, "RPD_Filter_RimPsyche".Translate(), ref context.Inc_RimPsyche, ref context.Inc_RimPsyche_All, "RPD_Tip_RimPsyche".Translate());
                else
                    DrawFilterRow(external, "RPD_Filter_RimPsyche".Translate(), ref context.Inc_RimPsyche, "RPD_Tip_RimPsyche".Translate());
            }
            if (ExpandMemoryLoaded)
            {
                DrawFilterRow(external, "RPD_Filter_Memories".Translate(), ref context.Inc_Memories);
                DrawFilterRow(external, "RPD_Filter_CommonKnowledge".Translate(), ref context.Inc_CommonKnowledge);
            }
            external.End();

            listing.Gap(Mathf.Max(biology.CurHeight, Mathf.Max(traits.CurHeight, external.CurHeight)));
        }

        private static Listing_Standard BeginColumn(float x, float y, float width)
        {
            Listing_Standard column = new Listing_Standard { ColumnWidth = width };
            column.Begin(new Rect(x, y, width, 9999f));
            return column;
        }

        private static void DrawFilterRow(
            Listing_Standard list,
            string label,
            ref bool enabled,
            ref bool includeDescription,
            string tooltip = null)
        {
            Rect row = list.GetRect(24f);
            Rect main = new Rect(row.x, row.y, row.width - 29f, row.height);
            Widgets.CheckboxLabeled(main, label, ref enabled);
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            Widgets.Checkbox(new Vector2(row.xMax - 24f, row.y), ref includeDescription, 24f, !enabled);
            GUI.enabled = previous;
            if (tooltip != null) TooltipHandler.TipRegion(row, tooltip);
        }

        private static void DrawFilterRow(
            Listing_Standard list,
            string label,
            ref bool enabled,
            string tooltip = null)
        {
            Rect row = list.GetRect(24f);
            Widgets.CheckboxLabeled(row, label, ref enabled);
            if (tooltip != null) TooltipHandler.TipRegion(row, tooltip);
        }

        private static void DrawHeader(Listing_Standard list, string text)
        {
            GUI.color = Color.yellow;
            list.Label("━━ " + text + " ━━");
            GUI.color = Color.white;
            list.Gap(2f);
        }
    }
}
