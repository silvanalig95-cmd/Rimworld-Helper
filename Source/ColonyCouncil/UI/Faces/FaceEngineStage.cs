using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Portraits, phases 4, 5 and 6: the large portrait at the inspect pane in front of a backdrop of where the person is
    /// (docs: DESIGN.md, "Portraits, phase 4: the painted library in the game, with backdrops", "Portraits, phase 5" and
    /// "Portraits, phase 6"; <c>docs/portrait-art/FRAMEWORK.md</c>, "How they go together").
    /// <list type="bullet">
    /// <item>A painted picture is shown whole (the player, 22 November 2026: "I'd rather, you know, see as much as I can"):
    /// the portrait takes the picture's shape (<see cref="StageTall"/>, the size setting its width), the person from the
    /// top of the head to where the body runs off the picture's foot.</item>
    /// <item>The game's own drawing, where the library has no picture for them, stands in front of the backdrop too (the
    /// player: "it still takes backdrop from the library, right? That's the least it can do"), with a soft shadow behind
    /// it, while "Backdrop behind the game's own drawing too" is on (<see cref="DrawGameStaged"/>); the portrait stays
    /// square.</item>
    /// <item>The backdrop by the room's kind, the colony's age and what is in the room, or the land outdoors
    /// (<see cref="Backdrops"/>); chosen when the portrait starts showing the person and kept while it goes on showing them
    /// (a room changed at speed doesn't change it), chosen again the next time they are shown.</item>
    /// <item>Made once per person, picture, backdrop, size, settings and step of light, on a worker thread from the small
    /// pictures already loaded (<see cref="PortraitComposeMath"/>), and kept as textures (the backdrop, and the person
    /// with the line round them), so each frame only draws them; the person breathes and sways in front of a backdrop that
    /// stays still, the body's foot carried on below the portrait's edge so no gap ever shows under it. The last few are
    /// kept, so turning back to someone shows them at once.</item>
    /// <item>Phase 6 (the player: "if I, for example, hover my mouse, it kind of centers towards the mouse in a way, like this
    /// kind of fake 3D effect"): with "Depth that follows the mouse" on, the backdrop is made a little larger than the
    /// portrait, cut into slices by its depth map where it has one, and the person's shadow on the wall made beside the
    /// person; each frame the layers are only moved by the eased mouse (<see cref="PortraitLifeMath.Shift"/>), the far wall
    /// against it, the person with it. The person's expressions (<c>FaceEngineAlive.cs</c>) are made the same way as the
    /// person, when first needed, and faded over them.</item>
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
            /// <summary>The size it was made at, in pixels (the person's; the backdrop is larger by its margin where depth is on).</summary>
            public int w, h;
            /// <summary>How far the backdrop reaches past the portrait each way, as shares of the portrait's width and height (none with depth off).</summary>
            public float marginX, marginY;
            /// <summary>The backdrop's nearer slices by its depth map (none without one).</summary>
            public List<StagedSlice> slices;
            /// <summary>The person's shadow on the wall (with depth on and a backdrop), a quarter the size, drawn stretched.</summary>
            public Texture2D shadow;
            /// <summary>Where the head is in the picture (its middle across and down, its share of the height), for the pose's lift and the light behind it.</summary>
            public float headU = 0.5f, headV = 0.3f, headShare = 0.3f;
            /// <summary>The tint toward the room's light, for the game's own drawing (1 for none).</summary>
            public float tr = 1f, tg = 1f, tb = 1f;
            /// <summary>The tint the painted person was made with, for their expressions made later the same way.</summary>
            public float ptr = 1f, ptg = 1f, ptb = 1f;
            /// <summary>The person's expressions made so far (blink, smile, frown, pain: the head of each, to lay over the picture), and those that couldn't be made.</summary>
            public readonly Dictionary<string, StagedFace> faces = new Dictionary<string, StagedFace>(StringComparer.Ordinal);
            public readonly HashSet<string> facesFailed = new HashSet<string>(StringComparer.Ordinal);
            public float lastUsed;
        }

        /// <summary>The head of one of the person's expressions: its texture, and where it sits in the picture (shares, from the top).</summary>
        private struct StagedFace
        {
            public Texture2D tex;
            public Rect at;
        }

        /// <summary>One slice of a backdrop by its depth: its texture, where it sits in the backdrop (shares, from the top) and how near it is.</summary>
        private struct StagedSlice
        {
            public Texture2D tex;
            public Rect at;
            public float z;
        }

        private sealed class StageLook
        {
            public float until;
            public string whereKey;
            public PictureFile backdrop, depth;
            public BackdropRecord backRecord;
            public int step = -1;
            /// <summary>Phase 6, for the backdrop alive: the weather over it outdoors, a fire's light indoors, and a number for the place.</summary>
            public PortraitWeather weather;
            public bool hearth;
            public int seed;
            /// <summary>The frame the large portrait last showed this person, so a new showing can be told from one going on.</summary>
            public int shownFrame = -10;
            /// <summary>Where they were when the backdrop was chosen, for the backdrop alive while it is kept.</summary>
            public BackdropWhere where;
        }

        private sealed class StageParts
        {
            public byte[] back, person, shadow;
            public int bw, bh, sw, sh;
            public List<DepthSlice> slices;
            public float tr = 1f, tg = 1f, tb = 1f;
        }

        private const int StagedMost = 4;

        /// <summary>How often where the person is and the light are looked at again, in seconds.</summary>
        private const float StageLookEvery = 2f;

        /// <summary>How long a made picture's expressions are kept once it is no longer shown, in real seconds.</summary>
        private const float FacesKeptFor = 20f;

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

        /// <summary>The strength of "Depth that follows the mouse" (0 off).</summary>
        private static float DepthStrength => PortraitLifeMath.StrengthOf(S?.portraitDepth ?? PortraitDepthMode.Subtle);

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
                var pic = PortraitSources.Shown(p, PortraitSources.ForStage(p), true);
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

        /// <summary>The depth's part of a made picture's key: its strength and the depth map it was cut by.</summary>
        private static string DepthKey(float depth, PictureFile map) => depth > 0f ? "|d" + depth.ToString("0.0") + (map != null ? ":" + map.key : "") : "";

        /// <summary>
        /// The large portrait with a painted picture, whole, its backdrop behind it; false where the person has no picture
        /// from the library (the caller draws the portrait as before). <paramref name="renderSize"/> is the size it is made
        /// at (the size before a drag, while dragging).
        /// </summary>
        public static bool DrawStaged(Rect r, Pawn p, Vector2 renderSize, bool moving)
        {
            if (p == null || Event.current.type != EventType.Repaint) return false;
            Tick();
            var own = PortraitSources.ForStage(p);
            // A painted condition (asleep, wounded ...) in its place while one holds.
            var pic = PortraitSources.Shown(p, own, true);
            if (pic?.file == null) return false;
            var l = LookOf(p);
            var pose = PoseOf(p, FaceTurn.Front, true);
            if (pose.bearing == FaceBearing.Dead) return false;
            Widgets.DrawBoxSolid(r, l.back);
            if (pic.texture == null) return true;
            int w = Px(renderSize.x, 1024), h = Px(renderSize.y, 1600);
            var look = StageLookOf(p);
            var mode = BackdropMode;
            float depth = DepthStrength;
            Texture2D backTex = null, depthTex = null;
            bool waiting = false;
            if (PortraitComposeMath.WhatToDraw(true, look.backdrop != null, mode, BehindGame) == PortraitStageDraw.PaintedOnBackdrop)
            {
                backTex = PortraitStore.Get(look.backdrop);
                waiting = backTex == null && !PortraitStore.Failed(look.backdrop);
                if (depth > 0f && look.depth != null)
                {
                    depthTex = PortraitStore.Get(look.depth);
                    waiting |= depthTex == null && !PortraitStore.Failed(look.depth);
                }
            }
            string key = p.thingIDNumber + "|" + pic.name + "|" + (backTex != null ? look.backdrop.key : "-") + "|" + w + "x" + h + "|" + (int)mode + (int)OutlineMode + (LightTintOn ? 1 : 0)
                         + "|" + look.step + DepthKey(depth, depthTex != null ? look.depth : null);
            if (stageFailed.Contains(key)) return false;
            var now = staged.Find(s => s.key == key);
            if (now == null && !waiting && staging == null) StartStage(key, p, pic, backTex, depthTex, w, h, look.step, mode, depth);
            // While it is made: the one before for the same person and picture (or the picture itself, for a condition's), else the ground.
            var show = now ?? Latest(p.thingIDNumber, pic.name) ?? (own != null && own != pic ? Latest(p.thingIDNumber, own.name) : null);
            if (show == null) return true;
            show.lastUsed = Time.realtimeSinceStartup;
            LetFacesGo(show);
            EaseDepth(p.thingIDNumber);
            DrawStagedParts(r, p, show, pose, l.glow, moving, look, depth);
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
            if (PortraitSources.ForStage(p) != null) return false;
            var pose = PoseOf(p, FaceTurn.Front, true);
            if (pose.bearing == FaceBearing.Dead) return false;
            var look = StageLookOf(p);
            if (PortraitComposeMath.WhatToDraw(false, look.backdrop != null, mode, true) != PortraitStageDraw.GameOnBackdrop) return false;
            int w = Px(renderSize.x, 1024), h = Px(renderSize.y, 1600);
            float depth = DepthStrength;
            var backTex = PortraitStore.Get(look.backdrop);
            Texture2D depthTex = depth > 0f && look.depth != null ? PortraitStore.Get(look.depth) : null;
            bool depthWait = depth > 0f && look.depth != null && depthTex == null && !PortraitStore.Failed(look.depth);
            string key = p.thingIDNumber + "|" + GameDrawing + "|" + look.backdrop.key + "|" + w + "x" + h + "|" + (int)mode + (LightTintOn ? 1 : 0) + "|" + look.step
                         + DepthKey(depth, depthTex != null ? look.depth : null);
            if (stageFailed.Contains(key)) return false;
            var now = staged.Find(s => s.key == key);
            if (now == null && backTex != null && !depthWait && staging == null) StartBackStage(key, p, backTex, depthTex, w, h, look.step, mode, depth);
            var show = now ?? Latest(p.thingIDNumber, GameDrawing);
            if (show == null) return false;
            show.lastUsed = Time.realtimeSinceStartup;
            EaseDepth(p.thingIDNumber);
            GUI.BeginGroup(r);
            try
            {
                DrawBackLayers(new Rect(0f, 0f, r.width, r.height), show, look, depth);
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = Color.white;
            }
            DrawGameFigure(r, p, pose, renderSize, moving, show, depth);
            return true;
        }

        /// <summary>
        /// The backdrop the large portrait shows behind this person now (its record's name, for its menu's "Delete this
        /// backdrop for good"), or null where it shows none: no backdrop found for where they are, "Backdrop" plain, or the
        /// game's own drawing with "Backdrop behind the game's own drawing too" off.
        /// </summary>
        public static string BackdropShown(Pawn p)
        {
            if (p == null || !stageLooks.TryGetValue(p.thingIDNumber, out var look) || look.backdrop == null || look.backRecord == null) return null;
            var d = PortraitComposeMath.WhatToDraw(PortraitSources.ForStage(p)?.file != null, true, BackdropMode, BehindGame);
            return d == PortraitStageDraw.PaintedOnBackdrop || d == PortraitStageDraw.GameOnBackdrop ? look.backRecord.name : null;
        }

        private static Staged Latest(int pawn, string picture)
        {
            Staged best = null;
            foreach (var s in staged)
                if (s.pawn == pawn && s.picture == picture && (best == null || s.lastUsed > best.lastUsed))
                    best = s;
            return best;
        }

        /// <summary>
        /// Where the person is and the light, read when the large portrait starts showing them, and the backdrop chosen then;
        /// both kept while it goes on showing them, read again only the next time it shows them after showing someone else
        /// or nothing (the player, 23 November 2026: "It should just, when I click the pawn, it should look in which room it
        /// is, take that background, and only refresh the background once I kind of click away and click back to the same
        /// pawn. It doesn't have to be a real time update."). The weather and a fire's light for the backdrop alive go on
        /// being read, at most every two seconds, for the place the backdrop shows.
        /// </summary>
        private static StageLook StageLookOf(Pawn p)
        {
            float now = Time.realtimeSinceStartup;
            if (!stageLooks.TryGetValue(p.thingIDNumber, out var look))
            {
                if (stageLooks.Count > 32) stageLooks.Clear();
                look = new StageLook();
                stageLooks[p.thingIDNumber] = look;
            }
            int frame = Time.frameCount;
            bool fresh = PortraitLifeMath.NewShowing(look.shownFrame, frame) || look.where == null;
            look.shownFrame = frame;
            if (!fresh && now < look.until) return look;
            look.until = now + StageLookEvery;
            try
            {
                if (fresh)
                {
                    var where = Backdrops.WhereOf(p, out float glow);
                    look.step = PortraitPictureMath.LightStep(glow, -1);
                    look.where = where;
                    string wk = where.Key;
                    if (wk != look.whereKey)
                    {
                        look.whereKey = wk;
                        look.backdrop = Backdrops.For(where, out var choice);
                        look.backRecord = Backdrops.RecordAt(choice);
                        look.depth = look.backdrop != null ? Backdrops.DepthAt(choice) : null;
                        look.seed = where.seed;
                        Backdrops.Note(where, where.seed);
                    }
                }
                LookAlive(p, look.where, look);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait's backdrop: where the person is", e);
            }
            return look;
        }

        /// <summary>The middle of a backdrop with the shape <paramref name="w"/> by <paramref name="h"/>, read back from the graphics card at that size (rows from the foot).</summary>
        private static byte[] ReadBackdrop(Texture2D backTex, int w, int h)
        {
            PortraitPictureMath.BackdropPart(backTex.width, backTex.height, (float)w / h, out float sx, out float sy, out float ox, out float oy);
            return PortraitGpu.ReadSharp(backTex, w, h, new Vector2(sx, sy), new Vector2(ox, oy));
        }

        /// <summary>
        /// On a worker thread: the backdrop (read <paramref name="bw"/> by <paramref name="bh"/>, larger than the portrait by
        /// its margin where depth is on) softened as the setting says and darkened by the light, the tint toward its light
        /// worked out where that is on, and its nearer slices cut by its depth map where it has one.
        /// </summary>
        private static void MakeBackParts(StageParts parts, byte[] backPx, byte[] depthPx, int bw, int bh, BackdropLook backLook, int step, bool tint)
        {
            parts.back = backPx;
            parts.bw = bw;
            parts.bh = bh;
            if (backPx == null) return;
            PortraitPictureMath.LightTint(step, out float lr, out float lg, out float lb);
            PortraitComposeMath.Backdrop(backPx, bw, bh, backLook, lr, lg, lb);
            if (tint)
            {
                PortraitComposeMath.Average(backPx, out float ar, out float ag, out float ab);
                PortraitComposeMath.TintToward(ar, ag, ab, out parts.tr, out parts.tg, out parts.tb);
            }
            if (depthPx != null) parts.slices = PortraitLifeMath.Slices(backPx, PortraitLifeMath.Grey(depthPx), bw, bh, PortraitLifeMath.LayersMost);
        }

        /// <summary>On the game's thread: the backdrop's textures made from its parts (its slices with where each sits, from the top).</summary>
        private static void MakeBackTextures(Staged s, StageParts parts, int w, int h)
        {
            if (parts.back == null) return;
            s.back = PortraitGpu.Make(parts.back, parts.bw, parts.bh, "RimfolkPortraitBack");
            s.marginX = (parts.bw - w) / 2f / w;
            s.marginY = (parts.bh - h) / 2f / h;
            if (parts.slices == null || parts.slices.Count == 0) return;
            s.slices = new List<StagedSlice>();
            foreach (var sl in parts.slices)
                s.slices.Add(new StagedSlice
                {
                    tex = PortraitGpu.Make(sl.px, sl.w, sl.h, "RimfolkPortraitSlice"),
                    at = new Rect((float)sl.x / parts.bw, 1f - (float)(sl.y + sl.h) / parts.bh, (float)sl.w / parts.bw, (float)sl.h / parts.bh), z = sl.z
                });
        }

        /// <summary>
        /// Starts making the picture: the whole person and the backdrop's middle (larger by its margin where depth is on, and
        /// its depth map the same) read back small from the graphics card (on the game's thread, as it must), then softened,
        /// lined, tinted, sliced and the shadow made on a worker thread, then made into textures one frame after.
        /// </summary>
        private static void StartStage(string key, Pawn p, PortraitPicture pic, Texture2D backTex, Texture2D depthTex, int w, int h, int step, PortraitBackdropMode mode, float depth)
        {
            try
            {
                var lay = pic.layout;
                // The whole picture at the portrait's size (its shape is the picture's own): nothing cropped.
                var personPx = PortraitGpu.ReadSharp(pic.texture, w, h, Vector2.one, Vector2.zero);
                if (personPx == null)
                {
                    stageFailed.Add(key);
                    return;
                }
                PortraitLifeMath.Margin(depth, w, out int mx, out int my);
                int bw = w + 2 * mx, bh = h + 2 * my;
                byte[] backPx = backTex != null ? ReadBackdrop(backTex, bw, bh) : null;
                byte[] depthPx = backPx != null && depthTex != null ? ReadBackdrop(depthTex, bw, bh) : null;
                var backLook = PortraitComposeMath.LookOf(mode);
                var lineLook = PortraitComposeMath.LookOf(OutlineMode);
                bool tint = LightTintOn;
                int pawn = p.thingIDNumber;
                string picture = pic.name;
                staging = key;
                PortraitStore.Run(() =>
                {
                    var parts = new StageParts();
                    MakeBackParts(parts, backPx, depthPx, bw, bh, backLook, step, tint);
                    // Rows from the foot throughout: what is done here is the same either way up.
                    parts.person = PortraitComposeMath.PersonLayer(personPx, w, h, lineLook, parts.tr, parts.tg, parts.tb);
                    if (depth > 0f && backPx != null) parts.shadow = PortraitLifeMath.Shadow(personPx, w, h, PortraitLifeMath.ShadowStrength, out parts.sw, out parts.sh);
                    return parts;
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
                        key = key, pawn = pawn, picture = picture, lastUsed = Time.realtimeSinceStartup, headU = lay.headU, headV = lay.headV, headShare = lay.headH, w = w, h = h,
                        ptr = parts.tr, ptg = parts.tg, ptb = parts.tb,
                        person = PortraitGpu.Make(parts.person, w, h, "RimfolkPortraitPerson"),
                        shadow = parts.shadow != null ? PortraitGpu.Make(parts.shadow, parts.sw, parts.sh, "RimfolkPortraitShadow") : null
                    };
                    MakeBackTextures(s, parts, w, h);
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

        /// <summary>Starts making the backdrop behind the game's own drawing: read back (larger by its margin where depth is on), softened, darkened by the light and sliced on a worker, the tint toward its light worked out.</summary>
        private static void StartBackStage(string key, Pawn p, Texture2D backTex, Texture2D depthTex, int w, int h, int step, PortraitBackdropMode mode, float depth)
        {
            try
            {
                PortraitLifeMath.Margin(depth, w, out int mx, out int my);
                int bw = w + 2 * mx, bh = h + 2 * my;
                var backPx = ReadBackdrop(backTex, bw, bh);
                if (backPx == null)
                {
                    stageFailed.Add(key);
                    return;
                }
                byte[] depthPx = depthTex != null ? ReadBackdrop(depthTex, bw, bh) : null;
                var backLook = PortraitComposeMath.LookOf(mode);
                bool tint = LightTintOn;
                int pawn = p.thingIDNumber;
                staging = key;
                PortraitStore.Run(() =>
                {
                    var parts = new StageParts();
                    MakeBackParts(parts, backPx, depthPx, bw, bh, backLook, step, tint);
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
                    var s = new Staged { key = key, pawn = pawn, picture = GameDrawing, lastUsed = Time.realtimeSinceStartup, tr = parts.tr, tg = parts.tg, tb = parts.tb, w = w, h = h };
                    MakeBackTextures(s, parts, w, h);
                    staged.Add(s);
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

        private sealed class FaceParts
        {
            public byte[] px;
            public int x, y, w, h;
        }

        /// <summary>
        /// Starts making one of the person's expressions (phase 6): read back at the person's size, then on a worker only its
        /// head taken, in a soft oval, where the person is solid, tinted as the person was
        /// (<see cref="PortraitLifeMath.FaceOverlay"/>; the player's tools may repaint a strand of hair or a fold of cloth
        /// elsewhere, so nothing on the body changes with a blink or a mood), made into a small texture a frame after. One at
        /// a time, with the pictures.
        /// </summary>
        private static void StartFace(Staged s, string word, Texture2D tex)
        {
            try
            {
                var px = PortraitGpu.Read(tex, s.w, s.h, Vector2.one, Vector2.zero);
                if (px == null)
                {
                    s.facesFailed.Add(word);
                    return;
                }
                int w = s.w, h = s.h;
                float tr = s.ptr, tg = s.ptg, tb = s.ptb, hu = s.headU, hv = s.headV, hh = s.headShare;
                staging = s.key + "|" + word;
                PortraitStore.Run(() =>
                {
                    var f = new FaceParts();
                    f.px = PortraitLifeMath.FaceOverlay(px, w, h, hu, hv, hh, tr, tg, tb, out f.x, out f.y, out f.w, out f.h);
                    return f;
                }, (f, ex) =>
                {
                    staging = null;
                    if (ex != null || f?.px == null)
                    {
                        s.facesFailed.Add(word);
                        if (ex != null) CouncilLog.ErrorOnce("the large portrait: making an expression", ex);
                        return;
                    }
                    // Let go meanwhile (another picture made, the game left): nothing to keep it for.
                    if (!staged.Contains(s)) return;
                    if (s.faces.TryGetValue(word, out var old) && old.tex != null) UnityEngine.Object.Destroy(old.tex);
                    s.faces[word] = new StagedFace
                    {
                        tex = PortraitGpu.Make(f.px, f.w, f.h, "RimfolkPortraitFace"),
                        at = new Rect((float)f.x / w, 1f - (float)(f.y + f.h) / h, (float)f.w / w, (float)f.h / h)
                    };
                }, false);
            }
            catch (Exception e)
            {
                staging = null;
                s.facesFailed.Add(word);
                CouncilLog.ErrorOnce("the large portrait: starting an expression", e);
            }
        }

        /// <summary>The expressions of made pictures no longer shown for a while let go (only the person at the inspect pane keeps theirs).</summary>
        private static void LetFacesGo(Staged shown)
        {
            float now = Time.realtimeSinceStartup;
            foreach (var s in staged)
                if (s != shown && s.faces.Count > 0 && now - s.lastUsed > FacesKeptFor)
                    DestroyFaces(s);
        }

        private static void DestroyFaces(Staged s)
        {
            foreach (var f in s.faces.Values)
                if (f.tex != null)
                    UnityEngine.Object.Destroy(f.tex);
            s.faces.Clear();
            s.facesFailed.Clear();
        }

        private static void Destroy(Staged s)
        {
            if (s.back != null) UnityEngine.Object.Destroy(s.back);
            if (s.person != null) UnityEngine.Object.Destroy(s.person);
            if (s.shadow != null) UnityEngine.Object.Destroy(s.shadow);
            if (s.slices != null)
                foreach (var sl in s.slices)
                    if (sl.tex != null)
                        UnityEngine.Object.Destroy(sl.tex);
            DestroyFaces(s);
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
                Destroy(oldest);
            }
        }

        /// <summary>
        /// The backdrop's layers, inside the portrait's group: the backdrop (larger than the portrait by its margin) moved
        /// against the mouse, its nearer slices each by how near they are, then the backdrop alive (the weather outdoors, a
        /// fire's light indoors) over it.
        /// </summary>
        private static void DrawBackLayers(Rect local, Staged s, StageLook look, float depth)
        {
            if (s.back == null) return;
            PortraitLifeMath.Shift(0f, depthX, depthY, depth, local.width, out float dx, out float dy);
            float mw = s.marginX * local.width, mh = s.marginY * local.height;
            var back = new Rect(local.x - mw + dx, local.y - mh + dy, local.width + 2f * mw, local.height + 2f * mh);
            GUI.DrawTexture(back, s.back);
            if (s.slices != null)
                foreach (var sl in s.slices)
                {
                    PortraitLifeMath.Shift(sl.z, depthX, depthY, depth, local.width, out float sx, out float sy);
                    GUI.DrawTexture(new Rect(back.x - dx + sx + sl.at.x * back.width, back.y - dy + sy + sl.at.y * back.height, sl.at.width * back.width, sl.at.height * back.height), sl.tex);
                }
            DrawBackdropAlive(local, look, depth);
        }

        /// <summary>
        /// The pictures drawn: the backdrop's layers (or, with no backdrop, the light behind the head on the portrait's
        /// ground), the person's shadow on the wall where depth is on, the person in front, lifted or lowered by the pose,
        /// leaning and swaying about the foot, breathing, moved with the mouse, their expression faded over them; the
        /// picture's foot row carried on below the portrait's edge (cut off by it), and its sides past its own where it moves,
        /// so the body always meets the edges.
        /// </summary>
        private static void DrawStagedParts(Rect r, Pawn p, Staged s, FacePose pose, Color glow, bool moving, StageLook look, float depth)
        {
            if (s.back == null) DrawGlow(r, new FaceCamera { headU = s.headU, headV = s.headV, headShare = s.headShare, zoom = 1f }, glow);
            int seed = p.thingIDNumber;
            PortraitViewMath.Pose(FacePoseMath.Shift(pose.bearing, FaceTurn.Front, seed), out float lift, out float turn);
            bool animate = moving && Moves;
            float clock = animate ? Clock : 0f;
            float breath = animate ? FacePoseMath.Breath(clock, seed) : 1f, sway = animate ? FacePoseMath.Sway(clock, seed) : 0f;
            var faces = FacesNow(p, s);
            GUI.BeginGroup(r);
            var saved = GUI.matrix;
            try
            {
                var local = new Rect(0f, 0f, r.width, r.height);
                DrawBackLayers(local, s, look, depth);
                float lean = turn + sway, shakeX = 0f;
                // What they go through moves them a little: the tremble near a break, the wobble of the drunk (PortraitEffects).
                if (animate)
                {
                    PortraitEffects.Shake(p, out float shakeLean, out float shakeDx);
                    lean += shakeLean;
                    shakeX = shakeDx * local.width;
                }
                if (lean != 0f) UI.RotateAroundPivot(lean, new Vector2(local.center.x, local.yMax));
                float up = Mathf.Max(0f, lift * s.headShare * local.height), down = Mathf.Max(0f, -lift * s.headShare * local.height);
                float ph = Mathf.Max(1f, local.height * breath);
                float below = up + FootBelow * local.width;
                // Moved with the mouse, its sides carried on past its own as its foot is below, so no strip opens at an edge.
                PortraitLifeMath.Shift(1f, depthX, depthY, depth, local.width, out float dx, out float dy);
                dx += shakeX;
                float side = PortraitLifeMath.PersonSide(depth) * local.width + Mathf.Abs(shakeX);
                var at = new Rect(local.x + dx - side, local.yMax - ph - up + down + dy, local.width + 2f * side, ph + below);
                // Lifted by the pose, the picture rises and its foot row is carried on under it; lowered, it sinks past the edge.
                var uv = new Rect(-side / local.width, -below / ph, 1f + 2f * side / local.width, 1f + below / ph);
                if (s.shadow != null)
                {
                    // The shadow on the wall behind them: a little to the side and down, moving less than they do.
                    PortraitLifeMath.Shift(PortraitLifeMath.ShadowZ, depthX, depthY, depth, local.width, out float sx, out float sy);
                    float off = PortraitLifeMath.ShadowOffset * local.width;
                    GUI.DrawTextureWithTexCoords(new Rect(at.x - dx + sx + off, at.y - dy + sy + off, at.width, at.height), s.shadow, uv);
                }
                DrawFaces(at, uv, s, faces);
            }
            finally
            {
                GUI.matrix = saved;
                GUI.EndGroup();
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// The person, and their expression's head laid over the picture's own: the face before whole (or going, back to the
        /// plain picture), the one now as far as it has faded in (<see cref="PortraitLifeMath.FadeStrengths"/>).
        /// </summary>
        private static void DrawFaces(Rect at, Rect uv, Staged s, FaceDraw f)
        {
            GUI.DrawTextureWithTexCoords(at, s.person, uv);
            PortraitLifeMath.FadeStrengths(f.before, f.now, f.faded, out float beforeA, out float nowA);
            LayFace(at, uv, s, f.before, beforeA);
            LayFace(at, uv, s, f.now, nowA);
        }

        /// <summary>One expression's head laid over the person drawn at <paramref name="at"/> (its texture spread by <paramref name="uv"/>), where it sits in the picture.</summary>
        private static void LayFace(Rect at, Rect uv, Staged s, string word, float alpha)
        {
            if (alpha <= 0f || string.IsNullOrEmpty(word) || !s.faces.TryGetValue(word, out var face) || face.tex == null) return;
            float X(float u) => at.x + (u - uv.x) / uv.width * at.width;
            // A share down from the picture's top, where the picture is drawn (its texture runs from the foot).
            float Y(float down) => at.yMax - (1f - down - uv.y) / uv.height * at.height;
            GUI.color = UiKit.WithAlpha(Color.white, alpha);
            GUI.DrawTexture(Rect.MinMaxRect(X(face.at.xMin), Y(face.at.yMin), X(face.at.xMax), Y(face.at.yMax)), face.tex);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The game's own drawing in front of a backdrop: a soft dark shadow behind the head and shoulders, then the drawing
        /// as the portrait draws it (posed, breathing, swaying), tinted toward the room's light where that is on, with a thin
        /// dark line round it where "Line round the person" is on (<see cref="PortraitComposeMath.GameLine"/>); with depth on,
        /// the drawing moved across with the mouse and its shadow less.
        /// </summary>
        private static void DrawGameFigure(Rect r, Pawn p, FacePose pose, Vector2 renderSize, bool moving, Staged s, float depth)
        {
            pose.turn = FaceTurn.Front;
            var size = Whole(renderSize);
            var cam = Camera(p, FaceFrame.Bust, size.x, size.y, pose.turn);
            PortraitComposeMath.GameLine(OutlineMode, out float shadow, out float reach, out float ink);
            // Across only: the game's drawing ends at the portrait's foot as it is, so it is never lifted from it.
            PortraitLifeMath.Shift(1f, depthX, 0f, depth, r.width, out float dx, out _);
            PortraitLifeMath.Shift(PortraitLifeMath.ShadowZ, depthX, 0f, depth, r.width, out float sx, out _);
            DrawShadow(new Rect(r.x + sx, r.y, r.width, r.height), cam, shadow);
            int seed = p.thingIDNumber;
            bool animate = moving && Moves;
            float clock = animate ? Clock : 0f;
            float breath = animate ? FacePoseMath.Breath(clock, seed) : 1f, sway = animate ? FacePoseMath.Sway(clock, seed) : 0f;
            float inkPx = ink > 0f ? Mathf.Max(1f, reach * r.width) : 0f;
            var tint = UiKit.Plain;
            tint.r *= s.tr;
            tint.g *= s.tg;
            tint.b *= s.tb;
            GUI.BeginGroup(r);
            try
            {
                DrawLayer(new Rect(dx, 0f, r.width, r.height), p, FaceFrame.Bust, size, pose, 1f, breath, sway, tint, inkPx, ink);
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
            GUI.color = UiKit.WithAlpha(UiKit.WindowGround, strength);
            float d1 = Mathf.Clamp(1.5f * head, 0.3f * side, side);
            GUI.DrawTexture(new Rect(cx - d1 / 2f, cy - d1 / 2f, d1, d1), GlowTex);
            float d2 = Mathf.Clamp(2.6f * head, 0.5f * side, 1.3f * side), cy2 = cy + 1.1f * head;
            GUI.DrawTexture(new Rect(cx - d2 / 2f, cy2 - d2 / 2f, d2, d2), GlowTex);
            GUI.color = Color.white;
        }

        /// <summary>The made pictures let go (a setting changed, a new game), so they are made again as they are shown.</summary>
        public static void ClearStaged()
        {
            foreach (var s in staged) Destroy(s);
            staged.Clear();
            stageLooks.Clear();
            stageFailed.Clear();
            ClearAlive();
        }
    }
}
