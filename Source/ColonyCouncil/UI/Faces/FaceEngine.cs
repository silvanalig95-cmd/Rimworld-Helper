using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The face engine (docs: DESIGN.md, "Portraits: the face engine and the portrait at the inspect pane" and "Portraits,
    /// phase 2"): the one place every portrait of a person in Rimfolk is drawn from, so a face is the same everywhere. No
    /// painted art: the picture is the game's own drawing of the pawn, so every mod's gear, race, hair and face (Facial
    /// Animation's eyes and mouth among them) comes with it.
    /// <list type="bullet">
    /// <item>Framed by the face: the camera is centred on the pawn's own head as the game draws it (the head's place and
    /// size from its render tree, so the body type, the life stage, the race's draw size and Humanoid Alien Races'
    /// offsets are in it; else the body type's head offset, which that mod patches too), and the framing is counted in
    /// head sizes (<see cref="FaceMath"/>), so a child, a big xenotype and another mod's race are framed alike.</item>
    /// <item>The steady face: headgear left off unless the player wants it (helmets come and go, the face stays), the
    /// clothes on unless they switch them off; the game's portrait cache takes both.</item>
    /// <item>Behind them their own colour (the favourite colour the game gives each person, else their people's) with a
    /// soft light behind the head, and the mark of their people or the colony (the game's faction icon in the faction's
    /// colour, as the game draws it), all drawn by code.</item>
    /// <item>Drawn sharp: always rendered at the size it is shown (the game's cache adds its supersampling and the
    /// interface scale), never a small picture made larger; the cache keeps only what was drawn in the last second and
    /// redraws a pawn the game marks dirty, so only the portraits on screen are held, at their size.</item>
    /// <item>Phase 2: the way they face (the game's side views for two facing each other), a pose by who they are and
    /// how they are (the head drawn apart from the body and set on it, <c>FaceEngineSplit.cs</c>; the pose chosen and kept
    /// steady, <c>FaceEnginePoses.cs</c>), and a little movement on the large portraits: breathing, a slow sway, now and
    /// then a turn to a side view and back, all as transforms of the pictures already made.</item>
    /// </list>
    /// </summary>
    [StaticConstructorOnStartup]
    public static partial class FaceEngine
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Headgear in portraits (off: the steady face).</summary>
        public static bool Headgear => S?.portraitHeadgear ?? false;

        /// <summary>Clothes in portraits (on).</summary>
        public static bool Clothes => S?.portraitClothes ?? true;

        /// <summary>Poses in portraits (on).</summary>
        public static bool Poses => S?.portraitPoses ?? true;

        /// <summary>The large portraits move a little (on).</summary>
        public static bool Moves => S?.portraitsMove ?? true;

        /// <summary>What the engine read of a pawn's drawing, and their colours: kept a while, forgotten when the game marks them dirty.</summary>
        private struct Look
        {
            public bool body;
            // The head in the front view, and in the side view facing right (facing left is its mirror).
            public float headX, headZ, headSize, sideX, sideZ, sideSize, drawW, drawH;
            public bool firm;
            public int tries;
            public float until;
            public bool hasColour;
            public Color colour;
            public Color back, glow;
        }

        private static readonly Dictionary<int, Look> looks = new Dictionary<int, Look>();
        private static readonly HashSet<int> failed = new HashSet<int>();
        private static Game game;

        /// <summary>A soft round light (full in the middle, nothing at the edge), drawn behind the head in their colour.</summary>
        private static readonly Texture2D GlowTex = MakeGlow(64);

        /// <summary>How long what was read of a pawn is kept, and how soon a reading the game's drawing couldn't give yet is tried again.</summary>
        private const float KeepFor = 30f, RetryIn = 0.5f;

        private const int Tries = 4;

        /// <summary>
        /// Pawns the game marked dirty or redrew since the last portrait was drawn: a set, so it holds each pawn once however
        /// often they are marked (a mod may mark a face many times a second), locked as the game may mark one from another thread.
        /// </summary>
        private static readonly HashSet<int> forgotten = new HashSet<int>();
        private static volatile bool anyForgotten;

        /// <summary>The game marked the pawn's portrait dirty or drew it again (new clothes, grown up, a new face): read them and draw them again.</summary>
        public static void Forget(Pawn p)
        {
            if (p == null) return;
            lock (forgotten)
            {
                forgotten.Add(p.thingIDNumber);
                anyForgotten = true;
            }
        }

        /// <summary>A new game: nothing read before holds (ids start again).</summary>
        private static void NewGameCheck()
        {
            if (Current.Game == game) return;
            looks.Clear();
            failed.Clear();
            bearings.Clear();
            ClearSplits();
            game = Current.Game;
        }

        /// <summary>Takes in the pawns the game marked: what was read of them, their pictures drawn apart and their grey ones are made again.</summary>
        private static void DrainForgotten()
        {
            if (!anyForgotten) return;
            lock (forgotten)
            {
                foreach (int id in forgotten)
                {
                    looks.Remove(id);
                    MarkSplitsDirty(id);
                }
                forgotten.Clear();
                anyForgotten = false;
            }
        }

        // ---------------- The camera and the picture ----------------

        /// <summary>The camera that frames this pawn for a picture this size, facing this way.</summary>
        public static FaceCamera Camera(Pawn p, FaceFrame frame, float w, float h, FaceTurn turn = FaceTurn.Front)
        {
            var l = LookOf(p);
            if (l.body) return FaceMath.FrameBody(l.drawW, l.drawH, w, h);
            if (turn == FaceTurn.Front) return FaceMath.Frame(frame, l.headX, l.headZ, l.headSize, w, h);
            // The game draws the left view as the right one mirrored.
            float x = turn == FaceTurn.Right ? l.sideX : -l.sideX;
            return FaceMath.Frame(frame, x, l.sideZ, l.sideSize, w, h);
        }

        /// <summary>
        /// The game's own drawing of them, whole, framed by their face, facing <paramref name="turn"/>, and rendered at
        /// <paramref name="size"/> (whole interface units, as shown); null when the game can't draw them.
        /// </summary>
        public static Texture Picture(Pawn p, Vector2 size, FaceFrame frame, out FaceCamera cam, FaceTurn turn = FaceTurn.Front)
        {
            size = Whole(size);
            cam = Camera(p, frame, size.x, size.y, turn);
            if (p == null || failed.Contains(p.thingIDNumber)) return null;
            try
            {
                return PortraitsCache.Get(p, size, RotFor(turn), new Vector3(cam.x, 0f, cam.z), cam.zoom, true, true, Headgear, Clothes);
            }
            catch (Exception)
            {
                // Drawn as the game's small icon from now on (a pawn another mod draws in a way the cache can't).
                failed.Add(p.thingIDNumber);
                return null;
            }
        }

        private static Vector2 Whole(Vector2 size) => new Vector2(Mathf.Max(1f, Mathf.Round(size.x)), Mathf.Max(1f, Mathf.Round(size.y)));

        /// <summary>The game's view for a turn: the front (south) or a side (east to the right, west to the left).</summary>
        public static Rot4 RotFor(FaceTurn turn) => turn == FaceTurn.Right ? Rot4.East : turn == FaceTurn.Left ? Rot4.West : Rot4.South;

        /// <summary>The ground behind them: their colour mixed into the portrait ground of the look drawn in (the plain ground for the dead).</summary>
        public static Color Backdrop(Pawn p) => p != null && p.Dead && Poses ? UiKit.PortraitBack : LookOf(p).back;

        /// <summary>The light's colour for this pawn.</summary>
        public static Color GlowColour(Pawn p) => LookOf(p).glow;

        // ---------------- Drawing ----------------

        /// <summary>
        /// A portrait in a tile (square or tall): their colour behind, the light behind the head, the game's drawing of
        /// them framed by the face in their pose. With <paramref name="renderSize"/>, the pictures are rendered at that size
        /// and drawn into the tile (while the large portrait is being resized, so no picture is made at every step of the
        /// drag). With <paramref name="moving"/> (the large portrait, the colonist card), a little movement.
        /// </summary>
        public static void Draw(Rect r, Pawn p, FaceFrame frame, Vector2? renderSize = null, float alpha = 1f, bool moving = false)
        {
            if (p == null || Event.current.type != EventType.Repaint) return;
            Tick();
            var l = LookOf(p);
            var pose = PoseOf(p, FaceTurn.Front, true);
            var size = Whole(renderSize ?? r.size);
            bool dead = pose.bearing == FaceBearing.Dead;
            Widgets.DrawBoxSolid(r, UiKit.WithAlpha(dead ? UiKit.PortraitBack : l.back, alpha));
            var cam = Camera(p, frame, size.x, size.y, pose.turn);
            if (!dead) DrawGlow(r, cam, l.glow, alpha);
            int seed = p.thingIDNumber;
            float clock = moving && Moves ? Clock : 0f;
            bool animate = moving && Moves && !dead;
            float k = 0f;
            bool right = false;
            if (animate) k = FacePoseMath.Turned(clock, seed, out right);
            GUI.BeginGroup(r);
            try
            {
                var local = new Rect(0f, 0f, r.width, r.height);
                float breath = animate ? FacePoseMath.Breath(clock, seed) : 1f, sway = animate ? FacePoseMath.Sway(clock, seed) : 0f;
                // A turn fades one view into the other, each full until halfway, so the figure never thins as it turns.
                float usual = Mathf.Min(1f, 2f * (1f - k)), turned = Mathf.Min(1f, 2f * k);
                if (usual > 0.001f) DrawLayer(local, p, frame, size, pose, usual * alpha, breath, sway);
                if (turned > 0.001f) DrawLayer(local, p, frame, size, new FacePose(FacePoseMath.TurnedTo(pose.turn, right), pose.bearing), turned * alpha, breath, sway);
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// One picture of them in a pose, in a tile (inside its group): the whole figure leaning and swaying about its feet,
        /// the body breathing from its feet, the head set on it where the pose puts it; the whole picture where the head
        /// can't be drawn apart, or the pose doesn't move it.
        /// </summary>
        private static void DrawLayer(Rect r, Pawn p, FaceFrame frame, Vector2 size, FacePose pose, float alpha, float breath, float sway)
        {
            var cam = Camera(p, frame, size.x, size.y, pose.turn);
            var shift = FacePoseMath.Shift(pose.bearing, pose.turn, p.thingIDNumber);
            Texture body = null, head = null, whole = null;
            if (shift.Apart && !Split(p, size, frame, pose.turn, cam, out body, out head)) body = head = null;
            if (body == null)
            {
                whole = Picture(p, size, frame, out cam, pose.turn);
                if (pose.bearing == FaceBearing.Dead) whole = Grey(p, whole, pose.turn) ?? whole;
            }
            var saved = GUI.matrix;
            try
            {
                float lean = shift.lean + sway;
                if (lean != 0f) UI.RotateAroundPivot(lean, new Vector2(r.center.x, r.yMax));
                var bodyRect = new Rect(r.x, r.yMax - r.height * breath, r.width, r.height * breath);
                GUI.color = UiKit.WithAlpha(UiKit.Plain, alpha);
                if (whole != null)
                {
                    GUI.DrawTexture(bodyRect, whole);
                    return;
                }
                if (body == null)
                {
                    Widgets.ThingIcon(r, p);
                    return;
                }
                GUI.DrawTexture(bodyRect, body);
                // The head rides on the body's breath, moved and turned about the neck as the pose puts it.
                float headPx = cam.headShare * r.height;
                float neckY = r.y + (cam.headV + FacePoseMath.NeckBelow * cam.headShare) * r.height;
                float lift = (r.yMax - neckY) * (breath - 1f);
                var headRect = new Rect(r.x + shift.dx * headPx, r.y - shift.dy * headPx - lift, r.width, r.height);
                if (shift.tilt != 0f) UI.RotateAroundPivot(shift.tilt, new Vector2(r.x + cam.headU * r.width + shift.dx * headPx, neckY - shift.dy * headPx - lift));
                GUI.DrawTexture(headRect, head);
            }
            finally
            {
                GUI.matrix = saved;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// A round portrait's picture in <paramref name="inner"/> (UiKit.Portrait draws the disc, the ring and the mark): the
        /// light behind the head, then the whole picture in the circle, or the body and then the head set on it for a
        /// pose, or the grey picture of the dead.
        /// </summary>
        public static void DrawRound(Rect inner, Pawn p, FacePose pose, float alpha)
        {
            if (p == null || Event.current.type != EventType.Repaint) return;
            Tick();
            var size = Whole(inner.size);
            var cam = Camera(p, FaceFrame.Face, size.x, size.y, pose.turn);
            bool dead = pose.bearing == FaceBearing.Dead;
            if (!dead && inner.width >= 30f) DrawGlow(inner, cam, GlowColour(p), alpha);
            var shift = FacePoseMath.Shift(pose.bearing, pose.turn, p.thingIDNumber);
            GUI.color = UiKit.WithAlpha(UiKit.Plain, alpha);
            if (shift.Apart && Split(p, size, FaceFrame.Face, pose.turn, cam, out var body, out var head))
            {
                UiKit.DrawInCircle(inner, body);
                var saved = GUI.matrix;
                try
                {
                    float headPx = cam.headShare * inner.height;
                    float neckY = inner.y + (cam.headV + FacePoseMath.NeckBelow * cam.headShare) * inner.height;
                    if (shift.tilt != 0f) UI.RotateAroundPivot(shift.tilt, new Vector2(inner.x + cam.headU * inner.width + shift.dx * headPx, neckY - shift.dy * headPx));
                    // The head moved inside the circle: its picture read from further along, so the circle itself stays put.
                    UiKit.DrawInCircle(inner, head, shift.dx * headPx / inner.width, -shift.dy * headPx / inner.height);
                }
                finally
                {
                    GUI.matrix = saved;
                }
            }
            else
            {
                var tex = Picture(p, size, FaceFrame.Face, out cam, pose.turn);
                if (dead && tex != null) tex = Grey(p, tex, pose.turn) ?? tex;
                if (tex != null) UiKit.DrawInCircle(inner, tex);
                else Widgets.ThingIcon(inner, p);
            }
            GUI.color = Color.white;
        }

        /// <summary>The light behind the head, for a picture drawn in <paramref name="r"/> with this camera.</summary>
        public static void DrawGlow(Rect r, FaceCamera cam, Color glow, float alpha = 1f)
        {
            if (GlowTex == null || Event.current.type != EventType.Repaint) return;
            float side = Mathf.Min(r.width, r.height);
            float d = Mathf.Clamp(0.9f * cam.headShare * r.height, 0.3f * side, 0.95f * side);
            var c = new Vector2(r.x + r.width * cam.headU, r.y + r.height * cam.headV);
            GUI.color = UiKit.WithAlpha(glow, 0.85f * alpha);
            GUI.DrawTexture(new Rect(c.x - d / 2f, c.y - d / 2f, d, d), GlowTex);
            GUI.color = Color.white;
        }

        // ---------------- The clock of the movement ----------------

        private static float clock;
        private static int clockFrame = -1;

        /// <summary>Seconds of movement so far: it runs only while the game runs, so a paused game's portraits hold still as its map does.</summary>
        public static float Clock => clock;

        /// <summary>Once a frame: the clock moves on (not while paused), and pictures no longer drawn are let go.</summary>
        private static void Tick()
        {
            if (clockFrame == Time.frameCount) return;
            clockFrame = Time.frameCount;
            NewGameCheck();
            DrainForgotten();
            if (!(Find.TickManager?.Paused ?? true)) clock += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            PruneSplits();
        }

        // ---------------- The mark of their people or the colony ----------------

        /// <summary>
        /// Their people for the mark: the faction they belong to, so the colony's own (colonists and its slaves) carry the
        /// colony's, a prisoner or a visitor their own people's; none for the factionless.
        /// </summary>
        public static Faction PeopleOf(Pawn p) => p?.Faction;

        /// <summary>The faction's icon as the game has it, or null.</summary>
        public static Texture2D MarkOf(Faction f)
        {
            if (f?.def == null) return null;
            try
            {
                var icon = f.def.FactionIcon;
                return icon == null || icon == BaseContent.BadTex ? null : icon;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The mark of their people on a small dark disc, in the faction's colour as the game tints it.</summary>
        public static void DrawMark(Rect r, Pawn p, float alpha = 1f)
        {
            var f = PeopleOf(p);
            var icon = MarkOf(f);
            if (icon == null) return;
            if (Event.current.type == EventType.Repaint)
            {
                UiKit.DiscFill(r, UiKit.WithAlpha(UiKit.WindowGround, 0.85f * alpha));
                Color c;
                try
                {
                    c = f.Color;
                }
                catch (Exception)
                {
                    c = Color.white;
                }
                GUI.color = UiKit.WithAlpha(c, alpha);
                GUI.DrawTexture(r.ContractedBy(Mathf.Max(2f, Mathf.Round(r.width * 0.2f))), icon);
                GUI.color = Color.white;
            }
        }

        // ---------------- What the engine reads of a pawn ----------------

        private static Look LookOf(Pawn p)
        {
            NewGameCheck();
            if (p == null) return default;
            DrainForgotten();
            float now = Time.realtimeSinceStartup;
            bool had = looks.TryGetValue(p.thingIDNumber, out var l);
            if (!had || now >= l.until)
            {
                int tries = had && !l.firm ? l.tries : 0;
                l = Read(p);
                l.tries = tries + 1;
                if (!l.firm && l.tries >= Tries) l.firm = true;
                l.until = now + (l.firm ? KeepFor : RetryIn);
                l.hasColour = ColourOf(p, out l.colour);
                looks[p.thingIDNumber] = l;
            }
            // The ground is the look's (Rimfolk's own inside its windows, the game's elsewhere), so the colours are worked
            // out for the ground they sit on, once for each colour and ground.
            var ground = UiKit.PortraitBack;
            if (!l.hasColour)
            {
                l.back = ground;
                l.glow = UiKit.WithAlpha(UiKit.Edge, 0.6f);
                return l;
            }
            uint g = Rgb(ground), c = Rgb(l.colour);
            ulong key = ((ulong)g << 32) | c;
            if (!shares.TryGetValue(key, out var share))
            {
                share = (FaceMath.BackdropShare(c, g), FaceMath.GlowShare(c, g));
                shares[key] = share;
            }
            var opaque = UiKit.WithAlpha(l.colour, 1f);
            l.back = Color.Lerp(ground, opaque, share.back);
            l.glow = Color.Lerp(ground, opaque, share.glow);
            return l;
        }

        /// <summary>How much of a colour goes into the backdrop and the light on a ground, worked out once for each colour and ground.</summary>
        private static readonly Dictionary<ulong, (float back, float glow)> shares = new Dictionary<ulong, (float back, float glow)>();

        private static Look Read(Pawn p)
        {
            var l = new Look();
            try
            {
                if (p.RaceProps == null || !p.RaceProps.Humanlike)
                {
                    // An animal or a mechanoid: its whole drawing, by the size the game draws it at its age.
                    var size = p.ageTracker?.CurKindLifeStage?.bodyGraphicData?.drawSize ?? new Vector2(FaceMath.HeadMesh, FaceMath.HeadMesh);
                    l.body = true;
                    l.drawW = size.x;
                    l.drawH = size.y;
                    l.firm = true;
                    return l;
                }
                if (FromTree(p, Rot4.South, out l.headX, out l.headZ, out l.headSize))
                {
                    if (!FromTree(p, Rot4.East, out l.sideX, out l.sideZ, out l.sideSize)) Fallback(p, Rot4.East, out l.sideX, out l.sideZ, out l.sideSize);
                    l.firm = true;
                    return l;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading a pawn's head from the game's drawing", e);
            }
            // The game hasn't drawn them yet (or can't be read): tried again shortly.
            Fallback(p, Rot4.South, out l.headX, out l.headZ, out l.headSize);
            Fallback(p, Rot4.East, out l.sideX, out l.sideZ, out l.sideSize);
            return l;
        }

        /// <summary>The head offset the game gives their body type and age for a view (Humanoid Alien Races patches it for its races), and the life stage's head size.</summary>
        private static void Fallback(Pawn p, Rot4 rot, out float x, out float z, out float size)
        {
            var off = new Vector3(0f, 0f, FaceMath.GrownHeadZ);
            try
            {
                off = p.Drawer?.renderer?.BaseHeadOffsetAt(rot) ?? off;
            }
            catch (Exception)
            {
                off = new Vector3(0f, 0f, FaceMath.GrownHeadZ);
            }
            float factor = 1f;
            try
            {
                factor = p.ageTracker?.CurLifeStage?.headSizeFactor ?? 1f;
            }
            catch (Exception)
            {
                factor = 1f;
            }
            x = off.x;
            z = off.z;
            size = FaceMath.FallbackHeadSize(factor);
        }

        /// <summary>
        /// The head as the game draws it in a portrait: its node in the pawn's render tree, placed and sized by the tree's
        /// own matrix for the view (every offset and scale the game, the race and other mods put on it), and the size of its
        /// mesh. False while the tree isn't built yet.
        /// </summary>
        private static bool FromTree(Pawn p, Rot4 rot, out float x, out float z, out float size)
        {
            x = z = size = 0f;
            var tree = p.Drawer?.renderer?.renderTree;
            if (tree == null || !tree.Resolved || PawnRenderNodeTagDefOf.Head == null) return false;
            if (!tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out var node) || node == null) return false;
            var parms = new PawnDrawParms
            {
                pawn = p,
                matrix = Matrix4x4.identity,
                facing = rot,
                rotDrawMode = RotDrawMode.Fresh,
                posture = PawnPosture.Standing,
                flags = PawnRenderFlags.Portrait | PawnRenderFlags.Cache | PawnRenderFlags.DrawNow | PawnRenderFlags.Clothes | PawnRenderFlags.Headgear,
                tint = Color.white
            };
            if (!tree.TryGetMatrix(node, parms, out var m)) return false;
            Vector3 at = m.GetColumn(3);
            float sx = ((Vector3)m.GetColumn(0)).magnitude, sz = ((Vector3)m.GetColumn(2)).magnitude;
            float meshW = FaceMath.HeadMesh, meshH = FaceMath.HeadMesh;
            var mesh = node.GetMesh(parms);
            if (mesh != null)
            {
                var b = mesh.bounds.size;
                if (b.x > 0.05f) meshW = b.x;
                if (b.z > 0.05f) meshH = b.z;
            }
            x = at.x;
            z = at.z;
            size = Mathf.Max(meshW * (sx > 0.01f ? sx : 1f), meshH * (sz > 0.01f ? sz : 1f));
            return FaceMath.Plausible(x, z, size);
        }

        /// <summary>Their colour: their favourite (Ideology gives each person one), else their people's; false for neither.</summary>
        public static bool ColourOf(Pawn p, out Color c)
        {
            c = default;
            try
            {
                var fav = p?.story?.favoriteColor;
                if (fav != null)
                {
                    c = fav.color;
                    return true;
                }
                var f = PeopleOf(p);
                if (f != null)
                {
                    c = f.Color;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        private static uint Rgb(Color c) =>
            ((uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255) << 16) | ((uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255) << 8)
            | (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);

        private static Texture2D MakeGlow(int size)
        {
            try
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "RimfolkFaceGlow",
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var px = new Color32[size * size];
                float c = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = (1f - d) * (1f - d);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                return tex;
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't make the portraits' light: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// The game marks a pawn's portrait dirty when how they look changes (clothes, hair, growing up, a new body): the face
    /// engine reads their head again and draws again what it drew apart. It changes nothing of the game's, so it isn't
    /// switched by the deeper game hooks. Applied on its own.
    /// </summary>
    [HarmonyPatch(typeof(PortraitsCache), nameof(PortraitsCache.SetDirty))]
    public static class Patch_FaceEngineDirty
    {
        public static void Postfix(Pawn pawn)
        {
            FaceEngine.Forget(pawn);
        }
    }
}
