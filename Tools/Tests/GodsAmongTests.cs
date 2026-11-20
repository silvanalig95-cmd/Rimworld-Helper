using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods among the colony's people and the world (Source/ColonyCouncil/Gods/Among/; the content update's integrations,
/// part 2: C6, C7, C8, C12, C14, C15, C18, C26, C29): the pure rules (GodsAmongMath), and the defs, words and seams they
/// stand on (the pools said over heads, the keys the code names, the shrine rooms, the season's question, the hooks into
/// the vow, the season away, the ledger, the book, the funeral, the weather book, the calendar and the room menu).
/// </summary>
public static class GodsAmongTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods among the people: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
        string among = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods", "Among"), "*.cs").Select(File.ReadAllText));
        string defs = Read("Defs/GodDefs/Rimfolk_GodsAmong.xml");
        string rooms = Read("Defs/RoomTemplateDefs/Rimfolk_RoomTemplates_GodRooms.xml");
        string keyed = Read("Languages/English/Keyed/Rimfolk_GodsAmong.xml");
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        string godsXml = Read("Defs/GodDefs/Rimfolk_Gods.xml");
        var godKeys = new HashSet<string>(Regex.Matches(godsXml, @"^    <key>(\w+)</key>", RegexOptions.Multiline).Select(m => m.Groups[1].Value));

        // ================= C6. A vowed pilgrimage =================

        C(!GodsAmongMath.InReach(0.5f) && GodsAmongMath.InReach(1f) && GodsAmongMath.InReach(6f) && !GodsAmongMath.InReach(6.01f) && !GodsAmongMath.InReach(float.NaN),
            "a holy place from one to six days' road each way; nearer is the colony's own land");
        C(GodsAmongMath.DaysThere(0f) == 1f && GodsAmongMath.DaysThere(1f) == 3f && GodsAmongMath.DaysThere(float.NaN) == 1f && GodsAmongMath.DaysThere(7f) == 3f,
            "one to three days at the place");
        C(GodsAmongMath.AwayDays(2f, 1.5f) == 6 && GodsAmongMath.AwayDays(1f, 1f) == 3 && GodsAmongMath.AwayDays(float.NaN, 2f) == 2, "the days away the words say, both roads and the days there");
        var places = new List<(GodsAmongMath.HolyKind, float)>
        {
            (GodsAmongMath.HolyKind.Old, 2f), (GodsAmongMath.HolyKind.Town, 2.5f), (GodsAmongMath.HolyKind.Fellowship, 2f), (GodsAmongMath.HolyKind.Town, 9f)
        };
        C(GodsAmongMath.PickHoly(places) == 1, "a town of a people that holds the god first, though a little farther");
        C(GodsAmongMath.PickHoly(new List<(GodsAmongMath.HolyKind, float)> { (GodsAmongMath.HolyKind.Old, 1.5f), (GodsAmongMath.HolyKind.Town, 4f) }) == 0,
            "an old shrine near at hand before a far town");
        C(GodsAmongMath.PickHoly(new List<(GodsAmongMath.HolyKind, float)> { (GodsAmongMath.HolyKind.Town, 0.5f), (GodsAmongMath.HolyKind.Town, 7f) }) == -1
          && GodsAmongMath.PickHoly(null) == -1, "none in reach: the walk on the colony's own land");
        C(Read("Source/ColonyCouncil/Gods/Worship/Vows.cs").Contains("VowPilgrimage.TryFar(p, g, map)") && Read("Source/ColonyCouncil/Gods/Worship/Vows.cs").Contains("SeasonAway.AwayNow(p)"),
            "the vow's hour tries the holy place in the region first, and never interrupts one setting out");
        C(Read("Source/ColonyCouncil/Peoples/Events2/SeasonAway.cs").Contains("[VowPilgrimage.AwayKey] = new VowPilgrimAway()"), "the pilgrimage is one of the seasons away");
        C(Read("Source/ColonyCouncil/Gods/GodTales.cs").Contains("string asName = null"), "a tale told by another people's name for the god");
        C(GodsAmongMath.TaleFrom < GodsAmongMath.TaleTo && GodsAmongMath.TaleTo <= 24, "the tale at supper");
        C(defs.Contains("<defName>Rimfolk_PilgrimToken</defName>") && among.Contains("VIE_RelicInertStatuette") && among.Contains("Pilgrimage.ShellDef"),
            "something of the place: a small relic of the mod's, the fellowship's shell, else a pilgrim's token");

        // ================= C7. Murmur and the fishers =================

        C(GodsAmongMath.Morning(4) && GodsAmongMath.Morning(11) && !GodsAmongMath.Morning(12) && !GodsAmongMath.Morning(3), "the first fish of the morning, from first light to late morning");
        C(GodsAmongMath.AfterGiving(5) == 4 && GodsAmongMath.AfterGiving(1) == 0 && GodsAmongMath.AfterGiving(0) == 0, "one fish of the catch goes back");
        C(defs.Contains("<FishingYield MayRequire=\"Ludeon.RimWorld.Odyssey\">" + GodsAmongMath.YieldOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</FishingYield>")
          && defs.Contains("<VCEF_FishingLuckOffset MayRequire=\"vanillaexpanded.vcef\">" + GodsAmongMath.LuckOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</VCEF_FishingLuckOffset>"),
            "the day's better fishing: the mods' own stats, each soft");
        C(PawnGodsMath.TraitDraws.Any(d => d.trait == "VCEF_Fisherman" && d.god == "Murmur"), "Vanilla Fishing Expanded's fisherman draws toward Murmur");
        C(among.Contains("[HarmonyPatch(typeof(FishingUtility), nameof(FishingUtility.GetCatchesFor))]") && Read("Source/ColonyCouncil/Gods/OnMap/OwnMurmur.cs").Contains("MurmurFishers.MudFish()"),
            "the catch hooked, and fish on the mud where the water gives back");
        C(GodsAmongMath.MudFishMin >= 1 && GodsAmongMath.MudFishMin <= GodsAmongMath.MudFishMax, "a few fish on the mud");

        // ================= C8. Ducat and what the colony owes =================

        C(GodsAmongMath.Turn(true, true, false) == GodsAmongMath.DebtTurn.Paid && GodsAmongMath.Turn(true, false, true) == GodsAmongMath.DebtTurn.Lapsed
          && GodsAmongMath.Turn(true, false, false) == GodsAmongMath.DebtTurn.None && GodsAmongMath.Turn(false, true, false) == GodsAmongMath.DebtTurn.None
          && GodsAmongMath.Turn(false, false, true) == GodsAmongMath.DebtTurn.None, "only what the colony owes: paid when kept, lapsed when broken, nothing when it ended or is owed to it");
        C(GodsAmongMath.Lengthened(1000, GodsAmongMath.LongerTicks) == 1000 - 30000 && GodsAmongMath.Lengthened(5, -10) == 5, "the trader's stay half a day longer");
        C(GodsAmongMath.HardBargain < 0f && GodsAmongMath.HardBargain > -0.1f && GodsAmongMath.HardTicks == 60000, "a hard bargain, a little worse, for the day");
        C(GodsAmongMath.CoinFrom < GodsAmongMath.CoinTo, "the coin in the morning");
        C(Read("Source/ColonyCouncil/World/Ledger/WorldLedger.cs").Contains("DucatDebts.Moved(tie)") && Read("Source/ColonyCouncil/Trade/Window_RimfolkTrade.cs").Contains("DucatDebts.WindowNote(trader)"),
            "the ledger's moves and the trade window's plain line");
        C(!Regex.IsMatch(keyed, @"<CC_GA_(DebtCoin|TraderStays|HardBargain)[A-Za-z]*>[^<]*(Ducat|\{GOD\})"), "Ducat never named in the coin, the stay or the bargain");

        // ================= C12. The keeper at work =================

        C(GodsAmongMath.GoesToLook(false, true, 30f) && !GodsAmongMath.GoesToLook(true, true, 30f) && !GodsAmongMath.GoesToLook(false, false, 30f)
          && !GodsAmongMath.GoesToLook(false, true, GodsAmongMath.LookReach + 1f) && !GodsAmongMath.GoesToLook(false, true, float.NaN),
            "the keeper walks out only to a place on the land they didn't see, within reach");
        C(GodsAmongMath.Writes(19, 1) && !GodsAmongMath.Writes(19, 0) && !GodsAmongMath.Writes(12, 3) && !GodsAmongMath.Writes(22, 3), "the evening's writing, with new entries");
        C(GodsAmongMath.WriteTicks(0) == 1250 && GodsAmongMath.WriteTicks(2) == 2050 && GodsAmongMath.WriteTicks(100) == 3750, "a little longer for each entry, an hour and a half at most");
        C(Read("Source/ColonyCouncil/Gods/Gods.cs").Split(new[] { "KeeperAtWork.Told(" }, StringSplitOptions.None).Length == 3, "the book's seen entries and the gods' moments both call the keeper");
        foreach (var v in new[] { "Bare", "Plain", "Devout", "Learned" })
        {
            C(keyed.Contains("<CC_GA_KeeperLooked_" + v + ">"), "the keeper's words after the entry, " + v);
            C(keyed.Contains("<CC_GodLore_Cried_" + v + ">"), "a raider's cry in the book, " + v);
        }
        C(among.Contains("VBE_WritersTable") && among.Contains("VBE_TypewritersTable") && defs.Contains("<driverClass>ColonyCouncil.JobDriver_KeeperWrites</driverClass>"),
            "Vanilla Books Expanded's writing tables first, the keeper's own job");

        // ================= C14. The raiders' gods =================

        C(GodsAmongMath.CryRange > 20f && GodsAmongMath.CryHours > 0 && GodsAmongMath.CryChance > 0f && GodsAmongMath.CryChance < 1f, "a cry in sight, not every band");
        C(GodsAmongMath.PrisonerTaleChance > 0f && GodsAmongMath.PrisonerTaleChance < 1f && GodsAmongMath.PrisonerGapDays >= 1f, "a prisoner's tale now and then");
        C(keyed.Contains("<CC_GodLore_How_Wall>") && keyed.Contains("<CC_GodLore_How_Cell>") && keyed.Contains("<CC_GodLore_How_Pilgrim>"), "where they were heard, in the book's words");
        C(Read("Source/ColonyCouncil/Gods/GodLore.cs").Contains("Key(key ?? \"Heard\", v)"), "the book's heard entry takes its own words for a cry");

        // ================= C15. The dead's own god at the grave =================

        C(GodsAmongMath.DeadHeldFrom == PawnGodsMath.LeansFrom && GodsAmongMath.MournerHoldsFrom == PawnGodsMath.LeansFrom, "the dead and the mourners who hold the god, from leaning toward it");
        C(Read("Source/ColonyCouncil/Gods/GodDeeds.cs").Contains("return GraveGods.Given(dead, speaker);") && Read("Source/ColonyCouncil/Secrets/Vendettas.cs").Contains("CC_GA_Grave_Letter"),
            "the eulogy's words to the god, and its letter says so");
        C(defs.Contains("<defName>Rimfolk_Thought_GivenToOurGod</defName>") && Regex.IsMatch(defs, @"Rimfolk_Thought_GivenToOurGod</defName>[\s\S]*?<baseMoodEffect>3</baseMoodEffect>"),
            "the grief a little lighter");

        // ================= C18. Named seasons =================

        foreach (var kind in new[] { "cold", "heat", "storms", "wet", "dry" })
        {
            var gods = GodsAmongMath.GodsFor(kind, "summer");
            C(gods.Length > 0 && gods.All(godKeys.Contains), "a named season (" + kind + ") taken for gods the pantheon has");
        }
        C(GodsAmongMath.GodsFor("cold", "winter")[0] == "Hush" && GodsAmongMath.GodsFor("wet", "spring")[0] == "Murmur" && GodsAmongMath.GodsFor("heat", "winter").SequenceEqual(new[] { "Helios" })
          && GodsAmongMath.GodsFor("other", "spring").Length == 0, "Hush for a long cold, Murmur for a wet one, Helios for a warm winter");
        C(GodsAmongMath.ComesRound(3, 2, 4, 2, 0) && GodsAmongMath.ComesRound(3, 2, 4, 2, GodsAmongMath.AskWithinDays - 1) && !GodsAmongMath.ComesRound(3, 2, 4, 2, GodsAmongMath.AskWithinDays)
          && !GodsAmongMath.ComesRound(3, 2, 5, 2, 0) && !GodsAmongMath.ComesRound(3, 2, 4, 1, 0), "the same season the year after, in its first days");
        C(GodsAmongMath.Argue("Helios", "Smoulder") && !GodsAmongMath.Argue("Helios", "Helios") && !GodsAmongMath.Argue("Helios", null), "two who take it for different gods argue");
        C(Read("Source/ColonyCouncil/Weather/WeatherBook.cs").Contains("SeasonGods.Named(map, q, named)"), "the weather book's named season reaches the believers");
        var question = XDocument.Parse(defs).Root!.Elements("ColonyCouncil.DilemmaDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Dlm_SeasonDay");
        C(question != null && question.Element("options")!.Elements("li").Count() == 2
          && question.Element("options")!.Elements("li").All(o => o.Element("effects")!.Elements("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_ShrineWords")),
            "the season's question: each answer plays out at the god's place");

        // ================= C26. A shrine room for each god =================

        var roomDefs = XDocument.Parse(rooms).Root!.Elements("ColonyCouncil.RoomTemplateDef").ToList();
        C(roomDefs.Count == GodsAmongMath.Rooms.Length, "a shrine room for each of " + GodsAmongMath.Rooms.Length + " gods");
        foreach (var (god, template) in GodsAmongMath.Rooms)
        {
            var d = roomDefs.FirstOrDefault(x => x.Element("defName")?.Value == template);
            C(d != null && d.Element("god")?.Value == god && godKeys.Contains(god), template + ": its god is in the pantheon");
            C(d != null && (d.Element("godLabel")?.Value ?? "").Contains("{GOD}") && (d.Element("godPurpose")?.Value ?? "").Contains("{GOD}") && !(d.Element("label")?.Value ?? "").Contains("{"),
                template + ": named by the colony's name for its god, a plain label besides");
            C(d != null && d.Element("things")!.Elements("li").Any(t => t.Element("optional")?.Value == "false"), template + ": one piece it can't do without, of the game's own");
            C(d != null && d.Element("things")!.Elements("li").All(t => t.Element("pos") != null), template + ": every piece placed");
            string purpose = d?.Element("godPurpose")?.Value ?? "";
            C(!Regex.IsMatch(purpose, @"\b(wants|likes|demands|pleases|angers|favour)\b", RegexOptions.IgnoreCase), template + ": nothing of what the god wants");
        }
        C(GodsAmongMath.RoomShown(true, true) && !GodsAmongMath.RoomShown(true, false) && !GodsAmongMath.RoomShown(false, true), "shown once the colony has a page for its god");
        C(Read("Source/ColonyCouncil/Building/RoomTypeDef.cs").Contains("GodRooms.Shown(t)") && Read("Source/ColonyCouncil/Building/TemplatePlanner.cs").Contains("plan.template.openSky"),
            "the room menu shows them so, and a room under the open sky leaves its roof off");

        // ================= C29. Midwinter =================

        C(GodsAmongMath.IsMidwinter("god|3", "Hush") && GodsAmongMath.IsMidwinter("god|4", "Selene") && GodsAmongMath.IsMidwinter(GodsAmongMath.MidwinterCustom, null)
          && !GodsAmongMath.IsMidwinter("god|5", "Helios") && !GodsAmongMath.IsMidwinter("custom|Council_Dilemma_Harvest", null), "the winter god's festival or the midwinter custom");
        C(GodsAmongMath.StageAt(10) == 0 && GodsAmongMath.StageAt(GodsAmongMath.TreeFrom) == 1 && GodsAmongMath.StageAt(GodsAmongMath.TableFrom) == 2
          && GodsAmongMath.StageAt(GodsAmongMath.PresentsFrom) == 3 && GodsAmongMath.StageAt(23) == 4 && GodsAmongMath.StageAt(2) == 4, "the tree at dusk, the table at supper, the presents after");
        C(GodsAmongMath.Presents(6, 10) == 6 && GodsAmongMath.Presents(6, 2) == 2 && GodsAmongMath.Presents(0, 5) == 0, "one present each, as many as the stores hold");
        C(Read("Source/ColonyCouncil/Calendar/CalendarKeeping.cs").Contains("Midwinter.DayKept(d, map)")
          && Regex.Matches(Read("Defs/DilemmaDefs/Council_Dilemmas_Seasons.xml"), "DilemmaEffect_Midwinter").Count == 2, "the calendar's day and the midwinter custom's answers keep it");
        C(GodsAmongMath.MidwinterCustom == "custom|Council_Dilemma_Midwinter" && Read("Defs/DilemmaDefs/Council_Dilemmas_Seasons.xml").Contains("<defName>Council_Dilemma_Midwinter</defName>"),
            "the midwinter custom is the calendar's own");

        // ================= The words =================

        foreach (Match m in Regex.Matches(among, "\"(CC_GA_[A-Za-z_]+)\""))
        {
            string k = m.Groups[1].Value;
            if (k.EndsWith("_")) continue;
            C(keyedAll.Contains("<" + k + ">"), "the key " + k + " exists");
        }
        C(!keyed.Contains("—") && !defs.Contains("—") && !rooms.Contains("—"), "no em dashes");
        C(!Regex.IsMatch(keyed, @"<CC_[A-Za-z_]+>[^<]*!") && !Regex.IsMatch(defs, @"<li>[^<]*!") && !Regex.IsMatch(rooms, @">[^<]*!<"), "no exclamation marks");
        foreach (var pool in new[] { "Rimfolk_Pool_GodsAmong_Fisher", "Rimfolk_Pool_GodsAmong_Keeper", "Rimfolk_Pool_GodsAmong_RaiderCry", "Rimfolk_Pool_GodsAmong_Grave", "Rimfolk_Pool_GodsAmong_Season", "Rimfolk_Pool_GodsAmong_Midwinter" })
            C(defs.Contains("<defName>" + pool + "</defName>") && among.Contains(pool), pool + ": defined and said");
        string keeperPool = Regex.Match(defs, @"Rimfolk_Pool_GodsAmong_Keeper[\s\S]*?</ColonyCouncil.FlavorPoolDef>").Value;
        string gravePool = Regex.Match(defs, @"Rimfolk_Pool_GodsAmong_Grave[\s\S]*?</ColonyCouncil.FlavorPoolDef>").Value;
        C(Regex.IsMatch(keeperPool, @"\b(mine|hither|wanteth|shall|I know not)\b") && Regex.IsMatch(gravePool, @"\b(thy|thee|thou|unto|thine)\b"), "the keeper's and the grave's words in the old voice");
        return n;
    }
}
