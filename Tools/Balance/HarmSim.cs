// Hurts from everyday life over a colony-year (Harm/, DESIGN.md "Harm from everyday life"). Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --harm [--runs N] [--seed S]
//
// How often each of Rimfolk's sources hurts one of the colony, through the shared cap (one Rimfolk-made hurt in three
// days each), beside the game's own everyday hurts; the fights Rimfolk starts; and what the hurts and the fights add to
// the heart's strain and its mood. What a source does once it comes (one bout in 25 landing for real, the game's own
// revenge chance, one journey in six bringing a sickness home, a frayed temper's factor) is the mod's own arithmetic,
// compiled in as it is (Harm/HarmMath.cs, Harm/HarmTwoMath.cs, Heart/HeartMath.cs). How often a colony meets each (a
// drills decree, a camp at high tension, a lone boar among a hunt's game, a journey to a sick town, a bitter rivalry) is
// the table below, each an estimate that says where it comes from. "Before" is the same colonies with harm, parts 2 and 3
// left out: part 1's hurts run the same code on 0.131.0 (HarmMath.cs is unchanged since). The heart is played hour by hour
// as the colony-year plays it (HeartSim.cs, the style's own life), each hurt its pain (as a raid's wound is) and each
// fight an hour of combat and a bruise's pain besides; every source draws from its own generator, so the lives and part
// 1's hurts are the same with and without parts 2 and 3. Grown-ups only (the cap keeps children from all but the
// mildest), the usual setting and storyteller (the harm scale 1).

using ColonyCouncil;

namespace Balance
{
    public static class HarmSim
    {
        private const int Days = HeartState.Days;

        /// <summary>A play style's exposure to each source over a colony-year of 60 days (estimates; where each comes from is beside it).</summary>
        private sealed class Exposure
        {
            public string key;
            // ---- Part 1 (0.106.0) ----
            /// <summary>Days a drills decree holds (the militia's in a tribal or medieval colony, the drills after): a five-day decree in most years.</summary>
            public float drillDays;
            /// <summary>Days a work decree holds (long hours, a harvest push, a building season), and the share of colonists under 30% rest then.</summary>
            public float workDays, lowRest;
            /// <summary>A strained back or a sprained ankle per colonist-year (the report's ailments: about 0.4 strained-back days, four days each), and the share worked hard through untended.</summary>
            public float strains, workedThrough;
            /// <summary>Dangers on the map a colony-year (six of Rimfolk's 140-odd events at their share of the pool), and the share that end in a hurt by the answer chosen.</summary>
            public float dangers, dangerHurt;
            /// <summary>The ailments of the land and the work that are Rimfolk-made hurts, per colonist-year in a temperate colony (a swim's itch, rabbit fever, a smith's fume fever, trench foot, the serious three now and then).</summary>
            public float landAilments;
            // ---- Parts 2 and 3 ----
            /// <summary>Share of camps whose tension reaches 70 (takings and arrests; most stay under 40), and of camps with the old quarrel answered badly.</summary>
            public float campHigh, campFeud;
            /// <summary>Journeys to another people's town a colony-year, the share with medicine packed, and with the quarantine decree for those back.</summary>
            public float journeys, medicine, quarantine;
            /// <summary>Hunts a colony-year: answers' hunts and hunting parties (the calendar's hunting day is beside them).</summary>
            public float hunts;
            /// <summary>Rimfolk's own sharp words with a fight's chance a colony of six meets in a year (a near fight, an answer's fight at its chance), and hard words with an outsider.</summary>
            public float sharpWords, hardWords;
        }

