using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Trading posts (World/Region/TradingPostMath.cs; docs/design/stage1-region.md, item 3; decision 6): which people opens
/// one (goodwill, trade, its towns far, town traders, one per people), when (the look, its chance, two a home, the gap
/// after an opening or a closing), where (6 to 12 tiles, clear of homes and towns, toward their town, roads and rivers
/// first, the heading on the sphere), its name and keeper by age, the souring clock day by day, its sacking, caravans
/// and the bandit road, the history's kinds. Also the mod's side: the region's day runs it, its save and model, Tell
/// only, straight-line tile searches, the game's own objects, the words, the setting, the hooks and the talk.
/// </summary>
public static class TradingPostTests
{
    private const int D = TradingPostMath.DayTicks;

    private static bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) < eps;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "trading posts: " + what);
        }

        // ---------------- Who opens one ----------------

        C(TradingPostMath.PeopleFits(true, false, 40, false, 3, 15f, true, false), "goodwill 40, three trades, its nearest town 15 tiles off, town traders: it may open one");
        C(!TradingPostMath.PeopleFits(true, false, 39, false, 3, 20f, true, false) && TradingPostMath.PeopleFits(true, false, 5, true, 3, 20f, true, false),
            "under 40 only as an ally");
        C(!TradingPostMath.PeopleFits(true, false, 80, false, 2, 20f, true, false), "fewer than three trades in the last 120 days: no");
        C(!TradingPostMath.PeopleFits(true, false, 80, false, 5, 14.9f, true, false) && !TradingPostMath.PeopleFits(true, false, 80, false, 5, -1f, true, false),
            "its towns near (under 15 tiles), or none on the home's layer to face toward: no");
        C(!TradingPostMath.PeopleFits(true, true, 80, true, 5, 20f, true, false), "never a people at war with the colony");
        C(!TradingPostMath.PeopleFits(true, false, 80, false, 5, 20f, false, false) && !TradingPostMath.PeopleFits(false, false, 80, false, 5, 20f, true, false),
            "only a humanlike people that keeps town traders");
        C(!TradingPostMath.PeopleFits(true, false, 80, false, 5, 20f, true, true), "one post per people at a home");
        C(TradingPostMath.MinTrades == 3 && TradingPostMath.TradeDays == 120 && TradingPostMath.MinGoodwill == 40 && Near(TradingPostMath.FarTiles, 15f), "the design's numbers");
        C(TradingPostMath.Pick(new[] { 3, 7, 7 }, new[] { 90, 50, 60 }) == 2, "the most trade first, then the best goodwill");
        C(TradingPostMath.Pick(new[] { 4, 4 }, new[] { 60, 60 }) == 0 && TradingPostMath.Pick(new int[0], new int[0]) == -1 && TradingPostMath.Pick(null, null) == -1,
            "the first of equals; none for nobody");

        // ---------------- When ----------------

        C(TradingPostMath.LookDue(-1, 0) && !TradingPostMath.LookDue(0, 14 * D) && TradingPostMath.LookDue(0, 15 * D), "a home is looked at every 15 days");
        C(Near(TradingPostMath.Chance, 0.2f), "20% at a look when a people qualifies");
        C(TradingPostMath.HomeAllows(0, -1, 0) && TradingPostMath.HomeAllows(1, -1, 0) && !TradingPostMath.HomeAllows(2, -1, 0), "two posts a home at most");
        C(!TradingPostMath.HomeAllows(0, 10 * D, 69 * D) && TradingPostMath.HomeAllows(0, 10 * D, 70 * D), "none within 60 days of another opening or closing there");
        SimulateLooks(C);

        // ---------------- Where ----------------

        C(TradingPostMath.PlaceFits(true, 6f, 6.6f, 5, -1f, 1f) && TradingPostMath.PlaceFits(true, 12f, 12f, 5, 3f, 0.2f), "6 to 12 tiles from the home, 3 from any town");
        C(!TradingPostMath.PlaceFits(true, 5.9f, 7f, 5, -1f, 1f) && !TradingPostMath.PlaceFits(true, 12.1f, 12.1f, 5, -1f, 1f), "not nearer than 6 nor farther than 12");
        C(!TradingPostMath.PlaceFits(true, 8f, 6.4f, 5, -1f, 1f), "6 tiles or more from every one of the colony's settlements (the game's goodwill reach and a tile and a half)");
        C(!TradingPostMath.PlaceFits(true, 8f, 8f, 5, 2.9f, 1f), "3 tiles or more from any other town");
        C(!TradingPostMath.PlaceFits(true, 8f, 8f, 5, -1f, 0f) && !TradingPostMath.PlaceFits(false, 8f, 8f, 5, -1f, 1f), "never on land that holds no town, nor a tile the game holds invalid");
        C(TradingPostMath.Toward(0f) && TradingPostMath.Toward(60f) && !TradingPostMath.Toward(60.5f) && !TradingPostMath.Toward(-1f), "toward their town: within 60 degrees of its heading");
        C(Near(TradingPostMath.PlaceWeight(true, false, 1f), 3f) && Near(TradingPostMath.PlaceWeight(false, true, 1f), 2f) && Near(TradingPostMath.PlaceWeight(true, true, 0.5f), 1.5f)
          && Near(TradingPostMath.PlaceWeight(false, false, 0.5f), 0.5f) && Near(TradingPostMath.PlaceWeight(true, false, 0f), 0f), "a road three times, a river twice, by the land's weight");
        C(TradingPostMath.PickTile(new[] { false, true, false }, new[] { 5f, 1f, 5f }, 0.01f) == 1 && TradingPostMath.PickTile(new[] { false, true, false }, new[] { 5f, 1f, 5f }, 0.99f) == 1,
            "a tile toward their town wins whenever there is one");
        C(TradingPostMath.PickTile(new[] { false, true }, new[] { 1f, 0f }, 0.5f) == 0, "toward their town but on land that holds nothing: any other tile");
        C(TradingPostMath.PickTile(new[] { false, false }, new[] { 0f, 0f }, 0.5f) == -1 && TradingPostMath.PickTile(null, new float[0], 0.5f) == -1, "none found, no post");
        var counts = new int[3];
        var rng = new Random(9);
        for (int i = 0; i < 6000; i++) counts[TradingPostMath.PickTile(new[] { true, true, false }, new[] { 3f, 1f, 9f }, (float)rng.NextDouble())]++;
        C(counts[2] == 0 && Math.Abs(counts[0] / 6000f - 0.75f) < 0.04f, "among the tiles toward their town, by weight (a road three to one)");
        float s10 = (float)Math.Sin(10 * Math.PI / 180), c10 = (float)Math.Cos(10 * Math.PI / 180);
        C(Near(TradingPostMath.AngleAt(0, 0, 100, s10 * 100, 0, c10 * 100, 0, s10 * 100, c10 * 100), 90f, 0.5f), "the heading on the sphere: north and east are 90 degrees apart");
        C(Near(TradingPostMath.AngleAt(0, 0, 100, s10 * 100, 0, c10 * 100, 2 * s10 * 100, 0, c10 * 100), 0f, 0.5f), "the same way, farther: 0 degrees");
        C(Near(TradingPostMath.AngleAt(0, 0, 100, s10 * 100, 0, c10 * 100, -s10 * 100, 0, c10 * 100), 180f, 0.5f), "the opposite way: 180 degrees");
        float c45 = (float)Math.Cos(Math.PI / 4);
        C(Near(TradingPostMath.AngleAt(0, 0, 100, s10 * 100, 0, c10 * 100, s10 * c45 * 100, s10 * c45 * 100, c10 * 100), 45f, 0.5f), "and 45 between");
        C(TradingPostMath.AngleAt(0, 0, 100, 0, 0, 100, 5, 0, 99) < 0f && TradingPostMath.AngleAt(0, 0, 0, 1, 0, 0, 0, 1, 0) < 0f, "no way at all: no angle");

        // ---------------- Name, keeper, fortune ----------------

        C(Near(TradingPostMath.Start, 40f) && Near(TradingPostMath.Cap, 70f) && Near(RegionMath.PostCap, 70f), "prosperity from 40, at most 70");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradingPosts.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var ages = (Dictionary<string, string>)XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ages.xml")).Root!.Elements()
            .ToDictionary(e => e.Name.LocalName, e => e.Value);
        var all = new[] { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer };
        C(all.Select(a => keyed.TryGetValue(TradingPostMath.NameKey(a), out var v) ? v : "").SequenceEqual(new[] { "{TOWN} Trading Camp", "{TOWN} Market", "{TOWN} Depot", "{TOWN} Ground Station" }),
            "named by their age: a trading camp, a market, a depot, a ground station");
        C(all.Select(a => keyed.TryGetValue(TradingPostMath.KeeperKey(a), out var v) ? v : "").SequenceEqual(new[] { "trader", "factor", "manager", "quartermaster" }),
            "its keeper by their age: a trader, a factor, a manager, a quartermaster");
        C(all.Select(a => ages.TryGetValue(AgesMath.RiteKey(AgeFeature.Post, a), out var v) ? v : "").SequenceEqual(new[] { "trading camp", "market", "depot", "ground station" }),
            "its kind in a sentence is the age form's rite (theirs)");
        C(all.All(a => ages.ContainsKey(AgesMath.WordKey(AgeFeature.Post, a)) && keyed.ContainsKey("CC_Post_Left_" + a)), "the colony's means by its own age, what is left by theirs");

        // ---------------- Souring ----------------

        C(TradingPostMath.SourSince(-1, -1, 5 * D) == 5 * D && TradingPostMath.SourSince(-30, 2 * D, 5 * D) == 2 * D && TradingPostMath.SourSince(0, 2 * D, 5 * D) == -1,
            "sour from the day goodwill falls under 0, until it is 0 or more");
        C(!TradingPostMath.WarnDue(0, -1, 9 * D) && TradingPostMath.WarnDue(0, -1, 10 * D) && !TradingPostMath.WarnDue(0, 10 * D, 15 * D) && !TradingPostMath.WarnDue(-1, -1, 99 * D),
            "the keeper's warning after ten days under 0, once");
        C(!TradingPostMath.CloseDue(0, 10 * D, 19 * D) && TradingPostMath.CloseDue(0, 10 * D, 20 * D) && !TradingPostMath.CloseDue(0, -1, 40 * D) && !TradingPostMath.CloseDue(-1, 10 * D, 40 * D),
            "it closes ten days after the warning while still sour; never unwarned");
        C(TradingPostMath.Mended(-1, 10 * D) && !TradingPostMath.Mended(0, 10 * D) && !TradingPostMath.Mended(-1, -1), "mended: no longer sour after a warning");

        // ---------------- Its home far away ----------------

        C(TradingPostMath.StaysNear(true, 6f, 35f) && TradingPostMath.StaysNear(true, 35f, 35f) && TradingPostMath.StaysNear(true, 12f, 20f),
            "a post 6 to 12 tiles off stays its home's at any reach the setting allows");
        C(!TradingPostMath.StaysNear(true, 48f, 35f) && !TradingPostMath.StaysNear(false, 8f, 35f) && !TradingPostMath.StaysNear(true, -1f, 35f),
            "the home's records carried far away (a gravship, a colony settled anew) or to another layer: no longer its post");
        SimulateSouring(C);

        // ---------------- What it does ----------------

        C(Near(TradingPostMath.Caravans(true), 1.5f) && Near(TradingPostMath.Caravans(false), 1f), "their trader caravans half again as likely while it stands");
        C(Near(TradingPostMath.EventFactor("Council_WS_BanditRoad", true), 2f) && Near(TradingPostMath.EventFactor("Council_WS_BanditRoad", false), 1f)
          && Near(TradingPostMath.EventFactor("Council_WS_Plague", true), 1f), "the bandit road twice as likely with a post near, nothing else changed");
        var roads = Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WorldSituationDef"))
            .Select(e => e.Element("defName")?.Value).ToList();
        C(roads.Contains(TradingPostMath.BanditRoad), "the bandit road is a world event of the defs");

        // ---------------- Sacked ----------------

        C(TradingPostMath.SackRisk(20f) && !TradingPostMath.SackRisk(20.1f) && !TradingPostMath.SackRisk(-1f), "at risk with an enemy's town within 20 tiles");
        C(TradingPostMath.SackEveryDays == 15 && Near(TradingPostMath.SackChance, 0.05f), "5% every 15 days");
        C(Near(TradingPostMath.AfterSack(40f), 10f) && Near(TradingPostMath.AfterSack(12f), 0f), "a sack costs 30, never under 0");
        C(TradingPostMath.SackGivesUp(39f) && !TradingPostMath.SackGivesUp(40f) && !TradingPostMath.SackGivesUp(-1f), "given up when it would fall under 10, else it stands poorer");

        // ---------------- The history ----------------

        C(TradingPostMath.EndKind(PostEnd.Soured) == TradingPostMath.KindClosed && TradingPostMath.EndKind(PostEnd.War) == TradingPostMath.KindClosed
          && TradingPostMath.EndKind(PostEnd.Sacked) == TradingPostMath.KindClosed && TradingPostMath.EndKind(PostEnd.Razed) == TradingPostMath.KindRazed
          && TradingPostMath.EndKind(PostEnd.Gone) == null, "closed for souring, war and a sack; razed; nothing for a post simply gone");
        C(TradingPostMath.EndKind(PostEnd.HardTimes) == TradingPostMath.KindClosed, "a post given up for hard times closes in its history too");
        foreach (var k in TradingPostMath.Kinds)
            C(HistoryKinds.Matches(k, TradingPostMath.Family) && HistoryKinds.Matches(k, "world"), $"{k} is of the families world:post and world");
        C(TradingPostMath.Milestone(TradingPostMath.KindOpened) && TradingPostMath.Milestone(TradingPostMath.KindClosed) && TradingPostMath.Milestone(TradingPostMath.KindRazed)
          && !TradingPostMath.Milestone(TradingPostMath.KindWarned) && !TradingPostMath.Milestone(TradingPostMath.KindMended), "its opening and its end are milestones");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string code = string.Concat(Directory.GetFiles(Path.Combine(src, "World", "Region"), "TradingPost*.cs").Select(File.ReadAllText));
        string run = Code("World/Region/TradingPost.cs");
        string book = Code("World/Region/RegionBook.cs");
        C(book.Contains("public TradingPostHome posts;") && book.Contains("Scribe_Deep.Look(ref posts, \"posts\")"), "each home's posts are saved with its part of the region");
        C(book.Contains("TradingPosts.Daily(this, now)"), "the region's day runs them (no pass of their own)");
        var tick = Regex.Match(book, @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(tick.Length > 0 && !tick.Contains("TradingPost") && !code.Contains("GameComponentTick"), "nothing per tick");
        string words = Code("World/Region/TradingPostWords.cs") + run + Code("World/Region/TradingPostPage.cs") + Code("World/Region/TradingPostFacts.cs");
        C(!Regex.IsMatch(words, @"Messages\.Message|LetterStack|News\.Post|Notify\."), "tells the player through Tell only (the dev actions answer directly)");
        C(!Regex.IsMatch(code, @"TraversalDistanceBetween|WorldPath|CaravanArrivalTimeEstimator"), "straight-line distances only, inside the tile search too");
        C(run.Contains("TileFinder.IsValidTileForNewSettlement(t)") && run.Contains("Find.WorldObjects.AnyWorldObjectAt(t)") && run.Contains("RegionFortunes.BiomeWeight(t)"),
            "a post where the game would put a town, on an empty tile, by the land's weight");
        C(run.Contains("RiseFall.GoodwillTiles") && run.Contains("WorldMechanics.TerritoryOwner(t)") && run.Contains("WorldMechanics.SettlementsBeingBuilt()"),
            "clear of the colony's settlements by the game's own reach, out of other peoples' territory and building sites");
        C(run.Contains("WorldObjectDefOf.Settlement") && run.Contains("SettlementNameGenerator.GenerateSettlementName"), "the game's own town, named by their naming rule and their age");
        C(run.Contains("SituationWorker_Abandoned.Safe(t)") && run.Contains("RiseFall.MakeRuin("), "it closes through the region's giving up, when safe (a ruin left)");
        C(run.Contains("rec.kind = TownKind.Post") && run.Contains("TradingPostMath.Start"), "on the ledger as a post, its fortune from 40");
        C(run.Contains("TradingPostMath.StaysNear(sameLayer,") && run.Contains("RegionTowns.ReachTiles))"), "a post its home has moved away from ends as a post, the region's day sees to it");
        C(run.Contains("NotableBook.StaffPost(") && Code("World/Notables.cs").Contains("public partial class NotableBook")
          && Code("World/Notables.cs").Contains("TradingPosts.KeeperTitle(n)"), "its keeper and trader are notables at once, the keeper titled by their age");
        C(run.Contains("HistoryMath.Milestone") && run.Contains("TradingPostMath.KindOpened"), "its opening is a milestone in the history");
        C(Code("World/Region/RiseFallPatches.cs").Contains("TradingPosts.CaravanFactor(f, map)"), "their caravans' odds through the region's caravan hook");
        C(Code("World/WorldSituations.cs").Contains("TradingPosts.EventFactor(d, map)"), "the bandit road's odds where the world events are weighed");
        C(run.Contains("LinksOn => On && Interplay.On"), "both behind \"Systems talk to each other\"");
        C(Code("UI/WeekModel.cs").Contains("TradingPostPage.WeekRows(c)"), "This week shows a post that may close");
        C(!code.Contains("MainTabWindow_World.Register(") && Code("UI/Region/LensModel.cs").Contains("TradingPosts.All()") && Code("UI/Region/LensModel.cs").Contains("TradingPostWords.Lines(postHome, post, true)"),
            "the posts are the region map's Trade lens, with their list (their old page's name forwards there)");
        C(code.Contains("nameof(SettlementDefeatUtility.CheckDefeated)") && !Regex.IsMatch(Code("World/Region/TradingPostPatches.cs"), @"return false;"),
            "a post razed by the colony is only noted (the game's own defeat runs as before)");
        string memory = Code("World/FactionMemory.cs");
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        C(memory.Contains("[\"" + TradingPostMath.DeedRazed + "\"] = K(DeedSide.Wrong, " + TradingPostMath.RazedWeight + ")")
          && keyedAll.Contains("<CC_Memory_Deed_" + TradingPostMath.DeedRazed + ">"), "a post razed is a heavy wrong their people remember, with its words");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/Region/TradingPost.cs:posts\"") && est.Contains("\"MaxPerHome\""), "the save model has the posts, by the rule's own bound");
        // Every key the code names exists; the words have no em dashes and no exclamation marks.
        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 30, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyedAll.Contains("<" + k + ">"), $"{k} exists");
        foreach (var e in new[] { PostEnd.Soured, PostEnd.War, PostEnd.Sacked })
            C(keyed.ContainsKey("CC_Post_Closed_" + e) && keyed.ContainsKey("CC_Post_H_" + e), $"the words of a closing: {e}");
        C(keyed.ContainsKey("CC_Post_H_Razed") && keyed.ContainsKey("CC_Post_Razed"), "the words of a post razed");
        C(keyed.ContainsKey("CC_Post_H_HardTimes") && run.Contains("ruin.why == LedgerRuin.GivenUp ? PostEnd.HardTimes : PostEnd.Gone")
          && !Regex.IsMatch(Code("World/Region/TradingPostWords.cs"), @"case PostEnd\.HardTimes"),
            "a post given up for hard times: one line in its history, no message of its own (the giving up told it)");
        string mine = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradingPosts.xml"));
        C(!mine.Contains("—") && !Regex.IsMatch(mine, ">[^<]*![^<]*<"), "no em dashes and no exclamation marks in its words");
        var told = keyed.Where(kv => kv.Key.StartsWith("CC_Post_")).Select(kv => kv.Value).ToList();
        C(told.All(t => !Regex.IsMatch(t, @"\{DAYS\}|\bin \d+ days|\bwithin \d+ days|\bdays? (left|more)\b")), "no countdown in its letters, news or panel");
        // The setting.
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool tradingPosts = true;") && settings.Contains("Scribe_Values.Look(ref tradingPosts, \"tradingPosts\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"tradingPosts\"") && keyed.ContainsKey("CC_Settings_TradingPosts_Tip"), "one switch, on by default, in the region's settings");
        C(run.Contains("ColonyCouncilMod.Settings?.tradingPosts"), "off, none opens");
        // The facts and the talk.
        C(new[] { "town:post", "town:post:warned", "their:post", "region:post" }.All(FactsMath.RegionTags.Contains)
          && new[] { "POST", "POSTKIND", "POSTPEOPLE", "KEEPER" }.All(FactsMath.RegionValues.Contains), "the facts' tags and placeholders for posts");
        C(Code("Text/Facts/FactsRegion.cs").Contains("Posts(s, f);"), "the region's provider reads the posts");
        var talk = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_TradingPosts.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talk.Count >= 12, $"talk pieces about the posts ({talk.Count})");
        C(talk.All(t => Regex.IsMatch(t.Element("when")?.Value ?? "", @"\b(town:post|their:post|region:post)")), "each speaks only when a post is there to speak of");
        foreach (var tag in new[] { "town:post", "town:post:warned", "their:post", "region:post" })
            C(talk.Any(t => (t.Element("when")?.Value ?? "").Split(',').Contains(tag)), "a piece for " + tag);
        return n;
    }

    /// <summary>
    /// Five years of one home's looks with every roll coming up and a people always qualifying: posts open on the look
    /// days only, two at most stand, and no two changes (an opening or a closing) come within 60 days.
    /// </summary>
    private static void SimulateLooks(Action<bool, string> C)
    {
        var rng = new Random(3);
        int lookedAt = -1, changedAt = -1, most = 0;
        var posts = new List<int>();
        var changes = new List<int>();
        for (int day = 0; day < 300; day++)
        {
            int now = day * D;
            // Now and then a post closes (its people soured, a sack).
            if (posts.Count > 0 && rng.NextDouble() < 0.01 && TradingPostMath.HomeAllows(0, changedAt, now))
            {
                posts.RemoveAt(0);
                changedAt = now;
                changes.Add(day);
            }
            if (!TradingPostMath.LookDue(lookedAt, now)) continue;
            lookedAt = now;
            if (!TradingPostMath.HomeAllows(posts.Count, changedAt, now)) continue;
            posts.Add(day);
            changedAt = now;
            changes.Add(day);
            most = Math.Max(most, posts.Count);
        }
        bool spaced = changes.Zip(changes.Skip(1), (a, b) => b - a).All(g => g >= TradingPostMath.GapDays);
        C(most <= TradingPostMath.MaxPerHome && most == 2, $"five years of looks: two posts at most ({most})");
        C(spaced && changes.Count >= 3, $"five years of looks: never two openings or closings within 60 days ({changes.Count} changes)");
    }

    /// <summary>The souring clock day by day: warned after ten days under 0, mended when it comes back, warned again, closed ten days after.</summary>
    private static void SimulateSouring(Action<bool, string> C)
    {
        // Days 0 to 14 under 0, day 15 back at 0 for two days, then under 0 again from day 17.
        int sour = -1, warned = -1, warnedDay = -1, mendedDay = -1, secondWarn = -1, closedDay = -1;
        for (int day = 0; day < 60 && closedDay < 0; day++)
        {
            int now = day * D;
            int goodwill = day >= 15 && day < 17 ? 0 : -20;
            sour = TradingPostMath.SourSince(goodwill, sour, now);
            if (TradingPostMath.Mended(sour, warned))
            {
                warned = -1;
                mendedDay = day;
            }
            else if (TradingPostMath.WarnDue(sour, warned, now))
            {
                warned = now;
                if (warnedDay < 0) warnedDay = day;
                else secondWarn = day;
            }
            else if (TradingPostMath.CloseDue(sour, warned, now)) closedDay = day;
        }
        C(warnedDay == 10 && mendedDay == 15, $"warned on the tenth day under 0, mended when goodwill came back ({warnedDay}, {mendedDay})");
        C(secondWarn == 27 && closedDay == 37, $"sour again: a new warning ten days on, closed ten days after it ({secondWarn}, {closedDay})");
    }
}
