using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Letters that read like letters (the player, 9 November 2026): Source/ColonyCouncil/Letters/LetterFormMath.cs and
/// LetterCultureMath.cs, and the pools Defs/FlavorPoolDefs/Rimfolk_Letters_*.xml. The form's rules (the salutation, the
/// closing and the P.S. by tone, relation, motive, age and people; the voice; the schooling; the strange voices); every
/// pool has lines for every purpose, tone and speech it can be asked for; a cool, curt or businesslike letter never draws a
/// warm line; each speech (medieval, unschooled, a strange voice) only for its writers and only its own lines; the
/// medieval English and the plain kept apart; outcomes never inside the writer's words; a call and words passed on not
/// shaped as letters; whole letters made from the real pools (printed with RIMFOLK_LETTERS_PRINT=1); and the seams.
/// </summary>
public static class LetterFormTests
{
    private static readonly LetterPart[] FrameParts = { LetterPart.Salute, LetterPart.Open, LetterPart.Aside, LetterPart.Close, LetterPart.PS };

    private static readonly string[] Registers = { "warm", "warmly", "cordial", "cool", "curt", "refusal" };

    /// <summary>The placeholders a letter's lines may use (the facts service's own besides).</summary>
    private static readonly HashSet<string> Known = new HashSet<string>
    {
        "FROM", "TO", "TOWN", "COLONY", "PEOPLE", "COUNCIL", "TOTITLE", "TITLE", "PURPOSE", "TOPIC", "HELP", "RAIDERS", "GOODS", "CAPTIVE", "ENVOY", "PACT", "TERM",
        "OURS", "THEIRS", "DEAL", "NEWCOMER", "COOK", "DOCTOR", "ARTIST", "DAY", "CAMP", "DISASTER", "CONDITION", "HOMELAND", "SENDER", "HELPER", "SEASON",
        // The shapes (Letters/LetterKindMath.cs): the land's animals and plants, their faith and its god, a riddle's answer,
        // and the facts a chronicle, a long letter's news and a raid's account say (Rimfolk_Letters_Chronicle.xml).
        "ANIMAL", "ANANIMAL", "ANIMALS", "PLANT", "TREE", "DEITY", "IDEO", "ANSWER", "FORTUNEWHY", "TOWNSITUATION", "SITUATION", "LOST", "LOSTTITLE", "HEAD", "HEADTITLE",
        "OLDHEAD", "ILL", "ILLTITLE", "WAR", "PEACEWITH", "NEWTOWN", "NEWTOWNPEOPLE", "RAIDCAMP", "DEED", "KNOWNFOR", "DEAD",
        // A correspondence that builds up (Letters/LetterThreadMath.cs): an earlier letter's thread, the names, the colony's news told.
        "RE", "TOFULL", "TONICK", "LASTPEN", "BORN", "WEDDING"
    };

    /// <summary>The values every letter has (the coverage asks only for these).</summary>
    private static Dictionary<string, string> Base() => new Dictionary<string, string>
    {
        ["FROM"] = "Dagna", ["TO"] = "Stace", ["TOWN"] = "Ashford", ["COLONY"] = "Riverhold", ["PEOPLE"] = "the Rust Kin", ["COUNCIL"] = "the council of Ashford",
        ["TOTITLE"] = "Baroness", ["TITLE"] = "headwoman", ["PURPOSE"] = "greetings"
    };

    private static Dictionary<string, List<string>> pools;

