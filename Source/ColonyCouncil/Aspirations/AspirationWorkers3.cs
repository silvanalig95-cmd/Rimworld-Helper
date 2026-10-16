using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Life goals for the young, the old, the faithful and the heart. Each reads something real in the game: the grown-ups'
    // opinion of a youngster, a love found, an animal of their own, a child grown, family at peace, a feeling let go, a
    // bond, days on the road, a faith held or shared.

    /// <summary>
    /// To be taken seriously: the grown-ups of the colony (25 or older, in human years) think well enough of them, on
    /// average, to reach <see cref="AspirationDef.amount"/>. For the young.
    /// </summary>
    public class AspirationWorker_Respect : AspirationWorker
    {
        private static float Average(Pawn p, out int count)
        {
            var grown = PawnsFinder.AllMaps_FreeColonists.Where(o => o != p && !o.IsQuestLodger() && o.relations != null && Flavor.HumanAge(o) >= 25f).ToList();
            count = grown.Count;
            return count == 0 ? 0f : (float)grown.Average(o => o.relations.OpinionOf(p));
        }

        public override bool CanHave(Pawn p)
        {
            float avg = Average(p, out int count);
            return count >= 2 && avg < def.amount - 5f;
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = Average(a.pawn, out _);
            a.target = def.amount;
            return true;
        }

        public override float Progress(Aspiration a)
        {
            float now = Average(a.pawn, out int count);
            if (count == 0) return 0f;
            return (now - a.baseline) / Mathf.Max(1f, a.target - a.baseline);
        }

        public override bool Done(Aspiration a) => Average(a.pawn, out int count) >= a.target && count > 0;

        public override string ProgressText(Aspiration a) => "CC_Asp_RespectProgress".Translate(Average(a.pawn, out _).ToString("0"), a.target.ToString("0"));
    }

    /// <summary>To find someone to love: a lover, fiancé or spouse. Someone fond of them who they are fond of counts as a start.</summary>
    public class AspirationWorker_Lover : AspirationWorker
    {
        private static bool Loved(Pawn p) => p.relations != null && LovePartnerRelationUtility.ExistingLovePartner(p, false) != null;

        public override bool CanHave(Pawn p)
        {
            var asexual = DefDatabase<TraitDef>.GetNamedSilentFail("Asexual");
            return !Loved(p) && (asexual == null || p.story?.traits?.HasTrait(asexual) != true);
        }

        public override float Progress(Aspiration a)
        {
            var p = a.pawn;
            if (Loved(p)) return 1f;
            bool someone = PawnsFinder.AllMaps_FreeColonists.Any(o => o != p && o.relations != null && p.relations != null && !ArcUtil.Kin(p, o)
                                                                      && p.relations.OpinionOf(o) >= 40 && o.relations.OpinionOf(p) >= 40);
            return someone ? 0.3f : 0f;
        }
    }

    /// <summary>An animal of their very own: one of the colony's animals bonded to them, or (half way) answering to them.</summary>
    public class AspirationWorker_OwnAnimal : AspirationWorker
    {
        private static bool Bonded(Pawn p) => p.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead);

        private static bool Master(Pawn p) => p.MapHeld?.mapPawns?.SpawnedColonyAnimals?.Any(x => x.playerSettings?.Master == p && !x.Dead) ?? false;

        public override bool CanHave(Pawn p) => !Bonded(p);

        public override float Progress(Aspiration a) => Bonded(a.pawn) ? 1f : Master(a.pawn) ? 0.5f : 0f;
    }

    /// <summary>
    /// To see a child of the colony grown (Biotech): the child they care most about (their own, family, or one they are
    /// fond of) comes of age. The child is kept by their id number (in the goal's baseline).
    /// </summary>
    public class AspirationWorker_SeeGrown : AspirationWorker
    {
        private static Pawn Child(int id) => PawnsFinder.AllMaps_FreeColonists.FirstOrDefault(c => c.thingIDNumber == id);

        private static Pawn Dearest(Pawn p) =>
            PawnsFinder.AllMaps_FreeColonists.Where(c => c != p && c.DevelopmentalStage.Juvenile() && !c.Dead && c.ageTracker != null)
                .Where(c => ArcUtil.Kin(p, c) || ArcUtil.Opinion(p, c) >= 25)
                .OrderByDescending(c => (ArcUtil.Kin(p, c) ? 50 : 0) + ArcUtil.Opinion(p, c) + c.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();

        public override bool CanHave(Pawn p) => ModsConfig.BiotechActive && Dearest(p) != null;

        public override bool Init(Aspiration a)
        {
            var c = Dearest(a.pawn);
            if (c == null) return false;
            a.baseline = c.thingIDNumber;
            a.target = 1f;
            a.sparkName = c.LabelShort;
            return true;
        }

        public override float Progress(Aspiration a)
        {
            var c = Child((int)a.baseline);
            if (c == null) return 0f;
            if (!c.DevelopmentalStage.Juvenile()) return 1f;
            var age = c.ageTracker;
            return age == null ? 0f : Mathf.Clamp01(age.AgeBiologicalYearsFloat / Mathf.Max(1f, age.AdultMinAge)) * 0.99f;
        }

        public override bool StillPossible(Aspiration a)
        {
            var c = Child((int)a.baseline);
            return c != null && !c.Dead;
        }

        public override string ProgressText(Aspiration a)
        {
            var c = Child((int)a.baseline);
            return c == null ? base.ProgressText(a) : "CC_Asp_SeeGrownProgress".Translate(c.LabelShort, (c.ageTracker?.AgeBiologicalYears ?? 0).ToString()).Resolve();
        }
    }

    /// <summary>Family at peace: every blood relative of theirs in the colony thinks well of them (20 or more), and they of each.</summary>
    public class AspirationWorker_KinPeace : AspirationWorker
    {
        private static List<Pawn> Kin(Pawn p) =>
            PawnsFinder.AllMaps_FreeColonists.Where(o => o != p && !o.Dead && !o.DevelopmentalStage.Baby() && ArcUtil.Kin(p, o)).ToList();

        private static bool AtPeace(Pawn p, Pawn o) => Mathf.Min(ArcUtil.Opinion(p, o), ArcUtil.Opinion(o, p)) >= 20;

        public override bool CanHave(Pawn p) => Kin(p).Any(o => Mathf.Min(ArcUtil.Opinion(p, o), ArcUtil.Opinion(o, p)) < 10);

        public override float Progress(Aspiration a)
        {
            var kin = Kin(a.pawn);
            return kin.Count == 0 ? 0f : kin.Count(o => AtPeace(a.pawn, o)) / (float)kin.Count;
        }

        public override bool Done(Aspiration a)
        {
            var kin = Kin(a.pawn);
            return kin.Count > 0 && kin.All(o => AtPeace(a.pawn, o));
        }

        public override bool StillPossible(Aspiration a) => Kin(a.pawn).Count > 0;

        public override string ProgressText(Aspiration a)
        {
            var kin = Kin(a.pawn);
            return "CC_Asp_CountProgress".Translate(kin.Count(o => AtPeace(a.pawn, o)), kin.Count);
        }
    }

    /// <summary>
    /// To let a feeling go (grief, shame): <see cref="AspirationDef.feeling"/> faint or gone (under a sixth of full
    /// strength) for <see cref="AspirationDef.amount"/> days in a row, looked at once a day. Only for someone who feels it
    /// strongly now, and only while feelings are on.
    /// </summary>
    public class AspirationWorker_LetGo : AspirationWorker
    {
        private float Share(Pawn p)
        {
            var f = def.feeling.NullOrEmpty() ? null : ArcUtil.Feeling(p, def.feeling);
            return f?.Share ?? 0f;
        }

        public override bool CanHave(Pawn p) => Heart.On && !def.feeling.NullOrEmpty() && Share(p) >= 0.35f;

        public override bool Init(Aspiration a)
        {
            a.baseline = 0f;
            a.target = Mathf.Max(1f, def.amount);
            return true;
        }

        public override void Daily(Aspiration a) => a.baseline = Share(a.pawn) < 0.16f ? a.baseline + 1f : 0f;

        public override float Progress(Aspiration a) => a.baseline / Mathf.Max(1f, a.target);

        public override bool StillPossible(Aspiration a) => Heart.On;

        public override string ProgressText(Aspiration a) => "CC_Asp_StreakProgress".Translate(a.baseline.ToString("0"), a.target.ToString("0"));
    }

    /// <summary>A bond of <see cref="AspirationDef.tie"/> with anyone, at least <see cref="AspirationDef.amount"/> strong (a friend for life).</summary>
    public class AspirationWorker_TieStrength : AspirationWorker
    {
        private float Best(Pawn p) =>
            CouncilGameComponent.Instance?.social?.ties.Where(t => t.kind == def.tie && t.announced && t.Involves(p)).Select(t => t.strength).DefaultIfEmpty(0f).Max() ?? 0f;

        public override bool CanHave(Pawn p) => ColonyCouncilMod.Settings.socialWeb && Best(p) < def.amount - 10f;

        public override bool Init(Aspiration a)
        {
            a.baseline = 0f;
            a.target = def.amount;
            return true;
        }

        public override float Progress(Aspiration a) => Best(a.pawn) / Mathf.Max(1f, a.target);

        public override bool StillPossible(Aspiration a) => ColonyCouncilMod.Settings.socialWeb;

        public override string ProgressText(Aspiration a) => "CC_Asp_TieProgress".Translate(Best(a.pawn).ToString("0"), a.target.ToString("0"));
    }

    /// <summary>To see what lies past the hills: <see cref="AspirationDef.amount"/> days away from home on journeys, counted once a day.</summary>
    public class AspirationWorker_Journey : AspirationWorker
    {
        public override bool Init(Aspiration a)
        {
            a.baseline = 0f;
            a.target = Mathf.Max(1f, def.amount);
            return true;
        }

        public override void Daily(Aspiration a)
        {
            var p = a.pawn;
            bool away = p != null && !p.Dead && !(p.MapHeld?.IsPlayerHome ?? false) && (p.MapHeld != null || p.GetCaravan() != null);
            if (away) a.baseline += 1f;
        }

        public override float Progress(Aspiration a) => a.baseline / Mathf.Max(1f, a.target);

        public override string ProgressText(Aspiration a) => "CC_Asp_JourneyProgress".Translate(a.baseline.ToString("0"), a.target.ToString("0"));
    }

    /// <summary>
    /// Faith found again (Ideology): their certainty, low now (under 60%), back up to <see cref="AspirationDef.amount"/>
    /// in the same faith. A change of faith ends it.
    /// </summary>
    public class AspirationWorker_Certainty : AspirationWorker
    {
        public override bool CanHave(Pawn p) =>
            ModsConfig.IdeologyActive && p.ideo != null && p.Ideo != null && !p.DevelopmentalStage.Juvenile() && p.ideo.Certainty < 0.6f;

        public override bool Init(Aspiration a)
        {
            if (a.pawn.ideo == null || a.pawn.Ideo == null) return false;
            a.baseline = a.pawn.ideo.Certainty;
            a.target = a.pawn.Ideo.id;
            return true;
        }

        private float Goal => def.amount > 0f && def.amount <= 1f ? def.amount : 0.9f;

        public override float Progress(Aspiration a)
        {
            float now = a.pawn.ideo?.Certainty ?? 0f;
            return (now - a.baseline) / Mathf.Max(0.05f, Goal - a.baseline);
        }

        public override bool Done(Aspiration a) => (a.pawn.ideo?.Certainty ?? 0f) >= Goal;

        public override bool StillPossible(Aspiration a) => ModsConfig.IdeologyActive && a.pawn.Ideo != null && a.pawn.Ideo.id == (int)a.target;

        public override string ProgressText(Aspiration a) => "CC_Asp_CertaintyProgress".Translate((a.pawn.ideo?.Certainty ?? 0f).ToStringPercent(), Goal.ToStringPercent());
    }

    /// <summary>
    /// To bring someone to their faith (Ideology): one more colonist sharing it than when they set their heart on it, in a
    /// colony where some still don't. A change of faith ends it.
    /// </summary>
    public class AspirationWorker_FaithGrows : AspirationWorker
    {
        private static int Sharing(Pawn p, out int total)
        {
            var all = PawnsFinder.AllMaps_FreeColonists.Where(c => !c.IsQuestLodger() && c.ideo != null && !c.DevelopmentalStage.Baby()).ToList();
            total = all.Count;
            var ideo = p.Ideo;
            return ideo == null ? 0 : all.Count(c => c.Ideo == ideo);
        }

        public override bool CanHave(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p.Ideo == null || p.ideo == null || p.DevelopmentalStage.Juvenile() || p.ideo.Certainty < 0.5f) return false;
            int have = Sharing(p, out int total);
            return total >= 3 && have < total;
        }

        public override bool Init(Aspiration a)
        {
            if (a.pawn.Ideo == null) return false;
            a.baseline = Sharing(a.pawn, out _);
            a.target = a.pawn.Ideo.id;
            return true;
        }

        public override float Progress(Aspiration a) => Sharing(a.pawn, out _) > a.baseline ? 1f : 0f;

        public override bool StillPossible(Aspiration a) => ModsConfig.IdeologyActive && a.pawn.Ideo != null && a.pawn.Ideo.id == (int)a.target;

        public override string ProgressText(Aspiration a)
        {
            int have = Sharing(a.pawn, out int total);
            return "CC_Asp_FaithProgress".Translate(have, total);
        }
    }
}
