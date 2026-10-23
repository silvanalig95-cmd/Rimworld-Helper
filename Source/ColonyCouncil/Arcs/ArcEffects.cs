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
        /// <summary>The third person, in a story of three (see <see cref="ArcEffect_Cast"/>).</summary>
        public Pawn third;
    }

    /// <summary>One of a story's people.</summary>
    public enum ArcPerson
    {
        Colonist,
        Other,
        Third
    }

    /// <summary>
    /// Effects with the story's people cast anew: A and B as any two of the colonist, the other person and the third (a
    /// mood for the third, a bond between the other two, a mentorship for a second student), and only if the story went
    /// this way (<see cref="ifTag"/>, the story's tag: "won", "lost|neither"). Any dilemma effect works inside it.
    /// </summary>
    public class ArcEffect_Cast : DilemmaEffect
    {
        public ArcPerson a = ArcPerson.Colonist;
        public ArcPerson b = ArcPerson.Other;
        public string ifTag;
        public List<DilemmaEffect> effects;
        /// <summary>
        /// What passes between A and B here (a memory of each other, a bond) waits until the two are together
        /// (Together.When; after a few hours one goes to find the other), for up to two days.
        /// </summary>
        public bool together;

        public static Pawn Who(Arc arc, ArcPerson p) => arc == null ? null : p == ArcPerson.Third ? arc.third : p == ArcPerson.Other ? (arc.OtherIsAnimal ? null : arc.other) : arc.pawn;

        /// <summary>Whether the story's tag is one of these ("won|lost"); none asked for, always.</summary>
        public static bool TagIs(Arc arc, string tags) => tags.NullOrEmpty() || (!(arc?.tag).NullOrEmpty() && tags.Split('|').Any(t => t.Trim() == arc.tag));

        public override void Apply(DilemmaContext ctx)
        {
            var arc = (ctx as ArcEffectContext)?.arc;
            if (arc == null || effects.NullOrEmpty() || !TagIs(arc, ifTag)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var c = new ArcEffectContext
            {
                arc = arc,
                map = ctx.map,
                a = Who(arc, a),
                b = Who(arc, b),
                third = arc.third,
                pet = ctx.pet,
                skill = ctx.skill,
                faction = ctx.faction,
                why = ctx.why,
                cause = ctx.cause
            };
            if (c.a == null || c.a.Dead) return;
            if (c.b == c.a || (c.b != null && c.b.Dead)) c.b = null;
            // together: with `together`, what passes between the two waits until they are really together, and after a
            // few hours one walks over to the other.
            if (together && c.b != null)
            {
                string key = "arccast|" + arc.def?.defName + "|" + c.a.thingIDNumber + "|" + c.b.thingIDNumber + "|" + a + b;
                Together.When(key, c.a, c.b, () => Run(c, arc), 48f, 4f);
                return;
            }
            Run(c, arc);
        }

        private void Run(ArcEffectContext c, Arc arc)
        {
            if (c.a == null || c.a.Dead || (c.b != null && c.b.Dead)) return;
            foreach (var e in effects)
            {
                try
                {
                    e?.Apply(c);
                }
                catch (System.Exception ex)
                {
                    CouncilLog.ErrorOnce("personal story effect " + arc.def?.defName + " " + e?.GetType().Name, ex);
                }
            }
        }
    }

    /// <summary>A bond between A and B grows (or, with a negative amount, fades, and may end).</summary>
    public class ArcEffect_Tie : DilemmaEffect
    {
        public TieKind kind = TieKind.Friends;
        public float amount = 10f;

        public override void Apply(DilemmaContext ctx)
        {
            var social = CouncilGameComponent.Instance?.social;
            // apart: a personal story's stage moving their bond; the talk it waited for was a game conversation, face to face.
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
                comp.quirks.Gain(comp, p, quirk, QuirkCause.Story);
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
                comp.quirks.Remove(q => q.pawn == p && quirks.Contains(q.def));
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
                string text = "CC_Arc_Passion".Translate(p.Named("PAWN"), ArcText.SkillNoun(ctx.skill).Named("SKILL")).Resolve();
                News.Post(text, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, NewsKind.People, MessageTypeDefOf.PositiveEvent, Importance.Notable);
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
            // Face to face: now if they're together, else when they are (or A goes to find B).
            comp.secrets.ConfideWhenTogether(comp, s, ctx.b);
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
            // Said out loud to whoever is there: now if anyone is, else when someone is (within a day).
            comp.secrets.ConfessInCompany(comp, s);
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
