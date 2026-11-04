using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>Where one card shown somewhere is scrolled to (a drawer's, the floating card's).</summary>
    public sealed class CardState
    {
        public Vector2 scroll;
        public float height = 400f;
        public ThingRef shownFor;
    }

    /// <summary>
    /// Draws a card (its model from Cards.Model): the head (picture, name, what it is, chips; for a place the small world
    /// view button that was "Show"), then the slots in their order, each section with its heading, count and fold (folds
    /// kept per kind of card for the session; a section with nothing is not drawn), and the acts at its foot, each greyed
    /// with why not and the first reason written under them. A colonist's card is the Roster's page of them
    /// (StoryView) under the same head. The sections other parts add (CardSections) draw in their slots; the region map
    /// panel's old lists (NeighbourPanel) still draw, in the Ties slot for a town and in Now for any other place.
    /// </summary>
    public static class CardView
    {
        private const float Pic = 42f;
        private const float RowH = 40f;
        private static readonly Dictionary<CardKind, Folds> folds = new Dictionary<CardKind, Folds>();
        /// <summary>Whether a section drawn by its own code had something to show for a thing the last time (so a folded one keeps its heading).</summary>
        private static readonly Dictionary<(ThingRef, string), bool> had = new Dictionary<(ThingRef, string), bool>();

        private static Folds FoldsOf(CardKind k)
        {
            if (!folds.TryGetValue(k, out var f)) folds[k] = f = new Folds();
            return f;
        }

        // A colonist's sections after their story, drawn from inside StoryView's scroll view.
        private static CardModel after;
        private static readonly Func<float, float, float> afterStory = (y, w) => after == null ? 0f : Slots(after, 0f, y, w);

        /// <summary>The whole card in the rect.</summary>
        public static void Draw(Rect rect, ThingRef r, CardState st)
        {
            if (r.IsNone) return;
            if (st.shownFor != r)
            {
                st.scroll = Vector2.zero;
                st.shownFor = r;
            }
            var m = Cards.Model(r);
            var font = Text.Font;
            try
            {
                float y = rect.y + Head(new Rect(rect.x, rect.y, rect.width, 0f), m);
                float footH = ActsHeight(m, rect.width);
                var body = new Rect(rect.x, y + 4f, rect.width, Mathf.Max(20f, rect.yMax - footH - y - 4f));
                if (m.kind == CardKind.Colonist && m.pawn != null && !m.gone)
                {
                    after = m;
                    StoryView.Draw(body, m.pawn, ref st.scroll, ref st.height, FoldsOf(CardKind.Colonist), StoryParts.Card, afterStory);
                    after = null;
                }
                else
                {
                    var view = new Rect(0f, 0f, body.width - 16f, Mathf.Max(st.height, body.height - 1f));
                    Widgets.BeginScrollView(body, ref st.scroll, view);
                    try
                    {
                        float h = Slots(m, 0f, 0f, view.width);
                        if (h <= 0f && !m.gone) h = UiKit.EmptyState(0f, 0f, view.width, Icons.World, "CC_Card_Nothing".Translate(), 80f);
                        st.height = Mathf.Max(h + 8f, 60f);
                    }
                    finally
                    {
                        Widgets.EndScrollView();
                    }
                }
                if (footH > 0f) Acts(new Rect(rect.x, rect.yMax - footH, rect.width, footH), m);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: drawing a card of " + m.kind, e);
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
            }
        }

        // ---------------- The head ----------------

        /// <summary>The head from the rect's top; returns its height.</summary>
        public static float Head(Rect r, CardModel m)
        {
            float x = r.x, y = r.y, w = r.width;
            UiKit.Picture(new Rect(x, y, Pic, Pic), m.pawn, m.faction, m.icon, m.iconColor, m.ring);
            float right = 0f;
            if (m.kind == CardKind.Place && (m.place != null || m.tile.Valid))
            {
                right = 28f;
                var b = new Rect(x + w - 24f, y + 2f, 24f, 24f);
                if (UiKit.IconButton(b, Icons.World, "CC_Card_WorldViewTip".Translate()))
                {
                    var target = m.place != null && !m.place.Destroyed ? new GlobalTargetInfo(m.place) : new GlobalTargetInfo(m.tile);
                    CameraJumper.TryJumpAndSelect(target);
                }
            }
            float tx = x + Pic + 10f, tw = Mathf.Max(0f, w - Pic - 10f - right);
            UiKit.LabelFit(new Rect(tx, y - 1f, tw, 28f), m.title, m.gone ? UiKit.Faint : UiKit.HeaderColor, GameFont.Medium);
            LinkText.Line(new Rect(tx, y + 25f, tw + right, 20f), m.subtitle, UiKit.Faint, GameFont.Small);
            y += Pic + 8f;
            if (m.chips.Count > 0) y += Chips(x, y, w, m.chips) + 6f;
            UiKit.Divider(x, y, w);
            return y + 2f - r.y;
        }

        /// <summary>The head's chips, wrapping to the width; returns the height used.</summary>
        private static float Chips(float x, float y, float w, List<CardChip> chips)
        {
            float cx = x, cy = y;
            foreach (var c in chips)
            {
                float cw = c.thing.IsNone ? UiKit.StateChipWidth(c.text, w) : UiKit.ThingChipWidth(c.text, w);
                if (cx > x && cx + cw > x + w)
                {
                    cx = x;
                    cy += UiKit.ChipHeight + 4f;
                }
                cx += (c.thing.IsNone ? UiKit.StateChip(cx, cy, c.text, c.color, w, c.tip) : UiKit.ThingChip(cx, cy, c.thing, c.text, null, c.faction, c.icon, w)) + 4f;
            }
            return cy - y + UiKit.ChipHeight;
        }

        // ---------------- The slots ----------------

        /// <summary>Every slot between the head and the acts, from (x, y) in the width; returns the height used.</summary>
        public static float Slots(CardModel m, float x, float y, float w)
        {
            float start = y;
            var f = FoldsOf(m.kind);
            int bi = 0;
            for (var slot = CardSlot.Now; slot <= CardSlot.Story; slot++)
            {
                while (bi < m.blocks.Count && m.blocks[bi].slot < slot) bi++;
                var sections = CardSections.registry.In(m.kind, slot);
                int si = 0;
                while (true)
                {
                    bool block = bi < m.blocks.Count && m.blocks[bi].slot == slot;
                    bool section = si < sections.Count;
                    if (!block && !section) break;
                    // Section before block when its order comes first.
                    if (section && (!block || sections[si].order < m.blocks[bi].order))
                    {
                        var e = sections[si++];
                        if (e.value.draw != null) y += Section(m, e, f, x, y, w);
                        continue;
                    }
                    y += Block(m.blocks[bi++], f, x, y, w);
                }
                y += Legacy(m, slot, x, y, w);
            }
            return y - start;
        }

        private static float Block(CardBlock b, Folds f, float x, float y, float w)
        {
            if (b.Empty) return 0f;
            float y0 = y;
            bool open = true;
            if (b.heading != null) y += UiKit.SectionHeading(x, y, w, b.heading, b.count, f, b.id, out open, b.right);
            if (!open) return y - y0 + 4f;
            if (b.steps != null) y += UiKit.Steps(x, y, w, b.steps, b.step) + 2f;
            foreach (var l in b.lines)
            {
                float lx = x, lw = w;
                if (l.icon != null)
                {
                    UiKit.Icon(new Rect(x, y + 3f, 14f, 14f), l.icon, l.iconColor);
                    lx += 20f;
                    lw -= 20f;
                }
                float h = LinkText.Draw(new Rect(lx, y, lw, 9999f), l.text, l.color, l.font);
                if (!l.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(lx, y, lw, h), l.tip);
                y += h + 2f;
            }
            foreach (var row in b.rows)
            {
                Row(new Rect(x, y, w, RowH), row);
                y += RowH;
            }
            if (b.more != null)
            {
                y += 2f;
                if (MoreLink(new Rect(x, y, w, 20f), b.more)) b.moreRun?.Invoke();
                y += 20f;
            }
            return y - y0 + 8f;
        }

        /// <summary>A link that leads to a place in the windows rather than to a card ("Its whole history (14)"). Returns true when clicked.</summary>
        public static bool MoreLink(Rect r, string label)
        {
            var old = Text.Font;
            Text.Font = GameFont.Small;
            float tw = Mathf.Min(Text.CalcSize(label).x, r.width);
            Text.Font = old;
            var hit = new Rect(r.x, r.y, tw, r.height);
            bool over = Mouse.IsOver(hit);
            UiKit.LabelFit(new Rect(r.x, r.y, r.width, r.height), label, over ? UiKit.LinkHot : UiKit.Link);
            LinkText.Underline(r.x, r.y + r.height - 3f, tw - 2f, over);
            if (!Widgets.ButtonInvisible(hit, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        /// <summary>A row: the picture (a link to its card), the name and a quieter line (with links), an act at the right.</summary>
        public static void Row(Rect r, CardRow row)
        {
            float ps = 32f;
            var pic = new Rect(r.x, r.y + 4f, ps, ps);
            UiKit.Picture(pic, row.pawn, row.faction, row.icon, row.ring, row.ring);
            if (!row.thing.IsNone)
            {
                if (Mouse.IsOver(pic))
                {
                    Widgets.DrawBox(pic);
                    Previews.Hover(row.thing, pic);
                }
                if (Widgets.ButtonInvisible(pic, false))
                {
                    Cards.Click();
                    Cards.Open(row.thing);
                }
            }
            float bw = row.act != null ? Mathf.Min(UiKit.ActButtonWidth(row.act.label), r.width * 0.35f) : 0f;
            float tx = r.x + ps + 8f, tw = r.xMax - tx - (bw > 0f ? bw + 6f : 0f);
            LinkText.Line(new Rect(tx, r.y + 2f, tw, 20f), row.name, Color.white);
            if (!row.second.NullOrEmpty()) LinkText.Line(new Rect(tx, r.y + 20f, tw, 18f), row.second, row.secondColor, GameFont.Tiny);
            if (row.act != null)
            {
                var b = new Rect(r.xMax - bw, r.y + 7f, bw, 26f);
                if (UiKit.ActButton(b, row.act.label, row.act.why, row.act.tip)) Run(row.act);
            }
            Widgets.DrawLineHorizontal(r.x, r.yMax - 1f, r.width, UiKit.DividerColor);
        }

        private static float Section(CardModel m, CardRegistry<CardSection>.Entry e, Folds f, float x, float y, float w)
        {
            var sec = e.value;
            string heading = HeadingOf(sec);
            var key = (m.thing, e.id);
            had.TryGetValue(key, out bool before);
            float hh = heading != null ? UiKit.HeadingHeight : 0f;
            if (heading != null && !f.IsOpen(e.id))
            {
                if (!before) return 0f;
                int c = Count(sec, m.thing);
                UiKit.SectionHeading(x, y, w, heading, c, f, e.id, out _);
                return hh + 4f;
            }
            float used = 0f;
            try
            {
                used = sec.draw(new Rect(x, y + hh, w, 2000f), m.thing);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("cards: the section " + e.id, ex);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
            }
            if (had.Count > 400) had.Clear();
            had[key] = used > 0f;
            if (used <= 0f) return 0f;
            if (heading != null) UiKit.SectionHeading(x, y, w, heading, Count(sec, m.thing), f, e.id, out _);
            return hh + used + 8f;
        }

        private static readonly Dictionary<CardSection, string> headings = new Dictionary<CardSection, string>();

        private static string HeadingOf(CardSection s)
        {
            if (s.heading.NullOrEmpty()) return null;
            if (!headings.TryGetValue(s, out var h)) headings[s] = h = s.heading.Translate().Resolve();
            return h;
        }

        private static int Count(CardSection s, ThingRef r)
        {
            if (s.count == null) return -1;
            try
            {
                return s.count(r);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>The region map panel's old lists, for what other mods or older code still add there (World/NeighbourPanel.cs).</summary>
        private static float Legacy(CardModel m, CardSlot slot, float x, float y, float w)
        {
            if (m.kind != CardKind.Place || NeighbourPanel.Sections.Count + NeighbourPanel.PlaceSections.Count + NeighbourPanel.HomeSections.Count == 0) return 0f;
            float start = y;
            if (slot == CardSlot.Ties)
            {
                var s = CardSections.TownOf(m.thing);
                if (s != null)
                    for (int i = 0; i < NeighbourPanel.Sections.Count; i++)
                        y += Old(() => NeighbourPanel.Sections[i](new Rect(x, y, w, 2000f), s), i);
            }
            else if (slot == CardSlot.Now)
            {
                var home = CardSections.HomeOf(m.thing);
                if (home != null)
                    for (int i = 0; i < NeighbourPanel.HomeSections.Count; i++)
                        y += Old(() => NeighbourPanel.HomeSections[i](new Rect(x, y, w, 2000f), home), i);
                else if (CardSections.OtherPlace(m.thing, out var tile, out var wo))
                    for (int i = 0; i < NeighbourPanel.PlaceSections.Count; i++)
                        y += Old(() => NeighbourPanel.PlaceSections[i](new Rect(x, y, w, 2000f), tile, wo), i);
            }
            return y - start;
        }

        private static float Old(Func<float> draw, int i)
        {
            float used = 0f;
            try
            {
                used = draw();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: an older panel section " + i, e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            return used > 0f ? used + 6f : 0f;
        }

        // ---------------- The acts ----------------

        private const int MaxActs = 3;

        public static float ActsHeight(CardModel m, float w)
        {
            if (m.acts.Count == 0) return 0f;
            bool why = false;
            foreach (var a in m.acts)
                if (a.why != null)
                    why = true;
            return 8f + 28f + (why ? 20f : 0f) + 4f;
        }

        private static void Acts(Rect r, CardModel m)
        {
            UiKit.Divider(r.x, r.y + 2f, r.width);
            float x = r.x, y = r.y + 8f;
            int shown = m.acts.Count > MaxActs ? MaxActs - 1 : m.acts.Count;
            CardAct firstWhy = null;
            for (int i = 0; i < m.acts.Count; i++)
                if (m.acts[i].why != null)
                {
                    firstWhy = m.acts[i];
                    break;
                }
            for (int i = 0; i < shown; i++)
            {
                var a = m.acts[i];
                float bw = Mathf.Min(UiKit.ActButtonWidth(a.label), r.xMax - x);
                if (bw < 60f) break;
                if (UiKit.ActButton(new Rect(x, y, bw, 28f), a.label, a.why, a.tip)) Run(a);
                x += bw + 6f;
            }
            if (shown < m.acts.Count)
            {
                string more = "CC_Card_Act_More".Translate();
                float bw = Mathf.Min(UiKit.ActButtonWidth(more), r.xMax - x);
                if (bw >= 50f && Widgets.ButtonText(new Rect(x, y, bw, 28f), more))
                {
                    var options = new List<FloatMenuOption>();
                    for (int i = shown; i < m.acts.Count; i++)
                    {
                        var a = m.acts[i];
                        options.Add(a.why == null
                            ? new FloatMenuOption(a.label, () => Run(a))
                            : new FloatMenuOption(a.label + " (" + a.why + ")", null));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            if (firstWhy != null)
                UiKit.LabelFit(new Rect(r.x, y + 30f, r.width, 18f), "CC_Card_WhyLine".Translate(firstWhy.label.Named("ACT"), firstWhy.why.Named("WHY")).Resolve(), UiKit.Dim,
                    GameFont.Tiny);
        }

        private static void Run(CardAct a)
        {
            if (a?.run == null || a.why != null) return;
            try
            {
                a.run();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: an act", e);
            }
            Cards.Dirty();
        }
    }
}
