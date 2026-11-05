using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2b.3, wards and hostages (docs/design/stage2-diplomacy.md, item 7; Diplomacy/WardMath.cs):
/// their offer and the gaps, asking by letter, the ward's age and kin, the skill the colony is known for and the guardian,
/// the hour-a-day lesson and its pace, how a ward is, the year's reckoning and what it does, a ward hurt or killed, the
/// colony's youth abroad (their people's skills by age, letters, the page with the Empire), hostages (adults only, the
/// scores, the term's end either way, forfeit and held), the records' bounds, the letters' codes, the facts; the round's
/// three fixes (an ended pact's answer, the trap's own deed, a truce sworn by word); and the mod's side: the game's lodger
/// quest, the keep reasons, the hooks, the cards and inbox, the save model, the defs and the words.
/// </summary>
public static class WardTests
{
    private const int D = WardMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "wards and hostages: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Their offer ----------------

        C(WardMath.YearDays == 60f && WardMath.MinRegard == 30 && WardMath.GuardianSkill == 10, "a year, their head's regard 30, a guardian with 10 in the skill");
        C(WardMath.MayOffer(false, 30, true, false, -1, -1, 0) && !WardMath.MayOffer(false, 29, true, false, -1, -1, 0), "offered at a regard of 30 or more");
        C(!WardMath.MayOffer(true, 90, true, false, -1, -1, 0), "never by a people at war");
        C(!WardMath.MayOffer(false, 50, false, false, -1, -1, 0), "only where the colony has a guardian for some skill");
        C(!WardMath.MayOffer(false, 50, true, true, -1, -1, 0), "one ward at a time at a home");
        C(!WardMath.MayOffer(false, 50, true, false, 10 * D, -1, 69 * D) && WardMath.MayOffer(false, 50, true, false, 10 * D, -1, 70 * D), "once a year per people");
        C(!WardMath.MayOffer(false, 50, true, false, -1, 10 * D, 24 * D) && WardMath.MayOffer(false, 50, true, false, -1, 10 * D, 25 * D), "a season apart at a home");
        C(WardMath.OffersToday(0.024f) && !WardMath.OffersToday(0.026f), "a people that may offers one day in forty");
        C(WardMath.SendChance(-1) == 0f && Math.Abs(WardMath.SendChance(0) - 0.3f) < 0.0001f && WardMath.SendChance(100) == 0.95f, "asked by letter: none below 0, three in ten at 0, never certain");
        C(!WardMath.SendsWhenAsked(true, 100, 0f) && WardMath.SendsWhenAsked(false, 30, 0.59f) && !WardMath.SendsWhenAsked(false, 30, 0.61f), "never by a people at war; by regard");
        C(WardMath.WardAge(true) == (9f, 12f) && WardMath.WardAge(false) == (14f, 19f), "a child of 9 to 12 where the game has children, else 14 to 19");
        C(WardMath.CanBeParent(40f, 12f) && WardMath.CanBeParent(30f, 16f) && !WardMath.CanBeParent(28f, 16f), "their notable is their parent where the ages fit");

        // ---------------- The skill and the guardian ----------------

        C(WardMath.KnownFor(new[] { 3, 12, 15, 9 }) == 2, "the skill the colony is known for: its best");
        C(WardMath.KnownFor(new[] { 9, 9, 9 }) == -1 && WardMath.KnownFor(null) == -1, "none when nobody has 10 in anything");
        C(WardMath.KnownFor(new[] { 12, 12 }) == 0, "where two tie, the first");
        C(WardMath.KnownFor(new[] { 14, 12 }, new[] { true, false }) == 1, "a skill barred is passed over");

        // ---------------- Lessons ----------------

