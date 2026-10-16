using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The World page's "What they remember": a card for each people that remembers the colony for something, with how
    /// it stands (grateful, owed, wary, holding a grudge) and the deeds it remembers best, newest first among equals.
    /// Hover a card for everything it remembers. Nothing is drawn with the colony's memory switched off.
    /// </summary>
    public static class FactionMemoryPanel
    {
        private const float Pad = 10f;
        private const int Shown = 4;
        private const int MaxPeoples = 8;
        private static readonly Color Accent = new Color(0.8f, 0.72f, 0.92f);

        private class Line
        {
            public string text, when;
            public DeedSide side;
            public bool settled;
            public float h;
        }

        private class Card
        {
            public Faction faction;
            public string tone, tip, more;
            public Color toneColor;
            public List<Line> lines = new List<Line>();
            public float width = -1f, height;
        }

        private static List<Card> cards = new List<Card>();
        private static float builtAt = -1f;
        private static int builtVersion = -1;

        /// <summary>Draws the section at <paramref name="y"/> (inside the World page's scroll view); returns the y below it.</summary>
        public static float Draw(float y, float w)
        {
            if (!FactionMemory.On) return y;
            if (builtVersion != FactionMemory.Version || Time.realtimeSinceStartup - builtAt > 2f)
            {
                try
                {
                    Build();
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("faction memory panel", e);
                    cards = new List<Card>();
                }
                builtAt = Time.realtimeSinceStartup;
                builtVersion = FactionMemory.Version;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Memory_Header".Translate(), Icons.Chronicle, Accent);
            if (cards.Count == 0) return y + UiKit.EmptyState(0f, y, w, Icons.Chronicle, "CC_Memory_Nothing".Translate(), 60f) + 10f;
            y += 4f;
            int cols = w >= 640f ? 2 : 1;
            float cw = (w - (cols - 1) * 8f) / cols;
            for (int i = 0; i < cards.Count; i += cols)
            {
                float rowH = 0f;
                for (int c = 0; c < cols && i + c < cards.Count; c++) rowH = Mathf.Max(rowH, Measure(cards[i + c], cw));
                for (int c = 0; c < cols && i + c < cards.Count; c++) DrawCard(new Rect(c * (cw + 8f), y, cw, rowH), cards[i + c]);
                y += rowH + 8f;
            }
            return y + 6f;
        }

        private static void Build()
        {
            var mem = FactionMemory.Instance;
            cards = new List<Card>();
            if (mem == null) return;
            int now = Find.TickManager.TicksGame;
            foreach (var f in mem.Peoples().Take(MaxPeoples))
            {
                var all = mem.Of(f).Where(d => d.StrengthAt(now) >= 1.5f).OrderByDescending(d => Mathf.Round(d.StrengthAt(now) / 3f)).ThenByDescending(d => d.tick).ToList();
                if (all.Count == 0) continue;
                int tone = mem.Tone(f);
                var card = new Card
                {
                    faction = f,
                    tone = ("CC_Memory_Tone_" + tone).Translate(),
                    toneColor = ToneColor(tone),
                    more = all.Count > Shown ? "CC_Memory_More".Translate(all.Count - Shown).Resolve() : null,
                    tip = Tip(f, all, now)
                };
                foreach (var d in all.Take(Shown)) card.lines.Add(new Line { text = LineText(d), when = FactionMemory.AgoText(d.tick), side = d.side, settled = d.settled });
                cards.Add(card);
            }
        }

        /// <summary>"The colony set Ana free." / "They raided the colony twice."</summary>
        public static string LineText(FactionDeed d)
        {
            string line = (d.Theirs ? "CC_Memory_Line_Theirs" : "CC_Memory_Line_Ours").Translate(FactionMemory.Text(d).Named("DEED")).Resolve().CapitalizeFirst();
            // The colonist they remember for it (World/KinMemory.cs).
            string by = KinMemory.ByLine(d);
            return by.NullOrEmpty() ? line : line + " " + by;
        }

        private static string Tip(Faction f, List<FactionDeed> all, int now)
        {
            var parts = new List<string> { "CC_Memory_TipHead".Translate(f.Name).Resolve() };
            foreach (var side in new[] { DeedSide.Favor, DeedSide.TheirFavor, DeedSide.Wrong, DeedSide.TheirWrong })
            {
                var some = all.Where(d => d.side == side).ToList();
                if (some.Count == 0) continue;
                parts.Add("");
                parts.Add(("CC_Memory_Side_" + side).Translate().Resolve());
                foreach (var d in some)
                    parts.Add("  - " + FactionMemory.Text(d) + " (" + FactionMemory.AgoText(d.tick) + (d.settled ? ", " + "CC_Memory_Settled".Translate().Resolve() : "") + ")");
            }
            parts.Add("");
            parts.Add("CC_Memory_TipFoot".Translate().Resolve());
            return string.Join("\n", parts);
        }

        public static Color ToneColor(int tone)
        {
            switch (tone)
            {
                case 1:
                case 2: return UiKit.Good;
                case 3: return UiKit.Warn;
                case 4: return UiKit.Bad;
                case 5: return UiKit.Gold;
                case 6: return UiKit.Info;
                default: return UiKit.Faint;
            }
        }

        public static Color SideColor(DeedSide s)
        {
            switch (s)
            {
                case DeedSide.Favor: return UiKit.Good;
                case DeedSide.Wrong: return UiKit.Bad;
                case DeedSide.TheirFavor: return UiKit.Gold;
                default: return UiKit.Warn;
            }
        }

        private static Texture2D SideIcon(DeedSide s)
        {
            switch (s)
            {
                case DeedSide.Favor: return Icons.Heart;
                case DeedSide.Wrong: return Icons.Vendetta;
                case DeedSide.TheirFavor: return Icons.Promise;
                default: return Icons.Raid;
            }
        }

        private const float WhenW = 92f;

        private static float Measure(Card c, float w)
        {
            if (Mathf.Abs(c.width - w) < 0.5f) return c.height;
            c.width = w;
            Text.Font = GameFont.Small;
            float textW = w - Pad * 2f - 20f - WhenW;
            float h = 6f + 24f + 2f;
            foreach (var l in c.lines)
            {
                l.h = Mathf.Max(20f, Text.CalcHeight(l.text, textW));
                h += l.h + 2f;
            }
            if (c.more != null) h += 18f;
            c.height = h + 6f;
            return c.height;
        }

        private static void DrawCard(Rect r, Card c)
        {
            UiKit.Card(r, UiKit.WithAlpha(c.toneColor, 0.7f), false, true);
            float x = r.x + Pad, w = r.width - Pad * 2f, cy = r.y + 6f;
            float chipW = UiKit.FactionChip(x, cy, c.faction, 22f, Mathf.Min(240f, w * 0.6f));
            UiKit.LabelFit(new Rect(x + chipW + 8f, cy, w - chipW - 8f, 22f), c.tone, c.toneColor, GameFont.Small, TextAnchor.MiddleRight);
            cy += 26f;
            float textW = w - 20f - WhenW;
            foreach (var l in c.lines)
            {
                var col = l.settled ? UiKit.WithAlpha(SideColor(l.side), 0.45f) : SideColor(l.side);
                UiKit.Icon(new Rect(x, cy + 3f, 14f, 14f), SideIcon(l.side), col);
                GUI.color = l.settled ? UiKit.Faint : Color.white;
                Widgets.Label(new Rect(x + 20f, cy, textW, l.h), l.text);
                GUI.color = Color.white;
                UiKit.LabelFit(new Rect(x + w - WhenW, cy, WhenW, 20f), l.when, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                cy += l.h + 2f;
            }
            if (c.more != null) UiKit.LabelFit(new Rect(x + 20f, cy, w - 20f, 18f), c.more, UiKit.Faint, GameFont.Tiny);
            if (!c.tip.NullOrEmpty())
            {
                string tip = c.tip;
                TooltipHandler.TipRegion(r, () => tip, c.faction.loadID * 31 + 7);
            }
        }
    }
}
