// What reaches the player in a day (--tell): the colony-year's stream of telling (the simulator's letters, messages and
// news lines, Sim.cs, and a message for each gathering or outing as it begins, Scenes.cs) told the old way and by the
// teller as built, with the colony's pace. Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --tell [repo root] [--runs N] [--colonists N] [--seed S]
// The old way: a cap of 20 messages and 6 letters that only tell a day for each kind of news and colony, every line of
// the smaller news in the evening's letter, a gathering's message for every scene. The teller (UI/TellMath.cs, linked:
// the very rules the game keeps): one count a day per colony (12 messages, 4 letters), a busy day (half the messages
// shown, or a big threat in the last six hours) when one person's news, health and the land's wait for the evening, the
// evening's letter carrying the twelve lines that weigh most and the rest going to the History tab; and the pace
// (PlayOut/StageMath.cs): only the scenes kept have a message, a day kept at supper a line in the evening's news. The
// simulator models only part of what Rimfolk tells (not the region, diplomacy, visitors or couriers), so the numbers are
// for the same stream before and after, not the game's whole day.

using System.Globalization;
using ColonyCouncil;

namespace Balance
{
    public static class TellingSim
    {
        /// <summary>One thing to tell: when, what kind of news, how much it matters, and the channel the system asked for.</summary>
        private struct Told
        {
            public int hour;
            public int kind;
            public int weight;
            public Channel channel;
        }

        /// <summary>The kind of news a simulated system's telling counts under (UI/TellMath.cs's numbers).</summary>
        private static int KindOf(string system)
        {
            switch (system)
            {
                case "people": case "stories": case "dreams": case "social events": return TellMath.KindPeople;
                case "health": case "ailments": return TellMath.KindHealth;
                case "world events": case "camps": case "encounters": return TellMath.KindWorld;
                default: return TellMath.KindColony;
            }
        }

