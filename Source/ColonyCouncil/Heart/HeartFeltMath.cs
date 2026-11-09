using System;

namespace ColonyCouncil
{
    /// <summary>What a heavy thing was, for its words and the weight's record. Saved by its number, so new kinds go at the end.</summary>
    public enum WeightKind
    {
        None,
        /// <summary>Someone close died or was lost (a partner, kin, a friend, a bonded animal, any of the colony a little).</summary>
        Loss,
        /// <summary>They were downed.</summary>
        Downed,
        /// <summary>A fight at home going badly: one of theirs downed while it lasted.</summary>
        Raid,
        /// <summary>They are badly ill themselves.</summary>
        SickSelf,
        /// <summary>Someone close to them is badly ill.</summary>
        SickClose
    }

    /// <summary>What the heart tells the player in a message, when someone's heart turns (Heart/HeartFelt.cs).</summary>
    public enum HeartTell
    {
        None,
        Worn,
        Breaking,
        /// <summary>Back from being worn down or near breaking.</summary>
        Back,
        GoodHeart,
        HighSpirits
    }

    /// <summary>
    /// The heart, felt (docs/design/heart-felt.md, release 1; the player's words of 9 November 2026: "i feel like this impact
    /// of strain and all the emotions is currently neglible, or hardly noticeable"), kept free of game types so the tests can
    /// check it and run it over weeks of play (HeartFeltTests):
    /// <list type="bullet">
    /// <item>Heavy things leave a lasting weight (a loss, being downed, a fight going badly, a sickness in the house) that
    /// rest can't take strain below; strain sinks toward it within a day, and it lifts over days, slowly at first.</item>
    /// <item>Rest eases at full rate at the low end and less the higher strain is.</item>
    /// <item>The two sides at once (the player's change): the good side grows at any strain, slower the more there is;
    /// good things ease strain and fill the good side together, a share each; hardship spends part of the good side as a
    /// cushion (HeartMath.Spend) and builds strain with the rest.</item>
    /// <item>How often the heart shows on the map (acts, lines, a sign over the head) and when it tells the player.</item>
    /// </list>
    /// </summary>
    public static class HeartFeltMath
    {
        // ---------------- The lasting weight ----------------

        /// <summary>
        /// Two weights together: the heavier in full and this share of the lighter, so a second loss on top of a first
        /// weighs more than either and several are a real disaster, without a run of small ones adding up to one.
        /// </summary>
        public const float CombineShare = 0.35f;

        public static float Combine(float a, float b)
        {
            a = Clean(a);
            b = Clean(b);
            return Math.Min(HeartMath.Max, Math.Max(a, b) + CombineShare * Math.Min(a, b));
        }

        /// <summary>
        /// After a heavy thing the weight lifts slowly for a while, longer the heavier it is (a friend's death about six
        /// days, a partner's about eight), then faster: so many days and so many days more a point.
        /// </summary>
        public const float HoldBaseDays = 3.5f, HoldPointsPerDay = 12f;

        public static float HoldDaysFor(float weight) => HoldBaseDays + Clean(weight) / HoldPointsPerDay;

        /// <summary>
        /// A weight that lasts while something does (a sickness in the house, a fight going badly) lifts slowly only this
        /// long once it is over: it was felt while it lasted.
        /// </summary>
        public const float HeldHoldDays = 1.5f;

        /// <summary>How much the weight lifts in a day while it holds, and after.</summary>
        public const float EaseSlowPerDay = 0.8f, EaseFastPerDay = 7f;

        /// <summary>The weight after <paramref name="hours"/>, slowly while it <paramref name="holds"/>, then faster.</summary>
        public static float Eased(float weight, bool holds, float hours)
        {
            weight = Clean(weight);
            if (weight <= 0f || hours <= 0f) return weight;
            float w = weight - (holds ? EaseSlowPerDay : EaseFastPerDay) * hours / 24f;
            return w < 0.05f ? 0f : w;
        }

        /// <summary>
        /// The weights together hold strain up to here at most: past it, only the hardship of the moment takes anyone
        /// further (to the day off at 85).
        /// </summary>
        public const float FloorMax = 76f;

