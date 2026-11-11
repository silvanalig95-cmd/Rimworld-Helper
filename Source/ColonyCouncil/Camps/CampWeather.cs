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
    /// craft or the colony's era and nothing dear (the game's own measure for a free warm layer: a plain parka's worth
    /// and a little more). What they put on is what they wear on the map, and the camp's letter says so.
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

        private static List<ThingStuffPair> pairs;
        private static float maxPrice;

        /// <summary>Every garment and what it can be made of, that the game may hand out against the cold or heat.</summary>
        private static List<ThingStuffPair> Pairs
        {
            get
            {
                if (pairs == null)
                {
                    pairs = ThingStuffPair.AllWith(td => td.IsApparel && td.apparel.canBeGeneratedToSatisfyWarmth && !td.apparel.mechanitorApparel && !td.apparel.slaveApparel)
                        .Where(pa => pa.InsulationCold > 0f || pa.InsulationHeat > 0f).ToList();
                    maxPrice = StatDefOf.MarketValue.Worker.GetValueAbstract(ThingDefOf.Apparel_Parka, ThingDefOf.Cloth) * 1.5f;
                }
                return pairs;
            }
        }

        /// <summary>Theirs to wear: their size, age and shape, of their people's craft and the colony's era, not dear.</summary>
        private static bool Fits(ThingStuffPair pa, Pawn p, TechLevel tech)
        {
            var t = pa.thing;
            if (pa.Price > maxPrice || !t.apparel.PawnCanWear(p, false) || !t.apparel.CorrectAgeForWearing(p) || !ApparelUtility.HasPartsToWear(p, t)) return false;
            if (t.techLevel > tech) return false;
            if (Avail.IsPreIndustrial && t.techLevel > TechLevel.Medieval) return false;
            // The game's own rule: tribal hides for tribal folk, no furs of the tribes for those past them, unless meant for anyone.
            if (!t.apparel.anyTechLevelCanUseForWarmth)
            {
                if (tech >= TechLevel.Industrial && t.techLevel == TechLevel.Neolithic) return false;
                if (tech == TechLevel.Neolithic && t.techLevel >= TechLevel.Industrial) return false;
            }
            return true;
        }

        /// <summary>
        /// Dresses one of them for weather from <paramref name="low"/> to <paramref name="high"/>, a layer at a time,
        /// as long as a layer brings them closer to comfortable through it. What they put on, in the order put on.
        /// </summary>
        public static List<Apparel> Dress(Pawn p, float low, float high, Faction f, CampKindDef kind = null)
        {
            var added = new List<Apparel>();
            if (p?.apparel == null || p.RaceProps?.Humanlike != true || f == null) return added;
            var tech = Peoples.TechOf(f) == TechLevel.Undefined ? TechLevel.Medieval : Peoples.TechOf(f);
            var body = p.RaceProps.body;
            // Winter folk put on their own winter clothes first, when the stay is colder than they're comfortable in.
            if (kind?.Wears != null && low < p.ComfortableTemperatureRange().min) added.AddRange(WearOwn(p, kind, tech));
            for (int step = 0; step < 4; step++)
            {
                var comfy = p.ComfortableTemperatureRange();
                float shortNow = CampWeatherMath.Shortfall(comfy.min, comfy.max, low, high);
                if (shortNow <= 0.5f) break;
                bool needCold = comfy.min > low, needHeat = comfy.max < high;
                var worn = p.apparel.WornApparel.ToList();
                var cold = worn.Select(a => a.GetStatValue(StatDefOf.Insulation_Cold)).ToList();
                var heat = worn.Select(a => a.GetStatValue(StatDefOf.Insulation_Heat)).ToList();
                // Comfortable with nothing on, from what they are now with what they wear.
                float bareMin = comfy.min + cold.Sum(), bareMax = comfy.max - heat.Sum();
                ThingStuffPair best = default;
                bool found = false;
                float bestShort = shortNow - 0.5f;
                foreach (var pa in Pairs)
                {
                    // Only what helps against what they're short of.
                    if (!(needCold && pa.InsulationCold > 0f) && !(needHeat && pa.InsulationHeat > 0f)) continue;
                    if (!Fits(pa, p, tech)) continue;
                    float c = pa.InsulationCold, h = pa.InsulationHeat;
                    for (int i = 0; i < worn.Count; i++)
                    {
                        if (!ApparelUtility.CanWearTogether(worn[i].def, pa.thing, body)) continue;
                        c += cold[i];
                        h += heat[i];
                    }
                    float s = CampWeatherMath.Shortfall(bareMin - c, bareMax + h, low, high);
                    if (s < bestShort - 0.01f || (found && Mathf.Abs(s - bestShort) <= 0.01f && pa.Price < best.Price))
                    {
                        best = pa;
                        bestShort = s;
                        found = true;
                    }
                }
                if (!found) break;
                foreach (var w in worn.Where(w => !ApparelUtility.CanWearTogether(w.def, best.thing, body)))
                {
                    p.apparel.Remove(w);
                    w.Destroy();
                }
                var ap = (Apparel)ThingMaker.MakeThing(best.thing, best.stuff);
                PawnApparelGenerator.PostProcessApparel(ap, p);
                p.apparel.Wear(ap, false);
                added.Add(ap);
            }
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
