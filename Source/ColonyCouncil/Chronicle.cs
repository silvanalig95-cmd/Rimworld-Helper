using Verse;

namespace ColonyCouncil
{
    public enum ChronicleKind
    {
        WishMade,
        WishGranted,
        WishExpired,
        WishDismissed,
        Petition,
        Meeting,
        AmbitionChosen,
        AmbitionAchieved,
        AmbitionFailed,
        AmbitionAbandoned,
        PersonalTouch,
        Note
    }

    /// <summary>One line in the colony's chronicle. Stores text, not references, so it survives anything.</summary>
    public class ChronicleEntry : IExposable
    {
        public int tick;
        public ChronicleKind kind;
        public string text;
        /// <summary>ThingID of the colonist the entry is mostly about (for RimTalk memory of recent events).</summary>
        public string pawnId;
        /// <summary>Short English topic for AI context, e.g. "got their own bedroom".</summary>
        public string topic;

        public ChronicleEntry()
        {
        }

        public ChronicleEntry(ChronicleKind kind, string text, Pawn pawn = null, string topic = null)
        {
            tick = Find.TickManager.TicksGame;
            this.kind = kind;
            this.text = text;
            pawnId = pawn?.ThingID;
            this.topic = topic;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref topic, "topic");
        }
    }
}
