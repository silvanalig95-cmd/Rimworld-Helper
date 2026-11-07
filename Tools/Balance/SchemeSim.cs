// Schemes against the colony over a colony-year (Diplomacy/Schemes.cs, DESIGN.md "Schemes against the colony"). Printed,
// nothing written:
//   dotnet run -c Release --project Tools/Balance -- --schemes [--runs N] [--seed S]
//
// A home's year played day by day with the mod's own rules compiled in as they are (Diplomacy/SchemeMath.cs: the day's
// chance, the season's wait, the act picked by what the colony has, who catches it): how many schemes begin, how many are
// acted out while one of theirs is at the colony, how many are caught, and what they cost (machines broken, silver's
// worth taken, fights, goodwill). How often one of theirs comes and who is about at night is the table below, each an
// estimate that says where it comes from. "Before" is 0.132.0, which has no schemes against the colony: every count is
// nothing there, and stays nothing for a colony with no grudge against it and no notable that cold (no roll is made).

using ColonyCouncil;

namespace Balance
{
    public static class SchemeSim
    {
        private const int Days = 60;

        /// <summary>A home's situation over the year (estimates; where each comes from is beside it).</summary>
        private sealed class Exposure
        {
            public string name;
            /// <summary>A people with a grudge or a notable that cold within reach, the whole year.</summary>
            public bool schemer;
            public WorldAge age;
            /// <summary>
            /// Visits of the schemer's people a season: the storyteller's people arriving (visitors and trader caravans) every
            /// four or five days, shared among the three to five peoples at peace with the colony, about one a season each.
            /// </summary>
            public float visits = 1f;
            /// <summary>The colony has a dog (about two colonies in five keep one), and the on-guard decree held or someone drafted at night (rare).</summary>
            public bool dog;
            public float guardNights = 0.02f;
        }

        private static readonly Exposure[] Exposures =
        {
            new Exposure { name = "no grudge, no notable that cold", schemer = false, age = WorldAge.Industrial },
            new Exposure { name = "a people with a grudge, industrial", schemer = true, age = WorldAge.Industrial },
            new Exposure { name = "a people with a grudge, tribal", schemer = true, age = WorldAge.Tribal },
            new Exposure { name = "a notable that cold, medieval", schemer = true, age = WorldAge.Medieval },
            new Exposure { name = "a grudge, a dog and the guard kept, industrial", schemer = true, age = WorldAge.Industrial, dog = true, guardNights = 0.25f },
            new Exposure { name = "a grudge, their people seldom here, spacer", schemer = true, age = WorldAge.Spacer, visits = 0.4f }
        };

        // ---- The same in every home ----

        /// <summary>A visit's stay in days (the game's visitors and caravans stay one to two and a half days).</summary>
        private const float StayMin = 1f, StayMax = 2.5f;
        /// <summary>One of theirs free to act while they are here (a guard of the caravan, a visitor grown up and up and about).</summary>
        private const float AgentFree = 0.9f;
        /// <summary>A night or a day of their stay with no danger on the map and a target nobody awake is near.</summary>
        private const float Quiet = 0.85f;
        /// <summary>A machine that can break down (industrial and after: nearly every colony has a generator or a cooler); a small valuable in the stores.</summary>
        private const float HasMachine = 0.95f, HasLoot = 0.95f;
        /// <summary>A colonist's fighting temper of 2 or more (PersonaMath.FightTemper): about one in seven.</summary>
        private const float HotShare = 0.15f;
        /// <summary>At night, a colonist awake (a night owl, the night shift) and within sight of the place: one in ten awake, three in ten of those near enough.</summary>
        private const float AwakeNear = 0.1f * 0.3f;
        /// <summary>A dog awake at night and near: three in ten awake, three in ten of those near.</summary>
        private const float DogNear = 0.3f * 0.3f;
        /// <summary>By day, another of the colony within earshot of a word in someone's ear.</summary>
        private const float Earshot = 0.15f;
        /// <summary>The shared cap holds one of the two back (a hurt in the last three days, a sickbed, already fighting): words instead.</summary>
        private const float CapHolds = 0.1f;
        /// <summary>Other peoples at peace within reach who hear a smear.</summary>
        private const int OtherPeoples = 3;

        private sealed class Tally
        {
            public int runs;
            public double begun, acted, givenUp, caught, byGuard, byColonist, byDog, machines, taken, takenValue, fights, words, smears, smearGoodwill, caughtGoodwill;
        }

