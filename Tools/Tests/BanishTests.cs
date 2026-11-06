using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Banishment by the council (the player's request, 5 November 2026; Social/BanishMath.cs): the trouble read from the
/// fights and insults, a fight seen twice counted once, a word before anything else, banishment only if it goes on after,
/// "let it be" holding the council back a while, who dislikes and who stands by, what the colony loses, the words of every
/// age, and the player's own case from the save (Barr: two fights in three days, one of them his, and an insult).
/// </summary>
public static class BanishTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "banishment by the council: " + what);
        }

        const int Day = BanishMath.DayTicks;
        var F = new Func<int, int, bool?, BanishMath.Fight>((o, t, s) => new BanishMath.Fight(o, t, s));

        // ================= The thresholds make sense together =================

        C(BanishMath.WindowDays >= 20f && BanishMath.WindowDays <= 45f, "the trouble is read over the last month or so");
        C(BanishMath.StartedWeight > BanishMath.JoinedWeight && BanishMath.JoinedWeight > BanishMath.InsultWeight, "a fight started counts most, then one joined, then an insult");
        C(BanishMath.Score(0, 0, 40) <= BanishMath.InsultsAtMost + 0.001f && BanishMath.Score(0, 0, 40) < BanishMath.WordScore, "insults alone never bring a question");
        C(BanishMath.Score(1, 0, 0) < BanishMath.WordScore, "one fight alone never brings a question");
        C(BanishMath.WordFights >= 2, "a question needs more than one fight");
        C(BanishMath.MinPeople >= 5, "a small colony is never asked to spare anyone");
        C(BanishMath.MostShare > 0.5f && BanishMath.MaxStandBy <= 1, "most of the colony, and almost nobody standing by them");
        C(BanishMath.GladOpinion <= BanishMath.DislikeOpinion && BanishMath.StandByOpinion > 0, "glad is a stronger dislike, standing by is a good opinion");
        C(BanishMath.LetBeDays >= 10f && BanishMath.StepKeepDays > BanishMath.LetBeDays, "let it be holds a while; a word is remembered longer");
        C(BanishMath.GrudgeChance > 0f && BanishMath.GrudgeChance <= 0.35f && BanishMath.RaidChance <= 0.15f, "a grudge among raiders is rare (the game's own rate)");

        // ================= A fight seen twice is one fight =================

        // The note as it starts (who threw the first punch) and the memories once it ends, a little later.
        var seen = new List<BanishMath.Fight> { F(7, 1000, true), F(7, 1400, null), F(7, 1450, null), F(9, 5000, null) };
        var merged = BanishMath.Merge(seen);
        C(merged.Count == 2, "two fights, not four (" + merged.Count + ")");
        C(merged.First(f => f.other == 7).started == true, "who started it is kept from the note");
        C(merged.First(f => f.other == 7).tick == 1000, "the earliest sighting's tick is kept");
        C(BanishMath.Merge(new[] { F(7, 0, null), F(7, 2 * Day, null) }).Count == 2, "two fights with the same person days apart are two");
        C(BanishMath.Merge(new[] { F(7, 0, false), F(7, 100, true) })[0].started == true, "a sighting that knows they started it wins");
        C(BanishMath.Merge(null).Count == 0, "nothing seen, no fights");

        // ================= The trouble and the questions =================

        int now = 100 * Day;
        var twoFights = new[] { F(1, now - 10 * Day, true), F(2, now - 3 * Day, false) };
        var t = BanishMath.Of(twoFights, new[] { now - 3 * Day }, BanishMath.WindowFrom(now));
        C(t.fights == 2 && t.started == 1 && t.joined == 1 && t.insults == 1, "the window's fights and insults counted");
        C(Math.Abs(t.score - (1f + 0.6f + 0.25f)) < 0.001f, "scored " + t.score);
        C(BanishMath.WordDue(t), "two fights, one theirs, and an insult: the council has a word");
        C(!BanishMath.WordDue(BanishMath.Of(new[] { F(1, now - 2 * Day, false), F(2, now - Day, false) }, new int[0], BanishMath.WindowFrom(now))),
            "two fights others started, nothing else: not yet");
        C(BanishMath.WordDue(BanishMath.Of(new[] { F(1, now - 2 * Day, false), F(2, now - Day, false), F(3, now - Day / 4, false) }, new int[0], BanishMath.WindowFrom(now))),
            "three fights, whoever started them: a word");
        C(!BanishMath.WordDue(BanishMath.Of(new[] { F(1, now - 40 * Day, true), F(2, now - 35 * Day, true) }, new int[0], BanishMath.WindowFrom(now))),
            "fights more than a month ago are forgotten");

        var none = default(BanishMath.Trouble);
        C(BanishMath.Due(now, -1, -1, t, none) == BanishMath.Ask.Word, "never asked before: the first question is a word, never banishment");
        C(BanishMath.Due(now, -1, now + Day, t, none) == BanishMath.Ask.None, "after let it be: not again for a while");
        C(BanishMath.Due(now, -1, now - Day, t, none) == BanishMath.Ask.Word, "and once that's over, the word again");
        var after = BanishMath.Of(new[] { F(1, now - Day, true), F(3, now - Day / 2, false) }, new int[0], now - 6 * Day);
        C(BanishMath.Due(now, now - 6 * Day, -1, t, after) == BanishMath.Ask.Banish, "after a word, more trouble: the second question");
        C(BanishMath.Due(now, now - Day, -1, t, after) == BanishMath.Ask.None, "never the day after the word");
        var calm = BanishMath.Of(new[] { F(1, now - Day, false) }, new int[0], now - 6 * Day);
        C(BanishMath.Due(now, now - 6 * Day, -1, t, calm) == BanishMath.Ask.None, "after a word, one fight someone else started: not yet");
        C(BanishMath.Due(now, now - 70 * Day, -1, t, after) == BanishMath.Ask.Word, "a word two months old is forgotten: the council starts again with a word");
        C(BanishMath.StepHolds(now, now - 59 * Day) && !BanishMath.StepHolds(now, now - 61 * Day) && !BanishMath.StepHolds(now, -1), "how long a word is remembered");

        // ================= The colony's view =================

        C(BanishMath.Disliked(10, 7, 1), "seven of ten dislike them, one stands by them");
        C(!BanishMath.Disliked(10, 7, 2), "two stand by them: not almost nobody");
        C(!BanishMath.Disliked(10, 5, 0), "half the colony isn't most of it");
        C(!BanishMath.Disliked(3, 2, 0) && BanishMath.Disliked(4, 3, 0), "never fewer than three who dislike them");
        C(!BanishMath.Disliked(0, 0, 0), "nobody else: nobody to dislike them");
        C(BanishMath.Dislikes(-10) && !BanishMath.Dislikes(-9), "dislike from -10");
        C(BanishMath.StandsBy(20, false) && !BanishMath.StandsBy(19, false), "a friend stands by them");
        C(BanishMath.StandsBy(0, true) && !BanishMath.StandsBy(-15, true), "a partner or close kin stands by them unless they dislike them too");
        C(BanishMath.Glad(-20) && !BanishMath.Glad(-19), "glad to see them go from -20 (the game's rival line)");
        C(!BanishMath.BigEnough(5) && BanishMath.BigEnough(6), "six people or more to spare one");

        // ================= The letter's voices =================

        C(BanishMath.Stance(-40, false) == "for" && BanishMath.Stance(30, false) == "against" && BanishMath.Stance(0, false) == "unsure", "a council member's stance by their opinion");
        C(BanishMath.Stance(5, true) == "against", "kin stand by them");
        C(BanishMath.Say(2, 1, 0.5f) == "defiant" && BanishMath.Say(0, 2, 0.2f) == "bitter" && BanishMath.Say(1, 1, 0.6f) == "sorry", "how they take it");

        // ================= What the colony loses =================

        var theirs = new Dictionary<string, int> { ["Plants"] = 10, ["Animals"] = 10, ["Cooking"] = 4, ["Mining"] = 12 };
        var others = new Dictionary<string, int> { ["Plants"] = 14, ["Animals"] = 6, ["Cooking"] = 2, ["Mining"] = 12 };
        var best = new List<string>();
        var good = new List<string>();
        BanishMath.Lost(theirs, others, best, good);
        C(best.SequenceEqual(new[] { "Animals" }), "the colony's best hand at animals (" + string.Join(",", best) + ")");
        C(good.SequenceEqual(new[] { "Mining", "Plants" }), "and a good hand at mining and plants (" + string.Join(",", good) + ")");
        C(!best.Contains("Cooking") && !good.Contains("Cooking"), "a low skill is no loss even when they're the best at it");

        // ================= Every age =================

        C(BanishMath.AgeTags.SequenceEqual(new[] { "tribal", "medieval", "industrial", "spacer" }), "the four ages");
        C(BanishMath.WordKey("tribal") == "CC_Banish_Word_tribal" && BanishMath.VerbKey(null) == "CC_Banish_Verb_industrial", "the keys of the words");

        // ================= After =================

        C(!BanishMath.MayRide(false, false, 0, 100 * Day), "no grudge, no return among raiders");
        C(!BanishMath.MayRide(true, true, 0, 100 * Day), "never twice");
        C(!BanishMath.MayRide(true, false, now - 5 * Day, now) && BanishMath.MayRide(true, false, now - 20 * Day, now), "not soon after");

        // ================= The player's case (save Atia, 5 November 2026) =================

        // Barr (Human2690595) at tick 7674934: a fight he started with MJ at 7506160 (the deed's note, and both memories
        // 2.81 days old), one Macy started with him at 7562027 (1.88 and 1.87 days), and the insult that began it (Macy's
        // memory, 1.89 days). No other fight is in anyone's memory of the last twenty days.
        int barrNow = 7674934;
        var barr = new[]
        {
            F(1489026, 7506160, true), F(1489026, barrNow - 168750, null), F(1489026, barrNow - 168300, null),
            F(685131, 7562027, false), F(685131, barrNow - 112050, null), F(685131, barrNow - 112350, null)
        };
        var barrTrouble = BanishMath.Of(barr, new[] { barrNow - 113400 }, BanishMath.WindowFrom(barrNow));
        C(barrTrouble.fights == 2 && barrTrouble.started == 1, "Barr: two fights, one of them his (" + barrTrouble.fights + ", " + barrTrouble.started + ")");
        C(BanishMath.Due(barrNow, -1, -1, barrTrouble, none) == BanishMath.Ask.Word, "Barr: the council would have a word first, never banishment yet");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        string tracker = Code("Social/Banishment.cs");
        string answers = Code("Social/BanishDilemmas.cs");

        // Every age has its word and its answer.
        foreach (var age in BanishMath.AgeTags)
        {
            C(keyed.ContainsKey(BanishMath.WordKey(age)) && keyed.ContainsKey(BanishMath.VerbKey(age)), $"the {age} age has its word and its answer");
            C(keyed.TryGetValue(BanishMath.VerbKey(age), out var verb) && verb.Contains("{PAWN_nameDef}"), $"the {age} answer names them");
        }
        // Every told line in two wordings or more, each with the same placeholders.
        var told = new[]
        {
            "CC_Banish_WordSaid", "CC_Banish_LastWordSaid", "CC_Banish_WordMissed", "CC_Banish_Told", "CC_Banish_Gathering", "CC_Banish_GatheringHere",
            "CC_Banish_GoneTitle", "CC_Banish_GoneText", "CC_Banish_GoneToDie", "CC_Banish_GladOf", "CC_Banish_MissedBy", "CC_Banish_Rides",
            "CC_Banish_Voice_for", "CC_Banish_Voice_against", "CC_Banish_Voice_unsure", "CC_Banish_Objects", "CC_Banish_OwnSay"
        };
        IEnumerable<string> Holes(string s) => System.Text.RegularExpressions.Regex.Matches(s, @"\{([A-Z]+)(_[a-zA-Z]+)?\}").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x);
        foreach (var k in told)
        {
            C(keyed.ContainsKey(k) && keyed.ContainsKey(k + "_2"), $"{k}: two wordings or more");
            if (keyed.ContainsKey(k) && keyed.ContainsKey(k + "_2"))
                C(!Holes(keyed[k + "_2"]).Except(Holes(keyed[k])).Any(), $"{k}: its other wording names nothing the first isn't given");
            C(tracker.Contains("\"" + k + "\"") || answers.Contains("\"" + k + "\"") || answers.Contains("\"CC_Banish_Voice_\" + stance"), $"{k} is told by the code");
        }
        C(!keyed.Where(kv => kv.Key.StartsWith("CC_Banish_") || kv.Key.StartsWith("CC_Impact_Banish")).Any(kv => kv.Value.Contains('—') || kv.Value.Contains('!')), "no em dashes or exclamation marks");
        C(new[] { "CC_Settings_Banishment", "CC_Settings_Banishment_Tip", "CC_SetNote_Banishment", "CC_Card_Banished", "CC_World_Departed_Council" }.All(keyed.ContainsKey), "the setting's, the card's and the world panel's words");
        C(Code("CouncilSettings.cs").Contains("public bool councilBanishment = true;") && Code("CouncilSettings.cs").Contains("Scribe_Values.Look(ref councilBanishment, \"councilBanishment\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"councilBanishment\""), "the setting, saved and on the What's on page");

        // The questions: a word first, banishment only in the second.
        var dilemmas = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_Banish.xml")).Root!.Elements("ColonyCouncil.DilemmaDef").ToList();
        var first = dilemmas.FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Dilemma_Trouble");
        var second = dilemmas.FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Dilemma_Banish");
        C(first != null && second != null, "both questions are there");
        if (first != null && second != null)
        {
            string Classes(XElement d) => string.Join(" ", d.Descendants("li").Select(li => (string)li.Attribute("Class") ?? ""));
            C(!Classes(first).Contains("DilemmaEffect_Banish"), "the first question never offers banishment");
            C(Classes(first).Contains("DilemmaEffect_TroubleWord") && Classes(first).Contains("DilemmaEffect_Hours") && Classes(first).Contains("DilemmaEffect_TroubleStep"),
                "the first offers a word, the night hours and let it be");
            C(Classes(second).Contains("DilemmaEffect_Banish") && Classes(second).Contains("DilemmaEffect_TroubleWord"), "the second offers banishment beside the milder answers");
            foreach (var d in new[] { first, second })
            {
                var options = d.Element("options")!.Elements("li").ToList();
                int def = int.Parse(d.Element("defaultOption")!.Value);
                C(!string.Join(" ", options[def].Descendants("li").Select(li => (string)li.Attribute("Class") ?? "")).Contains("DilemmaEffect_Banish"), $"{d.Element("defName")!.Value}: never banishment when nobody answers");
                C(options[def].Descendants("li").Any(li => li.Element("kind")?.Value == "letbe"), $"{d.Element("defName")!.Value}: unanswered, the council lets it be");
                C(d.Element("timely")?.Value == "true", $"{d.Element("defName")!.Value}: asked in its moment");
            }
        }

        // On the map: face to face through Together, the game's own banishment with its own moods, the walk to the edge.
        C(tracker.Contains("Together.When(key, by, p, () => SayWord(r, by)") && tracker.Contains("Together.When(key, by, p, () => Told(r, by)"), "the word and the news are said face to face");
        C(tracker.Contains("Speech.Say(by, Line(\"Rimfolk_Pool_BanishWord\"") && tracker.Contains("Speech.Say(p, Line(\"Rimfolk_Pool_BanishReply\""), "one sentence each, through Speech.Say");
        C(tracker.Contains("PawnBanishUtility.Banish(p, true)") && tracker.Contains("exitMapOnArrival = true"), "the game's own banishment and its moods, then the walk out by the edge");
        C(tracker.Contains("ownership?.OwnedBed") && tracker.Contains("JobDefOf.Wait"), "they gather their things in their own room");
        C(tracker.Contains("Wards.Of(p) != null") && tracker.Contains("Matches.TieOf(p) != null") && tracker.Contains("Envoys.Holds(p)"), "never anyone held under a tie with another people");
        C(tracker.Contains("!p.DevelopmentalStage.Adult()") && tracker.Contains("p.IsSlave") && tracker.Contains("p.IsQuestLodger()"), "never a child, a slave or a lodger");
        C(Code("CouncilGameComponent.cs").Contains("BanishTracker.Hourly(this)"), "played out once an hour, in the council's people pass");
        C(Code("Social/BanishPatches.cs").Contains("nameof(Pawn_InteractionsTracker.StartSocialFight)"), "each fight noted as it starts, with who threw the first punch");
        C(answers.Contains("ThoughtDefOf.ColonistBanishedToDie") && answers.Contains("WouldBeLeftToDie"), "the hints name the game's heavier mood when they'd be sent out to die");
        C(Code("UI/Cards/Cards.cs").Contains("\"banished\"") && Code("UI/WorldPanel.cs").Contains("CC_World_Departed_Council"), "their card and the world's memory of them");

        // The lines said over a head are among the spoken pools (ImmersionTests checks each is one sentence).
        string immersion = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "ImmersionTests.cs"));
        foreach (var pool in new[] { "Rimfolk_Pool_BanishWord", "Rimfolk_Pool_BanishWordReply", "Rimfolk_Pool_BanishTell", "Rimfolk_Pool_BanishReply", "Rimfolk_Pool_BanishLast" })
            C(immersion.Contains("\"" + pool + "\""), $"{pool} is checked as spoken");

        // The docs and the Almanac.
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### Banishment by the council"), "DESIGN.md has it");
        C(File.ReadAllText(Path.Combine(root, "docs", "HANDOFF.md")).Contains("banishment by the council (the player's request, 5 November 2026)"), "the handoff has it");
        C(File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council.xml")).Contains("<heading>Someone who keeps causing trouble</heading>"), "the Almanac's dilemmas page has a part on it");

        return n;
    }
}
