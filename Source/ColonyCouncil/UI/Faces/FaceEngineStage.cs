using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Portraits, phases 4 and 5: the large portrait at the inspect pane in front of a backdrop of where the person is
    /// (docs: DESIGN.md, "Portraits, phase 4: the painted library in the game, with backdrops" and "Portraits, phase 5";
    /// <c>docs/portrait-art/FRAMEWORK.md</c>, "How they go together").
    /// <list type="bullet">
    /// <item>A painted picture is shown whole (the player, 22 November 2026: "I'd rather, you know, see as much as I can"):
    /// the portrait takes the picture's shape (<see cref="StageTall"/>, the size setting its width), the person from the
    /// top of the head to where the body runs off the picture's foot.</item>
    /// <item>The game's own drawing, where the library has no picture for them, stands in front of the backdrop too (the
    /// player: "it still takes backdrop from the library, right? That's the least it can do"), with a soft shadow behind
    /// it, while "Backdrop behind the game's own drawing too" is on (<see cref="DrawGameStaged"/>); the portrait stays
    /// square.</item>
    /// <item>The backdrop by the room's kind, the colony's age and what is in the room, or the land outdoors
    /// (<see cref="Backdrops"/>); looked at again only when the person's room, map or the light's step changes, at most
    /// every two seconds.</item>
    /// <item>Made once per person, picture, backdrop, size, settings and step of light, on a worker thread from the small
    /// pictures already loaded (<see cref="PortraitComposeMath"/>), and kept as textures (the backdrop, and the person
    /// with the line round them), so each frame only draws them; the person breathes and sways in front of a backdrop that
    /// stays still, the body's foot carried on below the portrait's edge so no gap ever shows under it. The last few are
    /// kept, so turning back to someone shows them at once.</item>
    /// <item>While a picture is made the one before for the same person shows, or the portrait's ground for a moment.</item>
    /// </list>
    /// </summary>
    public static partial class FaceEngine
    {
        private sealed class Staged
        {
            public string key;
            public int pawn;
            public string picture;
            public Texture2D back, person;
            /// <summary>Where the head is in the picture (its middle across and down, its share of the height), for the pose's lift and the light behind it.</summary>
            public float headU = 0.5f, headV = 0.3f, headShare = 0.3f;
            /// <summary>The tint toward the room's light, for the game's own drawing (1 for none).</summary>
            public float tr = 1f, tg = 1f, tb = 1f;
            public float lastUsed;
        }

        private sealed class StageLook
        {
            public float until;
            public string whereKey;
            public PictureFile backdrop;
            public int step = -1;
        }

        private sealed class StageParts
        {
            public byte[] back, person;
            public float tr = 1f, tg = 1f, tb = 1f;
        }

        private const int StagedMost = 4;

        /// <summary>How often where the person is and the light are looked at again, in seconds.</summary>
        private const float StageLookEvery = 2f;

        /// <summary>The name the game's own drawing on a backdrop is kept under (no picture of the library is called so).</summary>
        private const string GameDrawing = "\u0001game";

        /// <summary>How far the body's foot is carried on below the portrait's edge, as a share of its width (the sway's turn about the foot lifts a corner by about this much at most).</summary>
        private const float FootBelow = 0.12f;

        private static readonly List<Staged> staged = new List<Staged>();
        private static readonly Dictionary<int, StageLook> stageLooks = new Dictionary<int, StageLook>();
        private static readonly HashSet<string> stageFailed = new HashSet<string>();
        private static string staging;

        private static PortraitBackdropMode BackdropMode => S?.portraitBackdrop ?? PortraitBackdropMode.AsPainted;

        private static PortraitOutlineMode OutlineMode => S?.portraitOutline ?? PortraitOutlineMode.Off;

        private static bool LightTintOn => S?.portraitLightTint ?? false;

        private static bool BehindGame => (S?.portraitLibrary ?? true) && (S?.portraitBackdropGame ?? true);

        /// <summary>
        /// The large portrait's inside height for each unit of its width for this person: a painted picture's own shape
        /// (<see cref="PortraitPictureMath.TallFor"/>; the library's 1024 x 1280 while it loads), square for the game's
        /// own drawing.
        /// </summary>
        public static float StageTall(Pawn p)
        {
            if (p == null) return 1f;
            try
            {
                var pic = PortraitSources.For(p);
                if (pic?.file == null) return 1f;
                return PortraitPictureMath.TallFor(pic.texture != null ? pic.layout.Shape : PortraitPictureMath.LibraryShape);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait: its shape", e);
                return 1f;
            }
        }

        private static int Px(float size, int most) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1f, size) * Prefs.UIScale), 16, most);

        /// <summary>
        /// The large portrait with a painted picture, whole, its backdrop behind it; false where the person has no picture
        /// from the library (the caller draws the portrait as before). <paramref name="renderSize"/> is the size it is made
        /// at (the size before a drag, while dragging).
        /// </summary>
        public static bool DrawStaged(Rect r, Pawn p, Vector2 renderSize, bool moving)
        {
            if (p == null || Event.current.type != EventType.Repaint) return false;
            Tick();
            var pic = PortraitSources.For(p);
            if (pic?.file == null) return false;
            var l = LookOf(p);
            var pose = PoseOf(p, FaceTurn.Front, true);
            if (pose.bearing == FaceBearing.Dead) return false;
            Widgets.DrawBoxSolid(r, l.back);
            if (pic.texture == null) return true;
            int w = Px(renderSize.x, 1024), h = Px(renderSize.y, 1600);
            var look = StageLookOf(p);
            var mode = BackdropMode;
            Texture2D backTex = null;
            bool waiting = false;
            if (PortraitComposeMath.WhatToDraw(true, look.backdrop != null, mode, BehindGame) == PortraitStageDraw.PaintedOnBackdrop)
            {
                backTex = PortraitStore.Get(look.backdrop);
                waiting = backTex == null && !PortraitStore.Failed(look.backdrop);
            }
            string key = p.thingIDNumber + "|" + pic.name + "|" + (backTex != null ? look.backdrop.key : "-") + "|" + w + "x" + h + "|" + (int)mode + (int)OutlineMode + (LightTintOn ? 1 : 0)
                         + "|" + look.step;
            if (stageFailed.Contains(key)) return false;
            var now = staged.Find(s => s.key == key);
            if (now == null && !waiting && staging == null) StartStage(key, p, pic, backTex, w, h, look.step, mode);
            // While it is made: the one before for the same person and picture, else the ground.
            var show = now ?? Latest(p.thingIDNumber, pic.name);
            if (show == null) return true;
            show.lastUsed = Time.realtimeSinceStartup;
            DrawStagedParts(r, p, show, pose, l.glow, moving);
            return true;
        }

        /// <summary>
        /// The large portrait with the game's own drawing in front of the backdrop of where they are (the player, 22
        /// November 2026: "when no appropriate image can be found for a pawn, and it just shows its default sprite ... it
        /// still takes backdrop from the library, right? That's the least it can do"): where the library has no painted
        /// picture for them, the setting "Backdrop behind the game's own drawing too" is on, "Backdrop" isn't plain and a
        /// backdrop was found for where they are (<see cref="PortraitComposeMath.WhatToDraw"/>). The backdrop is chosen,
        /// softened, darkened by the light and kept as for a painted picture; the drawing in front as the portrait draws it,
        /// with a soft shadow behind it and the line round it where that is on. False otherwise (the caller draws the
        /// portrait on its own colour), and for the moment the backdrop takes to load.
        /// </summary>
        public static bool DrawGameStaged(Rect r, Pawn p, Vector2 renderSize, bool moving)
        {
            if (p == null || Event.current.type != EventType.Repaint) return false;
            var mode = BackdropMode;
            if (!BehindGame || mode == PortraitBackdropMode.Plain || Backdrops.Count == 0) return false;
            Tick();
            if (PortraitSources.For(p) != null) return false;
            var pose = PoseOf(p, FaceTurn.Front, true);
            if (pose.bearing == FaceBearing.Dead) return false;
            var look = StageLookOf(p);
            if (PortraitComposeMath.WhatToDraw(false, look.backdrop != null, mode, true) != PortraitStageDraw.GameOnBackdrop) return false;
            int w = Px(renderSize.x, 1024), h = Px(renderSize.y, 1600);
            var backTex = PortraitStore.Get(look.backdrop);
            string key = p.thingIDNumber + "|" + GameDrawing + "|" + look.backdrop.key + "|" + w + "x" + h + "|" + (int)mode + (LightTintOn ? 1 : 0) + "|" + look.step;
            if (stageFailed.Contains(key)) return false;
            var now = staged.Find(s => s.key == key);
            if (now == null && backTex != null && staging == null) StartBackStage(key, p, backTex, w, h, look.step, mode);
            var show = now ?? Latest(p.thingIDNumber, GameDrawing);
            if (show == null) return false;
            show.lastUsed = Time.realtimeSinceStartup;
            GUI.DrawTexture(r, show.back);
            DrawGameFigure(r, p, pose, renderSize, moving, show);
            return true;
        }

        private static Staged Latest(int pawn, string picture)
        {
            Staged best = null;
            foreach (var s in staged)
                if (s.pawn == pawn && s.picture == picture && (best == null || s.lastUsed > best.lastUsed))
                    best = s;
            return best;
        }

        /// <summary>Where the person is and the light, looked at again at most every two seconds; the backdrop chosen again only when where they are changes.</summary>
        private static StageLook StageLookOf(Pawn p)
        {
            float now = Time.realtimeSinceStartup;
            if (!stageLooks.TryGetValue(p.thingIDNumber, out var look))
            {
                if (stageLooks.Count > 32) stageLooks.Clear();
                look = new StageLook();
                stageLooks[p.thingIDNumber] = look;
            }
            if (now < look.until) return look;
            look.until = now + StageLookEvery;
            try
            {
                var where = Backdrops.WhereOf(p, out float glow);
                look.step = PortraitPictureMath.LightStep(glow, look.step);
                string wk = where.Key;
                if (wk != look.whereKey)
                {
                    look.whereKey = wk;
                    look.backdrop = Backdrops.For(where, out _);
                    Backdrops.Note(where, where.seed);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait's backdrop: where the person is", e);
            }
            return look;
        }

        /// <summary>The middle of a backdrop with the portrait's shape, read back from the graphics card at <paramref name="w"/> by <paramref name="h"/> (rows from the foot).</summary>
        private static byte[] ReadBackdrop(Texture2D backTex, int w, int h)
        {
            PortraitPictureMath.BackdropPart(backTex.width, backTex.height, (float)w / h, out float sx, out float sy, out float ox, out float oy);
            return PortraitGpu.Read(backTex, w, h, new Vector2(sx, sy), new Vector2(ox, oy));
        }

        /// <summary>
        /// Starts making the picture: the whole person and the backdrop's middle read back small from the graphics card (on
        /// the game's thread, as it must), then softened, lined and tinted on a worker thread, then made into two textures one
        /// frame after.
        /// </summary>
        private static void StartStage(string key, Pawn p, PortraitPicture pic, Texture2D backTex, int w, int h, int step, PortraitBackdropMode mode)
        {
            try
            {
                var lay = pic.layout;
                // The whole picture at the portrait's size (its shape is the picture's own): nothing cropped.
                var personPx = PortraitGpu.Read(pic.texture, w, h, Vector2.one, Vector2.zero);
                if (personPx == null)
                {
                    stageFailed.Add(key);
                    return;
                }
                byte[] backPx = backTex != null ? ReadBackdrop(backTex, w, h) : null;
                var backLook = PortraitComposeMath.LookOf(mode);
                var lineLook = PortraitComposeMath.LookOf(OutlineMode);
                bool tint = LightTintOn;
                int pawn = p.thingIDNumber;
                string picture = pic.name;
                staging = key;
                PortraitStore.Run(() =>
                {
                    float tr = 1f, tg = 1f, tb = 1f;
                    if (backPx != null)
                    {
                        PortraitPictureMath.LightTint(step, out float lr, out float lg, out float lb);
                        PortraitComposeMath.Backdrop(backPx, w, h, backLook, lr, lg, lb);
                        if (tint)
                        {
                            PortraitComposeMath.Average(backPx, out float ar, out float ag, out float ab);
                            PortraitComposeMath.TintToward(ar, ag, ab, out tr, out tg, out tb);
                        }
                    }
                    // Rows from the foot throughout: what is done here is the same either way up.
                    return new StageParts { back = backPx, person = PortraitComposeMath.PersonLayer(personPx, w, h, lineLook, tr, tg, tb) };
                }, (parts, ex) =>
                {
                    staging = null;
                    if (ex != null || parts == null)
                    {
                        stageFailed.Add(key);
                        if (ex != null) CouncilLog.ErrorOnce("the large portrait: making its picture", ex);
                        return;
                    }
                    staged.Add(new Staged
                    {
                        key = key, pawn = pawn, picture = picture, lastUsed = Time.realtimeSinceStartup, headU = lay.headU, headV = lay.headV, headShare = lay.headH,
                        back = parts.back != null ? PortraitGpu.Make(parts.back, w, h, "RimfolkPortraitBack") : null,
                        person = PortraitGpu.Make(parts.person, w, h, "RimfolkPortraitPerson")
                    });
                    TrimStaged();
                }, false);
            }
            catch (Exception e)
            {
                staging = null;
                stageFailed.Add(key);
                CouncilLog.ErrorOnce("the large portrait: starting its picture", e);
            }
        }

        /// <summary>Starts making the backdrop behind the game's own drawing: read back, softened and darkened by the light on a worker, the tint toward its light worked out.</summary>
        private static void StartBackStage(string key, Pawn p, Texture2D backTex, int w, int h, int step, PortraitBackdropMode mode)
        {
            try
            {
                var backPx = ReadBackdrop(backTex, w, h);
                if (backPx == null)
                {
                    stageFailed.Add(key);
                    return;
                }
                var backLook = PortraitComposeMath.LookOf(mode);
                bool tint = LightTintOn;
                int pawn = p.thingIDNumber;
                staging = key;
                PortraitStore.Run(() =>
                {
                    var parts = new StageParts { back = backPx };
                    PortraitPictureMath.LightTint(step, out float lr, out float lg, out float lb);
                    PortraitComposeMath.Backdrop(backPx, w, h, backLook, lr, lg, lb);
                    if (tint)
                    {
                        PortraitComposeMath.Average(backPx, out float ar, out float ag, out float ab);
                        PortraitComposeMath.TintToward(ar, ag, ab, out parts.tr, out parts.tg, out parts.tb);
                    }
                    return parts;
                }, (parts, ex) =>
                {
                    staging = null;
                    if (ex != null || parts == null)
                    {
                        stageFailed.Add(key);
                        if (ex != null) CouncilLog.ErrorOnce("the large portrait: making the backdrop behind the game's drawing", ex);
                        return;
                    }
                    staged.Add(new Staged
                    {
                        key = key, pawn = pawn, picture = GameDrawing, lastUsed = Time.realtimeSinceStartup, tr = parts.tr, tg = parts.tg, tb = parts.tb,
                        back = PortraitGpu.Make(parts.back, w, h, "RimfolkPortraitBack")
                    });
                    TrimStaged();
                }, false);
            }
            catch (Exception e)
            {
                staging = null;
                stageFailed.Add(key);
                CouncilLog.ErrorOnce("the large portrait: starting the backdrop behind the game's drawing", e);
            }
        }

        private static void TrimStaged()
        {
            while (staged.Count > StagedMost)
            {
                Staged oldest = null;
                foreach (var s in staged)
                    if (oldest == null || s.lastUsed < oldest.lastUsed)
                        oldest = s;
                staged.Remove(oldest);
                if (oldest.back != null) UnityEngine.Object.Destroy(oldest.back);
                if (oldest.person != null) UnityEngine.Object.Destroy(oldest.person);
            }
        }

        /// <summary>
        /// The two pictures drawn: the backdrop still (or, with no backdrop, the light behind the head on the portrait's
        /// ground), the person in front, lifted or lowered by the pose, leaning and swaying about the foot, breathing; the
        /// picture's foot row carried on below the portrait's edge (cut off by it), so the body always meets the edge.
        /// </summary>
        private static void DrawStagedParts(Rect r, Pawn p, Staged s, FacePose pose, Color glow, bool moving)
        {
            if (s.back != null) GUI.DrawTexture(r, s.back);
            else DrawGlow(r, new FaceCamera { headU = s.headU, headV = s.headV, headShare = s.headShare, zoom = 1f }, glow);
            int seed = p.thingIDNumber;
            PortraitViewMath.Pose(FacePoseMath.Shift(pose.bearing, FaceTurn.Front, seed), out float lift, out float turn);
            bool animate = moving && Moves;
            float clock = animate ? Clock : 0f;
            float breath = animate ? FacePoseMath.Breath(clock, seed) : 1f, sway = animate ? FacePoseMath.Sway(clock, seed) : 0f;
            GUI.BeginGroup(r);
            var saved = GUI.matrix;
            try
            {
                var local = new Rect(0f, 0f, r.width, r.height);
                float lean = turn + sway;
                if (lean != 0f) UI.RotateAroundPivot(lean, new Vector2(local.center.x, local.yMax));
                float up = Mathf.Max(0f, lift * s.headShare * local.height), down = Mathf.Max(0f, -lift * s.headShare * local.height);
                float ph = Mathf.Max(1f, local.height * breath);
                float below = up + FootBelow * local.width;
                // Lifted by the pose, the picture rises and its foot row is carried on under it; lowered, it sinks past the edge.
                GUI.DrawTextureWithTexCoords(new Rect(local.x, local.yMax - ph - up + down, local.width, ph + below), s.person, new Rect(0f, -below / ph, 1f, 1f + below / ph));
            }
            finally
            {
                GUI.matrix = saved;
                GUI.EndGroup();
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// The game's own drawing in front of a backdrop: a soft dark shadow behind the head and shoulders, then the drawing
        /// as the portrait draws it (posed, breathing, swaying), tinted toward the room's light where that is on, with a thin
        /// dark line round it where "Line round the person" is on (<see cref="PortraitComposeMath.GameLine"/>).
        /// </summary>
        private static void DrawGameFigure(Rect r, Pawn p, FacePose pose, Vector2 renderSize, bool moving, Staged s)
        {
            pose.turn = FaceTurn.Front;
            var size = Whole(renderSize);
            var cam = Camera(p, FaceFrame.Bust, size.x, size.y, pose.turn);
            PortraitComposeMath.GameLine(OutlineMode, out float shadow, out float reach, out float ink);
            DrawShadow(r, cam, shadow);
            int seed = p.thingIDNumber;
            bool animate = moving && Moves;
            float clock = animate ? Clock : 0f;
            float breath = animate ? FacePoseMath.Breath(clock, seed) : 1f, sway = animate ? FacePoseMath.Sway(clock, seed) : 0f;
            float inkPx = ink > 0f ? Mathf.Max(1f, reach * r.width) : 0f;
            GUI.BeginGroup(r);
            try
            {
                DrawLayer(new Rect(0f, 0f, r.width, r.height), p, FaceFrame.Bust, size, pose, 1f, breath, sway, new Color(s.tr, s.tg, s.tb, 1f), inkPx, ink);
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = Color.white;
            }
        }

        /// <summary>A soft dark shadow behind the head and the shoulders under it, for the game's drawing on a backdrop (two soft round spots, no picture read back).</summary>
        private static void DrawShadow(Rect r, FaceCamera cam, float strength)
        {
            if (GlowTex == null || strength <= 0f) return;
            float side = Mathf.Min(r.width, r.height), head = cam.headShare * r.height;
            float cx = r.x + r.width * cam.headU, cy = r.y + r.height * cam.headV;
            GUI.color = new Color(0.03f, 0.025f, 0.03f, strength);
            float d1 = Mathf.Clamp(1.5f * head, 0.3f * side, side);
            GUI.DrawTexture(new Rect(cx - d1 / 2f, cy - d1 / 2f, d1, d1), GlowTex);
            float d2 = Mathf.Clamp(2.6f * head, 0.5f * side, 1.3f * side), cy2 = cy + 1.1f * head;
            GUI.DrawTexture(new Rect(cx - d2 / 2f, cy2 - d2 / 2f, d2, d2), GlowTex);
            GUI.color = Color.white;
        }

        /// <summary>The made pictures let go (a setting changed, a new game), so they are made again as they are shown.</summary>
        public static void ClearStaged()
        {
            foreach (var s in staged)
            {
                if (s.back != null) UnityEngine.Object.Destroy(s.back);
                if (s.person != null) UnityEngine.Object.Destroy(s.person);
            }
            staged.Clear();
            stageLooks.Clear();
            stageFailed.Clear();
        }
    }
}
