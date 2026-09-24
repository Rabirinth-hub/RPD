using RimTalk.Data;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RimPersonaDirector
{
    internal sealed class DirectorPawnRoleState
    {
        public Pawn Pawn;
        public string Category;
        public string Faction;
        public string HostFaction;
        public string OriginFaction;
        public string ExtraHomeFaction;
    }

    internal static class DirectorAutoEvolveTriggerService
    {
        private const int DuplicateWindowTicks = 120;
        private const int RecentKeyRetentionTicks = 60000;

        private static readonly Dictionary<string, int> RecentTriggerTicks =
            new Dictionary<string, int>();
        private static readonly Dictionary<int, HashSet<string>> TraitBaselines =
            new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<int, PendingRoleChange> PendingRoleChanges =
            new Dictionary<int, PendingRoleChange>();
        private static readonly Dictionary<string, PendingBirth> PendingBirths =
            new Dictionary<string, PendingBirth>();
        private static bool _cleanupRegistered;

        private sealed class PendingRoleChange
        {
            public Pawn Pawn;
            public DirectorPawnRoleState OldState;
        }

        private sealed class PendingBirth
        {
            public Pawn Mother;
            public Pawn Father;
            public Pawn GeneticMother;
            public bool FormalParents;
            public int LastTick;
            public readonly HashSet<int> ChildIds = new HashSet<int>();
            public int Boys;
            public int Girls;
            public int UnknownSex;
        }

        public static DirectorPawnRoleState CaptureRoleState(Pawn pawn)
        {
            return new DirectorPawnRoleState
            {
                Pawn = pawn,
                Category = AutoGenCategoryCatalog.GetPawnCategory(pawn),
                Faction = FormatFaction(pawn?.Faction),
                HostFaction = FormatFaction(pawn?.guest?.HostFaction),
                OriginFaction = FormatFaction(pawn?.guest?.SlaveFaction),
                ExtraHomeFaction = FormatFaction(
                    pawn != null && pawn.HasExtraHomeFaction()
                        ? pawn.GetExtraHomeFaction()
                        : null)
            };
        }

        public static void PublishRoleChange(
            Pawn pawn,
            DirectorPawnRoleState oldState,
            DirectorPawnRoleState newState)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || pawn == null
                || PawnGenerator.IsBeingGenerated(pawn)
                || oldState == null
                || newState == null
                || string.IsNullOrEmpty(oldState.Category)
                || string.IsNullOrEmpty(newState.Category)
                || string.Equals(oldState.Category, newState.Category, StringComparison.Ordinal))
            {
                return;
            }

            EnsureCleanupRegistered();
            if (!PendingRoleChanges.ContainsKey(pawn.thingIDNumber))
            {
                PendingRoleChanges[pawn.thingIDNumber] = new PendingRoleChange
                {
                    Pawn = pawn,
                    OldState = oldState
                };
            }
        }

        public static void FlushRoleChanges()
        {
            if (PendingRoleChanges.Count == 0) return;

            var pending = new List<PendingRoleChange>(PendingRoleChanges.Values);
            PendingRoleChanges.Clear();
            foreach (PendingRoleChange change in pending)
            {
                DispatchRoleChange(change.Pawn, change.OldState);
            }
        }

        private static void DispatchRoleChange(Pawn pawn, DirectorPawnRoleState oldState)
        {
            DirectorSettings settings = DirectorMod.Settings;
            DirectorPawnRoleState newState = CaptureRoleState(pawn);
            string oldCategory = oldState?.Category;
            string newCategory = newState.Category;
            if (!DirectorFeatureGate.ExperimentalEnabled
                || settings == null
                || pawn == null
                || !IsPawnOnMap(pawn)
                || string.IsNullOrEmpty(newCategory)
                || string.Equals(oldCategory, newCategory, StringComparison.Ordinal))
            {
                return;
            }

            settings.EnsureAutoGenCategories();
            AutoGenCategory category;
            if (!settings.autoGenCategories.TryGetValue(newCategory, out category)
                || category == null
                || !category.onlyOnRoleChange)
            {
                return;
            }

            EnsureCleanupRegistered();
            string key = "role:" + pawn.thingIDNumber + ":"
                + oldCategory + ":" + newCategory;
            if (WasRecentlyAccepted(key)) return;

            if (!settings.autoGenEnabled
                || !category.enabled
                || !DirectorAutoGenManager.ShouldAutoGenerateExistingPawn(pawn))
            {
                bool queued = Find.World?.GetComponent<DirectorAutoEvolveManager>()?.EnqueueTriggeredPawn(
                    pawn,
                    key,
                    BuildRoleChangeContext(oldState, newState)) ?? false;
                if (queued) MarkAccepted(key);
                return;
            }

            bool generated = Find.World?.GetComponent<DirectorAutoGenManager>()?.EnqueueRoleChangePawn(
                pawn,
                oldCategory,
                newCategory,
                BuildRoleChangeContext(oldState, newState)) ?? false;
            if (generated) MarkAccepted(key);
        }

        private static string BuildRoleChangeContext(
            DirectorPawnRoleState oldState,
            DirectorPawnRoleState newState)
        {
            string pawnName = newState?.Pawn?.LabelShortCap
                ?? oldState?.Pawn?.LabelShortCap
                ?? "Unknown pawn";
            return "Role transition for " + pawnName + " (with faction context):\n- Before: "
                + DescribeRoleState(oldState)
                + "\n- After: "
                + DescribeRoleState(newState);
        }

        private static string DescribeRoleState(DirectorPawnRoleState state)
        {
            if (state == null) return "unknown";
            var parts = new List<string>
            {
                "role=" + (state.Category ?? "Unknown"),
                "faction=" + state.Faction
            };
            if (state.HostFaction != "None") parts.Add("host faction=" + state.HostFaction);
            if (state.OriginFaction != "None" && state.OriginFaction != state.Faction)
                parts.Add("origin faction=" + state.OriginFaction);
            if (state.ExtraHomeFaction != "None" && state.ExtraHomeFaction != state.Faction)
                parts.Add("extra home faction=" + state.ExtraHomeFaction);
            return string.Join("; ", parts);
        }

        private static string FormatFaction(Faction faction)
        {
            if (faction == null) return "None";
            string name = faction.Name ?? faction.def?.label ?? "Unknown";
            string defName = faction.def?.defName ?? "Unknown";
            string relation;
            if (faction.IsPlayer) relation = "player faction";
            else if (Faction.OfPlayer == null) relation = "non-player faction";
            else relation = faction.PlayerRelationKind.ToString().ToLowerInvariant() + " to player";
            return name + " (" + defName + "; " + relation + ")";
        }

        public static void PublishMarriage(Pawn first, Pawn second)
        {
            if (!(DirectorMod.Settings?.autoEvolveOnMarriage ?? false)) return;
            PublishPair(
                first,
                second,
                "marriage",
                pawn => BuildPartnerContext(pawn, first, second, "Married"));
        }

        public static void PublishBreakup(Pawn first, Pawn second)
        {
            if (!(DirectorMod.Settings?.autoEvolveOnBreakup ?? false)) return;
            PublishPair(
                first,
                second,
                "breakup",
                pawn => BuildPartnerContext(pawn, first, second, "Divorced or broke up with"));
        }

        public static void PublishBirth(
            Pawn mother,
            Pawn father,
            Pawn child,
            Pawn geneticMother = null,
            Pawn birther = null)
        {
            if (!(DirectorMod.Settings?.autoEvolveOnBirth ?? false)
                || (!IsAutoEvolveEligiblePawn(mother)
                    && !IsAutoEvolveEligiblePawn(father)))
            {
                return;
            }

            EnsureCleanupRegistered();
            int tick = CurrentTick;
            int motherId = (birther ?? mother)?.thingIDNumber ?? 0;
            int fatherId = father?.thingIDNumber ?? 0;
            string pairKey = motherId + ":" + fatherId;
            PendingBirth pending;
            if (!PendingBirths.TryGetValue(pairKey, out pending))
            {
                pending = new PendingBirth { Mother = mother, Father = father };
                PendingBirths.Add(pairKey, pending);
            }
            pending.LastTick = tick;
            if (geneticMother != null) pending.GeneticMother = geneticMother;
            if (child != null)
            {
                if (mother != null) pending.Mother = mother;
                if (father != null) pending.Father = father;
            }
            pending.FormalParents = AreFormalPartners(
                pending.GeneticMother ?? pending.Mother,
                pending.Father);

            if (child == null)
            {
                return;
            }

            if (!pending.ChildIds.Add(child.thingIDNumber)) return;
            if (child.gender == Gender.Male) pending.Boys++;
            else if (child.gender == Gender.Female) pending.Girls++;
            else pending.UnknownSex++;
        }

        public static void FlushBirths()
        {
            if (PendingBirths.Count == 0) return;
            if (!(DirectorMod.Settings?.autoEvolveOnBirth ?? false))
            {
                PendingBirths.Clear();
                return;
            }
            int tick = CurrentTick;
            var readyKeys = new List<string>();
            foreach (KeyValuePair<string, PendingBirth> pair in PendingBirths)
            {
                if (tick - pair.Value.LastTick < 2) continue;
                PendingBirth birth = pair.Value;
                string key = "birth:" + pair.Key + ":" + birth.LastTick;
                PublishOptional(
                    birth.Mother,
                    key + ":mother",
                    BuildBirthContext(birth, birth.Mother),
                    true);
                if (birth.FormalParents && birth.Father != birth.Mother)
                {
                    PublishOptional(
                        birth.Father,
                        key + ":father",
                        BuildBirthContext(birth, birth.Father),
                        true);
                }
                readyKeys.Add(pair.Key);
            }
            foreach (string key in readyKeys) PendingBirths.Remove(key);
        }

        private static string BuildBirthContext(PendingBirth birth, Pawn recipient)
        {
            string context;
            if (birth.ChildIds.Count == 0)
            {
                context = "Became a parent following a birth; child details were unavailable.";
            }
            else
            {
                context = "Became a parent following a birth. Newborns: "
                    + birth.ChildIds.Count + " total ("
                    + birth.Boys + " male, "
                    + birth.Girls + " female, "
                    + birth.UnknownSex + " unknown sex).";
            }

            if (birth.FormalParents)
            {
                bool forFather = recipient == birth.Father;
                Pawn other = forFather
                    ? birth.GeneticMother ?? birth.Mother
                    : birth.Father;
                context += " Other parent ("
                    + (forFather ? "mother" : "father") + "): "
                    + other.LabelShortCap
                    + "; status=" + DirectorUtils.GetPawnSocialStatus(other)
                    + "; faction=" + FormatFaction(other.Faction) + ".";
            }
            else
            {
                context += " Other parent's identity is unknown for this update.";
            }
            if (birth.GeneticMother?.def != null
                && birth.Father?.def != null
                && birth.GeneticMother.def != birth.Father.def)
            {
                context += birth.FormalParents
                    ? " The genetic parents have different races: "
                        + birth.GeneticMother.def.label + " / " + birth.Father.def.label + "."
                    : " The recorded genetic parents have different races.";
            }
            return context;
        }

        private static bool AreFormalPartners(Pawn mother, Pawn father)
        {
            if (mother?.relations == null || father == null) return false;
            return mother.relations.DirectRelationExists(PawnRelationDefOf.Lover, father)
                || mother.relations.DirectRelationExists(PawnRelationDefOf.Fiance, father)
                || mother.relations.DirectRelationExists(PawnRelationDefOf.Spouse, father);
        }

        public static void PublishDirectFamilyDeath(Pawn survivor, Pawn deceased)
        {
            if (!(DirectorMod.Settings?.autoEvolveOnDirectFamilyDeath ?? false)
                || survivor == null
                || deceased == null)
            {
                return;
            }

            PublishOptional(
                survivor,
                "family-death:" + survivor.thingIDNumber + ":" + deceased.thingIDNumber,
                "Direct family member " + deceased.LabelShortCap + " died.",
                true);
        }

        public static void PublishTraitAdded(Pawn pawn, Trait trait)
        {
            if (!(DirectorMod.Settings?.autoEvolveOnTraitAdded ?? false)
                || pawn == null
                || PawnGenerator.IsBeingGenerated(pawn)
                || trait?.def == null
                || trait.Suppressed
                || !IsAutoEvolveEligiblePawn(pawn))
            {
                return;
            }

            EnsureCleanupRegistered();
            string defName = trait.def.defName;
            HashSet<string> baseline;
            if (!TraitBaselines.TryGetValue(pawn.thingIDNumber, out baseline))
            {
                baseline = CurrentActiveTraits(pawn);
                TraitBaselines[pawn.thingIDNumber] = baseline;
            }
            baseline.Add(defName);

            string traitDescription = trait.CurrentData?.description;
            if (!string.IsNullOrWhiteSpace(traitDescription))
            {
                traitDescription = traitDescription
                    .Formatted(pawn.Named("PAWN"))
                    .AdjustedFor(pawn)
                    .Resolve()
                    .StripTags()
                    .Trim();
            }

            PublishOptional(
                pawn,
                "trait:" + pawn.thingIDNumber + ":" + defName,
                "Gained a new trait: " + trait.Label + "."
                    + (string.IsNullOrWhiteSpace(traitDescription)
                        ? ""
                        : " Description: " + traitDescription),
                true);
        }

        public static void ScanTraitChanges()
        {
            if (!(DirectorMod.Settings?.autoEvolveOnTraitAdded ?? false))
            {
                TraitBaselines.Clear();
                return;
            }

            EnsureCleanupRegistered();
            var seenPawnIds = new HashSet<int>();
            foreach (Pawn pawn in PawnsFinder.AllMaps_Spawned)
            {
                if (!IsAutoEvolveEligiblePawn(pawn)
                    || pawn.story?.traits?.allTraits == null)
                {
                    continue;
                }

                seenPawnIds.Add(pawn.thingIDNumber);
                HashSet<string> current = CurrentActiveTraits(pawn);
                HashSet<string> previous;
                if (!TraitBaselines.TryGetValue(pawn.thingIDNumber, out previous))
                {
                    TraitBaselines[pawn.thingIDNumber] = current;
                    continue;
                }

                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    if (trait?.def == null
                        || trait.Suppressed
                        || previous.Contains(trait.def.defName))
                    {
                        continue;
                    }
                    PublishTraitAdded(pawn, trait);
                }
                TraitBaselines[pawn.thingIDNumber] = current;
            }

            var departedPawnIds = new List<int>();
            foreach (int pawnId in TraitBaselines.Keys)
            {
                if (!seenPawnIds.Contains(pawnId)) departedPawnIds.Add(pawnId);
            }
            foreach (int pawnId in departedPawnIds) TraitBaselines.Remove(pawnId);
        }

        private static void PublishPair(
            Pawn first,
            Pawn second,
            string eventId,
            Func<Pawn, string> contextFactory)
        {
            if (first == null || second == null) return;
            int low = Math.Min(first.thingIDNumber, second.thingIDNumber);
            int high = Math.Max(first.thingIDNumber, second.thingIDNumber);
            string pairKey = eventId + ":" + low + ":" + high;
            PublishOptional(
                first,
                pairKey + ":" + first.thingIDNumber,
                contextFactory(first),
                true);
            if (second != first)
            {
                PublishOptional(
                    second,
                    pairKey + ":" + second.thingIDNumber,
                    contextFactory(second),
                    true);
            }
        }

        private static void PublishOptional(
            Pawn pawn,
            string key,
            string context,
            bool requirePlayerRelevant)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || pawn == null
                || (requirePlayerRelevant && !IsAutoEvolveEligiblePawn(pawn)))
            {
                return;
            }

            EnsureCleanupRegistered();
            if (WasRecentlyAccepted(key)) return;
            bool queued = Find.World?.GetComponent<DirectorAutoEvolveManager>()?.EnqueueTriggeredPawn(
                pawn,
                key,
                context) ?? false;
            if (queued) MarkAccepted(key);
        }

        internal static bool IsPlayerRelevantPawn(Pawn pawn)
        {
            if (!IsPawnOnMap(pawn)
                || pawn.Destroyed
                || pawn.Dead
                || pawn.RaceProps == null
                || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (pawn.Faction?.IsPlayer == true
                || pawn.IsPrisonerOfColony
                || pawn.IsSlaveOfColony)
            {
                return true;
            }

            return false;
        }

        internal static bool IsAutoEvolveEligiblePawn(Pawn pawn)
        {
            return IsPawnOnMap(pawn) && IsAutoEvolveCandidatePawn(pawn);
        }

        internal static bool IsAutoEvolveCandidatePawn(Pawn pawn)
        {
            return pawn != null
                && HasPlayerFaction()
                && !pawn.Destroyed
                && !pawn.Dead
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike;
        }

        internal static bool IsPawnOnMap(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.Map != null;
        }

        internal static bool HasPlayerFaction()
        {
            FactionManager manager = Find.FactionManager;
            if (manager?.AllFactionsListForReading == null) return false;
            foreach (Faction faction in manager.AllFactionsListForReading)
            {
                if (faction?.IsPlayer == true) return true;
            }
            return false;
        }

        private static HashSet<string> CurrentActiveTraits(Pawn pawn)
        {
            var result = new HashSet<string>();
            if (pawn?.story?.traits?.allTraits == null) return result;
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait?.def != null && !trait.Suppressed)
                {
                    result.Add(trait.def.defName);
                }
            }
            return result;
        }

        private static string BuildPartnerContext(
            Pawn pawn,
            Pawn first,
            Pawn second,
            string eventText)
        {
            Pawn other = pawn == first ? second : first;
            string context = eventText + " " + (other?.LabelShortCap ?? "another pawn") + ".";
            if (other == null || DirectorUtils.UsesGlobalPlayerPersona(other))
                return context;

            string otherPersona = PersonaService.GetPersonality(other)?.Trim();
            if (string.IsNullOrWhiteSpace(otherPersona)) return context;

            return context + "\n[Other pawn's current persona (relationship context only; update only the subject pawn)]\n"
                + otherPersona;
        }

        private static bool WasRecentlyAccepted(string key)
        {
            int now = CurrentTick;
            int lastTick;
            return RecentTriggerTicks.TryGetValue(key, out lastTick)
                && now - lastTick <= DuplicateWindowTicks;
        }

        private static void MarkAccepted(string key)
        {
            int now = CurrentTick;
            RecentTriggerTicks[key] = now;
            if (RecentTriggerTicks.Count > 256)
            {
                var expired = new List<string>();
                foreach (KeyValuePair<string, int> pair in RecentTriggerTicks)
                {
                    if (now - pair.Value > RecentKeyRetentionTicks) expired.Add(pair.Key);
                }
                foreach (string expiredKey in expired) RecentTriggerTicks.Remove(expiredKey);
            }
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        private static void EnsureCleanupRegistered()
        {
            if (_cleanupRegistered) return;
            DirectorFeatureGate.RegisterTransientCleanup(ClearTransient);
            _cleanupRegistered = true;
        }

        private static void ClearTransient()
        {
            RecentTriggerTicks.Clear();
            TraitBaselines.Clear();
            PendingRoleChanges.Clear();
            PendingBirths.Clear();
        }

        internal static void ResetForNewWorld()
        {
            ClearTransient();
        }
    }
}
