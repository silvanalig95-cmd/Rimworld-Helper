using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 1, release 7 (docs/design/stage1-region.md, item 7; decisions 12 and 13): the follow-ups of world events (the
/// warlord's son by how his father's story ended, the town that grew from the settlers' camp, the other ways a raiders'
/// camp is found: World/Region/FollowUpsMath.cs) and a people's memory of single colonists (World/ColonistMemoryMath.cs):
/// marks and their merging, fading and bounds, friend and foe, the trader's refusal, a notable's regard. Also the mod's
/// side: the hooks in the earlier events, the passes, the saves, the defs and their words.
/// </summary>
public static class FollowUpTests
{
    private const int Day = FollowUpsMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "follow-ups: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string defs = Path.Combine(root, "Defs", "WorldSituationDefs");

        // ---------------- The warlord's son: which way he comes ----------------

        var warlord = XDocument.Load(Path.Combine(defs, "Council_WS_Warlord.xml"));
        var endings = warlord.Descendants("endings").Elements("li").Select(e => (string)e.Element("key")).Where(k => k != null).ToList();
        C(endings.Count >= 12, $"the warlord's endings are read ({endings.Count})");
        foreach (var k in endings) C(FollowUpsMath.Branch(k) != SonBranch.None, $"the warlord's ending {k} has a son who comes back");
        C(FollowUpsMath.Branch("slain") == SonBranch.Band, "his father slain by the colony: a band");
        C(new[] { "captured", "handedOver", "ransomed" }.All(k => FollowUpsMath.Branch(k) == SonBranch.Gate), "captured, handed over or ransomed: at the gate");
        C(new[] { "hunted", "bounty", "allies", "mercenaries" }.All(k => FollowUpsMath.Branch(k) == SonBranch.Exile), "hunted down by others: an exile");
        C(new[] { "brokeup", "gone", "peace", "vanished" }.All(k => FollowUpsMath.Branch(k) == SonBranch.Riders), "broken up or gone: riders looking for work");
        C(FollowUpsMath.Branch(null) == SonBranch.None && FollowUpsMath.Branch("__quiet") == SonBranch.None, "a quiet end has no son");
        C(FollowUpsMath.Now(SonBranch.Band, true, false) == SonBranch.Band, "a band needs no banner to ride under");
        C(FollowUpsMath.Now(SonBranch.Band, false, true) == SonBranch.Gate, "no camp possible: he comes to the gate instead");
        C(FollowUpsMath.Now(SonBranch.Band, false, false) == SonBranch.None, "no camp and nobody to ride with: not now");
        C(FollowUpsMath.Now(SonBranch.Exile, false, false) == SonBranch.None && FollowUpsMath.Now(SonBranch.Riders, false, true) == SonBranch.Riders,
            "an exile or riders need a people at peace with the colony to come with");

        // ---------------- When ----------------

        C(FollowUpsMath.MinDays == 60 && FollowUpsMath.MaxDays == 180, "60 to 180 days after the story it follows");
        bool inWindow = true;
        foreach (float r in new[] { -1f, 0f, 0.25f, 0.5f, 1f, 2f })
        {
            int due = FollowUpsMath.DueTick(1000 * Day, r);
            if (due < 1060 * Day || due > 1180 * Day) inWindow = false;
        }
        C(inWindow, "its day always falls in the window");
        C(!FollowUpsMath.Due(100 * Day, 99 * Day) && FollowUpsMath.Due(100 * Day, 100 * Day) && !FollowUpsMath.Due(-1, 5 * Day), "due on its day, not before");
        C(!FollowUpsMath.Lapsed(0, 240 * Day) && FollowUpsMath.Lapsed(0, 241 * Day), "let go 60 days past the window's end without a free slot");
        FollowUpsMath.SonAge(45f, out float lo, out float hi);
        C(lo == 16f && hi == 28f, "a father of 45: a son of 16 to 28");
        FollowUpsMath.SonAge(30f, out lo, out hi);
        C(lo == 16f && hi == 17f, "a young father: a son of 16 or 17");
        FollowUpsMath.SonAge(70f, out lo, out hi);
        C(hi == 30f, "never older than 30");
        FollowUpsMath.SonAge(-1f, out lo, out hi);
        C(lo == 17f && hi == 28f, "a father of unknown age: 17 to 28");

        // ---------------- At the gate ----------------

        C(FollowUpsMath.Ask(true, true, true, true) == SonAsk.Father, "his father in the colony's cells comes first");
        C(FollowUpsMath.Ask(false, true, true, true) == SonAsk.Meet, "then his father as one of the colony");
        C(FollowUpsMath.Ask(false, false, true, true) == SonAsk.Grave, "then his father's grave");
        C(FollowUpsMath.Ask(false, false, false, true) == SonAsk.Blade, "then his father's weapon");
        C(FollowUpsMath.Ask(false, false, false, false) == SonAsk.Peace, "else a peace");
        C(Enum.GetValues(typeof(SonAsk)).Cast<SonAsk>().Select(FollowUpsMath.AskFlag).Distinct().Count() == 5, "each ask its own flag");

        // ---------------- The town that grew ----------------

        var rival = XDocument.Load(Path.Combine(defs, "Rimfolk_WS_World.xml")).Descendants("defName").First(d => d.Value == "Council_WS_RivalColony").Parent;
        var rivalEnds = rival.Element("endings").Elements("li").Select(e => (string)e.Element("key")).ToList();
        C(rivalEnds.Contains("alliance") && rivalEnds.Contains("neighbours"), "the other settlers can end in friendship or as neighbours");
        C(FollowUpsMath.TownGrows("alliance") && FollowUpsMath.TownGrows("neighbours") && FollowUpsMath.TownGrows("hamlet"), "friends, neighbours and a hamlet's second year grow a town");
        C(new[] { "feud", "failed", "absorbed", "strangers" }.All(k => !FollowUpsMath.TownGrows(k)), "a feud, a failure, settlers who joined or strangers grow none");
        var rivalFlags = rival.Descendants("flags").Elements("li").Select(e => e.Value).Distinct().ToList();
        C(rivalFlags.Count >= 8 && rivalFlags.All(f => FollowUpsMath.FirstYear(f) == "first:" + f), $"their first year's flags are remembered as first:FLAG ({rivalFlags.Count})");
        C(FollowUpsMath.FirstYear("afterEnd:x") == null && FollowUpsMath.FirstYear("") == null, "no flag of a flag");
        C(FollowUpsMath.Grown(50f) == 65f && FollowUpsMath.Grown(85f) == 90f && FollowUpsMath.Grown(95f) == 95f && FollowUpsMath.Grown(-1f) == -1f,
            "grown: 15 more, 90 at most, never less than it was, nothing for a town with no fortune");

        // ---------------- Other ways to find a camp ----------------

        C(!CampWarnMath.May(1.9f) && CampWarnMath.May(2f), "a warning only once the camp has stood two days");
        C(CampWarnMath.Visitor(1f) == CampWarnMath.VisitorChance && CampWarnMath.Visitor(2f) > CampWarnMath.Visitor(1f) && CampWarnMath.Visitor(0f) > 0f,
            "the rate setting makes warnings likelier, and never takes them away");
        C(CampWarnMath.Letter(10f) <= 1f && CampWarnMath.LetterTiles > 0f, "a letter from a town near the camp, a chance at most 1");

        // ---------------- A people's memory of single colonists ----------------

        var marks = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(marks, "killedRaid", 10 * Day);
        ColonistMemoryMath.AddKind(marks, "killedRaid", 11 * Day);
        C(marks.Count == 1 && marks[0].count == 2 && marks[0].weight >= 3, "two raiders killed within two days are one mark, adding up");
        ColonistMemoryMath.AddKind(marks, "killedRaid", 20 * Day);
        C(marks.Count == 2, "another raid later is a mark of its own");
        ColonistMemoryMath.AddKind(marks, "foughtBeside", 20 * Day);
        ColonistMemoryMath.AddKind(marks, "campBroken", 21 * Day);
        ColonistMemoryMath.AddKind(marks, "executed", 22 * Day);
        C(marks.Count == ColonistMemoryMath.MaxMarks, $"at most {ColonistMemoryMath.MaxMarks} marks for a colonist");
        C(marks.Any(m => m.kind == "executed"), "the newest mark always stays");
        string packed = ColonistMemoryMath.Pack(marks);
        var back = ColonistMemoryMath.Unpack(packed);
        C(back.Count == marks.Count && back.Zip(marks, (a, b) => a.kind == b.kind && a.sign == b.sign && a.weight == b.weight && a.tick == b.tick && a.count == b.count).All(x => x),
            "the marks go into the ledger's tie and come back the same");
        C(ColonistMemoryMath.Unpack("bad;x:1;kind:0:5:1:1;ok:1:5:10:2").Count == 1 && ColonistMemoryMath.Unpack(null).Count == 0, "unreadable marks are left out");
        C(ColonistMemoryMath.Pack(new List<ColonistMemoryMath.Mark> { new ColonistMemoryMath.Mark { kind = "a:b", sign = 1, weight = 1 } }) == null, "a kind that can't be packed is left out");

        int now = 30 * Day;
        var hero = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(hero, "campBroken", now);
        float s0 = ColonistMemoryMath.Standing(hero, now);
        C(ColonistMemoryMath.Friend(s0) && !ColonistMemoryMath.Foe(s0), "who broke a camp that troubled them is named their friend");
        C(ColonistMemoryMath.Standing(hero, now + 60 * Day) < s0 && ColonistMemoryMath.Standing(hero, now + 60 * Day) > 0f, "it fades over a year, not at once");
        C(ColonistMemoryMath.Faded(hero, now + 60 * 20 * Day) && !ColonistMemoryMath.Faded(hero, now), "in time it is forgotten");

        var killer = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(killer, "killedRaid", now);
        float k1 = ColonistMemoryMath.Standing(killer, now);
        C(ColonistMemoryMath.Foe(-ColonistMemoryMath.Named) && !ColonistMemoryMath.Foe(k1) && !ColonistMemoryMath.Refuses(k1), "one raider killed at the colony's walls: not yet a foe");
        for (int i = 1; i < 6; i++) ColonistMemoryMath.AddKind(killer, "killedRaid", now + i * Day / 4);
        float k6 = ColonistMemoryMath.Standing(killer, now + 2 * Day);
        C(ColonistMemoryMath.Foe(k6) && !ColonistMemoryMath.Refuses(k6), "six of their raiders killed by one colonist in one raid: a foe, but their traders still deal");
        for (int i = 0; i < 6; i++) ColonistMemoryMath.AddKind(killer, "killedRaid", now + 10 * Day + i * Day / 4);
        C(ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(killer, now + 12 * Day)), "a second raid's worth soon after: their traders won't deal with them");
        var peace = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(peace, "killedPeace", now);
        C(ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(peace, now)), "one of theirs killed at peace: their traders won't deal");
        var exec = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(exec, "executed", now);
        C(ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(exec, now)) && ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(exec, now + 60 * Day)),
            "an execution is held against them for more than a year");
        var harvest = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(harvest, "harvested", now);
        C(ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(harvest, now)), "a prisoner cut open: the same");
        var mixed = new List<ColonistMemoryMath.Mark>();
        ColonistMemoryMath.AddKind(mixed, "campBroken", now);
        ColonistMemoryMath.AddKind(mixed, "killedRaid", now);
        C(ColonistMemoryMath.Standing(mixed, now) > 0f && ColonistMemoryMath.Sum(mixed, now, -1) > 0f && ColonistMemoryMath.Best(mixed, now, -1)?.kind == "killedRaid",
            "the good and the wrongs both remembered, the standing what is left between them");
        C(ColonistMemoryMath.Add(mixed, "x", 0, 5, now) == null && ColonistMemoryMath.Add(mixed, "x", 1, 0, now) == null && ColonistMemoryMath.AddKind(mixed, "nothing", now) == null,
            "a mark needs a side, a size and a kind it knows");
        C(ColonistMemoryMath.Regard(100f) == ColonistMemoryMath.RegardCap && ColonistMemoryMath.Regard(-100f) == -ColonistMemoryMath.RegardCap && ColonistMemoryMath.Regard(4f) == 6f,
            "a notable's regard of a colonist moves 1.5 a point, 40 at most either way");
        C(ColonistMemoryMath.Weakest(new List<float> { 5f, -1f, 12f }) == 1 && ColonistMemoryMath.Weakest(new List<float>()) == -1, "the one remembered least makes way");
        C(ColonistMemoryMath.KillKind(false, false) == "killedPeace" && ColonistMemoryMath.KillKind(true, true) == "killedRaid" && ColonistMemoryMath.KillKind(true, false) == "killedField",
            "a killing at peace, in their raid, or elsewhere in a fight");
        C(ColonistMemoryMath.Kinds.Keys.Where(k => k.StartsWith("killed")).All(k => ColonistMemoryMath.Tag(k) == "killed") && ColonistMemoryMath.Tag("released") == "freed"
          && ColonistMemoryMath.Tag("slighted") == "insulted", "the tags their lines ask for");
        C(ColonistMemoryMath.Kinds.Values.All(k => k.weight > 0 && k.weight <= k.max && k.max <= MemoryMath.MaxWeight && (k.sign == 1 || k.sign == -1)), "every kind has a side and a size within bounds");
        C(ColonistMemoryMath.MaxPerPeople * 8 >= ColonistMemoryMath.MaxAll && ColonistMemoryMath.MaxAll <= TieRules.MaxRegard / 4,
            "the colonists remembered by name take a quarter of the ledger's room for regard at most");

        // ---------------- The mod's side ----------------

        string keyed = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        string mine = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_FollowUps.xml"))
                      + File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_ColonistMemory.xml"));
        foreach (var k in ColonistMemoryMath.Kinds.Keys) C(keyed.Contains("<CC_ColMem_Why_" + k + ">"), $"what a colonist is remembered for has words: {k}");
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
            C(keyed.Contains("<CC_Grown_Name_Colony_" + age + ">") && keyed.Contains("<CC_Grown_Name_Friend_" + age + ">"), $"a grown town's names in the {age} age");
        C(keyed.Contains("<CC_RaidCamp_Why_son>"), "a camp the warlord's son leads says so when found");
        string sayVisitor = Regex.Match(mine, "<CC_RaidCamp_Say_Visitor>([^<]*)<").Groups[1].Value.Replace("{KIND}", "brigands' hold").Replace("{DIR}", "northeast");
        C(sayVisitor.Length > 0 && sayVisitor.Length <= 90 && sayVisitor.TrimEnd('.').IndexOfAny(new[] { '.', '!', '?' }) < 0, $"the visitor's warning is one short sentence ('{sayVisitor}')");
        C(!mine.Contains("—") && !Regex.IsMatch(mine, ">[^<]*![^<]*<"), "no em dashes and no exclamation marks in its words");
        string code = string.Concat(new[] { "World/Region/FollowUps.cs", "World/Region/WarlordSon.cs", "World/Region/GrownTown.cs", "World/Region/RaidersCampWarnings.cs", "World/ColonistMemory.cs" }
            .Select(f => File.ReadAllText(Path.Combine(src, f))));
        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 8, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyed.Contains("<" + k + ">"), $"{k} exists");
        C(!code.Contains("Messages.Message(") && !code.Contains("ReceiveLetter("), "it tells the player through Tell and the world events' own letters");
        C(!code.Contains("GameComponentTick") && !code.Contains("override void Tick"), "nothing per tick");

        string sons = File.ReadAllText(Path.Combine(defs, "Rimfolk_WS_WarlordSon.xml"));
        foreach (SonBranch b in Enum.GetValues(typeof(SonBranch)))
        {
            if (b == SonBranch.None) continue;
            C(sons.Contains("<defName>Rimfolk_WS_Son" + b + "</defName>") && sons.Contains("<workerClass>ColonyCouncil.SituationWorker_Son" + b + "</workerClass>"), $"the son's {b} has its world event");
        }
        C(Regex.Matches(sons, "<kind>son</kind>").Count == 4, "two of his stories never run at once");
        string grown = File.ReadAllText(Path.Combine(defs, "Rimfolk_WS_GrownTown.xml"));
        C(grown.Contains("ColonyCouncil.SituationWorker_GrownTown") && grown.Contains("<key>name</key>") && grown.Contains("<key>market</key>"), "the grown town asks for a name and to its first market");
        C(grown.Contains("namedColony") && grown.Contains("namedFriend") && File.ReadAllText(Path.Combine(src, "World/Region/GrownTown.cs")).Contains("\"namedColony\""),
            "the names the answers give are the ones the code reads");
        // Tags like [!named] aside, no line has an exclamation mark.
        foreach (var t in new[] { sons, grown }.Select(x => Regex.Replace(x, @"\[[^\]]*\]", "")))
            C(!t.Contains("—") && !Regex.IsMatch(t, "<(li|label|outcome)>[^<]*![^<]*</(li|label|outcome)>"), "no em dashes and no exclamation marks in the follow-ups' lines");

        C(File.ReadAllText(Path.Combine(src, "World", "WarlordWorker.cs")).Contains("WarlordSons.Remember(s, ending);"), "the warlord's end notes his son");
        C(File.ReadAllText(Path.Combine(src, "World", "WarlordWorker.cs")).Contains("WarlordSons.Watch(s, x);"), "the warlord's weapon is noted while he is at a home");
        C(File.ReadAllText(Path.Combine(src, "World", "Workers_World.cs")).Contains("GrownTowns.Remember(s, ending);"), "the other settlers' end notes the town that will grow");
        string book = File.ReadAllText(Path.Combine(src, "World", "Region", "RegionBook.cs"));
        C(book.Contains("public FollowUpsHome followUps;") && book.Contains("Scribe_Deep.Look(ref followUps, \"followUps\")") && book.Contains("FollowUps.Daily(this, now)"),
            "each home's follow-ups are kept and run in the region's day (no pass of their own)");
        string keep = File.ReadAllText(Path.Combine(src, "World", "Ledger", "LedgerKeep.cs"));
        C(keep.Contains("case RaidersCamps.KeepReason:") && keep.Contains("case FollowUps.KeepReason:"), "the ledger's daily check knows a camp's leader and a follow-up's person");
        C(File.ReadAllText(Path.Combine(src, "World", "Region", "RaidersCamp.cs")).Contains("LeaderOnMap(c, s.Map)"), "the camp's leader is on its map when the colony attacks it");
        C(File.ReadAllText(Path.Combine(src, "World", "KinMemory.cs")).Contains("ColonistMemory.FromDeed(d, colonist)"), "a deed naming a colonist is remembered of them too: one way to remember a colonist");
        C(File.ReadAllText(Path.Combine(src, "Text", "Facts", "FactsColony.cs")).Contains("ColonistMemory.AddFacts(f, c, p)"), "talk knows the foe ({FOE})");
        C(File.ReadAllText(Path.Combine(src, "World", "Notables.cs")).Contains("ColonistMemory.Regard(n.faction, p"), "a notable's regard of a colonist reads it");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"World/Region/FollowUps.cs:waiting\"") && save.Contains("\"World/Region/RaidersCamp.cs:fought\""), "the save model knows the new lists");
        return n;
    }
}
