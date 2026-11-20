using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A feast's guests bring their town's year (the content update's B29, 20 November 2026). A guest whose town has a
    /// world event running there now comes with it: from a hard year (any bad world event at their town) they think more of
    /// the meal than of the contests (FeastYearMath.Welcome) and, when the colony has meals to spare, ask leave to carry
    /// what is left home and take it with thanks (the meals go from the stores into their packs as they leave); from a good
    /// year (their fair, a bumper harvest) they bring a gift, carried in and set down at the colony as they arrive. The host's
    /// toast at the feast names it, in one sentence. Nothing is saved: the year is read as the feast goes.
    /// </summary>
    public static class FeastYear
    {
        /// <summary>The world event running at a guest's town now (their people's, at that town), or null.</summary>
        public static WorldSituation Of(FeastGuest g)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || g?.people == null) return null;
            return ws.Running.Where(s => s?.def != null && s.ctx?.faction == g.people && (g.town < 0 || s.place.kind == RefKind.Town && s.place.id == g.town))
                .OrderByDescending(s => s.def.bad).FirstOrDefault();
        }

        public static TownYear YearOf(FeastGuest g, out WorldSituation s)
        {
            s = Of(g);
            return s == null ? TownYear.None : FeastYearMath.Year(s.def.kind, s.def.bad);
        }

        private static string TownName(FeastGuest g, WorldSituation s) =>
            !(s?.ctx?.placeName).NullOrEmpty() ? s.ctx.placeName : g.town >= 0 ? NotableBook.TownOf(g.town)?.Name ?? g.people?.Name ?? "?" : g.people?.Name ?? "?";

        // ================= As they come =================

        /// <summary>The guests are at the gate (Feasts.Arrive): a good year's guest hands over their gift. The lines for the arrival's letter.</summary>
        public static string Arrived(GuestFeast p, Map map)
        {
            var lines = new List<string>();
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here && g.pawn != null && g.pawn.Spawned))
            {
                try
                {
                    var year = YearOf(g, out var s);
                    string town = TownName(g, s);
                    if (year == TownYear.Good)
                    {
                        int silver = FeastYearMath.Gift(LivingEvents.WealthFactor(map), Rand.Value);
                        var gate = PlaySpots.GateCell(map, g.pawn);
                        if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
                        Couriers.PaySilver(map, gate, silver, g.people, g.pawn);
                        lines.Add("CC_FeastYear_Gift".Translate(g.pawn.Named("NAME"), town.Named("TOWN"), s.Label.Named("EVENT"), silver.Named("COUNT")).Resolve());
                    }
                    else if (year == TownYear.Hard)
                        lines.Add("CC_FeastYear_Hard".Translate(g.pawn.Named("NAME"), town.Named("TOWN"), s.Label.Named("EVENT")).Resolve());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a feast's guests: their town's year", e);
                }
            }
            return lines.Count == 0 ? null : string.Join(" ", lines);
        }

        // ================= The toast =================

        /// <summary>What the host says as the cups go up, when a guest's town has a year to name: one sentence; null otherwise.</summary>
        public static string Toast(GuestFeast p)
        {
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
            {
                var year = YearOf(g, out var s);
                if (year == TownYear.None) continue;
                string town = TownName(g, s);
                return (year == TownYear.Hard ? "CC_FeastYear_ToastHard" : "CC_FeastYear_ToastGood").Translate(town.Named("TOWN"), (s.ctx?.name ?? s.def.label).Named("EVENT")).Resolve();
            }
            return null;
        }

        // ================= As they go =================

        /// <summary>A guest's welcome by their town's year (a hard year: the meal counts double, each contest half).</summary>
        public static float Welcome(FeastGuest g, float welcome, int contests) => FeastYearMath.Welcome(welcome, g.ate, contests, YearOf(g, out _));

        /// <summary>
        /// The feast is over (Feasts.End, before the guests leave): a hard year's guests who ate carry home what is left, meals
        /// from the stores into their packs, while the colony has meals to spare. The lines for the end's letter.
        /// </summary>
        public static List<string> Leftovers(GuestFeast p, Map map)
        {
            var lines = new List<string>();
            if (map == null) return lines;
            var hard = p.guests.Where(g => g.ate && g.pawn != null && g.pawn.Spawned && g.pawn.Map == map && YearOf(g, out _) == TownYear.Hard).ToList();
            if (hard.Count == 0) return lines;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            int spare = Math.Max(0, PlaySpots.CountMeals(map) - colonists);
            int n = FeastYearMath.Leftovers(hard.Count, spare);
            if (n <= 0) return lines;
            int each = Math.Max(1, n / hard.Count);
            foreach (var g in hard)
            {
                try
                {
                    int took = Pack(g.pawn, map, each);
                    if (took <= 0) continue;
                    var s = Of(g);
                    lines.Add("CC_FeastYear_Leftovers".Translate(g.pawn.Named("NAME"), TownName(g, s).Named("TOWN"), (s?.Label ?? "").Named("EVENT"), took.Named("COUNT")).Resolve());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a feast's guests: the leftovers", e);
                }
            }
            return lines;
        }

        /// <summary>So many meals from the colony's stores into the guest's pack (the plainest first); how many went.</summary>
        private static int Pack(Pawn guest, Map map, int count)
        {
            if (guest?.inventory?.innerContainer == null) return 0;
            int took = 0;
            foreach (var meal in PlaySpots.Meals(map).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).OrderBy(t => t.MarketValue).ToList())
            {
                if (took >= count) break;
                var part = meal.SplitOff(Math.Min(count - took, meal.stackCount));
                if (part == null) continue;
                int n = part.stackCount;
                if (guest.inventory.innerContainer.TryAdd(part)) took += n;
                else GenPlace.TryPlaceThing(part, guest.Position, map, ThingPlaceMode.Near);
            }
            return took;
        }
    }
}
