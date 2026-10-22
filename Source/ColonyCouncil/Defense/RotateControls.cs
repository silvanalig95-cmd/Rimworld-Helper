using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's rotate buttons above the designator bar (as when placing a building), with the rotate keys' labels on
    /// them, for Rimfolk's own placing tools: the defense line's spot and a defense plan placed by hand.
    /// </summary>
    public static class RotateControls
    {
        private const int WindowId = 0x52464C4E;

        /// <summary>Draws the two buttons; <paramref name="rotate"/> gets -1 (counter-clockwise) or 1 (clockwise).</summary>
        public static void Draw(float leftX, float bottomY, Action<int> rotate)
        {
            if (rotate == null) return;
            var winRect = new Rect(leftX, bottomY - 90f, 200f, 90f);
            Find.WindowStack.ImmediateWindow(WindowId, winRect, WindowLayer.GameUI, () =>
            {
                var oldAnchor = Text.Anchor;
                var oldFont = Text.Font;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                var left = new Rect(winRect.width / 2f - 64f - 5f, 15f, 64f, 64f);
                if (Widgets.ButtonImage(left, TexUI.RotLeftTex))
                {
                    SoundDefOf.DragSlider.PlayOneShotOnCamera();
                    rotate(-1);
                }
                Widgets.Label(left, KeyBindingDefOf.Designator_RotateLeft.MainKeyLabel);
                var right = new Rect(winRect.width / 2f + 5f, 15f, 64f, 64f);
                if (Widgets.ButtonImage(right, TexUI.RotRightTex))
                {
                    SoundDefOf.DragSlider.PlayOneShotOnCamera();
                    rotate(1);
                }
                Widgets.Label(right, KeyBindingDefOf.Designator_RotateRight.MainKeyLabel);
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            });
        }

        /// <summary>The rotate keys, for a designator's SelectedProcessInput: -1, 1, or 0 for neither.</summary>
        public static int FromKeys()
        {
            if (Event.current == null || Event.current.type != EventType.KeyDown) return 0;
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                Event.current.Use();
                return 1;
            }
            if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                Event.current.Use();
                return -1;
            }
            return 0;
        }
    }
}
