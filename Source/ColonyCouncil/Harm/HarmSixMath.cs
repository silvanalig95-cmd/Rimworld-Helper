using System;

namespace ColonyCouncil
{
    /// <summary>The hot or heavy work of an answer that plays out on the map (Harm/AnswerHurts.cs, 2.1.2).</summary>
    public enum AnswerWork
    {
        /// <summary>A fire laid and lit for a gathering (PlayFire.cs).</summary>
        Fire,
        /// <summary>The pyre lit at dusk (OnMapTimed.cs).</summary>
        Pyre,
        /// <summary>The dead carried out (PlayKind.Haul).</summary>
        Carry,
        /// <summary>The graves dug for them (PlayDead.cs).</summary>
        Dig
    }

    /// <summary>A mishap at a muster at the defense line (Harm/Contests.cs, 4.2.1), by the weapon in hand.</summary>
    public enum MusterMishap
    {
        /// <summary>A bow, a sling, a crossbow: the string snaps across the hand.</summary>
        Bowstring,
        /// <summary>A gun: a powder burn on the hand.</summary>
        Powder,
        /// <summary>A gun: a ricochet off the sandbags grazes an arm.</summary>
        Graze,
        /// <summary>A charge weapon of the spacer age: a burn from the cell.</summary>
        Charge
    }

    /// <summary>The dare a bold group eggs one of its own into (Harm/Contests.cs, 3.6.2).</summary>
    public enum DareKind
    {
        /// <summary>A swim in cold water.</summary>
        Swim,
        /// <summary>Wrestling the group's strongest.</summary>
        Wrestle
    }

    /// <summary>
    /// The rates and small rules of harm, part 6: the second of three batches of the review's remaining proposals the player
    /// took on 7 November 2026 ("all suggestions please"), the work's accidents, the animals and the contests and festivals
    /// (2.1.2, 2.1.3, 1.5.2, 3.3.2, 3.4.2, 4.3.2, 4.4.1, 3.11.1, 3.11.2, 4.7.1, 2.6.3; 2.4.3, 2.10.2, 3.11.4, 4.5.2, 4.6.1,
    /// 4.6.2; 4.1.2, 4.2.1, 4.8.2, 3.6.2, 3.7.2, 3.8.2). No game types, so Tools/Tests and Tools/Balance read them. Every
    /// chance here is a base rate: the game's side rolls the occasion once through the harm service with its kind
    /// (Harm.Roll, Harm.Chance), so the setting, the storyteller's lean and the kind's turn within the shared budget all
    /// apply once, and the shared cap holds.
    /// </summary>
    public static class HarmSixMath
    {
        public const int DayTicks = HarmMath.DayTicks, HourTicks = HarmMath.HourTicks;

        // ================= Hot and heavy answers (2.1.2) =================

        /// <summary>One play-out in 40 of an answer's hot or heavy work brings a small injury at the moment of the work.</summary>
        public const float HotHeavyChance = 1f / 40f;

        /// <summary>Skilled hands (8 or more in the work's skill: cooking for a fire, building for a load or a grave) half as often.</summary>
        public const int SkilledHands = 8;

        public static float HotHeavy(bool skilled) => HotHeavyChance * (skilled ? 0.5f : 1f);

        /// <summary>The fire and the pyre are hot work (a burn on a hand); the dead carried and the graves dug heavy (a bruise on a foot).</summary>
        public static bool Hot(AnswerWork w) => w == AnswerWork.Fire || w == AnswerWork.Pyre;

        /// <summary>A burn of 2 to 4 on a hand; a bruise of 3 to 5 on a foot from what was dropped.</summary>
        public const float HotLow = 2f, HotHigh = 4f, HeavyLow = 3f, HeavyHigh = 5f;

        // ================= Back too soon, for real (2.1.3) =================

        /// <summary>A third of the times one let back to light work overdoes it, they fall with a load.</summary>
        public const float BackTooSoonChance = 1f / 3f;

        /// <summary>The fall waits a day for them to be found carrying something.</summary>
        public const float BackTooSoonKeepDays = 1f;

