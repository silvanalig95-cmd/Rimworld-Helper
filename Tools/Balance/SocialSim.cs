// How bonds come about in a colony of ten over two years, with Rimfolk's rules as they were (comrades for everyone
// who fought, friendships from opinion alone) and as they are now (Social/BondsMath.cs: knowing each other, how two
// get on, comrades by shared danger, grudges with causes, a seat taken). Not a target: nothing is steered toward a
// number. It is a check that nothing runs away (everyone friends, or everyone at odds), and a look at how fast things
// form. The game's own social life is modelled roughly from its code (chitchat, deep talks, slights and insults with
// the game's chances, cumulative memories that fade a point a day), so read the numbers as shapes, not as the game.
//
//   dotnet run -c Release --project Tools/Balance -- --social [--runs N] [--seed S]

using ColonyCouncil;

namespace Balance
{
    public static class SocialSim
    {
        private const int Days = 120;
        private static readonly int[] Checkpoints = { 30, 60, 120 };
        private const int Offices = 5;

        private sealed class Person
        {
            public int id, joinDay;
            public float age;
            public bool kind, abrasive, jealous, greedy, hot, fighter, aspires, doctor;
            public int faith, work;
            public float[] views = new float[6];
            public int[] passions = new int[3];
            public float[] skill = new float[Offices];
            public int partner = -1;
            public bool Vain => greedy || jealous || abrasive;
        }

        private sealed class Dir
        {
            public float chit, deep, insult, slight;
            public readonly List<(float v, int until)> mems = new List<(float, int)>();
            public int rebuffedBy = -1, rebuffDay = -999, dumpedBy = -1, dumpDay = -999;

            public void Add(float v, int day, int days) => mems.Add((v, day + days));

            public float Sum(int day)
            {
                float s = chit + deep + insult + slight;
                mems.RemoveAll(m => m.until <= day);
                foreach (var m in mems) s += m.v;
                return s;
            }

            public void Fade()
            {
                chit = Math.Max(0f, chit - 1f);
                deep = Math.Max(0f, deep - 1f);
                insult = Math.Min(0f, insult + 1f);
                slight = Math.Min(0f, slight + 1f);
            }
        }

        private sealed class Pair
        {
            public Person a, b;
            public float compat, aff, fam, recent;
            public float friends, rivals, comrades;
            public bool fAnn, rAnn, cAnn;
            public int battles, lastBattle = -999, renewed = -999, awkwardUntil = -1, formedDay = -1, firstKnown = -1;
            public string cause;
            public int lossesAB, lossesBA;
            public bool attractAB, attractBA, lovers;
            public readonly Dir ab = new Dir(), ba = new Dir();

            public Dir From(Person x) => x == a ? ab : ba;
            public Person Other(Person x) => x == a ? b : a;
        }

        private sealed class Tally
        {
            public int runs;
            public readonly Dictionary<string, List<float>> values = new Dictionary<string, List<float>>();

            public void Add(string key, float v)
            {
                if (!values.TryGetValue(key, out var l)) values[key] = l = new List<float>();
                l.Add(v);
            }

            public string Line(string key, string fmt = "0.0")
            {
                if (!values.TryGetValue(key, out var l) || l.Count == 0) return "-";
                l.Sort();
                float mean = l.Average();
                float p10 = l[(int)(l.Count * 0.1)], p90 = l[Math.Min(l.Count - 1, (int)(l.Count * 0.9))];
                return mean.ToString(fmt) + " (" + p10.ToString(fmt) + "-" + p90.ToString(fmt) + ")";
            }
        }

        private sealed class Colony
        {
            public readonly Random rnd;

            public readonly List<Person> people = new List<Person>();
            public readonly List<Pair> pairs = new List<Pair>();
            public readonly int[] holders = Enumerable.Repeat(-1, Offices).ToArray();
            public int nextRaid, bestRoom = -1, nextElection = 20;
            /// <summary>How warm the game's own talk runs: 1 as modelled, 2 for a colony that talks twice as often and lets deep talks add up twice as far.</summary>
            public float warm = 1f;
            public readonly Dictionary<string, int> causes = new Dictionary<string, int>();

            public Colony(int seed)
            {
                rnd = new Random(seed);
            }

            public float U(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
            public bool P(double p) => rnd.NextDouble() < p;

            public float Gauss()
            {
                double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble();
                return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            }

            public Pair PairOf(Person x, Person y) => pairs.First(p => (p.a == x && p.b == y) || (p.a == y && p.b == x));

            public IEnumerable<Pair> Present(int day) => pairs.Where(p => p.a.joinDay <= day && p.b.joinDay <= day);
        }

