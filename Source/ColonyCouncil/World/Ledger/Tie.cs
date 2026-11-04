using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Saving a tie (its rules and fields are in TieMath.cs). Refs are saved in their short text form.</summary>
    public partial class Tie : IExposable
    {
        public void ExposeData()
        {
            terms ??= new TieTerms();
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Refs.Look(ref from, "from");
            Refs.Look(ref to, "to");
            Scribe_Values.Look(ref terms.goods, "goods");
            Scribe_Values.Look(ref terms.count, "count");
            Scribe_Values.Look(ref terms.days, "days");
            Scribe_Values.Look(ref terms.key, "key");
            Scribe_Values.Look(ref terms.number, "number");
            Refs.Look(ref terms.about, "about");
            Scribe_Values.Look(ref since, "since", Never);
            Scribe_Values.Look(ref until, "until", Never);
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref closed, "closed", Never);
            Scribe_Values.Look(ref origin, "origin");
        }
    }

    /// <summary>
    /// Refs to and from the game's own things: a pawn, a people, a town, a colony. A ref is a plain value; these find
    /// what it names while the game still has it (null once it's gone).
    /// </summary>
    public static class Refs
    {
        public static Ref Of(Pawn p) => p == null ? Ref.None : Ref.Person(p.thingIDNumber);

        public static Ref Of(Faction f) => f == null ? Ref.None : Ref.People(f.loadID);

        public static Ref Of(Settlement s) => s == null ? Ref.None : Ref.Town(s.ID);

        /// <summary>A colony: one of the player's homes, by its key (Homes.KeyOf).</summary>
        public static Ref Colony(Map home) => home == null ? Ref.None : Ref.Colony(Homes.KeyOf(home));

        /// <summary>The colony the council sits at (Homes.Seat), for what belongs to the colony as a whole.</summary>
        public static Ref Seat => Colony(Homes.Seat);

        public static Faction Faction(Ref r)
        {
            if (r.kind != RefKind.People) return null;
            var all = Find.FactionManager?.AllFactionsListForReading;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].loadID == r.id)
                    return all[i];
            return null;
        }

        /// <summary>The town while it stands.</summary>
        public static Settlement Town(Ref r) => r.kind == RefKind.Town ? NotableBook.TownOf(r.id) : null;

        /// <summary>The colony's home map while it's still a home.</summary>
        public static Map Home(Ref r) => r.kind == RefKind.Colony ? Homes.MapOf(r.id) : null;

        /// <summary>
        /// The pawn behind a person, alive or dead, wherever the game keeps them (on a map, in the world, in a caravan);
        /// null for a person with no pawn or one the game let go of. Looks through everyone: for views, not every tick.
        /// </summary>
        public static Pawn Pawn(Ref r)
        {
            if (!r.HasPawn) return null;
            return PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.FirstOrDefault(p => p != null && p.thingIDNumber == r.id);
        }

        /// <summary>Saves a ref as its text form ("p123"), and reads it back (none when missing or unreadable).</summary>
        public static void Look(ref Ref r, string label)
        {
            string s = Scribe.mode == LoadSaveMode.Saving && !r.IsNone ? r.ToString() : null;
            Scribe_Values.Look(ref s, label);
            if (Scribe.mode == LoadSaveMode.LoadingVars) r = Ref.Parse(s);
        }
    }
}
