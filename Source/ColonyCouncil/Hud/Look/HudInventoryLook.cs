using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The inventory in Rimfolk's own look, at the classic's sizes: the game's icon of each item on whole pixels, its name,
    /// its count, the change since yesterday as a small arrow with the number, and for a weapon the colony keeps the weapon
    /// rating's badge (the best one in storage, in its band's colour as the weapon card draws it); the essentials' meals with
    /// their days of food as a bar against the warning line. The grouped style's headers as quiet bands; the grid's tiles with
    /// the change in a corner and the rating along the foot. Every word, count and rating made when the panel gathers; the
    /// ratings read again every few seconds at most (Weapons/WeaponLive.cs keeps them).
    /// </summary>
    public partial class HudInventory
    {
        private string styleTip;

        private static readonly Dictionary<int, string> countTexts = new Dictionary<int, string>(), shortTexts = new Dictionary<int, string>();
        private static readonly Dictionary<ThingDef, (float at, float rating, string text, string tip)> ratings = new Dictionary<ThingDef, (float, float, string, string)>();

        private static string CountText(int n, bool shortForm)
        {
            var d = shortForm ? shortTexts : countTexts;
            if (d.TryGetValue(n, out var t)) return t;
            if (d.Count > 3000) d.Clear();
            t = shortForm ? HudInventoryMath.ShortCount(n) : n.ToString();
            d[n] = t;
            return t;
        }

        private void RefreshLook()
        {
            string next = Styles[(Array.IndexOf(Styles, Style) + 1) % Styles.Length];
            styleTip = "CC_Pnl_I_StyleTip".Translate(StyleLabel(next)).Resolve();
            bool grid = Style == StyleGrid;
            float now = Time.realtimeSinceStartup;
            foreach (var it in shown)
            {
                it.label = it.e.label.CapitalizeFirst();
                it.countText = CountText(it.e.count, grid);
                it.changeText = it.e.hasChange && it.e.change != 0 ? HudInventoryMath.Signed(it.e.change) : null;
                var item = it;
                it.tip = () => TipFor(item);
                Rating(it, now);
            }
            foreach (var g in groups) g.totalText = CountText(g.total >= 0 ? g.total : g.items.Count, false);
        }

        /// <summary>A weapon's rating: the best one of the kind lying in storage, as the weapon rating reads it (kept a few seconds).</summary>
        private void Rating(Item it, float now)
        {
            it.rating = -1f;
            var def = it.def;
            if (it.cat != null || def == null || !def.IsWeapon || !WeaponLive.On || map == null) return;
            if (!ratings.TryGetValue(def, out var kept) || now - kept.at > 5f || now < kept.at)
            {
                float best = -1f;
                try
                {
                    if (WeaponLive.Rateable(def))
                        foreach (var t in map.listerThings.ThingsOfDef(def))
                        {
                            if (t == null || !t.Spawned || !t.IsInAnyStorage()) continue;
                            var r = WeaponLive.Of(t);
                            if (r?.Rating != null && r.Rating.Overall > best) best = r.Rating.Overall;
                        }
                }
                catch (Exception e)
                {
                    best = -1f;
                    CouncilLog.ErrorOnce("inventory: a weapon's rating", e);
                }
                string text = best >= 0f ? WeaponForMath.RatingText(best) : null;
                string tip = text != null ? "CC_Pnl_Look_RatingTip".Translate(text.Named("RATING")).Resolve() : null;
                if (ratings.Count > 300) ratings.Clear();
                kept = (now, best, text, tip);
                ratings[def] = kept;
            }
            if (kept.rating < 0f) return;
            it.rating = kept.rating;
            it.ratingText = kept.text;
            it.ratingTip = kept.tip;
            it.ratingColor = WeaponUi.RatingColour(kept.rating);
        }

        private void DrawLook(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            float y = r.y + pad + SearchH(s);
            // The search box itself is the window's (it takes the keyboard); painted here as a quiet copy.
            if (Painting && St.Shows(PartSearch))
            {
                var sr = new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, Mathf.Round(24f * s));
                if (Event.current.type == EventType.Repaint) UiKit.RoundBox(sr, UiKit.MeterBack, UiKit.Edge, SkinMath.RadiusSmall);
                string text = search.filter.Text;
                HudLook.Label(sr.ContractedBy(6f, 0f), text.NullOrEmpty() ? HudKit.T("CC_Pnl_I_SearchHint") : text, text.NullOrEmpty() ? UiKit.Faint : UiKit.Ink, GameFont.Small);
            }
            if (shown.Count == 0)
            {
                HudLook.Label(new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(20f * s)), emptyText, UiKit.Faint, HudKit.Minor(s));
                return;
            }
            switch (Style)
            {
                case StyleGrid:
                    y = DrawGridLook(r, y, s);
                    break;
                case StyleGrouped:
                    y = DrawGroupedLook(r, y, s);
                    break;
                default:
                    for (int i = 0; i < shown.Count; i++) DrawRowLook(new Rect(r.x + 2f, y + i * RowH(s), r.width - 4f, RowH(s)), shown[i], s, i);
                    y += shown.Count * RowH(s);
                    break;
            }
            if (moreText != null) MoreLook(new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(16f * s)), moreText, hidden);
        }

        private void IconLook(Rect r, Item it)
        {
            if (it.def == null || Event.current.type != EventType.Repaint) return;
            r = HudLook.Snap(r);
            try
            {
                Widgets.ThingIcon(r, it.def);
            }
            catch (Exception)
            {
                if (it.def.uiIcon != null)
                {
                    GUI.color = it.def.uiIconColor;
                    GUI.DrawTexture(r, it.def.uiIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>The change since yesterday: a small arrow up or down in its colour and the number beside it; returns where it starts.</summary>
        private static float ChangeLook(Rect rr, float right, Item it, float s)
        {
            float cw = Mathf.Round(34f * s);
            var c = it.e.change > 0 ? Gain : Loss;
            float aw = Mathf.Max(6f, HudLookMath.At(7f, s));
            float nw = Mathf.Min(HudLook.Width(it.changeText, GameFont.Tiny) + 2f, cw - aw - 2f);
            HudLook.Label(new Rect(right - nw, rr.y, nw, rr.height), it.changeText, c, GameFont.Tiny, TextAnchor.MiddleRight);
            HudLook.Icon(HudLook.IconAt(right - nw - aw - 1f, rr.y, rr.height, aw), it.e.change > 0 ? Icons.TrendUp : Icons.TrendDown, c);
            return right - cw - 2f;
        }

        private void DrawRowLook(Rect rr, Item it, float s, int index)
        {
            bool big = Style == StyleEssentials;
            if (Event.current.type == EventType.Repaint)
            {
                if (it.e.pinned) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Gold, 0.07f));
                else UiKit.Zebra(rr, index);
            }
            if (Over(rr)) HudLook.Hover(rr);
            float icon = Mathf.Min(rr.height - 2f, Mathf.Round((big ? 22f : 18f) * s));
            float x = rr.x + Mathf.Round(3f * s);
            IconLook(new Rect(x, rr.y + (rr.height - icon) / 2f, icon, icon), it);
            x += icon + Mathf.Round(5f * s);
            float right = rr.xMax - Mathf.Round(3f * s);
            var font = big ? HudKit.Main(Mathf.Max(s, 1f)) : HudKit.Main(s);
            if (St.Shows(PartChange) && it.changeText != null) right = ChangeLook(rr, right, it, s);
            else if (St.Shows(PartChange) && Style == StyleClassic) right -= Mathf.Round(36f * s);
            if (it.rating >= 0f)
            {
                float bw = HudLookMath.At(26f, s), bh = Mathf.Min(rr.height - 4f, HudLookMath.At(14f, s));
                var badge = HudLook.Snap(new Rect(right - bw, rr.y + (rr.height - bh) / 2f, bw, bh));
                HudLook.Badge(badge, it.ratingColor, it.ratingText);
                Tip(badge, it.ratingTip);
                right -= bw + HudLookMath.At(4f, s);
            }
            if (Labels)
            {
                float nw = HudLook.Width(it.countText, font) + 4f;
                HudLook.Label(new Rect(right - nw, rr.y, nw, rr.height), it.countText, it.e.count > 0 ? UiKit.Ink : UiKit.Faint, font, TextAnchor.MiddleRight);
                right -= nw + 4f;
                if (it.fill >= 0f)
                {
                    // The meals' days of food as a bar (its words in the tooltip), the warning line at its middle.
                    float bw = HudLookMath.At(40f, s), bh = Mathf.Max(3f, HudLookMath.At(4f, s));
                    var bar = new Rect(right - bw, Mathf.Round(rr.y + (rr.height - bh) / 2f), bw, bh);
                    HudLook.Bar(bar, it.fill, it.fill < 0.5f ? UiKit.Soon : UiKit.Good);
                    HudLook.Tick(bar, 0.5f, UiKit.WithAlpha(UiKit.Ink, 0.7f));
                    Tip(bar, it.value);
                    right -= bw + HudLookMath.At(5f, s);
                }
                else if (!it.value.NullOrEmpty())
                {
                    float vw = HudLook.Width(it.value, GameFont.Tiny) + 6f;
                    HudLook.Label(new Rect(right - vw, rr.y, vw, rr.height), it.value, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= vw;
                }
                HudLook.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rr.height), it.label, it.e.pinned ? UiKit.Gold : UiKit.Faint, big ? font : HudKit.Minor(s));
            }
            else HudLook.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rr.height), it.countText, it.e.count > 0 ? UiKit.Ink : UiKit.Faint, font);
            InteractLook(rr, it);
        }

        private float DrawGroupedLook(Rect r, float y, float s)
        {
            float hh = HeadH(s), rh = RowH(s);
            int index = 0;
            foreach (var g in groups)
            {
                var head = new Rect(r.x + 2f, y, r.width - 4f, hh);
                y += hh;
                if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(head, UiKit.WithAlpha(UiKit.Raised, 0.8f));
                if (Over(head)) HudLook.Hover(head);
                float x = head.x + Mathf.Round(3f * s);
                float a = Mathf.Max(7f, HudLookMath.At(9f, s));
                HudLook.Icon(HudLook.IconAt(x, head.y, hh, a), g.folded ? TexButton.Reveal : TexButton.Collapse, UiKit.Faint);
                x += a + Mathf.Round(4f * s);
                if (g.icon != null)
                {
                    float ic = HudLook.MarkSize(s);
                    HudLook.Icon(HudLook.IconAt(x, head.y, hh, ic), g.icon, UiKit.Heading);
                    x += ic + Mathf.Round(4f * s);
                }
                float tw = HudLook.Width(g.totalText, GameFont.Tiny) + 4f;
                HudLook.Label(new Rect(head.xMax - tw - 3f, head.y, tw, hh), g.totalText, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                HudLook.Label(new Rect(x, head.y, Mathf.Max(0f, head.xMax - tw - 6f - x), hh), g.label, UiKit.Heading, GameFont.Tiny);
                Tip(head, HudKit.T(g.folded ? "CC_Pnl_I_UnfoldTip" : "CC_Pnl_I_FoldTip"));
                if (Clicked(head))
                {
                    if (Event.current.button == 1) OpenMenu();
                    else
                    {
                        var st = St;
                        if (!st.folds.Remove(g.key)) st.folds.Add(g.key);
                        MarkDirty();
                        HudLayout.Save();
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                if (g.folded) continue;
                foreach (var it in g.items)
                {
                    DrawRowLook(new Rect(r.x + 2f + Mathf.Round(6f * s), y, r.width - 4f - Mathf.Round(6f * s), rh), it, s, index++);
                    y += rh;
                }
            }
            return y;
        }

        private float DrawGridLook(Rect r, float y, float s)
        {
            float tw = TileW(s), th = TileH(s), gap = 2f;
            float x0 = r.x + Mathf.Round(4f * s);
            int cols = Mathf.Max(1, Mathf.Min(St.columns, shown.Count));
            for (int i = 0; i < shown.Count; i++)
            {
                var it = shown[i];
                HudInventoryMath.GridCell(i, cols, tw, th, gap, out float cx, out float cy);
                var tile = new Rect(x0 + cx, y + cy, tw, th);
                if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(tile, it.e.pinned ? UiKit.WithAlpha(UiKit.Gold, 0.1f) : UiKit.WithAlpha(UiKit.Raised, 0.7f));
                if (Over(tile)) HudLook.Hover(tile);
                float ic = Mathf.Round((St.compact ? 20f : 24f) * s);
                IconLook(new Rect(tile.x + (tw - ic) / 2f, tile.y + Mathf.Round(2f * s), ic, ic), it);
                var low = new Rect(tile.x, tile.yMax - Mathf.Round(15f * s), tw, Mathf.Round(15f * s));
                HudLook.Label(low, it.countText, it.e.count > 0 ? UiKit.Ink : UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
                if (Event.current.type == EventType.Repaint)
                {
                    if (St.Shows(PartChange) && it.changeText != null)
                    {
                        float m = Mathf.Max(3f, Mathf.Round(4f * s));
                        Widgets.DrawBoxSolid(new Rect(tile.xMax - m - 2f, tile.y + 2f, m, m), it.e.change > 0 ? Gain : Loss);
                    }
                    if (it.rating >= 0f) Widgets.DrawBoxSolid(new Rect(tile.x, tile.yMax - 2f, tile.width, 2f), it.ratingColor);
                }
                if (it.rating >= 0f) Tip(tile, it.ratingTip);
                InteractLook(tile, it);
            }
            HudInventoryMath.GridSize(shown.Count, cols, tw, th, gap, out _, out float h);
            return y + h;
        }

        /// <summary>An item's tooltip (made only while it shows), its click (its stacks, one after another) and its right-click (pin, unpin, the menu).</summary>
        private void InteractLook(Rect r, Item it)
        {
            if (it.tip != null) Tip(r, new TipSignal(it.tip, Gen.HashCombineInt(0x1A7E40, (it.cat?.shortHash ?? 0) ^ (it.def?.shortHash ?? 0))));
            if (!Clicked(r)) return;
            if (Event.current.button == 1) ItemMenu(it);
            else GoTo(it);
        }
    }
}
