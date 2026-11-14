using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The right-click's things done by the colonists themselves at a good moment (Outsiders/OutsiderAutoMath.cs,
/// OutsiderAuto.cs): which things may happen on their own on which setting, the moment, the pace (a few a day, two with
/// one outsider, never their last patience), the budget of the stores, what each thing would bring, who of the colony does
/// it and never the same one over and over; and the seams: the right-click's own reasons and talk job, no order of the
/// player's, no new message, letter or hook, the setting and its words.
/// </summary>
public static class OutsiderAutoTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "outsiders by themselves: " + what);
        }

        var acts = Enum.GetValues(typeof(OutsiderAct)).Cast<OutsiderAct>().ToList();

        // ---------------- Which things ----------------

        var free = acts.Where(a => OutsiderAutoMath.Tier(a) == AutoTier.Free).ToList();
        var limited = acts.Where(a => OutsiderAutoMath.Tier(a) == AutoTier.Limited).ToList();
        C(free.ToHashSet().SetEquals(new[] { OutsiderAct.AskHome, OutsiderAct.Befriend, OutsiderAct.GoodWord, OutsiderAct.Faith }),
            "free: news of home, getting to know them, a good word, faith");
        C(limited.ToHashSet().SetEquals(new[] { OutsiderAct.Meal, OutsiderAct.Drink, OutsiderAct.Gift, OutsiderAct.Teach, OutsiderAct.Spar, OutsiderAct.Court }),
            "within limits: a meal, a drink, a gift, a lesson, a bout, courting");
        foreach (var a in new[]
                 {
                     OutsiderAct.Chat, OutsiderAct.Letter, OutsiderAct.Bring, OutsiderAct.Invite, OutsiderAct.Provoke, OutsiderAct.Duel, OutsiderAct.AskFavour,
                     OutsiderAct.SpeakPact, OutsiderAct.SpeakMatch, OutsiderAct.OfferExchange, OutsiderAct.SoundOut
                 })
            C(OutsiderAutoMath.Tier(a) == AutoTier.Manual, $"{a} stays the player's own");
        foreach (var a in acts)
        {
            C(!OutsiderAutoMath.Allowed(OutsiderAutoMode.Off, a), $"{a}: nothing by themselves with the setting off");
            C(OutsiderAutoMath.Allowed(OutsiderAutoMode.FreeOnly, a) == (OutsiderAutoMath.Tier(a) == AutoTier.Free), $"{a}: free ones only means only those");
            C(OutsiderAutoMath.Allowed(OutsiderAutoMode.WithinLimits, a) == (OutsiderAutoMath.Tier(a) != AutoTier.Manual), $"{a}: within limits, the free and the limited");
        }
        C(OutsiderAutoMath.Order.Length == free.Count + limited.Count && OutsiderAutoMath.Order.Distinct().Count() == OutsiderAutoMath.Order.Length
          && OutsiderAutoMath.Order.All(a => OutsiderAutoMath.Tier(a) != AutoTier.Manual), "the things looked at are exactly the free and the limited, once each");
        C(!OutsiderAutoMath.Order.Any(OutsiderMath.Retired) && !OutsiderAutoMath.Order.Contains(OutsiderAct.Provoke) && !OutsiderAutoMath.Order.Contains(OutsiderAct.Duel),
            "never hard words, a duel or a retired act");
        C(OutsiderAutoMath.Order.Where(OutsiderMath.Pointed).SequenceEqual(new[] { OutsiderAct.Spar }), "of the pointed things, only a practice bout");

        // ---------------- The moment ----------------

        C(OutsiderAutoMath.ColonistFree(true, false, false, false, false, true, false), "a colonist awake and at leisure is free");
        C(!OutsiderAutoMath.ColonistFree(false, false, false, false, false, true, false), "not asleep");
        C(!OutsiderAutoMath.ColonistFree(true, true, false, false, false, true, false), "not down");
        C(!OutsiderAutoMath.ColonistFree(true, false, true, false, false, true, false), "not drafted");
        C(!OutsiderAutoMath.ColonistFree(true, false, false, true, false, true, false), "not in a mental state");
        C(!OutsiderAutoMath.ColonistFree(true, false, false, false, true, true, false), "not at the player's order");
        C(!OutsiderAutoMath.ColonistFree(true, false, false, false, false, false, false), "not mid-job: idle, wandering or at leisure only");
        C(!OutsiderAutoMath.ColonistFree(true, false, false, false, false, true, true), "not hungry or tired enough to need seeing to");
        C(OutsiderAutoMath.OutsiderFree(false, false, false, false, false, false), "an outsider free");
        for (int i = 0; i < 6; i++)
        {
            var f = new bool[6];
            f[i] = true;
            C(!OutsiderAutoMath.OutsiderFree(f[0], f[1], f[2], f[3], f[4], f[5]), $"an outsider busy, leaving, passing through, trading, in a scene or already sought ({i})");
        }
        C(OutsiderAutoMath.LookEvery * 250 == 1000 && OutsiderAutoMath.TakeChance > 0f && OutsiderAutoMath.TakeChance < 1f, "a look every 1,000 ticks, a moment not always taken at once");
        C(OutsiderAutoMath.Range >= 10f && OutsiderAutoMath.Range <= 40f, "near: a short walk");
        C(OutsiderAutoMath.OutsidersALook is >= 1 and <= 5 && OutsiderAutoMath.FullChecks is >= 1 and <= 8 && OutsiderAutoMath.NearestColonists is >= 2 and <= 10,
            "a look weighs a few outsiders, the nearest few colonists, and checks the best few in full");

        // ---------------- The pace ----------------

        const int H = OutsiderAutoMath.TicksPerHour, D = OutsiderAutoMath.TicksPerDay;
        var p = new AutoPace { now = 10 * D, today = 0, last = -1, withThem = 0, lastWithThem = -1, patienceLeft = 3, chatterLast = -1 };
        C(OutsiderAutoMath.PaceAllows(p), "nothing done yet: allowed");
        C(!OutsiderAutoMath.PaceAllows(p with { today = OutsiderAutoMath.PerDay }), "a few a day across the colony");
        C(!OutsiderAutoMath.PaceAllows(p with { withThem = OutsiderAutoMath.PerOutsiderDay }), "two a day with one outsider");
        C(!OutsiderAutoMath.PaceAllows(p with { patienceLeft = 1 }) && OutsiderAutoMath.PaceAllows(p with { patienceLeft = 2 }), "never their last patience: it's left for the player");
        C(!OutsiderAutoMath.PaceAllows(p with { last = p.now - H }) && OutsiderAutoMath.PaceAllows(p with { last = p.now - 2 * H }), "two hours between any two");
        C(!OutsiderAutoMath.PaceAllows(p with { lastWithThem = p.now - 3 * H }) && OutsiderAutoMath.PaceAllows(p with { lastWithThem = p.now - 4 * H }), "four hours between two with one outsider");
        C(!OutsiderAutoMath.PaceAllows(p with { chatterLast = p.now - H / 2 }) && OutsiderAutoMath.PaceAllows(p with { chatterLast = p.now - H }), "not right after a talk of their own");
        C(OutsiderAutoMath.PaceAllows(p with { last = p.now + 100 }), "a time ahead of now (a reload) doesn't hold anything up");
        C(OutsiderAutoMath.PerDay <= 6 && OutsiderAutoMath.PerOutsiderDay == 2 && OutsiderAutoMath.PerOutsiderDay < OutsiderTalkMath.PatienceBase, "a modest pace, below an outsider's patience");
        C(OutsiderAutoMath.PatienceKept >= 1 && OutsiderAutoMath.BoutsADay == 1, "the last patience kept, one bout a day");
        C(!OutsiderAutoMath.ColonistRested(p.now - 5 * H, p.now) && OutsiderAutoMath.ColonistRested(p.now - 6 * H, p.now) && OutsiderAutoMath.ColonistRested(-1, p.now),
            "the same colonist once in six hours");
        C(OutsiderAutoMath.Day(0) == 0 && OutsiderAutoMath.Day(D - 1) == 0 && OutsiderAutoMath.Day(D) == 1 && OutsiderAutoMath.Day(-1) == -1, "the day counted");

        // ---------------- The budget of the stores ----------------

        C(OutsiderAutoMath.Budget(12000f) == 30f && OutsiderAutoMath.Budget(40000f) == 100f && OutsiderAutoMath.Budget(100000f) == 250f
          && OutsiderAutoMath.Budget(1e7f) == 250f && OutsiderAutoMath.Budget(0f) == 30f, "a four-hundredth of the wealth a day, 30 to 250 silver");
        C(OutsiderAutoMath.BudgetLeft(40000f, 70f) == 30f && OutsiderAutoMath.BudgetLeft(40000f, 500f) == 0f, "what's left today");
        C(OutsiderAutoMath.Affords(30f, 40000f, 70f) && !OutsiderAutoMath.Affords(31f, 40000f, 70f) && !OutsiderAutoMath.Affords(-1f, 40000f, 0f), "within the budget only");
        C(OutsiderAutoMath.MealsToSpare(10, 8) && !OutsiderAutoMath.MealsToSpare(9, 8) && !OutsiderAutoMath.MealsToSpare(1, 0) && OutsiderAutoMath.MealsToSpare(2, 0),
            "a meal only with meals to spare beyond one each");
        C(OutsiderAutoMath.GiftFromPlenty(10, 30) && !OutsiderAutoMath.GiftFromPlenty(10, 29) && !OutsiderAutoMath.GiftFromPlenty(0, 100), "a gift only from three times as much");
        C(OutsiderAutoMath.GiftCount(12f, 75, 100f) == 8, "a gift of beer at twelve: as much as the budget left buys, under the right-click's share");
        C(OutsiderAutoMath.GiftCount(12f, 75, 500f) == OutsiderMath.GiftCount(12f, 75), "never more than the right-click's share");
        C(OutsiderAutoMath.GiftCount(12f, 75, 15f) == 0, "less than worth the walk: none");
        C(OutsiderAutoMath.GiftCount(300f, 1, 250f) == 0 && OutsiderAutoMath.GiftCount(60f, 1, 100f) == 1 && OutsiderAutoMath.GiftCount(0f, 10, 100f) == 0,
            "one thing within the budget, nothing beyond it or of no worth");
        C(OutsiderAutoMath.DrinksKept >= 2 && OutsiderAutoMath.GiftKeptTimes >= 2, "the stores keep their own");

        // ---------------- What it would bring ----------------

        var o = new AutoOption { ofColony = 20, ofColonist = 10, goodwill = 30 };
        float W(OutsiderAct a, AutoOption x) => OutsiderAutoMath.Worth(x with { act = a });
        C(W(OutsiderAct.GoodWord, o) > W(OutsiderAct.Befriend, o) && W(OutsiderAct.Befriend, o) >= W(OutsiderAct.AskHome, o),
            "a good word first, then getting to know them, then news");
        C(W(OutsiderAct.Befriend, o with { warmth = 30f, familiarity = 25f, visits = 1 }) > W(OutsiderAct.GoodWord, o), "close to becoming friends: getting to know them comes first");
        C(W(OutsiderAct.Befriend, o with { friends = true }) < W(OutsiderAct.Befriend, o), "friends already: less to gain");
        C(W(OutsiderAct.AskHome, o with { heardLately = true }) < W(OutsiderAct.AskHome, o), "news of their town heard lately: less to learn");
        C(W(OutsiderAct.Faith, o) < 0f && W(OutsiderAct.Faith, o with { sameFaith = true }) > 0f, "faith only with the same faith");
        C(W(OutsiderAct.Court, o) < 0f && W(OutsiderAct.Court, o with { sweethearts = true, courtChance = 0.4f }) < 0f
          && W(OutsiderAct.Court, o with { sweethearts = true, courtChance = 0.6f }) > 0f, "courting only a sweetheart, and only when it's likely to go well");
        C(W(OutsiderAct.Teach, o with { lead = 5 }) < 0f && W(OutsiderAct.Teach, o with { passion = 1, lead = 2 }) < 0f
          && W(OutsiderAct.Teach, o with { passion = 1, lead = 5 }) > 0f && W(OutsiderAct.Teach, o with { passion = 2, lead = 5 }) > W(OutsiderAct.Teach, o with { passion = 1, lead = 5 }),
            "a lesson only in a skill the colonist cares for, the more the better");
        C(W(OutsiderAct.Meal, o with { food = 0.8f }) < 0f && W(OutsiderAct.Meal, o with { food = 0.4f }) > 0f, "a meal when the colonist would eat soon anyway");
        C(W(OutsiderAct.Drink, o with { joy = 0.9f }) < 0f && W(OutsiderAct.Drink, o with { joy = 0.4f }) > 0f && W(OutsiderAct.Drink, o with { joy = 0.9f, joyTime = true }) > 0f,
            "a drink when they want recreation, or in their recreation time");
        C(W(OutsiderAct.Gift, o with { firstGift = true, giftFit = 1f }) < 0f && W(OutsiderAct.Gift, o with { firstGift = false, giftFit = 2f }) < 0f
          && W(OutsiderAct.Gift, o with { firstGift = true, giftFit = 1.5f }) > 0f, "a gift that suits them, the first of their visit only");
        C(W(OutsiderAct.Gift, o with { firstGift = true, giftFit = 1.5f, goodwill = 0 }) > W(OutsiderAct.Gift, o with { firstGift = true, giftFit = 1.5f, goodwill = 60 }),
            "a gift counts for more with a people cool toward the colony");
        C(W(OutsiderAct.Spar, o with { sparChance = 0.8f }) < 0f && W(OutsiderAct.Spar, o with { keen = true, sparChance = 0.3f }) < 0f
          && W(OutsiderAct.Spar, o with { keen = true, sparChance = 0.7f }) > 0f, "a bout only between two keen on it");
        foreach (var a in acts.Where(a => OutsiderAutoMath.Tier(a) == AutoTier.Manual))
            C(W(a, o with { sameFaith = true, sweethearts = true, courtChance = 1f, passion = 2, lead = 9, keen = true, sparChance = 1f, food = 0f, joy = 0f, firstGift = true, giftFit = 2f }) < 0f,
                $"{a} is never weighed for doing by themselves");
        C(W(OutsiderAct.Befriend, o with { ofColonist = -30 }) < W(OutsiderAct.Befriend, o), "dislike of the colonist takes a little off");
        C(OutsiderAutoMath.NearFriends(OutsiderMath.FriendWarmth - 12f, OutsiderMath.FriendKnowing - 12f, 1) && !OutsiderAutoMath.NearFriends(10f, 40f, 3),
            "close to friends: within reach of the bond and the knowing, a visit behind them");

        // ---------------- Who does it ----------------

        var w = new AutoWho { act = OutsiderAct.Befriend, social = 6, ofColonist = 10, familiarity = 10f, distance = 8f };
        float S(AutoWho x) => OutsiderAutoMath.WhoScore(x);
        C(S(w with { social = 14 }) > S(w) && S(w with { ofColonist = 50 }) > S(w) && S(w with { familiarity = 60f }) > S(w), "the better talker, the one they like, the one who knows them");
        C(S(w with { speakerOrSteward = true, notable = true }) > S(w) && S(w with { speakerOrSteward = true }) == S(w), "the speaker or the steward with someone of rank");
        C(S(w with { act = OutsiderAct.GoodWord, speakerOrSteward = true }) > S(w with { act = OutsiderAct.GoodWord }), "the speaker or the steward for a good word at home");
        C(S(w with { warden = true }) > S(w), "the warden of guests with any outsider");
        C(S(w with { act = OutsiderAct.Faith, chaplain = true }) > S(w with { act = OutsiderAct.Faith }) && S(w with { chaplain = true }) == S(w), "the chaplain for faith");
        C(S(w with { act = OutsiderAct.Teach, passion = 2, lead = 5 }) > S(w with { act = OutsiderAct.Teach, passion = 1, lead = 5 }), "a lesson to the one keenest on the skill");
        C(S(w with { act = OutsiderAct.Spar, melee = 12, keen = true }) > S(w with { act = OutsiderAct.Spar, melee = 4 }), "a bout to a fighter");
        C(S(w with { act = OutsiderAct.Meal, food = 0.2f }) > S(w with { act = OutsiderAct.Meal, food = 0.4f }), "a meal with the hungrier");
        C(S(w with { act = OutsiderAct.Drink, joy = 0.1f }) > S(w with { act = OutsiderAct.Drink, joy = 0.5f }), "a drink with the one who most wants a break");
        C(S(w with { shy = true }) < S(w) && S(w with { distance = 20f }) < S(w), "less for the shy, and for a long walk");
        C(S(w with { wasLast = true }) < S(w) && S(w with { today = 1 }) < S(w) && S(w with { today = 2 }) < S(w with { today = 1 }),
            "less for whoever did the last one, and for each they did today");
        // Two alike: the one who did the last one gives way, even a little the better talker.
        C(S(w with { social = 8, wasLast = true }) < S(w), "never the same colonist over and over");
        C(OutsiderAutoMath.Score(-1f, 100f) == float.MinValue && OutsiderAutoMath.Score(4f, 1f) > OutsiderAutoMath.Score(3f, 2f), "a thing not worth it is never picked; worth counts twice");

        // ---------------- The seams ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string auto = Read("Outsiders", "OutsiderAuto.cs");
        string code = Regex.Replace(auto, @"//[^\n]*|///[^\n]*", "");
        C(code.Contains("Outsiders.WhyNot(") && code.Contains("OutsiderTalk.Repeat("), "the right-click's own reasons why not, and never asked twice");
        C(code.Contains("Outsiders.StartTalk(") && code.Contains("byThemselves: true") && code.Contains("Outsiders.StartGift("), "the right-click's own talk and gift jobs");
        C(!code.Contains("TryTakeOrderedJob"), "never as the player's order");
        C(!Regex.IsMatch(code, @"Messages\.Message|Notify\.|LetterStack|Tell\.|News\.Post"), "no message or letter of its own");
        C(!code.Contains("[HarmonyPatch") && !code.Contains("HarmonyLib"), "no new Harmony patch");
        C(code.Contains("OutsiderAutoMath.PaceAllows(") && code.Contains("OutsiderAutoMath.Worth(") && code.Contains("OutsiderAutoMath.WhoScore(")
          && code.Contains("OutsiderAutoMath.Affords(") && code.Contains("Harm.May("), "the pace, the worth, the person, the budget and the cap on hurts");
        C(code.Contains("MapFacts.ThreatOn(") && code.Contains("DangerRating != StoryDanger.None"), "never with a threat or danger on the map");
        C(code.Contains("OutsiderAutoMath.OutsidersALook") && code.Contains("OutsiderAutoMath.FullChecks") && code.Contains("OutsiderAutoMath.NearestColonists"),
            "a look's work bounded as the rules say");
        string dev = Read("Outsiders", "OutsiderAutoDebug.cs");
        C(Regex.Matches(dev, @"\[DebugAction\(""Rimfolk"", ""Outsiders: ").Count == 2 && dev.Contains("OutsiderAuto.TryOn(") && dev.Contains("dry = dry"),
            "two dev tools, each through the look itself: what it would do, and one now");
        string jobs = Read("Outsiders", "OutsiderJobs.cs");
        C(jobs.Contains("private bool ByThemselves => !job.playerForced") && jobs.Contains("if (!ByThemselves) Messages.Message(") && jobs.Contains("ByThemselves);"),
            "the talk job: turned away in bubbles only, its outcome told it began by themselves");
        C(jobs.Contains("why != null && job.playerForced"), "the gift job: turned away in bubbles only");
        string outcomes = Read("Outsiders", "OutsiderOutcomes.cs");
        // Five: the meal, the lesson and the bout, and a meal whose outsider's portion is brought out of the cold store (the
        // bringing and the colonist's own meal after it, Visitors/KeepOut.cs).
        C(outcomes.Contains("if (byThemselvesNow && !StillTold(key)) return;") && Regex.Matches(outcomes, @"playerForced = !byThemselvesNow").Count == 5,
            "fewer messages by themselves; the meal, the lesson and the bout theirs, not an order");
        foreach (var k in new[] { "CC_Out_Msg_Friends", "CC_Out_Msg_Sweet", "CC_Out_Msg_Lovers", "CC_Out_Msg_InviteYes" })
            C(outcomes.Contains("key == \"" + k + "\""), $"{k} is still told");
        C(Read("Outsiders", "OutsiderBook.cs").Contains("OutsiderAuto.Rare(now)"), "the look rides on the talks of their own (no clock slot of its own)");
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public OutsiderAutoMode outsiderAuto = OutsiderAutoMode.WithinLimits;")
          && settings.Contains("Scribe_Values.Look(ref outsiderAuto, \"outsiderAuto\", OutsiderAutoMode.WithinLimits);"), "the setting, within limits by default, saved");
        var row = SettingsCatalog.Find("outsiderAuto");
        C(row != null && row.page == SettingsPage.WhatsOn && row.section == "CC_SetSec_Neighbours" && row.kind == SettingKind.Choice && row.Parent == "outsiderTalk",
            "the setting with the other talking-with-outsiders ones, under talking with outsiders");
        C(row != null && row.options.SequenceEqual(new[] { "Off", "FreeOnly", "WithinLimits" }), "off, free ones only, within limits");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Outsiders.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        keyed.TryGetValue("CC_Settings_OutsiderAuto_Tip", out var tip);
        C(tip != null && tip.Contains($"At most {OutsiderAutoMath.PerDay} a day") && tip.Contains($"{OutsiderAutoMath.PerOutsiderDay} a day with one outsider")
          && tip.Contains("30 to 250 silver") && !tip.Contains('!') && !tip.Contains('—'), "the tooltip says the pace and the budget as the rules have them");
        foreach (var o2 in new[] { "Off", "FreeOnly", "WithinLimits" })
            C(keyed.ContainsKey("CC_OutsiderAuto_" + o2 + "_Tip"), $"{o2} has its tooltip");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Neighbours.xml"));
        C(almanac.Contains("<heading>By themselves, at a good moment</heading>"), "the Almanac's page has a section on it");
        return n;
    }
}
