using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The World page's "Missions" section: each mission offered or under way as a card with its terms, a bar for each
    /// thing to deliver, how it stands, and the time left. Click a card to open its quest (or look at what it's about).
    /// Nothing is drawn when the colony has no missions.
    /// </summary>
    public static class MissionPanel
    {
        private const float Pad = 10f;

        private class Card
        {
            public Mission m;
            public string status;
            public float statusH, height, width = -1f;
        }

        private static List<Card> cards = new List<Card>();
        private static float builtAt = -1f;

        private static List<Card> Cards()
        {
            if (Time.realtimeSinceStartup - builtAt < 1f) return cards;
            builtAt = Time.realtimeSinceStartup;
            int now = Find.TickManager.TicksGame;
            cards = new List<Card>();
            foreach (var m in Missions.All.Where(m => m.Open || now - m.ended < GenDate.TicksPerDay).OrderBy(m => m.Open ? 0 : 1).ThenBy(m => m.offered))
            {
                string status = null;
                // Built every second while the page is open: a mission whose state can't be read shows without it, logged once.
                try
                {
                    switch (m.state)
                    {
                        case MissionState.Offered:
                        {
                            var q = Missions.QuestOf(m);
                            int left = q != null && q.acceptanceExpireTick > 0 ? q.acceptanceExpireTick - now : 0;
                            status = q != null ? "CC_Missions_Offered".Translate(TextUtil.DaysString(Mathf.Max(0, left))).ToString() : "CC_Missions_OfferedLetter".Translate().ToString();
                            break;
                        }
                        case MissionState.Active:
                            status = Missions.Worker(m)?.Status(m) ?? "CC_Missions_Working".Translate(Missions.GoodsText(m)).ToString();
                            break;
                        case MissionState.Done:
                            status = "CC_Missions_Done".Translate();
                            break;
                        default:
                            status = "CC_Missions_Failed".Translate();
                            break;
                    }
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("mission card " + m.kind, e);
                }
                cards.Add(new Card { m = m, status = status });
            }
            return cards;
        }

        /// <summary>Draws the section at <paramref name="y"/> (inside the World page's scroll view); returns the y below it.</summary>
        public static float Draw(float y, float w)
        {
            var list = Cards();
            if (list.Count == 0) return y;
            y += 14f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Missions_Header".Translate(), Icons.Promise, UiKit.Gold);
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
            Text.Font = GameFont.Small;
            c.statusH = c.status.NullOrEmpty() ? 0f : Text.CalcHeight(c.status, w - Pad * 2f);
            int meters = c.m.state == MissionState.Active ? c.m.goals.Count + (c.m.deadline > 0 ? 1 : 0) : 0;
            c.height = 8f + 24f + (c.statusH > 0f ? c.statusH + 4f : 0f) + meters * (UiKit.MeterHeight + 2f) + 6f;
            return c.height;
        }

        private static void DrawCard(Rect r, Card c)
        {
            var m = c.m;
            var color = m.state == MissionState.Failed || m.state == MissionState.Declined ? UiKit.Bad : m.state == MissionState.Done ? UiKit.Good : UiKit.Gold;
            UiKit.Card(r, UiKit.WithAlpha(color, 0.7f), false, true);
            float x = r.x + Pad, w = r.width - Pad * 2f, cy = r.y + 6f;
            float chipW = 0f;
            if (m.faction != null)
            {
                float maxChip = Mathf.Min(220f, w * 0.45f);
                chipW = UiKit.FactionChipWidth(m.faction, 22f, maxChip);
                UiKit.FactionChip(r.xMax - Pad - chipW, cy, m.faction, 22f, maxChip);
                chipW += 8f;
            }
            UiKit.LabelFit(new Rect(x, cy, w - chipW, 22f), m.title ?? m.def?.LabelCap ?? "?", color);
            cy += 24f;
            if (c.statusH > 0f)
            {
                Widgets.Label(new Rect(x, cy, w, c.statusH), c.status);
                cy += c.statusH + 4f;
            }
            if (m.state == MissionState.Active)
            {
                foreach (var g in m.goals)
                {
                    float fill = g.count > 0 ? Mathf.Clamp01(g.have / (float)g.count) : 1f;
                    UiKit.Meter(new Rect(x, cy, w, UiKit.MeterHeight), fill, fill >= 1f ? UiKit.Good : UiKit.Info, Missions.GoalLabel(g), g.have + " / " + g.count);
                    cy += UiKit.MeterHeight + 2f;
                }
                if (m.deadline > 0)
                {
                    int total = Mathf.Max(1, m.deadline - Mathf.Max(0, m.started));
                    UiKit.Countdown(new Rect(x, cy, w, UiKit.MeterHeight), m.deadline - Find.TickManager.TicksGame, total, "CC_Missions_TimeLeftLabel".Translate());
                }
            }
            TooltipHandler.TipRegion(r, () => Tip(m), m.id * 7919 + 17);
            if (Widgets.ButtonInvisible(r)) Click(m);
        }

        private static string Tip(Mission m)
        {
            var parts = new List<string>();
            string goods = Missions.GoodsText(m);
            if (!goods.NullOrEmpty()) parts.Add("CC_Missions_TipGoods".Translate(goods));
            if (m.reward > 0) parts.Add("CC_Missions_TipReward".Translate(m.reward));
            parts.Add(Missions.QuestOf(m) != null ? "CC_Missions_TipQuest".Translate().ToString() : "CC_Missions_TipLook".Translate().ToString());
            return string.Join("\n", parts);
        }

        private static void Click(Mission m)
        {
            if (Missions.QuestOf(m) != null)
            {
                Missions.ShowQuest(m);
                return;
            }
            var look = Missions.Worker(m)?.Targets(m);
            if (look != null && look.IsValid) CameraJumper.TryJumpAndSelect(look.TryGetPrimaryTarget());
        }
    }
}
