using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The region window's Neighbours page, its "Storylines" section under the missions: each storyline running (and each
    /// ended in the last fortnight) as a card with its parts so far, how each ended, and what is on its way or how it
    /// ended. Click a card to open the part's quest. Nothing is drawn when the colony has no storylines.
    /// </summary>
    public static class StorylinePanel
    {
        private const float Pad = 10f;

        /// <summary>An ended storyline shows this long.</summary>
        private const int ShowEndedDays = 15;

        private class Card
        {
            public Storyline s;
            public string text;
            public float textH, height, width = -1f;
        }

        private static List<Card> cards = new List<Card>();
        private static float builtAt = -1f;

        private static List<Card> Cards()
        {
            if (Time.realtimeSinceStartup - builtAt < 1f) return cards;
            builtAt = Time.realtimeSinceStartup;
            int now = Find.TickManager.TicksGame;
            cards = new List<Card>();
            foreach (var s in Storylines.All.Where(s => s.Running || now - s.endedTick < ShowEndedDays * GenDate.TicksPerDay).OrderBy(s => s.Running ? 0 : 1).ThenBy(s => s.started))
            {
                string text = null;
                // Built every second while the page is open: a storyline whose words can't be read shows without them, logged once.
                try
                {
                    text = Text(s);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("storyline card " + s.kind, e);
                }
                cards.Add(new Card { s = s, text = text });
            }
            return cards;
        }

        /// <summary>"Part 1: Keep the factor's crates. All kept, under a roof and unopened." a line per part, then what comes next or how it ended.</summary>
        public static string Text(Storyline s)
        {
            var lines = new List<string>();
            foreach (var st in s.steps)
            {
                string how = st.outcome == null
                    ? (Missions.ById(s.missionId) is Mission m && m.state == MissionState.Active ? "CC_Storyline_UnderWay" : "CC_Storyline_Offered").Translate().Resolve()
                    : Storylines.OutcomeWords(s, st) ?? "";
                lines.Add((st.title ?? st.part) + (how.NullOrEmpty() ? "" : ": " + how));
            }
            if (s.Running)
            {
                string coming = Storylines.ComingWords(s);
                if (!coming.NullOrEmpty()) lines.Add("CC_Storyline_Next".Translate(coming.Named("TEXT")).Resolve());
            }
            else
            {
                string end = Storylines.EndingLine(s);
                if (!end.NullOrEmpty()) lines.Add("CC_Storyline_Ended".Translate(end.Named("TEXT")).Resolve());
            }
            return string.Join("\n", lines);
        }

        /// <summary>Draws the section at <paramref name="y"/> (inside the Neighbours page's scroll view); returns the y below it.</summary>
        public static float Draw(float y, float w)
        {
            var list = Cards();
            if (list.Count == 0) return y;
            y += 14f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Storyline_Section".Translate(), Icons.WorldEvent, UiKit.Gold);
            foreach (var c in list)
            {
                float h = Measure(c, w);
                DrawCard(new Rect(0f, y, w, h), c);
                y += h + 8f;
            }
            return y;
        }

        private static float Measure(Card c, float w)
        {
            if (Mathf.Abs(c.width - w) < 0.5f) return c.height;
            c.width = w;
            Verse.Text.Font = GameFont.Small;
            c.textH = c.text.NullOrEmpty() ? 0f : Verse.Text.CalcHeight(c.text, w - Pad * 2f);
            c.height = 8f + 24f + (c.textH > 0f ? c.textH + 4f : 0f) + 6f;
            return c.height;
        }

        private static void DrawCard(Rect r, Card c)
        {
            var s = c.s;
            var def = Storylines.Def(s.kind);
            var e = s.Running ? null : def?.Ending(s.ending);
            var color = e == null ? UiKit.Gold : e.tone == "good" ? UiKit.Good : e.tone == "bad" ? UiKit.Bad : UiKit.Info;
            UiKit.Card(r, UiKit.WithAlpha(color, 0.7f), false, true);
            float x = r.x + Pad, w = r.width - Pad * 2f, cy = r.y + 6f;
            float chipW = 0f;
            if (s.giver != null)
            {
                float maxChip = Mathf.Min(220f, w * 0.45f);
                chipW = UiKit.FactionChipWidth(s.giver, 22f, maxChip);
                UiKit.FactionChip(r.xMax - Pad - chipW, cy, s.giver, 22f, maxChip);
                chipW += 8f;
            }
            UiKit.LabelFit(new Rect(x, cy, w - chipW, 22f), Storylines.Label(s), color);
            cy += 24f;
            if (c.textH > 0f) Widgets.Label(new Rect(x, cy, w, c.textH), c.text);
            if (Missions.ById(s.missionId) is Mission m && Missions.QuestOf(m) != null)
            {
                TooltipHandler.TipRegion(r, "CC_Missions_TipQuest".Translate());
                if (Widgets.ButtonInvisible(r)) Missions.ShowQuest(m);
            }
        }
    }
}
