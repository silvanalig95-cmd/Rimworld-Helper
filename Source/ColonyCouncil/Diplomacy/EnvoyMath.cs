using System;

namespace ColonyCouncil
{
    /// <summary>Where their envoy slept a night of the stay.</summary>
    public enum EnvoyNight
    {
        /// <summary>The bed the colony set aside for them (for the Empire's, alone in its room).</summary>
        Own,
        /// <summary>A spare bed of the colony's (for the Empire's, the bed set aside in a shared room).</summary>
        Spare,
        /// <summary>On the floor of a room, or out of doors.</summary>
        Floor
    }

    /// <summary>How their envoy's stay is going, in a word: content, cool, offended.</summary>
    public enum EnvoyMood
    {
        Content,
        Cool,
        Offended
    }

    /// <summary>Where an envoy's season stands, step by step (never a time left).</summary>
    public enum EnvoyStage
    {
        /// <summary>Agreed: theirs on the road to the colony, or ours about to set out.</summary>
        Coming,
        /// <summary>Theirs staying at the colony, or ours on the road to their town.</summary>
        Staying,
        /// <summary>Ours living at their town.</summary>
        There,
        /// <summary>The season over: theirs walking out, or ours on the road home.</summary>
        Leaving
    }

    /// <summary>
    /// The arithmetic of the envoy (docs/design/stage2-diplomacy.md, round a, item 4; release 2a.3; decision 6), with no
    /// game types so Tools/Tests can check it. Their envoy: a people at peace (goodwill 30 or more) whose envoy the colony
    /// knows may offer to send them for a season, at most once a year per people and a season apart at a home, one envoy
    /// at a time per home; or the colony asks by letter. They stay as a guest who does no work, and the stay is scored day
    /// by day (fed at the colony's table or from their packs, a bed of their own, a spare one or the floor, how their talks
    /// with the colonists went, gifts, a seat at the council's meeting, hurt while sheltered or in the open), read as a mood
    /// by the score a day. Their letters home every five days nudge their head's regard; at the end their report moves it
    /// (the deed "envoyHonored", or "slighted"), and a very good stay leaves a debt of honour. The colony's own envoy: a
    /// colonist good with people (Social 6 or more), away for a season at their town, whose letters every five days raise
    /// the regard of the town's notables by their Social; home again with the game's peace talks' practice in Social.
    /// </summary>
    public static class EnvoyMath
    {
        public const int DayTicks = 60000;

        /// <summary>A season.</summary>
        public const float StayDays = 15f;

        // ---------------- Their offer ----------------

        public const int MinGoodwill = 30;

        /// <summary>Once a year per people; a season apart at a home.</summary>
        public const float PeopleGapDays = 60f, HomeGapDays = 15f;

        /// <summary>Each day, a people that may offer does so one time in this many.</summary>
        public const float DailyOdds = 30f;

        /// <summary>
        /// A people may offer their envoy to this home now: at peace with goodwill 30 or more, their envoy known to the
        /// colony and free to come, no envoy staying at the home, and the gaps kept (-1: never offered).
        /// </summary>
        public static bool MayOffer(bool hostile, int goodwill, bool envoyKnown, bool oneHere, int lastPeopleOffer, int lastHomeOffer, int now) =>
            !hostile && goodwill >= MinGoodwill && envoyKnown && !oneHere
            && (lastPeopleOffer < 0 || now - lastPeopleOffer >= PeopleGapDays * DayTicks)
            && (lastHomeOffer < 0 || now - lastHomeOffer >= HomeGapDays * DayTicks);

        public static bool OffersToday(float roll) => roll < 1f / DailyOdds;

        /// <summary>The colony asks by letter: they send them by their head's regard (four in ten at 0, never certain).</summary>
        public static float SendChance(int regard) => Clamp(0.4f + regard / 100f, 0.1f, 0.95f);

        public static bool SendsWhenAsked(bool hostile, int goodwill, int regard, float roll) =>
            !hostile && goodwill >= MinGoodwill && Clamp01(roll) < SendChance(regard);

        /// <summary>Agreed: they arrive one to two days later.</summary>
        public static float ComeDays(float roll) => 1f + Clamp01(roll);

        // ---------------- The stay, day by day ----------------

        public const float FedDay = 1f, PacksDay = -2f, FineMeal = 0.5f, PlainForEmpire = -0.5f;
        public const float OwnBed = 1.5f, SpareBed = 0.5f, FloorNight = -2f, SharedRoomEmpire = -1f;
        public const float Gift = 3f, SatIn = 2f, HurtSheltered = -3f, HurtOpen = -8f;

