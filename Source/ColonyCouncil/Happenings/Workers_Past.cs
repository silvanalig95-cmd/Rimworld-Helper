using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== Someone asks to join =====================

    /// <summary>
    /// Someone asks to join the colony. They're made now and wait out in the world until the colony answers, then walk
    /// in from the edge (or go their way). Subclasses decide who A is and who X is to A.
    /// </summary>
    public class HappeningWorker_Joiner : IncidentWorker_Happening
    {
        protected virtual bool FitsA(Pawn a, HappeningExtension ext) => ext.colonist == null ? !a.DevelopmentalStage.Juvenile() : ext.colonist.Allows(a, null);

        protected virtual float WeightA(Pawn a, HappeningExtension ext) => 1f;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Any(p => FitsA(p, ext));

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Colonists(ctx.map).Where(p => FitsA(p, ext)).RandomElementByWeightWithFallback(p => WeightA(p, ext));
            if (ctx.a == null) return false;
            // A Hospitality guest who fits the part is the one (Strangers/GuestStories.cs); else someone is made.
            var x = GuestStories.On && GuestStories.TryCast(ctx, ext, g => FitsGuest(g, ctx, ext)) ? ctx.stranger : null;
            if (x != null) GuestCast(x, ctx, ext);
            x ??= MakeX(ctx, ext);
            if (x == null) return false;
            ctx.stranger = x;
            if (!x.Spawned)
            {
                ctx.xWaiting = true;
                Arrivals.Hold(x);
            }
            if (ctx.relation == null) ctx.relation = ctx.a.GetMostImportantRelation(x)?.GetGenderSpecificLabel(x);
            if (ext.amount.max > 0) ctx.count = ext.amount.RandomInRange;
            return true;
        }

        protected virtual Pawn MakeX(HappeningContext ctx, HappeningExtension ext) => Arrivals.Make(Arrivals.JoinerKind(), null, ext.ages);

        /// <summary>Whether a guest staying with the colony could be the story's stranger (none by default), and what the story makes of them when so.</summary>
        protected virtual bool FitsGuest(Pawn g, HappeningContext ctx, HappeningExtension ext) => false;

        protected virtual void GuestCast(Pawn g, HappeningContext ctx, HappeningExtension ext)
        {
        }

        protected static bool Sane(FloatRange r) => r.min >= 0f && r.max >= r.min;

        /// <summary>Only people of A's own kind can be their family (an elf's sister is an elf).</summary>
        protected static bool SameRaceAsJoiners(Pawn a) => a.def == Arrivals.JoinerKind()?.race;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a == null) return;
            PastTags(ctx, tags);
            if (LovePartnerRelationUtility.ExistingMostLikedLovePartner(ctx.a, false) != null) tags.Add("partner");
            if (ctx.stranger != null) tags.Add(ctx.stranger.gender == Gender.Female ? "female" : "male");
        }

        protected virtual void PastTags(HappeningContext ctx, HashSet<string> tags)
        {
            foreach (var k in Pasts.Of(ctx.a)) tags.Add(Pasts.Tag(k));
        }

        /// <summary>A newcomer with a fighter's past knows how to fight.</summary>
        protected static void MakeCapable(Pawn x, int min)
        {
            foreach (var sk in new[] { SkillDefOf.Shooting, SkillDefOf.Melee })
            {
                var r = x.skills?.GetSkill(sk);
                if (r != null && !r.TotallyDisabled && r.Level < min) r.Level = min + Rand.RangeInclusive(0, 3);
            }
        }
    }

    /// <summary>Family A thought they'd lost: a sibling, a parent, a grown child.</summary>
    public class HappeningWorker_LostKin : HappeningWorker_Joiner
    {
        protected override bool FitsA(Pawn a, HappeningExtension ext) =>
            base.FitsA(a, ext) && a.Name is NameTriple && SameRaceAsJoiners(a) && Flavor.HumanAge(a) >= 18f && Flavor.HumanAge(a) <= 75f;

        protected override Pawn MakeX(HappeningContext ctx, HappeningExtension ext)
        {
            var a = ctx.a;
            float age = a.ageTracker.AgeBiologicalYearsFloat;
            string last = (a.Name as NameTriple)?.Last;
            var father = a.GetFather();
            var mother = a.GetMother();
            var kinds = new List<string>();
            if (father != null || mother != null) kinds.Add("sibling");
            if (age <= 50f && (father == null || mother == null)) kinds.Add("parent");
            if (age >= 36f) kinds.Add("child");
            if (kinds.Count == 0) return null;
            var kind = Arrivals.JoinerKind();
            switch (kinds.RandomElement())
            {
                case "sibling":
                {
                    var range = new FloatRange(Mathf.Max(16f, age - 12f), Mathf.Min(70f, age + 12f));
                    if (!Sane(range)) return null;
                    var x = Arrivals.Make(kind, null, range, null, last, false, a);
                    if (father != null) x.SetFather(father);
                    if (mother != null) x.SetMother(mother);
                    return x;
                }
                case "parent":
                {
                    var g = father == null && mother == null ? (Rand.Bool ? Gender.Male : Gender.Female) : father == null ? Gender.Male : Gender.Female;
                    var range = new FloatRange(age + 18f, Mathf.Min(age + 40f, 88f));
                    if (!Sane(range)) return null;
                    var x = Arrivals.Make(kind, null, range, g, g == Gender.Male ? last : null, false, a);
                    a.relations.AddDirectRelation(PawnRelationDefOf.Parent, x);
                    return x;
                }
                default:
                {
                    var range = new FloatRange(16f, age - 18f);
                    if (!Sane(range)) return null;
                    var x = Arrivals.Make(kind, null, range, null, a.gender == Gender.Male ? last : null, false, a);
                    x.relations.AddDirectRelation(PawnRelationDefOf.Parent, a);
                    return x;
                }
            }
        }
    }

    /// <summary>Someone A once loved.</summary>
    public class HappeningWorker_OldFlame : HappeningWorker_Joiner
    {
        protected override bool FitsA(Pawn a, HappeningExtension ext)
        {
            if (!base.FitsA(a, ext) || !SameRaceAsJoiners(a) || Flavor.HumanAge(a) < 20f || Flavor.HumanAge(a) > 70f) return false;
            return !(a.story?.traits?.HasTrait(TraitDefOf.Asexual) ?? false);
        }

        /// <summary>A guest of the right gender and near enough in age, not family, was the old flame.</summary>
        protected override bool FitsGuest(Pawn g, HappeningContext ctx, HappeningExtension ext)
        {
            var a = ctx.a;
            if (a == null || g == null || g.def != a.def || a.relations == null || a.relations.DirectRelationExists(PawnRelationDefOf.ExLover, g) || a.relations.FamilyByBlood.Contains(g)) return false;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(a, g)) return false;
            var traits = a.story?.traits;
            bool fits = traits != null && traits.HasTrait(TraitDefOf.Gay) ? g.gender == a.gender
                : traits != null && traits.HasTrait(TraitDefOf.Bisexual) || g.gender != a.gender;
            float age = a.ageTracker.AgeBiologicalYearsFloat, theirs = g.ageTracker.AgeBiologicalYearsFloat;
            return fits && theirs >= 18f && Mathf.Abs(age - theirs) <= 10f;
        }

        protected override void GuestCast(Pawn g, HappeningContext ctx, HappeningExtension ext) => ctx.a.relations.AddDirectRelation(PawnRelationDefOf.ExLover, g);

        protected override Pawn MakeX(HappeningContext ctx, HappeningExtension ext)
        {
            var a = ctx.a;
            var traits = a.story?.traits;
            Gender g;
            if (traits != null && traits.HasTrait(TraitDefOf.Gay)) g = a.gender;
            else if (traits != null && traits.HasTrait(TraitDefOf.Bisexual)) g = Rand.Bool ? Gender.Male : Gender.Female;
            else g = a.gender == Gender.Male ? Gender.Female : Gender.Male;
            float age = a.ageTracker.AgeBiologicalYearsFloat;
            var range = new FloatRange(Mathf.Max(18f, age - 8f), Mathf.Min(75f, age + 8f));
            if (!Sane(range)) return null;
            var x = Arrivals.Make(Arrivals.JoinerKind(), null, range, g);
            a.relations.AddDirectRelation(PawnRelationDefOf.ExLover, x);
            return x;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // apart: the answer to an event, told in its letter.
            // An ex moving in doesn't sit well with the current partner.
            if (ctx.stranger != null && ctx.stranger.Faction == Faction.OfPlayer && ctx.a != null)
            {
                var partner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(ctx.a, false);
                if (partner != null && partner != ctx.stranger && partner.IsColonist)
                    CouncilGameComponent.GiveThought(partner, CouncilDefOf.Council_Evt_PartnersEx, ctx.stranger);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Someone A fought beside (or ran with) in the old days.</summary>
    public class HappeningWorker_OldComrade : HappeningWorker_Joiner
    {
        protected override bool FitsA(Pawn a, HappeningExtension ext) =>
            base.FitsA(a, ext) && (Pasts.Has(a, Past.Soldier) || Pasts.Has(a, Past.Outlaw));

        protected override Pawn MakeX(HappeningContext ctx, HappeningExtension ext)
        {
            // One past for the whole story: a soldier's comrade, or a partner in crime.
            bool soldier = Pasts.Has(ctx.a, Past.Soldier), outlaw = Pasts.Has(ctx.a, Past.Outlaw);
            ctx.variant = soldier && outlaw ? (Rand.Bool ? 0 : 1) : outlaw ? 1 : 0;
            var x = Arrivals.Make(Arrivals.JoinerKind(), null, ext.ages);
            MakeCapable(x, 6);
            return x;
        }

        protected override void PastTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "outlaw" : "soldier");
    }

    /// <summary>A young person who wants to learn from the colony's master of a craft.</summary>
    public class HappeningWorker_Apprentice : HappeningWorker_Joiner
    {
        private static SkillRecord Mastery(Pawn p) =>
            p.skills?.skills.Where(r => !r.TotallyDisabled && r.Level >= 12 && People.Passionate(r)).OrderByDescending(r => r.Level).FirstOrDefault();

        protected override bool FitsA(Pawn a, HappeningExtension ext) => base.FitsA(a, ext) && Mastery(a) != null;

        protected override float WeightA(Pawn a, HappeningExtension ext) => Mastery(a)?.Level ?? 1f;

        /// <summary>A young guest with a little of the master's skill and room to grow was the would-be apprentice.</summary>
        protected override bool FitsGuest(Pawn g, HappeningContext ctx, HappeningExtension ext)
        {
            var sk = Mastery(ctx.a)?.def;
            if (sk == null || g?.skills == null) return false;
            var r = g.skills.GetSkill(sk);
            return r != null && !r.TotallyDisabled && r.Level <= 7 && Flavor.HumanAge(g) <= 30f;
        }

        protected override void GuestCast(Pawn g, HappeningContext ctx, HappeningExtension ext)
        {
            ctx.skill = Mastery(ctx.a)?.def;
            var r = ctx.skill != null ? g.skills?.GetSkill(ctx.skill) : null;
            if (r != null && r.passion == Passion.None) r.passion = Passion.Minor;
        }

        protected override Pawn MakeX(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.skill = Mastery(ctx.a)?.def;
            if (ctx.skill == null) return null;
            for (int i = 0; i < 4; i++)
            {
                var x = Arrivals.Make(Arrivals.JoinerKind(), null, ext.ages);
                var r = x.skills?.GetSkill(ctx.skill);
                if (r == null || r.TotallyDisabled)
                {
                    Arrivals.Discard(x);
                    continue;
                }
                r.Level = Rand.RangeInclusive(2, 5);
                r.passion = Passion.Major;
                return x;
            }
            return null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.skill != null) tags.Add(ctx.skill.defName);
        }
    }

    /// <summary>A soldier who ran from a hostile people, asking for shelter. Their old comrades may come looking.</summary>
    public class HappeningWorker_Deserter : HappeningWorker_Joiner
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Raids.Enemy() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.enemy = Raids.Enemy();
            return ctx.enemy != null && base.Cast(ctx, ext);
        }

        protected override Pawn MakeX(HappeningContext ctx, HappeningExtension ext)
        {
            var f = ctx.enemy;
            var kind = Arrivals.PersonKind(f);
            if (kind == null || kind.RaceProps?.Humanlike != true) return null;
            var x = Arrivals.Make(kind, f, ext.ages);
            // They belong to no one now.
            x.SetFaction(null);
            MakeCapable(x, 5);
            return x;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Taken in (openly, or as drowned), and nobody came looking (yet): their old unit may find them one day.
            if ((option == 0 || option == 3) && !risk && ctx.stranger != null && ctx.stranger.Faction == Faction.OfPlayer && ctx.enemy != null)
                CouncilGameComponent.Instance?.happenings.Remember("deserter", ctx.stranger, ctx.enemy);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Someone who walked out (or was sent away) comes back and asks to be let in again.</summary>
    public class HappeningWorker_Prodigal : HappeningWorker_Joiner
    {
        private static Departed Usable(HappeningTracker t)
        {
            int now = Find.TickManager.TicksGame;
            return t.departed.Where(d =>
            {
                var p = d.pawn;
                return p != null && !p.Dead && !p.Discarded && !p.Spawned && Find.WorldPawns.Contains(p) && now - d.tick > GenDate.TicksPerDay * 12
                       && !p.IsPrisoner && !People.Kidnapped(p) && p.GetCaravan() == null && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)
                       && (p.Faction == null || !p.Faction.HostileTo(Faction.OfPlayer) || Rand.Chance(0.3f))
                       && !(CouncilGameComponent.Instance?.nemeses.IsNemesis(p) ?? false);
            }).RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            return t != null && t.departed.Count > 0 && Usable(t) != null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var d = t == null ? null : Usable(t);
            if (d == null) return false;
            var x = d.pawn;
            ctx.stranger = x;
            ctx.xWaiting = true;
            ctx.variant = d.quit ? 1 : 0;
            var colonists = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            // Whoever was closest to them hears first.
            ctx.a = colonists.OrderByDescending(p => p.relations?.OpinionOf(x) ?? 0).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.relation = ctx.a.GetMostImportantRelation(x)?.GetGenderSpecificLabel(x);
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            // They were someone before this event: never thrown away.
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add(ctx.variant == 1 ? "quit" : "banished");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            var comp = CouncilGameComponent.Instance;
            var d = comp?.happenings.departed.FirstOrDefault(e => e.pawn == x);
            if (d != null) comp.happenings.Forget(d);
            // apart: someone come home after years away, told in the event's letter.
            if (x != null && x.Faction == Faction.OfPlayer && ctx.map != null)
            {
                foreach (var p in Colonists(ctx.map).Where(p => p != x && p.relations != null))
                {
                    int o = p.relations.OpinionOf(x);
                    if (o >= 15) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_WelcomeBack, x);
                    else if (o <= -15) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_UnwelcomeBack, x);
                }
                comp?.standings.Change(x, 20f, "CC_Evt_Trust_Back".Translate());
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        public override void Cleanup(HappeningContext ctx)
        {
            // Turned away: they keep their place in the world (and in people's memories), nothing more.
            var x = ctx.stranger;
            if (x != null && !x.Spawned && x.Faction != Faction.OfPlayer) Arrivals.Release(x);
        }
    }

    // ===================== Old debts and old crimes =====================

    /// <summary>Someone A owes money to has tracked them down.</summary>
    public class HappeningWorker_Creditor : IncidentWorker_Happening
    {
        private static float Weight(Pawn p) =>
            Pasts.Has(p, Past.Outlaw) || Pasts.Has(p, Past.Trader) ? 4f : Pasts.Has(p, Past.Noble) ? 2f : 0.5f;

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Raids.Enemy() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && (ext.colonist == null || ext.colonist.Allows(p, null)))
                .RandomElementByWeightWithFallback(Weight);
            ctx.enemy = Raids.Enemy();
            if (ctx.a == null || ctx.enemy == null) return false;
            float wealth = Mathf.Clamp(ctx.map.wealthWatcher.WealthTotal / 50000f, 0.5f, 3f);
            ctx.count = Mathf.RoundToInt(ext.amount.RandomInRange * wealth / 10f) * 10;
            return ctx.count > 0;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            foreach (var k in Pasts.Of(ctx.a)) tags.Add(Pasts.Tag(k));
        }
    }

    /// <summary>Hunters come for a colonist with a price on their head: stand by them, pay, or hand them over.</summary>
    public class HappeningWorker_BountyHunters : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile() && Pasts.Violent(p)) && Raids.Enemy() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && Pasts.Violent(p))
                .RandomElementByWeightWithFallback(p => Pasts.Has(p, Past.Outlaw) ? 3f : 1f);
            ctx.enemy = Raids.Enemy();
            if (ctx.a == null || ctx.enemy == null) return false;
            float points = StorytellerUtility.DefaultThreatPointsNow(ctx.map);
            ctx.count = Mathf.Clamp(Mathf.RoundToInt(points * Rand.Range(1.2f, 1.8f) / 10f) * 10, 200, 3000);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            foreach (var k in Pasts.Of(ctx.a)) tags.Add(Pasts.Tag(k));
        }
    }

    // ===================== News from the old life =====================

    /// <summary>
    /// A letter, a message, a rumour from A's old life: a teacher's advice, good news, bad news, an inheritance.
    /// The skill is A's best passion (for the teacher's advice).
    /// </summary>
    public class HappeningWorker_News : IncidentWorker_Happening
    {
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.skill = ctx.a.skills?.skills.Where(r => !r.TotallyDisabled && People.Passionate(r)).OrderByDescending(r => r.Level).FirstOrDefault()?.def
                        ?? ctx.a.skills?.skills.Where(r => !r.TotallyDisabled).OrderByDescending(r => r.Level).FirstOrDefault()?.def;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            foreach (var k in Pasts.Of(ctx.a)) tags.Add(Pasts.Tag(k));
            if (ctx.skill != null) tags.Add(ctx.skill.defName);
        }
    }
}
