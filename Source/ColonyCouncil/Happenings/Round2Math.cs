using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the content update's second round without game types (docs/content-update-proposals-2.md, D16 to D32;
    /// Happenings/Workers_Round2.cs, Dilemmas/DilemmaWorkers_Round2.cs): when the storyteller events may come, how long
    /// what they keep on the map lasts, and how often the cold, a mad animal or a hot stone hurts, before the shared harm
    /// budget's scale.
    /// </summary>
    public static class Round2Math
    {
        // ================= Not in by dark (D16) =================

        /// <summary>Dusk: the hours someone may not come in by.</summary>
        public const int DuskFrom = 17, DuskTo = 21;

        /// <summary>How far out from the middle of the colony someone is when they haven't come in.</summary>
        public const float LostFar = 40f;

        /// <summary>
        /// The night's cold for whoever is out in it: every two hours out in a frost, one chance in twenty of frostnip or
        /// chilblains (the cold's own rate, Harm/ColdHurts.cs, the wait at the gate).
        /// </summary>
        public const float ColdEveryHours = 2f, ColdChance = 0.05f;

        /// <summary>A search that takes longer than this counts as a long one: the lost one feels the cold too.</summary>
        public const float SearchLongHours = 3f;

        /// <summary>First light: when one who waits for morning sets off home.</summary>
        public const int Dawn = 6;

        /// <summary>The searchers find the lost one within this many cells; they walk home together.</summary>
        public const float FoundWithin = 4f;

        /// <summary>The lost one hears the horn and starts walking toward the light this many hours after it is first sounded.</summary>
        public const float HearsAfterHours = 1f;

        /// <summary>A night out ends at the latest this many hours after the answer.</summary>
        public const float NightOutMaxHours = 16f;

        /// <summary>The hour of a dusk, in a day of 24: within it (DuskFrom to DuskTo, both kept).</summary>
        public static bool AtDusk(int hour) => hour >= DuskFrom && hour <= DuskTo;

        /// <summary>A night out waits until first light: from dusk to midnight and from midnight to dawn it is still night.</summary>
        public static bool StillNight(int hour) => hour >= DuskFrom || hour < Dawn;

        /// <summary>Another cold roll is due: two hours out in the frost since the last one.</summary>
        public static bool ColdDue(float hoursOut) => hoursOut >= ColdEveryHours;

        // ================= The fish are running (D17) =================

        /// <summary>The run lasts three days.</summary>
        public const float FishRunDays = 3f;

        /// <summary>While it lasts, a fisher at the water brings in this many fish an hour more than usual, on average.</summary>
        public const float ExtraFishPerHour = 0.6f;

        /// <summary>A bear comes down to the bank too, one run in three.</summary>
        public const float BearChance = 0.33f;

        /// <summary>The few fish now and the rest put by: a pemmican bill of this many.</summary>
        public const int PemmicanBill = 4;

        // ================= A bear asleep in the rock (D18) =================

        /// <summary>The walled den is first looked at this many days after it is walled up, then each day until it is warm.</summary>
        public const float DenFirstLookDays = 8f, DenLookAgainDays = 1f;

        /// <summary>Warm enough for the bear to walk out: the outdoor air at least this warm, in spring or summer.</summary>
        public const float DenWarm = 5f;

        /// <summary>The hollow behind the face: this many cells of rock broken into.</summary>
        public const int HollowCells = 2;

        /// <summary>The bear walks out once it is spring and warm enough.</summary>
        public static bool DenOpens(bool springOrSummer, float outdoorTemp) => springOrSummer && outdoorTemp >= DenWarm;

        // ================= Water for the fields (D19) =================

        /// <summary>The water chain goes on for two hours from dawn.</summary>
        public const float WaterChainHours = 2f;

        /// <summary>It starts between these hours; if it can't by the next day's end, it doesn't.</summary>
        public const int WaterFrom = 5, WaterTo = 8;

        /// <summary>A watered field grows a day's growth, as it would have in rain; the rest, when one field is let go, half that.</summary>
        public const float WaterGrowthDays = 1f, LetGoGrowthDays = 0.5f;

        /// <summary>A field worth the trouble: this many growing plants at least.</summary>
        public const int FieldAtLeast = 6;

        /// <summary>Water within this many cells of the field.</summary>
        public const float WaterWithin = 70f;

        /// <summary>A day's growth for a plant that takes <paramref name="growDays"/> days to grow, at its full rate (0 for none).</summary>
        public static float DayGrowth(float growDays, float days) => growDays <= 0f || days <= 0f ? 0f : Math.Min(1f, days / growDays);

        // ================= The animal that's wrong (D20) =================

        /// <summary>The hours at dawn when it's seen.</summary>
        public const int WrongFrom = 5, WrongTo = 9;

        /// <summary>Left be (or missed), it goes mad later that day, this many hours after it's seen at most.</summary>
        public const float WrongHours = 10f;

        /// <summary>Its madness, before the budget's scale: the game's own mad animal, most times it is let be.</summary>
        public const float MadChance = 0.8f;

        /// <summary>With the animals in, it wanders off the land after this many hours.</summary>
        public const float WrongLeavesHours = 4f;

        /// <summary>Sanguinary Animals Reborn: one in four is a sanguine deer.</summary>
        public const float SanguineChance = 0.25f;

        // ================= The one who hid (D21) =================

        /// <summary>A day or so after the raid: half a day at the soonest, two at the latest.</summary>
        public const float HidFromDays = 0.5f, HidToDays = 2f;

        /// <summary>Where they hide: this far out from the colony at least.</summary>
        public const float HidFar = 45f;

        /// <summary>Unanswered, they slip away after this many hours more.</summary>
        public const float HidSlipHours = 4f;

        /// <summary>The raid fits: it came within the window.</summary>
        public static bool RaidFits(float daysSince) => daysSince >= HidFromDays && daysSince <= HidToDays;

        // ================= Stuck in the bog (D22) =================

        /// <summary>Pulled out with a rope by two in an hour; boards laid out to it by one in half an hour.</summary>
        public const float RopeHours = 1f, BoardsHours = 0.5f;

        /// <summary>The wood the boards take.</summary>
        public const int BoardsWood = 20;

        /// <summary>Left, it works itself free in a day and a half.</summary>
        public const float BogFreeDays = 1.5f;

        // ================= Bad air in the deep (D23) =================

        /// <summary>The face is deep: no open sky within this many cells of it.</summary>
        public const float DeepFromSky = 9f;

        /// <summary>A shaft is opened only to air within this many cells of rock.</summary>
        public const int ShaftMax = 12;

        /// <summary>The gas let out at each cell of the pocket, at the budget's full scale.</summary>
        public const int GasPerCell = 160;

        /// <summary>The pocket: this many cells round the opening.</summary>
        public const int GasCells = 5;

        /// <summary>Short turns: nobody stays in the bad air longer than this before they're sent out to breathe.</summary>
        public const float TurnHours = 0.5f;

        /// <summary>The gas let out at a cell for the budget's scale (at least a little, so the sign is there).</summary>
        public static int GasFor(float scale) => (int)Math.Round(GasPerCell * Math.Max(0.25f, Math.Min(1.5f, scale)));

        // ================= The ordeal (D26) =================

        /// <summary>In the older ages, each who carries the stone is burned across the hands this often, before the scale.</summary>
        public const float OrdealBurnChance = 0.5f;

        /// <summary>A burn across the hands: a small one.</summary>
        public const float OrdealBurnLow = 2f, OrdealBurnHigh = 5f;

        /// <summary>The believers take the one less burned for the truthful; with neither or both burned alike, the first by the toss.</summary>
        public static int Truthful(float burnA, float burnB, bool tossFirst) => burnA < burnB ? 0 : burnB < burnA ? 1 : tossFirst ? 0 : 1;

        // ================= The feast after a death (D28) =================

        /// <summary>A death within this many days before the festival.</summary>
        public const float DeathWithinDays = 3f;

        /// <summary>The feast kept later: this many days on, when the mourning days are past.</summary>
        public const float LateFeastDays = 4f;

        // ================= The work my god won't have (D30) =================

        /// <summary>Excused for a season.</summary>
        public const float ExcusedDays = 15f;

        /// <summary>A devoted believer: they hold the god this much at least (of 100).</summary>
        public const float DevotedFrom = 60f;

        // ================= The room nobody will sleep in (D24) =================

        /// <summary>The room has stood empty since the death this many days at least, and is asked about until this many.</summary>
        public const float EmptyFromDays = 3f, EmptyToDays = 20f;

        /// <summary>One who thought well of the dead asks: an opinion of them this good at least.</summary>
        public const int FriendOpinion = 20;

        // ================= One of the camp died in the night (D31) =================

        /// <summary>Asked within this long of the death.</summary>
        public const float CampDeathDays = 1.5f;

        // ================= The new age (D32) =================

        /// <summary>The question for each age is asked once in a long while (its def's cooldown, ten years).</summary>
        public const float NewAgeCooldownDays = 600f;
    }
}
