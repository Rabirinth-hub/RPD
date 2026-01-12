using HarmonyLib;
using RimTalk.UI;
using RimWorld;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    
    [HarmonyPatch(typeof(PersonaEditorWindow), "DoWindowContents")]
    public static class Patch_PersonaEditorWindow_DirectorFeatures
    {
        // 状态变量
        private static Task<string> evolveTask = null;
        private static string evolveResult = null;
        private static Pawn evolvingPawn = null;

        private static void ClearEvolveState()
        {
            evolveTask = null;
            evolveResult = null;
            evolvingPawn = null;
        }

        public static void Postfix(Rect inRect, Window __instance)
        {
            Pawn pawn = (Pawn)AccessTools.Field(typeof(PersonaEditorWindow), "_pawn").GetValue(__instance);
            if (pawn == null) return;

            // 检查并应用结果
            if (evolveResult != null && evolvingPawn == pawn)
            {
                string currentText = DirectorUtils.GetWindowText(__instance);
                string newText = $"{currentText}\n\n[Development]: {evolveResult}";
                DirectorUtils.SetWindowText(__instance, newText);

                // 清理结果，防止重复应用
                evolveResult = null;
                evolvingPawn = null;
            }

            // --- 1. 布局参数 ---
            float footerY = inRect.y + 267f;
            float buttonWidth = 80f;
            float buttonHeight = 24f;
            float spacing = 5f;

            // --- 2. ★★★ 预先计算所有按钮的 Rect ★★★ ---
            float startX = inRect.x;
            Rect noteRect = new Rect(startX, footerY, buttonWidth, buttonHeight);
            Rect evolveRect = new Rect(noteRect.xMax + spacing, footerY, buttonWidth, buttonHeight);
            Rect timeRect = new Rect(evolveRect.xMax + spacing, footerY, buttonWidth, buttonHeight);

            // A. 绘制 Edit Notes (如果开启)
            if (DirectorMod.Settings.Context.Inc_DirectorNotes)
            {
                string currentNotes = DirectorMod.Settings.directorNotes;
                bool hasNotes = !string.IsNullOrEmpty(currentNotes);
                Color oldColor = GUI.color;
                if (hasNotes) GUI.color = Color.cyan;

                if (Widgets.ButtonText(noteRect, "RPD_Button_EditNotes".Translate()))
                {
                    Find.WindowStack.Add(new Window_DirectorNotesEditor());
                }
                GUI.color = oldColor;

                if (Mouse.IsOver(noteRect))
                {
                    string tooltip = hasNotes
                        ? $"{"RPD_Tip_CurrentNotes".Translate()}:\n{currentNotes}"
                        : "RPD_Tip_NoNotes".Translate().ToString();
                    TooltipHandler.TipRegion(noteRect, tooltip);
                }
            }

            if (DirectorMod.Settings.enableEvolveFeature)
            {
                // --- Evolve 按钮 ---
                bool isEvolving = evolveTask != null && !evolveTask.IsCompleted;

                if (isEvolving)
                {
                    Widgets.ButtonText(evolveRect, "RPD_Batch_Status_Generating".Translate(), active: false);
                }
                else
                {
                    if (Widgets.ButtonText(evolveRect, "RPD_Button_Evolve".Translate()))
                    {
                        ClearEvolveState();

                        evolvingPawn = pawn;
                        var (request, originalPersona) = DirectorUtils.PrepareEvolve(pawn, __instance);
                        if (request != null)
                        {
                            // ★★★ 2. 后台线程：只负责执行网络请求 ★★★
                            evolveTask = Task.Run(() => DirectorUtils.ExecuteEvolve(request, originalPersona));

                            // ★★★ 3. 任务完成后，将结果放入 evolveResult ★★★
                            evolveTask.ContinueWith(task =>
                            {
                                if (task.IsCompleted && !task.IsFaulted)
                                {
                                    // 这里拿到的 task.Result 就是拼接好的完整新文本
                                    evolveResult = task.Result;
                                }
                                evolveTask = null;
                            });
                        }
                        else
                        {
                            // 准备阶段就失败了，直接恢复按钮
                            ClearEvolveState();
                        }
                    }
                    TooltipHandler.TipRegion(evolveRect, "RPD_Tip_Evolve".Translate());
                }

                // C. 绘制 Set Time (最右)
                var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
                if (worldComp != null)
                {
                    int lastTick = worldComp.GetLastEvolveTick(pawn);
                    string timeLabel = "RPD_Button_SetTime".Translate();
                    string tooltip = "RPD_Tip_SetTime_Empty".Translate();

                    if (lastTick > 0)
                    {
                        int days = (GenTicks.TicksGame - lastTick) / 60000;
                        timeLabel = "RPD_Button_TimeAgo".Translate(days);
                        long ageBioYears = worldComp.GetLastEvolveBioAgeTicks(pawn) / 3600000;
                        tooltip = "RPD_Tip_SetTime_Info".Translate(days, ageBioYears);
                    }

                    if (Widgets.ButtonText(timeRect, timeLabel))
                    {
                        string snapshot = DirectorUtils.BuildCustomCharacterData(pawn, true);
                        worldComp.SetTimestamp(pawn, snapshot);
                        Messages.Message("RPD_Msg_TimestampUpdated".Translate(), MessageTypeDefOf.PositiveEvent, false);
                    }
                    TooltipHandler.TipRegion(timeRect, tooltip);
                }
            }
        }
    }
} 