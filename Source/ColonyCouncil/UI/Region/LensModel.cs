using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One row of a lens's List: the thing its card is, what it shows, and what can be done from it.</summary>
    public sealed class LensRow
    {
        public ThingRef thing;
        /// <summary>Where it is on the map ("Show"), or null (a fight on its way).</summary>
        public WorldObject place;
        public Faction faction;
        public Pawn pawn;
        public Texture2D icon;
        public Color tone = Color.white;
        /// <summary>Its name, a link to its card.</summary>
        public string name;
        /// <summary>A state chip's word and colour ("friendly", "thriving").</summary>
        public string state;
        public Color stateColor;
        /// <summary>A second line, links allowed.</summary>
        public string line;
        public string tip;
        /// <summary>A quiet note at the right (how far, when).</summary>
        public string note;
        /// <summary>A fortune that moved this season: -1, 0 or 1 (an arrow beside the state).</summary>
        public int moved;
        public DipAct act;
        public RegionViewsMath.RowOrder order;
        public PlanetTile tile = PlanetTile.Invalid;
        internal string distance;
        internal bool distanceDone;
    }

    /// <summary>A group of a List's rows under its heading ("Raiders' camps 1").</summary>
    public sealed class LensGroup
    {
        public string title;
        /// <summary>Its fold's key (kept per lens and group for the session).</summary>
        public string key;
        public Texture2D icon;
        public readonly List<LensRow> rows = new List<LensRow>();
    }

    /// <summary>A line on the map between two world objects.</summary>
    public readonly struct LensLine
    {
        public readonly int from, to;
        public readonly LineKind kind;

        public LensLine(int from, int to, LineKind kind)
        {
            this.from = from;
            this.to = to;
            this.kind = kind;
        }
    }

    /// <summary>
    /// What one lens shows (docs/design/interface.md, "The region map as the hub"): each mark's colour by world object, the
    /// marks with a world event or a fortune that moved, the lines between places, the legend at the map's foot, and the
    /// List's rows in groups with what came before (from the history). Built at most once a second while the Map view
    /// shows (<see cref="Get"/>), from what the region already keeps: the region's towns and their records, the peoples'
    /// standing, the ledger's ties (debts of honour, fealty, grudges; pacts and truces when they come), the raiders'
    /// camps, trading posts and hamlets, the fights on their way, the world's events, the colony's homes and caravans.
    /// Nothing is built while the window is closed.
    /// </summary>
    public sealed class LensModel
    {
        public Lens lens;
        /// <summary>A mark's colour by its world object's id; a mark not here keeps its own.</summary>
        public readonly Dictionary<int, Color> colours = new Dictionary<int, Color>();
        /// <summary>Marks with a fortune that moved this season, by id: -1 or 1.</summary>
        public readonly Dictionary<int, int> arrows = new Dictionary<int, int>();
        /// <summary>Marks with a world event at them (a ring in the world's colour).</summary>
        public readonly HashSet<int> events = new HashSet<int>();
        /// <summary>Under Stance and ties, a town's head's regard of the colony as a ring in its colour, by the town's id.</summary>
        public readonly Dictionary<int, Color> rings = new Dictionary<int, Color>();
        /// <summary>Tiles of world events at no town (a dot drawn there).</summary>
        public readonly List<PlanetTile> eventTiles = new List<PlanetTile>();
        public readonly List<LensLine> lines = new List<LensLine>();
        public readonly List<LegendItem> legend = new List<LegendItem>();
        public readonly List<LensGroup> groups = new List<LensGroup>();
        /// <summary>What came before (from the history), at the List's foot, with its heading.</summary>
        public readonly List<string> past = new List<string>();
        public string pastTitle;
        public int rows;
        /// <summary>What the lens shows, in one line.</summary>
        public string line;
        /// <summary>What the List says with no rows: what will show and what brings it.</summary>
        public string empty;

        private static LensModel current;
        private static float builtAt = -100f;
        private static Map builtHome;

        /// <summary>The model of a lens, built again when it is more than a second old or another lens is asked for.</summary>
        public static LensModel Get(Lens lens)
        {
            float now = Time.realtimeSinceStartup;
            var home = Home;
            if (current != null && current.lens == lens && builtHome == home && now - builtAt < 1f && now >= builtAt) return current;
            try
            {
                current = Build(lens, home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region map: the " + lens + " lens", e);
                current = new LensModel { lens = lens };
            }
            builtAt = now;
            builtHome = home;
            return current;
        }

        /// <summary>Built again at the next ask (an act changed what it shows, a game loaded).</summary>
        public static void Dirty() => builtAt = -100f;

        /// <summary>The home the region map is drawn around (as RegionMap.HomeTile picks it).</summary>
        public static Map Home => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome ? Find.CurrentMap : HappeningTracker.HomeMap() ?? Find.AnyPlayerHomeMap;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Building =================

        private sealed class Place
        {
            public WorldObject wo;
            public PlaceFacts facts;
            public RegionViewsMath.RowKind row;
            public float tiles;
        }

        private static LensModel Build(Lens lens, Map home)
        {
            var m = new LensModel { lens = lens, line = ("CC_Lens_" + lens + "_Line").Translate(), empty = ("CC_Lens_" + lens + "_Empty").Translate() };
            if (Current.Game == null || Find.World == null) return m;
            var places = Places(home);
            foreach (var p in places)
            {
                var tone = RegionViewsMath.MarkTone(lens, p.facts);
                if (tone != Hue.Plain) m.colours[p.wo.ID] = UiKit.ToneColor(tone);
                if (lens == Lens.Fortunes)
                {
                    if (p.facts.moved != 0) m.arrows[p.wo.ID] = p.facts.moved;
                    if (p.facts.eventHere) m.events.Add(p.wo.ID);
                }
            }
            if (lens == Lens.Fortunes) EventTiles(m);
            if (lens == Lens.Stance) HeadRings(m);
            Lines(m, home);
            Legend(m);
            Rows(m, home, places);
            m.rows = m.groups.Sum(g => g.rows.Count);
            return m;
        }

        /// <summary>Every world object on the map's reach around the home, with the facts its tone and row are made from.</summary>
        private static List<Place> Places(Map home)
        {
            var list = new List<Place>();
            if (!RegionMap.HomeTile(out var layer, out var homeTile)) return list;
            int reach = Mathf.Clamp(ColonyCouncilMod.Settings.regionMapRadius, RegionMapView.ReachMin, RegionMapView.ReachMax);
            var player = Faction.OfPlayerSilentFail;
            int now = Now;
            var eventPlaces = new HashSet<Ref>();
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            if (ws != null)
                foreach (var s in ws.Running)
                    if (!s.place.IsNone)
                        eventPlaces.Add(s.place);
            var grid = Find.WorldGrid;
            foreach (var wo in Find.WorldObjects.AllWorldObjectsOnLayer(layer))
            {
                if (wo == null || wo.Destroyed) continue;
                float tiles;
                try
                {
                    tiles = grid.ApproxDistanceInTiles(homeTile, wo.Tile);
                }
                catch (Exception)
                {
                    continue;
                }
                if (tiles > reach + 0.5f) continue;
                var p = new Place { wo = wo, tiles = tiles };
                var f = new PlaceFacts { fortune = -1 };
                if (wo is Settlement s)
                {
                    if (s.Faction == player)
                    {
                        f.kind = PlaceKind.Home;
                        p.row = RegionViewsMath.RowKind.Home;
                        var map = s.Map;
                        f.threatened = map != null && Threatened(map);
                    }
                    else
                    {
                        f.kind = TradingPosts.IsPost(s.ID) ? PlaceKind.Post : Hamlets.IsHamlet(s.ID) ? PlaceKind.Hamlet : PlaceKind.Town;
                        p.row = f.kind == PlaceKind.Post ? RegionViewsMath.RowKind.Post : f.kind == PlaceKind.Hamlet ? RegionViewsMath.RowKind.Hamlet : RegionViewsMath.RowKind.Town;
                        f.stance = StanceOf(s.Faction);
                        var fortune = RegionFortunes.Of(s.ID);
                        if (fortune.HasValue)
                        {
                            f.fortune = (int)fortune.Value;
                            var rec = WorldLedger.TownRecord(s.ID);
                            if (rec != null) f.moved = RegionViewsMath.MovedThisSeason(rec.causeDir, rec.causeAt, now, GenDate.TicksPerSeason);
                        }
                        f.trades = f.stance != Stance.Sour && Trades(s);
                        f.eventHere = eventPlaces.Contains(Ref.Town(s.ID));
                        f.truce = home != null && Truces.KeepsAway(s.Faction, home);
                    }
                }
                else if (wo is Caravan c)
                {
                    if (!c.IsPlayerControlled) continue;
                    f.kind = PlaceKind.Caravan;
                    p.row = RegionViewsMath.RowKind.Caravan;
                }
                else if (wo is Site site && RaidersCamps.CampAt(site, out _) is RaidersCamp camp && camp.Shown)
                {
                    f.kind = PlaceKind.Camp;
                    p.row = RegionViewsMath.RowKind.Camp;
                }
                else if (RiseFall.RuinOf(wo) != null)
                {
                    f.kind = PlaceKind.Ruin;
                    p.row = RegionViewsMath.RowKind.Ruin;
                }
                else if (WorldLedger.FoundingAt(wo.ID) is LedgerFounding founding && founding.built < 0)
                {
                    f.kind = PlaceKind.Building;
                    p.row = RegionViewsMath.RowKind.Building;
                }
                else if (wo is MapParent || wo is Site)
                {
                    f.kind = PlaceKind.Other;
                    p.row = RegionViewsMath.RowKind.Other;
                }
                else continue;
                p.facts = f;
                list.Add(p);
            }
            return list;
        }

        /// <summary>A home with a threat now: raiders on their way to it, a raiders' camp against it, or enemies on its map.</summary>
        private static bool Threatened(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp != null)
                foreach (var r in comp.happenings.raids)
                    if (r != null && r.map == map)
                        return true;
            var camp = RaidersCamps.CampOf(map);
            if (camp != null && camp.Shown) return true;
            try
            {
                return GenHostility.AnyHostileActiveThreatToPlayer(map);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Trades(Settlement s)
        {
            try
            {
                return s.TraderKind != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A people's stance with the colony in three words (RegionMapMath's standing).</summary>
        public static Stance StanceOf(Faction f)
        {
            if (f == null) return Stance.Cool;
            if (f.IsPlayer) return Stance.Ours;
            int kind = f.PlayerRelationKind == FactionRelationKind.Hostile ? -1 : f.PlayerRelationKind == FactionRelationKind.Ally ? 1 : 0;
            int goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            return RegionViewsMath.StanceOf(RegionMapMath.StandingOf(false, f.HasGoodwill, kind, goodwill, f.def != null && f.def.permanentEnemy));
        }

        private static string StanceWord(Stance s) => ("CC_Lens_Stance_" + s).Translate();

        /// <summary>Each town's head the colony knows by name: how they regard the colony, as a ring around the town's mark.</summary>
        private static void HeadRings(LensModel m)
        {
            var book = NotableBook.Instance;
            if (book == null || !NotableBook.On) return;
            foreach (var n in book.notables)
                if (n != null && n.role == NotableRole.Head && n.Alive && n.town >= 0 && !m.rings.ContainsKey(n.town))
                    m.rings[n.town] = NotableBook.RegardColor(NotableBook.Opinion(n));
        }

        private static void EventTiles(LensModel m)
        {
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            if (ws == null) return;
            foreach (var s in ws.Running)
                if (s.place.kind == RefKind.Tile)
                {
                    var t = RiseFall.TileOf(s.place);
                    if (t.Valid) m.eventTiles.Add(t);
                }
        }

        // ================= Lines =================

        private static void Lines(LensModel m, Map home)
        {
            var homeObj = home?.Parent;
            if (homeObj == null) return;
            var seen = new HashSet<long>();
            void Add(WorldObject a, WorldObject b, LineKind k)
            {
                if (a == null || b == null || a == b || !RegionViewsMath.Draws(m.lens, k)) return;
                long key = ((long)a.ID << 32 | (uint)b.ID) * 16 + (int)k;
                if (seen.Add(key)) m.lines.Add(new LensLine(a.ID, b.ID, k));
            }
            // A hamlet's oath is drawn as the holding it is under Ours, protection under Stance.
            if (m.lens == Lens.Ours)
                foreach (var (h0, hamlet) in Hamlets.All())
                {
                    var town = Hamlets.TownOf(hamlet);
                    if (town != null) Add(h0?.Parent, town, LineKind.Holding);
                }
            if (m.lens == Lens.Stance || m.lens == Lens.Threats || m.lens == Lens.Ours) TieLines(m, home, homeObj, Add);
            if (m.lens == Lens.Threats)
                foreach (var (h1, camp) in RaidersCamps.Known())
                    Add(RaidersCamps.SiteOf(camp), h1?.Parent, LineKind.Raids);
            if (m.lens == Lens.Trade)
                foreach (var (_, post) in TradingPosts.All())
                {
                    var town = TradingPosts.TownOf(post);
                    Add(town, MotherOf(town), LineKind.Post);
                }
        }

        private static readonly Tie.Kind[] LineTies = { Tie.Kind.Pact, Tie.Kind.Truce, Tie.Kind.Favour, Tie.Kind.Fealty, Tie.Kind.Grudge };

        /// <summary>The ledger's ties with the colony on one end, each as a line from home to the place of the other end.</summary>
        private static void TieLines(LensModel m, Map home, WorldObject homeObj, Action<WorldObject, WorldObject, LineKind> add)
        {
            var ties = new List<Tie>();
            foreach (var k in LineTies) ties.AddRange(WorldLedger.LiveTies(k));
            foreach (var t in WorldLedger.Today())
                if (t != null && t.Live && (t.kind == Tie.Kind.Fealty || t.kind == Tie.Kind.Pact || t.kind == Tie.Kind.Truce))
                    ties.Add(t);
            foreach (var t in ties)
            {
                bool fromOurs = Ours(t.from), toOurs = Ours(t.to);
                if (fromOurs == toOurs) continue;
                var kind = RegionViewsMath.TieLine(t.kind, fromOurs);
                if (!kind.HasValue) continue;
                var place = PlaceOf(fromOurs ? t.to : t.from, home);
                if (place != null) add(homeObj, place, kind.Value);
            }
        }

        /// <summary>One end of a tie is the colony's: one of its homes, its people, or one of its own.</summary>
        private static bool Ours(Ref r)
        {
            switch (r.kind)
            {
                case RefKind.Colony: return true;
                case RefKind.People: return Refs.Faction(r)?.IsPlayer ?? false;
                case RefKind.Person:
                    var p = Refs.Pawn(r);
                    return p != null && p.Faction != null && p.Faction.IsPlayer;
                default: return false;
            }
        }

        /// <summary>Where the other end of a tie is on the map: a town itself, a notable's town, or a people's town nearest the home.</summary>
        private static WorldObject PlaceOf(Ref r, Map home)
        {
            switch (r.kind)
            {
                case RefKind.Town: return Refs.Town(r);
                case RefKind.Person:
                    var n = NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.Person == r);
                    return n != null && n.town >= 0 ? NotableBook.TownOf(n.town) : null;
                case RefKind.People:
                    var f = Refs.Faction(r);
                    var reach = RegionTowns.Of(home);
                    if (f == null || reach == null) return null;
                    foreach (var s in reach.towns)
                        if (s != null && s.Faction == f && !s.Destroyed)
                            return s;
                    return null;
                default: return null;
            }
        }

        /// <summary>The town a trading post belongs to: the town it came from where the ledger has it, else its people's town nearest it.</summary>
        private static Settlement MotherOf(Settlement post)
        {
            if (post == null || post.Faction == null) return null;
            var rec = WorldLedger.TownRecord(post.ID);
            if (rec != null && rec.mother.kind == RefKind.Town)
            {
                var mother = Refs.Town(rec.mother);
                if (mother != null && !mother.Destroyed) return mother;
            }
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == post || s.Faction != post.Faction || s.Destroyed || TradingPosts.IsPost(s.ID)) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(post.Tile, s.Tile);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        // ================= The legend =================

        private static LegendItem Dot(string key, Color c) => new LegendItem { word = key.Translate(), color = c };

        private static LegendItem LineItem(LineKind k) => new LegendItem { word = ("CC_Lens_Line_" + k).Translate(), line = RegionViewsMath.Look(k), color = UiKit.ToneColor(RegionViewsMath.Look(k).tone) };

        private static void Legend(LensModel m)
        {
            var l = m.legend;
            switch (m.lens)
            {
                case Lens.Land:
                    l.AddRange(RegionMapView.LandLegend());
                    return;
                case Lens.Stance:
                    l.Add(Dot("CC_Lens_Leg_Warm", UiKit.Good));
                    l.Add(Dot("CC_Lens_Leg_Cool", UiKit.Cool));
                    l.Add(Dot("CC_Lens_Leg_Sour", UiKit.Bad));
                    l.Add(Dot("CC_Lens_Leg_Ours", UiKit.Gold));
                    l.Add(new LegendItem { word = "CC_Lens_Leg_HeadRing".Translate(), color = UiKit.Faint, ring = true });
                    break;
                case Lens.Fortunes:
                    l.Add(Dot("CC_Lens_Leg_Thriving", UiKit.Good));
                    l.Add(Dot("CC_Lens_Leg_Steady", UiKit.Cool));
                    l.Add(Dot("CC_Lens_Leg_Poor", UiKit.Warn));
                    l.Add(Dot("CC_Lens_Leg_Struggling", UiKit.Bad));
                    l.Add(new LegendItem { word = "CC_Lens_Leg_Rose".Translate(), icon = Icons.TrendUp, color = UiKit.Good });
                    l.Add(new LegendItem { word = "CC_Lens_Leg_Fell".Translate(), icon = Icons.TrendDown, color = UiKit.Bad });
                    l.Add(Dot("CC_Lens_Leg_Event", UiKit.WorldColor));
                    l.Add(Dot("CC_Lens_Leg_Ruin", UiKit.Cool));
                    return;
                case Lens.Threats:
                    l.Add(Dot("CC_Lens_Leg_Hostile", UiKit.Bad));
                    l.Add(Dot("CC_Lens_Leg_Threatened", UiKit.Bad));
                    l.Add(Dot("CC_Lens_Leg_Ours", UiKit.Gold));
                    break;
                case Lens.Trade:
                    l.Add(Dot("CC_Lens_Leg_Post", UiKit.Good));
                    l.Add(Dot("CC_Lens_Leg_Trades", UiKit.Cool));
                    l.Add(Dot("CC_Lens_Leg_Ours", UiKit.Gold));
                    break;
                case Lens.Ours:
                    l.Add(Dot("CC_Lens_Leg_OursAll", UiKit.Gold));
                    break;
            }
            foreach (var k in RegionViewsMath.LinesOf(m.lens)) l.Add(LineItem(k));
        }

        // ================= The List =================

        private static void Rows(LensModel m, Map home, List<Place> places)
        {
            var byGroup = new SortedDictionary<int, LensGroup>();
            void Put(int group, LensRow r)
            {
                if (group < 0 || r == null) return;
                if (!byGroup.TryGetValue(group, out var g))
                {
                    g = new LensGroup { title = GroupTitle(m.lens, group), icon = GroupIcon(m.lens, group), key = m.lens + ":" + group };
                    byGroup[group] = g;
                }
                g.rows.Add(r);
            }
            foreach (var p in places)
            {
                int group = RegionViewsMath.Group(m.lens, p.row);
                if (group < 0) continue;
                // Threats lists hostile towns only; trade the towns that trade.
                if (m.lens == Lens.Threats && (p.row == RegionViewsMath.RowKind.Town || p.row == RegionViewsMath.RowKind.Post) && p.facts.stance != Stance.Sour) continue;
                if (m.lens == Lens.Trade && p.row == RegionViewsMath.RowKind.Town && !p.facts.trades) continue;
                var r = Row(m.lens, p, home);
                if (r == null) continue;
                r.order = new RegionViewsMath.RowOrder(group, RegionViewsMath.Key(m.lens, p.row, p.facts.fortune, p.facts.moved, 0f, p.wo is Settlement hs && hs.Map == Homes.Seat), p.tiles, ThingRefs.Name(r.thing));
                Put(group, r);
            }
            if (m.lens == Lens.Threats) Fights(home, r => Put(RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Fight), r));
            if (m.lens == Lens.Ours) Settlers(r => Put(RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Hamlet), r));
            foreach (var g in byGroup.Values)
            {
                g.rows.Sort((a, b) => RegionViewsMath.Compare(a.order, b.order));
                m.groups.Add(g);
            }
            Past(m);
        }

        private static string GroupTitle(Lens lens, int group) => ("CC_Lens_" + lens + "_Group" + group).Translate();

        private static Texture2D GroupIcon(Lens lens, int group)
        {
            switch (lens)
            {
                case Lens.Threats: return group == 0 ? Icons.Raid : group == 1 ? Icons.Warning : Icons.LensThreats;
                case Lens.Trade: return group == 0 ? Icons.Trader : group == 1 ? Icons.LensTrade : Icons.ModeMap;
                case Lens.Ours: return group == 0 ? Icons.Colony : group == 1 ? Icons.LensOurs : Icons.Trader;
                case Lens.Fortunes: return group == 0 ? Icons.LensFortunes : Icons.History;
                default: return Icons.World;
            }
        }

        private static LensRow Row(Lens lens, Place p, Map home)
        {
            var wo = p.wo;
            var r = new LensRow { place = wo, thing = ThingRefs.Of(wo), tile = wo.Tile, faction = wo.Faction, tone = UiKit.ToneColor(RegionViewsMath.MarkTone(lens, p.facts)) };
            if (lens == Lens.Land) r.tone = Color.white;
            string label = wo.LabelCap;
            r.name = ThingRefs.Link(label, r.thing);
            r.icon = wo.ExpandingIcon == BaseContent.BadTex ? null : wo.ExpandingIcon;
            switch (p.row)
            {
                case RegionViewsMath.RowKind.Town:
                case RegionViewsMath.RowKind.Post:
                case RegionViewsMath.RowKind.Hamlet:
                    TownRow(lens, r, (Settlement)wo, p, home);
                    break;
                case RegionViewsMath.RowKind.Home:
                    HomeRow(r, (Settlement)wo, p);
                    break;
                case RegionViewsMath.RowKind.Camp:
                    CampRow(r, (Site)wo);
                    break;
                case RegionViewsMath.RowKind.Caravan:
                    CaravanRow(r, (Caravan)wo);
                    break;
                case RegionViewsMath.RowKind.Ruin:
                    var ruin = RiseFall.RuinOf(wo);
                    r.faction = ruin?.people;
                    r.state = "CC_Lens_Ruin".Translate();
                    r.stateColor = UiKit.Cool;
                    if (ruin != null && ruin.since >= 0)
                        r.line = (ruin.why == LedgerRuin.Fight ? "CC_Lens_RuinFell" : "CC_Lens_RuinGivenUp").Translate(FactionMemory.AgoText(ruin.since).Named("AGO")).Resolve();
                    break;
                case RegionViewsMath.RowKind.Building:
                    var f = WorldLedger.FoundingAt(wo.ID);
                    r.faction = f?.people;
                    r.state = "CC_Lens_Building".Translate();
                    r.stateColor = UiKit.Cool;
                    if (f?.people != null) r.line = "CC_Lens_BuildingBy".Translate(ThingRefs.Link(f.people.Name, ThingRefs.Of(f.people)).Named("PEOPLE")).Resolve();
                    break;
                default:
                    return null;
            }
            return r;
        }

        private static void TownRow(Lens lens, LensRow r, Settlement s, Place p, Map home)
        {
            var f = s.Faction;
            switch (lens)
            {
                case Lens.Fortunes:
                    var rec = WorldLedger.TownRecord(s.ID);
                    if (p.facts.fortune >= 0)
                    {
                        r.state = RegionFortunes.Word((Fortune)p.facts.fortune, f);
                        r.stateColor = UiKit.ToneColor(RegionViewsMath.FortuneTone((Fortune)p.facts.fortune));
                        r.moved = p.facts.moved;
                        string why = RegionFortunes.Line(rec);
                        r.line = why.NullOrEmpty() ? CardWords.TownKind(s) : why;
                    }
                    else
                    {
                        r.state = "CC_Lens_NoFortune".Translate();
                        r.stateColor = UiKit.Dim;
                        r.line = CardWords.TownKind(s);
                    }
                    break;
                case Lens.Trade when p.row == RegionViewsMath.RowKind.Post:
                    var post = TradingPosts.PostAt(s.ID, out var postHome);
                    if (post != null)
                    {
                        bool warned = post.warnedAt >= 0 || post.closing >= 0;
                        r.state = (warned ? "CC_Lens_PostWarned" : "CC_Lens_PostOpen").Translate();
                        r.stateColor = warned ? UiKit.Warn : UiKit.Good;
                        var lines = TradingPostWords.Lines(postHome, post, true);
                        r.line = lines.Count > 0 ? lines[0] : CardWords.TownKind(s);
                        r.tip = string.Join("\n", lines.Select(LinkTextMath.Plain));
                    }
                    break;
                case Lens.Ours when p.row == RegionViewsMath.RowKind.Hamlet:
                    var hamlet = Hamlets.HamletAt(s.ID, out var hHome);
                    if (hamlet != null)
                    {
                        r.state = "CC_Lens_Hamlet".Translate();
                        r.stateColor = UiKit.Gold;
                        var lines = HamletWords.Lines(hHome, hamlet, true);
                        r.line = lines.Count > 0 ? lines[0] : CardWords.TownKind(s);
                        r.tip = string.Join("\n", lines.Select(LinkTextMath.Plain));
                    }
                    break;
                default:
                    var stance = p.facts.stance;
                    r.state = StanceWord(stance);
                    r.stateColor = UiKit.ToneColor(RegionViewsMath.ToneOf(stance));
                    r.line = CardWords.TownKind(s);
                    if (lens == Lens.Stance) Notables(r, s);
                    break;
            }
        }

        /// <summary>
        /// A town's people the colony knows by name on its row (the Stance list, which was the Neighbours page): each a
        /// link with their title ("Edda Marsh, head"), and in the row's tooltip how each regards the colony.
        /// </summary>
        private static void Notables(LensRow r, Settlement s)
        {
            var book = NotableBook.Instance;
            if (book == null || !NotableBook.On) return;
            var parts = new List<string>();
            var tips = new List<string>();
            foreach (var n in book.notables.Where(n => n != null && n.town == s.ID && n.Alive).OrderBy(n => (int)n.role))
            {
                string title = NotableBook.TitleOf(n);
                string regard = NotableBook.RegardWords(NotableBook.Opinion(n));
                if (parts.Count < 4) parts.Add("CC_Lens_Notable".Translate(ThingRefs.Link(n.Name, ThingRefs.Of(n)).Named("NAME"), title.Named("TITLE")).Resolve());
                tips.Add("CC_Lens_NotableTip".Translate(n.Name.Named("NAME"), title.Named("TITLE"), regard.Named("REGARD")).Resolve());
            }
            r.line = parts.Count == 0 ? "CC_Lens_NobodyKnown".Translate().Resolve() : string.Join(" · ", parts);
            if (tips.Count > 0) r.tip = string.Join("\n", tips);
        }

        private static void HomeRow(LensRow r, Settlement s, Place p)
        {
            var map = s.Map;
            r.state = map != null && map == Homes.Seat && Homes.All.Count > 1 ? "CC_Lens_Seat".Translate() : "CC_Lens_Home".Translate();
            r.stateColor = UiKit.Gold;
            r.faction = null;
            if (map != null)
            {
                int people = IncidentWorker_Happening.Colonists(map).Count;
                r.line = "CC_Lens_HomePeople".Translate(people.Named("COUNT")).Resolve();
                if (p.facts.threatened) r.line += " · " + "CC_Lens_ThreatNow".Translate().Resolve();
            }
        }

        private static void CampRow(LensRow r, Site site)
        {
            var c = RaidersCamps.CampAt(site, out var home);
            if (c == null) return;
            r.faction = c.people;
            r.name = ThingRefs.Link(c.label ?? site.LabelCap, r.thing);
            var lines = RaidersCampPage.Lines(home, c, true);
            r.state = c.stage == CampStage.Paying ? "CC_Lens_CampPaying".Translate() : c.raids == 0 ? "CC_Lens_CampNoRaids".Translate() : "CC_Lens_CampRaids".Translate(c.raids.Named("RAIDS")).Resolve();
            r.stateColor = UiKit.Bad;
            r.line = string.Join(" ", lines.Skip(1).Take(3));
            r.tip = string.Join("\n", lines);
            if (c.offered && c.stage == CampStage.Found)
            {
                var camp = c;
                var h = home;
                string why = RaidersCampTerms.CantPay(home, c);
                r.act = new DipAct
                {
                    label = "CC_RaidCamp_Pay".Translate(c.price.Named("SILVER")).Resolve(),
                    why = why,
                    tip = "CC_RaidCampPg_PayTip".Translate(),
                    run = () => RaidersCampPage.AskToPay(h, camp)
                };
            }
        }

        private static void CaravanRow(LensRow r, Caravan c)
        {
            r.faction = null;
            int people = c.PawnsListForReading.Count(p => p.RaceProps.Humanlike);
            r.state = "CC_Lens_Caravan".Translate();
            r.stateColor = UiKit.Gold;
            string line = "CC_JourneysPg_People".Translate(people).Resolve();
            if (c.pather != null && c.pather.Moving)
            {
                var to = Find.WorldObjects.SettlementAt(c.pather.Destination);
                line += " · " + (to != null ? "CC_JourneysPg_HeadingTo".Translate(ThingRefs.Link(to.LabelCap, ThingRefs.Of(to)).Named("PLACE")).Resolve() : "CC_JourneysPg_OnTheMove".Translate().Resolve());
            }
            else line += " · " + "CC_JourneysPg_Resting".Translate().Resolve();
            r.line = line;
        }

        /// <summary>The fights on their way (promised by a people: a feud, a revenge), each to the home they are coming for.</summary>
        private static void Fights(Map home, Action<LensRow> put)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            int now = Now;
            foreach (var raid in comp.happenings.raids)
            {
                if (raid?.faction == null) continue;
                string title = raid.label.NullOrEmpty() ? "CC_World_AFight".Translate().ToString() : raid.label;
                var target = raid.map?.Parent;
                var r = new LensRow
                {
                    thing = ThingRefs.Of(raid.faction),
                    place = target,
                    faction = raid.faction,
                    icon = Icons.Raid,
                    tone = UiKit.Bad,
                    name = title,
                    state = "CC_Lens_FightComing".Translate(),
                    stateColor = UiKit.Bad,
                    line = ThingRefs.Link(raid.faction.Name, ThingRefs.Of(raid.faction)) + (Homes.All.Count > 1 && raid.map != null ? " · " + Homes.Name(raid.map) : ""),
                    tip = raid.text,
                    note = "CC_Week_In".Translate(UiKit.Duration(Mathf.Max(0, raid.tick - now))).Resolve()
                };
                r.order = new RegionViewsMath.RowOrder(RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Fight),
                    RegionViewsMath.Key(Lens.Threats, RegionViewsMath.RowKind.Fight, -1, 0, (raid.tick - now) / (float)GenDate.TicksPerDay, false), 0f, title);
                put(r);
            }
        }

        /// <summary>Settlers on their way to a hamlet of the colony's (no town of theirs yet): who, where they are, what takes them along.</summary>
        private static void Settlers(Action<LensRow> put)
        {
            if (RegionBook.Instance == null) return;
            foreach (var (home, h) in Hamlets.All())
            {
                if (h == null || h.stage == HamletStage.None || h.stage == HamletStage.Over || Hamlets.TownOf(h) != null) continue;
                var target = HamletPage.Target(h);
                var lines = HamletWords.Lines(home, h, true);
                var r = new LensRow
                {
                    thing = target.HasWorldObject ? ThingRefs.Of(target.WorldObject) : h.people != null ? ThingRefs.Of(h.people) : ThingRef.None,
                    place = target.HasWorldObject ? target.WorldObject : null,
                    tile = target.Tile,
                    faction = h.people,
                    icon = Icons.LensOurs,
                    tone = UiKit.Gold,
                    name = HamletPage.Title(h),
                    state = "CC_Lens_Settlers".Translate(),
                    stateColor = UiKit.Warn,
                    line = lines.Count > 0 ? lines[0] : null,
                    tip = string.Join("\n", lines.Select(LinkTextMath.Plain))
                };
                r.order = new RegionViewsMath.RowOrder(RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Hamlet), 1f, 0f, r.name);
                put(r);
            }
        }

        /// <summary>What came before, from the history: how the last camps, posts or hamlets went, newest first.</summary>
        private static void Past(LensModel m)
        {
            string family;
            string key;
            switch (m.lens)
            {
                case Lens.Threats: family = "world:camp"; key = "CC_RaidCampPg_PastLine"; m.pastTitle = "CC_RaidCampPg_Past".Translate(); break;
                case Lens.Trade: family = TradingPostMath.Family; key = "CC_PostPg_PastLine"; m.pastTitle = "CC_PostPg_Past".Translate(); break;
                case Lens.Ours: family = HamletMath.Family; key = "CC_HamletPg_PastLine"; m.pastTitle = "CC_HamletPg_Past".Translate(); break;
                default: return;
            }
            var seen = new HashSet<int>();
            var entries = new List<HistoryEntry>();
            foreach (var home in Homes.All)
                foreach (var e in History.At(Refs.Colony(home), new[] { family }, int.MinValue, 12))
                    if (e != null && seen.Add(e.id) && e.kind != RaidersCampMath.KindRaid)
                        entries.Add(e);
            foreach (var e in entries.OrderByDescending(e => e.tick).Take(12))
                m.past.Add(key.Translate(FactionMemory.AgoText(e.tick).CapitalizeFirst().Named("AGO"), (CardWords.HistoryLine(e, true) ?? e.kind).Named("TEXT")).Resolve());
        }
    }
}
