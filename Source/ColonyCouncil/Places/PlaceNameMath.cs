using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the colony's own names for its places (C25 of docs/content-update-proposals.md, Places/PlaceNames.cs),
    /// without game types, so Tools/Tests can check them: what names a place, how big a place is, how many a home keeps, how
    /// close two may be, how a name fades once its place is gone, how other words find the place nearest a cell, and how the
    /// supper talk is paced.
    /// </summary>
    public static class PlaceNameMath
    {
        /// <summary>
        /// What named it: a god seen on the land, the colony's first grave out in the open, a gathering held beside a grown
        /// tree, a raid held at a gate, a flood that reached the colony's path or fields.
        /// </summary>
        public const string God = "god", Graves = "graves", Tree = "tree", Gate = "gate", Flood = "flood";

        public static readonly string[] Kinds = { God, Graves, Tree, Gate, Flood };

        /// <summary>At most this many named places on a home map.</summary>
        public const int MaxPlaces = 12;

        /// <summary>No new name within this many cells of a named place's middle: one place, one name.</summary>
        public const float MinGap = 10f;

        /// <summary>A place whose tree, gate or graves are gone fades from the label for this many days, then is forgotten.</summary>
        public const float FadeDays = 15f;

        /// <summary>The label of a place that is gone fades down to this at the end of its season.</summary>
        public const float FadedAlpha = 0.25f;

        /// <summary>Words about a cell find a place this many cells beyond its own size.</summary>
        public const float NearSlack = 2f;

        /// <summary>A tree a gathering is held beside: within this many cells of the gathering's spot.</summary>
        public const float TreeReach = 5f;

        /// <summary>Graves are named once someone of the colony is within this many cells of them.</summary>
        public const float GraveReach = 12f;

        /// <summary>A place is brought up at supper at most once in this many days at a home.</summary>
        public const float SupperGapDays = 4f;

        /// <summary>The chance an evening hour at supper brings one up, once the gap is past.</summary>
        public const float SupperChance = 0.2f;

        /// <summary>How big each kind of place is, in cells from its middle.</summary>
        public static int Radius(string kind)
        {
            switch (kind)
            {
                case God: return 6;
                case Graves: return 5;
                case Tree: return 4;
                case Gate: return 4;
                case Flood: return 7;
                default: return 5;
            }
        }

        public static bool IsKind(string kind) => Array.IndexOf(Kinds, kind) >= 0;

        /// <summary>Room for another name on the map.</summary>
        public static bool Room(int named) => named < MaxPlaces;

        /// <summary>Too close to a named place for a name of its own (squared distance in cells).</summary>
        public static bool TooClose(float distSquared) => distSquared < MinGap * MinGap;

        /// <summary>A cell at this squared distance from a place's middle is at that place.</summary>
        public static bool Near(float distSquared, int radius)
        {
            float r = radius + NearSlack;
            return distSquared <= r * r;
        }

        /// <summary>The label's strength: whole while the place stands, fading over its season once it is gone.</summary>
        public static float Alpha(int goneTick, int now, int dayTicks)
        {
            if (goneTick < 0 || now <= goneTick) return 1f;
            float t = (now - goneTick) / (FadeDays * dayTicks);
            if (t >= 1f) return FadedAlpha;
            return 1f - (1f - FadedAlpha) * t;
        }

        /// <summary>A place gone a whole season is forgotten: the name goes with it.</summary>
        public static bool Forgotten(int goneTick, int now, int dayTicks) => goneTick >= 0 && now - goneTick >= FadeDays * dayTicks;

        /// <summary>A place still to be spoken of: it stands (a place that is gone is only on the fading label).</summary>
        public static bool Spoken(int goneTick) => goneTick < 0;

        /// <summary>
        /// What was seen, made definite for a name: "a great white thrumbo" becomes "the great white thrumbo", "an old man" "the
        /// old man"; a name ("Bellwether") or anything else stays as it is.
        /// </summary>
        public static string Definite(string words)
        {
            if (string.IsNullOrEmpty(words)) return words;
            if (words.StartsWith("a ", StringComparison.Ordinal)) return "the " + words.Substring(2);
            if (words.StartsWith("an ", StringComparison.Ordinal)) return "the " + words.Substring(3);
            return words;
        }

        /// <summary>
        /// The ground a god was seen on, as one word's key (CC_Place_Ground_*): by the water a shore, by a rock face a rise,
        /// on stony ground the stones, on sand the sands, on ice the ice, in a marsh the marsh, among trees the wood, else the
        /// field.
        /// </summary>
        public static string Ground(bool water, bool rockFace, bool stony, bool sand, bool ice, bool marsh, bool trees)
        {
            if (water) return "shore";
            if (rockFace) return "rise";
            if (ice) return "ice";
            if (marsh) return "marsh";
            if (sand) return "sands";
            if (stony) return "stones";
            if (trees) return "wood";
            return "field";
        }

        /// <summary>A flood's place by what it covered: the colony's path, else its fields; null for neither (not named).</summary>
        public static string FloodWhat(bool path, bool field) => path ? "path" : field ? "field" : null;

        /// <summary>Supper talk of a place may come: the gap since the last time is past.</summary>
        public static bool SupperDue(int lastTick, int now, int dayTicks) => lastTick < 0 || now - lastTick >= SupperGapDays * dayTicks;
    }
}
