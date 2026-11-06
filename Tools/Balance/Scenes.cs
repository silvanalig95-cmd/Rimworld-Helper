// How often Rimfolk stages something on the map (--scenes): every gathering, group outing and small errand the colony-year
// plays out, by the system that starts it, per year and per week, how they bunch up, and what a weekly allowance would
// leave. Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --scenes [repo root] [--runs N] [--colonists N] [--seed S] [--gap H] [--week W]
// The answers, the calendar, the council's meetings, evenings of faith and life goals are the simulator's own (Sim.cs);
// what it doesn't otherwise model (groups calling their people, elections, weddings, family suppers, camps, decrees that
// gather people, personal stories' stages) is added here, at the rates of the code where they can be read
// (SceneConstants) and of Rates.Scene* where they can't. An ordinary run is unchanged: nothing that stages a scene draws a
// number unless --scenes set the colony's list.
// Known gap: the simulator casts every dilemma at a flat share (Rates.WorkerCasts), so the seasonal customs, which the game
// asks whenever the others' own checks fail, come about a third as often as in a played game (the player's: seven in 60
// days, the model's: two or three a year). BALANCE_SCENES_DEBUG=1 lists the seasonal dilemmas with the gate the model gives them.

using System.Globalization;
using ColonyCouncil;

namespace Balance
{
    /// <summary>One thing staged on the map: when, by what, how big and how long.</summary>
    public sealed class Scene
    {
        /// <summary>Hours from the start of the measured year.</summary>
        public int hour;
        public string source;
        /// <summary>"gathering" (people sit or stand together: a feast, a toast, a fire, words, a meeting), "outing" (a group goes out
        /// to do something: a work party, a walk, a vigil, the dead carried out) or "small" (one or two: a visit, a lesson, an errand).</summary>
        public string tier;
        public int people;
        public float hours;
        /// <summary>The colony's own doing, not an answer the player picked: may wait or be let go under an allowance.</summary>
        public bool routine;
        /// <summary>A seasonal custom the council is asked about (a dilemma with seasons): need not be asked while the allowance is spent.</summary>
        public bool custom;
        /// <summary>A calendar day's way: "feast" (a feast, games or a fire), "faith" (an evening of faith), "remembrance" (at a grave).</summary>
        public string way;
    }

    /// <summary>The numbers of the code that the scene sources follow, read from the source where it can.</summary>
    public static class SceneConstants
    {
        public static float GroupCallAfterDays, GroupCallChance, GroupMinSize, GroupMaxSize;
        public static float SupperAtLeast, SupperChance, SupperGapDays;
        public static float CampDailyChance, CampGapDays, CampHelpChance;
        public static float EveningGapDays;

        public static void Read(SourceConstants s)
        {
            const string g = "Social/Groups/GroupMath.cs";
            GroupCallAfterDays = s.Num("a close group's anchor calls them, days after the last", g, @"public const int CallAfterDays = (\d+);", 8);
            GroupCallChance = s.Num("and then the chance a day", g, @"public const float CallChance = ([\d.]+)f;", 0.2f);
            GroupMinSize = s.Num("a group's fewest people", g, @"public const int MinSize = (\d+),", 3);
            GroupMaxSize = s.Num("a group's most people", g, @"MaxSize = (\d+),", 7);
            const string f = "Social/FamilyLifeMath.cs";
            SupperAtLeast = s.Num("a family supper needs this many at home", f, @"public const int SupperAtLeast = (\d+);", 3);
            SupperChance = s.Num("a family supper's chance an evening", f, @"public const float SupperChance = ([\d.]+)f;", 0.2f);
            SupperGapDays = s.Num("days between a family's suppers at least", f, @"public const int SupperGapDays = (\d+);", 6);
            CampDailyChance = s.Num("a camp's chance each open morning", "Camps/CampTimer.cs", @"public const float DailyChance = ([\d.]+)f;", 0.05f);
            CampGapDays = s.Num("days after a camp before the next may come", "Camps/CampTracker.cs", @"float gap = ([\d.]+)f \*", 15);
            CampHelpChance = s.Num("a camp's chance a day to help (a show, a song, a healer...)", "Camps/CampKindDef.cs", @"public float helpChance = ([\d.]+)f;", 0.15f);
            EveningGapDays = s.Num("days between evenings of faith at least", "Faith/FaithCouncilMath.cs", @"public const float EveningGapDays = ([\d.]+)f;", 5);
        }
    }

