using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One section a feature adds to a kind of card: drawn by its own code, or lines the card keeps with its model.</summary>
    public sealed class CardSection
    {
        /// <summary>Draws inside the rect it is given, from its top, and returns the height it used (0: nothing to show).</summary>
        public Func<Rect, ThingRef, float> draw;
        /// <summary>Or: its lines (links allowed), asked for once a second with the card's model; none or empty shows nothing.</summary>
        public Func<ThingRef, List<string>> lines;
        /// <summary>The heading's text key; none draws the section without a heading of the card's.</summary>
        public string heading;
        /// <summary>The count beside the heading, or -1.</summary>
        public Func<ThingRef, int> count;
    }

    /// <summary>
    /// What features add to a kind of card, in a slot (docs/design/interface.md, "Things and their cards"): a section
    /// drawn by its own code, or lines the card asks for once a second. In a slot, sections come in their order (the
    /// card's own sections have orders too: 0 for the first). The place card's helpers take what the region map's panel
    /// gave its sections (a town of another people, any other place, the home) and are what NeighbourPanel's three lists
    /// became. Call them once, from a static constructor with [StaticConstructorOnStartup]. A section added again by the
    /// same id replaces the first.
    /// </summary>
    public static class CardSections
    {
        internal static readonly CardRegistry<CardSection> registry = new CardRegistry<CardSection>();

        /// <summary>A section drawn by its own code: <paramref name="draw"/> draws inside the rect from its top and returns the height used.</summary>
        public static void Register(CardKind kind, CardSlot slot, float order, Func<Rect, ThingRef, float> draw, string heading = null, string id = null,
            Func<ThingRef, int> count = null)
        {
            if (draw == null || slot == CardSlot.Head || slot == CardSlot.Acts) return;
            registry.Add(kind, slot, order, id ?? heading ?? "section" + registry.Count, new CardSection { draw = draw, heading = heading, count = count });
        }

        /// <summary>A section of lines (with links where they name things), asked for once a second while the card shows.</summary>
        public static void RegisterLines(CardKind kind, CardSlot slot, float order, Func<ThingRef, List<string>> lines, string heading = null, string id = null)
        {
            if (lines == null || slot == CardSlot.Head || slot == CardSlot.Acts) return;
            registry.Add(kind, slot, order, id ?? heading ?? "lines" + registry.Count, new CardSection { lines = lines, heading = heading });
        }

        // ---------------- The place card ----------------

        /// <summary>Another people's town while it stands (what NeighbourPanel.Sections drew).</summary>
        public static Settlement TownOf(ThingRef r)
        {
            if (r.kind != ThingKind.Town) return null;
            var s = ThingRefs.Settlement(r);
            return s != null && (s.Faction == null || !s.Faction.IsPlayer) ? s : null;
        }

        /// <summary>A home of the colony with its map (what NeighbourPanel.HomeSections drew for the home the region map is drawn around).</summary>
        public static Map HomeOf(ThingRef r) => r.kind == ThingKind.Colony ? ThingRefs.Home(r) : null;

        /// <summary>Any other place: a site or another world object, a bare tile, a colony with no home map (what NeighbourPanel.PlaceSections drew).</summary>
        public static bool OtherPlace(ThingRef r, out PlanetTile tile, out WorldObject wo)
        {
            tile = PlanetTile.Invalid;
            wo = null;
            if (!r.IsPlace || r.kind == ThingKind.Town || HomeOf(r) != null) return false;
            wo = ThingRefs.WorldObject(r);
            tile = ThingRefs.TileOf(r);
            return tile.Valid;
        }

        public static void Town(CardSlot slot, float order, Func<Rect, Settlement, float> draw, string heading = null, string id = null, Func<Settlement, int> count = null)
        {
            if (draw == null) return;
            Register(CardKind.Place, slot, order, (rect, r) =>
            {
                var s = TownOf(r);
                return s == null ? 0f : draw(rect, s);
            }, heading, id, count == null ? (Func<ThingRef, int>)null : r =>
            {
                var s = TownOf(r);
                return s == null ? -1 : count(s);
            });
        }

        public static void TownLines(CardSlot slot, float order, Func<Settlement, List<string>> lines, string heading = null, string id = null)
        {
            if (lines == null) return;
            RegisterLines(CardKind.Place, slot, order, r =>
            {
                var s = TownOf(r);
                return s == null ? null : lines(s);
            }, heading, id);
        }

        public static void PlaceLines(CardSlot slot, float order, Func<PlanetTile, WorldObject, List<string>> lines, string heading = null, string id = null)
        {
            if (lines == null) return;
            RegisterLines(CardKind.Place, slot, order, r => OtherPlace(r, out var tile, out var wo) ? lines(tile, wo) : null, heading, id);
        }

        public static void HomeLines(CardSlot slot, float order, Func<Map, List<string>> lines, string heading = null, string id = null)
        {
            if (lines == null) return;
            RegisterLines(CardKind.Place, slot, order, r =>
            {
                var home = HomeOf(r);
                return home == null ? null : lines(home);
            }, heading, id);
        }
    }

    /// <summary>One act a feature adds to a kind of card.</summary>
    public sealed class CardActEntry
    {
        public Func<ThingRef, string> label;
        /// <summary>Why it can't be done now (shown greyed with the reason), or null when it can.</summary>
        public Func<ThingRef, string> whyNot;
        public Action<ThingRef> act;
        /// <summary>Whether it belongs on this thing's card at all (null: always).</summary>
        public Func<ThingRef, bool> shows;
        public string tip;
    }

    /// <summary>
    /// What the colony can do from a kind of card: the bar at its foot, each act greyed with why not, as the right-click
    /// menu does already; asked once a second with the card's model. Call it once, from a static constructor.
    /// </summary>
    public static class CardActs
    {
        internal static readonly CardRegistry<CardActEntry> registry = new CardRegistry<CardActEntry>();

        public static void Register(CardKind kind, string label, Func<ThingRef, string> whyNot, Action<ThingRef> act, Func<ThingRef, bool> shows = null,
            float order = 50f, string tip = null, string id = null)
        {
            if (label.NullOrEmpty()) return;
            Register(kind, _ => label.Translate().Resolve(), whyNot, act, shows, order, tip, id ?? label);
        }

        /// <summary>The same with a label that changes with the thing (already in words).</summary>
        public static void Register(CardKind kind, Func<ThingRef, string> label, Func<ThingRef, string> whyNot, Action<ThingRef> act, Func<ThingRef, bool> shows = null,
            float order = 50f, string tip = null, string id = null)
        {
            if (label == null || act == null) return;
            registry.Add(kind, CardSlot.Acts, order, id ?? "act" + registry.Count, new CardActEntry { label = label, whyNot = whyNot, act = act, shows = shows, tip = tip });
        }
    }
}
