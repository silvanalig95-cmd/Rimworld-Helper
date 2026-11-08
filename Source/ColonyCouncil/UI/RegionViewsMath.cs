using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The region map's six lenses (docs/design/interface.md, "The region map as the hub"): one map read six ways. Each
    /// recolours the marks drawn each frame, may draw lines between places, has its legend at the map's foot and gives
    /// the List its rows. A seventh comes only by replacing one: a lens is a new way of reading the whole region, not a
    /// feature's page.
    /// </summary>
    public enum Lens
    {
        /// <summary>The map as it was: the land, its marks in their people's colours.</summary>
        Land,
        /// <summary>Towns by how their people stand with the colony, lines for the ties between them.</summary>
        Stance,
        /// <summary>Towns by their fortune, an arrow where it moved this season; ruins and towns being built.</summary>
        Fortunes,
        /// <summary>Hostile towns, raiders' camps, the home with a threat now; a camp's line to the home it raids.</summary>
        Threats,
        /// <summary>Trading posts and the towns that trade; each post's line to its town.</summary>
        Trade,
        /// <summary>The colony's homes, its hamlets and caravans; a line from home to each hamlet and vassal.</summary>
        Ours
    }

    /// <summary>What a mark or a line means, drawn in its colour (UiKit's tokens): never colour alone, always beside a word or an icon.</summary>
    public enum Hue
    {
        /// <summary>As it was drawn before any lens (its people's colour).</summary>
        Plain,
        Warm,
        Cool,
        Sour,
        /// <summary>Ours.</summary>
        Gold,
        /// <summary>Needs the colony soon: a debt it owes, a warning.</summary>
        Soon,
        /// <summary>Not what this lens is about: drawn faint.</summary>
        Dim,
        /// <summary>World events and news from afar.</summary>
        World
    }

    /// <summary>A line drawn between two places on the region map.</summary>
    public enum LineKind
    {
        Pact,
        Truce,
        /// <summary>They owe the colony a favour (a debt of honour).</summary>
        FavourOwedUs,
        /// <summary>The colony owes them one.</summary>
        FavourWeOwe,
        /// <summary>Protection: fealty either way (a liege's grant, a vassal's oath, a hamlet's).</summary>
        Fealty,
        /// <summary>A people holds a grudge against the colony.</summary>
        Grudge,
        /// <summary>A raiders' camp to the home it raids.</summary>
        Raids,
        /// <summary>A trading post to its town.</summary>
        Post,
        /// <summary>Home to a hamlet of its own.</summary>
        Holding
    }

    /// <summary>How a line is drawn.</summary>
    public enum Dash
    {
        Solid,
        /// <summary>Long dashes.</summary>
        Dashed,
        /// <summary>Dots.</summary>
        Dotted,
        /// <summary>Two lines side by side.</summary>
        Double,
        /// <summary>Short dashes, as a road on the move.</summary>
        Short
    }

    /// <summary>A line's look: its dashes, its tone and its width in pixels.</summary>
    public readonly struct LineLook
    {
        public readonly Dash dash;
        public readonly Hue tone;
        public readonly float width;

        public LineLook(Dash dash, Hue tone, float width)
        {
            this.dash = dash;
            this.tone = tone;
            this.width = width;
        }
    }

    /// <summary>A people's stance with the colony in three words and ours: friendly or allied, neutral or wary, hostile.</summary>
    public enum Stance
    {
        Ours,
        Warm,
        Cool,
        Sour
    }

    /// <summary>What kind of mark a place is, for its tone under a lens.</summary>
    public enum PlaceKind
    {
        /// <summary>One of the colony's homes.</summary>
        Home,
        /// <summary>Another people's settlement.</summary>
        Town,
        /// <summary>A trading post (a town of its own people kept as a post).</summary>
        Post,
        /// <summary>A hamlet of the colony's.</summary>
        Hamlet,
        /// <summary>A raiders' camp the colony found.</summary>
        Camp,
        /// <summary>A town given up.</summary>
        Ruin,
        /// <summary>A town being built.</summary>
        Building,
        /// <summary>A caravan of the colony's.</summary>
        Caravan,
        /// <summary>Any other site or place.</summary>
        Other
    }

    /// <summary>What a mark's tone is worked out from: a few plain facts about the place, gathered once a second.</summary>
    public struct PlaceFacts
    {
        public PlaceKind kind;
        public Stance stance;
        /// <summary>The town's fortune band, or -1 for none (not kept in detail, or fortunes off).</summary>
        public int fortune;
        /// <summary>Its fortune moved this season: -1 down, 1 up, 0 not.</summary>
        public int moved;
        /// <summary>A home with a threat now (a fight on its way, a raiders' camp against it).</summary>
        public bool threatened;
        /// <summary>A town whose people trade with the colony (not hostile, with traders of their own).</summary>
        public bool trades;
        /// <summary>A world event runs there.</summary>
        public bool eventHere;
        /// <summary>Its people hold a sworn truce with the home (Diplomacy/Truces.cs): the Threats lens dims it.</summary>
        public bool truce;
    }

    /// <summary>
    /// The pure part of the region window's views (UI/Region/): the lenses in their order, how each reads a place, the
    /// lines each draws and how, how ties of the ledger become lines, and the order of each lens's List. Tools/Tests
    /// checks it (RegionViewsTests).
    /// </summary>
    public static class RegionViewsMath
    {
        /// <summary>At most six lenses: a seventh only by replacing one.</summary>
        public const int MaxLenses = 6;

        /// <summary>The lenses in the order of the lens bar.</summary>
        public static readonly Lens[] Lenses = { Lens.Land, Lens.Stance, Lens.Fortunes, Lens.Threats, Lens.Trade, Lens.Ours };

        /// <summary>A lens by its name in an address ("Trade"), in any case.</summary>
        public static bool TryLens(string name, out Lens lens)
        {
            lens = Lens.Land;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var l in Lenses)
                if (string.Equals(l.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    lens = l;
                    return true;
                }
            return false;
        }

        // ---------------- Stance ----------------

        /// <summary>The game's standing in three words: allies and friends warm, fair to unfriendly and no dealings cool, hostile and enemies sour.</summary>
        public static Stance StanceOf(RegionMapMath.Standing s)
        {
            switch (s)
            {
                case RegionMapMath.Standing.Own: return Stance.Ours;
                case RegionMapMath.Standing.Allies:
                case RegionMapMath.Standing.Friendly: return Stance.Warm;
                case RegionMapMath.Standing.Hostile:
                case RegionMapMath.Standing.Enemies: return Stance.Sour;
                default: return Stance.Cool;
            }
        }

        public static Hue ToneOf(Stance s) => s == Stance.Ours ? Hue.Gold : s == Stance.Warm ? Hue.Warm : s == Stance.Sour ? Hue.Sour : Hue.Cool;

        /// <summary>A fortune band's tone: struggling sour, poor soon, steady cool, thriving and flourishing warm.</summary>
        public static Hue FortuneTone(Fortune f)
        {
            switch (f)
            {
                case Fortune.Struggling: return Hue.Sour;
                case Fortune.Poor: return Hue.Soon;
                case Fortune.Steady: return Hue.Cool;
                default: return Hue.Warm;
            }
        }

        /// <summary>
        /// Which way a town's fortune moved this season: its cause's direction while the cause is no older than a season
        /// (<paramref name="seasonTicks"/>), else 0.
        /// </summary>
        public static int MovedThisSeason(int causeDir, int causeAt, int now, int seasonTicks)
        {
            if (causeDir == 0 || causeAt < 0 || now < causeAt || now - causeAt > seasonTicks) return 0;
            return causeDir < 0 ? -1 : 1;
        }

        // ---------------- Marks ----------------

        /// <summary>
        /// A mark's tone under a lens. Land keeps the marks as they were. Stance: homes gold, towns and posts by their
        /// people's stance, camps sour, the rest dim. Fortunes: towns by fortune, ruins and towns being built cool, the
        /// rest dim. Threats: hostile towns and camps sour, a home with a threat now sour, other homes gold, the rest dim.
        /// Trade: posts warm, towns that trade cool, homes gold, the rest dim. Ours: homes, hamlets and caravans gold,
        /// the rest dim.
        /// </summary>
        public static Hue MarkTone(Lens lens, PlaceFacts p)
        {
            switch (lens)
            {
                case Lens.Stance:
                    switch (p.kind)
                    {
                        case PlaceKind.Home:
                        case PlaceKind.Hamlet: return Hue.Gold;
                        case PlaceKind.Town:
                        case PlaceKind.Post: return ToneOf(p.stance);
                        case PlaceKind.Camp: return Hue.Sour;
                        default: return Hue.Dim;
                    }
                case Lens.Fortunes:
                    switch (p.kind)
                    {
                        case PlaceKind.Town:
                        case PlaceKind.Post:
                        case PlaceKind.Hamlet: return p.fortune >= 0 ? FortuneTone((Fortune)Math.Min(p.fortune, (int)Fortune.Flourishing)) : p.eventHere ? Hue.World : Hue.Dim;
                        case PlaceKind.Ruin:
                        case PlaceKind.Building: return Hue.Cool;
                        default: return p.eventHere ? Hue.World : Hue.Dim;
                    }
                case Lens.Threats:
                    switch (p.kind)
                    {
                        case PlaceKind.Home: return p.threatened ? Hue.Sour : Hue.Gold;
                        case PlaceKind.Camp: return Hue.Sour;
                        case PlaceKind.Town:
                        case PlaceKind.Post: return p.stance == Stance.Sour && !p.truce ? Hue.Sour : Hue.Dim;
                        default: return Hue.Dim;
                    }
                case Lens.Trade:
                    switch (p.kind)
                    {
                        case PlaceKind.Home: return Hue.Gold;
                        case PlaceKind.Post: return Hue.Warm;
                        case PlaceKind.Town: return p.trades ? Hue.Cool : Hue.Dim;
                        case PlaceKind.Caravan: return Hue.Gold;
                        default: return Hue.Dim;
                    }
                case Lens.Ours:
                    switch (p.kind)
                    {
                        case PlaceKind.Home:
                        case PlaceKind.Hamlet:
                        case PlaceKind.Caravan: return Hue.Gold;
                        default: return Hue.Dim;
                    }
                default:
                    return Hue.Plain;
            }
        }

        // ---------------- Lines ----------------

        /// <summary>
        /// A line's look: a pact solid and warm, a truce dashed and cool, a favour owed dotted (warm owed to the colony,
        /// soon owed by it), protection double and gold, a grudge dotted and sour, a camp's raids in short dashes and sour,
        /// a post to its town thin and warm, home to a hamlet solid and gold.
        /// </summary>
        public static LineLook Look(LineKind k)
        {
            switch (k)
            {
                case LineKind.Pact: return new LineLook(Dash.Solid, Hue.Warm, 2f);
                case LineKind.Truce: return new LineLook(Dash.Dashed, Hue.Cool, 2f);
                case LineKind.FavourOwedUs: return new LineLook(Dash.Dotted, Hue.Warm, 2.5f);
                case LineKind.FavourWeOwe: return new LineLook(Dash.Dotted, Hue.Soon, 2.5f);
                case LineKind.Fealty: return new LineLook(Dash.Double, Hue.Gold, 1.2f);
                case LineKind.Grudge: return new LineLook(Dash.Dotted, Hue.Sour, 2f);
                case LineKind.Raids: return new LineLook(Dash.Short, Hue.Sour, 1.6f);
                case LineKind.Post: return new LineLook(Dash.Solid, Hue.Warm, 1.2f);
                default: return new LineLook(Dash.Solid, Hue.Gold, 1.6f);
            }
        }

        /// <summary>The lines a lens draws: none on the land and the fortunes, the ties under stance, raids under threats, posts under trade, holdings under ours.</summary>
        public static bool Draws(Lens lens, LineKind k)
        {
            switch (lens)
            {
                case Lens.Stance:
                    return k == LineKind.Pact || k == LineKind.Truce || k == LineKind.FavourOwedUs || k == LineKind.FavourWeOwe || k == LineKind.Fealty
                        || k == LineKind.Grudge;
                case Lens.Threats: return k == LineKind.Raids || k == LineKind.Grudge;
                case Lens.Trade: return k == LineKind.Post;
                case Lens.Ours: return k == LineKind.Holding || k == LineKind.Fealty;
                default: return false;
            }
        }

        /// <summary>The line kinds a lens draws, in the order of its legend.</summary>
        public static List<LineKind> LinesOf(Lens lens)
        {
            var list = new List<LineKind>();
            foreach (LineKind k in Enum.GetValues(typeof(LineKind)))
                if (Draws(lens, k))
                    list.Add(k);
            return list;
        }

        /// <summary>
        /// A tie of the ledger with the colony on one end as a line, or null for a kind no lens draws (a regard, a claim, a
        /// promise and the rest). <paramref name="colonyBound"/>: the colony is the party bound (it owes, it swore).
        /// </summary>
        public static LineKind? TieLine(Tie.Kind kind, bool colonyBound)
        {
            switch (kind)
            {
                case Tie.Kind.Pact: return LineKind.Pact;
                case Tie.Kind.Truce: return LineKind.Truce;
                case Tie.Kind.Favour: return colonyBound ? LineKind.FavourWeOwe : LineKind.FavourOwedUs;
                case Tie.Kind.Fealty: return LineKind.Fealty;
                // A grudge held against the colony; one the colony holds is a colonist's own story (a vendetta).
                case Tie.Kind.Grudge: return colonyBound ? (LineKind?)null : LineKind.Grudge;
                default: return null;
            }
        }

        /// <summary>
        /// Where a dash starts and ends along a line of <paramref name="length"/> pixels: the dash at <paramref name="i"/>
        /// as (start, end), false past the end. Solid and double are one dash; the rest repeat their own measure.
        /// </summary>
        public static bool DashAt(Dash d, float length, int i, out float start, out float end)
        {
            start = end = 0f;
            if (length <= 0f || i < 0) return false;
            float on, off;
            switch (d)
            {
                case Dash.Dashed: on = 8f; off = 6f; break;
                case Dash.Dotted: on = 1.5f; off = 5f; break;
                case Dash.Short: on = 4f; off = 4f; break;
                default:
                    if (i > 0) return false;
                    end = length;
                    return true;
            }
            start = i * (on + off);
            if (start >= length) return false;
            end = Math.Min(length, start + on);
            return true;
        }

        // ---------------- The List ----------------

        /// <summary>What a row of a lens's List is, for its group in the list.</summary>
        public enum RowKind
        {
            Home,
            Town,
            Post,
            Hamlet,
            Camp,
            /// <summary>A fight promised: raiders on their way.</summary>
            Fight,
            Ruin,
            Building,
            Caravan,
            Other
        }

        /// <summary>
        /// A row's group in a lens's List, lower first: Threats lists camps, then fights on their way, then hostile towns;
        /// Trade posts, then towns; Ours homes, then hamlets, then caravans; Fortunes towns, then ruins and towns being
        /// built; Land and Stance one group. -1: the row isn't in that lens's List.
        /// </summary>
        public static int Group(Lens lens, RowKind k)
        {
            switch (lens)
            {
                case Lens.Threats:
                    return k == RowKind.Camp ? 0 : k == RowKind.Fight ? 1 : k == RowKind.Town || k == RowKind.Post ? 2 : -1;
                case Lens.Trade:
                    return k == RowKind.Post ? 0 : k == RowKind.Town ? 1 : k == RowKind.Caravan ? 2 : -1;
                case Lens.Ours:
                    return k == RowKind.Home ? 0 : k == RowKind.Hamlet ? 1 : k == RowKind.Caravan ? 2 : -1;
                case Lens.Fortunes:
                    return k == RowKind.Town || k == RowKind.Post || k == RowKind.Hamlet ? 0 : k == RowKind.Ruin || k == RowKind.Building ? 1 : -1;
                default:
                    return k == RowKind.Town || k == RowKind.Post || k == RowKind.Hamlet ? 0 : -1;
            }
        }

        /// <summary>What a List's rows are sorted by: their group, a key within it (lower first), then the nearest, then the name.</summary>
        public readonly struct RowOrder
        {
            public readonly int group;
            public readonly float key;
            public readonly float tiles;
            public readonly string name;

            public RowOrder(int group, float key, float tiles, string name)
            {
                this.group = group;
                this.key = key;
                this.tiles = tiles;
                this.name = name ?? "";
            }
        }

        /// <summary>The List all in one, closest first (its "Closest first" chip): by tiles from home, then by name, whatever the group.</summary>
        public static int CompareClosest(RowOrder a, RowOrder b)
        {
            int c = a.tiles.CompareTo(b.tiles);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        }

        public static int Compare(RowOrder a, RowOrder b)
        {
            int c = a.group.CompareTo(b.group);
            if (c != 0) return c;
            c = a.key.CompareTo(b.key);
            if (c != 0) return c;
            c = a.tiles.CompareTo(b.tiles);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        }

        /// <summary>
        /// The key within a group: Fortunes from struggling to flourishing, a town whose fortune moved this season before
        /// one at the same band that didn't; a fight by when it comes (in days); the seat before other homes; else 0,
        /// nearest first.
        /// </summary>
        public static float Key(Lens lens, RowKind k, int fortune, int moved, float days, bool seat)
        {
            if (lens == Lens.Fortunes && fortune >= 0) return fortune - (moved != 0 ? 0.5f : 0f);
            if (lens == Lens.Fortunes) return 10f;
            if (k == RowKind.Fight) return days;
            if (k == RowKind.Home) return seat ? 0f : 1f;
            return 0f;
        }
    }
}
