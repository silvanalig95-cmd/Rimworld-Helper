using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A mountain that wakes (the player's pick D2, 28 November 2026; docs/design/living-land-ground.md). Rarely, only on a
    /// land of mountains or lava (GroundMath.LandOf: the tile's hills, its biome, its features by name: Odyssey's lava field
    /// and lava features, Alpha Biomes' pyroclastic land and magma vents, Geological Landforms' caldera and lone mountain,
    /// Vanilla Landmarks Expanded's volcano and calderas), once in many years (GroundMath.WakeChancePerDay), the mountain
    /// stirs:
    /// <list type="number">
    /// <item>tremors for days: the ground shakes a few times a day, dust and stones come off the rock faces near the
    /// mountain, and a stone may strike someone standing at a face (the shared harm budget, HarmKinds.Ground); wild animals
    /// leave the land; as the tremors end, one or two warm vents open (the game's steam geysers);</item>
    /// <item>ash: smoke over the mountain, ash coming down over the land, the game's volcanic winter for some days and,
    /// with Odyssey, its volcanic ash in the air at the height of it;</item>
    /// <item>then, some days after the ash stopped, the ground where it lay thickest turns rich (Vanilla Landmarks
    /// Expanded's rich volcanic soil where it is, else the game's rich soil), a few dozen cells at most.</item>
    /// </list>
    /// Odyssey's own volcanic ash, when it settles on a home, leaves the same rich soil. A sleeper that turned lately under
    /// the colony's deep workings (Gods/OnMap/GodWaking.cs) makes the mountain wake more readily. Believers may take it for
    /// Smoulder's doing and say so (Gods/GodHands.cs: their words only, maybe wrong); nothing says it was.
    /// </summary>
    public static class GroundWaking
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "A mountain that wakes".</summary>
        public static bool On => S?.wakingMountain ?? true;

        private const int HourTicks = GroundMath.HourTicks, DayTicks = GroundMath.DayTicks;

        // ================= The land =================

        /// <summary>Which land the map is, for the waking: the tile's hills, its biome, its features (mutators and landmark) by name.</summary>
        public static WakeLand LandOf(Map map)
        {
            var tile = map?.TileInfo;
            if (tile == null || MapFacts.InVacuum(map)) return WakeLand.None;
            bool mountainous = tile.hilliness >= Hilliness.Mountainous;
            bool lavaBiome = map.Biome?.defName != null && GroundMath.LavaBiomes.Contains(map.Biome.defName);
            bool lava = false, mountain = false;
            try
            {
                var mutators = tile.Mutators;
                if (mutators != null)
                    foreach (var m in mutators)
                    {
                        lava |= GroundMath.LavaFeature(m?.defName);
                        mountain |= GroundMath.MountainFeature(m?.defName);
                    }
                string landmark = tile.Landmark?.def?.defName;
                lava |= GroundMath.LavaFeature(landmark);
                mountain |= GroundMath.MountainFeature(landmark);
            }
            catch
            {
                // A tile with features the game can't list: by its hills and biome only.
            }
            return GroundMath.LandOf(mountainous, lavaBiome, lava, mountain);
        }

        /// <summary>The sleeper under the deep workings turned lately (Gods/OnMap/GodWaking.cs).</summary>
        private static bool Stirred(int now)
        {
            var b = GodsOnMapBook.Instance;
            return b != null && b.lastDeep > -900000 && GroundMath.DaysSince(b.lastDeep, now) < GroundMath.StirredDays;
        }

        // ================= The day, the hour, each look =================

        /// <summary>The day's look: whether the mountain wakes.</summary>
        internal static void Daily(GroundMap g, Map map, int now)
        {
            if (!On || g.stage != WakeStage.Quiet || g.ashDue >= 0) return;
            int day = now / DayTicks;
            if (g.wakeDay == day) return;
            g.wakeDay = day;
            var land = LandOf(map);
            if (land == WakeLand.None || MapFacts.ThreatOn(map)) return;
            if (GroundMath.Wakes(land, GenDate.DaysPassed, GroundMath.DaysSince(g.lastWake, now), Stirred(now), Rand.Value)) Begin(g, map, now);
        }

        /// <summary>The hour's look: Odyssey's own ash settling, and the ground turning where the ash lay.</summary>
        internal static void Hourly(GroundMap g, Map map, int now)
        {
            if (!On)
            {
                // Switched off: the mountain goes quiet and the ash is forgotten.
                if (g.stage != WakeStage.Quiet) g.stage = WakeStage.Quiet;
                g.ClearAsh();
                return;
            }
            var odysseyAsh = DefDatabase<GameConditionDef>.GetNamedSilentFail("VolcanicAsh");
            bool on = odysseyAsh != null && map.gameConditionManager.ConditionIsActive(odysseyAsh);
            if (g.odysseyAshOn && !on && g.stage == WakeStage.Quiet && g.ashDue < 0 && LayAsh(map, g, false))
                g.ashDue = now + GroundMath.StageHours(WakeStage.Settling, Rand.Value) * HourTicks;
            g.odysseyAshOn = on;
            if (g.ashDue >= 0 && now >= g.ashDue) TurnRich(g, map);
        }

        /// <summary>Each look while the mountain is awake: a tremor now and then, the vents, smoke and ash, the next stage.</summary>
        internal static void Look(GroundMap g, Map map, int now)
        {
            if (!On) return;
            if (g.stage == WakeStage.Tremors)
            {
                if (Rand.Chance(GroundMath.TremorChancePerLook(Sched.RareTicks))) Tremor(g, map, true);
                if (g.vents == 0 && now >= g.stageUntil - 12 * HourTicks)
                {
                    g.vents = 1;
                    Vents(g, map);
                }
                if (now >= g.stageUntil) BeginAsh(g, map, now);
                return;
            }
            if (g.stage != WakeStage.Ash) return;
            Smoke(g, map, 2);
            // Ash coming down over the land (Odyssey's ash in the air spreads its own).
            var odysseyAsh = DefDatabase<GameConditionDef>.GetNamedSilentFail("VolcanicAsh");
            if ((odysseyAsh == null || !map.gameConditionManager.ConditionIsActive(odysseyAsh)) && Rand.Chance(0.5f)
                && CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map), map, 30, out var cell))
                FilthMaker.TryMakeFilth(cell, map, AshFilth);
            if (now < g.stageUntil) return;
            g.stage = WakeStage.Settling;
            g.stageUntil = -1;
            g.ashDue = now + GroundMath.StageHours(WakeStage.Settling, Rand.Value) * HourTicks;
        }

        // ================= Waking =================

        /// <summary>The mountain wakes: where it is, the first tremor, the animals going, the letter. True when it did.</summary>
        public static bool Begin(GroundMap g, Map map, int now)
        {
            if (g == null || map == null || g.stage != WakeStage.Quiet) return false;
            g.wakeFrom = Mountain(map, out bool onRock);
            g.wakeOnRock = onRock;
            g.stage = WakeStage.Tremors;
            g.stageUntil = now + GroundMath.StageHours(WakeStage.Tremors, Rand.Value) * HourTicks;
            g.vents = 0;
            g.lastWake = now;
            g.ClearAsh();
            Tremor(g, map, false);
            AnimalsLeave(map, g.wakeFrom, now);
            string place = StorytellerEvents.Direction(map, g.wakeFrom);
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_Ground_Wake_Title".Translate().Resolve(),
                (onRock ? "CC_Ground_Wake_Text" : "CC_Ground_Wake_TextBeyond").Translate(place.Named("PLACE")).Resolve(), LetterDefOf.NegativeEvent,
                new LookTargets(new TargetInfo(g.wakeFrom, map)));
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Ground_Chron_Wake".Translate(place.Named("PLACE")).Resolve()));
            GroundMarks.SayNear(map, g.wakeFrom, "CC_Ground_Say_Tremor");
            GroundMarks.SomeSay(map, "Smoulder", HandKind.Sign, "CC_Ground_Hand_Tremors", 0.6f);
            return true;
        }

        /// <summary>
        /// Where the mountain is: the rock face of the map's own rock with the most rock behind it (a sample of the faces),
        /// else, with no rock on the map, the edge it lies beyond.
        /// </summary>
        private static IntVec3 Mountain(Map map, out bool onRock)
        {
            onRock = false;
            IntVec3 best = IntVec3.Invalid;
            int bestMass = 0;
            for (int i = 0; i < 300; i++)
            {
                if (!CellFinderLoose.TryGetRandomCellWith(c => FaceSeen(map, c), map, 50, out var c)) break;
                int mass = GenRadial.RadialCellsAround(c, 6f, false).Count(x => StorytellerEvents.NaturalRock(x, map));
                if (mass <= bestMass) continue;
                bestMass = mass;
                best = c;
                if (i >= 40 && bestMass >= 90) break;
            }
            if (best.IsValid && bestMass >= GroundMath.RockMass)
            {
                onRock = true;
                return best;
            }
            return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, 0f, out var edge) ? edge : map.Center;
        }

        /// <summary>A face of natural rock someone could see: rock, not fogged, with standable ground beside it.</summary>
        private static bool FaceSeen(Map map, IntVec3 c)
        {
            if (!StorytellerEvents.NaturalRock(c, map) || c.Fogged(map)) return false;
            for (int i = 0; i < 4; i++)
            {
                var n = c + GenAdj.CardinalDirections[i];
                if (n.InBounds(map) && n.Standable(map) && !n.Fogged(map)) return true;
            }
            return false;
        }

        /// <summary>A few wild animals leave the land when the shaking starts (not predators, nor the gods' own).</summary>
        private static void AnimalsLeave(Map map, IntVec3 from, int now)
        {
            try
            {
                foreach (var a in GodTouchEffects.WildGame(map).OrderBy(x => x.Position.DistanceToSquared(from)).Take(6).ToList())
                    a.mindState.exitMapAfterTick = now + Rand.Range(60, 2500);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground: the animals leaving", e);
            }
        }

        /// <summary>
        /// A tremor: the ground shakes, dust and stones come off rock faces near the mountain, and (after the first) a stone
        /// may strike someone of the colony standing right at a face, within the shared budget.
        /// </summary>
        internal static void Tremor(GroundMap g, Map map, bool stones)
        {
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(1.2f);
            }
            catch
            {
                // No camera to shake.
            }
            var rubble = ThingDefOf.Filth_RubbleRock;
            for (int i = 0; i < 6; i++)
            {
                IntVec3 face;
                bool found = g.wakeOnRock && g.wakeFrom.IsValid
                    ? CellFinder.TryFindRandomCellNear(g.wakeFrom, map, 30, c => FaceSeen(map, c), out face)
                    : CellFinderLoose.TryGetRandomCellWith(c => FaceSeen(map, c), map, 40, out face);
                if (!found) break;
                var below = GenAdj.CardinalDirections.Select(d => face + d).FirstOrDefault(n => n.InBounds(map) && n.Standable(map));
                if (!below.IsValid || below == default) continue;
                FleckMaker.ThrowDustPuffThick(below.ToVector3Shifted(), map, Rand.Range(1.2f, 2f), new Color(0.6f, 0.57f, 0.5f, 0.75f));
                if (Rand.Chance(0.5f)) FilthMaker.TryMakeFilth(below, map, rubble);
            }
            if (!stones) return;
            // Now and then someone says so (the first tremor has its own word with the letter).
            if (Rand.Chance(0.2f)) GroundMarks.SayNear(map, StorytellerEvents.Center(map), "CC_Ground_Say_Tremor");
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder().ToList())
            {
                if (p.Downed || p.InBed() || !GenAdj.AdjacentCells.Any(d => StorytellerEvents.NaturalRock(p.Position + d, map))) continue;
                if (!Harm.Roll(p, HarmKinds.Ground, GroundMath.StoneHurtChance)) continue;
                FleckMaker.ThrowDustPuffThick(p.DrawPos, map, 1.6f, new Color(0.6f, 0.57f, 0.5f, 0.8f));
                FilthMaker.TryMakeFilth(p.Position, map, rubble);
                if (Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Arm : HurtWhere.Torso, 2f, 6f, HarmKinds.Ground) != null)
                    Tell.Message(NewsKind.Land, Importance.Important, map, "CC_Ground_HurtStone".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
                break;
            }
        }

        /// <summary>As the tremors end, one or two warm vents open out on the land: the game's steam geysers, clear of the colony and of old vents.</summary>
        private static void Vents(GroundMap g, Map map)
        {
            var def = ThingDefOf.SteamGeyser;
            if (def == null) return;
            var clear = new GroundMarks.Clearance(map);
            bool Fits(IntVec3 c)
            {
                foreach (var cell in GenAdj.OccupiedRect(c, Rot4.North, def.size))
                    if (!clear.Open(cell)) return false;
                return !map.listerThings.ThingsOfDef(def).Any(x => x.Position.DistanceTo(c) < 12f);
            }
            Thing first = null;
            int n = GroundMath.Vents(Rand.Value);
            for (int i = 0; i < n; i++)
            {
                IntVec3 at;
                bool found = g.wakeOnRock && g.wakeFrom.IsValid && CellFinder.TryFindRandomCellNear(g.wakeFrom, map, 40, Fits, out at)
                             || CellFinderLoose.TryGetRandomCellWith(Fits, map, 1500, out at);
                if (!found) break;
                var vent = GenSpawn.Spawn(def, at, map, WipeMode.Vanish);
                if (vent == null) break;
                first ??= vent;
                for (int k = 0; k < 4; k++) FleckMaker.ThrowSmoke(vent.DrawPos, map, 1.5f);
            }
            if (first == null) return;
            string place = StorytellerEvents.Direction(map, first.Position);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_Ground_Vents".Translate(place.Named("PLACE")).Resolve(), new LookTargets(first), MessageTypeDefOf.NeutralEvent);
            GroundMarks.SayNear(map, first.Position, "CC_Ground_Say_Vent");
        }

        /// <summary>
        /// The ash: smoke over the mountain, ash lying thickest in a few patches, the game's volcanic winter for some days and,
        /// with Odyssey, its volcanic ash in the air; the letter, the chronicle, and a believer's word.
        /// </summary>
        private static void BeginAsh(GroundMap g, Map map, int now)
        {
            g.stage = WakeStage.Ash;
            g.stageUntil = now + GroundMath.StageHours(WakeStage.Ash, Rand.Value) * HourTicks;
            Start(map, "VolcanicWinter", GroundMath.WinterDays(Rand.Value));
            bool air = Start(map, "VolcanicAsh", GroundMath.AshAirDays(Rand.Value));
            LayAsh(map, g, true);
            Smoke(g, map, 8);
            string place = StorytellerEvents.Direction(map, g.wakeFrom);
            string text = (g.wakeOnRock ? "CC_Ground_Ash_Text" : "CC_Ground_Ash_TextBeyond").Translate(place.Named("PLACE")).Resolve();
            if (air) text += "\n\n" + "CC_Ground_Ash_Air".Translate().Resolve();
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_Ground_Ash_Title".Translate().Resolve(), text, LetterDefOf.NegativeEvent,
                new LookTargets(new TargetInfo(g.wakeFrom, map)));
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Ground_Chron_Ash".Translate(place.Named("PLACE")).Resolve()));
            GroundMarks.SayNear(map, StorytellerEvents.Center(map), "CC_Ground_Say_Ash");
            GroundMarks.SomeSay(map, "Smoulder", HandKind.Wrath, "CC_Ground_Hand_Ash", 0.8f);
        }

        /// <summary>One of the game's conditions by name for some days, if the game has it and it isn't on already. True when it began.</summary>
        private static bool Start(Map map, string defName, int days)
        {
            try
            {
                var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(defName);
                if (def == null || days <= 0 || map.gameConditionManager.ConditionIsActive(def)) return false;
                map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, days * DayTicks));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground: " + defName, e);
                return false;
            }
        }

        /// <summary>Smoke over the mountain (on the map in view only): at the rock face, or at the edge it lies beyond.</summary>
        private static void Smoke(GroundMap g, Map map, int puffs)
        {
            if (Find.CurrentMap != map || !g.wakeFrom.IsValid) return;
            for (int i = 0; i < puffs; i++)
                FleckMaker.ThrowSmoke(g.wakeFrom.ToVector3Shifted() + new Vector3(Rand.Range(-3f, 3f), 0f, Rand.Range(-3f, 3f)), map, Rand.Range(2.5f, 4.5f));
        }

        // ================= Where the ash lay =================

        private static ThingDef AshFilth => DefDatabase<ThingDef>.GetNamedSilentFail("Filth_VolcanicAsh") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Ash");

        /// <summary>The rich ground the ash leaves: Vanilla Landmarks Expanded's rich volcanic soil where it is, else the game's rich soil.</summary>
        private static TerrainDef RichSoil => DefDatabase<TerrainDef>.GetNamedSilentFail("VEE_VolcanicSoilRich") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");

        private static bool AshCell(Map map, IntVec3 c, TerrainDef rich)
        {
            if (!c.InBounds(map) || c.Roofed(map) || c.Fogged(map) || c.GetEdifice(map) != null) return false;
            var t = c.GetTerrain(map);
            return t != null && t != rich && GroundMath.AshTurns(t.natural, t.IsWater, t.IsFloor, t.fertility, rich.fertility);
        }

        /// <summary>
        /// The cells where the ash lies thickest: a few patches of open natural soil, towards the mountain when it is on the
        /// map, GroundMath.MaxCells at most; with <paramref name="fall"/>, ash laid on them. True when there are any.
        /// </summary>
        private static bool LayAsh(Map map, GroundMap g, bool fall)
        {
            var rich = RichSoil;
            if (rich == null) return false;
            int total = GroundMath.AshCells(Rand.Value), patches = GroundMath.AshPatches(Rand.Value);
            int each = Mathf.Max(1, total / patches);
            var cells = new List<IntVec3>(g.ash);
            for (int i = 0; i < patches && cells.Count < GroundMath.MaxCells; i++)
            {
                bool Fits(IntVec3 c) => AshCell(map, c, rich) && !cells.Contains(c);
                bool found = g.wakeOnRock && g.wakeFrom.IsValid
                    ? CellFinder.TryFindRandomCellNear(g.wakeFrom, map, 60, Fits, out var centre)
                    : CellFinderLoose.TryGetRandomCellWith(Fits, map, 600, out centre);
                if (!found) continue;
                foreach (var c in GenRadial.RadialCellsAround(centre, 4f, true).Where(Fits).Take(Mathf.Min(each, GroundMath.MaxCells - cells.Count)))
                    cells.Add(c);
            }
            g.ash = cells;
            if (fall)
            {
                var filth = AshFilth;
                if (filth != null)
                    foreach (var c in cells)
                        FilthMaker.TryMakeFilth(c, map, filth, 2);
            }
            return cells.Count > 0;
        }

        /// <summary>Where the ash lay thickest the ground turns rich (still open natural soil, nothing built on it since); told, and in the chronicle.</summary>
        internal static void TurnRich(GroundMap g, Map map)
        {
            var rich = RichSoil;
            var cells = g.ash.ToList();
            g.ClearAsh();
            if (g.stage == WakeStage.Settling) g.stage = WakeStage.Quiet;
            if (rich == null) return;
            IntVec3 first = IntVec3.Invalid;
            int n = 0;
            foreach (var c in cells)
            {
                if (!AshCell(map, c, rich)) continue;
                map.terrainGrid.SetTerrain(c, rich);
                if (!first.IsValid) first = c;
                n++;
            }
            if (n == 0) return;
            string place = StorytellerEvents.Direction(map, first);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_Ground_Rich".Translate(place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(first, map)),
                MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Ground_Chron_Rich".Translate(place.Named("PLACE")).Resolve()));
            GroundMarks.SayNear(map, first, "CC_Ground_Say_Rich");
        }

        // ================= For the dev tools =================

        /// <summary>The waking moved on to its next stage now (dev mode).</summary>
        internal static string DevNext(GroundMap g, Map map)
        {
            int now = Find.TickManager.TicksGame;
            switch (g.stage)
            {
                case WakeStage.Quiet:
                    return Begin(g, map, now) ? "the mountain wakes: tremors." : "it couldn't wake here.";
                case WakeStage.Tremors:
                    if (g.vents == 0)
                    {
                        g.vents = 1;
                        Vents(g, map);
                    }
                    BeginAsh(g, map, now);
                    return "the vents, and the ash.";
                case WakeStage.Ash:
                    g.stageUntil = now;
                    Look(g, map, now);
                    return "the ash has stopped; the ground where it lay turns in 5 to 9 days (this again turns it now).";
                default:
                    TurnRich(g, map);
                    return "the ground turned where the ash lay.";
            }
        }
    }
}
