using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Alpha Skills' forbidden passion on a skill the colonist can never use (the player, 28 November 2026, of Cait, whose
    /// background rules out animal work and who had the forbidden passion in Animals: "just as a permanent debuff now ...
    /// do we need to change the code?", and "yes, please fix that"). Alpha Skills (sarg.alphaskills) gives anyone with a
    /// forbidden passion in any skill a mood of -5 for as long as they have one (its hidden hediff AS_ForbiddenPassion and
    /// ThoughtWorker_Hediff), in return for learning that skill much faster; on a skill whose main work they can never do
    /// (the base game's work for it, <see cref="MainWork"/>), or that the game marks wholly disabled, they only ever get
    /// the mood. Once an hour Rimfolk looks at the colony's own people: such a passion becomes no
    /// passion (<see cref="ForbiddenPassionsMath.Clears"/>), the mod's hediff goes once no forbidden passion is left, and a
    /// message says so. A forbidden passion on a skill they can use stays as the mod has it. Found softly, by the
    /// passion's def name in Vanilla Skills Expanded's passions: without the mod nothing here runs.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ForbiddenPassions
    {
        public const string PassionName = "AS_ForbiddenPassion", HediffName = "AS_ForbiddenPassion";

        /// <summary>The forbidden passion's value in a skill's passion (its def's index), or -1 without Alpha Skills.</summary>
        private static readonly int forbidden = -1;
        private static readonly HediffDef hediff;

        /// <summary>Alpha Skills' forbidden passion is loaded.</summary>
        public static bool Active => forbidden >= 0;

        static ForbiddenPassions()
        {
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef");
                if (type == null) return;
                var def = GenDefDatabase.GetDefSilentFail(type, PassionName, false);
                if (def == null) return;
                forbidden = def.index;
                hediff = DefDatabase<HediffDef>.GetNamedSilentFail(HediffName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Alpha Skills' forbidden passion", e);
                forbidden = -1;
            }
        }

        /// <summary>The council's hour: the colony's own people looked at (a skill each, a few dozen people at most).</summary>
        public static void Hourly()
        {
            if (!Active) return;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.ToList())
                Check(p);
        }

        /// <summary>
        /// A skill's main work: the base game's work types whose first skill it is (handling for Animals, doctoring for
        /// Medicine), else any work type whose first skill it is (a mod's own skill), else any that use it. Another mod's or
        /// a DLC's work that also uses the skill (fishing uses Animals) doesn't count: someone barred from animal work has the
        /// forbidden passion in Animals for nothing, which is what the player saw.
        /// </summary>
        private static List<WorkTypeDef> MainWork(SkillDef skill)
        {
            var all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            bool First(WorkTypeDef w) => w.relevantSkills != null && w.relevantSkills.Count > 0 && w.relevantSkills[0] == skill;
            var core = all.Where(w => First(w) && w.modContentPack != null && w.modContentPack.IsCoreMod).ToList();
            if (core.Count > 0) return core;
            var main = all.Where(First).ToList();
            return main.Count > 0 ? main : all.Where(w => w.relevantSkills != null && w.relevantSkills.Contains(skill)).ToList();
        }

        /// <summary>One person: each forbidden passion on a skill they can never work at cleared, the mood's hediff with the last of them.</summary>
        public static void Check(Pawn p)
        {
            var skills = p?.skills?.skills;
            if (!Active || skills == null || p.RaceProps == null || !p.RaceProps.Humanlike || p.Dead) return;
            var cleared = new List<SkillDef>();
            foreach (var s in skills)
            {
                if (s?.def == null || (int)s.passion != forbidden) continue;
                var main = MainWork(s.def);
                if (!s.TotallyDisabled && !ForbiddenPassionsMath.Clears(true, main.Count, main.Count(w => p.WorkTypeIsDisabled(w)))) continue;
                s.passion = Passion.None;
                cleared.Add(s.def);
            }
            if (cleared.Count == 0) return;
            if (hediff != null && !skills.Any(s => s != null && (int)s.passion == forbidden))
            {
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(hediff);
                if (h != null) p.health.RemoveHediff(h);
            }
            foreach (var skill in cleared)
                Tell.Minor(NewsKind.People, Importance.Notable, p.MapHeld, "CC_ForbiddenPassion_Cleared".Translate(p.Named("PAWN"), skill.label.Named("SKILL")).Resolve(), new LookTargets(p));
        }
    }
}
