using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Hollin's saplings between them (OwnHappening.HollinBound): two of the colony fight (the game's angering fight), and
    /// at the next dawn two saplings stand side by side on open ground between where they sleep. The two are brought out to
    /// stand at them together: they make it up, unless one of them scoffs at it, which only sours it. Grave and fair, she
    /// binds what quarrels; nobody says so but those who believe it.
    /// </summary>
    public class HollinBound : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HollinBound;

        /// <summary>Two of the colony at this home who fought lately and haven't been answered yet: the two, and the fight's mark.</summary>
        public static (Pawn a, Pawn b, string mark) Fought(OwnHappeningsBook b, Map map)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("HadAngeringFight");
            if (def == null) return (null, null, null);
            int window = Mathf.RoundToInt(OwnHappeningsMath.BoundFightDays * GenDate.TicksPerDay);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours))
            {
                var mems = p.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                {
                    if (m.def != def || !(m is Thought_MemorySocial s) || s.otherPawn == null || s.age > window) continue;
                    var o = s.otherPawn;
                    if (!OwnHappenings.Ours(o) || o.Map != map) continue;
                    var ids = new[] { p.thingIDNumber, o.thingIDNumber }.OrderBy(x => x).ToArray();
                    string mark = ids[0] + "|" + ids[1] + "|" + (OwnBetween.Now - s.age) / GenDate.TicksPerHour;
                    if (b.between.answered.Contains(mark)) continue;
                    return (p, o, mark);
                }
            }
            return (null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            var (x, y, mark) = Fought(b, map);
            if (x == null) return;
            // One look for one fight.
            b.between.answered.Add(mark);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.BoundChance)) return;
            Start(b, g, map, x, y);
        }

        private static BetweenScene Start(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            var s = OwnBetween.Begin(b, OwnHappening.HollinBound, g, map, IntVec3.Invalid, x, y, 30f);
            // The saplings stand there at the next dawn.
            s.stage = 0;
            s.next = OwnBetween.Now + Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 5));
            return s;
        }

        /// <summary>Open soil between where the two sleep (else between the two now), near the colony.</summary>
        private static IntVec3 Place(Map map, Pawn x, Pawn y)
        {
            var p1 = x.ownership?.OwnedBed?.Position ?? x.Position;
            var p2 = y.ownership?.OwnedBed?.Position ?? y.Position;
            var mid = new IntVec3((p1.x + p2.x) / 2, 0, (p1.z + p2.z) / 2);
            foreach (var c in GenRadial.RadialCellsAround(mid, 22f, true))
            {
                if (!c.InBounds(map) || !PeoplesE.SaplingCell(map, c)) continue;
                var c2 = c + IntVec3.East;
                if (PeoplesE.SaplingCell(map, c2)) return c;
            }
            return IntVec3.Invalid;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.stage == 0)
            {
                s.at = Place(map, s.a, s.b);
                var def = s.at.IsValid ? PeoplesE.SaplingFor(map, s.at) : null;
                if (def == null)
                {
                    s.stage = -1;
                    return;
                }
                foreach (var c in new[] { s.at, s.at + IntVec3.East })
                {
                    if (!(GenSpawn.Spawn(def, c, map) is Plant p)) continue;
                    p.Growth = Rand.Range(0.12f, 0.2f);
                    FleckMaker.ThrowDustPuff(c, map, 0.5f);
                }
                OwnBetween.Show(g, map, s.at);
                OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"));
                s.stage = 1;
                s.until = now + GenDate.TicksPerHour * 12;
                OwnBetween.Wait(s, 250);
                return;
            }
            if (s.stage == 1)
            {
                // Both sent out to the saplings as soon as both are free; within the day.
                bool aFree = OwnBetween.FreeNow(s.a), bFree = OwnBetween.FreeNow(s.b);
                if (aFree && bFree && OwnBetween.Send(s.a, map, s.at, 3f, 2) && OwnBetween.Send(s.b, map, s.at, 3f, 2))
                {
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 3;
                }
                else if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 500);
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            bool aHere = OwnBetween.There(s.a, s.at), bHere = OwnBetween.There(s.b, s.at);
            if (aHere && bHere && Together.Can(s.a, s.b))
            {
                var sa = OwnBetween.StanceOf(s.a, g);
                var sb = OwnBetween.StanceOf(s.b, g);
                OwnBetween.Face(s.a, s.b, 600);
                OwnBetween.Say(s.a, H, Part(sa), g, s.b);
                string reply = OwnBetween.Say(s.b, H, Part(sb), g, s.a);
                var o = OwnHappeningsMath.Mend(sa, sb);
                OwnBetween.End(H, o, s.a, s.b, OwnBetween.HeldBy(s.a), OwnBetween.HeldBy(s.b));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, s.a, s.b.LabelShort.Named("OTHER"));
                if (sb == BetweenStance.Doubts) OwnBetween.Doubted(s.b, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        private static string Part(BetweenStance st) => st == BetweenStance.Holds ? "believe" : st == BetweenStance.Doubts ? "doubt" : "plain";

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y, _) = Fought(b, map);
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            x ??= people.FirstOrDefault();
            y ??= people.FirstOrDefault(p => p != x);
            if (x == null || y == null) return "two of the colony are needed.";
            var s = Start(b, g, map, x, y);
            s.next = OwnBetween.Now + 1;
            return "two saplings come up between " + x.LabelShort + " and " + y.LabelShort + "; the two go out to them when both are free.";
        }
    }

    /// <summary>
    /// Brambles round the stumps (OwnHappening.HollinStumps): one of the colony who felled many trees lately, while few were
    /// planted, finds brambles grown up round their last stump overnight, and someone who holds Hollin walks out there with
    /// them. Ashamed if they hold her or have no strong belief; a doubter, or one who holds her rival, argues instead.
    /// </summary>
    public class HollinStumps : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HollinStumps;

        /// <summary>A tree felled by one of the colony's people (OwnHappenings.Felled): counted for them, with its stump.</summary>
        internal static void Felled(OwnHappeningsBook b, Plant plant, Pawn by)
        {
            if (b?.between == null || by == null || plant == null) return;
            int now = OwnBetween.Now;
            var f = b.between.fellers.FirstOrDefault(x => x.pawn == by);
            if (f == null || now - f.since > OwnHappeningsMath.StumpsDays * GenDate.TicksPerDay)
            {
                b.between.fellers.RemoveAll(x => x.pawn == by);
                f = new BetweenFeller { pawn = by, since = now };
                b.between.fellers.Add(f);
                b.between.Trim();
            }
            f.felled++;
            f.lastAt = now;
            f.stump = plant.PositionHeld;
        }

        /// <summary>Young trees the colony set in its own fields lately (sown, still small).</summary>
        private static int Planted(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Count(t => t is Plant p && p.def.plant.IsTree && p.sown && p.Growth < 0.3f);

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 7 || hour > 17) return;
            var f = b.between.fellers.Where(x => x.pawn != null && x.pawn.Map == map && OwnBetween.Able(x.pawn) && x.stump.IsValid).OrderByDescending(x => x.felled).FirstOrDefault();
            if (f == null) return;
            var t = OwnHappeningsMath.StumpsTrigger(f.felled, Planted(map));
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, H, t, g, map, now)) return;
            Begin(b, g, map, f);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, BetweenFeller f)
        {
            var feller = f.pawn;
            var shower = OwnBetween.Believer(OwnBetween.People(map).Where(p => p != feller && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)), g);
            if (shower == null || !f.stump.InBounds(map)) return false;
            var bramble = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Brambles");
            if (bramble?.plant == null) return false;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(f.stump, 1.9f, true).Where(c => c.InBounds(map) && c.Standable(map) && c.GetPlant(map) == null && c.GetFirstItem(map) == null
                                                                                          && c.GetFirstBuilding(map) == null && c.GetTerrain(map).fertility > 0.3f).InRandomOrder())
            {
                if (n >= OwnHappeningsMath.StumpBrambles) break;
                if (!(GenSpawn.Spawn(bramble, c, map) is Plant p)) continue;
                p.Growth = Rand.Range(0.3f, 0.6f);
                n++;
            }
            if (n == 0) return false;
            var s = OwnBetween.Begin(b, OwnHappening.HollinStumps, g, map, f.stump, shower, feller, 4f);
            s.num = f.felled;
            OwnBetween.Show(g, map, f.stump, true);
            OwnBetween.Card(OwnHappening.HollinStumps, g, map, f.stump, false, null, feller.Named("A"), f.felled.Named("COUNT"));
            OwnBetween.Send(shower, map, f.stump, 3f, 2);
            OwnBetween.Send(feller, map, f.stump, 3f, 2);
            // Counted again from now.
            f.felled = 0;
            f.since = OwnBetween.Now;
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
            if (OwnBetween.There(s.a, s.at, 4f) && OwnBetween.There(s.b, s.at, 4f) && Together.Can(s.a, s.b))
            {
                var st = OwnBetween.StanceOf(s.b, g);
                OwnBetween.Face(s.a, s.b, 500);
                OwnBetween.Say(s.a, H, "show", g, s.b);
                string reply = OwnBetween.Say(s.b, H, OwnHappeningsMath.Against(st) ? "doubt" : "ashamed", g, s.a);
                var o = OwnHappeningsMath.Shown(st);
                if (o == BetweenOutcome.Ashamed) OwnBetween.End(H, o, s.b, s.a);
                else OwnBetween.End(H, o, s.a, s.b, g, OwnBetween.HeldBy(s.b));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Wrath, s.a, s.b.LabelShort.Named("OTHER"));
                if (st == BetweenStance.Doubts) OwnBetween.Doubted(s.b, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var f = b.between.fellers.FirstOrDefault(x => x.pawn?.Map == map && x.stump.IsValid);
            if (f == null)
            {
                var p = OwnBetween.People(map).FirstOrDefault(OwnBetween.Grown);
                var stump = p == null ? IntVec3.Invalid : GenRadial.RadialCellsAround(p.Position, 20f, true).FirstOrDefault(c => PeoplesE.SaplingCell(map, c));
                if (p == null || !stump.IsValid) return "nobody, or no open soil near them.";
                f = new BetweenFeller { pawn = p, felled = OwnHappeningsMath.StumpsFrom, since = OwnBetween.Now, stump = stump };
                b.between.fellers.Add(f);
            }
            return Begin(b, g, map, f) ? "brambles round " + f.pawn.LabelShort + "'s stump; someone who holds her walks out with them." : "nobody free who holds her, or no room for brambles.";
        }
    }
}
