using Verse;
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimTalk.Data; // 引用以访问 Constant
using UnityEngine;

namespace RimPersonaDirector
{
    public static class DirectorStartup
    {
        // Bump this ID for each release that needs a fresh one-time notice.
        internal const string ReleaseNoticeId = "2026-09-24-important-update";
        private static bool releaseNoticeQueued;

        public static void Initialize()
        {
            try
            {
                var settings = DirectorMod.Settings;
                if (settings == null) return;

                // 防空
                if (settings.userPresets == null) settings.userPresets = new List<CustomPreset>();
                if (settings.assignmentRules == null) settings.assignmentRules = new List<AssignmentRule>();

                // ★★★ 核心修复 C：先备份真·原版数据 ★★★
                // 在我们做任何同步/覆盖之前，先看看 Constant.Personalities 里有什么
                // 此时游戏刚加载完，Constant 里肯定是干净的原版数据
                if (DirectorSettings.OriginalVanillaCache == null && Constant.Personalities != null)
                {
                    if (Constant.Personalities is IEnumerable<PersonalityData> list)
                    {
                        DirectorSettings.OriginalVanillaCache = new List<PersonalityData>(list);
                        Log.Message($"[Persona Director] Cached {DirectorSettings.OriginalVanillaCache.Count} original vanilla presets.");
                    }
                }

                // 2. 执行新用户初始化
                if (!settings._libraryInitialized)
                {
                    if (settings.userPresets.Count == 0 && settings.assignmentRules.Count == 0)
                    {
                        Log.Message("[Persona Director] First time setup detected. Initializing library...");
                        settings.InitLibrary();
                    }

                    settings._libraryInitialized = true;
                    settings.Write();
                }

                if (settings.RefreshLocalizedBuiltInsIfNeeded())
                {
                    settings.Write();
                    Log.Message("[Persona Director] Refreshed built-in presets for the active game language.");
                }

                // 3. ★★★ 最后再同步 ★★★
                // 现在可以用我们的数据去覆盖原版了，因为原版已经备份过了
                PresetSynchronizer.SyncToRimTalk();
                Log.Message("[Persona Director] Sync to RimTalk completed.");

                // 实验功能在正式版初始化完成后单独协调，默认保持关闭。
                DirectorFeatureGate.ReconcileOnStartup();
            }
            catch (Exception ex)
            {
                Log.Error($" Initialization Failed: {ex}");
            }
        }

        internal static void ShowReleaseNoticeOnMainMenu()
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (releaseNoticeQueued
                || settings == null
                || settings.acknowledgedReleaseNotice == ReleaseNoticeId
                || Find.WindowStack == null)
            {
                return;
            }

            Find.WindowStack.Add(new Window_ReleaseNotice(settings));
            releaseNoticeQueued = true;
            Log.Message("[Persona Director] Release notice opened on the main menu.");
        }

        internal static void ReleaseNoticeClosed()
        {
            releaseNoticeQueued = false;
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    internal static class Patch_ReleaseNoticeOnMainMenu
    {
        [HarmonyPostfix]
        private static void ShowNotice()
        {
            DirectorStartup.ShowReleaseNoticeOnMainMenu();
        }
    }

    internal sealed class Window_ReleaseNotice : Window
    {
        private readonly DirectorSettings settings;
        private Vector2 bodyScrollPosition;

        public override Vector2 InitialSize => new Vector2(660f, 500f);

        public Window_ReleaseNotice(DirectorSettings settings)
        {
            this.settings = settings;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            doCloseX = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 36f),
                "RPD_ReleaseNotice_Title".Translate());
            Text.Font = GameFont.Small;

            string body = "RPD_ReleaseNotice_Body".Translate();
            Rect bodyRect = new Rect(0f, 48f, inRect.width, inRect.height - 108f);
            float viewWidth = bodyRect.width - 18f;
            float viewHeight = Mathf.Max(bodyRect.height, Text.CalcHeight(body, viewWidth) + 8f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(bodyRect, ref bodyScrollPosition, viewRect);
            Widgets.Label(viewRect, body);
            Widgets.EndScrollView();

            Rect buttonRect = new Rect(inRect.width - 160f, inRect.height - 40f, 160f, 36f);
            if (Widgets.ButtonText(buttonRect, "RPD_ReleaseNotice_Acknowledge".Translate()))
            {
                settings.acknowledgedReleaseNotice = DirectorStartup.ReleaseNoticeId;
                settings.Write();
                Close();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            DirectorStartup.ReleaseNoticeClosed();
        }
    }
}
