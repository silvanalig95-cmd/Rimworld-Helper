using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The top of the council's World page: "In the world", one card per running world situation (its name, the people
    /// it's about, what it means for the colony, what the colony has done, and how long it may last).
    /// </summary>
    public static class WorldSituationsPanel
    {
        private static readonly Color Accent = new Color(0.86f, 0.74f, 0.5f);
        private const float IconX = 10f;
        private const float TextX = 46f;

        private class Card
        {
            public WorldSituation s;
            public string title, summary, part, tip;
            public float width = -1f, summaryH, partH, height;
        }

        private static List<Card> cards = new List<Card>();
        private static float builtAt = -1f;

        /// <summary>Draws the block at <paramref name="y"/>; returns the y below it.</summary>
        public static float Draw(float x, float y, float w)
        {
            var inst = WorldSituations.Instance;
            if (inst == null || !ColonyCouncilMod.Settings.worldSituations) return y;
            if (Time.realtimeSinceStartup - builtAt > 1f)
            {
                Build(inst);
                builtAt = Time.realtimeSinceStartup;
            }
            y += UiKit.SectionTitle(x, y, w, "CC_WS_InTheWorld".Translate(), Icons.WorldEvent, Accent);
            if (cards.Count == 0)
            {
                y += UiKit.EmptyState(x, y, w, Icons.WorldEvent, "CC_WS_Quiet".Translate(), 56f);
                return y + 14f;
            }
            y += 4f;
            foreach (var c in cards)
            {
                float h = Measure(c, w);
                DrawCard(new Rect(x, y, w, h), c);
                y += h + 8f;
            }
            return y + 8f;
        }

        private static void Build(WorldSituations inst)
        {
            var old = cards;
            cards = new List<Card>();
            foreach (var s in inst.Running.OrderBy(s => s.startTick))
            {
                var c = old.FirstOrDefault(o => o.s == s) ?? new Card { s = s };
                c.title = s.Label;
                c.summary = Summary(s);
                c.part = s.deeds.Count > 0 ? "CC_WS_ColonyPart".Translate(TextUtil.JoinAnd(s.deeds.Distinct().ToList())).Resolve() : null;
                c.tip = Tip(s);
                c.width = -1f;
                cards.Add(c);
            }
        }

        /// <summary>The same line for as long as nothing changes (picked with a seed, so it doesn't flicker).</summary>
        public static string Summary(WorldSituation s)
        {
            Rand.PushState(Gen.HashCombineInt(s.id, s.flags.Count * 31 + s.help));
            try
            {
                return s.Worker?.Summary(s);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world situation summary", e);
                return null;
            }
            finally
            {
                Rand.PopState();
            }
        }

        /// <summary>What the situation makes likelier or rarer, in the game's names for them.</summary>
        private static string Tip(WorldSituation s)
        {
            var up = new List<string>();
            var down = new List<string>();
            bool hooks = RimfolkPatches.On;
            foreach (var m in s.def.modifiers)
            {
                if (!m.Applies(s) || Mathf.Approximately(m.factor, 1f)) continue;
                string name = null;
                if (!m.incident.NullOrEmpty())
                {
                    if (!hooks) continue;
                    name = DefDatabase<IncidentDef>.GetNamedSilentFail(m.incident)?.label;
                }
                else if (!m.rimfolk.NullOrEmpty()) name = DefDatabase<IncidentDef>.GetNamedSilentFail(m.rimfolk)?.label;
                else if (m.Group.HasValue) name = ("CC_WS_Group_" + m.Group.Value).Translate();
                if (name.NullOrEmpty()) continue;
                (m.factor > 1f ? up : down).Add(name);
            }
            var lines = new List<string>();
            if (up.Count > 0) lines.Add("CC_WS_Likelier".Translate(TextUtil.JoinAnd(up.Distinct().ToList())).Resolve());
            if (down.Count > 0) lines.Add("CC_WS_Rarer".Translate(TextUtil.JoinAnd(down.Distinct().ToList())).Resolve());
            if (s.ctx.faction != null) lines.Add("CC_WS_About".Translate(s.ctx.faction.Name).Resolve());
            return lines.Count == 0 ? null : string.Join("\n\n", lines);
        }

        private static float Measure(Card c, float w)
        {
            if (Mathf.Abs(c.width - w) < 0.5f) return c.height;
            c.width = w;
            Text.Font = GameFont.Small;
            float tw = w - TextX - 10f;
            c.summaryH = c.summary.NullOrEmpty() ? 0f : Text.CalcHeight(c.summary, tw);
            Text.Font = GameFont.Tiny;
            c.partH = c.part.NullOrEmpty() ? 0f : Text.CalcHeight(c.part, tw);
            Text.Font = GameFont.Small;
            c.height = 8f + 24f + (c.summaryH > 0f ? c.summaryH + 4f : 0f) + (c.partH > 0f ? c.partH + 4f : 0f) + UiKit.MeterHeight + 8f;
            return c.height;
        }

        private static void DrawCard(Rect r, Card c)
        {
            var s = c.s;
            UiKit.Card(r, UiKit.WithAlpha(Accent, 0.8f), false, !c.tip.NullOrEmpty());
            UiKit.Badge(new Rect(r.x + IconX, r.y + 8f, 28f, 28f), Icons.WorldEvent, Accent);
            float x = r.x + TextX, w = r.width - TextX - 10f;
            float cy = r.y + 6f;
            float chipW = 0f;
            if (s.ctx.faction != null && !s.ctx.faction.defeated)
            {
                float maxChip = Mathf.Min(220f, w * 0.45f);
                chipW = UiKit.FactionChipWidth(s.ctx.faction, 22f, maxChip);
                UiKit.FactionChip(r.xMax - 8f - chipW, cy, s.ctx.faction, 22f, maxChip);
                chipW += 8f;
            }
            UiKit.LabelFit(new Rect(x, cy, w - chipW, 24f), c.title, UiKit.HeaderColor);
            cy += 26f;
            if (c.summaryH > 0f)
            {
                Widgets.Label(new Rect(x, cy, w, c.summaryH), c.summary);
                cy += c.summaryH + 4f;
            }
            if (c.partH > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(x, cy, w, c.partH), c.part);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                cy += c.partH + 4f;
            }
            UiKit.Countdown(new Rect(x, cy, w, UiKit.MeterHeight), s.TicksLeft, s.Duration, "CC_WS_MayLast".Translate());
            if (!c.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, c.tip);
        }
    }
}
