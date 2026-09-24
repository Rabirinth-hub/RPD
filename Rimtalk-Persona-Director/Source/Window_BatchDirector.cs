using RimTalk.Data; 
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class Window_BatchDirector : Window
    {
        private List<Pawn> cachedPawns = new List<Pawn>();
        private Vector2 scrollPos = Vector2.zero;

        // ★ 核心字典：Value 类型明确为 Task<PersonalityData>
        private Dictionary<Pawn, Task<PersonalityData>> generationTasks = new Dictionary<Pawn, Task<PersonalityData>>();

        // 批量任务状态
        private Task<PersonalityData> batchTask = null;
        private List<Pawn> batchTaskPawns = null;

        private HashSet<Pawn> selectedPawns = new HashSet<Pawn>();
        private string _searchText = "";

        // 滑动多选状态
        private bool _isDragging = false;
        private int _dragStartIndex = -1;
        private bool _dragState;

        private enum SortBy { Name, Race, Faction, Tag }
        private SortBy curSortBy = SortBy.Name;
        private bool sortAsc = true;
        private bool batchSendMode = false;

        public Window_BatchDirector()
        {
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = false;
            this.absorbInputAroundWindow = false;
            this.closeOnClickedOutside = false;

            if (DirectorMod.Settings.BatchFilters == null) DirectorMod.Settings.InitFilters();
            RefreshPawnCache();
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public override void PostClose()
        {
            // 清空任务引用，这样后台线程跑完后结果会被直接丢弃
            generationTasks.Clear();
            batchTask = null;
            batchTaskPawns = null;

            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (cachedPawns.RemoveAll(p => p == null || p.Destroyed) > 0)
            {
                RefreshPawnCache();
                selectedPawns.RemoveWhere(p => p == null || p.Destroyed);
            }
            UpdateAsyncTasks();

            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            // 顶部切换按钮
            Rect topRect = list.GetRect(30f);
            Widgets.Label(topRect.LeftPart(0.6f), "RPD_Batch_Title".Translate());
            if (Widgets.ButtonText(topRect.RightPart(0.4f), "RPD_Mode_SwitchToSimple".Translate()))
            {
                this.Close();
                Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }
            list.GapLine();

            DrawNotesSection(list);
            list.GapLine();

            DrawFilterSection(list);
            list.Gap(5f);

            Rect listRect = list.GetRect(inRect.height - list.CurHeight - 40f);
            DrawPawnList(listRect);

            list.Gap(5f);
            DrawGlobalActions(list);

            list.End();

            // 拖拽结束逻辑
            if (_isDragging && !UnityEngine.Input.GetMouseButton(0))
            {
                _isDragging = false;
                _dragStartIndex = -1;
            }
        }

        // ★ 核心：轮询任务并应用结果
        private void UpdateAsyncTasks()
        {
            // 1. 处理单体任务
            var keys = generationTasks.Keys.ToList();
            foreach (var pawn in keys)
            {
                var task = generationTasks[pawn];

                if (task.IsCompleted)
                {
                    generationTasks.Remove(pawn); // 移除任务

                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        var result = task.Result;
                        if (pawn != null && !pawn.Destroyed && result != null && !string.IsNullOrEmpty(result.Persona))
                        {
                            DirectorUtils.ApplyPersonalityToPawn(pawn, result);
                            Messages.Message("RPD_Message_GeneratedSuccess".Translate(pawn.LabelShortCap), MessageTypeDefOf.PositiveEvent, false);
                        }
                        else
                        {
                            // 失败或 Pawn 已不存在
                            if (pawn != null) Messages.Message("RPD_Message_GeneratedFail".Translate(pawn.LabelShortCap), MessageTypeDefOf.NegativeEvent, false);
                        }
                    }
                    else
                    {
                        Log.Error($"[Director] Task failed for {pawn.LabelShortCap}: {task.Exception}");
                    }
                }
            }

            // 2. 处理批量任务
            if (batchTask != null && batchTask.IsCompleted)
            {
                if (batchTask.Status == TaskStatus.RanToCompletion)
                {
                    var result = batchTask.Result;
                    if (result != null && !string.IsNullOrEmpty(result.Persona))
                    {
                        int count = DirectorUtils.ParseAndApplyBatchResult(batchTaskPawns, result.Persona);
                        if (count > 0)
                            Messages.Message("RPD_Message_BatchGeneratedSuccess".Translate(count), MessageTypeDefOf.PositiveEvent, false);
                        else
                            Messages.Message("RPD_Message_BatchGeneratedFail".Translate(), MessageTypeDefOf.NegativeEvent, false);
                    }
                    else
                    {
                        Messages.Message("RPD_Message_BatchGeneratedFail".Translate(), MessageTypeDefOf.NegativeEvent, false);
                    }
                }
                else
                {
                    Log.Error($"[Director] Batch Task failed: {batchTask.Exception}");
                }
                batchTask = null;
                batchTaskPawns = null;
            }
        }

        private void DrawNotesSection(Listing_Standard list)
        {
            Rect headerRect = list.GetRect(24f);
            Widgets.Label(headerRect.LeftPart(0.7f), "RPD_Batch_SceneNotes".Translate());
            if (Widgets.ButtonText(headerRect.RightPart(0.3f), "RPD_Button_Clear".Translate()))
            {
                DirectorMod.Settings.directorNotes = "";
            }
            DirectorMod.Settings.directorNotes = Widgets.TextArea(list.GetRect(60f), DirectorMod.Settings.directorNotes);
        }

        private void DrawFilterSection(Listing_Standard list)
        {
            Rect sectionRect = list.GetRect(60f);
            var filters = DirectorMod.Settings.BatchFilters;
            float toolY = sectionRect.y;
            float curX = sectionRect.x;

            // 全选/反选
            if (Widgets.ButtonText(new Rect(curX, toolY, 80f, 24f), "RPD_Batch_SelectAll".Translate()))
            {
                bool anyUnchecked = filters.Values.Any(v => !v);
                var keys = filters.Keys.ToList();
                foreach (var key in keys) filters[key] = anyUnchecked;
                RefreshPawnCache();
            }
            curX += 85f;

            // 搜索框
            Rect searchRect = new Rect(curX, toolY, 180f, 24f);
            string newSearch = Widgets.TextField(searchRect, _searchText);
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                RefreshPawnCache();
            }
            curX += 185f;

            // 刷新按钮
            if (Widgets.ButtonText(new Rect(curX, toolY, 80f, 24f), "RPD_Batch_Refresh".Translate()))
            {
                RefreshPawnCache();
            }
           
            // Prompt Dropdown
            float dropdownWidth = 150f;
            Rect promptRect = new Rect(sectionRect.xMax - dropdownWidth, toolY, dropdownWidth, 24f);

            // 获取当前 Label
            var settings = DirectorMod.Settings;
            if (settings.presets == null) settings.InitPresets();
            string currentLabel = settings.presets[settings.selectedPresetIndex].label;
            if (currentLabel.Length > 15) currentLabel = currentLabel.Substring(0, 12) + "..."; // 截断过长名字

            if (Widgets.ButtonText(promptRect, currentLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < settings.presets.Count; i++)
                {
                    if (i == 3) continue;
                    int index = i;
                    string name = settings.presets[i].label;
                    options.Add(new FloatMenuOption(name, () =>
                    {
                        settings.selectedPresetIndex = index;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(promptRect, "RPD_Tip_PromptSwitch".Translate());

            float checkY = toolY + 30f;
            var filterKeys = filters.Keys.ToList();
            if (!ModsConfig.AnomalyActive) filterKeys.Remove("Anomalies");

            int count = filterKeys.Count;
            float itemWidth = sectionRect.width / count;

            for (int i = 0; i < count; i++)
            {
                string key = filterKeys[i];
                bool value = filters[key];
                Rect itemRect = new Rect(sectionRect.x + i * itemWidth, checkY, itemWidth - 5f, 24f);

                bool newValue = value;
                Widgets.Checkbox(itemRect.x, itemRect.y, ref newValue);

                Rect labelRect = new Rect(itemRect.x + 28f, itemRect.y, itemRect.width - 28f, 24f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Tiny;
                Widgets.Label(labelRect, $"RPD_Filter_{key}".Translate());
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(labelRect)) newValue = !newValue;
                if (newValue != value)
                {
                    filters[key] = newValue;
                    RefreshPawnCache();
                }
            }
        }

        private void DrawPawnList(Rect outRect)
        {
            float colCheck = 24f;
            float colHead = 30f;
            float colName = 140f;
            float colRace = 140f;
            float colFaction = 240f;
            float colTag = 80f;
            float colActionWidth = 250f;

            Rect headerRect = new Rect(outRect.x, outRect.y, outRect.width - 16f, 24f);
            float curX = headerRect.x + colCheck + colHead;

            DrawHeaderCell(new Rect(curX, headerRect.y, colName, 24f), "RPD_Batch_Header_Name".Translate(), SortBy.Name);
            curX += colName;
            DrawHeaderCell(new Rect(curX, headerRect.y, colRace, 24f), "RPD_Batch_Header_Race".Translate(), SortBy.Race);
            curX += colRace;
            DrawHeaderCell(new Rect(curX, headerRect.y, colFaction, 24f), "RPD_Batch_Header_Faction".Translate(), SortBy.Faction);
            curX += colFaction;
            DrawHeaderCell(new Rect(curX, headerRect.y, colTag, 24f), "RPD_Batch_Header_Tag".Translate(), SortBy.Tag);

            Widgets.Label(new Rect(headerRect.width - colActionWidth, headerRect.y, colActionWidth, 24f), "RPD_Batch_Header_Actions".Translate());

            Rect listRect = new Rect(outRect.x, outRect.y + 24f, outRect.width, outRect.height - 24f);
            float viewHeight = cachedPawns.Count * 30f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);

            for (int i = 0; i < cachedPawns.Count; i++)
            {
                Pawn p = cachedPawns[i];

                Rect rowRect = new Rect(viewRect.x, i * 30f, viewRect.width, 28f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(rowRect);

                // Checkbox & Drag logic
                Rect checkColumnRect = new Rect(rowRect.x, rowRect.y, 30f, rowRect.height);
                bool isSelected = selectedPawns.Contains(p);
                Widgets.CheckboxDraw(checkColumnRect.x, checkColumnRect.y, isSelected, false, 24f);

                if (Mouse.IsOver(checkColumnRect))
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        _isDragging = true;
                        _dragStartIndex = i;
                        _dragState = !isSelected;
                        Event.current.Use();
                    }
                }

                if (_isDragging)
                {
                    // 只在鼠标当前悬停的那一行触发计算，节省性能
                    if (Mouse.IsOver(rowRect))
                    {
                        int start = Mathf.Min(i, _dragStartIndex);
                        int end = Mathf.Max(i, _dragStartIndex);

                        // 将起点到当前鼠标位置之间的所有项，都设置为与起始点击相同的状态
                        for (int j = start; j <= end; j++)
                        {
                            // 防止数组越界 (安全检查)
                            if (j >= 0 && j < cachedPawns.Count)
                            {
                                if (_dragState) selectedPawns.Add(cachedPawns[j]);
                                else selectedPawns.Remove(cachedPawns[j]);
                            }
                        }
                    }
                }

                float x = rowRect.x + colHead;

                // Icon
                Rect iconRect = new Rect(x, rowRect.y, 24f, 24f);
                Widgets.ThingIcon(iconRect, p);
                if (Widgets.ButtonInvisible(iconRect)) Find.WindowStack.Add(new Dialog_InfoCard(p));
                x += colHead;

                // Name
                Rect nameRect = new Rect(x, rowRect.y, colName - 5f, rowRect.height);
                string currentPersonality = DirectorUtils.GetCurrentPersonality(p);
                TooltipHandler.TipRegion(nameRect, $"RPD_Batch_PawnTooltip".Translate(currentPersonality));

                if (Widgets.ButtonInvisible(nameRect))
                {
                    if (p.Spawned && !p.Destroyed)
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(p);
                        CameraJumper.TryJump(p);
                    }
                    else { RefreshPawnCache(); }
                }
                Widgets.Label(nameRect, p.LabelShortCap);
                x += colName;

                // Race
                Rect raceRect = new Rect(x, rowRect.y, colRace - 5f, rowRect.height);
                DrawRaceWithIcon(raceRect, p);
                x += colRace;

                // Faction
                Rect factionRect = new Rect(x, rowRect.y, colFaction - 5f, rowRect.height);
                DrawFactionWithIcon(factionRect, p);
                x += colFaction;

                // Tag
                Rect tagRect = new Rect(x, rowRect.y, colTag - 5f, rowRect.height);
                GUI.color = GetTagColor(p);
                Widgets.Label(tagRect, GetPawnTag(p));
                GUI.color = Color.white;

                // Actions
                Rect actionRect = new Rect(viewRect.width - colActionWidth, rowRect.y, colActionWidth, rowRect.height);

                // ★ 修正后的逻辑：使用 ContainsKey 检查任务
                bool isSingleGen = generationTasks.ContainsKey(p) && !generationTasks[p].IsCompleted;
                bool isBatchGen = batchTask != null && !batchTask.IsCompleted && batchTaskPawns != null && batchTaskPawns.Contains(p);

                if (isSingleGen || isBatchGen)
                {
                    Widgets.Label(actionRect, "RPD_Batch_Status_Generating".Translate());
                }
                else
                {
                    // 将区域分为 3 份，间隔 5px
                    float btnWidth = (actionRect.width - 10f) / 3f;

                    Rect btn1 = new Rect(actionRect.x, actionRect.y, btnWidth, 24f);
                    Rect btn2 = new Rect(btn1.xMax + 5f, actionRect.y, btnWidth, 24f);
                    Rect btn3 = new Rect(btn2.xMax + 5f, actionRect.y, btnWidth, 24f);

                    // 按钮 3: Talk (直接调用 RimTalk)
                    // 允许对任何东西说话，只要它是 Pawn
                    if (Widgets.ButtonText(btn3, "RPD_Batch_Button_Talk".Translate()))
                    {
                        DirectorUtils.OpenRimTalkDialog(p);
                    }

                    // 按钮 1: Quick Gen
                    if (Widgets.ButtonText(btn1, "RPD_Batch_Button_QuickGen".Translate()))
                    {
                        // 只准备数据
                        string data = DirectorUtils.BuildCustomCharacterData(p);
                        string safeName = p.LabelShortCap;

                        // 调用 GeneratePersonalityTask，传入 Pawn 
                        generationTasks[p] = DirectorUtils.GeneratePersonalityTask(data, safeName, p);
                    }

                    // 按钮 2: Edit (Deep Edit)
                    if (Widgets.ButtonText(btn2, "RPD_Batch_Button_DeepEdit".Translate()))
                    {
                        Find.WindowStack.Add(new RimTalk.UI.PersonaEditorWindow(p));
                    }
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawRaceWithIcon(Rect rect, Pawn p)
        {
            Texture2D icon = null;
            string label = p.def.label;
            if (p.genes != null && p.genes.Xenotype != null) { icon = p.genes.XenotypeIcon; label = p.genes.XenotypeLabel; }
            if (icon == null) icon = p.def.uiIcon;

            Rect iconRect = new Rect(rect.x, rect.y, 24f, 24f);
            if (icon != null) GUI.DrawTexture(iconRect, icon);

            Rect labelRect = new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height);
            Widgets.Label(labelRect, label);
        }

        private void DrawFactionWithIcon(Rect rect, Pawn p)
        {
            if (p.Faction == null)
            {
                GUI.color = Color.gray; Widgets.Label(rect, "RPD_Faction_None".Translate()); GUI.color = Color.white; return;
            }
            Texture2D icon = p.Faction.def.FactionIcon;
            Color factionColor = p.Faction.Color;

            Rect iconRect = new Rect(rect.x, rect.y, 24f, 24f);
            if (icon != null) { GUI.color = factionColor; GUI.DrawTexture(iconRect, icon); GUI.color = Color.white; }

            Rect labelRect = new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height);
            GUI.color = factionColor; Widgets.Label(labelRect, p.Faction.Name); GUI.color = Color.white;
        }

        private void DrawHeaderCell(Rect rect, string label, SortBy sortBy)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            string text = label;
            if (curSortBy == sortBy) text += sortAsc ? " ▲" : " ▼";
            Widgets.Label(rect, text);
            if (Widgets.ButtonInvisible(rect))
            {
                if (curSortBy == sortBy) sortAsc = !sortAsc;
                else { curSortBy = sortBy; sortAsc = true; }
                RefreshPawnCache();
            }
        }

        private string GetPawnTag(Pawn p)
        {
            if (ModsConfig.AnomalyActive)
            {
                if (p.IsMutant || p.IsCreepJoiner || p.def.race.IsAnomalyEntity) return "RPD_Tag_Anomaly".Translate();
            }
            if (p.RaceProps.Animal) return "RPD_Tag_Animal".Translate();
            if (p.RaceProps.IsMechanoid) return "RPD_Tag_Mech".Translate();
            if (p.IsSlaveOfColony) return "RPD_Tag_Slave".Translate();
            if (p.IsPrisonerOfColony) return "RPD_Tag_Prisoner".Translate();
            if (p.IsFreeColonist) return "RPD_Tag_Colonist".Translate();
            if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) return "RPD_Tag_Enemy".Translate();
            if (p.Faction != Faction.OfPlayer) return "RPD_Tag_Visitor".Translate();
            return "RPD_Tag_Other".Translate();
        }

        private Color GetTagColor(Pawn p)
        {
            if (ModsConfig.AnomalyActive && (p.IsMutant || p.IsCreepJoiner || p.def.race.IsAnomalyEntity))
                return new Color(0.6f, 0.2f, 0.8f);
            if (p.IsSlaveOfColony) return new Color(0.8f, 0.8f, 0.5f);
            if (p.IsPrisonerOfColony) return new Color(1f, 0.7f, 0.2f);
            if (p.IsFreeColonist) return Color.white;
            if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) return new Color(1f, 0.4f, 0.4f);
            if (p.Faction != Faction.OfPlayer) return new Color(0.4f, 0.8f, 1f);
            return Color.gray;
        }

        private string GetRaceSortKey(Pawn p)
        {
            if (p.genes != null && p.genes.Xenotype != null) return p.genes.XenotypeLabel;
            return p.def.label;
        }

        private void DrawGlobalActions(Listing_Standard list)
        {
            Rect rect = list.GetRect(30f);
            Rect batchButtonRect = rect.LeftPart(0.7f);
            if (Widgets.ButtonText(batchButtonRect, "RPD_Batch_Button_BatchGen".Translate(selectedPawns.Count)))
            {
                if (batchSendMode)
                {
                    // 批量模式
                    if (batchTask == null && selectedPawns.Any())
                    {
                        batchTaskPawns = selectedPawns.ToList();
                        string combinedData = DirectorUtils.BuildCombinedCharacterData(batchTaskPawns);
                        Pawn representative = batchTaskPawns.FirstOrDefault();
                        batchTask = DirectorUtils.GenerateBatchPersonaTask(combinedData, representative);
                    }
                }
                else
                {
                    // 单体循环模式
                    foreach (var pawn in selectedPawns)
                    {
                        if (!generationTasks.ContainsKey(pawn))
                        {
                            string data = DirectorUtils.BuildCustomCharacterData(pawn);
                            string safeName = pawn.LabelShortCap;
                            generationTasks[pawn] = DirectorUtils.GeneratePersonalityTask(data, safeName, pawn);
                        }
                    }
                }
            }

            Rect modeButtonRect = rect.RightPart(0.25f);
            string modeLabelKey = batchSendMode ? "RPD_Batch_Button_ModeBatch" : "RPD_Batch_Button_ModeSingle";
            if (Widgets.ButtonText(modeButtonRect, modeLabelKey.Translate())) { batchSendMode = !batchSendMode; }
            TooltipHandler.TipRegion(modeButtonRect, "RPD_Batch_Button_ModeTooltip".Translate());
        }
        private void RefreshPawnCache()
        {
            cachedPawns.Clear();
            Map map = Find.CurrentMap;
            if (map == null || map.mapPawns == null) return;
            var filters = DirectorMod.Settings.BatchFilters;
            if (filters == null) return;

            var pawnsToShow = new List<Pawn>();
            var allMapPawns = map.mapPawns.AllPawns
                .Where(p =>
                    p != null &&
                    !p.Dead &&
                    p.def != null &&
                    p.Spawned &&
                    (p.Faction == Faction.OfPlayer || !p.Map.fogGrid.IsFogged(p.Position))
                ).ToList();

            if (filters.TryGetValue("Colonists", out bool c) && c)
                pawnsToShow.AddRange(allMapPawns.Where(p =>
                    p.IsFreeColonist && 
                    !p.IsSlave &&      
                    !p.IsPrisoner &&   
                    !p.IsMutant &&
                    !p.IsCreepJoiner));

            if (filters.TryGetValue("Prisoners", out bool pr) && pr)
                pawnsToShow.AddRange(allMapPawns.Where(p => p.IsPrisonerOfColony && !p.IsMutant));

            if (filters.TryGetValue("Slaves", out bool sl) && sl)
                pawnsToShow.AddRange(allMapPawns.Where(p => p.IsSlaveOfColony && !p.IsMutant));

            if (filters.TryGetValue("Visitors", out bool vis) && vis)
                pawnsToShow.AddRange(allMapPawns.Where(p =>
                    p.RaceProps.Humanlike &&
                    p.Faction != null &&
                    !p.Faction.IsPlayer &&
                    !p.Faction.HostileTo(Faction.OfPlayer) &&
                    !p.IsPrisonerOfColony &&
                    !p.IsSlaveOfColony &&
                    !p.IsMutant &&
                    !p.IsCreepJoiner &&
                    !p.def.race.IsAnomalyEntity));

            if (filters.TryGetValue("Enemies", out bool ene) && ene)
                pawnsToShow.AddRange(allMapPawns.Where(p =>
                    p.RaceProps.Humanlike &&
                    p.Faction != null &&
                    p.Faction.HostileTo(Faction.OfPlayer) &&
                    !p.IsPrisonerOfColony &&  
                    !p.IsSlaveOfColony &&
                    !p.IsMutant &&
                    !p.def.race.IsAnomalyEntity));

            if (ModsConfig.AnomalyActive && filters.TryGetValue("Anomalies", out bool ano) && ano)
            {
                pawnsToShow.AddRange(allMapPawns.Where(p => p.IsMutant || p.IsCreepJoiner || p.def.race.IsAnomalyEntity));
            }

            if (filters.TryGetValue("Animals", out bool ani) && ani) pawnsToShow.AddRange(allMapPawns.Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer));
            if (filters.TryGetValue("Mechs", out bool mec) && mec) pawnsToShow.AddRange(allMapPawns.Where(p => p.RaceProps.IsMechanoid && p.Faction == Faction.OfPlayer));

            var distinctPawns = pawnsToShow.Distinct();

            switch (curSortBy)
            {
                case SortBy.Name:
                    cachedPawns = sortAsc
                    ? distinctPawns.OrderBy(p => p.LabelShortCap).ToList()
                    : distinctPawns.OrderByDescending(p => p.LabelShortCap).ToList();
                    break;
                case SortBy.Race: cachedPawns = sortAsc ? distinctPawns.OrderBy(p => GetRaceSortKey(p)).ToList() : distinctPawns.OrderByDescending(p => GetRaceSortKey(p)).ToList(); break;
                case SortBy.Faction: cachedPawns = sortAsc ? distinctPawns.OrderBy(p => p.Faction?.Name ?? "ZZZ").ToList() : distinctPawns.OrderByDescending(p => p.Faction?.Name ?? "AAA").ToList(); break;
                case SortBy.Tag: cachedPawns = sortAsc ? distinctPawns.OrderBy(p => GetPawnTag(p)).ToList() : distinctPawns.OrderByDescending(p => GetPawnTag(p)).ToList(); break;
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                cachedPawns = cachedPawns.Where(p => p.Label.IndexOf(_searchText, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            selectedPawns.RemoveWhere(p => p == null || p.Destroyed || !p.Spawned);
        }
    }
}