        private static readonly Exposure[] Exposures =
        {
            new Exposure
            {
                key = "well", drillDays = 3f, workDays = 5f, lowRest = 0.05f, strains = 0.12f, workedThrough = 0.15f, dangers = 0.4f, dangerHurt = 0.2f, landAilments = 0.12f,
                campHigh = 0.04f, campFeud = 0.03f, journeys = 1.5f, medicine = 0.8f, quarantine = 0.15f, hunts = 1.2f, sharpWords = 1f, hardWords = 0.5f
            },
            new Exposure
            {
                key = "avg", drillDays = 5f, workDays = 10f, lowRest = 0.15f, strains = 0.15f, workedThrough = 0.35f, dangers = 0.4f, dangerHurt = 0.3f, landAilments = 0.18f,
                campHigh = 0.12f, campFeud = 0.05f, journeys = 1.5f, medicine = 0.5f, quarantine = 0.05f, hunts = 1.5f, sharpWords = 1.5f, hardWords = 0.6f
            },
            new Exposure
            {
                key = "neglect", drillDays = 5f, workDays = 15f, lowRest = 0.35f, strains = 0.2f, workedThrough = 0.6f, dangers = 0.4f, dangerHurt = 0.45f, landAilments = 0.25f,
                campHigh = 0.25f, campFeud = 0.08f, journeys = 1f, medicine = 0.2f, quarantine = 0f, hunts = 1.2f, sharpWords = 2.5f, hardWords = 0.6f
            }
        };

        // ---- The same in every style ----

        /// <summary>Camps a colony-year (Camps/CampTimer.cs: 5% each open morning once 15 days have passed since the last; stays of 4 to 12 days).</summary>
        private const float CampsPerYear = 1.4f;
        /// <summary>Evenings a high-tension camp spends at 70 or more before it goes.</summary>
        private const int HighEveningsMin = 1, HighEveningsMax = 4;
        /// <summary>Someone of the colony at their fire of an evening (and the deeper hooks on, so the colony's side can come to blows).</summary>
        private const float ColonyAtFire = 0.6f;
        /// <summary>Another of the colony at the fire besides, each (who may join), and theirs free to join.</summary>
        private const float OthersAtFire = 0.08f;
        private const int TheirsFreeMin = 1, TheirsFreeMax = 4;
        /// <summary>A peacemaker in office and at home (Offices/).</summary>
        private const float Peacemaker = 0.4f;
        /// <summary>
        /// A journey's town under a plague or a famine (its people's, or one within twenty tiles): the region's world events
        /// come 1.0 to 1.2 a year, about a third of the plague, hunger or drought kinds, two or three weeks each, near one or
        /// two of the region's few towns. The plague share of those.
        /// </summary>
        private const float SickTown = 0.12f, PlagueShare = 0.5f;
        /// <summary>Hours a caravan stops at the town: a trade at the gate (under two), an afternoon, a night or more.</summary>
        private const float QuickShare = 0.4f, AfternoonShare = 0.35f;
        /// <summary>Industrial or spacer medicine (half of games).</summary>
        private const float Modern = 0.5f;
        /// <summary>Travellers on a journey.</summary>
        private const int TravellersMin = 2, TravellersMax = 4;
        /// <summary>A travelling champion's challenge a colony-year (and a rematch), and the share that are a bout of fighting (one skill of nine, more for a fighter known by name).</summary>
        private const float Challenges = 0.6f, BoutShare = 0.15f;
        /// <summary>The calendar's hunting day kept (a colony that put one on its calendar).</summary>
        private const float HuntingDay = 0.3f;
        /// <summary>A lone head of game that can turn among a hunt's (boars, elk and muffalo come in herds as often as alone).</summary>
        private const float LoneTurner = 0.35f;
        /// <summary>
        /// The game's own revenge chance for such a head (its kind's, 0.2 to 0.5 by the storyteller's factor), the game's
        /// distance factor at a hunter's range (3 at point blank to 1 at 30 cells: about 1.3 at 20 to 25), and the hits it
        /// takes before it's down, each a roll.
        /// </summary>
        private const float RevengeMin = 0.2f, RevengeMax = 0.5f, RangeFactor = 1.3f;
        private const int HitsMin = 2, HitsMax = 4;
        /// <summary>A turned beast wounds its hunter before it's shot down.</summary>
        private const float TurnedWounds = 0.65f;
        /// <summary>A pair of the colony who are rivals (Rates.RivalShare), bitterly (50 or more), and a petty or abrasive nature on one side (abrasive or jealous, not kind).</summary>
        private const float Bitter = 0.3f, Petty = 0.13f;
        /// <summary>The two at the same craft; a bench of the other's craft with its bills; the act done within its day; someone sees it; the two meet to have it out.</summary>
        private const float SameWork = 0.3f, Bench = 0.8f, Acts = 0.8f, Witness = 0.3f, Confronts = 0.9f;
        /// <summary>The election's hustings held (danger and weather call them off), heckles at them, the heckler backing another, the best-behavior decree, calm candidates.</summary>
        private const float HustingsHeld = 0.8f, BacksOther = 0.7f, Manners = 0.05f, CalmCandidates = 0.5f;
        private const int Heckles = 2;
        /// <summary>A sharp word's own chance of blows (a near fight's one in four, an answer's 0.2 to 0.5), and hard words with an outsider's.</summary>
        private const float SharpChance = 0.3f, HardChance = 0.2f;

