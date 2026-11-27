using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The content round 3's happenings on the land that take a while (OwnHappeningsBook.land3): Morel's light lying over the
    /// wet ground until first light, Pip's sparks at a bench and the morning after, Selene's tide up the shore until first
    /// light. Nothing grows: the tide's cells are OwnHappeningsMath.TideCellsMost at most, and let go at first light.
    /// </summary>
    public class Round3LandState : IExposable
    {
        /// <summary>The light on the wet ground: where, until when (first light), who saw it in the night.</summary>
        public Map foxMap;
        public IntVec3 foxAt = IntVec3.Invalid;
        public int foxUntil = -1;
        public Pawn foxSeer;

        /// <summary>The night at the bench: the piece, where, its stage (1 the sparks, 2 waiting for the morning), until when, who saw it.</summary>
        public Map pipMap;
        public Thing pipPiece;
        public IntVec3 pipAt = IntVec3.Invalid;
        public int pipStage, pipUntil = -1;
        public Pawn pipSeer;

        /// <summary>The tide up the shore: the cells under the water, until when (first light), whether someone has seen it.</summary>
        public Map tideMap;
        public List<IntVec3> tide = new List<IntVec3>();
        public int tideUntil = -1;
        public bool tideSeen;

        public void ExposeData()
        {
            Scribe_References.Look(ref foxMap, "foxMap");
            Scribe_Values.Look(ref foxAt, "foxAt", IntVec3.Invalid);
            Scribe_Values.Look(ref foxUntil, "foxUntil", -1);
            Scribe_References.Look(ref foxSeer, "foxSeer");
            Scribe_References.Look(ref pipMap, "pipMap");
            Scribe_References.Look(ref pipPiece, "pipPiece");
            Scribe_Values.Look(ref pipAt, "pipAt", IntVec3.Invalid);
            Scribe_Values.Look(ref pipStage, "pipStage");
            Scribe_Values.Look(ref pipUntil, "pipUntil", -1);
            Scribe_References.Look(ref pipSeer, "pipSeer");
            Scribe_References.Look(ref tideMap, "tideMap");
            Scribe_Collections.Look(ref tide, "tide", LookMode.Value);
            Scribe_Values.Look(ref tideUntil, "tideUntil", -1);
            Scribe_Values.Look(ref tideSeen, "tideSeen");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tide ??= new List<IntVec3>();
                if (tide.Count > OwnHappeningsMath.TideCellsMost) tide.RemoveRange(OwnHappeningsMath.TideCellsMost, tide.Count - OwnHappeningsMath.TideCellsMost);
                if (foxMap == null) foxUntil = -1;
                if (pipMap == null || pipPiece == null || pipPiece.Destroyed) pipStage = 0;
            }
        }
    }

    public partial class OwnHappeningsBook
    {
        /// <summary>The content round 3's happenings on the land under way (Gods/OnMap/OwnRound3.cs).</summary>
        public Round3LandState land3 = new Round3LandState();

        /// <summary>Saved with the rest of the book (its ExposeData).</summary>
        internal void ExposeRound3()
        {
            Scribe_Deep.Look(ref land3, "land3");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) land3 ??= new Round3LandState();
        }
    }

    /// <summary>
    /// The content round 3's six happenings of the gods on the land (docs/content-round-3-events-gods.md, G1 to G6), each its
    /// own god's, in the frame of the gods' own happenings (OwnHappenings.cs; looked at in the second seven's hour, OwnSeven.cs):
    /// <list type="bullet">
    /// <item>Bellwether, the ring in the snow: at dawn after a frosty night, a newborn of the colony's herd born out in the snow
    /// is found alive and warm, the snow in a wide ring round it trodden flat.</item>
    /// <item>Helios, ripe at noon: at a hot clear summer noon a field nearly ripe is ripe all at once.</item>
    /// <item>Morel, the light on the wet ground: on a night after wet days a soft green light lies over wet or wooded ground
    /// near the colony, and by morning the soil there is rich.</item>
    /// <item>Pip, done in the night: in the small hours sparks at a bench where an unfinished piece lies, and by morning most
    /// of the work left on it is done.</item>
    /// <item>Selene, the full-moon tide: at the full moon the sea comes up the shore further than anyone has seen, over the
    /// empty sand, and goes back at first light.</item>
    /// <item>Cirrus, the giant in the mist: on a foggy morning on high ground, someone out in the open sees their own shadow
    /// huge on the mist with a ring of colours round its head.</item>
    /// </list>
    /// The gods stay unseen: a card's title names the god only where the colony knows it was that god (Gods.CardName), its
    /// words never do; what people say is their own guess (the frame's pool, unnamed); the colony's book keeps it in its
    /// witness's words. Each keeps the gods' pace (OwnHappeningsMath.Land3PaceOf) and comes only where its god acts at the
    /// home. None of them hurts anyone.
    /// </summary>
    public static class OwnRound3
    {
        private static int Now => Find.TickManager.TicksGame;

        private static God G(string key) => OwnHappenings.God(key);

        private static readonly Color White = new Color(0.95f, 0.95f, 0.9f), Gold = new Color(1f, 0.82f, 0.4f), Green = new Color(0.55f, 1f, 0.6f),
            Spark = new Color(0.6f, 1f, 0.75f), Silver = new Color(0.8f, 0.85f, 1f);

        /// <summary>The gods' hour at a home (the second seven's, OwnSeven.cs): each of the six looked at in its own hours.</summary>
        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            if (b?.land3 == null || map == null) return;
            if (!OwnHappenings.Quiet(map)) return;
            int hour = OwnHappenings.Hour(map);
            if (hour == OwnHappeningsMath.NewbornHour) OwnHappenings.Safe("the ring in the snow", () => NewbornLook(b, map, now));
            if (hour >= OwnHappeningsMath.RipenFrom && hour <= OwnHappeningsMath.RipenTo) OwnHappenings.Safe("ripe at noon", () => RipenLook(b, map, now));
            if (OwnHappeningsMath.FoxfireHour(hour) && b.land3.foxUntil < 0) OwnHappenings.Safe("the light on the wet ground", () => FoxfireLook(b, map, now));
            if (hour >= OwnHappeningsMath.FinishedFrom && hour <= OwnHappeningsMath.FinishedTo && b.land3.pipStage == 0) OwnHappenings.Safe("done in the night", () => FinishedLook(b, map, now));
            if (OwnHappeningsMath.TideHour(hour) && b.land3.tideUntil < 0) OwnHappenings.Safe("the full-moon tide", () => TideLook(b, map, now));
            if (hour >= OwnHappeningsMath.GloryFrom && hour <= OwnHappeningsMath.GloryTo) OwnHappenings.Safe("the giant in the mist", () => GloryLook(b, map, now));
        }

        /// <summary>The steps between the hours: the light on the wet ground, the sparks at the bench, the tide.</summary>
        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b?.land3;
            if (s == null) return;
            if (s.foxUntil >= 0) OwnHappenings.Safe("the light on the wet ground, its steps", () => FoxfireStep(b, now));
            if (s.pipStage > 0) OwnHappenings.Safe("done in the night, its steps", () => FinishedStep(b, now));
            if (s.tideUntil >= 0) OwnHappenings.Safe("the full-moon tide, its steps", () => TideStep(b, now));
        }

        /// <summary>Those of the colony who hold this god (at least leaning toward it, not in dread of it).</summary>
        private static int Holders(Map map, God g) =>
            g == null ? 0 : map.mapPawns.FreeColonistsSpawned.Count(p => OwnHappenings.Ours(p) && !Worship.Scoffer(p) && PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g));

        /// <summary>The tick of the next time the map's clock reads <paramref name="hour"/> o'clock.</summary>
        private static int NextHour(Map map, int hour)
        {
            int now = Now;
            int h = GenLocalDate.HourOfDay(map);
            int ahead = (hour - h + 24) % 24;
            if (ahead == 0) ahead = 24;
            return now + ahead * GenDate.TicksPerHour - (GenLocalDate.DayTick(map) % GenDate.TicksPerHour);
        }

        private static string Card(string what, params NamedArgument[] args) => TextVariants.Vary("CC_GodR3_" + what + "_Text").Translate(args).Resolve();

        // ================= G1. Bellwether: the ring in the snow =================

        /// <summary>A newborn of the colony's herd, born in the night out of doors in a frost, with snow lying round it.</summary>
        public static Pawn Newborn(Map map)
        {
            float temp = map.mapTemperature.OutdoorTemp;
            return map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed && a.ageTracker != null && !a.Position.Fogged(map)
                                                                && OwnHappeningsMath.NewbornFits(a.ageTracker.AgeBiologicalTicks / (float)GenDate.TicksPerHour, !a.Position.Roofed(map), temp,
                                                                    map.snowGrid.GetDepth(a.Position)))
                .OrderBy(a => a.ageTracker.AgeBiologicalTicks).FirstOrDefault();
        }

        private static void NewbornLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Bellwether");
            if (g == null || !OwnHappenings.Acts(g, map)) return;
            var calf = Newborn(map);
            if (calf == null) return;
            float chance = OwnHappeningsMath.NewbornChance(OwnHappenings.DaysSince(b, OwnHappening.BellwetherNewborn, now), GodNear.Of(g, map)) * GodTouch.Turn(g, map);
            if (OwnHappenings.forced != OwnHappening.BellwetherNewborn && !Rand.Chance(chance)) return;
            Ring(b, g, map, calf, now);
        }

        /// <summary>The snow in a wide ring round the newborn trodden flat, the newborn warm; the first to see it says so.</summary>
        public static bool Ring(OwnHappeningsBook b, God g, Map map, Pawn calf, int now)
        {
            if (calf == null || !calf.Spawned) return false;
            int cleared = 0;
            foreach (var c in GenRadial.RadialCellsAround(calf.Position, OwnHappeningsMath.NewbornRing, true))
            {
                if (!c.InBounds(map) || c.Roofed(map)) continue;
                if (map.snowGrid.GetDepth(c) > 0f) cleared++;
                map.snowGrid.SetDepth(c, 0f);
            }
            var cold = calf.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
            if (cold != null) calf.health.RemoveHediff(cold);
            OwnHappenings.Glow(map, calf.Position, White, 6, 1.6f, 0.45f);
            OwnHappenings.Came(b, OwnHappening.BellwetherNewborn, now, cleared > 0 ? "ring" : "warm");
            string beast = calf.KindLabel;
            OwnHappenings.CardOf(g, map, null, "CC_GodR3_Newborn_Title", "CC_GodR3_Newborn_TitleNamed",
                Card("Newborn", beast.Named("KIND"), Homes.Name(map).Named("HOME")), new LookTargets(calf), LetterDefOf.PositiveEvent);
            OwnHappenings.Found(b, map, calf.Position, "r3newborn", g, beast, GodActKind.Blessing, "CC_GodR3_Saw_Newborn", "BEAST", beast);
            return true;
        }

        // ================= G2. Helios: ripe at noon =================

        /// <summary>The colony's field nearest ripe (out in the open, four fifths grown or more on average, none of it ripe yet), and how far grown.</summary>
        public static Zone_Growing NearRipe(Map map, out float mean, out bool anyRipe)
        {
            mean = 0f;
            anyRipe = false;
            Zone_Growing best = null;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                var plants = Crop(z, map);
                if (plants.Count < 6) continue;
                float m = plants.Average(p => p.Growth);
                bool ripe = plants.Any(p => p.HarvestableNow);
                if (best != null && (ripe && !anyRipe || m <= mean)) continue;
                best = z;
                mean = m;
                anyRipe = ripe;
            }
            return best;
        }

        private static List<Plant> Crop(Zone_Growing z, Map map)
        {
            var want = z.GetPlantDefToGrow();
            var list = new List<Plant>();
            foreach (var c in z.Cells)
                if (c.GetPlant(map) is Plant p && p.def == want && p.def.plant != null && p.def.plant.Harvestable && !c.Roofed(map))
                    list.Add(p);
            return list;
        }

        private static void RipenLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Helios");
            if (g == null) return;
            var season = GenLocalDate.Season(map);
            var z = NearRipe(map, out float mean, out bool anyRipe);
            var t = OwnHappeningsMath.RipenTrigger(season == Season.Summer || season == Season.PermanentSummer, OwnHappenings.Clear(map), map.mapTemperature.OutdoorTemp,
                z == null ? 0f : mean, anyRipe, Holders(map, g));
            if (z == null || !OwnHappenings.Calls(b, OwnHappening.HeliosRipen, t, g, map, now)) return;
            Ripen(b, g, map, z, now);
        }

        /// <summary>The whole field ripe at once in the noon heat, heat shimmering over it; the first to see it says so.</summary>
        public static bool Ripen(OwnHappeningsBook b, God g, Map map, Zone_Growing z, int now)
        {
            var plants = z == null ? new List<Plant>() : Crop(z, map).Where(p => p.Growth < 1f).ToList();
            if (plants.Count == 0) return false;
            int n = 0;
            foreach (var p in plants)
            {
                p.Growth = 1f;
                p.DirtyMapMesh(map);
                if (n++ % 4 == 0) FleckMaker.ThrowHeatGlow(p.Position, map, 1.2f);
            }
            var mid = z.Cells[z.Cells.Count / 2];
            OwnHappenings.Motes(map, mid.ToVector3Shifted(), OwnHappenings.Heat, Gold, 8, 2f);
            OwnHappenings.Came(b, OwnHappening.HeliosRipen, now);
            string crop = plants[0].def.label;
            OwnHappenings.CardOf(g, map, null, "CC_GodR3_Ripen_Title", "CC_GodR3_Ripen_TitleNamed",
                Card("Ripen", z.label.Named("FIELD"), crop.Named("CROP"), Homes.Name(map).Named("HOME")), new LookTargets(new TargetInfo(mid, map)), LetterDefOf.PositiveEvent);
            OwnHappenings.Found(b, map, mid, "r3ripen", g, crop, GodActKind.Blessing, "CC_GodR3_Saw_Ripen", "FIELD", z.label);
            return true;
        }

        // ================= G3. Morel: the light on the wet ground =================

        private static bool TurnsRich(TerrainDef t) => t != null && (t.defName == "Soil" || t.defName == "MossyTerrain" || t.defName == "GrasslandSoil");

        private static List<IntVec3> SoilAround(Map map, IntVec3 at) =>
            GenRadial.RadialCellsAround(at, 2.9f, true).Where(c => c.InBounds(map) && !c.Roofed(map) && TurnsRich(c.GetTerrain(map)) && c.GetEdifice(map) == null).ToList();

        /// <summary>
        /// A patch of wet ground (marsh, mud) or of ground under trees, out of the colony's home ground but near it, with soil
        /// beside it that can turn rich.
        /// </summary>
        public static IntVec3 Patch(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            foreach (var c in GenRadial.RadialCellsAround(mid, 45f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || map.areaManager.Home[c] || c.DistanceTo(mid) < 15f) continue;
                var t = c.GetTerrain(map);
                if (t == null || t.IsWater) continue;
                bool wet = HappeningWorker_R3MarshPath.Wet(t);
                bool wood = !wet && TurnsRich(t) && GenRadial.RadialDistinctThingsAround(c, map, 3f, true).Count(x => x is Plant p && p.def.plant != null && p.def.plant.IsTree) >= 3;
                if ((wet || wood) && SoilAround(map, c).Count >= 3) return c;
            }
            return IntVec3.Invalid;
        }

        private static bool Rotting(Map map, IntVec3 at) =>
            GenRadial.RadialDistinctThingsAround(at, map, 8f, true).Any(t => t is Corpse c && c.GetRotStage() != RotStage.Fresh);

        private static void FoxfireLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Morel");
            // The wet run and the warmth first: the patch is only looked for on such a night.
            if (g == null || OwnHappenings.WetRun(map) < OwnHappeningsMath.FoxfireWetDays || map.mapTemperature.OutdoorTemp <= OwnHappeningsMath.FoxfireAbove) return;
            var at = Patch(map);
            var t = OwnHappeningsMath.FoxfireTrigger(OwnHappenings.WetRun(map), map.mapTemperature.OutdoorTemp, at.IsValid, at.IsValid && Rotting(map, at));
            if (!OwnHappenings.Calls(b, OwnHappening.MorelFoxfire, t, g, map, now)) return;
            BeginFoxfire(b, map, at, now);
        }

        public static bool BeginFoxfire(OwnHappeningsBook b, Map map, IntVec3 at, int now)
        {
            var s = b.land3;
            if (!at.IsValid || s.foxUntil >= 0) return false;
            s.foxMap = map;
            s.foxAt = at;
            s.foxSeer = null;
            s.foxUntil = NextHour(map, OwnHappeningsMath.FoxfireDawn);
            OwnHappenings.Glow(map, at, Green, 8, 1.8f, 0.45f);
            OwnHappenings.Came(b, OwnHappening.MorelFoxfire, now);
            b.Due(now + 250);
            return true;
        }

        private static void FoxfireStep(OwnHappeningsBook b, int now)
        {
            var s = b.land3;
            var map = s.foxMap;
            var g = G("Morel");
            if (map == null || !map.IsPlayerHome || !s.foxAt.IsValid)
            {
                s.foxUntil = -1;
                s.foxMap = null;
                return;
            }
            if (now >= s.foxUntil)
            {
                Dawn(b, g, map, now);
                return;
            }
            OwnHappenings.Glow(map, s.foxAt, Green, 3, 1.8f, 0.4f);
            if (s.foxSeer == null)
            {
                var seer = OwnHappenings.Seer(map, s.foxAt, 35f);
                if (seer != null && OwnHappenings.Ours(seer))
                {
                    s.foxSeer = seer;
                    OwnHappenings.Say(seer, "r3foxfire", g, null, StorytellerEvents.Direction(map, s.foxAt).Named("WHAT"));
                    OwnHappenings.Line(map, Card("FoxfireNight", seer.Named("PAWN"), StorytellerEvents.Direction(map, s.foxAt).Named("PLACE")), new LookTargets(new TargetInfo(s.foxAt, map)));
                    if (g != null) OwnHappenings.Saw(g, map, s.foxAt, GodActKind.Omen, seer, "CC_GodR3_Saw_Foxfire", StorytellerEvents.Direction(map, s.foxAt).Named("PLACE"));
                }
            }
            b.Due(now + 300);
        }

        /// <summary>First light: the light is gone, and the soil where it lay is rich; the first to see it says so.</summary>
        private static void Dawn(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.land3;
            var at = s.foxAt;
            s.foxUntil = -1;
            s.foxMap = null;
            s.foxSeer = null;
            var rich = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
            if (rich == null || g == null) return;
            int n = 0;
            foreach (var c in SoilAround(map, at).OrderBy(c => c.DistanceToSquared(at)).Take(OwnHappeningsMath.FoxfireSoilMost))
            {
                map.terrainGrid.SetTerrain(c, rich);
                n++;
            }
            if (n == 0) return;
            string place = StorytellerEvents.Direction(map, at);
            OwnHappenings.CardOf(g, map, null, "CC_GodR3_Foxfire_Title", "CC_GodR3_Foxfire_TitleNamed", Card("Foxfire", place.Named("PLACE"), Homes.Name(map).Named("HOME")),
                new LookTargets(new TargetInfo(at, map)), LetterDefOf.PositiveEvent);
            OwnHappenings.Found(b, map, at, "r3richsoil", g, place, GodActKind.Blessing, "CC_GodR3_Saw_RichSoil", "PLACE", place);
        }

        // ================= G4. Pip: done in the night =================

        /// <summary>An unfinished piece lying at a bench of the colony's (on its working spot), with nobody at work on it, and the bench.</summary>
        public static UnfinishedThing Piece(Map map, out Building_WorkTable bench)
        {
            bench = null;
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                if (!b.Spawned || !b.def.hasInteractionCell || !b.InteractionCell.InBounds(map) || b.InteractionCell.Fogged(map)) continue;
                var t = b.InteractionCell.GetThingList(map).OfType<UnfinishedThing>().FirstOrDefault(x => x.Spawned && !x.Destroyed && x.workLeft > 1f);
                if (t == null) continue;
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJob != null && (p.CurJob.targetA.Thing == t || p.CurJob.targetB.Thing == t || p.CurJob.targetA.Thing == b))) continue;
                bench = b;
                return t;
            }
            return null;
        }

        private static string What(UnfinishedThing t) => t?.Recipe?.ProducedThingDef?.label ?? t?.LabelNoCount ?? "";

        private static void FinishedLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Pip");
            if (g == null) return;
            var piece = Piece(map, out var bench);
            var maker = piece?.Creator;
            var t = OwnHappeningsMath.FinishedTrigger(piece != null, piece != null, maker != null && PawnGods.HoldOf(maker, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(maker, g));
            if (piece == null || !OwnHappenings.Calls(b, OwnHappening.PipFinished, t, g, map, now)) return;
            BeginFinished(b, map, piece, bench, now);
        }

        public static bool BeginFinished(OwnHappeningsBook b, Map map, UnfinishedThing piece, Building_WorkTable bench, int now)
        {
            var s = b.land3;
            if (piece == null || s.pipStage != 0) return false;
            s.pipMap = map;
            s.pipPiece = piece;
            s.pipAt = bench != null ? bench.Position : piece.Position;
            s.pipStage = 1;
            s.pipUntil = now + GenDate.TicksPerHour;
            s.pipSeer = null;
            FleckMaker.ThrowMicroSparks(piece.TrueCenter(), map);
            OwnHappenings.Came(b, OwnHappening.PipFinished, now);
            b.Due(now + 250);
            return true;
        }

        private static void FinishedStep(OwnHappeningsBook b, int now)
        {
            var s = b.land3;
            var map = s.pipMap;
            var piece = s.pipPiece as UnfinishedThing;
            var g = G("Pip");
            if (map == null || !map.IsPlayerHome || piece == null || piece.Destroyed || !piece.Spawned || piece.Map != map)
            {
                s.pipStage = 0;
                s.pipPiece = null;
                s.pipMap = null;
                return;
            }
            if (s.pipStage == 1)
            {
                // The sparks and a light at the bench for an hour.
                if (Rand.Chance(0.7f)) FleckMaker.ThrowMicroSparks(piece.TrueCenter(), map);
                OwnHappenings.Glow(map, s.pipAt, Spark, 2, 0.8f, 0.35f);
                if (s.pipSeer == null)
                {
                    var seer = OwnHappenings.Seer(map, s.pipAt, 25f);
                    if (seer != null && OwnHappenings.Ours(seer))
                    {
                        s.pipSeer = seer;
                        OwnHappenings.Say(seer, "r3pipnight", g, null, What(piece).Named("WHAT"));
                        if (g != null) OwnHappenings.Saw(g, map, s.pipAt, GodActKind.Omen, seer, "CC_GodR3_Saw_PipNight", What(piece).Named("THING"));
                    }
                }
                if (now >= s.pipUntil)
                {
                    piece.workLeft = OwnHappeningsMath.FinishedWork(piece.workLeft);
                    s.pipStage = 2;
                    s.pipUntil = NextHour(map, OwnHappeningsMath.FinishedMorning);
                }
                b.Due(now + 250);
                return;
            }
            if (now < s.pipUntil)
            {
                b.Due(Math.Min(s.pipUntil, now + GenDate.TicksPerHour));
                return;
            }
            // The morning: how far it has come.
            string what = What(piece);
            s.pipStage = 0;
            s.pipPiece = null;
            s.pipMap = null;
            if (g == null) return;
            OwnHappenings.CardOf(g, map, null, "CC_GodR3_Finished_Title", "CC_GodR3_Finished_TitleNamed", Card("Finished", what.Named("THING"), Homes.Name(map).Named("HOME")),
                new LookTargets(piece), LetterDefOf.PositiveEvent);
            OwnHappenings.Found(b, map, piece.Position, "r3finished", g, what, GodActKind.Blessing, "CC_GodR3_Saw_Finished", "THING", what);
        }

        // ================= G5. Selene: the full-moon tide =================

        private static bool Sea(TerrainDef t) => t != null && t.IsWater && t.defName.IndexOf("Ocean", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Empty shore the sea can come up over: within reach of the sea, nothing on it at all, no floor or temporary ground (the
        /// game's ice), out of the colony's home ground, the nearest the colony first; OwnHappeningsMath.TideCellsMost at most.
        /// </summary>
        public static List<IntVec3> Shore(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            var sea = new List<IntVec3>();
            foreach (var c in GenRadial.RadialCellsAround(mid, 50f, true))
                if (c.InBounds(map) && Sea(c.GetTerrain(map)) && GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && !n.GetTerrain(map).IsWater))
                    sea.Add(c);
            var cells = new HashSet<IntVec3>();
            foreach (var w in sea.Take(80))
                foreach (var c in GenRadial.RadialCellsAround(w, OwnHappeningsMath.TideReach, false))
                {
                    if (!c.InBounds(map) || cells.Contains(c) || c.Fogged(map) || c.Roofed(map) || map.areaManager.Home[c]) continue;
                    var t = c.GetTerrain(map);
                    if (t == null || t.IsWater || t.layerable || map.terrainGrid.TempTerrainAt(c) != null || c.GetThingList(map).Count > 0) continue;
                    cells.Add(c);
                }
            return cells.OrderBy(c => c.DistanceToSquared(mid)).Take(OwnHappeningsMath.TideCellsMost).ToList();
        }

        private static TerrainDef TideWater =>
            DefDatabase<TerrainDef>.GetNamedSilentFail("ShallowFloodwater") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("WaterOceanShallow") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("WaterShallow");

        private static void TideLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Selene");
            if (g == null || GodOmens.MoonTonight != GodsOnMapMath.MoonFace.Full || !Lands.Of(map).Contains("coast")) return;
            var cells = Shore(map);
            var t = OwnHappeningsMath.TideTrigger(true, true, cells.Count);
            if (!OwnHappenings.Calls(b, OwnHappening.SeleneTide, t, g, map, now)) return;
            BeginTide(b, map, cells, now);
        }

        public static bool BeginTide(OwnHappeningsBook b, Map map, List<IntVec3> cells, int now)
        {
            var s = b.land3;
            var water = TideWater;
            if (water == null || s.tideUntil >= 0 || cells.Count < OwnHappeningsMath.TideCellsFrom) return false;
            s.tide.Clear();
            foreach (var c in cells.Take(OwnHappeningsMath.TideCellsMost))
            {
                try
                {
                    map.terrainGrid.SetTempTerrain(c, water);
                    s.tide.Add(c);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the full-moon tide", e);
                    break;
                }
            }
            if (s.tide.Count == 0) return false;
            s.tideMap = map;
            s.tideSeen = false;
            s.tideUntil = NextHour(map, OwnHappeningsMath.TideBack);
            foreach (var c in s.tide.InRandomOrder().Take(4)) FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.4f, 2f);
            OwnHappenings.Came(b, OwnHappening.SeleneTide, now);
            b.Due(now + 250);
            return true;
        }

        private static IntVec3 TideMiddle(Round3LandState s) => s.tide.Count == 0 ? IntVec3.Invalid : s.tide[s.tide.Count / 2];

        private static void TideStep(OwnHappeningsBook b, int now)
        {
            var s = b.land3;
            var map = s.tideMap;
            var g = G("Selene");
            if (map == null || !map.IsPlayerHome || s.tide.Count == 0)
            {
                s.tideUntil = -1;
                s.tideMap = null;
                s.tide.Clear();
                return;
            }
            var mid = TideMiddle(s);
            if (Rand.Chance(0.5f)) OwnHappenings.Glow(map, s.tide.RandomElement(), Silver, 2, 1f, 0.4f);
            if (!s.tideSeen)
            {
                var seer = OwnHappenings.Seer(map, mid, 40f);
                if (seer != null && OwnHappenings.Ours(seer) && g != null)
                {
                    s.tideSeen = true;
                    string place = StorytellerEvents.Direction(map, mid);
                    OwnHappenings.Say(seer, "r3tide", g, null, place.Named("WHAT"));
                    OwnHappenings.CardOf(g, map, null, "CC_GodR3_Tide_Title", "CC_GodR3_Tide_TitleNamed", Card("Tide", seer.Named("PAWN"), place.Named("PLACE"), Homes.Name(map).Named("HOME")),
                        new LookTargets(new TargetInfo(mid, map)), LetterDefOf.NeutralEvent);
                    OwnHappenings.Saw(g, map, mid, GodActKind.Omen, seer, "CC_GodR3_Saw_Tide", place.Named("PLACE"));
                }
            }
            if (now >= s.tideUntil)
            {
                Ebb(s, map);
                return;
            }
            b.Due(now + 400);
        }

        /// <summary>First light: the sea goes back, and the shore is as it was.</summary>
        private static void Ebb(Round3LandState s, Map map)
        {
            foreach (var c in s.tide)
            {
                try
                {
                    if (c.InBounds(map) && map.terrainGrid.TempTerrainAt(c) != null) map.terrainGrid.RemoveTempTerrain(c);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the tide going back", e);
                }
            }
            if (s.tideSeen) OwnHappenings.Line(map, Card("TideBack", StorytellerEvents.Direction(map, TideMiddle(s)).Named("PLACE")), new LookTargets(new TargetInfo(TideMiddle(s), map)));
            s.tide.Clear();
            s.tideUntil = -1;
            s.tideMap = null;
            s.tideSeen = false;
        }

        // ================= G6. Cirrus: the giant in the mist =================

        private static bool Fog(Map map) => map.weatherManager?.curWeather?.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool High(Map map)
        {
            var lands = Lands.Of(map);
            return lands.Contains("mountains") || lands.Contains("hills");
        }

        /// <summary>One of the colony out in the open now and free to stop and look; the one who watches the sky first.</summary>
        public static Pawn OutInTheOpen(OwnHappeningsBook b, Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && GodOmens.Awake(p) && !p.Position.Roofed(map) && !p.Position.Fogged(map))
                .OrderBy(p => p == b?.watcher ? 0 : 1).FirstOrDefault();

        private static void GloryLook(OwnHappeningsBook b, Map map, int now)
        {
            var g = G("Cirrus");
            if (g == null || !Fog(map) || !High(map)) return;
            var p = OutInTheOpen(b, map);
            bool watcher = p != null && (p == b.watcher || PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g));
            var t = OwnHappeningsMath.GloryTrigger(true, true, p != null, watcher);
            if (!OwnHappenings.Calls(b, OwnHappening.CirrusGlory, t, g, map, now)) return;
            Glory(b, g, map, p, now);
        }

        /// <summary>They stop and look: their own shadow huge on the mist, a ring of colours round its head (a ring of light round them).</summary>
        public static bool Glory(OwnHappeningsBook b, God g, Map map, Pawn p, int now)
        {
            if (p == null || !p.Spawned) return false;
            var away = p.Position + (p.Position - OwnHappenings.Middle(map));
            OwnHappenings.Stand(p, away.ClampInsideMap(map), GenDate.TicksPerHour / 2);
            var mote = GodFx.MoteDef;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var at = p.DrawPos + new Vector3(Mathf.Cos(a) * 1.3f, 0f, Mathf.Sin(a) * 1.3f);
                var hue = Color.HSVToRGB(i / 12f, 0.45f, 1f);
                GodFx.Throw(mote, at, map, 0.4f, hue, 0f, 0f, 0f, 2.5f);
            }
            OwnHappenings.Came(b, OwnHappening.CirrusGlory, now);
            OwnHappenings.Say(p, "r3glory", g);
            OwnHappenings.CardOf(g, map, null, "CC_GodR3_Glory_Title", "CC_GodR3_Glory_TitleNamed", Card("Glory", p.Named("PAWN"), Homes.Name(map).Named("HOME")), new LookTargets(p),
                LetterDefOf.NeutralEvent);
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, "CC_GodR3_Saw_Glory");
            return true;
        }
    }
}