        /// <summary>What rest can't take strain below: the weight of what happened and of what holds now, together.</summary>
        public static float Floor(float weight, float held) => Math.Min(FloorMax, Combine(weight, held));

        /// <summary>
        /// Strain below the weight sinks toward it, this much an hour at most: a heavy thing is felt fully within a day, not
        /// in the moment.
        /// </summary>
        public const float SinkPerHour = 2.5f;

        public static float SinkIn(float strain, float floor, float hours)
        {
            strain = Clean(strain);
            floor = Clean(floor);
            if (strain >= floor || hours <= 0f) return 0f;
            return Math.Min(floor - strain, SinkPerHour * hours);
        }

        /// <summary>Good things lift the weight too: this share of what they eased off strain (a funeral, a talk, a kind word).</summary>
        public const float GoodEasesWeight = 0.15f;

        /// <summary>The weight of a death or a loss, by the memory the game gave (0: none). Patterns have one "*", as the heart's rules.</summary>
        public static float LossWeight(string thought)
        {
            if (string.IsNullOrEmpty(thought)) return 0f;
            // Exact names first, so "PawnWithGoodOpinionDied" isn't taken by a pattern.
            foreach (var (pattern, weight) in Losses)
                if (pattern.IndexOf('*') < 0 && pattern == thought) return weight;
            foreach (var (pattern, weight) in Losses)
            {
                int star = pattern.IndexOf('*');
                if (star < 0) continue;
                string pre = pattern.Substring(0, star), post = pattern.Substring(star + 1);
                if (thought.Length >= pre.Length + post.Length && thought.StartsWith(pre, StringComparison.Ordinal) && thought.EndsWith(post, StringComparison.Ordinal))
                    return weight;
            }
            return 0f;
        }

        /// <summary>The heavy memories: a partner's or kin's death heaviest, a friend's next, anyone of the colony's a little.</summary>
        public static readonly (string pattern, float weight)[] Losses =
        {
            ("My*Died", 58f),
            ("WitnessedDeathFamily", 50f),
            ("PsychicBondTorn", 50f),
            ("Stillbirth", 55f),
            ("Miscarried", 45f),
            ("PartnerMiscarried", 40f),
            ("PawnWithGoodOpinionDied", 45f),
            ("BondedAnimalDied", 28f),
            ("WitnessedDeathAlly", 16f),
            ("KnowColonistDied", 12f),
            ("OtherTravelerDied", 10f),
            ("My*Lost", 30f),
            ("PawnWithGoodOpinionLost", 22f),
            ("BondedAnimalLost", 15f),
            ("ColonistLost", 10f)
        };

        /// <summary>Being downed: once a downing.</summary>
        public const float DownedWeight = 22f;

        /// <summary>A fight at home going badly, while it lasts: by how many of the colony's own are down.</summary>
        public static float RaidWeight(int downed) => downed <= 0 ? 0f : Math.Min(36f, 12f + 8f * downed);

        /// <summary>
        /// Their own sickness, while it lasts: by how far along it is (its severity as a share of where it kills, 0 to 1),
        /// from a sickness that can kill.
        /// </summary>
        public static float SickSelfWeight(float share) => share <= 0f || float.IsNaN(share) ? 0f : 12f + 40f * Math.Min(1f, share);

        /// <summary>Someone close badly ill, while it lasts: less than their own, by how far along it is.</summary>
        public static float SickCloseWeight(float share) => share < SickCloseFrom || float.IsNaN(share) ? 0f : 14f + 36f * Math.Min(1f, share);

        /// <summary>Someone else's sickness weighs once it is this far along (a cold caught today is nothing yet).</summary>
        public const float SickCloseFrom = 0.3f;

        // ---------------- Rest under real strain ----------------

        /// <summary>Rest eases at full rate up to here (so 5 to 20 still settles to nothing within a day or two).</summary>
        public const float RestFullBelow = 30f;