        public static void Run(string[] args)
        {
            int runs = 200, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            Console.WriteLine($"Bonds in a colony growing to ten, over two years ({Days} days), {runs} runs each. Mean (10th-90th percentile).");
            Console.WriteLine();
            foreach (float warm in new[] { 1f, 2f })
                foreach (bool rules in new[] { false, true })
                {
                    var tally = new Tally();
                    for (int r = 0; r < runs; r++) Play(new Colony(seed * 7919 + r) { warm = warm }, tally, 0, Days, false, rules);
                    Report((rules ? "Now (knowing each other, how two get on, comrades by danger, grudges with causes)" : "As it was (comrades for every fighter, friendships from opinion alone)")
                           + (warm > 1f ? ", in a colony that talks twice as much" : ""), tally);
                }
            // The player's kind of save: nine together for 96 days under the old rules, then the new ones for 120 more.
            var switched = new Tally();
            for (int r = 0; r < runs; r++)
            {
                var c = new Colony(seed * 104729 + r);
                Play(c, null, 0, 96, true, false);
                Snapshot(c, switched, 96, false, false);
                Migrate(c, 96);
                Play(c, switched, 96, 96 + Days, true, true);
            }
            Report("A save like the player's: nine together for 96 days as it was, then the new rules (day 96 is the switch)", switched, new[] { 96, 126, 156, 216 });
        }

        // ---------------- One colony ----------------

        /// <summary>Days <paramref name="from"/> to <paramref name="to"/> of one colony, with the rules as they were or as they are now.</summary>
        private static void Play(Colony c, Tally tally, int from, int to, bool nine, bool rules)
        {
            if (from == 0) Found(c, nine);
            for (int day = from; day < to; day++)
            {
                Day(c, day, rules);
                int d = day + 1;
                if (tally != null && Checkpoints.Contains(d - from)) Snapshot(c, tally, d, rules, d == to);
            }
            if (tally != null) tally.runs++;
        }

        private static void Found(Colony c, bool nine)
        {
            int n = nine ? 9 : 10;
            int founders = nine ? 9 : 3 + c.rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                var p = new Person
                {
                    id = i,
                    joinDay = i < founders ? 0 : (int)c.U(8, 85),
                    age = c.U(18, 62),
                    kind = c.P(0.1), abrasive = c.P(0.1), jealous = c.P(0.08), greedy = c.P(0.08), hot = c.P(0.08),
                    fighter = c.P(0.85), aspires = c.P(0.15), faith = c.P(0.25) ? 1 : 0, work = c.rnd.Next(5)
                };
                if (p.kind) p.abrasive = false;
                for (int v = 0; v < p.views.Length; v++) p.views[v] = c.P(0.35) ? float.NaN : Math.Clamp(c.Gauss() * 0.5f, -1f, 1f);
                for (int s = 0; s < 3; s++) p.passions[s] = c.rnd.Next(12);
                for (int o = 0; o < Offices; o++) p.skill[o] = c.U(2, 18);
                c.people.Add(p);
            }
            c.people.OrderBy(p => p.joinDay).First().doctor = true;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    var a = c.people[i];
                    var b = c.people[j];
                    float gap = Math.Abs(a.age - b.age);
                    float ageTerm = Math.Clamp(0.45f - 0.9f * gap / 20f, -0.45f, 0.45f);
                    float g = c.Gauss();
                    float compat = ageTerm + 0.3f + (g < 0 ? g * 1f : g * 1.4f);
                    int agree = 0, odds = 0;
                    for (int v = 0; v < a.views.Length; v++)
                    {
                        if (float.IsNaN(a.views[v]) || float.IsNaN(b.views[v]) || Math.Abs(a.views[v]) < 0.2f || Math.Abs(b.views[v]) < 0.2f) continue;
                        if (Math.Sign(a.views[v]) == Math.Sign(b.views[v])) agree++;
                        else odds++;
                    }
                    int shared = a.passions.Distinct().Count(s => b.passions.Contains(s));
                    int kindN = (a.kind ? 1 : 0) + (b.kind ? 1 : 0), abr = (a.abrasive ? 1 : 0) + (b.abrasive ? 1 : 0);
                    var pair = new Pair
                    {
                        a = a, b = b, compat = compat,
                        aff = BondsMath.Affinity(compat, a.faith == b.faith ? 1 : -1, agree, odds, shared, kindN, abr),
                        attractAB = gap < 15f && c.P(0.3), attractBA = gap < 15f && c.P(0.3)
                    };
                    c.pairs.Add(pair);
                }
            c.nextRaid = (int)c.U(5, 14);
        }

        private static void Day(Colony c, int day, bool rules)
        {
            var here = c.people.Where(p => p.joinDay <= day).ToList();
            foreach (var pr in c.Present(day)) Together(c, pr, day, rules);
            foreach (var pr in c.Present(day)) Evolve(c, pr, day, rules);
            if (day >= c.nextRaid)
            {
                Raid(c, here, day, rules);
                c.nextRaid = day + (int)c.U(8, 17);
            }
            if (day == c.nextElection && here.Count >= 4)
            {
                Election(c, here, day, rules);
                c.nextElection += 60;
            }
            Romance(c, here, day, rules);
            Health(c, here, day, rules);
            if (day % 30 == 0) c.bestRoom = here.Count > 2 && c.P(0.6) ? here[c.rnd.Next(here.Count)].id : -1;
            if (rules && c.bestRoom >= 0)
                foreach (var x in here.Where(x => x.jealous && x.id != c.bestRoom && x.partner != c.bestRoom))
                {
                    var pr = c.PairOf(x, c.people[c.bestRoom]);
                    if (pr.friends < 45f) Grudge(c, pr, x, BondsMath.EnvyPerDay, "envy", day);
                }
        }

