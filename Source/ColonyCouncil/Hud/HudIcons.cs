using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The on-screen panels' own icons, drawn with the rest of Rimfolk's set by Tools/make_icons.py (white glyphs in
    /// Textures/ColonyCouncil/Icons, tinted when drawn). A missing file loads as null and the panels fall back to
    /// another icon.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HudIcons
    {
        /// <summary>The switch by the play settings: a screen with two small panels on it.</summary>
        public static readonly Texture2D Panels = Load("Panels");
        public static readonly Texture2D Power = Load("Power");
        public static readonly Texture2D Temperature = Load("Temperature");
        public static readonly Texture2D Research = Load("Research");

        /// <summary>
        /// The game's own drop of blood, the one its Health tab shows by a bleeding wound (HealthCardUtility's BleedingIcon):
        /// the health panel marks bleeding with it. Null if the game ever moves it; the panel then draws no icon.
        /// </summary>
        public static readonly Texture2D Bleeding = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding", false);

        private static Texture2D Load(string name) => ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/" + name, false);
    }
}
