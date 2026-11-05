using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2c.2, fealty to a stronger neighbour and vassals of the colony's own
/// (docs/design/stage2-diplomacy.md, items 9 and 10; Diplomacy/FealtyMath.cs; the player's decisions 9, 11 and 12): who may
/// swear and to whom, their answer and their offer, who objects, tribute by wealth and the owed and broken clock, levies,
/// protection, renouncing at a cost; struggling towns taken under protection, tribute by the council's level, the
/// quartermaster's dues, a vassal's levies, asks and release; the facts' tags; and the mod's side: the hooks in the shared
/// code, the save, the words, the lines, the talk pieces.
/// </summary>
public static class FealtyTests
{
    private const int D = FealtyMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "fealty and vassals: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Swearing ----------------

        C(FealtyMath.MaySwear(40, false, false, 2, false), "a people at goodwill 40 with two towns in reach can take the oath");
        C(!FealtyMath.MaySwear(39, false, false, 2, false) && !FealtyMath.MaySwear(40, false, false, 1, false), "not below 40 goodwill, not with one town in reach");
        C(!FealtyMath.MaySwear(90, true, false, 3, false) && !FealtyMath.MaySwear(90, false, true, 3, false), "never a people at war, never the Empire (decision 9)");
        C(!FealtyMath.MaySwear(90, false, false, 3, true), "one liege a colony");
        C(Math.Abs(FealtyMath.AcceptChance(40, 40) - 0.5f) < 0.001f && FealtyMath.AcceptChance(90, 90) >= 0.95f - 0.001f && FealtyMath.AcceptChance(-100, -100) >= 0.2f - 0.001f,
            "half the time at 40, nearly always among close friends, a fifth at worst");
        C(FealtyMath.AcceptChance(60, 30) > FealtyMath.AcceptChance(40, 30) && FealtyMath.AcceptChance(40, 60) > FealtyMath.AcceptChance(40, 30), "goodwill and regard both help");
        C(FealtyMath.Accepts(40, 40, 0.49f) && !FealtyMath.Accepts(40, 40, 0.51f), "accepted on the roll");
        C(FealtyMath.OathHoursMin > 0f && FealtyMath.OathHoursMax > FealtyMath.OathHoursMin && FealtyMath.OathWaitHours >= 12f, "their retinue comes some hours later and waits the day");

        // ---------------- Their offer ----------------

        C(FealtyMath.Struggling(2, 5) && !FealtyMath.Struggling(1, 3) && !FealtyMath.Struggling(3, 6), "raided twice in a season with five fighters or fewer");
        C(FealtyMath.MayOffer(50, false, false, 2, false, -1, 0) && !FealtyMath.MayOffer(50, false, false, 2, false, 0, 59 * D) && FealtyMath.MayOffer(50, false, false, 2, false, 0, 60 * D),
            "their offer once a year at most");
        C(!FealtyMath.MayOffer(50, false, true, 3, false, -1, 0) && !FealtyMath.MayOffer(30, false, false, 3, false, -1, 0), "the same people as may take an oath");

        // ---------------- Who objects ----------------

        C(FealtyMath.Objector(true, 80f, true) == FealtyObjector.Avenger, "an avenger first");
        C(FealtyMath.Objector(false, 30f, true) == FealtyObjector.Proud && FealtyMath.Objector(false, 29f, true) == FealtyObjector.Faith, "then the proud (pride 30 or more), then one of another faith");
        C(FealtyMath.Objector(false, 0f, false) == FealtyObjector.None, "or nobody");
        C(Math.Abs(FealtyMath.ProudAt - NatureMath.Threshold) < 0.001f, "the proud as the nature service reads them");

        // ---------------- Tribute ----------------

