using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A panel in Rimfolk's own look (docs/design/rimfolk-look.md, stage 3, point 3): its card (the look's rounded fill and
    /// faint edge), a title row inside it with the panel's icon and name whose right end holds the bar's buttons while the
    /// mouse is on the panel (nothing pops out over it), or, compact, the bar over it as before in the look's quiet colours;
    /// the edge while the panels are arranged. Dragging, the menu, the scroll and the window's input work as in the classic
    /// panel (HudPanel.cs), which is drawn as it was whenever the look is off.
    /// </summary>
    public abstract partial class HudPanel
    {
        private float opacityOf = -1f;
        private string opacityText;

        private void ContentsLook(bool paint)
        {
            var oldFont = Text.Font;
            painting = paint;
            bool wasOwn = HudLook.Begin();
            try
            {
                mouseHere = paint && !aside && Find.WindowStack.GetWindowAt(UI.MousePositionOnUIInverted) == this;
                var bodyLocal = BodyLocal;
                float scale = HudMath.ClampScale(Scale);
                HudLook.Card(bodyLocal, St.opacity, hovered || dragging);
                var content = ContentLocal;
                if (scrolls) DrawScrolled(content, scale, paint, size.y - (inline ? TitleH : 0f));
                else DrawBody(content, scale);
                if (HudManager.editing) HudLook.Arranging(bodyLocal);
                if (inline) DrawTitleRow(new Rect(bodyLocal.x, bodyLocal.y, bodyLocal.width, TitleH), scale, showHeader);
                else if (showHeader) DrawBarLook(new Rect(0f, headerAbove ? 0f : body.height, body.width, HeaderH), scale);
                if (!paint) WindowExtras(false);
                // A click no row took: right for the menu, left to move the panel.
                var ev = Event.current;
                if (!paint && ev.type == EventType.MouseDown && Mouse.IsOver(bodyLocal))
                {
                    if (ev.button == 1)
                    {
                        OpenMenu();
                        ev.Use();
                    }
                    else if (ev.button == 0 && CanMove)
                    {
                        StartDrag();
                        ev.Use();
                    }
                }
            }
            catch (Exception e)
            {
                HudManager.Fail(id, e);
            }
            finally
            {
                HudLook.End(wasOwn);
                painting = false;
                Text.Font = oldFont;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The compact panel's bar over it, in the look's quiet colours: a rounded strip, the panel's icon, name and buttons.</summary>
        private void DrawBarLook(Rect bar, float s)
        {
            if (Event.current.type == EventType.Repaint) UiKit.Round(bar, HudLook.CardFill(1f, true), SkinMath.RadiusSmall);
            DrawTitleRow(bar, s, true);
        }

        /// <summary>
        /// The title row: the panel's icon and name in the heading's ink, and while the mouse is on the panel (or they are
        /// arranged) the bar's buttons at its right: the panel's own, compact or full, the background, the options, hide.
        /// The rest of the row moves the panel; a right-click opens its menu.
        /// </summary>
        private void DrawTitleRow(Rect row, float s, bool buttons)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            float ic = Mathf.Max(9f, HudLookMath.At(11f, s));
            float x = row.x + pad + 1f;
            HudLook.Icon(HudLook.IconAt(x, row.y, row.height, ic), HudIcons.For(id), UiKit.WithAlpha(UiKit.Heading, 0.9f));
            x += ic + HudLookMath.At(4f, s);
            float right = row.xMax - pad;
            if (buttons) right = TitleButtons(row, right, s);
            HudLook.Label(new Rect(x, row.y, Mathf.Max(0f, right - x - 2f), row.height), HeaderLabel, UiKit.Heading, GameFont.Tiny);
            var grip = new Rect(row.x, row.y, Mathf.Max(0f, right - row.x), row.height);
            var ev = Event.current;
            if (!painting && ev.type == EventType.MouseDown && Mouse.IsOver(grip))
            {
                if (ev.button == 0 && CanMove) StartDrag();
                else if (ev.button == 1) OpenMenu();
                ev.Use();
            }
        }

        /// <summary>The bar's buttons in the title row, right to left; returns where they begin.</summary>
        private float TitleButtons(Rect row, float right, float s)
        {
            float bs = Mathf.Max(10f, Mathf.Min(row.height - 2f, HudLookMath.At(12f, s)));
            var hide = HudLook.IconAt(right - bs, row.y, row.height, bs);
            if (ImageButtonLook(hide, TexButton.CloseXSmall, HudKit.T("CC_Pnl_HideTip")))
            {
                St.on = false;
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = hide.x - HudLookMath.At(4f, s);
            var gear = HudLook.IconAt(right - bs, row.y, row.height, bs);
            if (ImageButtonLook(gear, TexButton.OpenInspectSettings, HudKit.T("CC_Pnl_OptionsTip"))) Dialog_HudPanel.Open(id);
            right = gear.x - HudLookMath.At(4f, s);
            // The background: steps down, then back to full (its words made when it changes, not each frame).
            var st = St;
            if (opacityText == null || opacityOf != st.opacity)
            {
                opacityOf = st.opacity;
                opacityText = st.opacity.ToStringPercent();
            }
            float ow = HudLook.Width(opacityText, GameFont.Tiny) + 6f;
            var opR = new Rect(right - ow, row.y, ow, row.height);
            if (Over(opR)) HudLook.Hover(opR);
            HudLook.Label(opR, opacityText, Over(opR) ? UiKit.Ink : UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
            Tip(opR, HudKit.T("CC_Pnl_OpacityTip"));
            if (Clicked(opR))
            {
                st.opacity = st.opacity > 0.8f ? 0.7f : st.opacity > 0.5f ? 0.45f : st.opacity > 0.3f ? 0.25f : 1f;
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = opR.x - HudLookMath.At(3f, s);
            var cf = HudLook.IconAt(right - bs, row.y, row.height, bs);
            if (ImageButtonLook(cf, st.compact ? TexButton.Reveal : TexButton.Collapse, HudKit.T(st.compact ? "CC_Pnl_FullTip" : "CC_Pnl_CompactTip")))
            {
                st.compact = !st.compact;
                MarkDirty();
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = cf.x - HudLookMath.At(3f, s);
            return HeaderButtons(row, right);
        }

        /// <summary>A small picture button on whole pixels, faint until the mouse is on it; true when clicked.</summary>
        protected bool ImageButtonLook(Rect r, Texture2D tex, string tip)
        {
            HudLook.Icon(r, tex, Over(r) ? UiKit.Ink : UiKit.Faint);
            Tip(r, tip);
            return Clicked(r);
        }

        /// <summary>
        /// A colonist's name at the start of a row as a link (their card on a left click, a preview after a pause), its width
        /// measured once and kept; with <paramref name="alarm"/> it keeps the alarm's colour. True when the click was the name's.
        /// </summary>
        protected bool NameLook(Rect r, string name, ThingRef thing, GameFont font, Color? alarm = null)
        {
            if (name.NullOrEmpty()) return false;
            float w = Mathf.Min(HudLook.Width(name, font) + 2f, r.width);
            var hit = new Rect(r.x, r.y, w, r.height);
            if (thing.IsNone)
            {
                HudLook.Label(r, name, alarm ?? UiKit.Ink, font);
                return false;
            }
            bool over = Over(hit);
            if (alarm.HasValue) HudLook.Label(r, name, alarm.Value, font);
            else
            {
                HudLook.Label(r, name, over ? UiKit.LinkHot : UiKit.Link, font);
                if (Event.current.type == EventType.Repaint)
                {
                    float lh = HudKit.LineHeight(font);
                    LinkText.Underline(r.x, Mathf.Round(r.y + (r.height + lh) / 2f - (font == GameFont.Tiny ? 4f : 5f)), w - 3f, over);
                }
            }
            if (over) Previews.Hover(thing, hit);
            if (Painting || Event.current.button != 0 || !Clicked(hit)) return false;
            Cards.Click();
            Cards.Open(thing);
            return true;
        }

        /// <summary>The "and N more" line in the look: hover says why, a click shows them all (as the classic line).</summary>
        protected void MoreLook(Rect r, string text, int hiddenCount)
        {
            HudLook.Label(r, text, Over(r) ? UiKit.Ink : UiKit.Faint, GameFont.Tiny);
            Tip(r, HudKit.T("CC_Pnl_MoreLinesTip"));
            if (!Clicked(r) || hiddenCount <= 0) return;
            var st = St;
            if (st != null && st.maxRows > 0) st.maxRows += hiddenCount;
            MarkDirty();
            HudLayout.Save();
        }

        /// <summary>
        /// A small text button on the bar in the look (the board's order, the graph's days, the inventory's style); returns
        /// where it starts, and whether it was clicked (the panel then changes what it asks, gathers again and saves).
        /// </summary>
        protected float BarTextLook(Rect bar, float right, string label, string tip, out bool clicked)
        {
            float w = HudLook.Width(label, GameFont.Tiny) + 8f;
            var r = new Rect(right - w, bar.y, w, bar.height);
            bool over = Over(r);
            if (over) HudLook.Hover(r);
            HudLook.Label(r, label, over ? UiKit.Ink : UiKit.Gold, GameFont.Tiny, TextAnchor.MiddleCenter);
            Tip(r, tip);
            clicked = Clicked(r);
            if (clicked)
            {
                MarkDirty();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            return r.x - 3f;
        }
    }
}