        /// <summary>A bruise of 2 to 4, and two more days in bed.</summary>
        public const float FallLow = 2f, FallHigh = 4f, FallMoreDays = 2f;

        // ================= Tired hands' rule elsewhere: the last push (1.5.2), clumsy when worn (3.3.2) =================

        /// <summary>A project's last three days: work on its own building counts as hard work.</summary>
        public const float LastPushDays = 3f;

        public static bool LastPush(int deadlineTick, int now) => deadlineTick > now && deadlineTick - now < LastPushDays * DayTicks;

        /// <summary>The last push's long day: the tired-hands rule as it stands (HarmMath.TiredHands, one driving).</summary>
        public static float ProjectPush(bool lowRest, bool tough, bool spacer) => HarmMath.TiredHands(1, lowRest, tough, spacer);

        /// <summary>Clumsy when worn: in the high band, a long day at any work has half the tired-hands rate (one and a half with a decree, its own beside it).</summary>
        public const float ClumsyShare = 0.5f;

        public static float Clumsy(bool high, bool lowRest, bool tough, bool spacer) => high ? ClumsyShare * HarmMath.TiredHands(1, lowRest, tough, spacer) : 0f;

        // ================= The makeshift spot's burn (3.4.2) =================

        /// <summary>One bill in 200 cooked at a campfire or butchered at a spot when a proper bench could be built.</summary>
        public const float MakeshiftChance = 1f / 200f;

        /// <summary>Only while a proper bench could be built for it (never in a colony that can't build better yet).</summary>
        public static float Makeshift(bool makeshift, bool betterBuildable, bool cooking) => makeshift && betterBuildable && cooking ? MakeshiftChance : 0f;

        /// <summary>A burn at the campfire, a cut at the spot: 2 to 4 on a hand.</summary>
        public const float MakeshiftLow = 2f, MakeshiftHigh = 4f;

        // ================= Taking down hurts too (4.3.2) =================

        /// <summary>One wall or door in 100 taken down by someone under Construction 5 drops stone or timber on them.</summary>
        public const float TakingDownChance = 1f / 100f;

        public const int TakingDownSkill = 5;

        public static float TakingDown(bool wallOrDoor, int construction) => wallOrDoor && construction < TakingDownSkill ? TakingDownChance : 0f;

        /// <summary>The failed build's hurt: a bruise of 3 to 6 on an arm, a crush on a foot one time in five.</summary>
        public const float TakeDownLow = 3f, TakeDownHigh = 6f, TakeDownCrushShare = 0.2f;

        // ================= Hurried hands at the harvest (4.4.1) =================

        /// <summary>One hour in 200 of hurried harvesting by hand cuts a hand on the sickle; twice after dark, half for a grower of Plants 8.</summary>
        public const float HurriedChance = 1f / 200f;

        public const int HurriedSkill = 8;

        public static float Hurried(bool hurried, bool dark, int plants) => hurried ? HurriedChance * (dark ? 2f : 1f) * (plants >= HurriedSkill ? 0.5f : 1f) : 0f;

        /// <summary>The sickle's cut: 2 to 4 on a hand.</summary>
        public const float SickleLow = 2f, SickleHigh = 4f;

        // ================= Carries more than they should (3.11.1) =================

        /// <summary>A heavy day: six hours of building, mining or hauling (the strained back's own cause).</summary>
        public const float HeavyDayHours = 6f;

        /// <summary>The strained back half again as likely: half the strain's own 0.005 a heavy day, on top of it.</summary>
        public const float StrongBackStrain = 0.0025f;

        /// <summary>Now and then a dropped load bruises a foot: one heavy day in 150, as tired hands.</summary>
        public const float StrongBackDrop = 1f / 150f;

        /// <summary>The dropped load: a bruise of 2 to 4 on a foot.</summary>
        public const float DropLow = 2f, DropHigh = 4f;

        // ================= Sharp habits (3.11.2) =================

        /// <summary>A day they sit down to whittling or the whetstone: one in ten nicks a finger, about once a season.</summary>
        public const float NickChance = 1f / 10f;

        /// <summary>A nick: a cut of 1 to 2 on a hand, the mildest.</summary>
        public const float NickLow = 1f, NickHigh = 2f;

