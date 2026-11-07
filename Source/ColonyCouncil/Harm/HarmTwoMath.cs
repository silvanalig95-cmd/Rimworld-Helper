using System;

namespace ColonyCouncil
{
    /// <summary>The kind of hardship a town lies under that a traveller may carry home (Harm/SickHome.cs).</summary>
    public enum SickTown
    {
        None,
        /// <summary>Hunger and drought: the sickness that follows short rations and bad water.</summary>
        Famine,
        /// <summary>A plague of its people's, or within reach of it.</summary>
        Plague
    }

    /// <summary>
    /// The rates and small rules of harm, parts 2 and 3 (docs/harm-review-proposals.md: 2.4.1, 3.1.1, 2.2.1, 2.1.1, 3.5.1,
    /// 1.4.1, 3.3.1), with no game types so Tools/Tests and Tools/Balance can read them. Every chance here is a base rate:
    /// the game's side multiplies it by Harm.Scale (the setting and the storyteller's lean) and gives the hurt through the
    /// harm service, under its cap (Harm/Harm.cs, HarmMath.cs).
    /// </summary>
    public static class HarmTwoMath
    {
        public const int DayTicks = HarmMath.DayTicks, HourTicks = HarmMath.HourTicks;

        // ================= A camp's quarrel that becomes a brawl (2.4.1) =================

        /// <summary>Tension at which a camp's quarrel can come to blows (the council's second warning).</summary>
        public const float BrawlTension = 70f;

        /// <summary>After the old quarrel with another people answered badly, its next three evenings can come to blows too.</summary>
        public const float SourQuarrelDays = 3f;

        /// <summary>The chance each evening at their fire while tension holds: about once in a camp's stay once it runs high.</summary>
        public const float CampBrawlEvening = 0.25f;

        /// <summary>The evening at their fire, from six to ten.</summary>
        public const int EveningFrom = 18, EveningTo = 22;

        public static bool Evening(int hour) => hour >= EveningFrom && hour < EveningTo;

        /// <summary>The quarrel can come to blows now: tension high, or the old quarrel answered badly in the last three days.</summary>
        public static bool Quarrelling(float tension, int souredAt, int now) =>
            tension >= BrawlTension || souredAt >= 0 && now >= souredAt && now - souredAt < SourQuarrelDays * DayTicks;

        /// <summary>
        /// An evening's chance of the brawl: a quarter, half with the colony's peacemaker at home, times a frayed temper
        /// among the colony's own at the fire (FrayedFactor, 1 for none).
        /// </summary>
        public static float CampBrawlChance(bool peacemaker, float frayed) => CampBrawlEvening * (peacemaker ? 0.5f : 1f) * Math.Max(1f, Math.Min(MaxFrayed, frayed));

        /// <summary>A few on each side join: at most two more pairs besides the first.</summary>
        public const int BrawlJoinPairs = 2;

        /// <summary>How many more pairs join: the fewer of the two sides' free hands, at most two.</summary>
        public static int JoinPairs(int oursFree, int theirsFree) => Math.Max(0, Math.Min(BrawlJoinPairs, Math.Min(oursFree, theirsFree)));

        /// <summary>Their people remember a brawl with the colony as a fistfight: goodwill lost once, never so much as to turn them hostile.</summary>
        public const int CampBrawlGoodwill = -6;

        /// <summary>A brawl watched for this long at most before it's over, whatever the fights say.</summary>
        public const int BrawlMaxTicks = 3 * HourTicks;

        // ================= Sickness brought home (3.1.1) =================

        /// <summary>About one journey in six to a sick town brings it home, for a night's stay there.</summary>
        public const float SickHomeChance = 1f / 6f;

        /// <summary>Hours stopped at the town before it counts as lodging there (a quick trade at the gate doesn't).</summary>
        public const float LodgedHours = 2f;

        /// <summary>A night's stay: the full chance from this many hours on.</summary>
        public const float NightHours = 8f;

        /// <summary>
        /// The chance a journey brings the town's sickness home: one in six for a night's stay, less for a shorter one,
        /// nothing under two hours; half with medicine packed, half with industrial or spacer medicine, half again if
        /// the quarantine decree holds for the returned; a famine's sickness half a plague's.
        /// </summary>
        public static float SickHome(SickTown kind, float hours, bool medicine, bool modern, bool quarantine)
        {
            if (kind == SickTown.None || hours < LodgedHours) return 0f;
            float p = SickHomeChance * Math.Min(1f, hours / NightHours);
            if (medicine) p *= 0.5f;
            if (modern) p *= 0.5f;
            if (quarantine) p *= 0.5f;
            if (kind == SickTown.Famine) p *= 0.5f;
            return p;
        }

        /// <summary>The sickness shows two to four days after the homecoming.</summary>
        public const float OnsetDaysMin = 2f, OnsetDaysMax = 4f;

