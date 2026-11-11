using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the gifts some of Rimfolk's peoples' people are born or trained to (Peoples/PeopleGifts.cs;
    /// docs/design/peoples.md "Their gear, their kinds of people and their gifts"): at most one each, by one roll against the
    /// kind's shares, which never add up to all of them; the psylink's levels; which of A RimWorld of Magic's settings
    /// switches each of its classes on; and which ages may have genes put in. Linked into the tests (Tools/Tests/PeoplesGearTests.cs).
    /// </summary>
    public static class PeoplesGearMath
    {
        /// <summary>The most of one kind that may have a gift: a share of them, never all.</summary>
        public const float MaxShare = 0.6f;

        /// <summary>
        /// Which gift a pawn of the kind has, by one roll in [0, 1): the shares laid end to end from the first, the roll
        /// landing in one; -1 past them all (most of the kind has none). A bad share counts as none.
        /// </summary>
        public static int Pick(IList<float> shares, float roll)
        {
            if (shares == null || float.IsNaN(roll) || roll < 0f) return -1;
            float at = 0f;
            for (int i = 0; i < shares.Count; i++)
            {
                float s = Share(shares[i]);
                if (s <= 0f) continue;
                at += s;
                if (roll < at) return i;
            }
            return -1;
        }

        /// <summary>A share read safely: 0 to 1, NaN as none.</summary>
        public static float Share(float s) => float.IsNaN(s) || s <= 0f ? 0f : Math.Min(1f, s);

        /// <summary>The shares of one kind are sound: each above none, and all of them together no more than <see cref="MaxShare"/>.</summary>
        public static bool Sound(IList<float> shares)
        {
            if (shares == null || shares.Count == 0) return false;
            float sum = 0f;
            foreach (float s in shares)
            {
                if (float.IsNaN(s) || s <= 0f || s > 1f) return false;
                sum += s;
            }
            return sum <= MaxShare + 0.0001f;
        }

        /// <summary>How many kinds of gift one gift names (a class, a psylink, genes): a sound gift names exactly one.</summary>
        public static int GiftKinds(bool trait, bool psylink, bool genes) => (trait ? 1 : 0) + (psylink ? 1 : 0) + (genes ? 1 : 0);

        /// <summary>
        /// Whether a people of this age (its FactionDef's techLevel) has the means to put genes in: industrial and later.
        /// Tribal and medieval peoples' gifts are A RimWorld of Magic's; their kinds of people are their xenotypes.
        /// </summary>
        public static bool GenesFit(string techLevel) =>
            techLevel == "Industrial" || techLevel == "Spacer" || techLevel == "Ultra" || techLevel == "Archotech";

        /// <summary>The same by the age a people lives in now (0 tribal to 3 spacer: Peoples.AgeOf), which can change over the years.</summary>
        public static bool GenesAge(int age) => age >= 2;

        /// <summary>The psylink's level for a roll in [0, 1): <paramref name="min"/> to <paramref name="max"/>, at least 1 and at most the game's 6.</summary>
        public static int Levels(int min, int max, float roll)
        {
            min = Math.Max(1, Math.Min(6, min));
            max = Math.Max(min, Math.Min(6, max));
            if (float.IsNaN(roll) || roll < 0f) roll = 0f;
            int n = min + (int)(roll * (max - min + 1));
            return Math.Min(max, n);
        }

        /// <summary>How many of a path's powers a psycaster of these levels knows: one, and one more for every second level past the first, at most four.</summary>
        public static int Powers(int levels) => Math.Max(1, Math.Min(4, 1 + (levels - 1) / 2));

        /// <summary>
        /// A RimWorld of Magic's class traits Rimfolk's peoples may give (by the trait's defName) and the switch in its
        /// settings that turns each on (TorannMagic.ModOptions.Settings, a bool field of that name): only the classes its
        /// own components know as a mage's or a fighter's (CompAbilityUserMagic.magicTraitIndexes,
        /// CompAbilityUserMight.mightTraitIndexes, 1.6).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> MagicClasses = new Dictionary<string, string>
        {
            ["Arcanist"] = "Arcanist", ["InnerFire"] = "FireMage", ["HeartOfFrost"] = "IceMage", ["StormBorn"] = "LitMage", ["Druid"] = "Druid",
            ["Paladin"] = "Paladin", ["Priest"] = "Priest", ["TM_Bard"] = "Bard", ["Geomancer"] = "Geomancer", ["Technomancer"] = "Technomancer",
            ["Enchanter"] = "Enchanter", ["Chronomancer"] = "Chronomancer", ["Summoner"] = "Summoner", ["TM_Wanderer"] = "Wanderer",
            ["Gladiator"] = "Gladiator", ["Bladedancer"] = "Bladedancer", ["Ranger"] = "Ranger", ["TM_Sniper"] = "Sniper", ["TM_Psionic"] = "Psionic",
            ["TM_Monk"] = "Monk", ["TM_Commander"] = "Commander", ["TM_SuperSoldier"] = "SuperSoldier", ["TM_Wayfarer"] = "Wayfarer",
            // The gods' gifts (Gods/World/GodGifts.cs): the dead's and the Hunger's.
            ["Necromancer"] = "Necromancer", ["DeathKnight"] = "DeathKnight"
        };

        /// <summary>The fighters among <see cref="MagicClasses"/> (A RimWorld of Magic's might classes): the rest are mages.</summary>
        public static readonly HashSet<string> MightClasses = new HashSet<string>
        {
            "Gladiator", "Bladedancer", "Ranger", "TM_Sniper", "TM_Psionic", "TM_Monk", "TM_Commander", "TM_SuperSoldier", "TM_Wayfarer", "DeathKnight"
        };

        /// <summary>The setting that turns a class on, or null for a trait Rimfolk's peoples don't give.</summary>
        public static string MagicSetting(string trait) => trait != null && MagicClasses.TryGetValue(trait, out var s) ? s : null;

        /// <summary>Whether a class of A RimWorld of Magic fights (might) rather than casts (magic).</summary>
        public static bool Might(string trait) => trait != null && MightClasses.Contains(trait);

        /// <summary>A RimWorld of Magic's own factor for a people (its settings' mage or fighter share by faction): none at 0, unset as 1.</summary>
        public static bool FactionAllows(float? factor) => !factor.HasValue || float.IsNaN(factor.Value) || factor.Value > 0f;
    }
}
