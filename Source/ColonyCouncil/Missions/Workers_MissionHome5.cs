using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== Q14. A cache in old ruins (the one away quest) =====================

    /// <summary>
    /// A neighbour's old map shows a cache close by (docs/quests-proposals.md, Q14): old ruins within three tiles of the colony,
    /// the game's own item stash site, with the cache inside (Rimfolk's own old chest) and a few things lying with it. The
    /// colony sends a caravan, brings the cache home, and their people come for it and pay; what else lay there is the colony's
    /// to keep. The only single quest that sends the colony's people out (rule 2 of the proposals): offered only when the ruins
    /// are that close, and at half the chance of the quests at home. Not brought home in time (a few days more for a caravan on
    /// its way), or destroyed: lost.
    /// </summary>
    public class HappeningWorker_MissionRuinsCache : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuests3Math.CacheKind;

        protected override float RestDays => HomeQuests3Math.RestDays;

        protected override int Rating => 3;

        public static ThingDef CacheDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_QuestCache");

        public static SitePartDef StashPart => DefDatabase<SitePartDef>.GetNamedSilentFail("ItemStash");

        private static bool Tile(Map map, out PlanetTile tile) =>
            TileFinder.TryFindNewSiteTile(out tile, map.Tile, HomeQuests3Math.MinTiles, HomeQuests3Math.MaxTiles, false, null, 0f);

        protected override bool Possible(Map map) => CacheDef != null && StashPart != null && Neighbor(map) != null && Tile(map, out _);

        /// <summary>Their old map by their age: painted on a hide, drawn on old parchment, a survey map, a data slate.</summary>
        public static string MapTag(Faction f)
        {
            int age = HomeQuests.AgeOf(f);
            if (Ages.IsEmpire(f) || age >= HomeQuestsMath.Spacer) return "slate";
            return age == HomeQuestsMath.Industrial ? "survey" : age == HomeQuestsMath.Medieval ? "parchment" : "hide";
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null || !Tile(map, out var tile)) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.variant = MapTag(m.faction);
            m.a = VarietyUtil.Best(map, SkillDefOf.Shooting) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            m.days = HomeQuests3Math.Days(Find.WorldGrid.ApproxDistanceInTiles(map.Tile, tile));
            m.reward = HomeQuests3Math.Reward(HomeQuests.AgeOf(m.faction), Ages.IsEmpire(m.faction), LivingEvents.WealthFactor(map), Rand.Value);
            m.goodwill = HomeQuests3Math.Goodwill;
            m.penalty = HomeQuests3Math.Penalty;
            m.target = -1;
            Fill(m, ctx);
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => Mathf.RoundToInt(m.days), m => m.reward);

        public static Site SiteOf(Mission m) => m == null || m.target < 0 ? null : Find.WorldObjects.Sites.FirstOrDefault(x => x.ID == m.target && !x.Destroyed);

        /// <summary>The cache, and what else lay with it (the colony's to keep), made for the ruins.</summary>
        private static List<Thing> Contents(Mission m, out Thing cache)
        {
            cache = HomeQuests.Make(CacheDef);
            var list = new List<Thing>();
            if (cache == null) return list;
            list.Add(cache);
            list.AddRange(Loot.Make(HomeQuests3Math.KeepWorth(m.reward)));
            return list;
        }

        public override bool Start(Mission m, Map map)
        {
            var part = StashPart;
            if (part == null || !Tile(map, out var tile)) return false;
            Site site;
            try
            {
                site = SiteMaker.MakeSite(part, tile, null, true, 0f);
                if (site == null) return false;
                site.customLabel = "CC_HomeQ3_Ruins".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve().CapitalizeFirst();
                site.GetComponent<TimeoutComp>()?.StartTimeout(Mathf.RoundToInt((m.days + HomeQuests3Math.HomewardDays) * GenDate.TicksPerDay));
                // The cache and its things in the stash's own holder: the game's map making sets them down inside the ruins.
                var sp = site.parts?.FirstOrDefault();
                if (sp != null)
                {
                    sp.things ??= new ThingOwner<Thing>(sp, false);
                    var things = Contents(m, out var cache);
                    foreach (var t in things) sp.things.TryAdd(t);
                    m.thing = cache;
                }
                Find.WorldObjects.Add(site);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the old ruins", e);
                return false;
            }
            m.target = site.ID;
            m.stage = 0;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, tile);
            int n = Mathf.Max(1, Mathf.RoundToInt(tiles));
            m.tale = (n == 1 ? "CC_HomeQ3_WhereOne" : "CC_HomeQ3_Where").Translate(n.Named("COUNT"), NeighbourLetters.Direction(map.Tile, tile).Named("WAY")).Resolve();
            return true;
        }

        /// <summary>The ruins' map made (the colony's caravan came to it): the cache there, wherever the game's map making left it.</summary>
        public static void MapMade(Site site)
        {
            var m = Missions.All.FirstOrDefault(x => x.Open && x.kind == HomeQuests3Math.CacheKind && x.target == site?.ID);
            var map = site?.Map;
            if (m == null || map == null) return;
            if (m.thing != null && !m.thing.Destroyed && m.thing.MapHeld == map) return;
            // Not set down by the map making (or never made): set down with its things in the open middle of the ruins.
            var list = new List<Thing>();
            if (m.thing != null && !m.thing.Destroyed && !m.thing.Spawned)
            {
                m.thing.holdingOwner?.Remove(m.thing);
                list.Add(m.thing);
            }
            else
            {
                list = Contents(m, out var cache);
                m.thing = cache;
            }
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 20, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstItem(map) == null, out var at)) at = map.Center;
            foreach (var t in list) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
        }

        /// <summary>The cache in the hands or packs of a caravan of the colony's, on the road.</summary>
        private static bool WithCaravan(Thing t) => t?.ParentHolder is Pawn_InventoryTracker inv && inv.pawn.Faction == Faction.OfPlayer && inv.pawn.GetCaravan() != null;

        public override void Check(Mission m, Map map, int now)
        {
            switch (m.stage)
            {
                case 0:
                {
                    var t = m.thing;
                    bool caravan = WithCaravan(t);
                    var site = SiteOf(m);
                    var how = HomeQuests3Math.Outcome(t != null && t.Destroyed, t != null && !t.Destroyed && t.MapHeld == map, caravan, site != null,
                        now >= m.deadline + (caravan ? HomeQuests3Math.HomewardDays * GenDate.TicksPerDay : 0));
                    if (how == CacheEnd.Away) return;
                    if (how == CacheEnd.Lost)
                    {
                        Missions.Goodwill(m, -m.penalty);
                        Missions.Finish(m, MissionState.Failed, t == null || site != null ? "notfound" : "lost", null);
                        return;
                    }
                    // Home: their people come for it within half a day.
                    m.stage = 1;
                    m.due = now + GenDate.TicksPerDay / 2;
                    string line = Line(More?.ready, m, map, null, null);
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(t.SpawnedParentOrMe ?? t), MessageTypeDefOf.PositiveEvent);
                    return;
                }
                case 1:
                    if (m.thing == null || m.thing.Destroyed || m.thing.MapHeld != map)
                    {
                        Missions.Goodwill(m, -m.penalty);
                        Missions.Finish(m, MissionState.Failed, "lost", null);
                        return;
                    }
                    if (now < m.due || !m.thing.Spawned) return;
                    m.stage = 2;
                    m.lastLook = now;
                    if (!Couriers.CollectThings(map, new List<Thing> { m.thing }, m.faction, HomeQuests.PayThings(m, m.reward))) Returned(m, map, true);
                    return;
                default:
                    if (m.thing == null || m.thing.Destroyed || HomeQuests.HeldBy(m.thing, m.faction) || !HomeQuests.OnMap(m.thing, map)) Returned(m, map, false);
                    else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Returned(m, map, true);
                    return;
            }
        }

        private void Returned(Mission m, Map map, bool now)
        {
            if (now)
            {
                if (m.thing != null && !m.thing.Destroyed) m.thing.Destroy();
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "returned", HomeQuests.RewardWords(m, m.reward));
        }

        /// <summary>The ruins taken from the world map once it is over (never while the colony is there).</summary>
        public override void Release(Mission m)
        {
            var site = SiteOf(m);
            if (site == null || site.HasMap) return;
            try
            {
                site.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the old ruins taken away", e);
            }
        }

        public override LookTargets Targets(Mission m)
        {
            if (m.thing != null && !m.thing.Destroyed && m.thing.SpawnedOrAnyParentSpawned) return new LookTargets(m.thing.SpawnedParentOrMe);
            var c = m.thing?.ParentHolder is Pawn_InventoryTracker inv ? inv.pawn.GetCaravan() : null;
            if (c != null) return new LookTargets(c);
            var site = SiteOf(m);
            return site != null ? new LookTargets(site) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            string cache = CacheDef?.label ?? "?";
            if (m.stage >= 1) return "CC_HomeQ3_Status_Home".Translate((m.faction?.Name ?? "?").Named("FACTION"), cache.Named("THING")).Resolve();
            if (WithCaravan(m.thing)) return "CC_HomeQ3_Status_Road".Translate(cache.Named("THING")).Resolve();
            return "CC_HomeQ3_Status_Ruins".Translate((m.tale ?? "?").Named("WHERE"), cache.Named("THING")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "home now", "returned", "lost" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            switch (outcome)
            {
                case "home now":
                    if (m.stage != 0) return;
                    var t = m.thing;
                    if (t == null || t.Destroyed) t = m.thing = HomeQuests.Make(CacheDef);
                    t.holdingOwner?.Remove(t);
                    if (t.Spawned) t.DeSpawn();
                    GenPlace.TryPlaceThing(t, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near);
                    return;
                case "returned":
                    Returned(m, map, true);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "lost", null);
                    return;
            }
        }
    }

    /// <summary>The old ruins' map made (the colony's caravan came to it): the cache there (HappeningWorker_MissionRuinsCache.MapMade). Only adds it.</summary>
    [HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]
    public static class Patch_HomeQuestRuinsCache
    {
        public static void Postfix(Site __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                HappeningWorker_MissionRuinsCache.MapMade(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the old ruins' map", e);
            }
        }
    }
}
