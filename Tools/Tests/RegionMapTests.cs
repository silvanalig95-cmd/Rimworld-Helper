using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;
using static ColonyCouncil.RegionMapMath;

/// <summary>
/// The region map (Source/ColonyCouncil/World/RegionMapMath.cs): the land laid flat around home at its true distance
/// and bearing, east to the right and north up as the game's longitude and latitude run; the tiles in the square;
/// tiles filled without gaps or overlaps; rivers and roads; zooming and panning; the land's colours; the plain words
/// for how a people stands with the colony.
/// </summary>
public static class RegionMapTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "region map: " + what);
        }

        Flat(Check);
        Tiles(Check);
        Picture(Check);
        Views(Check);
        Colours(Check);
        Standings(Check);
        return n;
    }

    private static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol;

    // ---------------- Laid flat ----------------

    private static void Flat(Action<bool, string> Check)
    {
        const double radius = 100, tile = 0.9;
        var origin = new V3(0, 0, 0);
        var pole = new V3(0, radius, 0);
        foreach (var (lon, lat) in new[] { (0.0, 0.0), (30.0, 20.0), (-120.0, -45.0), (170.0, 70.0), (60.0, -85.0), (10.0, 90.0) })
        {
            var home = FromLongLat(lon, lat, radius);
            var b = BasisAt(home, origin, pole);
            string at = $"at {lon},{lat}";
            Check(Near(b.up.Length, 1) && Near(b.east.Length, 1) && Near(b.north.Length, 1), $"the frame is of unit length {at}");
            Check(Near(b.up.Dot(b.east), 0) && Near(b.up.Dot(b.north), 0) && Near(b.east.Dot(b.north), 0), $"the frame is square {at}");
            Project(b, home, origin, radius, tile, out double hx, out double hy);
            Check(Near(hx, 0) && Near(hy, 0), $"home is in the middle {at}");
            if (Math.Abs(lat) < 89)
            {
                // The game's longitude grows to the east, its latitude to the north.
                Project(b, FromLongLat(lon + 1, lat, radius), origin, radius, tile, out double ex, out double ey);
                Check(ex > 0 && Math.Abs(ey) < ex * 0.1, $"a degree of longitude more is to the east {at} ({ex:0.00},{ey:0.00})");
                Project(b, FromLongLat(lon, lat + 1, radius), origin, radius, tile, out double nx, out double ny);
                Check(ny > 0 && Math.Abs(nx) < 1e-6, $"a degree of latitude more is to the north {at} ({nx:0.00},{ny:0.00})");
                // A degree of latitude is the same distance anywhere.
                Check(Near(ny, radius * Math.PI / 180 / tile, 1e-6), $"a degree north is its distance along the ground {at}");
            }
            // Every point keeps its distance and bearing from home.
            bool kept = true;
            for (int bearing = 0; bearing < 360; bearing += 30)
            for (double tiles = 1; tiles <= 61; tiles += 15)
            {
                double theta = tiles * tile / radius, beta = bearing * Math.PI / 180;
                var dir = b.east * Math.Sin(beta) + b.north * Math.Cos(beta);
                var p = origin + (b.up * Math.Cos(theta) + dir * Math.Sin(theta)) * radius;
                Project(b, p, origin, radius, tile, out double x, out double y);
                if (!Near(x, tiles * Math.Sin(beta), 1e-6) || !Near(y, tiles * Math.Cos(beta), 1e-6)) kept = false;
            }
            Check(kept, $"every place keeps its distance and bearing from home {at}");
        }
        // A planet whose centre isn't at zero (another layer) is the same.
        var o2 = new V3(5, -3, 2);
        var h2 = o2 + FromLongLat(40, 10, 50);
        var b2 = BasisAt(h2, o2, o2 + new V3(0, 50, 0));
        Project(b2, o2 + FromLongLat(40, 11, 50), o2, 50, 1, out _, out double y2);
        Check(Near(y2, 50 * Math.PI / 180), "a planet away from the origin is laid flat the same way");
    }

    // ---------------- The tiles in the square ----------------

    /// <summary>A flat hex grid: tile id from axial coordinates, centres a tile apart.</summary>
    private sealed class HexGrid
    {
        public readonly int size;
        public HexGrid(int size) => this.size = size;
        public int Id(int q, int r) => (q + size) * (2 * size + 1) + (r + size);
        public (int q, int r) Of(int id) => (id / (2 * size + 1) - size, id % (2 * size + 1) - size);
        public bool Has(int q, int r) => Math.Abs(q) <= size && Math.Abs(r) <= size;

        public (double x, double y) Centre(int id)
        {
            var (q, r) = Of(id);
            return (q + r / 2.0, r * Math.Sqrt(3) / 2);
        }

        private static readonly (int dq, int dr)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1) };

        public void Neighbours(int id, List<int> outList)
        {
            var (q, r) = Of(id);
            foreach (var (dq, dr) in Steps)
                if (Has(q + dq, r + dr)) outList.Add(Id(q + dq, r + dr));
        }

        public IEnumerable<int> All()
        {
            for (int q = -size; q <= size; q++)
            for (int r = -size; r <= size; r++)
                yield return Id(q, r);
        }
    }

    private static void Tiles(Action<bool, string> Check)
    {
        var grid = new HexGrid(110);
        int home = grid.Id(0, 0);
        foreach (int reach in new[] { 20, 35, 60 })
        {
            double half = Half(reach);
            bool Inside(int id)
            {
                var (x, y) = grid.Centre(id);
                return InSquare(x, y, half);
            }
            var region = Region(home, grid.Neighbours, Inside, 100000);
            var brute = grid.All().Where(Inside).ToHashSet();
            Check(region.Count > 0 && region[0] == home, $"home comes first ({reach})");
            Check(region.Count == brute.Count && region.All(brute.Contains), $"every tile in the square and no other is drawn ({reach}: {region.Count} of {brute.Count})");
            Check(region.Distinct().Count() == region.Count, $"no tile twice ({reach})");
            // The square with the corners: more than a round reach would hold.
            Check(region.Count > Math.PI * reach * reach * 2 / Math.Sqrt(3), $"the corners are drawn too ({reach})");
            Check(region.Count <= 65000, $"the tiles fit the picture's index ({reach}: {region.Count})");
            var few = Region(home, grid.Neighbours, Inside, 50);
            Check(few.Count == 50, "never more tiles than asked for");
        }
        // The edge of a world that doesn't cover the planet: the tiles that are there.
        var small = new HexGrid(8);
        var cut = Region(small.Id(0, 0), small.Neighbours, id => true, 100000);
        Check(cut.Count == small.All().Count(), "at the edge of the world, the tiles that exist");
    }

    // ---------------- The picture ----------------

    private static void Picture(Action<bool, string> Check)
    {
        for (int reach = 20; reach <= 60; reach += 5)
        {
            int ppt = PixelsPerTile(reach), size = PictureSize(reach);
            Check(ppt >= 10 && ppt <= 20, $"{ppt} pixels a tile at a reach of {reach}");
            Check(size <= MaxPicture && size >= 2 * Half(reach) * ppt, $"the picture is {size} pixels at a reach of {reach}, the whole square and no bigger than {MaxPicture}");
        }

        // A box covers the pixels whose centres are inside it.
        int count = 0, minC = 99, maxC = -1, minR = 99, maxR = -1;
        Spans(new[] { 2.0, 5.0, 5.0, 2.0 }, new[] { 3.0, 3.0, 7.0, 7.0 }, 4, 20, 20, (row, c0, c1) =>
        {
            count += c1 - c0 + 1;
            minC = Math.Min(minC, c0);
            maxC = Math.Max(maxC, c1);
            minR = Math.Min(minR, row);
            maxR = Math.Max(maxR, row);
        });
        Check(count == 12 && minC == 2 && maxC == 4 && minR == 3 && maxR == 6, $"a 3 by 4 box is 12 pixels ({count}, cols {minC}-{maxC}, rows {minR}-{maxR})");
        int clipped = 0;
        Spans(new[] { -5.0, 30.0, 30.0, -5.0 }, new[] { -5.0, -5.0, 30.0, 30.0 }, 4, 10, 8, (row, c0, c1) =>
        {
            if (row < 0 || row >= 8 || c0 < 0 || c1 >= 10) clipped = -1000;
            clipped += c1 - c0 + 1;
        });
        Check(clipped == 80, $"a shape bigger than the picture fills it and stays inside it ({clipped})");

        // Hexes side by side share no pixel and leave no gap, at any spacing and offset.
        foreach (var (spacing, ox, oy) in new[] { (16.0, 0.0, 0.0), (12.0, 0.37, 0.81), (19.5, 0.5, 0.5), (10.0, 0.25, 0.0) })
        {
            const int w = 260, h = 260;
            var hits = new int[w * h];
            var xs = new double[6];
            var ys = new double[6];
            for (int q = -16; q <= 16; q++)
            for (int r = -16; r <= 16; r++)
            {
                double cx = w / 2.0 + ox + spacing * (q + r / 2.0), cy = h / 2.0 + oy + spacing * r * Math.Sqrt(3) / 2;
                HexCorners(cx, cy, spacing, xs, ys);
                // Neighbours share their corners exactly, as the game's tiles share one list of corners.
                for (int i = 0; i < 6; i++)
                {
                    xs[i] = Math.Round(xs[i] * 4096) / 4096;
                    ys[i] = Math.Round(ys[i] * 4096) / 4096;
                }
                Spans(xs, ys, 6, w, h, (row, c0, c1) =>
                {
                    for (int c = c0; c <= c1; c++) hits[row * w + c]++;
                });
            }
            bool twice = hits.Any(v => v > 1);
            // The middle of the picture is all covered (the grid reaches well past it).
            bool gap = false;
            for (int row = 60; row < 200; row++)
            for (int c = 60; c < 200; c++)
                if (hits[row * w + c] == 0) gap = true;
            Check(!twice, $"no pixel belongs to two hexes (spacing {spacing}, offset {ox},{oy})");
            Check(!gap, $"no pixel between hexes is left out (spacing {spacing}, offset {ox},{oy})");
        }
        var hx = new double[6];
        var hy = new double[6];
        HexCorners(3, 4, 2, hx, hy);
        Check(Enumerable.Range(0, 6).All(i => Near(Math.Sqrt((hx[i] - 3) * (hx[i] - 3) + (hy[i] - 4) * (hy[i] - 4)), 2 / Math.Sqrt(3))), "a hex's corners are all as far from its centre");
        Check(Near(hx[0], 3) && hy[0] > 4, "a hex points up");
        Check(Near(hx.Max() - hx.Min(), 2), "a hex is as wide as the spacing, so neighbours meet");

        // Lines: full under the middle, nothing far off, rounded ends, as wide on a slant.
        var cover = new Dictionary<(int, int), double>();
        Line(2, 5.5, 12, 5.5, 1.0, 20, 20, (row, col, c) => cover[(row, col)] = c);
        Check(cover.TryGetValue((5, 7), out double mid) && Near(mid, 1), "a line covers the pixels under it");
        Check(!cover.ContainsKey((9, 7)) && !cover.ContainsKey((5, 16)), "a line leaves pixels far from it alone");
        Check(cover.Values.All(c => c > 0 && c <= 1), "a pixel is never covered more than fully");
        Check(cover.ContainsKey((5, 1)) && !cover.ContainsKey((5, 0)), "a line's ends are round and short");
        int straight = 0, slant = 0;
        Line(10, 10, 40, 10, 1.5, 60, 60, (row, col, c) => straight += c >= 1 && col == 25 ? 1 : 0);
        Line(10, 10, 40, 40, 1.5, 60, 60, (row, col, c) => slant += c >= 1 && Math.Abs(row - col) <= 6 && row == 25 ? 1 : 0);
        Check(straight >= 2 && slant >= straight, $"a line on a slant is as thick as a level one ({slant} and {straight})");
        int outside = 0;
        Line(-10, -10, 100, 100, 3, 30, 30, (row, col, c) => outside += row < 0 || col < 0 || row >= 30 || col >= 30 ? 1 : 0);
        Check(outside == 0, "a line past the picture's edge stays inside it");
    }

    // ---------------- Zoom and pan ----------------

    private static void Views(Action<bool, string> Check)
    {
        const double w = 640, h = 480;
        var v = new View(3.5, -7.25, 12);
        ToScreen(v, w, h, 10, 4, out double sx, out double sy);
        ToMap(v, w, h, sx, sy, out double x, out double y);
        Check(Near(x, 10) && Near(y, 4), "a point to the screen and back is the same point");
        ToScreen(v, w, h, v.cx, v.cy, out double mx, out double my);
        Check(Near(mx, w / 2) && Near(my, h / 2), "the view's middle is the screen's middle");
        ToScreen(v, w, h, v.cx, v.cy + 1, out _, out double upY);
        ToScreen(v, w, h, v.cx + 1, v.cy, out double rightX, out _);
        Check(upY < h / 2 && rightX > w / 2, "north is up and east to the right");

        foreach (var (cx, cy) in new[] { (100.0, 50.0), (600.0, 400.0), (320.0, 240.0) })
        {
            ToMap(v, w, h, cx, cy, out double bx, out double by);
            var z = ZoomAt(v, w, h, cx, cy, 30);
            ToMap(z, w, h, cx, cy, out double ax, out double ay);
            Check(Near(ax, bx) && Near(ay, by) && Near(z.scale, 30), $"zooming keeps the place under the mouse ({cx},{cy})");
        }
        var p = Pan(v, 24, -36);
        ToScreen(v, w, h, 5, 5, out double px0, out double py0);
        ToScreen(p, w, h, 5, 5, out double px1, out double py1);
        Check(Near(px1 - px0, 24) && Near(py1 - py0, -36), "dragging moves the land with the mouse");

        const double half = 36;
        double fit = FitScale(w, h, half);
        Check(Near(fit, h / (2 * half)), "at the least zoom the whole square fits");
        var outFar = Clamp(new View(20, -30, 0.5), w, h, half, 40);
        Check(Near(outFar.scale, fit) && Near(outFar.cx, Math.Max(0, half - w / (2 * fit)) * Math.Sign(20)) && Near(outFar.cy, 0), "zoomed right out, the square sits in the middle");
        var inFar = Clamp(new View(500, 500, 500), w, h, half, 40);
        Check(Near(inFar.scale, 40), "never zoomed in past the most");
        Check(Near(inFar.cx, half - w / 80) && Near(inFar.cy, half - h / 80), "the view stops at the square's edge");
        var ok = Clamp(new View(2, -3, 20), w, h, half, 40);
        Check(Near(ok.cx, 2) && Near(ok.cy, -3) && Near(ok.scale, 20), "a view inside the square is left alone");
        var tiny = Clamp(new View(1, 1, 50), w, h, half, 1);
        Check(Near(tiny.scale, fit), "the most zoom is never less than the least");
    }

    // ---------------- Colours of the land ----------------

    private static int Sum(Rgb c) => c.r + c.g + c.b;

    private static void Colours(Action<bool, string> Check)
    {
        var vanilla = new[]
        {
            "Ocean", "Lake", "SeaIce", "IceSheet", "Tundra", "ColdBog", "BorealForest", "TemperateForest", "TemperateSwamp", "TropicalRainforest",
            "TropicalSwamp", "AridShrubland", "Desert", "ExtremeDesert", "Glowforest", "Scarlands"
        };
        var lands = vanilla.Select(LandOf).ToList();
        Check(lands.All(l => l != Land.Unknown), "every biome of the game has its colour");
        Check(lands.Distinct().Count() == vanilla.Length, "no two of the game's biomes look the same");
        var colours = lands.Select(l => ColourOf(l)).Select(c => (c.r, c.g, c.b)).ToList();
        Check(colours.Distinct().Count() == colours.Count, "and no two share a colour");
        foreach (var l in new[] { Land.Ocean, Land.Lake })
        {
            var c = ColourOf(l);
            Check(c.b > c.r && c.b > c.g && IsWater(l), $"{l} is blue water");
        }
        Check(!IsWater(Land.SeaIce) && !IsWater(Land.Desert), "ice and sand are not water");
        Check(Sum(ColourOf(Land.Ice)) > Sum(ColourOf(Land.Tundra)) && Sum(ColourOf(Land.ExtremeDesert)) > Sum(ColourOf(Land.Desert)), "ice is paler than tundra, the extreme desert than the desert");

        // Other mods' biomes by their names; a name that says nothing is Unknown, for the climate to tell.
        var named = new (string name, Land land)[]
        {
            ("VFE_TropicalDesert", Land.Desert), ("AlpineMeadow", Land.Grassland), ("SeasonalForest", Land.Temperate), ("RG_Steppe", Land.Grassland),
            ("BiomesIslands_Atoll_Reef", Land.Ocean), ("MyMod_IceCaves", Land.Ice), ("ReGrowth_Mangrove", Land.TropicalSwamp),
            ("BMT_ChromaticDesert", Land.Desert), ("Savanna", Land.Grassland), ("CloudForest", Land.Temperate), ("PineBarrens", Land.Boreal),
            ("VolcanicIsland", Land.Lava), ("Jungle", Land.Tropical), ("tropicalswamp", Land.TropicalSwamp), ("Salt_Flats", Land.ExtremeDesert),
            ("ZZ_Strange", Land.Unknown), ("", Land.Unknown), (null, Land.Unknown)
        };
        foreach (var (name, land) in named) Check(LandOf(name) == land, $"{name ?? "null"} is {land} ({LandOf(name)})");

        Check(LandFromClimate(-25, 200) == Land.Ice, "bitter cold is ice");
        Check(LandFromClimate(-6, 300) == Land.Tundra && LandFromClimate(-6, 900) == Land.Bog, "cold is tundra, or bog where it is wet");
        Check(LandFromClimate(3, 600) == Land.Boreal, "cool and wet is boreal forest");
        Check(LandFromClimate(12, 1200) == Land.Temperate && LandFromClimate(12, 400) == Land.Shrubland && LandFromClimate(12, 100) == Land.Desert, "mild land by its rain");
        Check(LandFromClimate(28, 2500) == Land.Tropical && LandFromClimate(28, 100) == Land.ExtremeDesert && LandFromClimate(28, 1100) == Land.Grassland, "hot land by its rain");

        foreach (var l in new[] { Land.Temperate, Land.Desert, Land.Tundra, Land.Tropical })
        {
            var flat = ColourOf(l);
            var small = WithHills(flat, 2);
            var large = WithHills(flat, 3);
            var mountain = WithHills(flat, 4);
            var high = WithHills(flat, 5);
            Check(Sum(WithHills(flat, 1)) == Sum(flat) && Sum(WithHills(flat, 0)) == Sum(flat), $"{l}: flat land is its own colour");
            Check(Sum(small) < Sum(flat) && Sum(large) < Sum(small), $"{l}: the hillier the darker");
            double Grey(Rgb c) => Math.Abs(c.r - c.g) + Math.Abs(c.g - c.b) + Math.Abs(c.r - c.b);
            Check(Grey(mountain) < Grey(flat), $"{l}: mountains are rockier, greyer than the land");
            Check(Sum(high) > Sum(mountain), $"{l}: impassable heights are paler than mountains");
        }
    }

    // ---------------- How a people stands with the colony ----------------

    private static void Standings(Action<bool, string> Check)
    {
        Check(StandingOf(true, false, 0, 0, false) == Standing.Own, "the colony's own settlements are its own");
        Check(StandingOf(false, false, -1, -100, true) == Standing.Enemies && StandingOf(false, true, -1, -80, true) == Standing.Enemies, "pirates are enemies for good");
        Check(StandingOf(false, false, 0, 0, false) == Standing.NoDealings, "a people without goodwill has no dealings");
        Check(StandingOf(false, true, 1, 90, false) == Standing.Allies, "allies");
        Check(StandingOf(false, true, -1, -80, false) == Standing.Hostile, "hostile");
        Check(StandingOf(false, true, 0, 60, false) == Standing.Friendly && StandingOf(false, true, 0, 40, false) == Standing.Friendly, "friendly from 40");
        Check(StandingOf(false, true, 0, 0, false) == Standing.Fair && StandingOf(false, true, 0, 39, false) == Standing.Fair, "fair terms from 0");
        Check(StandingOf(false, true, 0, -1, false) == Standing.Cool && StandingOf(false, true, 0, -39, false) == Standing.Cool, "cool below 0");
        Check(StandingOf(false, true, 0, -40, false) == Standing.Unfriendly && StandingOf(false, true, 0, -74, false) == Standing.Unfriendly, "unfriendly from -40");
        // The game's relation decides over the number: an ally fallen to 10 is still an ally until the game says not.
        Check(StandingOf(false, true, 1, 10, false) == Standing.Allies && StandingOf(false, true, -1, 5, false) == Standing.Hostile, "the game's relation comes first");
    }
}
