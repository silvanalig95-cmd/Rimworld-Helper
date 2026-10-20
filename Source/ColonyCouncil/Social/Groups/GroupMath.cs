using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The part someone plays in a group (<see cref="SocialGroup"/>). None: one of them, nothing more.</summary>
    public enum GroupRole
    {
        None,
        /// <summary>Warm, sure of themselves, good with people, with the most friends in it: the one it gathers round.</summary>
        Anchor,
        /// <summary>The one who makes the jokes.</summary>
        Joker,
        /// <summary>Warm and calm: mends the quarrels.</summary>
        Peacemaker,
        /// <summary>Quick-tempered and proud: stirs things up.</summary>
        Firebrand,
        /// <summary>Gloomy and big-hearted: frets for everyone.</summary>
        Worrier,
        /// <summary>Says little, and is there anyway.</summary>
        Quiet,
        /// <summary>Joined lately: still finding their place.</summary>
        Newcomer
    }

    /// <summary>What one person brings to a group, for their role: their character and how they behave.</summary>
    public struct RoleFacts
    {
        public float warmth, nerve, temper, humour, trust, pride, openness, outlook;
        /// <summary>Social skill (0-20), 6 when unknown.</summary>
        public int social;
        /// <summary>The share of the other members they are friends with (0-1).</summary>
        public float tieShare;
        /// <summary>Quarrels they have mended (the game's record).</summary>
        public int mended;
        /// <summary>Joined lately (within <see cref="GroupMath.NewcomerDays"/>), into a group older than that.</summary>
        public bool newcomer;
    }

    /// <summary>How two people stand with each other, from the game, for who drifts together.</summary>
    public struct PairFacts
    {
        /// <summary>Strength (0-100) of the announced bonds between them.</summary>
        public float friends, rivals, comrades;
        public bool mentor, family, lovers;
        /// <summary>The lower of their two opinions of each other.</summary>
        public int minOpinion;
        /// <summary>How much of their days go to the same work (0-1).</summary>
        public float sharedWork;
        /// <summary>Both keep the night hours.</summary>
        public bool nightShift;
        /// <summary>How alike they are (<see cref="PersonaMath.Likeness"/>, 0-1).</summary>
        public float likeness;
        /// <summary>The same faith, both holding to it (only where faith divides the colony or is held strongly).</summary>
        public bool sameFaith;
        /// <summary>Came to the colony on the same day.</summary>
        public bool arrivedTogether;
        /// <summary>The same kind of life before (both tribe-born in a colony that isn't, both from off-world...).</summary>
        public bool sameOrigin;
        public bool bothYoung, bothOld;
    }

    /// <summary>One day in a group's life, for its cohesion.</summary>
    public struct CohesionDay
    {
        /// <summary>How much the members hold to each other on average (<see cref="GroupMath.Attachment"/>).</summary>
        public float attachment;
        public int habits, memories, quarrels, mends, died, left;
        /// <summary>Fewer than two of them at home together today, and for how many days running.</summary>
        public bool apart;
        public int apartDays;
    }

    /// <summary>One day of a newcomer finding their way into a group.</summary>
    public struct IntegrationDay
    {
        /// <summary>What the members think of them, on average (opinion).</summary>
        public float opinion;
        /// <summary>Things they went through with members today (shared memories).</summary>
        public int memories;
        /// <summary>How much of their work they share with the members (0-1).</summary>
        public float sharedWork;
        /// <summary>A member speaks for them.</summary>
        public bool sponsor;
        /// <summary>Welcomes from members today.</summary>
        public int welcomes;
    }

    /// <summary>A member's role over the days: the role, and the one they're drifting into (and for how long).</summary>
    public struct RoleState
    {
        public GroupRole role;
        public GroupRole pending;
        public int pendingDays;
    }

    /// <summary>
    /// The arithmetic of groups, with no game types, so the tests can check it: who drifts together (affinity, the
    /// clusters that become groups, who joins and who drifts off), the roles people settle into (slowly: a role changes
    /// only after the new one has fitted better for days), how close a group is (cohesion), and when it splits, merges
    /// or welcomes a newcomer.
    /// </summary>
    public static class GroupMath
    {
        /// <summary>A new group needs three; joining stops at seven; a merge may make nine.</summary>
        public const int MinSize = 3, MaxSize = 7, MergeMax = 9;
        /// <summary>Nobody belongs to more than two groups.</summary>
        public const int MaxGroupsEach = 2;
        /// <summary>Mean affinity a new group needs; what joining one takes; below this a member drifts off.</summary>
        public const float FormAt = 0.42f, JoinAt = 0.38f, LeaveBelow = 0.1f;
        /// <summary>Days below <see cref="LeaveBelow"/> before someone drifts off.</summary>
        public const int LeaveDays = 4;
        /// <summary>Cohesion under which a group with a rift in it splits (after <see cref="SplitDays"/> days), and under which it falls apart.</summary>
        public const float SplitBelow = 25f, DissolveBelow = 6f;
        public const int SplitDays = 3, DissolveDays = 5;
        /// <summary>Days a new member stays the newcomer.</summary>
        public const int NewcomerDays = 10;
        /// <summary>Days a better role has to fit before someone changes role, and by how much it must fit better.</summary>
        public const int RolePendingDays = 3;
        public const float RoleMargin = 12f;
        /// <summary>A rivalry between groups shows from here, and is over below this.</summary>
        public const float RivalryShows = 35f, RivalryEnds = 12f;
        /// <summary>Under this welcome a group closes ranks against a newcomer.</summary>
        public const float CloseRanksBelow = 0.15f;
        public const float Accepted = 100f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ---------------- Who drifts together ----------------

        /// <summary>
        /// How much two people pull together, roughly -1..1.5: friendship counts most and a rivalry pushes hardest; work
        /// shared, a fight fought together, the night hours, being alike, faith, where they came from and their age add.
        /// </summary>
        public static float Affinity(PairFacts f)
        {
            float a = 0f;
            a += Clamp(f.friends, 0f, 100f) / 100f * 0.9f;
            a -= Clamp(f.rivals, 0f, 100f) / 100f * 1.3f;
            a += Clamp(f.comrades, 0f, 100f) / 100f * 0.45f;
            if (f.mentor) a += 0.15f;
            if (f.family) a += 0.15f;
            if (f.lovers) a += 0.2f;
            a += Clamp(f.minOpinion, -100, 100) / 100f * 0.35f;
            a += Clamp(f.sharedWork, 0f, 1f) * 0.3f;
            if (f.nightShift) a += 0.2f;
            a += (Clamp(f.likeness, 0f, 1f) - 0.65f) * 0.8f;
            if (f.sameFaith) a += 0.12f;
            if (f.arrivedTogether) a += 0.12f;
            if (f.sameOrigin) a += 0.08f;
            if (f.bothYoung || f.bothOld) a += 0.12f;
            return a;
        }

        /// <summary>How much <paramref name="who"/> holds to the others in <paramref name="members"/>: their mean affinity (0 alone).</summary>
        public static float Attachment(float[,] aff, int who, IList<int> members)
        {
            float sum = 0f;
            int n = 0;
            foreach (int m in members)
            {
                if (m == who) continue;
                sum += aff[who, m];
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        /// <summary>The mean affinity over every pair in a set (0 for fewer than two).</summary>
        public static float Mean(float[,] aff, IList<int> set)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < set.Count; i++)
            for (int j = i + 1; j < set.Count; j++)
            {
                sum += aff[set[i], set[j]];
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        /// <summary>The worst pair in a set: the two who pull apart most (their affinity).</summary>
        public static float Worst(float[,] aff, IList<int> set, out int a, out int b)
        {
            float worst = float.MaxValue;
            a = b = -1;
            for (int i = 0; i < set.Count; i++)
            for (int j = i + 1; j < set.Count; j++)
            {
                float v = aff[set[i], set[j]];
                if (v < worst)
                {
                    worst = v;
                    a = set[i];
                    b = set[j];
                }
            }
            return a < 0 ? 0f : worst;
        }

        /// <summary>How many of <paramref name="a"/> are also in <paramref name="b"/>.</summary>
        public static int Overlap(IList<int> a, IList<int> b)
        {
            int n = 0;
            foreach (int x in a)
                if (b.Contains(x))
                    n++;
            return n;
        }

        /// <summary>
        /// The best new group among <paramref name="n"/> people, or null. It grows from the closest pair of people still
        /// free to join one (<paramref name="free"/>): each step adds whoever pulls hardest towards those already in it,
        /// as long as they pull towards it well enough (<see cref="JoinAt"/>) and against nobody in it. It counts when it
        /// has <see cref="MinSize"/> or more, holds together well enough (<see cref="FormAt"/>), and isn't mostly a group
        /// that already exists (two thirds of it in one of <paramref name="existing"/>).
        /// </summary>
        public static List<int> Cluster(float[,] aff, int n, bool[] free, List<List<int>> existing)
        {
            var pairs = new List<(int i, int j, float v)>();
            for (int i = 0; i < n; i++)
            {
                if (!free[i]) continue;
                for (int j = i + 1; j < n; j++)
                    if (free[j] && aff[i, j] >= FormAt)
                        pairs.Add((i, j, aff[i, j]));
            }
            pairs.Sort((x, y) =>
            {
                int c = y.v.CompareTo(x.v);
                return c != 0 ? c : x.i != y.i ? x.i.CompareTo(y.i) : x.j.CompareTo(y.j);
            });
            int tried = 0;
            foreach (var (i, j, _) in pairs)
            {
                if (++tried > 16) break;
                var c = new List<int> { i, j };
                while (c.Count < MaxSize)
                {
                    int best = -1;
                    float bestMean = JoinAt;
                    for (int k = 0; k < n; k++)
                    {
                        if (!free[k] || c.Contains(k)) continue;
                        bool against = false;
                        foreach (int m in c)
                            if (aff[k, m] < 0f)
                            {
                                against = true;
                                break;
                            }
                        if (against) continue;
                        float mean = Attachment(aff, k, c);
                        if (mean >= bestMean)
                        {
                            bestMean = mean;
                            best = k;
                        }
                    }
                    if (best < 0) break;
                    c.Add(best);
                }
                if (c.Count < MinSize || Mean(aff, c) < FormAt) continue;
                if (existing != null && existing.Any(g => Overlap(c, g) * 3 >= c.Count * 2)) continue;
                c.Sort();
                return c;
            }
            return null;
        }

        /// <summary>
        /// The group (index into <paramref name="groups"/>) someone outside it would join, or -1: the one they hold to
        /// best, if well enough and not full, and not one closed to them (<paramref name="closed"/>: one they left).
        /// </summary>
        public static int JoinTarget(float[,] aff, int who, List<List<int>> groups, bool[] closed = null)
        {
            int best = -1;
            float bestV = JoinAt;
            for (int g = 0; g < groups.Count; g++)
            {
                var members = groups[g];
                if (members.Contains(who) || members.Count >= MaxSize || (closed != null && g < closed.Length && closed[g])) continue;
                bool against = false;
                foreach (int m in members)
                    if (aff[who, m] < 0f)
                    {
                        against = true;
                        break;
                    }
                if (against) continue;
                float v = Attachment(aff, who, members);
                if (v >= bestV)
                {
                    bestV = v;
                    best = g;
                }
            }
            return best;
        }

        // ---------------- Roles ----------------

        /// <summary>The roles a character can settle into (the newcomer is given, not earned).</summary>
        public static readonly GroupRole[] Roles = { GroupRole.Anchor, GroupRole.Joker, GroupRole.Peacemaker, GroupRole.Firebrand, GroupRole.Worrier, GroupRole.Quiet };

        /// <summary>Only one of these to a group.</summary>
        public static bool Unique(GroupRole r) => r == GroupRole.Anchor || r == GroupRole.Joker || r == GroupRole.Peacemaker;

        /// <summary>A side of a person that shows (the persona tags and words do from here).</summary>
        private const float Shows = 30f;

        /// <summary>
        /// How well a role fits someone (higher is better; <see cref="Threshold"/> to fit at all). A role never goes
        /// against what shows of their character, so the part they play in a group matches how they talk everywhere
        /// else: no cold anchor, no dour joker, no quiet one who is the room's joker.
        /// </summary>
        public static float RoleScore(GroupRole r, RoleFacts x)
        {
            switch (r)
            {
                case GroupRole.Anchor:
                    if (x.warmth <= -Shows) return 0f;
                    return x.warmth * 0.35f + x.pride * 0.2f + (x.social - 6) * 2.5f + Clamp(x.tieShare, 0f, 1f) * 45f;
                case GroupRole.Joker:
                    if (x.humour < Shows) return 0f;
                    return x.humour * 0.9f + x.warmth * 0.1f;
                case GroupRole.Peacemaker:
                    if (x.temper >= Shows || x.warmth <= -Shows) return 0f;
                    return x.warmth * 0.45f - x.temper * 0.45f + Math.Min(x.mended, 6) * 5f;
                case GroupRole.Firebrand:
                    return x.temper * 0.6f + x.pride * 0.35f + x.nerve * 0.1f;
                case GroupRole.Worrier:
                    return x.outlook > -15f ? 0f : -x.outlook * 0.6f + x.warmth * 0.25f - x.nerve * 0.15f;
                case GroupRole.Quiet:
                    if (x.humour >= Shows || x.temper >= Shows) return 0f;
                    return -(x.humour * 0.35f + x.warmth * 0.2f) - (x.social - 6) * 2f - x.pride * 0.15f;
                default:
                    return 0f;
            }
        }

        /// <summary>The score a role needs to fit at all.</summary>
        public static float Threshold(GroupRole r)
        {
            switch (r)
            {
                case GroupRole.Anchor: return 30f;
                case GroupRole.Joker: return 32f;
                case GroupRole.Peacemaker: return 30f;
                case GroupRole.Firebrand: return 32f;
                case GroupRole.Worrier: return 26f;
                case GroupRole.Quiet: return 18f;
                default: return 0f;
            }
        }

        /// <summary>
        /// A day's roles for a whole group. <paramref name="facts"/> per member, <paramref name="current"/> their roles
        /// so far. Newcomers are the newcomer. Everyone else wants the role that fits them best (their own counting
        /// <see cref="RoleMargin"/> extra, so it sticks); one anchor, one joker and one peacemaker to a group, the best
        /// fit taking it. A wanted change waits <see cref="RolePendingDays"/> days in a row before it happens, except
        /// from no role (or from newcomer once that's over), which changes at once.
        /// </summary>
        public static RoleState[] AssignRoles(RoleFacts[] facts, RoleState[] current)
        {
            int n = facts.Length;
            var next = new RoleState[n];
            var want = new GroupRole[n];
            var wantScore = new float[n];
            // Every member's options, best first, counting the stickiness of their own role.
            var options = new List<(GroupRole r, float s)>[n];
            for (int i = 0; i < n; i++)
            {
                options[i] = new List<(GroupRole, float)>();
                if (facts[i].newcomer) continue;
                foreach (var r in Roles)
                {
                    float s = RoleScore(r, facts[i]);
                    bool mine = current[i].role == r;
                    if (s < Threshold(r) - (mine ? RoleMargin : 0f)) continue;
                    options[i].Add((r, s + (mine ? RoleMargin : 0f) - Threshold(r)));
                }
                options[i].Sort((a, b) => b.s.CompareTo(a.s));
            }
            // Unique roles go to whoever wants them most; the rest take their next choice.
            var taken = new HashSet<GroupRole>();
            var order = Enumerable.Range(0, n).OrderByDescending(i => options[i].Count > 0 ? options[i][0].s : float.MinValue).ThenBy(i => i).ToList();
            var settled = new bool[n];
            for (int round = 0; round < Roles.Length + 1; round++)
            {
                foreach (int i in order)
                {
                    if (settled[i]) continue;
                    if (facts[i].newcomer)
                    {
                        want[i] = GroupRole.Newcomer;
                        settled[i] = true;
                        continue;
                    }
                    var pick = options[i].FirstOrDefault(o => !(Unique(o.r) && taken.Contains(o.r)));
                    if (pick.r == GroupRole.None)
                    {
                        want[i] = GroupRole.None;
                        settled[i] = true;
                        continue;
                    }
                    // Someone else wants this unique role more: let them have it first.
                    if (Unique(pick.r) && order.Any(k => k != i && !settled[k] && !facts[k].newcomer
                                                         && options[k].FirstOrDefault(o => !(Unique(o.r) && taken.Contains(o.r))).r == pick.r
                                                         && options[k].First(o => o.r == pick.r).s > pick.s))
                        continue;
                    want[i] = pick.r;
                    wantScore[i] = pick.s;
                    if (Unique(pick.r)) taken.Add(pick.r);
                    settled[i] = true;
                }
            }
            for (int i = 0; i < n; i++)
            {
                var cur = current[i];
                if (want[i] == cur.role)
                {
                    next[i] = new RoleState { role = cur.role };
                    continue;
                }
                if (want[i] == GroupRole.Newcomer || cur.role == GroupRole.None || cur.role == GroupRole.Newcomer)
                {
                    next[i] = new RoleState { role = want[i] };
                    continue;
                }
                int days = cur.pending == want[i] ? cur.pendingDays + 1 : 1;
                next[i] = days >= RolePendingDays ? new RoleState { role = want[i] } : new RoleState { role = cur.role, pending = want[i], pendingDays = days };
            }
            // Never two in one unique role: whoever fits it less keeps waiting in their old one... or none.
            foreach (var r in Roles.Where(Unique))
            {
                var holders = Enumerable.Range(0, n).Where(i => next[i].role == r).ToList();
                if (holders.Count < 2) continue;
                int keep = holders.OrderByDescending(i => RoleScore(r, facts[i]) + (current[i].role == r ? RoleMargin : 0f)).First();
                foreach (int i in holders)
                    if (i != keep)
                        next[i] = new RoleState { role = want[i] == r ? GroupRole.None : want[i] };
            }
            return next;
        }

        // ---------------- Cohesion ----------------

        /// <summary>How close a new group starts, from how much its people pull together.</summary>
        public static float StartCohesion(float meanAffinity) => Clamp(35f + meanAffinity * 40f, 30f, 70f);

        /// <summary>
        /// A group's cohesion after a day: it settles slowly towards what the members' attachment holds up, and moves
        /// with what happened. Habits kept together and things gone through together raise it; quarrels inside it, a
        /// member lost and long separation lower it; quarrels mended win a little back. 0-100.
        /// </summary>
        public static float NextCohesion(float c, CohesionDay d)
        {
            float target = Clamp(25f + d.attachment * 90f, 10f, 90f);
            c += (target - c) * 0.06f;
            c += Math.Min(d.habits, 2) * 2f;
            c += Math.Min(d.memories, 4) * 1f;
            c -= Math.Min(d.quarrels, 4) * 3f;
            c += Math.Min(d.mends, 3) * 1.5f;
            c -= d.died * 12f + d.left * 5f;
            if (d.apart && d.apartDays >= 3) c -= 1.5f;
            return Clamp(c, 0f, 100f);
        }

        // ---------------- Splits and merges ----------------

        /// <summary>
        /// A group splits when it has stayed loose (<see cref="SplitBelow"/>) for <see cref="SplitDays"/> days, has four or
        /// more people, and two of them pull against each other (a rift: <paramref name="worstPair"/> below zero).
        /// </summary>
        public static bool ShouldSplit(float cohesion, int lowDays, int size, float worstPair) =>
            size >= 4 && cohesion < SplitBelow && lowDays >= SplitDays && worstPair < -0.05f;

        /// <summary>
        /// Where a group splits: the two who pull apart most go to different sides, and everyone else goes with the side
        /// they hold to better (the first side on a tie).
        /// </summary>
        public static void Cut(float[,] aff, IList<int> members, out List<int> a, out List<int> b)
        {
            a = new List<int>();
            b = new List<int>();
            Worst(aff, members, out int x, out int y);
            if (x < 0)
            {
                a.AddRange(members);
                return;
            }
            a.Add(x);
            b.Add(y);
            // The ones most decided first, so each side forms around its own.
            var rest = members.Where(m => m != x && m != y).OrderByDescending(m => Math.Abs(aff[m, x] - aff[m, y])).ToList();
            foreach (int m in rest)
            {
                if (Attachment(aff, m, b) > Attachment(aff, m, a)) b.Add(m);
                else a.Add(m);
            }
        }

        /// <summary>
        /// Two groups merge when most of the smaller is in the larger already (half of it or more), or when they pull
        /// together well (<paramref name="cross"/>, their mean affinity across), both hold together, and the two would
        /// still be a small group. Never two rivals, and never past <see cref="MergeMax"/>.
        /// </summary>
        public static bool ShouldMerge(IList<int> a, IList<int> b, float cross, float cohesionA, float cohesionB, bool rivals)
        {
            if (rivals) return false;
            int union = a.Union(b).Count();
            if (union > MergeMax) return false;
            int small = Math.Min(a.Count, b.Count);
            if (small > 0 && Overlap(a, b) * 2 >= small) return true;
            return cross >= FormAt && cohesionA >= 45f && cohesionB >= 45f && union <= MaxSize + 1;
        }

        // ---------------- Newcomers ----------------

        /// <summary>
        /// How open a group is to someone new (about 0-1): trusting and warm members open it, its ways (norms) open or
        /// close it, and a close group is a little more sure of itself. Under <see cref="CloseRanksBelow"/> it closes ranks.
        /// </summary>
        public static float Welcome(float meanTrust, float meanWarmth, float normWelcome, float cohesion) =>
            0.35f + meanTrust * 0.004f + meanWarmth * 0.003f + normWelcome + (cohesion - 50f) / 400f;

        public static bool ClosesRanks(float welcome) => welcome < CloseRanksBelow;

        /// <summary>
        /// A newcomer's way in after a day (0-100): what the members think of them, what they went through together, work
        /// shared, someone speaking for them and welcomes all help, more in an open group and much less in a closed one.
        /// </summary>
        public static float NextIntegration(float progress, IntegrationDay d, float welcome)
        {
            float gain = 3f + Clamp(d.opinion, -50f, 60f) / 10f + Math.Min(d.memories, 3) * 3f + Clamp(d.sharedWork, 0f, 1f) * 3f
                         + (d.sponsor ? 4f : 0f) + Math.Min(d.welcomes, 3) * 4f;
            float f = gain < 0f ? 1f : Clamp(welcome * 2f, 0.25f, 1.5f);
            return Clamp(progress + gain * f, 0f, Accepted);
        }

        // ---------------- Between groups ----------------

        /// <summary>
        /// A rivalry between two groups after a day (0-100): it settles towards what lies between their people (rival bonds
        /// across, views and ways at odds) and grows with each friction between them.
        /// </summary>
        public static float NextRivalry(float r, int rivalPairs, float rivalStrength, float viewClash, float normClash, int frictions)
        {
            float target = Clamp(rivalPairs * 12f + rivalStrength * 0.4f + Clamp(viewClash, 0f, 1f) * 30f + Clamp(normClash, 0f, 1f) * 20f, 0f, 100f);
            r += (target - r) * 0.08f + Math.Min(frictions, 3) * 2f;
            return Clamp(r, 0f, 100f);
        }

        // ---------------- Ways and views ----------------

        /// <summary>
        /// Whether a group's way (a norm on one side of a character, say merry: humour at least 25 on average) holds today:
        /// it takes the line to start, and lapses only once it has fallen a margin below it, so a norm doesn't flicker.
        /// </summary>
        public static bool NormHolds(bool held, float value, float line, bool above)
        {
            const float margin = 6f;
            if (above) return held ? value >= line - margin : value >= line;
            return held ? value <= line + margin : value <= line;
        }

        /// <summary>
        /// A view the group shares on a topic, from the stances of those members who have one: +1 or -1 when enough of them
        /// have one (two, and most of the group), nearly all lean the same way and they lean clearly; 0 otherwise.
        /// </summary>
        public static int SharedView(IList<float> stances, int members)
        {
            if (stances == null || stances.Count < Math.Max(2, (int)Math.Ceiling(members * 0.6))) return 0;
            float sum = 0f;
            int pos = 0, neg = 0;
            foreach (var s in stances)
            {
                sum += s;
                if (s > 0.1f) pos++;
                else if (s < -0.1f) neg++;
            }
            float avg = sum / stances.Count;
            if (Math.Abs(avg) < 0.3f) return 0;
            int same = avg > 0 ? pos : neg;
            return same * 4 >= stances.Count * 3 ? Math.Sign(avg) : 0;
        }

        /// <summary>A member pulled a little towards the group's view (conformity): a small step of the way, never past it.</summary>
        public static float Conform(float value, float toward, float step)
        {
            if (step <= 0f) return value;
            float d = toward - value;
            if (Math.Abs(d) <= step) return toward;
            return value + Math.Sign(d) * step;
        }
    }
}
