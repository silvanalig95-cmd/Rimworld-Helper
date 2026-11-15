using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The old powers' own strange happenings (OldPowers/), in the order they were built.</summary>
    public enum OldPower
    {
        /// <summary>A seer at the turn of the season reads the one coming, true in every word.</summary>
        Seer,
        /// <summary>At midnight everything on the land turns and faces one way, the shadows running out that way.</summary>
        OneWay,
        /// <summary>A stone of the colony's hums from dusk to dawn, under a storm or a psychic sky.</summary>
        Stone,
        /// <summary>A sealed scroll found in a room of the old ruins.</summary>
        Scroll,
        /// <summary>A sky full of power: the colony's casters burn bright, their mana or psyfocus filled.</summary>
        Charged,
        /// <summary>A wandering enchanter who will work one piece of the colony's up a grade, for silver.</summary>
        Enchanter,
        /// <summary>Two nights of knocking under the ground, and on the third morning something comes up.</summary>
        Buried,
        /// <summary>The dead walking a road across the land at dusk: a sight, or with Anomaly and the harm budget, shamblers.</summary>
        DeadRoad,
        /// <summary>Pilgrims passing through, who stop at the colony's oldest place to pray and leave a token.</summary>
        Pilgrims,
        /// <summary>A night the sky is another sky.</summary>
        OtherSky,
        /// <summary>A wild beast walks up to someone at dusk and speaks one true sentence.</summary>
        Beast,
        /// <summary>A god drawn near goes by in the night: its own signs one after another, and at dawn nothing more.</summary>
        GodPasses
    }

    /// <summary>
    /// The rules of the old powers' happenings (the player, 13 and 14 November 2026: "i like the old powers storyteller and
    /// want you to do some more more events in this style, including the gods and using ressources from rimworld of magic",
    /// then "do ... more old powers style events"), with no game types so Tools/Tests reads them. They keep the gods' pace
    /// (GodPaceMath): each its own gap, a chance that starts small after it and grows over its ramp, nothing on a condition
    /// that is nearly always true, and none within <see cref="SpacingDays"/> of another. How often they come is the ways'
    /// lever <see cref="StoryWaysMath.Uncanny"/>: three times as often under Old Powers or its story direction, as they are
    /// under any other storyteller.
    /// </summary>
    public static class OldPowersMath
    {
        public static readonly OldPower[] All = (OldPower[])Enum.GetValues(typeof(OldPower));

        /// <summary>Its short name ("seer", "oneway"), for the memory of the last ones and the lines' tags.</summary>
        public static string Name(OldPower e) => e.ToString().ToLowerInvariant();

        // ================= The pace =================

        /// <summary>None within this many days of another, at any lever: fewer, and stranger.</summary>
        public const float SpacingDays = 5f;

        /// <summary>
        /// A happening's pace at the lever's 1: never within <c>gap</c> days of its last; about every <c>mean</c> days of its
        /// call holding once it is free; its chance growing over <c>ramp</c> days after the gap.
        /// </summary>
        public static (float gap, float mean, float ramp) PaceOf(OldPower e)
        {
            switch (e)
            {
                case OldPower.Seer: return (40f, 6f, 30f);
                case OldPower.OneWay: return (30f, 5f, 20f);
                case OldPower.Stone: return (30f, 2.5f, 20f);
                case OldPower.Scroll: return (40f, 1.5f, 30f);
                case OldPower.Charged: return (30f, 1.5f, 20f);
                case OldPower.Enchanter: return (45f, 6f, 30f);
                case OldPower.Buried: return (40f, 5f, 25f);
                case OldPower.DeadRoad: return (60f, 3f, 30f);
                case OldPower.Pilgrims: return (40f, 6f, 25f);
                case OldPower.OtherSky: return (30f, 4f, 20f);
                case OldPower.Beast: return (35f, 4f, 20f);
                case OldPower.GodPasses: return (40f, 5f, 30f);
                default: return (40f, 6f, 30f);
            }
        }

        /// <summary>
        /// The hours its call is looked at (from, to; to past midnight when below from): the seer and the pilgrims in the
        /// morning, the enchanter by day, the beast and the dead at dusk, the rest in the night.
        /// </summary>
        public static (int from, int to) HoursOf(OldPower e)
        {
            switch (e)
            {
                case OldPower.Seer: return (9, 14);
                case OldPower.OneWay: return (23, 1);
                case OldPower.Stone: return (18, 23);
                case OldPower.Scroll: return (6, 20);
                case OldPower.Charged: return (21, 3);
                case OldPower.Enchanter: return (9, 14);
                case OldPower.Buried: return (22, 2);
                case OldPower.DeadRoad: return (17, 19);
                case OldPower.Pilgrims: return (8, 12);
                case OldPower.OtherSky: return (22, 1);
                case OldPower.Beast: return (18, 22);
                case OldPower.GodPasses: return (20, 22);
                default: return (0, 24);
            }
        }

        /// <summary>The hour is one its call is looked at.</summary>
        public static bool InHours(OldPower e, int hour)
        {
            var (from, to) = HoursOf(e);
            int h = ((hour % 24) + 24) % 24;
            return from <= to ? h >= from && h < to : h >= from || h < to;
        }

        /// <summary>How many hours a day its call is looked at.</summary>
        public static int WindowHours(OldPower e)
        {
            var (from, to) = HoursOf(e);
            int n = from <= to ? to - from : 24 - from + to;
            return Math.Max(1, n);
        }

        /// <summary>The ways' lever, made safe: a quarter at least, three times at most.</summary>
        public static float Lever(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 1f : Math.Max(StoryWaysMath.Floor, Math.Min(StoryWaysMath.MaxLever, v));

        /// <summary>
        /// A day's chance of a happening whose call holds: none within its gap or without a reason, its share growing after
        /// the gap (GodPaceMath.DayChance), more for a strong reason, and as many times likelier as the lever says.
        /// </summary>
        public static float DayChance(OldPower e, float daysSince, GodPaceMath.Trigger why, float lever)
        {
            var p = PaceOf(e);
            float w = GodPaceMath.Weight(why);
            if (p.mean <= 0f || w <= 0f) return 0f;
            return Clamp01(GodPaceMath.DayChance(daysSince, p.gap, p.mean / Lever(lever), p.ramp) * w);
        }

        /// <summary>The same shared out over the hours its call is looked at in a day (one look an hour).</summary>
        public static float CheckChance(OldPower e, float daysSince, GodPaceMath.Trigger why, float lever) =>
            GodPaceMath.PerCheck(DayChance(e, daysSince, why, lever), WindowHours(e));

        /// <summary>Another may come: the last of any is far enough back.</summary>
        public static bool Spaced(float daysSinceAny) => float.IsNaN(daysSinceAny) || daysSinceAny >= SpacingDays;

        /// <summary>
        /// The share of a day's looks at which its call holds in an ordinary colony, for the tests' and the balance's model
        /// only (a season's last two days for the seer, a storm for the stone, someone in the old ruins for the scroll ...).
        /// </summary>
        public static float AssumedShare(OldPower e)
        {
            switch (e)
            {
                case OldPower.Seer: return 2f / 15f;
                case OldPower.OneWay: return 0.10f;
                case OldPower.Stone: return 0.06f;
                case OldPower.Scroll: return 0.04f;
                case OldPower.Charged: return 0.05f;
                case OldPower.Enchanter: return 0.15f;
                case OldPower.Buried: return 0.10f;
                case OldPower.DeadRoad: return 0.06f;
                case OldPower.Pilgrims: return 0.10f;
                case OldPower.OtherSky: return 0.08f;
                case OldPower.Beast: return 0.10f;
                case OldPower.GodPasses: return 0.08f;
                default: return 0.1f;
            }
        }

        // ================= The colony's age =================

        /// <summary>The colony's age as the words take it: 0 tribal, 1 medieval, 2 industrial, 3 spacer.</summary>
        public static int Age(int techLevel) => techLevel <= 2 ? 0 : techLevel == 3 ? 1 : techLevel == 4 ? 2 : 3;

        // ================= The seer of the season =================

        /// <summary>What the seer may say of the season coming, each word true.</summary>
        public enum SeerWord
        {
            Frost,
            Cold,
            Mild,
            Warm,
            Heat,
            Grows,
            Bare,
            StoresShort,
            StoresEnough,
            Blades
        }

        /// <summary>The seer comes in the season's last two days; a strong reason when the next is hard (a frost or a heat coming on).</summary>
        public static GodPaceMath.Trigger SeerTrigger(int dayOfSeason, bool hardAhead)
        {
            if (dayOfSeason < 13 || dayOfSeason > 14) return GodPaceMath.Trigger.None;
            return hardAhead ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>A hard season ahead: the next's mean below freezing while it is above now, or above the heat's line.</summary>
        public static bool HardAhead(float nowTemp, float nextMean) =>
            !float.IsNaN(nowTemp) && !float.IsNaN(nextMean) && (nextMean < 0f && nowTemp > 0f || nextMean >= HeatFrom);

        /// <summary>The three twelfths of the season after the one <paramref name="twelfth"/> is in (0 to 11).</summary>
        public static int[] NextSeasonTwelfths(int twelfth)
        {
            int t = ((twelfth % 12) + 12) % 12;
            int start = (t / 3 + 1) * 3 % 12;
            return new[] { start, (start + 1) % 12, (start + 2) % 12 };
        }

        /// <summary>The lines of the temperature words (°C): below freezing a frost, below ten cold, below twenty mild, below thirty-two warm, else heat.</summary>
        public const float ColdFrom = 10f, MildFrom = 20f, HeatFrom = 32f;

        /// <summary>Crops grow in a season whose mean lies between these (°C), as the game's plants about do.</summary>
        public const float GrowsFrom = 6f, GrowsTo = 42f;

        /// <summary>Days of food under which the seer says the stores won't last a bare season; above this, that they will.</summary>
        public const float ShortBelow = 15f, EnoughFrom = 40f;

        /// <summary>
        /// The seer's words for the season coming: how warm it will be, whether the fields will grow, the stores where a bare
        /// season would empty them (or where they'll last), and blades where a fight is coming; three at most, all true.
        /// </summary>
        public static List<SeerWord> SeerWords(float nextMean, float foodDays, bool fightAhead)
        {
            var list = new List<SeerWord>();
            if (float.IsNaN(nextMean)) return list;
            list.Add(nextMean < 0f ? SeerWord.Frost : nextMean < ColdFrom ? SeerWord.Cold : nextMean < MildFrom ? SeerWord.Mild : nextMean < HeatFrom ? SeerWord.Warm : SeerWord.Heat);
            bool grows = nextMean >= GrowsFrom && nextMean <= GrowsTo;
            list.Add(grows ? SeerWord.Grows : SeerWord.Bare);
            if (fightAhead) list.Add(SeerWord.Blades);
            else if (!grows && !float.IsNaN(foodDays) && foodDays < ShortBelow) list.Add(SeerWord.StoresShort);
            else if (!float.IsNaN(foodDays) && foodDays >= EnoughFrom) list.Add(SeerWord.StoresEnough);
            return list;
        }

        // ================= The night everything faced one way =================

        /// <summary>Hours after a death of the colony in which everything faces the dead.</summary>
        public const float DeadFacedHours = 30f;

        /// <summary>A strong reason the night after one of the colony died (they face the dead); an ordinary one on a clear night with no moon.</summary>
        public static GodPaceMath.Trigger OneWayTrigger(float hoursSinceDeath, bool moonless, bool clear)
        {
            if (!float.IsNaN(hoursSinceDeath) && hoursSinceDeath >= 0f && hoursSinceDeath <= DeadFacedHours) return GodPaceMath.Trigger.Strong;
            return moonless && clear ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>How long everything stands facing that way (ticks: ten seconds at the game's speed).</summary>
        public const int FacedTicks = 600;

        /// <summary>The way a line runs, as a point of the compass (0 north, then clockwise by eighths; z grows north).</summary>
        public static int Compass(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return 0;
            double a = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (a < 0) a += 360.0;
            return (int)Math.Round(a / 45.0) % 8;
        }

        /// <summary>The compass's words' keys' ends, from north clockwise.</summary>
        public static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // ================= The humming stone =================

        /// <summary>The stone hums on a psychic sky (a strong reason) or under a thunderstorm.</summary>
        public static GodPaceMath.Trigger StoneTrigger(bool psychicSky, bool storm) =>
            psychicSky ? GodPaceMath.Trigger.Strong : storm ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The hum's turns (ticks), how far its dream reaches a sleeper (cells), and the hour it stops.</summary>
        public const int HumEvery = 360;
        public const float DreamReach = 14f;
        public const int StoneStopsAt = 5;

        // ================= The sealed scroll =================

        /// <summary>A room of the old ruins nobody of the colony has searched before, with one of them in it.</summary>
        public static GodPaceMath.Trigger ScrollTrigger(bool inRuin, bool searchedBefore) =>
            inRuin && !searchedBefore ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>Rooms of the old ruins remembered as searched (the oldest let go first).</summary>
        public const int RuinsKept = 20;

        // ================= The casters burning bright =================

        /// <summary>What someone of the colony is to a sky full of power.</summary>
        public enum Caster
        {
            None,
            Mage,
            Psycaster,
            Sensitive
        }

        /// <summary>Psychic sensitivity from which someone with no craft feels it (the game's own stat).</summary>
        public const float SensitiveFrom = 1.4f;

        /// <summary>A mage of A RimWorld of Magic first, a psycaster, else one sensitive enough.</summary>
        public static Caster CasterOf(bool mage, bool psylink, float sensitivity)
        {
            if (mage) return Caster.Mage;
            if (psylink) return Caster.Psycaster;
            return !float.IsNaN(sensitivity) && sensitivity >= SensitiveFrom ? Caster.Sensitive : Caster.None;
        }

        /// <summary>A mana storm or surge, or a psychic bloom (a strong reason), or an aurora; never with nobody to feel it.</summary>
        public static GodPaceMath.Trigger ChargedTrigger(bool manaSky, bool aurora, int casters)
        {
            if (casters <= 0) return GodPaceMath.Trigger.None;
            return manaSky ? GodPaceMath.Trigger.Strong : aurora ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The psyfocus a psycaster gains (a share of the whole).</summary>
        public const float PsyfocusGain = 0.5f;

        /// <summary>The chance, at the shared budget's rate, that one of them is burned by the light running out of their hands; the burn's size.</summary>
        public const float BurnChance = 0.12f, BurnLow = 1f, BurnHigh = 3f;

        // ================= The wandering enchanter =================

        /// <summary>
        /// The enchanter comes in a season's first three days (the roads open), never in winter, only with a piece to work and
        /// the silver for it; a strong reason with a caster in the colony (magic draws them).
        /// </summary>
        public static GodPaceMath.Trigger EnchanterTrigger(int dayOfSeason, bool winter, bool caster, bool piece, bool canPay)
        {
            if (winter || !piece || !canPay || dayOfSeason < 0 || dayOfSeason > 2) return GodPaceMath.Trigger.None;
            return caster ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>A piece the enchanter will take up a grade: awful (0) to excellent (4), never a masterwork or a legend.</summary>
        public static bool Enchantable(int quality) => quality >= 0 && quality <= 4;

        /// <summary>The enchanter's price to take a piece of this grade (0 awful to 4 excellent) up one, in silver.</summary>
        public static int EnchantPrice(int quality)
        {
            switch (quality)
            {
                case 0: return 60;
                case 1: return 100;
                case 2: return 180;
                case 3: return 320;
                case 4: return 600;
                default: return 0;
            }
        }

        /// <summary>The work's length (ticks: about an hour), and how long the colony has to answer (hours).</summary>
        public const int EnchantTicks = 2500;
        public const float AnswerHours = 6f;

        // ================= The thing under the ground =================

        /// <summary>A long wet run (a strong reason), a shorter one, or the thaw (spring's first days).</summary>
        public static GodPaceMath.Trigger BuriedTrigger(int wetRun, bool thaw)
        {
            if (wetRun >= 6) return GodPaceMath.Trigger.Strong;
            return wetRun >= 4 || thaw ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>What comes up on the third morning.</summary>
        public enum BuriedFind
        {
            /// <summary>An old cache: a little silver and an old worn thing of the colony's age.</summary>
            Cache,
            /// <summary>An old precious thing of the colony's age (gold, jade, the mods' own).</summary>
            Relic,
            /// <summary>A RimWorld of Magic's unrefined magicyte, with a minor arcane gem now and then.</summary>
            Magicyte,
            /// <summary>With Anomaly and through the shared harm budget: the dead crawl up out of it.</summary>
            Waker
        }

        /// <summary>The find's weights: a cache and a relic always, magicyte with the mod, the waking dead with Anomaly and harm on.</summary>
        public static BuriedFind PickFind(bool rom, bool waker, float roll)
        {
            float cache = 3f, relic = 2f, magic = rom ? 2f : 0f, wake = waker ? 1.5f : 0f;
            float x = Clamp01(float.IsNaN(roll) ? 0f : roll) * (cache + relic + magic + wake);
            if (x < cache) return BuriedFind.Cache;
            x -= cache;
            if (x < relic) return BuriedFind.Relic;
            x -= relic;
            if (x < magic) return BuriedFind.Magicyte;
            return wake > 0f ? BuriedFind.Waker : BuriedFind.Cache;
        }

        /// <summary>The waking dead's chance at the shared budget's rate once they are picked (else an old cache comes up), and how many.</summary>
        public const float WakerChance = 0.5f;
        public const int WakersMin = 1, WakersMax = 2;

        // ================= The dead on the road =================

        /// <summary>Hours after a death of the colony in which the dead may walk at dusk.</summary>
        public const float DeadWalkHours = 72f;

        /// <summary>A strong reason after a death of the colony; an ordinary one on a night with no moon while the void is awake or the dead lie unburied.</summary>
        public static GodPaceMath.Trigger DeadRoadTrigger(float hoursSinceDeath, bool moonless, bool voidAwake, bool unburied)
        {
            if (!float.IsNaN(hoursSinceDeath) && hoursSinceDeath >= 0f && hoursSinceDeath <= DeadWalkHours) return GodPaceMath.Trigger.Strong;
            return moonless && (voidAwake || unburied) ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The chance, at the shared budget's rate, that the dead who walk are the game's shamblers (with Anomaly); else they are a sight that passes.</summary>
        public const float DeadWalkChance = 0.5f;

        /// <summary>The shamblers' strength: a small share of the storyteller's threat points, within a small band.</summary>
        public static float DeadRoadPoints(float threatPoints) => Math.Max(60f, Math.Min(220f, (float.IsNaN(threatPoints) ? 0f : threatPoints) * 0.15f));

        /// <summary>The sight's steps along the road, and the ticks between them.</summary>
        public const int RoadSteps = 28, RoadStepTicks = 140;

        // ================= The pilgrims passing =================

        /// <summary>A sign in the sky now (a strong reason), or the first three days of spring or autumn (the pilgrims' roads).</summary>
        public static GodPaceMath.Trigger PilgrimsTrigger(int dayOfSeason, int quarter, bool signInSky)
        {
            if (signInSky) return GodPaceMath.Trigger.Strong;
            return dayOfSeason >= 0 && dayOfSeason <= 2 && (quarter == StoryWaysMath.Spring || quarter == StoryWaysMath.Autumn) ? GodPaceMath.Trigger.Ordinary
                : GodPaceMath.Trigger.None;
        }

        /// <summary>How many pilgrims walk together (three to five), and the hours they stay.</summary>
        public static int PilgrimCount(float roll) => 3 + (int)Math.Floor(Clamp01(float.IsNaN(roll) ? 0f : roll) * 2.999f);
        public const float PilgrimHours = 7f;

        // ================= Another sky =================

        /// <summary>An aurora on a clear night (a strong reason), or a clear night in the deep of summer or winter (days seven to nine).</summary>
        public static GodPaceMath.Trigger OtherSkyTrigger(bool aurora, int quarter, int dayOfSeason, bool clear)
        {
            if (!clear) return GodPaceMath.Trigger.None;
            if (aurora) return GodPaceMath.Trigger.Strong;
            return (quarter == StoryWaysMath.Summer || quarter == StoryWaysMath.Winter) && dayOfSeason >= 6 && dayOfSeason <= 8 ? GodPaceMath.Trigger.Ordinary
                : GodPaceMath.Trigger.None;
        }

        /// <summary>The other sky lasts till this hour, fading in and out over this many ticks.</summary>
        public const int OtherSkyEndsAt = 5, OtherSkyFade = 1500;

        // ================= The beast that spoke =================

        /// <summary>Wild beasts the colony killed lately from which a beast comes to speak for them (a strong reason); else at a full moon.</summary>
        public const int HuntedFrom = 4;
        public const float HuntedDays = 5f;

        public static GodPaceMath.Trigger BeastTrigger(int huntedLately, bool fullMoon)
        {
            if (huntedLately >= HuntedFrom) return GodPaceMath.Trigger.Strong;
            return fullMoon ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The one true thing the beast says.</summary>
        public enum BeastWord
        {
            Hunted,
            Coming,
            Sick,
            Hungry,
            Tired,
            Sad,
            Watched
        }

        /// <summary>
        /// The beast's word, the first that is true: the beasts the colony killed, a fight coming, the one it speaks to sick,
        /// hungry, worn out or low; else that the old ones are watching.
        /// </summary>
        public static BeastWord BeastWordOf(int hunted, bool fightAhead, bool sick, float food, float rest, float mood)
        {
            if (hunted >= HuntedFrom) return BeastWord.Hunted;
            if (fightAhead) return BeastWord.Coming;
            if (sick) return BeastWord.Sick;
            if (!float.IsNaN(food) && food < 0.25f) return BeastWord.Hungry;
            if (!float.IsNaN(rest) && rest < 0.2f) return BeastWord.Tired;
            if (!float.IsNaN(mood) && mood < 0.3f) return BeastWord.Sad;
            return BeastWord.Watched;
        }

        /// <summary>How near a colonist a wild beast must be to walk up to them (cells).</summary>
        public const float BeastReach = 35f;

        // ================= A god going by =================

        /// <summary>
        /// A god of the colony drawn near goes by on a night: never while one of the gods is coming or a presence is on the
        /// land; a strong reason drawn very near, an ordinary one drawn near on a night with no moon.
        /// </summary>
        public static GodPaceMath.Trigger GodPassesTrigger(float near, bool moonless, bool busy)
        {
            if (busy || float.IsNaN(near)) return GodPaceMath.Trigger.None;
            if (near >= 0.7f) return GodPaceMath.Trigger.Strong;
            return near >= 0.5f && moonless ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>Its signs in the night, at most, and the hours they come at (from ten at night, two hours apart).</summary>
        public const int PassSignsAtMost = 4;

        public static int PassSignAt(int i) => (22 + 2 * Math.Max(0, i)) % 24;

        /// <summary>The morning card comes at this hour.</summary>
        public const int PassMorning = 6;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
