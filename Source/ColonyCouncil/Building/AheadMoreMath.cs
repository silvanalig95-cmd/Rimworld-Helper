using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>One of Colony Manager's jobs as plain numbers (Integrations/ColonyManagerBridge.cs reads it from the mod).</summary>
    public class ManagerJobInfo
    {
        /// <summary>Stable from hour to hour: its kind and its place among the jobs of that kind ("hunting:0").</summary>
        public string key;
        /// <summary>AheadMoreMath.Hunting, Forestry, Mining, Livestock or Foraging.</summary>
        public string kind;
        /// <summary>The job's own name in the mod, and what it counts, in words.</summary>
        public string label, goods;
        /// <summary>It keeps a stock target (hunting, logging, mining, foraging); livestock keeps herd sizes instead.</summary>
        public bool threshold;
        /// <summary>Its target, what it counts now, and the most its slider allows (0: no limit known).</summary>
        public int target, current, max;
        public bool suspended, managed = true, completed;
        /// <summary>Its target may be raised from here (hunting, logging, mining).</summary>
        public bool raises;
        /// <summary>What the colony wants of its goods in all, in its own pieces (0: nothing beyond the usual), and why in words.</summary>
        public int wanted;
        public string wantedWhy;
    }

    /// <summary>Whether Colony Manager can do its work: its jobs, its managers and its desks, as counts.</summary>
    public class ManagerState
    {
        /// <summary>Jobs it keeps, and those still working (managed, not suspended, not at their target).</summary>
        public int jobs, active;
        /// <summary>The mod's Managing work type exists; colonists with it switched on; colonists who could have it.</summary>
        public bool workType;
        public int managersOn, managersAble;
        /// <summary>The work is done at a manager desk; desks there are, and those that can be worked (powered, allowed).</summary>
        public bool deskNeeded;
        public int desks, desksWorking;
    }

    /// <summary>Something on the shopping list: what the colony wants of a thing it can't make here, what it has, what is on its way.</summary>
    public class ShopNeed
    {
        public string key, label;
        /// <summary>What the colony would use of it in all (what it has counted in), from every reason together.</summary>
        public int want;
        public int have;
        /// <summary>On its way without buying: rock already marked to mine, at its yield.</summary>
        public int pending;
        /// <summary>Its base price a piece.</summary>
        public float price;
        /// <summary>The reasons, each in a few words, and the pieces each wants.</summary>
        public List<(string why, int want)> reasons = new List<(string, int)>();
        /// <summary>It is rock the colony could mine: a deposit of it is known on the map.</summary>
        public bool deposit;
        /// <summary>Colony Manager's mining job mines it now.</summary>
        public bool managerMines;
        /// <summary>Of it, what a trader here (on the map or in orbit) has; -1 none here or not readable.</summary>
        public int traderHas = -1;

        public int Short => AheadMoreMath.ShopShort(want, have, pending);
    }

    /// <summary>One kind of garment the colony's people wear, as plain numbers.</summary>
    public class ClothesKind
    {
        public string key, label;
        /// <summary>Worn now; of those, below half their hit points (worn out, a mood penalty); and those that wear below half within the days ahead at the garment's own wear a day.</summary>
        public int worn, frayed, soon;
        /// <summary>Whole ones in store (half their hit points or more).</summary>
        public int spare;
        /// <summary>Pieces the colony's bills will still make of it; a bill makes it for ever.</summary>
        public int billCover;
        public bool forever;
        /// <summary>Pieces worn through a day by the record (0 until it covers a day).</summary>
        public float wornOutADay;
        /// <summary>A bench here can make it; a batch's parts are in stock; a bill for it is set by another mod (Everybody Gets One).</summary>
        public bool canMake, partsInStock, managed;
    }

    /// <summary>
    /// Planning ahead, phase 3 (Building/AheadMore.cs) as plain rules: Colony Manager's targets against what the colony
    /// wants of their goods and whether the manager can work at all; the shopping list (what the colony wants and can't
    /// make here, less what it has and what is already marked to mine) and how much rock to mark; spare clothes by what
    /// is worn below half, what wears below half in the days ahead and what the record saw worn through, against the
    /// whole ones in store and the bills. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class AheadMoreMath
    {
        public const string Hunting = "hunting", Forestry = "forestry", Mining = "mining", Livestock = "livestock", Foraging = "foraging";
        /// <summary>The kinds in the order the rows are shown.</summary>
        public static readonly string[] ManagerKinds = { Hunting, Forestry, Mining, Foraging, Livestock };

        /// <summary>Why the manager can't work: no desk, a desk that can't be worked, nobody managing, nobody who could.</summary>
        public const string NoDesk = "nodesk", DeskOff = "deskoff", NoManager = "nomanager", NobodyCan = "nobodycan";

        /// <summary>A garment below this share of its hit points is worn out (the game's "frayed", a mood penalty); a spare is one at this or above.</summary>
        public const float FrayedShare = 0.5f;
        /// <summary>A step marks at most this much rock at once (what miners do in a day or two).</summary>
        public const int MaxMineCells = 40;
        /// <summary>At most this many kinds of garment are suggested at once.</summary>
        public const int MaxClothesSteps = 3;
        /// <summary>A shopping list's rows shown, most worth first.</summary>
        public const int MaxShopRows = 12;

        // ---------------- Colony Manager ----------------

        /// <summary>A target the manager's slider can show: up to 200 by tens, above that by fifties.</summary>
        public static int RoundTarget(float want)
        {
            if (want <= 0f) return 0;
            int step = want <= 200f ? 10 : 50;
            return (int)Math.Ceiling(want / step - 0.0001f) * step;
        }

        /// <summary>The target to raise a job to for what the colony wants: rounded, never past the slider's top; the old target when that is no higher.</summary>
        public static int RaiseTo(int target, int wanted, int max)
        {
            if (wanted <= target) return target;
            int to = RoundTarget(wanted);
            if (max > 0 && to > max) to = max;
            return Math.Max(target, to);
        }

        /// <summary>A job whose target is below what the colony wants of its goods, and that is running: raise it.</summary>
        public static bool ShouldRaise(ManagerJobInfo j) =>
            j != null && j.raises && j.threshold && j.managed && !j.suspended && j.wanted > j.target && RaiseTo(j.target, j.wanted, j.max) > j.target;

        /// <summary>A suspended job whose goods the colony wants more of than it has: resume it.</summary>
        public static bool ShouldResume(ManagerJobInfo j) => j != null && j.threshold && j.managed && j.suspended && j.wanted > j.current;

        /// <summary>Why the manager does nothing while it has work: the desk first (nobody can manage without one), then the managers. Null when it can work.</summary>
        public static string Stall(ManagerState s)
        {
            if (s == null || s.active <= 0) return null;
            if (s.deskNeeded && s.desks == 0) return NoDesk;
            if (s.deskNeeded && s.desksWorking == 0) return DeskOff;
            if (s.workType && s.managersOn == 0) return s.managersAble > 0 ? NoManager : NobodyCan;
            return null;
        }

        /// <summary>A raised target worked when the job's count rose a quarter of the way to it (a piece at least), or reached most of it.</summary>
        public static bool RaiseWorked(int before, int now, int target) => AheadMath.StockStepWorked(before, now, target, 0f, 0f);

        // ---------------- The shopping list ----------------

        /// <summary>What is left to buy: what the colony wants less what it has and what is marked to mine.</summary>
        public static int ShopShort(int want, int have, int pending) => Math.Max(0, want - Math.Max(0, have) - Math.Max(0, pending));

        /// <summary>
        /// One reason's use: a chain or a blueprint says how much more it needs beyond the stock, so what it would use in
        /// all is that and the stock. Each reason was reckoned against the same stock, so the list adds up the uses and
        /// takes the stock off once.
        /// </summary>
        public static int Gross(int moreThanStock, int have) => Math.Max(0, moreThanStock) + Math.Max(0, have);

        /// <summary>The same thing wanted for one more reason: its use added to the others'.</summary>
        public static void AddReason(ShopNeed n, string why, int use)
        {
            if (n == null || use <= 0) return;
            n.reasons.Add((why, use));
            n.want = n.reasons.Sum(r => Math.Max(0, r.want));
        }

        /// <summary>The list in order: what is short, the most silver's worth first, then by name; at most so many rows.</summary>
        public static List<ShopNeed> Order(IEnumerable<ShopNeed> all, int max = MaxShopRows) =>
            all.Where(n => n != null && n.Short > 0).OrderByDescending(n => n.Short * Math.Max(0.01f, n.price)).ThenBy(n => n.key, StringComparer.Ordinal).Take(max).ToList();

        /// <summary>Rock to mark for so much short at so much a cell: at least one cell, at most a step's.</summary>
        public static int CellsToMark(int shortBy, float perCell, int max = MaxMineCells)
        {
            if (shortBy <= 0 || perCell <= 0f) return 0;
            return Math.Max(1, Math.Min(max, (int)Math.Ceiling(shortBy / perCell - 0.0001f)));
        }

        /// <summary>A mining step worked once half its cells are mined out.</summary>
        public static bool MineWorked(int marked, int mined) => marked > 0 && mined * 2 >= marked;

        // ---------------- Spare clothes ----------------

        /// <summary>Days until a garment wears below half at its wear a day (0 when it is below already; AheadMath.Far when it doesn't wear).</summary>
        public static float DaysToFray(float hitPoints, float maxHitPoints, float wearPerDay)
        {
            if (maxHitPoints <= 0f) return AheadMath.Far;
            float left = hitPoints - maxHitPoints * FrayedShare;
            if (left < 0f) return 0f;
            return wearPerDay <= 0f ? AheadMath.Far : left / wearPerDay;
        }

        /// <summary>Replacements wanted over the days ahead: those worn below half now, those that will be, and what the record saw worn through in as many days.</summary>
        public static int ClothesWanted(ClothesKind c, float days) =>
            c == null ? 0 : Math.Max(0, c.frayed) + Math.Max(0, c.soon) + (int)Math.Ceiling(Math.Max(0f, c.wornOutADay) * Math.Max(0f, days) - 0.0001f);

        /// <summary>Replacements still missing: wanted less the spares in store and what the bills will make (none with a bill for ever).</summary>
        public static int ClothesShort(ClothesKind c, float days) => c == null || c.forever ? 0 : Math.Max(0, ClothesWanted(c, days) - Math.Max(0, c.spare) - Math.Max(0, c.billCover));

        /// <summary>The "do until" count of whole spares a bill keeps for it.</summary>
        public static int ClothesTarget(ClothesKind c, float days) => Math.Max(1, ClothesWanted(c, days));

        /// <summary>The garments to suggest bills for: short of replacements, not set by another mod, the most short first, at most a few.</summary>
        public static List<ClothesKind> ClothesSteps(IEnumerable<ClothesKind> all, float days, int max = MaxClothesSteps) =>
            all.Where(c => c != null && !c.managed && ClothesShort(c, days) > 0).OrderByDescending(c => ClothesShort(c, days)).ThenByDescending(c => c.frayed)
                .ThenBy(c => c.key, StringComparer.Ordinal).Take(max).ToList();
    }
}
