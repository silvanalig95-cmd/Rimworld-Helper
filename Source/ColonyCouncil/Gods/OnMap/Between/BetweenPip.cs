using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// It wanted both of them (OwnHappening.PipTwoHands): a machine of the colony's has stood broken down for hours with nobody
    /// at it. Two of the colony (one who holds the Hum, else the best builder, and another) walk over and lay their hands on it
    /// together, and it hums back to life with a spark, no part used. Whether they come away closer or arguing over it is by
    /// what each believes.
    /// </summary>
    public class PipTwoHands : BetweenKind
    {
        public override OwnHappening H => OwnHappening.PipTwoHands;

        /// <summary>When each machine was first seen broken down (this session; a machine mended is let go).</summary>
        private static readonly Dictionary<int, int> brokenSince = new Dictionary<int, int>();

        /// <summary>A machine of the colony's broken down with nobody at it, and how many hours it has stood so.</summary>
        public static (Building machine, float hours) Broken(Map map)
        {
            int now = OwnBetween.Now;
            Building best = null;
            float bestHours = -1f;
            foreach (var t in map.listerBuildings.allBuildingsColonist)
            {
                var br = t.TryGetComp<CompBreakdownable>();
                if (br == null) continue;
                if (!br.BrokenDown)
                {
                    brokenSince.Remove(t.thingIDNumber);
                    continue;
                }
                if (!brokenSince.TryGetValue(t.thingIDNumber, out int since)) brokenSince[t.thingIDNumber] = since = now;
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJob != null && p.CurJob.targetA.Thing == t)) continue;
                float hours = (now - since) / (float)GenDate.TicksPerHour;
                if (hours > bestHours)
                {
                    best = t;
                    bestHours = hours;
                }
            }
            return (best, bestHours);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            var (machine, hours) = Broken(map);
            if (machine == null) return;
            var t = OwnHappeningsMath.TwoHandsTrigger(hours, false);
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, H, t, g, map, now)) return;
            Begin(b, g, map, machine);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Building machine)
        {
            var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
            var first = OwnBetween.Believer(people, g) ?? people.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).FirstOrDefault();
            var second = people.Where(p => p != first).OrderBy(p => p.Position.DistanceToSquared(machine.Position)).FirstOrDefault();
            if (first == null || second == null || !OwnBetween.Send(first, map, machine.Position, 2f, 2) || !OwnBetween.Send(second, map, machine.Position, 2f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.PipTwoHands, g, map, machine.Position, first, second, 3f);
            s.thing = machine;
            s.word = machine.LabelNoCount;
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var br = s.thing?.TryGetComp<CompBreakdownable>();
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map) || br == null || !s.thing.Spawned || !br.BrokenDown)
            {
                s.stage = -1;
                return;
            }
            if (OwnBetween.There(s.a, s.at, 3f) && OwnBetween.There(s.b, s.at, 3f) && Together.Can(s.a, s.b))
            {
                OwnHappenings.Stand(s.a, s.at, 300);
                OwnHappenings.Stand(s.b, s.at, 300);
                br.Notify_Repaired();
                var loc = s.at.ToVector3Shifted();
                for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(loc + Rand.InsideUnitCircleVec3 * 0.5f, map);
                OwnHappenings.Sound(map, s.at, 0.6f, "Power_OnSmall");
                OwnBetween.Show(g, map, s.at);
                var what = (s.word ?? "").Named("MACHINE");
                var sa = OwnBetween.StanceOf(s.a, g);
                var sb = OwnBetween.StanceOf(s.b, g);
                OwnBetween.Say(s.a, H, sa == BetweenStance.Doubts ? "doubt" : "hands", g, s.b, what);
                string reply = OwnBetween.Say(s.b, H, sb == BetweenStance.Doubts ? "doubt" : sb == BetweenStance.Holds ? "believe" : "plain", g, s.a, what);
                var o = OwnHappeningsMath.Shared(sa, sb);
                if (o == BetweenOutcome.None) o = BetweenOutcome.Closer;
                OwnBetween.End(H, o, s.a, s.b, OwnBetween.HeldBy(s.a), OwnBetween.HeldBy(s.b), what);
                OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"), what);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, s.a, s.b.LabelShort.Named("OTHER"), what);
                if (sb == BetweenStance.Doubts) OwnBetween.Doubted(s.b, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var machine = Broken(map).machine;
            if (machine == null)
            {
                machine = map.listerBuildings.allBuildingsColonist.FirstOrDefault(t => t.TryGetComp<CompBreakdownable>() != null);
                machine?.TryGetComp<CompBreakdownable>()?.DoBreakdown();
            }
            if (machine == null) return "no machine here that can break down.";
            return Begin(b, g, map, machine) ? "two walk to the " + machine.LabelNoCount + " and lay their hands on it." : "two free of the colony are needed.";
        }
    }

    /// <summary>
    /// Talking to the machine (OwnHappening.PipTalked): one who holds the Hum is at a machine or a bench and talks to it, and
    /// a doubter near walks over and laughs; the machine hums and its light brightens for the one who talked to it, and the
    /// doubter goes quiet. A good sport takes the laugh well; else it stings.
    /// </summary>
    public class PipTalked : BetweenKind
    {
        public override OwnHappening H => OwnHappening.PipTalked;

        /// <summary>One who holds the Hum at work at a machine or a bench, the machine, and a doubter near.</summary>
        public static (Pawn talker, Building machine, Pawn doubter) Scene(Map map, God g)
        {
            var people = OwnBetween.People(map);
            foreach (var p in people.Where(p => OwnBetween.Holds(p, g) && OwnBetween.Grown(p)))
            {
                if (!(p.CurJob?.targetA.Thing is Building m) || m.Faction != Faction.OfPlayer || m.TryGetComp<CompPowerTrader>() == null && !(m is Building_WorkTable)) continue;
                var d = people.FirstOrDefault(x => x != p && OwnBetween.Doubts(x) && OwnBetween.Grown(x) && OwnBetween.FreeNow(x) && x.Position.InHorDistOf(p.Position, OwnHappeningsMath.TalkedWithin));
                if (d != null) return (p, m, d);
            }
            return (null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 8 || hour > 20) return;
            var (talker, machine, doubter) = Scene(map, g);
            if (talker == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, talker, machine, doubter);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn talker, Building machine, Pawn doubter)
        {
            var s = OwnBetween.Begin(b, OwnHappening.PipTalked, g, map, machine.Position, talker, doubter, 2f);
            s.thing = machine;
            s.word = machine.LabelNoCount;
            OwnHappenings.Stand(talker, machine.Position, 600);
            OwnBetween.Say(talker, OwnHappening.PipTalked, "talk", g, null, s.word.Named("MACHINE"));
            OwnBetween.Send(doubter, map, talker.Position, 1f, 2);
            s.stage = 1;
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
            if (s.b.Position.InHorDistOf(s.a.Position, 4f) && s.b.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                var what = (s.word ?? "").Named("MACHINE");
                OwnBetween.Face(s.b, s.a, 300);
                string mock = OwnBetween.Say(s.b, H, "mock", g, s.a, what);
                // The machine hums for the one who talked to it.
                OwnHappenings.Sound(map, s.at, 0.6f, "Power_OnSmall");
                OwnHappenings.Glow(map, s.at, s.God?.colour ?? new Color(0.6f, 0.85f, 1f), 8, 0.8f, 0.4f);
                OwnBetween.Show(g, map, s.at);
                OwnBetween.Say(s.a, H, "hum", g, s.b, what);
                OwnBetween.Shaken(s.b, g);
                var o = OwnHappeningsMath.Laugh(OwnBetween.Opinion(s.a, s.b));
                OwnBetween.End(H, o, s.a, s.b, null, null, what);
                OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"), what);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, s.a, s.b.LabelShort.Named("OTHER"), what);
                OwnBetween.Doubted(s.b, g, mock);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (talker, machine, doubter) = Scene(map, g);
            if (talker == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                talker = people.FirstOrDefault();
                doubter = people.FirstOrDefault(p => p != talker);
                machine = map.listerBuildings.allBuildingsColonist.FirstOrDefault(m => m.TryGetComp<CompPowerTrader>() != null || m is Building_WorkTable);
            }
            if (talker == null || doubter == null || machine == null) return "two of the colony and a machine or bench are needed.";
            Begin(b, g, map, talker, machine, doubter);
            return talker.LabelShort + " talks to the " + machine.LabelNoCount + "; " + doubter.LabelShort + " comes over to laugh.";
        }
    }
}