        /// <summary>
        /// And at this share from <see cref="RestLeastAt"/> on: the worn ease slower unless something good happens. Kept
        /// mild: what holds strain up after a heavy thing is its weight; slower rest alone, on a colony that's neglected day
        /// after day, would feed on itself (the balance simulator's neglectful colony near breaking 18% of the time with this,
        /// against 8% before; 30% with half from 85).
        /// </summary>
        public const float RestLeast = 0.7f, RestLeastAt = 90f;

        public static float RestFactor(float strain)
        {
            strain = Clean(strain);
            if (strain <= RestFullBelow) return 1f;
            return HeartMath.Lerp(1f, RestLeast, (strain - RestFullBelow) / (RestLeastAt - RestFullBelow));
        }

        // ---------------- The two sides at once ----------------

        /// <summary>The good side grows in full up to here, then slower.</summary>
        public const float GrowthFullBelow = 10f;

        /// <summary>And at this share from <see cref="GrowthLeastAt"/> on: harder, never shut.</summary>
        public const float GrowthLeast = 0.3f, GrowthLeastAt = 85f;

        public static float Growth(float strain)
        {
            strain = Clean(strain);
            if (strain <= GrowthFullBelow) return 1f;
            return HeartMath.Lerp(1f, GrowthLeast, (strain - GrowthFullBelow) / (GrowthLeastAt - GrowthFullBelow));
        }

        /// <summary>
        /// A good thing while there is strain: this share of it goes to the good side (at the source's own share), the rest
        /// eases strain; what can't ease anything (strain gone) goes to the good side too.
        /// </summary>
        public const float GoodSplit = 0.35f;

        /// <summary>What the hour's relief did (Settle): rest, a cry, and each good thing's easing and what it brings the good side.</summary>
        public sealed class Settled
        {
            public float rest, catharsis;
            public readonly float[] eased = new float[HeartMath.GoodSourceCount];
            public readonly float[] toGood = new float[HeartMath.GoodSourceCount];

            public float GoodEased
            {
                get
                {
                    float sum = 0f;
                    for (int i = 0; i < eased.Length; i++) sum += eased[i];
                    return sum;
                }
            }

            public void Clear()
            {
                rest = catharsis = 0f;
                Array.Clear(eased, 0, eased.Length);
                Array.Clear(toGood, 0, toGood.Length);
            }
        }

        /// <summary>
        /// The end of the hour, after the hardship of it is on <paramref name="strain"/>: rest eases it (less the higher it is,
        /// never below <paramref name="floor"/>), a cry lets it out (below the floor too), then each good thing in turn eases
        /// it and fills the good side together. <paramref name="good"/> is by GoodSource. Returns the strain left; what each
        /// part did is in <paramref name="into"/> (what goes to the good side before the good side's own caps). With the good
        /// side off (<paramref name="goodSide"/> false), good things ease strain in full and fill nothing.
        /// </summary>
        public static float Settle(float strain, float floor, float rest, float catharsis, float[] good, Settled into, bool goodSide = true)
        {
            into.Clear();
            float s = Clean(strain);
            float growth = Growth(s);
            float r = Clean(rest) * RestFactor(s);
            float low = Math.Min(s, Clean(floor));
            float afterRest = Math.Max(low, s - r);
            into.rest = s - afterRest;
            s = afterRest;
            float c = Math.Min(s, Clean(catharsis));
            into.catharsis = c;
            s -= c;
            if (good != null)
                for (int i = 0; i < good.Length && i < HeartMath.GoodSourceCount; i++)
                {
                    float a = Clean(good[i]);
                    if (a <= 0f) continue;
                    float e = s > 0f ? Math.Min(s, a * (goodSide ? 1f - GoodSplit : 1f)) : 0f;
                    s -= e;
                    into.eased[i] = e;
                    into.toGood[i] = goodSide ? (a - e) * HeartMath.GoodShare((GoodSource)i) * growth : 0f;
                }
            return Math.Max(0f, s);
        }

        // ---------------- On the map: acts, lines and a sign over the head ----------------

