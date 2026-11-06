using System;

namespace ColonyCouncil
{
    /// <summary>How a letter goes on the road, by the colony's means in its age (a call over the comms console is the other channel).</summary>
    public enum LetterMeans
    {
        /// <summary>Tribal: a runner carries it on from the messenger who came for it.</summary>
        Runner,
        /// <summary>Medieval: a rider.</summary>
        Rider,
        /// <summary>Industrial: a courier on the roads.</summary>
        Courier,
        /// <summary>Spacer: a courier by shuttle.</summary>
        Shuttle
    }

    /// <summary>What an answer brings beside its words, for the purposes whose answer is only words otherwise.</summary>
    public enum AnswerBrings
    {
        /// <summary>Words only: the regard moved, said; with a curt answer or a refusal, why.</summary>
        Nothing,
        /// <summary>A small gift in the messenger's pack.</summary>
        Token,
        /// <summary>How their town is faring, in their own words.</summary>
        TownNews,
        /// <summary>Their people remember it (a loss mourned with them, a warning heeded), and their goodwill moves.</summary>
        Remembered,
        /// <summary>A warning heeded warmly: their people count it a debt of honour the colony may call in.</summary>
        FavourOwed
    }

    /// <summary>How far an answer moved their regard of the colony, said in words.</summary>
    public enum RegardMove
    {
        /// <summary>Into a warmer band (civil to friendly).</summary>
        UpBand,
        /// <summary>Warmer by a good deal, in the same band.</summary>
        Up,
        /// <summary>A little warmer.</summary>
        Little,
        /// <summary>Not moved.</summary>
        Same,
        /// <summary>A little cooler, in the same band.</summary>
        Down,
        /// <summary>Into a cooler band.</summary>
        DownBand
    }

    /// <summary>Why an answer was curt or a refusal: the heaviest thing against it.</summary>
    public enum LetterWhy
    {
        /// <summary>Their people are at war with the colony.</summary>
        War,
        /// <summary>They don't think well enough of the colony.</summary>
        Regard,
        /// <summary>They don't think well of the one who wrote.</summary>
        Writer,
        /// <summary>The writer's words didn't come out well.</summary>
        Words,
        /// <summary>A cold or abrasive nature.</summary>
        Nature,
        /// <summary>Hard times at home: little to spare.</summary>
        HardTimes,
        /// <summary>The colony wrote for the same thing only lately.</summary>
        Lately,
        /// <summary>A call counts for less than a letter in a hand.</summary>
        ByCall,
        /// <summary>What was asked is a great deal.</summary>
        Asked,
        /// <summary>Nothing much against it: not the day for it.</summary>
        Luck
    }

    /// <summary>
    /// Phase 6 of the review, part 1 (the player's decision 12 of 6 November 2026: "letters to notables arrive in days and an
    /// answer brings something concrete"): how long a letter is on the road by the colony's means in its age (a runner, a
    /// rider, a courier, a courier by shuttle; a call over the comms console is at once), what the answer to a letter of
    /// kind words brings beside them, how far it moved their regard in words, and why an answer was curt or a refusal. No
    /// game types, so Tools/Tests can check it.
    /// </summary>
    public static class LetterMath
    {
        /// <summary>The colony's means of sending a letter by its age.</summary>
        public static LetterMeans MeansOf(WorldAge ours)
        {
            switch (ours)
            {
                case WorldAge.Tribal: return LetterMeans.Runner;
                case WorldAge.Medieval: return LetterMeans.Rider;
                case WorldAge.Industrial: return LetterMeans.Courier;
                default: return LetterMeans.Shuttle;
            }
        }

        /// <summary>Half a day at the start of each leg: handed on at the first crossroads, found at the other end.</summary>
        public const float LegStartDays = 0.5f;

        /// <summary>Days per world tile, by the means: a runner about eleven tiles a day, a rider sixteen, a courier twenty-five, a shuttle sixty-five.</summary>
        public static float PerTile(LetterMeans m)
        {
            switch (m)
            {
                case LetterMeans.Runner: return 0.09f;
                case LetterMeans.Rider: return 0.06f;
                case LetterMeans.Courier: return 0.04f;
                default: return 0.015f;
            }
        }

        /// <summary>The longest a leg takes, however far: six days by runner, five by rider, four by courier, two by shuttle.</summary>
        public static float MaxLegDays(LetterMeans m)
        {
            switch (m)
            {
                case LetterMeans.Runner: return 6f;
                case LetterMeans.Rider: return 5f;
                case LetterMeans.Courier: return 4f;
                default: return 2f;
            }
        }

        /// <summary>
        /// Days a letter (or its answer) is on the road over so many world tiles by the colony's means: half a day to start,
        /// then the tiles at the means' pace, at most <see cref="MaxLegDays"/>. A neighbour ten tiles off hears in a day or
        /// so, one fifty off in two to five days. (Before phase 6 a leg took the other people's walking pace and up to twelve
        /// days; letters already on the road keep the day they were given.)
        /// </summary>
        public static float LegDays(float tiles, LetterMeans m)
        {
            float d = LegStartDays + Math.Max(0f, tiles) * PerTile(m);
            return Math.Max(LegStartDays, Math.Min(MaxLegDays(m), d));
        }