        public static void Run(string[] args)
        {
            int runs = 4000, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            Console.WriteLine($"Schemes against the colony: a home's year ({Days} days), {runs} runs each, the setting at 100% (a day's chance of {SchemeMath.Chance(1f):P0}),");
            Console.WriteLine($"one a season at most (the rule's own rate: {SchemeMath.YearRate(SchemeMath.Chance(1f)):0.00} a year with a schemer in reach the whole year).");
            Console.WriteLine("Before (0.132.0): none of it. A machine of the game's own breaks down at a mean of 228 days: ten machines, 2.6 a year.");
            Console.WriteLine();
            Console.WriteLine("| Home | Colonists | Begun | Acted | Given up | Caught | Machines | Taken (silver) | Fights | Hard words | Smears | Goodwill lost | Hurts/colonist |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var x in Exposures)
                foreach (int n in new[] { 6, 12 })
                {
                    var t = Play(x, n, runs, seed);
                    double R(double v) => v / t.runs;
                    double hurts = R(t.fights) * 2 / n;
                    Console.WriteLine($"| {x.name} | {n} | {R(t.begun):0.00} | {R(t.acted):0.00} | {R(t.givenUp):0.00} | {R(t.caught):0.00} | {R(t.machines):0.00} | {R(t.taken):0.00} ({R(t.takenValue):0}) | "
                                      + $"{R(t.fights):0.000} | {R(t.words):0.000} | {R(t.smears):0.00} | {R(t.smearGoodwill + t.caughtGoodwill):0.0} | {hurts:0.000} |");
                    if (n == 6 && t.caught > 0)
                        Console.WriteLine($"|  caught by: a guard {t.byGuard / t.caught:P0}, someone awake {t.byColonist / t.caught:P0}, a dog {t.byDog / t.caught:P0}; acted out caught {t.caught / Math.Max(1, t.acted):P0} | | | | | | | | | | | | |");
                }
            Console.WriteLine();
            Console.WriteLine("Hurts are a fight's two the shared cap counts, per colonist-year; beside them, harm parts 1 to 3 together are 0.26 to 0.50 a colonist-year");
            Console.WriteLine("and a raid's wounds 1.4 to 2.0 (Tools/Balance --harm). No mood is added: none of it changes the colony-year report.");
        }

        private static Tally Play(Exposure x, int n, int runs, int seed)
        {
            var t = new Tally();
            for (int r = 0; r < runs; r++)
            {
                var rng = new Random(seed * 7919 + r * 31 + n);
                t.runs++;
                if (!x.schemer) continue;
                // The year's visits of their people: arrivals and the days each stay covers.
                var here = new bool[Days + 3];
                for (int d = 0; d < Days; d++)
                {
                    if (rng.NextDouble() >= x.visits / SchemeMath.SeasonDays) continue;
                    double stay = StayMin + (StayMax - StayMin) * rng.NextDouble();
                    for (int k = 0; k < Math.Ceiling(stay) && d + k < here.Length; k++) here[d + k] = true;
                }
                bool hot = rng.NextDouble() < 1 - Math.Pow(1 - HotShare, n);
                bool machine = SchemeMath.MachineAge(x.age) && rng.NextDouble() < HasMachine;
                bool loot = rng.NextDouble() < HasLoot;
                int last = -1, begunAt = -1;
                SchemeAct act = SchemeAct.Smear;
                bool live = false;
                for (int d = 0; d < Days; d++)
                {
                    int now = d * SchemeMath.DayTicks;
                    // The day's roll (diplomacy's day), a season after the last began.
                    if (!live && SchemeMath.SeasonPassed(last, now) && rng.NextDouble() < SchemeMath.Chance(1f))
                    {
                        live = true;
                        last = begunAt = now;
                        act = SchemeMath.Pick(machine, loot, hot, 1f, (float)rng.NextDouble());
                        t.begun++;
                    }
                    if (!live) continue;
                    if (now >= SchemeMath.Until(begunAt))
                    {
                        live = false;
                        t.givenUp++;
                        continue;
                    }
                    // Acted out on a day (or the night after it) one of theirs is here, free, and the place quiet.
                    if (!here[d] || rng.NextDouble() >= AgentFree * Quiet) continue;
                    live = false;
                    t.acted++;
                    var w = Watch(x, n, act, rng);
                    if (w != SchemeWatch.None)
                    {
                        t.caught++;
                        if (w == SchemeWatch.Guard) t.byGuard++;
                        else if (w == SchemeWatch.Colonist) t.byColonist++;
                        else t.byDog++;
                        t.caughtGoodwill += SchemeMath.CaughtGoodwill;
                        continue;
                    }
                    switch (act)
                    {
                        case SchemeAct.Machine:
                            t.machines++;
                            break;
                        case SchemeAct.Theft:
                            t.taken++;
                            t.takenValue += SchemeMath.LootMin + (SchemeMath.LootMax - SchemeMath.LootMin) * rng.NextDouble();
                            break;
                        case SchemeAct.Fight:
                            if (rng.NextDouble() < CapHolds) t.words++;
                            else t.fights++;
                            break;
                        default:
                            t.smears++;
                            t.smearGoodwill += SchemeMath.SmearGoodwill * OtherPeoples;
                            break;
                    }
                }
            }
            return t;
        }

        /// <summary>Who is about when they finish: at night someone awake near, a guard on duty, a dog; by day whoever is in earshot of the word.</summary>
        private static SchemeWatch Watch(Exposure x, int n, SchemeAct act, Random rng)
        {
            int colonists = 0, guards = 0, dogs = 0;
            if (act == SchemeAct.Fight)
            {
                for (int i = 0; i < n - 2; i++)
                    if (rng.NextDouble() < Earshot)
                        colonists++;
                return SchemeMath.Catcher(colonists, 0, 0, (float)rng.NextDouble(), SchemeMath.EarNotice);
            }
            for (int i = 0; i < n; i++)
                if (rng.NextDouble() < AwakeNear)
                    colonists++;
            if (rng.NextDouble() < x.guardNights) guards = 1;
            if (x.dog && rng.NextDouble() < DogNear) dogs = 1;
            return SchemeMath.Catcher(colonists, guards, dogs, (float)rng.NextDouble());
        }
    }
}
