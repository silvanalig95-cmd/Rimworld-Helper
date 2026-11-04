using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// What release 7 adds to the raiders' camps (docs/design/stage1-region.md, items 4 and 7): the camp's leader (the
    /// warlord's son) kept in the world while it stands and behind its walls when the colony attacks it; the colony's
    /// people who fought on its map, remembered by name when it is broken; and two more ways a hidden camp is found: a
    /// visitor at the colony of a people at peace with it, who came past its fires on the road, or a letter from a
    /// neighbouring town near it (CampWarnMath).
    /// </summary>
    public static partial class RaidersCamps
    {
        // ---------------- Its leader ----------------

        /// <summary>Someone leads a camp standing now (the ledger's daily check of its reason to keep them).</summary>
        public static bool Leads(Pawn p)
        {
            if (p == null || RegionBook.Instance == null) return false;
            foreach (var home in Homes.All)
                if (CampOf(home)?.leader == p)
                    return true;
            return false;
        }

        /// <summary>
        /// The camp's leader can no longer lead it: dead, lost from the save, a prisoner or a slave (the colony's or
        /// anyone's), or no longer one of its people (taken into the colony). The camp then breaks up
        /// (RaidersCampMath.BreaksUp), and his strength is no longer part of its raids.
        /// </summary>
        public static bool LeaderLost(RaidersCamp c)
        {
            var p = c?.leader;
            return p != null && RaidersCampMath.LeaderLost(true, p.Dead || p.Discarded, p.IsPrisoner || p.IsSlave, p.Faction == c.people);
        }

        /// <summary>
        /// The leader kept in the world while the camp stands: after a raid he led, the game took him back into the world
        /// with nothing keeping him there.
        /// </summary>
        private static void KeepLeader(RaidersCamp c)
        {
            var p = c?.leader;
            if (p == null || p.Dead || p.Discarded || p.Spawned || Find.WorldPawns == null || !Find.WorldPawns.Contains(p)) return;
            WorldLedger.Keep(p, KeepReason);
        }

        /// <summary>The colony came to attack the camp: its leader, free in the world, is on its map behind its walls with the rest of them.</summary>
        private static void LeaderOnMap(RaidersCamp c, Map map)
        {
            var p = c?.leader;
            if (p == null || map == null || p.Faction != c.people || !WorldSituationWorker.FreeInWorld(p) || p.health?.Downed == true) return;
            try
            {
                var theirs = map.mapPawns.SpawnedPawnsInFaction(c.people).Where(x => x != null && !x.Dead && !x.Downed && x.RaceProps.Humanlike).ToList();
                var anchor = theirs.FirstOrDefault(x => x.GetLord() != null) ?? theirs.FirstOrDefault();
                if (anchor == null) return;
                if (!CellFinder.TryFindRandomCellNear(anchor.Position, map, 4, x => x.Standable(map) && x.GetFirstPawn(map) == null, out var cell)) return;
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                Find.WorldPawns.RemovePawn(p);
                try
                {
                    GenSpawn.Spawn(p, cell, map);
                }
                catch
                {
                    // Neither on the map nor in the world he'd be lost from the save: back to the world with him.
                    if (!p.Spawned && !p.Destroyed && !Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                    throw;
                }
                var lord = anchor.GetLord();
                if (lord != null && lord.CanAddPawn(p)) lord.AddPawn(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its leader on its map", e);
            }
        }

        /// <summary>The colony's people on the camp's map, noted as those who fought there.</summary>
        private static void NoteFought(RaidersCamp c, Map map)
        {
            c.fought ??= new System.Collections.Generic.List<Pawn>();
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (c.fought.Count >= ColonistMemoryMath.MaxFought) return;
                if (p != null && !p.DevelopmentalStage.Juvenile() && !c.fought.Contains(p)) c.fought.Add(p);
            }
        }

        // ---------------- Found another way ----------------

        /// <summary>
        /// The day's other ways a hidden camp is found, once it has stood a while: a visitor of a people at peace with the
        /// colony here now, who came past its fires on the road, warns of it; else a neighbouring town near it writes.
        /// </summary>
        private static void TryWarn(Map home, RaidersCamp c, int now)
        {
            if (home == null || c?.people == null || !CampWarnMath.May((now - c.setAt) / (float)Day)) return;
            var visitor = Visitor(home, c.people);
            if (visitor != null && Rand.Chance(CampWarnMath.Visitor(Rate)))
            {
                if (Find.WorldObjects.AnyMapParentAt(TileOf(c))) return;
                Found(home, c, visitor, visitor.Faction.Name, now, "Visitor");
                return;
            }
            if (!(ColonyCouncilMod.Settings?.neighbourLetters ?? true)) return;
            var town = NeighbourNear(c);
            if (town == null || !Rand.Chance(CampWarnMath.Letter(Rate)) || Find.WorldObjects.AnyMapParentAt(TileOf(c))) return;
            Found(home, c, null, town.Faction.Name, now, "Letter", town.Name);
        }

        /// <summary>A visitor or trader of a people at peace with the colony, on the home's map now and up and about, not of the camp's people.</summary>
        private static Pawn Visitor(Map home, Faction camp)
        {
            foreach (var p in home.mapPawns.AllPawnsSpawned)
            {
                var f = p?.Faction;
                if (f == null || f.IsPlayer || f == camp || p.RaceProps == null || !p.RaceProps.Humanlike || p.Downed || p.IsPrisoner || p.InMentalState) continue;
                if (f.HostileTo(Faction.OfPlayer) || !FactionMemory.Remembers(f) || p.GetLord() == null || p.DevelopmentalStage.Juvenile()) continue;
                return p;
            }
            return null;
        }

        /// <summary>A town of a people at peace with the colony within CampWarnMath.LetterTiles of the camp (the nearest), or null.</summary>
        private static Settlement NeighbourNear(RaidersCamp c)
        {
            var t = TileOf(c);
            if (!t.Valid) return null;
            Settlement best = null;
            float bestTiles = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                var f = s?.Faction;
                if (f == null || f.IsPlayer || f == c.people || s.Destroyed || !s.HasName || s.Tile.Layer != t.Layer || f.HostileTo(Faction.OfPlayer) || !FactionMemory.Remembers(f)) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t);
                if (d > CampWarnMath.LetterTiles || d >= bestTiles) continue;
                best = s;
                bestTiles = d;
            }
            return best;
        }
    }
}