    public static class ScenesSim
    {
        /// <summary>What an answer's effect sets going on the map, by its class (Tools/playout_audit.py's list, in tiers).</summary>
        public static string Tier(string cls)
        {
            if (cls == null) return null;
            switch (cls)
            {
                case "DilemmaEffect_Gathering": case "DilemmaEffect_Feast": case "DilemmaEffect_Bonfire": case "DilemmaEffect_Toast":
                case "DilemmaEffect_Words": case "DilemmaEffect_FaithWords": case "DilemmaEffect_FaithEvening": case "DilemmaEffect_PlanWedding":
                case "DilemmaEffect_Race": case "VisitEffect_WorkHere": case "DilemmaEffect_Contest":
                    return "gathering";
                case "DilemmaEffect_WorkParty": case "DilemmaEffect_Procession": case "DilemmaEffect_Vigil": case "DilemmaEffect_Search":
                case "DilemmaEffect_HuntParty": case "DilemmaEffect_HaulDead": case "DilemmaEffect_Rounds": case "DilemmaEffect_UndertakersBury":
                case "DilemmaEffect_SmudgeFires": case "DilemmaEffect_SowPatch":
                    return "outing";
                case "DilemmaEffect_Visit": case "DilemmaEffect_Lesson": case "DilemmaEffect_Teach": case "DilemmaEffect_Mentorship":
                case "DilemmaEffect_CarryOut": case "DilemmaEffect_Made": case "DilemmaEffect_BurnIt": case "DilemmaEffect_TroubleWord":
                case "DilemmaEffect_KinFree": case "DilemmaEffect_KinTalk": case "DilemmaEffect_BarberCuts": case "DilemmaEffect_VetRounds":
                case "DilemmaEffect_SellArt": case "DilemmaEffect_ArtCommission": case "DilemmaEffect_WeddingNight": case "DilemmaEffect_Banish":
                    return "small";
                default:
                    return null;
            }
        }

        private static int Rank(string tier) => tier == "gathering" ? 3 : tier == "outing" ? 2 : tier == "small" ? 1 : 0;

        /// <summary>The biggest thing a list of effects stages, or null.</summary>
        public static string Biggest(IEnumerable<Effect> effects)
        {
            string best = null;
            foreach (var e in effects)
            {
                string t = Tier(e.cls);
                if (Rank(t) > Rank(best)) best = t;
            }
            return best;
        }

