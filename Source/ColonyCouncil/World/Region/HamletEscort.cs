using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The settlers' road (docs/design/stage1-region.md, item 6, "The escort"; decision 10): taken under the colony's
    // protection they camp at the gate with their carts (the camps' machinery, the settlers' kind) up to five days; a
    // caravan of the colony's with two grown colonists or more that sets out for the place takes them along, beside it and
    // not in its pawn list, and its panel says so (HamletPatches.cs). At the place, after a night with the caravan there,
    // the first fires are lit and the hamlet stands (HamletFound.cs); on arriving, one roll of the game's own caravan
    // demand or ambush, the settlers among the defenders. Let go alone, or left waiting, a third of them are turned back.
    public static partial class Hamlets
    {
        public const string SettlersCamp = "Rimfolk_Camp_Settlers";

        // ---------------- The colony's answer ----------------

        /// <summary>Taken under the colony's protection: they camp at the gate (or wait in their holding) for the colony's caravan.</summary>
        internal static void Escort(Map home, HamletHome h)
        {
            if (home == null || h == null || h.stage != HamletStage.Asked) return;
            int now = Now;
            h.stage = HamletStage.Waiting;
            h.waitSince = now;
            Camp c = null;
            if (h.way == HamletWay.Holding) c = CampOf(h);
            else
            {
                c = MakeCamp(home, h);
                h.camp = c?.id ?? -1;
            }
            HamletWords.TellWaiting(home, h, c);
        }

        /// <summary>The settlers' camp at the gate, with the settlers themselves (and their carts and beasts); null when no camp can come now (another camp is here, no room).</summary>
        private static Camp MakeCamp(Map home, HamletHome h)
        {
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(SettlersCamp);
            var tracker = CampTracker.Instance;
            if (kind == null || tracker == null || !CampTracker.Enabled || CampTracker.VisitingAt(home) != null) return null;
            try
            {
                var given = h.Living.Where(p => !p.Spawned || p.Map == home).ToList();
                if (given.Count == 0) return null;
                var usual = CampWeather.StayRange(home, GenDate.TicksPerDay * HamletMath.WaitDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(home, out var center, hot)) return null;
                var setup = tracker.Make(home, center, CampSite.ColonyCenter(home), kind, h.people, hot, null, null, given);
                if (setup == null) return null;
                setup.stayTicks = GenDate.TicksPerDay * (HamletMath.WaitDays + 1);
                setup.land = TravelLand.Read(home);
                var c = tracker.Start(setup);
                if (c == null) CampTracker.Discard(setup);
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the settlers' camp", e);
                return null;
            }
        }

        // ---------------- Waiting at the gate ----------------

        /// <summary>The hour while they wait: a caravan bound for the place takes them along; five days with none, or their camp gone, and they go alone.</summary>
        private static void Wait(Map home, HamletHome h, int now)
        {
            if (h.people == null || h.people.HostileTo(Faction.OfPlayer))
            {
                End(home, h, HamletEnd.Gone, null);
                return;
            }
            if (h.Living.Count == 0)
            {
                End(home, h, HamletEnd.Lost, null);
                return;
            }
            var camp = CampOf(h);
            var caravan = EscortCaravan(home, h);
            if (caravan != null)
            {
                Pickup(home, h, caravan, camp);
                return;
            }
            bool campGone = h.camp >= 0 && (camp == null || !camp.CanTalk);
            if (HamletMath.WaitOver(h.waitSince, now) || campGone) GoAlone(home, h, true);
        }

        /// <summary>A caravan of the colony's just out of the home (within three tiles), with two grown colonists or more, bound for the place; null for none.</summary>
        internal static Caravan EscortCaravan(Map home, HamletHome h)
        {
            var tile = TileOf(h);
            if (home == null || !tile.Valid) return null;
            var grid = Find.WorldGrid;
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || c.Tile.Layer != home.Tile.Layer) continue;
                bool bound = c.pather != null && c.pather.Moving && c.pather.Destination == tile;
                int adults = c.PawnsListForReading.Count(p => p != null && p.IsColonist && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.Dead);
                if (HamletMath.EscortFits(adults, bound, grid.ApproxDistanceInTiles(home.Tile, c.Tile))) return c;
            }
            return null;
        }

        /// <summary>
        /// The caravan takes them along: their camp packed onto their carts, they go out with it (off the map and kept in the
        /// world beside the caravan), its grown colonists noted as those who walked them out.
        /// </summary>
        private static void Pickup(Map home, HamletHome h, Caravan caravan, Camp camp)
        {
            var colonists = caravan.PawnsListForReading.Where(p => p != null && p.IsColonist && !p.DevelopmentalStage.Juvenile() && !p.Dead).ToList();
            h.escorts = colonists.Take(HamletMath.MaxSettlers).ToList();
            h.escort = colonists.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();
            OffTheMap(home, h, camp);
            h.stage = HamletStage.Escorted;
            HamletWords.TellSetOut(home, h, caravan);
        }

        /// <summary>
        /// The settlers leave the map at once (with the caravan, or alone with nothing more to see): their camp packed and its
        /// books closed without a word of its own, their beasts with them, and they are kept in the world for the road.
        /// </summary>
        private static void OffTheMap(Map home, HamletHome h, Camp camp)
        {
            if (camp != null && camp.phase != CampPhase.Over)
            {
                try
                {
                    camp.told = true;
                    if (camp.CanTalk) camp.Leave("hamlet");
                    foreach (var a in camp.herd.Where(a => a != null && a.Spawned).ToList())
                    {
                        a.GetLord()?.RemovePawn(a);
                        a.DeSpawn();
                        Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.Discard);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: the camp packed", e);
                }
            }
            foreach (var p in h.Living)
            {
                try
                {
                    if (p.Spawned)
                    {
                        p.GetLord()?.RemovePawn(p);
                        p.DeSpawn();
                    }
                    Arrivals.Hold(p);
                    KeepSettler(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: a settler off the map", e);
                }
            }
            if (camp != null && camp.phase != CampPhase.Over)
            {
                try
                {
                    var lord = camp.lord;
                    camp.Finish();
                    if (lord != null && camp.map != null && camp.map.lordManager.lords.Contains(lord) && lord.ownedPawns.Count == 0) camp.map.lordManager.RemoveLord(lord);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: the camp's books closed", e);
                }
            }
        }

        // ---------------- On the road with the caravan ----------------

        /// <summary>The escort's caravan now: the one its noted colonist is in, else another of those who set out; null when none is on the road.</summary>
        private static Caravan EscortNow(HamletHome h)
        {
            var c = h.escort?.GetCaravan();
            if (c != null) return c;
            foreach (var p in h.escorts)
            {
                var other = p != null && !p.Dead ? p.GetCaravan() : null;
                if (other == null) continue;
                h.escort = p;
                return other;
            }
            return null;
        }

        /// <summary>The escort is on a map at the place (a fight there, or the caravan entered it).</summary>
        private static bool EscortOnMapAt(HamletHome h, PlanetTile tile)
        {
            foreach (var p in h.escorts.Concat(new[] { h.escort }))
                if (p != null && !p.Dead && p.Spawned && p.MapHeld != null && p.MapHeld.Tile == tile)
                    return true;
            return false;
        }

        /// <summary>
        /// The hour on the road: arrived at the place (the road's one roll), the first night there and the founding once no
        /// map stands on the tile; the caravan gone home or elsewhere before it got there, and they go on alone.
        /// </summary>
        private static void Road(Map home, HamletHome h, int now)
        {
            var tile = TileOf(h);
            if (h.Living.Count == 0)
            {
                End(home, h, HamletEnd.Lost, null);
                return;
            }
            if (h.people == null || h.people.HostileTo(Faction.OfPlayer))
            {
                End(home, h, HamletEnd.Gone, null);
                return;
            }
            var c = EscortNow(h);
            if (h.arrivedAt < 0)
            {
                if (c != null && c.Tile == tile) Arrive(home, h, c, now);
                else if (EscortOnMapAt(h, tile)) h.arrivedAt = now;
                else if (c == null || c.pather != null && c.pather.Moving && c.pather.Destination != tile)
                {
                    // The caravan went home, or turned off for somewhere else: the settlers go on alone from here.
                    GoAlone(home, h, false);
                    return;
                }
            }
            if (h.arrivedAt < 0 || !HamletMath.FirstFires(h.arrivedAt, now, GenLocalDate.HourOfDay(tile))) return;
            // A fight's map still on the tile: the fires wait until it is gone.
            if (Current.Game.FindMap(tile) != null || Find.WorldObjects.ObjectsAt(tile).Any(o => !(o is Caravan))) return;
            Found(home, h, true);
        }

        /// <summary>They reached the place with the caravan: the one roll of the road's dangers at the founding.</summary>
        private static void Arrive(Map home, HamletHome h, Caravan c, int now)
        {
            h.arrivedAt = now;
            if (h.rolled) return;
            h.rolled = true;
            try
            {
                bool danger = HamletMath.Danger(RaidersCamps.CampOf(home) != null, NearestHostileTown(TileOf(h)));
                if (Rand.Chance(HamletMath.Risk(danger))) Strike(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the road's dangers", e);
            }
        }

        /// <summary>The nearest town of a people hostile to the colony to a tile (tiles), or -1 for none on its layer.</summary>
        private static float NearestHostileTown(PlanetTile t)
        {
            float best = -1f;
            if (!t.Valid) return best;
            var player = Faction.OfPlayer;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || s.Faction == null || s.Faction.IsPlayer || s.Tile.Layer != t.Layer || !s.Faction.HostileTo(player)) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        /// <summary>The game's own caravan demand or ambush on the caravan at the place, whichever can come (its own letter tells it).</summary>
        private static void Strike(Caravan c)
        {
            var defs = new List<IncidentDef>();
            foreach (var n in Rand.Bool ? new[] { "CaravanDemand", "Ambush" } : new[] { "Ambush", "CaravanDemand" })
            {
                var d = DefDatabase<IncidentDef>.GetNamedSilentFail(n);
                if (d?.Worker != null) defs.Add(d);
            }
            foreach (var d in defs)
            {
                var parms = StorytellerUtility.DefaultParmsNow(d.category, c);
                if (!d.Worker.CanFireNow(parms)) continue;
                if (d.Worker.TryExecute(parms)) return;
            }
        }

        /// <summary>
        /// A fight's map made for the caravan the settlers go with (HamletPatches.cs): its grown settlers stand among the
        /// defenders, when the attackers are their enemies too; the children stay hidden by the carts. Told once.
        /// </summary>
        internal static void Fight(HamletHome h, Map home, Map map, List<Pawn> enemies)
        {
            if (h == null || map == null || h.people == null) return;
            var foe = enemies?.FirstOrDefault(p => p?.Faction != null)?.Faction;
            if (foe == null || !foe.HostileTo(h.people)) return;
            var anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (anchor == null) return;
            var center = anchor.Position;
            var stand = new List<Pawn>();
            foreach (var p in h.Living.Where(p => !p.Spawned && !p.DevelopmentalStage.Juvenile() && !p.Downed))
            {
                try
                {
                    if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                    GenSpawn.Spawn(p, CellFinder.RandomSpawnCellForPawnNear(center, map), map);
                    Find.WorldPawns.ForcefullyKeptPawns.Add(p);
                    stand.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: a settler in the fight", e);
                }
            }
            if (stand.Count == 0) return;
            LordMaker.MakeNewLord(h.people, new LordJob_AssistColony(h.people, center), map, stand);
            HamletWords.TellFight(home, h, stand);
        }

        // ---------------- Alone ----------------

        /// <summary>
        /// They go alone (let go so, left waiting, or the caravan turned off): their camp packs up and walks off, kept in the
        /// world; a third of them will be turned back on the way, which the colony hears when they would have arrived.
        /// </summary>
        internal static void GoAlone(Map home, HamletHome h, bool fromCamp)
        {
            if (h == null) return;
            int now = Now;
            var camp = CampOf(h);
            if (camp != null && camp.CanTalk)
            {
                camp.told = true;
                camp.Leave("hamlet");
            }
            else Arrivals.SendAway(h.Living.Where(p => p.Spawned && p.Map == home).ToList());
            foreach (var p in h.Living) KeepSettler(p);
            var tile = TileOf(h);
            float tiles = home != null && tile.Valid && tile.Layer == home.Tile.Layer ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, tile) : HamletMath.MaxTiles;
            h.stage = HamletStage.Alone;
            h.turnedBack = HamletMath.TurnedBack(Rand.Value);
            h.due = now + HamletMath.AloneTicks(tiles);
            h.escort = null;
            HamletWords.TellAlone(home, h, fromCamp);
        }

        /// <summary>The hour alone: when they would have arrived, the colony hears they were turned back, or the hamlet stands.</summary>
        private static void RoadAlone(Map home, HamletHome h, int now)
        {
            if (now < h.due) return;
            // Still walking off the colony's map: the road begins once they are off it (one held up a day longer goes at once).
            if (h.Living.Any(p => p.Spawned && p.Map == home))
            {
                if (now < h.due + Day) return;
                OffTheMap(home, h, null);
            }
            if (h.Living.Count == 0)
            {
                End(home, h, HamletEnd.Lost, null);
                return;
            }
            if (h.people == null || h.people.HostileTo(Faction.OfPlayer))
            {
                End(home, h, HamletEnd.Gone, null);
                return;
            }
            if (h.turnedBack)
            {
                End(home, h, HamletEnd.TurnedBack, null);
                return;
            }
            var tile = TileOf(h);
            if (tile.Valid && Current.Game.FindMap(tile) != null) return;
            Found(home, h, false);
        }

        /// <summary>The settlers with this caravan beside it, with their home; null when none go with it.</summary>
        public static HamletHome EscortedBy(Caravan c, out Map home)
        {
            home = null;
            if (c == null || RegionBook.Instance == null) return null;
            foreach (var (m, h) in All())
            {
                if (h.stage != HamletStage.Escorted) continue;
                if (h.escort != null && h.escort.GetCaravan() == c || h.escorts.Any(p => p != null && p.GetCaravan() == c))
                {
                    home = m;
                    return h;
                }
            }
            return null;
        }
    }
}