        // ---- The game's own, beside them ----

        /// <summary>A raid's wound for a colonist at home (HeartSim's model: in it four times in five, hurt one time in three of those).</summary>
        private const float RaidWound = 0.8f * 0.3f;

        private static readonly string[] PartOne = { "drills (1.2.1)", "tired hands (1.2.2)", "working through it (3.2.1)", "dangers on the map", "ailments of the land and the work" };

        private static readonly string[] PartTwo =
        {
            "a camp's brawl (2.4.1)", "sickness brought home (3.1.1)", "the champion's bout (2.2.1)", "hunts that turn (2.1.1)", "a rival's sabotage (3.5.1)",
            "the hustings' blows (1.4.1)", "frayed tempers (3.3.1)"
        };

        private sealed class Person
        {
            public readonly HeartState heart = new HeartState();
            public int lastHurt = int.MinValue / 2;
            public bool tough;
        }

        private sealed class Tally
        {
            public int runs, colonists;
            public readonly Dictionary<string, double> hurts = new Dictionary<string, double>(), barred = new Dictionary<string, double>(), fights = new Dictionary<string, double>();
            public double wounds, hours, episodes, mood, raidWounds;
            public readonly double[] bands = new double[4];
        }

        private sealed class Year
        {
            public Exposure x;
            public HeartLife life;
            public int n, hour, lastColonyEpisode = -999999;
            public bool after;
            public List<Person> people;
            public Tally t;
            /// <summary>Each source's own generator: the lives and part 1 draw the same with parts 2 and 3 on or off.</summary>
            public Dictionary<string, Random> rngs;
            /// <summary>The day's events, by hour.</summary>
            public readonly List<(int hour, Action act)> today = new List<(int, Action)>();
            // A camp under way: the days it stays, its quarrelling evenings, a brawl already this stay.
            public int campUntil = -1, campGap = -1;
            public HashSet<int> quarrelDays = new HashSet<int>();
            public bool campBrawled;
            // Bitter rivalries one way (the rival, the other, petty, at the same work) and the colony's peacemaker in office.
            public List<(Person rival, Person other, bool petty, bool same)> rivals = new List<(Person, Person, bool, bool)>();
            public bool peacemaker;
            public int electionDay;
            // Sicknesses carried home: who, and the day it shows (a lapse past three more days held back).
            public List<(Person p, int day)> onsets = new List<(Person, int)>();
        }

        public static void Run(string[] args)
        {
            int runs = 400, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            Console.WriteLine($"Hurts from everyday life: a colony-year ({Days} days), {runs} runs each, the setting at 100% and the usual storyteller (harm scale 1).");
            Console.WriteLine("Hurts are what the shared cap counts (a wound, an ailment, a fight's two, a bout's real blow, a hunt's game turning on its hunter),");
            Console.WriteLine($"per colonist-year; the cap holds one back if they had one in the last {HarmMath.GapDays:0} days. \"Before\": parts 2 and 3 left out (as on 0.131.0).");
            Console.WriteLine();
            foreach (var x in Exposures)
                foreach (int n in new[] { 6, 12 })
                {
                    var before = Play(x, n, false, runs, seed);
                    var after = Play(x, n, true, runs, seed);
                    Report(x, n, before, after);
                }
        }

