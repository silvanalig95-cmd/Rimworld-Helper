using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The contests a feast with guests may hold (decision 7: every age; a shooting match only with a mod's targets).</summary>
    public enum ContestKind
    {
        /// <summary>Bouts between a colonist and a guest by the sparring rules: eight swings each, or until one yields to the pain.</summary>
        Wrestling,
        /// <summary>Runners from the gate to a marker and back, at a run.</summary>
        Race,
        /// <summary>Colonists and guests hunt the colony's game together, counted by kills.</summary>
        Hunt,
        /// <summary>A game at the colony's tables (chess, poker, billiards, horseshoes, hoopstone), played as the game's own recreation.</summary>
        Tables,
        /// <summary>Shots at a target, only where a mod gives one.</summary>
        Shooting
    }

    /// <summary>What guests come to the colony for: a feast of the colony's own, or a wedding (release 2b.2 hosts its guests here).</summary>
    public enum FeastPurpose
    {
        Feast,
        Wedding
    }

    /// <summary>Where a feast with guests stands, step by step (never a time left).</summary>
    public enum FeastStage
    {
        /// <summary>The invitations are out and the answers not all in.</summary>
        Inviting,
        /// <summary>The guests have answered; the day is set.</summary>
        Set,
        /// <summary>The guests walk in and wait by the gate.</summary>
        Here,
        /// <summary>The contests, one after another.</summary>
        Contests,
        /// <summary>The feast itself, a gathering with the guests.</summary>
        Feasting,
        /// <summary>The guests stay the night.</summary>
        Night,
        /// <summary>Over: the guests went home, or it was called off.</summary>
        Done
    }

    /// <summary>Where one guest stands.</summary>
    public enum GuestState
    {
        /// <summary>Their invitation is on its way, or they haven't answered.</summary>
        Invited,
        /// <summary>They said yes and will come on the day.</summary>
        Coming,
        /// <summary>They said no, or their invitation never reached them.</summary>
        Declined,
        /// <summary>At the colony.</summary>
        Here,
        /// <summary>Gone home (or lost on the way).</summary>
        Gone
    }

    /// <summary>What a guest hopes for at the colony's feast, by their people's ways.</summary>
    public enum FeastHope
    {
        None,
        /// <summary>A tribe: a hunt.</summary>
        Hunt,
        /// <summary>The middle ages: a hunt or a tournament (wrestling).</summary>
        HuntOrTourney,
        /// <summary>An industrial people: a race or a match.</summary>
        RaceOrMatch,
        /// <summary>Spacers: an exhibition match (wrestling or a game at the tables).</summary>
        Exhibition,
        /// <summary>The Empire: a fine meal and music.</summary>
        MealAndMusic
    }

    /// <summary>
    /// The arithmetic of feasts, hunts, games and contests with guests (docs/design/stage2-diplomacy.md, round b, item 5;
    /// release 2b.1; decision 7), with no game types so Tools/Tests can check it. A home holds one feast with guests a
    /// season at most, one at a time: up to three notables of peoples at peace, within three days' travel, whose regard of
    /// the colony is 0 or more, are invited by the existing letters and calls; each says yes by their regard, what they hope
    /// for being offered, the road and their people's goodwill. The day is the first fine one after the last of them can
    /// arrive; they come with a small retinue, wait by the gate, watch and take part in up to four contests (wrestling by
    /// the sparring rules, a race, a hunt, a game at the tables, a shooting match only with a mod's targets), sit at the
    /// feast, stay the night by the hosting rules and go home at first light. Their welcome moves their regard: a meal, a
    /// fine one, what they hoped for, the contests, a prize, a bed; a night gone sour and an empty plate count against it.
    /// The bad sides at the game's own rates: a bout's real blow one time in 25 under the harm rule's shared limits (never
    /// a child, nobody hurt in the last three days, nobody pregnant), a guest's night gone sour one guest night in 15.
    /// </summary>
    public static class FeastMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        // ---------------- Who may be invited ----------------

        /// <summary>Up to three notables; four contests at most; a guest's retinue of one or two.</summary>
        public const int MaxGuests = 3, MaxContests = 4, MaxRetinue = 2;

        /// <summary>Within three days' travel, by their people's means.</summary>
        public const float MaxTravelDays = 3f;

        /// <summary>Their regard of the colony is this or more.</summary>
        public const int MinRegard = 0;

        /// <summary>One feast with guests a season at a home (from the last one held or called off once they'd come).</summary>
        public const float SeasonDays = 15f;

        /// <summary>A home may plan a feast with guests now: none under way, and the season since the last one held.</summary>
        public static bool MayPlan(bool underWay, int lastHeld, int now) =>
            !underWay && (lastHeld < 0 || now - lastHeld >= (long)(SeasonDays * DayTicks));

        /// <summary>A notable may be invited: of a people at peace, regard 0 or more, within three days' travel, free to travel.</summary>
        public static bool MayInvite(bool hostile, int regard, float travelDays, bool free) =>
            !hostile && regard >= MinRegard && travelDays <= MaxTravelDays && free;

        // ---------------- Their answer ----------------

        /// <summary>
        /// The chance they say yes: more the better they think of the colony and when what their people hope for is offered,
        /// less the longer the road; a friend of the colony more. A tenth at worst, never certain.
        /// </summary>
        public static float AcceptChance(int regard, int goodwill, bool hopeOffered, float travelDays)
        {
            float c = 0.55f + Clamp(regard, -100, 100) * 0.006f + (hopeOffered ? 0.15f : 0f) - Math.Max(0f, travelDays - 1f) * 0.08f + (goodwill >= 75 ? 0.1f : 0f);
            return Clamp(c, 0.1f, 0.95f);
        }

        public static bool Accepts(int regard, int goodwill, bool hopeOffered, float travelDays, float roll) =>
            regard >= MinRegard && Clamp01(roll) < AcceptChance(regard, goodwill, hopeOffered, travelDays);

        /// <summary>
        /// The earliest they can be at the colony once they've answered: the road back that their answer travels too, and half
        /// a day to set out (a call: a day to set out and the road).
        /// </summary>
        public static float ComeDays(float backDays, bool call) => call ? 1f + Math.Max(0.5f, backDays) : Math.Max(0.5f, backDays) + 0.5f;

        // ---------------- The day ----------------

        /// <summary>The hours guests walk in on the day (from the morning to early afternoon), and when they give up a day held back by weather or danger.</summary>
        public const int ArriveFrom = 8, ArriveTo = 15;

        /// <summary>Days past the day they keep waiting for a fine one before the feast is called off.</summary>
        public const int WaitDays = 2;

        /// <summary>
        /// The day of the feast (a day number): the day the last guest can arrive if that is before noon of it, else the next.
        /// <paramref name="arriveDay"/> and <paramref name="arriveHour"/> are when the last of them can be there.
        /// </summary>
        public static int FeastDay(int arriveDay, float arriveHour) => arriveHour <= 12f ? arriveDay : arriveDay + 1;

        /// <summary>
        /// Fine enough for a feast's outdoor part: no rain or snow to speak of, not too cold or hot to stand about outdoors,
        /// no poisoned air.
        /// </summary>
        public static bool Fine(float rain, float snow, float tempC, bool foul) =>
            !foul && rain < 0.1f && snow < 0.1f && tempC >= -8f && tempC <= 38f;

        /// <summary>The guests come now: the day has come, the hour is in their walking hours, it is fine and safe.</summary>
        public static bool ArriveNow(int today, int feastDay, int hour, bool fine, bool danger) =>
            today >= feastDay && hour >= ArriveFrom && hour < ArriveTo && fine && !danger;

        /// <summary>Waited long enough: the feast is called off (WaitDays days past its day with no fine, safe hour).</summary>
        public static bool GiveUp(int today, int feastDay, int hour) => today > feastDay + WaitDays || today == feastDay + WaitDays && hour >= ArriveTo;

        /// <summary>The contests begin an hour after the last guest is at the gate, not before noon.</summary>
        public const int ContestsFrom = 12;

        /// <summary>The feast is held in the evening; the gathering waits for its hours.</summary>
        public const int FeastFrom = 16, FeastTo = 21;

        /// <summary>An outdoor contest not started by this hour is let go (the light and the feast come first).</summary>
        public const int OutdoorBy = 18;

        /// <summary>First light: the guests get up and go home.</summary>
        public const int FirstLight = 6;

        /// <summary>The guests leave at first light, once they have had the night (at least six hours since it began).</summary>
        public static bool Leave(int hour, int nightTicks) => hour >= FirstLight && hour < 12 && nightTicks >= 6 * HourTicks;

        // ---------------- Hopes, by their people's ways ----------------

        /// <summary>What a people hopes for: a tribe a hunt; the middle ages a hunt or a tournament; an industrial people a race or a match; spacers an exhibition match; the Empire a fine meal and music.</summary>
        public static FeastHope Hope(WorldAge age, bool empire)
        {
            if (empire) return FeastHope.MealAndMusic;
            switch (age)
            {
                case WorldAge.Tribal: return FeastHope.Hunt;
                case WorldAge.Medieval: return FeastHope.HuntOrTourney;
                case WorldAge.Industrial: return FeastHope.RaceOrMatch;
                default: return FeastHope.Exhibition;
            }
        }

        /// <summary>A contest that answers the hope.</summary>
        public static bool Answers(FeastHope hope, ContestKind k)
        {
            switch (hope)
            {
                case FeastHope.Hunt: return k == ContestKind.Hunt;
                case FeastHope.HuntOrTourney: return k == ContestKind.Hunt || k == ContestKind.Wrestling;
                case FeastHope.RaceOrMatch: return k == ContestKind.Race || k == ContestKind.Shooting;
                case FeastHope.Exhibition: return k == ContestKind.Wrestling || k == ContestKind.Tables;
                default: return false;
            }
        }

        /// <summary>The hope is offered by these contests (the Empire's by music: the colony has an instrument).</summary>
        public static bool Offered(FeastHope hope, IEnumerable<ContestKind> contests, bool music)
        {
            if (hope == FeastHope.MealAndMusic) return music;
            if (contests == null) return false;
            foreach (var k in contests)
                if (Answers(hope, k))
                    return true;
            return false;
        }

        // ---------------- The contests ----------------

        /// <summary>Bouts, runners, hunters, pairs at the tables and shooters at most.</summary>
        public const int MaxBouts = 3, MaxRunners = 7, MaxHunters = 6, MaxTablePairs = 2, MaxShooters = 6;

        /// <summary>How long a hunt may go on, and a game at the tables lasts.</summary>
        public const int HuntTicks = 3 * HourTicks, TableTicks = 2000;

        /// <summary>A contest that has run this long is over, whatever is still going on.</summary>
        public static int MaxTicks(ContestKind k)
        {
            switch (k)
            {
                case ContestKind.Wrestling: return 4 * HourTicks;
                case ContestKind.Race: return 2 * HourTicks;
                case ContestKind.Hunt: return HuntTicks + HourTicks;
                case ContestKind.Tables: return TableTicks + HourTicks;
                default: return 3 * HourTicks;
            }
        }

        /// <summary>Held out of doors (it waits for fine weather and the light).</summary>
        public static bool Outdoor(ContestKind k) => k != ContestKind.Tables;

        /// <summary>
        /// A bout's real blow (a cracked rib, a bloody nose gone bad), once a bout: one time in 25, the sparring rate; never
        /// when either fighter is a child, was hurt in the last three days (the harm rule's shared cap), is pregnant, or the
        /// "Hurts from everyday life" scale is at nothing.
        /// </summary>
        public const float HardBlowChance = 1f / 25f;

        public static bool HardBlow(float roll, bool mayBeHurt, float scale = 1f) => mayBeHurt && Clamp01(roll) < HardBlowChance * Math.Max(0f, scale);

        /// <summary>Days since someone's last hurt before they may take a Rimfolk-made one: the shared cap, one in three days.</summary>
        public const float HurtGapDays = 3f;

        public static bool MayBeHurt(bool child, bool pregnant, float daysSinceHurt) => !child && !pregnant && daysSinceHurt >= HurtGapDays;

        /// <summary>The winner of the bouts: the most won, then the most landed; null with none won.</summary>
        public static int BoutChampion(IList<int> wins, IList<int> hits)
        {
            int best = -1;
            for (int i = 0; i < wins.Count; i++)
            {
                if (wins[i] <= 0) continue;
                if (best < 0 || wins[i] > wins[best] || wins[i] == wins[best] && hits[i] > hits[best]) best = i;
            }
            return best;
        }

        /// <summary>A runner trips and loses a moment, one in twelve.</summary>
        public const float StumbleChance = 1f / 12f;

        public static bool Stumbles(float roll) => Clamp01(roll) < StumbleChance;

        /// <summary>How long a stumble costs (ticks): a second and a half to two and a half.</summary>
        public static int StumbleTicks(float roll) => 90 + (int)(Clamp01(roll) * 60f);

        /// <summary>The marker the runners go round: this many cells out from the gate, at most.</summary>
        public const int MarkerFar = 32, MarkerNear = 18;

        /// <summary>The first home: the lowest finish tick (each -1 if they never finished); -1 with none finished.</summary>
        public static int RaceWinner(IList<int> finish)
        {
            int best = -1;
            for (int i = 0; i < finish.Count; i++)
                if (finish[i] >= 0 && (best < 0 || finish[i] < finish[best]))
                    best = i;
            return best;
        }

        /// <summary>The best hunter: the most kills, then the first to make one; -1 with none.</summary>
        public static int HuntWinner(IList<int> kills, IList<int> firstKill)
        {
            int best = -1;
            for (int i = 0; i < kills.Count; i++)
            {
                if (kills[i] <= 0) continue;
                if (best < 0 || kills[i] > kills[best] || kills[i] == kills[best] && firstKill[i] >= 0 && (firstKill[best] < 0 || firstKill[i] < firstKill[best])) best = i;
            }
            return best;
        }

        /// <summary>Game for the hunt: one more than the hunters, two at least, eight at most.</summary>
        public static int HuntGame(int hunters) => Math.Max(2, Math.Min(8, hunters + 1));

        /// <summary>The chance the first player wins a game at the tables, by the game's own skill (0 to 20 each): a tenth to nine in ten.</summary>
        public static float TableChance(int skillA, int skillB) => Clamp(0.5f + (Clamp(skillA, 0, 20) - Clamp(skillB, 0, 20)) * 0.035f, 0.1f, 0.9f);

        public static bool TableWin(int skillA, int skillB, float roll) => Clamp01(roll) < TableChance(skillA, skillB);

        /// <summary>Shots each in a shooting match, and the ticks each takes to aim and loose.</summary>
        public const int Shots = 5, ShotTicks = 100;

        /// <summary>The best shot: the most hits, then the better shooting; -1 with no hits.</summary>
        public static int ShootingWinner(IList<int> hits, IList<int> skill)
        {
            int best = -1;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i] <= 0) continue;
                if (best < 0 || hits[i] > hits[best] || hits[i] == hits[best] && skill[i] > skill[best]) best = i;
            }
            return best;
        }

        /// <summary>A building that is a target to shoot at, read by name (Misc. Training's, Combat Training's, any "target" but a dummy).</summary>
        public static bool IsTarget(string defName, string label)
        {
            string a = (defName ?? "").ToLowerInvariant(), b = (label ?? "").ToLowerInvariant();
            if (a.Contains("dummy") || b.Contains("dummy")) return false;
            return a.Contains("target") || b.Contains("target") || b.Contains("archery butt") || a.Contains("archerybutt");
        }

        /// <summary>The champion of the day: the one who won the most contests, the first of them on a tie; -1 with none.</summary>
        public static int Champion(IList<int> wins)
        {
            int best = -1;
            for (int i = 0; i < wins.Count; i++)
                if (wins[i] > 0 && (best < 0 || wins[i] > wins[best]))
                    best = i;
            return best;
        }

        // ---------------- The night ----------------

        /// <summary>
        /// A guest's night gone sour after drink and the day's contests: one guest night in 15 (the harm review's 2.5.1),
        /// a hot temper more (half again for each step above none, twice at most), a kind one less; never a child.
        /// </summary>
        public const float SourChance = 1f / 15f;

        public static float SourNightChance(int temper, bool child)
        {
            if (child) return 0f;
            float f = 1f + Clamp(temper, -2, 4) * 0.5f;
            return SourChance * Clamp(f, 0.5f, 2f);
        }

        public static bool SourNight(int temper, bool child, float roll, float scale = 1f) => Clamp01(roll) < SourNightChance(temper, child) * Math.Max(0f, scale);

        // ---------------- The welcome ----------------

        /// <summary>
        /// How a guest's regard of the colony moves with their welcome: coming at all; a meal at the colony's table (none is
        /// a slight); a fine meal (the Empire expects one); what they hoped for held; the contests; a prize carried to them;
        /// a bed for the night (the floor is a poor welcome, more so for the Empire); a bout's real blow taken; a night gone
        /// sour. From about -25 to +35.
        /// </summary>
        public static float Welcome(bool ate, bool fine, bool hopeMet, int contests, bool prize, bool bed, bool floor, bool hurt, bool sour, bool empire)
        {
            float r = 6f;
            r += ate ? 4f : -10f;
            if (fine) r += empire ? 4f : 2f;
            else if (empire && ate) r -= 3f;
            if (hopeMet) r += 5f;
            r += Math.Min(3, Math.Max(0, contests)) * 2f;
            if (prize) r += 6f;
            if (bed) r += 2f;
            else if (floor) r -= empire ? 5f : 2f;
            if (hurt) r -= 2f;
            if (sour) r -= 6f;
            return r;
        }

        /// <summary>A welcome good enough for their people to remember the feast as the colony's good turn, and one so good it weighs like a large one.</summary>
        public const float RememberedAt = 10f, GreatAt = 20f;

        /// <summary>The deed's size in their people's memory: 8, and 12 for a great welcome.</summary>
        public static int DeedWeight(float welcome) => welcome >= GreatAt ? 12 : 8;

        /// <summary>Their head thinks a little better too when the head wasn't the guest (half, and never worse than nothing).</summary>
        public static float HeadShare(float welcome) => Math.Max(0f, welcome * 0.5f);

        /// <summary>Calling it off once guests said yes: each thinks this much less of the colony.</summary>
        public const float CalledOffRegard = -5f;

        // ---------------- The steward's suggestion ----------------

        /// <summary>Each day the steward may suggest a feast, one time in this many; once a season at most; a notable at 20 or more within reach.</summary>
        public const float SuggestOdds = 20f;

        public const int SuggestRegard = 20;

        public static bool Suggests(bool mayPlan, int lastSuggested, int now, bool friendNear, bool fed, bool quiet, float roll) =>
            mayPlan && friendNear && fed && quiet && (lastSuggested < 0 || now - lastSuggested >= (long)(SeasonDays * DayTicks)) && Clamp01(roll) < 1f / SuggestOdds;

        /// <summary>Meals enough for a feast: one for each of the colony's people and each guest and their retinue.</summary>
        public static int MealsWanted(int colonists, int guests, int retinue) => Math.Max(0, colonists) + Math.Max(0, guests) + Math.Max(0, retinue);

        // ---------------- Invited to theirs ----------------

        /// <summary>
        /// One of the colony's at a neighbour's feast (the missions' invitation): the chance they won something there, by
        /// their best skill for the contests their people hold (0 to 20): a fifth, up to a half. Told when they come home;
        /// nothing happens to them there.
        /// </summary>
        public static float AwayWinChance(int skill) => Clamp(0.2f + Clamp(skill, 0, 20) * 0.015f, 0.2f, 0.5f);

        public static bool AwayWin(int skill, float roll) => Clamp01(roll) < AwayWinChance(skill);

        /// <summary>A tale of theirs is told within this many days of coming home, else let go; a home keeps four.</summary>
        public const float TaleDays = 30f;

        public const int MaxTales = 4;

        // ---------------- Codes and tags ----------------

        /// <summary>The topic of an invitation to a feast in the letters' machinery: "feast:HOME".</summary>
        public const string TopicPrefix = "feast:";

        public static string Topic(int home) => TopicPrefix + home;

        public static bool ParseTopic(string key, out int home)
        {
            home = -1;
            return key != null && key.StartsWith(TopicPrefix, StringComparison.Ordinal) && int.TryParse(key.Substring(TopicPrefix.Length), out home);
        }

        /// <summary>
        /// The facts: invited to a feast at the colony (feast:invited), a feast with their people coming (feast:soon), their
        /// people's guests at the colony now (feast:here), the one spoken to came as a guest (them:feastguest), one of their
        /// people won a contest at it (feast:theywon) or one of the colony's did (feast:wewon); {FEAST} the colony's feast in
        /// its own words, {CHAMPION} who won the most.
        /// </summary>
        public const string InvitedTag = "feast:invited", SoonTag = "feast:soon", HereTag = "feast:here", GuestTag = "them:feastguest", TheyWonTag = "feast:theywon",
            WeWonTag = "feast:wewon", FeastKey = "FEAST", ChampionKey = "CHAMPION";

        public static readonly string[] Tags = { InvitedTag, SoonTag, HereTag, GuestTag, TheyWonTag, WeWonTag };

        /// <summary>Remembered this long after (days) in the talk.</summary>
        public const float TalkDays = 30f;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
