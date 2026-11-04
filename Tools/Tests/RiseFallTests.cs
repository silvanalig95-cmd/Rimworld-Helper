using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Towns that rise and fall (World/Region/RiseFallMath.cs; docs/design/stage1-region.md, item 2; decisions 2, 5 and
/// 14): the pace of change in a home's reach, which town may be given up (struggling long enough, not its people's
/// seat, not the Empire's, not one of their last towns, safe to take off the map), ruins and their lifetime, which
/// people settles a ruin again, when a flourishing town sends settlers out, where a town Rimfolk places may stand (clear
/// of every home, 6 to 15 tiles from its mother town, land that holds a town), what a fortune does to caravans, and the
/// caps over five simulated years. Also the mod's side: the passes, the saves and their model, Tell only, straight-line
/// tile searches, the world event and its words, the setting, the hooks.
/// </summary>
public static class RiseFallTests
{
    private const int Day = RiseFallMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "rise and fall: " + what);
        }

        // ---------------- The pace (decision 14) ----------------

        C(RiseFallMath.GiveUpGapDays == 60 && RiseFallMath.ResettleGapDays == 120 && RiseFallMath.FoundGapDays == 90 && RiseFallMath.RuinDays == 300,
            "one given up per 60 days, one resettled per 120, one founded per 90; ruins go after 300 days");
        C(RiseFallMath.GapPassed(-1, 5 * Day, 60), "a home that never saw one may see one");
        C(!RiseFallMath.GapPassed(10 * Day, 69 * Day, 60) && RiseFallMath.GapPassed(10 * Day, 70 * Day, 60), "the gap ends after its days, not a tick before");
        C(RiseFallMath.PaceAllows(new List<int> { -1, 0 }, 61 * Day, 60), "every home in reach past its gap: it may come");
        C(!RiseFallMath.PaceAllows(new List<int> { -1, 30 * Day }, 61 * Day, 60), "a town in reach of two homes waits for both");
        C(!RiseFallMath.PaceAllows(new List<int>(), 61 * Day, 60) && !RiseFallMath.PaceAllows(null, 61 * Day, 60), "beyond every home's reach nothing changes");
        bool once = true;
        foreach (int id in new[] { 1, 7, 55, 1234, int.MaxValue })
            foreach (int every in new[] { 5, 15 })
            {
                int hits = Enumerable.Range(300, every).Count(d => RiseFallMath.RollDay(d, id, every));
                if (hits != 1) once = false;
            }
        C(once, "each town's roll comes once in its days, on a day of its own");
        C(Enumerable.Range(0, 40).Select(id => Enumerable.Range(0, 5).First(d => RiseFallMath.RollDay(d, id, 5))).Distinct().Count() == 5, "the towns' rolls spread over the days");

        // ---------------- A town given up ----------------

        C(RiseFallMath.Low == RegionMath.Low, "low is the region's own low (15), whose since-when the fortunes keep");
        C(RiseFallMath.Struggling(12f, 0, 0, -1, 30 * Day), "under 15 for 30 days: struggling long enough");
        C(!RiseFallMath.Struggling(12f, 0, 0, -1, 29 * Day), "under 15 for 29 days: not yet");
        C(!RiseFallMath.Struggling(16f, 0, 0, -1, 90 * Day) && !RiseFallMath.Struggling(12f, -1, 0, -1, 90 * Day), "not low, or not low since a known day: no");
        C(RiseFallMath.Struggling(22f, -1, 2, -1, 10 * Day), "under 25 and struck twice in a season: given up sooner");
        C(!RiseFallMath.Struggling(22f, -1, 1, -1, 10 * Day) && !RiseFallMath.Struggling(26f, -1, 3, -1, 10 * Day), "struck once, or not under 25: no");
        C(RiseFallMath.Struggling(8f, -1, 0, 100 * Day, 110 * Day), "a new town under 10 in its first season fails");
        C(!RiseFallMath.Struggling(8f, -1, 0, 100 * Day, 116 * Day) && !RiseFallMath.Struggling(11f, -1, 0, 100 * Day, 101 * Day), "not after its first season, nor at 10 or more");
        C(!RiseFallMath.Struggling(-1f, 0, 5, 0, 90 * Day), "a town with no fortune yet is never given up");
        C(RiseFallMath.MayGiveUp(true, 3, false, false, true), "their people keep two other towns: it may go");
        C(!RiseFallMath.MayGiveUp(true, 2, false, false, true) && !RiseFallMath.MayGiveUp(true, 1, false, false, true), "never one of a people's last two");
        C(!RiseFallMath.MayGiveUp(true, 9, true, false, true), "never their seat");
        C(!RiseFallMath.MayGiveUp(true, 9, false, true, true), "never the Empire's");
        C(!RiseFallMath.MayGiveUp(true, 9, false, false, false), "never a town not safe to take off the map (a quest's, a map open, a caravan on the way)");
        C(!RiseFallMath.MayGiveUp(false, 9, false, false, true), "never a town that isn't struggling");
        C(Math.Abs(RiseFallMath.GiveUpChance - 0.07f) < 1e-6 && RiseFallMath.GiveUpEveryDays == 5 && RiseFallMath.QuietDays == 5, "7% every 5 days; quietly, the ruin after five days");

        // ---------------- Ruins ----------------

        C(!RiseFallMath.RuinFaded(0, 299 * Day) && RiseFallMath.RuinFaded(0, 300 * Day) && !RiseFallMath.RuinFaded(-1, 900 * Day), "a ruin Rimfolk made goes after 300 days");
        C(!RiseFallMath.WaitOver(0, 29 * Day) && RiseFallMath.WaitOver(0, 30 * Day), "a fight's ruin waits 30 days at most for the destroyed settlement to go");
        var since = new List<int> { 50, 10, 30, 10, 70 };
        C(RiseFallMath.OverCap(since, 3).SequenceEqual(new[] { 1, 3 }), "over the cap, the oldest go first, equals in the order they came");
        C(RiseFallMath.OverCap(since, 5).Count == 0 && RiseFallMath.OverCap(null).Count == 0, "at or under the cap nothing goes");
        C(RiseFallMath.MaxRuinsPerHome == 12 && RiseFallMath.MaxWaiting == 6, "twelve ruins a home's reach; six fights' ruins waiting");

        // ---------------- A ruin settled again ----------------

        C(!RiseFallMath.RuinReady(0, 119 * Day) && RiseFallMath.RuinReady(0, 120 * Day), "a ruin 120 days old may be settled again");
        C(RiseFallMath.MayResettle(true, false, 60f) && !RiseFallMath.MayResettle(true, false, 59f), "a neighbour whose town nearby stands at 60 or more");
        C(RiseFallMath.MayResettle(true, true, 50f) && !RiseFallMath.MayResettle(true, true, 49f), "its own people at 50 or more");
        C(!RiseFallMath.MayResettle(false, true, 99f), "never a people at war with the colony");
        C(RiseFallMath.Settlers(new[] { true, true, true }, new[] { false, true, false }, new[] { 90f, 55f, 70f }) == 1, "its own people first");
        C(RiseFallMath.Settlers(new[] { true, false, true }, new[] { false, true, false }, new[] { 65f, 55f, 80f }) == 2, "else the one doing best nearby");
        C(RiseFallMath.Settlers(new[] { false, false }, new[] { false, true }, new[] { 90f, 90f }) == -1 && RiseFallMath.Settlers(null, null, null) == -1, "nobody fit: nobody comes");
        C(Math.Abs(RiseFallMath.ResettleChance - 0.10f) < 1e-6 && RiseFallMath.ResettleEveryDays == 15 && RiseFallMath.ResettleTiles == 25f, "10% every 15 days, from a town within 25 tiles");

        // ---------------- Rimfolk's own new towns (decision 2) ----------------

        C(RiseFallMath.HighSince(85f, -1, 7 * Day) == 7 * Day && RiseFallMath.HighSince(85f, 3 * Day, 7 * Day) == 3 * Day && RiseFallMath.HighSince(79f, 3 * Day, 7 * Day) == -1,
            "flourishing since: kept while at 80 or more, lost below");
        C(!RiseFallMath.MaySendSettlers(85f, 0, 59 * Day) && RiseFallMath.MaySendSettlers(85f, 0, 60 * Day) && !RiseFallMath.MaySendSettlers(79f, 0, 90 * Day),
            "flourishing for 60 days, it may send settlers out");
        C(RiseFallMath.RoomInReach(9) && !RiseFallMath.RoomInReach(10), "only while fewer than 10 towns stand in reach");
        C(Math.Abs(RiseFallMath.FoundChance - 0.15f) < 1e-6 && RiseFallMath.FoundEveryDays == 15, "15% every 15 days");

        // ---------------- Where a town Rimfolk places may stand ----------------

        C(!RiseFallMath.ClearOfHomes(5f, 5) && !RiseFallMath.ClearOfHomes(6.4f, 5) && RiseFallMath.ClearOfHomes(6.6f, 5) && RiseFallMath.ClearOfHomes(-1f, 5),
            "6 tiles or more from every home (the game's goodwill reach, 5, and a tile and a half for the straight line)");
        C(RiseFallMath.FoundingTile(true, 6f, 10f, 35f, 5, 1f) && RiseFallMath.FoundingTile(true, 15f, 10f, 35f, 5, 0.2f), "6 to 15 tiles from its mother town, within reach, clear of homes");
        C(!RiseFallMath.FoundingTile(true, 5.9f, 10f, 35f, 5, 1f) && !RiseFallMath.FoundingTile(true, 15.1f, 10f, 35f, 5, 1f), "not nearer than 6 nor farther than 15 from its mother town");
        C(!RiseFallMath.FoundingTile(true, 8f, 36f, 35f, 5, 1f) && !RiseFallMath.FoundingTile(true, 8f, -1f, 35f, 5, 1f), "never beyond every home's reach");
        C(!RiseFallMath.FoundingTile(true, 8f, 6f, 35f, 5, 1f), "never within 6 tiles of a home");
        C(!RiseFallMath.FoundingTile(true, 8f, 10f, 35f, 5, 0f) && !RiseFallMath.FoundingTile(false, 8f, 10f, 35f, 5, 1f), "never on land that holds no town (an ice sheet), nor a tile the game holds invalid");
        C(RiseFallMath.ResettleTile(true, 10f, 35f, 5) && !RiseFallMath.ResettleTile(true, 6f, 35f, 5) && !RiseFallMath.ResettleTile(false, 10f, 35f, 5)
          && !RiseFallMath.ResettleTile(true, 40f, 35f, 5), "a ruin is settled again only where a new town could stand: clear of homes, within reach, still valid");
        C(RiseFallMath.PickTile(new[] { 0f, 0f }, 0.5f) == -1 && RiseFallMath.PickTile(new float[0], 0.5f) == -1, "a harsh, empty land stays empty");
        C(RiseFallMath.PickTile(new[] { 0f, 1f, 0f }, 0.99f) == 1 && RiseFallMath.PickTile(new[] { 0f, 1f, 0f }, 0f) == 1, "only land with weight is picked");
        var counts = new int[3];
        var rng = new Random(4);
        for (int i = 0; i < 6000; i++) counts[RiseFallMath.PickTile(new[] { 1f, 0.25f, 0.75f }, (float)rng.NextDouble())]++;
        C(counts[0] > counts[2] && counts[2] > counts[1] && Math.Abs(counts[0] / 6000f - 0.5f) < 0.04f, "weighed by the biome's settlement weight: a mild land fills first");

        // ---------------- What a fortune does ----------------

        C(RiseFallMath.CaravanFactor(Fortune.Struggling) == 0.6f && RiseFallMath.CaravanFactor(Fortune.Thriving) == 1.25f && RiseFallMath.CaravanFactor(Fortune.Flourishing) == 1.25f,
            "caravans 0.6 from a struggling town, 1.25 from a thriving one");
        C(RiseFallMath.CaravanFactor(Fortune.Poor) == 1f && RiseFallMath.CaravanFactor(Fortune.Steady) == 1f && RiseFallMath.CaravanFactor(null) == 1f, "else as the game has it");
        C(RiseFallMath.HardTimes == 2f, "asks and the hungry at the gate twice as likely from a struggling town");

        // ---------------- Five years of a reach ----------------

        Simulate(C, false);
        Simulate(C, true);

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string book = Code("World/Region/RegionBook.cs");
        C(book.Contains("RiseFall.Daily(this, now)") && book.Contains("RiseFall.Hourly(this, now)") && book.Contains("if (RiseFall.On) Safe("),
            "rises and falls on the region's two passes, behind its setting, each in its own guard");
        var tick = Regex.Match(book, @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(tick.Length > 0 && !tick.Contains("RiseFall"), "nothing per tick: no new pass and no new slot");
        C(book.Contains("Scribe_Values.Look(ref givenUpAt, \"givenUpAt\", -1)") && book.Contains("Scribe_Values.Look(ref resettledAt, \"resettledAt\", -1)")
          && book.Contains("Scribe_Values.Look(ref foundedAt, \"foundedAt\", -1)"), "each home keeps its three clocks");
        string persons = Code("World/Ledger/LedgerPersons.cs");
        C(persons.Contains("Scribe_Values.Look(ref highSince, \"highSince\", -1)") && persons.Contains("Scribe_Values.Look(ref closing, \"closing\", -1)"),
            "a town's record keeps since when it flourishes and since when it is being given up");
        string ledger = Code("World/Ledger/LedgerRegion.cs");
        string rl = Code("World/Region/RiseFallLedger.cs");
        C(ledger.Contains("ExposeRiseFall();") && rl.Contains("Scribe_Collections.Look(ref ruins, \"ruins\", LookMode.Deep)"), "the ledger saves its ruins");
        C(rl.Contains("waiting.Count - RiseFallMath.MaxWaiting"), "fights' ruins waiting are bounded");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/Region/RiseFallLedger.cs:ruins\"") && est.Contains("\"MaxRuinsPerHome\""), "the save model has the ruins, by the rule's own bound");
        string all = Code("World/Region/RiseFall.cs") + Code("World/Region/NewTowns.cs") + Code("World/Region/RiseFallWords.cs") + Code("World/Region/RiseFallEffects.cs")
                     + Code("World/Region/RiseFallSituation.cs") + Code("World/Region/RiseFallPatches.cs");
        C(!Regex.IsMatch(all, @"Messages\.Message|LetterStack|News\.Post|Notify\."), "tells the player through Tell only");
        C(!Regex.IsMatch(all, @"TraversalDistanceBetween|WorldPath|CaravanArrivalTimeEstimator"), "straight-line distances only, inside the tile search too");
        string fall = Code("World/Region/RiseFall.cs");
        string towns = Code("World/Region/NewTowns.cs");
        C(fall.Contains("SettlementProximityGoodwillUtility.MaxDist") && towns.Contains("RiseFallMath.ClearOfHomes(nearestOurs, goodwill)")
          && towns.Contains("RiseFallMath.ClearOfHomes(ours, RiseFall.GoodwillTiles)"), "nothing placed within the game's goodwill reach of any of the colony's settlements");
        C(towns.Contains("TileFinder.IsValidTileForNewSettlement(t)") && towns.Contains("RegionFortunes.BiomeWeight(t)") && towns.Contains("Find.WorldObjects.AnyWorldObjectAt(t)"),
            "a new town where the game would put one, by its biome's weight, on an empty tile");
        C(towns.Contains("WorldMechanics.SettlementsBeingBuilt()") && towns.Contains("WorldMechanics.TerritoryOwner(t)"),
            "another mod's building sites and other peoples' territories are left alone");
        C(fall.Contains("o.def == WorldObjectDefOf.AbandonedSettlement") && fall.Contains("GivenUpByEvent(id)"),
            "only Rimfolk's own ruins are kept: another mod's sites (Real Ruins') are left alone");
        C(fall.Contains("SituationWorker_Abandoned.Safe(t)") && towns.Contains("RiseFall.RuinSafe(o)"), "a town or a ruin comes off the map only when safe (no map, quest, caravan or pods)");
        C(fall.Contains("WorldObjectDefOf.AbandonedSettlement") && towns.Contains("WorldObjectDefOf.Settlement") && towns.Contains("SettlementNameGenerator.GenerateSettlementName"),
            "the game's own objects: a town of the vanilla def named by its people's rule, a ruin the game's abandoned settlement");
        C(towns.Contains("RegionFaces.Founded(town, null, why)") && towns.Contains("f.motherName"), "Rimfolk's new towns get the region's faces, with the town their settlers came from");
        string sit = Code("World/Workers_Situations.cs");
        C(sit.Contains("RiseFall.NoteFounded(Town(s));") && sit.Contains("RiseFall.EventMayGiveUp(t)") && sit.Contains("RiseFallMath.KeepsEnough("),
            "Rimfolk's world events count on the same clocks: a town they found or give up is not counted twice");
        string patches = Code("World/Region/RiseFallPatches.cs");
        C(patches.Contains("nameof(SettlementDefeatUtility.CheckDefeated)") && patches.Contains("!factionBase.Destroyed") && !patches.Contains("return false;"),
            "a town defeated is only noted (the game's own defeat runs as before)");
        // The game asks every town whether it is defeated every few ticks (every tick with the world open): both hooks stand
        // down at once for a town with no map, as the game's own first check does.
        string postPatches = Code("World/Region/TradingPostPatches.cs");
        C(Regex.Matches(patches + postPatches, @"!factionBase\.HasMap\) return;").Count == 2, "the defeat hooks look only at a town with a map (a hot method)");
        C(patches.Contains("parms.faction != null") && patches.Contains("if (!any || can.Count == 0) return;"), "a caravan whose people is chosen is left alone; with no fortune to weigh, the game picks");
        C(Regex.Matches(patches, @"RimfolkPatches\.On").Count >= 3, "every hook stands down with the deeper game hooks off");

        // The hooks of prosperity's effects.
        C(Code("Missions/Workers_MissionDelivery.cs").Contains("RiseFallEffects.AskTown(def, map)"), "a struggling town's asks come in its own name");
        C(Code("Happenings/IncidentWorker_Happening.cs").Contains("RiseFallEffects.EventFactor(def, target as Map)") && Code("Happenings/Happenings.cs").Contains("RiseFallEffects.EventFactor(d, map)"),
            "the asks' odds, drawn by the storyteller and on Rimfolk's own timer");
        string camps = Code("Camps/CampTracker.cs");
        C(camps.Contains("RiseFallEffects.CampFactor(k, map)") && camps.Contains("RiseFallEffects.CampPeople(kind, map, x)"), "a refugee family's odds and people");
        C(Code("World/Region/RiseFallEffects.cs").Contains("RegionBook.FortunesOn && Interplay.On"), "the effects behind the fortunes and \"Systems talk to each other\"");

        // The setting.
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool regionChange = true;") && settings.Contains("Scribe_Values.Look(ref regionChange, \"regionChange\", true)")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"regionChange\""), "one switch for the pace of change, on by default");
        C(fall.Contains("ColonyCouncilMod.Settings?.regionChange"), "off, nothing rises or falls");

        // The world event and the words.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_RiseFall.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
            foreach (var k in new[] { "CC_RiseFall_Ruin_", "CC_RiseFall_Back_", "CC_RiseFall_Newcomers_", "CC_RiseFall_Renamed_" })
                C(keyed.Contains(k + age), "words by age: " + k + age);
        var used = Regex.Matches(all, "\"(CC_RiseFall_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        foreach (var k in used) C(keyed.Contains(k), "the words exist: " + k);
        var def = Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WorldSituationDef"))
            .FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_WS_GivenUp");
        C(def != null && def.Element("workerClass")?.Value == "ColonyCouncil.SituationWorker_GivenUp", "the world event of a town given up");
        var texts = def?.Element("texts")?.Elements("li").Select(e => e.Value).ToList() ?? new List<string>();
        foreach (var cause in Regex.Matches(Code("World/Region/RiseFallSituation.cs"), "return \"([a-z]+)\";").Select(m => m.Groups[1].Value).Distinct())
            C(texts.Any(t => t.StartsWith("[" + cause + "]")), "its letter for what struck the town: " + cause);
        var beats = def?.Element("beats")?.Elements("li").Select(b => b.Element("key")?.Value).ToList() ?? new List<string>();
        C(beats.SequenceEqual(new[] { "refugees", "salvage" }), "its beats: a family of theirs at the gate, what the town leaves behind");
        var endings = def?.Element("endings")?.Elements("li").Select(b => b.Element("key")?.Value).ToList() ?? new List<string>();
        C(endings.SequenceEqual(new[] { "empty", "holdouts" }) && def!.Element("endings")!.Elements("li").All(e => e.Element("chronicle") == null),
            "it ends a ruin or held on; the history has it from the region, once");
        C((def?.Element("earliestDay")?.Value ?? "") == "0" && (def?.Element("cooldownDays")?.Value ?? "") == "0" && Code("World/Region/RiseFallSituation.cs").Contains("pending != null && base.CanStart(map)"),
            "it never starts by itself: the region's pace starts it");
        return n;
    }

    /// <summary>
    /// Five years of one home's reach: nine towns, some stuck in hard times and some flourishing, given up by the rule on
    /// their rolls, ruins settled again or fading, flourishing towns sending settlers out, every change held to the home's
    /// clocks. <paramref name="worst"/>: every roll comes up, so only the pace holds the changes back.
    /// </summary>
    private static void Simulate(Action<bool, string> C, bool worst)
    {
        var rng = new Random(worst ? 5 : 11);
        var aim = new Dictionary<int, float>();
        var p = new Dictionary<int, float>();
        var lowSince = new Dictionary<int, int>();
        var highSince = new Dictionary<int, int>();
        var ruins = new List<(int town, int since)>();
        int next = 1;
        float Aim() => rng.Next(2) == 0 ? 8f : 90f;
        void Add(float start)
        {
            aim[next] = Aim();
            p[next++] = start;
        }
        for (int i = 0; i < 9; i++) Add((float)rng.Next(5, 95));
        bool Roll(float chance) => worst || rng.NextDouble() < chance;
        int givenUpAt = -1, resettledAt = -1, foundedAt = -1;
        var givenUp = new List<int>();
        var resettled = new List<int>();
        var founded = new List<int>();
        int mostRuins = 0, mostTowns = 0;
        for (int day = 0; day < 300; day++)
        {
            int now = day * Day;
            foreach (int t in p.Keys.ToList())
            {
                p[t] = Math.Max(0f, Math.Min(100f, p[t] + (aim[t] - p[t]) * 0.1f + (float)(rng.NextDouble() * 6 - 3)));
                lowSince[t] = RegionMath.LowSince(p[t], lowSince.TryGetValue(t, out int ls) ? ls : -1, now);
                highSince[t] = RiseFallMath.HighSince(p[t], highSince.TryGetValue(t, out int hs) ? hs : -1, now);
            }
            foreach (int t in p.Keys.ToList())
            {
                if (!RiseFallMath.RollDay(day, t, RiseFallMath.GiveUpEveryDays)) continue;
                if (!RiseFallMath.MayGiveUp(RiseFallMath.Struggling(p[t], lowSince[t], 0, -1, now), p.Count, false, false, true)) continue;
                if (!RiseFallMath.PaceAllows(new[] { givenUpAt }, now, RiseFallMath.GiveUpGapDays) || !Roll(RiseFallMath.GiveUpChance)) continue;
                givenUpAt = now;
                givenUp.Add(day);
                p.Remove(t);
                ruins.Add((t, now));
                break;
            }
            foreach (var r in ruins.ToList())
            {
                if (!RiseFallMath.RollDay(day, r.town, RiseFallMath.ResettleEveryDays) || !RiseFallMath.RuinReady(r.since, now)) continue;
                if (!RiseFallMath.PaceAllows(new[] { resettledAt }, now, RiseFallMath.ResettleGapDays) || !Roll(RiseFallMath.ResettleChance)) continue;
                resettledAt = now;
                resettled.Add(day);
                ruins.Remove(r);
                Add(RegionMath.NewTownStart);
                break;
            }
            ruins.RemoveAll(r => RiseFallMath.RuinFaded(r.since, now));
            foreach (int i in RiseFallMath.OverCap(ruins.Select(r => r.since).ToList()).OrderByDescending(i => i)) ruins.RemoveAt(i);
            foreach (int t in p.Keys.ToList())
            {
                if (!RiseFallMath.RollDay(day, t, RiseFallMath.FoundEveryDays) || !RiseFallMath.MaySendSettlers(p[t], highSince[t], now)) continue;
                if (!RiseFallMath.RoomInReach(p.Count) || !RiseFallMath.PaceAllows(new[] { foundedAt }, now, RiseFallMath.FoundGapDays) || !Roll(RiseFallMath.FoundChance)) continue;
                foundedAt = now;
                founded.Add(day);
                highSince[t] = now;
                Add(RegionMath.NewTownStart);
                break;
            }
            mostRuins = Math.Max(mostRuins, ruins.Count);
            mostTowns = Math.Max(mostTowns, p.Count);
        }
        string how = worst ? "every roll coming up" : "at the rules' own odds";
        bool Spaced(List<int> days, int gap) => days.Zip(days.Skip(1), (a, b) => b - a).All(d => d >= gap);
        C(Spaced(givenUp, RiseFallMath.GiveUpGapDays), $"five years {how}: never two towns given up within 60 days ({givenUp.Count} given up)");
        C(Spaced(resettled, RiseFallMath.ResettleGapDays), $"five years {how}: never two ruins settled within 120 days ({resettled.Count} settled)");
        C(Spaced(founded, RiseFallMath.FoundGapDays), $"five years {how}: never two towns founded within 90 days ({founded.Count} founded)");
        C(mostRuins <= RiseFallMath.MaxRuinsPerHome && mostTowns <= 9 + founded.Count + resettled.Count, $"five years {how}: the ruins stay within their bound ({mostRuins} at most)");
        if (worst)
            C(givenUp.Count >= 3 && resettled.Count >= 1 && founded.Count >= 2, $"the worst case is the pace's own: {givenUp.Count} given up, {resettled.Count} settled, {founded.Count} founded");
    }
}
