using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Many colonies in play (Colonies/ManyColoniesMath.cs; docs/design/stage3-many-colonies.md, section 3, release 4; the
/// player's decisions 12A, 13A and 14A): two councils become one when a colony's people come to live at another home (where
/// they came, when it is a merge, the early election, the traditions at half their strength, the names, the remembrance
/// day, the welcome), each colony its own character (half its mother's, walled only with walls, word and renown the
/// seat's), the region's faces and events near every home, the pages that follow the colony in view, the small ones, and
/// with one colony every rule as before; then the code's seams with the game and the words.
/// </summary>
public static class ManyColoniesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "many colonies: " + what);
        }

        const int Day = ManyColoniesMath.DayTicks;
        const int None = HomesMath.None;

        // ---------------- Two councils become one (12A) ----------------

        var homes = new HashSet<int> { 10, 20, 30 };
        C(ManyColoniesMath.MergedInto(5, new List<int> { 5, 5, 5 }, homes) == None, "nobody of theirs has lived a day at another home yet: no merge");
        C(ManyColoniesMath.MergedInto(5, new List<int> { 20, 5, 20, 30 }, homes) == 20, "the home most of them live at");
        C(ManyColoniesMath.MergedInto(5, new List<int> { 30, 20 }, homes) == 20, "equal, the home founded first");
        C(ManyColoniesMath.MergedInto(5, new List<int> { 40, 40, 20 }, homes) == 20, "never a home that is no longer one");
        C(ManyColoniesMath.MergedInto(20, new List<int> { 20, 20 }, homes) == None, "never the lost home itself");

        C(ManyColoniesMath.IsMerge(3, true), "a home with grown-ups of its own and the other council kept: two councils become one");
        C(!ManyColoniesMath.IsMerge(0, true), "a home of theirs alone is their colony moved (7a carries the council)");
        C(!ManyColoniesMath.IsMerge(3, false), "the other council let go (past thirty days, or carried): nothing to merge");
        C(!ManyColoniesMath.IsMerge(3, true, true), "a colony founded from theirs lately is their founding, not a merge");
        C(ManyColoniesMath.FoundedLately(100 * Day, 105 * Day, 7, 7) && !ManyColoniesMath.FoundedLately(100 * Day, 120 * Day, 7, 7)
          && !ManyColoniesMath.FoundedLately(100 * Day, 105 * Day, 8, 7), "founded from theirs within a season");
        C(ManyColoniesMath.Moved(10, 20) && !ManyColoniesMath.Moved(None, 20) && !ManyColoniesMath.Moved(10, 10), "a move is from one home to another");
        C(ManyColoniesMath.CameFrom(10, 50 * Day, 10, 70 * Day) && !ManyColoniesMath.CameFrom(10, 10 * Day, 10, 70 * Day) && !ManyColoniesMath.CameFrom(20, 50 * Day, 10, 70 * Day),
            "one who moved on within the thirty days its council is kept is still one of its people");
        C(ManyColoniesMath.Lapsed(41 * Day, 10 * Day) && !ManyColoniesMath.Lapsed(39 * Day, 10 * Day), "a merge waits as long as the old council is kept, thirty days");

        // The early election: a third or more of the grown-ups.
        C(ManyColoniesMath.EarlyElection(3, 9) && ManyColoniesMath.EarlyElection(4, 12) && !ManyColoniesMath.EarlyElection(2, 9) && !ManyColoniesMath.EarlyElection(0, 0),
            "a third or more of the grown-ups: the council is chosen again within the season");
        int at = ManyColoniesMath.ElectionAt(100 * Day, true);
        C(at == 100 * Day + ManyColoniesMath.ElectionAfterDays * Day && at - 100 * Day < ManyColoniesMath.SeasonTicks && ManyColoniesMath.ElectionAt(100 * Day, false) == None,
            "called a few days after the merge, well within the season; none when they are fewer");
        C(ManyColoniesMath.ElectionDue(at, at, 0) && !ManyColoniesMath.ElectionDue(at - 1, at, 0) && !ManyColoniesMath.ElectionDue(at, at, 101 * Day),
            "due on its day, and let go when an election was held since the merge");

        // Traditions: one only they kept joins at half, one both kept keeps the stronger.
        C(ManyColoniesMath.Tradition(false, true, 90f, 50f) == TraditionMerge.None, "not a tradition of theirs: nothing joins");
        C(ManyColoniesMath.Tradition(true, false, 90f, 50f) == TraditionMerge.Join, "only they kept it: it joins");
        C(ManyColoniesMath.Tradition(true, true, 90f, 50f) == TraditionMerge.Theirs && ManyColoniesMath.Tradition(true, true, 50f, 90f) == TraditionMerge.Ours
          && ManyColoniesMath.Tradition(true, true, 70f, 70f) == TraditionMerge.Ours, "both kept it: the stronger stays (the colony's own on a tie)");
        int kept = 5;
        float weight = 30f;
        ManyColoniesMath.Halve(ref kept, ref weight);
        C(kept == 2 && Math.Abs(weight - 15f) < 0.001f, "half its strength: half its years kept and half its weight");
        float full = CalendarMath.Standing(30f, true, 5), half = CalendarMath.Standing(weight, true, kept);
        C(half < full && half > full * 0.4f, $"a tradition joined counts for less than at home ({half:0} of {full:0})");
        // A tradition joined lapses by the usual rule unless kept: two years let pass.
        bool tradition = true;
        int breaks = 0;
        var once = CalendarMath.Pass(ref kept, ref tradition, ref breaks);
        var twice = CalendarMath.Pass(ref kept, ref tradition, ref breaks);
        C(once == CalendarMath.PassResult.Broken && twice == CalendarMath.PassResult.Lapsed && !tradition, "and lapses as any tradition does, two years let pass");

        // Names: the better keeps one both had, the colony's own on a tie; no rival in the first season.
        C(ManyColoniesMath.NewcomerKeeps(40f, 30f) && !ManyColoniesMath.NewcomerKeeps(30f, 30f) && !ManyColoniesMath.NewcomerKeeps(20f, 30f), "the better keeps it, the colony's own holder on a tie");
        C(ManyColoniesMath.QuietSeason(105 * Day, 100 * Day) && !ManyColoniesMath.QuietSeason(116 * Day, 100 * Day) && !ManyColoniesMath.QuietSeason(105 * Day, -1),
            "a name taken over in the merge's first season makes no rival");

        // The calendar and the welcome.
        C(ManyColoniesMath.KeepRemembrance(1) && !ManyColoniesMath.KeepRemembrance(0), "the old founding day stays while some of its people live there");
        C(ManyColoniesMath.WelcomeNow(18) && ManyColoniesMath.WelcomeNow(21) && !ManyColoniesMath.WelcomeNow(12) && ManyColoniesMath.WelcomeFrom == 17, "the welcome is an evening's meal");
        C(ManyColoniesMath.WelcomeWaitDays >= 1 && ManyColoniesMath.WelcomeWaitDays <= 5, "a fight waits the welcome a few days at most");

        // ---------------- Each colony its own character (13A) ----------------

        C(Math.Abs(ManyColoniesMath.FoundedValue(48f) - 24f) < 0.001f && Math.Abs(ManyColoniesMath.FoundedValue(-30f) + 15f) < 0.001f, "a colony founded from another starts with half its mother's ways");
        C(ManyColoniesMath.StartsFromMother(3, true) && !ManyColoniesMath.StartsFromMother(0, true) && !ManyColoniesMath.StartsFromMother(3, false), "newcomers alone start fresh");
        // Half its mother's: a way the mother was renowned for (70) is still known (35); one she was only known for (30) is not (15).
        C(ManyColoniesMath.FoundedValue(70f) >= 25f && ManyColoniesMath.FoundedValue(30f) < 25f, "a renowned way stays known in the daughter, a known one fades to nothing");
        C(ManyColoniesMath.MayBeWalled(CalendarMath.WallsUp) && !ManyColoniesMath.MayBeWalled(CalendarMath.WallsUp - 1) && !ManyColoniesMath.MayBeWalled(0),
            "walled is never said of a colony with no walls (the calendar's walls-up line)");
        C(ManyColoniesMath.CarriesWord(true) && !ManyColoniesMath.CarriesWord(false), "word gets around of the seat's ways only: the goodwill it earns doesn't rise with more homes");
        C(ManyColoniesMath.TellsRenown(true) && !ManyColoniesMath.TellsRenown(false), "renown is the colony's as a whole: only the seat's name tells a change of it");

        // ---------------- The region near every home (14A) ----------------

        var tiles = new List<float> { 5f, 90f, 12f, 30f, 200f };
        var near2 = ManyColoniesMath.NearestTowns(tiles, new HashSet<int>(), 40f);
        C(near2.SequenceEqual(new[] { 0, 2 }), "a further home gets its nearest two towns");
        C(ManyColoniesMath.NearestTowns(tiles, new HashSet<int> { 0 }, 40f).SequenceEqual(new[] { 2, 3 }), "none the seat has already: the next nearest");
        C(ManyColoniesMath.NearestTowns(new List<float> { 85f, 100f }, null, 40f).Count == 0, "none beyond twice the region's reach");
        C(ManyColoniesMath.NotableCap(NotableMath.MaxNotables, 0, NotableMath.RolesPerTown) == NotableMath.MaxNotables
          && ManyColoniesMath.NotableCap(NotableMath.MaxNotables, 2, NotableMath.RolesPerTown) == NotableMath.MaxNotables + 16, "eight more people a further home, none with one");

        C(!ManyColoniesMath.RollsForHome(1) && ManyColoniesMath.RollsForHome(2), "one home: no roll for where a world event goes (one colony's rolls as they were)");
        C(ManyColoniesMath.PickHome(new List<int> { 7 }, new List<int> { 12 }, 0.99f) == 7, "one home: that home");
        var r = new Random(4);
        int atSeat = 0, atFar = 0, empty = 0;
        for (int i = 0; i < 6000; i++)
        {
            int k = ManyColoniesMath.PickHome(new List<int> { 1, 2, 3 }, new List<int> { 9, 3, 0 }, (float)r.NextDouble());
            if (k == 1) atSeat++;
            else if (k == 2) atFar++;
            else empty++;
        }
        C(Math.Abs(atSeat / 6000f - 0.75f) < 0.03f && Math.Abs(atFar / 6000f - 0.25f) < 0.03f && empty == 0,
            $"world events placed near each home by its people ({atSeat / 60f:0}% and {atFar / 60f:0}%), never at a home of nobody");
        C(ManyColoniesMath.AcquaintanceColony(20, 10) == 20 && ManyColoniesMath.AcquaintanceColony(None, 10) == 10, "an acquaintance lives nearest the colony of whoever met them, else the seat");

        // ---------------- What the player sees (3c) ----------------

        foreach (var page in new[] { "Table", "Moods", "Bonds", "Faiths", "Renown", "Chronicle", "Peoples", "HereAndAway" })
            C(ManyColoniesMath.Follows(page), "follows the colony in view: " + page);
        C(!ManyColoniesMath.Follows("Decrees") && !ManyColoniesMath.Follows("Week") && !ManyColoniesMath.Follows(null), "the council's own pages are each colony's already; This week lists every colony");
        C(PagesMath.All.Any(p => p.name == "Table") && ManyColoniesMath.FollowingPages.All(f => PagesMath.All.Any(p => p.name == f)), "every following page is a page of the council window");
        C(ManyColoniesMath.Shows(10, 20, false, 1) && ManyColoniesMath.Shows(10, 20, true, 2) && ManyColoniesMath.Shows(20, 20, false, 2) && !ManyColoniesMath.Shows(10, 20, false, 2),
            "one colony: everyone, as always; else the colony in view's own, everyone a click away");
        C(ManyColoniesMath.ChronicleShows(10, None, 20, false, 1) && ManyColoniesMath.ChronicleShows(10, None, 20, true, 2), "the chronicle whole with one colony or everyone");
        C(ManyColoniesMath.ChronicleShows(20, None, 20, false, 2) && !ManyColoniesMath.ChronicleShows(10, None, 20, false, 2) && ManyColoniesMath.ChronicleShows(None, None, 20, false, 2),
            "a line placed at the colony in view, or at none (the world's)");
        C(ManyColoniesMath.ChronicleShows(10, 20, 20, false, 2) && !ManyColoniesMath.ChronicleShows(20, 10, 20, false, 2), "a line about someone shows with their own colony");

        // ---------------- The small ones ----------------

        C(ManyColoniesMath.FrostAt(20, new List<int> { 10, 20 }, new List<bool> { true, true }) == 20, "the frost alert: the map in view first");
        C(ManyColoniesMath.FrostAt(20, new List<int> { 10, 20 }, new List<bool> { true, false }) == 10, "then any other home with a frost harvest open");
        C(ManyColoniesMath.FrostAt(20, new List<int> { 10, 20 }, new List<bool> { false, false }) == None, "none open, no alert");
        C(ManyColoniesMath.OutsiderHome(20, 30, 10) == 20 && ManyColoniesMath.OutsiderHome(None, 30, 10) == 30 && ManyColoniesMath.OutsiderHome(None, None, 10) == 10,
            "an outsider's distance and kin from the colony they are at");
        C(ManyColoniesMath.AnswerHome(20, 30, 10) == 20 && ManyColoniesMath.AnswerHome(None, 30, 10) == 30 && ManyColoniesMath.AnswerHome(None, None, 10) == 10,
            "an answer with no map: its colonist's colony, else the seat");

        // ---------------- The code's seams with the game ----------------

        string root = Root();
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string merges = Code("Colonies/Merges.cs"), homesMerge = Code("Colonies/HomesMerge.cs"), homesCode = Code("Lean/Homes.cs"), chars = Code("Character/ColonyCharacters.cs");
        C(homesCode.Contains("Merges.Hourly(this, now);") && Code("ColonyState.cs").Contains("Merges.HomesLost(lost);") && !merges.Contains("Sched.") && !homesMerge.Contains("Sched."),
            "the merge rides the council's hour and the homes' own change: no schedule slot");
        C(homesMerge.Contains("Scribe.mode != LoadSaveMode.Saving || merges != null && merges.Count > 0"), "merges are written only while there is one");
        C(homesCode.Contains("Scribe_Values.Look(ref prev, \"prev\", HomesMath.None)") && homesMerge.Contains("if (all.Count <= 1 && !merges.Any("),
            "where someone lived before is kept only with more than one home: one colony's save as it was");
        C(merges.Contains("ManyColoniesMath.IsMerge(hosts, comp.ColonyByKey(m.from) != null, founding)"), "a merge only with people of the home's own and the old council kept");
        C(merges.Contains("PlayFeast.Stage(new FeastOrder") && merges.Contains("toastLine = greeting") && merges.Contains("Speech.Say(speaker, greeting)"),
            "the welcome is the play-out's shared meal with the speaker's greeting at the cup; the greeting alone in a bubble without play-outs");
        C(merges.Contains("History.Add(FoundingMath.KindMerged") && FoundingMath.KindMerged == "colony:merged", "the milestone colony:merged");
        C(merges.Contains("offices.Call(comp, GenLocalDate.Year(map), false)"), "the early election is the council's own election");
        C(Code("Calendar/CalendarMerge.cs").Contains("ManyColoniesMath.Halve(ref kept, ref weight)") && Code("Calendar/CalendarMerge.cs").Contains("TryAdd(copy, false)"),
            "a tradition joins at half its strength, inside the calendar's cap");
        C(Code("Calendar/ColonyCalendar.cs").Contains("if (!KeepsOldFounding(d, known)) days.Remove(d);"), "the old founding day is let go once none of its people live there");
        string epithets = Code("Epithets/Epithets.cs");
        C(epithets.Contains("Merges.NamesRound(this, comp);") && epithets.Contains("&& !Merges.Quiet(colony, Find.TickManager.TicksGame)") && epithets.Contains("ManyColoniesMath.NewcomerKeeps(theirs, ours)"),
            "names looked at again in the naming round, the better keeping it, no rival in the first season");
        C(chars.Contains("if (others.Count > 0) Scribe_Collections.Look(ref others, \"characters\", LookMode.Deep);") && chars.Contains("Scribe_Deep.Look(ref seat, \"character\");")
          && chars.Contains("if (Scribe.mode == LoadSaveMode.Saving && savingSeat) return;"), "one colony's character saves as it always did; the others' only when there are any");
        C(chars.Contains("one.home = HomesMath.None;") && Code("ColonyState.cs").Contains("CharactersLost(lost);"), "an old save's one character is the seat's; a lost home's kept thirty days");
        C(Code("Character/Character.cs").Contains("if (ManyColoniesMath.CarriesWord(IsSeat)) Word(comp);"), "the goodwill a colony's ways earn: the seat's only");
        C(Code("Character/ColonyTraits.cs").Contains("def.defName == ManyColoniesMath.WalledTrait && !walledOk") && Code("Character/ColonyTraits.cs").Contains("name == ManyColoniesMath.WalledTrait && !walls"),
            "walled never of a colony with no walls, nor carried to a daughter on bare land");
        C(Code("Colonies/Foundings.cs").Contains("CharacterFounded(map, plan.from, plan.party.Count(p => p != null && !p.Dead))")
          && Code("Colonies/Foundings.cs").Contains("comp?.CharacterCarried(plan.from, plan.home);"), "a daughter colony starts with half its mother's; a colony that moves takes all of it");
        // Every reading of the colony's character asks the colony concerned (nothing reads the seat's by the old field name but what is the colony's as a whole).
        var readers = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Character" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"(comp|Comp|Instance)\??\.character\??\.(Shift|Fits|Is\(|traits|Approval|Value)")).Select(Path.GetFileName).ToList();
        C(readers.Count == 0, "the character's readers each ask their own colony: " + string.Join(", ", readers));
        C(Code("World/Notables.cs").Contains("towns.AddRange(FurtherTowns(map, towns));") && Code("World/Notables.cs").Contains("ManyColoniesMath.NearestTowns(tiles, taken, NotableMath.ReachTiles)"),
            "each further home's nearest two towns staffed, beside the seat's");
        C(Code("World/WorldSituations.cs").Contains("var map = PlaceNear();") && Code("World/WorldSituations.cs").Contains("if (!ManyColoniesMath.RollsForHome(homes.Count)) return HappeningTracker.HomeMap();")
          && Code("World/WorldSituations.cs").Contains("active.Count(s => !s.ended) >= MaxActive"), "world events placed near a home by its people, the two running slots still two");
        C(Code("Fields/FieldWeather.cs").Contains("foreach (var home in homes)") && Code("Fields/FieldWeather.cs").Contains("CC_Many_FrostAt"), "the frost alert for every home, naming the other");
        C(Code("UI/UiKitViews.cs").Contains("ColonyView.ScopeChip(") && Code("UI/MainTabWindow_Council.cs").Contains("ColonyView.Following(ManyColoniesMath.Follows(page));"),
            "the following pages carry the chip for everyone, beside the colony chip");
        foreach (var page in new[] { "UI/PeopleTable.cs", "Moods/MoodsPanel.cs", "UI/SocialPanel.cs", "UI/FaithsPanel.cs", "UI/RenownPage.cs" })
            C(Code(page).Contains("ColonyView.Shows") && Code(page).Contains("ColonyView.Version"), "the page follows the colony in view and builds again when it moves: " + page);
        C(Code("UI/ChroniclePanel.cs").Contains("ColonyView.ShowsLine(") && Code("Hud/HudCouncil.cs").Contains("CC_Many_HudCouncil"), "the chronicle filtered by colony, the council panel naming it");
        C(Code("UI/WorldPages.cs").Contains("book.NearColony(x, home)") && Code("UI/Region/HereAndAwayView.cs").Contains("ColonyView.ShowsHome(r.home)"),
            "the region's faces and here and away show the colony in view's neighbours");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool manyMerge = true;") && settings.Contains("Scribe_Values.Look(ref manyMerge, \"manyMerge\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"manyMerge\""), "one setting, Two councils become one, in Many colonies");

        // ---------------- The words ----------------

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_ManyColonies.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes, no exclamation marks");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\{DAYS\}|\bin \d+ (days?|hours?)\b|\bdays? (left|more)\b")), "no countdowns");
        foreach (var k in new[] { "CC_Many_MergedText", "CC_Many_MergedTraditions", "CC_Many_MergedElection", "CC_Many_MergedNames", "CC_Many_MergedDay", "CC_Many_MergedMeal", "CC_Many_MergedHistory",
                     "CC_Many_NameKept", "CC_Many_FrostAt", "CC_Many_ScopeColony", "CC_Many_ScopeEveryone", "CC_Settings_ManyMerge", "CC_Settings_ManyMerge_Tip", "CC_SetNote_ManyMerge" })
            C(keyed.ContainsKey(k), "the words: " + k);
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_ManyColonies.xml")).Root!;
        var lines = pools.Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
        C(lines.Count >= 12 && lines.All(l => Regex.Matches(l.TrimEnd('.', '?'), @"[.?!]\s").Count == 0 && !l.Contains("!") && !l.Contains("—")), "the welcome's lines: one sentence each, no exclamation marks");
        C(pools.Elements().Any(d => d.Element("defName")?.Value == "Rimfolk_Pool_MergeGreeting" && d.Descendants("li").All(l => l.Value.StartsWith("[speaker"))),
            "the greeting is the speaker's");
        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_ManyColonies.xml")).Root!.Elements().First();
        C(almanac.Element("label")?.Value == "many colonies" && !almanac.ToString().Contains("—"), "the Almanac page, a page of its own");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
