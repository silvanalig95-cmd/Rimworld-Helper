// The gods' happenings a colony-year (the player, 12 November 2026: "important is that there is enough variety that i
// don't have the same events etc. within a short time"; a count "as a check, not a cap"). Each happening is played by the
// mod's own rules (GodPaceMath, GodsOnMapMath, GodWorldMath, WorshipMath, GodHandsMath and GodNearMath, compiled in as
// they are) over colony-years of sixty days, hour by hour where the game looks hourly, with how often its reason comes
// from the table below (each an estimate that says where it comes from). Printed: how many a colony-year, the closest two
// came, and the check each must keep (at most so many a year, never two within so many days). A check that fails is
// named and the run ends with exit code 1. Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --gods [--runs N] [--seed S]

using ColonyCouncil;

namespace Balance
{
    public static class GodsSim
    {
        // ---- How often each happening's reason comes (estimates, a colony of six on a temperate home) ----

        /// <summary>A night after a day with a kindness or a cruelty out of the ordinary (a rescue, a stranger helped, a cruelty to a prisoner), and with a strong one.</summary>
        private const double DeedOrdinaryNight = 0.2, DeedStrongNight = 0.06;
        /// <summary>A night after a day of the colony's good turns or wrongs to the peoples (a gift, a caravan helped, raiders killed), most of them one or two.</summary>
        private const double WorldDealingsNight = 0.15, WorldDealingsStrong = 0.03;
        /// <summary>A change of favour big enough for a weather god to answer (an offering of worth, a prayer at a shrine, a deed in its domain): one in two days.</summary>
        private const double FavourActADay = 0.5;
        /// <summary>A hard moment someone prays in (a sickness, a death, a raid, a drought): once in twenty days a believer, five believers.</summary>
        private const double PrayersADay = 0.25;
        /// <summary>A god's act seen by a scoffer beside a believer of that god (an offering taken, a prayer answered).</summary>
        private const double MockMomentADay = 0.15;
        /// <summary>Rain after five or more dry days, with a couple of two gods together (a temperate land).</summary>
        private const double BickerRainADay = 0.03;
        /// <summary>A night with someone of the colony ill with something a god eases (a flu, a plague, an infection), the colony pleased with its healer.</summary>
        private const double IllNight = 0.1;
        /// <summary>A mark of a god on the land (most colonies have one after their first coming).</summary>
        private const double MarkHeld = 0.8;
        /// <summary>Hours a day someone of the colony mines under thick rock, in a colony that digs into a mountain.</summary>
        private const int DeepHoursADay = 4;
        /// <summary>The game's events whose hand people see (a blight, an eclipse, a trader at the right time, a cold snap): about twenty a colony-year.</summary>
        private const double HandEventsADay = 20.0 / 60.0;

        private const int YearDays = 60;

        private sealed class Count
        {
            public string name, check;
            public int most;
            public int minGap;
            public readonly List<int> perYear = new List<int>();
            public int closest = int.MaxValue;
            public int sameTwice;
            public int last = -99999;
            public int thisYear;
            public string lastForm;

            public void Came(int day, string form = null)
            {
                if (last > -99999) closest = Math.Min(closest, day - last);
                if (form != null && form == lastForm) sameTwice++;
                lastForm = form;
                last = day;
                thisYear++;
            }

            public int DaysSince(int day) => last <= -99999 ? 99999 : day - last;
        }

