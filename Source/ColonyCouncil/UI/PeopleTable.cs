using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Everyone: one table of the colonists, a row each, with two lines per cell: mood and Rimfolk's share of
    /// it, trust and how it moved this week, strain and the feeling on top, the setbacks and pressures weighing on them
    /// (with what helps), their open wish, their personal story's stage, ailments and convalescence, and quirks; office
    /// and epithet under the name. A click on a heading sorts by it (again for the other way); hovering a cell tells
    /// more; a click on a row selects the colonist on the map, a right click offers their page and the pin. The rows
    /// are worked out about once a second; the sort and the filter are remembered (<see cref="PageMemory"/>).
    /// </summary>
    public static class PeopleTable
    {
        public enum Col
        {
            Name,
            Mood,
            Rimfolk,
            Trust,
            Strain,
            Weighing,
            Wish,
            Story,
            Health,
            Quirks
        }

        public enum Filter
        {
            All,
            Attention,
            Pinned
        }

        private static readonly Col[] Cols = (Col[])Enum.GetValues(typeof(Col));

        /// <summary>Smallest width, share of the room left, and which give way first on a narrow window (0 never): CouncilLayoutMath.</summary>
        private static float[] MinWidth => CouncilLayoutMath.PeopleMin;
        private static float[] Weight => CouncilLayoutMath.PeopleWeight;
        private static int[] Priority => CouncilLayoutMath.PeoplePriority;

        public const float RowH = 40f;
        public const float HeaderH = 26f;

        private struct Cell
        {
            public string text, sub, tip;
            public Color color, subColor;
            public float sort;
        }

        private sealed class Row
        {
            public Pawn pawn;
            public readonly Cell[] cells = new Cell[Cols.Length];
            public float mood, minor, major;
            public Color moodColor;
            public bool attention, pinned;
            public string rowTip;
        }

        private static List<Row> rows = new List<Row>();
        private static readonly List<Row> shown = new List<Row>();
        private static float builtAt = -1f;
        private static bool loaded;
        private static Col sortBy = Col.Mood;
        private static bool descending;
        private static Filter filter = Filter.All;
        private static Vector2 scroll;
        private static string[] headers, headerTips, filterLabels, filterTips;
        private static string countText, header, subtitle, headerTip, noneAll, noneShown;

        // The columns' places, worked out again when the width changes.
        private static float laidWidth = -1f;
        private static readonly float[] colX = new float[Cols.Length], colW = new float[Cols.Length];
        private static readonly bool[] colShown = new bool[Cols.Length];

        public static void Dirty() => builtAt = -1f;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            sortBy = PageMemory.GetEnum("everyone.sort", Col.Mood);
            descending = PageMemory.GetBool("everyone.descending", false);
            filter = PageMemory.GetEnum("everyone.filter", Filter.All);
        }

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            Load();
            if (builtAt < 0f || Time.realtimeSinceStartup - builtAt > 1f) Build(comp);

            float top = UiKit.PageHeader(rect, Icons.Table ?? Icons.Roster, header, subtitle, countText, null, 330f, headerTip);
            // The filter, as three small tabs at the top right.
            float fx = rect.xMax;
            for (int i = filterLabels.Length - 1; i >= 0; i--)
            {
                var f = (Filter)i;
                string label = filterLabels[i];
                float fw = UiKit.TabButtonWidth(label, null) - 8f;
                fx -= fw + 4f;
                if (UiKit.TabButton(new Rect(fx, rect.y + 8f, fw, 26f), label, null, filter == f, filterTips[i]) && filter != f)
                {
                    filter = f;
                    PageMemory.SetEnum("everyone.filter", f);
                    Refilter();
                }
            }

            var body = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            float w = body.width - 16f;
            if (Mathf.Abs(laidWidth - w) > 0.5f)
            {
                CouncilLayoutMath.Layout(w, MinWidth, Weight, Priority, colX, colW, colShown);
                laidWidth = w;
            }
            DrawHeader(new Rect(body.x, body.y, w, HeaderH));
            var outRect = new Rect(body.x, body.y + HeaderH + 2f, body.width, body.height - HeaderH - 2f);
            var view = new Rect(0f, 0f, w, Mathf.Max(shown.Count * RowH, 10f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                if (shown.Count == 0) UiKit.EmptyState(0f, 0f, w, Icons.People, filter == Filter.All ? noneAll : noneShown, 120f);
                for (int i = 0; i < shown.Count; i++)
                {
                    float y = i * RowH;
                    // Only rows in sight are drawn.
                    if (y + RowH < scroll.y || y > scroll.y + outRect.height) continue;
                    DrawRow(new Rect(0f, y, w, RowH), shown[i], i);
                }
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static void DrawHeader(Rect r)
        {
            Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.05f));
            for (int i = 0; i < Cols.Length; i++)
            {
                if (!colShown[i]) continue;
                var c = Cols[i];
                var hr = new Rect(r.x + colX[i], r.y, colW[i], r.height);
                bool active = sortBy == c;
                if (Mouse.IsOver(hr)) Widgets.DrawHighlight(hr);
                float arrow = active ? 12f : 0f;
                UiKit.LabelFit(new Rect(hr.x + 4f, hr.y, hr.width - 6f - arrow, hr.height), headers[i], active ? UiKit.Gold : UiKit.Faint, GameFont.Tiny);
                if (active) UiKit.Icon(new Rect(hr.xMax - 13f, hr.y + 8f, 10f, 10f), descending ? Icons.TrendDown : Icons.TrendUp, UiKit.Gold);
                TooltipHandler.TipRegion(hr, headerTips[i]);
                if (Widgets.ButtonInvisible(hr))
                {
                    if (active) descending = !descending;
                    else
                    {
                        sortBy = c;
                        descending = false;
                    }
                    PageMemory.SetEnum("everyone.sort", sortBy);
                    PageMemory.SetBool("everyone.descending", descending);
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    Refilter();
                }
            }
            UiKit.Divider(r.x, r.yMax, r.width);
        }

        private static void DrawRow(Rect r, Row row, int index)
        {
            UiKit.Zebra(r, index);
            bool over = Mouse.IsOver(r);
            if (over) Widgets.DrawHighlight(r);
            if (row.attention) Widgets.DrawBoxSolid(new Rect(r.x, r.y + 3f, 2f, r.height - 6f), UiKit.WithAlpha(UiKit.Warn, 0.8f));
            for (int i = 0; i < Cols.Length; i++)
            {
                if (!colShown[i]) continue;
                var cr = new Rect(r.x + colX[i], r.y, colW[i], r.height);
                ref var cell = ref row.cells[i];
                switch (Cols[i])
                {
                    case Col.Name:
                        DrawName(cr, row, ref cell, over);
                        break;
                    case Col.Mood:
                        DrawMood(cr, row, ref cell);
                        break;
                    default:
                        Lines(cr, ref cell);
                        break;
                }
                if (Cols[i] != Col.Name && !cell.tip.NullOrEmpty()) TooltipHandler.TipRegion(cr, cell.tip);
            }
            if (over && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                Event.current.Use();
                Menu(row.pawn);
                return;
            }
            if (Widgets.ButtonInvisible(r)) Select(row.pawn);
        }

        private static void Lines(Rect cr, ref Cell cell)
        {
            if (cell.text.NullOrEmpty())
            {
                UiKit.LabelFit(new Rect(cr.x + 4f, cr.y + 2f, cr.width - 6f, 20f), "-", UiKit.Dim, GameFont.Tiny);
                return;
            }
            UiKit.LabelFit(new Rect(cr.x + 4f, cr.y + 2f, cr.width - 6f, 20f), cell.text, cell.color.a > 0f ? cell.color : Color.white);
            if (!cell.sub.NullOrEmpty())
                UiKit.LabelFit(new Rect(cr.x + 4f, cr.y + 20f, cr.width - 6f, 18f), cell.sub, cell.subColor.a > 0f ? cell.subColor : UiKit.Faint, GameFont.Tiny);
        }

        private static void DrawName(Rect cr, Row row, ref Cell cell, bool over)
        {
            const float size = 30f;
            UiKit.Portrait(new Rect(cr.x + 4f, cr.y + 5f, size, size), row.pawn, UiKit.TrustColor(row.cells[(int)Col.Trust].sort));
            float x = cr.x + size + 10f;
            float right = cr.xMax - 2f;
            if (row.pinned || over)
            {
                if (WeekPanel.PinButton(new Rect(right - 16f, cr.y + 3f, 16f, 16f), row.pinned, over))
                {
                    WeekMemory.Instance?.TogglePin(row.pawn);
                    row.pinned = !row.pinned;
                }
                right -= 18f;
            }
            UiKit.LabelFit(new Rect(x, cr.y + 2f, Mathf.Max(0f, right - x), 20f), cell.text, cell.color);
            if (!cell.sub.NullOrEmpty()) UiKit.LabelFit(new Rect(x, cr.y + 20f, Mathf.Max(0f, cr.xMax - 2f - x), 18f), cell.sub, cell.subColor, GameFont.Tiny);
            if (!row.rowTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(cr.x, cr.y, right - cr.x, cr.height), row.rowTip);
        }

        private static void DrawMood(Rect cr, Row row, ref Cell cell)
        {
            var bar = new Rect(cr.x + 4f, cr.y + 8f, Mathf.Max(10f, cr.width - 8f), 8f);
            UiKit.Bar(bar, row.mood, row.moodColor);
            // The minor break line.
            if (row.minor > 0f) Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * row.minor, bar.y - 2f, 1f, bar.height + 4f), UiKit.WithAlpha(UiKit.Warn, 0.9f));
            UiKit.LabelFit(new Rect(cr.x + 4f, cr.y + 19f, cr.width - 6f, 18f), cell.text, row.moodColor, GameFont.Tiny);
        }

        private static void Select(Pawn p)
        {
            if (p == null) return;
            if (p.Spawned || p.CarriedBy != null)
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p);
            }
            else MainTabWindow_Council.OpenPerson(p);
        }

        private static void Menu(Pawn p)
        {
            if (p == null) return;
            var memory = WeekMemory.Instance;
            bool pinned = memory != null && memory.IsPinned(p);
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_Week_OpenPage".Translate(p.LabelShort).Resolve(), () => MainTabWindow_Council.OpenPerson(p)),
                new FloatMenuOption("CC_Table_MoodTab".Translate(), () => MoodView.ShowMoodTab(p)),
                new FloatMenuOption(pinned ? PinTips.Unpin : PinTips.Pin, () =>
                {
                    memory?.TogglePin(p);
                    Dirty();
                })
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ---------------- The rows ----------------

        private static void Build(CouncilGameComponent comp)
        {
            builtAt = Time.realtimeSinceStartup;
            if (headers == null)
            {
                header = "CC_Table_Header".Translate();
                subtitle = "CC_UI_Sub_Everyone".Translate();
                headerTip = "CC_Table_HeaderTip".Translate();
                noneAll = "CC_People_None".Translate();
                noneShown = "CC_Table_NoneShown".Translate();
                var filters = (Filter[])Enum.GetValues(typeof(Filter));
                filterLabels = new string[filters.Length];
                filterTips = new string[filters.Length];
                for (int i = 0; i < filters.Length; i++)
                {
                    filterLabels[i] = ("CC_Table_Filter_" + filters[i]).Translate();
                    filterTips[i] = ("CC_Table_Filter_" + filters[i] + "_Tip").Translate();
                }
                headers = new string[Cols.Length];
                headerTips = new string[Cols.Length];
                for (int i = 0; i < Cols.Length; i++)
                {
                    headers[i] = ("CC_Table_Col_" + Cols[i]).Translate();
                    headerTips[i] = ("CC_Table_Col_" + Cols[i] + "_Tip").Translate() + "\n\n" + "CC_Table_SortTip".Translate().ToString().Colorize(ColoredText.SubtleGrayColor);
                }
            }
            var list = new List<Row>();
            List<Pawn> colonists;
            try
            {
                colonists = WeekModel.Colonists();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("people table", e);
                colonists = new List<Pawn>();
            }
            foreach (var p in colonists)
            {
                try
                {
                    list.Add(RowFor(comp, p));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("people table row", e);
                }
            }
            rows = list;
            countText = "CC_UI_Stat_People".Translate(rows.Count).Resolve();
            Refilter();
        }

        private static void Refilter()
        {
            shown.Clear();
            foreach (var r in rows)
                if (filter == Filter.All || (filter == Filter.Attention && r.attention) || (filter == Filter.Pinned && r.pinned))
                    shown.Add(r);
            int col = (int)sortBy;
            // Names sort by name; everything else by its number, ties by name.
            Comparison<Row> cmp = sortBy == Col.Name || sortBy == Col.Story
                ? (a, b) => string.Compare(a.cells[col].text ?? "￿", b.cells[col].text ?? "￿", StringComparison.CurrentCultureIgnoreCase)
                : (a, b) => a.cells[col].sort.CompareTo(b.cells[col].sort);
            shown.Sort((a, b) =>
            {
                int c = cmp(a, b);
                if (descending) c = -c;
                return c != 0 ? c : string.Compare(a.pawn.LabelShort, b.pawn.LabelShort, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private static Row RowFor(CouncilGameComponent comp, Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            var row = new Row { pawn = p, pinned = WeekMemory.Instance?.IsPinned(p) ?? false };
            var c = row.cells;
            int now = Find.TickManager.TicksGame;

            // Name, with office and epithet under it.
            var office = s.offices ? comp.offices.OfficeOf(p) : null;
            string titles = s.epithets ? comp.epithets.TitlesOf(p) : null;
            var under = new List<string>(2);
            if (office != null) under.Add(office.LabelCap);
            if (!titles.NullOrEmpty()) under.Add(titles);
            c[(int)Col.Name] = new Cell
            {
                text = p.LabelShortCap,
                sub = under.Count > 0 ? string.Join(" · ", under) : p.story?.TitleShortCap,
                color = Color.white,
                subColor = office != null ? UiKit.Gold : UiKit.Faint
            };

            // Mood, and what Rimfolk's own moods add up to.
            var lines = MoodInsight.LinesFor(p);
            row.mood = p.needs?.mood?.CurLevelPercentage ?? 0f;
            row.minor = 0.35f;
            row.major = 0.2f;
            try
            {
                var br = p.mindState?.mentalBreaker;
                if (br != null)
                {
                    row.minor = br.BreakThresholdMinor;
                    row.major = br.BreakThresholdMajor;
                }
            }
            catch
            {
            }
            row.moodColor = WeekModel.MoodColor(p, row.mood);
            float rimfolk = 0f;
            int ours = 0;
            foreach (var l in lines)
                if (MoodInsight.SourceOf(l.def).kind == MoodSource.Rimfolk)
                {
                    rimfolk += l.mood;
                    ours++;
                }
            c[(int)Col.Mood] = new Cell
            {
                text = p.InMentalState ? "CC_Table_InBreak".Translate(row.mood.ToStringPercent()).Resolve() : row.mood.ToStringPercent(),
                tip = MoodLines(p, lines) + "\n\n" + "CC_Table_ClickMood".Translate().ToString().Colorize(ColoredText.SubtleGrayColor),
                sort = row.mood
            };
            c[(int)Col.Rimfolk] = new Cell
            {
                text = Mathf.Abs(rimfolk) < 0.05f ? "0" : MoodView.Number(rimfolk),
                color = rimfolk > 0.05f ? MoodView.GoodColor : rimfolk < -0.05f ? MoodView.BadColor : UiKit.Faint,
                // How many of their moods are Rimfolk's, as "×3" under the sum.
                sub = ours > 0 ? "CC_Table_RimfolkSub".Translate(ours).Resolve() : null,
                subColor = UiKit.Dim,
                tip = RimfolkTip(p, lines, rimfolk),
                sort = rimfolk
            };

            // Trust and its week.
            var st = comp.standings.For(p, false);
            float trust = st?.trust ?? 0f;
            float? trend = WeekMemory.Instance?.TrustTrend(p, trust);
            bool leaving = st != null && st.leavingTick > 0;
            c[(int)Col.Trust] = new Cell
            {
                text = ("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate().CapitalizeFirst(),
                color = UiKit.TrustColor(trust),
                sub = leaving ? "CC_Trust_Leaving".Translate(TextUtil.DaysString(Mathf.Max(0, st.leavingTick - now))).Resolve()
                    : trend.HasValue && Mathf.Abs(trend.Value) >= 1f ? "CC_Week_TrustMoved".Translate(trend.Value.ToString("+0;-0")).Resolve() : trust.ToString("0"),
                subColor = leaving || (trend ?? 0f) <= -5f ? UiKit.Bad : (trend ?? 0f) >= 5f ? UiKit.Good : UiKit.Faint,
                tip = TrustTip(comp, p, st),
                sort = trust
            };

            // Strain and the feeling on top.
            var h = Heart.ForView(p);
            if (h != null)
                c[(int)Col.Strain] = new Cell
                {
                    text = HeartView.BandLabel(h.Band).CapitalizeFirst() + " (" + h.strain.ToString("0") + ")",
                    color = HeartView.BandColor(h.Band),
                    sub = HeartView.DominantText(h),
                    subColor = UiKit.Faint,
                    tip = HeartView.StrainTip(p, h) + (h.feelings.Count > 0 ? "\n\n" + HeartView.FeelingsTip(h) : "") + NeedsPart(p, h),
                    sort = h.strain
                };
            else c[(int)Col.Strain] = new Cell { sort = -1f };

            // Setbacks and pressures, worst first, with what helps.
            Weighing(lines, ref c[(int)Col.Weighing]);

            // Their open wish.
            var wish = s.enableWishes ? comp.OpenWishes.Where(w => w.pawn == p).OrderBy(w => w.TicksLeft).FirstOrDefault() : null;
            if (wish != null)
            {
                int left = wish.TicksLeft;
                float frac = left / (float)Mathf.Max(1, wish.expiresTick - wish.createdTick);
                c[(int)Col.Wish] = new Cell
                {
                    text = wish.Label,
                    color = wish.promised ? UiKit.Gold : Color.white,
                    sub = "CC_Week_Left".Translate(UiKit.Duration(left)).Resolve() + (wish.promised ? " · " + "CC_PromisedTag".Translate() : ""),
                    subColor = UiKit.Urgency(frac),
                    tip = "\"" + wish.quote + "\"" + (wish.reason.NullOrEmpty() ? "" : "\n\n" + "CC_Why".Translate() + ": " + wish.reason)
                          + "\n\n" + "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName("Wishes")).Resolve().Colorize(ColoredText.SubtleGrayColor),
                    sort = left
                };
            }
            else c[(int)Col.Wish] = new Cell { sort = float.MaxValue };

            // Their personal story.
            var arc = ArcTracker.On ? ArcTracker.Instance?.Of(p) : null;
            if (arc?.def != null && arc.Stage != null)
            {
                string waiting = null;
                try
                {
                    waiting = ArcText.Waiting(arc);
                }
                catch
                {
                }
                c[(int)Col.Story] = new Cell
                {
                    text = arc.def.LabelCap,
                    sub = "CC_Table_Stage".Translate(arc.Stage.label, arc.stage + 1, arc.def.stages.Count).Resolve(),
                    subColor = UiKit.Faint,
                    color = UiKit.DreamColor,
                    tip = "CC_Arc_Story_Now".Translate(arc.def.LabelCap, arc.Stage.label, arc.stage + 1, arc.def.stages.Count).Resolve() + (waiting.NullOrEmpty() ? "" : "\n\n" + waiting),
                    sort = arc.stage
                };
            }

            // Ailments and convalescence.
            HealthCell(p, ref c[(int)Col.Health]);

            // Quirks.
            var quirks = s.quirks ? comp.quirks.Of(p).Where(q => !q.Broken).ToList() : new List<Quirk>();
            if (quirks.Count > 0)
                c[(int)Col.Quirks] = new Cell
                {
                    text = quirks[0].Label.CapitalizeFirst(),
                    sub = quirks.Count > 1 ? (quirks.Count == 2 ? quirks[1].Label : "CC_Table_More".Translate(quirks.Count - 1).Resolve()) : null,
                    subColor = UiKit.Faint,
                    color = new Color(0.85f, 0.85f, 0.85f),
                    tip = string.Join("\n\n", quirks.Select(q => q.Label.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor) + (q.def.description.NullOrEmpty() ? "" : "\n" + q.def.description))),
                    sort = quirks.Count
                };

            var heartBand = h?.Band ?? StrainBand.Steady;
            row.attention = p.InMentalState || row.mood < row.minor + 0.05f || heartBand >= StrainBand.Worn || leaving
                            || (wish != null && wish.TicksLeft < GenDate.TicksPerDay) || c[(int)Col.Health].color == UiKit.Bad;
            row.rowTip = Summary(p) + "\n\n" + "CC_Table_RowClick".Translate().ToString().Colorize(ColoredText.SubtleGrayColor);
            return row;
        }

        // ---------------- Setbacks and pressures: what helps ----------------

        /// <summary>A setback or an everyday pressure (Setbacks/, Pressures/), by its class, its name or its mood hint.</summary>
        public static bool IsSetbackOrPressure(ThoughtDef d)
        {
            if (d == null) return false;
            if (typeof(Thought_Setback).IsAssignableFrom(d.ThoughtClass) || typeof(Thought_Pressure).IsAssignableFrom(d.ThoughtClass)) return true;
            if (d.defName.StartsWith("Council_Setback_") || d.defName.StartsWith("Council_Pressure_")) return true;
            var hint = MoodInsight.HintFor(d);
            return hint != null && (hint.defName.Contains("Setback") || hint.defName.Contains("Pressure"));
        }

        private static void Weighing(List<MoodLine> lines, ref Cell cell)
        {
            var bad = lines.Where(l => l.mood < 0f && IsSetbackOrPressure(l.def)).OrderBy(l => l.mood).ToList();
            if (bad.Count == 0)
            {
                cell.sort = 0f;
                return;
            }
            float sum = bad.Sum(l => l.mood);
            cell.text = bad[0].label;
            cell.color = UiKit.Warn;
            cell.sub = bad.Count > 1 ? "CC_Table_WeighingMore".Translate(bad.Count - 1, MoodView.Number(sum)).Resolve() : MoodView.Number(sum);
            cell.subColor = MoodView.BadColor;
            cell.tip = WhatHelps(bad);
            cell.sort = sum;
        }

        /// <summary>Each setback and pressure with its size, why, and what helps (its mood hint).</summary>
        public static string WhatHelps(List<MoodLine> bad)
        {
            var sb = new StringBuilder();
            sb.Append("CC_Table_WeighingHead".Translate().ToString().Colorize(ColoredText.TipSectionTitleColor));
            foreach (var l in bad)
            {
                sb.Append("\n\n").Append(l.label).Append("  ").Append(MoodView.Number(l.mood).Colorize(MoodView.BadColor));
                if (!l.why.NullOrEmpty()) sb.Append("\n").Append("CC_Cause_Line".Translate(l.why).Resolve().Colorize(ColoredText.SubtleGrayColor));
                string fix = MoodInsight.HintFor(l.def)?.fix;
                if (!fix.NullOrEmpty()) sb.Append("\n").Append("CC_Table_WhatHelps".Translate(fix).Resolve());
            }
            return sb.ToString();
        }

        // ---------------- Health ----------------

        private static void HealthCell(Pawn p, ref Cell cell)
        {
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            var found = new List<Hediff>();
            bool serious = false;
            foreach (var h in hediffs)
            {
                if (h?.def == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                bool lethal = h.def.lethalSeverity > 0f && h.def.makesSickThought && h.def.isBad;
                if (info == null && !lethal && h.def != HealthDefOf.Council_Ail_Convalescent) continue;
                if (info?.serious == true || lethal) serious = true;
                found.Add(h);
            }
            if (found.Count == 0) return;
            // The serious first, convalescence last.
            found.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            cell.text = found[0].LabelCap;
            cell.color = serious ? UiKit.Bad : found[0].def == HealthDefOf.Council_Ail_Convalescent ? UiKit.Info : UiKit.Warn;
            cell.sub = found.Count > 1 ? (found.Count == 2 ? found[1].LabelCap : "CC_Table_More".Translate(found.Count - 1).Resolve()) : null;
            cell.subColor = UiKit.Faint;
            var parts = new List<string>(found.Count);
            foreach (var h in found)
            {
                try
                {
                    parts.Add(h.GetTooltip(p, false));
                }
                catch
                {
                    parts.Add(h.LabelCap);
                }
            }
            cell.tip = string.Join("\n\n", parts) + "\n\n" + "CC_Table_ClickHealth".Translate().ToString().Colorize(ColoredText.SubtleGrayColor);
            cell.sort = found.Count + (serious ? 10f : 0f);
        }

        private static int Rank(Hediff h)
        {
            if (h.def == HealthDefOf.Council_Ail_Convalescent) return 2;
            var info = AilmentTracker.InfoOf(h.def);
            return info?.serious == true || h.def.lethalSeverity > 0f ? 0 : 1;
        }

        // ---------------- Tooltips ----------------

        /// <summary>The worst and best of their moods, with how much is Rimfolk's.</summary>
        public static string MoodLines(Pawn p, List<MoodLine> lines = null)
        {
            if (p?.needs?.mood == null) return null;
            lines ??= MoodInsight.LinesFor(p);
            var sb = new StringBuilder();
            sb.Append("CC_Table_MoodHead".Translate(p.LabelShort, p.needs.mood.CurLevelPercentage.ToStringPercent()).Resolve().Colorize(ColoredText.TipSectionTitleColor));
            int n = 0;
            foreach (var l in lines)
            {
                if (l.mood >= 0f || n >= 4) continue;
                sb.Append("\n").Append(MoodView.Number(l.mood).Colorize(MoodView.BadColor)).Append("  ").Append(l.label);
                n++;
            }
            n = 0;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var l = lines[i];
                if (l.mood <= 0f || n >= 3) continue;
                sb.Append("\n").Append(MoodView.Number(l.mood).Colorize(MoodView.GoodColor)).Append("  ").Append(l.label);
                n++;
            }
            return sb.ToString();
        }

        private static string RimfolkTip(Pawn p, List<MoodLine> lines, float total)
        {
            var sb = new StringBuilder();
            sb.Append("CC_Table_RimfolkHead".Translate(p.LabelShort, MoodView.Number(total)).Resolve().Colorize(ColoredText.TipSectionTitleColor));
            foreach (var l in lines)
            {
                if (MoodInsight.SourceOf(l.def).kind != MoodSource.Rimfolk) continue;
                sb.Append("\n").Append(MoodView.Number(l.mood).Colorize(MoodView.ColorFor(l.mood))).Append("  ").Append(l.label);
                if (!l.why.NullOrEmpty()) sb.Append(("  (" + l.why + ")").Colorize(ColoredText.SubtleGrayColor));
            }
            return sb.ToString();
        }

        private static string TrustTip(CouncilGameComponent comp, Pawn p, PawnStanding st)
        {
            if (st == null) return "CC_Table_NoTrustYet".Translate(p.LabelShort).Resolve();
            string tip = "CC_Trust_Tip".Translate(p.LabelShort, st.trust.ToString("0"), st.granted, st.ignored, st.refused, st.promisesKept, st.promisesBroken).Resolve();
            if (st.log.Count > 0) tip += "\n\n" + string.Join("\n", st.log.AsEnumerable().Reverse());
            return tip;
        }

        /// <summary>The heart's needs, explained, after a blank line (for the strain cell's tooltip); empty without them.</summary>
        private static string NeedsPart(Pawn p, PawnHeart h)
        {
            string needs = HeartView.NeedsTip(p, h);
            return needs.NullOrEmpty() ? "" : "\n\n" + needs;
        }

        /// <summary>
        /// Everything at a glance about one colonist, for the table's name cell and This week's pinned lines: mood,
        /// trust, strain, wish, story, health, quirks, office and names.
        /// </summary>
        public static string Summary(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp == null) return null;
            var sb = new StringBuilder();
            sb.Append(p.LabelShortCap.Colorize(ColoredText.TipSectionTitleColor));
            if (p.story != null) sb.Append(", ").Append(p.story.TitleShortCap);
            float? mood = p.needs?.mood?.CurLevelPercentage;
            if (mood.HasValue) sb.Append("\n").Append("CC_Table_SummaryMood".Translate(mood.Value.ToStringPercent()).Resolve());
            float trust = comp.standings.TrustOf(p);
            sb.Append("\n").Append("CC_Story_Trust".Translate(("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate()).Resolve());
            var h = Heart.ForView(p);
            if (h != null)
            {
                sb.Append("\n").Append("CC_Table_SummaryStrain".Translate(HeartView.BandLabel(h.Band), h.strain.ToString("0")).Resolve());
                string feel = HeartView.DominantText(h);
                if (!feel.NullOrEmpty()) sb.Append(" · ").Append(feel);
                string needs = HeartView.NeedsShort(h);
                if (!needs.NullOrEmpty()) sb.Append("\n").Append("CC_Table_SummaryNeeds".Translate(needs).Resolve());
            }
            var wish = comp.OpenWishes.FirstOrDefault(w => w.pawn == p);
            if (wish != null) sb.Append("\n").Append("CC_Table_SummaryWish".Translate(wish.Label, UiKit.Duration(wish.TicksLeft)).Resolve());
            var arc = ArcTracker.On ? ArcTracker.Instance?.Of(p) : null;
            if (arc?.def != null && arc.Stage != null) sb.Append("\n").Append("CC_Table_SummaryStory".Translate(arc.def.label, arc.Stage.label).Resolve());
            string quirks = comp.quirks.Describe(p);
            if (!quirks.NullOrEmpty()) sb.Append("\n").Append(quirks);
            var office = comp.offices.OfficeOf(p);
            if (office != null) sb.Append("\n").Append("CC_Office_Holds".Translate(p.LabelShort, office.label).Resolve());
            string titles = comp.epithets.TitlesOf(p);
            if (!titles.NullOrEmpty()) sb.Append("\n").Append("CC_Epithet_Known".Translate(p.LabelShort, titles).Resolve());
            return sb.ToString();
        }
    }
}
