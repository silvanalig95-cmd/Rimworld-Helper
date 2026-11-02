using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The colony's rooms and builders as plain numbers (Building/GrowthNeeds.cs fills it from the map).</summary>
    public class GrowthInput
    {
        /// <summary>Who lives here: colonists (quest lodgers among them), the colony's slaves, Hospitality's guests, prisoners.</summary>
        public int colonists, slaves, guests, prisoners, lodgers;
        /// <summary>Prisoners the wardens are recruiting: each wants a bed and a seat once they join.</summary>
        public int joining;
        /// <summary>People an accepted quest will bring to join the colony: each wants a bed and a seat once they come.</summary>
        public int questJoining;
        /// <summary>Colonists and slaves who barely eat (foodless, fed by light or minerals): no seat at the table for them.</summary>
        public int fasting;

        /// <summary>Sleeping places in the colony's own beds (not medical, not for prisoners or guests), and in bed blueprints and frames.</summary>
        public int beds, bedsPlanned;
        /// <summary>Colonists sleeping in a room they share with others (a barracks).</summary>
        public int shared;

        /// <summary>Seats at the colony's eating tables, built, and planned (in blueprints and frames, and in dining rooms being built).</summary>
        public int seats, seatsPlanned;

        /// <summary>Medical beds (not for prisoners), and the beds of hospitals being built.</summary>
        public int medicalBeds, medicalPlanned;
        /// <summary>People laid up now (in a medical bed, or who should be); the most at once on a day the record covers, and the days it covers.</summary>
        public int patientsNow, patientsMost, patientDays;

        /// <summary>Nutrition a day: the raw food the cooks need, and the meals eaten.</summary>
        public float rawUseADay, mealUseADay;
        /// <summary>The days of meals and of raw food the stock targets keep (the settings).</summary>
        public float mealDays = 2f, reserveDays = 5f;
        /// <summary>Days without growing in the cycle ahead, and the days until the first of them (-1: none ahead).</summary>
        public int withoutDays, winterIn = -1;
        /// <summary>Nutrition a place for one stack holds: of the colony's raw food as it lies in store, and of its usual meal.</summary>
        public float rawPerSlot = 3.75f, mealPerSlot = 9f;
        /// <summary>Places for a stack in cold storage that takes food (a room below zero, a powered fridge), and in cold rooms being built.</summary>
        public int coldSlots, coldPlanned;

        /// <summary>Production benches (not spots, not the kitchen's or the lab's), those standing in a workshop, the bench places still free in workshops, and those of workshops being built.</summary>
        public int benches, benchesInWorkshop, workshopSpare, workshopPlanned;
        /// <summary>Benches with more work waiting (that can go ahead) than their hands do in two days; of those, the ones with no second bench of their kind.</summary>
        public int busy, busyAlone;

        /// <summary>Stacks lying about at home that a store would take, those out under the sky, the free places in the stores (the cold ones aside), and those of storerooms being built.</summary>
        public int lying, lyingOut, freeSlots, storagePlanned;

        /// <summary>Hours of building waiting in blueprints and frames at a plain builder's speed; the builders' hours a day at theirs; how many build.</summary>
        public float queuedHours, builderHoursADay;
        public int builders;
        /// <summary>The miners' hours a day at their mining speeds, and how many mine; of the queued hours, the mining marked inside the rooms being built (as builders' hours).</summary>
        public float minerHoursADay, queuedMineHours;
        public int miners;

        /// <summary>Room steps the player set aside ("Not this"): not suggested.</summary>
        public HashSet<string> declined = new HashSet<string>();
    }

    /// <summary>One kind of room: what the colony needs, what is built and planned, and how soon it falls short.</summary>
    public class GrowthRow
    {
        public string kind;
        /// <summary>What the colony needs now, and once what is coming has come (the prisoners joining, the winter).</summary>
        public int need, soon;
        public int built, planned;
        /// <summary>Days until it falls short: 0 short now, GrowthMath.NoRush when it isn't a matter of days.</summary>
        public float days = GrowthMath.NoRush;
        /// <summary>4 critical to 0 an idea, as the suggestions rank.</summary>
        public int level;
        /// <summary>What makes it short (GrowthMath.WhyNow, WhyJoining, WhyWinter, WhySize, WhyInjuries, WhyBusy, WhyOutside, WhyLying); null when it isn't.</summary>
        public string why;
        /// <summary>The cold store: the days of raw food it holds beside the meals, and the days the stock target and the winter ahead want.</summary>
        public float holdsDays, wantsDays;
        /// <summary>The cold store: the places the meals take, and those the raw food takes now and through the winter.</summary>
        public int mealSlots, rawSlots, winterSlots;

        public int Short => Math.Max(0, need - built - planned);
        public int SoonShort => Math.Max(0, Math.Max(need, soon) - built - planned);
        public bool Wanting => SoonShort > 0;
    }

    /// <summary>A room the colony should build next: its kind, how much it should add, how soon, and the row behind it.</summary>
    public class GrowthIdea
    {
        public string key, kind;
        /// <summary>Beds, seats, places or bench places it should add.</summary>
        public int want;
        public float days = GrowthMath.NoRush;
        public int level;
        public GrowthRow row;
    }

    /// <summary>The rows, one per kind of room, and the rooms to build, the most urgent first.</summary>
    public class GrowthPlan
    {
        public List<GrowthRow> rows = new List<GrowthRow>();
        public List<GrowthIdea> ideas = new List<GrowthIdea>();

        public GrowthRow Row(string kind) => rows.FirstOrDefault(r => r.kind == kind);
    }

    /// <summary>
    /// Building ahead (Building/GrowthNeeds.cs, Building/BuildAhead.cs) as plain rules: per kind of room, what the colony
    /// needs against what is built and planned, and how soon it falls short. Beds for everyone who sleeps here and the
    /// prisoners being recruited and the people an accepted quest brings; cold storage for the meals and the raw food the stock targets keep, then for the
    /// winter's days without growing (in places for a stack, by the nutrition a place of the colony's own food holds);
    /// seats for everyone who eats here, guests too; hospital beds by the colony's size and the most laid up at once
    /// lately; bench places in the workshops for the benches and a second one where a bench is busy; store places for
    /// what lies about. The rooms to build come in order of urgency, one or two at a time, and only while the builders
    /// can do what is queued in a couple of days. A project is followed in stages, and a room keeps a side free to
    /// grow into. The starting ratios are rules of thumb; everything else is the colony's own numbers. Pure, so
    /// Tools/Tests can check it.
    /// </summary>
    public static class GrowthMath
    {
        public const float NoRush = SuggestMath.NoRush;

        public const string Beds = "beds", Cold = "cold", Dining = "dining", Hospital = "hospital", Workshop = "workshop", Storage = "storage";
        /// <summary>The kinds in the order the rows are shown, and the order of rooms equally urgent.</summary>
        public static readonly string[] Kinds = { Beds, Cold, Dining, Hospital, Workshop, Storage };

        public const string WhyNow = "now", WhyJoining = "joining", WhyWinter = "winter", WhySize = "size", WhyInjuries = "injuries", WhyBusy = "busy",
            WhyOutside = "outside", WhyLying = "lying";

        /// <summary>One seat fewer than eaters is enough: someone is always asleep or out.</summary>
        public const int DiningSlack = 1;
        /// <summary>One hospital bed for every four people (a rule of thumb), or as many as were laid up at once lately if more.</summary>
        public const float HospitalShare = 0.25f;
        /// <summary>A bench with more work waiting than its hands do in this many days is busy.</summary>
        public const float BusyDays = 2f;
        /// <summary>Hours a day a hand works at one bench.</summary>
        public const float BenchHoursADay = 8f;
        /// <summary>Fewer stacks than this lying about is the haulers' ordinary lag, not a lack of room.</summary>
        public const int MinLying = 5;
        /// <summary>The next one or two rooms are suggested, never more at once.</summary>
        public const int MaxRoomSteps = 2;
        /// <summary>Never more building queued than the builders do in this many days.</summary>
        public const float BudgetDays = 2f;
        /// <summary>Hours of a builder's day spent building (the rest is other work, meals and sleep).</summary>
        public const float BuildHoursADay = 6f;
        /// <summary>Building work a builder of speed 1 does a tick (the game's construction job).</summary>
        public const float WorkPerTick = 1.7f;
        public const float TicksPerHour = 2500f;
        /// <summary>Building takes about twice its work: the materials are fetched and the builders walk between pieces.</summary>
        public const float FetchFactor = 2f;
        /// <summary>A side kept free to grow is this deep at least, and at most.</summary>
        public const int MinGrow = 3, MaxGrow = 6;

        public static string RoomKey(string kind) => "build:room:" + kind;

        // ---------------- The rows ----------------

        /// <summary>Places for a stack that hold so much nutrition at so much a place.</summary>
        public static int Slots(float nutrition, float perSlot) => nutrition <= 0f ? 0 : perSlot <= 0f ? 0 : (int)Math.Ceiling(nutrition / perSlot - 0.0001f);

        /// <summary>The first day ahead with no growing (AheadMath.NoGrowFactor), -1 when none is.</summary>
        public static int WinterIn(float[] growFactors)
        {
            if (growFactors == null) return -1;
            for (int i = 0; i < growFactors.Length; i++)
                if (growFactors[i] < AheadMath.NoGrowFactor) return i;
            return -1;
        }

        public static GrowthRow BedsRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Beds, built = x.beds, planned = x.bedsPlanned };
            r.need = x.colonists + x.slaves;
            r.soon = r.need + x.joining + x.questJoining;
            if (r.Short > 0) Mark(r, 0f, 4, WhyNow);
            else if (r.SoonShort > 0) Mark(r, NoRush, 2, WhyJoining);
            return r;
        }

        public static GrowthRow ColdRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Cold, built = x.coldSlots, planned = x.coldPlanned };
            if (x.rawUseADay <= 0f && x.mealUseADay <= 0f) return r;
            r.mealSlots = Slots(x.mealUseADay * x.mealDays, x.mealPerSlot);
            r.rawSlots = Slots(x.rawUseADay * x.reserveDays, x.rawPerSlot);
            r.winterSlots = Slots(x.rawUseADay * (x.reserveDays + Math.Max(0, x.withoutDays)), x.rawPerSlot);
            r.need = r.mealSlots + r.rawSlots;
            r.soon = r.mealSlots + r.winterSlots;
            r.holdsDays = x.rawUseADay > 0f ? Math.Max(0, x.coldSlots - r.mealSlots) * x.rawPerSlot / x.rawUseADay : NoRush;
            r.wantsDays = x.reserveDays + Math.Max(0, x.withoutDays);
            if (r.Short > 0) Mark(r, 0f, 3, WhyNow);
            else if (r.SoonShort > 0)
            {
                float days = x.winterIn >= 0 ? x.winterIn : NoRush;
                Mark(r, days, Math.Max(1, days < NoRush ? SuggestMath.LevelOf(days) : 1), WhyWinter);
            }
            return r;
        }

        /// <summary>Who eats at the colony's tables: colonists, slaves and guests, but not those who barely eat.</summary>
        public static int Eaters(GrowthInput x) => Math.Max(0, x.colonists + x.slaves + x.guests - x.fasting);

        public static GrowthRow DiningRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Dining, built = x.seats, planned = x.seatsPlanned };
            int eaters = Eaters(x);
            if (eaters < 2) return r;
            r.need = Math.Max(1, eaters - DiningSlack);
            r.soon = Math.Max(1, eaters + x.joining + x.questJoining - DiningSlack);
            if (r.Short > 0) Mark(r, 0f, 3, WhyNow);
            else if (r.SoonShort > 0) Mark(r, NoRush, 2, WhyJoining);
            return r;
        }

        /// <summary>Hospital beds for so many people: one for every four, at least one.</summary>
        public static int HospitalBedsFor(int people) => people < 2 ? 0 : Math.Max(1, (int)Math.Ceiling(people * HospitalShare - 0.0001f));

        public static GrowthRow HospitalRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Hospital, built = x.medicalBeds, planned = x.medicalPlanned };
            int people = x.colonists + x.slaves;
            int size = HospitalBedsFor(people);
            if (size == 0) return r;
            r.need = r.soon = Math.Max(size, Math.Max(x.patientsMost, x.patientsNow));
            if (r.Short <= 0) return r;
            if (x.patientsNow > x.medicalBeds) Mark(r, 0f, 3, WhyNow);
            else if (x.patientsMost > size) Mark(r, NoRush, 2, WhyInjuries);
            else Mark(r, NoRush, 1, WhySize);
            return r;
        }

        public static GrowthRow WorkshopRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Workshop, built = x.benchesInWorkshop + x.workshopSpare, planned = x.workshopPlanned };
            if (x.benches <= 0) return r;
            r.need = r.soon = x.benches + Math.Max(0, x.busyAlone);
            if (r.Short <= 0) return r;
            if (x.busyAlone > 0) Mark(r, NoRush, 2, WhyBusy);
            else Mark(r, NoRush, 1, WhyOutside);
            return r;
        }

        public static GrowthRow StorageRow(GrowthInput x)
        {
            var r = new GrowthRow { kind = Storage, built = x.freeSlots, planned = x.storagePlanned, need = x.lying, soon = x.lying };
            if (x.lying < MinLying)
            {
                r.need = r.soon = Math.Min(x.lying, r.built + r.planned);
                return r;
            }
            if (r.Short > 0) Mark(r, NoRush, x.lyingOut > 0 ? 2 : 1, WhyLying);
            return r;
        }

        private static void Mark(GrowthRow r, float days, int level, string why)
        {
            r.days = days;
            r.level = level;
            r.why = why;
        }

        // ---------------- The plan ----------------

        /// <summary>The rows for every kind of room, and the rooms to build, the most urgent first (the game side takes the next one or two that fit).</summary>
        public static GrowthPlan Plan(GrowthInput x)
        {
            var p = new GrowthPlan();
            if (x == null) return p;
            p.rows.Add(BedsRow(x));
            p.rows.Add(ColdRow(x));
            p.rows.Add(DiningRow(x));
            p.rows.Add(HospitalRow(x));
            p.rows.Add(WorkshopRow(x));
            p.rows.Add(StorageRow(x));
            var declined = x.declined ?? new HashSet<string>();
            foreach (var r in p.rows)
            {
                if (!r.Wanting || r.why == null) continue;
                string key = RoomKey(r.kind);
                if (declined.Contains(key)) continue;
                p.ideas.Add(new GrowthIdea { key = key, kind = r.kind, want = r.Short > 0 ? r.Short : r.SoonShort, days = r.days, level = r.level, row = r });
            }
            p.ideas = p.ideas.OrderBy(i => Math.Min(i.days, NoRush)).ThenByDescending(i => i.level).ThenBy(i => Array.IndexOf(Kinds, i.kind)).ToList();
            return p;
        }

        // ---------------- The builders ----------------

        /// <summary>Hours a plain builder takes for so much building work, the fetching included.</summary>
        public static float BuildHours(float work) => work <= 0f ? 0f : work * FetchFactor / (WorkPerTick * TicksPerHour);

        /// <summary>The builders' hours a day, by their speeds added up (1 each for plain builders).</summary>
        public static float BuilderHoursADay(float speedSum) => Math.Max(0f, speedSum) * BuildHoursADay;

        /// <summary>Days the builders take for so many hours of building.</summary>
        public static float DaysOfWork(float hours, float hoursADay) => hours <= 0f ? 0f : hoursADay <= 0f ? NoRush : hours / hoursADay;

        /// <summary>
        /// A room may be suggested when the builders could do it and what is queued in a couple of days; or, when nothing
        /// is queued, whatever its size (else a big room would never come). Never when nobody builds.
        /// </summary>
        public static bool Fits(float queuedHours, float roomHours, float hoursADay) =>
            hoursADay > 0f && (queuedHours <= 0.01f || queuedHours + Math.Max(0f, roomHours) <= BudgetDays * hoursADay);

        /// <summary>How many of the rooms, in order, fit one after another on what is queued.</summary>
        public static int HowManyFit(float queuedHours, IList<float> roomHours, float hoursADay)
        {
            int n = 0;
            float q = Math.Max(0f, queuedHours);
            foreach (float h in roomHours ?? new float[0])
            {
                if (!Fits(q, h, hoursADay)) break;
                q += Math.Max(0f, h);
                n++;
            }
            return n;
        }

        /// <summary>A bench is busy when the work waiting at it is more than its hands do in two days.</summary>
        public static bool Busy(float queuedHours, float handHoursADay) => handHoursADay > 0f && queuedHours > BusyDays * handHoursADay;

        /// <summary>A bench worked this share of a hand's day at one bench, by the record, is in use most of the day.</summary>
        public const float BusyShare = 0.75f;

        /// <summary>Hours a day a bench was worked, by the record: the batches it made a day at the hours a batch takes its hand there.</summary>
        public static float WorkedHours(float batchesADay, float hoursABatch) => Math.Max(0f, batchesADay) * Math.Max(0f, hoursABatch);

        /// <summary>
        /// Busy by the record of the last days: the bench was worked most of a hand's day (BusyShare of BenchHoursADay) and
        /// at least as much work again still waits, so a second bench would take work it can't get to.
        /// </summary>
        public static bool BusyByRecord(float workedHoursADay, float queuedHours) =>
            workedHoursADay >= BusyShare * BenchHoursADay && queuedHours >= Math.Max(0.5f, workedHoursADay);

        /// <summary>Busy as the record has it when the record covers a day, else by the work queued alone.</summary>
        public static bool BusyBench(bool measured, float workedHoursADay, float queuedHours, float handHoursADay) =>
            measured ? BusyByRecord(workedHoursADay, queuedHours) : Busy(queuedHours, handHoursADay);

        // ---------------- Mining for a room ----------------

        /// <summary>A pick's swing every so many ticks at mining speed 1, and the damage a swing does to natural rock and to anything else (JobDriver_Mine).</summary>
        public const float TicksAPickHit = 100f, PickHitNatural = 80f, PickHitOther = 40f;

        /// <summary>Hours a miner of speed 1 takes to mine one cell of rock with so many hit points.</summary>
        public static float MineHours(float hitPoints, bool natural) =>
            hitPoints <= 0f ? 0f : (float)Math.Ceiling(hitPoints / (natural ? PickHitNatural : PickHitOther)) * TicksAPickHit / TicksPerHour;

        /// <summary>The miners' hours a day, by their mining speeds added up (a miner's day like a builder's).</summary>
        public static float MinerHoursADay(float speedSum) => Math.Max(0f, speedSum) * BuildHoursADay;

        /// <summary>
        /// Mining counted in the builders' hours: the days the miners take for it, as so many of the builders' hours (the
        /// room's hours are weighed against the builders' days). Nobody mining: the plain hours.
        /// </summary>
        public static float MineAsBuildHours(float mineHours, float minerHoursADay, float builderHoursADay) =>
            mineHours <= 0f ? 0f : minerHoursADay <= 0f || builderHoursADay <= 0f ? mineHours : mineHours / minerHoursADay * builderHoursADay;

        /// <summary>Free floor a bench takes in a workshop: the bench, its work spot and a way past.</summary>
        public const int BenchPlaceCells = 10;

        /// <summary>Benches a workshop's free floor still has room for.</summary>
        public static int SparePlaces(int freeCells) => freeCells <= 0 ? 0 : freeCells / BenchPlaceCells;

        // ---------------- A project in stages ----------------

        public const int Clear = 0, Walls = 1, Climate = 2, Floors = 3, Furniture = 4, Finished = 5;
        public static readonly string[] Stages = { "clear", "walls", "climate", "floors", "furniture", "finished" };

        /// <summary>The stage a project is at: the first with work left (clearing; walls, doors and roof; climate and power; floors; furniture).</summary>
        public static int StageOf(IList<int> left)
        {
            if (left == null) return Finished;
            for (int i = 0; i < Finished && i < left.Count; i++)
                if (left[i] > 0) return i;
            return Finished;
        }

        /// <summary>A stage step worked: the project moved on to a later stage, or a quarter of the stage's pieces (one at least) got done.</summary>
        public static bool StageMoved(int stageBefore, int leftBefore, int stageNow, int leftNow) =>
            stageNow > stageBefore || stageNow == stageBefore && leftNow <= leftBefore - Math.Max(1, leftBefore / 4);

        /// <summary>A room placed is going: its walls stand (climate or later), or a third of its work is done.</summary>
        public static bool RoomGoing(int stageNow, float workLeft, float workAtStart) => stageNow >= Climate || workAtStart > 0f && workLeft <= workAtStart * (2f / 3f);

        // ---------------- Room to grow ----------------

        /// <summary>Sides of a room: 0 north (+z), 1 east (+x), 2 south, 3 west.</summary>
        public const int North = 0, East = 1, South = 2, West = 3;

        /// <summary>How deep a room's side to grow into is kept: about half the room across it, three to six cells.</summary>
        public static int KeepDepth(int w, int h, int side)
        {
            int across = side == North || side == South ? h : w;
            return Math.Max(MinGrow, Math.Min(MaxGrow, (across + 1) / 2));
        }

        /// <summary>
        /// The side a room keeps free to grow into: of the sides not holding its door, the one with the most free ground
        /// beyond the wall (at least <see cref="MinGrow"/> deep, at most what is kept), the one facing away from the door
        /// on a tie. -1 when no side has room.
        /// </summary>
        public static int GrowSide(IList<int> freeDepth, int doorSide, IList<int> keep)
        {
            int best = -1, bestDepth = -1;
            int opposite = (doorSide + 2) % 4;
            for (int i = 0; i < 4; i++)
            {
                int side = (opposite + i) % 4;
                if (side == doorSide || freeDepth == null || side >= freeDepth.Count) continue;
                int depth = Math.Min(freeDepth[side], keep != null && side < keep.Count ? keep[side] : MaxGrow);
                if (depth < MinGrow || depth <= bestDepth) continue;
                best = side;
                bestDepth = depth;
            }
            return best;
        }

        /// <summary>The cells beyond a room's side kept free: x, z, width and height, outside its walls.</summary>
        public static (int x, int z, int w, int h) Strip(int x, int z, int w, int h, int side, int depth)
        {
            switch (side)
            {
                case North: return (x, z + h, w, depth);
                case East: return (x + w, z, depth, h);
                case South: return (x, z - depth, w, depth);
                default: return (x - depth, z, depth, h);
            }
        }

        public static bool Inside(int px, int pz, (int x, int z, int w, int h) r) => px >= r.x && pz >= r.z && px < r.x + r.w && pz < r.z + r.h;
    }
}
