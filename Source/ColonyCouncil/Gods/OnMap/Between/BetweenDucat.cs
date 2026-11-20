using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A coin passed on (OwnHappening.DucatPassed): a gold coin lies on the ground by one of the colony, who picks it up and
    /// carries it to the one at home lowest in spirits; the next day that one passes it to another, and the third sets it in
    /// the stores. Each who was given it won't forget the giver. Gold that wants to move, the believers say; it never stays.
    /// </summary>
    public class DucatPassed : BetweenKind
    {
        public override OwnHappening H => OwnHappening.DucatPassed;

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 9 || hour > 17 || DefDatabase<ThingDef>.GetNamedSilentFail("Gold") == null) return;
            var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
            if (people.Count < 3 || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, people.RandomElement());
        }

        /// <summary>The one at home lowest in spirits of those not yet given it (not these), or null.</summary>
        private static Pawn Lowest(Map map, params Pawn[] not) =>
            OwnBetween.People(map).Where(p => !not.Contains(p) && OwnBetween.Grown(p)).OrderBy(p => p.needs?.mood?.CurLevel ?? 1f).FirstOrDefault();

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn finder)
        {
            var gold = DefDatabase<ThingDef>.GetNamedSilentFail("Gold");
            var to = Lowest(map, finder);
            var at = OwnHappenings.Beside(map, finder.Position, finder);
            if (gold == null || to == null || !at.IsValid) return false;
            var coin = ThingMaker.MakeThing(gold);
            coin.stackCount = 1;
            if (!GenPlace.TryPlaceThing(coin, at, map, ThingPlaceMode.Near, out var placed)) return false;
            OwnHappenings.Glow(map, placed.Position, new Color(1f, 0.85f, 0.3f), 4, 0.4f, 0.3f);
            var s = OwnBetween.Begin(b, OwnHappening.DucatPassed, g, map, placed.Position, finder, to, 40f);
            s.thing = placed;
            s.num = 1;
            OwnBetween.Say(finder, OwnHappening.DucatPassed, "found", g);
            OwnBetween.Card(OwnHappening.DucatPassed, g, map, placed.Position, false, null, finder.Named("A"), to.Named("B"));
            OwnBetween.Book(OwnHappening.DucatPassed, g, map, placed.Position, GodActKind.Act, finder, to.LabelShort.Named("OTHER"));
            Hand(s, map);
            return true;
        }

        /// <summary>The one holding it now (a) carries it to the next (b).</summary>
        private static void Hand(BetweenScene s, Map map)
        {
            var beside = OwnHappenings.Beside(map, s.b.Position, s.a);
            if (!beside.IsValid || s.thing == null || !s.thing.Spawned || !OwnHappenings.Carry(s.a, s.thing, beside, 1))
            {
                s.stage = -1;
                return;
            }
            s.stage = 1;
            s.until = OwnBetween.Now + GenDate.TicksPerHour * 4;
            OwnBetween.Wait(s, 250);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.thing == null || s.thing.Destroyed)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
                {
                    s.stage = -1;
                    return;
                }
                bool carrying = s.a.CurJobDef == JobDefOf.HaulToCell;
                if (!carrying && s.thing.Spawned && s.a.Position.InHorDistOf(s.b.Position, 4f) && Together.Can(s.a, s.b))
                {
                    OwnBetween.Face(s.a, s.b, 300);
                    OwnBetween.Say(s.a, H, "give", g, s.b);
                    OwnBetween.Say(s.b, H, "thanks", g, s.a);
                    OwnBetween.End(H, BetweenOutcome.Kind, s.b, s.a);
                    // A day on, it is passed again (or, the last hand reached, set in the stores).
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerDay * 2;
                    OwnBetween.Wait(s, GenDate.TicksPerDay);
                    return;
                }
                if (!carrying && !s.a.Position.InHorDistOf(s.b.Position, 4f)) OwnBetween.Send(s.a, map, s.b.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            // The next hand.
            var holder = s.b;
            if (!OwnBetween.FreeNow(holder) || !s.thing.Spawned)
            {
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 1000);
                return;
            }
            if (s.num + 1 >= OwnHappeningsMath.CoinHands)
            {
                // The last: set in the stores, as the game hauls it, with a word.
                OwnBetween.Say(holder, H, "stores", g);
                OwnBetween.Card(H, g, map, s.thing.Position, false, "TextEnd", holder.Named("A"));
                s.stage = -1;
                return;
            }
            var next = Lowest(map, s.a, holder);
            if (next == null)
            {
                s.stage = -1;
                return;
            }
            s.a = holder;
            s.b = next;
            s.num++;
            Hand(s, map);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var finder = OwnBetween.People(map).FirstOrDefault(OwnBetween.Grown);
            if (finder == null) return "one of the colony is needed.";
            return Begin(b, g, map, finder) ? "a gold coin by " + finder.LabelShort + ", carried to the one lowest in spirits." : "nobody else to carry it to, or nowhere to lay it.";
        }
    }

    /// <summary>
    /// The miser's silver scattered (OwnHappening.DucatMiser): one of the colony with a greedy heart sits up late counting the
    /// colony's silver in the stores, and in the morning the silver is found scattered across the floor in little heaps
    /// (nothing lost). Someone comes and laughs at them; the miser is ashamed, or, if they believe none of it, takes it badly.
    /// Ducat despises a miser, the stories say.
    /// </summary>
    public class DucatMiser : BetweenKind
    {
        public override OwnHappening H => OwnHappening.DucatMiser;

        public static Thing Silver(Map map) =>
            map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).OrderByDescending(t => t.stackCount).FirstOrDefault();

        private static bool Greedy(Pawn p)
        {
            var t = DefDatabase<TraitDef>.GetNamedSilentFail("Greedy");
            return t != null && p.story?.traits?.HasTrait(t) == true;
        }

        public static Pawn Miser(Map map) =>
            OwnBetween.People(map).FirstOrDefault(p => OwnBetween.Grown(p) && !p.InBed() && OwnBetween.FreeNow(p) && Greedy(p));

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 20 || hour > 23) return;
            var silver = Silver(map);
            if (silver == null || map.resourceCounter.Silver < OwnHappeningsMath.MiserSilverFrom) return;
            var miser = Miser(map);
            if (miser == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, miser, silver, false);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn miser, Thing silver, bool soon)
        {
            if (!OwnBetween.Send(miser, map, silver.Position, 2f, 1)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.DucatMiser, g, map, silver.Position, miser, null, 20f);
            s.thing = silver;
            s.num = soon ? 1 : 0;
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.stage == 1)
            {
                if (OwnBetween.Still(s.a, map) && OwnBetween.There(s.a, s.at, 2.5f))
                {
                    OwnHappenings.Stand(s.a, s.at, 1500);
                    OwnHappenings.Glow(map, s.at, new Color(0.9f, 0.9f, 0.95f), 3, 0.4f, 0.3f);
                    s.stage = 2;
                    s.next = now + (s.num == 1 ? 600 : Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 6)));
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            if (s.stage == 2)
            {
                // The morning: the silver lies scattered in little heaps.
                var silver = s.thing != null && s.thing.Spawned ? s.thing : Silver(map);
                if (silver == null)
                {
                    s.stage = -1;
                    return;
                }
                int heaps = Mathf.Min(OwnHappeningsMath.MiserHeaps, silver.stackCount - 1);
                int each = Mathf.Max(1, silver.stackCount / (heaps + 1));
                var at = silver.Position;
                for (int i = 0; i < heaps && silver.stackCount > each; i++)
                {
                    var part = silver.SplitOff(each);
                    GenPlace.TryPlaceThing(part, at, map, ThingPlaceMode.Near);
                }
                OwnBetween.Show(g, map, at);
                OwnBetween.Card(H, g, map, at, false, null, s.a.Named("A"));
                s.at = at;
                s.stage = 3;
                s.until = now + GenDate.TicksPerHour * 8;
                OwnBetween.Wait(s, 500);
                return;
            }
            // Someone comes and laughs at them over it.
            if (s.b == null)
            {
                if (!OwnBetween.FreeNow(s.a))
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 500);
                    return;
                }
                var laugher = OwnBetween.People(map).Where(p => p != s.a && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).OrderByDescending(p => OwnBetween.Holds(p, g) ? 1 : 0)
                    .ThenBy(p => p.Position.DistanceToSquared(s.a.Position)).FirstOrDefault();
                if (laugher == null || !OwnBetween.Send(laugher, map, s.a.Position, 2f, 2))
                {
                    s.stage = -1;
                    return;
                }
                s.b = laugher;
                OwnBetween.Wait(s, 120);
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.b.Position.InHorDistOf(s.a.Position, 4f) && s.b.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.b, s.a, 300);
                OwnBetween.Say(s.b, H, OwnBetween.Holds(s.b, g) ? "believe" : "laugh", g, s.a);
                bool doubts = OwnBetween.Doubts(s.a);
                OwnBetween.Say(s.a, H, doubts ? "angry" : "ashamed", g, s.b);
                OwnBetween.End(H, doubts ? BetweenOutcome.Laughed : BetweenOutcome.Ashamed, s.a, s.b);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, s.b, s.a.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (s.b.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.b, map, s.a.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var silver = Silver(map);
            var miser = Miser(map) ?? OwnBetween.People(map).FirstOrDefault(OwnBetween.Grown);
            if (silver == null || miser == null) return "silver in the stores and one of the colony are needed.";
            return Begin(b, g, map, miser, silver, true) ? miser.LabelShort + " counts the silver; it is scattered shortly after." : "they can't reach the silver.";
        }
    }
}