        C(WardMath.LessonTicks == 2500, "a lesson is an hour");
        C(WardMath.LessonDue(-1, 0, 8) && WardMath.LessonDue(4, 5, 19) && !WardMath.LessonDue(5, 5, 12), "once a day");
        C(!WardMath.LessonDue(4, 5, 7) && !WardMath.LessonDue(4, 5, 20), "by day, from eight to eight");
        C(Math.Abs(WardMath.LessonXpPerTick(10, 10) - 0.06f) < 0.0001f && Math.Abs(WardMath.LessonXpPerTick(20, 0) - 0.2f) < 0.0001f
          && Math.Abs(WardMath.LessonXpPerTick(15, 5) - 0.16f) < 0.0001f, "a little at least, more the further the guardian leads, 0.2 a tick at most");
        float hourXp = WardMath.LessonXpPerTick(14, 2) * WardMath.LessonTicks;
        C(hourXp >= 300f && hourXp <= 500f, $"an hour with a good guardian is a few hundred practice ({hourXp:0})");

        // ---------------- How a ward is ----------------

        C(WardMath.Mood(0.2f, 30f) == WardMood.Homesick && WardMath.Mood(0.45f, 3f) == WardMood.Homesick && WardMath.Mood(0.55f, 3f) == WardMood.Settling,
            "homesick when low, or in the first days without a good mood");
        C(WardMath.Mood(0.65f, 20f) == WardMood.Happy && WardMath.Mood(0.5f, 20f) == WardMood.Settling && WardMath.Mood(0.35f, 20f) == WardMood.Homesick,
            "after ten days, their mood alone");
        C(WardMath.Fed(1, 10) && !WardMath.Fed(2, 10) && WardMath.Fed(6, 60) && !WardMath.Fed(7, 60) && WardMath.Fed(0, 0), "fed: hungry on a tenth of the days at most");

        // ---------------- The year's end ----------------

        C(WardMath.Year(40, 60, 0, 0, 0.7f) == WardResult.Fine, "taught, fed, unhurt and content: a fine year");
        C(WardMath.Year(20, 60, 0, 0, 0.5f) == WardResult.Good && WardMath.Year(40, 60, 0, 1, 0.6f) == WardResult.Good, "short of one: a good year");
        C(WardMath.Year(5, 60, 3, 1, 0.45f) == WardResult.Fair && WardMath.Year(5, 60, 10, 1, 0.45f) == WardResult.Poor, "fed and fairly content, little more: a fair year");
        C(WardMath.Year(0, 60, 30, 4, 0.2f) == WardResult.Poor, "untaught, hungry, hurt and unhappy: a poor year");
        C(WardMath.HomeRegard(WardResult.Fine) == 20 && WardMath.HomeRegard(WardResult.Good) == 12 && WardMath.HomeRegard(WardResult.Fair) == 4 && WardMath.HomeRegard(WardResult.Poor) == -10,
            "their head's regard by the year");
        C(WardMath.HomeGoodwill(WardResult.Fine) > WardMath.HomeGoodwill(WardResult.Good) && WardMath.HomeGoodwill(WardResult.Poor) < 0, "their goodwill by the year");
        C(FavourMath.Earns("fostered", true, WardMath.FosteredWeight(WardResult.Fine)) && !FavourMath.Earns("fostered", true, WardMath.FosteredWeight(WardResult.Good)),
            "a fine year leaves a debt of honor (the deed fostered at 12), a good one doesn't");
        C(WardMath.FosteredWeight(WardResult.Poor) == 0, "a poor year is no good turn (it is a slight)");
        C(WardMath.GrownRegard(WardResult.Fine) == 25 && WardMath.GrownRegard(WardResult.Poor) < 0 && WardMath.HeirAge == 18f, "the grown ward remembers; an heir at 18");
        C(WardMath.DiedRegard(true, false) > WardMath.DiedRegard(false, false) && WardMath.DiedRegard(false, false) > WardMath.DiedRegard(false, true),
            "a ward dead in the colony's care: little if sheltered, much in the open, most at a colonist's hand");
        C(WardMath.Feud(true) && !WardMath.Feud(false) && WardMath.FeudKey == "ward", "a death at a colonist's hand is a feud");

        // ---------------- Our ward ----------------