        public static int OnsetTicks(float roll) => (int)((OnsetDaysMin + (OnsetDaysMax - OnsetDaysMin) * HarmMath.Clamp(roll, 0f, 1f)) * DayTicks);

        /// <summary>An onset the cap holds back (a sickbed, a hurt lately) waits up to three days, then passes.</summary>
        public const float OnsetGraceDays = 3f;

        public static bool OnsetLapsed(int due, int now) => now - due > OnsetGraceDays * DayTicks;

        /// <summary>A plague's sickness is the game's plague one time in four, its flu otherwise.</summary>
        public const float PlagueShare = 0.25f;

        /// <summary>A stay remembered this long after the caravan left the town (the road home can be long).</summary>
        public const float StayKeepDays = 30f;

        public static bool StayKept(int lastAt, int now) => now - lastAt < StayKeepDays * DayTicks;

        /// <summary>The worse of two: a plague over a famine.</summary>
        public static SickTown Worse(SickTown a, SickTown b) => (int)a >= (int)b ? a : b;

        /// <summary>A world event's kind as a town's sickness ("sickness" a plague, "hunger" and "drought" a famine), unless it is in the herds.</summary>
        public static SickTown KindOf(string kind, bool herds)
        {
            if (herds) return SickTown.None;
            int h = TownLifeKind(kind);
            return h == 2 ? SickTown.Plague : h == 1 ? SickTown.Famine : SickTown.None;
        }

        /// <summary>The same reading of a world event's kind as the town's own hardships (TownLifeMath.HardshipKind).</summary>
        private static int TownLifeKind(string kind) => kind == "sickness" ? 2 : kind == "hunger" || kind == "drought" ? 1 : 0;

        /// <summary>A hardship reaches a town when it was its own people's, or within twenty tiles (TownLifeMath.HardshipTiles).</summary>
        public const float ReachTiles = 20f;

        public static bool Reaches(bool theirPeople, float tiles) => theirPeople || tiles >= 0f && tiles <= ReachTiles;

        // ================= The champion's bout (2.2.1) =================

        /// <summary>A champion's bout uses the sparring rule: one bout in 25 lands a real blow.</summary>
        public const float ChampionBlowChance = 1f / 25f;

        /// <summary>The bout the champion's people know (their ways): wrestling and sticks, staves or blunted swords, boxing, a stun-baton exhibition.</summary>
        public static string BoutForm(WorldAge theirs)
        {
            switch (theirs)
            {
                case WorldAge.Tribal: return "Tribal";
                case WorldAge.Medieval: return "Medieval";
                case WorldAge.Industrial: return "Industrial";
                default: return "Spacer";
            }
        }

        // ================= Hunts that can bite back (2.1.1) =================

        /// <summary>Game the hunts always took: a revenge chance at or under this, by the kind and by the storyteller.</summary>
        public const float SafeRevenge = 0.1f;

        /// <summary>The biggest kind of game a hunt goes after (PlayHunt.MaxGameSize).</summary>
        public const float MaxGameSize = 2.5f;

        /// <summary>The game's own reach for a turned animal's packmates (Pawn_MindState.GetPackmates).</summary>
        public const float PackRadius = 24f;

        /// <summary>At most one head in a hunt may be game that can turn.</summary>
        public const int MaxTurningPerHunt = 1;

        /// <summary>
        /// Game that can turn on its hunters and that a hunt may take now: never a predator, an insect or a dryad, nothing
        /// bigger than a muffalo or an elk, a revenge chance above the safe game's, and alone (no packmate within the
        /// game's own reach), so a turn is that one animal.
        /// </summary>
        public static bool MayTurn(bool predator, bool insect, bool dryad, float size, float revenge, bool lone) =>
            !predator && !insect && !dryad && size <= MaxGameSize && revenge > SafeRevenge && lone;

        /// <summary>The game's own revenge chance for a marked head, times the harm scale; nothing for a hunter the cap keeps from a hurt.</summary>
        public static float TurnChance(float gameChance, float scale, bool mayBeHurt) => mayBeHurt ? HarmMath.Clamp(gameChance * Math.Max(0f, scale), 0f, 1f) : 0f;

        /// <summary>A marked head is remembered this long (the hunt done or not).</summary>
        public const float TurningKeepDays = 6f;

        public static bool TurningKept(int markedAt, int now) => markedAt <= now && now - markedAt < TurningKeepDays * DayTicks;

        // ================= A rival's sabotage, found out (3.5.1) =================

        /// <summary>A bitter rivalry: the game's tie of rivals at this strength or more.</summary>
        public const float BitterRivalry = 50f;

        /// <summary>A day's chance for a bitter rival of a petty or abrasive nature: about twice a 60-day year while their work is shared.</summary>
        public const float SabotageDaily = 1f / 30f;

