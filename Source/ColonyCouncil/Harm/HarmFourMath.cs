using System;

namespace ColonyCouncil
{
    /// <summary>A heavy build whose failure can hurt the builder (Harm/Mishaps.cs): a wall, a big piece of furniture, a bridge.</summary>
    public enum HeavyBuild
    {
        None,
        Wall,
        Furniture,
        Bridge
    }

    /// <summary>A games day's contest (Harm/GamesDay.cs).</summary>
    public enum GamesContest
    {
        Bout,
        Race
    }

    /// <summary>
    /// The rates and small rules of harm, part 4: the fifteen proposals of docs/harm-review-proposals.md the player took on
    /// 7 November 2026 (1.1.1, 2.5.2, 2.2.3, 1.2.3, 4.3.1, 3.4.1, 4.7.2, 4.5.3, 4.5.1, 4.9.1, 3.6.1, 1.4.2, 2.5.1, 2.8.1,
    /// 4.1.1), with no game types so Tools/Tests and Tools/Balance read them. Every chance here is a base rate: the game's
    /// side rolls it through the harm service with its kind (Harm.Roll), so the setting, the storyteller's lean and the
    /// kind's turn within the shared budget all apply, and the shared cap holds.
    /// </summary>
    public static class HarmFourMath
    {
        public const int DayTicks = HarmMath.DayTicks, HourTicks = HarmMath.HourTicks;

        // ================= The physician catches what they tend (1.1.1) =================

        /// <summary>About one tending in 40 of a catching everyday ailment passes it to the doctor.</summary>
        public const float PhysicianEveryday = 1f / 40f;

        /// <summary>A contagious serious illness a tenth of that.</summary>
        public const float PhysicianSerious = 1f / 400f;

        /// <summary>A doctor's tendings of the sick in a day before they are run off their feet (a second doctor sharing the rounds keeps under it).</summary>
        public const int RoundsBeforeTired = 2;

        /// <summary>
        /// The chance a tending passes it on: one in 40 for the everyday kind, a tenth for the serious; gloves and masks (an
        /// industrial or spacer colony) halve it, a sterile room quarters it, the quarantine or rest-when-ill decree halves
        /// it; from a doctor's third tending of the sick that day, half again (run off their feet).
        /// </summary>
        public static float PhysicianChance(bool serious, bool modern, bool sterile, bool decree, int roundsToday)
        {
            float p = serious ? PhysicianSerious : PhysicianEveryday;
            if (modern) p *= 0.5f;
            if (sterile) p *= 0.25f;
            if (decree) p *= 0.5f;
            if (roundsToday > RoundsBeforeTired) p *= 1.5f;
            return p;
        }

        /// <summary>A room counts as sterile when the game's infection factor there is this or less (sterile tiles).</summary>
        public const float SterileInfection = 0.7f;

        /// <summary>What was caught shows a day or two after.</summary>
        public const float CaughtDaysMin = 1f, CaughtDaysMax = 2f;

        public static int CaughtTicks(float roll) => (int)((CaughtDaysMin + (CaughtDaysMax - CaughtDaysMin) * HarmMath.Clamp(roll, 0f, 1f)) * DayTicks);

        /// <summary>An onset the cap holds back (a sickbed, a hurt lately) waits up to three days, then passes.</summary>
        public const float OnsetGraceDays = 3f;

        public static bool OnsetLapsed(int due, int now) => now - due > OnsetGraceDays * DayTicks;

        // ================= The guest's cough (2.5.2) =================

        /// <summary>One visit in 20 brings a cough with it.</summary>
        public const float GuestCoughVisit = 1f / 20f;

        /// <summary>Near enough to catch it: within this many cells of the coughing guest, both awake.</summary>
        public const float CoughRange = 4f;

        /// <summary>Time spent with the coughing guest before one can catch it: an hour's looks near them.</summary>
        public const int CloseHoursToCatch = 1;

