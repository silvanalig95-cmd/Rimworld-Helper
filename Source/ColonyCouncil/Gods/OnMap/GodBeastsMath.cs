using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' beasts among the colony's own (docs/content-update-proposals.md, C3, C4 and C16; Gods/OnMap/GodBeasts.cs):
    /// the legendaries of PokéWorld people take for a god's, how much is kept, and how often a believer brings a beast a bite.
    /// No game types, so Tools/Tests reads the same numbers the game uses.
    /// </summary>
    public static class GodBeastsMath
    {
        /// <summary>
        /// PokéWorld's legendaries and the god each is like enough to be taken for (C16): the sun's birds, the moon's, the dark's,
        /// the frost's, the storm's, the fire under the mountain's, the waters', the green's, the cat that pays in coins, and the
        /// keeper of the dead's restless one. A believer may still take it for another (GodLoreMath.Attribute).
        /// </summary>
        public static readonly Dictionary<string, string> Legends = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PW_HoOh"] = "Helios", ["PW_Solgaleo"] = "Helios",
            ["PW_Lunala"] = "Selene", ["PW_Cresselia"] = "Selene",
            ["PW_Darkrai"] = "Undertow", ["PW_Giratina"] = "Undertow", ["PW_Yveltal"] = "Undertow",
            ["PW_Articuno"] = "Hush", ["PW_Regice"] = "Hush",
            ["PW_Zapdos"] = "Cirrus", ["PW_Raikou"] = "Cirrus", ["PW_Rayquaza"] = "Cirrus",
            ["PW_Moltres"] = "Smoulder", ["PW_Heatran"] = "Smoulder", ["PW_Groudon"] = "Smoulder",
            ["PW_Lugia"] = "Murmur", ["PW_Kyogre"] = "Murmur", ["PW_Suicune"] = "Murmur",
            ["PW_Celebi"] = "Hollin", ["PW_Shaymin"] = "Hollin", ["PW_Xerneas"] = "Hollin",
            ["PW_Persian"] = "Ducat", ["PW_Meowth"] = "Ducat",
            ["PW_Spiritomb"] = "Mog"
        };

        /// <summary>The god a legendary is like (its key), or null for one like none of them.</summary>
        public static string LegendGod(string defName) => defName != null && Legends.TryGetValue(defName, out var g) ? g : null;

        /// <summary>
        /// One of the table's kinds wild on the land is taken for a god's once in this many days at most (Meowth and Persian,
        /// which are not legendaries, come oftener than the rest).
        /// </summary>
        public const int LegendGapDays = 30;

        public static bool LegendDue(int lastOfKind, int now, int ticksPerDay = 60000) => lastOfKind < 0 || now < lastOfKind || now - lastOfKind >= LegendGapDays * ticksPerDay;

        /// <summary>Who greeted which of the gods' beasts at home ("beast|person"), and the beasts on the land already taken for a god's, kept at most this many (the oldest let go).</summary>
        public const int GreetedMax = 160, LegendsMax = 20;

        public static string Greeting(int beast, int person) => beast + "|" + person;

        /// <summary>A believer greets a god's beast at home this close, in sight.</summary>
        public const float GreetRange = 10f;

        /// <summary>The chance, in each of the animals' rounds hours, that a believer brings a god's beast a bite by hand; at least this many days between two at a home.</summary>
        public const float BiteChance = 0.15f;

        public const int BiteGapDays = 2;

        public static bool BiteDue(int lastBite, int now, float roll, int ticksPerDay = 60000) =>
            (lastBite < 0 || now - lastBite >= BiteGapDays * ticksPerDay || now < lastBite) && roll < BiteChance;

        /// <summary>The god a beast that stays is like is glad this much when the colony spares it as its believer asked (hidden, as all favour is).</summary>
        public const float SparedFavour = 3f;

        /// <summary>A god's beast born on its god's festival day is an omen while there is room among the living omens (the twenty days' gap gives way to the day).</summary>
        public static bool FestivalOmen(int living, int most) => living < most;

        /// <summary>
        /// The roll a believer takes a beast at home by: their own, fixed for the two of them, so the same person always takes
        /// the same beast for the same god (GodLoreMath.Attribute; a mistake is theirs for good).
        /// </summary>
        public static float Roll(int beast, int person)
        {
            unchecked
            {
                uint h = (uint)(beast * 73856093) ^ (uint)(person * 19349663) ^ 0x5bd1e995u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h % 10000u) / 10000f;
            }
        }
    }
}
