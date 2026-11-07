using System;

namespace ColonyCouncil
{
    /// <summary>What a scheme against the colony does on the map (Diplomacy/Schemes.cs).</summary>
    public enum SchemeAct
    {
        /// <summary>A machine broken down in the night (industrial and after): the game's own breakdown.</summary>
        Machine,
        /// <summary>A small valuable taken from a stockpile in the night: a totem, a purse, a tool.</summary>
        Theft,
        /// <summary>A colonist with a hot temper set against another by a word in their ear, by day: the game's social fight.</summary>
        Fight,
        /// <summary>The colony's rooms gone through in the night, for ill words to carry round the region.</summary>
        Smear
    }

    /// <summary>Who caught one of theirs at it: nobody, a guard on duty, a colonist awake, a dog.</summary>
    public enum SchemeWatch
    {
        None,
        Guard,
        Colonist,
        Dog
    }

    /// <summary>
    /// Schemes against the colony (docs/design/stage2-diplomacy.md, item 11, theirs only: the player's decision 16; release
    /// 2c.3), with no game types so Tools/Tests and Tools/Balance can read them: who schemes, how often, what they do by the
    /// colony's means and their people's ways, who catches them at it, and what it costs. Every scheme is a Scheme tie in
    /// the world ledger (origin <see cref="Origin"/>, from the schemer to the colony), unknown until it is found out.
    /// </summary>
    public static class SchemeMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        /// <summary>A season: fifteen days.</summary>
        public const int SeasonDays = 15;

        public const int SeasonTicks = SeasonDays * DayTicks;

        /// <summary>The origin of the ledger's Scheme ties of theirs, against the colony (the colony's own, never built, would be another).</summary>
        public const string Origin = "theirs";

        // ================= Who schemes =================

        /// <summary>A grudge: as the raiders' camps read one (RaidersCampMath.GrudgeAt), the wrongs they hold, their dead, a vendetta.</summary>
        public const float GrudgeAt = 20f;

        public const float DeadWeight = 15f, VendettaWeight = 20f;

        /// <summary>How big a grudge a people holds: the strength of the wrongs they still hold, each of their dead held against the colony, a vendetta.</summary>
        public static float Grudge(float wrongs, int dead, bool vendetta) => Math.Max(0f, wrongs) + DeadWeight * Math.Max(0, dead) + (vendetta ? VendettaWeight : 0f);

        /// <summary>A people schemes: a town of theirs within the home's reach, at peace (their people can come), with a grudge.</summary>
        public static bool PeopleSchemes(float grudge, bool hostile, bool inReach) => inReach && !hostile && grudge >= GrudgeAt;

        /// <summary>A notable this cold toward the colony schemes.</summary>
        public const int ColdNotable = -40;

        /// <summary>A notable schemes: alive, a neighbour of the home, their people at peace, thinking -40 or less of the colony.</summary>
        public static bool NotableSchemes(int opinion, bool alive, bool hostile, bool inReach) => alive && inReach && !hostile && opinion <= ColdNotable;

        // ================= How often =================

        /// <summary>
        /// A home with a schemer in reach rolls once a day at 8% once a season has passed since its last scheme began: about
        /// two a year (<see cref="YearRate"/>), at most one a season, about as often as the game's own breakdowns in a
        /// colony of ten machines.
        /// </summary>
        public const float DailyChance = 0.08f;

        /// <summary>The setting "Schemes against the colony" read safely: a broken number as the default, a negative one as off, at most 2.</summary>
        public static float Rate(float setting) => float.IsNaN(setting) || float.IsInfinity(setting) ? 1f : Math.Max(0f, Math.Min(2f, setting));

        /// <summary>The day's chance by the setting: 8% at 100%, nothing at 0, 16% at 200%.</summary>
        public static float Chance(float setting) => Clamp01(DailyChance * Rate(setting));

        /// <summary>A season has passed since this home's last scheme began (-1: none ever), so another may.</summary>
        public static bool SeasonPassed(int lastBegan, int now) => lastBegan < 0 || now < lastBegan || now - lastBegan >= SeasonTicks;

        /// <summary>A scheme not acted out within a season is given up: the tie's end.</summary>
        public static int Until(int now) => now + SeasonTicks;

