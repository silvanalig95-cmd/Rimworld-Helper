using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → Overview: the colony at a glance. A few numbers with how they've moved this week, graphs of mood, trust,
    /// wealth and people over the last weeks, and the one line that matters from each of the other pages.
    /// </summary>
    public static class OverviewPanel
    {
        private static readonly Color MoodColor = new Color(0.55f, 0.8f, 0.55f);
        private static readonly Color TrustColor = new Color(0.55f, 0.7f, 0.95f);
        private static readonly Color WealthColor = new Color(0.9f, 0.8f, 0.45f);
        private static readonly Color PeopleColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color Threat = new Color(0.9f, 0.55f, 0.45f);
        private static Vector2 scroll;
        private static float height = 700f;

        private class Tile
        {
            public string value, label, trend, tip;
            public Texture2D icon;
            public Color color;
            /// <summary>+1 better than a week ago, -1 worse, 0 about the same or no week-old sample.</summary>
            public int dir;
        }

        /// <summary>Everything the page shows, worked out about once a second rather than every frame.</summary>
        private class Model
        {
            public string title, day;
            public List<Tile> tiles;
            public List<DaySample> days;
            public float maxWealth, maxPeople;
            public List<string> needs, coming, character, lately, weather;
        }

        private static Model model;
        private static float modelAt = -1f;

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (model == null || Time.realtimeSinceStartup - modelAt > 1f)
            {
                model = Build(comp, map);
                modelAt = Time.realtimeSinceStartup;
            }
            var m = model;
            float top = UiKit.PageHeader(rect, Icons.Overview, m.title, "CC_UI_Sub_Overview".Translate(), m.day);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                y = Tiles(m.tiles, y, w) + 12f;

                // The last weeks, in four small graphs.
                float gw = (w - 12f) / 2f;
                const float gh = 116f;
                Chart(new Rect(0f, y, gw, gh), m.days, h => h.mood * 100f, 0f, 100f, MoodColor, "CC_Overview_Mood".Translate(), v => v.ToString("0") + "%", Icons.Moods);
                Chart(new Rect(gw + 12f, y, gw, gh), m.days, h => h.trust, -100f, 100f, TrustColor, "CC_Overview_Trust".Translate(), v => v.ToString("+0;-0;0"), Icons.Trust);
                y += gh + 12f;
                Chart(new Rect(0f, y, gw, gh), m.days, h => h.wealth, 0f, m.maxWealth, WealthColor, "CC_Overview_Wealth".Translate(), v => v.ToString("#,0"), Icons.Trader);
                Chart(new Rect(gw + 12f, y, gw, gh), m.days, h => h.colonists, 0f, m.maxPeople, PeopleColor, "CC_Overview_People".Translate(), v => v.ToString("0"), Icons.People);
                y += gh + 16f;

                // One line from each of the other pages, side by side.
                float colY = y;
                float left = Column(0f, colY, gw, m.needs, "CC_Overview_Needs".Translate(), "Priorities", Icons.Priorities, UiKit.Faint);
                left = Column(0f, left + 8f, gw, m.coming, "CC_Overview_Coming".Translate(), "World", Icons.World, m.coming.Count > 0 ? Threat : UiKit.Faint);
                if (m.weather.Count > 0) left = Column(0f, left + 8f, gw, m.weather, "CC_Overview_Weather".Translate(), "Chronicle", Icons.World, UiKit.Faint);
                float right = Column(gw + 12f, colY, gw, m.character, "CC_Overview_KnownAs".Translate(), "Ways", Icons.Ways, UiKit.Faint);
                right = Column(gw + 12f, right + 8f, gw, m.lately, "CC_Overview_Lately".Translate(), "Chronicle", Icons.Chronicle, UiKit.Faint);
                y = Mathf.Max(left, right) + 10f;
                height = Mathf.Max(y, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static Model Build(CouncilGameComponent comp, Map map)
        {
            var m = new Model();
            string name = Faction.OfPlayer?.Name;
            m.title = name.NullOrEmpty() ? "CC_Overview_Header".Translate().ToString() : "CC_Overview_HeaderNamed".Translate(name).ToString();
            m.day = "CC_DayN".Translate(GenDate.DaysPassed + 1);
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && p.needs?.mood != null).Distinct().ToList();
            var week = comp.history.LastOrDefault(h => h.day <= GenDate.DaysPassed - 7);
            m.tiles = Tiles(comp, pawns, week);
            m.days = comp.history.Skip(Mathf.Max(0, comp.history.Count - 60)).ToList();
            m.maxWealth = m.days.Count > 0 ? Mathf.Max(1000f, m.days.Max(h => h.wealth)) * 1.1f : 1000f;
            m.maxPeople = m.days.Count > 0 ? Mathf.Max(4f, m.days.Max(h => h.colonists) + 1f) : 4f;
            m.needs = Needs(map);
            m.coming = Coming(comp);
            m.character = Character(comp);
            m.lately = Lately(comp);
            m.weather = WeatherBook.Of(map)?.OverviewLines() ?? new List<string>();
            return m;
        }

        // ---------------- The numbers ----------------

        private static List<Tile> Tiles(CouncilGameComponent comp, List<Pawn> pawns, DaySample week)
        {
            float mood = pawns.Count > 0 ? pawns.Average(p => p.needs.mood.CurLevelPercentage) : 0f;
            float trust = pawns.Count > 0 ? pawns.Average(p => comp.standings.TrustOf(p)) : 0f;
            float wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);
            int wishes = comp.OpenWishes.Count();
            var tiles = new List<Tile>
            {
                Make(pawns.Count.ToString(), "CC_Overview_TilePeople".Translate(), "CC_Overview_TilePeople_Tip".Translate(), Icons.People, PeopleColor,
                    pawns.Count, week?.colonists, 0.5f, v => v.ToString("+0;-0")),
                Make(mood.ToStringPercent(), "CC_Overview_TileMood".Translate(), "CC_Overview_TileMood_Tip".Translate(), Icons.Moods, MoodColor,
                    mood * 100f, week != null ? week.mood * 100f : (float?)null, 2f, v => v.ToString("+0;-0") + "%"),
                Make(("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate().CapitalizeFirst(), "CC_Overview_TileTrust".Translate(),
                    "CC_Overview_TileTrust_Tip".Translate(trust.ToString("0")), Icons.Trust, UiKit.TrustColor(trust), trust, week?.trust, 3f, v => v.ToString("+0;-0")),
                Make(wishes.ToString(), "CC_Overview_TileWishes".Translate(), "CC_Overview_TileWishes_Tip".Translate(), Icons.Wishes, UiKit.Gold, 0f, null, 0f, null),
                Make(wealth.ToString("#,0"), "CC_Overview_TileWealth".Translate(), "CC_Overview_TileWealth_Tip".Translate(), Icons.Trader, WealthColor,
                    wealth, week?.wealth, Mathf.Max(100f, wealth * 0.03f), v => v.ToString("+#,0;-#,0"))
            };
            return tiles;
        }

        private static Tile Make(string value, string label, string tip, Texture2D icon, Color color, float now, float? then, float threshold, Func<float, string> format)
        {
            var t = new Tile { value = value, label = label, tip = tip, icon = icon, color = color };
            if (then != null && format != null)
            {
                float d = now - then.Value;
                if (Mathf.Abs(d) >= threshold)
                {
                    t.trend = "CC_Overview_Trend".Translate(format(d)).ToString();
                    t.dir = d > 0f ? 1 : -1;
                }
            }
            return t;
        }

        private static float Tiles(List<Tile> tiles, float y, float w)
        {
            const float gap = 8f;
            const float th = 74f;
            float tw = (w - gap * (tiles.Count - 1)) / tiles.Count;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                var r = new Rect(i * (tw + gap), y, tw, th);
                UiKit.Card(r);
                Widgets.DrawBoxSolid(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 2f), UiKit.WithAlpha(t.color, 0.7f));
                // Icon and value on one line; the icon gives way when the tile is narrow.
                float vx = r.x + 10f;
                if (tw >= 120f && t.icon != null)
                {
                    UiKit.Icon(new Rect(r.x + 10f, r.y + 12f, 22f, 22f), t.icon, t.color);
                    vx += 30f;
                }
                float vw = r.xMax - 8f - vx;
                Text.Font = GameFont.Medium;
                var font = Text.CalcSize(t.value).x <= vw ? GameFont.Medium : GameFont.Small;
                UiKit.LabelFit(new Rect(vx, r.y + 6f, vw, 32f), t.value, Color.white, font);
                UiKit.LabelFit(new Rect(r.x + 10f, r.y + 37f, r.width - 18f, 18f), t.label, UiKit.Faint, GameFont.Tiny);
                if (t.trend != null)
                {
                    var c = t.dir > 0 ? UiKit.Good : UiKit.Bad;
                    UiKit.Icon(new Rect(r.x + 10f, r.y + 58f, 10f, 10f), t.dir > 0 ? Icons.TrendUp : Icons.TrendDown, c);
                    UiKit.LabelFit(new Rect(r.x + 24f, r.y + 54f, r.width - 32f, 18f), t.trend, c, GameFont.Tiny);
                }
                TooltipHandler.TipRegion(r, t.tip);
            }
            Text.Font = GameFont.Small;
            return y + th;
        }

        // ---------------- Graphs ----------------

        private static void Chart(Rect r, List<DaySample> days, Func<DaySample, float> value, float min, float max, Color color, string label,
            Func<float, string> format, Texture2D icon)
        {
            UiKit.Card(r, null, false, false);
            var inner = r.ContractedBy(8f);
            float lx = inner.x;
            if (icon != null)
            {
                UiKit.Icon(new Rect(inner.x, inner.y, 14f, 14f), icon, color);
                lx += 18f;
            }
            UiKit.LabelFit(new Rect(lx, inner.y - 2f, inner.width * 0.7f - (lx - inner.x), 18f), label, UiKit.Faint, GameFont.Tiny);
            var plot = new Rect(inner.x, inner.y + 18f, inner.width, inner.height - 18f);
            if (days.Count < 2)
            {
                UiKit.Paragraph(plot.x, plot.y + 4f, plot.width, "CC_Overview_NotYet".Translate(), UiKit.Faint, GameFont.Tiny);
                return;
            }
            // Gridlines at the quarters, and the zero line for the trust graph (it runs from bitter to devoted).
            for (int q = 1; q < 4; q++) Widgets.DrawBoxSolid(new Rect(plot.x, plot.y + plot.height * q / 4f, plot.width, 1f), new Color(1f, 1f, 1f, 0.04f));
            if (min < 0f && max > 0f)
            {
                float zy = plot.yMax - (0f - min) / (max - min) * plot.height;
                Widgets.DrawLineHorizontal(plot.x, zy, plot.width, new Color(1f, 1f, 1f, 0.15f));
            }
            Vector2 Point(int i) =>
                new Vector2(plot.x + i / (float)(days.Count - 1) * plot.width,
                    plot.yMax - Mathf.Clamp01((value(days[i]) - min) / (max - min)) * plot.height);
            // A faint fill under the line, then the line.
            var fill = UiKit.WithAlpha(color, 0.09f);
            float step = plot.width / (days.Count - 1);
            float floor = min < 0f && max > 0f ? plot.yMax - (0f - min) / (max - min) * plot.height : plot.yMax;
            for (int i = 0; i < days.Count; i++)
            {
                var p = Point(i);
                float x0 = Mathf.Max(plot.x, p.x - step / 2f), x1 = Mathf.Min(plot.xMax, p.x + step / 2f);
                float ya = Mathf.Min(p.y, floor), yb = Mathf.Max(p.y, floor);
                if (x1 > x0 && yb > ya) Widgets.DrawBoxSolid(new Rect(x0, ya, x1 - x0, yb - ya), fill);
            }
            for (int i = 1; i < days.Count; i++) Widgets.DrawLine(Point(i - 1), Point(i), color, 2f);
            var last = Point(days.Count - 1);
            Widgets.DrawBoxSolid(new Rect(last.x - 2f, last.y - 2f, 4f, 4f), color);
            // The latest value, and the value under the mouse.
            UiKit.LabelFit(new Rect(inner.x + inner.width * 0.7f, inner.y - 2f, inner.width * 0.3f, 18f), format(value(days[days.Count - 1])), color, GameFont.Tiny,
                TextAnchor.MiddleRight);
            if (Mouse.IsOver(plot))
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - plot.x) / plot.width * (days.Count - 1)), 0, days.Count - 1);
                var p = Point(i);
                Widgets.DrawLine(new Vector2(p.x, plot.y), new Vector2(p.x, plot.yMax), new Color(1f, 1f, 1f, 0.25f), 1f);
                Widgets.DrawBoxSolid(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), color);
                TooltipHandler.TipRegion(plot, "CC_DayN".Translate(days[i].day + 1) + ": " + format(value(days[i])));
            }
            Text.Font = GameFont.Small;
        }

        // ---------------- One line from each page ----------------

        private static List<string> Needs(Map map)
        {
            if (map == null || !ColonyCouncilMod.Settings.enableAdvisor) return new List<string>();
            var signals = CouncilMapComponent.For(map)?.Signals;
            return signals == null ? new List<string>() : signals.OrderByDescending(s => s.Level).Take(3).Select(s => s.title).Where(t => !t.NullOrEmpty()).ToList();
        }

        private static List<string> Coming(CouncilGameComponent comp)
        {
            var list = new List<string>();
            int now = Find.TickManager.TicksGame;
            foreach (var r in comp.happenings.raids.OrderBy(r => r.tick).Take(3))
                if (r.faction != null)
                    list.Add("CC_Overview_Fight".Translate(r.faction.Name, TextUtil.DaysString(Mathf.Max(0, r.tick - now))).ToString());
            int letters = Find.LetterStack.LettersListForReading.Count(l => l is ChoiceLetter_Happening h && !h.resolved);
            if (letters > 0) list.Add("CC_Overview_Letters".Translate(letters).ToString());
            if (comp.happenings.beast != null && comp.happenings.beast.Spawned) list.Add("CC_Overview_Beast".Translate(comp.happenings.beastName ?? comp.happenings.beast.LabelShort).ToString());
            return list;
        }

        private static List<string> Character(CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.colonyCharacter) return new List<string>();
            string known = comp.character.Describe();
            return new List<string> { known != null ? "CC_Ways_KnownAs".Translate(known).Resolve() : "CC_Ways_NoName".Translate().ToString() };
        }

        private static List<string> Lately(CouncilGameComponent comp) =>
            comp.chronicle.AsEnumerable().Reverse().Take(4).Select(e => "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1).ToString() + ": " + e.text).ToList();

        /// <summary>A small titled card of lines; clicking it opens the page it summarizes.</summary>
        private static float Column(float x, float y, float w, List<string> lines, string header, string tab, Texture2D icon, Color headerColor)
        {
            Text.Font = GameFont.Tiny;
            float h = 28f;
            string nothing = lines.Count > 0 ? null : "CC_Overview_Nothing".Translate().ToString();
            float textW = w - 24f;
            if (nothing != null) h += Text.CalcHeight(nothing, textW) + 4f;
            for (int i = 0; i < lines.Count; i++) h += Text.CalcHeight(lines[i], textW) + 4f;
            h += 6f;
            var r = new Rect(x, y, w, h);
            UiKit.Card(r);
            UiKit.Icon(new Rect(x + 8f, y + 6f, 16f, 16f), icon, headerColor);
            UiKit.LabelFit(new Rect(x + 30f, y + 3f, w - 38f, 22f), header, headerColor, GameFont.Small);
            float ly = y + 28f;
            Text.Font = GameFont.Tiny;
            if (nothing != null)
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(x + 12f, ly, textW, Text.CalcHeight(nothing, textW)), nothing);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                float lh = Text.CalcHeight(lines[i], textW);
                // A small bullet in the header's color before each line.
                Widgets.DrawBoxSolid(new Rect(x + 8f, ly + 6f, 3f, 3f), headerColor);
                GUI.color = Color.white;
                Widgets.Label(new Rect(x + 16f, ly, textW, lh), lines[i]);
                ly += lh + 4f;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, "CC_Overview_Open".Translate(MainTabWindow_Council.TabName(tab)));
            if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenTab(tab);
            return y + h;
        }
    }
}