        /// <summary>The leg the old way (NotableMath.TravelDays by the other people's tech), for the before and after in the docs and tests.</summary>
        public static float OldLegDays(float tiles, int theirTech) => NotableMath.TravelDays(tiles, theirTech);

        // ---------------- What the answer brings ----------------

        /// <summary>
        /// What the answer to a letter of kind words brings beside them (the purposes whose answer is otherwise words only:
        /// greetings, condolences, a warning). Greetings: a token gift with a warm answer, how their town fares with a
        /// cordial or cool one. Condolences: remembered by their people with a warm or cordial one. A warning: a debt of
        /// honour with a warm answer, heeded and remembered with a cordial or cool one. A curt answer, a refusal or silence
        /// brings nothing but why. Other purposes bring what they always did (news, a visit, the help asked, their own).
        /// </summary>
        public static AnswerBrings Brings(LetterPurpose p, AnswerTone t)
        {
            if (t == AnswerTone.Curt || t == AnswerTone.Refusal || t == AnswerTone.Silence) return AnswerBrings.Nothing;
            switch (p)
            {
                case LetterPurpose.Greetings:
                    return t == AnswerTone.Warm ? AnswerBrings.Token : AnswerBrings.TownNews;
                case LetterPurpose.Condolences:
                    return t == AnswerTone.Cool ? AnswerBrings.Nothing : AnswerBrings.Remembered;
                case LetterPurpose.Warning:
                    return t == AnswerTone.Warm ? AnswerBrings.FavourOwed : AnswerBrings.Remembered;
                default:
                    return AnswerBrings.Nothing;
            }
        }

        /// <summary>A warm greeting's token, in silver's worth before the colony's wealth: modest, never a fortune.</summary>
        public static float TokenValue(float roll) => 50f + NotableMath.Clamp01(roll) * 50f;

        /// <summary>The weight of a warning heeded warmly in their people's memory: enough to be a debt of honour (FavourMath.MinSize).</summary>
        public const int HeededWarmly = 10;

        // ---------------- Regard, in words ----------------

        /// <summary>How the answer moved their regard of the colony: by its band before and after, and the shift.</summary>
        public static RegardMove Move(int before, int after, float shift)
        {
            int a = NotableMath.Level(before), b = NotableMath.Level(after);
            if (b < a) return RegardMove.UpBand;
            if (b > a) return RegardMove.DownBand;
            if (shift >= 4f) return RegardMove.Up;
            if (shift > 0.05f) return RegardMove.Little;
            if (shift < -0.05f) return RegardMove.Down;
            return RegardMove.Same;
        }

        // ---------------- Why not ----------------

        /// <summary>
        /// Why an answer was curt or a refusal: the heaviest of what counted against it in <see cref="NotableMath.Score"/>
        /// (a people at war first of all; then their regard of the colony or of the writer, the writer's words, their nature,
        /// hard times, writing lately, a call, the size of what was asked), else luck.
        /// </summary>
        public static LetterWhy Why(LetterInput x)
        {
            if (x.hostile) return LetterWhy.War;
            var best = LetterWhy.Luck;
            float worst = -2f;
            void Weigh(LetterWhy why, float v)
            {
                if (v < worst)
                {
                    worst = v;
                    best = why;
                }
            }
            // Against a friendly regard (10, the colony's "friendly" band), as the score counts it.
            Weigh(LetterWhy.Regard, (x.opinion - 10) * 0.55f);
            Weigh(LetterWhy.Writer, Math.Min(0, x.ofWriter) * 0.35f);
            Weigh(LetterWhy.Words, (NotableMath.Clamp(x.social, 0, 20) - 6) * 1.5f);
            Weigh(LetterWhy.Nature, NotableMath.Clamp(x.warmth, -2, 2) * 6f);
            if (Business(x.purpose))
            {
                Weigh(LetterWhy.Asked, -10f);
                if (x.troubled) Weigh(LetterWhy.HardTimes, -15f);
            }
            if (x.lately) Weigh(LetterWhy.Lately, -10f);
            if (x.channel == LetterChannel.Console) Weigh(LetterWhy.ByCall, -6f);
            return best;
        }

        /// <summary>A letter that asks something of them (help, a debt, a pact, a truce...), rather than kind words, news or an invitation.</summary>
        public static bool Business(LetterPurpose p) =>
            p != LetterPurpose.Greetings && p != LetterPurpose.Condolences && p != LetterPurpose.Warning && p != LetterPurpose.News && p != LetterPurpose.Invitation;

        /// <summary>An answer that says why: a curt one or a refusal (silence says nothing).</summary>
        public static bool SaysWhy(AnswerTone t) => t == AnswerTone.Curt || t == AnswerTone.Refusal;
    }
}