        // ---------------- Time side by side, and what passes between them ----------------

        private static void Together(Colony c, Pair pr, int day, bool rules)
        {
            float work = pr.a.work == pr.b.work ? c.U(3f, 7f) : c.U(0f, 1.5f);
            float share = (c.P(0.5) ? 0.75f : 0f) + (c.P(0.5) ? 0.75f : 0f) + (c.P(0.35) ? 1.5f : 0f);
            if (pr.lovers) share += 3f;
            float aff = rules ? pr.aff : pr.compat;
            if (rules)
            {
                bool awkward = day < pr.awkwardUntil;
                for (int h = 0; h < (int)work; h++) pr.fam += BondsMath.HourGain(pr.fam, false, pr.aff, awkward);
                for (int h = 0; h < (int)(share * 2) / 2; h++) pr.fam += BondsMath.HourGain(pr.fam, true, pr.aff, awkward);
                pr.recent = pr.recent * BondsMath.RecentKeep + work + share;
                pr.fam = Math.Max(0f, pr.fam + BondsMath.DayDrift(pr.fam, pr.recent));
                if (pr.firstKnown < 0 && pr.fam >= BondsMath.KnowAt) pr.firstKnown = day;
            }
            float hours = work + share;
            foreach (var (x, y) in new[] { (pr.a, pr.b), (pr.b, pr.a) })
            {
                for (int h = 0; h < (int)Math.Round(hours); h++)
                {
                    if (!c.P(0.15 * c.warm)) continue;
                    float opYX = Opinion(c, pr, y, day, rules);
                    float deep = 0.075f * DeepCurve(pr.compat);
                    float neg = x.kind ? 0f : NegFactor(pr.compat, Opinion(c, pr, x, day, rules)) * (x.abrasive ? 2.3f : 1f) * (x.hot ? 1.5f : 1f);
                    float roll = c.U(0f, 1f + deep + 0.02f * neg + 0.007f * neg);
                    if (roll < 1f) Warm(c, pr, x, y, 0.66f, false, day, rules);
                    else if (roll < 1f + deep) Warm(c, pr, x, y, 15f, true, day, rules);
                    else if (roll < 1f + deep + 0.02f * neg)
                    {
                        pr.From(y).slight = Math.Max(-15f, pr.From(y).slight - 5f * ChemistryMath.SoreFactor(aff));
                        if (rules) Grudge(c, pr, y, BondsMath.Slight, "words", day);
                    }
                    else
                    {
                        pr.From(y).insult = Math.Max(-30f, pr.From(y).insult - 15f * ChemistryMath.SoreFactor(aff));
                        if (rules) Grudge(c, pr, y, BondsMath.Insult, "words", day);
                        if (c.P(0.04 * (opYX < 0 ? 2 : 1) * (y.hot ? 2 : 1)))
                        {
                            pr.ab.Add(-15f, day, 10);
                            pr.ba.Add(-15f, day, 10);
                            if (rules) Grudge(c, pr, y, BondsMath.CameToBlows, "blows", day);
                            else Bump(c, pr, ref pr.rivals, ref pr.rAnn, 5f, day);
                        }
                    }
                }
            }
            // Rimfolk's moments and talks between two who spent a while together.
            if (hours >= 1f)
            {
                if (c.P(0.08 * ChemistryMath.MomentFactor(aff, false)))
                    foreach (var x in new[] { pr.a, pr.b }) WarmMemory(c, pr, x, 4f, 3, day, rules);
                if (c.P(0.02 * ChemistryMath.MomentFactor(aff, true)))
                    foreach (var x in new[] { pr.a, pr.b }) pr.From(x).Add(-3f * ChemistryMath.SoreFactor(aff), day, 2);
                if (c.P(0.06)) FriendBump(c, pr, 1.5f, day, rules);
                int odds = 0;
                for (int v = 0; v < 6; v++)
                    if (!float.IsNaN(pr.a.views[v]) && !float.IsNaN(pr.b.views[v]) && Math.Sign(pr.a.views[v]) != Math.Sign(pr.b.views[v]) && Math.Abs(pr.a.views[v]) >= 0.2f && Math.Abs(pr.b.views[v]) >= 0.2f) odds++;
                if (odds > 0 && c.P(0.02 * odds))
                {
                    pr.ab.Add(-3f, day, 3);
                    pr.ba.Add(-3f, day, 3);
                    if (rules) Grudge(c, pr, pr.a, 2f, "views", day);
                    else Bump(c, pr, ref pr.rivals, ref pr.rAnn, 2f, day);
                }
                if (rules && c.P(0.05)) pr.fam += BondsMath.SharedGain(1f, pr.fam);
            }
        }

        private static float DeepCurve(float c) => c <= -1.5f ? 0f : c <= -0.5f ? 0.1f * (c + 1.5f) : c <= 0.5f ? 0.1f + 0.9f * (c + 0.5f) : c <= 1f ? 1f + 1.6f * (c - 0.5f) : Math.Min(2f, 1.8f + 0.2f * (c - 1f));