        /// <summary>The one who spent the most time with them catches it, unless a decree keeps the sick apart (its factor on it).</summary>
        public static float GuestCoughCatch(int closeHours, float contagionFactor) => closeHours < CloseHoursToCatch ? 0f : HarmMath.Clamp(contagionFactor, 0f, 1f);

        /// <summary>It shows about a day after the guest has gone.</summary>
        public const float GuestCoughDays = 1f;

        /// <summary>A grown-up's cough is a chest cold one time in four, else a head cold; a child's always a head cold (the mildest).</summary>
        public const float ChestColdShare = 0.25f;

        // ================= Caught in the hail (2.2.3) =================

        /// <summary>Each 250 ticks out under the falling hail, about one chance in 30 of a stone that bruises (a third of an hour out).</summary>
        public const float HailHit = 1f / 30f;

        /// <summary>One or two bruises a storm at most, on a map.</summary>
        public const int HailMaxPerStorm = 2;

        /// <summary>The bruise: a blunt hit of 1 to 3, the mildest (children may take it).</summary>
        public const float HailLow = 1f, HailHigh = 3f;

        /// <summary>The chance for one of the colony out in it: none under a roof or with a helmet (worn headgear with blunt armour).</summary>
        public static float HailChance(bool roofed, bool helmet) => roofed || helmet ? 0f : HailHit;

        /// <summary>Headgear that turns hail: blunt armour of this much or more.</summary>
        public const float HelmetBlunt = 0.1f;

        // ================= A cold night for the frail (1.2.3) =================

        /// <summary>One chill per cold week of the banked fires decree for the frail: a seventh a night.</summary>
        public const float ColdNightChance = 1f / 7f;

        /// <summary>A hard frost: the night outside at this or colder.</summary>
        public const float HardFrost = -5f;

        /// <summary>Hours asleep in a bedroom below their comfortable warmth that make a cold night.</summary>
        public const int ColdHours = 3;

        /// <summary>A cold night for the frail: under the decree, in a hard frost, three hours asleep below comfort, and frail.</summary>
        public static bool ColdNight(bool decree, float outdoor, int coldHours, bool frail) => decree && frail && outdoor <= HardFrost && coldHours >= ColdHours;

        // ================= A felling mishap (4.3.1) =================

        /// <summary>One tree in 300 for a cutter under Plants 5; none for the skilled.</summary>
        public const float FellingChance = 1f / 300f;

        public const int FellingSkill = 5;

        /// <summary>
        /// A tree's chance of coming down the wrong way: by the cutter's skill, and by the colony's means (a stone axe half
        /// again as often, a steel one three quarters, a spacer's tools half).
        /// </summary>
        public static float Felling(int plants, WorldAge age)
        {
            if (plants >= FellingSkill) return 0f;
            return FellingChance * AgeTools(age);
        }

        public static float AgeTools(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return 1.5f;
                case WorldAge.Medieval: return 1f;
                case WorldAge.Industrial: return 0.75f;
                default: return 0.5f;
            }
        }

        /// <summary>A bruise of 3 to 6 on a leg; a crack of 2 to 4 on its bone one time in four.</summary>
        public const float FellBruiseLow = 3f, FellBruiseHigh = 6f, FellCrackLow = 2f, FellCrackHigh = 4f, FellCrackShare = 0.25f;

        // ================= A failed build that hurts (3.4.1) =================

        /// <summary>One failure in ten on heavy work bruises the builder.</summary>
        public const float FailedBuildChance = 1f / 10f;

        /// <summary>What counts as heavy: a wall, furniture of two cells or more or a heavy piece, a bridge.</summary>
        public static HeavyBuild HeavyOf(bool wall, bool bridge, int cells, float mass)
        {
            if (bridge) return HeavyBuild.Bridge;
            if (wall) return HeavyBuild.Wall;
            if (cells >= 2 || mass >= HeavyMass) return HeavyBuild.Furniture;
            return HeavyBuild.None;
        }

        /// <summary>A piece heavy enough to hurt when it comes down: this many kilograms.</summary>
        public const float HeavyMass = 20f;