        /// <summary>Schemes begun in a 60-day year at a day's chance, with the season's wait after each: 60 / (15 + 1/p - 1).</summary>
        public static float YearRate(float chance) => chance <= 0f ? 0f : 60f / (SeasonDays + 1f / Clamp01(chance) - 1f);

        // ================= The acts =================

        /// <summary>The night's acts slip out between eleven at night and four in the morning; a word in someone's ear is by day.</summary>
        public const int NightFrom = 23, NightTo = 4, DayFrom = 9, DayTo = 20;

        public static bool Night(int hour) => hour >= NightFrom || hour < NightTo;

        public static bool Day(int hour) => hour >= DayFrom && hour < DayTo;

        /// <summary>The hour fits the act: the word by day, the rest by night.</summary>
        public static bool Hour(SchemeAct act, int hour) => act == SchemeAct.Fight ? Day(hour) : Night(hour);

        /// <summary>A hot temper: a fighting temper (Outsiders.Temper, PersonaMath.FightTemper, -2 to 4) of 2 or more.</summary>
        public const int HotTemper = 2;

        /// <summary>A machine to break down needs an industrial colony or later (the game's breakdown).</summary>
        public static bool MachineAge(WorldAge ours) => ours >= WorldAge.Industrial;

        /// <summary>
        /// Which act, of those the colony offers now (a machine to break, a small valuable in its stores, a hot temper with
        /// someone to set against): a machine, a theft and a smear weigh one each, a fight one times the harm scale (at most
        /// one), so with hurts at nothing no fight is stirred up.
        /// </summary>
        public static SchemeAct Pick(bool machine, bool theft, bool fight, float harmScale, float roll)
        {
            float wm = machine ? 1f : 0f, wt = theft ? 1f : 0f, wf = fight ? Clamp01(harmScale) : 0f, ws = 1f;
            float r = Clamp01(roll) * (wm + wt + wf + ws);
            if (r < wm) return SchemeAct.Machine;
            r -= wm;
            if (r < wt) return SchemeAct.Theft;
            r -= wt;
            if (r < wf) return SchemeAct.Fight;
            return SchemeAct.Smear;
        }

        /// <summary>A machine's form by the colony's means: a console's breakdown in a spacer colony, a jammed machine otherwise.</summary>
        public static string MachineForm(WorldAge ours, bool console) => ours >= WorldAge.Spacer && console ? "Console" : "Machine";

        /// <summary>What a theft takes, by the colony's means: a totem (a small carving), a purse (silver, gold, jade), a tool (components).</summary>
        public const string Totem = "Totem", Purse = "Purse", Tool = "Tool";

        /// <summary>What a thief looks for first, by the colony's age: a tribe's totem, a medieval purse, an industrial or spacer colony's tools.</summary>
        public static string[] TheftOrder(WorldAge ours)
        {
            switch (ours)
            {
                case WorldAge.Tribal: return new[] { Totem, Purse, Tool };
                case WorldAge.Medieval: return new[] { Purse, Totem, Tool };
                default: return new[] { Tool, Purse, Totem };
            }
        }

        /// <summary>A smear by their people's ways: tales at other fires, slanders carried from hall to hall, the papers, the feeds, the Empire's court.</summary>
        public static string SmearForm(WorldAge theirs, bool empire)
        {
            if (empire) return "Court";
            switch (theirs)
            {
                case WorldAge.Tribal: return "Fires";
                case WorldAge.Medieval: return "Halls";
                case WorldAge.Industrial: return "Papers";
                default: return "Feeds";
            }
        }

        public static readonly string[] SmearForms = { "Fires", "Halls", "Papers", "Feeds", "Court" };

        /// <summary>
        /// The act a tie's key names: the act itself while it waits ("Theft"), or the form it took once done or found out
        /// ("Purse"); a key nobody knows is a smear, which needs nothing of the colony's.
        /// </summary>
        public static SchemeAct ActOf(string key)
        {
            switch (key)
            {
                case "Machine":
                case "Console":
                    return SchemeAct.Machine;
                case "Theft":
                case Totem:
                case Purse:
                case Tool:
                    return SchemeAct.Theft;
                case "Fight":
                    return SchemeAct.Fight;
                default:
                    return SchemeAct.Smear;
            }
        }

        // ================= A small valuable =================

        /// <summary>A small valuable: a theft takes about 60 to 180 silver's worth, never one thing worth more than 600 nor heavier than 3 kg a piece.</summary>
        public const float LootMin = 60f, LootMax = 180f, LootMaxUnit = 600f, LootMaxMass = 3f;