    public static int Run(string root, Action<bool, string> check, bool print = false)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "letters: " + what);
        }

        pools = Load(root);
        Rules(C);
        Hygiene(C);
        Coverage(C);
        Register(C);
        Speech(C);
        Medieval(C);
        Culture(root, C);
        Shapes(C);
        Samples(root, C, print);
        Seams(root, C);
        return n;
    }

    // ================= The pools =================

    private static Dictionary<string, List<string>> Load(string root)
    {
        var map = new Dictionary<string, List<string>>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
        {
            var doc = XDocument.Load(f);
            foreach (var d in doc.Root!.Elements())
            {
                string name = d.Name.LocalName == "ColonyCouncil.FlavorPoolDef" ? d.Element("defName")?.Value : d.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" ? d.Element("pool")?.Value : null;
                if (name == null) continue;
                if (!map.TryGetValue(name, out var list)) map[name] = list = new List<string>();
                foreach (var li in d.Element("lines")?.Element("plain")?.Elements("li") ?? Enumerable.Empty<XElement>()) list.Add(li.Value);
            }
        }
        return map;
    }

    private static List<string> Lines(LetterPart p) => pools.TryGetValue(LetterFormMath.PoolOf(p), out var l) ? l : new List<string>();

    private static IEnumerable<(LetterPart part, string line)> Ours()
    {
        foreach (LetterPart p in Enum.GetValues(typeof(LetterPart)))
            foreach (var l in Lines(p))
                yield return (p, l);
    }

    private static LineTags.Parsed P(string line) => LineTags.Of(line);

    private static bool Has(LineTags.Parsed p, string tag) => p.tags != null && p.tags.Contains(tag);

    /// <summary>A line's register group: the tag whose alternatives are all registers (warm, warmly, cordial, cool, curt, refusal), or null.</summary>
    private static string[] RegisterOf(LineTags.Parsed p)
    {
        if (p.tags == null) return null;
        foreach (var alts in p.alts)
            if (alts.All(a => !a.neg && Registers.Contains(a.name)))
                return alts.Select(a => a.name).ToArray();
        return null;
    }

    private static bool WarmOnly(LineTags.Parsed p)
    {
        var r = RegisterOf(p);
        return r != null && r.All(x => x == "warm" || x == "warmly");
    }

    // ================= Specs =================

    private static LetterSpec Spec(bool ours, string purpose, AnswerTone tone, WorldAge age = WorldAge.Industrial, LetterMotive motive = LetterMotive.Plain, string addr = LetterFormMath.AddrPlain,
        string speech = null, VoiceMain voice = VoiceMain.Plain)
    {
        var s = new LetterSpec { ours = ours, purpose = purpose, tone = tone, age = age, motive = motive };
        s.war = addr == LetterFormMath.AddrWar;
        s.council = addr == LetterFormMath.AddrCouncil;
        s.empire = addr == LetterFormMath.AddrEmpire;
        s.penFriend = addr == LetterFormMath.AddrFriend;
        if (s.penFriend && motive == LetterMotive.Plain) s.motive = LetterMotive.Friend;
        if (speech == LetterFormMath.Olde) s.age = WorldAge.Medieval;
        else if (speech == LetterFormMath.NoSchool) s.schooling = Schooling.None;
        else if (speech == LetterFormMath.SomeSchool) s.schooling = Schooling.Some;
        else if (speech != null) s.strange = speech;
        if (speech != LetterFormMath.Olde && s.age == WorldAge.Medieval) s.age = WorldAge.Industrial;
        s.voice = new LetterVoice { main = voice };
        foreach (var kv in Base()) s.values[kv.Key] = kv.Value;
        return s;
    }

    private static string Pick(LetterPart part, HashSet<string> tags, IReadOnlyDictionary<string, string> values, Random r) =>
        LetterFormMath.Pick(Lines(part), tags, values, null, () => (float)r.NextDouble());

    private static IEnumerable<string> Matching(LetterPart part, HashSet<string> tags) => Lines(part).Where(l => LetterFormMath.Fits(l, tags) >= 0);

    // ================= The rules =================

    private static void Rules(Action<bool, string> C)
    {
        // The voice.
        C(LetterFormMath.Voice(3, 8, false, false, 0).main == VoiceMain.Blunt, "little Social skill writes bluntly");
        C(LetterFormMath.Voice(14, 2, false, true, 0).main == VoiceMain.Blunt, "an abrasive writer is blunt whatever their skill");
        C(LetterFormMath.Voice(12, 6, false, false, 0).main == VoiceMain.Eloquent && LetterFormMath.Voice(15, 0, false, false, 0).main == VoiceMain.Eloquent, "a good deal of Social skill writes eloquently");
        C(LetterFormMath.Voice(8, 8, false, false, 0).main == VoiceMain.Plain, "a middling writer writes plainly");
        var v = LetterFormMath.Voice(8, 11, true, false, 2, true);
        C(v.kind && v.cheerful && v.learned && v.proud && !v.gloomy, "kind, cheerful, learned and proud colour the voice");
        C(LetterFormMath.VoiceTags(v).SequenceEqual(new[] { "v:plain", "v:kind", "v:cheerful", "v:learned", "v:proud" }), "the voice as tags");
        // How the colony stands with them.
        C(LetterFormMath.Stance(60) == AnswerTone.Warm && LetterFormMath.Stance(10) == AnswerTone.Cordial && LetterFormMath.Stance(-10) == AnswerTone.Cool
          && LetterFormMath.Stance(-40) == AnswerTone.Curt, "the colony's stance by their regard");
        // What the writer wants.
        C(LetterFormMath.Motive(false, true, true, true, true, "pact", 80, true, 90, -90) == LetterMotive.Plain, "at war, a letter is only a letter");
        C(LetterFormMath.Motive(false, false, true, false, false, "greetings", -20, true, 90, -90) == LetterMotive.Friend, "a pen friend writes as a friend");
        C(LetterFormMath.Motive(false, false, false, true, false, "council", 80, false, 0, 0) == LetterMotive.Duty, "a council writes as a duty");
        C(LetterFormMath.Motive(false, false, false, false, true, "greetings", 80, false, 0, 0) == LetterMotive.Duty, "the Empire writes as a duty");
        C(LetterFormMath.Motive(false, false, false, false, false, "pact", 80, false, 0, 0) == LetterMotive.Duty, "the business between peoples is a duty");
        C(LetterFormMath.Motive(false, false, false, false, false, "greetings", 0, true, 0, 0) == LetterMotive.Wary, "one who thinks little of the colony sounds it out");
        C(LetterFormMath.Motive(false, false, false, false, false, "greetings", 30, false, 0, -40) == LetterMotive.Wary, "one who trusts nobody sounds it out");
        C(LetterFormMath.Motive(false, false, false, false, false, "first", 30, true, 60, 0) == LetterMotive.Proud, "a proud one won't ask outright");
        C(LetterFormMath.Motive(false, false, false, false, false, "first", 30, true, 0, 0) == LetterMotive.Trade, "a trader angles for business");
        C(LetterFormMath.Motive(false, false, false, false, false, "greetings", 60, false, 0, 0) == LetterMotive.Friend, "a neighbour who thinks a good deal of the colony writes as a friend");
        C(LetterFormMath.Motive(false, false, false, false, false, "greetings", 30, false, 0, 0) == LetterMotive.Plain, "else nothing but the letter");
        C(LetterFormMath.Motive(true, false, false, false, false, "greetings", 30, true, 90, -90) == LetterMotive.Plain, "the colony's own: no trader's or proud motive of theirs");
        // The register: tone by the situation.
        var warmFriend = Spec(false, "greetings", AnswerTone.Warm);
        C(LetterFormMath.Warmth(warmFriend) && LetterFormMath.ToneTag(warmFriend) == "warm", "a warm answer that is no business is warm");
        var cordialPlain = Spec(false, "greetings", AnswerTone.Cordial);
        C(!LetterFormMath.Warmth(cordialPlain), "a courteous answer is not warm by default");
        C(LetterFormMath.Warmth(Spec(false, "greetings", AnswerTone.Cordial, motive: LetterMotive.Friend)), "a courteous answer between friends is warm");
        foreach (var m in new[] { LetterMotive.Duty, LetterMotive.Trade, LetterMotive.Wary, LetterMotive.Proud })
        {
            var s = Spec(false, "greetings", AnswerTone.Warm, motive: m);
            C(!LetterFormMath.Warmth(s) && LetterFormMath.ToneTag(s) == "cordial", $"a warm answer from {m} is courteous, not warm");
        }
        C(!LetterFormMath.Warmth(Spec(false, "pact", AnswerTone.Warm)) && LetterFormMath.ToneTag(Spec(false, "pact", AnswerTone.Warm)) == "cordial", "the business between peoples is never warm");
        C(!LetterFormMath.Warmth(Spec(false, "council", AnswerTone.Warm, addr: LetterFormMath.AddrCouncil)), "a council is never warm");
        C(!LetterFormMath.Warmth(Spec(false, "greetings", AnswerTone.Warm, addr: LetterFormMath.AddrWar)), "at war, not warm");
        var abrasive = Spec(true, "greetings", AnswerTone.Warm);
        abrasive.voice.abrasive = true;
        C(!LetterFormMath.Warmth(abrasive), "the colony's abrasive writer is never warm");
        var kindOurs = Spec(true, "greetings", AnswerTone.Cordial);
        kindOurs.voice.kind = true;
        C(LetterFormMath.Warmth(kindOurs), "the colony's kind writer warms a courteous letter");
        // Address.
        C(LetterFormMath.Address(Spec(false, "x", AnswerTone.Cool, addr: LetterFormMath.AddrWar)) == LetterFormMath.AddrWar, "at war first");
        var both = Spec(false, "council", AnswerTone.Cool, addr: LetterFormMath.AddrCouncil);
        both.war = true;
        C(LetterFormMath.Address(both) == LetterFormMath.AddrWar, "a council at war writes as one at war");
        // Schooling.
        C(LetterFormMath.SchoolingOf(3, true, false, false, WorldAge.Spacer, false, false) == Schooling.None, "a pirate of little learning can't write");
        C(LetterFormMath.SchoolingOf(6, true, false, false, WorldAge.Spacer, false, false) == Schooling.Some, "a pirate of some learning barely can");
        C(LetterFormMath.SchoolingOf(8, true, false, false, WorldAge.Spacer, false, false) == Schooling.Schooled, "a learned pirate writes well");
        C(LetterFormMath.SchoolingOf(1, false, false, false, WorldAge.Industrial, false, false) == Schooling.Some, "little Intellectual skill: a few slips");
        C(LetterFormMath.SchoolingOf(1, false, false, false, WorldAge.Medieval, false, false) == Schooling.None, "a medieval writer who never learnt letters");
        C(LetterFormMath.SchoolingOf(4, false, true, false, WorldAge.Industrial, false, false) == Schooling.Some, "a slow learner");
        C(LetterFormMath.SchoolingOf(3, true, true, true, WorldAge.Tribal, false, false) == Schooling.Some, "one too smart makes up for much");
        C(LetterFormMath.SchoolingOf(0, true, true, false, WorldAge.Tribal, true, false) == Schooling.Schooled && LetterFormMath.SchoolingOf(0, true, true, false, WorldAge.Tribal, false, true) == Schooling.Schooled,
            "a council and the Empire have scribes");
        // The speech.
        var med = new LetterSpec { purpose = "greetings", tone = AnswerTone.Cordial, age = WorldAge.Medieval };
        C(LetterFormMath.SpeechOf(med) == LetterFormMath.Olde, "a medieval writer writes medieval English");
        var medEmpire = Spec(false, "greetings", AnswerTone.Cordial, WorldAge.Medieval, addr: LetterFormMath.AddrEmpire);
        C(LetterFormMath.SpeechOf(medEmpire) == null, "the Empire keeps its own style");
        C(LetterFormMath.SpeechOf(Spec(false, "greetings", AnswerTone.Cordial, WorldAge.Tribal)) == null, "a tribe keeps its own plain voice");
        var badMed = Spec(false, "greetings", AnswerTone.Cordial, WorldAge.Medieval);
        badMed.schooling = Schooling.None;
        C(LetterFormMath.SpeechOf(badMed) == LetterFormMath.NoSchool, "an unschooled hand before the medieval English");
        badMed.strange = LetterCultureMath.Beast;
        C(LetterFormMath.SpeechOf(badMed) == LetterCultureMath.Beast, "a strange voice before all");
        C(LetterFormMath.HeartSpeech(LetterFormMath.Olde) == LetterFormMath.Olde && LetterFormMath.HeartSpeech(LetterFormMath.NoSchool) == LetterFormMath.NoSchool
          && LetterFormMath.HeartSpeech(LetterCultureMath.Hive) == LetterCultureMath.Hive && LetterFormMath.HeartSpeech(LetterFormMath.SomeSchool) == null
          && LetterFormMath.HeartSpeech(LetterCultureMath.Fey) == null, "whose hearts are their own");
        // The heading, dates and signatures.
        C(LetterFormMath.DateStyleOf(WorldAge.Tribal, false) == DateStyle.Season && LetterFormMath.DateStyleOf(WorldAge.Medieval, false) == DateStyle.Courtly
          && LetterFormMath.DateStyleOf(WorldAge.Industrial, false) == DateStyle.Plain && LetterFormMath.DateStyleOf(WorldAge.Spacer, false) == DateStyle.Standard
          && LetterFormMath.DateStyleOf(WorldAge.Spacer, true) == DateStyle.Courtly, "each age dates its letters its own way, the Empire in the court's words");
        var w = new LetterWords();
        C(LetterFormMath.Date(DateStyle.Season, w, 5, "Aprimay", 0, 5501, "when the geese came back") == "when the geese came back", "a tribe's date is its season");
        C(LetterFormMath.Date(DateStyle.Courtly, w, 5, "Aprimay", 0, 5501, null) == "the 5th day of Aprimay, in the year 5501", "the court's date");
        C(LetterFormMath.Date(DateStyle.Plain, w, 5, "Aprimay", 0, 5501, null) == "5 Aprimay 5501", "the plain date");
        C(LetterFormMath.Date(DateStyle.Standard, w, 5, "Jugust", 1, 5501, null) == "5501.2.05", "the standard date");
        C(LetterFormMath.Ordinal(1) == "1st" && LetterFormMath.Ordinal(2) == "2nd" && LetterFormMath.Ordinal(3) == "3rd" && LetterFormMath.Ordinal(11) == "11th"
          && LetterFormMath.Ordinal(12) == "12th" && LetterFormMath.Ordinal(13) == "13th" && LetterFormMath.Ordinal(15) == "15th" && LetterFormMath.Ordinal(21) == "21st", "ordinals");
        C(LetterFormMath.SignOf(true, false, false, false) == SignStyle.Ours && LetterFormMath.SignOf(true, false, false, true) == SignStyle.OursTitled
          && LetterFormMath.SignOf(false, true, false, true) == SignStyle.Council && LetterFormMath.SignOf(false, false, true, true) == SignStyle.Noble
          && LetterFormMath.SignOf(false, false, false, true) == SignStyle.Person && LetterFormMath.SignOf(false, false, false, false) == SignStyle.PersonUntitled,
            "each writer signs in kind");
        C(LetterFormMath.SignOf(false, false, false, true, LetterFormMath.NoSchool) == SignStyle.Mark && LetterFormMath.SignOf(true, false, false, false, LetterFormMath.NoSchool, true) == SignStyle.Helped
          && LetterFormMath.SignOf(false, false, false, true, LetterCultureMath.Hive) == SignStyle.Many, "a mark, a helper's hand, a hive's many");
        var sign = LetterFormMath.Signature(SignStyle.Person, w, Base());
        C(sign == "Dagna, headwoman of Ashford", "a person's signature: " + sign);
        var councilSpec = Spec(false, "council", AnswerTone.Cool, addr: LetterFormMath.AddrCouncil);
        int headings = 0, warHeadings = 0;
        for (int i = 0; i < 100; i++)
        {
            float r = i / 100f;
            if (LetterFormMath.Heading(Spec(false, "greetings", AnswerTone.Cordial), r)) headings++;
            if (LetterFormMath.Heading(Spec(false, "greetings", AnswerTone.Cordial, addr: LetterFormMath.AddrWar), r)) warHeadings++;
            C(LetterFormMath.Heading(councilSpec, r), "a council always heads its letter");
        }
        C(headings == 70 && warHeadings == 40, $"most letters are headed, fewer at war ({headings}, {warHeadings})");
        var call = Spec(false, "greetings", AnswerTone.Warm);
        call.shape = LetterShape.Call;
        C(!LetterFormMath.Heading(call, 0f) && LetterFormMath.Aside(call, 0f) == AsideKind.None && LetterFormMath.PSChance(call) == 0f, "a call has no heading, aside or P.S.");
        // The aside and the P.S. by the situation.
        int Count(LetterSpec s, AsideKind k) => Enumerable.Range(0, 1000).Count(i => LetterFormMath.Aside(s, i / 1000f) == k);
        var condol = Spec(false, "condolences", AnswerTone.Warm);
        C(Count(condol, AsideKind.Joke) == 0 && Count(condol, AsideKind.Serious) > 0, "a condolence never jokes, and is serious");
        var warLetter = Spec(false, "greetings", AnswerTone.Warm, addr: LetterFormMath.AddrWar);
        C(Count(warLetter, AsideKind.Joke) == 0, "a letter at war never jokes");
        C(Count(Spec(false, "council", AnswerTone.Warm, addr: LetterFormMath.AddrCouncil), AsideKind.None) == 1000, "a council writes no aside");
        int warmJokes = Count(warmFriend, AsideKind.Joke), cordialJokes = Count(cordialPlain, AsideKind.Joke), coolJokes = Count(Spec(false, "greetings", AnswerTone.Cool), AsideKind.Joke);
        C(warmJokes > cordialJokes && cordialJokes > coolJokes && coolJokes > 0, $"warm letters joke most, a cool one a dry remark now and then ({warmJokes}, {cordialJokes}, {coolJokes})");
        var dour = Spec(false, "greetings", AnswerTone.Warm);
        dour.humour = -100;
        var wit = Spec(false, "greetings", AnswerTone.Warm);
        wit.humour = 100;
        C(Count(dour, AsideKind.Joke) < warmJokes && Count(wit, AsideKind.Joke) > warmJokes, "a writer's humour sets how often they joke");
        C(Count(Spec(false, "greetings", AnswerTone.Refusal), AsideKind.None) == 1000, "a refusal of light business has no aside");
        C(Count(Spec(false, "greetings", AnswerTone.Cordial, motive: LetterMotive.Trade), AsideKind.None) < Count(cordialPlain, AsideKind.None), "a trader adds a line more often");
        var blunt = Spec(true, "greetings", AnswerTone.Warm, voice: VoiceMain.Blunt);
        var eloquent = Spec(true, "greetings", AnswerTone.Warm, voice: VoiceMain.Eloquent);
        C(Count(blunt, AsideKind.None) > Count(eloquent, AsideKind.None), "the blunt add less, the eloquent more");
        C(LetterFormMath.PSChance(warmFriend) > LetterFormMath.PSChance(cordialPlain) && LetterFormMath.PSChance(cordialPlain) > LetterFormMath.PSChance(Spec(false, "greetings", AnswerTone.Cool))
          && LetterFormMath.PSChance(Spec(false, "greetings", AnswerTone.Cool)) > LetterFormMath.PSChance(Spec(false, "greetings", AnswerTone.Refusal)), "a P.S. by the tone");
        C(LetterFormMath.PSChance(councilSpec) < 0.05f && LetterFormMath.PSChance(Spec(false, "greetings", AnswerTone.Cordial, motive: LetterMotive.Trade)) > LetterFormMath.PSChance(cordialPlain),
            "a council seldom adds a P.S., a trader more often");
        var ancient = Spec(false, "greetings", AnswerTone.Cool, speech: LetterCultureMath.Ancient);
        C(LetterFormMath.PSChance(ancient) > 0.8f && LetterFormMath.Heading(ancient, 0.99f), "the ancient always head their letters, and nearly always add a postscript");
        C(LetterFormMath.PSKind(condol, 0f) == AsideKind.Serious && LetterFormMath.PSKind(warmFriend, 0.1f) == AsideKind.Joke && LetterFormMath.PSKind(cordialPlain, 0.3f) == AsideKind.Everyday,
            "the P.S.'s kind by the situation");
        C(LetterFormMath.Cap(new string('a', 100) + ". " + new string('b', 100), 150).Length <= 150 && LetterFormMath.Cap("short", 150) == "short", "kept text is capped");
    }

    // ================= Every line =================

    private static void Hygiene(Action<bool, string> C)
    {
        var hole = new Regex(@"\{([A-Z_]+)\}");
        foreach (var (part, line) in Ours())
        {
            var p = P(line);
            string name = part + ": " + p.text;
            C(!line.Contains("—") && !line.Contains("–"), "no em or en dash: " + name);
            C(!p.text.Contains("!"), "no exclamation mark: " + name);
            C(p.tags != null && p.tags.Length > 0, "every line is tagged: " + name);
            foreach (Match m in hole.Matches(p.text)) C(Known.Contains(m.Groups[1].Value), "a known placeholder {" + m.Groups[1].Value + "}: " + name);
            C(!Regex.IsMatch(p.text, @"\b(dynasty|bloodline|lineage|heir to|noble house|our house|your house|the house of)\b", RegexOptions.IgnoreCase), "never dynastic: " + name);
            string speech = LetterFormMath.SpeechOf(p);
            if (FrameParts.Contains(part))
            {
                bool formal = Has(p, LetterFormMath.AddrWar) || Has(p, LetterFormMath.AddrCouncil) || Has(p, LetterFormMath.AddrEmpire);
                bool serious = Has(p, LetterFormMath.SeriousTag);
                // A line for one purpose may be neutral (it fits any register); every other frame line names its register.
                bool purposeOnly = p.tags.Any(t => LetterFormMath.TheirPurposes.Contains(t) || LetterFormMath.OurPurposes.Contains(t));
                C(RegisterOf(p) != null || formal || serious || purposeOnly || speech != null && speech.StartsWith("sv:"), "a frame line names its register: " + name);
                if (speech == null && (part == LetterPart.Salute || part == LetterPart.Close))
                    C(p.tags.Count(t => t.Split('|').Any(a => a.StartsWith("a:"))) == 1, "a plain salutation or closing names its address: " + name);
                if (part == LetterPart.Aside || part == LetterPart.PS)
                    C(Has(p, "joke") || Has(p, "everyday") || Has(p, "serious"), "an aside or P.S. names its kind: " + name);
            }
            if (speech == LetterFormMath.NoSchool || speech == LetterFormMath.SomeSchool) C(!Has(p, "a:council") && !Has(p, "a:empire"), "a council and the Empire have scribes: " + name);
        }
        C(Lines(LetterPart.Salute).Count >= 150 && Lines(LetterPart.Close).Count >= 140 && Lines(LetterPart.Open).Count >= 200 && Lines(LetterPart.Aside).Count >= 260
          && Lines(LetterPart.PS).Count >= 130 && Lines(LetterPart.Heart).Count >= 150 && Lines(LetterPart.Ours).Count >= 180,
            $"a library large enough to rarely repeat ({string.Join(", ", Enum.GetValues(typeof(LetterPart)).Cast<LetterPart>().Select(x => x + " " + Lines(x).Count))})");
    }

    // ================= Coverage =================

    private static readonly string[] Plainish = { LetterFormMath.AddrPlain, LetterFormMath.AddrFriend, LetterFormMath.AddrWar };

    private static void Coverage(Action<bool, string> C)
    {
        var r = new Random(7);
        var values = Base();
        // Their letters: every purpose, tone, address and motive, in plain and medieval English.
        foreach (var speech in new[] { null, LetterFormMath.Olde })
            foreach (var purpose in LetterFormMath.TheirPurposes)
                foreach (var tone in LetterFormMath.Tones)
                    foreach (var addr in speech == null ? LetterFormMath.Addresses : new[] { LetterFormMath.AddrPlain, LetterFormMath.AddrFriend, LetterFormMath.AddrWar, LetterFormMath.AddrCouncil })
                        foreach (var motive in LetterFormMath.Motives)
                        {
                            var s = Spec(false, purpose, tone, WorldAge.Industrial, motive, addr, speech);
                            var tags = LetterFormMath.Tags(s);
                            string what = $"theirs {speech ?? "plain"} {purpose} {tone} {addr} {motive}";
                            C(Pick(LetterPart.Salute, tags, values, r) != null, "a salutation for " + what);
                            C(Pick(LetterPart.Close, tags, values, r) != null, "a closing for " + what);
                            if (tone <= AnswerTone.Cool) C(Pick(LetterPart.Open, tags, values, r) != null, "an opening for " + what);
                            if (speech == LetterFormMath.Olde) C(Pick(LetterPart.Heart, LetterFormMath.HeartTags(s), values, r) != null, "a medieval heart for " + what);
                            foreach (var kind in new[] { AsideKind.Joke, AsideKind.Everyday, AsideKind.Serious })
                                if (Enumerable.Range(0, 200).Any(i => LetterFormMath.Aside(s, i / 200f) == kind))
                                    C(Pick(LetterPart.Aside, LetterFormMath.Tags(s, kind), values, r) != null, $"an aside ({kind}) for " + what);
                            if (LetterFormMath.PSChance(s) > 0.05f)
                                foreach (var kind in new[] { AsideKind.Joke, AsideKind.Everyday, AsideKind.Serious })
                                    if (Enumerable.Range(0, 20).Any(i => LetterFormMath.PSKind(s, i / 20f) == kind))
                                        C(Pick(LetterPart.PS, LetterFormMath.Tags(s, kind), values, r) != null, $"a P.S. ({kind}) for " + what);
                        }
        // Every age's plain writers, a tribe's season for its date.
        foreach (var age in new[] { WorldAge.Tribal, WorldAge.Industrial, WorldAge.Spacer })
            foreach (var tone in LetterFormMath.Tones)
            {
                var s = Spec(false, "greetings", tone, age);
                C(Pick(LetterPart.Salute, LetterFormMath.Tags(s), values, r) != null && Pick(LetterPart.Close, LetterFormMath.Tags(s), values, r) != null, $"every age greets and closes ({age} {tone})");
            }
        foreach (var season in new[] { "season:spring", "season:summer", "season:autumn", "season:winter" })
        {
            var s = Spec(false, "greetings", AnswerTone.Cordial, WorldAge.Tribal);
            s.tags.Add(season);
            C(Matching(LetterPart.When, LetterFormMath.Tags(s)).Count(l => Has(P(l), season)) >= 4, "a tribal writer's dates for " + season);
        }
        // The colony's own letters: every purpose, stance and voice, in plain and medieval English and an unschooled hand.
        foreach (var speech in new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool })
            foreach (var purpose in LetterFormMath.OurPurposes)
                foreach (var tone in new[] { AnswerTone.Warm, AnswerTone.Cordial, AnswerTone.Cool, AnswerTone.Curt })
                    foreach (var voice in new[] { VoiceMain.Plain, VoiceMain.Blunt, VoiceMain.Eloquent })
                        foreach (var addr in purpose == "dealing" ? new[] { LetterFormMath.AddrCouncil } : speech == null ? new[] { LetterFormMath.AddrPlain, LetterFormMath.AddrFriend, LetterFormMath.AddrWar, LetterFormMath.AddrEmpire } : Plainish)
                        {
                            var s = Spec(true, purpose, tone, WorldAge.Industrial, LetterFormMath.Motive(true, addr == LetterFormMath.AddrWar, addr == LetterFormMath.AddrFriend,
                                addr == LetterFormMath.AddrCouncil, addr == LetterFormMath.AddrEmpire, purpose, tone == AnswerTone.Warm ? 60 : tone == AnswerTone.Cordial ? 20 : -10, false, 0, 0), addr, speech, voice);
                            var tags = LetterFormMath.Tags(s);
                            string what = $"ours {speech ?? "plain"} {purpose} {tone} {voice} {addr}";
                            C(Pick(LetterPart.Salute, tags, values, r) != null, "a salutation for " + what);
                            C(Pick(LetterPart.Close, tags, values, r) != null, "a closing for " + what);
                            C(Pick(LetterPart.Ours, LetterFormMath.HeartTags(s), values, r) != null, "a heart for " + what);
                            if (speech != LetterFormMath.NoSchool) C(Pick(LetterPart.Open, tags, values, r) != null, "an opening for " + what);
                        }
        // The unschooled, the barely schooled and the strange voices: every tone greets (or has a form with none), closes and opens.
        foreach (var speech in new[] { LetterFormMath.SomeSchool, LetterFormMath.NoSchool }.Concat(LetterCultureMath.Voices))
            foreach (var tone in LetterFormMath.Tones)
                foreach (var addr in Plainish)
                    foreach (var purpose in new[] { "greetings", "condolences", "truce", "first", "help" })
                    {
                        var s = Spec(false, purpose, tone, WorldAge.Industrial, LetterMotive.Plain, addr, speech);
                        var tags = LetterFormMath.Tags(s);
                        string what = $"{speech} {purpose} {tone} {addr}";
                        bool salutes = speech != LetterCultureMath.Hive && speech != LetterCultureMath.Machine;
                        if (salutes) C(Pick(LetterPart.Salute, tags, values, r) != null, "a salutation for " + what);
                        C(Pick(LetterPart.Close, tags, values, r) != null, "a closing for " + what);
                        if (tone <= AnswerTone.Cool) C(Pick(LetterPart.Open, tags, values, r) != null, "an opening for " + what);
                        if (LetterFormMath.HeartSpeech(speech) != null) C(Pick(LetterPart.Heart, LetterFormMath.HeartTags(s), values, r) != null, "a heart of their own for " + what);
                    }
        // A call and words passed on.
        foreach (var tone in LetterFormMath.Tones)
            foreach (var age in new[] { WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer })
            {
                var s = Spec(false, "greetings", tone, age);
                s.shape = LetterShape.Call;
                foreach (var moment in new[] { "hail", "pickup", "reply", "bye", "back", "answer", "word", "wordbye" })
                {
                    var t = LetterFormMath.Tags(s);
                    t.Remove(LetterFormMath.Olde);
                    t.Add("call:" + moment);
                    C(Pick(LetterPart.Call, t, values, r) != null, $"a call's {moment} ({tone}, {age})");
                }
                s.shape = LetterShape.Spoken;
                foreach (var moment in new[] { "s:open", "s:close" })
                {
                    var t = LetterFormMath.Tags(s);
                    t.Add(moment);
                    C(Pick(LetterPart.Spoken, t, values, r) != null, $"words passed on, {moment} ({tone}, {age})");
                }
            }
    }

    // ================= A cool, curt or businesslike letter is never warm =================

    private static void Register(Action<bool, string> C)
    {
        int checkedContexts = 0;
        // Every purpose in plain and medieval English; a few for the other speeches, whose lines seldom name one.
        var few = new[] { "greetings", "condolences", "council", "dealing", "first", "help", "pact" };
        foreach (var ours in new[] { false, true })
            foreach (var speech in LetterFormMath.Speeches)
                foreach (var purpose in (ours ? LetterFormMath.OurPurposes : LetterFormMath.TheirPurposes).Where(x => speech == null || speech == LetterFormMath.Olde || few.Contains(x)))
                    foreach (var tone in LetterFormMath.Tones)
                        foreach (var motive in LetterFormMath.Motives)
                            foreach (var addr in LetterFormMath.Addresses)
                            {
                                var s = Spec(ours, purpose, tone, WorldAge.Industrial, motive, addr, speech);
                                bool cold = tone >= AnswerTone.Cool || LetterFormMath.Business(s) || motive == LetterMotive.Wary || motive == LetterMotive.Proud;
                                if (!cold) continue;
                                checkedContexts++;
                                C(!LetterFormMath.Warmth(s), $"no warm register for {tone} {motive} {purpose}");
                                foreach (var kind in new[] { AsideKind.None, AsideKind.Joke, AsideKind.Everyday, AsideKind.Serious })
                                {
                                    var tags = LetterFormMath.Tags(s, kind);
                                    C(!tags.Contains("warm") && !tags.Contains(LetterFormMath.WarmlyTag), $"a {tone} {motive} {purpose} letter is not asked for warm lines");
                                    // The salutation, opening and closing are asked for without a kind; an aside and a P.S. with one.
                                    var parts = kind == AsideKind.None ? new[] { LetterPart.Salute, LetterPart.Open, LetterPart.Close } : new[] { LetterPart.Aside, LetterPart.PS };
                                    foreach (var part in parts)
                                        foreach (var line in Matching(part, tags))
                                            C(!WarmOnly(P(line)), $"a cool, curt or businesslike letter ({tone}, {motive}, {purpose}, {addr}, {speech ?? "plain"}) drew a warm {part}: {P(line).text}");
                                }
                            }
        C(checkedContexts > 1000, "the cool, curt and businesslike letters looked at: " + checkedContexts);
        // The mix comes from the situation: a cool letter, a businesslike one and a wary one each have their own lines.
        foreach (var motive in new[] { LetterMotive.Trade, LetterMotive.Duty, LetterMotive.Wary, LetterMotive.Proud, LetterMotive.Friend })
        {
            string tag = LetterFormMath.MotiveTag(motive);
            C(FrameParts.Sum(p => Lines(p).Count(l => Has(P(l), tag) && LetterFormMath.SpeechOf(P(l)) == null)) >= 12, "plain lines written for " + tag);
            C(FrameParts.Sum(p => Lines(p).Count(l => Has(P(l), tag) && LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde)) >= 3, "medieval lines written for " + tag);
        }
        int cool = FrameParts.Sum(p => Lines(p).Count(l => RegisterOf(P(l))?.Contains("cool") == true));
        int warm = FrameParts.Sum(p => Lines(p).Count(l => WarmOnly(P(l))));
        C(cool >= 120, $"cool letters have lines enough of their own ({cool}; warm-only {warm})");
        C(Lines(LetterPart.Aside).Count(l => Has(P(l), "joke") && RegisterOf(P(l))?.Contains("cool") == true) >= 12, "dry remarks for a cool letter");
    }

    // ================= Each speech only for its writers =================

    private static void Speech(Action<bool, string> C)
    {
        foreach (var speech in LetterFormMath.Speeches)
            foreach (var other in LetterFormMath.Speeches)
            {
                var s = Spec(false, "greetings", AnswerTone.Cordial, WorldAge.Industrial, LetterMotive.Plain, LetterFormMath.AddrPlain, other);
                var tags = LetterFormMath.Tags(s, AsideKind.Everyday);
                foreach (var part in new[] { LetterPart.Salute, LetterPart.Open, LetterPart.Aside, LetterPart.Close, LetterPart.PS, LetterPart.Heart, LetterPart.Ours })
                    foreach (var line in Matching(part, tags))
                        C(LetterFormMath.SpeechOf(P(line)) == other, $"a {other ?? "plain"} writer drew only {other ?? "plain"} lines ({part}: {P(line).text})");
            }
        foreach (var speech in LetterFormMath.Speeches.Where(x => x != null))
            C(Ours().Count(x => LetterFormMath.SpeechOf(P(x.line)) == speech) >= (speech == LetterFormMath.Olde ? 300 : speech.StartsWith("sv:") ? 18 : 40), "lines enough in " + speech);
        // Each strange voice is reached by the writers it is written for, and only by them.
        foreach (var v in LetterCultureMath.Voices)
        {
            C(LetterCultureMath.StrangeXeno.Values.Contains(v) || LetterCultureMath.StrangeMemes.Values.Contains(v), v + " has writers");
            foreach (var (xeno, voice) in LetterCultureMath.StrangeXeno)
                C(LetterCultureMath.Read(xeno, "Human", null, new HashSet<string>()) == voice, $"{xeno} writes in {voice}");
        }
        C(LetterCultureMath.Read("Baseliner", "Human", new[] { "Collectivist" }, new HashSet<string>()) == null, "a baseliner of no strange culture writes plainly");
        C(LetterCultureMath.Read(null, null, null, null) == null, "nobody known: plain");
        var tags2 = new HashSet<string>();
        C(LetterCultureMath.Read("Neanderthal", "Human", new[] { "Raider", "Structure_TheistEmbodied" }, tags2) == null && tags2.SetEquals(new[] { "x:hulking", "c:raider", "c:pious" }), "a xenotype's kind and a culture's tags");
        C(LetterCultureMath.Read("VREA_AndroidBasic", "Human", new[] { "VME_Structure_ChthonianCult" }, new HashSet<string>()) == LetterCultureMath.Machine, "a xenotype's voice before a meme's");
        C(LetterCultureMath.Read("Baseliner", "Human", new[] { "VME_Gestalt" }, new HashSet<string>()) == LetterCultureMath.Hive, "a gestalt writes as a hive");
        // The machine's and the hive's own forms.
        var machine = Lines(LetterPart.Close).Where(l => LetterFormMath.SpeechOf(P(l)) == LetterCultureMath.Machine).ToList();
        C(machine.Count >= 3, "a machine closes its record");
        // The unschooled write their slips on purpose: a few to a barely schooled line, many to an unschooled one.
        var slips = new Regex(@"\b(yor|becos|wil|wiv|ritin|rite|leter|letter letter|peple|peeple|freind|frend|nuthin|nothin|wot|wen|sed|thay|ther|gud|plese|pleese|kno|nite|enuf|sumthing|tuff|wurd|wurds|sory|dont|cant|wont|gold|gould|goald)\b|\(crossed out\)", RegexOptions.IgnoreCase);
        foreach (var (part, line) in Ours().Where(x => LetterFormMath.SpeechOf(P(x.line)) == LetterFormMath.NoSchool))
            C(slips.Matches(P(line).text).Count >= 1 || P(line).text.Length < 25 || char.IsLower(P(line).text[0]), "an unschooled line shows it (a slip or no capital): " + P(line).text);
        C(Ours().Count(x => LetterFormMath.SpeechOf(P(x.line)) == LetterFormMath.NoSchool && P(x.line).text.Contains("(crossed out)")) >= 3, "a word crossed out and written again");
    }

    // ================= Medieval English, and the plain kept apart =================

    private static readonly Regex Archaic = new Regex(@"\b(thee|thou|thy|thine|hast|hath|doth|dost|ye|thyself|wouldst|shouldst|canst|shalt|mayst|art thou|thou art)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Modern = new Regex(@"\b(you|your|yours|yourself|yourselves)\b", RegexOptions.IgnoreCase);

    private static void Medieval(Action<bool, string> C)
    {
        foreach (var (part, line) in Ours())
        {
            var p = P(line);
            string speech = LetterFormMath.SpeechOf(p);
            string text = p.text;
            if (speech == LetterFormMath.Olde)
            {
                C(!Modern.IsMatch(text), $"a medieval line keeps to thee and thou ({part}): {text}");
                C(!Regex.IsMatch(text, @"\bthy\s+[aeiouAEIOU]"), $"thine before a vowel ({part}): {text}");
                // Thine before a consonant only where it stands alone ("was thine", "of thine"), not before a noun.
                C(!Regex.IsMatch(text, @"(?<!\b(of|was|is|be|are|were)\s)\bthine\s+[^aeiouhAEIOUH\s]"), $"thy before a consonant ({part}): {text}");
                C(!Regex.IsMatch(text, @"\bthou\s+(is|are|has|have|was|do|does|will|were)\b", RegexOptions.IgnoreCase), $"thou art, hast, dost, wilt ({part}): {text}");
                C(!Regex.IsMatch(text, @"\b(thee|thy|thine)\s+(art|hast|dost|wilt|shalt|canst)\b", RegexOptions.IgnoreCase), $"thou, not thee, as the subject ({part}): {text}");
                C(!Regex.IsMatch(text, @"\b(he|she|it)\s+(hast|dost|art)\b", RegexOptions.IgnoreCase), $"he hath, she doth ({part}): {text}");
                C(!Regex.IsMatch(text, @"\b(I|we)\s+(hath|doth|hast|dost)\b"), $"I have, we have ({part}): {text}");
            }
            else C(!Archaic.IsMatch(text), $"no thee or thou in a plain line ({part}): {text}");
        }
        // Medieval letters vary as much as the rest: every part has its own medieval lines, for every purpose and tone.
        foreach (var part in new[] { LetterPart.Salute, LetterPart.Open, LetterPart.Aside, LetterPart.Close, LetterPart.PS, LetterPart.Heart, LetterPart.Ours })
            C(Lines(part).Count(l => LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde) >= (part == LetterPart.Heart ? 100 : part == LetterPart.Ours ? 40 : 30),
                $"medieval {part} lines: {Lines(part).Count(l => LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde)}");
        C(Lines(LetterPart.Aside).Count(l => LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde && Has(P(l), "joke")) >= 12, "medieval jokes");
    }

    // ================= The writers' kinds, against the player's mods =================

    private static void Culture(string root, Action<bool, string> C)
    {
        var dir = Path.Combine(root, "docs", "player-mods");
        HashSet<string> Active(string file, Func<string[], bool> also = null)
        {
            var set = new HashSet<string>();
            foreach (var line in File.ReadLines(Path.Combine(dir, file)).Skip(1))
            {
                var cols = line.Split('\t');
                if (cols.Length > 2 && cols[2] == "yes" && (also == null || also(cols))) set.Add(cols[0]);
            }
            return set;
        }
        var xenos = Active("xenotypes.tsv");
        var races = Active("races.tsv", c => c.Length > 4 && c[4] == "humanlike");
        var memes = Active("memes.tsv");
        var factions = Active("factions.tsv");
        var traits = Active("traits.tsv");
        foreach (var (name, kind) in LetterCultureMath.Names())
        {
            var set = kind == "xenotype" ? xenos : kind == "race" ? races : kind == "meme" ? memes : kind == "faction" ? factions : traits;
            C(set.Contains(name), $"{kind} {name} is one of the player's active defs");
        }
        // Every flavour tag a table gives has lines that draw on it, and every x: or c: tag a line names comes from a table.
        var given = new HashSet<string>(LetterCultureMath.XenoTags.Values.SelectMany(x => x).Concat(LetterCultureMath.MemeTags.Values.SelectMany(x => x)));
        var used = new HashSet<string>(Ours().SelectMany(x => P(x.line).tags ?? Array.Empty<string>()).SelectMany(t => t.Split('|')).Where(t => t.StartsWith("x:") || t.StartsWith("c:")));
        foreach (var t in given) C(used.Contains(t), "lines for " + t);
        foreach (var t in used) C(given.Contains(t), "a line's culture tag comes from a table: " + t);
    }

    // ================= Shapes =================

    private static string Letter(LetterSpec s, int seed, List<(LetterPart, string)> drawn = null)
    {
        var r = new Random(seed);
        string P2(LetterPart part, HashSet<string> tags)
        {
            string line = LetterFormMath.Pick(Lines(part), tags, s.values, null, () => (float)r.NextDouble());
            if (line == null) return null;
            string kind = tags.Contains("joke") ? "joke" : tags.Contains("serious") ? "serious" : tags.Contains("everyday") ? "everyday" : "";
            drawn?.Add((part, kind + "|" + line));
            return Texts.Fill(line, s.values);
        }
        return LetterFormMath.Compose(s, new LetterWords(), P2, () => (float)r.NextDouble()).full;
    }

    private static void Shapes(Action<bool, string> C)
    {
        var salutes = new HashSet<string>(Lines(LetterPart.Salute).Select(l => Texts.Fill(P(l).text, Base())));
        var closes = new HashSet<string>(Lines(LetterPart.Close).Select(l => Texts.Fill(P(l).text, Base())));
        for (int seed = 0; seed < 300; seed++)
        {
            var tone = LetterFormMath.Tones[seed % 5];
            var s = Spec(false, LetterFormMath.TheirPurposes[seed % LetterFormMath.TheirPurposes.Length], tone, LetterFormMath.Ages[seed % 4], LetterFormMath.Motives[seed % 6],
                LetterFormMath.Addresses[seed % 5]);
            s.heart = "Our words, which are what the letter is for.";
            s.outcomes.Add("Dagna thinks better of the colony for it.");
            s.outcomes.Add("The messenger has set down 40 silver with the letter.");
            s.sign = LetterFormMath.Signature(SignStyle.Person, new LetterWords(), s.values);
            var made = LetterFormMath.Compose(s, new LetterWords(), (part, tags) => Pick(part, tags, s.values, new Random(seed)) is string l ? Texts.Fill(l, s.values) : null,
                () => (float)new Random(seed * 7 + 1).NextDouble());
            C(!made.letter.Contains("thinks better of the colony") && !made.letter.Contains("set down 40 silver"), "the outcomes are never inside the writer's words");
            C(made.full.StartsWith(made.letter) && made.full.EndsWith(made.note) && made.full.Contains("\n\n* * *\n\n"), "the narrator's lines come after the letter, set apart");
            C(made.letter.Contains(s.heart), "the heart is in the letter");
            C(made.letter.Contains(s.sign), "the letter is signed");
            // A call: a transcript, never a letter.
            s.shape = LetterShape.Call;
            s.us = "Stace";
            s.them = "Dagna";
            s.callHead = "Comms log, Riverhold to Ashford";
            s.callMode = seed % 3 == 0 ? "call" : seed % 3 == 1 ? "back" : "word";
            var call = LetterFormMath.Compose(s, new LetterWords(), (part, tags) => Pick(part, tags, s.values, new Random(seed)) is string l ? Texts.Fill(l, s.values) : null, () => 0.1f);
            var callLines = call.letter.Split('\n');
            C(callLines[0] == s.callHead && callLines.Skip(1).All(l => l.StartsWith("Stace: ") || l.StartsWith("Dagna: ")), "a call reads as a transcript: " + call.letter);
            C(!callLines.Any(l => salutes.Contains(l) || closes.Contains(l) || l.StartsWith("P.S.")) && !call.letter.Contains(s.sign), "a call has no salutation, closing, signature or P.S.");
            C(!call.letter.Contains("thinks better of the colony"), "a call's outcomes are under it too");
            if (s.callMode == "word") C(callLines.Skip(1).All(l => l.StartsWith("Dagna: ")), "left word: their voice alone");
            // Words passed on: the carrier's frame round the sender's words.
            s.shape = LetterShape.Spoken;
            var spoken = LetterFormMath.Compose(s, new LetterWords(), (part, tags) => Pick(part, tags, s.values, new Random(seed)) is string l ? Texts.Fill(l, s.values) : null, () => 0.1f);
            C(spoken.letter.Contains("\"" + s.heart + "\"") && !spoken.letter.Contains("P.S.") && !spoken.letter.Contains(s.sign)
              && !spoken.letter.Split('\n').Any(l => salutes.Contains(l) || closes.Contains(l)), "words passed on are not shaped as a letter: " + spoken.letter);
        }
        // The machine's record and the hive's many.
        var m = Spec(false, "greetings", AnswerTone.Cordial, speech: LetterCultureMath.Machine);
        m.heart = "Your letter was received.";
        m.date = "5501.2.05";
        m.place = "Ashford";
        m.sign = "Dagna";
        string ml = Letter(m, 3);
        C(ml.StartsWith("RECORD 5501.2.05") && Regex.IsMatch(ml, @"\n1\. ") && ml.Contains("2. Your letter was received."), "a machine writes a record with numbered points: " + ml);
        var h = Spec(false, "greetings", AnswerTone.Cordial, speech: LetterCultureMath.Hive);
        h.heart = "We read.";
        h.sign = LetterFormMath.Signature(SignStyle.Many, new LetterWords(), h.values);
        string hl = Letter(h, 4);
        C(hl.EndsWith("The many of Ashford") && !salutes.Any(x => hl.StartsWith(x)), "a hive signs as its many and greets nobody: " + hl);
    }

    // ================= Whole letters =================

    private static List<string> Quote(string purpose, string tone, params string[] more)
    {
        var tags = new HashSet<string>(more) { purpose, tone, "letter" };
        return pools["Rimfolk_Pool_NeighbourWrites"].Where(l => LineTags.Match(P(l), tags) > 0 && Has(P(l), purpose) && Has(P(l), tone)).ToList();
    }

    private static void Samples(string root, Action<bool, string> C, bool print)
    {
        var w = new LetterWords();
        var shown = new List<(string title, string text)>();
        string Make(string title, LetterSpec s, Func<string, bool> want, int tries = 400)
        {
            for (int seed = 1; seed < tries; seed++)
            {
                var drawn = new List<(LetterPart, string)>();
                string t = Letter(s, seed, drawn);
                if (want == null || want(string.Join("\n", drawn.Select(d => d.Item1 + "|" + d.Item2)) + "\n" + t))
                {
                    shown.Add((title, t));
                    return t;
                }
            }
            C(false, "a sample could be made: " + title);
            return "";
        }
        string Fill(string line, LetterSpec s) => Texts.Fill(P(line).text, s.values);
        // A warm letter with a joke.
        var warm = Spec(false, "greetings", AnswerTone.Warm, WorldAge.Industrial, LetterMotive.Friend);
        warm.values["WRITER"] = "Stace";
        warm.values["NAME"] = "Dagna";
        warm.place = "Ashford";
        warm.date = LetterFormMath.Date(DateStyle.Plain, w, 5, "Aprimay", 0, 5501, null);
        warm.heart = Fill(Quote("greetings", "warm").First(l => !Has(P(l), "call") && !Has(P(l), "theirLowtech")), warm);
        warm.sign = LetterFormMath.Signature(SignStyle.Person, w, warm.values);
        warm.outcomes.Add("Dagna thinks better of the colony for it: civil before, friendly now. Of Stace, who wrote, Dagna thinks better still.");
        Make("A warm letter with a joke", warm, d => d.Contains("Aside|") && d.Contains("joke"));
        // A serious condolence.
        var condol = Spec(false, "condolences", AnswerTone.Cordial, WorldAge.Industrial);
        condol.values["TOPIC"] = "the death of Ennis, their prior";
        condol.values["WRITER"] = "Stace";
        condol.place = "Ashford";
        condol.date = LetterFormMath.Date(DateStyle.Plain, w, 11, "Septober", 2, 5501, null);
        condol.heart = Fill(Quote("condolences", "cordial").First(l => !Has(P(l), "call") && !P(l).tags.Any(t => t.StartsWith("tie:"))), condol);
        condol.sign = LetterFormMath.Signature(SignStyle.Person, w, condol.values);
        condol.outcomes.Add("The Rust Kin will remember that the colony mourned with them.");
        condol.outcomes.Add("Dagna thinks a shade better of the colony for it, and is still civil.");
        string c = Make("A serious condolence", condol, d => d.Contains("Aside|") && d.Contains("serious"));
        C(!Regex.IsMatch(c, "goat|mule|frog"), "a condolence has no joke");
        // A curt refusal.
        var curt = Spec(false, "help", AnswerTone.Refusal, WorldAge.Industrial, LetterMotive.Wary);
        curt.values["HELP"] = "medicine";
        curt.heart = Fill(Quote("help", "refusal").First(l => !Has(P(l), "theirLowtech") && !P(l).tags.Any(t => t.StartsWith("tie:"))), curt);
        curt.sign = LetterFormMath.Signature(SignStyle.Person, w, curt.values);
        curt.outcomes.Add("Dagna sends nothing.");
        curt.outcomes.Add("Hard times at Ashford leave them little to spare.");
        curt.outcomes.Add("Dagna thinks a shade less of the colony for it, and is still cool.");
        Make("A curt refusal", curt, null);
        // A letter at war.
        var war = Spec(false, "truce", AnswerTone.Cool, WorldAge.Tribal, LetterMotive.Plain, LetterFormMath.AddrWar);
        war.values["TOWN"] = "Grey Hollow";
        war.values["FROM"] = "Okoa";
        war.values["TITLE"] = "chief";
        war.heart = Fill(Quote("truce", "cool").FirstOrDefault() ?? "[truce] We will think on it.", war);
        war.sign = LetterFormMath.Signature(SignStyle.Person, w, war.values);
        war.outcomes.Add("A messenger of a people at peace with the colony carried Stace's letter under a white flag, and has brought back the answer.");
        war.outcomes.Add("Their people are at war with the colony, and the answer shows it.");
        Make("A letter at war", war, d => d.Contains("Aside|") || d.Contains("PS|"));
        // The colony's own: a blunt writer and an eloquent one.
        var blunt = Spec(true, "warning", AnswerTone.Cordial, WorldAge.Industrial, LetterMotive.Plain, voice: VoiceMain.Blunt);
        blunt.values["FROM"] = "Bautista";
        blunt.values["TO"] = "Dagna";
        blunt.values["TOPIC"] = "the raiders of the Rust Kin";
        blunt.voice.abrasive = true;
        blunt.place = "Riverhold";
        blunt.date = LetterFormMath.Date(DateStyle.Plain, w, 3, "Jugust", 1, 5501, null);
        // The writer's own voice: one of the blunt lines for a warning.
        blunt.heart = Fill(LetterFormMath.Pick(Lines(LetterPart.Ours).Where(l => Has(P(l), "v:blunt")).ToList(), LetterFormMath.HeartTags(blunt), blunt.values, null, () => 0.9f), blunt);
        blunt.sign = LetterFormMath.Signature(SignStyle.Ours, w, blunt.values);
        Make("The colony's blunt writer", blunt, null);
        var eloquent = Spec(true, "invitation", AnswerTone.Warm, WorldAge.Industrial, LetterMotive.Friend, voice: VoiceMain.Eloquent);
        eloquent.values["FROM"] = "Hessa";
        eloquent.values["TO"] = "Dagna";
        eloquent.values["DAY"] = "Harvest Day";
        eloquent.tags.Add("colony:daysoon");
        eloquent.tags.Add("season:autumn");
        eloquent.tags.Add("colony:harvest");
        eloquent.voice.kind = true;
        eloquent.place = "Riverhold";
        eloquent.date = LetterFormMath.Date(DateStyle.Plain, w, 9, "Septober", 2, 5501, null);
        eloquent.heart = Fill(LetterFormMath.Pick(Lines(LetterPart.Ours).Where(l => Has(P(l), "v:eloquent")).ToList(), LetterFormMath.HeartTags(eloquent), eloquent.values, null, () => 0.6f),
            eloquent);
        eloquent.sign = LetterFormMath.Signature(SignStyle.Ours, w, eloquent.values);
        Make("The colony's eloquent writer", eloquent, d => d.Contains("Aside|"));
        // The Empire.
        var empire = Spec(false, "greetings", AnswerTone.Warm, WorldAge.Spacer, LetterMotive.Duty, LetterFormMath.AddrEmpire);
        empire.values["FROM"] = "Ilsabet Varro";
        empire.values["TITLE"] = "Baroness";
        empire.values["TOWN"] = "Coldmere";
        empire.place = "Coldmere";
        empire.date = LetterFormMath.Date(DateStyle.Courtly, w, 7, "Decembary", 3, 5501, null);
        empire.heart = Fill(Quote("greetings", "warm", "theirHightech").First(l => Has(P(l), "theirHightech")), empire);
        empire.sign = LetterFormMath.Signature(SignStyle.Noble, w, empire.values);
        empire.outcomes.Add("Ilsabet Varro thinks a good deal better of the colony for it, and is still friendly.");
        Make("An Empire letter", empire, null);
        // A medieval letter.
        var med = Spec(false, "news", AnswerTone.Cordial, WorldAge.Medieval, LetterMotive.Plain, speech: LetterFormMath.Olde);
        med.values["FROM"] = "Aldous";
        med.values["TITLE"] = "reeve";
        med.values["TOWN"] = "Wendham";
        med.place = "Wendham";
        med.date = LetterFormMath.Date(DateStyle.Courtly, w, 2, "Jugust", 1, 5501, null);
        med.heart = Fill(LetterFormMath.Pick(Lines(LetterPart.Heart), LetterFormMath.HeartTags(med), med.values, null, () => 0.4f), med);
        med.sign = LetterFormMath.Signature(SignStyle.Person, w, med.values);
        med.outcomes.Add("What Aldous tells of:\n  - bandits camped at the old mill, two days north of Wendham");
        string ml = Make("A medieval letter", med, d => d.Contains("Aside|"));
        C(Regex.IsMatch(ml, @"\b(thee|thou|thy|thine)\b") && !Modern.IsMatch(ml.Split("* * *")[0]), "a medieval letter writes thee and thou");
        // A bandit's demand.
        var bandit = Spec(false, "ransom", AnswerTone.Curt, WorldAge.Spacer, LetterMotive.Plain, LetterFormMath.AddrWar, LetterFormMath.NoSchool);
        bandit.values["FROM"] = "Knuckles";
        bandit.values["CAPTIVE"] = "Wil";
        bandit.heart = Fill(LetterFormMath.Pick(Lines(LetterPart.Heart), LetterFormMath.HeartTags(bandit), bandit.values, null, () => 0.2f), bandit);
        bandit.sign = LetterFormMath.Signature(SignStyle.Mark, w, bandit.values);
        bandit.outcomes.Add("Their people are at war with the colony, and the answer shows it.");
        Make("A bandit's demand", bandit, null);
        // Strange voices: an android and a fairy.
        var android = Spec(false, "pact", AnswerTone.Cordial, WorldAge.Spacer, LetterMotive.Duty, speech: LetterCultureMath.Machine);
        android.values["FROM"] = "Unit Ceres-4";
        android.values["PURPOSE"] = "a pact";
        android.values["WRITER"] = "Stace";
        android.place = "Hollowmere";
        android.date = LetterFormMath.Date(DateStyle.Standard, w, 14, "Aprimay", 0, 5502, null);
        android.heart = Fill(Quote("pact", "cordial").FirstOrDefault() ?? "[pact] We accept.", android);
        android.sign = "Unit Ceres-4, Hollowmere";
        android.outcomes.Add("The pact is sworn when their answer is in.");
        Make("An android's letter", android, d => d.Contains("Aside|"));
        var fey = Spec(false, "invitation", AnswerTone.Warm, WorldAge.Medieval, LetterMotive.Plain, speech: LetterCultureMath.Fey);
        fey.values["FROM"] = "Thistledown";
        fey.values["TOWN"] = "the Ring of Moss";
        fey.heart = Fill(Quote("invitation", "warm").First(l => !Has(P(l), "call")), fey);
        fey.sign = LetterFormMath.Signature(SignStyle.PersonUntitled, w, fey.values);
        fey.outcomes.Add("Thistledown will come to visit before long.");
        Make("A fairy's letter", fey, null);
        foreach (var (title, text) in shown)
        {
            C(text.Length > 60 && !text.Contains("{") && !text.Contains("}"), "a sample is whole and filled: " + title);
            if (print) Console.WriteLine("=== " + title + " ===\n" + text + "\n");
        }
    }

    // ================= The seams =================

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string form = Code("Letters/LetterForm.cs"), book = Code("Letters/LetterBook.cs"), window = Code("Letters/LetterReadWindow.cs"), nl = Code("World/NeighbourLetters.cs");
        C(nl.Contains("LetterForm.Answer(") && nl.Contains("LetterForm.Full"), "an answer is made on the letter's form when the letters are written out in full");
        C(nl.Contains("LetterForm.Ours(") && form.Contains("LetterBook.Written(") && form.Contains("LetterBook.Answered("), "the colony's letter is composed and kept as it is written, and so is their answer");
        C(form.Contains("LetterFormMath.Compose(") && form.Contains("LetterCultureMath.Read(") && form.Contains("LetterFormMath.SchoolingOf(") && form.Contains("LetterFormMath.Motive("),
            "the game side builds the spec from the writer: their culture, schooling and motive");
        C(form.Contains("Flavor.Memory") || form.Contains("LineMemory"), "lines don't come round again soon");
        C(book.Contains("class LetterBook : GameComponent") && book.Contains("Scribe_Collections.Look(ref exchanges") && book.Contains("LetterFormMath.MaxKept"), "the exchanges are kept, capped");
        C(Code("UI/Tell.cs").Contains("LetterBook.Told("), "a letter told is matched to the exchange it ends");
        C(Code("UI/EventCards/EventCardModel.cs").Contains("LetterBook.ReadAct("), "the answer's card offers to read the colony's letter");
        C(Code("UI/Region/PeoplePage.cs").Contains("LetterBook.With(") && Code("UI/Region/PeoplePage.cs").Contains("LetterReadWindow"), "the people page lists the correspondence");
        C(window.Contains("class LetterReadWindow : FramedWindow") && window.Contains("base(\"LetterRead\"") && Code("UI/FramedWindowsMath.cs").Contains("new FrameKey(\"LetterRead\""),
            "the reading window is on the shared frame");
        C(Code("Post/Building_Mailbag.cs").Contains("LetterReadWindow"), "the mailbag offers its letters to read");
        C(Code("CouncilSettings.cs").Contains("public bool lettersInFull = true") && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"lettersInFull\""), "the setting, on");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Letters/LetterBook.cs:exchanges"), "the kept letters are modelled in the save estimate");
        foreach (var part in Enum.GetValues(typeof(LetterPart)).Cast<LetterPart>())
            C(pools.ContainsKey(LetterFormMath.PoolOf(part)), "the pool " + LetterFormMath.PoolOf(part));
    }
}
