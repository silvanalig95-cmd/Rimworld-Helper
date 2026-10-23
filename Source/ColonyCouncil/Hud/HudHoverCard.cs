using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The hover card: rest the mouse on one of the colony's people on the map and, after a moment, a small card shows
    /// beside the mouse (or in a corner): their name and mood bar with its break lines and where it's heading, the two or
    /// three things that matter most about them right now by the shared ranking (bleeding, a dangerous condition, a break
    /// or the break line, hunger, strain, a wish waiting, the biggest moods dragging them down with what helps), and
    /// what they're doing. It never takes the mouse: the pawn is found from the cell under it (the cell's things), the
    /// card is kept half a second per pawn, and it stays away while a button is held, something is being placed or
    /// targeted, a window or menu is under the mouse, or the world map is up.
    /// </summary>
    public static class HudHoverCard
    {
        private const int WindowId = 0x5A1E77;

        private sealed class Card
        {
            public Pawn pawn;
            public HudPawn mood;
            public readonly List<HudConcern> concerns = new List<HudConcern>();
            /// <summary>For each concern: what would help goes on a line of its own (it doesn't fit beside the text).</summary>
            public readonly List<bool> actBelow = new List<bool>();
            public int more;
            public string moreText, title;
            public string doing;
            public float at;
            public float width, height;
        }

        private static Pawn target;
        private static float since, pickedAt = -1f;
        private static IntVec3 lastCell = IntVec3.Invalid;
        private static Pawn lastPicked;
        private static Card card;
        private static bool broken;
        private static Pawn shownPawn;
        private static int shownFrame = -10;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>
        /// Whether the card was drawn for this pawn this frame or the last, with "Hide the game's tooltip for that pawn"
        /// on: the game's own tooltip for them stays away (Patch_HudGameTooltip).
        /// </summary>
        public static bool ShowingFor(Pawn p) =>
            p != null && p == shownPawn && Time.frameCount - shownFrame <= 1 && S != null && S.hoverCard && S.hoverHideGameTip;

        public static void Reset()
        {
            shownPawn = null;
            target = null;
            lastPicked = null;
            card = null;
            lastCell = IntVec3.Invalid;
        }

        /// <summary>From <see cref="HudComponent"/>'s OnGUI, every frame; the work is done on repaint only.</summary>
        public static void OnGUI()
        {
            if (broken || Event.current == null || Event.current.type != EventType.Repaint) return;
            try
            {
                if (!S.hoverCard || Current.ProgramState != ProgramState.Playing)
                {
                    target = null;
                    return;
                }
                var p = PawnUnderMouse();
                float now = Time.realtimeSinceStartup;
                if (p != target)
                {
                    target = p;
                    since = now;
                }
                if (p == null || now - since < S.hoverDelay || !ModifierHeld()) return;
                var c = CardFor(p, now);
                if (c == null) return;
                // Never over a window: beside the mouse wherever there's room, or not at all.
                if (!Place(c, out var rect)) return;
                shownPawn = p;
                shownFrame = Time.frameCount;
                Find.WindowStack.ImmediateWindow(WindowId, rect, WindowLayer.Super, () => Draw(c, rect.AtZero()), false, false, 0f);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("hover card (off for this session)", e);
            }
        }

        private static bool ModifierHeld()
        {
            var e = Event.current;
            switch (S.hoverModifier)
            {
                case HudModifier.Alt: return e.alt;
                case HudModifier.Shift: return e.shift;
                case HudModifier.Ctrl: return e.control || e.command;
                default: return true;
            }
        }

        // ---------------- Who is under the mouse ----------------

        private static Pawn PawnUnderMouse()
        {
            var map = Find.CurrentMap;
            if (map == null || WorldRendererUtility.WorldSelected) return null;
            // Not while a button is held (dragging a box, the camera, a panel), placing or targeting something, or with a menu open.
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return null;
            if (Find.Selector?.dragBox != null && Find.Selector.dragBox.IsValidAndActive) return null;
            if (Find.DesignatorManager?.SelectedDesignator != null) return null;
            if (Find.Targeter != null && Find.Targeter.IsTargeting) return null;
            var stack = Find.WindowStack;
            if (stack.FloatMenu != null || stack.AnyWindowAbsorbingAllInput) return null;
            var mouse = UI.MousePositionOnUIInverted;
            var under = stack.GetWindowAt(mouse);
            if (under != null && under.ID != -WindowId) return null;
            // The game's own bars over the map: the main buttons at the bottom, the colonist bar at the top.
            if (mouse.y >= UI.screenHeight - 35f) return null;
            if (Find.PlaySettings != null && Find.PlaySettings.showColonistBar && Find.ColonistBar != null && Find.ColonistBar.AnyColonistOrCorpseAt(mouse))
                return null;
            // In the corner, the card itself may be under the mouse: keep showing whom it was showing.
            if (under != null) return target != null && target.Map == map && Fits(target) ? target : null;
            var cell = UI.MouseCell();
            float now = Time.realtimeSinceStartup;
            if (cell == lastCell && now - pickedAt < 0.1f) return lastPicked;
            lastCell = cell;
            pickedAt = now;
            lastPicked = null;
            if (!cell.InBounds(map) || cell.Fogged(map)) return null;
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && Fits(pawn))
                {
                    lastPicked = pawn;
                    break;
                }
            return lastPicked;
        }

        private static bool Fits(Pawn p)
        {
            if (p.Dead || !p.RaceProps.Humanlike) return false;
            if (p.IsFreeNonSlaveColonist) return true;
            return S.hoverWho == HoverWho.Everyone && (p.IsSlaveOfColony || p.IsPrisonerOfColony || p.IsFreeColonist);
        }

        // ---------------- The card ----------------

        private static float Scale => HudMath.ClampScale(S.hoverScale);

        private static Card CardFor(Pawn p, float now)
        {
            if (card != null && card.pawn == p && now - card.at < 0.5f && now >= card.at) return card;
            var c = new Card { pawn = p, at = now };
            if (p.needs?.mood != null) c.mood = HudMood.For(p);
            if (S.hoverConcerns)
            {
                var all = HudConcerns.For(p, c.mood);
                int max = Mathf.Clamp(S.hoverLines, 1, 5);
                foreach (var con in all)
                {
                    if (con.score < HudRank.WorthFloor) continue;
                    // Never cut what's critical; the rest up to the lines asked for.
                    if (c.concerns.Count < max || con.Level == HudLevel.Life) c.concerns.Add(con);
                    else c.more++;
                }
            }
            if (S.hoverDoing) c.doing = HudConcerns.Doing(p);
            if (c.more > 0) c.moreText = "CC_Pnl_AndMore".Translate(c.more).Resolve();
            c.title = p.LabelShortCap + (p.IsPrisonerOfColony ? " (" + HudKit.T("CC_Pnl_Prisoner") + ")" : p.IsSlaveOfColony ? " (" + HudKit.T("CC_Pnl_Slave") + ")" : "");
            float s = Scale;
            float pad = Mathf.Round(6f * s);
            c.width = Mathf.Round(292f * s);
            float h = pad + Mathf.Round(30f * s);
            if (S.hoverMood && c.mood != null) h += Mathf.Round(12f * s);
            float line = LineH(s);
            // What would help goes beside the mood when both fit, else on its own line under it: never cut short.
            var font = HudKit.Main(s);
            float textW = c.width - pad * 2f - Mathf.Round(13f * s) - 5f;
            int lines = 0;
            foreach (var con in c.concerns)
            {
                bool below = !con.act.NullOrEmpty() && HudKit.Width(con.text + ActJoin + con.act, font) > textW;
                c.actBelow.Add(below);
                lines += below ? 2 : 1;
            }
            if (c.concerns.Count > 0) h += Mathf.Round(4f * s) + lines * line;
            else if (S.hoverConcerns && c.mood != null) h += line;
            if (c.more > 0) h += Mathf.Round(15f * s);
            if (!c.doing.NullOrEmpty()) h += Mathf.Round(4f * s) + Mathf.Round(16f * s);
            c.height = h + pad;
            card = c;
            return c;
        }

        private static float LineH(float s) => Mathf.Round((HudKit.Main(s) == GameFont.Tiny ? 17f : 21f) * Mathf.Max(1f, s * 0.9f));

        private const string ActJoin = "  → ";

        /// <summary>
        /// Where the card goes: beside the mouse (below and right, else the other sides), or in the corner. False when
        /// every place would cover a window (the inspect pane, a main tab, a dialog, a planner): the card waits.
        /// </summary>
        private static bool Place(Card c, out Rect rect)
        {
            float sw = UI.screenWidth, sh = UI.screenHeight;
            float w = Mathf.Min(c.width, sw), h = Mathf.Min(c.height, sh);
            if (S.hoverPlace == HoverPlace.Corner)
            {
                float bottom = sh - 35f;
                var inspect = MainButtonDefOf.Inspect;
                if (Find.MainTabsRoot?.OpenTab == inspect && inspect?.TabWindow != null) bottom = Mathf.Min(bottom, inspect.TabWindow.windowRect.y);
                rect = new Rect(0f, Mathf.Max(0f, bottom - h - 6f), w, h);
                return !OverAWindow(rect);
            }
            var m = UI.MousePositionOnUIInverted;
            const float off = 20f;
            // Right and below first, as before; then left, above, or both.
            for (int i = 0; i < 4; i++)
            {
                bool left = i == 1 || i == 3, above = i >= 2;
                float x = left ? m.x - off - w : m.x + off, y = above ? m.y - off - h : m.y + off;
                if (i == 0)
                {
                    if (x + w > sw) x = m.x - off - w;
                    if (y + h > sh) y = m.y - off - h;
                }
                rect = new Rect(Mathf.Clamp(x, 0f, Mathf.Max(0f, sw - w)), Mathf.Clamp(y, 0f, Mathf.Max(0f, sh - h)), w, h);
                // Clamped onto the screen, the card mustn't land under the mouse.
                if (rect.Contains(m)) continue;
                if (!OverAWindow(rect)) return true;
            }
            rect = default;
            return false;
        }

        /// <summary>Whether a rectangle covers part of a window other than the panels and the card itself.</summary>
        private static bool OverAWindow(Rect r)
        {
            var windows = Find.WindowStack?.Windows;
            if (windows == null) return false;
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w == null || w.ID == -WindowId || HudWindowOrder.IsPanel(w)) continue;
                if (w.windowRect.Overlaps(r)) return true;
            }
            return false;
        }

        private static void Draw(Card c, Rect r)
        {
            var oldFont = Text.Font;
            try
            {
                float s = Scale;
                float pad = Mathf.Round(6f * s);
                Widgets.DrawBoxSolid(r, HudKit.Background(S.hoverOpacity, false));
                Widgets.DrawBox(r, 1);
                var p = c.pawn;
                var h = c.mood;
                float x = r.x + pad, y = r.y + pad, w = r.width - pad * 2f;
                // Portrait, name, mood.
                float ps = Mathf.Round(28f * s);
                var ring = h == null ? UiKit.Neutral : h.inBreak || h.now < h.major ? UiKit.Bad : h.now < h.minor ? UiKit.Warn : UiKit.WithAlpha(Color.white, 0.3f);
                UiKit.Portrait(new Rect(x, y, ps, ps), p, ring);
                float tx = x + ps + Mathf.Round(6f * s);
                var titleFont = s >= 1.2f ? GameFont.Medium : GameFont.Small;
                float numW = 0f;
                if (h != null)
                {
                    string num = h.now.ToStringPercent();
                    numW = HudKit.Width(num, titleFont) + 4f;
                    HudKit.Label(new Rect(x + w - numW, y, numW, ps), num, HudKit.MoodColor(h.now, h.minor, h.major, h.extreme), titleFont, TextAnchor.MiddleRight);
                    float aw = Mathf.Round(11f * s);
                    HudKit.Heading(new Rect(x + w - numW - aw - 2f, y + (ps - aw) / 2f, aw, aw), h.now, h.heading);
                    numW += aw + 4f;
                }
                HudKit.Label(new Rect(tx, y, Mathf.Max(0f, x + w - numW - tx), ps), c.title, Color.white, titleFont);
                y += ps + Mathf.Round(2f * s);
                if (S.hoverMood && h != null)
                {
                    HudKit.MoodBar(new Rect(x, y + Mathf.Round(3f * s), w, Mathf.Max(4f, Mathf.Round(6f * s))), h.now, h.heading, h.minor, h.major, h.extreme);
                    y += Mathf.Round(12f * s);
                }
                var font = HudKit.Main(s);
                float line = LineH(s);
                float icon = Mathf.Round(13f * s);
                if (c.concerns.Count > 0)
                {
                    y += Mathf.Round(4f * s);
                    for (int i = 0; i < c.concerns.Count; i++)
                    {
                        var con = c.concerns[i];
                        var color = HudKit.ColorOf(con.Level, con.Level == HudLevel.Worth ? new Color(0.9f, 0.9f, 0.9f) : UiKit.Faint);
                        if (con.icon != null) UiKit.Icon(new Rect(x, y + (line - icon) / 2f, icon, icon), con.icon, color);
                        var tr = new Rect(x + icon + 5f, y, w - icon - 5f, line);
                        bool below = i < c.actBelow.Count && c.actBelow[i];
                        if (con.act.NullOrEmpty()) HudKit.Label(tr, con.text, color, font);
                        else if (!below) HudKit.Label(tr, con.text + ActJoin + con.act, color, font);
                        else
                        {
                            // What would help on a line of its own, indented under the mood it eases.
                            HudKit.Label(tr, con.text, color, font);
                            y += line;
                            float indent = Mathf.Round(10f * s);
                            HudKit.Label(new Rect(tr.x + indent, y, tr.width - indent, line), "→ " + con.act, UiKit.Gold, font);
                        }
                        y += line;
                    }
                }
                else if (S.hoverConcerns && h != null)
                {
                    HudKit.Label(new Rect(x, y, w, line), HudKit.T("CC_Pnl_C_AllWell"), UiKit.Good, font);
                    y += line;
                }
                if (c.more > 0)
                {
                    HudKit.Label(new Rect(x + icon + 5f, y, w, Mathf.Round(15f * s)), c.moreText, UiKit.Dim, GameFont.Tiny);
                    y += Mathf.Round(15f * s);
                }
                if (!c.doing.NullOrEmpty())
                {
                    y += Mathf.Round(4f * s);
                    UiKit.Divider(x, y - 2f, w);
                    HudKit.Label(new Rect(x, y, w, Mathf.Round(16f * s)), c.doing, UiKit.Faint, GameFont.Tiny);
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("hover card (off for this session)", e);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
    }
}
