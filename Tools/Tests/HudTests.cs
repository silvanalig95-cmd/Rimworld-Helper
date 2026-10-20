using ColonyCouncil;

/// <summary>
/// The on-screen panels (Source/ColonyCouncil/Hud/): the shared ranking that orders the mood board, picks the hover
/// card's lines and colours the vitals (life and limb first, then what will soon be a problem, then what's worth
/// knowing, by size); and the layout maths: places kept as fractions of the screen from the nearest corner, kept on
/// the screen at every resolution, snapping to edges and other panels, the rows kept when space is short, the trend's
/// samples.
/// </summary>
public static class HudTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "on-screen panels: " + what);
        }

        // ---------- The ranking ----------
        const float minor = 0.35f, major = 0.2f, extreme = 0.05f, margin = 0.05f;
        float Mood(float now, float heading = -1f, bool inBreak = false) =>
            HudRank.MoodRisk(now, heading < 0f ? now : heading, minor, major, extreme, inBreak, margin);

        // The player's three examples.
        float bleeding = HudRank.Bleeding(30f);
        Check(bleeding > Mood(0.30f) && bleeding > Mood(0.25f) && bleeding > Mood(0.1f), "a bleeding colonist outranks a sad one, even one under the major line");
        Check(Mood(minor + 0.005f) > Mood(minor + 0.20f), "a colonist at the break line outranks one 20 points above it");
        Check(HudRank.Drag(8f, true) > HudRank.Drag(2f, false), "a large fixable drag outranks a small passing one");
        Check(HudRank.Drag(8f, true) > HudRank.Drag(2f, true) && HudRank.Drag(8f, false) > HudRank.Drag(2f, false), "a bigger drag outranks a smaller one of its kind");
        Check(HudRank.Drag(8f, true) > HudRank.Drag(8f, false), "a drag that can be fixed outranks one the same size that passes on its own");

        // The tiers.
        Check(HudRank.LevelOf(bleeding) == HudLevel.Life && HudRank.LevelOf(HudRank.Bleeding(1f)) == HudLevel.Life, "bleeding is life and limb");
        Check(HudRank.Bleeding(1f) > HudRank.Bleeding(10f) && HudRank.Bleeding(10f) > HudRank.Bleeding(40f), "bleeding out sooner outranks bleeding out later");
        Check(HudRank.LevelOf(HudRank.Dangerous(0.2f)) == HudLevel.Life && HudRank.Dangerous(0.9f) > HudRank.Dangerous(0.2f), "a dangerous condition is life and limb, worse as it goes on");
        Check(HudRank.LevelOf(HudRank.Starving(0f)) == HudLevel.Life, "starving is life and limb");
        Check(HudRank.LevelOf(HudRank.InBreak) == HudLevel.Life && HudRank.LevelOf(Mood(0.5f, inBreak: true)) == HudLevel.Life, "a mental break is life and limb");
        Check(HudRank.LevelOf(Mood(minor + 0.01f)) == HudLevel.Life && HudRank.LevelOf(Mood(minor - 0.05f)) == HudLevel.Life, "right at or under the break line is life and limb");
        Check(Mood(0.1f) > Mood(0.25f) && Mood(0.25f) > Mood(0.30f) && Mood(0.01f) > Mood(0.1f), "deeper under the lines ranks higher");
        // The order the player gave: bleeding, a dangerous condition, starvation, a mental break, the break line.
        Check(HudRank.Bleeding(40f) > HudRank.Dangerous(1f) && HudRank.Dangerous(0f) > HudRank.Starving(1f) && HudRank.Starving(0f) > HudRank.InBreak
              && HudRank.InBreak > Mood(0.01f), "life and limb in the player's order");
        // Soon: falling towards a break, food running out, a wish about to lapse.
        Check(HudRank.LevelOf(Mood(0.45f, 0.30f)) == HudLevel.Soon, "falling towards a break is soon");
        Check(Mood(0.40f, 0.30f) > Mood(0.40f), "heading under the line outranks steady at the same mood");
        Check(HudRank.LevelOf(Mood(minor + 0.03f)) == HudLevel.Soon && HudRank.LevelOf(Mood(minor + 0.2f)) == HudLevel.Quiet, "within the margin is soon; well above it is quiet");
        Check(HudRank.LevelOf(HudRank.Wish(0.1f, 1)) == HudLevel.Soon && HudRank.LevelOf(HudRank.Wish(0.6f, 2)) == HudLevel.Soon, "a wish about to lapse, or at its last call, is soon");
        Check(HudRank.LevelOf(HudRank.Wish(0.4f, 0)) == HudLevel.Worth && HudRank.LevelOf(HudRank.Wish(0.9f, 0)) == HudLevel.Worth, "a wish waiting is worth knowing");
        Check(HudRank.LevelOf(HudRank.Downed) == HudLevel.Soon && HudRank.LevelOf(HudRank.Exhausted) == HudLevel.Soon && HudRank.LevelOf(HudRank.Hungry) == HudLevel.Soon,
            "downed, hungry and exhausted are soon");
        Check(HudRank.LevelOf(HudRank.FaithWavering) == HudLevel.Worth && HudRank.LevelOf(HudRank.Strained) == HudLevel.Worth, "wavering faith and strain are worth knowing");
        foreach (float size in new[] { 0.5f, 3f, 10f, 30f, 200f })
        foreach (bool fix in new[] { false, true })
            Check(HudRank.LevelOf(HudRank.Drag(size, fix)) == HudLevel.Worth, $"a drag of {size} is worth knowing, never more");
        Check(HudRank.Drag(0f, true) == 0f, "no drag, nothing to say");
        // Every life-and-limb score outranks every soon one, and every soon one every worth-knowing one.
        float[] life = { HudRank.Bleeding(48f), HudRank.Dangerous(0f), HudRank.Starving(0f), HudRank.InBreak, Mood(minor + 0.019f), Mood(0.3f), HudRank.Food(0f, 10f, 4f),
            HudRank.Medicine(0, 5, true, 2), HudRank.Power(-100f, 0f), HudRank.Temperature(-40f) };
        float[] soon = { Mood(0.45f, 0.1f), Mood(minor + 0.021f), HudRank.Downed, HudRank.StrainBreaking, HudRank.Wish(0f, 2), HudRank.Hungry, HudRank.Exhausted,
            HudRank.StrainWorn, HudRank.Food(4.01f, 10f, 4f), HudRank.Food(9.9f, 10f, 4f), HudRank.Medicine(0, 5, false, 2), HudRank.Medicine(9, 5, false, 2),
            HudRank.Power(-100f, 50f), HudRank.Temperature(-20f), HudRank.Question(2f) };
        float[] worth = { HudRank.Drag(200f, true), HudRank.Wish(0.4f, 0), HudRank.FaithWavering, HudRank.Strained, HudRank.Research(false, true), HudRank.Question(48f) };
        Check(life.Min() > soon.Max(), $"every life-and-limb line outranks every soon one ({life.Min():0.#} > {soon.Max():0.#})");
        Check(soon.Min() > worth.Max(), $"every soon line outranks every worth-knowing one ({soon.Min():0.#} > {worth.Max():0.#})");
        Check(worth.Min() >= HudRank.WorthFloor, "worth-knowing lines are on the panels");
        // The vitals.
        Check(HudRank.LevelOf(HudRank.Food(3f, 10f, 4f)) == HudLevel.Life && HudRank.LevelOf(HudRank.Food(6f, 10f, 4f)) == HudLevel.Soon
              && HudRank.LevelOf(HudRank.Food(12f, 10f, 4f)) == HudLevel.Quiet, "food: red under the bad line, orange under the warning line, quiet above");
        Check(HudRank.Food(1f, 10f, 4f) > HudRank.Food(3f, 10f, 4f) && HudRank.Food(5f, 10f, 4f) > HudRank.Food(8f, 10f, 4f), "less food ranks higher");
        Check(HudRank.LevelOf(HudRank.Medicine(10, 5, false, 2)) == HudLevel.Quiet && HudRank.LevelOf(HudRank.Medicine(9, 5, false, 2)) == HudLevel.Soon,
            "medicine: quiet at the advisor's two a colonist, orange under it");
        Check(HudRank.Medicine(0, 5, true, 2) > HudRank.Medicine(0, 5, false, 2), "no medicine with someone to tend is worse");
        Check(HudRank.Medicine(0, 0, true, 2) == 0f, "no colonists, nothing to say about medicine");
        Check(HudRank.LevelOf(HudRank.Power(300f, 0f)) == HudLevel.Quiet && HudRank.LevelOf(HudRank.Power(-100f, 0f)) == HudLevel.Life
              && HudRank.LevelOf(HudRank.Power(-1000f, 500f)) == HudLevel.Soon && HudRank.LevelOf(HudRank.Power(-100f, 500f)) == HudLevel.Quiet,
            "power: a blackout is red, batteries running out within a day orange, more than a day quiet");
        Check(HudRank.LevelOf(HudRank.Temperature(15f)) == HudLevel.Quiet && HudRank.LevelOf(HudRank.Temperature(45f)) == HudLevel.Soon
              && HudRank.LevelOf(HudRank.Temperature(-35f)) == HudLevel.Life, "temperature: mild is quiet, harsh is orange, deadly is red");
        Check(HudRank.Question(1f) > HudRank.Question(20f) && HudRank.LevelOf(HudRank.Question(-1f)) == HudLevel.Worth, "a question about to go unanswered ranks higher; one without a deadline is worth knowing");

        // ---------- Places as fractions of the screen ----------
        var screens = new (float w, float h)[] { (1280f, 720f), (1366f, 768f), (1600f, 900f), (1920f, 1080f), (2560f, 1440f), (3840f, 2160f) };
        float[] scales = { 1f, 1.25f, 1.5f, 2f };
        var rnd = new Random(7);
        foreach (var (pw, ph) in screens)
        foreach (var ui in scales)
        {
            float sw = pw / ui, sh = ph / ui;
            for (int i = 0; i < 40; i++)
            {
                float w = 60f + (float)rnd.NextDouble() * 300f, h = 20f + (float)rnd.NextDouble() * 300f;
                float x = (float)rnd.NextDouble() * (sw - w), y = (float)rnd.NextDouble() * (sh - h);
                var b = new HudBox(x, y, w, h);
                HudMath.ToFraction(b, sw, sh, out float fx, out float fy, out bool right, out bool bottom);
                Check(fx >= 0f && fx <= 1f && fy >= 0f && fy <= 1f, "fractions are between 0 and 1");
                var back = HudMath.FromFraction(fx, fy, right, bottom, w, h, sw, sh);
                Check(Math.Abs(back.x - x) < 0.01f && Math.Abs(back.y - y) < 0.01f, $"a place comes back where it was ({sw:0}x{sh:0})");
                // The same fractions on another screen: still on it, and still against the same edges.
                foreach (var (ow, oh) in screens)
                {
                    var moved = HudMath.FromFraction(fx, fy, right, bottom, w, h, ow, oh);
                    Check(moved.x >= 0f && moved.y >= 0f && (w > ow || moved.XMax <= ow + 0.01f) && (h > oh || moved.YMax <= oh + 0.01f),
                        $"a panel stays on a {ow}x{oh} screen");
                }
            }
            // Against an edge, it stays against it at any size and resolution.
            var corner = new HudBox(sw - 200f, sh - 120f, 200f, 120f);
            HudMath.ToFraction(corner, sw, sh, out float cx, out float cy, out bool cr, out bool cb);
            Check(cr && cb && Math.Abs(cx - 1f) < 0.001f && Math.Abs(cy - 1f) < 0.001f, "a panel in the bottom-right corner is kept by its bottom-right corner");
            var grown = HudMath.FromFraction(cx, cy, cr, cb, 260f, 300f, sw, sh);
            Check(Math.Abs(grown.XMax - sw) < 0.01f && Math.Abs(grown.YMax - sh) < 0.01f, "a panel against the bottom and right edges grows away from them");
            var left = new HudBox(0f, 100f, 200f, 120f);
            HudMath.ToFraction(left, sw, sh, out float lx, out float ly, out bool lr, out bool lb);
            var tall = HudMath.FromFraction(lx, ly, lr, lb, 200f, 400f, sw, sh);
            Check(!lr && !lb && tall.x == 0f && (500f > sh || Math.Abs(tall.y - 100f) < 0.01f), "a panel high on the left grows downwards from where it is");
        }
        // Kept on the screen.
        var off = HudMath.OnScreen(new HudBox(-50f, 2000f, 200f, 100f), 1920f, 1080f);
        Check(off.x == 0f && Math.Abs(off.YMax - 1080f) < 0.01f, "a panel off the screen is brought back onto it");
        var huge = HudMath.OnScreen(new HudBox(300f, 300f, 3000f, 2000f), 1920f, 1080f);
        Check(huge.x == 0f && huge.y == 0f, "a panel bigger than the screen keeps its top-left corner on it");

        // ---------- Snapping ----------
        var others = new List<HudBox> { new HudBox(500f, 200f, 200f, 150f) };
        var s1 = HudMath.Snap(new HudBox(5f, 400f, 100f, 100f), 1920f, 1080f, others);
        Check(s1.x == 0f && s1.y == 400f, "snaps to the left edge within 8 px, and not up or down");
        var s2 = HudMath.Snap(new HudBox(1815f, 1000f, 100f, 76f), 1920f, 1080f, others);
        Check(Math.Abs(s2.XMax - 1920f) < 0.01f && Math.Abs(s2.YMax - 1080f) < 0.01f, "snaps to the right and bottom edges");
        var s3 = HudMath.Snap(new HudBox(706f, 260f, 100f, 50f), 1920f, 1080f, others);
        Check(Math.Abs(s3.x - 700f) < 0.01f, "snaps beside another panel, edge to edge");
        var s4 = HudMath.Snap(new HudBox(504f, 356f, 150f, 60f), 1920f, 1080f, others);
        Check(Math.Abs(s4.x - 500f) < 0.01f && Math.Abs(s4.y - 350f) < 0.01f, "snaps under another panel, lined up with its left edge");
        var s5 = HudMath.Snap(new HudBox(720f, 260f, 100f, 50f), 1920f, 1080f, others);
        Check(Math.Abs(s5.x - 720f) < 0.01f, "doesn't snap from further than 8 px");
        var s6 = HudMath.Snap(new HudBox(706f, 600f, 100f, 50f), 1920f, 1080f, others);
        Check(Math.Abs(s6.x - 706f) < 0.01f, "doesn't snap to a panel it isn't beside");
        var s7 = HudMath.Snap(new HudBox(3f, 3f, 100f, 50f), 1920f, 1080f, null);
        Check(s7.x == 0f && s7.y == 0f, "snaps into a corner");
        Check(HudMath.HeaderAbove(new HudBox(10f, 40f, 100f, 100f), 18f) && !HudMath.HeaderAbove(new HudBox(10f, 5f, 100f, 100f), 18f),
            "the bar goes above a panel, or below it at the top of the screen");

        // ---------- Rows when space is short ----------
        var scores = new List<float> { 50f, 320f, 10f, 250f, 360f, 110f, 330f };
        var keep = HudMath.Keep(scores, 2, HudRank.LifeFloor, out int hidden);
        Check(keep.SequenceEqual(new[] { 1, 4, 6 }) && hidden == 4, "the critical rows always show, even beyond the limit, in their order");
        keep = HudMath.Keep(scores, 5, HudRank.LifeFloor, out hidden);
        Check(keep.SequenceEqual(new[] { 1, 3, 4, 5, 6 }) && hidden == 2, "then the most important of the rest, keeping the rows' order");
        keep = HudMath.Keep(scores, 0, HudRank.LifeFloor, out hidden);
        Check(keep.Count == scores.Count && hidden == 0, "no limit shows every row");
        keep = HudMath.Keep(scores, 20, HudRank.LifeFloor, out hidden);
        Check(keep.Count == scores.Count && hidden == 0, "a limit above the count shows every row");
        keep = HudMath.Keep(new List<float> { 10f, 10f, 10f, 10f }, 2, HudRank.LifeFloor, out hidden);
        Check(keep.SequenceEqual(new[] { 0, 1 }) && hidden == 2, "equal rows: the first ones");
        keep = HudMath.Keep(new List<float>(), 3, HudRank.LifeFloor, out hidden);
        Check(keep.Count == 0 && hidden == 0, "no rows, nothing hidden");
        for (int n = 1; n <= 30; n++)
        {
            int cols = HudMath.GridColumns(n, 5);
            Check(cols >= 1 && cols <= 5 && cols <= n, $"the compact grid for {n} has 1 to 5 columns ({cols})");
        }

        // ---------- The trend's samples ----------
        foreach (int hour in new[] { 0, 1, 24, 7199, 150000 })
        foreach (float a in new[] { 0f, 0.354f, 0.5f, 1f })
        foreach (float l in new[] { 0f, 0.12f, 0.99f })
        {
            HudMath.Unpack(HudMath.Pack(hour, a, l), out int h2, out float a2, out float l2);
            Check(h2 == hour && Math.Abs(a2 - Math.Min(0.99f, (float)Math.Round(a * 100f) / 100f)) < 0.001f && Math.Abs(l2 - Math.Min(0.99f, (float)Math.Round(l * 100f) / 100f)) < 0.001f,
                $"a sample keeps its hour and values ({hour}, {a}, {l})");
        }
        Check(HudMath.Pack(int.MaxValue / 10000 - 1, 1f, 1f) > 0, "a long game's hours still fit");
        var hours = new List<int> { 10, 11, 12, 20 };
        Check(HudMath.Nearest(hours, 11.4f) == 1 && HudMath.Nearest(hours, 17f) == 3 && HudMath.Nearest(new List<int>(), 3f) == -1, "the point nearest the mouse");
        Check(HudMath.GraphY(1f, 10f, 50f) == 10f && HudMath.GraphY(0f, 10f, 50f) == 60f && HudMath.GraphY(2f, 10f, 50f) == 10f, "values draw within the graph");
        Check(HudMath.ClampScale(0.1f) == HudMath.MinScale && HudMath.ClampScale(9f) == HudMath.MaxScale && Math.Abs(HudMath.ClampScale(1.12f) - 1.1f) < 0.001f,
            "sizes stay between 75% and 150%, in steps of 5%");
        Check(HudMath.ClampOpacity(0f) == HudMath.MinOpacity && HudMath.ClampOpacity(float.NaN) == 1f, "a background never goes fully away");
        return cases;
    }
}
