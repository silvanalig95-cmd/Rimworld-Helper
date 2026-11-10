using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Diplomacy's balance and realism (the player, 10 November 2026: a debt of honour "balanced, appropriate to the debt i
/// actually owe and not be repeatable all the time", a match "only ... when we have great diplomatic relations", warning
/// of raiders "only ... if the target colony is actually a potential target for these raiders and not too much time has
/// expired", and talk's news "logged in the ledger/history/diplomatic screen"): warnings by reach and recency and what
/// they are worth (World/WarnMath.cs), debts called in by their size and the people's means (Diplomacy/FavourMath.cs),
/// the gates of matches, wards, truces they pay, greetings' gifts, help, a pen friend's favour and something brought
/// next time, the news heard kept, every number against its rule, two simulated years, an older save carried over, and
/// the seams in the mod.
/// </summary>
public static class DiplomacyBalanceTests
{
    private const int D = 60000;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "diplomacy balance: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= 1. Warn only of a real threat to their town =================

        C(WarnMath.ReachTiles == 20f && WarnMath.ReachTiles == DealingMath.WarnTiles, "raiders' towns within 20 tiles of theirs (DealingMath.WarnTiles)");
        C(WarnMath.TerritoryTiles == RaidersCampMath.TerritoryTiles && WarnMath.TerritoryTiles == 30f, "their land within 30 tiles (RaidersCampMath.TerritoryTiles)");
        C(WarnMath.NearTiles == 12f && WarnMath.CloseTiles == 10f && WarnMath.WarTiles == 20f && WarnMath.SpreadTiles == 20f, "a camp or site within 12, a close town within 10, a war or a hardship within 20");
        C(WarnMath.RaidersSize(-1f, 12f, -1f, -1f) == 12 && WarnMath.RaidersSize(5f, 12.1f, -1f, -1f) == 10, "a camp or site of theirs within 12 tiles: 12");
        C(WarnMath.RaidersSize(10f, -1f, -1f, -1f) == 10 && WarnMath.RaidersSize(10.1f, -1f, -1f, -1f) == 8, "a town of theirs within 10 tiles: 10");
        C(WarnMath.RaidersSize(20f, -1f, -1f, -1f) == 8 && WarnMath.RaidersSize(20.1f, -1f, -1f, -1f) == 0, "a town within 20 tiles: 8; beyond, no threat");
        C(WarnMath.RaidersSize(-1f, -1f, 30f, -1f) == 8 && WarnMath.RaidersSize(-1f, -1f, 30.1f, -1f) == 0, "their land within 30 tiles: 8");
        C(WarnMath.RaidersSize(-1f, -1f, -1f, 20f) == 8 && WarnMath.RaidersSize(-1f, -1f, -1f, 20.1f) == 0, "the home they raided within 20 tiles of their town: 8");
        C(WarnMath.RaidersSize(-1f, -1f, -1f, -1f) == 0 && WarnMath.RaidersSize(50f, 40f, 40f, 40f) == 0, "nothing near: no threat, whoever they are (a people at war with everyone too)");
        C(WarnMath.RaidersWhy(true, true, false, true, 12) == WarnWhy.NotTheirEnemy && WarnMath.RaidersWhy(false, false, false, true, 12) == WarnWhy.NotTheirEnemy,
            "never their own raiders, never a people at peace with them");
        C(WarnMath.RaidersWhy(false, true, true, true, 12) == WarnWhy.Ended, "raiders no longer standing are no threat");
        C(WarnMath.RaidersWhy(false, true, false, false, 12) == WarnWhy.TooOld && WarnMath.RaidersWhy(false, true, false, true, 0) == WarnWhy.TooFar
          && WarnMath.RaidersWhy(false, true, false, true, 8) == WarnWhy.None, "too old, too far, or a warning");
        var warDefs = DefKinds(root);
        foreach (var d in new[] { "Council_WS_Warlord", "Council_WS_ClansGather", "Council_WS_BanditRoad", "Rimfolk_WS_SonBand" })
            C(warDefs.TryGetValue(d, out var k) && WarnMath.WarOrRoad(k.kind, k.bad), $"{d} is a war or road trouble to warn of");
        foreach (var kv in warDefs)
            if (WarnMath.WarOrRoad(kv.Value.kind, kv.Value.bad))
                C(kv.Value.bad && (kv.Value.kind == "war" || kv.Value.kind == "roads" || kv.Value.kind == "son"), $"{kv.Key}: only a bad war, road or warlord's son's band");
        C(!WarnMath.WarOrRoad("war", false) && !WarnMath.WarOrRoad("court", true), "a war that isn't bad or a court's trouble is no warning");
        C(WarnMath.WarWhy(true, true, 5f) == WarnWhy.NotTheirEnemy && WarnMath.WarWhy(false, false, 5f) == WarnWhy.NotTheirEnemy, "the warlord's own people are never warned of him (the old bug)");
        C(WarnMath.WarWhy(false, true, 20f) == WarnWhy.None && WarnMath.WarWhy(false, true, 20.1f) == WarnWhy.TooFar && WarnMath.WarWhy(false, true, -1f) == WarnWhy.TooFar,
            "a war near them: its attacker at war with them, within 20 tiles");
        foreach (var d in new[] { "Council_WS_Plague", "Council_WS_Murrain", "Council_WS_Locusts", "Rimfolk_WS_Blight", "Council_WS_GreatFire" })
            C(warDefs.TryGetValue(d, out var k) && WarnMath.Spreading(d, k.kind, k.bad), $"{d} spreads to its neighbours");
        foreach (var d in new[] { "Council_WS_Famine", "Council_WS_HardWinter", "Council_WS_Flood", "Council_WS_Drought", "Rimfolk_WS_ColdYear" })
            C(warDefs.TryGetValue(d, out var k) && !WarnMath.Spreading(d, k.kind, k.bad), $"{d} is felt where it falls, no warning");
        C(WarnMath.HardshipWhy(true, false, 2f) == WarnWhy.Theirs && WarnMath.HardshipWhy(false, true, 2f) == WarnWhy.Theirs, "never to the people already suffering it (the old bug)");
        C(WarnMath.HardshipWhy(false, false, 20f) == WarnWhy.None && WarnMath.HardshipWhy(false, false, 21f) == WarnWhy.TooFar, "a hardship spreading within 20 tiles");
        C(WarnMath.SiteWhy(false, true, 12f) == WarnWhy.None && WarnMath.SiteWhy(false, true, 12.5f) == WarnWhy.TooFar && WarnMath.SiteWhy(true, true, 1f) == WarnWhy.NotTheirEnemy
          && WarnMath.SiteWhy(false, false, 1f) == WarnWhy.NotTheirEnemy, "a hostile site within 12 tiles, not their own, not a friend's");