        public static float FailedBuild(HeavyBuild heavy) => heavy == HeavyBuild.None ? 0f : FailedBuildChance;

        /// <summary>The bruise: 3 to 6 on an arm (holding it); a wall or a bridge crushes a foot one time in five.</summary>
        public const float BuildLow = 3f, BuildHigh = 6f, BuildCrushShare = 0.2f;

        // ================= Burned at the bench (4.7.2) =================

        /// <summary>One drink in 100 before hot work (cooking, smithing) ends in a burn or a cut.</summary>
        public const float HotBenchChance = 1f / 100f;

        /// <summary>The drink's mark lasts while its warmth does: four hours.</summary>
        public const float HotBenchHours = 4f;

        public static bool HotBenchKept(int drankAt, int now) => drankAt <= now && now - drankAt < HotBenchHours * HourTicks;

        /// <summary>A burn of 2 to 5 on a hand; at the stove a cut from the knife one time in three.</summary>
        public const float HotLow = 2f, HotHigh = 5f, KnifeShare = 1f / 3f;

        /// <summary>Hot work: the stove and the forge.</summary>
        public static bool HotWork(string workType) => workType == "Cooking" || workType == "Smithing";

        // ================= The rut in the pens (4.5.3) =================

        /// <summary>An hour in a pen near a rutting male: about one charge in twenty such hours (one a rut for a pen people go into).</summary>
        public const float RutChargeHour = 1f / 20f;

        /// <summary>Near enough to be charged: within this many cells of him.</summary>
        public const float RutRange = 6f;

        /// <summary>A charge lands half the time; the rest jump clear.</summary>
        public const float RutLands = 0.5f;

        /// <summary>The colony's males that rut: grown, a herd's grazer (never a predator), a body of 0.9 or more (a ram, a buck, a bull).</summary>
        public static bool Ruts(bool male, bool adult, bool predator, bool herd, float size) => male && adult && !predator && herd && size >= RutSize;

        public const float RutSize = 0.9f;

        /// <summary>
        /// The rut: autumn, by the land's own seasons (quadrum 3 of 4 where there are none). <paramref name="season"/> is the
        /// game's Season as a number (Spring 1, Summer 2, Fall 3, Winter 4, permanent summer 5, permanent winter 6...).
        /// </summary>
        public static bool RutSeason(int season, int quadrum) => season == 3 || season >= 5 && quadrum == 2;

        /// <summary>The chance an hour in his pen: none for the herdmaster (it's theirs to do) and half for a handler of Animals 10 or more.</summary>
        public static float RutCharge(bool herdmaster, int animals) => herdmaster ? 0f : RutChargeHour * (animals >= 10 ? 0.5f : 1f);

        /// <summary>The charge: a bruise of 4 to 8; a crack of 2 to 5 one time in five.</summary>
        public const float RutLow = 4f, RutHigh = 8f, RutCrackLow = 2f, RutCrackHigh = 5f, RutCrackShare = 0.2f;

        // ================= The herdmaster's lesson (4.5.1) =================

        /// <summary>One lesson in 30 a nervous animal kicks or bites the learner (the game's training revenge, halved with the herdmaster there).</summary>
        public const float LessonKickChance = 1f / 30f;

        /// <summary>A nervous animal: wild at heart (the game's wildness of this much or more), or timid by nature.</summary>
        public const float NervousWildness = 0.4f;

        public static bool Nervous(float wildness, bool timid) => timid || wildness >= NervousWildness;

        /// <summary>The learner beside the herdmaster's animal: within this many cells.</summary>
        public const float LessonRange = 4f;

        /// <summary>A kick's bruise of 3 to 6 from a big animal, a bite of 2 to 4 from a small one; a child's small bruise of 1 to 3.</summary>
        public const float KickLow = 3f, KickHigh = 6f, BiteLow = 2f, BiteHigh = 4f, ChildLow = 1f, ChildHigh = 3f;

        /// <summary>A body this big kicks; smaller ones bite.</summary>
        public const float KickSize = 0.8f;

