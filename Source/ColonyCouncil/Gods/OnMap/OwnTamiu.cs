using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Tamiu's two new happenings under way (OwnHappeningsBook.tamiu): a cat at the threshold of death, a cat by a body left lying far out.</summary>
    public class TamiuState : IExposable
    {
        /// <summary>The cat at the threshold: the cat, the one near death, since when out of danger, until when; seen, a stray, whether they died.</summary>
        public Pawn cat, dying;
        public int since = -1, livedSince = -1, until = -1, diedAt = -1;
        public bool seen, stray, died;

        /// <summary>The cat by the lost dead: the cat, the body, who comes to it, until when; whether a stray, whether found.</summary>
        public Pawn lostCat, finder;
        public Corpse body;
        public int lostUntil = -1, lastCry = -1;
        public bool lostStray, lostFound;

        public void ExposeData()
        {
            Scribe_References.Look(ref cat, "cat");
            Scribe_References.Look(ref dying, "dying");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref livedSince, "livedSince", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref diedAt, "diedAt", -1);
            Scribe_Values.Look(ref seen, "seen");
            Scribe_Values.Look(ref stray, "stray");
            Scribe_Values.Look(ref died, "died");
            Scribe_References.Look(ref lostCat, "lostCat");
            Scribe_References.Look(ref finder, "finder");
            Scribe_References.Look(ref body, "body");
            Scribe_Values.Look(ref lostUntil, "lostUntil", -1);
            Scribe_Values.Look(ref lastCry, "lastCry", -1);
            Scribe_Values.Look(ref lostStray, "lostStray");
            Scribe_Values.Look(ref lostFound, "lostFound");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cat == null || dying == null) EndThreshold();
                if (lostCat == null || body == null) EndLost();
            }
        }

        public void EndThreshold()
        {
            cat = dying = null;
            since = livedSince = until = diedAt = -1;
            seen = stray = died = false;
        }

        public void EndLost()
        {
            lostCat = finder = null;
            body = null;
            lostUntil = lastCry = -1;
            lostStray = lostFound = false;
        }
    }

    /// <summary>
    /// Tamiu, the Keeper of the Threshold (defName and key Mog; MogsWays has her stay, riddle, keepers and wrapped dead): two
    /// more of her own, both of the threshold she keeps, both through the cats that are all hers:
    /// <list type="bullet">
    /// <item>The cat at the threshold: when one of the colony lies near death (bleeding out, or a sickness about to win), a
    /// cat (one of the colony's, else a stray in from the edge) comes and lies beside them, facing out, and will not be sent
    /// away. If they die, it stays by the body a while; if they come through, it gets up once they are out of danger, stretches
    /// and walks off, and a card says so.</item>
    /// <item>The cat by the lost dead: a body of the colony's left lying unburied far out on the land, half a day or more; a cat
    /// walks out to it and sits by it crying, and the first one free who hears it goes out and finds the body (it is no longer
    /// forbidden, so it can be brought home).</item>
    /// </list>
    /// </summary>
    public static class OwnTamiu
    {
        public const string Key = "Mog";
        public const string Pool = "Rimfolk_Pool_GodTamiu_Say";
        public const string LetGoThought = "Rimfolk_Thought_Tamiu_CatWentAway";

        public static God God => OwnHappenings.God(Key) ?? MogsWays.Her;

        private static int Now => Find.TickManager.TicksGame;

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.tamiu;
            if (s.cat == null)
            {
                var p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => OwnHappenings.Ours(x) && NearDeath(x));
                if (p != null && OwnHappenings.Calls(b, OwnHappening.MogThreshold, OwnHappeningsMath.ThresholdTrigger(true), g, map, now)) Threshold(b, map, p);
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (s.lostCat == null && hour >= 7 && hour < 18 && !MapFacts.ThreatOn(map))
            {
                var body = LostBody(map, out var t);
                if (body != null && OwnHappenings.Calls(b, OwnHappening.MogLostDead, t, g, map, now)) Lost(b, map, body);
            }
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.tamiu;
            if (s.cat != null) OwnHappenings.Safe("the cat at the threshold", () => ThresholdStep(b, now));
            if (s.lostCat != null) OwnHappenings.Safe("the cat by the lost dead", () => LostStep(b, now));
        }

        /// <summary>Near death: down and bleeding out within hours, or a sickness all but won.</summary>
        public static bool NearDeath(Pawn p)
        {
            if (p?.health?.hediffSet == null || p.Dead) return false;
            if (p.Downed && p.health.hediffSet.BleedRateTotal > 0.01f && HealthUtility.TicksUntilDeathDueToBloodLoss(p) < 6 * GenDate.TicksPerHour) return true;
            return p.health.hediffSet.hediffs.Any(h => h.def.lethalSeverity > 0f && h.Severity / h.def.lethalSeverity > 0.82f && h is HediffWithComps hw
                                                       && hw.TryGetComp<HediffComp_Immunizable>() is HediffComp_Immunizable imm && imm.Immunity < h.Severity);
        }

        // ================= The cat at the threshold =================

        public static bool Threshold(OwnHappeningsBook b, Map map, Pawn p)
        {
            var s = b.tamiu;
            var at = p.PositionHeld;
            var cat = OwnHappenings.ColonyCat(map, at) ?? OwnHappenings.StrayCat(map, at);
            if (cat == null) return false;
            s.cat = cat;
            s.dying = p;
            s.stray = cat.Faction == null;
            s.since = Now;
            s.livedSince = -1;
            s.until = Now + GenDate.TicksPerDay;
            s.seen = s.died = false;
            OwnHappenings.Lead(cat, OwnHappenings.Beside(map, at, cat), GenDate.TicksPerHour);
            OwnHappenings.Came(b, OwnHappening.MogThreshold, OwnHappenings.Now, null, p.thingIDNumber.ToString());
            b.Due(Now + 250);
            return true;
        }

        private static void ThresholdStep(OwnHappeningsBook b, int now)
        {
            var s = b.tamiu;
            var c = s.cat;
            var p = s.dying;
            if (c == null || c.Dead || !c.Spawned || p == null || now > s.until)
            {
                Leave(s);
                return;
            }
            var map = c.Map;
            var g = God;
            if (p.Dead)
            {
                if (!s.died)
                {
                    s.died = true;
                    s.diedAt = now;
                    var seer = OwnHappenings.Seer(map, c.Position, 18f);
                    // apart: said of the dead, by someone who sees the cat stay by them.
                    if (seer != null) OwnHappenings.SayFromWith(seer, Pool, g, new[] { p.LabelShort.Named("OTHER") }, "died");
                    if (g != null) OwnHappenings.Saw(g, map, c.Position, GodActKind.Omen, seer, "CC_GodTamiu_Saw_Died", p.LabelShort.Named("DYING"));
                }
                // It stays by the body a while, then goes about its own way.
                if (now - s.diedAt > GenDate.TicksPerHour * 2 || p.Corpse == null || !p.Corpse.Spawned)
                {
                    Leave(s);
                    return;
                }
                OwnHappenings.Lead(c, OwnHappenings.Beside(map, p.Corpse.Position, c), GenDate.TicksPerHour);
                b.Due(now + 400);
                return;
            }
            if (!p.Spawned || p.Map != map)
            {
                Leave(s);
                return;
            }
            if (NearDeath(p)) s.livedSince = -1;
            else if (s.livedSince < 0) s.livedSince = now;
            if (s.livedSince >= 0 && now - s.livedSince >= OwnHappeningsMath.ThresholdLiveHours * GenDate.TicksPerHour)
            {
                WentAway(b, map, c, p);
                return;
            }
            var beside = OwnHappenings.Beside(map, p.PositionHeld, c);
            if (OwnHappenings.Yields(c) || c.CurJobDef == JobDefOf.Goto)
            {
                if (beside.IsValid && !c.Position.InHorDistOf(p.PositionHeld, 1.6f)) OwnHappenings.Lead(c, beside, GenDate.TicksPerHour);
                else if (c.CurJobDef != JobDefOf.Wait) OwnHappenings.Lead(c, c.Position, GenDate.TicksPerHour);
            }
            if (!s.seen)
            {
                var seer = OwnHappenings.Seer(map, c.Position, 15f);
                if (seer != null && c.Position.InHorDistOf(p.PositionHeld, 2f))
                {
                    s.seen = true;
                    // apart: said of the one near death, by someone who sees the cat come to them.
                    OwnHappenings.SayFromWith(seer, Pool, g, new[] { p.LabelShort.Named("OTHER") }, "threshold");
                    if (g != null) OwnHappenings.Saw(g, map, c.Position, GodActKind.Omen, seer, "CC_GodTamiu_Saw_Threshold", p.LabelShort.Named("DYING"));
                }
            }
            b.Due(now + 300);
        }

        /// <summary>They came through: the cat gets up, stretches and walks out; the card says so, and they are the gladder for it.</summary>
        private static void WentAway(OwnHappeningsBook b, Map map, Pawn c, Pawn p)
        {
            var s = b.tamiu;
            var g = God;
            FleckMaker.ThrowDustPuffThick(c.DrawPos, map, 0.6f, new Color(0.75f, 0.7f, 0.55f, 0.5f));
            var away = CellFinder.RandomClosewalkCellNear(c.Position + (c.Position - p.PositionHeld) * 6, map, 6, x => x.Standable(map));
            if (away.IsValid) OwnHappenings.Lead(c, away.ClampInsideMap(map), GenDate.TicksPerHour);
            OwnHappenings.Memory(p, LetGoThought);
            string text = "CC_GodTamiu_WentAway_Text".Translate(p.Named("PAWN"), c.KindLabel.Named("CAT")).Resolve();
            OwnHappenings.CardOf(g, map, null, "CC_GodTamiu_WentAway_Title", "CC_GodTamiu_WentAway_TitleNamed", text, new LookTargets(p), LetterDefOf.PositiveEvent, p.Named("PAWN"));
            var seer = OwnHappenings.Seer(map, c.Position, 18f);
            if (g != null) OwnHappenings.Saw(g, map, c.Position, GodActKind.Blessing, seer, "CC_GodTamiu_Saw_WentAway", p.LabelShort.Named("DYING"));
            Leave(s);
        }

        private static void Leave(TamiuState s)
        {
            if (s.stray && s.cat != null) OwnHappenings.SendOff(s.cat, GenDate.TicksPerHour);
            s.EndThreshold();
        }

        // ================= The cat by the lost dead =================

        /// <summary>A body of the colony's lying unburied far out on the land (not in a grave or a store), the longest lying first; and how strongly it calls.</summary>
        public static Corpse LostBody(Map map, out GodPaceMath.Trigger t)
        {
            t = GodPaceMath.Trigger.None;
            var mid = OwnHappenings.Middle(map);
            Corpse best = null;
            foreach (var x in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(x is Corpse c) || !c.Spawned || c.InnerPawn == null || c.InnerPawn.Faction != Faction.OfPlayer || c.InnerPawn.RaceProps?.Humanlike != true) continue;
                float hours = (Now - c.timeOfDeath) / (float)GenDate.TicksPerHour;
                var tt = OwnHappeningsMath.LostTrigger(hours, c.Position.DistanceTo(mid), c.IsInAnyStorage());
                if (tt == GodPaceMath.Trigger.None) continue;
                if (best == null || c.timeOfDeath < best.timeOfDeath)
                {
                    best = c;
                    t = tt;
                }
            }
            return best;
        }

        public static bool Lost(OwnHappeningsBook b, Map map, Corpse body)
        {
            var s = b.tamiu;
            var cat = OwnHappenings.ColonyCat(map, body.Position) ?? OwnHappenings.StrayCat(map, body.Position);
            if (cat == null) return false;
            s.lostCat = cat;
            s.body = body;
            s.lostStray = cat.Faction == null;
            s.lostFound = false;
            s.finder = null;
            s.lastCry = -1;
            s.lostUntil = Now + GenDate.TicksPerDay;
            OwnHappenings.Lead(cat, OwnHappenings.Beside(map, body.Position, cat), GenDate.TicksPerHour * 2);
            OwnHappenings.Came(b, OwnHappening.MogLostDead, OwnHappenings.Now, null, body.InnerPawn.thingIDNumber.ToString());
            b.Due(Now + 300);
            return true;
        }

        private static void LostStep(OwnHappeningsBook b, int now)
        {
            var s = b.tamiu;
            var c = s.lostCat;
            var body = s.body;
            if (c == null || c.Dead || !c.Spawned || body == null || body.Destroyed || !body.Spawned || now > s.lostUntil)
            {
                if (s.lostStray && c != null) OwnHappenings.SendOff(c, GenDate.TicksPerHour);
                s.EndLost();
                return;
            }
            var map = c.Map;
            bool there = c.Position.InHorDistOf(body.Position, 2f);
            if (OwnHappenings.Yields(c) || c.CurJobDef == JobDefOf.Goto) OwnHappenings.Lead(c, there ? c.Position : OwnHappenings.Beside(map, body.Position, c), GenDate.TicksPerHour);
            if (there && !s.lostFound)
            {
                // It cries by the body until someone comes.
                if (now - s.lastCry > 900)
                {
                    s.lastCry = now;
                    OwnHappenings.Sound(map, c.Position, 1f, "Pawn_Cat_Call");
                }
                var f = s.finder;
                if (f == null || f.Dead || !f.Spawned || f.Map != map)
                {
                    f = s.finder = map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Free(x) && x.CanReach(body.Position, PathEndMode.Touch, Danger.Some))
                        .OrderBy(x => x.Position.DistanceToSquared(body.Position)).FirstOrDefault();
                    if (f != null) OwnHappenings.GoTo(f, OwnHappenings.Beside(map, body.Position, f), GenDate.TicksPerHour * 3);
                }
                else if (f.Position.InHorDistOf(body.Position, 3f)) Found(b, map, f, body, c);
                else if (f.CurJobDef != JobDefOf.Goto && OwnHappenings.Free(f)) OwnHappenings.GoTo(f, OwnHappenings.Beside(map, body.Position, f), GenDate.TicksPerHour * 3);
            }
            b.Due(now + 300);
        }

        /// <summary>They find the body by the crying cat; it is no longer forbidden, so it can be brought home; the card says who it was and where.</summary>
        private static void Found(OwnHappeningsBook b, Map map, Pawn f, Corpse body, Pawn c)
        {
            var s = b.tamiu;
            var g = God;
            s.lostFound = true;
            string dead = body.InnerPawn?.LabelShort ?? "";
            try
            {
                body.SetForbidden(false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the body the cat found", e);
            }
            OwnHappenings.Stand(f, body.Position, 360);
            OwnHappenings.SayFromWith(f, Pool, g, new[] { dead.Named("DEAD") }, "lost");
            string text = "CC_GodTamiu_Lost_Text".Translate(dead.Named("DEAD"), f.Named("PAWN"), c.KindLabel.Named("CAT")).Resolve();
            OwnHappenings.CardOf(g, map, null, "CC_GodTamiu_Lost_Title", "CC_GodTamiu_Lost_TitleNamed", text, new LookTargets(body), LetterDefOf.NeutralEvent, dead.Named("DEAD"));
            if (g != null) OwnHappenings.Saw(g, map, body.Position, GodActKind.Omen, f, "CC_GodTamiu_Saw_Lost", dead.Named("DEAD"));
            // The cat stays by the body until it is carried off or the day is out.
            s.lostUntil = Math.Min(s.lostUntil, Now + GenDate.TicksPerHour * 8);
        }
    }

}
