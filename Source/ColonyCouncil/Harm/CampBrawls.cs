using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp's quarrel that becomes a brawl (the harm review's 2.4.1). While a camp's tension is at 70 or more, or for the
    /// three evenings after the old quarrel with another people was answered badly (DilemmaEffect_CampQuarrel), each
    /// evening at their fire (six to ten) has one chance in four (half with the colony's peacemaker at home, a frayed
    /// temper among the colony's people at the fire half again at most) that it comes to blows, once a stay. With a
    /// colonist at the fire (and the deeper hooks, so neither people counts a blow as an attack) it is one of the colony
    /// and one of theirs, the hottest temper first; otherwise two of their own. Raised words, then the game's own social
    /// fight, and up to two more pairs from those at the fire join it, each through the harm service's cap. When it's over
    /// their people remember it as a fistfight (goodwill lost once, never so much as to make enemies; the outsiders' brawl
    /// keeps each blow from counting as an attack), and the camp packs up early, its ending letter telling why. Never a
    /// child, and never while danger is on the map.
    /// </summary>
    public static class CampBrawls
    {
        /// <summary>With hurts from everyday life on.</summary>
        public static bool On => Harm.On;

        /// <summary>The colony's own may come to blows with theirs: the deeper hooks keep it a brawl, not an attack on guests.</summary>
        public static bool ColonySide => On && RimfolkPatches.On;

        private static readonly Color SayColor = new Color(1f, 0.75f, 0.6f);

        /// <summary>Someone at the fire who could come to blows: a grown-up up and about, not peaceful, the cap letting them fight.</summary>
        public static bool Fit(Pawn p, IntVec3 fire, float range) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Juvenile()
            && p.interactions != null && !p.WorkTagIsDisabled(WorkTags.Violent) && p.Position.InHorDistOf(fire, range) && Harm.May(p, HurtClass.Fight)
            && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false) && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false);

        /// <summary>A line over a head at the fire, to the other of the pair (both there: Speech.Say checks they're together).</summary>
        public static void Say(Pawn p, string pool, Pawn to, string tag = null)
        {
            try
            {
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                // together: said at the fire to the other of the pair, who stands within talking range (Together.Can).
                Speech.Say(p, line.Formatted(p.Named("PAWN"), to.Named("OTHER")).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a camp brawl's line", e);
            }
        }
    }

    /// <summary>The old quarrel with another people answered badly: the camp's next three evenings can come to blows (Harm/CampBrawls.cs).</summary>
    public class DilemmaEffect_CampQuarrel : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!CampBrawls.On || CampOnMap.For(ctx) == null) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_CampQuarrel".Translate().Resolve(), "CC_Impact_CampQuarrelBrief".Translate().Resolve(), "campquarrel");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !CampBrawls.On) return;
            c.quarrelSouredAt = Find.TickManager.TicksGame;
        }
    }

    public partial class Camp
    {
        /// <summary>The old quarrel with another people answered badly (the tick), for its next three evenings; -1 for none.</summary>
        public int quarrelSouredAt = -1;
        /// <summary>A brawl came this stay (once a stay at most).</summary>
        public bool brawled;
        /// <summary>When the brawl began, while it is watched; -1 for none.</summary>
        public int brawlAt = -1;
        /// <summary>The game day last rolled for one (an evening's one chance).</summary>
        public int brawlDay = -1;
        /// <summary>The brawl's first pair, by name, for its ending's words.</summary>
        public string brawlNames;

        private sealed class BrawlPair
        {
            public Pawn a, b;
            public OutsiderBout bout;
        }

        /// <summary>The pairs fighting, while it goes on (not saved: after a load the camp packs up as it would after it).</summary>
        [Unsaved] private List<BrawlPair> brawlPairs;

        private void ExposeBrawl()
        {
            Scribe_Values.Look(ref quarrelSouredAt, "quarrelSouredAt", -1);
            Scribe_Values.Look(ref brawled, "brawled");
            Scribe_Values.Look(ref brawlAt, "brawlAt", -1);
            Scribe_Values.Look(ref brawlDay, "brawlDay", -1);
            Scribe_Values.Look(ref brawlNames, "brawlNames");
        }

        /// <summary>The hour's look at a quarrel coming to blows, and at a brawl under way (Camp.Hourly, after the tension's).</summary>
        private void BrawlHour()
        {
            try
            {
                if (brawlAt >= 0)
                {
                    WatchBrawl();
                    return;
                }
                if (brawled || !CampBrawls.On || phase != CampPhase.Living || Hostile || map == null || MapFacts.ThreatOn(map)) return;
                if (!HarmTwoMath.Quarrelling(tension, quarrelSouredAt, Now) || !HarmTwoMath.Evening(GenLocalDate.HourOfDay(map))) return;
                int day = GenDate.DaysPassed;
                if (brawlDay == day) return;
                var fire = FireCell;
                var theirs = Here.Where(p => CampBrawls.Fit(p, fire, 14f)).ToList();
                if (theirs.Count == 0) return;
                var ours = CampBrawls.ColonySide
                    ? IncidentWorker_Happening.Colonists(map).Where(p => PlayOuts.OurPerson(p) && CampBrawls.Fit(p, fire, 16f)).ToList()
                    : new List<Pawn>();
                if (ours.Count == 0 && theirs.Count < 2) return;
                brawlDay = day;
                var pm = ColonyState.For(map)?.offices?.Peacemaker;
                bool peacemaker = pm != null && pm.Spawned && pm.Map == map;
                float frayed = ours.Count == 0 ? 1f : ours.Max(FrayedTempers.Factor);
                if (!Harm.Roll(HarmTwoMath.CampBrawlChance(peacemaker, frayed))) return;
                BeginBrawl(ours, theirs);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a camp's quarrel", e);
            }
        }

        /// <summary>Dev mode (Harm/HarmTwoDebug.cs): the quarrel comes to blows now, whatever the hour, the tension and the roll.</summary>
        internal string DebugBrawl()
        {
            if (brawled || brawlAt >= 0) return "this camp has had its brawl this stay.";
            if (phase != CampPhase.Living || map == null || Hostile) return "the camp isn't settled at peace on the map.";
            var fire = FireCell;
            var theirs = Here.Where(p => CampBrawls.Fit(p, fire, 30f)).ToList();
            var ours = CampBrawls.ColonySide
                ? IncidentWorker_Happening.Colonists(map).Where(p => PlayOuts.OurPerson(p) && CampBrawls.Fit(p, fire, 30f)).ToList()
                : new List<Pawn>();
            if (theirs.Count == 0 || ours.Count == 0 && theirs.Count < 2) return "nobody fit to come to blows near their fire (send a colonist over, or wait for the evening).";
            brawlDay = GenDate.DaysPassed;
            BeginBrawl(ours, theirs);
            return brawlAt >= 0 ? "it came to blows." : "it didn't begin: nobody within talking range of someone of the other side.";
        }

        private void BeginBrawl(List<Pawn> ours, List<Pawn> theirs)
        {
            brawlPairs = new List<BrawlPair>();
            Pawn a = null, x = null;
            if (ours.Count > 0)
            {
                // The hottest temper of the colony's at the fire, and the one of theirs they think least of within talking range.
                foreach (var c in ours.OrderByDescending(Outsiders.Temper).ThenBy(c => c.Position.DistanceToSquared(FireCell)))
                {
                    var m = theirs.Where(t => Together.Can(c, t)).OrderBy(t => c.relations?.OpinionOf(t) ?? 0).FirstOrDefault();
                    if (m == null) continue;
                    a = c;
                    x = m;
                    break;
                }
            }
            if (a == null)
            {
                // Two of their own.
                foreach (var t in theirs.OrderByDescending(Outsiders.Temper))
                {
                    var u = theirs.Where(o => o != t && Together.Can(t, o)).RandomElementWithFallback();
                    if (u == null) continue;
                    a = u;
                    x = t;
                    break;
                }
            }
            if (a == null || x == null || !Pair(a, x, true)) return;
            brawled = true;
            brawlAt = Now;
            bool colony = KinMemory.Ours(a);
            brawlNames = colony ? a.LabelShort : null;
            // A few on each side join: those still free at the fire, each with someone of the other side beside them.
            var freeOurs = ours.Where(p => p != a && !Fighting(p)).ToList();
            var freeTheirs = theirs.Where(p => p != x && p != a && !Fighting(p)).ToList();
            int more = colony ? HarmTwoMath.JoinPairs(freeOurs.Count, freeTheirs.Count) : HarmTwoMath.JoinPairs(freeTheirs.Count / 2, freeTheirs.Count / 2);
            for (int i = 0; i < more; i++)
            {
                var side = colony ? freeOurs : freeTheirs;
                var other = freeTheirs;
                var p = side.FirstOrDefault(s => other.Any(o => o != s && Together.Can(s, o)));
                if (p == null) break;
                var q = other.Where(o => o != p && Together.Can(p, o)).OrderBy(o => o.Position.DistanceToSquared(p.Position)).First();
                side.Remove(p);
                other.Remove(q);
                Pair(p, q, false);
            }
            AddTension(15f);
            var names = brawlPairs.SelectMany(b => new[] { b.a, b.b }).Where(p => p != null).Distinct().ToList();
            string key = colony ? "CC_HarmTwo_CampBrawl" : "CC_HarmTwo_CampBrawlTheirs";
            string text = Format(TextVariants.Vary(key).Translate().RawText, x, colony ? a : null, brawlPairs.Count, colony ? null : a.LabelShort);
            if (brawlPairs.Count > 1)
                text += " " + "CC_HarmTwo_CampBrawlJoined".Translate(TextUtil.JoinAnd(names.Where(p => p != a && p != x).Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve();
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Important, map, text, new LookTargets(names), MessageTypeDefOf.NegativeEvent);
        }

        private bool Fighting(Pawn p) => brawlPairs != null && brawlPairs.Any(b => b.a == p || b.b == p);

        /// <summary>One pair comes to blows: the words (the first's), then the game's own social fight under the cap for both.</summary>
        private bool Pair(Pawn a, Pawn x, bool first)
        {
            // Between the colony and them, a brawl their people won't count as an attack (the outsiders' own rule).
            OutsiderBout bout = null;
            if (KinMemory.Ours(a) || KinMemory.Ours(x))
            {
                var ours = KinMemory.Ours(a) ? a : x;
                bout = Outsiders.QuietBrawl(ours, ours == a ? x : a);
                if (bout == null) return false;
            }
            // together: the two at the camp's fire, within talking range of each other (Together.Can, checked by whoever chose them).
            CampBrawls.Say(x, first ? "Rimfolk_Pool_CampQuarrel" : "Rimfolk_Pool_CampBrawlJoin", a, KinMemory.Ours(a) ? "colony" : "theirs");
            if (!Harm.Fight(a, x, "a camp's quarrel"))
            {
                if (bout != null) Outsiders.ForgetBrawl(bout);
                return false;
            }
            brawlPairs.Add(new BrawlPair { a = a, b = x, bout = bout });
            return true;
        }

        /// <summary>Over once nobody in it is still fighting (or after three hours): their people hear of it, and the camp packs up.</summary>
        private void WatchBrawl()
        {
            bool still = brawlPairs != null && Now - brawlAt < HarmTwoMath.BrawlMaxTicks
                         && brawlPairs.Any(b => b.a?.MentalStateDef == MentalStateDefOf.SocialFighting || b.b?.MentalStateDef == MentalStateDefOf.SocialFighting);
            if (still) return;
            bool colony = brawlNames != null || (brawlPairs?.Any(b => b.bout != null) ?? false);
            var first = brawlPairs?.FirstOrDefault(b => b.bout != null);
            brawlAt = -1;
            brawlPairs = null;
            if (colony && faction != null)
            {
                // Remembered as a fistfight with the colony: goodwill lost once, stopping short of hostility (LoseGoodwill).
                var c = first != null ? (KinMemory.Ours(first.a) ? first.a : first.b) : null;
                var x = first != null ? first.bout.b : null;
                KinMemory.Note(faction, "brawled", c, x?.LabelShort);
                LoseGoodwill(-HarmTwoMath.CampBrawlGoodwill);
            }
            // A brawl among themselves sends them on too, with nothing held against the colony.
            if (Settled && !Hostile) Leave(colony ? "brawl" : "brawlTheirs");
        }
    }
}
