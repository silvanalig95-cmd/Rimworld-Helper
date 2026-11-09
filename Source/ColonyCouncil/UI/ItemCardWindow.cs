using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's card for one item (the apparel card, and the item card for food, drink, drugs and medicine; the weapon
    /// card's parts are the same, so it can move onto this base): a framed window, one of a kind at a time, about a thing in
    /// hand or a kind in a stuff, for a chosen colonist (the one who holds it, else the last chosen, else one picked from the map). It
    /// keeps the scroll, the colonist and the title, and draws the parts every item card shares in the kit, in Rimfolk's
    /// look or the game's: the head (the item's picture large in its quality's frame, the rating's badge, its band, how it
    /// is rated, the button to all the game's stats, the line in plain words), bars by situation, effects counted or named,
    /// rows of key numbers, and the colonist's heading with its button to choose another.
    /// </summary>
    public abstract class ItemCardWindow : FramedWindow
    {
        protected Thing thing;
        protected ThingDef def, stuff;
        protected Pawn who;
        private static Pawn lastWho;
        private Vector2 scroll;
        private float viewH = 1400f;

        public override Vector2 InitialSize => new Vector2(580f, Mathf.Min(800f, UI.screenHeight - 120f));

        protected ItemCardWindow(string key) : base(key)
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = SoundDefOf.TabClose;
        }

        /// <summary>Shows a thing in hand (its def and stuff), or a kind in a stuff, for a colonist (null: the last chosen).</summary>
        protected void Show(Thing t, ThingDef d, ThingDef s, Pawn forWho)
        {
            thing = t;
            def = t?.def ?? d;
            stuff = t != null ? t.Stuff : s;
            Choose(forWho);
        }

        /// <summary>The colonist the card speaks for; a humanlike one is remembered for the next card.</summary>
        protected void Choose(Pawn p)
        {
            scroll = Vector2.zero;
            if (p != null && p.RaceProps.Humanlike) lastWho = p;
            who = p ?? lastWho;
            OnChosen();
        }

        /// <summary>The card's view for a colonist is worked out again.</summary>
        protected virtual void OnChosen()
        {
        }

        /// <summary>The title when there is neither a thing nor a kind.</summary>
        protected abstract string Untitled { get; }

        protected override string FrameTitle =>
            thing != null ? thing.LabelCapNoCount : def != null ? GenLabel.ThingLabel(def, stuff).CapitalizeFirst() : Untitled;

        protected override Texture2D FrameIcon => Icons.CatNumbers;

        /// <summary>Whether there is something to show (the rating reads it); else <see cref="NotRated"/> is said.</summary>
        protected abstract bool Rated { get; }

        protected abstract string NotRated { get; }

        /// <summary>A short name for errors.</summary>
        protected abstract string What { get; }

        /// <summary>The card's body in a scroll view of this width; returns its height.</summary>
        protected abstract float Body(float width);

        protected static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        protected static string F2(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        protected override void DoFramedContents(Rect inRect)
        {
            if ((thing != null && thing.Destroyed) || def == null)
            {
                Close();
                return;
            }
            try
            {
                float top = TitleTop(inRect, FrameTitle);
                var outer = new Rect(inRect.x, top, inRect.width, inRect.yMax - top);
                if (!Rated)
                {
                    UiKit.EmptyState(outer, Icons.CatNumbers, NotRated);
                    return;
                }
                if (who != null && (who.Destroyed || who.Dead)) who = null;
                var inner = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(viewH, outer.height - 4f));
                Widgets.BeginScrollView(outer, ref scroll, inner);
                float y = Body(inner.width);
                Widgets.EndScrollView();
                viewH = y + 12f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Rimfolk's " + What, e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------------------------------------------------------- the parts every item card shares

        /// <summary>What the head says beside the picture.</summary>
        protected sealed class HeadFacts
        {
            /// <summary>
            /// The rating's badge; with <see cref="NoBadge"/> there is none (the item card for food or medicine), and the band
            /// starts beside the picture in the heading's ink.
            /// </summary>
            public float Rating;
            public bool NoBadge;
            public Func<string> RatingTip;
            /// <summary>Its band and role, in the rating's colour.</summary>
            public string Band = "";
            /// <summary>How it is rated (for whom, quality and stuff), quiet; its tooltip when rough.</summary>
            public string RatedFor = "", RoughTip;
            /// <summary>Another quiet line under it (a shooter's and a melee fighter's ratings), or null.</summary>
            public string Second;
            public string Plain = "";
            public string GameLabel = "", GameTip = "";
            public Action OpenGameCard;
        }

        /// <summary>
        /// The head: the item's own picture large in a tile framed in its quality's colour (UiKit.ItemPortrait: a thing as it
        /// looks, a kind in its stuff), and beside it the badge, its band, how it is rated, the button to all the game's stats,
        /// and the line in plain words. Returns the y under it.
        /// </summary>
        protected float Head(float w, float y, HeadFacts h)
        {
            float side = ItemPortraitMath.CardTile(w);
            var pic = new Rect(0f, y, side, side);
            UiKit.ItemPortrait(pic, thing, def, stuff, true);
            float x0 = pic.xMax + 12f;
            float x = x0;
            if (!h.NoBadge)
            {
                var badge = new Rect(x0, y, 66f, 46f);
                WeaponUi.Badge(badge, h.Rating, h.RatingTip, 81726, GameFont.Medium);
                x = badge.xMax + 10f;
            }
            float gw = Mathf.Clamp(UiKit.KeptWidth(h.GameLabel, GameFont.Small) + 24f, 110f, 200f);
            if (h.OpenGameCard != null && UiKit.ActButton(new Rect(w - gw, y, gw, 24f), h.GameLabel, null, h.GameTip)) h.OpenGameCard();
            UiKit.LabelFit(new Rect(x, y, w - x - gw - 6f, 24f), h.Band, h.NoBadge ? UiKit.Heading : WeaponUi.RatingColour(h.Rating));
            var sub = new Rect(x, y + 24f, w - x, 20f);
            UiKit.LabelFit(sub, h.RatedFor, UiKit.Faint, GameFont.Tiny);
            if (h.RoughTip != null) TooltipHandler.TipRegion(sub, h.RoughTip);
            float ty = y + 54f;
            if (!h.Second.NullOrEmpty())
            {
                UiKit.LabelFit(new Rect(x, y + 40f, w - x, 18f), h.Second, UiKit.Faint, GameFont.Tiny);
                ty = y + 60f;
            }
            if (!h.Plain.NullOrEmpty()) ty += UiKit.Paragraph(x0, ty, w - x0, h.Plain, UiKit.Ink) + 4f;
            return Mathf.Max(pic.yMax, ty) + 10f;
        }

        /// <summary>Sub-ratings as meters in two columns: each its label, its value in its band's colour and its meaning on the tooltip.</summary>
        protected float Meters(float w, float y, IList<(string label, float value, string tip)> meters)
        {
            float colW = (w - 12f) / 2f;
            for (int i = 0; i < meters.Count; i++)
            {
                var (label, v, tip) = meters[i];
                var cell = new Rect(i % 2 == 0 ? 0f : colW + 12f, y + (i / 2) * (UiKit.MeterHeight + 4f), colW, UiKit.MeterHeight);
                UiKit.Meter(cell, v / 10f, WeaponUi.RatingColour(v), label, F1(v), null, tip);
            }
            return y + ((meters.Count + 1) / 2) * (UiKit.MeterHeight + 4f);
        }

        /// <summary>One bar a situation, best first: best for in good ink, weak when in bad, each with its meaning.</summary>
        protected float Situations(float w, float y, IEnumerable<(string name, float value, bool best, bool weak, string tip)> rows, string bestWord, string weakWord)
        {
            foreach (var (name, value, best, weak, tip) in rows.OrderByDescending(r => r.value))
            {
                var row = new Rect(0f, y, w, 20f);
                UiKit.LabelFit(new Rect(0f, y, 170f, 20f), name, best ? UiKit.Good : weak ? UiKit.Bad : UiKit.Ink, GameFont.Small);
                var bar = new Rect(176f, y + 6f, w - 176f - 120f, 8f);
                UiKit.Bar(bar, value / 10f, WeaponUi.RatingColour(value));
                string note = best ? bestWord : weak ? weakWord : "";
                UiKit.LabelFit(new Rect(bar.xMax + 6f, y, 114f, 20f), F1(value) + (note.Length > 0 ? " · " + note : ""), best ? UiKit.Good : weak ? UiKit.Bad : UiKit.Faint, GameFont.Tiny);
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(row, tip);
                y += 22f;
            }
            return y;
        }

        /// <summary>Effects, each with its sign's mark and whether the rating counts it.</summary>
        protected float Effects(float w, float y, IEnumerable<(string label, bool counted, int sign)> effects, string countedWord, string namedWord)
        {
            foreach (var (label, counted, sign) in effects)
            {
                var c = sign > 0 ? UiKit.Good : sign < 0 ? UiKit.Bad : UiKit.Faint;
                UiKit.LabelFit(new Rect(0f, y, 14f, 20f), sign > 0 ? "+" : sign < 0 ? "-" : "·", c);
                float h = UiKit.Paragraph(16f, y + 1f, w - 16f - 96f, label.CapitalizeFirst(), UiKit.Ink, GameFont.Small);
                UiKit.LabelFit(new Rect(w - 92f, y, 92f, 20f), counted ? countedWord : namedWord, counted ? UiKit.Faint : UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
                y += Mathf.Max(20f, h) + 2f;
            }
            return y;
        }

        /// <summary>Rows of a label and its value (the key numbers).</summary>
        protected float Rows(float w, float y, IEnumerable<(string label, string value)> rows, float labelW = 150f)
        {
            foreach (var (label, value) in rows)
            {
                UiKit.LabelFit(new Rect(0f, y, labelW, 20f), label, UiKit.Faint, GameFont.Small);
                float h = UiKit.Paragraph(labelW + 6f, y + 1f, w - labelW - 6f, value, UiKit.Ink, GameFont.Small);
                y += Mathf.Max(20f, h) + 1f;
            }
            return y;
        }

        /// <summary>The colonist's heading with its button to choose another; returns the y under it.</summary>
        protected float ColonistHeading(float w, float y, string title, string chooseLabel, string chooseTip, string noneOnMap)
        {
            y += UiKit.SectionTitle(0f, y, w - 150f, title);
            if (UiKit.Button(new Rect(w - 144f, y - UiKit.SectionHeight + 1f, 144f, 24f), chooseLabel, ButtonKind.Normal, true, chooseTip)) Pick(noneOnMap);
            return y;
        }

        /// <summary>The map's free colonists to choose from.</summary>
        private void Pick(string noneOnMap)
        {
            var opts = new List<FloatMenuOption>();
            var pawns = Find.CurrentMap?.mapPawns?.FreeColonists?.Where(p => p.RaceProps.Humanlike).OrderBy(p => p.LabelShort).ToList() ?? new List<Pawn>();
            foreach (var p in pawns)
            {
                var pick = p;
                opts.Add(new FloatMenuOption(p.LabelShortCap, () => Choose(pick)));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption(noneOnMap, null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
