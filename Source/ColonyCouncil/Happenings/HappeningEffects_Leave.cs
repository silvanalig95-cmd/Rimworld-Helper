using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The target colonist leaves the colony for good of their own will, with its blessing (called home to their family):
    /// as when someone quits, their open wishes and the council's marks on them go first, and the colony's records count
    /// it as leaving, never a banishment, so nobody grieves a banished friend and they may come back one day as someone
    /// who walked out. Only someone up and about at home; never the last grown-up of the colony. With
    /// <see cref="joinFaction"/>, they go to the answer's people (family or a partner there: Social/MovingOn.cs), become
    /// one of them, and walk off the map to them.
    /// </summary>
    public class DilemmaEffect_LeaveForGood : DilemmaEffect
    {
        public bool joinFaction;

        /// <summary>Those leaving to a people of their own, by thing id, until they go (kept for the session).</summary>
        private static readonly Dictionary<int, Faction> joining = new Dictionary<int, Faction>();

        /// <summary>Someone of that people who came for them and sets off with them (Integrations/GuestWill.cs), by thing id.</summary>
        private static readonly Dictionary<int, Pawn> with = new Dictionary<int, Pawn>();

        /// <summary>When <paramref name="p"/> goes, <paramref name="companion"/> (a visitor of the people they go to) walks off with them.</summary>
        public static void GoesWith(Pawn p, Pawn companion)
        {
            if (p != null && companion != null) with[p.thingIDNumber] = companion;
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
            {
                if (p == null || p.Dead || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || !p.Spawned || p.Downed) continue;
                var map = p.Map;
                if (map == null || !map.mapPawns.FreeColonistsSpawned.Any(o => o != p && !o.DevelopmentalStage.Juvenile())) continue;
                if (joinFaction && MovingOn.Friendly(ctx.faction)) joining[p.thingIDNumber] = ctx.faction;
                // Seen off to the gate by the walk the answer set going: they go from there, at its end (PlayOut/PlayDepart.cs).
                if (PlayDepart.HoldLeaving(p)) continue;
                Leave(p);
            }
        }

        /// <summary>They go now, from wherever they are (still only one of ours, up and about, and never the last grown-up).</summary>
        public static void Leave(Pawn p)
        {
            if (p == null || p.Dead || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || !p.Spawned || p.Downed) return;
            var map = p.Map;
            if (map == null || !map.mapPawns.FreeColonistsSpawned.Any(o => o != p && !o.DevelopmentalStage.Juvenile())) return;
            var comp = CouncilGameComponent.Instance;
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
                // Going to a people of their own: one of them now, and on their way to them.
                if (joining.TryGetValue(p.thingIDNumber, out var to))
                {
                    joining.Remove(p.thingIDNumber);
                    if (MovingOn.Friendly(to) && !p.Dead)
                    {
                        if (p.Faction != to) p.SetFaction(to);
                        if (p.Spawned && p.GetLord() == null)
                            LordMaker.MakeNewLord(to, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), p.Map, new List<Pawn> { p });
                    }
                }
                // Whoever came for them goes with them, in the same party off the map.
                if (with.TryGetValue(p.thingIDNumber, out var c))
                {
                    with.Remove(p.thingIDNumber);
                    var lord = p.GetLord();
                    if (c != null && !c.Dead && c.Spawned && c.Map == map && !c.Downed && c.Faction != Faction.OfPlayer)
                    {
                        c.GetLord()?.RemovePawn(c);
                        if (lord != null && lord.faction == c.Faction) lord.AddPawn(c);
                        else Arrivals.SendAway(new[] { c });
                    }
                }
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
