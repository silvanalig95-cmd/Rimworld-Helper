using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The target colonist leaves the colony for good of their own will, with its blessing (called home to their family):
    /// as when someone quits, their open wishes and the council's marks on them go first, and the colony's records count
    /// it as leaving, never a banishment, so nobody grieves a banished friend and they may come back one day as someone
    /// who walked out. Only someone up and about at home; never the last grown-up of the colony.
    /// </summary>
    public class DilemmaEffect_LeaveForGood : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            foreach (var p in Targets(ctx).ToList())
            {
                if (p == null || p.Dead || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || !p.Spawned || p.Downed) continue;
                var map = p.Map;
                if (map == null || !map.mapPawns.FreeColonistsSpawned.Any(o => o != p && !o.DevelopmentalStage.Juvenile())) continue;
                var t = comp?.happenings;
                try
                {
                    if (comp != null)
                        foreach (var w in comp.wishes.Where(w => w.state == WishState.Open && w.Involves(p)).ToList())
                            comp.Resolve(w, WishState.Cancelled);
                    TrustEffects.StripAll(p);
                    if (p.Drafted) p.drafter.Drafted = false;
                    if (t != null) t.quitting = p;
                    PawnBanishUtility.Banish(p, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("leave for good", e);
                }
                finally
                {
                    if (t != null) t.quitting = null;
                }
            }
        }
    }
}
