using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Council → Dilemmas: the questions waiting for the council's answer, the traditions its answers have founded, and
    /// every answer given since (<see cref="DilemmaLog"/>) as a table: the day, the question, the answer, who it was
    /// about, and how it went (as answered, gone wrong, still to happen, taken place or fallen through), with the
    /// follow-up text in its tooltip. The table's filter is remembered (<see cref="PageMemory"/>).
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
            float top = UiKit.PageHeader(rect, Icons.Dilemmas ?? Icons.Decree, "CC_Dil_Header".Translate(), "CC_UI_Sub_Dilemmas".Translate(), m.stat, null, 330f);
            float fx = rect.xMax;
            for (int i = filterLabels.Length - 1; i >= 0; i--)
            {
                float fw = UiKit.TabButtonWidth(filterLabels[i], null) - 8f;
                fx -= fw + 4f;
                if (UiKit.TabButton(new Rect(fx, rect.y + 8f, fw, 26f), filterLabels[i], null, (int)filter == i) && (int)filter != i)
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
                if (!ColonyCouncilMod.Settings.enableDilemmas) y += UiKit.Paragraph(0f, y, w, "CC_Dil_Off".Translate(), UiKit.Warn) + 8f;

                // Waiting for an answer.
                y += UiKit.SectionTitle(0f, y, w, "CC_Dil_Waiting".Translate(), Icons.Warning, UiKit.Gold, m.next);
                if (m.waiting.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, "CC_Dil_NoneWaiting".Translate(), UiKit.Faint) + 6f;
                for (int i = 0; i < m.waiting.Count; i++)
                {
                    var (letter, text, left) = m.waiting[i];
                    var row = new Rect(0f, y, w, 30f);
                    UiKit.Zebra(row, i);
                    Widgets.DrawHighlightIfMouseover(row);
                    UiKit.Icon(new Rect(6f, y + 7f, 16f, 16f), Icons.Decree, UiKit.Gold);
                    float lw = left == null ? 0f : 140f;
                    UiKit.LabelFit(new Rect(28f, y, w - 34f - lw, 30f), text, Color.white);
                    if (left != null) UiKit.LabelFit(new Rect(w - lw - 4f, y, lw, 30f), left, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    TooltipHandler.TipRegion(row, "CC_Week_ClickLetter".Translate());
                    if (Widgets.ButtonInvisible(row)) letter.OpenLetter();
                    y += 30f;
                }
                y += 10f;

                // The traditions the answers founded.
                if (m.traditions.Count > 0)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_Dil_Traditions".Translate(), Icons.Ways, UiKit.HeaderColor);
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
                    y = ty + UiKit.PillHeight + 14f;
                }

                // Every answer since, newest first.
                y += UiKit.SectionTitle(0f, y, w, "CC_Dil_History".Translate(), Icons.Chronicle, UiKit.HeaderColor, m.countText);
                if (Mathf.Abs(laidWidth - w) > 0.5f)
                {
                    CouncilLayoutMath.Layout(w, MinW, WeightW, Prio, colX, colW, colShown);
                    laidWidth = w;
                }
                if (m.lines.Count == 0)
                    y += UiKit.Paragraph(4f, y, w - 4f, (m.count == 0 ? "CC_Dil_NoHistory" : "CC_Dil_NoneMatch").Translate(), UiKit.Faint) + 6f;
                else
                {
                    var hr = new Rect(0f, y, w, 22f);
                    Widgets.DrawBoxSolid(hr, new Color(1f, 1f, 1f, 0.05f));
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
                GUI.color = Color.white;
            }
        }

        private static void DrawLine(Rect r, Line l, int i)
        {
            UiKit.Zebra(r, i);
            Widgets.DrawHighlightIfMouseover(r);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y + 4f, 2f, r.height - 8f), UiKit.WithAlpha(l.wentColor, 0.8f));
            Cell(r, 0, l.day, null, UiKit.Faint);
            Cell(r, 1, l.question, null, Color.white);
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
                UiKit.LabelFit(new Rect(x, r.y, w, r.height), text, color, c == 0 || c == 3 ? GameFont.Tiny : GameFont.Small);
                return;
            }
            UiKit.LabelFit(new Rect(x, r.y + 2f, w, 20f), text, color);
            UiKit.LabelFit(new Rect(x, r.y + 20f, w, 18f), sub, UiKit.Faint, GameFont.Tiny);
        }

        private static Model Build(CouncilGameComponent comp)
        {
            var m = new Model();
            int now = Find.TickManager.TicksGame;
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Dilemma>())
            {
                if (l.resolved) continue;
                int left = l.TimeoutActive ? l.disappearAtTick - now : -1;
                m.waiting.Add((l, l.Label.ToString(), left > 0 ? "CC_Week_Left".Translate(UiKit.Duration(left)).Resolve() : null));
            }
            var tracker = comp.dilemmas;
            if (ColonyCouncilMod.Settings.enableDilemmas && tracker.nextTick > now)
                m.next = "CC_Dil_Next".Translate(TextUtil.DaysString(tracker.nextTick - now)).Resolve();
            foreach (var d in tracker.Traditions)
                m.traditions.Add(((d.traditionLabel ?? d.label).CapitalizeFirst(), "CC_Dil_TraditionTip".Translate(d.LabelCap).Resolve()));
            var records = DilemmaLog.Records ?? new List<DilemmaRecord>();
            m.count = records.Count;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var r = records[i];
                if (filter == Filter.WentWrong && r.Went != DilemmaWent.WentWrong && r.Went != DilemmaWent.FellThrough) continue;
                if (filter == Filter.Unanswered && !r.byDefault) continue;
                m.lines.Add(LineFor(r));
            }
            m.stat = "CC_Dil_Stat".Translate(m.waiting.Count).Resolve();
            m.countText = "CC_Dil_HistoryCount".Translate(m.count).Resolve();
            return m;
        }

        private static Line LineFor(DilemmaRecord r)
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
            tip.Add(("CC_Dil_Went_" + went + "_Tip").Translate(r.playLabel ?? "").Resolve().Colorize(ColoredText.SubtleGrayColor));
            return new Line
            {
                r = r,
                day = "CC_DayN".Translate(r.tick / GenDate.TicksPerDay + 1),
                question = r.question,
                answer = r.answer,
                answerSub = r.byDefault ? "CC_Dil_NoAnswer".Translate().ToString() : null,
                who = r.who,
                went = DilemmaLog.WentLabel(r),
                wentSub = wentSub,
                wentColor = DilemmaLog.WentColor(went),
                tip = string.Join("\n\n", tip)
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
