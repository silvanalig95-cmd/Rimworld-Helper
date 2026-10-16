using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The weather noticed: the first snow of a winter, a heat wave, a cold snap and a storm rolling in. When one begins
    /// on a home map, one or two colonists in view say something about it (the <c>Council_Pool_Say...</c> weather pools)
    /// and, now and then, someone writes it down in their journal. After the first snow, children have two days of it
    /// to play in: the game's own snowman building, more likely for them for a while (a small habit, see
    /// <see cref="SmallHabits"/>).
    /// </summary>
    /// <remarks>
    /// Run from <see cref="VisualTouches"/>' hourly look, on each home map. The first snow is snow falling in the cold
    /// after ten days without any; on the first look at a map (after a load) the weather there is only noted, so a
    /// snowy map or a heat wave already under way isn't announced again. Nothing is saved.
    /// </remarks>
    internal sealed class WeatherTalk
    {
        private const int SnowGapTicks = GenDate.TicksPerDay * 10;
        private const int SnowPlayTicks = GenDate.TicksPerDay * 2;
        private const int StormJournalGap = GenDate.TicksPerDay * 5;
        private const float SnowRate = 0.05f;

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.88f);

        private sealed class Watch
        {
            public Map map;
            public int lastSnow = int.MinValue / 2;
            public bool storm;
            public HashSet<int> conditions = new HashSet<int>();
            public int snowPlayUntil = -1;
            public int lastStormJournal = int.MinValue / 2;
        }

        private readonly List<Watch> watches = new List<Watch>();
        private int lastLook = int.MinValue / 2;

        /// <summary>Children may go out and play in the snow on this map (the first snow fell within two days).</summary>
        public static bool SnowPlay(Map map)
        {
            var self = VisualTouches.Instance?.weather;
            if (self == null || map == null) return false;
            foreach (var w in self.watches)
                if (w.map == map) return w.snowPlayUntil > Find.TickManager.TicksGame;
            return false;
        }

        /// <summary>
        /// Once an hour. <paramref name="touches"/>: the touches setting is on, so lines may be said and written; off, the
        /// first snow is only noted (for the children's snow days). True when a line was said on screen.
        /// </summary>
        public bool Hourly(VisualTouches vt, int now, bool touches)
        {
            // Not looked at for a while (both settings were off): start over, so old weather isn't taken for new.
            if (now - lastLook > GenDate.TicksPerHour * 3) watches.Clear();
            lastLook = now;
            watches.RemoveAll(w => w.map == null || w.map.Disposed || !Find.Maps.Contains(w.map));
            Map viewMap = null;
            CellRect view = CellRect.Empty;
            bool speech = touches && ColonyCouncilMod.Settings?.speechText == true && VisualTouches.InView(out viewMap, out view);
            bool spoke = false;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome || map.weatherManager == null) continue;
                try
                {
                    bool here = speech && map == viewMap;
                    spoke |= Look(vt, map, now, touches, here, view);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("weather talk", e);
                }
            }
            return spoke;
        }

        private Watch WatchOf(Map map, out bool fresh)
        {
            fresh = false;
            foreach (var w in watches)
                if (w.map == map) return w;
            fresh = true;
            var watch = new Watch { map = map };
            watches.Add(watch);
            return watch;
        }

        private bool Look(VisualTouches vt, Map map, int now, bool touches, bool here, CellRect view)
        {
            var w = WatchOf(map, out bool fresh);
            var wm = map.weatherManager;
            bool snowing = wm.SnowRate >= SnowRate && map.mapTemperature.OutdoorTemp <= 2f;
            bool storm = Flavor.IsStorm(wm.curWeather);
            var active = new HashSet<int>();
            var started = new List<GameConditionDef>();
            // The map's own conditions, and the world's (a heat wave over the whole region).
            foreach (var manager in new[] { map.gameConditionManager, Find.World?.gameConditionManager })
            {
                if (manager?.ActiveConditions == null) continue;
                foreach (var c in manager.ActiveConditions)
                {
                    if (c?.def == null || (c.def != GameConditionDefOf.HeatWave && c.def != GameConditionDefOf.ColdSnap)) continue;
                    int key = c.startTick * 31 + c.def.shortHash;
                    if (!active.Add(key)) continue;
                    if (!w.conditions.Contains(key) && !started.Contains(c.def)) started.Add(c.def);
                }
            }
            w.conditions = active;
            bool stormBegins = storm && !w.storm;
            w.storm = storm;
            if (fresh)
            {
                // The first look: only noted. A map with snow on the ground has had snow lately.
                if (snowing || AverageSnow(map) > 0.03f) w.lastSnow = now;
                return false;
            }
            bool spoke = false;
            if (snowing)
            {
                bool first = now - w.lastSnow > SnowGapTicks;
                w.lastSnow = now;
                if (first) spoke |= FirstSnow(vt, w, map, now, touches, here, view);
            }
            if (!touches) return spoke;
            foreach (var def in started)
            {
                bool heat = def == GameConditionDefOf.HeatWave;
                if (here) spoke |= Say(vt, map, view, now, heat ? "Council_Pool_SayHeatWave" : "Council_Pool_SayColdSnap", 1, true, false) > 0;
                if (Rand.Chance(0.6f)) Write(map, heat ? "Council_Journal_HeatWave" : "Council_Journal_ColdSnap", false);
            }
            if (stormBegins)
            {
                string kind = StormKind(wm.curWeather);
                if (here) spoke |= Say(vt, map, view, now, "Council_Pool_SayStorm", 1, false, false, kind) > 0;
                if (now - w.lastStormJournal > StormJournalGap && Rand.Chance(0.4f))
                {
                    w.lastStormJournal = now;
                    Write(map, "Council_Journal_Storm", false, kind);
                }
            }
            return spoke;
        }

        /// <summary>The first snow of a winter: a word or two from whoever is in view (children first), a journal line, and snow days for the children.</summary>
        private bool FirstSnow(VisualTouches vt, Watch w, Map map, int now, bool touches, bool here, CellRect view)
        {
            w.snowPlayUntil = now + SnowPlayTicks;
            if (!touches) return false;
            // The calendar writes the colony's very first snow down itself.
            var noted = CalendarTracker.Instance?.Get("snow");
            if (noted == null || now - noted.tick > GenDate.TicksPerHour * 2) Write(map, "Council_Journal_FirstSnowSeason", true);
            return here && Say(vt, map, view, now, "Council_Pool_SayFirstSnow", 2, true, true) > 0;
        }

        /// <summary>thunder, dust or other: which kind of storm it is, for the lines.</summary>
        private static string StormKind(WeatherDef w)
        {
            string n = w?.defName ?? "";
            if (n.IndexOf("Thunder", System.StringComparison.OrdinalIgnoreCase) >= 0) return "thunder";
            if (n.IndexOf("Dust", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Sand", System.StringComparison.OrdinalIgnoreCase) >= 0) return "dust";
            return "otherstorm";
        }

        private static float AverageSnow(Map map)
        {
            var grid = map.snowGrid;
            int area = map.Area;
            return grid == null || area <= 0 ? 0f : grid.TotalDepth / area;
        }

        /// <summary>Up to <paramref name="max"/> colonists in view say a line from the pool (those outside first, children first if asked).</summary>
        private static int Say(VisualTouches vt, Map map, CellRect view, int now, string pool, int max, bool outsideFirst, bool childrenFirst, params string[] tags)
        {
            var who = map.mapPawns.FreeColonistsSpawned.Where(p => vt.Free(p, map, view, now))
                .OrderByDescending(p => (childrenFirst && p.DevelopmentalStage.Child() ? 2f : 0f) + (outsideFirst && !p.Position.Roofed(map) ? 1f : 0f) + Rand.Value * 0.9f)
                .Take(max).ToList();
            int said = 0;
            foreach (var p in who)
            {
                string line = Flavor.FromPool(pool, p, null, null, tags);
                if (line.NullOrEmpty()) continue;
                CouncilGameComponent.Speak(p, line.Formatted(p.Named("PAWN")).Resolve(), SpeechColor);
                vt.Spoke(p, now + GenDate.TicksPerDay);
                said++;
            }
            return said;
        }

        /// <summary>One colonist at home writes about it (a child more often, for the snow).</summary>
        private static void Write(Map map, string pool, bool children, params string[] tags)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null) return;
            var home = CalendarTracker.AtHome(map);
            if (home.Count == 0) return;
            var writer = home.RandomElementByWeight(p => children && p.DevelopmentalStage.Child() ? 3f : 1f);
            journals.WriteTagged(writer, pool, tags);
        }
    }
}