        // ================= 2. Recent or coming only =================

        C(WarnMath.RaidDays == 5f && WarnMath.Recent(0f) && WarnMath.Recent(5f) && !WarnMath.Recent(5.01f) && !WarnMath.Recent(-1f), "a raid on a home counts 5 days, not 30");
        C(WarnMath.KeepDays == 30f && WarnMath.LetGo(0, 31 * D, false, WarnMath.KeepDays) && !WarnMath.LetGo(0, 29 * D, false, WarnMath.KeepDays) && !WarnMath.LetGo(0, 90 * D, true, WarnMath.KeepDays),
            "a people's record kept 30 days, and while the threat stands");

        // ================= 3. The debt's size by what the warning was worth =================

        C(WarnMath.SizeWar == 14 && WarnMath.SizeNear == 12 && WarnMath.SizeClose == 10 && WarnMath.SizeFar == 8, "14 a war or a warlord's band, 12 a camp or site near, 10 a close town, 8 further");
        C(WarnMath.Size(ThreatKind.War, 0) == 14 && WarnMath.Size(ThreatKind.Site, 0) == 12 && WarnMath.Size(ThreatKind.Raiders, 10) == 10 && WarnMath.Size(ThreatKind.Hardship, 0) == 6,
            "each kind's size");
        C(WarnMath.MakesDebt(ThreatKind.Raiders, 10) && !WarnMath.MakesDebt(ThreatKind.Raiders, 8) && WarnMath.MakesDebt(ThreatKind.War, 14) && !WarnMath.MakesDebt(ThreatKind.Hardship, 30),
            "8 is a memory only; a hardship never a debt");
        C(WarnMath.StruckBonus == 4 && WarnMath.StruckDays == 15f && WarnMath.Struck(0, 15 * D) && !WarnMath.Struck(0, 15 * D + 1) && !WarnMath.Struck(10 * D, 9 * D),
            "4 more when the threat strikes them within 15 days of the warning");
        C(WarnMath.SizeWar + WarnMath.StruckBonus == 18 && FavourMath.WeightOf(18) == DebtWeight.Great && FavourMath.WeightOf(14) == DebtWeight.Debt && FavourMath.WeightOf(10) == DebtWeight.Small,
            "a warlord's band that struck is a great debt; a close raiders' warning a small one");
        C(LetterMath.HeededWarmly == WarnMath.SizeClose, "an older save's warning letter, read with no size, is a small debt of 10");
        string memory = Code("World/FactionMemory.cs");
        C(memory.Contains("[\"warned\"] = K(DeedSide.Favor, 6),"), "a warning's memory stands alone: kind answers no longer add up to a debt");

