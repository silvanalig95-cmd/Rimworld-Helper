using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Handing a colony over (Colonies/HandOverMath.cs; docs/design/stage3-many-colonies.md, section 2, release 2a): the
/// checks and what the town waits for, the head and the head's second by office or standing (never by blood), the next
/// second, the meeting's vote and what going against it costs, the people's name, its def by age, its relations with
/// every other people, the town's fortune seeded from the colony's wealth, the bond in goodwill by its state, the day
/// set, the bound on the towns the colony was; the four peoples' defs, the words and the lines said on the map; and the
/// code's seams with the game and with release 1 (the same act as a fresh start).
/// </summary>
public static class HandOverTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "hand over: " + what);
        }

        // ---------------- The checks ----------------

        C(HandOverMath.WhyNot(true, false, false, 3, true, false) == null, "three grown-ups staying, someone elsewhere, no fight: it can go ahead");
        C(HandOverMath.WhyNot(false, true, true, 0, false, true) == "NotSettlement", "only a settlement becomes a town, before anything else");
        C(HandOverMath.WhyNot(true, true, true, 3, true, false) == "Fight", "never during a fight or a threat");
        C(HandOverMath.WhyNot(true, false, true, 3, true, false) == "Banishment", "never while a banishment plays out there");
        C(HandOverMath.WhyNot(true, false, false, 2, true, false) == "FewStay" && HandOverMath.MinGrownStay == 3, "fewer than three grown-ups is a homestead, not a town");
        C(HandOverMath.WhyNot(true, false, false, 3, false, false) == "NobodyElse", "nobody of the player's elsewhere: the game would end");
        C(HandOverMath.WhyNot(true, false, false, 3, true, true) == "Anchor", "a grav anchor would keep the map a home");
        C(HandOverMath.PlayersElsewhere(1, 0, false) && HandOverMath.PlayersElsewhere(0, 2, false) && HandOverMath.PlayersElsewhere(0, 0, true) && !HandOverMath.PlayersElsewhere(0, 0, false),
            "someone stays the player's: one who goes, a colonist elsewhere, or newcomers in the same act");

        C(HandOverMath.WaitsFor(0, 0, false, 3, false) == null, "nobody of those who go on the map: the town is made");
        C(HandOverMath.WaitsFor(2, 1, true, 1, true) == "Going", "those who go still there come first");
        C(HandOverMath.WaitsFor(0, 1, true, 1, true) == "Lodgers", "a guest of the colony goes with those who go");
        C(HandOverMath.WaitsFor(0, 0, true, 3, false) == "Founding", "nobody goes from here: the new colony of the same act stands first");
        C(HandOverMath.WaitsFor(0, 0, false, 2, false) == "FewStay", "fewer than three grown-ups left on the map: it waits");
        C(HandOverMath.WaitsFor(0, 0, false, 3, true) == "Engine", "the gravship's engine still there: it waits");
        C(HandOverMath.WaitsFor(0, 0, false, 3, false, false) == "NobodyElse", "those who went all lost: it waits rather than end the game");

        // ---------------- The head and the head's second ----------------

        var people = new List<HeadCandidate>
        {
            new HeadCandidate(false, -1, 40f),
            new HeadCandidate(false, 3, 10f),
            new HeadCandidate(true, 1, -20f),
            new HeadCandidate(false, 2, 5f)
        };
        C(HandOverMath.Head(people) == 2, "the speaker heads the town, however the others think of them");
        C(HandOverMath.Head(people, 0) == 0, "the player may pick another");
        C(HandOverMath.Second(people, 2) == 3, "the second: the next office holder by the seats' order");
        C(HandOverMath.Second(people, 2, 1) == 1 && HandOverMath.Second(people, 2, 2) == 3, "the player may pick the second, never the head");
        var noSpeaker = people.Select(p => new HeadCandidate(false, p.office, p.standing)).ToList();
        C(HandOverMath.Head(noSpeaker) == 2, "without the speaker, the office holder first by order");
        var noOffice = people.Select(p => new HeadCandidate(false, -1, p.standing)).ToList();
        C(HandOverMath.Head(noOffice) == 0 && HandOverMath.Second(noOffice, 0) == 1, "without offices, the one the others think best of, then the next");
        C(HandOverMath.Head(new List<HeadCandidate>()) == -1 && HandOverMath.Second(new List<HeadCandidate> { new HeadCandidate(true, 0, 0f) }, 0) == -1,
            "nobody to head it, or nobody but the head to stand second");
        var town = new List<HeadCandidate>
        {
            new HeadCandidate(false, -1, 90f, false),
            new HeadCandidate(false, -1, 10f, true),
            new HeadCandidate(false, -1, 30f, true)
        };
        C(HandOverMath.NextSecond(town, -1) == 2, "a town's next second: the colony's former people first, the best thought of among them");
        C(HandOverMath.NextSecond(town, 2) == 1, "never the head");
        C(HandOverMath.NextSecond(town.Select(p => new HeadCandidate(false, -1, p.standing, false)).ToList(), -1) == 0, "no former people left: the best of the town's own");
        C(HandOverMath.Best(new List<float> { 3f, 9f, 5f }, new HashSet<int> { 1 }) == 2 && HandOverMath.Best(new List<float>(), null) == -1,
            "the envoy and the trader: the best of the rest, never one taken");

        // ---------------- The meeting's vote ----------------

        C(HandOverMath.VoteOf(FoundingWish.Stay, false, 0f) == HandOverVote.For && HandOverMath.VoteOf(FoundingWish.Go, true, 0f) == HandOverVote.For,
            "the plan gives them their wish: for it");
        C(HandOverMath.VoteOf(FoundingWish.Go, false, 0f) == HandOverVote.Against && HandOverMath.VoteOf(FoundingWish.Stay, true, 0f) == HandOverVote.Against,
            "the plan goes against their wish: against it");
        C(HandOverMath.VoteOf(FoundingWish.Torn, true, 0f) == HandOverVote.Torn, "torn, with nothing for the council either way: not counted");
        C(HandOverMath.VoteOf(FoundingWish.Torn, true, 30f) == HandOverVote.For && HandOverMath.VoteOf(FoundingWish.Torn, false, -30f) == HandOverVote.Against,
            "the council's regard decides the torn");
        C(HandOverMath.VoteOf(FoundingWish.Go, false, 80f) == HandOverVote.For, "a devoted member votes for it against their own wish");
        C(HandOverMath.VoteOf(FoundingWish.Stay, false, -80f) == HandOverVote.Against, "a bitter one votes against it though it gives them their wish");
        C(Math.Abs(HandOverMath.VotePull(FoundingWish.Torn, true, 500f) - HandOverMath.VoteTrustMost) < 0.001f, "the council's regard weighs 1.5 reasons at most");
        C(HandOverMath.Carried(3, 2) && !HandOverMath.Carried(2, 2) && !HandOverMath.Carried(0, 0), "carried with more for than against; a tie or nobody is not a yes");
        C(HandOverMath.TrustCost(false, true) == HandOverMath.TrustAgainstVote && HandOverMath.TrustAgainstVote < 0f, "going ahead against the vote costs trust");
        C(HandOverMath.TrustCost(true, true) == 0f && HandOverMath.TrustCost(false, false) == 0f, "with the vote, or calling it off: nothing");

        // ---------------- The people ----------------

        C(HandOverMath.NameWhyNot("  ", null) == "Empty" && HandOverMath.NameWhyNot(new string('a', HandOverMath.MaxNameLength + 1), null) == "Long", "a name, and not too long");
        C(HandOverMath.NameWhyNot("Folk of Oakhold", new[] { "folk of oakhold " }) == "Taken", "never another people's name");
        C(HandOverMath.NameWhyNot("Folk of Oakhold", new[] { "Wallovian Tribe" }) == null, "a name of its own will do");
        C(HandOverMath.DefName(WorldAge.Tribal) == "Rimfolk_People_Tribal" && HandOverMath.DefName(WorldAge.Spacer) == "Rimfolk_People_Spacer", "Rimfolk's own people by the colony's age (6B)");
        C(HandOverMath.NameKey(WorldAge.Medieval) == "CC_HandOver_PeopleName_Medieval", "the people's name by the age's word");
        C(HandOverMath.AllyGoodwill == 80, "allied with the colony at 80");
        C(HandOverMath.OtherGoodwill(-100, 60, false, out bool h1) == -100 && h1, "a permanent enemy stays one, whatever the colony thinks of them");
        C(HandOverMath.OtherGoodwill(-80, 60, false, out bool h2) == -80 && h2, "a natural enemy stays one, by its def's own rule");
        C(HandOverMath.OtherGoodwill(0, 60, false, out bool h3) == 30 && !h3, "a friend of the colony's: half as warm");
        C(HandOverMath.OtherGoodwill(0, -90, true, out bool h4) == -45 && h4, "the colony's enemy: half as cold, and at war");
        C(HandOverMath.OtherGoodwill(0, -30, false, out bool h5) == -15 && !h5, "cool, but not at war while the colony isn't");
        C(HandOverMath.OtherGoodwill(0, 5, false, out _) == 3 && HandOverMath.OtherGoodwill(0, -5, false, out _) == -3, "halves rounded away from nothing");

        // ---------------- The town ----------------

        C(HandOverMath.Seed(0f, 5) == HandOverMath.SeedLow && HandOverMath.Seed(10000000f, 5) == HandOverMath.SeedHigh, "the fortune seeded between 30 and 70");
        C(HandOverMath.SeedLow == 30f && HandOverMath.SeedHigh == 70f, "30 to 70, as the design says");
        float mid = HandOverMath.Seed((HandOverMath.WealthLow + HandOverMath.WealthHigh) / 2f * 10f, 10);
        C(Math.Abs(mid - 50f) < 0.2f, "halfway in wealth per head, halfway in fortune: " + mid);
        C(HandOverMath.Seed(100000f, 0) == HandOverMath.Seed(100000f, 1), "nobody counted is one");

        C(HandOverMath.Bond(false, true) == 30 && HandOverMath.Bond(false, false) == 15 && HandOverMath.Bond(true, true) == 0,
            "the bond: +30 while a former colonist heads them, +15 after, gone after a war (8A)");
        C(HandOverMath.BondEnds(true, false) && HandOverMath.BondEnds(false, true) && !HandOverMath.BondEnds(false, false), "a war or a taking by force ends it");

        const int Day = HandOverMath.DayTicks;
        C(HandOverMath.DaySet(10 * Day + 14 * 2500, 14) == 11 * Day && HandOverMath.DaySet(10 * Day, 0) == 11 * Day, "the day set is the next day");
        C(HandOverMath.CeremonyFrom < HandOverMath.CeremonyTo && HandOverMath.MeetingFrom >= 17, "the meeting in the evening, the ceremony in the day's hours");
        C(HandOverMath.FarewellsEach == 2 && HandOverMath.MaxFarewells >= HandOverMath.FarewellsEach, "two farewells each, a dozen in all");

        var standing = Enumerable.Repeat(true, HandOverMath.MaxTowns).ToList();
        var handed = Enumerable.Range(0, HandOverMath.MaxTowns).Select(i => i * 100).ToList();
        C(HandOverMath.RecordToDrop(standing, handed) == -1 && HandOverMath.MaxTowns == 6, "six towns the colony was fit");
        standing.Add(true);
        handed.Add(900);
        C(HandOverMath.RecordToDrop(standing, handed) == -1, "past six with every town standing: none dropped");
        standing[4] = false;
        standing[2] = false;
        C(HandOverMath.RecordToDrop(standing, handed) == 2, "past six: the oldest whose town is gone");

        C(FoundingMath.Free(true), "newcomers come free with the old colony handed over in the same act (it is not kept)");

        // ---------------- The defs, the words and the lines ----------------

        string root = Root();
        C(root != null, "found the mod's folders");
        if (root == null) return n;
        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_HandOver.xml")).Root!;
        var factions = defs.Elements("FactionDef").ToList();
        C(factions.Count == 4, "four peoples, one for each age");
        var ages = new Dictionary<string, string> { ["Tribal"] = "Neolithic", ["Medieval"] = "Medieval", ["Industrial"] = "Industrial", ["Spacer"] = "Spacer" };
        foreach (var kv in ages)
        {
            var f = factions.FirstOrDefault(x => x.Element("defName")?.Value == "Rimfolk_People_" + kv.Key);
            C(f != null, kv.Key + ": its people's def");
            if (f == null) continue;
            string parent = (string)f.Attribute("ParentName");
            C(parent == (kv.Key == "Tribal" || kv.Key == "Medieval" ? "TribeBase" : "OutlanderFactionBase"), kv.Key + ": on the game's own parent, for its groups, caravans and defenders");
            C(f.Element("techLevel")?.Value == kv.Value, kv.Key + ": of the age's tech level");
            C(f.Element("fixedName") == null && f.Element("royalTitleTags") == null && f.Element("permanentEnemy")?.Value == "false" && f.Element("naturalEnemy")?.Value == "false"
              && f.Element("hidden")?.Value == "false", kv.Key + ": no fixed name, no titles, never an enemy, not hidden");
            C(f.Element("requiredCountAtGameStart")?.Value == "0" && f.Element("settlementGenerationWeight")?.Value == "0" && f.Element("startingCountAtWorldCreation")?.Value == "0"
              && f.Element("displayInFactionSelection")?.Value == "false", kv.Key + ": never made with the world");
            C(!string.IsNullOrEmpty(f.Element("leaderTitle")?.Value), kv.Key + ": its head's title by the age");
        }
        var bond = defs.Elements("GoodwillSituationDef").FirstOrDefault();
        C(bond?.Element("defName")?.Value == "Rimfolk_OnceOf" && bond.Element("workerClass")?.Value == "ColonyCouncil.GoodwillSituationWorker_OnceOf", "the bond in goodwill is a goodwill situation");

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
        {
            C(keyed.ContainsKey("CC_Form_HandOver_" + age) && keyed.ContainsKey("CC_Form_HandOver_" + age + "_Rite"), age + ": the hand-over's form");
            C(keyed.ContainsKey("CC_HandOver_Token_" + age) && keyed.ContainsKey("CC_HandOver_Second_" + age) && keyed.ContainsKey("CC_HandOver_PeopleName_" + age)
              && keyed["CC_HandOver_PeopleName_" + age].Contains("{PLACE}"), age + ": what changes hands, the second's title and the people's name");
        }
        foreach (var k in new[] { "NotSettlement", "Fight", "Banishment", "FewStay", "NobodyElse", "Anchor" }) C(keyed.ContainsKey("CC_HandOver_No_" + k), "the check's words: " + k);
        foreach (var k in new[] { "Going", "Lodgers", "Founding", "FewStay", "Engine", "NobodyElse" }) C(keyed.ContainsKey("CC_HandOver_Wait_" + k), "what it waits for, in words: " + k);
        foreach (var k in new[] { "Free", "Defence", "Trade" })
            C(keyed.ContainsKey("CC_HandOver_Terms_" + k) && keyed.ContainsKey("CC_HandOver_TermsLine_" + k) && keyed.ContainsKey("CC_HandOver_Terms_Made_" + k), "the terms: " + k);
        var mine = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HandOver.xml")).Root!.Elements().ToList();
        C(mine.All(e => !e.Value.Contains("—") && !e.Value.Contains("!")), "the hand-over's words: no em dashes and no exclamation marks");
        C(keyed.TryGetValue("CC_HandOver_NeedsRimfolk", out var needs) && needs.Contains("needs Rimfolk") && needs.Contains("drops the town"), "the window says plainly a save then needs Rimfolk (6B)");
        C(mine.All(e => !System.Text.RegularExpressions.Regex.IsMatch(e.Value, @"\bin \d+ (days?|hours?)\b")), "no countdowns: days are named by their date");

        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_HandOver.xml")).Root!.Elements().ToList();
        foreach (var name in new[] { "Rimfolk_Pool_HandOverMeeting", "Rimfolk_Pool_HandOver", "Rimfolk_Pool_HandOverGive", "Rimfolk_Pool_HandOverTake", "Rimfolk_Pool_HandOverFarewell" })
        {
            var pool = pools.FirstOrDefault(p => p.Element("defName")?.Value == name);
            var lines = pool?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
            C(lines.Count >= 6, name + ": its lines");
            foreach (var line in lines)
            {
                string body = line.StartsWith("[") ? line.Substring(line.IndexOf(']') + 1).Trim() : line.Trim();
                C(OneSentence(body) && body.Length <= 90 && !body.Contains("!"), name + ": '" + body + "' is one short sentence");
            }
            if (name != "Rimfolk_Pool_HandOverFarewell")
                foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
                    C(lines.Any(l => l.StartsWith("[" + age + "]")), name + ": a line of the " + age + " age's own");
        }
        var farewell = pools.First(p => p.Element("defName")?.Value == "Rimfolk_Pool_HandOverFarewell").Descendants("li").Select(li => li.Value).ToList();
        C(farewell.All(l => l.StartsWith("[going]") || l.StartsWith("[staying]")) && farewell.Any(l => l.StartsWith("[going]")) && farewell.Any(l => l.StartsWith("[staying]")),
            "farewells: one going, one staying, each their own lines");

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_HandOver.xml")).Root!.Elements().First();
        C(almanac.Element("label")?.Value == "a town of our own" && !almanac.ToString().Contains("—"), "the Almanac page, a page of its own");

        // ---------------- The seams ----------------

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string ho = Code("Colonies/HandOvers.cs");
        string peoples = Code("Colonies/HandOverPeoples.cs");
        string patches = Code("Colonies/HandOverPatches.cs");
        C(peoples.Contains("FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true))") && peoples.Contains("f.hidden = false")
          && peoples.Contains("Find.FactionManager.Add(f)") && peoples.Contains("SetPrimary(faith)"), "the people made the game's way: hidden at creation, then shown, its faith the colony's, then added");
        C(peoples.Contains("HandOverMath.AllyGoodwill") && peoples.Contains("HandOverMath.OtherGoodwill("), "its relations by the rule");
        C(ho.Contains("s.SetFaction(people)") && ho.Contains("\"cachedMat\"") && ho.Contains("wasSpawnedViaGravShipLanding = false"),
            "the settlement theirs, its look cleared, a gravship-landed home closed Rimfolk's way");
        C(ho.Contains("people.leader = head") && ho.Contains("ColonyEnd.HandedOver") && ho.Contains("TownKind.Former") && ho.Contains("KeepFor.Former") && ho.Contains("FoundingMath.KindHandedOver"),
            "the head leads; the records: the colony handed over, a town the colony was, the rest kept, the milestone");
        C(ho.Contains("Find.GameEnder.CheckOrUpdateGameOver()") && ho.Contains("HandOverMath.WaitsFor("), "the town made only once those who go have left, and the game's end checked after");
        C(ho.Contains("Pacts.Make(") && !ho.Contains("Fealty"), "the terms: a pact or nothing, never fealty (9A)");
        C(ho.Contains("Speech.Say(") && ho.Contains("Together.When(") && ho.Contains("PlayKind.Toast"), "the ceremony on the map: the head's words, the token and the farewells face to face");
        C(ho.Contains("TownLayouts.Capture(home, free)") && ho.Contains("layout = layout"), "release 2b's seam filled: the colony's layout kept as the town is made (Colonies/TownLayout.cs)");
        C(patches.Contains("typeof(Faction), nameof(Faction.TryGenerateNewLeader)") && patches.Contains("HandOverPeoples.IsOurs(__instance)"),
            "the second leads through the game's own succession, for these peoples only");
        C(patches.Contains("typeof(Settlement), nameof(Settlement.GetGizmos)") && patches.Contains("nameof(GravshipUtility.AbandonMap)"), "the world button beside the game's Abandon, and the gravship's takeoff");
        C(Code("World/Region/RegionBook.cs").Contains("HandOvers.Hourly(now)") && Code("World/Region/RegionBook.cs").Contains("HandOvers.Daily(now)"),
            "no schedule slot of its own: the region's hour and day");
        C(Code("PlayOut/OnMapPlays.cs").Contains("HandOvers.PlayOver("), "the meeting and the ceremony end through the play-outs' own afterward");
        string window = Code("Colonies/FoundingWindow.cs");
        C(window.Contains("CC_HandOver_FoundChoice") && window.Contains("HandOvers.Open(from, plan)"), "the founding window's third choice: handed over (HandOver)");
        C(Code("Colonies/ColonyCards.cs").Contains("HandOvers.Open(CardSections.HomeOf(r))"), "the act on the colony's card");
        string notables = Code("World/Notables.cs");
        C(notables.Contains("HandOvers.IsFormerTown(") && notables.Contains("HandOvers.NotableFor(") && notables.Contains("HandOvers.SecondTitle("),
            "the town's notables: kept wherever it lies, filled from its former people, the second titled by age");
        C(Code("World/Ledger/LedgerKeep.cs").Contains("case KeepFor.Former: return HandOvers.Holds(p);"), "kept with KeepFor.Former while the town stands");
        string saves = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(saves.Contains("Colonies/HomesHandOver.cs:handOvers") && saves.Contains("World/Ledger/LedgerFormers.cs:formerRecords"), "every new saved list has its model");
        return n;
    }

    private static bool OneSentence(string s)
    {
        for (int i = 12; i < s.Length - 1; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return false;
        return true;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
