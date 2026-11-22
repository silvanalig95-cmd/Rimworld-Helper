using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Portraits, phase 6: the large portrait alive and with depth (docs: DESIGN.md, "Portraits, phase 6"; the player, 22
    /// November 2026: "perhaps we can make the pawns, even though their image is generated, a little bit more alive", and
    /// "if I, for example, hover my mouse, it kind of centers towards the mouse in a way, like this kind of fake 3D
    /// effect"). Only the large portrait at the inspect pane; the same pictures elsewhere stay still.
    /// <list type="bullet">
    /// <item>The mouse over it (<see cref="Hover"/>), eased each frame toward it and back to the middle when it leaves
    /// (<see cref="EaseDepth"/>); the layers made in <c>FaceEngineStage.cs</c> are only moved by it.</item>
    /// <item>The face: what the person goes through read from the game every second or two (asleep, their wounds and
    /// pain, their memories of a death or good news, their mood; no new hook), the face chosen and held by
    /// <see cref="PortraitLifeMath.Want"/> and <see cref="PortraitLifeMath.Step"/>, a blink now and then; each expression
    /// made as the person was when it is first needed, and faded over the face before.</item>
    /// <item>The backdrop alive: outdoors under the open sky the map's weather (rain, snow, fog or dust, by its def) falls
    /// lightly over the backdrop, behind the person; indoors a fire's warm light flickers over its lower part where the
    /// backdrop shows a hearth or a fire or one is lit in the room. Drawn with three small tiles made once by code and the
    /// portraits' soft light, a few draws a frame.</item>
    /// </list>
    /// All of it on the movement clock (held while the game is paused) but the mouse, which follows the hand at any time.
    /// </summary>
    public static partial class FaceEngine
    {
        // ---------------- Depth that follows the mouse ----------------

        private static bool hoverOn;
        private static float hoverX, hoverY;
        private static int hoverFrame = -10;

        /// <summary>The eased mouse over the large portrait (-1 to 1 each way, 0 the middle), moved once a frame.</summary>
        private static float depthX, depthY;
        private static int depthFrame = -10, depthPawn = -1;

        /// <summary>The large portrait tells where the mouse is over it this frame (-1 to 1 each way), or that it isn't.</summary>
        public static void Hover(bool over, float x, float y)
        {
            hoverOn = over;
            hoverX = x;
            hoverY = y;
            hoverFrame = Time.frameCount;
        }

        /// <summary>Once a frame: the picture eased toward the mouse while it is over it, back to the middle when it leaves; a new person starts in the middle.</summary>
        private static void EaseDepth(int pawn)
        {
            if (depthFrame == Time.frameCount) return;
            float dt = Time.frameCount - depthFrame > 2 ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            depthFrame = Time.frameCount;
            if (pawn != depthPawn)
            {
                depthPawn = pawn;
                depthX = depthY = 0f;
            }
            bool over = hoverOn && Time.frameCount - hoverFrame <= 1;
            float time = over ? PortraitLifeMath.FollowFor : PortraitLifeMath.SettleFor;
            depthX = PortraitLifeMath.Ease(depthX, over ? hoverX : 0f, dt, time);
            depthY = PortraitLifeMath.Ease(depthY, over ? hoverY : 0f, dt, time);
        }

        // ---------------- The face ----------------

        /// <summary>What the person's picture shows this frame: the face before, the face now, and how far the one now has faded in over it.</summary>
        private struct FaceDraw
        {
            public string before, now;
            public float faded;
        }

        private sealed class FaceLife
        {
            public readonly PortraitFaceState state = new PortraitFaceState();
            public PortraitFeeling feeling = PortraitFeeling.Calm;
            public float feltUntil = -1f, stepUntil = -1f;
            public string want = "";
        }

        private static readonly Dictionary<int, FaceLife> lives = new Dictionary<int, FaceLife>();

        /// <summary>How often what the person goes through is read from the game, and the face wanted looked at again, in real seconds.</summary>
        private const float FeelEvery = 1.5f, StepEvery = 0.25f;

        private static bool FacesOn => S?.portraitFaces ?? true;

        private static readonly FaceDraw Plain = new FaceDraw { before = "", now = "", faded = 1f };

        /// <summary>
        /// The face the person's picture shows now, where it has expressions painted and "Blinking and expressions" is on: the
        /// face wanted by what they go through, held and faded (<see cref="PortraitLifeMath.Step"/>), a blink over it now and
        /// then; the expressions it needs made one at a time as they are first needed. Else the plain picture.
        /// </summary>
        private static FaceDraw FacesNow(Pawn p, Staged s)
        {
            if (!FacesOn || s.picture == GameDrawing) return Plain;
            try
            {
                var has = PortraitLibrary.Source.ExpressionsOf(s.picture);
                if (has == null) return Plain;
                float now = Time.realtimeSinceStartup;
                if (!lives.TryGetValue(p.thingIDNumber, out var life))
                {
                    if (lives.Count > 16) lives.Clear();
                    life = new FaceLife();
                    lives[p.thingIDNumber] = life;
                }
                if (now >= life.feltUntil)
                {
                    life.feltUntil = now + FeelEvery;
                    life.feeling = FeelingOf(p);
                }
                var st = life.state;
                // The face wanted, and moved on where the hold allows, a few times a second (the first time at once).
                if (now >= life.stepUntil || !st.started)
                {
                    life.stepUntil = now + StepEvery;
                    life.want = PortraitLifeMath.Want(life.feeling, w => has.ContainsKey(w) && !s.facesFailed.Contains(w), st.shown, out var why);
                    PortraitLifeMath.Step(st, life.want, why, now);
                }
                // A face not made yet: its fade waits for it, the face before (or the plain picture) shown meanwhile.
                if (!Ready(s, st.shown))
                {
                    st.fadeFrom = now;
                    if (!Ready(s, st.from)) st.from = "";
                }
                if (staging == null) MakeFace(s, has, st.shown, st.from, life.want);
                float faded = PortraitLifeMath.Faded(st, now);
                // A blink now and then over a face with its eyes open (the blinks are counted on whatever shows, so they keep their pace).
                bool blink = has.ContainsKey("blink") && PortraitLifeMath.Blinking(st, Clock, p.thingIDNumber) && PortraitLifeMath.MayBlink(st, faded) && Ready(s, "blink");
                if (blink) return new FaceDraw { before = "blink", now = "blink", faded = 1f };
                return new FaceDraw { before = st.from, now = st.shown, faded = faded };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait: the face it shows", e);
                return Plain;
            }
        }

        /// <summary>The plain picture, or an expression already made for this picture.</summary>
        private static bool Ready(Staged s, string word) => string.IsNullOrEmpty(word) || s.faces.ContainsKey(word);

        /// <summary>
        /// The next expression this picture needs made, one at a time with the pictures: the face shown, the blink, the face
        /// it fades from, the face wanted next; each picture asked of the store (loaded when first asked, kept while shown).
        /// </summary>
        private static void MakeFace(Staged s, IReadOnlyDictionary<string, PictureFile> has, string shown, string from, string want)
        {
            if (!TryFace(s, has, shown) && !TryFace(s, has, "blink") && !TryFace(s, has, from)) TryFace(s, has, want);
        }

        /// <summary>Starts making one expression where it is painted, not made yet and its picture is loaded; true where it was started.</summary>
        private static bool TryFace(Staged s, IReadOnlyDictionary<string, PictureFile> has, string word)
        {
            if (Ready(s, word) || s.facesFailed.Contains(word) || !has.TryGetValue(word, out var file)) return false;
            var tex = PortraitStore.Get(file);
            if (tex == null)
            {
                if (PortraitStore.Failed(file)) s.facesFailed.Add(word);
                return false;
            }
            StartFace(s, word, tex);
            return true;
        }

        /// <summary>
        /// What the person goes through, from the game's own health and memories: asleep or unconscious; their pain, and the
        /// game ticks since their newest wound (an injury not yet a scar, a part freshly lost); since the newest memory of a
        /// death close to them (<see cref="PortraitLifeMath.IsGrief"/>) and of good news; their mood and minor break line.
        /// </summary>
        private static PortraitFeeling FeelingOf(Pawn p)
        {
            var f = PortraitFeeling.Calm;
            try
            {
                f.asleep = !p.Awake();
                var hs = p.health?.hediffSet;
                if (hs != null)
                {
                    f.pain = hs.PainTotal;
                    foreach (var h in hs.hediffs)
                    {
                        bool wound = h is Hediff_Injury && !h.IsPermanent() || h is Hediff_MissingPart mp && mp.IsFresh;
                        if (wound && (f.hurtAgo < 0 || h.ageTicks < f.hurtAgo)) f.hurtAgo = h.ageTicks;
                    }
                }
                var mood = p.needs?.mood;
                if (mood != null)
                {
                    f.mood = mood.CurLevel;
                    f.minor = p.mindState?.mentalBreaker?.BreakThresholdMinor ?? 0.35f;
                    var list = mood.thoughts?.memories?.Memories;
                    if (list != null)
                        for (int i = 0; i < list.Count; i++)
                        {
                            var m = list[i];
                            if (m?.def == null || m.age > PortraitLifeMath.GriefFor) continue;
                            float off;
                            try
                            {
                                off = m.MoodOffset();
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            if (PortraitLifeMath.IsGrief(m.def.defName, off))
                            {
                                if (f.griefAgo < 0 || m.age < f.griefAgo) f.griefAgo = m.age;
                            }
                            else if (PortraitLifeMath.IsGoodNews(off) && m.age < PortraitLifeMath.GoodNewsFor && (f.goodAgo < 0 || m.age < f.goodAgo)) f.goodAgo = m.age;
                        }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait: what the person goes through", e);
            }
            return f;
        }

        // ---------------- The backdrop alive ----------------

        private static bool BackdropAliveOn => S?.portraitBackdropAlive ?? true;

        /// <summary>With the backdrop looked at (every two seconds): the weather over it outdoors under the open sky, a fire's light indoors.</summary>
        private static void LookAlive(Pawn p, BackdropWhere where, StageLook look)
        {
            look.weather = default;
            look.hearth = false;
            if (!BackdropAliveOn) return;
            if (where.place == BackdropPlace.Outdoors && where.open && where.group != "cave")
            {
                var wd = p.MapHeld?.weatherManager?.curWeather;
                if (wd != null) look.weather = PortraitLifeMath.WeatherOf(wd.defName, wd.rainRate, wd.snowRate, wd.overlayClasses?.Select(t => t?.Name));
            }
            else if (where.place == BackdropPlace.Room) look.hearth = PortraitLifeMath.Hearth(true, look.backRecord?.contents, where.fireLit);
        }

        private static float aliveRain, aliveSnow, aliveHaze, aliveHearth;
        private static int aliveFrame = -10, alivePawn = -1;

        /// <summary>Once a frame: the weather and the fire's light eased toward what they are now over a few seconds, so a change never pops in.</summary>
        private static void EaseAlive(StageLook look)
        {
            if (aliveFrame == Time.frameCount) return;
            float dt = Time.frameCount - aliveFrame > 2 ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            aliveFrame = Time.frameCount;
            if (depthPawn != alivePawn)
            {
                alivePawn = depthPawn;
                aliveRain = look.weather.rain;
                aliveSnow = look.weather.snow;
                aliveHaze = look.weather.haze;
                aliveHearth = look.hearth ? 1f : 0f;
                return;
            }
            aliveRain = PortraitLifeMath.Ease(aliveRain, look.weather.rain, dt, 2f);
            aliveSnow = PortraitLifeMath.Ease(aliveSnow, look.weather.snow, dt, 2f);
            aliveHaze = PortraitLifeMath.Ease(aliveHaze, look.weather.haze, dt, 3f);
            aliveHearth = PortraitLifeMath.Ease(aliveHearth, look.hearth ? 1f : 0f, dt, 1.5f);
        }

        /// <summary>
        /// The backdrop alive over the backdrop's layers, behind the person (inside the portrait's group): fog or dust in two
        /// slow drifting layers, rain in two falling ones, snow in two drifting down, all fainter in the dark; a fire's warm
        /// light flickering over the lower part. Moved a little with the mouse, as a layer between the wall and the person.
        /// </summary>
        private static void DrawBackdropAlive(Rect local, StageLook look, float depth)
        {
            if (!BackdropAliveOn || look == null) return;
            EaseAlive(look);
            if (aliveRain < 0.01f && aliveSnow < 0.01f && aliveHaze < 0.01f && aliveHearth < 0.01f) return;
            MakeWeatherTiles();
            float t = Clock, w = local.width;
            PortraitLifeMath.Shift(0.3f, depthX, depthY, depth, w, out float dx, out float dy);
            var wt = look.weather;
            int step = look.step;
            if (aliveHaze >= 0.01f && FogTile != null)
            {
                float a = PortraitLifeMath.WeatherAlpha(aliveHaze, step);
                GUI.color = Tinted(wt.hr, wt.hg, wt.hb, 0.5f * a);
                Tile(local, FogTile, 1.6f * w, 1.6f * w, t * 0.012f, t * 0.003f, dx, dy);
                GUI.color = Tinted(wt.hr, wt.hg, wt.hb, 0.32f * a);
                Tile(local, FogTile, 2.4f * w, 2.4f * w, 0.37f - t * 0.008f, 0.21f, dx, dy);
            }
            if (aliveRain >= 0.01f && RainTile != null)
            {
                float a = PortraitLifeMath.WeatherAlpha(aliveRain, step);
                // Along the streaks' slant: the tile is twice as tall as wide and its streaks lean 0.18 across for each down.
                GUI.color = Tinted(wt.rr, wt.rg, wt.rb, 0.35f * a);
                Tile(local, RainTile, 0.5f * w, 1f * w, 0.36f * t * 1.3f, t * 1.3f, dx, dy);
                GUI.color = Tinted(wt.rr, wt.rg, wt.rb, 0.55f * a);
                Tile(local, RainTile, 0.8f * w, 1.6f * w, 0.5f + 0.36f * t * 1.1f, 0.3f + t * 1.1f, dx, dy);
            }
            if (aliveSnow >= 0.01f && SnowTile != null)
            {
                float a = PortraitLifeMath.WeatherAlpha(aliveSnow, step);
                GUI.color = Tinted(255, 255, 255, 0.55f * a);
                Tile(local, SnowTile, 0.5f * w, 0.5f * w, 0.04f * Mathf.Sin(0.7f * t), t * 0.1f, dx, dy);
                GUI.color = Tinted(255, 255, 255, 0.8f * a);
                Tile(local, SnowTile, 0.9f * w, 0.9f * w, 0.5f + 0.06f * Mathf.Sin(0.5f * t + 1f), 0.4f + t * 0.16f, dx, dy);
            }
            if (aliveHearth >= 0.01f && GlowTex != null)
            {
                float f = PortraitLifeMath.Flicker(t, look.seed);
                GUI.color = Tinted(255, 158, 77, PortraitLifeMath.HearthAlpha(f, step) * aliveHearth);
                float wobble = (f - 0.5f) * 0.02f * w;
                GUI.DrawTexture(new Rect(local.x - 0.25f * w + wobble + dx, local.y + 0.25f * local.height + dy, 1.5f * w, 1.25f * local.height), GlowTex);
            }
            GUI.color = Color.white;
        }

        /// <summary>The colour of what is drawn over the backdrop (the rain's, the haze's, the fire's light; part of the picture, not of the interface) at a strength.</summary>
        private static Color32 Tinted(byte r, byte g, byte b, float a) => new Color32(r, g, b, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));

        /// <summary>A tile repeated over the portrait at <paramref name="tileW"/> by <paramref name="tileH"/>, scrolled by (<paramref name="u"/>, <paramref name="v"/>) tiles (more <paramref name="v"/> moves it down) and moved by the mouse.</summary>
        private static void Tile(Rect r, Texture2D tex, float tileW, float tileH, float u, float v, float dx, float dy)
        {
            if (tileW <= 0f || tileH <= 0f) return;
            u = Mathf.Repeat(u - dx / tileW, 1f);
            v = Mathf.Repeat(v + dy / tileH, 1f);
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(u, v, r.width / tileW, r.height / tileH));
        }

        // The three tiles of the weather, made once by code: thin falling streaks, soft flakes, a soft cloud. White, coloured when drawn.
        private static Texture2D RainTile, SnowTile, FogTile;
        private static bool tilesTried;

        private static void MakeWeatherTiles()
        {
            if (tilesTried) return;
            tilesTried = true;
            try
            {
                RainTile = MakeTile("RimfolkPortraitRain", 128, 256, DrawRain);
                SnowTile = MakeTile("RimfolkPortraitSnow", 128, 128, DrawSnow);
                FogTile = MakeTile("RimfolkPortraitFog", 64, 64, DrawFog);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the large portrait: the weather's tiles", e);
            }
        }

        private static Texture2D MakeTile(string name, int w, int h, Action<float[], int, int, System.Random> draw)
        {
            var a = new float[w * h];
            draw(a, w, h, new System.Random(name.Length * 7919 + w));
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a[i] * 255f), 0, 255));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Thin streaks a pixel wide, 14 to 34 long, leaning 0.18 across for each pixel down, fading at both ends; wrapped at the tile's edges.</summary>
        private static void DrawRain(float[] a, int w, int h, System.Random rnd)
        {
            for (int i = 0; i < 26; i++)
            {
                float x0 = (float)rnd.NextDouble() * w, y0 = (float)rnd.NextDouble() * h, len = 14f + (float)rnd.NextDouble() * 20f, strength = 0.45f + (float)rnd.NextDouble() * 0.5f;
                for (int k = 0; k < (int)len; k++)
                {
                    float s = k / len, fade = Mathf.Sin(s * Mathf.PI) * strength;
                    float x = x0 - 0.18f * k;
                    int y = ((int)(y0 - k) % h + h) % h, xi = Mathf.FloorToInt(x);
                    float part = x - xi;
                    Put(a, w, h, xi, y, fade * (1f - part));
                    Put(a, w, h, xi + 1, y, fade * part);
                }
            }
        }

        /// <summary>Soft round flakes, one to two and a half pixels across.</summary>
        private static void DrawSnow(float[] a, int w, int h, System.Random rnd)
        {
            for (int i = 0; i < 34; i++)
            {
                float cx = (float)rnd.NextDouble() * w, cy = (float)rnd.NextDouble() * h, rad = 1f + (float)rnd.NextDouble() * 1.6f, strength = 0.6f + (float)rnd.NextDouble() * 0.4f;
                int r = Mathf.CeilToInt(rad) + 1;
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - (cx - Mathf.Floor(cx))) * (x + 0.5f - (cx - Mathf.Floor(cx))) + (y + 0.5f - (cy - Mathf.Floor(cy))) * (y + 0.5f - (cy - Mathf.Floor(cy))));
                    float v = Mathf.Clamp01(1f - Mathf.SmoothStep(rad * 0.4f, rad, d)) * strength;
                    Put(a, w, h, Mathf.FloorToInt(cx) + x, Mathf.FloorToInt(cy) + y, v);
                }
            }
        }

        /// <summary>A soft cloud that repeats without a seam: two layers of smooth noise on a grid that wraps.</summary>
        private static void DrawFog(float[] a, int w, int h, System.Random rnd)
        {
            float[] Grid(int n)
            {
                var g = new float[n * n];
                for (int i = 0; i < g.Length; i++) g[i] = (float)rnd.NextDouble();
                return g;
            }
            float Sample(float[] g, int n, float x, float y)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = Mathf.SmoothStep(0f, 1f, x - x0), fy = Mathf.SmoothStep(0f, 1f, y - y0);
                float G(int i, int j) => g[((j % n + n) % n) * n + (i % n + n) % n];
                return Mathf.Lerp(Mathf.Lerp(G(x0, y0), G(x0 + 1, y0), fx), Mathf.Lerp(G(x0, y0 + 1), G(x0 + 1, y0 + 1), fx), fy);
            }
            var coarse = Grid(4);
            var fine = Grid(8);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0.65f * Sample(coarse, 4, x * 4f / w, y * 4f / h) + 0.35f * Sample(fine, 8, x * 8f / w, y * 8f / h);
                a[y * w + x] = Mathf.Clamp01((v - 0.25f) / 0.65f);
            }
        }

        private static void Put(float[] a, int w, int h, int x, int y, float v)
        {
            x = (x % w + w) % w;
            y = (y % h + h) % h;
            int i = y * w + x;
            if (v > a[i]) a[i] = v;
        }

        /// <summary>A new game or the settings changed: the faces and the eased movement start again.</summary>
        private static void ClearAlive()
        {
            lives.Clear();
            depthPawn = alivePawn = -1;
            depthX = depthY = 0f;
        }
    }
}
