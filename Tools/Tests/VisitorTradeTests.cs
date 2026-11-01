using System;
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
        return n;
    }
}
