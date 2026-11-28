using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The peoples' older events told in the visiting people's own age (docs/content-round-7-peoples-ages.md): rounds 1 to 3
    /// and releases C to G, which were written in the colony's age, take each people's age as rounds 4 to 10 do. The words go
    /// by the tags "age0" to "age3" (PeopleEvents.AgeTags); what a people brings, asks or pays in goods goes by these rules.
    /// The colony's own answer stays in the colony's own means. Pure (no game types), so Tools/Tests (PeoplesAgesTests) checks it.
    /// </summary>
    public static class PplAgesMath
    {
        public const int Tribal = 0, Medieval = 1, Industrial = 2, Spacer = 3;

        private static int Age(int a) => Math.Max(Tribal, Math.Min(Spacer, a));

        /// <summary>
        /// The age of a piece a people hands the colony (the Wheel's mended weapon): never past their own means, and never past
        /// the colony's, as it was before.
        /// </summary>
        public static int GoodsAge(int colony, int people) => Math.Min(Age(colony), Age(people));

        /// <summary>
        /// The learned society leaves a schematic only when it is of the industrial age or later, and the colony is past the
        /// medieval age, as before; else a textbook.
        /// </summary>
        public static bool Schematic(int society, bool colonyPreIndustrial) => Age(society) >= Industrial && !colonyPreIndustrial;

        /// <summary>A Signal Line of the tribal or medieval age keeps flag towers, not masts of steel (LineCrew.Tower's rule).</summary>
        public static bool Tower(int line) => Age(line) < Industrial;

        /// <summary>The wire-trader of a Line of the old ages buys cloth for the towers' flags instead of components for masts.</summary>
        public static bool Flags(int line) => Tower(line);

        /// <summary>The cloth an old Line's wire-trader asks for: as much as the colony has, within these, about the worth of the components' range (4 to 20).</summary>
        public const int ClothMin = 80, ClothMax = 400;

        /// <summary>The Deepwell Union's crate holds components and shells only from a union of the industrial age or later.</summary>
        public static bool Machined(int union) => Age(union) >= Industrial;

        /// <summary>
        /// The steel that stands in an old union's crate for the components it can't make: the components' worth in steel, to the
        /// nearest ten, so the crate and its price stay what they were.
        /// </summary>
        public static int SteelFor(int components, float componentWorth, float steelWorth)
        {
            if (components <= 0 || componentWorth <= 0f || steelWorth <= 0f || float.IsNaN(componentWorth) || float.IsNaN(steelWorth)) return 0;
            return (int)Math.Round(components * componentWorth / steelWorth / 10f) * 10;
        }

        /// <summary>
        /// The kind of medicine in the Helix Works' case, by the Works' age: 3 glitterworld medicine with the industrial (spacer),
        /// 2 industrial medicine (industrial), 1 herbal medicine (medieval, the Works' old knowledge of plants).
        /// </summary>
        public static int CaseTier(int works) => Math.Max(Medieval, Age(works));

        /// <summary>Herbal medicine for the industrial medicine an old Works' case would have held: about the same worth.</summary>
        public static int HerbalFor(int medicine, float industrialWorth, float herbalWorth)
        {
            if (medicine <= 0 || industrialWorth <= 0f || herbalWorth <= 0f || float.IsNaN(industrialWorth) || float.IsNaN(herbalWorth)) return 0;
            return Math.Max(1, (int)Math.Round(medicine * industrialWorth / herbalWorth));
        }

        /// <summary>The Lattice brings a turret only when it is of the industrial age or later; a Lattice fallen back can't make one.</summary>
        public static bool Turret(int lattice) => Age(lattice) >= Industrial;

        /// <summary>
        /// A master of the Kiln Guild reworks only a piece of the guild's own age or before (a piece's age by its tech level,
        /// PeopleAgesMath.AgeOfTech): a guild of the tribal age a spear or a bow, never a gun.
        /// </summary>
        public static bool CanWork(int pieceAge, int guild) => Age(pieceAge) <= Age(guild);
    }
}
