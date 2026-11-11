using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The temperature scale in the game (the rules are TemperatureScaleMath.cs): the colonists' comfort range, which is the
    /// game's own for people (the Human def's, the one its warm-clothes alert reads, so a mod that moves it moves the scale),
    /// asked once; and a temperature's colour on it. The game's readout and the vitals' outdoor temperature both ask here.
    /// </summary>
    public static class TemperatureScale
    {
        private static float humanMin = float.NaN, humanMax = float.NaN;

        /// <summary>The colonists' comfort range (Celsius): the Human def's own, else the base game's.</summary>
        public static float HumanMin
        {
            get
            {
                Look();
                return humanMin;
            }
        }

        public static float HumanMax
        {
            get
            {
                Look();
                return humanMax;
            }
        }

        private static void Look()
        {
            if (!float.IsNaN(humanMin)) return;
            humanMin = HudRank.HumanComfyMin;
            humanMax = HudRank.HumanComfyMax;
            try
            {
                var human = ThingDefOf.Human;
                if (human == null) return;
                humanMin = human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
                humanMax = human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            }
            catch (Exception e)
            {
                humanMin = HudRank.HumanComfyMin;
                humanMax = HudRank.HumanComfyMax;
                CouncilLog.ErrorOnce("the temperature scale: the human comfort range", e);
            }
        }

        /// <summary>The colour of a temperature (Celsius) on the scale, around the colonists' comfort range.</summary>
        public static uint Rgb(float celsius) => TemperatureScaleMath.Rgb(celsius, HumanMin, HumanMax);

        /// <summary>The same as a colour to draw with.</summary>
        public static Color Of(float celsius) => ColorOf(Rgb(celsius));

        public static Color ColorOf(uint rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