        private static Tally Play(Exposure x, int n, bool after, int runs, int seed)
        {
            var t = new Tally();
            var life = HeartState.For(x.key);
            for (int r = 0; r < runs; r++)
            {
                var y = new Year { x = x, life = life, n = n, after = after, t = t, rngs = new Dictionary<string, Random>() };
                int baseSeed = seed * 7919 + r * 31 + n;
                Random Rng(string name)
                {
                    if (!y.rngs.TryGetValue(name, out var g)) y.rngs[name] = g = new Random(baseSeed * 131 + Stable(name));
                    return g;
                }
                var lifeRng = new Random(baseSeed);
                var setup = Rng("setup");
                y.people = Enumerable.Range(0, n).Select(_ => new Person { tough = setup.NextDouble() < 0.07 }).ToList();
                // The heart's good side on, as by default, with its own draws.
                foreach (var p in y.people)
                {
                    p.heart.goodOn = true;
                    p.heart.goodRng = new Random(baseSeed * 17 + y.people.IndexOf(p));
                }
                y.peacemaker = setup.NextDouble() < Peacemaker;
                y.electionDay = 1 + setup.Next(Days - 1);
                for (int a = 0; a < n; a++)
                    for (int b = a + 1; b < n; b++)
                    {
                        if (setup.NextDouble() >= Rates.RivalShare * Bitter) continue;
                        bool same = setup.NextDouble() < SameWork;
                        y.rivals.Add((y.people[a], y.people[b], setup.NextDouble() < Petty, same));
                        y.rivals.Add((y.people[b], y.people[a], setup.NextDouble() < Petty, same));
                    }
                y.rngs["life"] = lifeRng;
                Rng("pain");
                PlayYear(y, Rng);
                t.runs++;
                t.colonists += n;
            }
            return t;
        }

        private static int Stable(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }

        private static void PlayYear(Year y, Func<string, Random> rng)
        {
            var life = y.life;
            var lifeRng = rng("life");
            for (int day = 0; day < Days; day++)
            {
                bool raid = lifeRng.NextDouble() < life.raid, death = lifeRng.NextDouble() < life.deathsPerYear / Days;
                int raidHour = lifeRng.Next(6, 20), deathHour = lifeRng.Next(0, 24);
                bool party = lifeRng.NextDouble() < life.party;
                foreach (var p in y.people) p.heart.StartDay(life, lifeRng, day, raid, raidHour, death, deathHour);
                if (raid) y.t.raidWounds += y.n * RaidWound;
                y.today.Clear();
                PartOneDay(y, day, rng);
                // Parts 2 and 3 draw on their own, and the day's frayed tempers roll in both (only "after" lets the strain count).
                Frayed(y, rng);
                if (y.after) PartTwoDay(y, day, rng);
                for (int h = 0; h < 24; h++)
                {
                    foreach (var e in y.today.Where(e => e.hour == h).ToList()) e.act();
                    foreach (var p in y.people)
                    {
                        if (p.heart.Hour(life, lifeRng, day, h, party, y.hour, ref y.lastColonyEpisode)) y.t.episodes++;
                        var band = HeartMath.BandFor(p.heart.strain);
                        y.t.bands[(int)band]++;
                        y.t.mood += HeartMath.MoodOffset(band);
                        y.t.hours++;
                    }
                    y.hour++;
                }
            }
        }

        // ---------------- The hurt, through the cap ----------------

        private static void Add(Dictionary<string, double> d, string key, double v = 1) => d[key] = d.GetValueOrDefault(key) + v;

        /// <summary>The cap lets them be hurt now (one in three days).</summary>
        private static bool May(Year y, Person p) => y.hour - p.lastHurt >= HarmMath.GapDays * 24;

