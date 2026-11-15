using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The colony's animals' small daily moments, each played out on the map (Animals/AnimalDays.cs).</summary>
    public enum AnimalDay
    {
        /// <summary>Home again after days away: its animal comes running.</summary>
        Greet,
        /// <summary>At night, a dog or a cat goes to sleep at the foot of its person's bed.</summary>
        FootOfBed,
        /// <summary>Someone eating: it sits beside them and watches every bite.</summary>
        Beg,
        /// <summary>A child at leisure and an animal: a game of chase.</summary>
        ChildPlay,
        /// <summary>The morning: a herd of one kind moves off together behind its eldest.</summary>
        Herd,
        /// <summary>The herdmaster (or whoever keeps the animals) goes round them, one after another.</summary>
        Rounds,
        /// <summary>Rain or snow coming on: a pet outdoors goes in under a roof.</summary>
        Shelter,
        /// <summary>Snow: a young one plays in it.</summary>
        SnowPlay,
        /// <summary>A bitter night: the herd lies down close together.</summary>
        Huddle,
        /// <summary>Raiders come: a pet raises the alarm, facing them.</summary>
        Alarm,
        /// <summary>A god's presence over the land: the animals won't settle and keep close to their people.</summary>
        Uneasy,
        /// <summary>Its person dies: it goes to lie by them, and cries.</summary>
        Mourn
    }

    /// <summary>
    /// An encounter or a personal story's step that an animal plays out on the map before it is told (Animals/AnimalScenes.cs):
    /// running to someone, sleeping at the foot of their bed, a bite snatched at a meal, a game of chase with a child, following
    /// them at work, comfort when they are low, a trick, a raid on the stores, sitting together, a grudge or a feud's ambush,
    /// an old one walking over slowly, a mother lying by her young, standing between someone and a stranger, crying out at
    /// night.
    /// </summary>
    public enum AnimalScene
    {
        None,
        Greet,
        FootOfBed,
        Plate,
        ChildPlay,
        Shadow,
        Comfort,
        Trick,
        Larder,
        Confide,
        Pester,
        SlowGreet,
        Litter,
        Guard,
        Cry
    }

    /// <summary>
    /// The rules behind the animals' daily moments, with no game types so the tests can run them: whom an animal with no bond
    /// and no master takes to, how often each moment comes, who may beg, sleep at the foot of a bed or play with a child,
    /// where the foot of a bed is, when someone counts as home again, which two animals may become friends, and the cold
    /// that makes a herd huddle.
    /// </summary>
    public static class AnimalDaysMath
    {
        // ---------------- Whom it takes to ----------------

        /// <summary>
        /// How likely an animal is to take to someone: everyone a little, a good hand with animals more, one who loves them
        /// more still, a child most (children and animals find each other), a kind soul a little more; someone who never handles
        /// animals hardly at all.
        /// </summary>
        public static float FondWeight(int animalsLevel, bool passion, bool child, bool kind, bool handlingClosed)
        {
            float w = 1f + Math.Max(0, Math.Min(20, animalsLevel)) / 5f;
            if (passion) w += 1.5f;
            if (child) w += 2f;
            if (kind) w += 1f;
            if (handlingClosed && !child) w *= 0.25f;
            return w;
        }

        /// <summary>
        /// Someone's score for an animal (lowest wins): an even draw from the two ids, stretched by their weight. Fixed for the
        /// pair, so the animal keeps the one it took to, and a newcomer only ever wins an animal from the rest.
        /// </summary>
        public static double Score(int seed, int id, float weight)
        {
            if (weight <= 0f) return double.MaxValue;
            uint h = AnimalNatureMath.Mix(unchecked(seed * 31 + (int)AnimalNatureMath.Mix(id)));
            double u = (h + 1.0) / (uint.MaxValue + 2.0);
            return -Math.Log(u) / weight;
        }

        /// <summary>
        /// The one an animal (its seed) takes to among these people (their ids and weights): the lowest score. -1 when there is
        /// nobody, or nobody with any weight.
        /// </summary>
        public static int Favourite(int seed, IList<int> ids, IList<float> weights)
        {
            if (ids == null || weights == null) return -1;
            int best = -1;
            double bestScore = double.MaxValue;
            for (int i = 0; i < ids.Count && i < weights.Count; i++)
            {
                double s = Score(seed, ids[i], weights[i]);
                if (s >= bestScore) continue;
                bestScore = s;
                best = ids[i];
            }
            return best;
        }

        // ---------------- How often ----------------

        /// <summary>
        /// Days between one animal's own moments of this kind at the setting's 100% (a herd's, a child's or a keeper's for the
        /// herd, the rounds and the play): a pet at the foot of the bed every three nights, at a meal every two days, a child at
        /// play every day, the herd moving off together most mornings, the rounds every two days. The rest come when their
        /// moment does (home again, the weather turning, raiders, a god near, a death).
        /// </summary>
        public static float Days(AnimalDay d)
        {
            switch (d)
            {
                case AnimalDay.FootOfBed: return 3f;
                case AnimalDay.Beg: return 2f;
                case AnimalDay.ChildPlay: return 1f;
                case AnimalDay.Herd: return 1.5f;
                case AnimalDay.Rounds: return 2f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The chance an hour of the moment, in the hours it can come: <paramref name="hours"/> a day of them, so that at the
        /// pace's 100% it comes about every <see cref="Days"/>. 0 at a pace of nothing.
        /// </summary>
        public static float HourChance(AnimalDay d, float rate, int hours)
        {
            if (rate <= 0f || hours <= 0) return 0f;
            return Math.Min(1f, rate / (Days(d) * hours));
        }

        /// <summary>The hours a moment can come in (its own part of the day), for <see cref="HourChance"/>.</summary>
        public static int HoursOf(AnimalDay d)
        {
            switch (d)
            {
                case AnimalDay.FootOfBed: return 4;
                case AnimalDay.Beg: return 15;
                case AnimalDay.ChildPlay: return 12;
                case AnimalDay.Herd: return 4;
                case AnimalDay.Rounds: return 8;
                default: return 24;
            }
        }

        /// <summary>Bedtime: from ten at night to two in the morning, when a pet goes to its person's bed.</summary>
        public static bool BedHour(int hour) => hour >= 22 || hour <= 1;

        /// <summary>The morning a pet's night at the foot of the bed is told: five to ten.</summary>
        public static bool WakingHour(int hour) => hour >= 5 && hour <= 10;

        /// <summary>Meals: from seven in the morning to nine at night.</summary>
        public static bool MealHour(int hour) => hour >= 7 && hour <= 21;

        /// <summary>A child's play: eight in the morning to seven at night.</summary>
        public static bool PlayHour(int hour) => hour >= 8 && hour <= 19;

        /// <summary>The herd moves off together in the early morning: six to nine.</summary>
        public static bool HerdHour(int hour) => hour >= 6 && hour <= 9;

        /// <summary>The rounds: nine to four.</summary>
        public static bool RoundsHour(int hour) => hour >= 9 && hour <= 16;

        /// <summary>The night, for the huddle: ten to five.</summary>
        public static bool NightHour(int hour) => hour >= 22 || hour <= 5;

        // ---------------- Who ----------------

        /// <summary>Begs at a meal: a pet up to a big dog's size (not the herd, nothing the size of a horse).</summary>
        public static bool MayBeg(float bodySize, bool livestock) => !livestock && bodySize > 0f && bodySize <= 1.6f;

        /// <summary>Sleeps at the foot of a bed: a pet up to a big dog's size.</summary>
        public static bool MaySleepAtFoot(float bodySize, bool livestock) => !livestock && bodySize > 0f && bodySize <= 1.6f;

        /// <summary>
        /// Plays chase with a child: anything up to the size of a horse that isn't a hunter bigger than a fox (a hen, a goat, a
        /// dog, a foal; never a bear or a great cat).
        /// </summary>
        public static bool PlaysWithChild(float bodySize, bool predator) => bodySize > 0f && bodySize <= 2.5f && (!predator || bodySize <= 1.2f);

        /// <summary>Comes running to someone home again: a pet always, the herd's ones half as often (a horse may; a hen rarely bothers).</summary>
        public static float GreetChance(bool companion) => companion ? 0.8f : 0.4f;

        /// <summary>The fewest of one kind that make a herd moving off together.</summary>
        public const int HerdMin = 2;

        /// <summary>The most of a herd that follow its eldest off in the morning, and the most the keeper goes round.</summary>
        public const int HerdMax = 12, RoundsMax = 5;

        /// <summary>
        /// Two of the colony's animals may become friends: two pets, or any two of different kinds (the horse and the barn's goat);
        /// never two of the herd's own kind, which would pair off in every barn.
        /// </summary>
        public static bool MayBefriend(bool companionX, bool companionY, bool sameKind) => companionX && companionY || !sameKind;

        // ---------------- Where and when ----------------

        /// <summary>
        /// Where a pet lies at the foot of a bed, best first: the cell just past the foot end of the sleeper's place (the bed
        /// faces its head's way), then the two cells beside its foot. A bed's own cells can't be stood on, so a double bed's
        /// other half drops out by itself.
        /// </summary>
        public static (int x, int z)[] FootCells(int footX, int footZ, int faceX, int faceZ) => new[]
        {
            (footX - faceX, footZ - faceZ),
            (footX + faceZ, footZ - faceX),
            (footX - faceZ, footZ + faceX)
        };

        /// <summary>Away this many hours or more (a caravan, another home) and back, someone is greeted.</summary>
        public const float GreetAwayHours = 4f;

        /// <summary>Back within this many hours, a greeting can still come.</summary>
        public const float GreetBackHours = 3f;

        /// <summary>Someone back home now is greeted: away long enough, and not back too long ago.</summary>
        public static bool Homecoming(float hoursAway, float hoursBack) => hoursAway >= GreetAwayHours && hoursBack >= 0f && hoursBack <= GreetBackHours;

        /// <summary>A herd outdoors at night lies down close together below this (°C).</summary>
        public const float HuddleBelow = -8f;

        public static bool Huddles(float outdoorTemp) => outdoorTemp < HuddleBelow;

        /// <summary>The rain or snow a pet goes in out of (the game's rain and snow rates, 0 to 1).</summary>
        public static bool Shelters(float rainRate, float snowRate) => rainRate >= 0.5f || snowRate >= 0.5f;

        /// <summary>Its person died within this many hours: it goes to lie by them.</summary>
        public const float MournWithinHours = 8f;

        /// <summary>Young born within this many days are a mother's newborns (the new litter's scene).</summary>
        public const float LitterDays = 6f;

        /// <summary>A newborn of its own: young, born within <see cref="LitterDays"/>, of the same kind.</summary>
        public static bool Newborn(float ageDays, bool sameKind) => sameKind && ageDays >= 0f && ageDays < LitterDays;

        /// <summary>A stranger this close to someone has their animal step in between (the guard's scene).</summary>
        public const float GuardRange = 18f;

        /// <summary>The night's crying out: from nine at night to four in the morning.</summary>
        public static bool CryHour(int hour) => hour >= 21 || hour <= 4;

        /// <summary>
        /// How long each lasts (ticks): by the dead, sheltering, at someone's elbow at a meal, at play, asleep at the foot of
        /// the bed (it wakes sooner when rested), the keeper beside each animal of the round, an animal at its person's side.
        /// </summary>
        public const int MournTicks = 7500, ShelterTicks = 1800, BegTicks = 700, PlayTicks = 1200, FootTicks = 15000, LookTicks = 300, SideTicks = 1800;

        /// <summary>
        /// The scenes' own (ticks): at someone's side after running to them, between them and a stranger, crying out at the
        /// dark (a call every <see cref="CryEvery"/>), a mother lying by her young, someone watching them.
        /// </summary>
        public const int GreetTicks = 240, GuardTicks = 1200, CryTicks = 1500, CryEvery = 300, LitterTicks = 2500, WatchTicks = 900;

        /// <summary>The fun of play a tick (a game's own recreation is about this).</summary>
        public const float PlayJoyPerTick = 0.00012f;
    }
}
