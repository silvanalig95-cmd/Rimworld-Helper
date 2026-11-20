using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The peoples' own events, round 10, the last (docs/peoples-events-proposals.md, items 96 to 100; the defs in
    // Defs/IncidentDefs/Rimfolk_Events_PeopleEvents10.xml): the events between peoples, each with two of Rimfolk's peoples at once, and
    // every people's art handed on. The Deepwell Union and the Mossborn put the old wood before the colony's table as neutral ground; the
    // road league's toll and the Wheel's carts that won't pay meet at the colony's land; the hospitaller order and the Quiet Bell ask for
    // the colony's doctor and two diggers for a plague town; a copyist of the learned society and a technician of the Sunken Vault meet at
    // the colony's table for three days; and once a game a people at good terms hands its own art to the colonist it remembers best.
    // Each comes only from peoples at peace with the colony (and not at war with each other), takes the form of their age, and plays out
    // on the map, built on rounds 1 to 9's shapes: the visitors at the gate, round 9's work at a place (JobDriver_PEv9Work), the moot and
    // the toll of the world events, round 2's season away and the illness budget's sickness brought home, round G's vault supply and the
    // ages' teaching rule, the peoples' memory and the colonist it names (KinMemory).

    /// <summary>Round 10's visits, by kind, its watch, its jobs' ends, and what its events share.</summary>
    public static class PeopleEvents10
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef HearJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_Hear");
        public static JobDef PleadJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_Plead");
        public static JobDef CopyJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_Copy");
        public static JobDef ReadOutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_ReadOut");
        public static JobDef TeachJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_Teach");
        public static JobDef LearnJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_Learn");
        public static JobDef ShowArtJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_ShowArt");
        public static JobDef LearnArtJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv10_LearnArt");

        /// <summary>An hour of one of round 10's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case WoodMoot.Kind: return WoodMoot.Hour(v, now);
                case RoadToll.Kind: return RoadToll.Hour(v, now);
                case LearningTraded.Kind: return LearningTraded.Hour(v, now);
                case ArtHandedOn.Kind: return ArtHandedOn.Hour(v, now);
            }
            return false;
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 to 9's): the season's prices that run out, the colony's people at a
        /// plague town and their days there.
        /// </summary>
        public static void Hourly(int now)
        {
            Safe("the season's prices", () => WoodMoot.Prices(now));
            Safe("the plague town", () => PlagueYear.Days(now));
        }

        /// <summary>One of round 10's jobs done at its target (round 9's work at a place hands it on): what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == HearJob)
                {
                    if (!WoodMoot.Heard(pawn)) RoadToll.Heard(pawn);
                }
                else if (job == CopyJob) LearningTraded.Copied(pawn);
                else if (job == TeachJob) LearningTraded.Taught(pawn);
                else if (job == LearnArtJob) ArtHandedOn.Learned(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 10: work done", e);
            }
        }

        /// <summary>One of round 10's jobs (round 9's driver: up to the target, a while at it with a bar and its mark).</summary>
        public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks) => PeopleEvents9.Send(p, job, target, ticks);

        /// <summary>One of theirs sent to work at a target, kept there by a lord of their own (round G's errand).</summary>
        public static bool Errand(Pawn x, Map map, LocalTargetInfo target, JobDef job, int ticks, float hours) => PeopleEvents9.Errand(x, map, target, job, ticks, hours);

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 10: " + what, e);
            }
        }

        /// <summary>A people at peace with the colony who may be paid or thanked (not the colony, not gone).</summary>
        public static bool Thanks(Faction f) => PeopleEvents9.Thanks(f);

        /// <summary>Goodwill with a people at peace, for a reason (a loss stops short of war, as the world events' does).</summary>
        public static void Goodwill(Faction f, int amount, HistoryEventDef why)
        {
            if (Thanks(f) && amount != 0) WorldSituationWorker.Goodwill(f, amount, why);
        }

        /// <summary>A colonist's mood memory of round 10's own, if the def is there.</summary>
        public static void Thought(Pawn p, string def) => PeopleEvents9.Thought(p, def);

        /// <summary>A people of this calling at peace with the colony and not at war with <paramref name="other"/>.</summary>
        public static Faction Friendly(string calling, Faction other = null) =>
            PeoplesD.Friendly(calling) is Faction f && (other == null || f != other && !f.HostileTo(other)) ? f : null;

        /// <summary>Two peoples of these callings at peace with the colony and with each other: the first, and the second in <paramref name="second"/>.</summary>
        public static Faction Pair(string first, string secondCalling, out Faction second)
        {
            second = null;
            var a = PeoplesD.Friendly(first);
            if (a == null) return null;
            second = Arrivals.Friendly(f => PeoplesD.Theirs(f, secondCalling) && f != a && !f.HostileTo(a));
            return second != null ? a : null;
        }

        /// <summary>The visit of this kind under way with this colonist as its "a".</summary>
        public static Visit OfColonist(string kind, Pawn a) =>
            a == null ? null : CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == kind && !v.done && v.a == a);

        /// <summary>The visit of this kind under way with this pawn as its visitor.</summary>
        public static Visit Of(string kind, Pawn x) => PeopleEvents9.Of(kind, x);

        /// <summary>A visit's people by its text's own field (a Faction's unique id), else null.</summary>
        public static Faction PeopleById(string id) =>
            id.NullOrEmpty() ? null : Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != null && f.GetUniqueLoadID() == id);

        /// <summary>The region hears how it went: a line of news.</summary>
        public static void Heard(string text, LookTargets look) => News.Post(text, look ?? LookTargets.Invalid, NewsKind.World);

        /// <summary>The visitors of an event at the colony, up and about: X and the rest of their party.</summary>
        public static List<Pawn> Up(Visit v) => v == null ? new List<Pawn>() : v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();
    }

    // ===================== The season's prices =====================

    /// <summary>
    /// A ruling at the moot over the old wood (item 96) moves a price for a season: steel with the shaft sunk, the Mossborn's herbal medicine
    /// with the wood kept. Every trader the colony deals with (caravans, visitors, towns, ships) asks and pays that much less for it while
    /// it holds; buying and selling move together, so the spread between them stays the game's. The region's hard and good years
    /// (World/Region/RegionPrices.cs) ride on the same hook, by the trader's people: one factor on the price, the moot's and the
    /// region's together and kept within half and one and a half (RegionPricesMath.Combined), so the two never pull against each other.
    /// </summary>
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_SeasonPrices
    {
        public static void Postfix(Tradeable __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            bool moot = PeopleEvents10Book.AnyPrice;
            if (!moot && !RegionPrices.Any) return;
            try
            {
                var def = __instance.ThingDef;
                float f = RegionPricesMath.Combined(moot ? PeopleEvents10Book.Factor(def) : 1f, RegionPrices.Factor(def, RegionPrices.Trader));
                if (f != 1f) __result = PeopleEvents10Math.Priced(__result, f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 10: a season's price", e);
            }
        }
    }
}
