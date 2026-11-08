using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A card opened from outside the region window (another of Rimfolk's windows, a panel, one of Rimfolk's letters): a
    /// small floating window with the same card, a trail of the cards followed in it, and "Open in its window", which
    /// opens the card where it lives (a colonist in the People roster, a place on the region map, the rest in the region
    /// window's drawer). One at a time; a new card replaces the one showing. It never pauses the game and leaves the
    /// camera free, as the inspect pane does. In Rimfolk's frame (key "Card"), the card's name in its title bar.
    /// </summary>
    public class Window_Card : FramedWindow
    {
        private readonly List<ThingRef> trail = new List<ThingRef>();
        private int at;
        private readonly CardState state = new CardState();

        public override Vector2 InitialSize => new Vector2(420f, Mathf.Min(600f, UI.screenHeight - 120f));

        public ThingRef Current => at >= 0 && at < trail.Count ? trail[at] : ThingRef.None;

        public Window_Card(ThingRef r) : base("Card")
        {
            trail.Add(r);
            at = 0;
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            layer = WindowLayer.GameUI;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = SoundDefOf.TabClose;
        }

        protected override string FrameTitle => ThingRefs.Name(Current);

        protected override Texture2D FrameIcon => Icons.Pin;

        /// <summary>Shows a card in the floating window: the one open follows it (a trail), or a new one opens.</summary>
        public static void Show(ThingRef r)
        {
            if (r.IsNone) return;
            var open = Find.WindowStack.WindowOfType<Window_Card>();
            if (open != null)
            {
                open.Follow(r);
                return;
            }
            Find.WindowStack.Add(new Window_Card(r));
        }

        private void Follow(ThingRef r)
        {
            int i = trail.IndexOf(r);
            if (i >= 0)
            {
                at = i;
                return;
            }
            if (at < trail.Count - 1) trail.RemoveRange(at + 1, trail.Count - at - 1);
            trail.Add(r);
            if (trail.Count > 8) trail.RemoveAt(0);
            at = trail.Count - 1;
        }

        protected override void DoFramedContents(Rect inRect)
        {
            var r = Current;
            if (r.IsNone)
            {
                Close();
                return;
            }
            try
            {
                float y = inRect.y;
                // Clear of the game's close cross; the frame's is in its title bar.
                var bar = new Rect(inRect.x, y, inRect.width - (TitleInBar ? 0f : 30f), 26f);
                float x = bar.x;
                if (at > 0)
                {
                    if (UiKit.IconButton(new Rect(x, y + 1f, 24f, 24f), Icons.Back ?? TexButton.ReorderUp, "CC_Nav_Back".Translate())) at--;
                    x += 30f;
                }
                string open = "CC_Card_OpenInWindow".Translate();
                Text.Font = GameFont.Small;
                float bw = Mathf.Min(Text.CalcSize(open).x + 24f, bar.xMax - x);
                if (UiKit.Button(new Rect(x, y, bw, 26f), open))
                {
                    var thing = r;
                    Close();
                    Cards.OpenInWindow(thing);
                    return;
                }
                y += 34f;
                CardView.Draw(new Rect(inRect.x, y, inRect.width, inRect.yMax - y), r, state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: the floating card", e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            // Its own links' previews and a click on a link inside it follow in this window.
        }
    }
}
