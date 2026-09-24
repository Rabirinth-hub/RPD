using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;

namespace RimPersonaDirector
{
    public class MainButtonWorker_Director : MainButtonWorker
    {
        public override bool Visible => DirectorMod.Settings.ShowMainButton;

        public override void Activate()
        {
            // 优先级 1: Shift + 左键 打开设置
            if (Event.current.shift)
            {
                var mod = LoadedModManager.GetMod<DirectorMod>();
                if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
                return; // 处理完就退出
            }

            // 右键 (button == 1)
            if (Event.current.button == 1)
            {
                // 切换高级窗口
                var win = Find.WindowStack.Windows.OfType<Window_BatchDirector>().FirstOrDefault();
                if (win != null) win.Close();
                else Find.WindowStack.Add(new Window_BatchDirector());

                // ★ 关键：消耗事件，防止右键菜单等其他游戏行为响应
                Event.current.Use();
            }
            // 默认行为（左键, button == 0）
            else
            {
                // 切换备注窗口
                var win = Find.WindowStack.Windows.OfType<Window_DirectorNotesEditor>().FirstOrDefault();
                if (win != null) win.Close();
                else Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }
        }
    }
}