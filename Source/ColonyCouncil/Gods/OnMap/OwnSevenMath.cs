using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own happenings, the second seven (round 4, batch 2 of docs/gods-next-proposals.md, section C: Hush,
    /// Undertow, Helios, Selene, Murmur, Ducat, and two more for Tamiu): their own rules, beside the first seven's in the
    /// same pure class (OwnHappeningsMath: the happenings, their pace, PaceOf), with no game types so Tools/Tests reads
    /// the same numbers the game uses. What calls each, and how it plays out; none of them hurts anyone.
    /// </summary>
    public static partial class OwnHappeningsMath
    {
        // ================= Hush =================

        /// <summary>The word on the door in the frost: what it points at, the strongest first.</summary>
        public enum FrostWord
        {
            None,
            /// <summary>Someone of the colony is cold (hypothermia, or sleeping in a room below <see cref="ColdRoom"/>).</summary>
            Warm,
            /// <summary>The stores are short of food (fewer than <see cref="FoodShortDays"/> days).</summary>
            Bread,
            /// <summary>The colony burns wood to keep warm and has little left.</summary>
            Wood,
            /// <summary>Food left to rot in the stores.</summary>
            Count
        }

        /// <summary>A room colder than this, slept in, counts as someone cold; the stores this short, as food short; this little wood, as fuel short.</summary>
        public const float ColdRoom = 8f, FoodShortDays = 4f, FoodVeryShortDays = 2f, WoodShort = 60f;

        /// <summary>Food this far toward spoiling, and not kept cold, is going to waste.</summary>
        public const float WastingShare = 0.75f;

        /// <summary>The word only comes in frost: an outdoor temperature at or below this.</summary>
        public const float WordFrostBelow = 2f;

        /// <summary>A colonist eats about this much a day, for the days of food.</summary>
        public const float NutritionADay = 1.6f;

        public static float FoodDays(float nutrition, int people) => people <= 0 || float.IsNaN(nutrition) ? 99f : Math.Max(0f, nutrition) / (people * NutritionADay);

        /// <summary>The word the frost writes, by what is wrong (someone cold first, then food, wood, waste), and how strongly it calls; none in a thaw or with nothing wrong.</summary>
        public static (FrostWord word, GodPaceMath.Trigger trigger) Word(float outdoorTemp, bool hypothermia, bool coldRoom, float foodDays, bool burnsWood, float wood, bool rotting)
        {
            if (float.IsNaN(outdoorTemp) || outdoorTemp > WordFrostBelow) return (FrostWord.None, GodPaceMath.Trigger.None);
            if (hypothermia) return (FrostWord.Warm, GodPaceMath.Trigger.Strong);
            if (foodDays < FoodVeryShortDays) return (FrostWord.Bread, GodPaceMath.Trigger.Strong);
            if (coldRoom) return (FrostWord.Warm, GodPaceMath.Trigger.Ordinary);
            if (foodDays < FoodShortDays) return (FrostWord.Bread, GodPaceMath.Trigger.Ordinary);
            if (burnsWood && wood < WoodShort) return (FrostWord.Wood, GodPaceMath.Trigger.Ordinary);
            if (rotting) return (FrostWord.Count, GodPaceMath.Trigger.Ordinary);
            return (FrostWord.None, GodPaceMath.Trigger.None);
        }

        /// <summary>The word is written in the small hours and found at first light: from this hour to before that one.</summary>
        public const int WindowFrom = 5, WindowTo = 9;

        /// <summary>The frail a vigil is kept for: the old, a baby, or the sick abed.</summary>
        public const float OldFrom = 60f, BabyBelow = 3f;

        public static bool Frail(float age, bool sickAbed) => sickAbed || !float.IsNaN(age) && (age >= OldFrom || age < BabyBelow);

        /// <summary>The vigil begins at dusk (from this hour to before that one) and is judged at first light.</summary>
        public const int VigilFrom = 18, VigilTo = 22, VigilDawn = 5;

        /// <summary>The share of the night the keeper must have sat by the bed for the cold to break.</summary>
        public const float VigilHeld = 0.7f;

        public static bool VigilKept(int satTicks, int nightTicks) => nightTicks > 0 && satTicks >= VigilHeld * nightTicks;

        /// <summary>A vigil is a cold snap's: only while one is on the land and the frail are abed in it.</summary>
        public static GodPaceMath.Trigger VigilTrigger(bool coldSnap, bool frailAbed) => coldSnap && frailAbed ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        /// <summary>The longest night: the middle of winter (these days of the season, 0 to 14).</summary>
        public const int LongestFrom = 6, LongestTo = 8;

        public static bool LongestNight(bool winter, int dayOfSeason) => winter && dayOfSeason >= LongestFrom && dayOfSeason <= LongestTo;

        /// <summary>The walk comes on the longest night, or else on a night past this cold.</summary>
        public const float RingCold = -18f;

        public static GodPaceMath.Trigger RingTrigger(bool longest, float outdoorTemp) =>
            longest ? GodPaceMath.Trigger.Strong : !float.IsNaN(outdoorTemp) && outdoorTemp <= RingCold ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The walk begins in these hours of the night (from 22 to before 2, over midnight).</summary>
        public static bool RingHour(int hour) => hour >= 22 || hour < 2;

        /// <summary>How far out from the colony's middle the figure walks: past its farthest building, kept within these.</summary>
        public const int RingPast = 7, RingMin = 12, RingMax = 45;

        public static int RingRadius(float farthest) => (int)Math.Max(RingMin, Math.Min(RingMax, Math.Round((float.IsNaN(farthest) ? 0f : farthest) + RingPast)));

        /// <summary>One step of the walk: this many cells along the ring, every this many ticks; it moves this many steps on when anyone comes within reach.</summary>
        public const int RingStepCells = 4, RingStepTicks = 300, RingFlee = 3;

        /// <summary>Anyone this close and it moves on: twenty steps.</summary>
        public const float RingReach = 20f;

        /// <summary>How many steps go all the way round a ring of this radius.</summary>
        public static int RingSteps(int radius) => Math.Max(8, (int)Math.Ceiling(2.0 * Math.PI * Math.Max(1, radius) / RingStepCells));

        /// <summary>The frost on a grave: a death this recent, in lean months (winter or food short), with food left to rot.</summary>
        public const float GraveWithinDays = 2f;

        public static GodPaceMath.Trigger GraveTrigger(float diedDaysAgo, bool winter, float foodDays, bool rotting)
        {
            if (float.IsNaN(diedDaysAgo) || diedDaysAgo < 0f || diedDaysAgo > GraveWithinDays) return GodPaceMath.Trigger.None;
            bool lean = winter || foodDays < FoodShortDays;
            return lean && rotting ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;
        }

        /// <summary>The frost lies on the grave until it melts into a ring of mushrooms this many days later (the soft mother takes it back).</summary>
        public const float MeltDaysMin = 2f, MeltDaysMax = 4f;

        /// <summary>The frost is laid at dusk (from this hour to before that one).</summary>
        public const int GraveFrom = 17, GraveTo = 22;

        // ================= Undertow =================

        /// <summary>After a bargain the beasts keep away from the one who made it for this many days, and shy from within this many cells.</summary>
        public const float ShunDays = 8f, ShunNear = 5f, ShunFlee = 10f;

        /// <summary>An animal shies at most once in this many ticks; the shying is said aloud once a day at most.</summary>
        public const int ShyEvery = 5000;

        /// <summary>The dark drifts toward someone at the edge of breaking: strongly at an extreme break, ordinarily at a major one; a minor mood is too common to call it.</summary>
        public static GodPaceMath.Trigger DriftTrigger(bool extreme, bool major) =>
            extreme ? GodPaceMath.Trigger.Strong : major ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>It comes easier in the dark of the moon (the moon's face gone, as the Hunger's voice does).</summary>
        public const float DarkMoonFactor = 2f;

        /// <summary>It begins at night (from 22 to before 3, over midnight), and is gone at this hour.</summary>
        public static bool DriftHour(int hour) => hour >= 22 || hour < 3;

        public const int DriftGone = 5;

        /// <summary>It starts this far from the one it drifts to, and moves this many cells every this many ticks.</summary>
        public const float DriftFrom = 28f;
        public const int DriftCells = 2, DriftTicks = 250;

        /// <summary>It has reached them within this; it stops at the edge of a light this far from its next cell, or beside someone awake this close to them.</summary>
        public const float DriftReached = 1.8f, DriftLight = 4f, DriftCompany = 2.5f;

        public enum DriftEnd
        {
            Drifting,
            Reached,
            HeldByLight,
            HeldByCompany
        }

        /// <summary>Where the drift stands after a step: reached them, held at a light's edge or by someone sitting with them, or still drifting.</summary>
        public static DriftEnd Drift(float distToThem, bool lightAhead, bool companyBeside)
        {
            if (companyBeside) return DriftEnd.HeldByCompany;
            if (lightAhead) return DriftEnd.HeldByLight;
            return distToThem <= DriftReached ? DriftEnd.Reached : DriftEnd.Drifting;
        }

        /// <summary>The hollow ear: someone in an old ruin's room for the first time hears it this often; it lasts this many days.</summary>
        public const float EarChance = 0.35f, EarDays = 10f;

        /// <summary>A room of old ruins is one this small or smaller that holds a thing of the ancients; this many rooms are remembered as heard.</summary>
        public const int RuinRoomMax = 600, RuinsHeldAtMost = 30;

        // ================= Helios =================

        /// <summary>The day after an eclipse, from this hour, the sun looks back for these hours.</summary>
        public const int LookBackFrom = 10, LookBackTo = 14;

        /// <summary>The look back comes the day after the eclipse was last seen, and never more than this many days after.</summary>
        public static bool LookBackDue(int eclipseDay, int today) => eclipseDay >= 0 && today > eclipseDay && today - eclipseDay <= 1;

        /// <summary>The first fruit is eaten out under the noon sun: from this hour to before that one; this many people at most.</summary>
        public const int FruitFrom = 11, FruitTo = 15, FruitMost = 10;

        /// <summary>The sun's return after the longest night: at first light, these hours; this many people at most.</summary>
        public const int SunBackFrom = 6, SunBackTo = 9, SunBackMost = 12;

        /// <summary>The morning after the longest night calls it strongly, and the morning after the frost walk the more.</summary>
        public static GodPaceMath.Trigger DawnTrigger(bool afterLongest, bool afterWalk) =>
            afterWalk || afterLongest ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        // ================= Selene =================

        /// <summary>What came back by night is told on a card in the morning, from this hour to before that one.</summary>
        public const int ReturnMorningFrom = 6, ReturnMorningTo = 12;

        /// <summary>Her face in still water: at the full moon, in these hours (from 22 to before 3, over midnight).</summary>
        public static bool WaterHour(int hour) => hour >= 22 || hour < 3;

        public static GodPaceMath.Trigger WaterTrigger(bool fullMoon, bool stillWater, bool clear, bool someoneAwake) =>
            fullMoon && stillWater && clear && someoneAwake ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The watch she keeps company: in the small hours (from 1 to before 5), with her face at least half; a watch kept under arms calls the more.</summary>
        public const int WatchFrom = 1, WatchTo = 5;

        public static GodPaceMath.Trigger WatchTrigger(bool moonUp, bool clear, bool outdoorsAwake, bool drafted)
        {
            if (!moonUp || !clear || !outdoorsAwake) return GodPaceMath.Trigger.None;
            return drafted ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>The dark face kept at the door on a night with no moon: from this hour to before that one, until this hour.</summary>
        public const int DarkFaceFrom = 21, DarkFaceTo = 24, DarkFaceUntil = 3;

        public static GodPaceMath.Trigger DarkFaceTrigger(bool noMoon, bool keeperAwake) => noMoon && keeperAwake ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        // ================= Murmur =================

        /// <summary>Sung to at the well: at dusk (from this hour to before that one), this many singers at most.</summary>
        public const int SongFrom = 17, SongTo = 21, SongMost = 8;

        /// <summary>A dry spell this long calls the colony's believers to sing; this many of them at least.</summary>
        public const int SongDryDays = 6, SongBelievers = 2;

        public static GodPaceMath.Trigger SongTrigger(int dryDays, bool drought, int believers)
        {
            if (believers < SongBelievers) return GodPaceMath.Trigger.None;
            if (drought || dryDays >= SongDryDays + 4) return GodPaceMath.Trigger.Strong;
            return dryDays >= SongDryDays ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>How often the song is answered with rain: at its festival, and in a dry spell, more the nearer he is drawn.</summary>
        public static float SongAnswered(bool festival, float near) => Clamp01((festival ? 0.65f : 0.5f) + 0.3f * Clamp01(near));

        /// <summary>The rain comes this many hours after the singing, at the soonest and the latest.</summary>
        public const float RainAfterMin = 4f, RainAfterMax = 16f;

        /// <summary>The water gives back after a wet run of this many days, the more after a longer one.</summary>
        public static GodPaceMath.Trigger GiveBackTrigger(int wetRun, bool flooded)
        {
            if (flooded || wetRun >= 3) return GodPaceMath.Trigger.Strong;
            return wetRun >= 2 ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>What it gives back: a little silver, and now and then an old thing of the land's age, worn.</summary>
        public const int SilverBackMin = 12, SilverBackMax = 40;
        public const float OldThingChance = 0.5f, OldThingWorn = 0.4f, OldThingWorth = 250f;

        /// <summary>The frogs come in with the rain that breaks a dry spell this long.</summary>
        public const int FrogsDryDays = 8;

        public static GodPaceMath.Trigger FrogsTrigger(int dryDaysBefore, bool droughtEnded) =>
            droughtEnded || dryDaysBefore >= FrogsDryDays ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        /// <summary>How many frogs come, by the land's size (a large map more), and how long they stay (they go at first light).</summary>
        public static int Frogs(int mapCells) => Math.Max(6, Math.Min(16, mapCells / 4000));

        // ================= Ducat =================

        /// <summary>Of the silver gone from a hoard, this many lie as single coins along the way out, a few steps apart; the last few gone over the edge.</summary>
        public const int TrailMost = 18, TrailEvery = 3;

        public static int TrailCoins(int taken) => taken <= 1 ? 0 : Math.Min(TrailMost, Math.Max(1, taken / 2));

        /// <summary>The open purse: a few coins pressed into a visitor's hand, and their people think this much better of the colony.</summary>
        public const int PurseMin = 1, PurseMax = 5, PurseGoodwill = 2;

        /// <summary>The open purse outside its festival: one devoted to the restless gold, with friendly visitors on the land.</summary>
        public static GodPaceMath.Trigger PurseTrigger(bool festival, bool devotedGiver, bool visitors)
        {
            if (!visitors) return GodPaceMath.Trigger.None;
            if (festival) return GodPaceMath.Trigger.Strong;
            return devotedGiver ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The beetle leads to a coin: strongly with traders or visitors on the land, ordinarily with a hoard this large, never otherwise.</summary>
        public const int BeetleHoard = 1000;

        public static GodPaceMath.Trigger BeetleTrigger(bool traders, int silver) =>
            traders ? GodPaceMath.Trigger.Strong : silver >= BeetleHoard ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>It walks this far from the table to where the coin lies.</summary>
        public const float BeetleFrom = 5f, BeetleTo = 12f;

        // ================= Tamiu =================

        /// <summary>A cat comes to lie by someone near death; it waits this long after they come through before it goes.</summary>
        public const int ThresholdLiveHours = 6;

        public static GodPaceMath.Trigger ThresholdTrigger(bool dying) => dying ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        /// <summary>A body of the colony's lying unburied this many hours, this far from the colony's middle, is one the cats go and find.</summary>
        public const float LostHours = 12f, LostFar = 25f;

        public static GodPaceMath.Trigger LostTrigger(float hoursLying, float fromMiddle, bool buried) =>
            !buried && hoursLying >= LostHours && fromMiddle >= LostFar ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        // ================= Shared =================

        /// <summary>A happening that is walked to waits for its walker this long before it is given up.</summary>
        public const int WalkWaitTicks = 2500;
    }
}
