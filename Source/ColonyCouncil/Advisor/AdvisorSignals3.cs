using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Everyone has grown hard to please: the council's rewards barely register any more.</summary>
    public class AdvisorWorker_Spoiled : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!ColonyCouncilMod.Settings.risingStandards || !ColonyCouncilMod.Settings.enableWishes) yield break;
            var colonists = Colonists(map);
            if (colonists.Count < 2) yield break;
            float avg = colonists.Average(p => Standards.Of(p));
            if (avg < 0.6f) yield break;
            var spoiled = colonists.Where(p => Standards.LevelOf(p) == StandardsLevel.Spoiled).ToList();
            var s = Signal(avg >= 0.8f ? 0.5f : 0.3f, "CC_Adv_Spoiled_Title".Translate(),
                "CC_Adv_Spoiled_Detail".Translate(spoiled.Count, colonists.Count), 0.5f);
            s.look = new LookTargets(spoiled.Cast<Thing>());
            yield return s;
        }
    }

    /// <summary>Colonists who've given up on the council, or are about to walk out.</summary>
    public class AdvisorWorker_Bitter : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) yield break;
            var colonists = Colonists(map);
            var leaving = colonists.Where(p => comp.standings.For(p, false)?.leavingTick > 0).ToList();
            if (leaving.Count > 0)
            {
                var st = comp.standings.For(leaving[0], false);
                var s = Signal(1f, "CC_Adv_Leaving_Title".Translate(leaving[0].Named("PAWN")),
                    "CC_Adv_Leaving_Detail".Translate(leaving[0].Named("PAWN"), TextUtil.DaysString(st.leavingTick - Find.TickManager.TicksGame).Named("TIME")), 1f);
                s.look = new LookTargets(leaving[0]);
                s.key = "leaving";
                yield return s;
                yield break;
            }
            var bitter = colonists.Where(p => comp.standings.LevelOf(p) == TrustLevel.Bitter).ToList();
            if (bitter.Count == 0) yield break;
            var b = Signal(0.4f + 0.15f * bitter.Count, "CC_Adv_Bitter_Title".Translate(bitter.Count),
                "CC_Adv_Bitter_Detail".Translate(string.Join(", ", bitter.Select(p => p.LabelShort))), 0.6f);
            b.look = new LookTargets(bitter.Cast<Thing>());
            b.key = "bitter";
            yield return b;
        }
    }
}