        // ================= 4. Calling in by the debt's size and their means =================

        C(FavourMath.GoodsPerSize == 30f, "goods worth 30 silver a point of the debt");
        C(Math.Abs(FavourMath.FortuneFactor(0f) - 0.75f) < 1e-4f && Math.Abs(FavourMath.FortuneFactor(50f) - 1f) < 1e-4f && Math.Abs(FavourMath.FortuneFactor(100f) - 1.25f) < 1e-4f
          && Math.Abs(FavourMath.FortuneFactor(-50f) - 0.75f) < 1e-4f, "their town's fortune: 0.75 to 1.25");
        C(Math.Abs(FavourMath.Means(WorldAge.Tribal) - 0.55f) < 1e-4f && Math.Abs(FavourMath.Means(WorldAge.Medieval) - 0.75f) < 1e-4f
          && Math.Abs(FavourMath.Means(WorldAge.Industrial) - 1f) < 1e-4f && Math.Abs(FavourMath.Means(WorldAge.Spacer) - 1.25f) < 1e-4f,
            "their means by their age (DealingMath.TheirLoadValue ÷ 400): 0.55, 0.75, 1, 1.25");
        C(Math.Abs(FavourMath.GoodsWorth(10, 50f, FavourMath.Means(WorldAge.Industrial)) - 300f) < 0.01f && Math.Abs(FavourMath.GoodsWorth(20, 100f, FavourMath.Means(WorldAge.Spacer)) - 937.5f) < 0.01f
          && Math.Abs(FavourMath.GoodsWorth(10, 0f, FavourMath.Means(WorldAge.Tribal)) - 123.75f) < 0.01f, "a warning's 300 at a steady industrial town; refugees' 937 at a flourishing spacer one; 124 from a poor tribe");
        C(FavourMath.Effective(10, true) == 5 && FavourMath.Effective(15, true) == 7 && FavourMath.Effective(15, false) == 15 && FavourMath.Effective(1, true) == 1, "a notable's personal favour gives half");
        C(FavourMath.WordGoodwill(10) == 5 && FavourMath.WordGoodwill(14) == 7 && FavourMath.WordGoodwill(20) == 10 && FavourMath.WordGoodwill(1) == 1, "a good word: half the size in goodwill");
        C(FavourMath.PeaceSize == 12 && FavourMath.FightersSize == 12 && FavourMath.CaptiveSize == 15, "peace help and fighters from 12, a colonist they hold from 15");
        C(FavourMath.Allows(FavourAsk.Goods, 1) && FavourMath.Allows(FavourAsk.Word, 1) && !FavourMath.Allows(FavourAsk.Peace, 11) && FavourMath.Allows(FavourAsk.Peace, 12)
          && !FavourMath.Allows(FavourAsk.Fighters, 11) && FavourMath.Allows(FavourAsk.Fighters, 12) && !FavourMath.Allows(FavourAsk.Captive, 14) && FavourMath.Allows(FavourAsk.Captive, 15),
            "what a debt of each size allows");
        C(!FavourMath.Allows(FavourAsk.Fighters, FavourMath.Effective(20, true)) && FavourMath.Allows(FavourAsk.Fighters, FavourMath.Effective(24, true)),
            "a personal favour asks for fighters only when it is twice as great");
        C(FavourMath.DebtFrom == 12 && FavourMath.GreatFrom == 18 && FavourMath.WeightOf(11) == DebtWeight.Small && FavourMath.WeightOf(12) == DebtWeight.Debt && FavourMath.WeightOf(17) == DebtWeight.Debt
          && FavourMath.WeightOf(18) == DebtWeight.Great, "a small debt below 12, a debt, a great debt from 18");
        string keys = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (DebtWeight w in Enum.GetValues(typeof(DebtWeight)))
            C(keys.Contains("<CC_Dip_Weight_" + w + ">"), $"the weight {w} in words");
        C(keys.Contains("<CC_Dip_Weight_Small>a small debt</CC_Dip_Weight_Small>") && keys.Contains("<CC_Dip_Weight_Debt>a debt</CC_Dip_Weight_Debt>")
          && keys.Contains("<CC_Dip_Weight_Great>a great debt</CC_Dip_Weight_Great>"), "the player's words: a small debt, a debt, a great debt");
        foreach (var k in new[] { "CC_Dip_Debt_Theirs", "CC_Dip_Debt_Ours", "CC_Dip_Debt_Personal", "CC_LetterBring_Owed" })
            C(Regex.IsMatch(keys, "<" + k + ">[^<]*\\{WEIGHT\\}"), $"{k} names the weight");

