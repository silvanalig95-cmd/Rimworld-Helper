using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ground answering where a shown god walks (docs/design/gods.md, "Presence", the trail), with the game's own
    /// things: flowers and grass grown at once on open fertile soil (the Green, the Harvest), a little snow out of doors
    /// (the Night), scorch marks (the Hearth, the War), ash (the Dead), puddles (the Waters, the Storm), and flecks that
    /// fade for the rest (sparks, light, shadow). What lasts is laid on one step in three and forty cells a day at most,
    /// never on a field, a floor or under a roof, and the filth only outside the home area (GodPresenceMath.TrailAt).
    /// And the mark a moment leaves where the god stood: a ring of flowers or scorched ground, a spring, a tree, frost,
    /// ash, a lump of slag or a stone.
    /// </summary>
    public static class GodTrails
    {
        private static readonly string[] Flowers = { "Plant_Daylily", "Plant_Rose", "Plant_Dandelion", "Plant_Astragalus", "Plant_Clivia" };
        private static readonly string[] Grasses = { "Plant_TallGrass", "Plant_Grass", "Plant_Moss" };
        private static readonly string[] Trees = { "Plant_TreeOak", "Plant_TreePoplar", "Plant_TreeBirch" };

        private static ThingDef Named(string n) => DefDatabase<ThingDef>.GetNamedSilentFail(n);

        private static GodPresenceMath.TrailCell Read(Map map, IntVec3 c)
        {
            var t = c.GetTerrain(map);
            var cell = new GodPresenceMath.TrailCell
            {
                fertility = map.fertilityGrid.FertilityAt(c),
                floor = t != null && (t.IsFloor || !t.natural),
                water = t != null && t.IsWater,
                roofed = c.Roofed(map),
                home = map.areaManager.Home[c],
                growZone = c.GetZone(map) is Zone_Growing,
                snow = map.snowGrid.GetDepth(c)
            };
            foreach (var th in c.GetThingList(map))
            {
                if (th is Plant) cell.plant = true;
                else if (th.def.category == ThingCategory.Building || th.def.category == ThingCategory.Item || th is Filth) cell.occupied = true;
            }
            return cell;
        }

        /// <summary>The trail at the cell a god just left. True when it left something that lasts.</summary>
        internal static bool Step(GodLookDef look, Map map, IntVec3 c, bool lasting, Color color)
        {
            if (look == null || map == null || !c.InBounds(map) || look.trail == GodTrail.None) return false;
            try
            {
                var act = GodPresenceMath.TrailAt(look.trail, Read(map, c), lasting);
                var loc = c.ToVector3Shifted();
                switch (act)
                {
                    case TrailAct.Plant:
                        if (Grow(map, c, look.trail == GodTrail.Grass ? Grasses : Flowers)) return true;
                        break;
                    case TrailAct.Snow:
                        map.snowGrid.AddDepth(c, 0.35f);
                        Fleck(look, map, loc, color);
                        return true;
                    case TrailAct.Filth:
                        if (FilthMaker.TryMakeFilth(c, map, FilthFor(look.trail))) return true;
                        break;
                }
                if (act != TrailAct.Nothing) Fleck(look, map, loc, color);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a trail", e);
            }
            return false;
        }

        private static ThingDef FilthFor(GodTrail t)
        {
            switch (t)
            {
                case GodTrail.Scorch:
                    return Named("Filth_BlastMark") ?? ThingDefOf.Filth_Ash;
                case GodTrail.Water:
                case GodTrail.Rain:
                    return Named("Filth_Water") ?? ThingDefOf.Filth_Ash;
                default:
                    return ThingDefOf.Filth_Ash;
            }
        }

        private static bool Grow(Map map, IntVec3 c, string[] kinds)
        {
            var def = kinds.Select(Named).Where(d => d?.plant != null && d.CanEverPlantAt(c, map)).RandomElementWithFallback();
            if (def == null) return false;
            var plant = ThingMaker.MakeThing(def) as Plant;
            if (plant == null) return false;
            plant.Growth = 1f;
            GenSpawn.Spawn(plant, c, map);
            return true;
        }

        /// <summary>What fades behind a god in passing.</summary>
        private static void Fleck(GodLookDef look, Map map, Vector3 loc, Color color)
        {
            if (map != Find.CurrentMap) return;
            switch (look.trail)
            {
                case GodTrail.Sparks:
                    FleckMaker.ThrowMicroSparks(loc, map);
                    break;
                case GodTrail.Water:
                case GodTrail.Rain:
                    FleckMaker.WaterSplash(loc, map, 1.2f, 1f);
                    break;
                case GodTrail.Scorch:
                    FleckMaker.ThrowHeatGlow(loc.ToIntVec3(), map, 0.6f);
                    break;
                case GodTrail.Shadow:
                    GodFx.Throw(GodLookDef.Mote("Mote_ShadowCloud") ?? FleckDefOf.Smoke, loc, map, 0.6f, new Color(0.15f, 0.1f, 0.2f), 0f, 0.1f, 10f, 0.4f);
                    break;
                case GodTrail.Frost:
                    GodFx.Throw(GodLookDef.Mote("Mote_WhiteIce") ?? GodFx.MoteDef, loc, map, 0.35f, color, 0f, 0.05f, 30f, 0.4f);
                    break;
                case GodTrail.Flowers:
                case GodTrail.Grass:
                    GodFx.Throw(GodLookDef.Mote("Mote_Flowers") ?? GodFx.MoteDef, loc, map, 0.35f, color, 0f, 0.05f, 20f, 0.4f);
                    break;
                case GodTrail.Ash:
                    GodFx.Throw(GodLookDef.Mote("Mote_BoneDust") ?? FleckDefOf.DustPuff, loc, map, 0.5f, new Color(0.75f, 0.72f, 0.68f), 0f, 0.1f, 0f, 0.4f);
                    break;
                default:
                    GodFx.Throw(GodFx.MoteDef, loc, map, 0.3f, color, 0f, 0.05f, 0f, 0.5f);
                    break;
            }
        }

        // ================= The mark of a moment =================

        /// <summary>The mark a moment leaves at <paramref name="at"/> (GodPresenceMath.MarkFor chose it), with the game's own things.</summary>
        internal static void Mark(GodMark mark, Map map, IntVec3 at, Color color)
        {
            if (mark == GodMark.None || map == null || !at.InBounds(map)) return;
            try
            {
                int r = GodPresenceMath.MarkRadius(mark);
                switch (mark)
                {
                    case GodMark.Flowers:
                        foreach (var (dx, dz) in GodPresenceMath.Ring(r))
                        {
                            var c = at + new IntVec3(dx, 0, dz);
                            if (c.InBounds(map) && Plantable(map, c)) Grow(map, c, Flowers);
                        }
                        break;
                    case GodMark.Tree:
                        var tree = Trees.Select(Named).Where(d => d?.plant != null).ToList();
                        var wild = map.Biome?.AllWildPlants?.Where(p => p.plant != null && p.plant.IsTree).ToList();
                        if (wild != null && wild.Count > 0) tree = wild;
                        foreach (var c in GenRadial.RadialCellsAround(at, 2.5f, true))
                        {
                            if (!c.InBounds(map) || !Plantable(map, c)) continue;
                            var def = tree.Where(d => d.CanEverPlantAt(c, map)).RandomElementWithFallback();
                            if (def == null) continue;
                            var plant = (Plant)ThingMaker.MakeThing(def);
                            plant.Growth = 1f;
                            GenSpawn.Spawn(plant, c, map);
                            break;
                        }
                        break;
                    case GodMark.Spring:
                        foreach (var c in GenRadial.RadialCellsAround(at, 2.5f, true))
                        {
                            if (!c.InBounds(map)) continue;
                            var t = c.GetTerrain(map);
                            if (t == null || !t.natural || t.IsWater || map.areaManager.Home[c] || c.GetThingList(map).Any(th => th.def.category != ThingCategory.Plant && th.def.category != ThingCategory.Filth)) continue;
                            map.terrainGrid.SetTerrain(c, TerrainDefOf.WaterShallow);
                            FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 2f, 2f);
                            break;
                        }
                        break;
                    case GodMark.Scorch:
                    case GodMark.Ash:
                        var filth = mark == GodMark.Scorch ? Named("Filth_BlastMark") ?? ThingDefOf.Filth_Ash : ThingDefOf.Filth_Ash;
                        foreach (var (dx, dz) in GodPresenceMath.Ring(r))
                        {
                            var c = at + new IntVec3(dx, 0, dz);
                            if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map)) FilthMaker.TryMakeFilth(c, map, filth);
                        }
                        break;
                    case GodMark.Frost:
                        foreach (var c in GenRadial.RadialCellsAround(at, r, true))
                            if (c.InBounds(map) && !c.Roofed(map))
                                map.snowGrid.AddDepth(c, 0.6f);
                        break;
                    case GodMark.Slag:
                    case GodMark.Stone:
                        var chunk = mark == GodMark.Slag ? Named("ChunkSlagSteel") : StoneChunk(map);
                        if (chunk != null) GenPlace.TryPlaceThing(ThingMaker.MakeThing(chunk), at, map, ThingPlaceMode.Near);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mark", e);
            }
        }

        private static bool Plantable(Map map, IntVec3 c)
        {
            var cell = Read(map, c);
            return !cell.floor && !cell.water && !cell.roofed && !cell.growZone && !cell.occupied && !cell.plant && cell.fertility >= 0.7f;
        }

        /// <summary>A chunk of the land's own stone (the standing stone of a war god or a road god), else granite.</summary>
        private static ThingDef StoneChunk(Map map)
        {
            var rock = Find.World?.NaturalRockTypesIn(map.Tile)?.FirstOrDefault();
            return rock?.building?.mineableThing ?? Named("ChunkGranite");
        }
    }
}
