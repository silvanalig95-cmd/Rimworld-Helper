using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A council ambition's goal from the defense survey: hold the way in most raids take, with a gate across it and
    /// cover behind; on open ground with no narrow place, wall the home in. Possible only while that isn't done and the
    /// colony can build walls and cover. The survey is the Defenses page's own (worked out at most once a game hour).
    /// </summary>
    public class Goal_HoldTheWayIn : AmbitionGoal
    {
        public int cover = 3;

        private static DefenseSurvey Survey(Map map) => map != null && map.IsPlayerHome ? DefenseSurvey.For(map) : null;

        public override bool Possible(Map map)
        {
            var s = Survey(map);
            if (s == null || !s.Reachable) return false;
            if (!Avail.CanBuild(RimWorld.ThingDefOf.Wall, map) || !Avail.AnyBuildable(map, Avail.IsCover)) return false;
            return Progress(map, 0f) < 0.999f;
        }

        public override float Progress(Map map, float baseline)
        {
            var s = Survey(map);
            if (s == null || !s.Reachable) return 0f;
            var main = s.Main;
            if (main == null) return s.result.enclosed ? 1f : 0f;
            return (main.gated ? 0.6f : 0f) + 0.4f * Mathf.Clamp01(main.coverInside / (float)Mathf.Max(1, cover));
        }

        public override string Describe(Map map, float baseline)
        {
            var s = Survey(map);
            if (s == null || !s.Reachable) return "CC_Goal_HoldWayIn_None".Translate();
            var main = s.Main;
            if (main == null)
                return "CC_Goal_WallIn".Translate((s.result.enclosed ? "CC_Goal_HoldWayIn_Done" : "CC_Goal_HoldWayIn_NotYet").Translate()).Resolve();
            return "CC_Goal_HoldWayIn".Translate(DefenseAdvice.Where(s, main.cell), (main.gated ? "CC_Goal_HoldWayIn_Done" : "CC_Goal_HoldWayIn_NotYet").Translate(),
                Mathf.Min(main.coverInside, cover), cover).Resolve();
        }
    }
}
