using RimTalk.API;
using RimTalk.Data;
using RimTalk.Prompt;
using RimTalk.Source.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimPersonaDirector
{
    internal sealed class DirectorEvolveRequest
    {
        public TalkRequest Request;
        public string OriginalPersona;
        public string HistoryContext;
    }

    internal static class DirectorEvolveRequestBuilder
    {
        public static DirectorEvolveRequest Build(Pawn pawn, string triggerContext = null)
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (settings == null || pawn == null) return null;

            string currentPersona = PersonaService.GetPersonality(pawn);
            if (string.IsNullOrWhiteSpace(currentPersona)) return null;
            currentPersona = currentPersona.Trim();

            DirectorWorldComponent world = Find.World?.GetComponent<DirectorWorldComponent>();
            int lastTick = world?.GetLastEvolveTick(pawn) ?? -1;
            string timeInfo = BuildTimeInfo(pawn, world, lastTick);
            string comparison = BuildComparison(pawn, world, lastTick);
            string memories = DirectorUtils.GetExternalMemories(pawn, lastTick);
            string notes = settings.autoEvolveNotes ?? "";
            if (!string.IsNullOrEmpty(notes))
                notes = DirectorUtils.RenderScribanText(notes, pawn) ?? "";

            StringBuilder data = new StringBuilder();
            data.AppendLine("[Basic Info]");
            data.AppendLine("Name: " + pawn.LabelShortCap);
            data.AppendLine("Gender: " + pawn.gender);
            data.AppendLine("Age: " + pawn.ageTracker.AgeBiologicalYears);
            data.AppendLine("Status: " + DirectorUtils.GetPawnSocialStatus(pawn));
            data.AppendLine();
            data.AppendLine("[Previous Persona (The Starting Point)]");
            data.AppendLine(currentPersona);
            data.AppendLine();
            data.AppendLine("[Time Context]");
            data.AppendLine(timeInfo);
            data.AppendLine();

            if (!string.IsNullOrWhiteSpace(triggerContext))
            {
                data.AppendLine("[Trigger Events]");
                data.AppendLine(triggerContext.Trim());
                data.AppendLine();
            }

            if (!string.IsNullOrEmpty(comparison))
            {
                data.AppendLine("[Status Changes (since last update)]");
                data.AppendLine(comparison);
                data.AppendLine();
            }

            if (!string.IsNullOrEmpty(notes))
            {
                data.AppendLine("[Director's Notes]");
                data.AppendLine(notes);
                data.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(memories))
            {
                data.AppendLine("[New Memories]");
                data.AppendLine(memories);
            }

            if (settings.Context != null && settings.Context.Inc_CommonKnowledge)
            {
                string commonKnowledge = DirectorUtils.GetCommonKnowledge(data.ToString(), pawn);
                if (!string.IsNullOrWhiteSpace(commonKnowledge))
                {
                    data.AppendLine();
                    data.AppendLine("[Common Knowledge]");
                    data.AppendLine(commonKnowledge);
                }
            }

            TalkRequest request = TryBuildAdvancedPresetRequest(
                settings,
                pawn,
                currentPersona,
                triggerContext);
            if (request == null)
            {
                // Do not replace a selected advanced preset with built-in content.
                if (!string.IsNullOrWhiteSpace(settings.rimTalkPreset_Evolve)
                    && !string.Equals(settings.rimTalkPreset_Evolve, "None (Use Internal)", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string prompt = GetInternalEvolvePrompt(settings);
                string instruction = prompt.Replace("{LANG}", Constant.Lang)
                    + "\n\n" + DirectorSettings.HiddenTechnicalPrompt_Single;
                request = new TalkRequest(
                    "[Update Data]\n" + data,
                    pawn,
                    null,
                    TalkType.User)
                {
                    Context = instruction
                };
            }

            return new DirectorEvolveRequest
            {
                Request = request,
                OriginalPersona = currentPersona,
                HistoryContext = BuildHistoryContext(timeInfo, comparison, memories, triggerContext)
            };
        }

        private static string BuildTimeInfo(
            Pawn pawn,
            DirectorWorldComponent world,
            int lastTick)
        {
            if (world == null || lastTick <= 0) return "No previous update record.";

            int days = (GenTicks.TicksGame - lastTick) / 60000;
            long previousAge = world.GetLastEvolveBioAgeTicks(pawn) / 3600000;
            long currentAge = pawn.ageTracker.AgeBiologicalYears;
            string result = "Time passed since last update: " + days + " days.";
            if (currentAge > previousAge)
                result += " Character aged from " + previousAge + " to " + currentAge + ".";
            return result;
        }

        private static string BuildComparison(
            Pawn pawn,
            DirectorWorldComponent world,
            int lastTick)
        {
            DirectorSettings settings = DirectorMod.Settings;
            if (world == null
                || lastTick <= 0
                || settings?.Context == null
                || !settings.Context.Inc_DataComparison)
            {
                return "";
            }

            string previous = world.GetSnapshot(pawn);
            if (string.IsNullOrEmpty(previous)) return "";
            string current = DirectorUtils.BuildCustomCharacterData(pawn, true, false);
            return DirectorUtils.GenerateDiffReport(previous, current);
        }

        private static string BuildHistoryContext(
            string timeInfo,
            string comparison,
            string memories,
            string triggerContext)
        {
            StringBuilder history = new StringBuilder();
            history.AppendLine("[Time Context]");
            history.AppendLine(timeInfo);
            if (!string.IsNullOrWhiteSpace(triggerContext))
            {
                history.AppendLine();
                history.AppendLine("[Trigger Events]");
                history.AppendLine(triggerContext.Trim());
            }
            if (!string.IsNullOrEmpty(comparison))
            {
                history.AppendLine();
                history.AppendLine("[Status Changes]");
                history.AppendLine(comparison);
            }
            if (!string.IsNullOrWhiteSpace(memories))
            {
                history.AppendLine();
                history.AppendLine("[New Memories]");
                history.AppendLine(memories);
            }
            return history.ToString().TrimEnd();
        }

        private static TalkRequest TryBuildAdvancedPresetRequest(
            DirectorSettings settings,
            Pawn pawn,
            string currentPersona,
            string triggerContext)
        {
            string presetName = settings.rimTalkPreset_Evolve;
            if (string.IsNullOrWhiteSpace(presetName)
                || string.Equals(
                    presetName,
                    "None (Use Internal)",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                var preset = RimTalkPromptAPI.GetAllPresets()
                    ?.FirstOrDefault(candidate => candidate.Name == presetName);
                if (preset == null)
                {
                    Log.Warning("[Persona Director] Auto-Evolve RimTalk preset '"
                        + presetName + "' was not found; request skipped.");
                    return null;
                }

                var context = new PromptContext(
                    new List<Pawn> { pawn },
                    new VariableStore());
                context.CurrentPawn = pawn;
                var system = new StringBuilder();
                var user = new StringBuilder();
                string previousPersonaOverride = DirectorDataEngine.TempCurrentPersona;
                string previousTriggerContext = DirectorApiAdapter.AdvancedTriggerContext;
                bool previousEvolveRendering = DirectorApiAdapter.RenderingAdvancedEvolve;

                try
                {
                    DirectorDataEngine.TempCurrentPersona = currentPersona;
                    DirectorApiAdapter.AdvancedTriggerContext = triggerContext?.Trim() ?? "";
                    DirectorApiAdapter.RenderingAdvancedEvolve = true;
                    foreach (var entry in preset.Entries)
                    {
                        if (entry == null || !entry.Enabled) continue;

                        string rendered = ScribanParser.Render(entry.Content, context, true);
                        if (string.IsNullOrWhiteSpace(rendered)) continue;

                        string role = entry.Role.ToString();
                        StringBuilder destination = role == "System" ? system : user;
                        if (destination.Length > 0) destination.AppendLine().AppendLine();
                        destination.Append(rendered);
                    }
                }
                finally
                {
                    DirectorDataEngine.TempCurrentPersona = previousPersonaOverride;
                    DirectorApiAdapter.AdvancedTriggerContext = previousTriggerContext;
                    DirectorApiAdapter.RenderingAdvancedEvolve = previousEvolveRendering;
                }

                if (user.Length == 0 && system.Length == 0)
                {
                    Log.Warning("[Persona Director] Auto-Evolve RimTalk preset '"
                        + presetName + "' produced no prompt; request skipped.");
                    return null;
                }
                return new TalkRequest(
                    user.ToString(),
                    pawn,
                    null,
                    TalkType.User)
                {
                    Context = system.ToString()
                };
            }
            catch (Exception ex)
            {
                Log.Warning("[Persona Director] Could not render Auto-Evolve RimTalk preset '"
                    + presetName + "'; request skipped. " + ex.Message);
                return null;
            }
        }

        private static string GetInternalEvolvePrompt(DirectorSettings settings)
        {
            int presetIndex = settings.autoMode == AutoEvolveMode.Overwrite ? 4 : 3;
            if (settings.presets != null
                && settings.presets.Count > presetIndex
                && !string.IsNullOrEmpty(settings.presets[presetIndex]?.text))
            {
                return settings.presets[presetIndex].text;
            }
            return settings.autoMode == AutoEvolveMode.Overwrite
                ? DirectorSettings.DefaultPrompt_Overwrite
                : DirectorSettings.DefaultPrompt_Evolve;
        }
    }
}
