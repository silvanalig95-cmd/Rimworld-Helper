using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2a.1 (docs/design/stage2-diplomacy.md): its base (the history's family, the deeds, the
/// pages' places, the purposes and acts in order, a people's stance, the model's rebuild), favours owed and called in
/// (Diplomacy/FavourMath.cs: earned by a deed's size and kind, one each way, renewed, honoured by regard, their calls'
/// rate, needs and season, goods by fortune and age, the asks kept in a tie's terms), the nature service with no effect
/// (Diplomacy/NatureMath.cs), the two reserved tie kinds, five simulated years of their calls, and the mod's side: Tell
/// only, ties only (nothing else saved), the hooks where the design puts them, the words.
/// </summary>
public static class DiplomacyTests
{
    private const int D = FavourMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "diplomacy: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Earning a debt ----------------

        C(FavourMath.Earns("foundChild", true, 18) && FavourMath.Earns("released", true, 10) && !FavourMath.Earns("released", true, 9),
            "a good turn of size 10 or more in their memory makes a debt, not a smaller one");
        C(!FavourMath.Earns("gift", true, 30) && !FavourMath.Earns("traded", true, 12) && !FavourMath.Earns("paidOff", true, 10) && !FavourMath.Earns("keptWord", true, 10),
            "a gift, a trade, a payment or the colony's word kept leaves no debt");
        C(FavourMath.Earns("relief", false, 10) && FavourMath.Earns("letterAid", false, 12) && !FavourMath.Earns("letterAid", false, 6) && !FavourMath.Earns("relief", true, 10),
            "their good turns leave the colony owing them, by the same size");
        C(!FavourMath.Earns("granted", false, 25) && !FavourMath.Earns("amends", false, 10) && !FavourMath.Earns("broughtAsked", false, 12),
            "a grant of land, amends and a favour brought on a visit are no new debt");
        C(FavourMath.OurGoodTurns.Distinct().Count() == FavourMath.OurGoodTurns.Length && FavourMath.TheirGoodTurns.Distinct().Count() == FavourMath.TheirGoodTurns.Length
          && !FavourMath.OurGoodTurns.Intersect(FavourMath.TheirGoodTurns).Any(), "each good turn counts one way only");
        string memory = Code("World/FactionMemory.cs");
        foreach (var k in FavourMath.OurGoodTurns.Concat(FavourMath.TheirGoodTurns))
            C(memory.Contains("[\"" + k + "\"]"), $"{k} is a deed the peoples remember");
        foreach (var k in FavourMath.OurGoodTurns)
            C(Regex.IsMatch(memory, "\\[\"" + k + "\"\\] = K\\(DeedSide\\.Favor"), $"{k} is the colony's good turn");
        foreach (var k in FavourMath.TheirGoodTurns)
            C(Regex.IsMatch(memory, "\\[\"" + k + "\"\\] = K\\(DeedSide\\.TheirFavor"), $"{k} is theirs");
        C(FavourMath.Until(1000) == 1000 + 120 * D && FavourMath.MaxPersonal == 12, "let go two years after, twelve personal at most");

        // ---------------- Honoured by regard ----------------

        C(FavourMath.HonourChance(20) == 1f && FavourMath.HonourChance(90) == 1f, "at a regard of 20 or more they always honour it");
        C(FavourMath.HonourChance(19) == 0.7f && FavourMath.HonourChance(0) == 0.7f, "seven in ten below 20");
        C(FavourMath.HonourChance(-1) == 0.4f && FavourMath.HonourChance(-100) == 0.4f, "four in ten below 0");
        C(FavourMath.Honours(10, 0.69f) && !FavourMath.Honours(10, 0.71f) && FavourMath.Honours(-5, 0.39f) && !FavourMath.Honours(-5, 0.41f), "the roll against the chance");

        // ---------------- Their calls ----------------

        C(Math.Abs(FavourMath.DailyChance - 1f / 90f) < 1e-6f, "called in at a mean of 90 days");
        C(FavourMath.AskAllowed(-1, 5 * D) && !FavourMath.AskAllowed(10 * D, 24 * D) && FavourMath.AskAllowed(10 * D, 25 * D), "one ask a season per people");
        C(FavourMath.DueDays(FavourNeed.Medicine) == 3f && FavourMath.DueDays(FavourNeed.Food) == 3f, "three days to answer an ask for goods");
        C(FavourMath.DueDays(FavourNeed.Harvest) > 10f && FavourMath.DueDays(FavourNeed.Refugees) > 10f, "a mission has its own course");
        C(FavourMath.Overdue(FavourMath.DueDay(FavourNeed.Medicine, 0), 3 * D) && !FavourMath.Overdue(FavourMath.DueDay(FavourNeed.Medicine, 0), 3 * D - 1),
            "unanswered in three days, it is overdue");
        C(!FavourMath.Overdue(0f, 100 * D), "no due day, never overdue");
        C(FavourMath.RefusedGoodwill == -5, "refused or unanswered: 5 goodwill");
        C(FavourMath.ByMission(FavourNeed.Harvest) && FavourMath.ByMission(FavourNeed.Refugees) && !FavourMath.ByMission(FavourNeed.Medicine) && !FavourMath.ByMission(FavourNeed.Dispute),
            "hands at harvest and refugees through the existing missions");
        C(FavourMath.Answers(FavourNeed.Harvest, "harvestHelp") && FavourMath.Answers(FavourNeed.Refugees, "refugees") && FavourMath.Answers(FavourNeed.Dispute, "sidedWith")
          && !FavourMath.Answers(FavourNeed.Harvest, "refugees") && !FavourMath.Answers(FavourNeed.Medicine, "harvestHelp"), "the deed that answers each need");
        C(FavourMath.MissionKind(FavourNeed.Harvest) == "harvest" && FavourMath.MissionKind(FavourNeed.Refugees) == "refugees" && FavourMath.MissionKind(FavourNeed.Food) == null,
            "the missions' own kinds");
        string missions = Code("Missions/Workers_MissionRoad.cs") + Code("Missions/Workers_MissionRemembered.cs");
        C(missions.Contains("protected override string Kind => \"harvest\";") && missions.Contains("protected override string Kind => \"refugees\";"),
            "the harvest and refugee missions are those kinds");
        C(missions.Contains("KinMemory.Note(m.faction, \"harvestHelp\"") && missions.Contains("FactionMemory.Note(m.faction, \"refugees\""), "and note the deeds that answer them");

        // Five years of a debt the colony owes, a need shown half the days: calls come about every 90 days, never twice a season.
        var rng = new Random(7);
        int calls = 0, lastAsk = -1, worstSeason = 0;
        var perSeason = new Dictionary<int, int>();
        for (int day = 0; day < 300; day++)
        {
            int now = day * D;
            if (!FavourMath.CallsToday((float)rng.NextDouble())) continue;
            if (rng.NextDouble() < 0.5 || !FavourMath.AskAllowed(lastAsk, now)) continue;
            calls++;
            lastAsk = now;
            int season = day / 15;
            perSeason[season] = perSeason.TryGetValue(season, out int c) ? c + 1 : 1;
            worstSeason = Math.Max(worstSeason, perSeason[season]);
        }
        C(calls >= 1 && calls <= 4 && worstSeason <= 1, $"five years of one debt: {calls} calls, at most one a season");

        // ---------------- What the colony asks ----------------

        C(Math.Abs(FavourMath.GoodsWorth(50f) - 400f) < 0.01f && Math.Abs(FavourMath.GoodsWorth(0f) - 300f) < 0.01f && Math.Abs(FavourMath.GoodsWorth(100f) - 500f) < 0.01f,
            "about 400 silver's worth by their town's fortune");
        C(FavourMath.GoodsCount(400f, 1f) == 400 && FavourMath.GoodsCount(400f, 18f) == 22 && FavourMath.GoodsCount(5f, 50f) == 1 && FavourMath.GoodsCount(400f, 0f) == 0,
            "how many make up the load");
        C(!FavourMath.GoodsKinds(WorldAge.Tribal).Contains("steel") && !FavourMath.GoodsKinds(WorldAge.Tribal).Contains("silver")
          && FavourMath.GoodsKinds(WorldAge.Medieval).Contains("steel") && FavourMath.GoodsKinds(WorldAge.Spacer).Contains("silver") && !FavourMath.GoodsKinds(WorldAge.Spacer).Contains("wood"),
            "the goods by their ways");
        foreach (var age in new[] { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer })
            C(FavourMath.GoodsKinds(age).Contains("food") && FavourMath.GoodsKinds(age).Contains("medicine"), $"{age}: food and medicine always");
        C(FavourMath.WordGoodwill == 10 && FavourMath.PeaceRegard == 20, "a word is 10 goodwill, help toward peace 20 better");
        foreach (FavourAsk a in Enum.GetValues(typeof(FavourAsk)))
        {
            string ac = FavourMath.AskCode(a, "f12");
            C(FavourMath.ParseAsk(ac, out var back, out var arg) && back == a && arg == "f12" && !FavourMath.ParseNeed(ac, out _), $"the ask {a} kept in a tie's terms and read back");
        }
        foreach (FavourNeed need in Enum.GetValues(typeof(FavourNeed)))
        {
            string nc = FavourMath.NeedCode(need);
            C(FavourMath.ParseNeed(nc, out var back) && back == need && !FavourMath.ParseAsk(nc, out _, out _), $"the need {need} kept in a tie's terms and read back");
        }
        C(!FavourMath.ParseAsk(null, out _, out _) && !FavourMath.ParseAsk("ask:Levies:x", out _, out _) && !FavourMath.ParseNeed("need:", out _) && !FavourMath.ParseNeed("Steel", out _),
            "anything else is no ask");
        C(FavourMath.Personal(Ref.Person(5), Ref.Person(6)) && !FavourMath.Personal(Ref.People(3), Ref.Colony(9)), "a personal favour: a person owes a person");
        C(FavourMath.ColonyOwes(Ref.Colony(9), Ref.People(3)) && !FavourMath.ColonyOwes(Ref.People(3), Ref.Colony(9)), "which way a debt runs");

        // ---------------- The base ----------------

        C(DiplomacyMath.Kinds.All(k => HistoryKinds.Family(k) == DiplomacyMath.Family) && DiplomacyMath.Kinds.Distinct().Count() == DiplomacyMath.Kinds.Length,
            "the history's family dip: for what isn't a tie");
        foreach (var k in new[] { "dip:ransomed", "dip:exchanged", "dip:envoy", "dip:feast", "dip:contest", "dip:wedding", "dip:task", "dip:tribute", "dip:levy", "dip:foundout", "dip:renown" })
            C(DiplomacyMath.Kinds.Contains(k), $"the design's {k} is in the family");
        C(HistoryKinds.Matches(DiplomacyMath.Word, DiplomacyMath.Family), "a query for dip finds its kinds");
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        C(DiplomacyMath.Deeds.Length == 20 && DiplomacyMath.Deeds.Select(d => d.kind).Distinct().Count() == 20, "the design's twenty new deeds");
        foreach (var (kind, side, size) in DiplomacyMath.Deeds)
        {
            C(Regex.IsMatch(memory, "\\[\"" + kind + "\"\\] = K\\(DeedSide\\." + side + ", " + size + "[,)]"), $"{kind} is remembered, {side}, size {size}");
            C(keyedAll.Contains("<CC_Memory_Deed_" + kind + ">"), $"{kind} has its words");
        }
        C(DiplomacyMath.DiplomacyOrder > 30f && DiplomacyMath.DiplomacyOrder < DiplomacyMath.HouseholdOrder && DiplomacyMath.HouseholdOrder < 40f,
            "the Diplomacy and Household pages after Letters, before Journeys");
        C(DiplomacyMath.Purposes[0] == "Favour" && DiplomacyMath.Purposes.Length == 8 && DiplomacyMath.Acts[0] == "AskFavour", "the purposes and acts in the design's order, the first built");
        // Built so far, in the order the releases built them (saved by name): Favour; Pact and Truce in 2a.2; Ransom and Envoy
        // in 2a.3; Ward in 2b.3, Match in 2b.2 (merged after it; Fealty waits for its release).
        var builtPurposes = DiplomacyMath.Purposes.Where(p => p != "Match" && p != "Ward" && p != "Fealty").Append("Ward").Append("Match").ToArray();
        C(Enum.GetNames(typeof(LetterPurpose)).Reverse().Take(builtPurposes.Length).Reverse().SequenceEqual(builtPurposes),
            "the letters' new purposes after the old ones, in the order they were built (Favour; Pact and Truce in 2a.2; Ransom and Envoy in 2a.3; Ward in 2b.3; Match in 2b.2)");
        var builtActs = DiplomacyMath.Acts.Where(a => a != "SpeakMatch" && a != "SoundOut").Append("SpeakMatch").ToArray();
        C(Enum.GetNames(typeof(OutsiderAct)).Reverse().Take(builtActs.Length).Reverse().SequenceEqual(builtActs),
            "the new acts after the old ones, in the order they were built (saved by name; OfferExchange in 2a.3, SpeakMatch in 2b.2)");
        C(DiplomacyMath.StanceKey(0, true, false) == "Hostile" && DiplomacyMath.StanceKey(10, false, true) == "Allied" && DiplomacyMath.StanceKey(80, false, false) == "Allied"
          && DiplomacyMath.StanceKey(30, false, false) == "Friendly" && DiplomacyMath.StanceKey(0, false, false) == "Peace" && DiplomacyMath.StanceKey(-10, false, false) == "Cool",
            "a people's stance in a word");
        foreach (var s in new[] { "Allied", "Friendly", "Peace", "Cool", "Hostile" }) C(keyedAll.Contains("<CC_Dip_Stance_" + s + ">"), $"the stance {s} has its words");
        C(DiplomacyMath.Stale(-1f, 0f, 1, 1) && DiplomacyMath.Stale(0f, 1f, 1, 2) && !DiplomacyMath.Stale(0f, 1f, 1, 1) && DiplomacyMath.Stale(0f, 2f, 1, 1),
            "the pages' model built again when stale, at most every two seconds");
        C(DiplomacyMath.Version(1, 2, 3, 4) != DiplomacyMath.Version(1, 2, 3, 5) && DiplomacyMath.Version(1, 2, 3, 4) != DiplomacyMath.Version(2, 2, 3, 4), "any change moves the version");

        // ---------------- Reserved tie kinds ----------------

        C(TieRules.Kinds == 14 && TieRules.MaxTies == 1080, "Secret and Scheme reserved: 14 kinds, 1,080 ties at most");
        C(TieRules.CanCallIn(Tie.Kind.Secret) && !TieRules.CanCallIn(Tie.Kind.Scheme) && TieRules.CanKeepOrBreak(Tie.Kind.Scheme) && TieRules.MaxOf(Tie.Kind.Secret) == 60,
            "a secret can be called in, a scheme kept or broken, each 60 at most");
        C(!Regex.IsMatch(string.Concat(Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").Select(File.ReadAllText)), @"Tie\.Kind\.(Secret|Scheme)"), "nothing uses them yet");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("const(\"World/Ledger/TieMath.cs\", \"Kinds\", 14) - 1"), "the save model's bound of all ties reads the kinds");

        // ---------------- Nature ----------------

        C(!NatureMath.SwitchedOn, "the nature service is written, with no effect until round d");
        foreach (NatureAct a in Enum.GetValues(typeof(NatureAct))) C(NatureMath.Rules.Any(r => r.act == a), $"{a} goes against someone's nature");
        var none = new float[PersonaMath.Axes];
        C(NatureMath.Strain(NatureAct.SworeFealty, none) == 0f && NatureMath.Relief(NatureAct.SworeFealty, none) == 0f, "an ordinary person feels nothing of most acts");
        var proud = new float[PersonaMath.Axes];
        proud[(int)PersonaAxis.Pride] = 80f;
        C(NatureMath.Strain(NatureAct.SworeFealty, proud) > 0f && NatureMath.Strain(NatureAct.SworeFealty, proud) <= NatureMath.MaxStrain, "an oath weighs on the proud, 12 at most");
        var very = new float[PersonaMath.Axes];
        very[(int)PersonaAxis.Pride] = 100f;
        C(Math.Abs(NatureMath.Strain(NatureAct.SworeFealty, very) - 12f) < 0.01f && NatureMath.Strain(NatureAct.SworeFealty, very) > NatureMath.Strain(NatureAct.SworeFealty, proud),
            "the further past it, the more");
        var cold = new float[PersonaMath.Axes];
        cold[(int)PersonaAxis.Warmth] = -70f;
        C(NatureMath.Strain(NatureAct.DarkScheme, cold) == 0f && NatureMath.Relief(NatureAct.DarkScheme, cold) > 0f && NatureMath.Relief(NatureAct.DarkScheme, cold) <= NatureMath.MaxRelief,
            "a dark scheme eases the cold, 6 at most");
        C(NatureMath.Strain(NatureAct.TruceWithFoe, none) >= NatureMath.AlwaysStrain && NatureMath.Strain(NatureAct.MatchAgainstHeart, none) >= NatureMath.AlwaysStrain,
            "a truce with one's foe and a match against the heart weigh on anyone");
        var open = new float[PersonaMath.Axes];
        open[(int)PersonaAxis.Openness] = 60f;
        var closed = new float[PersonaMath.Axes];
        closed[(int)PersonaAxis.Openness] = -60f;
        C(NatureMath.Strain(NatureAct.SeasonAway, closed) > 0f && NatureMath.Relief(NatureAct.SeasonAway, open) > 0f, "a season away weighs on the closed and eases the open");
        C(NatureMath.MayFeel(-1, 0) && !NatureMath.MayFeel(0, D - 1) && NatureMath.MayFeel(0, D), "one act a day per colonist");
        C(!Code("Diplomacy/Nature.cs").Contains("HeartLogic") && !Code("Diplomacy/Nature.cs").Contains("Interplay.Ease"), "nothing is felt yet");

        // ---------------- The mod's side ----------------

        var dip = Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        string code = string.Concat(dip.Values);
        C(dip.ContainsKey("FavourMath.cs") && dip.ContainsKey("DiplomacyMath.cs") && dip.ContainsKey("NatureMath.cs") && dip.ContainsKey("FavoursOwed.cs") && dip.ContainsKey("FavourCalls.cs")
          && dip.ContainsKey("FavourAsks.cs") && dip.ContainsKey("DiplomacyPanels.cs") && dip.ContainsKey("DiplomacyModel.cs") && dip.ContainsKey("DiplomacyLinks.cs"),
            "the parts in Diplomacy/");
        foreach (var f in new[] { "FavourMath.cs", "DiplomacyMath.cs", "NatureMath.cs" })
            C(!Regex.IsMatch(dip[f], @"using (Verse|RimWorld|UnityEngine)"), $"{f} is pure");
        foreach (var (file, text) in dip.Where(kv => !kv.Key.EndsWith("Debug.cs") && kv.Key != "DiplomacyPanels.cs"))
            C(!text.Contains("Messages.Message(") && !text.Contains("Find.LetterStack.ReceiveLetter("), $"{file} tells the player only through Tell");
        C(!Regex.IsMatch(string.Concat(dip.Where(kv => kv.Key.StartsWith("Favour") && kv.Key != "FavourAsks.cs" || kv.Key.StartsWith("Diplomacy") && kv.Key != "DiplomacyBook.cs").Select(kv => kv.Value)), @"Scribe_"),
            "favours keep nothing but the ledger's ties (only their letter saves which call it is; pacts and truces: PactTests; the records that aren't ties: DiplomacyBook, RansomTests)");
        C(Regex.IsMatch(dip["FavourAsks.cs"], @"class ChoiceLetter_FavourAsk[\s\S]*Scribe_Values\.Look\(ref tie"), "their letter knows its tie");
        C(code.Contains("WorldLedger.AddTie(Tie.Kind.Favour") && code.Contains("FavourMath.Origin") && code.Contains("WorldLedger.AddTie(Tie.Kind.Promise"),
            "debts are Favour ties of their own origin, help toward peace a Promise");
        C(!code.Contains("Favours.Ask(") && Code("Outsiders/OutsiderTies.cs").Contains("public const string Origin = \"outsiders\";"), "the outsiders' favours to bring keep their own origin");
        C(memory.Contains("FavoursOwed.Noted(d);") && Code("World/KinMemory.cs").Contains("FavoursOwed.Personal(d, stranger, pick.colonist);"),
            "debts are made where the memory notes a deed and names who did it");
        C(Code("World/Notables.cs").Contains("Diplomacy.Hourly(now)") && !Code("Lean/Sched.cs").Contains("Diplomacy"), "its hour rides on the neighbours' pass: no slot of its own");
        C(Code("Missions/Missions.cs").Contains("FavourAsks.MissionEnded(m, state);") && Code("Missions/Workers_MissionRemembered.cs").Contains("public static Faction Only;"),
            "their calls through the existing missions, and the mission's end answers them");
        string letters = Code("World/NeighbourLetters.cs");
        C(letters.Contains("DiplomacyLetters.WhyNot(n, p)") && letters.Contains("DiplomacyLetters.Topics(n, p)") && letters.Contains("DiplomacyLetters.Read(l, n, speaker ?? l.writer, ref tone)"),
            "the letters' frame: why not, what about, the answer");
        string menu = Code("Outsiders/OutsiderMenu.cs");
        C(Regex.IsMatch(menu, @"OutsiderAct\.Invite,[\s\S]{0,200}OutsiderAct\.AskFavour, OutsiderAct\.SpeakPact, OutsiderAct\.SpeakMatch, OutsiderAct\.OfferExchange,[\s\S]{0,40}OutsiderAct\.Spar"),
            "the new acts right after Invite, in the design's order");
        C(Code("Outsiders/OutsiderOutcomes.cs").Contains("DiplomacyActs.Talked(c, x, act)") && Code("Outsiders/Outsiders.cs").Contains("DiplomacyActs.WhyNot(c, x, act)"),
            "the right-click frame: shown, why not, answered face to face");
        C(!dip["DiplomacyPanels.cs"].Contains("MainTabWindow_World.Register(") && Code("UI/Region/PeoplesView.cs").Contains("DiplomacyPanels.Owed(y, w, dip, folds)")
          && Code("UI/Region/PeoplesView.cs").Contains("DiplomacyPanels.PeopleBody(") && Code("UI/Region/HereAndAwayView.cs").Contains("dip.here")
          && Code("UI/Region/HereAndAwayView.cs").Contains("dip.away") && Code("UI/Region/HereAndAwayView.cs").Contains("dip.houses")
          && dip["DiplomacyPanels.cs"].Contains("CardSections.Town(CardSlot.Ties, 10f, DrawTown, \"CC_Dip_TownHeader\""),
            "the panels in the region's views (the owed strip and each people's ties on Peoples; here, away and houses on Here and away), and the town's debts in its card's Ties");
        C(dip["DiplomacyLinks.cs"].Contains("Cards.Open(") && !dip["DiplomacyLinks.cs"].Contains("CameraJumper"), "a name in diplomacy's rows opens its card");
        C(Regex.Matches(dip["DiplomacyPanels.cs"] + dip["DiplomacyModel.cs"], @"DiplomacyLinks\.Open\(").Count >= 3 && !Regex.IsMatch(dip["DiplomacyPanels.cs"], @"CameraJumper|ShowOnMap"),
            "a click on a name goes through one helper");
        C(Code("UI/InboxProviders.cs").Contains("InboxSection.Needs, 30f, DiplomacyPanels.WeekRows, DiplomacyPanels.WeekCount") && !dip["DiplomacyPanels.cs"].Contains("CC_Week_In"),
            "This week shows their calls waiting in Needs you, counted, never with a timer");
        string settings = Code("CouncilSettings.cs");
        string catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool diplomacyFavours = true;") && settings.Contains("Scribe_Values.Look(ref diplomacyFavours, \"diplomacyFavours\", true);")
          && catalog.Contains("Section(\"CC_SetSec_Diplomacy\")") && catalog.Contains("Toggle(\"diplomacyFavours\""), "the settings section Diplomacy, favours on by default");
        C(Code("Text/Facts/FactsLedger.cs").Contains("FactsMath.AddHonour(f, fact, DipWords.Why(honour.tie))"), "a debt of honour speaks as its own facts, with {FAVOURWHY}");

        // ---------------- The words ----------------

        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 100, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyedAll.Contains("<" + k + ">"), $"{k} has its words");
        foreach (var k in FavourMath.OurGoodTurns.Concat(FavourMath.TheirGoodTurns)) C(keyedAll.Contains("<CC_Dip_For_" + k + ">"), $"what a debt for {k} was for, in words");
        foreach (FavourNeed need in Enum.GetValues(typeof(FavourNeed))) C(keyedAll.Contains("<CC_Dip_Need_" + need + ">"), $"the need {need} in words");
        foreach (var g in new[] { "food", "medicine", "wood", "steel", "silver" }) C(keyedAll.Contains("<CC_Dip_Goods_" + g + ">"), $"the goods {g} in words");
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" }) C(keyedAll.Contains("<CC_Dip_Means_" + age + ">"), $"the colony's means by the {age} age");
        var mine = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Diplomacy.xml")).Root!.Elements().ToList();
        foreach (var e in mine)
            C(!e.Value.Contains("\u2014") && !e.Value.Contains("!") && !Regex.IsMatch(e.Value, @"\b(honour|favour|neighbour|colour)", RegexOptions.IgnoreCase),
                $"{e.Name.LocalName}: no em dash, no exclamation, American spelling");
        C(mine.Count(e => Regex.IsMatch(e.Name.LocalName, "_[2-6]$")) >= 15, "the letters' lines have fellows to vary them");
        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Honour.xml")).Root!.Elements().ToList();
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Diplomacy.xml")).Root!.Descendants("li").ToList();
        C(talks.Count + lines.Count >= 20 && talks.Count + lines.Count <= 60, $"the content pass: {talks.Count} talk pieces and {lines.Count} lines");
        foreach (var t in talks)
            C((t.Element("when")?.Value ?? "").Split(',').Any(w => w.StartsWith("tie:honour") || w.StartsWith("deed:")), $"{t.Element("defName")?.Value} speaks of a debt of honour or how one ended");
        C(lines.Any(l => l.Value.StartsWith("[favour,refusal")) && lines.Any(l => l.Value.StartsWith("[favour,warm")) && lines.Any(l => l.Value.StartsWith("[favouryes"))
          && lines.Any(l => l.Value.StartsWith("[favourno")) && lines.Any(l => l.Value.StartsWith("[favourask")), "their answer in every tone, face to face and by letter");
        C(lines.Any(l => l.Value.Contains("theirLowtech")) && lines.Any(l => l.Value.Contains("theirHightech")) && talks.Any(t => t.ToString().Contains("their:tribal"))
          && talks.Any(t => t.ToString().Contains("their:spacer")), "words by their age");
        C(File.Exists(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml")), "the Almanac has a page on it");
        return n;
    }
}