        private static float NegFactor(float compat, float opinion)
        {
            float cf = compat <= -1.5f ? 3f : compat <= -0.5f ? 3f - (compat + 1.5f) : compat <= 0.5f ? 2f - (compat + 0.5f) : compat <= 1f ? 1f - 0.5f * (compat - 0.5f) : 0.75f - 0.25f * Math.Min(1f, compat - 1f);
            float of = opinion <= -50f ? 4f : opinion <= -25f ? 2f + 2f * (-25f - opinion) / 25f : opinion <= 0f ? 1f + (-opinion) / 25f : opinion <= 50f ? 1f - 0.9f * opinion / 50f : 0.1f * (1f - (opinion - 50f) / 50f);
            return cf * Math.Max(0f, of);
        }

        /// <summary>A warm word (chitchat or deep talk): both get it, as much as the two get on (and, now, as well as they know each other, up to the words' cap).</summary>
        private static void Warm(Colony c, Pair pr, Person x, Person y, float offset, bool deep, int day, bool rules)
        {
            foreach (var h in new[] { x, y })
            {
                var d = pr.From(h);
                float f;
                if (rules)
                {
                    pr.fam += BondsMath.TalkGain(offset, pr.fam);
                    f = ChemistryMath.WarmFactor(pr.aff) * BondsMath.KnowFactor(pr.fam) * BondsMath.CapFactor(Opinion(c, pr, pr.Other(h), day, true), BondsMath.WordsCap(pr.aff));
                }
                else f = ChemistryMath.WarmFactor(pr.compat);
                if (deep) d.deep = Math.Min(30f * c.warm, d.deep + offset * f);
                else d.chit = Math.Min(10f * c.warm, d.chit + offset * f);
            }
        }

        private static void WarmMemory(Colony c, Pair pr, Person h, float v, int days, int day, bool rules)
        {
            float f = rules
                ? ChemistryMath.WarmFactor(pr.aff) * BondsMath.KnowFactor(pr.fam) * BondsMath.CapFactor(Opinion(c, pr, pr.Other(h), day, true), BondsMath.WordsCap(pr.aff))
                : ChemistryMath.WarmFactor(pr.compat);
            pr.From(h).Add(v * f, day, days);
        }

        // ---------------- Opinion ----------------

        /// <summary>What <paramref name="x"/> thinks of the other in the pair.</summary>
        private static float Opinion(Colony c, Pair pr, Person x, int day, bool rules)
        {
            float s = pr.From(x).Sum(day);
            if (pr.fAnn) s += pr.friends >= 70f ? 18f : pr.friends >= 45f ? 10f : 5f;
            if (pr.rAnn) s -= pr.rivals >= 70f ? 18f : pr.rivals >= 45f ? 10f : 5f;
            if (pr.cAnn) s += pr.comrades >= 70f ? 8f : pr.comrades >= 45f ? 5f : 3f;
            int days = day - Math.Max(pr.a.joinDay, pr.b.joinDay);
            int stage = ChemistryMath.Stage(rules ? pr.aff : pr.compat, days);
            s += stage switch { 0 => -3f, 1 => -6f, 2 => -10f, 3 => 3f, 4 => 6f, _ => 0f };
            if (pr.lovers) s += 30f;
            return Math.Clamp(s, -100f, 100f);
        }

        private static (float low, float high) Mutual(Colony c, Pair pr, int day, bool rules)
        {
            float x = Opinion(c, pr, pr.a, day, rules), y = Opinion(c, pr, pr.b, day, rules);
            return (Math.Min(x, y), Math.Max(x, y));
        }

        // ---------------- Ties ----------------

        private static void Bump(Colony c, Pair pr, ref float strength, ref bool announced, float amount, int day)
        {
            strength = Math.Clamp(strength + amount, 0f, 100f);
            if (!announced && strength >= 20f) announced = true;
        }

        private static void FriendBump(Colony c, Pair pr, float amount, int day, bool rules)
        {
            if (pr.lovers) return;
            if (rules)
            {
                amount *= ChemistryMath.WarmFactor(pr.aff);
                float cap = BondsMath.FriendCap(pr.aff, pr.fam);
                pr.friends = Math.Max(Math.Min(pr.friends, 100f), Math.Min(pr.friends + amount, cap));
                if (!pr.fAnn && pr.friends >= 20f) Formed(c, pr, day, rules);
            }
            else
            {
                bool was = pr.fAnn;
                Bump(c, pr, ref pr.friends, ref pr.fAnn, amount, day);
                if (!was && pr.fAnn) pr.formedDay = day;
            }
            pr.rivals = Math.Max(0f, pr.rivals - amount * 0.7f);
            if (pr.rivals <= 0f) pr.rAnn = false;
        }

