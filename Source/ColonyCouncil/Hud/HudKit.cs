using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How the on-screen panels and the hover card look: the background for the chosen look and opacity, text sizes for
    /// a panel's scale and the chosen text size, colours by <see cref="HudLevel"/>, the small mood bar with its break
    /// lines, and the arrow for where a mood is heading. Built on UiKit's palette so the panels match the council window.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HudKit
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static readonly Color MinorLine = new Color(0.95f, 0.78f, 0.3f, 0.9f);
        public static readonly Color MajorLine = new Color(0.93f, 0.38f, 0.3f, 0.95f);
        public static readonly Color BarOk = new Color(0.42f, 0.66f, 0.45f);
        public static readonly Color BarLow = new Color(0.92f, 0.72f, 0.3f);
        public static readonly Color BarBad = new Color(0.93f, 0.5f, 0.25f);
        public static readonly Color BarWorst = new Color(0.9f, 0.26f, 0.24f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.08f);

        // ---------------- Background ----------------

        /// <summary>The panel's fill for the chosen look, times its opacity (fuller while the mouse is on it).</summary>
        public static Color Background(float opacity, bool hovered)
        {
            Color c;
            switch (S?.hudTheme ?? HudTheme.Dark)
            {
                case HudTheme.Darker:
                    c = new Color(0.05f, 0.055f, 0.06f, 0.9f);
                    break;
                case HudTheme.Clear:
                    c = new Color(0.08f, 0.09f, 0.1f, 0.42f);
                    break;
                default:
                    c = new Color(0.1f, 0.11f, 0.12f, 0.74f);
                    break;
            }
            float a = c.a * Mathf.Clamp01(opacity);
            if (hovered) a = Mathf.Max(a, Mathf.Min(0.92f, c.a + 0.15f));
            return new Color(c.r, c.g, c.b, a);
        }

        public static void Panel(Rect r, float opacity, bool hovered)
        {
            if (Event.current.type != EventType.Repaint) return;
            Widgets.DrawBoxSolid(r, Background(opacity, hovered));
            if (hovered || (S?.hudTheme ?? HudTheme.Dark) != HudTheme.Clear) Widgets.DrawBox(r, 1, BorderTex);
        }

        private static readonly Texture2D BorderTex = SolidColorMaterials.NewSolidColorTexture(Border);

        // ---------------- Text ----------------

        private static readonly System.Collections.Generic.Dictionary<string, string> texts = new System.Collections.Generic.Dictionary<string, string>();
        private static LoadedLanguage textsLanguage;

        /// <summary>A fixed line, translated once per language: what the panels draw every frame (labels, tooltips).</summary>
        public static string T(string key)
        {
            if (textsLanguage != LanguageDatabase.activeLanguage)
            {
                texts.Clear();
                textsLanguage = LanguageDatabase.activeLanguage;
            }
            if (!texts.TryGetValue(key, out var s))
            {
                s = key.Translate().Resolve();
                texts[key] = s;
            }
            return s;
        }

        /// <summary>The main text's font (names, values) at this scale.</summary>
        public static GameFont Main(float scale)
        {
            bool small = (S?.hudFont ?? HudFont.Normal) == HudFont.Small;
            if (small) return scale >= 1.3f ? GameFont.Small : GameFont.Tiny;
            return scale >= 0.9f ? GameFont.Small : GameFont.Tiny;
        }

        /// <summary>The second line's font (what drags a mood down, a note) at this scale.</summary>
        public static GameFont Minor(float scale)
        {
            bool small = (S?.hudFont ?? HudFont.Normal) == HudFont.Small;
            return !small && scale >= 1.35f ? GameFont.Small : GameFont.Tiny;
        }

        /// <summary>A line's height in a font.</summary>
        public static float LineHeight(GameFont f) => f == GameFont.Tiny ? (Text.TinyFontSupported ? 16f : 20f) : f == GameFont.Small ? 22f : 30f;

        public static void Label(Rect r, string text, Color color, GameFont font, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (text.NullOrEmpty() || r.width <= 2f) return;
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(r, UiKit.Fit(text, r.width));
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        public static float Width(string text, GameFont font)
        {
            if (text.NullOrEmpty()) return 0f;
            var old = Text.Font;
            Text.Font = font;
            float w = Text.CalcSize(text).x;
            Text.Font = old;
            return w;
        }

        // ---------------- Colours by what matters ----------------

        public static Color ColorOf(HudLevel level, Color quiet)
        {
            switch (level)
            {
                case HudLevel.Life: return UiKit.Bad;
                case HudLevel.Soon: return UiKit.Warn;
                default: return quiet;
            }
        }

        public static Color ColorOf(float score, Color quiet) => ColorOf(HudRank.LevelOf(score), quiet);

        /// <summary>A soft pulse between 0 and 1 (about once every two seconds), for what's below a major break line.</summary>
        public static float Pulse => 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3.1f);

        /// <summary>The mood bar's colour: green above the minor line, amber under it, orange under the major, red under the extreme.</summary>
        public static Color MoodColor(float now, float minor, float major, float extreme) =>
            now < extreme ? BarWorst : now < major ? BarBad : now < minor ? BarLow : BarOk;

        // ---------------- The mood bar ----------------

        /// <summary>
        /// A thin mood bar: the level, a tick at the minor and the major break lines, and a mark where the mood is heading.
        /// </summary>
        public static void MoodBar(Rect r, float now, float heading, float minor, float major, float extreme)
        {
            if (Event.current.type != EventType.Repaint) return;
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            now = Mathf.Clamp01(now);
            var fill = new Rect(r.x, r.y, Mathf.Max(1f, r.width * now), r.height);
            Widgets.DrawBoxSolid(fill, UiKit.WithAlpha(MoodColor(now, minor, major, extreme), 0.9f));
            if (r.height >= 4f) Widgets.DrawBoxSolid(new Rect(fill.x, fill.y, fill.width, 1f), new Color(1f, 1f, 1f, 0.18f));
            float tickH = r.height + 4f;
            Widgets.DrawBoxSolid(new Rect(r.x + r.width * Mathf.Clamp01(major) - 1f, r.y - 2f, 2f, tickH), MajorLine);
            Widgets.DrawBoxSolid(new Rect(r.x + r.width * Mathf.Clamp01(minor) - 1f, r.y - 2f, 2f, tickH), MinorLine);
            if (Mathf.Abs(heading - now) >= 0.01f)
            {
                var c = heading > now ? UiKit.Good : UiKit.Bad;
                Widgets.DrawBoxSolid(new Rect(r.x + r.width * Mathf.Clamp01(heading) - 1f, r.y, 2f, r.height), UiKit.WithAlpha(c, 0.95f));
            }
        }

        /// <summary>An arrow up or down for where the mood is heading, or nothing when it's steady.</summary>
        public static void Heading(Rect r, float now, float heading)
        {
            if (Mathf.Abs(heading - now) < 0.01f) return;
            bool up = heading > now;
            var tex = up ? Icons.TrendUp : Icons.TrendDown;
            var c = up ? UiKit.Good : UiKit.Bad;
            if (tex != null) UiKit.Icon(r, tex, c);
            else Label(r, up ? "+" : "-", c, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        /// <summary>"42%", the mood as the game shows it.</summary>
        public static string Percent(float v) => v.ToStringPercent();

        /// <summary>"-8", a mood's size as a whole number with its sign.</summary>
        public static string Signed(float v) => v >= 0f ? "+" + Mathf.RoundToInt(v) : "-" + Mathf.RoundToInt(-v);

        /// <summary>Two letters for the compact board: the first of each of two names, or the first two letters of one.</summary>
        public static string Initials(Pawn p)
        {
            string name = p?.LabelShort ?? "?";
            try
            {
                if (p?.Name is NameTriple t && !t.First.NullOrEmpty() && !t.Last.NullOrEmpty() && t.Nick == t.First)
                    return (t.First.Substring(0, 1) + t.Last.Substring(0, 1)).ToUpperInvariant();
            }
            catch (Exception)
            {
            }
            name = name.Trim();
            if (name.Length <= 2) return name;
            return name.Substring(0, 1).ToUpperInvariant() + name.Substring(1, 1);
        }
    }
}
