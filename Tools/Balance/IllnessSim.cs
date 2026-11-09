// The shared illness budget (Source/ColonyCouncil/Ailments/IllnessBudgetMath.cs, the player's choice of 8 November 2026,
// made soft on 9 November 2026): serious cases a colony-year on a temperate home, Rimfolk's and the game's own, with the
// budget and without, by play style and colony size, against the game's own rate there; and how often a colonist carries
// two serious illnesses at once. Played by the colony-year simulator itself (Sim.cs: the colony's own room for a serious
// illness, the catching clock with crowding, the serious illnesses of a colonist's day, the game's own diseases on their
// own draws and on the colonists they take, Rates.GameDiseaseKinds), on the same seeds with the budget on and off, so the
// game's own come at the same hours in both; each colony played six years after its warm-up (a debt and its skip can fall
// either side of a year's end). Printed, nothing written:
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
                              + $"{runs * years} colony-years a case ({runs} colonies, {years} years each); held back {ColonyCouncil.IllnessBudgetMath.HoldChance:P0} of the time, "
                              + $"in all at most {ColonyCouncil.IllnessBudgetMath.MostInAll - 1f:P0} above the game's own.");
            Console.WriteLine();
            Console.WriteLine("| Case | The game's own | Without: Rimfolk's | Without: in all | With: Rimfolk's | With: the game's | With: in all | Above the game's own | Rimfolk's share | Held back | Came anyway | Put off | Two at once, without / with | The game's deadly, without / with |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var twos = new List<string>();
            foreach (int n in new[] { 6, 12 })
                foreach (var style in Rates.Styles)
                {
                    var off = Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["illnessBudget"] = "false" }), style, n, runs, null).summary.perYear;
                    var on = Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["illnessBudget"] = "true" }), style, n, runs, null).summary.perYear;
                    double G(Dictionary<string, double> d, string k) => d.GetValueOrDefault(k) / Colony.MeasuredYears;
                    double offR = G(off, "serious illnesses"), offG = G(off, "the game's diseases"), onR = G(on, "serious illnesses"), onG = G(on, "the game's diseases");
                    Console.WriteLine($"| {n} colonists, {style.name} | {offG:0.00} | {offR:0.00} | {offR + offG:0.00} | {onR:0.00} | {onG:0.00} | {onR + onG:0.00} | "
                                      + $"{(onR + onG) / Math.Max(1e-9, offG) - 1.0:+0%;-0%} | {onR / Math.Max(1e-9, onR + onG):P0} | "
                                      + $"{G(on, "the game's diseases held back"):0.00} | {G(on, "the game's diseases came anyway"):0.00} | {G(on, "serious illnesses put off"):0.00} | "
                                      + $"{G(off, "two serious illnesses at once"):0.00} / {G(on, "two serious illnesses at once"):0.00} | "
                                      + $"{G(off, "the game's deadly diseases"):0.00} / {G(on, "the game's deadly diseases"):0.00} |");
                    string Two(Dictionary<string, double> d) => $"{G(d, "two at once: Rimfolk's on the game's"):0.000} | {G(d, "two at once: the game's on Rimfolk's"):0.000} | {G(d, "two at once: the game's own two"):0.000}";
                    twos.Add($"| {n} colonists, {style.name} | {Two(off)} | {Two(on)} |");
                }
            Console.WriteLine();
            Console.WriteLine("Two serious illnesses at once on one colonist, a colony-year, by which came on which:");
            Console.WriteLine();
            Console.WriteLine("| Case | Without: Rimfolk's on the game's | Without: the game's on Rimfolk's | Without: the game's own two | With: Rimfolk's on the game's | With: the game's on Rimfolk's | With: the game's own two |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            foreach (var line in twos) Console.WriteLine(line);
        }
    }
}