        C(WardMath.YouthMayGo(14f, false, false) && !WardMath.YouthMayGo(13f, false, false) && WardMath.YouthMayGo(10f, true, false) && !WardMath.YouthMayGo(9f, true, false)
          && !WardMath.YouthMayGo(20f, true, false) && !WardMath.YouthMayGo(12f, true, true), "the colony's youth: 14 to 19, or 10 to 19 where the game has children");
        foreach (var age in new[] { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer })
            C(WardMath.TheirSkills(age).Length == 2 && WardMath.TheirSkill(age, 0f) == WardMath.TheirSkills(age)[0] && WardMath.TheirSkill(age, 0.999f) == WardMath.TheirSkills(age)[1],
                $"{age}: what their people are good at, by a roll");
        C(WardMath.TheirSkills(WorldAge.Tribal).Contains("Animals") && WardMath.TheirSkills(WorldAge.Medieval).Contains("Melee")
          && WardMath.TheirSkills(WorldAge.Industrial).Contains("Construction") && WardMath.TheirSkills(WorldAge.Spacer).Contains("Intellectual"), "by their ways");
        C(WardMath.AbroadXp(60f, false) == 18000f && WardMath.AbroadXp(90f, false) == 18000f && WardMath.AbroadXp(60f, true) == 0f, "a year's practice; a page learns no skill");
        C(WardMath.LetterDue(10f, 0) && !WardMath.LetterDue(9.9f, 0) && WardMath.LetterDue(20f, 1) && !WardMath.LetterDue(20f, 2), "a letter home every ten days");
        C(WardMath.AbroadRegard(true) > WardMath.AbroadRegard(false) && WardMath.FriendRegard == 25, "a page's letters raise more regard; a friend made there");
        C(!FavourMath.Earns("taught", false, WardMath.TaughtWeight), "their teaching is remembered (taught), short of a debt");

        // ---------------- Hostages ----------------

        C(WardMath.MayBeHostage(18f, false) && !WardMath.MayBeHostage(17.9f, false) && !WardMath.MayBeHostage(30f, true), "adults only: children are never hostages");
        int base0 = PactMath.TruceScore(-50, false, false, false, TrucePrice.None, TruceTerm.Season);
        C(PactMath.TruceScore(-50, false, false, false, TrucePrice.TheirHostage, TruceTerm.Season) == base0 + WardMath.TheirHostageScore
          && PactMath.TruceScore(-50, false, false, false, TrucePrice.OurHostage, TruceTerm.Season) == base0 + WardMath.OurHostageScore
          && WardMath.TheirHostageScore < 0 && WardMath.OurHostageScore > 0, "a hostage of theirs makes a truce harder for them, one of the colony's easier");
        C(WardMath.PactRoll(0.5f, WardMath.TheirHostageScore) > 0.5f && WardMath.PactRoll(0.5f, WardMath.OurHostageScore) < 0.5f
          && Math.Abs(WardMath.PactRoll(0.5f, -12) - 0.7f) < 0.0001f, "and a pact the same, over the pact's own curve");
        foreach (TrucePrice p in new[] { TrucePrice.TheirHostage, TrucePrice.OurHostage }) C(PactMath.ParsePrice(PactMath.PriceKey(p)) == p, $"the price {p} kept and read back");
        C(WardMath.EndOf(true, false, false) == WardMath.HostageEnd.Holds && WardMath.EndOf(false, false, false) == WardMath.HostageEnd.Home
          && WardMath.EndOf(false, true, true) == WardMath.HostageEnd.TheyBroke && WardMath.EndOf(false, true, false) == WardMath.HostageEnd.WeBroke,
            "the term's end from its tie: holds, home, broken by them, broken by the colony");
        C(WardMath.WouldJoin(0.6f, 12f) && !WardMath.WouldJoin(0.4f, 30f) && !WardMath.WouldJoin(0.9f, 5f), "a forfeit hostage would stay only content and settled");
        C(WardMath.ForfeitDays == 5f && WardMath.ReturnedWeight(40f) > WardMath.ReturnedWeight(10f), "unanswered, they slip away; returned, remembered by how long kept");

        // ---------------- The records and the codes ----------------