        /// <summary>A Rimfolk-made hurt (counted for the cap), its pain on the heart when it wounds; false when the cap held it back.</summary>
        private static bool Hurt(Year y, Person p, string source, Random g, bool wounds = true)
        {
            if (!May(y, p))
            {
                Add(y.t.barred, source);
                return false;
            }
            p.lastHurt = y.hour;
            Add(y.t.hurts, source);
            if (wounds)
            {
                y.t.wounds++;
                p.heart.Build(StrainSource.Pain, 2f + (float)y.rngs["pain"].NextDouble() * 4f, "hurts");
            }
            return true;
        }

        /// <summary>A fight between two: an hour of combat and a bruise's pain for each; under the cap when it is Rimfolk's (both counted), or plain.</summary>
        private static bool Fight(Year y, Person a, Person b, string source, Random g, bool capped)
        {
            if (capped && (!May(y, a) || b != null && !May(y, b)))
            {
                Add(y.t.barred, source);
                return false;
            }
            Add(y.t.fights, source);
            foreach (var p in new[] { a, b })
            {
                if (p == null) continue;
                if (capped)
                {
                    p.lastHurt = y.hour;
                    Add(y.t.hurts, source);
                }
                p.heart.Build(StrainSource.Combat, 4f, "fights");
                p.heart.Build(StrainSource.Pain, 1f + (float)y.rngs["pain"].NextDouble() * 2f, "fights");
            }
            return true;
        }

        private static Person Any(Year y, Random g, Person not = null)
        {
            var pool = not == null ? y.people : y.people.Where(p => p != not).ToList();
            return pool.Count == 0 ? null : pool[g.Next(pool.Count)];
        }

        private static float Factor(Person p) => p == null ? 1f : HarmTwoMath.FrayedFactor(HeartMath.BandFor(p.heart.strain), 1f);

        // ---------------- Part 1 (0.106.0) ----------------

        private static void PartOneDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // Drills that bruise: the evening's bouts while a drills decree holds (the militia's one pair three evenings in four in half the colonies).
            var gd = rng("drills");
            if (gd.NextDouble() < x.drillDays / Days)
            {
                bool muster = gd.NextDouble() < 0.5, marshal = gd.NextDouble() < 0.4;
                int pairs = muster ? (HarmMath.MusterEvening(day) ? 1 : 0) : HarmMath.DrillPairs((int)(y.n * 0.7f), false);
                for (int i = 0; i < pairs; i++)
                {
                    if (gd.NextDouble() >= HarmMath.DrillBlow(marshal)) continue;
                    var p = Any(y, gd);
                    y.today.Add((18, () => Hurt(y, p, PartOne[0], gd)));
                }
            }
            // Tired hands: a long day at the work a work decree drives, one roll each (half of them work such a day).
            var gw = rng("work");
            if (gw.NextDouble() < x.workDays / Days)
                foreach (var p in y.people)
                {
                    if (gw.NextDouble() >= 0.5) continue;
                    bool low = gw.NextDouble() < x.lowRest;
                    if (gw.NextDouble() < HarmMath.TiredHands(1, low, p.tough, false)) y.today.Add((15, () => Hurt(y, p, PartOne[1], gw)));
                }
            // Working through it: a strain or a sprain worked hard through untended turns worse one time in ten.
            var gt = rng("through");
            foreach (var p in y.people)
                if (gt.NextDouble() < x.strains / Days * x.workedThrough && gt.NextDouble() < HarmMath.WorkedThroughChance)
                    y.today.Add((14, () => Hurt(y, p, PartOne[2], gt)));
            // Dangers on the map: one of Rimfolk's events, its hurt by the answer.
            var gm = rng("dangers");
            if (gm.NextDouble() < x.dangers / Days && gm.NextDouble() < x.dangerHurt)
            {
                var p = Any(y, gm);
                y.today.Add((12, () => Hurt(y, p, PartOne[3], gm)));
            }
            // The ailments of the land and the work that are Rimfolk-made hurts (they ask the cap before they come).
            var ga = rng("ailments");
            foreach (var p in y.people)
                if (ga.NextDouble() < x.landAilments / Days)
                    y.today.Add((10, () => Hurt(y, p, PartOne[4], ga)));
        }

