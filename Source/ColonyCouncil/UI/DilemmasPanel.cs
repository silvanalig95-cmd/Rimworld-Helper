using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Council → Dilemmas: the questions waiting for the council's answer, the traditions its answers have founded, and
    /// every answer given since as a table: the dilemmas (<see cref="DilemmaLog"/>, with what each answer was to do) and
    /// every other question from the history's answers' log (<see cref="Answers"/>: events, world events, camps,
    /// diplomacy's offers, calendar days, meetings, elections, the region's asks, journeys): the day, the question, the
    /// answer, who it was about, and how it went (as answered, gone wrong, still to happen, taken place or fallen
    /// through), with the follow-up text in its tooltip. The table's filter is remembered (<see cref="PageMemory"/>).
    /// </summary>
    public static class DilemmasPanel
    {
        private enum Filter
        {
            All,
            WentWrong,
            Unanswered
        }

        private static Filter filter = Filter.All;
        private static bool loaded;
        private static Vector2 scroll;
        private static float height = 600f;

        private const float RowH = 40f;
        private static float[] MinW => CouncilLayoutMath.DilemmaMin;
        private static float[] WeightW => CouncilLayoutMath.DilemmaWeight;
        private static int[] Prio => CouncilLayoutMath.DilemmaPriority;
        private static readonly float[] colX = new float[5], colW = new float[5];
        private static readonly bool[] colShown = new bool[5];
        private static float laidWidth = -1f;

        private sealed class Line
        {
            public DilemmaRecord r;
            public int tick;
            public string day, question, answer, answerSub, who, went, wentSub, tip;
            public Color wentColor;
        }

        private sealed class Model
        {
            public List<(ChoiceLetter_Dilemma letter, string text, string left)> waiting = new List<(ChoiceLetter_Dilemma, string, string)>();
            public List<(string label, string tip)> traditions = new List<(string, string)>();
            public List<Line> lines = new List<Line>();
            public string stat, next, countText;
            public int count;
            /// <summary>How many each filter shows (all, went wrong, unanswered).</summary>
            public readonly int[] filterCounts = new int[3];
        }

        private static Model model;
        private static float modelAt = -1f;
        private static Filter modelFilter;
        private static string[] filterLabels, headers;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!loaded)
            {
                loaded = true;
                filter = PageMemory.GetEnum("dilemmas.filter", Filter.All);
                filterLabels = new[] { "CC_Dil_Filter_All".Translate().ToString(), "CC_Dil_Filter_WentWrong".Translate().ToString(), "CC_Dil_Filter_Unanswered".Translate().ToString() };
                headers = new[]
                {
                    "CC_Dil_Col_Day".Translate().ToString(), "CC_Dil_Col_Question".Translate().ToString(), "CC_Dil_Col_Answer".Translate().ToString(),
                    "CC_Dil_Col_Who".Translate().ToString(), "CC_Dil_Col_Went".Translate().ToString()
                };
            }
            if (model == null || modelFilter != filter || Time.realtimeSinceStartup - modelAt > 1f)
            {
                modelAt = Time.realtimeSinceStartup;
                modelFilter = filter;
                try
                {
                    model = Build(comp);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("dilemmas page", e);
                    model ??= new Model { stat = "", countText = "" };
                }
            }
            var m = model;
            float chips = 0f;
            for (int i = 0; i < filterLabels.Length; i++) chips += UiKit.FilterChipWidth(filterLabels[i], m.filterCounts[i]) + 4f;
            float top = UiKit.PageHeader(rect, Icons.Dilemmas ?? Icons.Decree, "CC_Dil_Header".Translate(), "CC_UI_Sub_Dilemmas".Translate(), m.stat, null, chips + 8f);
            // The filter, as filter chips with their counts at the top right.
            float fx = rect.xMax;
            for (int i = filterLabels.Length - 1; i >= 0; i--)
            {
                float fw = UiKit.FilterChipWidth(filterLabels[i], m.filterCounts[i]);
                fx -= fw + 4f;
                if (UiKit.FilterChip(new Rect(fx, rect.y + 10f, fw, 22f), filterLabels[i], m.filterCounts[i], (int)filter == i) && (int)filter != i)
                {
                    filter = (Filter)i;
                    PageMemory.SetEnum("dilemmas.filter", filter);
                }
            }
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float y = 0f;
                if (!ColonyCouncilMod.Settings.enableDilemmas) y += UiKit.Paragraph(0f, y, w, "CC_Dil_Off".Translate(), UiKit.Soon) + 8f;

                // Waiting for an answer: each a row (a letter, the day it goes, never a time left), a click opens it.
                y += UiKit.SectionTitle(0f, y, w, "CC_Dil_Waiting".Translate(), m.waiting.Count, Icons.Warning, UiKit.Soon, m.next);
                if (m.waiting.Count == 0)
                {
                    UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowDense + 4f), "CC_Dil_NoneWaiting".Translate());
                    y += UiKit.RowDense + 8f;
                }
                for (int i = 0; i < m.waiting.Count; i++)
                {
                    var (letter, text, left) = m.waiting[i];
                    if (UiKit.Row(new Rect(0f, y, w, UiKit.RowStandard), i, text, UiKit.Ink, null, null, null, Icons.Decree, UiKit.Gold, left, UiKit.Faint,
                            ClickLetter, true))
                        letter.OpenLetter();
                    y += UiKit.RowStandard;
                }
                y += UiKit.SectionGap;

                // The traditions the answers founded.
                if (m.traditions.Count > 0)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_Dil_Traditions".Translate(), m.traditions.Count, Icons.Ways);
                    float tx = 0f, ty = y;
                    foreach (var (label, tip) in m.traditions)
                    {
                        float pw = Mathf.Min(UiKit.PillWidth(label, Icons.Ways), w);
                        if (tx > 0f && tx + pw > w)
                        {
                            tx = 0f;
                            ty += UiKit.PillHeight + 4f;
                        }
                        UiKit.Pill(tx, ty, label, UiKit.Gold, Icons.Ways, tip, w);
                        tx += pw + 4f;
                    }
                    y = ty + UiKit.PillHeight + UiKit.SectionGap;
                }

                // Every answer since, newest first.
                y += UiKit.SectionTitle(0f, y, w, "CC_Dil_History".Translate(), Icons.Chronicle, null, m.countText);
                if (Mathf.Abs(laidWidth - w) > 0.5f)
                {
                    CouncilLayoutMath.Layout(w, MinW, WeightW, Prio, colX, colW, colShown);
                    laidWidth = w;
                }
                if (m.lines.Count == 0)
                {
                    UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowDense + 4f), (m.count == 0 ? "CC_Dil_NoHistory" : "CC_Dil_NoneMatch").Translate());
                    y += UiKit.RowDense + 8f;
                }
                else
                {
                    var hr = new Rect(0f, y, w, UiKit.RowDense);
                    Widgets.DrawBoxSolid(hr, UiKit.CardFill);
                    for (int c = 0; c < headers.Length; c++)
                        if (colShown[c])
                            UiKit.LabelFit(new Rect(colX[c] + 4f, y, colW[c] - 6f, 22f), headers[c], UiKit.Faint, GameFont.Tiny);
                    y += 24f;
                    for (int i = 0; i < m.lines.Count; i++)
                    {
                        // Only rows in sight are drawn.
                        if (y + RowH >= scroll.y && y <= scroll.y + outRect.height) DrawLine(new Rect(0f, y, w, RowH), m.lines[i], i);
                        y += RowH;
                    }
                }
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UiKit.Ink;
            }
        }

        private static void DrawLine(Rect r, Line l, int i)
        {
            UiKit.Zebra(r, i);
            UiKit.RowHover(r);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y + 4f, 2f, r.height - 8f), UiKit.WithAlpha(l.wentColor, 0.8f));
            Cell(r, 0, l.day, null, UiKit.Faint);
            Cell(r, 1, l.question, null, UiKit.Ink);
            Cell(r, 2, l.answer, l.answerSub, UiKit.Quote);
            Cell(r, 3, l.who, null, UiKit.Faint);
            Cell(r, 4, l.went, l.wentSub, l.wentColor);
            if (!l.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, l.tip);
        }

        private static void Cell(Rect r, int c, string text, string sub, Color color)
        {
            if (!colShown[c] || text.NullOrEmpty()) return;
            float x = r.x + colX[c] + 4f, w = colW[c] - 6f;
            if (sub.NullOrEmpty())
            {
                // Names in the question and in who answered are links to their cards (marked with the model).
                LinkText.Line(new Rect(x, r.y, w, r.height), text, color, c == 0 || c == 3 ? GameFont.Tiny : GameFont.Small);
                return;
            }
            LinkText.Line(new Rect(x, r.y + 2f, w, 20f), text, color);
            UiKit.LabelFit(new Rect(x, r.y + 20f, w, 18f), sub, UiKit.Faint, GameFont.Tiny);
        }

        private static Model Build(CouncilGameComponent comp)
        {
            var m = new Model();
            int now = Find.TickManager.TicksGame;
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Dilemma>())
            {
                if (l.resolved) continue;
                // The day the letter goes, never how long until (InboxWords.When: today, tomorrow, or its date).
                bool goes = l.TimeoutActive && l.disappearAtTick > now;
                m.waiting.Add((l, l.Label.ToString(), goes ? "CC_Inbox_Until".Translate(InboxWords.When(l.disappearAtTick).Named("DATE")).Resolve() : null));
            }
            // The traditions of the colony in view.
            var tracker = ColonyView.Current?.dilemmas ?? comp.dilemmas;
            // Questions come unannounced, like events: no countdown to the next one.
            foreach (var d in tracker.Traditions)
                m.traditions.Add(((d.traditionLabel ?? d.label).CapitalizeFirst(), "CC_Dil_TraditionTip".Translate(d.LabelCap).Resolve()));
            var records = DilemmaLog.Records ?? new List<DilemmaRecord>();
            var colonists = PawnsFinder.AllMaps_FreeColonists;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var r = records[i];
                bool wrong = r.Went == DilemmaWent.WentWrong || r.Went == DilemmaWent.FellThrough;
                m.filterCounts[0]++;
                if (wrong) m.filterCounts[1]++;
                if (r.byDefault) m.filterCounts[2]++;
                if (filter == Filter.WentWrong && !wrong) continue;
                if (filter == Filter.Unanswered && !r.byDefault) continue;
                m.lines.Add(LineFor(r, colonists));
            }
            // Every other question's answer, from the history's answers' log (events, the world's, camps, diplomacy's
            // offers, calendar days, meetings, elections, the region's asks, journeys): the dilemmas are above, in full.
            var book = WorldLedger.Instance?.Book;
            if (book != null)
                foreach (var e in book.OfKind(AnswerMath.Family))
                {
                    if (e.folded || e.values == null || AnswerMath.SourceOf(e.kind) == "dilemma") continue;
                    var went = AnswerMath.Went(e.values);
                    bool wrong = went == AnswerWent.WentWrong || went == AnswerWent.FellThrough;
                    bool unanswered = AnswerMath.ByDefault(e.values);
                    m.filterCounts[0]++;
                    if (wrong) m.filterCounts[1]++;
                    if (unanswered) m.filterCounts[2]++;
                    if (filter == Filter.WentWrong && !wrong) continue;
                    if (filter == Filter.Unanswered && !unanswered) continue;
                    m.lines.Add(LineFor(e, colonists));
                }
            m.lines.Sort((a, b) => b.tick.CompareTo(a.tick));
            m.count = m.filterCounts[0];
            m.stat = "CC_Dil_Stat".Translate(m.waiting.Count).Resolve();
            m.countText = "CC_Dil_HistoryCount".Translate(m.count).Resolve();
            return m;
        }

        private static string clickLetter;
        private static string ClickLetter => clickLetter ??= "CC_Week_ClickLetter".Translate();

        private static Line LineFor(DilemmaRecord r, List<Pawn> colonists)
        {
            var went = r.Went;
            string wentSub = null;
            switch (went)
            {
                case DilemmaWent.Waiting:
                case DilemmaWent.UnderWay:
                case DilemmaWent.TookPlace:
                case DilemmaWent.FellThrough:
                    wentSub = r.playLabel?.CapitalizeFirst();
                    // How it went, in a word: "14 jobs done", "found 6 healroot", "the game got away".
                    if (!r.playNote.NullOrEmpty()) wentSub = wentSub.NullOrEmpty() ? r.playNote.CapitalizeFirst() : wentSub + ": " + r.playNote;
                    break;
                default:
                    wentSub = Short(r.outcome);
                    break;
            }
            var tip = new List<string>();
            tip.Add(r.question.Colorize(ColoredText.TipSectionTitleColor) + "\n" + "CC_Dil_TipAnswer".Translate(r.answer).Resolve());
            if (r.byDefault) tip.Add("CC_Dil_TipDefault".Translate());
            if (!r.outcome.NullOrEmpty()) tip.Add(r.outcome);
            // What the answer was to do, in words or in numbers as the setting has it (the gamble's part only if it came true
            // is the "How it went" column's to tell).
            string impact = DilemmaImpact.RecordFor(r);
            if (!impact.NullOrEmpty()) tip.Add(impact);
            tip.Add(("CC_Dil_Went_" + went + "_Tip").Translate(r.playLabel ?? "").Resolve().Colorize(ColoredText.SubtleGrayColor));
            return new Line
            {
                r = r,
                tick = r.tick,
                day = "CC_DayN".Translate(r.tick / GenDate.TicksPerDay + 1),
                question = PaneLinks.Mark(r.question, colonists),
                answer = r.answer,
                answerSub = r.byDefault ? "CC_Dil_NoAnswer".Translate().ToString() : null,
                who = PaneLinks.Mark(r.who, colonists),
                went = DilemmaLog.WentLabel(r),
                wentSub = wentSub,
                wentColor = DilemmaLog.WentColor(went),
                tip = string.Join("\n\n", tip)
            };
        }

        /// <summary>Another question's answer, as the history's answers' log keeps it: the same columns, its whole record in the tooltip.</summary>
        private static Line LineFor(HistoryEntry e, List<Pawn> colonists)
        {
            var went = AnswerMath.Went(e.values);
            string note = AnswerMath.Get(e.values, AnswerMath.NoteAt);
            string outcome = AnswerMath.Get(e.values, AnswerMath.Outcome);
            string who = null;
            foreach (var a in e.actors)
            {
                who = Chronicle.NameOf(a.who) ?? (a.who.kind == RefKind.People ? Refs.Faction(a.who)?.Name : null);
                if (!who.NullOrEmpty()) break;
            }
            return new Line
            {
                tick = e.tick,
                day = "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1),
                question = PaneLinks.Mark(AnswerMath.Get(e.values, AnswerMath.Question), colonists),
                answer = AnswerMath.Get(e.values, AnswerMath.Answer),
                answerSub = AnswerMath.ByDefault(e.values) ? "CC_Dil_NoAnswer".Translate().ToString() : null,
                who = PaneLinks.Mark(who, colonists),
                went = ("CC_Dil_Went_" + went).Translate(),
                wentSub = !note.NullOrEmpty() ? note.CapitalizeFirst() : Short(outcome),
                wentColor = DilemmaLog.WentColor((DilemmaWent)(int)went),
                tip = Chronicle.AnswerTip(e)
            };
        }

        /// <summary>The follow-up's first sentence, for the column (the whole of it is in the tooltip).</summary>
        private static string Short(string text)
        {
            if (text.NullOrEmpty()) return null;
            int stop = text.IndexOf(". ", System.StringComparison.Ordinal);
            return stop > 20 && stop < 140 ? text.Substring(0, stop + 1) : text;
        }
    }
}
