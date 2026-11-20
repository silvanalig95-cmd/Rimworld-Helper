using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    // The rules of colony life's integrations, part 2 (the content update, the player's go of 20 November 2026), without
    // game types, so Tools/Tests can check them. Each class is one item's: a place for the group (A10), the wished-for thing
    // tried out (A12), a wish talked up (A13), an ambition reached kept on the calendar (A14), Vanilla Aspirations Expanded's
    // aims (A17), the year's first supper (A27), the year read aloud (A29), and the lines said in turn at a gathering.

    /// <summary>Lines said in turn at a gathering (ColonyLife/Readings.cs).</summary>
    public static class ReadingMath
    {
        /// <summary>The pause after a line is said, so the bubble can be read: ten seconds at normal speed.</summary>
        public const int GapTicks = 600;

        /// <summary>Nothing said this look (nobody there whose turn it is yet): looked at again on the next pass.</summary>
        public const int LookAgainTicks = 250;

        /// <summary>The year's writer away from the table this many looks running: the rest of the reading is let go.</summary>
        public const int MissedLooks = 8;

        public static bool GivenUp(int missed) => missed >= MissedLooks;
    }

    /// <summary>What someone does with the thing they wished for when they first try it out (ColonyLife/TryOuts.cs).</summary>
    public enum TryAct
    {
        /// <summary>Sits in it (a chair, an armchair, a bench).</summary>
        Sit,
        /// <summary>Lies on it a moment (a bed).</summary>
        Lie,
        /// <summary>Warms their hands at it (a fire, a heater).</summary>
        Warm,
        /// <summary>Stands before it a while and looks it over (a work of art, anything else).</summary>
        Look,
        /// <summary>Walks into the room and looks round it (a room of their own, a room made finer).</summary>
        Room
    }

    /// <summary>A12: the wished-for thing, tried out.</summary>
    public static class TryOutMath
    {
        /// <summary>The try-out waits for their free time within a day of the wish being met; after that the thanks are said where they are.</summary>
        public const int WithinTicks = 60000;

        /// <summary>A friend nearby admires it now and then.</summary>
        public const float AdmireChance = 0.35f;

        /// <summary>A friend is someone who thinks this well of them.</summary>
        public const int FriendOpinion = 20;

        /// <summary>
        /// What they do with it: a room is walked into and looked round, a bed lain on, a seat sat in, a fire or a heater
        /// warmed at, anything else (art, a bookcase, a telescope) looked over.
        /// </summary>
        public static TryAct Act(bool room, bool bed, bool seat, bool heat)
        {
            if (room) return TryAct.Room;
            if (bed) return TryAct.Lie;
            if (seat) return TryAct.Sit;
            if (heat) return TryAct.Warm;
            return TryAct.Look;
        }

        /// <summary>How long they stay with it, in ticks.</summary>
        public static int StayTicks(TryAct act)
        {
            switch (act)
            {
                case TryAct.Sit: return 600;
                case TryAct.Lie: return 450;
                case TryAct.Warm: return 500;
                case TryAct.Room: return 500;
                default: return 400;
            }
        }

        /// <summary>
        /// The kinds of wish that leave a thing to try out (the promise watch's own words, KeptKind): a thing, a bed, a room
        /// (a room of their own too). Stocks, assignments and pets are not tried out.
        /// </summary>
        public static bool TriedOut(string keptKind) => keptKind == "thing" || keptKind == "bed" || keptKind == "room" || keptKind == "ownroom";

        /// <summary>Still waiting for their free time: within the day.</summary>
        public static bool StillWaits(int since, int now) => now - since < WithinTicks;
    }

    /// <summary>A13: a wish talked up finds backers.</summary>
    public static class TalkBackMath
    {
        /// <summary>
        /// The chance a listener who would want the same joins it there and then: half, more for a friend of the one
        /// talking it up, less for someone who dislikes them (the wish roll's own bond, <see cref="Bond"/>), at most nine in ten.
        /// </summary>
        public static float JoinChance(float warmth) => Math.Min(0.9f, 0.5f * Bond(warmth));

        /// <summary>Friends back each other's petitions; rivals don't (the wish roll's measure, CouncilGameComponent.AddOption).</summary>
        public static float Bond(float warmth) => Math.Max(0.2f, Math.Min(2.5f, 1f + warmth / 60f));

        /// <summary>A petition grows by talk once a day at most.</summary>
        public static bool MayGrow(int today, int lastDay) => today != lastDay;
    }

    /// <summary>The place a group's habit wants (A10): what the group asks the council for.</summary>
    public enum GroupPlaceKind
    {
        None,
        /// <summary>The fireside: a fire pit with seats round it.</summary>
        Fire,
        /// <summary>The readers: a bookcase with a chair near it.</summary>
        Books,
        /// <summary>The game night: a game table.</summary>
        Games,
        /// <summary>The musicians: an instrument.</summary>
        Music,
        /// <summary>The faithful: a shrine.</summary>
        Shrine
    }

    /// <summary>A10: a place for the group.</summary>
    public static class GroupPlaceMath
    {
        /// <summary>Those of the group at home: the anchor and at least two more (a group's gathering asks three, GroupHabits.Daily).</summary>
        public const int MinAtHome = 3;

        /// <summary>How far round a fire or a bookcase its seats may stand.</summary>
        public const float SeatRadius = 4f;

        /// <summary>The place a habit happens at, by where it is kept (GroupHabitDef.where): none for those that need no place of their own.</summary>
        public static GroupPlaceKind KindOf(string where)
        {
            switch (where)
            {
                case "fire": return GroupPlaceKind.Fire;
                case "read": return GroupPlaceKind.Books;
                case "rec": return GroupPlaceKind.Games;
                case "music": return GroupPlaceKind.Music;
                case "prayer": return GroupPlaceKind.Shrine;
                default: return GroupPlaceKind.None;
            }
        }

        /// <summary>Seats a place wants with it: two round a fire, one by the books, none for the rest.</summary>
        public static int SeatsWanted(GroupPlaceKind k) => k == GroupPlaceKind.Fire ? 2 : k == GroupPlaceKind.Books ? 1 : 0;

        /// <summary>
        /// Which of the group's habits it asks a place for: the first (in the group's own order) that wants a place of its
        /// own; -1 when none does. When the colony already has that place, the group asks for nothing (the habit can happen).
        /// </summary>
        public static int FirstWanting(IList<GroupPlaceKind> habits)
        {
            if (habits == null) return -1;
            for (int i = 0; i < habits.Count; i++)
                if (habits[i] != GroupPlaceKind.None) return i;
            return -1;
        }

        /// <summary>A close group (the card's "close") with enough of its people at home may ask.</summary>
        public static bool MayAsk(float cohesion, int atHome) => cohesion >= GroupMath.CloseAt && atHome >= MinAtHome;

        /// <summary>A gathering at the group's own place can be called: any habit with a place, once the place stands.</summary>
        public static bool CalledAtPlace(GroupPlaceKind k, bool placeStands) => k != GroupPlaceKind.None && placeStands;
    }

    /// <summary>A14: an ambition reached, kept on the calendar.</summary>
    public static class AmbitionDayMath
    {
        /// <summary>The calendar keeps at most this many of the days ambitions were reached.</summary>
        public const int KindLimit = 3;

        /// <summary>
        /// What the day means: more for an ambition the colony built from little (its effort, PressureMath.EffortFactor, 0 to 1)
        /// and for a longer one, between a new crop and the first harvest.
        /// </summary>
        public static float Weight(float effort, float days) =>
            22f + 10f * Math.Min(Math.Max(effort, 0f), 1f) + 0.25f * Math.Min(Math.Max(days, 0f), 40f);
    }

    /// <summary>A17: Vanilla Aspirations Expanded's aims, marked by the colony.</summary>
    public static class ModAimsMath
    {
        /// <summary>The mod's own names start so.</summary>
        public const string Prefix = "VAspirE_";

        /// <summary>
        /// Aims the colony wouldn't raise a cup to (to kill someone, to overdose, to eat human meat, a rival dead, pain sought
        /// out, the flesh's dark arts, a lover's bed): only their journal notes them.
        /// </summary>
        public static readonly HashSet<string> NotToasted = new HashSet<string>
        {
            "KillSomeone", "Overdose", "ConsumeHumanMeat", "DeadRival", "AchieveExcruciatingPain", "BecomeDisliked", "BurnSomeLuciferium",
            "ConsumeLuciferium", "ConsumeOpium", "GetBlackoutDrunk", "GetDigested", "GetFiveBurnScars", "LearnToShapeFlesh",
            "NuzzleWithTheFleshling", "BecomeInhuman", "GetEntombed", "BecomeAHentaiCharacter", "GetSomeLovin", "GetJoywired",
            "BecomeSanguophage", "ChainsawHands", "JoinTheDeserters"
        };

        /// <summary>The aim's own name without the mod's prefix.</summary>
        public static string Bare(string defName) =>
            defName == null ? "" : defName.StartsWith(Prefix, StringComparison.Ordinal) ? defName.Substring(Prefix.Length) : defName;

        /// <summary>One the colony raises a cup to: any of the mod's aims but those it wouldn't.</summary>
        public static bool Toasted(string defName) => !string.IsNullOrEmpty(defName) && !NotToasted.Contains(Bare(defName));

        /// <summary>
        /// Which of the mod's aims a fulfilment letter is about: the one whose label its text names (ignoring case), the
        /// longest such label when several do ("become a parent" over "become"). -1 when none is named.
        /// </summary>
        public static int Match(string text, IList<string> labels)
        {
            if (string.IsNullOrEmpty(text) || labels == null) return -1;
            string t = text.ToLowerInvariant();
            int best = -1, bestLength = 0;
            for (int i = 0; i < labels.Count; i++)
            {
                string l = labels[i];
                if (string.IsNullOrEmpty(l) || l.Length < 4) continue;
                if (t.IndexOf(l.ToLowerInvariant(), StringComparison.Ordinal) < 0) continue;
                if (l.Length > bestLength)
                {
                    best = i;
                    bestLength = l.Length;
                }
            }
            return best;
        }

        /// <summary>
        /// What the mod's fulfilment need adds to the heart's need for purpose (Heart/HeartNeeds.cs): from -10 when it is
        /// empty to +10 when it is full, nothing at half.
        /// </summary>
        public static float Purpose(float level) => 20f * (Math.Min(Math.Max(level, 0f), 1f) - 0.5f);
    }

    /// <summary>How an aim said at the year's first supper turned out, for the year letter (A27).</summary>
    public enum AimOutcome
    {
        /// <summary>Reached.</summary>
        Reached,
        /// <summary>Still at it.</summary>
        StillAt,
        /// <summary>Given up, or changed for another.</summary>
        LetGo,
        /// <summary>They died before they reached it.</summary>
        Died
    }

    /// <summary>A27: the year's first supper.</summary>
    public static class FirstSupperMath
    {
        /// <summary>The custom comes in the year's first days: up to this day of the year (0 is the first).</summary>
        public const int LastDayOfYear = 7;

        /// <summary>Someone who thinks this well of the one speaking backs their aim.</summary>
        public const int BackOpinion = 20;

        /// <summary>How much closer an aim said aloud brings its speaker and each who backs it (the social web's warmth).</summary>
        public const float Closer = 2.5f;

        /// <summary>A backer answers the aim at the table, now and then.</summary>
        public const float AnswerChance = 0.5f;

        /// <summary>The year letter reads back at most this many.</summary>
        public const int MaxInLetter = 5;

        public static bool InFirstDays(int dayOfYear) => dayOfYear >= 0 && dayOfYear <= LastDayOfYear;

        public static bool Backs(int opinion) => opinion >= BackOpinion;

        /// <summary>How it turned out: reached first (a goal reached is reached, whatever came after), then death, then still at it, else let go.</summary>
        public static AimOutcome Outcome(bool reached, bool dead, bool stillAt)
        {
            if (reached) return AimOutcome.Reached;
            if (dead) return AimOutcome.Died;
            return stillAt ? AimOutcome.StillAt : AimOutcome.LetGo;
        }
    }

    /// <summary>A29: the year read aloud.</summary>
    public static class YearReadMath
    {
        /// <summary>The reading is three to five lines of the year.</summary>
        public const int MinLines = 3, MaxLines = 5;

        /// <summary>Each year is read once.</summary>
        public static bool Due(int year, int lastRead) => year > lastRead;

        /// <summary>
        /// The lines to read, in the year letter's order (who came, who was lost, the work built, the aims kept, the names,
        /// and then the colony as it stands): the first <see cref="MaxLines"/> of those given, the filler lines only to make up
        /// <see cref="MinLines"/>. <paramref name="lines"/> and <paramref name="filler"/> may hold nulls, which are skipped.
        /// </summary>
        public static List<string> Pick(IList<string> lines, IList<string> filler)
        {
            var picked = new List<string>();
            if (lines != null)
                foreach (var l in lines)
                {
                    if (string.IsNullOrEmpty(l)) continue;
                    if (picked.Count >= MaxLines) break;
                    picked.Add(l);
                }
            if (filler != null)
                foreach (var l in filler)
                {
                    if (picked.Count >= MinLines) break;
                    if (string.IsNullOrEmpty(l) || picked.Contains(l)) continue;
                    picked.Add(l);
                }
            return picked;
        }
    }
}
