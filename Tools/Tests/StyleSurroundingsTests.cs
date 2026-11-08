using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Which exact things have the wrong style (the player's question, 8 November 2026): the things counted grouped by kind and
/// style, in order, the game's threshold and stage, what is shown and what is left as "and N more", the fix line by how the
/// game makes and styles things, the button and a thing's line, and the seams (the game's own helpers, the patches, the
/// setting, nothing saved and no mood changed).
/// </summary>
public static class StyleSurroundingsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "wrong-style things: " + what);
        }

        // ---------- The game's threshold and stage ----------
        C(StyleSurroundingsMath.Threshold(false, 50) == 10f && StyleSurroundingsMath.Threshold(false, 10) == 10f, "10 points either way in a room of 10 cells or more");
        C(StyleSurroundingsMath.Threshold(false, 9) == 9f && StyleSurroundingsMath.Threshold(false, 1) == 1f, "a room under 10 cells: its cell count");
        C(StyleSurroundingsMath.Threshold(true, 4) == 10f, "a doorway is never the small room: 10");
        C(StyleSurroundingsMath.Stage(-10f, 10f) == 1 && StyleSurroundingsMath.Stage(-26f, 10f) == 1 && StyleSurroundingsMath.Stage(-9.9f, 10f) == -1,
            "the bad mood at -10 or lower");
        C(StyleSurroundingsMath.Stage(10f, 10f) == 0 && StyleSurroundingsMath.Stage(9.5f, 10f) == -1 && StyleSurroundingsMath.Stage(-6f, 6f) == 1,
            "the good one at +10 or higher; a small room's own count");
        C(StyleSurroundingsMath.Radius == 24.9f && StyleSurroundingsMath.LookTicks == 900 && StyleSurroundingsMath.LookRadius == 5,
            "the game's reach (24.9 cells), how often it looks (900 ticks) and from how near (5 cells)");

        // ---------- Grouping, the game's way ----------
        var items = new List<StyleItem>();
        for (int i = 1; i <= 4; i++)
            items.Add(new StyleItem { key = "DiningChair|Animist", label = "Animist dining chair", id = i, points = -5f, fix = StyleFix.NoRebuild });
        for (int c = 0; c < 8; c++)
            items.Add(new StyleItem { key = "Table2x4|Animist", label = "Animist table", id = 9, points = -2f, fix = StyleFix.NoRebuild });
        for (int c = 0; c < 12; c++)
            items.Add(new StyleItem { key = "floor|MorbidTile", label = "Morbid tile", id = 100 + c, points = -1f, floor = true, fix = StyleFix.Replace });
        items.Add(new StyleItem { key = "EndTable|Federal", label = "Federal end table", id = 20, points = 3f, fix = StyleFix.Rebuild });
        items.Add(new StyleItem { key = "Wall|plain", label = "wall", id = 21, points = 0f });
        var groups = StyleSurroundingsMath.Group(items);
        C(groups.Count == 4, $"one group a kind and style, nothing that comes to zero ({groups.Count})");
        var chairs = groups.First(g => g.key == "DiningChair|Animist");
        C(chairs.count == 4 && chairs.points == -20f, "four Animist dining chairs: x4, -20");
        var table = groups.First(g => g.key == "Table2x4|Animist");
        C(table.count == 1 && table.points == -16f, "a table counts in every cell it covers, as the game counts it: one table, -16");
        var floor = groups.First(g => g.floor);
        C(floor.count == 12 && floor.points == -12f && floor.ids.Count == 12, "a floor by its cells");
        C(StyleSurroundingsMath.Total(items) == -45f, "the sum is the game's: every cell's points");
        var mixed = StyleSurroundingsMath.Group(new[]
        {
            new StyleItem { key = "Altar|x", label = "altar", id = 1, points = -4f },
            new StyleItem { key = "Altar|x", label = "altar", id = 2, points = 4f }
        });
        C(mixed.Count == 2, "plus and minus never share a group");

        // ---------- The order ----------
        var against = StyleSurroundingsMath.Against(groups);
        C(against.Select(g => g.label).SequenceEqual(new[] { "Animist dining chair", "Animist table", "Morbid tile" }), "against them, the biggest first");
        var forThem = StyleSurroundingsMath.For(groups);
        C(forThem.Count == 1 && forThem[0].label == "Federal end table", "and what counts for them, apart");
        var ties = StyleSurroundingsMath.Against(StyleSurroundingsMath.Group(new[]
        {
            new StyleItem { key = "b", label = "b thing", id = 1, points = -6f },
            new StyleItem { key = "a", label = "a thing", id = 2, points = -3f }, new StyleItem { key = "a", label = "a thing", id = 3, points = -3f },
            new StyleItem { key = "c", label = "c thing", id = 4, points = -6f }
        }));
        C(ties.Select(g => g.key).SequenceEqual(new[] { "a", "b", "c" }), "the same points: the most of them first, then by name");

        // ---------- What is shown ----------
        var ten = Enumerable.Range(1, 10).Select(i => new StyleGroup { key = "k" + i, label = "thing " + i, count = 1, points = -i }).ToList();
        var shown = StyleSurroundingsMath.Shown(StyleSurroundingsMath.Against(ten), StyleSurroundingsMath.MaxAgainst, out int more, out float morePoints);
        C(shown.Count == 8 && more == 2 && morePoints == -3f && shown[0].points == -10f, "eight lines, then \"and 2 more\" with what they come to");
        var few = StyleSurroundingsMath.Shown(against, StyleSurroundingsMath.MaxAgainst, out int none, out _);
        C(few.Count == 3 && none == 0, "all of them when they fit");
        C(StyleSurroundingsMath.MaxAgainst == 8 && StyleSurroundingsMath.MaxFor == 4, "at most eight against, four for");
        C(StyleSurroundingsMath.Points(3f) == "+3" && StyleSurroundingsMath.Points(-20f) == "-20" && StyleSurroundingsMath.Points(-7.46f) == "-7.5"
          && StyleSurroundingsMath.Points(0f) == "0" && StyleSurroundingsMath.Points(-0.04f) == "0", "points as +3, -20, -7.5");
        C(StyleSurroundingsMath.LineKey(chairs) == "CC_StyleNote_LineMany" && StyleSurroundingsMath.LineKey(table) == "CC_StyleNote_LineOne"
          && StyleSurroundingsMath.LineKey(floor) == "CC_StyleNote_LineFloor", "the line by what it is: many, one, or a floor's cells");

        // ---------- The words ----------
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_StyleSurroundings.xml"));
        string Text(string key)
        {
            var m = Regex.Match(keyed, "<" + key + ">(.*?)</" + key + ">", RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : null;
        }
        string Fill(string key, params string[] args)
        {
            string t = Text(key) ?? "";
            for (int i = 0; i < args.Length; i++) t = t.Replace("{" + i + "}", args[i]);
            return t;
        }
        C(Fill("CC_StyleNote_LineMany", chairs.label, chairs.count.ToString(), StyleSurroundingsMath.Points(chairs.points)) == "Animist dining chair ×4, -20",
            "\"Animist dining chair ×4, -20\"");
        C(Fill("CC_StyleNote_LineOne", table.label, "1", "-16") == "Animist table, -16" && Fill("CC_StyleNote_LineFloor", "Morbid tile", "12", "-12") == "Morbid tile, 12 cells, -12",
            "one thing alone, a floor by its cells");
        C(Fill("CC_StyleNote_More", "2", "-3") == "and 2 more, -3", "and N more");
        foreach (var k in new[]
                 {
                     "CC_Settings_StyleNotes", "CC_Settings_StyleNotes_Tip", "CC_SetNote_StyleNotes", "CC_StyleNote_Head", "CC_StyleNote_Outdoors", "CC_StyleNote_NoRoom",
                     "CC_StyleNote_Against", "CC_StyleNote_NothingAgainst", "CC_StyleNote_For", "CC_StyleNote_Total", "CC_StyleNote_Styled", "CC_StyleNote_OfIdeo",
                     "CC_StyleNote_Relic", "CC_StyleNote_Fix_Own", "CC_StyleNote_Fix_Shared", "CC_StyleNote_Fix_Plain", "CC_StyleNote_Fix_None", "CC_StyleNote_Fix_Some",
                     "CC_StyleNote_Gizmo", "CC_StyleNote_GizmoDesc", "CC_StyleNote_NothingHere", "CC_StyleNote_OnlyFloors", "CC_StyleNote_Inspect"
                 })
        {
            string t = Text(k);
            C(t != null && !t.Contains("—") && !t.Contains("!"), $"{k} is there, without dashes or exclamations");
        }
        C(Text("CC_StyleNote_Gizmo") == "Show the wrong-style things", "the button's name");
        C(Text("CC_StyleNote_Inspect") == "Against {IDEO}'s style: {NAMES}", "a thing's line: \"Against Federalism's style: Aquason, Ian\"");
        foreach (var k in new[] { "CC_StyleNote_Fix_Own", "CC_StyleNote_Fix_Shared", "CC_StyleNote_Fix_Plain", "CC_StyleNote_Fix_None", "CC_StyleNote_Fix_Some" })
            C(Text(k).Count(ch => ch == '.') == 1, $"{k}: one line");
        C(Text("CC_StyleNote_Fix_None").Contains("can't be restyled") && Text("CC_StyleNote_Fix_None").Contains("convert") && Text("CC_StyleNote_Fix_None").Contains("reform"),
            "rebuilding won't help: move, convert or reform");

        // ---------- The fix line ----------
        var R = StyleFix.Rebuild;
        var N = StyleFix.NoRebuild;
        var P = StyleFix.Replace;
        var M = StyleFix.Move;
        C(StyleSurroundingsMath.FixKey(true, false, new StyleFix[0]) == null, "nothing against them: no fix line");
        C(StyleSurroundingsMath.FixKey(true, true, new[] { R, P }) == "CC_StyleNote_Fix_Own", "their ideoligion is the main one: build them again");
        C(StyleSurroundingsMath.FixKey(true, false, new[] { R }) == "CC_StyleNote_Fix_Shared", "the main one's style doesn't go against theirs: build them again");
        C(StyleSurroundingsMath.FixKey(true, false, new[] { N, N }) == "CC_StyleNote_Fix_None" && StyleSurroundingsMath.FixKey(true, false, new[] { N, P, M }) == "CC_StyleNote_Fix_None",
            "the main one's style is another: rebuilding won't help (move, convert, reform)");
        C(StyleSurroundingsMath.FixKey(true, false, new[] { N, R }) == "CC_StyleNote_Fix_Some", "some would help, the rest not");
        C(StyleSurroundingsMath.FixKey(true, false, new[] { P, M }) == "CC_StyleNote_Fix_Plain" && StyleSurroundingsMath.FixKey(false, false, new[] { N }) == "CC_StyleNote_Fix_Plain",
            "only another's own things or items, or no main ideoligion: move them or take them down");
        var someGroups = StyleSurroundingsMath.Against(StyleSurroundingsMath.Group(new[]
        {
            new StyleItem { key = "a", label = "Totemic stool", id = 1, points = -3f, fix = R },
            new StyleItem { key = "b", label = "Animist chair", id = 2, points = -9f, fix = N },
            new StyleItem { key = "c", label = "Morbid lamp", id = 3, points = -6f, fix = R },
            new StyleItem { key = "d", label = "Rustic shelf", id = 4, points = -2f, fix = R },
            new StyleItem { key = "e", label = "Spikecore bench", id = 5, points = -1f, fix = R }
        }));
        C(StyleSurroundingsMath.RebuildLabels(someGroups).SequenceEqual(new[] { "Morbid lamp", "Totemic stool", "Rustic shelf" }), "the \"some\" line names up to three, the biggest first");

        // ---------- The button and a thing's line ----------
        C(StyleSurroundingsMath.ButtonShows(true, true, true, 1) && !StyleSurroundingsMath.ButtonShows(true, true, true, 0) && !StyleSurroundingsMath.ButtonShows(true, true, true, -1),
            "the button only with the bad mood");
        C(!StyleSurroundingsMath.ButtonShows(false, true, true, 1) && !StyleSurroundingsMath.ButtonShows(true, false, true, 1) && !StyleSurroundingsMath.ButtonShows(true, true, false, 1),
            "never off, on someone not the colony's, or without the mood's precept");
        C(StyleSurroundingsMath.ThingCounts(2f) && !StyleSurroundingsMath.ThingCounts(0f), "a thing's line only for a thing that counts at all");

        // ---------- The seams ----------
        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string code = src("Moods/StyleSurroundings.cs"), settings = src("CouncilSettings.cs"), catalog = src("UI/SettingsCatalog.cs");
        C(code.Contains("AccessTools.Method(typeof(IdeoUtility), \"GetStyleDominance\", new[] { typeof(Thing), typeof(Ideo) })")
          && code.Contains("AccessTools.Method(typeof(IdeoUtility), \"GetJoinedRooms\""), "the game's own private rule and joined rooms, by name, so the points can't drift");
        C(code.Contains("ideo.cachedPossibleBuildables.Contains(terrain)") && code.Contains("!terrain.canGenerateDefaultDesignator") && code.Contains("GenRadial.NumCellsInRadius(StyleSurroundingsMath.Radius)")
          && code.Contains("c.Fogged(map)") && code.Contains("rooms.Contains(cr)"), "floors and cells as the game counts them: within 24.9, unfogged, in the joined rooms");
        C(code.Contains("[HarmonyPatch(typeof(ThoughtWorker_StyleDominance), nameof(ThoughtWorker_StyleDominance.PostProcessDescription))]"),
            "the lines go under the game's own style mood, every diversity level's");
        C(code.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]") && code.Contains("Find.Selector.Select(t, false, false)") && code.Contains("CameraJumper.TryJump(first)"),
            "the button selects them, the camera moved when the first is out of view");
        C(code.Contains("[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]") && code.Contains("StatDefOf.StyleDominance"),
            "a styled thing's inspect line");
        C(code.Contains("ModsConfig.IdeologyActive") && code.Contains("im.classicMode") && code.Contains("RimfolkPatches.On") && code.Contains("styleNotes"),
            "only with Ideology outside classic mode, the setting and the hooks");
        C(code.Contains("Now / Sched.RareTicks") && code.Contains("c.bucket == b") && code.Contains("thingLinesBucket != Bucket"), "worked out once every 250 ticks, nothing each frame");
        C(!code.Contains("Scribe_") && !code.Contains("TryGainMemory") && !code.Contains("Notify_SituationalThoughtsDirty") && !code.Contains("styleDominanceThoughtIndex ="),
            "nothing saved, no mood changed");
        C(!code.Contains("Messages.Message(") || Regex.Matches(code, @"Messages\.Message\([^;]*MessageTypeDefOf\.RejectInput").Count == Regex.Matches(code, @"Messages\.Message\(").Count,
            "a direct message only to answer the button");
        C(settings.Contains("public bool styleNotes = true;") && settings.Contains("Scribe_Values.Look(ref styleNotes, \"styleNotes\", true);")
          && catalog.Contains("Toggle(\"styleNotes\", \"CC_Settings_StyleNotes\", \"CC_SetNote_StyleNotes\", \"deepHooks\")")
          && catalog.IndexOf("Toggle(\"styleNotes\"", StringComparison.Ordinal) > catalog.IndexOf("Section(\"CC_SetSec_OnTheMap\")", StringComparison.Ordinal),
            "its setting, on by default and saved, with what shows on the map and in the game's tabs");
        return n;
    }
}