        C(FealtyMath.Tribute(0f) == 150 && FealtyMath.Tribute(1e7f) == 600, "150 to 600 silver's worth by wealth");
        C(FealtyMath.Tribute(15000f) == 150 && FealtyMath.Tribute(150000f) == 600 && FealtyMath.Tribute(82500f) == 380, "from 15,000 wealth to 150,000, in between by wealth");
        bool tens = true, rises = true;
        for (float w = 0f; w <= 200000f; w += 2500f)
        {
            int t = FealtyMath.Tribute(w);
            tens &= t % 10 == 0;
            rises &= FealtyMath.Tribute(w + 2500f) >= t;
        }
        C(tens && rises, "in tens, never less for a richer colony");
        C(FealtyMath.TributeDue(30f, 30f) && !FealtyMath.TributeDue(30f, 29.9f), "due on its day");
        C(FealtyMath.Owed(300, 120) == 180 && FealtyMath.Owed(300, 400) == 0 && FealtyMath.Owed(300, -5) == 300, "what is still owed");
        C(FealtyMath.BreaksOath(true) && !FealtyMath.BreaksOath(false) && FealtyMath.BrokenGoodwill == 20, "owed still at the next season's day breaks the oath, 20 goodwill");
        C(FealtyMath.CollectHour(9) && FealtyMath.CollectHour(15) && !FealtyMath.CollectHour(16) && !FealtyMath.CollectHour(8), "their collector comes by day");

        // ---------------- Levies and protection ----------------

        C(FealtyMath.LevyFighters == 2 && Math.Abs(FealtyMath.LevyDays - 5f) < 0.001f, "levies: two fighters, set out within five days");
        C(FealtyMath.LeviesRefusedGoodwill == 10, "levies refused cost 10 goodwill");
        C(FealtyMath.MayProtect(0f, 3f) && !FealtyMath.MayProtect(10f, 24f) && FealtyMath.MayProtect(10f, 25f), "protection at most once a season");
        C(FealtyMath.Protects(false, 0.89f) && !FealtyMath.Protects(false, 0.9f) && !FealtyMath.Protects(true, 0f), "nine times in ten, never against their friends");
        var rng = new Random(11);
        int came = 0;
        for (int i = 0; i < 10000; i++)
            if (FealtyMath.Protects(false, (float)rng.NextDouble()))
                came++;
        C(came > 8800 && came < 9200, $"about nine raids in ten see them come ({came} of 10000)");

        // ---------------- Leaving ----------------

        C(FealtyMath.RenounceCost(false) == 30 && FealtyMath.RenounceCost(true) == 0, "renouncing costs 30 goodwill, nothing after a season they failed to protect");
        C(FealtyMath.Failed(FealtyMath.FailedKey) && !FealtyMath.Failed(FealtyMath.LiegeKey), "the failed season is kept in the terms");

        // ---------------- Vassals: protection asked ----------------

        C(FealtyMath.MaxProtected == 2 && FealtyMath.MaxVassals == 3, "two protected towns and the hamlet a home");
        C(FealtyMath.AsksProtection(15f, 0, 30 * D, false, true, false, 0, -1), "a town under 20 for 30 days, at peace with the colony and at war elsewhere, asks");
        C(!FealtyMath.AsksProtection(15f, 0, 29 * D, false, true, false, 0, -1) && !FealtyMath.AsksProtection(20f, 0, 40 * D, false, true, false, 0, -1),
            "not before 30 days, not at 20");
        C(!FealtyMath.AsksProtection(15f, 0, 40 * D, true, true, false, 0, -1) && !FealtyMath.AsksProtection(15f, 0, 40 * D, false, false, false, 0, -1)
          && !FealtyMath.AsksProtection(15f, 0, 40 * D, false, true, true, 0, -1), "never at war with the colony, never without a war elsewhere, never the Empire's");
        C(!FealtyMath.AsksProtection(15f, 0, 40 * D, false, true, false, 2, -1) && !FealtyMath.AsksProtection(15f, 0, 40 * D, false, true, false, 0, 0)
          && FealtyMath.AsksProtection(15f, 0, 80 * D, false, true, false, 1, 10 * D), "two at most, once a year a home");

        // ---------------- Vassals: tribute and dues ----------------