        // ================= Older saves: a size from the origin =================

        C(FavourMath.SizeOf(14f, 6) == 14 && FavourMath.SizeOf(0f, 20) == 20 && FavourMath.SizeOf(0f, 0) == FavourMath.MinSize && FavourMath.SizeOf(0.4f, 15) == 15,
            "a debt keeps its own size; an older one with none takes its deed's weight, else 10");
        foreach (var (kind, size) in new[] { ("refugees", 20), ("hardship", 15), ("foundChild", 18), ("hamlet", 15), ("released", 12), ("tended", 10), ("townDefended", 18) })
            C(Regex.IsMatch(memory, "\\[\"" + kind + "\"\\] = K\\(DeedSide\\.(Their)?Favor, " + size + "\\b"), $"{kind}'s debt keeps its size {size}");

        // ================= 5. One debt per real threat, a season apart =================

        C(WarnMath.DebtGapDays == 15f && WarnMath.DebtAllowed(-1, 0) && !WarnMath.DebtAllowed(0, 15 * D - 1) && WarnMath.DebtAllowed(0, 15 * D), "one debt from warnings per people a season");
        C(FavourMath.CallGapDays == FavourMath.AskGapDays && FavourMath.CallGapDays == 15f && FavourMath.CallAllowed(-1, 0) && !FavourMath.CallAllowed(0, 15 * D - 1) && FavourMath.CallAllowed(0, 15 * D),
            "one debt called in per people a season, as their own asks");
        C(WarnMath.RaidKey(12) == "raid:12" && WarnMath.SiteKey(345) == "site:345" && WarnMath.SitKey(7) == "sit:7", "the threats' keys, the same as before (old letters keep theirs)");
        C(WarnMath.IsWarningKey("raid:3") && WarnMath.IsWarningKey("site:3") && WarnMath.IsWarningKey("sit:3") && !WarnMath.IsWarningKey("loss:Ana") && !WarnMath.IsWarningKey(null),
            "an older save's warnings known by their keys, a loss mourned not");
        C(WarnMath.MaxKept == 60 && WarnMath.MaxKnown == 40 && WarnMath.KnownDays == 30f, "the records' bounds");
        var order = WarnMath.Order(new[] { 8, 14, 12, 14 });
        C(order.SequenceEqual(new[] { 1, 3, 2, 0 }), "the weightiest threat first, ties in their order");
        // Two years of warnings to one people every three days (each a new threat, half answered warmly): at most four debts a year.
        var rng = new Random(11);
        int debts = 0, lastDebt = -1;
        for (int day = 0; day < 120; day += 3)
        {
            int now = day * D;
            int size = new[] { 8, 10, 12, 14 }[rng.Next(4)];
            bool warm = rng.NextDouble() < 0.5;
            if (warm && WarnMath.MakesDebt(ThreatKind.Raiders, size) && WarnMath.DebtAllowed(lastDebt, now))
            {
                debts++;
                lastDebt = now;
            }
        }
        C(debts <= 8 && debts >= 4, $"two years of warnings every three days: {debts} debts, a season apart at most");
        int callsIn = 0, lastCall = -1;
        for (int day = 0; day < 120; day++)
            if (FavourMath.CallAllowed(lastCall, day * D))
            {
                callsIn++;
                lastCall = day * D;
            }
        C(callsIn == 8, "called in every day it may be: eight times in two years");

        // ================= 6. The gates =================