        private static void Grudge(Colony c, Pair pr, Person holder, float amount, string cause, int day)
        {
            int kind = (pr.a.kind ? 1 : 0) + (pr.b.kind ? 1 : 0), hot = (pr.a.abrasive || pr.a.hot || pr.a.jealous ? 1 : 0) + (pr.b.abrasive || pr.b.hot || pr.b.jealous ? 1 : 0);
            amount *= ChemistryMath.SoreFactor(pr.aff) * BondsMath.GrudgeGrowth(kind, hot);
            pr.rivals = Math.Clamp(pr.rivals + amount, 0f, 100f);
            pr.renewed = day;
            if (pr.cause == null || amount >= 5f) pr.cause = cause;
            if (!pr.rAnn && pr.rivals >= 20f)
            {
                pr.rAnn = true;
                c.causes[pr.cause] = (c.causes.TryGetValue(pr.cause, out int n) ? n : 0) + 1;
            }
            pr.friends = Math.Max(0f, pr.friends - amount * 0.7f);
            if (pr.friends <= 1f) pr.fAnn = false;
        }

        private static void Formed(Colony c, Pair pr, int day, bool rules)
        {
            pr.fAnn = true;
            if (pr.formedDay < 0) pr.formedDay = day;
            if (!rules) return;
            // A partner who minds them growing close (the jealous, or one who doesn't much like the friend).
            foreach (var (friend, other) in new[] { (pr.a, pr.b), (pr.b, pr.a) })
            {
                if (friend.partner < 0 || friend.partner == other.id) continue;
                var partner = c.people[friend.partner];
                var pp = c.PairOf(partner, other);
                if (partner.jealous || (!partner.kind && Opinion(c, pp, partner, day, true) < 15f && (other == pp.a ? pp.attractBA : pp.attractAB)))
                {
                    pp.From(partner).Add(-6f, day, 10);
                    Grudge(c, pp, partner, BondsMath.JealousFormed, "jealousy", day);
                }
            }
        }

        private static void Evolve(Colony c, Pair pr, int day, bool rules)
        {
            pr.ab.Fade();
            pr.ba.Fade();
            var (low, high) = Mutual(c, pr, day, rules);
            if (!pr.lovers)
            {
                if (rules)
                {
                    float target = BondsMath.FriendTarget((int)low, pr.aff, pr.fam);
                    if (!pr.fAnn && BondsMath.FriendsForm((int)low, pr.aff, pr.fam) && c.P(0.35))
                    {
                        pr.friends = Math.Max(pr.friends, Math.Min(low - 20f, BondsMath.FriendCap(pr.aff, pr.fam)));
                        if (pr.friends >= 20f) Formed(c, pr, day, rules);
                    }
                    else if (pr.friends > 0f)
                    {
                        pr.friends += (Math.Clamp(target, 0f, 100f) - pr.friends) * 0.1f;
                        if (pr.friends <= 1f)
                        {
                            pr.friends = 0f;
                            pr.fAnn = false;
                        }
                        else if (!pr.fAnn && pr.friends >= 20f) Formed(c, pr, day, rules);
                    }
                }
                else
                {
                    if (!pr.fAnn && low >= 45f && c.P(0.35))
                    {
                        pr.friends = Math.Max(pr.friends, low - 20f);
                        pr.fAnn = true;
                        pr.formedDay = day;
                    }
                    else if (pr.friends > 0f)
                    {
                        pr.friends += (Math.Clamp(low - 25f, 0f, 100f) - pr.friends) * 0.1f;
                        if (pr.friends <= 1f)
                        {
                            pr.friends = 0f;
                            pr.fAnn = false;
                        }
                        else if (!pr.fAnn && pr.friends >= 20f)
                        {
                            pr.fAnn = true;
                            pr.formedDay = day;
                        }
                    }
                }
            }
            // Rivals: steady loathing makes rivals as before; a grudge now holds a while and fades slowly.
            float rTarget = Math.Clamp(-high - 15f, 0f, 100f);
            if (!pr.rAnn && high <= -30f && c.P(0.35))
            {
                pr.rivals = Math.Max(pr.rivals, -high - 10f);
                pr.rAnn = true;
                pr.cause ??= "soured";
                if (rules) c.causes[pr.cause] = (c.causes.TryGetValue(pr.cause, out int n) ? n : 0) + 1;
            }
            else if (pr.rivals > 0f)
            {
                if (!rules || rTarget > pr.rivals) pr.rivals += (rTarget - pr.rivals) * 0.1f;
                else
                {
                    int kind = (pr.a.kind ? 1 : 0) + (pr.b.kind ? 1 : 0), hot = (pr.a.abrasive || pr.a.hot || pr.a.jealous ? 1 : 0) + (pr.b.abrasive || pr.b.hot || pr.b.jealous ? 1 : 0);
                    pr.rivals = BondsMath.GrudgeDay(pr.rivals, day - pr.renewed, kind, hot);
                }
                if (pr.rivals <= 1f)
                {
                    pr.rivals = 0f;
                    pr.rAnn = false;
                    pr.cause = null;
                }
                else if (!pr.rAnn && pr.rivals >= 20f) pr.rAnn = true;
            }
            // Comrades fade between fights.
            if (pr.comrades > 0f)
            {
                pr.comrades = rules ? BondsMath.ComradeDay(pr.comrades, day - pr.lastBattle, pr.battles) : pr.comrades - 0.5f;
                if (pr.comrades <= 1f)
                {
                    pr.comrades = 0f;
                    pr.cAnn = false;
                }
            }
        }

