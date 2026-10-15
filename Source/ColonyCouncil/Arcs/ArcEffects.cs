using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The people of a personal story, for its effects: A is the colonist, B the other person (or null), PET the animal,
    /// SKILL the story's skill. Stages use the dilemmas' effects (thoughts, trust, inspirations, lessons, strain) and these.
    /// </summary>
    public class ArcEffectContext : DilemmaContext
    {
        public Arc arc;
    }

    /// <summary>A bond between A and B grows (or, with a negative amount, fades, and may end).</summary>
    public class ArcEffect_Tie : DilemmaEffect
    {
        public TieKind kind = TieKind.Friends;
        public float amount = 10f;

        public override void Apply(DilemmaContext ctx)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || ctx.a == null || ctx.b == null || amount == 0f) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            if (amount > 0f) social.Bump(ctx.a, ctx.b, kind, amount, ctx.why);
            else social.Weaken(ctx.a, ctx.b, kind, -amount, ctx.why);
        }
    }

    /// <summary>Strain lifted (a negative amount) as a weight comes off: catharsis, in the heart's terms.</summary>
    public class ArcEffect_Strain : DilemmaEffect
    {
        public float amount = -8f;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Heart.On || amount == 0f) return;
            foreach (var p in Targets(ctx).ToList())
                if (Heart.Eligible(p)) HeartLogic.Change(p, amount, StrainRelief.Catharsis, StrainSource.Council);
        }
    }

    /// <summary>A quirk the story leaves behind (if they have room for one).</summary>
    public class ArcEffect_Quirk : DilemmaEffect
    {
        public QuirkDef quirk;

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || quirk == null || !ColonyCouncilMod.Settings.quirks) return;
            foreach (var p in Targets(ctx).ToList())
            {
                if (comp.quirks.Get(p, quirk) != null || comp.quirks.Of(p).Count() >= QuirkTracker.MaxPerPawn) continue;
                comp.quirks.Gain(comp, p, quirk);
            }
        }
    }

    /// <summary>Quirks the story ends (a fear faced): they're simply gone.</summary>
    public class ArcEffect_RemoveQuirk : DilemmaEffect
    {
        public List<QuirkDef> quirks;

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || quirks.NullOrEmpty()) return;
            foreach (var p in Targets(ctx).ToList())
                comp.quirks.quirks.RemoveAll(q => q.pawn == p && quirks.Contains(q.def));
        }
    }

    /// <summary>
    /// A name the colony gives them for what the story showed (the Wanderer, Steadyhand): only if nobody living holds it
    /// already, and, for a journey, only after a long one.
    /// </summary>
    public class ArcEffect_Epithet : DilemmaEffect
    {
        public EpithetDef epithet;
        /// <summary>Only after a journey of at least this many days (the long way home).</summary>
        public int minAwayDays;

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || epithet == null || !ColonyCouncilMod.Settings.epithets) return;
            var arc = (ctx as ArcEffectContext)?.arc;
            if (minAwayDays > 0 && (arc == null || arc.awayDays < minAwayDays)) return;
            foreach (var p in Targets(ctx).ToList()) comp.epithets.Bestow(epithet, p, comp);
        }
    }

    /// <summary>
    /// A passion found or deepened in the story's skill: none becomes an interest, an interest a burning passion. Very
    /// rare, by its chance. Passions from mods are left alone.
    /// </summary>
    public class ArcEffect_Passion : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.skill == null) return;
            foreach (var p in Targets(ctx).ToList())
            {
                var rec = p.skills?.GetSkill(ctx.skill);
                if (rec == null || rec.TotallyDisabled) continue;
                if (rec.passion == Passion.None) rec.passion = Passion.Minor;
                else if (rec.passion == Passion.Minor) rec.passion = Passion.Major;
                else continue;
                string text = "CC_Arc_Passion".Translate(p.Named("PAWN"), ctx.skill.label.Named("SKILL")).Resolve();
                Notify.Message(text, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
            }
        }
    }

    /// <summary>A real mentorship in the social web, in the story's skill: B teaches A (or A teaches B).</summary>
    public class ArcEffect_Mentorship : DilemmaEffect
    {
        /// <summary>A is the teacher (an elder passing a craft on) rather than the student.</summary>
        public bool aTeaches;

        public override void Apply(DilemmaContext ctx)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || ctx.a == null || ctx.b == null || ctx.skill == null) return;
            if (aTeaches) social.StartMentorship(ctx.a, ctx.b, ctx.skill);
            else social.StartMentorship(ctx.b, ctx.a, ctx.skill);
        }
    }

    /// <summary>A tells B their secret (Rimfolk's secrets: the friend knows now, with what that brings).</summary>
    public class ArcEffect_Confide : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var s = comp?.secrets.Of(ctx.a);
            if (s == null || s.revealed || ctx.b == null || s.knowers.Contains(ctx.b)) return;
            comp.secrets.Confide(comp, s, ctx.b);
        }
    }

    /// <summary>A tells everyone: the secret comes out, confessed, with everything that follows.</summary>
    public class ArcEffect_Confess : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var s = comp?.secrets.Of(ctx.a);
            if (s == null || s.revealed) return;
            comp.secrets.Reveal(comp, s, ctx.a);
        }
    }

    /// <summary>A's vendetta against the story's faction is laid down, without the satisfaction of vengeance.</summary>
    public class ArcEffect_SettleVendetta : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var faction = (ctx as ArcEffectContext)?.arc?.faction ?? ctx.faction;
            if (comp == null || ctx.a == null || faction == null) return;
            foreach (var v in comp.vendettas.vendettas.Where(v => v.avenger == ctx.a && v.faction == faction && !v.settled)) v.settled = true;
        }
    }

    /// <summary>A and the story's animal bond, the game's own way (if the game allows a bond between them).</summary>
    public class ArcEffect_AnimalBond : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var p = ctx.a;
            var pet = ctx.pet;
            if (p?.relations == null || pet == null || pet.Dead || pet.Faction != Faction.OfPlayer) return;
            if (p.relations.DirectRelationExists(PawnRelationDefOf.Bond, pet)) return;
            RelationsUtility.TryDevelopBondRelation(p, pet, 1f);
        }
    }
}
