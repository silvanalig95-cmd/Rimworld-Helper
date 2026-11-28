using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The living land, the land's part (docs/design/living-land-land.md; Source/ColonyCouncil/Nature/Land*.cs): B1 land that
/// recovers (the patches' stages and their timing in growing days, the patch boxes, the pasture grazed bare and rested), B3
/// sicknesses of the wild plants (how often one comes, which, its spread odds and caps, how it ends, when the council is
/// asked), B5 water that rises and falls (how wet the season reads, the spells, how far the water moves and the steps back to
/// the map's own), the caps on every pass and the clock; the record's short lines; the defs, words, settings, the Almanac,
/// the save model and the seams (one method for how wet the season is; rivers and the sea never touched; no hurt given;
/// every def named in the code in the player's mods).
/// </summary>
public static class LivingLandTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the living land: " + what);
        }

        // ================= B1: stages and their timing =================

        C(LandMath.StageFor(0, true) == LandStage.Bare && LandMath.StageFor(LandMath.GrassDays, true) == LandStage.Grass
          && LandMath.StageFor(LandMath.BrushDays, true) == LandStage.Brush && LandMath.StageFor(LandMath.SaplingDays, true) == LandStage.Saplings
          && LandMath.StageFor(LandMath.WoodsDays, true) == LandStage.Woods, "a wood comes back bare, grass, brush, young trees, woods");
        C(LandMath.StageFor(LandMath.WoodsDays + 50, false) == LandStage.Brush && LandMath.Closed(LandMath.SaplingDays, false) && !LandMath.Closed(LandMath.SaplingDays, true)
          && LandMath.Closed(LandMath.WoodsDays, true), "where no wood stood it is grass and brush, done after the brush; a wood is done as woods");
        C(LandMath.GrassDays < LandMath.BrushDays && LandMath.BrushDays < LandMath.SaplingDays && LandMath.SaplingDays < LandMath.WoodsDays, "the stages in order");
        for (int d = 0; d < 80; d++)
            C(LandMath.StageFor(d + 1, true) >= LandMath.StageFor(d, true), $"a patch never goes back a stage (day {d})");
        C(LandMath.Growing(6f) && !LandMath.Growing(5.9f), "a growing day averages 6 °C or more (the advisor's own winter)");
        // A temperate year: three growing seasons of 15 days and a winter that doesn't count.
        var p = LandMath.Open(LandCause.Burn, 10, 10, 0, true, "Plant_TreeOak");
        for (int i = 1; i <= 4; i++) LandMath.Add(p, 10 + i, 10, true, "Plant_TreePoplar");
        int day = 0, firstGrass = -1, firstWoods = -1;
        for (; day < 300 && firstWoods < 0; day++)
        {
            bool growing = day % 60 < 45;
            LandMath.CountDay(p, day, growing);
            LandMath.CountDay(p, day, growing);
            var s = LandMath.StageFor(p.growDays, p.Wood);
            if (s >= LandStage.Grass && firstGrass < 0) firstGrass = day;
            if (s == LandStage.Woods) firstWoods = day;
        }
        C(p.tree == "Plant_TreeOak" && p.trees == 5 && p.Wood, "the first tree's kind is kept for the young trees, and five trees lost make it a wood");
        C(firstGrass >= 0 && firstGrass <= 3, $"grass on a burn in the first days of a growing season ({firstGrass})");
        C(firstWoods > 60 && firstWoods < 120, $"a burnt wood is woods again in the second year of a temperate land ({firstWoods} days)");
        LandMath.StartOver(p);
        C(p.growDays == 0 && p.stage == LandStage.Bare && p.told == 0 && p.Wood, "felled or burned again while coming back, it starts over");
        var once = LandMath.Open(LandCause.Felled, 0, 0, 0, true, "x");
        LandMath.CountDay(once, 5, true);
        C(!LandMath.CountDay(once, 5, true) && once.growDays == 1 && !LandMath.CountDay(once, 6, false) && once.growDays == 1, "each day counted once, only a growing one");
        C(LandMath.Seeds(LandStage.Bare) == 0 && LandMath.Seeds(LandStage.Woods) == 0 && LandMath.Seeds(LandStage.Grass) > LandMath.Seeds(LandStage.Brush)
          && LandMath.Seeds(LandStage.Brush) > LandMath.Seeds(LandStage.Saplings) && LandMath.Seeds(LandStage.Saplings) > 0, "more grass set than brush, more brush than young trees");
        C(Enumerable.Range(0, 20).All(i => LandMath.Pick(LandStage.Grass, i / 20f) == 0), "at the grass stage only grass is set");
        C(Enumerable.Range(0, 20).Any(i => LandMath.Pick(LandStage.Brush, i / 20f) == 1) && Enumerable.Range(0, 20).All(i => LandMath.Pick(LandStage.Brush, i / 20f) != 2),
            "at the brush stage brush and grass, no trees yet");
        C(new[] { 0, 1, 2 }.All(k => Enumerable.Range(0, 20).Any(i => LandMath.Pick(LandStage.Saplings, i / 20f) == k)), "with the young trees, all three");
        C(LandMath.GrassGrowthMin < LandMath.GrassGrowthMax && LandMath.SaplingGrowthMax <= 0.2f && LandMath.SaplingGrowthMin > 0f, "young trees set small");

        // The patches' boxes.
        var box = LandMath.Open(LandCause.Burn, 50, 50, 3, false, null);
        C(LandMath.Joins(box, 50 + LandMath.JoinGap, 50) && !LandMath.Joins(box, 51 + LandMath.JoinGap, 50), "a lost plant joins a patch within three cells of it");
        for (int x = 50; x < 50 + LandMath.MaxSpan; x++) if (LandMath.Joins(box, x, 50)) LandMath.Add(box, x, 50, false, null);
        C(box.Width == LandMath.MaxSpan && !LandMath.Joins(box, 50 + LandMath.MaxSpan, 50) && box.CenterX == 50 + (LandMath.MaxSpan - 1) / 2,
            $"a patch grows to {LandMath.MaxSpan} cells across at most ({box.Width})");
        C(!LandMath.Counts(LandCause.Burn, 11, 0) && LandMath.Counts(LandCause.Burn, 12, 0) && !LandMath.Counts(LandCause.Felled, 20, 5) && LandMath.Counts(LandCause.Felled, 6, 6)
          && LandMath.Counts(LandCause.Wilt, 3, 3) && LandMath.Counts(LandCause.Rust, 10, 0), "a patch counts once enough is lost: a dozen burnt, six felled, three wilted, ten rusted");
        var small = LandMath.Open(LandCause.Felled, 0, 0, 100, true, "t");
        C(!LandMath.Stale(small, 100 + LandMath.PendingDays) && LandMath.Stale(small, 101 + LandMath.PendingDays), "a patch that never came to count is let go after ten days");
        C(LandMath.CellsEach(0) == 0 && LandMath.CellsEach(1) == LandMath.RegrowCells && LandMath.CellsEach(LandMath.MaxPatches) * LandMath.MaxPatches <= LandMath.PassCap,
            "the regrowth pass shares its cells out among the patches, within the cap");

        // The pasture grazed bare.
        C(LandMath.Overgrazed(8 * 2.4f, 30, 5, 36) && !LandMath.Overgrazed(2 * 1.2f, 30, 25, 36) && !LandMath.Overgrazed(50f, 30, 20, 36),
            "eight cows on a bare pen overgraze it; two deer in a meadow don't; nor a crowd while the grass covers the ground");
        C(!LandMath.Overgrazed(50f, 2, 0, 36) && !LandMath.Overgrazed(50f, 10, 0, 0), "rock and water aren't pasture");
        var g = new LandGrazing { block = 3 };
        for (int i = 0; i < LandMath.WornAfter - 1; i++) LandMath.Day(g, true, true);
        C(!LandMath.Wears(g), "not worn before six overgrazed days");
        LandMath.Day(g, true, true);
        C(LandMath.Wears(g), "worn after six");
        for (int i = 0; i < LandMath.WornAfter; i++) LandMath.Day(g, false, true);
        C(g.days == 0 && !LandMath.Rested(g), "a day of rest takes one off; not yet rested");
        for (int i = 0; i < LandMath.RestDays; i++) LandMath.Day(g, false, i % 3 != 0);
        C(!LandMath.Rested(g), "only growing days of rest count");
        for (int i = 0; i < LandMath.RestDays; i++) LandMath.Day(g, false, true);
        C(LandMath.Rested(g), "rested twenty growing days, the ground comes back");
        LandMath.Day(g, true, true);
        C(g.rest == 0, "crowded again, the rest starts over");
        C(LandMath.WornTo("SoilRich") == "Soil" && LandMath.WornTo("Soil") == "PackedDirt" && LandMath.WornTo("MossyTerrain") == "PackedDirt" && LandMath.WornTo("MarshyTerrain") == "Mud"
          && LandMath.WornTo("Gravel") == null && LandMath.WornTo("Sand") == null && LandMath.WornTo("PackedDirt") == null, "what grazed-bare ground wears to, and no further");
        C(LandMath.WearCap(36, 36) == (int)(LandMath.Block * LandMath.Block * LandMath.WearShare) && LandMath.WearCap(0, 36) == 0, "a quarter of the good ground at most");
        C(LandMath.BlockOf(0, 0, 250) == 0 && LandMath.BlockOf(17, 0, 250) == 1 && LandMath.BlockOf(0, 16, 250) == 16 && LandMath.BlockOf(249, 249, 250) == 15 * 16 + 15, "the grazing blocks");

        // ================= B3: how often, which, how it spreads =================

        C(LandMath.SickChance(false, 100, 100, true, true) == 0f && LandMath.SickChance(true, LandMath.SickFirstDays - 1, 999, false, false) == 0f
          && LandMath.SickChance(true, 100, LandMath.SickGapDays - 1, false, false) == 0f, "none in the cold, in a colony's first days, or soon after the last");
        float baseChance = LandMath.SickChance(true, 100, 100, false, false);
        C(Math.Abs(baseChance - 1f / 90f) < 1e-6 && Math.Abs(LandMath.SickChance(true, 100, 100, true, false) - baseChance * 3f) < 1e-6
          && Math.Abs(LandMath.SickChance(true, 100, 100, false, true) - baseChance * 1.5f) < 1e-6, "one in ninety growing days; three times with the region's blight; half again wet and warm");
        // A temperate land (45 growing days a year) over twenty years.
        var rng = new Random(7);
        int outbreaks = 0, last = -999;
        for (int d = 0; d < 1200; d++)
        {
            bool growing = d % 60 < 45;
            if (rng.NextDouble() < LandMath.SickChance(growing, d, d - last, false, false))
            {
                outbreaks++;
                last = d + 25;
            }
        }
        C(outbreaks >= 4 && outbreaks <= 20, $"a sickness every year or two in a temperate land ({outbreaks} in twenty years)");
        C(LandMath.PickKind(new[] { 0, 0, 0 }, 0.5f) == -1 && LandMath.PickKind(new[] { 14, 59, 5 }, 0.5f) == -1, "nothing begins without its hosts");
        C(LandMath.PickKind(new[] { 15, 0, 0 }, 0.99f) == 0 && LandMath.PickKind(new[] { 0, 60, 0 }, 0f) == 1 && LandMath.PickKind(new[] { 0, 0, 6 }, 0.5f) == 2, "only one with hosts: that one");
        var kinds = Enumerable.Range(0, 100).Select(i => LandMath.PickKind(new[] { 100, 500, 50 }, i / 100f)).ToList();
        C(kinds.Count(k => k == 0) > kinds.Count(k => k == 2) && kinds.Distinct().Count() == 3, "all three can come, the wilt most often");
        C(LandMath.SpreadChance(LandSickKind.Rust, true, true) == 0f && LandMath.SpreadChance(LandSickKind.Wilt, false, true) == 0f, "nothing spreads in the cold");
        C(LandMath.SpreadChance(LandSickKind.Rust, true, false) > LandMath.SpreadChance(LandSickKind.Rust, false, false)
          && LandMath.SpreadChance(LandSickKind.Rot, true, false) > LandMath.SpreadChance(LandSickKind.Rot, false, false)
          && LandMath.SpreadChance(LandSickKind.Wilt, true, false) == LandMath.SpreadChance(LandSickKind.Wilt, false, false), "rain helps the rust and the rot spread, not the wilt");
        foreach (LandSickKind k in Enum.GetValues(typeof(LandSickKind)))
        {
            C(LandMath.SpreadChance(k, true, false) <= 0.9f && LandMath.SpreadChance(k, false, false) > 0f, k + ": a chance, never certain");
            C(LandMath.Reach(k) >= 3 && LandMath.Reach(k) <= 6, k + ": reaches a few cells");
            C(LandMath.Cap(k) <= LandMath.SickLooks, k + ": every sick plant is looked at each day");
            C(LandMath.Room(k, 0, 0) == LandMath.NewPerDay(k) && LandMath.Room(k, LandMath.NewPerDay(k), 0) == 0 && LandMath.Room(k, 0, LandMath.Cap(k)) == 0
              && LandMath.Room(k, 0, LandMath.Cap(k) - 1) == 1, k + ": within the day's and the outbreak's caps");
            C(!LandMath.Dies(k, 10, 10 + LandMath.DaysToDie(k) - 1) && LandMath.Dies(k, 10, 10 + LandMath.DaysToDie(k)), k + ": a sick plant dies after its days");
            C(LandMath.MinHosts(k) > 0 && LandMath.Weight(k) > 0f, k + ": its hosts and weight");
        }
        C(LandMath.DaysToDie(LandSickKind.Wilt) == 10 && LandMath.DaysToDie(LandSickKind.Rust) == 5 && LandMath.DaysToDie(LandSickKind.Rot) == 7, "a tree ten days, a grass five, a bush seven");
        C(LandMath.Bare(LandSickKind.Wilt, 0, LandMath.BareAfter) && !LandMath.Bare(LandSickKind.Wilt, 0, LandMath.BareAfter - 1) && !LandMath.Bare(LandSickKind.Rust, 0, 30), "a wilting tree stands bare after two days");
        // How far one spreads: a wilt from one tree, every contact catching, can't pass its caps.
        int ever = 1;
        var byDay = new List<int>();
        for (int d = 0; d < 30; d++)
        {
            ever += LandMath.Room(LandSickKind.Wilt, 0, ever);
            byDay.Add(ever);
        }
        C(byDay[0] <= 1 + LandMath.NewPerDay(LandSickKind.Wilt) && byDay.Max() == LandMath.Cap(LandSickKind.Wilt) && byDay[6] <= LandMath.Cap(LandSickKind.Wilt),
            $"a wilt reaches {LandMath.NewPerDay(LandSickKind.Wilt)} more trees a day at most and {LandMath.Cap(LandSickKind.Wilt)} in all");
        C(LandMath.Over(0, 3, 0) && LandMath.Over(5, LandMath.SickMaxDays, 0) && LandMath.Over(5, 3, LandMath.ColdEnds) && !LandMath.Over(5, 3, 2), "over when nothing is sick, after 45 days, or after three cold days");
        C(LandMath.ColdRun(2, false) == 3 && LandMath.ColdRun(2, true) == 0, "a growing day starts the cold over");
        C(LandMath.Ask(LandMath.AskAt, false, 2) && !LandMath.Ask(LandMath.AskAt - 1, false, 2) && !LandMath.Ask(10, true, 2) && !LandMath.Ask(10, false, LandMath.AskWithinDays + 1),
            "the council is asked once a few are sick, once, while it is early");
        C(LandMath.BurnClear >= 15f && LandMath.BurnGiveUpDays >= 2 && LandMath.BurnCells <= 3, "a burn only clear of the colony, a few fires, given up after a few days");

        // ================= B5: how wet, and how far the water moves =================

        C(Math.Abs(LandMath.Wetness(3, 3, 0, 0, 1000f, false, false) - 0.5f) < 1e-6, "too few days read: as usual");
        float usual = LandMath.UsualShare(1000f);
        int wetUsual = (int)Math.Round(14 * usual);
        C(LandMath.State(LandMath.Wetness(14, wetUsual, 0, 2, 1000f, false, false)) == LandWetness.Usual, $"a temperate fortnight with {wetUsual} wet days is as usual");
        C(LandMath.State(LandMath.Wetness(14, 0, 0, 14, 1000f, false, false)) == LandWetness.Dry && LandMath.State(LandMath.Wetness(14, 10, 0, 0, 1000f, false, false)) == LandWetness.Wet,
            "a fortnight with nothing falling is dry, ten wet days wet");
        C(LandMath.State(LandMath.Wetness(14, 1, 0, 6, 250f, false, false)) == LandWetness.Usual, "a desert's one wet day in a fortnight is its usual");
        C(LandMath.State(LandMath.Wetness(14, 2, 0, LandMath.DroughtRun, 1000f, false, false)) == LandWetness.Dry, "ten dry days running make a dry spell");
        C(LandMath.State(LandMath.Wetness(14, 10, 0, 0, 1000f, true, false)) == LandWetness.Dry && LandMath.State(LandMath.Wetness(14, 0, 0, 14, 1000f, false, true)) == LandWetness.Wet,
            "a drought going on is dry; the region's floods wet");
        C(LandMath.Wetness(14, 7, 0, 0, 1000f, false, false) > LandMath.Wetness(14, 4, 4, 0, 1000f, false, false), "snow counts for half");
        int dry = 0, wet = 0;
        for (int i = 0; i < LandMath.SpellDays; i++) LandMath.Spell(LandWetness.Dry, ref dry, ref wet);
        C(dry == LandMath.SpellDays && wet == 0, "a dry spell's days counted");
        LandMath.Spell(LandWetness.Wet, ref dry, ref wet);
        C(dry == 0 && wet == 1, "a wet day starts the dry spell over");
        LandMath.Spell(LandWetness.Usual, ref dry, ref wet);
        C(dry == 0 && wet == 0, "a usual day ends both");
        C(LandMath.DryNext("WaterShallow") == "Mud" && LandMath.DryNext("Marsh") == "Mud" && LandMath.DryNext("Mud") == "Soil" && LandMath.DryNext("Soil") == null,
            "drying: water and marsh to mud, mud to soil");
        C(LandMath.SwellNext("Soil", 1) == "Mud" && LandMath.SwellNext("Mud", 1) == "Marsh" && LandMath.SwellNext("Mud", 2) == null && LandMath.SwellNext("Marsh", 1) == null
          && LandMath.SwellNext("Gravel", 2) == "Mud" && LandMath.SwellNext("TileGranite", 1) == null, "swelling: ground to mud, marsh only next to the water");
        C(LandMath.RestoreNext("Soil", "WaterShallow") == "Mud" && LandMath.RestoreNext("Mud", "WaterShallow") == "WaterShallow" && LandMath.RestoreNext("WaterShallow", "WaterShallow") == null
          && LandMath.RestoreNext("Marsh", "Soil") == "Mud" && LandMath.RestoreNext("Mud", "Soil") == "Soil", "back to the map's own a step at a time, through mud");
        // Every way of changing a cell comes back to what it was.
        foreach (var was in new[] { "WaterShallow", "Marsh" })
        {
            string now = was, at = was;
            for (int i = 0; i < 5 && LandMath.DryNext(now) != null; i++) now = LandMath.DryNext(now);
            for (int i = 0; i < 5 && LandMath.RestoreNext(now, was) != null; i++) now = LandMath.RestoreNext(now, was);
            C(now == at, $"dried {was} comes back as {was}");
        }
        foreach (var was in new[] { "Soil", "SoilRich", "MossyTerrain", "MarshyTerrain", "Gravel", "GrasslandSoil", "Sand" })
        {
            string now = was;
            for (int i = 0; i < 5 && LandMath.SwellNext(now, 1) != null; i++) now = LandMath.SwellNext(now, 1);
            C(now == "Marsh", $"{was} next to the water swells to marsh");
            for (int i = 0; i < 5 && LandMath.RestoreNext(now, was) != null; i++) now = LandMath.RestoreNext(now, was);
            C(now == was, $"and comes back as {was}");
        }
        C(new[] { "WaterMovingShallow", "WaterMovingChestDeep", "WaterOceanShallow", "WaterOceanDeep", "WaterDeep", "HotSpring", "ShallowFloodwater", "MarshFlood" }.All(t => !LandMath.StillShallow(t) && LandMath.DryNext(t) == null),
            "rivers, the sea, deep water, hot springs and floods never dry");
        C(LandMath.CanDry(1) && LandMath.CanDry(LandMath.MaxShrink) && !LandMath.CanDry(LandMath.MaxShrink + 1) && !LandMath.CanDry(0) && LandMath.MaxShrink == 3,
            "the water draws back three cells at most");
        C(LandMath.CanSwell(1) && LandMath.CanSwell(LandMath.MaxSwell) && !LandMath.CanSwell(LandMath.MaxSwell + 1) && LandMath.MaxSwell == 2, "and comes over two at most");
        C(LandMath.RingFrom(0) == 1 && LandMath.RingFrom(2) == 3, "a ring one out from its nearest");
        C(LandMath.PerPass(LandWetness.Dry, false) == LandMath.DryPerPass && LandMath.PerPass(LandWetness.Wet, false) == LandMath.SwellPerPass && LandMath.PerPass(LandWetness.Usual, false) == 0
          && LandMath.PerPass(LandWetness.Usual, true) < LandMath.PerPass(LandWetness.Dry, true), "a spell moves the water; as usual it only goes back, slowly");
        C(LandMath.FishAfterSwell(50f, 100f) == 55f && LandMath.FishAfterSwell(99f, 100f) == 100f, "a filled pond gains a few fish, never past what it holds");

        // ================= Caps on every pass, and the clock =================

        foreach (int cells in LandMath.PassCells) C(cells > 0 && cells <= LandMath.PassCap, $"a pass looks at {cells} cells at most (within {LandMath.PassCap})");
        C(LandMath.WaterFind + LandMath.WaterWork <= LandMath.PassCap && LandMath.MaxWaterCells <= 400 && LandMath.MaxWorn <= 300 && LandMath.MaxPatches <= 12 && LandMath.MaxBlocks <= 16,
            "the record stays small");
        for (int map = 0; map < 7; map++)
        {
            var parts = new List<int>();
            for (int cycle = 0; cycle < LandMath.CyclesADay; cycle++)
            {
                int part = LandMath.PartAt(cycle, map);
                if (part >= 0) parts.Add(part);
            }
            C(parts.Count == LandMath.Parts && parts.Distinct().Count() == LandMath.Parts, $"map {map}: each of the four parts once a day ({string.Join(",", parts)})");
        }
        C(Enumerable.Range(0, LandMath.CyclesADay).Any(c => LandMath.PartAt(c, 0) >= 0 && LandMath.PartAt(c, 1) < 0), "two homes don't share a cycle's work");
        C(LandMath.CyclesADay == Sched.DayTicks / Sched.RareTicks && Sched.RareOffsets.Contains(Sched.RareLand) && Sched.RareOffsets.Count(o => o == Sched.RareLand) == 1
          && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareLand), "the land's pass has a 250-tick slot of its own (31)");
        C(PassTable.All.Any(x => x.pass == Pass.RareLand && x.offset == Sched.RareLand && x.period == Sched.RareTicks), "and is timed on the clock");

        // ================= The record, as short lines =================

        var pp = LandMath.Open(LandCause.Wilt, 4, 5, 77, true, "Plant_TreeBirch");
        LandMath.Add(pp, 9, 2, false, null);
        pp.growDays = 13;
        pp.stage = LandStage.Brush;
        pp.lastDay = 90;
        pp.told = 1;
        var back = LandMath.UnpackPatch(LandMath.Pack(pp));
        C(back != null && back.cause == pp.cause && back.x0 == 4 && back.x1 == 9 && back.z0 == 2 && back.z1 == 5 && back.lost == 2 && back.trees == 1 && back.day == 77 && back.lastDay == 90
          && back.growDays == 13 && back.stage == LandStage.Brush && back.tree == "Plant_TreeBirch" && back.told == 1, "a patch kept and read back");
        C(LandMath.UnpackPatch("nonsense") == null && LandMath.UnpackPatch("9,1,1,1,1,1,0,0,0,0,0,") == null && LandMath.UnpackPatch("0,5,5,1,1,1,0,0,0,0,0,") == null, "a damaged patch is dropped");
        var o = new LandOutbreak { kind = LandSickKind.Rot, def = "Plant_Berry", start = 1234, day = 300, died = 2, cut = 3, ever = 9, cold = 1, asked = true, grazersLeft = false, burnWaiting = true, burner = 42, burnSince = 301 };
        var ob = LandMath.UnpackHead(LandMath.PackHead(o));
        C(ob != null && ob.kind == o.kind && ob.def == o.def && ob.start == 1234 && ob.day == 300 && ob.died == 2 && ob.cut == 3 && ob.ever == 9 && ob.cold == 1 && ob.asked && !ob.grazersLeft
          && ob.burnWaiting && ob.burner == 42 && ob.burnSince == 301, "a sickness kept and read back");
        C(LandMath.UnpackHead("1,,0,0,0,0,0,0,0,0,0,0,0") == null && LandMath.UnpackHead("7,x,0,0,0,0,0,0,0,0,0,0,0") == null, "a damaged sickness is dropped");
        C(LandMath.UnpackSick(LandMath.Pack(new LandSickPlant(5000, 33)), out var sp) && sp.cell == 5000 && sp.day == 33 && !LandMath.UnpackSick("x:1", out _) && !LandMath.UnpackSick("-3:1", out _),
            "a sick plant kept by its cell");
        var wc = LandMath.UnpackWater(LandMath.Pack(new LandWaterCell { cell = 77, was = "WaterShallow", ring = 2, dried = true }));
        C(wc != null && wc.cell == 77 && wc.was == "WaterShallow" && wc.ring == 2 && wc.dried && LandMath.UnpackWater("77:Soil:9:s") == null && LandMath.UnpackWater("77::1:d") == null,
            "a water cell kept with what it was");
        var wo = LandMath.UnpackWorn(LandMath.Pack(new LandWornCell { cell = 9, was = "Soil", block = 4 }));
        C(wo != null && wo.cell == 9 && wo.was == "Soil" && wo.block == 4 && LandMath.UnpackWorn("9:Soil") == null, "a worn cell kept with what it was");
        var gr = LandMath.UnpackGrazing(LandMath.Pack(new LandGrazing { block = 6, days = 4, rest = 2, told = true }));
        C(gr != null && gr.block == 6 && gr.days == 4 && gr.rest == 2 && gr.told && LandMath.UnpackGrazing("6:4") == null, "a grazing block kept");
        var trim = Enumerable.Range(0, 20).ToList();
        LandMath.Trim(trim, 5);
        C(trim.SequenceEqual(new[] { 15, 16, 17, 18, 19 }), "a list cut to its last entries");

        // ================= Defs, words, settings, the Almanac, the save =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var code = Directory.GetFiles(Path.Combine(src, "Nature"), "Land*.cs").ToDictionary(f => Path.GetFileName(f), File.ReadAllText);
        string all = string.Join("\n", code.Values);
        C(code.Count >= 10, $"the land's files ({code.Count})");
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(x => x.Key, x => x.First().Groups[2].Value);
        foreach (Match m in Regex.Matches(all, "\"(CC_(?:Land|Impact_Land|Clock_Pass_RareLand)[A-Za-z0-9_]*)\""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_")) continue;
            C(keyed.ContainsKey(key), "the words for " + key);
        }
        foreach (var c in Enum.GetNames(typeof(LandCause))) C(keyed.ContainsKey("CC_Land_Patch_" + c), "a patch's name: " + c);
        foreach (var k in Enum.GetNames(typeof(LandSickKind)))
        {
            C(keyed.ContainsKey("CC_Land_Begins_" + k) && keyed.ContainsKey("CC_Land_Sick_" + k), "a sickness's words: " + k);
            C(keyed.ContainsKey("CC_Land_Belief_" + k.ToLowerInvariant()), "a believer's word kept: " + k);
        }
        C(keyed.ContainsKey("CC_Land_Belief_woods") && keyed.ContainsKey("CC_Clock_Pass_RareLand"), "the woods' belief, the clock's name");
        var landKeys = keyed.Where(kv => kv.Key.StartsWith("CC_Land_") || kv.Key.StartsWith("CC_Impact_Land") || kv.Key.Contains("_Land")).ToList();
        foreach (var kv in landKeys)
            C(!kv.Value.Contains("—") && !kv.Value.Contains("!"), "plain words, no em dash or exclamation: " + kv.Key);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "Rimfolk_Land_*.xml", SearchOption.AllDirectories).ToList();
        C(defs.Count == 3, $"the land's def files ({defs.Count})");
        var docs = defs.Select(f => XDocument.Load(f).Root!).ToList();
        var q = docs.SelectMany(d => d.Elements("ColonyCouncil.DilemmaDef")).FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Land_SickWood");
        C(q != null && q.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_LandSick" && q.Element("options")!.Elements("li").Count() == 3, "the council's question, three answers");
        var texts = q!.Element("texts")!.Elements("li").Select(e => e.Value).ToList();
        foreach (var tag in new[] { "wilt", "rust", "rot" })
            C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*\b" + tag + @"\b")), "the question has words for the " + tag);
        string opts = q.Element("options")!.ToString();
        C(opts.Contains("DilemmaEffect_LandSickCut") && opts.Contains("DilemmaEffect_WorkParty") && opts.Contains("<li>Cut</li>") && opts.Contains("DilemmaEffect_LandSickBurn"),
            "cutting it out plays out (the marks and a work party), and so does the burn");
        C(!q.ToString().Contains("—") && !texts.Select(t => Regex.Replace(t, @"^\[[^\]]*\]\s*", "")).Any(t => t.Contains("!")), "the question's words, plainly");
        var pools = docs.SelectMany(d => d.Elements("ColonyCouncil.FlavorPoolDef")).ToList();
        C(pools.Select(e => e.Element("defName")?.Value).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_Land_Pool_Gods", "Rimfolk_Land_Pool_Said" }) && all.Contains("\"Rimfolk_Land_Pool_Gods\"")
          && all.Contains("\"Rimfolk_Land_Pool_Said\""), "the two pools, both used");
        foreach (var line in pools.SelectMany(e => e.Descendants("li")).Select(e => e.Value))
        {
            C(line.StartsWith("[") && line.Contains("]"), "every line is tagged: " + line);
            string said = Regex.Replace(line, @"^\[[^\]]*\]\s*", "");
            C(Regex.Matches(said, @"[.?](\s|$)").Count == 1 && !said.Contains("!"), "one plain sentence said aloud: " + said);
        }
        foreach (var tag in new[] { "found:wilt", "found:rust", "found:rot", "burn" })
            C(pools.Any(e => e.Element("defName")?.Value == "Rimfolk_Land_Pool_Said" && e.Descendants("li").Any(l => l.Value.StartsWith("[" + tag + "]"))), "lines said for " + tag);
        foreach (var tag in new[] { "wilt", "rust", "rot", "woods" })
            C(pools.Any(e => e.Element("defName")?.Value == "Rimfolk_Land_Pool_Gods" && e.Descendants("li").Any(l => l.Value.StartsWith("[" + tag + "]") && l.Value.Contains("{GOD}"))),
                "a believer's lines for " + tag + ", by the god's name");
        C(docs.SelectMany(d => d.Elements("JobDef")).Any(e => e.Element("defName")?.Value == "Rimfolk_Land_Burn" && e.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_LandBurn"), "the burn's job");

        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs"));
        foreach (var (id, key) in new[] { ("landRecovers", "LandRecovers"), ("landSickness", "LandSickness"), ("landWater", "LandWater") })
        {
            C(settings.Contains("public bool " + id + " = true;") && settings.Contains("Scribe_Values.Look(ref " + id + ", \"" + id + "\", true);"), id + ": on by default, kept");
            C(catalog.Contains("Toggle(\"" + id + "\", \"CC_Settings_" + key + "\", \"CC_SetNote_" + key + "\""), id + ": on the settings page under the land");
            C(keyed.ContainsKey("CC_Settings_" + key) && keyed.ContainsKey("CC_Settings_" + key + "_Tip") && keyed.ContainsKey("CC_SetNote_" + key), id + ": its words");
        }
        C(catalog.Contains("Section(\"CC_SetSec_Land\")") && keyed.ContainsKey("CC_SetSec_Land"), "the land's own section");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council_Weather.xml"));
        C(almanac.Contains("<heading>The land over the seasons</heading>") && almanac.Contains("Living Waters and Waves"), "the Almanac's paragraph");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var f in new[] { "patches", "sickCells", "water", "worn", "grazing" })
            C(estimate.Contains("\"Nature/LandBook.cs:" + f + "\"") && code["LandBook.cs"].Contains("Scribe_Collections.Look(ref " + f + ", \"" + f + "\""), "the save's model for " + f);
        C(File.Exists(Path.Combine(root, "docs", "design", "living-land-land.md")), "the design note");

        // ================= The seams =================

        C(code["LandWet.cs"].Contains("public static float SeasonWet(Map map)") && Regex.Matches(all, @"LandWet\.SeasonWet\(").Count >= 1
          && !Regex.IsMatch(code["LandWater.cs"], @"WeatherBook\.Of"), "the water reads how wet the season is through one method (the seam LandClimate can fill)");
        C(code["LandWater.cs"].Contains("IsRiver") && code["LandWater.cs"].Contains("IsOcean") && code["LandWater.cs"].Contains("WaterBodyType.Freshwater"),
            "rivers, the sea and anything joined to them are left alone");
        C(code["LandWater.cs"].Contains("areaManager.Home[c]") && code["LandWater.cs"].Contains("GetEdifice"), "the water never comes into the home area or near a building");
        C(code.Values.All(t => !Regex.IsMatch(t, @"\.TakeDamage\(|AddHediff\(|HediffMaker")), "the land hurts nobody itself (the burn's fire is the game's)");
        C(code.Values.All(t => !t.Contains("SetTerrain(") || t.Contains("map.terrainGrid.SetTerrain(")), "terrain changed only through the game's grid");
        C(code["LandPatches.cs"].Contains("[HarmonyPatch(typeof(Plant), nameof(Plant.Kill))]") && code["LandPatches.cs"].Contains("DamageDefOf.Flame")
          && code["LandPatches.cs"].Contains("nameof(Plant.PlantCollected)") && code["LandPatches.cs"].Contains("nameof(Blight.TryReproduceNow)")
          && Regex.Matches(code["LandPatches.cs"], @"RimfolkPatches\.On").Count == 3, "the hooks: a plant burned, a tree felled, the blight on a wild plant; all off with the deeper hooks");
        C(code["LandSickness.cs"].Contains("OwnHappenings.Held(p)") && code["LandSickness.cs"].Contains("OwnHappenings.Job(burner, j)") && code["LandSickness.cs"].Contains("BurnJob")
          && code["LandSickness.cs"].Contains("if (job == null"), "the burn never takes someone drafted, in a break, down or on the player's order; its job checked first");
        C(code["LandGods.cs"].Contains("Worship.LineWith(Pool") && code["LandGods.cs"].Contains("GodLore.Believes") && !code["LandGods.cs"].Contains("favour"),
            "a believer's word, kept as belief, never a god's favour shown");
        C(code["LandSickness.cs"].Contains("\"Rimfolk_WS_Blight\"") && code["LandRegrowth.cs"].Contains("PeoplesMath.Grovekeepers") && code["LandRegrowth.cs"].Contains("GoodwillWhy.Ledger"),
            "the region's blight and the Mossborn's ledger");
        string peoplesE = File.ReadAllText(Path.Combine(src, "Peoples", "PeoplesE.cs"));
        C(peoplesE.Contains("plant.Spawned && !plant.sown && plant.Blighted) return;"), "a sick wild tree felled isn't held against the colony in the ledger");
        C(all.Contains("Sched.RareLand") && all.Contains("PassClock.Time(Pass.RareLand)"), "the pass on its slot, timed");

        // Every def the land names in code is in the game or the player's mods.
        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var reader = new StreamReader(gz))
        {
            reader.ReadLine();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var f = line.Split('\t');
                if (f.Length >= 4 && (f[0] == "TerrainDef" || f[0] == "GameConditionDef" || f[0] == "WorldSituationDef") && OffForNow.Counts(root, f[3], f[2])) active.Add(f[1]);
            }
        }
        foreach (var name in new[] { "WaterShallow", "Marsh", "Mud", "Soil", "SoilRich", "MossyTerrain", "MarshyTerrain", "Gravel", "GrasslandSoil", "Sand", "PackedDirt", "WaterDeep", "Drought",
                     "VEE_Drought", "Rimfolk_WS_Blight", "Council_WS_Drought", "Council_WS_Flood" })
            C(active.Contains(name), name + " is in the player's game");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