        public static void Run(string[] args)
        {
            string root = ".";
            int runs = 200, n = 10, seed = 1, week = 2;
            float gap = 36f;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--scenes": break;
                    case "--runs": runs = int.Parse(args[++i]); break;
                    case "--colonists": n = int.Parse(args[++i]); break;
                    case "--seed": seed = int.Parse(args[++i]); break;
                    case "--gap": gap = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--week": week = int.Parse(args[++i]); break;
                    default: root = args[i]; break;
                }
            }
            var defs = new DefSet(root);
            var src = new SourceConstants(root);
            Code.Read(src);
            SceneConstants.Read(src);
            var set = new Settings(src.SettingDefaults());
            Console.WriteLine($"How often Rimfolk stages something on the map: {n} colonists, {runs} colony-years of {Colony.YearDays} days each, default settings.");
            Console.WriteLine();
            // What the defs offer: the share of dilemmas and events with an answer that gathers people, seasonal or not.
            foreach (var (name, list) in new[] { ("dilemmas", defs.dilemmas), ("events", defs.events) })
            {
                var seasonal = list.Where(d => d.seasons != null && d.seasons.Count > 0).ToList();
                var rest = list.Where(d => d.seasons == null || d.seasons.Count == 0).ToList();
                string Share(List<OutcomeDef> l) => l.Count == 0 ? "-" : $"{l.Count(d => d.options.Any(o => Biggest(o.effects) == "gathering")) / (double)l.Count:P0} of {l.Count}";
                Console.WriteLine($"The {name} with an answer that gathers people: seasonal {Share(seasonal)}, the rest {Share(rest)}.");
            }
            Console.WriteLine();
            if (Environment.GetEnvironmentVariable("BALANCE_SCENES_DEBUG") == "1")
                foreach (var d in defs.dilemmas.Where(d => d.seasons != null && d.seasons.Count > 0))
                    Console.WriteLine($"  {d.name} gate {d.gate:0.00} weight {d.weight} cd {d.cooldownDays} seasons {string.Join(",", d.seasons)} min {d.minColonists} threat {d.withinDaysOfThreat} stranger {d.stranger}");
            foreach (var style in Rates.Styles)
            {
                var all = new List<Scene>[runs];
                var counts = new Dictionary<string, int>[runs];
                Parallel.For(0, runs, r =>
                {
                    var c = new Colony(defs, set, style, n, seed * 7919 + r * 104729 + style.key.Length) { scenes = new List<Scene>() };
                    c.Run();
                    all[r] = c.scenes;
                    counts[r] = c.counts;
                });
                Print(style, all, runs, gap, week);
                var asked = counts.SelectMany(x => x).Where(kv => kv.Key == "dilemmas" || kv.Key == "events" || kv.Key.StartsWith("offered"))
                    .GroupBy(kv => kv.Key).Select(g => $"{g.Key} {g.Sum(kv => kv.Value) / (double)runs:0.0}");
                Console.WriteLine("A year: " + string.Join("; ", asked) + ".");
                Console.WriteLine();
            }
            Console.WriteLine("Read from the source:");
            foreach (var (name, file, value, found) in src.read.Where(x => x.file.Contains("Group") || x.file.Contains("Family") || x.file.Contains("Camp") || x.file.Contains("Faith")))
                Console.WriteLine($"  {name}: {value.ToString(CultureInfo.InvariantCulture)} ({file}{(found ? "" : ", NOT FOUND: the fallback")})");
        }

        private static void Print(Rates.Style style, List<Scene>[] all, int runs, float gap, int week)
        {
            const int days = Colony.YearDays;
            const double weeks = days / 7.0;
            Console.WriteLine($"== {style.name} colony");
            Console.WriteLine();
            Console.WriteLine("| Source | Gatherings a year | Outings a year | Small a year | Colonist-hours a year |");
            Console.WriteLine("|---|---|---|---|---|");
            var scenes = all.SelectMany(x => x).ToList();
            foreach (var g in scenes.GroupBy(s => s.source).OrderByDescending(g => g.Count(s => s.tier == "gathering")).ThenByDescending(g => g.Count()))
            {
                double Per(string tier) => g.Count(s => s.tier == tier) / (double)runs;
                double hours = g.Sum(s => s.people * s.hours) / runs;
                Console.WriteLine($"| {g.Key} | {Per("gathering"):0.0} | {Per("outing"):0.0} | {Per("small"):0.0} | {hours:0} |");
            }
            double gy = scenes.Count(s => s.tier == "gathering") / (double)runs, oy = scenes.Count(s => s.tier == "outing") / (double)runs, sy = scenes.Count(s => s.tier == "small") / (double)runs;
            Console.WriteLine($"| all | {gy:0.0} | {oy:0.0} | {sy:0.0} | {scenes.Sum(s => s.people * s.hours) / runs:0} |");
            Console.WriteLine();
            Console.WriteLine($"Per week: {gy / weeks:0.0} gatherings, {oy / weeks:0.0} outings, {sy / weeks:0.0} small; {(gy + oy) / weeks:0.0} gatherings and outings together.");
            Bunching(all, "gathering", s => s.tier == "gathering");
            Bunching(all, "gathering or outing", s => s.tier != "small");
            Allowance(all, runs, gap, week);
            Proposal(all, runs, gap, week);
            AsBuilt(all, runs, week);
            Console.WriteLine();
        }

        /// <summary>
        /// The colony's pace as the mod keeps it, by its own rules (PlayOut/StageMath.cs, linked): what the player chose
        /// always happens and counts; the colony's own (a group calling its people, a family supper, a camp's show, a
        /// child's birthday toast) waits in the evenings for <see cref="StageMath.Room"/>, up to
        /// <see cref="StageMath.WaitDays"/> days, then is let go; a life goal's toast is raised at the next gathering (never
        /// a scene of its own); the hustings join the count; a calendar day's feast, games or fire within
        /// <see cref="StageMath.TogetherDays"/> days after another kept that way is kept together with it, and a day that
        /// gathers people (those, an evening of faith, a quiet hour at a grave) within <see cref="StageMath.SupperDays"/> days of another
        /// gathering is kept at supper; one seasonal custom a season; and the floor's evening once eight days pass without a
        /// gathering with nothing of the colony's own waiting.
        /// </summary>
        /// <summary>One colony-year's scenes under the pace as built: when each kept scene was held, and what came of the rest.</summary>
        public sealed class Kept
        {
            /// <summary>The hours the kept gatherings and outings were held (the floor's evenings among them).</summary>
            public readonly List<int> held = new List<int>();
            /// <summary>The hours a calendar day was kept at supper instead (a word said, a line in the evening's news).</summary>
            public readonly List<int> supper = new List<int>();
            public int total, kept, folded, floors;
        }

        /// <summary>The pace as built, on one colony-year's scenes (gatherings and outings), each source's tally into <paramref name="bySource"/>.</summary>
        public static Kept KeepRun(List<Scene> run, int week, Dictionary<string, (int all, int kept)> bySource = null)
        {
            const int days = Colony.YearDays;
            const int T = StageMath.TicksPerHour;
            var r = new Kept();
            var list = run.Where(s => s.tier != "small").OrderBy(s => s.hour).ToList();
            var ticks = new List<int>();
            var waiting = new List<(Scene s, int until)>();
            int lastFeastDay = -9999, customSeason = -1;
            int Since(int h) => StageMath.Since(ticks, h * T);
            void Note(Scene s, bool ok)
            {
                if (bySource != null)
                {
                    var v = bySource.GetValueOrDefault(s.source);
                    bySource[s.source] = (v.all + 1, v.kept + (ok ? 1 : 0));
                }
                r.total++;
                if (ok) r.kept++;
                else r.folded++;
            }
            void Hold(Scene s, int h)
            {
                r.held.Add(h);
                ticks.Add(h * T);
                if (s != null) Note(s, true);
            }
            int idx = 0;
            for (int h = 0; h < days * 24; h++)
            {
                int hourOfDay = h % 24;
                for (int w = 0; w < waiting.Count; w++)
                {
                    var (s, until) = waiting[w];
                    if (hourOfDay >= 17 && hourOfDay <= 20 && StageMath.Room(ticks, h * T, week, Since(h)))
                    {
                        Hold(s, h);
                        waiting.RemoveAt(w--);
                    }
                    else if (h >= until)
                    {
                        Note(s, false);
                        waiting.RemoveAt(w--);
                    }
                }
                // The floor: long without a gathering and nothing of the colony's own waiting, an evening together.
                if (hourOfDay == 18 && waiting.Count == 0 && StageMath.FloorEvening(Since(h)))
                {
                    r.floors++;
                    Hold(null, h);
                }
                while (idx < list.Count && list[idx].hour == h)
                {
                    var s = list[idx++];
                    if (s.source == "life goals' toasts")
                    {
                        // Raised at the next gathering, or at supper: never a scene of its own.
                        Note(s, false);
                        continue;
                    }
                    if (s.source == "elections" && list.Skip(idx).Any(o => o.source == "elections" && o.hour - h <= 30))
                    {
                        // The hustings: the candidates speak at the count instead.
                        Note(s, false);
                        continue;
                    }
                    if (s.source == "calendar days" && s.way == "feast" && StageMath.KeptTogether(h / 24 - lastFeastDay))
                    {
                        // Kept together with the day just kept (its letter named them both).
                        Note(s, false);
                        continue;
                    }
                    if (s.source == "calendar days" && s.way != null && StageMath.AtSupper(Since(h)))
                    {
                        r.supper.Add(h + Math.Max(0, 19 - hourOfDay));
                        Note(s, false);
                        continue;
                    }
                    if (s.source == "calendar days" && s.way == "feast") lastFeastDay = h / 24;
                    if (s.custom)
                    {
                        int season = StageMath.SeasonKey(h / 24 / days, h / 24 % days);
                        if (!StageMath.CustomMayCome(customSeason, season))
                        {
                            Note(s, false);
                            continue;
                        }
                        customSeason = season;
                    }
                    if (s.routine)
                    {
                        if (hourOfDay >= 17 && hourOfDay <= 20 && StageMath.Room(ticks, h * T, week, Since(h))) Hold(s, h);
                        else waiting.Add((s, h + StageMath.WaitDays * 24));
                        continue;
                    }
                    Hold(s, h);
                }
            }
            r.held.Sort();
            return r;
        }

        private static void AsBuilt(List<Scene>[] all, int runs, int week)
        {
            const int days = Colony.YearDays;
            var kept = new Dictionary<string, (int all, int kept)>();
            int total = 0, keptTotal = 0, folded = 0, twoDays = 0, floors = 0;
            var weekly = new List<int>();
            var gaps = new List<int>();
            foreach (var run in all)
            {
                var r = KeepRun(run, week, kept);
                total += r.total;
                keptTotal += r.kept;
                folded += r.folded;
                floors += r.floors;
                var held = r.held;
                var perDay = new int[days];
                foreach (int h in held)
                    if (h / 24 < days) perDay[h / 24]++;
                twoDays += perDay.Count(x => x >= 2);
                for (int d = 0; d + 7 <= days; d++) weekly.Add(perDay.Skip(d).Take(7).Sum());
                for (int i = 1; i < held.Count; i++) gaps.Add(held[i] - held[i - 1]);
            }
            weekly.Sort();
            gaps.Sort();
            double under24 = gaps.Count == 0 ? 0 : gaps.Count(g => g < 24) / (double)gaps.Count;
            double perWeek = (keptTotal + floors) / (days / 7.0) / runs;
            Console.WriteLine();
            Console.WriteLine($"As built (PlayOut/StageMath.cs; {week} a week, {StageMath.GapHours / 24f:0.#} days apart, {StageMath.WaitDays} days' wait, the floor at {StageMath.FloorEveningDays:0} days): "
                              + $"{keptTotal / (double)runs:0.0} of {total / (double)runs:0.0} a year staged as their own scene and {floors / (double)runs:0.0} floor evenings "
                              + $"({perWeek:0.0} gatherings and outings a week), {folded / (double)runs:0.0} kept smaller, folded into another or let go; any 7 days: median "
                              + $"{weekly[weekly.Count / 2]}, 1 in 10 {weekly[(int)(weekly.Count * 0.9)]} or more, the busiest {weekly.LastOrDefault()}; days with two or more "
                              + $"{twoDays / (double)runs:0.0} a year; under a day apart {under24:P0}; the longest gap {(gaps.Count == 0 ? 0 : gaps[gaps.Count - 1]) / 24.0:0.0} days.");
            Console.WriteLine("| Source | A year | As its own scene |");
            Console.WriteLine("|---|---|---|");
            foreach (var kv in kept.OrderByDescending(k => k.Value.all))
                Console.WriteLine($"| {kv.Key} | {kv.Value.all / (double)runs:0.0} | {(kv.Value.all == 0 ? 0 : kv.Value.kept / (double)kv.Value.all):P0} |");
        }

        /// <summary>
        /// The audit's proposal, as rules on the same year (each scene in time order, gatherings and outings): an answer the
        /// player picked, a meeting, the election's count, a wedding, a decree, an evening of faith, an ambition and a
        /// story's stage always happen; a calendar day kept with a gathering within two days of another gathering is kept
        /// at supper instead (small), and two calendar days within three days are kept together; a seasonal custom is asked
        /// once a season at most, never within two days of a gathering; the hustings join the count; a life goal's toast is
        /// raised at the next gathering within two days, else at supper (small); the colony's own (a group calling its
        /// people, a family supper, a camp's show, a child's birthday toast) waits for room as in the allowance.
        /// </summary>
        private static void Proposal(List<Scene>[] all, int runs, float gap, int week)
        {
            const int days = Colony.YearDays;
            var kept = new Dictionary<string, (int all, int kept)>();
            int total = 0, keptTotal = 0, twoDays = 0, small = 0;
            var weekly = new List<int>();
            var gaps = new List<int>();
            foreach (var run in all)
            {
                var list = run.Where(s => s.tier != "small").OrderBy(s => s.hour).ToList();
                var held = new List<int>();
                var waiting = new List<(Scene s, int until)>();
                int lastCalendar = -9999, lastCustom = -9999;
                int Week(int h) => held.Count(x => x > h - 7 * 24 && x <= h);
                int Since(int h) => held.Count == 0 ? 9999 : h - held[held.Count - 1];
                bool Room(int h) => Week(h) < week && Since(h) >= gap;
                void Note(Scene s, bool ok)
                {
                    var v = kept.GetValueOrDefault(s.source);
                    kept[s.source] = (v.all + 1, v.kept + (ok ? 1 : 0));
                    total++;
                    if (ok) keptTotal++;
                    else small++;
                }
                void Hold(Scene s, int h)
                {
                    held.Add(h);
                    Note(s, true);
                }
                int idx = 0;
                for (int h = 0; h < days * 24; h++)
                {
                    for (int w = 0; w < waiting.Count; w++)
                    {
                        var (s, until) = waiting[w];
                        if (Room(h) && h % 24 >= 17 && h % 24 <= 20)
                        {
                            Hold(s, h);
                            waiting.RemoveAt(w--);
                        }
                        else if (h >= until)
                        {
                            Note(s, false);
                            waiting.RemoveAt(w--);
                        }
                    }
                    while (idx < list.Count && list[idx].hour == h)
                    {
                        var s = list[idx++];
                        if (s.source == "life goals' toasts")
                        {
                            // Raised at the next gathering within two days, else at supper: never a scene of its own.
                            Note(s, false);
                            continue;
                        }
                        if (s.source == "elections" && list.Skip(idx).Any(o => o.source == "elections" && o.hour - h <= 30))
                        {
                            // The hustings: the candidates speak at the count instead.
                            Note(s, false);
                            continue;
                        }
                        if (s.source == "calendar days")
                        {
                            if (h - lastCalendar <= 72 || Since(h) < 48) Note(s, false);
                            else
                            {
                                lastCalendar = h;
                                Hold(s, h);
                            }
                            continue;
                        }
                        if (s.custom)
                        {
                            if (h - lastCustom < 15 * 24 || Since(h) < 48) Note(s, false);
                            else
                            {
                                lastCustom = h;
                                Hold(s, h);
                            }
                            continue;
                        }
                        if (s.routine)
                        {
                            if (Room(h)) Hold(s, h);
                            else waiting.Add((s, h + 72));
                            continue;
                        }
                        Hold(s, h);
                    }
                }
                var perDay = new int[days];
                foreach (int h in held)
                    if (h / 24 < days) perDay[h / 24]++;
                twoDays += perDay.Count(x => x >= 2);
                for (int d = 0; d + 7 <= days; d++) weekly.Add(perDay.Skip(d).Take(7).Sum());
                for (int i = 1; i < held.Count; i++) gaps.Add(held[i] - held[i - 1]);
            }
            weekly.Sort();
            gaps.Sort();
            double under24 = gaps.Count == 0 ? 0 : gaps.Count(g => g < 24) / (double)gaps.Count;
            Console.WriteLine();
            Console.WriteLine($"The proposal (routine {week} a week, {gap / 24f:0.#} days apart; calendar days and customs thinned, toasts and hustings folded in): "
                              + $"{keptTotal / (double)runs:0.0} of {total / (double)runs:0.0} a year staged as their own scene ({keptTotal / (days / 7.0) / runs:0.0} a week), "
                              + $"{small / (double)runs:0.0} kept smaller or folded into another; any 7 days: median {weekly[weekly.Count / 2]}, 1 in 10 {weekly[(int)(weekly.Count * 0.9)]} or more, "
                              + $"the busiest {weekly.LastOrDefault()}; days with two or more {twoDays / (double)runs:0.0} a year; under a day apart {under24:P0}.");
            Console.WriteLine("| Source | A year | As its own scene |");
            Console.WriteLine("|---|---|---|");
            foreach (var kv in kept.OrderByDescending(k => k.Value.all))
                Console.WriteLine($"| {kv.Key} | {kv.Value.all / (double)runs:0.0} | {(kv.Value.all == 0 ? 0 : kv.Value.kept / (double)kv.Value.all):P0} |");
        }

        /// <summary>How the scenes bunch: the busiest week, days with two, the gap between one and the next.</summary>
        private static void Bunching(List<Scene>[] all, string what, Func<Scene, bool> which)
        {
            const int days = Colony.YearDays;
            var weekly = new List<int>();
            int twoDays = 0, threeDays = 0, gaps = 0, under24 = 0, under48 = 0;
            var gapList = new List<int>();
            foreach (var run in all)
            {
                var hours = run.Where(which).Select(s => s.hour).OrderBy(h => h).ToList();
                var perDay = new int[days];
                foreach (int h in hours)
                    if (h / 24 < days) perDay[h / 24]++;
                for (int d = 0; d + 7 <= days; d++) weekly.Add(perDay.Skip(d).Take(7).Sum());
                twoDays += perDay.Count(x => x >= 2);
                threeDays += perDay.Count(x => x >= 3);
                for (int i = 1; i < hours.Count; i++)
                {
                    int g = hours[i] - hours[i - 1];
                    gapList.Add(g);
                    gaps++;
                    if (g < 24) under24++;
                    if (g < 48) under48++;
                }
            }
            weekly.Sort();
            gapList.Sort();
            double P(List<int> l, double p) => l.Count == 0 ? 0 : l[Math.Clamp((int)(p * (l.Count - 1)), 0, l.Count - 1)];
            int runs = all.Length;
            Console.WriteLine($"Any 7 days ({what}): median {P(weekly, 0.5):0}, 1 week in 10 has {P(weekly, 0.9):0} or more, the busiest {weekly.LastOrDefault()}; "
                              + $"days with two or more: {twoDays / (double)runs:0.0} a year, three or more {threeDays / (double)runs:0.0}; "
                              + $"the gap to the next: median {P(gapList, 0.5) / 24.0:0.0} days, under a day {(gaps == 0 ? 0 : under24 / (double)gaps):P0}, under two days {(gaps == 0 ? 0 : under48 / (double)gaps):P0}.");
        }

        /// <summary>
        /// What an allowance would leave: gatherings and outings in time order; those the player chose (an answer, the
        /// calendar's day kept, a vote, a wedding, a life goal's toast) always happen and count; a seasonal custom isn't
        /// asked while the week's allowance is spent; the colony's own (a group calling its people, a family supper, a
        /// camp's show) waits for a gap of <paramref name="gap"/> hours since the last and room in the week, up to three
        /// days, else it is let go.
        /// </summary>
        private static void Allowance(List<Scene>[] all, int runs, float gap, int week)
        {
            var kept = new Dictionary<string, (int all, int kept)>();
            int total = 0, keptTotal = 0, twoDays = 0;
            var weekly = new List<int>();
            foreach (var run in all)
            {
                var list = run.Where(s => s.tier != "small").OrderBy(s => s.hour).ToList();
                var held = new List<int>();
                var waiting = new List<(Scene s, int until)>();
                int Week(int h) => held.Count(x => x > h - 7 * 24 && x <= h);
                bool Room(int h) => Week(h) < week && (held.Count == 0 || h - held[held.Count - 1] >= gap);
                void Note(Scene s, bool ok)
                {
                    var v = kept.GetValueOrDefault(s.source);
                    kept[s.source] = (v.all + 1, v.kept + (ok ? 1 : 0));
                    total++;
                    if (ok) keptTotal++;
                }
                int idx = 0;
                for (int h = 0; h < Colony.YearDays * 24; h++)
                {
                    for (int w = 0; w < waiting.Count; w++)
                    {
                        var (s, until) = waiting[w];
                        if (Room(h) && h % 24 >= 17 && h % 24 <= 20)
                        {
                            held.Add(h);
                            Note(s, true);
                            waiting.RemoveAt(w--);
                        }
                        else if (h >= until)
                        {
                            Note(s, false);
                            waiting.RemoveAt(w--);
                        }
                    }
                    while (idx < list.Count && list[idx].hour == h)
                    {
                        var s = list[idx++];
                        if (s.routine)
                        {
                            if (Room(h))
                            {
                                held.Add(h);
                                Note(s, true);
                            }
                            else waiting.Add((s, h + 72));
                        }
                        else if (s.custom && Week(h) >= week) Note(s, false);
                        else
                        {
                            held.Add(h);
                            Note(s, true);
                        }
                    }
                }
                var perDay = new int[Colony.YearDays];
                foreach (int h in held)
                    if (h / 24 < perDay.Length) perDay[h / 24]++;
                twoDays += perDay.Count(x => x >= 2);
                for (int d = 0; d + 7 <= Colony.YearDays; d++) weekly.Add(perDay.Skip(d).Take(7).Sum());
            }
            weekly.Sort();
            Console.WriteLine();
            Console.WriteLine($"With an allowance of {week} a week and {gap / 24f:0.#} days between (gatherings and outings): {keptTotal / (double)runs:0.0} of {total / (double)runs:0.0} a year happen "
                              + $"({keptTotal / (Colony.YearDays / 7.0) / runs:0.0} a week); any 7 days: median {weekly[weekly.Count / 2]}, the busiest {weekly.LastOrDefault()}; days with two or more {twoDays / (double)runs:0.0} a year.");
            Console.WriteLine("| Source | A year | Kept |");
            Console.WriteLine("|---|---|---|");
            foreach (var kv in kept.OrderByDescending(k => k.Value.all))
                Console.WriteLine($"| {kv.Key} | {kv.Value.all / (double)runs:0.0} | {(kv.Value.all == 0 ? 0 : kv.Value.kept / (double)kv.Value.all):P0} |");
        }
    }
}
