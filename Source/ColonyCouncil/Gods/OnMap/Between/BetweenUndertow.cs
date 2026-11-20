using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Seen whispering at the edge (OwnHappening.UndertowWhisper): late at night, one of the colony with a deep grudge against
    /// another, or at the end of their tether, walks out to a dark place at the edge and stands whispering into the dark. One
    /// of the colony awake sees it from a way off and keeps away from them after. Whatever listens there is never named.
    /// </summary>
    public class UndertowWhisper : BetweenKind
    {
        public override OwnHappening H => OwnHappening.UndertowWhisper;

        private static float Worst(Pawn p, Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(o => o != p && OwnHappenings.Ours(o)).Select(o => OwnBetween.Opinion(p, o)).DefaultIfEmpty(0f).Min();

        /// <summary>One awake late with a grudge or in despair, and one other awake to see it, and why.</summary>
        public static (Pawn whisperer, Pawn witness, GodPaceMath.Trigger why) Scene(Map map)
        {
            var awake = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && !p.InBed()).ToList();
            foreach (var p in awake)
            {
                var t = OwnHappeningsMath.WhisperTrigger(Worst(p, map), p.needs?.mood?.CurLevel ?? 1f, true);
                if (t == GodPaceMath.Trigger.None) continue;
                var w = awake.FirstOrDefault(o => o != p);
                if (w != null) return (p, w, t);
            }
            return (null, null, GodPaceMath.Trigger.None);
        }

        /// <summary>A dark place just outside the colony, under the sky.</summary>
        private static IntVec3 DarkEdge(Map map, Pawn p)
        {
            for (int i = 0; i < 8; i++)
            {
                if (!RCellFinder.TryFindRandomSpotJustOutsideColony(p, out var c)) break;
                if (!c.Roofed(map) && map.glowGrid.GroundGlowAt(c) < 0.3f) return c;
            }
            return GodOmens.Open(map);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.DeepNight(OwnHappenings.Hour(map))) return;
            var (whisperer, witness, why) = Scene(map);
            if (whisperer == null || !OwnHappenings.Calls(b, H, why, g, map, now)) return;
            Begin(b, g, map, whisperer, witness);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn whisperer, Pawn witness)
        {
            var edge = DarkEdge(map, whisperer);
            if (!edge.IsValid || !OwnBetween.Send(whisperer, map, edge, 2f, 1)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.UndertowWhisper, g, map, edge, whisperer, witness, 4f);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (!OwnBetween.Still(s.a, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (OwnBetween.There(s.a, s.at, 3f))
                {
                    var outward = s.at + (s.at - OwnHappenings.Middle(map));
                    OwnHappenings.Stand(s.a, outward, 1200);
                    OwnBetween.Show(g, map, s.at, true);
                    // The other, awake, is drawn a way toward it and sees.
                    var near = s.b != null && OwnBetween.Still(s.b, map) ? CellFinder.RandomClosewalkCellNear(s.at, map, 10, c => c.Standable(map) && !c.InHorDistOf(s.at, 6f)) : IntVec3.Invalid;
                    if (!near.IsValid || !OwnHappenings.GoTo(s.b, near, GenDate.TicksPerHour))
                    {
                        s.stage = -1;
                        return;
                    }
                    s.stage = 2;
                }
                else if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            if (!OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.b.Position.InHorDistOf(s.at, 12f) && s.b.CurJobDef != JobDefOf.Goto)
            {
                OwnHappenings.Stand(s.b, s.at, 300);
                OwnBetween.Show(g, map, s.at, true);
                OwnBetween.Say(s.b, H, "saw", g, null, s.a.Named("WHISPERER"));
                if (s.b.Position.InHorDistOf(s.a.Position, 8f) && Together.Can(s.a, s.b)) OwnBetween.Say(s.a, H, "caught", g, s.b);
                OwnBetween.End(H, BetweenOutcome.Wary, s.b, s.a);
                OwnBetween.Card(H, g, map, s.at, true, null, s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.b, s.a.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (whisperer, witness, _) = Scene(map);
            if (whisperer == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count < 2) return "two grown of the colony are needed.";
                whisperer = two[0];
                witness = two[1];
            }
            return Begin(b, g, map, whisperer, witness) ? whisperer.LabelShort + " walks out to whisper at the edge; " + witness.LabelShort + " sees." : "no dark place outside the colony to walk to.";
        }
    }

    /// <summary>
    /// The light let go out (OwnHappening.UndertowLampOut): in the small hours a lamp, torch or brazier by where someone sleeps
    /// goes out, its fuel gone. In the morning the sleeper and another walk to it, and unless they are good friends they argue
    /// over whose it was to keep it lit. A light let go out is what the dark likes best, the stories say; nobody says it here.
    /// </summary>
    public class UndertowLampOut : BetweenKind
    {
        public override OwnHappening H => OwnHappening.UndertowLampOut;

        /// <summary>A lit fuelled light near one of the colony asleep, and the sleeper.</summary>
        public static (Building light, Pawn sleeper) Scene(Map map)
        {
            var sleepers = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && p.InBed() && !p.Awake()).ToList();
            if (sleepers.Count == 0) return (null, null);
            foreach (var t in map.listerBuildings.allBuildingsColonist)
            {
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel == null || !fuel.HasFuel || t.TryGetComp<CompGlower>() == null) continue;
                var p = sleepers.FirstOrDefault(x => x.Position.InHorDistOf(t.Position, 6f));
                if (p != null) return (t, p);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.DeepNight(OwnHappenings.Hour(map))) return;
            var (light, sleeper) = Scene(map);
            if (light == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, light, sleeper, false);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Building light, Pawn sleeper, bool soon)
        {
            var fuel = light.TryGetComp<CompRefuelable>();
            fuel?.ConsumeFuel(fuel.Fuel);
            FleckMaker.ThrowSmoke(light.TrueCenter(), map, 0.8f);
            OwnBetween.Show(g, map, light.Position, true);
            var s = OwnBetween.Begin(b, OwnHappening.UndertowLampOut, g, map, light.Position, sleeper, null, 14f);
            s.thing = light;
            s.word = light.LabelNoCount;
            OwnBetween.Card(OwnHappening.UndertowLampOut, g, map, light.Position, false, null, sleeper.Named("A"), s.word.Named("LIGHT"));
            s.stage = 1;
            s.next = OwnBetween.Now + (soon ? 60 : Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 7)));
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.thing == null || !s.thing.Spawned)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                var sleeper = s.a;
                var other = OwnBetween.People(map).Where(p => p != sleeper && OwnBetween.FreeNow(p) && OwnBetween.Grown(p))
                    .OrderBy(p => (p.ownership?.OwnedBed?.Position ?? p.Position).DistanceToSquared(s.at)).FirstOrDefault();
                if (!OwnBetween.FreeNow(sleeper) || other == null)
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 500);
                    return;
                }
                if (!OwnBetween.Send(sleeper, map, s.at, 2f, 2) || !OwnBetween.Send(other, map, s.at, 2f, 2))
                {
                    s.stage = -1;
                    return;
                }
                s.b = other;
                s.stage = 2;
                OwnBetween.Wait(s, 120);
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (OwnBetween.There(s.a, s.at, 4f) && OwnBetween.There(s.b, s.at, 4f) && Together.Can(s.a, s.b))
            {
                var light = (s.word ?? "").Named("LIGHT");
                OwnBetween.Face(s.a, s.b, 300);
                OwnBetween.Say(s.a, H, "who", g, s.b, light);
                bool friends = OwnBetween.Opinion(s.a, s.b) >= 30f && OwnBetween.Opinion(s.b, s.a) >= 30f;
                OwnBetween.Say(s.b, H, friends ? "laugh" : "notme", g, s.a, light);
                if (!friends) OwnBetween.End(H, BetweenOutcome.Quarrel, s.a, s.b, null, null, light);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, light);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (light, sleeper) = Scene(map);
            if (light == null)
            {
                light = map.listerBuildings.allBuildingsColonist.FirstOrDefault(t => t.TryGetComp<CompRefuelable>()?.HasFuel == true && t.TryGetComp<CompGlower>() != null);
                sleeper = light == null ? null : OwnBetween.People(map).Where(OwnBetween.Grown).OrderBy(p => p.Position.DistanceToSquared(light.Position)).FirstOrDefault();
            }
            if (light == null || sleeper == null) return "no lit fuelled light (a torch, a lamp, a brazier) and someone near it.";
            Begin(b, g, map, light, sleeper, true);
            return "the " + light.LabelNoCount + " near " + sleeper.LabelShort + " goes out; the argument comes in a moment.";
        }
    }
}
