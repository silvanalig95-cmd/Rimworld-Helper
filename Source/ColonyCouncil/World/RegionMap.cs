using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static ColonyCouncil.RegionMapMath;

namespace ColonyCouncil
{
    /// <summary>What a mark on the region map stands for.</summary>
    public enum RegionMarkKind
    {
        Colony,
        Settlement,
        Site,
        Caravan,
        Other
    }

    /// <summary>One world object on the region map: where it is on the flat map and how it is drawn.</summary>
    public sealed class RegionMark
    {
        public WorldObject obj;
        public RegionMarkKind kind;
        /// <summary>The tile's index in the map's tiles, and the mark's place on the flat map (tiles from home).</summary>
        public int index;
        public float x, y;
        public Texture2D icon;
        public Color color = Color.white;
        public string label;
        public float labelWidth;

        public float Size => kind == RegionMarkKind.Colony ? 24f : kind == RegionMarkKind.Settlement ? 20f : kind == RegionMarkKind.Other ? 14f : 16f;
    }

    /// <summary>
    /// The picture behind the region map window (UI/Window_RegionMap.cs): the tiles around the colony's home tile laid
    /// flat (World/RegionMapMath.cs) and drawn once into a texture by their biome and hills, with rivers and roads along
    /// their links; a lookup from each pixel to its tile for the mouse; and the world objects on those tiles, gathered
    /// again every two seconds. It only reads the world and never touches the game's own world drawing, so mods that
    /// add biomes, map modes, factions or territories leave it alone (a biome it doesn't know is coloured by its name
    /// or its climate).
    /// </summary>
    public sealed class RegionMap
    {
        public PlanetLayer layer;
        public PlanetTile home;
        public int radius;
        public double half;
        public int ppt, size;
        public Texture2D texture;
        /// <summary>Each drawn tile by index: its id on the layer and its centre on the flat map.</summary>
        public int[] ids;
        public float[] cx, cy;
        private bool[] water;
        private readonly Dictionary<int, int> indexOf = new Dictionary<int, int>();
        /// <summary>The tile index plus one under each pixel of the picture, 0 where there is no tile.</summary>
        private ushort[] pixels;
        private Basis basis;
        private V3 origin;
        private double planetRadius, tileSize;
        private readonly List<RegionMark> marks = new List<RegionMark>();
        private float marksAt = -100f;

        private static readonly Color32 Empty = new Color32(0, 0, 0, 0);
        private static readonly Rgb RiverColour = new Rgb(70, 118, 168);
        private static readonly Dictionary<BiomeDef, Land> landByBiome = new Dictionary<BiomeDef, Land>();
        private static float widestRiver = -1f;

        public int Count => ids?.Length ?? 0;

        // ---------------- Where home is ----------------

        /// <summary>
        /// The tile the map is centred on: the colony the player is looking at, else the seat (Homes), else any of the
        /// colony's settlements or caravans; on the planet's surface (a home in orbit is centred on the ground below it).
        /// </summary>
        public static bool HomeTile(out PlanetLayer layer, out PlanetTile tile)
        {
            layer = null;
            tile = PlanetTile.Invalid;
            try
            {
                var map = Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome ? Find.CurrentMap : HappeningTracker.HomeMap() ?? Find.AnyPlayerHomeMap;
                var t = map != null ? map.Tile : PlanetTile.Invalid;
                if (!t.Valid)
                    foreach (var wo in Find.WorldObjects.AllWorldObjects)
                        if ((wo is Settlement || wo is Caravan) && wo.Faction == Faction.OfPlayer)
                        {
                            t = wo.Tile;
                            break;
                        }
                if (!t.Valid) return false;
                var surface = Find.WorldGrid.Surface;
                layer = t.Layer is SurfaceLayer own ? own : surface;
                if (layer == null) return false;
                if (t.Layer != layer) t = layer.GetClosestTile_NewTemp(t, false);
                tile = t;
                return tile.Valid;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map home", e);
                return false;
            }
        }

        // ---------------- Drawing the picture ----------------

        private static V3 V(Vector3 v) => new V3(v.x, v.y, v.z);

        private void Flat(Vector3 p, out double x, out double y) => Project(basis, V(p), origin, planetRadius, tileSize, out x, out y);

