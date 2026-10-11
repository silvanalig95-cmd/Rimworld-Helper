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
            public string summary, sources, badHeader, goodHeader;
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
                r.summary = "CC_Moods_Summary".Translate(r.pawns.Average(m => m.now).ToStringPercent(), near, r.pawns.Count).Resolve();
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
            try
            {
                float y = 0f;
                float w = view.width;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, w, 32f), "CC_Moods_Header".Translate());
                Text.Font = GameFont.Small;
                y += 34f;
                if (r.pawns.Count == 0)
                {
                    Widgets.Label(new Rect(0f, y, w, 24f), "CC_Moods_NoColonists".Translate());
                    return;
                }
                y += Label(0f, y, w, r.summary, Color.white) + 2f;
                y += Label(0f, y, w, r.sources, MoodView.Faint) + 2f;
                y += Label(0f, y, w, "CC_Moods_Intro".Translate(), MoodView.Faint) + 10f;

                // Who's closest to breaking.
                y = Header(y, w, "CC_Moods_Closest".Translate(), Color.white);
                foreach (var m in r.shaky)
                {
                    var row = new Rect(0f, y, w, 30f);
                    Widgets.DrawLightHighlight(row);
                    if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    Widgets.ThingIcon(new Rect(4f, y + 3f, 24f, 24f), m.pawn);
                    Widgets.Label(new Rect(34f, y + 5f, 130f, 24f), m.pawn.LabelShort);
                    MoodView.DrawBar(new Rect(170f, y + 8f, 160f, 14f), m);
                    GUI.color = MoodView.Faint;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(340f, y + 7f, w - 344f, 22f), m.worst);
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    var pm = m;
                    TooltipHandler.TipRegion(row, () => pm.state + "\n\n" + "CC_Moods_ClickPerson".Translate(), Gen.HashCombineInt(m.pawn.thingIDNumber, 0x300D));
                    if (Widgets.ButtonInvisible(row)) MoodView.ShowMoodTab(m.pawn);
                    y += 32f;
                }
                y += 10f;

                y = Header(y, w, r.badHeader, MoodView.BadColor);
                if (r.bad.Count == 0) y += Label(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
                y = MoodView.DrawList(r.bad, y, w, scroll.y, rect.height, c => c.texts, c => c.total, c => () => MoodInsight.Description(c.sample));
                y += 10f;
                y = Header(y, w, r.goodHeader, MoodView.GoodColor);
                if (r.good.Count == 0) y += Label(0f, y, w, "CC_Mood_Nothing".Translate(), MoodView.Faint) + 4f;
                y = MoodView.DrawList(r.good, y, w, scroll.y, rect.height, c => c.texts, c => c.total, c => () => MoodInsight.Description(c.sample));
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
