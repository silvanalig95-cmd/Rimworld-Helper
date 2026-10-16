using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers behind setbacks (Setbacks/): when something that went wrong in play becomes a mood, and how big. No
    /// game types, so Tools/Tests can check them.
    /// </summary>
    public static class SetbackMath
    {
        // ---------------- Strength ----------------

        /// <summary>
        /// The factor a setback is given with, so it's never felt as less than a whole point (as for the everyday
        /// pressures): a -1 memory on the gentle setting is still -1, not -0.5.
        /// </summary>
        public static float AtLeastOnePoint(float baseOffset, float factor)
        {
            if (baseOffset >= 0f || factor <= 0f) return factor;
            float felt = Math.Abs(baseOffset) * factor;
            return felt >= 1f ? factor : 1f / Math.Abs(baseOffset);
        }

        // ---------------- The council ----------------

        /// <summary>Days a wish must have waited before others being granted first is felt.</summary>
        public const float WaitDays = 3f;

        /// <summary>
        /// A wish still waiting: -1 (stage 0) once someone else's wish came true after it was voiced, -2 (stage 1) once
        /// two or more did; nothing before it has waited <see cref="WaitDays"/>.
        /// </summary>
        public static int WaitStage(float waitedDays, int grantedSince)
        {
            if (waitedDays < WaitDays || grantedSince < 1) return -1;
            return grantedSince >= 2 ? 1 : 0;
        }

        /// <summary>Days a decree must have held before it drags on for those who dislike it.</summary>
        public const float DragDays = 2f;

        /// <summary>A disliked decree dragging on: stage 0 (dislike), 1 (hate, or a repeal they asked for was ignored).</summary>
        public static int DragStage(float daysInForce, bool hate, bool repealIgnored)
        {
            if (daysInForce < DragDays) return -1;
            return hate || repealIgnored ? 1 : 0;
        }

        /// <summary>
        /// How much an answer goes against someone's nature: the answer's shift of the colony's ways against how much they
        /// care for each way (-1 dislikes it .. +1 likes it). Positive: against them.
        /// </summary>
        public static float Clash(float[] shift, float[] affinity)
        {
            if (shift == null || affinity == null) return 0f;
            float c = 0f;
            int n = Math.Min(shift.Length, affinity.Length);
            for (int i = 0; i < n; i++) c -= shift[i] * affinity[i];
            return c;
        }

        public const float ClashMild = 3f;
        public const float ClashStrong = 6f;
        public const float ClashToldSo = 1.5f;

        /// <summary>Not everyone agreed: stage 0 (-1) from a clash of 3, stage 1 (-2) from 6; -1 for none.</summary>
        public static int ClashStage(float clash) => clash >= ClashStrong ? 1 : clash >= ClashMild ? 0 : -1;

        /// <summary>The answer's gamble went wrong, and it went against their nature even a little: "I said so".</summary>
        public static bool ToldSo(float clash, bool riskCameTrue) => riskCameTrue && clash >= ClashToldSo;

        // ---------------- Work ----------------

        /// <summary>Work to build (ticks of work) that makes a building worth minding when it's lost.</summary>
        public const float NotableWork = 1500f;

        /// <summary>Work to build that makes it a big piece: losing it hurts most.</summary>
        public const float BigWork = 12000f;

        /// <summary>Destroyed: stage 0 (-4), or 1 (-6) for a big piece or a work of art.</summary>
        public static int WreckStage(float workToBuild, bool art) => art || workToBuild >= BigWork ? 1 : 0;

        /// <summary>Crops lost in one field before the growers mind (one or two plants is bad luck, not a loss).</summary>
        public const int CropMinLost = 4;

        /// <summary>Lost from one field to be "a whole field": stage 2 (-4). Passionate growers feel it from half that.</summary>
        public const int CropBigLoss = 30;

        /// <summary>A crop lost: stage 0 (blight, -2), 1 (frost, -2), 2 (a whole field, -4); -1 for too few to mind.</summary>
        public static int CropStage(int lost, bool blight, bool passionate)
        {
            if (lost < CropMinLost) return -1;
            if (lost >= CropBigLoss || (passionate && lost * 2 >= CropBigLoss)) return 2;
            return blight ? 0 : 1;
        }

        /// <summary>Cells still to go with the load for a haul to count as a long one.</summary>
        public const int LongHaulCells = 60;

        /// <summary>Long hauls caught in one day before it's a day of long hauls.</summary>
        public const int LongHaulSamples = 2;

        public static bool LongHaul(int cellsToGo) => cellsToGo >= LongHaulCells;

        /// <summary>A workbench this much slower than a proper one (its work speed factor) is making do.</summary>
        public const float MakeshiftSpeed = 0.8f;

        public static bool Makeshift(float benchSpeed, float bestSpeed) => benchSpeed < MakeshiftSpeed && bestSpeed >= benchSpeed + 0.2f;

        /// <summary>Hours left waiting for a doctor before it's felt.</summary>
        public const int TendWaitHours = 3;

        /// <summary>Days with no research project picked, with a research bench at home, before keen researchers mind.</summary>
        public const float ResearchIdleDays = 1f;

        // ---------------- Home and worries ----------------

        /// <summary>Impressiveness stages between their own bedroom and the best one at home, for envy.</summary>
        public const int EnvyGap = 3;

        public static bool Envious(int mine, int best) => best - mine >= EnvyGap;

        /// <summary>Days without a party or gathering at home before quiet days drag.</summary>
        public const float QuietDays = 10f;

        /// <summary>
        /// Quiet days: stage 0 (-1) with fewer kinds of recreation at home than they expect (the game's expectations,
        /// which rise with the colony's wealth), 1 (-2) with two fewer or more.
        /// </summary>
        public static int QuietStage(int joyKinds, int needed, float daysSinceGathering)
        {
            if (daysSinceGathering < QuietDays || joyKinds >= needed) return -1;
            return joyKinds <= needed - 2 ? 1 : 0;
        }

        /// <summary>Hard memories (-5 or worse) in the last week that make a hard week.</summary>
        public const int HardWeekMemories = 3;

        public static bool HardWeek(int hardMemories) => hardMemories >= HardWeekMemories;

        /// <summary>Worried about someone ill: stage 0 (-1) for a friend, 1 (-2) for a partner or family, or when it could kill.</summary>
        public static int SickStage(bool kin, bool lifeThreatening) => kin || lifeThreatening ? 1 : 0;

        /// <summary>A nemesis still out there is felt from when "they got away" fades until this many days after the deed.</summary>
        public const float NemesisFrom = 8f;
        public const float NemesisUntil = 60f;

        public static bool NemesisWorry(float daysSince) => daysSince >= NemesisFrom && daysSince <= NemesisUntil;

        /// <summary>Days of food a caravan with someone dear in it can be short of before it's a worry.</summary>
        public const float CaravanFoodDays = 1f;
    }
}
