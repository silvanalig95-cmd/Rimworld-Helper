using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What the town owes the colony once handed over (the player's decision 9A): nothing, or a pact for a year, renewed by letter.</summary>
    public enum HandOverTerms
    {
        Free,
        Defence,
        Trade
    }

    /// <summary>
    /// Where a hand-over stands: the meeting called to vote, the vote told and the player's word awaited, the day set,
    /// the ceremony on the map, then those who go leaving (the town is made once the last of them has left).
    /// </summary>
    public enum HandOverStage
    {
        Meeting,
        Voted,
        Set,
        Ceremony,
        Leaving
    }

    /// <summary>Where those who go go: to another home (the journey planner), with a founding party (a fresh start in the same act), or a caravan the player forms.</summary>
    public enum HandOverWhere
    {
        Home,
        Founding,
        Caravan
    }

    /// <summary>The colony's prisoners and slaves at the hand-over: set free there, or taken along by those who go.</summary>
    public enum CaptiveFate
    {
        Free,
        Go
    }

    /// <summary>One member's vote at the meeting before a hand-over (the player's decision 2A).</summary>
    public enum HandOverVote
    {
        For,
        Against,
        Torn
    }

    /// <summary>
    /// Someone who might head the town, or stand second to its head: the speaker, an office by its order (lower first,
    /// -1 for none), how the others think of them (the colony's view), and whether they were one of the colony (for a
    /// town's next second).
    /// </summary>
    public struct HeadCandidate
    {
        public bool speaker;
        public int office;
        public float standing;
        public bool former;

        public HeadCandidate(bool speaker, int office, float standing, bool former = true)
        {
            this.speaker = speaker;
            this.office = office;
            this.standing = standing;
            this.former = former;
        }
    }

    /// <summary>
    /// The rules of handing a colony over (docs/design/stage3-many-colonies.md, section 2; release 2a), with no game
    /// types so Tools/Tests can check them: what keeps it from going ahead, the head and the head's second by office or
    /// standing (never by blood), each member's vote at the meeting and what going against it costs, the people's name,
    /// the new people's relations with every other people, the town's prosperity seeded from the colony's wealth, the
    /// bond in goodwill by its state, the people's def by the colony's age, when the town can be made, and the bound on
    /// the towns the colony was.
    /// </summary>
    public static class HandOverMath
    {
        public const int DayTicks = 60000;

        // ---------------- The checks ----------------

        /// <summary>At least this many grown-ups stay: fewer is a homestead, not a town.</summary>
        public const int MinGrownStay = 3;

        /// <summary>
        /// What keeps a hand-over from being set going, as the key of its words (CC_HandOver_No_*), or null when it can:
        /// the home isn't a settlement (a home in orbit or a camp), a fight or a threat at that home, a banishment under way
        /// there, fewer than three grown-ups staying, nobody of the player's left anywhere else (the game would end), or a
        /// grav anchor that would stay (the town can't keep it, and the game would keep the map a home).
        /// </summary>
        public static string WhyNot(bool settlement, bool fight, bool banishment, int grownStaying, bool playersElsewhere, bool anchor)
        {
            if (!settlement) return "NotSettlement";
            if (fight) return "Fight";
            if (banishment) return "Banishment";
            if (grownStaying < MinGrownStay) return "FewStay";
            if (!playersElsewhere) return "NobodyElse";
            if (anchor) return "Anchor";
            return null;
        }

        /// <summary>
        /// Someone of the player's stays the player's when the home is handed over: one who goes, a grown colonist at
        /// another home, in a caravan, in pods or on the gravship, or newcomers coming to a new colony in the same act.
        /// </summary>
        public static bool PlayersElsewhere(int grownGoing, int colonistsElsewhere, bool newcomers) => grownGoing > 0 || colonistsElsewhere > 0 || newcomers;

        /// <summary>
        /// What the town still waits for once the ceremony is over, as the key of its words (CC_HandOver_Wait_*), or null
        /// when it can be made: those who go still on the map, a guest of the colony's still there (they go with those who
        /// go), the new colony of the same act not yet standing when nobody goes from here, fewer than three grown-ups left
        /// on the map, the gravship's engine still there (the town can't keep it), or nobody of the player's left anywhere
        /// else (those who went all lost: the game would end).
        /// </summary>
        public static string WaitsFor(int goingOnMap, int lodgersOnMap, bool foundingToStand, int grownOnMap, bool engineOnMap, bool playersElsewhere = true)
        {
            if (goingOnMap > 0) return "Going";
            if (lodgersOnMap > 0) return "Lodgers";
            if (foundingToStand) return "Founding";
            if (grownOnMap < MinGrownStay) return "FewStay";
            if (engineOnMap) return "Engine";
            if (!playersElsewhere) return "NobodyElse";
            return null;
        }

        // ---------------- The head and the head's second ----------------

        /// <summary>
        /// The town's head among those who stay: the one the player picked, else the speaker, else the office holder first
        /// by the seats' order, else the one the others think best of. -1 when nobody stays.
        /// </summary>
        public static int Head(IReadOnlyList<HeadCandidate> c, int picked = -1)
        {
            if (c == null || c.Count == 0) return -1;
            if (picked >= 0 && picked < c.Count) return picked;
            for (int i = 0; i < c.Count; i++)
                if (c[i].speaker)
                    return i;
            int best = ByOffice(c, -1);
            return best >= 0 ? best : ByStanding(c, -1, false);
        }

        /// <summary>
        /// The head's second, the next head by office or standing, never by blood: the one the player picked (not the
        /// head), else the next office holder by the same order, else the one the others think best of after the head.
        /// -1 when there is nobody but the head.
        /// </summary>
        public static int Second(IReadOnlyList<HeadCandidate> c, int head, int picked = -1)
        {
            if (c == null || c.Count == 0) return -1;
            if (picked >= 0 && picked < c.Count && picked != head) return picked;
            int best = ByOffice(c, head);
            return best >= 0 ? best : ByStanding(c, head, false);
        }

        /// <summary>
        /// A town's new second once the old one leads: one of the colony's former people first (the one the others thought
        /// best of), else the best of the town's other people. -1 when there is nobody but the head.
        /// </summary>
        public static int NextSecond(IReadOnlyList<HeadCandidate> c, int head)
        {
            if (c == null || c.Count == 0) return -1;
            int best = ByStanding(c, head, true);
            return best >= 0 ? best : ByStanding(c, head, false);
        }

        private static int ByOffice(IReadOnlyList<HeadCandidate> c, int except)
        {
            int best = -1;
            for (int i = 0; i < c.Count; i++)
            {
                if (i == except || c[i].office < 0) continue;
                if (best < 0 || c[i].office < c[best].office || c[i].office == c[best].office && c[i].standing > c[best].standing) best = i;
            }
            return best;
        }

        private static int ByStanding(IReadOnlyList<HeadCandidate> c, int except, bool formerOnly)
        {
            int best = -1;
            for (int i = 0; i < c.Count; i++)
            {
                if (i == except || formerOnly && !c[i].former) continue;
                if (best < 0 || c[i].standing > c[best].standing) best = i;
            }
            return best;
        }

        /// <summary>The best of the rest by a score (the most social for the envoy, the best trader for the trader), or -1: never one already taken.</summary>
        public static int Best(IReadOnlyList<float> scores, ICollection<int> taken)
        {
            int best = -1;
            if (scores == null) return best;
            for (int i = 0; i < scores.Count; i++)
            {
                if (taken != null && taken.Contains(i)) continue;
                if (best < 0 || scores[i] > scores[best]) best = i;
            }
            return best;
        }

        // ---------------- The meeting's vote (decision 2A) ----------------

        /// <summary>Trust in the council worth one whole reason either way at the vote.</summary>
        public const float VoteTrustScale = 40f;

        /// <summary>The most the council's regard can weigh at the vote (either way), in reasons.</summary>
        public const float VoteTrustMost = 1.5f;

        /// <summary>Past this either way a member votes for or against; between, they are torn and don't count.</summary>
        public const float VoteLine = 0.25f;

        /// <summary>
        /// A member's pull toward the hand-over: their own wish (one reason for it when the plan gives them their wish, one
        /// against when it goes against it, none when torn) and their regard for the player's council (trust, a reason
        /// for each <see cref="VoteTrustScale"/>, at most <see cref="VoteTrustMost"/>).
        /// </summary>
        public static float VotePull(FoundingWish wish, bool going, float trust)
        {
            float own = wish == FoundingWish.Torn ? 0f : FoundingMath.AgainstWish(wish, going) ? -1f : 1f;
            float regard = Math.Max(-VoteTrustMost, Math.Min(VoteTrustMost, trust / VoteTrustScale));
            return own + regard;
        }

        public static HandOverVote VoteOf(FoundingWish wish, bool going, float trust)
        {
            float p = VotePull(wish, going, trust);
            return p > VoteLine ? HandOverVote.For : p < -VoteLine ? HandOverVote.Against : HandOverVote.Torn;
        }

        /// <summary>The meeting is for it: more for than against (the torn don't count; nobody either way is not a yes).</summary>
        public static bool Carried(int forVotes, int against) => forVotes > against;

        /// <summary>Going against the meeting's vote costs this much trust among those who stay (it is what they think of the council as it leaves them).</summary>
        public const float TrustAgainstVote = -6f;

        /// <summary>The trust a go-ahead costs each of those who stay: only against the meeting's vote.</summary>
        public static float TrustCost(bool carried, bool goAhead) => goAhead && !carried ? TrustAgainstVote : 0f;

        // ---------------- The people ----------------

        /// <summary>The longest name the window takes for the people.</summary>
        public const int MaxNameLength = 40;

        /// <summary>Why a name for the people won't do, as the key of its words (CC_HandOver_Name_*), or null: empty, too long, or another people's already.</summary>
        public static string NameWhyNot(string name, IEnumerable<string> taken)
        {
            string n = name?.Trim() ?? "";
            if (n.Length == 0) return "Empty";
            if (n.Length > MaxNameLength) return "Long";
            if (taken != null)
                foreach (var t in taken)
                    if (!string.IsNullOrEmpty(t) && string.Equals(t.Trim(), n, StringComparison.OrdinalIgnoreCase))
                        return "Taken";
            return null;
        }

        /// <summary>The people's own kind by the colony's age (decision 6B): Rimfolk's own four, tribal, medieval, industrial, spacer.</summary>
        public static string DefName(WorldAge age) => "Rimfolk_People_" + age;

        /// <summary>The key of the people's default name by the age's word ("the folk of Oakhold").</summary>
        public static string NameKey(WorldAge age) => "CC_HandOver_PeopleName_" + age;

        /// <summary>The goodwill the new people start with toward the colony: allies.</summary>
        public const int AllyGoodwill = 80;

        /// <summary>A def's own rules at or below this (a permanent or natural enemy, the game's -100 and -80) stand: such a people stays an enemy.</summary>
        public const int EnemyLine = -80;

        /// <summary>
        /// The new people's relation with another people: half the colony's goodwill with them, at war with them only
        /// while the colony is and the half is below nothing; or what the defs' own rules gave (<paramref name="initial"/>,
        /// a permanent or natural enemy) when that is at or below <see cref="EnemyLine"/>.
        /// </summary>
        public static int OtherGoodwill(int initial, int playerGoodwill, bool playerHostile, out bool hostile)
        {
            if (initial <= EnemyLine)
            {
                hostile = true;
                return initial;
            }
            int g = (int)Math.Round(playerGoodwill / 2f, MidpointRounding.AwayFromZero);
            g = Math.Max(-100, Math.Min(100, g));
            hostile = playerHostile && g < 0;
            return g;
        }

        // ---------------- The town ----------------

        /// <summary>The town's prosperity is seeded between these, by the colony's wealth per head.</summary>
        public const float SeedLow = 30f, SeedHigh = 70f;

        /// <summary>Wealth per head at or below which the town starts poorest, and at or above which richest.</summary>
        public const float WealthLow = 2000f, WealthHigh = 40000f;

        /// <summary>The town's prosperity at the hand-over: 30 to 70 by the colony's wealth per head (everyone who stays and goes).</summary>
        public static float Seed(float wealth, int people)
        {
            float each = Math.Max(0f, wealth) / Math.Max(1, people);
            float t = (each - WealthLow) / (WealthHigh - WealthLow);
            t = Math.Max(0f, Math.Min(1f, t));
            return (float)Math.Round(SeedLow + (SeedHigh - SeedLow) * t, 1);
        }

        // ---------------- The bond in goodwill (decision 8A) ----------------

        /// <summary>The bond's natural goodwill while one of the colony's former people heads the town, and after.</summary>
        public const int BondHeld = 30, BondAfter = 15;

        /// <summary>The bond by its state: gone after a war or a taking by force, else +30 while a former colonist heads them, +15 after.</summary>
        public static int Bond(bool ended, bool formerHeads) => ended ? 0 : formerHeads ? BondHeld : BondAfter;

        /// <summary>The bond ends with a war between them (the people hostile to the colony) or a town taken by force (release 2b).</summary>
        public static bool BondEnds(bool hostile, bool takenByForce) => hostile || takenByForce;

        // ---------------- The meeting and the ceremony on the map ----------------

        /// <summary>The meeting gathers in the evening, as the council's meetings do; the ceremony in the day set's working hours.</summary>
        public const int MeetingFrom = 17, MeetingTo = 22, CeremonyFrom = 9, CeremonyTo = 19;

        /// <summary>How long each lasts, in hours, and how long either may wait past its hours for danger to pass.</summary>
        public const float MeetingHours = 1.5f, CeremonyHours = 2f, WaitHours = 30f;

        /// <summary>The ceremony is set for the day after the player's word: its first tick, from the tick and the hour it is now (local).</summary>
        public static int DaySet(int now, int hourNow) => now - Math.Max(0, Math.Min(23, hourNow)) * (DayTicks / 24) + DayTicks;

        /// <summary>Farewells said at the end of the ceremony: each who goes to their closest who stay, at most this many each, and in all.</summary>
        public const int FarewellsEach = 2, MaxFarewells = 12;

        // ---------------- The towns the colony was ----------------

        /// <summary>The most towns the colony was that the ledger keeps a record of (the game allows five homes at once).</summary>
        public const int MaxTowns = 6;

        /// <summary>
        /// Which record goes when there are too many: the oldest handed over whose town is gone, or -1 when there's room or
        /// every town still stands.
        /// </summary>
        public static int RecordToDrop(IReadOnlyList<bool> standing, IReadOnlyList<int> handedAt)
        {
            if (standing == null || handedAt == null || standing.Count <= MaxTowns) return -1;
            int best = -1;
            for (int i = 0; i < standing.Count && i < handedAt.Count; i++)
                if (!standing[i] && (best < 0 || handedAt[i] < handedAt[best]))
                    best = i;
            return best;
        }

        // release 2b: the kept layout's encoding and size cap (LayoutMath), the take-back answer, the homecoming's chance
        // and caps (FormerMath) come here, beside these.
    }
}
