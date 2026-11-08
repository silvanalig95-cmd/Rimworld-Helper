using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own trade window (Source/ColonyCouncil/Trade/TradeWindow.cs, Window_RimfolkTrade.cs, TradeWindowMath.cs):
/// each side's grade from the price against the usual one, the market and the record, the caps for need, the shares a
/// price is of its value and of the usual, the order a column's click puts the rows in, what a deal comes to, the best
/// deals, the columns that fit at 1920 and 1280 pixels wide, the chips' rows, the scroll to a row and the kinds; and
/// from the files, that the window opens in place of the game's screen only for a silver trade with the setting and the
/// deeper hooks on, uses the game's own prices, count control and Accept, falls back to the game's screen, switches
/// both ways on the same deal, saves nothing, and says every word from its own Keyed file.
/// </summary>
public static class TradeWindowTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "trade window: " + what);
        }

        bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
        DealRating Sale(float price, float usual = 1f, int market = 0, float best = 0f, bool kept = false) =>
            TradeWindowMath.Rate(new DealInput { sale = true, price = price, usual = usual, market = market, best = best, kept = kept, graded = true });
        DealRating Buy(float price, float usual = 1f, int market = 0, float best = 0f, bool use = true) =>
            TradeWindowMath.Rate(new DealInput { sale = false, price = price, usual = usual, market = market, best = best, use = use, graded = true });

        // ---------- The grades ----------
        C(TradeWindowMath.GradeOf(1.2f) == DealGrade.Great && TradeWindowMath.GradeOf(1.19f) == DealGrade.Good && TradeWindowMath.GradeOf(1.07f) == DealGrade.Good
          && TradeWindowMath.GradeOf(1.06f) == DealGrade.Fair && TradeWindowMath.GradeOf(0.93f) == DealGrade.Fair && TradeWindowMath.GradeOf(0.92f) == DealGrade.Poor,
            "a fifth better than usual is great, 7% good, within 7% fair, worse poor");
        C(Near(TradeWindowMath.GreatAt, TradeAdviceMath.GoodSale), "great is the advice's own good sale, a fifth above the usual");
        var usual = Sale(1f);
        C(usual.grade == DealGrade.Fair && Near(usual.vsUsual, 0f) && !usual.capped, "the usual price is fair");
        C(Sale(1.25f).grade == DealGrade.Great && Near(Sale(1.25f).vsUsual, 0.25f), "a sale a quarter above the usual is great");
        C(Buy(0.8f).grade == DealGrade.Great && Near(Buy(0.8f).vsUsual, -0.2f), "a purchase a fifth under the usual, for something the colony needs, is great");
        C(Sale(0.7f).grade == DealGrade.Poor && Buy(2f).grade == DealGrade.Poor, "a trader who pays the game's cheap price, or asks its expensive one, is poor");
        // Against its market value alone a sale is always under and a purchase always over: the usual is what tells a deal.
        float value = 10f, gain = 0.1f, loss = 0f;
        float usualSell = TradeAdviceMath.UsualSell(value, gain, loss), usualBuy = TradeAdviceMath.UsualBuy(value, gain, loss);
        C(TradeWindowMath.ShareOf(usualSell, value) < 100 && TradeWindowMath.ShareOf(usualBuy, value) > 100 && Sale(usualSell, usualSell).grade == DealGrade.Fair
          && Buy(usualBuy, usualBuy).grade == DealGrade.Fair, "at the usual prices a sale is under its value and a purchase over it, and both are fair");
        var cheap = Buy(0.7f, use: false);
        C(cheap.grade == DealGrade.Fair && cheap.capped, "never a good buy when the colony has no use for it and it isn't a bargain to keep");
        var keptSale = Sale(1.4f, kept: true);
        C(keptSale.grade == DealGrade.Fair && keptSale.capped, "a sale of what the colony is short of or keeps is no more than fair");
        C(!Buy(2f, use: false).capped && Buy(2f, use: false).grade == DealGrade.Poor, "the cap only lowers: a poor price stays poor");
        C(Sale(1.16f, market: 1).grade == DealGrade.Great && Sale(1.16f).grade == DealGrade.Good && Sale(1.16f, market: -1).grade == DealGrade.Good
          && Sale(1.1f, market: -1).grade == DealGrade.Fair, "Vanilla Trading Expanded's market moves the grade a step");
        C(Sale(1.16f, market: 5).marketStep == 1, "one step, however strong the market's word");
        C(TradeWindowMath.RecordStep(true, 1.1f, 1f) == 1 && TradeWindowMath.RecordStep(true, 0.85f, 1f) == -1 && TradeWindowMath.RecordStep(true, 0.95f, 1f) == 0
          && TradeWindowMath.RecordStep(true, 1f, 0f) == 0, "a sale better than the colony's best of 15 days is a step up, a tenth worse a step down");
        C(TradeWindowMath.RecordStep(false, 0.9f, 1f) == 1 && TradeWindowMath.RecordStep(false, 1.2f, 1f) == -1, "for a purchase, less than the colony paid is better");
        C(Sale(1.16f, best: 1f).grade == DealGrade.Great && Buy(0.95f, best: 0.8f).grade == DealGrade.Fair, "the record counts in the grade");
        C(Sale(1.16f, market: 1, best: 1.4f).recordStep == -1 && Sale(1.16f, market: 1, best: 1.4f).grade == DealGrade.Good, "the market and the record each a step, either way");
        C(!TradeWindowMath.Rate(new DealInput { sale = true, price = 5f, usual = 1f, graded = false }).Graded && !TradeWindowMath.Rate(new DealInput { sale = true, price = 5f, usual = 0f, graded = true }).Graded
          && !TradeWindowMath.Rate(new DealInput { sale = true, price = 0f, usual = 1f, graded = true }).Graded, "no grade for people, nor without a price or a usual one");
        C(TradeWindowMath.Bars(DealGrade.Great) == 3 && TradeWindowMath.Bars(DealGrade.Good) == 2 && TradeWindowMath.Bars(DealGrade.Fair) == 1 && TradeWindowMath.Bars(DealGrade.Poor) == 0
          && TradeWindowMath.Bars(DealGrade.None) == 0, "bars: three, two, one, none");
        C(TradeWindowMath.MarketStep(MarketHint.HighSellNow) == 1 && TradeWindowMath.MarketStep(MarketHint.FallingSellSooner) == 1 && TradeWindowMath.MarketStep(MarketHint.LowBuyNow) == 1
          && TradeWindowMath.MarketStep(MarketHint.RisingBuySooner) == 1 && TradeWindowMath.MarketStep(MarketHint.LowSellCanWait) == -1 && TradeWindowMath.MarketStep(MarketHint.HighBuyCanWait) == -1
          && TradeWindowMath.MarketStep(MarketHint.None) == 0, "the market's reasons: now a step up, wait a step down");
        C(TradeWindowMath.SaleIsBetter(Sale(1.25f), Buy(1f)) && !TradeWindowMath.SaleIsBetter(Sale(1f), Buy(0.8f)) && TradeWindowMath.SaleIsBetter(Sale(1f), default)
          && !TradeWindowMath.SaleIsBetter(default, Buy(1f)), "the better side leads");
        C(TradeWindowMath.DealKey(Sale(1.25f), default) > TradeWindowMath.DealKey(Sale(1.19f), default) && TradeWindowMath.DealKey(Sale(1.19f), default) > TradeWindowMath.DealKey(Sale(1.1f), default)
          && TradeWindowMath.DealKey(default, default) == null && TradeWindowMath.DealKey(Sale(1.4f, kept: true), default) < TradeWindowMath.DealKey(Sale(1.1f), default),
            "the deal column sorts by grade first, then by how far above the usual");

        // ---------- The shares ----------
        C(TradeWindowMath.ShareOf(6f, 10f) == 60 && TradeWindowMath.ShareOf(14f, 10f) == 140 && TradeWindowMath.ShareOf(1f, 0f) == -1, "a price as a share of its market value");
        C(TradeWindowMath.VsUsual(1.12f, 1f) == 12 && TradeWindowMath.VsUsual(0.9f, 1f) == -10 && TradeWindowMath.VsUsual(1f, 0f) == 0, "a price against the usual, either way");
        C(TradeWindowMath.Signed(12) == "+12%" && TradeWindowMath.Signed(-10) == "-10%" && TradeWindowMath.Signed(0) == "0%", "a signed share as the cells show it");
        C(TradeWindowMath.Better(true, 1.08f, 1f) == 1 && TradeWindowMath.Better(false, 0.9f, 1f) == 1 && TradeWindowMath.Better(true, 0.9f, 1f) == -1
          && TradeWindowMath.Better(false, 1.05f, 1f) == 0 && TradeWindowMath.Better(false, 1.1f, 1f) == -1, "green above the usual for a sale and under it for a purchase, orange the other way");

        // ---------- The sort ----------
        var rows = new List<TradeRowKeys>
        {
            new TradeRowKeys { name = "steel", category = (int)TradeCategory.Raw, ours = 800, theirs = 200, value = 1.9f, mass = 0.5f, pay = 1.3f, ask = 2.6f, deal = 205f, need = 250f },
            new TradeRowKeys { name = "Beer", category = (int)TradeCategory.Drugs, ours = 0, theirs = 30, value = 12f, mass = 0.3f, ask = 16f, deal = 210f },
            new TradeRowKeys { name = "wood", category = (int)TradeCategory.Raw, ours = 300, theirs = 0, value = 1.2f, mass = 0.4f, pay = 0.8f, deal = 310f, need = 340f },
            new TradeRowKeys { name = "Arrow", category = (int)TradeCategory.Weapons, ours = 2, theirs = 0, value = 1.9f, mass = 2f, pay = 1.1f }
        };
        List<string> Names(TradeColumn c, bool d) => TradeWindowMath.Order(rows, c, d).Select(i => rows[i].name).ToList();
        C(Names(TradeColumn.Name, false).SequenceEqual(new[] { "Arrow", "Beer", "steel", "wood" }), "names from A, whatever their case");
        C(Names(TradeColumn.Name, true).SequenceEqual(new[] { "wood", "steel", "Beer", "Arrow" }), "and again the other way");
        C(Names(TradeColumn.Value, true).SequenceEqual(new[] { "Beer", "Arrow", "steel", "wood" }), "numbers the highest first, a tie by name");
        C(Names(TradeColumn.Value, false).SequenceEqual(new[] { "wood", "Arrow", "steel", "Beer" }), "and the lowest first on the second click");
        C(Names(TradeColumn.Pay, true).Last() == "Beer" && Names(TradeColumn.Pay, false).Last() == "Beer", "a row without that side goes last either way");
        C(Names(TradeColumn.Ask, true).SequenceEqual(new[] { "Beer", "steel", "Arrow", "wood" }), "what they ask, the dearest first, the rest by name");
        C(Names(TradeColumn.Category, false).SequenceEqual(new[] { "steel", "wood", "Arrow", "Beer" }), "kinds in their order, each kind's goods by name");
        C(Names(TradeColumn.Deal, true).First() == "wood" && Names(TradeColumn.Deal, true).Last() == "Arrow", "the deal column: the best first, the ungraded last");
        C(Names(TradeColumn.Need, true).SequenceEqual(new[] { "wood", "steel", "Arrow", "Beer" }), "the need column: the advice's order, the rest last");
        C(Names(TradeColumn.Ours, true)[0] == "steel" && Names(TradeColumn.Theirs, true).SequenceEqual(new[] { "steel", "Beer", "Arrow", "wood" }) && Names(TradeColumn.Mass, true)[0] == "Arrow",
            "the counts and the mass");
        C(!TradeWindowMath.DefaultDescending(TradeColumn.Name) && !TradeWindowMath.DefaultDescending(TradeColumn.Category) && TradeWindowMath.DefaultDescending(TradeColumn.Deal)
          && TradeWindowMath.DefaultDescending(TradeColumn.Value), "a column's first click: names and kinds from A, numbers the highest first");
        C(!TradeWindowMath.Sortable(TradeColumn.Count) && Enum.GetValues(typeof(TradeColumn)).Cast<TradeColumn>().Count(TradeWindowMath.Sortable) == 12,
            "every column sorts but the count control");
        C(TradeWindowMath.Order(null, TradeColumn.Name, false).Count == 0 && TradeWindowMath.Order(new List<TradeRowKeys>(), TradeColumn.Value, true).Count == 0, "no rows, no order");

        // ---------- What the deal comes to ----------
        var s = TradeWindowMath.Sum(new[] { new DealLine(true, 10, 1.2f, 1f, 2f) });
        C(s.Any && s.soldPieces == 10 && Near(s.Net, 12f) && Near(s.VsUsual, 0.2f) && Near(s.SoldShare, 0.6f) && TradeWindowMath.Leaning(s) == 1,
            "a sale a fifth above the usual: about 20% better than usual, 60% of its worth");
        s = TradeWindowMath.Sum(new[] { new DealLine(false, 10, 0.8f, 1f, 0.7f) });
        C(Near(s.VsUsual, 0.2f) && Near(s.Net, -8f) && Near(s.BoughtShare, 0.8f / 0.7f), "a purchase a fifth under the usual: about 20% better than usual");
        s = TradeWindowMath.Sum(new[] { new DealLine(true, 10, 1.2f, 1f, 2f), new DealLine(false, 10, 1.1f, 1f, 0.8f) });
        C(Near(s.VsUsual, (12f - 11f - (10f - 10f)) / 20f) && TradeWindowMath.Leaning(s) == 1, "both ways, weighed by what each comes to");
        s = TradeWindowMath.Sum(new[] { new DealLine(true, 1, 300f, 0f, 500f) });
        C(Near(s.VsUsual, 0f) && Near(s.Net, 300f) && TradeWindowMath.Leaning(s) == 0, "a line with no usual price (people) neither helps nor hurts the comparison");
        s = TradeWindowMath.Sum(new[] { new DealLine(true, 10, 1.01f, 1f, 2f) });
        C(TradeWindowMath.Leaning(s) == 0, "within 2% it is about the usual");
        s = TradeWindowMath.Sum(new[] { new DealLine(true, 10, 1.2f, 1f, 2f), new DealLine(false, 5, 3f, 3f, 2.5f) });
        C(Near(s.GiveWorth, 20f + 15f) && Near(s.GetWorth, 12.5f + 12f), "by market value: the goods and the silver, both ways");
        C(!TradeWindowMath.Sum(null).Any && !TradeWindowMath.Sum(new[] { new DealLine(true, 0, 1f, 1f, 1f) }).Any && TradeWindowMath.Leaning(null) == 0, "nothing in the deal");

        // ---------- The best deals ----------
        var sides = new List<(int, DealRating)> { (0, Sale(1.1f)), (1, Sale(1.3f)), (2, Sale(1f)), (3, Sale(1.25f)), (4, Sale(0.7f)), (5, Sale(1.5f, kept: true)), (6, Sale(1.08f)) };
        var best = TradeWindowMath.Best(sides, 3);
        C(best.SequenceEqual(new[] { 1, 3, 0 }), "the best deals: good and great only, the best first, so many at most");
        C(TradeWindowMath.Best(sides, 10).SequenceEqual(new[] { 1, 3, 0, 6 }) && TradeWindowMath.Best(null, 3).Count == 0 && TradeWindowMath.Best(sides, 0).Count == 0,
            "never a fair, poor or held one");

        // ---------- The layout ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string window = File.ReadAllText(Path.Combine(src, "Trade", "Window_RimfolkTrade.cs"));
        // The window's own columns, read from its source, so the test lays out what the game draws.
        var specs = Regex.Matches(window, @"new ColumnSpec\(TradeColumn\.(\w+), ([\d.]+)f, (\d+)(?:, ([\d.]+)f)?\)")
            .Select(m => (col: Enum.Parse<TradeColumn>(m.Groups[1].Value), spec: new ColumnSpec(Enum.Parse<TradeColumn>(m.Groups[1].Value), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value), m.Groups[4].Success ? float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0f))).Select(x => x.spec).ToList();
        C(specs.Count == 13 && specs.Select(x => x.col).Distinct().Count() == 13, $"the window's thirteen columns, each once ({specs.Count})");
        var withoutVte = specs.Where(x => x.col != TradeColumn.Market && x.col != TradeColumn.Trend).ToList();
        // The window's own widths: the screen less 24 (at most 1800), less the margins, less the side (300 from 1500, else 240) and its gap, less the scroll bar.
        float Table(float screen)
        {
            float inner = Math.Min(screen - 24f, 1800f) - 36f;
            return inner - (inner >= 1500f ? 300f : 240f) - 14f - 16f;
        }
        foreach (var (wide, label) in new[] { (1920f, "1920"), (1280f, "1280") })
        foreach (var (set, vte) in new[] { (specs, "with VTE"), (withoutVte, "without VTE") })
        {
            float width = Table(wide);
            var placed = TradeWindowMath.Fit(set, width, 4f);
            var cols = placed.Select(p => p.col).ToList();
            float right = placed.Count == 0 ? 0f : placed.Max(p => p.x + p.width);
            C(right <= width + 0.5f, $"{label} wide, {vte}: the columns fit ({right:0} of {width:0})");
            C(new[] { TradeColumn.Name, TradeColumn.Ours, TradeColumn.Pay, TradeColumn.Count, TradeColumn.Ask, TradeColumn.Theirs, TradeColumn.Deal, TradeColumn.Value, TradeColumn.Need }
                .All(cols.Contains), $"{label} wide, {vte}: the name, both sides, the count, the grade, the value and the need are there");
            C(placed.Zip(placed.Skip(1), (a, b) => b.x >= a.x + a.width + 3.9f).All(x => x), $"{label} wide, {vte}: left to right in order, apart");
            C(placed.Select(p => (int)p.col).SequenceEqual(placed.Select(p => (int)p.col).OrderBy(x => x)), $"{label} wide, {vte}: in the columns' order");
            var name = placed.First(p => p.col == TradeColumn.Name);
            C(name.width >= 140f, $"{label} wide, {vte}: the name keeps its room ({name.width:0})");
            if (wide >= 1920f) C(cols.Count == set.Count && name.width >= 300f, $"1920 wide, {vte}: every column, and room for long names ({name.width:0})");
        }
        var tiny = TradeWindowMath.Fit(specs, 300f, 4f);
        C(tiny.Any(p => p.col == TradeColumn.Name) && tiny.Any(p => p.col == TradeColumn.Count), "however narrow, the name and the count stay");
        var chips = TradeWindowMath.Flow(new[] { 50f, 60f, 70f, 40f }, 130f, 4f, 24f, out float chipsH);
        C(chips[0] == (0f, 0f) && chips[1] == (54f, 0f) && chips[2] == (0f, 28f) && chips[3] == (74f, 28f) && Near(chipsH, 52f), "the chips run on to a second row where they don't fit");
        TradeWindowMath.Flow(new[] { 50f }, 30f, 4f, 24f, out chipsH);
        C(Near(chipsH, 24f) && TradeWindowMath.Flow(null, 100f, 4f, 24f, out _).Count == 0, "a chip wider than the room stays on its row");
        C(Near(TradeWindowMath.ScrollTo(20, 36f, 360f, 3600f), 20 * 36f - 162f) && TradeWindowMath.ScrollTo(1, 36f, 360f, 3600f) == 0f
          && Near(TradeWindowMath.ScrollTo(99, 36f, 360f, 3600f), 3240f) && TradeWindowMath.ScrollTo(3, 36f, 360f, 200f) == 0f, "a row found from the best deals shows in the middle, the list kept in bounds");

        // ---------- The kinds ----------
        TradeCategory K(TradeWindowMath.Facts f) => TradeWindowMath.CategoryOf(f);
        C(K(new TradeWindowMath.Facts { person = true, animal = true }) == TradeCategory.People && K(new TradeWindowMath.Facts { animal = true, food = true }) == TradeCategory.Animals,
            "people, then animals");
        C(K(new TradeWindowMath.Facts { medicine = true, drug = true, food = true }) == TradeCategory.Medicine && K(new TradeWindowMath.Facts { drug = true, food = true }) == TradeCategory.Drugs,
            "medicine and drugs before food (beer is a drug)");
        C(K(new TradeWindowMath.Facts { weapon = true, art = true }) == TradeCategory.Weapons && K(new TradeWindowMath.Facts { apparel = true, raw = true }) == TradeCategory.Apparel
          && K(new TradeWindowMath.Facts { art = true, made = true }) == TradeCategory.ArtBooks && K(new TradeWindowMath.Facts { book = true }) == TradeCategory.ArtBooks,
            "an engraved weapon is a weapon, a sculpture art, a book a book");
        C(K(new TradeWindowMath.Facts { raw = true, made = true }) == TradeCategory.Raw && K(new TradeWindowMath.Facts { made = true }) == TradeCategory.Manufactured
          && K(new TradeWindowMath.Facts()) == TradeCategory.Other, "raw materials, then what is made, then the rest");
        C(Enum.GetValues(typeof(TradeCategory)).Length == 11, "eleven kinds, as the chips offer");

        // ---------- The files ----------
        string math = File.ReadAllText(Path.Combine(src, "Trade", "TradeWindowMath.cs"));
        string opener = File.ReadAllText(Path.Combine(src, "Trade", "TradeWindow.cs"));
        string screen = File.ReadAllText(Path.Combine(src, "Trade", "TradeScreen.cs"));
        string advice = File.ReadAllText(Path.Combine(src, "Trade", "TradeAdvice.cs"));
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "the rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Trade/TradeWindowMath.cs"), "and are linked into the tests");
        C(Regex.IsMatch(opener, @"\[HarmonyPatch\(typeof\(WindowStack\), nameof\(WindowStack\.Add\)\)\]\s*public static class Patch_TradeWindowOpen"),
            "the opening is a Harmony patch class of its own, on the window stack's Add");
        C(opener.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tradeWindow ?? true);"), "it stands down with the deeper game hooks and its setting");
        C(opener.Contains("!TradeSession.giftMode") && opener.Contains("TradeSession.TradeCurrency == TradeCurrency.Silver") && opener.Contains("window.GetType() != typeof(Dialog_Trade)"),
            "only a silver trade on the game's own screen: gifts, favor and a mod's own kind of screen stay as they are");
        C(Regex.IsMatch(opener, @"catch \(Exception e\)\s*\{\s*CouncilLog\.ErrorOnce\(""trade window"", e\);\s*return window;"), "if Rimfolk's window can't be made, the game's own screen opens");
        C(window.Contains("TradeWindow.ToGame(this, game);") && Regex.IsMatch(window, @"catch \(Exception e\)\s*\{[^}]*failed = true;[^}]*TradeWindow\.ToGame\(this, game\);", RegexOptions.Singleline),
            "if anything fails while it draws, the game's own screen takes the trade over");
        C(opener.Contains("Find.WindowStack.TryRemove(game, false);") && opener.Contains("letThrough = game;") && opener.Contains("Find.WindowStack.Add(game);"),
            "the switch both ways keeps the game's own screen and deal, never set up again");
        C(window.Contains("TransferableUIUtility.DoCountAdjustInterface(rect, t, index, t.GetMinimumToTransfer(), t.GetMaximumToTransfer())"),
            "counts are set with the game's own control, within the game's own limits");
        C(window.Contains("t.GetPriceFor(TradeAction.PlayerSells)") && window.Contains("t.GetPriceFor(TradeAction.PlayerBuys)") && !Regex.IsMatch(window, @"\b0\.6f\b|\b1\.4f\b|GetPricePlayer"),
            "every price is the game's own: the price shown is the price paid");
        C(window.Contains("deal.TryExecute(out bool actuallyTraded)") && window.Contains("deal.DoesTraderHaveEnoughSilver()") && window.Contains("\"ConfirmTraderShortFunds\"")
          && window.Contains("\"MessageColonyCannotAfford\"") && window.Contains("RecacheInventory()"), "Accept is the game's own, with its confirmations");
        C(window.Contains("TradeUtility.ReceiveQuestFromTrader(pawn, TradeSession.playerNegotiator)") && window.Contains("if (switching)"),
            "a trader's quest is handed over when the trade ends, not at a switch");
        C(!Regex.IsMatch(window + opener, @"\.ForceTo\(|\.ForceToSource\(|\.ForceToDestination\(|ResolveTrade|GiveSoldThing"), "nothing moves a count or a good but the game's own control and Accept");
        C(window.Contains("CollectionsMassCalculator.MassUsageLeftAfterTradeableTransfer") && window.Contains("CaravanUIUtility.DrawCaravanInfo")
          && window.Contains("DaysWorthOfFoodCalculator.ApproxDaysWorthOfFoodLeftAfterTradeableTransfer"), "a caravan's mass and food days are the game's own");
        C(window.Contains("TradeScreen.Fill(null)") && window.Contains("TradeScreen.Clear(null)") && window.Contains("TradeAdvice.All") && window.Contains("TradeMarket.Known(")
          && window.Contains("TradeMarket.ImpactLine(") && window.Contains("TradeMarketMath.Best(") && window.Contains("TradeScreen.ReadoutLines()"),
            "the advice, the basket, the market and the record are Trade/'s own, not worked out again");
        C(!Regex.IsMatch(window + opener + math, @"Scribe_|ExposeData|GameComponent"), "nothing is saved");
        C(screen.Contains("|| TradeWindow.Open != null") && screen.Contains("TradeWindow.Drawing) return;") && screen.Contains("if (TradeWindow.Takes) TradeWindow.DrawBackButton(__instance);"),
            "the old screen's parts know Rimfolk's window: open, drawing, and the way back");
        C(advice.Contains("UsualPrices(thing, factor, gain, loss, out n.usualSell, out n.usualBuy)") && advice.Contains("public static void UsualFor(Tradeable t"),
            "the usual price is the advice's own, kept on its notes");

        // ---------- The setting ----------
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs"));
        C(settings.Contains("public bool tradeWindow = true;") && settings.Contains("Scribe_Values.Look(ref tradeWindow, \"tradeWindow\", true);"), "on at first, and kept");
        C(catalog.Contains("Toggle(\"tradeWindow\", \"CC_Settings_TradeWindow\", \"CC_SetNote_TradeWindow\", \"deepHooks\");"
                           + "\n            Toggle(\"tradeSorts\""), "first under What's on, Trade screen, hanging on the deeper game hooks");

        // ---------- The words ----------
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradeWindow.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        string code = window + opener;
        var used = Regex.Matches(code, "\"(CC_TradeWin_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        // Built from a name: a kind's chip, a column's header and tooltip, a grade's word.
        var built = new List<string>();
        foreach (TradeCategory k in Enum.GetValues(typeof(TradeCategory))) built.Add("CC_TradeWin_Cat_" + k);
        foreach (TradeColumn k in Enum.GetValues(typeof(TradeColumn)))
            if (k != TradeColumn.Count)
            {
                built.Add("CC_TradeWin_Col_" + k);
                built.Add("CC_TradeWin_Col_" + k + "Tip");
            }
        foreach (var g in new[] { DealGrade.Great, DealGrade.Good, DealGrade.Fair, DealGrade.Poor })
        {
            built.Add("CC_TradeWin_Grade_" + g + "Sell");
            built.Add("CC_TradeWin_Grade_" + g + "Buy");
        }
        C(code.Contains("\"CC_TradeWin_Cat_\" + ") && code.Contains("\"CC_TradeWin_Col_\" + c") && code.Contains("\"CC_TradeWin_Grade_\" + g"), "the built names are built so");
        C(used.Count >= 40, $"found the window's words ({used.Count})");
        foreach (var k in used.Concat(built).Distinct()) C(keyed.ContainsKey(k), $"{k} exists");
        foreach (var k in keyed.Keys.Where(k => k.StartsWith("CC_TradeWin_"))) C(used.Contains(k) || built.Contains(k), $"{k} is said somewhere");
        C(keyed.ContainsKey("CC_Settings_TradeWindow") && keyed.ContainsKey("CC_Settings_TradeWindow_Tip") && keyed.ContainsKey("CC_SetNote_TradeWindow"), "the setting has its label, tooltip and note");
        foreach (var kv in keyed)
        {
            C(!kv.Value.Contains('\u2014') && !kv.Value.Contains('!'), $"{kv.Key} has no em dash or exclamation mark");
            foreach (Match m in Regex.Matches(kv.Value, @"\{([A-Z]+)\}"))
                C(Regex.IsMatch(code, "\"" + kv.Key + "\"[^;]*\"" + m.Groups[1].Value + "\"", RegexOptions.Singleline), $"{kv.Key}'s {{{m.Groups[1].Value}}} is given");
        }
        // The window's words of the old screen are the old screen's own keys.
        var screenKeys = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradeScreen.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        foreach (Match m in Regex.Matches(code, "\"(CC_Trade_[A-Za-z_]+)\"")) C(screenKeys.Contains(m.Groups[1].Value), $"{m.Groups[1].Value} is the old screen's own word");
        C(!keyed.Keys.Any(screenKeys.Contains), "no key in both files");
        return n;
    }
}
