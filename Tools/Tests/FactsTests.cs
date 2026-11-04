using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The facts service (Text/Facts/) and the one fill (Text/Fill.cs). The scenes ask for the outsider talks' sections in
/// the order they were always read; the providers' small rules (how current, the ties, the history, the ages, the old
/// era rule for their people); and that every text system chooses as it did: over many generated talks, the facts the
/// new providers add change no piece, line or roll; the fill gives exactly what the talks' own fill and the chains of
/// Replace gave, over every text in the defs; and the letters' quote matches its lines as before.
/// </summary>
public static class FactsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "facts: " + what);
        }

        Scenes(root, C);
        Rules(C);
        var pieces = Pieces(root, out var texts);
        Talks(pieces, C);
        return n;
    }

    // ---------------- Scenes ----------------

    /// <summary>The outsider talks' sections in the order OutsiderFacts read them before the facts service.</summary>
    private static readonly string[] OldSections =
    {
        "moment", "people", "persons", "pair", "town", "land", "notables", "places", "wars", "situations", "conditions", "colony", "hospital", "deeds", "marks"
    };

    private static void Scenes(string root, Action<bool, string> C)
    {
        var talk = FactsMath.Talk;
        C(talk.Take(OldSections.Length).SequenceEqual(OldSections), "a talk asks for the outsider talks' sections first, in their old order: " + string.Join(",", talk));
        C(talk.Skip(OldSections.Length).SequenceEqual(new[] { FactsMath.Ages, FactsMath.Ties, FactsMath.History }), "then the ages, the ties and the history");
        C(talk.Distinct().Count() == talk.Length, "each provider once in a talk");
        C(FactsMath.Letter(false).SequenceEqual(new[] { FactsMath.Ties, FactsMath.History }) && FactsMath.Letter(true).SequenceEqual(new[] { FactsMath.News, FactsMath.Ties, FactsMath.History }),
            "a letter asks for the ties and the history, and the news when it tells some");
        // Every provider a scene can name is in the registry (Facts.cs).
        string facts = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Text", "Facts", "Facts.cs"));
        var names = typeof(FactsMath).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToList();
        foreach (var field in names)
            C(facts.Contains("[FactsMath." + field.Name + "] ="), $"the provider {field.GetValue(null)} is registered");
        C(names.Select(f => (string)f.GetValue(null)!).Distinct().Count() == names.Count, "providers' names are distinct");
    }

    // ---------------- The providers' rules ----------------

    private static void Rules(Action<bool, string> C)
    {
        // How current: the old formula (Mathf.Max(0.4, top * (1 - Clamp01(days / Max(0.1, span))))).
        C(FactsMath.Fresh(0f, 30f, 2.5f) == 2.5f && FactsMath.Fresh(30f, 30f, 2.5f) == 0.4f && FactsMath.Fresh(99f, 30f, 2.5f) == 0.4f
          && Math.Abs(FactsMath.Fresh(15f, 30f, 2f) - 1f) < 1e-6f && FactsMath.Fresh(-1f, 10f, 3f) == 3f && FactsMath.Fresh(1f, 0f, 2f) == 0.4f,
            "how current: the top when new, fading over its span to 0.4");

        // The ages: their people's tags exactly as the old rule from their tech level (0 undefined to 7 archotech).
        for (int tech = 0; tech <= 7; tech++)
        {
            var old = new HashSet<string>
            {
                tech <= 2 ? "their:tribal" : tech == 3 ? "their:medieval" : tech == 4 ? "their:industrial" : "their:spacer",
                tech <= 3 ? "their:lowtech" : "their:hightech"
            };
            foreach (var ours in AgesMath.All)
            foreach (bool empire in new[] { false, true })
            {
                var f = new FactSet();
                var theirs = LedgerMath.AgeOf(tech);
                FactsMath.AddAges(f, ours, theirs, empire);
                var era = f.tags.Where(t => t != "their:sameage" && t != "their:behind" && t != "their:ahead" && t != "their:empire").ToHashSet();
                C(era.SetEquals(old) && f.tags.Contains("their:empire") == empire && f.heat.Count == 0, $"their people's age tags as before (tech {tech}, empire {empire})");
                string rel = theirs == ours ? "their:sameage" : theirs < ours ? "their:behind" : "their:ahead";
                C(f.tags.Contains(rel) && new[] { "their:sameage", "their:behind", "their:ahead" }.Count(f.tags.Contains) == 1, $"their age beside ours: {rel} ({theirs}, ours {ours})");
            }
        }

        // The ties.
        var keys = FactsMath.TieKinds.Select(FactsMath.TieKey).ToList();
        C(keys.All(k => !string.IsNullOrEmpty(k) && Regex.IsMatch(k, "^[A-Z]+$")) && keys.Distinct().Count() == keys.Count,
            "each tie kind names its other party by a placeholder of its own (no underscore: not a pawn's)");
        C(FactsMath.TieKinds.Select(FactsMath.TieTag).SequenceEqual(new[] { "tie:favour", "tie:promise", "tie:claim", "tie:debt", "tie:fealty", "tie:grudge" }),
            "tie tags: favour, promise, claim, debt, fealty, grudge");
        C(!FactsMath.Speaks(Tie.Kind.Regard) && FactsMath.TieKey(Tie.Kind.Regard) == null, "a regard is no tie the texts speak of (it's in the pair's and notables' facts)");
        var ft = new FactSet();
        FactsMath.AddTie(ft, new TieFact { kind = Tie.Kind.Favour, oursBound = false, you = true, days = 2f, toName = "Ana" });
        FactsMath.AddTie(ft, new TieFact { kind = Tie.Kind.Debt, oursBound = true, calledIn = true, days = float.MaxValue, toName = "the Iron Hand" });
        C(ft.tags.SetEquals(new[] { "tie:favour", "tie:favour:theirs", "tie:favour:you", "tie:debt", "tie:debt:ours", "tie:debt:due" }),
            "a favour they owe the speaker, a debt the colony owes them and is asked for now: " + string.Join(",", ft.tags));
        C(ft.values["FAVOURFOR"] == "Ana" && ft.values["DEBTTO"] == "the Iron Hand" && ft.heat["tie:favour"] > ft.heat["tie:debt"] && ft.heat["tie:debt"] == 1f,
            "each names the party it is toward; a new one is more current");
        FactsMath.AddTie(ft, new TieFact { kind = Tie.Kind.Regard, oursBound = true, toName = "x" });
        C(!ft.tags.Any(t => t.StartsWith("tie:regard")), "a regard adds nothing");
        var a = new TieFact { you = false, days = 1f };
        var b = new TieFact { you = true, days = 50f };
        C(FactsMath.Before(b, a) && !FactsMath.Before(a, b) && FactsMath.Before(new TieFact { days = 1f }, new TieFact { days = 2f }),
            "of two ties of a kind: the one the speaker is party to, then the newer");

        // The history.
        var fh = new FactSet();
        FactsMath.AddLately(fh, "town", new[] { ("world:taken", 3f), ("world:began", 20f), ("deed:traded", 45f), ((string)null!, 1f) });
        C(fh.tags.SetEquals(new[] { "town:lately", "town:lately:world", "town:lately:world:taken", "town:lately:world:began" }),
            "what happened at a town lately, by kind and family; older than thirty days left out: " + string.Join(",", fh.tags));
        C(fh.heat["town:lately"] == FactsMath.Fresh(3f, FactsMath.LatelyDays, 1.5f) && fh.heat["town:lately:world"] == fh.heat["town:lately"]
          && fh.heat["town:lately:world:began"] < fh.heat["town:lately:world:taken"], "the newer the more current");
        var none = new FactSet();
        FactsMath.AddLately(none, "them", new[] { ("tie:favour:made", 31f) });
        C(none.tags.Count == 0, "nothing lately: no tag");
    }

    // ---------------- The library ----------------

    /// <summary>Every conversation piece, taken apart as the game does, and every text in them (lines and news heard).</summary>
    internal static List<TalkPiece> Pieces(string root, out List<string> texts)
    {
        var pieces = new List<TalkPiece>();
        texts = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "OutsiderTalkDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.OutsiderTalkDef"))
            {
                var lines = d.Element("lines")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
                float w = float.TryParse(d.Element("weight")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wv) ? wv : 1f;
                var p = OutsiderTalkMath.Parse(d.Element("defName")!.Value, d.Element("topic")?.Value, d.Element("uses")?.Value, w, d.Element("when")?.Value, lines,
                    d.Element("heard")?.Value, d.Element("mark")?.Value, d.Element("about")?.Value, null);
                pieces.Add(p);
                foreach (var t in p.turns)
                    foreach (var l in t)
                        texts.Add(l.text);
                if (p.heard != null) texts.Add(p.heard);
            }
        return pieces;
    }

    // ---------------- The talks choose as they did ----------------

    /// <summary>The tags only the new providers (ages beside ours, the ties, the history) give.</summary>
    private static bool NewTag(string t) =>
        t.StartsWith("tie:") || t.StartsWith("town:lately") || t.StartsWith("them:lately") || t == "their:sameage" || t == "their:behind" || t == "their:ahead";

    private static bool NewValue(string key) => FactsMath.TieKinds.Any(k => FactsMath.TieKey(k) == key);

    /// <summary>A piece names one of the new facts in its conditions or a line's.</summary>
    private static bool NamesNew(TalkPiece p)
    {
        IEnumerable<string> Names(LineTags.Parsed parsed) => parsed?.alts == null ? Enumerable.Empty<string>() : parsed.alts.SelectMany(x => x).Select(x => x.name);
        return Names(p.when).Concat(p.turns.SelectMany(t => t).SelectMany(l => Names(l.tags))).Any(NewTag)
               || p.turns.SelectMany(t => t).SelectMany(l => l.holes).Concat(p.heardHoles).Any(NewValue);
    }

    private static readonly string[] Tones = { "warm", "good", "plain", "cool", "sour" };
    private static readonly string[] Uses = { "chat", "home", "know" };
    private static readonly string[] NewKinds = { "world:taken", "world:began", "world:ended", "world:vassal", "tie:favour:made", "tie:promise:kept", "deed:traded", "loss" };

    /// <summary>
    /// A talk as the old facts gave it, made up from a seed: a tone; their people's age by the old rule; a handful of
    /// what the game says, or the moment a piece is written for (its conditions' first alternatives, and its turns'); the
    /// colonist's and the outsider's own tags; values for most placeholders; earlier talks' marks with what became of them.
    /// </summary>
    private static FactSet OldTalk(int seed, List<TalkPiece> pieces, List<string> markKeys, out WorldAge theirs, out WorldAge ours, out bool empire)
    {
        var r = new Random(seed);
        var f = new FactSet();
        f.Tag("tone:" + Tones[r.Next(Tones.Length)]);
        int tech = r.Next(0, 8);
        theirs = LedgerMath.AgeOf(tech);
        ours = AgesMath.All[r.Next(AgesMath.All.Length)];
        empire = r.Next(5) == 0;
        f.Tag(tech <= 2 ? "their:tribal" : tech == 3 ? "their:medieval" : tech == 4 ? "their:industrial" : "their:spacer");
        f.Tag(tech <= 3 ? "their:lowtech" : "their:hightech");
        if (empire) f.Tag("their:empire");
        f.Tag(AgesMath.Tag(ours));
        foreach (var w in OutsiderTalkTests.Words)
            if (!NewTag(w) && !w.StartsWith("tone:") && !w.StartsWith("their:") && r.NextDouble() < 0.2)
                f.Tag(w, r.NextDouble() < 0.4 ? (float)(r.NextDouble() * 3.0) : 0f);
        void Positive(LineTags.Parsed parsed, Action<string> add)
        {
            if (parsed?.alts == null) return;
            foreach (var alts in parsed.alts)
            {
                var first = alts.FirstOrDefault(x => !x.neg);
                if (first.raw != null) add(first.name);
            }
        }
        // The moment a piece is written for: an ordinary one, or a callback with the earlier talk it picks up.
        MarkFacts callback = null;
        if (r.NextDouble() < 0.7)
        {
            var p = pieces[r.Next(pieces.Count)];
            if (r.NextDouble() < 0.4)
            {
                var cbs = pieces.Where(x => x.callback).ToList();
                p = cbs[r.Next(cbs.Count)];
            }
            if (p.callback) callback = new MarkFacts { subject = Word(r), fact = "ws:" + r.Next(9) };
            Positive(p.when, t =>
            {
                if (callback != null && (t.StartsWith("talked:") || t.StartsWith("then:")))
                {
                    if (t.StartsWith("talked:")) callback.key = t.Substring(7);
                    callback.tags.Add(t);
                    callback.heat[t] = r.NextDouble() < 0.5 ? 3f : 1f;
                }
                else f.Tag(t, r.NextDouble() < 0.5 ? 2f : 0f);
            });
            foreach (var turn in p.turns)
                Positive(turn[r.Next(turn.Count)].tags, t => (r.Next(2) == 0 ? f.usTags : f.themTags).Add(t));
        }
        foreach (var fam in new[] { "ws:Council_WS_Plague", "deed:released", "deedbest:released", "condition:ToxicFallout", "last:war", "trait:Kind", "skill:Cooking" })
            if (r.NextDouble() < 0.3)
                (fam.StartsWith("trait:") || fam.StartsWith("skill:") ? r.Next(2) == 0 ? f.usTags : f.themTags : f.tags).Add(fam);
        foreach (var h in OutsiderTalkTests.Placeholders)
            if (!NewValue(h) && h != "THEN" && h != "NOW" && r.NextDouble() < 0.75)
                f.Value(h, Word(r), r.NextDouble() < 0.2 ? "x:" + r.Next(9) : null);
        int marks = r.Next(0, 4);
        for (int i = 0; i < marks && markKeys.Count > 0; i++)
        {
            var m = new MarkFacts { key = markKeys[r.Next(markKeys.Count)], subject = Word(r), fact = "ws:" + r.Next(9) };
            m.tags.Add("talked:" + m.key);
            m.heat["talked:" + m.key] = 1f;
            f.tags.Add("talked:" + m.key);
            foreach (var t in new[] { "then:still", "then:over", "then:dead", "then:changed", "then:recent", "then:long", "then:success", "then:failed" })
                if (r.NextDouble() < 0.25)
                {
                    m.tags.Add(t);
                    m.heat[t] = (float)(r.NextDouble() * 3.0);
                }
            m.values["THEN"] = m.subject;
            if (r.NextDouble() < 0.5) m.values["NOW"] = Word(r);
            f.marks.Add(m);
        }
        if (callback?.key != null)
        {
            f.tags.Add("talked:" + callback.key);
            callback.values["THEN"] = callback.subject;
            callback.values["NOW"] = Word(r);
            f.marks.Insert(r.Next(f.marks.Count + 1), callback);
        }
        return f;
    }

    /// <summary>The same talk with what the new providers add: their age beside ours, ties of every kind and side, what the history holds lately.</summary>
    private static void AddNew(FactSet f, int seed, WorldAge theirs, WorldAge ours, bool empire)
    {
        var r = new Random(seed * 31 + 7);
        FactsMath.AddAges(f, ours, theirs, empire);
        foreach (var k in FactsMath.TieKinds)
            if (r.NextDouble() < 0.5)
                FactsMath.AddTie(f, new TieFact
                {
                    kind = k, oursBound = r.Next(2) == 0, you = r.Next(3) == 0, calledIn = r.Next(4) == 0, days = (float)(r.NextDouble() * 40.0), toName = Word(r)
                });
        foreach (var who in new[] { "town", "them" })
            FactsMath.AddLately(f, who, Enumerable.Range(0, r.Next(0, 4)).Select(_ => (NewKinds[r.Next(NewKinds.Length)], (float)(r.NextDouble() * 40.0))).ToList());
    }

    private static readonly string[] Words =
        { "Kettleford", "the gray cough", "Ana", "Bo Reyes", "a long winter", "the Iron Hand", "sister", "medicine", "the old mill", "Lenlia", "an ox", "Tamsin" };

    private static string Word(Random r) => Words[r.Next(Words.Length)];

    private static string Show(TalkChoice ch) =>
        ch == null ? "none" : ch.piece.name + "|" + ch.mark?.key + "|" + string.Join("/", ch.lines.Select(l => (l.us ? "us:" : "them:") + l.text)) + "|" + ch.heard + "|" + ch.markSubject + "|" + ch.markFact;

    private static void Talks(List<TalkPiece> pieces, Action<bool, string> C)
    {
        // Today's pieces (and, later, any that names no new fact): their choices must not move when the new facts come.
        var old = pieces.Where(p => !NamesNew(p)).ToList();
        C(old.Count >= 150, $"the library's pieces that name no new fact ({old.Count} of {pieces.Count})");
        var markKeys = pieces.Where(p => p.mark != null).Select(p => p.mark!).Distinct().ToList();
        int talks = 0, chosen = 0, same = 0, rollsSame = 0, callbacks = 0;
        var firstDiff = "";
        for (int seed = 1; seed <= 1500; seed++)
        {
            var before = OldTalk(seed, pieces, markKeys, out var theirs, out var ours, out var empire);
            var after = OldTalk(seed, pieces, markKeys, out _, out _, out _);
            AddNew(after, seed, theirs, ours, empire);
            var r = new Random(seed);
            string use = Uses[r.Next(Uses.Length)];
            int maxLines = r.Next(2, 7);
            bool toneOnly = r.Next(3) == 0;
            var saidToThem = old.Where(_ => r.NextDouble() < 0.05).Select(p => p.name).ToList();
            var saidLately = old.Where(_ => r.NextDouble() < 0.05).Select(p => p.name).ToList();
            var topics = new List<string> { old[r.Next(old.Count)].topic };
            int rollSeed = r.Next();
            TalkChoice Run(FactSet f, out int rolls)
            {
                var dice = new Random(rollSeed);
                int count = 0;
                var ch = OutsiderTalkMath.Choose(old, f, use, maxLines, toneOnly, saidToThem, saidLately, topics, () =>
                {
                    count++;
                    return (float)dice.NextDouble();
                });
                rolls = count;
                return ch;
            }
            var a = Run(before, out int rollsA);
            var b = Run(after, out int rollsB);
            talks++;
            if (a != null) chosen++;
            if (a?.mark != null) callbacks++;
            if (Show(a) == Show(b)) same++;
            else if (firstDiff == "") firstDiff = $"seed {seed}: {Show(a)} / {Show(b)}";
            if (rollsA == rollsB) rollsSame++;
        }
        C(same == talks, $"over {talks} generated talks the new facts change no piece, mark, line or news heard ({same} the same{(firstDiff == "" ? "" : "; " + firstDiff)})");
        C(rollsSame == talks, $"and roll the dice as often ({rollsSame} of {talks})");
        C(chosen >= talks / 2 && callbacks >= talks / 50, $"the generated talks say something most of the time ({chosen} of {talks}, {callbacks} callbacks)");
    }
}
