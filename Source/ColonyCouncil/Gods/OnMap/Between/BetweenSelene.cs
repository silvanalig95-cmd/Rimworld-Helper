using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Sorry in the morning (OwnHappening.SeleneSorry): one of the colony said something cruel to another today (the game's
    /// insult), and in the night, under a moon at least half full, they are out of doors standing in the moonlight, unable to
    /// settle. In the morning they go to the one they hurt and say sorry: taken, the two make it up; refused, it is worse. She
    /// gives back what was done by day, the stories say.
    /// </summary>
    public class SeleneSorry : BetweenKind
    {
        public override OwnHappening H => OwnHappening.SeleneSorry;

        private static readonly Color Silver = new Color(0.82f, 0.88f, 1f);

        /// <summary>One awake tonight who insulted another of the colony lately, the one insulted, and the insult's mark.</summary>
        public static (Pawn insulter, Pawn victim, string mark) Insult(OwnHappeningsBook b, Map map)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
            if (def == null) return (null, null, null);
            int window = Mathf.RoundToInt(OwnHappeningsMath.SorryDays * GenDate.TicksPerDay);
            foreach (var victim in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours))
            {
                var mems = victim.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                {
                    if (m.def != def || !(m is Thought_MemorySocial s) || s.otherPawn == null || s.age > window) continue;
                    var insulter = s.otherPawn;
                    if (!OwnBetween.Able(insulter) || insulter.Map != map || insulter.InBed() || !OwnBetween.Grown(insulter)) continue;
                    string mark = "S|" + victim.thingIDNumber + "|" + insulter.thingIDNumber + "|" + (OwnBetween.Now - s.age) / GenDate.TicksPerHour;
                    if (b.between.answered.Contains(mark)) continue;
                    return (insulter, victim, mark);
                }
            }
            return (null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (!(hour >= 22 || hour < 3) || !OwnHappeningsMath.MoonEnough((int)GodOmens.MoonTonight)) return;
            var (insulter, victim, mark) = Insult(b, map);
            if (insulter == null) return;
            b.between.answered.Add(mark);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.SorryChance)) return;
            Begin(b, g, map, insulter, victim, false);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn insulter, Pawn victim, bool soon)
        {
            var spot = CellFinder.RandomClosewalkCellNear(insulter.Position, map, 12, c => c.Standable(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null);
            if (!spot.IsValid || !OwnBetween.Send(insulter, map, spot, 1f, 1)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.SeleneSorry, g, map, spot, insulter, victim, 16f);
            s.stage = 1;
            s.num = soon ? 1 : 0;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.a == null || s.a.Dead || s.b == null || s.b.Dead)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                // Out under the moon, unable to settle.
                if (OwnBetween.Still(s.a, map) && OwnBetween.There(s.a, s.at))
                {
                    OwnHappenings.Stand(s.a, s.at + IntVec3.North * 6, 1500);
                    OwnHappenings.Glow(map, s.a.Position, Silver, 6, 0.8f, 0.4f);
                    OwnBetween.Show(g, map, s.a.Position);
                    s.stage = 2;
                    s.next = OwnBetween.Now + (s.num == 1 ? 600 : Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 7)));
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            if (s.stage == 2)
            {
                // The morning: they go to the one they hurt.
                if (OwnBetween.FreeNow(s.a) && OwnBetween.Still(s.b, map) && OwnBetween.Send(s.a, map, s.b.Position, 2f, 2))
                {
                    s.stage = 3;
                    OwnBetween.Wait(s, 120);
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 500);
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.a.Position.InHorDistOf(s.b.Position, 4f) && s.a.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.a, s.b, 400);
                OwnBetween.Say(s.a, H, "sorry", g, s.b);
                var o = OwnHappeningsMath.Sorry(OwnBetween.Opinion(s.b, s.a));
                OwnBetween.Say(s.b, H, o == BetweenOutcome.MadeUp ? "accept" : "refuse", g, s.a);
                OwnBetween.End(H, o, s.a, s.b);
                OwnBetween.Card(H, g, map, s.a.Position, false, o == BetweenOutcome.MadeUp ? null : "TextRefused", s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, s.b.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (s.a.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.a, map, s.b.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (insulter, victim, _) = Insult(b, map);
            if (insulter == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count < 2) return "two grown of the colony are needed.";
                insulter = two[0];
                victim = two[1];
            }
            return Begin(b, g, map, insulter, victim, true) ? insulter.LabelShort + " stands out under the moon, and then goes to say sorry to " + victim.LabelShort + "."
                : "no open ground near them.";
        }
    }

    /// <summary>
    /// The same dream at breakfast (OwnHappening.SeleneSameDream): in the morning two of the colony, rested, find they
    /// dreamt the same dream, and tell it over breakfast. Two who like each other come away closer; two who don't are
    /// uneasy about it. She notices those who tell their dreams, the believers say.
    /// </summary>
    public class SeleneSameDream : BetweenKind
    {
        public override OwnHappening H => OwnHappening.SeleneSameDream;

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var rested = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && !p.InBed() && (p.needs?.rest?.CurLevel ?? 0f) >= 0.75f).ToList();
            foreach (var x in rested.InRandomOrder())
            {
                var y = rested.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, 10f) && Together.Can(x, o));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.BreakfastFrom || hour >= OwnHappeningsMath.BreakfastTo) return;
            var (x, y) = Pair(map);
            if (x == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Play(b, g, map, x, y);
        }

        private static void Play(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            OwnHappenings.Came(b, OwnHappening.SeleneSameDream, OwnBetween.Now, null, x.LabelShort);
            OwnBetween.Face(x, y, 600);
            OwnBetween.Say(x, OwnHappening.SeleneSameDream, "dream", g, y);
            var o = OwnHappeningsMath.SameDream(OwnBetween.Opinion(x, y), OwnBetween.Opinion(y, x));
            OwnBetween.Say(y, OwnHappening.SeleneSameDream, o == BetweenOutcome.Closer ? "same" : "uneasy", g, x);
            OwnBetween.Show(g, map, x.Position);
            OwnBetween.End(OwnHappening.SeleneSameDream, o, x, y);
            OwnBetween.Card(OwnHappening.SeleneSameDream, g, map, x.Position, false, null, x.Named("A"), y.Named("B"));
            OwnBetween.Book(OwnHappening.SeleneSameDream, g, map, x.Position, GodActKind.Omen, x, y.LabelShort.Named("OTHER"));
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
            if (x == null || y == null) return "two grown of the colony together (near each other) are needed.";
            Play(b, g, map, x, y);
            return x.LabelShort + " and " + y.LabelShort + " tell the same dream.";
        }
    }
}
