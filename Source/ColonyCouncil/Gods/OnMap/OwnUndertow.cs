using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>The dark's happenings under way (OwnHappeningsBook.undertow): the beasts keeping away from one who bargained, and the dark drifting at night.</summary>
    public class UndertowState : IExposable
    {
        /// <summary>The newest bargain looked at (the tick the world book has it at), and the one the beasts keep away from, until when.</summary>
        public int bargainSeen = -1;
        public Pawn shunned;
        public int shunUntil = -1, shunSaidDay = -1;
        public bool shunCarded;

        /// <summary>The dark drifting: where it stands, toward whom, until when, how it stands now, and who held it off.</summary>
        public Map driftMap;
        public IntVec3 driftAt = IntVec3.Invalid;
        public Pawn driftTo, driftHolder;
        public int driftUntil = -1, driftEnd;
        public bool driftSeen, driftSpoke, driftDimmed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref bargainSeen, "bargainSeen", -1);
            Scribe_References.Look(ref shunned, "shunned");
            Scribe_Values.Look(ref shunUntil, "shunUntil", -1);
            Scribe_Values.Look(ref shunSaidDay, "shunSaidDay", -1);
            Scribe_Values.Look(ref shunCarded, "shunCarded");
            Scribe_References.Look(ref driftMap, "driftMap");
            Scribe_Values.Look(ref driftAt, "driftAt", IntVec3.Invalid);
            Scribe_References.Look(ref driftTo, "driftTo");
            Scribe_References.Look(ref driftHolder, "driftHolder");
            Scribe_Values.Look(ref driftUntil, "driftUntil", -1);
            Scribe_Values.Look(ref driftEnd, "driftEnd");
            Scribe_Values.Look(ref driftSeen, "driftSeen");
            Scribe_Values.Look(ref driftSpoke, "driftSpoke");
            Scribe_Values.Look(ref driftDimmed, "driftDimmed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (shunned == null) shunUntil = -1;
                if (driftMap == null || driftTo == null) EndDrift();
            }
        }

        public void EndDrift()
        {
            driftMap = null;
            driftTo = driftHolder = null;
            driftAt = IntVec3.Invalid;
            driftUntil = -1;
            driftEnd = 0;
            driftSeen = driftSpoke = driftDimmed = false;
        }
    }

    /// <summary>
    /// Undertow, the Hunger (docs/gods-next-proposals.md, C 9): something in the dark under the world that grants the desperate
    /// what they ask and always takes more. It never comes itself and is never named on a card: what happens is what anyone
    /// could see. Its own happenings:
    /// <list type="bullet">
    /// <item>The others turn away, seen: after a bargain (GodVoid), for days, the beasts keep away from the one who made it
    /// (the colony's animals, the wild ones, the dryads, the cats): any that come near get up and walk off, and someone says
    /// so.</item>
    /// <item>The dark that drifts: at night, a darkness that stands comes in from the edge of the colony and drifts slowly
    /// toward whoever is nearest to breaking (or bleeding out), and every lamp it passes burns low. A lit lamp beside them
    /// stops it at the light's edge; someone sitting with them stops it a few steps off; else it reaches them, stands over
    /// them till dawn and speaks to them, kindly. It comes easier on a night with no moon, never past someone keeping the dark
    /// face at the door (Selene's).</item>
    /// <item>The hollow ear: someone of the colony who stands in a room of the old ruins for the first time may come out with a
    /// hollow ear, a quiet too deep in one ear that something on the other side of says their name now and then (minds more
    /// open to the psychic for ten days).</item>
    /// </list>
    /// </summary>
    public static class OwnUndertow
    {
        public const string Key = "Undertow";
        public const string Pool = "Rimfolk_Pool_GodUndertow_Say";
        public const string StoodOverThought = "Rimfolk_Thought_Undertow_StoodOver";
        public const string KeptOffThought = "Rimfolk_Thought_Undertow_KeptOff";
        public const string KeptOffMeThought = "Rimfolk_Thought_Undertow_KeptOffMe";
        public const string EarHediff = "Rimfolk_HollowEar";

        /// <summary>The Hunger, by its key, else the god of its kind.</summary>
        public static God God => OwnHappenings.God(Key) ?? GodVoid.Hunger;

        private static int Now => Find.TickManager.TicksGame;

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            Bargain(b, now);
            int hour = GenLocalDate.HourOfDay(map);
            if (OwnHappeningsMath.DriftHour(hour) && !MapFacts.ThreatOn(map)) Drift(b, g, map, now);
            Ruins(b, g, map, now);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.undertow;
            if (s.shunned != null && s.shunUntil >= 0) OwnHappenings.Safe("the beasts keeping away", () => ShunStep(b, now));
            if (s.driftMap != null) OwnHappenings.Safe("the dark drifting", () => DriftStep(b, now));
        }

        // ================= The others turn away =================

        /// <summary>A bargain made since the last look (the world book's newest): the beasts keep away from the one who made it.</summary>
        private static void Bargain(OwnHappeningsBook b, int now)
        {
            var world = GodWorld.Book;
            var s = b.undertow;
            if (world == null) return;
            int last = world.Last("bargain");
            if (last < 0 || last <= s.bargainSeen) return;
            s.bargainSeen = last;
            // A bargain long past (an older save's first look) is not seen anew.
            if (now - last > OwnHappeningsMath.ShunDays * GenDate.TicksPerDay) return;
            var p = world.Of("take").Where(d => d.pawn != null && !d.pawn.Dead).OrderByDescending(d => d.id).Select(d => d.pawn).FirstOrDefault();
            if (p != null) Shun(b, p);
        }

        public static void Shun(OwnHappeningsBook b, Pawn p)
        {
            var s = b.undertow;
            s.shunned = p;
            s.shunUntil = Now + Mathf.RoundToInt(OwnHappeningsMath.ShunDays * GenDate.TicksPerDay);
            s.shunCarded = false;
            s.shunSaidDay = -1;
            OwnHappenings.Came(b, OwnHappening.UndertowShun, OwnHappenings.Now, null, p.thingIDNumber.ToString());
            b.Due(Now + 60);
        }

        private static readonly Dictionary<int, int> shied = new Dictionary<int, int>();

        private static void ShunStep(OwnHappeningsBook b, int now)
        {
            var s = b.undertow;
            var p = s.shunned;
            if (p == null || p.Dead || now > s.shunUntil)
            {
                s.shunned = null;
                s.shunUntil = -1;
                shied.Clear();
                return;
            }
            if (p.Spawned && p.Map != null)
            {
                var map = p.Map;
                foreach (var a in map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps.Animal && !a.Dead && !a.Downed && !a.InMentalState && a.Awake() && !GodAvatar.Is(a)
                                                                       && !(a.roping?.IsRoped ?? false) && a.Position.InHorDistOf(p.Position, OwnHappeningsMath.ShunNear)).ToList())
                {
                    if (shied.TryGetValue(a.thingIDNumber, out int at) && now - at < OwnHappeningsMath.ShyEvery) continue;
                    if (a.CurJob != null && !a.CurJob.def.casualInterruptible && a.CurJobDef != JobDefOf.Wait && a.CurJobDef != JobDefOf.LayDown) continue;
                    if (!ShyFrom(a, p)) continue;
                    shied[a.thingIDNumber] = now;
                    Shied(b, p, a);
                }
            }
            b.Due(now + 300);
        }

        /// <summary>The beast gets up and walks off, away from them, unhurried and not looking back.</summary>
        private static bool ShyFrom(Pawn a, Pawn p)
        {
            var map = a.Map;
            var away = (a.Position - p.Position).ToVector3();
            if (away.sqrMagnitude < 0.01f) away = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            var to = (a.Position.ToVector3Shifted() + away.normalized * OwnHappeningsMath.ShunFlee).ToIntVec3().ClampInsideMap(map);
            to = CellFinder.RandomClosewalkCellNear(to, map, 3, c => c.Standable(map));
            if (!to.IsValid || !a.CanReach(to, PathEndMode.OnCell, Danger.Some)) return false;
            try
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, to);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                go.expiryInterval = 900;
                a.jobs.StartJob(go, JobCondition.InterruptForced);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a beast keeping away", e);
                return false;
            }
        }

        /// <summary>Said aloud once a day by someone who saw it (never the one it shies from); the first time, a card.</summary>
        private static void Shied(OwnHappeningsBook b, Pawn p, Pawn a)
        {
            var s = b.undertow;
            var map = p.Map;
            int day = GenDate.DaysPassed;
            if (s.shunSaidDay == day) return;
            var seer = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && GodOmens.Awake(x) && x.Position.InHorDistOf(p.Position, 15f) && GenSight.LineOfSight(x.Position, p.Position, map, true))
                .OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (seer == null && s.shunCarded) return;
            s.shunSaidDay = day;
            var g = God;
            if (seer != null) OwnHappenings.SayFromWith(seer, Pool, null, new[] { p.Named("OTHER"), a.KindLabel.Named("BEAST") }, "shun");
            else if (GodOmens.Awake(p)) OwnHappenings.SayFrom(p, Pool, null, "shunself");
            if (!s.shunCarded)
            {
                s.shunCarded = true;
                // apart: what the beasts do around one person, told as seen; nobody is with anyone in it.
                OwnHappenings.CardOf(null, map, null, "CC_GodUndertow_Shun_Title", null, "CC_GodUndertow_Shun_Text".Translate(p.Named("PAWN"), a.KindLabel.Named("BEAST")).Resolve(),
                    new LookTargets(p), null, p.Named("PAWN"));
            }
            if (g != null) OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, seer, "CC_GodUndertow_Saw_Shun", p.LabelShort.Named("SHUNNED"), a.KindLabel.Named("BEAST"));
        }

        // ================= The dark that drifts =================

        private static void Drift(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.undertow;
            if (s.driftMap != null || OwnSelene.KeptTonight(map)) return;
            var target = Desperate(map, out var t);
            float factor = GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Gone ? OwnHappeningsMath.DarkMoonFactor : 1f;
            if (target == null || !OwnHappenings.Calls(b, OwnHappening.UndertowDrift, t, g, map, now, factor)) return;
            BeginDrift(b, map, target);
        }

        /// <summary>The one of the colony nearest to breaking (an extreme break first, then a major one), or bleeding out; and how strongly they call it.</summary>
        public static Pawn Desperate(Map map, out GodPaceMath.Trigger t)
        {
            t = GodPaceMath.Trigger.None;
            Pawn best = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!OwnHappenings.Ours(p)) continue;
                bool bleeding = p.Downed && p.health.hediffSet.BleedRateTotal > 0.01f && HealthUtility.TicksUntilDeathDueToBloodLoss(p) < 6 * GenDate.TicksPerHour;
                var br = p.mindState?.mentalBreaker;
                bool extreme = bleeding || br != null && !p.InMentalState && br.BreakExtremeIsImminent;
                bool major = !extreme && br != null && !p.InMentalState && br.BreakMajorIsImminent;
                var tt = OwnHappeningsMath.DriftTrigger(extreme, major);
                if (tt > t)
                {
                    t = tt;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>The dark comes in from the edge of the colony, a long way from them, and begins to drift.</summary>
        public static bool BeginDrift(OwnHappeningsBook b, Map map, Pawn target)
        {
            var s = b.undertow;
            if (target == null || !target.Spawned) return false;
            IntVec3 start = IntVec3.Invalid;
            for (int i = 0; i < 20 && !start.IsValid; i++)
            {
                float a = Rand.Range(0f, Mathf.PI * 2f);
                var c = new IntVec3(target.Position.x + Mathf.RoundToInt(Mathf.Cos(a) * OwnHappeningsMath.DriftFrom), 0, target.Position.z + Mathf.RoundToInt(Mathf.Sin(a) * OwnHappeningsMath.DriftFrom));
                if (c.InBounds(map) && !c.Fogged(map)) start = c;
            }
            if (!start.IsValid) return false;
            s.driftMap = map;
            s.driftAt = start;
            s.driftTo = target;
            s.driftHolder = null;
            s.driftEnd = (int)OwnHappeningsMath.DriftEnd.Drifting;
            s.driftSeen = s.driftSpoke = false;
            s.driftUntil = Now + GenDate.TicksPerHour * 9;
            OwnHappenings.Came(b, OwnHappening.UndertowDrift, OwnHappenings.Now, null, target.thingIDNumber.ToString());
            b.Due(Now + 1);
            return true;
        }

        private static void DriftStep(OwnHappeningsBook b, int now)
        {
            var s = b.undertow;
            var map = s.driftMap;
            var p = s.driftTo;
            if (map == null || p == null || p.Dead || !p.Spawned || p.Map != map)
            {
                s.EndDrift();
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (now > s.driftUntil || hour >= OwnHappeningsMath.DriftGone && hour < 21)
            {
                Dawn(b, map);
                return;
            }
            var end = (OwnHappeningsMath.DriftEnd)s.driftEnd;
            if (end == OwnHappeningsMath.DriftEnd.Drifting)
            {
                var dir = (p.Position - s.driftAt).ToVector3();
                var next = dir.magnitude <= OwnHappeningsMath.DriftCells ? p.Position : (s.driftAt.ToVector3Shifted() + dir.normalized * OwnHappeningsMath.DriftCells).ToIntVec3();
                next = next.ClampInsideMap(map);
                var near = Lights(map, p.Position, OwnHappeningsMath.DriftLight + 2f);
                bool lightAhead = near.Any(l => l.Position.InHorDistOf(next, OwnHappeningsMath.DriftLight)) && !p.Position.InHorDistOf(next, OwnHappeningsMath.DriftReached);
                var company = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x != p && x.Awake() && !x.Downed && x.Position.InHorDistOf(p.Position, OwnHappeningsMath.DriftCompany)
                                                                                      && next.InHorDistOf(p.Position, OwnHappeningsMath.DriftCompany + 3f));
                end = OwnHappeningsMath.Drift(next.DistanceTo(p.Position), lightAhead, company != null);
                s.driftEnd = (int)end;
                if (end == OwnHappeningsMath.DriftEnd.HeldByCompany) s.driftHolder = company;
                else
                {
                    if (end != OwnHappeningsMath.DriftEnd.HeldByLight) s.driftAt = next;
                    // Every lamp it passes burns low.
                    foreach (var l in Lights(map, s.driftAt, 4f))
                        if (GodOmens.Play(map, l, 900, false, false) > 0)
                            s.driftDimmed = true;
                }
                if (end == OwnHappeningsMath.DriftEnd.Reached) Reached(map, p);
            }
            else if (end == OwnHappeningsMath.DriftEnd.Reached) s.driftAt = p.Position;
            GodSky.Stay(map, SkySign.Dark, s.driftAt, OwnHappeningsMath.DriftTicks + 40, end == OwnHappeningsMath.DriftEnd.Reached ? 3.5f : 4.5f);
            if (!s.driftSeen)
            {
                var seer = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && GodOmens.Awake(x) && x.Position.InHorDistOf(s.driftAt, 25f)
                                                                       && GenSight.LineOfSight(x.Position, s.driftAt, map, true)).FirstOrDefault();
                if (seer != null)
                {
                    s.driftSeen = true;
                    // apart: said of the one it drifts toward, by someone who sees the dark from a way off.
                    OwnHappenings.SayFromWith(seer, Pool, null, new[] { p.Named("OTHER") }, "drift");
                }
            }
            b.Due(now + OwnHappeningsMath.DriftTicks);
        }

        private static List<ThingWithComps> Lights(Map map, IntVec3 at, float range) => GodOmens.Lights(map).Where(l => l.Position.InHorDistOf(at, range)).ToList();

        /// <summary>It stands over them: awake, they hear it, sweet and reasonable.</summary>
        private static void Reached(Map map, Pawn p)
        {
            var s = OwnHappenings.Book?.undertow;
            if (s == null || s.driftSpoke) return;
            s.driftSpoke = true;
            if (p.Awake() && !p.Downed) OwnHappenings.SayFrom(p, Pool, null, "voice");
        }

        /// <summary>At dawn it is gone, and what it did is told as it was seen: it stood over them, or it stopped at a light, or at someone sitting with them.</summary>
        private static void Dawn(OwnHappeningsBook b, Map map)
        {
            var s = b.undertow;
            var p = s.driftTo;
            var holder = s.driftHolder;
            var end = (OwnHappeningsMath.DriftEnd)s.driftEnd;
            bool seen = s.driftSeen;
            string lamps = s.driftDimmed ? "CC_GodUndertow_Lamps".Translate().Resolve() : "";
            var at = s.driftAt;
            s.EndDrift();
            var g = God;
            if (p == null) return;
            switch (end)
            {
                case OwnHappeningsMath.DriftEnd.Reached:
                    OwnHappenings.Memory(p, StoodOverThought);
                    if (g != null) PawnGods.Fear(p, g, 10f, "CC_GodUndertow_Why_StoodOver".Translate(GodLorePage.Date(Now).Named("DATE")).Resolve());
                    OwnHappenings.CardOf(null, map, null, "CC_GodUndertow_Drift_Title", null, "CC_GodUndertow_Drift_Text".Translate(p.Named("PAWN"), lamps.Named("LAMPS")).Resolve(), new LookTargets(p),
                        LetterDefOf.NegativeEvent, p.Named("PAWN"));
                    if (g != null) OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, "CC_GodUndertow_Saw_Drift", p.LabelShort.Named("DESPERATE"));
                    break;
                case OwnHappeningsMath.DriftEnd.HeldByLight:
                    OwnHappenings.CardOf(null, map, null, "CC_GodUndertow_Light_Title", null, "CC_GodUndertow_Light_Text".Translate(p.Named("PAWN"), lamps.Named("LAMPS")).Resolve(), new LookTargets(p), null, p.Named("PAWN"));
                    if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Omen, p.Awake() ? p : null, "CC_GodUndertow_Saw_Light", p.LabelShort.Named("DESPERATE"));
                    break;
                case OwnHappeningsMath.DriftEnd.HeldByCompany:
                    Held(map, p, holder, at);
                    break;
                default:
                    if (seen) OwnHappenings.Line(map, "CC_GodUndertow_Gone_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
                    break;
            }
        }

        /// <summary>Someone sat with them and the dark waited a few steps off till dawn: both remember it, each of the other.</summary>
        private static void Held(Map map, Pawn p, Pawn holder, IntVec3 at)
        {
            // together: the holder was sitting beside them (within a few steps) when the dark stopped.
            var g = God;
            if (holder != null && !holder.Dead)
            {
                OwnHappenings.Memory(holder, KeptOffThought);
                OwnHappenings.MemoryOf(p, KeptOffMeThought, holder);
            }
            string text = "CC_GodUndertow_Company_Text".Translate(p.Named("PAWN"), (holder?.LabelShort ?? "").Named("HOLDER")).Resolve();
            OwnHappenings.CardOf(null, map, null, "CC_GodUndertow_Company_Title", null, text, new LookTargets(p), LetterDefOf.PositiveEvent, p.Named("PAWN"));
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Omen, holder, "CC_GodUndertow_Saw_Company", p.LabelShort.Named("DESPERATE"));
        }

        // ================= The hollow ear =================

        private static readonly List<Pawn> looked = new List<Pawn>();

        private static void Ruins(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.Free(OwnHappening.UndertowEar, OwnHappenings.DaysSince(b, OwnHappening.UndertowEar, now)) || !OwnHappenings.Acts(g, map)) return;
            looked.Clear();
            looked.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && GodOmens.Awake(p)));
            foreach (var p in looked)
            {
                var room = p.GetRoom();
                string key = RuinKey(map, room);
                if (key == null || b.ruins.Contains(key)) continue;
                b.ruins.Add(key);
                while (b.ruins.Count > OwnHappeningsMath.RuinsHeldAtMost) b.ruins.RemoveAt(0);
                if (!Rand.Chance(OwnHappeningsMath.EarChance)) continue;
                Ear(b, g, map, p);
                return;
            }
        }

        /// <summary>A room of the old ruins (indoors, not too large, holding a thing of the ancients) as remembered, else null.</summary>
        public static string RuinKey(Map map, Room room)
        {
            if (room == null || room.PsychologicallyOutdoors || room.IsHuge || room.CellCount > OwnHappeningsMath.RuinRoomMax) return null;
            if (!room.ContainedAndAdjacentThings.Any(t => t.def.defName.StartsWith("Ancient", StringComparison.Ordinal) && t.Faction != Faction.OfPlayer)) return null;
            var r = room.ExtentsClose;
            return map.uniqueID + "|" + r.minX + "|" + r.minZ;
        }

        /// <summary>They stop, a hand to one ear; they come out of it with a hollow ear, and say what they hear.</summary>
        public static bool Ear(OwnHappeningsBook b, God g, Map map, Pawn p)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(EarHediff);
            if (def == null || p?.health == null) return false;
            if (!p.health.hediffSet.HasHediff(def)) p.health.AddHediff(def);
            OwnHappenings.Stand(p, IntVec3.Invalid, 420);
            OwnHappenings.ModMote(map, p.Position, new Color(0.25f, 0.2f, 0.35f), 1.2f, "Mote_ShadowCloud", "Mote_Shadow");
            OwnHappenings.SayFrom(p, Pool, null, "ear");
            OwnHappenings.CardOf(null, map, null, "CC_GodUndertow_Ear_Title", null, "CC_GodUndertow_Ear_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), null, p.Named("PAWN"));
            if (g != null) OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, "CC_GodUndertow_Saw_Ear", p.LabelShort.Named("HEARER"));
            OwnHappenings.Came(b, OwnHappening.UndertowEar, OwnHappenings.Now, null, p.thingIDNumber.ToString());
            return true;
        }
    }

}
