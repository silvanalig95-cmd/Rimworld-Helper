using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Other mods' world mechanics behind adapters (Integrations/World/) and the ages (Lean/Ages.cs, Lean/AgesMath.cs): the
/// colony's age read exactly as Flavor's era tags always read it, a people's as the ledger's view did, every feature's
/// word and rite in every age, the rule for mixed ages; the adapters' small rules (what the history keeps, which home a
/// holding is under, a settlement's progress, a battle decided after it was written); and that the core names no mod and
/// nothing in the adapters tells the player anything.
/// </summary>
public static class WorldMechanicsTests
{
    private const int Day = HistoryMath.DayTicks;

    /// <summary>Flavor.AddEraTags before it asked Ages: the tag for a tech level (the game's TechLevel as a number).</summary>
    private static string OldEraTag(int tech)
    {
        const int neolithic = 2, medieval = 3, industrial = 4, spacer = 5;
        if (tech <= neolithic) return "tribal";
        if (tech == medieval) return "medieval";
        if (tech == industrial) return "industrial";
        if (tech >= spacer) return "spacer";
        return null;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "world mechanics: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Ages: the same tags and choices as before ----------------

        for (int tech = 0; tech <= 9; tech++)
            C(AgesMath.Tag(LedgerMath.AgeOf(tech)) == OldEraTag(tech), $"tech level {tech} gives the era tag it always gave ({OldEraTag(tech)})");
        C(AgesMath.All.Select(AgesMath.Tag).SequenceEqual(new[] { "tribal", "medieval", "industrial", "spacer" }), "the four ages' tags");
        C(AgesMath.All.All(a => LineTagsKnow(AgesMath.Tag(a))), "every age's tag is one the lines are tagged with");

        string flavor = Code("Flavor.cs");
        var era = Regex.Match(flavor, @"public static void AddEraTags\(HashSet<string> tags\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(era.Contains("tags.Add(Ages.Tag(Ages.Ours));") && era.Contains("if (Avail.IsPreIndustrial) tags.Add(\"lowtech\");") && !era.Contains("TechLevel."),
            "Flavor's era tags ask Ages for the colony's age, lowtech as before");
        string ages = Code("Lean/Ages.cs");
        C(ages.Contains("public static WorldAge Ours => LedgerMath.AgeOf((int)Avail.EffectiveTech);"), "the colony's age is read from the tech Flavor always read");
        C(ages.Contains("return LedgerMath.AgeOf((int)(people.def?.techLevel ?? TechLevel.Industrial));")
          && ages.Contains("public static bool IsEmpire(Faction people) => people?.def != null && people.def.HasRoyalTitles;"),
            "a people's age and the Empire read as the ledger's people view read them");
        string views = Code("World/Ledger/LedgerViews.cs");
        C(views.Contains("age = Ages.Of(f), empire = Ages.IsEmpire(f)") && !views.Contains("LedgerMath.AgeOf("), "the ledger's people view asks Ages");

        // ---------------- Forms: every feature, every age ----------------

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ages.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var expected = new HashSet<string>();
        foreach (var f in AgesMath.Features)
        {
            var words = new List<string>();
            var rites = new List<string>();
            foreach (var age in AgesMath.All)
            {
                string wk = AgesMath.WordKey(f, age), rk = AgesMath.RiteKey(f, age);
                expected.Add(wk);
                expected.Add(rk);
                C(keyed.TryGetValue(wk, out var w) && !string.IsNullOrWhiteSpace(w), $"{f} has a word in the {age} age ({wk})");
                C(keyed.TryGetValue(rk, out var r) && !string.IsNullOrWhiteSpace(r), $"{f} has a rite in the {age} age ({rk})");
                words.Add(w ?? "");
                rites.Add(r ?? "");
                foreach (var s in new[] { w, r }.Where(s => s != null))
                {
                    C(s.Trim() == s && char.IsLower(s[0]) && !s.EndsWith(".") && !s.Contains("!") && !s.Contains("\u2014") && !s.Contains("\u2013"),
                        $"\"{s}\" is a short noun phrase to sit in a sentence (lowercase, no stop, no dash)");
                    C(s.Split(' ').Length <= 8, $"\"{s}\" is short");
                    C(!Regex.IsMatch(s, @"\b(honour|favour|colour|armour|rumour|labour|behaviour|harbour|neighbour|valour|vigour|parlour|splendour)", RegexOptions.IgnoreCase),
                        $"\"{s}\" is spelled the American way");
                }
            }
            C(words.Distinct().Count() == 4 && rites.Distinct().Count() == 4, $"{f}'s four ages each have a word and a rite of their own");
        }
        C(AgesMath.Features.Length == 20 && AgesMath.Features.Distinct().Count() == 20 && Enum.GetValues(typeof(AgeFeature)).Length == 20,
            "nine features, as ROADMAP's table has (council tasks dropped, 5 November 2026), the region's founding, trading post, hamlet and oath, stage 2's truce, hostage, envoy and levy, and stage 3's seat, founding party and call for newcomers");
        var extra = keyed.Keys.Where(k => k.StartsWith("CC_Form_") && !expected.Contains(k)).ToList();
        C(extra.Count == 0, "no form key outside the table: " + string.Join(", ", extra));
        C(AgesMath.WordKey(AgeFeature.Favor, WorldAge.Tribal) == "CC_Form_Favor_Tribal" && AgesMath.RiteKey(AgeFeature.Fealty, WorldAge.Spacer) == "CC_Form_Fealty_Spacer_Rite",
            "the keys' names");
        foreach (var ours in AgesMath.All)
        foreach (var theirs in AgesMath.All)
            C(AgesMath.WordAge(ours, theirs) == ours && AgesMath.RiteAge(ours, theirs) == theirs, $"{ours} with {theirs}: the word is the colony's age, the rite theirs");
        C(ages.Contains("AgesMath.WordKey(feature, AgesMath.WordAge(ours, theirs))") && ages.Contains("AgesMath.RiteKey(feature, AgesMath.RiteAge(ours, theirs))"),
            "Form takes the word and the rite by the rule");

        // ---------------- The adapters' small rules ----------------

        C(WorldMath.Kinds.Length == 6 && WorldMath.Kinds.Distinct().Count() == 6 && WorldMath.Kinds.All(k => HistoryKinds.Family(k) == "world"),
            "the adapters' kinds are of the family world");
        C(!WorldMath.Kinds.Contains(HistoryKinds.WorldBegan) && !WorldMath.Kinds.Contains(HistoryKinds.WorldEnded), "and none is a world situation's");
        C(WorldMath.Records(true, false) && WorldMath.Records(false, true) && WorldMath.Records(true, true) && !WorldMath.Records(false, false),
            "the history keeps what is ours anywhere, the rest within reach");
        C(HistoryMath.IsWeighty(WorldMath.Weight(true)) && !HistoryMath.IsWeighty(WorldMath.Weight(false)), "what is ours is a milestone, the region's comings and goings everyday");
        C(WorldMath.InReach(new List<float> { 80f, 30f }, 35f) && WorldMath.InReach(new List<float> { 35f }, 35f) && !WorldMath.InReach(new List<float> { 36f, float.MaxValue }, 35f)
          && !WorldMath.InReach(new List<float>(), 35f) && !WorldMath.InReach(null, 35f), "within the region map's reach of a home");

        var homes = new List<int> { 100, 200, 300 };
        C(WorldMath.HomeFor(200, homes, new List<float> { 1f, 50f, 2f }) == 1, "a holding under the home it names");
        C(WorldMath.HomeFor(999, homes, new List<float> { 9f, 4f, 4f }) == 1 && WorldMath.HomeFor(HomesMath.None, homes, new List<float> { 9f, 8f, 3f }) == 2,
            "else the nearest (the first of equals)");
        C(WorldMath.HomeFor(HomesMath.None, homes, new List<float> { float.MaxValue, float.MaxValue, float.MaxValue }) == 0, "all out of reach: the first");
        C(WorldMath.HomeFor(100, new List<int>(), new List<float>()) == -1 && WorldMath.HomeFor(100, null, null) == -1, "no home, no answer");

        C(WorldMath.Progress(1000, 2000, 500) == 0f && WorldMath.Progress(1000, 2000, 1500) == 0.5f && WorldMath.Progress(1000, 2000, 2500) == 1f
          && WorldMath.Progress(1000, 2000, 1000) == 0f && WorldMath.Progress(1000, 2000, 2000) == 1f, "a settlement's progress by the clock");
        C(WorldMath.Progress(1000, 1000, 999) == 0f && WorldMath.Progress(1000, 1000, 1000) == 1f && WorldMath.Progress(1000, 900, 950) == 1f, "a site with no time to build");

        // A battle written when it begins, decided when the mod's trigger tells.
        var book = new HistoryBook();
        Ref a = Ref.People(3), b = Ref.People(4), c = Ref.People(5), home = Ref.Colony(77), elsewhere = Ref.Colony(78);
        HistoryEntry Battle(int tick, Ref x, Ref y, HistoryRole rx = HistoryRole.With, HistoryRole ry = HistoryRole.With, Ref place = default)
        {
            var e = new HistoryEntry { tick = tick, kind = WorldMath.Battle, place = place.IsNone ? home : place, weight = WorldMath.Weight(true) };
            e.actors.Add(new HistoryActor(x, rx));
            e.actors.Add(new HistoryActor(y, ry));
            return book.Add(e);
        }
        var old = Battle(1 * Day, a, b);
        var other = Battle(9 * Day, a, c);
        var fight = Battle(10 * Day, b, a);
        var away = Battle(10 * Day + 100, a, b, place: elsewhere);
        List<HistoryEntry> Here(int now) => book.At(home, new[] { WorldMath.Battle }, now - WorldMath.BattleDays * Day);
        int now1 = 11 * Day;
        C(WorldMath.OpenBattle(Here(now1), a, b, now1) == fight && WorldMath.OpenBattle(Here(now1), b, a, now1) == fight, "the open battle between the two, either way round");
        C(WorldMath.OpenBattle(Here(now1), a, Ref.People(9), now1) == null && WorldMath.OpenBattle(Here(20 * Day), a, b, 20 * Day) == null,
            "none with someone else, none begun too long ago");
        C(!Here(now1).Contains(away), "a battle at another home is another place");
        int v = book.Version;
        C(WorldMath.Decide(book, fight, a, b) && fight.RoleOf(a) == HistoryRole.Did && fight.RoleOf(b) == HistoryRole.Against && book.Version > v, "decided: the winner did, the loser was against");
        C(fight.actors[0].who == b && fight.actors[1].who == a, "each keeps their place in it");
        C(book.Of(a).Contains(fight) && book.Of(b).Contains(fight) && book.At(home).Contains(fight) && book.OfKind("world").Contains(fight), "the indexes still find it");
        C(WorldMath.OpenBattle(Here(now1), a, b, now1) == null && WorldMath.DecidedBattle(Here(now1), b, a, now1) == fight, "decided, it is no longer open");
        C(old.RoleOf(a) == HistoryRole.With && other.RoleOf(a) == HistoryRole.With && away.RoleOf(a) == HistoryRole.With, "the other battles stay as they were");
        C(!WorldMath.Decide(book, other, a, b) && !WorldMath.Decide(book, other, a, a) && !WorldMath.Decide(null, other, a, c) && !WorldMath.Decide(book, null, a, c),
            "only those in it, two of them, can decide it");
        v = book.Version;
        C(!book.SetRole(fight, a, HistoryRole.Did) && book.Version == v, "the same part again changes nothing");
        C(!book.SetRole(fight, c, HistoryRole.Did) && !book.SetRole(fight, Ref.None, HistoryRole.Did) && !book.SetRole(null, a, HistoryRole.Did), "only someone in it gets a part");
        C(!new HistoryBook().SetRole(fight, a, HistoryRole.With), "only an event of this book");

        // ---------------- The core names no mod; nothing in the adapters tells the player ----------------

        string dir = Path.Combine(src, "Integrations", "World");
        var files = Directory.GetFiles(dir, "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        string core = files["WorldMechanics.cs"] + files["WorldMath.cs"] + ages + Code("Lean/AgesMath.cs");
        var adapters = files.Where(kv => kv.Key != "WorldMechanics.cs" && kv.Key != "WorldMath.cs").ToList();
        var packages = adapters.SelectMany(kv => Regex.Matches(kv.Value, "PackageId = \"([^\"]+)\"").Select(m => m.Groups[1].Value)).ToList();
        C(packages.Count == 4, "four adapters know a mod by its package id: " + string.Join(", ", packages));
        foreach (var p in packages)
            C(core.IndexOf(p, StringComparison.OrdinalIgnoreCase) < 0, $"the core doesn't name {p}");
        foreach (var name in new[] { "Faction Territories", "Factional War", "Vanilla Outposts", "Diplomatic Marriage", "Royalty", "FactionTerritories", "SR.Mod", "VOE." })
            C(core.IndexOf(name, StringComparison.Ordinal) < 0, $"the core doesn't name {name}");
        C(files["WorldMechanics.cs"].Contains("typeof(WorldAdapter).IsAssignableFrom(t)"), "the adapters are found by their type");
        foreach (var kv in adapters)
        {
            C(Regex.IsMatch(kv.Value, @"class \w+ : WorldAdapter"), kv.Key + " is an adapter");
            C(!Regex.IsMatch(kv.Value, @"\bTell\.|Messages\.Message|LetterStack|News\.Post|Notify\.|Speech\."), kv.Key + " tells the player nothing");
            foreach (Match m in Regex.Matches(kv.Value, @"\[HarmonyPatch\]\s*public static class (\w+)\s*\{(.*?)\n    \}", RegexOptions.Singleline))
            {
                string body = m.Groups[2].Value;
                C(body.Contains("public static bool Prepare() => TargetMethod() != null;"), m.Groups[1].Value + " stands down without its mod");
                C(body.Contains("RimfolkPatches.On") && body.Contains("CouncilLog.ErrorOnce"), m.Groups[1].Value + " follows the deep-hooks setting and never throws into the mod");
            }
        }
        C(!Regex.IsMatch(files["WorldMechanics.cs"], @"\bTell\.|Messages\.Message|LetterStack|News\.Post|Notify\."), "the facade tells the player nothing");
        C(files["MarriageAllianceAdapter.cs"].IndexOf("\"IsValid\"", StringComparison.Ordinal) < 0, "the marriage alliance is only read (its IsValid changes it)");

        return n;
    }

    private static bool LineTagsKnow(string tag) => LineTags.EraNames.Contains(tag);
}
