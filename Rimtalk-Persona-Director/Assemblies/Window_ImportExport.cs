using RimWorld;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using System.Linq;

namespace RimPersonaDirector
{
    public class Window_ImportExport : Window
    {
        private string _text = "";

        public Window_ImportExport()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
        }

        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            // --- 1. 布局定义 ---
            float buttonRowHeight = 35f;
            float gap = 5f;

            Rect buttonRowRect = new Rect(inRect.x, inRect.y, inRect.width, buttonRowHeight);
            Rect textRect = new Rect(inRect.x, buttonRowRect.yMax + gap, inRect.width, inRect.height - buttonRowHeight - gap);

            // --- 2. 绘制按钮行 ---
            float btnWidth = (buttonRowRect.width - 30f) / 4f;

            Rect exportBtnRect = new Rect(buttonRowRect.x, buttonRowRect.y, btnWidth, 30f);

            // 手工绘制按钮外观
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Button(exportBtnRect, "RPD_IO_Export".Translate());
            }

            // 处理点击
            if (Event.current.type == EventType.MouseUp && exportBtnRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    ExportAndCopyToClipboard();
                }
                else if (Event.current.button == 1)
                {
                    ExportToFile();
                }
                Event.current.Use();
            }
            TooltipHandler.TipRegion(exportBtnRect, "RPD_IO_TipExport".Translate());

            // 导入(追加)
            if (Widgets.ButtonText(new Rect(buttonRowRect.x + btnWidth + 10f, buttonRowRect.y, btnWidth, 30f), "RPD_IO_ImportAppend".Translate()))
            {
                ImportFromText(false);
            }

            // 导入(覆盖)
            if (Widgets.ButtonText(new Rect(buttonRowRect.x + (btnWidth + 10f) * 2, buttonRowRect.y, btnWidth, 30f), "RPD_IO_ImportOverwrite".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RPD_IO_ConfirmOverwrite".Translate(), () =>
                {
                    ImportFromText(true);
                }, destructive: true));
            }

            // 导入(从文件)
            Rect importFileRect = new Rect(buttonRowRect.x + (btnWidth + 10f) * 3, buttonRowRect.y, btnWidth, 30f);
            if (Widgets.ButtonText(importFileRect, "RPD_IO_ImportFile".Translate()))
            {
                string dirPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "PersonaDirector");
                if (!Directory.Exists(dirPath))
                {
                    Messages.Message("RPD_IO_MsgNoFolder".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    var files = Directory.GetFiles(dirPath, "*.xml").OrderByDescending(f => File.GetCreationTime(f)).ToList();
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (var f in files)
                    {
                        var fname = Path.GetFileName(f);
                        var path = f; // capture

                        // 这里的 (Append)/(Overwrite) 比较通用，可以保留英文，或者再加 Key
                        opts.Add(new FloatMenuOption($"{fname} (Append)", () =>
                        {
                            _text = File.ReadAllText(path);
                            ImportFromText(false);
                        }));
                        opts.Add(new FloatMenuOption($"{fname} (Overwrite)", () =>
                        {
                            _text = File.ReadAllText(path);
                            ImportFromText(true);
                        }));
                    }
                    if (opts.Count == 0) opts.Add(new FloatMenuOption("RPD_IO_MsgNoFiles".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }

            // 分割线
            Widgets.DrawLineHorizontal(inRect.x, buttonRowRect.yMax, inRect.width);

            // --- 3. 绘制文本框 ---
            _text = Widgets.TextArea(textRect, _text);
        }

        private void ExportAndCopyToClipboard()
        {
            try
            {
                string xml = GenerateExportXml();
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("RPD_IO_MsgExportClipboard".Translate(), MessageTypeDefOf.PositiveEvent, false);
                _text = xml;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Export failed: {ex}");
            }
        }

        private void ExportToFile()
        {
            try
            {
                string xml = GenerateExportXml();
                string dirPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "PersonaDirector");
                Directory.CreateDirectory(dirPath);

                string filename = $"Persona_Library_{System.DateTime.Now:yyyyMMdd_HHmmss}.xml";
                string fullPath = Path.Combine(dirPath, filename);

                File.WriteAllText(fullPath, xml);

                Messages.Message("RPD_IO_MsgExportFile".Translate(fullPath), MessageTypeDefOf.PositiveEvent, false);
                Log.Message($"[Persona Director] Library exported to: {fullPath}");

                _text = xml;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Export to file failed: {ex}");
            }
        }

        private string GenerateExportXml()
        {
            var dataToExport = new TempExportData
            {
                Presets = DirectorMod.Settings.userPresets,
                Rules = DirectorMod.Settings.assignmentRules
            };
            return Scribe.saver.DebugOutputFor(dataToExport);
        }

        private void ImportFromText(bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(_text)) return;

            try
            {
                var loadedData = new TempExportData();
                string tempPath = null;
                try
                {
                    tempPath = Path.Combine(Path.GetTempPath(), $"RPD_Import_{System.DateTime.Now:yyyyMMdd_HHmmssfff}.xml");
                    File.WriteAllText(tempPath, _text);
                    Scribe.loader.InitLoading(tempPath);
                    loadedData.ExposeData();
                    Scribe.loader.FinalizeLoading();
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }

                if (overwrite)
                {
                    DirectorMod.Settings.userPresets = loadedData.Presets ?? new List<CustomPreset>();
                    DirectorMod.Settings.assignmentRules = loadedData.Rules ?? new List<AssignmentRule>();
                }
                else
                {
                    if (loadedData.Presets != null)
                        DirectorMod.Settings.userPresets.AddRange(loadedData.Presets);

                    if (loadedData.Rules != null)
                        DirectorMod.Settings.assignmentRules.AddRange(loadedData.Rules);
                }

                PresetSynchronizer.SyncToRimTalk();

                Messages.Message("RPD_IO_MsgImportSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                this.Close();
            }
            catch (System.Exception ex)
            {
                Log.Error($"Import failed: {ex}");
                Messages.Message("RPD_IO_MsgImportFail".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }

        private class TempExportData : IExposable
        {
            public List<CustomPreset> Presets;
            public List<AssignmentRule> Rules;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref Presets, "UserPresets", LookMode.Deep);
                Scribe_Collections.Look(ref Rules, "AssignmentRules", LookMode.Deep);
            }
        }
    }
}