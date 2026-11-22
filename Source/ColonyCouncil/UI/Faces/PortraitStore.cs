using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One picture of the painted library as a file the store loads when it is shown (a person's cut-out or a backdrop):
    /// its PNG under its mod's <c>Portraits</c> folder, or for an older def a texture the game loaded under Textures.
    /// </summary>
    public sealed class PictureFile
    {
        public enum State : byte
        {
            None,
            Queued,
            Loading,
            Ready,
            Failed
        }

        /// <summary>The def's name, for the log.</summary>
        public string key;
        /// <summary>The PNG's full path, or null where the picture is a texture the game loaded (<see cref="texPath"/>).</summary>
        public string file;
        public string texPath;
        /// <summary>A person cut out on a clear ground (the head is measured from its shape, its clear edge given the person's colour); a backdrop is not.</summary>
        public bool cutOut;

        public State state;
        public Texture2D texture;
        /// <summary>The picture as kept (scaled down), and the head as measured from its alpha (not found for a backdrop or an old picture painted to its edges).</summary>
        public int width, height;
        public HeadMeasure head;
        public int lastUsed;
        /// <summary>Deleted for good (UI/Faces/PortraitDelete.cs): never loaded again, and what is still being read let go when it comes.</summary>
        public bool gone;

        public float Shape => height > 0 ? (float)width / height : 1f;
    }

    /// <summary>
    /// The painted library's pictures, loaded only when shown (docs: DESIGN.md, "Portraits, phase 4: the painted library in
    /// the game, with backdrops"; the player, 22 November 2026: "the impact on the gameplay and performance requirements
    /// on the device are hopefully not big"):
    /// <list type="bullet">
    /// <item>Nothing is read at start-up beyond the defs. A picture is asked for when a portrait first shows it; until it is
    /// ready the portrait shows its ground only (a fraction of a second).</item>
    /// <item>Its file is read on a worker thread; the game decodes it (<c>ImageConversion.LoadImage</c>), at most one a
    /// frame; the worker measures the head from its alpha, scales it down to what the largest portrait needs
    /// (<see cref="PortraitPictureMath.LongSideFor"/>: 450 pixels long at the interface's own scale, at most 720) and gives
    /// its clear edge the person's colour; the game makes the texture (with mipmaps, for the small round portraits), at
    /// most one a frame.</item>
    /// <item>Kept in a small cache: at most <see cref="MostPictures"/> pictures and <see cref="MostPixels"/> pixels, the
    /// least lately shown let go (its texture destroyed) when more are needed.</item>
    /// <item>A file that can't be read: one warning, and the game's own drawing for whoever it was for.</item>
    /// </list>
    /// </summary>
    public static class PortraitStore
    {
        public const int MostPictures = 48;
        public const long MostPixels = 12_000_000;

        /// <summary>How many pixels deep the clear edge round a person is given their colour.</summary>
        private const int BleedDepth = 4;

        private static readonly Queue<PictureFile> queued = new Queue<PictureFile>();
        private static readonly List<PictureFile> ready = new List<PictureFile>();
        private static long pixels;
        private static int pumped = -1;
        private static int loading;

        /// <summary>What the worker threads hand back, run on the game's thread: the heavy ones (a decode) one a frame, the light ones (a texture made) a few.</summary>
        private static readonly ConcurrentQueue<Action> heavyDone = new ConcurrentQueue<Action>(), lightDone = new ConcurrentQueue<Action>();

        /// <summary>For the dev log: pictures loaded this session, and the time the game's thread spent on them.</summary>
        public static int Loaded, FailedCount;
        public static double MainMs;

        /// <summary>The picture's texture where it is ready (it is marked as shown now), else null and its loading asked for.</summary>
        public static Texture2D Get(PictureFile f)
        {
            if (f == null || f.gone) return null;
            switch (f.state)
            {
                case PictureFile.State.Ready:
                    if (f.texture == null)
                    {
                        // Destroyed under us (the game quit to the menu, a mod let textures go): loaded again.
                        Drop(f);
                        goto case PictureFile.State.None;
                    }
                    f.lastUsed = Time.frameCount;
                    return f.texture;
                case PictureFile.State.None:
                    f.state = PictureFile.State.Queued;
                    f.lastUsed = Time.frameCount;
                    queued.Enqueue(f);
                    return null;
                default:
                    f.lastUsed = Time.frameCount;
                    return null;
            }
        }

        public static bool Failed(PictureFile f) => f != null && f.state == PictureFile.State.Failed;

        /// <summary>Runs work on a worker thread and its answer on the game's thread in a later frame (a heavy answer one a frame).</summary>
        public static void Run<T>(Func<T> work, Action<T, Exception> then, bool heavy)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                T r = default;
                Exception ex = null;
                try
                {
                    r = work();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                (heavy ? heavyDone : lightDone).Enqueue(() => then(r, ex));
            });
        }

        /// <summary>Once a frame, from the portraits' drawing: the answers of the worker threads, the next files started, and the cache kept small.</summary>
        public static void Pump()
        {
            if (pumped == Time.frameCount) return;
            pumped = Time.frameCount;
            if (heavyDone.TryDequeue(out var heavy)) Timed(heavy);
            for (int i = 0; i < 3 && lightDone.TryDequeue(out var light); i++) Timed(light);
            // A few files read at once on the worker threads; their decoding waits its turn.
            while (loading < 3 && queued.Count > 0) Start(queued.Dequeue());
            PortraitFolder.Pump();
            PortraitNeeds.Pump();
        }

        private static void Timed(Action a)
        {
            float t0 = Time.realtimeSinceStartup;
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: loading a picture of the library", e);
            }
            MainMs += (Time.realtimeSinceStartup - t0) * 1000.0;
        }

        private static void Start(PictureFile f)
        {
            if (f.state != PictureFile.State.Queued) return;
            if (f.gone)
            {
                f.state = PictureFile.State.None;
                return;
            }
            f.state = PictureFile.State.Loading;
            loading++;
            int longSide = PortraitPictureMath.LongSideFor(Prefs.UIScale);
            if (f.file == null)
            {
                // A texture the game loaded under Textures: copied small onto one of Rimfolk's own to read it.
                lightDone.Enqueue(() => FromGameTexture(f, longSide));
                return;
            }
            string path = f.file;
            Run(() => File.ReadAllBytes(path), (bytes, ex) => Decode(f, bytes, ex, longSide), true);
        }

        private static void Fail(PictureFile f, string why)
        {
            loading = Math.Max(0, loading - 1);
            f.state = PictureFile.State.Failed;
            FailedCount++;
            CouncilLog.WarnOnce("Portraits: the picture " + f.key + " could not be read (" + why + "), so the game's own drawing stays where it was meant for.");
        }

        /// <summary>The game decodes the file (on its own thread, as it must), then the worker measures, scales and edges it.</summary>
        private static void Decode(PictureFile f, byte[] bytes, Exception ex, int longSide)
        {
            if (f.gone)
            {
                Forgotten(f);
                return;
            }
            if (ex != null || bytes == null || bytes.Length == 0)
            {
                Fail(f, ex?.Message ?? "an empty file");
                return;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, bytes, false) || tex.width < 8 || tex.height < 8)
                {
                    Fail(f, "not a picture the game reads");
                    return;
                }
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                Process(f, px, w, h, longSide);
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static void FromGameTexture(PictureFile f, int longSide)
        {
            var src = f.texPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(f.texPath, false);
            if (src == null)
            {
                Fail(f, "no texture at " + f.texPath);
                return;
            }
            PortraitPictureMath.FitSize(src.width, src.height, longSide, out int w, out int h);
            var px = PortraitGpu.Read(src, w, h, Vector2.one, Vector2.zero);
            if (px == null)
            {
                Fail(f, "its texture could not be copied");
                return;
            }
            Process(f, ToColours(px), w, h, longSide);
        }

        /// <summary>On a worker thread: the head measured, the picture scaled down, the body made solid where it meets the picture's foot, its clear edge coloured; then the texture made.</summary>
        private static void Process(PictureFile f, Color32[] px, int w, int h, int longSide)
        {
            bool cut = f.cutOut;
            Run(() =>
            {
                var top = ToBytes(px, w, h);
                var head = cut ? PortraitPictureMath.MeasureHead(PortraitPictureMath.AlphaOf(top), w, h) : default;
                PortraitPictureMath.FitSize(w, h, longSide, out int nw, out int nh);
                var small = PortraitPictureMath.Downscale(top, w, h, nw, nh);
                if (cut)
                {
                    PortraitPictureMath.SolidFoot(small, nw, nh);
                    PortraitPictureMath.Bleed(small, nw, nh, BleedDepth);
                }
                return new Processed { px = ToColours(PortraitPictureMath.FlipRows(small, nw, nh)), w = nw, h = nh, head = head };
            }, (done, ex) => Upload(f, done, ex), false);
        }

        private sealed class Processed
        {
            public Color32[] px;
            public int w, h;
            public HeadMeasure head;
        }

        private static void Upload(PictureFile f, Processed p, Exception ex)
        {
            if (f.gone)
            {
                Forgotten(f);
                return;
            }
            if (ex != null || p == null)
            {
                Fail(f, ex?.Message ?? "nothing came of it");
                return;
            }
            loading = Math.Max(0, loading - 1);
            var tex = new Texture2D(p.w, p.h, TextureFormat.RGBA32, true)
            {
                name = "RimfolkPortrait_" + f.key, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
            };
            tex.SetPixels32(p.px);
            tex.Apply(true, true);
            f.texture = tex;
            f.width = p.w;
            f.height = p.h;
            f.head = p.head;
            f.state = PictureFile.State.Ready;
            ready.Add(f);
            pixels += (long)p.w * p.h;
            Loaded++;
            Trim();
        }

        /// <summary>The least lately shown pictures let go (their textures destroyed) while the cache is over its count or pixels; never one shown this frame or the last.</summary>
        private static void Trim()
        {
            while (ready.Count > MostPictures || pixels > MostPixels)
            {
                PictureFile oldest = null;
                foreach (var f in ready)
                    if (Time.frameCount - f.lastUsed > 1 && (oldest == null || f.lastUsed < oldest.lastUsed))
                        oldest = f;
                if (oldest == null) return;
                Drop(oldest);
            }
        }

        private static void Drop(PictureFile f)
        {
            if (ready.Remove(f)) pixels -= (long)f.width * f.height;
            if (f.texture != null) UnityEngine.Object.Destroy(f.texture);
            f.texture = null;
            f.state = PictureFile.State.None;
        }

        /// <summary>A picture deleted for good while it was being read: nothing is made of it.</summary>
        private static void Forgotten(PictureFile f)
        {
            loading = Math.Max(0, loading - 1);
            f.state = PictureFile.State.None;
        }

        /// <summary>
        /// A picture deleted for good (UI/Faces/PortraitDelete.cs): its texture let go now, or, while it is still being read,
        /// as soon as the reading is done; it is never loaded again.
        /// </summary>
        public static void LetGo(PictureFile f)
        {
            if (f == null) return;
            f.gone = true;
            if (f.state == PictureFile.State.Ready) Drop(f);
        }

        /// <summary>Every picture let go (the dev action, and the settings' size change).</summary>
        public static void Clear()
        {
            foreach (var f in ready.ToArray()) Drop(f);
        }

        public static int Count => ready.Count;
        public static long Pixels => pixels;

        // ---------------- Pixels ----------------

        /// <summary>Unity's pixels (rows from the foot) as RGBA bytes from the top.</summary>
        private static byte[] ToBytes(Color32[] px, int w, int h)
        {
            var b = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int from = (h - 1 - y) * w, to = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    var c = px[from + x];
                    int o = to + x * 4;
                    b[o] = c.r;
                    b[o + 1] = c.g;
                    b[o + 2] = c.b;
                    b[o + 3] = c.a;
                }
            }
            return b;
        }

        /// <summary>RGBA bytes as Unity's pixels, in the same row order.</summary>
        private static Color32[] ToColours(byte[] b)
        {
            var px = new Color32[b.Length / 4];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
            return px;
        }
    }

    /// <summary>Copies of textures read back from the graphics card, small, for the pictures Rimfolk makes from them (the game keeps its textures unreadable).</summary>
    public static class PortraitGpu
    {
        /// <summary>
        /// The texture (its part from <paramref name="offset"/>, <paramref name="scale"/> of it, as the graphics card counts
        /// from the foot) drawn at <paramref name="w"/> by <paramref name="h"/> and read back as RGBA bytes, rows from the foot; null where it fails.
        /// </summary>
        public static byte[] Read(Texture src, int w, int h, Vector2 scale, Vector2 offset)
        {
            if (src == null || w <= 0 || h <= 0) return null;
            RenderTexture rt = null;
            Texture2D read = null;
            var active = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt, scale, offset);
                RenderTexture.active = rt;
                read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                read.Apply(false);
                return read.GetRawTextureData();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading a picture back from the graphics card", e);
                return null;
            }
            finally
            {
                RenderTexture.active = active;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (read != null) UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>A texture of Rimfolk's own from RGBA bytes (rows from the foot), drawn at its own size (no mipmaps).</summary>
        public static Texture2D Make(byte[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.LoadRawTextureData(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
