using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Back to it (Rimfolk_Arc_BackToIt): one of the dangers on the map hurt someone in the last three days (the harm
    /// service's wound, MapDangers.Wound, kept by SharedTimesTogether), and they are shaken. A friend talks it over with
    /// them, they go back to the work it happened at (the dig's mining, the plants of the fields and trees, the scrap's
    /// crafting, or what they do best when it was only the ground giving way), and in time it's just work again.
    /// </summary>
    public class ArcWorker_BackToIt : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            string kind = SharedTimesTogether.RecentCloseCall(p, TogetherFourMath.BackToItDays);
            if (kind == null || p.Downed || p.InBed()) return null;
            string named = TogetherFourMath.CloseCallSkill(kind);
            var skill = named != null ? DefDatabase<SkillDef>.GetNamedSilentFail(named) : null;
            var rec = ArcUtil.Skill(p, skill);
            // Work they hardly do (below 4) wouldn't bring them back to it: what they do best does.
            if (skill == null || rec == null || rec.TotallyDisabled || rec.Level < 4)
                skill = ArcUtil.Best(p, DefDatabase<SkillDef>.AllDefsListForReading.Where(s => s.usuallyDefinedInBackstories), 0);
            if (skill == null) return null;
            var friend = ArcUtil.Closest(p, home.Where(o => o.IsColonist && !o.Downed && !o.DevelopmentalStage.Juvenile()), 10f);
            if (friend == null) return null;
            return new ArcCandidate { other = friend, skill = skill, key = kind, weight = 2f };
        }
    }
}
