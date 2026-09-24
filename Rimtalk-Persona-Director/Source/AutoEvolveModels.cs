using Verse;

namespace RimPersonaDirector
{
    public enum AutoEvolveMode
    {
        Append,
        Overwrite
    }

    public enum AutoEvolveNotify
    {
        Silent,
        TopLeftMessage
    }

    public enum SpeedProtection
    {
        Disabled,
        Speed2X,
        Speed3X,
        Speed4X
    }

    public sealed class PersonaHistoryRecord : IExposable
    {
        public int timestampTick;
        public string personaText;
        public string diffSnapshot;

        public void ExposeData()
        {
            Scribe_Values.Look(ref timestampTick, "timestampTick", 0);
            Scribe_Values.Look(ref personaText, "personaText");
            Scribe_Values.Look(ref diffSnapshot, "diffSnapshot");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (personaText == null) personaText = "";
                if (diffSnapshot == null) diffSnapshot = "";
            }
        }

        public PersonaHistoryRecord Copy()
        {
            return new PersonaHistoryRecord
            {
                timestampTick = timestampTick,
                personaText = personaText ?? "",
                diffSnapshot = diffSnapshot ?? ""
            };
        }
    }

    public sealed class AutoEvolveConfig : IExposable
    {
        public bool enabled;
        public int intervalDays;
        public int nextUpdateTick = -1;
        public string daysBuffer = "0";

        public bool TimedUpdatesEnabled => enabled && intervalDays >= 1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref intervalDays, "intervalDays", 0);
            Scribe_Values.Look(ref nextUpdateTick, "nextUpdateTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Older saves could omit the old default value (15 days) while
                // still carrying a valid scheduled tick. Preserve that schedule.
                if (enabled && intervalDays < 1 && nextUpdateTick > 0)
                    intervalDays = 15;
                intervalDays = NormalizeInterval(intervalDays);
                daysBuffer = intervalDays.ToString();
                if (!TimedUpdatesEnabled) nextUpdateTick = -1;
            }
        }

        public static int NormalizeInterval(int days)
        {
            if (days < 1) return 0;
            if (days > 3600) return 3600;
            return days;
        }

        public void ScheduleFromNow(int currentTick)
        {
            intervalDays = NormalizeInterval(intervalDays);
            daysBuffer = intervalDays.ToString();
            nextUpdateTick = TimedUpdatesEnabled
                ? currentTick + intervalDays * 60000
                : -1;
        }
    }
}
