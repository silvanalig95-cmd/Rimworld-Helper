using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    // The rules of colony life's variety, part 2 (the content update, the player's go of 20 November 2026), without game
    // types, so Tools/Tests can check them: coming of age's four more paths (A8), a toy for the little one (A11), three
    // ambitions of the colony's people (A15), life goals for the other seats (A16), marks from the colony's own life (A22),
    // deeds and names of the council's life (A23), group habits from the colony's pastimes (A24), the council's seasons in
    // talk (A25), the giving day (A26), the elders' day (A28), and the colony's own events and stories.

    /// <summary>A toy for the little one (A11, LifeVariety/ToyWish.cs).</summary>
    public static class ToyMath
    {
        /// <summary>A child old enough to play with a toy and young enough to want one: three to twelve.</summary>
        public const float MinAge = 3f, MaxAge = 12f;

        /// <summary>A grown-up fond enough of the child to ask for them: as fond as this, by the game's opinion.</summary>
        public const int FondAtLeast = 30;

        /// <summary>An elder asks for any child they are not at odds with.</summary>
        public const float ElderAge = 60f;

        /// <summary>A child given a toy this way isn't asked for again for this many days.</summary>
        public const int AgainAfterDays = 40;

        public static bool ChildAge(float age) => age >= MinAge && age <= MaxAge;

        /// <summary>
        /// Who may ask for a toy for a child: a parent, an elder or a grown-up fond of them, all alike (the colony is the
        /// family: none of it follows family lines, a parent is only one of those who may), never someone at odds with the child.
        /// </summary>
        public static bool MayAsk(bool parent, float askerAge, int opinionOfChild)
        {
            if (opinionOfChild < 0) return false;
            return parent || askerAge >= ElderAge || opinionOfChild >= FondAtLeast;
        }

        /// <summary>The wish's weight: a little more for one who is fonder of the child.</summary>
        public static float Weight(int opinionOfChild) => 0.8f + Math.Max(0, Math.Min(100, opinionOfChild)) / 200f;

        public static bool GivenLately(int givenTick, int now, int dayTicks) => givenTick >= 0 && now - givenTick < AgainAfterDays * dayTicks;
    }

    /// <summary>Three ambitions of the colony's people (A15, LifeVariety/ColonyAmbitions.cs).</summary>
    public static class ColonyAmbitionMath
    {
        /// <summary>Nobody left out is proposed when this many grown colonists stand outside every group with no friend.</summary>
        public const int OutsideToPropose = 2;

        /// <summary>A friend, for "nobody left out": both think this well of each other, or the social web's friendship is this strong.</summary>
        public const int FriendOpinion = 40;
        public const float FriendTie = 30f;

        public static bool HasFriend(int bestMutualOpinion, float bestFriendTie) => bestMutualOpinion >= FriendOpinion || bestFriendTie >= FriendTie;

        /// <summary>Outside: in no group and with no friend.</summary>
        public static bool Outside(bool inGroup, bool hasFriend) => !inGroup && !hasFriend;

        /// <summary>
        /// How far nobody left out has come: the share of the grown colonists who have a group or a friend, met only when
        /// nobody is outside (so a newcomer arriving puts it back, as it should).
        /// </summary>
        public static float NobodyOutside(int grown, int outside)
        {
            if (grown <= 0) return 0f;
            if (outside <= 0) return 1f;
            return Math.Min(0.99f, (grown - outside) / (float)grown);
        }

        /// <summary>A season of kept promises: this many days running with no promise let lapse, and this many kept along the way.</summary>
        public const float PromiseDays = 15f;
        public const int PromisesKept = 2;

        /// <summary>A promise broken since the last look starts the streak again.</summary>
        public static bool StreakBroken(int brokenNow, int brokenSeen) => brokenNow > brokenSeen;

        /// <summary>How far a season of kept promises has come: both halves must be there (the days and the promises kept).</summary>
        public static float KeptProgress(float days, int kept)
        {
            float d = Math.Max(0f, Math.Min(1f, days / PromiseDays));
            float k = Math.Max(0f, Math.Min(1f, kept / (float)PromisesKept));
            return Math.Min(d, k);
        }

        /// <summary>A lighter season: this many days with nobody worn down or near breaking (the heart's strain of 55 or more).</summary>
        public const float LighterDays = 15f;
        public const float WornAt = 55f;

        public static bool Worn(float strain) => strain >= WornAt;
    }

    /// <summary>Life goals for the other seats (A16, LifeVariety/SeatSparks.cs): the seat's own work that sets someone on it.</summary>
    public static class SeatSparkMath
    {
        /// <summary>Spark keys, each the work of one seat.</summary>
        public const string Harvest = "seat:harvest", Mended = "seat:mended", DayKept = "seat:daykept", Tamed = "seat:tamed", RiteLed = "seat:riteled",
            Greeted = "seat:greeted", Taught = "seat:taught";

        public static readonly string[] All = { Harvest, Mended, DayKept, Tamed, RiteLed, Greeted, Taught };

        /// <summary>The record each spark grows from (the game's own, or Rimfolk's: Defs/Misc/), and how much of it in a day sparks it.</summary>
        public static string RecordOf(string spark)
        {
            switch (spark)
            {
                case Harvest: return "PlantsHarvested";
                case Mended: return "Council_QuarrelsMended";
                case DayKept: return "Council_DaysKept";
                case Tamed: return "AnimalsTamed";
                case RiteLed: return "Council_RitualsLed";
                case Greeted: return "Council_CampsGreeted";
                case Taught: return "Council_StudentsTaught";
                default: return null;
            }
        }

        /// <summary>A big harvest is a day's real work in the fields (twenty plants or more); any of the rest counts once.</summary>
        public static float Needed(string spark) => spark == Harvest ? 20f : 1f;

        public static bool Sparks(string spark, float gainedToday) => gainedToday >= Needed(spark);

        /// <summary>The seat whose work it is (an OfficeDef's name), so the seat's own holder isn't set on what they already hold.</summary>
        public static string SeatOf(string spark)
        {
            switch (spark)
            {
                case Harvest: return "Council_Office_Reeve";
                case Mended: return "Council_Office_Peacemaker";
                case DayKept: return "Council_Office_Keeper";
                case Tamed: return "Council_Office_Herdmaster";
                case RiteLed: return "Council_Office_Chaplain";
                case Greeted: return "Council_Office_Warden";
                case Taught: return "Council_Office_Elder";
                default: return null;
            }
        }
    }

    /// <summary>Deeds and names of the council's life (A23, LifeVariety/LifeDeeds.cs).</summary>
    public static class CouncilNameMath
    {
        /// <summary>A name for long service: two years (120 days) in one of these seats.</summary>
        public const int NameDays = 120;

        /// <summary>Word-Kept: two promises in a seat judged kept.</summary>
        public const int WordKeptPromises = 2;

        /// <summary>The record of days in a seat that has none yet (the physician's and the marshal's have their own), or null.</summary>
        public static string DaysRecord(string office)
        {
            switch (office)
            {
                case "Council_Office_Quartermaster": return "Council_DaysAsQuartermaster";
                case "Council_Office_Reeve": return "Council_DaysAsReeve";
                case "Council_Office_Herdmaster": return "Council_DaysAsHerdmaster";
                case "Council_Office_Steward": return "Council_DaysAsSteward";
                case "Council_Office_Elder": return "Council_DaysAsElder";
                default: return null;
            }
        }

        public static readonly string[] Seats = { "Council_Office_Quartermaster", "Council_Office_Reeve", "Council_Office_Herdmaster", "Council_Office_Steward", "Council_Office_Elder" };
    }

    /// <summary>The kinds of gift the giving day draws from the stores (A26), in the order they're looked for.</summary>
    public enum GiftKind
    {
        /// <summary>A toy for a child (Vanilla Christmas Expanded's).</summary>
        Toy,
        /// <summary>Something sweet: gingerbread, milk and cookies, chocolate.</summary>
        Sweet,
        /// <summary>Something warm to wear in winter: a hat, a scarf, a jacket.</summary>
        Warm,
        /// <summary>A book.</summary>
        Book,
        /// <summary>Something to drink: eggnog, hot cocoa, tea, beer (never a drink their faith or their own rule forbids).</summary>
        Drink,
        /// <summary>A small carving or sculpture.</summary>
        Carving
    }

    /// <summary>The giving day, a winter custom (A26, LifeVariety/GivingDay.cs).</summary>
    public static class GivingMath
    {
        /// <summary>
        /// Names drawn so everyone gives and everyone gets, nobody to themselves: giver i gives to the one at
        /// <c>result[i]</c>. The order is shuffled with <paramref name="seed"/>, then each gives to the next in the ring.
        /// With fewer than two, nobody.
        /// </summary>
        public static int[] Draw(int n, int seed)
        {
            if (n < 2) return new int[0];
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            var r = new Random(seed);
            for (int i = n - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                int t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            var to = new int[n];
            for (int k = 0; k < n; k++) to[order[k]] = order[(k + 1) % n];
            return to;
        }

        /// <summary>
        /// What is looked for first, by what the one it's for wants or lacks: a child a toy and then something sweet; a
        /// grown-up short of warm things in winter something warm; a reader a book; someone who may drink, a drink; then
        /// a carving, then something sweet, for anyone.
        /// </summary>
        public static List<GiftKind> Order(bool child, bool lacksWarm, bool reader, bool mayDrink)
        {
            var list = new List<GiftKind>();
            if (child)
            {
                list.Add(GiftKind.Toy);
                list.Add(GiftKind.Sweet);
                if (lacksWarm) list.Add(GiftKind.Warm);
                list.Add(GiftKind.Carving);
                return list;
            }
            if (lacksWarm) list.Add(GiftKind.Warm);
            if (reader) list.Add(GiftKind.Book);
            if (mayDrink) list.Add(GiftKind.Drink);
            list.Add(GiftKind.Carving);
            list.Add(GiftKind.Sweet);
            if (!lacksWarm) list.Add(GiftKind.Warm);
            if (!reader) list.Add(GiftKind.Book);
            return list;
        }

        /// <summary>The gifts are taken from the stores in the afternoon, from this hour (local time).</summary>
        public const int TakeFrom = 13;
        /// <summary>The supper is from 17; the gifts are handed round after it, from this hour, and the evening ends at midnight.</summary>
        public const int GiveFrom = 19, EveningEnds = 23;

        /// <summary>A gift worth giving, in silver, at most (no one hands over the colony's best rifle on the giving day).</summary>
        public const float MostWorth = 400f;

        /// <summary>The giving day and midwinter fall on the same day: the giving day's gifts are the evening's presents, and midwinter hands none of its own.</summary>
        public static bool SameDay(int givingDay, int midwinterDay) => givingDay >= 0 && givingDay == midwinterDay;

        /// <summary>The children only: each child gets one gift from the grown-up who thinks best of them, none given twice by one grown-up while another is free.</summary>
        public static int[] ForChildren(IList<int[]> fondness)
        {
            // fondness[c][g]: how fond grown-up g is of child c. Each child the fondest grown-up who has given least so far.
            if (fondness == null || fondness.Count == 0) return new int[0];
            int grown = fondness[0].Length;
            var given = new int[grown];
            var result = new int[fondness.Count];
            for (int c = 0; c < fondness.Count; c++)
            {
                int best = -1;
                for (int g = 0; g < grown; g++)
                {
                    if (fondness[c][g] < 0) continue;
                    if (best < 0 || given[g] < given[best] || (given[g] == given[best] && fondness[c][g] > fondness[c][best])) best = g;
                }
                result[c] = best;
                if (best >= 0) given[best]++;
            }
            return result;
        }
    }

    /// <summary>The elders' day, an autumn custom (A28, LifeVariety/EldersDay.cs).</summary>
    public static class EldersMath
    {
        /// <summary>An elder: sixty or more in human years, or the one who holds the elder's seat.</summary>
        public const float ElderAge = 60f;

        public static bool IsElder(float humanAge, bool holdsElderSeat) => holdsElderSeat || humanAge >= ElderAge;

        /// <summary>Asked when two are elders, or the elder's seat is held at home.</summary>
        public static bool MayAsk(int elders, bool elderSeatHeld) => elders >= 2 || (elderSeatHeld && elders >= 1);

        /// <summary>The one who asks is young: under thirty.</summary>
        public const float YoungUnder = 30f;

        public static bool Young(float humanAge) => humanAge < YoungUnder;

        /// <summary>At most this many children make something for the elders, each for one elder.</summary>
        public const int MostGifts = 4;

        /// <summary>The children's gifts go round the elders in turn: child k makes something for elder k mod the elders.</summary>
        public static int ElderFor(int child, int elders) => elders <= 0 ? -1 : child % elders;
    }

    /// <summary>Group habits from the colony's pastimes (A24, Social/Groups/GroupHabits.cs).</summary>
    public static class PastimeMath
    {
        /// <summary>The four new habits' "where": fishing, the hot water, a pot of tea or coffee, cards or chess or darts.</summary>
        public const string Fish = "fish", Bath = "bath", Cup = "cup", Games = "games";

        public static readonly string[] Wheres = { Fish, Bath, Cup, Games };

        /// <summary>The fishing jobs: Odyssey's and PokéWorld's.</summary>
        public static bool FishingJob(string jobDef) => jobDef == "Fish" || jobDef == "PW_Fish";

        /// <summary>The hot water: Dubs Bad Hygiene's hydrotherapy (its pool, hot tub and sauna) and ReGrowth's hot spring.</summary>
        public static bool BathJob(string jobDef, string joyKind) => joyKind == "Hydrotherapy" || jobDef == "RG_Bathe";

        /// <summary>Cards, chess and darts: the game's poker and chess, Vanilla Furniture Expanded's darts.</summary>
        public static bool GameJob(string jobDef) => jobDef == "Play_Poker" || jobDef == "Play_Chess" || jobDef == "Play_DartsBoard";
    }

    /// <summary>The council's seasons in talk and social events (A25, LifeVariety/CouncilSeasons.cs).</summary>
    public static class CouncilSeasonMath
    {
        /// <summary>The social events' kinds of the council's seasons.</summary>
        public const string Speech = "speech", Handover = "handover", PromiseWell = "promisewell", PromiseBadly = "promisebadly", RivalBackers = "rivalbackers",
            Petition = "petition", ElderWord = "elderword";

        public static readonly string[] Kinds = { Speech, Handover, PromiseWell, PromiseBadly, RivalBackers, Petition, ElderWord };

        /// <summary>A holder young enough for an elder's word: under thirty, and not an elder themselves.</summary>
        public const float YoungHolder = 30f;

        /// <summary>The old holder shows the new one the seat's things within this many days of the handover.</summary>
        public const int HandoverDays = 6;

        /// <summary>
        /// Whom someone leans to in the vote, as the talk's lines say it (from the vote's own lean, so the words match):
        /// themselves, the holder of the seat, someone else standing, or nobody yet (two within a hair of each other).
        /// </summary>
        public static string LeanTag(bool toSelf, bool toHolder, bool undecided) => undecided ? "undecided" : toSelf ? "lean:self" : toHolder ? "lean:holder" : "lean:other";

        /// <summary>Two candidates within this much of each other in someone's lean: they haven't made up their mind.</summary>
        public const float Undecided = 0.6f;

        public static bool IsUndecided(float best, float second) => best - second < Undecided;
    }

    /// <summary>The colony's own events (LifeVariety/ColonyEvents.cs): what makes each come up.</summary>
    public static class ColonyEventMath
    {
        /// <summary>A quarrel at supper: two this much at odds (both ways), at the supper hours.</summary>
        public const int QuarrelOpinion = -15;
        public const int SupperFrom = 17, SupperTo = 21;

        public static bool AtOdds(int ab, int ba) => ab <= QuarrelOpinion && ba <= QuarrelOpinion;

        /// <summary>The newcomer at the edge: here this many days at most, outside every group.</summary>
        public const int NewcomerDays = 20;

        /// <summary>Short in the stores: fewer days of food than this.</summary>
        public const float ShortDays = 5f;

        /// <summary>A wish left waiting: this share of its time gone, not promised.</summary>
        public const float WaitedShare = 0.6f;

        /// <summary>A broken promise still sore: within this many days of it.</summary>
        public const float SoreDays = 10f;

        /// <summary>The old song: an elder (sixty or more).</summary>
        public const float SingerAge = 60f;

        /// <summary>A night with the herd: an animal of the colony this close to giving birth (the pregnancy's share gone).</summary>
        public const float NearTerm = 0.9f;

        /// <summary>Hands for the last field: this many ripe plants waiting in the colony's fields, in autumn.</summary>
        public const int RipePlants = 30;

        /// <summary>A vow in the storm: within this many days of a storm, a believer with a god.</summary>
        public const int StormDays = 2;
    }

    /// <summary>The colony's own personal stories (LifeVariety/ColonyStories.cs).</summary>
    public static class ColonyStoryMath
    {
        /// <summary>A promise waited on: promised, and still open this many days later.</summary>
        public const float PromiseWaitDays = 4f;

        /// <summary>A prayer for a friend: as close as this (the closeness of the stories), and the friend ill.</summary>
        public const float FriendClose = 40f;

        /// <summary>An unexpected gift: given by someone they thought little of (under this), and only within this many days.</summary>
        public const int StrangerOpinion = 10;
        public const int GiftDays = 8;

        /// <summary>A custom of their own: the custom kept once (or twice), not yet a tradition.</summary>
        public static bool CustomStillGrowing(int keptYears, bool tradition) => !tradition && keptYears >= 1 && keptYears < 3;

        /// <summary>The year's aim: said at this year's first supper, and the year not yet half over.</summary>
        public static bool AimFresh(int saidYear, int year, int dayOfYear) => saidYear == year && dayOfYear < 30;
    }
}
