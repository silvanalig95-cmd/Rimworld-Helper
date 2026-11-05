// How much acts against their nature (Diplomacy/Nature.cs, release 2d.2) add to the heart's strain over a colony-year,
// beside everything else that builds it. Not a target: a check that the acts are felt (they show on the heart's page, a
// few points each) but never the main source of strain or of breaks (the heart's episodes). Printed, nothing written.
//
// The heart is played hour by hour as HeartLogic does it, with the mod's own numbers (Heart/HeartMath.cs: each source's
// daily cap, the day's cap, the episodes' odds and relief; Diplomacy/NatureMath.cs: what each act brings whom, scaled as
// the pressures' bad side by Pressures/PressureMath.cs). The rest of a colonist's life is modelled roughly per play style
// (fights, deaths, bad nights, hunger, no free time, low spirits, decrees; sleep, quiet hours, recreation, gatherings and
// talks easing it) and set so the time spent strained, worn and near breaking matches the colony-year report's shares for
// each style (Rates.cs: Strained -3/-5/-8). Natures come from the vanilla traits as Rimfolk_PersonaNature.xml sets them,
// each trait about one colonist in twenty, the social skill for warmth, and a personal lean of up to 12 either way. The
// acts come at a colony's rates per year when it is deep in diplomacy ("busy", on the generous side) and at half that.
//
//   dotnet run -c Release --project Tools/Balance -- --nature [--runs N] [--seed S]

using ColonyCouncil;

namespace Balance
{
    public static class NatureSim
    {
        private const int Days = 60;
        private const int HourTicks = 2500, DayTicks = 60000;

        /// <summary>A play style's everyday life for the heart: chances a day for each colonist (or the colony), and how well they rest.</summary>
        private sealed class Life
        {
            public string key, name;
            public float raid, deathsPerYear, badNight, hungry, joyless, exhausted, lowMood, dark, decree, party, talk, comfy, mood;
            /// <summary>A hard spell (a few days of bad nights, low spirits, no free time and a need of the heart missing) starting for a colonist on a given day, and how long.</summary>
            public float spell, spellDays;
            public float[] shares;
        }

        // The chances are set so the time strained, worn and near breaking comes near the colony-year report's shares for each
        // style (2/0/0, 8/2/0 and 20/8/2 per cent): with these, 2.2/0.2/0, 7.8/2.1/0.4 and 18/8/2.5.
        private static readonly Life[] Lives =
        {
            new Life
            {
                key = "well", name = "well-run", raid = 0.1f, deathsPerYear = 0.6f, badNight = 0.04f, hungry = 0.01f, joyless = 0.01f, exhausted = 0.01f, lowMood = 0.01f,
                dark = 0.01f, decree = 0.05f, party = 0.12f, talk = 0.6f, comfy = 0.97f, mood = 1.35f, spell = 0.035f, spellDays = 6f, shares = Rates.WellRun.strain
            },
            new Life
            {
                key = "avg", name = "average", raid = 0.12f, deathsPerYear = 1.6f, badNight = 0.2f, hungry = 0.05f, joyless = 0.08f, exhausted = 0.05f, lowMood = 0.06f,
                dark = 0.1f, decree = 0.15f, party = 0.07f, talk = 0.45f, comfy = 0.8f, mood = 1.1f, spell = 0.03f, spellDays = 7f, shares = Rates.Average.strain
            },
            new Life
            {
                key = "neglect", name = "neglectful", raid = 0.14f, deathsPerYear = 3.5f, badNight = 0.35f, hungry = 0.08f, joyless = 0.2f, exhausted = 0.1f, lowMood = 0.12f,
                dark = 0.2f, decree = 0.3f, party = 0.02f, talk = 0.25f, comfy = 0.6f, mood = 0.95f, spell = 0.035f, spellDays = 5f, shares = Rates.Neglectful.strain
            }
        };

