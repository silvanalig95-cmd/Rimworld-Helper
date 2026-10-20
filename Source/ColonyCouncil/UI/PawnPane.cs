using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One line of a <see cref="PaneSection"/>: text (wrapping), with an optional face or icon in front of it and a short
    /// note on the right (with a small bar under it, for a bond's strength). Its height is measured once per width.
    /// </summary>
    public class PaneRow
    {
        public string text;
        public string tip;
        /// <summary>Or a tooltip built only when it shows, with an id of its own.</summary>
        public Func<string> tipFn;
        public int tipId;
        public Color color = Color.white;
        /// <summary>A small portrait in front of the text, ringed in <see cref="accent"/>.</summary>
        public Pawn face;
        /// <summary>Or an icon there, tinted in <see cref="accent"/>.</summary>
        public Texture2D icon;
        public Color accent = UiKit.Neutral;
        /// <summary>A few words on the right, tiny, in <see cref="accent"/>.</summary>
        public string right;
        /// <summary>A small bar under the note on the right (0..1), or below 0 for none.</summary>
        public float bar = -1f;
        /// <summary>Clicking the row selects this colonist (and jumps to them).</summary>
        public Pawn select;
        public GameFont font = GameFont.Small;

        /// <summary>The height last measured, and at what width.</summary>
        public float measuredWidth = -1f, height;
    }

    /// <summary>A section of a pawn page: a heading that folds, then rows, a character section, or something drawn of its own.</summary>
    public class PaneSection
    {
        /// <summary>Names the section in the folds, the same in every language.</summary>
        public string key;
        public string header;
        public Texture2D icon;
        public Color? color;
        /// <summary>A few words on the right of the heading (a count).</summary>
        public string right;
        public readonly List<PaneRow> rows = new List<PaneRow>();
        /// <summary>A part of the character page (Persona/CharacterView.cs), drawn after any rows.</summary>
        public CharacterSection character;
        /// <summary>Drawn first, at (x, y) and w wide; returns the height used.</summary>
        public Func<float, float, float, float> custom;

        public bool Empty => rows.Count == 0 && (character == null || character.Empty) && custom == null;

        public PaneRow Add(string text, Color? color = null, string tip = null)
        {
            if (text.NullOrEmpty()) return null;
            var r = new PaneRow { text = text, tip = tip, color = color ?? Color.white };
            rows.Add(r);
            return r;
        }
    }

    /// <summary>
    /// Draws pawn pages made of folding sections, the way the Story tab looks: the Social tab's Rimfolk column and the
    /// Culture tab. The page's model is built by its owner a couple of times a second; this only lays it out.
    /// </summary>
    public static class PawnPane
    {
        public const float FaceSize = 22f;
        private const float Indent = 8f, RightW = 70f;

        /// <summary>The sections in a scroll view filling <paramref name="outRect"/>; an empty page shows <paramref name="empty"/>.</summary>
        public static void Draw(Rect outRect, List<PaneSection> sections, ref Vector2 scroll, ref float height, Folds folds, string empty, Pawn p)
        {
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                if (sections == null || sections.Count == 0)
                {
                    y += UiKit.Paragraph(0f, y, w, empty, UiKit.Faint) + 8f;
                }
                else
                {
                    foreach (var s in sections)
                    {
                        y += UiKit.FoldTitle(0f, y, w, s.header, folds, s.key, out bool open, s.icon, s.color ?? UiKit.HeaderColor, s.right) - 4f;
                        if (!open)
                        {
                            y += 6f;
                            continue;
                        }
                        y += DrawBody(Indent, y, w - Indent, s, p) + 8f;
                    }
                }
                height = Mathf.Max(y, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>A section's body under its heading; returns the height used.</summary>
        public static float DrawBody(float x, float y, float w, PaneSection s, Pawn p)
        {
            float start = y;
            if (s.custom != null)
            {
                try
                {
                    y += s.custom(x, y, w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pawn page: " + s.key, e);
                }
            }
            foreach (var r in s.rows) y += DrawRow(x, y, w, r) + 2f;
            if (s.character != null && !s.character.Empty)
                y += CharacterView.DrawSection(new Rect(x, y, w, 0f), p, s.character);
            Text.Font = GameFont.Small;
            return y - start;
        }

        /// <summary>One row; returns its height.</summary>
        public static float DrawRow(float x, float y, float w, PaneRow r)
        {
            bool lead = r.face != null || r.icon != null;
            float tx = x + (lead ? FaceSize + 6f : 0f);
            bool side = !r.right.NullOrEmpty() || r.bar >= 0f;
            float tw = Mathf.Max(40f, x + w - tx - (side ? RightW + 6f : 0f));
            var oldFont = Text.Font;
            Text.Font = r.font;
            if (r.measuredWidth != tw)
            {
                r.measuredWidth = tw;
                r.height = Mathf.Max(lead ? FaceSize + 2f : 0f, Text.CalcHeight(r.text ?? "", tw));
            }
            float h = r.height;
            var rect = new Rect(x, y, w, h);
            if ((r.select != null || !r.tip.NullOrEmpty() || r.tipFn != null) && Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            if (r.face != null) UiKit.Portrait(new Rect(x, y + 1f, FaceSize, FaceSize), r.face, r.accent);
            else if (r.icon != null) UiKit.Icon(new Rect(x + 3f, y + 4f, FaceSize - 6f, FaceSize - 6f), r.icon, r.accent);
            GUI.color = r.color;
            Text.Anchor = lead && h <= FaceSize + 3f ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
            Widgets.Label(new Rect(tx, y, tw, h), r.text ?? "");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = oldFont;
            if (side)
            {
                float rx = x + w - RightW;
                bool both = !r.right.NullOrEmpty() && r.bar >= 0f;
                if (!r.right.NullOrEmpty())
                    UiKit.LabelFit(new Rect(rx, y + (both ? 0f : 2f), RightW, both ? 14f : 18f), r.right, r.accent, GameFont.Tiny, TextAnchor.MiddleRight);
                if (r.bar >= 0f) UiKit.Bar(new Rect(rx + 8f, y + (both ? 15f : 8f), RightW - 8f, 6f), r.bar, r.accent);
            }
            if (r.tipFn != null) TooltipHandler.TipRegion(rect, r.tipFn, r.tipId);
            else if (!r.tip.NullOrEmpty()) TooltipHandler.TipRegion(rect, r.tip);
            if (r.select != null && !r.select.Dead && Widgets.ButtonInvisible(rect, false)) CameraJumper.TryJumpAndSelect(r.select);
            return h;
        }
    }
}
