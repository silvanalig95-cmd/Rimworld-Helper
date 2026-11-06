using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2d.1, the colony's renown (docs/design/stage2-diplomacy.md, item 12; Diplomacy/RenownMath.cs;
/// the player's decision 17; reshaped by the decision of 5 November 2026, the colony is the family, never a dynasty): where
/// renown comes from and how much, fading, the bands and their hold, what the colony is known for, each colonist's own name,
/// what it does (regard, good words, matches, the events it draws, a feast's guests, the champion's challenge), the houses
/// gone and an older save's, the words and the facts, and the mod's side (the hooks in the shared code, the talk and the lines).
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
            check(ok, "colony renown: " + what);
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
        // 4 and 5. A press and a task overtaken by events, and pressed face to face: their words and code went with the
        // keeper of secrets and the council's tasks (the player's decision, 5 November 2026).
        C(overtaken.All(l => !l.StartsWith("[task,")) && !File.Exists(Path.Combine(root, "Source", "ColonyCouncil", "Diplomacy", "SecretTies.cs"))
          && leviesTalk.All(t => !(t.Element("when")?.Value ?? "").Contains("them:pressed")), "no press or task left to answer or speak of");
        // 6. The fealty talk's header.
        C(!Def("OutsiderTalkDefs/Rimfolk_OutsiderTalks_Fealty.xml").Contains("fealty:oath (new for ten days)"), "the fealty talk's header: fealty:oath holds the whole oath");

        // ================= Where renown comes from =================

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Renown.xml"));
        string colonyKeyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Renown_Colony.xml"));
        foreach (var kv in RenownMath.Sources)
        {
            C(kv.Value.key == kv.Key && kv.Value.cap > 0f && kv.Value.points != 0f, $"the source {kv.Key}: its key, points and cap");
            C(keyed.Contains("<CC_Renown_Part_" + kv.Key + ">"), $"the source {kv.Key} in words");
        }
        C(RenownMath.Sources.Values.Where(s => s.points < 0f).Select(s => s.key).OrderBy(x => x).SequenceEqual(
            new[] { "brawled", "brokePact", "brokeTruce", "executed", "harvested", "killedPeace", "refused", "renounced", "schemed", "slighted", "tributeShort", "wordBroken" }.OrderBy(x => x)),
            "the wrongs: a truce or pact broken, the colony's scheme, blood shed at peace, prisoners executed or cut open, hard words and brawls, an oath renounced, a call refused, tribute short, a word broken");
        // The houses went (5 November 2026): no source of another town's house is left.
        C(new[] { "founding", "head", "leader", "colonyTie", "thriving", "struggling", "skill" }.All(k => !RenownMath.Sources.ContainsKey(k) && !keyed.Contains("<CC_Renown_Part_" + k + ">")),
            "no renown of other towns' houses: their town is what counts");
        foreach (var m in RenownMath.MarkKinds) C(RenownMath.SourceOfMark(m) == m, $"what the peoples remember by name: {m}");
        C(RenownMath.SourceOfMark("gaveGift") == null && RenownMath.SourceOfMark(null) == null, "other marks give nothing");
        C(RenownMath.ColonyDeeds.All(RenownMath.Sources.ContainsKey), "the colony's deeds are sources");
        C(RenownMath.SourceOf(DiplomacyMath.Contest, HistoryRole.Did, false, false, false) == "contest" && RenownMath.SourceOf(DiplomacyMath.Contest, HistoryRole.With, false, false, false) == null,
            "the champion of a contest, not the colony beside them");
        C(RenownMath.OursIn(DiplomacyMath.Contest, HistoryRole.Did) && !RenownMath.OursIn(DiplomacyMath.Contest, HistoryRole.With) && RenownMath.OursIn(DiplomacyMath.Wedding, HistoryRole.For),
            "a contest's champion is the colony's own only when the colony won it (a guest may win)");
        C(RenownMath.SourceOf(DiplomacyMath.Wedding, HistoryRole.Did, false, false, true) == "wedding", "married into another people");
        C(RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.Did, false, true, true) == "fostered" && RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.Did, false, false, true) == "abroad"
          && RenownMath.SourceOf(DiplomacyMath.Ward, HistoryRole.About, false, false, true) == null, "a ward's guardian (the ward named) and the colony's youth home; the ward themselves nothing");
        C(RenownMath.GuardianRole(true) == HistoryRole.Did && RenownMath.GuardianRole(false) == HistoryRole.With, "a guardian did it only in a good year");
        C(RenownMath.SourceOf(DiplomacyMath.Parley, HistoryRole.About, false, false, true) == "parley" && RenownMath.SourceOf(DiplomacyMath.Parley, HistoryRole.Did, false, false, true) == null,
            "the speaker named at a truce sworn");
        C(RenownMath.SourceOf(DiplomacyMath.Task, HistoryRole.Did, false, true, true) == null && !RenownMath.Sources.ContainsKey("task") && !RenownMath.Sources.ContainsKey("told"),
            "no renown from an older save's council tasks");
        C(RenownMath.SourceOf(DiplomacyMath.Envoy, HistoryRole.Did, false, false, true) == "envoy" && RenownMath.SourceOf(DiplomacyMath.Hostage, HistoryRole.Did, false, false, true) == "hostage"
          && RenownMath.SourceOf(DiplomacyMath.Feast, HistoryRole.Did, false, false, true) == null, "a season abroad as envoy or hostage; a feast is the colony's");
        C(RenownMath.SourceOf(DiplomacyMath.Hostage, HistoryRole.Did, false, false, false, true) == "hostage" && RenownMath.SourceOf(DiplomacyMath.Hostage, HistoryRole.Did, false, false, true, false) == null
          && RenownMath.SourceOf(DiplomacyMath.Hostage, HistoryRole.About, false, false, false, true) == null, "a term served for the colony, not one of theirs who stayed");
        C(Regex.IsMatch(Code("Diplomacy/WardsAbroad.cs"), @"DiplomacyMath\.Hostage, new List<HistoryActor>\s*\{\s*new HistoryActor\(Refs\.Of\(p\), HistoryRole\.Did\), new HistoryActor\(Refs\.Of\(f\), HistoryRole\.With\), new HistoryActor\(Diplomacy\.Colony\(map\), HistoryRole\.For\)")
          && Code("Diplomacy/Renown.cs").Contains("RenownMath.SourceOf(e.kind, a.role, against, about, e.Weighty, forColony)"), "the hostage home names them as having done it, for the colony");
        C(RenownMath.MemberKinds.All(k => DiplomacyMath.Kinds.Contains(k)), "the history's kinds read are diplomacy's own");
        var epithetDefs = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "EpithetDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var e in RenownMath.Epithets) C(epithetDefs.Contains("<defName>" + e.Key + "</defName>"), $"the name {e.Key} exists");
        C(RenownMath.EpithetFor("Council_Epithet_Healer") == KnownFor.Healers && RenownMath.EpithetFor("Council_Epithet_Scholar") == KnownFor.None, "a name's kind; one not listed counts with none");
        string allDefs = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var o in RenownMath.Offices.Keys) C(allDefs.Contains("<defName>" + o + "</defName>"), $"the record {o} exists");
        C(RenownMath.YearsOf(59f) == 0 && RenownMath.YearsOf(60f) == 1 && RenownMath.YearsOf(130f) == 2, "a year in office is 60 days");
        C(Math.Abs(RenownMath.TitlePoints(300) - 6f) < 0.001f && RenownMath.TitlePoints(2000) == 12f && RenownMath.TitlePoints(0) == 0f, "the Empire's title: a fiftieth of its seniority, twelve at most");

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
        var tie = RenownMath.Read(new[] { new RenownMath.Hit("tended", 0f, 2f), new RenownMath.Hit("contest") });
        C(tie.parts[0].Key == "contest" && tie.parts[1].Key == "tended", "a tie is ordered by the source's name, so a colonist's own name is the same each time");
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
        // The colony's band is saved on the diplomacy book; an older save (or a new game) has none read yet: set quietly.
        C(RenownMath.Told(RenownMath.NotRead) == RenownBand.Unknown && RenownMath.Told((int)RenownBand.Region) == RenownBand.Region && RenownMath.Told(99) == RenownBand.Unknown,
            "the band last told, from its saved number");
        C(!RenownMath.MovedFrom(RenownMath.NotRead, RenownBand.Peoples) && RenownMath.MovedFrom((int)RenownBand.Town, RenownBand.Region) && !RenownMath.MovedFrom((int)RenownBand.Town, RenownBand.Town),
            "the first look never tells: an older save's name, worked out from what it already holds, is set without a word");
        C(RenownMath.Bubble(RenownBand.Unknown, RenownBand.Town) == "renownup" && RenownMath.Bubble(RenownBand.Region, RenownBand.Peoples) == "renownup"
          && RenownMath.Bubble(RenownBand.Infamous, RenownBand.Town) == "renownup", "renownup as the name carries further");
        C(RenownMath.Bubble(RenownBand.Town, RenownBand.Unknown) == "renowndown" && RenownMath.Bubble(RenownBand.Region, RenownBand.Unknown) == "renowndown",
            "renowndown as it fades from the talk");
        C(RenownMath.Bubble(RenownBand.Infamous, RenownBand.Unknown) == null && RenownMath.Bubble(RenownBand.Town, RenownBand.Infamous) == null
          && RenownMath.Bubble(RenownBand.Unknown, RenownBand.Infamous) == null && RenownMath.Bubble(RenownBand.Region, RenownBand.Town) == null
          && RenownMath.Bubble(RenownBand.Peoples, RenownBand.Region) == null && RenownMath.Bubble(RenownBand.Town, RenownBand.Town) == null,
            "no bubble for a name gone bad or mending, nor for one still known, only less widely");
        C(RenownMath.BubbleTag(RenownBand.Region, RenownBand.Unknown) == RenownMath.WasRenownedTag && RenownMath.BubbleTag(RenownBand.Peoples, RenownBand.Unknown) == RenownMath.WasRenownedTag
          && RenownMath.BubbleTag(RenownBand.Town, RenownBand.Unknown) == null && RenownMath.BubbleTag(RenownBand.Unknown, RenownBand.Town) == null
          && RenownMath.BubbleTag(RenownBand.Region, RenownBand.Peoples) == null && RenownMath.WasRenownedTag == "colony:wasrenowned", "renowndown's tag for a name that was told across the region or wider");

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

        // ================= Each colonist's own name =================

        var champion = RenownMath.Read(new[] { new RenownMath.Hit("contest", 0f, 2f) });
        C(champion.band == RenownBand.Town && RenownMath.KnownAs(champion) == "contest" && champion.known == KnownFor.Fighters, "two contests won: known by name in a town nearby, as the champion");
        C(RenownMath.KnownAs(one) == null && RenownMath.KnownAs(null) == null, "one contest is not yet a name");
        var butcher = RenownMath.Read(new[] { new RenownMath.Hit("executed", 0f, 2f), new RenownMath.Hit("tended") });
        C(butcher.band == RenownBand.Infamous && RenownMath.KnownAs(butcher) == "executed", "known by name for the wrong things: the worst of them");
        C(RenownMath.TheyKnow(RenownBand.Region, false, true) && RenownMath.TheyKnow(RenownBand.Town, true, true) && !RenownMath.TheyKnow(RenownBand.Town, false, true)
          && RenownMath.TheyKnow(RenownBand.Town, false, false) && !RenownMath.TheyKnow(RenownBand.Unknown, true, true),
            "their people know a colonist's name across the region, or in a town nearby when the deed was done with them (or names no people)");
        C(RenownMath.NamesPeople("contest") && RenownMath.NamesPeople("tended") && !RenownMath.NamesPeople("epithet") && !RenownMath.NamesPeople("title"), "which parts are done with a people");
        C(RenownMath.OwnTags(champion, true).SequenceEqual(new[] { "us:knownas", "us:knownas:fighters" }) && RenownMath.OwnTags(champion, false).Count == 0
          && RenownMath.OwnTags(butcher, true).Count == 0 && RenownMath.OwnTags(one, true).Count == 0, "the speaker's own name in talk: only a good one, only where it is known");
        foreach (var part in RenownMath.Sources.Keys.Where(k => Array.IndexOf(RenownMath.ColonyDeeds, k) < 0 && k != "pactKept"))
        {
            C(colonyKeyed.Contains("<" + RenownMath.KnownAsWords(part, true) + ">"), $"a colonist known by name for {part}, in words");
            if (RenownMath.NamesPeople(part))
            {
                C(colonyKeyed.Contains("<" + RenownMath.KnownAsWords(part, false) + ">"), $"and without a people named, for {part}");
                C(Regex.IsMatch(colonyKeyed, "<" + RenownMath.KnownAsWords(part, true) + ">[^<]*\\{PEOPLE\\}"), $"the people named for {part}");
            }
        }
        foreach (var o in RenownMath.Offices.Keys) C(colonyKeyed.Contains("<CC_Renown_KnownAs_office_" + o + ">"), $"the office {o} in a colonist's name");
        foreach (ContestKind k in Enum.GetValues(typeof(ContestKind))) C(colonyKeyed.Contains("<CC_Renown_KnownAs_Contest_" + k + ">the "), $"what a champion won, {k}, as their name says it");
        C(Regex.Matches(colonyKeyed, @"<CC_Renown_KnownAs_\w+>the one ").Count >= 30 && !Regex.IsMatch(colonyKeyed, @"<CC_Renown_KnownAs_\w+>[^<]*\b(house|family|heir)\b"),
            "a colonist's own name: \"the one who ...\", of what they did, never of a house");

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
        C(RenownMath.OwnFactor("Council_Evt_NeighborsSick", new[] { KnownFor.Healers }) == 1.25f && RenownMath.OwnFactor("Council_Evt_NeighborsSick", new[] { KnownFor.Fighters }) == 1f
          && RenownMath.OwnFactor("Council_Evt_NeighborsSick", new KnownFor[0]) == 1f, "a healer known by name draws the sick a quarter more, no more");
        var incidents = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "IncidentDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var kv in RenownMath.Events)
            foreach (var d in kv.Value)
                C(incidents.Contains("<defName>" + d + "</defName>"), $"the event {d} ({kv.Key}) exists");
        C(RenownMath.Kinds.All(RenownMath.Events.ContainsKey), "every kind draws something");
        C(Math.Abs(RenownMath.GuestFactor(RenownBand.Region) - 1.2f) < 0.001f && RenownMath.GuestFactor(RenownBand.Unknown) == 1f, "a feast's guests answer hosts a little more readily");
        C(RenownMath.ChallengesFighter(KnownFor.Fighters, RenownBand.Town) && !RenownMath.ChallengesFighter(KnownFor.Fighters, RenownBand.Unknown)
          && !RenownMath.ChallengesFighter(KnownFor.Hosts, RenownBand.Peoples) && RenownMath.ChallengeShare > 0.5f && RenownMath.ChallengeShare < 1f,
            "the champion challenges a colonist known by name as a fighter more often than not");

        // ================= The houses gone, and an older save =================

        string renownCode = Code("Diplomacy/Renown.cs");
        string region = Code("World/Ledger/LedgerRegion.cs");
        C(!File.Exists(Path.Combine(src, "World", "Ledger", "LedgerRenown.cs")) && !File.Exists(Path.Combine(src, "UI", "HousesPanel.cs")) && File.Exists(Path.Combine(src, "UI", "RenownPage.cs")),
            "the colony's houses on the ledger and the Houses page are gone; People → Renown is where it was");
        C(!region.Contains("public bool ours") && !region.Contains("ref folkNames") && region.Contains("houses.RemoveAll(h => h == null || h.house.IsNone || h.town < 0);"),
            "an older save's houses of the colony's (no town) are dropped quietly as it loads; nothing reads their nodes");
        C(Code("Diplomacy/DiplomacyBook.cs").Contains("Scribe_Values.Look(ref renownBand, \"renownBand\", RenownMath.NotRead);"), "the colony's band saved, an older save's not yet read");
        C(Regex.IsMatch(renownCode, @"if \(book\.renownBand == RenownMath\.NotRead\)\s*\{\s*book\.renownBand = \(int\)r\.band;\s*return;"), "the first look sets the band without a word");
        C(renownCode.Contains("book.OfKind(kind)") && renownCode.Contains("ColonistMemory.AllMarks()") && renownCode.Contains("RenownMath.OursIn(e.kind, party.role)"),
            "the colony's name read from every deed of its people the history keeps, the living and the gone, and every mark the peoples keep");
        C(Regex.IsMatch(renownCode, @"KinMemory\.Ours\(p\) && WorldLedger\.KeptFor\(p, KeepFor\.Lent\)"), "one away on the colony's errand is still of its people: what they hold still counts");
        string cards = Code("UI/Cards/Cards.cs");
        C(Regex.IsMatch(cards, @"m\.kind = CardKind\.Place;\s*Place\(m\);") && !cards.Contains("RenownPanels.OurHouse"), "a house's name opens its town's card: no House card");
        C(!Code("UI/Search.cs").Contains("Part(\"houses\"") && !Code("World/Region/RegionPanel.cs").Contains("Link(RegionWords.HouseName(h), h.house)"), "no house in the search or as a link");
        C(PagesMath.Resolve("Houses") == "Renown" && PagesMath.ArticleOf("Renown") == "Council_Alm_C_Diplomacy", "the old address Houses opens the Renown page");
        C(!File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("folkText"), "the save model has no houses of the colony's");

        // ================= The words and the facts =================

        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
        {
            foreach (var k in RenownMath.Kinds) C(keyed.Contains("<" + RenownMath.ForKey(k, (WorldAge)Enum.Parse(typeof(WorldAge), age)) + ">"), $"{k} in {age} words");
            foreach (var b in RenownMath.Bands) C(keyed.Contains("<" + RenownMath.BandKey(b, (WorldAge)Enum.Parse(typeof(WorldAge), age)) + ">"), $"the band {b} in {age} ways");
        }
        C(keyed.Contains("<CC_Renown_Band_Unknown>"), "not yet known, in words");
        foreach (var b in RenownMath.Bands)
            C(RenownMath.BandKey(b, WorldAge.Spacer, true) == "CC_Renown_Band_" + b + "_Empire" && keyed.Contains("<" + RenownMath.BandKey(b, WorldAge.Spacer, true) + ">"), $"the band {b} in the Empire's court's ways");
        C(Code("Diplomacy/FactsRenown.cs").Contains("RenownWords.Band(r.band, s.people != null ? Ages.Of(s.people) : Ages.Ours, Ages.IsEmpire(s.people))"), "the one spoken to of the Empire hears the court's words");
        foreach (var k in new[] { "CC_Renown_Colony_Up", "CC_Renown_Colony_UpFor", "CC_Renown_Colony_Down", "CC_Renown_Colony_Faded", "CC_Renown_Colony_Infamous", "CC_Renown_Colony_Mended" })
            C(colonyKeyed.Contains("<" + k + ">") && colonyKeyed.Contains("<" + k + "_2>") && colonyKeyed.Contains("<" + k + "_4>") && colonyKeyed.Contains("<" + k + "_5>"), $"{k} with four fellows");
        C(!Regex.IsMatch(colonyKeyed, @"<CC_Renown_Colony_\w+>[^<]*\b(house|family|families)\b"), "the colony's news speaks of the colony, never a house or a family");
        C(!File.Exists(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Renown_Variants.xml")) && !keyed.Contains("<CC_Renown_Up>") && !keyed.Contains("{HOUSE}"),
            "the houses' news and its fellows went with them");
        C(!colonyKeyed.Contains("—") && !Regex.IsMatch(colonyKeyed, @">[^<]*![^<]*<") && !(keyed).Contains("—") && !Regex.IsMatch(keyed, @">[^<]*![^<]*<"), "no em dashes or exclamation marks");
        C(keyed.Contains("<CC_Settings_DiplomacyRenown>Colony renown</CC_Settings_DiplomacyRenown>") && keyed.Contains("<CC_SetNote_DiplomacyRenown>") && keyed.Contains("<CC_Settings_DiplomacyRenown_Tip>")
          && !Regex.IsMatch(keyed, @"<CC_Settings_DiplomacyRenown_Tip>[^<]*\bhouses?\b") && !keyed.Contains("council's business"), "the setting \"Colony renown\", its tip the colony's (and no task left in it)");
        var tags = RenownMath.Tags();
        C(tags.Distinct().Count() == tags.Length && tags.All(t => t.StartsWith("colony:") || t.StartsWith("us:knownas")), "the tags, each once: the colony's and the speaker's own name");
        C(RenownMath.ColonyTags(RenownBand.Unknown, KnownFor.Fighters).Count == 0, "a known-for is said only of a name known somewhere");
        C(RenownMath.ColonyTags(RenownBand.Peoples, KnownFor.Hosts).SequenceEqual(new[] { "colony:known", "colony:renowned", "colony:wide", "colony:knownfor:hosts" }), "the wider bands hold the narrower");
        C(RenownMath.ColonyTags(RenownBand.Infamous, KnownFor.Healers).SequenceEqual(new[] { "colony:infamous" }), "infamy says nothing good");

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
        string NoComments(string code) => Regex.Replace(code, @"//[^\n]*", "");
        C(!Regex.IsMatch(string.Concat(dip.Where(kv => kv.Key.StartsWith("Renown") || kv.Key == "FactsRenown.cs").Select(kv => NoComments(kv.Value))), @"LedgerHouse|HouseOf\(|FamilyNames"),
            "nothing of the colony's renown reads a house or a family name");
        C(Code("World/Notables.cs").Contains("personal += Renown.RegardBonus(p);"), "notables' regard of a colonist reads the colony's name");
        C(Code("Outsiders/OutsiderOutcomes.cs").Contains("shift *= Renown.GoodWordFactor(c);"), "a good word counts more");
        string matches = Code("Diplomacy/Matches.cs");
        C(matches.Contains("RenownMath.MatchChance(MatchMath.AcceptChance(") && matches.Contains("MatchMath.OfferDaily * Renown.MatchFactor(ours)"), "matches offered and accepted more readily");
        C(Code("Diplomacy/Feasts.cs").Contains("Renown.GuestFactor(home)"), "a feast's guests and a colony known for hosting");
        C(Code("Happenings/Happenings.cs").Contains("w *= Renown.EventFactor(d, map);") && Code("Happenings/IncidentWorker_Happening.cs").Contains("Renown.EventFactor(def, target as Map)"),
            "the events the colony draws, by Rimfolk's own picking and the storyteller's");
        C(renownCode.Contains("RenownMath.OwnFactor(def.defName, named)"), "and those a colonist known by name draws");
        C(Code("Happenings/Workers_Challenges.cs").Contains("Renown.KnownFighter(c.p)"), "the champion's challenge to a colonist known by name as a fighter");
        C(Code("Outsiders/Outsiders.cs").Contains("Renown.AddTags(tags,"), "bubbles know the colony's name");
        C(Code("Text/Facts/FactsLedger.cs").Contains("RenownFacts(s, f);") && Code("Diplomacy/Diplomacy.cs").Contains("Renown.Daily(now)"), "the facts and the day");
        C(Code("Diplomacy/Feasts.cs").Contains("FeastWords.Result(r), false, r.kind.ToString());"), "a contest's kind kept with it, so a champion's name says what they won");
        C(Code("Diplomacy/Wards.cs").Contains("RenownMath.GuardianRole(r >= WardResult.Good)"), "the guardian named at a ward's year's end");
        C(Code("CouncilSettings.cs").Contains("public bool diplomacyRenown = true;") && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyRenown\""), "the setting, in the Diplomacy section");
        C(dip["Renown.cs"].Contains("RenownMath.Bubble(was, r.band)") && dip["Renown.cs"].Contains("Diplomacy.Write(DiplomacyMath.Renown"), "a band moved: its word on the map, into the history");
        C(Regex.IsMatch(dip["Renown.cs"], @"FreeColonistsSpawned\.Where\(p => !p\.Dead && !p\.Downed && !p\.InMentalState && !p\.Drafted && p\.Awake\(\)"),
            "the word said by one of the colony on the seat's map, awake and free (never in their sleep)");
        C(dip["Renown.cs"].Contains("Outsiders.Say(speaker, moment, null, null, RenownMath.BubbleTag(was, r.band))")
          && Code("Outsiders/Outsiders.cs").Contains("if (!also.NullOrEmpty()) tags.Add(also);"), "the bubble said with the band it fell from");
        // Since one name (phase 4 of the review, part 1): a colonist's own name is part of their one "known as", the colony's
        // renown part of its one card.
        C(Code("Epithets/KnownAs.cs").Contains("CardKind.Colonist") && Code("Epithets/KnownAs.cs").Contains("Renown.Own(p)")
          && Code("Character/ColonyName.cs").Contains("CardSections.HomeLines(") && Code("Character/ColonyName.cs").Contains("RenownPanels.From(r)"),
            "a colonist's own name on their card in their one name, the colony's renown on a home's card in its one name");

        // ================= Shipped lines asked for more, or retired =================

        var allLis = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Descendants("li").Select(e => e.Value)).ToHashSet();
        // Besides the renown's: the text pass on the world's voices (6 November 2026) asked a chat's opening for two who
        // never talked (Outsiders.Line), a meal for what is on the plate (the shared meal), a war party's line for its foe
        // (Camp.Bubble) and a camp's farewell for the hour they set off (CampLook.FarewellText).
        var others = new HashSet<string> { "first", "returning", "fare:awful", "foe:lowtech", "morning", "day", "evening", "night" };
        C(Code("Outsiders/Outsiders.cs").Contains("tags.Add(\"first\")") && Code("Outsiders/OutsiderOutcomes.cs").Contains("\"fare:awful\"")
          && Code("Camps/Camp.cs").Contains("\"foe:lowtech\"") && Code("Camps/CampLook.cs").Contains("tags.Add(Flavor.HourTag(") && Code("Flavor.cs").Contains("\"night\" : hour < 11 ? \"morning\""),
            "the other tags the shipped lines ask for are set where those lines are picked");
        foreach (var kv in ShippedLines.AlsoWhen)
        {
            C(allLis.Contains(kv.Key), $"the shipped line is still in the defs, word for word: {kv.Key}");
            var cond = ShippedLines.ConditionOf(kv.Key);
            C(cond?.tags != null && cond.alts.SelectMany(a => a).All(a => tags.Contains(a.name) || a.name == RenownMath.WasRenownedTag || others.Contains(a.name)),
                $"its condition names tags the game sets: {kv.Value}");
        }
        foreach (var l in ShippedLines.Retired) C(allLis.Contains(l) && ShippedLines.ConditionOf(l) == ShippedLines.Never, $"the retired line is still in the defs, never said: {l}");
        var song = "[renownup,us,medieval] I hear there's a song with our name in it now.";
        C(!ShippedLines.Holds(ShippedLines.ConditionOf(song), new HashSet<string> { "renownup", "us", "medieval", "colony:known" })
          && ShippedLines.Holds(ShippedLines.ConditionOf(song), new HashSet<string> { "renownup", "us", "medieval", "colony:known", "colony:renowned" })
          && ShippedLines.Holds(ShippedLines.ConditionOf("[renownup,us] Funny to think strangers know our name."), new HashSet<string>()),
            "a song only of a name sung across the region; a line with nothing more asked always holds");
        var nearby = "[renownup,us,medieval,!house:renowned] They say our name came up at the next town's market.";
        C(ShippedLines.Holds(ShippedLines.ConditionOf(nearby), new HashSet<string> { "colony:known" }) && !ShippedLines.Holds(ShippedLines.ConditionOf(nearby), new HashSet<string> { "colony:known", "colony:renowned" }),
            "the next town's market only at the town's band, now the houses' tags ask nothing");
        // Every line in the defs that speaks of a house's facts went with them: none asks a house tag, none names {HOUSE}.
        var houseTag = new Regex(@"^\[[^\]]*(?<![!\w])(house:|them:house)");
        C(allLis.All(l => !houseTag.IsMatch(l) && !l.Contains("{HOUSE}") && !l.Contains("{THEIRHOUSE}")), "no line asks a house's facts or names a house");
        var renownLines = Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "Rimfolk_Lines_*Renown*.xml").SelectMany(f => XDocument.Load(f).Descendants("li").Select(l => l.Value)).ToList();
        C(renownLines.Where(l => Regex.IsMatch(l, @"\bfamily\b", RegexOptions.IgnoreCase)).All(l => ShippedLines.Retired.Contains(l)), "every renown line that says \"our family\" is retired");
        C(Code("Flavor.cs").Contains("p.also = ShippedLines.ConditionOf(line);") && Code("Flavor.cs").Contains("!ShippedLines.Holds(p.also, context) ? -1"), "every flavour pick asks it, once per line");

        // ================= The talk and the lines =================

        var talk = Talks("Rimfolk_OutsiderTalks_ColonyRenown.xml");
        C(talk.Count >= 50, $"talk of the colony's name ({talk.Count})");
        C(!File.Exists(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Renown.xml")) && !File.Exists(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_RenownMore.xml")),
            "the houses' talk went with their facts");
        C(talk.Descendants("weight").All(w => float.Parse(w.Value, System.Globalization.CultureInfo.InvariantCulture) <= 1.2f), "the talk pieces' weights stay at 1.2 or less");
        string talkText = string.Concat(talk.Select(t => t.ToString()));
        foreach (var t in tags) C(talkText.Contains(t), $"the talk speaks of {t}");
        foreach (var ways in new[] { "their:tribal", "their:medieval", "their:industrial", "their:spacer", "their:empire" })
            foreach (var band in new[] { "colony:known,!colony:renowned", "colony:renowned,!colony:wide" })
                C(talk.Any(t => (t.Element("when")?.Value ?? "").StartsWith(band) && (t.Element("when")?.Value ?? "").Contains(ways)), $"the colony's name known ({band}) in the words of {ways}");
        var spoken = talk.SelectMany(t => t.Descendants("li")).Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
        C(spoken.All(l => !Regex.IsMatch(l, @"^\{(KNOWNFOR|RENOWN|KNOWNAS)\}") && !Regex.IsMatch(l, @"[.?] \{(RENOWN|KNOWNAS)\}")), "no sentence starts with the colony's renown or a colonist's own name");
        C(spoken.All(l => l.Length <= 95), "each talk line short enough to read in a bubble");
        var raw = talk.SelectMany(t => t.Descendants("li")).Select(l => l.Value).ToList();
        C(raw.All(l => !(l.StartsWith("[them") && Regex.IsMatch(l, @"\byour (family|house)\b", RegexOptions.IgnoreCase)) && !(l.StartsWith("[us") && Regex.IsMatch(l, @"\bour (family|house)\b", RegexOptions.IgnoreCase))),
            "the colony's name, never a family's or a house's (a visitor's own family is theirs to be asked about)");
        C(talk.Count(t => t.Element("topic")?.Value == "callback") >= 25, "callbacks that pick up the earlier talk with the colony's name");
        C(talk.Count(t => (t.Element("when")?.Value ?? "").Contains("us:knownas")) >= 8, "visitors speak of a colonist's own name");
        var colonyLines = Lis("FlavorPoolDefs/Rimfolk_Lines_ColonyRenown.xml");
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
        {
            C(colonyLines.Any(x => x.StartsWith("[renownup,us," + age + ",colony:renowned,!colony:wide]")) && colonyLines.Any(x => x.StartsWith("[renownup,us," + age + ",colony:wide]")),
                $"renownup in the {age} colony's words across the region and among every people");
            C(colonyLines.Any(x => x.StartsWith("[renowndown,us," + age + ",colony:wasrenowned]")), $"renowndown in the {age} colony's words from the region's talk");
        }
        var bubbles = colonyLines.Where(x => Regex.IsMatch(x, @"^\[(renownup|renowndown|wordyes),")).ToList();
        C(bubbles.Count >= 25 && bubbles.All(x => !x.Substring(x.IndexOf(']') + 1).Trim().TrimEnd('.', '?').Contains(". ") && !x.Contains("{")), "the bubbles: one sentence each, naming nobody");
        var letters = colonyLines.Where(x => !bubbles.Contains(x)).ToList();
        C(letters.Count >= 30, $"their letters to one of a known colony ({letters.Count})");
        C(letters.Where(x => x.StartsWith("[help,") || x.StartsWith("[invitation,")).All(x => Regex.IsMatch(x, @"^\[(help|invitation),(warm|cordial)") || !Regex.IsMatch(x, @"\bwe (send|will come|can send)\b", RegexOptions.IgnoreCase))
          && !letters.Any(x => x.StartsWith("[help,cool")), "help and invitations promised only in a granting tone");
        C(letters.Where(x => x.StartsWith("[condolences,")).All(x => !Regex.IsMatch(x, @"your loss", RegexOptions.IgnoreCase)), "condolences answered as their own loss");
        C(letters.Any(x => x.Contains("us:knownas")), "a letter to one known by their own name");

        return n;
    }
}