        // ================= The second drink (4.7.1) =================

        /// <summary>Three days running with a drink before work, then one such day in five a second cup.</summary>
        public const int DrinkStreak = 3;

        public const float SecondCupChance = 1f / 5f;

        public static bool SecondCupDue(int streakBefore) => streakBefore >= DrinkStreak;

        /// <summary>The streak: a drink today after one yesterday goes on; a day missed starts again.</summary>
        public static int Streak(int lastDay, int lastStreak, int today) => lastDay == today ? lastStreak : lastDay == today - 1 ? lastStreak + 1 : 1;

        /// <summary>After the second cup, the next hour right at the bench, the stove or the build: one stagger in four slips.</summary>
        public const float StaggerChance = 1f / 4f;

        /// <summary>The second cup's stagger is looked for this long.</summary>
        public const float StaggerHours = 4f;

        public static bool StaggerKept(int cupAt, int now) => cupAt <= now && now - cupAt < StaggerHours * HourTicks;

        /// <summary>A stagger's slip: 2 to 5.</summary>
        public const float StaggerLow = 2f, StaggerHigh = 5f;

        // ================= The hands come home sore (2.6.3) =================

        /// <summary>
        /// What a day and a half in the fields gives at home, by the everyday ailments' own rates: a strained back (0.005 a
        /// heavy day), blisters (0.03 a day on their feet), a sunburn (0.03 a day in the sun), each a day and a half.
        /// </summary>
        public const float SoreBack = 0.0075f, SoreBlisters = 0.045f, SoreSunburn = 0.045f;

        public const float SoreAll = SoreBack + SoreBlisters + SoreSunburn;

        /// <summary>A hand's chance by the share of the work done (half the time, half the share), half for the tough.</summary>
        public static float HandsSore(float share, bool tough) => SoreAll * HarmMath.Clamp(share, 0f, 1f) * (tough ? 0.5f : 1f);

        /// <summary>Which it is, by a roll: 0 the back, 1 blisters, 2 a sunburn.</summary>
        public static int SoreWhich(float roll)
        {
            float r = HarmMath.Clamp(roll, 0f, 0.9999f) * SoreAll;
            if (r < SoreBack) return 0;
            return r < SoreBack + SoreBlisters ? 1 : 2;
        }

        /// <summary>It waits for them to come home: twelve days at most.</summary>
        public const float HandsKeepDays = 12f;

        // ================= The drovers' bull (2.4.3) =================

        /// <summary>An hour among a camp's herd: one charge in 30 such hours, once a stay (one camp stay in 15 with a herd).</summary>
        public const float DroversChance = 1f / 30f;

        /// <summary>Among the herd: within this many cells of a beast of theirs of a body of 0.5 or more.</summary>
        public const float HerdRange = 3f, HerdBeastSize = 0.5f;

        /// <summary>The charge: a bruise of 3 to 6, a crack one time in six; a child's small bruise.</summary>
        public const float BullLow = 3f, BullHigh = 6f, BullCrackLow = 2f, BullCrackHigh = 4f, BullCrackShare = 1f / 6f, BullChildLow = 1f, BullChildHigh = 3f;

        // ================= The wild one too close (2.10.2) =================

        /// <summary>
        /// One such encounter in 20, by the game's taming-revenge chance for the kind (its manhunterOnTameFailChance, the
        /// usual being about a tenth): half for a kind that rarely turns, twice for one that often does.
        /// </summary>
        public const float TooCloseChance = 1f / 20f;

        public static float TooClose(float revenge, bool cautious)
        {
            if (cautious) return 0f;
            return TooCloseChance * HarmMath.Clamp(revenge / 0.1f, 0.5f, 2f);
        }

        /// <summary>A big animal kicks (a bruise of 3 to 6); a small one bites (2 to 4, and a bite can carry rabies by the serious tier's own roll); a child is nipped.</summary>
        public const float KickSize = 0.8f, KickLow = 3f, KickHigh = 6f, BiteLow = 2f, BiteHigh = 4f, NipLow = 1f, NipHigh = 1.8f;

