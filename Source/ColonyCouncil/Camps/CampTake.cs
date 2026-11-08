using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a camp may take from the map, read off the game for the pure rule (CampTakeMath): every tree they fell, beast
    /// they hunt, plant they gather, rock they mine or chunk they carry is asked here first.
    /// </summary>
    public static class CampTake
    {
        private static float stoneValue = -1f;

        /// <summary>What a piece of plain stone is worth: a granite chunk's market value (a silver at the least, so a thing worth nothing counts as plain).</summary>
        public static float StoneValue
        {
            get
            {
                if (stoneValue < 0f)
                {
                    var granite = DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
                    stoneValue = UnityEngine.Mathf.Max(1f, granite?.BaseMarketValue ?? 0f);
                }
                return stoneValue;
            }
        }

        /// <summary>A stone chunk (the game's or a mod's, by its category).</summary>
        public static bool StoneChunk(ThingDef d) => d != null && d.thingCategories != null && d.IsWithinCategory(ThingCategoryDefOf.StoneChunks);

        /// <summary>A mineable whose yield is plain stone or nothing, decided by what it yields (so every mod's ore is left alone).</summary>
        public static bool PlainRock(ThingDef d)
        {
            if (d?.building == null || !d.mineable) return false;
            var yields = d.building.mineableThing;
            return CampTakeMath.PlainRock(d.building.isResourceRock, yields == null || StoneChunk(yields), yields?.BaseMarketValue ?? 0f, StoneValue);
        }

        /// <summary>The facts about a thing on the map for the rule, as this camp sees it (null camp: as any camp would, before it comes).</summary>
        public static TakeFacts Facts(Thing t, Camp camp)
        {
            var map = t.MapHeld;
            var f = new TakeFacts { stoneValue = StoneValue };
            var pos = t.PositionHeld;
            f.theirs = camp != null && (camp.Owns(t) || camp.FromTheirWork(t));
            f.colony = t.Faction == Faction.OfPlayer || (t is Pawn p0 && p0.Faction == Faction.OfPlayer);
            f.others = !f.colony && t.Faction != null && (camp == null || t.Faction != camp.faction);
            if (map != null && pos.InBounds(map))
            {
                var home = map.areaManager.Home;
                if (home != null)
                    foreach (var x in GenRadial.RadialCellsAround(pos, CampSite.HomeMargin, true))
                        if (x.InBounds(map) && home[x])
                        {
                            f.home = true;
                            break;
                        }
                f.zoned = map.zoneManager.ZoneAt(pos) != null || CampSite.Marked(pos, map) || map.designationManager.DesignationOn(t) != null;
                f.stored = t.Spawned && StoreUtility.IsInAnyStorage(t);
            }
            f.forbidden = t.def.category == ThingCategory.Item && t.IsForbidden(Faction.OfPlayer) && !f.theirs && !(camp?.FromTheirWork(t) ?? false);
            switch (t.def.category)
            {
                case ThingCategory.Plant:
                    var plant = t as Plant;
                    f.what = plant?.def.plant?.IsTree == true ? TakeWhat.Tree : TakeWhat.Plant;
                    // A tree of the faiths (an anima tree, a gauranlen) is nobody's to fell.
                    f.sown = plant == null || plant.sown || plant.def.plant.treeCategory == TreeCategory.Super;
                    break;
                case ThingCategory.Pawn:
                    f.what = TakeWhat.Animal;
                    break;
                case ThingCategory.Building:
                    if (t.def.mineable)
                    {
                        f.what = TakeWhat.Mineable;
                        var y = t.def.building?.mineableThing;
                        f.resourceRock = t.def.building?.isResourceRock ?? false;
                        f.stoneChunk = y == null || StoneChunk(y);
                        f.value = y?.BaseMarketValue ?? 0f;
                    }
                    else f.what = TakeWhat.Building;
                    break;
                default:
                    f.what = TakeWhat.Item;
                    f.stoneChunk = StoneChunk(t.def);
                    f.value = t is Corpse ? 0f : t.MarketValue;
                    break;
            }
            return f;
        }

        /// <summary>The camp may take this (or clear it away).</summary>
        public static bool May(Thing t, Camp camp) => t != null && CampTakeMath.May(Facts(t, camp));
    }
}
