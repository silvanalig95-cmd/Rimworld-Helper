using System;

namespace ColonyCouncil
{
    /// <summary>A sickness carried in by someone (Harm/Carriers.cs): what brought it.</summary>
    public enum CarrierKind
    {
        /// <summary>One of the colony's own, working on with it after the petition to rest lapsed (1.2.5).</summary>
        SickWorker,
        /// <summary>A visitor, trader or caravan from a people under a plague (2.3.1).</summary>
        Gate,
        /// <summary>A courier from a people under a plague (2.7.2).</summary>
        Courier,
        /// <summary>The camp's own sick one, lying ill by their fire (2.4.2).</summary>
        Camp
    }

    /// <summary>
    /// The rates and small rules of harm, part 5: the first batch of the review's remaining proposals the player took on
    /// 7 November 2026 ("all suggestions please"), sickness that spreads and the weather and the cold (1.2.5, 2.3.1, 2.4.2,
    /// 2.7.2, 3.2.2, 2.2.4, 3.2.3, 4.9.3, 3.1.2, 4.2.2, 3.11.3, 3.12.1, 3.3.3, 4.9.2; and 4.4.2, the forecast that can be
    /// wrong, which hurts nobody and stays outside the budget). No game types, so Tools/Tests and Tools/Balance read them.
    /// Every chance here is a base rate: the game's side rolls the occasion once through the harm service with its kind
    /// (Harm.Roll, Harm.Chance), so the setting, the storyteller's lean and the kind's turn within the shared budget all
    /// apply once, and the shared cap holds.
    /// </summary>
    public static class HarmFiveMath
    {
        public const int DayTicks = HarmMath.DayTicks, HourTicks = HarmMath.HourTicks;

        // ================= Carriers: the sick work on, the gate, a courier, the camp's fire =================

        /// <summary>
        /// The sick work on (1.2.5): a petition to rest when ill or for a quarantine let lapse while a catching ailment goes
        /// round brings one more case, whoever worked closest to the sick one that day.
        /// </summary>
        public const float SickWorkOnChance = 1f;

        /// <summary>The sick one works on this long after the petition lapsed; then whoever was nearest them most catches it.</summary>
        public const float SickWorkOnHours = 24f;

        /// <summary>A group at the gate from a people under a plague brings it one time in four (2.3.1); a courier the same (2.7.2).</summary>
        public const float GateCarrierChance = 1f / 4f, CourierCarrierChance = 1f / 4f;

        /// <summary>One camp sickness in five passes to one of the colony who sat at their fire (2.4.2).</summary>
        public const float CampFeverChance = 1f / 5f;

        /// <summary>The camp's fever is catching for its first three days; then whoever sat by them longest catches it.</summary>
        public const float CampFeverDays = 3f;

        /// <summary>
        /// The chance a carrier's occasion comes at all: none while the gate is shut (the world event's answer) or the
        /// quarantine decree holds for those at the gate and the couriers (strangers turned away, the goods left at the gate).
        /// </summary>
        public static float CarrierChance(CarrierKind kind, bool gateShut, bool quarantine)
        {
            switch (kind)
            {
                case CarrierKind.SickWorker: return SickWorkOnChance;
                case CarrierKind.Camp: return CampFeverChance;
                case CarrierKind.Gate: return gateShut || quarantine ? 0f : GateCarrierChance;
                case CarrierKind.Courier: return gateShut || quarantine ? 0f : CourierCarrierChance;
                default: return 0f;
            }
        }

        /// <summary>A grown-up who sat at the camp's fire gets a chest cold, or the game's flu one time in four; a child a head cold.</summary>
        public const float CampFluShare = 0.25f;

        /// <summary>The petition's words: only a petition to rest when ill or for a quarantine, and only while something catching goes round.</summary>
        public static bool SickPetition(string decree) => decree == "Council_Decree_SickBay" || decree == "Council_Decree_Quarantine";

        /// <summary>What a carrier brought shows this many days after the one who sat closest last saw them: one for a cold, two or three for a plague's.</summary>
        public static int CarrierOnsetTicks(CarrierKind kind, float roll)
        {
            float days = kind == CarrierKind.Gate || kind == CarrierKind.Courier ? 2f + HarmMath.Clamp(roll, 0f, 1f) : 1f;
            return (int)(days * DayTicks);
        }

        /// <summary>How long a carrier's waiting is kept before it lapses: the camp's fever its whole stay, the rest about a week.</summary>
        public static float CarrierKeepDays(CarrierKind kind) => kind == CarrierKind.Camp ? 14f : 7f;

        // ================= The patient who brings something worse (3.2.2) =================

        /// <summary>One outside patient in 20 brings a catching serious illness (tuberculosis, cholera).</summary>
        public const float PatientCarrierChance = 1f / 20f;

        /// <summary>Of them, tuberculosis two times in three, cholera the rest.</summary>
        public const float PatientTuberculosisShare = 2f / 3f;

        /// <summary>Hours asleep in the same room as the patient that make a night shared with them.</summary>
        public const int WardHours = 3;

