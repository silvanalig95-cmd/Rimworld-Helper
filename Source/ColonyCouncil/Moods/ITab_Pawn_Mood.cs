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
        private Vector2 scroll;
        private float height = 400f;
        private Pawn cachedPawn;
        private float cachedAt = -1f;
        private List<MoodLine> lines;

        public ITab_Pawn_Mood()
        {
            size = new Vector2(500f, 560f);
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

        protected override void FillTab()
        {
            var p = SelPawn;
            if (p?.needs?.mood == null) return;
            if (cachedPawn != p || lines == null || Time.realtimeSinceStartup - cachedAt > 0.5f)
            {
                lines = MoodInsight.LinesFor(p);
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), "CC_Mood_Title".Translate(p.LabelShortCap).Resolve());
            Text.Font = GameFont.Small;
            float y = rect.y + 32f;
            MoodView.DrawBar(new Rect(rect.x, y, rect.width, 18f), p);
            y += 24f;
            string state = MoodView.State(p);
            float sh = Text.CalcHeight(state, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, sh), state);
            y += sh + 2f;
            string sources = MoodView.Sources(lines, 1);
            if (sources != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = MoodView.Faint;
                float h = Text.CalcHeight(sources, rect.width);
                Widgets.Label(new Rect(rect.x, y, rect.width, h), sources);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += h + 4f;
            }

            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float cy = 0f;
            cy = Section(cy, view.width, "CC_Mood_Weighing".Translate(p.LabelShort).Resolve(), lines.Where(l => l.mood < 0f).ToList(), MoodView.BadColor);
            cy = Section(cy + 6f, view.width, "CC_Mood_Lifting".Translate(p.LabelShort).Resolve(), lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).ToList(), MoodView.GoodColor);
            height = Mathf.Max(cy + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float Section(float y, float w, string header, List<MoodLine> list, Color color)
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
            foreach (var l in list) y += MoodView.DrawLine(0f, y, w, l) + 3f;
            return y;
        }
    }
}