        public static void Run(string[] args)
        {
            int runs = 400, seed = 23, years = 5;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            var rng = new Random(seed);
            var counts = new List<Count>();
            Count Add(string name, int most, int minGap, string check)
            {
                var c = new Count { name = name, most = most, minGap = minGap, check = check };
                counts.Add(c);
                return c;
            }

            var moon = Add("the moon's return (a deed by night, or the day with the peoples)", 4, GodsOnMapMath.ReturnGapDays, "at most 4 a year, a cycle of the moon apart");
            var deep = Add("the rock rumbling over deep mining (a mountain colony)", 6, GodsOnMapMath.DeepGapDays, "at most 6 a year, days apart");
            var weather = Add("a god answering in the weather", 6, (int)GodsOnMapMath.WeatherGap(true), "at most 6 a year, ten days apart");
            var coming = Add("a god coming (a stranger, a sacred beast, a rising)", 3, GodsOnMapMath.AnyGapDays, "at most 3 a year, forty days apart");
            var myth = Add("a myth told at supper", 5, GodWorldMath.MythGapDays, "at most 5 a year, twelve days apart");
            var answer = Add("a prayer answered (one god)", 5, (int)WorshipMath.AnswerGapDays, "at most 5 a year, ten days apart");
            var mock = Add("a scoffer's mockery", 6, (int)WorshipMath.MockGapDays, "at most 6 a year, ten days apart");
            var bicker = Add("a couple bickering over whose god sent the rain", 4, (int)WorshipMath.BickerGapDays, "at most 4 a year, ten days apart");
            var fever = Add("a fever eased in the night", 6, (int)GodWorldMath.FeverGapDays, "at most 6 a year, five days apart");
            var visit = Add("a mark visited on a quiet day", 5, GodsOnMapMath.VisitGapDays, "at most 5 a year, twelve days apart");
            var supperMark = Add("a mark brought up at supper", 8, GodsOnMapMath.SupperMarkGapDays, "at most 8 a year, six days apart");
            var dream = Add("a god's dream (the priest)", 6, (int)WorshipMath.GodDreamGapDays, "at most 6 a year, ten days apart");
            var hand = Add("a god's hand remarked on (the game's events)", 10, GodHandsMath.AnyQuietDays, "at most 10 a year, days apart");
            var gift = Add("a god's gift to one of the colony", 2, (int)GodWorldMath.GiftGap, "at most 2 a year");
            var kind = Add("a god's own kindness (one god, favoured)", 3, (int)GodWorldMath.BlessingGap, "at most 3 a year, twenty days apart");

            for (int r = 0; r < runs; r++)
            {
                foreach (var c in counts)
                {
                    c.last = -99999;
                    c.lastForm = null;
                }
                int deepWarnings = 0, lastDeep = -99999;
                for (int y = 0; y < years; y++)
                {
                    foreach (var c in counts) c.thisYear = 0;
                    for (int d = 0; d < YearDays; d++)
                    {
                        int day = y * YearDays + d;

                        // The moon: at night, the day's reason, its face, its own pace; it leans to a different form.
                        var t = GodPaceMath.Trigger.None;
                        double deedRoll = rng.NextDouble();
                        if (deedRoll < DeedStrongNight) t = GodPaceMath.Trigger.Strong;
                        else if (deedRoll < DeedStrongNight + DeedOrdinaryNight) t = GodPaceMath.Trigger.Ordinary;
                        double worldRoll = rng.NextDouble();
                        var w = worldRoll < WorldDealingsStrong ? GodPaceMath.Trigger.Strong : worldRoll < WorldDealingsNight ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
                        if (w > t) t = w;
                        if (GodsOnMapMath.Returns(GodsOnMapMath.Face(day), moon.DaysSince(day), t, (float)rng.NextDouble()))
                        {
                            bool kindDeed = rng.NextDouble() < 0.6;
                            var recent = moon.lastForm == null ? null : new[] { moon.lastForm };
                            var form = GodsOnMapMath.ReturnFor(kindDeed ? 5f : -5f, rng.NextDouble() < 0.3, rng.NextDouble() < 0.2, rng.NextDouble() < 0.3, (float)rng.NextDouble(), recent);
                            moon.Came(day, form.ToString());
                        }

                        // The rock over deep mining: hours of mining a day.
                        if (day - lastDeep >= GodsOnMapMath.DeepGapDays)
                            for (int h = 0; h < DeepHoursADay; h++)
                                if (rng.NextDouble() < GodsOnMapMath.DeepNow(day - lastDeep))
                                {
                                    deepWarnings = GodsOnMapMath.Warned(deepWarnings, day - lastDeep);
                                    lastDeep = day;
                                    deep.Came(day);
                                    if (GodsOnMapMath.Wakes(deepWarnings, 99999)) deepWarnings = 0;
                                    break;
                                }

                        // A god answering in the weather: a sky god, after a change of favour, its chance growing back after its gap.
                        if (rng.NextDouble() < FavourActADay && weather.DaysSince(day) >= GodsOnMapMath.WeatherGap(true)
                            && rng.NextDouble() < GodsOnMapMath.VoiceChance(0.5f, 5f, true) * GodPaceMath.Ramp(weather.DaysSince(day), GodsOnMapMath.WeatherGap(true), GodsOnMapMath.WeatherRampDays))
                            weather.Came(day);

                        // A god coming, or rising.
                        var k = GodsOnMapMath.Due(day + 30, coming.DaysSince(day), 99999, 99999, true, true, (float)rng.NextDouble(), (float)rng.NextDouble());
                        if (k != ComingKind.None || GodsOnMapMath.RisingDue(day + 90, coming.DaysSince(day), 99999, true, (float)rng.NextDouble())) coming.Came(day);

                        // A myth at supper: three supper hours.
                        for (int h = 0; h < 3; h++)
                            if (myth.DaysSince(day) >= GodWorldMath.MythGapDays
                                && rng.NextDouble() < GodWorldMath.MythEveningChance / 3f * GodPaceMath.Ramp(myth.DaysSince(day), GodWorldMath.MythGapDays, GodWorldMath.MythRampDays))
                            {
                                myth.Came(day);
                                break;
                            }

                        // A prayer answered.
                        if (rng.NextDouble() < PrayersADay && answer.DaysSince(day) >= WorshipMath.AnswerGapDays
                            && rng.NextDouble() < WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Pleased, rng.NextDouble() < 0.3, rng.NextDouble() < 0.5, false)
                            * GodPaceMath.Ramp(answer.DaysSince(day), WorshipMath.AnswerGapDays, WorshipMath.AnswerRampDays))
                            answer.Came(day);

                        if (rng.NextDouble() < MockMomentADay && mock.DaysSince(day) >= WorshipMath.MockGapDays) mock.Came(day);
                        if (rng.NextDouble() < BickerRainADay && bicker.DaysSince(day) >= WorshipMath.BickerGapDays) bicker.Came(day);

                        // A fever eased: five small hours a night.
                        if (rng.NextDouble() < IllNight && fever.DaysSince(day) >= GodWorldMath.FeverGapDays)
                            for (int h = 0; h < 5; h++)
                                if (rng.NextDouble() < GodWorldMath.BlessingChance(FavourWord.Pleased) / 5f * GodPaceMath.Ramp(fever.DaysSince(day), GodWorldMath.FeverGapDays, GodWorldMath.FeverRampDays))
                                {
                                    fever.Came(day);
                                    break;
                                }

                        // A god's mark: visited on a quiet day, brought up at supper.
                        if (rng.NextDouble() < MarkHeld)
                        {
                            if (visit.DaysSince(day) >= GodsOnMapMath.VisitGapDays
                                && rng.NextDouble() < GodsOnMapMath.VisitChance * GodPaceMath.Ramp(visit.DaysSince(day), GodsOnMapMath.VisitGapDays, GodsOnMapMath.VisitRampDays))
                                visit.Came(day);
                            for (int h = 0; h < 5; h++)
                                if (supperMark.DaysSince(day) >= GodsOnMapMath.SupperMarkGapDays
                                    && rng.NextDouble() < GodsOnMapMath.SupperMarkChance * GodPaceMath.Ramp(supperMark.DaysSince(day), GodsOnMapMath.SupperMarkGapDays, GodsOnMapMath.SupperMarkRampDays))
                                {
                                    supperMark.Came(day);
                                    break;
                                }
                        }

                        // The priest's dreams: a dream on waking about one night in three, a god's share of them, one in ten days at most.
                        if (rng.NextDouble() < 0.3 && rng.NextDouble() < WorshipMath.GodDreamShare(true, true, false, true, false) && dream.DaysSince(day) >= WorshipMath.GodDreamGapDays)
                            dream.Came(day);

                        // A god's hand in the game's events: each its own chance, a god quiet a while after, no two close at a home.
                        if (rng.NextDouble() < HandEventsADay && GodHandsMath.Remarked(hand.last <= -99999 ? -1 : hand.last * 60000, hand.last <= -99999 ? -1 : hand.last * 60000,
                                day * 60000, 0.35f, (float)rng.NextDouble()))
                            hand.Came(day);

                        // The world: a gift, a god's own kindness.
                        if (gift.DaysSince(day) >= GodWorldMath.GiftGap && rng.NextDouble() < GodWorldMath.DayChance(GodWorldMath.GiftDays)) gift.Came(day);
                        if (kind.DaysSince(day) >= GodWorldMath.BlessingGap && rng.NextDouble() < GodWorldMath.DayChance(GodWorldMath.BlessingDays(FavourWord.Favoured))) kind.Came(day);
                    }
                    foreach (var c in counts) c.perYear.Add(c.thisYear);
                }
            }