        // ================= A fall from the saddle (4.9.1) =================

        /// <summary>One ride in 50 on a mount still green under the saddle throws a rider short of Animals 6.</summary>
        public const float SaddleThrowChance = 1f / 50f;

        public const int SaddleSkill = 6;

        /// <summary>The chance a dismount throws them: nothing for a rider of Animals 6 or more, or on a mount trained to obey.</summary>
        public static float SaddleThrow(int animals, bool obedient) => animals >= SaddleSkill || obedient ? 0f : SaddleThrowChance;

        /// <summary>The fall: a bruise of 2 to 5; a child's of 1 to 2.</summary>
        public const float SaddleLow = 2f, SaddleHigh = 5f, SaddleChildLow = 1f, SaddleChildHigh = 2f;

        // ================= The groups come to blows (3.6.1) =================

        /// <summary>Two groups bitter enough for a dig to come to blows: their rivalry at this or more.</summary>
        public const float BitterGroups = 60f;

        /// <summary>A dig at a shared table or a gathering between two bitter groups: one in eight comes to blows.</summary>
        public const float GroupBrawlChance = 1f / 8f;

        /// <summary>The chance: half with the peacemaker in office, half under the shared meals decree, never short of bitter or away from a table.</summary>
        public static float GroupBrawl(float rivalry, bool atTable, bool peacemaker, bool sharedMeals)
        {
            if (rivalry < BitterGroups || !atTable) return 0f;
            return GroupBrawlChance * (peacemaker ? 0.5f : 1f) * (sharedMeals ? 0.5f : 1f);
        }

        /// <summary>Two or three pairs at once: up to two more join the first.</summary>
        public const int GroupJoinPairs = 2;

        public static int GroupJoins(int oursFree, int theirsFree) => Math.Max(0, Math.Min(GroupJoinPairs, Math.Min(oursFree, theirsFree)));

        /// <summary>Those of both groups near enough to join: within this many cells.</summary>
        public const float GroupJoinRange = 9f;

