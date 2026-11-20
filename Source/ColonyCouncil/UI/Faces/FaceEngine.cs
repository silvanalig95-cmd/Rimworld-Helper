using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The face engine (docs: DESIGN.md, "Portraits: the face engine and the portrait at the inspect pane"): the one place
    /// every portrait of a person in Rimfolk is drawn from, so a face is the same everywhere. No painted art: the picture
    /// is the game's own drawing of the pawn from its portrait cache, so every mod's gear, race, hair and face (Facial
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
    /// </list>
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FaceEngine
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Headgear in portraits (off: the steady face).</summary>
        public static bool Headgear => S?.portraitHeadgear ?? false;

        /// <summary>Clothes in portraits (on).</summary>
        public static bool Clothes => S?.portraitClothes ?? true;

        /// <summary>What the engine read of a pawn's drawing, and their colours: kept a while, forgotten when the game marks them dirty.</summary>
        private struct Look
        {
            public bool body;
            public float headX, headZ, headSize, drawW, drawH;
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

        /// <summary>Pawns the game marked dirty since the last portrait was drawn (the game may mark one from another thread).</summary>
        private static readonly System.Collections.Concurrent.ConcurrentQueue<int> forgotten = new System.Collections.Concurrent.ConcurrentQueue<int>();

        /// <summary>The game marked the pawn's portrait dirty (new clothes, grown up, a new look): read them again.</summary>
        public static void Forget(Pawn p)
        {
            if (p != null) forgotten.Enqueue(p.thingIDNumber);
        }

        /// <summary>The camera that frames this pawn for a picture this size.</summary>
        public static FaceCamera Camera(Pawn p, FaceFrame frame, float w, float h)
        {
            var l = LookOf(p);
            return l.body ? FaceMath.FrameBody(l.drawW, l.drawH, w, h) : FaceMath.Frame(frame, l.headX, l.headZ, l.headSize, w, h);
        }

        /// <summary>
        /// The game's own drawing of them, framed by their face and rendered at <paramref name="size"/> (whole interface
        /// units, as shown); null when the game can't draw them.
        /// </summary>
        public static Texture Picture(Pawn p, Vector2 size, FaceFrame frame, out FaceCamera cam, FacePose pose = FacePose.Front)
        {
            size = new Vector2(Mathf.Max(1f, Mathf.Round(size.x)), Mathf.Max(1f, Mathf.Round(size.y)));
            cam = Camera(p, frame, size.x, size.y);
            if (p == null || failed.Contains(p.thingIDNumber)) return null;
            try
            {
                return PortraitsCache.Get(p, size, RotFor(pose), new Vector3(cam.x, 0f, cam.z), cam.zoom, true, true, Headgear, Clothes);
            }
            catch (Exception)
            {
                // Drawn as the game's small icon from now on (a pawn another mod draws in a way the cache can't).
                failed.Add(p.thingIDNumber);
                return null;
            }
        }

        private static Rot4 RotFor(FacePose pose) => Rot4.South;

        /// <summary>The ground behind them in their colour, and the light behind the head.</summary>
        public static Color Backdrop(Pawn p) => LookOf(p).back;

        /// <summary>
        /// A portrait in a tile (square or tall): their colour behind, the light behind the head, the game's drawing of
        /// them framed by the face. With <paramref name="renderSize"/>, the picture is rendered at that size and drawn
        /// into the tile (while the large portrait is being resized, so no picture is made at every step of the drag).
        /// </summary>
        public static void Draw(Rect r, Pawn p, FaceFrame frame, Vector2? renderSize = null, float alpha = 1f)
        {
            if (p == null || Event.current.type != EventType.Repaint) return;
            var l = LookOf(p);
            var tex = Picture(p, renderSize ?? r.size, frame, out var cam);
            Widgets.DrawBoxSolid(r, UiKit.WithAlpha(l.back, alpha));
            DrawGlow(r, cam, l.glow, alpha);
            if (tex != null)
            {
                GUI.color = UiKit.WithAlpha(UiKit.Plain, alpha);
                GUI.DrawTexture(r, tex);
            }
            else Widgets.ThingIcon(r, p);
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

        /// <summary>The light's colour for this pawn.</summary>
        public static Color GlowColour(Pawn p) => LookOf(p).glow;

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
            if (Current.Game != game)
            {
                looks.Clear();
                failed.Clear();
                game = Current.Game;
            }
            if (p == null) return default;
            while (!forgotten.IsEmpty && forgotten.TryDequeue(out int id)) looks.Remove(id);
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
                if (FromTree(p, out l.headX, out l.headZ, out l.headSize))
                {
                    l.firm = true;
                    return l;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading a pawn's head from the game's drawing", e);
            }
            // The game hasn't drawn them yet (or can't be read): the head offset it gives their body type and age, which
            // Humanoid Alien Races patches for its races, and the life stage's head size. Tried again shortly.
            var off = new Vector3(0f, 0f, FaceMath.GrownHeadZ);
            try
            {
                off = p.Drawer?.renderer?.BaseHeadOffsetAt(Rot4.South) ?? off;
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
            l.headX = off.x;
            l.headZ = off.z;
            l.headSize = FaceMath.FallbackHeadSize(factor);
            return l;
        }

        /// <summary>
        /// The head as the game draws it in a portrait: its node in the pawn's render tree, placed and sized by the tree's
        /// own matrix for the front view (every offset and scale the game, the race and other mods put on it), and the
        /// size of its mesh. False while the tree isn't built yet.
        /// </summary>
        private static bool FromTree(Pawn p, out float x, out float z, out float size)
        {
            x = z = size = 0f;
            var tree = p.Drawer?.renderer?.renderTree;
            if (tree == null || !tree.Resolved || PawnRenderNodeTagDefOf.Head == null) return false;
            if (!tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out var node) || node == null) return false;
            var parms = new PawnDrawParms
            {
                pawn = p,
                matrix = Matrix4x4.identity,
                facing = Rot4.South,
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
    /// engine reads their head again. It changes nothing of the game's, so it isn't switched by the deeper game hooks.
    /// Applied on its own.
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
