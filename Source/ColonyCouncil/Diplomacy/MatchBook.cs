using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A match under way at a home (Matches.cs; docs/design/stage2-diplomacy.md, item 6): who of the colony's and who of
    /// theirs, their people and town, how it came about, where the couple will live, where it stands, the wedding's guests
    /// (a feast that hosts them, Feasts.Host) or their kin come for the colony's own, and whether the council pressed it
    /// over the colonist's objection. Kept on its home's part (DiplomacyHome.matches) until the wedding makes the Marriage
    /// tie, or it is called off; the alliance itself is the ledger's tie.
    /// </summary>
    public class MatchPlan : IExposable
    {
        public int id;
        public MatchWay way;
        public MatchHome lives;
        public MatchStage stage;
        public Pawn ours, theirs;
        public Faction people;
        /// <summary>Their notable (the one matched, or the one whose house the one matched is of), or -1.</summary>
        public int notable = -1;
        /// <summary>Their town (a settlement's id) and its name kept.</summary>
        public int town = -1;
        public string townName;
        /// <summary>The one of theirs was made for the match (a member of a notable's house): let go if it comes to nothing.</summary>
        public bool made;
        /// <summary>The council pressed it over the colonist's objection.</summary>
        public bool pressed;
        /// <summary>When it was agreed; the day their kin come (a tick on or after which they walk in); when they came, and when the colony's own left with them.</summary>
        public int agreed = -1, due = -1, arrived = -1, left = -1;
        /// <summary>The feast that hosts the wedding's guests (GuestFeast.id), or -1; hosting was tried and couldn't be (the spouse walks in alone).</summary>
        public int feast = -1;
        public bool alone;
        /// <summary>The history's entry for the agreement (-1: none).</summary>
        public int entry = -1;
        public string oursName, theirsName;

        public bool Live => stage != MatchStage.Wed && stage != MatchStage.Off;

        public bool Has(Pawn p) => p != null && (ours == p || theirs == p);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (ours != null && ours.Discarded) ours = null;
                if (theirs != null && theirs.Discarded) theirs = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref way, "way");
            Scribe_Values.Look(ref lives, "lives");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref ours, "ours");
            Scribe_References.Look(ref theirs, "theirs");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref made, "made");
            Scribe_Values.Look(ref pressed, "pressed");
            Scribe_Values.Look(ref agreed, "agreed", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref left, "left", -1);
            Scribe_Values.Look(ref feast, "feast", -1);
            Scribe_Values.Look(ref alone, "alone");
            Scribe_Values.Look(ref entry, "entry", -1);
            Scribe_Values.Look(ref oursName, "oursName");
            Scribe_Values.Look(ref theirsName, "theirsName");
        }
    }
}
