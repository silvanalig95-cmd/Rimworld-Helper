using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The run (Rimfolk_WS_Run, Odyssey): in spring or autumn the fish come up the water in numbers. While it lasts the
    /// water's own fish stock on the colony's map is kept full (the game's fish stock, every body of water that holds
    /// fish), so the colony's fishers and any fishing camp bring in the most the water gives; once the run thins (its
    /// "thinning" flag) the water is left to the game again. A fishing family may camp by the water for it
    /// (Rimfolk_Camp_Fishers, a beat), the first big catch is the colony's own fishers' (only with a fishing spot in use),
    /// and a quarrel over the best water needs the fishers' camp and the colony's fishing spot both.
    /// </summary>
    public class SituationWorker_Run : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) =>
            CampLand.Odyssey && map?.waterBodyTracker != null && WishWorker_Fishing.CanFish(map) && CampLand.Bodies(map).Any();

        public override void Begin(WorldSituation s) => CampLand.FillWaters(s.ctx.map);

        public override string Daily(WorldSituation s)
        {
            if (!s.Has("thinning")) CampLand.FillWaters(s.ctx.map);
            return null;
        }

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            switch (beat.key)
            {
                case "catch":
                    // The colony's own fishers at the water, or nothing to tell.
                    return WishWorker_Fishing.InUse(map);
                case "quarrel":
                {
                    var c = CampTracker.Instance?.camp;
                    if (c == null || c.kind?.work != "fish" || c.map != map || !c.Settled || !c.CanTalk || c.farWater) return false;
                    return WishWorker_Fishing.HasFishingZone(map);
                }
            }
            return true;
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var map = s.ctx.map;
            var c = map == null ? null : CampLand.FishTargets(map).Cast<IntVec3?>().FirstOrDefault();
            return c.HasValue ? new LookTargets(new TargetInfo(c.Value, map)) : base.StartLook(s);
        }
    }
}
