using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S9, raiders settling nearby (StorylineRaiders.cs; docs/quests-proposals.md). Raiders of a people at war with the colony
    /// are setting up camp in the region, and a people nearby warns the colony. 1. Build defences by a date (the colony's
    /// watch sees the raiders coming when it is ready). 2. The raiders test the colony with a small raid. 3. If their camp is
    /// within three tiles, the neighbours ask the colony to join a strike on it (the one away part here, a camp the colony's
    /// caravan goes to, the neighbours' fighters beside it); otherwise the neighbours go, and the colony sends food and
    /// medicine for their fighters. Endings: the camp is gone, or the raiders demand payment each season for a year: each
    /// season the colony pays, or refuses and they come for it.
    /// </summary>
    public sealed class RaidersFlow : StorylineFlow
    {
        public const string Key = "raiders";

        public override string Kind => Key;

        public override int Number => 9;

        public override string[] Parts => new[] { "defences", "raid", "strike", "tribute" };

        public override string[] Endings => new[] { "declined", "leftalone", "campgone", "theycleared", "movedon", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "defences": return new[] { "ready", "partly", "unready" };
                case "raid": return new[] { "repelled", "overrun", "quiet" };
                case "strike": return new[] { "cleared", "failed" };
                case "tribute": return new[] { "paid" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "unready" || outcome == "overrun" || outcome == "failed";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "defences":
                    if (outcome == "ready" || outcome == "partly" || outcome == "unready") return To("raid", 2f, 5f, outcome);
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "raid":
                    if (outcome == "repelled" || outcome == "quiet") return To("strike", 2f, 6f, outcome);
                    if (outcome == "overrun") return To("tribute", 2f, 5f, "overrun");
                    if (outcome == StorylineMath.Declined) return End("leftalone");
                    return End(StorylineMath.Faded);
                case "strike":
                    if (outcome == "cleared") return End("campgone");
                    if (outcome == "failed") return To("tribute", 2f, 5f, "failed");
                    // The colony would not help: the neighbours strike alone, and sometimes that is enough.
                    if (outcome == StorylineMath.Declined)
                        return Safe(roll) < AloneChance ? End("theycleared", 2f, 5f) : To("tribute", 3f, 6f, "alone");
                    return End(StorylineMath.Faded);
                case "tribute":
                    // Paid or refused (a refusal brings them for it), each season for a year; then they move on.
                    if (outcome == "paid" || outcome == StorylineMath.Declined)
                        return times < Seasons ? To("tribute", 13f, 17f, outcome == "paid" ? "paid" : "refused", true) : End("movedon");
                    return End("movedon");
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "theycleared" || ending == "movedon" || ending == StorylineMath.Gone;

        // ================= The numbers =================

        /// <summary>Days the colony has to build its defences.</summary>
        public const float DefenceMinDays = 5f, DefenceMaxDays = 7f;

        /// <summary>Traps or turrets to build: three to six by the colony's size.</summary>
        public static int GuardsGoal(int colonists) => Clamp(2 + colonists / 3, 3, 6);

        /// <summary>Lengths of wall, fence or barricade to build: 24 to 60 by the colony's size.</summary>
        public static int WallGoal(int colonists) => Clamp(16 + colonists * 4, 24, 60);

        /// <summary>"ready" (either goal met), "partly" (half of either), "unready" (less).</summary>
        public static string Readiness(int guards, int guardsGoal, int walls, int wallGoal)
        {
            if (guards >= guardsGoal || walls >= wallGoal) return "ready";
            if (guards * 2 >= guardsGoal || walls * 2 >= wallGoal) return "partly";
            return "unready";
        }

        /// <summary>The test raid: this share of the threat the storyteller would send now (a small raid).</summary>
        public const float RaidShare = 0.5f;

        /// <summary>It comes this often, through the shared harm budget (HarmKinds.StoryFight); else they keep away.</summary>
        public const float RaidChance = 0.85f;

        /// <summary>Days the colony waits for the test raid once it agrees to meet it.</summary>
        public const float RaidMinDays = 1f, RaidMaxDays = 3f;

        /// <summary>Ready, the colony's watch sees them coming this many hours ahead (a letter before the raid).</summary>
        public const float WarnHours = 4f;

        /// <summary>
        /// The raid is over: beaten off when half of them or more fell, or more of them fell than of the colony's people; else
        /// they had the better of it. None came: quiet.
        /// </summary>
        public static string RaidOutcome(int size, int down, int oursDown) => size <= 0 ? "quiet" : down * 2 >= size || down > oursDown ? "repelled" : "overrun";

        /// <summary>A raid still going this long after it came is judged as it stands.</summary>
        public const float RaidOverDays = 2f;

        /// <summary>The camp within reach: within this many tiles the colony is asked to join the strike (the storylines' away rule).</summary>
        public const int NearTiles = 3;

        /// <summary>The camp's strength, a share of the threat the storyteller would send now; and how long it stands for the strike.</summary>
        public const float CampShare = 0.6f, StrikeDays = 8f;

        /// <summary>The neighbours' fighters who join the colony's at the camp: two to four.</summary>
        public static int Allies(int colonists) => Clamp(2 + colonists / 5, 2, 4);

        /// <summary>The far camp: days to send the food and medicine, then days until word of the strike comes.</summary>
        public const float SupplyDays = 3f, StrikeWordMin = 2f, StrikeWordMax = 4f;

        /// <summary>The food and medicine their fighters need: by how many go, meals and medicine.</summary>
        public static int SupplyMeals(int fighters) => Clamp(fighters, 3, 8) * 3;

        public static int SupplyMedicine(int fighters) => Clamp(fighters, 3, 8);

        /// <summary>The neighbours' strike on a far camp succeeds this often: likelier with the colony's food and medicine.</summary>
        public static float StrikeChance(bool supplied) => supplied ? 0.75f : 0.45f;

        /// <summary>The colony would not help: the neighbours clear it alone this often.</summary>
        public const float AloneChance = 0.35f;

        /// <summary>Their price each season: about half their raid's strength in silver, rounded to tens, a hundred at least.</summary>
        public static int Price(float raidPoints)
        {
            float p = Safe(raidPoints) * 0.5f;
            return Math.Max(100, (int)Math.Round(p / 10f, MidpointRounding.AwayFromZero) * 10);
        }

        /// <summary>Seasons they ask: a year.</summary>
        public const int Seasons = 4;

        /// <summary>Refused, they come for it this many days later.</summary>
        public const float RefusedRaidMin = 1f, RefusedRaidMax = 2f;

        /// <summary>Goodwill with the people who warned the colony: for the camp gone with the colony's help, and for joining in person.</summary>
        public const int ClearedGoodwill = 15, JoinedGoodwill = 8;
    }
}
