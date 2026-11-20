using System;

namespace ColonyCouncil
{
    /// <summary>
    /// A call to arms answered from home (docs/content-update-proposals.md, B27; Diplomacy/CallsFromHome.cs): supplies in
    /// place of fighters (food, medicine and up to two spare weapons from the stores, carried to the gate for their runners),
    /// or under fealty silver in place of service (a season's tribute, from the medieval age on; a tribal colony sends
    /// supplies as a pact's partner does); or the colony's beds for their wounded after the fight. Either keeps the colony's
    /// word, as a smaller kept word than fighters: half the deed. Pure (no game types): Tools/Tests checks them.
    /// </summary>
    public static class CallsFromHomeMath
    {
        /// <summary>The supplies: this much raw food (or cooked meals when the raw food is short), medicine and spare weapons at most.</summary>
        public const int Food = 60, Meals = 20, Medicine = 6, Arms = 2;

        /// <summary>What food goes: "rawfood" when the stores hold enough, else "meals", else none (the answer is greyed out).</summary>
        public static string FoodKind(int rawFood, int meals) => rawFood >= Food ? "rawfood" : meals >= Meals ? "meals" : null;

        public static int FoodCount(string kind) => kind == "meals" ? Meals : Food;

        /// <summary>Supplies can go: food enough of one kind and the medicine; the weapons only as many as there are.</summary>
        public static bool SuppliesEnough(int rawFood, int meals, int medicine) => FoodKind(rawFood, meals) != null && medicine >= Medicine;

        public static int ArmsSent(int spare) => Math.Max(0, Math.Min(Arms, spare));

        /// <summary>Under fealty, silver in place of service: from the medieval age on (the colony's age, 0 tribal to 3 spacer).</summary>
        public static bool PaysSilver(bool fealty, int colonyAge) => fealty && colonyAge > 0;

        /// <summary>Silver in place of service: a season's tribute for a colony of this wealth (FealtyMath.Tribute), 150 to 600.</summary>
        public static int Silver(float wealth) => FealtyMath.Tribute(wealth);

        /// <summary>The deed of a call kept from home: half that of fighters sent (keptWord 10, levies 8).</summary>
        public const int KeptWeight = 5, LeviesWeight = 4;

        /// <summary>Their wounded who come to the colony's beds (TakenInMath's).</summary>
        public static int Wounded(float roll) =>
            TakenInMath.WoundedMin + (int)Math.Floor(Math.Max(0f, Math.Min(0.999f, float.IsNaN(roll) ? 0f : roll)) * (TakenInMath.WoundedMax - TakenInMath.WoundedMin + 1));
    }
}
