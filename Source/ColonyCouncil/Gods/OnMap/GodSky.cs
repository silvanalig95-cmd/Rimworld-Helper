using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The signs Rimfolk draws itself, softly (Tools/make_god_sky.py), for gods with no body on the map.</summary>
    public enum SkySign
    {
        /// <summary>Smoulder: a vast winged shadow crossing the land at noon, a wind with it.</summary>
        Wings,
        /// <summary>Cirrus: a shadow too large to have a source, drifting over.</summary>
        Vast,
        /// <summary>Cirrus: a single star that moves the wrong way across the night.</summary>
        Star,
        /// <summary>Hush: a tall white figure at the edge of the land at night, never close when looked at.</summary>
        Figure,
        /// <summary>Undertow: a patch of dark that a light doesn't reach.</summary>
        Dark,
        /// <summary>Morel: white threads across a floor.</summary>
        Threads,
        /// <summary>Hollin: roots drawn up out of the ground into a shape taller than any tree, standing an hour and sinking.</summary>
        Rising,
        /// <summary>Selene: the moon very large and close over the colony all night, its light pooled over the land.</summary>
        Moon,
        /// <summary>Ducat: a golden beetle walking across a table.</summary>
        Beetle
    }

    /// <summary>
    /// The gods' signs drawn on the map (GodOmens, GodRising): a shadow or a star passing over (on real seconds, so it is
    /// seen at any speed), and a figure, a dark, threads or a shape of roots that stay a few hours of the game's. Soft
    /// shapes with no edge, darkened or tinted, never a picture. Nothing is saved: a load forgets a sign under way (what
    /// came of it, the remark, the mark and the memory, is kept elsewhere).
    /// </summary>
    public static class GodSky
    {
        private const string Dir = "ColonyCouncil/Gods/Sky/";

        private static Material wings, vast, figure, dark, threads, star, rising, halo;

        private static Material Mat(ref Material m, string name, Shader shader) => m ??= MaterialPool.MatFrom(Dir + name, shader);

        // Dragon's Descent's flight sprites (read by path, only when the mod is loaded): the noon shadow takes their shape,
        // wings beating, head first (they face north), drawn dark, huge and faint; Rimfolk's own winged shadow otherwise.
        private const string Flight = "Things/Pawn/Animal/Flight/Flying";
        private static Material flyA, flyB;
        private static bool? dragons;

        private static bool Dragons
        {
            get
            {
                if (dragons == null)
                {
                    try
                    {
                        dragons = ContentFinder<Texture2D>.Get(Flight + "1", false) != null && ContentFinder<Texture2D>.Get(Flight + "2", false) != null;
                    }
                    catch
                    {
                        dragons = false;
                    }
                }
                return dragons.Value;
            }
        }

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        internal sealed class Sign
        {
            public SkySign kind;
            public Vector3 from, to;
            public float start, seconds;
            public int sinceTick, untilTick;
            public IntVec3 cell;
            public float size;
            public float lastPuff;
        }

        /// <summary>A shadow or a star passing over <paramref name="map"/> from one edge to the other, through near the middle. The cell it crosses nearest the middle.</summary>
        public static IntVec3 Pass(Map map, SkySign kind)
        {
            var d = GodSkyDrawer.Of(map);
            if (d == null) return IntVec3.Invalid;
            float angle = Rand.Range(0f, 360f);
            if (kind == SkySign.Star) angle = Rand.Range(-25f, 25f); // west to east: the wrong way
            var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            var mid = map.Center.ToVector3Shifted() + new Vector3(Rand.Range(-0.2f, 0.2f) * map.Size.x, 0f, Rand.Range(-0.2f, 0.2f) * map.Size.z);
            float reach = Mathf.Max(map.Size.x, map.Size.z) * 0.85f;
            float size = kind == SkySign.Wings ? 70f : kind == SkySign.Vast ? 150f : 2.4f;
            d.signs.Add(new Sign
            {
                kind = kind, from = mid - dir * (reach + size), to = mid + dir * (reach + size), start = Time.realtimeSinceStartup,
                seconds = kind == SkySign.Wings ? 16f : kind == SkySign.Vast ? 34f : 40f, size = size
            });
            return mid.ToIntVec3().ClampInsideMap(map);
        }

        /// <summary>A sign that stays at <paramref name="cell"/> for <paramref name="ticks"/> of the game's (a figure, a dark, threads).</summary>
        public static void Stay(Map map, SkySign kind, IntVec3 cell, int ticks, float size)
        {
            var d = GodSkyDrawer.Of(map);
            if (d == null || !cell.IsValid) return;
            int now = Find.TickManager.TicksGame;
            d.signs.Add(new Sign { kind = kind, cell = cell, sinceTick = now, untilTick = now + ticks, size = size });
        }

        /// <summary>A small thing walking from <paramref name="from"/> to <paramref name="to"/> over <paramref name="seconds"/> real seconds (Ducat's beetle).</summary>
        public static void Crawl(Map map, Vector3 from, Vector3 to, float seconds)
        {
            var d = GodSkyDrawer.Of(map);
            if (d == null) return;
            d.signs.Add(new Sign { kind = SkySign.Beetle, from = from, to = to, start = Time.realtimeSinceStartup, seconds = seconds, size = 0.45f });
        }

        /// <summary>A sign of this kind is on the map now (one at a time of each).</summary>
        public static bool On(Map map, SkySign kind)
        {
            var d = GodSkyDrawer.Of(map);
            if (d == null) return false;
            foreach (var s in d.signs)
                if (s.kind == kind)
                    return true;
            return false;
        }

        // ---------------- Drawing ----------------

        internal static void Draw(Map map, List<Sign> signs)
        {
            float now = Time.realtimeSinceStartup;
            int tick = Find.TickManager.TicksGame;
            float sky = map.skyManager.CurSkyGlow;
            for (int i = signs.Count - 1; i >= 0; i--)
            {
                var s = signs[i];
                bool passing = s.kind == SkySign.Wings || s.kind == SkySign.Vast || s.kind == SkySign.Star || s.kind == SkySign.Beetle;
                if (passing ? now - s.start > s.seconds : tick > s.untilTick)
                {
                    signs.RemoveAt(i);
                    continue;
                }
                if (Find.CurrentMap != map) continue;
                try
                {
                    if (passing) DrawPassing(map, s, now, sky);
                    else DrawStaying(map, s, tick, sky);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a sign on the sky", e);
                    signs.RemoveAt(i);
                }
            }
        }

        private static void DrawPassing(Map map, Sign s, float now, float sky)
        {
            float t = Mathf.Clamp01((now - s.start) / s.seconds);
            var at = Vector3.Lerp(s.from, s.to, t);
            var dir = (s.to - s.from).normalized;
            float angle = -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            // Faint by day as a shadow is, stronger toward the middle of its crossing; a star only in the dark.
            float fade = Mathf.Sin(t * Mathf.PI);
            switch (s.kind)
            {
                case SkySign.Wings:
                    at.y = AltitudeLayer.Weather.AltitudeFor();
                    if (Dragons)
                    {
                        bool down = (int)(now * 1.6f) % 2 == 0;
                        var mat = down ? flyA ??= MaterialPool.MatFrom(Flight + "1", ShaderDatabase.Transparent) : flyB ??= MaterialPool.MatFrom(Flight + "2", ShaderDatabase.Transparent);
                        Quad(mat, at, angle + 90f, s.size, s.size, Color.black, 0.3f * sky * fade);
                    }
                    else Quad(Mat(ref wings, "Wings", ShaderDatabase.Transparent), at, angle, s.size, s.size, Color.black, 0.32f * sky * fade);
                    // The wind it brings: dust and leaves blown along under it.
                    if (now - s.lastPuff > 0.12f && at.ToIntVec3().InBounds(map))
                    {
                        s.lastPuff = now;
                        for (int k = 0; k < 3; k++)
                        {
                            var loc = at + new Vector3(Rand.Range(-0.3f, 0.3f) * s.size, 0f, Rand.Range(-0.3f, 0.3f) * s.size);
                            if (loc.ToIntVec3().InBounds(map) && !loc.ToIntVec3().Roofed(map)) FleckMaker.ThrowDustPuffThick(loc, map, Rand.Range(1.2f, 2.4f), new Color(0.78f, 0.74f, 0.62f, 0.7f));
                        }
                    }
                    break;
                case SkySign.Vast:
                    at.y = AltitudeLayer.Weather.AltitudeFor();
                    Quad(Mat(ref vast, "Vast", ShaderDatabase.Transparent), at, angle, s.size, s.size * 0.75f, new Color(0.08f, 0.1f, 0.16f), 0.22f * sky * fade);
                    break;
                case SkySign.Beetle:
                {
                    // A gold glint walking, with a small stop now and then, as a beetle does.
                    float step = Mathf.Sin(now * 9f) * 0.5f + 0.5f;
                    at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    Quad(Mat(ref halo, "../Aura/Halo", ShaderDatabase.MoteGlow), at, 0f, s.size, s.size, new Color(1f, 0.82f, 0.3f), (0.55f + 0.35f * step) * Mathf.Min(1f, fade * 4f));
                    break;
                }
                case SkySign.Star:
                    if (sky > 0.45f) return;
                    at.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                    float twinkle = 0.75f + 0.25f * Mathf.Sin(now * 7.3f);
                    Quad(Mat(ref star, "../Aura/Halo", ShaderDatabase.MoteGlow), at, 0f, s.size, s.size, new Color(0.85f, 0.92f, 1f), (1f - sky * 1.8f) * twinkle * Mathf.Min(1f, fade * 3f));
                    break;
            }
        }

        private static void DrawStaying(Map map, Sign s, int tick, float sky)
        {
            var at = s.cell.ToVector3Shifted();
            switch (s.kind)
            {
                case SkySign.Figure:
                {
                    if (sky > 0.5f) return;
                    // Never close when anyone looks straight at it: fainter the nearer the view's middle comes.
                    float near = (Find.CameraDriver.MapPosition.ToVector3Shifted() - at).magnitude;
                    float alpha = Mathf.Clamp01((near - 12f) / 30f) * (1f - sky * 1.6f) * 0.55f;
                    at.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                    Quad(Mat(ref figure, "Figure", ShaderDatabase.MoteGlow), at + new Vector3(0f, 0f, s.size * 0.4f), 0f, s.size * 0.3f, s.size * 1.2f, new Color(0.88f, 0.94f, 1f), alpha);
                    break;
                }
                case SkySign.Dark:
                    at.y = AltitudeLayer.VisEffects.AltitudeFor();
                    Quad(Mat(ref dark, "Dark", ShaderDatabase.Transparent), at, 0f, s.size, s.size, Color.black, 0.75f);
                    break;
                case SkySign.Threads:
                    at.y = AltitudeLayer.Filth.AltitudeFor();
                    Quad(Mat(ref threads, "Threads", ShaderDatabase.Transparent), at, s.cell.x * 37 % 360, s.size, s.size, new Color(0.95f, 0.94f, 0.88f), 0.75f);
                    break;
                case SkySign.Moon:
                {
                    // The moon's light pooled over the colony, pale, at night only.
                    if (sky > 0.5f) return;
                    at.y = AltitudeLayer.Weather.AltitudeFor();
                    Quad(Mat(ref halo, "../Aura/Halo", ShaderDatabase.MoteGlow), at, 0f, s.size, s.size, new Color(0.82f, 0.88f, 1f), 0.22f * (1f - sky * 1.6f));
                    break;
                }
                case SkySign.Rising:
                {
                    // Drawn up out of the ground over the first few minutes of the hour, standing, sinking back at the end;
                    // its feet on the cell, taller than any tree, bark-dark with a little moss in the colour.
                    float span = Mathf.Max(1, s.untilTick - s.sinceTick);
                    float t = (tick - s.sinceTick) / span;
                    float up = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1f - t) / 0.12f);
                    up = up * up * (3f - 2f * up);
                    if (up <= 0.01f) return;
                    float h = s.size * up, w = s.size * 0.5f;
                    at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    Quad(Mat(ref rising, "Rising", ShaderDatabase.Transparent), at + new Vector3(0f, 0f, h * 0.45f), 0f, w, h, new Color(0.2f, 0.19f, 0.12f), 0.9f * Mathf.Min(1f, up * 1.5f));
                    break;
                }
            }
        }

        private static void Quad(Material mat, Vector3 at, float angle, float w, float h, Color color, float alpha)
        {
            if (alpha <= 0.005f || mat == null) return;
            Block.SetColor(ShaderPropertyIDs.Color, new Color(color.r, color.g, color.b, alpha));
            var m = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(w, 1f, h));
            Graphics.DrawMesh(MeshPool.plane10, m, mat, 0, null, 0, Block);
        }
    }

    /// <summary>Draws the gods' signs on the map being looked at (GodSky; nothing saved, nothing at all with none).</summary>
    public class GodSkyDrawer : MapComponent
    {
        internal readonly List<GodSky.Sign> signs = new List<GodSky.Sign>();

        public GodSkyDrawer(Map map) : base(map)
        {
        }

        internal static GodSkyDrawer Of(Map map) => map?.GetComponent<GodSkyDrawer>();

        public override void MapComponentUpdate()
        {
            if (signs.Count > 0) GodSky.Draw(map, signs);
        }
    }
}
