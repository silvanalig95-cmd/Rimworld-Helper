using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The trade screen's market and the colony's trade record (Source/ColonyCouncil/Trade/TradeMarketMath.cs and the files
/// around it): a good's market value against its usual worth and its 30 days' average, its change over 7 and 30 days
/// read as Vanilla Trading Expanded reads it, its 60 days' high and low, what a notable move is, what VTE's own rule for
/// the colony's trades makes of a trade, the market's reasons for a mark, the record's caps and its best and last
/// prices; and from the files, that VTE is read by name only, the record is written on the game's own trade hook, the
/// new hooks stand down with the deeper game hooks, the price change sort is VTE's only and stays out of the caravan
/// windows, no letters or messages, the settings and the save size model, and every word.
/// </summary>
public static class TradeMarketTests
{
    private static bool Near(float a, float b, float eps = 0.0005f) => Math.Abs(a - b) <= eps;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "trade market: " + what);
        }

        // ---------- The market's history ----------
        var none = TradeMarketMath.Stats(new List<float>(), 2f, 2f);
        C(!none.known && none.heading == 0 && TradeMarketMath.MoveScore(none) < 0f, "no history: nothing is said");
        C(!TradeMarketMath.Stats(null, 2f, 2f).known && !TradeMarketMath.Stats(new List<float> { 2f }, 0f, 2f).known, "no list, or no price now: nothing is said");
        // 60 days at 2, then rising to 2.6 over the last 10.
        var rising = Enumerable.Repeat(2f, 50).Concat(Enumerable.Range(1, 10).Select(i => 2f + 0.06f * i)).ToList();
        var s = TradeMarketMath.Stats(rising, 2.6f, 2f);
        C(s.known && s.days == 60 && Near(s.vsUsual, 0.3f), "against the usual: 2.6 against 2 is 30% above");
        C(s.weekDays == 7 && Near(s.week, 2.6f / rising[53] - 1f), "the week's change is from the price kept 7 records back, as VTE's terminal reads it");
        C(s.monthDays == 30 && Near(s.month, 0.3f), "the month's from 30 records back");
        C(s.averageDays == 30 && Near(s.average, rising.Skip(30).Average()) && s.vsAverage > 0f, "the average is of the last 30 days");
        C(Near(s.high, 2.6f) && Near(s.low, 2f) && s.rangeDays == 60 && s.atHigh && !s.atLow, "the 60 days' high and low, and at its high now");
        C(s.heading == 1 && TradeMarketMath.Heading(s) == 1, "a tenth up in a week is a rise");
        // A long history: only the last 60 days count for the high and low.
        var old = Enumerable.Repeat(9f, 100).Concat(Enumerable.Repeat(2f, 60)).ToList();
        var so = TradeMarketMath.Stats(old, 2f, 2f);
        C(Near(so.high, 2f) && Near(so.low, 2f) && !so.atHigh && !so.atLow && so.heading == 0, "older than 60 days is not counted, and a flat price is at neither end");
        // A short history: the change is over what there is.
        var shortH = new List<float> { 2f, 2f, 2.1f };
        var ss = TradeMarketMath.Stats(shortH, 2.4f, 2f);
        C(ss.weekDays == 3 && ss.monthDays == 3 && Near(ss.week, 0.2f), "a short history: the change is over the days there are");
        C(ss.heading == 1, "a fifth up over 3 days is a rise (3 days at least for a week's move)");
        var two = TradeMarketMath.Stats(new List<float> { 2f, 2.3f }, 2.3f, 2f);
        C(two.heading == 0 && !two.atHigh, "two days are too few to call a move or a high");
        var one = TradeMarketMath.Stats(new List<float> { 2f }, 2.5f, 2f);
        C(one.known && one.weekDays == 0 && one.monthDays == 0 && TradeMarketMath.MoveScore(one) < 0f, "one price: no change to tell");
        // Falling by the month only.
        var slow = Enumerable.Range(0, 30).Select(i => 3f - 0.025f * i).ToList();
        var sf = TradeMarketMath.Stats(slow, 2.25f, 3f);
        C(Math.Abs(sf.week) < TradeMarketMath.WeekMove && sf.month <= -TradeMarketMath.MonthMove && sf.heading == -1, "a fifth down over a month is a fall, though no week was a tenth");
        C(sf.atLow && !sf.atHigh, "and at its lowest of those days");
        var calm = Enumerable.Range(0, 30).Select(i => 2f + (i % 2 == 0 ? 0.02f : -0.02f)).ToList();
        C(TradeMarketMath.Stats(calm, 2f, 2f).heading == 0, "a price that wavers a little has no heading");
        C(Near(TradeMarketMath.MoveScore(sf), Math.Abs(sf.month)) && TradeMarketMath.MoveScore(s) >= TradeMarketMath.MoveScore(TradeMarketMath.Stats(calm, 2f, 2f)),
            "the price change sort: the bigger move first, either way");
        C(TradeMarketMath.Percent(0.183f) == 18 && TradeMarketMath.Percent(-0.05f) == -5 && TradeMarketMath.Percent(0.125f) == 13, "a change in whole percent");
        C(TradeMarketMath.IsSteady(0.019f) && TradeMarketMath.IsSteady(-0.01f) && !TradeMarketMath.IsSteady(0.02f), "steady under 2%");
        var pts = TradeMarketMath.Points(old, 2.1f);
        C(pts.Count == 61 && Near(pts[^1], 2.1f) && Near(pts[0], 2f), "the drawn line: the last 60 days and now");
        C(TradeMarketMath.Points(null, 2f).Count == 1, "with no history, only now");

        // ---------- This trade's own effect, by VTE's rule ----------
        C(Near(TradeMarketMath.Chance(100f), 0.1f) && Near(TradeMarketMath.Chance(-500f), 0.5f) && TradeMarketMath.Chance(2500f) == 1f,
            "VTE's chance: the market worth traded over 1,000 silver, certain from 1,000");
        C(TradeMarketMath.Steps(500f) == 1f && TradeMarketMath.Steps(2500f) == 2.5f && TradeMarketMath.Steps(50000f) == 10f, "its step: one up to 1,000 silver, ten at most");
        C(TradeMarketMath.Level(50f, 1f) == ImpactLevel.None && TradeMarketMath.Level(150f, 1f) == ImpactLevel.May && TradeMarketMath.Level(-500f, 1f) == ImpactLevel.Likely
          && TradeMarketMath.Level(900f, 1f) == ImpactLevel.Sure, "the words' thresholds: may from a tenth, likely from two in five, almost surely from four in five");
        C(TradeMarketMath.Level(5000f, 0f) == ImpactLevel.None, "with VTE's own setting at nothing, no trade moves a price");
        var sell = TradeMarketMath.Impact(0f, 600f, 1f);
        C(sell.level == ImpactLevel.Likely && sell.way == -1 && !sell.lately && !sell.evensOut && !sell.sharp, "a sale of 600 silver's worth likely pushes its price down");
        var buy = TradeMarketMath.Impact(0f, -1200f, 1f);
        C(buy.level == ImpactLevel.Sure && buy.way == 1 && !buy.sharp, "a purchase of 1,200 almost surely pushes it up");
        C(TradeMarketMath.Impact(0f, 3500f, 1f).sharp && !TradeMarketMath.Impact(0f, 2000f, 1f).sharp && TradeMarketMath.Impact(0f, 1500f, 2f).sharp,
            "sharply from three of VTE's steps, its setting counted");
        C(TradeMarketMath.Impact(0f, 40f, 1f).level == ImpactLevel.None && TradeMarketMath.Impact(0f, 40f, 1f).way == 0, "a small trade: nothing said");
        var adds = TradeMarketMath.Impact(300f, 300f, 1f);
        C(adds.level == ImpactLevel.Likely && adds.way == -1 && !adds.lately, "this trade adds to what was sold lately");
        var evens = TradeMarketMath.Impact(300f, -300f, 1f);
        C(evens.evensOut && evens.level == ImpactLevel.None, "a purchase against as much sold lately evens it out");
        var some = TradeMarketMath.Impact(900f, -500f, 1f);
        C(some.evensOut && some.level == ImpactLevel.Likely && some.way == -1 && some.lately, "against more sold lately: some of it evened out, the earlier sales still decide");
        var outweighed = TradeMarketMath.Impact(-2000f, 500f, 1f);
        C(outweighed.lately && outweighed.way == 1 && !outweighed.evensOut && outweighed.level == ImpactLevel.Sure,
            "a small sale after a big purchase: the purchase still pushes it up, and says so");
        var latelyOnly = TradeMarketMath.Impact(-450f, 0f, 1f);
        C(latelyOnly.lately && latelyOnly.level == ImpactLevel.Likely && latelyOnly.way == 1, "nothing of it in this trade: what was bought lately is told");
        C(TradeMarketMath.Impact(-50f, 0f, 1f).level == ImpactLevel.None && TradeMarketMath.Impact(-50f, 0f, 1f).way == 0, "and nothing when that was little");

        // ---------- The market's reasons for a mark ----------
        C(TradeMarketMath.Hint(s, true) == MarketHint.HighSellNow && TradeMarketMath.Hint(s, false) == MarketHint.RisingBuySooner,
            "at a 60-day high: a good sale now; a buy is told it rose");
        C(TradeMarketMath.Hint(sf, false) == MarketHint.LowBuyNow && TradeMarketMath.Hint(sf, true) == MarketHint.FallingSellSooner,
            "falling to its low: a good buy now; a sale is better made before it falls more");
        var highOnly = TradeMarketMath.Stats(Enumerable.Repeat(2f, 25).Concat(Enumerable.Repeat(2.4f, 35)).ToList(), 2.4f, 2f);
        C(highOnly.atHigh && highOnly.heading == 0 && TradeMarketMath.Hint(highOnly, false) == MarketHint.HighBuyCanWait, "a buy at a steady high: what can wait may cost less later");
        var lowOnly = TradeMarketMath.Stats(Enumerable.Repeat(2.4f, 40).Concat(Enumerable.Repeat(2f, 20)).ToList(), 2f, 2f);
        C(lowOnly.atLow && lowOnly.heading == 0 && TradeMarketMath.Hint(lowOnly, true) == MarketHint.LowSellCanWait, "a sale at a steady low: a later trader may pay more");
        C(TradeMarketMath.Hint(so, true) == MarketHint.None && TradeMarketMath.Hint(null, false) == MarketHint.None, "a flat price, or none: no reason given");

        // ---------- The record's caps and prices ----------
        C(TradeMarketMath.Overflow(TradeMarketMath.MaxLinesPerGood + 2) == 2 && TradeMarketMath.Overflow(3) == 0, "a good keeps its newest few lines");
        C(TradeMarketMath.ToDrop(Enumerable.Range(0, TradeMarketMath.MaxGoods).ToList()) == -1, "room for the cap's goods");
        var ticks = Enumerable.Range(0, TradeMarketMath.MaxGoods + 1).Select(i => 1000 + i).ToList();
        ticks[17] = 5;
        C(TradeMarketMath.ToDrop(ticks) == 17, "past the cap, the good traded longest ago goes first");
        int day = TradeMarketMath.TicksADay;
        var lines = new List<TradeRecordEntry>
        {
            new TradeRecordEntry(40 * day, true, 3.0f),
            new TradeRecordEntry(38 * day, true, 3.4f),
            new TradeRecordEntry(20 * day, true, 5.0f),
            new TradeRecordEntry(39 * day, false, 4.2f),
            new TradeRecordEntry(35 * day, false, 3.9f)
        };
        int now = 41 * day;
        C(TradeMarketMath.Last(lines, true) == 0 && TradeMarketMath.Last(lines, false) == 3, "the last price is the newest line of its side");
        C(TradeMarketMath.Best(lines, true, now) == 1, "the best sale is the highest of the last 15 days (an older, higher one not counted)");
        C(TradeMarketMath.Best(lines, false, now) == 4, "the best purchase is the lowest of the last 15 days");
        C(TradeMarketMath.Best(lines, true, 60 * day) == -1 && TradeMarketMath.Last(new List<TradeRecordEntry>(), true) == -1, "none within the days, none said");
        var quality = new List<TradeRecordEntry>
        {
            new TradeRecordEntry(40 * day, true, 90f, 2),
            new TradeRecordEntry(39 * day, true, 230f, 4),
            new TradeRecordEntry(38 * day, true, 260f, 4)
        };
        C(TradeMarketMath.Last(quality, true, 4) == 1 && TradeMarketMath.Best(quality, true, now, 4) == 2 && TradeMarketMath.Best(quality, true, now, 2) == 0,
            "a good with a quality: the last and the best of the same quality");
        C(TradeMarketMath.Last(quality, true, 6) == 0, "with none of that quality, the last of any");
        C(TradeMarketMath.DaysAgo(now - 3 * day - 5, now) == 3 && TradeMarketMath.DaysAgo(now + 9, now) == 0, "whole days ago");
        C(Near(TradeMarketMath.MergedPrice(2f, 10, 4f, 30), 3.5f) && TradeMarketMath.MergedPrice(2f, 0, 4f, 0) == 0f, "two rows of one good in a trade: one line at the average price");
        C(TradeMarketMath.Who(new string('x', 80)).Length == TradeMarketMath.MaxWho + 3 && TradeMarketMath.Who(null) == "" && TradeMarketMath.Who(" Kanata ") == "Kanata",
            "a trader's name is cut to fit");

        // ---------- The files ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string market = File.ReadAllText(Path.Combine(src, "Trade", "TradeMarket.cs"));
        string window = File.ReadAllText(Path.Combine(src, "Trade", "Window_TradeMarket.cs"));
        string record = File.ReadAllText(Path.Combine(src, "Trade", "TradeRecord.cs"));
        string math = File.ReadAllText(Path.Combine(src, "Trade", "TradeMarketMath.cs"));
        string screen = File.ReadAllText(Path.Combine(src, "Trade", "TradeScreen.cs"));
        string advice = File.ReadAllText(Path.Combine(src, "Trade", "TradeAdvice.cs"));
        string sorts = File.ReadAllText(Path.Combine(src, "Trade", "TradeSorts.cs"));
        string vte = File.ReadAllText(Path.Combine(src, "Integrations", "VanillaTrading.cs"));
        string records = File.ReadAllText(Path.Combine(src, "Epithets", "CouncilRecords.cs"));
        var keyed0 = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradeScreen.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "the rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Trade/TradeMarketMath.cs"), "and are linked into the tests");
        foreach (var name in new[] { "priceHistoryRecorders", "\"records\"", "thingsAffectedBySoldPurchasedMarketValue", "allNews", "priceImpactStartTick", "AffectedThingDefs",
                     "playerTransactionImpactMultiplier", "VanillaTradingExpanded.VanillaTradingExpandedMod" })
            C(vte.Contains(name), $"VTE's {name.Trim('"')} is read by name");
        C(!vte.Contains("using VanillaTradingExpanded") && !market.Contains("VanillaTradingExpanded") && !window.Contains("VanillaTradingExpanded"),
            "nothing of VTE is referenced outside its by-name reads");
        C(Regex.Matches(vte, @"\.SetValue\(").Count == 2 && Regex.IsMatch(vte, @"finally\s*\{\s*vanillaOnly\.SetValue\(null, was\);"),
            "the one VTE switch Rimfolk sets (for a good's usual worth) is set back at once, in one place");
        C(!Regex.IsMatch(vte, @"(priceModifiers|recorders|records|pending|allNews|impactSetting|modSettings)\.SetValue") && !Regex.IsMatch(vte + market, @"(history|points|records)\.(Add|Remove|Clear|Insert|Sort)\("),
            "VTE's market, history, trades and news are only read");
        C(Regex.IsMatch(market, @"\[HarmonyPatch\(typeof\(TradeUI\), nameof\(TradeUI\.DrawTradeableRow\)\)\]\s*public static class Patch_TradeMarketCard"),
            "the card is a Harmony patch class of its own");
        C(market.Contains("public static bool On => RimfolkPatches.On && (S?.tradeMarket") && record.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tradeRecord"),
            "the card, the trend line, the button and the record stand down with the deeper game hooks and their settings");
        C(records.Contains("public static void Prefix() => TradeRecord.Take();") && records.Contains("TradeRecord.Write(__result && actuallyTraded);"),
            "the record is taken on the game's own trade hook (the negotiator's count's), and written only when the trade went through");
        C(record.Contains("TradeSession.giftMode") && record.Contains("TradeCurrency.Silver") && record.Contains("t is Tradeable_Pawn"), "silver trades only, never gifts, people or animals");
        C(!Regex.IsMatch(market + window + record, @"Messages\.Message|LetterStack|Find\.LetterStack"), "no letters or messages");
        C(!Regex.IsMatch(market + window + record, @"AdjustTo|TryExecute\(|ResolveTrade"), "the market only shows: it sets no counts and trades nothing");
        C(screen.Contains("TradeMarket.DrawTrendMark(t, rect, ref curX)") && screen.Contains("TradeMarket.DrawMarketButton(rect, ref curX)"),
            "the trend line and the Market button are drawn with the row's own icons");
        C(screen.Contains("TradeMarket.History ? all : all.Where(d => d?.comparerClass != typeof(TransferableComparer_PriceChange))"),
            "the price change sort is offered only with VTE's history");
        C(screen.Contains("return all.Where(d => d?.comparerClass == null || !typeof(TransferableComparer_Rimfolk).IsAssignableFrom(d.comparerClass))")
          && Regex.IsMatch(sorts, @"public class TransferableComparer_PriceChange : TransferableComparer_Rimfolk"),
            "and like the other sorts it is left out of the caravan and transport pod windows");
        C(advice.Contains("n.trend = TradeMarket.HintLine(t.ThingDef, n.mark == TradeMark.Sell)") && advice.Contains("if (n.mark == TradeMark.Sell || n.mark == TradeMark.Buy) n.trend"),
            "the market's reasons go with a sale or a buy already marked, never a mark of their own");
        C(advice.Contains("TradeMarket.ImpactLine(t.ThingDef)") && market.Contains("CC_Trade_Impact_Evens"), "the mark's tooltip says what this trade does to the price");
        C(!market.Contains("TradeMarketMath.Chance(") && !market.Contains("TradeMarketMath.Steps(") && !keyed0.Where(kv => kv.Key.StartsWith("CC_Trade_Impact")).Any(kv => Regex.IsMatch(kv.Value, @"\d|\{PCT\}")),
            "what a trade does to the price is told in words, no numbers: VTE's rule is a chance");
        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_TradeSorts.xml")).Root!.Elements("TransferableSorterDef").ToList();
        C(defs.Any(d => d.Element("defName")?.Value == "Rimfolk_PriceChange" && d.Element("comparerClass")?.Value == "ColonyCouncil.TransferableComparer_PriceChange"),
            "the price change sort is a def of the game's own kind");

        // ---------- The settings ----------
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs"));
        foreach (var f in new[] { "tradeMarket", "tradeRecord", "tradeVteHistory" })
        {
            C(settings.Contains($"public bool {f} = true;") && settings.Contains($"Scribe_Values.Look(ref {f}, \"{f}\", true);"), $"{f} is on at first and kept");
            C(catalog.Contains($"Toggle(\"{f}\""), $"{f} is in the settings window");
        }
        C(catalog.Contains("Toggle(\"tradeMarket\", \"CC_Settings_TradeMarket\", \"CC_SetNote_TradeMarket\", \"deepHooks\")")
          && catalog.Contains("Toggle(\"tradeRecord\", \"CC_Settings_TradeRecord\", \"CC_SetNote_TradeRecord\", \"deepHooks\")"),
            "the card and the record hang on the deeper game hooks, beside the trade screen's other switches");
        C(catalog.Contains("Mod(Toggle(\"tradeVteHistory\", \"CC_Settings_TradeVteHistory\", \"CC_SetNote_TradeVteHistory\", \"tradeVte\"), VteId)"),
            "the history's switch is under Vanilla Trading Expanded on the page of your other mods");

        // ---------- The save ----------
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("\"Trade/TradeRecord.cs:goods\"") && estimate.Contains("\"Trade/TradeRecord.cs:lines\"") && estimate.Contains("MaxGoods") && estimate.Contains("MaxLinesPerGood"),
            "the record is modelled in the save size estimate, its caps read from the code");
        C(record.Contains("public TradeBook(Game game)") && record.Contains("goods ??= new List<TradeGoodLog>()"), "a save from before gets an empty record");

        // ---------- The words ----------
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TradeScreen.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        string code = market + window + record + advice;
        var used = Regex.Matches(code, "\"(CC_Trade_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 60, $"found the market's words ({used.Count})");
        foreach (var k in used) C(keyed.ContainsKey(k), $"{k} exists");
        foreach (var k in new[] { "CC_Settings_TradeMarket", "CC_Settings_TradeRecord", "CC_Settings_TradeVteHistory" })
            C(keyed.ContainsKey(k) && keyed.ContainsKey(k + "_Tip") && keyed.ContainsKey(k.Replace("CC_Settings_", "CC_SetNote_")), $"{k} has its tooltip and note");
        foreach (Match m in Regex.Matches(window, "Add\\(Col\\.[A-Za-z]+, \"(CC_Trade_Market_Col_[A-Za-z]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value) && keyed.ContainsKey(m.Groups[1].Value + "Tip"), $"{m.Groups[1].Value} has its header and tooltip");
        foreach (var kv in keyed.Where(kv => used.Contains(kv.Key)))
            foreach (Match m in Regex.Matches(kv.Value, @"\{([A-Z]+)\}"))
                C(Regex.IsMatch(code, "\"" + kv.Key + "\"[^;]*\"" + m.Groups[1].Value + "\"", RegexOptions.Singleline), $"{kv.Key}'s {{{m.Groups[1].Value}}} is given");
        return n;
    }
}
