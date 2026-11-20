using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' rarest gifts (C10 of docs/content-update-proposals.md), the pure rules (Gods/Touch/GodRareGifts.cs; Tools/Tests
    /// reads the same): after one of a god's great effects (GodTouchMath, one in forty days at most for any god), now and then,
    /// and once in two colony-years at most for the whole colony, something the player's mods make is left where it happened:
    /// a named weapon of Possessed Weapons in a ring of scorched ground, matched to the god; and after Smoulder's waking, a warm
    /// drake's egg of Dragons Descent in his deep. By name only (none without the mod).
    /// </summary>
    public static class GodRareGiftsMath
    {
        /// <summary>Each god's weapons, by its key (Tamiu's is Mog): Possessed Weapons' own, by defName.</summary>
        public static readonly Dictionary<string, string[]> Weapons = new Dictionary<string, string[]>
        {
            ["Hush"] = new[] { "BotchJob_PossessedGlacialCleaver" },
            ["Cirrus"] = new[] { "BotchJob_PossessedLightningJavelin" },
            ["Smoulder"] = new[] { "BotchJob_PossessedVolcanicMaul", "BotchJob_PossessedCraghammer" },
            ["Hollin"] = new[] { "BotchJob_PossessedVerdantBow" },
            ["Ducat"] = new[] { "BotchJob_PossessedGildedArquebus" },
            ["Mog"] = new[] { "BotchJob_PossessedShroudedDagger" },
            ["Helios"] = new[] { "BotchJob_PossessedSanctifiedSword" },
            ["Pip"] = new[] { "BotchJob_PossessedAutomatonCrossbow" }
        };

        /// <summary>Dragons Descent's warm drake's egg, found in Smoulder's deep after his waking.</summary>
        public const string DrakeEgg = "DD_EggDrakeFertilized";

        /// <summary>Once in two colony-years at most (days), for the whole colony; the chance after a great effect or a waking once that is past.</summary>
        public const float RareDays = 120f, RareChance = 0.5f, EggChance = 0.5f;

        /// <summary>A god's weapons, or none.</summary>
        public static string[] Of(string god) => god != null && Weapons.TryGetValue(god, out var w) ? w : new string[0];

        /// <summary>A rare gift may be left now: its wait past, at its chance.</summary>
        public static bool Due(float daysSinceRare, float roll, float chance) => !float.IsNaN(daysSinceRare) && daysSinceRare >= RareDays && roll < chance;

        /// <summary>The scorched ring round where it lies (cells from the middle).</summary>
        public const float RingFrom = 1.4f, RingTo = 2.4f;
    }
}
