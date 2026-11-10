using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The diplomacy screen's step A (docs/design/diplomacy-screen.md; Source/ColonyCouncil/UI/Region/PeoplesViewMath.cs,
/// PeopleActMath.cs, World/LetterOccasionMath.cs): the list's groups, sorts (closest first by default), search and filters;
/// the regard meter and this season's trend; the web of who stands with whom; the layout at 1280 by 720 and 1920 by 1080
/// with the drawer shut and open; letters by occasion (post-and-letters.md, step 2, point 10); what each act is for and
/// costs (point 11), checked against the rules the acts' own code follows; and the seams: each act through its own code
/// path, the words, the setting, the region map and the addresses.
/// </summary>
public static class PeoplesScreenTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples screen: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        Groups(C);
        Sorting(C);
        SearchAndFilters(C);
        RegardAndWeb(C);
        Layout(C);
        Occasions(C);
        Acts(C);
        Words(root, Code, C);
        Seams(root, Code, C);
        return n;
    }

    private static PeopleFacts P(string name, int relation = 0, int goodwill = 0, float days = 3f, float tiles = 10f, int age = 1, bool permanent = false,
        bool neighbour = false, bool mail = false, bool dealings = false, string search = null) =>
        new PeopleFacts
        {
            name = name, relation = relation, goodwill = goodwill, hasGoodwill = !permanent, days = days, tiles = tiles, age = age, permanentEnemy = permanent,
            neighbour = neighbour, mail = mail, dealings = dealings, search = (search ?? name).ToLowerInvariant()
        };

    // ---------------- Groups ----------------

    private static void Groups(Action<bool, string> C)
    {
        C(PeoplesViewMath.GroupOf(1, false) == PeoplesGroup.Allies, "allies are allies");
        C(PeoplesViewMath.GroupOf(0, false) == PeoplesGroup.Neutral, "a people at peace and not allied is in the middle group");
        C(PeoplesViewMath.GroupOf(-1, false) == PeoplesGroup.War, "a people at war with the colony is at war");
        C(PeoplesViewMath.GroupOf(0, true) == PeoplesGroup.War && PeoplesViewMath.GroupOf(1, true) == PeoplesGroup.War, "a people at war with everyone is always at war");
        C(PeoplesViewMath.Groups.SequenceEqual(new[] { PeoplesGroup.Allies, PeoplesGroup.Neutral, PeoplesGroup.War }), "the groups in order: allies, at peace, at war");
        C(PeoplesViewMath.Tabs.SequenceEqual(new[] { PeopleTab.Overview, PeopleTab.Towns, PeopleTab.People, PeopleTab.Dealings, PeopleTab.History }),
            "the page's five tabs: overview, towns, people, dealings, history");
        C(PeoplesViewMath.Tabs.Length <= PagesMath.MaxViews, "five tabs at most, as a view's modes");
    }

    // ---------------- Sorting ----------------

    private static void Sorting(Action<bool, string> C)
    {
        C(PeoplesViewMath.Sorts[0] == PeoplesSort.Closest && default(PeoplesSort) == PeoplesSort.Closest, "closest first is the first sort and the default");
        C(Enum.GetValues(typeof(PeoplesSort)).Length == 4 && PeoplesViewMath.Sorts.Length == 4, "four sorts: closest, regard, name, age");
        var near = P("Ashford", days: 1.5f);
        var far = P("Brightwater", days: 6f);
        var noRoad = P("Cinder", days: -1f, tiles: 4f);
        var noRoadFar = P("Dunmere", days: -1f, tiles: 40f);
        C(PeoplesViewMath.Compare(near, far, PeoplesSort.Closest) < 0, "closest: a town a day and a half off before one six days off");
        C(PeoplesViewMath.Compare(far, noRoad, PeoplesSort.Closest) < 0, "closest: a town with a road before one with none, however near in a straight line");
        C(PeoplesViewMath.Compare(noRoad, noRoadFar, PeoplesSort.Closest) < 0, "closest: of the towns with no road, the nearer in tiles first");
        C(PeoplesViewMath.Compare(P("b", days: 2f), P("a", days: 2f), PeoplesSort.Closest) > 0, "closest: the same days, by name");
        var warm = P("Warm", goodwill: 70);
        var cool = P("Cool", goodwill: -20);
        var foe = P("Pirates", permanent: true);
        C(PeoplesViewMath.Compare(warm, cool, PeoplesSort.Regard) < 0 && PeoplesViewMath.Compare(cool, foe, PeoplesSort.Regard) < 0,
            "regard: the warmest first, everyone's foe last");
        C(PeoplesViewMath.Compare(P("a", goodwill: 10, days: 5f), P("b", goodwill: 10, days: 1f), PeoplesSort.Regard) > 0, "regard: a tie goes to the closest");
        C(PeoplesViewMath.Compare(P("alder"), P("Birch"), PeoplesSort.Name) < 0 && PeoplesViewMath.Compare(P("Birch"), P("alder"), PeoplesSort.Name) > 0,
            "name: alphabetical, in any case");
        C(PeoplesViewMath.Compare(P("t", age: 0), P("s", age: 3), PeoplesSort.Age) < 0, "age: tribal before spacer");
        C(PeoplesViewMath.Compare(P("x", age: 2, days: 4f), P("y", age: 2, days: 1f), PeoplesSort.Age) > 0, "age: the same age, the closest first");
        C(PeoplesViewMath.DaysKey(P("x", days: -1f, tiles: 5f)) > PeoplesViewMath.DaysKey(P("y", days: 50f)), "no road sorts after the longest road");
    }

    // ---------------- Search and filters ----------------

    private static void SearchAndFilters(Action<bool, string> C)
    {
        C(PeoplesViewMath.Matches("", "anything") && PeoplesViewMath.Matches("  ", "anything") && PeoplesViewMath.Matches(null, "x"), "an empty search finds every people");
        C(PeoplesViewMath.Matches("river", "the river folk ferrymoot") && PeoplesViewMath.Matches("RIVER", "the river folk"), "a word of theirs, in any case");
        C(PeoplesViewMath.Matches("river moot", "the river folk ferrymoot") && !PeoplesViewMath.Matches("river ash", "the river folk ferrymoot"), "every word must be found");
        C(!PeoplesViewMath.Matches("x", null), "nothing to search finds nothing");
        var a = P("A", neighbour: true, mail: true);
        var b = P("B", neighbour: true, dealings: true);
        var c = P("C");
        C(PeoplesViewMath.Passes(c, PeoplesFilter.None), "no filter on: everyone passes");
        C(PeoplesViewMath.Passes(a, PeoplesFilter.Neighbours) && PeoplesViewMath.Passes(b, PeoplesFilter.Neighbours) && !PeoplesViewMath.Passes(c, PeoplesFilter.Neighbours),
            "neighbors: only those with a town within reach");
        C(PeoplesViewMath.Passes(a, PeoplesFilter.Neighbours | PeoplesFilter.Mail) && !PeoplesViewMath.Passes(b, PeoplesFilter.Neighbours | PeoplesFilter.Mail),
            "two filters on: a people must pass both");
        C(PeoplesViewMath.Passes(b, PeoplesFilter.Dealings) && !PeoplesViewMath.Passes(a, PeoplesFilter.Dealings), "dealings: only those with ties or something under way");
        var all = new List<PeopleFacts> { a, b, c };
        C(PeoplesViewMath.Count(all, PeoplesFilter.Neighbours, "") == 2 && PeoplesViewMath.Count(all, PeoplesFilter.Mail, "") == 1, "each chip counts the peoples it would show");
        C(PeoplesViewMath.Count(all, PeoplesFilter.Neighbours, "b") == 1, "a chip's count follows the search");
        var list = new List<PeopleFacts>
        {
            P("Iron", relation: -1, days: 2f), P("Oak", relation: 1, days: 9f), P("Elm", relation: 0, days: 4f), P("Ash", relation: 0, days: 1f), P("Raiders", permanent: true, days: 1f),
            P("Pine", relation: 1, days: 2f)
        };
        var g = PeoplesViewMath.Grouped(list, PeoplesSort.Closest, PeoplesFilter.None, "");
        C(g.Length == 3 && g[0].SequenceEqual(new[] { 5, 1 }) && g[1].SequenceEqual(new[] { 3, 2 }) && g[2].SequenceEqual(new[] { 4, 0 }),
            $"the list in its groups, closest first in each ({string.Join(",", g[0])} | {string.Join(",", g[1])} | {string.Join(",", g[2])})");
        var named = PeoplesViewMath.Grouped(list, PeoplesSort.Name, PeoplesFilter.None, "");
        C(named[1].SequenceEqual(new[] { 3, 2 }) && named[0].SequenceEqual(new[] { 1, 5 }), "the same by name");
        var found = PeoplesViewMath.Grouped(list, PeoplesSort.Closest, PeoplesFilter.None, "o");
        C(found[0].SequenceEqual(new[] { 1 }) && found[1].Count == 0 && found[2].SequenceEqual(new[] { 0 }), "the search narrows every group");
    }

    // ---------------- Regard, trend, the web ----------------

    private static void RegardAndWeb(Action<bool, string> C)
    {
        C(Math.Abs(PeoplesViewMath.RegardFill(-100)) < 0.001f && Math.Abs(PeoplesViewMath.RegardFill(0) - 0.5f) < 0.001f && Math.Abs(PeoplesViewMath.RegardFill(100) - 1f) < 0.001f,
            "the regard meter: goodwill -100 empty, 0 half, 100 full");
        C(PeoplesViewMath.RegardFill(-300) == 0f && PeoplesViewMath.RegardFill(300) == 1f, "the meter never spills");
        C(PeoplesViewMath.Trend(8f, 0f) == 1 && PeoplesViewMath.Trend(0f, 15f) == -1 && PeoplesViewMath.Trend(1f, 0f) == 0 && PeoplesViewMath.Trend(10f, 9f) == 0,
            "this season's trend: a good turn warms, a wrong cools, one sale or a balance leaves it steady");
        PeoplesViewMath.WebPoint(0, 8, 100f, 50f, 40f, 30f, out float x0, out float y0);
        C(Math.Abs(x0 - 100f) < 0.01f && Math.Abs(y0 - 20f) < 0.01f, "the web's first people at the top");
        PeoplesViewMath.WebPoint(2, 8, 100f, 50f, 40f, 30f, out float x2, out float y2);
        C(Math.Abs(x2 - 140f) < 0.01f && Math.Abs(y2 - 50f) < 0.01f, "a quarter round is the right edge (clockwise)");
        bool inside = true;
        for (int i = 0; i < 14; i++)
        {
            PeoplesViewMath.WebPoint(i, 14, 100f, 50f, 40f, 30f, out float x, out float y);
            inside &= x >= 59.99f && x <= 140.01f && y >= 19.99f && y <= 80.01f;
        }
        C(inside, "every dot within the web's ellipse");
        C(PeoplesViewMath.TieOf(1, false, false) == WebTie.Allies && PeoplesViewMath.TieOf(-1, false, false) == WebTie.War && PeoplesViewMath.TieOf(0, false, false) == WebTie.None,
            "lines for allies and for wars, none between peoples merely at peace");
        C(PeoplesViewMath.TieOf(-1, true, false) == WebTie.None && PeoplesViewMath.TieOf(-1, false, true) == WebTie.None, "a people at war with everyone draws no lines (its ring says it)");
        C(PeoplesViewMath.WebMost >= 8 && PeoplesViewMath.WebMost <= 16, "the web draws the first dozen or so of the list, never a tangle");
    }

    // ---------------- Layout ----------------

    private static void Layout(Action<bool, string> C)
    {
        foreach (var (sw, sh) in new[] { (1280f, 720f), (1920f, 1080f) })
        foreach (bool large in new[] { false, true })
        {
            WindowLayoutMath.Size(sw, sh, large, out float ww, out float wh);
            float content = WindowLayoutMath.ContentWidth(ww);
            float body = PeoplesViewMath.BodyHeight(WindowLayoutMath.ContentHeight(wh), ViewHeadMath.Height(true));
            foreach (bool drawer in new[] { false, true })
            {
                float view = content;
                if (drawer)
                {
                    DrawerMath.Split(0f, content, out _, out float vw, out _, out _, out bool over);
                    view = over ? content : vw;
                }
                float list = PeoplesViewMath.ListWidth(view), page = PeoplesViewMath.PageWidth(view);
                string at = $"{sw}x{sh}{(large ? " large" : "")}{(drawer ? ", drawer open" : "")}: view {view}, list {list}, page {page}";
                C(list >= PeoplesViewMath.ListCompact && list <= PeoplesViewMath.ListWide, at + ": the list 200 to 320 wide");
                C(page >= 330f, at + ": the page keeps room for its header and acts");
                C(Math.Abs(list + PeoplesViewMath.Gap + page - view) < 0.01f, at + ": the list, the gap and the page fill the view");
                C(body - PeoplesViewMath.ListTop >= 5f * PeoplesViewMath.RowH, at + $": five rows or more in sight (body {body})");
            }
            if (!large)
                C(PeoplesViewMath.WebFixed(body) == (sh >= 1080f), $"{sw}x{sh}: the web fixed at the list's foot on a 1080 screen, in the list's scroll on a 720 one (body {body})");
        }
        C(PeoplesViewMath.ListWidth(950f) == PeoplesViewMath.ListWide && PeoplesViewMath.ListWidth(700f) == PeoplesViewMath.ListNarrow && PeoplesViewMath.ListWidth(558f) == PeoplesViewMath.ListCompact,
            "the list: 320 beside a roomy page, 260 narrower, 200 with the drawer open");
        C(PeoplesViewMath.Compact(PeoplesViewMath.ListCompact) && !PeoplesViewMath.Compact(PeoplesViewMath.ListNarrow), "compact rows only in the narrowest list");
        C(PeoplesViewMath.ActColumns(888f) == 2 && PeoplesViewMath.ActColumns(618f) == 1 && PeoplesViewMath.ActColumns(346f) == 1, "the acts in two columns only on a wide page");
    }

    // ---------------- Letters by occasion ----------------

    private static void Occasions(Action<bool, string> C)
    {
        var none = new OccasionFacts { foodDays = 30f, medicine = 20, colonists = 5, dayOfYear = 30 };
        C(LetterOccasionMath.Occasion(LetterPurpose.News, none) == LetterOccasion.Always, "asking for news: always open");
        foreach (var p in new[] { LetterPurpose.Greetings, LetterPurpose.Condolences, LetterPurpose.Warning, LetterPurpose.Invitation, LetterPurpose.Help })
            C(LetterOccasionMath.Occasion(p, none) == LetterOccasion.None, $"{p}: no reason in a quiet season mid-year with full stores");
        var loss = none;
        loss.lossHeard = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Condolences, loss) == LetterOccasion.Loss, "condolences: only after a loss heard of");
        var threat = none;
        threat.threatKnown = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Warning, threat) == LetterOccasion.Threat && LetterOccasionMath.Occasion(LetterPurpose.Warning, loss) == LetterOccasion.None,
            "a warning: only when a threat to them is known");
        var feast = none;
        feast.feastComing = true;
        feast.gatheringComing = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Invitation, feast) == LetterOccasion.Feast, "an invitation: a feast with guests coming comes first");
        var day = none;
        day.gatheringComing = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Invitation, day) == LetterOccasion.Gathering, "an invitation: a gathering day on the calendar within the week");
        var beds = none;
        beds.guestBedsEmpty = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Invitation, beds) == LetterOccasion.Visitors, "an invitation: guest beds standing empty (the colony would like visitors)");
        C(LetterOccasionMath.Coming(0) && LetterOccasionMath.Coming(7) && !LetterOccasionMath.Coming(8) && !LetterOccasionMath.Coming(-1), "a day comes within the week, today counted");
        var hungry = none;
        hungry.foodDays = 4f;
        C(LetterOccasionMath.Occasion(LetterPurpose.Help, hungry) == LetterOccasion.ShortFood, "asking for help: under six days of food");
        var sick = none;
        sick.medicine = 3;
        C(LetterOccasionMath.Occasion(LetterPurpose.Help, sick) == LetterOccasion.ShortMedicine, "asking for help: less than one medicine a colonist");
        var both = hungry;
        both.medicine = 0;
        C(LetterOccasionMath.Occasion(LetterPurpose.Help, both) == LetterOccasion.ShortFood, "short of both: food first");
        C(!LetterOccasionMath.ShortOfFood(6f, 5) && LetterOccasionMath.ShortOfFood(5.9f, 5) && !LetterOccasionMath.ShortOfFood(0f, 0), "six days is the line; no colonists, no hunger");
        C(LetterOccasionMath.ShortFoodDays == 6f, "the food line is where allies send food (World/Allies.cs HungryDays 6)");
        C(!LetterOccasionMath.ShortOfMedicine(5, 5) && LetterOccasionMath.ShortOfMedicine(4, 5), "one medicine a colonist is enough");
        foreach (int d in new[] { 57, 58, 59, 0, 3, 6 }) C(LetterOccasionMath.TurnOfYear(d), $"day {d} is the turn of the year");
        foreach (int d in new[] { 7, 15, 30, 56 }) C(!LetterOccasionMath.TurnOfYear(d), $"day {d} is not");
        C(LetterOccasionMath.TurnOfYear(60) && LetterOccasionMath.TurnOfYear(-1), "the year's days wrap");
        var newYear = none;
        newYear.dayOfYear = 2;
        C(LetterOccasionMath.Occasion(LetterPurpose.Greetings, newYear) == LetterOccasion.NewYear, "greetings at the turn of the year");
        var gift = none;
        gift.giftLately = true;
        C(LetterOccasionMath.Occasion(LetterPurpose.Greetings, gift) == LetterOccasion.Gift, "greetings with a gift");
        C(LetterOccasionMath.Lately(0f) && LetterOccasionMath.Lately(10f) && !LetterOccasionMath.Lately(10.5f), "a gift counts for ten days");
        foreach (var p in new[] { LetterPurpose.Pact, LetterPurpose.Truce, LetterPurpose.Favour, LetterPurpose.Envoy, LetterPurpose.Fealty })
            C(LetterOccasionMath.Occasion(p, none) == LetterOccasion.Own && !LetterOccasionMath.ByOccasion(p), $"{p}: its own checks only, never an occasion");
        C(LetterOccasionMath.Holds(LetterPurpose.Greetings, LetterOccasion.None, true) && !LetterOccasionMath.Holds(LetterPurpose.Greetings, LetterOccasion.None, false),
            "with the setting off greetings are open as before");
        C(LetterOccasionMath.Holds(LetterPurpose.Condolences, LetterOccasion.None, false), "condolences always needed a loss, setting or not");
        C(!LetterOccasionMath.Holds(LetterPurpose.Help, LetterOccasion.ShortFood, true), "a reason opens the letter");
        C(LetterOccasionMath.Key(LetterPurpose.Help, LetterOccasion.None) == "CC_Occ_None_Help" && LetterOccasionMath.Key(LetterPurpose.Help, LetterOccasion.ShortFood) == "CC_Occ_ShortFood",
            "the reason's words: what would bring one, or the reason");
    }

    // ---------------- The acts ----------------

    private static void Acts(Action<bool, string> C)
    {
        foreach (var p in PeopleActMath.Letters) C(PeopleActMath.IsLetter(PeopleActMath.KindOf(p)) && PeopleActMath.GroupOf(PeopleActMath.KindOf(p)) == ActGroup.Letters, $"{p} is a letter act");
        C(PeopleActMath.Letters[0] == LetterPurpose.News && PeopleActMath.Letters.Length == 6, "six kind letters, news first (always open)");
        C(PeopleActMath.KindOf(LetterPurpose.Pact) == PeopleActKind.Other, "a pact's letter is its own act, not a kind letter");
        C(PeopleActMath.KindOf("truce-offer") == PeopleActKind.Truce && PeopleActMath.KindOf("envoy-send-town") == PeopleActKind.SendEnvoy && PeopleActMath.KindOf("nobody") == PeopleActKind.Other,
            "a card act by its id");
        C(PeopleActMath.Way(PeopleActKind.Truce) == ActWay.Letter && PeopleActMath.Way(PeopleActKind.Greetings) == ActWay.Letter, "a truce, a pact or a letter goes by the post's reach");
        C(PeopleActMath.Way(PeopleActKind.SendEnvoy) == ActWay.Colonist && PeopleActMath.Way(PeopleActKind.Journey) == ActWay.Colonist, "an envoy of the colony's and a journey go with a colonist");
        C(PeopleActMath.Way(PeopleActKind.CallPact) == ActWay.Signal && PeopleActMath.Way(PeopleActKind.Levies) == ActWay.Signal, "a pact called in and a vassal's levies are called from home");
        C(PeopleActMath.Way(PeopleActKind.Renounce) == ActWay.Council && PeopleActMath.Way(PeopleActKind.FaceToFace) == ActWay.Face, "renouncing is the council's; talking is face to face");
        C(PeopleActMath.WayKey(ActWay.Letter) == null && PeopleActMath.WayKey(ActWay.Signal) == "CC_PplAct_Way_Signal", "a letter says the reach line, the rest their way");
        C(PeopleActMath.Alt(PeopleActKind.Truce, false, true) && !PeopleActMath.Alt(PeopleActKind.Truce, true, true), "a truce says it keeps raids away only when it does");
        C(PeopleActMath.Alt(PeopleActKind.Warning, true, false) && !PeopleActMath.Alt(PeopleActKind.Warning, true, true), "a warning says it makes a debt of honor only where debts are kept");
        C(!PeopleActMath.Alt(PeopleActKind.Pact, false, false) && PeopleActMath.ForKey(PeopleActKind.Pact, true) == "CC_PplAct_For_Pact", "an act with no other form keeps its line");
        C(PeopleActMath.ForKey(PeopleActKind.Truce, true) == "CC_PplAct_For_Truce_Alt", "the other form's key");
        C(PeopleActMath.CostKey(PeopleActKind.Greetings, true) == "CC_PplAct_Cost_Spoken" && PeopleActMath.CostKey(PeopleActKind.Greetings, false) == "CC_PplAct_Cost_Letter",
            "a kind letter costs a writer's hour, or a word at the gate in a tribe");
        C(PeopleActMath.CostKey(PeopleActKind.Help, false) == "CC_PplAct_Cost_Help" && PeopleActMath.CostKey(PeopleActKind.TakeBack, false) == null, "costs by the act");
        // The lines' numbers are the code's own (checked against the rules the acts follow).
        C(EnvoyMath.EndRegard(EnvoyMood.Content) == 12, "an envoy's good stay: 12 of their head's regard, as its line says");
        C(WardMath.HomeRegard(WardResult.Fine) == 20, "a ward's fine year: 20 of their head's regard, as its line says");
        C(Math.Abs(PactMath.CartPayShare - 0.8f) < 0.001f, "a trade pact's cart paid four fifths of its worth, as its cost says");
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Condolences, 1f, false) > NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, false),
            "condolences warm their regard more than any other letter");
        C(NotableMath.GoodwillShift(AnswerTone.Warm, LetterPurpose.Warning, false) > NotableMath.GoodwillShift(AnswerTone.Warm, LetterPurpose.Greetings, false),
            "a warning heeded raises their goodwill more than kind words");
        C(LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Warm) == AnswerBrings.Token && LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Cordial) == AnswerBrings.TownNews
          && LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Cool) == AnswerBrings.TownNews, "greetings: a gift with a warm answer, their town's news with a cordial or cool one");
        C(LetterMath.Brings(LetterPurpose.Warning, AnswerTone.Warm) == AnswerBrings.FavourOwed && LetterMath.Brings(LetterPurpose.Warning, AnswerTone.Cordial) == AnswerBrings.Remembered,
            "a warning: a debt of honor heeded warmly, remembered otherwise");
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Help, 1f, false) < NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.News, 1f, false)
          && NotableMath.OpinionShift(AnswerTone.Refusal, LetterPurpose.Help, 1f, false) < 0f, "asking for help costs a little of their regard, granted or not");
        C(NotableMath.Grants(new LetterInput { hostile = false }, AnswerTone.Cordial) && !NotableMath.Grants(new LetterInput { hostile = false }, AnswerTone.Cool),
            "help and visits are granted by a warm or cordial answer");
    }

    // ---------------- The words ----------------

    private static void Words(string root, Func<string, string> Code, Action<bool, string> C)
    {
        var path = Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesScreen.xml");
        var mine = XDocument.Load(path).Root!.Elements().ToList();
        var keys = new HashSet<string>(mine.Select(e => e.Name.LocalName));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (PeopleActKind k in Enum.GetValues(typeof(PeopleActKind)))
        {
            if (k == PeopleActKind.Other) continue;
            C(keys.Contains(PeopleActMath.ForKey(k, false)), $"what {k} is for, in words");
            if (PeopleActMath.HasAlt(k)) C(keys.Contains(PeopleActMath.ForKey(k, true)), $"{k}'s other form, in words");
            string cost = PeopleActMath.CostKey(k, false);
            if (cost != null) C(keys.Contains(cost), $"what {k} costs, in words");
        }
        C(keys.Contains("CC_PplAct_Cost_Spoken"), "a spoken message's cost, in words");
        foreach (ActWay w in Enum.GetValues(typeof(ActWay)))
        {
            string k = PeopleActMath.WayKey(w);
            if (k != null) C(keys.Contains(k), $"how a {w} act travels, in words");
        }
        foreach (ActGroup g in Enum.GetValues(typeof(ActGroup))) C(keys.Contains(PeopleActMath.GroupKey(g)), $"the {g} section's heading");
        foreach (LetterOccasion o in Enum.GetValues(typeof(LetterOccasion)))
            if (o != LetterOccasion.None && o != LetterOccasion.Own)
                C(keys.Contains("CC_Occ_" + o), $"the occasion {o}, in words");
        foreach (var p in PeopleActMath.Letters)
            if (p != LetterPurpose.News)
                C(keys.Contains(LetterOccasionMath.Key(p, LetterOccasion.None)), $"no reason now for {p}, with what would bring one");
        foreach (var e in mine)
        {
            string v = e.Value;
            C(!v.Contains('—') && !v.Contains('!'), $"{e.Name.LocalName}: no em dash, no exclamation");
            C(!Regex.IsMatch(v, @"\b(honour|favour|neighbour|colour)", RegexOptions.IgnoreCase), $"{e.Name.LocalName}: American spelling, like the game");
            C(!Regex.IsMatch(v, @"\bin \d+ (days?|hours?)\b"), $"{e.Name.LocalName}: no countdowns");
        }
        foreach (var s in new[] { "Closest", "Regard", "Name", "Age" }) C(keys.Contains("CC_PplV_Sort_" + s) && keys.Contains("CC_PplV_Sort_" + s + "_Tip"), $"the sort {s}, with its tip");
        foreach (var f in PeoplesViewMath.Filters) C(keys.Contains("CC_PplV_Filter_" + f) && keys.Contains("CC_PplV_Filter_" + f + "_Tip"), $"the filter {f}, with its tip");
        foreach (var g in PeoplesViewMath.Groups) C(keys.Contains("CC_PplV_Group_" + g), $"the group {g}");
        foreach (var t in PeoplesViewMath.Tabs) C(keys.Contains("CC_PplP_Tab_" + t) && keys.Contains("CC_PplP_Tab_" + t + "_Tip"), $"the tab {t}, with its tip");
        foreach (var st in new[] { "Own", "Allies", "Friendly", "Fair", "Cool", "Unfriendly", "Hostile", "Enemies", "NoDealings" }) C(keys.Contains("CC_PplV_Std_" + st), $"the stance {st} in a word");
        // Every key the new files name has its words.
        foreach (var file in new[] { "UI/Region/PeoplesView.cs", "UI/Region/PeoplesList.cs", "UI/Region/PeoplePage.cs", "UI/Region/PeopleActs.cs", "UI/Region/PeoplesModel.cs", "World/LetterOccasions.cs" })
            foreach (Match m in Regex.Matches(Code(file), "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(keyedAll.Contains("<" + k + ">"), $"{file}: {k} has its words");
            }
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C)
    {
        string view = Code("UI/Region/PeoplesView.cs"), list = Code("UI/Region/PeoplesList.cs"), page = Code("UI/Region/PeoplePage.cs"), acts = Code("UI/Region/PeopleActs.cs");
        string model = Code("UI/Region/PeoplesModel.cs");
        C(view.Contains("PeoplesList.Draw(") && view.Contains("PeoplePage.Draw(") && view.Contains("PeoplesViewMath.ListWidth(body.width)"), "the view: the list beside the page, by the layout's rule");
        C(model.Contains("now - builtAt > 1f") && model.Contains("PostReach.Of(home, t.s, true)") && model.Contains("PostReach.Kept(home, t.s)"),
            "the model at most once a second; each town's days by the post's reach rule, worked out a couple a frame");
        C(model.Contains("RegionTowns.Reach") && model.Contains("PostBook.Instance") && model.Contains("FavourAsks.Waiting()"), "neighbors by the region's reach; mail from the post, their calls and the letter stack");
        C(model.Contains("FactionMemory.Text(d)") && model.Contains("GenDate.TicksPerSeason"), "this season's trend and its reasons from what they remember");
        C(list.Contains("Widgets.TextField(") && list.Contains("UiKit.FilterChip(") && list.Contains("UiKit.Modes(r, labels, (int)RegionView.sort"), "the list's search, chips and sort");
        C(list.Contains("PeoplesViewMath.WebFixed(rect.height)") && list.Contains("a.f.RelationWith(b.f, true)"), "the web's place by the layout's rule, its lines from the game's relations");
        C(page.Contains("DiplomacyPanels.PeopleBody(") && page.Contains("DiplomacyPanels.Owed(y, w, dip, folds)") && page.Contains("FactionMemoryPanel.Draw(y, w)"),
            "Dealings is diplomacy's own body; all peoples has the owed strip and their memories");
        C(page.Contains("Cards.ShowOnMap(t.s)") && page.Contains("PostRing.Ring("), "a town's row shows it on the region map, its range ring from the post's hook");
        C(page.Contains("WorldHistoryPage.ShowFor(Refs.Of(e.f), Ref.None)") && page.Contains("History.Of(Refs.Of(e.f)"), "History: their timeline, and their whole history a click on");
        C(page.Contains("Peoples.Of(f)") && page.Contains("FeastWords.Hope(Feasts.HopeOf(f))") && page.Contains("Warnings.Threats(f)"),
            "what they want and fear from what Rimfolk knows: their template, their feast's hope, the threats known");
        C(acts.Contains("CardActs.registry.In(kind, CardSlot.Acts)") && acts.Contains("a.act(th)") && acts.Contains("a.whyNot?.Invoke(th)"),
            "every card act of the target through its own code path, greyed with its own reason");
        C(acts.Contains("DiplomacyModel.CallAct(t, home)") && acts.Contains("DiplomacyModel.AnswerAct(t)"), "debts called in and answered as the diplomacy model does");
        C(acts.Contains("new Dialog_NeighbourLetter(n, p, topic)") && acts.Contains("LetterOccasions.WhyNot(to, p)"), "a letter by occasion opens the letter's own window, set to it");
        foreach (var id in PeopleActMath.ById.Keys)
        {
            bool found = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories)
                .Any(f => !f.EndsWith("PeopleActMath.cs") && File.ReadAllText(f).Contains("\"" + id + "\""));
            C(found, $"the card act {id} is registered somewhere");
        }
        string dialog = Code("World/NeighboursPanel.cs");
        C(dialog.Contains("whyNot[p] = LetterOccasions.WhyNot(to, p)") && dialog.Contains("LetterOccasions.Reason(to, p)"), "the letter's window greys a kind letter for the same reason and says the occasion");
        C(!Code("World/NeighbourLetters.cs").Contains("LetterOccasions"), "the letters' own checks are unchanged (a feast's invitations still go by them)");
        string occ = Code("World/LetterOccasions.cs");
        C(occ.Contains("Feasts.At(home)") && occ.Contains("ColonyCalendar.For(home)") && occ.Contains("GuestBeds.IsGuestBed(b)") && occ.Contains("MapFacts.Of(home)")
          && occ.Contains("NeighbourLetters.Topics(n, LetterPurpose.Condolences)") && occ.Contains("GenLocalDate.DayOfYear(home)"),
            "the occasions from what Rimfolk tracks: the feast, the calendar, the guest beds, the stores, the letters' topics, the date");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool letterOccasions = true;") && settings.Contains("Scribe_Values.Look(ref letterOccasions, \"letterOccasions\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"letterOccasions\""), "the setting Letters by occasion, on by default");
        C(Code("UI/RegionMapView.cs").Contains("RegionView.lit") && Code("UI/Region/MapView.cs").Contains("RegionViewsMath.CompareClosest("), "the region map lights the chosen people's towns; its List sorts closest first");
        var rows = new List<RegionViewsMath.RowOrder> { new RegionViewsMath.RowOrder(2, 0f, 3f, "near"), new RegionViewsMath.RowOrder(0, 0f, 9f, "far"), new RegionViewsMath.RowOrder(1, 0f, 5f, "mid") };
        rows.Sort(RegionViewsMath.CompareClosest);
        C(rows.Select(r => r.name).SequenceEqual(new[] { "near", "mid", "far" }), "the map's List closest first, whatever the group");
        C(Code("UI/Cards/Cards.cs").Contains("PeoplesView.Choose(f)"), "a people's card opened in full is its page on Peoples");
        C(PagesMath.TryAddress("Peoples", out var a1) && a1.page == "Peoples" && PagesMath.TryAddress("Peoples/Faces", out var a2) && a2.mode == "Faces"
          && PagesMath.TryAddress("Diplomacy", out var a3) && a3.page == "Peoples", "the old addresses still land on Peoples and its Faces");
        C(Code("UI/Region/RegionView.cs").Contains("public static PeoplesSort sort = PeoplesSort.Closest;") && !Regex.IsMatch(Code("UI/Region/RegionView.cs"), @"Reset\(\)[\s\S]{0,300}sort ="),
            "the sort is the player's for the session, closest first until they choose another");
    }
}
