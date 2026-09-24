using RimWorld;
using Verse;

namespace RimPersonaDirector
{
    public sealed class AutoGenCategory : IExposable
    {
        public string categoryName;
        public bool enabled;
        public bool syncWithModSettings = true;
        public int presetIndex = 1;
        public string advancedPreset = "";
        public bool onlyOnRoleChange;
        public ContextSettings customContext = new ContextSettings();

        public void ExposeData()
        {
            Scribe_Values.Look(ref categoryName, "categoryName");
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref syncWithModSettings, "syncWithModSettings", true);
            Scribe_Values.Look(ref presetIndex, "presetIndex", 1);
            Scribe_Values.Look(ref advancedPreset, "advancedPreset", "");
            Scribe_Values.Look(ref onlyOnRoleChange, "onlyOnRoleChange", false);
            Scribe_Deep.Look(ref customContext, "customContext");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (advancedPreset == null)
                {
                    advancedPreset = "";
                }

                if (customContext == null)
                {
                    customContext = new ContextSettings();
                }
            }
        }

        public ContextSettings GetEffectiveContext(ContextSettings globalContext)
        {
            ContextSettings source = syncWithModSettings ? globalContext : customContext;
            return source?.Copy() ?? new ContextSettings();
        }
    }

    internal static class AutoGenCategoryCatalog
    {
        public const string Colonist = "Colonist";
        public const string Prisoner = "Prisoner";
        public const string Slave = "Slave";
        public const string Visitor = "Visitor";
        public const string Enemy = "Enemy";
        public const string Other = "Other";

        public static readonly string[] OrderedIds =
        {
            Colonist,
            Prisoner,
            Slave,
            Visitor,
            Enemy,
            Other
        };

        public static AutoGenCategory CreateDefault(string categoryId)
        {
            var category = new AutoGenCategory
            {
                categoryName = categoryId,
                enabled = false,
                syncWithModSettings = true,
                presetIndex = 1
            };

            category.customContext.Inc_DirectorNotes = false;
            return category;
        }

        public static string GetPawnCategory(Pawn pawn)
        {
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return null;
            if (pawn.IsFreeColonist && !pawn.IsSlave && !pawn.IsPrisoner) return Colonist;
            if (pawn.IsPrisonerOfColony) return Prisoner;
            if (pawn.IsSlaveOfColony) return Slave;
            if (pawn.Faction != null && !pawn.Faction.IsPlayer)
            {
                return pawn.Faction.HostileTo(Faction.OfPlayer) ? Enemy : Visitor;
            }

            return Other;
        }

        public static bool IsExternalCategory(string categoryId)
        {
            return categoryId == Visitor || categoryId == Enemy || categoryId == Other;
        }
    }
}
