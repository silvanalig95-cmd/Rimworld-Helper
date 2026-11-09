using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How the weapon rating is drawn wherever it shows: the badge (the number in its band's colour), the tag chips (each
    /// category's colour and its own mark, so colour is never the only cue), the badge's tooltip with the tags and the
    /// colonist's own rating, and the words for the steps that explain it. The marks for strengths, weaknesses and risks are
    /// Rimfolk's own icons (TrendUp, TrendDown, Warning); the star, diamond and dot are drawn once at first use.
    /// </summary>
    public static class WeaponUi
    {
        public static Color Colour(uint rgb) => new Color(SkinMath.R(rgb), SkinMath.G(rgb), SkinMath.B(rgb));

        public static Color RatingColour(float o) => Colour(WeaponForMath.RatingColour(o));

        /// <summary>The ground every badge and chip sits on, so its colour reads at 4.5:1 or better on any window.</summary>
        private static readonly Color ChipGround = new Color(SkinMath.R(SkinMath.WindowGround), SkinMath.G(SkinMath.WindowGround), SkinMath.B(SkinMath.WindowGround), 0.94f);

        /// <summary>A rating's badge: the number in its band's colour, framed in it, on a dark ground.</summary>
        public static void Badge(Rect r, float rating, Func<string> tip = null, int tipId = 0, GameFont font = GameFont.Tiny)
        {
            var c = RatingColour(rating);
            UiKit.RoundBox(r, ChipGround, c, SkinMath.RadiusSmall);
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = c;
            Widgets.Label(r, WeaponForMath.RatingText(rating));
            GUI.color = Color.white;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            // The tooltip is made only while the mouse is over it.
            if (tip != null) TooltipHandler.TipRegion(r, tip, tipId);
        }

        // ---------------- The tags' marks ----------------

        private static Texture2D star, diamond, dot;

        /// <summary>A tag category's mark: up for a strength, down for a weakness, a warning for a risk, a star, a diamond, a dot.</summary>
        public static Texture2D CategoryIcon(TagCategory c)
        {
            switch (c)
            {
                case TagCategory.Strength: return Icons.TrendUp;
                case TagCategory.Weakness: return Icons.TrendDown;
                case TagCategory.Risk: return Icons.Warning;
                case TagCategory.Special: return star ??= Shape(Star);
                case TagCategory.Role: return diamond ??= Shape(Diamond);
                default: return dot ??= Shape(Dot);
            }
        }

        private static bool Star(float x, float y) => Math.Sqrt(Math.Abs(x)) + Math.Sqrt(Math.Abs(y)) <= Math.Sqrt(0.92);

        private static bool Diamond(float x, float y) => Math.Abs(x) + Math.Abs(y) <= 0.86f;

        private static bool Dot(float x, float y) => x * x + y * y <= 0.62f * 0.62f;

        /// <summary>A white shape on a clear ground, smoothed at its edge (four by four samples a pixel), to be tinted.</summary>
        private static Texture2D Shape(Func<float, float, bool> inside)
        {
            const int size = 32, ss = 4;
            try
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "RimfolkWeaponMark", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < ss; sy++)
                            for (int sx = 0; sx < ss; sx++)
                            {
                                float u = (x + (sx + 0.5f) / ss) / size * 2f - 1f, v = (y + (sy + 0.5f) / ss) / size * 2f - 1f;
                                if (inside(u, v)) hits++;
                            }
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
                    }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                return tex;
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't make a weapon tag's mark: " + e.Message);
                return BaseContent.WhiteTex;
            }
        }

        // ---------------- Chips ----------------

        public const float ChipH = 20f;

        public static float ChipWidth(ThingTag t) => UiKit.KeptWidth(t.Label ?? "", GameFont.Tiny) + 28f;

        /// <summary>A tag as a chip: its mark and label in its category's colour, framed in it; its why on the tooltip. Returns its width.</summary>
        public static float Chip(float x, float y, ThingTag t, float maxWidth)
        {
            var style = WeaponTagMath.Style(t.Category);
            var c = Colour(style.Colour);
            float w = Mathf.Min(ChipWidth(t), maxWidth);
            var r = new Rect(x, y, w, ChipH);
            UiKit.RoundBox(r, ChipGround, UiKit.WithAlpha(c, 0.75f), SkinMath.RadiusCard);
            UiKit.Icon(new Rect(r.x + 6f, r.center.y - 6f, 12f, 12f), CategoryIcon(t.Category), c);
            UiKit.LabelFit(new Rect(r.x + 22f, r.y, Mathf.Max(0f, r.width - 26f), r.height), t.Label, c, GameFont.Tiny);
            TooltipHandler.TipRegion(r, WeaponInfoCard.CategoryWord(t.Category) + ": " + t.Label + "\n\n" + t.Why.CapitalizeFirst() + ".");
            return w;
        }

        /// <summary>Tags as chips in rows, wrapping when the width runs out. Returns the height used; with draw off, only measures.</summary>
        public static float Chips(float x, float y, float width, List<ThingTag> tags, bool draw = true)
        {
            if (tags == null || tags.Count == 0) return 0f;
            float cx = x, cy = y;
            foreach (var t in tags)
            {
                float w = Mathf.Min(ChipWidth(t), width);
                if (cx > x && cx + w > x + width)
                {
                    cx = x;
                    cy += ChipH + 4f;
                }
                if (draw) Chip(cx, cy, t, width);
                cx += w + 4f;
            }
            return cy - y + ChipH;
        }

        // ---------------- Words ----------------

        /// <summary>A step of a colonist's rating in words: their skill, a trait by name, their body and gear.</summary>
        public static string StepWords(string why)
        {
            if (why == "body and gear") return "CC_Weapon_Step_Body".Translate();
            if (why.StartsWith("Shooting ")) return SkillDefOf.Shooting.LabelCap + " " + why.Substring(9);
            if (why.StartsWith("Melee ")) return SkillDefOf.Melee.LabelCap + " " + why.Substring(6);
            return why.CapitalizeFirst();
        }

        /// <summary>The badge's tooltip: the rating and its band, the line, the tags with why, and the colonist's own rating.</summary>
        public static string Tip(WeaponReport r, Pawn p, Thing thing = null, bool clickOpensCard = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CC_Weapon_BadgeTip".Translate(WeaponForMath.RatingText(r.Rating.Overall).Named("RATING"), WeaponWords.Quality(r.Rating.Overall).Named("BAND"),
                WeaponInfoCard.RoleWords(r.Rating.Role).Named("ROLE")).Resolve() + (r.Rough ? " (" + "CC_Weapon_Rough".Translate() + ")" : ""));
            if (!r.Plain.NullOrEmpty()) sb.AppendLine(r.Plain);
            if (r.Tags.Count > 0)
            {
                sb.AppendLine();
                foreach (var t in r.Tags)
                    sb.AppendLine(t.Label.Colorize(Colour(WeaponTagMath.Style(t.Category).Colour)) + ": " + t.Why);
            }
            if (p != null && p.RaceProps.Humanlike)
            {
                string cant = WeaponLive.CantUse(p, thing, r);
                var fc = cant == null ? WeaponLive.ForColonist(p, r) : null;
                sb.AppendLine();
                if (cant != null) sb.AppendLine(cant);
                else if (fc != null)
                {
                    sb.Append("CC_Weapon_ForShort".Translate(p.Named("PAWN"), WeaponForMath.RatingText(fc.Rating).Named("RATING")).Resolve());
                    var steps = WeaponForMath.Telling(fc);
                    if (steps.Count > 0) sb.Append(" (" + string.Join(", ", steps.Select(s => StepWords(s.why).ToLowerInvariant() + " " + WeaponForMath.Delta(s.delta))) + ")");
                    sb.AppendLine();
                }
            }
            if (clickOpensCard && WeaponCardWindow.On) sb.Append("CC_Weapon_ClickForCard".Translate());
            return sb.ToString().TrimEnd();
        }
    }
}
