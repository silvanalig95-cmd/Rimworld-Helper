using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The trade screen (Source/ColonyCouncil/Trade/): the reserve the colony keeps of each kind of good before any of it
/// is more than needed, food by days of eating, the surplus shared over the stocks, the worse spares, the price against
/// the usual, the order of the suggestions, and the suggested basket filled within the silver both sides have; and from
/// the files, that the sorts are the game's own defs naming real comparers, every word the screen says exists without
/// em dashes or exclamation marks, the hooks stand down with the deeper game hooks, and Vanilla Trading Expanded and
/// Trade Helper are met by name only.
/// </summary>
public static class TradeScreenTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "trade screen: " + what);
        }

        // ---------- The reserve ----------
        C(TradeAdviceMath.Floor(TradeGood.Material, 3) == TradeAdviceMath.MaterialAtLeast && TradeAdviceMath.Floor(TradeGood.Material, 10) == 500,
            "wood, metal and stone blocks: 50 a person, at least 250");
        C(TradeAdviceMath.Floor(TradeGood.Textile, 1) == 75 && TradeAdviceMath.Floor(TradeGood.Textile, 5) == 125, "cloth and leather: 25 a person, at least 75");
        C(TradeAdviceMath.Floor(TradeGood.Component, 2) == 8 && TradeAdviceMath.Floor(TradeGood.Component, 4) == 12, "components: 3 a person, at least 8");
        C(TradeAdviceMath.Floor(TradeGood.Precious, 2) == 20 && TradeAdviceMath.Floor(TradeGood.Precious, 6) == 30, "gold and the like: 5 a person, at least 20");
        C(TradeAdviceMath.Floor(TradeGood.Medicine, 1) == 6 && TradeAdviceMath.Floor(TradeGood.Medicine, 5) == 15, "medicine: 3 a person, at least 6");
        C(TradeAdviceMath.Floor(TradeGood.Other, 9) == 0 && TradeAdviceMath.Floor(TradeGood.Drug, 9) == 0 && TradeAdviceMath.Floor(TradeGood.Equipment, 9) == 0,
            "art, body parts, drugs and equipment have no floor");
        C(TradeAdviceMath.Floor(TradeGood.Material, 0) == TradeAdviceMath.Floor(TradeGood.Material, 1), "no people counts as one");
        C(!TradeAdviceMath.Counted(TradeGood.Other, 0f) && TradeAdviceMath.Counted(TradeGood.Other, 0.5f) && TradeAdviceMath.Counted(TradeGood.Material, 0f),
            "a good with no floor is counted only once the colony is seen using it");
        C(!TradeAdviceMath.Counted(TradeGood.Food, 5f) && !TradeAdviceMath.Counted(TradeGood.Feed, 5f) && !TradeAdviceMath.Counted(TradeGood.Equipment, 5f),
            "food and feed go by days, weapons and clothes by spares, never by a count");
        C(TradeAdviceMath.Need(TradeGood.Material, 5, 0f) == 250 && TradeAdviceMath.Need(TradeGood.Material, 5, 20f) == 300,
            "the need: a season's use by the record, or the floor, the larger");
        C(TradeAdviceMath.Need(TradeGood.Medicine, 2, 0.1f, 9f) == 9 && TradeAdviceMath.Need(TradeGood.Medicine, 2, 1f, 9f) == 15,
            "medicine's need takes the plan ahead's target where that is more");
        C(TradeAdviceMath.Keep(TradeGood.Material, 5, 0f, 0) == 500 && TradeAdviceMath.Keep(TradeGood.Material, 5, 0f, 120) == 620,
            "the keep: the need and as much again, and what is set aside on top");
        C(TradeAdviceMath.Keep(TradeGood.Other, 5, 0f, 7) == 7 && TradeAdviceMath.Keep(TradeGood.Other, 5, 0f, 0) == 0,
            "a good with no count keeps only what is set aside");
        C(TradeAdviceMath.Surplus(800, 420) == 380 && TradeAdviceMath.Surplus(100, 420) == 0 && TradeAdviceMath.Surplus(5, -3) == 5, "the surplus is what is beyond the keep");
        C(TradeAdviceMath.WorthSaying(80, 800, 1.9f) && !TradeAdviceMath.WorthSaying(30, 800, 1.9f) && !TradeAdviceMath.WorthSaying(80, 800, 0.4f)
          && !TradeAdviceMath.WorthSaying(0, 0, 100f), "a surplus is said only when it is a tenth of the stock and worth a little silver");

        // ---------- Food by days ----------
        C(Math.Abs(TradeAdviceMath.FoodDays(18f, 2f) - 9f) < 0.001f && TradeAdviceMath.FoodDays(-3f, 2f) == 0f,
            "days of food: the stores as the meals the kitchen would make of them, over what is eaten a day");
        C(TradeAdviceMath.FoodDays(10f, 0f) >= AheadMath.Far, "nobody eating: food lasts for ever");
        C(TradeAdviceMath.FoodNeedDays(5f) == TradeAdviceMath.FoodDaysAtLeast && TradeAdviceMath.FoodNeedDays(25f) == 25f, "the food need: the plan ahead's days, ten at least");
        C(TradeAdviceMath.FoodShortDays(4f, 10f) == 6f && TradeAdviceMath.FoodShortDays(12f, 10f) == 0f, "short of food below the need");
        C(TradeAdviceMath.FoodSurplusDays(26f, 10f) == 6f && TradeAdviceMath.FoodSurplusDays(18f, 10f) == 0f && TradeAdviceMath.FoodSurplusDays(AheadMath.Far, 10f) == 0f,
            "more food than needed only beyond twice the need, never with nobody eating");
        var share = TradeAdviceMath.ShareOut(30f, new List<(int, float)> { (10, 1f), (40, 1f), (5, 2f) });
        C(share[1] == 30 && share[0] == 0 && share[2] == 0, "a surplus comes from the biggest stock first");
        share = TradeAdviceMath.ShareOut(50f, new List<(int, float)> { (10, 1f), (40, 1f), (5, 2f) });
        C(share[1] == 40 && share[0] == 10 && share[2] == 0 && share.Sum() == 50, "and then the next, never more than the surplus");
        share = TradeAdviceMath.ShareOut(5f, new List<(int, float)> { (10, 2f) });
        C(share[0] == 2, "pieces worth more than what is left are not given");
        C(TradeAdviceMath.ShareOut(0f, new List<(int, float)> { (10, 1f) })[0] == 0 && TradeAdviceMath.ShareOut(5f, null).Length == 0, "no surplus, nothing shared");

        // ---------- Spares ----------
        C(TradeAdviceMath.Spares(1) == 1 && TradeAdviceMath.Spares(3) == 1 && TradeAdviceMath.Spares(4) == 2 && TradeAdviceMath.Spares(9) == 3,
            "one spare of a kind for every three people, at least one");
        C(TradeAdviceMath.Standing(4, 0.5f, false) > TradeAdviceMath.Standing(3, 1f, false) && TradeAdviceMath.Standing(3, 1f, false) > TradeAdviceMath.Standing(3, 0.5f, false)
          && TradeAdviceMath.Standing(3, 1f, false) > TradeAdviceMath.Standing(3, 1f, true) && TradeAdviceMath.Standing(3, 0.96f, false) == TradeAdviceMath.Standing(3, 1f, false),
            "a copy stands by quality, then hit points by tenths, then whether the dead wore it");
        var worse = TradeAdviceMath.WorseCopies(new[] { 300, 500, 300, 410 }, 1);
        C(!worse[1] && worse[0] && worse[2] && worse[3], "beyond the spare kept, the worse copies are worth selling");
        worse = TradeAdviceMath.WorseCopies(new[] { 300, 500, 300, 410 }, 2);
        C(!worse[1] && !worse[3] && worse[0] && worse[2], "the best are kept as spares, the best first");
        worse = TradeAdviceMath.WorseCopies(new[] { 500, 500, 500 }, 1);
        C(!worse.Any(w => w), "copies as good as the best are not called worse");
        C(!TradeAdviceMath.WorseCopies(new[] { 100, 500 }, 2).Any(w => w) && TradeAdviceMath.WorseCopies(null, 1).Length == 0, "no more copies than spares: none to sell");

        // ---------- The price against the usual ----------
        float sell = TradeAdviceMath.UsualSell(100f, 0.1f, 0f), buy = TradeAdviceMath.UsualBuy(100f, 0.1f, 0f);
        C(Math.Abs(sell - 66f) < 0.01f && Math.Abs(buy - 126f) < 0.01f, "the usual: 60% of its worth paid, 140% asked, the negotiator's gain counted");
        C(Math.Abs(TradeAdviceMath.UsualSell(100f, 0f, 0.2f) - 48f) < 0.01f && Math.Abs(TradeAdviceMath.UsualBuy(100f, 0f, 0.2f) - 168f) < 0.01f, "and the difficulty's loss");
        C(TradeAdviceMath.UsualBuy(0.1f, 0f, 0f) == 0.5f && TradeAdviceMath.UsualSell(0f, 0f, 0f) == 0.01f, "the game's own least prices");
        C(TradeAdviceMath.SellDeal(79.3f, sell) >= TradeAdviceMath.GoodSale && TradeAdviceMath.SellDeal(78.5f, sell) < TradeAdviceMath.GoodSale, "a sale a fifth above the usual is a good sale");
        C(TradeAdviceMath.BuyDeal(100.5f, buy) >= TradeAdviceMath.Bargain && TradeAdviceMath.BuyDeal(101.5f, buy) < TradeAdviceMath.Bargain, "a price a fifth under the usual is a bargain");
        C(TradeAdviceMath.BuyDeal(126f, buy) < TradeAdviceMath.Bargain && TradeAdviceMath.SellDeal(66f, sell) < TradeAdviceMath.GoodSale, "the usual price is neither");
        C(TradeAdviceMath.PercentOff(1.23f) == 25 && TradeAdviceMath.PercentOff(0.8f) == -20 && TradeAdviceMath.PercentOff(1.02f) == 0 && TradeAdviceMath.PercentOff(1.3f) == 30,
            "a deal in words: the share off the usual, by fives");
        // Vanilla Trading Expanded's market: a good worth a third more just now is a good sale at the same share of its price.
        float worth = 130f / 1.3f;
        C(Math.Abs(TradeAdviceMath.SellDeal(66f * 1.3f, TradeAdviceMath.UsualSell(worth, 0.1f, 0f)) - 1.3f) < 0.001f,
            "the usual worth is the good's own, so a world price in demand reads as a good sale");

        // ---------- The order of the suggestions ----------
        C(TradeAdviceMath.BuyFood > TradeAdviceMath.BuyMedicine && TradeAdviceMath.BuyMedicine > TradeAdviceMath.BuyFeed && TradeAdviceMath.BuyFeed > TradeAdviceMath.BuyPlans
          && TradeAdviceMath.BuyPlans > TradeAdviceMath.BuyComponents && TradeAdviceMath.BuyComponents > TradeAdviceMath.BuyContract && TradeAdviceMath.BuyContract > TradeAdviceMath.BuyWish
          && TradeAdviceMath.BuyWish > TradeAdviceMath.BuyResearch && TradeAdviceMath.BuyResearch > TradeAdviceMath.BuyBargain, "buys: food, medicine, feed, the plans, components, contracts, wishes, research, bargains");
        C(TradeAdviceMath.SellUnused > TradeAdviceMath.SellSurplus && TradeAdviceMath.SellSurplus > TradeAdviceMath.SellGoodPrice && TradeAdviceMath.SellGoodPrice > TradeAdviceMath.SellWorse,
            "sales: what no one can use, the surplus, a good price, the worse spares");
        C(TradeAdviceMath.SortScore(TradeMark.Buy, TradeAdviceMath.BuyBargain) > TradeAdviceMath.SortScore(TradeMark.Sell, TradeAdviceMath.SellUnused)
          && TradeAdviceMath.SortScore(TradeMark.Sell, TradeAdviceMath.SellWorse) > TradeAdviceMath.SortScore(TradeMark.Keep, 99f)
          && TradeAdviceMath.SortScore(TradeMark.Keep, 0f) > TradeAdviceMath.SortScore(TradeMark.None, 99f), "suggested first: buys, then sales, then what is kept, then the rest");

        // ---------- The basket ----------
        var buys = new List<BasketLine>
        {
            new BasketLine { id = 1, max = 50, price = 2f, priority = TradeAdviceMath.BuyFood, group = "food", perUnit = 0.9f },
            new BasketLine { id = 2, max = 60, price = 1f, priority = TradeAdviceMath.BuyFood, group = "food", perUnit = 0.05f * 1.8f },
            new BasketLine { id = 3, max = 10, price = 20f, priority = TradeAdviceMath.BuyMedicine, group = "medicine" }
        };
        var needs = new Dictionary<string, float> { ["food"] = 9f, ["medicine"] = 4f };
        var take = TradeAdviceMath.Fill(buys, needs, null, 1000, 0);
        C(take.TryGetValue(1, out int meals) && meals == 10 && !take.ContainsKey(2), "a shared need is answered once, the cheapest for what it gives first");
        C(take.TryGetValue(3, out int med) && med == 4, "each need takes only what it lacks");
        take = TradeAdviceMath.Fill(buys, needs, null, 60, 0);
        C(Spent(take, buys) <= 60 - TradeAdviceMath.Slack && take.ContainsKey(1) && take[1] == 10 && take.TryGetValue(3, out med) && med == 1,
            "with little silver the most pressing comes first and the rest as far as it goes");
        var sales = new List<BasketLine>
        {
            new BasketLine { id = 10, max = 300, price = 1.5f, priority = TradeAdviceMath.SellSurplus },
            new BasketLine { id = 11, max = 2, price = 100f, priority = TradeAdviceMath.SellUnused }
        };
        take = TradeAdviceMath.Fill(new List<BasketLine>(), null, sales, 0, 250);
        C(take.TryGetValue(11, out int unused) && unused == 2 && take.TryGetValue(10, out int surplus) && surplus == 32, "sales stop where the trader's silver runs out, the most pressing first");
        take = TradeAdviceMath.Fill(buys, needs, sales, 0, 100);
        float spent = Spent(take, buys), earned = Earned(take, sales);
        C(spent <= earned + 0.001f && earned <= 100 + spent, "with no silver of its own the colony buys what its sales pay for, and the trader pays no more than it has and what it is paid");
        C(take.ContainsKey(1), "selling to pay for food short is suggested together");
        // The trader has little silver, so the sales are cut short: the buys are chosen again on what was sold.
        var oneBuy = new List<BasketLine> { new BasketLine { id = 1, max = 1, price = 100f, priority = 90f } };
        var oneSale = new List<BasketLine> { new BasketLine { id = 2, max = 1, price = 150f, priority = 50f } };
        take = TradeAdviceMath.Fill(oneBuy, null, oneSale, 0, 0);
        C(Spent(take, oneBuy) <= Earned(take, oneSale) + 0.001f && !take.ContainsKey(1),
            "a buy that counted on a sale the trader can't pay for is let go");
        var rng = new Random(7);
        bool always = true;
        for (int i = 0; i < 300; i++)
        {
            var b = Enumerable.Range(1, rng.Next(0, 6)).Select(k => new BasketLine
            {
                id = k, max = rng.Next(1, 40), price = (float)(rng.NextDouble() * 50 + 0.5), priority = rng.Next(0, 99), group = rng.Next(3) == 0 ? "g" : null,
                perUnit = (float)(rng.NextDouble() + 0.1)
            }).ToList();
            var s = Enumerable.Range(100, rng.Next(0, 6)).Select(k => new BasketLine { id = k, max = rng.Next(1, 40), price = (float)(rng.NextDouble() * 50 + 0.5), priority = rng.Next(0, 99) })
                .ToList();
            int cs = rng.Next(0, 400), ts = rng.Next(0, 400);
            var tk = TradeAdviceMath.Fill(b, new Dictionary<string, float> { ["g"] = (float)(rng.NextDouble() * 30) }, s, cs, ts);
            float sp = Spent(tk, b), ea = Earned(tk, s);
            if (sp > cs + ea + 0.01f || ea > ts + sp + 0.01f || tk.Any(kv => kv.Value <= 0 || kv.Value > (b.Concat(s).First(l => l.id == kv.Key).max))) always = false;
        }
        C(always, "in any basket the colony can pay for what it buys and the trader for what it buys, each line within its own count");

        // ---------- The files ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string screen = File.ReadAllText(Path.Combine(src, "Trade", "TradeScreen.cs"));
        string advice = File.ReadAllText(Path.Combine(src, "Trade", "TradeAdvice.cs"));
        string sorts = File.ReadAllText(Path.Combine(src, "Trade", "TradeSorts.cs"));
        string vte = File.ReadAllText(Path.Combine(src, "Integrations", "VanillaTrading.cs"));
        // The market card, its window and the colony's trade record (TradeMarketTests checks them in full).
        string market = File.ReadAllText(Path.Combine(src, "Trade", "TradeMarket.cs")) + File.ReadAllText(Path.Combine(src, "Trade", "Window_TradeMarket.cs"))
                        + File.ReadAllText(Path.Combine(src, "Trade", "TradeRecord.cs"));
        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_TradeSorts.xml")).Root!.Elements("TransferableSorterDef").ToList();
        C(defs.Count == 6, "six sorts (the price change with Vanilla Trading Expanded), each a def of the game's own kind");
        foreach (var d in defs)
        {
            string cls = d.Element("comparerClass")?.Value ?? "";
            string name = cls.Replace("ColonyCouncil.", "");
            C(cls.StartsWith("ColonyCouncil.TransferableComparer_") && Regex.IsMatch(sorts, @"public class " + name + @" : TransferableComparer_Rimfolk"),
                $"{d.Element("defName")?.Value} names a comparer of Rimfolk's");
            C((d.Element("label")?.Value ?? "").Length is > 0 and <= 16, $"{d.Element("defName")?.Value}'s label fits the game's sort button");
        }
        C(sorts.Contains("KeyOf(b).CompareTo(KeyOf(a))"), "the sorts put the highest first");
        C(sorts.Contains("MarketValue.CompareTo"), "outside a trade the sorts fall back on market value");
        foreach (var hook in new[] { "Patch_TradeSortMenu", "Patch_TradeRowMark", "Patch_TradeSilverAfter", "Patch_TradeFoot" })
            C(Regex.IsMatch(screen, @"\[HarmonyPatch\([^\]]*\)\]\s*public static class " + hook), $"{hook} is a Harmony patch class of its own");
        C(screen.Contains("RimfolkPatches.On && (S?.tradeMarks") && screen.Contains("RimfolkPatches.On && (S?.tradeBasket") && screen.Contains("RimfolkPatches.On && (S?.tradeReadouts")
          && screen.Contains("if (!RimfolkPatches.On) return all;"), "every hook stands down with the deeper game hooks");
        C(screen.Contains("SilverAfterOn => ReadoutsOn && Silver && !TradeHelper") && screen.Contains("\"timmyliang.tradehelper\""),
            "with Trade Helper loaded the silver after the trade is left to its lists");
        C(!screen.Contains("Messages.Message") && !screen.Contains("LetterStack") && !advice.Contains("Messages.Message") && !advice.Contains("LetterStack"),
            "no letters or messages: it is all on the trade screen");
        C(screen.Contains("t.AdjustTo(target)") && screen.Contains("Changed(dialog)") && !screen.Contains("TryExecute"), "the basket only sets counts; the game's Accept trades");
        C(!vte.Contains("using VanillaTradingExpanded") && vte.Contains("AccessTools.TypeByName(\"VanillaTradingExpanded.TradingManager\")") && vte.Contains("People.ModActive(PackageId)"),
            "Vanilla Trading Expanded is read by name, only when it is loaded");
        C(Regex.Matches(vte, @"\.SetValue\(").Count == 2 && vte.Contains("finally"), "the one switch of VTE's read for a good's usual worth is set back at once");
        string csproj = File.ReadAllText(Path.Combine(src, "ColonyCouncil.csproj"));
        C(!csproj.Contains("VanillaTrading") && !csproj.Contains("TradeHelper"), "no reference to either mod");

        // Every key the screen says exists, with no em dash or exclamation mark.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradeScreen.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var used = Regex.Matches(screen + advice + vte + market, "\"(CC_Trade_[A-Za-z_]+|CC_Settings_Trade[A-Za-z_]*|CC_SetNote_Trade[A-Za-z]*)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 40, $"found the screen's words ({used.Count})");
        foreach (var k in used) C(keyed.ContainsKey(k), $"{k} exists");
        foreach (var kv in keyed)
        {
            C(!kv.Value.Contains('\u2014') && !kv.Value.Contains('!'), $"{kv.Key} has no em dash or exclamation mark");
            foreach (Match m in Regex.Matches(kv.Value, @"\{([A-Z]+)\}"))
                C(Regex.IsMatch(screen + advice + market, "\"" + kv.Key + "\"[^;]*\"" + m.Groups[1].Value + "\"", RegexOptions.Singleline)
                  || kv.Key.StartsWith("CC_Trade_Why_FoodShort") && Regex.IsMatch(advice, "\"" + m.Groups[1].Value + "\""),
                    $"{kv.Key}'s {{{m.Groups[1].Value}}} is given");
        }
        return n;
    }

    private static float Spent(Dictionary<int, int> take, IEnumerable<BasketLine> buys) => buys.Where(b => take.ContainsKey(b.id)).Sum(b => take[b.id] * b.price);

    private static float Earned(Dictionary<int, int> take, IEnumerable<BasketLine> sales) => sales.Where(s => take.ContainsKey(s.id)).Sum(s => take[s.id] * s.price);
}
