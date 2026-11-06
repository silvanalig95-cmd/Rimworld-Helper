using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// How bonds come about (Social/BondsMath.cs, Social/Bonds.cs, SocialWeb, Offices): two come to know each other from
/// time side by side and drift apart when they stop; how two get on sets how far words and a friendship go; comrades
/// only from fighting near each other, every fight counting in full and a long history staying; grudges with causes
/// that hold and fade; a seat lost felt as much as it was wanted. Nothing aims at a number of friends or rivals.
/// </summary>
public static class BondsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "bonds: " + what);
        }

        // ---------- A fight at home came with a real threat (the player's save: a raiders' camp's raid on day 124) ----------
        const int Day = 60000;
        Check(BondsMath.WasThreat(124 * Day, 124 * Day - 100, -1, Day), "the storyteller's raid makes the fight a real one");
        Check(BondsMath.WasThreat(124 * Day + 500, 114 * Day, 124 * Day, Day), "a raid Rimfolk sent without telling the storyteller makes it a real one too");
        Check(!BondsMath.WasThreat(124 * Day, 114 * Day, -1, Day), "no threat for ten days: a scuffle, not a victory");
        Check(!BondsMath.WasThreat(124 * Day, 114 * Day, 120 * Day, Day), "an old raid of Rimfolk's doesn't count for a new fight");
        Check(!BondsMath.WasThreat(-1, 124 * Day, 124 * Day, Day), "no fight counted, no victory");
        {
            string send = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Happenings", "HappeningHelpers.cs"));
            int at = send.IndexOf("Notify_RaidSent(map)", StringComparison.Ordinal), told = send.IndexOf("if (ok && tellStoryteller)", StringComparison.Ordinal);
            Check(at > 0 && told > at, "every raid Rimfolk sends is noted for the fight, before and apart from telling the storyteller");
        }

        // ---------- How two get on ----------
        Check(Near(BondsMath.Affinity(0.5f, 0, 0, 0, 0, 0, 0), 0.5f), "with nothing else known, how two get on is the game's compatibility");
        Check(BondsMath.Affinity(0f, 0, 0, 0, 0, 1, 0) > 0f && BondsMath.Affinity(0f, 0, 0, 0, 0, 0, 1) < 0f, "a kind soul gets on with most people, an abrasive one with few");
        Check(BondsMath.FaithPart(1) > 0f && BondsMath.FaithPart(-1) < 0f && BondsMath.FaithPart(0) == 0f, "the same faith draws two together, different faiths apart, no faith known nothing");
        Check(BondsMath.ViewsPart(3, 0) > 0f && BondsMath.ViewsPart(0, 3) < 0f && BondsMath.ViewsPart(10, 0) <= 0.3f && BondsMath.ViewsPart(0, 10) >= -0.4f,
            "views agreed on draw two together and views at odds apart, within limits");
        Check(BondsMath.InterestsPart(1) > 0f && BondsMath.InterestsPart(9) <= 0.3f && BondsMath.InterestsPart(-2) == 0f, "shared passions help, up to a point");
        float most = BondsMath.FaithPart(-1) + BondsMath.ViewsPart(0, 9) + BondsMath.TemperPart(0, 2);
        Check(most > -1.5f, "faith, views and temper together move a pair less than the game's own spread of compatibility (" + most.ToString("0.00") + ")");

        // ---------- Knowing each other ----------
        Check(BondsMath.KnowStage(0f) == BondsMath.Strangers && BondsMath.KnowStage(BondsMath.AcquaintedAt) == BondsMath.Acquainted
              && BondsMath.KnowStage(BondsMath.KnowAt) == BondsMath.Known && BondsMath.KnowStage(BondsMath.WellAt) == BondsMath.WellKnown, "the four bands");
        Check(BondsMath.HourGain(0f, true, 0.5f, false) > BondsMath.HourGain(0f, false, 0.5f, false), "a meal or recreation together counts for more than working side by side");
        Check(Near(BondsMath.HourGain(10f, false, 0.5f, true), BondsMath.HourGain(10f, false, 0.5f, false) * 0.5f), "after a rejection or a breakup they come to know each other half as fast");
        Check(BondsMath.HourGain(80f, false, 0.5f, false) < BondsMath.HourGain(10f, false, 0.5f, false), "the first weeks count most");
        Check(BondsMath.KnowPace(2f) > BondsMath.KnowPace(0.5f) && BondsMath.KnowPace(0.5f) > BondsMath.KnowPace(-1.5f), "two who get on easily come to know each other faster");
        Check(BondsMath.DayDrift(50f, 10f) == 0f && BondsMath.DayDrift(50f, 0.5f) < 0f && BondsMath.DayDrift(0.1f, 0f) >= -0.1f,
            "two who still see each other don't drift apart; two who don't, do, never below nothing");
        Check(BondsMath.TalkGain(15f, 0f) > BondsMath.TalkGain(0.66f, 0f) && BondsMath.TalkGain(-15f, 0f) == 0f, "a deep talk brings two closer than a chitchat; an insult doesn't");
        int sideBySide = DaysToKnow(work: 6f, share: 1.5f, talk: 0.25f), seldom = DaysToKnow(work: 0.5f, share: 0.75f, talk: 0.05f);
        Check(sideBySide >= 7 && sideBySide <= 30, $"two who work side by side every day come to know each other in a week or a few ({sideBySide} days)");
        Check(seldom > sideBySide && seldom >= 21, $"two who seldom meet take weeks longer ({seldom} days)");
        Check(BondsMath.Seed(0, 0f) == 0f && Near(BondsMath.Seed(100, 0f), 70f) && BondsMath.Seed(400, 100f) <= 85f && BondsMath.Seed(10, 0f) < BondsMath.KnowAt,
            "on an old save, pairs know each other from their days together; a newcomer of ten days doesn't yet");
        Check(BondsMath.KinKnow >= BondsMath.KnowAt, "family and partners know each other from the start");

        // ---------- Friendship ----------
        Check(BondsMath.FriendCapByChemistry(-1.5f) < BondsMath.AnnouncedAt, "two who rub each other the wrong way never become friends: friendly colleagues at best");
        Check(BondsMath.FriendCapByChemistry(-0.8f) < 45f && BondsMath.FriendCapByChemistry(-0.8f) >= BondsMath.AnnouncedAt, "two who don't click can be new friends at most");
        Check(BondsMath.FriendCapByChemistry(-0.3f) < 70f && BondsMath.FriendCapByChemistry(-0.3f) >= 45f, "a slight mismatch allows good friends, never close ones");
        Check(BondsMath.FriendCapByChemistry(0.5f) == 100f, "an ordinary pair can be anything");
        Check(BondsMath.FriendCapByKnowing(0f) == 0f && BondsMath.FriendCapByKnowing(BondsMath.WellAt) >= 69f, "a friendship never runs ahead of how well they know each other; close friends know each other well");
        Check(!BondsMath.FriendsForm(80, 0.5f, 5f) && BondsMath.FriendsForm(80, 0.5f, 40f), "strangers who like each other aren't friends yet; once they know each other they can be");
        Check(!BondsMath.FriendsForm(30, 0.5f, 90f) && BondsMath.FriendsForm(45, 0.5f, 90f), "knowing each other isn't enough: they must like each other too");
        Check(BondsMath.FriendsForm(40, 2f, 25f) && !BondsMath.FriendsForm(40, 0.5f, 25f), "two who click become friends sooner");
        Check(!BondsMath.FriendsForm(100, -1.5f, 100f), "two who rub each other the wrong way don't become friends, however much they talk");
        Check(BondsMath.FriendTarget(100, -0.8f, 100f) <= 35f && BondsMath.FriendTarget(100, 0.5f, 20f) <= 23f && Near(BondsMath.FriendTarget(60, 0.5f, 100f), 35f),
            "a friendship drifts toward how they feel, no further than the cap");

        // ---------- Words between them ----------
        Check(BondsMath.WordsCap(-1.5f) < BondsMath.WordsCap(-0.8f) && BondsMath.WordsCap(-0.8f) < BondsMath.WordsCap(-0.3f) && BondsMath.WordsCap(0.5f) > 1000f,
            "the less two click, the sooner warm words stop carrying them; no limit for an ordinary pair");
        Check(BondsMath.CapFactor(0f, 25f) == 1f && Near(BondsMath.CapFactor(25f, 25f), 0.1f) && BondsMath.CapFactor(40f, 25f) == 0.1f && BondsMath.CapFactor(18f, 25f) < 1f,
            "words count in full well below the cap, a tenth at it");
        Check(BondsMath.CapFactor(90f, float.MaxValue) == 1f, "no cap, no limit");
        Check(Near(BondsMath.KnowFactor(0f), 0.5f) && Near(BondsMath.KnowFactor(BondsMath.KnowAt), 1f) && Near(BondsMath.KnowFactor(90f), 1f), "a warm word between strangers counts half");
        // Two who don't click, talking every day for a season: they settle as friendly colleagues.
        float op = 0f;
        for (int day = 0; day < 60; day++) op = Math.Min(100f, op + 3f * BondsMath.CapFactor(op, BondsMath.WordsCap(-1.5f)) - 0.5f);
        Check(op <= BondsMath.WordsCap(-1.5f) + 1f, $"a season of daily talks leaves two who grate as friendly colleagues ({op:0})");

        // ---------- Comrades ----------
        Check(BondsMath.BattleBond(0, 8, 0) == 0f && BondsMath.BattleBond(1, 8, 0) == 0f, "two who fought apart (or were seen together once in a long fight) don't bond");
        Check(BondsMath.BattleBond(2, 8, 0) > 0f && BondsMath.BattleBond(8, 8, 0) > BondsMath.BattleBond(2, 8, 0), "two seen side by side bond, more the longer");
        Check(BondsMath.BattleBond(1, 2, 0) > 0f, "a short fight needs only one look");
        Check(BondsMath.BattleBond(4, 8, BondsMath.DeedRescued) > BondsMath.BattleBond(4, 8, BondsMath.DeedGuarded) && BondsMath.BattleBond(4, 8, BondsMath.DeedGuarded) > BondsMath.BattleBond(4, 8, 0),
            "carrying someone out counts most, standing over them some");
        Check(BondsMath.DeedBond(BondsMath.DeedRescued | BondsMath.DeedTended | BondsMath.DeedGuarded) <= 20f, "deeds add up, within reason");
        Check(BondsMath.BattleMend(BondsMath.DeedRescued) > BondsMath.BattleMend(0) && BondsMath.BattleMend(0) > 0f, "rivals who fight side by side ease off, more after a rescue");
        Check(BondsMath.ComradeFloor(0) == 0f && BondsMath.ComradeFloor(1) < 10f && BondsMath.ComradeFloor(10) > BondsMath.ComradeFloor(3) && BondsMath.ComradeFloor(100) < 50.01f,
            "a long history of fighting together leaves a lasting bond; one fight doesn't");
        Check(BondsMath.ComradeDay(80f, BondsMath.QuietDays, 3) == 80f && BondsMath.ComradeDay(80f, BondsMath.QuietDays + 1, 3) < 80f, "comradeship holds through the quiet days after a fight, then fades");
        float c = 100f;
        for (int d = 0; d < 400; d++) c = BondsMath.ComradeDay(c, d, 4);
        Check(Near(c, BondsMath.ComradeFloor(4)), $"in a long quiet spell it fades to what their fights left ({c:0})");
        // Fighting together every twelve days: it keeps paying off (no fight counts less than the last).
        float k = 0f;
        int fights = 0;
        for (int d = 0; d < 240; d++)
        {
            if (d % 12 == 0)
            {
                k = Math.Min(100f, k + BondsMath.BattleBond(5, 8, 0));
                fights++;
            }
            k = BondsMath.ComradeDay(k, d % 12, fights);
        }
        Check(k >= 70f, $"two who keep fighting side by side end up bound by battle ({k:0})");
        Check(BondsMath.SeedBattles(100f) >= 4 && BondsMath.SeedBattles(10f) == 1, "an old comradeship counts the battles its strength shows at least");
        Check(BondsMath.NearNeeded(2) == 1 && BondsMath.NearNeeded(10) == 2, "two looks side by side in a long fight, one in a short one");

        // ---------- Grudges ----------
        Check(BondsMath.GrudgeDay(30f, BondsMath.GrudgeHoldDays, 0, 0) == 30f && BondsMath.GrudgeDay(30f, BondsMath.GrudgeHoldDays + 1, 0, 0) < 30f, "a grudge holds a few days after it was renewed, then fades");
        Check(BondsMath.GrudgeFade(1, 0) > BondsMath.GrudgeFade(0, 0) && BondsMath.GrudgeFade(0, 1) < BondsMath.GrudgeFade(0, 0), "a kind soul lets go sooner; a hot one holds on");
        Check(BondsMath.GrudgeGrowth(1, 0) < 1f && BondsMath.GrudgeGrowth(0, 1) > 1f && BondsMath.GrudgeGrowth(0, 0) == 1f, "and takes less to heart, or more");
        float g = 25f;
        int days = 0;
        while (g > 1f && days < 1000) g = BondsMath.GrudgeDay(g, days++, 0, 0);
        Check(days > 60 && days < 120, $"a rivalry left alone fades over a season or two ({days} days)");
        Check(BondsMath.Insult < BondsMath.AnnouncedAt && BondsMath.CameToBlows < BondsMath.AnnouncedAt && BondsMath.BotchedSurgery < BondsMath.AnnouncedAt && BondsMath.CheatedOn < BondsMath.AnnouncedAt,
            "no single thing makes a rivalry from nothing: it takes more than one, or a bad one on top of bad feeling");
        float words = 0f;
        for (int d = 0; d < 20; d++)
        {
            if (d % 3 == 0) words += BondsMath.Insult;
            words = BondsMath.GrudgeDay(words, d % 3, 0, 0);
        }
        Check(words >= BondsMath.AnnouncedAt, $"insults traded every few days become a rivalry ({words:0})");

        // ---------- A council seat ----------
        Check(BondsMath.SeatWant(true, false, false, false) > BondsMath.SeatWant(false, false, false, false), "holding the seat before, they wanted it more");
        Check(BondsMath.SeatWant(false, true, true, true) > BondsMath.SeatWant(false, false, false, false) && BondsMath.SeatWant(true, true, true, true) <= 2f,
            "a life goal of a seat, a vain nature and a campaign add to it, up to a point");
        Check(BondsMath.SeatStage(BondsMath.SeatWant(false, false, false, false)) == BondsMath.SeatMild && BondsMath.SeatStage(BondsMath.SeatWant(true, true, false, false)) == BondsMath.SeatBitter,
            "a plain candidate minds it a little; an incumbent with a seat as a life goal is bitter");
        Check(BondsMath.SeatGrudge(1f, 1, true) > BondsMath.SeatGrudge(1f, 1, false), "the incumbent voted out minds it most, a candidate who stood less");
        Check(BondsMath.SeatGrudge(1f, 2, false) > BondsMath.SeatGrudge(1f, 1, false) && BondsMath.SeatGrudge(1f, 3, false) > BondsMath.SeatGrudge(1f, 2, false), "losing to the same person again counts for more each time");
        Check(BondsMath.SeatGrudge(BondsMath.SeatWant(false, false, false, false), 1, false) < BondsMath.AnnouncedAt, "one lost vote is no rivalry");
        Check(BondsMath.SeatGrudge(BondsMath.SeatWant(true, false, false, false), 2, true) >= BondsMath.AnnouncedAt, "voted out by the same person twice is a rivalry");
        Check(BondsMath.SeatGrudge(BondsMath.SeatWant(false, true, true, false), 3, false) >= BondsMath.AnnouncedAt, "a candidate who badly wants the seat and loses it to the same person a third time is a rival");

        // ---------- The defs ----------
        var social = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Council_Thoughts_Social.xml"));
        var decrees = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Council_Thoughts_Decrees2.xml"));
        List<(float mood, float op)> Stages(XDocument doc, string def) => doc.Root.Elements("ThoughtDef").FirstOrDefault(d => (string)d.Element("defName") == def)?
            .Element("stages")?.Elements("li").Select(li => ((float?)li.Element("baseMoodEffect") ?? 0f, (float?)li.Element("baseOpinionOffset") ?? 0f)).ToList();
        var saved = Stages(social, "Council_SavedMe");
        Check(saved != null && saved.Count == 2 && saved.All(s => s.mood == 0f && s.op > 0f) && saved[1].op > saved[0].op, "saved me: tended under fire, then carried out (more), opinion only");
        var grateful = Stages(social, "Council_GratefulTo");
        Check(grateful != null && grateful.Count == 2 && grateful.All(s => s.mood == 0f && s.op > 0f), "grateful to a doctor: opinion only");
        var jealous = Stages(social, "Council_JealousOf");
        Check(jealous != null && jealous.Count == 2 && jealous.All(s => s.op < 0f && s.mood <= 0f) && jealous[1].op < jealous[0].op, "jealous of a partner's friend, more of a close one");
        var lostVote = Stages(decrees, "Council_LostElection");
        Check(lostVote != null && lostVote.Count == 3 && lostVote[1].op > lostVote[0].op && lostVote[2].op < lostVote[0].op && lostVote.All(s => s.op < 0f && s.mood < 0f),
            "a vote lost: felt (stage 0, as it always was), mild (1), bitter (2)");
        var lostSeat = Stages(decrees, "Council_LostOffice");
        Check(lostSeat != null && lostSeat.Count == 2 && lostSeat[1].op < lostSeat[0].op && lostSeat[0].op <= lostVote[0].op, "a seat lost stings more than a vote lost, and most for one who wanted it badly");
        var pools = File.ReadAllText(Path.Combine(root, "Defs/FlavorPoolDefs/Rimfolk_Bonds_Lines.xml"));
        Check(pools.Contains("Rimfolk_Pool_SeatGloat") && pools.Contains("Rimfolk_Pool_ThanksSaved"), "a gloat and a thanks to say face to face");

        // ---------- The code ----------
        string Src(string rel) => File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil", rel));
        string bonds = Src("Social/Bonds.cs"), web = Src("Social/SocialWeb.cs"), offices = Src("Offices/Offices.cs"), chem = Src("Social/Chemistry.cs");
        foreach (var memory in new[] { "Insulted", "Slighted", "HadAngeringFight", "HarmedMe", "BotchedMySurgery", "CheatedOnMe", "RebuffedMyRomanceAttempt", "RejectedMyProposal", "BrokeUpWithMe", "DivorcedMe" })
            Check(bonds.Contains("\"" + memory + "\""), $"the game's {memory} is read for what it does between two");
        Check(bonds.Contains("SocialLog.Near") && bonds.Contains("HourGain"), "knowing each other comes of time actually side by side");
        Check(web.Contains("FightSides.Near") && web.Contains("BondsMath.BattleBond") && !web.Contains("TieKind.Comrades, 25f"), "comrades come of fighting near each other, not of being in the same fight");
        Check(web.Contains("ComradeDay") && web.Contains("Bonds.FriendsForm") && web.Contains("Bonds.FriendCap") && web.Contains("GrudgeDay"), "the day's evolution follows the new rules");
        Check(offices.Contains("BondsMath.SeatWant") && offices.Contains("BondsMath.SeatGrudge") && offices.Contains("Rimfolk_Pool_SeatGloat") && offices.Contains("Together.When"),
            "a seat lost is felt as much as it was wanted, and a gloat is said face to face");
        Check(offices.Contains("CouncilDefOf.Council_MovedSeat, null") && offices.Contains("CouncilDefOf.Council_MovedDown, null"), "moves within the council stay a mood, toward nobody");
        Check(chem.Contains("Bonds.Affinity") && chem.Contains("BondsMath.WordsCap") && chem.Contains("BondsMath.KnowFactor"), "words between two follow how they get on and how well they know each other");
        Check(Src("CouncilGameComponent.cs").Contains("social.opinionOffset = def.stages[stage].baseOpinionOffset"),
            "a social memory given at a later stage takes that stage's opinion (the game sets it from the first when it's made)");
        string settings = Src("CouncilSettings.cs"), catalog = Src("UI/SettingsCatalog.cs");
        foreach (var s in new[] { "bondsTakeTime", "chemistryLimits", "comradesByDanger", "deedsRemembered", "seatFeelings", "loveFeelings" })
            Check(settings.Contains("public bool " + s + " = true;") && settings.Contains("ref " + s + ", \"" + s + "\", true") && catalog.Contains("Toggle(\"" + s + "\""), $"the setting {s} is saved and shown");
        return cases;
    }

    /// <summary>Days for two to come to know each other (BondsMath.KnowAt), with these hours side by side and deep talks a day.</summary>
    private static int DaysToKnow(float work, float share, float talk)
    {
        float fam = 0f;
        for (int day = 1; day <= 200; day++)
        {
            for (int h = 0; h < (int)work; h++) fam += BondsMath.HourGain(fam, false, 0.5f, false);
            for (int h = 0; h < (int)Math.Ceiling(share); h++) fam += BondsMath.HourGain(fam, true, 0.5f, false);
            fam += 2f * talk * BondsMath.TalkGain(15f, fam);
            if (fam >= BondsMath.KnowAt) return day;
        }
        return 200;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;
}
