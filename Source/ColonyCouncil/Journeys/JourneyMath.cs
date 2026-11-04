using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers of a journey made ready (Journeys/): when to set off by default, how each traveller's last night is
    /// shifted so they wake rested in time, when each of the last hours' steps opens, what to pack for the days on the
    /// road, which travel food comes first, how long the food and feed last against the road, the list's groups and
    /// counts as the player changes them, what to change into for the weather on the way, and which edge of the map and
    /// which spot at the gate to leave by. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class JourneyMath
    {
        // ---------------- When ----------------

        /// <summary>Setting off at dawn, by default.</summary>
        public const int DawnHour = 6;

        /// <summary>The default departure is at least this many hours off: time for a shifted night's sleep and the last hours.</summary>
        public const int DefaultLeadHours = 10;

        /// <summary>The earliest departure the plan allows: the last steps need a couple of hours.</summary>
        public const int MinLeadHours = 2;

        /// <summary>A departure is planned at most this many days ahead.</summary>
        public const int MaxDaysAhead = 30;

        /// <summary>Waiting for someone who isn't ready moves the departure by an hour.</summary>
        public const int WaitHours = 1;

        /// <summary>The plan's changes to timetables start this many hours before the departure (the timetable is a day long).</summary>
        public const int PrepareHours = 24;

        /// <summary>Hours from the start of this hour to the next dawn at least <paramref name="minAhead"/> hours off.</summary>
        public static int DefaultHoursAhead(int hourNow, int minAhead = DefaultLeadHours, int dawn = DawnHour)
        {
            int h = Mod(dawn - hourNow, 24);
            while (h < minAhead) h += 24;
            return h;
        }

        /// <summary>A chosen departure kept within what the plan allows: not sooner than the lead, not past the last day.</summary>
        public static int ClampHoursAhead(int hoursAhead) => Math.Max(MinLeadHours, Math.Min(MaxDaysAhead * 24, hoursAhead));

        // ---------------- The night before ----------------

        /// <summary>A timetable hour, as the plan reads and sets it.</summary>
        public enum Slot : byte
        {
            Anything,
            Work,
            Joy,
            Sleep,
            /// <summary>Another mod's or the game's other kind (meditation): left alone, counted as awake and free.</summary>
            Other
        }

        /// <summary>The game's own: in their sleeping hours they go to bed only below this much rest.</summary>
        public const float FallAsleepBelow = 0.75f;

        /// <summary>The game's own: in free hours they go to bed only when this tired.</summary>
        public const float SleepWhenFreeBelow = 0.3f;

        /// <summary>Rested enough at the departure.</summary>
        public const float RestTarget = 0.85f;

        /// <summary>Never so long awake that rest falls under this.</summary>
        public const float RestFloor = 0.15f;

        /// <summary>A shifted night lasts this long at least and at most.</summary>
        public const int MinNight = 3, MaxNight = 11;

        /// <summary>Recreation is topped up before the night when it would be under this at bedtime, up to the target.</summary>
        public const float JoyLow = 0.6f, JoyTarget = 0.9f;

        public const int MaxJoyHours = 3;

        /// <summary>How the last day before a departure is laid out for one traveller (hours counted from the start of this hour).</summary>
        public sealed class NightPlan
        {
            /// <summary>The timetable for each hour from now to the departure.</summary>
            public Slot[] slots;

            /// <summary>The shifted night: to bed at <see cref="bed"/>, up at <see cref="wake"/>; -1 when their own hours are kept.</summary>
            public int bed = -1, wake = -1;

            /// <summary>Recreation hours put in before the night (from <see cref="joyFrom"/>); none when -1.</summary>
            public int joyFrom = -1, joyHours;

            /// <summary>What the plan expects: rest at the departure, and the lowest it gets on the way.</summary>
            public float restAtDeparture, restLowest;

            public bool Shifted => bed >= 0;
        }

        /// <summary>
        /// Rest through a run of timetable hours, by the game's rules in short: in their sleeping hours they go to bed
        /// below <see cref="FallAsleepBelow"/>, in free hours only below <see cref="SleepWhenFreeBelow"/>, never in work
        /// hours, and sleep until fully rested. Rest falls by <paramref name="fall"/> an hour awake and grows by
        /// <paramref name="gain"/> an hour asleep.
        /// </summary>
        public static void Simulate(Slot[] slots, float rest, float fall, float gain, out float end, out float lowest) =>
            Simulate(slots, rest, fall, gain, slots.Length, out end, out lowest, out _);

        /// <summary>The same, and how many hours from <paramref name="upBy"/> on they are still asleep (they sleep on until rested, whatever the hour).</summary>
        public static void Simulate(Slot[] slots, float rest, float fall, float gain, int upBy, out float end, out float lowest, out int late)
        {
            bool asleep = false;
            lowest = rest;
            late = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (!asleep && ((s == Slot.Sleep && rest < FallAsleepBelow) || (s != Slot.Work && rest < SleepWhenFreeBelow))) asleep = true;
                if (asleep && i >= upBy) late++;
                if (asleep)
                {
                    rest = Math.Min(1f, rest + gain);
                    if (rest >= 1f) asleep = false;
                }
                else rest = Math.Max(0f, rest - fall);
                if (rest < lowest) lowest = rest;
            }
            end = rest;
        }

        private static int Changes(Slot[] a, Slot[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) n++;
            return n;
        }

        /// <summary>An hour still asleep when the last steps start weighs this much against a plan.</summary>
        public const float LatePenalty = 2f;

        private static float Score(Slot[] s, Slot[] baseline, float rest, float fall, float gain, int upBy, out float end, out float low)
        {
            Simulate(s, rest, fall, gain, upBy, out end, out low, out int late);
            // Near full at the departure counts, past that it doesn't; up in time for the last steps; never run anyone
            // into the ground; fewer changes win.
            float score = Math.Min(end, 0.95f) * 10f - late * LatePenalty;
            if (low < RestFloor) score -= (RestFloor - low) * 200f;
            return score - 0.02f * Changes(s, baseline);
        }

        /// <summary>Up this many hours before the departure after a shifted night: in time for the meal, the wash and the gate.</summary>
        public const int WakeHours = 3;

        /// <summary>
        /// One traveller's last day: the last <paramref name="freeHours"/> hours before the departure free of work, the
        /// night before moved (when <paramref name="shiftSleep"/>) so they wake <see cref="WakeHours"/> before it as
        /// rested as it can make them without running them into the ground, and an hour or more of recreation before that
        /// night when it would be low by then. <paramref name="original"/> holds their own timetable for each hour from now
        /// to the departure. Rest and recreation are 0 to 1; the rates are per hour.
        /// </summary>
        public static NightPlan PlanNight(Slot[] original, int freeHours, float rest, float fall, float gain, float joy, float joyFall, float joyGain,
            bool shiftSleep)
        {
            int n = original.Length;
            var plan = new NightPlan();
            int free = Math.Max(0, freeHours);
            int wake = Math.Max(0, n - Math.Min(free, WakeHours));
            // The last hours are free: no work, and not back to bed.
            var baseline = (Slot[])original.Clone();
            for (int i = Math.Max(0, n - free); i < n; i++) baseline[i] = Slot.Anything;
            var best = baseline;
            float bestScore = Score(baseline, baseline, rest, fall, gain, wake, out float bestEnd, out float bestLow);
            int bestBed = -1;
            if (shiftSleep && wake >= MinNight)
            {
                var trial = new Slot[n];
                foreach (int cancel in new[] { 0, 3, 6, 9, 12 })
                    for (int len = MinNight; len <= MaxNight; len++)
                    {
                        int bed = wake - len;
                        if (bed < 0) break;
                        Array.Copy(baseline, trial, n);
                        for (int i = bed; i < wake; i++) trial[i] = Slot.Sleep;
                        // Staying up to the new bedtime: their own sleeping hours just before it are free instead.
                        for (int i = Math.Max(0, bed - cancel); i < bed; i++)
                            if (trial[i] == Slot.Sleep) trial[i] = Slot.Anything;
                        float score = Score(trial, baseline, rest, fall, gain, wake, out float end, out float low);
                        if (score > bestScore + 0.001f)
                        {
                            bestScore = score;
                            best = (Slot[])trial.Clone();
                            bestEnd = end;
                            bestLow = low;
                            bestBed = bed;
                        }
                    }
            }
            plan.slots = best;
            plan.restAtDeparture = bestEnd;
            plan.restLowest = bestLow;
            if (bestBed >= 0)
            {
                plan.bed = bestBed;
                plan.wake = wake;
            }
            // Recreation the day before: before the night they sleep, topped up when it would be low by then.
            int night = plan.bed >= 0 ? plan.bed : LastNightStart(best, wake);
            if (night > 0 && joyGain > 0f)
            {
                float atBed = joy - joyFall * night;
                if (atBed < JoyLow)
                {
                    int want = Math.Max(1, Math.Min(MaxJoyHours, (int)Math.Ceiling((JoyTarget - atBed) / joyGain - 1e-4)));
                    int put = 0, from = night;
                    var withJoy = (Slot[])best.Clone();
                    for (int i = night - 1; i >= 0 && put < want; i--)
                    {
                        if (withJoy[i] != Slot.Work && withJoy[i] != Slot.Anything) break;
                        withJoy[i] = Slot.Joy;
                        from = i;
                        put++;
                    }
                    // Only when it costs no rest: someone worn out enough to doze off in free hours keeps their work hours.
                    Simulate(withJoy, rest, fall, gain, wake, out float joyEnd, out _, out int joyLate);
                    Simulate(best, rest, fall, gain, wake, out float noJoyEnd, out _, out int noJoyLate);
                    if (put > 0 && joyLate <= noJoyLate && joyEnd >= noJoyEnd - 0.01f)
                    {
                        plan.slots = withJoy;
                        plan.joyFrom = from;
                        plan.joyHours = put;
                    }
                }
            }
            return plan;
        }

        /// <summary>Where the last run of sleeping hours before <paramref name="before"/> starts, or -1 when there is none.</summary>
        public static int LastNightStart(Slot[] slots, int before)
        {
            int i = Math.Min(before, slots.Length) - 1;
            while (i >= 0 && slots[i] != Slot.Sleep) i--;
            if (i < 0) return -1;
            while (i > 0 && slots[i - 1] == Slot.Sleep) i--;
            return i;
        }

        // ---------------- The last hours ----------------

        /// <summary>The steps of the last hours, in their order.</summary>
        public enum Step
        {
            Dress,
            Pack,
            Meal,
            Toilet,
            Wash,
            Gather
        }

        /// <summary>A meal in the last couple of hours, the bathroom and a wash just before, at the gate a little before the hour.</summary>
        public const float MealHours = 2.5f, WashHours = 1.25f, GatherHours = 0.75f;

        /// <summary>A meal only for someone this far from full.</summary>
        public const float MealBelow = 0.8f;

        /// <summary>How many hours before the departure a step opens (never before the free hours start).</summary>
        public static float Opens(Step s, int freeHours)
        {
            float free = Math.Max(0, freeHours);
            switch (s)
            {
                case Step.Meal: return Math.Min(free, MealHours);
                case Step.Toilet:
                case Step.Wash: return Math.Min(free, WashHours);
                case Step.Gather: return Math.Min(free, GatherHours);
                default: return free;
            }
        }

        // ---------------- What to pack ----------------

        /// <summary>A quarter more food than the days need, and a spare day's worth.</summary>
        public const float FoodMargin = 0.25f, SpareDays = 1f;

        /// <summary>Bedrolls (and tents) for a trip with a night on the road: half a day or more.</summary>
        public const float NightFrom = 0.5f;

        /// <summary>A tent for every two travellers.</summary>
        public const int PerTent = 2;

        /// <summary>Food for a trip: nutrition a day for everyone together, the days, a margin and a spare day.</summary>
        public static float FoodNutrition(float perDay, float days) => perDay <= 0f || days <= 0f ? 0f : perDay * (days * (1f + FoodMargin) + SpareDays);

        /// <summary>How many of something make up an amount (rounded up).</summary>
        public static int Units(float amount, float perUnit) => amount <= 0f || perUnit <= 0f ? 0 : (int)Math.Ceiling(amount / perUnit - 1e-4);

        /// <summary>Medicine for a trip: half a dose each and one more for every eight days on the road, at least one.</summary>
        public static int Medicine(int people, float days) => people <= 0 || days <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(people * (0.5f + days / 8f) - 1e-4));

        /// <summary>Medicine taken: what's wanted, but never more than half of the stores while anyone stays home.</summary>
        public static int MedicineTaken(int wanted, int stock, int stayHome)
        {
            if (wanted <= 0 || stock <= 0) return 0;
            int cap = stayHome > 0 ? (stock + 1) / 2 : stock;
            return Math.Min(wanted, cap);
        }

        /// <summary>A sleeping place each for a trip with nights on the road.</summary>
        public static int Bedrolls(int people, float days) => people <= 0 || days < NightFrom ? 0 : people;

        public static int Tents(int people, float days) => people <= 0 || days < NightFrom ? 0 : (people + PerTent - 1) / PerTent;

        /// <summary>The trip's length in days: the way there, or there and back.</summary>
        public static float TripDays(int oneWayTicks, bool andBack, int ticksPerDay = 60000) =>
            oneWayTicks <= 0 ? 0f : oneWayTicks * (andBack ? 2f : 1f) / ticksPerDay;

        /// <summary>A food in the stores, for packing.</summary>
        public struct FoodOption
        {
            public float nutrition, mass;
            /// <summary>Days before it starts to rot; <see cref="float.PositiveInfinity"/> when it never does.</summary>
            public float rotDays;
            /// <summary>The game's preferability (a meal over raw food), as a number.</summary>
            public int preferability;
            /// <summary>Travel food first: 2 packaged meals, 1 pemmican, 0 the rest.</summary>
            public int travel;
            public int stock;
        }

        /// <summary>Keeps through the trip and its spare days.</summary>
        public static bool Keeps(FoodOption f, float days) => float.IsPositiveInfinity(f.rotDays) || f.rotDays >= days * (1f + FoodMargin) + SpareDays;

        // ---------------- How long it lasts ----------------

        /// <summary>The days the packing is for: the days on the road, a quarter more and a spare day (none without a road).</summary>
        /// <summary>
        /// Doses for an addiction over <paramref name="days"/>: the craving falls by <paramref name="fallPerDay"/> a day and a
        /// dose fills it by <paramref name="perDose"/> (the drug's own amount, one when it says none). At least one.
        /// </summary>
        public static int AddictionDoses(float days, float fallPerDay, float perDose)
        {
            if (days <= 0f || fallPerDay <= 0f) return 0;
            return Math.Max(1, (int)Math.Ceiling(days * fallPerDay / (perDose > 0f ? perDose : 1f) - 1e-4));
        }

        /// <summary>
        /// Doses for a chemical dependency over <paramref name="days"/>: one each time it comes due, every
        /// <paramref name="everyDays"/>, counting the days since the last (no more than one interval). At least one.
        /// </summary>
        public static int DependencyDoses(float days, float daysSince, float everyDays)
        {
            if (days <= 0f) return 0;
            float every = Math.Max(0.5f, everyDays);
            float since = Math.Max(0f, Math.Min(daysSince, every));
            return Math.Max(1, (int)Math.Floor((since + days) / every + 1e-4));
        }

        public static float MarginDays(float roadDays) => roadDays <= 0f ? 0f : roadDays * (1f + FoodMargin) + SpareDays;

        /// <summary>A simple meal's nourishment: the shortfall is told in meals of this much.</summary>
        public const float MealNutrition = 0.9f;

        /// <summary>
        /// How many days food lasts eaters who need <paramref name="perDay"/> a day between them; without eaters, forever
        /// when there is food, none when there isn't.
        /// </summary>
        public static float Lasts(float nutrition, float perDay)
        {
            if (nutrition <= 0f) return 0f;
            return perDay <= 0f ? float.PositiveInfinity : nutrition / perDay;
        }

        /// <summary>How far food reaches: through the road with the margin, through the road only, or short of it.</summary>
        public enum Cover
        {
            Margin,
            Road,
            Short
        }

        /// <summary>Food that lasts <paramref name="lasts"/> days against <paramref name="roadDays"/> on the road (and the margin on top).</summary>
        public static Cover Covers(float lasts, float roadDays)
        {
            if (lasts >= MarginDays(roadDays) - 0.01f) return Cover.Margin;
            return lasts >= roadDays - 0.01f ? Cover.Road : Cover.Short;
        }

        /// <summary>Days short of a target (none when it is reached).</summary>
        public static float ShortDays(float lasts, float targetDays) => float.IsPositiveInfinity(lasts) ? 0f : Math.Max(0f, targetDays - lasts);

        /// <summary>The meals of <see cref="MealNutrition"/> that would make up <paramref name="days"/> for eaters of <paramref name="perDay"/>.</summary>
        public static int ShortMeals(float days, float perDay) => Units(days * perDay, MealNutrition);

        /// <summary>
        /// The animals' feed against the days they can't graze: each animal with what it eats a day and the days it needs
        /// feed for (none for a grazer on good land). <c>lasts</c> is how many days the feed keeps those that need it fed,
        /// <c>need</c> the days they need it, on average by what each eats; <see cref="Covers"/> of the two then reads
        /// against the same margin the planner packs feed with.
        /// </summary>
        public static (float lasts, float need) FeedCover(IList<(float perDay, float feedDays)> animals, float feed)
        {
            float eat = 0f, need = 0f;
            if (animals != null)
                foreach (var a in animals)
                {
                    if (a.perDay <= 0f || a.feedDays <= 0f) continue;
                    eat += a.perDay;
                    need += a.perDay * a.feedDays;
                }
            if (eat <= 0f) return (feed > 0f ? float.PositiveInfinity : 0f, 0f);
            return (Math.Max(0f, feed) / eat, need / eat);
        }

        // ---------------- Changing the list ----------------

        /// <summary>A number typed for a line, kept to what is to hand and never below none.</summary>
        public static int ClampCount(int typed, int toHand) => Math.Max(0, Math.Min(typed, Math.Max(0, toHand)));

        /// <summary>
        /// A line's count moved by a step (the game's modifier keys make it 10 or 100): never up past what is to hand (a
        /// count already over it stays where it is), never below none.
        /// </summary>
        public static int StepCount(int count, int step, int toHand)
        {
            int next = count + step;
            if (step > 0) next = Math.Min(next, Math.Max(Math.Max(0, toHand), count));
            return Math.Max(0, next);
        }

        /// <summary>How much of something each traveller has (medicine a person).</summary>
        public static float PerPerson(int count, int people) => people <= 0 ? 0f : Math.Max(0, count) / (float)people;

        /// <summary>The list's groups in the order they are shown.</summary>
        public static readonly string[] Groups = { "food", "medicine", "drugs", "bedding", "feed", "other" };

        /// <summary>A line's group: food, medicine and feed by their kind, drugs, beds and tents together as bedding, anything else other.</summary>
        public static string GroupOf(string kind)
        {
            switch (kind)
            {
                case "food":
                case "medicine":
                case "feed":
                    return kind;
                case "drug":
                    return "drugs";
                case "bed":
                case "tent":
                    return "bedding";
                default:
                    return "other";
            }
        }

        /// <summary>One line of the list, as its group's subtotal and the load count it.</summary>
        public struct PackRow
        {
            public string kind;
            public int count;
            /// <summary>What the travellers carry of it already (theirs, not loaded again).</summary>
            public int carried;
            /// <summary>Each one's nourishment and weight; how many sleep on one (a bed's width).</summary>
            public float nutrition, mass;
            public int sleeps;
        }

        /// <summary>A group's subtotal: how many things, their nourishment, weight and sleeping places.</summary>
        public struct PackSum
        {
            public int lines, count, sleeps;
            public float nutrition, mass;
        }

        /// <summary>The subtotal of the rows in a group (<see cref="GroupOf"/>); every row when <paramref name="group"/> is null.</summary>
        public static PackSum Sum(IEnumerable<PackRow> rows, string group = null)
        {
            var s = new PackSum();
            foreach (var r in rows)
            {
                if (r.count <= 0 || (group != null && GroupOf(r.kind) != group)) continue;
                s.lines++;
                s.count += r.count;
                s.nutrition += r.count * Math.Max(0f, r.nutrition);
                s.mass += r.count * Math.Max(0f, r.mass);
                if (r.kind == "bed") s.sleeps += r.count * Math.Max(1, r.sleeps);
            }
            return s;
        }

        /// <summary>The weight still to be taken up: what the list holds less what the travellers carry already.</summary>
        public static float LoadToTake(IEnumerable<PackRow> rows)
        {
            float m = 0f;
            foreach (var r in rows) m += Math.Max(0, r.count - Math.Max(0, r.carried)) * Math.Max(0f, r.mass);
            return m;
        }

        /// <summary>
        /// The order to pack food in: what keeps through the trip first, travel food first among it (packaged meals,
        /// pemmican, then the rest), then the better food, then the lighter for its nourishment.
        /// </summary>
        public static List<int> FoodOrder(IList<FoodOption> foods, float days)
        {
            return Enumerable.Range(0, foods.Count).Where(i => foods[i].stock > 0 && foods[i].nutrition > 0f)
                .OrderByDescending(i => Keeps(foods[i], days))
                .ThenByDescending(i => foods[i].travel)
                .ThenByDescending(i => foods[i].preferability)
                .ThenByDescending(i => foods[i].mass > 0f ? foods[i].nutrition / foods[i].mass : 0f)
                .ThenBy(i => i)
                .ToList();
        }

        /// <summary>
        /// What to pack for <paramref name="nutrition"/>: from the best first (see <see cref="FoodOrder"/>), food that
        /// doesn't keep only when nothing else is left. Each entry is the food's index and how many.
        /// </summary>
        public static List<(int food, int units)> PackFood(IList<FoodOption> foods, float days, float nutrition)
        {
            var packed = new List<(int, int)>();
            float left = nutrition;
            foreach (int i in FoodOrder(foods, days))
            {
                if (left <= 1e-4f) break;
                var f = foods[i];
                int units = Math.Min(f.stock, Units(left, f.nutrition));
                if (units <= 0) continue;
                packed.Add((i, units));
                left -= units * f.nutrition;
            }
            return packed;
        }

        // ---------------- Dressed for the road ----------------

        /// <summary>Clothes for the road: what a piece does against cold and heat, and whether it may be taken off.</summary>
        public struct Garment
        {
            public float cold, heat;
            /// <summary>Theirs to take off (not locked, not forced by the player, not armour).</summary>
            public bool removable;
        }

        /// <summary>What a traveller would be comfortable through with these clothes on.</summary>
        public static (float min, float max) Comfy(float bareMin, float bareMax, IList<Garment> items, IEnumerable<int> on)
        {
            float c = 0f, h = 0f;
            foreach (int i in on)
            {
                c += items[i].cold;
                h += items[i].heat;
            }
            return (bareMin - c, bareMax + h);
        }

        /// <summary>
        /// What one traveller changes into for weather from <paramref name="low"/> to <paramref name="high"/>, a piece at
        /// a time while a piece brings them closer to comfortable through it. <paramref name="items"/> holds what they
        /// wear (the first <paramref name="worn"/>) and then what the stores have; <paramref name="clash"/> says whether
        /// two can't be worn together (the one put on replaces the other). Nothing taken by someone else, nothing that
        /// would take off something they keep on. Returns the stores' pieces (indices into items) in the order put on.
        /// </summary>
        public static List<int> Dress(float comfyMin, float comfyMax, IList<Garment> items, int worn, Func<int, int, bool> clash, float low, float high,
            ISet<int> taken = null, int maxPieces = 4)
        {
            var picks = new List<int>();
            var on = Enumerable.Range(0, Math.Min(worn, items.Count)).ToList();
            var now = Comfy(0f, 0f, items, on);
            // Comfortable with nothing on, from what they are now with what they wear.
            float bareMin = comfyMin - now.min, bareMax = comfyMax - now.max;
            for (int step = 0; step < maxPieces; step++)
            {
                var cur = Comfy(bareMin, bareMax, items, on);
                float shortNow = CampWeatherMath.Shortfall(cur.min, cur.max, low, high);
                if (shortNow <= 0.5f) break;
                int best = -1;
                float bestShort = shortNow - 0.5f;
                List<int> bestOn = null;
                for (int c = worn; c < items.Count; c++)
                {
                    if (on.Contains(c) || (taken != null && taken.Contains(c))) continue;
                    var off = on.Where(o => clash(o, c)).ToList();
                    if (off.Any(o => o < worn && !items[o].removable)) continue;
                    var next = on.Where(o => !off.Contains(o)).ToList();
                    next.Add(c);
                    var r = Comfy(bareMin, bareMax, items, next);
                    float s = CampWeatherMath.Shortfall(r.min, r.max, low, high);
                    if (s < bestShort - 0.01f)
                    {
                        best = c;
                        bestShort = s;
                        bestOn = next;
                    }
                }
                if (best < 0) break;
                // A piece put on earlier and replaced now isn't put on after all.
                picks.RemoveAll(p => !bestOn.Contains(p));
                picks.Add(best);
                on = bestOn;
            }
            return picks;
        }

        /// <summary>The weather to dress for: the coldest and hottest hour of the trip, and a few degrees either way.</summary>
        public static (float low, float high) RouteRange(IList<float> temps)
        {
            if (temps == null || temps.Count == 0) return (float.NaN, float.NaN);
            return (temps.Min() - CampWeatherMath.Margin, temps.Max() + CampWeatherMath.Margin);
        }

        /// <summary>
        /// Where on the way a traveller there and back is, as a share of the way there (0 home, 1 the destination), at
        /// <paramref name="t"/> of a trip whose way there takes <paramref name="oneWay"/>.
        /// </summary>
        public static float WayShare(float t, float oneWay, bool andBack)
        {
            if (oneWay <= 0f) return 0f;
            if (t <= oneWay) return Math.Max(0f, t / oneWay);
            if (!andBack) return 1f;
            return Math.Max(0f, 2f - t / oneWay);
        }

        // ---------------- Leaving the map ----------------

        /// <summary>
        /// The map's edges in the order to try leaving by (the game's Rot4: 0 north, 1 east, 2 south, 3 west): the one
        /// the way faces first (<paramref name="dx"/> east, <paramref name="dz"/> north), then the next nearest.
        /// </summary>
        public static int[] EdgeOrder(float dx, float dz)
        {
            var normals = new[] { (0f, 1f), (1f, 0f), (0f, -1f), (-1f, 0f) };
            return Enumerable.Range(0, 4).OrderByDescending(i => normals[i].Item1 * dx + normals[i].Item2 * dz).ThenBy(i => i).ToArray();
        }

        /// <summary>A road out is worth this many cells' longer walk.</summary>
        public const float RoadWorth = 20f;

        /// <summary>The cell of an edge to leave by: the nearest to where they start, a road preferred when not much further. -1: none.</summary>
        public static int BestEdgeCell(IList<(int x, int z, bool road)> cells, int fromX, int fromZ, float roadWorth = RoadWorth)
        {
            int best = -1;
            double bestCost = double.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                double dx = cells[i].x - fromX, dz = cells[i].z - fromZ;
                double cost = Math.Sqrt(dx * dx + dz * dz) - (cells[i].road ? roadWorth : 0f);
                if (cost < bestCost - 1e-9)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The cells on a straight line from one to the other, both ends included.</summary>
        public static List<(int x, int z)> Line(int x0, int z0, int x1, int z1)
        {
            var cells = new List<(int, int)>();
            int dx = Math.Abs(x1 - x0), dz = -Math.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1;
            int err = dx + dz;
            int x = x0, z = z0;
            while (true)
            {
                cells.Add((x, z));
                if (x == x1 && z == z1) break;
                int e2 = 2 * err;
                if (e2 >= dz)
                {
                    err += dz;
                    x += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    z += sz;
                }
            }
            return cells;
        }

        /// <summary>A few steps past the colony's last cell on the way out.</summary>
        public const int GateSteps = 3;

        /// <summary>
        /// The gate: along the way out (cells from home toward the exit, <paramref name="inside"/> when the colony's own,
        /// <paramref name="open"/> when someone can stand there), a few steps past the last of the colony's cells, on
        /// open ground. Without any of the colony's cells on the way, the first open cell. -1: nowhere.
        /// </summary>
        public static int GateIndex(IList<bool> inside, IList<bool> open, int steps = GateSteps)
        {
            int n = Math.Min(inside.Count, open.Count);
            int last = -1;
            for (int i = 0; i < n; i++)
                if (inside[i]) last = i;
            int firstOpen = -1, chosen = -1;
            for (int i = last + 1; i < n; i++)
            {
                if (!open[i] || inside[i]) continue;
                if (firstOpen < 0) firstOpen = i;
                chosen = i;
                if (i - last >= steps) break;
            }
            return last < 0 ? firstOpen : chosen;
        }

        private static int Mod(int a, int b)
        {
            int r = a % b;
            return r < 0 ? r + b : r;
        }
    }
}
