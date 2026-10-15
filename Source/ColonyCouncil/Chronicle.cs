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
        Note,
        /// <summary>Decrees, dilemmas, offices, traditions.</summary>
        Council,
        /// <summary>Friendships, rivalries, mentorships, circles, social moments.</summary>
        Bond,
        /// <summary>Life goals, names, coming of age.</summary>
        Life,
        Secret,
        /// <summary>Deaths, eulogies, vendettas, heirlooms.</summary>
        Loss,
        Dream,
        /// <summary>Visitors, traders, prisoners, beasts, pets, nemeses.</summary>
        Stranger,
        /// <summary>World events: pasts catching up, challenges, wonders, visitors with a purpose.</summary>
        World
    }

    /// <summary>One line in the colony's chronicle. Stores text, not references, so it survives anything.</summary>
    public class ChronicleEntry : IExposable
    {
        public int tick;
        public ChronicleKind kind;
        public string text;
        /// <summary>ThingID of the colonist the entry is mostly about.</summary>
        public string pawnId;

        public ChronicleEntry()
        {
        }

        /// <summary>
        /// A milestone of the colony's history (a loss, an ambition won or lost, a life event, a world event, a secret out):
        /// when the chronicle is full, older everyday lines make way before these do (Caps.TrimKeeping).
        /// </summary>
        public bool Milestone =>
            kind == ChronicleKind.Loss || kind == ChronicleKind.AmbitionAchieved || kind == ChronicleKind.AmbitionFailed || kind == ChronicleKind.Life
            || kind == ChronicleKind.World || kind == ChronicleKind.Secret;

        public ChronicleEntry(ChronicleKind kind, string text, Pawn pawn = null)
        {
            tick = Find.TickManager.TicksGame;
            this.kind = kind;
            this.text = text;
            pawnId = pawn?.ThingID;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref pawnId, "pawnId");
        }
    }
}