        C(FealtyMath.Produce(50, TributeLevel.Light) == 30 && FealtyMath.Produce(50, TributeLevel.Usual) == 50 && FealtyMath.Produce(50, TributeLevel.Heavy) == 80,
            "light, usual and heavy at harvest");
        C(FealtyMath.Produce(0, TributeLevel.Light) >= 1, "always something");
        C(FealtyMath.ProtectedProduce(0f) == 20 && FealtyMath.ProtectedProduce(100f) == 50, "a protected town's produce by its fortune");
        C(FealtyMath.HeavyGoodwill == 3 && FealtyMath.HeavyRegard > 0f && FealtyMath.LightRegard > 0f, "heavy costs goodwill and regard each season, light warms");
        C(FealtyMath.Dues(50f, TributeLevel.Usual) == 70 && FealtyMath.Dues(100f, TributeLevel.Usual) == 100 && FealtyMath.Dues(10f, TributeLevel.Usual) == 25,
            "the season's dues by fortune, half and explained when struggling");
        C(FealtyMath.Dues(50f, TributeLevel.Heavy) > FealtyMath.Dues(50f, TributeLevel.Usual) && FealtyMath.Dues(50f, TributeLevel.Light) < FealtyMath.Dues(50f, TributeLevel.Usual),
            "by the council's level");
        C(FealtyMath.Explained(19f) && !FealtyMath.Explained(20f) && !FealtyMath.Explained(-1f), "explained under 20");
        C(FealtyMath.DuesDue(-1, 0) && !FealtyMath.DuesDue(0, 14 * D) && FealtyMath.DuesDue(0, 15 * D), "once a season a vassal");

        // ---------------- Vassals: levies, asks, release ----------------

        C(Math.Abs(FealtyMath.LevyStrength * 0.5f - 1f / 3f) < 0.001f, "a vassal's band is a third of the threat");
        C(FealtyMath.MayCallLevies(-1, 0) && !FealtyMath.MayCallLevies(0, 14 * D) && FealtyMath.MayCallLevies(0, 15 * D), "called once a season at most");
        C(FealtyMath.FallenRegard(0) == 0f && FealtyMath.FallenRegard(2) == -12f && FealtyMath.FallenRegard(9) == -20f, "their dead weigh on their head's regard, 20 at most");
        C(FealtyMath.MayAsk(-1, 0) && !FealtyMath.MayAsk(0, 14 * D) && FealtyMath.MayAsk(0, 15 * D), "one ask a season a vassal");
        C(FealtyMath.AskOf(true, true, true, true, true, false) == VassalAsk.Release && FealtyMath.AskOf(true, true, true, true, true, true) == VassalAsk.Defend,
            "release first (never asked of Faction Territories' vassals), an invasion first for theirs");
        C(FealtyMath.AskOf(false, true, false, true, true, false) == VassalAsk.Camp && FealtyMath.AskOf(false, false, false, true, true, false) == VassalAsk.Help
          && FealtyMath.AskOf(false, false, false, false, true, false) == VassalAsk.Judgement && FealtyMath.AskOf(false, false, false, false, false, false) == null,
            "then a camp, help, a judgement, or nothing");
        C(FealtyMath.AskOf(false, false, true, false, false, false) == null, "an invasion is only Faction Territories' to ask about");
        C(FealtyMath.HelpNeed(true) == "medicine" && FealtyMath.HelpNeed(false) == "rawfood" && FealtyMath.HelpCount(false) == 30, "food, or medicine in a sickness");
        C(FealtyMath.LowSince(-20, -1, 5) == 5 && FealtyMath.LowSince(-25, 3, 9) == 3 && FealtyMath.LowSince(-19, 3, 9) == -1, "the head's low regard is counted from when it fell");
        C(FealtyMath.AsksRelease(0, 15 * D) && !FealtyMath.AsksRelease(0, 14 * D) && !FealtyMath.AsksRelease(-1, 99 * D), "a season at -20 asks release");
        C(FealtyMath.BreakAwayGoodwill == 15, "refused, they break away");

        // ---------------- The terms and the facts ----------------

        foreach (TributeLevel l in Enum.GetValues(typeof(TributeLevel)))
            C(FealtyMath.ParseLevel(FealtyMath.Code(l), out var back) && back == l, $"the level {l} reads back");
        C(!FealtyMath.ParseLevel("lots", out var none) && none == TributeLevel.Usual, "an unknown level is usual");
        C(FealtyMath.Tags.Distinct().Count() == FealtyMath.Tags.Length && FealtyMath.Tags.All(t => t.StartsWith("fealty:") || t.StartsWith("vassal:")), "the tags, each once");
        C(FealtyMath.ThisSeason(0, 14 * D) && !FealtyMath.ThisSeason(0, 15 * D) && !FealtyMath.ThisSeason(-1, 0), "this season");

        return n;
    }
}