        /// <summary>Close enough for it to lash out: within this many cells.</summary>
        public const float TooCloseRange = 8f;

        // ================= Walks the fields at dawn (3.11.4) =================

        /// <summary>Dawn: five to seven.</summary>
        public static bool Dawn(int hour) => hour >= 5 && hour <= 7;

        /// <summary>Alone at the field's edge outside the walls with a hungry predator in reach: one dawn in eight it comes.</summary>
        public const float DawnPredatorChance = 1f / 8f;

        public static float DawnPredator(bool alone, bool outsideWalls, bool predatorNear) => alone && outsideWalls && predatorNear ? DawnPredatorChance : 0f;

        /// <summary>The walk: out to the field's edge and a while there.</summary>
        public const int DawnWalkTicks = 900;

        // ================= The fed wolf grows bold (4.5.2) =================

        /// <summary>Fed some days: it has grown bold.</summary>
        public const float BoldAfterDays = 3f;

        public static bool Bold(bool predator, bool fed, float daysSinceFed) => predator && fed && daysSinceFed >= BoldAfterDays;

        /// <summary>A night with someone alone near the stores: one in 40 it comes for them (about one fed predator in four over a season).</summary>
        public const float BoldNightChance = 1f / 40f;

        /// <summary>Near the stores: within this many cells of storage; alone: nobody else of the colony within this many.</summary>
        public const float StoresNear = 12f, AloneRange = 10f;

        /// <summary>The night hours it may come: nine at night to four.</summary>
        public static bool Night(int hour) => hour >= 21 || hour < 4;

        /// <summary>Dusk, when it shows itself at the stores' door: five to seven.</summary>
        public static bool Dusk(int hour) => hour >= 17 && hour <= 19;

        // ================= A catch that fights back (4.6.1), the visitor at the stores (4.6.2) =================

        /// <summary>One try in four the wild Pokémon fights; a partner Pokémon at the trainer's side takes it instead.</summary>
        public const float CatchFightChance = 1f / 4f;

        public static float CatchFight(bool partner) => partner ? 0f : CatchFightChance;

        /// <summary>The trainer near enough to throw: within this many cells; a partner at their side within this many.</summary>
        public const float CatchRange = 6f, PartnerRange = 8f;

        /// <summary>The wild one's fight lasts this long at most, then it breaks off.</summary>
        public const int CatchFightTicks = 400;

        /// <summary>A fed Pokémon comes to the stores at dusk one day in three; one shooing in ten it lashes out (about one fed visitor in five).</summary>
        public const float StoresVisitChance = 1f / 3f, StoresSwipeChance = 1f / 10f;

        /// <summary>The swipe: a scratch of 2 to 4 on a hand or an arm; a child's 1 to 2.</summary>
        public const float SwipeLow = 2f, SwipeHigh = 4f, SwipeChildLow = 1f, SwipeChildHigh = 2f;

        // ================= The bonfire's spark (4.1.2) =================

        /// <summary>One bonfire in 20 on a dry, windy summer night throws a spark into the dry grass; never in rain or snow.</summary>
        public const float SparkChance = 1f / 20f;

        /// <summary>Windy: the game's wind speed at this or more.</summary>
        public const float SparkWind = 1f;

        public static float Spark(bool summer, bool rainOrSnow, float wind, bool outdoors) => summer && !rainOrSnow && wind >= SparkWind && outdoors ? SparkChance : 0f;

        /// <summary>Where a spark can land: dry grass from two to six cells off the fire (a cleared spot or a firebreak keeps it clear).</summary>
        public const float SparkNear = 2f, SparkFar = 6f;

        /// <summary>Someone this near where it caught is singed: a burn of 1 to 2, the mildest.</summary>
        public const float SingeRange = 2.5f, SingeLow = 1f, SingeHigh = 2f;

        /// <summary>Those who beat it out: up to three of the gathering.</summary>
        public const int Beaters = 3;

        // ================= Musters at the line (4.2.1) =================

        /// <summary>One muster in 30 has a mishap; half with a marshal of Shooting 10 or more.</summary>
        public const float MusterChance = 1f / 30f;

