using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the seven peoples' own touchpoints at the colony's gate (docs/design/peoples.md, "Their stories,
    /// sayings and touchpoints"; Peoples/PeoplesLore.cs), with no game types so Tools/Tests can check them: whom the
    /// order's hospitaller sits up with, when the herders leave a young beast at the gate, what the league's winter
    /// quarters and the pilgrims' Long Walk bring, what the enclave's salvage holds and costs by the colony's age and wealth,
    /// and how the people's card names what they honour and fear.
    /// </summary>
    public static class PeoplesLoreMath
    {
        // ================= The order's night nurse =================

        /// <summary>The hospitaller sits up with this many of the colony's fevered at most, the sickest first.</summary>
        public const int NurseMax = 4;

        /// <summary>How many the hospitaller sees to: the fevered, up to <see cref="NurseMax"/>.</summary>
        public static int NurseCount(int fevered) => Math.Max(0, Math.Min(NurseMax, fevered));

        /// <summary>
        /// The order comes for a fever, not for a cold: an illness the body has to fight off (a disease with an immunity to
        /// win, as the game's flu, plague, malaria and infections are), not yet won.
        /// </summary>
        public static bool Fever(bool immunizable, bool fullyImmune, bool everyday) => immunizable && !fullyImmune && !everyday;

        // ================= The herders' young beast =================

        /// <summary>A camp's unease below this at the end of its stay is a stay that went well (the camp's own scale: asks to join come below 40).</summary>
        public const float GoodStay = 40f;

        /// <summary>The herders leave a young beast of the herd at the gate only after a stay that went well.</summary>
        public static bool LeavesYoung(float tension, float most) => !float.IsNaN(tension) && tension < (most > 0f ? most : GoodStay);

        /// <summary>The young beast's age as a share of its kind's lifespan: weaned, not yet grown.</summary>
        public const float YoungFrom = 0.06f, YoungTo = 0.12f;

        // ================= The pilgrims' Long Walk =================

        /// <summary>A long file of pilgrims on the fellowship's walking day: six to nine of them.</summary>
        public const int WalkMin = 6, WalkMax = 9;

        // ================= The enclave's salvage =================

        /// <summary>What the salvage is worth in silver at the colony's wealth factor of 1 (0.6 to 2 scales it).</summary>
        public const float SalvageWorth = 600f;

        /// <summary>The enclave sells what isn't worth lifting back to orbit at this share of its worth.</summary>
        public const float SalvagePriceShare = 0.4f;

        /// <summary>The salvage's worth for a colony of this wealth factor (clamped to the game's 0.6 to 2).</summary>
        public static float Worth(float wealthFactor)
        {
            if (float.IsNaN(wealthFactor)) wealthFactor = 1f;
            return SalvageWorth * Math.Max(0.6f, Math.Min(2f, wealthFactor));
        }

        /// <summary>The enclave's price for salvage of this worth, to the nearest ten, never under 50.</summary>
        public static int Price(float worth) => Math.Max(50, (int)Math.Round(Math.Max(0f, worth) * SalvagePriceShare / 10f) * 10);

        /// <summary>
        /// What a salvage drop holds, by the colony's age (0 tribal to 3 spacer): the defNames and each one's share of its
        /// worth. Steel and cloth for any age; components and medicine; plasteel and chemfuel from the industrial age on;
        /// a spacer component in the spacer age. The shares come to 1.
        /// </summary>
        public static List<(string def, float share)> SalvageGoods(int age)
        {
            if (age <= 1)
                return new List<(string, float)> { ("Steel", 0.45f), ("Cloth", 0.2f), ("ComponentIndustrial", 0.2f), ("MedicineIndustrial", 0.15f) };
            if (age == 2)
                return new List<(string, float)> { ("Steel", 0.35f), ("ComponentIndustrial", 0.25f), ("Plasteel", 0.15f), ("Chemfuel", 0.15f), ("MedicineIndustrial", 0.1f) };
            return new List<(string, float)> { ("Steel", 0.25f), ("Plasteel", 0.25f), ("ComponentIndustrial", 0.2f), ("ComponentSpacer", 0.15f), ("Chemfuel", 0.15f) };
        }

        /// <summary>How many of a good of this market value a share of the salvage's worth comes to: one at least.</summary>
        public static int Count(float worth, float share, float marketValue) =>
            marketValue <= 0f || float.IsNaN(marketValue) ? 0 : Math.Max(1, (int)Math.Round(Math.Max(0f, worth) * Math.Max(0f, share) / marketValue));

        // ================= The people's card =================

        /// <summary>
        /// Which line a people's card gives its honour and fear (the faction's own words in its template): "Both", "Honour"
        /// or "Fear" (the key's ending, CC_PeoplesLore_Card_*), or null when it has neither.
        /// </summary>
        public static string CardForm(string honours, string fears)
        {
            bool h = !string.IsNullOrWhiteSpace(honours), f = !string.IsNullOrWhiteSpace(fears);
            return h && f ? "Both" : h ? "Honour" : f ? "Fear" : null;
        }

        /// <summary>One of a people's sayings for a seed, the same for the same seed (a card read twice reads the same); null with none.</summary>
        public static string Saying(IList<string> sayings, int seed)
        {
            var list = sayings?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (list == null || list.Count == 0) return null;
            return list[(int)((uint)seed % (uint)list.Count)];
        }
    }
}
