using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the wild's alphas and bosses (Beasts/; DESIGN.md "Alphas, nemeses, legends and great herds"): which
    /// animals lead a pack, roam the region as a legend or cross the land as a great herd (by what they are: their size,
    /// whether they hunt, whether they live in herds, never by a list of names), how often each part comes by its setting
    /// (0 to 200%), when a hurt beast flees and when it comes back, who swears to bring it down, what spooks a herd into a
    /// stampede and what a stampede can do to someone in its path. Pure (no game types): Tools/Tests checks it and
    /// Tools/Balance reads its rates for the harm report.
    /// </summary>
    public static class BeastsMath
    {
        public const int DayTicks = 60000;
        public const int HourTicks = 2500;

        // ================= The settings =================

        /// <summary>A part's setting as a rate: 0 switches it off, past 200% it goes no further.</summary>
        public static float Rate(float setting) => float.IsNaN(setting) || setting <= 0f ? 0f : Math.Min(setting, 2f);

        /// <summary>A wait in days at the setting: halved at 200%, doubled at 50%, never at 0.</summary>
        public static float Gap(float setting, float days)
        {
            float r = Rate(setting);
            return r <= 0f ? float.PositiveInfinity : days / r;
        }

        /// <summary>A chance at the setting, never above certain.</summary>
        public static float Chance(float setting, float baseChance) => Clamp01(baseChance * Rate(setting));

        /// <summary>The wait since <paramref name="last"/> (a tick, -1 for never) is over at the setting.</summary>
        public static bool Waited(float setting, float days, int now, int last)
        {
            if (Rate(setting) <= 0f) return false;
            if (last < 0) return true;
            return (now - (double)last) >= Gap(setting, days) * DayTicks;
        }

        // ================= Pack alphas =================

        /// <summary>Wild animals of one hunting kind this close together are a pack, three of them or more.</summary>
        public const int PackMin = 3;
        public const float PackRadius = 14f;
        /// <summary>One alpha a home at a time, and a month at least between two at the setting's 100%.</summary>
        public const float AlphaGapDays = 30f;
        /// <summary>A day's chance that a pack on the land turns out to be led by an alpha.</summary>
        public const float AlphaDayChance = 0.25f;
        /// <summary>A pack of the game's own manhunters is led by one this often (the game's pack, a leader among it).</summary>
        public const float ManhunterAlphaChance = 0.3f;
        public const float ManhunterAlphaGapDays = 15f;
        /// <summary>Of a tamed alpha's pack, this many follow it in at most, one after another; one not in by then gives up.</summary>
        public const int FollowMax = 6;
        public const float FollowGiveUpDays = 2f;
        /// <summary>A pack member this close to its old alpha has come in.</summary>
        public const float JoinRange = 7f;
        public const int MaxAlphaRecords = 16;

        /// <summary>A hunting animal that runs in packs: a wolf, a warg, a raptor, never an insect, a dryad or anything tiny.</summary>
        public static bool PackKind(bool animal, bool predator, bool insect, bool dryad, float bodySize) =>
            animal && predator && !insect && !dryad && bodySize >= 0.4f;

        /// <summary>
        /// Which of a pack leads it: the biggest, in its prime (half its kind's years: neither a yearling nor ancient)
        /// before the old, a male a little before a female.
        /// </summary>
        public static float AlphaScore(float bodySize, float ageShare, bool male) =>
            bodySize * 10f + (1f - Math.Min(1f, Math.Abs(Clamp01(ageShare) - 0.5f) * 2f)) * 2f + (male ? 0.5f : 0f);

        /// <summary>Days after the taming the pack's member at <paramref name="index"/> starts to come in (the first within a day).</summary>
        public static float FollowAfterDays(int index, float roll) => 0.5f + Math.Max(0, index) * 0.8f + Clamp01(roll) * 0.5f;

        // ================= Animal nemeses =================

        /// <summary>A beast nemesis this hurt (and still on its feet) turns and runs for the edge of the map.</summary>
        public const float FleeBelow = 0.45f;
        /// <summary>It comes back between these days after it fled (at the setting's 100%), twice at most.</summary>
        public const float ReturnMinDays = 6f, ReturnMaxDays = 16f;
        public const int MaxReturns = 2;
        /// <summary>Of its returns, this share come for blood (the game's own manhunting); the rest prowl the land.</summary>
        public const float BloodChance = 0.35f;
        /// <summary>Someone close to the one it killed or maimed swears to bring it down this often.</summary>
        public const float OathChance = 0.6f;
        public const int MaxFlights = 12, MaxOaths = 20, MaxNoted = 40;

        /// <summary>A beast that turns and runs: badly hurt but on its feet, alive and nobody's.</summary>
        public static bool Flees(float health, bool downed, bool dead, bool owned) => !dead && !downed && !owned && health > 0f && health < FleeBelow;

        /// <summary>Days until it comes back, by a roll of 0 to 1 at the setting; never at 0.</summary>
        public static float ReturnDays(float setting, float roll)
        {
            float r = Rate(setting);
            if (r <= 0f) return float.PositiveInfinity;
            return (ReturnMinDays + (ReturnMaxDays - ReturnMinDays) * Clamp01(roll)) / r;
        }

        /// <summary>It may come back once more: the setting on, and fewer than two returns so far.</summary>
        public static bool MayReturn(float setting, int returns) => Rate(setting) > 0f && returns < MaxReturns;

        /// <summary>It came back for blood (a roll of 0 to 1 under the share).</summary>
        public static bool ForBlood(float roll) => roll < BloodChance;

        /// <summary>What it did that leaves it scarred and someone sworn against it: a death or a maiming (a colonist only knocked down leaves neither).</summary>
        public static bool Grave(string deed) => deed == "killed" || deed == "maimed";

        /// <summary>Close to the one it hurt, as a vendetta asks: a partner or kin, a bond of 30 or more, or an opinion of 60 or more.</summary>
        public static bool Close(bool partnerOrKin, float tie, int opinion) => partnerOrKin || tie >= 30f || opinion >= 60;

        /// <summary>How close, to pick the one who swears: a partner or kin first, then the bond and the opinion.</summary>
        public static float Closeness(bool partnerOrKin, float tie, int opinion) => (partnerOrKin ? 100f : 0f) + Math.Max(0f, tie) + opinion;

        // ================= Legends of the region =================

        /// <summary>A legend is of the biggest animals loaded (this big at least), one of the dozen biggest that fit.</summary>
        public const float LegendMinBody = 2.5f;
        public const int LegendPool = 12;
        /// <summary>Days between one legend's end and the next's start at the setting's 100%; none before the world's 40th day.</summary>
        public const float LegendGapDays = 150f;
        public const int LegendEarliestDay = 40;
        /// <summary>Its ground is this many tiles from the home, and it moves on to new ground this often, this far.</summary>
        public const int LairMinTiles = 4, LairMaxTiles = 12;
        public const float MoveMinDays = 5f, MoveMaxDays = 9f;
        public const int MoveMinTiles = 2, MoveMaxTiles = 4;
        /// <summary>Its age, as a share of its kind's years.</summary>
        public const float AgeMin = 0.6f, AgeMax = 0.85f;

        /// <summary>
        /// A legend: an animal (never an insect, a dryad or an entity) of the biggest loaded; a Pokémon only a legendary one,
        /// and only once PokéWorld itself has opened its legendaries in this game (whatever its size).
        /// </summary>
        public static bool LegendFits(bool animal, bool insect, bool dryad, bool entity, float bodySize, bool pokemon, bool legendary, bool legendsOpen) =>
            animal && !insect && !dryad && !entity && (pokemon ? legendary && legendsOpen : bodySize >= LegendMinBody);

        /// <summary>The bigger the likelier; a legendary Pokémon as likely as a beast of two and a half.</summary>
        public static float LegendWeight(float bodySize, bool pokemon) => pokemon ? 6.25f : bodySize * bodySize;

        /// <summary>A new legend may come: the setting on, the world's 40th day reached, the wait since the last one over.</summary>
        public static bool LegendDue(float setting, int now, int lastEnded) =>
            Rate(setting) > 0f && now >= LegendEarliestDay * DayTicks && Waited(setting, LegendGapDays, now, lastEnded);

        // ================= Great herds =================

        /// <summary>A great herd comes in spring and in fall (seasons 0 and 2), this often a season at the setting's 100%.</summary>
        public const float SeasonChance = 0.6f;
        /// <summary>A herd of one kind crossing for an edge counts from four head (the game's own migration too); the colony stops to watch from six.</summary>
        public const int MinHerd = 4, MinWatchHerd = 6;
        public const int GreatMin = 6, GreatMax = 24;
        /// <summary>All the herd's bodies together, at most (a herd of mammoths is a few, of deer many).</summary>
        public const float HerdBodyCap = 60f;

        /// <summary>Spring (0) and fall (2), the seasons the herds move in.</summary>
        public static bool Migrates(int season) => season == 0 || season == 2;

        /// <summary>The day of the season (2 to 12 of its 15) it comes on, by a roll of 0 to 1.</summary>
        public static int PlannedDay(float roll) => 2 + Math.Min(10, (int)(Clamp01(roll) * 11f));

        /// <summary>An animal of the great herds: one that lives in herds and migrates, never a hunter, an insect or a dryad, of a deer's size or more.</summary>
        public static bool HerdKind(bool animal, bool herdAnimal, bool predator, bool insect, bool dryad, bool canMigrate, float bodySize) =>
            animal && herdAnimal && !predator && !insect && !dryad && canMigrate && bodySize >= 0.9f;

        /// <summary>The bigger the likelier; one of the land's own three times as likely as one from beyond it.</summary>
        public static float HerdKindWeight(float bodySize, bool ofTheLand) => Math.Max(0f, bodySize) * (ofTheLand ? 1f : 0.3f);

        /// <summary>How many head: ten to twenty of a deer's size, fewer the bigger they are, six to twenty-four.</summary>
        public static int GreatCount(float bodySize, float roll)
        {
            float size = Math.Max(1f, bodySize);
            int n = (int)Math.Round((10f + 10f * Clamp01(roll)) / Math.Sqrt(size));
            n = Math.Min(n, (int)(HerdBodyCap / size));
            return Math.Max(GreatMin, Math.Min(GreatMax, n));
        }

        // ---------------- The stampede ----------------

        /// <summary>What spooks a herd: one of it hurt (a hunter's shot, an arrow, a bite), a shot flying near, a fire close by.</summary>
        public const string SpookHurt = "hurt", SpookShot = "shot", SpookFire = "fire";
        public const float ShotRadius = 9f, FireRadius = 7f;

        /// <summary>The chance a herd breaks into a stampede when spooked, at the setting's 100%.</summary>
        public static float SpookChance(string cause) =>
            cause == SpookHurt ? 0.6f : cause == SpookFire ? 0.5f : cause == SpookShot ? 0.25f : 0f;

        /// <summary>A stampede tramples for the first hour and a little more; then the herd runs on for the edge.</summary>
        public const float StampedeHours = 1.2f;
        /// <summary>Only a runner this big knocks someone down, and only someone it passes this close.</summary>
        public const float TrampleMinBody = 1.2f, TrampleReach = 1.3f;
        /// <summary>Three knocked down in one stampede at most, twelve plants flattened.</summary>
        public const int MaxTrampled = 3, MaxCrops = 12;
        public const float CropChance = 0.2f;

        /// <summary>A runner knocks someone down this often as it passes: more the bigger it is beside them.</summary>
        public static float TrampleChance(float runner, float victim) => Clamp(0.3f * runner / Math.Max(0.3f, victim), 0.08f, 0.6f);

        /// <summary>The blow, blunt: 6 to 16 by the runner's size, give or take a quarter.</summary>
        public static float TrampleDamage(float runner, float roll) => Clamp(4f + 3f * runner, 6f, 16f) * (0.75f + 0.5f * Clamp01(roll));

        /// <summary>Someone bigger than half again the runner is not knocked down by it.</summary>
        public static bool CanTrample(float runner, float victim) => runner >= TrampleMinBody && victim < runner * 1.5f;

        /// <summary>How far a point is from a runner's way since the last look (a segment), squared.</summary>
        public static float SegmentDistSq(float ax, float az, float bx, float bz, float px, float pz)
        {
            float dx = bx - ax, dz = bz - az;
            float len = dx * dx + dz * dz;
            float t = len <= 0f ? 0f : Clamp01(((px - ax) * dx + (pz - az) * dz) / len);
            float cx = ax + t * dx - px, cz = az + t * dz - pz;
            return cx * cx + cz * cz;
        }

        // ---------------- The pause while they pass ----------------

        /// <summary>Up to three of the colony in sight of the herd and at nothing pressing stop to watch it go by, about half an hour.</summary>
        public const float WatchRange = 32f;
        public const int WatchMax = 3, WatchTicks = 1200;

        // ================= Arithmetic =================

        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : float.IsNaN(x) ? 0f : x;

        public static float Clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;
    }
}
