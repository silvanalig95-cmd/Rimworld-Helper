using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → Moods: every mood in the colony added up. What weighs on the colony and what lifts it, who has it and
    /// what to do about it, who is closest to breaking, and how much comes from the base game, Rimfolk and each mod.
    /// Everything is worked out about once a second, not every frame.
    /// </summary>
    public static class MoodsPanel
    {
        private static Vector2 scroll;
        private static float height = 800f;

        private class Report
        {
            public List<PawnMood> pawns;
            public List<ColonyMoodLine> bad, good;
            public string summary, sources, badHeader, goodHeader, average;
            public Color averageColor;
            public List<PawnMood> shaky;
        }

        private static Report report;
        private static float reportAt = -1f;

        /// <summary>Forget the last game's colonists (called when a game loads).</summary>
        public static void Reset()
        {
            report = null;
            scroll = Vector2.zero;
        }

        private static Report Build()
        {
            var r = new Report { pawns = MoodInsight.Colonists().Select(p => MoodInsight.MoodOf(p)).ToList() };
            var colony = MoodInsight.Colony(r.pawns);
            foreach (var c in colony) c.texts = MoodView.TextsFor(c);
            r.bad = colony.Where(c => !c.good && c.total < -0.05f).OrderBy(c => c.total).Take(20).ToList();
            r.good = colony.Where(c => c.good && c.total > 0.05f).OrderByDescending(c => c.total).Take(12).ToList();
            r.badHeader = "CC_Moods_Weighing".Translate() + "  (" + MoodView.Number(colony.Where(c => !c.good).Sum(c => c.total)) + ")";
            r.goodHeader = "CC_Moods_Lifting".Translate() + "  (" + MoodView.Number(colony.Where(c => c.good).Sum(c => c.total)) + ")";
            r.shaky = r.pawns.OrderBy(m => m.now - m.minor).Take(6).ToList();
            if (r.pawns.Count > 0)
            {
                int near = r.pawns.Count(m => !m.pawn.InMentalState && m.now < m.minor + 0.05f);
                int inBreak = r.pawns.Count(m => m.pawn.InMentalState);
                float avg = r.pawns.Average(m => m.now);
                r.average = "CC_UI_Stat_AverageMood".Translate(avg.ToStringPercent()).Resolve();
                r.averageColor = near + inBreak > 0 ? UiKit.Warn : UiKit.Good;
                r.summary = "CC_Moods_Summary".Translate(avg.ToStringPercent(), near, r.pawns.Count).Resolve();
                if (inBreak > 0) r.summary += " " + "CC_Moods_InBreak".Translate(inBreak).Resolve();
                string sources = MoodView.Sources(r.pawns.SelectMany(m => m.lines), r.pawns.Count);
                if (sources != null) r.sources = sources + " " + "CC_Moods_PerColonist".Translate();
            }
            return r;
        }

        public static void Draw(Rect rect)
        {
            if (!ColonyCouncilMod.Settings.moodInsight)
            {
                UiKit.EmptyState(rect, Icons.Moods, "CC_Moods_Off".Translate());
                return;
            }
            if (report == null || Time.realtimeSinceStartup - reportAt > 1f)
            {
                report = Build();
                reportAt = Time.realtimeSinceStartup;
            }
            var r = report;
            float top = UiKit.PageHeader(rect, Icons.Moods, "CC_Moods_Header".Translate(), "CC_UI_Sub_Moods".Translate(), r.average, r.averageColor);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                if (r.pawns.Count == 0)
                {
                    UiKit.EmptyState(0f, y, w, Icons.Moods, "CC_Moods_NoColonists".Translate(), 140f);
                    return;
                }
                y += UiKit.Paragraph(0f, y, w, r.summary, Color.white) + 2f;
                y += UiKit.Paragraph(0f, y, w, r.sources, MoodView.Faint) + 2f;
                y += UiKit.Paragraph(0f, y, w, "CC_Moods_Intro".Translate(), MoodView.Faint) + 10f;

                // Who's closest to breaking.
                y += UiKit.SectionTitle(0f, y, w, "CC_Moods_Closest".Translate(), Icons.Warning, Color.white);
                for (int i = 0; i < r.shaky.Count; i++)
                {
                    var m = r.shaky[i];
                    var row = new Rect(0f, y, w, 34f);
                    UiKit.Zebra(row, i);
                    Widgets.DrawHighlightIfMouseover(row);
                    UiKit.Portrait(new Rect(4f, y + 3f, 28f, 28f), m.pawn, StateColor(m));
                    UiKit.LabelFit(new Rect(40f, y + 2f, 130f, 30f), m.pawn.LabelShort, Color.white);
                    MoodView.DrawBar(new Rect(176f, y + 10f, 160f, 14f), m);
                    UiKit.LabelFit(new Rect(346f, y + 2f, Mathf.Max(0f, w - 350f), 30f), m.worst, MoodView.Faint, GameFont.Tiny);
                    var pm = m;
                    TooltipHandler.TipRegion(row, () => pm.state + "\n\n" + "CC_Moods_ClickPerson".Translate(), Gen.HashCombineInt(m.pawn.thingIDNumber, 0x300D));
                    if (Widgets.ButtonInvisible(row)) MoodView.ShowMoodTab(m.pawn);
                    y += 36f;
                }
                y += 12f;

                y += UiKit.SectionTitle(0f, y, w, r.badHeader, Icons.TrendDown, MoodView.BadColor);
                if (r.bad.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
                y = MoodView.DrawList(r.bad, y, w, scroll.y, outRect.height, c => c.texts, c => c.total, c => () => MoodInsight.Description(c.sample));
                y += 12f;
                y += UiKit.SectionTitle(0f, y, w, r.goodHeader, Icons.TrendUp, MoodView.GoodColor);
                if (r.good.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
                y = MoodView.DrawList(r.good, y, w, scroll.y, outRect.height, c => c.texts, c => c.total, c => () => MoodInsight.Description(c.sample));
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

        /// <summary>Red when breaking or below the major break line, orange below the minor one, gray otherwise.</summary>
        private static Color StateColor(PawnMood m) =>
            m.pawn.InMentalState || m.now < m.major ? MoodView.BadColor : m.now < m.minor ? UiKit.Warn : UiKit.Neutral;
    }
}