        public static bool SmallValuable(float unitValue, float unitMass) => unitValue > 0f && unitValue <= LootMaxUnit && unitMass <= LootMaxMass;

        /// <summary>How many of a stack: about the theft's worth, at least one, never more than the stack.</summary>
        public static int LootCount(float unitValue, int stack, float roll)
        {
            if (unitValue <= 0f || stack <= 0) return 0;
            float want = LootMin + (LootMax - LootMin) * Clamp01(roll);
            int n = (int)Math.Round(want / unitValue);
            return Math.Max(1, Math.Min(stack, n));
        }

        // ================= Caught at it =================

        /// <summary>
        /// Who can catch them: a colonist awake within 12 cells in sight, a guard on duty (drafted, or awake under the
        /// on-guard decree) within 20, a dog awake within 10 in sight or 5 through a wall; a word in someone's ear, whoever
        /// is within earshot (7). Nobody awake within 6 of the place when they set to it.
        /// </summary>
        public const float SeeRange = 12f, GuardRange = 20f, DogRange = 10f, DogNear = 5f, EarRange = 7f, QuietRange = 6f;

        /// <summary>Each one's chance to notice: a colonist awake half, a guard nine in ten, a dog three in four, a word overheard two in five.</summary>
        public const float ColonistNotice = 0.5f, GuardNotice = 0.9f, DogNotice = 0.75f, EarNotice = 0.4f;

        /// <summary>The chance one of them catches it.</summary>
        public static float CatchChance(int colonists, int guards, int dogs, float colonistNotice = ColonistNotice) =>
            1f - Miss(colonists, colonistNotice) * Miss(guards, GuardNotice) * Miss(dogs, DogNotice);

        /// <summary>Who catches it with this roll (0 to 1): a guard first, then a colonist, then a dog, or nobody.</summary>
        public static SchemeWatch Catcher(int colonists, int guards, int dogs, float roll, float colonistNotice = ColonistNotice)
        {
            float r = Clamp01(roll);
            float g = 1f - Miss(guards, GuardNotice);
            if (r < g) return SchemeWatch.Guard;
            float c = 1f - Miss(guards, GuardNotice) * Miss(colonists, colonistNotice);
            if (r < c) return SchemeWatch.Colonist;
            if (r < CatchChance(colonists, guards, dogs, colonistNotice)) return SchemeWatch.Dog;
            return SchemeWatch.None;
        }

        private static float Miss(int n, float p) => n <= 0 ? 1f : (float)Math.Pow(1f - Clamp01(p), n);

        /// <summary>Caught, their people think less of the colony that caught them: 5 goodwill, never so much as to turn them hostile.</summary>
        public const int CaughtGoodwill = 5;

        /// <summary>A smear that goes round costs the colony a little with every other people at peace in reach: 3 goodwill each, never to hostility.</summary>
        public const int SmearGoodwill = 3;

        /// <summary>A people turns hostile at -75 goodwill (the game's own line).</summary>
        public const int HostileAt = -75;

        /// <summary>How much of a loss may come off goodwill without turning them hostile.</summary>
        public static int Loss(int goodwill, int loss) => Math.Max(0, Math.Min(loss, goodwill - HostileAt - 1));

        /// <summary>A smear lands one to three days after it was gathered: the tales carried home and told.</summary>
        public const float SmearDaysMin = 1f, SmearDaysMax = 3f;

        public static int SmearLands(int now, float roll) => now + (int)((SmearDaysMin + (SmearDaysMax - SmearDaysMin) * Clamp01(roll)) * DayTicks);

        /// <summary>A tie's count while its smear is carried home, not yet told round the region.</summary>
        public const int Carried = 1;

        // ================= The history and the facts =================

        /// <summary>A scheme of theirs in the history, written only once found out: "tie:scheme:theirs:broken".</summary>
        public static string HistoryKind(string what) => "tie:scheme:theirs:" + what;

        /// <summary>The facts of a scheme found out: their people's (tie:scheme:theirs:broken), and the one caught at it (them:caught).</summary>
        public const string FoundOutTag = "tie:scheme:theirs:broken", CaughtTag = "them:caught";

        public static readonly string[] Tags = { FoundOutTag, CaughtTag };

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
