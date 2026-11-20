using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of a cup for the sick (Immersion/SickCup.cs, C24 of docs/content-update-proposals.md): which ailments it is
    /// for, what may be brought (the player's mods, by name, the first found in the stores), how often, and how much sooner
    /// the ailment ends. No game types, so Tools/Tests checks it against the defs and the mod catalogue.
    /// </summary>
    public static class SickCupMath
    {
        /// <summary>A cold, a cough or a sore throat: Rimfolk's everyday ailments of the nose, throat and chest (Defs/HediffDefs).</summary>
        public static readonly string[] Ailments =
        {
            "Council_Ail_HeadCold", "Council_Ail_ChestCold", "Council_Ail_SmokeCough", "Council_Ail_RimrotCough", "Council_Ail_FluCough",
            "Council_Ail_PollutionCough", "Council_Ail_LingeringCough", "Council_Ail_SmokersCough", "Council_Ail_SporeCough"
        };

        /// <summary>
        /// What is brought, the first of these the stores hold that the sick one will have: Vanilla Brewing Expanded - Coffees
        /// and Teas' mint, lemon, breakfast, green and earl grey teas, Vanilla Brewing Expanded's own tea, Vanilla Christmas
        /// Expanded's hot cocoa, then honey (Alpha Bees', Vanilla Factions Expanded - Medieval 2's raw honey). Without any of
        /// these mods nothing is brought.
        /// </summary>
        public static readonly string[] Drinks =
        {
            "VBE_MintTea", "VBE_LemonTea", "VBE_BlackTea", "VBE_GreenTea", "VBE_EarlGreyTea", "VBE_Tea", "VCE_HotCocoa", "RB_BaseHoney", "VFEM2_Honey"
        };

        /// <summary>What a drink is, for the words said with it: "tea", "cocoa" or "honey".</summary>
        public static string KindOf(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (defName.IndexOf("Honey", StringComparison.Ordinal) >= 0) return "honey";
            if (defName.IndexOf("Cocoa", StringComparison.Ordinal) >= 0) return "cocoa";
            return defName.IndexOf("Tea", StringComparison.Ordinal) >= 0 ? "tea" : null;
        }

        public static bool IsAilment(string defName) => Array.IndexOf(Ailments, defName) >= 0;

        /// <summary>The hours of the day a cup is brought in (from, to): waking hours.</summary>
        public const int FromHour = 7, ToHour = 21;

        /// <summary>One cup a day for each who is ill, and at most one cup set going every two hours on a map.</summary>
        public const float DaysBetween = 1f, MapHoursBetween = 2f;

        /// <summary>How far the bringer goes for the cup and to the sick one.</summary>
        public const float Reach = 60f;

        /// <summary>
        /// How much the ailment eases with a cup: a little off its severity, so it ends a few hours sooner (a head cold falls
        /// about 0.18 a day once the body has the better of it: some five hours). An ailment almost gone is left to go.
        /// </summary>
        public const float Ease = 0.04f, LeaveBelow = 0.06f;

        public static float Eased(float severity) => severity < LeaveBelow ? severity : Math.Max(0.02f, severity - Ease);

        /// <summary>A cup for them is due: none in the last day.</summary>
        public static bool Due(int lastTick, int now, int ticksPerDay = 60000) => lastTick < 0 || now < lastTick || now - lastTick >= DaysBetween * ticksPerDay;
    }
}
