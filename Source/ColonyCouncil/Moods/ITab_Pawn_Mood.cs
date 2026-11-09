using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Mood" in a colonist's inspect pane: how close they are to breaking, every mood weighing on them or lifting
    /// them, why it's there, when it fades, which mod it comes from, and what can be done about it. Fits the room above
    /// the inspect pane (RimfolkTab); the name and the bar stay put, the rest scrolls.
    /// <para>
    /// At a glance first (the player, 9 November 2026: "so the most relevant details for strain and mood are visible at
    /// first glance, with additional details below that"; MoodGlance): the heart as two meters under the mood bar with the
    /// day off's button beside them (Heart/HeartMeter.cs), their mood and where it's heading, what weighs on them and lifts
    /// them most, what the bands are doing to them, and the top advice with its buttons. Then, in order of use, sections that fold away and are remembered between sessions: every
    /// mood (compact: chips with each group's total, then the moods grouped by what the player can do, one line each, a
    /// click opening a line's whole card; details: every mood as a card; the switch in the header is remembered in the
    /// settings), the heart (strain and the good side, what built and eased it, what would help), its feelings, its needs,
    /// what has grown ordinary and where their moods come from, and what happened lately.
    /// </para>
    /// </summary>
    public class ITab_Pawn_Mood : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn cachedPawn;
        private float cachedAt = -1f;
        private bool cachedCompact;
        private PawnMood mood;
        private List<MoodLine> bad, good;
        private CompactModel compact;
        private MoodGlance glance;
        /// <summary>What good things grown ordinary take off right now (GoodLife.Readout).</summary>
        private string goodTimes;
        private string title, weighing, lifting, badTotal, goodTotal, allTotal, nothingBad, allMoods, ordinary, lately;
        /// <summary>Which of the details view's lists are folded away this session (the cards).</summary>
        private static readonly Folds folds = new Folds();
        /// <summary>The tab's sections under the summary, remembered between sessions: the heart's feelings and needs, what's grown ordinary and lately start folded.</summary>
        private static readonly Folds sections = Folds.Remembered("mood.sections", "feelings", "needs", "ordinary", "lately");
        /// <summary>The compact view's folds, open card and groups' places.</summary>
        private static readonly CompactState state = new CompactState(false);

        public ITab_Pawn_Mood()
        {
            size = new Vector2(500f, 540f);
            labelKey = "CC_Tab_Mood";
        }

        protected override float PreferredHeight => 540f;

        public override bool IsVisible
        {
            get
            {
                var p = SelPawn;
                return ColonyCouncilMod.Settings.moodInsight && p?.needs?.mood?.thoughts != null && p.RaceProps.Humanlike
                       && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony);
            }
        }

        public override void Notify_ClearingAllMapsMemory()
        {
            base.Notify_ClearingAllMapsMemory();
            cachedPawn = null;
            mood = null;
            bad = good = null;
            compact = null;
            glance = null;
            goodTimes = null;
            state.Forget();
        }

        protected override void FillTab()
        {
            var p = SelPawn;
            if (p?.needs?.mood == null) return;
            bool isCompact = !MoodCompact.Details;
            if (cachedPawn != p || mood == null || isCompact != cachedCompact || Time.realtimeSinceStartup - cachedAt > 0.5f || Time.realtimeSinceStartup < cachedAt)
            {
                if (cachedPawn != p || isCompact != cachedCompact)
                {
                    scroll = Vector2.zero;
                    state.Forget();
                }
                Rebuild(p, isCompact);
            }
            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            UiKit.Icon(new Rect(rect.x, rect.y + 4f, 22f, 22f), Icons.Moods, MoodView.ColorFor(mood.now - mood.minor));
            // The switch and the title keep clear of the tab's close button in the top right corner.
            float toggleW = MoodCompact.DrawViewToggle(rect.xMax - 14f, rect.y + 3f);
            UiKit.LabelFit(new Rect(rect.x + 30f, rect.y, Mathf.Max(0f, rect.width - 30f - 14f - toggleW - 8f), 30f), title, Color.white, GameFont.Medium);
            Text.Font = GameFont.Small;
            float y = rect.y + 32f;
            MoodView.DrawBar(new Rect(rect.x, y, rect.width, 18f), mood);
            y += 24f;
            Fill(p, rect, y, isCompact);
        }

        private void Rebuild(Pawn p, bool isCompact)
        {
            mood = MoodInsight.MoodOf(p);
            foreach (var l in mood.lines) l.texts = MoodView.TextsFor(l);
            bad = mood.lines.Where(l => l.mood < 0f).ToList();
            good = mood.lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).ToList();
            compact = isCompact ? MoodCompact.For(p, mood.lines) : null;
            goodTimes = GoodLife.Readout(p);
            glance = MoodGlance.For(p, mood);
            // The headings and their totals, with the model rather than every frame.
            title = "CC_Mood_Title".Translate(p.LabelShortCap).Resolve();
            weighing = "CC_Mood_Weighing".Translate(p.LabelShort).Resolve();
            lifting = "CC_Mood_Lifting".Translate(p.LabelShort).Resolve();
            nothingBad = "CC_MoodC_NothingBad".Translate(p.LabelShort).Resolve();
            badTotal = MoodView.Number(bad.Sum(l => l.mood));
            goodTotal = MoodView.Number(good.Sum(l => l.mood));
            allTotal = MoodView.Number(mood.lines.Sum(l => l.mood));
            allMoods = "CC_Glance_AllMoods".Translate(p.Named("PAWN")).Resolve();
            ordinary = "CC_Glance_Ordinary".Translate().Resolve();
            lately = "CC_Glance_Lately".Translate().Resolve();
            cachedPawn = p;
            cachedCompact = isCompact;
            cachedAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Everything under the bar scrolls, so a tab squeezed above a tall inspect pane loses nothing: the summary first (in
        /// sight without scrolling), then the sections in order of use.
        /// </summary>
        private void Fill(Pawn p, Rect rect, float y, bool isCompact)
        {
            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            float to = isCompact ? MoodCompact.TakeScroll(state) : -1f;
            if (to >= 0f) scroll.y = Mathf.Min(to, Mathf.Max(0f, height - outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float cy = glance != null ? glance.Draw(0f, 0f, w) : 0f;
                UiKit.Divider(0f, cy, w);
                cy += 6f;

                // Every mood: compact rows by what can be done, or every mood as a card.
                cy += UiKit.FoldTitle(0f, cy, w, allMoods, sections, "moods", out bool open, Icons.Moods, UiKit.HeaderColor, allTotal);
                if (open)
                {
                    if (isCompact)
                    {
                        cy += MoodCompact.DrawChips(0f, cy, w, compact, state) + 8f;
                        cy = MoodCompact.DrawGroups(compact, state, cy, w, scroll.y, outRect.height, nothingBad);
                    }
                    else
                    {
                        cy = Section(cy, w, outRect.height, "weighing", weighing, bad, MoodView.BadColor, badTotal);
                        cy = Section(cy + 6f, w, outRect.height, "lifting", lifting, good, MoodView.GoodColor, goodTotal);
                    }
                    cy += 4f;
                }

                // The heart (Heart/): strain and the good side with what would help, its feelings, its needs.
                // Under the summary, the heart's section adds only what the summary leaves out.
                cy = HeartView.DrawHeart(p, cy, w, sections, glance != null);
                cy = HeartView.DrawFeelings(p, cy, w, sections);
                cy = HeartView.DrawNeeds(p, cy, w, sections);

                // What good times take off, and where their moods come from.
                if (goodTimes != null || mood.sources != null)
                {
                    cy += UiKit.FoldTitle(0f, cy, w, ordinary, sections, "ordinary", out bool openOrdinary, null, UiKit.HeaderColor);
                    if (openOrdinary)
                    {
                        if (goodTimes != null)
                        {
                            float h = UiKit.Paragraph(0f, cy, w, goodTimes, MoodView.Faint, GameFont.Tiny);
                            TooltipHandler.TipRegion(new Rect(0f, cy, w, h), () => GoodLife.Tip(p), 0x600D7135);
                            cy += h + 4f;
                        }
                        if (mood.sources != null) cy += UiKit.Paragraph(0f, cy, w, mood.sources, MoodView.Faint, GameFont.Tiny) + 4f;
                    }
                    cy += 4f;
                }

                // What happened lately: strain since the day began, the last day off, bad afternoon, release, break.
                if (glance?.lately != null)
                {
                    cy += UiKit.FoldTitle(0f, cy, w, lately, sections, "lately", out bool openLately, null, UiKit.HeaderColor);
                    if (openLately) cy += UiKit.Paragraph(0f, cy, w, glance.lately, MoodView.Faint, GameFont.Tiny) + 4f;
                }
                height = Mathf.Max(cy + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>A heading with the section's total on the right (it folds away), then its lines.</summary>
        private float Section(float y, float w, float viewHeight, string key, string header, List<MoodLine> list, Color color, string total)
        {
            y += UiKit.FoldTitle(0f, y, w, header, folds, key, out bool open, null, color, total);
            if (!open) return y;
            if (list.Count == 0)
            {
                GUI.color = MoodView.Faint;
                Widgets.Label(new Rect(0f, y, w, 24f), "CC_Mood_Nothing".Translate());
                GUI.color = Color.white;
                return y + 26f;
            }
            return MoodView.DrawList(list, y, w, scroll.y, viewHeight, l => l.texts, l => l.mood, l => () => MoodInsight.Description(l));
        }
    }
}
