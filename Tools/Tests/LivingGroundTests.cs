using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The ground that stays and the mountain that wakes (Source/ColonyCouncil/Nature/Ground*.cs; the player's picks D1 and D2
/// of 28 November 2026): which quakes leave a mark and how often, each mark's size (a few to a few dozen cells), never on or
/// near the colony's things, the rain's landslide and the wet season's sinkhole, which lands have a mountain that may wake,
/// its stages and how often; and the mod's side: the hook on the game's events, the harm through the shared budget, the
/// settings, the clock, the save, the words (plain, one sentence said aloud, no god named as fact), and every def name read
/// softly in the player's active mods.
/// </summary>
public static class LivingGroundTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "living ground: " + what);
        }

        const int D = GroundMath.DayTicks;
        var rolls = Enumerable.Range(0, 1000).Select(i => i / 1000f).ToList();

        // ================= Which quakes =================

        C(GroundMath.SourceOf("Earthquake", "IncidentWorker_Earthquake") == QuakeSource.Quake && GroundMath.SourceOf("VEE_Earthquake", "EarthQuake") == QuakeSource.Quake
          && GroundMath.SourceOf("DrillQuake", "IncidentWorker_Earthquake") == QuakeSource.Quake, "Seasonal Weather's, Vanilla Events Expanded's and Rimefeller's quakes by name");
        C(GroundMath.SourceOf("SomeMod_GreatEarthquake", "Whatever") == QuakeSource.Quake && GroundMath.SourceOf("SomeMod_Shake", "SomeMod.EarthQuakeWorker") == QuakeSource.Quake,
            "any other mod's earthquake by its name or its worker's");
        C(GroundMath.SourceOf("Council_Evt_Rockslide", "HappeningWorker_Rockslide") == QuakeSource.Rockslide
          && GroundMath.SourceOf("Council_Evt_WarmVent", "HappeningWorker_WarmVent") == QuakeSource.WarmVent, "Rimfolk's own rockslide and warm vent");
        C(GroundMath.SourceOf("VPE_TimeQuake", "GameCondition_TimeQuake") == QuakeSource.None && GroundMath.SourceOf("RaidEnemy", "IncidentWorker_RaidEnemy") == QuakeSource.None
          && GroundMath.SourceOf(null, null) == QuakeSource.None, "a time quake, a raid and nothing are no quakes");
        C(GroundMath.IsQuakeCondition("Earthquake") && !GroundMath.IsQuakeCondition("VPE_TimeQuake") && !GroundMath.IsQuakeCondition("Flashstorm") && !GroundMath.IsQuakeCondition(null),
            "Seasonal Weather's earthquake going on, by its condition");

        // ================= How often a mark =================

        C(GroundMath.MarkChance(QuakeSource.Quake) == GroundMath.QuakeMarkChance && GroundMath.QuakeMarkChance <= 0.35f
          && GroundMath.MarkChance(QuakeSource.Quake) > GroundMath.MarkChance(QuakeSource.Rockslide) && GroundMath.MarkChance(QuakeSource.Rockslide) > GroundMath.MarkChance(QuakeSource.WarmVent)
          && GroundMath.MarkChance(QuakeSource.WarmVent) > 0f && GroundMath.MarkChance(QuakeSource.None) == 0f,
            "a mark after some quakes only, fewer after the events that leave their own");
        C(GroundMath.MayMark(-1, 5 * D) && !GroundMath.MayMark(10 * D, 69 * D) && GroundMath.MayMark(10 * D, 70 * D) && GroundMath.MayMark(90 * D, 5 * D),
            "no two marks within a season (a tick from a save played on counts as long ago)");
        C(GroundMath.MarkGapDays >= 60, "a season at least between marks");

        // ================= What mark =================

        var both = rolls.Select(r => GroundMath.PickAfterQuake(QuakeSource.Quake, true, true, r)).ToList();
        C(new[] { GroundMarkKind.Cave, GroundMarkKind.Scree, GroundMarkKind.Fissure, GroundMarkKind.Spring }.All(both.Contains) && both.All(k => k != GroundMarkKind.None),
            "an earthquake with rock and open ground near may leave a cave, scree, a fissure or a spring");
        C(Math.Abs(both.Count(k => k == GroundMarkKind.Cave) / 1000f - 3f / 11f) < 0.01f && Math.Abs(both.Count(k => k == GroundMarkKind.Spring) / 1000f - 2f / 11f) < 0.01f,
            "by their weights (3, 3, 3 and 2)");
        C(rolls.All(r => GroundMath.PickAfterQuake(QuakeSource.Quake, false, true, r) is GroundMarkKind.Fissure or GroundMarkKind.Spring)
          && rolls.All(r => GroundMath.PickAfterQuake(QuakeSource.Quake, true, false, r) is GroundMarkKind.Cave or GroundMarkKind.Scree)
          && GroundMath.PickAfterQuake(QuakeSource.Quake, false, false, 0.5f) == GroundMarkKind.None, "only what the land has room for");
        C(rolls.All(r => GroundMath.PickAfterQuake(QuakeSource.Rockslide, true, true, r) is GroundMarkKind.Cave or GroundMarkKind.Spring)
          && rolls.All(r => GroundMath.PickAfterQuake(QuakeSource.WarmVent, true, true, r) is GroundMarkKind.Spring or GroundMarkKind.Fissure)
          && GroundMath.PickAfterQuake(QuakeSource.None, true, true, 0.3f) == GroundMarkKind.None, "the rockslide a hollow or water, the warm vent's tremor water or a crack");
        C(GroundMath.PickAfterQuake(QuakeSource.Quake, true, true, -1f) != GroundMarkKind.None && GroundMath.PickAfterQuake(QuakeSource.Quake, true, true, 2f) != GroundMarkKind.None,
            "a roll out of range still picks");

        // ================= How big: a few to a few dozen cells =================

        var kinds = new[] { GroundMarkKind.Cave, GroundMarkKind.Spring, GroundMarkKind.Fissure, GroundMarkKind.Scree, GroundMarkKind.Landslide, GroundMarkKind.Sinkhole };
        foreach (var k in kinds)
        {
            var sizes = rolls.Select(r => GroundMath.Cells(k, r)).ToList();
            C(sizes.All(s => s >= 3 && s <= GroundMath.MaxCells && s >= GroundMath.Fewest(k)), $"{k}: {sizes.Min()} to {sizes.Max()} cells, a few to a few dozen");
        }
        C(GroundMath.MaxCells <= 40 && rolls.All(r => GroundMath.Cells(GroundMarkKind.Landslide, r) <= 24) && rolls.All(r => GroundMath.Cells(GroundMarkKind.Cave, r) <= 7)
          && GroundMath.Cells(GroundMarkKind.None, 0.5f) == 0, "kept small: a landslide 24 cells at most, a cave 7");
        C(GroundMath.SinkholeRadius(9) == 1.5f && GroundMath.SinkholeRadius(13) == 2f && rolls.All(r => GroundMath.Cells(GroundMarkKind.Sinkhole, r) is 9 or 13),
            "a sinkhole of 9 or 13 cells, round");
        C(Enumerable.Range(10, 15).All(c => GroundMath.SlideRubble(c) >= 2 && GroundMath.SlideRubble(c) <= 5 && GroundMath.SlideRubble(c) < c), "a landslide's rubble against the face, 2 to 5 cells");

        // ================= Never on or near the colony's things =================

        C(GroundMath.CellAllowed(false, false, false, false, false), "open land far from the colony takes a mark");
        C(!GroundMath.CellAllowed(true, false, false, false, false) && !GroundMath.CellAllowed(false, true, false, false, false) && !GroundMath.CellAllowed(false, false, true, false, false)
          && !GroundMath.CellAllowed(false, false, false, true, false) && !GroundMath.CellAllowed(false, false, false, false, true),
            "never near the colony's things, in its home area, in a zone, on anything built, or where someone stands");
        C(GroundMath.ColonyGap >= 8f, $"{GroundMath.ColonyGap} cells kept clear round the colony's things");
        C(GroundMath.CaveCellHolds(GroundMath.CaveRockAround) && !GroundMath.CaveCellHolds(GroundMath.CaveRockAround - 1) && GroundMath.CaveRockAround >= 3,
            "a cave stays a narrow hollow in solid rock, every roof over it resting on rock");
        C(GroundMath.RockMass >= 15 && GroundMath.CaveClearOfPeople >= 8f, "only into a real rock mass, with nobody near as it opens");

        // ================= The rain and the wet season =================

        C(GroundMath.SlideToday(true, true, true, false, 3, 0f) && !GroundMath.SlideToday(false, true, true, false, 9, 0f) && !GroundMath.SlideToday(true, false, true, false, 9, 0f)
          && !GroundMath.SlideToday(true, true, false, false, 9, 0f) && !GroundMath.SlideToday(true, true, true, false, 2, 0f),
            "a hillside comes down only on hilly land with a rock face, in rain after three wet days");
        C(!GroundMath.SlideToday(true, true, true, false, 5, 0.06f) && GroundMath.SlideToday(true, true, true, true, 5, 0.06f) && !GroundMath.SlideToday(true, true, true, true, 5, 0.09f),
            "four days in a hundred such days, twice that in a storm");
        C(GroundMath.WetSeason(8) && !GroundMath.WetSeason(7) && GroundMath.WetSeasonDays * 2 > 15, "a wet season is rain on more than half its days");
        C(Math.Abs(GroundMath.SinkholeChanceFor(true) - 2f * GroundMath.SinkholeChanceFor(false)) < 1e-6f && GroundMath.SinkholeChanceFor(false) <= 0.15f,
            "a sinkhole ends a wet season now and then, twice as often on dissolving rock");
        C(GroundMath.Karst("Limestone") && GroundMath.Karst("Marble") && GroundMath.Karst("AB_Chalk") && !GroundMath.Karst("Granite") && !GroundMath.Karst("Sandstone") && !GroundMath.Karst(null),
            "limestone and marble dissolve, granite doesn't");
        C(rolls.All(r => GroundMath.SinkDelayHours(r) >= 8 && GroundMath.SinkDelayHours(r) <= 16), "the ground sags 8 to 16 hours before it gives way (the warning)");
        C(GroundMath.SeasonKey(3, 0) > GroundMath.SeasonKey(2, 3) && GroundMath.SeasonKey(2, 2) > GroundMath.SeasonKey(2, 1), "each season looked at once, in order");

        // ================= The harm =================

        C(GroundMath.SlideHurtChance <= 0.6f && GroundMath.SinkHurtChance <= 0.5f && GroundMath.StoneHurtChance <= 0.2f,
            "only someone caught there may be hurt, and not always (the budget's scale on top)");
        C(HarmKinds.All.Contains(HarmKinds.Ground) && HarmKinds.Ground.system == "ground" && !HarmKinds.Ground.founding && HarmKinds.Ground.weight < 0.01f && HarmKinds.Of("ground") == HarmKinds.Ground,
            "the ground's hurts are a small kind of the shared budget");

        // ================= Which lands may wake =================

        C(GroundMath.LandOf(false, false, false, false) == WakeLand.None, "flat land and hills never wake");
        C(GroundMath.LandOf(true, false, false, false) == WakeLand.Mountain && GroundMath.LandOf(false, false, false, true) == WakeLand.Mountain, "mountainous land, or a mountain among its features");
        C(GroundMath.LandOf(false, true, false, false) == WakeLand.Lava && GroundMath.LandOf(true, false, true, true) == WakeLand.Lava, "a lava field, or lava among the features, before mountains");
        foreach (var biome in new[] { "LavaField", "AB_PyroclasticConflagration" }) C(GroundMath.LavaBiomes.Contains(biome), $"{biome} is a land of lava");
        foreach (var f in new[] { "LavaCaves", "LavaCrater", "LavaFlow", "LavaLake", "AB_MagmaVents", "GL_Caldera", "VEE_Volcano", "VEE_ResurgentCaldera", "SomeMod_VolcanicPeak" })
            C(GroundMath.LavaFeature(f), $"{f} is a feature of lava");
        foreach (var f in new[] { "Mountain", "GL_LoneMountain", "VEE_JadeiteMountains" }) C(GroundMath.MountainFeature(f) && !GroundMath.LavaFeature(f), $"{f} is a mountain");
        C(!GroundMath.LavaFeature("Coast") && !GroundMath.LavaFeature(null) && !GroundMath.MountainFeature("River") && !GroundMath.MountainFeature(null), "a coast or a river is neither");

        // ================= How often it wakes =================

        C(GroundMath.WakeChancePerDay(WakeLand.None, true) == 0f && GroundMath.WakeChancePerDay(WakeLand.Lava, false) > GroundMath.WakeChancePerDay(WakeLand.Mountain, false)
          && Math.Abs(GroundMath.WakeChancePerDay(WakeLand.Mountain, true) - 2f * GroundMath.WakeChancePerDay(WakeLand.Mountain, false)) < 1e-7f,
            "lava sooner than mountains; deep mining under it lately twice as readily");
        float mountainYears = GroundMath.MeanYearsBetween(WakeLand.Mountain, false), lavaYears = GroundMath.MeanYearsBetween(WakeLand.Lava, false);
        C(mountainYears >= 15f && mountainYears <= 20f && lavaYears >= 10f && lavaYears < mountainYears, $"once in many years: about every {mountainYears:0} on mountains, {lavaYears:0} on lava");
        C(float.IsPositiveInfinity(GroundMath.MeanYearsBetween(WakeLand.None, true)), "never on flat land");
        C(GroundMath.Wakes(WakeLand.Mountain, 61, 99999f, false, 0f) && !GroundMath.Wakes(WakeLand.Mountain, 59, 99999f, false, 0f)
          && !GroundMath.Wakes(WakeLand.Lava, 400, GroundMath.WakeGapDays - 1, true, 0f) && !GroundMath.Wakes(WakeLand.None, 400, 99999f, true, 0f)
          && !GroundMath.Wakes(WakeLand.Lava, 400, 99999f, false, 0.5f), "not before day 60, never within five years of the last, only by the day's roll");
        // A hundred years on a mountain, day by day, with a fixed sequence of rolls: a handful of wakings, never two close.
        var rng = new Random(7);
        int wakes = 0, last = -99999, closest = int.MaxValue;
        for (int day = 0; day < 100 * GroundMath.YearDays; day++)
        {
            if (!GroundMath.Wakes(WakeLand.Mountain, day, day - last, false, (float)rng.NextDouble())) continue;
            if (last >= 0) closest = Math.Min(closest, day - last);
            last = day;
            wakes++;
        }
        C(wakes >= 2 && wakes <= 12 && closest >= GroundMath.WakeGapDays, $"a hundred years on a mountain: {wakes} wakings, the closest {closest} days apart");

        // ================= Its stages =================

        C(GroundMath.Next(WakeStage.Tremors) == WakeStage.Ash && GroundMath.Next(WakeStage.Ash) == WakeStage.Settling && GroundMath.Next(WakeStage.Settling) == WakeStage.Quiet
          && GroundMath.Next(WakeStage.Quiet) == WakeStage.Quiet, "tremors, then ash, then the settling, then quiet");
        C(rolls.All(r => GroundMath.StageHours(WakeStage.Tremors, r) is >= 72 and <= 120) && rolls.All(r => GroundMath.StageHours(WakeStage.Ash, r) is >= 48 and <= 72)
          && rolls.All(r => GroundMath.StageHours(WakeStage.Settling, r) is >= 120 and <= 216) && GroundMath.StageHours(WakeStage.Quiet, 0.5f) == 0,
            "tremors for three to five days, ash for two to three, rich soil five to nine days after");
        float perDay = GroundMath.TremorChancePerLook(250) * (D / 250f);
        C(Math.Abs(perDay - GroundMath.TremorsPerDay) < 0.01f && GroundMath.TremorsPerDay <= 6f, $"a few tremors a day ({perDay:0.0})");
        C(rolls.All(r => GroundMath.Vents(r) is 1 or 2) && rolls.All(r => GroundMath.WinterDays(r) is >= 4 and <= 8) && rolls.All(r => GroundMath.AshAirDays(r) is 1 or 2),
            "one or two warm vents, the game's volcanic winter four to eight days, Odyssey's ash in the air a day or two");
        C(rolls.All(r => GroundMath.AshCells(r) is >= 20 and <= GroundMath.MaxCells) && rolls.All(r => GroundMath.AshPatches(r) is >= 2 and <= 4), "twenty to forty cells of rich soil, in two to four patches");
        C(GroundMath.AshTurns(true, false, false, 1f, 1.4f) && GroundMath.AshTurns(true, false, false, 0.7f, 1.4f) && !GroundMath.AshTurns(true, true, false, 1f, 1.4f)
          && !GroundMath.AshTurns(true, false, true, 1f, 1.4f) && !GroundMath.AshTurns(false, false, false, 1f, 1.4f) && !GroundMath.AshTurns(true, false, false, 1.4f, 1.4f)
          && !GroundMath.AshTurns(true, false, false, 0.05f, 1.4f), "only natural soil that grows something turns rich, never water, a floor or what is rich already");

        // ================= The mod's side =================

        string root = Root();
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string map = Code("Nature/GroundMap.cs"), marks = Code("Nature/GroundMarks.cs"), waking = Code("Nature/GroundWaking.cs");
        string all = map + marks + waking + Code("Nature/GroundDebug.cs");
        C(map.Contains("[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]") && map.Contains("if (!__result || !RimfolkPatches.On") && map.Contains("GroundMarks.Noted("),
            "quakes noted from the game's own events, off with the deeper hooks");
        C(marks.Contains("GroundMath.IsQuakeCondition(") && marks.Contains("ActiveConditions"), "Seasonal Weather's earthquake read by its condition, by name");
        C(map.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareGround)") && map.Contains("PassClock.Time(Pass.RareGround)") && Sched.RareOffsets.Contains(Sched.RareGround)
          && Sched.RareGround != 61 && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareGround) && PassTable.All.Any(p => p.pass == Pass.RareGround && !p.waiting),
            "its own tick of the cycle, timed on the clock");
        C(map.Contains("map.IsPlayerHome") && map.Contains("MapFacts.InVacuum(map)"), "home maps on the ground only");
        // Every hurt through the budget, named as the ground's.
        foreach (Match m in Regex.Matches(all, @"Harm\.(Roll|Injure)\([^;]*"))
            C(m.Value.Contains("HarmKinds.Ground"), $"'{m.Value.Substring(0, Math.Min(60, m.Value.Length))}' names the ground's kind");
        C(Regex.Matches(all, @"Harm\.(Roll|Injure)\(").Count >= 4 && !all.Contains("TakeDamage("), "every hurt through the harm service");
        C(Code("Harm/Harm.cs").Contains("case \"ground\": return s.groundMarks || s.wakingMountain;"), "the ground's share of the budget only while a part is on");
        // Never on or near the colony's things: the clearance reads all of them and every mark asks it.
        C(marks.Contains("allBuildingsColonist") && marks.Contains("ThingRequestGroup.Blueprint") && marks.Contains("ThingRequestGroup.BuildingFrame") && marks.Contains("GroundMath.CellAllowed(")
          && marks.Contains("map.areaManager.Home[c]") && marks.Contains("c.GetZone(map) != null"), "the colony's buildings, plans, home area and zones kept clear");
        C(Regex.Matches(marks, @"clear\.(Open|Allowed)\(").Count >= 8 && waking.Contains("clear.Open(cell)"), "every mark's cells and every vent asked first");
        C(marks.Contains("GenSpawn.Spawn(ThingDefOf.CollapsedRocks") && marks.Contains("c.GetFirstPawn(map) == null && c.GetFirstItem(map) == null"), "rubble never on anyone or anything");
        C(marks.Contains("GroundMath.CaveCellHolds(") && marks.Contains("RoofDefOf.RoofRockThin") && marks.Contains("GroundMath.CaveClearOfPeople"), "a cave roofed with rock, carved where it holds, nobody near");
        C(marks.Contains("SinkholeSign(") && marks.Contains("Filth_LooseGround") && marks.Contains("g.sinkDue = now + GroundMath.SinkDelayHours("), "a sinkhole shows first, then opens");
        // Told and shown.
        C(marks.Contains("Tell.Message(NewsKind.Land") && marks.Contains("AddChronicle(") && marks.Contains("Speech.Say(") && marks.Contains("Find.CameraDriver.shaker.DoShake")
          && waking.Contains("Tell.Letter(NewsKind.Land") && waking.Contains("FleckMaker.ThrowSmoke"), "told, written into the chronicle, said and shown on the map");
        C(marks.Contains("GodHands.Show(") && marks.Contains("GodHandsMath.Remarked(") && waking.Contains("GroundMarks.SomeSay(map, \"Smoulder\""),
            "a believer may take it for Smoulder's doing, in their own words, as rarely as the hands");
        // Tied to the game's and Odyssey's volcanic conditions, softly.
        C(waking.Contains("Start(map, \"VolcanicWinter\"") && waking.Contains("Start(map, \"VolcanicAsh\"") && waking.Contains("GetNamedSilentFail(\"VolcanicAsh\")")
          && waking.Contains("GameConditionMaker.MakeCondition("), "the game's volcanic winter and Odyssey's volcanic ash by name");
        C(waking.Contains("GodsOnMapBook.Instance") && waking.Contains("lastDeep"), "the sleeper turning under deep mines stirs the mountain");
        // Settings, on by default, beside the events.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool groundMarks = true;") && settings.Contains("public bool wakingMountain = true;")
          && settings.Contains("Scribe_Values.Look(ref groundMarks, \"groundMarks\", true);") && settings.Contains("Scribe_Values.Look(ref wakingMountain, \"wakingMountain\", true);"),
            "two settings, on by default and saved");
        C(catalog.Contains("Toggle(\"groundMarks\", \"CC_Settings_GroundMarks\", \"CC_SetNote_GroundMarks\")")
          && catalog.Contains("Toggle(\"wakingMountain\", \"CC_Settings_WakingMountain\", \"CC_SetNote_WakingMountain\")"), "both on the settings page");
        // Saved small.
        C(map.Contains("Scribe_Collections.Look(ref ash, \"ash\", LookMode.Value);") && map.Contains("GroundMath.MaxCells")
          && File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Nature/GroundMap.cs:ash\""), "the ash's cells saved, capped and modelled");

        // ================= The words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ground.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var used = Regex.Matches(all, "\"(CC_Ground_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        foreach (var k in used) C(keyed.ContainsKey(k), $"{k} has its words");
        foreach (var s in new[] { QuakeSource.Quake, QuakeSource.Rockslide, QuakeSource.WarmVent }) C(keyed.ContainsKey("CC_Ground_What_" + s), $"what shook it: {s}");
        foreach (var k in kinds)
        {
            C(keyed.ContainsKey("CC_Ground_Chron_" + k), $"{k} in the chronicle");
            C(keyed.ContainsKey("CC_Ground_Say_" + k) && keyed.ContainsKey("CC_Ground_Say_" + k + "_2"), $"{k} said aloud, in more than one way");
        }
        foreach (var kv in keyed.Where(kv => kv.Key.StartsWith("CC_Ground_Say_")))
        {
            string body = kv.Value.Trim();
            int end = body.IndexOfAny(new[] { '.', '!', '?' }, Math.Min(12, body.Length - 1));
            C(end == body.Length - 1 && body.Length <= 90 && !body.StartsWith("\"") && !body.Contains('!'), $"{kv.Key}: one short sentence said aloud ('{body}')");
        }
        var godNames = new[] { "Smoulder", "Murmur", "Hollin", "Morel", "Cirrus", "Helios", "Hush", "Selene", "Bellwether" };
        foreach (var kv in keyed)
        {
            C(!kv.Value.Contains('—') && !kv.Value.Contains('!'), $"{kv.Key}: plain, no em dash or exclamation mark");
            C(!godNames.Any(g => kv.Value.Contains(g)), $"{kv.Key}: no god named as fact");
        }
        C(keyed.ContainsKey("CC_Clock_Pass_RareGround"), "the clock names its pass");

        // ================= Every name read softly is in the player's active mods =================

        var table = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        C(File.Exists(table), "the catalogue of the player's defs is there");
        if (File.Exists(table))
        {
            var active = new HashSet<string>();
            using (var gz = new GZipStream(File.OpenRead(table), CompressionMode.Decompress))
            using (var r = new StreamReader(gz))
            {
                r.ReadLine();
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    var cells = line.Split('\t');
                    if (cells.Length >= 4 && cells[3] == "yes") active.Add(cells[0] + ":" + cells[1]);
                }
            }
            var names = new (string type, string name)[]
            {
                ("IncidentDef", "Earthquake"), ("GameConditionDef", "Earthquake"), ("IncidentDef", "VEE_Earthquake"), ("IncidentDef", "DrillQuake"),
                ("ThingDef", "VEE_EarthquakeEpicenter"), ("TerrainDef", "HotSpring"), ("TerrainDef", "Gravel"), ("TerrainDef", "SoilRich"), ("TerrainDef", "VEE_VolcanicSoilRich"),
                ("GameConditionDef", "VolcanicWinter"), ("GameConditionDef", "VolcanicAsh"), ("ThingDef", "Filth_VolcanicAsh"), ("ThingDef", "Filth_Ash"), ("ThingDef", "ChunkGranite"),
                ("ThingDef", "CollapsedRocks"), ("ThingDef", "SteamGeyser"), ("BiomeDef", "LavaField"), ("BiomeDef", "AB_PyroclasticConflagration")
            };
            foreach (var (type, name) in names) C(active.Contains(type + ":" + name), $"{type} {name} is in the player's active mods");
            foreach (var f in GroundMath.LavaFeatures)
                C(active.Contains("TileMutatorDef:" + f) || active.Contains("LandmarkDef:" + f), $"the lava feature {f} is in the player's active mods");
            foreach (var name in new[] { "VEE_EarthquakeEpicenter", "HotSpring", "VEE_VolcanicSoilRich", "Filth_VolcanicAsh", "VolcanicAsh", "VolcanicWinter" })
                C(all.Contains("GetNamedSilentFail(\"" + name + "\")") || all.Contains("\"" + name + "\""), $"{name} read softly, by name");
        }
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
