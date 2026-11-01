using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>A good the colony keeps a stock of, as plain numbers (Building/ColonyNeeds.cs fills it from the map).</summary>
    public class AheadGood
    {
        /// <summary>The thing's defName, and its label.</summary>
        public string key, label;
        /// <summary>One of AheadMath's kinds: meal, raw, feed, medicine, clothes, material.</summary>
        public string kind;
        /// <summary>Items in store.</summary>
        public float count;
        /// <summary>Nutrition in one item (0 for what isn't eaten).</summary>
        public float nutrition;
        public float stackLimit = 75f;
        /// <summary>Storage cells it takes now.</summary>
        public int cells;
        /// <summary>Days before it starts to rot at room temperature (AheadMath.Far: never).</summary>
        public float rotDays = AheadMath.Far;
        /// <summary>The share of it kept below freezing.</summary>
        public float frozenShare;
        /// <summary>Items used a day by the colony's record (when it covers a day).</summary>
        public float usedADay;
        /// <summary>How much the blueprints and frames are short of it (materials).</summary>
        public int shortForBuilding;

        public float Nutrition => count * nutrition;
    }

    /// <summary>One of the player's bills, as plain numbers.</summary>
    public class AheadBill
    {
        /// <summary>Stable: the bench's id and the recipe (AheadMath.RaiseKey and the like build on it).</summary>
        public string key;
        public string product, label, bench, kind;
        /// <summary>Per product: its nutrition for food, 1 for anything counted by the piece.</summary>
        public float unit = 1f;
        /// <summary>AheadMath.Until, Forever, Times, or Other (a mode some mod added).</summary>
        public string mode = AheadMath.Until;
        /// <summary>"Do until X": X; "do X times": the times left.</summary>
        public int target;
        public bool suspended;
        /// <summary>Set by another mod (Everybody Gets One's "per person" targets): read, never changed.</summary>
        public bool managed;
        /// <summary>Bills Better Workbench Management links with it, itself counted (1 alone).</summary>
        public int linked = 1;
        /// <summary>Products its bench can turn out a day at its hand's hours (AheadMath.Far: not known).</summary>
        public float canMakeADay = AheadMath.Far;
        /// <summary>Another bench here that could take a copy of it (a name), or null.</summary>
        public string freeBench;
    }

    /// <summary>A bill the colony could add for a kind of good it has no bill for: the recipe, the bench, the product.</summary>
    public class AheadAdd
    {
        public string key, kind, product, label, bench;
        public float unit = 1f;
    }

    /// <summary>A growing zone, as plain numbers.</summary>
    public class AheadField
    {
        /// <summary>Stable: the zone's id.</summary>
        public string key;
        public string label, crop, cropLabel, product, productLabel;
        /// <summary>Its harvest feeds people or animals.</summary>
        public bool food;
        public int cells, sown;
        /// <summary>What a cell gives in a day of full growth at its soil: nutrition for food, items else.</summary>
        public float perCellADay;
        /// <summary>For each day ahead, this crop's share of a full growing day (null: the input's typical one).</summary>
        public float[] growFactors;
        /// <summary>Fertile free cells next to it the zone could grow onto (counted up to AheadMath.MaxCellsAStep).</summary>
        public int freeNext;
        /// <summary>The product in store: items, and nutrition (food).</summary>
        public float stock, stockNutrition;
        /// <summary>The product used a day by the record, and whether the record covers a day.</summary>
        public float useADay;
        public bool measured;
        /// <summary>One harvest of the whole zone (items).</summary>
        public float harvest;
        /// <summary>Storage cells the product takes now.</summary>
        public int stockCells;
    }

    /// <summary>A food crop the colony can sow, for a new zone or a switch.</summary>
    public class AheadCrop
    {
        public string key, label;
        /// <summary>Nutrition a cell gives in a day of full growth on the colony's usual soil.</summary>
        public float perCellADay;
        public float[] growFactors;
    }

    /// <summary>Everything the planner reads: the colony now, as plain numbers.</summary>
    public class AheadInput
    {
        public int colonists, slaves, prisoners, guests, feedAnimals;
        /// <summary>Nutrition a day the people who eat meals here need, each from their own hunger.</summary>
        public float humanNeed;
        /// <summary>Nutrition a day the animals fed from the stores need.</summary>
        public float feedNeed;
        /// <summary>Medicine a day, estimated (those who need tending, and a little for the small hurts).</summary>
        public float medicineEstimate;
        /// <summary>The record of use covers a day or more.</summary>
        public bool measured;
        public List<AheadGood> goods = new List<AheadGood>();
        public List<AheadBill> bills = new List<AheadBill>();
        public List<AheadAdd> canAdd = new List<AheadAdd>();
        public List<AheadField> fields = new List<AheadField>();
        /// <summary>Food crops the colony can sow, any order.</summary>
        public List<AheadCrop> crops = new List<AheadCrop>();
        /// <summary>For each day ahead (a year's cycle), a typical crop's share of a full growing day.</summary>
        public float[] growFactors = new float[0];
        public float growingLeftThisYear;
        /// <summary>Of the raw food used, the share that came from plants (the rest meat and the like), by the record.</summary>
        public float plantShare = 1f;
        public float mealDays = 2f, reserveDays = 5f;
        /// <summary>A working nutrient paste dispenser feeds people without stored meals: the meals then go by the record alone.</summary>
        public bool dispenser;
        /// <summary>A freezer takes meals; and the rot days of the meal the colony makes most.</summary>
        public bool freezer;
        public float mealRotDays = AheadMath.Far;
        /// <summary>Nutrition the stores that take meals could still hold.</summary>
        public float mealRoom = AheadMath.Far;
        /// <summary>Free fertile cells found near the fields for a new zone.</summary>
        public int newGround = AheadMath.MaxCellsAStep;
        /// <summary>Suggestions the player set aside: the planner gives their share to the next way.</summary>
        public HashSet<string> declined = new HashSet<string>();
    }

    /// <summary>One stock row: what there is, what goes a day, how long it lasts, what to keep, and how it stands.</summary>
    public class AheadRow
    {
        public string kind, key, label;
        /// <summary>In nutrition for food rows, in pieces else.</summary>
        public float have, useADay, target;
        public float count;
        public float daysLeft = AheadMath.Far;
        /// <summary>The days the target covers, and the days its stock keeps (AheadMath.Far: no limit).</summary>
        public float targetDays, keepDays = AheadMath.Far;
        public int status = AheadMath.NoUse, cells;
        public bool food, useMeasured;
        /// <summary>Bills for it another mod sets (left alone).</summary>
        public int managedBills;
        /// <summary>What the bills ask for together, in the row's measure (AheadMath.Far: a bill runs for ever).</summary>
        public float billsCover;
        public List<AheadGood> parts = new List<AheadGood>();
    }

    /// <summary>One suggestion the planner makes, as numbers; the game side turns it into a step with its words.</summary>
    public class AheadIdea
    {
        public string key, kind, area;
        /// <summary>Days until what it is about runs short (AheadMath.Far: no rush).</summary>
        public float days = AheadMath.Far;
        public AheadRow row;
        public AheadBill bill;
        public AheadAdd add;
        public AheadField field;
        public AheadCrop crop;
        public int from, to, cells;
        /// <summary>Food a day the step adds over the year's cycle (fields), or the bills' cover it adds.</summary>
        public float gives;
    }

    /// <summary>The planner's answer: the stock rows, the field plan's numbers and the suggestions, most urgent first.</summary>
    public class AheadPlan
    {
        public List<AheadRow> rows = new List<AheadRow>();
        public List<AheadIdea> ideas = new List<AheadIdea>();
        /// <summary>Crop nutrition the colony needs a day; what its food fields give a day over the year's cycle.</summary>
        public float cropNeed, cropGive;
        /// <summary>Growing days in the cycle ahead, days in it with no growing, and growing days left this year.</summary>
        public float growingDays, growingLeftThisYear;
        public int withoutDays, cycleDays;
        /// <summary>Cells of the best crop the fields are short of (0: enough).</summary>
        public int cellsShort;
        /// <summary>Days until the crop food runs out with the fields as they are (AheadMath.Far: not within the cycle).</summary>
        public float runsShortIn = AheadMath.Far;

        public AheadRow Row(string kind) => rows.FirstOrDefault(r => r.kind == kind);
    }

    /// <summary>
    /// Planning ahead (Building/ColonyNeeds.cs, Building/Ahead.cs) as plain rules. Food is measured in nutrition and
    /// storage in cells, never in item counts: a pile of a crop with little nutrition in each piece is not a big
    /// stock. Stock targets are a day's use times the days chosen, cut to what the stock keeps before it rots and to
    /// the room there is; crops give their harvest's nutrition over the days they take to grow at their soil (resting
    /// at night); the year's growing days come from the land's temperatures. The planner turns a colony's numbers into
    /// stock rows and suggestions (bills raised or lowered, a missing bill, a second bench, fields grown, a crop
    /// switched, a new field), each with the numbers behind it. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class AheadMath
    {
        public const float Far = 999f;
        public const int TicksPerDay = 60000, DaysAYear = 60, DaysATwelfth = 5;

        public const string Meal = "meal", Raw = "raw", Feed = "feed", Medicine = "medicine", Clothes = "clothes", Material = "material";
        /// <summary>The kinds in the order the rows are shown.</summary>
        public static readonly string[] Kinds = { Meal, Raw, Feed, Medicine, Clothes, Material };

        public const string Until = "until", Forever = "forever", Times = "times", Other = "other";
        public const int Short = 0, Low = 1, Enough = 2, More = 3, NoUse = 4;

        /// <summary>Under a day's use is short, whatever the target.</summary>
        public const float ShortDays = 1f;
        /// <summary>Under this share of the target is short, under the second low; over the factor times it, more than needed.</summary>
        public const float ShortShare = 0.25f, LowShare = 0.75f, MoreFactor = 2f;
        /// <summary>A simple or fine meal gives 0.9 nutrition from 0.5 of ingredients.</summary>
        public const float RawPerMeal = 0.5f / 0.9f;
        /// <summary>What rots is kept to half its days before rot: the oldest aren't eaten first.</summary>
        public const float KeepShare = 0.5f;
        /// <summary>Plants grow from 6 in the morning to about 7 in the evening (Plant.Resting: the day's first quarter and last fifth).</summary>
        public const float GrowingShareOfDay = 0.55f;
        /// <summary>Between a harvest and the next crop taking hold: the harvest's own work and the sowing.</summary>
        public const float ReplantDays = 0.5f;
        /// <summary>A field step adds at most this many cells (what growers sow in a day or two), at least this few.</summary>
        public const int MaxCellsAStep = 80, MinCellsAStep = 4;
        /// <summary>Fields within this share of the need are enough.</summary>
        public const float FieldTolerance = 0.05f;
        /// <summary>A crop whose stock alone covers this many days (a year) is far more than the colony uses.</summary>
        public const float OverDays = 60f;
        /// <summary>A day with less than this share of growth is a day without growing.</summary>
        public const float NoGrowFactor = 0.1f;
        /// <summary>Bills that ask for this share of the target are enough; a meal bill above this many times what keeps is lowered.</summary>
        public const float BillCovered = 0.9f, SpoilLower = 1.5f;
        /// <summary>A crop is switched to another only when that gives this much more a cell.</summary>
        public const float SwitchGain = 1.2f;

        // ---------------- Eaters ----------------

        /// <summary>A day's nutrition from a hunger's fall a tick.</summary>
        public static float NutritionADay(float fallPerTick) => Math.Max(0f, fallPerTick) * TicksPerDay;

        /// <summary>Raw food the cooks need for meals of so much nutrition.</summary>
        public static float RawFor(float mealNutrition) => Math.Max(0f, mealNutrition) * RawPerMeal;

        // ---------------- Stocks ----------------

        public static float DaysLeft(float have, float useADay) => useADay <= 0.0001f ? Far : Math.Max(0f, have) / useADay;

        /// <summary>The days a stock keeps: no limit frozen, in a freezer or when it never rots; else half its days before rot.</summary>
        public static float KeepDays(float rotDays, float frozenShare, bool freezer) =>
            rotDays >= Far || freezer || frozenShare >= 0.5f ? Far : Math.Max(0.5f, rotDays * KeepShare);

        /// <summary>A stock target: a day's use times the days chosen, cut to the days it keeps and to the room there is.</summary>
        public static float Target(float useADay, float days, float keepDays, float room = Far) =>
            Math.Max(0f, Math.Min(Math.Max(0f, useADay) * Math.Min(days, keepDays), room));

        /// <summary>How a stock stands against its target and its use.</summary>
        public static int Status(float have, float useADay, float target)
        {
            if (target <= 0.0001f) return have > 0f ? Enough : NoUse;
            if (DaysLeft(have, useADay) < ShortDays || have < target * ShortShare) return Short;
            if (have < target * LowShare) return Low;
            if (have > target * MoreFactor) return More;
            return Enough;
        }

        public static int StorageCells(float count, float stackLimit) => count <= 0f ? 0 : (int)Math.Ceiling(count / Math.Max(1f, stackLimit));

        /// <summary>Nutrition a full storage cell holds.</summary>
        public static float NutritionPerCell(float nutrition, float stackLimit) => Math.Max(0f, nutrition) * Math.Max(1f, stackLimit);

        /// <summary>A bill's count rounded up to fives, at least one.</summary>
        public static int RoundUp5(float n) => n <= 1f ? 1 : (int)Math.Ceiling(n / 5f) * 5;

        // ---------------- Crops ----------------

        /// <summary>The soil's effect on growth, as the game reckons it (PlantUtility.GrowthRateFactorFor_Fertility).</summary>
        public static float FertilityFactor(float fertility, float sensitivity) => fertility * sensitivity + (1f - sensitivity);

        /// <summary>Temperature's effect on growth, as the game reckons it (PlantUtility.GrowthRateFactorFor_Temperature).</summary>
        public static float TemperatureFactor(float temp, float min, float minOptimal, float maxOptimal, float max)
        {
            if (temp < minOptimal) return minOptimal <= min ? (temp >= min ? 1f : 0f) : Clamp01((temp - min) / (minOptimal - min));
            if (temp > maxOptimal) return max <= maxOptimal ? (temp <= max ? 1f : 0f) : Clamp01((max - temp) / (max - maxOptimal));
            return 1f;
        }

        /// <summary>Days from sowing to sowing again: the growing days at the soil's rate, resting at night, and the replanting.</summary>
        public static float CycleDays(float growDays, float growthFactor) => growDays / (GrowingShareOfDay * Math.Max(0.05f, growthFactor)) + ReplantDays;

        /// <summary>What a cell gives in a day of full growth: its harvest's worth over its cycle at the soil's rate.</summary>
        public static float PerCellADay(float harvestYield, float worthEach, float growDays, float fertilityFactor, float yieldFactor = 1f) =>
            growDays <= 0f ? 0f : Math.Max(0f, harvestYield) * Math.Max(0f, yieldFactor) * Math.Max(0f, worthEach) / CycleDays(growDays, fertilityFactor);

        /// <summary>
        /// The temperature of each day ahead from the land's twelve twelfth averages (each the middle of its five days,
        /// in between drawn straight), starting at the day of the year given.
        /// </summary>
        public static float[] DayTemperatures(float[] twelfths, int dayOfYear, int days)
        {
            var temps = new float[Math.Max(0, days)];
            if (twelfths == null || twelfths.Length != 12) return temps;
            for (int i = 0; i < temps.Length; i++)
            {
                float d = (dayOfYear + i) % DaysAYear + 0.5f;
                float pos = (d - DaysATwelfth / 2f) / DaysATwelfth;
                int t0 = (int)Math.Floor(pos);
                float frac = pos - t0;
                float a = twelfths[((t0 % 12) + 12) % 12], b = twelfths[(((t0 + 1) % 12) + 12) % 12];
                temps[i] = a + (b - a) * frac;
            }
            return temps;
        }

        /// <summary>A crop's share of a full growing day for each day, from its temperatures.</summary>
        public static float[] GrowFactors(float[] temps, float min, float minOptimal, float maxOptimal, float max)
        {
            var f = new float[temps?.Length ?? 0];
            for (int i = 0; i < f.Length; i++) f[i] = TemperatureFactor(temps[i], min, minOptimal, maxOptimal, max);
            return f;
        }

        public static float GrowingDays(float[] factors) => factors == null ? 0f : factors.Sum();

        public static int DaysWithout(float[] factors) => factors == null ? 0 : factors.Count(f => f < NoGrowFactor);

        /// <summary>Cells to cover a shortfall at so much a cell (both over the same days); 0 for none.</summary>
        public static int CellsFor(float shortfall, float perCell) => shortfall <= 0f || perCell <= 0f ? 0 : (int)Math.Ceiling(shortfall / perCell);

        /// <summary>Days until a stock runs out, its use taken each day and the fields' make of that day added; Far when it lasts.</summary>
        public static float RunsShortIn(float stock, float useADay, float[] makeByDay)
        {
            if (useADay <= 0f) return Far;
            float s = Math.Max(0f, stock);
            int days = makeByDay?.Length ?? 0;
            for (int d = 0; d < days; d++)
            {
                float net = useADay - makeByDay[d];
                if (net > 0f && s <= net) return d + s / net;
                s -= net;
            }
            return days == 0 ? DaysLeft(stock, useADay) : Far;
        }

        // ---------------- Whether a step worked ----------------

        /// <summary>
        /// A stock step (a bill raised, added or copied) worked when the stock rose a quarter of the way to its target
        /// (a piece at least), reached most of the target, or the bench made half a day's use while it was eaten as fast.
        /// </summary>
        public static bool StockStepWorked(float before, float now, float target, float madeSince, float useADay)
        {
            if (target > 0f && now >= target * 0.8f) return true;
            float rise = Math.Max(1f, (target - before) * 0.25f);
            if (now - before >= rise) return true;
            return madeSince > 0f && madeSince >= useADay * 0.5f;
        }

        /// <summary>A field step worked once half the cells it added are sown.</summary>
        public static bool SownWorked(int cells, int sown) => cells > 0 && sown >= Math.Ceiling(cells * 0.5);

        // ---------------- The keys of the suggestions ----------------

        public static string RaiseKey(AheadBill b) => "ahead:raise:" + b.key;
        public static string LowerKey(AheadBill b) => "ahead:lower:" + b.key;
        public static string SecondKey(AheadBill b) => "ahead:second:" + b.key;
        public static string AddKey(AheadAdd a) => "ahead:add:" + a.key;
        public static string GrowKey(AheadField f) => "ahead:grow:" + f.key;
        public static string SwitchKey(AheadField f, AheadCrop c) => "ahead:switch:" + f.key + ":" + c.key;
        public static string NewKey(AheadCrop c) => "ahead:new:" + c.key;

        // ---------------- The plan ----------------

        /// <summary>The stock rows, the field plan and the suggestions for a colony's numbers.</summary>
        public static AheadPlan Plan(AheadInput x)
        {
            var p = new AheadPlan();
            if (x == null) return p;
            var declined = x.declined ?? new HashSet<string>();
            var typical = x.growFactors ?? new float[0];
            p.cycleDays = typical.Length > 0 ? typical.Length : DaysAYear;
            p.growingDays = GrowingDays(typical);
            p.withoutDays = DaysWithout(typical);
            p.growingLeftThisYear = x.growingLeftThisYear;
            float foodDays = Math.Max(0f, x.reserveDays) + p.withoutDays;

            // The rows.
            var meal = FoodRow(x, Meal, x.dispenser ? 0f : x.humanNeed, x.mealDays, x.freezer ? Far : x.mealRotDays, x.mealRoom);
            var raw = FoodRow(x, Raw, RawFor(x.humanNeed), foodDays, Far, Far);
            var feed = FoodRow(x, Feed, x.feedNeed, foodDays, Far, Far);
            p.rows.Add(meal);
            p.rows.Add(raw);
            if (x.feedAnimals > 0 || feed.have > 0f) p.rows.Add(feed);
            var med = CountRow(x, Medicine, x.measured ? (float?)null : x.medicineEstimate);
            if (med.useADay > 0f || med.have > 0f) p.rows.Add(med);
            var clothes = CountRow(x, Clothes, null);
            if (clothes.useADay > 0f) p.rows.Add(clothes);
            foreach (var g in x.goods.Where(g => g.kind == Material && g.shortForBuilding > 0).OrderBy(g => g.key, StringComparer.Ordinal))
                p.rows.Add(new AheadRow
                {
                    kind = Material, key = g.key, label = g.label, have = g.count, count = g.count, target = g.count + g.shortForBuilding, status = Short, daysLeft = 0f,
                    cells = g.cells, parts = { g }
                });

            // The bills behind the rows.
            foreach (var row in new[] { meal, feed, med })
                Bills(x, p, row, declined);

            // The fields.
            Fields(x, p, raw, feed, declined);

            p.ideas = p.ideas.OrderBy(i => i.days).ThenBy(i => Array.IndexOf(Kinds, i.row?.kind ?? Raw)).ThenBy(i => i.key, StringComparer.Ordinal).ToList();
            return p;
        }

        private static AheadRow FoodRow(AheadInput x, string kind, float needEstimate, float days, float keepCap, float room)
        {
            var goods = x.goods.Where(g => g.kind == kind).ToList();
            var r = new AheadRow { kind = kind, key = kind, food = true, parts = goods.OrderByDescending(g => g.Nutrition).ThenBy(g => g.key, StringComparer.Ordinal).ToList() };
            r.have = goods.Sum(g => g.Nutrition);
            r.count = goods.Sum(g => g.count);
            r.cells = goods.Sum(g => g.cells);
            float measured = x.measured ? goods.Sum(g => g.usedADay * g.nutrition) : 0f;
            // Hunger is the floor: what wasn't eaten because there was none is still needed.
            r.useADay = Math.Max(needEstimate, measured);
            r.useMeasured = x.measured && measured >= needEstimate && measured > 0f;
            // What keeps: the meal the colony makes most and the meals in store (the shortest), or the raw stock's own
            // rot days weighed by nutrition.
            float keep = KeepDays(keepCap, 0f, false);
            if (goods.Count > 0)
            {
                if (kind == Meal) keep = Math.Min(keep, goods.Min(g => KeepDays(g.rotDays, g.frozenShare, x.freezer)));
                else
                {
                    float total = goods.Sum(g => g.Nutrition);
                    var keeps = goods.Select(g => KeepDays(g.rotDays, g.frozenShare, false)).ToList();
                    if (total > 0f && keeps.All(k => k < Far)) keep = Math.Min(keep, goods.Select((g, i) => keeps[i] * g.Nutrition).Sum() / total);
                }
            }
            r.keepDays = keep;
            r.targetDays = Math.Min(days, keep);
            r.target = Target(r.useADay, days, keep, room < Far ? r.have + Math.Max(0f, room) : Far);
            r.daysLeft = DaysLeft(r.have, r.useADay);
            r.status = Status(r.have, r.useADay, r.target);
            return r;
        }

        private static AheadRow CountRow(AheadInput x, string kind, float? estimate)
        {
            var goods = x.goods.Where(g => g.kind == kind).ToList();
            var r = new AheadRow { kind = kind, key = kind, parts = goods.OrderByDescending(g => g.count).ThenBy(g => g.key, StringComparer.Ordinal).ToList() };
            r.have = r.count = goods.Sum(g => g.count);
            r.cells = goods.Sum(g => g.cells);
            r.useADay = estimate ?? goods.Sum(g => g.usedADay);
            r.useMeasured = estimate == null && x.measured;
            r.targetDays = x.reserveDays;
            r.target = Target(r.useADay, x.reserveDays, Far);
            r.daysLeft = DaysLeft(r.have, r.useADay);
            r.status = Status(r.have, r.useADay, r.target);
            return r;
        }

        /// <summary>
        /// The bills for a short or low row: the biggest "do until" raised to cover the target, a bill added when there
        /// is none, a copy on a second bench when the benches can't keep up; meals that would rot, a bill lowered.
        /// Bills another mod sets are left alone.
        /// </summary>
        private static void Bills(AheadInput x, AheadPlan p, AheadRow row, HashSet<string> declined)
        {
            if (row == null || row.useADay <= 0f) return;
            var bills = x.bills.Where(b => b.kind == row.kind).ToList();
            row.managedBills = bills.Count(b => b.managed);
            var own = bills.Where(b => !b.managed).ToList();
            var active = own.Where(b => !b.suspended).ToList();
            bool forever = active.Any(b => b.mode == Forever);
            float cover = active.Where(b => b.mode == Until || b.mode == Times).Sum(b => b.target * b.unit);
            row.billsCover = forever ? Far : cover;
            bool wanting = row.status == Short || row.status == Low;
            if (wanting && !forever && cover < row.target * BillCovered)
            {
                var pick = active.Where(b => b.mode == Until && !declined.Contains(RaiseKey(b))).OrderByDescending(b => b.target).ThenBy(b => b.key, StringComparer.Ordinal).FirstOrDefault();
                if (pick != null)
                {
                    int to = RoundUp5(pick.target + (float)Math.Ceiling((row.target - cover) / Math.Max(0.01f, pick.unit)));
                    if (to > pick.target)
                        p.ideas.Add(new AheadIdea { key = RaiseKey(pick), kind = "raise", area = row.kind, days = row.daysLeft, row = row, bill = pick, from = pick.target, to = to, gives = (to - pick.target) * pick.unit });
                }
                else if (own.Count == 0)
                {
                    var add = x.canAdd.FirstOrDefault(a => a.kind == row.kind && !declined.Contains(AddKey(a)));
                    if (add != null)
                    {
                        int to = RoundUp5((float)Math.Ceiling(row.target / Math.Max(0.01f, add.unit)));
                        p.ideas.Add(new AheadIdea { key = AddKey(add), kind = "add", area = row.kind, days = row.daysLeft, row = row, add = add, to = to, gives = to * add.unit });
                    }
                }
            }
            // The benches can't keep up with the use: a copy of the bill on another bench.
            if (wanting && active.Count > 0 && active.All(b => b.canMakeADay < Far))
            {
                float canMake = active.Sum(b => b.canMakeADay * b.unit);
                if (canMake < row.useADay * BillCovered)
                {
                    var b = active.Where(a => a.freeBench != null && !declined.Contains(SecondKey(a))).OrderByDescending(a => a.target).ThenBy(a => a.key, StringComparer.Ordinal).FirstOrDefault();
                    if (b != null)
                        p.ideas.Add(new AheadIdea { key = SecondKey(b), kind = "second", area = row.kind, days = row.daysLeft, row = row, bill = b, to = b.target, gives = canMake });
                }
            }
            // Meals the bills would make more of than keep: the biggest bill lowered.
            if (row.kind == Meal && row.keepDays < Far && !forever && cover > row.target * SpoilLower && row.have >= row.target)
            {
                var pick = active.Where(b => b.mode == Until && !declined.Contains(LowerKey(b))).OrderByDescending(b => b.target).ThenBy(b => b.key, StringComparer.Ordinal).FirstOrDefault();
                if (pick != null)
                {
                    int to = Math.Max(1, RoundUp5(pick.target - (cover - row.target) / Math.Max(0.01f, pick.unit)));
                    if (to < pick.target)
                        p.ideas.Add(new AheadIdea { key = LowerKey(pick), kind = "lower", area = row.kind, row = row, bill = pick, from = pick.target, to = to });
                }
            }
        }

        private static float[] FactorsOf(float[] own, float[] typical) => own != null && own.Length > 0 ? own : typical;

        /// <summary>
        /// The field plan: the crop food the colony needs over the year's cycle against what its food fields give over
        /// it (each crop by its own growing days), and the shortfall covered in steps: fields grown onto free fertile
        /// ground next to them (the best yield first), cells switched from a crop the colony has far more of than it
        /// uses (judged by nutrition or by days of use, never by the number of pieces), then a new field.
        /// </summary>
        private static void Fields(AheadInput x, AheadPlan p, AheadRow raw, AheadRow feed, HashSet<string> declined)
        {
            var typical = x.growFactors ?? new float[0];
            int cycle = p.cycleDays;
            p.cropNeed = raw.useADay * Clamp01(x.plantShare) + (feed != null && (x.feedAnimals > 0 || feed.have > 0f) ? feed.useADay : 0f);
            var food = x.fields.Where(f => f.food && f.cells > 0).ToList();
            float PerCycle(float perCell, float[] own) => perCell * GrowingDays(FactorsOf(own, typical));
            float make = food.Sum(f => f.cells * PerCycle(f.perCellADay, f.growFactors));
            p.cropGive = cycle > 0 ? make / cycle : 0f;

            // When the crop food runs out with the fields as they are.
            var byDay = new float[cycle];
            foreach (var f in food)
            {
                var ff = FactorsOf(f.growFactors, typical);
                for (int d = 0; d < cycle; d++) byDay[d] += f.cells * f.perCellADay * (d < ff.Length ? ff[d] : 1f);
            }
            float stock = raw.have * Clamp01(x.plantShare) + (feed != null ? feed.have : 0f);
            p.runsShortIn = RunsShortIn(stock, p.cropNeed, byDay);

            float need = p.cropNeed * cycle;
            float shortfall = need - make;
            var crops = x.crops.Where(c => c.perCellADay > 0f).OrderByDescending(c => PerCycle(c.perCellADay, c.growFactors)).ThenBy(c => c.key, StringComparer.Ordinal).ToList();
            var bestCrop = crops.FirstOrDefault();
            float bestPer = bestCrop != null ? PerCycle(bestCrop.perCellADay, bestCrop.growFactors) : food.Select(f => PerCycle(f.perCellADay, f.growFactors)).DefaultIfEmpty(0f).Max();
            if (p.cropNeed <= 0f || shortfall <= need * FieldTolerance) return;
            p.cellsShort = CellsFor(shortfall, bestPer);
            float days = p.runsShortIn;
            float remaining = shortfall;

            // 1. Fields grown onto the free fertile ground next to them, the best yield first, two at most.
            int grown = 0;
            foreach (var f in food.Where(f => f.freeNext >= MinCellsAStep).OrderByDescending(f => PerCycle(f.perCellADay, f.growFactors)).ThenBy(f => f.key, StringComparer.Ordinal))
            {
                if (remaining <= 0f || grown >= 2) break;
                if (declined.Contains(GrowKey(f))) continue;
                float per = PerCycle(f.perCellADay, f.growFactors);
                int cells = Math.Min(Math.Min(CellsFor(remaining, per), f.freeNext), MaxCellsAStep);
                if (cells < MinCellsAStep) continue;
                p.ideas.Add(new AheadIdea { key = GrowKey(f), kind = "grow", area = "fields", days = days, row = raw, field = f, cells = cells, from = f.cells, to = f.cells + cells, gives = cells * per / cycle });
                remaining -= cells * per;
                grown++;
            }

            // 2. Cells switched from a crop the colony has far more of than it uses.
            foreach (var f in x.fields.Where(f => f.cells > 0 && OverSupplied(f, p.cropNeed)).OrderByDescending(f => f.cells).ThenBy(f => f.key, StringComparer.Ordinal))
            {
                if (remaining <= 0f) break;
                float own = f.food ? PerCycle(f.perCellADay, f.growFactors) : 0f;
                var to = crops.FirstOrDefault(c => c.key != f.crop && !declined.Contains(SwitchKey(f, c)) && PerCycle(c.perCellADay, c.growFactors) > own * SwitchGain);
                if (to == null) continue;
                float gain = PerCycle(to.perCellADay, to.growFactors) - own;
                int cells = Math.Min(Math.Min(CellsFor(remaining, gain), f.cells), MaxCellsAStep);
                if (cells < MinCellsAStep) continue;
                p.ideas.Add(new AheadIdea { key = SwitchKey(f, to), kind = "switch", area = "fields", days = days, row = raw, field = f, crop = to, cells = cells, gives = cells * gain / cycle });
                remaining -= cells * gain;
            }

            // 3. A new field of the best crop, on free fertile ground near the fields.
            if (remaining > 0f && x.newGround >= MinCellsAStep)
            {
                var c = crops.FirstOrDefault(k => !declined.Contains(NewKey(k)));
                if (c != null)
                {
                    float per = PerCycle(c.perCellADay, c.growFactors);
                    int cells = Math.Min(Math.Min(CellsFor(remaining, per), MaxCellsAStep), x.newGround);
                    if (cells >= MinCellsAStep)
                        p.ideas.Add(new AheadIdea { key = NewKey(c), kind = "new", area = "fields", days = days, row = raw, crop = c, cells = cells, gives = cells * per / cycle });
                }
            }
        }

        /// <summary>
        /// Far more than the colony uses: a food crop whose stock alone covers a year of the colony's crop need (by its
        /// nutrition, never by its count); another crop whose stock covers a year of its own use, or that nothing used
        /// over the record while two of the field's harvests lie in store.
        /// </summary>
        public static bool OverSupplied(AheadField f, float cropNeed)
        {
            if (f == null) return false;
            if (f.food) return cropNeed > 0f && f.stockNutrition / cropNeed > OverDays;
            if (f.useADay > 0f) return f.stock / f.useADay > OverDays;
            return f.measured && f.harvest > 0f && f.stock >= f.harvest * 2f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