        // ---------------- Frayed tempers (3.3.1) ----------------

        /// <summary>Rimfolk's own sharp words with a fight's chance: the strain's part only after (the cap for both), the rest a fight as before.</summary>
        private static void Frayed(Year y, Func<string, Random> rng)
        {
            var g = rng("frayed");
            if (g.NextDouble() < y.x.sharpWords * y.n / 6f / Days)
            {
                var a = Any(y, g);
                var b = Any(y, g, a);
                float roll = (float)g.NextDouble();
                y.today.Add((13, () => Words(y, a, b, roll, SharpChance, g)));
            }
            if (g.NextDouble() < y.x.hardWords / Days)
            {
                var c = Any(y, g);
                float roll = (float)g.NextDouble();
                y.today.Add((16, () => Words(y, c, null, roll, HardChance, g)));
            }
        }

        private static void Words(Year y, Person a, Person b, float roll, float chance, Random g)
        {
            float factor = y.after ? HarmTwoMath.Frayed(Factor(a), Factor(b)) : 1f;
            if (!HarmTwoMath.FrayedRoll(roll, chance, factor, out bool frayed)) return;
            if (!frayed)
            {
                Fight(y, a, b, "Rimfolk's moments, as before", g, false);
                return;
            }
            Fight(y, a, b, PartTwo[6], g, true);
        }

        // ---------------- Parts 2 and 3 ----------------

        private static void PartTwoDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // A camp's quarrel that becomes a brawl: an evening's one roll while it runs high or soured, once a stay.
            var gc = rng("camp");
            if (y.campUntil < day && y.campGap < day && gc.NextDouble() < CampsPerYear / Days)
            {
                int stay = gc.Next(4, 13);
                y.campUntil = day + stay;
                y.campGap = y.campUntil + 15;
                y.campBrawled = false;
                y.quarrelDays.Clear();
                if (gc.NextDouble() < x.campHigh)
                {
                    int evenings = gc.Next(HighEveningsMin, HighEveningsMax + 1);
                    for (int i = 0; i < evenings; i++) y.quarrelDays.Add(y.campUntil - i);
                }
                if (gc.NextDouble() < x.campFeud)
                {
                    int from = day + gc.Next(1, Math.Max(2, stay - 2));
                    for (int i = 0; i < HarmTwoMath.SourQuarrelDays; i++) y.quarrelDays.Add(from + i);
                }
            }
            if (day <= y.campUntil && y.quarrelDays.Contains(day) && !y.campBrawled)
                y.today.Add((19, () => CampEvening(y, gc)));

            // Sickness brought home: a journey's one roll, the sickness showing two to four days after.
            var gs = rng("sick");
            if (gs.NextDouble() < x.journeys / Days && gs.NextDouble() < SickTown)
            {
                double h = gs.NextDouble();
                float hours = h < QuickShare ? 1f : h < QuickShare + AfternoonShare ? 2f + (float)gs.NextDouble() * 6f : 8f + (float)gs.NextDouble() * 16f;
                var kind = gs.NextDouble() < PlagueShare ? ColonyCouncil.SickTown.Plague : ColonyCouncil.SickTown.Famine;
                float chance = HarmTwoMath.SickHome(kind, hours, gs.NextDouble() < x.medicine, gs.NextDouble() < Modern, gs.NextDouble() < x.quarantine);
                int travellers = gs.Next(TravellersMin, TravellersMax + 1);
                var who = y.people.OrderBy(_ => gs.Next()).Take(travellers).ToList();
                int back = day + gs.Next(3, 9);
                if (gs.NextDouble() < chance)
                {
                    var p = who[gs.Next(who.Count)];
                    y.onsets.Add((p, back + (int)(HarmTwoMath.OnsetTicks((float)gs.NextDouble()) / HarmTwoMath.DayTicks)));
                }
            }
            foreach (var o in y.onsets.Where(o => o.day <= day).ToList())
            {
                if (day - o.day > HarmTwoMath.OnsetGraceDays)
                {
                    y.onsets.Remove(o);
                    continue;
                }
                var p = o.p;
                y.today.Add((9, () =>
                {
                    if (!y.onsets.Contains(o)) return;
                    if (!May(y, p)) return;
                    y.onsets.Remove(o);
                    Hurt(y, p, PartTwo[1], gs);
                }));
            }

