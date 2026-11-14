using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own way of taking what is left at its place (round 2 of docs/gods-next-proposals.md, section D, 5 and 6;
    /// GodWorshipDef.taking; the rules are OfferingWayMath): what shows on the map where it lay, in the way's own words in
    /// the news. A sapling where Hollin's lay; Smoulder's down a crack, the floor warm after; Bellwether's grazed, a
    /// hoofprint beside it; Morel's rotted into black soil with a mushroom; Ennu's gone with footprints in and none away;
    /// Pip's part gone and a broken machine working again; Cirrus's taken up in a streak of light; Hush's frosted over in
    /// the night and gone at dawn; Undertow's gone and something else with it; Helios's never taken; Selene's gone only
    /// under a moon; Murmur's down into the well with a croak; Ducat's given to a visitor; Tamiu's eaten by a cat at dawn.
    /// A god whose worship names no way keeps the shared one (gone by morning, nobody saw it go). What it is noticed for by
    /// day is in the way's own words too (Rimfolk_Pool_Worship_Way), as the one who noticed takes it.
    /// </summary>
    public static class OfferingWays
    {
        public const string NoticedPool = "Rimfolk_Pool_Worship_Way";

        /// <summary>A god's way of taking: its worship's own, else the shared one.</summary>
        public static OfferingWay Of(God g) => g == null ? OfferingWay.Vanish : GodWorshipDef.For(g)?.taking ?? OfferingWay.Vanish;

        /// <summary>The one a gift is given to is on the land: a visitor or a trader for Ducat's.</summary>
        public static bool BearerHere(OfferingWay w, Map map) => w == OfferingWay.Given && Visitor(map, IntVec3.Invalid) != null;

        /// <summary>The words for this way and how it went, when the way has them (else the shared words).</summary>
        public static string Key(OfferingWay w, string variant)
        {
            string key = OfferingWayMath.Key(w) + "_" + variant;
            return w != OfferingWay.Vanish && key.CanTranslate() ? key : null;
        }

        // ================= While it waits =================

        /// <summary>
        /// The way's signs while the gift still lies there: Hush's frost forming on it in the night, Helios's noon light lying
        /// on it (each once).
        /// </summary>
        internal static void Before(OfferingRecord o, Map map, God g, OfferingWay w, int hour, float hours)
        {
            if (o.stage != 0 || o.thing == null || !o.thing.Spawned) return;
            var cell = o.thing.Position;
            if (OfferingWayMath.FrostsNow(w, hour, hours))
            {
                o.stage = 1;
                GodOmens.Rime(map, cell);
                for (int i = 0; i < 6; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.3f, 0.4f)), map, 0.3f, Frost, 0f, 0.1f, 0f, 1.4f);
            }
            else if (OfferingWayMath.NoonLightNow(w, hour))
            {
                o.stage = 1;
                Worship.Glow(map, cell, g);
                for (int i = 0; i < 8; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map, 0.4f, Sun, 0f, 0.3f, 0f, 1.2f);
            }
        }

        private static readonly Color Frost = new Color(0.85f, 0.93f, 1f), Sun = new Color(1f, 0.92f, 0.55f), Silver = new Color(0.78f, 0.84f, 1f), Green = new Color(0.55f, 0.88f, 0.42f),
            Gold = new Color(1f, 0.84f, 0.35f), Up = new Color(0.82f, 0.9f, 1f);

        // ================= The taking =================

        /// <summary>
        /// The god takes it in its own way, at <paramref name="cell"/>: what shows there (the thing itself goes, or is given
        /// on), and which of the way's words tell it (<see cref="OfferingWayMath.Variants"/>), with what else the words name
        /// (<paramref name="extra"/>: the tree, the machine mended, what else went, the visitor). Null when it isn't done yet
        /// (a cat has been sent to eat it: it is told once she has).
        /// </summary>
        internal static string Take(OfferingRecord o, Map map, God g, OfferingWay w, IntVec3 cell, bool plain, out string extra, out string extra2)
        {
            extra = "";
            extra2 = "";
            var thing = o.thing;
            string felt = plain ? "Plain" : "Felt";
            switch (w)
            {
                case OfferingWay.Sapling:
                {
                    Gone(thing);
                    var tree = Sapling(map, cell);
                    for (int i = 0; i < 8; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map, 0.35f, Green, 0f, 0.3f, 0f, 1.2f);
                    if (tree == null)
                    {
                        if (ThingDefOf.Filth_Dirt != null) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt);
                        return "Bare";
                    }
                    extra = tree.LabelNoCount;
                    return felt;
                }
                case OfferingWay.Crack:
                    Gone(thing);
                    if (ThingDefOf.Filth_RubbleRock != null) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_RubbleRock);
                    FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, 1.2f, new Color(0.5f, 0.45f, 0.4f, 0.7f));
                    FleckMaker.ThrowHeatGlow(cell, map, 1.4f);
                    FleckMaker.ThrowAirPuffUp(cell.ToVector3Shifted(), map);
                    GenTemperature.PushHeat(cell, map, 30f);
                    return felt;
                case OfferingWay.Grazed:
                {
                    Gone(thing);
                    var print = GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)).Where(c => c.InBounds(map) && c.Standable(map)).RandomElementWithFallback(cell);
                    Print(map, print, 0.35f);
                    return felt;
                }
                case OfferingWay.Rot:
                {
                    Gone(thing);
                    var t = cell.GetTerrain(map);
                    if (t != null && !t.layerable && t.fertility > 0f && !t.IsWater)
                    {
                        var rich = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
                        if (rich != null && t != rich) map.terrainGrid.SetTerrain(cell, rich);
                    }
                    else if (ThingDefOf.Filth_Dirt != null) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt);
                    Mushroom(map, cell);
                    for (int i = 0; i < 6; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted(), map, 0.3f, new Color(0.9f, 0.88f, 0.75f), Rand.Range(-40f, 40f), 0.15f, 0f, 1.6f);
                    return felt;
                }
                case OfferingWay.Guest:
                    Gone(thing);
                    FootprintsIn(map, cell);
                    return felt;
                case OfferingWay.Mended:
                {
                    Gone(thing);
                    var broken = map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.GetComp<CompBreakdownable>()?.BrokenDown == true)
                        .OrderBy(b => b.Position.DistanceToSquared(cell)).FirstOrDefault();
                    FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
                    if (broken != null)
                    {
                        try
                        {
                            broken.GetComp<CompBreakdownable>().Notify_Repaired();
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("worship: a machine mended", e);
                        }
                        for (int i = 0; i < 3; i++) FleckMaker.ThrowMicroSparks(broken.TrueCenter(), map);
                        GodSounds.Play(GodSounds.Sound("Power_OnSmall"), map, broken.Position);
                        if (broken is ThingWithComps lit && lit.GetComp<CompGlower>() != null) GodOmens.Play(map, lit, 240, true, false);
                        extra = broken.LabelShort;
                        return felt;
                    }
                    var light = GodOmens.Powered(map, true).OrderBy(l => l.Position.DistanceToSquared(cell)).FirstOrDefault();
                    if (light != null) GodOmens.Play(map, light, 240, true, false);
                    return plain ? "Plain" : "None";
                }
                case OfferingWay.TakenUp:
                    Gone(thing);
                    FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 3f);
                    for (int i = 0; i < 10; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted(), map, Rand.Range(0.3f, 0.6f), Up, Rand.Range(-8f, 8f), Rand.Range(4f, 8f), 0f, 0.6f);
                    return felt;
                case OfferingWay.Frost:
                    Gone(thing);
                    GodOmens.Rime(map, cell);
                    for (int i = 0; i < 6; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted(), map, 0.3f, Frost, Rand.Range(-30f, 30f), 0.2f, 0f, 1.4f);
                    return felt;
                case OfferingWay.Hunger:
                {
                    Gone(thing);
                    Worship.Glow(map, cell, g, true);
                    var other = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .Where(t => t.Spawned && t != thing && !(t is MinifiedThing) && !(t is Corpse) && t.IsInAnyStorage() && t.Position.InHorDistOf(cell, 12f)
                                    && OfferingWayMath.HungerTakes(t.stackCount) > 0)
                        .InRandomOrder().FirstOrDefault();
                    if (other == null) return plain ? "Plain" : "Only";
                    int n = OfferingWayMath.HungerTakes(other.stackCount);
                    extra = GenLabel.ThingLabel(other.def, other.Stuff, n);
                    try
                    {
                        other.SplitOff(n).Destroy(DestroyMode.Vanish);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("worship: what else the Hunger took", e);
                    }
                    return felt;
                }
                case OfferingWay.Moon:
                    Gone(thing);
                    Worship.Glow(map, cell, g);
                    for (int i = 0; i < 8; i++) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map, 0.35f, Silver, 0f, 0.2f, 0f, 1.6f);
                    return felt;
                case OfferingWay.Well:
                {
                    Gone(thing);
                    var at = GodOmens.Wet(map);
                    if (ThingDefOf.Filth_Water != null) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Water);
                    var where = at.IsValid ? at : cell;
                    for (int i = 0; i < 4; i++) FleckMaker.WaterRipple((where + GenRadial.RadialPattern[Rand.Range(0, 9)]).ToVector3Shifted(), map, 1.2f);
                    GodSounds.Play(GodSounds.Sound("Pawn_Frog_Call") ?? GodSounds.Sound("AG_Pawn_Frog_Call"), map, where);
                    return felt;
                }
                case OfferingWay.Given:
                {
                    var v = Visitor(map, cell);
                    if (v == null) return null;
                    try
                    {
                        thing.SetForbidden(false, false);
                        if (thing.Spawned) thing.DeSpawn();
                        if (!v.inventory.innerContainer.TryAdd(thing))
                        {
                            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                            thing.SetForbidden(true, false);
                            return null;
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("worship: a gift given on", e);
                        return null;
                    }
                    for (int i = 0; i < 6; i++) GodFx.Throw(GodFx.MoteDef, v.DrawPos + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.2f, 0.5f)), map, 0.3f, Gold, 0f, 0.1f, 0f, 0.6f);
                    string line = Worship.Line(NoticedPool, v, g, null, OfferingWayMath.Tag(w), "visitor");
                    if (!line.NullOrEmpty()) Speech.Say(v, line);
                    extra = v.LabelShort;
                    extra2 = v.Faction?.Name ?? "";
                    return plain ? "Plain" : "Seen";
                }
                case OfferingWay.Cat:
                {
                    var cat = plain ? null : MogsWays.Cats(map, false).Where(c => c.Spawned && !c.InMentalState && (c.CurJob == null || c.CurJob.def.casualInterruptible)
                                                                                   && c.CanReach(thing, PathEndMode.Touch, Danger.Some))
                        .OrderBy(c => c.Position.DistanceToSquared(cell)).FirstOrDefault();
                    if (o.stage == 0 && cat != null && thing.IngestibleNow && cat.RaceProps.CanEverEat(thing))
                    {
                        try
                        {
                            var eat = JobMaker.MakeJob(JobDefOf.Ingest, thing);
                            eat.count = thing.stackCount;
                            eat.expiryInterval = GenDate.TicksPerHour * 2;
                            cat.jobs.StartJob(eat, JobCondition.InterruptForced);
                            if (cat.CurJob?.def == JobDefOf.Ingest)
                            {
                                o.stage = 1;
                                o.cat = cat;
                                return null;
                            }
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("worship: a cat sent to eat", e);
                        }
                    }
                    // Gone at dawn all the same: paw prints where it lay, and a cat (if one is near) lying down there after.
                    Gone(thing);
                    for (int i = 0; i < 5; i++)
                        FleckMaker.PlaceFootprint(cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(-0.6f, 0.6f)), map, Rand.Range(0f, 360f));
                    if (ThingDefOf.Filth_Dirt != null) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt);
                    var lie = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.LieJob);
                    if (cat != null && lie != null)
                    {
                        var j = JobMaker.MakeJob(lie, cell);
                        j.count = 1500;
                        j.expiryInterval = 1700;
                        cat.jobs.StartJob(j, JobCondition.InterruptForced);
                    }
                    return felt;
                }
                default:
                    Gone(thing);
                    return felt;
            }
        }

        /// <summary>Tamiu's cat has eaten it where it lay (the cat sent at dawn): her taking, told as seen.</summary>
        internal static bool Eaten(OfferingRecord o, God g) => o != null && o.stage == 1 && Of(g) == OfferingWay.Cat;

        private static void Gone(Thing t)
        {
            if (t != null && !t.Destroyed) t.Destroy(DestroyMode.Vanish);
        }

        /// <summary>A young tree of the land where it lay (or beside it): the biome's own trees, the first that will grow there.</summary>
        private static Plant Sapling(Map map, IntVec3 cell)
        {
            var trees = (map.Biome?.AllWildPlants ?? Enumerable.Empty<ThingDef>()).Where(d => d?.plant != null && d.plant.IsTree).ToList();
            if (trees.Count == 0) return null;
            foreach (var c in GenRadial.RadialCellsAround(cell, 2f, true))
            {
                if (!c.InBounds(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null) continue;
                var def = trees.InRandomOrder().FirstOrDefault(d => d.CanEverPlantAt(c, map, true));
                if (def == null) continue;
                try
                {
                    c.GetPlant(map)?.Destroy(DestroyMode.Vanish);
                    var p = (Plant)ThingMaker.MakeThing(def);
                    p.Growth = 0.15f;
                    GenSpawn.Spawn(p, c, map);
                    return p;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: a sapling where the gift lay", e);
                    return null;
                }
            }
            return null;
        }

        /// <summary>A mushroom standing in the black soil (the game's, the first that will grow there).</summary>
        private static void Mushroom(Map map, IntVec3 cell)
        {
            foreach (var name in new[] { "Agarilux", "Glowstool", "Bryolux" })
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d?.plant == null || cell.GetPlant(map) != null || cell.GetFirstItem(map) != null || !d.CanEverPlantAt(cell, map, true)) continue;
                try
                {
                    var p = (Plant)ThingMaker.MakeThing(d);
                    p.Growth = 1f;
                    GenSpawn.Spawn(p, cell, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: a mushroom in the soil", e);
                }
                return;
            }
        }

        /// <summary>A print in the ground: carved in the snow where it lies, else dirt.</summary>
        private static void Print(Map map, IntVec3 c, float snow)
        {
            if (!c.InBounds(map)) return;
            float d = map.snowGrid.GetDepth(c);
            if (d > 0.1f) map.snowGrid.SetDepth(c, Mathf.Max(0f, d - snow));
            else if (ThingDefOf.Filth_Dirt != null) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_Dirt);
            FleckMaker.PlaceFootprint(c.ToVector3Shifted(), map, Rand.Range(0f, 360f));
        }

        /// <summary>Footprints coming in to where it lay from the nearest edge of the land, and none going away.</summary>
        private static void FootprintsIn(Map map, IntVec3 cell)
        {
            var edges = new[] { new IntVec3(cell.x, 0, 0), new IntVec3(cell.x, 0, map.Size.z - 1), new IntVec3(0, 0, cell.z), new IntVec3(map.Size.x - 1, 0, cell.z) };
            var edge = edges.OrderBy(e => e.DistanceToSquared(cell)).First();
            var dir = (edge - cell).ToVector3().normalized;
            for (int i = 1; i <= 7; i++)
            {
                var c = (cell.ToVector3Shifted() + dir * i * 1.4f).ToIntVec3();
                if (c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null) Print(map, c, 0.2f);
            }
        }

        /// <summary>The visitor or trader on the land nearest <paramref name="near"/> (or any), who may be given something to carry on.</summary>
        private static Pawn Visitor(Map map, IntVec3 near)
        {
            if (map == null) return null;
            var player = Faction.OfPlayer;
            var all = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && p.Faction != player && !p.Faction.HostileTo(player) && !p.IsPrisoner
                                                             && !p.IsSlave && !p.Downed && !p.Dead && !p.InMentalState && p.inventory != null && !GodAvatar.Is(p) && !p.IsQuestLodger());
            return near.IsValid ? all.OrderBy(p => p.Position.DistanceToSquared(near)).FirstOrDefault() : all.FirstOrDefault();
        }

        // ================= Noticed by day, and the inspect line =================

        /// <summary>The words the one who notices it gone says: the way's own (what they see where it lay), else the shared ones.</summary>
        internal static string NoticedLine(Pawn p, God g, bool mine)
        {
            var w = Of(g);
            if (w == OfferingWay.Vanish) return null;
            return Worship.Line(NoticedPool, p, g, null, OfferingWayMath.Tag(w), mine ? "mine" : "theirs");
        }

        /// <summary>What became of the last gift, for the place's inspect line: the way's own words where it has them.</summary>
        internal static string FateKey(God g, OfferingFate fate)
        {
            var w = Of(g);
            if (w == OfferingWay.Vanish || fate != OfferingFate.Taken && fate != OfferingFate.Returned) return null;
            string key = "CC_Wsh_Inspect_Fate_Way_" + w + (fate == OfferingFate.Returned ? "_Returned" : "");
            return key.CanTranslate() ? key : null;
        }
    }
}
