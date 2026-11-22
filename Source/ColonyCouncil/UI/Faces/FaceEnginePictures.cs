using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Portraits, phase 3: a whole picture of a person from a portrait source (the library's painted portraits,
    /// <see cref="PortraitSources"/>) drawn in place of the game's own drawing, with everything else of the portrait as
    /// before: their colour behind, the light behind the head (where the picture's head is), the mark of their people,
    /// the frame. Framed by the picture's own layout for each place (<see cref="PortraitViewMath.View"/>), so its face
    /// sits where the game's face sat; the pose as small transforms of the whole picture (the head lifted or lowered by
    /// moving the view, the lean and half the head's tilt as a turn about the foot; in round portraits the lift only);
    /// never mirrored (the player, 22 November 2026: "They should, you know, always look the same direction if they're
    /// represented on different parts of the UI"; the game's own drawing still turns to face its text or the other of a
    /// pair); the dead grey, a grey copy made once.
    /// </summary>
    public static partial class FaceEngine
    {
        /// <summary>The view of a source's picture for a frame in a tile this size, lifted or lowered by the pose.</summary>
        private static PictureView ViewOf(PortraitPicture pic, FaceFrame frame, Vector2 tile, FaceBearing bearing, int seed, out float turn)
        {
            PortraitViewMath.Pose(FacePoseMath.Shift(bearing, FaceTurn.Front, seed), out float lift, out turn);
            return PortraitViewMath.View(pic.layout, frame, tile.y > 0f ? tile.x / tile.y : 1f, lift);
        }

        /// <summary>The camera numbers the light behind the head reads, for a picture's view.</summary>
        private static FaceCamera CameraOf(PictureView v) => new FaceCamera { headU = v.headU, headV = v.headV, headShare = v.headShare, zoom = 1f };

        /// <summary>
        /// A source's picture in a tile (inside its group): turned about the tile's foot by the pose's turn and the sway,
        /// breathing from the foot, as painted (never mirrored), grey for the dead. The picture carries on below the tile's
        /// foot (the group cuts it off), so the sway's turn never shows a gap under the body.
        /// </summary>
        private static void DrawPicture(Rect r, PortraitPicture pic, PictureView view, FaceFrame frame, FacePose pose, float turn, float alpha, float breath, float sway)
        {
            Texture tex = pose.bearing == FaceBearing.Dead ? GreyPicture(pic.texture) ?? pic.texture : pic.texture;
            PortraitViewMath.TexCoords(view, out float u, out float v, out float w, out float h);
            var saved = GUI.matrix;
            try
            {
                float lean = turn + sway;
                if (lean != 0f) UI.RotateAroundPivot(lean, new Vector2(r.center.x, r.yMax));
                GUI.color = UiKit.WithAlpha(UiKit.Plain, alpha);
                float ph = Mathf.Max(1f, r.height * breath), below = FootBelow * r.width;
                // Below the view's foot: the picture itself where there is more of it, else its foot row carried on.
                GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - ph, r.width, ph + below), tex, new Rect(u, v - h * below / ph, w, h * (1f + below / ph)));
            }
            finally
            {
                GUI.matrix = saved;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// A source's picture in a round portrait: the light behind the head, then the picture in the circle, lifted or
        /// lowered by the pose, as painted (never mirrored).
        /// </summary>
        private static void DrawPictureRound(Rect inner, Pawn p, PortraitPicture pic, FacePose pose, float alpha)
        {
            var view = ViewOf(pic, FaceFrame.Face, inner.size, pose.bearing, p.thingIDNumber, out _);
            bool dead = pose.bearing == FaceBearing.Dead;
            if (!dead && inner.width >= 30f) DrawGlow(inner, CameraOf(view), GlowColour(p), alpha);
            Texture tex = dead ? GreyPicture(pic.texture) ?? pic.texture : pic.texture;
            PortraitViewMath.TexCoords(view, out float u, out float v, out float w, out float h);
            GUI.color = UiKit.WithAlpha(UiKit.Plain, alpha);
            UiKit.DrawInCircle(inner, tex, new Rect(u, v, w, h));
            GUI.color = Color.white;
        }

        // ---------------- The dead's grey, for a picture ----------------

        private sealed class GreyPic
        {
            public Texture2D tex;
            public float lastUsed;
        }

        private static readonly Dictionary<Texture2D, GreyPic> greyPics = new Dictionary<Texture2D, GreyPic>();
        private static readonly List<Texture2D> dropGreyPics = new List<Texture2D>();

        /// <summary>The grey copies are made at most this large (the pictures may be larger).</summary>
        private const int GreyMost = 256;

        /// <summary>
        /// A grey copy of a picture (by brightness as the eye sees it, a little dimmed, as the game's face is greyed): made
        /// once on a texture of Rimfolk's own (the game keeps loaded textures unreadable), let go when no longer shown.
        /// </summary>
        private static Texture GreyPicture(Texture2D from)
        {
            if (from == null) return null;
            float now = Time.realtimeSinceStartup;
            if (greyPics.TryGetValue(from, out var g) && g.tex != null)
            {
                g.lastUsed = now;
                return g.tex;
            }
            int w = Mathf.Max(1, Mathf.Min(GreyMost, from.width)), h = Mathf.Max(1, Mathf.RoundToInt((float)w * from.height / Mathf.Max(1, from.width)));
            RenderTexture rt = null;
            var active = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(from, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, name = "RimfolkPictureGrey" };
                tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    byte l = FacePoseMath.GreyOf(c.r, c.g, c.b);
                    px[i] = new Color32(l, l, l, c.a);
                }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                greyPics[from] = new GreyPic { tex = tex, lastUsed = now };
                return tex;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the grey picture of the dead, from a painted portrait", e);
                return null;
            }
            finally
            {
                RenderTexture.active = active;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Lets go of the grey copies not shown for a few seconds.</summary>
        private static void PruneGreyPictures()
        {
            if (greyPics.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            dropGreyPics.Clear();
            foreach (var kv in greyPics)
                if (now - kv.Value.lastUsed > 5f)
                    dropGreyPics.Add(kv.Key);
            foreach (var k in dropGreyPics)
            {
                if (greyPics[k].tex != null) UnityEngine.Object.Destroy(greyPics[k].tex);
                greyPics.Remove(k);
            }
        }
    }
}
