// The heart's strain for one colonist, played hour by hour as HeartLogic does it, with the mod's own numbers
// (Heart/HeartMath.cs: each source's daily cap, the day's cap, the episodes' odds and relief; Moods/SmallMomentMath.cs: the
// small letdowns' and joys' cap). The rest of a colonist's life is modelled roughly per play style (fights, deaths, bad
// nights, hunger, no free time, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks easing it) and set
// so that life alone gives the time spent strained, worn and near breaking the colony-year report always assumed for each
// style (Rates.cs: 2/0/0, 8/2/0 and 20/8/2 per cent). The colony-year (Sim.cs) adds what Rimfolk's small letdowns and joys,
// and everyone's small moods, bring on top; the nature mode (NatureSim.cs) adds acts against their nature.

using ColonyCouncil;

namespace Balance
{
    /// <summary>A play style's everyday life for the heart: chances a day for each colonist (or the colony), and how well they rest.</summary>
    public sealed class HeartLife
    {
        public string key, name;
        public float raid, deathsPerYear, badNight, hungry, joyless, exhausted, lowMood, dark, decree, party, talk, comfy, mood;
        /// <summary>A hard spell (a few days of bad nights, low spirits, no free time and a need of the heart missing) starting for a colonist on a given day, and how long.</summary>
        public float spell, spellDays;
        public float[] shares;
    }

    /// <summary>One colonist's heart through the year.</summary>
    public sealed class HeartState
    {
        public const int Days = 60;

        // The chances are set so the time strained, worn and near breaking comes near the colony-year report's shares for each
        // style (2/0/0, 8/2/0 and 20/8/2 per cent): with these, 2.2/0.2/0, 7.8/2.1/0.4 and 18/8/2.5.
        public static readonly HeartLife[] Lives =
        {
            new HeartLife
            {
                key = "well", name = "well-run", raid = 0.1f, deathsPerYear = 0.6f, badNight = 0.04f, hungry = 0.01f, joyless = 0.01f, exhausted = 0.01f, lowMood = 0.01f,
                dark = 0.01f, decree = 0.05f, party = 0.12f, talk = 0.6f, comfy = 0.97f, mood = 1.35f, spell = 0.035f, spellDays = 6f, shares = Rates.WellRun.strain
            },
            new HeartLife
            {
                key = "avg", name = "average", raid = 0.12f, deathsPerYear = 1.6f, badNight = 0.2f, hungry = 0.05f, joyless = 0.08f, exhausted = 0.05f, lowMood = 0.06f,
                dark = 0.1f, decree = 0.15f, party = 0.07f, talk = 0.45f, comfy = 0.8f, mood = 1.1f, spell = 0.03f, spellDays = 7f, shares = Rates.Average.strain
            },
            new HeartLife
            {
                key = "neglect", name = "neglectful", raid = 0.14f, deathsPerYear = 3.5f, badNight = 0.35f, hungry = 0.08f, joyless = 0.2f, exhausted = 0.1f, lowMood = 0.12f,
                dark = 0.2f, decree = 0.3f, party = 0.02f, talk = 0.25f, comfy = 0.6f, mood = 0.95f, spell = 0.035f, spellDays = 5f, shares = Rates.Neglectful.strain
            }
        };

        public static HeartLife For(string styleKey) => Lives.FirstOrDefault(l => l.key == styleKey) ?? Lives[1];

        public float strain, todayTotal, todayJoys;
        public readonly float[] today = new float[HeartMath.SourceCount];
        public readonly float[] recent = new float[HeartMath.SourceCount];
        public int spellUntil = -1, lastEpisode = -999999;
        /// <summary>What built and eased strain, by what it was, for the report.</summary>
        public readonly Dictionary<string, double> built = new Dictionary<string, double>(), eased = new Dictionary<string, double>();
        public int episodes;
        private readonly List<(int hour, StrainSource source, float amount)> day = new List<(int, StrainSource, float)>();

