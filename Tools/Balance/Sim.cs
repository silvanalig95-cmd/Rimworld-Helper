using ColonyCouncil;

namespace Balance
{
    /// <summary>How a letter or message reaches the player.</summary>
    public enum Channel
    {
        /// <summary>A letter with a question (a choice letter): dilemmas, events with answers, calendar days, votes.</summary>
        Decision,
        /// <summary>A wish voiced: a letter that asks for something without buttons.</summary>
        Request,
        /// <summary>A letter that only tells.</summary>
        Letter,
        /// <summary>A message on screen as it happens.</summary>
        Message,
        /// <summary>A line in the evening's colony news.</summary>
        Digest
    }

    public sealed class Mem
    {
        public ThoughtInfo def;
        public string src;
        public float own;
        public int age;
        public int dur;
        public bool good;
    }

    public sealed class Ail
    {
        public AilmentInfo info;
        public int until;
        public float mood;
    }

    public sealed class Wish
    {
        public WishInfo def;
        public Colonist who;
        public int created, expires, grantAt = -1;
        public bool promised;
        public int grantedSince;
    }

    public sealed class Colonist
    {
        public int id;
        public string role;
        public bool ascetic, greedy;
        public float vanillaGood, vanillaBad;
        public float standards, trust;
        public float mood = 0.6f;
        public readonly List<int> grants = new List<int>();
        public readonly List<Mem> mems = new List<Mem>();
        public readonly List<Ail> ails = new List<Ail>();
        public readonly Dictionary<string, int> ailLast = new Dictionary<string, int>();
        public readonly Dictionary<string, int> momentLast = new Dictionary<string, int>();
        public readonly Dictionary<string, int> nuisanceLast = new Dictionary<string, int>();
        public readonly Dictionary<string, int> wishCooldown = new Dictionary<string, int>();
        public List<Colonist> friends = new List<Colonist>();
        /// <summary>Wishes a lasting discomfort makes them voice (sleeping in barracks, eating off the floor, the dark).</summary>
        public List<WishInfo> discomforts = new List<WishInfo>();
        public int lastMoment = -9999, lastNuisance = -9999, lastDream = -9999, lastTrigger = -9999;
        public bool triggerToday;
        public Wish open;
        // Steady and daily states
        public int sameMeal, sameWork, crowded, unfinished, missing, waysStage = -1, strain;
        public bool varied, quiet, researchIdle, envy, worry, inReach, pride, heirloom;
        public int anniversary;
        public int convalescentUntil = -1, convalescentFrom = -1;

        public bool Ill => ails.Count > 0;
    }

    public sealed class LetterRec
    {
        public int hour;
        public string system;
        public Channel channel;
    }

    /// <summary>One colonist's mood from Rimfolk at one moment.</summary>
    public struct Sample
    {
        public float good, goodRaw, bad, steadyGood, steadyBad, mood;
        public int goodCount;
        public float trust;
        public bool ill, illNotQuiet;
    }

    /// <summary>
    /// One colony, one settled year: Rimfolk's systems hour by hour, at the rates of the code and <see cref="Rates"/>,
    /// with every mood sized and timed from the defs and weighed by the mod's own rules (GoodTimesMath: the shared
    /// budget, fading sooner in good times, standards).
    /// </summary>
    public sealed class Colony
    {
        public const int WarmupDays = 30, YearDays = 60;
        /// <summary>BALANCE_DETAIL=1: the report's breakdown names every mood instead of its source.</summary>
        public static readonly bool Detail = Environment.GetEnvironmentVariable("BALANCE_DETAIL") == "1";
        private readonly DefSet defs;
        private readonly Settings set;
        private readonly Rates.Style style;
        private readonly Random rng;
        public readonly List<Colonist> people = new List<Colonist>();
        public readonly List<LetterRec> letters = new List<LetterRec>();
        public readonly List<(int day, Colonist c, Sample s)> samples = new List<(int, Colonist, Sample)>();
        public readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        /// <summary>What each source adds to one colonist's mood, summed over every sample (divide by <see cref="partSamples"/>).</summary>
        public readonly Dictionary<string, double> partSum = new Dictionary<string, double>();
        public int partSamples;
        /// <summary>The system giving moods right now, for the report's breakdown.</summary>
        private string source = "other";
        private Dictionary<string, double> parts;
        public float ailmentDays;
        /// <summary>Days with each ailment, all colonists together (for the report's list of what fills the time).</summary>
        public readonly Dictionary<string, double> ailmentDaysBy = new Dictionary<string, double>();
        private int hour;
        private readonly List<Wish> wishes = new List<Wish>();
        private readonly Dictionary<string, int> defLast = new Dictionary<string, int>();
        private int nextDilemma = -1, lastDilemma = -99999, dilemmaPending = -1, lastThreat = -99999, nextThreat;
        private int eventPending = -1, nextOwnEvent = -1;
        private string lastGroup;
        private int socialToday;
        private int nextMeeting, ambitionUntil = -1, lastMeetingHour = -99999;
        private bool ambitionWillSucceed;
        private Colonist proposer;
        private int campUntil = -1;
        private int lastStoryDay = -9999;
        private readonly List<int> storyStarts = new List<int>();
        private readonly List<(int until, Colonist who, bool well)> stories = new List<(int, Colonist, bool)>();
        private readonly List<(int start, int until, SituationInfo def, List<int> beats)> situations = new List<(int, int, SituationInfo, List<int>)>();
        private int nextSituation;
        private readonly List<(int dayOfYear, string kind)> calendar = new List<(int, string)>();
        private int lastDecreeRequest = -9999;
        private readonly Dictionary<string, int> catchingLast = new Dictionary<string, int>();
        private readonly int startHour;

        public Colony(DefSet defs, Settings set, Rates.Style style, int n, int seed)
        {
            this.defs = defs;
            this.set = set;
            this.style = style;
            rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                var c = new Colonist
                {
                    id = i,
                    role = PickRole(),
                    ascetic = rng.NextDouble() < 0.05,
                    greedy = rng.NextDouble() < 0.12,
                    vanillaGood = style.vanillaGood + Gauss() * style.vanillaSpread * 0.6f,
                    vanillaBad = style.vanillaBad + Gauss() * style.vanillaSpread * 0.4f
                };
                people.Add(c);
            }
            foreach (var c in people)
            {
                var others = people.Where(o => o != c).OrderBy(_ => rng.Next()).ToList();
                int k = (int)Rates.FriendsPerColonist + (rng.NextDouble() < Rates.FriendsPerColonist % 1f ? 1 : 0);
                c.friends = others.Take(Math.Min(k, others.Count)).ToList();
                c.anniversary = rng.Next(YearDays);
                c.pride = Chance(style.traitPride);
                c.heirloom = Chance(style.heirloom);
                var triggered = defs.wishes.Where(w => w.triggered).OrderBy(_ => rng.Next()).ToList();
                int d = Poisson(style.discomforts);
                c.discomforts = triggered.Take(Math.Min(d, triggered.Count)).ToList();
                double w = rng.NextDouble(), acc = 0;
                for (int s = 0; s < style.ways.Length; s++)
                {
                    acc += style.ways[s];
                    if (w < acc)
                    {
                        c.waysStage = s;
                        break;
                    }
                }
            }
            int days = Rates.CalendarDays(n);
            string[] kinds = { "Remembered", "Remembered", "Birthday", "Birthday", "FeastDay", "GamesDay", "RestDay", "FeastDay", "Remembered", "GamesDay", "Birthday" };
            for (int i = 0; i < days; i++) calendar.Add((rng.Next(YearDays), kinds[i % kinds.Length]));
            startHour = 0;
            nextThreat = (int)(Exp(Rates.BigThreatDays) * 24);
            nextMeeting = (int)(rng.NextDouble() * set.F("daysBetweenMeetings", 15) * 24);
            nextSituation = (int)(rng.NextDouble() * set.F("daysBetweenWorldSituations", 45) * 24);
        }

        // ---------------- Helpers ----------------

