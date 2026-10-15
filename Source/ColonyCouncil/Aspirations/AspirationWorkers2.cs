using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An older child's goal (Biotech): to grow up. It comes true when they come of age; a while later they choose a
    /// grown-up's goal, so a life's goals change as it goes on. Only from <see cref="AspirationDef.amount"/> years on, so
    /// it never waits longer than the few years a goal is kept.
    /// </summary>
    public class AspirationWorker_GrowUp : AspirationWorker
    {
        public override bool CanHave(Pawn p) =>
            p.ageTracker != null && p.DevelopmentalStage.Child() && p.ageTracker.AgeBiologicalYearsFloat >= def.amount
            && p.ageTracker.AgeBiologicalYearsFloat < p.ageTracker.AdultMinAge;

        public override bool Init(Aspiration a)
        {
            var age = a.pawn.ageTracker;
            if (age == null) return false;
            a.baseline = age.AgeBiologicalYearsFloat;
            a.target = Mathf.Max(a.baseline + 0.5f, age.AdultMinAge);
            return true;
        }

        public override float Progress(Aspiration a)
        {
            if (!a.pawn.DevelopmentalStage.Juvenile()) return 1f;
            float now = a.pawn.ageTracker?.AgeBiologicalYearsFloat ?? a.baseline;
            return Mathf.Clamp01((now - a.baseline) / Mathf.Max(0.5f, a.target - a.baseline)) * 0.99f;
        }

        public override bool Done(Aspiration a) => !a.pawn.DevelopmentalStage.Juvenile();

        public override string ProgressText(Aspiration a) =>
            "CC_Asp_GrowUpProgress".Translate((a.pawn.ageTracker?.AgeBiologicalYears ?? 0).ToString(), a.target.ToString("0"));
    }

    /// <summary>
    /// The old: to see a grandchild born (Biotech). One of their children, grown and young enough for it, lives in the
    /// colony; a pregnancy among them counts as halfway.
    /// </summary>
    public class AspirationWorker_Grandchild : AspirationWorker
    {
        private static IEnumerable<Pawn> Children(Pawn p) => p.relations?.Children.Where(c => c != null) ?? Enumerable.Empty<Pawn>();

        private static int Grandchildren(Pawn p) =>
            Children(p).SelectMany(c => c.relations?.Children ?? Enumerable.Empty<Pawn>()).Where(g => g != null && !g.Dead).Distinct().Count();

        private static bool Expecting(Pawn p)
        {
            var pregnant = DefDatabase<HediffDef>.GetNamedSilentFail("PregnantHuman");
            if (pregnant == null) return false;
            foreach (var c in Children(p).Where(c => !c.Dead))
            {
                if (c.health?.hediffSet?.HasHediff(pregnant) ?? false) return true;
                var partner = LovePartnerRelationUtility.ExistingLovePartner(c, false);
                if (partner?.health?.hediffSet?.HasHediff(pregnant) ?? false) return true;
            }
            return false;
        }

        public override bool CanHave(Pawn p) =>
            ModsConfig.BiotechActive && Children(p).Any(c => !c.Dead && c.IsColonist && !c.DevelopmentalStage.Juvenile() && Flavor.HumanAge(c) < 45f);

        public override bool Init(Aspiration a)
        {
            a.baseline = Grandchildren(a.pawn);
            a.target = a.baseline + 1;
            return true;
        }

        public override float Progress(Aspiration a) => Grandchildren(a.pawn) >= a.target ? 1f : Expecting(a.pawn) ? 0.5f : 0f;

        public override bool StillPossible(Aspiration a) => Progress(a) >= 1f || CanHave(a.pawn);
    }

    /// <summary>A believer's goal (Ideology): to take a role in their faith: a moral guide, a specialist, its leader.</summary>
    public class AspirationWorker_IdeoRole : AspirationWorker
    {
        public override bool CanHave(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p.Ideo == null || p.ideo == null || p.DevelopmentalStage.Juvenile() || p.IsSlave) return false;
            if (p.ideo.Certainty < 0.5f || p.Ideo.GetRole(p) != null) return false;
            return p.Ideo.RolesListForReading.Any(r => r != null && r.Active);
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = a.pawn.Ideo?.id ?? -1;
            a.target = 1f;
            return true;
        }

        public override float Progress(Aspiration a) => a.pawn.Ideo != null && a.pawn.Ideo.GetRole(a.pawn) != null ? 1f : 0f;

        /// <summary>They lost their faith, or changed it: the goal goes with it.</summary>
        public override bool StillPossible(Aspiration a) => ModsConfig.IdeologyActive && a.pawn.Ideo != null && a.pawn.Ideo.id == (int)a.baseline;
    }

    /// <summary>A devout believer's goal (Ideology): everyone in the colony sharing their faith.</summary>
    public class AspirationWorker_SharedFaith : AspirationWorker
    {
        private static List<Pawn> Colonists() => PawnsFinder.AllMaps_FreeColonists.Where(c => !c.IsQuestLodger() && c.ideo != null && !c.DevelopmentalStage.Baby()).ToList();

        private static int Sharing(Pawn p, out int total)
        {
            var all = Colonists();
            total = all.Count;
            var ideo = p.Ideo;
            return ideo == null ? 0 : all.Count(c => c.Ideo == ideo);
        }

        public override bool CanHave(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p.Ideo == null || p.ideo == null || p.DevelopmentalStage.Juvenile() || p.ideo.Certainty < 0.7f) return false;
            int have = Sharing(p, out int total);
            return total >= 3 && have < total;
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = a.pawn.Ideo?.id ?? -1;
            a.target = 1f;
            return true;
        }

        public override float Progress(Aspiration a)
        {
            int have = Sharing(a.pawn, out int total);
            return total == 0 ? 0f : have / (float)total;
        }

        public override bool Done(Aspiration a)
        {
            int have = Sharing(a.pawn, out int total);
            return total > 0 && have >= total;
        }

        public override bool StillPossible(Aspiration a) => ModsConfig.IdeologyActive && a.pawn.Ideo != null && a.pawn.Ideo.id == (int)a.baseline;

        public override string ProgressText(Aspiration a)
        {
            int have = Sharing(a.pawn, out int total);
            return "CC_Asp_FaithProgress".Translate(have, total);
        }
    }

    /// <summary>
    /// The heart's goals: to feel at home (belonging), to live free (freedom). The need well met (at least
    /// <see cref="AspirationDef.level"/>) for <see cref="AspirationDef.amount"/> days in a row, looked at once a day; a day
    /// short of it starts the count again. Only while feelings and the heart's needs are on.
    /// </summary>
    public class AspirationWorker_HeartStreak : AspirationWorker
    {
        /// <summary>The need now (0 to 100), or -1 when it isn't known.</summary>
        private float Value(Pawn p)
        {
            var needs = Heart.Of(p)?.needs;
            int i = (int)def.heartNeed;
            return needs != null && i >= 0 && i < needs.Length ? needs[i].value : -1f;
        }

        /// <summary>The heart is on, and they don't have it now.</summary>
        public override bool CanHave(Pawn p)
        {
            if (!Heart.On) return false;
            float v = Value(p);
            return v >= 0f && v < def.level;
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = 0f;
            a.target = Mathf.Max(1f, def.amount);
            return true;
        }

        public override void Daily(Aspiration a)
        {
            float v = Value(a.pawn);
            if (v < 0f) return;
            a.baseline = v >= def.level ? a.baseline + 1f : 0f;
        }

        public override float Progress(Aspiration a) => a.baseline / Mathf.Max(1f, a.target);

        public override bool StillPossible(Aspiration a) => Heart.On;

        public override string ProgressText(Aspiration a) => "CC_Asp_StreakProgress".Translate(a.baseline.ToString("0"), a.target.ToString("0"));
    }
}
