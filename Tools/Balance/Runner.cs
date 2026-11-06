namespace Balance
{
    /// <summary>What a set of runs comes to, per colonist and per day (and the JSON a later report compares against).</summary>
    public sealed class Summary
    {
        public double net { get; set; }
        public double good { get; set; }
        public double goodRaw { get; set; }
        public double bad { get; set; }
        public double steadyGood { get; set; }
        public double steadyBad { get; set; }
        public double mood { get; set; }
        public double pinned { get; set; }
        /// <summary>A low and a high moment of one colonist's whole mood (P10, P90), the game's moods included.</summary>
        public double moodP10 { get; set; }
        public double moodP90 { get; set; }
        public double netP10 { get; set; }
        public double netP90 { get; set; }
        public double goodP90 { get; set; }
        public double goodP99 { get; set; }
        /// <summary>Share of the time Rimfolk's good moods (memories and steady) add up to 12 or more.</summary>
        public double goodHigh { get; set; }
        public double goodCount { get; set; }
        public double trustStart { get; set; }
        public double trustEnd { get; set; }
        public double trustMean { get; set; }
        public double trustingShare { get; set; }
        public double doubtfulShare { get; set; }
        public double standards { get; set; }
        public double decisionsPerDay { get; set; }
        public double requestsPerDay { get; set; }
        public double lettersPerDay { get; set; }
        public double messagesPerDay { get; set; }
        public double digestPerDay { get; set; }
        public double clustered { get; set; }
        public double twoDecisionDays { get; set; }
        public double attentionPerDay { get; set; }
        public double attentionClustered { get; set; }
        public double ailmentDays { get; set; }
        public double illShare { get; set; }
        public double illNotQuietShare { get; set; }
        public Dictionary<string, double[]> systems { get; set; } = new Dictionary<string, double[]>();
        public Dictionary<string, double> perYear { get; set; } = new Dictionary<string, double>();
        /// <summary>What each source adds to a colonist's mood on average (good memories after the budget; "(bad)" for bad ones).</summary>
        public Dictionary<string, double> parts { get; set; } = new Dictionary<string, double>();
        /// <summary>Days per colonist per year with each ailment.</summary>
        public Dictionary<string, double> ailmentsBy { get; set; } = new Dictionary<string, double>();
        /// <summary>The heart (HeartSim.cs): time strained, worn and near breaking; episodes (the day off) a colonist-year.</summary>
        public double strained { get; set; }
        public double worn { get; set; }
        public double breaking { get; set; }
        public double episodesPerYear { get; set; }
        /// <summary>Strain built and eased a colonist-year, by what (life, Rimfolk's letdowns and joys, the small moods' pile by who made them).</summary>
        public Dictionary<string, double> strainBy { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> reliefBy { get; set; } = new Dictionary<string, double>();
        /// <summary>The heart's good side: time steady, in good heart and in high spirits; inspirations and kind words a colonist-year.</summary>
        public double steady { get; set; }
        public double goodHeart { get; set; }
        public double highSpirits { get; set; }
        /// <summary>Share of the time with strain at nothing.</summary>
        public double noStrain { get; set; }
        /// <summary>Share of the time with the good side in each tenth of its range.</summary>
        public double[] goodTenths { get; set; }
        public double inspirationsPerYear { get; set; }
        public double kindWordsPerYear { get; set; }
        /// <summary>What filled the good side and what spent it, a colonist-year, by what.</summary>
        public Dictionary<string, double> goodBy { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> goodSpentBy { get; set; } = new Dictionary<string, double>();
        /// <summary>Share of the time a colonist's whole mood is under the game's usual minor-break line.</summary>
        public double underMinor { get; set; }
        /// <summary>What good things growing ordinary takes off the game's own good moods (the colony's mood, not Rimfolk's).</summary>
        public double gameOrdinary { get; set; }
        public double[] netSeries { get; set; }
        public double[] goodSeries { get; set; }
        public double[] badSeries { get; set; }
        public double[] trustSeries { get; set; }
    }

    public sealed class Result
    {
        public string preset;
        public Rates.Style style;
        public int colonists;
        public Summary summary;
        public string Key => (preset ?? "default") + "|" + style.key + "|" + colonists;
    }

    public static class Runner
    {
        public const int Bins = 6;

        public static Result Run(DefSet defs, Settings set, Rates.Style style, int n, int runs, string preset)
        {
            var colonies = new Colony[runs];
            Parallel.For(0, runs, i =>
            {
                var c = new Colony(defs, set, style, n, 1000 * n + 17 * i + style.key.Length * 7919 + (preset?.Length ?? 0) * 104729);
                c.Run();
                colonies[i] = c;
            });
            var s = new Summary();
            var samples = colonies.SelectMany(c => c.samples).ToList();
            double M(Func<Sample, double> f) => samples.Count == 0 ? 0 : samples.Average(x => f(x.s));
            s.good = M(x => x.good);
            s.goodRaw = M(x => x.goodRaw);
            s.bad = M(x => x.bad);
            s.steadyGood = M(x => x.steadyGood);
            s.steadyBad = M(x => x.steadyBad);
            s.net = s.good + s.bad + s.steadyGood + s.steadyBad;
            s.mood = M(x => x.mood);
            s.pinned = M(x => x.mood >= 0.95f ? 1 : 0);
            var moods = samples.Select(x => (double)x.s.mood).OrderBy(v => v).ToList();
            s.moodP10 = Pct(moods, 0.1);
            s.moodP90 = Pct(moods, 0.9);
            s.goodCount = M(x => x.goodCount);
            s.standards = colonies.SelectMany(c => c.people).Average(p => p.standards);
            var nets = samples.Select(x => (double)(x.s.good + x.s.bad + x.s.steadyGood + x.s.steadyBad)).OrderBy(v => v).ToList();
            // Rimfolk's good moods as felt: what growing ordinary takes off them comes off here too.
            var goods = samples.Select(x => (double)(x.s.good + x.s.steadyGood - x.s.ordinary)).OrderBy(v => v).ToList();
            s.netP10 = Pct(nets, 0.1);
            s.netP90 = Pct(nets, 0.9);
            s.goodP90 = Pct(goods, 0.9);
            s.goodP99 = Pct(goods, 0.99);
            s.goodHigh = goods.Count == 0 ? 0 : goods.Count(v => v >= 12) / (double)goods.Count;
            s.trustStart = samples.Where(x => x.day == 0).Select(x => (double)x.s.trust).DefaultIfEmpty(0).Average();
            s.trustEnd = samples.Where(x => x.day == Colony.YearDays - 1).Select(x => (double)x.s.trust).DefaultIfEmpty(0).Average();
            s.trustMean = M(x => x.trust);
            s.trustingShare = M(x => x.trust >= Code.TrustTrusting ? 1 : 0);
            s.doubtfulShare = M(x => x.trust <= -Code.TrustDoubtful ? 1 : 0);
            s.illShare = M(x => x.ill ? 1 : 0);
            s.illNotQuietShare = M(x => x.illNotQuiet ? 1 : 0);
            s.ailmentDays = colonies.Sum(c => c.ailmentDays) / (runs * (double)n);
            s.underMinor = M(x => x.mood < Rates.MinorBreak ? 1 : 0);
            s.gameOrdinary = M(x => x.gameOrdinary);
            double heartHours = colonies.Sum(c => c.heartHours);
            if (heartHours > 0)
            {
                s.strained = colonies.Sum(c => c.bandHours[1]) / heartHours;
                s.worn = colonies.Sum(c => c.bandHours[2]) / heartHours;
                s.breaking = colonies.Sum(c => c.bandHours[3]) / heartHours;
                s.steady = colonies.Sum(c => c.goodBandHours[1]) / heartHours;
                s.goodHeart = colonies.Sum(c => c.goodBandHours[2]) / heartHours;
                s.highSpirits = colonies.Sum(c => c.goodBandHours[3]) / heartHours;
                s.noStrain = colonies.Sum(c => c.noStrainHours) / heartHours;
                s.goodTenths = Enumerable.Range(0, 10).Select(t => colonies.Sum(c => c.goodTenths[t]) / heartHours).ToArray();
            }
            double colonistYears = runs * (double)n;
            s.episodesPerYear = colonies.Sum(c => c.episodes) / colonistYears;
            s.inspirationsPerYear = colonies.Sum(c => c.releases[0]) / colonistYears;
            s.kindWordsPerYear = colonies.Sum(c => c.releases[1]) / colonistYears;
            foreach (var p in colonies.SelectMany(c => c.people))
            {
                double share = Colony.YearDays / (double)(Colony.YearDays + Colony.WarmupDays);
                foreach (var kv in p.heart.filled) s.goodBy[kv.Key] = s.goodBy.GetValueOrDefault(kv.Key) + kv.Value / colonistYears * share;
                foreach (var kv in p.heart.spent) s.goodSpentBy[kv.Key] = s.goodSpentBy.GetValueOrDefault(kv.Key) + kv.Value / colonistYears * share;
                foreach (var kv in p.heart.built) s.strainBy[kv.Key] = s.strainBy.GetValueOrDefault(kv.Key) + kv.Value / colonistYears * Colony.YearDays / (Colony.YearDays + Colony.WarmupDays);
                foreach (var kv in p.heart.eased) s.reliefBy[kv.Key] = s.reliefBy.GetValueOrDefault(kv.Key) + kv.Value / colonistYears * Colony.YearDays / (Colony.YearDays + Colony.WarmupDays);
            }

            double days = runs * (double)Colony.YearDays;
            var letters = colonies.SelectMany(c => c.letters.Select(l => (c, l))).ToList();
            double Per(Channel ch) => letters.Count(x => x.l.channel == ch) / days;
            s.decisionsPerDay = Per(Channel.Decision);
            s.requestsPerDay = Per(Channel.Request);
            s.lettersPerDay = Per(Channel.Letter);
            s.messagesPerDay = Per(Channel.Message);
            s.digestPerDay = Per(Channel.Digest);
            s.attentionPerDay = s.decisionsPerDay + s.requestsPerDay;
            foreach (var g in letters.GroupBy(x => x.l.system))
            {
                var arr = new double[5];
                foreach (var x in g) arr[(int)x.l.channel] += 1;
                for (int i = 0; i < arr.Length; i++) arr[i] /= days;
                s.systems[g.Key] = arr;
            }
            // Clustering: a question that comes within 12 hours of another, and days with two or more.
            int clustered = 0, total = 0, attnClustered = 0, attnTotal = 0, twoDays = 0;
            foreach (var c in colonies)
            {
                var dec = c.letters.Where(l => l.channel == Channel.Decision).Select(l => l.hour).OrderBy(h => h).ToList();
                var attn = c.letters.Where(l => l.channel == Channel.Decision || l.channel == Channel.Request).Select(l => l.hour).OrderBy(h => h).ToList();
                for (int i = 1; i < dec.Count; i++)
                    if (dec[i] - dec[i - 1] < 12) clustered++;
                total += dec.Count;
                for (int i = 1; i < attn.Count; i++)
                    if (attn[i] - attn[i - 1] < 12) attnClustered++;
                attnTotal += attn.Count;
                twoDays += dec.GroupBy(h => h / 24).Count(g => g.Count() >= 2);
            }
            s.clustered = total == 0 ? 0 : clustered / (double)total;
            s.attentionClustered = attnTotal == 0 ? 0 : attnClustered / (double)attnTotal;
            s.twoDecisionDays = twoDays / days;
            foreach (var key in colonies.SelectMany(c => c.counts.Keys).Distinct())
                s.perYear[key] = colonies.Sum(c => c.counts.GetValueOrDefault(key)) / (double)runs;

            foreach (var key in colonies.SelectMany(c => c.ailmentDaysBy.Keys).Distinct())
                s.ailmentsBy[key] = colonies.Sum(c => c.ailmentDaysBy.GetValueOrDefault(key)) / (runs * (double)n);
            double partSamples = colonies.Sum(c => c.partSamples);
            foreach (var key in colonies.SelectMany(c => c.partSum.Keys).Distinct())
                s.parts[key] = partSamples == 0 ? 0 : colonies.Sum(c => c.partSum.GetValueOrDefault(key)) / partSamples;
            int bin = Colony.YearDays / Bins;
            s.netSeries = new double[Bins];
            s.goodSeries = new double[Bins];
            s.badSeries = new double[Bins];
            s.trustSeries = new double[Bins];
            for (int b = 0; b < Bins; b++)
            {
                var part = samples.Where(x => x.day / bin == b).ToList();
                if (part.Count == 0) continue;
                s.goodSeries[b] = part.Average(x => x.s.good + x.s.steadyGood);
                s.badSeries[b] = part.Average(x => x.s.bad + x.s.steadyBad);
                s.netSeries[b] = s.goodSeries[b] + s.badSeries[b];
                s.trustSeries[b] = part.Average(x => x.s.trust);
            }
            return new Result { preset = preset, style = style, colonists = n, summary = s };
        }

        private static double Pct(List<double> sorted, double p) => sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
    }
}
