using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    public class Window_DirectorNotesEditor : Window
    {
        private Vector2 scrollPos = Vector2.zero;

        public Window_DirectorNotesEditor()
        {
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = true;
            this.absorbInputAroundWindow = false; 
            this.closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(500f, 420f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            // 标题栏：左边标题，右边切换按钮
            Rect headerRect = list.GetRect(30f);
            Widgets.Label(headerRect.LeftPart(0.6f), "RPD_Notes_Title".Translate());

            // ★ 切换按钮 ★
            if (Widgets.ButtonText(headerRect.RightPart(0.4f), "RPD_Mode_SwitchToAdvanced".Translate()))
            {
                this.Close();
                Find.WindowStack.Add(new Window_BatchDirector());
            }

            list.Gap(5f);
            list.Label("RPD_Notes_Description".Translate());
            list.Gap(5f);

            // 清空按钮
            if (list.ButtonText("RPD_Button_Clear".Translate())) 
            {
                DirectorMod.Settings.directorNotes = "";
            }
            list.GapLine();

            // Token 估算
            int notesCharCount = DirectorMod.Settings.directorNotes?.Length ?? 0;
            int estTokens = (int)(notesCharCount / 2.5f);

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;

            Rect footerRect = list.GetRect(20f);
            Widgets.Label(footerRect, "RPD_Label_CharCount".Translate(notesCharCount, estTokens)); 

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            list.Gap(-5f);

            // 文本框
            float textAreaHeight = inRect.height - list.CurHeight - 40f;
            Rect outRect = list.GetRect(textAreaHeight);

            float contentHeight = Text.CalcHeight(DirectorMod.Settings.directorNotes, outRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(contentHeight, outRect.height));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            DirectorMod.Settings.directorNotes = Widgets.TextArea(viewRect, DirectorMod.Settings.directorNotes);
            Widgets.EndScrollView();

            list.End();
        }

        public override void PreClose()
        {
            base.PreClose();
            DirectorMod.Settings.Write();
        }
    }
}