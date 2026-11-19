using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 6 (docs/peoples-events-proposals.md, items 48, 49, 50, 52, 53, 54, 55,
    /// 56, 57 and 58; Peoples/Events6/): the Stormglass Conclave's rain in a drought, the tide read on a watch and the storm
    /// tower; the Mossborn's run to a fire in the canopy, the seeds of the old wood and their camp after the fire; the Ashen
    /// Banner's contract on the colony's enemy, the old soldier who settles, the other side's contract and the company on the
    /// march. Shared with the tests (Tools/Tests/PeopleEvents6Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents6Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        // ================= 48. Breaking the drought (the Stormglass Conclave) =================

        /// <summary>The dry spells the storm-walkers come to break: the droughts of Odyssey and Vanilla Events Expanded, and the game's heat wave.</summary>
        public static readonly string[] DryConditions = { "Drought", "VEE_Drought", "HeatWave" };

        public static bool Dry(string condition) => condition != null && Array.IndexOf(DryConditions, condition) >= 0;

        /// <summary>Two storm-walkers come once the dry spell has run half a day, at a chance, once a spell; not again for so many days.</summary>
        public const int RainWalkers = 2, DryHours = 12, RainGapDays = 20;

        public const float RainChance = 0.5f;

        /// <summary>Their fee, by the colony's wealth (the watch's factor), to the nearest ten.</summary>
        public static int RainFee(float wealthFactor) => Clamp(Round10(200f * (float.IsNaN(wealthFactor) ? 1f : wealthFactor)), 120, 400);

        /// <summary>One time in four the rain comes as a thunderstorm.</summary>
        public const float StormOdds = 0.25f;

        public static bool Storm(float roll) => roll < StormOdds;

        /// <summary>They set their rods from dusk, keep the night with them, and the rain comes at first light.</summary>
        public const int SetUpFrom = 17, RainHour = 6;

        public static bool SetUpHour(int hour) => hour >= SetUpFrom || hour < RainHour - 1;

        public static bool RainNow(int hour) => hour >= RainHour && hour < 12;

        /// <summary>After their night by the rods: the first hour of daylight brings the rain (from first light until dusk).</summary>
        public static bool Daylight(int hour) => hour >= RainHour && hour < SetUpFrom;

        /// <summary>A rod set or taken down; the night kept at them, at most.</summary>
        public const int RodTicks = 1500, TakeDownTicks = 600, NightTicks = 2500;

        /// <summary>Their rods stand this far apart, at most this far from the place.</summary>
        public const int RodsApart = 3;

        public const int RainGoodwill = 3;

        /// <summary>The colonist who keeps the night with them: what they learn of the fields.</summary>
        public const float NightXp = 900f;

        // ================= 49. The tide read (the Stormglass Conclave) =================

        /// <summary>The psychic tides a watch holds back like a condition of the sky: the game's drone and soothe, which touch one sex.</summary>
        public static readonly string[] TideIncidents = { "PsychicDrone", "PsychicSoothe" };

        public static bool Tide(string incident) => incident != null && Array.IndexOf(TideIncidents, incident) >= 0;

        public static bool Drone(string incident) => incident == "PsychicDrone";

        /// <summary>A reader sits an hour with the one the drone touches worst (two with the second answer).</summary>
        public const int SitTicks = 2500;

        /// <summary>The one touched worst: the lowest mood (-1 for none).</summary>
        public static int Worst(float[] moods)
        {
            if (moods == null || moods.Length == 0) return -1;
            int best = -1;
            float low = float.MaxValue;
            for (int i = 0; i < moods.Length; i++)
                if (!float.IsNaN(moods[i]) && moods[i] < low)
                {
                    low = moods[i];
                    best = i;
                }
            return best;
        }

        /// <summary>A held tide forgotten if it hasn't come within this long of its hour (the game's queue gave up on it).</summary>
        public const int TideForgetDays = 3;

        public const int SitGoodwill = 2;

        // ================= 50. The storm tower (the Stormglass Conclave) =================

        /// <summary>Two readers and an apprentice, a week.</summary>
        public const int TowerReaders = 2, TowerDays = 7;

        /// <summary>The meals the second answer sends with them; their thanks, more with the meals.</summary>
        public const int TowerMeals = 8, TowerGoodwill = 3, TowerMealsGoodwill = 2;

        /// <summary>
        /// It can turn, once a stay each: a strike lands past the rods in a storm (the game's own bolt, at a cell beyond their
        /// reach, clear of anyone), and the apprentice takes a chill (the illness budget).
        /// </summary>
        public const float StrikeChance = 0.3f, ChillChance = 0.25f;

        /// <summary>Where the stray strike lands: past the rods' reach (18 cells) and not far beyond, this far from anyone.</summary>
        public const float StrikeMin = 20f, StrikeMax = 28f, StrikeClear = 5f;

        public static bool PastRods(float fromRod, float rodRadius) => !float.IsNaN(fromRod) && fromRod > rodRadius + 1f;

        /// <summary>The chill only after the second day, at night, in rain or below ten degrees.</summary>
        public static bool ChillNight(float days, int hour, bool wet, float temp) => days >= 2f && (hour >= 22 || hour <= 4) && (wet || !float.IsNaN(temp) && temp < 10f);

        /// <summary>A storm counted once, however long it lasts: a new one after four hours of other weather.</summary>
        public const int StormGapHours = 4;

        // ================= 52. Fire in the canopy (the Mossborn) =================

        /// <summary>A wildfire in the colony's woods: so many fires burning on trees or beside them.</summary>
        public const int CanopyFires = 6;

        /// <summary>Three to five thorn-guards, at a chance once a fire, not again for so many days; they stay at most this long.</summary>
        public const int GuardsMin = 3, GuardsMax = 5, CanopyGapDays = 15, CanopyHours = 12;

        public const float CanopyChance = 0.6f;

        /// <summary>The fires they fight: within this of where the fire was when they came.</summary>
        public const float CanopyRadius = 45f;

        public const int CanopyGoodwill = 4;

        /// <summary>The fire is out: no fire left within their reach.</summary>
        public static bool Out(int firesLeft) => firesLeft <= 0;

        // ================= 53. Seeds of the old wood (the Mossborn) =================

        /// <summary>The player's plant mods whose seeds come first (by package), the game's own after.</summary>
        public static readonly string[] SeedPackages =
        {
            "vanillaexpanded.vplantse", "vanillaexpanded.vplantsemore", "vanillaexpanded.vplantsesucculents", "vanillaexpanded.vpeflowers", "sarg.alphabiomes"
        };

        /// <summary>A plot of four by four, three by three where that won't fit.</summary>
        public const int PlotSide = 4, PlotSmall = 3;

        /// <summary>The seeds' price: a quarter of what a plot's first harvest is worth, to the nearest ten.</summary>
        public static int SeedPrice(int cells, float yieldValue) => Clamp(Round10(Math.Max(0, cells) * Clamp(yieldValue, 0f, 200f) * 0.25f), 60, 300);

        /// <summary>Swapped for the colony's own harvest instead: a quarter again of the price in its stores' worth.</summary>
        public static int SwapCount(int price, float unitValue) => unitValue <= 0.01f || float.IsNaN(unitValue) ? 0 : Math.Max(1, (int)Math.Ceiling(price * 1.25f / unitValue));

        /// <summary>Three hours at the plot; what the grower learns.</summary>
        public const int SowTicks = 7500;

        public const float GrowerXp = 1200f;

        public const int SeedGoodwill = 2;

        // ================= 54. After the fire (the Mossborn) =================

        /// <summary>After a wildfire that left this many burned trees, or a blight on this many plants.</summary>
        public const int BurnedMin = 10, BlightMin = 6;

        /// <summary>A burn they come to when asked back from the fire (fewer than for their own coming).</summary>
        public const int BurnedAny = 3;

        public const float AfterChance = 0.5f;

        public const int AfterGapDays = 30, AfterDays = 7;

        /// <summary>The saplings they plant across the burn, at most, this far apart (squared cells).</summary>
        public const int MaxSaplings = 24, SaplingsApartSq = 9;

        /// <summary>The planting's reach: from its middle to the farthest of the burn and a little beyond, within reason.</summary>
        public static float PlantingRadius(float farthest) => Clamp((float.IsNaN(farthest) ? 0f : farthest) + 2f, 6f, 30f);

        public const int CutTicks = 900, SaplingTicks = 600, SingTicks = 2500;

        /// <summary>They sing by the fire at night.</summary>
        public static bool Sing(int hour) => hour >= 19 && hour <= 22;

        /// <summary>It can turn: the fire starts again in the ash (once a stay, in dry weather, the camps' embers' kind).</summary>
        public const float RekindleChance = 0.15f;

        /// <summary>Felling within the planting is a wrong: they leave, and think less of the colony.</summary>
        public const int FellWrongGoodwill = -6, AfterGoodwill = 4, PlanterGoodwill = 2, AfterMeals = 10;

        // ================= 55. A contract on the colony's enemy (the Ashen Banner) =================

        /// <summary>An enemy camp within this many tiles.</summary>
        public const float ContractTiles = 12f;

        /// <summary>The colony has so many days to get there; the company waits that long.</summary>
        public const int ContractDays = 15;

        /// <summary>The price, by the threat it is (the storyteller's points now) and whether it is a town or a camp.</summary>
        public static int ContractPrice(float points, bool town) => Clamp(Round10(Clamp(points, 0f, 10000f) * (town ? 0.9f : 0.6f)), 200, 2000);

        /// <summary>Paid up front, the loot all the colony's: half again.</summary>
        public static int UpFront(int price) => Round10(price * 1.5f);

        /// <summary>The company that marches: by the threat, three to eight.</summary>
        public static int Company(float points) => Clamp((int)(Clamp(points, 0f, 10000f) / 200f), 3, 8);

        /// <summary>The company's share of the silver found there, by the contract.</summary>
        public const float LootShare = 0.5f;

        public static int Share(int silver) => silver <= 0 ? 0 : (int)Math.Floor(silver * LootShare);

        public const int ContractGoodwill = 4, LapseGoodwill = -3;

        // ================= 56. An old soldier settles (the Ashen Banner) =================

        /// <summary>The veteran's age.</summary>
        public const float VeteranMinAge = 45f, VeteranMaxAge = 62f;

        /// <summary>Once a year (sixty days) the muster, a week at the Banner's town.</summary>
        public const int MusterYearDays = 60, MusterDays = 7;

        public static int MusterFee(float wealthFactor) => Clamp(Round10(150f * (float.IsNaN(wealthFactor) ? 1f : wealthFactor)), 100, 300);

        /// <summary>The muster bought out for good when they settle: three years' fees.</summary>
        public static int BuyOut(int fee) => Round10(fee * 3f);

        public const int MusterGoodwill = 3, MusterRefusedGoodwill = -5, SettleGoodwill = 2;

        /// <summary>A muster call unanswered this long is refused.</summary>
        public const int MusterAnswerDays = 3;

        /// <summary>The war-horse of the age: the Paragon horse of Vanilla Genetics Expanded in the spacer age, the game's horse in every age.</summary>
        public static string[] Mounts(int age) => age >= 3 ? new[] { "GR_ParagonHorse", "Horse" } : new[] { "Horse" };

        /// <summary>Home from the muster: what they learned on the drill ground.</summary>
        public const float MusterXp = 1500f;

        // ================= 57. The other side's contract (the Ashen Banner) =================

        /// <summary>A raid by a people at war with the colony, the Banner at peace with both: at this chance, not again for so many days.</summary>
        public const float HireChance = 0.25f;

        public const int HireGapDays = 45;

        /// <summary>The raid held back a day or so while the sergeant comes.</summary>
        public const int HoldMinHours = 18, HoldMaxHours = 30;

        public static int HoldHours(float roll) => HoldMinHours + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (HoldMaxHours - HoldMinHours + 1));

        /// <summary>The colony buys the contract (the company stands with it against that raid), or pays them to stand aside.</summary>
        public static int BuyOutPrice(float points) => Clamp(Round10(Clamp(points, 0f, 10000f) * 0.6f), 200, 2500);

        public static int StandAsidePrice(float points) => Clamp(Round10(Clamp(points, 0f, 10000f) * 0.3f), 100, 1200);

        /// <summary>Marching with the raid, the Banner's part of its points (the storyteller's own size, theirs within it).</summary>
        public const float BannerShare = 0.35f;

        /// <summary>The soldiers who stand with the colony when it bought the contract: by the raid, two to six.</summary>
        public static int Allies(float points) => Clamp((int)(Clamp(points, 0f, 10000f) * BannerShare / 180f), 2, 6);

        public const int BuyOutGoodwill = 3, StandAsideGoodwill = 1;

        // ================= 58. A company on the march (the Ashen Banner) =================

        /// <summary>They drill at dawn.</summary>
        public static bool DrillHour(int hour) => hour >= 5 && hour <= 7;

        public const int DrillTicks = 1500;

        /// <summary>The food they buy in quantity (the second answer): so many meals, at a quarter over their worth.</summary>
        public const int FoodMeals = 20;

        public static int FoodPay(float worth) => Round10(Clamp(worth, 0f, 100000f) * 1.25f);

        /// <summary>It can turn, once a stay each: the enemy they march against catches them here; a bet at the fire becomes a brawl.</summary>
        public const float CaughtChance = 0.2f, BetChance = 0.3f;

        /// <summary>A soldier who asks to stay and is taken in: the Banner holds it against the colony.</summary>
        public const int StayedGoodwill = -4, MarchGoodwill = 2;

        /// <summary>A colonist enlisted for a season with the company: their days, their pay, what they learn.</summary>
        public const int EnlistDays = 15, EnlistPay = 150;

        public const float EnlistXp = 2500f;
    }
}