        /// <summary>The words for the two lots by the colony's means: clans at the fire, factions in the hall, shifts, departments.</summary>
        public static string GroupsForm(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return "Tribal";
                case WorldAge.Medieval: return "Medieval";
                case WorldAge.Industrial: return "Industrial";
                default: return "Spacer";
            }
        }

        // ================= The sore loser (1.4.2) =================

        /// <summary>One close loss in ten among the hot-tempered or the bitter brings the loser to the winner's door.</summary>
        public const float SoreLoserChance = 1f / 10f;

        /// <summary>A close race: one vote in it, or a fifth of the votes cast.</summary>
        public static bool CloseRace(int winnerVotes, int loserVotes, int counted)
        {
            int margin = winnerVotes - loserVotes;
            return margin >= 0 && (margin <= 1 || counted > 0 && margin <= counted * 0.2f);
        }

        /// <summary>The chance: only for a hot temper or a bitter loser, half with a gracious winner (a handshake at the count).</summary>
        public static float SoreLoser(bool close, bool hot, bool bitter, bool gracious)
        {
            if (!close || !(hot || bitter)) return 0f;
            return SoreLoserChance * (gracious ? 0.5f : 1f);
        }

        /// <summary>A bitter loser: their opinion of the winner this low or lower.</summary>
        public const int BitterOpinion = -20;

        /// <summary>They walk over one to three days after the count.</summary>
        public const float SoreDaysMin = 1f, SoreDaysMax = 3f;

        public static int SoreTicks(float roll) => (int)((SoreDaysMin + (SoreDaysMax - SoreDaysMin) * HarmMath.Clamp(roll, 0f, 1f)) * DayTicks);

        /// <summary>Words, and blows half the time.</summary>
        public const float SoreBlows = 0.5f;

        /// <summary>The walk over waits this long for the two to meet, then it passes.</summary>
        public const float SoreKeepDays = 3f;

        // ================= A guest's night goes sour (2.5.1) =================

        /// <summary>One guest's night in 15 with a bed for it (the feasts' own rate).</summary>
        public const float GuestNightChance = 1f / 15f;

        /// <summary>
        /// A night's chance by the guest's fighting temper (as a feast's guest), half again after a drink at supper, half with
        /// the warden of guests in office, half under the best-behavior decree; never a child.
        /// </summary>
        public static float GuestNight(int temper, bool drink, bool warden, bool manners, bool child)
        {
            if (child) return 0f;
            float f = HarmMath.Clamp(1f + Math.Max(-2, Math.Min(4, temper)) * 0.5f, 0.5f, 2f);
            return GuestNightChance * f * (drink ? 1.5f : 1f) * (warden ? 0.5f : 1f) * (manners ? 0.5f : 1f);
        }

        /// <summary>The evening it can sour: after supper, seven to eleven.</summary>
        public static bool GuestEvening(int hour) => hour >= 19 && hour < 23;

        /// <summary>One of the colony near the guest after supper: within this many cells.</summary>
        public const float GuestNear = 12f;

        // ================= The rematch (2.8.1) =================

        /// <summary>One lost duel or badly lost bout in three brings a rematch within the year.</summary>
        public const float RematchChance = 1f / 3f;

        /// <summary>A grudge kept a year (sixty days): after that it is let go.</summary>
        public const float RematchKeepDays = 60f;

        /// <summary>On a later visit: at least this long after the loss.</summary>
        public const float RematchAfterDays = 3f;

        public static bool RematchDue(int lostAt, int now, bool leftSince) => now - lostAt >= RematchAfterDays * DayTicks && leftSince;

        public static bool RematchKept(int lostAt, int now) => lostAt <= now && now - lostAt < RematchKeepDays * DayTicks;

        /// <summary>Whether the colonist takes the rematch: a hot or proud temper always, a calm one when they are the better fighter.</summary>
        public static bool TakesRematch(int temper, int melee, int theirMelee) => temper > 0 || melee >= theirMelee;

        // ================= Games with real contests (4.1.1) =================

        /// <summary>A games day's bout: one in 25 lands a real blow, the sparring rule.</summary>
        public const float GamesBlowChance = 1f / 25f;

        /// <summary>A race on rough ground: one runner in eight turns an ankle.</summary>
        public const float RaceSprainChance = 1f / 8f;

        /// <summary>Pairs at the games' bout, and runners in the race.</summary>
        public const int GamesPairs = 2, GamesRunners = 4;

        /// <summary>A runner's chance: on rough ground only (none on a floor), none in a spacer colony's sim tourney.</summary>
        public static float RaceSprain(bool rough, WorldAge age) => rough && age != WorldAge.Spacer ? RaceSprainChance : 0f;

        /// <summary>The share of the course on natural ground at which it counts as rough.</summary>
        public const float RoughShare = 0.5f;

        /// <summary>The race's course: out to a mark this many cells away and back.</summary>
        public const int RaceLength = 14;

        /// <summary>Expected small hurts in a games day (for the tests and the docs): the bouts' blows and the race's ankles.</summary>
        public static float GamesDayHurts(int pairs, int runners, float roughShare, WorldAge age) =>
            pairs * GamesBlowChance + runners * roughShare * RaceSprain(true, age);

        /// <summary>The contests by the colony's means: wrestling and a footrace; staves and a race; boxing and a three-legged race; a sim tourney.</summary>
        public static string GamesForm(WorldAge age) => GroupsForm(age);

        /// <summary>A spacer's sim tourney's only hurt: a wrenched wrist, a bruise of 2 to 4 on a hand.</summary>
        public const float WristLow = 2f, WristHigh = 4f;

        /// <summary>A child's scrape in the race: 1 to 2, the mildest.</summary>
        public const float ScrapeLow = 1f, ScrapeHigh = 2f;

        /// <summary>The contests wait for the day's gathering this long, then the day goes on without them.</summary>
        public const float GamesKeepHours = 12f;
    }
}
