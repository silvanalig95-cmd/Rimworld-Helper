using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The region map's arithmetic (World/RegionMap.cs, UI/RegionMapView.cs), free of game types so Tools/Tests can
    /// check it: the land around the colony laid flat on the plane that touches the planet at the home tile (east to
    /// the right, north up, every point at its true distance and bearing from home), which tiles fall inside the square
    /// that is drawn, filling a tile and drawing a river or road into the picture, zooming and panning the view, the
    /// colours of the land, and the plain words for how a people stands with the colony.
    /// </summary>
    public static class RegionMapMath
    {
        // ---------------- Points on the planet ----------------

        public readonly struct V3
        {
            public readonly double x, y, z;

            public V3(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
            public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
            public static V3 operator *(V3 a, double k) => new V3(a.x * k, a.y * k, a.z * k);
            public double Dot(V3 b) => x * b.x + y * b.y + z * b.z;
            public V3 Cross(V3 b) => new V3(y * b.z - z * b.y, z * b.x - x * b.z, x * b.y - y * b.x);
            public double Length => Math.Sqrt(x * x + y * y + z * z);

            public V3 Normalized
            {
                get
                {
                    double l = Length;
                    return l < 1e-12 ? new V3(0, 0, 0) : new V3(x / l, y / l, z / l);
                }
            }
        }

        /// <summary>The flat map's frame at the home tile: up away from the planet's centre, east and north along the ground.</summary>
        public readonly struct Basis
        {
            public readonly V3 up, east, north;

            public Basis(V3 up, V3 east, V3 north)
            {
                this.up = up;
                this.east = east;
                this.north = north;
            }
        }

        /// <summary>
        /// The frame at a point of the planet. East is the way the game's longitude grows (its LongLatOf reads
        /// atan2(x, -z)), north is toward the pole; at the pole itself, where north has no meaning, any east will do.
        /// </summary>
        public static Basis BasisAt(V3 point, V3 origin, V3 northPole)
        {
            var up = (point - origin).Normalized;
            var axis = (northPole - origin).Normalized;
            var east = up.Cross(axis);
            if (east.Length < 1e-9) east = up.Cross(Math.Abs(up.x) < 0.9 ? new V3(1, 0, 0) : new V3(0, 0, 1));
            east = east.Normalized;
            var north = east.Cross(up).Normalized;
            return new Basis(up, east, north);
        }

        /// <summary>
        /// Where a point of the planet lies on the flat map, in tiles from home (one tile is the distance between two
        /// neighbouring tiles' centres): its distance along the ground and its bearing from home are kept, so a place
        /// twelve tiles off is twelve tiles off on the map in the right direction.
        /// </summary>
        public static void Project(Basis b, V3 point, V3 origin, double radius, double tileSize, out double x, out double y)
        {
            var d = (point - origin).Normalized;
            double cos = d.Dot(b.up);
            var q = d - b.up * cos;
            double sin = q.Length;
            if (sin < 1e-12 || tileSize <= 0)
            {
                x = y = 0;
                return;
            }
            double dist = radius * Math.Atan2(sin, cos) / tileSize;
            x = dist * q.Dot(b.east) / sin;
            y = dist * q.Dot(b.north) / sin;
        }

        /// <summary>The game's own longitude and latitude (degrees) as a point on a planet of that radius around the origin.</summary>
        public static V3 FromLongLat(double longitude, double latitude, double radius)
        {
            double lo = longitude * Math.PI / 180.0, la = latitude * Math.PI / 180.0;
            return new V3(radius * Math.Cos(la) * Math.Sin(lo), radius * Math.Sin(la), -radius * Math.Cos(la) * Math.Cos(lo));
        }

        // ---------------- Which tiles ----------------

        /// <summary>Half the drawn square's side, in tiles: the reach and a tile more, so the corners are land too.</summary>
        public static double Half(int radius) => radius + 1.0;

        public static bool InSquare(double x, double y, double half) => Math.Abs(x) <= half && Math.Abs(y) <= half;

        /// <summary>
        /// The tiles to draw: every tile reached from home through neighbours whose centre is inside, home first, at
        /// most <paramref name="most"/>. The ground in a square is all of a piece, so this is every tile in it.
        /// </summary>
        public static List<int> Region(int home, Action<int, List<int>> neighbours, Func<int, bool> inside, int most)
        {
            var result = new List<int>();
            var seen = new HashSet<int> { home };
            var queue = new Queue<int>();
            queue.Enqueue(home);
            var around = new List<int>();
            while (queue.Count > 0 && result.Count < most)
            {
                int t = queue.Dequeue();
                result.Add(t);
                around.Clear();
                neighbours(t, around);
                foreach (int n in around)
                    if (seen.Add(n) && inside(n)) queue.Enqueue(n);
            }
            return result;
        }

        // ---------------- The picture ----------------

        /// <summary>Pixels per tile in the picture: finer for a small reach, never so big a picture that it weighs on memory.</summary>
        public static int PixelsPerTile(int radius) => Math.Max(10, Math.Min(20, (int)(MaxPicture / (2.0 * Half(radius)))));

        /// <summary>The picture is square, at most this many pixels a side.</summary>
        public const int MaxPicture = 1536;

        public static int PictureSize(int radius) => (int)Math.Ceiling(2.0 * Half(radius) * PixelsPerTile(radius));

        /// <summary>
        /// The pixels a polygon covers, row by row: a pixel is in when its centre is (a centre on the left or bottom edge
        /// is in, on the right or top edge out), so tiles side by side share no pixel and leave no gap.
        /// <paramref name="span"/> gets the row and the first and last column.
        /// </summary>
        public static void Spans(double[] xs, double[] ys, int n, int width, int height, Action<int, int, int> span)
        {
            if (n < 3) return;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                minY = Math.Min(minY, ys[i]);
                maxY = Math.Max(maxY, ys[i]);
            }
            int r0 = Math.Max(0, (int)Math.Ceiling(minY - 0.5)), r1 = Math.Min(height - 1, (int)Math.Ceiling(maxY - 0.5) - 1);
            var cuts = new List<double>(4);
            for (int row = r0; row <= r1; row++)
            {
                double yc = row + 0.5;
                cuts.Clear();
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    // Each edge worked out from its lower end, so the two tiles that share it find the same crossing to the last digit.
                    double xa = xs[j], ya = ys[j], xb = xs[i], yb = ys[i];
                    if (ya > yb)
                    {
                        (xa, xb) = (xb, xa);
                        (ya, yb) = (yb, ya);
                    }
                    if (ya <= yc && yb > yc) cuts.Add(xa + (yc - ya) * (xb - xa) / (yb - ya));
                }
                if (cuts.Count < 2) continue;
                cuts.Sort();
                for (int k = 0; k + 1 < cuts.Count; k += 2)
                {
                    int c0 = Math.Max(0, (int)Math.Ceiling(cuts[k] - 0.5));
                    int c1 = Math.Min(width - 1, (int)Math.Ceiling(cuts[k + 1] - 0.5) - 1);
                    if (c1 >= c0) span(row, c0, c1);
                }
            }
        }

        /// <summary>
        /// A river or road between two points: every pixel near the line, with how much of it the line covers (1 inside,
        /// fading over the last pixel), so a line keeps its width at any slant and its ends are round.
        /// </summary>
        public static void Line(double x0, double y0, double x1, double y1, double halfWidth, int width, int height, Action<int, int, double> plot)
        {
            double reach = halfWidth + 1.0;
            int cMin = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach)), cMax = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
            int rMin = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - reach)), rMax = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));
            double dx = x1 - x0, dy = y1 - y0, len2 = dx * dx + dy * dy;
            for (int row = rMin; row <= rMax; row++)
            for (int col = cMin; col <= cMax; col++)
            {
                double px = col + 0.5, py = row + 0.5;
                double t = len2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((px - x0) * dx + (py - y0) * dy) / len2));
                double ex = px - (x0 + t * dx), ey = py - (y0 + t * dy);
                double cover = Math.Min(1.0, halfWidth + 0.5 - Math.Sqrt(ex * ex + ey * ey));
                if (cover > 0) plot(row, col, cover);
            }
        }

        /// <summary>
        /// A hexagon's corners around a centre, pointed at the top, as far from the centre as a hex grid with that
        /// spacing between centres needs: drawn for a tile whose own corners can't be read.
        /// </summary>
        public static void HexCorners(double cx, double cy, double spacing, double[] xs, double[] ys)
        {
            double r = spacing / Math.Sqrt(3.0);
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 2 + i * Math.PI / 3;
                xs[i] = cx + r * Math.Cos(a);
                ys[i] = cy + r * Math.Sin(a);
            }
        }

        // ---------------- The view ----------------

        /// <summary>What the window shows: the map point at its middle (in tiles from home) and how many pixels a tile is.</summary>
        public struct View
        {
            public double cx, cy, scale;

            public View(double cx, double cy, double scale)
            {
                this.cx = cx;
                this.cy = cy;
                this.scale = scale;
            }
        }

        /// <summary>A map point on a screen area of that size (y grows downward on the screen, north is up).</summary>
        public static void ToScreen(View v, double w, double h, double x, double y, out double sx, out double sy)
        {
            sx = w / 2 + (x - v.cx) * v.scale;
            sy = h / 2 - (y - v.cy) * v.scale;
        }

        public static void ToMap(View v, double w, double h, double sx, double sy, out double x, out double y)
        {
            double s = v.scale <= 0 ? 1 : v.scale;
            x = v.cx + (sx - w / 2) / s;
            y = v.cy - (sy - h / 2) / s;
        }

        /// <summary>The scale at which the whole square fits the area.</summary>
        public static double FitScale(double w, double h, double half) => half <= 0 ? 1 : Math.Max(0.01, Math.Min(w, h) / (2 * half));

        /// <summary>Zoomed to a new scale around a screen point, which stays over the same place.</summary>
        public static View ZoomAt(View v, double w, double h, double sx, double sy, double scale)
        {
            ToMap(v, w, h, sx, sy, out double x, out double y);
            var z = new View(v.cx, v.cy, scale);
            // The point that was under the mouse is under it again.
            z.cx = x - (sx - w / 2) / scale;
            z.cy = y + (sy - h / 2) / scale;
            return z;
        }

        /// <summary>Moved by a drag of so many pixels: the land follows the mouse.</summary>
        public static View Pan(View v, double dx, double dy)
        {
            double s = v.scale <= 0 ? 1 : v.scale;
            return new View(v.cx - dx / s, v.cy + dy / s, v.scale);
        }

        /// <summary>
        /// Kept to the square: the scale between the least (the whole square shows) and the most, and the middle where
        /// the view still lies over the drawn land (in the square's middle when the whole of it shows).
        /// </summary>
        public static View Clamp(View v, double w, double h, double half, double most)
        {
            double least = FitScale(w, h, half);
            double scale = Math.Max(least, Math.Min(Math.Max(least, most), v.scale));
            double hx = Math.Max(0, half - w / (2 * scale)), hy = Math.Max(0, half - h / (2 * scale));
            return new View(Math.Max(-hx, Math.Min(hx, v.cx)), Math.Max(-hy, Math.Min(hy, v.cy)), scale);
        }

        // ---------------- The land's colours ----------------

        public readonly struct Rgb
        {
            public readonly byte r, g, b;

            public Rgb(int r, int g, int b)
            {
                this.r = (byte)Math.Max(0, Math.Min(255, r));
                this.g = (byte)Math.Max(0, Math.Min(255, g));
                this.b = (byte)Math.Max(0, Math.Min(255, b));
            }

            public Rgb Times(double k) => new Rgb((int)Math.Round(r * k), (int)Math.Round(g * k), (int)Math.Round(b * k));

            public Rgb Toward(Rgb o, double t) =>
                new Rgb((int)Math.Round(r + (o.r - r) * t), (int)Math.Round(g + (o.g - g) * t), (int)Math.Round(b + (o.b - b) * t));
        }

        /// <summary>The kinds of land the map tells apart by colour.</summary>
        public enum Land
        {
            Unknown,
            Ocean,
            Lake,
            SeaIce,
            Ice,
            Tundra,
            Bog,
            Boreal,
            Temperate,
            Swamp,
            Tropical,
            TropicalSwamp,
            Shrubland,
            Desert,
            ExtremeDesert,
            Grassland,
            Glowforest,
            Scarland,
            Lava,
            Rock
        }

        public static bool IsWater(Land l) => l == Land.Ocean || l == Land.Lake;

        /// <summary>The game's own biomes by name (with Odyssey's), then other mods' by the words in their names.</summary>
        private static readonly Dictionary<string, Land> Known = new Dictionary<string, Land>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ocean"] = Land.Ocean,
            ["Lake"] = Land.Lake,
            ["SeaIce"] = Land.SeaIce,
            ["IceSheet"] = Land.Ice,
            ["Tundra"] = Land.Tundra,
            ["ColdBog"] = Land.Bog,
            ["BorealForest"] = Land.Boreal,
            ["TemperateForest"] = Land.Temperate,
            ["TemperateSwamp"] = Land.Swamp,
            ["TropicalRainforest"] = Land.Tropical,
            ["TropicalSwamp"] = Land.TropicalSwamp,
            ["AridShrubland"] = Land.Shrubland,
            ["Desert"] = Land.Desert,
            ["ExtremeDesert"] = Land.ExtremeDesert,
            ["Grasslands"] = Land.Grassland,
            ["Glowforest"] = Land.Glowforest,
            ["Scarlands"] = Land.Scarland,
            ["GlacialPlain"] = Land.Ice,
            ["LavaField"] = Land.Lava
        };

        /// <summary>
        /// Words in a modded biome's name and the land they mean, the more telling first: sea ice before ice, a tropical
        /// desert is a desert and a tropical savanna grassland, an alpine meadow a meadow. Water is told by the biome's
        /// own flag rather than its name ("Seasonal" has "sea" in it).
        /// </summary>
        private static readonly (string word, Land land)[] Words =
        {
            ("seaice", Land.SeaIce), ("packice", Land.SeaIce), ("glacier", Land.Ice), ("glacial", Land.Ice), ("icesheet", Land.Ice), ("polar", Land.Ice),
            ("frozen", Land.Ice), ("snow", Land.Ice), ("ice", Land.Ice),
            ("ocean", Land.Ocean), ("reef", Land.Ocean), ("lake", Land.Lake),
            ("lava", Land.Lava), ("volcan", Land.Lava), ("magma", Land.Lava),
            ("glow", Land.Glowforest), ("fungal", Land.Glowforest), ("mushroom", Land.Glowforest),
            ("scar", Land.Scarland), ("wasteland", Land.Scarland), ("toxic", Land.Scarland), ("blight", Land.Scarland),
            ("tundra", Land.Tundra), ("permafrost", Land.Tundra),
            ("boreal", Land.Boreal), ("taiga", Land.Boreal), ("conifer", Land.Boreal),
            ("bog", Land.Bog), ("mire", Land.Bog),
            ("mangrove", Land.TropicalSwamp), ("swamp", Land.Swamp), ("marsh", Land.Swamp), ("wetland", Land.Swamp),
            ("extremedesert", Land.ExtremeDesert), ("dune", Land.ExtremeDesert), ("saltflat", Land.ExtremeDesert),
            ("desert", Land.Desert), ("badland", Land.Desert), ("mesa", Land.Desert), ("canyon", Land.Desert),
            ("shrub", Land.Shrubland), ("scrub", Land.Shrubland), ("arid", Land.Shrubland), ("chaparral", Land.Shrubland),
            ("savanna", Land.Grassland), ("steppe", Land.Grassland), ("prairie", Land.Grassland), ("grass", Land.Grassland),
            ("meadow", Land.Grassland),
            ("rainforest", Land.Tropical), ("jungle", Land.Tropical), ("tropic", Land.Tropical),
            ("mountain", Land.Rock), ("alpine", Land.Rock), ("highland", Land.Rock), ("crag", Land.Rock),
            ("pine", Land.Boreal), ("forest", Land.Temperate), ("wood", Land.Temperate), ("grove", Land.Temperate)
        };

        /// <summary>The land a biome's name tells, or Unknown when the name says nothing the map knows.</summary>
        public static Land LandOf(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return Land.Unknown;
            if (Known.TryGetValue(defName, out var land)) return land;
            string s = defName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            foreach (var (word, l) in Words)
                if (s.Contains(word)) return l;
            return Land.Unknown;
        }

        /// <summary>The land a tile's year would grow, for a biome whose name says nothing: from its mean temperature (°C) and rain (mm a year).</summary>
        public static Land LandFromClimate(double temperature, double rainfall)
        {
            if (temperature < -12) return Land.Ice;
            if (temperature < -2) return rainfall > 700 ? Land.Bog : Land.Tundra;
            if (temperature < 7) return rainfall < 300 ? Land.Tundra : Land.Boreal;
            if (temperature < 20)
            {
                if (rainfall < 250) return Land.Desert;
                if (rainfall < 600) return Land.Shrubland;
                return rainfall > 2000 ? Land.Swamp : Land.Temperate;
            }
            if (rainfall < 200) return Land.ExtremeDesert;
            if (rainfall < 450) return Land.Desert;
            if (rainfall < 900) return Land.Shrubland;
            if (rainfall < 1400) return Land.Grassland;
            return rainfall > 3000 ? Land.TropicalSwamp : Land.Tropical;
        }

        public static Rgb ColourOf(Land land)
        {
            switch (land)
            {
                case Land.Ocean: return new Rgb(36, 66, 104);
                case Land.Lake: return new Rgb(52, 94, 140);
                case Land.SeaIce: return new Rgb(188, 208, 222);
                case Land.Ice: return new Rgb(228, 235, 240);
                case Land.Tundra: return new Rgb(152, 154, 128);
                case Land.Bog: return new Rgb(104, 116, 92);
                case Land.Boreal: return new Rgb(62, 98, 80);
                case Land.Temperate: return new Rgb(80, 124, 64);
                case Land.Swamp: return new Rgb(78, 106, 80);
                case Land.Tropical: return new Rgb(38, 112, 56);
                case Land.TropicalSwamp: return new Rgb(54, 96, 72);
                case Land.Shrubland: return new Rgb(168, 154, 100);
                case Land.Desert: return new Rgb(206, 182, 126);
                case Land.ExtremeDesert: return new Rgb(228, 206, 154);
                case Land.Grassland: return new Rgb(132, 152, 78);
                case Land.Glowforest: return new Rgb(62, 118, 122);
                case Land.Scarland: return new Rgb(126, 100, 92);
                case Land.Lava: return new Rgb(92, 62, 56);
                case Land.Rock: return new Rgb(124, 116, 106);
                default: return new Rgb(110, 110, 110);
            }
        }

        /// <summary>The bare rock mountains are drawn toward, and the snow on the highest.</summary>
        public static readonly Rgb Mountain = new Rgb(118, 110, 100), Peak = new Rgb(214, 214, 210);

        /// <summary>
        /// A tile's colour for its hills (the game's Hilliness: 0 undefined, 1 flat, 2 small hills, 3 large hills,
        /// 4 mountainous, 5 impassable): darker the hillier, mountains mostly rock, impassable ones grey and pale.
        /// </summary>
        public static Rgb WithHills(Rgb c, int hilliness)
        {
            switch (hilliness)
            {
                case 2: return c.Times(0.9);
                case 3: return c.Times(0.78);
                case 4: return c.Toward(Mountain, 0.55).Times(0.8);
                case 5: return c.Toward(Mountain, 0.75).Toward(Peak, 0.2);
                default: return c;
            }
        }

        // ---------------- How a people stands with the colony ----------------

        public enum Standing
        {
            Own,
            Allies,
            Friendly,
            Fair,
            Cool,
            Unfriendly,
            Hostile,
            Enemies,
            NoDealings
        }

        /// <summary>
        /// Plain words for a people's relation with the colony: <paramref name="kind"/> is the game's relation (-1
        /// hostile, 0 neutral, 1 allied); a neutral people is told by its goodwill (the game turns hostile at -75 and
        /// allied at 75). Pirates and the like are never at peace.
        /// </summary>
        public static Standing StandingOf(bool player, bool hasGoodwill, int kind, int goodwill, bool permanentEnemy)
        {
            if (player) return Standing.Own;
            if (permanentEnemy) return Standing.Enemies;
            if (!hasGoodwill) return kind < 0 ? Standing.Enemies : Standing.NoDealings;
            if (kind > 0) return Standing.Allies;
            if (kind < 0) return Standing.Hostile;
            if (goodwill >= 40) return Standing.Friendly;
            if (goodwill >= 0) return Standing.Fair;
            if (goodwill > -40) return Standing.Cool;
            return Standing.Unfriendly;
        }
    }
}
