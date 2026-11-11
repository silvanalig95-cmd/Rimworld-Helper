using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A correspondence that builds up (the player, 10 November 2026: "continued letter exchange should build up, the contents
/// and familiarity between the colony and others should develop and be reflected in the messages"):
/// Source/ColonyCouncil/Letters/LetterThreadMath.cs and the pools Defs/FlavorPoolDefs/Rimfolk_Letters_Familiar.xml and
/// _Thread.xml. The stages by letters, time, regard, goodwill, good turns and wrongs; cooling; names and nicknames; the
/// shapes and the form by the stage; the threads raised from what a letter said, taken up by the right side at the right
/// time, followed by what is true now, kept in bounds; the running jokes; the pools' hygiene and coverage; whole
/// correspondences played out from the real pools (printed with RIMFOLK_LETTERS_PRINT=1); the seams.
/// </summary>
public static class LetterThreadTests
{
    private static Dictionary<string, List<string>> pools;

    private const int Day = 60000;

    public static int Run(string root, Action<bool, string> check, bool print = false)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "correspondence: " + what);
        }

        pools = Load(root);
        Stages(C);
        Names(C);
        Shapes(C);
        Moves(C);
        Outcomes(C);
        Choosing(C);
        Raising(C);
        Pools(C);
        Correspondences(C, print);
        Seams(root, C);
        return n;
    }

    // ================= Pools and specs =================

    private static Dictionary<string, List<string>> Load(string root)
    {
        var map = new Dictionary<string, List<string>>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "Rimfolk_Letters_*.xml"))
        {
            var doc = XDocument.Load(f);
            foreach (var d in doc.Root!.Elements())
            {
                string name = d.Element("defName")?.Value;
                if (name == null) continue;
                if (!map.TryGetValue(name, out var list)) map[name] = list = new List<string>();
                foreach (var li in d.Element("lines")?.Element("plain")?.Elements("li") ?? Enumerable.Empty<XElement>()) list.Add(li.Value);
            }
        }
        return map;
    }

    private static List<string> Lines(LetterPart p) => pools.TryGetValue(LetterFormMath.PoolOf(p), out var l) ? l : new List<string>();

    private static LineTags.Parsed P(string line) => LineTags.Of(line);

    private static bool Has(LineTags.Parsed p, string tag) => p.tags != null && p.tags.Any(t => t.Split('|').Contains(tag));

    private static Dictionary<string, string> Base(bool ours) => new Dictionary<string, string>
    {
        ["FROM"] = ours ? "Stace" : "Dagna", ["TO"] = ours ? "Dagna" : "Stace", ["TOFULL"] = ours ? "Dagna Holt" : "Stacey Morrow", ["TOWN"] = "Ashford", ["COLONY"] = "Riverhold",
        ["PEOPLE"] = "the Rust Kin", ["TITLE"] = "headwoman", ["PURPOSE"] = "greetings", ["COOK"] = "Bautista"
    };

    private static LetterSpec Spec(bool ours, Familiarity fam, AnswerTone tone = AnswerTone.Warm, string speech = null, WorldAge age = WorldAge.Industrial, string purpose = "greetings",
        LetterMotive motive = LetterMotive.Friend)
    {
        var s = new LetterSpec { ours = ours, fam = fam, tone = tone, age = age, purpose = purpose, motive = motive, shape = LetterShape.Written, who = new LetterWho() };
        if (speech == LetterFormMath.Olde) s.age = WorldAge.Medieval;
        else if (speech == LetterFormMath.NoSchool) s.schooling = Schooling.None;
        else if (speech == LetterFormMath.SomeSchool) s.schooling = Schooling.Some;
        else if (speech != null) s.strange = speech;
        if (speech != LetterFormMath.Olde && s.age == WorldAge.Medieval) s.age = WorldAge.Industrial;
        foreach (var kv in Base(ours)) s.values[kv.Key] = kv.Value;
        return s;
    }

    /// <summary>The pools as the game draws on them, each drawn line's single tags kept in the spec (what it said).</summary>
    private static Func<LetterPart, HashSet<string>, string> Pick(LetterSpec s, Random r, List<(LetterPart part, string line)> drawn = null) => (part, tags) =>
    {
        var lines = Lines(part);
        string text = LetterFormMath.Pick(lines, tags, s.values, null, () => (float)r.NextDouble());
        if (text == null) return null;
        foreach (var l in lines)
        {
            var p = P(l);
            if (p.text != text || LetterFormMath.Fits(l, tags) < 0) continue;
            foreach (var t in p.tags.Where(t => !t.Contains('|'))) s.drawn.Add(t);
            drawn?.Add((part, l));
            break;
        }
        return Texts.Fill(text, s.values);
    };

    private static LetterLines Shaped(LetterSpec s, Random r)
    {
        var pick = Pick(s, r);
        return new LetterLines { pick = pick, has = (part, tags) => LetterFormMath.Any(Lines(part), tags, s.values) };
    }

    // ================= The stages =================

    private static LetterStanding St(int letters, int mine, float days, int opinion, int peak = -999, int goodwill = 20, float silence = 2f) => new LetterStanding
    {
        letters = letters, mine = mine, days = days, opinion = opinion, peak = peak == -999 ? opinion : peak, goodwill = goodwill, silence = silence, wrongDays = -1f
    };

    private static void Stages(Action<bool, string> C)
    {
        C(LetterThreadMath.Tag(Familiarity.None) == null, "no stage, no tag");
        foreach (var f in LetterThreadMath.Stages) C(LetterThreadMath.Tag(f) == "fam:" + f.ToString().ToLowerInvariant(), "the stage's tag " + f);
        // The writer's first letter in it is new, whatever else holds; a war, a wrong or a fall cools only what had begun.
        foreach (var war in new[] { false, true })
            foreach (var letters in new[] { 0, 1, 5, 20 })
            {
                var s = St(letters, 0, 50f, 80);
                s.war = war;
                C(LetterThreadMath.StageOf(s) == Familiarity.New, $"a writer's first letter is new ({letters} before, war {war})");
            }
        var known = St(1, 1, 3f, 20);
        C(LetterThreadMath.StageOf(known) == Familiarity.Known, "a letter or two: known");
        C(LetterThreadMath.StageOf(St(4, 2, 8f, 0)) == Familiarity.Familiar, "four letters over eight days at a regard of 0: familiar");
        C(LetterThreadMath.StageOf(St(4, 2, 7.9f, 0)) == Familiarity.Known, "not before eight days");
        C(LetterThreadMath.StageOf(St(4, 2, 8f, -1)) == Familiarity.Known, "a wary correspondent stays formal");
        C(LetterThreadMath.StageOf(St(3, 2, 30f, 50)) == Familiarity.Known, "three letters are not yet a habit");
        C(LetterThreadMath.StageOf(St(8, 4, 30f, 30)) == Familiarity.Close, "eight letters over thirty days at a regard of 30: old friends");
        C(LetterThreadMath.StageOf(St(8, 4, 30f, 29)) == Familiarity.Familiar, "close only at a regard of 30");
        C(LetterThreadMath.StageOf(St(8, 4, 29f, 60)) == Familiarity.Familiar, "close only after thirty days");
        C(LetterThreadMath.StageOf(St(8, 4, 30f, 60, goodwill: -1)) == Familiarity.Familiar, "close only while their people's goodwill is not below 0");
        var friend = St(6, 3, 30f, 40);
        C(LetterThreadMath.StageOf(friend) == Familiarity.Familiar, "six letters: familiar");
        friend.penFriend = true;
        C(LetterThreadMath.StageOf(friend) == Familiarity.Close, "a pen friend counts two more");
        var deeds = St(6, 3, 30f, 40);
        deeds.deeds = 20;
        C(LetterThreadMath.StageOf(deeds) == Familiarity.Close, "good turns remembered count too");
        deeds.deeds = 10;
        C(LetterThreadMath.StageOf(deeds) == Familiarity.Familiar, "a smaller good turn counts one");
        // A long silence: a step back, never below known, never cooled.
        C(LetterThreadMath.StageOf(St(10, 5, 60f, 60, silence: 40f)) == Familiarity.Familiar, "old friends after a long silence: familiar again");
        C(LetterThreadMath.StageOf(St(5, 3, 20f, 10, silence: 45f)) == Familiarity.Known, "the familiar after a long silence: known");
        C(LetterThreadMath.StageOf(St(1, 1, 50f, 10, silence: 50f)) == Familiarity.Known, "never below known");
        // Cooling.
        var war2 = St(10, 5, 60f, 60);
        war2.war = true;
        C(LetterThreadMath.StageOf(war2) == Familiarity.Cooled, "a war cools old friends");
        foreach (var (days, weight, cooled) in new[] { (5f, 8, true), (30f, 8, true), (31f, 8, false), (5f, 7, false), (60f, 20, true), (90f, 20, true), (91f, 25, false), (0f, 6, false) })
        {
            var w = St(10, 5, 60f, 60);
            w.wrongDays = days;
            w.wrongWeight = weight;
            C((LetterThreadMath.StageOf(w) == Familiarity.Cooled) == cooled, $"a wrong of {weight} {days} days ago cools: {cooled}");
        }
        C(LetterThreadMath.StageOf(St(10, 5, 60f, 15, peak: 50)) == Familiarity.Cooled, "regard fallen 35 to 15: cooled");
        C(LetterThreadMath.StageOf(St(10, 5, 60f, 25, peak: 60)) != Familiarity.Cooled, "fallen 35 but still at 25: not cooled");
        C(LetterThreadMath.StageOf(St(10, 5, 60f, 10, peak: 35)) != Familiarity.Cooled, "fallen 25: not cooled");
        // Over a grid: more letters never make it colder; cooled exactly when Cooled says.
        int looked = 0;
        foreach (var letters in Enumerable.Range(1, 14))
            foreach (var days in new[] { 0f, 5f, 10f, 29f, 31f, 80f })
                foreach (var op in new[] { -40, -1, 0, 15, 29, 30, 70 })
                    foreach (var gw in new[] { -10, 0, 30 })
                    {
                        var a = St(letters, 1, days, op, goodwill: gw);
                        var b = St(letters + 1, 1, days, op, goodwill: gw);
                        var sa = LetterThreadMath.StageOf(a);
                        var sb = LetterThreadMath.StageOf(b);
                        looked++;
                        C(sb >= sa && sa != Familiarity.New && sa != Familiarity.Cooled, $"more letters never colder ({letters} {days} {op} {gw}: {sa} then {sb})");
                    }
        C(looked > 1000, "standings looked at: " + looked);
        // The wrongs a cooled letter can name, against FactionMemory's own kinds.
        C(LetterThreadMath.SlightOf("wordBroken", false) == "word" && LetterThreadMath.SlightOf("townAttacked", false) == "harm" && LetterThreadMath.SlightOf("slighted", false) == "insult"
          && LetterThreadMath.SlightOf("raided", true) == "theirs" && LetterThreadMath.SlightOf("gift", false) == null && LetterThreadMath.SlightOf(null, false) == null,
            "a wrong as its cold word names it");
    }

    // ================= Names =================

    private static void Names(Action<bool, string> C)
    {
        foreach (var (name, nick) in new[] { ("Dagna", "Dag"), ("Hessa", "Hess"), ("Wenna", "Wen"), ("Marta", "Mar"), ("Margaret", "Mar"), ("Stace", null), ("Bautista", null),
                     ("Acraeraes", null), ("Okoa", null), ("Kesh", null), ("Aldous", null), ("Jean-Luc", null), ("", null), (null, null), ("Unit Ceres-4", null), ("Brrr", null) })
            C(LetterThreadMath.Nickname(name) == nick, $"a short name for {name ?? "nobody"}: {LetterThreadMath.Nickname(name) ?? "none"}");
        var r = new Random(4);
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        for (int i = 0; i < 3000; i++)
        {
            int len = 3 + r.Next(8);
            string name = char.ToUpperInvariant(letters[r.Next(26)]) + new string(Enumerable.Range(0, len - 1).Select(_ => letters[r.Next(26)]).ToArray());
            string nick = LetterThreadMath.Nickname(name);
            if (nick == null) continue;
            C(nick.Length >= 3 && nick.Length <= 5 && name.StartsWith(nick, StringComparison.OrdinalIgnoreCase) && char.IsUpper(nick[0]) && nick.Length < name.Length,
                $"a short name is the start of the name: {name} {nick}");
        }
        C(LetterThreadMath.FullName("Stacey", "Morrow", "Stace") == "Stacey Morrow" && LetterThreadMath.FullName("Stacey", "", "Stace") == "Stacey"
          && LetterThreadMath.FullName(null, null, "Stace") == "Stace", "a full name for formal terms");
    }

    // ================= The shapes and the form by the stage =================

    private static (float note, float longShare) Shares(LetterSpec s)
    {
        float total = LetterKindMath.Kinds.Sum(k => LetterKindMath.Weight(k, s, LetterLength.Mixed));
        float note = LetterKindMath.Weight(LetterKind.Note, s, LetterLength.Mixed) / total;
        float lng = LetterKindMath.Kinds.Where(LetterKindMath.IsLong).Sum(k => LetterKindMath.Weight(k, s, LetterLength.Mixed)) / total;
        return (note, lng);
    }

    private static LetterSpec Talker(Familiarity fam)
    {
        var s = Spec(false, fam);
        s.penFriend = true;
        s.humour = 60;
        s.who.talkative = s.who.lonely = s.who.artistic = s.who.zealous = true;
        s.who.hands.Add("Pell");
        return s;
    }

    private static void Shapes(Action<bool, string> C)
    {
        var (noteNew, longNew) = Shares(Talker(Familiarity.New));
        var (noteKnown, longKnown) = Shares(Talker(Familiarity.Known));
        var (noteClose, longClose) = Shares(Talker(Familiarity.Close));
        C(longNew < longKnown && longKnown < longClose, $"long letters grow with the friendship ({longNew:F2}, {longKnown:F2}, {longClose:F2})");
        C(noteClose < noteKnown, $"a bare note between old friends is rarer ({noteKnown:F3}, {noteClose:F3})");
        var cooled = Talker(Familiarity.Cooled);
        foreach (var k in LetterKindMath.Kinds)
            C(LetterKindMath.Fits(k, cooled) == (k == LetterKind.Usual || k == LetterKind.Note), "a cooled correspondence writes only the usual letter or a note: " + k);
        var boast = Spec(false, Familiarity.Cooled, AnswerTone.Curt, LetterFormMath.NoSchool);
        boast.war = true;
        boast.who.boast = true;
        C(LetterKindMath.Fits(LetterKind.Raid, boast), "a raider's boast still comes");
        var cool = Spec(false, Familiarity.Known, AnswerTone.Cool);
        var coolCooled = Spec(false, Familiarity.Cooled, AnswerTone.Cool);
        C(LetterKindMath.Weight(LetterKind.Note, coolCooled, LetterLength.Mixed) > 2f * LetterKindMath.Weight(LetterKind.Note, cool, LetterLength.Mixed), "a cooled one writes a bare note far more often");
        // The form: no joke in a cooled letter, fewer asides and postscripts; old friends a little more.
        var warm = Spec(false, Familiarity.Close);
        var known2 = Spec(false, Familiarity.Known);
        var cold = Spec(false, Familiarity.Cooled);
        int jokesCold = Enumerable.Range(0, 1000).Count(i => LetterFormMath.Aside(cold, i / 1000f) == AsideKind.Joke);
        int asidesCold = Enumerable.Range(0, 1000).Count(i => LetterFormMath.Aside(cold, i / 1000f) != AsideKind.None);
        int asidesKnown = Enumerable.Range(0, 1000).Count(i => LetterFormMath.Aside(known2, i / 1000f) != AsideKind.None);
        int asidesClose = Enumerable.Range(0, 1000).Count(i => LetterFormMath.Aside(warm, i / 1000f) != AsideKind.None);
        C(jokesCold == 0 && asidesCold < asidesKnown && asidesKnown <= asidesClose, $"asides: cooled {asidesCold} (no jokes), known {asidesKnown}, close {asidesClose}");
        C(Enumerable.Range(0, 100).All(i => LetterFormMath.PSKind(cold, i / 100f) != AsideKind.Joke), "no joking P.S. in a cooled letter");
        C(LetterFormMath.PSChance(cold) < LetterFormMath.PSChance(known2) && LetterFormMath.PSChance(known2) < LetterFormMath.PSChance(warm), "postscripts: fewer cooled, more between old friends");
        // The stage's own words come first now and then, more for old friends and a cooled correspondence.
        foreach (var part in new[] { LetterThreadMath.SaluteTag, LetterThreadMath.OpenTag, LetterThreadMath.CloseTag })
        {
            C(LetterThreadMath.FamiliarChance(Familiarity.None, part) == 0f, "no stage, no words of its own: " + part);
            C(LetterThreadMath.FamiliarChance(Familiarity.Close, part) > LetterThreadMath.FamiliarChance(Familiarity.Known, part)
              && LetterThreadMath.FamiliarChance(Familiarity.Cooled, part) > LetterThreadMath.FamiliarChance(Familiarity.Known, part), "old friends and a cooled one sound like it: " + part);
        }
        // A short signature: old friends in a warm letter, never a council's nor the Empire's.
        C(LetterThreadMath.SignsShort(Spec(false, Familiarity.Close), 0.1f) && !LetterThreadMath.SignsShort(Spec(false, Familiarity.Known), 0.1f)
          && !LetterThreadMath.SignsShort(Spec(false, Familiarity.Close, AnswerTone.Cool, motive: LetterMotive.Plain), 0.1f)
          && !LetterThreadMath.SignsShort(Spec(false, Familiarity.Cooled), 0.1f), "old friends sign with the name alone, in a warm letter");
        var emp = Spec(false, Familiarity.Close);
        emp.empire = true;
        C(!LetterThreadMath.SignsShort(emp, 0.1f), "the Empire keeps its titles");
        // Without a stage nothing changes: a letter of an older save, a call.
        var none = Spec(false, Familiarity.None);
        C(!LetterFormMath.Tags(none).Any(t => t.StartsWith("fam:")), "no stage, no tag in the context");
    }

    // ================= Moves =================

    private static ThreadRec T(string key, bool ours, int at = 1, byte asked = 0, byte told = 0, string value = "Ennis", int tick = 0) =>
        new ThreadRec { key = key, ours = ours, at = at, asked = asked, told = told, value = value, tick = tick };

    private static void Moves(Action<bool, string> C)
    {
        var known = Familiarity.Known;
        foreach (var key in new[] { "ill", "war", "hard", "head", "raid" })
        {
            C(LetterThreadMath.Move(T(key, false), true, 2, known, false) == "ask", key + ": the colony asks after their news in its next letter");
            C(LetterThreadMath.Move(T(key, false), true, 1, known, false) == null, key + ": not in the same letter");
            C(LetterThreadMath.Move(T(key, false, asked: 1), true, 4, known, false) == null, key + ": asked once");
            C(LetterThreadMath.Move(T(key, false, asked: 1), false, 3, known, false) == "follow", key + ": they follow it up once asked");
            C(LetterThreadMath.Move(T(key, false), false, 3, known, false) == null && LetterThreadMath.Move(T(key, false), false, 4, known, false) == "follow",
                key + ": unasked, they follow it up three letters on");
            C(LetterThreadMath.Move(T(key, false, asked: 1, told: 1), false, 5, known, false) == null, key + ": followed once");
        }
        C(LetterThreadMath.Move(T("loss", false), true, 2, known, false) == "sorry" && LetterThreadMath.Move(T("loss", false, asked: 1), false, 3, known, false) == "follow",
            "a death: sorrow, then how they bear it");
        foreach (var key in new[] { "c:harvest", "c:raid", "c:newcomer", "c:hungry", "c:birth", "c:wedding" })
        {
            C(LetterThreadMath.Move(T(key, true), false, 2, known, false) == "ask", key + ": they ask after the colony's news");
            C(LetterThreadMath.Move(T(key, true, asked: 1), true, 3, known, false) == "follow", key + ": the colony follows it up");
        }
        C(LetterThreadMath.Move(T("c:death", true), false, 2, known, false) == "sorry" && LetterThreadMath.Move(T("c:death", true, asked: 1), true, 3, known, false) == null,
            "a death at home: their sorrow, nothing after");
        foreach (var key in new[] { "aid", "gift" })
        {
            C(LetterThreadMath.Move(T(key, false), true, 2, known, false) == "thank", key + ": the colony thanks for it in its next letter");
            C(LetterThreadMath.Move(T(key, false, asked: 1), true, 4, known, false) == null && LetterThreadMath.Move(T(key, false, asked: 1), true, 4, Familiarity.Close, false) == "thank",
                key + ": once, twice between old friends");
            C(LetterThreadMath.Move(T(key, false), false, 3, known, false) == "hope" && LetterThreadMath.Move(T(key, false, told: 1), false, 5, known, false) == null, key + ": they hope it was of use, once");
        }
        foreach (var key in new[] { "goods", "warned", "condoled" })
        {
            C(LetterThreadMath.Move(T(key, true), false, 2, known, false) == null && LetterThreadMath.Move(T(key, true), false, 3, known, false) == "thank",
                key + ": thanked after their own answer, not in it");
        }
        C(LetterThreadMath.Move(T("goods", true), true, 3, known, false) == "hope" && LetterThreadMath.Move(T("warned", true), true, 3, known, false) == null, "the colony hopes its goods were of use");
        foreach (var key in new[] { "poem", "story", "sermon", "recipe", "enclosure", "long", "chronicle", "diary" })
            foreach (var ours in new[] { false, true })
                C(LetterThreadMath.Move(T(key, ours), !ours, 2, known, false) == "thank" && LetterThreadMath.Move(T(key, ours, asked: 1), !ours, 4, known, false) == null,
                    $"{key} from {(ours ? "the colony" : "them")}: spoken of once");
        C(LetterThreadMath.Move(T("recipe", false), false, 3, known, false) == "hope", "did you try the recipe?");
        C(LetterThreadMath.Move(T("hands", true), false, 2, known, false) == "greet" && LetterThreadMath.Move(T("hands", true), true, 2, known, false) == null, "others' lines greeted back");
        C(LetterThreadMath.Move(T("aid:no", false), false, 3, known, false) == null && LetterThreadMath.Move(T("aid:no", false), false, 3, Familiarity.Familiar, false) == "regret",
            "help refused regretted among friends");
        C(LetterThreadMath.Move(T("aid:no", false), true, 3, known, false) == null && LetterThreadMath.Move(T("aid:no", false), true, 3, known, true) == "grudge", "remembered by the colony in a cool letter");
        C(LetterThreadMath.Move(T("jk:goat", false), true, 2, known, false) == "ask" && LetterThreadMath.Move(T("jk:goat", false, asked: 1), true, 4, known, false) == null
          && LetterThreadMath.Move(T("jk:goat", false, asked: 1, told: 1), true, 5, known, false) == "ask", "a joke asked after once each telling");
        C(LetterThreadMath.Move(T("jk:goat", false), false, 3, known, false) == "more" && LetterThreadMath.Move(T("jk:goat", false, told: 2), false, 5, known, false) == null, "a joke goes on twice");
        C(LetterThreadMath.Move(T("nonsense", false), true, 3, known, false) == null, "an unknown thread does nothing");
        var r = T("ill", false);
        LetterThreadMath.Mark(r, "ask", 777);
        C(r.asked == 1 && r.told == 0 && r.touched == 777, "asked after: counted and touched");
        LetterThreadMath.Mark(r, "follow", 900);
        C(r.told == 1 && r.touched == 900, "followed up");
        // Stale threads let go.
        C(!LetterThreadMath.Stale(T("ill", false, tick: 0), 39 * Day) && LetterThreadMath.Stale(T("ill", false, tick: 0), 41 * Day), "news goes stale after 40 days");
        C(!LetterThreadMath.Stale(T("jk:goat", false, tick: 0), 200 * Day) && LetterThreadMath.Stale(T("jk:goat", false, tick: 0), 241 * Day), "a joke keeps 240 days");
        C(LetterThreadMath.Stale(T("nonsense", false), 0), "an unknown thread is let go");
        // Slots.
        var list = new List<ThreadRec> { T("ill", false, tick: 5), T("war", false, tick: 3) };
        C(LetterThreadMath.Slot(list, T("ill", false, value: "Orrin")) == 0, "news of another ill takes the old one's place");
        C(LetterThreadMath.Slot(list, T("hard", false)) == -1, "a new kind is added while there is room");
        var full = Enumerable.Range(0, LetterThreadMath.MaxThreads).Select(i => T("jk:" + i, false, tick: 10 + i)).ToList();
        full[5].asked = 1;
        C(LetterThreadMath.Slot(full, T("ill", false)) == 5, "a full list gives up a thread already taken up");
        full[5].asked = 0;
        C(LetterThreadMath.Slot(full, T("ill", false)) == 0, "else its oldest");
    }

    // ================= Follow-ups by what is true now =================

    private static void Outcomes(Action<bool, string> C)
    {
        string O(string key, string value, string[] tags, params (string k, string v)[] values) =>
            LetterThreadMath.Outcome(T(key, false, value: value), new HashSet<string>(tags), values.ToDictionary(x => x.k, x => x.v), n => n == "Hessa", 10 * Day);
        C(O("ill", "Ennis", new[] { "town:ill" }, ("ILL", "Ennis")) == "still", "still ill while the facts say so");
        C(O("ill", "Ennis", new[] { "town:ill" }, ("ILL", "Orrin")) == "better", "someone else ill now: Ennis is better");
        C(O("ill", "Ennis", new[] { "town:loss" }, ("LOST", "Ennis")) == "lost", "gone, when the facts hold the death");
        C(O("ill", "Ennis", new string[0]) == "better", "else better");
        C(O("war", "the Red Hand", new[] { "their:peace" }, ("PEACEWITH", "the Red Hand")) == "peace", "a peace made");
        C(O("war", "the Red Hand", new[] { "their:war", "war:winning" }, ("WAR", "the Red Hand")) == "winning"
          && O("war", "the Red Hand", new[] { "their:war", "war:losing" }, ("WAR", "the Red Hand")) == "losing"
          && O("war", "the Red Hand", new[] { "their:war" }, ("WAR", "the Red Hand")) == "still", "a war won, lost, or on");
        C(O("war", "the Red Hand", new[] { "their:war" }, ("WAR", "the Ash Kin")) == "over", "another war now: this one is over");
        C(O("hard", "the blight", new[] { "town:struggling" }) == "still" && O("hard", "the blight", new[] { "town:thriving" }) == "better", "hard times still, or easing");
        C(O("head", "Marta", new[] { "town:head" }, ("HEAD", "Marta")) == "settled" && O("head", "Marta", new[] { "town:head" }, ("HEAD", "Orrin")) == null, "a head settled only while head");
        C(O("c:harvest", "", new[] { "colony:hungry" }) == "thin" && O("c:harvest", "", new[] { "colony:plenty" }) == "full" && O("c:harvest", "", new string[0]) == "in", "the harvest as the stores say");
        C(LetterThreadMath.Outcome(T("c:raid", true, tick: 0), new HashSet<string> { "colony:raid" }, null, null, 10 * Day) == "again"
          && LetterThreadMath.Outcome(T("c:raid", true, tick: 0), new HashSet<string> { "colony:raid" }, null, null, 3 * Day) == "quiet", "raiders again only for a raid after the one told");
        C(O("c:newcomer", "Hessa", new string[0]) == "settled" && O("c:newcomer", "Pell", new string[0]) == null, "a newcomer settled only while at home");
        C(O("c:birth", "Hessa", new string[0]) == "grows" && O("c:birth", "Pell", new string[0]) == null, "a child growing only while at home");
    }

    // ================= Choosing =================

    private static RecallScene Scene(bool ours, Familiarity stage, int letterNo = 6, params string[] tags) => new RecallScene
    {
        ours = ours, stage = stage, letterNo = letterNo, now = 10 * Day, tags = new HashSet<string>(tags), values = new Dictionary<string, string> { ["FROM"] = ours ? "Stace" : "Dagna" },
        here = n => true
    };

    private static void Choosing(Action<bool, string> C)
    {
        var threads = new List<ThreadRec> { T("ill", false, at: 3, tick: 8 * Day), T("jk:goat", false, at: 2, tick: 7 * Day) };
        var r = new Random(9);
        Func<float> roll = () => (float)r.NextDouble();
        foreach (var stage in new[] { Familiarity.None, Familiarity.New })
            C(Enumerable.Range(0, 200).All(_ => LetterThreadMath.Choose(threads, Scene(true, stage), roll) == null), "nothing recalled in a first letter: " + stage);
        // How often a letter takes up a thread, by the stage.
        foreach (var (stage, lo, hi) in new[] { (Familiarity.Known, 0.38f, 0.52f), (Familiarity.Familiar, 0.58f, 0.72f), (Familiarity.Close, 0.73f, 0.87f) })
        {
            int got = Enumerable.Range(0, 2000).Count(_ => LetterThreadMath.Choose(threads, Scene(true, stage), roll) != null);
            C(got / 2000f >= lo && got / 2000f <= hi, $"{stage}: a thread in {got / 20f:F0}% of letters");
        }
        // Only the moves the threads allow, for the side that writes.
        for (int i = 0; i < 500; i++)
        {
            var c = LetterThreadMath.Choose(threads, Scene(true, Familiarity.Close), roll);
            if (c == null) continue;
            C(c.tag == "re:ill:ask" || c.tag == "re:jk:goat:ask", "the colony asks after their news or their joke: " + c.tag);
            C(c.index >= 0 && c.key == threads[c.index].key && (c.value == threads[c.index].value), "the thread it takes up, with its words");
            if (c.ps) C(c.tag == "re:jk:goat:ask", "only a quick line goes in the P.S.");
        }
        var theirSide = LetterThreadMath.Choose(new List<ThreadRec> { T("ill", false, at: 3, asked: 1, tick: 8 * Day) },
            new RecallScene { ours = false, stage = Familiarity.Close, letterNo = 5, now = 10 * Day, tags = new HashSet<string> { "town:ill" }, values = new Dictionary<string, string> { ["ILL"] = "Ennis" } },
            () => 0.1f);
        C(theirSide?.tag == "re:ill:still", "their follow-up says what the facts hold: " + theirSide?.tag);
        // re:last: the thread was touched by the other side's last letter.
        var touched = T("ill", false, at: 3, asked: 1, tick: 2 * Day);
        touched.touched = 8 * Day;
        var last = LetterThreadMath.Choose(new List<ThreadRec> { touched }, new RecallScene { ours = false, stage = Familiarity.Close, letterNo = 6, now = 10 * Day, otherLast = 8 * Day, tags = new HashSet<string>() }, () => 0.1f);
        C(last != null && last.last, "asked in the colony's last letter: \"you asked\" in theirs");
        var notLast = LetterThreadMath.Choose(new List<ThreadRec> { touched }, new RecallScene { ours = false, stage = Familiarity.Close, letterNo = 6, now = 10 * Day, otherLast = 9 * Day, tags = new HashSet<string>() }, () => 0.1f);
        C(notLast != null && !notLast.last, "not in the last letter: no \"your last letter\"");
        // Stale threads are left alone.
        C(Enumerable.Range(0, 200).All(_ => LetterThreadMath.Choose(new List<ThreadRec> { T("ill", false, at: 1, tick: 0) }, new RecallScene { ours = true, stage = Familiarity.Close, letterNo = 3, now = 60 * Day, tags = new HashSet<string>() }, roll) == null),
            "news gone stale is not asked after");
        // Tells: only a fact that holds, never of the writer, never twice.
        var tellScene = Scene(false, Familiarity.Familiar, 4, "town:ill", "their:war");
        ((Dictionary<string, string>)tellScene.values)["ILL"] = "Ennis";
        ((Dictionary<string, string>)tellScene.values)["WAR"] = "the Red Hand";
        var tells = Enumerable.Range(0, 400).Select(_ => LetterThreadMath.Choose(null, tellScene, roll)).Where(c => c != null).ToList();
        C(tells.All(c => c.move == "tell" && (c.tag == "re:tell:ill" && c.value == "Ennis" || c.tag == "re:tell:war" && c.value == "the Red Hand")) && tells.Count > 100, "news told first from the facts: " + tells.Count);
        ((Dictionary<string, string>)tellScene.values)["ILL"] = "Dagna";
        C(Enumerable.Range(0, 300).Select(_ => LetterThreadMath.Choose(null, tellScene, roll)).All(c => c == null || c.tag != "re:tell:ill"), "nobody tells of their own illness as news of the town");
        C(Enumerable.Range(0, 300).Select(_ => LetterThreadMath.Choose(new List<ThreadRec> { T("war", false, value: "the Red Hand", tick: 9 * Day) }, tellScene, roll)).All(c => c == null || c.tag != "re:tell:war"),
            "a war already told is not told again");
        var oursTell = Scene(true, Familiarity.Known, 3, "colony:harvest");
        C(Enumerable.Range(0, 300).Select(_ => LetterThreadMath.Choose(null, oursTell, roll)).Any(c => c?.tag == "re:tell:harvest" && c.raise == "c:harvest"), "the colony tells of its harvest");
        // The colony's guess at their riddle.
        var guess = Scene(true, Familiarity.Familiar);
        guess.riddleOwed = true;
        C(Enumerable.Range(0, 200).Select(_ => LetterThreadMath.Choose(null, guess, roll)).Any(c => c?.tag == "re:riddle:guess"), "the colony guesses at their riddle");
        var noGuess = Scene(false, Familiarity.Familiar);
        noGuess.riddleOwed = true;
        C(Enumerable.Range(0, 200).All(_ => LetterThreadMath.Choose(null, noGuess, roll)?.tag != "re:riddle:guess"), "only the colony guesses");
        // A cooled letter: its cold word, never a friendly thread.
        var cold = Scene(false, Familiarity.Cooled);
        cold.slight = "harm";
        var colds = Enumerable.Range(0, 1000).Select(_ => LetterThreadMath.Choose(threads, cold, roll)).ToList();
        C(colds.All(c => c == null || c.tag == "re:slight:harm") && colds.Count(c => c != null) > 600 && colds.Count(c => c != null) < 800, "a cooled letter names the wrong, seven times in ten");
        var war = Scene(true, Familiarity.Cooled);
        war.war = true;
        C(Enumerable.Range(0, 100).Select(_ => LetterThreadMath.Choose(threads, war, roll)).All(c => c == null || c.tag == "re:slight:war"), "at war: the war");
        C(Enumerable.Range(0, 100).All(_ => LetterThreadMath.Choose(threads, Scene(true, Familiarity.Cooled), roll) == null), "cooled by a fall in regard: nothing named");
    }

    // ================= Raised =================

    private static void Raising(Action<bool, string> C)
    {
        var s = Spec(false, Familiarity.Familiar);
        s.tags.UnionWith(new[] { "town:ill", "their:war", "town:struggling", "town:newhead" });
        s.values["ILL"] = "Ennis";
        s.values["WAR"] = "the Red Hand";
        s.values["FORTUNEWHY"] = "the blight";
        s.values["HEAD"] = "Dagna";
        s.drawn.Add("!jk:goat");
        s.drawn.Add("jk:cat");
        var raised = LetterThreadMath.Raised(false, s, "Ennis is ill. The Red Hand burned the mill.", "aid", "medicine", null);
        string Keys(List<ThreadRec> l) => string.Join(",", l.Select(x => x.key + "=" + x.value));
        C(raised.Any(x => x.key == "ill" && x.value == "Ennis" && !x.ours), "their news raised when the words named it: " + Keys(raised));
        C(raised.Any(x => x.key == "war") && !raised.Any(x => x.key == "hard"), "hard times not named, not raised: " + Keys(raised));
        C(!raised.Any(x => x.key == "head"), "never the writer as their own news");
        C(raised.Any(x => x.key == "jk:goat") && !raised.Any(x => x.key == "jk:cat"), "a joke begun is raised, one going on is not");
        C(raised.Any(x => x.key == "aid" && x.value == "medicine"), "help sent with their answer");
        s.kind = LetterKind.Poem;
        C(LetterThreadMath.Raised(false, s, "", null, null, null).Any(x => x.key == "poem" && x.value == ""), "their poem, for the colony to speak of");
        s.kind = LetterKind.Hands;
        s.who.hands.Add("Pell");
        C(LetterThreadMath.Raised(false, s, "", null, null, null).Any(x => x.key == "hands" && x.value == "Pell"), "others' lines, by the first who added one");
        var tell = new RecallChoice { tag = "re:tell:ill", move = "tell", raise = "ill", value = "Orrin" };
        C(LetterThreadMath.Raised(false, Spec(false, Familiarity.Known), "", null, null, tell).Any(x => x.key == "ill" && x.value == "Orrin"), "news told first raises its thread");
        var o = Spec(true, Familiarity.Familiar, purpose: "condolences");
        o.drawn.Add("colony:harvest");
        o.drawn.Add("colony:newcomer");
        o.values["NEWCOMER"] = "Hessa";
        o.values["LOSTNAME"] = "Ennis";
        o.tags.Add("colony:raid");
        var ours = LetterThreadMath.Raised(true, o, "", null, null, null);
        C(ours.Any(x => x.key == "c:harvest" && x.ours) && ours.Any(x => x.key == "c:newcomer" && x.value == "Hessa") && !ours.Any(x => x.key == "c:raid"),
            "the colony's news only as its lines said it: " + Keys(ours));
        C(ours.Any(x => x.key == "condoled" && x.value == "Ennis"), "condolences sent, for the one who died");
        var reply = Spec(true, Familiarity.Known, purpose: "reply");
        reply.tags.UnionWith(new[] { "k:request", "yes" });
        reply.values["GOODS"] = "some medicine";
        C(LetterThreadMath.Raised(true, reply, "", null, null, null).Any(x => x.key == "goods" && x.value == "some medicine"), "goods agreed to on their request");
        var poet = Spec(true, Familiar(), purpose: "greetings");
        poet.kind = LetterKind.Recipe;
        C(LetterThreadMath.Raised(true, poet, "", null, null, null).Any(x => x.key == "recipe" && x.value == "Bautista"), "the cook's recipe, by the cook");
        poet.kind = LetterKind.Poem;
        C(LetterThreadMath.Raised(true, poet, "", null, null, null).Any(x => x.key == "poem" && x.value == "Stace"), "the colony's poem, by its poet");
        var warn = Spec(true, Familiarity.Known, purpose: "warning");
        C(LetterThreadMath.Raised(true, warn, "", null, null, null).Any(x => x.key == "warned"), "a warning given");
        foreach (var x in raised.Concat(ours)) C(LetterThreadMath.Known(x.key), "every thread raised is of a known kind: " + x.key);
    }

    private static Familiarity Familiar() => Familiarity.Familiar;

    // ================= The pools =================

    /// <summary>Which side writes each move of each kind's line.</summary>
    private static string SideOf(string key, string move)
    {
        bool theirNews = new[] { "ill", "war", "hard", "head", "raid", "loss" }.Contains(key);
        bool ourNews = key.StartsWith("c:");
        if (key == "tell") return null;
        if (key == "slight" || key == "riddle") return key == "riddle" ? "ours" : null;
        if (theirNews) return move == "ask" || move == "sorry" ? "ours" : "theirs";
        if (ourNews) return move == "ask" || move == "sorry" ? "theirs" : "ours";
        if (key == "aid" || key == "gift") return move == "thank" ? "ours" : "theirs";
        if (key == "goods" || key == "warned" || key == "condoled") return move == "thank" ? "theirs" : "ours";
        if (key == "aid:no") return move == "regret" ? "theirs" : "ours";
        if (key.StartsWith("jk:")) return move == "more" ? "theirs" : "ours";
        return null;
    }

    /// <summary>Every line situation a letter can ask for: re:KEY:MOVE with the side that writes it.</summary>
    private static IEnumerable<(string tag, string side)> Reachable()
    {
        var follow = new Dictionary<string, string[]>
        {
            ["ill"] = new[] { "still", "better", "lost" }, ["war"] = new[] { "still", "winning", "losing", "peace", "over" }, ["hard"] = new[] { "still", "better" },
            ["head"] = new[] { "settled" }, ["raid"] = new[] { "after" }, ["loss"] = new[] { "after" }, ["c:harvest"] = new[] { "in", "thin", "full" },
            ["c:raid"] = new[] { "quiet", "again" }, ["c:newcomer"] = new[] { "settled" }, ["c:hungry"] = new[] { "better", "still" }, ["c:birth"] = new[] { "grows" },
            ["c:wedding"] = new[] { "well" }
        };
        foreach (var (key, outs) in follow)
        {
            string first = key == "loss" ? "sorry" : "ask";
            yield return (LetterThreadMath.RecallTag(key, first), SideOf(key, first));
            foreach (var o in outs) yield return (LetterThreadMath.RecallTag(key, o), SideOf(key, "follow"));
        }
        yield return ("re:c:death:sorry", "theirs");
        foreach (var key in new[] { "aid", "gift" })
        {
            yield return (LetterThreadMath.RecallTag(key, "thank"), "ours");
            yield return (LetterThreadMath.RecallTag(key, "hope"), "theirs");
        }
        yield return ("re:goods:thank", "theirs");
        yield return ("re:goods:hope", "ours");
        yield return ("re:warned:thank", "theirs");
        yield return ("re:condoled:thank", "theirs");
        yield return ("re:aid:no:regret", "theirs");
        yield return ("re:aid:no:grudge", "ours");
        foreach (var key in new[] { "poem", "story", "sermon", "recipe", "enclosure", "long", "chronicle", "diary" }) yield return (LetterThreadMath.RecallTag(key, "thank"), "ours");
        // The pieces the colony sends (its poet's verse, its zealot's preaching, its cook's recipe, something folded in): their word on them.
        foreach (var key in new[] { "poem", "sermon", "recipe", "enclosure" }) yield return (LetterThreadMath.RecallTag(key, "thank"), "theirs");
        yield return ("re:recipe:hope", "theirs");
        yield return ("re:recipe:hope", "ours");
        yield return ("re:hands:greet", "theirs");
        yield return ("re:hands:greet", "ours");
        foreach (var j in LetterThreadMath.Jokes)
        {
            yield return (LetterThreadMath.RecallTag(j, "more"), "theirs");
            yield return (LetterThreadMath.RecallTag(j, "ask"), "ours");
        }
        foreach (var t in LetterThreadMath.TheirTells) yield return (LetterThreadMath.TellTag(t.key), "theirs");
        foreach (var t in LetterThreadMath.OurTells) yield return (LetterThreadMath.TellTag(t.key), "ours");
        yield return ("re:riddle:guess", "ours");
        foreach (var g in new[] { "word", "harm", "insult", "theirs", "war" })
        {
            yield return ("re:slight:" + g, "theirs");
            yield return ("re:slight:" + g, "ours");
        }
    }

    private static void Pools(Action<bool, string> C)
    {
        var fam = Lines(LetterPart.Familiar);
        var thread = Lines(LetterPart.Thread);
        var parts = new[] { LetterThreadMath.SaluteTag, LetterThreadMath.OpenTag, LetterThreadMath.CloseTag, LetterThreadMath.PenPart, LetterThreadMath.LongPart };
        var hole = new Regex(@"\{([A-Z_0-9]+)\}");
        foreach (var l in fam)
        {
            var p = P(l);
            string name = p.text;
            C(p.tags.Count(t => parts.Contains(t)) == 1, "a familiar line is one part: " + name);
            C(p.tags.Any(t => t.Split('|').Any(a => a.StartsWith("fam:") && a != LetterThreadMath.LongTag)), "a familiar line names its stage: " + name);
            if (Has(p, LetterThreadMath.PenPart)) C(Has(p, LetterThreadMath.PenTag) && p.text.Contains("{LASTPEN}"), "a new hand's line names the one before: " + name);
            if (Has(p, LetterThreadMath.LongPart)) C(Has(p, LetterThreadMath.LongTag), "a long silence's line is for one: " + name);
            if (p.text.Contains("{TONICK}")) C(Has(p, "fam:familiar") || Has(p, "fam:close"), "a short name only between old friends: " + name);
            if (p.text.Contains("{TOWN}") || p.text.Contains("{COLONY}")) C(Has(p, "theirs") || Has(p, "ours"), "a line naming a place says whose letter it is: " + name);
            if (Has(p, "fam:new")) C(!Regex.IsMatch(name, @"\bagain\b|\bas ever\b|\bas always\b|\bold friend\b", RegexOptions.IgnoreCase), "a first letter is not a familiar one: " + name);
            if (Has(p, "fam:cooled") && !Has(p, "warm|cordial")) C(!p.tags.Any(t => t.Split('|').Contains("warmly")) && !name.Contains("{TONICK}"), "a cooled line is not warm: " + name);
        }
        var known = new HashSet<string> { "re:last", "at:ps" };
        foreach (var l in thread)
        {
            var p = P(l);
            string name = p.text;
            var re = p.tags.Where(t => t.StartsWith("re:") && !known.Contains(t)).ToList();
            C(re.Count == 1 && !re[0].Contains('|'), "a thread's line names one situation: " + name);
            if (re.Count != 1) continue;
            var reached = Reachable().Where(x => x.tag == re[0]).ToList();
            C(reached.Count > 0, "a situation a letter can ask for: " + re[0]);
            if (reached.Count > 0 && reached.All(x => x.side != null))
                C(reached.Any(x => Has(p, x.side)) || !Has(p, "theirs") && !Has(p, "ours") && !p.text.Contains("{TOWN}") && !p.text.Contains("{COLONY}"), $"the right side writes {re[0]}: {name}");
            if (Has(p, "at:ps")) C(name.Length <= 70, "a P.S.'s line is quick: " + name);
            if (p.text.Contains("{RE}"))
                C(!Regex.IsMatch(name, @"\b(he|him|his)\b") && (!Regex.IsMatch(name, @"\b(she|her)\b") || Regex.IsMatch(name, @"\b(goat|cat)\b")), "no pronoun for {RE}: " + name);
            if (re[0].StartsWith("re:gift:")) C(!name.Contains("the {RE}"), "a gift in words is never after \"the\": " + name);
            foreach (Match m in hole.Matches(p.text)) C(m.Groups[1].Value != "RE" || !re[0].StartsWith("re:tell:") && !re[0].StartsWith("re:slight:") && re[0] != "re:riddle:guess", "{RE} only where a thread gives it: " + name);
        }
        // Coverage: every situation has a plain line for its side, and the common ones medieval lines too.
        foreach (var (tag, side) in Reachable().Distinct())
        {
            var fits = thread.Where(l => Has(P(l), tag) && LetterFormMath.SpeechOf(P(l)) == null && (side == null || Has(P(l), side) || !Has(P(l), "theirs") && !Has(P(l), "ours"))).ToList();
            C(fits.Count >= 1, $"a plain line for {tag} ({side})");
            bool common = tag.StartsWith("re:ill:") || tag.StartsWith("re:war:") && !tag.EndsWith(":over") || tag.StartsWith("re:c:harvest:") || tag.StartsWith("re:tell:") && !tag.Contains("hungry")
                          || tag.StartsWith("re:jk:goat") || tag.StartsWith("re:slight:") && tag != "re:slight:insult";
            if (common && side != null) C(fits.Count(l => !Has(P(l), "re:last") && !Has(P(l), "at:ps")) >= 1, $"a line for {tag} ({side}) in any letter");
        }
        foreach (var tag in new[] { "re:ill:ask", "re:ill:still", "re:ill:better", "re:ill:lost", "re:war:ask", "re:war:still", "re:war:peace", "re:c:harvest:ask", "re:c:harvest:in",
                     "re:aid:thank", "re:poem:thank", "re:story:thank", "re:jk:goat:more", "re:jk:goat:ask", "re:riddle:guess", "re:slight:harm", "re:slight:war", "re:tell:war", "re:tell:ill" })
            C(thread.Any(l => Has(P(l), tag) && LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde), "a medieval line for " + tag);
        // A value that may be empty has lines without it.
        foreach (var tag in new[] { "re:raid:ask", "re:c:raid:ask", "re:condoled:thank", "re:gift:thank", "re:poem:thank", "re:recipe:thank" })
            C(thread.Any(l => Has(P(l), tag) && LetterFormMath.SpeechOf(P(l)) == null && !P(l).text.Contains("{RE}")), "a line without {RE} for " + tag);
        // The familiar lines: every stage and side greets, opens and closes in plain English, and in medieval English.
        foreach (var stage in LetterThreadMath.Stages)
            foreach (var side in new[] { "theirs", "ours" })
                foreach (var part in new[] { LetterThreadMath.SaluteTag, LetterThreadMath.OpenTag, LetterThreadMath.CloseTag })
                {
                    foreach (var speech in new[] { null, LetterFormMath.Olde })
                    {
                        foreach (var tone in new[] { AnswerTone.Warm, AnswerTone.Cordial, AnswerTone.Cool })
                        {
                            if (speech == LetterFormMath.Olde && tone != AnswerTone.Cordial) continue;
                            var s = Spec(side == "ours", stage, tone, speech, motive: tone == AnswerTone.Warm ? LetterMotive.Friend : LetterMotive.Plain);
                            var tags = LetterFormMath.With(LetterFormMath.Tags(s), part);
                            bool any = LetterFormMath.Any(fam, tags, s.values);
                            C(any, $"{side} {speech ?? "plain"} {stage} {tone}: a line for {part}");
                        }
                    }
                }
        // The stages read as they should: their lines warm as the friendship grows, formal when it cools.
        int Count(string stage, string pat) => fam.Count(l => Has(P(l), stage) && Regex.IsMatch(P(l).text, pat));
        C(Count("fam:new", @"\{TOFULL\}") >= 10 && Count("fam:cooled", @"\{TOFULL\}") >= 10, "first letters and cooled ones use the full name");
        C(Count("fam:close", @"\{TONICK\}") >= 6, "old friends use a short name");
        C(fam.Count >= 280 && thread.Count >= 350, $"lines enough: {fam.Count} familiar, {thread.Count} threads");
        C(fam.Concat(thread).Count(l => LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde) >= 150, "medieval lines enough: " + fam.Concat(thread).Count(l => LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde));
        // Every running joke is begun by an aside or a P.S., and goes on in the thread pool.
        var jokeLines = Lines(LetterPart.Aside).Concat(Lines(LetterPart.PS)).ToList();
        foreach (var j in LetterThreadMath.Jokes)
        {
            C(jokeLines.Any(l => P(l).tags.Contains("!" + j)), "a joke begun: " + j);
            C(thread.Count(l => Has(P(l), LetterThreadMath.RecallTag(j, "more"))) >= 1 && thread.Count(l => Has(P(l), LetterThreadMath.RecallTag(j, "ask"))) >= 1, "a joke goes on: " + j);
        }
        foreach (var l in jokeLines.Where(l => P(l).tags.Any(t => t.StartsWith("!jk:") || t.StartsWith("jk:"))))
            C(Has(P(l), "theirs") || LetterFormMath.SpeechOf(P(l)) != null, "a running joke is theirs: " + P(l).text);
    }

    // ================= Whole correspondences =================

    private sealed class Book
    {
        public readonly List<ThreadRec> threads = new List<ThreadRec>();
        public int ours, theirs, first = -1, last = -1, lastOurs = -1, lastTheirs = -1, peak = -100;
        public int Letters => ours + theirs;
    }

    /// <summary>
    /// One letter of a correspondence as the game writes it: the standing read, the stage, the thread chosen, the letter
    /// composed from the real pools, then counted, the thread marked and the new ones kept.
    /// </summary>
    private static (LetterSpec s, LetterText t, Familiarity stage) Write(Book b, bool ours, int day, int opinion, Random r, Action<LetterSpec> setup = null, LetterStanding? over = null,
        string slight = null, bool war = false, string speech = null, WorldAge age = WorldAge.Industrial)
    {
        int now = day * Day;
        var st = over ?? new LetterStanding
        {
            letters = b.Letters, mine = ours ? b.ours : b.theirs, days = b.first < 0 ? 0f : (now - b.first) / (float)Day, silence = b.last < 0 ? 0f : (now - b.last) / (float)Day,
            opinion = opinion, peak = Math.Max(b.peak, opinion), goodwill = 20, wrongDays = -1f, war = war
        };
        var stage = LetterThreadMath.StageOf(st);
        var tone = war ? AnswerTone.Curt : opinion >= 40 ? AnswerTone.Warm : opinion >= 5 ? AnswerTone.Cordial : AnswerTone.Cool;
        var s = Spec(ours, stage, tone, speech, age, motive: opinion >= 50 ? LetterMotive.Friend : LetterMotive.Plain);
        s.war = war;
        s.humour = 40;
        if (st.silence >= LetterThreadMath.LongSilence && b.Letters > 0) s.tags.Add(LetterThreadMath.LongTag);
        foreach (var t in b.threads.Where(t => t.key.StartsWith("jk:"))) s.tags.Add(t.key);
        if (stage == Familiarity.Close || stage == Familiarity.Familiar && LetterFormMath.Warmth(s))
        {
            string nick = LetterThreadMath.Nickname(s.values["TO"]);
            if (nick != null) s.values["TONICK"] = nick;
        }
        s.heart = ours ? "We wanted to send our greetings, and to say that Ashford is often in our thoughts." : "Your letter came, and we read it twice over, and were glad of it.";
        if (speech == LetterFormMath.Olde) s.heart = ours ? "We send thee greeting, and think often on Ashford." : "Thy letter came, and we read it twice over, and were glad of it.";
        if (speech == LetterCultureMath.Beast) s.heart = "Your letter came. We read it with our noses first, then our eyes.";
        setup?.Invoke(s);
        string title = ours ? null : "headwoman";
        s.sign = LetterFormMath.Signature(LetterThreadMath.SignsShort(s, (float)r.NextDouble()) ? SignStyle.Short : ours ? SignStyle.Ours : SignStyle.Person, new LetterWords(),
            new Dictionary<string, string>(s.values) { ["TITLE"] = title ?? "" });
        var scene = new RecallScene
        {
            ours = ours, stage = stage, letterNo = b.Letters + 1, now = now, otherLast = ours ? b.lastTheirs : b.lastOurs, tags = s.tags, values = s.values, here = n => true,
            cool = tone >= AnswerTone.Cool, slight = slight, war = war
        };
        s.recall = LetterThreadMath.Choose(b.threads, scene, () => (float)r.NextDouble());
        s.kind = LetterKindMath.Choose(s, LetterLength.Mixed, (float)r.NextDouble());
        if (s.kind != LetterKind.Usual && s.kind != LetterKind.Note && !LetterKindMath.Build(s, new LetterWords(), Shaped(s, r), () => (float)r.NextDouble())) s.kind = LetterKind.Usual;
        var text = LetterFormMath.Compose(s, new LetterWords(), Pick(s, r), () => (float)r.NextDouble());
        var rec = s.recall;
        if (text.recalled && rec != null && rec.index >= 0) LetterThreadMath.Mark(b.threads[rec.index], rec.move, now);
        if (ours)
        {
            b.ours++;
            b.lastOurs = now;
        }
        else
        {
            b.theirs++;
            b.lastTheirs = now;
        }
        if (b.first < 0) b.first = now;
        b.last = now;
        b.peak = Math.Max(b.peak, opinion);
        foreach (var x in LetterThreadMath.Raised(ours, s, text.full, null, null, text.recalled ? rec : null))
        {
            x.at = b.Letters;
            x.tick = now;
            int slot = LetterThreadMath.Slot(b.threads, x);
            if (slot >= 0) b.threads[slot] = x;
            else b.threads.Add(x);
        }
        b.threads.RemoveAll(t => LetterThreadMath.Stale(t, now));
        return (s, text, stage);
    }

    private static readonly Regex Archaic = new Regex(@"\b(thee|thou|thy|thine|hast|hath|doth|dost|thyself)\b", RegexOptions.IgnoreCase);

    private static void Correspondences(Action<bool, string> C, bool print)
    {
        var shown = new List<(string title, string text)>();
        // A warm correspondence over a year: it grows familiar, takes up its threads, and every line drawn fits.
        int recalls = 0, letters = 0, followed = 0;
        var stagesSeen = new HashSet<Familiarity>();
        for (int run = 0; run < 40; run++)
        {
            var r = new Random(100 + run);
            var b = new Book();
            for (int i = 0; i < 16; i++)
            {
                bool ours = i % 2 == 0;
                int day = 1 + i * 4;
                int opinion = 20 + i * 4;
                var drawn = new List<(LetterPart part, string line)>();
                var (s, t, stage) = Write(b, ours, day, opinion, r, x =>
                {
                    if (!ours && i == 3)
                    {
                        x.tags.Add("town:ill");
                        x.values["ILL"] = "Ennis";
                    }
                    if (ours) x.tags.Add("colony:harvest");
                });
                letters++;
                stagesSeen.Add(stage);
                if (t.recalled) recalls++;
                if (t.recalled && s.recall?.move == "follow") followed++;
                C(!Regex.IsMatch(t.letter, @"\{[A-Z]+\}") && !t.letter.Contains("\\n"), "a letter is whole and filled: " + t.letter);
                C(!Archaic.IsMatch(t.letter), "a plain correspondence has no thee or thou: " + t.letter);
                if (stage == Familiarity.New) C(!t.recalled, "a first letter takes up nothing: " + t.letter);
                if (t.letter.Contains("Ennis") && !ours && stage != Familiarity.New && s.recall?.tag != null && s.recall.tag.StartsWith("re:ill:"))
                    C(s.recall.tag == "re:ill:better" || s.recall.tag == "re:ill:still" || s.recall.tag == "re:ill:lost", "their follow-up of Ennis: " + s.recall.tag);
                if (run == 0 && (i == 1 || i == 9 || i == 14)) shown.Add(($"A warm correspondence, letter {i + 1} ({stage})", t.full));
            }
            C(b.threads.Count <= LetterThreadMath.MaxThreads, "threads kept in bounds: " + b.threads.Count);
        }
        C(stagesSeen.SetEquals(new[] { Familiarity.New, Familiarity.Known, Familiarity.Familiar, Familiarity.Close }), "a warm correspondence passes through every stage: " + string.Join(",", stagesSeen));
        C(recalls > letters / 4 && followed > 10, $"letters take up earlier ones ({recalls} of {letters}, {followed} follow-ups)");
        // Ennis's illness: told, asked after, followed by what is true now.
        {
            var r = new Random(7);
            var b = new Book();
            for (int i = 0; i < 6; i++) Write(b, i % 2 == 0, 1 + i * 5, 60, r);
            Write(b, false, 40, 60, r, x =>
            {
                x.tags.Add("town:ill");
                x.values["ILL"] = "Ennis";
            });
            if (!b.threads.Any(t => t.key == "ill"))
                b.threads.Add(new ThreadRec { key = "ill", value = "Ennis", ours = false, at = b.Letters, tick = 40 * Day });
            string asked = null, answered = null;
            for (int i = 0; i < 12 && answered == null; i++)
            {
                bool ours = i % 2 == 0;
                var (s, t, _) = Write(b, ours, 42 + i * 2, 60, r);
                if (t.recalled && s.recall.tag == "re:ill:ask") asked = t.letter;
                if (t.recalled && s.recall.tag == "re:ill:better") answered = t.letter;
            }
            C(asked != null && asked.Contains("Ennis"), "the colony asks after Ennis: " + asked);
            C(answered != null && answered.Contains("Ennis"), "and they write that Ennis is better: " + answered);
            if (asked != null) shown.Add(("The colony asks after Ennis", asked));
            if (answered != null) shown.Add(("They follow it up", answered));
        }
        // The goat: begun, asked after, gone on with.
        {
            int gone = 0;
            for (int run = 0; run < 60 && gone == 0; run++)
            {
                var r = new Random(300 + run);
                var b = new Book();
                b.threads.Add(new ThreadRec { key = "jk:goat", value = "", ours = false, at = 1, tick = 0 });
                b.theirs = 1;
                b.first = 0;
                for (int i = 0; i < 10; i++)
                {
                    var (s, t, _) = Write(b, i % 2 == 0, 2 + i * 4, 70, r);
                    C(!Regex.IsMatch(t.letter, @"Our goat has learned to open the gate"), "the goat is not begun twice");
                    if (t.recalled && s.recall.tag == "re:jk:goat:more")
                    {
                        gone++;
                        if (gone == 1) shown.Add(("The goat, again", t.letter));
                    }
                }
            }
            C(gone > 0, "a running joke comes back");
        }
        // A cooled correspondence: formal, short, cold; the wrong named.
        {
            int notes = 0, cold = 0, made = 0;
            for (int run = 0; run < 60; run++)
            {
                var r = new Random(500 + run);
                var b = new Book();
                for (int i = 0; i < 10; i++) Write(b, i % 2 == 0, 1 + i * 5, 60, r);
                var over = new LetterStanding { letters = b.Letters, mine = b.theirs, days = 50f, opinion = 10, peak = 60, goodwill = 0, wrongDays = 4f, wrongWeight = 12 };
                var (s, t, stage) = Write(b, false, 52, 10, r, null, over, "harm");
                made++;
                C(stage == Familiarity.Cooled, "a wrong cools old friends");
                C(s.kind == LetterKind.Usual || s.kind == LetterKind.Note, "a cooled letter is the usual one or a note: " + s.kind);
                C(!t.letter.Contains("Dearest") && !t.letter.Contains("old friend") && !t.letter.Contains("{TONICK}"), "a cooled letter is not warm: " + t.letter);
                if (s.kind == LetterKind.Note) notes++;
                if (t.recalled && s.recall.tag == "re:slight:harm") cold++;
                if (run == 0) shown.Add(("A cooled correspondence (a wrong done them)", t.full));
            }
            C(cold > made / 3, $"the wrong is named ({cold} of {made})");
        }
        // Medieval old friends and a beast-folk correspondent.
        {
            var r = new Random(41);
            var b = new Book();
            string med = null;
            for (int i = 0; i < 14; i++)
            {
                var (s, t, stage) = Write(b, i % 2 == 0, 1 + i * 5, 50 + i * 3, r, null, null, null, false, i % 2 == 0 ? null : LetterFormMath.Olde, i % 2 == 0 ? WorldAge.Industrial : WorldAge.Medieval);
                if (i % 2 == 1)
                {
                    C(!Regex.IsMatch(t.letter, @"\b(you|your|yours)\b"), "a medieval letter keeps to thee and thou: " + t.letter);
                    if (stage == Familiarity.Close) med = t.full;
                }
            }
            if (med != null) shown.Add(("Medieval old friends", med));
            var r2 = new Random(43);
            var b2 = new Book();
            string beast = null;
            for (int i = 0; i < 14; i++)
            {
                var (s, t, stage) = Write(b2, i % 2 == 0, 1 + i * 5, 50 + i * 3, r2, null, null, null, false, i % 2 == 0 ? null : LetterCultureMath.Beast);
                if (i % 2 == 1 && stage >= Familiarity.Familiar && stage != Familiarity.Cooled) beast = t.full;
            }
            if (beast != null) shown.Add(("A beast-folk correspondent, familiar", beast));
        }
        foreach (var (title, text) in shown)
        {
            C(text.Length > 60 && !Regex.IsMatch(text, @"\{[A-Z]+\}"), "a sample is whole: " + title);
            if (print) Console.WriteLine("=== " + title + " ===\n" + text + "\n");
        }
    }

    // ================= The seams =================

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string form = Code("Letters/LetterForm.cs"), shapes = Code("Letters/LetterFormShapes.cs"), book = Code("Letters/LetterBook.cs"), threads = Code("Letters/LetterThreads.cs");
        C(shapes.Contains("LetterThreads.Correspond(s, n, false, to, null);") && shapes.Contains("LetterThreads.Correspond(s, n, true, n.pawn, writer.LabelShort);")
          && shapes.IndexOf("LetterThreads.Correspond(s, n, false", StringComparison.Ordinal) < shapes.IndexOf("Make(s);\n            }\n            catch (Exception e)\n            {\n                CouncilLog.ErrorOnce(\"letters: their letter's shape\"", StringComparison.Ordinal),
            "the stage and the thread are read before the shape is chosen");
        C(form.Contains("LetterThreads.Corresponded(s, t, n, false, null, a.sent, a.sentWhat);") && form.Contains("LetterThreads.Corresponded(s, t, n, true, writer?.LabelShort ?? l.WriterName);")
          && Regex.Matches(form, @"LetterThreads\.Corresponded\(s, t, n, false\);").Count == 2, "every letter written either way is counted and its threads kept");
        C(form.Contains("Pick(part, tags, s.values, s.drawn)") && form.Contains("drawn.Add(t);"), "what a letter said is read from the lines drawn");
        C(Code("World/NeighbourLetters.cs").Contains("full.sent = l.purpose == LetterPurpose.Help ? things.Count > 0 ? \"aid\" : \"aid:no\" : gifts.Count > 0 ? \"gift\" : null;"),
            "what came with their answer is kept as a thread");
        C(book.Contains("Scribe_Collections.Look(ref correspondences, \"correspondences\", LookMode.Deep);") && book.Contains("public const int MaxCorrespondences = 60;")
          && book.Contains("while (correspondences.Count > MaxCorrespondences)") && book.Contains("LetterThreadMath.MaxThreads"), "the correspondences are saved and bounded");
        C(book.Contains("public static Correspondence CorrespondenceWith(Notable n, bool make)") && book.Contains("foreach (var x in b.exchanges.Where(x => x.notable == n.id))"),
            "an older save's correspondence is counted from the letters kept");
        C(threads.Contains("FactionMemory.Instance") && threads.Contains("NotableBook.Opinion(n)") && threads.Contains("PenFriends.Is(n)") && threads.Contains("NotableBook.Hostile(n)"),
            "how things stand is read from regard, war, pen friends and their people's memory");
        C(threads.Contains("CouncilLog.ErrorOnce(\"letters: a correspondence's stage\", e);") && threads.Contains("s.fam = Familiarity.None;"), "a failure leaves the letter as it was");
        C(Code("Letters/LetterReadWindow.cs").Contains("LetterThreads.Summary(with)"), "the reading window says how the correspondence stands");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_LetterForm.xml"));
        foreach (var f in Enum.GetValues(typeof(Familiarity)).Cast<Familiarity>()) C(keys.Contains("<CC_LetterThread_Stage_" + f + ">"), "the stage's words: " + f);
        C(keys.Contains("<CC_LetterThread_Summary>"), "the summary's words");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Letters/LetterBook.cs:correspondences"), "the correspondences are modelled in the save estimate");
        string validator = File.ReadAllText(Path.Combine(root, "Tools", "DefValidator", "Program.cs"));
        foreach (var ph in new[] { "RE", "TOFULL", "TONICK", "LASTPEN" }) C(validator.Contains("\"" + ph + "\""), "the def validator knows {" + ph + "}");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Letters/LetterThreadMath.cs"), "the rules are linked into the tests");
        // FactionMemory's wrongs: every wrong of the colony's that weighs is named by a cooled letter's cold word.
        string mem = Code("World/FactionMemory.cs");
        foreach (Match m in Regex.Matches(mem, @"\[""(\w+)""\] = K\(DeedSide\.(Wrong|TheirWrong), (\d+)"))
        {
            string kind = m.Groups[1].Value;
            bool theirs = m.Groups[2].Value == "TheirWrong";
            int weight = int.Parse(m.Groups[3].Value);
            if (weight < 6 && kind != "slain" && kind != "fell") continue;
            C(LetterThreadMath.SlightOf(kind, theirs) != null, "a wrong a cooled letter can name: " + kind);
        }
        foreach (var k in LetterThreadMath.Words.Concat(LetterThreadMath.Harms).Concat(LetterThreadMath.Insults))
            C(Regex.IsMatch(mem, @"\[""" + k + @"""\] = K\(DeedSide\.Wrong"), "a wrong of the colony's in FactionMemory: " + k);
    }
}
