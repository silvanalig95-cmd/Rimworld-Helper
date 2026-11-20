using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// One cup between them (OwnHappening.RevelRivalsCup): late in the evening two of the colony at odds are both still up, and
    /// one of them fetches a drink from the stores (the colony's own drink, Revel's goods) and sets it down between them. The
    /// other drinks it, as their drug policy and their faith allow, and the two make it up; one who won't drink says so, and
    /// nothing comes of it. A cup shared is half the quarrel gone, his believers say.
    /// </summary>
    public class RevelRivalsCup : BetweenKind
    {
        public override OwnHappening H => OwnHappening.RevelRivalsCup;

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var up = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p) && !p.InBed()).ToList();
            foreach (var x in up.InRandomOrder())
            {
                var y = up.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, 20f)
                                               && (OwnBetween.Opinion(x, o) <= OwnHappeningsMath.CupBelow || OwnBetween.Opinion(o, x) <= OwnHappeningsMath.CupBelow));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.LateFrom || hour >= OwnHappeningsMath.LateTo) return;
            var drink = RevelGoods.HeldDrink(map);
            if (drink == null) return;
            var (x, y) = Pair(map);
            if (x == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, x, y, drink);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y, Thing drink)
        {
            var between = OwnHappenings.Beside(map, y.Position, x);
            if (!between.IsValid || !OwnHappenings.Carry(x, drink, between, 1)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.RevelRivalsCup, g, map, between, x, y, 3f);
            s.word = drink.def.label;
            s.thing = drink;
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.a.CurJobDef == JobDefOf.HaulToCell)
            {
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            // The cup set down: the one lying by them now, of that drink.
            var cup = GenRadial.RadialCellsAround(s.at, 1.5f, true).Where(c => c.InBounds(map)).SelectMany(c => c.GetThingList(map))
                .FirstOrDefault(t => t.def.label == s.word && t.def.ingestible != null && !t.IsInAnyStorage());
            if (s.a.Position.InHorDistOf(s.b.Position, 4f) && Together.Can(s.a, s.b))
            {
                var what = (s.word ?? "").Named("DRINK");
                OwnBetween.Face(s.a, s.b, 600);
                OwnBetween.Say(s.a, H, "pour", g, s.b, what);
                bool drinks = cup != null && FirstPot.WillDrink(s.b, cup.def);
                OwnBetween.Say(s.b, H, drinks ? "drink" : "refuse", g, s.a, what);
                if (drinks)
                {
                    var ingest = JobMaker.MakeJob(JobDefOf.Ingest, cup);
                    ingest.count = 1;
                    OwnHappenings.Job(s.b, ingest);
                    OwnBetween.Show(g, map, s.at);
                    OwnBetween.End(H, BetweenOutcome.MadeUp, s.a, s.b, null, null, what);
                    OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"), what);
                }
                else OwnBetween.Card(H, g, map, s.at, false, "TextRefused", s.a.Named("A"), s.b.Named("B"), what);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, s.a, s.b.LabelShort.Named("OTHER"), what);
                s.stage = -1;
                return;
            }
            if (s.a.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.a, map, s.b.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var drink = RevelGoods.HeldDrink(map);
            var (x, y) = Pair(map);
            if (x == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count == 2)
                {
                    x = two[0];
                    y = two[1];
                }
            }
            if (drink == null || x == null) return "a drink in the stores and two grown of the colony are needed.";
            return Begin(b, g, map, x, y, drink) ? x.LabelShort + " carries a " + drink.def.label + " over to " + y.LabelShort + "." : "nowhere to set it down by them.";
        }
    }

    /// <summary>
    /// A wager lost (OwnHappening.RevelWager): late at night two of the colony, both drunk, make a wager, and in the morning
    /// the loser is seen scrubbing the floor of the winner's room as they owe. A good loser laughs about it with the winner;
    /// a sore one feels laughed at. He likes nothing better than someone fooled, the stories of him say.
    /// </summary>
    public class RevelWager : BetweenKind
    {
        public override OwnHappening H => OwnHappening.RevelWager;

        private static bool Drunk(Pawn p) => p?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.AlcoholHigh) != null;

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var drunk = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && Drunk(p) && !p.InBed()).ToList();
            foreach (var x in drunk)
            {
                var y = drunk.FirstOrDefault(o => o != x && Together.Can(x, o));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.WagerHour(OwnHappenings.Hour(map))) return;
            var (x, y) = Pair(map);
            if (x == null || !OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.WagerChance)) return;
            Begin(b, g, map, x, y, false);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y, bool soon)
        {
            var s = OwnBetween.Begin(b, OwnHappening.RevelWager, g, map, x.Position, x, y, 16f);
            OwnBetween.Face(x, y, 400);
            OwnBetween.Say(x, OwnHappening.RevelWager, "wager", g, y);
            OwnBetween.Say(y, OwnHappening.RevelWager, "taken", g, x);
            OwnBetween.Show(g, map, x.Position);
            // Who loses: the luck of the night. a is kept as the winner, b as the loser.
            if (Rand.Bool)
            {
                s.a = y;
                s.b = x;
            }
            s.stage = 1;
            s.next = OwnBetween.Now + (soon ? 600 : Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 7)));
        }

        /// <summary>Somewhere in the winner's room for the loser to scrub (its filth first), else beside the winner's bed.</summary>
        private static LocalTargetInfo Floor(Pawn winner, Map map)
        {
            var room = winner.ownership?.OwnedRoom;
            if (room != null)
            {
                var filth = room.ContainedAndAdjacentThings.OfType<Filth>().FirstOrDefault(f => f.Spawned);
                if (filth != null) return filth;
                var c = room.Cells.Where(x => x.Standable(map)).RandomElementWithFallback(IntVec3.Invalid);
                if (c.IsValid) return c;
            }
            return winner.ownership?.OwnedBed != null ? (LocalTargetInfo)winner.ownership.OwnedBed.Position : LocalTargetInfo.Invalid;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var winner = s.a;
            var loser = s.b;
            if (winner == null || loser == null || winner.Dead || loser.Dead)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                // The morning: the loser goes to scrub the winner's floor.
                var floor = Floor(winner, map);
                if (!floor.IsValid || !OwnBetween.FreeNow(loser))
                {
                    if (now > s.until || !floor.IsValid) s.stage = -1;
                    OwnBetween.Wait(s, 500);
                    return;
                }
                if (floor.Thing is Filth f) OwnHappenings.Job(loser, JobMaker.MakeJob(JobDefOf.Clean, f));
                else OwnBetween.Send(loser, map, floor.Cell, 2f, 1);
                s.at = floor.Cell;
                s.stage = 2;
                OwnBetween.Wait(s, 250);
                return;
            }
            if (!OwnBetween.Still(loser, map))
            {
                s.stage = -1;
                return;
            }
            if (loser.Position.InHorDistOf(s.at, 3f))
            {
                if (loser.CurJobDef != JobDefOf.Clean) OwnHappenings.Stand(loser, s.at, 900);
                FleckMaker.ThrowDustPuff(s.at, map, 0.4f);
                OwnBetween.Say(loser, H, "scrub", g, winner);
                var o = OwnBetween.Opinion(loser, winner) >= OwnHappeningsMath.WagerGoodSport ? BetweenOutcome.Closer : BetweenOutcome.Laughed;
                OwnBetween.Card(H, g, map, s.at, false, null, loser.Named("A"), winner.Named("B"));
                if (o == BetweenOutcome.Closer && OwnBetween.Still(winner, map) && Together.Can(loser, winner)) OwnBetween.End(H, o, loser, winner);
                else if (o == BetweenOutcome.Laughed) OwnBetween.End(H, o, loser, winner);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, loser, winner.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (loser.CurJobDef != JobDefOf.Goto && loser.CurJobDef != JobDefOf.Clean) OwnBetween.Send(loser, map, s.at, 1f, 1);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 150);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y) = Pair(map);
            if (x == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                x = people.FirstOrDefault();
                y = people.FirstOrDefault(p => p != x && x != null && Together.Can(x, p));
            }
            if (x == null || y == null) return "two grown of the colony together are needed.";
            Begin(b, g, map, x, y, true);
            return x.LabelShort + " and " + y.LabelShort + " make a wager; the loser scrubs the winner's floor shortly.";
        }
    }
}
