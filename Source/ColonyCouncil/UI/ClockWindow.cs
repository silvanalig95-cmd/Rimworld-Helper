using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What "Rimfolk's clock" shows (Debug actions, Rimfolk): every pass in Sched's table with its runs and milliseconds in
    /// the last in-game hour and day, its average and worst run, by cost; the totals; the sizes of the main saved stores;
    /// and the world's objects and pawns. Worked out from <see cref="PassClock"/> at most every half second while the
    /// window is open, so drawing only lays out strings already made.
    /// </summary>
    public sealed class ClockModel
    {
        public sealed class Row
        {
            public string name, tip;
            public string hourRuns, hourMs, dayRuns, dayMs, perRun, worst;
            public long hourTime, dayTime, worstTime;
            public int order;
        }

        public readonly List<Row> rows = new List<Row>();
        public Row total;
        public bool wholeHour, wholeDay;
        public string summary, stat;
        public readonly List<string> stores = new List<string>();
        public readonly List<string> world = new List<string>();

        public static ClockModel Build()
        {
            var m = new ClockModel();
            int now = Find.TickManager?.TicksGame ?? 0;
            int since = PassClock.Since;
            long freq = PassClock.Frequency;
            m.wholeHour = PassClockMath.SawWholePrevious(since, now, Sched.HourTicks);
            m.wholeDay = PassClockMath.SawWholePrevious(since, now, Sched.DayTicks);

            int hourRuns = 0, dayRuns = 0, allRuns = 0;
            long hourTime = 0, dayTime = 0, allTime = 0, worst = 0;
            for (int i = 0; i < PassTable.Count; i++)
            {
                var info = PassTable.All[i];
                var f = PassClock.Of(info.pass);
                var hour = m.wholeHour ? f.LastHourAt(now) : f.HourAt(now);
                var day = m.wholeDay ? f.LastDayAt(now) : f.DayAt(now);
                string name = info.key.Translate().Resolve();
                var row = new Row
                {
                    name = name,
                    order = i,
                    hourTime = hour.time,
                    dayTime = day.time,
                    worstTime = f.worst,
                    hourRuns = hour.runs.ToString(),
                    hourMs = Ms(hour.time, freq),
                    dayRuns = day.runs.ToString(),
                    dayMs = Ms(day.time, freq),
                    perRun = f.runs > 0 ? Fine(PassClockMath.PerRun(f.time, f.runs) * 1000.0 / freq) : "-",
                    worst = f.runs > 0 ? Fine(PassClockMath.Ms(f.worst, freq)) : "-",
                    tip = name + "\n\n" + Rhythm(info) + "\n" + "CC_Clock_SinceLoad".Translate(f.runs, Ms(f.time, freq)).Resolve()
                          + (f.runs > 0 && f.worstTick != PassWindow.None ? " " + "CC_Clock_WorstAt".Translate(When(now - f.worstTick)).Resolve() : ""),
                };
                m.rows.Add(row);
                hourRuns += hour.runs;
                hourTime += hour.time;
                dayRuns += day.runs;
                dayTime += day.time;
                allRuns += f.runs;
                allTime += f.time;
                if (f.worst > worst) worst = f.worst;
            }
            // By cost: the day's time, then the hour's, then the worst run.
            m.rows.Sort((a, b) =>
            {
                int c = b.dayTime.CompareTo(a.dayTime);
                if (c == 0) c = b.hourTime.CompareTo(a.hourTime);
                if (c == 0) c = b.worstTime.CompareTo(a.worstTime);
                return c != 0 ? c : a.order.CompareTo(b.order);
            });
            m.total = new Row
            {
                name = "CC_Clock_Total".Translate().Resolve(),
                hourRuns = hourRuns.ToString(),
                hourMs = Ms(hourTime, freq),
                dayRuns = dayRuns.ToString(),
                dayMs = Ms(dayTime, freq),
                perRun = allRuns > 0 ? Fine(PassClockMath.PerRun(allTime, allRuns) * 1000.0 / freq) : "-",
                worst = allRuns > 0 ? Fine(PassClockMath.Ms(worst, freq)) : "-",
            };

            if (since == PassWindow.None)
            {
                m.summary = "CC_Clock_NothingYet".Translate().Resolve();
                m.stat = "";
            }
            else
            {
                // Rimfolk's share of each tick over the day shown: the whole day before, or this one since it began (or since loading).
                int dayStart = PassClockMath.Index(now, Sched.DayTicks) * Sched.DayTicks;
                int ticks = m.wholeDay ? Sched.DayTicks : Math.Max(1, now - Math.Max(dayStart, since) + 1);
                string perTick = Fine(PassClockMath.Ms(dayTime, freq) / ticks);
                m.summary = "CC_Clock_Summary".Translate(When(now - since), perTick).Resolve();
                m.stat = "CC_Clock_Stat".Translate(perTick).Resolve();
            }

            Stores(m);
            World(m);
            return m;
        }

        /// <summary>How often a pass comes round, in words.</summary>
        private static string Rhythm(PassInfo info)
        {
            string when;
            if (info.period == Sched.HourTicks) when = "CC_Clock_Rhythm_Hour".Translate(info.offset).Resolve();
            else if (info.period == Sched.DayTicks) when = "CC_Clock_Rhythm_Day".Translate(info.offset).Resolve();
            else when = "CC_Clock_Rhythm_Every".Translate(info.period, info.offset).Resolve();
            return info.waiting ? when + " " + "CC_Clock_Rhythm_Waiting".Translate().Resolve() : when;
        }

        /// <summary>Game time as "5 hours" or "2.5 days".</summary>
        private static string When(int ticks) => Math.Max(0, ticks).ToStringTicksToPeriod();

        private static string Ms(long time, long freq) => PassClockMath.Ms(time, freq).ToString("0.00");

        /// <summary>A small time in milliseconds, with the digits it needs.</summary>
        private static string Fine(double ms) => ms < 1.0 ? ms.ToString("0.000") : ms < 100.0 ? ms.ToString("0.00") : ms.ToString("0.0");

        /// <summary>The sizes of the stores Rimfolk keeps in the save, each on its own (one that can't be read says so).</summary>
        private static void Stores(ClockModel m)
        {
            var comp = CouncilGameComponent.Instance;
            Line(m.stores, () => "CC_Clock_Store_Chronicle".Translate(comp.chronicle.Count).Resolve());
            Line(m.stores, () => "CC_Clock_Store_Notables".Translate(NotableBook.Instance.notables.Count).Resolve());
            Line(m.stores, () =>
            {
                var book = OutsiderBook.Instance;
                return "CC_Clock_Store_Outsiders".Translate(book.pairs.Count, book.heard.Count).Resolve();
            });
            Line(m.stores, () => "CC_Clock_Store_Deeds".Translate(FactionMemory.Instance.deeds.Count).Resolve());
            Line(m.stores, () => "CC_Clock_Store_Memos".Translate(comp.happenings.memos.Count).Resolve());
            Line(m.stores, () =>
            {
                int journals = 0, lines = 0;
                foreach (var j in comp.journals.All)
                {
                    journals++;
                    lines += j.entries.Count;
                }
                return "CC_Clock_Store_Journals".Translate(journals, lines).Resolve();
            });
            Line(m.stores, () =>
            {
                var ws = WorldSituations.Instance;
                return "CC_Clock_Store_Situations".Translate(ws.active.Count, ws.history.Count).Resolve();
            });
            Line(m.stores, () => "CC_Clock_Store_Camps".Translate(CampTracker.Instance.past.Count).Resolve());
        }

        private static void World(ClockModel m)
        {
            Line(m.world, () => "CC_Clock_WorldObjects".Translate(Find.WorldObjects.AllWorldObjects.Count, Find.WorldObjects.Settlements.Count).Resolve());
            Line(m.world, () => "CC_Clock_WorldPawns".Translate(Find.WorldPawns.AllPawnsAlive.Count, Find.WorldPawns.AllPawnsDead.Count).Resolve());
        }

        private static void Line(List<string> lines, Func<string> make)
        {
            try
            {
                lines.Add(make());
            }
            catch (Exception e)
            {
                lines.Add("CC_Clock_CantRead".Translate(e.GetType().Name).Resolve());
            }
        }

        /// <summary>The window as plain text, for the clipboard.</summary>
        public string Plain()
        {
            var sb = new StringBuilder("[Rimfolk] ").Append("CC_Clock_Title".Translate().Resolve()).Append('\n');
            sb.Append(summary).Append("\n\n");
            sb.Append("CC_Clock_Col_Pass".Translate().Resolve()).Append(" | ")
              .Append(HourLabel).Append(": ").Append("CC_Clock_Col_Runs".Translate().Resolve()).Append(", ").Append("CC_Clock_Col_Ms".Translate().Resolve()).Append(" | ")
              .Append(DayLabel).Append(": ").Append("CC_Clock_Col_Runs".Translate().Resolve()).Append(", ").Append("CC_Clock_Col_Ms".Translate().Resolve()).Append(" | ")
              .Append("CC_Clock_Col_PerRun".Translate().Resolve()).Append(" | ").Append("CC_Clock_Col_Worst".Translate().Resolve()).Append('\n');
            foreach (var r in rows) Plain(sb, r);
            Plain(sb, total);
            sb.Append('\n').Append("CC_Clock_Stores".Translate().Resolve()).Append('\n');
            foreach (var s in stores) sb.Append("- ").Append(s).Append('\n');
            sb.Append('\n').Append("CC_Clock_World".Translate().Resolve()).Append('\n');
            foreach (var s in world) sb.Append("- ").Append(s).Append('\n');
            return sb.ToString();
        }

        private static void Plain(StringBuilder sb, Row r) =>
            sb.Append(r.name).Append(" | ").Append(r.hourRuns).Append(", ").Append(r.hourMs).Append(" | ").Append(r.dayRuns).Append(", ").Append(r.dayMs)
              .Append(" | ").Append(r.perRun).Append(" | ").Append(r.worst).Append('\n');

        public string HourLabel => (wholeHour ? "CC_Clock_Col_LastHour" : "CC_Clock_Col_ThisHour").Translate().Resolve();
        public string DayLabel => (wholeDay ? "CC_Clock_Col_LastDay" : "CC_Clock_Col_Today").Translate().Resolve();
    }

    /// <summary>The "Rimfolk's clock" window (Debug actions, Rimfolk; next to "check myself").</summary>
    public class Dialog_Clock : Window
    {
        private const float RowH = 22f, NumW = 66f, Refresh = 0.5f;

        private ClockModel model;
        private float builtAt = -100f;
        private Vector2 scroll;
        private float height = 600f;
        private string title, subtitle, colPass, colRuns, colMs, colPerRun, colWorst, storesTitle, worldTitle, reading, copy;

        public Dialog_Clock()
        {
            // Open beside the running game, to watch the figures change.
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            preventCameraMotion = false;
            title = "CC_Clock_Title".Translate().Resolve();
            subtitle = "CC_Clock_Subtitle".Translate().Resolve();
            colPass = "CC_Clock_Col_Pass".Translate().Resolve();
            colRuns = "CC_Clock_Col_Runs".Translate().Resolve();
            colMs = "CC_Clock_Col_Ms".Translate().Resolve();
            colPerRun = "CC_Clock_Col_PerRun".Translate().Resolve();
            colWorst = "CC_Clock_Col_Worst".Translate().Resolve();
            storesTitle = "CC_Clock_Stores".Translate().Resolve();
            worldTitle = "CC_Clock_World".Translate().Resolve();
            reading = "CC_Clock_Reading".Translate().Resolve();
            copy = "CC_Clock_Copy".Translate().Resolve();
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(900f, UI.screenWidth - 40f), Mathf.Min(860f, UI.screenHeight - 40f));

        private string hourLabel, dayLabel;

        public override void DoWindowContents(Rect inRect)
        {
            float real = Time.realtimeSinceStartup;
            if (model == null || real - builtAt >= Refresh || real < builtAt)
            {
                model = ClockModel.Build();
                hourLabel = model.HourLabel;
                dayLabel = model.DayLabel;
                builtAt = real;
            }

            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Council, title, subtitle, model.stat);

            const float buttonsH = 34f;
            var outRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top - buttonsH - 6f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            y += UiKit.Paragraph(0f, y, view.width, model.summary, Color.white) + 4f;
            y += UiKit.Paragraph(0f, y, view.width, reading, UiKit.Faint) + 8f;

            // The table: a header in two lines (the window, then what is counted), the passes by cost, the totals.
            float nameW = view.width - NumW * 6f;
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.HeaderColor;
            Text.Anchor = TextAnchor.LowerCenter;
            Widgets.Label(new Rect(nameW, y, NumW * 2f, RowH), hourLabel);
            Widgets.Label(new Rect(nameW + NumW * 2f, y, NumW * 2f, RowH), dayLabel);
            y += RowH;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(4f, y, nameW - 8f, RowH), colPass);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Cell(nameW, 0, y), colRuns);
            Widgets.Label(Cell(nameW, 1, y), colMs);
            Widgets.Label(Cell(nameW, 2, y), colRuns);
            Widgets.Label(Cell(nameW, 3, y), colMs);
            Widgets.Label(Cell(nameW, 4, y), colPerRun);
            Widgets.Label(Cell(nameW, 5, y), colWorst);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += RowH;
            UiKit.Divider(0f, y, view.width);
            y += 2f;

            // Only the rows in sight are drawn.
            for (int i = 0; i < model.rows.Count; i++)
            {
                if (y + RowH >= scroll.y && y <= scroll.y + outRect.height) DrawRow(model.rows[i], i, y, nameW, view.width, Color.white);
                y += RowH;
            }
            UiKit.Divider(0f, y, view.width);
            y += 2f;
            DrawRow(model.total, 0, y, nameW, view.width, UiKit.Gold);
            y += RowH + 12f;

            y += UiKit.SectionTitle(0f, y, view.width, storesTitle);
            foreach (var s in model.stores) y += UiKit.Paragraph(4f, y, view.width - 4f, s, Color.white) + 2f;
            y += 8f;
            y += UiKit.SectionTitle(0f, y, view.width, worldTitle);
            foreach (var s in model.world) y += UiKit.Paragraph(4f, y, view.width - 4f, s, Color.white) + 2f;
            height = Mathf.Max(y + 4f, 100f);
            Widgets.EndScrollView();

            var button = new Rect(inRect.x, inRect.yMax - buttonsH + 4f, 170f, 30f);
            if (Widgets.ButtonText(button, copy))
            {
                GUIUtility.systemCopyBuffer = model.Plain();
                Messages.Message("CC_Clock_Copied".Translate(), MessageTypeDefOf.SilentInput, false);
            }
        }

        private static Rect Cell(float nameW, int column, float y) => new Rect(nameW + NumW * column, y, NumW - 6f, RowH);

        private static void DrawRow(ClockModel.Row r, int index, float y, float nameW, float width, Color color)
        {
            var line = new Rect(0f, y, width, RowH);
            UiKit.Zebra(line, index);
            if (!r.tip.NullOrEmpty() && Mouse.IsOver(line))
            {
                Widgets.DrawHighlight(line);
                TooltipHandler.TipRegion(line, r.tip);
            }
            GUI.color = color;
            Text.Anchor = TextAnchor.MiddleLeft;
            var nameRect = new Rect(4f, y, nameW - 8f, RowH);
            Widgets.Label(nameRect, UiKit.Fit(r.name, nameRect.width));
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Cell(nameW, 0, y), r.hourRuns);
            Widgets.Label(Cell(nameW, 1, y), r.hourMs);
            Widgets.Label(Cell(nameW, 2, y), r.dayRuns);
            Widgets.Label(Cell(nameW, 3, y), r.dayMs);
            Widgets.Label(Cell(nameW, 4, y), r.perRun);
            Widgets.Label(Cell(nameW, 5, y), r.worst);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
