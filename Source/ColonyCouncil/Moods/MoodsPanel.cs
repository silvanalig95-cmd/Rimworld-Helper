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
    /// </summary>
    public static class MoodsPanel
    {
        private static Vector2 scroll;
        private static float height = 800f;

        private class Report
        {
            public List<Pawn> pawns;
            public Dictionary<Pawn, List<MoodLine>> perPawn = new Dictionary<Pawn, List<MoodLine>>();
            public List<ColonyMoodLine> colony;
            public float average;
            public int nearBreak;
            public int inBreak;
        }

        private static Report report;
        private static float reportAt = -1f;

        private static Report Build()
        {
            var r = new Report { pawns = MoodInsight.Colonists() };
            r.colony = MoodInsight.Colony(r.pawns, r.perPawn);
            if (r.pawns.Count > 0) r.average = r.pawns.Average(p => p.needs.mood.CurLevelPercentage);
            foreach (var p in r.pawns)
            {
                var lv = MoodInsight.Levels(p);
                if (p.InMentalState) r.inBreak++;
                else if (lv.now < lv.minor + 0.05f) r.nearBreak++;
            }
            return r;
        }

        public static void Draw(Rect rect)
        {
            if (!ColonyCouncilMod.Settings.moodInsight)
            {
                Widgets.Label(rect, "CC_Moods_Off".Translate());
                return;
            }
            if (report == null || Time.realtimeSinceStartup - reportAt > 1f)
            {
                report = Build();
                reportAt = Time.realtimeSinceStartup;
            }
            var r = report;
            var view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            float y = 0f;
            float w = view.width;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, w, 32f), "CC_Moods_Header".Translate());
            Text.Font = GameFont.Small;
            y += 34f;
            if (r.pawns.Count == 0)
            {
                Widgets.Label(new Rect(0f, y, w, 24f), "CC_Moods_NoColonists".Translate());
                Widgets.EndScrollView();
                return;
            }
            string summary = "CC_Moods_Summary".Translate(r.average.ToStringPercent(), r.nearBreak, r.pawns.Count).Resolve();
            if (r.inBreak > 0) summary += " " + "CC_Moods_InBreak".Translate(r.inBreak).Resolve();
            y += Label(0f, y, w, summary, Color.white) + 2f;
            string sources = MoodView.Sources(r.perPawn.Values.SelectMany(l => l), r.pawns.Count);
            if (sources != null) y += Label(0f, y, w, sources + " " + "CC_Moods_PerColonist".Translate(), MoodView.Faint) + 2f;
            y += Label(0f, y, w, "CC_Moods_Intro".Translate(), MoodView.Faint) + 10f;

            // Who's closest to breaking.
            var shaky = r.pawns.Select(p => (p, lv: MoodInsight.Levels(p))).OrderBy(x => x.lv.now - x.lv.minor).Take(6).ToList();
            y = Header(y, w, "CC_Moods_Closest".Translate(), Color.white);
            foreach (var (p, lv) in shaky)
            {
                var row = new Rect(0f, y, w, 30f);
                Widgets.DrawLightHighlight(row);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.ThingIcon(new Rect(4f, y + 3f, 24f, 24f), p);
                Widgets.Label(new Rect(34f, y + 5f, 130f, 24f), p.LabelShort);
                MoodView.DrawBar(new Rect(170f, y + 8f, 160f, 14f), p);
                var worst = r.perPawn.TryGetValue(p, out var pl) ? pl.Where(l => l.mood < 0f).Take(3).Select(l => l.label + " " + MoodView.Number(l.mood)).ToList() : new List<string>();
                GUI.color = MoodView.Faint;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(340f, y + 7f, w - 344f, 22f), worst.Count > 0 ? string.Join(",  ", worst) : "CC_Mood_Nothing".Translate().ToString());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                TooltipHandler.TipRegion(row, MoodView.State(p) + "\n\n" + "CC_Moods_ClickPerson".Translate());
                if (Widgets.ButtonInvisible(row)) MoodView.ShowMoodTab(p);
                y += 32f;
            }
            y += 10f;

            var bad = r.colony.Where(c => c.total < -0.05f).OrderBy(c => c.total).ToList();
            var good = r.colony.Where(c => c.total > 0.05f).OrderByDescending(c => c.total).ToList();
            y = Header(y, w, "CC_Moods_Weighing".Translate() + "  (" + MoodView.Number(bad.Sum(c => c.total)) + ")", MoodView.BadColor);
            if (bad.Count == 0) y += Label(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
            foreach (var c in bad.Take(20)) y += MoodView.DrawColonyLine(0f, y, w, c) + 3f;
            y += 10f;
            y = Header(y, w, "CC_Moods_Lifting".Translate() + "  (" + MoodView.Number(good.Sum(c => c.total)) + ")", MoodView.GoodColor);
            if (good.Count == 0) y += Label(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
            foreach (var c in good.Take(12)) y += MoodView.DrawColonyLine(0f, y, w, c) + 3f;

            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float Header(float y, float w, string text, Color color)
        {
            Text.Font = GameFont.Medium;
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, w, 30f), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return y + 32f;
        }

        private static float Label(float x, float y, float w, string text, Color color)
        {
            if (text.NullOrEmpty()) return 0f;
            float h = Text.CalcHeight(text, w);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, w, h), text);
            GUI.color = Color.white;
            return h;
        }
    }
}
