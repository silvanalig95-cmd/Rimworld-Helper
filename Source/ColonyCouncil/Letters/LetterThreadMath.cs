using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How well the colony and one correspondent know each other by letter (Letters/LetterThreadMath.cs).</summary>
    public enum Familiarity : byte
    {
        /// <summary>Not read (a call, words passed on, a council's letter): no lines of its own.</summary>
        None,
        /// <summary>The writer's first letter in this correspondence.</summary>
        New,
        /// <summary>A letter or a few have passed: known, still on formal terms.</summary>
        Known,
        /// <summary>Many letters over a while, on easy terms: first names, a habit of writing.</summary>
        Familiar,
        /// <summary>A long, warm correspondence: old friends by letter, nicknames, shared jokes.</summary>
        Close,
        /// <summary>Cooled after a slight, a broken promise, a war or a fall in regard: formal again, shorter, colder.</summary>
        Cooled
    }

    /// <summary>How things stand between the colony and one correspondent as a letter is written (read by the game, LetterThreads).</summary>
    public struct LetterStanding
    {
        /// <summary>Letters both ways before this one, and of them the writer's side's.</summary>
        public int letters, mine;
        /// <summary>Days since the first letter, and since the last one either way.</summary>
        public float days, silence;
        /// <summary>Their regard of the colony now, the warmest it reached while they wrote, and their people's goodwill.</summary>
        public int opinion, peak, goodwill;
        public bool war, penFriend;
        /// <summary>Days since the newest wrong between the two peoples (either side's), -1 for none; and how heavy it weighs.</summary>
        public float wrongDays;
        public int wrongWeight;
        /// <summary>Good turns either way remembered lately, their weights summed.</summary>
        public int deeds;
    }

    /// <summary>
    /// One thread of a correspondence: something a letter said that a later one can take up (their news, the colony's
    /// news, something sent, a piece written, a joke begun). Saved as LetterThread (Letters/LetterBook.cs).
    /// </summary>
    public class ThreadRec
    {
        /// <summary>What it is about (<see cref="LetterThreadMath.Keys"/>: "ill", "c:harvest", "aid", "poem", "jk:goat"...).</summary>
        public string key;
        /// <summary>Its words ({RE}): a name, a war's enemy, what was sent; empty for none.</summary>
        public string value;
        /// <summary>The colony's letter raised it (else theirs).</summary>
        public bool ours;
        /// <summary>The correspondence's letter count with the letter that raised it (the first letter is 1).</summary>
        public int at;
        public int tick;
        /// <summary>When a letter last took it up (either side), -1 for never.</summary>
        public int touched = -1;
        /// <summary>How often the other side took it up (asked after it, thanked for it), and the side that raised it followed it up.</summary>
        public byte asked, told;
    }

    /// <summary>The thread a letter takes up, or a piece of news it tells first, or the cold word of a cooled correspondence.</summary>
    public sealed class RecallChoice
    {
        /// <summary>The thread taken up (its index), -1 for a tell, a riddle's guess or a slight.</summary>
        public int index = -1;
        /// <summary>The key of the thread taken up (to find it again once the letter is written).</summary>
        public string key;
        /// <summary>The line's tag in Rimfolk_Pool_LetterThread: re:KEY:MOVE ("re:ill:ask", "re:ill:better", "re:tell:war", "re:slight:word").</summary>
        public string tag;
        /// <summary>The words of {RE}.</summary>
        public string value;
        /// <summary>What it does: ask, follow, thank, hope, more, greet, sorry, regret, grudge, tell, guess, slight.</summary>
        public string move;
        /// <summary>A tell raises this thread (its key) with <see cref="value"/>.</summary>
        public string raise;
        /// <summary>Best as a P.S. (a quick question, a word of hope), when the P.S. is free.</summary>
        public bool ps;
        /// <summary>The thread was raised or taken up by the other side's last letter (re:last: "in your last letter").</summary>
        public bool last;
    }

    /// <summary>What holds now, for a follow-up's words (the letter's tags and values, the colony's people still at home).</summary>
    public sealed class RecallScene
    {
        public bool ours;
        public Familiarity stage;
        /// <summary>This letter's number in the correspondence (the first is 1).</summary>
        public int letterNo;
        public int now;
        /// <summary>The tick of the other side's last letter (-1 none): a thread touched by it is "in your last letter".</summary>
        public int otherLast = -1;
        public HashSet<string> tags;
        public IReadOnlyDictionary<string, string> values;
        /// <summary>A colonist by this name is still at home (a newcomer, a child born).</summary>
        public Func<string, bool> here;
        /// <summary>The letter is cool or worse (a grudge may show).</summary>
        public bool cool;
        /// <summary>The colony writes, and their last letter asked a riddle still owed.</summary>
        public bool riddleOwed;
        /// <summary>A wrong that cooled the correspondence (<see cref="LetterThreadMath.SlightOf"/>), or null.</summary>
        public string slight;
        public bool war;
    }

    /// <summary>
    /// A correspondence that builds up (the player, 10 November 2026: "continued letter exchange should build up, the
    /// contents and familiarity between the colony and others should develop and be reflected in the messages"). Each
    /// exchange with a notable grows familiar over its letters (<see cref="StageOf"/>: how many have passed both ways, how
    /// long it has run, their regard and their people's goodwill, good turns remembered), and cools as truly after a
    /// wrong, a broken word, a war or a fall in regard. The stage is a tag the pools read (fam:new, fam:known,
    /// fam:familiar, fam:close, fam:cooled): salutations, openings and closings warm or settle into habit, first names and
    /// nicknames are taken up, a cooled letter is formal again, shorter and colder. Each letter may take up a thread of an
    /// earlier one (<see cref="Choose"/>): a question asked is answered, news is followed up by what is true now (an ill
    /// friend better, still ill, or gone; a war won, lost or over), something sent is remembered, a joke comes back; and
    /// tells news of its own that the next letter can ask after (<see cref="Raised"/>). The colony's letters keep the
    /// thread too. Pure (no game types): Tools/Tests/LetterThreadTests.cs checks the rules and whole correspondences.
    /// </summary>
    public static class LetterThreadMath
    {
        // ================= The stage =================

        public static readonly Familiarity[] Stages = { Familiarity.New, Familiarity.Known, Familiarity.Familiar, Familiarity.Close, Familiarity.Cooled };

        /// <summary>The stage as lines are tagged: fam:new, fam:known, fam:familiar, fam:close, fam:cooled; none for None.</summary>
        public static string Tag(Familiarity f) => f == Familiarity.None ? null : "fam:" + f.ToString().ToLowerInvariant();

        /// <summary>A long silence since the last letter (fam:long), and a new hand writing for the colony (pen:new, {LASTPEN}).</summary>
        public const string LongTag = "fam:long", PenTag = "pen:new";

        /// <summary>The parts of Rimfolk_Pool_LetterFamiliar: a salutation, an opening, a closing, a new hand's word, a long silence's.</summary>
        public const string SaluteTag = "fa:salute", OpenTag = "fa:open", CloseTag = "fa:close", PenPart = "fa:pen", LongPart = "fa:long";

        /// <summary>Days without a letter either way that make a long silence: a step back in warmth, and a word about it.</summary>
        public const float LongSilence = 40f;

        /// <summary>A wrong this heavy within this many days cools a correspondence; a heavier one for longer.</summary>
        public const int WrongWeight = 8, HeavyWrong = 20;

        public const float WrongDays = 30f, HeavyWrongDays = 90f;

        /// <summary>Regard fallen this far from its warmest, and under <see cref="FallBelow"/>, cools a correspondence.</summary>
        public const int Fall = 30, FallBelow = 20;

        /// <summary>
        /// How well they know each other as this letter is written. The writer's first letter in it is New. A war, a fresh
        /// wrong between the two peoples (weight 8 within 30 days, or 20 within 90), or their regard fallen 30 from its warmest
        /// to under 20 cools it. Else: Close after 8 letters (good turns remembered count one at 10 and two at 20, a pen friend
        /// two more) over 30 days with their regard at 30 and their people's goodwill not below 0; Familiar after 4 over 8 days
        /// with their regard not below 0; else Known. A long silence (40 days) is a step back, never below Known.
        /// </summary>
        public static Familiarity StageOf(LetterStanding s)
        {
            if (s.mine <= 0) return Familiarity.New;
            if (Cooled(s)) return Familiarity.Cooled;
            int n = s.letters + (s.deeds >= 20 ? 2 : s.deeds >= 10 ? 1 : 0) + (s.penFriend ? 2 : 0);
            var st = Familiarity.Known;
            if (n >= 8 && s.days >= 30f && s.opinion >= 30 && s.goodwill >= 0) st = Familiarity.Close;
            else if (n >= 4 && s.days >= 8f && s.opinion >= 0) st = Familiarity.Familiar;
            if (s.silence >= LongSilence && st > Familiarity.Known) st--;
            return st;
        }

        /// <summary>A war, a fresh wrong, or regard fallen far from its warmest.</summary>
        public static bool Cooled(LetterStanding s) =>
            s.war || s.wrongDays >= 0f && (s.wrongDays <= WrongDays && s.wrongWeight >= WrongWeight || s.wrongDays <= HeavyWrongDays && s.wrongWeight >= HeavyWrong)
                  || s.peak - s.opinion >= Fall && s.opinion < FallBelow;

        /// <summary>
        /// A wrong between the two peoples as the cold word of a cooled letter names it: word (a promise or a word broken, a
        /// truce or a pact, help refused, an absence), harm (an attack, a theft, a killing, a town struck), insult (a slight, a
        /// guest unfed, a brawl, sides taken against them), theirs (their own people's wrong to the colony). Null for any other.
        /// </summary>
        public static string SlightOf(string kind, bool theirWrong)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            if (theirWrong) return "theirs";
            if (Words.Contains(kind)) return "word";
            if (Harms.Contains(kind)) return "harm";
            if (Insults.Contains(kind)) return "insult";
            return null;
        }

        public static readonly HashSet<string> Words = new HashSet<string>
        {
            "wordBroken", "brokeTruce", "brokePact", "contractBroken", "renounced", "tributeShort", "letDown", "refused", "absent", "questFailed", "missionFailed",
            "hamletRefused", "peaceTalksBad"
        };

        public static readonly HashSet<string> Harms = new HashSet<string>
        {
            "attacked", "robbed", "caravanRaided", "killed", "captured", "sold", "prisonerDied", "townAttacked", "townDestroyed", "townTaken", "townTakenFaith",
            "campHarmed", "postRazed", "campBroken", "fell", "slain"
        };

        public static readonly HashSet<string> Insults = new HashSet<string>
            { "slighted", "unfed", "brawled", "tookSides", "campDriven", "campSour", "polluted", "schemed", "spouseWronged" };

        /// <summary>
        /// How often the stage's own lines come first (Rimfolk_Pool_LetterFamiliar), else the usual ones: a first letter's
        /// introductions half the time, the known now and then, the familiar about half, old friends and a cooled
        /// correspondence most of the time.
        /// </summary>
        public static float FamiliarChance(Familiarity f, string part)
        {
            switch (f)
            {
                case Familiarity.New: return part == OpenTag ? 0.6f : part == SaluteTag ? 0.5f : 0.4f;
                case Familiarity.Known: return part == OpenTag ? 0.35f : 0.3f;
                case Familiarity.Familiar: return part == OpenTag ? 0.5f : 0.55f;
                case Familiarity.Close: return part == OpenTag ? 0.6f : 0.7f;
                case Familiarity.Cooled: return part == OpenTag ? 0.7f : 0.75f;
                default: return 0f;
            }
        }

        /// <summary>
        /// How a shape's weight moves with the stage: nobody sends several pages to a stranger (the long shapes, others' lines
        /// and enclosures half as often at New); old friends write long and add more (the long shapes 1.4 times at Close, 1.2
        /// at Familiar; other hands, enclosures, diaries and riddles more); a note between old friends is rarer, a cooled
        /// correspondence's far likelier (2.5).
        /// </summary>
        public static float KindFactor(LetterKind k, Familiarity f)
        {
            bool longKind = LetterKindMath.IsLong(k);
            switch (f)
            {
                case Familiarity.New:
                    return longKind || k == LetterKind.Hands || k == LetterKind.Enclosure || k == LetterKind.Riddle ? 0.5f : 1f;
                case Familiarity.Familiar:
                    return longKind ? 1.2f : k == LetterKind.Hands || k == LetterKind.Enclosure ? 1.2f : k == LetterKind.Note ? 0.8f : 1f;
                case Familiarity.Close:
                    return longKind ? 1.4f : k == LetterKind.Hands || k == LetterKind.Enclosure || k == LetterKind.Diary || k == LetterKind.Riddle ? 1.5f
                        : k == LetterKind.Note ? 0.6f : 1f;
                case Familiarity.Cooled:
                    return k == LetterKind.Note ? 2.5f : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>A cooled correspondence writes only the usual letter or a note (or a raider's boast); shorter, colder.</summary>
        public static bool CooledFits(LetterKind k, bool boast) => k == LetterKind.Usual || k == LetterKind.Note || k == LetterKind.Raid && boast;

        /// <summary>
        /// Old friends sign with the name alone: Familiar or Close, a warm letter (LetterFormMath.Warmth), no council's or
        /// the Empire's, by the roll (half the time when familiar, seven in ten when close).
        /// </summary>
        public static bool SignsShort(LetterSpec s, float roll) =>
            (s.fam == Familiarity.Familiar || s.fam == Familiarity.Close) && !s.council && !s.empire && LetterFormMath.Warmth(s)
            && roll < (s.fam == Familiarity.Close ? 0.7f : 0.5f);

        // ================= Names =================

        /// <summary>A full name for formal terms: first and last; the first alone, or the name as known, when there is no last.</summary>
        public static string FullName(string first, string last, string known)
        {
            first = first?.Trim();
            last = last?.Trim();
            if (string.IsNullOrEmpty(first)) return known;
            return string.IsNullOrEmpty(last) ? first : first + " " + last;
        }

        private static bool Vowel(char c) => "aeiouyAEIOUY".IndexOf(c) >= 0;

        /// <summary>
        /// A friend's short name, taken from their name where it breaks cleanly after its first syllable ("Dagna" Dag,
        /// "Hessa" Hess, "Marta" Mar), else none: a name of letters only, five or more, starting with a consonant, its first
        /// syllable closed by a consonant before another consonant, three to five letters. Lines with {TONICK} are drawn only
        /// when there is one.
        /// </summary>
        public static string Nickname(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 5) return null;
            foreach (char c in name)
                if (!char.IsLetter(c))
                    return null;
            if (Vowel(name[0])) return null;
            int i = 0;
            while (i < name.Length && !Vowel(name[i])) i++;
            if (i >= name.Length) return null;
            while (i < name.Length && Vowel(name[i])) i++;
            if (i >= name.Length - 1 || Vowel(name[i]) || Vowel(name[i + 1])) return null;
            string nick = name.Substring(0, i + 1);
            char last = char.ToLowerInvariant(name[i]);
            if (char.ToLowerInvariant(name[i + 1]) == last && "slfz".IndexOf(last) >= 0) nick += name[i + 1];
            if (nick.Length < 3 || nick.Length > 5 || nick.Length >= name.Length) return null;
            return char.ToUpperInvariant(nick[0]) + nick.Substring(1).ToLowerInvariant();
        }

        // ================= The threads =================

        /// <summary>The kinds of thread and how each is taken up.</summary>
        private enum Group
        {
            /// <summary>News of their town: the colony asks after it, they follow it up by what is true now.</summary>
            TheirNews,
            /// <summary>News of the colony: they ask after it, the colony follows it up.</summary>
            OurNews,
            /// <summary>Something sent: the one it was sent to thanks for it, the sender hopes it was of use.</summary>
            Sent,
            /// <summary>A piece written (a poem, a story, a recipe...): the reader says something of it.</summary>
            Piece,
            /// <summary>Others at home added a line: greeted back.</summary>
            Hands,
            /// <summary>Help refused: the refuser regrets it among friends, the refused remembers it in a cool letter.</summary>
            Refused,
            /// <summary>A joke begun: the teller goes on with it, the other asks after it.</summary>
            Joke
        }

        private sealed class KindInfo
        {
            public Group group;
            /// <summary>Letters after the one that raised it before the other side takes it up (a thanks for a warning waits past their own answer).</summary>
            public int gap = 1;
            /// <summary>Days after which it is let go.</summary>
            public float stale;
        }

        private static KindInfo K(Group g, float stale, int gap = 1) => new KindInfo { group = g, stale = stale, gap = gap };

        /// <summary>The running jokes of their asides and postscripts (Rimfolk_Pool_LetterAside, _PS: a line that begins one carries !jk:KEY).</summary>
        public static readonly string[] Jokes = { "jk:goat", "jk:pumpkin", "jk:singer", "jk:cat", "jk:mule", "jk:crow", "jk:chickens", "jk:bridge", "jk:well" };

        private static readonly Dictionary<string, KindInfo> kinds = Make();

        private static Dictionary<string, KindInfo> Make()
        {
            var d = new Dictionary<string, KindInfo>
            {
                ["ill"] = K(Group.TheirNews, 40f), ["war"] = K(Group.TheirNews, 40f), ["hard"] = K(Group.TheirNews, 40f), ["loss"] = K(Group.TheirNews, 40f),
                ["head"] = K(Group.TheirNews, 40f), ["raid"] = K(Group.TheirNews, 40f),
                ["c:harvest"] = K(Group.OurNews, 40f), ["c:raid"] = K(Group.OurNews, 40f), ["c:newcomer"] = K(Group.OurNews, 40f), ["c:hungry"] = K(Group.OurNews, 40f),
                ["c:birth"] = K(Group.OurNews, 40f), ["c:wedding"] = K(Group.OurNews, 40f), ["c:death"] = K(Group.OurNews, 40f),
                ["aid"] = K(Group.Sent, 90f), ["gift"] = K(Group.Sent, 90f), ["goods"] = K(Group.Sent, 90f, 2), ["warned"] = K(Group.Sent, 90f, 2), ["condoled"] = K(Group.Sent, 90f, 2),
                ["poem"] = K(Group.Piece, 60f), ["story"] = K(Group.Piece, 60f), ["sermon"] = K(Group.Piece, 60f), ["recipe"] = K(Group.Piece, 60f),
                ["enclosure"] = K(Group.Piece, 60f), ["long"] = K(Group.Piece, 60f), ["chronicle"] = K(Group.Piece, 60f), ["diary"] = K(Group.Piece, 60f),
                ["hands"] = K(Group.Hands, 40f),
                ["aid:no"] = K(Group.Refused, 90f, 2)
            };
            foreach (var j in Jokes) d[j] = K(Group.Joke, 240f);
            return d;
        }

        /// <summary>Every kind of thread a letter can raise.</summary>
        public static IEnumerable<string> Keys => kinds.Keys;

        public static bool Known(string key) => key != null && kinds.ContainsKey(key);

        /// <summary>A thread let go: of a kind no longer known, or older than its kind keeps (news 40 days, things sent 90, a joke 240).</summary>
        public static bool Stale(ThreadRec r, int now) =>
            r == null || !kinds.TryGetValue(r.key ?? "", out var k) || (now - r.tick) / TicksPerDay > k.stale;

        public const float TicksPerDay = 60000f;

        /// <summary>Threads kept with one correspondent, at most.</summary>
        public const int MaxThreads = 8;

        /// <summary>The things sent whose sender hopes they were of use.</summary>
        private static bool Hopes(string key) => key == "aid" || key == "gift" || key == "goods" || key == "recipe";

        /// <summary>
        /// What the writer's side can do with a thread now, or null: the other side's news asked after (a loss or a death
        /// sorrowed for) once, the next letter on; one's own news followed up once, after it was asked after or three
        /// letters on; something sent thanked for once (twice between old friends), hoped of use once by the sender; a
        /// piece spoken of once; others' lines greeted; help refused regretted among friends, remembered in a cool letter;
        /// a joke gone on with twice, asked after once each telling.
        /// </summary>
        public static string Move(ThreadRec r, bool ours, int letterNo, Familiarity stage, bool cool)
        {
            if (r == null || !kinds.TryGetValue(r.key ?? "", out var k)) return null;
            bool raiser = r.ours == ours;
            int since = letterNo - r.at;
            switch (k.group)
            {
                case Group.TheirNews:
                case Group.OurNews:
                    bool sorrow = r.key == "loss" || r.key == "c:death";
                    if (!raiser) return r.asked == 0 && since >= 1 ? sorrow ? "sorry" : "ask" : null;
                    if (r.key == "c:death") return null;
                    return r.told == 0 && since >= 2 && (r.asked > 0 || since >= 3) ? "follow" : null;
                case Group.Sent:
                    if (!raiser) return r.asked < (stage == Familiarity.Close ? 2 : 1) && since >= k.gap ? "thank" : null;
                    return Hopes(r.key) && r.told == 0 && since >= 2 ? "hope" : null;
                case Group.Piece:
                    if (!raiser) return r.asked == 0 && since >= 1 ? "thank" : null;
                    return Hopes(r.key) && r.told == 0 && since >= 2 ? "hope" : null;
                case Group.Hands:
                    return !raiser && r.asked == 0 && since >= 1 ? "greet" : null;
                case Group.Refused:
                    if (raiser) return r.told == 0 && since >= k.gap && (stage == Familiarity.Familiar || stage == Familiarity.Close) ? "regret" : null;
                    return r.asked == 0 && since >= k.gap && cool ? "grudge" : null;
                case Group.Joke:
                    if (raiser) return r.told < 2 && since >= 2 ? "more" : null;
                    return r.asked <= r.told && since >= 1 ? "ask" : null;
                default:
                    return null;
            }
        }

        private static bool Has(HashSet<string> t, string tag) => t != null && t.Contains(tag);

        private static bool Is(IReadOnlyDictionary<string, string> v, string key, string value) =>
            v != null && !string.IsNullOrEmpty(value) && v.TryGetValue(key, out var x) && x == value;

        /// <summary>
        /// How a follow-up goes, by what is true now (the letter's facts): an ill friend still ill, gone or better; a war
        /// over in peace, being won or lost, still on, or over; hard times still hard or easing; a new head settled in (only
        /// while they are head); after a raid, after a loss. The colony's own: the harvest thin, plentiful or simply in;
        /// raiders again or quiet since; a newcomer settled (only while at home); hunger still or eased; a child growing
        /// (while at home); the newly wed. Null when nothing true can be said.
        /// </summary>
        public static string Outcome(ThreadRec r, HashSet<string> t, IReadOnlyDictionary<string, string> v, Func<string, bool> here, int now)
        {
            switch (r?.key)
            {
                case "ill":
                    if (Has(t, "town:loss") && Is(v, "LOST", r.value)) return "lost";
                    if (Has(t, "town:ill") && Is(v, "ILL", r.value)) return "still";
                    return "better";
                case "war":
                    if (Has(t, "their:peace") && Is(v, "PEACEWITH", r.value)) return "peace";
                    if (Has(t, "their:war") && Is(v, "WAR", r.value)) return Has(t, "war:winning") ? "winning" : Has(t, "war:losing") ? "losing" : "still";
                    return "over";
                case "hard":
                    return Has(t, "town:struggling") || Has(t, "town:poor") ? "still" : "better";
                case "head":
                    return Has(t, "town:head") && Is(v, "HEAD", r.value) ? "settled" : null;
                case "raid":
                case "loss":
                    return "after";
                case "c:harvest":
                    return Has(t, "colony:hungry") ? "thin" : Has(t, "colony:plenty") ? "full" : "in";
                case "c:raid":
                    return Has(t, "colony:raid") && (now - r.tick) / TicksPerDay > 6f ? "again" : "quiet";
                case "c:newcomer":
                    return here != null && here(r.value) ? "settled" : null;
                case "c:hungry":
                    return Has(t, "colony:hungry") ? "still" : "better";
                case "c:birth":
                    return here != null && here(r.value) ? "grows" : null;
                case "c:wedding":
                    return "well";
                default:
                    return null;
            }
        }

        /// <summary>A piece of news a letter can tell first, raising a thread: the fact it needs, the thread's key, the value it carries.</summary>
        public struct Tell
        {
            public string key, fact, value;

            public Tell(string key, string fact, string value)
            {
                this.key = key;
                this.fact = fact;
                this.value = value;
            }
        }

        /// <summary>Their town's news, as the facts hold it: someone ill, their war, hard times, a new head, a death.</summary>
        public static readonly Tell[] TheirTells =
        {
            new Tell("ill", "town:ill", "ILL"), new Tell("war", "their:war", "WAR"), new Tell("hard", "town:struggling", "FORTUNEWHY"), new Tell("head", "town:newhead", "HEAD"),
            new Tell("loss", "town:loss", "LOST")
        };

        /// <summary>The colony's news, as the facts hold it at home: the harvest, a raid, a newcomer, hunger, a birth, a wedding, a death.</summary>
        public static readonly Tell[] OurTells =
        {
            new Tell("c:harvest", "colony:harvest", null), new Tell("c:raid", "colony:raid", null), new Tell("c:newcomer", "colony:newcomer", "NEWCOMER"),
            new Tell("c:hungry", "colony:hungry", null), new Tell("c:birth", "colony:birth", "BORN"), new Tell("c:wedding", "colony:wedding", "WEDDING"),
            new Tell("c:death", "colony:death", "DEAD")
        };

        /// <summary>The tell's tag: re:tell:ill, re:tell:harvest...</summary>
        public static string TellTag(string key) => "re:tell:" + (key.StartsWith("c:", StringComparison.Ordinal) ? key.Substring(2) : key);

        /// <summary>A thread's line tag: re:KEY:MOVE, the move a follow-up's outcome ("re:ill:better").</summary>
        public static string RecallTag(string key, string move) => "re:" + key + ":" + move;

        /// <summary>
        /// The thread this letter takes up, or the news it tells, or null. Nothing in a first letter. A cooled one names the
        /// wrong coldly (seven in ten), else nothing. Otherwise a thread now and then (Known 0.45, Familiar 0.65, Close 0.8):
        /// a follow-up most, a question, a thanks, a regret, a joke, a hope, a greeting back, the colony's guess at their
        /// riddle, or a piece of news told first that the next letter can ask after; fresher threads first.
        /// </summary>
        public static RecallChoice Choose(IReadOnlyList<ThreadRec> threads, RecallScene x, Func<float> roll)
        {
            if (x == null) return null;
            roll ??= () => 0.5f;
            if (x.stage == Familiarity.None || x.stage == Familiarity.New) return null;
            if (x.stage == Familiarity.Cooled)
            {
                string g = x.slight ?? (x.war ? "war" : null);
                if (g == null || roll() >= 0.7f) return null;
                return new RecallChoice { tag = "re:slight:" + g, move = "slight" };
            }
            float chance = x.stage == Familiarity.Known ? 0.45f : x.stage == Familiarity.Familiar ? 0.65f : 0.8f;
            if (roll() >= chance) return null;
            var cands = new List<(RecallChoice c, float w)>();
            if (threads != null)
                for (int i = 0; i < threads.Count; i++)
                {
                    var r = threads[i];
                    if (r == null || Stale(r, x.now)) continue;
                    string move = Move(r, x.ours, x.letterNo, x.stage, x.cool);
                    if (move == null) continue;
                    string tagMove = move;
                    if (move == "follow")
                    {
                        tagMove = Outcome(r, x.tags, x.values, x.here, x.now);
                        if (tagMove == null) continue;
                    }
                    float w = move == "follow" ? 3f : move == "ask" ? r.key.StartsWith("jk:", StringComparison.Ordinal) ? 1.5f : 2.5f : move == "thank" ? r.asked == 0 ? 2.5f : 1.2f
                        : move == "sorry" || move == "regret" ? 2f : move == "more" ? 1.6f : move == "greet" ? 1.5f : move == "grudge" ? 1.5f : 1.2f;
                    float days = Math.Max(0f, (x.now - r.tick) / TicksPerDay);
                    w *= 1f + 1f / (1f + days / 10f);
                    bool quick = move == "hope" || move == "greet" || move == "ask" && r.key.StartsWith("jk:", StringComparison.Ordinal);
                    cands.Add((new RecallChoice { index = i, key = r.key, tag = RecallTag(r.key, tagMove), value = r.value ?? "", move = move, ps = quick,
                        last = x.otherLast >= 0 && Math.Max(r.tick, r.touched) >= x.otherLast }, w));
                }
            if (x.ours && x.riddleOwed) cands.Add((new RecallChoice { tag = "re:riddle:guess", move = "guess", ps = true }, 1.4f));
            string from = x.values != null && x.values.TryGetValue("FROM", out var f) ? f : null;
            foreach (var tell in x.ours ? OurTells : TheirTells)
            {
                if (!Has(x.tags, tell.fact)) continue;
                string value = "";
                if (tell.value != null && (x.values == null || !x.values.TryGetValue(tell.value, out value) || string.IsNullOrEmpty(value))) continue;
                // Nobody tells their correspondent of themselves as news ("{HEAD} leads the town now", by {HEAD}).
                if (!string.IsNullOrEmpty(value) && value == from) continue;
                bool told = false;
                if (threads != null)
                    foreach (var r in threads)
                        if (r != null && r.key == tell.key && !Stale(r, x.now) && (tell.value == null || r.value == value))
                            told = true;
                if (told) continue;
                cands.Add((new RecallChoice { tag = TellTag(tell.key), move = "tell", raise = tell.key, value = value ?? "" }, 1.4f));
            }
            if (cands.Count == 0) return null;
            float total = 0f;
            foreach (var c in cands) total += c.w;
            float pick = Math.Max(0f, Math.Min(0.9999f, roll())) * total;
            RecallChoice chosen = cands[cands.Count - 1].c;
            foreach (var (c, w) in cands)
            {
                if (pick < w)
                {
                    chosen = c;
                    break;
                }
                pick -= w;
            }
            // A quick question or word of hope goes in the P.S. now and then.
            if (chosen.ps) chosen.ps = roll() < 0.4f;
            return chosen;
        }

        /// <summary>The thread taken up by a line written: the other side's taking it up counted (asked), or the raiser's (told).</summary>
        public static void Mark(ThreadRec r, string move, int now)
        {
            if (r == null || move == null) return;
            r.touched = now;
            switch (move)
            {
                case "ask":
                case "sorry":
                case "thank":
                case "greet":
                case "grudge":
                    if (r.asked < 250) r.asked++;
                    break;
                case "follow":
                case "hope":
                case "more":
                case "regret":
                    if (r.told < 250) r.told++;
                    break;
            }
        }

        /// <summary>The pieces a letter of each shape leaves to be spoken of (a poem, a story, a sermon...).</summary>
        private static string PieceKey(LetterKind k)
        {
            switch (k)
            {
                case LetterKind.Poem: return "poem";
                case LetterKind.Story: return "story";
                case LetterKind.Sermon: return "sermon";
                case LetterKind.Recipe: return "recipe";
                case LetterKind.Enclosure: return "enclosure";
                case LetterKind.Long: return "long";
                case LetterKind.Chronicle: return "chronicle";
                case LetterKind.Diary: return "diary";
                default: return null;
            }
        }

        private static string V(LetterSpec s, string key) => s.values.TryGetValue(key, out var v) ? v ?? "" : "";

        /// <summary>
        /// The threads a letter raises, from what it said (<paramref name="shown"/>: its words and the narrator's lines, as
        /// the player reads them). Their letter: news of their town only when the words name it (someone ill, their war, hard
        /// times and why, a death, a new head), a raid told, a joke begun (a line drawn with jk: or !jk:), a piece, others'
        /// lines, and what came with it (help sent, a gift, help refused: <paramref name="sent"/>). The colony's: its news as
        /// the lines drawn said it (colony:harvest, :raid, :newcomer, :hungry, :birth, :wedding, :death), a piece (a poem by
        /// its writer, the cook's recipe), others' lines, a warning given, condolences sent (for a loss by name), goods agreed
        /// to on their request. And the news this letter told first (<paramref name="told"/>).
        /// </summary>
        public static List<ThreadRec> Raised(bool ours, LetterSpec s, string shown, string sent, string sentWhat, RecallChoice told)
        {
            var list = new List<ThreadRec>();
            if (s == null) return list;
            shown ??= "";
            void Add(string key, string value) => list.Add(new ThreadRec { key = key, value = value ?? "", ours = ours });
            if (told?.raise != null) Add(told.raise, told.value);
            var tags = s.tags;
            if (!ours)
            {
                foreach (var tell in TheirTells)
                {
                    string v = V(s, tell.value);
                    // Only what the words named, and never the writer themselves (their name is in the signature).
                    if (v.Length > 0 && v != V(s, "FROM") && Has(tags, tell.fact) && shown.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0 && !list.Exists(r => r.key == tell.key)) Add(tell.key, v);
                }
                var raid = s.who?.raid;
                if (s.kind == LetterKind.Raid && raid != null && !raid.ours && !raid.byColony) Add("raid", raid.raiders ?? V(s, "RAIDERS"));
                // A joke begun: a line that tells it first (!jk:KEY); one that goes on with a joke already running raises nothing.
                foreach (var d in s.drawn)
                {
                    if (!d.StartsWith("!jk:", StringComparison.Ordinal)) continue;
                    string j = d.Substring(1);
                    if (Array.IndexOf(Jokes, j) >= 0 && !list.Exists(r => r.key == j)) Add(j, "");
                }
                if (sent == "aid" || sent == "gift" || sent == "aid:no") Add(sent, sentWhat);
            }
            else
            {
                foreach (var tell in OurTells)
                {
                    if (!s.drawn.Contains(tell.fact) || list.Exists(r => r.key == tell.key)) continue;
                    string v = tell.value != null ? V(s, tell.value) : tell.key == "c:raid" && Has(tags, "colony:raidby") ? V(s, "RAIDERS") : "";
                    if (tell.value != null && v.Length == 0) continue;
                    Add(tell.key, v);
                }
                if (s.kind == LetterKind.Raid && s.who?.raid != null && s.who.raid.ours && !list.Exists(r => r.key == "c:raid")) Add("c:raid", s.who.raid.raiders ?? "");
                if (s.purpose == "warning") Add("warned", "");
                if (s.purpose == "condolences")
                {
                    string topic = V(s, "LOSTNAME");
                    Add("condoled", topic);
                }
                if (s.purpose == "reply" && Has(tags, "k:request") && Has(tags, "yes")) Add("goods", V(s, "GOODS"));
            }
            string piece = PieceKey(s.kind);
            if (piece != null) Add(piece, piece == "poem" && ours ? V(s, "FROM") : piece == "recipe" && ours ? V(s, "COOK") : "");
            if (s.kind == LetterKind.Hands && s.who != null && s.who.hands.Count > 0) Add("hands", s.who.hands[0]);
            return list;
        }

        /// <summary>
        /// A thread raised, into a correspondence's list: where it goes (the index to replace, or -1 to add): one of the same
        /// kind is replaced (news of someone else ill takes the old one's place); a full list gives up a thread already taken
        /// up, else its oldest.
        /// </summary>
        public static int Slot(IReadOnlyList<ThreadRec> threads, ThreadRec incoming, int max = MaxThreads)
        {
            if (threads == null || incoming == null) return -1;
            for (int i = 0; i < threads.Count; i++)
                if (threads[i] != null && threads[i].key == incoming.key)
                    return i;
            if (threads.Count < max) return -1;
            int best = 0;
            for (int i = 1; i < threads.Count; i++)
            {
                var a = threads[i];
                var b = threads[best];
                bool aDone = a.asked > 0 || a.told > 0, bDone = b.asked > 0 || b.told > 0;
                if (aDone && !bDone || aDone == bDone && a.tick < b.tick) best = i;
            }
            return best;
        }
    }
}
