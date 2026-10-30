using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The shape of a camp's stay and of its coming back, as plain numbers (Tools/Tests/CampStoryTests.cs): which
    /// endings are remembered, how likely a remembered camp is to return and whom it brings, on which day of a stay
    /// its ask, its trouble and its parting fall, and how strong a war party comes to a fight.
    /// </summary>
    public static class CampStoryMath
    {
        // ---------------- Coming back ----------------

        /// <summary>How many camps the colony remembers at most, and how many of each camp's people are kept to come back.</summary>
        public const int RememberMax = 6;
        public const int KeepPeople = 4;
        /// <summary>No return sooner than this after they left, none after this long, and the same people come back at most this often.</summary>
        public const float ReturnAfterDays = 30f;
        public const float ForgetAfterDays = 200f;
        public const int MaxReturns = 3;

        /// <summary>A stay worth remembering: they came, and left in peace with things not too sour between the camp and the colony.</summary>
        public static bool Remembered(string ending, bool arrived, float tension)
        {
            if (!arrived || tension >= 60f || ending == null) return false;
            switch (ending)
            {
                case "time":
                case "asked":
                case "weather":
                case "raid":
                case "hunger":
                case "crowded":
                case "moved":
                case "empty":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The chance that a camp coming now is this remembered one coming back: more for a warm stay (help, gifts,
        /// trade), more when the colony asked them back, less each time they've already returned; nothing too soon
        /// or after too many returns.
        /// </summary>
        public static float ReturnChance(float daysSince, int helpsAndGifts, int tradeDays, bool invited, int returns)
        {
            if (daysSince < ReturnAfterDays || daysSince > ForgetAfterDays || returns >= MaxReturns) return 0f;
            float c = 0.3f + 0.05f * Math.Min(helpsAndGifts, 4) + 0.03f * Math.Min(tradeDays, 3) + (invited ? 0.2f : 0f) - 0.1f * returns;
            return Clamp(c, 0.05f, 0.75f);
        }

        /// <summary>
        /// Who of a camp is kept to come back: whoever spoke for them, their trader, then those the colony thought best
        /// of (by the score given for each), up to <see cref="KeepPeople"/>. Indices into the list given.
        /// </summary>
        public static List<int> Kept(int count, int leader, int trader, IList<float> liking)
        {
            var kept = new List<int>();
            if (count <= 0) return kept;
            if (leader >= 0 && leader < count) kept.Add(leader);
            if (trader >= 0 && trader < count && !kept.Contains(trader)) kept.Add(trader);
            var rest = new List<int>();
            for (int i = 0; i < count; i++) if (!kept.Contains(i)) rest.Add(i);
            rest.Sort((a, b) => Score(liking, b).CompareTo(Score(liking, a)));
            foreach (int i in rest)
            {
                if (kept.Count >= KeepPeople) break;
                kept.Add(i);
            }
            return kept;
        }

        private static float Score(IList<float> liking, int i) => liking != null && i < liking.Count ? liking[i] : 0f;

        /// <summary>A returning camp settles in quicker: its unease grows at this share of the usual rate.</summary>
        public const float FamiliarTension = 0.6f;

        // ---------------- The stay's shape ----------------

        /// <summary>The day of the stay (from arrival) on which they ask something of the colony, or do it a good turn.</summary>
        public const float AskDay = 1.5f;

        /// <summary>A stay long enough to have trouble in the middle of it.</summary>
        public static bool HasTrouble(float stayDays) => stayDays >= 3f;

        /// <summary>Trouble comes midway through the stay.</summary>
        public static float TroubleDay(float stayDays) => stayDays * 0.5f;

        /// <summary>The ask is due: a day and a half in, with at least half a day of the stay left.</summary>
        public static bool AskDue(float daysHere, float stayDays) => daysHere >= AskDay && daysHere < stayDays - 0.5f;

        public static bool TroubleDue(float daysHere, float stayDays) => HasTrouble(stayDays) && daysHere >= TroubleDay(stayDays) && daysHere < stayDays - 0.5f;

        /// <summary>The parting word comes within the last day of a stay of two days or more.</summary>
        public static bool PartingDue(float daysHere, float stayDays) => stayDays >= 2f && daysHere >= stayDays - 1f && daysHere < stayDays;

        /// <summary>Meals for the road: two a head.</summary>
        public static int Provisions(int people) => Math.Max(2, people * 2);

        // ---------------- Neighbours for good ----------------

        /// <summary>
        /// Whether a camp of a kind that can settle asks to stay for good at its parting: a stay of three days or more
        /// that went well enough, and no neighbours settled on the land already.
        /// </summary>
        public static bool SettleOffered(bool canSettle, float tension, float daysHere, bool holdingFree) => canSettle && holdingFree && tension < 40f && daysHere >= 3f;

        /// <summary>Settled neighbours' unease eases a little each day instead of growing.</summary>
        public const float HoldingTensionADay = -1.5f;
        /// <summary>Their trader's stock is new every so often; one of them falls ill now and then; one of them comes over to the colony's gate every few days.</summary>
        public const int HoldingStockDays = 15;
        public const int HoldingSickDays = 25;
        public const float HoldingSickChance = 0.3f;
        public const int HoldingVisitDays = 3;
        /// <summary>Food they find for themselves each day: enough for everyone, so they never go hungry on the colony's land.</summary>
        public static int HoldingProvisions(int people) => Math.Max(2, people * 3);
        /// <summary>After raiders killed one of them, the chance they bury their dead and move on.</summary>
        public const float GriefLeaveChance = 0.5f;

        // ---------------- A war party ----------------

        /// <summary>A threat must have lasted this long before anyone comes to it (word has to reach them).</summary>
        public const int WarAfterHours = 1;

        /// <summary>How many fighters come: by the threat's size, three to seven.</summary>
        public static int Fighters(float threatPoints) => Clamp((int)Math.Round(threatPoints / 120f), 3, 7);

        /// <summary>The strength they come with: about half the threat's, never trifling, never an army.</summary>
        public static float WarPoints(float threatPoints) => Clamp(threatPoints * 0.55f, 150f, 1400f);

        /// <summary>The chance a people at war with the attacker sends help: their goodwill, and an old quarrel with the attacker, count.</summary>
        public static float WarChance(int goodwill, bool oldFoe) => Clamp(0.2f + goodwill / 300f + (oldFoe ? 0.15f : 0f), 0.1f, 0.6f);

        /// <summary>Which people sends help: by their goodwill, those at odds with the attacker three times as likely.</summary>
        public static float HelperWeight(int goodwill, bool hostileToEnemy) => Math.Max(1f, 20f + goodwill) * (hostileToEnemy ? 3f : 1f);

        /// <summary>A war party stays to rest this long after the fight (a day or two), and gives up on a fight that drags on past this.</summary>
        public const float RestDays = 1.5f;
        public const float FightGiveUpDays = 2f;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
