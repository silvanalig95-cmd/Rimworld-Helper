using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The hover card in Rimfolk's own look, built every half second as before and drawn from what was built: a rounded card
    /// in the look's panel colour with its edge; their portrait ringed by their mood, their name (a link in the corner), the
    /// mood small at the right in its colour; the mood bar across with their break lines and where it is heading, the heart's
    /// line under it as the heart's meters show it; what matters most, each with the stripe in its colour and its icon, what
    /// would help beside it or under it in the accent; a day off or a bad afternoon under way, an empty weapon slot; and what
    /// they are doing at the foot. Nothing made while drawing.
    /// </summary>
    public static partial class HudHoverCard
    {
        private static Card CardLook(Pawn p, float now)
        {
            bool wasOwn = HudLook.Begin();
            try
            {
                var c = new Card { pawn = p, at = now, refined = true };
                if (p.needs?.mood != null)
                {
                    c.mood = HudMood.For(p);
                    HudLookFacts.Of(c.mood);
                }
                if (S.hoverConcerns)
                {
                    int max = Mathf.Clamp(S.hoverLines, 1, 5);
                    foreach (var con in HudConcerns.For(p, c.mood))
                    {
                        if (con.score < HudRank.WorthFloor) continue;
                        if (c.concerns.Count < max || con.Level == HudLevel.Life) c.concerns.Add(con);
                        else c.more++;
                    }
                }
                var h = c.mood;
                if (h != null && h.off != TimeOffKind.None && h.offTip != null)
                    c.extra.Add((HudIcons.DayOff ?? Icons.Heart, h.offTip, h.off == TimeOffKind.BadDay ? UiKit.Soon : UiKit.Cool));
                if (h != null && h.kitGap && h.kitWords != null) c.extra.Add((HudIcons.KitGap ?? Icons.Defenses, h.kitWords, UiKit.Soon));
                if (S.hoverDoing) c.doing = HudConcerns.Doing(p);
                if (c.more > 0) c.moreText = "CC_Pnl_AndMore".Translate(c.more).Resolve();
                c.title = p.LabelShortCap + (p.IsPrisonerOfColony ? " (" + HudKit.T("CC_Pnl_Prisoner") + ")" : p.IsSlaveOfColony ? " (" + HudKit.T("CC_Pnl_Slave") + ")" : "");
                float s = Scale;
                float pad = HudLookMath.At(8f, s);
                c.width = HudLookMath.At(292f, s);
                float h0 = pad + HudLookMath.At(28f, s) + HudLookMath.At(4f, s);
                if (S.hoverMood && h != null) h0 += HudLookMath.At(HudLookMath.MoodBarHeight, s) + (h.heartOn ? HudLookMath.At(HudLookMath.HeartGap + HudLookMath.HeartLine, s) : 0f) + HudLookMath.At(6f, s);
                float line = LineH(s);
                var font = HudKit.Main(s);
                float textW = c.width - pad * 2f - HudLookMath.At(HudLookMath.Stripe + 4f, s) - HudLook.IconSize(s) - 5f;
                int lines = 0;
                foreach (var con in c.concerns)
                {
                    string joined = con.act.NullOrEmpty() ? con.text : con.text + ActJoin + con.act;
                    bool below = !con.act.NullOrEmpty() && HudLook.Width(joined, font) > textW;
                    c.joined.Add(joined);
                    c.actBelow.Add(below);
                    lines += below ? 2 : 1;
                }
                lines += c.extra.Count;
                if (lines > 0) h0 += lines * line;
                else if (S.hoverConcerns && h != null) h0 += line;
                if (c.more > 0) h0 += HudLookMath.At(15f, s);
                if (!c.doing.NullOrEmpty()) h0 += HudLookMath.At(6f, s) + HudLookMath.At(16f, s);
                c.height = h0 + pad;
                return c;
            }
            finally
            {
                HudLook.End(wasOwn);
            }
        }

        private static void DrawLook(Card c, Rect r)
        {
            var oldFont = Text.Font;
            bool wasOwn = HudLook.Begin();
            try
            {
                float s = Scale;
                float pad = HudLookMath.At(8f, s);
                if (Event.current.type == EventType.Repaint)
                {
                    UiKit.Round(r, HudLook.CardFill(S.hoverOpacity, true), SkinMath.RadiusCard);
                    UiKit.RoundEdge(r, UiKit.EdgeStrong, SkinMath.RadiusCard);
                }
                var p = c.pawn;
                var h = c.mood;
                float x = r.x + pad, y = r.y + pad, w = r.width - pad * 2f;
                float ps = HudLookMath.At(28f, s);
                var ring = h == null ? UiKit.Faint : h.inBreak || h.now < h.major ? UiKit.Danger : h.now < h.minor ? UiKit.Soon : UiKit.WithAlpha(UiKit.Ink, 0.3f);
                UiKit.Portrait(HudLook.Snap(new Rect(x, y, ps, ps)), p, ring);
                float tx = x + ps + HudLookMath.At(7f, s);
                var titleFont = s >= 1.2f ? GameFont.Medium : GameFont.Small;
                float numW = 0f;
                if (h != null && h.nowText != null)
                {
                    numW = HudLook.Width(h.nowText, GameFont.Small) + 4f;
                    HudLook.Label(new Rect(x + w - numW, y, numW, ps), h.nowText, HudLook.MoodInk(h.now, h.minor, h.major, h.extreme), GameFont.Small, TextAnchor.MiddleRight);
                }
                var titleRect = new Rect(tx, y, Mathf.Max(0f, x + w - numW - tx - 4f), ps);
                if (S.hoverPlace == HoverPlace.Corner)
                {
                    float nw = Mathf.Min(HudLook.Width(c.title, titleFont) + 2f, titleRect.width);
                    var hit = new Rect(titleRect.x, titleRect.y, nw, titleRect.height);
                    bool over = Mouse.IsOver(hit);
                    HudLook.Label(titleRect, c.title, over ? UiKit.LinkHot : UiKit.Link, titleFont);
                    LinkText.Underline(hit.x, Mathf.Round(hit.y + (hit.height + HudKit.LineHeight(titleFont)) / 2f - 5f), nw - 3f, over);
                    if (Widgets.ButtonInvisible(hit, false))
                    {
                        Cards.Click();
                        Cards.Open(ThingRefs.Of(p));
                    }
                }
                else HudLook.Label(titleRect, c.title, UiKit.Ink, titleFont);
                y += ps + HudLookMath.At(4f, s);
                if (S.hoverMood && h != null)
                {
                    float bh = Mathf.Max(4f, HudLookMath.At(HudLookMath.MoodBarHeight, s));
                    HudLook.MoodBar(new Rect(x, y, w, bh), h.now, h.heading, h.minor, h.major, h.extreme);
                    y += bh;
                    if (h.heartOn)
                    {
                        y += HudLookMath.At(HudLookMath.HeartGap, s);
                        float hh = HudLookMath.At(HudLookMath.HeartLine, s);
                        HudLook.HeartLine(new Rect(x, y, w, hh), h.band, h.strain, h.good, h.goodOn);
                        y += hh;
                    }
                    y += HudLookMath.At(6f, s);
                }
                var font = HudKit.Main(s);
                float line = LineH(s);
                float icon = HudLook.IconSize(s);
                float ix = x + HudLookMath.At(HudLookMath.Stripe + 4f, s);
                for (int i = 0; i < c.concerns.Count; i++)
                {
                    var con = c.concerns[i];
                    var level = con.Level;
                    var color = level >= HudLevel.Soon ? HudLook.LevelColor(level) : level == HudLevel.Worth ? UiKit.Ink : UiKit.Faint;
                    bool below = i < c.actBelow.Count && c.actBelow[i];
                    float rowH = below ? line * 2f : line;
                    HudLook.Stripe(new Rect(x, y, w, rowH), level, s);
                    var tex = con.icon ?? HudLook.LevelIcon(level);
                    if (tex != null) HudLook.Icon(HudLook.IconAt(ix, y, line, icon), tex, color);
                    var tr = new Rect(ix + icon + 5f, y, Mathf.Max(0f, x + w - ix - icon - 5f), line);
                    if (!below) HudLook.Label(tr, i < c.joined.Count ? c.joined[i] : con.text, color, font);
                    else
                    {
                        HudLook.Label(tr, con.text, color, font);
                        float indent = HudLookMath.At(10f, s);
                        HudLook.Label(new Rect(tr.x + indent, y + line, tr.width - indent, line), con.act, UiKit.Gold, font);
                    }
                    y += rowH;
                }
                foreach (var (tex, text, color) in c.extra)
                {
                    HudLook.Icon(HudLook.IconAt(ix, y, line, icon), tex, color);
                    HudLook.Label(new Rect(ix + icon + 5f, y, Mathf.Max(0f, x + w - ix - icon - 5f), line), text, color, font);
                    y += line;
                }
                if (c.concerns.Count == 0 && c.extra.Count == 0 && S.hoverConcerns && h != null)
                {
                    HudLook.Label(new Rect(x, y, w, line), HudKit.T("CC_Pnl_C_AllWell"), UiKit.Good, font);
                    y += line;
                }
                if (c.more > 0)
                {
                    HudLook.Label(new Rect(ix + icon + 5f, y, w, HudLookMath.At(15f, s)), c.moreText, UiKit.Faint, GameFont.Tiny);
                    y += HudLookMath.At(15f, s);
                }
                if (!c.doing.NullOrEmpty())
                {
                    y += HudLookMath.At(6f, s);
                    if (Event.current.type == EventType.Repaint) UiKit.Divider(x, y - 3f, w);
                    HudLook.Label(new Rect(x, y, w, HudLookMath.At(16f, s)), c.doing, UiKit.Faint, GameFont.Tiny);
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("hover card (off for this session)", e);
            }
            finally
            {
                HudLook.End(wasOwn);
                Text.Font = oldFont;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
    }
}