        C(MatchMath.SeekRegard == 30 && MatchMath.SeekGoodwill == 40 && MatchMath.CourtedRegard == 10 && MatchMath.CourtedGoodwill == 20, "a match sought at regard 30 and goodwill 40, two who courted 10 and 20");
        C(MatchMath.SeekWhyNot(30, 40, false) == null && MatchMath.SeekWhyNot(29, 90, false) == "Regard" && MatchMath.SeekWhyNot(90, 39, false) == "Goodwill", "the great terms");
        C(MatchMath.SeekWhyNot(10, 20, true) == null && MatchMath.SeekWhyNot(9, 90, true) == "Regard" && MatchMath.SeekWhyNot(90, 19, true) == "Goodwill", "two who courted, a lower bar");
        C(MatchMath.AnyCanSeek(10, 20) && !MatchMath.AnyCanSeek(9, 20), "below the lowest bar nothing is offered");
        foreach (var bar in new[] { "Regard", "Goodwill" })
        foreach (var court in new[] { "", "Courted" })
            C(keys.Contains("<CC_Match_Why_Bar_" + bar + court + ">"), $"the reason {bar}{court} in words");
        C(Math.Abs(MatchMath.AcceptChance(30, 40, false, false) - 0.2f) < 1e-4f && Math.Abs(MatchMath.AcceptChance(50, 40, false, false) - 0.4f) < 1e-4f
          && Math.Abs(MatchMath.AcceptChance(70, 40, false, false) - 0.6f) < 1e-4f, "accepted 20% and 1% a point of regard above 30");
        C(Math.Abs(MatchMath.AcceptChance(30, 40, true, false) - 0.55f) < 1e-4f && Math.Abs(MatchMath.AcceptChance(30, 75, false, false) - 0.3f) < 1e-4f
          && Math.Abs(MatchMath.AcceptChance(30, 74, false, false) - 0.2f) < 1e-4f, "35% more for lovers, 10% at goodwill 75");
        C(MatchMath.AcceptChance(-1, 90, true, false) == 0f && MatchMath.AcceptChance(100, 100, true, false) == 0.95f && Math.Abs(MatchMath.AcceptChance(10, 20, false, true) - 0.15f) < 1e-4f,
            "none below 0, never certain, a little less when theirs moves");
        C(MatchMath.FreshRegard == 50 && MatchMath.MayMakeFresh(50) && !MatchMath.MayMakeFresh(49), "one of their town made for the match only at regard 50");
        C(MatchMath.RefusedGapDays == 15f && MatchMath.RefusalOver(-1, 0) && !MatchMath.RefusalOver(0, 15 * D - 1) && MatchMath.RefusalOver(0, 15 * D), "a season's wait after a refusal");
        C(DiplomacyMath.MatchRefused == "dip:matchNo" && DiplomacyMath.Kinds.Contains(DiplomacyMath.MatchRefused), "a refusal kept in diplomacy's history");
        C(MatchMath.TownGiftGoodwill == 20 && MatchMath.TownGiftSent(20) && !MatchMath.TownGiftSent(19), "the town's gift only at goodwill 20");
        C(MatchMath.AlliancePact(false, PactMath.PactGoodwill, 0, false) && !MatchMath.AlliancePact(true, 90, 90, false) && !MatchMath.AlliancePact(false, PactMath.PactGoodwill - 1, 50, false)
          && !MatchMath.AlliancePact(false, 90, -1, false) && !MatchMath.AlliancePact(false, 90, 90, true) && PactMath.PactGoodwill == 20,
            "the alliance's pact only while a pact's rules hold: goodwill 20, never the Empire, not at war");
        C(WardMath.MinRegard == 30 && WardMath.MayAsk(30) && !WardMath.MayAsk(29) && WardMath.SendChance(29) == 0f, "a ward asked for only at their head's regard of 30");
        C(PactMath.TheyPayScore == -40 && PactMath.TruceScore(0, false, false, false, TrucePrice.Theirs, TruceTerm.Season) == -40, "a truce they pay weighs -40, not -20");
        C(PactMath.TheirPriceSilver(1000f) == 250 && PactMath.TheirPriceSilver(30f) == 50 && PactMath.TheirPriceSilver(333f) % 10 == 0, "they pay half the colony's price, in tens, 50 at least");
        C(PactMath.TheyMayPay(true) && !PactMath.TheyMayPay(false) && PactMath.BeatenDays == 15f, "only after a raid of theirs beaten lately");
        C(NotableMath.GiftGapDays == 15f && NotableMath.GiftAllowed(-1, 0) && !NotableMath.GiftAllowed(0, 15 * D - 1) && NotableMath.GiftAllowed(0, 15 * D), "a gift with an answer once a season per notable");
        C(NotableMath.HelpPeopleDays == 20f && NotableMath.HelpAllowed(-1, 0) && !NotableMath.HelpAllowed(0, 20 * D - 1) && NotableMath.HelpAllowed(0, 20 * D), "help asked once in 20 days per people");
        C(DealingMath.FavourSize == 10 && Math.Abs(FavourMath.GoodsWorth(FavourMath.Effective(DealingMath.FavourSize, true), 50f, 1f) - 150f) < 0.01f,
            "a pen friend's favour in person: half the goods, 150 at a steady industrial town");
        C(OutsiderMath.BringFreeGapDays == 15f && OutsiderMath.BringFree(-1, 0) && !OutsiderMath.BringFree(0, 15 * D - 1) && OutsiderMath.BringFree(0, 15 * D), "brought free once a season per people");
        C(OutsiderMath.BringPrice(2f, 40) == 80 && OutsiderMath.BringPrice(0f, 5) == 1 && OutsiderMath.BringPrice(14.6f, 3) == 44, "else paid at its market price when it comes");

