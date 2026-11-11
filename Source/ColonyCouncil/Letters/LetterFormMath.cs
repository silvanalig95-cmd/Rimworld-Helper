using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>How words between the colony and someone out in the world are shaped: a letter, a call on the console, words carried by mouth.</summary>
    public enum LetterShape
    {
        /// <summary>A letter: a heading now and then, a salutation, a few short paragraphs, a closing and a signature, now and then a P.S.</summary>
        Written,
        /// <summary>A call on the comms console: a few spoken lines, a transcript in the console's age.</summary>
        Call,
        /// <summary>Words given to a carrier and passed on by them ("Hessa bids me tell you...").</summary>
        Spoken
    }

    /// <summary>A part of a letter drawn from the pools (Defs/FlavorPoolDefs/Rimfolk_Letters_*.xml).</summary>
    public enum LetterPart
    {
        Salute,
        Open,
        /// <summary>
        /// The heart where the answer pool has no line, or none in the writer's speech: a council's answer, the world writing
        /// first, a reply's thanks, a friend's word; a medieval, unschooled or hive writer's own answer to every purpose.
        /// </summary>
        Heart,
        Aside,
        Close,
        PS,
        /// <summary>A tribal writer's date: "when the geese came back".</summary>
        When,
        /// <summary>The heart of the colony's own letter, by its purpose and the writer's voice.</summary>
        Ours,
        /// <summary>A line of a call's transcript.</summary>
        Call,
        /// <summary>The carrier's frame round words passed on by mouth.</summary>
        Spoken,
        // ---------------- The shapes of 10 November 2026 (Letters/LetterKindMath.cs) ----------------
        /// <summary>A bare note's few words round its heart (note:open, note:close).</summary>
        Note,
        /// <summary>The line that brings a piece in (lk:KIND), and the line after it (lk:after).</summary>
        Lead,
        /// <summary>A story's parts by theme (st:open, st:turn, st:end, st:moral, st:joke; st:hunt, st:feud...).</summary>
        Story,
        /// <summary>A raid's account in parts (rd:come, rd:stand, rd:burn, rd:fall, rd:end, rd:after), by its record or as an old tale.</summary>
        Raid,
        /// <summary>Poems and songs, whole (po:ballad, po:verse, po:lament, po:drinking, po:hymn, po:nonsense, po:work, po:war; po:short).</summary>
        Poem,
        /// <summary>Sermons and tracts, whole, by their memes and precepts (c:..., p:...), true believers' and cranks'.</summary>
        Sermon,
        /// <summary>A chronicle's masthead, items by subject (ch:item, chs:...), notices and foot.</summary>
        Chronicle,
        /// <summary>Set pieces: a petition (pe:), a list or an account (ac:), a recipe or remedy (rc:), a riddle (ri:).</summary>
        Set,
        /// <summary>The lines others at home add under the signature (ha:line).</summary>
        Hands,
        /// <summary>What is folded in with the letter (en:line).</summary>
        Enclosure,
        /// <summary>A diary's entries (di:entry), and a letter broken off (di:break) and taken up again (di:resume).</summary>
        Diary,
        /// <summary>A long letter's parts: their headings (pa:head) and bodies (pa:news, pa:life).</summary>
        Part,
        // ---------------- A correspondence that builds up (Letters/LetterThreadMath.cs) ----------------
        /// <summary>The stage's own salutations, openings and closings (fa:salute, fa:open, fa:close; a new hand's word fa:pen, a long silence's fa:long).</summary>
        Familiar,
        /// <summary>An earlier letter's thread taken up (re:KEY:MOVE), news told first (re:tell:KEY), a cooled letter's cold word (re:slight:KIND).</summary>
        Thread
    }

    /// <summary>How a writer of the colony puts things: their Social and Intellectual skill and their nature.</summary>
    public enum VoiceMain
    {
        Plain,
        Blunt,
        Eloquent
    }

    /// <summary>What an aside or a P.S. is: none, a joke, an everyday detail, a serious line.</summary>
    public enum AsideKind
    {
        None,
        Joke,
        Everyday,
        Serious
    }

    /// <summary>How a writer dates a letter, by their age: by the season, in the court's words, plainly, by the standard date.</summary>
    public enum DateStyle
    {
        Season,
        Courtly,
        Plain,
        Standard
    }

    /// <summary>How a letter is signed.</summary>
    public enum SignStyle
    {
        Person,
        PersonUntitled,
        Council,
        Noble,
        Ours,
        OursTitled,
        /// <summary>One who can't write their name: a mark.</summary>
        Mark,
        /// <summary>Written down for them by someone else.</summary>
        Helped,
        /// <summary>A hive: the many of their town.</summary>
        Many,
        /// <summary>A bare note: the name alone.</summary>
        Short
    }

    /// <summary>How well a writer was schooled: their letters read like it.</summary>
    public enum Schooling
    {
        Schooled,
        /// <summary>Barely: a few slips.</summary>
        Some,
        /// <summary>Not at all: many.</summary>
        None
    }

    /// <summary>
    /// What the writer wants from the letter, which sets its tone as much as their regard does: a friend writing to a
    /// friend; a trader angling for business; an official doing their duty (a council, the Empire, a head on their
    /// people's business); a wary neighbour sounding the colony out; a proud one who won't ask outright; or nothing beyond
    /// the letter itself.
    /// </summary>
    public enum LetterMotive
    {
        Plain,
        Friend,
        Trade,
        Duty,
        Wary,
        Proud
    }

    /// <summary>A writer of the colony's voice: the main way they put things, and what colours it.</summary>
    public struct LetterVoice
    {
        public VoiceMain main;
        public bool kind, abrasive, cheerful, gloomy, learned, proud;
    }

    /// <summary>
    /// Everything a letter is made from (no game types): who writes, in what shape, age and speech, how they stand and what
    /// they want, what it is for, the tags its lines are chosen by, the values they are filled with, the heading, the heart
    /// (filled), the signature, and the narrator's lines that go under it, set apart.
    /// </summary>
    public sealed class LetterSpec
    {
        public LetterShape shape;
        /// <summary>The colony writes (else they do).</summary>
        public bool ours;
        /// <summary>The writer's age (their means and their ways).</summary>
        public WorldAge age;
        /// <summary>The Empire writes, or is written to: its titles and honour.</summary>
        public bool empire;
        /// <summary>A council writes (a dealing's answer), or is written to.</summary>
        public bool council;
        public bool penFriend;
        public bool war;
        /// <summary>Theirs: how they took the colony's letter. The colony's own: how the two stand (their regard of it, <see cref="LetterFormMath.Stance"/>).</summary>
        public AnswerTone tone;
        /// <summary>What it is for, as lines are tagged ("greetings", "condolences", "council", "first"...).</summary>
        public string purpose;
        /// <summary>Hard times on the writer's side, or anything else that makes it a grave letter beyond its purpose.</summary>
        public bool grave;
        /// <summary>What the writer wants from it (<see cref="LetterFormMath.Motive"/>).</summary>
        public LetterMotive motive;
        /// <summary>The writer's humour, -100 (dour) to 100 (the one who makes the jokes).</summary>
        public int humour;
        /// <summary>The colony's writer's voice (the colony's own letters only).</summary>
        public LetterVoice voice;
        /// <summary>A strange voice of their own (LetterCultureMath.Voices), or null.</summary>
        public string strange;
        /// <summary>How well the writer was schooled.</summary>
        public Schooling schooling;
        /// <summary>The rest of the tags lines are chosen by: their people's ways, culture, the facts, the season, the moment of a call.</summary>
        public readonly HashSet<string> tags = new HashSet<string>();
        /// <summary>The placeholders' values ({FROM}, {TO}, {TOWN}, {COLONY}, {PURPOSE}, {TOPIC}, the facts' own).</summary>
        public readonly Dictionary<string, string> values = new Dictionary<string, string>();
        /// <summary>The heading's place and date (made by <see cref="LetterFormMath.Date"/>); no place, no heading.</summary>
        public string place, date;
        /// <summary>The heart: what the letter is for, in the writer's words (filled).</summary>
        public string heart;
        /// <summary>More paragraphs in the writer's words after the heart (filled).</summary>
        public readonly List<string> more = new List<string>();
        /// <summary>The signature (made by <see cref="LetterFormMath.Signature"/>).</summary>
        public string sign;
        /// <summary>A call's names in its transcript: the colony's own speaker and theirs; the log's head line.</summary>
        public string us, them, callHead;
        /// <summary>A call: "call" (made and answered), "back" (they called back), "word" (they left word).</summary>
        public string callMode = "call";
        /// <summary>The narrator's plain lines under it, set apart: what it brings, why it went badly, how their regard moved.</summary>
        public readonly List<string> outcomes = new List<string>();
        // ---------------- The shape (Letters/LetterKindMath.cs) ----------------
        /// <summary>The shape it takes (LetterKindMath.Choose): the usual letter, a bare note, a long letter, a story, a poem...</summary>
        public LetterKind kind;
        /// <summary>Who writes, as the shapes are chosen by (their gifts, real news and raids, who else at home), or null.</summary>
        public LetterWho who;
        /// <summary>How long letters may get (the setting).</summary>
        public LetterLength length = LetterLength.Mixed;
        /// <summary>The shape's paragraphs after the heart (LetterKindMath.Build): the story, the poem, the parts.</summary>
        public readonly List<string> piece = new List<string>();
        /// <summary>The lines under the signature: others' added lines, what is folded in.</summary>
        public readonly List<string> after = new List<string>();
        /// <summary>The answer to the riddle this letter asks, to be given in the writer's next (kept with the letter).</summary>
        public string riddleAnswer;
        // ---------------- The correspondence (Letters/LetterThreadMath.cs) ----------------
        /// <summary>How well the two know each other by letter (None: not read, and no lines of its own).</summary>
        public Familiarity fam;
        /// <summary>An earlier letter's thread to take up in this one, news to tell first, or a cooled letter's cold word; or null.</summary>
        public RecallChoice recall;
        /// <summary>The tags of the lines drawn into it, as the game's picker found them: what the letter said (a joke begun, the colony's news).</summary>
        public readonly HashSet<string> drawn = new HashSet<string>();
        /// <summary>Set by the form when the thread was written into it.</summary>
        public bool recalledNow;
    }

    /// <summary>The fixed words of the form (the keyed strings, read once): the heading, the dates, the signatures, the P.S., the mark between the letter and the narrator.</summary>
    public sealed class LetterWords
    {
        public string heading = "{PLACE}, {DATE}", headingCourt = "Given at {PLACE}, {DATE}";
        public string dateCourtly = "the {ORD} day of {QUADRUM}, in the year {YEAR}", datePlain = "{DAY} {QUADRUM} {YEAR}", dateStandard = "{YEAR}.{Q}.{DD}";
        public string ps = "P.S. {LINE}", mark = "* * *", callLine = "{NAME}: {LINE}", quote = "\"{WORDS}\"";
        public string saluteFallback = "{TO},", closeFallback = "Yours,";
        public string signPerson = "{FROM}, {TITLE} of {TOWN}", signUntitled = "{FROM} of {TOWN}", signCouncil = "{COUNCIL}, by the hand of {FROM}";
        public string signNoble = "{FROM}, {TITLE}, at {TOWN}", signOurs = "{FROM}, for all of us at {COLONY}", signOursTitled = "{FROM}, {TITLE}, for all of us at {COLONY}";
        public string signMark = "X, the mark of {FROM}", signHelped = "{FROM}, written down for them by {HELPER}", signMany = "The many of {TOWN}";
        public string machineHead = "RECORD {DATE}\nFROM: {FROM}, {PLACE}\nTO: {TO}\nSUBJECT: {PURPOSE}", machinePoint = "{N}. {LINE}", machinePs = "ADDENDUM: {LINE}";
        // The shapes (Letters/LetterKindMath.cs).
        public string signShort = "{FROM}", adds = "{HAND} adds: {LINE}", answer = "The answer to my riddle: {ANSWER}";
        public string goesOnOne = "(The letter goes on for another page. Read the whole of it from this card, or with {NAME} on the Diplomacy screen's People.)";
        public string goesOnMany = "(The letter goes on for {N} more pages. Read the whole of it from this card, or with {NAME} on the Diplomacy screen's People.)";
        public string diaryCourtly = "The {ORD} day.", diaryPlain = "{DAY} {QUADRUM}.", diaryStandard = "{YEAR}.{Q}.{DD}:", diarySeason = "The first morning.|The second morning.|The third morning.";
        public string signed = "Signed: {NAMES}.", signedMore = "Signed: {NAMES}, and many more of {TOWN} besides.", and = "and";
    }

    /// <summary>A letter made: the writer's own words, the narrator's lines, and both with the mark between.</summary>
    public sealed class LetterText
    {
        /// <summary>The writer's whole letter, the narrator's lines, and what the game's own letter shows (the start of a long letter, with a note where the rest is).</summary>
        public string letter = "", note = "", full = "";
        /// <summary>The letter in pages, as the reading window shows it (LetterKindMath.Pages).</summary>
        public List<string> pages = new List<string>();
        /// <summary>The game's letter shows only the first page.</summary>
        public bool cut;
        /// <summary>Its shape.</summary>
        public LetterKind kind;
        /// <summary>The answer to the riddle it asks, or null.</summary>
        public string riddleAnswer;
        /// <summary>The thread it was given (<see cref="LetterSpec.recall"/>) was written into it.</summary>
        public bool recalled;
    }

    /// <summary>
    /// The player (9 November 2026): "letters exchanged from and to pawns should also read like actual letters, make a varied
    /// selection of interesting letters that read well and can also crack a joke sometimes, or be serious", and, of their tone,
    /// "they don't necessarily have to be warm, just according to the situation and motivation". The form shared by every
    /// letter a person writes, both ways: an optional heading (the place and the date as the writer would put it, by their
    /// age), a salutation that fits who writes to whom and how they stand, a few short paragraphs in the writer's own voice
    /// (an opening, the heart, now and then an aside), a closing and a signature in kind, now and then a P.S. What the game
    /// does with it (what it brings, why it went badly, how their regard moved) goes under it as the narrator's plain lines,
    /// set apart: never inside the writer's words. A call reads as a call (a transcript in the console's age), words passed
    /// on by mouth as words passed on, neither as a letter.
    /// <para>
    /// The tone comes from the situation: how they took it (warm to a refusal, at war), what the writer wants (a friend, a
    /// trader angling, an official's duty, a wary neighbour, a proud one), the purpose, hard times. A warm register
    /// ("warmly") only for a warm letter that is not business, or a courteous one between friends; a businesslike, wary or
    /// proud letter never warmer than courteous. The writer's speech: their own strange voice (LetterCultureMath), an
    /// unschooled hand, a medieval writer's thee and thou (the colony's own too, in its medieval age; never a tribe's or the
    /// Empire's), else plain; each speech draws only its own lines (<see cref="SpeechOf"/>). Pure (no game types):
    /// Tools/Tests checks the rules and makes whole letters from the real pools.
    /// </para>
    /// </summary>
    public static class LetterFormMath
    {
        // ================= The tags lines are chosen by =================

        public const string OursTag = "ours", TheirsTag = "theirs", WarTag = "war", PenFriendTag = "penfriend", CouncilTag = "council", EmpireTag = "empire";
        public const string JokeTag = "joke", EverydayTag = "everyday", SeriousTag = "serious", LightTag = "light", GraveTag = "grave";
        public const string LetterTag = "letter", CallTag = "call", SpokenTag = "spoken";
        /// <summary>A warm letter's register (not business, not wary, not proud), and a businesslike one.</summary>
        public const string WarmlyTag = "warmly", BusinessTag = "business";
        /// <summary>The speeches: medieval English, a barely schooled hand, an unschooled one. A strange voice is LetterCultureMath's sv: tag.</summary>
        public const string Olde = "olde", SomeSchool = "u:some", NoSchool = "u:none";

        /// <summary>
        /// Who writes to whom, as salutations, closings and the other parts' lines are tagged: at war; a council; the Empire;
        /// a pen friend; anyone else. Exactly one, in that order (a council at war writes as one at war).
        /// </summary>
        public const string AddrWar = "a:war", AddrCouncil = "a:council", AddrEmpire = "a:empire", AddrFriend = "a:friend", AddrPlain = "a:plain";

        public static readonly string[] Addresses = { AddrWar, AddrCouncil, AddrEmpire, AddrFriend, AddrPlain };

        public static readonly AnswerTone[] Tones = { AnswerTone.Warm, AnswerTone.Cordial, AnswerTone.Cool, AnswerTone.Curt, AnswerTone.Refusal };

        public static readonly WorldAge[] Ages = { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer };

        public static readonly LetterMotive[] Motives = { LetterMotive.Plain, LetterMotive.Friend, LetterMotive.Trade, LetterMotive.Duty, LetterMotive.Wary, LetterMotive.Proud };

        /// <summary>The speeches a line can be in: plain (null), medieval, barely or not schooled, and each strange voice.</summary>
        public static readonly string[] Speeches = { null, Olde, SomeSchool, NoSchool, LetterCultureMath.Machine, LetterCultureMath.Hive, LetterCultureMath.Beast,
            LetterCultureMath.Green, LetterCultureMath.Fey, LetterCultureMath.Ancient, LetterCultureMath.Deep };

        /// <summary>What their letters can be for: the colony's purposes, a council's answer, the world writing first, a reply's thanks, a friend's word.</summary>
        public static readonly string[] TheirPurposes =
        {
            "greetings", "condolences", "warning", "news", "invitation", "help", "favour", "pact", "truce", "ransom", "envoy", "ward", "match", "fealty", "takeback",
            "council", "first", "thanks", "friendmade", "friendfavour", "raidword"
        };

        /// <summary>What the colony's own letters can be for (every purpose the letter's window offers, a dealing and a reply to the world's letter).</summary>
        public static readonly string[] OurPurposes =
        {
            "greetings", "condolences", "warning", "news", "invitation", "help", "favour", "pact", "truce", "ransom", "envoy", "ward", "match", "fealty", "takeback",
            "dealing", "reply"
        };

        /// <summary>The purposes no letter jokes in: grief, danger, hunger, a war's business, captives.</summary>
        private static readonly HashSet<string> serious = new HashSet<string> { "condolences", "warning", "help", "truce", "ransom", "raidword" };

        /// <summary>The purposes that are the business between two peoples: written as a duty, never warmer than courteous.</summary>
        private static readonly HashSet<string> business = new HashSet<string>
            { "favour", "pact", "truce", "ransom", "envoy", "ward", "fealty", "takeback", "council", "dealing" };

        public static bool SeriousPurpose(string purpose) => purpose != null && serious.Contains(purpose);

        public static bool BusinessPurpose(string purpose) => purpose != null && business.Contains(purpose);

        public static string AgeTag(WorldAge a) => "w:" + AgesMath.Tag(a);

        public static string MotiveTag(LetterMotive m) => "m:" + m.ToString().ToLowerInvariant();

        public static string KindTag(AsideKind k) => k == AsideKind.Joke ? JokeTag : k == AsideKind.Serious ? SeriousTag : k == AsideKind.Everyday ? EverydayTag : null;

        /// <summary>The address: at war, a council, the Empire, a pen friend, else plain.</summary>
        public static string Address(LetterSpec s) =>
            s.war ? AddrWar : s.council ? AddrCouncil : s.empire ? AddrEmpire : s.penFriend ? AddrFriend : AddrPlain;

        /// <summary>A grave letter: at war, hard times, or a serious purpose. A light one may joke.</summary>
        public static bool Grave(LetterSpec s) => s.war || s.grave || SeriousPurpose(s.purpose);

        /// <summary>A businesslike letter: a council's, an official's duty, a trader's angling, or the business between two peoples.</summary>
        public static bool Business(LetterSpec s) =>
            s.council || s.motive == LetterMotive.Duty || s.motive == LetterMotive.Trade || BusinessPurpose(s.purpose);

        /// <summary>
        /// The warm register: a warm letter that is not business, nor wary, nor proud, nor at war; or a courteous one between
        /// friends (a pen friend, a warm neighbour; the colony's kind writer). The colony's abrasive writer never.
        /// </summary>
        public static bool Warmth(LetterSpec s)
        {
            if (s.war || Business(s) || s.motive == LetterMotive.Wary || s.motive == LetterMotive.Proud) return false;
            if (s.ours && s.voice.abrasive) return false;
            if (s.tone == AnswerTone.Warm) return true;
            return s.tone == AnswerTone.Cordial && (s.motive == LetterMotive.Friend || s.ours && s.voice.kind);
        }

        /// <summary>The tone as lines are tagged: a warm answer is only courteous when it is business, wary or proud.</summary>
        public static string ToneTag(LetterSpec s)
        {
            var t = s.tone;
            if (t == AnswerTone.Warm && (Business(s) || s.motive == LetterMotive.Wary || s.motive == LetterMotive.Proud)) t = AnswerTone.Cordial;
            return ToneTag(t);
        }

        public static string ToneTag(AnswerTone t) => t.ToString().ToLowerInvariant();

        /// <summary>
        /// The writer's speech, which lines are drawn from alone: a strange voice of their own; an unschooled or barely
        /// schooled hand; a medieval writer's English (the Empire keeps its own formal style, a tribe its own plain one); else
        /// plain (null).
        /// </summary>
        public static string SpeechOf(LetterSpec s)
        {
            if (!string.IsNullOrEmpty(s.strange)) return s.strange;
            if (s.schooling == Schooling.None) return NoSchool;
            if (s.schooling == Schooling.Some) return SomeSchool;
            if (s.age == WorldAge.Medieval && !s.empire) return Olde;
            return null;
        }

        /// <summary>
        /// The speech a letter's heart is drawn in: a medieval writer's, an unschooled one's and a hive's have hearts of their
        /// own; a barely schooled hand and the other strange voices bend the letter round the heart, which is plain.
        /// </summary>
        public static string HeartSpeech(string speech) => speech == Olde || speech == NoSchool || speech == LetterCultureMath.Hive ? speech : null;

        /// <summary>The tags a heart is chosen by: the letter's, in the heart's own speech (<see cref="HeartSpeech"/>).</summary>
        public static HashSet<string> HeartTags(LetterSpec s)
        {
            var t = Tags(s);
            string speech = SpeechOf(s);
            if (speech != null && HeartSpeech(speech) == null) t.Remove(speech);
            return t;
        }

        /// <summary>The pool a part is drawn from: Rimfolk_Pool_LetterSalute, ..._LetterOurs, ..._LetterCall.</summary>
        public static string PoolOf(LetterPart part) => "Rimfolk_Pool_Letter" + part;

        /// <summary>A line's speech: the olde, u: or sv: tag it carries (a plain line carries none).</summary>
        public static string SpeechOf(LineTags.Parsed p)
        {
            if (p?.tags == null) return null;
            foreach (var t in p.tags)
                if (t == Olde || t == SomeSchool || t == NoSchool || t.StartsWith("sv:", StringComparison.Ordinal))
                    return t;
            return null;
        }

        /// <summary>The voice as tags: v:plain, v:blunt or v:eloquent, and v:kind, v:abrasive, v:cheerful, v:gloomy, v:learned, v:proud.</summary>
        public static List<string> VoiceTags(LetterVoice v)
        {
            var list = new List<string> { v.main == VoiceMain.Blunt ? "v:blunt" : v.main == VoiceMain.Eloquent ? "v:eloquent" : "v:plain" };
            if (v.kind) list.Add("v:kind");
            if (v.abrasive) list.Add("v:abrasive");
            if (v.cheerful) list.Add("v:cheerful");
            if (v.gloomy) list.Add("v:gloomy");
            if (v.learned) list.Add("v:learned");
            if (v.proud) list.Add("v:proud");
            return list;
        }

        /// <summary>
        /// The tags a part of the letter is chosen by: the spec's own, the side, the age, the tone, the register, the motive,
        /// the address, light or grave, the shape, the speech, the voice, the aside's kind.
        /// </summary>
        public static HashSet<string> Tags(LetterSpec s, AsideKind kind = AsideKind.None)
        {
            var t = new HashSet<string>(s.tags) { s.ours ? OursTag : TheirsTag, AgeTag(s.age), ToneTag(s), Address(s), Grave(s) ? GraveTag : LightTag, MotiveTag(s.motive) };
            t.Add(s.shape == LetterShape.Call ? CallTag : s.shape == LetterShape.Spoken ? SpokenTag : LetterTag);
            if (Warmth(s)) t.Add(WarmlyTag);
            if (Business(s)) t.Add(BusinessTag);
            if (s.war) t.Add(WarTag);
            if (s.penFriend) t.Add(PenFriendTag);
            if (s.council) t.Add(CouncilTag);
            if (s.empire) t.Add(EmpireTag);
            if (!string.IsNullOrEmpty(s.purpose)) t.Add(s.purpose);
            string speech = SpeechOf(s);
            if (speech != null) t.Add(speech);
            if (s.ours)
                foreach (var v in VoiceTags(s.voice))
                    t.Add(v);
            t.Add(LetterKindMath.Tag(s.kind));
            // How well the two know each other by letter (fam:new ... fam:cooled).
            string fam = LetterThreadMath.Tag(s.fam);
            if (fam != null) t.Add(fam);
            string k = KindTag(kind);
            if (k != null) t.Add(k);
            return t;
        }

        // ================= Who writes, and why =================

        /// <summary>
        /// A writer of the colony's voice: blunt when abrasive or of little Social skill (4 or less), eloquent with a good
        /// deal of it (12 and Intellectual 6, or 15), plain otherwise; kind, abrasive, cheerful (an optimist), gloomy (a
        /// pessimist), learned (Intellectual 10), proud colour it. <paramref name="mood"/> is their outlook, -2 to 2.
        /// </summary>
        public static LetterVoice Voice(int social, int intellectual, bool kind, bool abrasive, int mood, bool proud = false)
        {
            var v = new LetterVoice { kind = kind, abrasive = abrasive, cheerful = mood > 0, gloomy = mood < 0, learned = intellectual >= 10, proud = proud };
            v.main = abrasive || social <= 4 ? VoiceMain.Blunt : social >= 15 || social >= 12 && intellectual >= 6 ? VoiceMain.Eloquent : VoiceMain.Plain;
            return v;
        }

        /// <summary>How the colony stands with them, for its own letter's tone: by their regard of it (at war is said apart).</summary>
        public static AnswerTone Stance(int regard) =>
            regard >= 40 ? AnswerTone.Warm : regard >= 5 ? AnswerTone.Cordial : regard >= -25 ? AnswerTone.Cool : AnswerTone.Curt;

        /// <summary>
        /// What the writer wants from the letter. At war, nothing but the letter. A pen friend writes as a friend. A council,
        /// the Empire and the business between two peoples are written as a duty. A writer who thinks little of the colony
        /// (regard under 5) or trusts nobody (trust -30 and under) sounds it out; a proud one (pride 40) won't ask outright;
        /// a trader (their role, or a people of traders) angles for business; a neighbour who thinks a good deal of the colony
        /// (50) writes as a friend. The colony's own: pen friends, duty for business, wary where they think little of it.
        /// </summary>
        public static LetterMotive Motive(bool ours, bool war, bool penFriend, bool council, bool empire, string purpose, int regard, bool trader, int pride, int trust)
        {
            if (war) return LetterMotive.Plain;
            if (penFriend) return LetterMotive.Friend;
            if (council || empire || BusinessPurpose(purpose)) return LetterMotive.Duty;
            if (regard < 5 || !ours && trust <= -30) return LetterMotive.Wary;
            if (!ours && pride >= 40) return LetterMotive.Proud;
            if (!ours && trader) return LetterMotive.Trade;
            if (regard >= 50) return LetterMotive.Friend;
            return LetterMotive.Plain;
        }

        /// <summary>
        /// How well a writer was schooled: their Intellectual skill, less four for an outlaw people, three for a slow learner,
        /// one for the tribal and medieval ages, six more for one too smart or a scholar; 0 and under not at all, 1 and 2
        /// barely. A council and the Empire have scribes.
        /// </summary>
        public static Schooling SchoolingOf(int intellectual, bool outlaw, bool slowLearner, bool bright, WorldAge age, bool council, bool empire)
        {
            if (council || empire) return Schooling.Schooled;
            int s = intellectual;
            if (outlaw) s -= 4;
            if (slowLearner) s -= 3;
            if (bright) s += 6;
            if (age <= WorldAge.Medieval) s -= 1;
            return s <= 0 ? Schooling.None : s <= 2 ? Schooling.Some : Schooling.Schooled;
        }

        /// <summary>
        /// One not schooled at all may have it written down by someone a little better (theirs: a third of the time; the
        /// colony's: half the time, by whoever is free and writes best), or send it as it is.
        /// </summary>
        public static bool Helped(bool ours, float roll) => roll < (ours ? 0.5f : 0.35f);

        // ================= What a letter gets =================

        /// <summary>
        /// Whether a letter has a heading: always from a council, the Empire or the ancient; never from the unschooled or a
        /// hive (it has its own head); else most letters (seven in ten), fewer when curt, a refusal or at war (four in ten).
        /// Only letters have one.
        /// </summary>
        public static bool Heading(LetterSpec s, float roll)
        {
            if (s.shape != LetterShape.Written) return false;
            string speech = SpeechOf(s);
            if (speech == NoSchool || speech == LetterCultureMath.Hive || speech == LetterCultureMath.Machine) return false;
            if (s.council || s.empire || speech == LetterCultureMath.Ancient) return true;
            return roll < (s.war || s.tone >= AnswerTone.Curt ? 0.4f : 0.7f);
        }

        /// <summary>
        /// The aside a letter gets. A council writes none. A refusal seldom (a serious line if it is grave); a curt letter
        /// seldom (serious if grave, else everyday); a grave letter a serious line, often; a light one an everyday detail or
        /// a joke: most often in a warm letter, less in a courteous or businesslike one, least in a cool one (a dry remark).
        /// A trader likes to add a line of business. How often a writer jokes follows their humour; the colony's blunt
        /// writers add less, the eloquent more, the cheerful joke more, the gloomy less; the Empire jokes seldom. A call and
        /// words passed on by mouth have none.
        /// </summary>
        public static AsideKind Aside(LetterSpec s, float roll)
        {
            if (s.shape != LetterShape.Written || s.council) return AsideKind.None;
            bool grave = Grave(s);
            if (s.tone >= AnswerTone.Refusal) return grave && roll < 0.2f ? AsideKind.Serious : AsideKind.None;
            if (s.tone == AnswerTone.Curt) return roll < 0.15f ? grave ? AsideKind.Serious : AsideKind.Everyday : AsideKind.None;
            bool warm = Warmth(s);
            float chance = warm ? 0.8f : s.tone == AnswerTone.Cool ? 0.35f : 0.5f;
            if (grave) chance = s.tone == AnswerTone.Cool ? 0.35f : 0.55f;
            if (s.motive == LetterMotive.Trade) chance += 0.2f;
            if (s.ours)
            {
                if (s.voice.main == VoiceMain.Blunt) chance -= 0.2f;
                else if (s.voice.main == VoiceMain.Eloquent) chance += 0.1f;
            }
            // A cooled correspondence says less, and jokes not at all; old friends run on a little.
            if (s.fam == Familiarity.Cooled) chance *= 0.5f;
            else if (s.fam == Familiarity.Close && !grave) chance += 0.1f;
            if (roll >= chance) return AsideKind.None;
            if (grave) return AsideKind.Serious;
            if (s.fam == Familiarity.Cooled) return AsideKind.Everyday;
            float joke = (warm ? 0.45f : 0.2f) + s.humour / 250f;
            if (s.ours && s.voice.cheerful) joke += 0.15f;
            if (s.ours && s.voice.gloomy) joke -= 0.2f;
            if (s.empire) joke *= 0.5f;
            joke = Math.Max(0f, Math.Min(0.85f, joke));
            return roll < chance * joke ? AsideKind.Joke : AsideKind.Everyday;
        }

        /// <summary>
        /// How likely a P.S. is: none on a call or words passed on; seldom from a council (3 in 100), at war or with a refusal
        /// (4), the Empire or a curt letter (8); a cool letter one in ten, a courteous one in five, a warm one three in ten; a
        /// friend a tenth more, a trader with something to sell more still; the eloquent a little more, the blunt a little
        /// less; the ancient nearly always (a postscript longer than the letter).
        /// </summary>
        public static float PSChance(LetterSpec s)
        {
            if (s.shape != LetterShape.Written) return 0f;
            if (SpeechOf(s) == LetterCultureMath.Ancient) return 0.85f;
            if (s.council) return 0.03f;
            if (s.war || s.tone >= AnswerTone.Refusal) return 0.04f;
            if (s.empire || s.tone == AnswerTone.Curt) return 0.08f;
            float c = Warmth(s) ? 0.3f : s.tone == AnswerTone.Cool ? 0.1f : 0.2f;
            if (s.motive == LetterMotive.Friend) c += 0.1f;
            if (s.motive == LetterMotive.Trade) c += 0.15f;
            if (s.ours && s.voice.main == VoiceMain.Eloquent) c += 0.05f;
            if (s.ours && s.voice.main == VoiceMain.Blunt) c -= 0.05f;
            // Old friends add a P.S. more often; a cooled correspondence seldom.
            if (s.fam == Familiarity.Close) c += 0.1f;
            else if (s.fam == Familiarity.Familiar) c += 0.05f;
            else if (s.fam == Familiarity.Cooled) c *= 0.3f;
            return c;
        }

        /// <summary>What a P.S. is: serious in a grave letter; a joke now and then in a light one that is not curt (half the time when warm, a dry one a fifth of the time otherwise); else an everyday line.</summary>
        public static AsideKind PSKind(LetterSpec s, float roll)
        {
            if (Grave(s)) return AsideKind.Serious;
            if (!s.empire && s.tone < AnswerTone.Curt && s.fam != Familiarity.Cooled && roll < (Warmth(s) ? 0.5f : 0.2f)) return AsideKind.Joke;
            return AsideKind.Everyday;
        }

        /// <summary>How a writer of this age dates a letter: a tribe by the season, the medieval age and the Empire in the court's words, the industrial plainly, the spacer age by the standard date.</summary>
        public static DateStyle DateStyleOf(WorldAge age, bool empire) =>
            empire ? DateStyle.Courtly : age == WorldAge.Tribal ? DateStyle.Season : age == WorldAge.Medieval ? DateStyle.Courtly : age == WorldAge.Industrial ? DateStyle.Plain : DateStyle.Standard;

        /// <summary>The date as the writer would put it (<paramref name="when"/>: a tribal writer's season, "when the geese came back").</summary>
        public static string Date(DateStyle style, LetterWords w, int day, string quadrum, int quadrumIndex, int year, string when)
        {
            switch (style)
            {
                case DateStyle.Season: return when ?? "";
                case DateStyle.Courtly: return Fill(w.dateCourtly, ("ORD", Ordinal(day)), ("QUADRUM", quadrum), ("YEAR", year.ToString()));
                case DateStyle.Plain: return Fill(w.datePlain, ("DAY", day.ToString()), ("QUADRUM", quadrum), ("YEAR", year.ToString()));
                default: return Fill(w.dateStandard, ("YEAR", year.ToString()), ("Q", (quadrumIndex + 1).ToString()), ("DD", day.ToString("00")));
            }
        }

        /// <summary>1st, 2nd, 3rd, 4th, 11th, 12th, 13th, 21st.</summary>
        public static string Ordinal(int n)
        {
            int t = n % 100;
            if (t >= 11 && t <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        /// <summary>
        /// How the letter is signed: a hive as its many; one who can't write their name with a mark; one helped by their
        /// helper's hand; the colony's own (with a title, the Empire's, if the writer holds one); a council; the Empire; a
        /// person with or without a title.
        /// </summary>
        public static SignStyle SignOf(bool ours, bool council, bool empire, bool titled, string speech = null, bool helped = false, bool note = false)
        {
            if (speech == LetterCultureMath.Hive) return SignStyle.Many;
            if (helped) return SignStyle.Helped;
            if (speech == NoSchool) return SignStyle.Mark;
            if (!ours && council) return SignStyle.Council;
            if (!ours && empire && titled) return SignStyle.Noble;
            // A bare note is signed with the name alone.
            if (note) return SignStyle.Short;
            return ours ? titled ? SignStyle.OursTitled : SignStyle.Ours : titled ? SignStyle.Person : SignStyle.PersonUntitled;
        }

        /// <summary>The signature, filled from the spec's values (FROM, TITLE, TOWN, COUNCIL, COLONY, HELPER).</summary>
        public static string Signature(SignStyle style, LetterWords w, IReadOnlyDictionary<string, string> values)
        {
            string f;
            switch (style)
            {
                case SignStyle.Person: f = w.signPerson; break;
                case SignStyle.PersonUntitled: f = w.signUntitled; break;
                case SignStyle.Council: f = w.signCouncil; break;
                case SignStyle.Noble: f = w.signNoble; break;
                case SignStyle.OursTitled: f = w.signOursTitled; break;
                case SignStyle.Mark: f = w.signMark; break;
                case SignStyle.Helped: f = w.signHelped; break;
                case SignStyle.Many: f = w.signMany; break;
                case SignStyle.Short: f = w.signShort; break;
                default: f = w.signOurs; break;
            }
            return Texts.Fill(f, values);
        }

        // ================= Choosing lines =================

        /// <summary>
        /// One of these lines that fits: in the writer's own speech (a line's olde, u: or sv: tag against the context's: a
        /// medieval writer draws only medieval lines, a plain writer none of them), every tag on it holding for
        /// <paramref name="tags"/> by LineTags' rules, weighted to the most specific (1 + 1.5 a tag that speaks to it), never
        /// one whose placeholders have no value, and none of <paramref name="avoid"/> (as written, tags removed) while another
        /// fits. The line without its tags, unfilled; null when none fits.
        /// </summary>
        public static string Pick(IList<string> lines, HashSet<string> tags, IReadOnlyDictionary<string, string> values, ICollection<string> avoid, Func<float> roll)
        {
            if (lines == null || lines.Count == 0) return null;
            string found = PickFrom(lines, tags, values, avoid, roll);
            return found ?? (avoid != null && avoid.Count > 0 ? PickFrom(lines, tags, values, null, roll) : null);
        }

        /// <summary>The speech a context asks for: the olde, u: or sv: tag in it, or null for plain.</summary>
        public static string ContextSpeech(HashSet<string> tags)
        {
            if (tags == null) return null;
            foreach (var t in tags)
                if (t == Olde || t == SomeSchool || t == NoSchool || t.StartsWith("sv:", StringComparison.Ordinal))
                    return t;
            return null;
        }

        /// <summary>Whether a line fits the context: its speech and every tag (-1 when not; else how many of its tags speak to it).</summary>
        public static int Fits(string line, HashSet<string> tags)
        {
            var p = LineTags.Of(line);
            if (p == null || p.text == null) return -1;
            if (SpeechOf(p) != ContextSpeech(tags)) return -1;
            return LineTags.Match(p, tags);
        }

        private static string PickFrom(IList<string> lines, HashSet<string> tags, IReadOnlyDictionary<string, string> values, ICollection<string> avoid, Func<float> roll)
        {
            var fits = new List<(string text, float w)>();
            float total = 0f;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                int m = Fits(line, tags);
                if (m < 0) continue;
                string text = LineTags.Of(line).text;
                if (avoid != null && avoid.Contains(text)) continue;
                if (!Fillable(text, values)) continue;
                float w = 1f + 1.5f * m;
                fits.Add((text, w));
                total += w;
            }
            if (fits.Count == 0) return null;
            float r = (roll != null ? roll() : 0.5f) * total;
            foreach (var (text, w) in fits)
            {
                if (r < w) return text;
                r -= w;
            }
            return fits[fits.Count - 1].text;
        }

        /// <summary>Whether any of these lines fits (its speech, its tags, a value for each placeholder): nothing drawn.</summary>
        public static bool Any(IList<string> lines, HashSet<string> tags, IReadOnlyDictionary<string, string> values)
        {
            if (lines == null) return false;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || Fits(line, tags) < 0) continue;
                if (Fillable(LineTags.Of(line).text, values)) return true;
            }
            return false;
        }

        /// <summary>Every placeholder of the line has a value.</summary>
        public static bool Fillable(string text, IReadOnlyDictionary<string, string> values)
        {
            foreach (var h in Texts.Holes(text))
                if (values == null || !values.TryGetValue(h, out var v) || string.IsNullOrEmpty(v))
                    return false;
            return true;
        }

        public static string Fill(string text, params (string key, string value)[] values) => Texts.Fill(text, values);

        // ================= Making the letter =================

        /// <summary>
        /// The letter made from the spec: its parts drawn by <paramref name="pick"/> (a part and its tags: the line, filled,
        /// or null), the dice by <paramref name="roll"/>. A letter: the heading (sometimes), the salutation, the opening,
        /// the heart, more of their words, an aside (sometimes), the closing and the signature, a P.S. (sometimes); a machine
        /// writes a record with numbered points, a hive its many's words with no salutation. A call: its log's head and the
        /// spoken lines, each "Name: words". Words passed on: the carrier's words round the sender's, in quotes. Under every
        /// shape, set apart by the mark, the narrator's lines (<see cref="LetterSpec.outcomes"/>), never inside the writer's words.
        /// </summary>
        public static LetterText Compose(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll)
        {
            w ??= new LetterWords();
            roll ??= () => 0.5f;
            s.recalledNow = false;
            string letter;
            switch (s.shape)
            {
                case LetterShape.Call: letter = Call(s, w, pick); break;
                case LetterShape.Spoken: letter = Spoken(s, w, pick); break;
                default:
                    string speech = SpeechOf(s);
                    letter = speech == LetterCultureMath.Machine ? Machine(s, w, pick, roll) : Written(s, w, pick, roll, speech != LetterCultureMath.Hive);
                    break;
            }
            var note = new List<string>();
            foreach (var o in s.outcomes)
                if (!string.IsNullOrEmpty(o))
                    note.Add(o.Trim());
            var t = new LetterText { letter = letter, note = string.Join("\n\n", note), kind = s.kind, riddleAnswer = s.riddleAnswer, recalled = s.recalledNow };
            t.pages = LetterKindMath.Pages(letter);
            // A long letter: the game's own letter shows its first page, and says where the rest is.
            t.cut = letter.Length > LetterKindMath.ShowMax && t.pages.Count > 1;
            string shown = letter;
            if (t.cut)
            {
                int more = t.pages.Count - 1;
                string name = s.values.TryGetValue("FROM", out var from) ? from : "";
                shown = t.pages[0] + "\n\n" + Fill(more == 1 ? w.goesOnOne : w.goesOnMany, ("N", more.ToString()), ("NAME", name));
            }
            t.full = t.note.Length == 0 ? shown : shown + "\n\n" + w.mark + "\n\n" + t.note;
            return t;
        }

        private static string Written(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll, bool salute)
        {
            if (s.kind == LetterKind.Note) return Note(s, w, pick, roll, salute);
            var paras = new List<string>();
            if (Heading(s, roll()) && !string.IsNullOrEmpty(s.place))
                paras.Add(string.IsNullOrEmpty(s.date) ? s.place : Fill(s.empire && !s.ours ? w.headingCourt : w.heading, ("PLACE", s.place), ("DATE", s.date)));
            var tags = Tags(s);
            // The stage's own words first now and then (how well the two know each other by letter), else the usual ones.
            if (salute) paras.Add(Familiar(s, pick, roll, tags, LetterThreadMath.SaluteTag) ?? pick(LetterPart.Salute, tags) ?? Texts.Fill(w.saluteFallback, s.values));
            Add(paras, Opening(s, pick, roll, tags) ?? pick(LetterPart.Open, tags));
            Add(paras, s.heart);
            foreach (var m in s.more) Add(paras, m);
            // An earlier letter's thread taken up (a question answered, news followed, a gift remembered), now and then as a P.S.
            string recallPs = s.recall?.ps == true && string.IsNullOrEmpty(s.who?.owedAnswer) ? Recall(s, pick, tags, true) : null;
            if (recallPs == null) Add(paras, Recall(s, pick, tags, false));
            // The shape's own paragraphs: the story, the poem, the parts of a long letter.
            foreach (var p in s.piece) Add(paras, p);
            var kind = AsideOf(s) ? Aside(s, roll()) : AsideKind.None;
            if (kind != AsideKind.None) Add(paras, pick(LetterPart.Aside, Tags(s, kind)));
            string close = Familiar(s, pick, roll, tags, LetterThreadMath.CloseTag) ?? pick(LetterPart.Close, tags) ?? w.closeFallback;
            paras.Add(string.IsNullOrEmpty(s.sign) ? close : close + "\n" + s.sign);
            // Under the signature: others' lines, what is folded in.
            foreach (var a in s.after) Add(paras, a);
            string ps = recallPs ?? PS(s, w, pick, roll);
            if (!string.IsNullOrEmpty(ps)) paras.Add(Fill(w.ps, ("LINE", ps.Trim())));
            return string.Join("\n\n", paras);
        }

        /// <summary>
        /// A part in the stage's own words (Rimfolk_Pool_LetterFamiliar, <paramref name="part"/> fa:salute, fa:close), by
        /// <see cref="LetterThreadMath.FamiliarChance"/>; null for the usual pool's line.
        /// </summary>
        private static string Familiar(LetterSpec s, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll, HashSet<string> tags, string part)
        {
            if (s.fam == Familiarity.None || roll() >= LetterThreadMath.FamiliarChance(s.fam, part)) return null;
            return pick(LetterPart.Familiar, Own(With(tags, part)));
        }

        /// <summary>
        /// The opening in the stage's own words now and then: a new hand writing for the colony says so (fa:pen), a long
        /// silence is spoken of (fa:long), else the stage's opening (fa:open); null for the usual pool's.
        /// </summary>
        private static string Opening(LetterSpec s, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll, HashSet<string> tags)
        {
            if (s.fam == Familiarity.None) return null;
            string line = null;
            if (tags.Contains(LetterThreadMath.PenTag) && roll() < 0.8f) line = pick(LetterPart.Familiar, Own(With(tags, LetterThreadMath.PenPart)));
            if (line == null && tags.Contains(LetterThreadMath.LongTag) && roll() < 0.8f) line = pick(LetterPart.Familiar, Own(With(tags, LetterThreadMath.LongPart)));
            return line ?? Familiar(s, pick, roll, tags, LetterThreadMath.OpenTag);
        }

        /// <summary>The correspondence's lines in the writer's speech: a barely schooled hand draws the plain ones (as its pieces).</summary>
        private static HashSet<string> Own(HashSet<string> tags)
        {
            tags.Remove(SomeSchool);
            return tags;
        }

        /// <summary>
        /// The thread this letter takes up (<see cref="LetterSpec.recall"/>), in the writer's words (Rimfolk_Pool_LetterThread,
        /// re:KEY:MOVE with {RE}; re:last when the other side's last letter raised or took it up; at:ps for a P.S.), or null.
        /// </summary>
        private static string Recall(LetterSpec s, Func<LetterPart, HashSet<string>, string> pick, HashSet<string> tags, bool ps)
        {
            var r = s.recall;
            if (r == null || string.IsNullOrEmpty(r.tag) || s.recalledNow) return null;
            s.values["RE"] = r.value ?? "";
            var t = Own(With(tags, r.tag));
            if (r.last) t.Add("re:last");
            if (ps) t.Add("at:ps");
            string line = pick(LetterPart.Thread, t);
            if (!string.IsNullOrEmpty(line)) s.recalledNow = true;
            return line;
        }

        /// <summary>
        /// A bare note: a salutation of the name alone now and then, a word to start it now and then ("In haste."), the
        /// heart, a word to end it now and then, the name. A council's and the Empire's keep their heading.
        /// </summary>
        private static string Note(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll, bool salute)
        {
            var paras = new List<string>();
            if ((s.council || s.empire) && !string.IsNullOrEmpty(s.place) && Heading(s, roll()))
                paras.Add(string.IsNullOrEmpty(s.date) ? s.place : Fill(s.empire && !s.ours ? w.headingCourt : w.heading, ("PLACE", s.place), ("DATE", s.date)));
            var tags = Tags(s);
            if (salute && roll() < 0.6f) paras.Add(Texts.Fill(w.saluteFallback, s.values));
            if (roll() < 0.3f) Add(paras, pick(LetterPart.Note, With(tags, "note:open")));
            Add(paras, s.heart);
            foreach (var m in s.more) Add(paras, m);
            // A bare note takes up no thread, but a cooled one says its cold word.
            if (s.recall?.move == "slight") Add(paras, Recall(s, pick, tags, false));
            string end = roll() < 0.55f ? pick(LetterPart.Note, With(tags, "note:close")) : null;
            if (!string.IsNullOrEmpty(end)) paras.Add(end.Trim());
            if (!string.IsNullOrEmpty(s.sign)) paras.Add(s.sign);
            // An owed answer to a riddle goes even in a note.
            string ps = s.who?.owedAnswer != null ? PS(s, w, pick, roll) : null;
            if (!string.IsNullOrEmpty(ps)) paras.Add(Fill(w.ps, ("LINE", ps)));
            return string.Join("\n\n", paras);
        }

        /// <summary>The tags with one more.</summary>
        public static HashSet<string> With(HashSet<string> tags, string more) => new HashSet<string>(tags) { more };

        /// <summary>Whether an aside goes in: in the usual letter and the short shapes, never in a long one, a poem, a sermon or a petition.</summary>
        public static bool AsideOf(LetterSpec s) =>
            s.kind == LetterKind.Usual || s.kind == LetterKind.Hands || s.kind == LetterKind.Enclosure || s.kind == LetterKind.Recipe || s.kind == LetterKind.Riddle
            || s.kind == LetterKind.Account;

        /// <summary>
        /// The P.S., or null: the answer to the riddle of the writer's last letter when one is owed (always given), else
        /// one by the usual chance and kind.
        /// </summary>
        private static string PS(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll)
        {
            string owed = s.who?.owedAnswer;
            if (!string.IsNullOrEmpty(owed))
            {
                if (!s.values.ContainsKey("ANSWER")) s.values["ANSWER"] = owed;
                return pick(LetterPart.Set, With(Tags(s), "ri:answer")) ?? Fill(w.answer, ("ANSWER", owed));
            }
            if (roll() >= PSChance(s)) return null;
            return pick(LetterPart.PS, Tags(s, PSKind(s, roll())));
        }

        /// <summary>A machine's letter: a stamped record for a head, its points numbered, a closing line, the sign, an addendum for a P.S.</summary>
        private static string Machine(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick, Func<float> roll)
        {
            var paras = new List<string>();
            var values = new Dictionary<string, string>(s.values) { ["DATE"] = s.date ?? "", ["PLACE"] = s.place ?? "" };
            paras.Add(Texts.Fill(w.machineHead, values));
            var tags = Tags(s);
            var points = new List<string>();
            void Point(string p)
            {
                if (!string.IsNullOrEmpty(p)) points.Add(Fill(w.machinePoint, ("N", (points.Count + 1).ToString()), ("LINE", p.Trim())));
            }
            bool note = s.kind == LetterKind.Note;
            if (!note) Point(Opening(s, pick, roll, tags) ?? pick(LetterPart.Open, tags));
            Point(s.heart);
            foreach (var m in s.more) Point(m);
            if (!note || s.recall?.move == "slight") Point(Recall(s, pick, tags, false));
            var kind = !note && AsideOf(s) ? Aside(s, roll()) : AsideKind.None;
            if (kind != AsideKind.None) Point(pick(LetterPart.Aside, Tags(s, kind)));
            if (points.Count > 0) paras.Add(string.Join("\n", points));
            // The shape's own paragraphs (an incident report, a log, an inventory), after the points.
            foreach (var p in s.piece) Add(paras, p);
            string close = note ? null : Familiar(s, pick, roll, tags, LetterThreadMath.CloseTag) ?? pick(LetterPart.Close, tags);
            paras.Add(string.IsNullOrEmpty(close) ? s.sign ?? "" : string.IsNullOrEmpty(s.sign) ? close : close + "\n" + s.sign);
            foreach (var a in s.after) Add(paras, a);
            string ps = note && s.who?.owedAnswer == null ? null : PS(s, w, pick, roll);
            if (!string.IsNullOrEmpty(ps)) paras.Add(Fill(w.machinePs, ("LINE", ps)));
            return string.Join("\n\n", paras);
        }

        /// <summary>
        /// A call's transcript: the log's head, then the spoken lines. Made and answered: the caller hails, they pick up,
        /// they say what they have to say, the caller answers, they sign off. Called back: they hail, the colony picks up,
        /// and the same. Left word: their recorded words alone.
        /// </summary>
        private static string Call(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(s.callHead)) lines.Add(s.callHead);
            // Spoken on the console: the frame's lines are plain speech whoever speaks (their heart keeps their own).
            string speech = SpeechOf(s);
            void Say(string who, string moment)
            {
                var t = Tags(s);
                if (speech != null) t.Remove(speech);
                t.Add("call:" + moment);
                string line = pick(LetterPart.Call, t);
                if (!string.IsNullOrEmpty(line)) lines.Add(Fill(w.callLine, ("NAME", who), ("LINE", line)));
            }
            if (s.callMode == "word")
            {
                Say(s.them, "word");
                Heart(lines, w, s.them, s.heart);
                Say(s.them, "wordbye");
            }
            else
            {
                if (s.callMode == "back")
                {
                    Say(s.them, "back");
                    Say(s.us, "answer");
                }
                else
                {
                    Say(s.us, "hail");
                    Say(s.them, "pickup");
                }
                Heart(lines, w, s.them, s.heart);
                Say(s.us, "reply");
                Say(s.them, "bye");
            }
            return string.Join("\n", lines);
        }

        private static void Heart(List<string> lines, LetterWords w, string who, string heart)
        {
            if (!string.IsNullOrEmpty(heart)) lines.Add(Fill(w.callLine, ("NAME", who), ("LINE", heart)));
        }

        /// <summary>Words passed on by mouth: the carrier's frame, the sender's own words in quotes (the heart and more of their words), the carrier's last.</summary>
        private static string Spoken(LetterSpec s, LetterWords w, Func<LetterPart, HashSet<string>, string> pick)
        {
            var paras = new List<string>();
            // The carrier's own words: a medieval carrier's English, else plain (the sender's heart keeps theirs).
            string speech = SpeechOf(s);
            var open = Tags(s);
            if (speech != null && speech != Olde) open.Remove(speech);
            open.Add("s:open");
            Add(paras, pick(LetterPart.Spoken, open));
            var words = new StringBuilder(s.heart ?? "");
            foreach (var m in s.more)
                if (!string.IsNullOrEmpty(m))
                    words.Append(' ').Append(m);
            if (words.Length > 0) paras.Add(Fill(w.quote, ("WORDS", words.ToString().Trim())));
            var close = Tags(s);
            if (speech != null && speech != Olde) close.Remove(speech);
            close.Add("s:close");
            Add(paras, pick(LetterPart.Spoken, close));
            return string.Join("\n\n", paras);
        }

        private static void Add(List<string> paras, string p)
        {
            if (!string.IsNullOrEmpty(p)) paras.Add(p.Trim());
        }

        // ================= On its event card =================

        /// <summary>
        /// A received letter's text on its event card taken apart, so the letter itself is drawn on paper (the player, 10
        /// November 2026: "the sent letter had a really cool style that looked like a real letter while the received one is
        /// here in the plain event design"): what the teller put before it (<paramref name="before"/>: "At Riverhold."),
        /// the writer's own words as the card shows them (<paramref name="paper"/>, found by the kept letter's first words),
        /// a long letter's note where the rest is (<paramref name="more"/>), and the narrator's lines under the mark
        /// (<paramref name="under"/>). False when the kept letter's start isn't in the text (the letter is drawn plain).
        /// </summary>
        public static bool OnPaper(string shown, string kept, LetterWords w, out string before, out string paper, out string more, out string under)
        {
            before = paper = more = under = "";
            if (string.IsNullOrEmpty(shown) || string.IsNullOrEmpty(kept)) return false;
            w ??= new LetterWords();
            string head = kept.Length > 60 ? kept.Substring(0, 60) : kept;
            int at = shown.IndexOf(head, StringComparison.Ordinal);
            if (at < 0) return false;
            before = shown.Substring(0, at).Trim();
            string rest = shown.Substring(at);
            string mark = "\n\n" + w.mark + "\n\n";
            int m = rest.IndexOf(mark, StringComparison.Ordinal);
            string letter = m < 0 ? rest : rest.Substring(0, m);
            under = m < 0 ? "" : rest.Substring(m + mark.Length).Trim();
            // A long letter's note where the rest is: its last paragraph, as the form wrote it.
            string goesOn = w.goesOnMany.IndexOf('{') > 0 ? w.goesOnMany.Substring(0, w.goesOnMany.IndexOf('{')) : w.goesOnMany;
            int last = letter.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (last > 0 && goesOn.Length > 4 && letter.Substring(last + 2).StartsWith(goesOn, StringComparison.Ordinal))
            {
                more = letter.Substring(last + 2).Trim();
                letter = letter.Substring(0, last);
            }
            paper = letter.Trim();
            return paper.Length > 0;
        }

        // ================= Kept =================

        /// <summary>A letter kept for reading later is cut to this many characters (the longest usual letter is well under it).</summary>
        public const int MaxKept = 2400;

        /// <summary>A long letter (over MaxKept) is kept to this many, the newest LetterBook.MaxLong of them; older ones are cut to MaxKept.</summary>
        public const int MaxKeptLong = 8000;

        /// <summary>The narrator's lines kept with it, at most.</summary>
        public const int MaxNote = 900;

        /// <summary>Text kept no longer than <paramref name="max"/>: cut at the last paragraph or sentence that fits.</summary>
        public static string Cap(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            string cut = text.Substring(0, max);
            int at = cut.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (at < max / 2) at = cut.LastIndexOf(". ", StringComparison.Ordinal) + 1;
            return (at > max / 4 ? cut.Substring(0, at) : cut).TrimEnd();
        }
    }
}