            Console.WriteLine($"The gods' happenings a colony-year (Tools/Balance/GodsSim.cs): {runs} colonies of six, {years} years each, by the mod's own rules.");
            Console.WriteLine("A check, not a cap: each happening keeps its own pace; this names any that comes too often or too close together.");
            Console.WriteLine();
            Console.WriteLine("| Happening | A colony-year | One year in ten, more than | The closest two came (days) | The same form twice running | The check | |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            var failed = new List<string>();
            foreach (var c in counts)
            {
                double mean = c.perYear.Average();
                var sorted = c.perYear.OrderBy(x => x).ToList();
                int p90 = sorted[(int)(sorted.Count * 0.9)];
                int closest = c.closest == int.MaxValue ? 0 : c.closest;
                bool ok = mean <= c.most && (c.closest == int.MaxValue || c.closest >= c.minGap);
                if (!ok) failed.Add(c.name);
                string same = c.lastForm == null && c.sameTwice == 0 ? "" : c.sameTwice.ToString();
                Console.WriteLine($"| {c.name} | {mean:0.00} | {p90} | {(c.closest == int.MaxValue ? "-" : closest.ToString())} | {same} | {c.check} | {(ok ? "OK" : "TOO OFTEN")} |");
            }
            Console.WriteLine();
            if (failed.Count == 0)
            {
                Console.WriteLine("OK: every happening of the gods keeps its pace.");
                return;
            }
            Console.WriteLine("Too often or too close: " + string.Join("; ", failed) + ".");
            Environment.ExitCode = 1;
        }
    }
}
