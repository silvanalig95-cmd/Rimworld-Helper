// The ways to play a colony-year (the player's request, 12 November 2026; Defs/StorytellerDefs/Rimfolk_Storytellers.xml,
// Source/ColonyCouncil/Story/). Each of the eight storytellers is played from its own def, comp by comp, by the game's
// own rules as the decompiled game has them (an on-off cycle's hits spread over its days on and thinned by its share,
// a faction interaction's visits a year, a category's mean days between, the biome's sickness), with the ways' own shapes
// from StoryWaysMath compiled in as they are, beside the classic storyteller (Cassandra, from the game's own def) for
// comparison. The colony's threat points follow the game's own wealth-by-time curve and points curves for six colonists,
// the same for every storyteller, so the points they send compare. Printed: a colony-year's big threats and the points
// they bring, small threats, the ways' own picks (strange threats, hardships, the season's weather), the home's other
// happenings, sickness, traders, visitors and quests, in the first year and the two after; then each way's levers on
// Rimfolk's own happenings over a year or an arc. Caravans on the road and friends sent in a fight are left out: they
// depend on where the colony goes and when it is attacked. A way whose big threats over the years come above the
// classic pace by more than a twentieth fails the run (exit code 1). Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --storytellers [repo root] [--runs N] [--seed S]

using System.Globalization;
using System.Xml.Linq;
using ColonyCouncil;

namespace Balance
{
    public static class StorytellerSim
    {
        private const int YearDays = 60;
        private const int Years = 3;
        private const int Colonists = 6;
        /// <summary>The biome's days between human sicknesses (a temperate forest's), at the usual difficulty.</summary>
        private const float DiseaseMtb = 60f;

        // The classic storyteller's parts that send what the colony meets at home (the game's Storytellers.xml, Cassandra).
        private const string Classic = @"<comps>
  <li Class='StorytellerCompProperties_OnOffCycle'><category>ThreatBig</category><minDaysPassed>11.0</minDaysPassed><onDays>4.6</onDays><offDays>6.0</offDays><minSpacingDays>1.9</minSpacingDays><numIncidentsRange>1~2</numIncidentsRange></li>
  <li Class='StorytellerCompProperties_OnOffCycle'><category>ThreatSmall</category><minDaysPassed>11.0</minDaysPassed><onDays>4.6</onDays><offDays>6.0</offDays><numIncidentsRange>0.2~1</numIncidentsRange>
    <acceptPercentFactorPerThreatPointsCurve><points><li>(800, 1)</li><li>(2800, 0)</li></points></acceptPercentFactorPerThreatPointsCurve></li>
  <li Class='StorytellerCompProperties_CategoryMTB'><category>Misc</category><allowedTargetTags><li>Map_PlayerHome</li></allowedTargetTags><minDaysPassed>5</minDaysPassed><mtbDays>4.8</mtbDays></li>
  <li Class='StorytellerCompProperties_Disease'><category>DiseaseHuman</category><minDaysPassed>9</minDaysPassed></li>
  <li Class='StorytellerCompProperties_FactionInteraction'><incident>TraderCaravanArrival</incident><minDaysPassed>5</minDaysPassed><baseIncidentsPerYear>5</baseIncidentsPerYear><minSpacingDays>6</minSpacingDays></li>
  <li Class='StorytellerCompProperties_FactionInteraction'><incident>VisitorGroup</incident><minDaysPassed>3</minDaysPassed><baseIncidentsPerYear>4</baseIncidentsPerYear><minSpacingDays>5</minSpacingDays></li>
  <li Class='StorytellerCompProperties_FactionInteraction'><incident>TravelerGroup</incident><minDaysPassed>1</minDaysPassed><baseIncidentsPerYear>6</baseIncidentsPerYear><minSpacingDays>1</minSpacingDays></li>
  <li Class='StorytellerCompProperties_RandomQuest'><category>GiveQuest</category><onDays>12</onDays><numIncidentsRange>2</numIncidentsRange><enableIfAnyModActive><li>Ludeon.RimWorld.Royalty</li></enableIfAnyModActive></li>
  <li Class='StorytellerCompProperties_OnOffCycle'><incident>OrbitalTraderArrival</incident><onDays>7</onDays><offDays>8</offDays><numIncidentsRange>1</numIncidentsRange></li>
</comps>";

