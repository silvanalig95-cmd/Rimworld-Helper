using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Fewer quests out on the world map, their sites nearer home, Rimfolk's own timer besides the storyteller's, and the events
/// record (the player's wish of 20 November 2026, their go of 22 November 2026): the rule that tells a quest out on the world
/// map from one at home (Story/QuestPlaceMath.cs) on a sample of the player's own active quests, each modelled from its own
/// files (the mods' quest scripts and root nodes as their repositories have them, the game's from its quest scripts' shape);
/// the share and what it does to the mix; the sites' range; the record's lines, window, cap, counts and held reasons; the
/// middle way's pace; the settings, the words and the seams.
/// </summary>
public static class QuestPlaceTests
{
    /// <summary>One quest of the player's: what its tree holds (node types, the types they are built on) and what its nodes' code calls.</summary>
    private sealed class Sample
    {
        public string name, from;
        public bool away;
        public string[] nodes = Array.Empty<string>(), calls = Array.Empty<string>();
    }

    /// <summary>
    /// The sample, with what each was read from. "xml": the quest script's own file (node classes, and the scripts it runs as
    /// a part of itself, as SubScript); "cs": the root node's own source (its base type and calls). The game's own are the
    /// shape every one of its site quests and its trade request has (the mods' copies of them confirm it).
    /// </summary>
    private static readonly Sample[] Samples =
    {
        // ---- Out on the world map ----
        new Sample { name = "OpportunitySite_BanditCamp", from = "the game, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_GetSiteTile", "QuestNode_GetSitePartDefsByTagsAndFaction", "QuestNode_GenerateSite", "QuestNode_SpawnWorldObjects" } },
        new Sample { name = "OpportunitySite_ItemStash", from = "the game, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript", "QuestNode_GenerateSite" } },
        new Sample { name = "OpportunitySite_PrisonerWillingToJoin", from = "the game, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_GeneratePawn", "QuestNode_SubScript", "QuestNode_GenerateSite" } },
        new Sample { name = "TradeRequest", from = "the game, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetNearbySettlement", "QuestNode_TradeRequest_GetRequestedThing", "QuestNode_TradeRequest_Initiate" } },
        new Sample { name = "AC_ArtisanTradeRequest", from = "Alpha Crafts, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetNearbySettlement" } },
        new Sample { name = "VFEM2_OpportunitySite_SiegeCamp", from = "Vanilla Factions Expanded - Medieval 2, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_GetSiteTile" } },
        new Sample { name = "VFEM2_OpportunitySite_Skirmish", from = "Vanilla Factions Expanded - Medieval 2, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_GetSiteTile" } },
        new Sample { name = "VFEI2_OpportunitySite_InsectoidHive", from = "Vanilla Factions Expanded - Insectoids 2, xml (its VFEI2_Util_GenerateSite)", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_GetSiteTile", "QuestNode_GenerateSite" } },
        new Sample { name = "VFES_CaravanRaid", from = "Vanilla Factions Expanded - Settlers, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "VFES_Wanted", from = "Vanilla Factions Expanded - Settlers, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "VFED_AerodroneStation", from = "Vanilla Factions Expanded - Deserters, xml (runs VFED_DeserterQuestBase)", away = true,
            nodes = new[] { "QuestNode_SubScript", "QuestNode_Sequence", "QuestNode_GetSiteTile" } },
        new Sample { name = "VFED_DeserterEndgame", from = "Vanilla Factions Expanded - Deserters, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTileForComplex" } },
        new Sample { name = "GR_OpportunitySite_AbandonedLab", from = "Vanilla Genetics Expanded, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "AG_OpportunitySite_AbandonedBiotechLab", from = "Alpha Genes, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "AM_OpportunitySite_SanguophageCamp", from = "Alpha Memes, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "VPE_EltexMeteor", from = "Vanilla Psycasts Expanded, xml", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetSiteTile", "QuestNode_SubScript" } },
        new Sample { name = "VFEE_DeserterHideout", from = "Vanilla Factions Expanded - Empire, cs (QuestNode_Root_DeserterHideout)", away = true,
            nodes = new[] { "QuestNode_Root_DeserterHideout" },
            calls = new[] { "QuestGen_Get.GetMap", "TileFinder.TryFindNewSiteTile", "QuestGen_Sites.GenerateSite", "QuestGen_Sites.SpawnWorldObject", "QuestGen_Shuttle.GenerateShuttle" } },
        new Sample { name = "VFET_OpportunitySite_WildMen", from = "Vanilla Factions Expanded - Tribals, cs (QuestNode_Root_WildMen)", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_GetMap", "QuestNode_GetPawn", "QuestNode_Root_WildMen" },
            calls = new[] { "QuestGen_Sites.GenerateSite", "QuestGen_Sites.SpawnWorldObject" } },
        new Sample { name = "VQE_AncientLabComplex", from = "Vanilla Quests Expanded - Ancients, cs (QuestNode_Site, its base)", away = true,
            nodes = new[] { "QuestNode_Root_AncientLabComplex", "QuestNode_Site" },
            calls = new[] { "TileFinder.RandomSettlementTileFor", "QuestGen_Sites.GenerateSite", "QuestGen_Sites.SpawnWorldObject" } },
        new Sample { name = "VQE_ArcSite", from = "Vanilla Quests Expanded - The Generator, cs (QuestNode_Site, its base)", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_WorldObjectTimeout", "QuestNode_Root_ArcSite", "QuestNode_Site" },
            calls = new[] { "QuestGen_Get.GetMap", "QuestGen_Sites.GenerateSite", "QuestGen_Sites.SpawnWorldObject" } },
        new Sample { name = "VQE_Deadlife_AncientSilo", from = "Vanilla Quests Expanded - Deadlife, cs (QuestNode_Site, its base)", away = true,
            nodes = new[] { "QuestNode_Root_AncientSilo", "QuestNode_Site" }, calls = new[] { "QuestGen_Sites.GenerateSite" } },
        new Sample { name = "VQE_Cryptoforge_Chapter1", from = "Vanilla Quests Expanded - Cryptoforge, cs (QuestNode_Site, its base)", away = true,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_Root_CryptoforgeChapter1", "QuestNode_Site" }, calls = new[] { "QuestGen_Sites.GenerateSite" } },
        // A quest part made with a generic call counts as what it makes.
        new Sample { name = "(a mod's root that adds the spawn part itself)", from = "a generic call", away = true,
            nodes = new[] { "QuestNode_Root_Example" }, calls = new[] { "Quest.AddPart", "QuestPart_SpawnWorldObject." } },
        new Sample { name = "(a mod's root that makes its site by hand)", from = "SiteMaker", away = true,
            nodes = new[] { "QuestNode_Root_Example" }, calls = new[] { "SiteMaker.MakeSite" } },

        // ---- At home ----
        new Sample { name = "WandererJoins", from = "the game (QuestNode_Root_WandererJoin_WalkIn)", away = false,
            nodes = new[] { "QuestNode_Root_WandererJoin_WalkIn", "QuestNode_Root_WandererJoin" },
            calls = new[] { "QuestGen_Get.GetMap", "PawnGenerator.GeneratePawn", "Quest.PawnsArrive" } },
        new Sample { name = "AA_MimeWandererJoins", from = "Alpha Animals, cs (QuestNode_Root_MimeWandererJoin_WalkIn)", away = false,
            nodes = new[] { "QuestNode_Root_MimeWandererJoin_WalkIn", "QuestNode_Root_WandererJoin" }, calls = new[] { "PawnGenerator.GeneratePawn" } },
        new Sample { name = "VEE_WandererJoinsTraitor", from = "Vanilla Events Expanded, cs (QuestNode_Root_WandererJoinTraitor_WalkIn)", away = false,
            nodes = new[] { "QuestNode_Root_WandererJoinTraitor_WalkIn", "QuestNode_Root_WandererJoin_WalkIn", "QuestNode_Root_WandererJoin" } },
        new Sample { name = "VFEE_GrandBall", from = "Vanilla Factions Expanded - Empire, cs (QuestNode_Root_GrandBall)", away = false,
            nodes = new[] { "QuestNode_Root_GrandBall" }, calls = new[] { "QuestGen_Get.GetMap", "QuestGen_Shuttle.GenerateShuttle", "Quest.AddPart" } },
        new Sample { name = "VFEE_ArtExhibit", from = "Vanilla Factions Expanded - Empire, cs (Questnode_Root_ArtExhibit)", away = false,
            nodes = new[] { "Questnode_Root_ArtExhibit" }, calls = new[] { "QuestGen_Get.GetMap", "QuestGen_Shuttle.GenerateShuttle" } },
        new Sample { name = "VFEE_NobleVisit", from = "Vanilla Factions Expanded - Empire, cs (QuestNode_Root_NobleVisit)", away = false,
            nodes = new[] { "QuestNode_Root_NobleVisit" }, calls = new[] { "QuestGen_Get.GetMap" } },
        new Sample { name = "VFED_ChasedDeserter", from = "Vanilla Factions Expanded - Deserters, xml (runs Util_Raid)", away = false,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_GetMap", "QuestNode_Raid" } },
        new Sample { name = "VFED_EmpireBargain", from = "Vanilla Factions Expanded - Deserters, xml (runs Util_Raid)", away = false,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_Raid" } },
        new Sample { name = "Hospitality_Refugee", from = "Royalty (QuestNode_Root_Hospitality_Refugee)", away = false,
            nodes = new[] { "QuestNode_Root_Hospitality_Refugee" }, calls = new[] { "QuestGen_Get.GetMap", "QuestGen_Shuttle.GenerateShuttle", "Quest.ExtraFaction" } },
        new Sample { name = "ThreatReward_Raid_Joiner", from = "Royalty, xml (runs Util_Raid and Util_JoinerWalkIn)", away = false,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_SubScript", "QuestNode_Raid", "QuestNode_GeneratePawn", "QuestNode_PawnsArrive" } },
        // A site's own nodes inside a quest at home's tree (a timeout, a threat's points) don't make one.
        new Sample { name = "(a quest at home that times something out)", from = "names alone", away = false,
            nodes = new[] { "QuestNode_Sequence", "QuestNode_Delay", "QuestNode_GetSiteThreatPoints" } }
    };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "quests out on the world map: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        // ================= Which quests are out on the world map =================

        foreach (var s in Samples)
        {
            string why = QuestPlaceMath.Why(s.nodes, s.calls);
            C((why != null) == s.away, $"{s.name} ({s.from}): {(s.away ? "out on the world map" : "at home")}, read {(why ?? "at home")}");
        }
        C(Samples.Count(s => s.away) >= 20 && Samples.Count(s => !s.away) >= 10, "a good sample of each side");
        C(QuestPlaceMath.AwayNode("QuestNode_GetSiteTile") && QuestPlaceMath.AwayNode("QuestNode_GetSiteTileForComplex") && QuestPlaceMath.AwayNode("VFEI2_QuestNode_GenerateSiteHive")
          && !QuestPlaceMath.AwayNode("QuestNode_GetSiteThreatPoints") && !QuestPlaceMath.AwayNode("QuestNode_WorldObjectTimeout") && !QuestPlaceMath.AwayNode(null),
            "a node by its name: a site's tile or a site made, not a site's threat points or its timeout");
        C(QuestPlaceMath.AwayCall("TileFinder.TryFindNewSiteTile") && !QuestPlaceMath.AwayCall("TileFinder.RandomSettlementTileFor")
          && QuestPlaceMath.AwayCall("SiteMaker.TryMakeSite_SingleSitePart") && QuestPlaceMath.AwayCall("QuestPart_InitiateTradeRequest..ctor")
          && !QuestPlaceMath.AwayCall("QuestGen_Shuttle.GenerateShuttle") && !QuestPlaceMath.AwayCall("QuestGen_Get.GetMap") && !QuestPlaceMath.AwayCall(""),
            "a call by its type and member: a new site's tile, a site, a trade request; not a shuttle or the home map");

        // ================= The share, and the mix it makes =================

        C(QuestPlaceMath.Share(QuestPlaceMath.DefaultAwayShare) == 0.5f && QuestPlaceMath.Share(0f) == QuestPlaceMath.MinAwayShare && QuestPlaceMath.Share(3f) == 1f
          && QuestPlaceMath.Share(float.NaN) == QuestPlaceMath.DefaultAwayShare, "the share: 50% to begin with, 10% to 100%");
        C(QuestPlaceMath.WeightFactor(true, 0.5f) == 0.5f && QuestPlaceMath.WeightFactor(false, 0.5f) == 1f && QuestPlaceMath.WeightFactor(true, 1f) == 1f,
            "only a quest out on the world map weighs less; one at home keeps its weight");
        // The player's save: 46 of 77 quests out on the map, about six in ten.
        float before = 46f / 77f, after = QuestPlaceMath.AwayAfter(before, 0.5f);
        C(after > 0.38f && after < 0.46f && Math.Abs(QuestPlaceMath.AwayAfter(0.6f, 0.5f) - 0.4286f) < 0.001f, $"at 50%, six in ten out on the map become about four in ten ({after:0.00})");
        C(QuestPlaceMath.AwayAfter(0.6f, 1f) == 0.6f && QuestPlaceMath.AwayAfter(0.6f, 0.1f) < 0.15f && QuestPlaceMath.AwayAfter(1f, 0.5f) == 1f && QuestPlaceMath.AwayAfter(0f, 0.5f) == 0f,
            "100% as the game has it; 10% about one in seven; with nothing at home, still a quest out on the map");

        // ================= How far a site may be =================

        void Range(int min, int max, QuestSiteRange r, int emin, int emax)
        {
            QuestPlaceMath.SiteRange(min, max, r, out int a, out int b);
            C(a == emin && b == emax, $"a site asked for {min} to {max} tiles, {r}: {emin} to {emax} (got {a} to {b})");
        }
        Range(7, 27, QuestSiteRange.AsTheGame, 7, 27);
        Range(7, 27, QuestSiteRange.Nearer, 7, 16);
        Range(7, 27, QuestSiteRange.Near, 4, 9);
        Range(2, 10, QuestSiteRange.Nearer, 2, 6);
        Range(2, 10, QuestSiteRange.Near, 2, 6);
        Range(3, 6, QuestSiteRange.Near, 3, 6);
        Range(15, 30, QuestSiteRange.Near, 5, 11);
        Range(10, 10, QuestSiteRange.Near, 10, 10);
        for (int min = 0; min <= 30; min++)
        for (int max = 0; max <= 60; max++)
        foreach (QuestSiteRange r in Enum.GetValues(typeof(QuestSiteRange)))
        {
            QuestPlaceMath.SiteRange(min, max, r, out int a, out int b);
            bool ok = b <= max && a <= min && (a < b || (a == min && b == max)) && (b == max || b >= QuestPlaceMath.CloseEnough) && a >= 0;
            if (!ok) C(false, $"{min} to {max} at {r}: never farther, never an empty range, never closer than {QuestPlaceMath.CloseEnough} at the farthest ({a} to {b})");
        }
        C(QuestPlaceMath.GameFarthest(QuestSiteRange.AsTheGame) == 27 && QuestPlaceMath.GameFarthest(QuestSiteRange.Nearer) == 16 && QuestPlaceMath.GameFarthest(QuestSiteRange.Near) == 9,
            "the game's 27 tiles: 16 nearer, 9 near");

        // ================= The events record =================

        string l = EventRecordMath.Line(120000, EventKind.GodCard, "A light | over\nthe well");
        C(EventRecordMath.TryParse(l, out int t, out var k, out var w) && t == 120000 && k == EventKind.GodCard && w == "A light / over the well", "a line read back as it was kept, no break in it");
        C(!EventRecordMath.TryParse("", out _, out _, out _) && !EventRecordMath.TryParse("x|1|a", out _, out _, out _) && !EventRecordMath.TryParse("5|99|a", out _, out _, out _),
            "a line that can't be read is let go");
        int day = EventRecordMath.TicksPerDay, now = 100 * day;
        var lines = new List<string>
        {
            EventRecordMath.Line(now - 31 * day, EventKind.Threat, "Raid"),
            EventRecordMath.Line(now - 29 * day, EventKind.Threat, "Raid"),
            EventRecordMath.Line(now - 10 * day, EventKind.Everyday, "Trader caravan"),
            "broken"
        };
        EventRecordMath.Prune(lines, now);
        C(lines.Count == 2 && lines[0].StartsWith((now - 29 * day).ToString()), "kept 30 days, the older and the unreadable let go");
        var many = new List<string>();
        for (int i = 0; i < EventRecordMath.MaxLines + 50; i++) many.Add(EventRecordMath.Line(now - i * 100, EventKind.Everyday, "e" + i));
        many.Reverse();
        EventRecordMath.Prune(many, now);
        C(many.Count == EventRecordMath.MaxLines && many[^1].EndsWith("|e0"), $"at most {EventRecordMath.MaxLines} lines, the oldest let go first");
        var held = new List<string>();
        C(!EventRecordMath.HeldToday(held, EventRecordMath.HeldWait, now), "nothing held yet");
        held.Add(EventRecordMath.Line(now, EventKind.Held, EventRecordMath.HeldWait));
        C(EventRecordMath.HeldToday(held, EventRecordMath.HeldWait, now + 1000) && !EventRecordMath.HeldToday(held, EventRecordMath.HeldFight, now + 1000)
          && !EventRecordMath.HeldToday(held, EventRecordMath.HeldWait, now + day), "a held reason once a day each");
        var entries = new List<(int, EventKind, string)>
        {
            (now - 5 * day, EventKind.Threat, "Raid"), (now - 3 * day, EventKind.Threat, "Manhunter pack"), (now - 2 * day, EventKind.Threat, "Infestation"),
            (now - 1 * day, EventKind.Threat, "Raid"), (now - 40 * day, EventKind.Threat, "Old raid"), (now - 2 * day, EventKind.QuestAway, "Bandit camp"),
            (now - 2 * day, EventKind.Held, EventRecordMath.HeldWait), (now - 1 * day, EventKind.Held, EventRecordMath.HeldWait), (now - 1 * day, EventKind.Held, EventRecordMath.HeldFight)
        };
        var sums = EventRecordMath.Sum(entries, now);
        C(sums[EventKind.Threat].count == 4 && sums[EventKind.Threat].last.Count == EventRecordMath.LastShown && sums[EventKind.Threat].last[0].what == "Raid"
          && sums[EventKind.Threat].last[0].day == EventRecordMath.Day(now - day) && sums[EventKind.Threat].last[2].what == "Manhunter pack",
            "each kind counted in the 30 days, its newest three named with their day, newest first");
        C(sums.ContainsKey(EventKind.QuestAway) && !sums.ContainsKey(EventKind.Held) && !sums.ContainsKey(EventKind.Rimfolk), "quests counted beside the rest; held lines apart; a kind with none has no card's count");
        var hc = EventRecordMath.HeldCounts(entries, now);
        C(hc[EventRecordMath.HeldWait] == 2 && hc[EventRecordMath.HeldFight] == 1 && !hc.ContainsKey(EventRecordMath.HeldDrawWait), "held reasons counted by days");
        C(EventRecordMath.Day(0) == 1 && EventRecordMath.Day(day * 134 + 5) == 135, "days counted from 1, as Rimfolk says them");

        // ================= Rimfolk's own timer besides the storyteller's =================

        C(!EventRecordMath.BesidesOn(0f) && !EventRecordMath.BesidesOn(float.NaN) && EventRecordMath.BesidesOn(0.05f), "none at 0%, as before");
        C(EventRecordMath.BesidesDays(1f) == 4f && EventRecordMath.BesidesDays(0.5f) == 8f && EventRecordMath.BesidesDays(0.25f) == 16f && float.IsPositiveInfinity(EventRecordMath.BesidesDays(0f)),
            "one about every 4 days at 100%, 8 at 50%, 16 at 25%");

        // ================= The settings =================

        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public float ownTimerShare = 0f;") && settings.Contains("public float awayQuestShare = 0.5f;")
          && settings.Contains("public QuestSiteRange questSiteRange = QuestSiteRange.Nearer;"), "the defaults: none besides, 50% out on the map, nearer sites");
        var own = SettingsCatalog.Find("ownTimerShare");
        var away = SettingsCatalog.Find("awayQuestShare");
        var sites = SettingsCatalog.Find("questSiteRange");
        C(own != null && own.page == SettingsPage.HowMuch && own.needs == "worldEvents,eventsFromStoryteller" && own.min == 0f && own.max == 1f,
            "the middle way beside the storyteller's weight, only while the storyteller draws Rimfolk's events");
        C(away != null && away.page == SettingsPage.HowMuch && away.min == QuestPlaceMath.MinAwayShare && away.max == 1f && away.needs == "deepHooks",
            "the share of quests out on the world map on How much happens, 10% to 100%, with the deeper game hooks");
        C(sites != null && sites.kind == SettingKind.Choice && sites.options.SequenceEqual(new[] { "AsTheGame", "Nearer", "Near" }) && sites.needs == "deepHooks",
            "how far quest sites may be: as the game has it, nearer, near");

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(f).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (EventKind kind in Enum.GetValues(typeof(EventKind)))
            if (kind != EventKind.Held) C(keyed.ContainsKey("CC_Record_Kind_" + kind), $"the record names its kind {kind}");
        foreach (var reason in EventRecordMath.HeldReasons) C(keyed.ContainsKey("CC_Record_Held_" + reason), $"the record says why for {reason}");
        var own2 = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_EventRecord.xml")).Root!.Elements().ToList();
        C(own2.Count >= 40 && own2.All(e => !e.Value.Contains('—') && !e.Value.Contains('!')), "plain words: no em dash, no exclamation mark");
        string awayTip = keyed.GetValueOrDefault("CC_Settings_AwayQuests_Tip") ?? "";
        C(awayTip.Contains("about four in ten") && awayTip.Contains("never held back") && awayTip.Contains("Work sites"), "the share's words say what it does, as the numbers have it");
        string siteTip = keyed.GetValueOrDefault("CC_Settings_QuestSites_Tip") ?? "";
        C(siteTip.Contains("between 7 and 27") && siteTip.Contains("16 tiles of 27") && siteTip.Contains("(9 tiles), and as close as 4") && siteTip.Contains("as the game would"),
            "the range's words say the numbers the rule has");
        string ownTip = keyed.GetValueOrDefault("CC_Settings_OwnTimerShare_Tip") ?? "";
        C(ownTip.Contains("every 4 days") && ownTip.Contains("about every 8 days") && ownTip.Contains("about every 16 days") && ownTip.Contains("At 0%, none"),
            "the middle way's words say its pace");

        // ================= The seams =================

        string patches = Code("Story/QuestPlacePatches.cs");
        C(Regex.Matches(patches, @"\[HarmonyPatch").Count >= 5 && patches.Contains("RimfolkPatches.On"), "every hook a [HarmonyPatch] class, off with the deeper game hooks");
        C(patches.Contains("nameof(NaturalRandomQuestChooser.ChooseNaturalRandomQuest)") && patches.Contains("Patch_QuestSlotChoosing.choosing <= 0"),
            "the weight changes only while the quest slot chooses");
        C(patches.Contains("StorytellerComp_WorkSite") && patches.Contains("Rand.Chance(QuestPlaces.Share)"), "the work sites by their own slot, at the share");
        C(patches.Contains("def.category != IncidentCategoryDefOf.GiveQuest") && patches.Contains("workSites.Contains(def)"),
            "a quest incident of its own in the quest slot, the work sites' left to their slot");
        C(patches.Contains("QuestGen.quest == null") && patches.Contains("__originalMethod.Invoke(null, args)"), "sites nearer only while a quest is made, and as the game asked when nothing that near fits");
        string places = Code("Story/QuestPlaces.cs");
        C(places.Contains("PatchProcessor.ReadMethodBody") && places.Contains("SlateRef<QuestScriptDef>") && places.Contains("QuestPart_SpawnWorldObject"),
            "the scripts read by their nodes, the scripts they run, their code, and what the game's quests made");
        C(!places.Contains("\"OpportunitySite_") && !patches.Contains("\"OpportunitySite_"), "no quest named: the game's, the DLCs' and every mod's read the same way");
        string happenings = Code("Happenings/Happenings.cs");
        C(happenings.Contains("if (!OwnTimerRuns)") && happenings.Contains("EventRecordMath.BesidesDays(S.ownTimerShare)")
          && happenings.Contains("EventRecord.Held(waiting ? EventRecordMath.HeldWait : EventRecordMath.HeldFight)"),
            "the own timer runs besides at its share, and says when it waited");
        string record = Code("Story/EventRecord.cs");
        C(record.Contains("[HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]") && record.Contains("ChoiceLetter_Dilemma") && record.Contains("GodLetter")
          && record.Contains("Scribe_Collections.Look(ref lines, \"lines\", LookMode.Value)"), "the record hears the storyteller, the questions and the cards, and keeps its lines");
        C(Code("UI/OverviewPanel.cs").Contains("EventRecordPanel.Draw("), "the record shows on the Overview");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("\"Story/EventRecord.cs:lines\""), "the record's lines have a model in the save estimate");
        return n;
    }
}