        // ================= 7 to 10. News heard kept =================

        C(OutsiderTalkMath.SameNews(0, 10 * D - 1) && !OutsiderTalkMath.SameNews(0, 10 * D) && !OutsiderTalkMath.SameNews(-1, 5), "the same news within 10 days is one line");
        C(OutsiderTalkMath.HeardPeopleShown == 3 && OutsiderTalkMath.HeardTownShown == 1 && OutsiderTalkMath.HeardNotableShown == 1, "the Overview's newest 3, each town's newest one");
        C(OutsiderTalkMath.KeepsFact("ws:7") && OutsiderTalkMath.KeepsFact("site:3") && OutsiderTalkMath.KeepsFact("ill:2") && OutsiderTalkMath.KeepsFact("head:4:55")
          && !OutsiderTalkMath.KeepsFact("war:3") && !OutsiderTalkMath.KeepsFact(null), "the facts a heard line keeps: threats and notables");
        C(WarnMath.FromFact("ws:7") == "sit:7" && WarnMath.FromFact("site:3") == "site:3" && WarnMath.FromFact("ill:2") == null && WarnMath.FromFact(null) == null,
            "a threat named in news becomes one the colony knows of, by the warning's own key");
        var dir = Path.Combine(root, "Defs");
        var heard = Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories).Select(File.ReadAllText).SelectMany(t => Regex.Matches(t, "<heard>([^<]*)</heard>").Cast<Match>().Select(m => m.Groups[1].Value)).ToList();
        C(heard.Any(h => h.Contains("{SITUATION}")) && heard.Any(h => h.Contains("{SITE}")), "talk's news names world events and sites, to be known");

        // ================= The seams =================

        string letters = Code("World/NeighbourLetters.cs"), warnings = Code("World/Warnings.cs"), calls = Code("Diplomacy/FavourCalls.cs");
        C(letters.Contains("foreach (var t in Warnings.Topics(n)) Add(t.key, t.text);") && !letters.Contains("Now - d.tick < GenDate.TicksPerDay * 30"), "warning topics from the warnings' rules only");
        C(letters.Contains("Warnings.WhyNone(n) ?? \"CC_Letter_Why_NoThreat\""), "nothing to warn of says why");
        C(letters.Contains("Warnings.Heed(n.faction, l.topicKey)") && letters.Contains("Warnings.Record(n.faction, l.topicKey, heed, brings == AnswerBrings.FavourOwed)")
          && letters.Contains("l.owedSize = heed.size;") && letters.Contains("Mathf.Min(heed.size, FavourMath.MinSize - 1)"), "read: its worth, the people's record, a debt by size, never one by memory");
        C(letters.Contains("giftAllowed && !l.owed && NotableMath.Gift(") && letters.Contains("if (brings == AnswerBrings.Token && !giftAllowed) brings = AnswerBrings.TownNews;")
          && letters.Contains("n.SetLastOf(GiftKey, now)"), "a gift once a season per notable, none beside a debt");
        C(letters.Contains("NotableMath.HelpAllowed(LastOfPeople(n.faction, LetterPurpose.Help), Now)"), "help once in 20 days per people");
        C(letters.Contains("int size = l.owedSize > 0 ? l.owedSize : LetterMath.HeededWarmly;") && letters.Contains("Scribe_Values.Look(ref owedSize, \"owedSize\");"),
            "the debt made with the warning's size, an older letter's 10");
        C(warnings.Contains("WarnMath.RaidersSize(townTiles, campTiles, landTiles, homeTiles)") && warnings.Contains("WarnMath.WarOrRoad(s.def.kind, s.def.bad)")
          && warnings.Contains("WarnMath.Spreading(s.def.defName, s.def.kind, s.def.bad)") && warnings.Contains("WarnMath.SiteWhy("), "the map read by the pure rules");
        C(warnings.Contains("if (s.ctx.faction == f || s.ctx.enemy == f) continue;") && warnings.Contains("Attacker(s, f)") && warnings.Contains("threats.RemoveAll(t => t.kind == ThreatKind.Raiders && warBy.Contains(t.by));"),
            "a war's attacker, never their own war; raiders who are a war's attacker told once, as the war");
        C(warnings.Contains("RaidersCamps.Known()") && warnings.Contains("happenings?.raids") && warnings.Contains("d.kind == \"raided\""), "raiders seen: a raid, one coming, a camp found");
        C(warnings.Contains("Knows(WarnMath.SiteKey(site.ID))") && warnings.Contains("RegionTowns.ReachTiles"), "a site known: seen within the region, or heard of");
        C(warnings.Contains("CallsToArms.Attacked(f, m, out _, out var attacker) && attacker == by") && warnings.Contains("w.size + WarnMath.StruckBonus"), "struck: CallsToArms.Attacked, 4 more");
        C(warnings.Contains("n.done.Where(WarnMath.IsWarningKey)") && warnings.Contains("n.done.Remove(key)"), "an older save: each notable's warnings become their people's");
        C(Code("World/Notables.cs").Contains("Scribe_Collections.Look(ref warned, \"warned\", LookMode.Deep);") && Code("World/Notables.cs").Contains("Scribe_Collections.Look(ref known, \"known\", LookMode.Deep);")
          && Code("World/Notables.cs").Contains("Warnings.Daily(now);"), "the records saved in the neighbours' book, looked at daily");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/Notables.cs:warned\"") && est.Contains("\"World/Notables.cs:known\""), "the new records modelled in the save estimate");
        C(calls.Contains("FavourMath.GoodsWorth(Worth(t), Prosperity(f, t), FavourMath.Means(Ages.Of(f)))") && calls.Contains("FavourMath.WordGoodwill(Worth(t))")
          && calls.Contains("SizeWhy(FavourAsk.Captive, t)") && calls.Contains("SizeWhy(FavourAsk.Peace, t)") && calls.Contains("SizeWhy(FavourAsk.Fighters, t)"), "calling in by size");
        C(calls.Contains("if (CalledLately(f)) return DipWords.P(\"CC_Dip_Why_CalledLately\"") && calls.Contains("FavourMath.CallAllowed(t.closed, now)"), "one called in a season, greyed with why");
        C(Code("Diplomacy/DiplomacyWords.cs").Contains("FavourCalls.WeightWords(t).Named(\"WEIGHT\")"), "the debt's line names its weight");
        string dl = Code("Diplomacy/DiplomacyLetters.cs");
        C(dl.Contains("PactMath.TheirPriceSilver(") && dl.Contains("!PactMath.TheyMayPay(beaten)") && dl.Contains("CC_Truce_Why_TheyPayBeaten"), "a truce they pay: half, after a beaten raid, greyed with why");
        C(Code("Diplomacy/Truces.cs").Contains("PactMath.TheirPriceSilver(StorytellerUtility.DefaultThreatPointsNow(home))"), "their own offer's price by the same rule");
        string matches = Code("Diplomacy/Matches.cs");
        C(matches.Contains("MatchMath.SeekWhyNot(regard, goodwill, courted)") && matches.Contains("MatchMath.MayMakeFresh(regard)") && matches.Contains("MatchMath.RefusalOver(LastRefusal(f), Now)")
          && matches.Contains("Diplomacy.Write(DiplomacyMath.MatchRefused"), "the match's gates and its refusal kept");
        C(matches.Contains("pair.court >= 1") && matches.Contains("LovePartnerRelationUtility.LovePartnerRelationExists(ours, theirs)"), "two who courted: lovers, or sweet on each other face to face");
        C(Code("Diplomacy/MatchLetters.cs").Contains("Matches.SeekWhyNot(f, home)") && Code("Diplomacy/MatchLetters.cs").Contains("Matches.TermsWhyNot(f, home, c, cand.pawn, cand.head)")
          && Code("Diplomacy/MatchActs.cs").Contains("Matches.SeekWhyNot(f, c.MapHeld)"), "the gates by letter and face to face, greyed with why");
        string alliance = Code("Diplomacy/MatchAlliance.cs");
        C(alliance.Contains("MatchMath.TownGiftSent(") && alliance.Contains("if (!PactHolds(f, home)) return;") && Code("Diplomacy/MatchDays.cs").Contains("bool pact = PactHolds(f, map)"),
            "the alliance's gift and pact by their goodwill");
        C(Code("Diplomacy/Wards.cs").Contains("WardMath.MayAsk(NotableBook.Opinion(head))") && keys.Contains("<CC_Ward_Why_Regard>"), "a ward asked for, greyed by their head's regard");
        C(Code("Dealings/PenFriends.cs").Contains("FavoursOwed.MakePersonal(n, writer, \"penFriend\", DealingMath.FavourSize)") && !Code("Dealings/PenFriends.cs").Contains("FavoursOwed.Make(n.faction"),
            "a pen friend's favour is theirs in person, to the one who writes");
        string outs = Code("Outsiders/Outsiders.cs") + Code("Outsiders/OutsiderOutcomes.cs") + Code("Outsiders/OutsiderMenu.cs") + Code("Outsiders/OutsiderTies.cs");
        C(outs.Contains("Favours.PriceFor(f, thing, count)") && outs.Contains("number = Math.Max(0, price)") && outs.Contains("VarietyUtil.InStores(bearer.Map, ThingDefOf.Silver) < f.price")
          && outs.Contains("Couriers.TakeFromStores(bearer.Map, ThingDefOf.Silver, price, f.faction)"), "something brought: free once a season, else paid on delivery");
        string talk = Code("Outsiders/OutsiderTalk.cs");
        C(talk.Contains("History.Add(FarNews.Kind, actors, Refs.Colony(home), HistoryMath.Everyday, line)") && talk.Contains("Warnings.Heard(fact)") && talk.Contains("OutsiderTalkMath.SameNews(h.tick, now)"),
            "news heard: the history as world:heard, once in ten days, its threats known");
        C(talk.Contains("Heard(x, c, ch.heard, ch.heardFacts)") && Code("Outsiders/OutsiderTalkMath.cs").Contains("ch.heardFacts.Add(fact)"), "the facts behind the news go with it");
        C(!Code("Outsiders/OutsiderHeard.cs").Contains("public static float Draw(") && !Code("World/Ledger/LedgerViews.cs").Contains("HeardView"), "the dead code gone");
        C(!talk.Contains("the World page and") && !Code("Outsiders/OutsiderOutcomes.cs").Contains("on the World page and the region map"), "the stale comments fixed");
        string page = Code("UI/Region/PeoplePage.cs");
        C(page.Contains("OutsiderTalk.HeardOf(f, OutsiderTalkMath.HeardPeopleShown)") && page.Contains("OutsiderTalk.HeardAt(row.s.ID, OutsiderTalkMath.HeardTownShown)")
          && page.Contains("OutsiderTalk.HeardOfNotable(n, OutsiderTalkMath.HeardNotableShown)") && page.Contains("OutsiderTalk.HeardAbout(e.f"), "the People page: Overview, Towns, People");
        C(Code("Outsiders/OutsiderBook.cs").Contains("Scribe_Values.Look(ref notable, \"notable\", -1);"), "the notable a news names kept");
        foreach (var k in new[]
                 {
                     "CC_Dip_Why_CalledLately", "CC_Dip_Why_TooSmall", "CC_Dip_Why_TooSmallPersonal", "CC_Dip_Word_Good", "CC_Dip_Word_Warm", "CC_Dip_Word_Strong", "CC_Letter_Why_HelpPeople",
                     "CC_Letter_Topic_RaidsStruck", "CC_Letter_Topic_RaidsComing", "CC_Letter_Topic_RaidsCamp", "CC_Letter_Topic_War", "CC_Letter_Topic_Spreading", "CC_LetterBring_HeededLate",
                     "CC_Warn_Struck", "CC_Warn_Struck_Debt", "CC_Match_Why_Refused", "CC_Match_Why_Fresh", "CC_Match_Hist_Refused", "CC_Out_BringItemPaid", "CC_Out_Msg_BringYesPaid",
                     "CC_Out_Msg_BroughtPaid", "CC_Out_Heard_LineFrom", "CC_Out_Heard_Hist", "CC_PplP_HeardOfThem", "CC_PplP_HeardOf"
                 })
            C(keys.Contains("<" + k + ">"), $"{k} in words");
        foreach (var why in new[] { WarnWhy.TooFar, WarnWhy.TooOld, WarnWhy.NotTheirEnemy, WarnWhy.Ended })
            C(keys.Contains("<CC_Warn_Why_" + why + ">") && keys.Contains("<CC_Warn_NotListed_" + why + ">"), $"a raid not listed for being {why}, in words");
        return n;
    }

    /// <summary>Each world event's kind and whether it is bad, from the defs.</summary>
    private static Dictionary<string, (string kind, bool bad)> DefKinds(string root)
    {
        var all = new Dictionary<string, (string, bool)>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<ColonyCouncil\.WorldSituationDef[^>]*>(.*?)</ColonyCouncil\.WorldSituationDef>", RegexOptions.Singleline))
            {
                var b = m.Groups[1].Value;
                var dn = Regex.Match(b, "<defName>(.*?)</defName>");
                var k = Regex.Match(b, "<kind>(.*?)</kind>");
                if (!dn.Success) continue;
                all[dn.Groups[1].Value] = (k.Success ? k.Groups[1].Value : null, Regex.IsMatch(b, "<bad>true</bad>"));
            }
        return all;
    }
}
