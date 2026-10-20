using ColonyCouncil;

/// <summary>
/// The colonist bar's badges (BarBadgeMath): how close to a break someone is and the ring's colour for it, each kind's
/// step, which badges win when more apply than are shown (the most pressing first, a calm colonist none), and where each
/// sits on the portrait: inside it, apart from each other and clear of the game's own icons along the bottom.
/// </summary>
public static class BarBadgeTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "colonist bar badges: " + what);
        }

        // A colonist with the game's usual lines: minor 35%, major 20%, extreme 5%.
        const float minor = 0.35f, major = 0.2f, extreme = 0.05f, margin = 0.05f;

        // ---------- How close to a break ----------
        Check(BarBadgeMath.Level(0.8f, minor, major, extreme, margin) == BarBreakLevel.None, "a good mood is calm");
        Check(BarBadgeMath.Level(0.4f, minor, major, extreme, margin) == BarBreakLevel.None, "at the warning line, still calm");
        Check(BarBadgeMath.Level(0.38f, minor, major, extreme, margin) == BarBreakLevel.Near, "just above the minor line is a warning");
        Check(BarBadgeMath.Level(0.38f, minor, major, extreme, 0f) == BarBreakLevel.None, "no margin, no early warning");
        Check(BarBadgeMath.Level(minor, minor, major, extreme, 0f) == BarBreakLevel.None, "on the minor line is not under it (as the game counts)");
        Check(BarBadgeMath.Level(0.34f, minor, major, extreme, margin) == BarBreakLevel.Minor, "under the minor line");
        Check(BarBadgeMath.Level(0.19f, minor, major, extreme, margin) == BarBreakLevel.Major, "under the major line");
        Check(BarBadgeMath.Level(0.04f, minor, major, extreme, margin) == BarBreakLevel.Extreme, "under the extreme line");
        // An iron will moves the lines; the level follows the colonist's own lines.
        Check(BarBadgeMath.Level(0.3f, 0.2f, 0.1f, 0.01f, margin) == BarBreakLevel.None, "lower lines for the iron-willed: 30% is calm");
        Check(BarBadgeMath.Level(0.3f, 0.45f, 0.3f, 0.15f, margin) == BarBreakLevel.Minor, "higher lines for the nervous: 30% is a minor risk");

        float last = -1f;
        bool rising = true, inRange = true;
        for (int i = 100; i >= 0; i--)
        {
            float mood = i / 100f;
            float c = BarBadgeMath.Closeness(mood, minor, major, extreme, margin);
            if (c < last - 0.0001f) rising = false;
            if (c < 0f || c > 1f) inRange = false;
            last = c;
        }
        Check(rising, "closeness only grows as mood falls");
        Check(inRange, "closeness stays between 0 and 1");
        Check(BarBadgeMath.Closeness(0.9f, minor, major, extreme, margin) == 0f, "no closeness for a calm colonist");
        Check(BarBadgeMath.Closeness(0.01f, minor, major, extreme, margin) == 1f, "full closeness under the extreme line");
        Check(Math.Abs(BarBadgeMath.Closeness(minor - 0.0001f, minor, major, extreme, margin) - 1f / 3f) < 0.01f, "a third at the minor line");
        Check(Math.Abs(BarBadgeMath.Closeness(major - 0.0001f, minor, major, extreme, margin) - 2f / 3f) < 0.01f, "two thirds at the major line");
        Check(BarBadgeMath.Closeness(0.2f, 0.2f, 0.2f, 0.2f, 0f) >= 0f, "equal lines don't divide by nothing");

        // The ring: amber to deep red, faint to strong.
        BarBadgeMath.RingColor(0.05f, out _, out float gNear, out _, out float aNear);
        BarBadgeMath.RingColor(0.5f, out _, out float gMinor, out _, out float aMinor);
        BarBadgeMath.RingColor(0.9f, out float rMajor, out float gMajor, out _, out float aMajor);
        Check(gNear > gMinor && gMinor > gMajor, "the ring turns from amber through orange to red");
        Check(rMajor > 0.8f, "close to the edge, the ring is red");
        Check(aNear < aMinor && aMinor < aMajor, "the closer, the stronger the ring");
        Check(aNear >= 0.3f && aMajor <= 0.95f, "even the faintest ring shows, and the strongest isn't solid");
        Check(BarBadgeMath.RingAlpha(0f) == 0f, "no ring for a calm colonist");
        BarBadgeMath.RingColor(2f, out float rOver, out _, out _, out float aOver);
        Check(rOver >= 0f && rOver <= 1f && aOver <= 0.95f, "closeness past 1 is the same as 1");

        // ---------- Each kind's step ----------
        Check(BarBadgeMath.WishLevel(0.3f, 0.5f, 0) == -1, "a fresh wish has no badge");
        Check(BarBadgeMath.WishLevel(0.55f, 0.5f, 0) == 0, "past half its time, a wish shows");
        Check(BarBadgeMath.WishLevel(0.3f, 0.5f, 2) == 1, "a wish on its last call (reminded twice) always shows");
        Check(BarBadgeMath.WishLevel(0.86f, 0.9f, 0) == 1, "85% of its time gone is a last call, whatever the threshold");
        Check(BarBadgeMath.WishLevel(0.3f, 0.25f, 0) == 0, "a lower threshold shows wishes sooner");
        Check(BarBadgeMath.StrainLevel(0) == -1 && BarBadgeMath.StrainLevel(1) == -1, "steady and strained have no badge");
        Check(BarBadgeMath.StrainLevel(2) == 0 && BarBadgeMath.StrainLevel(3) == 1, "worn down, then near breaking");
        Check(BarBadgeMath.FaithLevel(0.7f, 0.6f) == -1, "a firm faith has no badge");
        Check(BarBadgeMath.FaithLevel(0.45f, 0.6f) == 0, "under 60% faith wavers");
        Check(BarBadgeMath.FaithLevel(0.2f, 0.6f) == 1, "far under it, faith barely holds");

        // ---------- What wins ----------
        // The whole order, most pressing first.
        var order = new[]
        {
            new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Extreme),
            new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Major),
            new BarBadge(BarBadgeKind.Leaving, 0),
            new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Minor),
            new BarBadge(BarBadgeKind.Wish, 1),
            new BarBadge(BarBadgeKind.Strain, 1),
            new BarBadge(BarBadgeKind.Wish, 0),
            new BarBadge(BarBadgeKind.Promise, 1),
            new BarBadge(BarBadgeKind.Promise, 0),
            new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Near),
            new BarBadge(BarBadgeKind.Strain, 0),
            new BarBadge(BarBadgeKind.Faith, 1),
            new BarBadge(BarBadgeKind.Faith, 0)
        };
        for (int i = 1; i < order.Length; i++)
            Check(order[i - 1].Priority > order[i].Priority, $"{order[i - 1]} comes before {order[i]}");
        foreach (var b in order) Check(b.Priority > 0, $"{b} has a place in the order");
        Check(BarBadgeMath.Priority(BarBadgeKind.Break, (int)BarBreakLevel.None) == 0, "no break risk, no place");

        // The player's rule: close to breaking or about to leave before a long-waiting wish, before wavering faith.
        foreach (var urgent in new[] { new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Minor), new BarBadge(BarBadgeKind.Leaving, 0) })
        {
            Check(urgent.Priority > new BarBadge(BarBadgeKind.Wish, 1).Priority, $"{urgent} before even a wish on its last call");
            Check(urgent.Priority > new BarBadge(BarBadgeKind.Faith, 1).Priority, $"{urgent} before wavering faith");
        }
        Check(new BarBadge(BarBadgeKind.Wish, 0).Priority > new BarBadge(BarBadgeKind.Faith, 1).Priority, "a long-waiting wish before wavering faith");

        // Sorting: in place, most pressing first, at most the setting's number (1 to 3).
        var badges = new BarBadge[6];
        badges[0] = new BarBadge(BarBadgeKind.Faith, 0);
        badges[1] = new BarBadge(BarBadgeKind.Wish, 0);
        badges[2] = new BarBadge(BarBadgeKind.Leaving, 0);
        int shown = BarBadgeMath.Order(badges, 3, 2);
        Check(shown == 2, $"two of three shown with a most of two (got {shown})");
        Check(badges[0].kind == BarBadgeKind.Leaving && badges[1].kind == BarBadgeKind.Wish && badges[2].kind == BarBadgeKind.Faith,
            $"leaving, then the wish; wavering faith is left for the tooltip (got {badges[0]}, {badges[1]}, {badges[2]})");
        Check(BarBadgeMath.Order(badges, 3, 0) == 1, "a most of 0 still shows one");
        Check(BarBadgeMath.Order(badges, 3, 9) == 3, "never more than three");
        Check(BarBadgeMath.Order(badges, 0, 3) == 0, "a calm colonist shows nothing");
        Check(BarBadgeMath.Order(null, 2, 3) == 0, "no list, nothing shown");
        // Equal priorities keep the kinds' order, whatever order they were found in.
        var ties = new[] { new BarBadge(BarBadgeKind.Promise, 0), new BarBadge(BarBadgeKind.Wish, 0), new BarBadge(BarBadgeKind.Strain, 0) };
        BarBadgeMath.Order(ties, 3, 3);
        Check(ties[0].kind == BarBadgeKind.Wish && ties[1].kind == BarBadgeKind.Promise && ties[2].kind == BarBadgeKind.Strain, "sorted by how pressing each is");
        // Every arrangement of six badges sorts the same way.
        var kinds = new[]
        {
            new BarBadge(BarBadgeKind.Break, (int)BarBreakLevel.Near), new BarBadge(BarBadgeKind.Leaving, 0), new BarBadge(BarBadgeKind.Wish, 0),
            new BarBadge(BarBadgeKind.Strain, 1), new BarBadge(BarBadgeKind.Faith, 0), new BarBadge(BarBadgeKind.Promise, 1)
        };
        var rng = new Random(7);
        bool same = true;
        string first = null;
        for (int n = 0; n < 200; n++)
        {
            var shuffled = kinds.OrderBy(_ => rng.Next()).ToArray();
            BarBadgeMath.Order(shuffled, shuffled.Length, 3);
            string seq = string.Join(",", shuffled.Select(b => b.ToString()));
            first ??= seq;
            if (seq != first) same = false;
        }
        Check(same, $"the order doesn't depend on the order found ({first})");
        Check(first != null && first.StartsWith("Leaving"), "about to leave leads over strain near breaking and a long wait");

        // ---------- Where they sit ----------
        Check(BarBadgeMath.SizeFor(BarBadgeSize.Small, 1f) < BarBadgeMath.SizeFor(BarBadgeSize.Normal, 1f)
              && BarBadgeMath.SizeFor(BarBadgeSize.Normal, 1f) < BarBadgeMath.SizeFor(BarBadgeSize.Large, 1f), "small, normal, large");
        float normal = BarBadgeMath.SizeFor(BarBadgeSize.Normal, 1f);
        Check(normal >= 12f && normal <= 14f, $"a normal badge is about 12 to 14 pixels at full size ({normal})");
        Check(BarBadgeMath.SizeFor(BarBadgeSize.Small, 0.3f) == BarBadgeMath.MinSize, "never smaller than the least size");
        Check(BarBadgeMath.SizeFor(BarBadgeSize.Normal, 0.5f) < normal, "badges shrink with the bar");
        Check(BarBadgeMath.ColumnFits(48f, normal, BarBadgeMath.GameIconRow) == 2, "two normal badges fit down the side above the game's icons");
        Check(BarBadgeMath.ColumnFits(48f, BarBadgeMath.SizeFor(BarBadgeSize.Large, 1f), BarBadgeMath.GameIconRow) == 1, "one large badge fits down the side");
        Check(BarBadgeMath.ColumnFits(10f, 13f, 21f) == 1, "at least one, however crowded");

        foreach (BarBadgeCorner corner in Enum.GetValues(typeof(BarBadgeCorner)))
        foreach (BarBadgeSize size in Enum.GetValues(typeof(BarBadgeSize)))
        foreach (float scale in new[] { 1f, 0.8f, 0.6f, 0.45f })
        {
            float w = BarBadgeMath.BaseBarSize * scale, h = w, x = 100f, y = 20f;
            float px = BarBadgeMath.SizeFor(size, scale), row = BarBadgeMath.GameIconRow * scale;
            int column = BarBadgeMath.ColumnFits(h, px, row);
            int fits = BarBadgeMath.Capacity(w, h, px, row);
            string what = $"{corner}, {size}, scale {scale}";
            Check(fits >= 1, $"{what}: at least one badge fits");
            if (scale >= 0.6f) Check(fits >= 2, $"{what}: two badges fit ({fits})");
            var placed = new List<(float x, float y)>();
            for (int i = 0; i < Math.Min(fits, BarBadgeMath.MaxShown); i++)
            {
                BarBadgeMath.Slot(i, corner, x, y, w, h, px, row, out float sx, out float sy);
                Check(sx >= x - 0.01f && sy >= y - 0.01f && sx + px <= x + w + 0.01f && sy + px <= y + h + 0.01f, $"{what}: badge {i} stays on the portrait");
                if (i < column) Check(sy + px <= y + h - row + 0.01f, $"{what}: badge {i} down the side stays clear of the game's icons");
                foreach (var o in placed)
                    Check(Math.Abs(o.x - sx) >= px - 0.01f || Math.Abs(o.y - sy) >= px - 0.01f, $"{what}: badge {i} doesn't cover another");
                placed.Add((sx, sy));
            }
            BarBadgeMath.Slot(0, corner, x, y, w, h, px, row, out float fx, out float fy);
            bool right = corner == BarBadgeCorner.TopRight || corner == BarBadgeCorner.BottomRight;
            bool top = corner == BarBadgeCorner.TopRight || corner == BarBadgeCorner.TopLeft;
            Check(right ? fx + px >= x + w - 2f : fx <= x + 2f, $"{what}: the first badge sits at the chosen side");
            Check(top ? fy <= y + 2f : fy + px >= y + h - row - 0.01f, $"{what}: the first badge sits at the chosen end");
        }
        // The default (top right, normal, two): two down the right side, clear of the head in the middle.
        BarBadgeMath.Slot(1, BarBadgeCorner.TopRight, 0f, 0f, 48f, 48f, normal, BarBadgeMath.GameIconRow, out float x1, out float y1);
        Check(x1 >= 48f - normal - 1.01f && y1 > 0f, "by default the second badge sits under the first, along the right edge");

        return cases;
    }
}
