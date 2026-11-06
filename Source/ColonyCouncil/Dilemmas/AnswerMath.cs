using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How an answered question went, as its record in the history keeps it (<see cref="AnswerMath.Went"/>).</summary>
    public enum AnswerWent
    {
        /// <summary>It happened as the answer said (nothing set going on the map to wait for).</summary>
        AsAnswered,
        /// <summary>The answer's gamble went wrong.</summary>
        WentWrong,
        /// <summary>What it set going on the map is waiting for its moment.</summary>
        Waiting,
        /// <summary>What it set going is happening now.</summary>
        UnderWay,
        /// <summary>What it set going took place.</summary>
        TookPlace,
        /// <summary>What it set going never came about.</summary>
        FellThrough
    }

    /// <summary>
    /// The answers' log in the history: every question letter Rimfolk asks (a dilemma, an event, a world event, a camp's
    /// word, each of diplomacy's offers, a calendar day, a meeting, an election, a brigands' hold's terms, a hamlet, a
    /// journey at the gate) writes one event of the family "answer" when it is answered or its time runs out: the
    /// question, the answer, the follow-up told, and later what came of what it set going on the map. Its sources, the
    /// chronicle's kind each shows under, and its values as saved. Pure: Tools/Tests checks it.
    /// </summary>
    public static class AnswerMath
    {
        public const string Family = "answer";

        /// <summary>Every kind of question letter, by its source word, and the chronicle's kind its answer shows under.</summary>
        public static readonly (string source, ChronicleKind kind, string letter)[] Sources =
        {
            ("dilemma", ChronicleKind.Council, "ChoiceLetter_Dilemma"),
            ("event", ChronicleKind.World, "ChoiceLetter_Happening"),
            ("worldEvent", ChronicleKind.World, "ChoiceLetter_Situation"),
            ("camp", ChronicleKind.Stranger, "ChoiceLetter_Camp"),
            ("calendar", ChronicleKind.Council, "ChoiceLetter_CalendarDay"),
            ("meeting", ChronicleKind.Meeting, "ChoiceLetter_CouncilMeeting"),
            ("election", ChronicleKind.Council, "ChoiceLetter_Election"),
            ("raidersCamp", ChronicleKind.World, "ChoiceLetter_RaidersCamp"),
            ("hamlet", ChronicleKind.World, "ChoiceLetter_Hamlet"),
            ("journey", ChronicleKind.World, "ChoiceLetter_Journey"),
            ("callToArms", ChronicleKind.World, "ChoiceLetter_CallToArms"),
            ("envoy", ChronicleKind.World, "ChoiceLetter_EnvoyOffer"),
            ("ransom", ChronicleKind.World, "ChoiceLetter_RansomOffer"),
            ("ransomByHand", ChronicleKind.World, "ChoiceLetter_RansomByHand"),
            ("vassal", ChronicleKind.World, "ChoiceLetter_VassalAsk"),
            ("ward", ChronicleKind.World, "ChoiceLetter_WardOffer"),
            ("hostage", ChronicleKind.World, "ChoiceLetter_HostageForfeit"),
            ("match", ChronicleKind.World, "ChoiceLetter_MatchOffer"),
            ("feast", ChronicleKind.World, "ChoiceLetter_FeastSuggest"),
            ("favour", ChronicleKind.World, "ChoiceLetter_FavourAsk"),
            ("fealty", ChronicleKind.World, "ChoiceLetter_FealtyOffer"),
            ("pact", ChronicleKind.World, "ChoiceLetter_PactOffer"),
            ("truce", ChronicleKind.World, "ChoiceLetter_TruceOffer")
        };

        /// <summary>The word for a small answer: kept in the history and the answers' log, but not one for the chronicle (an event that was never chronicled, "let the vote run").</summary>
        public const string Minor = "minor";

        /// <summary>The history kind of an answer from this source: "answer:dilemma", or "answer:event:minor" for a small one.</summary>
        public static string KindFor(string source, bool minor = false) => Family + ":" + (string.IsNullOrEmpty(source) ? "?" : source) + (minor ? ":" + Minor : "");

        /// <summary>A small answer (<see cref="Minor"/>).</summary>
        public static bool IsMinor(string kind) => kind != null && SourceOf(kind) != null && kind.EndsWith(":" + Minor, StringComparison.Ordinal);

        /// <summary>The source of an answer's kind ("dilemma" of "answer:dilemma"), or null for one that isn't an answer.</summary>
        public static string SourceOf(string kind)
        {
            if (kind == null || !HistoryKinds.Matches(kind, Family) || kind.Length <= Family.Length + 1) return null;
            string rest = kind.Substring(Family.Length + 1);
            int colon = rest.IndexOf(':');
            return colon < 0 ? rest : rest.Substring(0, colon);
        }

        public static bool Known(string source)
        {
            foreach (var s in Sources)
                if (s.source == source)
                    return true;
            return false;
        }

        /// <summary>The chronicle's kind an answer shows under (by its source; the council's for one it doesn't know).</summary>
        public static ChronicleKind KindOf(string kind)
        {
            string source = SourceOf(kind);
            foreach (var s in Sources)
                if (s.source == source)
                    return s.kind;
            return ChronicleKind.Council;
        }

        // ---------------- Its values ----------------

        /// <summary>Where each value sits in an answer's list: the question, the answer, the follow-up told, how it went, a word on it, and "d" when nobody answered.</summary>
        public const int Question = 0, Answer = 1, Outcome = 2, WentAt = 3, NoteAt = 4, ByAt = 5, Count = 6;

        /// <summary>The longest follow-up kept with an answer: its first sentence, cut at a word past this.</summary>
        public const int OutcomeMax = 200;

        private static readonly string[] WentCodes = { "as", "wrong", "wait", "under", "took", "fell" };

        public static string Code(AnswerWent w) => WentCodes[(int)w];

        public static AnswerWent WentOf(string code)
        {
            int i = Array.IndexOf(WentCodes, code);
            return i < 0 ? AnswerWent.AsAnswered : (AnswerWent)i;
        }

        /// <summary>An answer's values as written: what it set going is waiting (or under way at once), else as answered or gone wrong.</summary>
        public static List<string> Values(string question, string answer, string outcome, bool byDefault, bool risk, bool setGoing)
        {
            var v = new List<string>(Count);
            for (int i = 0; i < Count; i++) v.Add("");
            v[Question] = question ?? "";
            v[Answer] = answer ?? "";
            v[Outcome] = Short(outcome);
            v[WentAt] = Code(risk ? AnswerWent.WentWrong : setGoing ? AnswerWent.Waiting : AnswerWent.AsAnswered);
            v[ByAt] = byDefault ? "d" : "";
            return v;
        }

        public static string Get(IReadOnlyList<string> values, int at) => values != null && at < values.Count ? values[at] ?? "" : "";

        public static AnswerWent Went(IReadOnlyList<string> values) => WentOf(Get(values, WentAt));

        public static bool ByDefault(IReadOnlyList<string> values) => Get(values, ByAt) == "d";

        /// <summary>
        /// What came of it, once the watching ends: how many of the things it set going took place and fell through (a gamble
        /// gone wrong stays so). Changes the values in place; true when they changed.
        /// </summary>
        public static bool Settle(List<string> values, int tookPlace, int fellThrough, string note)
        {
            if (values == null || values.Count < Count) return false;
            var was = Went(values);
            if (was == AnswerWent.WentWrong) return SetNote(values, note);
            var now = tookPlace > 0 ? AnswerWent.TookPlace : fellThrough > 0 ? AnswerWent.FellThrough : AnswerWent.AsAnswered;
            bool changed = was != now;
            values[WentAt] = Code(now);
            return SetNote(values, note) | changed;
        }

        /// <summary>What it set going was seen under way.</summary>
        public static bool Started(List<string> values)
        {
            if (values == null || values.Count < Count || Went(values) != AnswerWent.Waiting) return false;
            values[WentAt] = Code(AnswerWent.UnderWay);
            return true;
        }

        private static bool SetNote(List<string> values, string note)
        {
            if (string.IsNullOrEmpty(note) || values[NoteAt] == note) return false;
            values[NoteAt] = note;
            return true;
        }

        /// <summary>The follow-up's first sentence, at most <see cref="OutcomeMax"/> letters (cut at a word).</summary>
        public static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            int stop = text.IndexOf(". ", StringComparison.Ordinal);
            if (stop > 0 && stop < OutcomeMax) return text.Substring(0, stop + 1);
            if (text.Length <= OutcomeMax) return text;
            int cut = text.LastIndexOf(' ', OutcomeMax);
            return (cut > 40 ? text.Substring(0, cut) : text.Substring(0, OutcomeMax)).TrimEnd(',', ';', ':') + "...";
        }
    }
}
