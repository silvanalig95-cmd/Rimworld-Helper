using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's card (docs/design/rimfolk-look.md, stage 3, point 4; the player, 9 November 2026: "colonist card would be
    /// cool!"): one framed window per colonist (key "ColonistCard"), several open at once, in Rimfolk's own look or the
    /// game's. The header has their portrait large from the game's portrait cache (as dressed), the heart's line under it in
    /// the colonist bar's colours, their full name, who they are in the Story tab's one line, and what they are now (a break,
    /// downed, drafted, their role in the ideoligion, the Empire's title, where they are). Under it, in columns that fit the
    /// width (stacked when narrow, ColonistCardMath.Parts): <b>Now</b> (the mood bar with its break lines and where it heads,
    /// the heart's two meters with the day off's place, what weighs and lifts most, what the bands do, the top advice),
    /// <b>Skills</b> (each as a bar with its level and passion, what they are best at, those they can't do marked), <b>Kit</b>
    /// (the weapons in hand and carried, the apparel by slot, each with its picture and rating badge and the rating for them,
    /// a click opening the item's card; the kit's gaps and better weapons with their acts; whether they are dressed for the
    /// season) and <b>Life</b> (the Story tab's glance: life goal, wish with Show me and Promise, quirks, standing, friends
    /// and rivals). At its foot the game's tabs (Mood, Story, Gear, Health), going to them, selecting them, their whole story
    /// in the council window, and what else other parts let the colony do. The arrows in the title bar step to the previous or
    /// next colonist in the colonist bar's order, past those whose card is open already.
    /// <para>
    /// A colonist's card opens wherever Rimfolk opened a colonist's card before (Cards.Open: every name and portrait in
    /// Rimfolk's windows, panels and tabs), from a Ctrl-click on their portrait in the colonist bar, from the card button by
    /// their name on the inspect pane, and from an event card's "who is involved". Each card moves and sizes on its own;
    /// where the last one was left is remembered, and one opened while others are open steps down from them so each title
    /// bar shows. Built when it opens and again as ColonistCardModel says (twice a second, every three seconds); drawn only
    /// from that model.
    /// </para>
    /// </summary>
    public sealed class ColonistCardWindow : FramedWindow
    {
        private Pawn pawn;
        private readonly ColonistCardModel m = new ColonistCardModel();
        private Vector2 scroll;
        private float viewH = 600f;

        /// <summary>"Rimfolk's colonist card": a colonist's card is this window (on at first).</summary>
        public static bool On => ColonyCouncilMod.Settings?.colonistCard ?? true;

        public override Vector2 InitialSize => new Vector2(ColonistCardMath.FirstW, Mathf.Min(ColonistCardMath.FirstH, UI.screenHeight - 120f));

        public ColonistCardWindow(Pawn p) : base("ColonistCard", true)
        {
            pawn = p;
            m.Show(p);
            onlyOneOfTypeAllowed = false;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            layer = WindowLayer.GameUI;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = SoundDefOf.TabClose;
        }

        // ---------------- Who has a card, and opening it ----------------

        /// <summary>One of the colony's own, alive: a colonist or a slave of the colony (prisoners and the dead keep the small card).</summary>
        public static bool Fits(Pawn p) => p != null && !p.Dead && !p.Destroyed && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction != null && p.Faction.IsPlayer;

        /// <summary>Whether a card a name leads to is a colonist's card: one of the colony's own, with "Rimfolk's colonist card" on.</summary>
        public static bool Takes(ThingRef r, out Pawn p)
        {
            p = null;
            if (!On || r.IsNone || Current.Game == null || ThingRefs.CardOf(r) != CardKind.Colonist) return false;
            p = ThingRefs.Pawn(r);
            return Fits(p);
        }

        /// <summary>A colonist's card: the one open already comes to the front, else a new one opens beside any others.</summary>
        public static void Open(Pawn p)
        {
            if (!Fits(p) || Find.WindowStack == null) return;
            try
            {
                var open = For(p);
                if (open != null)
                {
                    Find.WindowStack.Notify_ClickedInsideWindow(open);
                    return;
                }
                Find.WindowStack.Add(new ColonistCardWindow(p));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: opening it", e);
            }
        }

        /// <summary>The card open for a colonist, or null.</summary>
        public static ColonistCardWindow For(Pawn p)
        {
            var windows = Find.WindowStack?.Windows;
            if (windows == null || p == null) return null;
            for (int i = 0; i < windows.Count; i++)
                if (windows[i] is ColonistCardWindow c && c.pawn == p)
                    return c;
            return null;
        }

        /// <summary>The colonists the arrows step through: the colonist bar's order, or the colony's people where the bar has none.</summary>
        internal static List<Pawn> Order()
        {
            var list = new List<Pawn>();
            try
            {
                var bar = Find.ColonistBar?.GetColonistsInOrder();
                if (bar != null)
                    foreach (var p in bar)
                        if (Fits(p) && !list.Contains(p))
                            list.Add(p);
                if (list.Count == 0)
                    foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
                        if (Fits(p) && !list.Contains(p))
                            list.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: the colonists in order", e);
            }
            return list;
        }

        // ---------------- The window ----------------

        protected override string FrameTitle => pawn?.LabelShortCap ?? "";

        protected override Texture2D FrameIcon => Icons.ColonistCard ?? Icons.People;

        protected override float FrameToolsWidth => ArrowsW;

        protected override void DrawFrameTools(Rect r) => Arrows(new Rect(r.x, r.center.y - 12f, r.width, 24f));

        private const float ArrowsW = 60f;

        /// <summary>A second card opens a title bar's height down from one already where the last was left, so each shows.</summary>
        public override void PostOpen()
        {
            base.PostOpen();
            if (!WindowFrame.MovesOn) return;
            var others = new List<FrameRect>();
            foreach (var w in Find.WindowStack.Windows)
                if (w is ColonistCardWindow c && c != this)
                    others.Add(new FrameRect(c.windowRect.x, c.windowRect.y, c.windowRect.width, c.windowRect.height));
            if (others.Count == 0) return;
            var r = ColonistCardMath.Cascaded(new FrameRect(windowRect.x, windowRect.y, windowRect.width, windowRect.height), others, UI.screenWidth, UI.screenHeight,
                frame.minW, frame.minH, frame.bottom);
            windowRect = new Rect(r.x, r.y, r.w, r.h);
        }

        public override void WindowOnGUI()
        {
            // The model first, so the title bar's arrows and the contents read the same one.
            if (Fits(pawn)) m.Refresh(pawn);
            base.WindowOnGUI();
        }

        protected override void DoFramedContents(Rect inRect)
        {
            if (!Fits(pawn))
            {
                Close();
                return;
            }
            var font = Text.Font;
            try
            {
                float y = inRect.y;
                float right = 0f;
                // Without the frame's title bar the arrows sit at the header's top right, clear of the game's close cross.
                if (!TitleInBar)
                {
                    right = ArrowsW + 34f;
                    Arrows(new Rect(inRect.xMax - right, y, ArrowsW, 24f));
                }
                Header(new Rect(inRect.x, y, inRect.width - right, ColonistCardMath.HeaderH));
                y += ColonistCardMath.HeaderH + 4f;
                UiKit.Divider(inRect.x, y, inRect.width);
                y += 6f;
                float footH = FootHeight(inRect.width);
                var body = new Rect(inRect.x, y, inRect.width, Mathf.Max(40f, inRect.yMax - footH - 8f - y));
                Body(body);
                Foot(new Rect(inRect.x, inRect.yMax - footH, inRect.width, footH));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: drawing it", e);
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
            }
        }

        // ---------------- The arrows ----------------

        private void Arrows(Rect r)
        {
            var back = new Rect(r.x + 4f, r.y, 24f, 24f);
            var forward = new Rect(back.xMax + 4f, r.y, 24f, 24f);
            if (UiKit.IconButton(back, Icons.Back, m.prevTip, m.prev != null)) StepTo(m.prev);
            if (UiKit.IconButton(forward, Icons.Forward, m.nextTip, m.next != null)) StepTo(m.next);
        }

        /// <summary>The card shows another colonist (the arrows): never one whose card is open in another window.</summary>
        private void StepTo(Pawn p)
        {
            if (!Fits(p) || p == pawn || For(p) != null) return;
            pawn = p;
            m.Show(p);
            m.Refresh(p);
            scroll = Vector2.zero;
        }

        // ---------------- The header ----------------

        private void Header(Rect r)
        {
            var tile = new Rect(r.x, r.y, ColonistCardMath.PortraitW, ColonistCardMath.PortraitH);
            if (Event.current.type == EventType.Repaint)
            {
                if (UiKit.OwnLook) UiKit.RoundBox(tile, UiKit.PortraitBack, UiKit.Edge, SkinMath.RadiusCard);
                else Widgets.DrawBoxSolidWithOutline(tile, UiKit.PortraitBack, UiKit.CardBorder, 1);
                Portrait(tile, pawn);
            }
            // The heart's line under the portrait, as the colonist bar draws it under theirs.
            if (m.heartOn)
            {
                float ly = tile.yMax + 4f;
                float sw = ColonistCardMath.HeartLine(m.strain, tile.width), gw = ColonistCardMath.HeartLine(m.good, tile.width);
                if (sw > 0f)
                {
                    Widgets.DrawBoxSolid(new Rect(tile.x, ly, sw, 3f), m.strainColor);
                    ly += 4f;
                }
                if (gw > 0f) Widgets.DrawBoxSolid(new Rect(tile.x, ly, gw, 3f), m.goodColor);
                if (!m.heartTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(tile.x, tile.yMax + 2f, tile.width, 10f), m.heartTip);
            }
            float tx = tile.xMax + 14f, tw = Mathf.Max(0f, r.xMax - tx);
            UiKit.LabelFit(new Rect(tx, r.y, tw, 32f), m.name ?? pawn.LabelCap, UiKit.Ink, GameFont.Medium);
            float y = r.y + 32f;
            if (!m.who.NullOrEmpty())
            {
                var line = new Rect(tx, y, tw, 22f);
                StoryGlance.Plain(line, m.who, UiKit.Heading);
                if (!m.whoTip.NullOrEmpty()) TooltipHandler.TipRegion(line, m.whoTip);
                y += 24f;
            }
            if (m.chips.Count > 0) UiKit.Pills(tx, y + 2f, tw, m.chips);
        }

        /// <summary>Their whole figure as dressed, from the game's portrait cache (the size of the header's tile).</summary>
        private static void Portrait(Rect r, Pawn p)
        {
            try
            {
                var tex = PortraitsCache.Get(p, new Vector2(Mathf.Round(r.width), Mathf.Round(r.height)), Rot4.South, default(Vector3), 1f);
                GUI.color = Color.white;
                GUI.DrawTexture(r, tex);
            }
            catch (Exception)
            {
                Widgets.ThingIcon(r, p);
            }
        }

        // ---------------- The body: the parts in columns ----------------

        private void Body(Rect body)
        {
            float w = body.width - 16f;
            int n = ColonistCardMath.Columns(w);
            float cw = ColonistCardMath.ColumnWidth(w, n);
            var parts = ColonistCardMath.Parts(n);
            var view = new Rect(0f, 0f, w, Mathf.Max(viewH, body.height - 1f));
            Widgets.BeginScrollView(body, ref scroll, view);
            try
            {
                float tallest = 0f;
                for (int c = 0; c < parts.Length; c++)
                {
                    float x = ColonistCardMath.ColumnX(w, n, c), y = 0f;
                    foreach (var part in parts[c])
                    {
                        float h = Part(part, x, y, cw);
                        if (h > 0f) y += h + 14f;
                    }
                    tallest = Mathf.Max(tallest, y);
                }
                viewH = Mathf.Max(tallest, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private float Part(CardPart part, float x, float y, float w)
        {
            try
            {
                switch (part)
                {
                    case CardPart.Now: return Now(x, y, w);
                    case CardPart.Skills: return Skills(x, y, w);
                    case CardPart.Kit: return Kit(x, y, w);
                    default: return Life(x, y, w);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: drawing " + part, e);
                return 0f;
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
            }
        }

        // ---------------- Now ----------------

        private float Now(float x, float y, float w)
        {
            float top = y;
            y += UiKit.SectionTitle(x, y, w, Words.Now, Icons.Moods);
            if (m.mood != null)
            {
                MoodView.DrawBar(new Rect(x, y + 2f, w, 18f), m.mood);
                y += 26f;
            }
            if (m.glance != null) y += m.glance.Draw(x, y, w);
            if (m.noMood != null) y += Quiet(x, y, w, Icons.Moods, m.noMood);
            return y - top;
        }

        // ---------------- Skills ----------------

        private float Skills(float x, float y, float w)
        {
            float top = y;
            y += UiKit.SectionTitle(x, y, w, Words.Skills, Icons.CatNumbers);
            if (m.noSkills != null) return y - top + Quiet(x, y, w, Icons.CatNumbers, m.noSkills);
            if (m.best != null) y += UiKit.Paragraph(x, y, w, m.best, UiKit.Faint, GameFont.Tiny) + 4f;
            foreach (var s in m.skills)
            {
                SkillRow(new Rect(x, y, w, ColonistCardMath.SkillRowH), s);
                y += ColonistCardMath.SkillRowH;
            }
            return y - top;
        }

        private static void SkillRow(Rect r, ColonistCardModel.SkillRow s)
        {
            UiKit.RowHover(r);
            float labelW = Mathf.Min(104f, r.width * 0.38f), levelW = 24f, passionW = 18f;
            StoryGlance.Plain(new Rect(r.x + 2f, r.y, labelW - 2f, r.height), s.label, s.disabled ? UiKit.Dim : s.best ? UiKit.Gold : UiKit.Ink);
            var bar = new Rect(r.x + labelW + 4f, r.y + 6f, Mathf.Max(10f, r.width - labelW - 4f - passionW - levelW - 4f), 8f);
            if (s.disabled) StoryGlance.Plain(new Rect(bar.x, r.y, bar.width, r.height), Words.Cant, UiKit.Dim, GameFont.Tiny);
            else UiKit.Bar(bar, s.fill, s.best ? UiKit.Gold : UiKit.Cool);
            if (s.passion != null) UiKit.Icon(new Rect(bar.xMax + 3f, r.y + 3f, 14f, 14f), s.passion, s.passionColor);
            StoryGlance.Plain(new Rect(r.xMax - levelW, r.y, levelW - 2f, r.height), s.level, s.disabled ? UiKit.Dim : UiKit.Ink, GameFont.Small, TextAnchor.MiddleRight);
            if (!s.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, s.tip);
        }

        // ---------------- Kit ----------------

        private float Kit(float x, float y, float w)
        {
            float top = y;
            y += UiKit.SectionTitle(x, y, w, Words.Kit, Icons.CatDefense);
            y += SubHead(x, y, w, Words.Weapons);
            foreach (var row in m.weapons)
            {
                Gear(new Rect(x, y, w, ColonistCardMath.WeaponRowH), row, 34f);
                y += ColonistCardMath.WeaponRowH + 2f;
            }
            if (m.noWeapon != null) y += Quiet(x, y, w, HudIcons.KitGap ?? Icons.CatDefense, m.noWeapon);
            foreach (var l in m.kitLines) y += Line(x, y, w, l) + 2f;
            y += 6f;
            y += SubHead(x, y, w, Words.Apparel);
            foreach (var row in m.worn)
            {
                Gear(new Rect(x, y, w, ColonistCardMath.WornRowH), row, 26f);
                y += ColonistCardMath.WornRowH + 2f;
            }
            if (m.noWorn != null) y += Quiet(x, y, w, Icons.CatDefense, m.noWorn);
            if (m.weather != null)
            {
                var r = new Rect(x, y, w, 22f);
                UiKit.RowHover(r);
                UiKit.Icon(new Rect(x + 1f, y + 3f, 16f, 16f), HudIcons.Temperature ?? Icons.Warning, m.weatherColor);
                StoryGlance.Plain(new Rect(x + 22f, y, w - 22f, r.height), m.weather, m.weatherColor == UiKit.Faint ? UiKit.Faint : UiKit.Ink, GameFont.Tiny);
                if (!m.weatherTip.NullOrEmpty()) TooltipHandler.TipRegion(r, m.weatherTip);
                y += 24f;
            }
            foreach (var l in m.hookLines) y += Line(x, y, w, l) + 2f;
            return y - top;
        }

        private static float SubHead(float x, float y, float w, string text)
        {
            StoryGlance.Plain(new Rect(x, y, w, 18f), text, UiKit.Faint, GameFont.Tiny);
            return 18f;
        }

        /// <summary>A weapon or a worn piece: its picture in its quality's frame, its name, what it is to them and their rating, the badge; a click opens its card.</summary>
        private void Gear(Rect r, ColonistCardModel.GearRow row, float tile)
        {
            if (row.thing == null) return;
            UiKit.RowHover(r);
            int tipId = row.thing.thingIDNumber ^ 0x1A7E;
            var pic = new Rect(r.x + 1f, r.y + (r.height - tile) / 2f, tile, tile);
            UiKit.ItemPortrait(pic, row.thing, tip: row.tip);
            float bw = WeaponGearTab.BadgeW, tx = pic.xMax + 6f;
            bool rated = !float.IsNaN(row.rating);
            if (rated) WeaponUi.Badge(new Rect(r.xMax - bw - 2f, r.y + 3f, bw, WeaponGearTab.BadgeH), row.rating);
            float tw = Mathf.Max(0f, r.xMax - tx - (rated ? bw + 8f : 4f));
            bool two = r.height >= 30f && !row.second.NullOrEmpty();
            StoryGlance.Plain(new Rect(tx, r.y + (two ? 1f : 0f), tw, two ? 18f : r.height), row.name, row.nameColor == Color.white ? UiKit.Ink : row.nameColor);
            if (two) StoryGlance.Plain(new Rect(tx, r.y + 17f, Mathf.Max(0f, r.xMax - tx - 2f), 14f), row.second, UiKit.Faint, GameFont.Tiny);
            if (row.tip != null) TooltipHandler.TipRegion(r, row.tip, tipId);
            if (!Widgets.ButtonInvisible(r, false)) return;
            Cards.Click();
            OpenItem(row.thing, row.weapon);
        }

        /// <summary>A weapon's or a piece's card: Rimfolk's own where it rates it, else the game's.</summary>
        private void OpenItem(Thing t, bool weapon)
        {
            if (t == null) return;
            if (weapon && WeaponCardWindow.On && WeaponLive.Rateable(t.def)) WeaponCardWindow.Open(t, pawn);
            else if (!weapon && ApparelCardWindow.On && ApparelLive.Rateable(t.def)) ApparelCardWindow.Open(t, pawn);
            else Find.WindowStack.Add(new Dialog_InfoCard(t));
        }

        /// <summary>A line of the kit (an empty slot, a better weapon, a feature's line): its icon, its words cut to the column, its act at the right.</summary>
        private float Line(float x, float y, float w, ColonistCardLine l)
        {
            const float h = 22f;
            var r = new Rect(x, y, w, h);
            UiKit.RowHover(r);
            UiKit.Icon(new Rect(x + 1f, y + 3f, 16f, 16f), l.icon ?? Icons.Warning, l.color);
            float aw = l.act != null ? UiKit.RowActWidth(l.act) : 0f;
            var words = new Rect(x + 22f, y, Mathf.Max(0f, w - 22f - (aw > 0f ? aw + 6f : 0f)), h);
            StoryGlance.Plain(words, l.text, UiKit.Ink, GameFont.Tiny);
            if (!l.tip.NullOrEmpty()) TooltipHandler.TipRegion(words, l.tip);
            if (l.thing != null && Widgets.ButtonInvisible(words, false))
            {
                Cards.Click();
                OpenItem(l.thing, l.thing.def.IsWeapon);
            }
            if (aw > 0f && UiKit.RowAct(new Rect(x + w - aw, y + 1f, aw, h - 2f), l.act, l.whyNot, l.actTip))
            {
                try
                {
                    l.run?.Invoke();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a colonist's card: a kit's act", e);
                }
                m.Stale();
            }
            return h;
        }

        // ---------------- Life ----------------

        private float Life(float x, float y, float w)
        {
            float top = y;
            y += UiKit.SectionTitle(x, y, w, Words.Life, Icons.LifeGoal);
            if (m.story != null && !m.story.Empty) y += m.story.Draw(x, y, w);
            foreach (var line in m.also) y += LinkText.Draw(new Rect(x, y, w, 9999f), line, UiKit.Faint, GameFont.Tiny) + 3f;
            return y - top;
        }

        /// <summary>A quiet line with its icon ("Nothing in hand.").</summary>
        private static float Quiet(float x, float y, float w, Texture2D icon, string text)
        {
            UiKit.Icon(new Rect(x + 1f, y + 3f, 16f, 16f), icon, UiKit.Dim);
            StoryGlance.Plain(new Rect(x + 22f, y, Mathf.Max(0f, w - 22f), 22f), text, UiKit.Faint, GameFont.Tiny);
            return 24f;
        }

        // ---------------- The foot ----------------

        private enum FootAct
        {
            Mood,
            Story,
            Gear,
            Health,
            GoTo,
            Select,
            WholeStory,
            More
        }

        private static readonly List<float> footWidths = new List<float>(8);

        private int FootCount => m.acts.Count > 0 ? 8 : 7;

        private static float FootWidth(int i) => Mathf.Max(64f, UiKit.KeptWidth(Words.Foot(i), GameFont.Small) + 24f);

        private float FootHeight(float width)
        {
            footWidths.Clear();
            for (int i = 0; i < FootCount; i++) footWidths.Add(FootWidth(i));
            int rows = ColonistCardMath.FootRows(footWidths, width, ColonistCardMath.FootGap);
            return rows * ColonistCardMath.FootButtonH + (rows - 1) * ColonistCardMath.FootGap;
        }

        private void Foot(Rect r)
        {
            float x = r.x, y = r.y;
            for (int i = 0; i < FootCount; i++)
            {
                float bw = FootWidth(i);
                if (x > r.x && x + bw > r.xMax + 0.01f)
                {
                    x = r.x;
                    y += ColonistCardMath.FootButtonH + ColonistCardMath.FootGap;
                }
                var b = new Rect(x, y, bw, ColonistCardMath.FootButtonH);
                x += bw + ColonistCardMath.FootGap;
                var which = (FootAct)i;
                string why = which <= FootAct.Health ? m.tabsWhyNot : null;
                bool can = why == null;
                if (UiKit.Button(b, Words.Foot(i), ButtonKind.Normal, can, can ? m.footTips[i] : why) && can) Do(which);
            }
        }

        private void Do(FootAct which)
        {
            var p = pawn;
            try
            {
                switch (which)
                {
                    case FootAct.Mood:
                        OpenTab(p, ColonyCouncilMod.Settings.moodInsight ? typeof(ITab_Pawn_Mood) : typeof(ITab_Pawn_Needs));
                        break;
                    case FootAct.Story:
                        OpenTab(p, typeof(ITab_Pawn_Story));
                        break;
                    case FootAct.Gear:
                        OpenTab(p, typeof(ITab_Pawn_Gear));
                        break;
                    case FootAct.Health:
                        OpenTab(p, typeof(ITab_Pawn_Health));
                        break;
                    case FootAct.GoTo:
                        Pages.LeaveForMap();
                        CameraJumper.TryJump(p);
                        break;
                    case FootAct.Select:
                        Pages.LeaveForMap();
                        CameraJumper.TryJumpAndSelect(p);
                        break;
                    case FootAct.WholeStory:
                        MainTabWindow_Council.OpenPerson(p);
                        break;
                    case FootAct.More:
                        More();
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: " + which, e);
            }
        }

        /// <summary>One of the game's tabs for them: selected (the camera going to them), then the tab opened in the inspect pane.</summary>
        private static void OpenTab(Pawn p, Type tab)
        {
            Pages.LeaveForMap();
            CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
            // Someone carried selects whoever holds them: open nothing on the wrong person.
            if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(tab);
        }

        /// <summary>What else other parts let the colony do from a colonist's card, in a menu, each greyed with why not.</summary>
        private void More()
        {
            var options = new List<FloatMenuOption>();
            foreach (var a in m.acts)
            {
                var act = a;
                options.Add(act.why == null
                    ? new FloatMenuOption(act.label, () =>
                    {
                        act.run?.Invoke();
                        Cards.Dirty();
                        m.Stale();
                    })
                    : new FloatMenuOption(act.label + " (" + act.why + ")", null));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        // ---------------- Words, translated once ----------------

        private static class Words
        {
            private static string now, skills, kit, life, weapons, apparel, cant;
            private static readonly string[] foot = new string[8];

            public static string Now => now ??= "CC_CCard_Now".Translate();
            public static string Skills => skills ??= "CC_CCard_Skills".Translate();
            public static string Kit => kit ??= "CC_CCard_Kit".Translate();
            public static string Life => life ??= "CC_CCard_Life".Translate();
            public static string Weapons => weapons ??= "CC_CCard_Weapons".Translate();
            public static string Apparel => apparel ??= "CC_CCard_Apparel".Translate();
            public static string Cant => cant ??= "CC_CCard_Cant".Translate();

            public static string Foot(int i) => foot[i] ??= ColonistCardModel.FootKeys[i].Translate();
        }
    }
}
