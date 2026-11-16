using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The seven single quests at home of round 2 (the player's "great, do some more", 16 November 2026:
    /// docs/quests-proposals.md, Q3, Q4, Q6, Q8, Q11, Q12 and Q13).
    /// </summary>
    public enum HomeQuest2
    {
        /// <summary>Q3: a neighbour's young fighter stays and trains with the colony's best shot or melee fighter.</summary>
        Trainee,
        /// <summary>Q4: one piece of good quality or better made for a wedding, collected on the day.</summary>
        WeddingPiece,
        /// <summary>Q6: their sick animal tended back to health at the colony, then collected.</summary>
        SickAnimal,
        /// <summary>Q8: their caravan camps one night at the edge of the colony's land; the colony keeps watch.</summary>
        CampGuard,
        /// <summary>Q11: their seed kept cold and untouched in the colony's cold store until the sowing.</summary>
        SeedGrain,
        /// <summary>Q12: their scholar stays while the colony's researchers copy their book at a research bench.</summary>
        BookCopy,
        /// <summary>Q13: their musicians stay one evening and play at the colony's party, and leave in the morning.</summary>
        Musicians
    }

    /// <summary>How a young fighter's training went, by the hours trained against the hours agreed.</summary>
    public enum TrainEnd
    {
        /// <summary>All the hours agreed.</summary>
        Full,
        /// <summary>Half of them at least.</summary>
        Half,
        /// <summary>Less than half.</summary>
        Little
    }

    /// <summary>How the sick animal stands.</summary>
    public enum SickEnd
    {
        Sick,
        Well,
        Died,
        /// <summary>No longer the colony's to keep for them (sold, released, gone off the map).</summary>
        Lost,
        /// <summary>Well and bonded to someone of the colony: they leave it where it is loved.</summary>
        Bonded,
        /// <summary>The days are up and it is still sick: they take it home as it is.</summary>
        StillSick
    }

    /// <summary>How the camp's night went.</summary>
    public enum NightEnd
    {
        /// <summary>Nothing came.</summary>
        Quiet,
        /// <summary>Something came, and none of their people died.</summary>
        Defended,
        /// <summary>One of their people died in the night.</summary>
        Lost
    }

    /// <summary>How their seed stands.</summary>
    public enum SeedEnd
    {
        Keeping,
        /// <summary>Kept cold and whole until the sowing.</summary>
        Kept,
        /// <summary>Some of it used.</summary>
        Eaten,
        /// <summary>Gone: burned, taken, nowhere on the map.</summary>
        Lost,
        /// <summary>Rotten, by the game's own rot.</summary>
        Spoiled,
        /// <summary>Kept warmer than freezing for too many hours: no good as seed.</summary>
        Warm
    }

    /// <summary>How the copy of their book stands.</summary>
    public enum CopyEnd
    {
        Copying,
        /// <summary>All the hours of work done.</summary>
        Done,
        /// <summary>The scholar's days are up with half the work done at least.</summary>
        Half,
        /// <summary>The scholar's days are up with less than half done.</summary>
        Little
    }

    /// <summary>
    /// The pure rules of round 2's seven quests at home (the workers in Workers_MissionHome3.cs and Workers_MissionHome4.cs,
    /// what they share in HomeQuests2.cs): their kinds, their share of the missions' one draw, the reward by the asking
    /// people's age (round 1's own scale: HomeQuestsMath), the days, the hours and the odds, and what counts as done. No game
    /// types, so Tools/Tests can check it.
    /// </summary>
    public static class HomeQuests2Math
    {
        public const string TraineeKind = "trainee", WeddingKind = "weddingpiece", SickKind = "sickanimal", CampKind = "campguard", SeedKind = "seedkept",
            CopyKind = "bookcopy", MusicKind = "musicians";

        /// <summary>Their mission kinds, in the order of <see cref="HomeQuest2"/>.</summary>
        public static readonly string[] Kinds = { TraineeKind, WeddingKind, SickKind, CampKind, SeedKind, CopyKind, MusicKind };

        public static string KindOf(HomeQuest2 q) => Kinds[(int)q];

        // ---------------- Pace ----------------

        /// <summary>
        /// Each one's base chance, round 1's own: the seven take 2.1 of the missions' one draw (MissionPool), which still
        /// weighs what it weighed before, so missions as a whole come no oftener; every other mission gives up a little.
        /// </summary>
        public const float BaseChance = 0.3f;

        /// <summary>The same quest not again for this long (days).</summary>
        public const int RestDays = 75;

        // ---------------- The reward ----------------

        /// <summary>The silver each quest is worth before the people's age and the colony's wealth (the wedding's piece is paid by its worth instead).</summary>
        public static void Base(HomeQuest2 q, out float min, out float max)
        {
            switch (q)
            {
                case HomeQuest2.Trainee: min = 140f; max = 240f; break;
                case HomeQuest2.WeddingPiece: min = 60f; max = 120f; break;
                case HomeQuest2.SickAnimal: min = 140f; max = 240f; break;
                case HomeQuest2.CampGuard: min = 120f; max = 220f; break;
                case HomeQuest2.SeedGrain: min = 120f; max = 200f; break;
                case HomeQuest2.BookCopy: min = 80f; max = 160f; break;
                default: min = 60f; max = 140f; break;
            }
        }

        /// <summary>
        /// The reward: the quest's own range (by <paramref name="roll"/>, 0 to 1) times the people's age (a tribe 0.8 to a
        /// spacer people 1.3, the Empire as a spacer people) and the colony's wealth (0.6 to 2), rounded to tens, thirty at least.
        /// </summary>
        public static int Reward(HomeQuest2 q, int age, bool empire, float wealth, float roll)
        {
            Base(q, out float min, out float max);
            float r = Math.Max(0f, Math.Min(1f, roll));
            float w = Math.Max(0.6f, Math.Min(2f, wealth));
            return HomeQuestsMath.Tens(Math.Max(30f, (min + (max - min) * r) * HomeQuestsMath.AgeFactor(age, empire) * w));
        }

        /// <summary>The goodwill a quest done well brings.</summary>
        public static int Goodwill(HomeQuest2 q)
        {
            switch (q)
            {
                case HomeQuest2.Trainee: return 10;
                case HomeQuest2.WeddingPiece: return 10;
                case HomeQuest2.SickAnimal: return 10;
                case HomeQuest2.CampGuard: return 12;
                case HomeQuest2.SeedGrain: return 10;
                case HomeQuest2.BookCopy: return 8;
                default: return 8;
            }
        }

        /// <summary>The goodwill a quest failed costs, before what failed it (a death is twice this).</summary>
        public static int Penalty(HomeQuest2 q) => q == HomeQuest2.Musicians || q == HomeQuest2.BookCopy ? 4 : q == HomeQuest2.CampGuard ? 8 : 6;

        // ---------------- The days ----------------

        /// <summary>
        /// The whole days a quest names, by <paramref name="roll"/> (0 to 1): the young fighter stays 3 to 5, the wedding is
        /// 6 to 9 days off, the sick animal has 8 to 12 to get well, the caravan camps one night, the seed is kept 10 to 14,
        /// the scholar stays 3 to 4, the musicians one evening.
        /// </summary>
        public static int Days(HomeQuest2 q, float roll)
        {
            int min, max;
            switch (q)
            {
                case HomeQuest2.Trainee: min = 3; max = 5; break;
                case HomeQuest2.WeddingPiece: min = 6; max = 9; break;
                case HomeQuest2.SickAnimal: min = 8; max = 12; break;
                case HomeQuest2.SeedGrain: min = 10; max = 14; break;
                case HomeQuest2.BookCopy: min = 3; max = 4; break;
                default: min = 1; max = 1; break;
            }
            float r = Math.Max(0f, Math.Min(0.999f, roll));
            return min + (int)(r * (max - min + 1));
        }

        // ---------------- Q3. A young fighter trained ----------------

        /// <summary>Hours of training each day of the stay, in the evening (the drills' hours and a little either side).</summary>
        public const int TrainHoursPerDay = 2, TrainFrom = 16, TrainTo = 21;

        /// <summary>
        /// Experience a tick of training gives, through the game's own learning (its passions and its daily limit): about what
        /// an hour at the research bench gives (the game's 0.1 a tick), a little more for the one being taught, half for the
        /// one teaching.
        /// </summary>
        public const float TraineeXpPerTick = 0.12f, TrainerXpPerTick = 0.06f;

        public static bool TrainHour(int hour) => hour >= TrainFrom && hour < TrainTo;

        /// <summary>The hours agreed: two a day for each day of the stay.</summary>
        public static int TrainTarget(int days) => Math.Max(1, days) * TrainHoursPerDay;

        /// <summary>
        /// They train at the colony's shooting or archery target when it has one, the young fighter has a ranged weapon and the
        /// colony's best shot is at least as good as its best melee fighter; else they spar, which needs nothing but open ground.
        /// </summary>
        public static bool Shoots(bool target, bool traineeRanged, int bestShot, int bestMelee) => target && traineeRanged && bestShot >= bestMelee;

        /// <summary>The young fighter comes with less of the skill than their teacher: three levels under at most (none below 0).</summary>
        public static int TraineeLevel(int level, int trainerLevel) => Math.Max(0, Math.Min(level, trainerLevel - 3));

        public static TrainEnd TrainOutcome(int ticksTrained, int targetHours, int ticksPerHour)
        {
            int need = Math.Max(1, targetHours) * ticksPerHour;
            if (ticksTrained >= need) return TrainEnd.Full;
            return ticksTrained * 2 >= need ? TrainEnd.Half : TrainEnd.Little;
        }

        /// <summary>Half the pay for half the hours.</summary>
        public static int TrainPay(int reward, TrainEnd e) => e == TrainEnd.Full ? reward : e == TrainEnd.Half ? HomeQuestsMath.Tens(reward / 2f) : 0;

        // ---------------- Q4. A piece for a wedding ----------------

        /// <summary>Good quality or better (the game's QualityCategory: 3 good, 4 excellent, 5 masterwork, 6 legendary).</summary>
        public const int MinQuality = 3;

        public static bool GoodEnough(int quality) => quality >= MinQuality;

        /// <summary>The game's price factor for good quality: what they reckon the piece is worth.</summary>
        public const float GoodPriceFactor = 1.25f;

        /// <summary>
        /// They pay half as much again as the piece is worth at good quality, and the quest's own sum for the making on top
        /// (by their age and the colony's wealth), rounded to tens.
        /// </summary>
        public static int PiecePay(float pieceValue, int age, bool empire, float wealth, float roll) =>
            HomeQuestsMath.Tens(Math.Max(0f, pieceValue) * GoodPriceFactor * 1.5f + Reward(HomeQuest2.WeddingPiece, age, empire, wealth, roll));

        // ---------------- Q6. A sick animal tended ----------------

        /// <summary>The illness it comes with: the game's animal flu, its plague (only with a doctor this good), or underfed from the road.</summary>
        public const string Flu = "flu", Plague = "plague", Thin = "thin";

        public const int PlagueSkill = 6;

        /// <summary>The illness by the colony's best doctor's skill and a roll: plague one time in four when they could treat it, underfed one in four, else flu.</summary>
        public static string Illness(int doctorSkill, float roll)
        {
            if (doctorSkill >= PlagueSkill && roll < 0.25f) return Plague;
            return roll > 0.75f ? Thin : Flu;
        }

        /// <summary>How far along the illness is when it comes: far enough to need treatment, never at its end.</summary>
        public static float Severity(string illness) => illness == Plague ? 0.25f : illness == Thin ? 0.35f : 0.3f;

        /// <summary>
        /// Where the animal stands: dead, gone from the colony, well (bonded or not), the days up while still sick, else sick.
        /// </summary>
        public static SickEnd SickOutcome(bool dead, bool ours, bool sick, bool bonded, bool timeUp)
        {
            if (dead) return SickEnd.Died;
            if (!ours) return SickEnd.Lost;
            if (!sick) return bonded ? SickEnd.Bonded : SickEnd.Well;
            return timeUp ? SickEnd.StillSick : SickEnd.Sick;
        }

        // ---------------- Q8. Guard their camp for a night ----------------

        /// <summary>
        /// Something comes for the camp one night in three (raiders or predators), through the shared harm budget
        /// (HarmKinds.CampNight): its scale and its pass make it rarer with harm turned down, or after other hurts lately.
        /// </summary>
        public const float AttackChance = 0.35f;

        /// <summary>Raiders rather than predators this often, when there are raiders about who are at war with both.</summary>
        public const float RaiderChance = 0.6f;

        /// <summary>They make camp from six in the evening and leave at six in the morning; what comes, comes between ten and three.</summary>
        public const int CampHour = 18, DawnHour = 6, AttackFrom = 22, AttackTo = 27;

        /// <summary>The camp is at least this far from the middle of the colony, at the edge of its land.</summary>
        public const float CampMin = 30f;

        /// <summary>What comes is a quarter of the storyteller's threat points, between one ordinary fighter and a small band.</summary>
        public static float AttackPoints(float threatPoints) => Math.Max(60f, Math.Min(300f, threatPoints * 0.25f));

        /// <summary>How many come: as many as the points buy at <paramref name="each"/>, one to three.</summary>
        public static int Attackers(float points, float each) => Math.Max(1, Math.Min(3, (int)Math.Round(points / Math.Max(20f, each))));

        public static NightEnd CampOutcome(int theirDead, bool attacked) => theirDead > 0 ? NightEnd.Lost : attacked ? NightEnd.Defended : NightEnd.Quiet;

        /// <summary>A night guarded through an attack pays a quarter more.</summary>
        public static int CampPay(int reward, NightEnd e) => e == NightEnd.Defended ? HomeQuestsMath.Tens(reward * 1.25f) : e == NightEnd.Quiet ? reward : 0;

        /// <summary>The hour of the night it comes, by a roll: from ten at night to three in the morning (27 is three the next day).</summary>
        public static int AttackHour(float roll) => AttackFrom + (int)(Math.Max(0f, Math.Min(0.999f, roll)) * (AttackTo - AttackFrom));

        // ---------------- Q11. Seed grain kept cold ----------------

        /// <summary>Cold is freezing or colder (where the game's food stops rotting).</summary>
        public const float ColdMax = 0f;

        /// <summary>Hours it may be warmer than that in all before it is no good as seed: half a day.</summary>
        public const int WarmHoursMax = 12;

        /// <summary>Hours after their carts set it down before the cold is counted: time to carry it into the cold store.</summary>
        public const int CarryHours = 6;

        public static bool Cold(float temperature) => temperature <= ColdMax;

        /// <summary>How much they bring: 50 to 75, never more than one stack.</summary>
        public static int SeedCount(int stackLimit, float roll)
        {
            int max = Math.Max(1, Math.Min(75, stackLimit));
            int min = Math.Min(max, 50);
            return min + (int)(Math.Max(0f, Math.Min(0.999f, roll)) * (max - min + 1));
        }

        /// <summary>
        /// Where their seed stands, in this order: gone, some of it used, rotten, too many warm hours, the days up (kept), else
        /// still keeping.
        /// </summary>
        public static SeedEnd SeedOutcome(bool gone, int have, int count, bool rotten, int warmHours, bool timeUp)
        {
            if (gone) return SeedEnd.Lost;
            if (have < count) return SeedEnd.Eaten;
            if (rotten) return SeedEnd.Spoiled;
            if (warmHours > WarmHoursMax) return SeedEnd.Warm;
            return timeUp ? SeedEnd.Kept : SeedEnd.Keeping;
        }

        // ---------------- Q12. Their book copied ----------------

        /// <summary>Hours of work at the research bench the copy takes: 10 to 16.</summary>
        public static int CopyHours(float roll) => 10 + (int)(Math.Max(0f, Math.Min(0.999f, roll)) * 7);

        /// <summary>Intellectual experience a tick of copying gives: the game's own for research.</summary>
        public const float CopyXpPerTick = 0.1f;

        public static CopyEnd CopyOutcome(int ticksCopied, int targetHours, int ticksPerHour, bool timeUp)
        {
            int need = Math.Max(1, targetHours) * ticksPerHour;
            if (ticksCopied >= need) return CopyEnd.Done;
            if (!timeUp) return CopyEnd.Copying;
            return ticksCopied * 2 >= need ? CopyEnd.Half : CopyEnd.Little;
        }

        /// <summary>The book's kind by their age: their stories for a tribe (a novel), learning for the medieval (a textbook), a manual for the modern (a schematic).</summary>
        public static string BookTag(int age, bool empire) => empire ? "learning" : age <= HomeQuestsMath.Tribal ? "stories" : age == HomeQuestsMath.Medieval ? "learning" : "manual";

        // ---------------- Q13. Musicians for a night ----------------

        /// <summary>They come in the afternoon, the party is started from six (tried until ten), and they leave at seven in the morning.</summary>
        public const int ArriveHour = 14, PlayFrom = 18, PlayUntil = 23, PartyBy = 22, MorningHour = 7;

        /// <summary>How many come: two or three.</summary>
        public static int Musicians(float roll) => roll < 0.5f ? 2 : 3;

        /// <summary>A night without a bed for one of them pays half the goodwill.</summary>
        public static int MusicGoodwill(int goodwill, bool slept) => slept ? goodwill : goodwill / 2;

        /// <summary>Hours from <paramref name="hour"/> to the next <paramref name="target"/> o'clock, at least <paramref name="min"/>.</summary>
        public static float HoursUntil(float hour, int target, float min)
        {
            float h = (target - hour + 48f) % 24f;
            while (h < min) h += 24f;
            return h;
        }
    }
}
