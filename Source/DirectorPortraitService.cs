using RimTalk.Data;
using RimTalk.Service;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimPersonaDirector
{
    internal static class DirectorPortraitService
    {
        private const int RequestSize = 512;
        private const int HistorySize = 160;
        private const string PortraitReferencePrompt =
            "The reference image is an in-game RimWorld image.";
        private const string PortraitComparisonPrompt =
            "The reference image is an in-game RimWorld image. Left: last recorded portrait; right: current portrait.";
        private const string ImageResponseProtocol = @"
# IMAGE PERSONA RESPONSE FORMAT
Return exactly one JSON object on one line, with fields 'name' and 'text'.
'name' must be the pawn name given in the request.
'text' must begin with CHATTINESS=0.5 (choose a number from 0.1 to 1.0), then a newline and the complete persona text.
Escape line breaks and quotes correctly inside the JSON string. Do not use Markdown or write dialogue.";

        private sealed class PreparedPortrait
        {
            public int PawnId;
            public int CreatedTick;
            public string HistoryBase64;
        }

        private sealed class PendingPortrait
        {
            public string Persona;
            public int CreatedTick;
            public string HistoryBase64;
        }

        private static readonly ConcurrentDictionary<TalkRequest, PreparedPortrait> Prepared =
            new ConcurrentDictionary<TalkRequest, PreparedPortrait>();
        private static readonly Dictionary<int, PendingPortrait> Pending =
            new Dictionary<int, PendingPortrait>();

        static DirectorPortraitService()
        {
            DirectorFeatureGate.RegisterTransientCleanup(ClearTransient);
        }

        // Called while the request is being built on the game thread. The original Query path
        // receives the unmodified request whenever this setting is off or capture fails.
        public static void Prepare(TalkRequest request, Pawn pawn, bool enabled, bool builtIn,
            bool compareHistory = false)
        {
            if (!enabled || request == null || pawn == null || pawn.Destroyed) return;

            string image = CaptureJpegBase64(pawn, RequestSize);
            if (string.IsNullOrEmpty(image)) return;

            bool hasComparison = false;
            if (compareHistory)
            {
                string previous = Find.World?.GetComponent<DirectorWorldComponent>()
                    ?.GetLatestHistoryPortrait(pawn);
                if (!string.IsNullOrEmpty(previous))
                {
                    string comparison = ComposeComparison(previous, image);
                    if (!string.IsNullOrEmpty(comparison))
                    {
                        image = comparison;
                        hasComparison = true;
                    }
                }
            }

            request.ImageBase64 = image;
            request.Participants = new List<Pawn> { pawn };

            string context = (request.Context ?? "").Replace(
                DirectorSettings.HiddenTechnicalPrompt_Single, "").TrimEnd();
            context += "\n\n" + ImageResponseProtocol;
            string userPrompt = request.Prompt ?? "";
            if (hasComparison)
                userPrompt += "\n\n" + PortraitComparisonPrompt;
            else if (builtIn)
                userPrompt += "\n\n" + PortraitReferencePrompt;
            request.PromptMessages = new List<(Role role, string content)>
            {
                (Role.System, context),
                (Role.User, userPrompt)
            };

            Prepared[request] = new PreparedPortrait
            {
                PawnId = pawn.thingIDNumber,
                CreatedTick = GenTicks.TicksGame,
                HistoryBase64 = DirectorFeatureGate.ExperimentalEnabled
                    ? CaptureJpegBase64(pawn, HistorySize) : null
            };
        }

        public static Task<PersonalityData> Query(TalkRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ImageBase64))
                return AIService.Query<PersonalityData>(request);
            return QueryWithImage(request);
        }

        private static async Task<PersonalityData> QueryWithImage(TalkRequest request)
        {
            PreparedPortrait prepared;
            Prepared.TryRemove(request, out prepared);
            PersonalityData result = null;
            await AIService.ChatStreaming(request, response =>
            {
                if (result != null || response == null) return;
                result = ParseResponse(response.Text);
            });

            if (result == null)
                Log.Warning("[Persona Director] Image persona request returned no valid persona response.");

            if (result != null && prepared != null && !string.IsNullOrEmpty(prepared.HistoryBase64))
            {
                lock (Pending)
                {
                    if (Pending.Count >= 24 && !Pending.ContainsKey(prepared.PawnId))
                    {
                        foreach (int key in new List<int>(Pending.Keys))
                        {
                            Pending.Remove(key);
                            break;
                        }
                    }
                    Pending[prepared.PawnId] = new PendingPortrait
                    {
                        Persona = result.Persona,
                        CreatedTick = prepared.CreatedTick,
                        HistoryBase64 = prepared.HistoryBase64
                    };
                }
            }
            return result;
        }

        private static PersonalityData ParseResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string normalized = text.Replace("\\n", "\n").Trim();
            int newline = normalized.IndexOf('\n');
            if (newline < 0) return null;
            string header = normalized.Substring(0, newline).Trim();
            if (!header.StartsWith("CHATTINESS=", StringComparison.OrdinalIgnoreCase)) return null;
            float chattiness;
            if (!float.TryParse(header.Substring("CHATTINESS=".Length),
                NumberStyles.Float, CultureInfo.InvariantCulture, out chattiness)) return null;
            string persona = normalized.Substring(newline + 1).Trim();
            if (string.IsNullOrEmpty(persona)) return null;
            return new PersonalityData(persona, Mathf.Clamp(chattiness, 0.1f, 1f));
        }

        public static string TakeHistoryPortrait(Pawn pawn, string appliedPersona)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(appliedPersona)) return null;
            lock (Pending)
            {
                PendingPortrait pending;
                if (!Pending.TryGetValue(pawn.thingIDNumber, out pending)
                    || GenTicks.TicksGame < pending.CreatedTick
                    || GenTicks.TicksGame - pending.CreatedTick > 60000)
                    return null;
                string applied = appliedPersona.Trim();
                string generated = pending.Persona?.Trim() ?? "";
                if (!string.Equals(generated, applied, StringComparison.Ordinal)
                    && !(applied.Length >= 30 && generated.Contains(applied)))
                    return null;
                Pending.Remove(pawn.thingIDNumber);
                return pending.HistoryBase64;
            }
        }

        public static void RemapPendingPortrait(Pawn pawn, string generatedPersona, string appliedPersona)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(appliedPersona)) return;
            lock (Pending)
            {
                PendingPortrait pending;
                if (Pending.TryGetValue(pawn.thingIDNumber, out pending)
                    && string.Equals(pending.Persona?.Trim(), generatedPersona?.Trim(), StringComparison.Ordinal))
                    pending.Persona = appliedPersona;
            }
        }

        private static void ClearTransient()
        {
            Prepared.Clear();
            lock (Pending) Pending.Clear();
        }

        // Runs from PromptManager.BuildMessages on the game thread, before RimTalk
        // hands its completed request to the streaming worker.
        internal static void AttachDailyDialoguePortrait(TalkRequest request, Pawn pawn,
            List<(Role role, string content)> messages)
        {
            if (request == null || pawn == null || pawn.Destroyed || messages == null
                || !string.IsNullOrEmpty(request.ImageBase64)) return;

            string thumbnail = CaptureJpegBase64(pawn, HistorySize);
            if (string.IsNullOrEmpty(thumbnail)) return;
            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            if (world == null) return;
            string previous = world.GetDailyPortraitBaseline(pawn);
            if (string.IsNullOrEmpty(previous))
            {
                world.SaveDailyPortraitBaseline(pawn, thumbnail);
                return;
            }
            if (!HasMeaningfulChange(previous, thumbnail)) return;

            string current = CaptureJpegBase64(pawn, RequestSize);
            if (string.IsNullOrEmpty(current)) return;
            string composite = ComposeComparison(previous, current);
            if (string.IsNullOrEmpty(composite)) return;
            int participantIndex = request.Participants?.IndexOf(pawn) ?? -1;
            string uniqueName = PromptService.GetUniqueName(pawn, request.Participants);
            string subject = participantIndex >= 0
                ? $"[P{participantIndex + 1}] {uniqueName}" : uniqueName;
            string hint = $"The reference image is an in-game RimWorld image of {subject}. "
                + "Left: last recorded portrait; right: current portrait.";
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].role != Role.User) continue;
                messages[i] = (Role.User, messages[i].content + "\n\n" + hint);
                request.ImageBase64 = composite;
                world.SaveDailyPortraitBaseline(pawn, thumbnail);
                return;
            }
            messages.Add((Role.User, hint));
            request.ImageBase64 = composite;
            world.SaveDailyPortraitBaseline(pawn, thumbnail);
        }

        private static bool HasMeaningfulChange(string previousBase64, string currentBase64)
        {
            if (string.Equals(previousBase64, currentBase64, StringComparison.Ordinal)) return false;
            Texture2D previous = null;
            Texture2D current = null;
            try
            {
                previous = new Texture2D(2, 2, TextureFormat.RGB24, false);
                current = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(previous, Convert.FromBase64String(previousBase64))
                    || !ImageConversion.LoadImage(current, Convert.FromBase64String(currentBase64)))
                    return false;

                if (previous.width != current.width || previous.height != current.height)
                    return true;
                // The stored portrait is already only 160x160. Compare every pixel
                // and tolerate small JPEG/color shifts without missing small details.
                Color32[] before = previous.GetPixels32();
                Color32[] after = current.GetPixels32();
                const int changedPixelThreshold = 32;
                int changed = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    int difference = Math.Abs(before[i].r - after[i].r)
                        + Math.Abs(before[i].g - after[i].g)
                        + Math.Abs(before[i].b - after[i].b);
                    if (difference >= 45 && ++changed >= changedPixelThreshold)
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Daily portrait comparison failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (previous != null) UnityEngine.Object.Destroy(previous);
                if (current != null) UnityEngine.Object.Destroy(current);
            }
        }

        private static string CaptureJpegBase64(Pawn pawn, int size)
        {
            Texture2D copy = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture portrait = PortraitsCache.Get(
                    pawn, new Vector2(size, size), Rot4.South,
                    supersample: false, compensateForUIScale: false);
                if (portrait == null) return null;
                RenderTexture.active = portrait;
                copy = new Texture2D(portrait.width, portrait.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0f, 0f, portrait.width, portrait.height), 0, 0);
                copy.Apply(false, false);
                byte[] jpeg = ImageConversion.EncodeToJPG(copy, size == RequestSize ? 88 : 76);
                return jpeg == null || jpeg.Length == 0 ? null : Convert.ToBase64String(jpeg);
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Pawn portrait capture failed: " + ex.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        private static string ComposeComparison(string previousBase64, string currentBase64)
        {
            Texture2D previous = null;
            Texture2D current = null;
            Texture2D combined = null;
            try
            {
                previous = new Texture2D(2, 2, TextureFormat.RGB24, false);
                current = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(previous, Convert.FromBase64String(previousBase64))
                    || !ImageConversion.LoadImage(current, Convert.FromBase64String(currentBase64)))
                    return null;

                const int half = RequestSize / 2;
                combined = new Texture2D(RequestSize, half, TextureFormat.RGB24, false);
                var pixels = new Color[RequestSize * half];
                for (int y = 0; y < half; y++)
                {
                    float v = (y + 0.5f) / half;
                    for (int x = 0; x < RequestSize; x++)
                    {
                        bool left = x < half;
                        float u = ((x % half) + 0.5f) / half;
                        pixels[y * RequestSize + x] = left
                            ? previous.GetPixelBilinear(u, v)
                            : current.GetPixelBilinear(u, v);
                    }
                }
                combined.SetPixels(pixels);
                combined.Apply(false, false);
                return Convert.ToBase64String(ImageConversion.EncodeToJPG(combined, 88));
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Portrait comparison failed: " + ex.Message);
                return null;
            }
            finally
            {
                if (previous != null) UnityEngine.Object.Destroy(previous);
                if (current != null) UnityEngine.Object.Destroy(current);
                if (combined != null) UnityEngine.Object.Destroy(combined);
            }
        }
    }
}
