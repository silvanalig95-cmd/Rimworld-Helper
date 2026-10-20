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
    /// Compact (the default): chips with each group's total, then the moods grouped by what the player can do (You can
    /// fix, Will pass, Faith and who they are, Lifting), one line each with the short action; a click opens a line's
    /// whole card. The heart section comes after the moods, folded. Details: every mood as a card, as before. The switch
    /// in the header is remembered in the settings.
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
        /// <summary>What getting used to good times, good moods adding up slowly and standards take off right now (GoodTimes.Readout).</summary>
        private string goodTimes;
        private string title, weighing, lifting, badTotal, goodTotal, nothingBad;
        /// <summary>The state sentence's height at the width it was measured for.</summary>
        private float stateH = -1f, stateFor = -1f;
        /// <summary>Which of the tab's sections are folded away this session (the cards).</summary>
        private static readonly Folds folds = new Folds();
        /// <summary>The compact view's folds, open card and groups' places.</summary>
        private static readonly CompactState state = new CompactState(true);

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
            if (isCompact) FillCompact(p, rect, y);
            else FillDetails(p, rect, y);
        }

        private void Rebuild(Pawn p, bool isCompact)
        {
            mood = MoodInsight.MoodOf(p);
            foreach (var l in mood.lines) l.texts = MoodView.TextsFor(l);
            bad = mood.lines.Where(l => l.mood < 0f).ToList();
            good = mood.lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).ToList();
            compact = isCompact ? MoodCompact.For(p, mood.lines) : null;
            goodTimes = GoodTimes.Readout(p);
            // The headings and their totals, with the model rather than every frame.
            title = "CC_Mood_Title".Translate(p.LabelShortCap).Resolve();
            weighing = "CC_Mood_Weighing".Translate(p.LabelShort).Resolve();
            lifting = "CC_Mood_Lifting".Translate(p.LabelShort).Resolve();
            nothingBad = "CC_MoodC_NothingBad".Translate(p.LabelShort).Resolve();
            badTotal = MoodView.Number(bad.Sum(l => l.mood));
            goodTotal = MoodView.Number(good.Sum(l => l.mood));
            stateFor = -1f;
            cachedPawn = p;
            cachedCompact = isCompact;
            cachedAt = Time.realtimeSinceStartup;
        }

        // ---------------- Compact ----------------

        private void FillCompact(Pawn p, Rect rect, float y)
        {
            // Where there's room, the state and the chips stay put above the moods; on a short tab the state scrolls with them.
            bool stateFixed = MoodSortMath.StateFixed(rect.yMax - y);
            if (stateFixed) y += State(rect.x, y, rect.width) + 6f;
            y += MoodCompact.DrawChips(rect.x, y, rect.width, compact, state) + 8f;

            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            float to = MoodCompact.TakeScroll(state);
            if (to >= 0f) scroll.y = Mathf.Min(to, Mathf.Max(0f, height - outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float cy = 0f;
                if (!stateFixed) cy += State(0f, cy, w) + 6f;
                cy = MoodCompact.DrawGroups(compact, state, cy, w, scroll.y, outRect.height, nothingBad);
                // Secondary: what good times take off, where it comes from, and the heart (folded at first).
                if (goodTimes != null)
                {
                    float h = UiKit.Paragraph(0f, cy, w, goodTimes, MoodView.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(new Rect(0f, cy, w, h), () => "CC_GoodTimes_Tip".Translate().Resolve(), 0x600D7135);
                    cy += h + 4f;
                }
                if (mood.sources != null) cy += UiKit.Paragraph(0f, cy, w, mood.sources, MoodView.Faint, GameFont.Tiny) + 8f;
                cy = HeartView.DrawSection(p, cy, w, state.folds);
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

        /// <summary>The one-sentence state ("Mood 42%, sinking toward 35%. ..."), measured once per refresh and width.</summary>
        private float State(float x, float y, float w)
        {
            if (Mathf.Abs(stateFor - w) > 0.5f)
            {
                Text.Font = GameFont.Small;
                stateH = Text.CalcHeight(mood.state, w);
                stateFor = w;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x, y, w, stateH), mood.state);
            return stateH;
        }

        // ---------------- Details: every mood as a card ----------------

        private void FillDetails(Pawn p, Rect rect, float y)
        {
            // Everything under the bar scrolls, so a tab squeezed above a tall inspect pane loses nothing.
            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float cy = UiKit.Paragraph(0f, 0f, w, mood.state, Color.white) + 2f;
                if (mood.sources != null) cy += UiKit.Paragraph(0f, cy, w, mood.sources, MoodView.Faint, GameFont.Tiny) + 4f;
                if (goodTimes != null)
                {
                    float h = UiKit.Paragraph(0f, cy, w, goodTimes, MoodView.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(new Rect(0f, cy, w, h), () => "CC_GoodTimes_Tip".Translate().Resolve(), 0x600D7135);
                    cy += h + 4f;
                }
                cy += 4f;
                // Strain, feelings and the heart's needs (Heart/), when that layer is on.
                cy = HeartView.DrawSection(p, cy, w, folds);
                cy = Section(cy, w, outRect.height, "weighing", weighing, bad, MoodView.BadColor, badTotal);
                cy = Section(cy + 6f, w, outRect.height, "lifting", lifting, good, MoodView.GoodColor, goodTotal);
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
