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
    /// the inspect pane (RimfolkTab); the name and the bar stay put, the rest scrolls, and the heart, weighing and
    /// lifting sections fold away.
    /// </summary>
    public class ITab_Pawn_Mood : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn cachedPawn;
        private float cachedAt = -1f;
        private PawnMood mood;
        private List<MoodLine> bad, good;
        /// <summary>What getting used to good times, good moods adding up slowly and standards take off right now (GoodTimes.Readout).</summary>
        private string goodTimes;
        private string title, weighing, lifting, badTotal, goodTotal;
        /// <summary>Which of the tab's sections are folded away this session.</summary>
        private static readonly Folds folds = new Folds();

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
            goodTimes = null;
        }

        protected override void FillTab()
        {
            var p = SelPawn;
            if (p?.needs?.mood == null) return;
            if (cachedPawn != p || mood == null || Time.realtimeSinceStartup - cachedAt > 0.5f)
            {
                if (cachedPawn != p) scroll = Vector2.zero;
                mood = MoodInsight.MoodOf(p);
                foreach (var l in mood.lines) l.texts = MoodView.TextsFor(l);
                bad = mood.lines.Where(l => l.mood < 0f).ToList();
                good = mood.lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).ToList();
                goodTimes = GoodTimes.Readout(p);
                // The headings and their totals, with the model rather than every frame.
                title = "CC_Mood_Title".Translate(p.LabelShortCap).Resolve();
                weighing = "CC_Mood_Weighing".Translate(p.LabelShort).Resolve();
                lifting = "CC_Mood_Lifting".Translate(p.LabelShort).Resolve();
                badTotal = MoodView.Number(bad.Sum(l => l.mood));
                goodTotal = MoodView.Number(good.Sum(l => l.mood));
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            UiKit.Icon(new Rect(rect.x, rect.y + 4f, 22f, 22f), Icons.Moods, MoodView.ColorFor(mood.now - mood.minor));
            // Clear of the tab's close button in the top right corner.
            UiKit.LabelFit(new Rect(rect.x + 30f, rect.y, rect.width - 30f - 14f, 30f), title, Color.white, GameFont.Medium);
            Text.Font = GameFont.Small;
            float y = rect.y + 32f;
            MoodView.DrawBar(new Rect(rect.x, y, rect.width, 18f), mood);
            y += 24f;

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