        var none = new List<(WardKind, int)>();
        C(WardMath.MaxPerHome == 4 && WardMath.Fits(none, WardKind.Theirs, -1), "four records a home at most");
        C(!WardMath.Fits(new List<(WardKind, int)> { (WardKind.Theirs, -1) }, WardKind.Theirs, -1) && WardMath.Fits(new List<(WardKind, int)> { (WardKind.Theirs, -1) }, WardKind.Ours, -1),
            "one ward each way");
        C(!WardMath.Fits(new List<(WardKind, int)> { (WardKind.TheirHostage, 7) }, WardKind.TheirHostage, 7)
          && WardMath.Fits(new List<(WardKind, int)> { (WardKind.TheirHostage, 7) }, WardKind.TheirHostage, 8)
          && WardMath.Fits(new List<(WardKind, int)> { (WardKind.TheirHostage, 7) }, WardKind.OurHostage, 7), "one hostage each way per tie");
        C(!WardMath.Fits(new List<(WardKind, int)> { (WardKind.TheirHostage, 1), (WardKind.TheirHostage, 2), (WardKind.OurHostage, 1), (WardKind.Ours, -1) }, WardKind.Theirs, -1),
            "and four in all");
        C(WardMath.ParseSkill(WardMath.SkillCode("Crafting"), out string sk) && sk == "Crafting" && !WardMath.ParseSkill("skill:", out _) && !WardMath.ParseSkill("youth:3", out _),
            "the skill asked kept in the letter and read back");
        C(WardMath.ParseYouth(WardMath.YouthCode(123), out int yid) && yid == 123 && !WardMath.ParseYouth("youth:-1", out _) && !WardMath.ParseYouth("skill:x", out _),
            "the youth offered kept and read back");
        C(WardMath.ParseHostage(WardMath.HostageCode(false), out bool o1, out _) && !o1 && WardMath.ParseHostage(WardMath.HostageCode(true, 42), out bool o2, out int hid) && o2 && hid == 42
          && !WardMath.ParseHostage("hostage:None", out _, out _) && !WardMath.ParseHostage(null, out _, out _), "a pact's hostage term kept and read back; none is none");
        C(WardMath.AskTopic != WardMath.SendTopic, "asking and offering are two topics");

        // ---------------- The facts ----------------

        C(WardMath.TagsOf(WardKind.Theirs, WardStage.Here).SequenceEqual(new[] { "tie:ward", "tie:ward:theirs" })
          && WardMath.TagsOf(WardKind.Ours, WardStage.Away).SequenceEqual(new[] { "tie:ward", "tie:ward:ours" }), "a ward's tags, by which side");
        C(WardMath.TagsOf(WardKind.TheirHostage, WardStage.Forfeit).SequenceEqual(new[] { "tie:hostage", "tie:hostage:theirs", "tie:hostage:due" })
          && WardMath.TagsOf(WardKind.OurHostage, WardStage.Held).Contains("tie:hostage:due") && !WardMath.TagsOf(WardKind.TheirHostage, WardStage.Here).Contains("tie:hostage:due"),
            "a hostage forfeit or held is due");
        C(WardMath.Tags.Distinct().Count() == WardMath.Tags.Length && WardMath.Tags.Contains("them:ward:homesick") && WardMath.Tags.Contains("them:fostered"), "the tags, each once");
        C(WardMath.WardKey == "WARD" && WardMath.HostageKey == "HOSTAGE" && WardMath.GuardianKey == "GUARDIAN", "{WARD}, {HOSTAGE}, {GUARDIAN}");

        // ---------------- The round's three fixes ----------------