        private float Gauss()
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
        }

        private int Poisson(float mean)
        {
            double l = Math.Exp(-mean), p = 1.0;
            int k = 0;
            while (true)
            {
                p *= rng.NextDouble();
                if (p < l) return k;
                k++;
            }
        }

        private float Exp(float mean) => (float)(-Math.Log(1.0 - rng.NextDouble()) * mean);
        private bool Chance(float p) => rng.NextDouble() < p;
        private float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        private T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];
        private int Day => hour / 24;
        private int HourOfDay => hour % 24;
        private int Quadrum => Day % YearDays / 15;
        private string Season => new[] { "Spring", "Summer", "Fall", "Winter" }[Quadrum];
        private bool Measuring => Day >= WarmupDays;
        private bool Awake => HourOfDay >= Rates.WakingFrom && HourOfDay < Rates.WakingTo;

        private string PickRole()
        {
            double r = rng.NextDouble(), acc = 0;
            foreach (var (role, share) in Rates.Roles)
            {
                acc += share;
                if (r < acc) return role;
            }
            return Rates.Roles[0].role;
        }

        private T Weighted<T>(IList<T> list, Func<T, float> weight)
        {
            float total = 0f;
            foreach (var x in list) total += Math.Max(0f, weight(x));
            if (total <= 0f) return default;
            double r = rng.NextDouble() * total;
            foreach (var x in list)
            {
                r -= Math.Max(0f, weight(x));
                if (r <= 0) return x;
            }
            return list[list.Count - 1];
        }

        private void Count(string what, int n = 1)
        {
            if (!Measuring) return;
            counts.TryGetValue(what, out int v);
            counts[what] = v + n;
        }

        private int lastQuestion = -99999;

        /// <summary>LetterGap.Open: a question that can wait doesn't come within the gap after Rimfolk's last one.</summary>
        private bool GapOpen => Code.LetterGapHours <= 0f || hour - lastQuestion >= Code.LetterGapHours;

        private void Send(string system, Channel ch)
        {
            if (ch == Channel.Decision) lastQuestion = hour;
            if (Measuring) letters.Add(new LetterRec { hour = hour - WarmupDays * 24, system = system, channel = ch });
        }

        /// <summary>A minor piece of news: into the evening's colony news, or nothing at the default message level when it's flavour.</summary>
        private void News(string system, bool notable)
        {
            if (!notable && set.S("messageLevel") != "Everything") return;
            if (notable && set.S("messageLevel") == "LettersOnly") return;
            Send(system, set.S("colonyNews") == "AsItHappens" ? Channel.Message : Channel.Digest);
        }

        private float LiveScale(Colonist c) => set.RewardFactor * (1f - 0.5f * c.standards);
        private float BadScale(Colonist c) => 1f + 0.4f * c.standards;

        /// <summary>How near breaking (PressureMath.HardTimes), with the game's usual minor-break line.</summary>
        private static float Hard(Colonist c) => PressureMath.HardTimes(c.mood, Rates.MinorBreak);

        // ---------------- Moods ----------------

        /// <summary>CouncilGameComponent.GiveThought: rewards and penalties, scaled by the settings and the colonist's standards.</summary>
        private void Give(Colonist c, string thought, float scale = -1f, int stage = 0)
        {
            if (!set.MoodEffects) return;
            var t = defs.T(thought);
            if (t == null || t.Situational || t.rewardInstead) return;
            float own = t.Stage(stage);
            if (own == 0f) return;
            float factor = scale >= 0f ? scale : own > 0f ? set.RewardFactor : set.PenaltyScale;
            if (factor <= 0f) return;
            factor *= own > 0f ? 1f - 0.5f * c.standards : 1f + 0.4f * c.standards;
            AddMemory(c, t, own * factor);
        }

        /// <summary>A memory given as it is, or with a scale of its own (dreams in full; moments live).</summary>
        private void AddMemory(Colonist c, ThoughtInfo t, float own)
        {
            if (own == 0f || t.durationDays <= 0f) return;
            int dur = (int)Math.Round(t.durationDays * 24);
            bool good = own > 0f;
            if (good && set.B("goodTimesFade")) dur = GoodTimesMath.ShortenedTicks(dur, GoodTimesMath.DurationFactor(c.mood));
            int same = 0;
            Mem oldest = null;
            foreach (var m in c.mems)
            {
                if (m.def != t) continue;
                same++;
                if (oldest == null || m.age > oldest.age) oldest = m;
            }
            if (same >= Math.Max(1, t.stackLimit) && oldest != null)
            {
                oldest.age = 0;
                oldest.dur = dur;
                oldest.own = own;
                oldest.src = source;
                return;
            }
            c.mems.Add(new Mem { def = t, src = source, own = own, dur = dur, good = good });
        }

        /// <summary>A setback: the group's setting, standards, and never less than a whole point (SetbackMath.AtLeastOnePoint).</summary>
        private void Setback(Colonist c, string thought, int stage = 0)
        {
            string was = source;
            source = "setbacks";
            try
            {
                SetbackInner(c, thought, stage);
            }
            finally
            {
                source = was;
            }
        }

        private void SetbackInner(Colonist c, string thought, int stage)
        {
            var t = defs.T(thought);
            if (t == null || !set.MoodEffects) return;
            string group = t.setbackGroup ?? "Life";
            float factor;
            switch (group)
            {
                case "Council":
                    if (!set.Negatives) return;
                    factor = set.PenaltyScale;
                    break;
                case "Friction":
                    if (!set.B("frictions")) return;
                    factor = PressureMath.LetdownFactor(set.Life) * PressureMath.HardTimesLetdown(Hard(c));
                    break;
                default:
                    if (t.notGentle && set.Life == LifePressure.Gentle) return;
                    factor = PressureMath.Factor(set.Life);
                    break;
            }
            float own = t.Stage(stage);
            factor = SetbackMath.AtLeastOnePoint(own, factor * BadScale(c));
            AddMemory(c, t, own * factor);
        }

        private void Trust(Colonist c, float delta)
        {
            if (Math.Abs(delta) < 0.01f) return;
            c.trust = Math.Clamp(c.trust + delta, -100f, 100f);
        }

        /// <summary>The trust mood: bitter, doubtful, trusting, devoted (Standing.cs).</summary>
        private float TrustMood(Colonist c)
        {
            if (!set.B("enableWishes") || !set.MoodEffects) return 0f;
            var t = defs.T("Council_Trust");
            if (t == null) return 0f;
            if (c.trust <= -Code.TrustBitter) return set.Negatives ? t.Stage(0) : 0f;
            if (c.trust <= -Code.TrustDoubtful) return set.Negatives ? t.Stage(1) : 0f;
            if (c.trust >= Code.TrustDevoted) return t.Stage(3) * LiveScale(c);
            if (c.trust >= Code.TrustTrusting) return t.Stage(2) * LiveScale(c);
            return 0f;
        }

        private float Pressure(string thought, int stage, Colonist c)
        {
            if (stage <= 0) return 0f;
            var t = defs.T(thought);
            if (t == null) return 0f;
            return PressureMath.Scale(t.Stage(stage - 1), PressureMath.Factor(set.Life) * BadScale(c) * PressureMath.HardTimesPressure(Hard(c)));
        }

        private float SetbackSituational(string thought, int stage, Colonist c)
        {
            var t = defs.T(thought);
            if (t == null || !set.MoodEffects) return 0f;
            string group = t.setbackGroup ?? "Life";
            float factor = group == "Council" ? set.PenaltyScale
                : group == "Friction" ? (set.B("frictions") ? PressureMath.LetdownFactor(set.Life) * PressureMath.HardTimesLetdown(Hard(c)) : 0f)
                : PressureMath.Factor(set.Life) * PressureMath.HardTimesPressure(Hard(c));
            if (factor <= 0f) return 0f;
            float own = t.Stage(stage);
            return own * SetbackMath.AtLeastOnePoint(own, factor * BadScale(c));
        }

        /// <summary>The steady (situational) moods: trust, the colony's ways, pressures, waiting and worrying, ailments, strain.</summary>
        private (float good, float bad) Steady(Colonist c)
        {
            float good = 0f, bad = 0f;
            string part = "trust";
            void Add(float v)
            {
                if (v > 0f) good += v;
                else bad += v;
                if (parts != null && v != 0f) parts[part] = parts.GetValueOrDefault(part) + v;
            }
            Add(TrustMood(c));
            if (set.MoodEffects)
            {
                part = "the colony's ways";
                if (set.B("colonyCharacter") && c.waysStage >= 0)
                {
                    var w = defs.T("Council_ColonyWays");
                    float v = w?.Stage(c.waysStage) ?? 0f;
                    Add(v > 0f ? v * LiveScale(c) : set.Negatives ? v * set.PenaltyScale : 0f);
                }
                string Pr(string name) => Detail ? "pressures: " + name : "pressures";
                part = Pr("same meal");
                Add(Pressure("Council_Pressure_SameMeal", c.sameMeal, c));
                part = Pr("same work");
                Add(Pressure("Council_Pressure_SameWork", c.sameWork, c));
                part = Pr("crowded");
                Add(Pressure("Council_Pressure_Crowded", c.crowded, c));
                part = Pr("unfinished");
                Add(Pressure("Council_Pressure_Unfinished", set.Life == LifePressure.Gentle ? 0 : c.unfinished, c));
                part = Pr("missing");
                Add(Pressure("Council_Pressure_Missing", c.missing, c));
                part = Pr("varied table");
                if (c.varied) Add(defs.T("Council_Pressure_VariedTable")?.First ?? 0f);
                part = "setbacks (lasting)";
                string Sub(string name) => Detail ? "setbacks (lasting): " + name : "setbacks (lasting)";
                part = Sub("quiet days");
                if (c.quiet) Add(SetbackSituational("Council_Setback_QuietDays", 0, c));
                part = Sub("research idle");
                if (c.researchIdle) Add(SetbackSituational("Council_Setback_ResearchIdle", 0, c));
                part = Sub("bedroom envy");
                if (c.envy && set.B("jealousy")) Add(SetbackSituational("Council_Setback_BedroomEnvy", 0, c));
                part = Sub("worries");
                if (c.worry) Add(SetbackSituational("Council_Setback_CaravanTrouble", 0, c));
                part = Sub("still waiting");
                if (c.open != null && set.Negatives)
                {
                    int st = SetbackMath.WaitStage((hour - c.open.created) / 24f, c.open.grantedSince);
                    if (st >= 0) Add(SetbackSituational("Council_Setback_StillWaiting", st, c));
                }
                part = Sub("sick friend");
                if (c.friends.Any(f => f.ails.Any(a => !a.info.quiet))) Add(SetbackSituational("Council_Setback_SickFriend", 0, c));
                part = "life and honours";
                if (c.pride && set.B("colonyCharacter")) Add((defs.T("Council_ColonyTraitPride")?.First ?? 0f) * LiveScale(c));
                if (c.heirloom && set.B("heirlooms")) Add(defs.T("Council_Heirloom")?.Stage(1) ?? 0f);
                part = "life goals";
                if (c.inReach && set.B("aspirations")) Add((defs.T("Council_Asp_InReach")?.First ?? 0f) * LiveScale(c));
                part = "strain";
                if (set.B("heart") && c.strain > 0) Add(defs.T("Council_Heart_Strain")?.Stage(c.strain - 1) ?? 0f);
                part = "health";
                if (c.convalescentFrom >= 0 && hour >= c.convalescentFrom && hour < c.convalescentUntil)
                    Add(defs.T("Council_Health_WeakFrustrated")?.First ?? 0f);
            }
            // Ailments: their own moods, by stage, whatever the stakes.
            part = "ailments";
            foreach (var a in c.ails) Add(a.mood);
            return (good, bad);
        }

        /// <summary>
        /// The colonist's Rimfolk memories now: good ones weighed against each other (SmallJoys: the two biggest in full,
        /// then GoodTimesMath.RankFactor), each mood group stacked as the game stacks it.
        /// </summary>
        private (float good, float goodRaw, float bad, int goodCount) Memories(Colonist c)
        {
            var goods = c.mems.Where(m => m.good).OrderByDescending(m => m.own).ToList();
            var factor = new Dictionary<Mem, float>();
            bool budget = set.B("smallJoysDiminish");
            for (int r = 0; r < goods.Count; r++) factor[goods[r]] = budget ? GoodTimesMath.RankFactor(r) : 1f;
            float good = 0f, raw = 0f, bad = 0f;
            foreach (var g in c.mems.GroupBy(m => m.def))
            {
                var list = g.ToList();
                float mult = 1f, total = 0f, felt = 0f, plain = 0f;
                foreach (var m in list)
                {
                    felt += m.own * (m.good ? factor[m] : 1f);
                    plain += m.own;
                    total += mult;
                    mult *= g.Key.stackMult;
                }
                float avgFelt = felt / list.Count * total, avgRaw = plain / list.Count * total;
                if (parts != null)
                    foreach (var m in list)
                    {
                        string key = Detail ? m.src + ": " + m.def.name : m.src + (m.good ? "" : " (bad)");
                        parts[key] = parts.GetValueOrDefault(key) + m.own * (m.good ? factor[m] : 1f) / list.Count * total;
                    }
                if (list[0].good)
                {
                    good += avgFelt;
                    raw += avgRaw;
                }
                else bad += avgFelt;
            }
            return (good, raw, bad, goods.Count);
        }

        private void UpdateMood(Colonist c, bool record)
        {
            parts = record && Measuring ? new Dictionary<string, double>() : null;
            var (good, raw, bad, n) = Memories(c);
            var (sg, sb) = Steady(c);
            if (parts != null)
            {
                foreach (var kv in parts) partSum[kv.Key] = partSum.GetValueOrDefault(kv.Key) + kv.Value;
                partSamples++;
                parts = null;
            }
            float comfort = set.B("standardsCoverComforts") && set.B("risingStandards") ? GoodTimesMath.ComfortFactor((int)Math.Min(3, c.standards * 4f)) : 1f;
            float vanilla = c.vanillaGood * comfort + c.vanillaBad;
            c.mood = Math.Clamp(0.5f + (vanilla + good + bad + sg + sb) / 100f, 0f, 1f);
            if (!record || !Measuring) return;
            samples.Add((Day - WarmupDays, c, new Sample
            {
                good = good, goodRaw = raw, bad = bad, steadyGood = sg, steadyBad = sb, mood = c.mood, goodCount = n, trust = c.trust,
                ill = c.ails.Any(a => !a.info.serious), illNotQuiet = c.ails.Any(a => !a.info.quiet && !a.info.serious)
            }));
        }

        // ---------------- The year ----------------

        public void Run()
        {
            int total = (WarmupDays + YearDays) * 24;
            for (hour = startHour; hour < total; hour++)
            {
                if (HourOfDay == 0) Daily();
                Wishes();
                Dilemmas();
                Events();
                Encounters();
                Social();
                if (Awake)
                {
                    Moments();
                    Nuisances();
                }
                if (HourOfDay == Rates.WakingFrom) Dreams();
                Ailments();
                Meetings();
                Situations();
                foreach (var c in people)
                {
                    for (int i = c.mems.Count - 1; i >= 0; i--)
                        if (++c.mems[i].age >= c.mems[i].dur)
                            c.mems.RemoveAt(i);
                    for (int i = c.ails.Count - 1; i >= 0; i--)
                        if (hour >= c.ails[i].until)
                            EndAilment(c, c.ails[i]);
                    if (Measuring && c.ails.Any(a => !a.info.serious))
                    {
                        ailmentDays += 1f / 24f;
                        foreach (var a in c.ails) ailmentDaysBy[a.info.name] = ailmentDaysBy.GetValueOrDefault(a.info.name) + 1.0 / 24.0;
                    }
                    UpdateMood(c, HourOfDay % 4 == 2);
                }
            }
        }

        private void Daily()
        {
            int day = Day;
            foreach (var c in people)
            {
                // Trust drifts back (StandingTracker.Daily); recent grants are forgotten after 30 days.
                c.trust *= Code.TrustDecay;
                c.grants.RemoveAll(t => hour - t > 30 * 24);
                // Standards (Standards.Daily).
                if (set.B("risingStandards"))
                {
                    float rate;
                    if (c.mood >= 0.7f)
                    {
                        rate = c.mood >= 0.85f ? Code.StandardsUpHigh : Code.StandardsUp;
                        if (c.ascetic) rate *= 0.3f;
                        if (c.greedy) rate *= 1.5f;
                    }
                    else if (c.mood < 0.4f) rate = -Code.StandardsDown;
                    else rate = -Code.StandardsDrift;
                    c.standards = Math.Clamp(c.standards + rate, 0f, 1f);
                }
                else c.standards = 0f;
                // Today's pressures and worries.
                c.sameMeal = PickStage(style.sameMeal);
                c.sameWork = PickStage(style.sameWork);
                c.crowded = PickStage(style.crowded);
                c.unfinished = PickStage(style.unfinished);
                c.missing = PickStage(style.missing);
                c.varied = Chance(style.variedTable);
                if (day % 5 == 0)
                {
                    c.quiet = Chance(style.quietDays);
                    c.researchIdle = c.role == "Researcher" && Chance(style.researchIdle * 3f);
                    c.envy = Chance(style.bedroomEnvy);
                    c.worry = Chance(style.worries);
                    c.inReach = set.B("aspirations") && Chance(0.08f);
                    float s = (float)rng.NextDouble();
                    c.strain = s < style.strain[2] ? 3 : s < style.strain[2] + style.strain[1] ? 2 : s < style.strain.Sum() ? 1 : 0;
                }
                c.triggerToday = c.discomforts.Any(_ => Chance(Rates.DiscomfortDays));
                // Setbacks from what happens in play.
                foreach (var (thought, stage, perDay, role) in Rates.Setbacks)
                {
                    if (role != null && c.role != role) continue;
                    float f = Rates.CareSetbacks.Contains(thought) ? style.setbackFactor * style.setbackFactor : style.setbackFactor;
                    if (Chance(perDay * f)) Setback(c, thought, stage);
                }
                // Convalescence (Health/): after an illness, an operation or a long bed rest.
                if (set.B("convalescence") && c.convalescentUntil < hour && Chance(style.convalescencePerYear / YearDays))
                {
                    int len = (int)(Range(4f, 10f) * 24);
                    c.convalescentFrom = hour + len / 2;
                    c.convalescentUntil = hour + len;
                    News("health", true);
                    Count("convalescence");
                }
                // A life goal fulfilled (Aspirations/).
                if (set.B("aspirations") && Chance(style.aspirationsPerYear / YearDays))
                {
                    source = "life goals";
                    Give(c, "Council_Asp_Fulfilled");
                    Trust(c, 10f);
                    Send("people", Channel.Letter);
                }
                AilmentDay(c);
            }
            Calendar();
            Stories();
            Decrees();
            Faith();
            Camps();
            OtherLetters();
            LifeAndHonours();
        }

        private int nextTouch = -1;

        /// <summary>Personal touches built, a year in the colony remembered, a name given, a seat on the council.</summary>
        private void LifeAndHonours()
        {
            source = "life and honours";
            if (set.B("enablePersonalTouches"))
            {
                float gap = set.F("daysBetweenPersonalTouches", 6);
                if (nextTouch < 0) nextTouch = Day + (int)(gap * Range(0.5f, 1f));
                if (Day >= nextTouch)
                {
                    nextTouch = Day + Math.Max(1, (int)Math.Round(gap * Range(0.6f, 1.4f)));
                    Send("colony", Channel.Message);
                    Count("personal touches");
                    if (Chance(style.touchBuilt)) Give(Pick(people), "Council_PersonalTouchDone");
                }
            }
            if (set.B("anniversaries"))
                foreach (var c in people.Where(p => p.anniversary == Day % YearDays))
                {
                    Give(c, "Council_Anniversary");
                    Trust(c, 3f);
                    News("people", true);
                }
            if (set.B("epithets") && Chance(Rates.EpithetsPerYear / YearDays))
            {
                var who = Pick(people);
                Give(who, "Council_Epithet");
                News("people", true);
                foreach (var r in people.Where(p => p != who && Chance(Rates.RivalShare))) Setback(r, "Council_Setback_RivalHonoured");
            }
            if (set.B("offices") && Chance(1f / YearDays))
            {
                var who = Pick(people);
                Give(who, "Council_ElectedToOffice");
                foreach (var r in people.Where(p => p != who && Chance(Rates.RivalShare))) Setback(r, "Council_Setback_RivalHonoured");
            }
        }

        private int PickStage(float[] shares)
        {
            double r = rng.NextDouble(), acc = 0;
            for (int i = 0; i < shares.Length; i++)
            {
                acc += shares[i];
                if (r < acc) return i;
            }
            return 0;
        }

        // ---------------- Wishes ----------------

        private float Patience(Colonist c) => Math.Clamp(1f + c.trust / 200f, 0.6f, 1.4f) * (1f - 0.3f * c.standards);

        private void Wishes()
        {
            source = "wishes";
            if (!set.B("enableWishes")) return;
            // Event wishes: a discomfort makes someone speak up within hours (CouncilDynamics.CheckTriggers).
            if (set.B("eventWishes") && Awake && wishes.Count < (int)set.F("maxOpenWishes", 5) + 1)
            {
                var askers = people.Where(c => c.triggerToday && c.open == null && hour - c.lastTrigger > 36).ToList();
                if (askers.Count > 0 && Chance(Code.EventWishChance))
                {
                    var c = Pick(askers);
                    c.lastTrigger = hour;
                    c.triggerToday = false;
                    var pool = c.discomforts.Where(w => !OnCooldown(c, w)).ToList();
                    if (pool.Count > 0) NewWish(c, Weighted(pool, w => w.weight), "event wishes");
                }
            }
            // The council's own rhythm (CouncilGameComponent.TryGenerateWish).
            float rateFactor = 1f + 0.6f * people.Average(c => set.B("risingStandards") ? c.standards : 0f);
            if (Chance(rateFactor / (Math.Max(0.25f, set.F("daysBetweenWishes", 2)) * 24f)) && wishes.Count < (int)set.F("maxOpenWishes", 5))
            {
                var free = people.Where(c => c.open == null).ToList();
                if (free.Count > 0)
                {
                    var c = Weighted(free, p => Personality(p) * TrustFactor(p));
                    var pool = defs.wishes.Where(w => !OnCooldown(c, w) && w.weight > 0f).ToList();
                    if (pool.Count > 0)
                        NewWish(c, Weighted(pool, w => w.weight * w.gate * (w.luxury ? (c.ascetic ? 0.1f : 1f) * (1f + 1.5f * c.standards) : 1f)), "wishes");
                }
            }
            foreach (var w in wishes.ToList())
            {
                if (w.grantAt >= 0 && hour >= w.grantAt) Grant(w);
                else if (hour >= w.expires) Expire(w);
            }
        }

        private bool OnCooldown(Colonist c, WishInfo w) => c.wishCooldown.TryGetValue(w.name, out int until) && until > hour;

        private static float Personality(Colonist c) => (c.greedy ? 1.25f : 1f) * (c.mood < 0.35f ? 1.3f : c.mood > 0.8f ? 0.8f : 1f);

        private float TrustFactor(Colonist c)
        {
            float f = c.trust <= -Code.TrustBitter ? 0.5f : c.trust <= -Code.TrustDoubtful ? 1.25f : c.trust >= Code.TrustDevoted ? 0.85f : 1f;
            return f * (1f + 0.12f * c.grants.Count(t => hour - t < 15 * 24));
        }

        private void NewWish(Colonist c, WishInfo def, string system)
        {
            if (def == null) return;
            var w = new Wish { def = def, who = c, created = hour };
            w.expires = hour + (int)(def.expiryDays * set.F("wishDurationFactor", 1f) * Patience(c) * 24f);
            if (Chance(style.promise))
            {
                w.promised = true;
                Trust(c, Code.TrustPromised);
                w.expires += 48;
            }
            bool grant = w.promised ? Chance(style.promiseKept) : Chance(style.grant);
            if (grant) w.grantAt = w.created + (int)((w.expires - w.created) * Range(style.grantFrom, style.grantTo));
            c.open = w;
            wishes.Add(w);
            Count("wishes made");
            Send(system, set.B("wishLetters") ? Channel.Request : Channel.Message);
        }

        private void Grant(Wish w)
        {
            var c = w.who;
            wishes.Remove(w);
            c.open = null;
            // SetbackTracker.Waiting: only a wish voiced after theirs and granted first counts, and not for a promised one.
            foreach (var o in wishes)
                if (o.created < w.created && !o.promised)
                    o.grantedSince++;
            Give(c, w.def.grantedThought);
            Trust(c, w.promised ? Code.TrustGrantPromised : Code.TrustGrant);
            c.grants.Add(hour);
            if (w.promised) Give(c, "Council_PromiseKept");
            c.wishCooldown[w.def.name] = hour + (int)(w.def.cooldownDays * 24);
            Count("wishes granted");
            float inspire = w.def.inspirationChance + (c.mood >= 0.85f ? 0.45f : c.mood >= 0.7f ? 0.25f : c.mood >= 0.55f ? 0.1f : 0f) + (w.promised ? 0.1f : 0f);
            if (set.B("grantedLetters") || (set.MoodEffects && Chance(inspire))) Send("wishes", Channel.Letter);
            else News("wishes", true);
        }

        private void Expire(Wish w)
        {
            var c = w.who;
            wishes.Remove(w);
            c.open = null;
            float pen = set.PenaltyScale;
            float ent = Math.Min(2f, 1f + 0.2f * c.grants.Count(t => hour - t < 15 * 24));
            Trust(c, -Code.TrustIgnored * ent);
            if (w.promised) Trust(c, -Code.TrustBroken);
            if (pen > 0f)
            {
                Give(c, w.promised ? "Council_BrokenPromise" : "Council_WishIgnored", pen * ent);
                foreach (var f in people.Where(p => p != c && p.friends.Contains(c)))
                {
                    Trust(f, -2f);
                    // Setbacks.Notify_PromiseBroken: only a broken promise, and closer friends take it harder.
                    if (w.promised) Setback(f, "Council_Setback_FriendLetDown", Chance(0.3f) ? 1 : 0);
                }
            }
            c.wishCooldown[w.def.name] = hour + (int)(w.def.cooldownDays * 1.5f * 24);
            Count("wishes lapsed");
            News("wishes", true);
        }

        // ---------------- Answers: dilemmas, events, world events ----------------

        /// <summary>Who an effect reaches.</summary>
        private List<Colonist> Targets(Effect e, Colonist a, Colonist b)
        {
            IEnumerable<Colonist> list;
            switch (e.target)
            {
                case "A": list = a != null ? new[] { a } : new Colonist[0]; break;
                case "B": list = b != null ? new[] { b } : new Colonist[0]; break;
                case "AAndB": list = new[] { a, b }.Where(x => x != null); break;
                case "Colony": list = people; break;
                case "Others": list = people.Where(p => p != a && p != b); break;
                case "FriendsOfA": list = people.Where(p => p != a && p != b && a != null && (p.friends.Contains(a) || Chance(0.1f))); break;
                case "RivalsOfA": list = people.Where(p => p != a && p != b && Chance(0.12f)); break;
                default: list = new Colonist[0]; break;
            }
            return list.Where(_ => e.share >= 1f || Chance(e.share)).ToList();
        }

        private float TargetCount(Effect e)
        {
            int n = people.Count;
            float k = e.target switch
            {
                "A" => 1, "B" => 1, "AAndB" => 2, "Colony" => n, "Others" => Math.Max(0, n - 2), "FriendsOfA" => Rates.FriendsPerColonist, "RivalsOfA" => n * 0.12f, _ => 0
            };
            return k * e.share;
        }

        /// <summary>What an answer is worth to the colony's mood and trust, as a player who cares would weigh it.</summary>
        private float Value(Option o)
        {
            float Of(List<Effect> effects)
            {
                float v = 0f;
                foreach (var e in effects)
                {
                    if (e.thought != null)
                    {
                        var t = defs.T(e.thought);
                        if (t != null && !t.Situational) v += t.First * Math.Min(t.durationDays, 10f) / 5f * TargetCount(e);
                    }
                    if (e.cls == "DilemmaEffect_Trust") v += e.amount * 0.3f * TargetCount(e);
                }
                return v;
            }
            float main = Of(o.effects), risk = Of(o.riskEffects);
            return o.riskChance > 0f ? (1f - o.riskChance) * main + o.riskChance * ((o.riskReplaces ? 0f : main) + risk) : main;
        }

        private int Answer(OutcomeDef d)
        {
            if (d.options.Count <= 1) return 0;
            double r = rng.NextDouble();
            if (r < style.byDefault) return Math.Clamp(d.defaultOption, 0, d.options.Count - 1);
            if (r < style.byDefault + style.best)
            {
                int best = 0;
                float bv = float.MinValue;
                for (int i = 0; i < d.options.Count; i++)
                {
                    float v = Value(d.options[i]);
                    if (v > bv)
                    {
                        bv = v;
                        best = i;
                    }
                }
                return best;
            }
            return rng.Next(d.options.Count);
        }

        /// <summary>The answer's effects, for real: moods, trust, a gathering that plays out, a risk that comes true.</summary>
        private void Apply(OutcomeDef d, int index, Colonist a, Colonist b, string system)
        {
            source = system.StartsWith("dilemmas") ? "dilemmas" : system;
            var o = d.options[Math.Clamp(index, 0, d.options.Count - 1)];
            bool risk = o.riskChance > 0f && Chance(o.riskChance);
            var effects = risk ? (o.riskReplaces ? o.riskEffects : o.effects.Concat(o.riskEffects).ToList()) : o.effects;
            bool playsOut = false;
            foreach (var e in effects)
            {
                if (e.PlaysOut) playsOut = true;
                if (e.thought != null)
                    foreach (var c in Targets(e, a, b))
                        Give(c, e.thought);
                if (e.cls == "DilemmaEffect_Trust")
                    foreach (var c in Targets(e, a, b))
                        Trust(c, e.amount);
            }
            if (playsOut && set.B("answersPlayOut"))
            {
                // A gathering or a visit on the map: a message as it starts, another as it ends.
                Send("play-outs", Channel.Message);
                Send("play-outs", Channel.Message);
                Count("play-outs");
            }
            // Not everyone agreed with the answer (SetbackMath.ClashStage), and some said so when it went wrong.
            if (d.options.Count > 1)
                foreach (var c in people)
                {
                    double r = rng.NextDouble();
                    if (r < 0.05) Setback(c, "Council_Setback_NotAgreed", 1);
                    else if (r < 0.2) Setback(c, "Council_Setback_NotAgreed", 0);
                    if (risk && Chance(0.15f)) Setback(c, "Council_Setback_ToldThemSo");
                }
        }

        private (Colonist a, Colonist b) Cast()
        {
            var a = Pick(people);
            var b = people.Count > 1 ? Pick(people.Where(p => p != a).ToList()) : null;
            return (a, b);
        }

        private bool Fits(OutcomeDef d)
        {
            if (d.minColonists > people.Count) return false;
            if (d.seasons != null && d.seasons.Count > 0 && !d.seasons.Contains(Season)) return false;
            return !(defLast.TryGetValue(d.name, out int last) && hour - last < d.cooldownDays * 24);
        }

        /// <summary>How long until the council answers a question: soon when the colony is looked after, at the deadline when not.</summary>
        private int AnswerDelay(float answerDays) => (int)(Math.Max(0.25f, answerDays) * 24 * (style.byDefault > 0.5f ? 1f : style.best > 0.5f ? 0.2f : 0.5f));

        private void Dilemmas()
        {
            if (!set.B("enableDilemmas") || Day < Code.DilemmaStartDay) return;
            if (hour >= nextThreat)
            {
                lastThreat = hour;
                nextThreat = hour + (int)(Exp(Rates.BigThreatDays) * 24) + 24;
            }
            if (dilemmaPending > hour) return;
            if (nextDilemma < 0) nextDilemma = hour + DilemmaInterval();
            // The aftermath of a raid brings its own questions.
            if (hour - lastThreat > 6 && hour - lastThreat < 36 && hour - lastDilemma > Code.DilemmaGapDays * 24 && Chance(Code.DilemmaRaidChance))
                if (FireDilemma(d => d.withinDaysOfThreat > 0f, "dilemmas (after a raid)")) return;
            // Strangers' questions can't wait for the council's rhythm (EncounterTracker.Hourly).
            if (set.B("strangers") && hour - lastDilemma > Code.DilemmaGapDays * 24)
            {
                float quiet = 1f;
                var present = new HashSet<string>();
                foreach (var kv in Rates.Present)
                    if (Chance(kv.Key == "Pet" ? style.pets : kv.Value))
                    {
                        present.Add(kv.Key);
                        quiet *= 1f - Code.StrangerDilemmaHourly.GetValueOrDefault(kv.Key);
                    }
                if (Chance(1f - quiet) && FireDilemma(d => d.stranger && present.Contains(d.strangerKind ?? ""), "dilemmas (strangers)")) return;
            }
            if (hour < nextDilemma || !GapOpen) return;
            nextDilemma = FireDilemma(d => !d.stranger && d.withinDaysOfThreat <= 0f, "dilemmas") ? hour + DilemmaInterval() : hour + 24;
        }

        private int DilemmaInterval()
        {
            float mood = people.Average(c => c.mood);
            float factor = Math.Clamp(1.6f - mood, 0.6f, 1.3f);
            return (int)(set.F("daysBetweenDilemmas", 8) * factor * Range(0.7f, 1.3f) * 24);
        }

        private bool FireDilemma(Func<OutcomeDef, bool> only, string system)
        {
            if (!GapOpen) return false;
            var pool = defs.dilemmas.Where(d => only(d) && Fits(d)).ToList();
            foreach (var d in pool.OrderByDescending(d => d.weight * rng.NextDouble() * (d.seasons == null && d.withinDaysOfThreat <= 0f ? 1 : 3)))
            {
                if (!Chance(Rates.WorkerCasts * d.gate)) continue;
                defLast[d.name] = hour;
                lastDilemma = hour;
                dilemmaPending = hour + AnswerDelay(2f);
                Send(system, Channel.Decision);
                Count("dilemmas");
                var (a, b) = Cast();
                Apply(d, Answer(d), a, b, system);
                return true;
            }
            return false;
        }

        private float EventShare()
        {
            float w = Math.Clamp(set.F("storytellerEventWeight", 1f), 0.05f, 4f);
            float ours = defs.events.Where(e => !e.battle && e.weight > 0f).Sum(e => e.weight) * w;
            float share = ours / (ours + Rates.VanillaMiscWeight);
            return Math.Min(share, Code.EventMaxShare);
        }

        private void Events()
        {
            if (!set.B("worldEvents") || Day < 3) return;
            bool fire;
            if (set.B("eventsFromStoryteller")) fire = Chance(EventShare() / (Rates.StorytellerMiscMtbDays * 24f));
            else
            {
                if (nextOwnEvent < 0) nextOwnEvent = hour + (int)(Math.Max(0.5f, set.F("daysBetweenWorldEvents", 4)) * Range(0.6f, 1.4f) * 24);
                fire = hour >= nextOwnEvent && eventPending <= hour;
                if (fire) nextOwnEvent = hour + (int)(Math.Max(0.5f, set.F("daysBetweenWorldEvents", 4)) * Range(0.6f, 1.4f) * 24);
            }
            if (!fire) return;
            var pool = defs.events.Where(e => !e.battle && e.weight > 0f && Day >= e.earliestDay && Fits(e) && (!e.asks || GapOpen)).ToList();
            for (int tries = 0; tries < 10 && pool.Count > 0; tries++)
            {
                var d = Weighted(pool, e => e.weight * (e.group == lastGroup ? Code.EventSameGroup : 1f));
                pool.Remove(d);
                if (!Chance(Rates.Castable)) continue;
                defLast[d.name] = hour;
                lastGroup = d.group;
                string system = d.worker != null && d.worker.StartsWith("HappeningWorker_Mission") ? "missions" : d.name == "Council_Evt_Camp" ? "camps" : "events";
                if (system == "camps")
                {
                    if (campUntil > hour || !set.B("camps")) continue;
                    campUntil = hour + (int)(Range(4f, 12f) * 24);
                }
                Count(system);
                Send(system, d.asks || system == "missions" ? Channel.Decision : Channel.Letter);
                if (d.asks) eventPending = hour + AnswerDelay(1f);
                var (a, b) = Cast();
                Apply(d, Answer(d), a, b, system);
                return;
            }
        }

        private void Camps()
        {
            if (campUntil < hour) return;
            if (Chance(Rates.CampLettersPerDay)) Send("camps", Chance(Rates.CampQuestionShare) ? Channel.Decision : Channel.Letter);
            if (campUntil - hour < 24) News("camps", true);
        }

        private void Situations()
        {
            source = "world events";
            if (!set.B("worldSituations") || HourOfDay != 10) return;
            situations.RemoveAll(s => s.until < hour);
            foreach (var s in situations)
                foreach (int b in s.beats)
                    if (b / 24 == Day)
                    {
                        bool choice = s.def.choiceBeats > 0 && Chance(s.def.choiceBeats / (float)Math.Max(1, s.def.beats));
                        Send("world events", choice ? Channel.Decision : Channel.Letter);
                        var good = s.def.thoughts.Select(defs.T).Where(t => t != null && !t.Situational).ToList();
                        if (good.Count > 0 && Chance(0.5f))
                        {
                            var t = Pick(good);
                            foreach (var c in people.Where(_ => Chance(0.3f))) Give(c, t.name);
                        }
                    }
            foreach (var s in situations)
                if (s.until / 24 == Day) Send("world events", Channel.Letter);
            if (hour < nextSituation || situations.Count >= Code.SituationsMaxActive || defs.situations.Count == 0) return;
            if (!GapOpen)
            {
                nextSituation = hour + 6;
                return;
            }
            var def = Pick(defs.situations);
            int len = (int)(Range(def.durMin, def.durMax) * 24);
            var beats = new List<int>();
            for (int i = 0; i < def.beats; i++)
                if (Chance(Rates.SituationBeatShare)) beats.Add(hour + (int)(rng.NextDouble() * len));
            situations.Add((hour, hour + len, def, beats));
            Send("world events", Channel.Decision);
            Count("world events");
            float days = Math.Max(5f, set.F("daysBetweenWorldSituations", 45)) * Range(0.67f, 1.33f);
            if (situations.Count > 0) days *= 1.25f;
            nextSituation = hour + (int)(days * 24);
        }

        // ---------------- Encounters, social events, moments, nuisances, dreams ----------------

        private int encountersToday, encounterDay = -1, lastEncounter = -9999;

        private void Encounters()
        {
            if (!set.B("strangers") || !Awake) return;
            if (encounterDay != Day)
            {
                encounterDay = Day;
                encountersToday = 0;
            }
            float freq = set.F("encounterFrequency", 1f);
            if (encountersToday >= (int)Math.Ceiling(Code.EncounterMaxPerDay * Math.Max(1f, freq)) || hour - lastEncounter < 3) return;
            var present = new HashSet<string>();
            foreach (var kv in Rates.Present)
            {
                float share = kv.Key == "Pet" ? style.pets : kv.Value;
                if (Chance(share)) present.Add(kv.Key);
            }
            float none = 1f;
            foreach (var k in present) none *= 1f - Math.Min(0.9f, Code.EncounterHourly.GetValueOrDefault(k) * Math.Clamp(freq, 0.1f, 4f));
            if (!Chance(1f - none)) return;
            var pool = defs.encounters.Where(d => d.strangerKind != null && present.Contains(d.strangerKind) && Fits(d)).ToList();
            foreach (var d in pool.OrderByDescending(d => d.weight * rng.NextDouble()))
            {
                if (!Chance(Rates.WorkerCasts * d.gate)) continue;
                defLast[d.name] = hour;
                lastEncounter = hour;
                encountersToday++;
                Count("encounters");
                var (a, b) = Cast();
                Apply(d, 0, a, null, "encounters");
                News("encounters", d.notable || Chance(Rates.NotableShare));
                return;
            }
        }

        private void Social()
        {
            if (!set.B("socialWeb") || !set.B("socialEvents") || people.Count < 2) return;
            if (HourOfDay == 0)
            {
                float expected = Math.Clamp(people.Count / Code.SocialPerColonist, Code.SocialMin, Code.SocialMax) * set.F("socialEventFrequency", 1f);
                socialToday = (int)expected + (Chance(expected % 1f) ? 1 : 0);
            }
            int h = HourOfDay;
            if (socialToday <= 0 || h < 7 || h > 21 || !Chance(Code.SocialHourly)) return;
            var pool = defs.socialEvents.Where(Fits).ToList();
            foreach (var d in pool.OrderByDescending(d => d.weight * rng.NextDouble()))
            {
                // Any pair that fits will do: the more people, the likelier one pair does.
                float fits = 1f - (float)Math.Pow(1f - d.gate, Math.Max(1f, people.Count / 3f));
                if (!Chance(Rates.WorkerCasts * fits)) continue;
                socialToday--;
                Count("social events");
                var (a, b) = Cast();
                Apply(d, 0, a, b, "social events");
                News("social events", d.notable || Chance(Rates.NotableShare));
                return;
            }
        }

        private void Moments()
        {
            if (!set.B("colonistMoments") || people.Count < 2) return;
            float f = set.F("momentFrequency", 1f);
            float w = MomentWeight() * f;
            float perInteraction = w / (w + Rates.VanillaInteractionWeight);
            float perHour = 1f - (float)Math.Pow(1f - perInteraction, Rates.InteractionsPerHour);
            foreach (var c in people)
            {
                if (hour - c.lastMoment < Code.MomentGapHours || !Chance(perHour)) continue;
                var pool = defs.moments.Where(m => (!m.friction || set.B("frictions")) && !(c.momentLast.TryGetValue(m.name, out int t) && hour - t < m.cooldownDays * 24)).ToList();
                var d = Weighted(pool, MomentPick);
                if (d == null) continue;
                c.lastMoment = hour;
                c.momentLast[d.name] = hour;
                var other = Pick(people.Where(p => p != c).ToList());
                Count(d.friction ? "friction moments" : "moments");
                source = d.friction ? "frictions" : "moments";
                MomentThought(c, d.initiatorThought);
                MomentThought(other, d.recipientThought);
                if (d.winnerThought != null)
                {
                    bool won = Chance(0.5f);
                    MomentThought(won ? c : other, d.winnerThought);
                    MomentThought(won ? other : c, d.loserThought);
                }
            }
        }

        private float momentWeight = -1f;

        private float MomentPick(OutcomeDef m) => m.weight * m.gate * (m.friction ? style.frictionFactor : 1f) * (m.sharesBedroom ? style.sharedBedrooms : 1f);

        /// <summary>The moment weight that fits two colonists at a time: every moment's weight times how often its conditions hold.</summary>
        private float MomentWeight()
        {
            if (momentWeight < 0f)
                momentWeight = defs.moments.Where(m => !m.friction || set.B("frictions")).Sum(MomentPick);
            return momentWeight;
        }

        /// <summary>A moment's memory: not given by the council, so a good one is scaled as it's felt (SmallJoys.LiveScale).</summary>
        private void MomentThought(Colonist c, string thought)
        {
            var t = defs.T(thought);
            if (t == null || t.Situational || t.rewardInstead || !set.MoodEffects) return;
            float own = t.First;
            AddMemory(c, t, own > 0f ? own * LiveScale(c) : own * PressureMath.LetdownFactor(set.Life) * BadScale(c) * PressureMath.HardTimesLetdown(Hard(c)));
        }

        private void Nuisances()
        {
            source = "nuisances";
            if (!set.B("frictions")) return;
            float chance = Math.Min(Code.NuisanceMax, Code.NuisancePerColonist * people.Count) * Math.Clamp(set.F("momentFrequency", 1f), 0.1f, 4f);
            if (!Chance(chance)) return;
            var c = Pick(people);
            if (hour - c.lastNuisance < Code.NuisanceGapHours || !Chance(style.nuisanceFits)) return;
            var pool = defs.nuisances.Where(n => !(c.nuisanceLast.TryGetValue(n.name, out int t) && hour - t < n.cooldownDays * 24)).ToList();
            var d = Weighted(pool, n => n.weight);
            if (d == null) return;
            c.lastNuisance = hour;
            c.nuisanceLast[d.name] = hour;
            Count("nuisances");
            var t2 = defs.T(d.options[0].effects[0].thought);
            if (t2 != null && set.MoodEffects) AddMemory(c, t2, t2.First);
        }

        private void Dreams()
        {
            source = "dreams";
            if (!set.B("dreams")) return;
            foreach (var c in people)
            {
                if (hour - c.lastDream < Code.DreamGapHours || !Chance(Math.Min(0.95f, Code.DreamChance * set.F("dreamFrequency", 1f)))) continue;
                c.lastDream = hour;
                var d = Weighted(defs.dreams, x => x.weight * x.gate * (x.aboutEvents ? 4f : 1f) * (x.dreamKind == "Nightmare" ? (c.mood < 0.35f ? 1.9f : c.mood < 0.5f ? 1.5f : 0.7f) : 1f));
                if (d == null) continue;
                Count("dreams");
                var t = defs.T("Council_Dream_" + (d.dreamKind ?? "Sweet"));
                // Dreams count in full, as their moods always have.
                if (t != null && !t.rewardInstead && set.MoodEffects) AddMemory(c, t, t.First);
                Apply(d, 0, c, null, "dreams");
                if (d.notable || set.B("allDreamMessages")) News("dreams", true);
            }
        }

        // ---------------- Ailments ----------------

        private float Freq => AilmentMath.Frequency(set.F("ailmentFrequency", 1f));

        private int EverydayCount(Colonist c) => c.ails.Count(a => !a.info.serious);

        private bool CanGet(Colonist c, AilmentInfo a)
        {
            if (!set.B("ailments") || (a.serious && !set.B("seriousIllnesses"))) return false;
            if (c.ails.Any(x => x.info == a)) return false;
            if (c.ailLast.TryGetValue(a.name, out int t) && hour - t < a.cooldownDays * 24) return false;
            return a.serious || EverydayCount(c) < Code.MaxEverydayAtOnce;
        }

        /// <summary>How many days it lasts and how it feels: recovery (faster in bed), tending, or the immunity race (AilmentMath.Simulate).</summary>
        private (float days, float mood) Course(AilmentInfo a, float start)
        {
            bool tended = a.tendPerDay < 0f && Chance(style.tendShare);
            float q = style.tendQuality;
            if (a.immunizable)
            {
                var course = new AilmentMath.Course
                {
                    severityPerDayNotImmune = a.sevNotImmune,
                    immunityPerDaySick = a.immunityPerDay,
                    severityPerDayImmune = a.sevImmune,
                    severityPerDayTended = a.tendPerDay,
                    startSeverity = a.initialSeverity,
                    lethalSeverity = a.serious ? 1f : -1f
                };
                var o = AilmentMath.Simulate(course, tended ? q : 0f, 1f, tended ? 0.8f : 0f, Range(0.85f, 1.1f), Range(0.85f, 1.15f), -1f, 60);
                return (o.days, a.MoodAt(Math.Max(0.1f, o.peak * 0.6f)));
            }
            float rate = -a.recoveryPerDay;
            float bed = Chance(style.bedShare) ? a.bedFactor : 1f;
            rate *= bed;
            if (tended) rate += -a.tendPerDay * q * 0.8f;
            if (rate <= 0f) rate = 0.05f;
            return (start / rate, a.MoodAt(start * 0.6f));
        }

        private void Give(Colonist c, AilmentInfo a, float start)
        {
            var (days, mood) = Course(a, start);
            c.ails.Add(new Ail { info = a, until = hour + Math.Max(1, (int)(days * 24)), mood = mood });
            c.ailLast[a.name] = hour;
            Count(a.serious ? "serious illnesses" : a.lingering ? "after-effects" : "ailments");
            if (a.lingering) return;
            if (a.serious) Send("health", Channel.Letter);
            else News("ailments", !a.quiet);
        }

        private void EndAilment(Colonist c, Ail x)
        {
            c.ails.Remove(x);
            foreach (var (hediff, chance, lo, hi) in x.info.after)
                if (hediff != null && defs.ailments.TryGetValue(hediff, out var a) && Chance(chance) && !c.ails.Any(y => y.info == a))
                    Give(c, a, Range(lo, hi));
        }

        /// <summary>Once a day per colonist: did the day hold enough hours of a cause? Then one roll (AilmentTracker.Hour).</summary>
        private void AilmentDay(Colonist c)
        {
            if (!set.B("ailments") || Day < 2) return;
            var held = new HashSet<string>();
            if (Rates.RoleDays.TryGetValue(c.role, out var role))
                foreach (var kv in role)
                    if (Chance(Math.Min(1f, kv.Value * Rates.Season(kv.Key, Quadrum)))) held.Add(kv.Key);
            foreach (var kv in style.home)
                if (Chance(kv.Value * Rates.Season(kv.Key, Quadrum)))
                    held.Add(kv.Key);
            if (Chance(Rates.Season("BitterCold", Quadrum) * 0.5f)) held.Add("BitterCold");
            if (Chance(Rates.Season("Sweltering", Quadrum))) held.Add("Sweltering");
            if (c.mood < 0.3f || c.strain >= 1) held.Add("Worry");
            float freq = Freq;
            foreach (var a in defs.ailments.Values)
            {
                foreach (var cause in a.causes)
                {
                    float chance = cause.chance * freq;
                    switch (cause.trigger)
                    {
                        case "RichMeal":
                            chance *= style.key == "well" ? 1.5f : style.key == "avg" ? 0.7f : 0.1f;
                            break;
                        case "Drink":
                            chance *= 0.3f;
                            break;
                        case "Sweets":
                            chance *= 1.5f;
                            break;
                        case "Wound":
                        case "AnimalBite":
                        case "InsectBite":
                        case "AfterCold":
                        case "Diet":
                            continue;
                        default:
                            if (!held.Contains(cause.trigger)) continue;
                            break;
                    }
                    if (Chance(chance) && CanGet(c, a))
                    {
                        Give(c, a, Range(a.startMin, a.startMax));
                        break;
                    }
                }
            }
        }

        /// <summary>The catching clock (AilmentTracker.Clock): an everyday catching ailment now and then, a serious one rarely.</summary>
        private void Ailments()
        {
            if (!set.B("ailments")) return;
            float freq = Freq;
            if (Chance(1f / (Code.EverydayCatchingDays / freq * 24f))) Catching(false);
            if (set.B("seriousIllnesses") && Chance(1f / (Code.SeriousCatchingDays / freq * 24f))) Catching(true);
        }

        private void Catching(bool serious)
        {
            var pool = defs.catching.Where(x => defs.ailments.TryGetValue(x.hediff ?? "", out var a) && a.serious == serious
                                               && !(catchingLast.TryGetValue(x.name, out int t) && hour - t < x.minRefireDays * 24)).ToList();
            var pick = Weighted(pool, x => x.weight);
            if (pick == null) return;
            catchingLast[pick.name] = hour;
            var a = defs.ailments[pick.hediff];
            int n = Math.Min(pick.maxVictims, Math.Max(1, (int)Math.Round(people.Count * Range(pick.victimsMin, pick.victimsMax))));
            int got = 0;
            foreach (var c in people.OrderBy(_ => rng.Next()).Take(n))
                if (CanGet(c, a))
                {
                    var (days, mood) = Course(a, a.initialSeverity);
                    c.ails.Add(new Ail { info = a, until = hour + Math.Max(1, (int)(days * 24)), mood = mood });
                    c.ailLast[a.name] = hour;
                    got++;
                }
            if (got == 0) return;
            Count(serious ? "serious illnesses" : "catching ailments");
            Send(serious ? "health" : "ailments", Channel.Letter);
        }

        // ---------------- The colony's year: calendar, stories, meetings, decrees ----------------

        private void Calendar()
        {
            source = "calendar";
            if (!set.B("colonyCalendar")) return;
            int doy = Day % YearDays;
            foreach (var (day, kind) in calendar)
            {
                if (day != doy) continue;
                Count("calendar days");
                if (kind == "Remembered")
                {
                    Send("calendar", Channel.Letter);
                    foreach (var c in people.Where(_ => Chance(0.6f))) Give(c, "Council_Cal_Remembered");
                    continue;
                }
                Send("calendar", Channel.Decision);
                if (Chance(style.calendarPass)) continue;
                foreach (var c in people) Give(c, "Council_Cal_" + kind);
                if (kind == "FeastDay" || kind == "GamesDay")
                {
                    Send("play-outs", Channel.Message);
                    Send("play-outs", Channel.Message);
                }
            }
        }

        private void Stories()
        {
            source = "stories";
            stories.RemoveAll(s =>
            {
                if (s.until > hour) return false;
                if (s.well)
                {
                    var pool = defs.thoughts.Values.Where(t => t.name.StartsWith("Council_Arc_") && !t.Situational && t.First > 0f).ToList();
                    if (pool.Count > 0) Give(s.who, Pick(pool).name);
                    if (Chance(0.5f) && people.Count > 1 && pool.Count > 0) Give(Pick(people.Where(p => p != s.who).ToList()), Pick(pool).name);
                    Send("stories", Channel.Letter);
                }
                else News("stories", true);
                return true;
            });
            int freq = set.ArcFrequency;
            float perYear = ArcMath.PerYear(freq, people.Count);
            storyStarts.RemoveAll(d => Day - d >= YearDays);
            float chance = ArcMath.DailyChance(perYear, storyStarts.Count, Day - lastStoryDay, stories.Count, ArcMath.MaxActive(freq, people.Count));
            if (!Chance(chance)) return;
            lastStoryDay = Day;
            storyStarts.Add(Day);
            var who = Pick(people);
            int len = (int)(Range(Rates.StoryDaysMin, Rates.StoryDaysMax) * 24);
            stories.Add((hour + len, who, Chance(Rates.StoryEndsWell)));
            Count("stories");
            News("stories", true);
            News("stories", true);
        }

        private void Meetings()
        {
            source = "meetings";
            if (!set.B("enableAmbitions") || HourOfDay < 8 || HourOfDay > 18) return;
            if (ambitionUntil >= 0)
            {
                if (hour < ambitionUntil) return;
                ambitionUntil = -1;
                if (ambitionWillSucceed)
                {
                    foreach (var c in people)
                    {
                        Give(c, c == proposer ? "Council_MyAmbitionAchieved" : "Council_AmbitionAchieved");
                        Trust(c, c == proposer ? 12f : 5f);
                    }
                    Send("meetings", Channel.Letter);
                    Count("ambitions reached");
                }
                else
                {
                    foreach (var c in people)
                    {
                        Give(c, "Council_AmbitionFailed");
                        Trust(c, c == proposer ? -8f : -3f);
                    }
                    News("meetings", true);
                }
                nextMeeting = Math.Max(nextMeeting, hour + 24);
                return;
            }
            if (hour < nextMeeting) return;
            nextMeeting = hour + (int)(set.F("daysBetweenMeetings", 15) * 24);
            lastMeetingHour = hour;
            Count("meetings");
            // The meeting gathers everyone (a play-out), then the vote is a question for the council.
            Send("meetings", Channel.Decision);
            Send("play-outs", Channel.Message);
            var proposers = people.OrderBy(_ => rng.Next()).Take(Math.Min(Rates.MeetingProposals, people.Count)).ToList();
            proposer = proposers[0];
            foreach (var p in proposers.Skip(1)) Setback(p, "Council_Setback_IdeaPassedOver");
            foreach (var c in people.Where(p => !proposers.Contains(p) && Chance(Rates.OutVotedShare))) Setback(c, "Council_Setback_OutVoted");
            Trust(proposer, 5f);
            ambitionUntil = hour + (int)(Range(Rates.AmbitionDaysMin, Rates.AmbitionDaysMax) * 24);
            ambitionWillSucceed = Chance(style.ambitionDone);
        }

        private void Decrees()
        {
            if (!set.B("enableWishes") || !set.B("decreeRequests") || Day - lastDecreeRequest < Code.DecreeRequestGap) return;
            if (!Chance(Code.DecreeRequestChance * Rates.DecreeRequestFits)) return;
            lastDecreeRequest = Day;
            Count("decree requests");
            Send("decrees", Channel.Message);
            var c = Pick(people);
            Trust(c, Chance(style.best) ? 5f : -2f);
        }

        // ---------------- Faith and the council (Faith/, with Ideology) ----------------

        private bool? faithSplit;
        private int lastEvening = -9999;

        /// <summary>
        /// A colony of more than one faith (a share of runs, Rates.FaithSplitColony): now and then an evening of faith,
        /// where those who come get recreation and fellowship (a good evening, no mood) or a small good mood (a fine one);
        /// and some of the year one faith is the colony's by decree, and those of other faiths trust the council less.
        /// </summary>
        private void Faith()
        {
            source = "faith";
            if (!set.B("faithLife") || !set.B("answersPlayOut")) return;
            faithSplit ??= Chance(Rates.FaithSplitColony);
            if (!faithSplit.Value) return;
            if (Day - lastEvening >= 5 && Chance(1f / Rates.EveningEveryDays))
            {
                lastEvening = Day;
                Count("evenings of faith");
                Send("play-outs", Channel.Message);
                float roll = (float)rng.NextDouble();
                string thought = roll < Rates.EveningPoor ? null : roll < 1f - Rates.EveningFine ? "Council_Faith_GoodEvening" : "Council_Faith_FineEvening";
                if (thought != null)
                    foreach (var c in people.Where(_ => Chance(Rates.EveningComes)).ToList())
                        Give(c, thought);
            }
            if (Chance(Rates.OneFaithDays))
            {
                Count("days of one faith for the colony");
                foreach (var c in people.Where(_ => Chance(Rates.OneFaithOutsiders)).ToList()) Trust(c, Rates.OneFaithTrustPerDay);
            }
        }

        /// <summary>The rest of the people and colony letters, at a documented yearly rate (OtherLettersPerYear).</summary>
        private void OtherLetters()
        {
            foreach (var (what, perYear, channel) in OtherLettersPerYear())
                if (Chance(perYear / YearDays))
                    Send(what, channel);

        }

        private IEnumerable<(string what, float perYear, Channel channel)> OtherLettersPerYear()
        {
            float deaths = style.key == "well" ? 0.5f : style.key == "avg" ? 1f : 2f;
            yield return ("people", 2f + (set.B("secrets") ? 1f : 0f) + (set.B("eulogies") ? deaths : 0f), Channel.Letter);
            yield return ("people", 6f, Channel.Digest);
            yield return ("colony", (set.B("annals") ? 1f : 0f) + (set.B("offices") ? 1f : 0f) + (set.B("colonyCharacter") ? 0.5f : 0f), Channel.Letter);
            if (set.B("heart") && set.B("heartEpisodes")) yield return ("people", style.key == "well" ? 0.2f : style.key == "avg" ? 1f : 3f, Channel.Letter);
            if (set.B("nemeses")) yield return ("world", 0.5f, Channel.Letter);
            if (set.B("healerReputations")) yield return ("health", 3f, Channel.Digest);
        }
    }
}
