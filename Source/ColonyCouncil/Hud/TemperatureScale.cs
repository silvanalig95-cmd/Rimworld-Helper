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

        /// <summary>How full the thermometer is for a temperature (Celsius), 0 to 1.</summary>
        public static float Level(float celsius) => TemperatureScaleMath.Level(celsius, HumanMin, HumanMax);

        /// <summary>The temperature in a word ("comfortable", "killing cold").</summary>
        public static string Word(float celsius) => ("CC_TempFeel_" + TemperatureScaleMath.Feel(celsius, HumanMin, HumanMax)).Translate().Resolve();

        /// <summary>The thermometer's tooltip: the temperature and its word, and the colonists' comfortable range, in the player's unit.</summary>
        public static string Tip(float celsius) =>
            "CC_TempReadout_Tip".Translate(celsius.ToStringTemperature("F0").Named("TEMP"), Word(celsius).Named("FEEL"), HumanMin.ToStringTemperature("F0").Named("MIN"),
                HumanMax.ToStringTemperature("F0").Named("MAX")).Resolve();
    }
}