            // The champion's bout: one bout in 25 lands a real blow, on either of the two.
            var gb = rng("champion");
            if (gb.NextDouble() < Challenges * BoutShare / Days)
            {
                var p = Any(y, gb);
                y.today.Add((15, () =>
                {
                    if (!May(y, p)) return;
                    if (gb.NextDouble() >= HarmTwoMath.ChampionBlowChance) return;
                    if (gb.NextDouble() < 0.5) Hurt(y, p, PartTwo[2], gb);
                }));
            }

            // Hunts that turn: a lone beast that can turn among the game, the game's own chance with each hit.
            var gh = rng("hunt");
            if (gh.NextDouble() < (x.hunts + HuntingDay) / Days && gh.NextDouble() < LoneTurner)
            {
                var p = Any(y, gh);
                y.today.Add((10, () =>
                {
                    // Never on a hunter the cap keeps from a hurt (their own shot at it not counted as fighting).
                    if (!May(y, p))
                    {
                        Add(y.t.barred, PartTwo[3]);
                        return;
                    }
                    float revenge = Math.Min(1f, (RevengeMin + (float)gh.NextDouble() * (RevengeMax - RevengeMin)) * RangeFactor);
                    float each = HarmTwoMath.TurnChance(revenge, 1f, true);
                    int hits = gh.Next(HitsMin, HitsMax + 1);
                    bool turned = false;
                    for (int i = 0; i < hits && !turned; i++) turned = gh.NextDouble() < each;
                    if (!turned) return;
                    Add(y.t.fights, "a hunt's game turning on its hunter");
                    Hurt(y, p, PartTwo[3], gh, gh.NextDouble() < TurnedWounds);
                }));
            }

            // A rival's sabotage, found out: a day's chance each way for a bitter rivalry of a petty nature.
            var gr = rng("sabotage");
            foreach (var (rival, other, petty, same) in y.rivals)
            {
                float chance = HarmTwoMath.SabotageChance(60f, petty, same, y.peacemaker);
                if (chance <= 0f || gr.NextDouble() >= chance) continue;
                if (gr.NextDouble() >= Bench * Acts) continue;
                bool seen = gr.NextDouble() < Witness;
                if (gr.NextDouble() >= HarmTwoMath.FoundOut(seen)) continue;
                if (gr.NextDouble() >= Confronts) continue;
                y.today.Add((17, () =>
                {
                    if (!Fight(y, other, rival, PartTwo[4], gr, true)) Add(y.t.fights, "a sabotage had out in words");
                }));
            }

