// The shared illness budget (Source/ColonyCouncil/Ailments/IllnessBudgetMath.cs, the player's choice of 8 November 2026):
// serious cases a colony-year on a temperate home, Rimfolk's and the game's own, with the budget and without, by play style
// and colony size, against the game's own rate there. Played by the colony-year simulator itself (Sim.cs: the colony's own
// room for a serious illness, the catching clock with crowding, the serious illnesses of a colonist's day, the game's own
// diseases on their own draws), on the same seeds with the budget on and off, each colony played six years after its
// warm-up (a debt and its skip can fall either side of a year's end). Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --illness [repo root] [--runs N] [--years N] [--interval F]

namespace Balance
{
    public static class IllnessSim
    {
        public static void Run(string[] args)
        {
            string root = ".";
            int runs = 80, years = 6;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--illness": break;
                    case "--runs": runs = int.Parse(args[++i]); break;
                    case "--years": years = int.Parse(args[++i]); break;
                    case "--interval": Rates.GameDiseaseInterval = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    default: root = args[i]; break;
                }
            }
            var defs = new DefSet(root);
            var src = new SourceConstants(root);
            Code.Read(src);
            var defaults = src.SettingDefaults();
            Colony.MeasuredYears = Math.Max(1, years);
            float mean = Rates.GameDiseaseMtbDays * Rates.GameDiseaseInterval;
            float gameRate = ColonyCouncil.IllnessBudgetMath.GamePerYear(mean);
            Console.WriteLine($"The shared illness budget: a temperate forest, the game's own human diseases every {mean:0} days ({gameRate:0.00} a year), "
                              + $"{runs * years} colony-years a case ({runs} colonies, {years} years each), Rimfolk's share at most {ColonyCouncil.IllnessBudgetMath.MaxShare(mean):P0}.");
            Console.WriteLine();
            Console.WriteLine("| Case | Without: Rimfolk's | Without: the game's | Without: together | With: Rimfolk's | With: the game's | With: together | Held back | Put off | Rimfolk's share | The game's deadly, without / with |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (int n in new[] { 6, 12 })
                foreach (var style in Rates.Styles)
                {
                    var off = Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["illnessBudget"] = "false" }), style, n, runs, null).summary.perYear;
                    var on = Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["illnessBudget"] = "true" }), style, n, runs, null).summary.perYear;
                    double G(Dictionary<string, double> d, string k) => d.GetValueOrDefault(k) / Colony.MeasuredYears;
                    double offR = G(off, "serious illnesses"), offG = G(off, "the game's diseases"), onR = G(on, "serious illnesses"), onG = G(on, "the game's diseases");
                    Console.WriteLine($"| {n} colonists, {style.name} | {offR:0.00} | {offG:0.00} | {offR + offG:0.00} | {onR:0.00} | {onG:0.00} | {onR + onG:0.00} | "
                                      + $"{G(on, "the game's diseases held back"):0.00} | {G(on, "serious illnesses put off"):0.00} | {onR / Math.Max(1e-9, onR + onG):P0} | "
                                      + $"{G(off, "the game's deadly diseases"):0.00} / {G(on, "the game's deadly diseases"):0.00} |");
                }
        }
    }
}
