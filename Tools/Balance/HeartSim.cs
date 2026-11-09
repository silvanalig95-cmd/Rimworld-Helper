// The heart's strain for one colonist, played hour by hour as HeartLogic does it, with the mod's own numbers
// (Heart/HeartMath.cs: each source's daily cap, the day's cap, the episodes' odds and relief; Moods/SmallMomentMath.cs: the
// small letdowns' and joys' cap). The rest of a colonist's life is modelled roughly per play style (fights, deaths, bad
// nights, hunger, no free time, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks easing it) and set
// so that life alone gives the time spent strained, worn and near breaking the colony-year report always assumed for each
// style (Rates.cs: 2/0/0, 8/2/0 and 20/8/2 per cent). The colony-year (Sim.cs) adds what Rimfolk's small letdowns and joys,
// and everyone's small moods, bring on top; the nature mode (NatureSim.cs) adds acts against their nature.
// The heart's good side (HeartMath, "The good side"), both sides at once since 9 November 2026 (HeartFeltMath): hardship
// spends part of it as a cushion; a gathering, a talk or a joy eases strain and fills it together, a share each; friends close
// by and a purpose (the heart's needs well met, drawn a day at a time per play style), deeds the colony speaks well of and a
// life goal moving fill it at any strain, slower the more there is; it fades slowly; and in high spirits it now and then
// gives one of the game's inspirations or a kind word to someone close (the colony-year gives the moment). The heart, felt
// (HeartFeltMath): rest eases less the higher strain is, and a death or a downing leaves a lasting weight rest can't take
// strain below, lifting over days (a death of someone close a third of the time, as the loss's own strain is drawn).
// The heart, made to count (HeartCountMath): from worn down, now and then a bad afternoon (off work a few hours at leisure,
// easing a good share of strain), and hearts spread a little (the colony gives each colonist's friends' bands an hour).

using ColonyCouncil;