        // ---------------- Fights ----------------

        private static void Raid(Colony c, List<Person> here, int day, bool rules)
        {
            var fighters = here.Where(p => p.fighter).ToList();
            if (fighters.Count < 2) return;
            int clusters = 1 + c.rnd.Next(Math.Min(3, 1 + fighters.Count / 4));
            var at = fighters.ToDictionary(p => p, p => c.rnd.Next(clusters));
            int samples = 4 + c.rnd.Next(9);
            var deeds = new Dictionary<(Person, Person), int>();
            foreach (var hurt in fighters.Where(p => c.P(0.08)))
            {
                var mates = fighters.Where(m => m != hurt && at[m] == at[hurt]).ToList();
                if (mates.Count > 0 && c.P(0.6)) Deed(deeds, mates[c.rnd.Next(mates.Count)], hurt, BondsMath.DeedRescued);
                else if (c.P(0.3)) Deed(deeds, here[c.rnd.Next(here.Count)], hurt, BondsMath.DeedTended);
                foreach (var m in mates.Where(m => c.P(0.5))) Deed(deeds, m, hurt, BondsMath.DeedGuarded);
            }
            for (int i = 0; i < fighters.Count; i++)
                for (int j = i + 1; j < fighters.Count; j++)
                {
                    var x = fighters[i];
                    var y = fighters[j];
                    var pr = c.PairOf(x, y);
                    if (!rules)
                    {
                        Bump(c, pr, ref pr.comrades, ref pr.cAnn, 25f, day);
                        continue;
                    }
                    int near = at[x] == at[y] ? (int)Math.Round(samples * c.U(0.5f, 1f)) : (int)Math.Round(samples * c.U(0f, 0.15f));
                    int d = (deeds.TryGetValue((x, y), out int d1) ? d1 : 0) | (deeds.TryGetValue((y, x), out int d2) ? d2 : 0);
                    float bond = BondsMath.BattleBond(near, samples, d);
                    if (bond <= 0f) continue;
                    Bump(c, pr, ref pr.comrades, ref pr.cAnn, bond, day);
                    pr.battles++;
                    pr.lastBattle = day;
                    pr.fam += BondsMath.BattleKnow;
                    if (pr.rivals > 0f)
                    {
                        pr.rivals = Math.Max(0f, pr.rivals - BondsMath.BattleMend(d));
                        if (pr.rivals <= 1f) pr.rAnn = false;
                    }
                    if (at[x] == at[y] && c.P(0.01))
                    {
                        var victim = c.P(0.5) ? x : y;
                        pr.From(victim).Add(-10f, day, 10);
                        Grudge(c, pr, victim, BondsMath.HitInFight, "friendly fire", day);
                    }
                }
            if (!rules) return;
            foreach (var kv in deeds)
            {
                var (helper, helped) = kv.Key;
                if (helper == helped) continue;
                var pr = c.PairOf(helper, helped);
                if ((kv.Value & BondsMath.DeedRescued) != 0) pr.From(helped).Add(15f, day, 30);
                else if ((kv.Value & BondsMath.DeedTended) != 0) pr.From(helped).Add(8f, day, 30);
            }
        }

        private static void Deed(Dictionary<(Person, Person), int> deeds, Person helper, Person helped, int deed)
        {
            if (helper == helped) return;
            deeds[(helper, helped)] = (deeds.TryGetValue((helper, helped), out int d) ? d : 0) | deed;
        }

        // ---------------- The council's seats ----------------