            // Hustings that come to blows: the first heckle with a rival among the speaker's supporters, once a hustings.
            var gu = rng("hustings");
            if (day == y.electionDay && gu.NextDouble() < HustingsHeld)
            {
                double fans = Math.Max(1.0, y.n / 3.0);
                double q = (1.0 - Math.Pow(1.0 - Rates.RivalShare, fans)) * BacksOther;
                if (gu.NextDouble() < 1.0 - Math.Pow(1.0 - q, Heckles))
                {
                    var a = Any(y, gu);
                    var b = Any(y, gu, a);
                    bool calm = gu.NextDouble() < CalmCandidates, manners = gu.NextDouble() < Manners, pm = gu.NextDouble() < Peacemaker;
                    y.today.Add((19, () =>
                    {
                        float chance = HarmTwoMath.HustingsBrawl(pm, manners, calm, HarmTwoMath.Frayed(Factor(a), Factor(b)));
                        if (gu.NextDouble() < chance) Fight(y, a, b, PartTwo[5], gu, true);
                    }));
                }
            }
        }

        /// <summary>An evening at a camp's fire while the quarrel can come to blows: one roll, the colony's side if one of them is there.</summary>
        private static void CampEvening(Year y, Random g)
        {
            if (y.campBrawled) return;
            bool colony = g.NextDouble() < ColonyAtFire;
            var at = colony ? y.people.Where(p => May(y, p)).OrderBy(_ => g.Next()).ToList() : new List<Person>();
            if (colony && at.Count == 0) colony = false;
            float frayed = colony ? at.Take(1 + y.people.Count(_ => g.NextDouble() < OthersAtFire)).Max(Factor) : 1f;
            if (g.NextDouble() >= HarmTwoMath.CampBrawlChance(y.peacemaker, frayed)) return;
            y.campBrawled = true;
            if (!colony)
            {
                Add(y.t.fights, "a camp's brawl among themselves");
                return;
            }
            var first = at[0];
            int others = y.people.Count(p => p != first && May(y, p) && g.NextDouble() < OthersAtFire);
            int join = HarmTwoMath.JoinPairs(others, g.Next(TheirsFreeMin, TheirsFreeMax + 1));
            foreach (var p in new[] { first }.Concat(at.Skip(1).Take(join)))
                Fight(y, p, null, PartTwo[0], g, true);
        }

        // ---------------- The report ----------------

        private static void Report(Exposure x, int n, Tally before, Tally after)
        {
            double cy = after.colonists, by = before.colonists, years = after.runs;
            string name = HeartState.For(x.key).name;
            Console.WriteLine($"== {name}, {n} colonists");
            Console.WriteLine($"   {"source",-36} {"before",8} {"after",8} {"held back",10} {"a colony-year",14}   (hurts per colonist-year; the last, after, for the whole colony)");
            double tb = 0, ta = 0;
            foreach (var s in PartOne.Concat(PartTwo))
            {
                double b = before.hurts.GetValueOrDefault(s) / by, a = after.hurts.GetValueOrDefault(s) / cy, held = after.barred.GetValueOrDefault(s) / cy;
                tb += b;
                ta += a;
                Console.WriteLine($"   {s,-36} {b,8:0.000} {a,8:0.000} {held,10:0.000} {after.hurts.GetValueOrDefault(s) / years,14:0.000}");
            }
            double partTwo = PartTwo.Sum(s => after.hurts.GetValueOrDefault(s)) / cy;
            Console.WriteLine($"   {"in all",-36} {tb,8:0.000} {ta,8:0.000}   (parts 2 and 3 {partTwo:0.000}, {partTwo / Math.Max(1e-9, ta):P0} of it; wounds that hurt {after.wounds / cy:0.000})");
            Console.WriteLine($"   the game's own beside it: a raid's wound {after.raidWounds / cy:0.00} a colonist-year (HeartSim's raids), a food poisoning 0.3 to 1 (two meals a day,");
            Console.WriteLine("   a middling cook), the storyteller's diseases a few a colony-year.");
            Console.Write("   fights a colony-year:");
            foreach (var k in after.fights.Keys.Union(before.fights.Keys).OrderBy(k => k))
                Console.Write($" {k} {before.fights.GetValueOrDefault(k) / before.runs:0.00} -> {after.fights.GetValueOrDefault(k) / years:0.00};");
            Console.WriteLine();
            string Bands(Tally t) => $"strained {t.bands[1] / t.hours:P1}, worn {t.bands[2] / t.hours:P1}, near breaking {t.bands[3] / t.hours:P1}; "
                                     + $"episodes {t.episodes / t.colonists:0.000} a colonist-year; strain's mood {t.mood / t.hours:0.00}";
            Console.WriteLine("   heart before: " + Bands(before));
            Console.WriteLine("   heart after:  " + Bands(after));
            Console.WriteLine();
        }
    }
}