        /// <summary>A night in the same room passes it on one time in four, less under the quarantine decree (its factor on spreading).</summary>
        public const float WardCatchChance = 1f / 4f;

        public static float WardCatch(int hours, float contagionFactor) => hours < WardHours ? 0f : WardCatchChance * HarmMath.Clamp(contagionFactor, 0f, 1f);

        /// <summary>Their stay is a day or so; kept three days at most.</summary>
        public const float WardKeepDays = 3f;

        // ================= The hedge healer's bad batch (2.2.4) =================

        /// <summary>One paid visit in eight the healer's batch is bad.</summary>
        public const float BadBatchChance = 1f / 8f;

        /// <summary>The colony's doctor of this much Medicine looks it over first and catches it.</summary>
        public const int BatchSpotSkill = 8;

        /// <summary>A bad batch: a few of those who took it are queasy the next morning, one in three.</summary>
        public const float BadBatchSick = 1f / 3f;

        /// <summary>Whether the batch is bad for the colony: rolled bad, and not caught by its doctor's look.</summary>
        public static bool BatchBad(bool rolledBad, int doctorMedicine) => rolledBad && doctorMedicine < BatchSpotSkill;

        /// <summary>It shows the next morning: ten to sixteen hours after the dose.</summary>
        public static int DoseOnsetTicks(float roll) => (int)((10f + 6f * HarmMath.Clamp(roll, 0f, 1f)) * HourTicks);

        /// <summary>A shot's bad batch: a sore arm where it went in (a bruise of 1 to 2), no message.</summary>
        public const float SoreArmLow = 1f, SoreArmHigh = 2f;

        /// <summary>The batch is kept a day: the healer's round is over by then.</summary>
        public const float BatchKeepDays = 1f;

        // ================= Small things that grow (3.2.3) =================

        /// <summary>Worms left untended three days, or insect bites left a day in a malaria country: one in 20 grows into the game's own.</summary>
        public const float SmallThingsChance = 1f / 20f;

        public const float WormDays = 3f, BiteDays = 1f;

        /// <summary>The look falls in the one hour the ailment turns that old, so nothing needs remembering.</summary>
        public static bool AgeWindow(int ageTicks, float days) => ageTicks >= days * DayTicks && ageTicks < days * DayTicks + HourTicks;

        /// <summary>Bites: none outside a malaria country; sleeves (clothes over the arms) half, a bed indoors (a net, a roof) half.</summary>
        public static float BitesGrow(bool malariaLand, bool sleeves, bool indoors)
        {
            if (!malariaLand) return 0f;
            return SmallThingsChance * (sleeves ? 0.5f : 1f) * (indoors ? 0.5f : 1f);
        }

        // ================= Dirty water (4.9.3, Dubs Bad Hygiene) =================

        /// <summary>
        /// A day drinking untreated water: the stomach bug one day in 600 (a food poisoning's kind of chance, once a day
        /// rather than for each drink, so it doesn't outgrow the rest).
        /// </summary>
        public const float DirtyWaterChance = 1f / 600f;

        /// <summary>A drink is a rise of the thirst need by this much within an hour.</summary>
        public const float DrinkRise = 0.15f;

        public static float DirtyWater(bool drank, bool untreated, bool modOwnIllnesses) => drank && untreated && !modOwnIllnesses ? DirtyWaterChance : 0f;

        // ================= The cold: waiting at the gate, the night watch, weather lovers, cold water =================

        /// <summary>A hard frost: the air outside at this or colder.</summary>
        public const float HardFrost = -5f;

        /// <summary>
        /// Out in the cold: outdoors in a hard frost and colder than comfortable, warm clothes counted (the game's own
        /// comfortable range, which clothes widen).
        /// </summary>
        public static bool ColdOut(bool outdoors, float outdoor, float ambient, float comfortMin) => outdoors && outdoor <= HardFrost && ambient < comfortMin;

        /// <summary>Every two hours of standing in it, one chance of frostnip (bitter cold) or chilblains: about the frostnip's own rate, doubled for standing still.</summary>
        public const int ColdRollHours = 2;

        public const float ColdStandChance = 1f / 20f;

        /// <summary>A cold hour's count brings its roll every two hours.</summary>
        public static bool ColdRoll(int coldHours) => coldHours > 0 && coldHours % ColdRollHours == 0;

        /// <summary>Waiting at the gate in the cold (3.1.2): only while the hour has passed and someone isn't ready.</summary>
        public static float GateCold(bool waitingPastTheHour, bool cold) => waitingPastTheHour && cold ? ColdStandChance : 0f;

        /// <summary>The night watch in the cold (4.2.2): two or more on the watch half it (they spell each other by a fire).</summary>
        public static float WatchCold(bool cold, int watchers) => cold ? ColdStandChance * (watchers >= 2 ? 0.5f : 1f) : 0f;

        /// <summary>A lone watcher outside the walls with a hungry predator in reach: one watch in six it comes for them.</summary>
        public const float WatchPredatorChance = 1f / 6f;