        /// <summary>The acts a year for a colony deep in diplomacy (every release of stage 2 at once, on the generous side): how often, and who.</summary>
        private static readonly (NatureAct act, float perYear, string who)[] Acts =
        {
            (NatureAct.SworeFealty, 0.5f, "speaker"),
            (NatureAct.BrokeTruce, 0.3f, "anyone"),
            (NatureAct.BrokeTruce, 0.2f, "speaker"),
            (NatureAct.GivenHostage, 0.4f, "hostage"),
            (NatureAct.WardSent, 0.4f, "parents"),
            (NatureAct.MatchAgainstHeart, 0.3f, "anyone"),
            (NatureAct.AcceptedChallenge, 1.2f, "champion"),
            (NatureAct.RefusedChallenge, 0.4f, "champion"),
            (NatureAct.SeasonAway, 0.8f, "anyone"),
            (NatureAct.HaggledForKin, 0.8f, "speaker"),
            (NatureAct.TruceWithFoe, 0.2f, "anyone")
        };

        // Where the vanilla traits set each side (Rimfolk_PersonaNature.xml), each trait one colonist in twenty.
        private static readonly (PersonaAxis axis, float push)[] TraitPushes =
        {
            (PersonaAxis.Warmth, 50f), (PersonaAxis.Warmth, -60f), (PersonaAxis.Warmth, -25f), (PersonaAxis.Nerve, 25f), (PersonaAxis.Nerve, 25f),
            (PersonaAxis.Nerve, -40f), (PersonaAxis.Nerve, 30f), (PersonaAxis.Nerve, -15f), (PersonaAxis.Temper, 30f), (PersonaAxis.Temper, 35f),
            (PersonaAxis.Temper, 20f), (PersonaAxis.Temper, -30f), (PersonaAxis.Trust, 15f), (PersonaAxis.Trust, -20f), (PersonaAxis.Trust, -15f),
            (PersonaAxis.Pride, 15f), (PersonaAxis.Pride, -25f), (PersonaAxis.Pride, 20f), (PersonaAxis.Pride, 25f), (PersonaAxis.Pride, 20f),
            (PersonaAxis.Openness, 35f), (PersonaAxis.Openness, -25f), (PersonaAxis.Openness, 25f), (PersonaAxis.Openness, 25f), (PersonaAxis.Openness, -10f)
        };

        private sealed class Person
        {
            public float[] sides = new float[PersonaMath.Axes];
            public float strain, todayTotal;
            public float[] recent = new float[HeartMath.SourceCount], today = new float[HeartMath.SourceCount];
            public int lastEpisode = -999999, natureTick = -1, kin = -1, spellUntil = -1;
            public float lowMoodHours;
            public float natureBuilt;
        }

        private sealed class Tally
        {
            public int runs, colonists, episodes, episodesNatureTop, episodesWithNatureRecent, acts, felt, eased, feltBy, speakerRuns;
            public float natureBuilt, natureEased, maxAct, speakerNature, speakerAll, mostOne;
            public readonly float[] built = new float[HeartMath.SourceCount];
            public readonly float[] bandHours = new float[4];
            public float hours;
        }

        private static Random rng;

        private static bool Chance(float p) => rng.NextDouble() < p;

        private static float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public static void Run(string[] args)
        {
            int runs = 400, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            Console.WriteLine($"Against their nature: a colony-year ({Days} days), {runs} runs each, the \"Life's ups and downs\" setting on normal.");
            Console.WriteLine($"An act brings up to {NatureMath.MaxStrain:0} strain ({NatureMath.AlwaysStrain:0} at least for what weighs on anyone, {NatureMath.MaxStrain * NatureMath.Floor:0} just past where a side shows), up to {NatureMath.MaxRelief:0} relief; one a day; the heart's caps {HeartMath.SourceCap(StrainSource.Nature):0} for the source and {HeartMath.DailyCap:0} a day for everything.");
            Console.WriteLine();
            foreach (var life in Lives)
                foreach (int n in new[] { 6, 12 })
                {
                    var none = Play(life, n, 0f, runs, seed);
                    foreach (float rate in new[] { 0.5f, 1f })
                    {
                        var t = Play(life, n, rate, runs, seed);
                        Report(life, n, rate, t, none);
                    }
                }
        }

        private static Tally Play(Life life, int n, float rate, int runs, int seed)
        {
            var t = new Tally();
            for (int r = 0; r < runs; r++)
            {
                // The same lives with and without the acts: only the acts draw from their own generator.
                rng = new Random(seed * 7919 + r * 31 + n);
                var acts = new Random(seed * 104729 + r * 17 + n);
                Year(life, n, rate, t, acts);
            }
            return t;
        }

