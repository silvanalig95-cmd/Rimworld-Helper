using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Two life moments told as personal stories (Defs/ArcDefs/Rimfolk_Arcs_LifeNew.xml): getting clean of a drug with a
    // friend at one's side, and the first days on a new leg. Both start only from the game's own state: an addiction
    // in withdrawal, a replacement leg the game's own operation fitted.

    /// <summary>
    /// Getting clean: someone in the game's own drug withdrawal, and a friend (not hooked on the same thing) who sits
    /// with them in the evenings, stays one bad night at their side, and raises a cup at supper once the addiction is
    /// gone. If they take the drug again (the craving won), the story ends without blame and can start over another
    /// time (<see cref="ArcDef.again"/>). Never for an addiction nobody gets free of (luciferium).
    /// </summary>
    public class ArcWorker_GettingClean : ArcWorker
    {
        /// <summary>Their addiction to this chemical (by name), or to anything when none is named.</summary>
        public static Hediff_Addiction Addiction(Pawn p, string chemical)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (var h in hediffs)
                if (h is Hediff_Addiction a && a.Chemical != null && (chemical == null || a.Chemical.defName == chemical)) return a;
            return null;
        }

        public static bool InWithdrawal(Hediff_Addiction a) => a?.Need != null && a.Need.CurCategory == DrugDesireCategory.Withdrawal;

        /// <summary>Some addictions are for life: the story would be a lie.</summary>
        private static bool ForLife(Hediff_Addiction a) =>
            a.Chemical.defName.IndexOf("Luciferium", System.StringComparison.OrdinalIgnoreCase) >= 0 || a.def.defName.IndexOf("Luciferium", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var hediffs = p.health?.hediffSet?.hediffs;
            var a = hediffs?.OfType<Hediff_Addiction>().FirstOrDefault(x => x.Chemical != null && InWithdrawal(x) && !ForLife(x));
            if (a == null) return null;
            string chemical = a.Chemical.defName;
            var friend = ArcUtil.Closest(p, home.Where(o => o.IsColonist && !o.Downed && Addiction(o, chemical) == null), 20f);
            if (friend == null) return null;
            return new ArcCandidate { other = friend, key = chemical, weight = 2.5f };
        }

        /// <summary>A slip: still addicted and out of withdrawal, so they've had it again.</summary>
        public override string Broken(Arc arc)
        {
            var a = Addiction(arc.pawn, arc.key);
            return LifeNewMath.Relapsed(a != null, InWithdrawal(a)) ? "relapse" : null;
        }

        /// <summary>The addiction is gone (the game's own: it wears off in time without the drug).</summary>
        public override bool CustomGate(Arc arc, ArcGate gate) => Addiction(arc.pawn, arc.key) == null;
    }

    /// <summary>
    /// The new leg: the game's own operation fitted a replacement leg (a peg leg, a prosthetic, a bionic one) in the
    /// last ten days, and they can walk on it. First steps with a friend at their side (a short walk together), a
    /// frustrating day, the first full day's work in what they do best, and pride. Never for a body purist, who wants
    /// none of it. If the leg comes off again, the story ends.
    /// </summary>
    public class ArcWorker_NewLimb : ArcWorker
    {
        private static bool Leg(Hediff h) => h is Hediff_AddedPart && h.Part?.def?.tags != null && h.Part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore);

        /// <summary>The newest replacement leg fitted within this many days.</summary>
        public static Hediff NewLeg(Pawn p, float days)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            Hediff best = null;
            foreach (var h in hediffs)
                if (Leg(h) && h.ageTicks < days * GenDate.TicksPerDay && (best == null || h.ageTicks < best.ageTicks)) best = h;
            return best;
        }

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (ArcUtil.HasTrait(p, TraitDefOf.BodyPurist) || !(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false)) return null;
            var leg = NewLeg(p, 10f);
            if (leg == null) return null;
            var friend = ArcUtil.Closest(p, home.Where(o => o.IsColonist && !o.Downed && (o.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false)), 15f);
            if (friend == null) return null;
            var skill = ArcUtil.Best(p, DefDatabase<SkillDef>.AllDefsListForReading.Where(s => s.usuallyDefinedInBackstories), 0);
            if (skill == null) return null;
            return new ArcCandidate { other = friend, skill = skill, key = leg.def.defName, weight = 2.5f };
        }

        /// <summary>The leg came off again.</summary>
        public override string Broken(Arc arc)
        {
            var hediffs = arc.pawn?.health?.hediffSet?.hediffs;
            return hediffs != null && hediffs.Any(h => Leg(h) && h.def.defName == arc.key) ? null : "gone";
        }
    }
}
