using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the content round's camps and world events (docs/content-update-proposals-2.md, D33 to D44): the
    /// diggers' share and their night at the old graves, the herd kept apart, the stone-raisers' stone by age, the
    /// smugglers' night, the lord's named beast, and the world events' small numbers. No game types here: Tools/Tests
    /// checks them. The parts are Camps/CampRound2.cs and World/Variety/WorldRound2.cs.
    /// </summary>
    public static class Round2Math
    {
        // ================= D33. Diggers at the old place =================

        /// <summary>What the diggers carry to the gate as the colony's share on their last day.</summary>
        public enum DigFind
        {
            /// <summary>An old worn thing out of the ground (a blade, a buckle, a coat), of the colony's age or older.</summary>
            Worn,
            /// <summary>An inert relic (Vanilla Ideology Expanded - Relics and Artifacts' bone, scroll or medallion).</summary>
            Relic,
            /// <summary>Now and then something of worth, sealed up and buried long ago, as the old powers' buried finds give.</summary>
            Sealed
        }

        /// <summary>One share in this many holds something sealed.</summary>
        public const float SealedChance = 0.2f;

        /// <summary>The share by its roll: something sealed now and then, else an inert relic where the mod is loaded, else an old worn thing.</summary>
        public static DigFind Share(bool relicsLoaded, float roll) => roll < SealedChance ? DigFind.Sealed : relicsLoaded && roll < 0.65f ? DigFind.Relic : DigFind.Worn;

        /// <summary>Let dig the graves too, they bring a second thing.</summary>
        public static int ShareCount(bool graves) => graves ? 2 : 1;

        /// <summary>Told to keep to the ruins, one of them goes to the old graves with a spade one night in a stay at this chance.</summary>
        public const float NightAtGravesChance = 0.35f;

        /// <summary>How near an awake colonist must be to see the digger at the graves.</summary>
        public const float SeenRange = 22f;

        /// <summary>Caught at it: a colonist awake and near enough to see it.</summary>
        public static bool Caught(float distance, bool awake) => awake && distance >= 0f && distance <= SeenRange;

        /// <summary>The camp's tension after one of them is caught at the graves: at the brawl's mark (CampBrawls: 70), so an evening at their fire can come to blows.</summary>
        public static float CaughtTension(float tension) => Math.Max(tension + 30f, 70f);

        /// <summary>Not caught, only the turned earth found in the morning: the camp's unease rises a little.</summary>
        public const float FoundTension = 10f;

        // ================= D34. A herd kept apart =================

        /// <summary>Their herd and the colony's beasts this near (cells) have met.</summary>
        public const float MetRange = 6f;

        /// <summary>The herds met: the game's own animal flu comes to the colony's herd at this chance (once a stay).</summary>
        public const float FluChanceMet = 0.5f;

        /// <summary>The colony's best with animals tended their beasts and came back among the colony's: the flu at this chance (once).</summary>
        public const float FluChanceTended = 0.25f;

        /// <summary>
        /// The corner of the map farthest from the colony (by its middle), inset from the edge: where a herd kept apart camps.
        /// </summary>
        public static (int x, int z) FarCorner(int width, int height, int cx, int cz, int inset)
        {
            inset = Math.Max(0, Math.Min(inset, Math.Min(width, height) / 2 - 1));
            var corners = new[] { (inset, inset), (width - 1 - inset, inset), (inset, height - 1 - inset), (width - 1 - inset, height - 1 - inset) };
            (int x, int z) best = corners[0];
            long bestD = -1;
            foreach (var c in corners)
            {
                long dx = c.Item1 - cx, dz = c.Item2 - cz;
                long d = dx * dx + dz * dz;
                if (d <= bestD) continue;
                bestD = d;
                best = c;
            }
            return best;
        }

        // ================= D35. The stone-raisers =================

        /// <summary>
        /// The god's stone in the form of the raisers' own age, each the first that exists: Ideology's totemic slab (tribal),
        /// a megalith (medieval: Alpha Memes', Vanilla Furniture Expanded - Props and Decor's), an obelisk (industrial: Vanilla
        /// Furniture Expanded - Art's), a futuristic obelisk (spacer: Alpha Memes', Props and Decor's); the game's large
        /// sculpture after each, so every age has one.
        /// </summary>
        public static string[] StoneFor(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return new[] { "TotemicSlab_Medium", "AM_Megalith", "SculptureLarge" };
                case "medieval": return new[] { "AM_Megalith", "VFEPD_Megalith", "SculptureLarge" };
                case "industrial": return new[] { "VFE_Obelisk", "VFEPD_Obelisk", "SculptureLarge" };
                case "spacer": return new[] { "AM_FuturisticObelisk", "VFEPD_FuturisticObelisk", "SculptureLarge" };
                default: return new[] { "SculptureLarge" };
            }
        }

        /// <summary>Days of hauling and raising before it stands: two to four.</summary>
        public static int RaiseDays(float roll) => 2 + Math.Min(2, Math.Max(0, (int)(roll * 3f)));

        /// <summary>The stone they bring to raise it with, in their packs (blocks of the land's own rock).</summary>
        public const int StoneBlocks = 60;

        /// <summary>Where it stands: the kinds of place a stone is raised at, in order (a hill, the oldest tree, the water's edge, else the open ground).</summary>
        public static readonly string[] Places = { "hill", "tree", "water", "open" };

        // ================= D36. Smugglers by night =================

        /// <summary>The hours they sell by their lamp: from dusk until the small hours.</summary>
        public static bool NightHour(int hour) => hour >= 20 || hour < 4;

        /// <summary>Said nothing of them: one of the colony with a craving, or a believer of the cup, walks out on a night at this chance.</summary>
        public const float WalkOutChance = 0.3f;

        /// <summary>What such a walk costs: silver gone from the colony's stores for one bottle or twist, in tens.</summary>
        public static int WalkOutSilver(float marketValue) => Math.Max(10, (int)Math.Round(Math.Max(10f, marketValue * 1.5f) / 10f) * 10);

        // ================= D38. A lord's hunt =================

        /// <summary>
        /// The beast the lord names, from the land's wild beasts (each: what it is worth, whether it is of a god's sacred kind,
        /// whether it is the white beast): a god's own beast or one of a sacred kind first, else the one worth most. -1 for none.
        /// </summary>
        public static int Named(IList<(float value, bool sacred, bool godBeast)> beasts)
        {
            int best = -1;
            int bestRank = -1;
            float bestValue = -1f;
            for (int i = 0; i < (beasts?.Count ?? 0); i++)
            {
                var b = beasts[i];
                int rank = b.godBeast ? 2 : b.sacred ? 1 : 0;
                if (rank < bestRank || rank == bestRank && b.value <= bestValue) continue;
                best = i;
                bestRank = rank;
                bestValue = b.value;
            }
            return best;
        }

        /// <summary>A beast worth naming: the land's rarest are worth this much at least (a thrumbo, a royal tiger), unless of a sacred kind.</summary>
        public const float NamedWorth = 300f;

        /// <summary>Told to spare it, one of their hunters goes after it anyway at this chance, once, while the camp's tension is at <see cref="StrayTension"/> or more.</summary>
        public const float StrayChance = 0.35f;

        public const float StrayTension = 45f;

        public static bool MayStray(float tension, float roll) => tension >= StrayTension && roll < StrayChance;

        /// <summary>The lord's gift for leave to hunt anything: silver by how much the named beast is worth, in tens, 60 to 400.</summary>
        public static int HuntGift(float value) => Math.Max(60, Math.Min(400, (int)Math.Round(value * 0.25f / 10f) * 10));

        // ================= D39 to D44. The world events =================

        /// <summary>D40. One of the colony who danced with them catches the dancing fever, after the dancers slept indoors, at this chance (the harm budget's own after it).</summary>
        public const float FeverChance = 0.5f;

        /// <summary>D40. How near (cells) a colonist must be to the dancers to join them.</summary>
        public const float JoinRange = 18f;

        /// <summary>D41. Left as they lie, with Anomaly: a shambler rises from the colony's oldest grave at this chance (the harm budget's own after it), once.</summary>
        public const float RiseChance = 0.2f;

        /// <summary>D41. At most this many of the colony's graves are taken up for the burning.</summary>
        public const int BurnGraves = 6;

        /// <summary>D42. The bad coin found in the colony's silver when it is weighed: a twenty-fifth of it, 10 to 150, in tens.</summary>
        public static int BadCoin(int silver) => silver <= 0 ? 0 : Math.Max(10, Math.Min(150, (int)Math.Round(silver / 25f / 10f) * 10));

        /// <summary>D42. The money-changer's fee: 20 to 60 silver by how much there is to weigh, in tens.</summary>
        public static int ChangerFee(int silver) => Math.Max(20, Math.Min(60, (int)Math.Round(silver / 100f / 10f) * 10));

        /// <summary>D43. The colony's own slaves hear of the rising: their suppression falls by this much (the game's own measure that keeps a rebellion off).</summary>
        public const float SuppressionDip = 0.12f;

        /// <summary>D44. The stronger of two peoples: more towns, then more goodwill with nobody in particular (the first on a tie).</summary>
        public static bool FirstStronger(int firstTowns, int secondTowns) => firstTowns >= secondTowns;

        /// <summary>D44. A small toll for each claimant: half the beat's sum, in tens, 20 at least.</summary>
        public static int Toll(int count) => Math.Max(20, (int)Math.Round(count / 2f / 10f) * 10);
    }
}