namespace Balance
{
    /// <summary>A play style's everyday life for the heart: chances a day for each colonist (or the colony), and how well they rest.</summary>
    public sealed class HeartLife
    {
        public string key, name;
        public float raid, deathsPerYear, badNight, hungry, joyless, exhausted, lowMood, dark, decree, party, talk, comfy, mood;
        /// <summary>
        /// The good side's life: belonging and purpose as the heart's needs usually stand (0 to 100, drawn a day at a time
        /// within 20 either way), the chance a day of a deed the colony speaks well of, and of a life goal moving.
        /// </summary>
        public float belonging, purpose, deed, goalMoved;
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
                dark = 0.01f, decree = 0.05f, party = 0.12f, talk = 0.6f, comfy = 0.97f, mood = 1.35f, spell = 0.035f, spellDays = 6f, shares = Rates.WellRun.strain,
                belonging = 68f, purpose = 72f, deed = 0.05f, goalMoved = 0.2f
            },
            new HeartLife
            {
                key = "avg", name = "average", raid = 0.12f, deathsPerYear = 1.6f, badNight = 0.2f, hungry = 0.05f, joyless = 0.08f, exhausted = 0.05f, lowMood = 0.06f,
                dark = 0.1f, decree = 0.15f, party = 0.07f, talk = 0.45f, comfy = 0.8f, mood = 1.1f, spell = 0.03f, spellDays = 7f, shares = Rates.Average.strain,
                belonging = 55f, purpose = 60f, deed = 0.035f, goalMoved = 0.12f
            },
            new HeartLife
            {
                key = "neglect", name = "neglectful", raid = 0.14f, deathsPerYear = 3.5f, badNight = 0.35f, hungry = 0.08f, joyless = 0.2f, exhausted = 0.1f, lowMood = 0.12f,
                dark = 0.2f, decree = 0.3f, party = 0.02f, talk = 0.25f, comfy = 0.6f, mood = 0.95f, spell = 0.035f, spellDays = 5f, shares = Rates.Neglectful.strain,
                belonging = 40f, purpose = 48f, deed = 0.02f, goalMoved = 0.06f
            }
        };

        public static HeartLife For(string styleKey) => Lives.FirstOrDefault(l => l.key == styleKey) ?? Lives[1];

        public float strain, todayTotal, todayJoys;
        /// <summary>The good side (the setting on), what it filled today by source and in all, and how high it stood lately.</summary>
        public bool goodOn;
        public float good, goodTodayTotal, lately, belongingToday, purposeToday;
        /// <summary>Their own lean in belonging and purpose (a partner and friends, or none; work they love, or not), drawn once.</summary>
        public float lean = float.NaN;
        public readonly float[] goodToday = new float[HeartMath.GoodSourceCount];
        /// <summary>What filled the good side and what spent it, by what it was, for the report; the releases a year.</summary>
        public readonly Dictionary<string, double> filled = new Dictionary<string, double>(), spent = new Dictionary<string, double>();
        public int lastRelease = -999999, inspirations, kindWords;
        /// <summary>The good side's own draws (the colony's), so the heart's life draws the same with it on or off.</summary>
        public Random goodRng;
        public readonly float[] today = new float[HeartMath.SourceCount];
        public readonly float[] recent = new float[HeartMath.SourceCount];
        public int spellUntil = -1, lastEpisode = -999999;
        /// <summary>The lasting weight of a death or a downing (HeartFeltMath), and the hour until which it lifts slowly.</summary>
        public float weight;
        public int weightUntil = -1;
        /// <summary>What built and eased strain, by what it was, for the report.</summary>
        public readonly Dictionary<string, double> built = new Dictionary<string, double>(), eased = new Dictionary<string, double>();
        public int episodes;
        /// <summary>The heart, made to count: bad afternoons and spreading on (the settings); bad afternoons had, the last, the one under way; the colony's last.</summary>
        public bool badDaysOn = true, spreadOn = true;
        public int badDays, lastBadDay = -999999, badUntil = -1;
        public int[] colonyBadDay;
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
            if (goodOn) GoodDay(life, goodRng ?? rng);
            if (raid && Chance(0.8f))
            {
                for (int h = 0; h < 3; h++) day.Add((raidHour + h, StrainSource.Combat, 4f));
                day.Add((raidHour + 1, StrainSource.Fear, 2f));
                if (Chance(0.3f)) day.Add((raidHour + 2, StrainSource.Pain, Range(2f, 6f)));
                if (Chance(0.08f))
                {
                    day.Add((raidHour + 2, StrainSource.Downed, 8f));
                    Weigh(HeartFeltMath.DownedWeight, dayIndex, raidHour + 2);
                }
            }
            if (death)
            {
                bool close = Chance(0.3f);
                day.Add((deathHour, StrainSource.Loss, 4f + (close ? Range(4f, 8f) : 0f)));
                for (int g = 1; g <= 3 && deathHour + g * 6 < 24; g++) day.Add((deathHour + g * 6, StrainSource.Grief, 1f));
                Weigh(HeartFeltMath.LossWeight(close ? "PawnWithGoodOpinionDied" : "KnowColonistDied"), dayIndex, deathHour);
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

        /// <summary>A heavy thing's lasting weight, with what weighs already, held a while by how heavy it is.</summary>
        private void Weigh(float w, int dayIndex, int hour)
        {
            weight = HeartFeltMath.Combine(weight, w);
            weightUntil = Math.Max(weightUntil, dayIndex * 24 + hour + (int)(HeartFeltMath.HoldDaysFor(weight) * 24f));
        }

        /// <summary>Strain from one source within its daily cap and the day's cap (HeartMath.Add at 100% strength, no traits).</summary>
        public float Build(StrainSource s, float raw, string what)
        {
            int i = (int)s;
            float t = today[i];
            float added = HeartMath.Add(raw, 1f, 1f, ref t, HeartMath.SourceCap(s), ref todayTotal, HeartMath.DailyCap);
            today[i] = t;
            // Hardship spends part of the good side as a cushion.
            if (goodOn && good > 0f && added > 0f)
            {
                added = HeartMath.Spend(added, ref good, out float sp);
                string key = what ?? "small moods";
                if (sp > 0f) spent[key] = spent.GetValueOrDefault(key) + sp;
            }
            float before = strain;
            strain = HeartMath.Clamp(strain + added);
            float moved = strain - before;
            recent[i] += moved;
            if (what != null && moved > 0f) built[what] = built.GetValueOrDefault(what) + moved;
            return moved;
        }

        /// <summary>
        /// Relief from small joys within the day's cap for them (SmallMomentMath.WithinJoyCap). With the good side, they ease
        /// strain and fill it together (Rimfolk's joys, or the game's comforts with <paramref name="comforts"/>).
        /// </summary>
        public float EaseJoys(float amount, string what, bool comforts = false)
        {
            float a = SmallMomentMath.WithinJoyCap(goodOn ? amount : Math.Min(amount, strain), todayJoys);
            if (a <= 0f) return 0f;
            var s = comforts ? GoodSource.Comforts : GoodSource.Joys;
            float e = GoodThing(s, a, comforts ? "the game's comforts" : "small joys", what, out float filled);
            // The day's cap for joys counts what eased strain and what filled the good side, not what neither could take.
            float toGood = settled.toGood[(int)s];
            float used = e + (toGood > 0f ? (a - e) * Math.Min(1f, filled / toGood) : 0f);
            todayJoys += Math.Min(a, used);
            return e;
        }

        // ---------------- The good side ----------------

        /// <summary>The day's draws for the good side: how belonging and purpose stand, a deed, a life goal moving.</summary>
        private void GoodDay(HeartLife life, Random rng)
        {
            Array.Clear(goodToday, 0, goodToday.Length);
            goodTodayTotal = 0f;
            if (float.IsNaN(lean)) lean = (float)(rng.NextDouble() * 30.0 - 15.0);
            belongingToday = Math.Clamp(life.belonging + lean + (float)(rng.NextDouble() * 30.0 - 15.0), 0f, 100f);
            purposeToday = Math.Clamp(life.purpose + lean * 0.5f + (float)(rng.NextDouble() * 30.0 - 15.0), 0f, 100f);
            if (rng.NextDouble() < life.deed) Growing(GoodSource.Deeds, HeartMath.DeedFillFor(1f), "deeds");
            if (rng.NextDouble() < life.goalMoved) Growing(GoodSource.Purpose, HeartMath.GoalMovedFill, "purpose");
        }

        /// <summary>
        /// Fills the good side within the caps, at any strain: <paramref name="amount"/> as it comes (a good thing's share
        /// already taken by HeartFeltMath.Settle); the needs, deeds and a life goal through <see cref="Growing"/>.
        /// </summary>
        public float Fill(GoodSource s, float amount, string what)
        {
            if (!goodOn || amount <= 0f) return 0f;
            int i = (int)s;
            float t = goodToday[i];
            float a = HeartMath.Fill(amount, ref good, ref t, HeartMath.GoodCap(s), ref goodTodayTotal, HeartMath.GoodDailyCap);
            goodToday[i] = t;
            if (a > 0f) filled[what] = filled.GetValueOrDefault(what) + a;
            return a;
        }

        /// <summary>Friends, purpose, a deed or a life goal: slower the more strain there is (HeartFeltMath.Growth).</summary>
        private float Growing(GoodSource s, float amount, string what) => Fill(s, amount * HeartFeltMath.Growth(strain), what);

        private readonly HeartFeltMath.Settled settled = new HeartFeltMath.Settled();
        private readonly float[] things = new float[HeartMath.GoodSourceCount];

        /// <summary>A good thing: it eases strain and fills the good side together, a share each (HeartFeltMath.Settle).</summary>
        private float GoodThing(GoodSource s, float amount, string what, string easedAs, out float filledNow)
        {
            Array.Clear(things, 0, things.Length);
            things[(int)s] = amount;
            float left = HeartFeltMath.Settle(strain, 0f, 0f, 0f, things, settled, goodOn);
            float e = settled.eased[(int)s];
            strain = HeartMath.Clamp(left);
            LiftWeight(e);
            if (e > 0f) eased[easedAs ?? "life"] = eased.GetValueOrDefault(easedAs ?? "life") + e;
            filledNow = goodOn ? Fill(s, settled.toGood[(int)s], what) : 0f;
            return e;
        }

        private void GoodThing(GoodSource s, float amount, string what) => GoodThing(s, amount, what, "life", out _);

        /// <summary>
        /// Hearts spread a little, this hour: what their friends worn down wear on them (never past HeartCountMath.SpreadCeiling)
        /// and what those in high spirits lift (a good thing, as a talk is).
        /// </summary>
        public void SpreadHour(float wear, float lift)
        {
            if (!spreadOn) return;
            float room = HeartCountMath.Spread(wear, strain, 1f);
            if (room > 0f) Build(StrainSource.Spread, room, "someone close worn down");
            float up = HeartCountMath.Lift(lift, 1f);
            if (up > 0f) GoodThing(GoodSource.Lifted, up, "someone close in high spirits", "someone close in high spirits", out _);
        }

        private void LiftWeight(float eased) => weight = Math.Max(0f, weight - eased * HeartFeltMath.GoodEasesWeight);

        /// <summary>
        /// The release, in high spirits and safe: now and then one of the game's inspirations or a kind word to someone
        /// close (the colony gives the moment). 0 none, 1 an inspiration, 2 a kind word.
        /// </summary>
        public int Release(Random rng, int now, bool safe, ref int lastColonyRelease)
        {
            if (!goodOn || !safe || rng.NextDouble() >= HeartMath.ReleaseChancePerHour(good)) return 0;
            if (now - lastRelease < HeartMath.ReleasePawnDays * 60000 || now - lastColonyRelease < HeartMath.ReleaseColonyDays * 60000) return 0;
            lastRelease = now;
            lastColonyRelease = now;
            bool inspired = rng.NextDouble() < 0.5;
            good = Math.Max(0f, good - (inspired ? HeartMath.InspirationSpends : HeartMath.KindWordSpends));
            if (inspired) inspirations++;
            else kindWords++;
            return inspired ? 1 : 2;
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
            // A heavy thing sinks in toward its weight, cushioned by the good side as any hardship is.
            int abs = dayIndex * 24 + hourOfDay;
            float sink = HeartFeltMath.SinkIn(strain, weight, 1f);
            if (sink > 0f)
            {
                if (goodOn && good > 0f) sink = HeartMath.Spend(sink, ref good, out _);
                strain = HeartMath.Clamp(strain + sink);
                built["life"] = built.GetValueOrDefault("life") + sink;
            }
            float relief;
            float mood = spellUntil >= dayIndex ? 0.7f : life.mood;
            // A bad afternoon under way: at leisure, not at work; over, it eases a good share.
            bool bad = badUntil > abs;
            if (badUntil >= 0 && abs >= badUntil)
            {
                float e = Math.Min(strain, HeartCountMath.BadDayRelief(strain, true));
                strain = HeartMath.Clamp(strain - e);
                LiftWeight(e);
                if (e > 0f) this.eased["a bad afternoon"] = this.eased.GetValueOrDefault("a bad afternoon") + e;
                badUntil = -1;
            }
            if (hourOfDay < 6 || hourOfDay >= 22) relief = (rng.NextDouble() < life.comfy ? 0.6f : 0.25f) * mood;
            else if (hourOfDay >= 19 || bad) relief = 0.5f * mood;
            else relief = 0.15f * mood;
            // A quicker recovery for someone in good heart lately; less the higher strain is, never below the weight.
            if (goodOn) relief *= HeartMath.RecoveryFactor(HeartMath.GoodBandFor(lately));
            relief *= HeartFeltMath.RestFactor(strain);
            float eased = Math.Max(0f, Math.Min(relief, strain - Math.Min(strain, weight)));
            strain = HeartMath.Clamp(strain - eased);
            if (eased > 0f) this.eased["life"] = this.eased.GetValueOrDefault("life") + eased;
            // A gathering and a good talk: strain first, the good side with the rest.
            if (party && hourOfDay == 20) GoodThing(GoodSource.Gathering, 6f, "gatherings");
            if (hourOfDay == 13 && rng.NextDouble() < life.talk) GoodThing(GoodSource.Friends, 2f, "good talks");
            weight = HeartFeltMath.Eased(weight, abs < weightUntil, 1f);
            if (goodOn)
            {
                Growing(GoodSource.Belonging, HeartMath.NeedFillPerHour(belongingToday), "belonging");
                Growing(GoodSource.Purpose, HeartMath.NeedFillPerHour(purposeToday), "purpose");
                good = HeartMath.Faded(good, 1f);
                lately = HeartMath.Lately(lately, good, 1f);
            }
            for (int s = 0; s < recent.Length; s++) recent[s] *= HeartMath.RecentDecayPerHour;
            // From worn down, in the afternoon, now and then a bad afternoon (its waits: a week for each colonist, two days in the colony).
            if (badDaysOn && badUntil < 0 && strain >= HeartCountMath.BadDayFrom && hourOfDay >= HeartCountMath.BadDayStartHour && hourOfDay <= HeartCountMath.BadDayLastHour
                && abs - lastBadDay >= HeartCountMath.BadDayPawnDays * 24 && (colonyBadDay == null || abs - colonyBadDay[0] >= HeartCountMath.BadDayColonyDays * 24)
                && rng.NextDouble() < HeartCountMath.BadDayChancePerHour(strain))
            {
                badUntil = abs + 4;
                lastBadDay = abs;
                if (colonyBadDay != null) colonyBadDay[0] = abs;
                badDays++;
            }
            int now = hourAbs * 2500;
            bool roll = rng.NextDouble() < HeartMath.EpisodeChancePerHour(strain);
            if (episodesOn && roll && now - lastEpisode >= 10 * 60000 && now - lastColonyEpisode >= 2 * 60000)
            {
                strain = HeartMath.Clamp(strain - HeartMath.EpisodeRelief);
                LiftWeight(HeartMath.EpisodeRelief);
                lastEpisode = now;
                lastColonyEpisode = now;
                episodes++;
                return true;
            }
            return false;
        }
    }
}
