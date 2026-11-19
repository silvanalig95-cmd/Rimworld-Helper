using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 5 (docs/peoples-events-proposals.md, items 35, 36, 37, 38, 39, 41, 42,
    /// 43, 44, 45 and 46; Peoples/Events5/): the Quiet Bell's gravewardens when the field stirs, the mourners from the other
    /// side, the grave-garden and the field of the fallen; the Circuit of Tamers' bond freely given, the egg from the crater
    /// and the ring; the Wheel's mending by night, a piece made better, a wagon of the colony's own and the yard that stops by.
    /// Shared with the tests (Tools/Tests/PeopleEvents5Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents5Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Round5(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 5f) * 5;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        // ================= 35. The field that stirs (the Quiet Bell) =================

        /// <summary>Three gravewardens come at a run.</summary>
        public const int StirWardens = 3;

        /// <summary>Once a rising, at this chance (the storytellers' lean on visitors), within the hour; not again for so many days.</summary>
        public const float StirChance = 0.5f;

        public const int StirGapDays = 15;

        /// <summary>The graves they dig for the risen they put down, at most.</summary>
        public const int StirGraves = 12;

        /// <summary>Their thanks for the fight; more for the colony's promise to bury its dead; less when that promise is broken within a year.</summary>
        public const int StirGoodwill = 3, PromiseGoodwill = 2, PromiseBrokenGoodwill = -5, PromiseDays = 60;

        /// <summary>The risen are the colony's unburied dead: humanlike bodies that lay on its land before they rose.</summary>
        public static bool Risen(bool wasLyingHere, bool shambler) => wasLyingHere && shambler;

        /// <summary>A promise to bury the dead, broken: the dead rose again while it held (within its days).</summary>
        public static bool PromiseBroken(int promisedTick, int nowTick) => promisedTick >= 0 && nowTick - promisedTick <= PromiseDays * DayTicks;

        // ================= 36. Mourners from the other side (the Quiet Bell) =================

        /// <summary>A season after the Bell buried them, their people's mourners come.</summary>
        public const int MournAfterDays = 15;

        /// <summary>A mourners' record unanswered this long after its day is let go.</summary>
        public const int MournExpireDays = 30;

        /// <summary>An hour at the graves.</summary>
        public const int MournHours = 1;

        public const int MournersCount = 2;

        /// <summary>Let through, their people warm a little; more with one of the colony beside them; turned away, the Bell is saddened.</summary>
        public const int MournGoodwill = 5, MournWithGoodwill = 8, MournTurnedBellGoodwill = -3;

        /// <summary>At war with the colony, their people may talk of a truce after: at this chance, always with one of the colony beside them.</summary>
        public static float TruceChance(bool withColonist) => withColonist ? 1f : 0.5f;

        public const int MaxMourns = 6;

        // ================= 37. A grave-garden for the colony (the Quiet Bell) =================

        /// <summary>A grave-garden needs this many of the colony's graves together.</summary>
        public const int GardenMinGraves = 3;

        /// <summary>The graves gathered into it: within this of the cluster's middle.</summary>
        public const float GardenGather = 10f;

        /// <summary>Its ground: from the bell-post to the farthest of its graves and a little beyond, within reason.</summary>
        public static float GardenRadius(float farthestGrave) => Clamp((float.IsNaN(farthestGrave) ? 0f : farthestGrave) + 2f, 6f, 14f);

        public const int MaxStones = 30, MaxRoses = 10;

        /// <summary>Three days' work (the whole garden), or half a day (the bell-post alone).</summary>
        public const int GardenDays = 3, PostOnlyHours = 8;

        public const int StoneTicks = 900, RoseTicks = 700, PostTicks = 5000;

        /// <summary>Their work by day only.</summary>
        public static bool WorkHour(int hour) => hour >= 7 && hour <= 19;

        public const int GardenGoodwill = 4, PostGoodwill = 2;

        // ================= 38. The field of the fallen (the Quiet Bell) =================

        /// <summary>After a fight leaves a dozen dead or more on the land.</summary>
        public const int FallenMin = 12;

        /// <summary>The row of graves they dig, at most.</summary>
        public const int FallenGraves = 16;

        /// <summary>The bell at each dusk.</summary>
        public static bool Dusk(int hour) => hour == 18;

        /// <summary>The mourners of the dead's peoples come once the camp has stood this long; three peoples at most.</summary>
        public const float MournersDay = 1.5f;

        public const int FallenPeoples = 3;

        /// <summary>Each people thinks better of the colony, by how many of theirs lie in the field.</summary>
        public static int FallenGoodwill(int dead) => Clamp(2 + Math.Max(0, dead), 3, 8);

        /// <summary>It can turn, once a stay each: the field stirs (with Anomaly), or the dead's people come for them.</summary>
        public const float FieldStirChance = 0.12f, ClaimChance = 0.2f;

        /// <summary>What they ask for the graves.</summary>
        public const int FallenWood = 40, FallenCloth = 20;

        /// <summary>Talked out of taking their dead: the talker's Social, as answers' risks are (3.5% a level).</summary>
        public static float ClaimTalkFails(int social) => Clamp(0.75f - 0.035f * Math.Max(0, social), 0.05f, 0.95f);

        public const int ClaimGiveGoodwill = 4, ClaimKeptGoodwill = 2, ClaimRefusedGoodwill = -4;

        // ================= 39. The bond freely given (the Circuit of Tamers) =================

        /// <summary>An afternoon at the pen.</summary>
        public const int BondTicks = 9000;

        /// <summary>The hand's lesson, or the few words alone.</summary>
        public const float BondXp = 1500f, WordsXp = 500f;

        public const int BondGoodwill = 3;

        // ================= 41. An egg from the crater (the Circuit of Tamers) =================

        /// <summary>The player's beast mods whose eggs come first (by package), the game's own after.</summary>
        public static readonly string[] EggPackages =
        {
            "spincrus.dinosauria", "spino.megafauna", "regrowth.botr.core", "regrowth.botr.boilingforest", "mlie.regrowthextinctanimals", "sarg.alphaanimals",
            "themoonitself.pokeworld"
        };

        /// <summary>Whoever is this near the egg, or carries it, has tended it for the hour.</summary>
        public const float TendCells = 3f;

        /// <summary>The one who tended it most: the most hours, else the keeper named on the card.</summary>
        public static int Most(int[] hours)
        {
            if (hours == null || hours.Length == 0) return -1;
            int best = -1, most = 0;
            for (int i = 0; i < hours.Length; i++)
                if (hours[i] > most)
                {
                    most = hours[i];
                    best = i;
                }
            return best;
        }

        /// <summary>The tamer comes back a season after it hatched.</summary>
        public const int GrownAfterDays = 15;

        public const int EggGoodwill = 3, GrownGoodwill = 4, GrownWordsGoodwill = 2;

        /// <summary>A crater egg unhatched this long is let go (lost, or a kind that takes too long).</summary>
        public const int EggForgetDays = 120;

        public const int MaxEggs = 4;

        /// <summary>The look-over when the tamer comes back: an hour with the grown beast.</summary>
        public const int LookTicks = 2500;

        // ================= 42. The Circuit's ring (the Circuit of Tamers) =================

        /// <summary>A bout each afternoon, two of the colony's challengers a day at most.</summary>
        public static bool BoutHour(int hour) => hour >= 13 && hour <= 17;

        public const int ChallengesPerDay = 2;

        /// <summary>The colony's best: the most bouts won, one at least.</summary>
        public static bool Champion(int wins) => wins >= 1;

        public const int ChampionGoodwill = 5, RingGoodwill = 2;

        // ================= 43. Mend it tonight (the Wheel) =================

        /// <summary>The night after a raid: a raid within the last day, after dark.</summary>
        public static bool MendHour(int hour) => hour >= 19 && hour <= 23;

        public static bool AfterRaid(int raidTick, int nowTick) => raidTick >= 0 && nowTick - raidTick >= 0 && nowTick - raidTick <= DayTicks;

        public const float MendChance = 0.5f;

        public const int MendGapDays = 10, MinDamaged = 3, MaxMended = 24, MaxGear = 12, MendCrew = 3;

        /// <summary>A little silver, by the damage (hit points missing in all).</summary>
        public static int MendSilver(int missing) => Clamp(Round10(30f + Math.Max(0, missing) * 0.05f), 40, 250);

        /// <summary>Paid in silver alone, the gear kept: the silver and some of the gear's worth besides.</summary>
        public static int MendSilverOnly(int missing, float gearWorth) => Clamp(MendSilver(missing) + Round10(Math.Max(0f, float.IsNaN(gearWorth) ? 0f : gearWorth) * 0.4f), 60, 600);

        /// <summary>How long one piece takes, by its damage.</summary>
        public static int MendTicks(int missing) => Clamp(Math.Max(0, missing) * 6, 300, 2500);

        /// <summary>The raiders' gear that counts as broken: worn below this share of its hit points.</summary>
        public const float BrokenShare = 0.6f;

        public static bool Broken(int hp, int max, bool tainted) => tainted || max > 0 && hp < max * BrokenShare;

        public const int MendGoodwill = 3;

        // ================= 44. Made better (the Wheel) =================

        /// <summary>The game's qualities in order (awful 0 to legendary 6); made better one step, masterwork at most.</summary>
        public const int Masterwork = 5;

        public static bool Betterable(int quality) => quality >= 0 && quality < Masterwork;

        public static int Better(int quality) => Clamp(quality + 1, 0, Masterwork);

        /// <summary>Armour enough to be worth the cart: its sharp armour at least this.</summary>
        public const float MinArmour = 0.2f;

        public static int BetterSilver(float worth) => Clamp(Round10(Math.Max(0f, float.IsNaN(worth) ? 0f : worth) * 0.3f), 80, 800);

        public static int BetterSteel(float worth) => Clamp(Round5(Math.Max(0f, float.IsNaN(worth) ? 0f : worth) * 0.05f), 15, 75);

        /// <summary>Paid in silver alone: half again the silver.</summary>
        public static int BetterSilverOnly(float worth) => Clamp(Round10(BetterSilver(worth) * 1.5f), 120, 1200);

        /// <summary>A day at their cart.</summary>
        public const int BetterHours = 24;

        public const int HandTicks = 900, BetterGoodwill = 2;

        // ================= 45. A wagon of our own (the Wheel) =================

        /// <summary>Vanilla Vehicles Expanded's vehicle for the colony's age (0 tribal to 3 spacer): the wagon before the industrial age, a small truck after.</summary>
        public static string[] WagonDefs(int age) => age <= 1 ? new[] { "VVE_Wagon" } : new[] { "VVE_Mule", "VVE_Highwayman" };

        /// <summary>What it is made of, from the colony's stores: wood and some steel for the wagon, steel and components for a truck.</summary>
        public static (string def, int count)[] WagonGoods(int age) =>
            age <= 1 ? new[] { ("WoodLog", 120), ("Steel", 30) } : new[] { ("Steel", 150), ("ComponentIndustrial", 5) };

        /// <summary>The fixer's silver for the work.</summary>
        public static int WagonFee(int age) => age <= 1 ? 150 : 350;

        /// <summary>A day's work at the gate, in turns at the frame.</summary>
        public const int WagonHours = 24, WagonTurnTicks = 2500;

        public const int WagonGoodwill = 4;

        public const float WagonLessonXp = 2500f;

        // ================= 46. The Wheel stops by (the Wheel) =================

        /// <summary>A Wheel town's move that brings it within so many tiles of a home brings its yard.</summary>
        public const float NearTiles = 6f;

        public static bool Near(float tiles) => !float.IsNaN(tiles) && tiles >= 0f && tiles <= NearTiles;

        /// <summary>They come within the day of the move.</summary>
        public const int YardMinHours = 2, YardMaxHours = 10;

        /// <summary>It can turn, once a stay each: a price argued, or the yard staying two days longer.</summary>
        public const float QuarrelChance = 0.3f, StayLongerChance = 0.35f;

        public const int StayLongerDays = 2;

        public const float QuarrelTension = 6f, StayLongerTension = 2f;

        /// <summary>After an argument over a price, the rest of the stay's mending costs a tenth more.</summary>
        public const float SourPrice = 1.1f;

        /// <summary>The yard's mending: about a third of what the damage takes off the pieces' worth, as the tinker's.</summary>
        public static int YardMendPrice(float missingWorth, bool sour) => Clamp(Round5(Math.Max(0f, float.IsNaN(missingWorth) ? 0f : missingWorth) * 0.3f * (sour ? SourPrice : 1f)), 25, 600);

        public const int YardGoodwill = 3;
    }
}