        private static Person Make()
        {
            var p = new Person();
            for (int i = 0; i < PersonaMath.Axes; i++) p.sides[i] = Range(-12f, 12f);
            foreach (var (axis, push) in TraitPushes)
                if (Chance(0.05f))
                    p.sides[(int)axis] += push;
            // The social skill shows in warmth (level 0 to 15, a passion now and then).
            int social = rng.Next(0, 16);
            p.sides[(int)PersonaAxis.Warmth] += (social - 6) * 1.5f + (Chance(0.3f) ? 5f : 0f);
            for (int i = 0; i < PersonaMath.Axes; i++) p.sides[i] = Math.Max(-100f, Math.Min(100f, p.sides[i]));
            return p;
        }

        private static void Year(Life life, int n, float rate, Tally t, Random actRng)
        {
            var people = new List<Person>();
            for (int i = 0; i < n; i++) people.Add(Make());
            // About two in five have close kin among the colony.
            for (int i = 0; i + 1 < n; i += 2)
                if (Chance(0.4f))
                {
                    people[i].kin = i + 1;
                    people[i + 1].kin = i;
                }
            int speaker = 0;
            int lastColonyEpisode = -999999;
            t.runs++;
            t.colonists += n;
            // The day's events, each at an hour.
            var builds = new List<(int person, int hour, StrainSource source, float amount)>();
            var natureToday = new List<(int person, int hour, NatureAct act)>();
            for (int day = 0; day < Days; day++)
            {
                builds.Clear();
                natureToday.Clear();
                foreach (var p in people)
                {
                    Array.Clear(p.today, 0, p.today.Length);
                    p.todayTotal = 0f;
                }
                if (Chance(life.raid))
                {
                    int hour = rng.Next(6, 20);
                    for (int i = 0; i < n; i++)
                    {
                        if (!Chance(0.8f)) continue;
                        for (int h = 0; h < 3; h++) builds.Add((i, hour + h, StrainSource.Combat, 4f));
                        builds.Add((i, hour + 1, StrainSource.Fear, 2f));
                        if (Chance(0.3f)) builds.Add((i, hour + 2, StrainSource.Pain, Range(2f, 6f)));
                        if (Chance(0.08f)) builds.Add((i, hour + 2, StrainSource.Downed, 8f));
                    }
                }
                if (Chance(life.deathsPerYear / Days))
                {
                    int hour = rng.Next(0, 24);
                    for (int i = 0; i < n; i++)
                    {
                        builds.Add((i, hour, StrainSource.Loss, 4f + (Chance(0.3f) ? Range(4f, 8f) : 0f)));
                        for (int g = 1; g <= 3 && hour + g * 6 < 24; g++) builds.Add((i, hour + g * 6, StrainSource.Grief, 1f));
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    var q = people[i];
                    if (q.spellUntil < day && Chance(life.spell)) q.spellUntil = day + (int)Range(3f, 2f * life.spellDays - 3f);
                    if (q.spellUntil >= day)
                    {
                        // A hard spell: bad nights, low spirits, no free time, a need of the heart missing for days.
                        if (Chance(0.7f)) builds.Add((i, 6, StrainSource.BadSleep, Range(2f, 4f)));
                        if (Chance(0.6f)) builds.Add((i, 16, StrainSource.LowMood, Range(4f, 6f)));
                        if (Chance(0.5f)) builds.Add((i, 18, StrainSource.Joyless, 3f));
                        if (Chance(0.6f)) builds.Add((i, 20, StrainSource.Unmet, 3f));
                    }
                    if (Chance(life.badNight)) builds.Add((i, 6, StrainSource.BadSleep, Range(1f, 2f)));
                    if (Chance(life.hungry)) builds.Add((i, 14, StrainSource.Hunger, 4f));
                    if (Chance(life.joyless)) builds.Add((i, 18, StrainSource.Joyless, 3f));
                    if (Chance(life.exhausted)) builds.Add((i, 21, StrainSource.Exhaustion, 3f));
                    if (Chance(life.lowMood)) builds.Add((i, 16, StrainSource.LowMood, Range(3f, 6f)));
                    if (Chance(life.dark)) builds.Add((i, 22, StrainSource.Dark, 2f));
                    if (Chance(life.decree)) builds.Add((i, 12, StrainSource.Council, 1f));
                }
                if (rate > 0f)
                    foreach (var (act, perYear, who) in Acts)
                    {
                        if (actRng.NextDouble() >= perYear * rate / Days) continue;
                        int hour = actRng.Next(8, 20);
                        switch (who)
                        {
                            case "speaker":
                                natureToday.Add((speaker, hour, act));
                                break;
                            case "champion":
                                natureToday.Add((actRng.Next(0, Math.Min(n, 9)), hour, act));
                                break;
                            case "hostage":
                            {
                                int h = actRng.Next(0, n);
                                natureToday.Add((h, hour, act));
                                if (people[h].kin >= 0) natureToday.Add((people[h].kin, hour, NatureAct.KinHostage));
                                break;
                            }
                            case "parents":
                                // A youth with parents among the colony about half the time: one or two of them.
                                if (actRng.NextDouble() < 0.5)
                                {
                                    int a = actRng.Next(0, n);
                                    natureToday.Add((a, hour, act));
                                    if (people[a].kin >= 0) natureToday.Add((people[a].kin, hour, act));
                                }
                                break;
                            default:
                                natureToday.Add((actRng.Next(0, n), hour, act));
                                break;
                        }
                    }
                bool party = Chance(life.party);
                for (int hour = 0; hour < 24; hour++)
                {
                    int now = day * DayTicks + hour * HourTicks;
                    for (int i = 0; i < n; i++)
                    {
                        var p = people[i];
                        foreach (var b in builds)
                            if (b.person == i && b.hour == hour)
                                Build(p, b.source, b.amount, t);
                        foreach (var a in natureToday)
                            if (a.person == i && a.hour == hour)
                                Nature(p, a.act, now, t, a.act == NatureAct.GivenHostage);
                        // Relief: sleep, quiet hours, recreation, a gathering, a good talk.
                        float relief;
                        float mood = p.spellUntil >= day ? 0.7f : life.mood;
                        if (hour < 6 || hour >= 22) relief = (Chance(life.comfy) ? 0.6f : 0.25f) * mood;
                        else if (hour >= 19) relief = 0.5f * mood;
                        else relief = 0.15f * mood;
                        if (party && hour == 20) relief += 6f;
                        if (hour == 13 && Chance(life.talk)) relief += 2f;
                        p.strain = HeartMath.Clamp(p.strain - relief);
                        for (int s = 0; s < p.recent.Length; s++) p.recent[s] *= HeartMath.RecentDecayPerHour;
                        // The heart's episode, its own odds and waits (the roll made every hour, so the lives with and without the acts draw alike).
                        bool roll = Chance(HeartMath.EpisodeChancePerHour(p.strain));
                        if (roll && now - p.lastEpisode >= 10 * DayTicks && now - lastColonyEpisode >= 2 * DayTicks)
                        {
                            t.episodes++;
                            int top = 0;
                            for (int s = 1; s < p.recent.Length; s++)
                                if (p.recent[s] > p.recent[top])
                                    top = s;
                            if (top == (int)StrainSource.Nature) t.episodesNatureTop++;
                            if (p.recent[(int)StrainSource.Nature] >= 1f) t.episodesWithNatureRecent++;
                            p.strain = HeartMath.Clamp(p.strain - HeartMath.EpisodeRelief);
                            p.lastEpisode = now;
                            lastColonyEpisode = now;
                        }
                        t.bandHours[(int)HeartMath.BandFor(p.strain)]++;
                        t.hours++;
                    }
                }
            }
            foreach (var p in people)
            {
                if (p.natureBuilt > 0f) t.feltBy++;
                t.mostOne = Math.Max(t.mostOne, p.natureBuilt);
            }
            t.speakerRuns++;
            t.speakerNature += people[speaker].natureBuilt;
        }

        private static float Build(Person p, StrainSource s, float raw, Tally t)
        {
            int i = (int)s;
            float today = p.today[i];
            float added = HeartMath.Add(raw, 1f, 1f, ref today, HeartMath.SourceCap(s), ref p.todayTotal, HeartMath.DailyCap);
            p.today[i] = today;
            p.strain = HeartMath.Clamp(p.strain + added);
            p.recent[i] += added;
            t.built[i] += added;
            return added;
        }

        private static void Nature(Person p, NatureAct act, int now, Tally t, bool hostage)
        {
            if (!NatureMath.MayFeel(p.natureTick, now)) return;
            var acts = hostage ? new[] { NatureAct.GivenHostage, NatureAct.SeasonAway } : new[] { act };
            float felt = NatureMath.Pick(acts, p.sides, out _);
            t.acts++;
            if (felt == 0f) return;
            if (felt > 0f)
            {
                // Hard times: the model keeps no mood, so a worn colonist stands for one near breaking.
                float hard = p.strain >= 55f ? 0.5f : 0f;
                float added = Build(p, StrainSource.Nature, NatureMath.Scale(felt, LifePressure.Normal, hard), t);
                if (added <= 0f) return;
                p.natureBuilt += added;
                t.natureBuilt += added;
                t.felt++;
                t.maxAct = Math.Max(t.maxAct, added);
            }
            else
            {
                float eased = Math.Min(-felt, p.strain);
                if (eased <= 0f) return;
                p.strain -= eased;
                t.natureEased += eased;
                t.eased++;
            }
            p.natureTick = now;
        }

        private static void Report(Life life, int n, float rate, Tally t, Tally none)
        {
            float years = Math.Max(1, t.colonists);
            float all = t.built.Sum();
            string Band(Tally x, int b) => (x.bandHours[b] / Math.Max(1f, x.hours)).ToString("P1");
            Console.WriteLine($"{life.name}, {n} colonists, diplomacy {(rate >= 1f ? "busy" : "at half that")}:");
            Console.WriteLine($"  time strained / worn / near breaking: {Band(t, 1)} / {Band(t, 2)} / {Band(t, 3)}  (without the acts {Band(none, 1)} / {Band(none, 2)} / {Band(none, 3)}; the report's style {life.shares[0]:P0} / {life.shares[1]:P0} / {life.shares[2]:P0})");
            Console.WriteLine($"  strain built a colonist-year: {all / years:0.0}, of it against their nature {t.natureBuilt / years:0.00} ({t.natureBuilt / Math.Max(1f, all):P1}); eased by acts that suit them {t.natureEased / years:0.00}");
            var top = Enumerable.Range(0, HeartMath.SourceCount).OrderByDescending(i => t.built[i]).Take(5).Select(i => $"{(StrainSource)i} {t.built[i] / Math.Max(1f, all):P0}");
            Console.WriteLine($"  the main sources: {string.Join(", ", top)}; against their nature ranks {Enumerable.Range(0, HeartMath.SourceCount).OrderByDescending(i => t.built[i]).ToList().IndexOf((int)StrainSource.Nature) + 1} of {HeartMath.SourceCount}");
            float each = t.natureBuilt / Math.Max(1, t.felt);
            Console.WriteLine($"  acts a colony-year: {t.acts / (float)t.runs:0.0} ({t.felt / (float)t.runs:0.0} felt, {t.eased / (float)t.runs:0.00} eased); colonists who felt one: {t.feltBy / years:P0}; the speaker's own a year: {t.speakerNature / Math.Max(1, t.speakerRuns):0.0}");
            Console.WriteLine($"  an act felt adds {each:0.0} on average (on the heart's page about {2.0 * Math.Log(Math.Max(1f, each), 2):0.0} days), {t.maxAct:0.0} at most; the most one colonist took from it in a year: {t.mostOne:0.0}");
            Console.WriteLine($"  episodes a colonist-year: {t.episodes / years:0.000} (without the acts {none.episodes / years:0.000}); with an act on the heart's page {t.episodesWithNatureRecent / (float)Math.Max(1, t.episodes):P0} of them, the act the main cause in {t.episodesNatureTop / (float)Math.Max(1, t.episodes):P1}");
            Console.WriteLine();
        }
    }
}
