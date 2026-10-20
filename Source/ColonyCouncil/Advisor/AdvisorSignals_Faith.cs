using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // ---------------- Faith in the colony ----------------
    //
    // A colony split between many faiths (Faith/, with Ideology): no single faith's rites, roles and buildings serve
    // everyone, and each faith's demands weigh on its own. The advice says who holds what, who is wavering and which
    // faiths have no moral guide here, and leads to the faiths page and the game's own tools. Once an hour with the rest
    // of the advisor; it reads only the faith census, which is kept an hour anyway.

    /// <summary>Half the colony or more split between three faiths or more.</summary>
    public class AdvisorWorker_FaithSplit : AdvisorSignalWorker
    {
        /// <summary>Split this far (0: one faith, 1: everyone their own) with at least this many faiths.</summary>
        public const float SplitFrom = 0.5f;
        public const int FaithsFrom = 3;

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Faiths.On || map != HappeningTracker.HomeMap()) yield break;
            var census = Faiths.Census();
            if (census.Count < FaithsFrom) yield break;
            float split = Faiths.Split();
            if (split < SplitFrom) yield break;
            var people = census.SelectMany(f => f.believers).Where(p => p != null && !p.Dead).ToList();
            var wavering = people.Where(Faiths.Wavering).ToList();
            var noGuide = census.Where(f => !HasGuideHere(f)).Select(f => f.ideo.name).ToList();
            string faiths = TextUtil.JoinAnd(census.Select(f => "CC_Adv_FaithSplit_Faith".Translate(f.ideo.name.Named("FAITH"), f.believers.Count.Named("N")).Resolve()).ToList());
            string detail = "CC_Adv_FaithSplit_Detail".Translate(faiths.Named("FAITHS")).Resolve();
            if (wavering.Count > 0) detail += " " + "CC_Adv_FaithSplit_Wavering".Translate(TextUtil.JoinAnd(wavering.Select(p => p.LabelShort).ToList()).Named("LIST")).Resolve();
            if (noGuide.Count > 0) detail += " " + "CC_Adv_FaithSplit_NoGuide".Translate(TextUtil.JoinAnd(noGuide).Named("LIST")).Resolve();
            detail += " " + "CC_Adv_FaithSplit_Tools".Translate();
            var s = Signal(0.15f + 0.15f * split, "CC_Adv_FaithSplit_Title".Translate(census.Count.Named("N")).Resolve(), detail, 0.3f);
            s.page = "Faiths";
            s.look = new LookTargets(people.Where(p => p.Spawned && p.Map == map).Take(6));
            s.key = "split";
            yield return s;
        }

        private static bool HasGuideHere(Faiths.Faith f)
        {
            foreach (var r in f.ideo.RolesListForReading)
                if (r?.def == PreceptDefOf.IdeoRole_Moralist)
                {
                    var who = r.ChosenPawnSingle();
                    return who != null && f.believers.Contains(who);
                }
            return false;
        }
    }
}
