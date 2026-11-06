using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// A fresh start in the same world (Colonies/FoundingMath.cs; docs/design/stage3-many-colonies.md, release 1): each
/// grown colonist's wish and what goes against it, who may be sent and the party's checks, the newcomers' price and
/// their once a year, the seat once a season, the game's colony limit raised by one and set back, the founding kit by
/// age, close bonds parted, the colonies' record and its bound, a council carried, and the code's seams.
/// </summary>
public static class FoundingTests
{
    private sealed class Entry : IColonyEntry
    {
        public int Home { get; set; } = HomesMath.None;
        public int LostAt { get; set; } = ColonyStateMath.NotLost;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "founding: " + what);
        }

        const int Day = FoundingMath.DayTicks;

        // ---------------- Each one's wish ----------------

        C(FoundingMath.WishOf(new FoundingTies()) == FoundingWish.Torn, "nothing either way: torn");
        C(FoundingMath.WishOf(new FoundingTies { kinGoing = 1 }) == FoundingWish.Go, "their spouse or child goes: they want to go");
        C(FoundingMath.WishOf(new FoundingTies { kinStaying = 1 }) == FoundingWish.Stay, "their own stay: they want to stay");
        C(FoundingMath.WishOf(new FoundingTies { kinGoing = 1, kinStaying = 1 }) == FoundingWish.Torn, "their own on both sides: torn");
        C(FoundingMath.WishOf(new FoundingTies { atOdds = true }) == FoundingWish.Go, "at odds with most here: a reason to go");
        C(FoundingMath.WishOf(new FoundingTies { office = true, ownRoom = true }) == FoundingWish.Stay, "a seat on the council and a room of their own hold them");
        C(FoundingMath.WishOf(new FoundingTies { rivalsGoing = 2 }) == FoundingWish.Stay && FoundingMath.WishOf(new FoundingTies { rivalsStaying = 2 }) == FoundingWish.Go,
            "rivals push them the other way");
        C(FoundingMath.WishOf(new FoundingTies { friendsGoing = 1 }) == FoundingWish.Torn, "one friend going isn't enough on its own");
        C(FoundingMath.WishOf(new FoundingTies { friendsGoing = 1, young = true }) == FoundingWish.Go, "a friend going and youth: they want to go");
        C(FoundingMath.WishOf(new FoundingTies { elder = true, friendsStaying = 1 }) == FoundingWish.Stay, "old age and a friend staying: they want to stay");
        var why = FoundingMath.Reasons(new FoundingTies { kinGoing = 1, friendsStaying = 1, elder = true });
        C(why.Count == 3 && why[0] == "KinGoing", "the reasons, the strongest first: " + string.Join(",", why));
        C(FoundingMath.Reasons(new FoundingTies()).Count == 0, "no reasons for no ties");

        C(FoundingMath.AgainstWish(FoundingWish.Stay, true) && FoundingMath.AgainstWish(FoundingWish.Go, false), "sent wanting to stay, kept wanting to go: against their wish");
        C(!FoundingMath.AgainstWish(FoundingWish.Torn, true) && !FoundingMath.AgainstWish(FoundingWish.Torn, false), "the torn go either way");
        C(!FoundingMath.AgainstWish(FoundingWish.Go, true) && !FoundingMath.AgainstWish(FoundingWish.Stay, false), "as they wished: nothing");
        C(FoundingMath.UprootedDays == 15 && FoundingMath.PartedDays == 15, "uprooted and parted for a season");

        C(!FoundingMath.AskMoveDue(2 * Day, 0, false) && FoundingMath.AskMoveDue(3 * Day, 0, false) && FoundingMath.AskMoveDue(14 * Day, 0, false),
            "asking to move comes from the third day");
        C(!FoundingMath.AskMoveDue(15 * Day, 0, false) && FoundingMath.AskMoveLapsed(15 * Day, 0), "and not once the season is over");
        C(!FoundingMath.AskMoveDue(5 * Day, 0, true), "asked once");

        C(FoundingMath.Close(true, false, false, 0) && FoundingMath.Close(false, true, false, -20) && FoundingMath.Close(false, false, true, 0),
            "a partner, a parent or child, a sibling: close whatever they think");
        C(FoundingMath.Close(false, false, false, 60) && !FoundingMath.Close(false, false, false, 59), "a friend close from an opinion of 60");

        // ---------------- Who may be sent ----------------

        C(FoundingMath.CantSend(true, false, false, false, true) == null, "one of this home's people at home may go");
        C(FoundingMath.CantSend(false, false, false, false, true) == "NotOurs", "not of this home");
        C(FoundingMath.CantSend(true, true, false, false, true) == "Lodger", "a lodger of another people isn't the colony's to send");
        C(FoundingMath.CantSend(true, false, true, false, true) == "Leaving", "one being banished is leaving, and in no party");
        C(FoundingMath.CantSend(true, false, false, true, true) == "OtherJourney", "one already planned on another journey");
        C(FoundingMath.CantSend(true, false, false, false, false) == "Away", "one away from home now");

        C(FoundingMath.PartyCheck(0, 0, 5, true, false, true, FoundingMeans.Caravan) == "Nobody", "nobody goes and no newcomers");
        C(FoundingMath.PartyCheck(0, 2, 5, true, false, true, FoundingMeans.Caravan) == "ChildrenOnly", "children alone don't found a colony");
        C(FoundingMath.PartyCheck(3, 0, 0, true, false, true, FoundingMeans.Caravan) == "NobodyStays", "the old colony kept needs a grown colonist left in it");
        C(FoundingMath.PartyCheck(3, 0, 0, false, false, true, FoundingMeans.Caravan) == null, "abandoned, everyone may go");
        C(FoundingMath.PartyCheck(0, 0, 5, true, true, false, FoundingMeans.Newcomers) == "NewcomersNotYet", "newcomers not yet this year");
        C(FoundingMath.PartyCheck(0, 0, 5, true, true, true, FoundingMeans.Newcomers) == null, "newcomers alone found it");
        C(FoundingMath.PartyCheck(2, 1, 3, true, true, true, FoundingMeans.Pods) == null, "a party and newcomers both");
        C(FoundingMath.MeansOf(0, FoundingMeans.Caravan) == FoundingMeans.Newcomers && FoundingMath.MeansOf(3, FoundingMeans.Newcomers) == FoundingMeans.Caravan
          && FoundingMath.MeansOf(3, FoundingMeans.Gravship) == FoundingMeans.Gravship, "nobody from the colony: newcomers only; a party goes by the means chosen");
        C(FoundingMath.AtPlace(0f) && FoundingMath.AtPlace(2f) && !FoundingMath.AtPlace(3f) && !FoundingMath.AtPlace(-1f), "a home within two tiles of the place is the founding's");

        // ---------------- The newcomers' price ----------------

        C(FoundingMath.Free(true) && !FoundingMath.Free(false), "free only with the old colony abandoned in the same act");
        C(FoundingMath.Passage(new[] { 400f, 90f }, true) == 0, "free costs nothing");
        C(FoundingMath.Passage(90f) == FoundingMath.MinPassage && FoundingMath.Passage(400f) == 400 && FoundingMath.Passage(404f) == 400,
            "a passage is about the kit's worth, at least the least");
        C(FoundingMath.Passage(new[] { 400f, 90f }, false) == 400 + FoundingMath.MinPassage, "the passage of several adds up");
        C(FoundingMath.PassageNamed(3, false) == 3 * FoundingMath.MinPassage && FoundingMath.PassageNamed(3, true) == 0, "the price named before they are chosen");
        C(FoundingMath.Pay(300, 500, 500f, out int s1, out float f1) && Math.Abs(f1 - 150f) < 0.01f && s1 == 150, "half in food, half in silver");
        C(FoundingMath.Pay(300, 100, 500f, out int s2, out float f2) && s2 == 100 && Math.Abs(f2 - 200f) < 0.01f, "short of silver: more food in its place");
        C(FoundingMath.Pay(300, 400, 0f, out int s3, out float f3) && s3 == 300 && f3 == 0f, "no food: all in silver");
        C(!FoundingMath.Pay(300, 100, 100f, out int s4, out float f4) && s4 == 0 && f4 == 0f, "the stores can't cover it: nothing is paid");
        C(FoundingMath.Pay(0, 0, 0f, out _, out _), "free is always paid");

        C(FoundingMath.NewcomersMayCome(true, 10 * Day, 5 * Day), "abandoning: newcomers whenever");
        C(FoundingMath.NewcomersMayCome(false, 10 * Day, -1) && !FoundingMath.NewcomersMayCome(false, 59 * Day, 0) && FoundingMath.NewcomersMayCome(false, 60 * Day, 0),
            "with the old colony kept: once a year");
        C(FoundingMath.NewcomersNext(10 * Day, 0) == 60 * Day && FoundingMath.NewcomersNext(61 * Day, 0) == -1, "when they may come next");

        // ---------------- The seat ----------------

        C(FoundingMath.SeatWhyNot(1, false, true, 0, -1) == "OneColony", "one colony has nothing to move");
        C(FoundingMath.SeatWhyNot(2, true, true, 0, -1) == "Already", "the seat already");
        C(FoundingMath.SeatWhyNot(2, false, false, 0, -1) == "Orbit", "a home in orbit can't be the seat");
        C(FoundingMath.SeatWhyNot(2, false, true, 14 * Day, 0) == "ThisSeason" && FoundingMath.SeatWhyNot(2, false, true, 15 * Day, 0) == null, "at most once a season");
        C(FoundingMath.SeatWhyNot(3, false, true, 100, -1) == null, "a home on the ground of several, never moved: yes");
        C(FoundingMath.SeatNext(5 * Day, 0) == 15 * Day && FoundingMath.SeatNext(20 * Day, 0) == -1 && FoundingMath.SeatNext(0, -1) == -1, "when it may move next");
        C(FoundingMath.SeatTrust < 0f && FoundingMath.SeatTrust > -10f, "the old seat's people trust the council a little less");

        // ---------------- The game's colony limit ----------------

        C(FoundingMath.LimitFor(3, 1) == 3, "room under the setting: left as it is");
        C(FoundingMath.LimitFor(1, 1) == 2 && FoundingMath.LimitFor(4, 4) == 5, "full: raised by one");
        C(FoundingMath.LimitFor(5, 5) == -1, "never past the game's five");
        C(FoundingMath.LimitFor(1, 2) == -1, "one more isn't enough: not raised");
        for (int pref = 1; pref <= 5; pref++)
        for (int counted = 0; counted <= 6; counted++)
        {
            int to = FoundingMath.LimitFor(pref, counted);
            C(to == -1 || to == pref || to == pref + 1, $"raised by one at most ({pref}, {counted})");
            C(to == -1 || to <= FoundingMath.GameMax, $"never past five ({pref}, {counted})");
            C(to == -1 || counted < to, $"room for the new colony when it goes ahead ({pref}, {counted})");
        }
        C(FoundingMath.LimitBack(2, 2, 1) == 1, "abandoned: back down once the old is gone");
        C(FoundingMath.LimitBack(3, 2, 1) == 3, "the player changed it: left as it is");
        C(FoundingMath.LimitBack(2, 2, 2) == 2, "both homes still there: left as it is");
        C(FoundingMath.LimitBack(2, -1, 1) == 2, "never raised: left as it is");

        // ---------------- The founding kit ----------------

        var tribe = FoundingMath.Shelter(0, 4);
        var medieval = FoundingMath.Shelter(1, 4);
        var industry = FoundingMath.Shelter(2, 4);
        var spacer = FoundingMath.Shelter(3, 4);
        C(tribe.wood > 0 && tribe.steel == 0 && tribe.components == 0 && medieval.wood == tribe.wood, "a tribe and a medieval colony raise their first shelter of wood");
        C(industry.steel > 0 && industry.components > 0 && spacer.steel == industry.steel, "industry and beyond: steel and components");
        C(FoundingMath.Shelter(0, 8).wood > tribe.wood && FoundingMath.Shelter(2, 8).steel > industry.steel, "more for a bigger party");
        C(FoundingMath.KitMedicine(1) == 1 && FoundingMath.KitMedicine(4) == 2 && FoundingMath.KitMedicine(5) == 3, "medicine for the first season");
        C(FoundingMath.KitFoodDays == 15, "food for fifteen days besides the road");
        C(FoundingMath.FoodDaysTried[0] == FoundingMath.KitFoodDays && FoundingMath.FoodDaysTried.Last() == 0
          && FoundingMath.FoodDaysTried.Zip(FoundingMath.FoodDaysTried.Skip(1), (a, b) => a > b).All(x => x), "fewer days of food tried in turn, down to the road's alone");
        C(FoundingMath.LoadShare > 0.5f && FoundingMath.LoadShare <= 0.9f, "the kit never fills the room past the planner's own warning");

        // ---------------- The colonies' record ----------------

        var ended = Enumerable.Repeat(-1, FoundingMath.MaxRecords).ToList();
        C(FoundingMath.RecordToFold(ended) == -1, "24 records fit");
        ended.Add(-1);
        C(FoundingMath.RecordToFold(ended) == -1, "past 24 with every colony standing: nothing to fold");
        ended[3] = 500;
        ended[7] = 200;
        C(FoundingMath.RecordToFold(ended) == 7, "past 24: the oldest ended one folds");

        // ---------------- A council carried ----------------

        var book = new List<Entry> { new Entry { Home = 10, LostAt = 400 }, new Entry { Home = 20 } };
        var old = book[0];
        C(FoundingMath.CarryCouncil(book, 10, 20) && book.Count == 1 && book[0] == old && old.Home == 20 && old.LostAt == ColonyStateMath.NotLost,
            "the old colony abandoned in the same act: its council goes to the new home in place of the new one's");
        C(!FoundingMath.CarryCouncil(book, 99, 20) && book.Count == 1, "no council to carry: nothing changes");

        // ---------------- The seams and the game's calls ----------------

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string found = Code("Colonies/Foundings.cs");
        C(found.Contains("SettleInEmptyTileUtility.Settle(") && found.Contains("SettleUtility.AddNewHome(") && found.Contains("GetOrGenerateMapUtility.GetOrGenerateMap("),
            "a colony is settled the game's own way");
        C(found.Contains("Prefs.MaxNumberOfPlayerSettlements") && found.Contains("FoundingMath.LimitFor(") && found.Contains("FoundingMath.LimitBack("),
            "the game's limit raised by the rule and set back by it");
        C(found.Contains(".Abandon(false)") && found.Contains("CheckOrUpdateGameOver"), "the old colony abandoned the game's way");
        C(found.Contains("HandOver") && found.Contains("Release 2"), "the seam for the hand-over (release 2)");
        string window = Code("Colonies/NewcomersWindow.cs");
        C(window.Contains("Current.Game.InitData = null") && window.Contains("ClearAllStartingPawns") && window.Contains("PostClose"),
            "the newcomers' window clears the game's start data however it closes");
        C(window.Contains("StartingPawnUtility.DrawPortraitArea(") && window.Contains("StartingPawnUtility.DrawSkillSummaries("), "the game's own portrait area and skills summary");
        C(window.Contains("Scenario") && window.Contains("Release 3"), "the seam for a scenario's start (release 3)");
        string seat = Code("Colonies/ColonySeat.cs");
        C(seat.Contains("FoundingMath.SeatWhyNot(") && seat.Contains("Homes.SetSeat(") && seat.Contains("FoundingMath.SeatTrust"), "the seat moved by the rule, with the trust it costs");
        return n;
    }
}