        // The game's own categories of the incidents the ways name by hand (from the game's defs).
        private static readonly Dictionary<string, string> Category = new()
        {
            ["TraderCaravanArrival"] = "trader", ["VisitorGroup"] = "visitor", ["TravelerGroup"] = "traveller", ["RaidFriendly"] = "friends",
            ["OrbitalTraderArrival"] = "orbital", ["AnimalInsanityMass"] = "ThreatBig", ["Infestation"] = "ThreatBig", ["SightstealerSwarm"] = "ThreatBig",
            ["UnnaturalDarkness"] = "ThreatBig", ["DeathPall"] = "ThreatBig", ["BloodRain"] = "ThreatBig", ["HateChanters"] = "ThreatBig", ["VREA_PsychicStorm"] = "ThreatBig"
        };

        private sealed class Tally
        {
            public double big, points, small, picks, misc, sick, traders, visitors, travellers, quests, orbital, other;

            public void Add(Tally t, double f)
            {
                big += t.big * f; points += t.points * f; small += t.small * f; picks += t.picks * f; misc += t.misc * f; sick += t.sick * f;
                traders += t.traders * f; visitors += t.visitors * f; travellers += t.travellers * f; quests += t.quests * f; orbital += t.orbital * f; other += t.other * f;
            }
        }

        public static int Run(string[] args)
        {
            string root = args.FirstOrDefault(a => !a.StartsWith("--") && Directory.Exists(Path.Combine(a, "Defs"))) ?? ".";
            int runs = 400, seed = 1;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[i + 1]);
                if (args[i] == "--seed") seed = int.Parse(args[i + 1]);
            }
            var doc = XDocument.Load(Path.Combine(root, "Defs", "StorytellerDefs", "Rimfolk_Storytellers.xml"));
            var tellers = new List<(string name, List<XElement> comps, StoryWayExtensionLite ext, float deeds)>
            {
                ("The classic (Cassandra)", XElement.Parse(Classic).Elements("li").ToList(), null, 0f)
            };
            foreach (var d in doc.Root.Elements("StorytellerDef"))
            {
                var ext = StoryWayExtensionLite.Read(d);
                var comps = d.Element("comps").Elements("li").ToList();
                string label = d.Element("label").Value;
                if (ext.shape == StoryShape.Deeds)
                {
                    tellers.Add((label + ", a kind colony", comps, ext, 0.8f));
                    tellers.Add((label + ", of no name", comps, ext, 0f));
                    tellers.Add((label + ", a cruel colony", comps, ext, -0.8f));
                }
                else tellers.Add((label, comps, ext, 0f));
            }

