using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace ColonyCouncil
{
    /// <summary>
    /// What the region map's panel shows for a settlement besides its name, people and distance: other parts of
    /// Rimfolk add their own sections here (the notables and the letters). Each drawer draws inside the rect it is
    /// given, from its top, and returns the height it used.
    /// </summary>
    public static class NeighbourPanel
    {
        public static readonly List<Func<Rect, Settlement, float>> Sections = new List<Func<Rect, Settlement, float>>();
    }
}
