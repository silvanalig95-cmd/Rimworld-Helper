using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's bill mods, read softly so the planning helper (Building/Ahead.cs) changes a bill only the way they
    /// expect. Everybody Gets One (memegoddess.everybodygetsone) gives bills its own repeat modes ("one per person +X",
    /// "X per person", "with surplus": TD_PersonCount, TD_XPerPerson, TD_WithSurplusIng); any repeat mode that isn't
    /// one of the game's three belongs to some mod, and such a bill is only read, never changed. Better Workbench
    /// Management (falconne.BWM) links bills on several benches (ImprovedWorkbenches.ExtendedBillDataStorage, a world
    /// component: GetBillSetContaining(bill).Bills) and copies the bill under the mouse onto the others of its set, so
    /// a change to one bill is made to every bill of its set at once. Anything that can't be read leaves the bill on
    /// its own.
    /// </summary>
    public static class BillMods
    {
        /// <summary>A repeat mode the game itself has: do X times, do until you have X, do forever.</summary>
        public static bool GameMode(Bill_Production b) =>
            b?.repeatMode == null || b.repeatMode == BillRepeatModeDefOf.Forever || b.repeatMode == BillRepeatModeDefOf.RepeatCount || b.repeatMode == BillRepeatModeDefOf.TargetCount;

        /// <summary>The mod a bill's repeat mode belongs to, when it isn't the game's own: its name ("Everybody Gets One"), else null.</summary>
        public static string ManagedBy(Bill_Production b)
        {
            if (GameMode(b)) return null;
            string name = b.repeatMode.modContentPack?.Name;
            return name.NullOrEmpty() ? b.repeatMode.LabelCap.ToString() : name;
        }

        private static bool looked;
        private static Type storageType;
        private static MethodInfo setOf;
        private static PropertyInfo billsOf;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                storageType = GenTypes.GetTypeInAnyAssembly("ImprovedWorkbenches.ExtendedBillDataStorage");
                setOf = storageType?.GetMethod("GetBillSetContaining", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Bill_Production) }, null);
                var setType = GenTypes.GetTypeInAnyAssembly("ImprovedWorkbenches.LinkedBillsSet");
                billsOf = setType?.GetProperty("Bills", BindingFlags.Public | BindingFlags.Instance);
                if (storageType != null && (setOf == null || billsOf == null)) Log.Message("[Rimfolk] Better Workbench Management's linked bills couldn't be read; each bill is changed on its own.");
            }
            catch
            {
                setOf = null;
            }
        }

        /// <summary>The bills Better Workbench Management links with this one, itself first; just itself without the mod or a link.</summary>
        public static List<Bill_Production> Linked(Bill_Production b)
        {
            var list = new List<Bill_Production> { b };
            if (b == null) return list;
            Look();
            if (setOf == null || billsOf == null || Find.World == null) return list;
            try
            {
                var storage = Find.World.GetComponent(storageType);
                var set = storage == null ? null : setOf.Invoke(storage, new object[] { b });
                if (set == null || !(billsOf.GetValue(set) is IEnumerable bills)) return list;
                foreach (var o in bills)
                    if (o is Bill_Production other && other != b && !other.DeletedOrDereferenced) list.Add(other);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bwm linked bills", e);
            }
            return list;
        }
    }
}
