using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The diplomacy screen's step B (docs/design/diplomacy-screen.md, points 6 to 13) and the letters' step 2, points 12 to
/// 14 (docs/design/post-and-letters.md): Source/ColonyCouncil/Dealings/DealingMath.cs. Each dealing's terms; what is open
/// and why not; the answer by regard and ways; the road's effect on days; pen friends' thresholds; the world's letter rate
/// (simulated); the chain of neighbours; and the seams: the letter purpose, the acts, the words, the setting, the save.
/// </summary>
public static class DealingTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "dealings: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        Kinds(C);
        Open(C);
        Answers(C);
        Terms(C);
        PenFriendsRules(C);
        WorldRate(C);
        Chain(C);
        Words(root, Code, C);
        Seams(root, Code, C);
        return n;
    }

    private static DealFacts Ok() => new DealFacts
    {
        settingOn = true, goodwill = 30, regard = 20, surplus = true, silver = true, hardTimes = true, relief = true, raidedLately = true, wrongedLately = true,
        refugeesHere = true, crops = true, campRoom = true, gathering = true, enemies = true, feastsOn = true, missionsOn = true, campsOn = true, guestsOk = true
    };

    private static IEnumerable<(DealKind k, byte s)> All()
    {
        foreach (var k in DealingMath.Kinds)
            for (byte s = 0; s < DealingMath.Subs(k); s++)
                yield return (k, s);
    }

    // ---------------- The kinds ----------------

    private static void Kinds(Action<bool, string> C)
    {
        C(DealingMath.Kinds.Length == 8 && !DealingMath.Kinds.Contains(DealKind.Reply), "eight dealings, one for each of the design's points 6 to 13");
        C(All().Count() == 19, "nineteen in all with their kinds (two contracts, three proclamations, two hires, four petitions, four works, two invitations, relief, market rights)");
        foreach (var (k, s) in All())
        {
            string key = DealingMath.TopicKey(k, s, 1234);
            C(DealingMath.ParseTopic(key, out var k2, out byte s2, out int t) && k2 == k && s2 == s && t == 1234, $"{DealingMath.SubName(k, s)}: its letter's topic read back");
            C(DealingMath.ParseTopic(DealingMath.TopicKey(k, s, -1), out _, out _, out int t2) && t2 == -1, $"{DealingMath.SubName(k, s)}: with the people as a whole");
        }
        C(!DealingMath.ParseTopic("deal:Supply:7:12", out _, out _, out _) && !DealingMath.ParseTopic("pact:trade:3", out _, out _, out _) && !DealingMath.ParseTopic(null, out _, out _, out _),
            "a topic that is no dealing's, or a kind out of range, is not read as one");
        C(DealingMath.TownOnly(DealKind.Supply) && DealingMath.TownOnly(DealKind.Work) && DealingMath.TownOnly(DealKind.Market) && !DealingMath.TownOnly(DealKind.Proclaim)
          && !DealingMath.TownOnly(DealKind.Petition), "contracts, works, stalls with a town; proclamations and petitions to a people as a whole too");
        C(DealingMath.SubName(DealKind.Work, (byte)WorkKind.Tower) == "Work_Tower" && DealingMath.SubName(DealKind.Relief, 0) == "Relief", "the sub-kinds' names for their words");
    }

    // ---------------- What is open, and why not ----------------

    private static void Open(Action<bool, string> C)
    {
        foreach (var (k, s) in All()) C(DealingMath.WhyNot(k, s, Ok()) == null, $"{DealingMath.SubName(k, s)}: open when everything is there for it");
        foreach (var (k, s) in All())
        {
            var f = Ok();
            f.settingOn = false;
            C(DealingMath.WhyNot(k, s, f) == "Off", $"{DealingMath.SubName(k, s)}: not with the setting off");
            f = Ok();
            f.noOne = true;
            C(DealingMath.WhyNot(k, s, f) == "NoOne", $"{DealingMath.SubName(k, s)}: nobody of theirs known to receive it");
            f = Ok();
            f.already = true;
            C(DealingMath.WhyNot(k, s, f) == "Already", $"{DealingMath.SubName(k, s)}: one at a time of each with them");
            f = Ok();
            f.lately = true;
            C(DealingMath.WhyNot(k, s, f) == "Lately", $"{DealingMath.SubName(k, s)}: not soon after they turned it down");
            f = Ok();
            f.hostile = true;
            bool heard = k == DealKind.Petition && (s == (byte)PetitionKind.Raiders || s == (byte)PetitionKind.Reparations);
            C(heard ? DealingMath.WhyNot(k, s, f) == null : DealingMath.WhyNot(k, s, f) == "War", $"{DealingMath.SubName(k, s)}: at war, {(heard ? "still heard" : "not asked")}");
        }
        DealFacts F(Func<DealFacts, DealFacts> with) => with(Ok());
        C(DealingMath.WhyNot(DealKind.Supply, (byte)SupplyWay.Ours, F(x => { x.surplus = false; return x; })) == "NoSurplus", "the colony delivers only what its stores can spare");
        C(DealingMath.WhyNot(DealKind.Supply, (byte)SupplyWay.Theirs, F(x => { x.silver = false; return x; })) == "NoSilver", "they deliver only for silver the colony has");
        C(DealingMath.WhyNot(DealKind.Relief, 0, F(x => { x.hardTimes = false; return x; })) == "NoHardTimes", "relief only for hard times the colony has heard of");
        C(DealingMath.WhyNot(DealKind.Relief, 0, F(x => { x.relief = false; return x; })) == "NoRelief", "relief only with medicine or food to send");
        C(DealingMath.WhyNot(DealKind.Relief, 0, F(x => { x.goodwill = -50; return x; })) == null, "relief even for a people that thinks little of the colony");
        C(DealingMath.WhyNot(DealKind.Supply, 0, F(x => { x.goodwill = -5; return x; })) == "Goodwill", "no dealing with a people that thinks too little of the colony");
        C(DealingMath.WhyNot(DealKind.Proclaim, (byte)ProclaimKind.Friendship, F(x => { x.goodwill = DealingMath.FriendshipGoodwill - 1; return x; })) == "FriendshipGoodwill"
          && DealingMath.WhyNot(DealKind.Proclaim, (byte)ProclaimKind.Fair, F(x => { x.goodwill = 0; return x; })) == null, "friendship declared needs goodwill; a fair day doesn't");
        C(DealingMath.WhyNot(DealKind.Hire, (byte)HireKind.Crew, F(x => { x.crops = false; return x; })) == "NoCrops"
          && DealingMath.WhyNot(DealKind.Hire, (byte)HireKind.Guard, F(x => { x.crops = false; return x; })) == null, "harvest hands only with crops to reap; guards any time");
        C(DealingMath.WhyNot(DealKind.Hire, 0, F(x => { x.campRoom = false; return x; })) == "NoCampRoom" && DealingMath.WhyNot(DealKind.Hire, 0, F(x => { x.campsOn = false; return x; })) == "NoCamps",
            "a hire needs a camp that can be pitched, with the camps on");
        C(DealingMath.WhyNot(DealKind.Petition, (byte)PetitionKind.Raiders, F(x => { x.raidedLately = false; return x; })) == "NotRaided", "a petition about their raiders after a raid of theirs");
        C(DealingMath.WhyNot(DealKind.Petition, (byte)PetitionKind.Reparations, F(x => { x.wrongedLately = false; return x; })) == "NotWronged", "amends for a wrong of theirs");
        C(DealingMath.WhyNot(DealKind.Petition, (byte)PetitionKind.Settle, F(x => { x.refugeesHere = false; return x; })) == "NoRefugees", "refugees settled only when a refugee family is camped here");
        C(DealingMath.WhyNot(DealKind.Petition, (byte)PetitionKind.TakeIn, F(x => { x.hardTimes = false; return x; })) == "NoHardTimes", "a family of theirs taken in when they're in hard times");
        C(DealingMath.WhyNot(DealKind.Petition, (byte)PetitionKind.Reparations, F(x => { x.goodwill = -80; return x; })) == null, "a petition is heard whatever they think of the colony");
        C(DealingMath.WhyNot(DealKind.Work, (byte)WorkKind.Tower, F(x => { x.enemies = false; return x; })) == "NoEnemies", "a watchtower when the colony has an enemy");
        C(DealingMath.WhyNot(DealKind.Work, (byte)WorkKind.Road, F(x => { x.built = true; return x; })) == "Built", "a work built once with each town");
        C(DealingMath.WhyNot(DealKind.Work, (byte)WorkKind.Well, F(x => { x.surplus = false; return x; })) == "NoShare", "the colony brings its share or nothing is built");
        C(DealingMath.WhyNot(DealKind.Market, 0, F(x => { x.goodwill = DealingMath.MarketGoodwill - 1; return x; })) == "MarketGoodwill", "market rights need some goodwill");
        C(DealingMath.WhyNot(DealKind.Market, 0, F(x => { x.silver = false; return x; })) == "NoSilver", "market rights need the fee");
        C(DealingMath.WhyNot(DealKind.Invite, (byte)InviteKind.Feast, F(x => { x.gathering = false; return x; })) == "NoGathering", "a town invited to a gathering that is coming");
        C(DealingMath.WhyNot(DealKind.Invite, (byte)InviteKind.Entertainers, F(x => { x.gathering = false; return x; })) == null, "the entertainers go to their festival whatever the colony holds");
        C(DealingMath.WhyNot(DealKind.Supply, 0, F(x => { x.unreachable = true; return x; })) == "Unreachable", "not to a town nothing reaches");
    }

    // ---------------- Their answer ----------------

    private static void Answers(Action<bool, string> C)
    {
        var plain = new DealWays { calling = null, age = WorldAge.Medieval };
        foreach (var (k, s) in All())
        {
            float low = DealingMath.Chance(k, s, -60, -20, plain), mid = DealingMath.Chance(k, s, 0, 0, plain), high = DealingMath.Chance(k, s, 60, 60, plain);
            C(low <= mid && mid <= high && high > low, $"{DealingMath.SubName(k, s)}: likelier the better they think of the colony");
            C(DealingMath.Chance(k, s, -100, -100, plain) >= 0.05f && DealingMath.Chance(k, s, 100, 100, new DealWays { calling = PeoplesMath.Traders }) <= 0.95f,
                $"{DealingMath.SubName(k, s)}: never certain either way");
            C(DealingMath.Agrees(k, s, 0, 0, plain, 0f) && !DealingMath.Agrees(k, s, 0, 0, plain, 0.999f), $"{DealingMath.SubName(k, s)}: decided by the roll against the chance");
        }
        var traders = new DealWays { calling = PeoplesMath.Traders, age = WorldAge.Medieval };
        var mercs = new DealWays { calling = PeoplesMath.Mercenaries, age = WorldAge.Medieval };
        var healers = new DealWays { calling = PeoplesMath.Healers, age = WorldAge.Medieval };
        var empire = new DealWays { age = WorldAge.Spacer, empire = true };
        C(DealingMath.Chance(DealKind.Supply, 0, 0, 0, traders) > DealingMath.Chance(DealKind.Supply, 0, 0, 0, plain), "traders like a contract");
        C(DealingMath.Chance(DealKind.Market, 0, 0, 0, traders) > DealingMath.Chance(DealKind.Market, 0, 0, 0, plain), "traders like a stall in their market");
        C(DealingMath.Chance(DealKind.Hire, (byte)HireKind.Guard, 0, 0, mercs) > DealingMath.Chance(DealKind.Hire, (byte)HireKind.Guard, 0, 0, plain) + 0.3f,
            "mercenaries hire out their fighters gladly");
        C(DealingMath.Chance(DealKind.Petition, (byte)PetitionKind.Settle, 0, 0, healers) > DealingMath.Chance(DealKind.Petition, (byte)PetitionKind.Settle, 0, 0, plain),
            "healers take in refugees more readily");
        C(DealingMath.Chance(DealKind.Relief, 0, 0, 0, empire) < DealingMath.Chance(DealKind.Relief, 0, 0, 0, plain) - 0.3f, "the Empire takes no charity from those beneath it");
        C(DealingMath.Chance(DealKind.Relief, 0, 0, 0, plain) > DealingMath.Chance(DealKind.Proclaim, (byte)ProclaimKind.Friendship, 0, 0, plain),
            "relief is taken more readily than friendship is answered");
        C(DealingMath.Chance(DealKind.Petition, (byte)PetitionKind.Reparations, 0, 0, plain) < 0.3f, "amends are seldom paid by those who don't think well of the colony");
    }

    // ---------------- The terms ----------------

    private static void Terms(Action<bool, string> C)
    {
        C(DealingMath.TheirLoadValue(WorldAge.Tribal) < DealingMath.TheirLoadValue(WorldAge.Medieval) && DealingMath.TheirLoadValue(WorldAge.Medieval) < DealingMath.TheirLoadValue(WorldAge.Industrial)
          && DealingMath.TheirLoadValue(WorldAge.Industrial) < DealingMath.TheirLoadValue(WorldAge.Spacer), "their loads grow with their age");
        C(DealingMath.TheirLoadCount(1.5f, WorldAge.Medieval) == 200 && DealingMath.TheirLoadCount(0f, WorldAge.Medieval) == 0 && DealingMath.TheirLoadCount(5000f, WorldAge.Tribal) == 1,
            "a load: about its worth in units, one at least");
        C(DealingMath.TheirLoadPay(2f, 100) == 220, "the colony pays eleven tenths of their goods' worth at the gate");
        C(DealingMath.Deliveries * DealingMath.SeasonDays == 60f, "a contract runs a year of four seasons");
        C(!DealingMath.Broken(1, 1) && DealingMath.Broken(2, 2) && DealingMath.Broken(1, 3) && !DealingMath.Broken(0, 2), "two missed in a row, or three in all, break it");
        C(DealingMath.BrokenGoodwill < 0 && DealingMath.KeptGoodwill > DealingMath.DeliveryGoodwill, "broken costs goodwill; kept to its end earns more than a season");
        C(DealingMath.ReliefGoods.All(g => DealingMath.ReliefCount(g) > 0) && DealingMath.ReliefCount("medicine") < DealingMath.ReliefCount("meals"), "relief's goods and amounts");
        C(DealingMath.Reparations(1, WorldAge.Medieval) == 100 && DealingMath.Reparations(500, WorldAge.Spacer) == 600 && DealingMath.Reparations(20, WorldAge.Medieval) == 240,
            "amends: 12 a point of their wrong, 100 to 600");
        C(DealingMath.Reparations(20, WorldAge.Tribal) < DealingMath.Reparations(20, WorldAge.Industrial), "amends by their age");
        C(DealingMath.WorkShare(WorkKind.Road, 30f) > DealingMath.WorkShare(WorkKind.Road, 5f) && DealingMath.WorkShare(WorkKind.Road, 100f) == 300, "a longer road asks a bigger share, 300 at most");
        C(DealingMath.WorkDays(WorkKind.Road, 4f) == 5f && DealingMath.WorkDays(WorkKind.Road, 100f) == 15f && DealingMath.WorkDays(WorkKind.Bridge, 1f) == 8f, "the work's days");
        C(DealingMath.ShareGoods(WorkKind.Road, WorldAge.Tribal) == "wood" && DealingMath.ShareGoods(WorkKind.Road, WorldAge.Industrial) == "steel"
          && DealingMath.ShareGoods(WorkKind.Well, WorldAge.Medieval) == "blocks", "the share by the age: wood early, steel later, stone for a well");
        C(DealingMath.RoadDef(WorldAge.Tribal) == "DirtPath" && DealingMath.RoadDef(WorldAge.Medieval) == "DirtRoad" && DealingMath.RoadDef(WorldAge.Industrial) == "StoneRoad"
          && DealingMath.RoadDef(WorldAge.Spacer) == "StoneRoad", "the road of the age: a path, a dirt road, a stone road");
        C(DealingMath.SharedAge(WorldAge.Industrial, WorldAge.Tribal) == WorldAge.Tribal && DealingMath.SharedAge(WorldAge.Medieval, WorldAge.Spacer) == WorldAge.Medieval,
            "the road by the earlier age of the two who build it");
        // The road's effect on days: the game's own road multiplier over the land's days, as the post's model counts it.
        C(DealingMath.OnRoad(10f, 0.5f) == 5f && DealingMath.OnRoad(10f, 1f) == 10f && DealingMath.OnRoad(10f, 0f) == 0.5f, "a road halves (or so) the days over it");
        C(PostMath.TileDays(1f, 0.5f, 1f, 1f) < PostMath.TileDays(1f, 1f, 1f, 1f) && PostMath.TileDays(2f, 0.3f, 1f, 1f) < PostMath.TileDays(2f, 0.5f, 1f, 1f),
            "the post's days over a tile with a road are fewer, a stone road fewer still");
        float off = PostMath.RouteDays(Enumerable.Repeat(PostMath.TileDays(1f, 1f, 1f, 1f), 20).ToList());
        float on = PostMath.RouteDays(Enumerable.Repeat(PostMath.TileDays(1f, 0.5f, 1f, 1f), 20).ToList());
        C(Math.Abs(on - DealingMath.OnRoad(off, 0.5f)) < 0.001f, "twenty tiles by a dirt road: half the days");
        C(PostMath.LegDays(on, 20f, LetterMeans.Rider) < PostMath.LegDays(off, 20f, LetterMeans.Rider), "and the post's rider there sooner");
        C(DealingMath.Offset(true, true, true) == DealingMath.MaxOffset && DealingMath.Offset(true, false, false) == DealingMath.MarketOffset && DealingMath.Offset(false, false, false) == 0f,
            "better terms from dealings, capped");
        C(DealingMath.MarketFee(WorldAge.Tribal) < DealingMath.MarketFee(WorldAge.Spacer), "a stall's fee by their age");
        C(DealingMath.FairIn(5) == 5 && DealingMath.FairIn(-1) == DealingMath.FairDefaultDays && DealingMath.FairIn(30) == DealingMath.FairDefaultDays && DealingMath.FairIn(1) == DealingMath.FairDefaultDays,
            "the fair on the calendar's gathering day when it comes within the window, else a few days on");
        C(DealingMath.WarnHours(0f) == DealingMath.WarnHoursMin && DealingMath.WarnHours(0.999f) == DealingMath.WarnHoursMax, "word of a raid comes a few hours ahead");
        C(DealingMath.ThatSide(90f, 120f) && !DealingMath.ThatSide(90f, 200f) && DealingMath.ThatSide(350f, 20f), "a tower watches its side of the world");
        // What their people remember.
        foreach (var (k, s) in All())
        {
            string kept = DealingMath.Deed(k, s, DealStage.Kept), broken = DealingMath.Deed(k, s, DealStage.Broken);
            C(broken != null, $"{DealingMath.SubName(k, s)}: broken, their people remember it");
            C(DealingMath.Deed(k, s, DealStage.Ended) == null, $"{DealingMath.SubName(k, s)}: over with nobody at fault, nothing held against anyone");
            if (kept != null) C(kept.Length > 0, $"{DealingMath.SubName(k, s)}: kept, remembered as {kept}");
        }
        C(DealingMath.Deed(DealKind.Supply, 0, DealStage.Kept) == "contractKept" && DealingMath.Deed(DealKind.Relief, 0, DealStage.Kept) == "hardship"
          && DealingMath.Deed(DealKind.Work, 0, DealStage.Kept) == "builtTogether" && DealingMath.Deed(DealKind.Petition, (byte)PetitionKind.Raiders, DealStage.Broken) == "brokeWord",
            "kept and broken in their memory's own words");
        C(DealingMath.WorkWeight >= 10, "a work built together weighs enough to be a debt of honor (FavourMath.MinSize)");
    }

    // ---------------- Pen friends ----------------

    private static void PenFriendsRules(Action<bool, string> C)
    {
        int day = DealingMath.DayTicks, now = 200 * day;
        var letters = new List<int> { now - 70 * day, now - 50 * day, now - 20 * day, now - 1 * day };
        C(DealingMath.Recent(letters, now, 60f) == 3 && DealingMath.Recent(letters, now, 90f) == 4 && DealingMath.Recent(null, now, 60f) == 0, "letters counted within a window");
        C(DealingMath.Becomes(3, 30) && !DealingMath.Becomes(2, 90) && !DealingMath.Becomes(5, 29), "three letters in 60 days and regard 30 make a pen friend");
        C(DealingMath.Holds(1, 15) && !DealingMath.Holds(0, 80) && !DealingMath.Holds(4, 14), "it holds while a letter passes in 90 days and regard stays at 15");
        C(DealingMath.KeepDays > DealingMath.ExchangeDays && DealingMath.KeepRegard < DealingMath.FriendRegard, "harder to make than to keep");
        C(!DealingMath.FavourDue(10f, -1f) && DealingMath.FavourDue(15f, -1f) && !DealingMath.FavourDue(40f, 30f) && DealingMath.FavourDue(80f, 60f),
            "a favor after a season of friendship, then once in sixty days at most");
        C(DealingMath.PenFriendOffset > 0f && DealingMath.PenFriendOffset < DealingMath.MarketOffset, "a pen friend's better terms, less than a stall's");
        C(DealingMath.MaxExchanges >= DealingMath.FriendLetters && DealingMath.MaxFriends >= 3, "enough letters remembered; a bound on friends");
    }

    // ---------------- The world's letter rate ----------------

    private static void WorldRate(Action<bool, string> C)
    {
        C(DealingMath.WriteChance(0, 0, 0, -1f) == 0f, "nobody who thinks well of the colony: nobody writes");
        C(DealingMath.WriteChance(3, 0, DealingMath.MaxWaiting, -1f) == 0f, "never with two letters waiting for an answer");
        C(DealingMath.WriteChance(3, 0, 0, 2f) == 0f && DealingMath.WriteChance(3, 0, 0, 7f) > 0f, "never within six days of the last");
        C(DealingMath.WriteChance(1, 0, 0, -1f) == DealingMath.BaseChance && DealingMath.WriteChance(1, 2, 0, -1f) > DealingMath.BaseChance
          && DealingMath.WriteChance(1, 50, 0, -1f) == DealingMath.MaxChance, "a modest base, more with pen friends, capped");
        // A simulated year of days with one warm notable and no pen friend, answered at once: about one letter in two or three weeks.
        var rng = new Random(7);
        foreach (int friends in new[] { 0, 3 })
        {
            int letters = 0;
            float last = -1f;
            const int days = 6000;
            for (int d = 0; d < days; d++)
            {
                float since = last < 0f ? -1f : d - last;
                if (rng.NextDouble() < DealingMath.WriteChance(1, friends, 0, since))
                {
                    letters++;
                    last = d;
                }
            }
            float gap = days / (float)Math.Max(1, letters);
            C(gap >= 10f && gap <= 25f, $"with {friends} pen friends, a letter every {gap:0.0} days on average (modest: 10 to 25)");
        }
        C(DealingMath.WriteKind(0.1f, true) == ReplyKind.News && DealingMath.WriteKind(0.6f, true) == ReplyKind.Request && DealingMath.WriteKind(0.9f, true) == ReplyKind.Invitation
          && DealingMath.WriteKind(0.9f, false) == ReplyKind.Request, "news most often, then a request, an invitation when one can be made");
        C(DealingMath.RequestGoods(WorldAge.Tribal, 0.5f) == "wood" && DealingMath.RequestGoods(WorldAge.Industrial, 0.5f) == "Steel" && DealingMath.RequestGoods(WorldAge.Medieval, 0.99f) == "Cloth",
            "a request by their age");
        C(DealingMath.RequestCount("medicine") <= 5 && DealingMath.RequestCount("wood") <= 100, "a small request");
        C(DealingMath.UnansweredRegard < 0f && DealingMath.GrantedRegard > DealingMath.ReplyRegard, "silence cools them; a request granted warms more than a reply");
    }

    // ---------------- A chain of neighbours ----------------

    private static void Chain(Action<bool, string> C)
    {
        C(DealingMath.ChainDays(3f, 6f, 7f, 7f) == 3f + 2f * PostMath.RelayDays + 6f, "there, a day to be introduced and a day further, and on");
        C(DealingMath.ChainDays(8f, 6f, 7f, 7f) < 0f, "their town within the colony's range");
        C(DealingMath.ChainDays(3f, 15f, 7f, 7f) < 0f && DealingMath.ChainDays(3f, 13f, 7f, 7f) > 0f, "on within twice their own range");
        C(DealingMath.ChainDays(3f, 40f, 7f, -1f) > 0f, "a people with no range (shuttles) carries it however far");
        C(DealingMath.ChainDays(-1f, 4f, 7f, 7f) < 0f && DealingMath.ChainDays(3f, -1f, 7f, 7f) < 0f, "no way by land, no chain");
        // Slower than a relay over the same legs.
        var relay = new List<RelayOption> { new RelayOption { id = 1, toRelay = 3f, onward = 6f, goodTerms = true } };
        PostMath.Relay(relay, 7f, out float relayDays);
        C(DealingMath.ChainDays(3f, 6f, 7f, 7f) > relayDays, "slower than a relay");
        C(DealingMath.Introduces(true, DealingMath.IntroGoodwill, false) && !DealingMath.Introduces(true, DealingMath.IntroGoodwill - 1, false)
          && DealingMath.Introduces(true, 0, true) && !DealingMath.Introduces(false, 90, true) && !DealingMath.Introduces(true, -5, true),
            "introduced by a people at peace with the far one, on good terms with the colony or with a pen friend among them");
    }

    // ---------------- The words ----------------

    private static void Words(string root, Func<string, string> Code, Action<bool, string> C)
    {
        var path = Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Dealings.xml");
        var mine = XDocument.Load(path).Root!.Elements().ToList();
        var keys = new HashSet<string>(mine.Select(e => e.Name.LocalName));
        var all = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).Select(e => e.Name.LocalName));
        foreach (var (k, s) in All())
        {
            C(keys.Contains(DealingMath.LabelKey(k, s)), $"{DealingMath.SubName(k, s)}: its name in words");
            C(keys.Contains(DealingMath.AnswerKey(k, s, true)) && keys.Contains(DealingMath.AnswerKey(k, s, false)), $"{DealingMath.SubName(k, s)}: their council's yes and no");
        }
        foreach (ReplyKind r in Enum.GetValues(typeof(ReplyKind)))
        {
            C(keys.Contains(DealingMath.LabelKey(DealKind.Reply, (byte)r)) && keys.Contains("CC_World_Label_" + r) && keys.Contains("CC_Deal_Topic_Reply_" + r) && keys.Contains("CC_Deal_Week_" + r)
              && keys.Contains("CC_World_Ans_" + r + "_Yes") && keys.Contains("CC_World_Ans_" + r + "_No"), $"a letter the world wrote ({r}): its words and the answer's");
        }
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer", "Empire" })
            C(keys.Contains("CC_Deal_Council_" + age) && keys.Contains("CC_Deal_Leaders_" + age), $"the council and the leaders in the words of the age: {age}");
        // Every reason the rules give has its words.
        string math = Code("Dealings/DealingMath.cs");
        int from = math.IndexOf("public static string WhyNot(", StringComparison.Ordinal), to = math.IndexOf("// ================= Their answer", StringComparison.Ordinal);
        var reasons = Regex.Matches(math.Substring(from, to - from), "\"([A-Z][A-Za-z]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(reasons.Count >= 20, $"the rules give {reasons.Count} reasons");
        foreach (var why in reasons) C(keys.Contains(DealingMath.WhyKey(why)), $"why not ({why}), in words");
        foreach (var why in new[] { "Off", "War", "NoOne", "NoTown", "Unreachable", "Already", "Lately", "Goodwill", "Nothing", "NoLetter", "PayShort", "ShareShort" })
            C(keys.Contains(DealingMath.WhyKey(why)), $"why not ({why}), in words");
        foreach (var k in DealingMath.Kinds) C(keys.Contains("CC_Deal_Act_" + k) && keys.Contains("CC_Deal_Act_" + k + "_Tip"), $"the act {k} on a card, with its tip");
        foreach (var w in Enum.GetNames(typeof(WorkKind))) C(keys.Contains("CC_Deal_Work_Begun_" + w) && keys.Contains("CC_Deal_Work_Built_" + w), $"the work {w}: begun and built");
        foreach (var s in new[] { "Kept", "Broken", "Ended" }) C(keys.Contains("CC_Deal_EndLabel_" + s), $"a dealing {s}, in a letter's title");
        foreach (var s in new[] { "TownDealings", "PenFriends", "WorldWrites" })
            C(keys.Contains("CC_Settings_" + s) && keys.Contains("CC_Settings_" + s + "_Tip") && keys.Contains("CC_SetNote_" + s), $"the setting {s}, its tip and note");
        foreach (var d in new[] { "contractKept", "contractBroken", "builtTogether", "friendship" })
            C(keys.Contains("CC_Memory_Deed_" + d) && keys.Contains("CC_Memory_Deed_" + d + "_Name"), $"their memory of {d}, in words");
        C(keys.Contains("CC_Dip_For_builtTogether") && keys.Contains("CC_Dip_For_penFriend"), "the debts of honor they make, in words");
        // Every key the dealings' files name has its words.
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Dealings"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(CC_[A-Za-z0-9_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(all.Contains(k), $"{Path.GetFileName(file)}: {k} is written");
            }
        foreach (var e in mine)
        {
            string v = e.Value;
            C(!v.Contains('—') && !v.Contains('!'), $"{e.Name.LocalName}: no em dash, no exclamation");
            C(!Regex.IsMatch(v, @"\bin \d+ (days?|hours?)\b"), $"{e.Name.LocalName}: no countdowns");
            C(!Regex.IsMatch(v, @"\b(honour|favour|neighbour|colour)", RegexOptions.IgnoreCase), $"{e.Name.LocalName}: American spelling, like the game");
            C(!Regex.IsMatch(v, @"\b(house|dynasty|lineage|bloodline)\b", RegexOptions.IgnoreCase), $"{e.Name.LocalName}: the colony is the family, no houses or lines");
        }
        // The camp's spoken lines are one sentence (ImmersionTests holds them too).
        var camp = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Hired.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        foreach (var li in camp.Element("lines")!.Elements("li").Concat(camp.Element("momentLines")!.Elements("li")))
        {
            string line = Regex.Replace(li.Value, @"^\[[^\]]*\]\s*", "");
            C(Regex.Matches(line, @"[.?](\s|$)").Count <= 1, $"the guards say one sentence: {line}");
        }
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C)
    {
        string math = Code("Dealings/DealingMath.cs");
        C(!Regex.IsMatch(math, @"using (RimWorld|Verse|UnityEngine)"), "the rules use no game types");
        C(Enum.GetNames(typeof(LetterPurpose)).Last() == "Dealing", "the letter purpose is the last, so saved letters keep their places");
        C(NotableMath.WaitDays(LetterPurpose.Dealing) == 3f && !NotableMath.Gift(AnswerTone.Warm, LetterPurpose.Dealing, 90, 0f) && LetterMath.OwnReasons(LetterPurpose.Dealing),
            "a dealing's letter: its own waits, no stray gift, its own reasons");
        C(PostMath.UrgencyOf(LetterPurpose.Dealing) == PostUrgency.Business, "a council's answer is business: it waits a few days for a carrier at most");
        string dip = Code("Diplomacy/DiplomacyLetters.cs"), letters = Code("World/NeighbourLetters.cs"), panel = Code("World/NeighboursPanel.cs");
        C(dip.Contains("DealingLetters.Owns(p)") && dip.Contains("DealingLetters.WhyNot(n, Home)") && dip.Contains("DealingLetters.Topics(n, Home)") && dip.Contains("DealingLetters.Asks(topicKey")
          && dip.Contains("DealingLetters.Read(l, n"), "the letter's own machinery: can it be written, what about, what is asked, their answer");
        C(letters.Contains("DealingLetters.AnswerIn(l)") && letters.Contains("PenFriends.Exchanged(n, tone)") && letters.Contains("DealingLetters.ToName(n, topic?.key)")
          && letters.Contains("DealingLetters.Title(l, n)"), "the letters: the answer in starts it, a steady exchange counted, addressed to the council, its title");
        C(panel.Contains("DealingLetters.Owns(p) && purpose != p"), "the letter's window offers a dealing only when opened for one");
        foreach (var k in DealingMath.Kinds)
        {
            C(PeopleActMath.KindOf("deal-" + k.ToString().ToLowerInvariant()) == PeopleActMath.KindOf(k), $"the act {k} on a town's card is the act {PeopleActMath.KindOf(k)}");
            C(PeopleActMath.GroupOf(PeopleActMath.KindOf(k)) == ActGroup.Dealings && PeopleActMath.Way(PeopleActMath.KindOf(k)) == ActWay.Letter,
                $"{k}: under the council as a whole, by letter (the post's reach line says how)");
        }
        C(PeopleActMath.KindOf("deal-proclaim-people") == PeopleActKind.Proclaim && PeopleActMath.KindOf("deal-petition-people") == PeopleActKind.Petition
          && PeopleActMath.KindOf("deal-reply") == PeopleActKind.Reply, "the people's card and a notable's");
        string acts = Code("Dealings/DealingActs.cs");
        C(acts.Contains("CardKind.Place") && acts.Contains("CardKind.People") && acts.Contains("CardKind.Person") && acts.Contains("Inbox.Register(\"dealings-letters\""),
            "on a town's, a people's and a notable's card, and the inbox");
        C(Code("UI/Region/PeoplePage.cs").Contains("DealingActs.Draw(e.f") && Code("UI/Region/PeoplesModel.cs").Contains("Dealings.AnyLive(f)"),
            "the Dealings tab shows them, and the filter counts them");
        string patches = Code("Dealings/DealingPatches.cs");
        C(Regex.Matches(patches, @"\[HarmonyPatch\(").Count == Regex.Matches(patches, @"RimfolkPatches\.On").Count, "every patch stands down with the deeper hooks off");
        C(patches.Contains("nameof(Storyteller.TryFire)") && Code("Dealings/PenFriends.cs").Contains("RelayMast.Working(map)"), "word of a raid; never where the mast already warns");
        string dealings = Code("Dealings/Dealings.cs");
        C(dealings.Contains("Couriers.CollectOrder(") && dealings.Contains("Couriers.Exchange(") && dealings.Contains("IncidentDefOf.TraderCaravanArrival")
          && dealings.Contains("Arrivals.Visit(pawns, home, f, null, hours)") && dealings.Contains("OverlayRoad(") && dealings.Contains("tracker.Start(setup, true)"),
            "it plays out on the map: their porters, their caravans and visitors, a road on the world, a camp of theirs");
        C(!Regex.IsMatch(dealings + Code("Dealings/WorldWrites.cs") + Code("Dealings/PenFriends.cs"), @"Messages\.Message\(|Find\.LetterStack\.ReceiveLetter\("),
            "the player is told through Notify and Tell");
        C(!Regex.IsMatch(dealings, @"in \d+ days"), "no countdowns in its words");
        // Rimfolk's own visitors and errands at the errand's pace (0.171.1): never the game's half-speed walk or its plain travel toil.
        var dealingCode = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Dealings"), "*.cs").Select(File.ReadAllText).ToList();
        C(dealingCode.All(t => !t.Contains("LocomotionUrgency.Walk") && !t.Contains("new LordToil_Travel(") && !t.Contains("IncidentDefOf.VisitorGroup")),
            "their folk come at an errand's pace (LordJob_VisitOnErrand), not the game's half-speed visit");
        C(DealingMath.GuestsMin >= 2 && DealingMath.GuestsMax <= 8 && DealingMath.GuestHours >= 6f, "a few of their folk for the day");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        foreach (var s in new[] { "townDealings", "penFriends", "worldWrites" })
            C(settings.Contains("public bool " + s + " = true") && settings.Contains("ref " + s + ", \"" + s + "\", true") && catalog.Contains("Toggle(\"" + s + "\""), $"the setting {s}");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var c in new[] { "deals", "friends", "asks" }) C(est.Contains("Dealings/DealingBook.cs:" + c), $"the saved {c} in the save's estimate");
        C(Code("Dealings/DealingBook.cs").Contains(": GameComponent") && Code("Dealings/DealingBook.cs").Contains("deals ??= new List<Dealing>()"),
            "a game component of its own: an older save starts it empty");
        var camp = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Hired.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        C(camp.Element("defName")?.Value == "Rimfolk_Camp_HiredGuard" && camp.Element("work")?.Value == "contract" && camp.Element("weight")?.Value == "0" && camp.Element("fights")?.Value == "true"
          && camp.Element("calling") == null, "the guard detail: the contract's camp for any people, never on its own, fights for the colony");
        C(dealings.Contains("GuardCamp = \"Rimfolk_Camp_HiredGuard\", CrewCamp = \"Rimfolk_Camp_HarvestHands\""), "guards and harvest hands by their camps' defs");
        var mission = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_Festival.xml")).Root!.Elements("IncidentDef").Single();
        C(mission.Element("baseChance")?.Value == "0" && mission.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_MissionFestival",
            "the festival's journey: never drawn by the storyteller");
        C(Code("World/FactionMemory.cs").Contains("[\"contractKept\"]") && Code("World/FactionMemory.cs").Contains("[\"builtTogether\"]") && Code("Diplomacy/FavourMath.cs").Contains("\"builtTogether\""),
            "their memory's new deeds; a work built together is a good turn that leaves them owing");
        C(Code("Post/PostReach.cs").Contains("FindChain(home, town, info, ui)") && Code("Post/Outbox.cs").Contains("chain && !theirs ? info.chain"),
            "the chain of neighbours in the post's reach and its carriers");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Dealings/DealingMath.cs"), "the rules linked into the tests");
    }
}
