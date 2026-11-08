using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The flavour line under each answer (Dilemmas/ImpactFlavourMath.cs): what it chooses to say, never against the way
/// things go, never a number or a mechanic's name, within its length, and its words the same for an answer every time
/// and different between the answers of one letter. The words come from the mod's own Keyed files.
/// </summary>
public static class ImpactFlavourTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "answer flavour: " + what);
        }

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var w = new HintWords(k => keyed.TryGetValue(k, out var v) ? v : null, keyed.ContainsKey);
        string M = ImpactMath.Mark;
        var banned = new[] { "spirits", "trust", "opinion", "mood", "goodwill", "recreation", "strain", "standards", "certainty", "xp", "%" };
        bool NoMechanic(string s) => !banned.Any(b => Regex.IsMatch(s.ToLowerInvariant(), b == "%" ? "%" : @"\b" + b + @"\b"));
        bool Clean(string s) => s != null && !s.Any(char.IsDigit) && !s.Contains('!') && !s.Contains('—') && !s.Contains("CC_") && !s.Contains('{') && NoMechanic(s);

        // ---------- The words: every family has its variants, each clean, each going the family's way ----------
        var kinds = (FlavourKind[])Enum.GetValues(typeof(FlavourKind));
        var main = new[]
        {
            FlavourKind.MoodUp, FlavourKind.MoodDown, FlavourKind.TrustUp, FlavourKind.TrustDown, FlavourKind.ThinkWorse, FlavourKind.ThinkBetter, FlavourKind.Fight,
            FlavourKind.Gamble, FlavourKind.GambleOn, FlavourKind.Cost, FlavourKind.Work, FlavourKind.Gather, FlavourKind.MapDone, FlavourKind.Lean, FlavourKind.ManyOverOne,
            FlavourKind.OneOverMany, FlavourKind.MoodUpBig, FlavourKind.MoodDownBig
        };
        int words = 0;
        var good = new[] { "glad", "please", "pleased", "welcome", "kindly", "brighten", "delighted", "overjoyed", "heart", "mean a lot", "grateful", "closer", "richer", "warm", "lift", "heard", "win", "better for it", "friendship", "favour" };
        var poor = new[] { "won't", "sting", "hard", "badly", "sour", "ill", "blame", "grudge", "against", "doubt", "let down", "slight", "smarting", "shadow", "worse", "lose", "trouble", "apart", "odds", "blows", "fight", "grumble", "weigh", "worn", "tense", "doubts", "shake", "waver", "toll", "price", "cost", "low", "thin", "wait", "gone", "parting", "uneasy", "bristle", "edge", "break", "coldly", "mutter" };
        bool Has(string s, string word) => Regex.IsMatch(s.ToLowerInvariant(), @"\b" + Regex.Escape(word) + @"\b");
        foreach (var k in kinds)
            foreach (bool many in ImpactFlavourMath.HasMany(k) ? new[] { false, true } : new[] { false })
            {
                string key = ImpactFlavourMath.KeyOf(k, many);
                int count = w.Variants(key);
                words += count;
                C(count >= (main.Contains(k) ? 6 : 3), $"{key}: enough ways to say it ({count})");
                for (int i = 1; i <= count; i++)
                {
                    string t = keyed[key + "_" + i];
                    string bare = Regex.Replace(t, @"\{(\d|WOULD|WONT)\}", "");
                    C(Clean(bare) && !bare.Contains('{'), $"{key}_{i}: no number, mechanic, exclamation or em dash, and only its own placeholders ({t})");
                    C(!t.TrimEnd().EndsWith("."), $"{key}_{i}: no full stop of its own ({t})");
                    C(t.Length <= 70, $"{key}_{i}: short ({t.Length})");
                    int dir = ImpactFlavourMath.Direction(k);
                    if (dir > 0) C(!poor.Any(p => Has(bare, p)), $"{key}_{i}: good news in good words ({t})");
                    if (dir < 0) C(!good.Any(p => Has(bare, p)), $"{key}_{i}: bad news in bad words ({t})");
                    // Words a family is made for: who, the other, when.
                    if (k == FlavourKind.ThinkBetter || k == FlavourKind.ThinkWorse || k == FlavourKind.Bond || k == FlavourKind.Rift || k == FlavourKind.Fight
                        || k == FlavourKind.ManyOverOne || k == FlavourKind.OneOverMany)
                        C(t.Contains("{0}") && t.Contains("{1}"), $"{key}_{i}: names both ({t})");
                }
            }
        C(words >= 250, $"many words in all ({words})");
        foreach (var k in new[] { FlavourKind.Gather, FlavourKind.MapDone })
            C(Enumerable.Range(1, w.Variants(ImpactFlavourMath.KeyOf(k, false))).Any(i => !keyed[ImpactFlavourMath.KeyOf(k, false) + "_" + i].Contains("{0}")),
                $"{k}: words for when it isn't known when");
        foreach (var k in keyed.Where(k => k.Key.StartsWith("CC_ImpactShows_Flavour") || k.Key.StartsWith("CC_SetNote_ImpactShows_Flavour")))
            C(!k.Value.Any(char.IsDigit) && !k.Value.Contains('—') && !k.Value.Contains('!'), $"{k.Key} is plain");

        // ---------- Lines to build answers from ----------
        ImpactLine Mood(string who, float size, float chance = 1f, bool each = false) => new ImpactLine
        {
            kind = ImpactKind.Mood, sign = size > 0 ? 1 : -1, text = who + ": mood", topic = HintTopic.Mood, who = who, size = size, chance = chance, each = each, key = "mood|" + who + size
        };
        ImpactLine Trust(string who, float amount, float chance = 1f, bool each = false) => new ImpactLine
        {
            kind = ImpactKind.Trust, sign = 1, text = who + ": " + M + " trust", key = "trust|" + who, amount = amount, sum = true, topic = HintTopic.Trust, who = who, chance = chance,
            each = each
        };
        ImpactLine Opinion(string who, string of, float size) => new ImpactLine
        {
            kind = ImpactKind.Opinion, sign = size > 0 ? 1 : -1, text = who + " of " + of, topic = HintTopic.Opinion, who = who, about = of, size = size, key = "op|" + who + of
        };
        ImpactLine Tie(string a, string b, bool bond) => new ImpactLine { kind = ImpactKind.Opinion, sign = bond ? 1 : -1, text = a + " and " + b + ": tie", cue = bond ? FlavourCue.Bond : FlavourCue.Rift, who = a, with = b };
        ImpactLine Fight(string a, string b) => new ImpactLine { kind = ImpactKind.Health, sign = -1, text = a + " and " + b + " come to blows", cue = FlavourCue.Fight, who = a, with = b };
        ImpactLine Ways(string lean) => new ImpactLine { kind = ImpactKind.Ways, text = "The colony grows " + lean, topic = HintTopic.Ways, about = lean };
        OptionImpact Of(params ImpactLine[] lines) => new OptionImpact { lines = ImpactMath.Merge(lines) };
        uint letter = ImpactFlavourMath.Seed("Council_Dilemma_Soc_Favoritism", "", "11", "12", "", "Guido has been watching the stores");
        FlavourLine Say(OptionImpact r, int answer = 0, uint? seed = null) => ImpactFlavourMath.Make(r, w, seed ?? letter, answer);
        bool Kinds(FlavourLine f, params FlavourKind[] k) => f.said.Any(b => k.Contains(b.kind));
        var ups = kinds.Where(k => ImpactFlavourMath.Direction(k) > 0).ToArray();
        var downs = kinds.Where(k => ImpactFlavourMath.Direction(k) < 0).ToArray();

        // ---------- The screenshot's four answers (the quartermaster's friends: A Guido, B Avila) ----------
        var audit = Of(Mood("Guido", -3f), Opinion("Guido", "Avila", -15f), Tie("Guido", "Avila", false), Trust("Guido", -2f), Mood("Avila", -6f), Trust("Avila", -4f),
            Trust("everyone else (6)", 1f, 0.5f, true));
        audit.riskChance = 0.35f;
        audit.riskReplaces = true;
        audit.risk = ImpactMath.Merge(new[] { Mood("Guido", -9f), Opinion("Guido", "Avila", -15f), Trust("Guido", -10f), Mood("Avila", 5f), Trust("Avila", 6f), Mood("Guido's friends (2)", -3f) });
        var back = Of(Mood("Guido", 5f), Trust("Guido", 5f), Mood("Avila", -6f), Opinion("Avila", "Guido", -15f), Tie("Avila", "Guido", false), Trust("Avila", -8f),
            Mood("those who dislike Guido (2)", -3f), Trust("those who dislike Guido (2)", -2f), Ways("traditional"));
        var ledger = Of(Mood("Avila", 5f), Trust("Avila", 3f), Mood("Guido", -4f), Trust("Guido", -3f), Mood("Guido and Avila", -3f), Ways("thrifty"));
        ledger.riskChance = 0.35f;
        ledger.risk.Add(Fight("Guido", "Avila"));
        var byLot = Of(Mood("Guido", -6f), Trust("Guido", -5f), Mood("Avila", 5f), Trust("Avila", 3f), Mood("Guido's friends (2)", -3f), Mood("everyone else (6)", -2f, 0.35f, true),
            Trust("everyone else (6)", 1f, 0.4f, true), Ways("restless"));
        var four = new[] { audit, back, ledger, byLot };
        var lines4 = four.Select((r, i) => Say(r, i)).ToList();
        for (int i = 0; i < 4; i++)
        {
            string t = lines4[i].text;
            Console.WriteLine("  flavour, answer " + (i + 1) + ": " + t);
            C(t != null && t.EndsWith(".") && t.Length <= ImpactFlavourMath.LineChars && Clean(t), $"answer {i + 1}: one clean line within its length ({t})");
            C(lines4[i].said.Count >= 1 && lines4[i].said.Count <= 2, $"answer {i + 1}: one or two things said");
        }
        C(lines4[0].text.Contains("Guido") && lines4[0].text.Contains("Avila") && lines4[0].said.Any(b => ImpactFlavourMath.IsGamble(b.kind) && b.a == "Guido"),
            $"the audit: both put out, and the gamble Guido carries ({lines4[0].text})");
        C(lines4[0].said.Where(b => !ImpactFlavourMath.IsGamble(b.kind)).All(b => ImpactFlavourMath.Direction(b.kind) < 0), "the audit goes badly for both, and says so");
        C(lines4[1].said.Any(b => b.a == "Guido" && ImpactFlavourMath.Direction(b.kind) > 0) && lines4[1].said.Any(b => b.a == "Avila" && ImpactFlavourMath.Direction(b.kind) < 0),
            $"backing the quartermaster: good for Guido, bad for Avila ({lines4[1].text})");
        C(lines4[2].said.Any(b => b.kind == FlavourKind.Fight && b.maybe) && lines4[2].text.Contains("Guido") && lines4[2].text.Contains("Avila") && lines4[2].text.Contains("may"),
            $"the ledger: Guido and Avila may come to blows ({lines4[2].text})");
        C(lines4[3].said.Any(b => b.a == "Guido" && ImpactFlavourMath.Direction(b.kind) < 0), $"by lot: Guido loses by it ({lines4[3].text})");
        C(lines4.Select(f => f.text).Distinct().Count() == 4, "four answers, four lines");

        // ---------- What it chooses to say ----------
        var big = Say(Of(Mood("Alo", 1f), Mood("Bea", -9f), Trust("Cy", 1f)));
        C(big.said[0].a == "Bea" && big.said[0].kind == FlavourKind.MoodDownBig, $"the biggest thing first ({big.text})");
        var harm = Say(Of(Mood("Alo", 9f), Trust("Alo", 6f), Mood("Bea", 8f), Ways("open"), Fight("Cy", "Dan")));
        C(harm.said.Any(b => b.kind == FlavourKind.Fight) && harm.said.Count == 2, $"a fight is always said, beside the biggest ({harm.text})");
        var hurtRisk = Of(Mood("Alo", 9f), Trust("Alo", 6f), Mood("Bea", 8f));
        hurtRisk.riskChance = 0.05f;
        hurtRisk.risk.Add(new ImpactLine { kind = ImpactKind.Health, sign = -1, text = "Tam: a bruised foot" });
        C(Kinds(Say(hurtRisk), FlavourKind.Hurt), $"an injury that may come is said, however unlikely ({Say(hurtRisk).text})");
        var gamble = Of(Mood("Alo", 9f), Mood("Bea", 8f));
        gamble.riskChance = 0.4f;
        C(Kinds(Say(gamble), FlavourKind.Gamble, FlavourKind.GambleBig), $"a real gamble is said ({Say(gamble).text})");
        gamble.riskChance = 0.05f;
        C(!Say(gamble).said.Any(b => ImpactFlavourMath.IsGamble(b.kind)) || Say(gamble).said.Count == 2, "a small gamble only when there's room");
        var on = Of(Mood("Alo", 4f));
        on.riskChance = 0.5f;
        on.risk.Add(Mood("Bea", -9f));
        C(Say(on).said.Any(b => b.kind == FlavourKind.GambleOn && b.a == "Bea"), $"a gamble that goes badly for someone names them ({Say(on).text})");
        var fizzle = Of(Mood("Alo", 4f));
        fizzle.riskChance = 0.3f;
        fizzle.riskReplaces = true;
        C(Kinds(Say(fizzle), FlavourKind.Fizzle), $"a gamble that may come to nothing ({Say(fizzle).text})");
        var together = Say(Of(Mood("Guido", -4f), Trust("Guido", -3f), Mood("Avila", -5f), Trust("Avila", -2f)));
        C(together.said.Count >= 1 && together.said[0].a == "Guido and Avila", $"two put out alike are said together ({together.text})");
        var manyOne = Say(Of(Mood("Guido", -6f), Trust("Guido", -5f), Trust("everyone else (6)", 2f, 0.5f, true)));
        C(manyOne.said.Count == 1 && manyOne.said[0].kind == FlavourKind.ManyOverOne && manyOne.text.Contains("Guido") && Regex.IsMatch(manyOne.text, @"\b[Ss]ome\b"),
            $"some glad, one not ({manyOne.text})");
        var oneMany = Say(Of(Mood("Guido", 6f), Trust("Guido", 5f), Mood("those who dislike Guido (2)", -4f)));
        C(oneMany.said.Count == 1 && oneMany.said[0].kind == FlavourKind.OneOverMany, $"one glad, a group not ({oneMany.text})");
        var mixed = Say(Of(Mood("Alo", 5f), Trust("Alo", -5f)));
        C(mixed.said[0].kind == FlavourKind.Mixed, $"both ways at once is mixed feelings ({mixed.text})");
        var other = Say(Of(Mood("Alo", 9f), Opinion("Alo", "Bea", 20f), Mood("Cy", -6f)));
        C(other.said.Count == 2 && other.said[1].a == "Cy", $"someone else before the same person again ({other.text})");
        var small = Say(Of(Mood("Alo", 9f), Trust("Alo", 6f), Ways("open")));
        C(small.said.Count == 1, $"a small thing beside a big one is left to the hover ({small.text})");
        var lean = Say(Of(Ways("restless")));
        C(lean.text != null && lean.text.Contains("restless") && lean.said[0].kind == FlavourKind.Lean, $"the colony's lean, alone ({lean.text})");
        C(Say(new OptionImpact()).text == null && Say(null).text == null, "nothing to say: no line");
        var play = Of(new ImpactLine { kind = ImpactKind.Map, text = "The feast", topic = HintTopic.Play, hint = "The feast together in the evening", when = "Evening", together = true });
        C(Say(play).said[0].kind == FlavourKind.Gather, $"a gathering ({Say(play).text})");
        var work = Of(new ImpactLine { kind = ImpactKind.Map, text = "The feast", topic = HintTopic.Play, when = "Day", together = true, workTime = true, hours = 3f });
        C(Say(work).said[0].kind == FlavourKind.Work, $"in working hours, the work waits ({Say(work).text})");

        // ---------- Never against the way things go: each kind, one way and the other ----------
        var pairs = new List<(string what, Func<bool, OptionImpact> make)>
        {
            ("a named mood", up => Of(Mood("Alo", up ? 4f : -4f))),
            ("a big named mood", up => Of(Mood("Alo", up ? 9f : -9f))),
            ("a group's mood", up => Of(Mood("everyone (9)", up ? 4f : -4f))),
            ("a big group's mood", up => Of(Mood("everyone (9)", up ? 9f : -9f))),
            ("some of a group", up => Of(Mood("everyone else (8)", up ? 4f : -4f, 0.4f, true))),
            ("trust", up => Of(Trust("Alo", up ? 5f : -5f))),
            ("a group's trust", up => Of(Trust("the devout (3)", up ? 5f : -5f))),
            ("opinion", up => Of(Opinion("Alo", "Bea", up ? 15f : -15f))),
            ("opinion of each other", up => Of(Opinion("Alo and Bea", "each other", up ? 15f : -15f))),
            ("a bond", up => Of(Tie("Alo", "Bea", up))),
            ("goods", up => Of(new ImpactLine { kind = ImpactKind.Goods, sign = up ? 1 : -1, text = M + " beer", key = "g", amount = 12f, sum = true, signed = false, topic = HintTopic.Goods, about = "beer", stock = 20f })),
            ("much goods", up => Of(new ImpactLine { kind = ImpactKind.Goods, sign = up ? 1 : -1, text = M + " beer", key = "g", amount = 30f, sum = true, signed = false, topic = HintTopic.Goods, about = "beer", stock = 20f })),
            ("silver", up => Of(new ImpactLine { kind = ImpactKind.Goods, sign = 1, text = M + " silver", key = "s", amount = up ? 300f : -300f, sum = true, topic = HintTopic.Silver })),
            ("another people", up => Of(new ImpactLine { kind = ImpactKind.Goodwill, sign = 1, text = "x: " + M, key = "gw", amount = up ? 6f : -6f, sum = true, topic = HintTopic.Goodwill, about = "the Iron Hand" })),
            ("the camp", up => Of(new ImpactLine { kind = ImpactKind.Goodwill, sign = -1, text = "tension " + M, key = "t", amount = up ? -8f : 8f, sum = true, topic = HintTopic.Tension })),
            ("strain", up => Of(new ImpactLine { kind = ImpactKind.Needs, sign = -1, text = "strain " + M, key = "st", amount = up ? -5f : 5f, sum = true, topic = HintTopic.Strain, who = "Alo" })),
            ("faith", up => Of(new ImpactLine { kind = ImpactKind.Other, sign = up ? 1 : -1, text = "certainty", topic = HintTopic.Certainty, who = "Alo", size = up ? 4f : -4f })),
            ("standards", up => Of(new ImpactLine { kind = ImpactKind.Needs, sign = -1, text = "standards " + M, key = "sd", amount = up ? -4f : 4f, sum = true, topic = HintTopic.Standards, who = "Alo" })),
            ("people", up => Of(new ImpactLine { kind = ImpactKind.People, sign = up ? 1 : -1, text = "Tam joins", cue = up ? FlavourCue.Joins : FlavourCue.Leaves, who = up ? null : "Tam" })),
            ("a custom", up => Of(new ImpactLine { kind = ImpactKind.Other, sign = up ? 1 : -1, text = "custom", cue = up ? FlavourCue.CustomKept : FlavourCue.CustomBroken, about = "the harvest supper" })),
            ("health", up => Of(new ImpactLine { kind = ImpactKind.Health, sign = up ? 1 : -1, text = "Tam: an ailment" })),
            ("anything else", up => Of(new ImpactLine { kind = ImpactKind.Other, sign = up ? 1 : -1, text = "something" })),
            ("the many and the one", up => Of(Mood("Guido", up ? 6f : -6f), Trust("Guido", up ? 5f : -5f), Mood("everyone else (6)", up ? -4f : 4f))),
        };
        foreach (var (what, make) in pairs)
            foreach (bool up in new[] { true, false })
                for (int answer = 0; answer < 4; answer++)
                {
                    var f = Say(make(up), answer);
                    C(f.text != null && Clean(f.text), $"{what} ({(up ? "good" : "bad")}): a clean line ({f.text})");
                    C(f.said.All(b => ImpactFlavourMath.Direction(b.kind) == 0 || ImpactFlavourMath.Direction(b.kind) == (up ? 1 : -1))
                      || f.said.Any(b => b.kind == FlavourKind.ManyOverOne || b.kind == FlavourKind.OneOverMany),
                        $"{what} ({(up ? "good" : "bad")}): said the way it goes ({f.text}: {string.Join(", ", f.said.Select(b => b.kind))})");
                    if (what == "the many and the one")
                        C(f.said[0].kind == (up ? FlavourKind.OneOverMany : FlavourKind.ManyOverOne), $"the many and the one, the right way round ({f.text})");
                    else C(!f.said.Any(b => (up ? downs : ups).Contains(b.kind)), $"{what} ({(up ? "good" : "bad")}): never the other way's words");
                }

        // ---------- Many made-up answers: clean, short, true to each one's direction ----------
        var rnd = new Random(11);
        var names = new[] { "Alo", "Bea", "Cy", "Dana", "Ezra", "Guido", "Avila", "Marguerite" };
        var groups = new[] { "everyone (10)", "everyone else (8)", "the devout (3)", "Alo's friends (4)", "those who dislike Bea (2)", "everyone else (6), but not the ascetic" };
        var topics = new[] { HintTopic.Mood, HintTopic.Trust, HintTopic.Opinion, HintTopic.Joy, HintTopic.Strain, HintTopic.Skill, HintTopic.Goods, HintTopic.Silver, HintTopic.Goodwill, HintTopic.Ways, HintTopic.Play, HintTopic.None, HintTopic.Break, HintTopic.Inspire, HintTopic.Certainty };
        int lines = 0;
        for (int round = 0; round < 400; round++)
        {
            var list = new List<ImpactLine>();
            int count = rnd.Next(1, 8);
            for (int i = 0; i < count; i++)
            {
                var t = topics[rnd.Next(topics.Length)];
                float v = (float)(rnd.NextDouble() * 20 - 10);
                string who = rnd.Next(3) == 0 ? groups[rnd.Next(groups.Length)] : rnd.Next(4) == 0 ? names[rnd.Next(names.Length)] + " and " + names[rnd.Next(names.Length)] : names[rnd.Next(names.Length)];
                var l = new ImpactLine
                {
                    kind = (ImpactKind)rnd.Next(12), sign = v >= 0 ? 1 : -1, topic = t, who = who, about = t == HintTopic.Opinion ? names[rnd.Next(names.Length)] : t == HintTopic.Ways ? "merciful" : "the Iron Hand",
                    text = "line " + i, size = v, chance = rnd.Next(3) == 0 ? (float)rnd.NextDouble() : 1f, each = rnd.Next(2) == 0, stock = rnd.Next(-1, 50), hours = rnd.Next(0, 9),
                    workTime = rnd.Next(2) == 0, when = rnd.Next(2) == 0 ? "Evening" : null, together = rnd.Next(2) == 0
                };
                int c = rnd.Next(8);
                if (c == 0)
                {
                    l.cue = FlavourCue.Fight;
                    l.with = names[rnd.Next(names.Length)];
                    l.who = names[rnd.Next(names.Length)];
                }
                else if (c == 1) l.cue = (FlavourCue)rnd.Next(Enum.GetValues(typeof(FlavourCue)).Length);
                if (rnd.Next(2) == 0)
                {
                    l.sum = true;
                    l.amount = v;
                }
                list.Add(l);
            }
            var r = new OptionImpact { lines = list, riskChance = rnd.Next(3) == 0 ? (float)rnd.NextDouble() : 0f, riskReplaces = rnd.Next(2) == 0 };
            if (r.riskChance > 0f && rnd.Next(2) == 0) r.risk.Add(rnd.Next(2) == 0 ? Fight("Alo", "Bea") : Mood(names[rnd.Next(names.Length)], -6f));
            int answer = rnd.Next(5);
            var f = ImpactFlavourMath.Make(r, w, (uint)rnd.Next(), answer);
            if (f.text == null) continue;
            lines++;
            C(Clean(f.text) && f.text.EndsWith(".") && !f.text.Contains('\n'), $"made-up answer {round}: a clean line ({f.text})");
            C(f.text.Length <= ImpactFlavourMath.LineChars, $"made-up answer {round}: within its length ({f.text.Length}: {f.text})");
            C(f.said.Count >= 1 && f.said.Count <= 2, $"made-up answer {round}: one or two things");
            C(f.said.All(b => ImpactFlavourMath.Direction(b.kind) == 0 || b.sign == ImpactFlavourMath.Direction(b.kind)), $"made-up answer {round}: every part the way it goes ({f.text})");
            // The people a feeling names really feel it that way, on balance.
            foreach (var b in f.said.Where(b => !b.many && b.names.Count == 1 && (b.kind == FlavourKind.MoodUp || b.kind == FlavourKind.MoodDown || b.kind == FlavourKind.MoodUpBig
                                                   || b.kind == FlavourKind.MoodDownBig || b.kind == FlavourKind.TrustUp || b.kind == FlavourKind.TrustDown)))
            {
                float net = 0f;
                foreach (var l in r.lines.Where(l => l.topic == HintTopic.Mood || l.topic == HintTopic.Trust))
                {
                    var who = ImpactHints.WhoOf(l.who, w);
                    if (!who.names || !ImpactFlavourMath.SplitNames(who.words, w).Contains(b.names[0])) continue;
                    net += Math.Sign(ImpactHints.Value(l)) * (ImpactHints.Level(l) + 1) * (ImpactHints.Tiny(l) ? 0.5f : 1f) * (l.chance >= 1f ? 1f : 0.4f + 0.6f * l.chance);
                }
                C(Math.Sign(net) == ImpactFlavourMath.Direction(b.kind) || Math.Abs(net) < 0.001f, $"made-up answer {round}: {b.names[0]} said as {b.kind}, and it goes that way for them ({net})");
            }
            // Harm is always said: the fight in the gamble, or another harm that weighs more.
            if (r.risk.Any(l => l.cue == FlavourCue.Fight)) C(f.said.Any(b => b.harm), $"made-up answer {round}: a fight in the gamble is said ({f.text})");
        }
        C(lines > 300, $"made-up answers had something to say ({lines})");

        // ---------- The same words each time, different between the answers of one letter ----------
        var same = Of(Mood("Alo", -4f), Trust("Alo", -3f));
        C(Say(same, 1).text == Say(same, 1).text, "the same answer reads the same each time its letter opens");
        var five = Enumerable.Range(0, 5).Select(i => Say(same, i).text).ToList();
        C(five.Distinct().Count() == 5, $"five answers with the same impact, five different lines ({string.Join(" | ", five)})");
        var seeds = Enumerable.Range(0, 40).Select(i => Say(same, 0, ImpactFlavourMath.Seed("letter", i.ToString())).text).Distinct().Count();
        C(seeds >= 4, $"other letters pick other words ({seeds})");
        C(ImpactFlavourMath.Seed("a", "b") == ImpactFlavourMath.Seed("a", "b") && ImpactFlavourMath.Seed("a", "b") != ImpactFlavourMath.Seed("ab", ""), "a letter's number is stable and its parts kept apart");
        // Every family's words come round for a letter's answers in order, none twice before all have been.
        foreach (var k in new[] { FlavourKind.MoodDown, FlavourKind.TrustDown, FlavourKind.Fight })
        {
            var r = k == FlavourKind.Fight ? Of(Fight("Alo", "Bea")) : k == FlavourKind.TrustDown ? Of(Trust("Alo", -4f)) : Of(Mood("Alo", -4f));
            int count = w.Variants(ImpactFlavourMath.KeyOf(k, false));
            var keys = Enumerable.Range(0, count).Select(i => Say(r, i).keys[0]).ToList();
            C(keys.Distinct().Count() == count, $"{k}: each of {count} answers its own words");
        }

        // ---------- The setting ----------
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        C(settings.Contains("public ImpactShow answerImpact = ImpactShow.Flavour;"), "the flavour line is the default for a new settings file");
        C(Regex.IsMatch(settings, @"answerImpact == ImpactShow\.Flavour && impactShows != ImpactShow\.Hints\) answerImpact = impactShows"), "an older file's exact numbers or off carry over, its hints become flavour");
        C(((ImpactShow[])Enum.GetValues(typeof(ImpactShow))).SequenceEqual(new[] { ImpactShow.Flavour, ImpactShow.Hints, ImpactShow.Exact, ImpactShow.Off }), "four ways to show it");
        return n;
    }
}