        /// <summary>The tiles within the reach of home, drawn. Null when the world can't be read.</summary>
        public static RegionMap Build(PlanetLayer layer, PlanetTile home, int radius)
        {
            var m = new RegionMap { layer = layer, home = home, radius = radius };
            m.Draw();
            return m;
        }

        private void Draw()
        {
            origin = V(layer.Origin);
            planetRadius = layer.Radius;
            tileSize = layer.AverageTileSize;
            basis = BasisAt(V(layer.GetTileCenter(home)), origin, V(layer.NorthPolePos));
            half = Half(radius);
            ppt = PixelsPerTile(radius);
            size = PictureSize(radius);

            // The tiles: every one whose centre is in the square, and a row past it so the edge is filled.
            var centres = new Dictionary<int, (double x, double y)>();
            var around = new List<PlanetTile>();
            bool Inside(int id)
            {
                if (!centres.TryGetValue(id, out var c))
                {
                    Flat(layer.GetTileCenter(new PlanetTile(id, layer)), out c.x, out c.y);
                    centres[id] = c;
                }
                return InSquare(c.x, c.y, half + 1.0);
            }
            void Neighbours(int id, List<int> list)
            {
                around.Clear();
                layer.GetTileNeighbors(new PlanetTile(id, layer), around);
                foreach (var t in around) list.Add(t.tileId);
            }
            Inside(home.tileId);
            var region = Region(home.tileId, Neighbours, Inside, ushort.MaxValue - 1);
            int n = region.Count;
            ids = region.ToArray();
            cx = new float[n];
            cy = new float[n];
            water = new bool[n];
            indexOf.Clear();
            for (int i = 0; i < n; i++)
            {
                indexOf[ids[i]] = i;
                var c = centres[ids[i]];
                cx[i] = (float)c.x;
                cy[i] = (float)c.y;
            }

            var px = new Color32[size * size];
            pixels = new ushort[size * size];
            var verts = new List<Vector3>();
            var xs = new double[16];
            var ys = new double[16];
            Color32 fill = Empty;
            ushort mark = 0;
            Action<int, int, int> span = (row, c0, c1) =>
            {
                int at = row * size;
                for (int c = c0; c <= c1; c++)
                {
                    px[at + c] = fill;
                    pixels[at + c] = mark;
                }
            };
            for (int i = 0; i < n; i++)
            {
                var pt = new PlanetTile(ids[i], layer);
                var tile = layer[ids[i]];
                var rgb = TileColour(tile, out bool wet, out int hills);
                water[i] = wet;
                fill = new Color32(rgb.r, rgb.g, rgb.b, 255);
                mark = (ushort)(i + 1);
                int k = 0;
                verts.Clear();
                layer.GetTileVertices(pt, verts);
                if (verts.Count >= 3 && verts.Count <= xs.Length)
                {
                    foreach (var v in verts)
                    {
                        Flat(v, out double vx, out double vy);
                        xs[k] = (vx + half) * ppt;
                        ys[k] = (vy + half) * ppt;
                        k++;
                    }
                }
                else
                {
                    HexCorners((cx[i] + half) * ppt, (cy[i] + half) * ppt, ppt, xs, ys);
                    k = 6;
                }
                Spans(xs, ys, k, size, size, span);
                // A mountain gets a peak drawn in its middle, so mountains read as mountains at a glance.
                if (hills >= 4 && !wet)
                {
                    var peak = hills >= 5 ? Peak : rgb.Times(0.68);
                    fill = new Color32(peak.r, peak.g, peak.b, 255);
                    double mx = (cx[i] + half) * ppt, my = (cy[i] + half) * ppt;
                    xs[0] = mx - 0.3 * ppt;
                    ys[0] = my - 0.2 * ppt;
                    xs[1] = mx + 0.3 * ppt;
                    ys[1] = my - 0.2 * ppt;
                    xs[2] = mx;
                    ys[2] = my + 0.26 * ppt;
                    Spans(xs, ys, 3, size, size, span);
                }
            }

            // A faint line between neighbouring tiles of land, so the tiles read as tiles; none on open water.
            for (int row = 0; row < size; row++)
            {
                int at = row * size;
                for (int c = 0; c < size; c++)
                {
                    int k = pixels[at + c];
                    if (k == 0) continue;
                    int right = c + 1 < size ? pixels[at + c + 1] : 0, up = row + 1 < size ? pixels[at + size + c] : 0;
                    bool edge = (right != 0 && right != k && !(water[k - 1] && water[right - 1])) || (up != 0 && up != k && !(water[k - 1] && water[up - 1]));
                    if (!edge) continue;
                    var p = px[at + c];
                    px[at + c] = new Color32((byte)(p.r * 0.86f), (byte)(p.g * 0.86f), (byte)(p.b * 0.86f), 255);
                }
            }

            Links(px, true);
            Links(px, false);

            texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "RimfolkRegionMap",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(px);
            texture.Apply(true, true);
        }

