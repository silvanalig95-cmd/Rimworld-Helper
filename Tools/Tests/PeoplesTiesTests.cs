using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples and their ties (the content update's B4, B12, B14, B17, B22, B29 and B31, 20 November 2026;
/// Peoples/PeoplesTiesMath.cs): the rules of each, the defs (the new head's world event, the peoples' events' callings, the
/// council's question of help sent home, the moods and their hints, the keyed lines and their tags) and the seams in the code.
/// </summary>
public static class PeoplesTiesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "the peoples' ties: " + what);
        }

        Rules(Check);
        Defs(root, Check);
        Seams(root, Check);
        return cases;
    }

    private static void Rules(Action<bool, string> Check)
    {
        // B14: ties make a people's own things come.
        Check(PeopleTiesMath.Factor(false, false) == 1f && PeopleTiesMath.Factor(true, false) == 1.5f && Math.Abs(PeopleTiesMath.Factor(false, true) - 0.67f) < 1e-4f,
            "bound: half again as often; a broken word: a third less; neither: as ever");
        Check(Math.Abs(PeopleTiesMath.Factor(true, true) - 1f) < 0.01f, "bound and broken at once about cancel out");
        Check(PeopleTiesMath.Strongest(new[] { TieWay.Debt, TieWay.Marriage, TieWay.Pact }) == TieWay.Marriage && PeopleTiesMath.Strongest(new TieWay[0]) == TieWay.None
              && PeopleTiesMath.Strongest(null) == TieWay.None, "the letter names the strongest tie: a marriage over a pact over a debt");
        Check(PeopleTiesMath.CarrierRank(TieWay.Ward) < PeopleTiesMath.CarrierRank(TieWay.Marriage) && PeopleTiesMath.CarrierRank(TieWay.Marriage) < PeopleTiesMath.CarrierRank(TieWay.Debt)
              && PeopleTiesMath.CarrierRank(TieWay.Debt) < PeopleTiesMath.CarrierRank(TieWay.Pact) && PeopleTiesMath.CarrierRank(TieWay.Pact) == PeopleTiesMath.CarrierRank(TieWay.Fealty)
              && PeopleTiesMath.CarrierRank(TieWay.Hostage) < 0 && PeopleTiesMath.CarrierRank(TieWay.None) < 0,
            "the gift goes first to the ward's guardian, then the one married in, then the colonist of a debt, then the speaker; a hostage is nobody's to carry");
        Check(PeopleTiesMath.IsBrokenWord("brokePact") && PeopleTiesMath.IsBrokenWord("renounced") && !PeopleTiesMath.IsBrokenWord("killed") && !PeopleTiesMath.IsBrokenWord(null),
            "a broken word is a pact, a truce or a contract broken, the word broken, fealty renounced; not every wrong");

        // B12: a town's fortune at the gate.
        Check(FortuneShowsMath.Shows(Fortune.Struggling) && FortuneShowsMath.Shows(Fortune.Flourishing) && !FortuneShowsMath.Shows(Fortune.Steady)
              && !FortuneShowsMath.Shows(Fortune.Poor) && !FortuneShowsMath.Shows(Fortune.Thriving), "only the two ends show at the gate");
        Check(FortuneShowsMath.WornHitPoints(100, 100, 0f) == 40 && FortuneShowsMath.WornHitPoints(100, 100, 1f) == 65 && FortuneShowsMath.WornHitPoints(30, 100, 0.5f) == 30
              && FortuneShowsMath.WornHitPoints(5, 2, 0f) >= 1 && FortuneShowsMath.WornHitPoints(1, 0, 0.5f) == 1, "worn to 40 to 65 percent, never mended by it, never below one");
        Check(FortuneShowsMath.StepUp(2) == 3 && FortuneShowsMath.StepUp(FortuneShowsMath.Masterwork) == FortuneShowsMath.Masterwork
              && FortuneShowsMath.StepUp(FortuneShowsMath.Legendary) == FortuneShowsMath.Legendary && FortuneShowsMath.StepUp(-1) == -1, "a quality step up, never to legendary by it");
        Check(FortuneShowsMath.PackBeastsKept(3) == 2 && FortuneShowsMath.PackBeastsKept(2) == 1 && FortuneShowsMath.PackBeastsKept(1) == 1 && FortuneShowsMath.PackBeastsKept(0) == 0,
            "one pack beast fewer, never the last");
        Check(FortuneShowsMath.GiftSilver(1f, 0f) == 20 && FortuneShowsMath.GiftSilver(1f, 1f) == 45 && FortuneShowsMath.GiftSilver(2f, 1f) == 90 && FortuneShowsMath.GiftSilver(float.NaN, 0f) == 20
              && FortuneShowsMath.GiftSilver(1f, 0.3f) % 5 == 0, "a small gift: 20 to 45 silver by the colony's wealth, in fives");
        Check(FortuneShowsMath.GiftChance > 0f && FortuneShowsMath.GiftChance < 0.5f && FortuneShowsMath.AskHours >= 6 && FortuneShowsMath.AskRange >= 6f, "now and then a gift; the ask stands some hours");
        Check(OutsiderAutoMath.Worth(new AutoOption { act = OutsiderAct.Meal, food = 1f, askedMeal = true }) == OutsiderAutoMath.AskedMealWorth
              && OutsiderAutoMath.Worth(new AutoOption { act = OutsiderAct.Meal, food = 1f }) < 0f
              && OutsiderAutoMath.AskedMealWorth > OutsiderAutoMath.Worth(new AutoOption { act = OutsiderAct.GoodWord, ofColony = 100 }),
            "a meal asked for is seen to first by the colonists themselves, hungry or not; unasked it waits for the colonist's own hunger");

        // B4: the new head comes to the gate.
        Check(NewHeadMath.DueTick(0, 0f) == 3 * NewHeadMath.DayTicks && NewHeadMath.DueTick(0, 1f) == 8 * NewHeadMath.DayTicks && NewHeadMath.DueTick(100, float.NaN) == 100 + 3 * NewHeadMath.DayTicks,
            "three to eight days after the seat is settled");
        Check(!NewHeadMath.Lapsed(0, 29 * NewHeadMath.DayTicks) && NewHeadMath.Lapsed(0, 31 * NewHeadMath.DayTicks) && !NewHeadMath.Lapsed(-1, 999999999), "let go after a month without a slot");
        Check(NewHeadMath.OpensPact(HeadTemper.Steady, 20, true) && !NewHeadMath.OpensPact(HeadTemper.Steady, 19, true) && !NewHeadMath.OpensPact(HeadTemper.Kind, 80, true)
              && !NewHeadMath.OpensPact(HeadTemper.Steady, 80, false), "only a steady head opens talk of a trade pact, at goodwill 20 or more, when a pact could be made");
        Check(NewHeadMath.Regard(HeadTemper.Steady, "table", 20) > NewHeadMath.Regard(HeadTemper.Steady, "table", 0) && NewHeadMath.Regard(HeadTemper.Steady, "table", 0) > NewHeadMath.Regard(HeadTemper.Steady, "word", 20),
            "the table counts by the host's Social, and for more than a word at the gate");
        Check(NewHeadMath.Regard(HeadTemper.Greedy, "gift", 0) > NewHeadMath.Regard(HeadTemper.Steady, "gift", 0), "a greedy head thinks more of a gift");
        Check(NewHeadMath.Regard(HeadTemper.Harsh, "word", 10) < 0f && NewHeadMath.Regard(HeadTemper.Harsh, "none", 10) < NewHeadMath.Regard(HeadTemper.Harsh, "word", 10)
              && NewHeadMath.Regard(HeadTemper.Harsh, "table", 0) > 0f, "a harsh head wants a token of respect: a word alone offends, nothing offends more");
        Check(NewHeadMath.Regard(HeadTemper.Kind, "none", 0) >= 0f && NewHeadMath.Regard(HeadTemper.Kind, "word", 0) > NewHeadMath.Regard(HeadTemper.Steady, "word", 0),
            "a kind head takes nothing amiss and a point more for anything");
        Check(NewHeadMath.Of("kind") == HeadTemper.Kind && NewHeadMath.Of("greedy") == HeadTemper.Greedy && NewHeadMath.Of("harsh") == HeadTemper.Harsh && NewHeadMath.Of(null) == HeadTemper.Steady
              && NewHeadMath.Flag(HeadTemper.Steady) == "steady", "the temper by the succession's flags");
        Check(NewHeadMath.RetinueMin == 2 && NewHeadMath.RetinueMax == 4 && NewHeadMath.Personal(20) > NewHeadMath.Personal(0), "two to four of their people with them; the host's own regard by their talk");

        // B17: kin hear of their town's hard year.
        Check(KinNewsMath.SendFor("sickness") == "medicine" && KinNewsMath.SendFor("hunger") == "rawfood" && KinNewsMath.SendFor("drought") == "rawfood" && KinNewsMath.SendFor("fire") == "rawfood"
              && KinNewsMath.SendFor("court") == null && KinNewsMath.SendFor(null) == null, "medicine for a sickness, food for a hard year's want, nothing where neither helps");
        Check(!KinNewsMath.ByRoad(2f) && KinNewsMath.ByRoad(KinNewsMath.RoadDays), "no courier: the road brings it in two and a half days");
        Check(KinNewsMath.Worry(TieWay.Marriage) > KinNewsMath.Worry(TieWay.Envoy) && KinNewsMath.Worry(TieWay.None) > 0f, "the closest ties worry most, every tie some");
        Check(KinNewsMath.Amount("medicine", 0) == 0 && KinNewsMath.Amount("medicine", 3) == 2 && KinNewsMath.Amount("medicine", 100) == 6 && KinNewsMath.Amount("rawfood", 30) == 10
              && KinNewsMath.Amount("rawfood", 5000) == 60 && KinNewsMath.Amount("rawfood", 260) % 10 == 0, "a fifth of the stores, 2 to 6 medicine or 10 to 60 food in tens");
        Check(KinNewsMath.AskChance > 0f && KinNewsMath.AskChance < 1f && KinNewsMath.ComeHomeChance > 0f && KinNewsMath.ComeHomeChance < 1f && KinNewsMath.Plague("sickness") && !KinNewsMath.Plague("hunger"),
            "may ask, may come home early: only from a plague town");

        // B22: the neighbours for good keep the colony's days.
        Check(HoldingDaysMath.Guests(5) == 3 && HoldingDaysMath.Guests(1) == 1 && HoldingDaysMath.Guests(-1) == 0, "up to three of them at a gathering");
        Check(HoldingDaysMath.Fresh(0, 2500 * 47) && !HoldingDaysMath.Fresh(0, 2500 * 49) && !HoldingDaysMath.Fresh(-1, 10) && !HoldingDaysMath.Fresh(100, 50), "a grave of the last two days");
        Check(HoldingDaysMath.AtEase(10f) && !HoldingDaysMath.AtEase(50f), "only while at ease with the colony");
        Check(HoldingDaysMath.Anniversary(40, 40, 60f) && !HoldingDaysMath.Anniversary(40, 40, 0f) && !HoldingDaysMath.Anniversary(40, 41, 400f) && !HoldingDaysMath.Anniversary(-1, -1, 400f),
            "the day they settled, from the first year on");
        Check(HoldingDaysMath.WeddingChance > 0f && HoldingDaysMath.WeddingChance <= 0.05f && HoldingDaysMath.FriendWarmth >= 1, "a wedding in the family is rare");

        // B29: a feast's guests bring their town's year.
        Check(FeastYearMath.Year("hunger", true) == TownYear.Hard && FeastYearMath.Year("fair", false) == TownYear.Good && FeastYearMath.Year("harvest", false) == TownYear.Good
              && FeastYearMath.Year("court", false) == TownYear.None && FeastYearMath.Year(null, false) == TownYear.None, "hard: a bad world event at home; good: their fair or a bumper harvest");
        Check(FeastYearMath.Welcome(10f, true, 0, TownYear.Hard) == 14f && FeastYearMath.Welcome(10f, true, 3, TownYear.Hard) == 11f && FeastYearMath.Welcome(10f, false, 2, TownYear.Hard) == 8f
              && FeastYearMath.Welcome(10f, true, 3, TownYear.Good) == 10f && FeastYearMath.Welcome(10f, true, 3, TownYear.None) == 10f,
            "a hard year: the meal counts double, each contest half; any other year as ever");
        Check(FeastYearMath.Leftovers(2, 20) == 4 && FeastYearMath.Leftovers(3, 4) == 2 && FeastYearMath.Leftovers(1, 1) == 0 && FeastYearMath.Leftovers(0, 50) == 0,
            "two meals a party, never past half of what is spare");
        Check(FeastYearMath.Gift(1f, 0f) == 60 && FeastYearMath.Gift(1f, 1f) == 120 && FeastYearMath.Gift(0.5f, 0f) == 30 && FeastYearMath.Gift(float.NaN, float.NaN) == 60,
            "a good year's gift: 60 to 120 silver by the colony's wealth");

        // B31: camps that come back bring the world's news.
        Check(CampNewsMath.Topic("Council_WS_Plague", "sickness", "helped") == "fever" && CampNewsMath.Topic("Council_WS_Murrain", "sickness", "lean") == "herds"
              && CampNewsMath.Topic("Council_WS_Drought", "drought", "rains") == "rains" && CampNewsMath.Topic("Council_WS_Drought", "drought", "hunger") == "dryyear"
              && CampNewsMath.Topic("Council_WS_Locusts", "hunger", "missed") == null && CampNewsMath.Topic("Council_WS_Assize", "court", "rises") == null
              && CampNewsMath.Topic("Council_WS_Succession", "court", "settled") == "head" && CampNewsMath.Topic("Rimfolk_WS_TownRaid", "townraid", "defended") == "raidheld"
              && CampNewsMath.Topic("Council_WS_Comet", "comet", "gone") == null, "the news by the event's kind and how it ended; none for what a camp wouldn't speak of");
        Check(CampNewsMath.Pick(new[] { 10, 50, 30 }, new[] { true, true, true }, 20) == 1 && CampNewsMath.Pick(new[] { 10, 50 }, new[] { true, false }, 20) == -1
              && CampNewsMath.Pick(new[] { 10, 15 }, new[] { true, true }, 20) == -1 && CampNewsMath.Pick(null, null, 0) == -1, "the newest since they left that has news");
    }

    private static void Defs(string root, Action<bool, string> Check)
    {
        string defs = Path.Combine(root, "Defs");
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";

        // B4: the world event.
        var head = XDocument.Load(Path.Combine(defs, "WorldSituationDefs", "Rimfolk_WS_NewHead.xml")).Root!.Elements().Single(e => Dn(e) == "Rimfolk_WS_NewHead");
        Check(head.Element("workerClass")?.Value == "ColonyCouncil.SituationWorker_NewHead" && head.Element("kind")?.Value == "head" && head.Element("faction") == null,
            "the new head's world event: its own worker and kind (never two court events' kind), its people from the follow-up");
        var texts = head.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        Check(texts.Count >= 4 && texts.Any(t => t.StartsWith("[succeeded]")) && texts.Any(t => t.StartsWith("[claimed]")) && texts.Any(t => t.StartsWith("[theirLowtech]")),
            "its start letters for a death and for a contested seat, and by the people's age");
        var gate = head.Element("beats")!.Elements("li").Single(b => b.Element("key")?.Value == "gate");
        Check(gate.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.VisitEffect_AtGate" && li.Element("withStranger")?.Value == "true" && li.Element("count")?.Value == "2~4"),
            "the head comes to the gate with two to four of their people");
        var gateTexts = gate.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        foreach (var t in new[] { "kind", "greedy", "harsh", "steady" })
            Check(gateTexts.Any(x => x.StartsWith("[" + t)), $"the gate's letter for a {t} head");
        var opts = gate.Element("options")!.Elements("li").ToList();
        Check(opts.Count == 3 && opts[0].Element("flags")!.Value == "table" && opts[1].Element("flags")!.Value == "gift" && opts[2].Element("flags")!.Value == "word"
              && gate.Element("defaultOption")?.Value == "2", "three answers: the table, a gift, a word at the gate (the word if none is given)");
        Check(opts[0].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Feast" && li.Element("guests")?.Value == "visitors" && li.Element("organizer")?.Value == "A")
              && opts[0].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.VisitEffect_Stay"), "the table: a meal from the stores with them at it, the best talker hosting, a bed for the night");
        Check(opts[1].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver")
              && opts[2].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Visit" && li.Element("to")?.Value == "Stranger"),
            "the gift carried out to them; the word said at the gate in person");
        Check(opts[2].Descendants("li").Any(li => li.Element("requires")?.Value == "harsh" && int.Parse(li.Element("amount")!.Value) < 0)
              && opts[0].Descendants("li").Any(li => li.Element("requires")?.Value == "harsh" && int.Parse(li.Element("amount")!.Value) > 0),
            "a harsh head is offended by a word alone and pleased by the table");
        var caravan = head.Element("beats")!.Elements("li").Single(b => b.Element("key")?.Value == "caravan");
        Check(caravan.Element("requires")!.Value == "greedy" && caravan.Descendants("incident").Single().Value == "TraderCaravanArrival", "a greedy head's caravan follows");
        var endTexts = head.Element("endings")!.Elements("li").Single().Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        Check(endTexts.Any(t => t.StartsWith("[kindGift]")) && endTexts.Any(t => t.StartsWith("[pactOffered]")) && endTexts.Any(t => t.StartsWith("[table,invited]")) && endTexts.Any(t => !t.StartsWith("[")),
            "its ending tells the kind head's gift, the pact offered, the feast promised, and has a line for anything");

        // B14: the peoples' events' callings.
        var callingNames = Regex.Matches(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Peoples", "PeoplesMath.cs")), @"(\w+) = ""(\w+)""").Select(m => m.Groups[2].Value).ToHashSet();
        int withCallings = 0, firing = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "IncidentDefs"), "Rimfolk_Events_PeopleEvents*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements("IncidentDef"))
            {
                var ext = e.Descendants("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil.HappeningExtension");
                var callings = ext?.Element("callings")?.Elements("li").Select(l => l.Value).ToList();
                float chance = float.Parse(e.Element("baseChance")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture);
                if (chance > 0f) firing++;
                if (callings == null) continue;
                withCallings++;
                Check(chance > 0f, $"{Dn(e)}: only an event that comes by itself is weighed by ties (a follow-up comes of what started it)");
                Check(callings.Count > 0 && callings.All(callingNames.Contains), $"{Dn(e)}: its callings are real callings");
            }
        Check(withCallings >= 75 && withCallings >= firing - 3, $"nearly every people's event that comes by itself names its callings ({withCallings} of {firing})");

        // B17: the council's question, the moods and their hints.
        var ask = XDocument.Load(Path.Combine(defs, "DilemmaDefs", "Rimfolk_Dilemmas_KinSendHome.xml")).Root!.Elements().Single();
        Check(ask.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_KinSendHome" && ask.Element("options")!.Elements("li").Count() == 2
              && ask.Element("options")!.Elements("li").First().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_KinSendHome"),
            "help sent home: send it with the courier, or not now");
        var askTexts = ask.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        Check(askTexts.Any(t => t.StartsWith("[tie:marriage]")) && askTexts.Any(t => t.StartsWith("[tie:came]")) && askTexts.Any(t => !t.StartsWith("[")) && askTexts.All(t => t.Contains("{COUNT} {WHAT}")),
            "the question's words for one married in, one who came from them, and anyone; each names what and how much");
        var thoughts = XDocument.Load(Path.Combine(defs, "ThoughtDefs", "Rimfolk_Thoughts_PeoplesTies.xml")).Root!.Elements("ThoughtDef").ToDictionary(Dn);
        Check(thoughts.ContainsKey("Rimfolk_KinWorried") && float.Parse(thoughts["Rimfolk_KinWorried"].Descendants("baseMoodEffect").Single().Value) < 0f
              && thoughts.ContainsKey("Rimfolk_KinSentHelp") && float.Parse(thoughts["Rimfolk_KinSentHelp"].Descendants("baseMoodEffect").Single().Value) > 0f,
            "worried about home, and glad of help sent");
        string hints = File.ReadAllText(Path.Combine(defs, "MoodHintDefs", "Rimfolk_MoodHints_PeoplesTies.xml"));
        Check(hints.Contains("<li>Rimfolk_KinWorried</li>") && hints.Contains("<li>Rimfolk_KinSentHelp</li>"), "each new mood has a mood hint");

        // The keyed lines.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesTies.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (TieWay w in Enum.GetValues(typeof(TieWay)))
            if (w != TieWay.None) Check(keyed.ContainsKey("CC_Ties_Letter_" + w), $"the letter's line for a {w}");
        Check(keyed.ContainsKey("CC_Ties_Letter_FealtyOurs") && keyed.ContainsKey("CC_Ties_Letter_EnvoyOurs"), "the fealty and the envoy either way");
        foreach (var a in new[] { "table", "gift", "word", "none" }) Check(keyed.ContainsKey("CC_NewHead_Note_" + a), $"the head's note for the answer {a}");
        var kin = keyed.Where(kv => kv.Key.StartsWith("CC_Kin_Letter_") && kv.Key != "CC_Kin_Letter_Label").Select(kv => kv.Value).ToList();
        Check(kin.Any(t => t.StartsWith("[courier,many]")) && kin.Any(t => t.StartsWith("[road,many]")) && kin.Where(t => !t.Contains("many]")).All(t => t.Contains("!many")),
            "a letter from home for several of them, by courier and by road; the others for one");
        foreach (var t in new[] { "marriage", "ward", "hostage", "envoy", "came" }) Check(kin.Any(x => x.Contains("tie:" + t)), $"a letter from home for one tied by {t}");
        var abroad = keyed.Where(kv => kv.Key.StartsWith("CC_Kin_Abroad_") && kv.Key != "CC_Kin_Abroad_Label").Select(kv => kv.Value).ToList();
        Check(abroad.Any(t => t.StartsWith("[comingHome]")) && abroad.Any(t => t.StartsWith("[staying]")), "the colony's own there write, staying or coming home");
        var news = keyed.Where(kv => kv.Key.StartsWith("CC_Camp_WorldNews_")).Select(kv => kv.Value).ToList();
        foreach (var topic in new[] { "fever", "hunger", "rains", "dryyear", "fire", "flood", "winter", "herds", "harvest", "fair", "head", "war", "raid", "raidheld", "rebuilt", "faith", "mourning" })
            Check(news.Any(t => t.StartsWith("[news:" + topic + "]") || t.StartsWith("[news:" + topic + ",")), $"a camp's news of home: {topic}");
        foreach (var kv in keyed)
        {
            string text = Regex.Replace(kv.Value, @"^\[[^\]]*\]\s*", "");
            Check(!text.Contains("—") && !text.Contains('!'), $"{kv.Key}: no em dash, no exclamation mark");
        }
        foreach (var k in new[] { "CC_Fortune_AskLine", "CC_Fortune_Thanks", "CC_HoldingDay_CameLine", "CC_HoldingDay_GraveLine1", "CC_HoldingDay_GraveLine2", "CC_HoldingDay_GraveLine3",
                     "CC_FeastYear_ToastHard", "CC_FeastYear_ToastGood" })
            Check(keyed.ContainsKey(k) && Regex.Matches(keyed[k], @"[.?](\s|$)").Count == 1, $"{k}: one sentence, as speech is");
    }

    private static void Seams(string root, Action<bool, string> Check)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        Check(Read("Happenings", "Happenings.cs").Contains("PeopleTies.EventFactor(d)") && Read("Happenings", "IncidentWorker_Happening.cs").Contains("PeopleTies.EventFactor(def)"),
            "B14: ties weigh the peoples' events on Rimfolk's own rhythm and the storyteller's");
        Check(Read("Happenings", "IncidentWorker_Happening.cs").Contains("PeopleTies.LetterLine(ctx)") && Read("Happenings", "IncidentWorker_Happening.cs").Contains("PeopleTies.Giving(carrier)")
              && Read("Peoples", "Events", "PeopleEvents.cs").Contains("PeopleTies.GiftTo"), "B14: the letter names the tie; the gift goes first to its carrier, else as ever");
        Check(Read("Camps", "CampTracker.cs").Contains("PeopleTies.CampKindFactor(k)") && Read("Camps", "CampTracker.cs").Contains("PeopleTies.CampPeopleFactor(x)"), "B14: and their camps");
        Check(Read("World", "Region", "FortuneShows.cs").Contains("[HarmonyPatch(typeof(IncidentWorker_NeutralGroup), \"SpawnPawns\")]") && Read("Camps", "CampTracker.cs").Contains("FortuneShows.CampCame(c)")
              && Read("Visitors", "Visitors.cs").Contains("FortuneShows.Dress(list, fortune)") && Read("Outsiders", "OutsiderAuto.cs").Contains("o.askedMeal = FortuneShows.Asked(x)")
              && Read("Outsiders", "OutsiderOutcomes.cs").Contains("FortuneShows.Fed(x, c)"), "B12: the game's groups, Rimfolk's visits and camps dressed; the meal asked for seen to and thanked");
        Check(Read("World", "Workers_Situations.cs").Contains("NewHeads.Remember(s, s.ctx.stranger, temper)") && Read("World", "Workers_Situations.cs").Contains("NewHeads.Remember(s, winner, NewHeads.TemperOf(winner))")
              && Read("World", "Region", "FollowUps.cs").Contains("NewHeads.Start(home, f)") && Read("Diplomacy", "FeastDialog.cs").Contains("NewHeads.Promised(n)")
              && Read("Diplomacy", "Feasts.cs").Contains("NewHeads.Invited("), "B4: both successions remember the head; the follow-up starts the ride; the feast keeps the promise");
        Check(Read("Couriers", "CourierTracker.cs").Contains("KinNews.CourierCame(t)") && Read("Heart", "FeelingWorkers2.cs").Contains("KinNews.Worry(p, sink)"),
            "B17: the word comes with the courier (beside its spoken line, which the messengers' road news may change), and the worry is on the heart's card");
        Check(Read("Calendar", "CalendarKeeping.cs").Contains("HoldingDays.DayKept(d, map)") && Read("Dilemmas", "Dilemmas.cs").Contains("HoldingDays.CustomKept(ctx, index)")
              && Read("Camps", "Holding.cs").Contains("ExposeHoldingDays()") && Read("Camps", "CampRequests.cs").Contains("case \"spot\":"), "B22: the days and customs kept, the holding's own saved, the grave's errand");
        Check(Read("Diplomacy", "Feasts.cs").Contains("FeastYear.Arrived(p, map)") && Read("Diplomacy", "Feasts.cs").Contains("toastLine = FeastYear.Toast(p)")
              && Read("Diplomacy", "Feasts.cs").Contains("FeastYear.Welcome(g, g.welcome, contests)") && Read("Diplomacy", "Feasts.cs").Contains("FeastYear.Leftovers(p, map)"),
            "B29: the gift at the gate, the toast, the welcome, the leftovers");
        Check(Read("Camps", "CampMemory.cs").Contains("WorldNews(c, past, tags)"), "B31: the camp back says what it lived through at home");
        Check(Read("World", "Region", "RegionBook.cs").Contains("PeoplesTies.Hourly(now)") && Read("World", "Region", "RegionBook.cs").Contains("PeoplesTies.Daily(now)"), "the hour and the day with the region's own");
        Check(!Regex.IsMatch(Read("World", "KinNews.cs"), @"\b(inherit\w*|heirs?|Heir\w*|FamilyLife\w*|Bloodline\w*)\b"), "B17 is flavour and mood: no inheritance, no duty to a family line");
    }
}
