using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the gear, looks and gifts Rimfolk gives the people of another mod's faction (Peoples/Foreign/
    /// ForeignPeoples.cs; DESIGN.md "The Stormriders: their gear, their looks and some magic"): which role a pawn kind
    /// plays, which look a pawn has, when a piece may take the place of one the pawn already wears (never one the kind's
    /// own gear requires, unless the role names it, and never one that protects more), which styles fit a man or a woman,
    /// when the unarmed get a sidearm, which gifts apply with or without A RimWorld of Magic, and how big a share of a
    /// role may have one. Linked into the tests (Tools/Tests/ForeignPeoplesTests.cs).
    /// </summary>
    public static class ForeignPeoplesMath
    {
        /// <summary>The role for the kinds no role names: the people's ordinary folk.</summary>
        public const string FolkRole = "folk";

        /// <summary>The role a trader kind no role names plays.</summary>
        public const string TraderRole = "trader";

        /// <summary>
        /// The role of a pawn kind, by index into the roles: the first role whose kinds name it; else, for a trader, the
        /// role called <see cref="TraderRole"/>; else the role called <see cref="FolkRole"/>; -1 when none fits.
        /// </summary>
        public static int RoleOf(IList<IList<string>> kinds, IList<string> names, string kind, bool trader)
        {
            if (kinds == null || names == null) return -1;
            if (kind != null)
                for (int i = 0; i < kinds.Count; i++)
                    if (kinds[i] != null && kinds[i].Contains(kind))
                        return i;
            if (trader)
                for (int i = 0; i < names.Count; i++)
                    if (names[i] == TraderRole)
                        return i;
            for (int i = 0; i < names.Count; i++)
                if (names[i] == FolkRole)
                    return i;
            return -1;
        }

        /// <summary>Which look a pawn has, by one roll in [0, 1) against the looks' weights; -1 when none has any weight.</summary>
        public static int PickLook(IList<float> weights, float roll)
        {
            if (weights == null || float.IsNaN(roll)) return -1;
            float sum = 0f;
            foreach (float w in weights) sum += Weight(w);
            if (sum <= 0f) return -1;
            float at = Math.Max(0f, Math.Min(0.99999f, roll)) * sum;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Weight(weights[i]);
                if (w <= 0f) continue;
                if (at < w) return i;
                at -= w;
            }
            for (int i = weights.Count - 1; i >= 0; i--)
                if (Weight(weights[i]) > 0f)
                    return i;
            return -1;
        }

        private static float Weight(float w) => float.IsNaN(w) || w <= 0f ? 0f : w;

        /// <summary>A chance, read safely (PeoplesGearMath.Share), hit by a roll in [0, 1).</summary>
        public static bool Rolls(float chance, float roll) => !float.IsNaN(roll) && roll >= 0f && roll < PeoplesGearMath.Share(chance);

        /// <summary>How much less a new piece may protect than the one it takes the place of (its sharp armour), for rounding.</summary>
        public const float ArmourSlack = 0.02f;

        /// <summary>
        /// Whether a new piece may take the place of one the pawn wears: the worn piece isn't one the pawn kind's own gear
        /// requires, unless the role names it as one it may replace (a civilian's cape or tunic), and it protects no more
        /// than the new piece (its sharp armour). So a fighter keeps their kit, and nobody ends up less protected.
        /// </summary>
        public static bool MayDisplace(bool required, bool replaceable, float wornArmour, float newArmour)
        {
            if (required && !replaceable) return false;
            if (float.IsNaN(wornArmour) || float.IsNaN(newArmour)) return false;
            return newArmour + ArmourSlack >= wornArmour;
        }

        /// <summary>
        /// A hair style fits a woman or a man by the game's style gender (StyleGender's names): Any for everyone, Male and
        /// MaleUsually for men, Female and FemaleUsually for women.
        /// </summary>
        public static bool StyleFits(string styleGender, bool female)
        {
            switch (styleGender)
            {
                case "Any": return true;
                case "Male":
                case "MaleUsually": return !female;
                case "Female":
                case "FemaleUsually": return female;
                default: return false;
            }
        }

        /// <summary>A sidearm for one who carries nothing and can fight, by its chance.</summary>
        public static bool Sidearm(bool armed, bool canFight, float chance, float roll) => !armed && canFight && Rolls(chance, roll);

        /// <summary>
        /// The gifts that apply: the classes with A RimWorld of Magic loaded; without it the role's other gifts (a psylink),
        /// or none.
        /// </summary>
        public static IList<T> Gifts<T>(bool magicLoaded, IList<T> withMagic, IList<T> without) => magicLoaded ? withMagic : without;

        /// <summary>
        /// The most of a role that may have a gift: a role of casters by their kind's own name (the dark elves' sorceresses)
        /// up to the peoples' whole share (PeoplesGearMath.MaxShare, never all); any other as one of Rimfolk's peoples'
        /// ordinary kinds (PeoplesMagicMath.ShareCap: sparing, more among a people built on magic).
        /// </summary>
        public static float GiftCap(bool casters, bool builtOnMagic) => casters ? PeoplesGearMath.MaxShare : PeoplesMagicMath.ShareCap(builtOnMagic, false);

        /// <summary>A role's gift shares are sound (each above none) and together fit its cap.</summary>
        public static bool GiftsFit(IList<float> shares, bool casters, bool builtOnMagic)
        {
            if (shares == null || shares.Count == 0) return true;
            float sum = 0f;
            foreach (float s in shares)
            {
                if (float.IsNaN(s) || s <= 0f || s > 1f) return false;
                sum += s;
            }
            return sum <= GiftCap(casters, builtOnMagic) + 0.0001f;
        }
    }
}
