using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Mourners carrying their dead home (CampKindDef.bearsDead): one of them carries the body in from the edge and lays
    // it by their lamp, where one of them keeps the vigil; when they go in their own time the body goes with them in
    // someone's arms (CarryDeadHome). See Visitors/Borne.cs for the body itself.
    public partial class Camp
    {
        /// <summary>The dead they carry home, a real body, while it's with them.</summary>
        public Thing dead;

        /// <summary>Where their dead lies while they stay: beside their lamp (else a little way from the fire), never on the spot of anything they build.</summary>
        public IntVec3 DeadCell()
        {
            if (map == null) return IntVec3.Invalid;
            var at = plans.FirstOrDefault(p => p.role == "light" && !p.dropped)?.cell ?? FireCell;
            if (!at.IsValid) at = center;
            foreach (var c in GenRadial.RadialCellsAround(at, 3.5f, false).OrderBy(c => c.DistanceToSquared(at)))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c == FireCell || c.GetEdifice(map) != null || c.GetFirstItem(map) != null) continue;
                if (plans.Any(p => !p.dropped && p.def != null && p.Rect.ExpandedBy(p.role == "fire" ? 1 : 0).Contains(c))) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>As they set out from the edge: their dead, carried in by one of them (not their leader) to lie by the lamp.</summary>
        public void BearDead()
        {
            if (kind == null || !kind.bearsDead || dead != null || map == null || faction == null) return;
            try
            {
                var bearer = members.FirstOrDefault(p => p != leader && Borne.CanBear(p)) ?? (Borne.CanBear(leader) ? leader : null);
                if (bearer == null) return;
                var corpse = Borne.MakeDead(faction, Arrivals.PersonKind(faction) ?? bearer.kindDef, null, map);
                if (corpse == null || !Borne.SetDownBy(bearer, corpse)) return;
                dead = corpse;
                // Laid by the lamp (or, with no free place there, near the middle of the camp): never carried about all stay.
                var cell = DeadCell();
                Borne.Bear(bearer, corpse, cell.IsValid ? cell : CellFinder.RandomClosewalkCellNear(center, map, 4));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp's dead carried in", e);
            }
        }

        /// <summary>Every hour while they stay: their dead put down away from the lamp (the bearer called away on the walk in) is carried there.</summary>
        private void WatchDead()
        {
            if (dead == null) return;
            if (dead.Destroyed || dead.MapHeld != map)
            {
                dead = null;
                return;
            }
            Borne.Keep(dead);
            if (!dead.Spawned || Here.Any(p => p.CurJob != null && p.CurJob.targetA.Thing == dead)) return;
            var cell = DeadCell();
            var at = plans.FirstOrDefault(p => p.role == "light" && !p.dropped)?.cell ?? FireCell;
            if (!cell.IsValid || dead.Position.InHorDistOf(at, 4f)) return;
            var bearer = Here.Where(p => !Carrying(p) && p.carryTracker?.CarriedThing == null && Borne.CanBear(p) && p.CanReserveAndReach(dead, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(dead.Position)).FirstOrDefault();
            if (bearer != null) Borne.Bear(bearer, dead, cell);
        }

        private void ExposeDead()
        {
            if (Scribe.mode == LoadSaveMode.Saving && dead != null && dead.Destroyed) dead = null;
            Scribe_References.Look(ref dead, "dead");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && dead != null) Borne.Keep(dead);
        }
    }
}
