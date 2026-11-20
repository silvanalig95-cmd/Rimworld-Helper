using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The head drawn apart from the body (Portraits, phase 2), so a pose can set it at a small angle and offset on the
    /// body. The game has no head-only render flag (1.6 and 1.7 have <c>HeadStump</c> and <c>NoBody</c>, no
    /// <c>HeadOnly</c>), so both pictures are the game's own drawing through its pawn renderer, at the same camera:
    /// <list type="bullet">
    /// <item>the body: the game's own call without the head (<c>renderHead</c> off, which draws with <c>HeadStump</c>);</item>
    /// <item>the head: the same call with <c>NoBody</c> added to the draw's flags while it renders this pawn
    /// (<see cref="Patch_FaceHeadOnlyDraw"/>, <see cref="Patch_FaceHeadOnlyPreDraw"/>), the flag the game draws a pawn in
    /// bed with, its body under the blanket.</item>
    /// </list>
    /// Before a pawn is drawn apart, a small test picture of their whole figure checks the game drew it so: the head
    /// picture has nothing at the feet, the body picture nothing at the crown (<see cref="FacePoseMath.SplitLooksRight"/>).
    /// A pawn for whom it didn't (a race with a body part drawn with the head, wings above it) keeps the whole picture
    /// and its pose's lean only; after three such pawns and none that came apart, no pawn is drawn apart this session.
    /// The pictures are Rimfolk's own, the size they are shown at (the game's supersampling and interface scale on it),
    /// drawn again when the game marks the pawn dirty or draws its portrait again, and let go a second after they were
    /// last shown, as the game's portrait cache does.
    /// <para>Also here: the dead drawn grey, a grey copy of their picture made once.</para>
    /// </summary>
    public static partial class FaceEngine
    {
        /// <summary>The pawn whose head alone is being drawn (read by the two hooks), or null.</summary>
        public static volatile Pawn HeadOnly;

        private struct SplitKey : IEquatable<SplitKey>
        {
            public int id, w, h, cx, cz, zoom;
            public byte turn;
            public bool headgear, clothes;

            public bool Equals(SplitKey o) =>
                id == o.id && w == o.w && h == o.h && cx == o.cx && cz == o.cz && zoom == o.zoom && turn == o.turn && headgear == o.headgear && clothes == o.clothes;

            public override bool Equals(object obj) => obj is SplitKey o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int x = id * 397 ^ w;
                    x = x * 397 ^ h;
                    x = x * 397 ^ cx;
                    x = x * 397 ^ cz;
                    x = x * 397 ^ zoom;
                    return x * 397 ^ (turn | (headgear ? 4 : 0) | (clothes ? 8 : 0));
                }
            }
        }

        private sealed class SplitPair
        {
            public RenderTexture body, head;
            public float lastUsed, drawnAt;
            public bool dirty = true;
        }

        private sealed class GreyCopy
        {
            public Texture2D tex;
            public Texture from;
            public float lastUsed;
            public bool dirty;
        }

        private static readonly Dictionary<SplitKey, SplitPair> splits = new Dictionary<SplitKey, SplitPair>();
        private static readonly Dictionary<SplitKey, GreyCopy> greys = new Dictionary<SplitKey, GreyCopy>();
        /// <summary>Whether a pawn came apart in the test picture (checked once a game).</summary>
        private static readonly Dictionary<int, bool> splitOk = new Dictionary<int, bool>();
        private static int splitGood, splitBad;
        private static bool splitOff;
        private static readonly List<SplitKey> dropKeys = new List<SplitKey>();
        private static readonly List<SplitKey> dropGreys = new List<SplitKey>();

        /// <summary>Pictures not shown for this long are let go; one marked dirty is drawn again at most this often.</summary>
        private const float LetGoAfter = 1f, RedrawEvery = 0.2f;

        private static int hooksIn = -1;

        /// <summary>Whether heads can be drawn apart at all: the deeper game hooks on and both hooks in place (looked up once).</summary>
        private static bool SplitOn
        {
            get
            {
                if (splitOff || !RimfolkPatches.On) return false;
                if (hooksIn < 0)
                    hooksIn = RimfolkPatches.Applied.Contains(nameof(Patch_FaceHeadOnlyDraw)) && RimfolkPatches.Applied.Contains(nameof(Patch_FaceHeadOnlyPreDraw)) ? 1 : 0;
                return hooksIn == 1;
            }
        }

        /// <summary>
        /// The body and the head drawn apart for a picture this size, facing this way, framed by this camera; false where
        /// they can't be (the pawn kept whole).
        /// </summary>
        private static bool Split(Pawn p, Vector2 size, FaceFrame frame, FaceTurn turn, FaceCamera cam, out Texture body, out Texture head)
        {
            body = head = null;
            if (p == null || !SplitOn || p.Dead || failed.Contains(p.thingIDNumber) || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            if (!splitOk.TryGetValue(p.thingIDNumber, out bool ok))
            {
                ok = Probe(p);
                splitOk[p.thingIDNumber] = ok;
                if (ok) splitGood++;
                else splitBad++;
                if (splitGood == 0 && splitBad >= FacePoseMath.SplitGiveUpAfter)
                {
                    splitOff = true;
                    CouncilLog.WarnOnce("Portraits: the game didn't draw a head apart from its body for " + splitBad + " people, so poses keep their whole pictures this session.");
                }
            }
            if (!ok) return false;
            float scale = Prefs.UIScale;
            int pw = FaceMath.RenderPixels(size.x, scale), ph = FaceMath.RenderPixels(size.y, scale);
            var key = new SplitKey
            {
                id = p.thingIDNumber, w = pw, h = ph, cx = Mathf.RoundToInt(cam.x * 1000f), cz = Mathf.RoundToInt(cam.z * 1000f), zoom = Mathf.RoundToInt(cam.zoom * 1000f),
                turn = (byte)turn, headgear = Headgear, clothes = Clothes
            };
            float now = Time.realtimeSinceStartup;
            if (!splits.TryGetValue(key, out var pair))
            {
                pair = new SplitPair { body = NewTexture(pw, ph), head = NewTexture(pw, ph) };
                splits[key] = pair;
            }
            pair.lastUsed = now;
            if (pair.dirty && now - pair.drawnAt >= RedrawEvery)
            {
                try
                {
                    DrawApart(p, pair.body, pair.head, cam, turn);
                    pair.dirty = false;
                    pair.drawnAt = now;
                }
                catch (Exception e)
                {
                    failed.Add(p.thingIDNumber);
                    CouncilLog.ErrorOnce("portraits: drawing a head apart from its body", e);
                    return false;
                }
            }
            body = pair.body;
            head = pair.head;
            return true;
        }

        private static RenderTexture NewTexture(int w, int h) =>
            new RenderTexture(Mathf.Max(1, w), Mathf.Max(1, h), 24) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "RimfolkFacePart" };

        /// <summary>The game's own drawing of the pawn twice at one camera: without the head, and the head alone.</summary>
        private static void DrawApart(Pawn p, RenderTexture body, RenderTexture head, FaceCamera cam, FaceTurn turn)
        {
            var renderer = Find.PawnCacheRenderer;
            var at = new Vector3(cam.x, 0f, cam.z);
            var rot = RotFor(turn);
            var active = RenderTexture.active;
            try
            {
                renderer.RenderPawn(p, body, at, cam.zoom, 0f, rot, false, Headgear, Clothes, true);
                HeadOnly = p;
                renderer.RenderPawn(p, head, at, cam.zoom, 0f, rot, true, Headgear, Clothes, true);
            }
            finally
            {
                HeadOnly = null;
                RenderTexture.active = active;
            }
        }

        /// <summary>
        /// The test: a small picture of the whole figure drawn both ways, read back and checked
        /// (<see cref="FacePoseMath.SplitLooksRight"/>). Once a game for each pawn shown in a pose.
        /// </summary>
        private static bool Probe(Pawn p)
        {
            const int W = 32, H = 64;
            RenderTexture body = null, head = null;
            Texture2D read = null;
            var active = RenderTexture.active;
            try
            {
                var cam = Camera(p, FaceFrame.Figure, W, H, FaceTurn.Front);
                body = RenderTexture.GetTemporary(W, H, 24);
                head = RenderTexture.GetTemporary(W, H, 24);
                DrawApart(p, body, head, cam, FaceTurn.Front);
                read = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var b = Rows(body, read, W, H);
                var h = Rows(head, read, W, H);
                return FacePoseMath.SplitLooksRight(b, h, W);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: testing a head drawn apart from its body", e);
                return false;
            }
            finally
            {
                RenderTexture.active = active;
                if (body != null) RenderTexture.ReleaseTemporary(body);
                if (head != null) RenderTexture.ReleaseTemporary(head);
                if (read != null) UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>Each row's alpha summed across, read back from a picture.</summary>
        private static float[] Rows(RenderTexture rt, Texture2D read, int w, int h)
        {
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
            read.Apply(false);
            var px = read.GetPixels32();
            var rows = new float[h];
            for (int y = 0; y < h; y++)
            {
                float s = 0f;
                for (int x = 0; x < w; x++) s += px[y * w + x].a / 255f;
                rows[y] = s;
            }
            return rows;
        }

        /// <summary>
        /// The dead drawn grey: a copy of their picture in greys (no shader to do it as it draws), made once from the
        /// picture the game drew and again only if the game draws them again; let go when no longer shown.
        /// </summary>
        private static Texture Grey(Pawn p, Texture from, FaceTurn turn)
        {
            if (p == null || !(from is RenderTexture rt) || rt.width <= 0 || rt.height <= 0) return null;
            var key = new SplitKey { id = p.thingIDNumber, w = rt.width, h = rt.height, turn = (byte)turn };
            float now = Time.realtimeSinceStartup;
            if (greys.TryGetValue(key, out var g) && g.tex != null && !g.dirty && g.from == from)
            {
                g.lastUsed = now;
                return g.tex;
            }
            var active = RenderTexture.active;
            try
            {
                if (g == null)
                {
                    g = new GreyCopy();
                    greys[key] = g;
                }
                if (g.tex == null || g.tex.width != rt.width || g.tex.height != rt.height)
                {
                    if (g.tex != null) UnityEngine.Object.Destroy(g.tex);
                    g.tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "RimfolkFaceGrey" };
                }
                RenderTexture.active = rt;
                g.tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0, false);
                var px = g.tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    byte l = FacePoseMath.GreyOf(c.r, c.g, c.b);
                    px[i] = new Color32(l, l, l, c.a);
                }
                g.tex.SetPixels32(px);
                g.tex.Apply(false);
                g.from = from;
                g.dirty = false;
                g.lastUsed = now;
                return g.tex;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the grey picture of the dead", e);
                return null;
            }
            finally
            {
                RenderTexture.active = active;
            }
        }

        /// <summary>The pawn was marked dirty or drawn again: its pictures drawn apart and its grey ones are made again.</summary>
        private static void MarkSplitsDirty(int id)
        {
            foreach (var kv in splits)
                if (kv.Key.id == id)
                    kv.Value.dirty = true;
            foreach (var kv in greys)
                if (kv.Key.id == id)
                    kv.Value.dirty = true;
        }

        /// <summary>Lets go of the pictures not shown in the last second.</summary>
        private static void PruneSplits()
        {
            if (splits.Count == 0 && greys.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            dropKeys.Clear();
            foreach (var kv in splits)
                if (now - kv.Value.lastUsed > LetGoAfter)
                    dropKeys.Add(kv.Key);
            foreach (var k in dropKeys)
            {
                Release(splits[k]);
                splits.Remove(k);
            }
            dropGreys.Clear();
            foreach (var kv in greys)
                if (now - kv.Value.lastUsed > LetGoAfter * 2f)
                    dropGreys.Add(kv.Key);
            foreach (var k in dropGreys)
            {
                if (greys[k].tex != null) UnityEngine.Object.Destroy(greys[k].tex);
                greys.Remove(k);
            }
        }

        private static void Release(SplitPair s)
        {
            foreach (var rt in new[] { s.body, s.head })
            {
                if (rt == null) continue;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        /// <summary>A new game: every picture drawn apart and every grey one let go, every test forgotten.</summary>
        private static void ClearSplits()
        {
            foreach (var s in splits.Values) Release(s);
            splits.Clear();
            foreach (var g in greys.Values)
                if (g.tex != null)
                    UnityEngine.Object.Destroy(g.tex);
            greys.Clear();
            splitOk.Clear();
        }
    }

    /// <summary>
    /// While the face engine draws a pawn's head alone (<see cref="FaceEngine.HeadOnly"/>), that pawn's draw is made with
    /// the game's <c>NoBody</c> flag: its body and the clothes on it are left out. Every other draw passes untouched. The
    /// game builds what it draws here, before drawing it. Applied on its own; without it (or its pair), heads aren't drawn
    /// apart and poses keep the whole picture.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
    public static class Patch_FaceHeadOnlyPreDraw
    {
        public static void Prefix(ref PawnDrawParms parms)
        {
            var only = FaceEngine.HeadOnly;
            if (only != null && parms.pawn == only) parms.flags |= PawnRenderFlags.NoBody;
        }
    }

    /// <summary>The same for the draw itself (see <see cref="Patch_FaceHeadOnlyPreDraw"/>). Applied on its own.</summary>
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.Draw))]
    public static class Patch_FaceHeadOnlyDraw
    {
        public static void Prefix(ref PawnDrawParms parms)
        {
            var only = FaceEngine.HeadOnly;
            if (only != null && parms.pawn == only) parms.flags |= PawnRenderFlags.NoBody;
        }
    }

    /// <summary>
    /// The game drew a pawn's portrait again (dirty, or a face that changes as Facial Animation's does): the face engine's
    /// pictures of them drawn apart are drawn again with it, so the expression comes as the game draws it, never forced.
    /// The game's portrait parameters are a private struct, so the method is found by name; without it, those pictures
    /// are drawn again when the game marks the pawn dirty. Applied on its own.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_FaceEngineRedrawn
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target()
        {
            var t = AccessTools.Inner(typeof(PortraitsCache), "PortraitParams");
            return t == null ? null : AccessTools.Method(t, "RenderPortrait", new[] { typeof(Pawn), typeof(RenderTexture) });
        }

        public static void Postfix(Pawn pawn)
        {
            FaceEngine.Forget(pawn);
        }
    }
}
