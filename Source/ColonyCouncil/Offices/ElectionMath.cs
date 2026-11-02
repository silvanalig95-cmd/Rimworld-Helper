using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>When a plank is something a candidate would run on: what the colony is going through right now.</summary>
    public enum PlankWhen
    {
        Always,
        /// <summary>A big raid or threat in the last fifteen days.</summary>
        RecentRaid,
        /// <summary>No big threat in the last fifteen days.</summary>
        Quiet,
        /// <summary>Fewer cooked meals than two for each mouth, or under five days of food.</summary>
        MealsShort,
        /// <summary>Fifteen days of food or more.</summary>
        FoodPlenty,
        /// <summary>Someone at home is ill or hurt and ought to be in bed, or has wounds to tend.</summary>
        Sick,
        /// <summary>Less medicine than one for each colonist.</summary>
        MedicineLow,
        /// <summary>The colony's mood is low (under 45% on average).</summary>
        LowMood,
        /// <summary>Trust in the council is low on average (under 5).</summary>
        LowTrust,
        /// <summary>Two colonists are known rivals.</summary>
        Rivalries,
        /// <summary>Spring or summer.</summary>
        Growing,
        /// <summary>Fall or winter.</summary>
        Cold,
        /// <summary>The colony keeps three animals or more.</summary>
        Herd,
        /// <summary>The calendar has a tradition.</summary>
        Traditions
    }

    /// <summary>
    /// The arithmetic of an election that plays out on the map (Offices/OfficesElection.cs), free of game types so the
    /// tests can cover it (Tools/Tests/ElectionTests.cs): how far a candidate's word moves a voter, what a showing at the
    /// hustings is worth, what the council's endorsement does, the poll (the same vote math, without the luck of the day),
    /// how the count is read out, and the days it takes.
    /// </summary>
    public static class ElectionMath
    {
        // ---------------- The vote ----------------

        /// <summary>The luck of the day on every ballot: each candidate gets a draw of 0 to this much (OfficeTracker.Score).</summary>
        public const float VoteNoise = 2.5f;

        /// <summary>
        /// How likely each candidate is to top this voter's ballot, given how the voter leans to each (<paramref name="leans"/>)
        /// and a fresh draw of 0 to <paramref name="noise"/> added to each: the chance that a candidate's lean plus draw beats
        /// every other's. Worked out by summing over the candidate's draw in fine steps; the shares add up to one.
        /// </summary>
        public static float[] Shares(IList<float> leans, float noise = VoteNoise, int steps = 96)
        {
            int n = leans?.Count ?? 0;
            var shares = new float[n];
            if (n == 0) return shares;
            if (n == 1)
            {
                shares[0] = 1f;
                return shares;
            }
            if (noise <= 0f)
            {
                int best = Leader(leans);
                shares[best] = 1f;
                return shares;
            }
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                double p = 0;
                for (int s = 0; s < steps; s++)
                {
                    double x = (s + 0.5) / steps * noise;
                    double mine = leans[i] + x;
                    double all = 1;
                    for (int j = 0; j < n && all > 0; j++)
                    {
                        if (j == i) continue;
                        // The chance the other's lean plus their own draw falls below this one's.
                        double below = (mine - leans[j]) / noise;
                        all *= below <= 0 ? 0 : below >= 1 ? 1 : below;
                    }
                    p += all;
                }
                shares[i] = (float)(p / steps);
                total += shares[i];
            }
            if (total > 0)
                for (int i = 0; i < n; i++) shares[i] = (float)(shares[i] / total);
            return shares;
        }

        /// <summary>The poll: each voter's shares (<see cref="Shares"/>) added up, so a candidate's number is the votes they can expect.</summary>
        public static float[] Poll(IList<float[]> voters, int candidates, float noise = VoteNoise)
        {
            var sum = new float[Math.Max(0, candidates)];
            if (voters == null) return sum;
            foreach (var leans in voters)
            {
                if (leans == null || leans.Length != candidates) continue;
                var s = Shares(leans, noise);
                for (int i = 0; i < candidates; i++) sum[i] += s[i];
            }
            return sum;
        }

        /// <summary>The candidate a voter leans to most (the first of equals), or -1 with none.</summary>
        public static int Leader(IList<float> values)
        {
            int best = -1;
            for (int i = 0; i < (values?.Count ?? 0); i++)
                if (best < 0 || values[i] > values[best]) best = i;
            return best;
        }

        /// <summary>The poll in whole votes for the page: the expected votes rounded, the largest remainders first, adding up to the voters.</summary>
        public static int[] Rounded(IList<float> expected, int voters)
        {
            int n = expected?.Count ?? 0;
            var whole = new int[n];
            if (n == 0 || voters <= 0) return whole;
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Math.Max(0f, expected[i]);
            if (sum <= 0f) return whole;
            var rest = new List<(int i, float r)>();
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                float v = Math.Max(0f, expected[i]) / sum * voters;
                whole[i] = (int)Math.Floor(v);
                given += whole[i];
                rest.Add((i, v - whole[i]));
            }
            rest.Sort((a, b) => b.r != a.r ? b.r.CompareTo(a.r) : a.i.CompareTo(b.i));
            for (int k = 0; given < voters && k < rest.Count; k++, given++) whole[rest[k].i]++;
            return whole;
        }

        // ---------------- The campaign ----------------

        /// <summary>All a candidate's visits to one voter add up to at most this much (about the luck of the day).</summary>
        public const float CanvassCap = 2f;

        /// <summary>A candidate visits at most this many voters a day.</summary>
        public const int CanvassPerDay = 3;

        /// <summary>
        /// What one visit does to the voter's lean toward the candidate: more from a good talker (0.15 at Social 0, 1.15
        /// at 20), times how the listener feels about them (<see cref="OpinionFactor"/>); someone they dislike puts them off.
        /// </summary>
        public static float CanvassNudge(int social, float opinion)
        {
            float talk = 0.15f + 0.05f * Clamp(social, 0, 20);
            return talk * OpinionFactor(opinion);
        }

        /// <summary>How the listener's opinion of the candidate (-100 to 100) colours their words: 0.4 at indifference, up to 1.4 for a friend, down to -0.6.</summary>
        public static float OpinionFactor(float opinion) => Clamp(0.4f + opinion / 50f, -0.6f, 1.4f);

        /// <summary>A visit's nudge added to what the voter already holds from this candidate's visits, kept within the cap either way.</summary>
        public static float AddNudge(float held, float nudge) => Clamp(held + nudge, -CanvassCap, CanvassCap);

        // ---------------- The hustings ----------------

        /// <summary>A showing this good or better wins listeners over; this bad or worse loses them a little.</summary>
        public const float GoodShowing = 0.25f, PoorShowing = -0.25f;

        /// <summary>The most a showing at the hustings moves a listener, either way.</summary>
        public const float ShowingCap = 1.5f;

        /// <summary>
        /// How a candidate's speech goes: their Social (6 is ordinary), their mood (0 to 1), what their nature adds
        /// (<paramref name="traits"/>: the kind, the steady and the abrasive), and the moment (<paramref name="roll"/>, 0 to 1).
        /// The result is what every listener's lean toward them moves by.
        /// </summary>
        public static float Showing(int social, float mood, float traits, float roll)
        {
            float s = (Clamp(social, 0, 20) - 6f) * 0.12f + (Clamp(mood, 0f, 1f) - 0.5f) * 1.2f + traits + (Clamp(roll, 0f, 1f) - 0.5f);
            return Clamp(s, -ShowingCap, ShowingCap);
        }

        public static bool Good(float showing) => showing >= GoodShowing;

        public static bool Poor(float showing) => showing <= PoorShowing;

        // ---------------- The council's hand ----------------

        /// <summary>Trust each voter leaning to another candidate loses when the council endorses one.</summary>
        public const float EndorseTrustCost = 2f;

        /// <summary>Trust each supporter of the one who would have won loses when the council calls the vote off and appoints someone else.</summary>
        public const float AppointTrustCost = 3f;

        /// <summary>
        /// What the council's endorsement is worth on a voter's ballot, by their trust in the council (-100 to 100): those
        /// who trust it follow its lead (up to two), the doubtful hardly, the bitter vote against it.
        /// </summary>
        public static float EndorseLean(float trust)
        {
            if (trust <= -45f) return -0.75f;
            if (trust <= -15f) return 0.1f;
            return 0.5f + 1.5f * Clamp((trust + 15f) / 75f, 0f, 1f);
        }

        // ---------------- The days ----------------

        /// <summary>Days of campaigning before the hustings' evening: three for the yearly election, two to fill an empty seat.</summary>
        public static int CampaignDays(bool byElection) => byElection ? 2 : 3;

        /// <summary>How many stand for a seat: three where the colony is big enough to make a race of it, else two.</summary>
        public static int CandidatesPerSeat(int voters) => voters >= 6 ? 3 : 2;

        /// <summary>The hours of an election's day: the hustings and the count at six in the evening, the polls open from seven in the morning.</summary>
        public const int EveningHour = 18, PollsOpenHour = 7;

        /// <summary>From this hour of polling day, voters are called from ordinary work too, not only their free time.</summary>
        public const int AfterWorkHour = 14;

        /// <summary>An evening, or a polling day, is put off at most this many times; then it goes ahead with what there is.</summary>
        public const int MaxPutOffs = 2;

        /// <summary>Polling day is extended when fewer than this share of the voters at home could vote (a raid, a storm of trouble).</summary>
        public const float EnoughVoted = 0.5f;

        /// <summary>
        /// How many voters to call to the polls this hour: the ones still to vote spread over the hours left, plus one,
        /// never more than four at once (so a few stand at the table, not a crowd).
        /// </summary>
        public static int CallsThisHour(int left, int hoursLeft)
        {
            if (left <= 0) return 0;
            int spread = (int)Math.Ceiling(left / (double)Math.Max(1, hoursLeft));
            return Math.Min(4, Math.Min(left, spread + 1));
        }

        // ---------------- The count ----------------

        /// <summary>
        /// How the counter reads one seat's ballots aloud, in the order they were cast: vote by vote when there are at
        /// most <paramref name="maxCalls"/>; otherwise in handfuls of the same size, each handful read as one call per
        /// candidate in it. Returns (candidate, votes) calls; together they give every candidate exactly their tally.
        /// </summary>
        public static List<(int candidate, int votes)> ReadOut(IList<int> ballots, int maxCalls)
        {
            var calls = new List<(int, int)>();
            int n = ballots?.Count ?? 0;
            if (n == 0) return calls;
            int size = n <= Math.Max(1, maxCalls) ? 1 : (int)Math.Ceiling(n / (double)Math.Max(1, maxCalls));
            for (int start = 0; start < n; start += size)
            {
                var order = new List<int>();
                var count = new Dictionary<int, int>();
                for (int k = start; k < Math.Min(n, start + size); k++)
                {
                    int c = ballots[k];
                    if (!count.ContainsKey(c))
                    {
                        count[c] = 0;
                        order.Add(c);
                    }
                    count[c]++;
                }
                foreach (int c in order) calls.Add((c, count[c]));
            }
            return calls;
        }

        /// <summary>
        /// A ranked ballot's vote once some candidates are out of the race (already seated in an earlier seat, gone from
        /// the colony): the first of its choices still standing, or -1 when none is.
        /// </summary>
        public static int FirstStanding(IList<int> ranking, ICollection<int> standing)
        {
            if (ranking == null || standing == null) return -1;
            foreach (int c in ranking)
                if (standing.Contains(c)) return c;
            return -1;
        }

        /// <summary>"12>4>9": a ranked ballot as it is kept, and back.</summary>
        public static string Join(IList<int> ranking) => ranking == null ? "" : string.Join(">", ranking);

        public static List<int> Split(string ballot)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(ballot)) return list;
            foreach (var part in ballot.Split('>'))
                if (int.TryParse(part, out int id)) list.Add(id);
            return list;
        }

        /// <summary>
        /// The winner of a tally: the most votes, then (between equals) the more able (<paramref name="tieBreak"/>), then the
        /// first. -1 with no candidates.
        /// </summary>
        public static int Winner(IList<int> votes, IList<float> tieBreak)
        {
            int best = -1;
            for (int i = 0; i < (votes?.Count ?? 0); i++)
            {
                if (best < 0 || votes[i] > votes[best]) best = i;
                else if (votes[i] == votes[best] && tieBreak != null && i < tieBreak.Count && best < tieBreak.Count && tieBreak[i] > tieBreak[best]) best = i;
            }
            return best;
        }

        /// <summary>The whole count reads about this many calls, however many seats it has.</summary>
        public const int CallBudget = 30;

        /// <summary>How many calls one seat's ballots are read in: the budget shared between the seats with a race, two at least.</summary>
        public static int CallsPerSeat(int contested) => Math.Max(2, CallBudget / Math.Max(1, contested));

        /// <summary>How many lines the count takes for its seats, so its gathering lasts long enough: an opening, the calls, three for each seat (the winner, a cheer, a word from a loser) and the toast.</summary>
        public static int CountLines(int calls, int seats) => 2 + Math.Max(0, calls) + 3 * Math.Max(0, seats);

        /// <summary>A line is said every this many ticks at the hustings and the count.</summary>
        public const int LineTicks = 150;

        /// <summary>
        /// A gathering of <paramref name="lines"/> lines: an hour for people to arrive, the lines with room to spare (a
        /// speaker waited for, a line not said), and a little time after; one to six hours. It ends as soon as all is said.
        /// </summary>
        public static int GatheringTicks(int lines) => Math.Max(2500, Math.Min(15000, 2500 + lines * LineTicks * 5 / 4 + 1000));

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
