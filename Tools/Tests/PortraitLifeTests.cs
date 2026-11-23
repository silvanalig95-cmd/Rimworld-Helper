using System.Diagnostics;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Portraits, phase 6: the large portrait alive and with depth (the player, 22 November 2026: "to make it perhaps a little
/// bit more alive", and "if I, for example, hover my mouse, it kind of centers towards the mouse in a way, like this kind
/// of fake 3D effect"). The pure parts: the layers' moves for the mouse and their easing, never an edge showing and the
/// body on the bottom edge; a backdrop cut into slices by a small depth map; which expression shows when, held so it never
/// flickers, and the blinks' timing; the weather and the fire's flicker; the records of a picture's expressions and a
/// backdrop's depth map; the settings, words and seams; the slicing timed.
/// </summary>
public static class PortraitLifeTests
{
    private static PortraitRecord Picture(string name, string? variant = null, string? variantOf = null, string? pawn = null, string? character = null, List<string>? problems = null)
    {
        var f = new Dictionary<string, string?>
        {
            ["texPath"] = "x/" + name, ["sex"] = "male", ["age"] = "adult", ["skinColor"] = "#c9a27e", ["hairColor"] = "#2a2a2a", ["variant"] = variant, ["variantOf"] = variantOf,
            ["pawn"] = pawn, ["character"] = character
        };
        return PortraitLibraryMath.Record(name, k => f.TryGetValue(k, out var v) ? v : null, _ => null, problems ?? new List<string>());
    }

    private static PortraitFeeling Feel(float mood = 0.6f, float minor = 0.35f, bool asleep = false, float pain = 0f, int hurt = -1, int grief = -1, int good = -1) =>
        new PortraitFeeling { mood = mood, minor = minor, asleep = asleep, pain = pain, hurtAgo = hurt, griefAgo = grief, goodAgo = good };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits, phase 6: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- The backdrop kept while a person is shown ----------------
        // (the player: "only refresh the background once I kind of click away and click back to the same pawn")
        C(PortraitLifeMath.NewShowing(-10, 100) && PortraitLifeMath.NewShowing(50, 100) && !PortraitLifeMath.NewShowing(99, 100)
            && !PortraitLifeMath.NewShowing(98, 100) && PortraitLifeMath.NewShowing(97, 100) && PortraitLifeMath.NewShowing(500, 100),
            "a person shown frame after frame is the same showing; after a gap of a few frames, a new one");
        string stageKept = Read(Path.Combine("UI", "Faces", "FaceEngineStage.cs"));
        C(stageKept.Contains("PortraitLifeMath.NewShowing(look.shownFrame, frame)") && stageKept.Contains("if (fresh)")
            && stageKept.Contains("LookAlive(p, look.where, look)"),
            "the large portrait reads where they are and chooses the backdrop only when it starts showing them");

        // ---------------- Depth that follows the mouse ----------------
        C(PortraitLifeMath.StrengthOf(PortraitDepthMode.Off) == 0f && PortraitLifeMath.StrengthOf(PortraitDepthMode.Subtle) > 0f
          && PortraitLifeMath.StrengthOf(PortraitDepthMode.Stronger) > PortraitLifeMath.StrengthOf(PortraitDepthMode.Subtle), "off moves nothing, stronger more than subtle");
        PortraitLifeMath.Pointer(150f, 50f, 0f, 0f, 200f, 100f, out float px, out float py);
        C(Math.Abs(px - 0.5f) < 1e-4f && Math.Abs(py) < 1e-4f, "the mouse halfway to the right edge is 0.5 across, at the middle 0 down");
        PortraitLifeMath.Pointer(-80f, 400f, 0f, 0f, 200f, 100f, out px, out py);
        C(px == -1f && py == 1f, "the mouse past the edges is held at -1 and 1");
        PortraitLifeMath.Pointer(5f, 5f, 0f, 0f, 0f, 0f, out px, out py);
        C(px == 0f && py == 0f, "a portrait of no size: the middle");
        {
            float v = 0f, last = 0f;
            bool rising = true;
            for (int i = 0; i < 60; i++)
            {
                v = PortraitLifeMath.Ease(v, 1f, 1f / 60f, PortraitLifeMath.FollowFor);
                rising &= v >= last && v <= 1f;
                last = v;
            }
            C(rising && v > 0.95f, $"easing toward the mouse rises steadily and is nearly there within a second ({v:0.000})");
            float t = 0f;
            v = 0f;
            while (v < 0.63f && t < 2f)
            {
                v = PortraitLifeMath.Ease(v, 1f, 1f / 120f, PortraitLifeMath.FollowFor);
                t += 1f / 120f;
            }
            C(Math.Abs(t - PortraitLifeMath.FollowFor) < 0.03f, $"about two thirds of the way in its time ({t:0.00} s)");
            float back = 1f;
            for (int i = 0; i < 240; i++) back = PortraitLifeMath.Ease(back, 0f, 1f / 60f, PortraitLifeMath.SettleFor);
            C(back == 0f, "back to the middle when the mouse leaves, settled exactly within a few seconds");
            C(PortraitLifeMath.Ease(0.3f, 1f, 0f, 0.2f) == 0.3f && PortraitLifeMath.Ease(0.3f, 1f, float.NaN, 0.2f) == 0.3f && PortraitLifeMath.Ease(0.3f, 1f, 0.1f, 0f) == 1f,
                "no time passed: no move; no easing time: at once");
            // The same distance in big or small steps (a slow frame or a fast one) lands close.
            float a1 = 0f, a2 = 0f;
            for (int i = 0; i < 4; i++) a1 = PortraitLifeMath.Ease(a1, 1f, 0.05f, 0.2f);
            a2 = PortraitLifeMath.Ease(a2, 1f, 0.2f, 0.2f);
            C(Math.Abs(a1 - a2) < 1e-3f, "the easing doesn't depend on the frame rate");
        }
        {
            float s = PortraitLifeMath.StrengthOf(PortraitDepthMode.Subtle), w = 288f;
            PortraitLifeMath.Shift(0f, 1f, 0f, s, w, out float wall, out _);
            PortraitLifeMath.Shift(1f, 1f, 0f, s, w, out float person, out _);
            PortraitLifeMath.Shift(PortraitLifeMath.ShadowZ, 1f, 0f, s, w, out float shadow, out _);
            PortraitLifeMath.Shift(PortraitLifeMath.NearestSliceZ, 1f, 0f, s, w, out float near, out _);
            C(wall < 0f && person > 0f, $"the mouse to the right: the far wall moves left, against it ({wall:0.0}), the person right, with it ({person:0.0})");
            C(shadow > wall && shadow < person && Math.Abs(shadow) < Math.Abs(person), $"the shadow on the wall moves less than the person ({shadow:0.0})");
            C(near > wall && near < shadow, $"a near slice of the backdrop moves less against the mouse than the far wall, so the two part ({near:0.0})");
            C(Math.Abs(wall) >= 2f && Math.Abs(wall) <= 6f, $"subtle at 288 pixels: a few pixels ({wall:0.0})");
            PortraitLifeMath.Shift(0f, 1f, 0f, 1f, w, out float strong, out _);
            PortraitLifeMath.Shift(1f, 1f, 1f, 1f, w, out float strongPerson, out float strongPersonY);
            C(Math.Abs(strong - (-7f)) < 0.2f && Math.Abs(strongPerson - 5f) < 0.2f && Math.Abs(strongPersonY - 2f) < 0.2f,
                "stronger at 288 pixels: the lead's mock-up (the backdrop 7 across, the person 5 across and 2 down)");
            PortraitLifeMath.Shift(0f, 0f, 0f, s, w, out float still, out float stillY);
            PortraitLifeMath.Shift(1f, 1f, 1f, 0f, w, out float off, out float offY);
            C(still == 0f && stillY == 0f && off == 0f && offY == 0f, "the mouse in the middle, or depth off: nothing moves");
            // Never an edge: at every mouse position and both strengths, the backdrop larger by its margin covers the portrait.
            bool covered = true, sides = true, foot = true;
            foreach (var mode in new[] { PortraitDepthMode.Subtle, PortraitDepthMode.Stronger })
            foreach (int pw in new[] { 96, 186, 288, 354, 720 })
            {
                float st = PortraitLifeMath.StrengthOf(mode);
                PortraitLifeMath.Margin(st, pw, out int mx, out int my);
                for (float ax = -1f; ax <= 1f; ax += 0.25f)
                for (float ay = -1f; ay <= 1f; ay += 0.25f)
                {
                    PortraitLifeMath.Shift(0f, ax, ay, st, pw, out float dx, out float dy);
                    covered &= Math.Abs(dx) <= mx - 0.5f && Math.Abs(dy) <= my - 0.5f;
                    PortraitLifeMath.Shift(1f, ax, ay, st, pw, out float pdx, out float pdy);
                    sides &= Math.Abs(pdx) <= PortraitLifeMath.PersonSide(st) * pw;
                    // The person's foot row is carried on an eighth of the width below the edge (FaceEngineStage.FootBelow).
                    foot &= -pdy <= 0.12f * pw;
                }
            }
            C(covered, "never an edge: the backdrop's margin always covers the far wall's move, with a pixel to spare");
            C(sides, "the person's sides carried on past their own farther than they ever move");
            C(foot, "the body stays on the bottom edge: the person never rises past the foot carried on below it");
            PortraitLifeMath.Margin(0f, 288, out int m0x, out int m0y);
            C(m0x == 0 && m0y == 0 && PortraitLifeMath.PersonSide(0f) == 0f, "depth off: the backdrop and the person made as before, no larger");
        }

        // ---------------- A backdrop in slices by its depth ----------------
        {
            int w = 24, h = 20;
            var back = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                back[i * 4] = (byte)(i % 251);
                back[i * 4 + 1] = 90;
                back[i * 4 + 2] = 40;
                back[i * 4 + 3] = 255;
            }
            // A far wall on the left, a near cupboard on the right.
            var depth = new byte[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                depth[y * w + x] = x < w / 2 ? (byte)30 : (byte)230;
            int count = PortraitLifeMath.LayerCount(depth, PortraitLifeMath.LayersMost, out int lo, out int hi);
            C(lo == 30 && hi == 230 && count == PortraitLifeMath.LayersMost, $"a wall and a cupboard: the depth's ends found, worth every layer ({lo}, {hi}, {count})");
            var slices = PortraitLifeMath.Slices(back, depth, w, h, PortraitLifeMath.LayersMost);
            C(slices.Count == PortraitLifeMath.LayersMost - 1, $"the far wall is the backdrop itself, and three nearer slices ({slices.Count})");
            C(slices.Select(s => s.z).SequenceEqual(slices.Select(s => s.z).OrderBy(z => z)) && Math.Abs(slices[^1].z - PortraitLifeMath.NearestSliceZ) < 1e-4f && slices[0].z > 0f,
                "the slices nearer one after another, the nearest at the nearest slice's depth");
            var near = slices[^1];
            C(near.x >= w / 2 - 2 && near.x + near.w == w && near.y == 0 && near.h == h, $"the nearest slice cut to the cupboard's box ({near.x}, {near.y}, {near.w} x {near.h})");
            int A(DepthSlice s, int x, int y) => x < s.x || y < s.y || x >= s.x + s.w || y >= s.y + s.h ? 0 : s.px[((y - s.y) * s.w + x - s.x) * 4 + 3];
            C(A(near, w - 2, h / 2) == 255 && A(near, 2, h / 2) == 0, "the cupboard whole in it, the wall not at all");
            C(near.px[((h / 2 - near.y) * near.w + (w - 3 - near.x)) * 4] == back[((h / 2) * w + w - 3) * 4], "its colours the backdrop's own");
            bool nested = true;
            for (int k = 0; k + 1 < slices.Count; k++)
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    nested &= A(slices[k + 1], x, y) <= A(slices[k], x, y) + 1;
            C(nested, "each slice holds everything nearer than its edge, so drawn far to near no hole shows behind a near thing as it moves");
            var flat = Enumerable.Repeat((byte)120, w * h).ToArray();
            C(PortraitLifeMath.Slices(back, flat, w, h, 4).Count == 0 && PortraitLifeMath.LayerCount(flat, 4, out _, out _) == 1, "a flat wall: no slices, the backdrop moves as one");
            C(PortraitLifeMath.Slices(back, depth, w, h, 1).Count == 0 && PortraitLifeMath.Slices(back, null!, w, h, 4).Count == 0 && PortraitLifeMath.Slices(back, new byte[3], w, h, 4).Count == 0,
                "one layer asked, or no depth map, or the wrong size: no slices");
            // A floor receding slowly: no hard tear, the slice's edge eases across it.
            var ramp = new byte[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                ramp[y * w + x] = (byte)(20 + y * 210 / (h - 1));
            var rs = PortraitLifeMath.Slices(back, ramp, w, h, 4);
            int steps = 0, jumps = 0;
            var first = rs.Count > 0 ? rs[0] : null;
            if (first != null)
                for (int y = 1; y < h; y++)
                {
                    int d = A(first, w / 2, y) - A(first, w / 2, y - 1);
                    if (d != 0) steps++;
                    if (Math.Abs(d) > 200) jumps++;
                }
            C(first != null && steps >= 3 && jumps == 0, $"a floor that recedes slowly blends from one slice into the next over several rows, never a tear ({steps} rows)");
            int few = PortraitLifeMath.LayerCount(Enumerable.Range(0, w * h).Select(i => (byte)(100 + i % 30)).ToArray(), 4, out _, out _);
            C(few == 2, $"a narrow spread of depth: fewer layers ({few})");
            var rgbaDepth = new byte[] { 10, 200, 30, 255, 7, 7, 7, 255 };
            C(PortraitLifeMath.Grey(rgbaDepth).SequenceEqual(new byte[] { 200, 7 }) && PortraitLifeMath.Grey(null!) == null, "a depth map read as one value a pixel, the brightest of its colours");
        }
        {
            int w = 40, h = 48;
            var person = new byte[w * h * 4];
            for (int y = 10; y < h; y++)
            for (int x = 14; x < 26; x++)
                person[(y * w + x) * 4 + 3] = 255;
            var sh = PortraitLifeMath.Shadow(person, w, h, 0.5f, out int sw, out int shh);
            C(sw == 10 && shh == 12 && sh.Length == sw * shh * 4, "the shadow a quarter the size");
            C(sh[((8 * sw) + 5) * 4 + 3] > 40 && sh[((8 * sw) + 5) * 4 + 3] <= 128 && sh[((1 * sw) + 0) * 4 + 3] < 10 && sh[0] < 30,
                "dark where the person is (at its strength), clear far from them, dark in colour");
        }

        // ---------------- Which face shows ----------------
        {
            var all = new HashSet<string>(PortraitLifeMath.Faces);
            Func<string, bool> has = all.Contains, none = _ => false;
            C(PortraitLifeMath.Faces.SequenceEqual(new[] { "blink", "smile", "frown", "pain" }) && PortraitLifeMath.FaceWord(" Smile ") == "smile" && PortraitLifeMath.FaceWord("laugh") == null,
                "the four expressions by their words, the case aside");
            string W(PortraitFeeling f, Func<string, bool> h, string shown, out PortraitFaceWhy why) => PortraitLifeMath.Want(f, h, shown, out why);
            C(W(Feel(asleep: true, pain: 0.5f), has, "", out var why) == "blink" && why == PortraitFaceWhy.Sleep, "asleep or unconscious: the eyes closed held, before pain");
            C(W(Feel(asleep: true), _ => false, "", out why) == "" && why == PortraitFaceWhy.Sleep, "asleep with no blink painted: the plain picture");
            C(W(Feel(hurt: 2000, grief: 100), has, "", out why) == "pain" && why == PortraitFaceWhy.Pain, "hurt in the last hours: pain, before the news of a death");
            C(W(Feel(hurt: PortraitLifeMath.HurtFor + 1), has, "", out why) == "" && why == PortraitFaceWhy.Mood, "a wound older than a few hours: no longer");
            C(W(Feel(pain: 0.2f), has, "", out _) == "pain" && W(Feel(pain: 0.12f), has, "", out _) == "" && W(Feel(pain: 0.12f), has, "pain", out _) == "pain"
              && W(Feel(pain: 0.05f), has, "pain", out _) == "", "in pain from the game's medium pain, let go only below a little less (no flicker at the line)");
            C(W(Feel(grief: 5000, good: 10), has, "", out why) == "frown" && why == PortraitFaceWhy.News, "the news of a death close to them: a frown, before good news");
            C(W(Feel(good: 1000, mood: 0.2f), has, "", out why) == "smile" && why == PortraitFaceWhy.News, "good news: a smile for a while, even in a low mood");
            C(W(Feel(good: PortraitLifeMath.GoodNewsFor + 5, mood: 0.6f), has, "", out _) == "" && W(Feel(grief: PortraitLifeMath.GriefFor + 5, mood: 0.6f), has, "", out _) == "",
                "old news shows no more");
            // The mood: its middle between the minor break line (0.35) and full is 0.675.
            C(W(Feel(mood: 0.80f), has, "", out why) == "smile" && why == PortraitFaceWhy.Mood, "a mood well above its middle: a smile");
            C(W(Feel(mood: 0.70f), has, "", out _) == "", "a mood a little above its middle: the plain picture");
            C(W(Feel(mood: 0.76f), has, "smile", out _) == "smile" && W(Feel(mood: 0.73f), has, "smile", out _) == "", "a smile lets go only a little below where it began");
            C(W(Feel(mood: 0.25f), has, "", out _) == "frown" && W(Feel(mood: 0.32f), has, "", out _) == "", "a mood well below the minor break line: a frown; just below it: plain");
            C(W(Feel(mood: 0.31f), has, "frown", out _) == "frown" && W(Feel(mood: 0.34f), has, "frown", out _) == "", "a frown lets go only a little above where it began");
            C(W(Feel(mood: 0.5f, minor: 0.2f), has, "", out _) == "" && W(Feel(mood: 0.12f, minor: 0.2f), has, "", out _) == "frown"
              && W(Feel(mood: 0.72f, minor: 0.2f), has, "", out _) == "smile", "by their own break line: an iron will lowers both");
            C(W(Feel(mood: -1f), has, "", out why) == "" && why == PortraitFaceWhy.Plain, "no mood (some kinds have none): the plain picture");
            var noPain = new HashSet<string> { "blink", "frown" };
            C(W(Feel(hurt: 10, mood: 0.2f), noPain.Contains, "", out why) == "frown" && why == PortraitFaceWhy.Mood, "hurt but no pain painted: the next that applies (here a low mood's frown)");
            C(W(Feel(good: 10, mood: 0.9f), none, "", out _) == "" && W(Feel(asleep: true), none, "", out _) == "", "a picture with no expressions stays as it is");
        }
        {
            var s = new PortraitFaceState();
            C(PortraitLifeMath.Step(s, "smile", PortraitFaceWhy.Mood, 10f) && s.shown == "smile" && PortraitLifeMath.Faded(s, 10f) == 1f, "the first face for a person shows at once, with no fade");
            C(!PortraitLifeMath.Step(s, "", PortraitFaceWhy.Mood, 40f) && s.shown == "smile", "a change of mood waits: half a minute later the smile holds");
            C(PortraitLifeMath.Step(s, "", PortraitFaceWhy.Mood, 10f + PortraitLifeMath.MoodHold) && s.shown == "" && s.from == "smile", "a minute later the mood changes it");
            C(PortraitLifeMath.Faded(s, 70f) == 0f && Math.Abs(PortraitLifeMath.Faded(s, 70f + PortraitLifeMath.FadeFor / 2f) - 0.5f) < 0.01f && PortraitLifeMath.Faded(s, 72f) == 1f,
                "one face fades into the next over a little over half a second");
            C(!PortraitLifeMath.Step(s, "pain", PortraitFaceWhy.Pain, 71f), "pain waits a moment while a face has only just come");
            C(PortraitLifeMath.Step(s, "pain", PortraitFaceWhy.Pain, 70f + PortraitLifeMath.ShowFirst) && s.shown == "pain", "then pain shows at once, without the mood's minute");
            C(!PortraitLifeMath.Step(s, "", PortraitFaceWhy.Mood, 75f) && PortraitLifeMath.Step(s, "", PortraitFaceWhy.Mood, 73f + PortraitLifeMath.NewsHold), "the pain's face held a few seconds, then back to the mood");
            C(PortraitLifeMath.Step(s, "blink", PortraitFaceWhy.Sleep, 81.5f) && s.shown == "blink" && PortraitLifeMath.Step(s, "", PortraitFaceWhy.Mood, 82f) && s.shown == "",
                "falling asleep and waking: at once");
            C(PortraitLifeMath.Faded(s, 82f + PortraitLifeMath.FadeFor) < 1f && PortraitLifeMath.Faded(s, 82f + PortraitLifeMath.FadeFor * 1.5f) == 1f, "waking opens the eyes a little slower");
            // Over ten minutes of a mood at the line, the face changes at most once a minute.
            var t = new PortraitFaceState();
            int changes = 0;
            for (int i = 0; i < 600 * 4; i++)
            {
                float now = i / 4f;
                var f = Feel(mood: 0.775f + 0.02f * (float)Math.Sin(now * 3f));
                string want = PortraitLifeMath.Want(f, PortraitLifeMath.Faces.Contains, t.shown, out var why);
                if (PortraitLifeMath.Step(t, want, why, now) && i > 0) changes++;
            }
            C(changes <= 10, $"a mood wavering at the line never flickers ({changes} changes in ten minutes)");
        }

        // ---------------- Blinking ----------------
        {
            bool gaps = true;
            int quick = 0, twice = 0;
            bool lastQuick = false;
            for (int k = 0; k < 4000; k++)
            {
                float g = PortraitLifeMath.BlinkGap(1234, k, lastQuick);
                bool q = g == PortraitLifeMath.BlinkAgain;
                gaps &= q || g >= PortraitLifeMath.BlinkGapLeast && g <= PortraitLifeMath.BlinkGapMost;
                if (q) quick++;
                if (q && lastQuick) twice++;
                lastQuick = q;
            }
            C(gaps, "the gap between blinks between two and a half and six and a half seconds, or a quick second blink");
            C(quick > 300 && quick < 700 && twice == 0, $"about one blink in eight is followed at once by another, never two in a row ({quick} of 4000)");
            C(PortraitLifeMath.BlinkGap(7, 3, false) == PortraitLifeMath.BlinkGap(7, 3, false) && Enumerable.Range(0, 20).Select(i => PortraitLifeMath.BlinkGap(7, i, true)).Distinct().Count() > 10,
                "the same person blinks the same way each time; the gaps vary");
            var s = new PortraitFaceState();
            int blinks = 0, closedFrames = 0;
            bool was = false;
            for (int i = 0; i < 120 * 60; i++)
            {
                bool shut = PortraitLifeMath.Blinking(s, 500f + i / 60f, 42);
                if (shut) closedFrames++;
                if (shut && !was) blinks++;
                was = shut;
            }
            C(blinks >= 18 && blinks <= 50, $"two minutes on the clock: a blink every few seconds ({blinks})");
            float perBlink = closedFrames / 60f / Math.Max(1, blinks);
            C(perBlink > 0.08f && perBlink < 0.16f, $"each about a tenth of a second ({perBlink:0.000} s)");
            var paused = new PortraitFaceState();
            int during = 0;
            for (int i = 0; i < 600; i++)
                if (PortraitLifeMath.Blinking(paused, 10f, 42))
                    during++;
            C(during == 0 || during == 600, "the clock held (the game paused): the eyes stay as they are");
            var left = new PortraitFaceState();
            PortraitLifeMath.Blinking(left, 10f, 5);
            C(!PortraitLifeMath.Blinking(left, 900f, 5) && left.nextBlink > 900f && left.nextBlink < 905f, "shown again after a while: the blinks begin afresh, not all at once");
            var face = new PortraitFaceState { shown = "", why = PortraitFaceWhy.Mood };
            C(PortraitLifeMath.MayBlink(face, 1f) && !PortraitLifeMath.MayBlink(face, 0.5f), "a blink over an open-eyed face, not while it fades in");
            face.shown = "smile";
            bool smile = PortraitLifeMath.MayBlink(face, 1f);
            face.shown = "pain";
            bool pain = PortraitLifeMath.MayBlink(face, 1f);
            face.shown = "blink";
            face.why = PortraitFaceWhy.Sleep;
            C(smile && !pain && !PortraitLifeMath.MayBlink(face, 1f), "a smile blinks; pain and sleep don't");
        }

        // ---------------- Only the head of an expression laid over the picture ----------------
        {
            int w = 100, h = 125;
            // An expression painted from a picture: a figure solid from row 20 (from the top) down, its edge soft at the sides.
            var face = new byte[w * h * 4];
            for (int top = 0; top < h; top++)
            for (int x = 0; x < w; x++)
            {
                int o = ((h - 1 - top) * w + x) * 4;
                face[o] = 200;
                face[o + 1] = 100;
                face[o + 2] = 50;
                face[o + 3] = top < 20 || x < 35 || x >= 65 ? (byte)0 : x == 35 || x == 64 ? (byte)128 : (byte)255;
            }
            // The head's middle 30% across... at the middle, 30% down, 30% of the height tall.
            var over = PortraitLifeMath.FaceOverlay(face, w, h, 0.5f, 0.3f, 0.3f, 1f, 1f, 1f, out int ox, out int oy, out int ow, out int oh);
            float cy = 0.7f * h, ry = PortraitLifeMath.HeadReachY * 0.3f * h, rx = PortraitLifeMath.HeadReachX * 0.3f * h;
            C(ow < w && oh < h / 2 && ox > 0 && Math.Abs(ox + ow / 2f - w / 2f) <= 1.5f && Math.Abs(oy + oh / 2f - cy) <= 1.5f,
                $"cut to the box round the head ({ox}, {oy}, {ow} x {oh}), not the whole picture");
            C(Math.Abs(ow - 2f * rx) <= 3f && Math.Abs(oh - 2f * ry) <= 3f, "the box as wide and tall as the oval round the head");
            int OA(int x, int rowFromFoot) => over[((rowFromFoot - oy) * ow + (x - ox)) * 4 + 3];
            C(OA(w / 2, (int)cy) == 255 && over[((((int)cy - oy) * ow) + (w / 2 - ox)) * 4] == 200, "the face itself taken whole, in the expression's colours");
            C(OA(ox, oy) == 0 && OA(ox + ow - 1, oy + oh - 1) == 0, "the oval's corners left to the picture");
            int rowEdge = (int)cy;
            C(OA(35, rowEdge) == 0 && OA(36, rowEdge) == 255 && OA(64, rowEdge) == 0, "the person's soft edge left to the picture (laid over it never thickens the edge); just inside it taken");
            var softer = new List<int>();
            for (int row = (int)cy; row >= oy; row--) softer.Add(OA(w / 2, row));
            C(softer.Zip(softer.Skip(1), (a, b) => b <= a).All(b => b) && softer[0] == 255 && softer[^1] < 30 && softer.Count(v => v > 0 && v < 255) >= 3,
                "the oval's edge softens toward the body over several rows, so no line shows round the face");
            var tinted = PortraitLifeMath.FaceOverlay(face, w, h, 0.5f, 0.3f, 0.3f, 0.5f, 1f, 1f, out _, out _, out _, out _);
            C(tinted[(((int)cy - oy) * ow + (w / 2 - ox)) * 4] == 100, "tinted as the person was");
            var fallback = PortraitLifeMath.FaceOverlay(face, w, h, float.NaN, 0.3f, 0f, 1f, 1f, 1f, out int fx, out _, out int fw, out _);
            C(fallback.Length == fw * 4 * ((fallback.Length / 4) / fw) && fw < w && fx > 0, "no head measured: the library's usual place for it");
            var high = PortraitLifeMath.FaceOverlay(face, w, h, 0.5f, 0.05f, 0.3f, 1f, 1f, 1f, out _, out int hy, out _, out int hh);
            C(hy + hh <= h && high.Length > 0, "a head at the very top: the box held inside the picture");
            // Fading: the face before whole, the one now coming; back to the plain picture, the one before going.
            PortraitLifeMath.FadeStrengths("", "smile", 0.25f, out float b1, out float n1);
            PortraitLifeMath.FadeStrengths("smile", "frown", 0.4f, out float b2, out float n2);
            PortraitLifeMath.FadeStrengths("smile", "", 0.25f, out float b3, out float n3);
            PortraitLifeMath.FadeStrengths("smile", "", 1f, out float b4, out float n4);
            PortraitLifeMath.FadeStrengths("blink", "blink", 1f, out float b5, out float n5);
            C(b1 == 0f && n1 == 0.25f && b2 == 1f && n2 == 0.4f && Math.Abs(b3 - 0.75f) < 1e-5f && n3 == 0f && b4 == 0f && n4 == 0f && b5 == 0f && n5 == 1f,
                "fading: into a face from the plain picture, from one face to another, back to the plain picture, settled, a blink");
        }

        // ---------------- News from the memories ----------------
        foreach (var d in new[] { "KnowColonistDied", "PawnWithGoodOpinionDied", "MySonDied", "MyWifeDied", "WitnessedDeathFamily", "BondedAnimalDied", "PsychicBondTorn" })
            C(PortraitLifeMath.IsGrief(d, -3f), $"grief: {d}");
        C(!PortraitLifeMath.IsGrief("WitnessedDeathNonAlly", -10f) && !PortraitLifeMath.IsGrief("KnowPrisonerDiedInnocent", -10f) && !PortraitLifeMath.IsGrief("AteWithoutTable", -10f)
          && !PortraitLifeMath.IsGrief(null!, -10f), "a stranger's death, or no death, is no grief");
        C(PortraitLifeMath.IsGrief("VFE_SquireDied", -8f) && !PortraitLifeMath.IsGrief("VFE_SquireDied", -2f), "another mod's death memory: by how much it weighs");
        C(PortraitLifeMath.IsGoodNews(12f) && !PortraitLifeMath.IsGoodNews(5f), "good news: a memory that lifts the mood clearly");

        // ---------------- The backdrop alive ----------------
        {
            var rain = PortraitLifeMath.WeatherOf("Rain", 1f, 0f, new[] { "WeatherOverlay_Rain" });
            C(rain.rain > 0.6f && rain.rain < 0.7f && rain.snow == 0f && rain.haze == 0f && rain.Any, "rain: by its rain rate, no snow or haze");
            var snow = PortraitLifeMath.WeatherOf("SnowHard", 0f, 1.2f, new[] { "WeatherOverlay_SnowHard" });
            C(snow.snow > 0.7f && snow.rain == 0f, "hard snow: by its snow rate");
            C(PortraitLifeMath.WeatherOf("SnowGentle", 0f, 0.6f, null).snow < snow.snow, "gentle snow lighter");
            var fog = PortraitLifeMath.WeatherOf("Fog", 0f, 0f, new[] { "WeatherOverlay_Fog" });
            C(fog.haze > 0.4f && fog.rain == 0f, "fog: a haze");
            var foggyRain = PortraitLifeMath.WeatherOf("FoggyRain", 0.6f, 0f, null);
            C(foggyRain.haze > 0f && foggyRain.rain > 0f, "foggy rain: both");
            C(PortraitLifeMath.WeatherOf("BlindFog", 0f, 0f, null).haze > fog.haze && PortraitLifeMath.WeatherOf("VEE_Whiteout", 0f, 1f, null).haze > fog.haze, "a blinding fog or a whiteout thicker");
            C(PortraitLifeMath.WeatherOf("Somebody_MistyMorning", 0f, 0f, null).haze > 0f && PortraitLifeMath.WeatherOf("GrayPall", 0f, 0f, null).haze > 0f
              && PortraitLifeMath.WeatherOf("ModWeather", 0f, 0f, new[] { "WeatherOverlay_Fog" }).haze > 0f, "fog by its name or its overlay, another mod's too");
            var blood = PortraitLifeMath.WeatherOf("BloodRain", 1f, 0f, null);
            C(blood.rr > 120 && blood.rg < 60 && blood.rain > 0f, "a blood rain red");
            var tox = PortraitLifeMath.WeatherOf("ToxRain", 1f, 0f, null);
            C(tox.rg > tox.rr && tox.rg > tox.rb, "a toxic rain sickly green");
            var sand = PortraitLifeMath.WeatherOf("Sandstorm", 0f, 0f, null);
            var ash = PortraitLifeMath.WeatherOf("AB_VolcanicAsh", 0f, 0f, null);
            C(sand.haze > 0f && sand.hr > sand.hb + 40 && ash.haze > 0f && Math.Abs(ash.hr - ash.hb) < 20, "a sandstorm a tan haze, volcanic ash a grey one");
            var clear = PortraitLifeMath.WeatherOf("Clear", 0f, 0f, null);
            C(!clear.Any && !PortraitLifeMath.WeatherOf("DryThunderstorm", 0f, 0f, null).Any && !PortraitLifeMath.WeatherOf("Overcast", 0f, 0f, null).Any, "clear skies, a dry storm or cloud: nothing");
            C(PortraitLifeMath.WeatherOf("TorrentialRain", 3f, 0f, null).rain == 1f && PortraitLifeMath.WeatherOf("Odd", float.NaN, -1f, null).rain == 0f, "held between nothing and full");
            C(PortraitLifeMath.WeatherAlpha(1f, 0) == 1f && PortraitLifeMath.WeatherAlpha(1f, 3) < PortraitLifeMath.WeatherAlpha(1f, 1) && PortraitLifeMath.WeatherAlpha(1f, 3) > 0.3f,
                "fainter in the dark, still seen");
            var hearth = new HashSet<string> { "bed", "hearth" };
            C(PortraitLifeMath.Hearth(true, hearth, false) && PortraitLifeMath.Hearth(true, new HashSet<string> { "fire" }, false), "indoors, a backdrop showing a hearth or a fire: its flicker");
            C(PortraitLifeMath.Hearth(true, new HashSet<string> { "bed" }, true) && PortraitLifeMath.Hearth(true, null!, true), "a fire lit in the room: its flicker, whatever the backdrop shows");
            C(!PortraitLifeMath.Hearth(true, new HashSet<string> { "bed" }, false) && !PortraitLifeMath.Hearth(false, hearth, true), "no fire, or outdoors: none");
            float lo = 1f, hi = 0f;
            for (int i = 0; i < 2000; i++)
            {
                float f = PortraitLifeMath.Flicker(i * 0.013f, 77);
                lo = Math.Min(lo, f);
                hi = Math.Max(hi, f);
            }
            C(lo >= 0f && hi <= 1f && hi - lo > 0.5f, $"the fire's light flickers between {lo:0.00} and {hi:0.00}");
            C(PortraitLifeMath.Flicker(3.3f, 5) == PortraitLifeMath.Flicker(3.3f, 5) && PortraitLifeMath.Flicker(3.3f, 5) != PortraitLifeMath.Flicker(3.3f, 6), "the same fire the same way; another place's otherwise");
            C(PortraitLifeMath.HearthAlpha(1f, 0) <= 0.15f && PortraitLifeMath.HearthAlpha(0f, 0) >= 0.04f && PortraitLifeMath.HearthAlpha(0.5f, 3) > PortraitLifeMath.HearthAlpha(0.5f, 0),
                "a soft warm light, never the whole picture; a little stronger in the dark");
        }

        // ---------------- The records: a picture's expressions, a backdrop's depth map ----------------
        {
            var probs = new List<string>();
            var blink = Picture("P_01_v-blink", "Blink", "P_01", pawn: "Avila", problems: probs);
            C(probs.Count == 0 && blink.IsExpression && blink.variant == "blink" && blink.variantOf == "P_01", "an expression's record: the picture it belongs to, its word (the case aside)");
            var bad = Picture("P_01_v-Laugh", "Laugh", "P_01", problems: probs);
            C(probs.Count == 0 && bad.IsExpression && bad.variant == "laugh", "an expression with a word Rimfolk doesn't know yet (the tools may paint more later): kept quietly");
            Picture("P_01_v-", "", "P_01", problems: probs);
            C(probs.Any(p => p.Contains("names its expression")), "an expression with no word: told");
            probs.Clear();
            Picture("P_02", "smile", "P_02", problems: probs);
            C(probs.Any(p => p.Contains("itself")), "a picture can't be an expression of itself");
            probs.Clear();
            var old = Picture("Old_scarred", "scarred", character: "Old", problems: probs);
            C(probs.Count == 0 && !old.IsExpression && old.variant == "scarred" && old.Character == "Old", "phase 3's variant (no variantOf): the same face with a mark, as before");
            var records = new List<PortraitRecord>
            {
                Picture("P_01", pawn: "Avila"), blink, Picture("P_01_v-smile", "smile", "P_01", pawn: "Avila"), Picture("P_01_v-smile2", "smile", "P_01"), Picture("P_03"),
                Picture("Lost_v-frown", "frown", "Gone"), Picture("Chain_v-pain", "pain", "P_01_v-smile"), bad
            };
            var faces = new Dictionary<string, Dictionary<string, int>>();
            var split = new List<string>();
            var own = PortraitLibraryMath.SplitExpressions(records, faces, split);
            C(own.Select(i => records[i].name).SequenceEqual(new[] { "P_01", "P_03" }), "only the pictures of their own stand: an expression is never picked on its own");
            C(faces.Count == 1 && faces["P_01"].Keys.OrderBy(k => k).SequenceEqual(new[] { "blink", "smile" }) && records[faces["P_01"]["smile"]].name == "P_01_v-smile",
                "a picture's expressions by their words, the first of each kept");
            C(split.Any(p => p.Contains("Lost_v-frown") && p.Contains("Gone")) && split.Any(p => p.Contains("P_01_v-smile2")) && split.Any(p => p.Contains("Chain_v-pain")) && split.Count == 3,
                "told: one of a picture not in the library, a second smile, an expression of an expression; a later word left out quietly");
            var ownRecords = own.Select(i => records[i]).ToList();
            C(!ownRecords.Any(r => r.IsExpression) && ownRecords.Count(r => r.Pinned) == 1, "a pin copied onto an expression doesn't count: the colonist has one picture");
            // Counted as a variant of the character, the blink would leave "Face" with no base picture without marks.
            C(PortraitLibraryMath.LibraryProblems(new[] { Picture("A", character: "Face", variant: "scarred"), Picture("A_v", "blink", "A", character: "Face") }.ToList()).Count == 0
              && PortraitLibraryMath.LibraryProblems(new[] { Picture("A", character: "Face", variant: "scarred"), Picture("B", character: "Face", variant: "older") }.ToList()).Count == 1,
                "an expression is no variant of the character: no problem told for it, as for a real variant");
            var look = new PortraitLook { sex = PortraitSex.Male, age = PortraitAge.Adult, skin = ownRecords[0].skin, hairColour = ownRecords[0].hairColour, xenotype = "baseliner", race = "human" };
            var lacking = PortraitLibraryMath.LooksLacking(look, ownRecords, _ => false);
            C(PortraitLibraryMath.Pick(look, ownRecords, 1, _ => 0, _ => false, (_, _) => true).Any && lacking.Count == 0, "the matcher and the missing list read only the pictures of their own");
        }
        {
            var people = new List<FolderPortrait>();
            var backs = new List<FolderBackdrop>();
            var probs = new List<string>();
            PortraitFolderMath.Parse(@"<Defs>
  <ColonyCouncil.PortraitDef><defName>P_01</defName><texPath>people/p01</texPath><sex>male</sex><age>adult</age><skinColor>#c9a27e</skinColor><hairColor>#222222</hairColor></ColonyCouncil.PortraitDef>
  <ColonyCouncil.PortraitDef><defName>P_01_v-blink</defName><texPath>people/p01_v-blink</texPath><sex>male</sex><age>adult</age><skinColor>#c9a27e</skinColor><hairColor>#222222</hairColor><variant>blink</variant><variantOf>P_01</variantOf></ColonyCouncil.PortraitDef>
  <ColonyCouncil.BackdropDef><defName>B_bed</defName><texPath>backdrops/bed</texPath><depthPath>backdrops/bed_depth</depthPath><place>room</place><rooms><li>Bedroom</li></rooms><era>medieval</era></ColonyCouncil.BackdropDef>
  <ColonyCouncil.BackdropDef><defName>B_hall</defName><texPath>backdrops/hall</texPath><place>room</place><rooms><li>DiningRoom</li></rooms></ColonyCouncil.BackdropDef>
  <ColonyCouncil.BackdropDef><defName>B_gone</defName><texPath>backdrops/gone</texPath><depthPath>backdrops/gone_depth</depthPath><place>plain</place></ColonyCouncil.BackdropDef>
</Defs>", people, backs, probs);
            C(probs.Count == 0 && people.Count == 2 && people[1].record.IsExpression && people[1].record.variantOf == "P_01", "records.xml: an expression read with its picture's name");
            C(backs[0].depthPath == "backdrops/bed_depth" && backs[1].depthPath == "", "records.xml: a backdrop's depth map, none where not given");
            var files = new HashSet<string>(new[] { "backdrops/bed.png", "backdrops/bed_depth.png", "backdrops/hall.png", "backdrops/hall_depth.png", "backdrops/gone.png" }, StringComparer.OrdinalIgnoreCase);
            C(PortraitFolderMath.DepthOf(backs[0], files) == "backdrops/bed_depth", "a depth map named in the record, found");
            C(PortraitFolderMath.DepthOf(backs[1], files) == "backdrops/hall_depth", "none named: the backdrop's own name with _depth beside it");
            C(PortraitFolderMath.DepthOf(backs[2], files) == "" && PortraitFolderMath.DepthOf(backs[1], new HashSet<string>()) == "", "a depth map that isn't there: none, the backdrop kept");
        }

        // ---------------- The settings, the words and the seams ----------------
        string settings = Read("CouncilSettings.cs");
        foreach (var (field, def) in new[] { ("portraitDepth", "PortraitDepthMode.Subtle"), ("portraitFaces", "true"), ("portraitBackdropAlive", "true") })
            C(Regex.IsMatch(settings, $@"public \w+ {field} = {Regex.Escape(def)};") && settings.Contains($"Scribe_Values.Look(ref {field}, \"{field}\", {def});")
              && SettingsCatalog.Find(field)?.section == "CC_SetSec_Portraits", $"the setting {field}, {def} by default, under Portraits");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        foreach (var k in new[]
                 {
                     "CC_SetLabel_PortraitDepth", "CC_SetTip_PortraitDepth", "CC_PortraitDepth_Off", "CC_PortraitDepth_Subtle", "CC_PortraitDepth_Stronger", "CC_SetNote_PortraitDepth_Subtle",
                     "CC_Settings_PortraitFaces", "CC_Settings_PortraitFaces_Tip", "CC_SetNote_PortraitFaces", "CC_Settings_PortraitBackdropAlive", "CC_Settings_PortraitBackdropAlive_Tip",
                     "CC_SetNote_PortraitBackdropAlive", "CC_PortraitFolder_Alive"
                 })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");
        C(keyed.Contains("<CC_SetLabel_PortraitDepth>Depth that follows the mouse<") && keyed.Contains("<CC_Settings_PortraitBackdropAlive>The backdrop alive<"), "the settings by the names the brief gave them");
        string stage = Read("UI/Faces/FaceEngineStage.cs"), alive = Read("UI/Faces/FaceEngineAlive.cs"), inspect = Read("UI/Faces/InspectPortrait.cs"), lib = Read("UI/Faces/PortraitLibrary.cs");
        string backdrops = Read("UI/Faces/Backdrops.cs"), folder = Read("UI/Faces/PortraitFolder.cs"), backDef = Read("UI/Faces/BackdropDef.cs"), defSrc = Read("UI/Faces/PortraitDef.cs");
        C(inspect.Contains("FaceEngine.Hover(!dragging && Mouse.IsOver(r), mx, my);") && inspect.Contains("PortraitLifeMath.Pointer(ev.mousePosition.x"),
            "the large portrait tells where the mouse is over it, not while it is sized");
        C(stage.Contains("PortraitLifeMath.Margin(depth, w, out int mx, out int my);") && stage.Contains("ReadBackdrop(depthTex, bw, bh)")
          && stage.Contains("PortraitLifeMath.Slices(backPx, PortraitLifeMath.Grey(depthPx), bw, bh, PortraitLifeMath.LayersMost)") && stage.Contains("PortraitLifeMath.Shadow(personPx, w, h"),
            "made once on a worker: the backdrop larger by its margin, its depth map read the same, its slices cut, the shadow made");
        C(stage.Contains("PortraitStore.Run(") && Regex.Matches(stage, @"PortraitLifeMath\.Shift\(").Count >= 5 && !Regex.IsMatch(stage + alive, @"PortraitLifeMath\.Slices\([^)]*\)\s*;[^\n]*//\s*every frame"),
            "each frame the cached layers only moved");
        C(stage.Contains("+ DepthKey(depth, depthTex != null ? look.depth : null)"), "the depth's strength and map in the made picture's key, so a change of the setting makes it again");
        C(stage.Contains("StartFace(Staged s, string word, Texture2D tex)") && stage.Contains("PortraitLifeMath.FaceOverlay(px, w, h, hu, hv, hh, tr, tg, tb,") && stage.Contains("FacesKeptFor = 20f")
          && stage.Contains("GUI.DrawTextureWithTexCoords(at, s.person, uv);") && stage.Contains("PortraitLifeMath.FadeStrengths(f.before, f.now, f.faded, out float beforeA, out float nowA);"),
            "an expression's head made on a worker, one at a time, laid over the picture (the body always the picture's own); let go when no longer shown");
        C(alive.Contains("p.Awake()") && alive.Contains("hs.PainTotal") && alive.Contains("h.ageTicks") && alive.Contains("mood.thoughts?.memories?.Memories") && alive.Contains("BreakThresholdMinor")
          && !alive.Contains("[HarmonyPatch"), "what the person goes through from the game's own health, memories and mood; no new hook");
        C(alive.Contains("PortraitLifeMath.Blinking(st, Clock, p.thingIDNumber)") && alive.Contains("PortraitLifeMath.Want(") && alive.Contains("PortraitLifeMath.Step(st, life.want, why, now)"),
            "the face wanted, held and faded; the blinks on the movement clock");
        C(alive.Contains("weatherManager?.curWeather") && alive.Contains("wd.rainRate, wd.snowRate") && alive.Contains("where.place == BackdropPlace.Outdoors && where.open")
          && alive.Contains("PortraitLifeMath.Hearth(true, look.backRecord?.contents, where.fireLit)"), "the weather outdoors under the open sky by its def; a fire's light indoors");
        C(backdrops.Contains("land.open = roof == null") && backdrops.Contains("if (t is Fire) lit = true;") && backdrops.Contains("fuel != null && !fuel.HasFuel") && backdrops.Contains("flick.SwitchIsOn"),
            "open to the sky where no roof is over them; a fire lit in the room (fuel, switched on)");
        C(lib.Contains("PortraitLibraryMath.SplitExpressions(") && lib.Contains("public IReadOnlyDictionary<string, PictureFile> ExpressionsOf(string picture)"),
            "the library splits a picture's expressions off before anything is matched");
        C(backdrops.Contains("depths.Add(b.depthPath.NullOrEmpty() ? null : PortraitFolder.FileOf(") && backDef.Contains("public string depthPath;") && backDef.Contains("texPath + \"_depth\"")
          && folder.Contains("b.depthPath = PortraitFolderMath.DepthOf(b, got.pictures);"), "a backdrop's depth map from the folder and from a mod's defs");
        C(defSrc.Contains("public string variantOf;") && defSrc.Contains("case \"variantOf\": return variantOf;"), "a mod's picture defs may carry an expression too");

        // ---------------- The slicing timed ----------------
        {
            int w = 362, h = 454;
            var back = new byte[w * h * 4];
            var depth = new byte[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                back[(y * w + x) * 4 + 3] = 255;
                depth[y * w + x] = (byte)(y * 200 / h + (x > w / 2 && y < h / 3 ? 40 : 0));
            }
            var sw = Stopwatch.StartNew();
            var slices = PortraitLifeMath.Slices(back, depth, w, h, PortraitLifeMath.LayersMost);
            var shadow = PortraitLifeMath.Shadow(back, w, h, 0.4f, out _, out _);
            sw.Stop();
            Console.WriteLine($"  portraits, phase 6: a backdrop of {w} x {h} cut into {slices.Count} slices and a shadow made in {sw.ElapsedMilliseconds} ms (on a worker, once a picture)");
            C(slices.Count >= 2 && shadow.Length > 0 && sw.ElapsedMilliseconds < 1500, "the slices and the shadow of a large portrait made in well under a second");
        }
        return n;
    }
}
