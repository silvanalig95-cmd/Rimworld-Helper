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
    /// Each god's own signs (GodShapeDef.omens; docs/gods-pantheon-brainstorm.md, "The nine", how each is seen): what
    /// shows on the map for each, as the words say it. A sign that can't show now (not noon, not dark, no anima tree)
    /// waits a few hours and lets go. And Smoulder turning in his sleep where the colony mines deep (<see cref="Deep"/>).
    /// </summary>
    public static partial class GodOmens
    {
        private static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

        private static bool Noon(Map map) => map.skyManager.CurSkyGlow > 0.65f;

        private static bool Night(Map map) => map.skyManager.CurSkyGlow < 0.3f;

        internal static List<Thing> AnimaTrees(Map map)
        {
            var def = Thing("Plant_TreeAnima");
            return def == null ? new List<Thing>() : map.listerThings.ThingsOfDef(def).Where(t => t.Spawned).ToList();
        }

        internal static List<Pawn> Dryads(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && !p.Downed && p.def.defName.StartsWith("Dryad_", StringComparison.Ordinal)).ToList();

        private static Building Console(Map map) => map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b is Building_CommsConsole && b.Spawned);

        /// <summary>The colony's powered lights: lit (for a flicker), or dark for want of power or a switch (for one that glows anyway).</summary>
        internal static List<ThingWithComps> Powered(Map map, bool lit) =>
            map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Where(b => b.Spawned && b.GetComp<CompPowerTrader>() != null
                                                                                        && b.GetComp<CompGlower>() is CompGlower g && g.Glows == lit).ToList();

        /// <summary>A closed room of the colony's on the home ground, of a size for threads on its floor.</summary>
        private static Room ShutRoom(Map map) =>
            map.regionGrid.AllRooms.Where(r => r != null && !r.PsychologicallyOutdoors && !r.IsHuge && r.CellCount >= 6 && r.CellCount <= 160 && !r.Fogged
                                               && map.areaManager.Home[r.Cells.First()]).OrderBy(r => r.Role == RoomRoleDefOf.Bedroom ? 1 : 0).ThenBy(_ => Rand.Value)
                .FirstOrDefault();

        /// <summary>The most desperate of the colony awake: the lowest spirits, under four in ten.</summary>
        internal static Pawn Desperate(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && (p.needs?.mood?.CurLevelPercentage ?? 1f) < 0.4f).OrderBy(p => p.needs.mood.CurLevelPercentage)
                .FirstOrDefault();

        /// <summary>A cell under the mountain's thick rock, near where the colony is, or none.</summary>
        internal static IntVec3 UnderMountain(Map map)
        {
            var thick = RoofDefOf.RoofRockThick;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
                if (p.Position.GetRoof(map) == thick)
                    return p.Position;
            for (int i = 0; i < 200; i++)
            {
                var c = CellFinder.RandomCell(map);
                if (c.GetRoof(map) == thick && c.Standable(map) && !c.Fogged(map)) return c;
            }
            return IntVec3.Invalid;
        }

        private static bool CanOwn(Map map, OmenKind k)
        {
            switch (k)
            {
                case OmenKind.AnimaHum: return AnimaTrees(map).Count > 0;
                case OmenKind.DryadsStill: return Dryads(map).Count > 0;
                case OmenKind.Rumble: return UnderMountain(map).IsValid;
                case OmenKind.Threads: return ShutRoom(map) != null;
                case OmenKind.Flicker: return Powered(map, true).Count > 0;
                case OmenKind.Static: return Console(map) != null;
                case OmenKind.Unpowered: return Powered(map, false).Count > 0;
                case OmenKind.Whisper: return map.mapPawns.FreeColonistsSpawned.Count > 0;
                case OmenKind.Croak: return Wet(map).IsValid;
                case OmenKind.Beetle: return Table(map) != null;
                default: return true;
            }
        }

        /// <summary>Shows one of a god's own signs now: where it is, and who saw it. False when it can't show this hour.</summary>
        private static bool ShowOwn(God god, Map map, OmenKind kind, out IntVec3 at, out Pawn speaker)
        {
            at = IntVec3.Invalid;
            speaker = null;
            switch (kind)
            {
                case OmenKind.Drift:
                {
                    // Every leaf and seed on the land drifts toward one place, on no wind.
                    at = AnimaTrees(map).FirstOrDefault()?.Position ?? Open(map);
                    if (!at.IsValid) return false;
                    var to = at.ToVector3Shifted();
                    int thrown = 0;
                    foreach (var c in GenRadial.RadialCellsAround(at, 28f, false).Where(c => c.InBounds(map) && c.GetPlant(map) != null).InRandomOrder().Take(70))
                    {
                        var from = c.ToVector3Shifted();
                        float angle = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                        GodFx.Throw(DefDatabase<FleckDef>.GetNamedSilentFail(GodFx.OwnMote), from, map, Rand.Range(0.25f, 0.45f), new Color(0.55f, 0.85f, 0.4f), angle,
                            Rand.Range(0.8f, 1.6f), Rand.Range(-40f, 40f), Rand.Range(1.4f, 2.4f));
                        thrown++;
                    }
                    if (thrown < 5) return false;
                    speaker = Witness(map, at, 30f) ?? map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    return true;
                }
                case OmenKind.AnimaHum:
                {
                    var tree = AnimaTrees(map).FirstOrDefault();
                    if (tree == null) return false;
                    at = tree.Position;
                    for (int i = 0; i < 24; i++)
                        GodFx.Throw(DefDatabase<FleckDef>.GetNamedSilentFail(GodFx.OwnMote), at.ToVector3Shifted() + new Vector3(Rand.Range(-1.5f, 1.5f), 0f, Rand.Range(-1f, 1.5f)), map,
                            Rand.Range(0.3f, 0.55f), new Color(0.65f, 0.9f, 1f), Rand.Range(-20f, 20f), Rand.Range(0.2f, 0.5f), 0f, Rand.Range(1.5f, 2.5f));
                    // Loud enough to hear indoors: anyone near, wall or no wall.
                    var c0 = at;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(c0, 40f)).OrderBy(p => p.Position.DistanceToSquared(c0)).FirstOrDefault();
                    return true;
                }
                case OmenKind.DryadsStill:
                {
                    var dryads = Dryads(map);
                    if (dryads.Count == 0) return false;
                    at = dryads[0].Position;
                    var toward = AnimaTrees(map).FirstOrDefault()?.Position ?? at;
                    var look = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.BeholdJob);
                    if (look == null) return false;
                    foreach (var d in dryads)
                    {
                        try
                        {
                            var j = JobMaker.MakeJob(look, toward);
                            j.count = 1200;
                            j.expiryInterval = 1400;
                            d.jobs.StartJob(j, JobCondition.InterruptForced);
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("gods: a dryad standing still", e);
                        }
                    }
                    speaker = Witness(map, at, 25f);
                    return true;
                }
                case OmenKind.Shadow:
                case OmenKind.VastShadow:
                    if (!Noon(map) || GodSky.On(map, SkySign.Wings) || GodSky.On(map, SkySign.Vast)) return false;
                    at = GodSky.Pass(map, kind == OmenKind.Shadow ? SkySign.Wings : SkySign.Vast);
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    return at.IsValid;
                case OmenKind.Star:
                    if (!Night(map) || GodSky.On(map, SkySign.Star)) return false;
                    at = GodSky.Pass(map, SkySign.Star);
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    return at.IsValid && speaker != null;
                case OmenKind.Steam:
                {
                    // His breath: steam from the geysers, else from the ground at the foot of the rock, and warm air with it.
                    var vents = map.listerThings.ThingsOfDef(Thing("SteamGeyser") ?? ThingDefOf.SteamGeyser).Select(g => g.Position).Take(4).ToList();
                    if (vents.Count == 0)
                        for (int i = 0; i < 60 && vents.Count < 3; i++)
                        {
                            var c = CellFinder.RandomCell(map);
                            if (c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetEdifice(map)?.def.building?.isNaturalRock == true))
                                vents.Add(c);
                        }
                    if (vents.Count == 0) vents.Add(Open(map));
                    foreach (var v in vents.Where(v => v.IsValid))
                    {
                        for (int i = 0; i < 6; i++) FleckMaker.ThrowAirPuffUp(v.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map);
                        FleckMaker.ThrowSmoke(v.ToVector3Shifted(), map, 2.2f);
                        GenTemperature.PushHeat(v, map, 25f);
                    }
                    at = vents[0];
                    speaker = Witness(map, at, 30f);
                    return at.IsValid;
                }
                case OmenKind.Rumble:
                    at = UnderMountain(map);
                    if (!at.IsValid) return false;
                    Rumble(map, at);
                    speaker = Witness(map, at, 20f);
                    return true;
                case OmenKind.FairyRing:
                    return FairyRing(map, out at, out speaker);
                case OmenKind.Earth:
                {
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = at + GenRadial.RadialPattern[Rand.Range(2, 40)];
                        if (c.InBounds(map) && !c.Roofed(map)) FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(0.8f, 1.4f), new Color(0.36f, 0.27f, 0.18f, 0.6f));
                    }
                    return true;
                }
                case OmenKind.Threads:
                {
                    var room = ShutRoom(map);
                    if (room == null) return false;
                    at = room.Cells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(room.ExtentsClose.CenterCell)).FirstOrDefault();
                    if (!at.IsValid) return false;
                    GodSky.Stay(map, SkySign.Threads, at, GenDate.TicksPerDay, Mathf.Clamp(Mathf.Sqrt(room.CellCount) * 0.9f, 2.5f, 6f));
                    speaker = Witness(map, at, 10f);
                    return true;
                }
                case OmenKind.Flicker:
                {
                    var lights = Powered(map, true).OrderByDescending(l => Colonists(map, l.Position, 12f)).Take(4).ToList();
                    int n = lights.Sum(l => Play(map, l, 1500, true, false));
                    if (n == 0) return false;
                    at = lights[0].Position;
                    speaker = Witness(map, at, 15f);
                    return true;
                }
                case OmenKind.Static:
                {
                    var console = Console(map);
                    if (console == null) return false;
                    at = console.Position;
                    for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(console.TrueCenter(), map);
                    var c0 = at;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(c0, 12f)).OrderBy(p => p.Position.DistanceToSquared(c0)).FirstOrDefault();
                    return true;
                }
                case OmenKind.Unpowered:
                {
                    var dark = Powered(map, false).OrderByDescending(l => Colonists(map, l.Position, 15f)).FirstOrDefault();
                    if (dark == null || Play(map, dark, GenDate.TicksPerHour, false, true) == 0) return false;
                    at = dark.Position;
                    speaker = Witness(map, at, 15f);
                    return true;
                }
                case OmenKind.FrostWriting:
                    return Frost(map, out at, out speaker);
                case OmenKind.Silence:
                {
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(Awake).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    try
                    {
                        if (Find.CurrentMap == map) Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(14f, 0.6f, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: the silence", e);
                    }
                    Calm(map);
                    // Everyone stops for a breath.
                    foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && (p.CurJob == null || p.CurJob.def.casualInterruptible)).ToList())
                    {
                        var wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                        wait.expiryInterval = Rand.Range(150, 260);
                        p.jobs.StartJob(wait, JobCondition.InterruptForced, null, true);
                    }
                    return true;
                }
                case OmenKind.WhiteFigure:
                {
                    if (!Night(map) || GodSky.On(map, SkySign.Figure)) return false;
                    for (int i = 0; i < 30 && !at.IsValid; i++)
                    {
                        var c = CellFinder.RandomEdgeCell(map);
                        c += (map.Center - c).ToVector3().normalized.ToIntVec3() * 4;
                        if (c.InBounds(map) && !c.Fogged(map) && !c.Roofed(map)) at = c;
                    }
                    if (!at.IsValid) return false;
                    GodSky.Stay(map, SkySign.Figure, at, GenDate.TicksPerHour * 2, 6f);
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    return speaker != null;
                }
                case OmenKind.Dark:
                {
                    if (!Night(map)) return false;
                    var lit = Lights(map).OrderByDescending(l => Colonists(map, l.Position, 12f)).FirstOrDefault();
                    at = lit?.Position ?? Open(map);
                    if (!at.IsValid) return false;
                    GodSky.Stay(map, SkySign.Dark, at, GenDate.TicksPerHour, 7f);
                    if (lit != null) Gutter(map, lit, GenDate.TicksPerHour);
                    speaker = Witness(map, at, 15f);
                    return true;
                }
                case OmenKind.Whisper:
                    speaker = Desperate(map);
                    if (speaker == null) return false;
                    at = speaker.Position;
                    return true;
                case OmenKind.Croak:
                {
                    // A deep croaking from the wet ground or a well, felt in the chest: the water shivers.
                    at = Wet(map);
                    if (!at.IsValid) return false;
                    try
                    {
                        if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.15f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: the croaking", e);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        var c = at + GenRadial.RadialPattern[Rand.Range(0, 20)];
                        if (c.InBounds(map)) FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(0.5f, 0.9f), new Color(0.55f, 0.7f, 0.8f, 0.35f));
                    }
                    var c0 = at;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(c0, 35f)).OrderBy(p => p.Position.DistanceToSquared(c0)).FirstOrDefault();
                    return speaker != null;
                }
                case OmenKind.StillSun:
                {
                    // The sun stands still a moment too long: the shadows hold where they are for an hour and a half
                    // (WeatherEvent_StillSun), and those out of doors stop and look up.
                    if (map.skyManager.CurSkyGlow < 0.6f || !WeatherEvent_StillSun.Begin(map, GenDate.TicksPerHour * 3 / 2)) return false;
                    var outside = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).ToList();
                    if (outside.Count == 0) return false;
                    foreach (var p in outside.Where(p => p.CurJob == null || p.CurJob.def.casualInterruptible).Take(4).ToList())
                    {
                        var wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                        wait.expiryInterval = Rand.Range(200, 400);
                        p.jobs.StartJob(wait, JobCondition.InterruptForced, null, true);
                    }
                    speaker = outside.RandomElement();
                    at = speaker.Position;
                    return true;
                }
                case OmenKind.HeatHaze:
                {
                    // The air shimmering over the open ground near the colony's people, and warm.
                    if (map.skyManager.CurSkyGlow < 0.6f) return false;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    foreach (var c in GenRadial.RadialCellsAround(at, 14f, true).Where(c => c.InBounds(map) && !c.Roofed(map)).InRandomOrder().Take(30))
                    {
                        FleckMaker.ThrowHeatGlow(c, map, Rand.Range(0.8f, 1.4f));
                        if (Rand.Chance(0.4f)) FleckMaker.ThrowAirPuffUp(c.ToVector3Shifted(), map);
                    }
                    GenTemperature.PushHeat(at, map, 20f);
                    return true;
                }
                case OmenKind.BigMoon:
                {
                    // The moon very large and very close over the colony for a night, its light pooled over the land, as
                    // much as its face shows (none on a night with no moon: the sign waits).
                    float light = GodsOnMapMath.MoonLight(MoonTonight);
                    if (!Night(map) || light <= 0f || GodSky.On(map, SkySign.Moon)) return false;
                    var people = map.mapPawns.FreeColonistsSpawned;
                    if (people.Count == 0) return false;
                    at = new IntVec3((int)people.Average(p => p.Position.x), 0, (int)people.Average(p => p.Position.z));
                    GodSky.Stay(map, SkySign.Moon, at, GenDate.TicksPerHour * 7, Mathf.Max(map.Size.x, map.Size.z) * (0.4f + 0.3f * light));
                    speaker = people.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    return speaker != null;
                }
                case OmenKind.Glint:
                {
                    // Gold in the corner of someone's eye, and gone when they turn.
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(Awake).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    var loc = speaker.DrawPos + new Vector3(Rand.Range(-2.5f, 2.5f), 0f, Rand.Range(-2.5f, 2.5f));
                    GodFx.Throw(DefDatabase<FleckDef>.GetNamedSilentFail(GodFx.OwnMote), loc, map, 0.35f, new Color(1f, 0.85f, 0.35f), 0f, 0.05f, 0f, 0.4f);
                    return true;
                }
                case OmenKind.Beetle:
                {
                    // A golden beetle walks across a table and off the edge.
                    var table = Table(map);
                    if (table == null) return false;
                    at = table.Position;
                    var c0 = table.Position;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(c0, 10f) && GenSight.LineOfSight(p.Position, c0, map, true))
                        .FirstOrDefault();
                    if (speaker == null) return false;
                    var mid = table.TrueCenter();
                    var along = table.Rotation.IsHorizontal ? new Vector3(0f, 0f, table.def.size.x * 0.45f) : new Vector3(table.def.size.x * 0.45f, 0f, 0f);
                    GodSky.Crawl(map, mid - along, mid + along, 22f);
                    return true;
                }
                case OmenKind.Coin:
                {
                    // A coin where none was dropped, at someone's feet.
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(Awake).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    try
                    {
                        var coin = ThingMaker.MakeThing(ThingDefOf.Gold);
                        coin.stackCount = 1;
                        if (!GenPlace.TryPlaceThing(coin, at, map, ThingPlaceMode.Near)) return false;
                        at = coin.Position;
                        FleckMaker.ThrowLightningGlow(coin.DrawPos, map, 0.4f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: a coin found", e);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Wet ground near the colony's people (a marsh, mud, shallow water) or a well of the colony's, or none.</summary>
        internal static IntVec3 Wet(Map map)
        {
            var well = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.Spawned && b.def.defName.IndexOf("Well", StringComparison.OrdinalIgnoreCase) >= 0);
            if (well != null) return well.Position;
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            var from = near?.Position ?? map.Center;
            foreach (var c in GenRadial.RadialCellsAround(from, 40f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                var t = c.GetTerrain(map);
                if (t == null) continue;
                string n = t.defName;
                if (n.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IsWater && !t.passability.Equals(Traversability.Impassable))
                    return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A table of the colony's someone could watch a beetle cross, or null.</summary>
        internal static Building Table(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.IsTable && !b.Position.Fogged(map)).OrderByDescending(b => Colonists(map, b.Position, 10f)).FirstOrDefault();

        /// <summary>The moon's face tonight (Rimfolk's own calendar: GodsOnMapMath.Face, one round a season).</summary>
        internal static GodsOnMapMath.MoonFace MoonTonight => GodsOnMapMath.Face(Find.TickManager.TicksAbs / GenDate.TicksPerDay);

        /// <summary>An open cell of the colony's land outdoors near its people, for a sign with nothing else to point at.</summary>
        internal static IntVec3 Open(Map map)
        {
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.Spawned);
            if (near != null && RCellFinder.TryFindRandomSpotJustOutsideColony(near, out var spot)) return spot;
            return CellFinder.TryFindRandomCellNear(map.Center, map, 30, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map), out var c0) ? c0 : IntVec3.Invalid;
        }

        /// <summary>The rock rumbling: the ground shakes, dust falls, loose stone on the floor (nothing falls on anyone).</summary>
        internal static void Rumble(Map map, IntVec3 at)
        {
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.6f);
                var rubble = Thing("Filth_RubbleRock");
                for (int i = 0; i < 10; i++)
                {
                    var c = at + GenRadial.RadialPattern[Rand.Range(0, 30)];
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(1f, 1.8f), new Color(0.6f, 0.58f, 0.52f, 0.7f));
                    if (rubble != null && Rand.Chance(0.4f)) FilthMaker.TryMakeFilth(c, map, rubble);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the rock rumbling", e);
            }
        }

        /// <summary>A ring of mushrooms overnight round something she wants noticed: a grave, a body left lying, else open ground.</summary>
        internal static bool FairyRing(Map map, out IntVec3 at, out Pawn speaker)
        {
            speaker = null;
            var grave = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b is Building_Grave && !b.Position.Roofed(map));
            var body = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).FirstOrDefault(c => c.Spawned && !c.Position.Roofed(map) && !c.Position.Fogged(map));
            at = grave?.Position ?? body?.Position ?? Open(map);
            if (!at.IsValid) return false;
            var kinds = new[] { "Glowstool", "Agarilux", "Bryolux" }.Select(Thing).Where(d => d?.plant != null).ToList();
            if (kinds.Count == 0) return false;
            int placed = 0;
            foreach (var (dx, dz) in GodPresenceMath.Ring(2))
            {
                var c = at + new IntVec3(dx, 0, dz);
                if (!c.InBounds(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || !c.Standable(map)) continue;
                var def = kinds.FirstOrDefault(k => k.CanEverPlantAt(c, map, true)) ?? kinds[0];
                if (map.fertilityGrid.FertilityAt(c) <= 0f) continue;
                try
                {
                    var plant = (Plant)ThingMaker.MakeThing(def);
                    plant.Growth = 1f;
                    GenSpawn.Spawn(plant, c, map);
                    placed++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a ring of mushrooms", e);
                }
            }
            if (placed < 4) return false;
            speaker = Witness(map, at, 25f) ?? map.mapPawns.FreeColonistsSpawned.Where(Awake).RandomElementWithFallback();
            return true;
        }

        /// <summary>Frost by a wall in lines that look like writing (outdoors, beside one of the colony's walls).</summary>
        private static bool Frost(Map map, out IntVec3 at, out Pawn speaker)
        {
            at = IntVec3.Invalid;
            speaker = null;
            var walls = map.listerBuildings.allBuildingsColonist.Where(b => b.def.building != null && !b.def.building.isNaturalRock && b.def.fillPercent >= 0.99f && b.def.size == IntVec2.One)
                .InRandomOrder().Take(60);
            foreach (var w in walls)
            {
                var c = w.Position + IntVec3.South;
                if (c.InBounds(map) && !c.Roofed(map) && c.Standable(map) && (c + new IntVec3(5, 0, 0)).InBounds(map))
                {
                    at = c;
                    break;
                }
            }
            if (!at.IsValid) return false;
            // Two short lines and a mark, like a word scratched in rime.
            for (int k = 0; k < 6; k++)
            {
                if (k % 3 != 2) Rime(map, at + new IntVec3(k, 0, 0));
                if (k % 2 == 0 && k < 5) Rime(map, at + new IntVec3(k, 0, -1));
            }
            Rime(map, at + new IntVec3(6, 0, -1));
            var c0 = at;
            speaker = Witness(map, at, 15f) ?? map.mapPawns.FreeColonistsSpawned.Where(Awake).OrderBy(p => p.Position.DistanceToSquared(c0)).FirstOrDefault();
            return true;
        }

        internal static void Rime(Map map, IntVec3 c)
        {
            if (!c.InBounds(map) || c.Roofed(map)) return;
            map.snowGrid.AddDepth(c, 0.55f);
            FleckMaker.ThrowAirPuffUp(c.ToVector3Shifted(), map);
        }

        // ================= Those who keep the night =================

        /// <summary>
        /// The moon notices those who keep the night (a god whose shape returns the day by night): in the small hours, a watch
        /// kept out of doors or someone asleep under the open sky moves its regard, unseen.
        /// </summary>
        internal static void NightWatch(GodsOnMapBook b, int now)
        {
            var moon = Gods.All.Where(g => g != null && GodShapes.Of(g).returns).ToList();
            if (moon.Count == 0) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || GenLocalDate.HourOfDay(map) != 2) continue;
                bool kept = map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && !p.Position.Roofed(map) && (p.Awake() || p.InBed() || p.CurJobDef == JobDefOf.LayDown));
                if (!kept) continue;
                foreach (var g in moon) Gods.AddFavour(g, map, 0.25f, null);
            }
        }

        // ================= The sleeper under the mountain =================

        /// <summary>
        /// Mining too deep under the mountain makes a sleeping god turn (GodWaking: a god whose shape's signs are the rock
        /// rumbling): in an hour one of the colony mines under thick rock, at a small chance that grows the longer since the
        /// last time (GodsOnMapMath.DeepNow), never within days of it.
        /// </summary>
        private static void Deep(GodsOnMapBook b, int now)
        {
            if (b.woken != null || now - b.lastDeep < GenDate.TicksPerDay * GodsOnMapMath.DeepGapDays) return;
            var god = GodWaking.Sleeper;
            if (god == null) return;
            var miner = GodWaking.Miner(out var map);
            if (miner == null || MapFacts.ThreatOn(map) || !Rand.Chance(GodsOnMapMath.DeepNow(GodPace.DaysSince(b.lastDeep, now)))) return;
            GodWaking.Turned(b, god, map, miner, now);
        }
    }
}