        public static float WatchPredator(int watchers, bool outsideWalls, bool predatorNear) => watchers == 1 && outsideWalls && predatorNear ? WatchPredatorChance : 0f;

        /// <summary>A predator in reach: within this many cells.</summary>
        public const float PredatorRange = 45f;

        /// <summary>A predator that would come for someone is this hungry or more (its food need at this or lower).</summary>
        public const float PredatorHungry = 0.6f;

        /// <summary>
        /// Weather lovers (3.11.3): the chest cold or frostnip a third likelier. A rain lover's day of two hours out in the
        /// rain: a chest cold one in 150 (a third of the catching clock's share for the chest cold, spread over the rainy
        /// days they stand in); a snow lover's day of three hours out in falling snow in bitter cold: frostnip one in 75 (a
        /// third of the frostnip's 0.04). A coat or a hood halves it.
        /// </summary>
        public const float RainLoverChance = 1f / 150f, SnowLoverChance = 1f / 75f;

        public const int RainLoverHours = 2, SnowLoverHours = 3;

        public static float WeatherLover(bool rain, int hours, bool covered)
        {
            if (hours < (rain ? RainLoverHours : SnowLoverHours)) return 0f;
            return (rain ? RainLoverChance : SnowLoverChance) * (covered ? 0.5f : 1f);
        }

        /// <summary>
        /// A slip into cold water (4.9.2): an hour fishing below freezing, one in 200; in a hard frost twice that. The
        /// game's own hypothermia, at its shivering stage, which goes once they're warm.
        /// </summary>
        public const float ColdWaterChance = 1f / 200f;

        public static float ColdWater(float outdoor) => outdoor >= 0f ? 0f : ColdWaterChance * (outdoor <= HardFrost ? 2f : 1f);

        /// <summary>The hypothermia of a soaking: 0.12 to 0.2 (the game's shivering).</summary>
        public const float SoakLow = 0.12f, SoakHigh = 0.2f;

        // ================= Night walks: sleepwalking, a restless walk at dusk =================

        /// <summary>Sleepwalking after a nightmare (3.12.1): one nightmare in 15 that left them shaken walks them out in their sleep.</summary>
        public const float SleepwalkChance = 1f / 15f;

        /// <summary>The night hours a sleepwalk can begin: eleven to four.</summary>
        public static bool SmallHours(int hour) => hour >= 23 || hour < 4;

        /// <summary>The walk's end: frostnip when it's freezing out and they're colder than comfortable; otherwise a stumble half the time.</summary>
        public const float StumbleShare = 0.5f;

        public const float FreezingOut = 0f;

        /// <summary>What a sleepwalk ends in: 2 frostnip, 1 a stumble, 0 nothing (led back unhurt).</summary>
        public static int SleepwalkEnds(float outdoor, bool belowComfort, float roll)
        {
            if (outdoor < FreezingOut && belowComfort) return 2;
            return roll < StumbleShare ? 1 : 0;
        }

        /// <summary>A stumble in the dark: a bruise of 1 to 3, the mildest.</summary>
        public const float StumbleLow = 1f, StumbleHigh = 3f;

        /// <summary>
        /// A restless walk at dusk (3.3.3): restlessness at its height (their strongest feeling, at this many times its
        /// threshold) walks them out alone at dusk about once a season (a quadrum of fifteen days).
        /// </summary>
        public const float RestlessHeight = 1.6f;

        public const float RestlessWalkChance = 1f / 15f;

        public static bool RestlessAtHeight(bool strongest, float intensity, float threshold) => strongest && threshold > 0f && intensity >= threshold * RestlessHeight;

        /// <summary>Dusk: the walk starts five to eight in the evening.</summary>
        public static bool Dusk(int hour) => hour >= 17 && hour <= 19;

        /// <summary>Beyond the walls at nightfall with a hungry predator in reach, one lone walk in four draws it; a friend along, never.</summary>
        public const float RestlessPredatorChance = 1f / 4f;

        public static float RestlessPredator(bool alone, bool outsideWalls, bool predatorNear) => alone && outsideWalls && predatorNear ? RestlessPredatorChance : 0f;

        /// <summary>The walk: out to the edge of the colony's ground, an hour there as night falls.</summary>
        public const int WalkTicks = 2500;

        // ================= The forecast can be wrong (4.4.2, outside the budget) =================

        /// <summary>
        /// A field sown by the weather that its forecast said would pay is frosted all the same: the air now below its crop's
        /// cold limit, when the last forecast for this hour had it at the limit and the frost margin above. Nobody is hurt;
        /// the zone's note says the forecast missed.
        /// </summary>
        public static bool ForecastMissed(float forecast, float actual, float limit, float margin) => actual < limit && forecast >= limit + Math.Max(0f, margin);

        /// <summary>The note stays on the field this long.</summary>
        public const float MissedNoteDays = 5f;

        /// <summary>A field's outlook that said it would pay is held to for a season (a quadrum): a crop sown then is still growing.</summary>
        public const float PromiseKeepDays = 15f;
    }
}
