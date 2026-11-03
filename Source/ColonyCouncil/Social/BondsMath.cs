using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How bonds develop between colonists (Social/Bonds.cs, Social/SocialWeb.cs): the arithmetic, free of game types so
    /// Tools/Tests checks it and Tools/Balance plays two years of a colony with it (--social). Nothing here aims at a
    /// number of friends or rivals: each rule says what one thing that happened does between two people.
    /// <list type="bullet">
    /// <item>Knowing each other (0 to 100): time actually spent side by side (work, meals, recreation), talks and what
    /// they went through together. New colonists start as strangers; two who stop seeing each other drift apart.</item>
    /// <item>How two get on (the affinity): the game's compatibility, with faith, views, shared passions and temper.
    /// It sets how far words can carry them and how close a friendship can get.</item>
    /// <item>A friendship needs both good feeling and knowing each other.</item>
    /// <item>Comrades: only those who fought near each other, more when one pulled the other out; every fight together
    /// counts in full; it fades in long quiet spells, down to what a long history of fighting together leaves.</item>
    /// <item>Grudges: from things that happened, each with its cause; they hold a few days, then fade slowly unless renewed.</item>
    /// <item>A council seat taken by someone else, felt toward the winner as much as the seat was wanted.</item>
    /// </list>
    /// </summary>
    public static class BondsMath
    {
        // ---------------- How two get on ----------------

        /// <summary>Faith: the same faith draws two together a little; different faiths more so apart (Ideology). 1 same, -1 different, 0 not known.</summary>
        public static float FaithPart(int faith) => faith > 0 ? 0.2f : faith < 0 ? -0.3f : 0f;

        /// <summary>Views: the topics they agree on against those they're at odds over (Persona's stances).</summary>
        public static float ViewsPart(int agree, int odds) => Clamp(0.1f * (agree - odds), -0.4f, 0.3f);

        /// <summary>Interests: passions they share (both burning for the same skill).</summary>
        public static float InterestsPart(int shared) => Math.Min(0.3f, 0.12f * Math.Max(0, shared));

        /// <summary>Temper: a kind person gets on with most people; an abrasive one rubs most the wrong way.</summary>
        public static float TemperPart(int kind, int abrasive) => 0.35f * kind - 0.35f * abrasive;

        /// <summary>How two get on: the game's compatibility (most pairs between -1 and 2) with faith, views, interests and temper.</summary>
        public static float Affinity(float game, int faith, int agree, int odds, int sharedPassions, int kind, int abrasive) =>
            game + FaithPart(faith) + ViewsPart(agree, odds) + InterestsPart(sharedPassions) + TemperPart(kind, abrasive);

        // ---------------- Knowing each other ----------------

        public const float KnowMax = 100f;

        /// <summary>The bands: strangers, acquaintances, know each other, know each other well.</summary>
        public const float AcquaintedAt = 12f, KnowAt = 30f, WellAt = 60f;

        public const int Strangers = 0, Acquainted = 1, Known = 2, WellKnown = 3;

        /// <summary>Family by blood and partners know each other at least this well.</summary>
        public const float KinKnow = 60f;

        public static int KnowStage(float fam) => fam >= WellAt ? WellKnown : fam >= KnowAt ? Known : fam >= AcquaintedAt ? Acquainted : Strangers;

        /// <summary>An hour side by side: working near each other, and more at a meal or recreation together.</summary>
        public const float HourWorking = 0.25f, HourSharing = 0.4f;

        /// <summary>Two who get on easily come to know each other faster; two who don't click, slower.</summary>
        public static float KnowPace(float affinity) => Curve3(affinity, -1.5f, 0.75f, 0.5f, 1f, 2f, 1.3f);

        /// <summary>The first weeks count most: the better they know each other, the less an hour adds.</summary>
        public static float Room(float fam) => Clamp(1f - fam / 115f, 0f, 1f);

        /// <summary>
        /// One hour side by side (<paramref name="sharing"/>: a meal or recreation together), as familiarity gained. Half
        /// while things are awkward between them (after a rejection or a breakup).
        /// </summary>
        public static float HourGain(float fam, bool sharing, float affinity, bool awkward) =>
            (sharing ? HourSharing : HourWorking) * KnowPace(affinity) * Room(fam) * (awkward ? 0.5f : 1f);

        /// <summary>A warm word between them, for each side: a deep talk (+15) is about one point, a chitchat next to nothing.</summary>
        public static float TalkGain(float opinionOffset, float fam) => opinionOffset <= 0f ? 0f : Math.Min(2f, 0.08f * opinionOffset) * Math.Max(0.3f, Room(fam));

        /// <summary>Something they went through together (a shared memory), by its weight.</summary>
        public static float SharedGain(float weight, float fam) => 1.5f * Math.Max(0f, weight) * Math.Max(0.3f, Room(fam));

        /// <summary>A fight fought near each other.</summary>
        public const float BattleKnow = 3f;

        /// <summary>Hours side by side lately: each day keeps this share of the last (about a week's worth).</summary>
        public const float RecentKeep = 0.85f;

        /// <summary>Under this many recent hours they're apart, and slowly drift apart.</summary>
        public const float ApartBelow = 1.5f;

        public const float DriftPerDay = 0.35f;

        /// <summary>A day's change to familiarity from their recent hours: nothing while they still see each other.</summary>
        public static float DayDrift(float fam, float recentHours) => recentHours < ApartBelow && fam > 0f ? -Math.Min(fam, DriftPerDay) : 0f;

        /// <summary>
        /// Familiarity for two who were already together when this started (an existing save): from the days they have both
        /// been with the colony, and a little more for a friendship already there.
        /// </summary>
        public static float Seed(int daysTogether, float friendStrength) =>
            Math.Min(85f, Math.Min(70f, 0.7f * Math.Max(0, daysTogether)) + 0.25f * Math.Max(0f, friendStrength));

        // ---------------- Friendship ----------------

        /// <summary>Compatibility bands, as in ChemistryMath: don't click, rub each other the wrong way; easy company.</summary>
        public const float PoorAt = -0.6f, WorseAt = -1.2f, GoodAt = 1.5f;

        /// <summary>
        /// How close a friendship can get between two who get on like this: none between two who rub each other the wrong
        /// way (friendly colleagues at best), new friends at most between two who don't click, good friends but never close
        /// with a slight mismatch, any depth otherwise.
        /// </summary>
        public static float FriendCapByChemistry(float affinity) =>
            affinity <= WorseAt ? 0f : affinity <= PoorAt ? 35f : affinity < 0f ? 60f : 100f;

        /// <summary>A friendship never runs ahead of how well they know each other.</summary>
        public static float FriendCapByKnowing(float fam) => Math.Max(0f, fam) * 1.15f;

        public static float FriendCap(float affinity, float fam) => Math.Min(FriendCapByChemistry(affinity), FriendCapByKnowing(fam));

        /// <summary>The mutual opinion a friendship forms at: a little lower for two who get on easily.</summary>
        public static float FormOpinion(float affinity) => affinity >= GoodAt ? 38f : 45f;

        /// <summary>How well they must know each other first: a little less for two who get on easily.</summary>
        public static float FormKnowing(float affinity) => affinity >= GoodAt ? 22f : KnowAt;

        /// <summary>A friendship can form: both like each other enough, know each other enough, and there is room for one.</summary>
        public static bool FriendsForm(int lowOpinion, float affinity, float fam) =>
            lowOpinion >= FormOpinion(affinity) && fam >= FormKnowing(affinity) && FriendCap(affinity, fam) >= AnnouncedAt;

        /// <summary>Where a friendship drifts to: how they feel about each other, no further than the cap.</summary>
        public static float FriendTarget(int lowOpinion, float affinity, float fam) => Math.Min(lowOpinion - 25f, FriendCap(affinity, fam));

        /// <summary>A tie is a real bond (and told) from this strength.</summary>
        public const float AnnouncedAt = 20f;

        // ---------------- Words between them ----------------

        /// <summary>
        /// The opinion past which warm words stop carrying two who don't click any further: friendly colleagues, never close
        /// friends however many talks. No limit for an ordinary pair.
        /// </summary>
        public static float WordsCap(float affinity) => affinity <= WorseAt ? 10f : affinity <= PoorAt ? 25f : affinity < 0f ? 50f : float.MaxValue;

        /// <summary>A warm word's weight as the holder's opinion nears the cap: in full well below it, a tenth at it and beyond.</summary>
        public static float CapFactor(float opinion, float cap)
        {
            if (cap >= float.MaxValue / 2f || opinion <= cap - 15f) return 1f;
            if (opinion >= cap) return 0.1f;
            return 1f - 0.9f * (opinion - (cap - 15f)) / 15f;
        }

        /// <summary>Between two who barely know each other a warm word counts for less (half between strangers).</summary>
        public static float KnowFactor(float fam) => 0.5f + 0.5f * Clamp(fam / KnowAt, 0f, 1f);

        // ---------------- Comrades ----------------

        /// <summary>
        /// One fight, for two who fought in it: <paramref name="near"/> of <paramref name="samples"/> looks saw them side by
        /// side; <paramref name="deeds"/> is what one did for the other (<see cref="DeedRescued"/> and the others). Nothing
        /// for two who fought apart. Every fight counts in full, however many came before.
        /// </summary>
        public static float BattleBond(int near, int samples, int deeds)
        {
            if (samples <= 0 || near < NearNeeded(samples)) return 0f;
            float share = Clamp(near / (float)samples, 0f, 1f);
            return 8f + 7f * share + DeedBond(deeds);
        }

        /// <summary>Looks side by side needed to count as having fought together: two once the fight was looked at four times.</summary>
        public static int NearNeeded(int samples) => samples >= 4 ? 2 : 1;

        /// <summary>What one did for the other under fire: carried them out, tended them, stood over them while they were down.</summary>
        public const int DeedRescued = 1, DeedTended = 2, DeedGuarded = 4;

        public static float DeedBond(int deeds) =>
            Math.Min(20f, ((deeds & DeedRescued) != 0 ? 12f : 0f) + ((deeds & DeedTended) != 0 ? 8f : 0f) + ((deeds & DeedGuarded) != 0 ? 6f : 0f));

        /// <summary>A rivalry eases between two who fought side by side, more when one saved the other.</summary>
        public static float BattleMend(int deeds) => 3f + ((deeds & (DeedRescued | DeedTended)) != 0 ? 10f : 0f);

        /// <summary>Quiet days after a fight together before comradeship starts to fade, and how fast it fades then.</summary>
        public const int QuietDays = 10;

        public const float ComradeFadePerDay = 0.6f;

        /// <summary>What a long history of fighting together leaves: a few points after one fight, about 30 after ten, near 45 after twenty.</summary>
        public static float ComradeFloor(int battles) => battles <= 0 ? 0f : 50f * (1f - (float)Math.Exp(-battles / 10f));

        /// <summary>A day's fading: nothing in the first quiet days, then down to what their history leaves.</summary>
        public static float ComradeDay(float strength, int daysSinceBattle, int battles)
        {
            if (daysSinceBattle <= QuietDays) return strength;
            float floor = ComradeFloor(battles);
            return strength <= floor ? strength : Math.Max(floor, strength - ComradeFadePerDay);
        }

        /// <summary>
        /// Battles together for a comradeship from before this was kept (an existing save): what the old rule's strength
        /// shows at the least (it gave 25 a battle).
        /// </summary>
        public static int SeedBattles(float strength) => Math.Max(1, (int)Math.Round(strength / 25f));

        // ---------------- Grudges ----------------

        /// <summary>Days a grudge holds after it was last renewed, then how fast it fades.</summary>
        public const int GrudgeHoldDays = 6;

        public const float GrudgeFadePerDay = 0.3f;

        /// <summary>
        /// How a temper takes a grudge, for its growth and its fading: a kind soul lets go sooner and takes less to heart;
        /// an abrasive, volatile or jealous one holds on. <paramref name="kind"/> and <paramref name="hot"/> count the two.
        /// </summary>
        public static float GrudgeGrowth(int kind, int hot) => (float)Math.Pow(0.75, kind) * (float)Math.Pow(1.15, hot);

        public static float GrudgeFade(int kind, int hot) => GrudgeFadePerDay * (float)Math.Pow(1.3, kind) * (float)Math.Pow(0.8, hot);

        /// <summary>A day of a grudge: held for a while after it was renewed, then fading.</summary>
        public static float GrudgeDay(float strength, int daysSinceRenewed, int kind, int hot) =>
            daysSinceRenewed <= GrudgeHoldDays ? strength : Math.Max(0f, strength - GrudgeFade(kind, hot));

        // What happened, as a grudge's growth (before temper and how the two get on).
        public const float Insult = 3f, Slight = 1.5f, CameToBlows = 8f, HitInFight = 5f, AttackedInRage = 4f, BotchedSurgery = 6f;
        public const float CheatedOn = 15f, TookUpWith = 12f, WonTheirHeart = 10f, JealousFormed = 5f, JealousClose = 8f, Gloated = 8f;

        /// <summary>A day of envy for a much better room or a title (the jealous, the greedy), while it lasts.</summary>
        public const float EnvyPerDay = 0.5f;

        /// <summary>A bedroom this much more impressive than one's own is envied.</summary>
        public const float EnvyRoomGap = 40f;

        /// <summary>The same harm within this many hours is one incident (a social fight's blows, a burst of fire).</summary>
        public const int IncidentHours = 6;

        // ---------------- A council seat ----------------

        /// <summary>
        /// How much someone wanted a seat they lost: standing for it at all, holding it before, a life goal of a seat on
        /// the council, a vain or jealous nature, a campaign of their own. 0.5 to 2.
        /// </summary>
        public static float SeatWant(bool incumbent, bool aspires, bool vain, bool campaigned) =>
            Math.Min(2f, 0.5f + (incumbent ? 0.5f : 0f) + (aspires ? 0.5f : 0f) + (vain ? 0.5f : 0f) + (campaigned ? 0.25f : 0f));

        /// <summary>How it shows toward the winner: minded a little, felt, bitter.</summary>
        public const int SeatMild = 0, SeatFelt = 1, SeatBitter = 2;

        public static int SeatStage(float want) => want < 0.9f ? SeatMild : want < 1.4f ? SeatFelt : SeatBitter;

        /// <summary>
        /// The grudge toward the winner: twice as much for a seat held before, by how much it was wanted, and more for each
        /// time they lost to the same person (<paramref name="losses"/> counts this one).
        /// </summary>
        public static float SeatGrudge(float want, int losses, bool incumbent) =>
            (incumbent ? 12f : 6f) * want * (1f + 0.75f * Math.Max(0, losses - 1));

        // ---------------- Helpers ----------------

        public static float Clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;

        /// <summary>Straight lines between three points, flat beyond the ends.</summary>
        private static float Curve3(float x, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            if (x <= x0) return y0;
            if (x <= x1) return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
            if (x <= x2) return y1 + (y2 - y1) * (x - x1) / (x2 - x1);
            return y2;
        }
    }
}
