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
/// price is of its value and of the usual, the order a column's click puts the rows in and the kinds they are grouped
/// under, what a deal comes to, the best deals, the two lists side by side or as tabs at 1920, 1600 and 1280 pixels wide
/// and at the window's least with the columns that fit each, the rows' heights, the chips' rows, the scroll to a row, the
/// one-way counts and the kinds; and from the files, that the window opens in place of the game's screen only for a
/// silver trade with the setting and the deeper hooks on, uses the game's own prices, count limits and Accept, falls
/// back to the game's screen, switches both ways on the same deal, draws its rows from what it read, saves nothing but
/// the lists' layout and tab with the settings, and says every word from its own Keyed file.
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
        C(TradeWindowMath.SideKey(Sale(1.25f)) > TradeWindowMath.SideKey(Sale(1.19f)) && TradeWindowMath.SideKey(Sale(1.19f)) > TradeWindowMath.SideKey(Sale(1.1f))
          && TradeWindowMath.SideKey(default) == null && TradeWindowMath.SideKey(Sale(1.4f, kept: true)) < TradeWindowMath.SideKey(Sale(1.1f))
          && TradeWindowMath.SideKey(Buy(0.8f)) > TradeWindowMath.SideKey(Buy(0.95f)),
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
        C(Names(TradeColumn.Deal, true).First() == "wood" && Names(TradeColumn.Deal, true).Last() == "Arrow", "the deal column: the best first, the ungraded last");
        C(Names(TradeColumn.Need, true).SequenceEqual(new[] { "wood", "steel", "Arrow", "Beer" }), "the need column: the advice's order, the rest last");
        C(Names(TradeColumn.Ours, true)[0] == "steel" && Names(TradeColumn.Theirs, true).SequenceEqual(new[] { "steel", "Beer", "Arrow", "wood" }) && Names(TradeColumn.Mass, true)[0] == "Arrow",
            "the counts and the mass");
        C(!TradeWindowMath.DefaultDescending(TradeColumn.Name) && TradeWindowMath.DefaultDescending(TradeColumn.Deal) && TradeWindowMath.DefaultDescending(TradeColumn.Value),
            "a column's first click: names from A, numbers the highest first");
        C(!TradeWindowMath.Sortable(TradeColumn.Count) && Enum.GetValues(typeof(TradeColumn)).Cast<TradeColumn>().Count(TradeWindowMath.Sortable) == 11,
            "every sort but the count control: the lists' columns, value, the market, its trend and mass");
        // Grouped under the kinds, each kind's rows in the column's order, what the trader won't deal in last.
        var grouped = new List<TradeRowKeys>
        {
            new TradeRowKeys { name = "wood", category = (int)TradeCategory.Raw, pay = 0.8f },
            new TradeRowKeys { name = "pemmican", category = (int)TradeCategory.Food, pay = 1.5f },
            new TradeRowKeys { name = "steel", category = (int)TradeCategory.Raw, pay = 1.3f },
            new TradeRowKeys { name = "Beer", category = (int)TradeCategory.Drugs, off = true },
            new TradeRowKeys { name = "Apple", category = (int)TradeCategory.Food, pay = 0.9f }
        };
        string Lines(TradeColumn c, bool d) => string.Join(",", TradeWindowMath.Grouped(grouped, c, d).Select(l => l.row < 0 ? "#" + l.group + ":" + l.count : grouped[l.row].name));
        C(Lines(TradeColumn.Name, false) == "#0:2,Apple,pemmican,#2:2,steel,wood,#-1:1,Beer", $"under each kind, food first, by name; what they won't deal in last ({Lines(TradeColumn.Name, false)})");
        C(Lines(TradeColumn.Pay, true) == "#0:2,pemmican,Apple,#2:2,steel,wood,#-1:1,Beer", "a header's sort orders each kind's rows, the kinds stay in their order");
        C(TradeWindowMath.Grouped(null, TradeColumn.Name, false).Count == 0 && TradeWindowMath.Grouped(new List<TradeRowKeys>(), TradeColumn.Name, false).Count == 0, "no rows, no lines");
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

        // ---------- The two lists ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string window = File.ReadAllText(Path.Combine(src, "Trade", "Window_RimfolkTrade.cs"));
        var sellCols = TradeWindowMath.ListColumns(true);
        var buyCols = TradeWindowMath.ListColumns(false);
        C(sellCols.Select(x => x.col).SequenceEqual(new[] { TradeColumn.Name, TradeColumn.Ours, TradeColumn.Pay, TradeColumn.Deal, TradeColumn.Need, TradeColumn.Count })
          && buyCols.Select(x => x.col).SequenceEqual(new[] { TradeColumn.Name, TradeColumn.Theirs, TradeColumn.Ask, TradeColumn.Deal, TradeColumn.Need, TradeColumn.Count }),
            "each list: the picture and name, how many there are, the price, the deal, the need, the count; ours and what they pay to sell, theirs and what they ask to buy");
        C(sellCols.Zip(buyCols, (x, y) => x.width == y.width && x.priority == y.priority).All(x => x), "both lists alike in width");
        C(sellCols.Where(x => x.priority <= 0).Select(x => x.col).SequenceEqual(new[] { TradeColumn.Name, TradeColumn.Pay, TradeColumn.Count })
          && sellCols.OrderByDescending(x => x.priority).Select(x => x.col).Take(3).SequenceEqual(new[] { TradeColumn.Need, TradeColumn.Ours, TradeColumn.Deal }),
            "the name, the price and the count always; in a narrow list the need gives way first, then how many, then the deal");
        float all = TradeWindowMath.ColumnsWidth(sellCols, TradeWindowMath.ColGap, false), essentials = TradeWindowMath.ColumnsWidth(sellCols, TradeWindowMath.ColGap, true);
        C(Near(all, 176f + 44f + 80f + 60f + 70f + 136f + 5 * 4f) && Near(essentials, 176f + 80f + 136f + 2 * 4f), $"every column {all:0}, the essentials {essentials:0}");
        // The window's own widths: the screen less 24 (at most 1800), less its body's insets (10 a side in Rimfolk's look, 18
        // in the game's), less the side (300 from 1500, else 240) and its gap.
        float Table(float screen, bool own)
        {
            float inner = WindowFrameMath.BodyWidth(Math.Min(screen - 24f, 1800f), own);
            return inner - (inner >= 1500f ? 300f : 240f) - 14f;
        }
        float leastTable = WindowFrameMath.BodyWidth(WindowFrameMath.TradeLeastW, true);
        leastTable -= (leastTable >= 1500f ? 300f : 240f) + 14f;
        foreach (bool own in new[] { true, false })
        {
            string look = own ? "Rimfolk's look" : "the game's look";
            C(TradeWindowMath.SideBySide(Table(1920f, own), TradeListsChoice.Auto), $"1920 wide, {look}: the two lists side by side");
            C(TradeWindowMath.SideBySide(Table(1600f, own), TradeListsChoice.Auto), $"1600 wide, {look}: side by side still");
            C(!TradeWindowMath.SideBySide(Table(1280f, own), TradeListsChoice.Auto), $"1280 wide, {look}: tabs");
            C(TradeWindowMath.SideBySide(Table(1280f, own), TradeListsChoice.SideBySide), $"1280 wide, {look}: side by side when chosen, with the name, the price and the count");
            C(!TradeWindowMath.SideBySide(Table(1920f, own), TradeListsChoice.Tabs), $"1920 wide, {look}: tabs when chosen");
        }
        C(!TradeWindowMath.SideBySide(leastTable, TradeListsChoice.Auto) && !TradeWindowMath.SideBySide(leastTable, TradeListsChoice.SideBySide),
            $"the window at its least: tabs, even when side by side is chosen ({leastTable:0} wide)");
        foreach (var (label, table, sideBy) in new[]
                 {
                     ("1920 side by side", Table(1920f, true), true), ("1600 side by side", Table(1600f, true), true), ("1280 as tabs", Table(1280f, true), false),
                     ("1280 side by side by choice", Table(1280f, true), true), ("the least as tabs", leastTable, false), ("1920 in the game's look", Table(1920f, false), true)
                 })
        foreach (bool sale in new[] { true, false })
        {
            float rowsW = TradeWindowMath.RowsWidth(TradeWindowMath.ListWidth(table, sideBy));
            var placed = TradeWindowMath.Fit(TradeWindowMath.ListColumns(sale), rowsW, TradeWindowMath.ColGap);
            var cols = placed.Select(p => p.col).ToList();
            string what = label + (sale ? ", selling" : ", buying");
            float right = placed.Max(p => p.x + p.width);
            C(right <= rowsW + 0.5f, $"{what}: the columns fit ({right:0} of {rowsW:0})");
            C(placed.Zip(placed.Skip(1), (x, y) => y.x >= x.x + x.width + 3.9f).All(x => x), $"{what}: left to right, apart");
            C(cols.Contains(TradeColumn.Name) && cols.Contains(sale ? TradeColumn.Pay : TradeColumn.Ask) && cols.Contains(TradeColumn.Count), $"{what}: the name, the price and the count");
            float nameText = placed[0].width - TradeWindowMath.PictureW - TradeWindowMath.InfoW;
            C(nameText >= 110f, $"{what}: the name has {nameText:0} for its words, wrapped, never cut");
            bool full = label != "1280 side by side by choice";
            if (full) C(cols.Count == 6, $"{what}: every column ({string.Join(", ", cols)})");
            else C(cols.Contains(TradeColumn.Deal) && !cols.Contains(TradeColumn.Need), $"{what}: the deal kept, the need given way ({string.Join(", ", cols)})");
            if (label == "1920 side by side") C(nameText >= 220f, $"1920 side by side: room for long names ({nameText:0})");
        }
        var tiny = TradeWindowMath.Fit(sellCols, 300f, TradeWindowMath.ColGap);
        C(tiny.Any(p => p.col == TradeColumn.Name) && tiny.Any(p => p.col == TradeColumn.Count) && tiny.Any(p => p.col == TradeColumn.Pay), "however narrow, the name, the price and the count stay");
        C(TradeWindowMath.RowHeight(20f) == TradeWindowMath.RowMin && Near(TradeWindowMath.RowHeight(44f), 44f + TradeWindowMath.NamePad) && TradeWindowMath.RowHeight(0f) == TradeWindowMath.RowMin,
            "a row as tall as its wrapped name needs, at least its least");
        C(TradeWindowMath.RowMin >= 36f, "a row's least holds a price and its mark under it");
        var chips = TradeWindowMath.Flow(new[] { 50f, 60f, 70f, 40f }, 130f, 4f, 24f, out float chipsH);
        C(chips[0] == (0f, 0f) && chips[1] == (54f, 0f) && chips[2] == (0f, 28f) && chips[3] == (74f, 28f) && Near(chipsH, 52f), "the chips run on to a second row where they don't fit");
        TradeWindowMath.Flow(new[] { 50f }, 30f, 4f, 24f, out chipsH);
        C(Near(chipsH, 24f) && TradeWindowMath.Flow(null, 100f, 4f, 24f, out _).Count == 0, "a chip wider than the room stays on its row");
        C(Near(TradeWindowMath.ScrollToSpan(720f, 40f, 360f, 3600f), 720f - 160f) && TradeWindowMath.ScrollToSpan(40f, 40f, 360f, 3600f) == 0f
          && Near(TradeWindowMath.ScrollToSpan(3560f, 40f, 360f, 3600f), 3240f) && TradeWindowMath.ScrollToSpan(100f, 40f, 360f, 200f) == 0f,
            "a row found from the best deals shows in the middle, the list kept in bounds");

        // ---------- One way: each list's count ----------
        // The game's count: positive buys, negative sells; its limits here -8 (the colony has 8) and 5 (the trader has 5).
        C(TradeWindowMath.ThisWay(-3, true) == 3 && TradeWindowMath.ThisWay(-3, false) == 0 && TradeWindowMath.ThisWay(4, false) == 4 && TradeWindowMath.ThisWay(4, true) == 0,
            "each list counts its own way: sold in the sell list, bought in the buy list");
        C(TradeWindowMath.OtherWay(4, true) && TradeWindowMath.OtherWay(-3, false) && !TradeWindowMath.OtherWay(0, true) && !TradeWindowMath.OtherWay(-3, true), "a good in the deal the other way is told apart");
        C(TradeWindowMath.MostThisWay(true, -8, 5) == 8 && TradeWindowMath.MostThisWay(false, -8, 5) == 5 && TradeWindowMath.MostThisWay(true, 0, 5) == 0, "the most is all the colony has, or all the trader has");
        C(TradeWindowMath.Stepped(true, -3, true, 1, -8, 5) == -4 && TradeWindowMath.Stepped(true, -3, false, 1, -8, 5) == -2, "selling: one more, one back");
        C(TradeWindowMath.Stepped(true, -3, true, 10, -8, 5) == -8 && TradeWindowMath.Stepped(true, -3, false, 10, -8, 5) == 0, "ten with Shift, kept between none and all, never over to buying");
        C(TradeWindowMath.Stepped(false, 2, true, 1, -8, 5) == 3 && TradeWindowMath.Stepped(false, 2, true, 100, -8, 5) == 5 && TradeWindowMath.Stepped(false, 2, false, 100, -8, 5) == 0,
            "buying: one more, a hundred with Ctrl up to all they have, back to none and never over to selling");
        C(TradeWindowMath.Stepped(true, 0, true, int.MaxValue, -8, 5) == -8, "no overflow on a large step");
        C(TradeWindowMath.CountFor(true, 8, -8, 5) == -8 && TradeWindowMath.CountFor(false, 5, -8, 5) == 5 && TradeWindowMath.CountFor(true, 0, -8, 5) == 0,
            "a double arrow: all, or none");
        C(TradeWindowMath.CountFor(true, 30, -8, 5) == -8 && TradeWindowMath.CountFor(false, 30, -8, 5) == 5 && TradeWindowMath.CountFor(false, -4, -8, 5) == 0,
            "a number typed: kept between none and all there is");

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
        string Method(string head) => Regex.Match(window, Regex.Escape(head) + @".*?\n        \}", RegexOptions.Singleline).Value;
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
        string oneWay = Method("private void OneWay(") + Method("private static void Arrow(") + Method("private static void Field(") + Method("private static void OtherWay(");
        C(oneWay.Contains("int least = t.GetMinimumToTransfer(), most = t.GetMaximumToTransfer();") && oneWay.Contains("GenUI.CurrentAdjustmentMultiplier()")
          && Regex.Matches(oneWay, @"if \(!?[^\n]*CanAdjustTo\((target|0)\)\.Accepted\)").Count >= 3 && Regex.Matches(oneWay, @"\.AdjustTo\((target|0)\);").Count == 3,
            "counts are set within the game's own limits, by its own CanAdjustTo and AdjustTo, a step one, ten or a hundred as its multiplier says");
        C(oneWay.Contains("TradeWindowMath.OtherWay(c, sale)") && oneWay.Contains("TradeWindowMath.Stepped(") && oneWay.Contains("TradeWindowMath.CountFor(")
          && oneWay.Contains("bool single = top <= 1;") && oneWay.Contains("\"TraderWillNotTrade\"") && oneWay.Contains("\"NegotiatorWillNotTradeSlaves\""),
            "one way a list, never over to the other; one piece with single arrows; the game's words where it won't trade");
        // Drawn from what was read: the rows' words made when read, the names' heights measured once for a width.
        string drawn = Method("private void DrawRow(") + Method("private static void NameCell(") + Method("private static void PriceCell(") + Method("private static void DealTag(")
                       + Method("private static void NeedCell(") + Method("private static void CountCell(");
        C(drawn.Length > 2000 && !Regex.IsMatch(drawn, @"\bW\(|Money\(|CalcHeight|CalcSize|\.Translate\(|string\.Join|ToStringPercent|GetPriceFor"),
            "a row draws from what was read: no words made, no price asked, no text measured as it draws");
        C(Regex.Matches(window, @"Text\.CalcHeight\(line\.row\.label").Count == 1 && Method("private static void Measure(").Contains("if (Mathf.Abs(s.nameFor - w) > 0.5f)"),
            "a name's wrapping measured once for a width");
        C(Method("private Row Read(").Contains("r.payText") && Method("private void Refilter(").Length > 0 && window.Contains("PageMemory.SetEnum(ListsKey, chosen);")
          && window.Contains("PageMemory.Set(TabKey, sell ? \"sell\" : \"buy\");"), "the lists' layout and the tab kept with the settings");
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
        C(!Regex.IsMatch(window + opener + math, @"Scribe_|ExposeData|GameComponent"), "nothing is saved in the game");
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
        // A column's heading from its name (the need's and the count's by the list), and the tooltip of each the lists show.
        foreach (TradeColumn k in Enum.GetValues(typeof(TradeColumn)))
            if (k != TradeColumn.Count && k != TradeColumn.Need)
                built.Add("CC_TradeWin_Col_" + k);
        foreach (var k in new[] { "Name", "Ours", "Theirs", "Pay", "Ask", "Deal", "Need", "Keep", "ToSell", "ToBuy" }) built.Add("CC_TradeWin_Col_" + k + "Tip");
        foreach (var g in new[] { DealGrade.Great, DealGrade.Good, DealGrade.Fair, DealGrade.Poor })
        {
            built.Add("CC_TradeWin_Grade_" + g + "Sell");
            built.Add("CC_TradeWin_Grade_" + g + "Buy");
            built.Add("CC_TradeWin_Tag_" + g);
        }
        C(code.Contains("\"CC_TradeWin_Cat_\" + ") && code.Contains("\"CC_TradeWin_Col_\" + c") && code.Contains("\"CC_TradeWin_Grade_\" + g") && code.Contains("\"CC_TradeWin_Tag_\" + g")
          && code.Contains("W(key + \"Tip\")"), "the built names are built so");
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
