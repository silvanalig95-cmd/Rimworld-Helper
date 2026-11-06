using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How bad a Rimfolk-made hurt may be, which decides who may take it: children only the mildest, never a bout or a
    /// fight; babies nothing at all.
    /// </summary>
    public enum HurtClass
    {
        /// <summary>The mildest: a scrape or a small bruise, a cold from the rain. Children may take these.</summary>
        Mild,
        /// <summary>A real wound or a worse ailment: a cut, a burn, a crack, a torn back. Grown-ups only.</summary>
        Injury,
        /// <summary>A real blow in a practice bout, a contest or a champion's bout. Grown-ups only.</summary>
        Bout,
        /// <summary>A fight: the game's social fight, a brawl. Grown-ups only.</summary>
        Fight
    }

    /// <summary>The kind of wound, as the game has it: its own injuries (Bruise, Cut, Burn, Crush, Crack, Scratch).</summary>
    public enum HurtKind
    {
        Bruise,
        Cut,
        Burn,
        Crush,
        Crack,
        Scratch
    }

    /// <summary>Where on the body a hurt lands: a hand, an arm, a foot, a leg, the torso, or any of the arms, legs and torso.</summary>
    public enum HurtWhere
    {
        Hand,
        Arm,
        Foot,
        Leg,
        Torso,
        Any
    }

    /// <summary>Why someone may not take a Rimfolk-made hurt now (<see cref="None"/>: they may).</summary>
    public enum HurtBar
    {
        None,
        /// <summary>"Hurts from everyday life" is at nothing.</summary>
        Off,
        Baby,
        /// <summary>A child, and the hurt is more than the mildest.</summary>
        Child,
        Downed,
        /// <summary>In a sickbed, or ought to be.</summary>
        Sickbed,
        Pregnant,
        /// <summary>Already fighting: drafted, in a fight or a rage, or hurt in the last hour.</summary>
        Fighting,
        /// <summary>A Rimfolk-made hurt in the last three days (the shared cap).</summary>
        Lately
    }

    /// <summary>What a decree can bring on the body while it holds (DecreeDef.hurts; Harm/DrillBouts.cs, Harm/WorkHurts.cs).</summary>
    public enum DecreeHurt
    {
        None,
        /// <summary>Drills: paired bouts every evening, one in 25 landing a real blow.</summary>
        Drills,
        /// <summary>Militia drills: one pair at the evening's muster, most evenings.</summary>
        Muster,
        /// <summary>Long hours, a harvest push, a building season: a work injury now and then on a long day.</summary>
        TiredHands
    }

    /// <summary>The work a long day's slip comes at (Harm/WorkHurts.cs), by the job's work type.</summary>
    public enum WorkHurtKind
    {
        None,
        /// <summary>Crafting, tailoring, art: a cut at the bench (a crushed hand at a machine, a fabricator's burn).</summary>
        Bench,
        /// <summary>Cooking and smithing: a burn at the stove, the fire pit or the forge.</summary>
        Hot,
        /// <summary>Building and mining: a crushed foot (a crushed hand at a machine).</summary>
        Heavy,
        /// <summary>Hauling: a load dropped on a foot.</summary>
        Load,
        /// <summary>Sowing, harvesting, cutting plants: a cut from the sickle.</summary>
        Field
    }

    /// <summary>
    /// The harm review's shared rules (docs/harm-review-proposals.md, "Rules every proposal keeps") and the rates of the
    /// hurts built on them, with no game types so Tools/Tests can check them. Harm/Harm.cs is the game's side: every
    /// Rimfolk-made hurt goes through it.
    /// <list type="bullet">
    /// <item>Every rate is multiplied by the setting "Hurts from everyday life" (0 to 200%, 100 by default) and leans with
    /// the storyteller's own threat and disease numbers (<see cref="Lean"/>), so a peaceful game stays gentle.</item>
    /// <item>The shared cap (<see cref="Bar"/>): at most one Rimfolk-made hurt per colonist in three days, and none while
    /// downed, in a sickbed, pregnant or already fighting. Babies never; children only the mildest.</item>
    /// <item>Nothing kills outright: a wound never takes more than a part has left but one point
    /// (<see cref="SafeSeverity"/>), and lands only on an arm, a leg, a hand, a foot or the torso.</item>
    /// </list>
    /// </summary>
    public static class HarmMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        // ---------------- The setting and the storyteller ----------------

        public const float MinSetting = 0f, MaxSetting = 2f, DefaultSetting = 1f;

        /// <summary>The game's "no diseases" line on its disease interval (Difficulty.DiseaseIntervalFactorCutoff).</summary>
        public const float NoDiseases = 100f;

        /// <summary>
        /// How far the storyteller's own numbers lean the hurts: the geometric mean of a threat term (0.35 at no threats,
        /// 1 at the usual 100%, up to 1.25 at twice it) and a disease term (one over the disease interval, between a half
        /// and 1.25; diseases off count as a half). A peaceful game comes to about two fifths, the hardest about a
        /// quarter more than the usual.
        /// </summary>
        public static float Lean(float threatScale, float diseaseInterval)
        {
            float t = Clamp(threatScale, 0f, 2f);
            float threat = t <= 1f ? 0.35f + 0.65f * t : 1f + 0.25f * (t - 1f);
            float disease = diseaseInterval >= NoDiseases ? 0.5f : Clamp(1f / Math.Max(0.01f, diseaseInterval), 0.5f, 1.25f);
            return (float)Math.Sqrt(threat * disease);
        }

        /// <summary>The factor on every rate: the setting (0 to 2) times the storyteller's lean.</summary>
        public static float Scale(float setting, float threatScale, float diseaseInterval) =>
            Clamp(setting, MinSetting, MaxSetting) * Lean(threatScale, diseaseInterval);

        /// <summary>A chance after the scale, never above certainty.</summary>
        public static float Chance(float baseChance, float scale) => Clamp(baseChance * Math.Max(0f, scale), 0f, 1f);

        public static bool Roll(float roll, float baseChance, float scale) => roll < Chance(baseChance, scale);

        // ---------------- The shared cap ----------------

        /// <summary>Days between two Rimfolk-made hurts of one colonist.</summary>
        public const float GapDays = 3f;

        public const int GapTicks = (int)(GapDays * DayTicks);

        /// <summary>Hurt in this many ticks (a raid, a fight) counts as already fighting.</summary>
        public const int FreshHarmTicks = HourTicks;

        /// <summary>No Rimfolk-made hurt on record: the tick to store.</summary>
        public const int Never = int.MinValue;

        /// <summary>A Rimfolk-made hurt lately: one in the last three days (a tick in the future, from a save played on, counts as long ago).</summary>
        public static bool Lately(int lastTick, int now) => lastTick != Never && lastTick <= now && now - lastTick < GapTicks;

        /// <summary>Children may take only the mildest; grown-ups anything.</summary>
        public static bool ClassFits(bool child, HurtClass c) => !child || c == HurtClass.Mild;

        /// <summary>
        /// Whether someone may take a Rimfolk-made hurt of this class now, and if not, why (the first reason that holds).
        /// <paramref name="scale"/> is <see cref="Scale"/>; <paramref name="lastTick"/> their last Rimfolk-made hurt.
        /// </summary>
        public static HurtBar Bar(float scale, bool baby, bool child, bool downed, bool sickbed, bool pregnant, bool fighting, int lastTick, int now, HurtClass c)
        {
            if (scale <= 0f) return HurtBar.Off;
            if (baby) return HurtBar.Baby;
            if (!ClassFits(child, c)) return HurtBar.Child;
            if (downed) return HurtBar.Downed;
            if (sickbed) return HurtBar.Sickbed;
            if (pregnant) return HurtBar.Pregnant;
            if (fighting) return HurtBar.Fighting;
            if (Lately(lastTick, now)) return HurtBar.Lately;
            return HurtBar.None;
        }

        // ---------------- The wound itself ----------------

        /// <summary>A wound's size: between low and high by the roll, times the body's own damage factor (the tough take half).</summary>
        public static float Severity(float low, float high, float roll, float incomingFactor) =>
            (low + (high - low) * Clamp(roll, 0f, 1f)) * Math.Max(0f, incomingFactor);

        /// <summary>Never more than the part has left but one point: a Rimfolk-made wound never takes a part off.</summary>
        public static float SafeSeverity(float severity, float partHealth) => Math.Max(0f, Math.Min(severity, partHealth - 1f));

        /// <summary>Too small to bother with (a part already nearly gone): no wound at all.</summary>
        public const float SmallestWound = 0.5f;

        // ---------------- Drills that bruise (1.2.1) ----------------

        /// <summary>One bout in 25 lands a real blow, as the outsiders' sparring does.</summary>
        public const float DrillBlowChance = 1f / 25f;

        /// <summary>A marshal in office halves it.</summary>
        public static float DrillBlow(bool marshal) => DrillBlowChance * (marshal ? 0.5f : 1f);

        /// <summary>The real blow: a bruise or a cut of 6 to 9 (the tough take half, by the game's own damage factor).</summary>
        public const float DrillLow = 6f, DrillHigh = 9f;

        /// <summary>The evening's drill: from five to eight, local time.</summary>
        public const int DrillFrom = 17, DrillTo = 20;

        public static bool DrillHour(int hour) => hour >= DrillFrom && hour < DrillTo;

        /// <summary>
        /// The pairs at the evening's drill: every two who can fight under the drills decree; one pair at the militia's
        /// muster. With fewer than two, none.
        /// </summary>
        public static int DrillPairs(int fighters, bool muster) => fighters < 2 ? 0 : muster ? 1 : fighters / 2;

        /// <summary>The militia mustering most evenings: three in four, by the day.</summary>
        public static bool MusterEvening(int day) => ((day % 4) + 4) % 4 != 3;

        /// <summary>Swings each in a drill bout, and about how far apart.</summary>
        public const int DrillSwings = 6, DrillSwingTicks = 110;

        /// <summary>Expected real blows in a decree: bouts an evening times evenings times the chance (for the tests and the docs).</summary>
        public static float DrillBlowsPerDecree(int fighters, float days, bool muster, bool marshal) =>
            DrillPairs(fighters, muster) * days * (muster ? 0.75f : 1f) * DrillBlow(marshal);

        // ---------------- Tired hands (1.2.2) ----------------

        /// <summary>One work injury in 150 long days worked under a work decree, like the everyday work ailments.</summary>
        public const float TiredHandsChance = 1f / 150f;

        /// <summary>A long day: six hours of the decree's work.</summary>
        public const float LongDayHours = 6f;

        /// <summary>Below this much rest (30%), twice as likely.</summary>
        public const float LowRest = 0.3f;

        /// <summary>
        /// The chance of a work injury on a long day: once for each work decree driving that work (two at once, twice),
        /// twice below 30% rest, half for the tough, half in a spacer colony (rarer, and worse when it comes).
        /// </summary>
        public static float TiredHands(int decrees, bool lowRest, bool tough, bool spacer)
        {
            if (decrees <= 0) return 0f;
            return TiredHandsChance * Math.Min(decrees, 2) * (lowRest ? 2f : 1f) * (tough ? 0.5f : 1f) * (spacer ? 0.5f : 1f);
        }

        /// <summary>A work injury: a wound of 3 to 6; a spacer colony's fabricator burn 5 to 8.</summary>
        public const float WorkLow = 3f, WorkHigh = 6f, SpacerLow = 5f, SpacerHigh = 8f;

        public static float WorkLowFor(bool spacer) => spacer ? SpacerLow : WorkLow;

        public static float WorkHighFor(bool spacer) => spacer ? SpacerHigh : WorkHigh;

        /// <summary>
        /// What a slip at this work is, by the colony's means: a cut at the bench and a crushed foot under a load in every
        /// age; at an industrial colony's machines a crushed hand; at a spacer colony's fabricator a burn (rarer, worse).
        /// </summary>
        public static void WorkSlip(WorkHurtKind work, WorldAge age, out HurtKind kind, out HurtWhere where)
        {
            switch (work)
            {
                case WorkHurtKind.Bench:
                    kind = age == WorldAge.Industrial ? HurtKind.Crush : age == WorldAge.Spacer ? HurtKind.Burn : HurtKind.Cut;
                    where = HurtWhere.Hand;
                    return;
                case WorkHurtKind.Hot:
                    kind = HurtKind.Burn;
                    where = HurtWhere.Hand;
                    return;
                case WorkHurtKind.Heavy:
                    kind = HurtKind.Crush;
                    where = age == WorldAge.Industrial ? HurtWhere.Hand : HurtWhere.Foot;
                    return;
                case WorkHurtKind.Load:
                    kind = age == WorldAge.Spacer ? HurtKind.Bruise : HurtKind.Crush;
                    where = HurtWhere.Foot;
                    return;
                case WorkHurtKind.Field:
                    kind = HurtKind.Cut;
                    where = HurtWhere.Hand;
                    return;
                default:
                    kind = HurtKind.Bruise;
                    where = HurtWhere.Any;
                    return;
            }
        }

        /// <summary>The work a job is, by its work type's name (and the game's mining and hauling jobs): none for the rest.</summary>
        public static WorkHurtKind WorkOf(string workType, bool mining, bool hauling)
        {
            if (mining) return WorkHurtKind.Heavy;
            switch (workType)
            {
                case "Cooking":
                case "Smithing":
                    return WorkHurtKind.Hot;
                case "Crafting":
                case "Tailoring":
                case "Art":
                    return WorkHurtKind.Bench;
                case "Construction":
                case "Mining":
                    return WorkHurtKind.Heavy;
                case "Growing":
                case "PlantCutting":
                    return WorkHurtKind.Field;
            }
            if (hauling || (workType != null && workType.IndexOf("Haul", StringComparison.Ordinal) >= 0)) return WorkHurtKind.Load;
            return WorkHurtKind.None;
        }

        /// <summary>A work decree drives this work: with no work named, any work (long hours); else only the work it names.</summary>
        public static bool Drives(WorkHurtKind[] named, WorkHurtKind work)
        {
            if (work == WorkHurtKind.None) return false;
            if (named == null || named.Length == 0) return true;
            return Array.IndexOf(named, work) >= 0;
        }

        /// <summary>Expected work injuries in a decree, everyone working long days (for the tests and the docs).</summary>
        public static float TiredHandsPerDecree(int colonists, float days, float lowRestShare) =>
            colonists * days * TiredHandsChance * (1f + Clamp(lowRestShare, 0f, 1f));

        // ---------------- Working through it (3.2.1) ----------------

        /// <summary>A strain or a sprain worked hard through: four hours of heavy work in a day with it, untended.</summary>
        public const float WorkedThroughHours = 4f;

        /// <summary>One in ten such ailments turns worse.</summary>
        public const float WorkedThroughChance = 0.1f;

        public static bool WorkedThrough(float heavyHours, bool tended, bool inBed) => !tended && !inBed && heavyHours >= WorkedThroughHours;

        /// <summary>The fall on a bad ankle: a crack one time in three, else a bruise.</summary>
        public const float CrackShare = 1f / 3f;

        /// <summary>The fall's bruise (3 to 5 on the leg) and crack (2 to 4 on the bone).</summary>
        public const float FallBruiseLow = 3f, FallBruiseHigh = 5f, FallCrackLow = 2f, FallCrackHigh = 4f;

        // ---------------- Small helpers ----------------

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
