using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Hustings that come to blows (the harm review's 1.4.1). At an election's hustings, a heckle thrown at a candidate's
    /// speech by someone who backs another, with a rival of theirs among the speaker's supporters in the crowd, can become
    /// the game's own social fight: once a hustings at most, one in eight (half with the peacemaker there, half under the
    /// best-behavior decree, half with calm candidates; a frayed temper half again at most), times the harm scale and
    /// under its cap for both. The supporter answers the heckle, they come to blows, the speeches wait while it lasts, and
    /// then whoever chairs the hustings calls for order. Told at once; nobody of the colony a child.
    /// </summary>
    public partial class OfficeTracker
    {
        private static readonly Color BrawlColor = new Color(1f, 0.75f, 0.6f);

        /// <summary>Dev mode (Harm/HarmTwoDebug.cs): the next heckle at a hustings comes to blows, when the cap lets both fight.</summary>
        internal static bool DebugHustingsBlows;

        /// <summary>A heckle was thrown at <paramref name="speaker"/>'s speech by <paramref name="heckler"/>: it may come to blows with a rival backing the speaker.</summary>
        private void HeckleTurns(Election e, ElectionRace race, Pawn speaker, Pawn heckler, List<Pawn> fans, List<Pawn> here)
        {
            if (!Harm.On || e == null || e.brawlRolled || heckler == null || race == null) return;
            try
            {
                var social = CouncilGameComponent.Instance?.social;
                if (social == null) return;
                // A rival of the heckler's among the speaker's supporters, and the heckler backing another candidate.
                var rival = fans.Where(f => f != heckler && social.Get(f, heckler, TieKind.Rivals) != null && Together.Can(f, heckler)).OrderByDescending(f => social.Strength(f, heckler, TieKind.Rivals))
                    .FirstOrDefault();
                // Dev mode (Harm/HarmTwoDebug.cs): any of the speaker's supporters beside the heckler stands in for the rival.
                bool forced = DebugHustingsBlows;
                if (rival == null && forced) rival = fans.FirstOrDefault(f => f != heckler && Together.Can(f, heckler));
                if (rival == null) return;
                bool backsOther = race.Candidates.Any(c => c != speaker && Supporters(race, c).Contains(heckler));
                if (!backsOther && !forced) return;
                DebugHustingsBlows = false;
                e.brawlRolled = true;
                var pm = Peacemaker;
                bool peacemaker = pm != null && here.Contains(pm);
                var manners = DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_Manners");
                bool polite = manners != null && DecreeTracker.InForceFor(heckler, manners);
                bool calm = race.Candidates.All(c => !Hot(c));
                float chance = HarmTwoMath.HustingsBrawl(peacemaker, polite, calm, FrayedTempers.Factor(rival, heckler));
                if (!forced && !Harm.Roll(chance)) return;
                // together: the supporter answers the heckle face to face at the hustings (Together.Can above).
                Line(rival, "Rimfolk_Pool_HustingsShove", heckler);
                if (!Harm.Fight(rival, heckler, "the hustings")) return;
                e.brawlA = rival;
                e.brawlB = heckler;
                e.brawlOrdered = false;
                string text = TextVariants.Vary("CC_HarmTwo_HustingsBrawl").Translate(heckler.Named("PAWN"), rival.Named("OTHER"), speaker.Named("SPEAKER")).Resolve();
                Tell.Message(NewsKind.Colony, Importance.Important, e.map, text, new LookTargets(new[] { heckler, rival }), MessageTypeDefOf.NegativeEvent);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("harm: the hustings come to blows", ex);
            }
        }

        /// <summary>A hot temper on the stage: abrasive, or volatile of nerve.</summary>
        private static bool Hot(Pawn c)
        {
            var t = c?.story?.traits;
            if (t == null) return false;
            if (t.HasTrait(TraitDefOf.Abrasive)) return true;
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            return nerves != null && t.HasTrait(nerves) && t.DegreeOfTrait(nerves) < 0;
        }

        /// <summary>
        /// The speeches wait while the hustings' brawl lasts, and once it's over the chair calls for order (one step). True
        /// while the gathering is paused round it.
        /// </summary>
        private bool BrawlPause(Election e, List<Pawn> here)
        {
            if (e?.brawlA == null || e.brawlOrdered) return false;
            bool fighting = e.brawlA.MentalStateDef == MentalStateDefOf.SocialFighting || e.brawlB?.MentalStateDef == MentalStateDefOf.SocialFighting;
            if (fighting) return true;
            e.brawlOrdered = true;
            var chair = e.chair != null && here.Contains(e.chair) && Together.Able(e.chair) ? e.chair : here.Where(Together.Able).OrderByDescending(Flavor.HumanAge).FirstOrDefault();
            // together: the chair calls the whole hustings to order, standing among them.
            if (chair != null) Line(chair, "Rimfolk_Pool_HustingsOrder", null);
            return true;
        }

        private static void Line(Pawn p, string pool, Pawn to)
        {
            string line = Flavor.FromPool(pool, p);
            if (line.NullOrEmpty()) return;
            var args = to != null ? new[] { p.Named("PAWN"), to.Named("OTHER") } : new[] { p.Named("PAWN") };
            // together: said at the hustings, to the other there when there is one (Speech.Say checks they're together).
            Speech.Say(p, line.Formatted(args).Resolve(), to, BrawlColor);
        }
    }
}
