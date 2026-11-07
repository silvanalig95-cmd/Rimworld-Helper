// The wild's alphas and bosses (Beasts/, DESIGN.md "Alphas, nemeses, legends and great herds"): what they add to a
// colony's hurts over a colony-year, printed after the everyday hurts with --harm. The rules are the mod's own
// (Beasts/BeastsMath.cs, compiled in as it is); how often a colony meets each (herds crossing, a hunt among them, someone
// out in a stampede's way, a wild animal that maims and gets away) is the table below, each an estimate that says where it
// comes from. The great herds' and stampedes' setting, the beast nemeses' and the alphas' at 100%, the everyday hurts'
// scale at 1.

using ColonyCouncil;

namespace Balance
{
    public static class BeastSim
    {
        // ---- How often a colony meets each (estimates) ----

        /// <summary>The game's own herd migrations a colony-year (the storyteller's small events in a temperate land: two or three).</summary>
        private const double GameHerds = 2.5;
        /// <summary>A herd the colony hunts (the council's hunting season, or its hunters on their own), and the shots that hit before it's spooked or gone.</summary>
        private const double Hunted = 0.5;
        private const int HitsMin = 1, HitsMax = 3;
        /// <summary>A herd spooked by something else (a predator at it, a fire): one in ten.</summary>
        private const double OtherSpook = 0.1;
        /// <summary>Herds of a kind big enough to knock someone down (a muffalo's size or more).</summary>
        private const double BigKind = 0.7;
        /// <summary>
        /// One of the colony out in a stampede's way in its first hour: the herd runs away from the shot across a map of 250
        /// cells, the colony's people mostly at home or at work in its fields; about one in thirty each.
        /// </summary>
        private const double InTheWay = 1.0 / 30.0;
        /// <summary>The shared cap holding a hurt back (one Rimfolk-made hurt in three days).</summary>
        private const double CapAllows = 0.9;
        /// <summary>Wild animals that knock down, maim or kill one of the colony and are still up after (the nemeses' own, a colony-year).</summary>
        private const double BeastNemeses = 0.3;
        /// <summary>Of those, the share hurt badly enough to run (the rest fight on or are killed), and still someone's nemesis when its day comes.</summary>
        private const double Runs = 0.5, StillOpen = 0.8;
        /// <summary>A lone manhunting beast back at the colony wounds someone before it's brought down.</summary>
        private const double BloodWounds = 0.5;

        public static void Run(string[] args)
        {
            int runs = 4000, seed = 7;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]) * 10;
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            var rng = new Random(seed);
            Console.WriteLine("The wild's alphas and bosses: what they add, a colony-year, the settings at 100% (Tools/Balance/BeastSim.cs).");
            foreach (int n in new[] { 6, 12 })
            {
                double stampedes = 0, trampled = 0, returns = 0, blood = 0, crops = 0;
                for (int r = 0; r < runs; r++)
                {
                    int great = 0;
                    for (int s = 0; s < 2; s++)
                        if (rng.NextDouble() < BeastsMath.Chance(1f, BeastsMath.SeasonChance)) great++;
                    int herds = Poisson(rng, GameHerds) + great;
                    for (int h = 0; h < herds; h++)
                    {
                        bool spooked;
                        if (rng.NextDouble() < Hunted)
                        {
                            int hits = rng.Next(HitsMin, HitsMax + 1);
                            spooked = false;
                            for (int k = 0; k < hits && !spooked; k++) spooked = rng.NextDouble() < BeastsMath.SpookChance(BeastsMath.SpookHurt);
                        }
                        else spooked = rng.NextDouble() < OtherSpook * BeastsMath.SpookChance(BeastsMath.SpookHurt);
                        if (!spooked) continue;
                        stampedes++;
                        if (rng.NextDouble() >= BigKind) continue;
                        crops += rng.NextDouble() < 0.3 ? rng.Next(1, BeastsMath.MaxCrops + 1) : 0;
                        int down = 0;
                        for (int c = 0; c < n && down < BeastsMath.MaxTrampled; c++)
                            if (rng.NextDouble() < InTheWay && rng.NextDouble() < BeastsMath.TrampleChance(2.4f, 1f) * CapAllows)
                                down++;
                        trampled += down;
                    }
                    int fled = Poisson(rng, BeastNemeses * (n / 6.0) * Runs);
                    for (int f = 0; f < fled; f++)
                        for (int k = 0; k < BeastsMath.MaxReturns; k++)
                        {
                            if (rng.NextDouble() >= StillOpen) break;
                            returns++;
                            if (BeastsMath.ForBlood((float)rng.NextDouble()) && rng.NextDouble() < BloodWounds) blood++;
                        }
                }
                double cy = (double)runs * n, years = runs;
                Console.WriteLine($"== {n} colonists");
                Console.WriteLine($"   stampedes {stampedes / years:0.00} a colony-year (the game's own herds and the great ones), plants flattened {crops / years:0.0}");
                Console.WriteLine($"   knocked down by a stampede {trampled / cy:0.000} a colonist-year ({trampled / years:0.000} for the colony)");
                Console.WriteLine($"   a beast nemesis back {returns / years:0.00} a colony-year, wounds when it comes for blood {blood / cy:0.000} a colonist-year");
                Console.WriteLine($"   in all {(trampled + blood) / cy:0.000} a colonist-year; beside it a raid's wound 1.4 to 2.0 and Rimfolk's everyday hurts 0.26 to 0.50.");
            }
            Console.WriteLine("   A pack's alpha adds no hurt of its own: its pack's danger is the game's, the alpha a little harder to hurt and harder-hitting");
            Console.WriteLine("   (0.85 and 1.15) on one of three to eight, and the pack scatters when it falls. A legend hurts only those who go to its ground.");
            Console.WriteLine();
        }

        private static int Poisson(Random rng, double mean)
        {
            double l = Math.Exp(-mean), p = 1;
            int k = 0;
            do
            {
                k++;
                p *= rng.NextDouble();
            } while (p > l);
            return k - 1;
        }
    }
}
