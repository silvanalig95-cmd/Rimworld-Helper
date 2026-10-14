using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Mood" in a colonist's inspect pane: how close they are to breaking, every mood weighing on them or lifting
    /// them, why it's there, when it fades, which mod it comes from, and what can be done about it.
    /// </summary>
    public class ITab_Pawn_Mood : ITab
    {
        private const float Height = 540f;
        private Vector2 scroll;
        private float height = 400f;
        private Pawn cachedPawn;
        private float cachedAt = -1f;
        private PawnMood mood;
        private List<MoodLine> bad, good;
        /// <summary>What getting used to good times, small joys and standards take off right now (GoodTimes.Readout).</summary>
        private string goodTimes;

        public ITab_Pawn_Mood()
        {
            size = new Vector2(500f, Height);
            labelKey = "CC_Tab_Mood";
        }

        public override bool IsVisible
        {
            get
            {
                var p = SelPawn;
                return ColonyCouncilMod.Settings.moodInsight && p?.needs?.mood?.thoughts != null && p.RaceProps.Humanlike
                       && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony);
            }
        }

        /// <summary>Fit small screens and big interface scales: never taller than the space above the inspect pane.</summary>
        protected override void UpdateSize()
        {
            base.UpdateSize();
            size.y = Mathf.Max(300f, Mathf.Min(Height, PaneTopY - 10f));
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
                mood = MoodInsight.MoodOf(p);
                foreach (var l in mood.lines) l.texts = MoodView.TextsFor(l);
                bad = mood.lines.Where(l => l.mood < 0f).ToList();
                good = mood.lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).ToList();
                goodTimes = GoodTimes.Readout(p);
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            UiKit.Icon(new Rect(rect.x, rect.y + 4f, 22f, 22f), Icons.Moods, MoodView.ColorFor(mood.now - mood.minor));
            UiKit.LabelFit(new Rect(rect.x + 30f, rect.y, rect.width - 30f, 30f), "CC_Mood_Title".Translate(p.LabelShortCap).Resolve(), Color.white, GameFont.Medium);
            Text.Font = GameFont.Small;
            float y = rect.y + 32f;
            MoodView.DrawBar(new Rect(rect.x, y, rect.width, 18f), mood);
            y += 24f;
            float sh = Text.CalcHeight(mood.state, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, sh), mood.state);
            y += sh + 2f;
            if (mood.sources != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = MoodView.Faint;
                float h = Text.CalcHeight(mood.sources, rect.width);
                Widgets.Label(new Rect(rect.x, y, rect.width, h), mood.sources);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += h + 4f;
            }
            if (goodTimes != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = MoodView.Faint;
                float h = Text.CalcHeight(goodTimes, rect.width);
                var line = new Rect(rect.x, y, rect.width, h);
                Widgets.Label(line, goodTimes);
                TooltipHandler.TipRegion(line, () => "CC_GoodTimes_Tip".Translate().Resolve(), 0x600D7135);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += h + 4f;
            }

            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                // Strain, feelings and the heart's needs (Heart/), when that layer is on.
                float cy = HeartView.DrawSection(p, 0f, view.width);
                cy = Section(cy, view.width, outRect.height, "CC_Mood_Weighing".Translate(p.LabelShort).Resolve(), bad, MoodView.BadColor);
                cy = Section(cy + 6f, view.width, outRect.height, "CC_Mood_Lifting".Translate(p.LabelShort).Resolve(), good, MoodView.GoodColor);
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

        private float Section(float y, float w, float viewHeight, string header, List<MoodLine> list, Color color)
        {
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, w, 24f), header + "  (" + MoodView.Number(list.Sum(l => l.mood)) + ")");
            GUI.color = Color.white;
            y += 26f;
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
