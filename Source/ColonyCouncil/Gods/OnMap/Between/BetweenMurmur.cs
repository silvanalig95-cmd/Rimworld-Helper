using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A splash at the water (OwnHappening.MurmurSplash): on a hot afternoon two of the colony walk down to the still water near
    /// home, and one splashes the other; the frogs set up croaking (Odyssey's bullfrogs come up out of the reeds where it is
    /// loaded). A good sport splashes back and the two laugh; one who isn't takes offence, like the god of the water himself.
    /// </summary>
    public class MurmurSplash : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MurmurSplash;

        /// <summary>A standable cell at the still water's edge near the colony, or Invalid.</summary>
        public static IntVec3 Edge(Map map)
        {
            var w = OwnSelene.StillWater(map);
            if (!w.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(w, 4f, true))
                if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.GetTerrain(map).IsWater) return c;
            return w.Standable(map) ? w : IntVec3.Invalid;
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 13 || hour > 18 || OwnHappenings.Raining(map) || map.mapTemperature.OutdoorTemp < OwnHappeningsMath.SplashFrom) return;
            var edge = Edge(map);
            if (!edge.IsValid || !edge.InHorDistOf(OwnHappenings.Middle(map), OwnHappeningsMath.SplashWater)) return;
            var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p)).ToList();
            if (people.Count < 2 || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            var x = OwnBetween.Believer(people, g) ?? people.RandomElement();
            var y = people.Where(p => p != x).OrderByDescending(p => OwnBetween.Opinion(x, p)).First();
            Begin(b, g, map, edge, x, y);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, IntVec3 edge, Pawn x, Pawn y)
        {
            if (!OwnBetween.Send(x, map, edge, 2f, 2) || !OwnBetween.Send(y, map, edge, 2f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.MurmurSplash, g, map, edge, x, y, 3f);
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
            if (OwnBetween.There(s.a, s.at) && OwnBetween.There(s.b, s.at) && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.a, s.b, 800);
                for (int i = 0; i < 4; i++) FleckMaker.WaterSplash(s.b.DrawPos + Rand.InsideUnitCircleVec3 * 0.6f, map, 1.4f, 2f);
                OwnBetween.Say(s.a, H, "splash", g, s.b);
                int frogs = Frogs(map, s.at);
                OwnBetween.Show(g, map, s.at);
                var o = OwnHappeningsMath.Laugh(OwnBetween.Opinion(s.b, s.a));
                OwnBetween.Say(s.b, H, o == BetweenOutcome.Closer ? "back" : "offended", g, s.a);
                if (o == BetweenOutcome.Closer)
                    for (int i = 0; i < 3; i++) FleckMaker.WaterSplash(s.a.DrawPos + Rand.InsideUnitCircleVec3 * 0.6f, map, 1.4f, 2f);
                OwnBetween.End(H, o, s.b, s.a);
                OwnBetween.Card(H, g, map, s.at, false, frogs > 0 ? null : "TextNoFrogs", s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, s.a, s.b.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        /// <summary>A couple of frogs up out of the reeds by the water, croaking (Odyssey's bullfrogs), gone by evening; how many came.</summary>
        internal static int Frogs(Map map, IntVec3 at)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(OwnMurmur.FrogKind);
            int came = 0;
            if (kind != null)
                for (int i = 0; i < OwnHappeningsMath.SplashFrogs; i++)
                {
                    var c = CellFinder.RandomClosewalkCellNear(at, map, 4, x => x.Standable(map) && x.GetFirstPawn(map) == null);
                    if (!c.IsValid) continue;
                    try
                    {
                        var f = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)), c, map);
                        OwnHappenings.SendOff(f, GenDate.TicksPerHour * 4);
                        came++;
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods between them: the frogs by the water", e);
                        break;
                    }
                }
            OwnHappenings.Sound(map, at, 0.8f, "Pawn_Frog_Call", "AG_Pawn_Frog_Call");
            return came;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var edge = Edge(map);
            var two = OwnBetween.People(map).Take(2).ToList();
            if (!edge.IsValid || two.Count < 2) return "still water near the colony and two of the colony are needed.";
            return Begin(b, g, map, edge, two[0], two[1]) ? two[0].LabelShort + " and " + two[1].LabelShort + " walk down to the water." : "they can't reach the water.";
        }
    }

    /// <summary>
    /// A frog in the bedroom (OwnHappening.MurmurFrogIn): in the rain a frog (Odyssey's bullfrog; it never comes without it)
    /// turns up in one of the colony's bedrooms. Whoever sleeps there and one other come to look: one who holds Old Croak would
    /// leave it be, it being his; the other wants it out. Of one mind they laugh and see it out together; of two, they argue.
    /// Then it hops off out into the wet.
    /// </summary>
    public class MurmurFrogIn : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MurmurFrogIn;

        /// <summary>A bedroom of one of the colony at home, its sleeper and a cell in it for the frog.</summary>
        public static (Pawn sleeper, IntVec3 cell) Bedroom(Map map)
        {
            foreach (var p in OwnBetween.People(map).Where(OwnBetween.Grown).InRandomOrder())
            {
                var room = p.ownership?.OwnedRoom;
                if (room == null || room.PsychologicallyOutdoors) continue;
                var c = room.Cells.Where(x => x.Standable(map) && x.GetFirstPawn(map) == null && x.GetFirstBuilding(map) == null).RandomElementWithFallback(IntVec3.Invalid);
                if (c.IsValid) return (p, c);
            }
            return (null, IntVec3.Invalid);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 7 || hour > 21 || !OwnHappenings.Raining(map) || DefDatabase<PawnKindDef>.GetNamedSilentFail(OwnMurmur.FrogKind) == null) return;
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.FrogInChance)) return;
            Begin(b, g, map);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(OwnMurmur.FrogKind);
            var (sleeper, cell) = Bedroom(map);
            if (kind == null || sleeper == null) return false;
            var room = sleeper.ownership.OwnedRoom;
            var other = room.Owners.FirstOrDefault(o => o != sleeper && OwnBetween.FreeNow(o))
                        ?? OwnBetween.People(map).Where(p => p != sleeper && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
            if (other == null || !OwnBetween.FreeNow(sleeper)) return false;
            Pawn frog;
            try
            {
                frog = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)), cell, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: a frog in the bedroom", e);
                return false;
            }
            OwnHappenings.Sound(map, cell, 0.8f, "Pawn_Frog_Call", "AG_Pawn_Frog_Call");
            var s = OwnBetween.Begin(b, OwnHappening.MurmurFrogIn, g, map, cell, sleeper, other, 3f);
            s.thing = frog;
            OwnBetween.Send(sleeper, map, cell, 2f, 2);
            OwnBetween.Send(other, map, cell, 2f, 2);
            OwnBetween.Card(OwnHappening.MurmurFrogIn, g, map, cell, false, null, sleeper.Named("A"));
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var frog = s.thing as Pawn;
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map) || frog == null || frog.Dead || !frog.Spawned)
            {
                Out(frog);
                s.stage = -1;
                return;
            }
            if (s.a.Position.InHorDistOf(frog.Position, 5f) && s.b.Position.InHorDistOf(frog.Position, 5f) && s.a.CurJobDef != JobDefOf.Goto && s.b.CurJobDef != JobDefOf.Goto
                && Together.Can(s.a, s.b))
            {
                bool aHolds = OwnBetween.Holds(s.a, g), bHolds = OwnBetween.Holds(s.b, g);
                OwnHappenings.Stand(s.a, frog.Position, 400);
                OwnHappenings.Stand(s.b, frog.Position, 400);
                OwnBetween.Say(s.a, H, aHolds ? "keep" : "out", g, s.b);
                OwnBetween.Say(s.b, H, aHolds == bHolds ? "agree" : bHolds ? "keep" : "out", g, s.a);
                var o = aHolds == bHolds ? BetweenOutcome.Closer : BetweenOutcome.Quarrel;
                OwnBetween.End(H, o, s.a, s.b, OwnBetween.HeldBy(s.a), OwnBetween.HeldBy(s.b));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, s.b.LabelShort.Named("OTHER"));
                Out(frog);
                s.stage = -1;
                return;
            }
            if (now > s.until)
            {
                Out(frog);
                s.stage = -1;
                return;
            }
            OwnBetween.Wait(s, 120);
        }

        /// <summary>The frog hops off out into the wet and away.</summary>
        private static void Out(Pawn frog)
        {
            if (frog == null || frog.Dead || !frog.Spawned) return;
            var map = frog.Map;
            var outside = CellFinder.RandomClosewalkCellNear(frog.Position, map, 12, c => c.Standable(map) && !c.Roofed(map));
            if (outside.IsValid) OwnHappenings.Lead(frog, outside, 900);
            OwnHappenings.SendOff(frog, 1000);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            if (DefDatabase<PawnKindDef>.GetNamedSilentFail(OwnMurmur.FrogKind) == null) return "no frogs without Odyssey (its bullfrog).";
            return Begin(b, g, map) ? "a frog in a bedroom; its sleeper and another come to look." : "no bedroom with its sleeper free, or nobody else free.";
        }
    }
}
