using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The weather of a camp's stay (the numbers are CampWeatherMath.cs). The coldest and hottest it is likely to bring
    /// come from the land's own reckoning of each hour of the stay (its seasons, its days and nights, its run of warm
    /// and cold days), with what a heat wave or a cold snap adds now. Travellers dress for it: someone who wouldn't be
    /// comfortable through it puts on a warmer or a cooler layer of their own people's kind, nothing past their people's
    /// craft or the colony's era and nothing dear, their armour and gear left on (Visitors/WeatherDress.cs, the way
    /// everyone who comes to the map dresses). What they put on is what they wear on the map, and the camp's letter says so.
    /// </summary>
    /// <summary>
    /// How a camp came dressed and pitched for its weather, for the letter that brings them (not saved): what they put on
    /// in words, whether that was mostly against the cold, beds drawn in round the fire for cold nights, behind a
    /// windbreak, and a spot among trees for shade in hot weather.
    /// </summary>
    public class CampPitch
    {
        public string clothes;
        public bool clothesForCold, cold, windbreak, shade;
        /// <summary>How they will sleep (CampTracker.ShelterOf): hut, huts, tents, tentbeds or ring.</summary>
        public string shelter;
    }

    public static class CampWeather
    {
        /// <summary>A condition no camp stays through, whatever they wear: poisoned air, a sky on fire, a winter of ash.</summary>
        public static GameCondition Hazard(Map map)
        {
            foreach (var c in map.gameConditionManager.ActiveConditions)
            {
                var d = c.def;
                if (d == GameConditionDefOf.ToxicFallout || d == GameConditionDefOf.VolcanicWinter || d == GameConditionDefOf.Flashstorm
                    || d == GameConditionDefOf.NoxiousHaze || WeatherMods.StormproofStorm(d)) return c;
            }
            return null;
        }

        /// <summary>The coldest and hottest the next <paramref name="ticks"/> on this map are likely to bring.</summary>
        public static (float low, float high) StayRange(Map map, int ticks)
        {
            float now = map.mapTemperature.OutdoorTemp;
            var temps = Find.World.tileTemperatures;
            int abs = Find.TickManager.TicksAbs;
            float reckoned = temps.OutdoorTemperatureAt(map.Tile, abs);
            float lo = reckoned, hi = reckoned;
            for (int t = GenDate.TicksPerHour; t <= ticks; t += GenDate.TicksPerHour)
            {
                float temp = temps.OutdoorTemperatureAt(map.Tile, abs + t);
                if (temp < lo) lo = temp;
                if (temp > hi) hi = temp;
            }
            return (CampWeatherMath.StayLow(now, lo + CampWeatherMath.Lasting(now, reckoned, true)),
                CampWeatherMath.StayHigh(now, hi + CampWeatherMath.Lasting(now, reckoned, false)));
        }

        private static Map usualMap;
        private static int usualHour = -1;
        private static (float low, float high) usual;

        /// <summary>The coming week's weather (<see cref="StayRange"/> over seven days), worked out once an hour for a home.</summary>
        public static (float low, float high) Usual(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (map != usualMap || hour != usualHour)
            {
                usualMap = map;
                usualHour = hour;
                usual = StayRange(map, GenDate.TicksPerDay * 7);
            }
            return usual;
        }

        /// <summary>
        /// Dresses one of them for weather from <paramref name="low"/> to <paramref name="high"/>: their kind's own winter
        /// clothes first, then plain layers of their people's age and kind, a layer at a time, as long as a layer brings them
        /// nearer comfortable through it (Visitors/WeatherDress.cs, as everyone who comes to the map dresses; their armour,
        /// belts and gear stay on). What they put on, in the order put on.
        /// </summary>
        public static List<Apparel> Dress(Pawn p, float low, float high, Faction f, CampKindDef kind = null)
        {
            var added = new List<Apparel>();
            if (p?.apparel == null || p.RaceProps?.Humanlike != true || f == null) return added;
            var tech = Peoples.TechOf(f) == TechLevel.Undefined ? TechLevel.Medieval : Peoples.TechOf(f);
            // Winter folk put on their own winter clothes first, when the stay is colder than they're comfortable in.
            if (kind?.Wears != null && low < p.ComfortableTemperatureRange().min) added.AddRange(WearOwn(p, kind, tech));
            added.AddRange(WeatherDress.Layers(p, low, high, tech, f.HostileTo(Faction.OfPlayer)));
            // A layer swapped for a better one later is gone: only what they still wear.
            added.RemoveAll(a => a.Destroyed || a.Wearer != p);
            return added;
        }

        /// <summary>
        /// A winter kind's own clothes against the cold (CampKindDef.wears): each entry for their people's age, the first of its
        /// defs they can wear (their size, age and shape), of its first material that fits; whatever it can't be worn with
        /// comes off. Their own gear, so neither the price nor the tribes' rule on furs holds them back.
        /// </summary>
        private static List<Apparel> WearOwn(Pawn p, CampKindDef kind, TechLevel tech)
        {
            var put = new List<Apparel>();
            var body = p.RaceProps.body;
            foreach (var w in kind.Wears)
            {
                if (w == null || !w.ForAge(tech)) continue;
                var def = w.Defs.FirstOrDefault(t => t.apparel.PawnCanWear(p, false) && t.apparel.CorrectAgeForWearing(p) && ApparelUtility.HasPartsToWear(p, t));
                if (def == null || p.apparel.WornApparel.Any(a => a.def == def)) continue;
                // Never over their armour, belt or gear.
                if (p.apparel.WornApparel.Any(a => !ApparelUtility.CanWearTogether(a.def, def, body) && WeatherDress.Kept(a, p))) continue;
                try
                {
                    foreach (var old in p.apparel.WornApparel.Where(a => !ApparelUtility.CanWearTogether(a.def, def, body)).ToList())
                    {
                        p.apparel.Remove(old);
                        old.Destroy();
                    }
                    var ap = (Apparel)ThingMaker.MakeThing(def, w.StuffFor(def));
                    PawnApparelGenerator.PostProcessApparel(ap, p);
                    p.apparel.Wear(ap, false);
                    put.Add(ap);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp winter clothes " + def.defName, e);
                }
            }
            put.RemoveAll(a => a.Destroyed || a.Wearer != p);
            return put;
        }

        /// <summary>What a group put on for the weather, in words ("parkas and tuques"), or null for nothing.</summary>
        public static string Clothes(IEnumerable<Apparel> added)
        {
            var kinds = added.Where(a => a != null).GroupBy(a => a.def).OrderByDescending(g => g.Count()).Take(3)
                .Select(g => g.Count() > 1 ? Find.ActiveLanguageWorker.Pluralize(g.Key.label, g.Count()) : Find.ActiveLanguageWorker.WithIndefiniteArticle(g.Key.label)).ToList();
            return kinds.Count == 0 ? null : TextUtil.JoinAnd(kinds);
        }
    }
}