        string dipLetters = Code("Diplomacy/DiplomacyLetters.cs");
        C(Code("World/NeighbourLetters.cs").Contains("DiplomacyLetters.QuoteTags(l, n, tags);"), "their own words in an answer take the diplomacy's tags");
        C(dipLetters.Contains("tags.Remove(\"pact\");") && dipLetters.Contains("tags.Add(PactEndTag);") && dipLetters.Contains("else if (tone > AnswerTone.Curt) tone = AnswerTone.Curt;"),
            "a pact ended by the colony's letter is answered under its own tag, cool or curt, never as a refused offer");
        C(dipLetters.Contains("tags.Remove(\"truce\");") && dipLetters.Contains("Truces.ByWord(n.faction)") && dipLetters.Contains("!tags.Contains(\"refusal\")"),
            "a truce agreed and sworn by word is answered under its own tag");
        string truces = Code("Diplomacy/Truces.cs");
        C(truces.Contains("TruceParley.Escort(f) == null") && truces.Contains("ByWord(f) ? \"CC_Truce_Ans_YesByWord\" : \"CC_Truce_Ans_Yes\"")
          && truces.Contains("(ByWord(f) ? \"ByWord\" : \"\")") && Code("Diplomacy/Pacts.cs").Contains("CC_Truce_AcceptedByWord")
          && Code("Diplomacy/PactsPanels.cs").Contains("CC_Truce_RowPendingNoteByWord"), "the truce's keyed words by how it is sworn");
        C(Code("Diplomacy/Ransoms.cs").Contains("FactionMemory.Note(f, \"trapped\"") && !Code("Diplomacy/Ransoms.cs").Contains("\"brokeWord\"")
          && Regex.IsMatch(Code("World/FactionMemory.cs"), "\\[\"trapped\"\\] = K\\(DeedSide\\.TheirWrong, 12\\)"), "the trap at an exchange is its own deed, trapped");
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (var k in new[] { "CC_Memory_Deed_trapped", "CC_Truce_Ans_YesByWord", "CC_Truce_OfferByWord", "CC_Truce_Offer_CallByWord", "CC_Truce_AcceptedByWord", "CC_Truce_RowPendingNoteByWord" })
            C(keyedAll.Contains("<" + k + ">"), $"{k} has its words");
        var ends = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PactEnds.xml")).Descendants("li").Select(x => x.Value).ToList();
        C(ends.Any(l => l.StartsWith("[pactend,cool")) && ends.Any(l => l.StartsWith("[pactend,curt")) && !ends.Any(l => l.StartsWith("[pactend,refusal")),
            "an ended pact's own words: cool or curt, never a refusal");
        C(ends.Any(l => l.StartsWith("[trucebyword,cordial")) && ends.Any(l => l.StartsWith("[trucebyword,cool")) && !ends.Any(l => l.Contains("gate")),
            "a truce sworn by word: its own words, none promising an envoy at the gate");
        var trapTalks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Trap.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(trapTalks.Count >= 2 && trapTalks.All(t => (t.Element("when")?.Value ?? "").Contains("deed:trapped")), "talk of the trap under its own deed");
        C(!File.ReadAllText(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Honour.xml")).Contains("deed:trapped"),
            "and a debt refused keeps its own words");

        // ---------------- The mod's side ----------------

        var dip = Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        foreach (var f in new[] { "WardMath.cs", "Wards.cs", "WardsAbroad.cs", "Hostages.cs", "WardLord.cs", "WardLetters.cs", "WardPanels.cs", "WardWords.cs", "FactsWards.cs", "WardDebug.cs" })
            C(dip.ContainsKey(f), $"{f} in Diplomacy/");
        C(!Regex.IsMatch(dip["WardMath.cs"], @"using (Verse|RimWorld|UnityEngine)"), "WardMath.cs is pure");
        foreach (var (file, text) in dip.Where(kv => (kv.Key.StartsWith("Ward") || kv.Key == "Hostages.cs") && kv.Key != "WardDebug.cs"))
        {
            C(!text.Contains("Messages.Message(") && !text.Contains("Find.LetterStack.ReceiveLetter("), $"{file} tells the player only through Tell");
            C(!Regex.IsMatch(text, @"TicksToDays|ToStringTicksToPeriod|days left|hours left"), $"{file} never shows a time left");
        }
        foreach (var f in new[] { "Wards.cs", "WardsAbroad.cs", "Hostages.cs", "WardLetters.cs", "WardPanels.cs" })
        {
            var scribed = Regex.Split(dip[f], @"(?=\bclass \w+)").Where(part => part.Contains("Scribe_")).ToList();
            C(scribed.Count == 0, $"{f} saves nothing of its own (the records are DiplomacyBook's, the ties the ledger's)");
        }
        string book = dip["DiplomacyBook.cs"];
        C(book.Contains("public class WardStay : IExposable") && book.Contains("public List<WardStay> wards = new List<WardStay>();")
          && book.Contains("Scribe_Collections.Look(ref wards, \"wards\", LookMode.Deep);") && book.Contains("h.wards.Count > 0") && book.Contains("Wards.HomeLost(h);"),
            "each home's part keeps its wards and hostages, waits on them, and lets them go with a lost home");
        string wards = dip["Wards.cs"];
        C(wards.Contains("new QuestPart_ExtraFaction") && wards.Contains("ExtraFactionType.HomeFaction") && Regex.IsMatch(wards, @"quest\.AddPart\(new QuestPart_ExtraFaction[\s\S]*?quest\.SetInitiallyAccepted\(\);\s*Find\.QuestManager\.Add\(quest\);"),
            "the game's lodger: the extra-faction part in a quest of Rimfolk's own before the game takes it in, under way from the start");
        C(wards.Contains("LeaveQuestPartUtility.MakePawnLeave(p, q)") && wards.Contains("Arrivals.Join(p, map)"), "joining and leaving the game's own way");
        C(!Regex.IsMatch(wards, @"class QuestPart_\w+"), "no quest part of Rimfolk's own (a save without Rimfolk keeps the game's parts only)");
        C(File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_Wards.xml")).Contains("<defName>Rimfolk_LodgerQuest</defName>") && wards.Contains("QuestRoot = \"Rimfolk_LodgerQuest\""),
            "the lodger quest's def");
        C(wards.Contains("Together.When(key, p, g, () => StartLesson(w)") && wards.Contains("Free(p) && Free(g)") && wards.Contains("AtGameLessons(p)"),
            "the lesson waits for the two to be free and together; a child with Biotech at the game's lessons");
        C(wards.Contains("WardMath.Year(lessons, w.days, w.hungryDays, w.hurt, mood)") && wards.Contains("FactionMemory.Note(f, \"fostered\"") && wards.Contains("FactionMemory.Note(f, \"slighted\""),
            "the year reckoned: fostered, or slighted");
        C(wards.Contains("WardMath.DiedRegard(sheltered, byColony)") && wards.Contains("WorldLedger.AddTie(Tie.Kind.Grudge") && dip["WardLord.cs"].Contains("class Patch_WardKilled"),
            "a ward dead in the colony's care: by shelter, a feud at a colonist's hand");
        C(wards.Contains("WorldLedger.AddTie(Tie.Kind.Ward") && dip["WardsAbroad.cs"].Contains("WorldLedger.AddTie(Tie.Kind.Ward") && dip["Hostages.cs"].Contains("WorldLedger.AddTie(Tie.Kind.Hostage"),
            "wards and hostages are the ledger's own ties");
        C(wards.Contains("WorldLedger.AddTie(Tie.Kind.Regard") && wards.Contains("WardMath.FosteredKey") && Code("World/Notables.cs").Contains("Wards.Grown(n);")
          && Code("World/Notables.cs").Contains("Wards.HeirFor(s)"), "the grown ward remembers, and may stand as their town's heir");
        string keep = Code("World/Ledger/LedgerKeep.cs");
        C(keep.Contains("public const string Ward = \"ward\";") && keep.Contains("public const string Hostage = \"hostage\";") && keep.Contains("case KeepFor.Ward: return Wards.Holds(p);")
          && keep.Contains("case KeepFor.Hostage: return Hostages.Holds(p);") && keep.Contains("EnvoysAbroad.Holds(p) || WardsAbroad.Holds(p)"),
            "kept with KeepFor.Ward and KeepFor.Hostage, the colony's own away as lent (KeepFor.Lent), each checked daily");
        string hostages = dip["Hostages.cs"];
        C(hostages.Contains("f.kidnapped.Kidnap(p, null)") && hostages.Contains("JobDefOf.Arrest") && hostages.Contains("FactionMemory.Note(f, \"forgave\"")
          && hostages.Contains("FactionMemory.Note(f, \"hostageReturned\""), "forfeit and held the game's own way: its captivity, its arrest; forgave and hostageReturned");
        C(hostages.Contains("WardMath.MayBeHostage(") && dip["WardsAbroad.cs"].Contains("WardMath.YouthMayGo("), "children are never hostages; the youth by age");
        C(Code("Diplomacy/Truces.cs").Contains("case TrucePrice.TheirHostage:") && Code("Diplomacy/Truces.cs").Contains("case TrucePrice.OurHostage:")
          && dipLetters.Contains("Hostages.TruceOptions(f, map, PricePrefix)") && dipLetters.Contains("Hostages.PactOptions(pf, map ?? Home)") && dipLetters.Contains("Hostages.ForPact(f, home, k, l.help)"),
            "hostages as a truce's price and a defense pact's term");
        string hub = Code("Diplomacy/Diplomacy.cs");
        C(hub.Contains("Wards.Hourly(now)") && hub.Contains("Wards.Daily(now)"), "in diplomacy's hour and day");
        C(dipLetters.Contains("WardLetters.Owns(p)") && dipLetters.Contains("WardLetters.Read(l, n, l.map ?? Home, ref tone)"), "the letter purpose Ward in the existing frame");
        string panels = dip["WardPanels.cs"];
        C(panels.Contains("Inbox.Register(\"ward\", InboxSection.Needs, 35f, NeedRows, NeedCount)") && panels.Contains("Inbox.Register(\"ward-under-way\", InboxSection.UnderWay"),
            "This week: a decision on a ward or hostage in Needs you, counted; each under way in steps");
        C(panels.Contains("CardSections.RegisterLines(CardKind.Colonist") && panels.Contains("CardSections.RegisterLines(CardKind.Person") && panels.Contains("CardKind.Tie")
          && panels.Contains("CardKind.People") && panels.Contains("CardSections.TownLines("), "the ward's and hostage's card, a person's, the tie's, the people's and the town's");
        string model = Code("Diplomacy/DiplomacyModel.cs");
        C(model.Contains("WardPanels.Rows(f, home)") && model.Contains("WardPanels.AwayRows()") && model.Contains("Wards.HereAs(p)") && model.Contains("WardPanels.Acts(f)"),
            "Peoples' rows and acts, Here and away");
        C(Code("Text/Facts/FactsLedger.cs").Contains("WardFacts(s, f);"), "the facts read with the ties");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool diplomacyWards = true;") && settings.Contains("Scribe_Values.Look(ref diplomacyWards, \"diplomacyWards\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyWards\""), "the setting Wards and hostages in the section Diplomacy, on by default");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"Diplomacy/DiplomacyBook.cs:wards\"") && save.Contains("\"MaxPerHome\""), "the save model knows the list, its cap read from the source");
        C(DiplomacyMath.Kinds.Contains(DiplomacyMath.Ward) && DiplomacyMath.Kinds.Contains(DiplomacyMath.Hostage) && DiplomacyMath.Ward == "dip:ward", "dip:ward and dip:hostage in the history");
        C(dip["WardDebug.cs"].Contains("[DebugAction(\"Rimfolk\", \"Wards: ") && dip["WardDebug.cs"].Contains("[DebugAction(\"Rimfolk\", \"Hostages: "), "dev actions for each part");

        // ---------------- The defs and the words ----------------

        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Wards.xml"));
        var names = defs.Root!.Elements().Select(x => x.Element("defName")?.Value).ToHashSet();
        string lord = dip["WardLord.cs"];
        foreach (Match m in Regex.Matches(lord.Substring(lord.IndexOf("class WardDefOf", StringComparison.Ordinal)), @"public static \w+ (Rimfolk_\w+);"))
            C(names.Contains(m.Groups[1].Value), $"the def {m.Groups[1].Value} is in Rimfolk_Wards.xml");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Wards.xml")).Root!.Elements()
            .Concat(XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Wards_Variants.xml")).Root!.Elements())
            .ToDictionary(x => x.Name.LocalName, x => x.Value);
        void Keys(string prefix, IEnumerable<string> suffixes)
        {
            foreach (var s in suffixes) C(keyed.ContainsKey(prefix + s), $"{prefix}{s} exists");
        }
        Keys("CC_Ward_Mood_", Enum.GetNames(typeof(WardMood)));
        Keys("CC_Ward_Result_", Enum.GetNames(typeof(WardResult)));
        Keys("CC_Ward_Home_", Enum.GetNames(typeof(WardResult)));
        Keys("CC_Ward_Hist_Home_", Enum.GetNames(typeof(WardResult)));
        Keys("CC_Ward_Letter_", new[] { "Tribal", "Medieval", "Industrial", "Spacer", "Page" });
        Keys("CC_Hostage_Row_", new[] { "Coming", "Here", "Going", "Forfeit", "Held" });
        Keys("CC_Ward_Row_", new[] { "Coming", "Here", "Going", "Ours_Coming", "Ours_Road", "Ours_There", "Ours_Going" });
        Keys("CC_Truce_Price_", new[] { "TheirHostage", "OurHostage" });
        Keys("CC_Letter_", new[] { "Purpose_Ward", "PurposeCap_Ward" });
        var named = Regex.Matches(string.Concat(dip.Where(kv => kv.Key.StartsWith("Ward") || kv.Key == "Hostages.cs").Select(kv => kv.Value)), "\"(CC_(Ward|Hostage)_[A-Za-z_]+)\"")
            .Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 150, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyed.ContainsKey(k), $"{k} has its words");
        foreach (var (k, v) in keyed)
        {
            C(!v.Contains('!') && !v.Contains('—'), $"{k}: no exclamation marks or em dashes");
            C(!Regex.IsMatch(v, @"\b(in|within) \d+ (days|hours)\b|\bdays left\b|\bhours left\b"), $"{k}: no countdown");
            C(!Regex.IsMatch(v, @"\b(honour|favour|neighbour|colour|defence)", RegexOptions.IgnoreCase), $"{k}: American spelling");
        }
        C(keyed.Keys.Count(k => Regex.IsMatch(k, @"_\d$")) >= 25, "lines told often have fellows to vary them");
        var ages = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ages.xml"));
        C(!Regex.IsMatch(ages, @"<CC_Form_Ward_\w+_Rite>[^<]*(hostage|guarantor)"), "the ward's rites say the ward's side only: hostages have forms of their own");

        // Bubbles: each moment the code says has lines in the pool.
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Wards.xml")).Descendants("li").Select(x => x.Value).ToList();
        string all = string.Concat(dip.Values);
        foreach (var moment in new[] { "wardlesson", "wardlearn", "wardmeet", "wardkin", "wardbye", "hostagego" })
        {
            C(all.Contains("\"" + moment + "\""), $"the code says '{moment}'");
            C(lines.Count(l => l.StartsWith("[" + moment + ",")) >= 2, $"two or more bubbles for '{moment}'");
        }
        foreach (var tone in new[] { "warm", "cordial", "cool", "refusal" })
        {
            C(lines.Any(l => l.StartsWith("[ward," + tone) && l.Contains("askward")), $"their answer to asking for a ward, {tone}");
            C(lines.Any(l => l.StartsWith("[ward," + tone) && l.Contains(",send")), $"their answer to a youth offered, {tone}");
        }
        C(lines.Any(l => l.Contains(",page]")), "the Empire's own answer: a page");
        C(lines.Count(l => l.Contains("tie:ward") || l.Contains("tie:hostage")) >= 8, "letters of any purpose while a ward or hostage is with either side");
        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Wards.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talks.Count >= 20 && talks.Count <= 40, $"conversation pieces about wards and hostages ({talks.Count})");
        string talkText = string.Join("\n", talks.Select(x => x.ToString()));
        foreach (var tag in WardMath.Tags.Where(t => t != "tie:hostage")) C(talkText.Contains(tag), $"the talk speaks of {tag}");
        foreach (var ph in new[] { "{WARD}", "{HOSTAGE}", "{GUARDIAN}" }) C(talkText.Contains(ph), $"the talk names {ph}");
        C(talkText.Contains("their:tribal") && talkText.Contains("their:empire") && talkText.Contains("their:hightech") && talkText.Contains("them:young"),
            "by their ways, and a child ward's own");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml"));
        foreach (var w in new[] { "Wards.", "The colony's own youth.", "Hostages.", "children are never hostages", "page in a titled household", "sworn by word", "Wards and hostages" })
            C(almanac.Contains(w), $"the Almanac's diplomacy page says \"{w}\"");
        return n;
    }
}
