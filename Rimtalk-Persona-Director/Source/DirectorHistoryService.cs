using RimTalk.Data;
using System;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorHistoryService
    {
        [ThreadStatic]
        private static int _suppressionDepth;

        public static bool IsSuppressed => _suppressionDepth > 0;

        public static bool ApplyWithoutHistory(Pawn pawn, string personality)
        {
            if (pawn == null
                || pawn.Destroyed
                || string.IsNullOrWhiteSpace(personality))
            {
                return false;
            }

            string target = personality.Trim();
            _suppressionDepth++;
            try
            {
                PersonaService.SetPersonality(pawn, target);
            }
            finally
            {
                _suppressionDepth--;
            }

            string applied = PersonaService.GetPersonality(pawn) ?? "";
            return string.Equals(applied.Trim(), target, StringComparison.Ordinal);
        }

        public static bool ApplyWithHistory(
            Pawn pawn,
            string personality,
            string historyContext,
            bool updateTimestamp)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || pawn == null
                || pawn.Destroyed
                || !pawn.Spawned
                || pawn.Map == null
                || DirectorUtils.UsesGlobalPlayerPersona(pawn)
                || string.IsNullOrWhiteSpace(personality))
            {
                return false;
            }

            string target = personality.Trim();
            string previous = PersonaService.GetPersonality(pawn) ?? "";
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            if (string.Equals(previous, target, StringComparison.Ordinal))
            {
                if (world != null)
                {
                    world.MarkAsProcessed(pawn);
                    if (updateTimestamp)
                    {
                        world.SetTimestamp(
                            pawn,
                            DirectorUtils.BuildCustomCharacterData(pawn, true, false));
                    }
                }
                return true;
            }

            _suppressionDepth++;
            try
            {
                PersonaService.SetPersonality(pawn, target);
            }
            finally
            {
                _suppressionDepth--;
            }

            string applied = PersonaService.GetPersonality(pawn) ?? "";
            if (!string.Equals(applied.Trim(), target, StringComparison.Ordinal)) return false;

            if (world != null)
            {
                world.MarkAsProcessed(pawn);
                if (!string.IsNullOrWhiteSpace(previous))
                {
                    world.AddHistory(pawn, previous, historyContext);
                }

                if (updateTimestamp)
                {
                    world.SetTimestamp(
                        pawn,
                        DirectorUtils.BuildCustomCharacterData(pawn, true, false));
                }
            }

            return true;
        }
    }
}
