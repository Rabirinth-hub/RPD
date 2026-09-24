using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    internal sealed class PawnRosterList
    {
        private enum SortBy { Name, Race, Faction, Tag }

        private readonly Func<IEnumerable<Pawn>> _sourceProvider;
        private readonly Func<Pawn, bool> _eligibility;
        private readonly List<string> _filterKeys;
        private readonly List<Pawn> _cachedPawns = new List<Pawn>();
        private readonly HashSet<Pawn> _selectedPawns = new HashSet<Pawn>();
        private readonly HashSet<Pawn> _sourceSnapshot = new HashSet<Pawn>();
        private readonly Dictionary<Pawn, string> _sourceCategories = new Dictionary<Pawn, string>();
        private Vector2 _scrollPosition;
        private string _searchText = "";
        private bool _isDragging;
        private int _dragStartIndex = -1;
        private bool _dragState;
        private SortBy _sortBy = SortBy.Name;
        private bool _sortAscending = true;

        public PawnRosterList(
            Func<IEnumerable<Pawn>> sourceProvider,
            IEnumerable<string> filterKeys = null,
            Func<Pawn, bool> eligibility = null)
        {
            _sourceProvider = sourceProvider ?? (() => Enumerable.Empty<Pawn>());
            _eligibility = eligibility;
            _filterKeys = (filterKeys ?? new[]
            {
                "Colonists", "Prisoners", "Slaves", "Visitors",
                "Enemies", "Animals", "Mechs", "Anomalies"
            }).Distinct().ToList();

            DirectorMod.Settings.InitFilters();
            Refresh();
        }

        public IReadOnlyList<Pawn> VisiblePawns => _cachedPawns;
        public HashSet<Pawn> SelectedPawns => _selectedPawns;
        public List<Pawn> SelectedVisiblePawns => _cachedPawns.Where(_selectedPawns.Contains).ToList();

        public void RefreshIfInvalid()
        {
            List<Pawn> currentSource = GetEligibleSource().Distinct().ToList();
            bool sourceChanged = currentSource.Count != _sourceSnapshot.Count
                || currentSource.Any(pawn => !_sourceSnapshot.Contains(pawn))
                || currentSource.Any(pawn =>
                    !_sourceCategories.TryGetValue(pawn, out string category)
                    || category != GetFilterCategory(pawn));
            if (sourceChanged || _cachedPawns.Any(pawn => pawn == null || pawn.Destroyed || !pawn.Spawned))
                Refresh();
            _selectedPawns.RemoveWhere(pawn =>
                pawn == null || pawn.Destroyed || !pawn.Spawned || !_sourceSnapshot.Contains(pawn));
        }

        public void Refresh()
        {
            _cachedPawns.Clear();
            List<Pawn> source = GetEligibleSource().Distinct().ToList();
            _sourceSnapshot.Clear();
            _sourceCategories.Clear();
            foreach (Pawn pawn in source)
            {
                _sourceSnapshot.Add(pawn);
                _sourceCategories[pawn] = GetFilterCategory(pawn);
            }
            IEnumerable<Pawn> pawns = source;

            Dictionary<string, bool> filters = DirectorMod.Settings.BatchFilters;
            pawns = pawns.Where(pawn => IsCategoryEnabled(pawn, filters));

            switch (_sortBy)
            {
                case SortBy.Race:
                    pawns = _sortAscending
                        ? pawns.OrderBy(GetRaceSortKey)
                        : pawns.OrderByDescending(GetRaceSortKey);
                    break;
                case SortBy.Faction:
                    pawns = _sortAscending
                        ? pawns.OrderBy(pawn => pawn.Faction?.Name ?? "ZZZ")
                        : pawns.OrderByDescending(pawn => pawn.Faction?.Name ?? "AAA");
                    break;
                case SortBy.Tag:
                    pawns = _sortAscending
                        ? pawns.OrderBy(GetPawnTag)
                        : pawns.OrderByDescending(GetPawnTag);
                    break;
                default:
                    pawns = _sortAscending
                        ? pawns.OrderBy(pawn => pawn.LabelShortCap)
                        : pawns.OrderByDescending(pawn => pawn.LabelShortCap);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                pawns = pawns.Where(pawn =>
                    pawn.Label.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _cachedPawns.AddRange(pawns.Distinct());
            _selectedPawns.RemoveWhere(pawn =>
                pawn == null || pawn.Destroyed || !pawn.Spawned || !_sourceSnapshot.Contains(pawn));
        }

        private IEnumerable<Pawn> GetEligibleSource()
        {
            IEnumerable<Pawn> source = _sourceProvider() ?? Enumerable.Empty<Pawn>();
            source = source.Where(IsUsablePawn);
            return _eligibility == null ? source : source.Where(_eligibility);
        }

        public void DrawFilters(Rect rect, Action<Rect> drawRight = null, float rightWidth = 0f)
        {
            Dictionary<string, bool> filters = DirectorMod.Settings.BatchFilters;
            float x = rect.x;
            if (Widgets.ButtonText(new Rect(x, rect.y, 80f, 24f), "RPD_Batch_SelectAll".Translate()))
            {
                bool enable = _filterKeys.Any(key => IsAvailableFilter(key) && !filters[key]);
                foreach (string key in _filterKeys.Where(IsAvailableFilter)) filters[key] = enable;
                Refresh();
            }
            x += 85f;

            Rect searchRect = new Rect(x, rect.y, 180f, 24f);
            string search = Widgets.TextField(searchRect, _searchText);
            if (!string.Equals(search, _searchText, StringComparison.Ordinal))
            {
                _searchText = search;
                Refresh();
            }
            TooltipHandler.TipRegion(searchRect, "RPD_Batch_SearchTip".Translate());
            x += 185f;

            if (Widgets.ButtonText(new Rect(x, rect.y, 80f, 24f), "RPD_Batch_Refresh".Translate()))
                Refresh();

            if (drawRight != null && rightWidth > 0f)
                drawRight(new Rect(rect.xMax - rightWidth, rect.y, rightWidth, 24f));

            List<string> visibleKeys = _filterKeys.Where(IsAvailableFilter).ToList();
            if (visibleKeys.Count == 0) return;
            float width = rect.width / visibleKeys.Count;
            for (int i = 0; i < visibleKeys.Count; i++)
            {
                string key = visibleKeys[i];
                Rect item = new Rect(rect.x + i * width, rect.y + 30f, width - 5f, 24f);
                bool enabled = filters[key];
                Widgets.Checkbox(new Vector2(item.x, item.y), ref enabled);
                Rect label = new Rect(item.x + 28f, item.y, item.width - 28f, 24f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, ("RPD_Filter_" + key).Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(label)) enabled = !enabled;
                if (enabled != filters[key])
                {
                    filters[key] = enabled;
                    Refresh();
                }
            }
        }

        public void DrawList(
            Rect outRect,
            float trailingWidth,
            Action<Rect> drawTrailingHeader,
            Action<Pawn, Rect> drawTrailingRow)
        {
            const float checkWidth = 30f;
            const float iconWidth = 30f;
            const float nameWidth = 140f;
            const float raceWidth = 120f;
            const float factionWidth = 180f;
            const float tagWidth = 80f;

            Rect header = new Rect(outRect.x, outRect.y, outRect.width - 16f, 24f);
            bool allSelected = _cachedPawns.Count > 0 && _cachedPawns.All(_selectedPawns.Contains);
            bool selectAll = allSelected;
            Widgets.Checkbox(new Vector2(header.x + 3f, header.y), ref selectAll);
            if (selectAll != allSelected)
            {
                foreach (Pawn pawn in _cachedPawns)
                {
                    if (selectAll) _selectedPawns.Add(pawn);
                    else _selectedPawns.Remove(pawn);
                }
            }

            float x = header.x + checkWidth + iconWidth;
            DrawHeaderCell(new Rect(x, header.y, nameWidth, 24f), "RPD_Batch_Header_Name".Translate(), SortBy.Name); x += nameWidth;
            DrawHeaderCell(new Rect(x, header.y, raceWidth, 24f), "RPD_Batch_Header_Race".Translate(), SortBy.Race); x += raceWidth;
            DrawHeaderCell(new Rect(x, header.y, factionWidth, 24f), "RPD_Batch_Header_Faction".Translate(), SortBy.Faction); x += factionWidth;
            DrawHeaderCell(new Rect(x, header.y, tagWidth, 24f), "RPD_Batch_Header_Tag".Translate(), SortBy.Tag);
            drawTrailingHeader?.Invoke(new Rect(header.xMax - trailingWidth, header.y, trailingWidth, 24f));

            Rect scroll = new Rect(outRect.x, outRect.y + 24f, outRect.width, outRect.height - 24f);
            Rect view = new Rect(0f, 0f, scroll.width - 16f, _cachedPawns.Count * 30f);
            Widgets.BeginScrollView(scroll, ref _scrollPosition, view);
            for (int i = 0; i < _cachedPawns.Count; i++)
            {
                Pawn pawn = _cachedPawns[i];
                Rect row = new Rect(0f, i * 30f, view.width, 28f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                DrawSelection(row, i, pawn);

                float rowX = row.x + checkWidth;
                Rect icon = new Rect(rowX, row.y, 24f, 24f);
                Widgets.ThingIcon(icon, pawn);
                if (Widgets.ButtonInvisible(icon)) Find.WindowStack.Add(new Dialog_InfoCard(pawn));
                rowX += iconWidth;

                Rect name = new Rect(rowX, row.y, nameWidth - 5f, row.height);
                TooltipHandler.TipRegion(name,
                    "RPD_Batch_PawnTooltip".Translate(DirectorUtils.GetCurrentPersonality(pawn)));
                if (Widgets.ButtonInvisible(name)) JumpToPawn(pawn);
                Widgets.Label(name, pawn.LabelShortCap);
                rowX += nameWidth;

                DrawRaceWithIcon(new Rect(rowX, row.y, raceWidth - 5f, row.height), pawn);
                rowX += raceWidth;
                DrawFactionWithIcon(new Rect(rowX, row.y, factionWidth - 5f, row.height), pawn);
                rowX += factionWidth;

                GUI.color = GetTagColor(pawn);
                Widgets.Label(new Rect(rowX, row.y, tagWidth - 5f, row.height), GetPawnTag(pawn));
                GUI.color = Color.white;

                drawTrailingRow?.Invoke(
                    pawn,
                    new Rect(view.xMax - trailingWidth, row.y, trailingWidth, row.height));
            }
            Widgets.EndScrollView();
        }

        public void ClearSelection() => _selectedPawns.Clear();

        public void EndDragIfReleased()
        {
            if (_isDragging && !Input.GetMouseButton(0))
            {
                _isDragging = false;
                _dragStartIndex = -1;
            }
        }

        private void DrawSelection(Rect row, int index, Pawn pawn)
        {
            Rect check = new Rect(row.x, row.y, 30f, row.height);
            bool selected = _selectedPawns.Contains(pawn);
            Widgets.CheckboxDraw(check.x, check.y, selected, false, 24f);
            if (Mouse.IsOver(check) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _isDragging = true;
                _dragStartIndex = index;
                _dragState = !selected;
                Event.current.Use();
            }

            if (!_isDragging || !Mouse.IsOver(row)) return;
            int start = Mathf.Min(index, _dragStartIndex);
            int end = Mathf.Max(index, _dragStartIndex);
            for (int i = start; i <= end && i < _cachedPawns.Count; i++)
            {
                if (_dragState) _selectedPawns.Add(_cachedPawns[i]);
                else _selectedPawns.Remove(_cachedPawns[i]);
            }
        }

        private void DrawHeaderCell(Rect rect, string label, SortBy sortBy)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            string text = label;
            if (_sortBy == sortBy) text += _sortAscending ? " ▲" : " ▼";
            Widgets.Label(rect, text);
            if (!Widgets.ButtonInvisible(rect)) return;
            if (_sortBy == sortBy) _sortAscending = !_sortAscending;
            else
            {
                _sortBy = sortBy;
                _sortAscending = true;
            }
            Refresh();
        }

        private bool IsCategoryEnabled(Pawn pawn, IDictionary<string, bool> filters)
        {
            string category = GetFilterCategory(pawn);
            return category != null
                && _filterKeys.Contains(category)
                && filters.TryGetValue(category, out bool enabled)
                && enabled;
        }

        private static string GetFilterCategory(Pawn pawn)
        {
            if (ModsConfig.AnomalyActive
                && (pawn.IsMutant || pawn.IsCreepJoiner || pawn.def.race.IsAnomalyEntity)) return "Anomalies";
            if (pawn.RaceProps.Animal) return pawn.Faction == Faction.OfPlayer ? "Animals" : null;
            if (pawn.RaceProps.IsMechanoid) return pawn.Faction == Faction.OfPlayer ? "Mechs" : null;
            if (pawn.IsSlaveOfColony) return "Slaves";
            if (pawn.IsPrisonerOfColony) return "Prisoners";
            if (pawn.IsFreeColonist && !pawn.IsSlave && !pawn.IsPrisoner) return "Colonists";
            if (pawn.RaceProps.Humanlike && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return "Enemies";
            if (pawn.RaceProps.Humanlike && pawn.Faction != null && pawn.Faction != Faction.OfPlayer) return "Visitors";
            if (pawn.RaceProps.Humanlike) return "Other";
            return null;
        }

        private bool IsAvailableFilter(string key)
        {
            return key != "Anomalies" || ModsConfig.AnomalyActive;
        }

        private static bool IsUsablePawn(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && !pawn.Dead && pawn.def != null && pawn.Spawned;
        }

        private void JumpToPawn(Pawn pawn)
        {
            if (pawn.Spawned && !pawn.Destroyed)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(pawn);
                CameraJumper.TryJump(pawn);
            }
            else Refresh();
        }

        private static void DrawRaceWithIcon(Rect rect, Pawn pawn)
        {
            Texture2D icon = null;
            string label = pawn.def.label;
            if (pawn.genes?.Xenotype != null)
            {
                icon = pawn.genes.XenotypeIcon;
                label = pawn.genes.XenotypeLabel;
            }
            if (icon == null) icon = pawn.def.uiIcon;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x, rect.y, 24f, 24f), icon);
            Widgets.Label(new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height), label);
        }

        private static void DrawFactionWithIcon(Rect rect, Pawn pawn)
        {
            if (pawn.Faction == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "RPD_Faction_None".Translate());
                GUI.color = Color.white;
                return;
            }
            Texture2D icon = pawn.Faction.def.FactionIcon;
            Color color = pawn.Faction.Color;
            if (icon != null)
            {
                GUI.color = color;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 24f, 24f), icon);
                GUI.color = Color.white;
            }
            GUI.color = color;
            Widgets.Label(new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height), pawn.Faction.Name);
            GUI.color = Color.white;
        }

        private static string GetPawnTag(Pawn pawn)
        {
            if (ModsConfig.AnomalyActive && (pawn.IsMutant || pawn.IsCreepJoiner || pawn.def.race.IsAnomalyEntity)) return "RPD_Tag_Anomaly".Translate();
            if (pawn.RaceProps.Animal) return "RPD_Tag_Animal".Translate();
            if (pawn.RaceProps.IsMechanoid) return "RPD_Tag_Mech".Translate();
            if (pawn.IsSlaveOfColony) return "RPD_Tag_Slave".Translate();
            if (pawn.IsPrisonerOfColony) return "RPD_Tag_Prisoner".Translate();
            if (pawn.IsFreeColonist) return "RPD_Tag_Colonist".Translate();
            if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return "RPD_Tag_Enemy".Translate();
            if (pawn.Faction != Faction.OfPlayer) return "RPD_Tag_Visitor".Translate();
            return "RPD_Tag_Other".Translate();
        }

        private static Color GetTagColor(Pawn pawn)
        {
            if (ModsConfig.AnomalyActive && (pawn.IsMutant || pawn.IsCreepJoiner || pawn.def.race.IsAnomalyEntity)) return new Color(0.6f, 0.2f, 0.8f);
            if (pawn.IsSlaveOfColony) return new Color(0.8f, 0.8f, 0.5f);
            if (pawn.IsPrisonerOfColony) return new Color(1f, 0.7f, 0.2f);
            if (pawn.IsFreeColonist) return Color.white;
            if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return new Color(1f, 0.4f, 0.4f);
            if (pawn.Faction != Faction.OfPlayer) return new Color(0.4f, 0.8f, 1f);
            return Color.gray;
        }

        private static string GetRaceSortKey(Pawn pawn)
        {
            return pawn.genes?.Xenotype != null ? pawn.genes.XenotypeLabel : pawn.def.label;
        }
    }
}
