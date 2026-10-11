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
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color MoodColor = new Color(0.55f, 0.8f, 0.55f);
        private static readonly Color TrustColor = new Color(0.55f, 0.7f, 0.95f);
        private static readonly Color WealthColor = new Color(0.9f, 0.8f, 0.45f);
        private static readonly Color PeopleColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color Threat = new Color(0.9f, 0.55f, 0.45f);
        private static Vector2 scroll;
        private static float height = 700f;

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                Text.Font = GameFont.Medium;
                string name = Faction.OfPlayer?.Name;
                Widgets.Label(new Rect(0f, y, w, 32f), name.NullOrEmpty() ? "CC_Overview_Header".Translate().ToString() : "CC_Overview_HeaderNamed".Translate(name).ToString());
                Text.Font = GameFont.Small;
                y += 36f;

                var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && p.needs?.mood != null).Distinct().ToList();
                var week = comp.history.LastOrDefault(h => h.day <= GenDate.DaysPassed - 7);
                y = Tiles(comp, pawns, week, y, w) + 12f;

                // The last weeks, in four small graphs.
                var days = comp.history.Skip(Mathf.Max(0, comp.history.Count - 60)).ToList();
                float gw = (w - 12f) / 2f;
                const float gh = 110f;
                Chart(new Rect(0f, y, gw, gh), days, h => h.mood * 100f, 0f, 100f, MoodColor, "CC_Overview_Mood".Translate(), v => v.ToString("0") + "%");
                Chart(new Rect(gw + 12f, y, gw, gh), days, h => h.trust, -100f, 100f, TrustColor, "CC_Overview_Trust".Translate(), v => v.ToString("+0;-0;0"));
                y += gh + 12f;
                float maxWealth = days.Count > 0 ? Mathf.Max(1000f, days.Max(h => h.wealth)) * 1.1f : 1000f;
                float maxPeople = days.Count > 0 ? Mathf.Max(4f, days.Max(h => h.colonists) + 1f) : 4f;
                Chart(new Rect(0f, y, gw, gh), days, h => h.wealth, 0f, maxWealth, WealthColor, "CC_Overview_Wealth".Translate(), v => v.ToString("#,0"));
                Chart(new Rect(gw + 12f, y, gw, gh), days, h => h.colonists, 0f, maxPeople, PeopleColor, "CC_Overview_People".Translate(), v => v.ToString("0"));
                y += gh + 16f;

                // One line from each of the other pages, side by side.
                float colY = y;
                float left = Column(0f, colY, gw, Needs(map), "CC_Overview_Needs".Translate(), "Priorities");
                left = Column(0f, left + 8f, gw, Coming(comp), "CC_Overview_Coming".Translate(), "World");
                float right = Column(gw + 12f, colY, gw, Character(comp), "CC_Overview_KnownAs".Translate(), "Ways");
                right = Column(gw + 12f, right + 8f, gw, Lately(comp), "CC_Overview_Lately".Translate(), "Chronicle");
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

        // ---------------- The numbers ----------------

        private static float Tiles(CouncilGameComponent comp, List<Pawn> pawns, DaySample week, float y, float w)
        {
            float mood = pawns.Count > 0 ? pawns.Average(p => p.needs.mood.CurLevelPercentage) : 0f;
            float trust = pawns.Count > 0 ? pawns.Average(p => comp.standings.TrustOf(p)) : 0f;
            float wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);
            int wishes = comp.OpenWishes.Count();
            var tiles = new List<(string value, string label, string trend, string tip)>
            {
                (pawns.Count.ToString(), "CC_Overview_TilePeople".Translate(), Trend(pawns.Count, week?.colonists, 0.5f, v => v.ToString("+0;-0")), "CC_Overview_TilePeople_Tip".Translate()),
                (mood.ToStringPercent(), "CC_Overview_TileMood".Translate(), Trend(mood * 100f, week != null ? week.mood * 100f : (float?)null, 2f, v => v.ToString("+0;-0") + "%"), "CC_Overview_TileMood_Tip".Translate()),
                (("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate().CapitalizeFirst(), "CC_Overview_TileTrust".Translate(), Trend(trust, week?.trust, 3f, v => v.ToString("+0;-0")), "CC_Overview_TileTrust_Tip".Translate(trust.ToString("0"))),
                (wishes.ToString(), "CC_Overview_TileWishes".Translate(), null, "CC_Overview_TileWishes_Tip".Translate()),
                (wealth.ToString("#,0"), "CC_Overview_TileWealth".Translate(), Trend(wealth, week?.wealth, Mathf.Max(100f, wealth * 0.03f), v => v.ToString("+#,0;-#,0")), "CC_Overview_TileWealth_Tip".Translate())
            };
            float tw = (w - 8f * (tiles.Count - 1)) / tiles.Count;
            const float th = 64f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var r = new Rect(i * (tw + 8f), y, tw, th);
                Widgets.DrawMenuSection(r);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(r.x, r.y + 6f, r.width, 30f), tiles[i].value);
                Text.Font = GameFont.Tiny;
                GUI.color = Faint;
                string sub = tiles[i].label + (tiles[i].trend != null ? "  " + tiles[i].trend : "");
                Widgets.Label(new Rect(r.x + 2f, r.y + 38f, r.width - 4f, 22f), sub);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(r, tiles[i].tip);
            }
            return y + th;
        }

        /// <summary>"▲ +3 this week", or nothing when it hardly moved or there's no week-old sample yet.</summary>
        private static string Trend(float now, float? then, float threshold, Func<float, string> format)
        {
            if (then == null) return null;
            float d = now - then.Value;
            if (Mathf.Abs(d) < threshold) return null;
            return "CC_Overview_Trend".Translate(format(d)).ToString();
        }

        // ---------------- Graphs ----------------

        private static void Chart(Rect r, List<DaySample> days, Func<DaySample, float> value, float min, float max, Color color, string label, Func<float, string> format)
        {
            Widgets.DrawMenuSection(r);
            var inner = r.ContractedBy(8f);
            Text.Font = GameFont.Tiny;
            GUI.color = Faint;
            Widgets.Label(new Rect(inner.x, inner.y - 2f, inner.width, 18f), label);
            GUI.color = Color.white;
            var plot = new Rect(inner.x, inner.y + 16f, inner.width, inner.height - 16f);
            if (days.Count < 2)
            {
                GUI.color = Faint;
                Widgets.Label(plot, "CC_Overview_NotYet".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }
            // Zero line for the trust graph (it runs from bitter to devoted).
            if (min < 0f && max > 0f)
            {
                float zy = plot.yMax - (0f - min) / (max - min) * plot.height;
                Widgets.DrawLineHorizontal(plot.x, zy, plot.width, new Color(1f, 1f, 1f, 0.15f));
            }
            Vector2 Point(int i) =>
                new Vector2(plot.x + i / (float)(days.Count - 1) * plot.width,
                    plot.yMax - Mathf.Clamp01((value(days[i]) - min) / (max - min)) * plot.height);
            for (int i = 1; i < days.Count; i++) Widgets.DrawLine(Point(i - 1), Point(i), color, 2f);
            // The latest value, and the value under the mouse.
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = color;
            Widgets.Label(new Rect(inner.x, inner.y - 2f, inner.width, 18f), format(value(days[days.Count - 1])));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(plot))
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - plot.x) / plot.width * (days.Count - 1)), 0, days.Count - 1);
                var p = Point(i);
                Widgets.DrawLine(new Vector2(p.x, plot.y), new Vector2(p.x, plot.yMax), new Color(1f, 1f, 1f, 0.25f), 1f);
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

        /// <summary>A small titled box of lines; clicking it opens the page it summarises.</summary>
        private static float Column(float x, float y, float w, List<string> lines, string header, string tab)
        {
            Text.Font = GameFont.Tiny;
            float h = 24f;
            var texts = lines.Count > 0 ? lines : new List<string> { "CC_Overview_Nothing".Translate() };
            var heights = texts.Select(t => Text.CalcHeight(t, w - 16f)).ToList();
            h += heights.Sum() + 4f * texts.Count + 8f;
            var r = new Rect(x, y, w, h);
            Widgets.DrawMenuSection(r);
            Widgets.DrawHighlightIfMouseover(r);
            GUI.color = tab == "World" && lines.Count > 0 ? Threat : Faint;
            Widgets.Label(new Rect(x + 8f, y + 4f, w - 16f, 20f), header);
            GUI.color = lines.Count > 0 ? Color.white : Faint;
            float ly = y + 24f;
            for (int i = 0; i < texts.Count; i++)
            {
                Widgets.Label(new Rect(x + 8f, ly, w - 16f, heights[i]), texts[i]);
                ly += heights[i] + 4f;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, "CC_Overview_Open".Translate(MainTabWindow_Council.TabName(tab)));
            if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenTab(tab);
            return y + h;
        }
    }
}
