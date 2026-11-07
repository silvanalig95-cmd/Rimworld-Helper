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
        // Colour by meaning only (UI/UiKit.cs's tokens): a graph and a tile take the colour of where they are heading
        // (warm rising, sour falling), the fights coming sour; their icons and words say which is which.
        private static readonly Color Threat = UiKit.Sour;
        private static Vector2 scroll;
        private static float height = 700f;

        private class Tile
        {
            public string value, label, trend, tip;
            public Texture2D icon;
            public Color color;
            /// <summary>+1 better than a week ago, -1 worse, 0 about the same or no week-old sample.</summary>
            public int dir;
            /// <summary>Its last days, oldest first, for the line of days (none for a number the history doesn't keep).</summary>
            public List<float> days;
        }

        /// <summary>Everything the page shows, worked out about once a second rather than every frame.</summary>
        private class Model
        {
            public string title, day;
            public List<Tile> tiles;
            public List<DaySample> days;
            public float maxWealth, maxPeople;
            /// <summary>Each graph's colour: where its last week is heading.</summary>
            public Color moodColor, trustColor, wealthColor, peopleColor;
            public List<string> needs, coming, character, lately, weather, land;
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
                Chart(new Rect(0f, y, gw, gh), m.days, h => h.mood * 100f, 0f, 100f, m.moodColor, "CC_Overview_Mood".Translate(), v => v.ToString("0") + "%", Icons.Moods);
                Chart(new Rect(gw + 12f, y, gw, gh), m.days, h => h.trust, -100f, 100f, m.trustColor, "CC_Overview_Trust".Translate(), v => v.ToString("+0;-0;0"), Icons.Trust);
                y += gh + 12f;
                Chart(new Rect(0f, y, gw, gh), m.days, h => h.wealth, 0f, m.maxWealth, m.wealthColor, "CC_Overview_Wealth".Translate(), v => v.ToString("#,0"), Icons.Trader);
                Chart(new Rect(gw + 12f, y, gw, gh), m.days, h => h.colonists, 0f, m.maxPeople, m.peopleColor, "CC_Overview_People".Translate(), v => v.ToString("0"), Icons.People);
                y += gh + UiKit.SectionGap;

                // One line from each of the other pages, side by side.
                float colY = y;
                float left = Column(0f, colY, gw, m.needs, "CC_Overview_Needs".Translate(), "Priorities", Icons.Priorities, UiKit.Faint);
                left = Column(0f, left + 8f, gw, m.coming, "CC_Overview_Coming".Translate(), "Map/Threats/List", Icons.World, m.coming.Count > 0 ? Threat : UiKit.Faint);
                if (m.weather.Count > 0) left = Column(0f, left + 8f, gw, m.weather, "CC_Overview_Weather".Translate(), "Chronicle", Icons.World, UiKit.Faint);
                // The herd's leader, the beast the colony watches, the neighbours settled for good (Animals/HerdStory.cs, ResidentBeast.cs, Camps/Holding.cs).
                if (m.land.Count > 0) left = Column(0f, left + 8f, gw, m.land, "CC_Overview_Land".Translate(), "HereAndAway", Icons.Visitor, UiKit.Faint);
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
                GUI.color = UiKit.Ink;
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
            m.days = comp.history.Skip(Mathf.Max(0, comp.history.Count - 60)).ToList();
            m.tiles = Tiles(comp, pawns, week, m.days);
            m.moodColor = Heading(m.days, h => h.mood * 100f, 2f);
            m.trustColor = Heading(m.days, h => h.trust, 3f);
            m.wealthColor = Heading(m.days, h => h.wealth, Mathf.Max(100f, (m.days.Count > 0 ? m.days[m.days.Count - 1].wealth : 0f) * 0.03f));
            m.peopleColor = Heading(m.days, h => h.colonists, 0.5f);
            m.maxWealth = m.days.Count > 0 ? Mathf.Max(1000f, m.days.Max(h => h.wealth)) * 1.1f : 1000f;
            m.maxPeople = m.days.Count > 0 ? Mathf.Max(4f, m.days.Max(h => h.colonists) + 1f) : 4f;
            m.needs = Needs(map);
            m.coming = Coming(comp);
            m.character = Character(comp, map);
            m.lately = Lately(comp);
            m.weather = WeatherBook.Of(map)?.OverviewLines() ?? new List<string>();
            m.land = Land(comp, map);
            return m;
        }

        // ---------------- The numbers ----------------

        /// <summary>A graph's colour by where its last week went (warm up, sour down, faint about the same).</summary>
        private static Color Heading(List<DaySample> days, Func<DaySample, float> value, float threshold)
        {
            if (days.Count < 2) return UiKit.Faint;
            int from = Mathf.Max(0, days.Count - 8);
            float d = value(days[days.Count - 1]) - value(days[from]);
            return UiKit.DaysColor(d >= threshold ? 1 : d <= -threshold ? -1 : 0);
        }

        /// <summary>The last eight samples (a week and today) of one number, oldest first, for a tile's line of days.</summary>
        private static List<float> Last(List<DaySample> days, Func<DaySample, float> value)
        {
            var list = new List<float>();
            for (int i = Mathf.Max(0, days.Count - 8); i < days.Count; i++) list.Add(value(days[i]));
            return list;
        }

        private static List<Tile> Tiles(CouncilGameComponent comp, List<Pawn> pawns, DaySample week, List<DaySample> days)
        {
            float mood = pawns.Count > 0 ? pawns.Average(p => p.needs.mood.CurLevelPercentage) : 0f;
            float trust = pawns.Count > 0 ? pawns.Average(p => comp.standings.TrustOf(p)) : 0f;
            float wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);
            int wishes = comp.OpenWishes.Count(comp.LeadsPetition);
            var tiles = new List<Tile>
            {
                Make(pawns.Count.ToString(), "CC_Overview_TilePeople".Translate(), "CC_Overview_TilePeople_Tip".Translate(), Icons.People, UiKit.Faint,
                    pawns.Count, week?.colonists, 0.5f, v => v.ToString("+0;-0"), Last(days, h => h.colonists)),
                Make(mood.ToStringPercent(), "CC_Overview_TileMood".Translate(), "CC_Overview_TileMood_Tip".Translate(), Icons.Moods, UiKit.Faint,
                    mood * 100f, week != null ? week.mood * 100f : (float?)null, 2f, v => v.ToString("+0;-0") + "%", Last(days, h => h.mood * 100f)),
                Make(("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate().CapitalizeFirst(), "CC_Overview_TileTrust".Translate(),
                    "CC_Overview_TileTrust_Tip".Translate(trust.ToString("0")), Icons.Trust, UiKit.TrustColor(trust), trust, week?.trust, 3f, v => v.ToString("+0;-0"),
                    Last(days, h => h.trust)),
                Make(wishes.ToString(), "CC_Overview_TileWishes".Translate(), "CC_Overview_TileWishes_Tip".Translate(), Icons.Wishes, UiKit.Gold, 0f, null, 0f, null,
                    Last(days, h => h.wishes)),
                Make(wealth.ToString("#,0"), "CC_Overview_TileWealth".Translate(), WealthTip(), Icons.Trader, UiKit.Faint,
                    wealth, week?.wealth, Mathf.Max(100f, wealth * 0.03f), v => v.ToString("+#,0;-#,0"), Last(days, h => h.wealth))
            };
            return tiles;
        }

        /// <summary>The wealth tile's words: the colony's wealth, all its homes together; with more than one colony, each one's too.</summary>
        private static string WealthTip()
        {
            string tip = "CC_Overview_TileWealth_Tip".Translate();
            if (!ColonyState.Several) return tip;
            var each = Homes.All.Select(m => "CC_Tell_At".Translate(Homes.Name(m).Named("PLACE"), m.wealthWatcher.WealthTotal.ToString("#,0").Named("TEXT")).Resolve());
            return tip + "\n\n" + string.Join("\n", each);
        }

        private static Tile Make(string value, string label, string tip, Texture2D icon, Color color, float now, float? then, float threshold, Func<float, string> format,
            List<float> days)
        {
            var t = new Tile { value = value, label = label, tip = tip, icon = icon, color = color, days = days };
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
            const float gap = UiKit.CardGap;
            const float th = 80f;
            float tw = (w - gap * (tiles.Count - 1)) / tiles.Count;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                var r = new Rect(i * (tw + gap), y, tw, th);
                UiKit.Card(r);
                // Icon and value on one line (the tile's one title); the icon gives way when the tile is narrow.
                float vx = r.x + UiKit.CardInset;
                if (tw >= 120f && t.icon != null)
                {
                    UiKit.Icon(new Rect(r.x + UiKit.CardInset, r.y + 12f, 22f, 22f), t.icon, t.color);
                    vx += 30f;
                }
                float vw = r.xMax - 8f - vx;
                Text.Font = GameFont.Medium;
                var font = Text.CalcSize(t.value).x <= vw ? GameFont.Medium : GameFont.Small;
                UiKit.LabelFit(new Rect(vx, r.y + 6f, vw, 32f), t.value, UiKit.Ink, font);
                UiKit.LabelFit(new Rect(r.x + UiKit.CardInset, r.y + 37f, r.width - 18f, 18f), t.label, UiKit.Faint, GameFont.Tiny);
                // The line of its last week, area filled and today marked, in the colour of where it went; the change in words beside it.
                var lr = new Rect(r.x + UiKit.CardInset, r.y + 56f, r.width - 2f * UiKit.CardInset, 18f);
                var c = t.trend == null ? UiKit.Faint : UiKit.DaysColor(t.dir);
                float lineW = t.days != null && t.days.Count >= 2 ? Mathf.Min(lr.width * 0.45f, 64f) : 0f;
                if (lineW > 0f) UiKit.DaysLine(new Rect(lr.x, lr.y + 2f, lineW, lr.height - 4f), t.days, t.days.Count, Min(t.days), Max(t.days), c);
                if (t.trend != null) UiKit.LabelFit(new Rect(lr.x + lineW + (lineW > 0f ? 6f : 0f), lr.y, lr.width - lineW - 6f, lr.height), t.trend, c, GameFont.Tiny);
                TooltipHandler.TipRegion(r, t.tip);
            }
            Text.Font = GameFont.Small;
            return y + th;
        }

        // A line's floor and top, with a little room so a flat week sits in the middle.
        private static float Min(List<float> v)
        {
            float m = v[0], x = v[0];
            foreach (float f in v)
            {
                m = Mathf.Min(m, f);
                x = Mathf.Max(x, f);
            }
            return x - m < 0.0001f ? m - 1f : m;
        }

        private static float Max(List<float> v)
        {
            float m = v[0], x = v[0];
            foreach (float f in v)
            {
                m = Mathf.Min(m, f);
                x = Mathf.Max(x, f);
            }
            return x - m < 0.0001f ? x + 1f : x;
        }

        // ---------------- Graphs ----------------

        private static void Chart(Rect r, List<DaySample> days, Func<DaySample, float> value, float min, float max, Color color, string label,
            Func<float, string> format, Texture2D icon)
        {
            UiKit.Card(r, null, false, false);
            var inner = r.ContractedBy(UiKit.CardInset);
            float lx = inner.x;
            if (icon != null)
            {
                UiKit.Icon(new Rect(inner.x, inner.y, UiKit.IconDense, UiKit.IconDense), icon, UiKit.Faint);
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
            for (int q = 1; q < 4; q++) Widgets.DrawBoxSolid(new Rect(plot.x, plot.y + plot.height * q / 4f, plot.width, 1f), UiKit.WithAlpha(UiKit.Ink, 0.04f));
            if (min < 0f && max > 0f)
            {
                float zy = plot.yMax - (0f - min) / (max - min) * plot.height;
                Widgets.DrawLineHorizontal(plot.x, zy, plot.width, UiKit.WithAlpha(UiKit.Ink, 0.15f));
            }
            Vector2 Point(int i) =>
                new Vector2(plot.x + i / (float)(days.Count - 1) * plot.width,
                    plot.yMax - Mathf.Clamp01((value(days[i]) - min) / (max - min)) * plot.height);
            // A faint fill under the line, then the line.
            var fill = UiKit.WithAlpha(color, 0.16f);
            float step = plot.width / (days.Count - 1);
            float floor = min < 0f && max > 0f ? plot.yMax - (0f - min) / (max - min) * plot.height : plot.yMax;
            for (int i = 0; i < days.Count; i++)
            {
                var p = Point(i);
                float x0 = Mathf.Max(plot.x, p.x - step / 2f), x1 = Mathf.Min(plot.xMax, p.x + step / 2f);
                float ya = Mathf.Min(p.y, floor), yb = Mathf.Max(p.y, floor);
                if (x1 > x0 && yb > ya) Widgets.DrawBoxSolid(new Rect(x0, ya, x1 - x0, yb - ya), fill);
            }
            for (int i = 1; i < days.Count; i++) Widgets.DrawLine(Point(i - 1), Point(i), color, 1.4f);
            var last = Point(days.Count - 1);
            UiKit.DiscFill(new Rect(last.x - 2.5f, last.y - 2.5f, 5f, 5f), color);
            // The latest value, and the value under the mouse.
            UiKit.LabelFit(new Rect(inner.x + inner.width * 0.7f, inner.y - 2f, inner.width * 0.3f, 18f), format(value(days[days.Count - 1])), color, GameFont.Tiny,
                TextAnchor.MiddleRight);
            if (Mouse.IsOver(plot))
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - plot.x) / plot.width * (days.Count - 1)), 0, days.Count - 1);
                var p = Point(i);
                Widgets.DrawLine(new Vector2(p.x, plot.y), new Vector2(p.x, plot.yMax), UiKit.WithAlpha(UiKit.Ink, 0.25f), 1f);
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
                    list.Add(PaneLinks.Mark("CC_Overview_Fight".Translate(r.faction.Name, TextUtil.DaysString(Mathf.Max(0, r.tick - now))).ToString(), r.faction));
            int letters = Find.LetterStack.LettersListForReading.Count(l => l is ChoiceLetter_Happening h && !h.resolved);
            if (letters > 0) list.Add("CC_Overview_Letters".Translate(letters).ToString());
            if (comp.happenings.beast != null && comp.happenings.beast.Spawned) list.Add("CC_Overview_BeastWatched".Translate(comp.happenings.beastName ?? comp.happenings.beast.LabelShort).ToString());
            return list;
        }

        private static List<string> Character(CouncilGameComponent comp, Map map)
        {
            if (!ColonyCouncilMod.Settings.colonyCharacter) return new List<string>();
            // The colony's one name in its card's first line (Character/ColonyName.cs): the colony in view's own.
            string known = ColonyName.Main(ColonyState.Several && Homes.IsHome(map) ? map : null);
            return new List<string> { known ?? "CC_Ways_NoName".Translate().ToString() };
        }

        /// <summary>The herd's leader and its line, the beast the colony watches, a shiny rumour, the neighbours settled for good.</summary>
        private static List<string> Land(CouncilGameComponent comp, Map map)
        {
            var list = new List<string>();
            int now = Find.TickManager.TicksGame;
            try
            {
                var herd = comp.HerdOf(map);
                var leader = herd?.leader;
                if (leader != null && !leader.Dead && AnimalFacts.Ours(leader))
                {
                    int head = AnimalFacts.HerdCount(map);
                    list.Add(herd.generation > 1 && herd.lineName != null
                        ? "CC_Overview_HerdLeaderLine".Translate(leader.Named("PET"), HerdStory.Ordinal(herd.generation).Named("ORD"), herd.lineName.Named("LINE"), head.Named("COUNT")).Resolve()
                        : "CC_Overview_HerdLeader".Translate(leader.Named("PET"), head.Named("COUNT")).Resolve());
                    if (herd.lastSeasonTick >= 0) list.Add("CC_Overview_HerdOld".Translate(leader.Named("PET")).Resolve());
                }
                var rb = comp.BeastOf(map);
                if (rb != null && rb.Here)
                {
                    string stanceKey = "CC_Beast_Stance_" + rb.stance;
                    string stance = (stanceKey.CanTranslate() ? stanceKey : "CC_Beast_Stance_watched").Translate();
                    list.Add("CC_Overview_BeastWatched".Translate(rb.animal.Named("PET"), AnimalFacts.KindLabel(rb.animal).Named("KIND"), stance.Named("STANCE"),
                        TextUtil.DaysString(Mathf.Max(0, now - rb.namedTick)).Named("DAYS")).Resolve());
                }
                if (rb != null && rb.rumourKind != null && rb.rumourUntil > now)
                {
                    var species = DefDatabase<ThingDef>.GetNamedSilentFail(rb.rumourKind);
                    if (species != null) list.Add("CC_Overview_Rumour".Translate(species.label.Named("SPECIES"), TextUtil.DaysString(rb.rumourUntil - now).Named("DAYS")).Resolve());
                }
                var holding = CampTracker.HoldingAt(map);
                if (holding != null && holding.Here.Count > 0) list.Add(holding.HoldingLine());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("overview: the land", e);
            }
            return list;
        }

        private static List<string> Lately(CouncilGameComponent comp) =>
            Chronicle.Lines.Take(4).Select(e => "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1).ToString() + ": " + ChroniclePanel.TextOf(e, comp)).ToList();

        /// <summary>A small card of lines under a section heading, the names in them links; clicking it opens the page it summarizes.</summary>
        private static float Column(float x, float y, float w, List<string> lines, string header, string tab, Texture2D icon, Color headerColor)
        {
            float h = 32f;
            string nothing = lines.Count > 0 ? null : "CC_Overview_Nothing".Translate().ToString();
            float textW = w - 24f;
            if (nothing != null) h += LinkText.Height(nothing, textW, GameFont.Tiny) + 4f;
            for (int i = 0; i < lines.Count; i++) h += LinkText.Height(lines[i], textW, GameFont.Tiny) + 4f;
            h += 6f;
            var r = new Rect(x, y, w, h);
            UiKit.Card(r);
            UiKit.HeadingLine(x + 8f, y + 4f, w - 16f, header, lines.Count > 0 ? lines.Count : -1, null, icon, headerColor, false, false);
            float ly = y + 32f;
            if (nothing != null) LinkText.Draw(new Rect(x + 12f, ly, textW, 40f), nothing, UiKit.Faint, GameFont.Tiny);
            for (int i = 0; i < lines.Count; i++)
            {
                float lh = LinkText.Height(lines[i], textW, GameFont.Tiny);
                // A small dot before each line, in the heading's colour.
                UiKit.DiscFill(new Rect(x + 8f, ly + 5f, 4f, 4f), headerColor == UiKit.Faint ? UiKit.Dim : headerColor);
                LinkText.Draw(new Rect(x + 16f, ly, textW, lh), lines[i], UiKit.Ink, GameFont.Tiny);
                ly += lh + 4f;
            }
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, "CC_Overview_Open".Translate(MainTabWindow_Council.TabName(tab)));
            if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenTab(tab);
            return y + h;
        }
    }
}
