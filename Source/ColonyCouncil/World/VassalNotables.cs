using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A vassal's head among the notables (Diplomacy/Vassals.cs; docs/design/stage2-diplomacy.md, item 10, decision 12): a
    /// town taken under the colony's protection has its head named at once, wherever it lies; and each of Faction
    /// Territories' vassal outposts gets a head of its first people (the one who headed the town before it became the
    /// colony's vassal, when the notables knew them, else one made for it), who writes, asks and thinks of the colony as
    /// any notable does. They are kept while the vassal stands (Vassals.KeepsHead, read in Look).
    /// </summary>
    public partial class NotableBook
    {
        /// <summary>A town under the colony's protection has a head: named now if it has none.</summary>
        public static void StaffVassal(Settlement town)
        {
            var book = Instance;
            if (!On || book == null || town == null || town.Destroyed || town.Faction == null || town.Faction.IsPlayer) return;
            if (book.notables.Any(n => n != null && n.town == town.ID && n.role == NotableRole.Head && n.Alive)) return;
            try
            {
                book.Staff(town, Now, 1);
                Changed();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notables: a vassal's head", e);
            }
        }

        /// <summary>
        /// One of Faction Territories' vassal outposts has a head of its first people: the town's own head carried over
        /// when the outpost was made from a town the notables knew (the history's world:vassal names it), else one made for
        /// it. Returns the head, or null without the notables.
        /// </summary>
        public static Notable StaffOutpost(WorldObject outpost, Faction people, string name)
        {
            var book = Instance;
            if (!On || book == null || outpost == null || people == null || people.defeated) return null;
            var head = book.notables.FirstOrDefault(n => n != null && n.town == outpost.ID && n.role == NotableRole.Head && n.Alive);
            if (head != null) return head;
            try
            {
                // The town it was: its head stays on as the outpost's.
                var made = History.Latest(WorldMath.Vassal, place: Ref.Town(outpost.ID));
                int former = -1;
                if (made?.actors != null)
                    foreach (var a in made.actors)
                        if (a.role == HistoryRole.About && a.who.kind == RefKind.Town)
                            former = a.who.id;
                var old = former >= 0 ? book.notables.FirstOrDefault(n => n != null && n.town == former && n.role == NotableRole.Head && n.Alive && n.faction == people) : null;
                if (old != null && !PawnUtility.IsFactionLeader(old.pawn))
                {
                    old.town = outpost.ID;
                    old.townName = name;
                    Changed();
                    return old;
                }
                var p = Make(people, NotableRole.Head, null, null);
                if (p == null) return null;
                var n2 = book.Add(p, null, NotableRole.Head, true, null, null);
                n2.town = outpost.ID;
                n2.townName = name;
                n2.faction = people;
                n2.Remember("CC_Vassal_Note_Named".Translate((name ?? "?").Named("TOWN")).Resolve(), 0f);
                Changed();
                return n2;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notables: an outpost's head", e);
                return null;
            }
        }
    }
}
