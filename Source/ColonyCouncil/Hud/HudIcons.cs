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

        // Rimfolk's own look (Hud/Look/): the cards' title icons the panels had none of, and two marks on a colonist's row.
        public static readonly Texture2D Trend = Load("Trend");
        public static readonly Texture2D Vitals = Load("Vitals");
        public static readonly Texture2D Stock = Load("Stock");
        /// <summary>A day off or a bad afternoon under way: the sun half over the horizon.</summary>
        public static readonly Texture2D DayOff = Load("DayOff");
        /// <summary>An empty weapon slot in a colonist's kit: a sword's outline.</summary>
        public static readonly Texture2D KitGap = Load("KitGap");

        /// <summary>A panel's icon in its card's title row: one of Rimfolk's set for each, the same weight and size.</summary>
        public static Texture2D For(string id)
        {
            switch (id)
            {
                case HudIds.Moods: return Icons.Moods;
                case HudIds.Trend: return Trend ?? Icons.Moods;
                case HudIds.Vitals: return Vitals ?? Icons.Colony;
                case HudIds.Council: return Icons.Council;
                case HudIds.Alerts: return Icons.Warning;
                case HudIds.Health: return Icons.CatHealth;
                case HudIds.Work: return Icons.Priorities;
                case HudIds.Inventory: return Stock ?? Icons.Base;
                case HudIds.Following: return Icons.Pin;
                default: return Panels;
            }
        }

        /// <summary>
        /// The game's own drop of blood, the one its Health tab shows by a bleeding wound (HealthCardUtility's BleedingIcon):
        /// the health panel marks bleeding with it. Null if the game ever moves it; the panel then draws no icon.
        /// </summary>
        public static readonly Texture2D Bleeding = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding", false);

        private static Texture2D Load(string name) => ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/" + name, false);
    }
}