        public const int MusterMarshalSkill = 10;

        public static float Muster(bool skilledMarshal) => MusterChance * (skilledMarshal ? 0.5f : 1f);

        /// <summary>The muster is called on the militia's evening off its bouts (one evening in four), with the militia or the "hold the way in" project.</summary>
        public static bool MusterEvening(int day) => !HarmMath.MusterEvening(day);

        /// <summary>Shooters at the line, at most.</summary>
        public const int MusterShooters = 4;

        /// <summary>A while at their posts: about half an hour.</summary>
        public const int MusterTicks = 1250;

        /// <summary>A muster's practice, each shooter: a little Shooting.</summary>
        public const float MusterXp = 200f;

        /// <summary>The mishap by the weapon: a bow's or a sling's string (neolithic and medieval), a gun's powder or ricochet (half each), a charge weapon's burn.</summary>
        public static MusterMishap MishapOf(bool lowTech, bool spacer, float roll)
        {
            if (lowTech) return MusterMishap.Bowstring;
            if (spacer) return MusterMishap.Charge;
            return roll < 0.5f ? MusterMishap.Powder : MusterMishap.Graze;
        }

        /// <summary>The hurt: 2 to 4.</summary>
        public const float MishapLow = 2f, MishapHigh = 4f;

        // ================= Fighters spar (4.8.2, A RimWorld of Magic) =================

        /// <summary>One spar in 25 lands a real blow, the sparring rule.</summary>
        public const float SparBlowChance = 1f / 25f;

        /// <summary>Two fighters spar one evening in three, with their consent: neither thinks ill of the other, neither worn down.</summary>
        public const int SparEveryDays = 3;

        public static bool SparDay(int day) => ((day % SparEveryDays) + SparEveryDays) % SparEveryDays == 0;

        public static bool Consent(int opinionAB, int opinionBA, bool aWorn, bool bWorn) => opinionAB >= 0 && opinionBA >= 0 && !aWorn && !bWorn;

        // ================= Brave to a fault (3.6.2) =================

        /// <summary>A bold group eggs one of its own into a dare about once a season: one evening in 15.</summary>
        public const float DareChance = 1f / 15f;

        /// <summary>A cautious one at hand (the group's worrier, or a nervous anchor) talks them out of it half the time.</summary>
        public const float TalkedOut = 0.5f;

        /// <summary>One dare in three ends in a sprain or a chill; the rest in a laugh.</summary>
        public const float DareHurtChance = 1f / 3f;

        /// <summary>Cold enough for a swim to be a dare: the air outside at this or colder; freezing gives the game's hypothermia, else a head cold.</summary>
        public const float DareCold = 12f, DareFreezing = 0f;

        public static DareKind DareOf(bool water, float outdoor) => water && outdoor <= DareCold ? DareKind.Swim : DareKind.Wrestle;

        /// <summary>The swim's chill when it's freezing: the game's hypothermia at its shivering stage.</summary>
        public const float ChillLow = 0.1f, ChillHigh = 0.16f;

        // ================= A coward's name (3.7.2), fury that forgets cover (3.8.2) =================

        /// <summary>Once after the name forms, in the next fight: the pass and the setting decide.</summary>
        public const float CowardChance = 1f;

        /// <summary>A coward's name waits this long for a fight; then it is let go.</summary>
        public const float CowardKeepDays = 60f;

        /// <summary>Against the sworn people, once a battle at most: one in three.</summary>
        public const float FuryChance = 1f / 3f;

        /// <summary>The enemy they run at: the nearest within this many cells.</summary>
        public const float BreakRange = 45f;

        /// <summary>Breaking cover, a ranged weapon's holder runs to within this share of its range of the enemy.</summary>
        public const float BreakRangeShare = 0.6f;

        /// <summary>The run forward lasts at most this long (the attack after it the game's own).</summary>
        public const int BreakTicks = 1200;

        /// <summary>A name for courage gone bad: the courage side of their name at its first degree of cowardice or worse.</summary>
        public static bool CowardFormed(int wasDegree, int nowDegree) => nowDegree < 0 && wasDegree >= 0;
    }
}
