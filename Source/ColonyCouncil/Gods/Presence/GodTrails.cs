using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The ground answering where a shown god walks (docs/design/gods.md, "Presence", the trail), with the game's own
    /// things: flowers and grass grown at once on open fertile soil (the Green, the Harvest), a little snow out of doors
    /// (the Night), scorch marks (the Hearth, the War), ash (the Dead), puddles (the Waters, the Storm), and flecks that
    /// fade for the rest (sparks, light, shadow). What lasts is laid on one step in three and forty cells a day at most,
    /// never on a field, a floor or under a roof, and the filth only outside the home area (GodPresenceMath.TrailAt).
    /// And the mark a moment leaves where it happened: a ring of flowers or scorched ground, a spring, a tree, frost, ash,
    /// lightning, or the goods of a mark worth having (GodMarkGoods: a fresh kill, a weapon, a standing stone, firewood and
    /// warm meals, metal, components).
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

        /// <summary>
        /// The mark a moment leaves at <paramref name="at"/>, as its plan has it (GodMarkGoods.Plan, from GodPresenceMath.MarkFor),
        /// with the game's own things and the player's mods': a ring of flowers or scorched ground, a spring, a tree, frost, ash,
        /// lightning, and the goods of a mark worth having.
        /// </summary>
        internal static void Mark(GodMarkPlan plan, Map map, IntVec3 at, Color color)
        {
            var mark = plan?.mark ?? GodMark.None;
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
                        var (cell, def) = TreeAt(map, at);
                        if (def == null) break;
                        var plant = (Plant)ThingMaker.MakeThing(def);
                        plant.Growth = 1f;
                        GenSpawn.Spawn(plant, cell, map);
                        break;
                    case GodMark.Spring:
                        var spring = SpringAt(map, at);
                        if (!spring.IsValid) break;
                        map.terrainGrid.SetTerrain(spring, TerrainDefOf.WaterShallow);
                        FleckMaker.WaterSplash(spring.ToVector3Shifted(), map, 2f, 2f);
                        break;
                    case GodMark.Scorch:
                        Scorched(map, at, r, BlastMark);
                        break;
                    case GodMark.Ash:
                        Scorched(map, at, r, ThingDefOf.Filth_Ash);
                        break;
                    case GodMark.Lightning:
                        Strike(map, at);
                        Scorched(map, at, r, BlastMark);
                        break;
                    case GodMark.Frost:
                        foreach (var c in GenRadial.RadialCellsAround(at, r, true))
                            if (c.InBounds(map) && !c.Roofed(map))
                                map.snowGrid.AddDepth(c, 0.6f);
                        break;
                    case GodMark.Weapon:
                        if (plan.ring) Scorched(map, at, r, BlastMark);
                        GodMarkGoods.Lay(plan, map, at, color);
                        break;
                    case GodMark.Kill:
                        GodMarkGoods.LayKill(plan, map, at, color);
                        break;
                    case GodMark.StandingStone:
                        GodMarkGoods.Raise(plan, map, at, color);
                        break;
                    case GodMark.Hearth:
                    case GodMark.Metal:
                    case GodMark.Components:
                        GodMarkGoods.Lay(plan, map, at, color);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mark", e);
            }
        }

        /// <summary>
        /// The ground at <paramref name="at"/> can take a mark that only changes it (asked as the moment begins, so its words say
        /// only what will be there): fertile open soil for flowers and a tree, natural ground off the home area for a spring,
        /// open ground under the sky for scorch, ash, frost, lightning and a weapon's ring. The goods of the other marks go
        /// anywhere.
        /// </summary>
        internal static bool CanMark(GodMark mark, Map map, IntVec3 at)
        {
            if (mark == GodMark.None || map == null || !at.InBounds(map)) return false;
            int r = GodPresenceMath.MarkRadius(mark);
            switch (mark)
            {
                case GodMark.Flowers:
                    return GodPresenceMath.Ring(r).Any(d => (at + new IntVec3(d.dx, 0, d.dz)).InBounds(map) && Plantable(map, at + new IntVec3(d.dx, 0, d.dz)));
                case GodMark.Tree:
                    return TreeAt(map, at).def != null;
                case GodMark.Spring:
                    return SpringAt(map, at).IsValid;
                case GodMark.Scorch:
                case GodMark.Ash:
                case GodMark.Lightning:
                case GodMark.Weapon:
                    return GodPresenceMath.Ring(r).Any(d => Open(map, at + new IntVec3(d.dx, 0, d.dz)));
                case GodMark.Frost:
                    return GenRadial.RadialCellsAround(at, r, true).Any(c => c.InBounds(map) && !c.Roofed(map));
                default:
                    return true;
            }
        }

        private static ThingDef BlastMark => Named("Filth_BlastMark") ?? ThingDefOf.Filth_Ash;

        /// <summary>Open ground under the sky that a scorch mark or ash can lie on.</summary>
        private static bool Open(Map map, IntVec3 c) => c.InBounds(map) && c.Standable(map) && !c.Roofed(map);

        private static void Scorched(Map map, IntVec3 at, int r, ThingDef filth)
        {
            foreach (var (dx, dz) in GodPresenceMath.Ring(r))
            {
                var c = at + new IntVec3(dx, 0, dz);
                if (Open(map, c)) FilthMaker.TryMakeFilth(c, map, filth);
            }
        }

        /// <summary>Lightning at the place as the eye and the ear take it (the sky's flash, a glow on the ground, thunder, a scorch mark): no fire, nobody hurt.</summary>
        private static void Strike(Map map, IntVec3 at)
        {
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
            FleckMaker.ThrowLightningGlow(at.ToVector3Shifted(), map, 4f);
            SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(at, map));
            if (Open(map, at)) FilthMaker.TryMakeFilth(at, map, BlastMark);
        }

        /// <summary>The cell nearest the place where one of the land's trees (else an oak, a poplar or a birch) can grow now, and the tree; none on bare ground.</summary>
        private static (IntVec3 cell, ThingDef def) TreeAt(Map map, IntVec3 at)
        {
            var tree = Trees.Select(Named).Where(d => d?.plant != null).ToList();
            var wild = map.Biome?.AllWildPlants?.Where(p => p.plant != null && p.plant.IsTree).ToList();
            if (wild != null && wild.Count > 0) tree = wild;
            foreach (var c in GenRadial.RadialCellsAround(at, 2.5f, true))
            {
                if (!c.InBounds(map) || !Plantable(map, c)) continue;
                var def = tree.Where(d => d.CanEverPlantAt(c, map)).RandomElementWithFallback();
                if (def != null) return (c, def);
            }
            return (IntVec3.Invalid, null);
        }

        /// <summary>The cell nearest the place where a spring can come up: natural dry ground off the home area, with nothing on it but plants and filth.</summary>
        private static IntVec3 SpringAt(Map map, IntVec3 at)
        {
            foreach (var c in GenRadial.RadialCellsAround(at, 2.5f, true))
            {
                if (!c.InBounds(map)) continue;
                var t = c.GetTerrain(map);
                if (t == null || !t.natural || t.IsWater || map.areaManager.Home[c]
                    || c.GetThingList(map).Any(th => th.def.category != ThingCategory.Plant && th.def.category != ThingCategory.Filth)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        private static bool Plantable(Map map, IntVec3 c)
        {
            var cell = Read(map, c);
            return !cell.floor && !cell.water && !cell.roofed && !cell.growZone && !cell.occupied && !cell.plant && cell.fertility >= 0.7f;
        }
    }
}