        /// <summary>A tile's colour: its biome's land (or its climate's, for a biome the map doesn't know), darker for hills.</summary>
        private static Rgb TileColour(Tile tile, out bool wet, out int hills)
        {
            var biome = tile.PrimaryBiome;
            Land land = Land.Unknown;
            if (biome != null && !landByBiome.TryGetValue(biome, out land))
            {
                land = LandOf(biome.defName);
                if (land == Land.Unknown && biome.isWaterBiome) land = Land.Ocean;
                landByBiome[biome] = land;
            }
            if (land == Land.Unknown) land = tile.WaterCovered ? Land.Ocean : LandFromClimate(tile.temperature, tile.rainfall);
            wet = IsWater(land);
            hills = wet ? 0 : (int)HillsOf(tile);
            return WithHills(ColourOf(land), hills);
        }

        private static Hilliness HillsOf(Tile tile)
        {
            try
            {
                return tile.HillinessLabel;
            }
            catch
            {
                return tile.hilliness;
            }
        }

        /// <summary>Rivers (true) or roads (false) along their links, each drawn once, out to the picture's edge.</summary>
        private void Links(Color32[] px, bool rivers)
        {
            if (rivers && widestRiver < 0f)
            {
                widestRiver = 0.01f;
                foreach (var r in DefDatabase<RiverDef>.AllDefsListForReading) widestRiver = Mathf.Max(widestRiver, r.widthOnWorld);
            }
            Rgb colour = RiverColour;
            double halfWidth = 1;
            Action<int, int, double> plot = (row, col, cover) =>
            {
                int at = row * size + col;
                var p = px[at];
                if (p.a == 0) return;
                float t = (float)cover;
                px[at] = new Color32((byte)(p.r + (colour.r - p.r) * t), (byte)(p.g + (colour.g - p.g) * t), (byte)(p.b + (colour.b - p.b) * t), 255);
            };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!(layer[ids[i]] is SurfaceTile st)) continue;
                if (rivers)
                {
                    var list = st.Rivers;
                    if (list == null) continue;
                    foreach (var link in list)
                    {
                        if (link.river == null || !Other(i, link.neighbor, out double ox, out double oy)) continue;
                        halfWidth = 0.7 + Mathf.Clamp01(link.river.widthOnWorld / widestRiver) * ppt * 0.16;
                        Line((cx[i] + half) * ppt, (cy[i] + half) * ppt, (ox + half) * ppt, (oy + half) * ppt, halfWidth, size, size, plot);
                    }
                }
                else
                {
                    var list = st.Roads;
                    if (list == null) continue;
                    foreach (var link in list)
                    {
                        if (link.road == null || !Other(i, link.neighbor, out double ox, out double oy)) continue;
                        colour = RoadColour(link.road);
                        halfWidth = 0.6 + Mathf.Clamp01(1f - link.road.movementCostMultiplier) * ppt * 0.09;
                        Line((cx[i] + half) * ppt, (cy[i] + half) * ppt, (ox + half) * ppt, (oy + half) * ppt, halfWidth, size, size, plot);
                    }
                }
            }
        }

        /// <summary>The far end of a link from tile <paramref name="i"/>: false when the link is drawn from the other side instead.</summary>
        private bool Other(int i, PlanetTile neighbour, out double x, out double y)
        {
            if (indexOf.TryGetValue(neighbour.tileId, out int j))
            {
                x = cx[j];
                y = cy[j];
                return ids[i] < neighbour.tileId;
            }
            Flat(layer.GetTileCenter(new PlanetTile(neighbour.tileId, layer)), out x, out y);
            return true;
        }

        private static Rgb RoadColour(RoadDef road)
        {
            string s = road.defName?.ToLowerInvariant() ?? "";
            if (s.Contains("asphalt") || s.Contains("highway")) return new Rgb(66, 66, 70);
            if (s.Contains("stone") || s.Contains("paved") || s.Contains("cobble")) return new Rgb(150, 144, 134);
            if (s.Contains("path") || s.Contains("trail")) return new Rgb(172, 146, 106);
            return new Rgb(138, 104, 70);
        }

        public void Dispose()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
            pixels = null;
        }

        // ---------------- Reading it back ----------------

        public bool TryIndex(PlanetTile tile, out int index)
        {
            index = -1;
            return tile.Valid && tile.Layer == layer && indexOf.TryGetValue(tile.tileId, out index);
        }

        /// <summary>The tile under a point of the flat map, as drawn: its index, or -1.</summary>
        public int TileAt(double x, double y)
        {
            if (pixels == null) return -1;
            int c = (int)Math.Floor((x + half) * ppt), r = (int)Math.Floor((y + half) * ppt);
            if (c < 0 || r < 0 || c >= size || r >= size) return -1;
            return pixels[r * size + c] - 1;
        }

        public PlanetTile TileOf(int index) => index >= 0 && index < Count ? new PlanetTile(ids[index], layer) : PlanetTile.Invalid;

        /// <summary>A tile's corners on the flat map (for the ring round a picked tile).</summary>
        public List<Vector2> Corners(int index)
        {
            var list = new List<Vector2>();
            if (index < 0 || index >= Count) return list;
            var verts = new List<Vector3>();
            layer.GetTileVertices(TileOf(index), verts);
            foreach (var v in verts)
            {
                Flat(v, out double x, out double y);
                list.Add(new Vector2((float)x, (float)y));
            }
            return list;
        }

        /// <summary>Tiles from home along the ground, as the map lays them out.</summary>
        public float TilesFromHome(int index) => index >= 0 && index < Count ? Mathf.Sqrt(cx[index] * cx[index] + cy[index] * cy[index]) : 0f;

        // ---------------- What's on the land ----------------

        /// <summary>The settlements, sites, the colony's caravans and other places on the drawn tiles, gathered every two seconds.</summary>
        public List<RegionMark> Marks()
        {
            if (Time.realtimeSinceStartup - marksAt < 2f) return marks;
            marksAt = Time.realtimeSinceStartup;
            marks.Clear();
            try
            {
                var player = Faction.OfPlayerSilentFail;
                var font = Text.Font;
                Text.Font = GameFont.Tiny;
                foreach (var wo in Find.WorldObjects.AllWorldObjectsOnLayer(layer))
                {
                    if (wo == null || wo.Destroyed || !indexOf.TryGetValue(wo.Tile.tileId, out int i)) continue;
                    RegionMarkKind kind;
                    if (wo is Settlement s) kind = s.Faction == player && s.Tile == home ? RegionMarkKind.Colony : RegionMarkKind.Settlement;
                    else if (wo is Caravan c)
                    {
                        if (!c.IsPlayerControlled) continue;
                        kind = RegionMarkKind.Caravan;
                    }
                    else if (wo is Site) kind = RegionMarkKind.Site;
                    else if (wo is MapParent) kind = RegionMarkKind.Other;
                    else continue;
                    var m = new RegionMark { obj = wo, kind = kind, index = i, x = cx[i], y = cy[i] };
                    try
                    {
                        m.icon = wo.ExpandingIcon;
                        m.color = wo.ExpandingIconColor;
                        m.label = wo.LabelCap;
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("region map: a world object's look", e);
                    }
                    if (m.icon == BaseContent.BadTex) m.icon = null;
                    if (kind == RegionMarkKind.Caravan)
                    {
                        // A caravan at a settlement sits beside it, not on it.
                        m.x += 0.32f;
                        m.y -= 0.22f;
                    }
                    if (!m.label.NullOrEmpty()) m.labelWidth = Text.CalcSize(m.label).x;
                    marks.Add(m);
                }
                Text.Font = font;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map marks", e);
            }
            return marks;
        }

        /// <summary>Gather the marks again at the next chance (a selection may have changed what is there).</summary>
        public void MarksDirty() => marksAt = -100f;

        // ---------------- Words ----------------

        /// <summary>What a tile is, for the tooltip and the panel: land, hills, climate, rivers and roads, its name.</summary>
        public string TileText(int index, bool withDistance)
        {
            if (index < 0 || index >= Count) return null;
            try
            {
                var tile = layer[ids[index]];
                var lines = new List<string>();
                string biome = tile.PrimaryBiome?.LabelCap.ToString() ?? "?";
                var hills = HillsOf(tile);
                lines.Add(hills == Hilliness.Undefined || water[index] ? biome : "CC_Region_Land".Translate(biome, HillinessUtility.GetLabel(hills)).Resolve());
                string feature = tile.feature?.name, landmark = tile.Landmark?.name;
                if (!feature.NullOrEmpty()) lines.Add(feature);
                if (!landmark.NullOrEmpty() && landmark != feature) lines.Add(landmark);
                lines.Add("CC_Region_Climate".Translate(tile.temperature.ToStringTemperature("F0"), Mathf.RoundToInt(tile.rainfall)).Resolve());
                if (tile is SurfaceTile st)
                {
                    var rivers = new List<string>();
                    if (st.Rivers != null)
                        foreach (var l in st.Rivers)
                            if (l.river != null && !rivers.Contains(l.river.LabelCap)) rivers.Add(l.river.LabelCap);
                    if (rivers.Count > 0) lines.Add("CC_Region_River".Translate(TextUtil.JoinAnd(rivers)).Resolve());
                    var roads = new List<string>();
                    if (st.Roads != null)
                        foreach (var l in st.Roads)
                            if (l.road != null && !roads.Contains(l.road.LabelCap)) roads.Add(l.road.LabelCap);
                    if (roads.Count > 0) lines.Add("CC_Region_Road".Translate(TextUtil.JoinAnd(roads)).Resolve());
                }
                if (withDistance)
                {
                    int tiles = Mathf.RoundToInt(TilesFromHome(index));
                    lines.Add(tiles == 0 ? "CC_Region_HomeTile".Translate().ToString() : "CC_Region_TilesAway".Translate(tiles).Resolve());
                }
                return string.Join("\n", lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map tile text", e);
                return null;
            }
        }

        /// <summary>How a people stands with the colony, in plain words.</summary>
        public static string StandingText(Faction f)
        {
            if (f == null) return null;
            try
            {
                int kind = f.IsPlayer ? 0 : f.PlayerRelationKind == FactionRelationKind.Hostile ? -1 : f.PlayerRelationKind == FactionRelationKind.Ally ? 1 : 0;
                int goodwill = f.IsPlayer || !f.HasGoodwill ? 0 : f.PlayerGoodwill;
                string gw = goodwill.ToStringWithSign();
                switch (StandingOf(f.IsPlayer, f.HasGoodwill, kind, goodwill, f.def != null && f.def.permanentEnemy))
                {
                    case Standing.Own: return "CC_Region_Std_Own".Translate();
                    case Standing.Allies: return "CC_Region_Std_Allies".Translate(gw);
                    case Standing.Friendly: return "CC_Region_Std_Friendly".Translate(gw);
                    case Standing.Fair: return "CC_Region_Std_Fair".Translate(gw);
                    case Standing.Cool: return "CC_Region_Std_Cool".Translate(gw);
                    case Standing.Unfriendly: return "CC_Region_Std_Unfriendly".Translate(gw);
                    case Standing.Hostile: return "CC_Region_Std_Hostile".Translate(gw);
                    case Standing.Enemies: return "CC_Region_Std_Enemies".Translate();
                    default: return "CC_Region_Std_NoDealings".Translate();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map standing", e);
                return null;
            }
        }

        public static Color StandingColor(Faction f)
        {
            if (f == null || f.IsPlayer) return UiKit.Gold;
            try
            {
                if (f.PlayerRelationKind == FactionRelationKind.Hostile) return UiKit.Bad;
                if (f.PlayerRelationKind == FactionRelationKind.Ally) return UiKit.Good;
                return f.HasGoodwill && f.PlayerGoodwill < 0 ? UiKit.Warn : UiKit.Faint;
            }
            catch
            {
                return UiKit.Faint;
            }
        }
    }
}