        /// <summary>One act a colonist at most in so many hours, and one in the colony (each map) in so many.</summary>
        public const int ActPawnHours = 16, ActColonyHours = 3;

        /// <summary>
        /// The chance an hour of a small act, for someone free to: by how worn they are, how high their good side stands, and
        /// how strong their strongest feeling is (0 to 1 of full). Nothing at all for someone steady with nothing strong.
        /// </summary>
        public static float ActChance(StrainBand band, GoodBand good, float strongest)
        {
            float none = 1f;
            switch (band)
            {
                case StrainBand.Strained: none *= 1f - 0.07f; break;
                case StrainBand.Worn: none *= 1f - 0.12f; break;
                case StrainBand.Breaking: none *= 1f - 0.16f; break;
            }
            switch (good)
            {
                case GoodBand.GoodHeart: none *= 1f - 0.05f; break;
                case GoodBand.HighSpirits: none *= 1f - 0.09f; break;
            }
            if (strongest >= StrongFeeling) none *= 1f - 0.12f * Math.Min(1f, strongest);
            return 1f - none;
        }

        /// <summary>A feeling counts as strong from here (its share of full intensity): it acts, it shows over the head.</summary>
        public const float StrongFeeling = 0.6f;

        /// <summary>A sign over the head: once in so many hours for each colonist at most, and its chance an hour while a feeling holds strong.</summary>
        public const int SignHours = 8;

        public const float SignChance = 0.2f;

        /// <summary>A sign now: always when the feeling has just grown strong (once the hours are past), now and then while it holds.</summary>
        public static bool SignNow(bool justStrong, int hoursSinceLast, float roll) => hoursSinceLast >= SignHours && (justStrong || roll < SignChance);

        /// <summary>A line when their band changes waits so many hours for them to be in view, at most.</summary>
        public const int TurnLineHours = 8;

        // ---------------- Telling the player ----------------

        /// <summary>"Back from it" only this far below where worn starts, so a heart hovering at the line isn't told twice.</summary>
        public const float BackMargin = 5f;

        /// <summary>
        /// What to tell now, from the bands last told (<paramref name="toldStrain"/>, <paramref name="toldGood"/>: a band's
        /// number, -1 never told) and where the heart is now; the bands told are moved on. One message at most: strain's first.
        /// The first look (never told) only takes note.
        /// </summary>
        public static HeartTell Tell(ref int toldStrain, ref int toldGood, float strain, float good, bool goodOn)
        {
            var band = HeartMath.BandFor(Clean(strain));
            var gband = goodOn ? HeartMath.GoodBandFor(Clean(good)) : GoodBand.None;
            if (toldStrain < 0 || toldGood < 0)
            {
                toldStrain = (int)band;
                toldGood = (int)gband;
                return HeartTell.None;
            }
            var tell = HeartTell.None;
            if (band >= StrainBand.Worn && (int)band > toldStrain)
            {
                tell = band == StrainBand.Breaking ? HeartTell.Breaking : HeartTell.Worn;
                toldStrain = (int)band;
            }
            else if (toldStrain >= (int)StrainBand.Worn && strain < HeartMath.BandStart(StrainBand.Worn) - BackMargin)
            {
                tell = HeartTell.Back;
                toldStrain = (int)band;
            }
            else if (toldStrain == (int)StrainBand.Breaking && strain < HeartMath.BandStart(StrainBand.Breaking) - BackMargin)
                toldStrain = (int)band;
            else if ((int)band < toldStrain && toldStrain < (int)StrainBand.Worn)
                toldStrain = (int)band;

            if (gband >= GoodBand.GoodHeart && (int)gband > toldGood)
            {
                toldGood = (int)gband;
                if (tell == HeartTell.None) tell = gband == GoodBand.HighSpirits ? HeartTell.HighSpirits : HeartTell.GoodHeart;
            }
            else if ((int)gband < toldGood && good < HeartMath.GoodBandStart((GoodBand)toldGood) - BackMargin)
                toldGood = (int)gband;
            return tell;
        }

        private static float Clean(float v) => float.IsNaN(v) || v < 0f ? 0f : v;
    }
}