        /// <summary>
        /// The day's chance: nothing short of a bitter rivalry or without a petty or abrasive nature; half with their work
        /// kept apart (the saboteur not at the other's craft), half again with the colony's peacemaker in office.
        /// </summary>
        public static float SabotageChance(float rivalry, bool petty, bool sameWork, bool peacemaker)
        {
            if (rivalry < BitterRivalry || !petty) return 0f;
            return SabotageDaily * (sameWork ? 1f : 0.5f) * (peacemaker ? 0.5f : 1f);
        }

        /// <summary>With nobody to see it, found out half the time, by word going round.</summary>
        public const float FoundOutChance = 0.5f;

        /// <summary>Found out: always when someone saw it done, half the time when nobody did.</summary>
        public static float FoundOut(bool witness) => witness ? 1f : FoundOutChance;

        /// <summary>By day, the other well away from their bench.</summary>
        public const int SabotageFrom = 8, SabotageTo = 20;

        public static bool SabotageHour(int hour) => hour >= SabotageFrom && hour < SabotageTo;

        /// <summary>The other this far from the bench at least, so they don't see it done.</summary>
        public const float AwayFromBench = 15f;

        /// <summary>A witness: within this many cells of the bench, with a clear line to it.</summary>
        public const float WitnessRange = 12f;

        /// <summary>A bench without a machine to jam takes a quarter of its hit points, never below a third of them.</summary>
        public const float BenchDamage = 0.25f, BenchFloor = 1f / 3f;

        public static int BenchHitPoints(int hp, int max)
        {
            if (max <= 0) return hp;
            int floor = (int)Math.Ceiling(max * BenchFloor);
            return Math.Max(Math.Min(hp, floor), hp - (int)Math.Round(max * BenchDamage));
        }

        /// <summary>
        /// What it is by the colony's means and what was done: a charm taken (tribal), a tool broken (medieval, and a bench
        /// with no machine to jam in a later age), a machine jammed (industrial), a program tampered with (spacer).
        /// </summary>
        public static string SabotageForm(WorldAge ours, bool jammed)
        {
            if (jammed) return ours == WorldAge.Spacer ? "Spacer" : "Industrial";
            return ours == WorldAge.Tribal ? "Tribal" : "Medieval";
        }

        /// <summary>The intent lasts a day: the act waits for an hour the bench is free and the other away.</summary>
        public const float IntentDays = 1f;

        /// <summary>Word going round reaches the other within this many hours when nobody saw it.</summary>
        public const float GossipHours = 12f;

        /// <summary>The rivalry it leaves, found out or not once the other knows.</summary>
        public const float SabotageRivalry = 8f;

        // ================= Hustings that come to blows (1.4.1) =================

        /// <summary>One hustings in eight with rivals in the crowd backing different candidates.</summary>
        public const float HustingsBrawlChance = 1f / 8f;

        /// <summary>The hustings' chance: half with the peacemaker there, half under the best-behavior decree, half with calm candidates.</summary>
        public static float HustingsBrawl(bool peacemaker, bool manners, bool calm, float frayed) =>
            HustingsBrawlChance * (peacemaker ? 0.5f : 1f) * (manners ? 0.5f : 1f) * (calm ? 0.5f : 1f) * Math.Max(1f, Math.Min(MaxFrayed, frayed));

        // ================= Frayed tempers (3.3.1) =================

        /// <summary>The most a frayed temper makes a fight likelier: a beer's (DrinkBeforeWorkMath.MaxFightFactor).</summary>
        public const float MaxFrayed = 1.5f;

        /// <summary>
        /// How much likelier a sharp word comes to blows by the heart's strain: worn down a quarter more, near breaking half
        /// again (a beer's), each times the harm scale up to the usual; nothing below worn down.
        /// </summary>
        public static float FrayedFactor(StrainBand band, float scale)
        {
            float s = HarmMath.Clamp(scale, 0f, 1f);
            switch (band)
            {
                case StrainBand.Worn: return 1f + 0.25f * s;
                case StrainBand.Breaking: return 1f + 0.5f * s;
                default: return 1f;
            }
        }

        /// <summary>The more frayed of the two sets the moment's factor (not the two together).</summary>
        public static float Frayed(float a, float b) => Math.Min(MaxFrayed, Math.Max(1f, Math.Max(a, b)));

        /// <summary>
        /// One roll of a moment's fight chance with a frayed temper: a fight under the chance times the factor (never past
        /// certainty); <paramref name="frayed"/> when only the factor made it one (the part that goes through the cap).
        /// </summary>
        public static bool FrayedRoll(float roll, float baseChance, float factor, out bool frayed)
        {
            float b = HarmMath.Clamp(baseChance, 0f, 1f);
            float f = HarmMath.Clamp(b * Math.Max(1f, Math.Min(MaxFrayed, factor)), 0f, 1f);
            bool fight = roll < f;
            frayed = fight && roll >= b;
            return fight;
        }
    }
}
