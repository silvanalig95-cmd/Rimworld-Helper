using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Visitors and campers buying at the colony's market stalls, and the drovers' milk for the colony to buy
/// (Source/ColonyCouncil/Visitors/VisitorTradeMath.cs, VisitorTrade.cs, Camps/CampMarket.cs, CampMilk.cs): what each kind
/// of traveller looks for, how many they take and that they can always pay for it, the price and the chance to buy as
/// RimMarket reckons a shopper's sale, their purse, the market's hours, the milk a beast gives and what it costs; and from
/// the defs, that every camp kind's list is known, the drovers' ask is there, and the visitors' duties send them.
/// </summary>
public static class VisitorTradeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors at the market: " + what);
        }

        // ---------- What they look for ----------
        C(VisitorTradeMath.Wants("drovers")[0] == TradeGoods.Feed && VisitorTradeMath.Wants("herders")[0] == TradeGoods.Feed, "drovers and herders look for feed for the herd first");
        C(VisitorTradeMath.Wants("smith")[0] == TradeGoods.Metal && VisitorTradeMath.Wants("tinker")[0] == TradeGoods.Metal, "a smith and a tinker look for metal first");
        C(VisitorTradeMath.Wants("family").Contains(TradeGoods.Clothes) && VisitorTradeMath.Wants("family")[0] == TradeGoods.Meal, "a family looks for a meal first, and clothes");
        C(VisitorTradeMath.Wants("someone new").SequenceEqual(VisitorTradeMath.RoadNeeds) && VisitorTradeMath.Wants((string)null).SequenceEqual(VisitorTradeMath.RoadNeeds),
            "anyone else looks for what the road needs");
        // A market day's buyers (Council_WS_MarketDays): food first, then a bit of everything, each with a purse of their own.
        var market = VisitorTradeMath.Wants(VisitorTradeMath.Buyers);
        C(market[0] == TradeGoods.RawFood && market.Distinct().Count() == market.Length, "market-day buyers look for food first, and each kind once");
        foreach (TradeGoods g in Enum.GetValues(typeof(TradeGoods)))
            if (g != TradeGoods.None) C(VisitorTradeMath.Fit(g, market) > 0, $"market-day buyers will look at {g}");
        var (bMin, bMax) = VisitorTradeMath.Purse(VisitorTradeMath.Buyers, false);
        C(bMin >= VisitorTradeMath.LeastSilver && bMax > bMin && bMin > VisitorTradeMath.Purse("family", false).max - 1 && VisitorTradeMath.Purse(VisitorTradeMath.Buyers, true).max == 0,
            "market-day buyers come with more coin than the poor, and none if robbed");
        C(VisitorTradeMath.MarketBuys > VisitorTradeMath.VisitBuys, "a market day's buyers buy more than a passing visit");
        var (soonLo, soonHi) = VisitorTradeMath.NextTryHours(true);
        var (lateLo, lateHi) = VisitorTradeMath.NextTryHours(false);
        C(soonLo >= 1 && soonHi >= soonLo && soonHi < lateLo && lateHi >= lateLo, "market-day buyers go back to the stalls sooner than other visitors");
        C(VisitorTradeMath.Wants(new[] { "Feed", "roadfood", "nonsense", "Feed" }).SequenceEqual(new[] { TradeGoods.Feed, TradeGoods.RoadFood }),
            "a camp kind's list, in order, any case, unknown words and repeats left out");
        C(VisitorTradeMath.Wants(new string[0]).SequenceEqual(VisitorTradeMath.RoadNeeds) && VisitorTradeMath.Wants((string[])null).SequenceEqual(VisitorTradeMath.RoadNeeds),
            "a camp kind with no list buys what the road needs");
        C(VisitorTradeMath.Parse("Feed, Meal") == TradeGoods.None && VisitorTradeMath.Parse("3") == TradeGoods.None && VisitorTradeMath.Parse("None") == TradeGoods.None
          && VisitorTradeMath.Parse(" medicine ") == TradeGoods.Medicine, "one kind of goods by name, never two at once or a number");
        foreach (TradeGoods g in Enum.GetValues(typeof(TradeGoods)))
            if (g != TradeGoods.None) C(VisitorTradeMath.Parse(g.ToString()) == g && VisitorTradeMath.Parse(g.ToString().ToLowerInvariant()) == g, $"{g} is known by its name");

        // ---------- How well an article fits ----------
        var drovers = VisitorTradeMath.Wants("drovers");
        C(VisitorTradeMath.Fit(TradeGoods.Feed, drovers) > VisitorTradeMath.Fit(TradeGoods.RoadFood, drovers)
          && VisitorTradeMath.Fit(TradeGoods.RoadFood, drovers) > VisitorTradeMath.Fit(TradeGoods.Medicine, drovers), "the first thing they look for fits best");
        C(VisitorTradeMath.Fit(TradeGoods.Art, drovers) == 0 && VisitorTradeMath.Fit(TradeGoods.None, drovers) == 0 && VisitorTradeMath.Fit(TradeGoods.Feed, null) == 0,
            "what they don't want doesn't fit at all");
        C(VisitorTradeMath.Fit(TradeGoods.Meal | TradeGoods.RoadFood, drovers) == VisitorTradeMath.Fit(TradeGoods.RoadFood, drovers)
          && VisitorTradeMath.Kind(TradeGoods.Meal | TradeGoods.RoadFood, drovers) == TradeGoods.RoadFood, "pemmican is road food to a drover: its best fit counts, and names it");
        C(VisitorTradeMath.Kind(TradeGoods.Art, drovers) == TradeGoods.None, "nothing they want, no kind");

        // ---------- How many, and always paid for ----------
        C(VisitorTradeMath.Need(TradeGoods.Feed, 3, 0) == 10 && VisitorTradeMath.Need(TradeGoods.Feed, 3, 4) == 42 && VisitorTradeMath.Need(TradeGoods.Feed, 3, 20) == 60,
            "feed by the herd's size, at most 60");
        C(VisitorTradeMath.Need(TradeGoods.Meal, 7, 0) == 4 && VisitorTradeMath.Need(TradeGoods.Meal, 0, 0) == 1 && VisitorTradeMath.Need(TradeGoods.Clothes, 5, 0) == 1,
            "a meal each up to four, one coat");
        bool fair = true, paid = true;
        var rng = new Random(11);
        for (int i = 0; i < 20000; i++)
        {
            int need = rng.Next(0, 70), counter = rng.Next(0, 80), purse = rng.Next(0, 300);
            float unit = (float)(rng.NextDouble() * 60.0);
            int k = VisitorTradeMath.HowMany(need, counter, unit, purse);
            if (k < 0 || k > need || k > counter) fair = false;
            if (k > 0 && VisitorTradeMath.Total(VisitorTradeMath.UnitPrice(unit, 1f, 1f, 1f), k) > purse) paid = false;
            if (k == 0 && need > 0 && counter > 0 && VisitorTradeMath.Total(Math.Max(1f, unit), 1) <= purse) fair = false;
        }
        C(fair, "never more than they need or lies on the counter, and at least one when they can pay for one");
        C(paid, "never more than the purse pays for, with RimMarket's rounding up");
        C(VisitorTradeMath.HowMany(5, 5, 30f, 20) == 0 && VisitorTradeMath.HowMany(5, 5, 4f, 20) == 5 && VisitorTradeMath.HowMany(5, 5, 4.5f, 20) == 4, "too dear for one: none; four at 4.5 make 18");

        // ---------- The price and the chance, as RimMarket reckons them ----------
        C(Math.Abs(VisitorTradeMath.UnitPrice(10f, 1.1f, 1.03f, 0.9f) - 10f * 1.1f * 1.03f * 0.9f) < 1e-4f && VisitorTradeMath.UnitPrice(0.3f, 1f, 1f, 1f) == 1f,
            "sell value times the line's price, the keeper and the demand, never under one silver");
        C(VisitorTradeMath.Total(2.2f, 3) == 7 && VisitorTradeMath.Total(2f, 3) == 6 && VisitorTradeMath.Total(0.2f, 0) == 1, "a sale rounds up to the silver, at least one");
        C(Math.Abs(VisitorTradeMath.BuyChance(0.7f, true, 1f) - 0.8f) < 1e-4f && Math.Abs(VisitorTradeMath.BuyChance(0.7f, false, 0.5f) - 0.35f) < 1e-4f
          && VisitorTradeMath.BuyChance(0.95f, true, 1f) == 1f && VisitorTradeMath.BuyChance(-1f, false, 1f) == 0f, "a keeper adds a tenth, demand scales it, kept within 0 and 1");

        // ---------- Their purse and the market's hours ----------
        C(VisitorTradeMath.Purse("drovers", true) == (0, 0), "robbed on the road: no coin");
        var poor = VisitorTradeMath.Purse("family", false);
        var trade = VisitorTradeMath.Purse("drovers", false);
        var other = VisitorTradeMath.Purse(null, false);
        C(poor.min <= poor.max && trade.min <= trade.max && other.min <= other.max && poor.max < trade.max && poor.min >= VisitorTradeMath.LeastSilver,
            "the poor carry a little, those going to market more, everyone enough to buy something");
        C(VisitorTradeMath.ShopHour(8, 8, 18) && VisitorTradeMath.ShopHour(16, 8, 18) && !VisitorTradeMath.ShopHour(17, 8, 18) && !VisitorTradeMath.ShopHour(7, 8, 18)
          && !VisitorTradeMath.ShopHour(23, 8, 18), "in the market's hours, with time to walk back before it closes");

        // ---------- The herd's milk ----------
        C(VisitorTradeMath.MilkGives(14, 0.49f) == 0 && VisitorTradeMath.MilkGives(14, 1f) == 14 && VisitorTradeMath.MilkGives(14, 1.4f) == 14
          && VisitorTradeMath.MilkGives(14, 0.75f) == 11 && VisitorTradeMath.MilkGives(0, 1f) == 0, "a beast gives its amount by how full it is, none under half full");
        C(VisitorTradeMath.MilkPrice(26, 1.9f) == 49 && VisitorTradeMath.MilkPrice(0, 1.9f) == 0 && VisitorTradeMath.MilkPrice(1, 0.1f) == 1, "milk costs its worth, at least one silver");
        C(VisitorTradeMath.PayWith(49, 120) == 49 && VisitorTradeMath.PayWith(49, 20) == 20 && VisitorTradeMath.PayWith(49, -3) == 0, "the colony pays the price, or what it has");

        // ---------- The defs ----------
        int kinds = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml"))
            foreach (var def in XDocument.Load(file).Root!.Elements("ColonyCouncil.CampKindDef"))
            {
                var buys = def.Element("buys");
                if (buys == null) continue;
                kinds++;
                foreach (var li in buys.Elements("li"))
                    C(VisitorTradeMath.Parse(li.Value) != TradeGoods.None, $"{def.Element("defName")?.Value}: buys {li.Value}, a kind of goods");
            }
        C(kinds >= 15, $"camp kinds say what they buy ({kinds})");
        var road = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Road.xml")).Root!;
        var droversDef = road.Elements("ColonyCouncil.CampKindDef").FirstOrDefault(d => d.Element("defName")?.Value == "Council_Camp_Drovers");
        var milk = droversDef?.Element("asks")?.Elements("li").FirstOrDefault(a => a.Element("key")?.Value == "milk");
        C(milk != null && milk.Element("price")?.Value == "milk" && milk.Element("needs")?.Value == "herdMilk"
          && milk.Descendants("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_CampMilkSale"), "the drovers' herd's milk can be bought, priced by what it gives");
        C(milk != null && milk.Element("label")!.Value.Contains("{THING}") && !milk.Element("outcomes")!.Elements("li").Any(l => Regex.IsMatch(l.Value, @"\{(COUNT|THING)\}")),
            "the ask names the milk and its price; what's said when asked doesn't claim an amount yet");
        var src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Camps/CampAsks.cs"));
        C(src.Contains("case \"herdMilk\":"), "the ask's need is one the code knows");
        var life = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampLife.xml")).Root!;
        foreach (var job in new[] { "Council_CampMilk", "Council_CampShear" })
            C(life.Elements("JobDef").Any(j => j.Element("defName")?.Value == job && j.Descendants("progress").Any(p => p.Value == "true")), $"{job} shows its progress in a bar");
        var visitors = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Visitors.xml")).Root!;
        foreach (var duty in new[] { "Rimfolk_VisitWait", "Rimfolk_VisitWork" })
            C(visitors.Elements("DutyDef").Any(d => d.Element("defName")?.Value == duty && d.ToString().Contains("ColonyCouncil.JobGiver_VisitShop")), $"{duty} sends one of them to the market");
        var trade2 = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_VisitorTrade.xml")).Root!;
        C(trade2.Elements("JobDef").Any(j => j.Element("defName")?.Value == "Rimfolk_MarketBuy" && j.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_MarketBuy"),
            "the walk to the stall is a job of its own");
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_VisitorTrade.xml")).Descendants("li").Select(l => l.Value).ToList();
        foreach (var moment in new[] { "buy", "dear", "none" })
            C(lines.Any(l => Regex.IsMatch(l, @"^\[([a-z]+,)*" + moment + @"(,[a-z]+)*\]")), $"buyers have lines for {moment}");
        foreach (TradeGoods g in Enum.GetValues(typeof(TradeGoods)))
            if (g != TradeGoods.None) C(lines.Any(l => l.StartsWith("[buy," + g.ToString().ToLowerInvariant())), $"a line for buying {g}");

        n += Guests(root, C);
        return n;
    }

    /// <summary>
    /// Every other visitor at the market (Visitors/VisitorTradeGuests.cs): who may walk over and when, the group's turns,
    /// what each kind of visitor looks for and carries, a trader's people by what their trader deals in, the fine only
    /// for the good; and from the code and the defs, that it goes the campers' way, needs the deeper hooks and its
    /// setting, ends when their group sets off, and has its lines and the keeper's.
    /// </summary>
    private static int Guests(string root, Action<bool, string> C)
    {
        int n = 0;
        void G(bool ok, string what)
        {
            n++;
            C(ok, "every visitor: " + what);
        }

        // ---------- Who may go ----------
        var may = new GuestFacts
        {
            group = GuestGroup.Visit, role = GuestRole.Plain, person = true, grown = true, up = true, awake = true, atStay = true, atPeace = true, quiet = true, hours = true
        };
        G(VisitorTradeMath.WhyNot(may) == null, "a grown visitor at their group's stay, at peace, the map quiet, in the market's hours, may go");
        foreach (var g in new[] { GuestGroup.Visit, GuestGroup.Caravan, GuestGroup.Envoys, GuestGroup.Feast })
        {
            var f = may;
            f.group = g;
            G(VisitorTradeMath.WhyNot(f) == null, $"one of a {g} group may go");
        }
        GuestFacts With(Func<GuestFacts, GuestFacts> change) => change(may);
        G(VisitorTradeMath.WhyNot(With(f => { f.group = GuestGroup.None; return f; })) == "group", "Rimfolk's own visits, camps, raiders and others' lords don't go this way");
        G(VisitorTradeMath.WhyNot(With(f => { f.role = GuestRole.Trader; return f; })) == "role", "the trader keeps to their trade");
        G(VisitorTradeMath.WhyNot(With(f => { f.role = GuestRole.Carrier; return f; })) == "role"
          && VisitorTradeMath.WhyNot(With(f => { f.role = GuestRole.Chattel; return f; })) == "role", "a caravan's pack beasts and what it has for sale never go");
        G(VisitorTradeMath.WhyNot(With(f => { f.person = false; return f; })) == "role", "only people go");
        G(VisitorTradeMath.WhyNot(With(f => { f.grown = false; return f; })) == "child", "children don't go");
        G(VisitorTradeMath.WhyNot(With(f => { f.atPeace = false; return f; })) == "war", "never at war");
        G(VisitorTradeMath.WhyNot(With(f => { f.quiet = false; return f; })) == "danger", "never with the map in danger");
        G(VisitorTradeMath.WhyNot(With(f => { f.leaving = true; return f; })) == "going", "never on their way out");
        G(VisitorTradeMath.WhyNot(With(f => { f.atStay = false; return f; })) == "going", "not while their group walks in or gathers to go");
        G(VisitorTradeMath.WhyNot(With(f => { f.up = false; return f; })) == "unable" && VisitorTradeMath.WhyNot(With(f => { f.awake = false; return f; })) == "unable",
            "not downed, drafted, out of their mind or asleep");
        G(VisitorTradeMath.WhyNot(With(f => { f.pressed = true; return f; })) == "needs", "hunger, weariness and sickness come first");
        G(VisitorTradeMath.WhyNot(With(f => { f.held = true; return f; })) == "held", "not while an answer, a caller's work, a story, the gods or a quest holds them");
        G(VisitorTradeMath.WhyNot(With(f => { f.hours = false; return f; })) == "hours", "only in the market's hours");
        G(VisitorTradeMath.WhyNot(With(f => { f.atPeace = false; f.leaving = true; f.hours = false; return f; })) == "war", "at war, that's the reason given first");

        // ---------- When: the group's turns ----------
        G(VisitorTradeMath.Turn(0, -1, 1000, false, 0) == MarketTurn.FirstSeen, "a group just settled has its first look a little later");
        G(VisitorTradeMath.FirstLookMin > 0f && VisitorTradeMath.FirstLookMax > VisitorTradeMath.FirstLookMin, "the first look comes within an hour or two");
        G(VisitorTradeMath.Turn(0, 5000, 4000, false, 0) == MarketTurn.NotYet && VisitorTradeMath.Turn(0, 5000, 5000, false, 0) == MarketTurn.Go, "not before their next try, then they go");
        G(VisitorTradeMath.Turn(0, 0, 10, true, 0) == MarketTurn.Busy, "one of a group at a time");
        G(VisitorTradeMath.Turn(0, 0, 10, false, VisitorTradeMath.MostAtStalls) == MarketTurn.Busy && VisitorTradeMath.Turn(0, 0, 10, false, VisitorTradeMath.MostAtStalls - 1) == MarketTurn.Go,
            $"never more than {VisitorTradeMath.MostAtStalls} at the stalls in all");
        G(VisitorTradeMath.Turn(VisitorTradeMath.GuestBuys, 0, 10, false, 0) == MarketTurn.Done && VisitorTradeMath.Turn(VisitorTradeMath.GuestBuys - 1, 0, 10, false, 0) == MarketTurn.Go,
            "a purchase or two a day");
        G(VisitorTradeMath.GuestBuys >= 1 && VisitorTradeMath.GuestBuys <= 2 && VisitorTradeMath.MostAtStalls >= 2 && VisitorTradeMath.MostAtStalls <= 4, "one or two a group, a few at the stalls at once");
        G(VisitorTradeMath.ShoppedToday(2, 7, 7) == 2 && VisitorTradeMath.ShoppedToday(2, 6, 7) == 0 && VisitorTradeMath.ShoppedToday(1, -1, 0) == 0, "yesterday's purchases don't count today");
        G(VisitorTradeMath.RetryHours >= 1 && VisitorTradeMath.RetryHours < VisitorTradeMath.NextTryHours(false).min, "after a try with nothing bought they look again sooner than after a purchase");

        // ---------- What they look for ----------
        G(VisitorTradeMath.Wants(VisitorTradeMath.Envoy)[0] == TradeGoods.Art && VisitorTradeMath.Wants(VisitorTradeMath.Notable).SequenceEqual(VisitorTradeMath.Wants(VisitorTradeMath.Envoy)),
            "an envoy and a notable look for something fine, art first");
        G(VisitorTradeMath.IsFine(VisitorTradeMath.Envoy) && VisitorTradeMath.IsFine(VisitorTradeMath.Notable) && !VisitorTradeMath.IsFine(VisitorTradeMath.Travellers)
          && !VisitorTradeMath.IsFine(VisitorTradeMath.Caravan), "only envoys and notables want only the good");
        G(VisitorTradeMath.GoodEnough(1, false) && !VisitorTradeMath.GoodEnough(2, true) && VisitorTradeMath.GoodEnough(3, true) && VisitorTradeMath.GoodEnough(6, true)
          && VisitorTradeMath.GoodEnough(-1, true), "for the fine, good quality or better (things without a quality always do)");
        G(VisitorTradeMath.Wants(VisitorTradeMath.Travellers).SequenceEqual(VisitorTradeMath.RoadNeeds) && VisitorTradeMath.RoadNeeds[0] == TradeGoods.RoadFood,
            "a traveller looks for food that keeps first");
        G(VisitorTradeMath.Wants(VisitorTradeMath.Guests)[0] == TradeGoods.Drink && VisitorTradeMath.Wants(VisitorTradeMath.Guests).Contains(TradeGoods.Art), "a feast's guests something to take home");
        G(VisitorTradeMath.Wants(VisitorTradeMath.Caravan).SequenceEqual(VisitorTradeMath.Wants("merchant")), "a trader's people whose trader deals in nothing known look as a merchant does");
        // A people's calling (PeoplesMath): each of them looks for something of its own way.
        G(VisitorTradeMath.Wants("healers")[0] == TradeGoods.Medicine && VisitorTradeMath.Wants("tamers")[0] == TradeGoods.Feed && VisitorTradeMath.Wants("artisans")[0] == TradeGoods.Metal
          && VisitorTradeMath.Wants("mercenaries")[0] == TradeGoods.Medicine && VisitorTradeMath.Wants("fishers")[0] == TradeGoods.Textiles && VisitorTradeMath.Wants("traders")[0] == TradeGoods.Art,
            "the peoples' callings: healers medicine, tamers feed, artisans metal, mercenaries medicine, fishers cloth, traders art");
        var callings = Regex.Matches(File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Peoples/PeoplesMath.cs")), @"public const string ([A-Z]\w+ = ""[a-z]+""(, )?)+;")
            .Cast<Match>().SelectMany(m => Regex.Matches(m.Value, @"""([a-z]+)""").Cast<Match>().Select(x => x.Groups[1].Value)).Distinct().ToList();
        G(callings.Count >= 20, $"the peoples' callings are read ({callings.Count})");
        int own = callings.Count(c => !VisitorTradeMath.Wants(c).SequenceEqual(VisitorTradeMath.RoadNeeds));
        G(own >= callings.Count - 2, $"nearly every calling looks for something of its own ({own} of {callings.Count}; the rest what the road needs)");
        G(VisitorTradeMath.Purse(VisitorTradeMath.Envoy, false).min > VisitorTradeMath.Purse(VisitorTradeMath.Travellers, false).min
          && VisitorTradeMath.Purse(VisitorTradeMath.Travellers, false).min >= VisitorTradeMath.LeastSilver, "an envoy carries more coin than a traveller, everyone enough for something");

        // ---------- A trader's people: what their trader deals in ----------
        var all = new Dictionary<TradeGoods, int> { [TradeGoods.Textiles] = 40, [TradeGoods.Metal] = 10, [TradeGoods.Clothes] = 300, [TradeGoods.Medicine] = 5, [TradeGoods.Art] = 20 };
        var bulk = new Dictionary<TradeGoods, int> { [TradeGoods.Textiles] = 40, [TradeGoods.Metal] = 9, [TradeGoods.Clothes] = 30, [TradeGoods.Medicine] = 1 };
        var tw = VisitorTradeMath.TraderWants(bulk, all);
        G(tw.Length == 4 && tw[0] == TradeGoods.Textiles && tw[1] == TradeGoods.Metal && tw[2] == TradeGoods.Medicine && tw[3] == TradeGoods.RoadFood,
            "the biggest shares of what the trader deals in first (all the cloth, most of the metal), three at most, then food for the road");
        G(VisitorTradeMath.TraderWants(new Dictionary<TradeGoods, int>(), all).SequenceEqual(VisitorTradeMath.RoadNeeds)
          && VisitorTradeMath.TraderWants(null, null).SequenceEqual(VisitorTradeMath.RoadNeeds), "a trader dealing in none of the kinds: what the road needs");
        var tie = VisitorTradeMath.TraderWants(new Dictionary<TradeGoods, int> { [TradeGoods.Art] = 2, [TradeGoods.Feed] = 2 }, new Dictionary<TradeGoods, int> { [TradeGoods.Art] = 4, [TradeGoods.Feed] = 4 });
        G(tie[0] == TradeGoods.Feed && tie[1] == TradeGoods.Art && tie.Last() == TradeGoods.RoadFood, "equal shares in the kinds' own order");
        var road = VisitorTradeMath.TraderWants(new Dictionary<TradeGoods, int> { [TradeGoods.RoadFood] = 3, [TradeGoods.Meal | TradeGoods.Art] = 5 }, null);
        G(road.SequenceEqual(new[] { TradeGoods.RoadFood }), "food that keeps is never listed twice, and only single kinds count");

        // ---------- The code and the defs ----------
        string src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorTradeGuests.cs"));
        string trade = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorTrade.cs"));
        G(src.Contains("[HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]") && src.Contains("JobDefOf.GotoWander") && src.Contains("JobDefOf.Wait_Wander"),
            "only an idle wander near their spot gives way to the market");
        G(src.Contains("public static bool GuestsOn => RimfolkPatches.On && (ColonyCouncilMod.Settings?.marketVisitorsBuy ?? true) && RimMarketStall.SaleReady;"),
            "off with its setting, the deeper hooks, or without RimMarket's sale");
        G(src.Contains("return ShopJob(p,") && src.Contains("GivePurse(p,") && trade.Contains("GuestWants(p,"), "the walk, the purse and the sale are the campers' own");
        G(trade.Contains("VisitorTrade.GuestGoing(pawn)") && src.Contains("public static bool GuestGoing(Pawn p)"), "the walk ends when their group sets off, so they leave with it");
        G(src.Contains("LordJob_TradeWithColony") && src.Contains("LordJob_VisitColony") && src.Contains("LordJob_EnvoyStay") && src.Contains("LordJob_FeastGuests")
          && src.Contains("\"RimMarket\"") && src.Contains("\"Hospitality\""), "the game's visits and caravans, envoys and feasts go; RimMarket's and Hospitality's own lords don't");
        var settings = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/CouncilSettings.cs"));
        G(settings.Contains("public bool marketVisitorsBuy = true;") && settings.Contains("Scribe_Values.Look(ref marketVisitorsBuy, \"marketVisitorsBuy\", true);"), "the setting is kept");
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_VisitorTrade.xml")).Root!.Elements("ColonyCouncil.FlavorPoolDef").ToList();
        var buyLines = pools.First(d => d.Element("defName")?.Value == "Rimfolk_Pool_MarketBuy").Descendants("li").Select(l => l.Value).ToList();
        foreach (var who in new[] { VisitorTradeMath.Caravan, VisitorTradeMath.Envoy, VisitorTradeMath.Notable, VisitorTradeMath.Guests, VisitorTradeMath.Travellers })
            G(buyLines.Any(l => Regex.IsMatch(l, @"^\[buy(,[a-z]+)*," + who + @"\]")), $"a line for one of {who} buying");
        var keeper = pools.FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Pool_MarketKeeper")?.Descendants("li").Select(l => l.Value).ToList();
        G(keeper != null && keeper.Count >= 10 && keeper.All(l => l.StartsWith("[sold")), "the keeper's lines, each for a sale made");
        if (keeper != null)
            foreach (TradeGoods g in Enum.GetValues(typeof(TradeGoods)))
                if (g != TradeGoods.None) G(keeper.Any(l => l.StartsWith("[sold," + g.ToString().ToLowerInvariant() + "]")), $"the keeper has a word for {g} sold");
        G(trade.Contains("RimMarketStall.Keeper(stall)") && trade.Contains("KeeperPool"), "the keeper behind the counter has the word now and then");
        return n;
    }
}
