using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>One mood as a compact row: its group, number, label and short line, with its whole card a click away.</summary>
    public class CompactRow
    {
        /// <summary>The same from one refresh to the next, so an open card stays open.</summary>
        public string key;
        public MoodGroup group;
        public float mood;
        /// <summary>The label with ×count (or, on the Moods page, how many colonists have it).</summary>
        public string label;
        /// <summary>The short action, or when it ends, or what kind of mood it is.</summary>
        public string right;
        /// <summary>The short line is something to do (accent colour).</summary>
        public bool gold;
        public int ticksLeft;
        /// <summary>The whole card (why, when it ends, where it's from, what helps, the buttons).</summary>
        public MoodView.Texts texts;
        /// <summary>The game's description, asked only when shown.</summary>
        public Func<string> description;
        public int tipId;

        // Laid out once per width, not every frame.
        public float laidFor = -1f, labelX, labelW, rightX, rightW;
        public string labelShown, rightShown;
        private string tip;

        /// <summary>
        /// The row's tooltip, the same as its card says: the game's description, when it ends and where it's from, why,
        /// and what helps. Worked out the first time it's shown.
        /// </summary>
        public string Tip()
        {
            if (tip != null) return tip;
            var parts = new List<string>();
            try
            {
                string d = description?.Invoke();
                if (!d.NullOrEmpty()) parts.Add(d.TrimEnd());
            }
            catch
            {
            }
            if (!texts.meta.NullOrEmpty()) parts.Add(texts.meta.Colorize(ColoredText.SubtleGrayColor));
            if (!texts.why.NullOrEmpty()) parts.Add(texts.why);
            if (!texts.advice.NullOrEmpty()) parts.Add(texts.bad ? texts.advice.Colorize(MoodView.AdviceColor) : texts.advice);
            parts.Add(MoodCompact.ClickForCard.Colorize(ColoredText.SubtleGrayColor));
            return tip = string.Join("\n\n", parts);
        }
    }

    /// <summary>A page's moods in their groups (MoodSortMath), with the texts for headings and chips, built a couple of times a second.</summary>
    public class CompactModel
    {
        public readonly List<CompactRow>[] groups = new List<CompactRow>[MoodSortMath.GroupCount];
        public readonly float[] totals = new float[MoodSortMath.GroupCount];
        /// <summary>The fold heading, its note on the right (the total), and the chip, for each group.</summary>
        public readonly string[] titles = new string[MoodSortMath.GroupCount], rights = new string[MoodSortMath.GroupCount],
            chips = new string[MoodSortMath.GroupCount];
        /// <summary>Each chip's width in the tiny font, measured the first time it's drawn.</summary>
        public readonly float[] chipWidths = { -1f, -1f, -1f, -1f };
        /// <summary>When the last fading mood is gone (ticks), or -1.</summary>
        public int lastGone = -1;
        public ThirdKind third;
        /// <summary>The largest mood shown, for the faint bars behind the rows.</summary>
        public float largest;
        /// <summary>"+22 from 9: Fine meal +5, ..." for the folded good moods.</summary>
        public string liftSummary;
        public bool anyBad;

        public CompactModel()
        {
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CompactRow>();
        }

        public int Count => groups.Sum(g => g.Count);
    }

    /// <summary>What one page remembers between frames: folded groups, the one open card, and where each group starts.</summary>
    public class CompactState
    {
        public readonly Folds folds = new Folds();
        /// <summary>The row whose card is open (only one at a time), by key.</summary>
        public string expanded;
        /// <summary>Where each group's heading was last drawn in the scroll view, for the chips.</summary>
        public readonly float[] groupY = new float[MoodSortMath.GroupCount];
        /// <summary>A chip was clicked: scroll to this group next frame (-1 for none).</summary>
        public int scrollTo = -1;

        /// <param name="heartFolded">Start with the heart section folded away (the colonist's tab).</param>
        public CompactState(bool heartFolded)
        {
            // The good moods start folded to one line; so does the heart in the colonist's tab.
            folds.Toggle(MoodCompact.LiftingKey);
            if (heartFolded) folds.Toggle("heart");
        }

        /// <summary>A new colonist: start at the top, nothing open.</summary>
        public void Forget()
        {
            expanded = null;
            scrollTo = -1;
        }
    }

    /// <summary>
    /// The compact view of moods, shared by a colonist's Mood tab and the council's Moods page: chips with each group's
    /// total, then each group as a fold with one line per mood (a faint bar for its size, the number, the label, the
    /// short action or when it ends). Hover a line for everything its card says; click it for the card itself, with its
    /// buttons. Only the lines in sight are drawn.
    /// </summary>
    public static class MoodCompact
    {
        public const string LiftingKey = "c.lifting";
        private static readonly string[] keys = { "c.fix", "c.pass", "c.faith", LiftingKey };
        private const float ChipHeight = 22f;

        private static string clickForCard, compactLabel, detailsLabel, compactTip, detailsTip, foldedTip;
        private static string[] chipTips;

        public static string ClickForCard => clickForCard ??= "CC_MoodC_ClickForCard".Translate().Resolve();

        public static Texture2D IconFor(MoodGroup g)
        {
            switch (g)
            {
                case MoodGroup.Fix: return Icons.CatManagement;
                case MoodGroup.Pass: return Icons.History;
                case MoodGroup.Faith: return Icons.CatIdeology;
                default: return Icons.TrendUp;
            }
        }

        public static Color ColorOf(MoodGroup g)
        {
            switch (g)
            {
                case MoodGroup.Fix: return UiKit.Gold;
                case MoodGroup.Pass: return UiKit.Faint;
                case MoodGroup.Faith: return UiKit.Info;
                default: return MoodView.GoodColor;
            }
        }

        // ---------------- The model ----------------

        /// <summary>A colonist's moods in their groups. <paramref name="lines"/> must already have their texts.</summary>
        public static CompactModel For(Pawn p, List<MoodLine> lines)
        {
            var m = new CompactModel();
            int faith = 0, other = 0;
            foreach (var l in lines)
            {
                try
                {
                    var g = MoodGroups.GroupOf(l);
                    var line = l;
                    var row = new CompactRow
                    {
                        key = (l.def?.defName ?? "?") + "|" + l.label,
                        group = g,
                        mood = l.mood,
                        ticksLeft = l.ticksLeft,
                        texts = l.texts ?? (l.texts = MoodView.TextsFor(l)),
                        description = () => MoodInsight.Description(line)
                    };
                    row.label = row.texts.title;
                    row.tipId = row.texts.tipId ^ 0x5C0FF;
                    row.right = MoodGroups.Right(l, g, out row.gold);
                    if (g == MoodGroup.Faith)
                    {
                        if (MoodGroups.FromFaith(l.thought, l.def)) faith++;
                        else other++;
                    }
                    m.groups[(int)g].Add(row);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("compact mood row " + l?.def?.defName, e);
                }
            }
            Finish(m, faith, other, p.LabelShort, false);
            return m;
        }

        /// <summary>The colony's moods by kind in their groups (the council's Moods page). Each kind's texts must be worked out already.</summary>
        public static CompactModel For(List<ColonyMoodLine> colony)
        {
            var m = new CompactModel();
            int faith = 0, other = 0;
            foreach (var c in colony)
            {
                try
                {
                    var g = MoodGroups.GroupOf(c, out int ticksLeft);
                    var sample = c.sample;
                    var row = new CompactRow
                    {
                        key = (c.def?.defName ?? "?") + (c.good ? "|+" : "|-"),
                        group = g,
                        mood = c.total,
                        ticksLeft = ticksLeft,
                        texts = c.texts ?? (c.texts = MoodView.TextsFor(c)),
                        description = () => MoodInsight.Description(sample)
                    };
                    int people = c.byPawn.Count;
                    row.label = people == 1 ? "CC_MoodC_ColonyLabelOne".Translate(c.label).Resolve() : "CC_MoodC_ColonyLabel".Translate(c.label, people).Resolve();
                    row.tipId = row.texts.tipId ^ 0x5C0FF;
                    bool fromFaith = g == MoodGroup.Faith && c.lines.Any(l => MoodGroups.FromFaith(l.thought, l.def));
                    row.right = MoodGroups.Right(g, c.def, c.total, ticksLeft, fromFaith, out row.gold);
                    if (g == MoodGroup.Faith)
                    {
                        if (fromFaith) faith++;
                        else other++;
                    }
                    m.groups[(int)g].Add(row);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("compact colony mood row " + c?.def?.defName, e);
                }
            }
            Finish(m, faith, other, null, true);
            return m;
        }

        private static void Finish(CompactModel m, int faith, int other, string who, bool colony)
        {
            for (int i = 0; i < MoodSortMath.GroupCount; i++)
            {
                var g = (MoodGroup)i;
                MoodSortMath.Sort(m.groups[i], g, r => r.mood, r => r.label);
                m.totals[i] = m.groups[i].Sum(r => r.mood);
            }
            m.anyBad = m.groups[0].Count + m.groups[1].Count + m.groups[2].Count > 0;
            m.lastGone = MoodSortMath.LastGone(m.groups[(int)MoodGroup.Pass].Select(r => r.ticksLeft));
            m.third = MoodSortMath.ThirdOf(faith, other);
            m.largest = MoodSortMath.Largest(m.groups.SelectMany(g => g).Select(r => r.mood));

            string days = m.lastGone > 0 ? TextUtil.DaysString(m.lastGone) : null;
            for (int i = 0; i < MoodSortMath.GroupCount; i++)
            {
                var g = (MoodGroup)i;
                string total = MoodView.Number(m.totals[i]);
                string name = ShortName(g, m.third);
                m.titles[i] = Title(g, m.third, who, colony);
                m.rights[i] = g == MoodGroup.Pass && days != null ? "CC_MoodC_PassRight".Translate(total, days).Resolve() : total;
                m.chips[i] = g == MoodGroup.Pass && days != null
                    ? "CC_MoodC_ChipDays".Translate(name, total, days).Resolve()
                    : "CC_MoodC_Chip".Translate(name, total).Resolve();
            }
            var good = m.groups[(int)MoodGroup.Lifting];
            if (good.Count > 0)
            {
                int named = MoodSortMath.Named(good.Count);
                string list = string.Join(", ", good.Take(named).Select(r => r.label + " " + MoodView.Number(r.mood)));
                if (good.Count > named) list = "CC_MoodC_AndMore".Translate(list, good.Count - named).Resolve();
                m.liftSummary = "CC_MoodC_LiftSummary".Translate(MoodView.Number(m.totals[(int)MoodGroup.Lifting]), good.Count, list).Resolve();
            }
        }

        /// <summary>A group's name on its chip and in the Needs tab's line: "You can fix", "Will pass", "Faith", "Lifting".</summary>
        public static string ShortName(MoodGroup g, ThirdKind third)
        {
            switch (g)
            {
                case MoodGroup.Fix: return "CC_MoodC_Short_Fix".Translate();
                case MoodGroup.Pass: return "CC_MoodC_Short_Pass".Translate();
                case MoodGroup.Faith:
                    return (third == ThirdKind.Faith ? "CC_MoodC_Short_Faith" : third == ThirdKind.Nature ? "CC_MoodC_Short_Nature" : "CC_MoodC_Short_Both").Translate();
                default: return "CC_MoodC_Short_Lifting".Translate();
            }
        }

        private static string Title(MoodGroup g, ThirdKind third, string who, bool colony)
        {
            switch (g)
            {
                case MoodGroup.Fix: return "CC_MoodC_Fix".Translate();
                case MoodGroup.Pass: return "CC_MoodC_Pass".Translate();
                case MoodGroup.Faith:
                    return (third == ThirdKind.Faith ? "CC_MoodC_Title_Faith" : third == ThirdKind.Nature ? "CC_MoodC_Title_Nature" : "CC_MoodC_Title_Both").Translate();
                default: return colony ? "CC_Moods_Lifting".Translate().Resolve() : "CC_Mood_Lifting".Translate(who ?? "").Resolve();
            }
        }

        // ---------------- Drawing ----------------

        /// <summary>The chips with each group's total; a click scrolls to the group (and unfolds it). Returns the height used.</summary>
        public static float DrawChips(float x, float y, float w, CompactModel m, CompactState st)
        {
            if (chipTips == null)
                chipTips = new[]
                {
                    "CC_MoodC_Tip_Fix".Translate().Resolve(), "CC_MoodC_Tip_Pass".Translate().Resolve(),
                    "CC_MoodC_Tip_Faith".Translate().Resolve(), "CC_MoodC_Tip_Lifting".Translate().Resolve()
                };
            float cx = x, cy = y;
            bool any = false;
            var oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            try
            {
                for (int i = 0; i < MoodSortMath.GroupCount; i++)
                {
                    if (m.groups[i].Count == 0) continue;
                    var g = (MoodGroup)i;
                    if (m.chipWidths[i] < 0f) m.chipWidths[i] = Text.CalcSize(m.chips[i]).x + 30f;
                    float cw = Mathf.Min(m.chipWidths[i], w);
                    if (cx > x && cx + cw > x + w)
                    {
                        cx = x;
                        cy += ChipHeight + 4f;
                    }
                    var r = new Rect(cx, cy, cw, ChipHeight);
                    var c = ColorOf(g);
                    Widgets.DrawBoxSolidWithOutline(r, UiKit.WithAlpha(c, Mouse.IsOver(r) ? 0.24f : 0.12f), UiKit.WithAlpha(c, 0.45f), 1);
                    UiKit.Icon(new Rect(r.x + 6f, r.center.y - 6f, 12f, 12f), IconFor(g), c);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = c;
                    Widgets.Label(new Rect(r.x + 22f, r.y, Mathf.Max(0f, r.width - 24f), r.height), UiKit.Fit(m.chips[i], r.width - 26f));
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    TooltipHandler.TipRegion(r, chipTips[i]);
                    if (Widgets.ButtonInvisible(r))
                    {
                        st.scrollTo = i;
                        if (!st.folds.IsOpen(keys[i])) st.folds.Toggle(keys[i]);
                    }
                    cx += cw + 6f;
                    any = true;
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            return any ? cy - y + ChipHeight : 0f;
        }

        /// <summary>Where a chip asked to scroll to, if one was clicked since the last frame (then forgotten), or -1.</summary>
        public static float TakeScroll(CompactState st)
        {
            if (st.scrollTo < 0) return -1f;
            float y = st.groupY[st.scrollTo];
            st.scrollTo = -1;
            return Mathf.Max(0f, y);
        }

        /// <summary>
        /// Every group with its rows, inside a scroll view whose visible part is <paramref name="top"/> to top +
        /// <paramref name="view"/>. Returns the new y.
        /// </summary>
        public static float DrawGroups(CompactModel m, CompactState st, float y, float w, float top, float view, string nothingBad)
        {
            if (!m.anyBad && !nothingBad.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, nothingBad, MoodView.Faint) + 8f;
            for (int i = 0; i < MoodSortMath.GroupCount; i++)
            {
                var rows = m.groups[i];
                if (rows.Count == 0) continue;
                var g = (MoodGroup)i;
                st.groupY[i] = y;
                y += UiKit.FoldTitle(0f, y, w, m.titles[i], st.folds, keys[i], out bool open, IconFor(g), ColorOf(g), m.rights[i]) - 2f;
                if (!open)
                {
                    if (g == MoodGroup.Lifting && m.liftSummary != null) y = FoldedSummary(m.liftSummary, st, y, w);
                    y += 6f;
                    continue;
                }
                foreach (var r in rows) y = DrawRow(r, st, y, w, m.largest, top, view);
                y += 10f;
            }
            return y;
        }

        /// <summary>The good moods in one line while folded; a click unfolds them.</summary>
        private static float FoldedSummary(string text, CompactState st, float y, float w)
        {
            var r = new Rect(0f, y, w, 22f);
            Widgets.DrawHighlightIfMouseover(r);
            UiKit.Icon(new Rect(4f, y + 5f, 12f, 12f), TexButton.Reveal, UiKit.WithAlpha(MoodView.GoodColor, 0.7f));
            var oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = MoodView.Faint;
            Widgets.Label(new Rect(22f, y, w - 24f, 22f), UiKit.Fit(text, w - 26f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
            TooltipHandler.TipRegion(r, () => text + "\n\n" + (foldedTip ??= "CC_UI_UnfoldTip".Translate().Resolve()), text.GetHashCode() ^ 0x1F7);
            if (Widgets.ButtonInvisible(r, false))
            {
                st.folds.Toggle(LiftingKey);
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
            }
            return y + 24f;
        }

        /// <summary>One row, or its card if it's the open one. Drawn only in sight; returns the y below it.</summary>
        private static float DrawRow(CompactRow r, CompactState st, float y, float w, float largest, float top, float view)
        {
            if (st.expanded == r.key)
            {
                float h = MoodView.Measure(r.texts, w);
                if (MoodSortMath.InView(y, h, top, view))
                {
                    MoodView.Draw(0f, y, w, r.texts, r.mood, r.description);
                    // Anywhere on the card but its buttons folds it back to its line.
                    if (Widgets.ButtonInvisible(new Rect(0f, y, w, h), false))
                    {
                        st.expanded = null;
                        SoundDefOf.TabClose.PlayOneShotOnCamera();
                    }
                }
                return y + h + 3f;
            }
            float rh = MoodSortMath.RowHeight;
            if (!MoodSortMath.InView(y, rh, top, view)) return y + rh + 1f;
            var row = new Rect(0f, y, w, rh);
            Lay(r, w);
            // A faint bar for its size, against the largest mood shown.
            float f = MoodSortMath.BarFraction(r.mood, largest);
            var color = MoodView.ColorFor(r.mood);
            if (f > 0f) Widgets.DrawBoxSolid(new Rect(0f, y + 2f, Mathf.Max(2f, w * f), rh - 4f), UiKit.WithAlpha(color, 0.12f));
            Widgets.DrawHighlightIfMouseover(row);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, MoodSortMath.NumberWidth, rh), MoodView.Number(r.mood));
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(r.labelX, y, r.labelW, rh), r.labelShown);
            if (r.rightW > 0f)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = r.gold ? UiKit.Gold : MoodView.Faint;
                Widgets.Label(new Rect(r.rightX, y, r.rightW, rh), r.rightShown);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(row, new TipSignal(r.Tip, r.tipId));
            if (Widgets.ButtonInvisible(row, false))
            {
                st.expanded = r.key;
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
            }
            return y + rh + 1f;
        }

        /// <summary>Where the label and the short line go at this width, and their text cut to fit, once per width.</summary>
        private static void Lay(CompactRow r, float w)
        {
            if (Mathf.Abs(r.laidFor - w) < 0.5f) return;
            var oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float wanted = r.right.NullOrEmpty() ? 0f : Text.CalcSize(r.right).x + 2f;
            MoodSortMath.RowLayout(w, wanted, out r.labelX, out r.labelW, out r.rightX, out r.rightW);
            r.rightShown = r.rightW > 0f ? UiKit.Fit(r.right, r.rightW) : null;
            Text.Font = GameFont.Small;
            r.labelShown = UiKit.Fit(r.label, r.labelW);
            Text.Font = oldFont;
            r.laidFor = w;
        }

        /// <summary>
        /// The "Compact | Details" switch at the top right of a page, its right edge at <paramref name="right"/>. Flips
        /// the setting and saves it. Returns the width it took.
        /// </summary>
        public static float DrawViewToggle(float right, float y, float h = 24f)
        {
            float dw = ToggleWidth(true), cw = ToggleWidth(false);
            bool details = Details;
            float x = right - dw;
            if (UiKit.TabButton(new Rect(x, y, dw, h), detailsLabel, null, details, detailsTip) && !details) SetDetails(true);
            x -= cw + 2f;
            if (UiKit.TabButton(new Rect(x, y, cw, h), compactLabel, null, !details, compactTip) && details) SetDetails(false);
            return right - x;
        }

        /// <summary>The width <see cref="DrawViewToggle"/> takes.</summary>
        public static float ViewToggleWidth => ToggleWidth(true) + ToggleWidth(false) + 2f;

        private static float ToggleWidth(bool details)
        {
            compactLabel ??= "CC_MoodC_Compact".Translate().Resolve();
            detailsLabel ??= "CC_MoodC_Details".Translate().Resolve();
            compactTip ??= "CC_MoodC_CompactTip".Translate().Resolve();
            detailsTip ??= "CC_MoodC_DetailsTip".Translate().Resolve();
            return UiKit.TabButtonWidth(details ? detailsLabel : compactLabel, null) - 8f;
        }

        /// <summary>Whether the pages show every mood as a card (the switch's "Details"); compact otherwise.</summary>
        public static bool Details => ColonyCouncilMod.Settings?.moodDetails ?? false;

        private static void SetDetails(bool on)
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null) return;
            s.moodDetails = on;
            CouncilSettings.SaveNow();
        }
    }

    /// <summary>
    /// The one line at the top of the game's Needs tab mood list: "You can fix -15 · Will pass -11 · Faith -4 · Lifting +22",
    /// each part in its group's colour. Worked out every half second or so per colonist, not every frame.
    /// </summary>
    public static class MoodSummary
    {
        public class Line
        {
            public List<(string text, Color color)> parts = new List<(string, Color)>();
            /// <summary>The whole line as plain text, for measuring.</summary>
            public string plain;
            public string tip;
            public float width = -1f;
        }

        private static int pawnId = -1;
        private static float at = -1f;
        private static Line cached;

        public static void Reset()
        {
            pawnId = -1;
            cached = null;
        }

        /// <summary>The line for this colonist, or null when there's nothing to say (no moods worth a line).</summary>
        public static Line For(Pawn p)
        {
            if (p == null) return null;
            float now = Time.realtimeSinceStartup;
            if (pawnId == p.thingIDNumber && now - at < 0.5f && now >= at) return cached;
            pawnId = p.thingIDNumber;
            at = now;
            cached = Build(p);
            return cached;
        }

        private static Line Build(Pawn p)
        {
            try
            {
                var lines = MoodInsight.LinesFor(p);
                if (lines.Count == 0) return null;
                var totals = new float[MoodSortMath.GroupCount];
                var counts = new int[MoodSortMath.GroupCount];
                var pass = new List<int>();
                int faith = 0, other = 0;
                foreach (var l in lines)
                {
                    var g = MoodGroups.GroupOf(l);
                    totals[(int)g] += l.mood;
                    counts[(int)g]++;
                    if (g == MoodGroup.Pass) pass.Add(l.ticksLeft);
                    if (g == MoodGroup.Faith)
                    {
                        if (MoodGroups.FromFaith(l.thought, l.def)) faith++;
                        else other++;
                    }
                }
                var third = MoodSortMath.ThirdOf(faith, other);
                int lastGone = MoodSortMath.LastGone(pass);
                var line = new Line();
                for (int i = 0; i < MoodSortMath.GroupCount; i++)
                {
                    if (counts[i] == 0) continue;
                    var g = (MoodGroup)i;
                    string name = MoodCompact.ShortName(g, third), total = MoodView.Number(totals[i]);
                    string text = g == MoodGroup.Pass && lastGone > 0
                        ? "CC_MoodC_ChipDays".Translate(name, total, TextUtil.DaysString(lastGone)).Resolve()
                        : "CC_MoodC_Chip".Translate(name, total).Resolve();
                    line.parts.Add((text, MoodCompact.ColorOf(g)));
                }
                if (line.parts.Count == 0) return null;
                line.plain = string.Join("  ·  ", line.parts.Select(x => x.text));
                line.tip = "CC_MoodC_NeedsTip".Translate(p.LabelShort).Resolve();
                return line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("needs tab mood summary", e);
                return null;
            }
        }

        /// <summary>Draws the line in <paramref name="r"/>, each part in its colour, cut at the end if it can't fit; a click opens the Mood tab.</summary>
        public static void Draw(Rect r, Line line, Pawn p)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                Widgets.DrawHighlightIfMouseover(r);
                if (line.width < 0f) line.width = Text.CalcSize(line.plain).x;
                Text.Anchor = TextAnchor.MiddleLeft;
                float x = r.x + 2f, right = r.xMax - 2f;
                if (line.width > right - x)
                {
                    // Too narrow for every part in its colour: the whole line, plain and cut.
                    GUI.color = MoodView.Faint;
                    Widgets.Label(new Rect(x, r.y, right - x, r.height), UiKit.Fit(line.plain, right - x));
                }
                else
                {
                    for (int i = 0; i < line.parts.Count; i++)
                    {
                        if (i > 0)
                        {
                            GUI.color = UiKit.Dim;
                            float sw = Text.CalcSize("  ·  ").x;
                            Widgets.Label(new Rect(x, r.y, sw + 2f, r.height), "  ·  ");
                            x += sw;
                        }
                        var (text, color) = line.parts[i];
                        float tw = Text.CalcSize(text).x;
                        GUI.color = color;
                        Widgets.Label(new Rect(x, r.y, tw + 2f, r.height), text);
                        x += tw;
                    }
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(r, line.tip);
                if (Widgets.ButtonInvisible(r)) MoodView.ShowMoodTab(p, false);
            }
            finally
            {
                GUI.color = Color.white;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
            }
        }
    }
}
