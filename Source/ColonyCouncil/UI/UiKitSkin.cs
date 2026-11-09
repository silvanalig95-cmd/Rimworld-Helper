using System;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>How a button reads: the usual one, the one act a place is for (the accent), or a quiet one beside them.</summary>
    public enum ButtonKind
    {
        Normal,
        Primary,
        Quiet
    }

    /// <summary>
    /// Rimfolk's own look (docs/design/rimfolk-look.md, stage 1): the surfaces and controls a window in Rimfolk's frame draws
    /// with while the setting "Rimfolk's own look" is on, and the switch between it and the game's look. The surfaces are
    /// rounded rects drawn from a few white 9-slice textures (each radius, a fill, a one-unit edge and a tab's rounded top)
    /// and one soft shadow, all made once at load (SkinMath has the shapes); the controls are flat buttons (normal,
    /// primary, quiet) with hover and pressed states, a text field whose focus shows, a switch, a lit row with its accent
    /// edge, a tab that reads as a tab, and scrollbars that blend in. Every control draws the game's own look when Rimfolk's
    /// is off, exactly as Rimfolk drew it before, so a page calls one thing in either look. Nothing is made per frame.
    /// </summary>
    public static partial class UiKit
    {
        // ---------------- The look's switch ----------------

        /// <summary>Whether what is drawn now is in Rimfolk's own look (inside a window in Rimfolk's frame, the setting on).</summary>
        public static bool OwnLook => own;

        /// <summary>
        /// Starts drawing in Rimfolk's own look (a window's contents); returns how it was, for <see cref="EndOwn"/>. The
        /// scrollbars take the look's thumb and track while it lasts.
        /// </summary>
        public static bool BeginOwn()
        {
            bool was = own;
            own = true;
            if (!was) Scrollbars(true);
            return was;
        }

        /// <summary>Back to how it was before <see cref="BeginOwn"/> (always in a finally, so the game's look never stays changed).</summary>
        public static void EndOwn(bool was)
        {
            if (!was) Scrollbars(false);
            own = was;
        }

        /// <summary>
        /// Rimfolk's own look's colours only, without the scrollbars' styles: the on-screen panels, painted every frame and with
        /// no scrollbar of the game's (Hud/HudLook.cs). Returns how it was, for <see cref="EndOwnColours"/>.
        /// </summary>
        public static bool BeginOwnColours()
        {
            bool was = own;
            own = true;
            return was;
        }

        /// <summary>Back to how it was before <see cref="BeginOwnColours"/> (always in a finally).</summary>
        public static void EndOwnColours(bool was) => own = was;

        // ---------------- The textures, made once at load ----------------

        /// <summary>A white 9-slice texture and the share of it its corner takes.</summary>
        private sealed class Slice9
        {
            public Texture2D tex;
            public float margin, texMargin;
        }

        private static readonly Slice9[] fills = MakeSet(false, false);
        private static readonly Slice9[] edges = MakeSet(true, false);
        private static readonly Slice9[] tops = MakeSet(false, true);
        private static readonly Slice9[] topEdges = MakeSet(true, true);
        private static readonly Slice9 shadow = MakeShadow();

        private static readonly Slice9[] masks = MakeSet(false, false, true);

        private static Slice9[] MakeSet(bool ring, bool topOnly, bool mask = false)
        {
            var set = new Slice9[SkinMath.Radii.Length];
            for (int i = 0; i < set.Length; i++) set[i] = MakeRound(SkinMath.Radii[i], ring, topOnly, mask);
            return set;
        }

        private static Slice9 MakeRound(int radius, bool ring, bool topOnly, bool mask = false)
        {
            try
            {
                int s = SkinMath.TextureSide(radius), k = SkinMath.Supersample;
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float a = ring ? SkinMath.Ring(x, y, s, radius * k, k, topOnly) : SkinMath.Coverage(x, y, s, radius * k, 0f, topOnly);
                    // A mask is the corner's outside: drawn in the ground's colour, it rounds whatever is under it (a map's picture).
                    if (mask) a = 1f - a;
                    // Unity's rows run from the bottom; SkinMath's from the top.
                    px[(s - 1 - y) * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    name = "RimfolkRound" + radius + (ring ? "Edge" : "") + (topOnly ? "Top" : ""),
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.SetPixels32(px);
                tex.Apply(false, true);
                if (mask) tex.name += "Mask";
                return new Slice9 { tex = tex, margin = radius + 1f, texMargin = (radius + 1f) * k / s };
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't make an interface texture: " + e.Message);
                return null;
            }
        }

        private static Slice9 MakeShadow()
        {
            try
            {
                int reach = SkinMath.ShadowReach, radius = SkinMath.RadiusCard;
                int s = (reach + radius) * 2 + 4;
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(SkinMath.Shadow(x, y, s, reach, radius) * 255f));
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "RimfolkShadow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px);
                tex.Apply(false, true);
                return new Slice9 { tex = tex, margin = reach + radius, texMargin = (reach + radius) / (float)s };
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't make an interface texture: " + e.Message);
                return null;
            }
        }

        private static int RadiusIndex(int radius)
        {
            var radii = SkinMath.Radii;
            for (int i = 0; i < radii.Length; i++)
                if (radius <= radii[i])
                    return i;
            return radii.Length - 1;
        }

        /// <summary>A 9-slice texture laid into a rect in a colour (repaint only; nine draws).</summary>
        private static void Draw9(Rect r, Slice9 s, Color color)
        {
            if (Event.current.type != EventType.Repaint || r.width < 1f || r.height < 1f) return;
            if (s?.tex == null)
            {
                Widgets.DrawBoxSolid(r, color);
                return;
            }
            var old = GUI.color;
            GUI.color = color;
            for (int i = 0; i < 9; i++)
            {
                if (!SkinMath.Slice(i, r.x, r.y, r.width, r.height, s.margin, s.texMargin, out float x, out float y, out float w, out float h, out float u, out float v, out float uw,
                        out float vh))
                    continue;
                GUI.DrawTextureWithTexCoords(new Rect(x, y, w, h), s.tex, new Rect(u, v, uw, vh));
            }
            GUI.color = old;
        }

        // ---------------- Surfaces ----------------

        /// <summary>A rounded rect filled in a colour (radius in the interface's units: 4, 6, 8 or 10).</summary>
        public static void Round(Rect r, Color fill, int radius = SkinMath.RadiusMedium) => Draw9(r, fills[RadiusIndex(radius)], fill);

        /// <summary>A rounded rect's edge, one unit wide.</summary>
        public static void RoundEdge(Rect r, Color edge, int radius = SkinMath.RadiusMedium) => Draw9(r, edges[RadiusIndex(radius)], edge);

        /// <summary>A rounded rect filled, with its edge.</summary>
        public static void RoundBox(Rect r, Color fill, Color edge, int radius = SkinMath.RadiusMedium)
        {
            Round(r, fill, radius);
            if (edge.a > 0.001f) RoundEdge(r, edge, radius);
        }

        /// <summary>A rect with its top corners rounded (a tab), filled, with its edge on the top and sides.</summary>
        public static void RoundTop(Rect r, Color fill, Color edge, int radius = SkinMath.RadiusMedium)
        {
            int i = RadiusIndex(radius);
            Draw9(r, tops[i], fill);
            if (edge.a > 0.001f) Draw9(r, topEdges[i], edge);
        }

        /// <summary>
        /// Rounds the corners of what is drawn in <paramref name="r"/> (a picture, which can't be rounded itself) by drawing
        /// the corners' outside in the colour of the ground round it.
        /// </summary>
        public static void CornerMask(Rect r, Color ground, int radius = SkinMath.RadiusCard)
        {
            var s = masks[RadiusIndex(radius)];
            if (s?.tex == null || Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = ground;
            for (int i = 0; i < 9; i += i == 2 ? 4 : 2)
            {
                // The four corners only: 0, 2, 6 and 8.
                if (!SkinMath.Slice(i, r.x, r.y, r.width, r.height, s.margin, s.texMargin, out float x, out float y, out float w, out float h, out float u, out float v, out float uw,
                        out float vh))
                    continue;
                GUI.DrawTextureWithTexCoords(new Rect(x, y, w, h), s.tex, new Rect(u, v, uw, vh));
            }
            GUI.color = old;
        }

        /// <summary>A soft shadow under a rounded rect (drawn around it, reaching out from its edges).</summary>
        public static void SoftShadow(Rect r, float alpha = 1f)
        {
            if (shadow == null) return;
            float reach = SkinMath.ShadowReach;
            Draw9(new Rect(r.x - reach, r.y - reach + 2f, r.width + 2f * reach, r.height + 2f * reach), shadow, WithAlpha(ShadowInk, ShadowInk.a * alpha));
        }

        /// <summary>A card in Rimfolk's own look: its soft shadow, its raised fill, a faint edge.</summary>
        public static void OwnCard(Rect r, bool hover = false, bool selected = false, bool shadowed = false)
        {
            if (shadowed) SoftShadow(r, 0.6f);
            RoundBox(r, selected ? RaisedHover : hover ? RaisedHover : Raised, selected ? WithAlpha(Accent, 0.7f) : hover ? EdgeStrong : Edge, SkinMath.RadiusCard);
        }

        // ---------------- Buttons ----------------

        /// <summary>
        /// A button with its word: in Rimfolk's own look flat and rounded, lit under the mouse and darker held down (the
        /// primary in the accent, a quiet one without a fill until the mouse is on it), greyed when it can't be used; in the
        /// game's, the game's own button. Returns true when used.
        /// </summary>
        public static bool Button(Rect r, string label, ButtonKind kind = ButtonKind.Normal, bool active = true, string tip = null, GameFont? font = null, bool mouseoverSound = true)
        {
            if (!own)
            {
                // The game's button in the font in use, as the pages drew it before (or the one asked for).
                var oldFont = Text.Font;
                if (font.HasValue) Text.Font = font.Value;
                bool clicked = Widgets.ButtonText(r, label, true, mouseoverSound, active);
                Text.Font = oldFont;
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
                return clicked && active;
            }
            bool over = active && Mouse.IsOver(r);
            bool held = over && Input.GetMouseButton(0);
            Color fill, edge, ink;
            switch (kind)
            {
                case ButtonKind.Primary when active:
                    fill = held ? AccentPressed : over ? AccentHover : Accent;
                    edge = Color.clear;
                    ink = AccentInk;
                    break;
                case ButtonKind.Quiet:
                    fill = !active ? Color.clear : held ? Pressed : over ? RaisedHover : Color.clear;
                    edge = over ? Edge : Color.clear;
                    ink = !active ? Dim : over ? Ink : Faint;
                    break;
                default:
                    fill = !active ? WithAlpha(Raised, 0.5f) : held ? Pressed : over ? RaisedHover : Raised;
                    edge = !active ? WithAlpha(Edge, 0.6f) : over ? EdgeStrong : Edge;
                    ink = active ? Ink : Dim;
                    break;
            }
            if (fill.a > 0.001f || edge.a > 0.001f) RoundBox(r, fill, edge, SkinMath.RadiusMedium);
            var old = Text.Font;
            if (font.HasValue) Text.Font = font.Value;
            string s = Fit(label, r.width - 12f);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ink;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(new Rect(r.x + 4f, r.y + (held ? 1f : 0f), r.width - 8f, r.height), s);
            Text.WordWrap = wrap;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            else if (s.Length != (label ?? "").Length) TooltipHandler.TipRegion(r, label);
            if (!active) return false;
            if (mouseoverSound) MouseoverSounds.DoRegion(r);
            return Widgets.ButtonInvisible(r, false);
        }

        /// <summary>
        /// A small square button with an icon (the title bar's, the map's zoom): in Rimfolk's own look a rounded plate under
        /// the mouse; in the game's, the game's image button. Returns true when used.
        /// </summary>
        public static bool IconPlate(Rect r, Texture2D icon, string tip = null, bool lit = false, bool active = true)
        {
            if (!own)
            {
                bool clicked = active && Widgets.ButtonImage(r, icon);
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
                return clicked;
            }
            bool over = active && Mouse.IsOver(r);
            bool held = over && Input.GetMouseButton(0);
            if (lit) RoundBox(r, CardSelected, WithAlpha(Accent, 0.55f), SkinMath.RadiusMedium);
            else if (over) RoundBox(r, held ? Pressed : RaisedHover, Edge, SkinMath.RadiusMedium);
            var c = !active ? WithAlpha(Dim, 0.6f) : lit ? Accent : over ? Ink : Faint;
            if (icon != null) Icon(r.ContractedBy(Mathf.Max(3f, r.width * 0.2f)), icon, c);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!active) return false;
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        // ---------------- Rows ----------------

        /// <summary>
        /// The row that is chosen: in Rimfolk's own look a faint wash of the accent with the accent's edge down its left; in
        /// the game's, the lit box (with the gold edge where <paramref name="edge"/> asks, as before).
        /// </summary>
        public static void RowLit(Rect r, bool edge = false)
        {
            if (!own)
            {
                Widgets.DrawBoxSolid(r, CardSelected);
                if (edge) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), Gold);
                return;
            }
            Widgets.DrawBoxSolid(r, CardSelected);
            Round(new Rect(r.x, r.y + 2f, 3f, Mathf.Max(2f, r.height - 4f)), Accent, SkinMath.RadiusSmall);
        }

        /// <summary>A row under the mouse, lit (the game's highlight in its look, a soft lift in Rimfolk's).</summary>
        public static void RowHover(Rect r)
        {
            if (!own)
            {
                Widgets.DrawHighlightIfMouseover(r);
                return;
            }
            if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r, WithAlpha(Ink, 0.045f));
        }

        /// <summary>
        /// A row of a list that can be chosen (the Almanac's parts and pages, a picker's things): in the game's look the
        /// game's highlight, selected with the gold edge down its left, lit under the mouse; in Rimfolk's, the lit row with
        /// the accent's edge, or the soft lift under the mouse.
        /// </summary>
        public static void ListRow(Rect r, bool chosen)
        {
            if (!own)
            {
                if (chosen) Widgets.DrawHighlightSelected(r);
                else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
                if (chosen) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), Gold);
                return;
            }
            if (chosen) RowLit(r);
            else RowHover(r);
        }

        /// <summary>A section's ground (a list's, a page's): in Rimfolk's own look a rounded panel with its faint edge; in the game's, its menu section.</summary>
        public static void Section(Rect r)
        {
            if (!own)
            {
                Widgets.DrawMenuSection(r);
                return;
            }
            RoundBox(r, Panel, Edge, SkinMath.RadiusCard);
        }

        // ---------------- Text fields ----------------

        private static GUIStyle fieldTiny, fieldSmall, fieldMedium;

        /// <summary>The game's text field style for the font in use, without its background (the field is drawn under it), made once per font.</summary>
        private static GUIStyle FieldStyle()
        {
            switch (Text.Font)
            {
                case GameFont.Tiny: return fieldTiny ??= BareField();
                case GameFont.Medium: return fieldMedium ??= BareField();
                default: return fieldSmall ??= BareField();
            }
        }

        private static GUIStyle BareField()
        {
            var s = new GUIStyle(Text.CurTextFieldStyle);
            foreach (var st in new[] { s.normal, s.hover, s.active, s.focused, s.onNormal, s.onHover, s.onActive, s.onFocused })
            {
                st.background = null;
                st.textColor = own_Ink;
            }
            return s;
        }

        /// <summary>
        /// A text field: in Rimfolk's own look a rounded raised field whose edge takes the accent while it has the focus,
        /// with a quiet word in it while empty (<paramref name="placeholder"/>); in the game's, the game's own field.
        /// <paramref name="name"/> names the control (its focus). Returns the text.
        /// </summary>
        public static string TextField(Rect r, string text, string name, string placeholder = null, int maxLength = 0, Regex validator = null)
        {
            text ??= "";
            if (!own)
            {
                // The game's own field, by the same overload the page called before.
                if (name != null) GUI.SetNextControlName(name);
                if (validator != null) return Widgets.TextField(r, text, maxLength > 0 ? maxLength : int.MaxValue, validator);
                if (maxLength > 0) return Widgets.TextField(r, text, maxLength);
                return Widgets.TextField(r, text);
            }
            bool focused = name != null && GUI.GetNameOfFocusedControl() == name;
            bool over = Mouse.IsOver(r);
            RoundBox(r, focused ? Pressed : over ? RaisedHover : Raised, focused ? Accent : over ? EdgeStrong : Edge, SkinMath.RadiusMedium);
            if (focused) RoundEdge(r.ContractedBy(1f), WithAlpha(Accent, 0.35f), SkinMath.RadiusSmall);
            var inner = new Rect(r.x + 6f, r.y, r.width - 10f, r.height);
            if (text.Length == 0 && !focused && !placeholder.NullOrEmpty()) LabelFit(inner, placeholder, Dim, Text.Font);
            if (name != null) GUI.SetNextControlName(name);
            var oldColor = GUI.color;
            GUI.color = Color.white;
            string got = GUI.TextField(new Rect(inner.x - 2f, r.y + (r.height - Text.LineHeight) / 2f - 2f, inner.width + 2f, Text.LineHeight + 4f), text, FieldStyle());
            GUI.color = oldColor;
            if (maxLength > 0 && got.Length > maxLength) return text;
            return validator != null && got != text && !validator.IsMatch(got) ? text : got;
        }

        private static readonly string[] searchNames = new string[8];
        private static int searchCount;

        /// <summary>
        /// The game's quick search drawn in Rimfolk's own look (a field with the search icon and a clear button, Escape and a
        /// click elsewhere letting go of it, as the game's does), or the game's own widget in the game's look.
        /// </summary>
        public static void SearchField(Rect r, QuickSearchWidget w, Action changed, int slot = 0)
        {
            if (!own || w == null)
            {
                w?.OnGUI(r, changed);
                return;
            }
            slot = Mathf.Clamp(slot, 0, searchNames.Length - 1);
            string name = searchNames[slot] ??= "RimfolkSearch" + slot + "_" + searchCount++;
            bool focused = GUI.GetNameOfFocusedControl() == name;
            var e = Event.current;
            if (focused && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                UI.UnfocusCurrentControl();
                e.Use();
            }
            if (focused && e.type == EventType.MouseDown && !r.Contains(e.mousePosition)) UI.UnfocusCurrentControl();
            string before = w.filter.Text ?? "";
            var inner = new Rect(r.x + 22f, r.y, r.width - 22f - (before.Length > 0 ? 20f : 4f), r.height);
            // The field's frame round the icon too, then the text where the icon leaves room.
            RoundBox(r, focused ? Pressed : Mouse.IsOver(r) ? RaisedHover : Raised, focused ? Accent : Edge, SkinMath.RadiusMedium);
            Icon(new Rect(r.x + 6f, r.y + (r.height - 14f) / 2f, 14f, 14f), TexButton.Search, focused ? Accent : Faint);
            GUI.SetNextControlName(name);
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            string got = GUI.TextField(new Rect(inner.x + 2f, r.y + (r.height - Text.LineHeight) / 2f - 2f, inner.width - 2f, Text.LineHeight + 4f), before, FieldStyle());
            Text.Font = oldFont;
            if (got.Length > w.maxSearchTextLength) got = before;
            if (got != before)
            {
                w.filter.Text = got;
                changed?.Invoke();
            }
            if (before.Length > 0 && IconPlate(new Rect(r.xMax - 20f, r.y + (r.height - 18f) / 2f, 18f, 18f), TexButton.CloseXSmall))
            {
                w.filter.Text = "";
                SoundDefOf.CancelMode.PlayOneShotOnCamera();
                changed?.Invoke();
            }
        }

        // ---------------- A switch ----------------

        /// <summary>
        /// A switch with its word (on or off, the word always beside it): in Rimfolk's own look a small track with a knob,
        /// the accent when on; in the game's, the game's labelled checkbox. Returns true when it was flipped.
        /// </summary>
        public static bool Toggle(Rect r, string label, ref bool on, string tip = null, bool disabled = false, bool nearText = false)
        {
            bool was = on;
            if (!own)
            {
                // The game's own labelled checkbox, by the same overload the page called before.
                if (disabled || nearText) Widgets.CheckboxLabeled(r, label, ref on, disabled, null, null, nearText);
                else Widgets.CheckboxLabeled(r, label, ref on);
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
                return was != on;
            }
            bool over = !disabled && Mouse.IsOver(r);
            const float tw = 30f, th = 16f;
            // Near its word, the switch follows the word; otherwise it sits at the row's right, the word at its left.
            float labelW = Mathf.Max(0f, r.width - tw - 8f);
            if (nearText) labelW = Mathf.Min(labelW, KeptWidth(label ?? "", Text.Font) + 4f);
            var track = new Rect(nearText ? r.x + labelW + 6f : r.xMax - tw - 2f, r.y + (r.height - th) / 2f, tw, th);
            var trackFill = on ? (over ? AccentHover : Accent) : over ? EdgeStrong : Edge;
            Round(track, disabled ? WithAlpha(trackFill, 0.45f) : trackFill, SkinMath.RadiusCard);
            var knob = new Rect(on ? track.xMax - th + 2f : track.x + 2f, track.y + 2f, th - 4f, th - 4f);
            DiscFill(knob, disabled ? Dim : on ? AccentInk : Ink);
            LabelFit(new Rect(r.x, r.y, nearText ? labelW : Mathf.Max(0f, track.x - r.x - 6f), r.height), label, disabled ? Dim : over ? Ink : Faint);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!disabled && Widgets.ButtonInvisible(r, false))
            {
                on = !on;
                (on ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            }
            return was != on;
        }

        // ---------------- Tabs ----------------

        /// <summary>
        /// A tab that reads as a tab (Rimfolk's own look): the lit one a raised panel with rounded top corners joined to the
        /// panel under it, its word in ink with the accent's line along its top; the others quiet words that lift under the
        /// mouse. Returns true when clicked (a click on the lit one does nothing).
        /// </summary>
        public static bool OwnTab(Rect r, string label, Texture2D icon, bool lit, string tip = null, bool iconOnly = false)
        {
            bool over = Mouse.IsOver(r);
            if (lit)
            {
                RoundTop(new Rect(r.x, r.y, r.width, r.height + 2f), Panel, Edge, SkinMath.RadiusCard);
                Round(new Rect(r.x + 8f, r.y + 1f, r.width - 16f, 2f), Accent, SkinMath.RadiusSmall);
            }
            else if (over) RoundTop(new Rect(r.x + 1f, r.y + 3f, r.width - 2f, r.height - 3f), WithAlpha(RaisedHover, 0.7f), Color.clear, SkinMath.RadiusCard);
            var c = lit ? Ink : over ? Ink : Faint;
            var old = Text.Font;
            Text.Font = GameFont.Small;
            const float iconSize = 16f;
            string text = iconOnly ? "" : Fit(label, r.width - 20f - (icon != null ? iconSize + 6f : 0f));
            float tw = text.Length > 0 ? Text.CalcSize(text).x : 0f;
            float total = (icon != null ? iconSize + (tw > 0f ? 6f : 0f) : 0f) + tw;
            float x = r.center.x - total / 2f;
            if (icon != null)
            {
                Icon(new Rect(x, r.center.y - iconSize / 2f + 1f, iconSize, iconSize), icon, lit ? Accent : c);
                x += iconSize + 6f;
            }
            if (tw > 0f) LabelFit(new Rect(x, r.y + 1f, tw + 4f, r.height), text, c);
            Text.Font = old;
            string t = iconOnly ? label + (tip.NullOrEmpty() ? "" : "\n\n" + tip) : tip;
            if (!t.NullOrEmpty()) TooltipHandler.TipRegion(r, t);
            if (lit || !Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        // ---------------- Scrollbars that blend in ----------------

        private static Texture2D thumbV, thumbVHot, thumbH, thumbHHot, trackV, trackH;
        private static bool scrollFailed, swapped;
        private static readonly GUIStyle[] scrollStyles = new GUIStyle[4];
        private static readonly Texture2D[] savedBacks = new Texture2D[12];
        private static readonly int[] savedBorders = new int[16];

        /// <summary>
        /// A slim capsule along a 16 by 16 texture (the scrollbar's thumb or track), <paramref name="across"/> wide, in a
        /// colour, made once.
        /// </summary>
        private static Texture2D Bar(bool vertical, uint rgb, float alpha, float across)
        {
            const int s = 16;
            var px = new Color32[s * s];
            byte cr = (byte)Mathf.RoundToInt(SkinMath.R(rgb) * 255f), cg = (byte)Mathf.RoundToInt(SkinMath.G(rgb) * 255f), cb = (byte)Mathf.RoundToInt(SkinMath.B(rgb) * 255f);
            float r = across / 2f, mid = s / 2f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // The distance from the bar's middle line (from r to s - r along it), less its half width.
                float a = (vertical ? x : y) + 0.5f - mid;
                float b = (vertical ? y : x) + 0.5f;
                float along = b < r ? r - b : b > s - r ? b - (s - r) : 0f;
                float d = Mathf.Sqrt(a * a + along * along) - r;
                byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * alpha * 255f);
                px[y * s + x] = new Color32(cr, cg, cb, alphaByte);
            }
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "RimfolkScroll", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>
        /// The scrollbars in Rimfolk's own look while a window draws in it: the game's own scrollbar styles keep their sizes
        /// and take a slim rounded thumb on a faint track, put back as they were when the window is done. Any trouble and
        /// the game's scrollbars simply stay.
        /// </summary>
        private static void Scrollbars(bool on)
        {
            if (scrollFailed || on == swapped) return;
            try
            {
                var skin = GUI.skin;
                if (skin == null) return;
                scrollStyles[0] = skin.verticalScrollbar;
                scrollStyles[1] = skin.verticalScrollbarThumb;
                scrollStyles[2] = skin.horizontalScrollbar;
                scrollStyles[3] = skin.horizontalScrollbarThumb;
                if (on)
                {
                    if (thumbV == null)
                    {
                        thumbV = Bar(true, SkinMath.EdgeStrong, 1f, 6f);
                        thumbVHot = Bar(true, SkinMath.Faint, 0.85f, 6f);
                        thumbH = Bar(false, SkinMath.EdgeStrong, 1f, 6f);
                        thumbHHot = Bar(false, SkinMath.Faint, 0.85f, 6f);
                        trackV = Bar(true, SkinMath.WindowGround, 0.7f, 6f);
                        trackH = Bar(false, SkinMath.WindowGround, 0.7f, 6f);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        var st = scrollStyles[i];
                        if (st == null) continue;
                        savedBacks[i * 3] = st.normal.background;
                        savedBacks[i * 3 + 1] = st.hover.background;
                        savedBacks[i * 3 + 2] = st.active.background;
                        var b = st.border;
                        savedBorders[i * 4] = b.left;
                        savedBorders[i * 4 + 1] = b.right;
                        savedBorders[i * 4 + 2] = b.top;
                        savedBorders[i * 4 + 3] = b.bottom;
                        bool vertical = i < 2, thumb = (i & 1) == 1;
                        var normal = thumb ? vertical ? thumbV : thumbH : vertical ? trackV : trackH;
                        var hot = thumb ? vertical ? thumbVHot : thumbHHot : normal;
                        st.normal.background = normal;
                        st.hover.background = hot;
                        st.active.background = hot;
                        b.left = b.right = vertical ? 0 : 7;
                        b.top = b.bottom = vertical ? 7 : 0;
                    }
                    swapped = true;
                    return;
                }
                swapped = false;
                for (int i = 0; i < 4; i++)
                {
                    var st = scrollStyles[i];
                    if (st == null) continue;
                    st.normal.background = savedBacks[i * 3];
                    st.hover.background = savedBacks[i * 3 + 1];
                    st.active.background = savedBacks[i * 3 + 2];
                    var b = st.border;
                    b.left = savedBorders[i * 4];
                    b.right = savedBorders[i * 4 + 1];
                    b.top = savedBorders[i * 4 + 2];
                    b.bottom = savedBorders[i * 4 + 3];
                }
            }
            catch (Exception e)
            {
                scrollFailed = true;
                swapped = false;
                CouncilLog.ErrorOnce("the own look's scrollbars", e);
            }
        }
    }
}
