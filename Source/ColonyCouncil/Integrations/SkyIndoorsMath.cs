using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure part of keeping another mod's sky effects out of roofed rooms (Integrations/SkyIndoors.cs): the open-sky cells
    /// of a map as runs along each row, so a map of a few hundred rooms is drawn with a few hundred quads, and which of
    /// Dynamic Atmosphere's map materials are the sky's (cloud shadows, haze and the event tints) rather than rain and its
    /// ripples, which it already keeps out of roofed rooms itself.
    /// </summary>
    public static class SkyIndoorsMath
    {
        /// <summary>Dynamic Atmosphere's material for its cloud shadows on the map.</summary>
        public const string ShadowMaterial = "DA_MapCloudShadow";

        /// <summary>Its haze, which carries a condition's tint (the yellow of a heat wave).</summary>
        public const string HazeMaterial = "DA_MapHaze";

        /// <summary>Its event layers' materials begin so (DA_MapEvent0 to 3, DA_MapEventAdd0 to 3: flares, auroras, tints).</summary>
        public const string EventMaterialPrefix = "DA_MapEvent";

        /// <summary>A material of the open sky: drawn only over cells open to the sky.</summary>
        public static bool SkyMaterial(string name) =>
            !string.IsNullOrEmpty(name) && (name == ShadowMaterial || name == HazeMaterial || name.StartsWith(EventMaterialPrefix, System.StringComparison.Ordinal));

        /// <summary>One run of open cells in a row: x from <see cref="x0"/> to <see cref="x1"/> inclusive, at row <see cref="z"/>.</summary>
        public struct Run
        {
            public int x0, x1, z;

            public Run(int x0, int x1, int z)
            {
                this.x0 = x0;
                this.x1 = x1;
                this.z = z;
            }
        }

        /// <summary>
        /// The runs of open cells (open[z * width + x] true where the sky is seen: not roofed, not fogged), row by row, left
        /// to right. Empty where nothing is open; one run per row on a map with no roofs.
        /// </summary>
        public static List<Run> Runs(bool[] open, int width, int height)
        {
            var runs = new List<Run>();
            if (open == null || width <= 0 || height <= 0 || open.Length < width * height) return runs;
            for (int z = 0; z < height; z++)
            {
                int row = z * width, start = -1;
                for (int x = 0; x < width; x++)
                {
                    bool o = open[row + x];
                    if (o && start < 0) start = x;
                    else if (!o && start >= 0)
                    {
                        runs.Add(new Run(start, x - 1, z));
                        start = -1;
                    }
                }
                if (start >= 0) runs.Add(new Run(start, width - 1, z));
            }
            return runs;
        }

        /// <summary>
        /// The texture coordinate the game's whole-map plane gives a point (a plane 2000 cells wide around the draw's centre,
        /// its coordinates 0 to 200 across it), so a shader that reads them sees the same values on Rimfolk's open-sky mesh.
        /// </summary>
        public static float PlaneUv(float world, float center) => (world - center) / 10f + 100f;

        /// <summary>Vertices a mesh of these runs needs (four a run); above 65,000 it needs 32-bit indices.</summary>
        public static int Vertices(int runs) => runs * 4;

        public const int MaxVertices16 = 65000;

        /// <summary>Real seconds between two rebuilds of a map's open-sky mesh, however often its roofs change (as Dynamic Atmosphere's own roof mask).</summary>
        public const float RebuildEvery = 0.5f;
    }
}