            Console.WriteLine($"The ways to play a colony-year: {runs} runs of {Years} years ({YearDays} days each), six colonists, a temperate home, the usual difficulty.");
            Console.WriteLine("Threat points follow the game's own wealth by time and points curves, the same for all, so the points sent compare.\n");
            Console.WriteLine("| Storyteller | Year | Big threats | Their points | Small threats | Way's own picks | Other home happenings | Sickness | Traders | Visitors | Travellers | Orbital | Quests |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            int failed = 0;
            double classicBig = -1;
            foreach (var (name, comps, ext, deeds) in tellers)
            {
                var first = new Tally();
                var later = new Tally();
                var rng = new Random(seed);
                for (int r = 0; r < runs; r++)
                {
                    var years = Play(comps, deeds, rng);
                    first.Add(years[0], 1.0 / runs);
                    for (int y = 1; y < Years; y++) later.Add(years[y], 1.0 / runs / (Years - 1));
                }
                Row(name, "1st", first);
                Row(name, "2nd, 3rd", later);
                double allBig = (first.big + later.big * (Years - 1)) / Years;
                if (classicBig < 0) classicBig = allBig;
                else if (allBig > classicBig * 1.05)
                {
                    Console.WriteLine($"FAIL {name}: {allBig:0.0} big threats a year against the classic {classicBig:0.0}");
                    failed++;
                }
            }

            Console.WriteLine("\nRimfolk's levers over a year (the seasons) or an arc (Rise and Fall), 1 as it is; the floor is " + StoryWaysMath.Floor + ".\n");
            Console.WriteLine("| Way | " + string.Join(" | ", StoryWaysMath.Keys) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", StoryWaysMath.Keys.Length)));
            foreach (var d in doc.Root.Elements("StorytellerDef"))
            {
                var ext = StoryWayExtensionLite.Read(d);
                foreach (float deeds in ext.shape == StoryShape.Deeds ? new[] { 0.8f, 0f, -0.8f } : new[] { 0f })
                {
                    string label = d.Element("label").Value + (ext.shape != StoryShape.Deeds ? "" : deeds > 0 ? ", kind" : deeds < 0 ? ", cruel" : ", no name");
                    var cells = StoryWaysMath.Keys.Select(k => Mean(ext, k, deeds).ToString("0.00", CultureInfo.InvariantCulture));
                    Console.WriteLine("| " + label + " | " + string.Join(" | ", cells) + " |");
                }
            }
            Console.WriteLine(failed == 0 ? "\nEvery way's big threats over the years are at the classic pace or less." : $"\n{failed} way(s) above the classic pace.");
            return failed == 0 ? 0 : 1;
        }

        /// <summary>A lever's mean over a year's seasons and an arc's phases, each phase by its days.</summary>
        private static float Mean(StoryWayExtensionLite ext, string key, float deeds)
        {
            float sum = 0f, weight = 0f;
            for (int q = 0; q < 4; q++)
            foreach (ArcPhase p in Enum.GetValues(typeof(ArcPhase)))
            {
                float w = StoryWaysMath.PhaseDays(p);
                sum += w * StoryWaysMath.Combine(key, ext.levers.Get(key), ext.shape, q, deeds, p);
                weight += w;
            }
            return sum / weight;
        }

        private static void Row(string name, string year, Tally t) =>
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2:0.0} | {3:0} | {4:0.0} | {5:0.0} | {6:0.0} | {7:0.0} | {8:0.0} | {9:0.0} | {10:0.0} | {11:0.0} | {12:0.0} |",
                name, year, t.big, t.points, t.small, t.picks, t.misc, t.sick, t.traders, t.visitors, t.travellers, t.orbital, t.quests));

        // ================= One colony's years =================

        private static Tally[] Play(List<XElement> comps, float deeds, Random rng)
        {
            var years = Enumerable.Range(0, Years).Select(_ => new Tally()).ToArray();
            int total = YearDays * Years;
            int seed = rng.Next();
            void At(double day, Action<Tally> a)
            {
                int y = (int)(day / YearDays);
                if (y >= 0 && y < Years) a(years[y]);
            }
            foreach (var c in comps)
            {
                string cls = (c.Attribute("Class")?.Value ?? "").Replace("ColonyCouncil.", "");
                if (Targets(c).Any(t => t != "Map_PlayerHome" && t != "World")) continue;
                if (c.Element("enableIfAnyModActive") != null && !c.Element("enableIfAnyModActive").Elements("li").Any(li => li.Value == "Ludeon.RimWorld.Royalty")) continue;
                if (c.Element("disableIfAnyModActive") != null) continue;
                float minDays = F(c, "minDaysPassed");
                string cat = c.Element("category")?.Value, inc = c.Element("incident")?.Value;
                string what = inc != null && Category.TryGetValue(inc, out var k) ? k : cat;
                StoryShape shape = Enum.TryParse(c.Element("shape")?.Value ?? "None", out StoryShape s) ? s : StoryShape.None;
                switch (cls)
                {
                    case "StorytellerCompProperties_OnOffCycle":
                    case "StorytellerCompProperties_WayCycle":
                    {
                        float on = F(c, "onDays"), off = F(c, "offDays");
                        var range = Range(c.Element("numIncidentsRange")?.Value ?? "1");
                        var curve = Curve(c.Element("acceptPercentFactorPerThreatPointsCurve"));
                        for (double start = minDays; start < total; start += on + off)
                        {
                            int n = RoundRandom(range.min + rng.NextDouble() * (range.max - range.min), rng);
                            for (int i = 0; i < n; i++)
                            {
                                double day = start + rng.NextDouble() * on;
                                float accept = Math.Max(StoryWaysMath.Floor, Accept(shape, day, deeds, seed));
                                if (cls.EndsWith("_OnOffCycle")) accept = 1f;
                                if (curve != null) accept *= Eval(curve, Points(day));
                                if (rng.NextDouble() >= accept) continue;
                                At(day, t => Count(t, what, day));
                            }
                        }
                        break;
                    }
                    case "StorytellerCompProperties_FactionInteraction":
                    case "StorytellerCompProperties_WayVisits":
                    {
                        if (what == "friends") break;
                        float perYear = F(c, "baseIncidentsPerYear");
                        string lever = c.Element("lever")?.Value ?? StoryWaysMath.Visitors;
                        for (double start = minDays; start < total; start += YearDays)
                        {
                            int n = RoundRandom(perYear, rng);
                            for (int i = 0; i < n; i++)
                            {
                                double day = start + rng.NextDouble() * YearDays;
                                float accept = cls.EndsWith("_WayVisits")
                                    ? Math.Max(StoryWaysMath.Floor, StoryWaysMath.VisitAccept(shape, lever, Quarter(day), deeds, StoryWaysMath.PhaseAt((float)day)))
                                    : 1f;
                                if (rng.NextDouble() < accept) At(day, t => Count(t, what, day));
                            }
                        }
                        break;
                    }
                    case "StorytellerCompProperties_CategoryMTB":
                    {
                        float mtb = F(c, "mtbDays");
                        if (Targets(c).Contains("World")) break;
                        for (int day = (int)Math.Ceiling(minDays); day < total; day++)
                            if (rng.NextDouble() < 1 - Math.Exp(-1.0 / mtb)) At(day, t => Count(t, cat == "Misc" ? "misc" : "other", day));
                        break;
                    }
                    case "StorytellerCompProperties_Disease":
                    case "StorytellerCompProperties_WayDisease":
                    {
                        if (cat != "DiseaseHuman") break;
                        for (int day = (int)Math.Ceiling(minDays); day < total; day++)
                        {
                            float f = cls.EndsWith("_WayDisease")
                                ? Math.Max(StoryWaysMath.Floor, StoryWaysMath.DiseaseFactor(shape, Quarter(day), StoryWaysMath.PhaseAt(day), StoryWaysMath.TroubleOf(StoryWaysMath.ArcCycle(day), seed)))
                                : 1f;
                            if (rng.NextDouble() < 1 - Math.Exp(-f / DiseaseMtb)) At(day, t => t.sick++);
                        }
                        break;
                    }
                    case "StorytellerCompProperties_WayPicks":
                    {
                        float mtb = F(c, "mtbDays");
                        var picks = c.Element("picks").Elements("li").ToList();
                        var last = new Dictionary<string, double>();
                        for (int day = (int)Math.Ceiling(minDays); day < total; day++)
                        {
                            if (rng.NextDouble() >= 1 - Math.Exp(-1.0 / mtb)) continue;
                            var phase = StoryWaysMath.PhaseAt(day);
                            var trouble = StoryWaysMath.TroubleOf(StoryWaysMath.ArcCycle(day), seed);
                            var fit = picks.Where(p => Fits(p, Quarter(day), phase, trouble)).ToList();
                            if (fit.Count == 0) continue;
                            var w = fit.Select(p => F(p, "weight") * StoryWaysMath.PickRecency(last.TryGetValue(p.Element("incident").Value, out var l) ? (float)(day - l) : float.NaN)).ToList();
                            double x = rng.NextDouble() * w.Sum();
                            int pick = 0;
                            while (pick < w.Count - 1 && x >= w[pick]) x -= w[pick++];
                            string name = fit[pick].Element("incident").Value;
                            last[name] = day;
                            At(day, t => Count(t, Category.TryGetValue(name, out var pc) && pc == "ThreatBig" ? "ThreatBig" : "pick", day));
                        }
                        break;
                    }
                    case "StorytellerCompProperties_RandomQuest":
                    {
                        float on = F(c, "onDays");
                        var range = Range(c.Element("numIncidentsRange")?.Value ?? "1");
                        for (double start = 0; start < total; start += on)
                        {
                            int n = RoundRandom(range.min + rng.NextDouble() * (range.max - range.min), rng);
                            for (int i = 0; i < n; i++)
                            {
                                double day = start + rng.NextDouble() * on;
                                if (day >= 15 || rng.NextDouble() < Math.Max(0, (day - 8) / 7)) At(day, t => t.quests++);
                            }
                        }
                        break;
                    }
                }
            }
            return years;
        }

        private static void Count(Tally t, string what, double day)
        {
            switch (what)
            {
                case "ThreatBig": t.big++; t.points += Points(day); break;
                case "ThreatSmall": t.small++; break;
                case "pick": t.picks++; break;
                case "misc": t.misc++; break;
                case "trader": t.traders++; break;
                case "visitor": t.visitors++; break;
                case "traveller": t.travellers++; break;
                case "orbital": t.orbital++; break;
                default: t.other++; break;
            }
        }

        private static float Accept(StoryShape shape, double day, float deeds, int seed) =>
            StoryWaysMath.ThreatAccept(shape, Quarter(day), deeds, StoryWaysMath.PhaseAt((float)day), StoryWaysMath.TroubleOf(StoryWaysMath.ArcCycle((float)day), seed));

        /// <summary>The quarter of the year on a temperate home: each fifteen days a season, from spring.</summary>
        private static int Quarter(double day) => (int)(day / 15) % 4;

        // ---- The game's threat points for six colonists (StorytellerUtility.DefaultThreatPointsNow, the BaseStoryteller's days curve) ----

        private static readonly (float x, float y)[] WealthByDays = { (0, 10000), (180, 180000), (720, 1000000) };
        private static readonly (float x, float y)[] PointsPerWealth = { (0, 0), (14000, 0), (400000, 2400), (700000, 3600), (1000000, 4200) };
        private static readonly (float x, float y)[] PointsPerColonist = { (0, 15), (10000, 15), (400000, 140), (1000000, 200) };
        private static readonly (float x, float y)[] DaysFactor = { (10, 0.7f), (40, 1f) };

        private static float Points(double day)
        {
            float wealth = Eval(WealthByDays, (float)day);
            return (Eval(PointsPerWealth, wealth) + Colonists * Eval(PointsPerColonist, wealth)) * Eval(DaysFactor, (float)day);
        }

        private static float Eval((float x, float y)[] c, float x)
        {
            if (x <= c[0].x) return c[0].y;
            for (int i = 1; i < c.Length; i++)
                if (x <= c[i].x)
                    return c[i - 1].y + (c[i].y - c[i - 1].y) * (x - c[i - 1].x) / (c[i].x - c[i - 1].x);
            return c[^1].y;
        }

        private static (float x, float y)[] Curve(XElement e) =>
            e?.Element("points")?.Elements("li").Select(li =>
            {
                var p = li.Value.Trim('(', ')', ' ').Split(',');
                return (float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture));
            }).ToArray();

        private static IEnumerable<string> Targets(XElement c) => c.Element("allowedTargetTags")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>();

        private static float F(XElement e, string name) => float.Parse(e.Element(name)?.Value ?? "0", CultureInfo.InvariantCulture);

        private static (float min, float max) Range(string v)
        {
            var p = v.Split('~').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            return (p[0], p.Length > 1 ? p[1] : p[0]);
        }

        private static int RoundRandom(double v, Random rng) => (int)Math.Floor(v) + (rng.NextDouble() < v - Math.Floor(v) ? 1 : 0);

        private static bool Fits(XElement p, int quarter, ArcPhase phase, StoryTrouble trouble)
        {
            string[] names = { "Spring", "Summer", "Fall", "Winter" };
            var seasons = p.Element("seasons")?.Elements("li").Select(x => x.Value).ToList();
            var phases = p.Element("phases")?.Elements("li").Select(x => x.Value).ToList();
            var troubles = p.Element("troubles")?.Elements("li").Select(x => x.Value).ToList();
            return (seasons == null || seasons.Contains(names[quarter])) && (phases == null || phases.Contains(phase.ToString()))
                   && (troubles == null || troubles.Contains(trouble.ToString()));
        }

        /// <summary>A way's levers and shape, read from its def's StoryWayExtension.</summary>
        private sealed class StoryWayExtensionLite
        {
            public StoryShape shape;
            public StoryLevers levers = new StoryLevers();

            public static StoryWayExtensionLite Read(XElement def)
            {
                var r = new StoryWayExtensionLite();
                var e = def.Element("modExtensions")?.Elements("li").FirstOrDefault(x => x.Attribute("Class")?.Value == "ColonyCouncil.StoryWayExtension");
                if (e == null) return r;
                r.shape = Enum.TryParse(e.Element("shape")?.Value ?? "None", out StoryShape s) ? s : StoryShape.None;
                foreach (var l in e.Element("levers")?.Elements() ?? Enumerable.Empty<XElement>())
                    typeof(StoryLevers).GetField(l.Name.LocalName)?.SetValue(r.levers, float.Parse(l.Value, CultureInfo.InvariantCulture));
                return r;
            }
        }
    }
}