        /// <summary>A new day: the caps start over and the day's own events are rolled (a raid and a death are the colony's, given by it).</summary>
        public void StartDay(HeartLife life, Random rng, int dayIndex, bool raid, int raidHour, bool death, int deathHour)
        {
            Array.Clear(today, 0, today.Length);
            todayTotal = 0f;
            todayJoys = 0f;
            day.Clear();
            bool Chance(float p) => rng.NextDouble() < p;
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (raid && Chance(0.8f))
            {
                for (int h = 0; h < 3; h++) day.Add((raidHour + h, StrainSource.Combat, 4f));
                day.Add((raidHour + 1, StrainSource.Fear, 2f));
                if (Chance(0.3f)) day.Add((raidHour + 2, StrainSource.Pain, Range(2f, 6f)));
                if (Chance(0.08f)) day.Add((raidHour + 2, StrainSource.Downed, 8f));
            }
            if (death)
            {
                day.Add((deathHour, StrainSource.Loss, 4f + (Chance(0.3f) ? Range(4f, 8f) : 0f)));
                for (int g = 1; g <= 3 && deathHour + g * 6 < 24; g++) day.Add((deathHour + g * 6, StrainSource.Grief, 1f));
            }
            if (spellUntil < dayIndex && Chance(life.spell)) spellUntil = dayIndex + (int)Range(3f, 2f * life.spellDays - 3f);
            if (spellUntil >= dayIndex)
            {
                if (Chance(0.7f)) day.Add((6, StrainSource.BadSleep, Range(2f, 4f)));
                if (Chance(0.6f)) day.Add((16, StrainSource.LowMood, Range(4f, 6f)));
                if (Chance(0.5f)) day.Add((18, StrainSource.Joyless, 3f));
                if (Chance(0.6f)) day.Add((20, StrainSource.Unmet, 3f));
            }
            if (Chance(life.badNight)) day.Add((6, StrainSource.BadSleep, Range(1f, 2f)));
            if (Chance(life.hungry)) day.Add((14, StrainSource.Hunger, 4f));
            if (Chance(life.joyless)) day.Add((18, StrainSource.Joyless, 3f));
            if (Chance(life.exhausted)) day.Add((21, StrainSource.Exhaustion, 3f));
            if (Chance(life.lowMood)) day.Add((16, StrainSource.LowMood, Range(3f, 6f)));
            if (Chance(life.dark)) day.Add((22, StrainSource.Dark, 2f));
            if (Chance(life.decree)) day.Add((12, StrainSource.Council, 1f));
        }

        /// <summary>Strain from one source within its daily cap and the day's cap (HeartMath.Add at 100% strength, no traits).</summary>
        public float Build(StrainSource s, float raw, string what)
        {
            int i = (int)s;
            float t = today[i];
            float added = HeartMath.Add(raw, 1f, 1f, ref t, HeartMath.SourceCap(s), ref todayTotal, HeartMath.DailyCap);
            today[i] = t;
            float before = strain;
            strain = HeartMath.Clamp(strain + added);
            float moved = strain - before;
            recent[i] += moved;
            if (what != null && moved > 0f) built[what] = built.GetValueOrDefault(what) + moved;
            return moved;
        }

        /// <summary>Relief from small joys within the day's cap for them (SmallMomentMath.WithinJoyCap).</summary>
        public float EaseJoys(float amount, string what)
        {
            float a = SmallMomentMath.WithinJoyCap(Math.Min(amount, strain), todayJoys);
            if (a <= 0f) return 0f;
            todayJoys += a;
            strain = HeartMath.Clamp(strain - a);
            if (what != null) eased[what] = eased.GetValueOrDefault(what) + a;
            return a;
        }

        /// <summary>
        /// One hour of the life: its events, then sleep, quiet hours, recreation, a gathering and a good talk easing it, then
        /// the heart's episode (its odds and its waits). Returns whether an episode began.
        /// </summary>
        public bool Hour(HeartLife life, Random rng, int dayIndex, int hourOfDay, bool party, int hourAbs, ref int lastColonyEpisode, bool episodesOn = true)
        {
            foreach (var b in day)
                if (b.hour == hourOfDay)
                    Build(b.source, b.amount, "life");
            float relief;
            float mood = spellUntil >= dayIndex ? 0.7f : life.mood;
            if (hourOfDay < 6 || hourOfDay >= 22) relief = (rng.NextDouble() < life.comfy ? 0.6f : 0.25f) * mood;
            else if (hourOfDay >= 19) relief = 0.5f * mood;
            else relief = 0.15f * mood;
            if (party && hourOfDay == 20) relief += 6f;
            if (hourOfDay == 13 && rng.NextDouble() < life.talk) relief += 2f;
            float eased = Math.Min(relief, strain);
            strain = HeartMath.Clamp(strain - relief);
            if (eased > 0f) this.eased["life"] = this.eased.GetValueOrDefault("life") + eased;
            for (int s = 0; s < recent.Length; s++) recent[s] *= HeartMath.RecentDecayPerHour;
            int now = hourAbs * 2500;
            bool roll = rng.NextDouble() < HeartMath.EpisodeChancePerHour(strain);
            if (episodesOn && roll && now - lastEpisode >= 10 * 60000 && now - lastColonyEpisode >= 2 * 60000)
            {
                strain = HeartMath.Clamp(strain - HeartMath.EpisodeRelief);
                lastEpisode = now;
                lastColonyEpisode = now;
                episodes++;
                return true;
            }
            return false;
        }
    }
}
