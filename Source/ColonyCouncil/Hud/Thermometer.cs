using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small thermometer in the temperature's colour, filled to its height (the rules are TemperatureScaleMath.cs): beside
    /// the game's temperature line at the bottom right, and as the vitals' outdoor temperature's icon. Three white layers
    /// drawn by Tools/make_thermometer.py (Textures/ColonyCouncil/Thermometer), tinted as they're drawn: the shape a little
    /// larger in black underneath (so it reads over snow), the inside in the colour cut off at the level (the bulb always
    /// full), the glass light on top. A missing picture draws nothing.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Thermometer
    {
        private static readonly Texture2D Back = Layer("Back"), Fill = Layer("Fill"), Glass = Layer("Glass");

        private static readonly Color GlassColour = new Color(0.88f, 0.88f, 0.88f, 0.95f);

        private static Texture2D Layer(string name) => ContentFinder<Texture2D>.Get("ColonyCouncil/Thermometer/" + name, false);

        /// <summary>Its pictures were found.</summary>
        public static bool Ready => Back != null && Fill != null && Glass != null;

        /// <summary>
        /// The thermometer in <paramref name="r"/> (twice as tall as wide; a wider rect keeps it centred): filled to
        /// <paramref name="level"/> (0 to 1) in <paramref name="colour"/>. On repaint only.
        /// </summary>
        public static void Draw(Rect r, float level, Color colour, float alpha = 1f)
        {
            if (!Ready || Event.current == null || Event.current.type != EventType.Repaint) return;
            float h = Mathf.Min(r.height, r.width * 2f);
            float w = h / 2f;
            var at = new Rect(Mathf.Round(r.x + (r.width - w) / 2f), Mathf.Round(r.y + (r.height - h) / 2f), Mathf.Round(w), Mathf.Round(h));
            var was = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, TemperatureScaleMath.ThermoBackAlpha * alpha);
            GUI.DrawTexture(at, Back);
            float shown = TemperatureScaleMath.Shown(level);
            if (shown > 0f)
            {
                // The fill's lower part only: its picture from the bottom up to the level (texture coordinates count from the bottom).
                var part = new Rect(at.x, at.y + at.height * (1f - shown), at.width, at.height * shown);
                GUI.color = new Color(colour.r, colour.g, colour.b, alpha);
                GUI.DrawTextureWithTexCoords(part, Fill, new Rect(0f, 0f, 1f, shown));
            }
            GUI.color = new Color(GlassColour.r, GlassColour.g, GlassColour.b, GlassColour.a * alpha);
            GUI.DrawTexture(at, Glass);
            GUI.color = was;
        }
    }
}
