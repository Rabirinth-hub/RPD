using RimTalk.Data;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public sealed class Window_PersonaHistory : Window
    {
        private readonly Pawn _pawn;
        private readonly List<PersonaHistoryRecord> _history = new List<PersonaHistoryRecord>();
        private int _currentIndex;
        private bool _loaded;
        private Vector2 _contextScroll;
        private Vector2 _beforePersonaScroll;
        private Vector2 _afterPersonaScroll;

        public Window_PersonaHistory(Pawn pawn)
        {
            _pawn = pawn;
            doCloseX = true;
            draggable = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            if (!DirectorFeatureGate.ExperimentalEnabled
                || _pawn == null
                || _pawn.Destroyed
                || DirectorUtils.UsesGlobalPlayerPersona(_pawn))
            {
                Close();
                return;
            }

            EnsureLoaded();
            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, 35f));

            if (_history.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(
                    new Rect(inRect.x, inRect.y + 50f, inRect.width, 30f),
                    "RPD_History_NoRecords".Translate());
                GUI.color = Color.white;
                return;
            }

            PersonaHistoryRecord record = _history[_currentIndex];
            Rect content = new Rect(inRect.x, inRect.y + 48f, inRect.width, inRect.height - 52f);
            float gap = 10f;
            float leftWidth = content.width * 0.4f;
            Rect left = new Rect(content.x, content.y, leftWidth, content.height);
            Rect right = new Rect(
                left.xMax + gap,
                content.y,
                content.width - leftWidth - gap,
                content.height);
            float personaHeight = (right.height - gap) / 2f;
            Rect beforeRect = new Rect(right.x, right.y, right.width, personaHeight);
            Rect afterRect = new Rect(
                right.x,
                beforeRect.yMax + gap,
                right.width,
                personaHeight);

            DrawContext(left, record);
            DrawPersona(
                beforeRect,
                "RPD_History_BeforePersona".Translate().ToString(),
                record.personaText,
                ref _beforePersonaScroll,
                true,
                "RPD_History_RestoreBeforeConfirm");

            bool latestRecord = _currentIndex == 0;
            string afterPersona = latestRecord
                ? PersonaService.GetPersonality(_pawn)
                : _history[_currentIndex - 1].personaText;
            DrawPersona(
                afterRect,
                (latestRecord
                    ? "RPD_History_CurrentPersona".Translate()
                    : "RPD_History_AfterPersona".Translate()).ToString(),
                afterPersona,
                ref _afterPersonaScroll,
                !latestRecord,
                "RPD_History_RestoreAfterConfirm");
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            if (world != null) _history.AddRange(world.GetHistory(_pawn));
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(rect.LeftPart(0.42f), "RPD_History_Title".Translate(_pawn.LabelShortCap));
            Text.Font = GameFont.Small;

            if (_history.Count == 0) return;
            Rect nav = rect.RightPart(0.55f);
            float buttonWidth = 34f;
            float labelWidth = nav.width - buttonWidth * 2f;

            if (_currentIndex < _history.Count - 1
                && Widgets.ButtonText(new Rect(nav.x, nav.y, buttonWidth, 30f), "◀"))
            {
                _currentIndex++;
                ResetScroll();
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(
                new Rect(nav.x + buttonWidth, nav.y, labelWidth, 30f),
                "RPD_History_Record".Translate(_currentIndex + 1, _history.Count));
            Text.Anchor = TextAnchor.UpperLeft;

            if (_currentIndex > 0
                && Widgets.ButtonText(
                    new Rect(nav.x + buttonWidth + labelWidth, nav.y, buttonWidth, 30f),
                    "▶"))
            {
                _currentIndex--;
                ResetScroll();
            }
        }

        private void DrawContext(Rect rect, PersonaHistoryRecord record)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            int daysAgo = Math.Max(0, (GenTicks.TicksGame - record.timestampTick) / 60000);
            Widgets.Label(
                new Rect(inner.x, inner.y, inner.width, 24f),
                "RPD_History_Context".Translate(daysAgo).Colorize(Color.yellow));
            DrawScrollableText(
                new Rect(inner.x, inner.y + 26f, inner.width, inner.height - 26f),
                ref _contextScroll,
                string.IsNullOrEmpty(record.diffSnapshot)
                    ? "RPD_History_NoContext".Translate().ToString()
                    : record.diffSnapshot);
        }

        private void DrawPersona(
            Rect rect,
            string title,
            string persona,
            ref Vector2 scroll,
            bool canRestore,
            string confirmKey)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);

            const float buttonWidth = 130f;
            Rect titleRect = new Rect(inner.x, inner.y, inner.width, 24f);
            if (canRestore)
            {
                Rect restoreRect = new Rect(
                    titleRect.xMax - buttonWidth,
                    titleRect.y,
                    buttonWidth,
                    24f);
                titleRect.width -= buttonWidth + 6f;
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 0.7f, 0.2f);
                if (Widgets.ButtonText(
                    restoreRect,
                    "RPD_History_RestoreButton".Translate()))
                {
                    string targetPersona = persona;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        confirmKey.Translate(),
                        () => Restore(targetPersona),
                        true));
                }
                GUI.color = oldColor;
            }

            Widgets.Label(
                titleRect,
                title.Colorize(Color.cyan));
            DrawScrollableText(
                new Rect(inner.x, inner.y + 26f, inner.width, inner.height - 26f),
                ref scroll,
                string.IsNullOrEmpty(persona)
                    ? "RPD_History_Empty".Translate().ToString()
                    : persona);
        }

        private static void DrawScrollableText(Rect rect, ref Vector2 scroll, string text)
        {
            float height = Math.Max(rect.height, Text.CalcHeight(text, rect.width - 16f));
            Rect view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            Widgets.Label(new Rect(0f, 0f, view.width, height), text);
            Widgets.EndScrollView();
        }

        private void Restore(string persona)
        {
            try
            {
                string snapshot = DirectorUtils.BuildCustomCharacterData(_pawn, true, false);
                string context = "RPD_History_SystemRestore".Translate() + "\n\n" + snapshot;
                bool restored = DirectorHistoryService.ApplyWithHistory(
                    _pawn,
                    persona,
                    context,
                    true);

                if (restored)
                {
                    Messages.Message(
                        "RPD_History_RestoreSuccess".Translate(_pawn.LabelShortCap),
                        MessageTypeDefOf.PositiveEvent,
                        false);
                    Close();
                }
                else
                {
                    Messages.Message(
                        "RPD_Msg_UpdateFailed".Translate(),
                        MessageTypeDefOf.RejectInput,
                        false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Persona Director] History restore failed: " + ex);
                Messages.Message(
                    "RPD_Msg_UpdateFailed".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
            }
        }

        private void ResetScroll()
        {
            _contextScroll = Vector2.zero;
            _beforePersonaScroll = Vector2.zero;
            _afterPersonaScroll = Vector2.zero;
        }
    }
}
