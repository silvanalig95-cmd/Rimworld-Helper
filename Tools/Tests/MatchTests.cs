using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2b.2, marriage alliances (docs/design/stage2-diplomacy.md, item 6; Diplomacy/MatchMath.cs;
/// the player's decisions 8 and 9): who may marry by the game's answers, the colonist asked first, their answer and their
/// offer, where the couple lives, the wedding day, the gifts, what the alliance gives while it holds, its ends (a death
/// with no child, a spouse wronged, the couple parted) and the feud carried down, the codes kept in letters and ties, the
/// facts' tags; and the mod's side: the hooks in the shared code, the save, the words, the lines, the talk pieces.
/// </summary>
public static class MatchTests
{
    private const int D = MatchMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "marriage alliances: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Who may marry ----------------

        C(MatchMath.OfAge(18f, true) && !MatchMath.OfAge(17.9f, true) && !MatchMath.OfAge(30f, false), "grown: the game's adulthood and 18 or more");
        C(MatchMath.WhyNot(true, true, true, true, false, true, true, false, false, false, false) == null, "two grown, drawn to each other, not kin, free and not engaged may marry");
        C(MatchMath.WhyNot(false, true, true, true, false, true, true, false, false, false, false) == "Young" && MatchMath.WhyNot(true, false, true, true, false, true, true, false, false, false, false) == "Young",
            "either too young: no");
        C(MatchMath.WhyNot(true, true, false, true, false, true, true, false, false, false, false) == "NotDrawn" && MatchMath.WhyNot(true, true, true, false, false, true, true, false, false, false, false) == "NotDrawn",
            "the game's orientation both ways");
        C(MatchMath.WhyNot(true, true, true, true, true, true, true, false, false, false, false) == "Kin", "kin never");
        C(MatchMath.WhyNot(true, true, true, true, false, false, true, false, false, false, false) == "Married" && MatchMath.WhyNot(true, true, true, true, false, true, false, false, false, false, false) == "Married",
            "the faith's rule on how many spouses, either side");
        C(MatchMath.WhyNot(true, true, true, true, false, true, true, true, false, false, false) == "Engaged", "promised to someone else: no");
        // Decision 9: the Empire's titled nobles marry only titled colonists.
        C(!MatchMath.EmpireFits(true, true, false) && MatchMath.EmpireFits(true, true, true) && MatchMath.EmpireFits(true, false, false) && MatchMath.EmpireFits(false, true, false),
            "the Empire's titled marry only the titled; its untitled and anyone else's titled, anyone");
        C(MatchMath.WhyNot(true, true, true, true, false, true, true, false, true, true, false) == "Titled" && MatchMath.WhyNot(true, true, true, true, false, true, true, false, true, true, true) == null,
            "a titled noble of the Empire and an untitled colonist: no");

        // ---------------- The colonist asked first ----------------

        C(MatchMath.ConsentOf(true, -50, false, false, out var w0) == Consent.Glad && w0 == ConsentWhy.None, "lovers are glad of it, whatever their quarrels");
        C(MatchMath.ConsentOf(false, MatchMath.GladOpinion, false, false, out _) == Consent.Glad && MatchMath.ConsentOf(false, MatchMath.GladOpinion - 1, false, false, out _) == Consent.Willing,
            "glad from a regard of 30");
        C(MatchMath.ConsentOf(false, 0, false, false, out _) == Consent.Willing, "strangers: willing");
        C(MatchMath.ConsentOf(false, MatchMath.AgainstOpinion, false, false, out var w1) == Consent.Unwilling && w1 == ConsentWhy.Dislike
          && MatchMath.ConsentOf(false, MatchMath.AgainstOpinion + 1, false, false, out _) == Consent.Willing, "against it at a regard of -10 or less: they can't stand them");
        C(MatchMath.ConsentOf(true, 80, true, false, out var w2) == Consent.Unwilling && w2 == ConsentWhy.Another, "loving someone else here comes first, even over lovers");
        C(MatchMath.ConsentOf(false, 40, false, true, out var w3) == Consent.Unwilling && w3 == ConsentWhy.Children, "leaving their children behind: against it");

        // ---------------- Their answer ----------------

        C(MatchMath.AcceptChance(-1, 90, true, false) == 0f, "below a regard of 0 they won't hear of it");
        C(MatchMath.AcceptChance(0, 0, false, false) >= 0.1f && MatchMath.AcceptChance(100, 100, true, false) <= 0.95f, "a tenth at worst, never certain");
        C(MatchMath.AcceptChance(60, 30, false, false) > MatchMath.AcceptChance(20, 30, false, false), "the better they think of the colony, the likelier");
        C(MatchMath.AcceptChance(20, 30, true, false) - MatchMath.AcceptChance(20, 30, false, false) > 0.3f, "lovers much likelier");
        C(MatchMath.AcceptChance(40, 30, false, true) < MatchMath.AcceptChance(40, 30, false, false), "a little less when one of theirs would leave them");
        C(MatchMath.AcceptChance(40, 80, false, false) > MatchMath.AcceptChance(40, 70, false, false), "allies a little more");
        C(MatchMath.Accepts(50, 30, false, false, 0f) && !MatchMath.Accepts(50, 30, false, false, 0.99f), "the roll against the chance");

        // ---------------- Their offer ----------------

        C(MatchMath.MayOffer(40, false, -1, 0, 0) && !MatchMath.MayOffer(39, false, -1, 0, 0), "they offer when their head thinks 40 or more of the colony");
        C(!MatchMath.MayOffer(80, true, -1, 0, 0), "never while at war");
        C(!MatchMath.MayOffer(80, false, 0, 60 * D - 1, 0) && MatchMath.MayOffer(80, false, 0, 60 * D, 0), "once a year at most per people");
        C(MatchMath.MaxPerPeople == 3 && !MatchMath.MayOffer(80, false, -1, 0, 3) && MatchMath.MayOffer(80, false, -1, 0, 2), "three marriages with a people at most");
        C(MatchMath.MaxPerPeople == 3 && MatchMath.MaxPlans == 3, "three a people (the ledger's bound of a kind between two), three under way at a home");
        string ties = Code("World/Ledger/TieMath.cs");
        C(ties.Contains("public const int MaxPerPairKind = 3;") && ties.Contains("public const int MaxPerKind = 60;"), "the ledger's own bounds: three between two, sixty in all");
        C(MatchMath.TheyPropose(true, 0.9f) == MatchHome.Theirs, "their heir stays home: the colony's own goes to them");
        int toColony = 0;
        for (int i = 0; i < 100; i++) if (MatchMath.TheyPropose(false, i / 100f) == MatchHome.Colony) toColony++;
        C(toColony > 30 && toColony < 70, $"anyone else: either side may move (decision 8), {toColony} in 100 to the colony");
        // Five years of a people who may offer, every day: at most one offer a year, about one a year.
        var rng = new Random(11);
        int offers = 0, last = -1, worst = 0;
        var perYear = new Dictionary<int, int>();
        for (int day = 0; day < 300; day++)
        {
            int now = day * D;
            if (!MatchMath.MayOffer(50, false, last, now, 0) || rng.NextDouble() >= MatchMath.OfferDaily) continue;
            offers++;
            last = now;
            int year = day / 60;
            perYear[year] = perYear.TryGetValue(year, out int c) ? c + 1 : 1;
            worst = Math.Max(worst, perYear[year]);
        }
        C(offers >= 3 && offers <= 5 && worst <= 1, $"five years of a friendly people: {offers} offers, at most one a year");

        // ---------------- The wedding day ----------------

        C(MatchMath.LeadDays(0f) == 1.5f && MatchMath.LeadDays(2f) == 3f, "their kin come after their road and a day to make ready, a day and a half at least");
        C(MatchMath.ArriveNow(100, 100, 8, false) && !MatchMath.ArriveNow(99, 100, 10, false) && !MatchMath.ArriveNow(100, 100, 15, false) && !MatchMath.ArriveNow(100, 100, 10, true),
            "they walk in on the day, by daylight, never into danger");
        C(MatchMath.CeremonyFrom == 5 && MatchMath.CeremonyTo == 16, "the game's own hours for a wedding ceremony");
        C(MatchMath.ArriveTo <= MatchMath.CeremonyTo, "the guests are at the gate in time for the ceremony");
        // Review of round b: the ceremony (its party before and after the vows) and the evening's feast each let no other
        // gathering begin; begun in the same hour, one waited on the other and the feast fell through.
        C(MatchMath.CeremonyHour(5, false) && MatchMath.CeremonyHour(16, false) && !MatchMath.CeremonyHour(4, false) && !MatchMath.CeremonyHour(17, false),
            "the game's hours for its ceremony");
        C(MatchMath.CeremonyHour(FeastMath.FeastFrom - 1, true) && !MatchMath.CeremonyHour(FeastMath.FeastFrom, true) && MatchMath.CeremonyHour(5, true),
            "with the wedding's guests here, never from the feast's hour on (the next morning instead)");
        C(MatchMath.RoadDays(0.2f) == 1f && MatchMath.RoadDays(3f) == 3f, "the road to their town, a day at least, before the wedding there");
        C(MatchMath.EscortWaitHours == 10f && MatchMath.GiveUpDays > MatchMath.EscortWaitHours / 24f, "their kin wait at the gate ten hours");

        // ---------------- Gifts ----------------

        C(MatchMath.GiftValue(0f) == 100 && MatchMath.GiftValue(100f) == 250 && MatchMath.GiftValue(50f) == 175 && MatchMath.GiftValue(500f) == 250, "gifts by their town's fortune, 100 to 250");
        C(MatchMath.GiftCount(150f, 10f) == 15 && MatchMath.GiftCount(5f, 50f) == 1 && MatchMath.GiftCount(100f, 0f) == 0, "how many make up a gift");
        C(MatchMath.KinGiftValue(50f) == 58, "the kin's gift each season, about a third of a match's");

        // ---------------- While it holds ----------------

        C(MatchMath.RegardBonus == 15, "their notables think 15 better of the colony while it holds");
        C(!MatchMath.KinGiftDue(-1f, 0f, 14.9f) && MatchMath.KinGiftDue(-1f, 0f, 15f) && !MatchMath.KinGiftDue(20f, 0f, 34f) && MatchMath.KinGiftDue(20f, 0f, 35f),
            "their kin's gift once a season, from the wedding");
        C(!MatchMath.Warns(1.9f, 0f) && MatchMath.Warns(2f, 0.2f) && !MatchMath.Warns(5f, 0.3f), "a camp hidden two days or more: their letter, one day in four");
        C(!MatchMath.PactAgain(true, false, 999f) && !MatchMath.PactAgain(false, true, 999f) && !MatchMath.PactAgain(false, false, 59f) && MatchMath.PactAgain(false, false, 60f),
            "the alliance makes its pact again a year after the last closed; never with the Empire (decision 9), never two");

        // ---------------- Its ends and the bad sides ----------------

        C(MatchMath.EndsAtDeath(false) && !MatchMath.EndsAtDeath(true), "a spouse's death ends it, unless a child of the two lives (decision 8)");
        C(MatchMath.DiedRegard(false, true, true) == 0, "a death away from the colony: nobody's fault");
        C(MatchMath.DiedRegard(true, false, false) == -3 && MatchMath.DiedRegard(true, true, false) == -15 && MatchMath.DiedRegard(true, false, true) == -15,
            "a death at the colony: a little if sheltered and tended, much in the open or untended");
        C(MatchMath.WrongOf(true, true, false, false, false, false) == MatchWrong.Executed && MatchMath.WrongOf(true, false, true, false, false, false) == MatchWrong.Killed,
            "an execution, then a colonist's blow");
        C(MatchMath.WrongOf(false, false, true, true, false, false) == MatchWrong.Enslaved && MatchMath.WrongOf(false, false, true, false, false, false) == MatchWrong.Imprisoned
          && MatchMath.WrongOf(false, false, false, false, true, true) == MatchWrong.Sold && MatchMath.WrongOf(false, false, false, false, false, true) == MatchWrong.Banished,
            "imprisoned, enslaved, sold, banished: the design's wrongs");
        C(MatchMath.WrongOf(false, false, false, false, false, false) == MatchWrong.None && !MatchMath.Wronged(MatchWrong.None) && MatchMath.Wronged(MatchWrong.Sold), "nothing else is a wrong");
        C(MatchMath.SoldAway(true, false, false) && !MatchMath.SoldAway(false, false, false) && !MatchMath.SoldAway(true, true, false),
            "sold: another people's, not held as a prisoner");
        C(!MatchMath.SoldAway(true, false, true), "carried off by raiders and gone over to them in the game's captivity: no wrong of the colony's");
        C(MatchMath.PassesDown(true, true) && !MatchMath.PassesDown(false, true) && !MatchMath.PassesDown(true, false), "the grudge passes to the next head once its holder is gone");
        C(MatchMath.PartedRegard < 0 && MatchMath.CalledOffRegard < MatchMath.PartedRegard && MatchMath.WrongedRegard < MatchMath.CalledOffRegard && MatchMath.DeclinedRegard < 0
          && MatchMath.DeclinedRegard > MatchMath.PartedRegard, "a match declined costs little, parting more, breaking it off more, a spouse wronged most");
        string memory = Code("World/FactionMemory.cs");
        C(Regex.IsMatch(memory, "\\[\"married\"\\] = K\\(DeedSide\\.Favor, 15") && Regex.IsMatch(memory, "\\[\"spouseWronged\"\\] = K\\(DeedSide\\.Wrong, 30"),
            "the deeds married (15) and spouseWronged (30), as the design sizes them");

        // ---------------- Codes ----------------

        C(MatchMath.ParseSpouse(MatchMath.SpouseKey("p123"), out string sr) && sr == "p123" && !MatchMath.ParseSpouse("spouse:", out _) && !MatchMath.ParseSpouse("x", out _),
            "the Marriage tie's key spouse:pX, as Diplomatic Marriage+'s view writes it");
        C(Code("Integrations/World/MarriageAllianceAdapter.cs").Contains("key = \"spouse:\" + Refs.Of(theirs)") && MatchMath.SpousePrefix == "spouse:",
            "one key for both kinds of marriage");
        C(MatchMath.LivesOf(MatchMath.LivesKey(MatchHome.Theirs)) == MatchHome.Theirs && MatchMath.LivesOf(MatchMath.LivesKey(MatchHome.Colony)) == MatchHome.Colony
          && MatchMath.LivesOf(null) == MatchHome.Colony, "where they live, kept in the tie");
        C(MatchMath.ParseTopic(MatchMath.Topic(12, 34), out int o, out int t, out int nt, out int g) && o == 12 && t == 34 && nt == -1 && g == -1, "a letter's topic: a colonist and one of theirs");
        C(MatchMath.ParseTopic(MatchMath.HouseTopic(12, 7, 2), out o, out t, out nt, out g) && o == 12 && t == -1 && nt == 7 && g == 2, "or one of a notable's house, made when they agree");
        C(!MatchMath.ParseTopic("match:x", out _, out _, out _, out _) && !MatchMath.ParseTopic("pact:defence:3", out _, out _, out _, out _), "anything else is no topic");
        foreach (MatchHome h in Enum.GetValues(typeof(MatchHome)))
            C(MatchMath.ParseAsk(MatchMath.AskCode(h), out var back) && back == h, $"where they live ({h}) asked in the letter and read back");
        C(MatchMath.ParseFeastTag(MatchMath.FeastTag(9), out int pl) && pl == 9 && !MatchMath.ParseFeastTag(MatchMath.Topic(1, 2), out _) && !MatchMath.ParseFeastTag(null, out _),
            "the wedding's guests carry the match's tag, apart from a letter's topic");
        C(MatchMath.Tags.Distinct().Count() == MatchMath.Tags.Length && MatchMath.Tags.All(x => x.StartsWith("tie:marriage") || x.StartsWith("match:")), "the facts' tags, each once");

        // ---------------- The mod's side ----------------

        foreach (var f in new[] { "MatchMath.cs" })
            C(!Regex.IsMatch(Code("Diplomacy/" + f), @"using (Verse|RimWorld|UnityEngine)"), $"{f} is pure");
        string matches = Code("Diplomacy/Matches.cs") + Code("Diplomacy/MatchDays.cs") + Code("Diplomacy/MatchAlliance.cs");
        string all = string.Concat(Directory.GetFiles(Path.Combine(src, "Diplomacy"), "Match*.cs").Select(File.ReadAllText)) + Code("Diplomacy/FactsMatch.cs");
        C(matches.Contains("WorldLedger.AddTie(Tie.Kind.Marriage, Refs.Of(f), Diplomacy.Colony(map), terms, Tie.Never, MatchMath.Origin)"),
            "the Marriage tie runs from their people to the colony, about the colony's own");
        C(matches.Contains("FactionMemory.Note(f, \"married\"") && matches.Contains("FactionMemory.Note(f, \"spouseWronged\"") && matches.Contains("DiplomacyMath.Wedding"),
            "the deeds married and spouseWronged, the history's dip:wedding");
        C(matches.Contains("Feasts.Host(map, p.people, guests, FeastPurpose.Wedding, p.due, MatchMath.FeastTag(p.id))") && Code("Diplomacy/MatchHooks.cs").Contains("Feasts.StageChanged += Matches.OnStage"),
            "the wedding's guests through the feasts' own entry point, the ceremony when they are at the gate");
        C(matches.Contains("TryStartMarriageCeremony") && matches.Contains("AcceptableGameConditionsToStartCeremony") && matches.Contains("WeddingPlans.Plan(a, b, WeddingPlans.Quiet)"),
            "the game's own ceremony, when the game would hold one; else the council's wedding plan");
        C(matches.Contains("MatchMath.CeremonyHour(GenLocalDate.HourOfDay(map), guests)"), "the ceremony begun only in its hours, before the feast's with the guests here");
        C(Regex.IsMatch(Code("Diplomacy/MatchDays.cs"), @"Married already[\s\S]{0,300}foreach \(var t in With\(people\)\)[\s\S]{0,200}Ceremonies\(map, ours\) != null\) return ours\.Position"),
            "the guests stay by the couple, and the feast waits, through the game's party after the vows");
        C(matches.Contains("kin ? \"CC_Match_Wed\" : \"CC_Match_WedQuiet\"") && matches.Contains("pactHad ? \"CC_Match_Wed_WhatPactHad\" : \"CC_Match_Wed_WhatNoPact\""),
            "the wedding's letter names kin among the guests only when they came, and a pact running on only when one stands");
        C(Code("Diplomacy/MatchAlliance.cs").Contains("MatchMath.SoldAway(other, theirs.IsPrisoner, other && theirs.Faction.HostileTo(Faction.OfPlayer))"),
            "the daily look reads a spouse gone over to their captors as no sale");
        C(matches.Contains("PawnRelationDefOf.Fiance") && Code("Patches/Patch_WorldEvents.cs").Contains("Matches.Married(firstPawn, secondPawn);"),
            "engaged when agreed, the alliance made where every wedding is heard");
        C(matches.Contains("Arrivals.Join(x, map, near)") && matches.Contains("c.SetFaction(p.people)") && matches.Contains("WorldLedger.Keep(c, KeepFor.Spouse)"),
            "theirs joins the colony at the gate; ours becomes one of theirs and is kept by the ledger");
        C(Code("World/Ledger/LedgerKeep.cs").Contains("public const string Spouse = \"spouse\";") && Code("World/Ledger/LedgerKeep.cs").Contains("case KeepFor.Spouse: return Matches.Holds(p);"),
            "the keep reason Spouse, and its daily check");
        C(matches.Contains("MatchObjection.Ask(h, p)") && Code("Diplomacy/MatchObjection.cs").Contains("d.Worker is DilemmaWorker_MatchAgainst"),
            "a match the colonist is against comes before the council first");
        C(Code("Diplomacy/MatchObjection.cs").Contains("Matches.ConsentOf(p.ours, p.theirs, p.lives, out _, out _) != Consent.Unwilling"), "a colonist who comes round is agreed without the question");
        C(matches.Contains("Nature.Felt(p.ours, NatureAct.MatchAgainstHeart)"), "a match pressed goes against the colonist's nature (felt once round d switches it on)");
        string dlm = File.ReadAllText(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_Match.xml"));
        C(dlm.Contains("DilemmaEffect_MatchPress") && dlm.Contains("DilemmaEffect_MatchWithdraw") && dlm.Contains("Rimfolk_MatchPressed") && dlm.Contains("<defaultOption>1</defaultOption>"),
            "the council's question: press it (the colonist's thought) or withdraw it, withdrawn when nobody answers");
        C(File.ReadAllText(Path.Combine(root, "Defs", "MoodHintDefs", "Rimfolk_MoodHints_Matches.xml")).Contains("<li>Rimfolk_MatchPressed</li>"), "the new mood has a mood hint");
        C(matches.Contains("Pacts.Make(f, map, PactKind.Defence, false, null, 0)") && matches.Contains("!Ages.IsEmpire(f)") && matches.Contains("Pacts.Of(f, home, PactKind.Defence)"),
            "the alliance stands as one defense pact, kept running while it holds; never with the Empire");
        C(Code("World/Notables.cs").Contains("Matches.RegardBonus(f)") && matches.Contains("foreach (var t in Live())") && !Regex.IsMatch(Code("Diplomacy/MatchAlliance.cs"), @"RegardBonus[\s\S]{0,400}Views\(\)"),
            "their notables think better of the colony through Rimfolk's own marriages, never Diplomatic Marriage+'s");
        C(Code("Diplomacy/MatchWarnings.cs").Contains("Found(home, c, null, kin.Name, now, \"Letter\", town)"), "in-laws write when a raiders' camp is set against the colony");
        C(matches.Contains("ChildOf(") && matches.Contains("t.terms.count = 1"), "a child of the two keeps it after a death");
        C(matches.Contains("Tie.Kind.Grudge") && matches.Contains("MatchMath.GrudgeKey") && matches.Contains("PassesDown("), "a grudge for a spouse wronged, carried down to the next head");
        C(Regex.IsMatch(Code("Diplomacy/MatchHooks.cs"), @"\[HarmonyPatch\(typeof\(Pawn\), nameof\(Pawn\.Kill\)\)\]\s*public static class Patch_MatchKilled") && Code("Diplomacy/MatchHooks.cs").Contains("RimfolkPatches.On"),
            "a death by the colony's hand heard as it happens, with the deeper hooks");
        C(Code("Diplomacy/Diplomacy.cs").Contains("Matches.Hourly(now)") && Code("Diplomacy/Diplomacy.cs").Contains("Matches.Daily(now)"), "the hour and the day ride on diplomacy's");
        string book = Code("Diplomacy/DiplomacyBook.cs");
        C(book.Contains("Scribe_Collections.Look(ref matches, \"matches\", LookMode.Deep)") && book.Contains("Matches.HomeLost(h)") && book.Contains("h.matches.Count > 0"),
            "the home's part keeps its matches; a home lost ends them");
        C(Code("Diplomacy/DiplomacyLetters.cs").Contains("MatchLetters.Owns(p)") && Code("Diplomacy/DiplomacyActs.cs").Contains("MatchActs.Talked(c, x, a, pick.code)"),
            "the letter purpose and the face-to-face act in the frames");
        C(Code("Text/Facts/FactsLedger.cs").Contains("MatchFacts(s, f);"), "the facts ask the marriages'");
        C(Code("Diplomacy/DiplomacyModel.cs").Contains("MatchPanels.Rows(f, home)") && Code("Diplomacy/DiplomacyModel.cs").Contains("MatchPanels.SeekAct(f)")
          && Code("Diplomacy/DiplomacyModel.cs").Contains("MatchPanels.AwayRows()") && Code("Diplomacy/DiplomacyModel.cs").Contains("MatchPanels.InlawRows()")
          && Code("Diplomacy/DiplomacyModel.cs").Contains("Matches.HereAs(p)"), "the Peoples view's rows and act, Here and away, the in-laws among the houses");
        string panels = Code("Diplomacy/MatchPanels.cs");
        C(panels.Contains("CardKind.Tie") && panels.Contains("CardKind.House") && panels.Contains("CardKind.Person") && panels.Contains("Inbox.Register(\"match\", InboxSection.Needs"),
            "the Tie, House and Person cards; the match proposed in Needs you");
        string told = string.Concat(Directory.GetFiles(Path.Combine(src, "Diplomacy"), "Match*.cs").Where(f => !f.EndsWith("MatchDebug.cs")).Select(File.ReadAllText));
        C(!Regex.IsMatch(told, @"Messages\.Message\(|Find\.LetterStack\.ReceiveLetter\("), "everything told through Tell (the dev actions' notes aside)");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool diplomacyMarriage = true;") && settings.Contains("Scribe_Values.Look(ref diplomacyMarriage, \"diplomacyMarriage\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyMarriage\""), "the setting, in the section Diplomacy");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"Diplomacy/DiplomacyBook.cs:matches\""), "the save model knows the matches");

        // ---------------- The words ----------------

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Matches.xml"))
                       + File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Matches_Variants.xml"));
        foreach (var k in new[] { "Young", "NotDrawn", "Kin", "Married", "Engaged", "Titled" }) C(keyed.Contains("<CC_Match_Why_" + k + ">"), $"why not ({k}) has its words");
        foreach (ConsentWhy w in Enum.GetValues(typeof(ConsentWhy))) C(keyed.Contains("<CC_Match_Consent_Against_" + w + ">"), $"against it ({w}) has its words");
        foreach (MatchWrong w in Enum.GetValues(typeof(MatchWrong))) C(keyed.Contains("<CC_Match_Wrong_" + w + ">"), $"the wrong {w} has its words");
        var named = Regex.Matches(all, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        C(named.Count >= 100, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyedAll.Contains("<" + k + ">"), $"{k} has its words");
        C(!keyed.Contains("\u2014") && !Regex.IsMatch(keyed, @">[^<]*!\s*<"), "no em dashes or exclamation marks in the words");
        C(!Regex.IsMatch(keyed, @"\b(honour|favour|neighbour|colour|defence)\b"), "American spelling");
        C(Regex.Matches(keyed, @"<CC_Match_\w+_[2-6]>").Count >= 20, "lines told often have fellows");
        C(keyed.Contains("killed, imprisoned, enslaved, sold or banished"), "the risks named before the choice (the setting, the agreement)");

        // The lines: every moment the code says has lines.
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Matches.xml"));
        var says = lines.Root!.Elements("ColonyCouncil.FlavorExtensionDef").First(e => e.Element("pool")?.Value == "Rimfolk_Pool_OutsiderSays").Descendants("li").Select(l => l.Value).ToList();
        var moments = Regex.Matches(all + Code("Diplomacy/DiplomacyActs.cs"), "\"(match[a-z]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(moments.Count >= 6, $"the moments said aloud ({moments.Count})");
        foreach (var m in moments) C(says.Any(l => l.StartsWith("[" + m + ",") || l.StartsWith("[" + m + "]")), $"the moment {m} has lines");
        foreach (var l in says)
        {
            string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(body.Length <= 90 && body.Count(ch => ch == '!') <= 1 && !body.Contains('\u2014'), $"'{body}' is short and plain");
        }
        var writes = lines.Root!.Elements("ColonyCouncil.FlavorExtensionDef").First(e => e.Element("pool")?.Value == "Rimfolk_Pool_NeighbourWrites").Descendants("li").Select(l => l.Value).ToList();
        C(writes.Count >= 20, $"their words in letters ({writes.Count})");
        foreach (var tone in new[] { "warm", "cordial", "cool", "refusal" }) C(writes.Any(l => l.StartsWith("[match," + tone)), $"their answer to a match, {tone}");
        C(says.Any(l => l.Contains("theirLowtech")) && says.Any(l => l.Contains("theirHightech")) && writes.Any(l => l.Contains("theirLowtech")) && writes.Any(l => l.Contains("theirHightech")),
            "words by their ways");
        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Matches.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talks.Count >= 20 && talks.Count <= 40 && talks.All(x => x.Element("topic")?.Value == "marriage"), $"talk pieces about marriages ({talks.Count})");
        foreach (var tag in new[] { MatchMath.Tag, MatchMath.OursTag, MatchMath.TheirsTag, MatchMath.YouTag, MatchMath.AgreedTag, MatchMath.ChildTag, MatchMath.WrongedTag })
            C(talks.Any(x => (x.Element("when")?.Value ?? "").Split(',').Contains(tag)), $"something is said on {tag}");
        C(talks.Any(x => x.ToString().Contains("their:tribal")) && talks.Any(x => x.ToString().Contains("their:spacer")) && talks.Any(x => x.ToString().Contains("their:empire")),
            "talk by their ways, the Empire's own");

        // The defs.
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_Matches.xml"));
        foreach (var d in new[] { "Rimfolk_MatchOffer", "Rimfolk_MatchLeave" })
            C(defs.Contains("<defName>" + d + "</defName>") && Code("Diplomacy/Matches.cs").Contains(" " + d + ";"), $"{d} is defined and in MatchDefOf");
        C(File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml")).Contains("marriage alliance"), "the Almanac's diplomacy page tells of it");
        return n;
    }
}
