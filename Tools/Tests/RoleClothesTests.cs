using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;
using R = ColonyCouncil.RoleClothesMath;

/// <summary>
/// Role clothes the colony can reach (the player, 9 November 2026: "i keep getting toga as mandatory gear but it's not
/// really a standard research or anything worth spending 1200 research points into... across several playthroughs and
/// ideologies already"). The rule first (each age's usual cost, off the usual path, dear for its age, a way and a piece in
/// or out of reach, what keeps a piece, the role's pool, the dialog's order, the share done), with the player's own mods'
/// numbers and their save's Inquisitor; then the seams: the game's choice patched for the colony's ideoligions only and
/// standing down safely, the mood's advice and button, the dialog editing the role as the game's editor does, the
/// setting, the hint, and the words.
/// </summary>
public static class RoleClothesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "role clothes the colony can reach: " + what);
        }

        Ages(C);
        Rule(C);
        PlayersCase(C);
        PoolAndOrder(C);
        Random(C);
        Seams(root, C);
        return n;
    }

    /// <summary>The player's main research tab (docs/player-mods, 9 November 2026): the median cost of each age.</summary>
    private static readonly Dictionary<int, float> Players = new Dictionary<int, float>
    {
        [R.Neolithic] = 500f, [R.Medieval] = 600f, [R.Industrial] = 1000f, [R.Spacer] = 2500f, [R.Ultra] = 2000f
    };

    private static R.Research Proj(string name, int age, float cost, bool main = true, bool done = false, float progress = 0f, bool own = false, bool game = false,
        bool mod = false) =>
        new R.Research { name = name, age = age, cost = cost, shownCost = cost, mainTab = main, finished = done, progress = progress, ownStart = own, gameStart = game, modStart = mod };

    private static R.Way Way(string bench, bool built, params R.Research[] research)
    {
        var w = new R.Way { bench = bench, benchBuilt = built };
        w.research.AddRange(research);
        return w;
    }

    private static R.Item Item(string name, int has = 0, bool calledFor = false, params R.Way[] ways)
    {
        var it = new R.Item { name = name, has = has, calledFor = calledFor };
        it.ways.AddRange(ways);
        return it;
    }

    private static void Ages(Action<bool, string> C)
    {
        C(R.Median(new float[] { 300, 500, 1000 }) == 500f && R.Median(new float[] { 400, 600 }) == 500f && R.Median(new float[0]) == 0f && R.Median(null) == 0f,
            "the median of an odd and an even count, none for none");
        C(R.Median(new float[] { 0, 0, 800 }) == 800f, "a project costing nothing (a knowledge project) doesn't count");
        var med = R.Medians(new[] { (R.Neolithic, 300f), (R.Neolithic, 500f), (R.Neolithic, 1000f), (R.Medieval, 700f), (R.Industrial, 0f) });
        C(med.Count == 2 && med[R.Neolithic] == 500f && med[R.Medieval] == 700f, "each age's median from the main tab's projects, an age of free projects left out");
        C(R.Typical(R.Neolithic, Players) == 500f && R.Typical(R.Spacer, Players) == 2500f, "an age's own median");
        C(R.Typical(R.Archotech, Players) == 2000f, "an age without one takes the nearest below (archotech: ultra's)");
        C(R.Typical(R.Animal, Players) == 500f && R.Typical(R.Undefined, Players) == 500f, "below every age with one, the nearest above (neolithic's)");
        C(R.Typical(R.Medieval, new Dictionary<int, float>()) == R.FallbackTypical && R.Typical(R.Medieval, null) == R.FallbackTypical, "no medians at all: the fallback");
    }

    private static void Rule(Action<bool, string> C)
    {
        // Off the usual path.
        var togas = Proj("Togas", R.Neolithic, 1200f, main: false, mod: true);
        C(R.OffPath(togas), "VFE Classical's togas: in a tab of its own and a modded start's project, off the path");
        C(R.OffPath(Proj("x", R.Industrial, 600f, main: false)), "any project in a tab of its own is off the path");
        C(!R.OffPath(Proj("Tree sowing", R.Neolithic, 1000f, game: true)), "tree sowing, the game's tribal start's project on the main tab: on the path");
        C(R.OffPath(Proj("Elven equipment", R.Medieval, 700f, mod: true)), "a modded start's project on the main tab (wood elves'): off the path");
        C(!R.OffPath(Proj("Elven equipment", R.Medieval, 700f, mod: true, own: true)), "unless the colony started as that start");
        C(!R.OffPath(Proj("x", R.Neolithic, 500f, mod: true, game: true)), "a project the game's own starts share with a modded one stays on the path");
        C(!R.OffPath(Proj("Smithing", R.Medieval, 700f)) && !R.OffPath(null), "an ordinary main-tab project is on the path");

        // Dear for its age.
        C(R.Dear(togas, Players), "1200 for a neolithic project, where 500 is usual: dear");
        C(R.Dear(Proj("x", R.Neolithic, 750f), Players) && !R.Dear(Proj("x", R.Neolithic, 749f), Players), "dear from half again the usual (750 for neolithic)");
        C(!R.Dear(Proj("Casual wear", R.Industrial, 600f, main: false), Players), "Vanilla Apparel Expanded's casual wear, 600 at industrial: not dear");
        C(R.Dear(Proj("Siegebreaker armor", R.Spacer, 6000f, main: false), Players), "its siegebreaker armour, 6000 at spacer: dear");
        C(!R.Dear(Proj("x", R.Spacer, 0f, main: false), Players), "a project with no research cost (a knowledge one) is never dear");

        // All three, unfinished.
        C(R.Blocks(togas, Players), "unfinished, off the path and dear: out of reach");
        C(!R.Blocks(Proj("Togas", R.Neolithic, 1200f, main: false, mod: true, done: true), Players), "finished: in reach");
        C(!R.Blocks(Proj("Tree sowing", R.Neolithic, 1000f, game: true), Players), "dear but on the path: in reach");
        C(!R.Blocks(Proj("Casual wear", R.Industrial, 600f, main: false), Players), "off the path but cheap: in reach");
        C(!R.Blocks(Proj("Rich main", R.Spacer, 6000f), Players), "dear on the main tab: in reach (the game's own path)");
        C(!R.Blocks(null, Players), "nothing blocks nothing");

        // A piece.
        var spot = Way("crafting spot", true, togas);
        C(R.Of(Item("toga", 0, false, spot), Players) == R.Reach.OutOfReach, "the toga, made only after togas: out of reach");
        C(R.Of(Item("toga", 1, false, spot), Players) == R.Reach.Has, "unless the colony has one");
        C(R.Of(Item("toga", 0, true, spot), Players) == R.Reach.CalledFor && R.Reachable(R.Reach.CalledFor), "or the ideoligion calls for it: kept");
        var tailor = Way("hand tailoring bench", false, Proj("Complex clothing", R.Medieval, 600f, done: true), togas);
        C(R.Of(Item("toga", 0, false, spot, tailor), Players) == R.Reach.OutOfReach, "every way needs togas: still out of reach");
        var other = Way("loom", false, Proj("Weaving", R.Medieval, 800f, main: false));
        C(R.Of(Item("toga", 0, false, spot, other), Players) == R.Reach.Research, "a second way through research in reach: in reach, needing research");
        var finished = Way("crafting spot", true, Proj("Togas", R.Neolithic, 1200f, main: false, mod: true, done: true));
        C(R.Of(Item("toga", 0, false, finished), Players) == R.Reach.Ready, "togas finished: can be made now");
        C(R.Of(Item("headscarf", 0, false, Way("hand tailoring bench", true)), Players) == R.Reach.Ready, "a way needing no research: can be made now");
        C(R.Of(Item("crown"), Players) == R.Reach.Unmade && R.Reachable(R.Reach.Unmade), "nothing makes it: not judged, kept");
        C(R.Of(null, Players) == R.Reach.Unmade, "no piece: not judged");

        // The best of several pieces (a requirement by tags).
        C(R.Best(new[] { R.Reach.OutOfReach, R.Reach.Research }) == R.Reach.Research, "a requirement any piece of which is in reach is in reach");
        C(R.Best(new[] { R.Reach.OutOfReach, R.Reach.OutOfReach }) == R.Reach.OutOfReach, "every piece out of reach: out of reach");
        C(R.Best(new R.Reach[0]) == R.Reach.Unmade && R.Best(null) == R.Reach.Unmade, "no pieces at all: not judged");

        // The way to tell of, and what it still needs.
        var it = Item("toga", 0, false, Way("loom", false, Proj("Weaving", R.Medieval, 800f, main: false)), Way("crafting spot", true, togas),
            Way("tailor", false, Proj("Sewing", R.Medieval, 600f, progress: 0.5f)));
        C(R.BestWay(it, Players) == 2, "the way in reach with the least research left (sewing, half done)");
        var open = Item("x", 0, false, Way("electric bench", false), Way("crafting spot", true));
        C(R.BestWay(open, Players) == 1, "an open way at a bench the colony has built first");
        C(R.BestWay(Item("x", 0, false, spot), Players) == 0 && R.BestWay(Item("x"), Players) == -1, "only a way out of reach: that one; none: none");
        var two = Way("bench", false, Proj("Small", R.Medieval, 300f), Proj("Big", R.Medieval, 900f, progress: 0.1f), Proj("Done", R.Medieval, 500f, done: true));
        var missing = R.Missing(two);
        C(missing.Count == 2 && missing[0].name == "Big" && missing[1].name == "Small", "what a way still needs, the most left first, the finished left out");
        C(Math.Abs(R.Left(two) - (300f + 810f)) < 0.01f, "what is left of a way, in the cost the research screen shows");
        C(R.Blockers(Item("toga", 0, false, spot, tailor), Players).Count == 1, "the project that puts it out of reach, named once");

        // The share done.
        C(R.Percent(0f, false) == 0 && R.Percent(0.004f, false) == 1 && R.Percent(0.4f, false) == 40 && R.Percent(0.999f, false) == 99 && R.Percent(1f, true) == 100
          && R.Percent(-1f, false) == 0 && R.Percent(5f, false) == 99, "the share done: none, begun, part, never 100 before finished");
    }

    /// <summary>The player's save, League of Toba (9 November 2026): Federalism's Inquisitor asks for VFE Classical's toga.</summary>
    private static void PlayersCase(Action<bool, string> C)
    {
        // Their research: togas not started (1200, neolithic, the VanillaExpanded tab, ClassicalStart only); complex clothing finished.
        var togas = Proj("Togas", R.Neolithic, 1200f, main: false, mod: true);
        var complex = Proj("Complex clothing", R.Medieval, 600f, done: true, own: true);
        var smithing = Proj("Smithing", R.Medieval, 700f, done: true);
        var toga = Item("toga", 0, false, Way("electric tailoring bench", true, complex, togas), Way("hand tailoring bench", false, complex, togas), Way("crafting spot", true, togas));
        var wreath = Item("wreath", 0, false, Way("fueled smithy", true, togas, smithing), Way("electric smithy", false, togas, smithing));
        // Vanilla Ideology Expanded - Hats and Rags' headscarf and fez need no research; the colony wears one headscarf.
        var headscarf = Item("headscarf", 1, false, Way("hand tailoring bench", false), Way("electric tailoring bench", true));
        var fez = Item("fez", 0, false, Way("hand tailoring bench", false), Way("electric tailoring bench", true));
        var peltCoat = Item("pelt coat", 1, false, Way("hand tailoring bench", false));
        C(R.Of(toga, Players) == R.Reach.OutOfReach && R.Of(wreath, Players) == R.Reach.OutOfReach, "their toga and wreath are out of reach");
        C(R.Of(headscarf, Players) == R.Reach.Has && R.Of(fez, Players) == R.Reach.Ready && R.Of(peltCoat, Players) == R.Reach.Has,
            "the headscarf and pelt coat they have, the fez they can make now");
        var pool = new List<R.Reach> { R.Of(toga, Players), R.Of(wreath, Players), R.Of(headscarf, Players), R.Of(fez, Players) };
        C(R.Kept(pool).SequenceEqual(new[] { 2, 3 }), "the role's first draw keeps the headscarf and the fez, leaves out the toga and the wreath");
        // A roman culture (VFE Classical's own) keeps them.
        var roman = Item("toga", 0, true, Way("crafting spot", true, togas));
        C(R.Kept(new List<R.Reach> { R.Of(roman, Players) }).Count == 1, "an ideoligion of VFE Classical's roman culture keeps the toga");
        // With VFE Classical's own start (ClassicalStart), the start gives togas.
        var classical = Item("toga", 0, false, Way("crafting spot", true, Proj("Togas", R.Neolithic, 1200f, main: false, mod: true, own: true, done: true)));
        C(R.Of(classical, Players) == R.Reach.Ready, "VFE Classical's own scenario starts with togas: the toga can be made");
        var choices = new List<R.Choice>
        {
            new R.Choice(R.Of(wreath, Players), 1200f, "Wreath"), new R.Choice(R.Of(fez, Players), 0f, "Fez"), new R.Choice(R.Of(headscarf, Players), 0f, "Headscarf"),
            new R.Choice(R.Of(peltCoat, Players), 0f, "Pelt coat", false)
        };
        var order = R.Order(choices).Select(i => choices[i].name).ToList();
        C(order.SequenceEqual(new[] { "Headscarf", "Fez", "Pelt coat", "Wreath" }),
            "the dialog lists what they have first, then what can be made, then one that can't be worn with the rest, the wreath out of reach last (" + string.Join(", ", order) + ")");
    }

    private static void PoolAndOrder(Action<bool, string> C)
    {
        C(R.Kept(null).Count == 0 && R.Kept(new List<R.Reach>()).Count == 0, "an empty pool stays empty");
        var all = new List<R.Reach> { R.Reach.Has, R.Reach.Ready, R.Reach.Research, R.Reach.Unmade, R.Reach.CalledFor };
        C(R.Kept(all).Count == all.Count, "everything in reach is kept, in order");
        C(R.Kept(new List<R.Reach> { R.Reach.OutOfReach, R.Reach.OutOfReach }).Count == 0,
            "all out of reach: the first draw has nothing, and the game's desperate draw picks from everything");
        var choices = new List<R.Choice>
        {
            new R.Choice(R.Reach.Research, 900f, "b"), new R.Choice(R.Reach.Research, 300f, "c"), new R.Choice(R.Reach.OutOfReach, 0f, "a"), new R.Choice(R.Reach.Has, 0f, "z"),
            new R.Choice(R.Reach.Ready, 0f, "y", false), new R.Choice(R.Reach.Research, 300f, "B")
        };
        var order = R.Order(choices).Select(i => choices[i].name).ToList();
        C(order.SequenceEqual(new[] { "z", "B", "c", "b", "y", "a" }), "in reach and allowed first by reach and research left, then name; not allowed, then out of reach (" + string.Join(",", order) + ")");
        C(R.Order(null).Count == 0, "no choices, no order");
    }

    /// <summary>Hundreds of pieces made at random: the rule holds as written.</summary>
    private static void Random(Action<bool, string> C)
    {
        var rng = new System.Random(9112026);
        bool holds = true, keptHolds = true;
        for (int t = 0; t < 600; t++)
        {
            var ways = new List<R.Way>();
            int wn = rng.Next(0, 4);
            for (int w = 0; w < wn; w++)
            {
                var rs = new List<R.Research>();
                int rn = rng.Next(0, 3);
                for (int r = 0; r < rn; r++)
                    rs.Add(Proj("r" + r, rng.Next(0, 8), rng.Next(0, 5) * 400f, rng.Next(2) == 0, rng.Next(3) == 0, (float)rng.NextDouble(), rng.Next(4) == 0, rng.Next(4) == 0,
                        rng.Next(2) == 0));
                ways.Add(Way("b", rng.Next(2) == 0, rs.ToArray()));
            }
            var item = Item("i", rng.Next(4) == 0 ? 1 : 0, rng.Next(5) == 0, ways.ToArray());
            var reach = R.Of(item, Players);
            bool allBlocked = ways.Count > 0 && ways.All(w => w.research.Any(r => !r.finished && (!r.mainTab || r.modStart && !r.ownStart && !r.gameStart)
                                                                                         && r.cost > 0f && r.cost >= R.DearFactor * R.Typical(r.age, Players)));
            bool shouldBeOut = item.has == 0 && !item.calledFor && allBlocked;
            if ((reach == R.Reach.OutOfReach) != shouldBeOut) holds = false;
            if (R.Kept(new List<R.Reach> { reach }).Count != (shouldBeOut ? 0 : 1)) keptHolds = false;
        }
        C(holds, "600 random pieces: out of reach exactly when the colony has none, nothing calls for it, and every way needs an unfinished project off the path and dear");
        C(keptHolds, "and the role's first draw leaves out exactly those");
    }

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string math = Code("Faith/RoleClothesMath.cs"), game = Code("Faith/RoleClothes.cs"), patch = Code("Faith/RoleClothesPatch.cs"), window = Code("Faith/RoleClothesWindow.cs");
        string view = Code("Moods/MoodView.cs"), insight = Code("Moods/MoodInsight.cs"), settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");

        // Pure, and tried here.
        C(!Regex.IsMatch(math, @"using (RimWorld|Verse|UnityEngine)"), "the rule is free of game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Source/ColonyCouncil/Faith/RoleClothesMath.cs"), "and linked into the tests");

        // The game's choice: patched as classes, standing down safely, the colony's ideoligions only, the first draw only.
        C(patch.Contains("[HarmonyPatch(typeof(Precept_Role), nameof(Precept_Role.GenerateNewApparelRequirements))]")
          && patch.Contains("[HarmonyPatch(typeof(Precept_Role), nameof(Precept_Role.AllPossibleRequirements))]"), "two patch classes on the game's own choice of a role's clothes");
        C(Regex.Matches(patch, @"public static bool Prepare\(\)").Count == 2 && patch.Contains("m.DeclaringType == typeof(Precept_Role)")
          && patch.Contains("ps[0].Name == \"ideo\" && ps[1].Name == \"def\" && ps[2].Name == \"desperate\""), "each checks the game's method is as it expects, or isn't applied");
        C(patch.Contains("__state = RoleClothes.choosingFor;") && patch.Contains("public static void Finalizer(Ideo __state)") && patch.Contains("RoleClothes.choosingFor = __state;"),
            "the choice is marked while it runs and the mark put back whatever happens");
        C(patch.Contains("RoleClothes.On && RoleClothes.ForColony(__instance, generatingFor) ? __instance.ideo : null"), "marked only for the colony, with the setting on");
        C(patch.Contains("if (desperate || __result == null") && patch.Contains("if (RoleClothes.choosingFor != ideo || !RoleClothes.On) return;"),
            "the pool is filtered only in the first draw of the colony's own role, never the desperate one nor the editor's list");
        C(game.Contains("if (generatingFor != null) return generatingFor.isPlayer;") && game.Contains("return ColonyIdeo(role.ideo);")
          && game.Contains("Faction.OfPlayerSilentFail?.ideos?.Has(ideo) == true"), "the colony's: made for the player's faction, or an ideoligion the colony holds; never a faction's own");
        C(game.Contains("[ThreadStatic] internal static Ideo choosingFor;"), "each thread its own mark (ideoligions are made on a long event's thread too)");
        C(game.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.roleClothesReach ?? true);"), "behind the setting and the deeper hooks");
        string filter = Between(game, "public static void FilterPool(", "// ---------------- The mood");
        C(filter.Contains("RoleClothesMath.Kept(reaches)") && filter.Contains("pool.Clear();") && filter.IndexOf("RoleClothesMath.Kept", StringComparison.Ordinal) < filter.IndexOf("pool.Clear();", StringComparison.Ordinal),
            "the pool is worked out whole before it is changed");

        // The game read into the rule.
        C(game.Contains("p.tab == null || p.tab == ResearchTabDefOf.Main") && game.Contains("p.knowledgeCategory == null && p.baseCost > 0f"),
            "each age's usual cost from the main tab's projects bought with research");
        C(game.Contains("f.isPlayer") && game.Contains("f.modContentPack?.IsOfficialMod") && game.Contains("mods.ExceptWith(game);"), "the start tags: the game's own starts' and the modded starts'");
        C(game.Contains("r.finished = p.IsFinished;") && game.Contains("else r.finished = own || memeStarts.Contains(p);"),
            "finished in play; before the game begins, given by the colony's start or a meme");
        C(game.Contains("recipe.AllRecipeUsers") && game.Contains("bench?.researchPrerequisites") && game.Contains("recipe.researchPrerequisites"),
            "every way: each recipe at each bench, with the recipe's and the bench's research");
        C(game.Contains("PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists") && game.Contains("!t.IsForbidden(Faction.OfPlayer)"),
            "what the colony has: worn by its people, or on its homes not forbidden");
        C(game.Contains("p is Precept_Apparel pa && pa.apparelDef == def") && game.Contains("thingDefStyles") && game.Contains("ideo.culture?.modContentPack == pack")
          && game.Contains("!pack.IsOfficialMod") && game.Contains("m.apparelRequirements.Any(x => SameAs(x, req))"),
            "what the ideoligion calls for: a desired apparel, a style's own look, a meme's own clothes, its culture, memes, styles or role from the piece's mod");

        // The mood: what it needs, and the button, for the colony's own ideoligions.
        C(game.Contains("(t as Thought_IdeoRoleApparelRequirementNotMet)?.sourcePrecept as Precept_Role") && game.Contains("ColonyIdeo(role.ideo) ? role : null"),
            "the mood of a role's clothes, of the colony's own ideoligions only");
        C(game.Contains("req.IsActive(p) && !req.IsMet(p)"), "the pieces they lack, as the game's mood counts them");
        C(insight.Contains("RoleClothes.RoleOf(l.thought) is Precept_Role role") && insight.Contains("RoleClothes.Advice(l.pawn, role)"), "the mood's advice says each piece and how to come by it");
        C(view.Contains("t.roleClothes = RoleClothes.RoleOf(l.thought);") && view.Contains("new Dialog_RoleClothes(t.roleClothes, t.roleClothesPawn)")
          && view.Contains("\"CC_RoleClothes_Button\""), "the mood's card has \"Choose other clothes for the role\"");
        int roleButton = view.IndexOf("new Dialog_RoleClothes(", StringComparison.Ordinal), readMore = view.IndexOf("MainTabWindow_Council.OpenAlmanac(t.hint.article)", StringComparison.Ordinal);
        C(roleButton > 0 && readMore > roleButton, "its button first among the card's, so it fits");
        C(view.Contains("c.lines.All(l => RoleClothes.RoleOf(l.thought) == role)"), "the colony's mood list has it only when everyone's is the one role");

        // The dialog: the game's own pool, the game's own checks, the game's own way of editing.
        C(window.Contains("class Dialog_RoleClothes : FramedWindow") && window.Contains(": base(\"RoleClothes\")") && FramedWindowsMath.TryKey("RoleClothes", out _),
            "the dialog in Rimfolk's frame, with its least size");
        C(window.Contains("Precept_Role.AllPossibleRequirements(role.ideo, role.def, true)"), "the alternatives are the pool the game's editor offers for that role and ideoligion");
        C(window.Contains("row.req.CanAddRequirement(role, others, out string reason)") && window.Contains("row.req.RequirementOverlapsOther(others, out string clash)"),
            "those the game wouldn't take are greyed with its own reason");
        C(window.Contains("RoleClothesMath.Order(") && window.Contains("Widgets.ThingIcon(") && window.Contains("row.has") && window.Contains("row.how"),
            "in reach first, each with its icon, how to come by it and whether the colony has one");
        C(window.Contains("RoleClothes.ColonyIdeo(role.ideo)") && window.Contains("\"CC_RoleClothes_EditNote\""), "the colony's own ideoligions only, and it says it edits the role");
        C(Regex.Matches(window, @"RoleClothes\.Replace\(").Count == 1 && Between(window, "private void Confirm()", "\n        }").Contains("RoleClothes.Replace("),
            "nothing changes until the player confirms");
        C(window.Contains("whyNot = memeWhy ??") && window.Contains("\"CannotNotAddRoleApparelDueToMeme\""), "a meme that forbids role clothes forbids the change, in the game's words");
        string replace = Between(game, "public static bool Replace(", "\n        }\n    }");
        C(replace.Contains("with.CopyTo(copy);") && replace.Contains("role.ApparelRequirements = list;") && replace.Contains("p.ClearTipCache();")
          && replace.Contains("RequirementOverlapsOther(list, out _)") && replace.Contains("\"OverlappingRoleApparel\""), "replaced as the game's editor does, with its own check");
        C(replace.Contains("p?.apparel?.Notify_RoleChanged();"), "whoever holds the role reads their clothes again (the game's mood follows on its own)");

        // The setting.
        C(settings.Contains("public bool roleClothesReach = true;") && settings.Contains("Scribe_Values.Look(ref roleClothesReach, \"roleClothesReach\", true);"), "a setting, on");
        C(catalog.Contains("Toggle(\"roleClothesReach\", \"CC_Settings_RoleClothesReach\", \"CC_SetNote_RoleClothesReach\", \"deepHooks\");"),
            "under faiths from other mods, needing the deeper hooks");
        C(SettingsCatalog.Find("roleClothesReach")?.page == SettingsPage.OtherMods && SettingsCatalog.Find("roleClothesReach")?.section == "CC_SetSec_Mod_Faiths",
            "on the page of other mods, always shown");

        // The hint.
        var hints = XDocument.Load(Path.Combine(root, "Defs", "MoodHintDefs", "Council_MoodHints.xml")).Root!.Elements("ColonyCouncil.MoodHintDef").ToList();
        var own = hints.FirstOrDefault(h => (string)h.Element("defName") == "Council_Mood_RoleClothes");
        C(own != null && own.Element("thoughts")!.Elements("li").Any(l => l.Value == "IdeoRoleApparelRequirementNotMet") && (string)own.Element("act") == "Get the role's clothes",
            "the mood has its own hint");
        C(hints.Where(h => (string)h.Element("defName") != "Council_Mood_RoleClothes").All(h => h.Element("thoughts")?.Elements("li").All(l => l.Value != "IdeoRoleApparelRequirementNotMet") ?? true),
            "and no other hint claims it");

        // The words.
        var keys = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_RoleClothes.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        string code = game + window + view + catalog;
        foreach (Match m in Regex.Matches(code, "\"(CC_(?:RoleClothes_\\w+|Settings_RoleClothesReach|SetNote_RoleClothesReach))\""))
            C(keys.ContainsKey(m.Groups[1].Value), $"the key {m.Groups[1].Value} exists");
        C(keys.ContainsKey("CC_Settings_RoleClothesReach_Tip"), "the setting has its tooltip");
        foreach (var kv in keys)
        {
            C(!kv.Value.Contains('—') && !kv.Value.Contains('–') && !kv.Value.Contains('!'), $"{kv.Key}: no dashes or exclamation marks");
            // Every argument a key is given is in its text.
            foreach (string args in Arguments(code, "\"" + kv.Key + "\".Translate("))
                foreach (Match named in Regex.Matches(args, "Named\\(\"(\\w+)\"\\)"))
                    C(kv.Value.Contains("{" + named.Groups[1].Value + "}"), $"{kv.Key}: says its {named.Groups[1].Value}");
        }
        C(keys["CC_RoleClothes_Line"] == "{ITEM}: {HOW}." && keys["CC_RoleClothes_ResearchPart"] == "{RESEARCH} ({COST}), {STATE}" && keys["CC_RoleClothes_NotStarted"] == "not started"
          && keys["CC_RoleClothes_How_Research"] == "needs the research {RESEARCH}", "the line reads \"Toga: needs the research Togas (1200), not started.\"");
        C(keys["CC_RoleClothes_Button"] == "Choose other clothes for the role" && keys["CC_Settings_RoleClothesReach"] == "Role clothes the colony can reach", "the button and the setting as named");
    }

    /// <summary>What each call starting with <paramref name="call"/> is given: the text up to its closing bracket.</summary>
    private static IEnumerable<string> Arguments(string code, string call)
    {
        for (int at = code.IndexOf(call, StringComparison.Ordinal); at >= 0; at = code.IndexOf(call, at + 1, StringComparison.Ordinal))
        {
            int start = at + call.Length, depth = 1, i = start;
            for (; i < code.Length && depth > 0; i++)
            {
                if (code[i] == '(') depth++;
                else if (code[i] == ')') depth--;
            }
            yield return code.Substring(start, Math.Max(0, i - 1 - start));
        }
    }

    private static string Between(string s, string from, string to)
    {
        int a = s.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = s.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? s.Substring(a) : s.Substring(a, b - a);
    }
}