        private static void Election(Colony c, List<Person> here, int day, bool rules)
        {
            var seated = new HashSet<Person>();
            for (int o = 0; o < Offices && o < here.Count - 1; o++)
            {
                var incumbent = c.holders[o] >= 0 ? c.people[c.holders[o]] : null;
                var free = here.Where(p => !seated.Contains(p)).ToList();
                var votes = new Dictionary<Person, int>();
                foreach (var v in here)
                {
                    var pick = free.OrderByDescending(cand =>
                        cand.skill[o] + (cand == v ? (v.Vain ? 6f : 1.5f) : Opinion(c, c.PairOf(v, cand), v, day, rules) / 12f) + c.U(0f, 4f)).First();
                    votes[pick] = (votes.TryGetValue(pick, out int n) ? n : 0) + 1;
                }
                var winner = votes.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key.skill[o]).First().Key;
                seated.Add(winner);
                c.holders[o] = winner.id;
                foreach (var loser in votes.Keys.Concat(incumbent != null ? new[] { incumbent } : new Person[0]).Distinct().Where(p => p != winner && !seated.Contains(p)))
                {
                    bool inc = loser == incumbent;
                    var pr = c.PairOf(loser, winner);
                    if (!rules)
                    {
                        if (inc) pr.From(loser).Add(-12f, day, 10);
                        else if (loser.Vain)
                        {
                            pr.From(loser).Add(-8f, day, 8);
                            Bump(c, pr, ref pr.rivals, ref pr.rAnn, 10f, day);
                        }
                        continue;
                    }
                    float want = BondsMath.SeatWant(inc, loser.aspires, loser.Vain, c.P(0.5));
                    int stage = BondsMath.SeatStage(want);
                    if (inc) pr.From(loser).Add(stage == BondsMath.SeatBitter ? -18f : -12f, day, 10);
                    else pr.From(loser).Add(stage == BondsMath.SeatBitter ? -12f : stage == BondsMath.SeatFelt ? -8f : -3f, day, 8);
                    int losses = loser == pr.a ? ++pr.lossesAB : ++pr.lossesBA;
                    Grudge(c, pr, loser, BondsMath.SeatGrudge(want, losses, inc), "seat", day);
                    if (winner.abrasive && c.P(0.5)) Grudge(c, pr, loser, BondsMath.Gloated, "seat", day);
                }
            }
        }

        // ---------------- Love, health ----------------

        private static void Romance(Colony c, List<Person> here, int day, bool rules)
        {
            foreach (var pr in c.Present(day).ToList())
            {
                if (pr.lovers)
                {
                    if (c.P(0.004))
                    {
                        pr.lovers = false;
                        pr.a.partner = pr.b.partner = -1;
                        var dumped = c.P(0.5) ? pr.a : pr.b;
                        pr.From(dumped).Add(-20f, day, 20);
                        pr.From(dumped).dumpedBy = pr.Other(dumped).id;
                        pr.From(dumped).dumpDay = day;
                        if (rules) pr.awkwardUntil = day + 20;
                    }
                    else if (rules && c.P(0.0008))
                    {
                        // A partner strays: the wronged one holds it against the cheat, and the one they strayed with.
                        var cheat = c.P(0.5) ? pr.a : pr.b;
                        var wronged = pr.Other(cheat);
                        var with = here.Where(p => p != cheat && p != wronged).OrderBy(_ => c.rnd.Next()).FirstOrDefault();
                        pr.From(wronged).Add(-30f, day, 20);
                        Grudge(c, pr, wronged, BondsMath.CheatedOn, "cheated", day);
                        if (with != null) Grudge(c, c.PairOf(wronged, with), wronged, BondsMath.TookUpWith, "cheated", day);
                    }
                    continue;
                }
                if (pr.a.partner >= 0 || pr.b.partner >= 0 || !(pr.attractAB && pr.attractBA)) continue;
                var (low, _) = Mutual(c, pr, day, rules);
                if (low < 30f || !c.P(0.01)) continue;
                var asker = c.P(0.5) ? pr.a : pr.b;
                var asked = pr.Other(asker);
                if (c.P(0.6))
                {
                    pr.lovers = true;
                    pr.a.partner = pr.b.id;
                    pr.b.partner = pr.a.id;
                    pr.fAnn = false;
                    pr.friends = 0f;
                    if (!rules) continue;
                    // Someone the new partner turned down, or left, lately: they lost out to the other one.
                    foreach (var x in here.Where(x => x != pr.a && x != pr.b))
                        foreach (var (beloved, winner) in new[] { (pr.a, pr.b), (pr.b, pr.a) })
                        {
                            var xd = c.PairOf(x, beloved).From(x);
                            if ((xd.rebuffedBy == beloved.id && day - xd.rebuffDay <= 30) || (xd.dumpedBy == beloved.id && day - xd.dumpDay <= 30))
                                Grudge(c, c.PairOf(x, winner), x, BondsMath.WonTheirHeart, "love", day);
                        }
                }
                else
                {
                    pr.From(asker).Add(-10f, day, 15);
                    pr.From(asked).Add(-5f, day, 15);
                    pr.From(asker).rebuffedBy = asked.id;
                    pr.From(asker).rebuffDay = day;
                    if (rules) pr.awkwardUntil = day + 10;
                }
            }
        }

        private static void Health(Colony c, List<Person> here, int day, bool rules)
        {
            var doctor = here.FirstOrDefault(p => p.doctor) ?? here[0];
            foreach (var p in here)
            {
                if (p == doctor || !c.P(1.0 / 45.0)) continue;
                if (!rules) continue;
                var pr = c.PairOf(p, doctor);
                pr.From(p).Add(10f, day, 30);
                pr.fam += 2f;
            }
            if (here.Count > 2 && c.P(1.0 / 200.0))
            {
                var patient = here.Where(p => p != doctor).OrderBy(_ => c.rnd.Next()).First();
                var pr = c.PairOf(patient, doctor);
                pr.From(patient).Add(-15f, day, 20);
                if (rules) Grudge(c, pr, patient, BondsMath.BotchedSurgery, "surgery", day);
            }
        }

        // ---------------- The switch on an old save ----------------

        private static void Migrate(Colony c, int day)
        {
            foreach (var pr in c.Present(day))
            {
                pr.fam = BondsMath.Seed(day - Math.Max(pr.a.joinDay, pr.b.joinDay), pr.friends);
                if (pr.comrades > 0f)
                {
                    pr.battles = BondsMath.SeedBattles(pr.comrades);
                    pr.lastBattle = day;
                }
                pr.renewed = day;
            }
        }

        // ---------------- What emerged ----------------

        private static void Snapshot(Colony c, Tally t, int day, bool rules, bool final)
        {
            var prs = c.Present(day).Where(p => !p.lovers).ToList();
            if (prs.Count == 0) return;
            string k = "d" + day + ":";
            t.Add(k + "pairs", prs.Count);
            t.Add(k + "friends", prs.Count(p => p.fAnn));
            t.Add(k + "good friends", prs.Count(p => p.fAnn && p.friends >= 45f));
            t.Add(k + "close friends", prs.Count(p => p.fAnn && p.friends >= 70f));
            t.Add(k + "rivals", prs.Count(p => p.rAnn));
            t.Add(k + "comrades", prs.Count(p => p.cAnn));
            t.Add(k + "comrades 70+", prs.Count(p => p.cAnn && p.comrades >= 70f));
            var ops = prs.SelectMany(p => new[] { Opinion(c, p, p.a, day, rules), Opinion(c, p, p.b, day, rules) }).ToList();
            t.Add(k + "opinion mean", ops.Average());
            float mean = ops.Average();
            t.Add(k + "opinion spread", (float)Math.Sqrt(ops.Average(o => (o - mean) * (o - mean))));
            t.Add(k + "both 20+ (game's Friend)", prs.Count(p => Math.Min(Opinion(c, p, p.a, day, rules), Opinion(c, p, p.b, day, rules)) >= 20f));
            t.Add(k + "both 60+", prs.Count(p => Math.Min(Opinion(c, p, p.a, day, rules), Opinion(c, p, p.b, day, rules)) >= 60f));
            t.Add(k + "cool (both under 10)", prs.Count(p => Math.Max(Opinion(c, p, p.a, day, rules), Opinion(c, p, p.b, day, rules)) < 10f));
            t.Add(k + "either -20 or less", prs.Count(p => Math.Min(Opinion(c, p, p.a, day, rules), Opinion(c, p, p.b, day, rules)) <= -20f));
            t.Add(k + "lovers", c.Present(day).Count(p => p.lovers));
            if (rules)
            {
                t.Add(k + "know each other (30+)", prs.Count(p => p.fam >= BondsMath.KnowAt));
                t.Add(k + "know each other well (60+)", prs.Count(p => p.fam >= BondsMath.WellAt));
                t.Add(k + "don't click (friendly at best)", prs.Count(p => p.aff <= BondsMath.WorseAt));
            }
            var newcomers = c.people.Where(p => p.joinDay > 0 && p.joinDay <= day - 30).ToList();
            foreach (var n in newcomers)
            {
                var firsts = c.pairs.Where(p => (p.a == n || p.b == n) && p.formedDay >= 0).Select(p => p.formedDay - n.joinDay).ToList();
                if (firsts.Count > 0) t.Add(k + "newcomer: days to a first friend", firsts.Min());
                t.Add(k + "newcomer: no friend yet (share)", firsts.Count > 0 ? 0f : 1f);
            }
            // The same pairs either way (how they get on is worked out the same), so the two rules compare.
            {
                var poor = prs.Where(p => p.aff <= BondsMath.PoorAt).ToList();
                var good = prs.Where(p => p.aff >= BondsMath.GoodAt).ToList();
                if (poor.Count > 0)
                {
                    t.Add(k + "opinion, pairs who don't click", poor.Average(p => (Opinion(c, p, p.a, day, rules) + Opinion(c, p, p.b, day, rules)) / 2f));
                    t.Add(k + "friends among them", poor.Count(p => p.fAnn));
                }
                if (good.Count > 0) t.Add(k + "opinion, pairs who get on easily", good.Average(p => (Opinion(c, p, p.a, day, rules) + Opinion(c, p, p.b, day, rules)) / 2f));
            }
            if (final)
                foreach (var kv in c.causes) t.Add("cause:" + kv.Key, kv.Value);
        }

        private static void Report(string title, Tally t, int[] days = null)
        {
            Console.WriteLine(title);
            days ??= Checkpoints;
            var rows = new[]
            {
                "pairs", "friends", "good friends", "close friends", "rivals", "comrades", "comrades 70+", "both 20+ (game's Friend)", "both 60+", "cool (both under 10)",
                "either -20 or less", "opinion mean", "opinion spread", "lovers", "know each other (30+)", "know each other well (60+)", "don't click (friendly at best)",
                "newcomer: days to a first friend", "newcomer: no friend yet (share)", "opinion, pairs who don't click", "friends among them", "opinion, pairs who get on easily"
            };
            Console.WriteLine("  " + "".PadRight(32) + string.Join("", days.Select(d => ("day " + d).PadRight(20))));
            foreach (var r in rows)
                Console.WriteLine("  " + r.PadRight(32) + string.Join("", days.Select(d => t.Line("d" + d + ":" + r).PadRight(20))));
            var causes = t.values.Keys.Where(k => k.StartsWith("cause:")).OrderBy(k => k).ToList();
            if (causes.Count > 0)
                Console.WriteLine("  rivalries told over two years, by cause (per colony): " + string.Join(", ", causes.Select(k => k.Substring(6) + " " + (t.values[k].Sum() / Math.Max(1, t.runs)).ToString("0.00"))));
            Console.WriteLine();
        }
    }
}
