using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of content round 4, the colonists' own lives (docs/content-round-4-lives.md), without game types: who a
    /// story, a wish, a life goal or an ambition is for, and how each is settled. Read by Arcs/Round4Lives.cs and checked in
    /// Tools/Tests/Round4LivesTests.cs.
    /// </summary>
    public static class Round4LivesMath
    {
        // ---------------- A trade of their own ----------------

        /// <summary>The hands-on skills a story of work (a trade of their own, work friends) can be about.</summary>
        public static readonly string[] HandsOnSkills = { "Construction", "Mining", "Cooking", "Plants", "Crafting", "Artistic", "Medicine", "Animals", "Shooting" };

        public static bool HandsOn(string skill) => skill != null && Array.IndexOf(HandsOnSkills, skill) >= 0;

        /// <summary>Still poor at it: at most this level.</summary>
        public const int TradeLevelMax = 6;

        /// <summary>
        /// The work has fallen to them: it is on for them, it is no passion of theirs, they are still poor at it, and among
        /// those who do it nobody is better (<paramref name="betterDoing"/>: how many at home with the work on are better).
        /// </summary>
        public static bool TradeFell(bool workOn, int passion, int level, int betterDoing) =>
            workOn && passion <= 0 && level >= 0 && level <= TradeLevelMax && betterDoing == 0;

        // ---------------- The wager ----------------

        /// <summary>Both at least this good at the work.</summary>
        public const int WagerSkill = 5;

        /// <summary>Both think at least this well of each other.</summary>
        public const int WagerOpinion = 10;

        /// <summary>The race lasts this many days.</summary>
        public const float WagerDays = 6f;

        /// <summary>The kinds of work a wager can be about, with the game's record each counts by.</summary>
        public static readonly string[] WagerWorks = { "Growing", "Mining", "Construction", "Cooking", "Crafting", "Hunting" };

        /// <summary>The game's record a wager on this kind of work counts (null: no wager on it).</summary>
        public static string WagerRecord(string work)
        {
            switch (work)
            {
                case "Growing": return "PlantsHarvested";
                case "Mining": return "CellsMined";
                case "Construction": return "ThingsConstructed";
                case "Cooking": return "MealsCooked";
                case "Crafting": return "ThingsCrafted";
                case "Hunting": return "KillsAnimals";
                default: return null;
            }
        }

        /// <summary>A pair the wager can be between: both good at it, fond enough of each other, not rivals.</summary>
        public static bool WagerPair(int levelA, int levelB, int opinionAB, int opinionBA, bool rivals) =>
            !rivals && levelA >= WagerSkill && levelB >= WagerSkill && opinionAB >= WagerOpinion && opinionBA >= WagerOpinion;

        /// <summary>
        /// Who won, by what each brought in since the wager: "won" (the colonist), "lost", or "even" when it is within a
        /// tenth of the larger (and at least one apart is needed to win at all).
        /// </summary>
        public static string WagerOutcome(float gainA, float gainB)
        {
            float a = Math.Max(0f, gainA), b = Math.Max(0f, gainB);
            float margin = Math.Max(1f, Math.Max(a, b) * 0.1f);
            if (Math.Abs(a - b) <= margin) return "even";
            return a > b ? "won" : "lost";
        }

        /// <summary>The wager's key: the work, and what the two had brought in when it was made.</summary>
        public static string WagerKey(string work, float baseA, float baseB) =>
            work + "|" + baseA.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + baseB.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public static bool ReadWagerKey(string key, out string work, out float baseA, out float baseB)
        {
            work = null;
            baseA = baseB = 0f;
            var parts = (key ?? "").Split('|');
            if (parts.Length != 3 || WagerRecord(parts[0]) == null) return false;
            work = parts[0];
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out baseA)
                   && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out baseB);
        }

        // ---------------- The old one's tales ----------------

        /// <summary>An elder, in human years.</summary>
        public const float ElderAge = 55f;

        /// <summary>A child old enough to listen to a tale.</summary>
        public const float ChildAge = 4f;

        public const int TalesOpinion = 10;

        public static bool TalesPair(float elderAge, float childAge, bool isChild, int opinionEC, int opinionCE) =>
            elderAge >= ElderAge && isChild && childAge >= ChildAge && opinionEC >= TalesOpinion && opinionCE >= TalesOpinion;

        // ---------------- The first winter ----------------

        /// <summary>Land with a real winter: its coldest month's mean is below freezing.</summary>
        public static bool RealWinter(float coldestMonthMean) => coldestMonthMean < 0f;

        /// <summary>
        /// Has never wintered here: in autumn, someone who came less than this many days ago came after the last winter
        /// (spring and summer between them make thirty days).
        /// </summary>
        public const float NewSinceWinterDays = 30f;

        public static bool FirstWinter(bool autumn, bool realWinter, float daysHere) => autumn && realWinter && daysHere >= 0f && daysHere < NewSinceWinterDays;

        /// <summary>Someone who has wintered here (a year with the colony) is the one to ask.</summary>
        public const float WinteredDays = 60f;

        // ---------------- Work friends ----------------

        /// <summary>Hardly know each other yet: opinions both ways from 0 to this.</summary>
        public const int StrangersAtMost = 25;

        public static bool WorkStrangers(int opinionAB, int opinionBA, bool friends, bool partners) =>
            !friends && !partners && opinionAB >= 0 && opinionBA >= 0 && opinionAB <= StrangersAtMost && opinionBA <= StrangersAtMost;

        // ---------------- Friends over the hills ----------------

        /// <summary>A talk with the visitor at most this many days ago.</summary>
        public const float MetDays = 4f;

        public const int VisitorOpinion = 20;

        /// <summary>How long they wait for that people to come again.</summary>
        public const float WaitDays = 45f;

        public static bool GotOnWith(int opinion, float daysSinceTalk, bool peopleAtPeace, bool stillHere) =>
            opinion >= VisitorOpinion && daysSinceTalk >= 0f && daysSinceTalk <= MetDays && peopleAtPeace && !stillHere;

        // ---------------- A promise in the dark ----------------

        /// <summary>Down and helpless at most this many days ago (long enough to be up from a sickbed again).</summary>
        public const float DownedDays = 10f;

        public static bool Spared(int downedTick, int now, int ticksPerDay, bool upNow) =>
            upNow && downedTick >= 0 && now >= downedTick && now - downedTick <= DownedDays * ticksPerDay;

        // ---------------- Wishes ----------------

        /// <summary>An evening with an old friend: no warm moment between them for this many days.</summary>
        public const float QuietDays = 4f;

        /// <summary>A friendship at least this strong.</summary>
        public const float OldFriendTie = 45f;

        public static bool GoneQuiet(int lastWarmTick, int now, int ticksPerDay) => lastWarmTick < 0 || now - lastWarmTick >= QuietDays * ticksPerDay;

        /// <summary>Someone to read to me: old, with poor eyes, and a reader fond of them.</summary>
        public const float ReadAge = 60f;

        public const float PoorSight = 0.75f;

        public const int ReaderOpinion = 20;

        public static bool WantsReading(float age, float sight) => age >= ReadAge && sight >= 0f && sight <= PoorSight;

        /// <summary>The harvest in before the frost: this many ripe plants standing.</summary>
        public const int RipeWorth = 12;

        public static bool FrostHarvestWorth(bool autumn, bool realWinter, int ripe) => autumn && realWinter && ripe >= RipeWorth;

        /// <summary>In: nine in ten of them no longer standing ripe.</summary>
        public static bool HarvestIn(int stillRipe, int all) => all > 0 && stillRipe * 10 <= all;

        /// <summary>A snow day: this much deep snow on the open ground, and not bitter cold.</summary>
        public const int SnowCells = 30;

        public const float TooCold = -15f;

        public static bool SnowDay(int deepCells, float outdoorTemp) => deepCells >= SnowCells && outdoorTemp >= TooCold;

        /// <summary>A word at the grave: died at most this many days ago, and thought well of.</summary>
        public const float GraveDays = 15f;

        public const int GraveOpinion = 20;

        public static bool GraveWord(float daysDead, int opinion) => daysDead >= 0f && daysDead <= GraveDays && opinion >= GraveOpinion;

        /// <summary>An hour at the shrine: a hard time is a low mood, strain, or a loss lately.</summary>
        public const float HardMood = 0.35f;

        public const float HardStrain = 30f;

        public const float LossDays = 10f;

        public static bool HardTime(float mood, float strain, float daysSinceLoss) =>
            (mood >= 0f && mood < HardMood) || strain >= HardStrain || (daysSinceLoss >= 0f && daysSinceLoss <= LossDays);

        // ---------------- Ambitions ----------------

        /// <summary>A good old age: each elder's bedroom at least this impressive (decent).</summary>
        public const float ElderRoom = 30f;

        /// <summary>How many of the elders are well housed, of how many.</summary>
        public static float EldersShare(int housed, int elders) => elders <= 0 ? 1f : Math.Min(1f, Math.Max(0, housed) / (float)elders);

        /// <summary>Worth pitching: an elder at home, and one of them without a decent room of their own.</summary>
        public static bool EldersWorth(int housed, int elders) => elders > 0 && housed < elders;

        /// <summary>The cells a wish keeps, written as "x,z;x,z".</summary>
        public static string Cells(IEnumerable<(int x, int z)> cells) => LifeNewMath.Encode(cells);
    }
}
