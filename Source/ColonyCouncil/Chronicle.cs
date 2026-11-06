using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One line told for the colony's chronicle, as a system writes it (<see cref="CouncilGameComponent.AddChronicle"/>):
    /// its words, its kind and the colonist it is mostly about. It goes into the one history, which keeps it; nothing holds
    /// the entry itself. An older save's chronicle list is read back as these, taken into the history once and let go.
    /// </summary>
    public class ChronicleEntry : IExposable
    {
        public int tick;
        public ChronicleKind kind;
        public string text;
        /// <summary>ThingID of the colonist the entry is mostly about.</summary>
        public string pawnId;
        /// <summary>That colonist, only until the entry is added (the history asks where they belong); never saved.</summary>
        [Unsaved] public Pawn about;

        public ChronicleEntry()
        {
        }

        /// <summary>
        /// A milestone of the colony's history (a loss, an ambition won or lost, a life event, a world event, a secret out):
        /// the history keeps these as they are, while older everyday lines fold into their season's summary.
        /// </summary>
        public bool Milestone =>
            kind == ChronicleKind.Loss || kind == ChronicleKind.AmbitionAchieved || kind == ChronicleKind.AmbitionFailed || kind == ChronicleKind.Life
            || kind == ChronicleKind.World || kind == ChronicleKind.Secret;

        public ChronicleEntry(ChronicleKind kind, string text, Pawn pawn = null)
        {
            tick = Find.TickManager.TicksGame;
            this.kind = kind;
            // A line of the chronicle is a sentence: a label that leads it ("the harvest dance became a tradition") is capitalized.
            this.text = text.CapitalizeFirst();
            pawnId = pawn?.ThingID;
            about = pawn;
        }

        /// <summary>Only an older save's chronicle is read: these are never written again (the history keeps the lines).</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref pawnId, "pawnId");
        }
    }

    /// <summary>One line of the chronicle as the colony's windows show it: an event of the history, the chronicle's kind it shows under, and its words.</summary>
    public sealed class ChronicleLine
    {
        public readonly HistoryEntry entry;
        public readonly ChronicleKind kind;
        /// <summary>Its words, without links (<see cref="Chronicle.Words"/>).</summary>
        public readonly string text;
        /// <summary>The colonist it is mostly about (its first person), or none.</summary>
        public readonly Ref about;

        public ChronicleLine(HistoryEntry e, string text)
        {
            entry = e;
            kind = ChronicleMath.ShownAs(e);
            this.text = text ?? "";
            about = Ref.None;
            if (e?.actors != null)
                foreach (var a in e.actors)
                    if (a.who.kind == RefKind.Person)
                    {
                        about = a.who;
                        break;
                    }
        }

        public int tick => entry.tick;

        public bool Milestone => entry.Weighty;

        /// <summary>A season's summary of small lines (its words count them).</summary>
        public bool Summary => entry.folded;

        /// <summary>A question's answer (the answers' log).</summary>
        public bool Answer => HistoryKinds.Matches(entry.kind, AnswerMath.Family);

        /// <summary>The colonist it is about is this one.</summary>
        public bool IsAbout(Pawn p) => p != null && about == Refs.Of(p);
    }

    /// <summary>
    /// The colony's chronicle: its view of the one history (World/Ledger/History.cs), newest first. Every line a system
    /// tells for it, every question's answer and the colony's own milestones written elsewhere (<see cref="ChronicleMath.Shows"/>),
    /// worded for the colony's story: a line in its own words, an answer with what came of it, an old season's small
    /// lines as one summary. Nothing is kept twice: the history keeps the lines, by its own rules (the last sixty days whole,
    /// milestones for good). Built again when the history changes; safe without a game (empty).
    /// </summary>
    public static class Chronicle
    {
        private static List<ChronicleLine> lines = new List<ChronicleLine>();
        private static HistoryBook builtFor;
        private static int builtVersion = -1;

        /// <summary>Every line, newest first.</summary>
        public static List<ChronicleLine> Lines
        {
            get
            {
                var book = WorldLedger.Instance?.Book;
                if (book == null) return Empty;
                if (book != builtFor || book.Version != builtVersion) Build(book);
                return lines;
            }
        }

        private static readonly List<ChronicleLine> Empty = new List<ChronicleLine>();

        public static int Count => Lines.Count;

        /// <summary>The lines since then, newest first.</summary>
        public static IEnumerable<ChronicleLine> Since(int tick)
        {
            foreach (var l in Lines)
            {
                if (l.tick < tick) yield break;
                yield return l;
            }
        }

        private static void Build(HistoryBook book)
        {
            var list = new List<ChronicleLine>();
            // Each name looked up once a build (a summary's name may be someone long gone, found by a search).
            names.Clear();
            try
            {
                var ours = Ours();
                for (int i = book.events.Count - 1; i >= 0; i--)
                {
                    var e = book.events[i];
                    if (!ChronicleMath.Shows(e, ours.Contains)) continue;
                    list.Add(new ChronicleLine(e, Words(e)));
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("chronicle: read from the history", ex);
            }
            lines = list;
            builtFor = book;
            builtVersion = book.Version;
        }

        /// <summary>The colony as a party: its homes, its people, its colonists.</summary>
        private static HashSet<Ref> Ours()
        {
            var set = new HashSet<Ref>();
            foreach (var home in Homes.All) set.Add(Refs.Colony(home));
            var us = Refs.Us;
            if (!us.IsNone) set.Add(us);
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists) set.Add(Refs.Of(p));
            return set;
        }

        // ---------------- Words ----------------

        /// <summary>
        /// A line's words in the chronicle: an answer's line with what came of it, a season's summary counted ("Harper made
        /// 5 wishes (Day 12 to Day 26)."), else the history's own words (the line as it was told).
        /// </summary>
        public static string Words(HistoryEntry e)
        {
            if (e == null) return "";
            if (e.folded) return FoldWords(e);
            string text = History.Text(e);
            if (HistoryKinds.Matches(e.kind, AnswerMath.Family))
            {
                if (text.NullOrEmpty()) text = AnswerLine(AnswerMath.Get(e.values, AnswerMath.Question), AnswerMath.Get(e.values, AnswerMath.Answer), AnswerMath.ByDefault(e.values));
                string came = CameOf(e.values);
                if (!came.NullOrEmpty()) text += " " + came;
                return text;
            }
            return text.NullOrEmpty() ? CardWords.HistoryLine(e, false) : text;
        }

        /// <summary>An answer's line: "{QUESTION}: the council chose "{ANSWER}"." (or that no answer came in time).</summary>
        public static string AnswerLine(string question, string answer, bool byDefault) =>
            (byDefault ? "CC_Answer_LineDefault" : "CC_Answer_Line").Translate((question ?? "").Trim().TrimEnd(':').Named("QUESTION"),
                (answer ?? "").Trim().TrimEnd('.', ' ').Named("ANSWER")).Resolve();

        /// <summary>What came of an answer, once it's known: taken place (with a word on it), never come about, gone wrong; nothing while it waits.</summary>
        public static string CameOf(IReadOnlyList<string> values)
        {
            var went = AnswerMath.Went(values);
            string note = AnswerMath.Get(values, AnswerMath.NoteAt);
            switch (went)
            {
                case AnswerWent.TookPlace:
                    return note.NullOrEmpty() ? "CC_Answer_Came_TookPlace".Translate().Resolve() : "CC_Answer_Came_TookPlaceNote".Translate(note.Named("NOTE")).Resolve();
                case AnswerWent.FellThrough:
                    return "CC_Answer_Came_FellThrough".Translate().Resolve();
                case AnswerWent.WentWrong:
                    return "CC_Answer_Came_WentWrong".Translate().Resolve();
                default:
                    return null;
            }
        }

        /// <summary>An answer's whole record, for a tooltip: the question, the answer, the follow-up told, how it went.</summary>
        public static string AnswerTip(HistoryEntry e)
        {
            if (e?.values == null) return null;
            var tip = new List<string>
            {
                AnswerMath.Get(e.values, AnswerMath.Question).Colorize(ColoredText.TipSectionTitleColor) + "\n"
                + "CC_Dil_TipAnswer".Translate(AnswerMath.Get(e.values, AnswerMath.Answer)).Resolve()
            };
            if (AnswerMath.ByDefault(e.values)) tip.Add("CC_Dil_TipDefault".Translate());
            string outcome = AnswerMath.Get(e.values, AnswerMath.Outcome);
            if (!outcome.NullOrEmpty()) tip.Add(outcome);
            string went = ("CC_Dil_Went_" + AnswerMath.Went(e.values)).Translate();
            string note = AnswerMath.Get(e.values, AnswerMath.NoteAt);
            tip.Add((note.NullOrEmpty() ? went : went + ": " + note).Colorize(ColoredText.SubtleGrayColor));
            return string.Join("\n\n", tip);
        }

        private static string FoldWords(HistoryEntry e)
        {
            string name = null;
            if (e.actors != null && e.actors.Count > 0 && e.actors[0].who.kind == RefKind.Person) name = NameOf(e.actors[0].who);
            string key = ChronicleMath.FoldKey(e, !name.NullOrEmpty());
            if (key == null || !key.CanTranslate()) return CardWords.HistoryLine(e, false);
            string text = key.Translate(e.count.Named("COUNT"), (name ?? "").Named("NAME")).Resolve();
            var (from, to) = WorldHistoryMath.Days(e);
            return text + " " + (from < to
                ? "CC_ChronFold_Span".Translate("CC_DayN".Translate(from).Named("FROM"), "CC_DayN".Translate(to).Named("TO")).Resolve()
                : "CC_ChronFold_SpanDay".Translate("CC_DayN".Translate(to).Named("DAY")).Resolve());
        }

        private static readonly Dictionary<Ref, string> names = new Dictionary<Ref, string>();

        /// <summary>Someone's name while the game or the ledger still knows them.</summary>
        public static string NameOf(Ref r)
        {
            if (r.IsNone) return null;
            if (names.TryGetValue(r, out var known)) return known;
            try
            {
                string n = Refs.Pawn(r)?.LabelShort ?? WorldLedger.NameOf(r);
                if (names.Count > 500) names.Clear();
                names[r] = n;
                return n;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("chronicle: a name", e);
                return null;
            }
        }
    }
}
