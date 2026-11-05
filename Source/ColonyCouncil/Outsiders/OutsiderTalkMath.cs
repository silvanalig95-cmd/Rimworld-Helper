using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One line a speaker may say at one turn of a conversation piece: who says it, what it needs, its words.</summary>
    public sealed class TalkLine
    {
        /// <summary>Said by the colonist (true) or by the outsider.</summary>
        public bool us;
        /// <summary>Its own conditions (the speaker tag taken out) and its text.</summary>
        public LineTags.Parsed tags;
        public string text;
        /// <summary>The placeholders it fills ("TOWN", "SITUATION").</summary>
        public string[] holes;
    }

    /// <summary>
    /// A conversation piece (an OutsiderTalkDef, taken apart once): a short exchange between a colonist and someone from
    /// outside the colony, two to six turns, the speakers taking turns. Each turn has one or more lines to choose from
    /// (written one after another for the same speaker); the one that fits the moment best is said.
    /// </summary>
    public sealed class TalkPiece
    {
        public string name;
        /// <summary>What it's about, for the pair's memory and so the same two don't keep to one subject.</summary>
        public string topic;
        /// <summary>When it can be said: "chat" (a chat, ordered or on their own), "home" (asked about home), "know" (getting to know them).</summary>
        public string[] uses;
        public float weight = 1f;
        public LineTags.Parsed when;
        public readonly List<List<TalkLine>> turns = new List<List<TalkLine>>();
        /// <summary>What goes into the town's "Heard lately" once it's said (placeholders filled), or null.</summary>
        public string heard;
        public string[] heardHoles;
        /// <summary>The mark it leaves on the pair's memory ("plague"), and the placeholder whose fact it remembers ("SITUATION").</summary>
        public string mark, about;
        /// <summary>It picks up an earlier talk: its conditions name a mark (talked:KEY) or what became of it (then:...).</summary>
        public bool callback;
        /// <summary>Written for a talk that went cool or sour (a tone: condition).</summary>
        public bool toned;
        /// <summary>Written for a child (them:young or us:young asked for).</summary>
        public bool forYoungThem, forYoungUs;

        public bool For(string use)
        {
            if (uses == null) return false;
            foreach (var u in uses)
                if (u == use) return true;
            return false;
        }
    }

    /// <summary>What an earlier talk between two left behind: its mark, when, what it was about then, and a reference to the fact.</summary>
    public sealed class TalkMark
    {
        public string key;
        public int tick;
        public string subject;
        public string fact;
    }

    /// <summary>A piece chosen for two people, with the lines picked for each turn, filled with the facts.</summary>
    public sealed class TalkChoice
    {
        public TalkPiece piece;
        public MarkFacts mark;
        public readonly List<(bool us, string text)> lines = new List<(bool, string)>();
        public string heard;
        public string markSubject, markFact;
    }

    /// <summary>
    /// The pure rules of talking with outsiders the way people do (Outsiders/OutsiderTalk*.cs): how much patience
    /// someone has for the colony on a visit, asking the same thing twice, a pushy colonist, gifts in a row, how often
    /// colonists start a talk by themselves, and the conversation pieces: taking them apart, whether one fits the
    /// moment, which one is said (what's current and recent first, never the same one twice to the same person while
    /// another fits), and filling their placeholders with the facts (Texts.FillLine). No game types, so Tools/Tests can
    /// check it.
    /// </summary>
    public static class OutsiderTalkMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        // ---------------- Patience ----------------

        public const int PatienceBase = 3, PatienceMin = 1, PatienceMax = 6;

        /// <summary>
        /// How many real things someone has patience for with the colony on a visit: 3, one more when they think warmly of
        /// it (30), one more when they hold it dear (60), one more with a friend in the colony, one more for a kind nature
        /// and one less for a cold one; one less when they think ill of it (below 0), another at -30. 1 to 6.
        /// </summary>
        public static int Patience(int ofColony, int nature, bool friend)
        {
            int p = PatienceBase;
            if (ofColony >= 30) p++;
            if (ofColony >= 60) p++;
            if (friend) p++;
            if (nature >= 1) p++;
            else if (nature <= -1) p--;
            if (ofColony < 0) p--;
            if (ofColony <= -30) p--;
            return Math.Max(PatienceMin, Math.Min(PatienceMax, p));
        }

        /// <summary>Patience comes back, one thing for every this many hours since they last gave some (a guest who stays on).</summary>
        public const float RefillHours = 12f;

        /// <summary>What's used after a rest of <paramref name="hours"/>.</summary>
        public static float Rested(float used, float hours) => hours <= 0f ? Math.Max(0f, used) : Math.Max(0f, used - hours / RefillHours);

        /// <summary>Things left for today (whole ones).</summary>
        public static int Left(int max, float used) => Math.Max(0, max - (int)Math.Ceiling(used - 0.001f));

        /// <summary>
        /// What a thing asks of their patience: one, but nothing for hard words or a challenge (they ask nothing of them)
        /// and nothing for a letter handed over (the letters' own errand).
        /// </summary>
        public static float Cost(OutsiderAct a) => a == OutsiderAct.Provoke || a == OutsiderAct.Duel || a == OutsiderAct.Letter ? 0f : 1f;

        /// <summary>
        /// The asks: news, a good word, a lesson, a favour, an invitation, getting to know them, courting, faith. Asked
        /// again within its wait, the answer is "you asked me that already", with nothing more coming of it.
        /// </summary>
        public static bool Asks(OutsiderAct a) =>
            a == OutsiderAct.AskHome || a == OutsiderAct.GoodWord || a == OutsiderAct.Teach || a == OutsiderAct.Bring || a == OutsiderAct.Invite
            || a == OutsiderAct.Befriend || a == OutsiderAct.Court || a == OutsiderAct.Faith || a == OutsiderAct.AskFavour || a == OutsiderAct.SpeakPact
            || a == OutsiderAct.OfferExchange;

        /// <summary>Asks by one colonist this close together count as in a row.</summary>
        public const float PushyHours = 6f;
        /// <summary>The third ask in a row (two before it) is pushy.</summary>
        public const int PushyAfter = 2;

        public static bool Pushy(int asksBefore) => asksBefore >= PushyAfter;

        /// <summary>What a pushy ask costs: from the bond between the two, and from a notable's regard of the colonist.</summary>
        public const float PushyWarmth = -4f, PushyRegard = -1.5f;

        /// <summary>A gift counts for less after others on the same visit: 1, 1/2, 1/3, 1/4...</summary>
        public static float GiftFactor(int giftsBefore) => 1f / (1f + Math.Max(0, giftsBefore));

        /// <summary>A gift counting for less than this leaves no memory of it.</summary>
        public const float GiftMemoryFloor = 0.3f;

        /// <summary>How patient they are, in words (a key's last part): Plenty (3 or more left), Some (2), Last (1), None.</summary>
        public static string PatienceWord(int left) => left >= 3 ? "Plenty" : left == 2 ? "Some" : left == 1 ? "Last" : "None";

        // ---------------- Talks they start themselves ----------------

        /// <summary>What decides how likely a colonist is to start a talk with an outsider near them.</summary>
        public struct ChatterInput
        {
            public int social;
            /// <summary>0 none, 1 minor, 2 major passion for Social.</summary>
            public int passion;
            /// <summary>The persona's warmth, -100 to 100 (0 with personalities off).</summary>
            public float warmth;
            public bool kind, abrasive, psychopath, shy;
            /// <summary>Both at leisure, idle or eating near each other (not one passing by).</summary>
            public bool lingering;
            public bool theyKind, theyAbrasive;
            /// <summary>How the outsider regards the colonist.</summary>
            public int ofColonist;
            /// <summary>The setting, 1 normal.</summary>
            public float frequency;
        }

        /// <summary>The chance per look (every 250 ticks, near and free).</summary>
        public const float ChatterBase = 0.035f;

        /// <summary>
        /// The chance a colonist starts a talk with an outsider near them in one look: 3.5%, by their way with words (0.7
        /// at Social 0 to 1.5 at 20, more with a passion), their warmth, a kind nature (1.3), a shy (0.5), cold (0.6) or
        /// abrasive one (0.9); more when both linger (1.4) than in passing (0.5), by the outsider's nature and how they
        /// regard the colonist; times the setting. 25% at most.
        /// </summary>
        public static float ChatterChance(ChatterInput x)
        {
            if (x.frequency <= 0f) return 0f;
            float c = ChatterBase * (0.7f + 0.04f * Math.Max(0, Math.Min(20, x.social))) * (1f + 0.15f * Math.Max(0, Math.Min(2, x.passion)))
                      * (1f + Math.Max(-100f, Math.Min(100f, x.warmth)) / 250f);
            if (x.kind) c *= 1.3f;
            if (x.abrasive) c *= 0.9f;
            if (x.psychopath) c *= 0.6f;
            if (x.shy) c *= 0.5f;
            c *= x.lingering ? 1.4f : 0.5f;
            if (x.theyKind) c *= 1.2f;
            if (x.theyAbrasive) c *= 0.8f;
            if (x.ofColonist >= 25) c *= 1.3f;
            else if (x.ofColonist <= -15) c *= 0.5f;
            c *= x.frequency;
            return Math.Max(0f, Math.Min(0.25f, c));
        }

        /// <summary>Close enough to start one (cells), within the talking range.</summary>
        public const float ChatterRange = 6f;
        /// <summary>An outsider talks on their own at most once in this many hours, the same two once in this many.</summary>
        public const float ChatterGapHours = 4f, ChatterPairGapHours = 12f;
        /// <summary>At most this many talks on their own with one outsider a visit (a day, for one who stays).</summary>
        public const int ChatterPerVisit = 4;

        public static bool ChatterWaited(int lastOutsider, int lastPair, int now) =>
            (lastOutsider < 0 || now - lastOutsider >= ChatterGapHours * TicksPerHour) && (lastPair < 0 || now - lastPair >= ChatterPairGapHours * TicksPerHour);

        /// <summary>How long a talk may run: one in passing three lines; lingering, six.</summary>
        public static int ChatterMaxLines(bool lingering) => lingering ? 6 : 3;

        /// <summary>How long a talk may run by how it goes: warm or good 6 lines, plain 5, cool 3, sour 2.</summary>
        public static int MaxLines(TalkTone t)
        {
            switch (t)
            {
                case TalkTone.Warm:
                case TalkTone.Good: return 6;
                case TalkTone.Plain: return 5;
                case TalkTone.Cool: return 3;
                default: return 2;
            }
        }

        public const int MinTurns = 2, MaxTurns = 6;

        // ---------------- The pace of a scene ----------------

        /// <summary>Ticks before the first line.</summary>
        public const int FirstGap = 30;

        /// <summary>Ticks after a line before the next, by how long it is to read: 150 and 2 a character, 170 to 320.</summary>
        public static int LineGap(int chars) => Math.Max(170, Math.Min(320, 150 + 2 * Math.Max(0, chars)));

        /// <summary>The ticks from the first line due to the scene's end (the last line read).</summary>
        public static int SceneTicks(IList<string> lines)
        {
            if (lines == null || lines.Count == 0) return 0;
            int t = FirstGap;
            for (int i = 0; i < lines.Count; i++) t += LineGap(lines[i]?.Length ?? 0);
            return t;
        }

        // ---------------- Pieces taken apart ----------------

        /// <summary>
        /// A piece from its def's fields: the speakers' lines in order ("[us] ...", "[them,their:tribal] ..."; lines
        /// one after another for the same speaker are choices for one turn), its conditions and what it leaves. The
        /// problems found go into <paramref name="errors"/> (a piece with any is still made, for the tests to report).
        /// </summary>
        public static TalkPiece Parse(string name, string topic, string uses, float weight, string when, IList<string> lines, string heard, string mark, string about,
            List<string> errors)
        {
            var p = new TalkPiece { name = name, topic = topic ?? "", weight = weight > 0f ? weight : 1f, heard = string.IsNullOrEmpty(heard) ? null : heard.Trim() };
            p.uses = string.IsNullOrEmpty(uses) ? new[] { "chat" } : Split(uses);
            p.when = LineTags.Parse("[" + (when ?? "") + "] x");
            p.mark = string.IsNullOrEmpty(mark) ? null : mark.Trim();
            p.about = string.IsNullOrEmpty(about) ? null : about.Trim();
            p.heardHoles = Texts.Holes(p.heard);
            if (p.when.tags != null)
                foreach (var alts in p.when.alts)
                    foreach (var a in alts)
                    {
                        if (!a.neg && (a.name.StartsWith("talked:", StringComparison.Ordinal) || a.name.StartsWith("then:", StringComparison.Ordinal))) p.callback = true;
                        if (!a.neg && a.name.StartsWith("tone:", StringComparison.Ordinal)) p.toned = true;
                        if (!a.neg && a.name == "them:young") p.forYoungThem = true;
                        if (!a.neg && a.name == "us:young") p.forYoungUs = true;
                    }
            bool? last = null;
            if (lines != null)
                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var parsed = LineTags.Parse(raw.Trim());
                    bool hasUs = false, hasThem = false;
                    var rest = new List<string>();
                    if (parsed.tags != null)
                        foreach (var t in parsed.tags)
                        {
                            if (t == "us") hasUs = true;
                            else if (t == "them") hasThem = true;
                            else rest.Add(t);
                        }
                    if (hasUs == hasThem)
                    {
                        errors?.Add($"{name}: a line needs [us] or [them]: {raw.Trim()}");
                        continue;
                    }
                    var line = new TalkLine { us = hasUs, text = parsed.text, holes = Texts.Holes(parsed.text) };
                    line.tags = rest.Count == 0 ? LineTags.Parse(parsed.text) : LineTags.Parse("[" + string.Join(",", rest) + "] " + parsed.text);
                    if (last != hasUs) p.turns.Add(new List<TalkLine>());
                    p.turns[p.turns.Count - 1].Add(line);
                    last = hasUs;
                }
            if (p.turns.Count < MinTurns) errors?.Add($"{name}: {p.turns.Count} turns, at least {MinTurns}");
            if (p.turns.Count > MaxTurns) errors?.Add($"{name}: {p.turns.Count} turns, at most {MaxTurns}");
            if (p.about != null && p.mark == null) errors?.Add($"{name}: <about> without a <mark>");
            if (p.callback && p.mark == null && p.when.tags != null)
            {
                bool talked = false;
                foreach (var t in p.when.tags)
                    if (t.StartsWith("talked:", StringComparison.Ordinal))
                        talked = true;
                if (!talked) errors?.Add($"{name}: a then: condition needs the talked:KEY it follows");
            }
            return p;
        }

        private static string[] Split(string s)
        {
            var parts = s.Split(',');
            var list = new List<string>();
            foreach (var x in parts)
            {
                string t = x.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        // ---------------- Whether a piece fits ----------------

        private static bool Has(Dictionary<string, string> a, Dictionary<string, string> b, string key) =>
            a != null && a.ContainsKey(key) || b != null && b.ContainsKey(key);

        private static bool Filled(string[] holes, FactSet f, MarkFacts m)
        {
            if (holes == null) return true;
            foreach (var h in holes)
                if (!Has(f.values, m?.values, h))
                    return false;
            return true;
        }

        /// <summary>The tags a piece's conditions are read against: everything now, or for a callback this one mark's instead of all marks'.</summary>
        public static HashSet<string> Context(FactSet f, MarkFacts m)
        {
            if (m == null) return f.tags;
            var set = new HashSet<string>();
            foreach (var t in f.tags)
                if (!t.StartsWith("talked:", StringComparison.Ordinal))
                    set.Add(t);
            foreach (var t in m.tags) set.Add(t);
            return set;
        }

        private static HashSet<string> With(HashSet<string> a, HashSet<string> b)
        {
            if (b == null || b.Count == 0) return a;
            var set = new HashSet<string>(a);
            foreach (var t in b) set.Add(t);
            return set;
        }

        /// <summary>
        /// Whether the piece can be said now: written for this use, short enough, its conditions true, a talk that went
        /// cool or sour only with a piece written for it (<paramref name="toneOnly"/>), a child only with one written for
        /// a child, every turn with a line that fits, and every placeholder with a fact. <paramref name="lineOk"/> can
        /// rule lines out (the colonist's lines about guns in a colony without them).
        /// </summary>
        public static bool Fits(TalkPiece p, FactSet f, MarkFacts m, string use, int maxLines, bool toneOnly, Func<TalkLine, bool> lineOk = null)
        {
            if (p == null || f == null || p.turns.Count < MinTurns || p.turns.Count > Math.Min(MaxTurns, maxLines)) return false;
            if (use != null && !p.For(use)) return false;
            if (p.callback != (m != null)) return false;
            if (toneOnly && !p.toned) return false;
            if (f.tags.Contains("them:young") && !p.forYoungThem) return false;
            if (f.tags.Contains("us:young") && !p.forYoungUs) return false;
            var ctx = Context(f, m);
            if (LineTags.Match(p.when, ctx) < 0) return false;
            if (!Filled(p.heardHoles, f, m)) return false;
            if (p.about != null && !Has(f.values, m?.values, p.about)) return false;
            var usCtx = With(ctx, f.usTags);
            var themCtx = With(ctx, f.themTags);
            foreach (var turn in p.turns)
            {
                bool any = false;
                foreach (var l in turn)
                    if (LineFits(l, l.us ? usCtx : themCtx, f, m, lineOk))
                    {
                        any = true;
                        break;
                    }
                if (!any) return false;
            }
            return true;
        }

        private static bool LineFits(TalkLine l, HashSet<string> ctx, FactSet f, MarkFacts m, Func<TalkLine, bool> lineOk) =>
            LineTags.Match(l.tags, ctx) >= 0 && Filled(l.holes, f, m) && (lineOk == null || lineOk(l));

        // ---------------- Which piece ----------------

        /// <summary>How current a condition with no heat of its own counts (the season, their role, the weather).</summary>
        public const float DefaultHeat = 0.25f;

        /// <summary>
        /// How much a piece is wanted now: its weight, times one plus how current each of its conditions is (a raid
        /// yesterday, a war just begun, news that changed since they last talked count most; a piece with no conditions
        /// is plain talk at 1).
        /// </summary>
        public static float Score(TalkPiece p, FactSet f, MarkFacts m)
        {
            float s = 1f;
            if (p.when?.tags != null)
                for (int i = 0; i < p.when.alts.Length; i++)
                {
                    if (p.when.standing[i]) continue;
                    float best = -1f;
                    foreach (var a in p.when.alts[i])
                    {
                        if (a.neg) continue;
                        float h;
                        if (m != null && m.heat.TryGetValue(a.name, out h) || f.heat.TryGetValue(a.name, out h))
                        {
                            if (h > best) best = h;
                        }
                        else if ((m != null && m.tags.Contains(a.name) || f.tags.Contains(a.name)) && DefaultHeat > best) best = DefaultHeat;
                    }
                    if (best > 0f) s += best;
                }
            return p.weight * s;
        }

        /// <summary>A piece already said to this person counts for this much (another nearly always fits).</summary>
        public const float SaidToThem = 0.03f;
        /// <summary>One said lately by anyone at the colony, and one on the subject they talked of last.</summary>
        public const float SaidLately = 0.35f, SameTopic = 0.4f;

        /// <summary>
        /// The piece for these two now, with each turn's line picked and filled; null when nothing fits. Among those
        /// that fit, the more current the likelier (<see cref="Score"/>), less for one said to them before, one said at
        /// the colony lately, or one on the subject of their last talks. A callback is weighed with the earlier talk it
        /// picks up. <paramref name="roll"/> gives numbers from 0 to 1.
        /// </summary>
        public static TalkChoice Choose(IList<TalkPiece> pieces, FactSet f, string use, int maxLines, bool toneOnly, ICollection<string> saidToThem,
            ICollection<string> saidLately, IList<string> lastTopics, Func<float> roll, Func<TalkLine, bool> lineOk = null)
        {
            if (pieces == null || f == null || roll == null) return null;
            var cands = new List<(TalkPiece p, MarkFacts m, float w)>();
            foreach (var p in pieces)
            {
                if (p == null) continue;
                if (p.callback)
                {
                    foreach (var m in f.marks)
                        if (Fits(p, f, m, use, maxLines, toneOnly, lineOk))
                            cands.Add((p, m, Weigh(p, f, m, saidToThem, saidLately, lastTopics)));
                }
                else if (Fits(p, f, null, use, maxLines, toneOnly, lineOk))
                    cands.Add((p, null, Weigh(p, f, null, saidToThem, saidLately, lastTopics)));
            }
            if (cands.Count == 0) return null;
            float total = 0f;
            foreach (var c in cands) total += c.w;
            float r = Clamp01(roll()) * total;
            var pick = cands[cands.Count - 1];
            foreach (var c in cands)
            {
                r -= c.w;
                if (r <= 0f)
                {
                    pick = c;
                    break;
                }
            }
            return Build(pick.p, f, pick.m, roll, lineOk);
        }

        private static float Weigh(TalkPiece p, FactSet f, MarkFacts m, ICollection<string> saidToThem, ICollection<string> saidLately, IList<string> lastTopics)
        {
            float w = Score(p, f, m);
            if (saidToThem != null && saidToThem.Contains(p.name)) w *= SaidToThem;
            if (saidLately != null && saidLately.Contains(p.name)) w *= SaidLately;
            if (lastTopics != null && !string.IsNullOrEmpty(p.topic))
                for (int i = 0; i < lastTopics.Count && i < 2; i++)
                    if (lastTopics[i] == p.topic)
                    {
                        w *= SameTopic;
                        break;
                    }
            return Math.Max(0.0001f, w);
        }

        /// <summary>The lines of a piece that fits: for each turn, a line that fits, the more it speaks to the moment the likelier; filled.</summary>
        public static TalkChoice Build(TalkPiece p, FactSet f, MarkFacts m, Func<float> roll, Func<TalkLine, bool> lineOk = null)
        {
            var ch = new TalkChoice { piece = p, mark = m };
            var ctx = Context(f, m);
            var usCtx = With(ctx, f.usTags);
            var themCtx = With(ctx, f.themTags);
            foreach (var turn in p.turns)
            {
                var fits = new List<(TalkLine l, float w)>();
                foreach (var l in turn)
                {
                    var c = l.us ? usCtx : themCtx;
                    if (!LineFits(l, c, f, m, lineOk)) continue;
                    int n = LineTags.Match(l.tags, c);
                    fits.Add((l, n <= 0 ? 1f : 3f + 2f * n));
                }
                if (fits.Count == 0) return null;
                float total = 0f;
                foreach (var x in fits) total += x.w;
                float r = Clamp01(roll()) * total;
                var pick = fits[fits.Count - 1].l;
                foreach (var x in fits)
                {
                    r -= x.w;
                    if (r <= 0f)
                    {
                        pick = x.l;
                        break;
                    }
                }
                ch.lines.Add((pick.us, Texts.FillLine(pick.text, f.values, m?.values)));
            }
            if (p.heard != null) ch.heard = Texts.FillLine(p.heard, f.values, m?.values);
            if (p.about != null)
            {
                ch.markSubject = Value(f, m, p.about);
                ch.markFact = f.refs.TryGetValue(p.about, out var r2) ? r2 : null;
            }
            return ch;
        }

        private static string Value(FactSet f, MarkFacts m, string key)
        {
            if (m != null && m.values.TryGetValue(key, out var v)) return v;
            return f.values.TryGetValue(key, out v) ? v : null;
        }

        // ---------------- Marks ----------------

        /// <summary>Marks kept per pair: the newest; a mark of the same key replaces the old one.</summary>
        public const int MaxMarks = 10;
        /// <summary>Pieces remembered per pair as said to them, and topics of their last talks.</summary>
        public const int MaxSaid = 16;

        public static string EncodeMark(string key, int tick, string subject, string fact) =>
            Clean(key) + "|" + tick + "|" + Clean(subject) + "|" + Clean(fact);

        private static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace('|', '/');

        public static TalkMark DecodeMark(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split('|');
            if (parts.Length < 2 || parts[0].Length == 0 || !int.TryParse(parts[1], out int tick)) return null;
            return new TalkMark { key = parts[0], tick = tick, subject = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null, fact = parts.Length > 3 && parts[3].Length > 0 ? parts[3] : null };
        }

        /// <summary>A mark added to a pair's list: one of the same key goes, the oldest go past <see cref="MaxMarks"/>.</summary>
        public static void AddMark(List<string> marks, string key, int tick, string subject, string fact)
        {
            if (marks == null || string.IsNullOrEmpty(key)) return;
            marks.RemoveAll(s => DecodeMark(s)?.key == key);
            marks.Add(EncodeMark(key, tick, subject, fact));
            while (marks.Count > MaxMarks) marks.RemoveAt(0);
        }

        /// <summary>A name added to a list kept newest last, at most <paramref name="max"/>.</summary>
        public static void Remember(List<string> list, string name, int max)
        {
            if (list == null || string.IsNullOrEmpty(name)) return;
            list.Remove(name);
            list.Add(name);
            while (list.Count > max) list.RemoveAt(0);
        }

        /// <summary>
        /// A callback was said: a mark whose fact has changed (over, dead, someone new) is settled and let go; one still so
        /// is brought up to date, so the same two don't ask after it every time they meet.
        /// </summary>
        public static void PickedUp(List<string> marks, MarkFacts m, int now)
        {
            if (marks == null || m == null || string.IsNullOrEmpty(m.key)) return;
            bool changed = m.tags.Contains("then:over") || m.tags.Contains("then:dead") || m.tags.Contains("then:changed");
            marks.RemoveAll(s => DecodeMark(s)?.key == m.key);
            if (!changed) marks.Add(EncodeMark(m.key, now, m.subject, m.fact));
        }

        /// <summary>A callback's tags for how long ago it was: then:recent (under 3 days), then:long (20 days or more).</summary>
        public static string AgoTag(int tick, int now)
        {
            float days = (now - tick) / (float)TicksPerDay;
            return days < 3f ? "then:recent" : days >= 20f ? "then:long" : null;
        }

        // ---------------- Heard lately ----------------

        /// <summary>Kept at most, all towns together; each town shows its newest few; let go after this many days.</summary>
        public const int HeardMax = 40, HeardShown = 4;
        public const float HeardDays = 60f;
        /// <summary>The same news again within this many days is the same news (its teller and day brought up to date).</summary>
        public const float HeardSameDays = 10f;

        // ---------------- Arithmetic ----------------

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
