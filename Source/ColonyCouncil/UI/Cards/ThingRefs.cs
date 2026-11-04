using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's side of <see cref="ThingRef"/>: making one from what the game has (a pawn, a people, a town, any world
    /// object, a tie, a history entry, a letter), finding what one names while the game still has it, whether it is
    /// still there, its name and its card. Everything here is safe once the thing is gone: a name falls back to what the
    /// ledger, the notables or the history kept, a lookup gives null. Names are kept a few seconds, world objects found
    /// by id are kept while they stand, so the windows ask freely.
    /// </summary>
    public static class ThingRefs
    {
        public static ThingRef Of(Pawn p) => p == null ? ThingRef.None : ThingRef.From(Refs.Of(p));

        public static ThingRef Of(Faction f) => f == null || f.IsPlayer ? ThingRef.None : ThingRef.From(Refs.Of(f));

        /// <summary>A settlement: one of the colony's homes is a colony, anyone else's a town.</summary>
        public static ThingRef Of(Settlement s) => s == null ? ThingRef.None : ThingRef.From(Refs.Place(s));

        /// <summary>Any place on the world: a settlement as a town or a colony, anything else by its world object.</summary>
        public static ThingRef Of(WorldObject o) => o == null ? ThingRef.None : o is Settlement s ? Of(s) : ThingRef.Site(o.ID);

        public static ThingRef Of(Tie t) => t == null || t.IsView ? ThingRef.None : ThingRef.Tie(t.id);

        public static ThingRef Of(HistoryEntry e) => e == null ? ThingRef.None : ThingRef.Event(e.id);

        public static ThingRef Of(NeighbourLetter l) => l == null ? ThingRef.None : ThingRef.Letter(l.id);

        public static ThingRef Of(Notable n) => n == null ? ThingRef.None : ThingRef.From(n.Person);

        public static ThingRef Of(Ref r) => ThingRef.From(r);

        /// <summary>A tile of the planet, or what stands on it (a settlement, another world object).</summary>
        public static ThingRef OfTile(PlanetTile t)
        {
            if (!t.Valid) return ThingRef.None;
            try
            {
                var s = Find.WorldObjects.SettlementAt(t);
                if (s != null) return Of(s);
            }
            catch (Exception)
            {
                // Another layer's tile: the tile itself.
            }
            return ThingRef.From(Refs.Of(t));
        }

        // ---------------- What it names ----------------

        /// <summary>The pawn behind a person, alive or dead, while the game keeps them.</summary>
        public static Pawn Pawn(ThingRef r) => r.kind == ThingKind.Person ? Refs.Pawn(r.ToRef()) : null;

        public static Faction Faction(ThingRef r) => r.kind == ThingKind.People ? Refs.Faction(r.ToRef()) : null;

        /// <summary>A notable's record for a person, or null.</summary>
        public static Notable Notable(ThingRef r)
        {
            if (r.kind != ThingKind.Person) return null;
            var book = NotableBook.Instance;
            if (book == null) return null;
            var lr = r.ToRef();
            foreach (var n in book.notables)
                if (n != null && n.person == lr)
                    return n;
            return null;
        }

        public static Tie Tie(ThingRef r) => r.kind == ThingKind.Tie ? WorldLedger.Get(r.id) : null;

        public static HistoryEntry Entry(ThingRef r) => r.kind == ThingKind.Event ? History.Get(r.id) : null;

        public static NeighbourLetter Letter(ThingRef r)
        {
            if (r.kind != ThingKind.Letter) return null;
            var book = NotableBook.Instance;
            if (book == null) return null;
            foreach (var l in book.letters)
                if (l != null && l.id == r.id)
                    return l;
            return null;
        }

        public static LedgerHouse House(ThingRef r)
        {
            if (r.kind != ThingKind.House) return null;
            var houses = WorldLedger.Instance?.Houses;
            if (houses == null) return null;
            var lr = r.ToRef();
            foreach (var h in houses)
                if (h != null && h.house == lr)
                    return h;
            return null;
        }

        private static readonly Dictionary<ThingRef, WorldObject> objects = new Dictionary<ThingRef, WorldObject>();

        /// <summary>The world object a place is (a town's or a colony's settlement, a site's object) while it stands; null for a tile or anything gone.</summary>
        public static WorldObject WorldObject(ThingRef r)
        {
            if (r.kind != ThingKind.Town && r.kind != ThingKind.Colony && r.kind != ThingKind.Site) return null;
            if (objects.TryGetValue(r, out var o) && o != null && !o.Destroyed && o.ID == r.id) return o;
            o = null;
            try
            {
                var all = Find.WorldObjects?.AllWorldObjects;
                if (all != null)
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] != null && all[i].ID == r.id && !all[i].Destroyed)
                        {
                            o = all[i];
                            break;
                        }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: a place's world object", e);
            }
            if (objects.Count > 200) objects.Clear();
            objects[r] = o;
            return o;
        }

        /// <summary>The settlement of a town or a colony while it stands.</summary>
        public static Settlement Settlement(ThingRef r) => WorldObject(r) as Settlement;

        /// <summary>The home map of a colony while it is one.</summary>
        public static Map Home(ThingRef r) => r.kind == ThingKind.Colony ? Refs.Home(r.ToRef()) : null;

        /// <summary>Where a place is on the planet: its world object's tile, a tile's own; invalid for anything else or gone.</summary>
        public static PlanetTile TileOf(ThingRef r)
        {
            if (r.kind == ThingKind.Tile)
            {
                var lr = r.ToRef();
                try
                {
                    var grid = Find.WorldGrid;
                    PlanetLayer layer = null;
                    if (grid != null)
                        foreach (var kv in grid.PlanetLayers)
                            if (kv.Value != null && kv.Value.LayerID == lr.TileLayer)
                            {
                                layer = kv.Value;
                                break;
                            }
                    return layer != null ? new PlanetTile(lr.TileId, layer) : PlanetTile.Invalid;
                }
                catch (Exception)
                {
                    return PlanetTile.Invalid;
                }
            }
            var o = WorldObject(r);
            return o != null ? o.Tile : PlanetTile.Invalid;
        }

        // ---------------- Is it still there ----------------

        /// <summary>
        /// The thing is still something a card can show: anyone the ledger, the notables or the game still knows; a town
        /// standing or remembered; a people not gone; a colony, a house, a tie, an entry or a letter still kept; a place
        /// still on the world.
        /// </summary>
        public static bool Exists(ThingRef r)
        {
            try
            {
                switch (r.kind)
                {
                    case ThingKind.Person:
                        return WorldLedger.PersonRecord(r.ToRef()) != null || Notable(r) != null || Pawn(r) != null;
                    case ThingKind.Town:
                        return WorldObject(r) != null || WorldLedger.TownRecord(r.id) != null || WorldLedger.Town(r.id) != null;
                    case ThingKind.People:
                        return Faction(r) != null;
                    case ThingKind.Colony:
                        return WorldObject(r) != null || Home(r) != null;
                    case ThingKind.House:
                        return House(r) != null;
                    case ThingKind.Tile:
                        return TileOf(r).Valid;
                    case ThingKind.Site:
                        return WorldObject(r) != null;
                    case ThingKind.Tie:
                        return Tie(r) != null;
                    case ThingKind.Event:
                        return Entry(r) != null;
                    case ThingKind.Letter:
                        return Letter(r) != null;
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: whether a thing is there", e);
                return false;
            }
        }

        /// <summary>The card a thing opens: a person of the colony's own (a colonist, a slave, a prisoner of it) a colonist's, anyone else a person's.</summary>
        public static CardKind CardOf(ThingRef r)
        {
            if (r.kind != ThingKind.Person) return r.Card;
            var p = r.id >= 0 ? Pawn(r) : null;
            bool ours = p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && (p.IsColonist || p.IsSlaveOfColony || p.IsPrisonerOfColony);
            return r.CardOf(ours);
        }

        // ---------------- Names ----------------

        private static readonly Dictionary<ThingRef, (float at, string name)> names = new Dictionary<ThingRef, (float, string)>();

        /// <summary>Its name as Rimfolk's windows show it, kept a few seconds; "?" for a thing nobody knows any more.</summary>
        public static string Name(ThingRef r)
        {
            if (r.IsNone) return "";
            float now = Time.realtimeSinceStartup;
            if (names.TryGetValue(r, out var e) && now - e.at < 5f) return e.name;
            string n;
            try
            {
                n = MakeName(r);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("cards: a thing's name", ex);
                n = null;
            }
            if (n.NullOrEmpty()) n = "?";
            if (names.Count > 500) names.Clear();
            names[r] = (now, n);
            return n;
        }

        private static string MakeName(ThingRef r)
        {
            switch (r.kind)
            {
                case ThingKind.Person:
                {
                    var p = Pawn(r);
                    if (p != null && !p.Discarded) return p.LabelShort;
                    return WorldLedger.NameOf(r.ToRef()) ?? Notable(r)?.Name;
                }
                case ThingKind.Town:
                {
                    var s = Settlement(r);
                    if (s != null && s.HasName) return s.Name;
                    return WorldLedger.TownRecord(r.id)?.name ?? WorldLedger.Town(r.id)?.name;
                }
                case ThingKind.People:
                    return Faction(r)?.Name;
                case ThingKind.Colony:
                {
                    var home = Home(r);
                    if (home != null) return Homes.Name(home);
                    var s = Settlement(r);
                    return s != null ? s.LabelCap.ToString() : null;
                }
                case ThingKind.House:
                {
                    var h = House(r);
                    return h != null ? RegionWords.HouseName(h) : null;
                }
                case ThingKind.Tile:
                    return "CC_Card_Tile".Translate().Resolve();
                case ThingKind.Site:
                    return WorldObject(r)?.LabelCap.ToString();
                case ThingKind.Tie:
                {
                    var t = Tie(r);
                    return t != null ? CardWords.TieName(t) : null;
                }
                case ThingKind.Event:
                {
                    var e = Entry(r);
                    return e != null ? WorldHistoryMath.Words(e.kind).CapitalizeFirst() : null;
                }
                case ThingKind.Letter:
                {
                    var l = Letter(r);
                    return l != null ? CardWords.LetterName(l) : null;
                }
                default:
                    return null;
            }
        }

        /// <summary>A name shown as a link to its thing (<see cref="LinkTextMath.Wrap"/>), for text drawn in Rimfolk's windows only.</summary>
        public static string Link(string label, ThingRef r) => r.IsNone ? label : LinkTextMath.Wrap(label, r.ToString());

        /// <summary>A thing's own name as a link to it.</summary>
        public static string Link(ThingRef r) => Link(Name(r), r);

        /// <summary>Forget what was kept (a game loaded).</summary>
        public static void Reset()
        {
            names.Clear();
            objects.Clear();
        }
    }
}
