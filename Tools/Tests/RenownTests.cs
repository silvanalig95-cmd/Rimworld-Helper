using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2d.1, house renown (docs/design/stage2-diplomacy.md, item 12; Diplomacy/RenownMath.cs; the
/// player's decision 17): where renown comes from and how much, fading, the bands and their hold, what a house is known
/// for, what it does (regard, good words, matches, the events it draws, a feast's guests), the words and the facts, the
/// colony's houses on the ledger, and the mod's side (the hooks in the shared code, the save, the talk and the lines).
/// First, the round c text pass's six words-versus-code fixes: the levies by their outcome, help by what is sent, a moot
/// oath's and a moot press's (and a moot task's) answers under their own tags, a secret pressed face to face apart from
/// one pressed by letter, and the fealty talk's header.
/// </summary>
public static class RenownTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "house renown: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Def(string rel) => File.ReadAllText(Path.Combine(root, "Defs", rel));
        List<string> Lis(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel)).Descendants("li").Select(e => e.Value).ToList();
        List<XElement> Talks(string rel) => XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", rel)).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();

        // ================= The round c words' six fixes =================

        // 1. Their call on the levies, by how it ended.
        C(FealtyMath.Tags.Contains("fealty:levies:sent") && FealtyMath.Tags.Contains("fealty:levies:refused"), "the levies' outcome has its tags");
        bool sent = false, refused = false;
        FealtyMath.LevyOutcome(true, false, ref sent, ref refused);
        C(sent && !refused, "levies that came to their town: sent");
        sent = refused = false;
        FealtyMath.LevyOutcome(false, true, ref sent, ref refused);
        C(!sent && refused, "a call refused (or unanswered): refused, never sent");
        string fealtyFacts = Code("Diplomacy/FactsFealty.cs");
        C(!fealtyFacts.Contains("ParseArms") && Regex.IsMatch(fealtyFacts, @"if \(sent\)\s*\{\s*f\.Tag\(FealtyMath\.LeviesTag"),
            "fealty:levies (whose older lines say the levies came) only when they came; a call still open says neither");
        string fealty = Code("Diplomacy/Fealty.cs");
        C(Regex.IsMatch(fealty, @"LeviesCame[\s\S]{0,400}new HistoryActor\(Refs\.Of\(f\), HistoryRole\.For\)")
          && Regex.IsMatch(fealty, @"LeviesRefused[\s\S]{0,700}new HistoryActor\(Refs\.Of\(f\), HistoryRole\.Against\)"), "the history keeps the outcome: their people For it when sent, Against when refused");
        var leviesTalk = Talks("Rimfolk_OutsiderTalks_LeviesPressed.xml");
        var refusedTalk = leviesTalk.Where(t => (t.Element("when")?.Value ?? "").Contains("fealty:levies:refused")).ToList();
        C(refusedTalk.Count >= 3 && refusedTalk.SelectMany(t => t.Descendants("li")).All(l => !Regex.IsMatch(l.Value, @"you sent|stood with|came when", RegexOptions.IgnoreCase)),
            "a refusal has its own words, none saying the levies came");
        // 2. Help asked for, answered by what is sent.
        C(NotableMath.HelpQuoteTone(AnswerTone.Cool, false) == AnswerTone.Refusal && NotableMath.HelpQuoteTone(AnswerTone.Warm, true) == AnswerTone.Warm
          && NotableMath.HelpQuoteTone(AnswerTone.Cordial, true) == AnswerTone.Cordial && NotableMath.HelpQuoteTone(AnswerTone.Curt, false) == AnswerTone.Refusal
          && NotableMath.HelpQuoteTone(AnswerTone.Silence, false) == AnswerTone.Silence, "help is quoted in its tone only when it is sent, else in the refusal's words");
        C(Code("World/NeighbourLetters.cs").Contains("l.purpose == LetterPurpose.Help && things.Count > 0") && Code("World/NeighbourLetters.cs").Contains("NotableMath.HelpQuoteTone(tone, helpSent)"),
            "the letter's words for help are picked by the help it brings");
        var overtaken = Lis("FlavorPoolDefs/Rimfolk_Lines_Overtaken.xml");
        C(overtaken.Any(l => l.StartsWith("[help,cordial,fealty:oath]")), "the cool line's words kept as a cordial line, true when help comes");
        // 3. An oath overtaken by events.
        string fealtyLetters = Code("Diplomacy/FealtyLetters.cs"), dipLetters = Code("Diplomacy/DiplomacyLetters.cs");
        C(fealtyLetters.Contains("DiplomacyLetters.Moot(l);") && fealtyLetters.Contains("Fealty.SwearWhyNot(f, home) != null") && Regex.IsMatch(fealtyLetters, @"Moot\(l\);\s*tone = AnswerTone\.Cool;"),
            "a moot oath answer: marked moot and cool (it changes little), never a refusal");
        C(dipLetters.Contains("tags.Remove(\"fealty\");") && dipLetters.Contains("tags.Add(FealtyMootTag);") && dipLetters.Contains("mootLetter = -1;"),
            "answered under fealtymoot in place of fealty, the mark reset for each answer");
        var fealtyMoot = overtaken.Where(l => l.StartsWith("[fealtymoot")).ToList();
        C(fealtyMoot.Count >= 3 && fealtyMoot.All(l => !Regex.IsMatch(l, @"^\[[^\]]*(warm|cordial|cool|curt|refusal)")), "a moot oath's words, with no tone of their own");
        // 4. A press (and a task) overtaken by events.
        string taskLetters = Code("Diplomacy/TaskLetters.cs"), secretTies = Code("Diplomacy/SecretTies.cs");
        C(taskLetters.Contains("moot ? PressMootTag : PressedTag") && taskLetters.Contains("tags.Add(TaskMootTag);") && secretTies.Contains("out bool moot"),
            "a press no longer pressed answers under pressmoot, a task over under taskmoot");
        var pressMoot = overtaken.Where(l => l.StartsWith("[task,pressmoot")).ToList();
        C(pressMoot.Count >= 3 && pressMoot.All(l => !Regex.IsMatch(l, @"silver|pay", RegexOptions.IgnoreCase)), "a moot press promises no silver");
        C(overtaken.Count(l => l.StartsWith("[task,taskmoot")) >= 3, "a moot task's own words");
        // 5. Pressed face to face.
        C(KeeperMath.PressedKeys(true).Contains(KeeperMath.PressedKey) && KeeperMath.PressedKeys(true).Contains(KeeperMath.PressedFaceKey)
          && KeeperMath.PressedKeys(false).Contains(KeeperMath.PressedLetterKey) && !KeeperMath.PressedKeys(false).Contains(KeeperMath.PressedFaceKey), "a press remembered, and how");
        C(KeeperMath.PressedTagOf(KeeperMath.PressedKey) == "them:pressed" && KeeperMath.PressedTagOf(KeeperMath.PressedLetterKey) == "them:pressed:letter"
          && KeeperMath.PressedTagOf(KeeperMath.PressedFaceKey) == "them:pressed:face", "them:pressed either way, :letter and :face for how");
        C(Regex.IsMatch(secretTies, @"Answer\(t, n, home, false, out string line\)") && Regex.IsMatch(secretTies, @"Answer\(t, n, home, true, out string line\)"), "by letter and face to face each remembered as itself");
        var keeperPressed = Talks("Rimfolk_OutsiderTalks_Tasks.xml").First(t => t.Element("defName")!.Value == "Rimfolk_OT_Keeper_Pressed");
        var parsed = OutsiderTalkMath.Parse("Rimfolk_OT_Keeper_Pressed", "keeper", null, 1f, keeperPressed.Element("when")!.Value, keeperPressed.Descendants("li").Select(l => l.Value).ToList(),
            null, null, null, new List<string>());
        C(keeperPressed.Value.Contains("letter") && parsed.when.alts.SelectMany(a => a).Any(a => a.name == "them:pressed:letter" && !a.neg),
            "the piece that speaks of the colony's letter is said only to one pressed by letter (OutsiderTalkMath.AlsoWhen)");
        var faceTalk = leviesTalk.Where(t => (t.Element("when")?.Value ?? "").Contains("them:pressed:face")).ToList();
        C(faceTalk.Count >= 3 && faceTalk.SelectMany(t => t.Descendants("li")).All(l => !l.Value.Contains("letter")), "pressed face to face: its own words, none of a letter");
        // 6. The fealty talk's header.
        C(!Def("OutsiderTalkDefs/Rimfolk_OutsiderTalks_Fealty.xml").Contains("fealty:oath (new for ten days)"), "the fealty talk's header: fealty:oath holds the whole oath");

        // ================= Where renown comes from =================

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Renown.xml"));
        string variants = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Renown_Variants.xml"));
        foreach (var kv in RenownMath.Sources)
        {
            C(kv.Value.key == kv.Key && kv.Value.cap > 0f && kv.Value.points != 0f, $"the source {kv.Key}: its key, points and cap");
            C(keyed.Contains("<CC_Renown_Part_" + kv.Key + ">"), $"the source {kv.Key} in words");
        }
        C(RenownMath.Sources.Values.Where(s => s.points < 0f).Select(s => s.key).OrderBy(x => x).SequenceEqual(
            new[] { "brawled", "brokePact", "brokeTruce", "executed", "harvested", "killedPeace", "refused", "renounced", "schemed", "slighted", "struggling", "told", "tributeShort", "wordBroken" }.OrderBy(x => x)),
            "the wrongs: a truce or pact broken, a secret pressed and told, blood shed at peace, prisoners executed or cut open, hard words and brawls, an oath renounced, a call refused, tribute short, a word broken, a struggling town");
        foreach (var m in RenownMath.MarkKinds) C(RenownMath.SourceOfMark(m) == m, $"what the peoples remember by name: {m}");
        C(RenownMath.SourceOfMark("gaveGift") == null && RenownMath.SourceOfMark(null) == null, "other marks give nothing");
        C(RenownMath.ColonyDeeds.All(RenownMath.Sources.ContainsKey), "the colony's deeds are sources");
        C(RenownMath.SourceOf(DiplomacyMath.Contest, HistoryRole.Did, false, false, false) == "contest" && RenownMath.SourceOf(DiplomacyMath.Contest, HistoryRole.With, false, false, false) == null,
            "the champion of a contest, not the colony beside them");
        C(RenownMath.SourceOf(DiplomacyMath.Wedding, HistoryRole.Did, false, false, true) == "wedding", "married into a house of theirs");
        C(RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.Did, false, true, true) == "fostered" && RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.Did, false, false, true) == "abroad"
          && RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.About, false, false, true) == null, "a ward's guardian (the ward named) and the colony's youth home; the ward themselves nothing");
        C(RenownMath.GuardianRole(true) == HistoryRole.Did && RenownMath.GuardianRole(false) == HistoryRole.With, "a guardian did it only in a good year");
        C(RenownMath.SourceOf(DiplomacyMath.Parley, HistoryRole.About, false, false, true) == "parley" && RenownMath.SourceOf(DiplomacyMath.Parley, HistoryRole.Did, false, false, true) == null,
            "the speaker named at a truce sworn");
        C(RenownMath.SourceOf(DiplomacyMath.Task, HistoryRole.Did, false, true, true) == "task" && RenownMath.SourceOf(DiplomacyMath.Task, HistoryRole.Did, true, false, true) == "told"
          && RenownMath.SourceOf(DiplomacyMath.Task, HistoryRole.Did, true, false, false) == null, "a task done; a secret pressed and told a wrong, paid nobody hears of");
        C(RenownMath.SourceOf(DiplomacyMath.Envoy, HistoryRole.Did, false, false, true) == "envoy" && RenownMath.SourceOf(DiplomacyMath.Hostage, HistoryRole.Did, false, false, true) == "hostage"
          && RenownMath.SourceOf(DiplomacyMath.Feast, HistoryRole.Did, false, false, true) == null, "a season abroad as envoy or hostage; a feast is the colony's");
        C(RenownMath.MemberKinds.All(k => DiplomacyMath.Kinds.Contains(k)), "the history's kinds read are diplomacy's own");
        var epithetDefs = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "EpithetDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var e in RenownMath.Epithets) C(epithetDefs.Contains("<defName>" + e.Key + "</defName>"), $"the name {e.Key} exists");
        C(RenownMath.EpithetFor("Council_Epithet_Healer") == KnownFor.Healers && RenownMath.EpithetFor("Council_Epithet_Scholar") == KnownFor.None, "a name's kind; one not listed counts with none");
        string allDefs = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var o in RenownMath.Offices.Keys) C(allDefs.Contains("<defName>" + o + "</defName>"), $"the record {o} exists");
        C(RenownMath.YearsOf(59f) == 0 && RenownMath.YearsOf(60f) == 1 && RenownMath.YearsOf(130f) == 2, "a year in office is 60 days");
        C(Math.Abs(RenownMath.TitlePoints(300) - 6f) < 0.001f && RenownMath.TitlePoints(2000) == 12f && RenownMath.TitlePoints(0) == 0f, "the Empire's title: a fiftieth of its seniority, twelve at most");
        C(RenownMath.SkillFor("Melee") == KnownFor.Fighters && RenownMath.SkillFor("Medicine") == KnownFor.Healers && RenownMath.SkillFor("Mining") == KnownFor.None, "their head's best skill");
        C(RenownMath.FortuneSource(70) == "thriving" && RenownMath.FortuneSource(19) == "struggling" && RenownMath.FortuneSource(-1) == null && RenownMath.FortuneSource(40) == null, "a town's fortune");

        // ================= Fading =================

        C(RenownMath.Fade(0f) == 1f && RenownMath.Fade(60f) == 1f, "full for a year");
        C(Math.Abs(RenownMath.Fade(61f) - 0.9f) < 0.001f && Math.Abs(RenownMath.Fade(75f) - 0.9f) < 0.001f && Math.Abs(RenownMath.Fade(76f) - 0.8f) < 0.001f, "then a tenth a season");
        C(Math.Abs(RenownMath.Fade(1000f) - 0.3f) < 0.001f, "down to three tenths");

        // ================= Reading =================

        var one = RenownMath.Read(new[] { new RenownMath.Hit("contest") });
        C(one.score == 6f && one.byKind[(int)KnownFor.Fighters] == 6f && one.known == KnownFor.Fighters && one.band == RenownBand.Unknown, "one contest won: 6, fighters, not yet known");
        var many = RenownMath.Read(Enumerable.Range(0, 10).Select(_ => new RenownMath.Hit("contest")));
        C(many.score == 24f && Math.Abs(many.byKind[(int)KnownFor.Fighters] - 24f) < 0.001f, "a source held to its cap, and its kind with it");
        var old = RenownMath.Read(new[] { new RenownMath.Hit("contest", 200f) });
        C(Math.Abs(old.score - 1.8f) < 0.001f, "an old deed fades");
        var held = RenownMath.Read(new[] { new RenownMath.Hit("epithet", 500f, 1f, KnownFor.Healers) });
        C(held.score == 4f && held.byKind[(int)KnownFor.Healers] == 4f, "what is held now doesn't fade, its kind its own");
        var mixed = RenownMath.Read(new[] { new RenownMath.Hit("tended", 0f, 4f), new RenownMath.Hit("epithet", 0f, 1f, KnownFor.Healers), new RenownMath.Hit("brokeTruce") });
        C(mixed.score == 8f && mixed.known == KnownFor.Healers && mixed.parts[0].Key == "tended" && mixed.parts.Any(p => p.Key == "brokeTruce" && p.Value == -8f),
            "the good and the wrongs together; the largest part first");
        var wrongs = RenownMath.Read(new[] { new RenownMath.Hit("executed", 0f, 3f) });
        C(wrongs.score == -20f && wrongs.band == RenownBand.Infamous && wrongs.known == KnownFor.None, "wrongs: known for the wrong things, for nothing good");
        C(RenownMath.Read(new[] { new RenownMath.Hit("nonsense"), new RenownMath.Hit("contest", 0f, 0f) }).score == 0f && RenownMath.Read(null).score == 0f, "unknown sources and none at all");

        // ================= Bands =================

        C(RenownMath.BandOf(9.9f) == RenownBand.Unknown && RenownMath.BandOf(10f) == RenownBand.Town && RenownMath.BandOf(30f) == RenownBand.Region
          && RenownMath.BandOf(60f) == RenownBand.Peoples && RenownMath.BandOf(-15f) == RenownBand.Infamous && RenownMath.BandOf(-14.9f) == RenownBand.Unknown,
            "a town at 10, the region at 30, the peoples at 60; infamous at -15");
        C(RenownMath.Band(28f, RenownBand.Region) == RenownBand.Region && RenownMath.Band(26.9f, RenownBand.Region) == RenownBand.Town && RenownMath.Band(28f, RenownBand.Town) == RenownBand.Town,
            "a band is held until three under its start");
        C(RenownMath.Band(-13f, RenownBand.Infamous) == RenownBand.Infamous && RenownMath.Band(-11f, RenownBand.Infamous) == RenownBand.Unknown && RenownMath.Band(-20f, RenownBand.Town) == RenownBand.Infamous,
            "infamy holds to -12, and comes at once");
        C(RenownMath.Moved(RenownBand.Town, RenownBand.Region) && !RenownMath.Moved(RenownBand.Town, RenownBand.Town), "a move is a change of band");

        // ================= Known for =================

        var kinds = new float[RenownMath.KindCount];
        kinds[(int)KnownFor.Hosts] = 5f;
        C(RenownMath.KnownForOf(kinds) == KnownFor.None, "too little to be known for");
        kinds[(int)KnownFor.Hosts] = 6f;
        C(RenownMath.KnownForOf(kinds) == KnownFor.Hosts, "six points and the most");
        kinds[(int)KnownFor.Fighters] = 9f;
        kinds[(int)KnownFor.Healers] = 9f;
        kinds[(int)KnownFor.Makers] = 6f;
        C(RenownMath.KnownForOf(kinds) == KnownFor.Fighters, "the most of it, the first on a tie");
        Array.Clear(kinds, 0, kinds.Length);
        for (int i = 1; i < kinds.Length; i++) kinds[i] = 6f;
        kinds[(int)KnownFor.Traders] = 7f;
        C(RenownMath.KnownForOf(kinds) == KnownFor.None, "nothing when no kind has three tenths of it");

        // ================= What it does =================

        C(RenownMath.RegardBonus(60f) == 15f && RenownMath.RegardBonus(100f) == 15f && RenownMath.RegardBonus(0f) == 0f && RenownMath.RegardBonus(-30f) == -10f
          && Math.Abs(RenownMath.RegardBonus(-9f) + 3f) < 0.001f, "notables think up to 15 better, up to 10 worse");
        C(RenownMath.GoodWordFactor(60f) == 1.5f && RenownMath.GoodWordFactor(0f) == 1f && RenownMath.GoodWordFactor(-40f) == 1f, "good words count up to half again");
        C(RenownMath.MatchFactor(60f) == 2f && RenownMath.MatchFactor(30f) == 1.5f && RenownMath.MatchFactor(-10f) == 1f, "matches up to twice as readily");
        C(RenownMath.MatchChance(0.6f, 2f) == 0.95f && RenownMath.MatchChance(0f, 2f) == 0f && Math.Abs(RenownMath.MatchChance(0.3f, 1.5f) - 0.45f) < 0.001f
          && RenownMath.MatchChance(0.3f, 0.5f) == 0.3f, "never certain, never below their own answer");
        C(RenownMath.BandFactor(RenownBand.Unknown) == 1f && RenownMath.BandFactor(RenownBand.Town) == 1.25f && RenownMath.BandFactor(RenownBand.Peoples) == 1.75f
          && RenownMath.BandFactor(RenownBand.Infamous) == 1f, "the events a name draws, by how far it carries");
        var fighters = new[] { new KeyValuePair<KnownFor, RenownBand>(KnownFor.Fighters, RenownBand.Region) };
        C(RenownMath.EventFactor("Council_Evt_Champion", fighters) == 1.5f && RenownMath.EventFactor("Council_Evt_Pilgrims", fighters) == 1f
          && RenownMath.EventFactor("Council_Evt_Champion", new[] { new KeyValuePair<KnownFor, RenownBand>(KnownFor.Healers, RenownBand.Peoples) }) == 1f,
            "challengers come to fighters, and only to them");
        var incidents = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "IncidentDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var kv in RenownMath.Events)
            foreach (var d in kv.Value)
                C(incidents.Contains("<defName>" + d + "</defName>"), $"the event {d} ({kv.Key}) exists");
        C(RenownMath.Kinds.All(RenownMath.Events.ContainsKey), "every kind draws something");
        C(Math.Abs(RenownMath.GuestFactor(RenownBand.Region) - 1.2f) < 0.001f && RenownMath.GuestFactor(RenownBand.Unknown) == 1f, "a feast's guests answer hosts a little more readily");
        C(RenownMath.ChallengesFighter(KnownFor.Fighters, RenownBand.Town) && !RenownMath.ChallengesFighter(KnownFor.Fighters, RenownBand.Unknown)
          && !RenownMath.ChallengesFighter(KnownFor.Hosts, RenownBand.Peoples) && RenownMath.ChallengeShare > 0.5f && RenownMath.ChallengeShare < 1f,
            "the champion challenges a fighters' house's fighter more often than not");

        // ================= The colony's houses =================

        C(RenownMath.MaxOurs == 12 && RenownMath.AtLeast == FamilyLifeMath.FamilyAtLeast && RenownMath.MembersKept == 12, "twelve houses of two or more, twelve of their people remembered");
        C(RenownMath.Kept(true, false, -1, 0) && RenownMath.Kept(false, true, 0, 999 * RenownMath.DayTicks) && RenownMath.Kept(false, false, 0, 119 * RenownMath.DayTicks)
          && !RenownMath.Kept(false, false, 0, 120 * RenownMath.DayTicks) && !RenownMath.Kept(false, false, -1, 0), "kept while one lives here or a tie names them, then two years");
        C(RenownMath.Key(" Coor ") == "coor" && RenownMath.Key("COOR") == RenownMath.Key("coor") && RenownMath.Key(null) == null, "a house by its name");
        string region = Code("World/Ledger/LedgerRegion.cs");
        C(region.Contains("Scribe_Values.Look(ref ours, \"ours\")") && region.Contains("Scribe_Collections.Look(ref folkText, \"folk\"") && region.Contains("Scribe_Values.Look(ref band, \"band\")")
          && region.Contains("h.town < 0 && !h.ours"), "the colony's houses saved beside the towns', kept on load without a town");
        C(Code("World/Ledger/LedgerRenown.cs").Contains("RenownMath.MaxOurs"), "twelve at most");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("World/Ledger/LedgerRegion.cs:folkText"), "the save model has the houses' people");

        // ================= The words and the facts =================

        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
        {
            foreach (var k in RenownMath.Kinds) C(keyed.Contains("<" + RenownMath.ForKey(k, (WorldAge)Enum.Parse(typeof(WorldAge), age)) + ">"), $"{k} in {age} words");
            foreach (var b in RenownMath.Bands) C(keyed.Contains("<" + RenownMath.BandKey(b, (WorldAge)Enum.Parse(typeof(WorldAge), age)) + ">"), $"the band {b} in {age} ways");
        }
        C(keyed.Contains("<CC_Renown_Band_Unknown>"), "not yet known, in words");
        foreach (var k in new[] { "CC_Renown_Up", "CC_Renown_UpFor", "CC_Renown_Down", "CC_Renown_Faded", "CC_Renown_Infamous", "CC_Renown_Mended" })
            C(keyed.Contains("<" + k + ">") && variants.Contains("<" + k + "_2>"), $"{k} with a fellow");
        C(!(keyed + variants).Contains("—") && !Regex.IsMatch(keyed + variants, @">[^<]*![^<]*<"), "no em dashes or exclamation marks in the words");
        C(keyed.Contains("<CC_Settings_DiplomacyRenown>") && keyed.Contains("<CC_SetNote_DiplomacyRenown>") && keyed.Contains("<CC_Settings_DiplomacyRenown_Tip>"), "the setting in words");
        var tags = RenownMath.Tags();
        C(tags.Distinct().Count() == tags.Length && tags.All(t => t.StartsWith("house:") || t.StartsWith("them:house")), "the tags, each once");
        C(RenownMath.OurTags(RenownBand.Unknown, KnownFor.Fighters).SequenceEqual(new[] { "house:ours" }), "a known-for is said only of a name known somewhere");
        C(RenownMath.OurTags(RenownBand.Peoples, KnownFor.Hosts).SequenceEqual(new[] { "house:ours", "house:known", "house:renowned", "house:wide", "house:knownfor:hosts" }), "the wider bands hold the narrower");
        C(RenownMath.OurTags(RenownBand.Infamous, KnownFor.Healers).SequenceEqual(new[] { "house:ours", "house:infamous" }), "infamy says nothing good");

        // ================= The mod's side =================

        var dip = Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        foreach (var f in new[] { "RenownMath.cs", "Renown.cs", "RenownWords.cs", "RenownPanels.cs", "FactsRenown.cs", "RenownDebug.cs" }) C(dip.ContainsKey(f), $"{f} in Diplomacy/");
        C(!Regex.IsMatch(dip["RenownMath.cs"], @"using (Verse|RimWorld|UnityEngine)"), "RenownMath.cs is pure");
        foreach (var (file, text) in dip.Where(kv => kv.Key.StartsWith("Renown") && kv.Key != "RenownDebug.cs"))
        {
            C(!text.Contains("Messages.Message(") && !text.Contains("Find.LetterStack.ReceiveLetter("), $"{file} tells the player only through Tell");
            C(!Regex.IsMatch(text, @"TicksToDays|ToStringTicksToPeriod|days left|hours left"), $"{file} never shows a time left");
        }
        C(!Regex.IsMatch(dip["Renown.cs"], @"\bpersonal\s*[+-]?=|TryAffectGoodwillWith|GainFavor"), "renown is read, never spent: it changes no regard, goodwill or honor itself");
        C(Code("World/Notables.cs").Contains("personal += Renown.RegardBonus(p);"), "notables' regard of a colonist reads their house's name");
        C(Code("Outsiders/OutsiderOutcomes.cs").Contains("shift *= Renown.GoodWordFactor(c);"), "a good word counts more");
        string matches = Code("Diplomacy/Matches.cs");
        C(matches.Contains("RenownMath.MatchChance(MatchMath.AcceptChance(") && matches.Contains("MatchMath.OfferDaily * Renown.MatchFactor(ours)"), "matches offered and accepted more readily");
        C(Code("Diplomacy/Feasts.cs").Contains("Renown.GuestFactor(home)"), "a feast's guests and a house known for hosting");
        C(Code("Happenings/Happenings.cs").Contains("w *= Renown.EventFactor(d, map);") && Code("Happenings/IncidentWorker_Happening.cs").Contains("Renown.EventFactor(def, target as Map)"),
            "the events a house draws, by Rimfolk's own picking and the storyteller's");
        C(Code("Happenings/Workers_Challenges.cs").Contains("Renown.FightersHouse(c.p)"), "the champion's challenge to a fighters' house");
        C(Code("Outsiders/Outsiders.cs").Contains("Renown.AddTags(tags,"), "bubbles know the colonist's house");
        C(Code("Text/Facts/FactsLedger.cs").Contains("RenownFacts(s, f);") && Code("Diplomacy/Diplomacy.cs").Contains("Renown.Daily(now)"), "the facts and the day");
        C(Code("Diplomacy/Wards.cs").Contains("RenownMath.GuardianRole(r >= WardResult.Good)"), "the guardian named at a ward's year's end");
        C(Code("UI/Cards/Cards.cs").Contains("RenownPanels.OurHouse(m, h);") && Code("UI/HousesPanel.cs").Contains("RenownPanels.ListLine(") && Code("UI/HousesPanel.cs").Contains("!h.ours"),
            "the House card of one of the colony's, People → Houses with renown, the region's list without the colony's own");
        C(Code("CouncilSettings.cs").Contains("public bool diplomacyRenown = true;") && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyRenown\""), "the setting, in the Diplomacy section");
        C(dip["Renown.cs"].Contains("\"renownup\"") && dip["Renown.cs"].Contains("\"renowndown\"") && dip["Renown.cs"].Contains("Diplomacy.Write(DiplomacyMath.Renown"), "a band moved: its word on the map, into the history");

        // ================= The talk and the lines =================

        var talk = Talks("Rimfolk_OutsiderTalks_Renown.xml");
        C(talk.Count >= 30 && talk.Count <= 50, $"30 to 50 talk pieces ({talk.Count})");
        C(talk.Descendants("weight").All(w => float.Parse(w.Value, System.Globalization.CultureInfo.InvariantCulture) <= 1.2f), "the talk pieces' weights stay at 1.2 or less");
        string talkText = string.Concat(talk.Select(t => t.ToString()));
        var renownLines = Lis("FlavorPoolDefs/Rimfolk_Lines_Renown.xml");
        string lineText = string.Join("\n", renownLines);
        foreach (var t in tags) C(talkText.Contains(t) || lineText.Contains(t), $"the words speak of {t}");
        foreach (var k in RenownMath.Kinds) C(talkText.Contains(RenownMath.ForTag(k) + "<") || talkText.Contains(RenownMath.ForTag(k) + ","), $"talk of a house known for {k}");
        var spoken = talk.SelectMany(t => t.Descendants("li")).Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
        C(spoken.All(l => !Regex.IsMatch(l, @"^\{(HOUSE|KNOWNFOR|RENOWN|THEIRHOUSE)\}") && !Regex.IsMatch(l, @"[.?] \{(HOUSE|RENOWN|THEIRHOUSE)\}")),
            "no sentence starts with a house's name or its renown (they read in lower case)");
        C(spoken.All(l => l.Length <= 90), "each talk line 90 characters at most");
        foreach (var m in new[] { "renownup", "renowndown" })
        {
            var said = renownLines.Where(x => x.StartsWith("[" + m + ",")).ToList();
            C(said.Count >= 5 && said.All(x => !x.Substring(x.IndexOf(']') + 1).Trim().TrimEnd('.', '?').Contains(". ") && !x.Contains("{")), $"the bubbles at {m}, one sentence each, naming nobody");
            foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" }) C(said.Any(x => x.StartsWith("[" + m + ",us," + age)), $"{m} in the {age} colony's words");
        }
        C(renownLines.Count(x => x.StartsWith("[greetings,") && x.Contains("house:")) >= 10, "letters from one of a house, by their name");
        C(renownLines.Where(x => x.StartsWith("[help,") || x.StartsWith("[invitation,")).All(x => Regex.IsMatch(x, @"^\[(help|invitation),(warm|cordial)") || !Regex.IsMatch(x, @"\bwe (send|will come)\b", RegexOptions.IgnoreCase)),
            "help and invitations promised only in a granting tone");

        return n;
    }
}
