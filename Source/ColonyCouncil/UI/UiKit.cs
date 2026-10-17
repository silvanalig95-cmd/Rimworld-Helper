using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's interface icons: white glyphs in Textures/ColonyCouncil/Icons (drawn by Tools/make_icons.py), tinted
    /// when drawn like vanilla's. A missing file loads as null and is simply not drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Icons
    {
        // Sections
        public static readonly Texture2D Colony = Load("Colony");
        public static readonly Texture2D People = Load("People");
        public static readonly Texture2D Council = Load("Council");
        public static readonly Texture2D History = Load("History");
        public static readonly Texture2D Almanac = Load("Almanac");

        // Pages
        public static readonly Texture2D Overview = Load("Overview");
        public static readonly Texture2D Priorities = Load("Priorities");
        public static readonly Texture2D Moods = Load("Moods");
        public static readonly Texture2D World = Load("World");
        public static readonly Texture2D Rooms = Load("Rooms");
        public static readonly Texture2D Roster = Load("Roster");
        public static readonly Texture2D Bonds = Load("Bonds");
        public static readonly Texture2D Wishes = Load("Wishes");
        public static readonly Texture2D Decrees = Load("Decrees");
        public static readonly Texture2D Ambition = Load("Ambition");
        public static readonly Texture2D Ways = Load("Ways");
        public static readonly Texture2D Chronicle = Load("Chronicle");
        public static readonly Texture2D Dreams = Load("Dreams");
        public static readonly Texture2D Journals = Load("Journals");
        public static readonly Texture2D Week = Load("Week");
        public static readonly Texture2D Table = Load("Table");
        public static readonly Texture2D Dilemmas = Load("Dilemmas");
        public static readonly Texture2D Defenses = Load("Defenses");

        // Markers
        public static readonly Texture2D Pin = Load("Pin");
        public static readonly Texture2D Trust = Load("Trust");
        public static readonly Texture2D Office = Load("Office");
        public static readonly Texture2D LifeGoal = Load("LifeGoal");
        public static readonly Texture2D Secret = Load("Secret");
        public static readonly Texture2D Vendetta = Load("Vendetta");
        public static readonly Texture2D Nemesis = Load("Nemesis");
        public static readonly Texture2D Dream = Load("Dream");
        public static readonly Texture2D Journal = Load("Journal");
        public static readonly Texture2D WorldEvent = Load("WorldEvent");
        public static readonly Texture2D Raid = Load("Raid");
        public static readonly Texture2D Trader = Load("Trader");
        public static readonly Texture2D Visitor = Load("Visitor");
        public static readonly Texture2D Death = Load("Death");
        public static readonly Texture2D Birth = Load("Birth");
        public static readonly Texture2D Wedding = Load("Wedding");
        public static readonly Texture2D Decree = Load("Decree");
        public static readonly Texture2D Promise = Load("Promise");
        public static readonly Texture2D Warning = Load("Warning");
        public static readonly Texture2D Heart = Load("Heart");
        public static readonly Texture2D TrendUp = Load("TrendUp");
        public static readonly Texture2D TrendDown = Load("TrendDown");

        // Almanac categories
        public static readonly Texture2D CatBasics = Load("CatBasics");
        public static readonly Texture2D CatFood = Load("CatFood");
        public static readonly Texture2D CatDefense = Load("CatDefense");
        public static readonly Texture2D CatHealth = Load("CatHealth");
        public static readonly Texture2D CatManagement = Load("CatManagement");
        public static readonly Texture2D CatLowTech = Load("CatLowTech");
        public static readonly Texture2D CatRoomDesign = Load("CatRoomDesign");
        public static readonly Texture2D CatRoyalty = Load("CatRoyalty");
        public static readonly Texture2D CatIdeology = Load("CatIdeology");
        public static readonly Texture2D CatBiotech = Load("CatBiotech");
        public static readonly Texture2D CatAnomaly = Load("CatAnomaly");
        public static readonly Texture2D CatOdyssey = Load("CatOdyssey");
        public static readonly Texture2D CatNumbers = Load("CatNumbers");
        public static readonly Texture2D CatMods = Load("CatMods");

        private static Texture2D Load(string name) => ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/" + name, false);

        /// <summary>The icon for a kind of chronicle entry.</summary>
        public static Texture2D For(ChronicleKind k)
        {
            switch (k)
            {
                case ChronicleKind.WishMade:
                case ChronicleKind.WishGranted:
                case ChronicleKind.WishExpired:
                case ChronicleKind.WishDismissed:
                case ChronicleKind.Petition: return Wishes;
                case ChronicleKind.PersonalTouch: return Promise;
                case ChronicleKind.Meeting: return Council;
                case ChronicleKind.AmbitionChosen:
                case ChronicleKind.AmbitionAchieved:
                case ChronicleKind.AmbitionFailed:
                case ChronicleKind.AmbitionAbandoned: return Ambition;
                case ChronicleKind.Council: return Decree;
                case ChronicleKind.Bond: return Bonds;
                case ChronicleKind.Life: return LifeGoal;
                case ChronicleKind.Secret: return Secret;
                case ChronicleKind.Loss: return Death;
                case ChronicleKind.Dream: return Dream;
                case ChronicleKind.Stranger: return Visitor;
                case ChronicleKind.World: return WorldEvent;
                default: return Journal;
            }
        }

        /// <summary>The icon for a kind of bond.</summary>
        public static Texture2D For(TieKind k)
        {
            switch (k)
            {
                case TieKind.Friends: return Heart;
                case TieKind.Rivals: return Raid;
                case TieKind.Comrades: return CatDefense;
                case TieKind.Mentor: return Chronicle;
                default: return Bonds;
            }
        }

        /// <summary>The icon for a part of the almanac.</summary>
        public static Texture2D For(AlmanacCategory c)
        {
            switch (c)
            {
                case AlmanacCategory.Basics: return CatBasics;
                case AlmanacCategory.Council: return Council;
                case AlmanacCategory.Food: return CatFood;
                case AlmanacCategory.Rooms: return Rooms;
                case AlmanacCategory.Mood: return Moods;
                case AlmanacCategory.Defense: return CatDefense;
                case AlmanacCategory.Defending: return Defenses ?? CatDefense;
                case AlmanacCategory.Health: return CatHealth;
                case AlmanacCategory.Management: return CatManagement;
                case AlmanacCategory.LowTech: return CatLowTech;
                case AlmanacCategory.RoomDesign: return CatRoomDesign;
                case AlmanacCategory.Mistakes: return Warning;
                case AlmanacCategory.Royalty: return CatRoyalty;
                case AlmanacCategory.Ideology: return CatIdeology;
                case AlmanacCategory.Biotech: return CatBiotech;
                case AlmanacCategory.Anomaly: return CatAnomaly;
                case AlmanacCategory.Odyssey: return CatOdyssey;
                case AlmanacCategory.Numbers: return CatNumbers;
                case AlmanacCategory.Mods: return CatMods;
                default: return Almanac;
            }
        }
    }

    /// <summary>
    /// The building blocks every Rimfolk page is drawn with, so the pages share one look: the palette, spacing, and
    /// helpers for page headers, cards, pills, meters, portraits and chips. Everything here stays in vanilla's visual
    /// language (dark panels, white and gray text, small accents, tinted white icons).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UiKit
    {
        // ---------------- Palette ----------------

        public static readonly Color Gold = new Color(1f, 0.83f, 0.47f);
        public static readonly Color Good = new Color(0.52f, 0.85f, 0.5f);
        public static readonly Color Bad = new Color(0.93f, 0.45f, 0.38f);
        public static readonly Color Warn = new Color(0.95f, 0.66f, 0.28f);
        public static readonly Color Info = new Color(0.56f, 0.73f, 0.96f);
        public static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color Dim = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color Neutral = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color Quote = new Color(1f, 0.93f, 0.75f);
        public static readonly Color HeaderColor = new Color(0.95f, 0.85f, 0.6f);
        public static readonly Color DreamColor = new Color(0.8f, 0.72f, 0.96f);
        public static readonly Color LossColor = new Color(0.85f, 0.7f, 0.65f);
        public static readonly Color StrangerColor = new Color(0.75f, 0.85f, 0.7f);
        public static readonly Color WorldColor = new Color(0.72f, 0.8f, 0.95f);

        // Surfaces: all translucent, so they sit on any vanilla panel.
        public static readonly Color CardFill = new Color(1f, 1f, 1f, 0.035f);
        public static readonly Color CardHover = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color CardSelected = new Color(1f, 1f, 1f, 0.11f);
        public static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color ZebraFill = new Color(1f, 1f, 1f, 0.03f);
        public static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.13f);
        public static readonly Color MeterBack = new Color(0f, 0f, 0f, 0.4f);
        private static readonly Color PortraitBack = new Color(0.12f, 0.13f, 0.14f, 1f);

        // ---------------- Spacing ----------------

        public const float Gap = 6f;
        public const float Pad = 8f;
        /// <summary>Height of <see cref="PageHeader"/>, with the space below it.</summary>
        public const float HeaderHeight = 56f;
        /// <summary>Height of <see cref="SectionTitle"/>, with the space below it.</summary>
        public const float SectionHeight = 32f;
        public const float RowHeight = 26f;
        /// <summary>Height of <see cref="Meter"/>: a line of tiny text and the bar under it.</summary>
        public const float MeterHeight = 28f;

        /// <summary>Pills use the tiny font; languages without one fall back to the small font, which needs more room.</summary>
        public static float PillHeight => Text.TinyFontSupported ? 18f : 22f;

        // ---------------- Colors by meaning ----------------

        /// <summary>Gray for neutral, greener with trust, redder with bitterness.</summary>
        public static Color TrustColor(float trust) =>
            trust >= 0f ? Color.Lerp(Neutral, Good, Mathf.Clamp01(trust / 100f)) : Color.Lerp(Neutral, Bad, Mathf.Clamp01(-trust / 100f));

        public static Color TieColor(TieKind k)
        {
            switch (k)
            {
                case TieKind.Friends: return new Color(0.45f, 0.82f, 0.45f);
                case TieKind.Rivals: return new Color(0.92f, 0.42f, 0.35f);
                case TieKind.Comrades: return new Color(0.48f, 0.64f, 0.95f);
                case TieKind.Mentor: return new Color(0.93f, 0.77f, 0.36f);
                default: return Neutral;
            }
        }

        public static Color ReactionColor(DecreeReaction r)
        {
            switch (r)
            {
                case DecreeReaction.Love: return new Color(0.45f, 0.86f, 0.45f);
                case DecreeReaction.Like: return new Color(0.66f, 0.83f, 0.56f);
                case DecreeReaction.Dislike: return new Color(0.93f, 0.66f, 0.4f);
                case DecreeReaction.Hate: return new Color(0.93f, 0.38f, 0.32f);
                default: return Neutral;
            }
        }

        public static Color ChronicleColor(ChronicleKind k)
        {
            switch (k)
            {
                case ChronicleKind.WishGranted:
                case ChronicleKind.AmbitionAchieved:
                case ChronicleKind.PersonalTouch: return new Color(0.55f, 0.9f, 0.55f);
                case ChronicleKind.WishExpired:
                case ChronicleKind.AmbitionFailed:
                case ChronicleKind.AmbitionAbandoned:
                case ChronicleKind.WishDismissed: return Faint;
                case ChronicleKind.Meeting:
                case ChronicleKind.AmbitionChosen:
                case ChronicleKind.Council: return new Color(0.45f, 0.7f, 0.95f);
                case ChronicleKind.Dream: return Quote;
                case ChronicleKind.Loss: return LossColor;
                case ChronicleKind.Stranger: return StrangerColor;
                case ChronicleKind.World: return WorldColor;
                default: return Color.white;
            }
        }

        /// <summary>Green with plenty of time left, orange when it's getting short, red at the end.</summary>
        public static Color Urgency(float fractionLeft) => fractionLeft > 0.5f ? Good : fractionLeft > 0.25f ? Warn : Bad;

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------- Text ----------------

        private static readonly Dictionary<(string, int, int), string> fitted = new Dictionary<(string, int, int), string>();
        private static readonly Dictionary<int, string> durations = new Dictionary<int, string>();

        /// <summary>The text cut with "..." to fit the width in the current font. Remembered, so it's cheap every frame.</summary>
        public static string Fit(string text, float width)
        {
            if (text.NullOrEmpty()) return text ?? "";
            if (width <= 4f) return "";
            var key = (text, (int)Text.Font, Mathf.RoundToInt(width));
            if (fitted.TryGetValue(key, out var s)) return s;
            if (fitted.Count > 4000) fitted.Clear();
            s = Text.CalcSize(text).x <= width ? text : text.Truncate(width);
            fitted[key] = s;
            return s;
        }

        /// <summary>"3.5 days" or "5 hours", remembered by the hour so meters don't format text every frame.</summary>
        public static string Duration(int ticks)
        {
            int hours = Mathf.Max(0, ticks) / GenDate.TicksPerHour;
            if (durations.TryGetValue(hours, out var s)) return s;
            if (durations.Count > 2000) durations.Clear();
            s = TextUtil.DaysString(Mathf.Max(1, hours) * GenDate.TicksPerHour);
            durations[hours] = s;
            return s;
        }

        /// <summary>One line of text, cut to fit, with the whole text as a tooltip when it had to be cut.</summary>
        public static void LabelFit(Rect r, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (text.NullOrEmpty()) return;
            var oldFont = Text.Font;
            Text.Font = font;
            string s = Fit(text, r.width);
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(r, s);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
            if (s.Length != text.Length) TooltipHandler.TipRegion(r, text);
        }

        /// <summary>Wrapped text in a color; returns its height.</summary>
        public static float Paragraph(float x, float y, float width, string text, Color color, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            var oldFont = Text.Font;
            Text.Font = font;
            float h = Text.CalcHeight(text, width);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, width, h), text);
            GUI.color = Color.white;
            Text.Font = oldFont;
            return h;
        }

        // ---------------- Icons and lines ----------------

        public static void Icon(Rect r, Texture2D tex, Color color)
        {
            if (tex == null) return;
            GUI.color = color;
            GUI.DrawTexture(r, tex);
            GUI.color = Color.white;
        }

        public static void Divider(float x, float y, float width) => Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), DividerColor);

        public static void Zebra(Rect r, int index)
        {
            if ((index & 1) == 1) Widgets.DrawBoxSolid(r, ZebraFill);
        }

        // ---------------- Page and section headers ----------------

        /// <summary>
        /// The top of a page: an icon, the title, a one-line subtitle and, on the right, an optional stat. Keeps
        /// <paramref name="reserveRight"/> free at the right for the page's own buttons. Returns <see cref="HeaderHeight"/>.
        /// </summary>
        public static float PageHeader(Rect rect, Texture2D icon, string title, string subtitle, string stat = null, Color? statColor = null,
            float reserveRight = 0f, string tip = null)
        {
            float x = rect.x;
            if (icon != null)
            {
                Icon(new Rect(rect.x + 1f, rect.y + 6f, 32f, 32f), icon, Gold);
                x += 42f;
            }
            float right = Mathf.Max(x, rect.xMax - reserveRight);
            float statW = 0f;
            if (!stat.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                statW = Mathf.Min(Text.CalcSize(stat).x + 6f, (right - x) * 0.45f);
                LabelFit(new Rect(right - statW, rect.y + 4f, statW, 26f), stat, statColor ?? Gold, GameFont.Small, TextAnchor.MiddleRight);
                statW += 10f;
            }
            LabelFit(new Rect(x, rect.y + 1f, Mathf.Max(0f, right - statW - x), 32f), title, Color.white, GameFont.Medium);
            if (!subtitle.NullOrEmpty()) LabelFit(new Rect(x, rect.y + 29f, Mathf.Max(0f, right - x), 22f), subtitle, Faint);
            float lineY = rect.y + HeaderHeight - 5f;
            Divider(rect.x, lineY, rect.width);
            Widgets.DrawBoxSolid(new Rect(rect.x, lineY - 1f, Mathf.Min(rect.width, 34f), 2f), WithAlpha(Gold, 0.8f));
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(rect.x, rect.y, Mathf.Max(0f, right - rect.x), HeaderHeight - 6f), tip);
            Text.Font = GameFont.Small;
            return HeaderHeight;
        }

        /// <summary>A small heading inside a page: icon, text, an optional note on the right, and a line under it. Returns <see cref="SectionHeight"/>.</summary>
        public static float SectionTitle(float x, float y, float width, string text, Texture2D icon = null, Color? color = null, string right = null)
        {
            var c = color ?? HeaderColor;
            float tx = x;
            if (icon != null)
            {
                Icon(new Rect(x, y + 3f, 18f, 18f), icon, c);
                tx += 24f;
            }
            float rw = 0f;
            if (!right.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                rw = Mathf.Min(Text.CalcSize(right).x + 4f, width * 0.4f);
                LabelFit(new Rect(x + width - rw, y, rw, 24f), right, Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                rw += 8f;
            }
            LabelFit(new Rect(tx, y, Mathf.Max(0f, width - (tx - x) - rw), 24f), text, c);
            Divider(x, y + 25f, width);
            Text.Font = GameFont.Small;
            return SectionHeight;
        }

        /// <summary>
        /// A <see cref="SectionTitle"/> that folds its section away when clicked, with a small arrow in front saying which
        /// way it is. <paramref name="open"/> says whether to draw the section under it; the key names the section in
        /// <paramref name="folds"/> (without folds it's a plain heading, always open). Returns <see cref="SectionHeight"/>.
        /// </summary>
        public static float FoldTitle(float x, float y, float width, string text, Folds folds, string key, out bool open, Texture2D icon = null,
            Color? color = null, string right = null)
        {
            open = folds == null || folds.IsOpen(key);
            if (folds == null) return SectionTitle(x, y, width, text, icon, color, right);
            const float arrow = 15f;
            var hit = new Rect(x, y, width, 26f);
            if (Mouse.IsOver(hit)) Widgets.DrawHighlight(hit);
            Icon(new Rect(x + 1f, y + 7f, 10f, 10f), open ? TexButton.Collapse : TexButton.Reveal, WithAlpha(color ?? HeaderColor, 0.8f));
            SectionTitle(x + arrow, y, width - arrow, text, icon, color, right);
            Divider(x, y + 25f, arrow);
            TooltipHandler.TipRegion(hit, open ? "CC_UI_FoldTip".Translate() : "CC_UI_UnfoldTip".Translate());
            if (Widgets.ButtonInvisible(hit, false))
            {
                folds.Toggle(key);
                (open ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
            }
            return SectionHeight;
        }

        /// <summary>An icon and one line, centered: what a list shows when there's nothing in it. Returns the height used.</summary>
        public static float EmptyState(float x, float y, float width, Texture2D icon, string text, float height = 110f)
        {
            Text.Font = GameFont.Small;
            const float iconSize = 36f;
            float th = text.NullOrEmpty() ? 0f : Text.CalcHeight(text, Mathf.Max(20f, width - 20f));
            float total = (icon != null ? iconSize + 8f : 0f) + th;
            height = Mathf.Max(height, total + 16f);
            float top = y + (height - total) / 2f;
            if (icon != null) Icon(new Rect(x + width / 2f - iconSize / 2f, top, iconSize, iconSize), icon, new Color(1f, 1f, 1f, 0.22f));
            if (th > 0f)
            {
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Faint;
                Widgets.Label(new Rect(x + 10f, top + (icon != null ? iconSize + 8f : 0f), Mathf.Max(20f, width - 20f), th), text);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            return height;
        }

        public static void EmptyState(Rect r, Texture2D icon, string text) => EmptyState(r.x, r.y, r.width, icon, text, r.height);

        // ---------------- Cards, tabs and pills ----------------

        /// <summary>A subtle filled box with a border, lighter under the mouse; an optional colored stripe down the left edge.</summary>
        public static void Card(Rect r, Color? accent = null, bool selected = false, bool hover = true)
        {
            var fill = selected ? CardSelected : hover && Mouse.IsOver(r) ? CardHover : CardFill;
            Widgets.DrawBoxSolidWithOutline(r, fill, CardBorder, 1);
            if (accent.HasValue) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), accent.Value);
        }

        public static float TabButtonWidth(string label, Texture2D icon)
        {
            Text.Font = GameFont.Small;
            return Text.CalcSize(label).x + 24f + (icon != null ? 23f : 0f);
        }

        /// <summary>A flat tab: icon and label, a gold line under it when active. Returns true when clicked.</summary>
        public static bool TabButton(Rect r, string label, Texture2D icon, bool active, string tip = null)
        {
            if (active) Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.1f));
            Widgets.DrawHighlightIfMouseover(r);
            var c = active ? Color.white : Faint;
            Text.Font = GameFont.Small;
            float iconSize = Mathf.Min(18f, r.height - 8f);
            bool withIcon = icon != null && r.width >= iconSize + 40f;
            float avail = r.width - 12f - (withIcon ? iconSize + 5f : 0f);
            string text = Fit(label, avail);
            float tw = Text.CalcSize(text).x;
            float sx = r.center.x - ((withIcon ? iconSize + 5f : 0f) + tw) / 2f;
            if (withIcon)
            {
                Icon(new Rect(sx, r.center.y - iconSize / 2f, iconSize, iconSize), icon, active ? Gold : c);
                sx += iconSize + 5f;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = c;
            Widgets.Label(new Rect(sx, r.y, tw + 6f, r.height), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (active) Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 2f), Gold);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            else if (text.Length != label.Length) TooltipHandler.TipRegion(r, label);
            return Widgets.ButtonInvisible(r);
        }

        /// <summary>A short colored tag ("Promised", "Last call").</summary>
        public struct PillSpec
        {
            public string label;
            public Color color;
            public Texture2D icon;
            public string tip;

            public PillSpec(string label, Color color, Texture2D icon = null, string tip = null)
            {
                this.label = label;
                this.color = color;
                this.icon = icon;
                this.tip = tip;
            }
        }

        public static float PillWidth(string label, Texture2D icon = null)
        {
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            float w = Text.CalcSize(label ?? "").x + 12f + (icon != null ? 14f : 0f);
            Text.Font = old;
            return w;
        }

        /// <summary>Draws a pill; returns its width.</summary>
        public static float Pill(float x, float y, string label, Color color, Texture2D icon = null, string tip = null, float maxWidth = 9999f)
        {
            float w = Mathf.Min(PillWidth(label, icon), maxWidth);
            var r = new Rect(x, y, w, PillHeight);
            Widgets.DrawBoxSolidWithOutline(r, WithAlpha(color, 0.15f), WithAlpha(color, 0.5f), 1);
            float tx = r.x + 6f;
            if (icon != null)
            {
                Icon(new Rect(tx - 1f, r.center.y - 6f, 12f, 12f), icon, color);
                tx += 14f;
            }
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = color;
            Widgets.Label(new Rect(tx, r.y, Mathf.Max(0f, r.xMax - tx), r.height), Fit(label, r.xMax - tx - 2f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            return w;
        }

        /// <summary>Pills in a row, wrapping when the width runs out. Returns the height used (0 for none). With draw off, only measures.</summary>
        public static float Pills(float x, float y, float width, List<PillSpec> pills, bool draw = true)
        {
            if (pills == null || pills.Count == 0) return 0f;
            float cx = x, cy = y, h = PillHeight;
            for (int i = 0; i < pills.Count; i++)
            {
                var p = pills[i];
                float w = Mathf.Min(PillWidth(p.label, p.icon), width);
                if (cx > x && cx + w > x + width)
                {
                    cx = x;
                    cy += h + 4f;
                }
                if (draw) Pill(cx, cy, p.label, p.color, p.icon, p.tip, width);
                cx += w + 4f;
            }
            return cy - y + h;
        }

        // ---------------- Meters ----------------

        /// <summary>A thin bar with a dark track.</summary>
        public static void Bar(Rect r, float fill, Color color)
        {
            Widgets.DrawBoxSolidWithOutline(r, MeterBack, new Color(1f, 1f, 1f, 0.1f), 1);
            fill = Mathf.Clamp01(fill);
            if (fill <= 0f) return;
            var f = new Rect(r.x + 1f, r.y + 1f, Mathf.Max(1f, (r.width - 2f) * fill), Mathf.Max(1f, r.height - 2f));
            Widgets.DrawBoxSolid(f, WithAlpha(color, 0.85f));
            Widgets.DrawBoxSolid(new Rect(f.x, f.y, f.width, 1f), new Color(1f, 1f, 1f, 0.2f));
        }

        /// <summary>
        /// A labelled bar: the label on the left, the value on the right, the bar underneath. Meant for a rect
        /// <see cref="MeterHeight"/> tall.
        /// </summary>
        public static void Meter(Rect r, float fill, Color color, string label, string value = null, Texture2D icon = null, string tip = null)
        {
            float lx = r.x;
            if (icon != null)
            {
                Icon(new Rect(r.x, r.y + 2f, 14f, 14f), icon, color);
                lx += 18f;
            }
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            float vw = value.NullOrEmpty() ? 0f : Mathf.Min(Text.CalcSize(value).x + 4f, r.width * 0.6f);
            if (vw > 0f) LabelFit(new Rect(r.xMax - vw, r.y, vw, 18f), value, color, GameFont.Tiny, TextAnchor.MiddleRight);
            if (!label.NullOrEmpty()) LabelFit(new Rect(lx, r.y, Mathf.Max(0f, r.xMax - vw - 4f - lx), 18f), label, Faint, GameFont.Tiny);
            Text.Font = old;
            Bar(new Rect(r.x, r.y + 19f, r.width, Mathf.Max(4f, Mathf.Min(8f, r.height - 20f))), fill, color);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
        }

        /// <summary>Time left as a meter: the bar is the share of the time still left, colored by how close it is.</summary>
        public static void Countdown(Rect r, int ticksLeft, int totalTicks, string label, string tip = null)
        {
            float left = totalTicks > 0 ? Mathf.Clamp01(ticksLeft / (float)totalTicks) : 0f;
            Meter(r, left, Urgency(left), label, Duration(ticksLeft), null, tip);
        }

        // ---------------- Portraits and chips ----------------

        // Head and shoulders: the portrait camera centered a little above the pawn's middle, zoomed in.
        private static readonly Vector3 PortraitOffset = new Vector3(0f, 0f, 0.28f);
        private const float PortraitZoom = 1.75f;
        private static readonly Texture2D Disc = MakeCircle(64, 0f);
        private static readonly Texture2D Ring = MakeCircle(64, 0.8f);
        private static readonly HashSet<int> portraitFailed = new HashSet<int>();

        /// <summary>A colonist's head and shoulders in a circle, with a colored ring around it.</summary>
        public static void Portrait(Rect r, Pawn p, Color ring, float alpha = 1f)
        {
            if (p == null || Event.current.type != EventType.Repaint) return;
            GUI.color = WithAlpha(PortraitBack, alpha);
            if (Disc != null) GUI.DrawTexture(r, Disc);
            else Widgets.DrawBoxSolid(r, WithAlpha(PortraitBack, alpha));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float inset = Mathf.Max(1f, Mathf.Round(r.width * 0.07f));
            var inner = r.ContractedBy(inset);
            Texture tex = null;
            if (!portraitFailed.Contains(p.thingIDNumber))
            {
                try
                {
                    tex = PortraitsCache.Get(p, new Vector2(Mathf.Round(inner.width), Mathf.Round(inner.height)), Rot4.South, PortraitOffset, PortraitZoom);
                }
                catch (Exception)
                {
                    portraitFailed.Add(p.thingIDNumber);
                    tex = null;
                }
            }
            if (tex != null) DrawInCircle(inner, tex);
            else Widgets.ThingIcon(inner, p);
            GUI.color = new Color(ring.r, ring.g, ring.b, ring.a * alpha);
            if (Ring != null) GUI.DrawTexture(r, Ring);
            else Widgets.DrawBox(r, 2);
            GUI.color = Color.white;
        }

        /// <summary>A filled circle (a glow behind a portrait, a dot at the end of a line).</summary>
        public static void DiscFill(Rect r, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (Disc == null)
            {
                Widgets.DrawBoxSolid(r, color);
                return;
            }
            GUI.color = color;
            GUI.DrawTexture(r, Disc);
            GUI.color = Color.white;
        }

        /// <summary>An icon in a small dark disc with a colored ring: the nodes of a timeline.</summary>
        public static void Badge(Rect r, Texture2D icon, Color color)
        {
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = PortraitBack;
                if (Disc != null) GUI.DrawTexture(r, Disc);
                else Widgets.DrawBoxSolid(r, PortraitBack);
                GUI.color = WithAlpha(color, 0.85f);
                if (Ring != null) GUI.DrawTexture(r, Ring);
                else Widgets.DrawBox(r, 1);
                GUI.color = Color.white;
            }
            if (icon != null) Icon(r.ContractedBy(Mathf.Round(r.width * 0.24f)), icon, color);
        }

        /// <summary>A square texture drawn as a circle, in horizontal strips each as wide as the circle is there.</summary>
        private static void DrawInCircle(Rect r, Texture tex)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(r.height / 2.5f), 6, 28);
            float h = r.height / n;
            float rad = r.width / 2f;
            float cx = r.center.x, cy = r.center.y;
            for (int i = 0; i < n; i++)
            {
                float y0 = r.y + i * h, y1 = y0 + h;
                // The narrower end of the strip, so nothing pokes out of the circle.
                float dy = Mathf.Max(Mathf.Abs(y0 - cy), Mathf.Abs(y1 - cy));
                if (dy >= rad) continue;
                float half = Mathf.Sqrt(rad * rad - dy * dy);
                var strip = new Rect(cx - half, y0, half * 2f, h);
                // Texture coordinates start at the bottom.
                var uv = new Rect((strip.x - r.x) / r.width, 1f - (y1 - r.y) / r.height, strip.width / r.width, h / r.height);
                GUI.DrawTextureWithTexCoords(strip, tex, uv);
            }
        }

        private static Texture2D MakeCircle(int size, float innerFraction)
        {
            try
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = innerFraction > 0f ? "RimfolkRing" : "RimfolkDisc",
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var px = new Color32[size * size];
                float c = size / 2f, ro = size / 2f - 1f, ri = ro * innerFraction;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(ro - d + 0.5f);
                        if (innerFraction > 0f) a *= Mathf.Clamp01(d - ri + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                return tex;
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't make an interface texture: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// A colonist's portrait with a ring in their trust color, and their name; clicking it opens their page in the
        /// People roster. Returns the width used.
        /// </summary>
        public static float PawnChip(float x, float y, Pawn p, float size = 24f, bool showName = true, float maxWidth = 160f, Color? ring = null,
            Func<string> tip = null, bool clickable = true)
        {
            if (p == null) return 0f;
            var comp = CouncilGameComponent.Instance;
            var rc = ring ?? (comp != null ? TrustColor(comp.standings.TrustOf(p)) : Neutral);
            float w = size;
            string name = null;
            var font = size >= 22f ? GameFont.Small : GameFont.Tiny;
            if (showName && maxWidth > size + 24f)
            {
                Text.Font = font;
                name = Fit(p.LabelShort, maxWidth - size - 6f);
                w += 5f + Text.CalcSize(name).x;
            }
            var r = new Rect(x, y, Mathf.Min(w, Mathf.Max(size, maxWidth)), size);
            if (clickable) Widgets.DrawHighlightIfMouseover(r);
            Portrait(new Rect(x, y, size, size), p, rc);
            if (name != null)
            {
                Text.Font = font;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x + size + 5f, y, r.width - size - 5f, size), name);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            var pawn = p;
            TooltipHandler.TipRegion(r, tip ?? (() => ChipTip(pawn, clickable)), p.thingIDNumber ^ 0x51C4);
            if (clickable && Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenPerson(p);
            return r.width;
        }

        private static string ChipTip(Pawn p, bool clickable)
        {
            var comp = CouncilGameComponent.Instance;
            string tip = p.LabelShortCap;
            if (comp != null && p.IsColonist)
                tip += "\n" + "CC_Story_Trust".Translate(("CC_Trust_" + PawnStanding.LevelFor(comp.standings.TrustOf(p))).Translate()).Resolve();
            if (clickable) tip += "\n\n" + "CC_UI_OpenPage".Translate();
            return tip;
        }

        /// <summary>A thing's icon and label. Returns the width used.</summary>
        public static float ThingChip(float x, float y, ThingDef def, string label = null, float size = 24f, float maxWidth = 200f)
        {
            if (def == null) return 0f;
            label = label ?? def.LabelCap.ToString();
            Text.Font = GameFont.Small;
            string text = Fit(label, maxWidth - size - 6f);
            float w = Mathf.Min(maxWidth, size + 5f + Text.CalcSize(text).x);
            var r = new Rect(x, y, w, size);
            try
            {
                if (Widgets.CanDrawIconFor(def)) Widgets.DefIcon(new Rect(x, y, size, size), def);
            }
            catch (Exception)
            {
                // A modded thing without a usable graphic: just the name.
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + size + 5f, y, w - size - 5f, size), text);
            Text.Anchor = TextAnchor.UpperLeft;
            var d = def;
            TooltipHandler.TipRegion(r, () => d.LabelCap + (d.description.NullOrEmpty() ? "" : "\n\n" + d.description), d.shortHash ^ 0x7C41);
            return w;
        }

        public static float FactionChipWidth(Faction f, float height = 22f, float maxWidth = 220f)
        {
            if (f == null) return 0f;
            var old = Text.Font;
            Text.Font = GameFont.Small;
            string name = Fit(f.Name ?? f.def?.label ?? "?", maxWidth - height - 12f);
            float w = Mathf.Min(maxWidth, height + 10f + Text.CalcSize(name).x);
            Text.Font = old;
            return w;
        }

        /// <summary>A faction's icon in its color, and its name. Returns the width used.</summary>
        public static float FactionChip(float x, float y, Faction f, float height = 22f, float maxWidth = 220f)
        {
            if (f == null) return 0f;
            Texture2D icon = null;
            var color = Color.white;
            try
            {
                icon = f.def?.FactionIcon;
                color = f.Color;
            }
            catch (Exception)
            {
                icon = null;
            }
            if (icon == BaseContent.BadTex) icon = null;
            Text.Font = GameFont.Small;
            string name = Fit(f.Name ?? f.def?.label ?? "?", maxWidth - height - 12f);
            float w = Mathf.Min(maxWidth, height + 10f + Text.CalcSize(name).x);
            var r = new Rect(x, y, w, height);
            Widgets.DrawBoxSolidWithOutline(r, WithAlpha(color, 0.1f), WithAlpha(color, 0.35f), 1);
            if (icon != null) Icon(new Rect(x + 3f, y + 2f, height - 4f, height - 4f), icon, color);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + height + 4f, y, w - height - 6f, height), name);
            Text.Anchor = TextAnchor.UpperLeft;
            var faction = f;
            TooltipHandler.TipRegion(r, () => FactionTip(faction), f.loadID ^ 0x3F11);
            return w;
        }

        private static string FactionTip(Faction f)
        {
            try
            {
                string tip = f.Name;
                if (f.IsPlayer) return tip;
                tip += "\n" + f.PlayerRelationKind.GetLabelCap();
                if (f.def != null && !f.def.permanentEnemy) tip += " (" + f.PlayerGoodwill.ToStringWithSign() + ")";
                return tip;
            }
            catch (Exception)
            {
                return f.Name ?? "";
            }
        }
    }
}
