using System.Text;

namespace Balance
{
    /// <summary>The readable report: targets first, then moods, trust, letters and ailments, then what the model assumes.</summary>
    public static class Report
    {
        private static string N(double v, string f = "0.0") => (v >= 0 ? "+" : "") + v.ToString(f);
        private static string P(double v) => (v * 100).ToString("0") + "%";
        private static string D(double v) => v.ToString("0.00");

        private static readonly string[] SystemOrder =
        {
            "wishes", "event wishes", "dilemmas", "dilemmas (after a raid)", "dilemmas (strangers)", "events", "encounters", "social events", "setbacks", "ailments",
            "health", "calendar", "stories", "camps", "world events", "missions", "play-outs", "meetings", "decrees", "dreams", "people", "colony", "world"
        };

        public static string Write(DefSet defs, SourceConstants src, List<Result> results, Dictionary<string, Summary> before, int runs)
        {
            var sb = new StringBuilder();
            Result R(string preset, string style, int n) => results.First(r => (r.preset ?? "default") == preset && r.style.key == style && r.colonists == n);

            sb.AppendLine("# Rimfolk balance report");
            sb.AppendLine();
            sb.AppendLine("Written by the colony-year simulator in `Tools/Balance` (run it from the repository root with");
            sb.AppendLine("`dotnet run -c Release --project Tools/Balance -- . --before Tools/Balance/before.json`). It plays a settled colony through one");
            sb.AppendLine($"60-day year ({Colony.WarmupDays} days of warm-up first, so standards and trust have settled) {runs} times for each case, without the game:");
            sb.AppendLine("the moods, wishes, dilemmas, events and ailments come from the def XML, the rates from the code (read from the source");
            sb.AppendLine("where it can, see the last section), and the mood rules from the mod's own `GoodTimesMath`, `PressureMath`,");
            sb.AppendLine("`SetbackMath`, `AilmentMath` and `ArcMath`, compiled in as they are. What it can't know (how often the storyteller");
            sb.AppendLine("sends visitors, how a player plays) is in `Tools/Balance/Rates.cs`, one documented table. Treat the numbers as");
            sb.AppendLine("a model's: good for comparing settings and changes, not a promise about one game.");
            sb.AppendLine();
            sb.AppendLine("**Rimfolk's mood** below is everything Rimfolk adds to a colonist's mood, per colonist, averaged over the year:");
            sb.AppendLine("its good memories (after the shared budget and fading sooner in good times), its bad memories (setbacks, a lapsed");
            sb.AppendLine("wish, frictions, a nightmare), and its steady moods (trust, the colony's ways, everyday pressures, ailments, a wish");
            sb.AppendLine("still waiting, strain). The game's own moods aren't in it.");
            sb.AppendLine();

            // ---------------- Targets ----------------
            sb.AppendLine("## Against the targets");
            sb.AppendLine();
            sb.AppendLine("The targets are in DESIGN.md (\"Balance targets\").");
            sb.AppendLine();
            sb.AppendLine("| | Target | 6 colonists | 12 colonists |" + (before != null ? " 6 colonists before |" : ""));
            sb.AppendLine("|---|---|---|---|" + (before != null ? "---|" : ""));
            string Before(string key, Func<Summary, string> f) => before != null && before.TryGetValue(key, out var b) ? " " + f(b) + " |" : before != null ? " - |" : "";
            foreach (var (style, target) in new[] { ("well", "+3 to +6"), ("avg", "0 to +3"), ("neglect", "-4 to -10") })
            {
                var a = R("default", style, 6).summary;
                var b = R("default", style, 12).summary;
                sb.AppendLine($"| Rimfolk's mood, {R("default", style, 6).style.name} | {target} | {N(a.net)} | {N(b.net)} |" + Before("default|" + style + "|6", x => N(x.net)));
            }
            {
                var a = R("default", "well", 6).summary;
                var b = R("default", "well", 12).summary;
                sb.AppendLine($"| Rimfolk's good moods at a high point (P99), well run | well short of the top | {N(a.goodP99)} | {N(b.goodP99)} |" + Before("default|well|6", x => N(x.goodP99)));
                sb.AppendLine($"| Time with +12 or more from Rimfolk's good moods, well run | rare | {P(a.goodHigh)} | {P(b.goodHigh)} |" + Before("default|well|6", x => P(x.goodHigh)));
                var a2 = R("default", "avg", 6).summary;
                var b2 = R("default", "avg", 12).summary;
                sb.AppendLine($"| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | {D(a2.decisionsPerDay)} | {D(b2.decisionsPerDay)} |" + Before("default|avg|6", x => D(x.decisionsPerDay)));
                sb.AppendLine($"| Questions and wishes voiced per day, average | (for reference) | {D(a2.attentionPerDay)} | {D(b2.attentionPerDay)} |" + Before("default|avg|6", x => D(x.attentionPerDay)));
                sb.AppendLine($"| Questions within 12 hours of another, average | few | {P(a2.clustered)} | {P(b2.clustered)} |" + Before("default|avg|6", x => P(x.clustered)));
                sb.AppendLine($"| Time with an everyday ailment, average | 10-15% | {P(a2.illShare)} | {P(b2.illShare)} |" + Before("default|avg|6", x => P(x.illShare)));
                sb.AppendLine($"| Time with an everyday ailment, well run | under 10-15% | {P(a.illShare)} | {P(b.illShare)} |" + Before("default|well|6", x => P(x.illShare)));
            }
            sb.AppendLine();

            // ---------------- Mood ----------------
            sb.AppendLine("## Rimfolk's mood per colonist");
            sb.AppendLine();
            sb.AppendLine("Good memories are what they count for after the shared budget (the two biggest in full, then 50%, 35%, 25%, 20%)");
            sb.AppendLine("and fading sooner above 75% mood; \"raw\" is the same memories without the budget. Steady moods are trust, the");
            sb.AppendLine("colony's ways, pressures, ailments, waiting and strain. P10 and P90: a hard and a good moment for one colonist.");
            sb.AppendLine();
            sb.AppendLine("| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in results.Where(r => r.preset == null))
            {
                var s = r.summary;
                sb.AppendLine($"| {r.colonists} colonists, {r.style.name} | {N(s.good)} ({N(s.goodRaw)}) | {N(s.bad)} | {N(s.steadyGood)} | {N(s.steadyBad)} | **{N(s.net)}** | {N(s.netP10)} / {N(s.netP90)} | {s.goodCount:0.0} | {P(s.mood)} | {s.standards:0.00} |");
            }
            sb.AppendLine();
            sb.AppendLine("Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:");
            sb.AppendLine();
            sb.AppendLine("| Play | " + string.Join(" | ", Enumerable.Range(0, Runner.Bins).Select(b => $"days {b * 10}-{b * 10 + 9}")) + " |");
            sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", Runner.Bins)));
            foreach (var r in results.Where(r => r.preset == null && r.colonists == 6))
            {
                var s = r.summary;
                sb.AppendLine($"| {r.style.name} | " + string.Join(" | ", Enumerable.Range(0, Runner.Bins).Select(b => $"{N(s.netSeries[b])} ({N(s.goodSeries[b])} / {N(s.badSeries[b])})")) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("Where it comes from (6 colonists): what each source adds to a colonist's mood on average. Good memories are");
            sb.AppendLine("shown as they count after the budget.");
            sb.AppendLine();
            var six = results.Where(r => r.preset == null && r.colonists == 6).ToList();
            var partKeys = six.SelectMany(r => r.summary.parts.Keys).Distinct()
                .OrderByDescending(k => six.Max(r => Math.Abs(r.summary.parts.GetValueOrDefault(k)))).ToList();
            sb.AppendLine("| Source | " + string.Join(" | ", six.Select(r => r.style.name)) + " |");
            sb.AppendLine("|---|" + string.Concat(six.Select(_ => "---|")));
            foreach (var k in partKeys)
            {
                if (six.All(r => Math.Abs(r.summary.parts.GetValueOrDefault(k)) < 0.05)) continue;
                sb.AppendLine($"| {k} | " + string.Join(" | ", six.Select(r => N(r.summary.parts.GetValueOrDefault(k), "0.00"))) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("What the shared budget and fading sooner take off (6 colonists):");
            sb.AppendLine();
            sb.AppendLine("| Play | Net with both rules | Net without either | Good memories with | without |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in results.Where(r => r.preset == null && r.colonists == 6))
            {
                var nr = results.FirstOrDefault(x => x.preset == "no budget or fading" && x.style == r.style);
                if (nr == null) continue;
                sb.AppendLine($"| {r.style.name} | {N(r.summary.net)} | {N(nr.summary.net)} | {N(r.summary.good)} | {N(nr.summary.good)} |");
            }
            sb.AppendLine();

            // ---------------- Trust ----------------
            sb.AppendLine("## Trust");
            sb.AppendLine();
            sb.AppendLine("Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).");
            sb.AppendLine();
            sb.AppendLine("| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in results.Where(r => r.preset == null))
            {
                var s = r.summary;
                sb.AppendLine($"| {r.colonists} colonists, {r.style.name} | {N(s.trustStart, "0")} | {N(s.trustEnd, "0")} | {N(s.trustMean, "0")} | {P(s.trustingShare)} | {P(s.doubtfulShare)} | {string.Join(", ", s.trustSeries.Select(v => N(v, "0")))} |");
            }
            sb.AppendLine();

            // ---------------- Letters ----------------
            sb.AppendLine("## Letters and messages per day");
            sb.AppendLine();
            sb.AppendLine("Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission");
            sb.AppendLine("offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show");
            sb.AppendLine("on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the");
            sb.AppendLine("smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.");
            sb.AppendLine();
            foreach (var r in results.Where(r => r.preset == null && r.style.key == "avg"))
            {
                sb.AppendLine($"**{r.colonists} colonists, {r.style.name} play**: {D(r.summary.decisionsPerDay)} questions, {D(r.summary.requestsPerDay)} wishes voiced, {D(r.summary.lettersPerDay)} letters, {D(r.summary.messagesPerDay)} messages and {D(r.summary.digestPerDay)} news lines a day.");
                sb.AppendLine();
                sb.AppendLine("| System | Questions | Wishes | Letters | Messages | News lines |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var kv in r.summary.systems.OrderBy(kv => Array.IndexOf(SystemOrder, kv.Key) < 0 ? 99 : Array.IndexOf(SystemOrder, kv.Key)))
                    sb.AppendLine($"| {kv.Key} | {D(kv.Value[0])} | {D(kv.Value[1])} | {D(kv.Value[2])} | {D(kv.Value[3])} | {D(kv.Value[4])} |");
                sb.AppendLine();
            }
            sb.AppendLine("By play style (6 colonists):");
            sb.AppendLine();
            sb.AppendLine("| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var r in results.Where(r => r.preset == null && r.colonists == 6))
            {
                var s = r.summary;
                sb.AppendLine($"| {r.style.name} | {D(s.decisionsPerDay)} | {D(s.requestsPerDay)} | {D(s.lettersPerDay)} | {D(s.messagesPerDay)} | {D(s.digestPerDay)} | {P(s.clustered)} | {P(s.twoDecisionDays)} |");
            }
            sb.AppendLine();

            // ---------------- Ailments ----------------
            sb.AppendLine("## Ailments");
            sb.AppendLine();
            sb.AppendLine("Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. \"Not quiet\" leaves");
            sb.AppendLine("out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.");
            sb.AppendLine();
            sb.AppendLine("| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in results.Where(r => r.preset == null))
            {
                var s = r.summary;
                sb.AppendLine($"| {r.colonists} colonists, {r.style.name} | {s.ailmentDays:0.0} | {P(s.illShare)} | {P(s.illNotQuietShare)} | {s.perYear.GetValueOrDefault("catching ailments"):0.0} | {s.perYear.GetValueOrDefault("ailments"):0.0} | {s.perYear.GetValueOrDefault("serious illnesses"):0.0} |");
            }
            sb.AppendLine();

            sb.AppendLine("What fills the time (days per colonist per year, 6 colonists):");
            sb.AppendLine();
            var sixAll = results.Where(r => r.preset == null && r.colonists == 6).ToList();
            var ailKeys = sixAll.SelectMany(r => r.summary.ailmentsBy.Keys).Distinct()
                .OrderByDescending(k => sixAll.Sum(r => r.summary.ailmentsBy.GetValueOrDefault(k))).Take(16).ToList();
            sb.AppendLine("| Ailment | " + string.Join(" | ", sixAll.Select(r => r.style.name)) + " |");
            sb.AppendLine("|---|" + string.Concat(sixAll.Select(_ => "---|")));
            foreach (var k in ailKeys)
                sb.AppendLine($"| {k.Replace("Council_Ail_", "")} | " + string.Join(" | ", sixAll.Select(r => r.summary.ailmentsBy.GetValueOrDefault(k).ToString("0.0"))) + " |");
            sb.AppendLine();

            // ---------------- Presets ----------------
            sb.AppendLine("## The presets");
            sb.AppendLine();
            sb.AppendLine("The style presets of the settings window, applied as the window applies them (6 colonists).");
            sb.AppendLine();
            sb.AppendLine("| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var preset in new[] { "default", "lightTouch", "storyRich", "demanding" })
                foreach (var r in results.Where(r => (r.preset ?? "default") == preset && r.colonists == 6))
                {
                    var s = r.summary;
                    sb.AppendLine($"| {preset} | {r.style.name} | {N(s.net)} | {N(s.good)} | {N(s.bad + s.steadyBad)} | {D(s.decisionsPerDay)} | {D(s.perYear.GetValueOrDefault("wishes made") / Colony.YearDays)} | {P(s.illShare)} |");
                }
            sb.AppendLine();

            // ---------------- How often things happen ----------------
            sb.AppendLine("## How often things happen (per colony per year)");
            sb.AppendLine();
            var keys = results.Where(r => r.preset == null).SelectMany(r => r.summary.perYear.Keys).Distinct().OrderBy(k => k).ToList();
            sb.AppendLine("| What | " + string.Join(" | ", results.Where(r => r.preset == null).Select(r => $"{r.colonists}, {r.style.name}")) + " |");
            sb.AppendLine("|---|" + string.Concat(results.Where(r => r.preset == null).Select(_ => "---|")));
            foreach (var k in keys)
                sb.AppendLine($"| {k} | " + string.Join(" | ", results.Where(r => r.preset == null).Select(r => r.summary.perYear.GetValueOrDefault(k).ToString("0.0"))) + " |");
            sb.AppendLine();

            // ---------------- The model ----------------
            sb.AppendLine("## What the model reads and assumes");
            sb.AppendLine();
            sb.AppendLine($"From the defs: {defs.thoughts.Count} moods, {defs.dilemmas.Count} dilemmas, {defs.events.Count} events and missions, {defs.encounters.Count} encounters,");
            sb.AppendLine($"{defs.socialEvents.Count} social events, {defs.moments.Count} moments, {defs.dreams.Count} dreams, {defs.nuisances.Count} nuisances, {defs.wishes.Count} wishes,");
            sb.AppendLine($"{defs.ailments.Count} ailments and illnesses, {defs.catching.Count} catching ones, {defs.situations.Count} world events ({defs.skippedForMods} defs for other mods left out).");
            sb.AppendLine();
            sb.AppendLine("From the code, read from the source each run:");
            sb.AppendLine();
            sb.AppendLine("| Constant | File | Value | Found |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var (name, file, value, found) in src.read)
                sb.AppendLine($"| {name} | `{file}` | {value:0.###} | {(found ? "yes" : "**no, fallback used**")} |");
            sb.AppendLine();
            sb.AppendLine("Mirrored in `Tools/Balance/Rates.cs` (estimates, not read from anywhere):");
            sb.AppendLine();
            sb.AppendLine($"- The storyteller sends an everyday event every {Rates.StorytellerMiscMtbDays} days (Cassandra); the base game's own weigh {Rates.VanillaMiscWeight}, and Rimfolk's events share the rest up to the code's cap.");
            sb.AppendLine($"- A big threat every {Rates.BigThreatDays} days. Strangers at home: " + string.Join(", ", Rates.Present.Select(kv => $"{kv.Key.ToLowerInvariant()} {P(kv.Value)} of hours")) + ".");
            sb.AppendLine($"- {P(Rates.Castable)} of picked events can be cast; dilemmas, encounters and social events are cast {P(Rates.WorkerCasts)} of the time times how often the conditions they name hold (a tie, a memory of a death, an illness: `Rates.Gates`). Each trait a filter names reaches {P(Rates.TraitShare)} of colonists.");
            sb.AppendLine($"- Colonists talk {Rates.InteractionsPerHour} times a waking hour; the moments that fit (their weight times how often their conditions hold) compete with chitchat's 1.");
            sb.AppendLine($"- A settled calendar holds {Rates.CalendarDays(6)} days (6 colonists) or {Rates.CalendarDays(12)} (12); a personal story runs {Rates.StoryDaysMin}-{Rates.StoryDaysMax} days; an ambition {Rates.AmbitionDaysMin}-{Rates.AmbitionDaysMax}.");
            sb.AppendLine("- What colonists do all day (builders, growers, crafters, cooks, researchers, hunters, doctors) and the share of days that bring each ailment's cause, by season.");
            sb.AppendLine("- The play styles: the game's own moods around Rimfolk, how many wishes are granted and how soon, which answers are chosen, how ailments are tended, how often pressures and setbacks come:");
            sb.AppendLine();
            sb.AppendLine("| | " + string.Join(" | ", Rates.Styles.Select(s => s.name)) + " |");
            sb.AppendLine("|---|---|---|---|");
            sb.AppendLine("| The game's own moods (good / bad) | " + string.Join(" | ", Rates.Styles.Select(s => $"+{s.vanillaGood:0} / {s.vanillaBad:0}")) + " |");
            sb.AppendLine("| Wishes granted | " + string.Join(" | ", Rates.Styles.Select(s => P(s.grant))) + " |");
            sb.AppendLine("| Promises made / kept | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.promise)} / {P(s.promiseKept)}")) + " |");
            sb.AppendLine("| Lasting discomforts that voice a wish, per colonist | " + string.Join(" | ", Rates.Styles.Select(s => s.discomforts.ToString("0.0"))) + " |");
            sb.AppendLine("| Answers: best for the colony / left to the default | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.best)} / {P(s.byDefault)}")) + " |");
            sb.AppendLine("| Ambitions reached | " + string.Join(" | ", Rates.Styles.Select(s => P(s.ambitionDone))) + " |");
            sb.AppendLine("| Ailments tended (quality) | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.tendShare)} ({P(s.tendQuality)})")) + " |");
            sb.AppendLine("| Setbacks (factor on the table) | " + string.Join(" | ", Rates.Styles.Select(s => $"x{s.setbackFactor:0.0}")) + " |");
            sb.AppendLine("| A varied table | " + string.Join(" | ", Rates.Styles.Select(s => P(s.variedTable))) + " |");
            sb.AppendLine("| The same meal again (stage 1 / 2 / 3) | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.sameMeal[1])} / {P(s.sameMeal[2])} / {P(s.sameMeal[3])}")) + " |");
            sb.AppendLine("| The same work again (stage 1 / 2 / 3) | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.sameWork[1])} / {P(s.sameWork[2])} / {P(s.sameWork[3])}")) + " |");
            sb.AppendLine("| Strained (-3 / -5 / -8) | " + string.Join(" | ", Rates.Styles.Select(s => $"{P(s.strain[0])} / {P(s.strain[1])} / {P(s.strain[2])}")) + " |");
            sb.AppendLine("| The colony's ways (-4 / -2 / +2 / +4) | " + string.Join(" | ", Rates.Styles.Select(s => string.Join(" / ", s.ways.Select(w => P(w))))) + " |");
            sb.AppendLine();
            sb.AppendLine("Not modelled: quirks, heirlooms and the colony's trait pride (small steady moods either way), decrees' own moods");
            sb.AppendLine("(the player's choice), secrets, vendettas and nemeses' moods (rare), the base game's moods themselves (a fixed");
            sb.AppendLine("good and bad per play style, under the standards' share), battles from events, and other mods' content.");
            return sb.ToString();
        }
    }
}
