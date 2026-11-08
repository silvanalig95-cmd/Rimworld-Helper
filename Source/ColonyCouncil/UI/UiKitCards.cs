using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The interface design's components names, cards and the drawer are drawn with (docs/design/interface.md, "The
    /// look"; the tokens are in UI/UiKit.cs): a section heading with its count, fold and act, a thing chip (a link with
    /// its picture and a preview), a state chip (a dot and a word), an act button with why not, steps for what is under
    /// way, the count badge, an icon button. Measured once per width where they can be (their words through UiKit.Fit,
    /// KeptWidth and LinkText's layouts).
    /// </summary>
    public static partial class UiKit
    {
        // ---------------- Section heading ----------------

        /// <summary>Height of <see cref="SectionHeading"/>, with the space under it.</summary>
        public const float HeadingHeight = 26f;

        /// <summary>
        /// A section's heading (component 4): a fold caret (with <paramref name="folds"/>), its name, its count, an optional
        /// note on the right, and a line under it. Returns <see cref="HeadingHeight"/>; <paramref name="open"/> says whether
        /// to draw the section under it.
        /// </summary>
        public static float SectionHeading(float x, float y, float width, string label, int count, Folds folds, string key, out bool open, string right = null)
        {
            open = folds == null || folds.IsOpen(key);
            var hit = new Rect(x, y, width, 22f);
            if (folds != null) RowHover(hit);
            HeadingLine(x, y, width, label, count, right, null, Heading, folds != null, open);
            if (folds != null && Widgets.ButtonInvisible(hit, false))
            {
                folds.Toggle(key);
                (open ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
            }
            return HeadingHeight;
        }

        /// <summary>
        /// A section's heading with an act on its right ("Call an election"): a small button, greyed with why not as its
        /// tooltip. <paramref name="acted"/> says whether it was used. Returns <see cref="HeadingHeight"/>.
        /// </summary>
        public static float SectionHeading(float x, float y, float width, string label, int count, Folds folds, string key, out bool open, string act, string actWhyNot,
            out bool acted, string actTip = null)
        {
            acted = false;
            float aw = act.NullOrEmpty() ? 0f : Mathf.Clamp(KeptWidth(act, GameFont.Tiny) + 18f, 60f, 200f);
            if (aw > 0f && RowAct(new Rect(x + width - aw, y, aw, 20f), act, actWhyNot, actTip)) acted = true;
            float h = SectionHeading(x, y, width - (aw > 0f ? aw + 6f : 0f), label, count, folds, key, out open);
            if (aw > 0f) Divider(x + width - aw - 6f, y + 22f, aw + 6f);
            return h;
        }

        // ---------------- Chips ----------------

        /// <summary>Height of a chip.</summary>
        public const float ChipHeight = 20f;

        public static float StateChipWidth(string word, float maxWidth = 260f) => word.NullOrEmpty() ? 0f : Mathf.Min(maxWidth, KeptWidth(word, GameFont.Tiny) + 22f);

        /// <summary>A state (component 7): a dot in its colour and a word ("friendly", "thriving"). Returns the width used.</summary>
        public static float StateChip(float x, float y, string word, Color color, float maxWidth = 260f, string tip = null)
        {
            if (word.NullOrEmpty()) return 0f;
            float w = StateChipWidth(word, maxWidth);
            var r = new Rect(x, y, w, ChipHeight);
            if (own) RoundBox(r, Raised, Edge, SkinMath.RadiusCard);
            else Widgets.DrawBoxSolidWithOutline(r, CardFill, CardBorder, 1);
            DiscFill(new Rect(x + 6f, y + ChipHeight / 2f - 3.5f, 7f, 7f), color);
            LabelFit(new Rect(x + 17f, y, w - 19f, ChipHeight), word, Ink, GameFont.Tiny);
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
            if (own) RoundBox(r, over ? RaisedHover : Raised, over ? EdgeStrong : Edge, SkinMath.RadiusCard);
            else Widgets.DrawBoxSolidWithOutline(r, over ? CardHover : CardFill, CardBorder, 1);
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
                    DiscFill(r, PortraitBack);
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
            // Rimfolk's own look: the kit's flat button, greyed when it can't be done; the game's button in its look.
            bool clicked = own ? Button(r, label, ButtonKind.Normal, can) : Widgets.ButtonText(r, Fit(label, r.width - 10f), true, true, can);
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
        /// Something under way in steps (component 10: written, on the road, arrived, answered): a dot for each, the ones
        /// done filled in done's colour, the one it is at gold, each with its word; never a time left. Returns the height used.
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
                var c = i < at ? Done : i == at ? Gold : Dim;
                if (i <= at) DiscFill(dot, c);
                else RingDraw(dot, c);
                var old = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = i == at ? Ink : i < at ? Faint : Dim;
                Widgets.Label(new Rect(cx - cell / 2f, lineY + 7f, cell, 18f), Fit(words[i], cell - 2f));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = old;
            }
            return 32f;
        }

        /// <summary>
        /// The same steps on one line in small print, for a row (This week's Under way): a dot and a word for each, the
        /// ones done in done's colour, the one it is at lit, the rest quiet; cut where the width ends. Never a time left.
        /// </summary>
        public static void StepsLine(Rect r, string[] words, int at)
        {
            if (words == null || words.Length == 0) return;
            float x = r.x;
            float cy = r.y + r.height / 2f;
            for (int i = 0; i < words.Length; i++)
            {
                if (x + 12f > r.xMax) break;
                var c = i < at ? Done : i == at ? Gold : Dim;
                var dot = new Rect(x, cy - 3f, 6f, 6f);
                if (i <= at) DiscFill(dot, c);
                else RingDraw(dot, c);
                x += 9f;
                float w = KeptWidth(words[i], GameFont.Tiny) + 2f;
                float room = r.xMax - x;
                LabelFit(new Rect(x, r.y, Mathf.Min(w, room), r.height), words[i], i == at ? Ink : i < at ? Faint : Dim, GameFont.Tiny);
                x += w + 8f;
            }
        }

        // ---------------- Count badge ----------------

        private static readonly string[] counts = new string[100];

        /// <summary>
        /// A count on an icon's top right corner (the inbox in the corner, the council button): a small gold plate with the
        /// number, "99+" above ninety-nine.
        /// </summary>
        public static void CountBadge(Rect icon, int n)
        {
            if (n <= 0 || Event.current.type != EventType.Repaint) return;
            string s = n > 99 ? "99+" : counts[n] ??= n.ToString();
            float w = Mathf.Max(14f, KeptWidth(s, GameFont.Tiny) + 6f);
            var r = new Rect(icon.xMax - w + 5f, icon.y - 4f, w, 14f);
            if (own) Round(r, Accent, SkinMath.RadiusCard);
            else Widgets.DrawBoxSolidWithOutline(r, BadgeFill, WithAlpha(Gold, 0.9f), 1);
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = own ? AccentInk : Color.white;
            Widgets.Label(new Rect(r.x, r.y - 1f, r.width, r.height + 2f), s);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
        }

        // ---------------- Small buttons ----------------

        /// <summary>A square icon button, lit under the mouse, with its tooltip; returns true when clicked.</summary>
        public static bool IconButton(Rect r, Texture2D tex, string tip, bool active = true, Color? color = null)
        {
            bool over = active && Mouse.IsOver(r);
            if (over)
            {
                if (own) RoundBox(r, Input.GetMouseButton(0) ? Pressed : RaisedHover, Edge, SkinMath.RadiusMedium);
                else Widgets.DrawBoxSolid(r, CardHover);
            }
            var c = !active ? WithAlpha(Dim, 0.6f) : over ? Ink : color ?? WithAlpha(Ink, 0.7f);
            if (tex != null) Icon(r.ContractedBy(Mathf.Max(2f, r.width * 0.12f)), tex, c);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!active) return false;
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }
    }
}
