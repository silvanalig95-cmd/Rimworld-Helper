using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Portraits, phase 4: the large portrait at the inspect pane with a painted picture from the library, in front of a
    /// backdrop of where the person is (docs: DESIGN.md, "Portraits, phase 4: the painted library in the game, with
    /// backdrops"; <c>docs/portrait-art/FRAMEWORK.md</c>, "How they go together").
    /// <list type="bullet">
    /// <item>The crop follows the portrait's size (<see cref="PortraitPictureMath.CropFor"/>): at 96 the face, at 360 the
    /// pose; past the picture's edges it is clear.</item>
    /// <item>The backdrop by the room's kind, the colony's age and what is in the room, or the land outdoors
    /// (<see cref="Backdrops"/>); looked at again only when the person's room, map or the light's step changes, at most
    /// every two seconds.</item>
    /// <item>Made once per person, picture, backdrop, size, settings and step of light, on a worker thread from the small
    /// pictures already loaded (<see cref="PortraitComposeMath"/>), and kept as two textures (the backdrop, and the person
    /// with the line round them), so each frame only draws them; the person breathes and sways in front of a backdrop that
    /// stays still. The last few are kept, so turning back to someone shows them at once.</item>
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
            public int side;
            /// <summary>Where the head's middle is down the square, and its share of it (for the pose's lift).</summary>
            public float headShare;
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
        }

        private const int StagedMost = 4;

        /// <summary>How often where the person is and the light are looked at again, in seconds.</summary>
        private const float StageLookEvery = 2f;

        private static readonly List<Staged> staged = new List<Staged>();
        private static readonly Dictionary<int, StageLook> stageLooks = new Dictionary<int, StageLook>();
        private static readonly HashSet<string> stageFailed = new HashSet<string>();
        private static string staging;

        private static PortraitBackdropMode BackdropMode => S?.portraitBackdrop ?? PortraitBackdropMode.AsPainted;

        private static PortraitOutlineMode OutlineMode => S?.portraitOutline ?? PortraitOutlineMode.Off;

        private static bool LightTintOn => S?.portraitLightTint ?? false;

        /// <summary>
        /// The large portrait at the inspect pane with a painted picture, its backdrop behind it; false where the person has
        /// no picture from the library (the caller draws the portrait as before). <paramref name="renderSize"/> is the size it
        /// is made at (the size before a drag, while dragging).
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
            float size = Mathf.Max(1f, renderSize.x);
            int side = Mathf.Clamp(Mathf.RoundToInt(size * Prefs.UIScale), 16, 1024);
            var look = StageLookOf(p);
            var mode = BackdropMode;
            Texture2D backTex = null;
            bool waiting = false;
            if (mode != PortraitBackdropMode.Plain && look.backdrop != null)
            {
                backTex = PortraitStore.Get(look.backdrop);
                waiting = backTex == null && !PortraitStore.Failed(look.backdrop);
            }
            string key = p.thingIDNumber + "|" + pic.name + "|" + (backTex != null ? look.backdrop.key : "-") + "|" + side + "|" + (int)mode + (int)OutlineMode + (LightTintOn ? 1 : 0)
                         + "|" + look.step;
            if (stageFailed.Contains(key)) return false;
            var now = staged.Find(s => s.key == key);
            if (now == null && !waiting && staging == null) StartStage(key, p, pic, backTex, side, size, look.step, mode);
            // While it is made: the one before for the same person and picture, else the ground.
            var show = now ?? Latest(p.thingIDNumber, pic.name);
            if (show == null) return true;
            show.lastUsed = Time.realtimeSinceStartup;
            DrawStagedParts(r, p, show, pose, l.glow, moving);
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

        /// <summary>
        /// Starts making the picture: the person's crop and the backdrop's square read back small from the graphics card
        /// (on the game's thread, as it must), then softened, lined and tinted on a worker thread, then made into two
        /// textures one frame after.
        /// </summary>
        private static void StartStage(string key, Pawn p, PortraitPicture pic, Texture2D backTex, int side, float size, int step, PortraitBackdropMode mode)
        {
            try
            {
                var tex = pic.texture;
                int tw = tex.width, th = tex.height;
                var lay = pic.layout;
                var head = new HeadMeasure { top = lay.headV - lay.headH / 2f, neck = lay.headV + lay.headH / 2f, centre = lay.headU, found = true };
                var crop = PortraitPictureMath.CropFor(head, tw, th, size);
                float k = side / Mathf.Max(1f, crop.side);
                int rw = Mathf.Max(1, Mathf.RoundToInt(tw * k)), rh = Mathf.Max(1, Mathf.RoundToInt(th * k));
                var personPx = PortraitGpu.Read(tex, rw, rh, Vector2.one, Vector2.zero);
                if (personPx == null)
                {
                    stageFailed.Add(key);
                    return;
                }
                int x0 = Mathf.RoundToInt(crop.x * k), y0 = Mathf.RoundToInt(crop.y * k);
                byte[] backPx = null;
                if (backTex != null)
                {
                    float bw = backTex.width, bh = backTex.height;
                    // The backdrop's middle square: its full width, the band across its middle (or its full height, the middle of its width).
                    var scale = bh >= bw ? new Vector2(1f, bw / bh) : new Vector2(bh / bw, 1f);
                    var offset = new Vector2((1f - scale.x) / 2f, (1f - scale.y) / 2f);
                    backPx = PortraitGpu.Read(backTex, side, side, scale, offset);
                }
                var backLook = PortraitComposeMath.LookOf(mode);
                var lineLook = PortraitComposeMath.LookOf(OutlineMode);
                bool tint = LightTintOn;
                float headShare = PortraitPictureMath.HeadFill(size);
                int pawn = p.thingIDNumber;
                string picture = pic.name;
                staging = key;
                PortraitStore.Run(() =>
                {
                    var person = PortraitPictureMath.CropPad(PortraitPictureMath.FlipRows(personPx, rw, rh), rw, rh, x0, y0, side);
                    float tr = 1f, tg = 1f, tb = 1f;
                    if (backPx != null)
                    {
                        PortraitPictureMath.LightTint(step, out float lr, out float lg, out float lb);
                        PortraitComposeMath.Backdrop(backPx, side, backLook, lr, lg, lb);
                        if (tint)
                        {
                            PortraitComposeMath.Average(backPx, out float ar, out float ag, out float ab);
                            PortraitComposeMath.TintToward(ar, ag, ab, out tr, out tg, out tb);
                        }
                    }
                    var layer = PortraitComposeMath.PersonLayer(person, side, lineLook, tr, tg, tb);
                    return new StageParts { back = backPx, person = PortraitPictureMath.FlipRows(layer, side, side) };
                }, (parts, ex) =>
                {
                    staging = null;
                    if (ex != null || parts == null)
                    {
                        stageFailed.Add(key);
                        if (ex != null) CouncilLog.ErrorOnce("the large portrait: making its picture", ex);
                        return;
                    }
                    var s = new Staged
                    {
                        key = key, pawn = pawn, picture = picture, side = side, headShare = headShare, lastUsed = Time.realtimeSinceStartup,
                        back = parts.back != null ? PortraitGpu.Make(parts.back, side, side, "RimfolkPortraitBack") : null,
                        person = PortraitGpu.Make(parts.person, side, side, "RimfolkPortraitPerson")
                    };
                    staged.Add(s);
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
        /// ground), the person in front, lifted or lowered by the pose, leaning and swaying about the foot, breathing.
        /// </summary>
        private static void DrawStagedParts(Rect r, Pawn p, Staged s, FacePose pose, Color glow, bool moving)
        {
            if (s.back != null) GUI.DrawTexture(r, s.back);
            else
            {
                float mid = PortraitPictureMath.HeadMiddle(r.width);
                DrawGlow(r, new FaceCamera { headU = 0.5f, headV = mid, headShare = s.headShare, zoom = 1f }, glow);
            }
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
                float up = lift * s.headShare * local.height;
                GUI.DrawTexture(new Rect(local.x, local.yMax - local.height * breath - up, local.width, local.height * breath), s.person);
            }
            finally
            {
                GUI.matrix = saved;
                GUI.EndGroup();
                GUI.color = Color.white;
            }
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
