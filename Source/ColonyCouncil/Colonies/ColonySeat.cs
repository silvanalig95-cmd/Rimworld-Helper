using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Make this the seat" (the player's decision 1A, docs/design/stage3-many-colonies.md, 3a): on a colony's card, for a
    /// home on the ground of several, at most once a season. What the colony as a whole has reads the seat
    /// (<see cref="Homes.Seat"/>), so it moves with it: the events and letters about no home of their own (the world's
    /// attention, <see cref="HappeningTracker.HomeMap"/>), the speaker who signs letters to the world (the seat's council),
    /// the seat's calendar's world days and nemeses from then on (the old seat keeps those it had), the region's notables
    /// and world events (anchored at the seat until every home has its own, release 4), the region map's default. Each
    /// colony's own council, calendar and names stay its own. The old seat's people trust the council a little less
    /// (trust, not a mood), and the history keeps it as a milestone.
    /// </summary>
    public static class ColonySeat
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Why this home can't be made the seat now (in words), or null.</summary>
        public static string WhyNot(Map map)
        {
            if (map == null || !Homes.IsHome(map)) return "CC_Seat_No_NotHome".Translate();
            var h = Homes.Instance;
            string why = FoundingMath.SeatWhyNot(Homes.All.Count, map == Homes.Seat, !MapFacts.InVacuum(map), Now, h?.seatMovedAt ?? -1);
            if (why == null) return null;
            if (why == "ThisSeason")
            {
                int next = FoundingMath.SeatNext(Now, h?.seatMovedAt ?? -1);
                return "CC_Seat_No_ThisSeason".Translate(GenDate.DateFullStringAt(Find.TickManager.TicksAbs + Math.Max(0, next - Now), Find.WorldGrid.LongLatOf(map.Tile)).Named("DATE")).Resolve();
            }
            return ("CC_Seat_No_" + why).Translate();
        }

        /// <summary>The seat's word by the colony's age: the elders' fire, the council's hall, the town hall, the administration.</summary>
        public static string Word(Map map) => Ages.Form(AgeFeature.Seat, Ages.Of(map), Ages.Of(map)).word;

        /// <summary>
        /// The seat moves to <paramref name="map"/>: stored, with when (once a season), the old seat's people trusting the
        /// council a little less, a milestone in the history, and a letter. <paramref name="founding"/>: made the seat as
        /// the new colony stands, so the letter is the founding's own.
        /// </summary>
        public static bool Move(Map map, bool founding = false)
        {
            if (WhyNot(map) != null) return false;
            var h = Homes.Instance;
            var old = Homes.Seat;
            if (h == null || old == null) return false;
            try
            {
                Homes.SetSeat(map);
                h.seatMovedAt = Now;
                string word = Word(map);
                string oldName = Homes.Name(old), newName = Homes.Name(map);
                // The old seat's people: the council's eye has gone elsewhere (trust, not a mood).
                var comp = CouncilGameComponent.Instance;
                if (comp != null)
                {
                    string why = "CC_Seat_TrustWhy".Translate(newName.Named("PLACE")).Resolve();
                    foreach (var p in Homes.People(old)) comp.standings.Change(p, FoundingMath.SeatTrust, why);
                }
                var line = "CC_Seat_History".Translate(newName.Named("PLACE"), oldName.Named("OLD"), word.Named("SEAT")).Resolve();
                History.Add(FoundingMath.KindSeat, new List<HistoryActor> { new HistoryActor(Refs.Colony(old), HistoryRole.About) }, Refs.Colony(map), HistoryMath.Milestone, line);
                if (!founding)
                    Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_Seat_LetterLabel".Translate(newName.Named("PLACE")).Resolve(),
                        "CC_Seat_LetterText".Translate(newName.Named("PLACE"), oldName.Named("OLD"), word.Named("SEAT")).Resolve(), LetterDefOf.NeutralEvent,
                        new LookTargets(map.Parent));
                LensModel.Dirty();
                Cards.Dirty();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the seat moved", e);
                return false;
            }
        }
    }
}
