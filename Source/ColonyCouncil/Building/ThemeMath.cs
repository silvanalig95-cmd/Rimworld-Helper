using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Pure rules for room themes, with no game types, so the tests can check every theme the way the game applies it.
    /// A theme swaps a piece only for one of the same shape: the same footprint, no work spot the original didn't have,
    /// nothing that blocks where the original let people through, and the same facing rules unless it's a single cell
    /// with no work spot. Then the room's layout stays as it was designed, in every turn and mirrored.
    /// </summary>
    public static class ThemeMath
    {
        /// <summary>What the layout rules need to know about a piece of furniture.</summary>
        public struct Shape
        {
            public int sizeX, sizeZ;
            public bool rotatable, passable;
            /// <summary>Work spots for a north facing, relative to the root.</summary>
            public List<(int x, int z)> spots;
        }

        private static readonly List<(int x, int z)> NoSpots = new List<(int x, int z)>();

        /// <summary>Walls, doors and pieces built into the wall are the room's shell: a theme leaves them as they are.</summary>
        public static bool Themable(string role, bool inWall) =>
            !inWall && !string.IsNullOrEmpty(role) && role != "wall" && role != "door";

        /// <summary>Swapping <paramref name="a"/> for <paramref name="b"/> keeps the layout exactly as it is.</summary>
        public static bool KeepsShape(Shape a, Shape b)
        {
            if (a.sizeX != b.sizeX || a.sizeZ != b.sizeZ) return false;
            // A walk-through piece (a bedroll, a chair) can't become one that blocks the way.
            if (a.passable && !b.passable) return false;
            var aSpots = a.spots ?? NoSpots;
            var bSpots = b.spots ?? NoSpots;
            foreach (var s in bSpots)
                if (!aSpots.Contains(s)) return false;
            if (a.rotatable != b.rotatable)
            {
                // Only a single cell without a work spot looks the same whichever way it faces.
                if (a.sizeX != 1 || a.sizeZ != 1 || aSpots.Count > 0 || bSpots.Count > 0) return false;
            }
            return true;
        }

        /// <summary>
        /// A swap may change the look, never what the piece is for: a hospital bed stays a hospital bed (a theme's bedroll
        /// or simple bed would tend patients worse).
        /// </summary>
        public static bool KeepsPurpose(bool baseIsMedical, bool candidateIsMedical) => !baseIsMedical || candidateIsMedical;

        /// <summary>The theme's choices for a piece with this purpose, best first; several swap lists for one purpose are read in order.</summary>
        public static List<string> Candidates(string role, IEnumerable<(string role, List<string> defs)> swaps)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(role) || swaps == null) return result;
            foreach (var (r, defs) in swaps)
            {
                if (r != role || defs == null) continue;
                foreach (var d in defs)
                    if (!string.IsNullOrEmpty(d) && !result.Contains(d)) result.Add(d);
            }
            return result;
        }

        /// <summary>"comfortable" or "lavish" (the default): from which style on the plan adds plant pots along the walls.</summary>
        public static int DecorFrom(string decorFrom) => decorFrom == "comfortable" ? TemplateLayout.Comfortable : TemplateLayout.Lavish;

        /// <summary>The plan adds plant pots along free walls in this style.</summary>
        public static bool AddsDecor(int style, int decorFrom) => style >= decorFrom;

        /// <summary>The rug goes down in the lavish style only, whatever the theme.</summary>
        public static bool AddsRug(int style) => style == TemplateLayout.Lavish;
    }
}
