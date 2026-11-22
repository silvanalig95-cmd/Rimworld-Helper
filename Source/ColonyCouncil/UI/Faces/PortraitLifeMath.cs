using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The setting "Depth that follows the mouse" on the large portrait: off, subtle (the default) or stronger.</summary>
    public enum PortraitDepthMode
    {
        Off,
        Subtle,
        Stronger
    }

    /// <summary>Why a face shows on the large portrait: nothing in particular, the mood, news (a death, good news), pain, or sleep.</summary>
    public enum PortraitFaceWhy
    {
        Plain,
        Mood,
        News,
        Pain,
        Sleep
    }

    /// <summary>
    /// What the large portrait's person is going through, read from the game every second or two
    /// (<see cref="PortraitLifeMath.Want"/>): asleep or unconscious, their pain, the game ticks since their newest wound, since
    /// the news of a death close to them and since a piece of good news (-1 for none), their mood and their minor break line
    /// (a mood below 0: they have none).
    /// </summary>
    public struct PortraitFeeling
    {
        public bool asleep;
        public float pain;
        public int hurtAgo, griefAgo, goodAgo;
        public float mood, minor;

        public static PortraitFeeling Calm => new PortraitFeeling { hurtAgo = -1, griefAgo = -1, goodAgo = -1, mood = -1f, minor = 0.35f };
    }

    /// <summary>One slice of a backdrop by its depth: its pixels (RGBA, rows as the backdrop's), where it sits in the backdrop, and how near it is (0 the far wall, 1 the person).</summary>
    public sealed class DepthSlice
    {
        public byte[] px;
        public int x, y, w, h;
        public float z;
    }

    /// <summary>The weather drawn over a backdrop outdoors: how much rain, snow and haze (0 to 1 each), and the rain's and the haze's colours.</summary>
    public struct PortraitWeather
    {
        public float rain, snow, haze;
        public byte rr, rg, rb, hr, hg, hb;

        public bool Any => rain > 0f || snow > 0f || haze > 0f;
    }

    /// <summary>The large portrait's life for one person: the face shown and the one it fades from, why and since when, and the blinks.</summary>
    public sealed class PortraitFaceState
    {
        /// <summary>The expression shown ("" the plain picture) and the one it fades from.</summary>
        public string shown = "", from = "";
        public PortraitFaceWhy why;
        /// <summary>Real seconds: when the face shown began, and when its fade began.</summary>
        public float since, fadeFrom = float.NegativeInfinity;
        public bool started;
        /// <summary>On the movement clock (held while the game is paused): the next blink, the end of the one now, how many so far, and whether the last gap was a quick second blink.</summary>
        public float nextBlink = -1f, blinkEnd = -1f;
        public int blinks;
        public bool lastQuick;
    }

    /// <summary>
    /// Portraits, phase 6: the large portrait alive and with depth (docs: DESIGN.md, "Portraits, phase 6"; the player, 22
    /// November 2026: "to make it perhaps a little bit more alive", and "if I, for example, hover my mouse, it kind of centers
    /// towards the mouse in a way, like this kind of fake 3D effect"), without game types (Tools/Tests try it):
    /// <list type="bullet">
    /// <item>Depth that follows the mouse: each layer moves by how near it is, the far wall of the backdrop against the
    /// mouse, the nearer slices of the backdrop less, the person's shadow on the wall a little with it, the person most with
    /// it (<see cref="Shift"/>); the backdrop made a little larger than the portrait so no edge shows (<see cref="Margin"/>);
    /// the movement eased toward the mouse and back to the middle when it leaves (<see cref="Ease"/>).</item>
    /// <item>A backdrop with a depth map cut into a few slices by depth once, when the picture is made (<see cref="Slices"/>).</item>
    /// <item>Alive: which of a picture's expressions shows (<see cref="Want"/>, <see cref="Step"/>), held so it never
    /// flickers, and a blink now and then (<see cref="Blinking"/>).</item>
    /// <item>The backdrop alive: the weather outdoors (<see cref="WeatherOf"/>) and a fire's flicker indoors
    /// (<see cref="Hearth"/>, <see cref="Flicker"/>).</item>
    /// </list>
    /// </summary>
    public static class PortraitLifeMath
    {
        // ---------------- Depth that follows the mouse ----------------

        /// <summary>The setting's strength: subtle a little over half the lead's mock-up, stronger the mock-up itself.</summary>
        public static float StrengthOf(PortraitDepthMode m) => m == PortraitDepthMode.Subtle ? 0.6f : m == PortraitDepthMode.Stronger ? 1f : 0f;

        /// <summary>
        /// How far the far wall moves against the mouse and the person with it at full strength, as shares of the portrait's
        /// width, across and down (the lead's mock-up at 288 pixels wide: the backdrop 7 and 5 pixels, the person 5 and 2).
        /// </summary>
        public const float WallX = 0.024f, WallY = 0.017f, PersonX = 0.017f, PersonY = 0.007f;

        /// <summary>How near the shadow on the wall is (two thirds of the way from the wall to the person, so it moves less than the person), and the nearest slice of a backdrop.</summary>
        public const float ShadowZ = 0.65f, NearestSliceZ = 0.4f;

        /// <summary>How quickly the picture follows the mouse, and settles back to the middle when it leaves, in seconds (about two thirds of the way in that time).</summary>
        public const float FollowFor = 0.18f, SettleFor = 0.45f;

        /// <summary>The mouse over the portrait (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>, <paramref name="h"/>) from -1 to 1 each way, the middle 0, held at the edges.</summary>
        public static void Pointer(float mouseX, float mouseY, float x, float y, float w, float h, out float px, out float py)
        {
            px = w > 0f ? Clamp((mouseX - (x + w / 2f)) / (w / 2f), -1f, 1f) : 0f;
            py = h > 0f ? Clamp((mouseY - (y + h / 2f)) / (h / 2f), -1f, 1f) : 0f;
        }

        /// <summary>One step of easing toward <paramref name="target"/> over <paramref name="dt"/> seconds, about two thirds of the way in <paramref name="time"/>; settles on the target when within a hair of it.</summary>
        public static float Ease(float now, float target, float dt, float time)
        {
            if (dt <= 0f || float.IsNaN(dt)) return now;
            if (time <= 0f) return target;
            float k = 1f - (float)Math.Exp(-dt / time);
            float v = now + (target - now) * k;
            return Math.Abs(v - target) < 0.0005f ? target : v;
        }

        /// <summary>
        /// A layer's move for the eased mouse (<paramref name="px"/>, <paramref name="py"/>), by how near it is
        /// (<paramref name="z"/>: 0 the far wall, against the mouse; 1 the person, with it), in the unit of
        /// <paramref name="width"/> (the portrait's width).
        /// </summary>
        public static void Shift(float z, float px, float py, float strength, float width, out float dx, out float dy)
        {
            float ax = -WallX + (PersonX + WallX) * z, ay = -WallY + (PersonY + WallY) * z;
            dx = ax * strength * width * px;
            dy = ay * strength * width * py;
        }

        /// <summary>How much larger than the portrait (<paramref name="w"/> pixels wide) the backdrop is made each way, in pixels, so its edge never shows: the far wall's farthest move and a pixel; none when off.</summary>
        public static void Margin(float strength, int w, out int mx, out int my)
        {
            if (strength <= 0f || w <= 0)
            {
                mx = my = 0;
                return;
            }
            mx = (int)Math.Ceiling(WallX * strength * w) + 1;
            my = (int)Math.Ceiling(WallY * strength * w) + 1;
        }

        /// <summary>How far the person's picture is carried on past its sides, as a share of its width, so moving it never opens a strip where the body meets the side.</summary>
        public static float PersonSide(float strength) => strength > 0f ? PersonX * strength + 0.004f : 0f;

        // ---------------- A backdrop in slices by its depth ----------------

        /// <summary>The most layers a backdrop is drawn in by its depth: the far wall (the backdrop itself) and up to three nearer slices.</summary>
        public const int LayersMost = 4;

        /// <summary>The least spread of depth (of 255) worth slicing: a flat wall stays one layer.</summary>
        public const int FlatBelow = 24;

        /// <summary>A depth map's pixels (RGBA) as one depth a pixel, white near: the brightest of its three colours.</summary>
        public static byte[] Grey(byte[] rgba)
        {
            if (rgba == null) return null;
            var d = new byte[rgba.Length / 4];
            for (int i = 0; i < d.Length; i++) d[i] = Math.Max(rgba[i * 4], Math.Max(rgba[i * 4 + 1], rgba[i * 4 + 2]));
            return d;
        }

        /// <summary>The depth's near and far ends in a depth map (its 2nd and 98th percentile, so a few stray pixels don't count) and how many layers it is worth: one where it is flat.</summary>
        public static int LayerCount(byte[] depth, int most, out int lo, out int hi)
        {
            lo = hi = 0;
            if (depth == null || depth.Length == 0) return 1;
            var hist = new int[256];
            foreach (var d in depth) hist[d]++;
            int n = depth.Length, cut = Math.Max(0, n * 2 / 100), sum = 0;
            for (lo = 0; lo < 255; lo++)
            {
                sum += hist[lo];
                if (sum > cut) break;
            }
            sum = 0;
            for (hi = 255; hi > 0; hi--)
            {
                sum += hist[hi];
                if (sum > cut) break;
            }
            if (most <= 1 || hi - lo < FlatBelow) return 1;
            return Math.Min(most, 1 + (hi - lo) / FlatBelow);
        }

        /// <summary>
        /// The nearer slices of a backdrop (<paramref name="w"/> by <paramref name="h"/>, the far wall being the backdrop itself),
        /// cut once from its depth map (one byte a pixel, white near, the same size and rows): slice k holds every pixel nearer
        /// than its edge, so the slices drawn far to near cover each other and no hole ever shows behind a near thing as it
        /// moves; the edge softened over a third of a step of depth and a pixel or two across, so a floor that recedes slowly
        /// blends from one slice into the next instead of tearing; each slice cut to the box of what it holds, with how near it
        /// is (up to <see cref="NearestSliceZ"/>). None where the depth is flat.
        /// </summary>
        public static List<DepthSlice> Slices(byte[] rgba, byte[] depth, int w, int h, int most)
        {
            var list = new List<DepthSlice>();
            if (rgba == null || depth == null || w <= 0 || h <= 0 || depth.Length < w * h || rgba.Length < w * h * 4) return list;
            int n = LayerCount(depth, most, out int lo, out int hi);
            if (n <= 1) return list;
            float step = (hi - lo) / (float)n, soft = Math.Max(1f, step * 0.35f);
            int blur = Math.Max(1, (int)Math.Round(Math.Min(w, h) * 0.004));
            var a = new byte[w * h];
            for (int k = 1; k < n; k++)
            {
                float edge = lo + step * k;
                for (int i = 0; i < a.Length; i++) a[i] = Byte(255f * Smooth(edge - soft, edge + soft, depth[i]));
                PortraitComposeMath.BlurAlpha(a, w, h, blur, 1);
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (a[y * w + x] < 4) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
                if (x1 < 0) continue;
                int sw = x1 - x0 + 1, sh = y1 - y0 + 1;
                var px = new byte[sw * sh * 4];
                for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    int from = (y0 + y) * w + x0 + x, to = (y * sw + x) * 4;
                    px[to] = rgba[from * 4];
                    px[to + 1] = rgba[from * 4 + 1];
                    px[to + 2] = rgba[from * 4 + 2];
                    px[to + 3] = a[from] < 4 ? (byte)0 : a[from];
                }
                list.Add(new DepthSlice { px = px, x = x0, y = y0, w = sw, h = sh, z = NearestSliceZ * k / (n - 1) });
            }
            return list;
        }

        /// <summary>
        /// The person's shadow on the wall behind them: their shape (from the picture's alpha) a quarter the size, blurred soft
        /// and dark at <paramref name="strength"/>, as RGBA in the same rows (drawn stretched to the person's size).
        /// </summary>
        public static byte[] Shadow(byte[] person, int w, int h, float strength, out int sw, out int sh)
        {
            sw = Math.Max(1, w / 4);
            sh = Math.Max(1, h / 4);
            var a = new byte[sw * sh];
            for (int y = 0; y < sh; y++)
            {
                int ya = y * h / sh, yb = Math.Max(ya + 1, (y + 1) * h / sh);
                for (int x = 0; x < sw; x++)
                {
                    int xa = x * w / sw, xb = Math.Max(xa + 1, (x + 1) * w / sw);
                    int sum = 0, count = 0;
                    for (int yy = ya; yy < yb && yy < h; yy++)
                    for (int xx = xa; xx < xb && xx < w; xx++)
                    {
                        sum += person[(yy * w + xx) * 4 + 3];
                        count++;
                    }
                    a[y * sw + x] = (byte)(count > 0 ? sum / count : 0);
                }
            }
            PortraitComposeMath.BlurAlpha(a, sw, sh, 2, 2);
            var px = new byte[sw * sh * 4];
            float s = Clamp(strength, 0f, 1f);
            for (int i = 0; i < a.Length; i++)
            {
                px[i * 4] = 12;
                px[i * 4 + 1] = 10;
                px[i * 4 + 2] = 14;
                px[i * 4 + 3] = Byte(a[i] * s);
            }
            return px;
        }

        /// <summary>How dark the shadow on the wall is.</summary>
        public const float ShadowStrength = 0.42f;

        /// <summary>Where the shadow falls behind the person: a little to the right and down, as a share of the portrait's width.</summary>
        public const float ShadowOffset = 0.01f;

        // ---------------- Alive: which face shows ----------------

        /// <summary>The expressions a picture may have beside it, by their words in records.xml (<c>&lt;variant&gt;</c> of a picture with <c>&lt;variantOf&gt;</c>).</summary>
        public static readonly string[] Faces = { "blink", "smile", "frown", "pain" };

        /// <summary>An expression's word as Rimfolk knows it (the case aside), or null for none of them.</summary>
        public static string FaceWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim();
            foreach (var f in Faces)
                if (string.Equals(f, t, StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        /// <summary>How long, in game ticks, a wound shows as pain on the face, the news of a death as a frown, and good news as a smile (four, six and two hours).</summary>
        public const int HurtFor = 4 * 2500, GriefFor = 6 * 2500, GoodNewsFor = 2 * 2500;

        /// <summary>The pain that shows on the face (the game's "medium pain"), and below which a pained face lets go.</summary>
        public const float PainFrom = 0.15f, PainUntil = 0.1f;

        /// <summary>A memory good enough to smile about (its mood), and a death's memory strong enough to frown about where its name doesn't say whose.</summary>
        public const float GoodNewsMood = 8f, GriefMood = -5f;

        /// <summary>A smile from this far above the middle of their mood (between their minor break line and full); a frown from this far below their minor break line; the slack before either lets go.</summary>
        public const float SmileAbove = 0.1f, FrownBelow = 0.05f, MoodSlack = 0.03f;

        /// <summary>
        /// Real seconds: a face chosen by the mood is held at least a minute before the mood changes it; a pain's or news' face
        /// eight seconds before the mood takes over again; any face three before pain or news replaces it; the fade between two faces.
        /// </summary>
        public const float MoodHold = 60f, NewsHold = 8f, ShowFirst = 3f, FadeFor = 0.6f;

        /// <summary>
        /// The news of a death close to them, by the memory's name (the game's: a colonist, a bonded animal, kin, someone
        /// they thought well of; a psychic bond torn, a stillbirth), or any other memory named for a death that weighs at
        /// least <see cref="GriefMood"/> (another mod's); not a stranger's death.
        /// </summary>
        public static bool IsGrief(string defName, float moodOffset)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            switch (defName)
            {
                case "KnowColonistDied":
                case "PawnWithGoodOpinionDied":
                case "WitnessedDeathFamily":
                case "WitnessedDeathAlly":
                case "BondedAnimalDied":
                case "PsychicBondTorn":
                case "Stillbirth":
                    return true;
                case "WitnessedDeathNonAlly":
                case "KnowPrisonerDiedInnocent":
                case "KnowGuestExecuted":
                    return false;
            }
            if (defName.StartsWith("My", StringComparison.Ordinal) && defName.EndsWith("Died", StringComparison.Ordinal)) return true;
            return defName.EndsWith("Died", StringComparison.Ordinal) && moodOffset <= GriefMood;
        }

        /// <summary>A memory to smile about: one that lifts the mood by <see cref="GoodNewsMood"/> or more.</summary>
        public static bool IsGoodNews(float moodOffset) => moodOffset >= GoodNewsMood;

        /// <summary>
        /// The face they should show now, of those the picture has (<paramref name="has"/>), with why: asleep or unconscious,
        /// the eyes closed (the blink picture held, else the plain one); hurt in the last hours or in pain, pain; the news of a
        /// death close to them, a frown; good news, a smile; else by the mood: a smile well above its middle, a frown well
        /// below the minor break line, the plain picture between. Each only where the picture has it, else the next.
        /// </summary>
        public static string Want(PortraitFeeling f, Func<string, bool> has, string shown, out PortraitFaceWhy why)
        {
            bool Has(string face) => has != null && has(face);
            if (f.asleep)
            {
                why = PortraitFaceWhy.Sleep;
                return Has("blink") ? "blink" : "";
            }
            bool hurt = f.hurtAgo >= 0 && f.hurtAgo < HurtFor || f.pain >= (shown == "pain" ? PainUntil : PainFrom);
            if (hurt && Has("pain"))
            {
                why = PortraitFaceWhy.Pain;
                return "pain";
            }
            if (f.griefAgo >= 0 && f.griefAgo < GriefFor && Has("frown"))
            {
                why = PortraitFaceWhy.News;
                return "frown";
            }
            if (f.goodAgo >= 0 && f.goodAgo < GoodNewsFor && Has("smile"))
            {
                why = PortraitFaceWhy.News;
                return "smile";
            }
            if (f.mood < 0f)
            {
                why = PortraitFaceWhy.Plain;
                return "";
            }
            why = PortraitFaceWhy.Mood;
            float middle = (f.minor + 1f) / 2f;
            if (f.mood >= middle + SmileAbove - (shown == "smile" ? MoodSlack : 0f) && Has("smile")) return "smile";
            if (f.mood <= f.minor - FrownBelow + (shown == "frown" ? MoodSlack : 0f) && Has("frown")) return "frown";
            return "";
        }

        /// <summary>
        /// Whether a face wanted for <paramref name="want"/> replaces one shown for <paramref name="shown"/> these
        /// <paramref name="shownFor"/> real seconds: falling asleep or waking at once; pain or news once the face before has
        /// shown a few seconds; back to the mood once a pain's or news' face has had its eight seconds; a change of mood only
        /// once the face before has shown a minute.
        /// </summary>
        public static bool SwitchNow(PortraitFaceWhy shown, float shownFor, PortraitFaceWhy want)
        {
            if (want == PortraitFaceWhy.Sleep || shown == PortraitFaceWhy.Sleep) return true;
            if (want == PortraitFaceWhy.Pain || want == PortraitFaceWhy.News) return shownFor >= ShowFirst;
            if (shown == PortraitFaceWhy.Pain || shown == PortraitFaceWhy.News) return shownFor >= NewsHold;
            if (shown == PortraitFaceWhy.Plain) return true;
            return shownFor >= MoodHold;
        }

        /// <summary>The face moved on to <paramref name="want"/> where <see cref="SwitchNow"/> allows (the first face for a person at once, with no fade); true where it changed.</summary>
        public static bool Step(PortraitFaceState s, string want, PortraitFaceWhy why, float now)
        {
            want ??= "";
            if (!s.started)
            {
                s.started = true;
                s.shown = s.from = want;
                s.why = why;
                s.since = now;
                s.fadeFrom = float.NegativeInfinity;
                return true;
            }
            if (want == s.shown)
            {
                s.why = why;
                return false;
            }
            if (!SwitchNow(s.why, now - s.since, why)) return false;
            s.from = s.shown;
            s.shown = want;
            s.why = why;
            s.since = now;
            s.fadeFrom = now;
            return true;
        }

        /// <summary>How far the face shown has faded in over the one before (0 to 1); falling asleep a little slower.</summary>
        public static float Faded(PortraitFaceState s, float now)
        {
            float d = s.why == PortraitFaceWhy.Sleep || s.from == "blink" ? FadeFor * 1.5f : FadeFor;
            return Clamp((now - s.fadeFrom) / d, 0f, 1f);
        }

        // ---------------- Alive: blinking ----------------

        /// <summary>How long a blink shows (seconds), the gap between blinks (from, to), and the gap before a quick second blink.</summary>
        public const float BlinkFor = 0.12f, BlinkGapLeast = 2.5f, BlinkGapMost = 6.5f, BlinkAgain = 0.3f;

        /// <summary>The seconds after blink <paramref name="n"/> to the next: between 2.5 and 6.5 by the person and the count, one time in eight a quick second blink, never two in a row.</summary>
        public static float BlinkGap(int seed, int n, bool afterQuick)
        {
            uint h = Hash(seed, n);
            if (!afterQuick && h % 8u == 0u) return BlinkAgain;
            return BlinkGapLeast + (BlinkGapMost - BlinkGapLeast) * ((h >> 8) % 1000u) / 999f;
        }

        /// <summary>
        /// Whether the eyes are shut in a blink at <paramref name="clock"/> (the movement clock, held while the game is paused),
        /// moving the blinks on; begun afresh where it was left a while (the person not shown).
        /// </summary>
        public static bool Blinking(PortraitFaceState s, float clock, int seed)
        {
            if (s.nextBlink < 0f || clock > s.nextBlink + 2f || clock < s.blinkEnd - BlinkFor - 1f)
            {
                s.nextBlink = clock + BlinkGap(seed, s.blinks, true) * 0.5f;
                s.blinkEnd = -1f;
                s.lastQuick = false;
            }
            if (clock >= s.nextBlink)
            {
                s.blinkEnd = s.nextBlink + BlinkFor;
                s.blinks++;
                float gap = BlinkGap(seed, s.blinks, s.lastQuick);
                s.lastQuick = gap < 1f;
                s.nextBlink += gap;
            }
            return clock < s.blinkEnd;
        }

        /// <summary>Whether a blink may show over this face: the plain picture, a smile or a frown, not pain or sleep, and not while a face fades in.</summary>
        public static bool MayBlink(PortraitFaceState s, float faded) => faded >= 1f && s.why != PortraitFaceWhy.Sleep && s.shown != "pain" && s.shown != "blink";

        // ---------------- The backdrop alive ----------------

        /// <summary>
        /// The weather over a backdrop outdoors, from the weather's own def: rain and snow by its rain and snow rates (1.5 or
        /// more full), a haze where its name or its overlays say fog, mist, a whiteout or a pall (thicker for a blinding fog or
        /// a whiteout), dust, sand or ash (tan, or grey for ash); a blood rain red, a toxic or acid one sickly green, a red fog red.
        /// </summary>
        public static PortraitWeather WeatherOf(string defName, float rainRate, float snowRate, IEnumerable<string> overlays)
        {
            var w = new PortraitWeather { rr = 200, rg = 210, rb = 225, hr = 214, hg = 218, hb = 224 };
            string n = (defName ?? "").ToLowerInvariant();
            string o = overlays == null ? "" : string.Join(" ", overlays.Where(x => x != null)).ToLowerInvariant();
            bool Has(params string[] words) => words.Any(x => n.Contains(x) || o.Contains(x));
            w.rain = float.IsNaN(rainRate) ? 0f : Clamp(rainRate / 1.5f, 0f, 1f);
            w.snow = float.IsNaN(snowRate) ? 0f : Clamp(snowRate / 1.5f, 0f, 1f);
            if (Has("fog", "mist", "whiteout", "pall"))
            {
                w.haze = Has("blind", "whiteout") ? 0.85f : 0.55f;
                if (Has("redfog")) Colour(ref w.hr, ref w.hg, ref w.hb, 205, 132, 116);
            }
            else if (Has("dust", "sand", "ash"))
            {
                w.haze = 0.5f;
                if (Has("ash")) Colour(ref w.hr, ref w.hg, ref w.hb, 132, 126, 120);
                else Colour(ref w.hr, ref w.hg, ref w.hb, 196, 170, 128);
            }
            if (Has("blood")) Colour(ref w.rr, ref w.rg, ref w.rb, 150, 30, 32);
            else if (Has("tox", "acid")) Colour(ref w.rr, ref w.rg, ref w.rb, 176, 196, 96);
            return w;
        }

        private static void Colour(ref byte r, ref byte g, ref byte b, byte nr, byte ng, byte nb)
        {
            r = nr;
            g = ng;
            b = nb;
        }

        /// <summary>How strongly the weather shows at a step of light (0 daylight to 3 dark): fainter as it darkens.</summary>
        public static float WeatherAlpha(float amount, int step) => Clamp(amount, 0f, 1f) * (1f - 0.18f * Math.Max(0, Math.Min(3, step)));

        /// <summary>A fire's light over a backdrop indoors: the backdrop shows a hearth or a fire, or a fire is lit in the room.</summary>
        public static bool Hearth(bool indoors, ICollection<string> contents, bool fireLit) =>
            indoors && (fireLit || contents != null && (contents.Contains("hearth") || contents.Contains("fire")));

        /// <summary>The fire's light at <paramref name="clock"/>, 0 to 1: three waves out of step, so no beat the eye catches.</summary>
        public static float Flicker(float clock, int seed)
        {
            double s = (seed & 1023) * 0.37;
            double v = 0.55 + 0.22 * Math.Sin(6.1 * clock + s) + 0.14 * Math.Sin(11.7 * clock + 2.1 * s) + 0.09 * Math.Sin(23.3 * clock + 0.7 * s);
            return Clamp((float)v, 0f, 1f);
        }

        /// <summary>How strong the warm light over the backdrop is for a flicker and a step of light: soft, a little stronger in the dark where the fire is the light.</summary>
        public static float HearthAlpha(float flicker, int step) => (0.05f + 0.07f * Clamp(flicker, 0f, 1f)) * (1f + 0.15f * Math.Max(0, Math.Min(3, step)));

        // ---------------- Helpers ----------------

        private static uint Hash(int seed, int n)
        {
            uint h = 2166136261u;
            unchecked
            {
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)n) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
            }
            return h;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Clamp((x - e0) / (e1 - e0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static byte Byte(float v) => v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)(v + 0.5f);
    }
}
