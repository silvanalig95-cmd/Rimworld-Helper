using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The interface design's tokens and the components names, cards and the drawer are drawn with
    /// (docs/design/interface.md, "The look"): link ink and its line, the cool stance, done and danger, the drawer's and
    /// the preview's surfaces; a section heading with its count and fold, a thing chip (a link with its picture and a
    /// preview), a state chip (a dot and a word), an act button with why not, steps for what is under way, an icon button.
    /// Measured once per width where they can be (their words through UiKit.Fit and LinkText's layouts).
    /// </summary>
    public static partial class UiKit
    {
        // ---------------- Tokens ----------------

        /// <summary>A name that leads to its card: a light parchment, a little warmer than the text.</summary>
        public static readonly Color Link = new Color(0.945f, 0.863f, 0.667f);
        public static readonly Color LinkLine = new Color(0.945f, 0.863f, 0.667f, 0.5f);
        public static readonly Color LinkHot = new Color(1f, 0.949f, 0.812f);
        /// <summary>A stance neither warm nor sour: neutral or wary.</summary>
        public static readonly Color Cool = new Color(0.616f, 0.714f, 0.839f);
        /// <summary>Finished, kept, answered.</summary>
        public static readonly Color Done = new Color(0.561f, 0.627f, 0.561f);
        /// <summary>Harm now (brighter than Bad, always with the warning icon).</summary>
        public static readonly Color Danger = new Color(1f, 0.353f, 0.282f);
        public static readonly Color DrawerFill = new Color(0.125f, 0.141f, 0.157f, 1f);
        public static readonly Color PreviewFill = new Color(0.165f, 0.184f, 0.204f, 1f);
        public static readonly Color PreviewEdge = new Color(0.298f, 0.318f, 0.341f, 1f);

        // ---------------- Section heading ----------------

        /// <summary>Height of <see cref="SectionHeading"/>, with the space under it.</summary>
        public const float HeadingHeight = 26f;

        /// <summary>
        /// A section's heading: a fold caret (with <paramref name="folds"/>), its name, its count, an optional note on the
        /// right, and a line under it. Returns <see cref="HeadingHeight"/>; <paramref name="open"/> says whether to draw
        /// the section under it.
        /// </summary>
        public static float SectionHeading(float x, float y, float width, string label, int count, Folds folds, string key, out bool open, string right = null)
        {
            open = folds == null || folds.IsOpen(key);
            var hit = new Rect(x, y, width, 22f);
            float tx = x;
            if (folds != null)
            {
                if (Mouse.IsOver(hit)) Widgets.DrawHighlight(hit);
                Icon(new Rect(x + 1f, y + 6f, 10f, 10f), open ? TexButton.Collapse : TexButton.Reveal, WithAlpha(HeaderColor, 0.8f));
                tx += 15f;
            }
            Text.Font = GameFont.Small;
            float rw = 0f;
            if (!right.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                rw = Mathf.Min(Text.CalcSize(right).x + 4f, width * 0.45f);
                LabelFit(new Rect(x + width - rw, y, rw, 22f), right, Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                rw += 6f;
                Text.Font = GameFont.Small;
            }
            float lw = Mathf.Min(Text.CalcSize(label ?? "").x + 2f, Mathf.Max(0f, x + width - rw - tx - 24f));
            LabelFit(new Rect(tx, y, lw, 22f), label, HeaderColor);
            if (count >= 0) LabelFit(new Rect(tx + lw + 5f, y + 1f, 40f, 22f), count.ToString(), Faint, GameFont.Tiny);
            Divider(x, y + 22f, width);
            if (folds != null && Widgets.ButtonInvisible(hit, false))
            {
                folds.Toggle(key);
                (open ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
            }
            return HeadingHeight;
        }

        // ---------------- Chips ----------------

        /// <summary>Height of a chip.</summary>
        public const float ChipHeight = 20f;

        public static float StateChipWidth(string word, float maxWidth = 260f)
        {
            if (word.NullOrEmpty()) return 0f;
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            float w = Mathf.Min(maxWidth, Text.CalcSize(word).x + 22f);
            Text.Font = old;
            return w;
        }

        /// <summary>A state: a dot in its colour and a word ("friendly", "thriving"). Returns the width used.</summary>
        public static float StateChip(float x, float y, string word, Color color, float maxWidth = 260f, string tip = null)
        {
            if (word.NullOrEmpty()) return 0f;
            float w = StateChipWidth(word, maxWidth);
            var r = new Rect(x, y, w, ChipHeight);
            Widgets.DrawBoxSolidWithOutline(r, WithAlpha(color, 0.08f), WithAlpha(color, 0.35f), 1);
            DiscFill(new Rect(x + 6f, y + ChipHeight / 2f - 3.5f, 7f, 7f), color);
            LabelFit(new Rect(x + 17f, y, w - 19f, ChipHeight), word, color, GameFont.Tiny);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            return w;
        }

        public static float ThingChipWidth(string label, float maxWidth = 240f)
        {
            if (label.NullOrEmpty()) return 0f;
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            float w = Mathf.Min(maxWidth, Text.CalcSize(label).x + ChipHeight + 10f);
            Text.Font = old;
            return w;
        }

        /// <summary>
        /// A thing: its picture (a portrait, a people's icon, or an icon) and its name as a link, with a preview after a
        /// pause and its card on a click. Returns the width used.
        /// </summary>
        public static float ThingChip(float x, float y, ThingRef thing, string label, Pawn pawn = null, Faction faction = null, Texture2D icon = null,
            float maxWidth = 240f)
        {
            if (label.NullOrEmpty()) return 0f;
            float w = ThingChipWidth(label, maxWidth);
            var r = new Rect(x, y, w, ChipHeight);
            bool over = Mouse.IsOver(r);
            Widgets.DrawBoxSolidWithOutline(r, over ? CardHover : CardFill, CardBorder, 1);
            var pic = new Rect(x + 2f, y + 2f, ChipHeight - 4f, ChipHeight - 4f);
            Picture(pic, pawn, faction, icon, Faint);
            var tr = new Rect(x + ChipHeight + 2f, y, w - ChipHeight - 6f, ChipHeight);
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            string s = Fit(label, tr.width);
            float tw = Text.CalcSize(s).x;
            LabelFit(tr, s, over ? LinkHot : Link, GameFont.Tiny);
            Text.Font = old;
            if (!thing.IsNone)
            {
                LinkText.Underline(tr.x, y + ChipHeight - 4f, Mathf.Min(tw - 4f, tr.width), over);
                if (over) Previews.Hover(thing, r);
                if (Widgets.ButtonInvisible(r, false))
                {
                    Cards.Click();
                    Cards.Open(thing);
                }
            }
            return w;
        }

        /// <summary>A thing's picture in a square: the pawn's portrait, else the people's icon in their colour, else the icon.</summary>
        public static void Picture(Rect r, Pawn pawn, Faction faction, Texture2D icon, Color iconColor, Color? ring = null)
        {
            if (pawn != null && !pawn.Discarded)
            {
                Portrait(r, pawn, ring ?? Neutral);
                return;
            }
            if (faction != null)
            {
                Texture2D fi = null;
                var color = Color.white;
                try
                {
                    fi = faction.def?.FactionIcon;
                    color = faction.Color;
                }
                catch (Exception)
                {
                    fi = null;
                }
                if (fi == BaseContent.BadTex) fi = null;
                if (fi != null)
                {
                    DiscFill(r, new Color(0.12f, 0.13f, 0.14f, 1f));
                    Icon(r.ContractedBy(Mathf.Round(r.width * 0.18f)), fi, color);
                    return;
                }
            }
            if (icon != null) Badge(r, icon, iconColor);
        }

        // ---------------- Acts ----------------

        /// <summary>
        /// An act's button: greyed when it can't be done now, with the reason as its tooltip (a card also says it in a line
        /// under its acts). Returns true when used.
        /// </summary>
        public static bool ActButton(Rect r, string label, string whyNot, string tip = null)
        {
            bool can = whyNot == null;
            Text.Font = GameFont.Small;
            bool clicked = Widgets.ButtonText(r, Fit(label, r.width - 10f), true, true, can);
            string t = can ? tip : whyNot;
            if (!t.NullOrEmpty()) TooltipHandler.TipRegion(r, t);
            return clicked && can;
        }

        public static float ActButtonWidth(string label)
        {
            var old = Text.Font;
            Text.Font = GameFont.Small;
            float w = Mathf.Clamp(Text.CalcSize(label ?? "").x + 24f, 72f, 220f);
            Text.Font = old;
            return w;
        }

        // ---------------- Steps ----------------

        /// <summary>
        /// Something under way in steps (written, on the road, arrived, answered): a dot for each, the ones done filled, the
        /// one it is at lit, each with its word; never a time left. Returns the height used.
        /// </summary>
        public static float Steps(float x, float y, float width, string[] words, int at)
        {
            if (words == null || words.Length == 0) return 0f;
            int n = words.Length;
            float cell = width / n;
            float lineY = y + 6f;
            Widgets.DrawBoxSolid(new Rect(x + cell / 2f, lineY - 0.5f, cell * (n - 1), 1f), DividerColor);
            for (int i = 0; i < n; i++)
            {
                float cx = x + cell * i + cell / 2f;
                var dot = new Rect(cx - 5f, lineY - 5f, 10f, 10f);
                var c = i < at ? Done : i == at ? Info : Dim;
                if (i <= at) DiscFill(dot, c);
                else RingDraw(dot, c);
                var old = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = i == at ? Color.white : i < at ? Done : Dim;
                Widgets.Label(new Rect(cx - cell / 2f, lineY + 7f, cell, 18f), Fit(words[i], cell - 2f));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = old;
            }
            return 32f;
        }

        // ---------------- Small buttons ----------------

        /// <summary>A square icon button, lit under the mouse, with its tooltip; returns true when clicked.</summary>
        public static bool IconButton(Rect r, Texture2D tex, string tip, bool active = true, Color? color = null)
        {
            bool over = active && Mouse.IsOver(r);
            if (over) Widgets.DrawBoxSolid(r, CardHover);
            var c = !active ? WithAlpha(Dim, 0.6f) : over ? Color.white : color ?? new Color(1f, 1f, 1f, 0.7f);
            if (tex != null) Icon(r.ContractedBy(Mathf.Max(2f, r.width * 0.12f)), tex, c);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!active) return false;
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }
    }
}