        /// <summary>A day's meals: fed from the colony's table, or only from their own packs; the Empire's envoy minds whether it was fine.</summary>
        public static float Day(bool fedOurs, bool packsOnly, bool empire, bool fine)
        {
            float s = 0f;
            if (fedOurs) s += FedDay;
            else if (packsOnly) s += PacksDay;
            if (empire && fedOurs) s += fine ? FineMeal : PlainForEmpire;
            return s;
        }

        /// <summary>A night: the bed of their own, a spare one, the floor; the Empire's envoy expects a room of their own.</summary>
        public static float Night(EnvoyNight n, bool empire, bool roomAlone)
        {
            switch (n)
            {
                case EnvoyNight.Own: return empire && !roomAlone ? OwnBed + SharedRoomEmpire : OwnBed;
                case EnvoyNight.Spare: return empire ? SpareBed + SharedRoomEmpire : SpareBed;
                default: return FloorNight;
            }
        }

        /// <summary>How their talks with the colonists went in a day (the change in the warmth between them), a little either way.</summary>
        public static float Talks(float warmthChange) => Clamp(warmthChange / 20f, -2f, 1.5f);

        /// <summary>Hurt at the colony: a little against it while it sheltered them, a lot when it left them in the open.</summary>
        public static float Hurt(bool sheltered) => sheltered ? HurtSheltered : HurtOpen;

        // ---------------- How it goes ----------------

        /// <summary>The stay in a word, by the score a day (the first day counts as one): content from 1 a day, offended below -0.5.</summary>
        public static EnvoyMood Mood(float score, float days)
        {
            float perDay = score / Math.Max(1f, days);
            return perDay >= 1f ? EnvoyMood.Content : perDay >= -0.5f ? EnvoyMood.Cool : EnvoyMood.Offended;
        }

        /// <summary>A letter home moves their head's regard of the colony by how the stay goes.</summary>
        public static int LetterRegard(EnvoyMood m) => m == EnvoyMood.Content ? 2 : m == EnvoyMood.Offended ? -2 : 0;

        /// <summary>The report at the end: their head's regard.</summary>
        public static int EndRegard(EnvoyMood m) => m == EnvoyMood.Content ? 12 : m == EnvoyMood.Offended ? -10 : 3;

        /// <summary>A very good stay (content at 2 a day or more) leaves their people owing the colony a debt of honour.</summary>
        public static bool LeavesFavour(float score, float days) => score / Math.Max(1f, days) >= 2f;

        /// <summary>The weight of the deed at the end: envoyHonored 8, 12 for a very good stay (a debt); slighted 8.</summary>
        public static int DeedWeight(float score, float days) => LeavesFavour(score, days) ? 12 : 8;

        /// <summary>Their envoy died at the colony: a little regard lost while it sheltered them, a lot in the open.</summary>
        public static int DiedRegard(bool sheltered) => sheltered ? -5 : -20;

        /// <summary>Letters home every five days of the stay.</summary>
        public const float LetterDays = 5f;

        /// <summary>A letter is due (the days stayed and the letters already sent).</summary>
        public static bool LetterDue(float daysStayed, int sent) => daysStayed >= LetterDays * (sent + 1) && daysStayed < StayDays;

        /// <summary>A runner waits this long at the gate for the letter before going without it.</summary>
        public const float RunnerWaitHours = 6f;

        // ---------------- The colony's own envoy ----------------

        /// <summary>Good with people: Social this high at least.</summary>
        public const int MinSocial = 6;

        public static bool CanGo(int social, bool grown, bool canTalk) => grown && canTalk && social >= MinSocial;

        /// <summary>Each letter from their town raises the regard of its notables by the envoy's Social: 1 and a quarter of it.</summary>
        public static int AbroadRegard(int social) => 1 + Math.Max(0, social) / 4;

        /// <summary>Home again: the game's peace talks' practice in Social.</summary>
        public const float SocialXp = 6000f;

        /// <summary>Their head sends a parting gift home with them when they think this well of the colony.</summary>
        public static bool PartingGift(int headRegard) => headRegard >= 30;

        /// <summary>The parting gift is worth this much silver.</summary>
        public const int GiftWorth = 200;

        /// <summary>Their town gives them food for the road home: this many days at a full appetite, two at least.</summary>
        public static float RoadDays(float days) => Math.Max(2f, days * 1.5f);

        // ---------------- The facts ----------------

        /// <summary>
        /// The talk's tags: their envoy is staying at the colony (envoy:here, {ENVOY} their name), the one spoken to is that
        /// envoy (them:envoy), the colony's envoy lives at their town now (envoy:ours, {OURENVOY}).
        /// </summary>
        public const string HereTag = "envoy:here", ThemTag = "them:envoy", OursTag = "envoy:ours", EnvoyKey = "ENVOY", OurEnvoyKey = "OURENVOY";

        public static readonly string[] Tags = { HereTag, ThemTag, OursTag };

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