        public static void Run(string[] args)
        {
            string root = ".";
            int runs = 200, n = 10, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--tell": break;
                    case "--runs": runs = int.Parse(args[++i]); break;
                    case "--colonists": n = int.Parse(args[++i]); break;
                    case "--seed": seed = int.Parse(args[++i]); break;
                    default: root = args[i]; break;
                }
            }
            var defs = new DefSet(root);
            var src = new SourceConstants(root);
            Code.Read(src);
            SceneConstants.Read(src);
            var set = new Settings(src.SettingDefaults());
            int week = (int)Math.Round(src.Num("the colony's own gatherings a week", "CouncilSettings.cs", @"public int gatheringsAWeek = (\d+);", StageMath.DefaultAWeek));
            Console.WriteLine($"What reaches the player in a day: {n} colonists, {runs} colony-years of {Colony.YearDays} days each, default settings.");
            Console.WriteLine();
            foreach (var style in Rates.Styles)
            {
                var old = new Tally();
                var now = new Tally();
                var lockObj = new object();
                Parallel.For(0, runs, r =>
                {
                    var c = new Colony(defs, set, style, n, seed * 7919 + r * 104729 + style.key.Length) { scenes = new List<Scene>() };
                    c.Run();
                    var kept = ScenesSim.KeepRun(c.scenes, week);
                    var stream = Stream(c, false, null);
                    var after = Stream(c, true, kept);
                    var a = Old(stream);
                    var b = Teller(after, c.threats);
                    lock (lockObj)
                    {
                        old.Add(a);
                        now.Add(b);
                    }
                });
                double days = runs * (double)Colony.YearDays;
                Console.WriteLine($"== {style.name} colony");
                Console.WriteLine("| A day | The old way | The teller and the pace |");
                Console.WriteLine("|---|---|---|");
                Console.WriteLine($"| Questions (never capped) | {old.questions / days:0.00} | {now.questions / days:0.00} |");
                Console.WriteLine($"| Letters that only tell | {old.letters / days:0.00} | {now.letters / days:0.00} |");
                Console.WriteLine($"| Messages | {old.messages / days:0.00} | {now.messages / days:0.00} |");
                Console.WriteLine($"| of them a gathering beginning | {old.sceneMessages / days:0.00} | {now.sceneMessages / days:0.00} |");
                Console.WriteLine($"| Letters and messages together | {(old.letters + old.messages) / days:0.00} | {(now.letters + now.messages) / days:0.00} |");
                Console.WriteLine($"| Lines in the evening's news | {old.evening / days:0.00} | {now.evening / days:0.00} |");
                Console.WriteLine($"| of them held back from the day | {old.held / days:0.00} | {now.held / days:0.00} |");
                Console.WriteLine($"| To the History tab only | {old.history / days:0.00} | {now.history / days:0.00} |");
                Console.WriteLine($"| The busiest day's letters and messages | {old.busiest} | {now.busiest} |");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// The colony-year's telling: its letters, messages and news lines (the simulator's play-out messages left out, as
        /// the scenes give them), and a message as each gathering or outing begins: every one the old way, or only those the
        /// pace keeps, with a line in the evening's news for a day kept at supper.
        /// </summary>
        private static List<Told> Stream(Colony c, bool teller, ScenesSim.Kept kept)
        {
            var list = new List<Told>();
            foreach (var l in c.letters)
            {
                if (l.system == "play-outs" && l.channel == Channel.Message) continue;
                int kind = KindOf(l.system);
                // A serious illness's letter is hard to miss; the rest are worth knowing.
                int weight = l.system == "health" && l.channel == Channel.Letter ? TellWeight.Important : TellWeight.Notable;
                list.Add(new Told { hour = l.hour, kind = kind, weight = weight, channel = l.channel });
            }
            var begins = teller ? kept.held : c.scenes.Where(s => s.tier != "small").Select(s => s.hour).ToList();
            foreach (int h in begins) list.Add(new Told { hour = h, kind = -1, weight = TellWeight.Notable, channel = Channel.Message });
            if (teller)
                foreach (int h in kept.supper)
                    list.Add(new Told { hour = h, kind = TellMath.KindColony, weight = TellWeight.Notable, channel = Channel.Digest });
            list.Sort((x, y) => x.hour.CompareTo(y.hour));
            return list;
        }

        private sealed class Tally
        {
            public long questions, letters, messages, sceneMessages, evening, held, history;
            public int busiest;

            public void Add(Tally t)
            {
                questions += t.questions;
                letters += t.letters;
                messages += t.messages;
                sceneMessages += t.sceneMessages;
                evening += t.evening;
                held += t.held;
                history += t.history;
                busiest = Math.Max(busiest, t.busiest);
            }
        }

        /// <summary>The old way: 20 messages and 6 letters a day per kind of news, every line of the smaller news in the evening's letter.</summary>
        private static Tally Old(List<Told> stream)
        {
            var t = new Tally();
            var count = new Dictionary<(int day, int kind, bool letter), int>();
            var perDay = new Dictionary<int, int>();
            foreach (var x in stream)
            {
                int day = x.hour / 24;
                int kind = x.kind < 0 ? TellMath.KindColony : x.kind;
                switch (x.channel)
                {
                    case Channel.Decision:
                        t.questions++;
                        continue;
                    case Channel.Digest:
                        t.evening++;
                        continue;
                }
                bool letter = x.channel == Channel.Letter || x.channel == Channel.Request;
                var key = (day, kind, letter);
                count.TryGetValue(key, out int c);
                int cap = letter ? 6 : 20;
                if (x.weight < TellWeight.Important && c >= cap)
                {
                    t.evening++;
                    t.held++;
                    continue;
                }
                count[key] = c + 1;
                if (letter) t.letters++;
                else
                {
                    t.messages++;
                    if (x.kind < 0) t.sceneMessages++;
                }
                perDay.TryGetValue(day, out int d);
                perDay[day] = d + 1;
            }
            t.busiest = perDay.Count == 0 ? 0 : perDay.Values.Max();
            return t;
        }

        /// <summary>The teller as built (UI/TellMath.cs): the colony's day, a busy day, the evening's letter.</summary>
        private static Tally Teller(List<Told> stream, List<int> threats)
        {
            var t = new Tally();
            var book = new TellBook();
            const int colony = 1, H = Sched.HourTicks;
            var evening = new Dictionary<int, List<int>>();
            var perDay = new Dictionary<int, int>();
            var raids = threats.OrderBy(h => h).ToList();
            int nextRaid = 0, nextEvening = 18;
            void Evening(int day, int score)
            {
                if (!evening.TryGetValue(day, out var l)) evening[day] = l = new List<int>();
                l.Add(score);
            }
            void Close(int day, int shown)
            {
                if (!evening.TryGetValue(day, out var l)) return;
                var keep = TellMath.EveningKeep(l, TellMath.EveningRoom(shown));
                int k = keep.Count(b => b);
                t.evening += k;
                t.history += l.Count - k;
                evening.Remove(day);
            }
            foreach (var x in stream)
            {
                int now = x.hour * H;
                while (nextRaid < raids.Count && raids[nextRaid] <= x.hour) book.Important(colony, raids[nextRaid++] * H, now);
                // After six in the evening, the next evening's letter.
                int day = x.hour / 24 + (x.hour % 24 >= 18 ? 1 : 0);
                int kind = x.kind < 0 ? TellMath.KindColony : x.kind;
                // The evening's letter goes at six: its room is how busy the day was by then.
                while (nextEvening <= x.hour)
                {
                    Close(nextEvening / 24, book.ShownToday(nextEvening * H));
                    nextEvening += 24;
                }
                switch (x.channel)
                {
                    case Channel.Decision:
                        t.questions++;
                        book.Asked(now);
                        continue;
                    case Channel.Digest:
                        Evening(day, TellMath.EveningScore(x.weight, false, kind));
                        continue;
                }
                bool letter = x.channel == Channel.Letter || x.channel == Channel.Request;
                var v = book.Judge(null, colony, kind, letter, x.weight, letter ? TellMath.DefaultLettersADay : TellMath.DefaultMessagesADay, now);
                if (v == TellVerdict.Held)
                {
                    t.held++;
                    Evening(day, TellMath.EveningScore(x.weight, true, kind));
                    continue;
                }
                if (letter) t.letters++;
                else
                {
                    t.messages++;
                    if (x.kind < 0) t.sceneMessages++;
                }
                perDay.TryGetValue(x.hour / 24, out int d);
                perDay[x.hour / 24] = d + 1;
            }
            foreach (int day in evening.Keys.ToList()) Close(day, 0);
            t.busiest = perDay.Count == 0 ? 0 : perDay.Values.Max();
            return t;
        }
    }
}
