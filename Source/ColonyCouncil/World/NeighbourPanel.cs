using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The region map panel's three lists of sections, kept for one release as a forwarding shim: the panel is now the
    /// place card (UI/Cards/), and Rimfolk's own fourteen sections moved into its slots through
    /// <see cref="CardSections"/> (CardSections.Town, TownLines, PlaceLines, HomeLines) and <see cref="CardActs"/>. What
    /// other code still adds here keeps drawing on the place card: a town's in its Ties slot, any other place's and a
    /// home's in its Now slot. Each drawer draws inside the rect it is given, from its top, and returns the height it used.
    /// New code registers with CardSections instead.
    /// </summary>
    public static class NeighbourPanel
    {
        /// <summary>Sections for a town of another people (drawn in the place card's Ties slot).</summary>
        public static readonly List<Func<Rect, Settlement, float>> Sections = new List<Func<Rect, Settlement, float>>();

        /// <summary>
        /// The same for any other place: a tile, a site or another world object (the world object is null for a bare
        /// tile). Not called for the towns of other peoples nor for a home (drawn in the place card's Now slot).
        /// </summary>
        public static readonly List<Func<Rect, PlanetTile, WorldObject, float>> PlaceSections = new List<Func<Rect, PlanetTile, WorldObject, float>>();

        /// <summary>The same for one of the colony's homes (drawn in the place card's Now slot).</summary>
        public static readonly List<Func<Rect, Map, float>> HomeSections = new List<Func<Rect, Map, float>>();
    }
}
