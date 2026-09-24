using System;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    internal sealed class Window_AutoGenContextSettings : Window
    {
        private readonly string _categoryId;
        private readonly ContextSettings _context;
        private readonly Action _onChanged;
        private readonly string _initialState;

        public Window_AutoGenContextSettings(
            string categoryId,
            ContextSettings context,
            Action onChanged)
        {
            _categoryId = categoryId;
            _context = context;
            _onChanged = onChanged;
            _initialState = GetState(context);
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(900f, 330f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(inRect.x, inRect.y, inRect.width, 30f),
                "RPD_AutoGen_ContextTitle".Translate(
                    ("RPD_AutoGen_Category" + _categoryId).Translate()));
            Text.Font = GameFont.Small;
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f));
            DirectorContextSettingsDrawer.Draw(listing, _context);
            listing.End();
        }

        public override void PostClose()
        {
            if (!string.Equals(_initialState, GetState(_context), StringComparison.Ordinal))
                _onChanged?.Invoke();
            base.PostClose();
        }

        private static string GetState(ContextSettings context)
        {
            if (context == null) return "";
            return string.Join("", new[]
            {
                context.Inc_Basic, context.Inc_Race, context.Inc_Race_Desc,
                context.Inc_Genes, context.Inc_Genes_Desc,
                context.Inc_Backstory, context.Inc_Backstory_Desc,
                context.Inc_Relations, context.Inc_DirectorNotes,
                context.Inc_Traits, context.Inc_Traits_Desc,
                context.Inc_Ideology, context.Inc_Ideology_Desc,
                context.Inc_Skills, context.Inc_Skills_Desc,
                context.Inc_Health, context.Inc_Health_Desc,
                context.Inc_Equipment, context.Inc_Inventory,
                context.Inc_RimPsyche, context.Inc_RimPsyche_All,
                context.Inc_Memories, context.Inc_CommonKnowledge,
                context.Inc_DataComparison
            });
        }
    }
}
