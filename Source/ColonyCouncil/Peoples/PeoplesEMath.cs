using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of release E's four peoples (docs/design/peoples.md, "Release E"; docs/design/peoples-next.md), with
    /// no game types so Tools/Tests can check them: the Stormglass Conclave's watch on the sky (which of the game's
    /// conditions it foresees, how long it holds one back to tell it first, what the fields reckon it as, its fee), its rod
    /// and its lesson; the Mossborn's ledger of the colony's planting and felling and what it does to their goodwill, their
    /// saplings and their graves' trees; the Ashen Banner's squad, its wages by the age it lives in and its Roll; the Kiln
    /// Guild's pay by quality, its bids on masterworks and its wander-year.
    /// </summary>
    public static class PeoplesEMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        private static int Round5(float v) => (int)Math.Round(v / 5f, MidpointRounding.AwayFromZero) * 5;

        private static int Round10(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        // ================= The Stormglass Conclave: the watch on the sky =================

        /// <summary>
        /// The game's conditions of the sky the Conclave reads (by the incident that brings each): the cold snap and the
        /// heat wave, toxic fallout, the volcanic winter, a flashstorm, an eclipse; with Biotech the noxious haze; with
        /// Odyssey and Vanilla Events Expanded their droughts, whiteouts, scorches and the long night.
        /// </summary>
        public static readonly string[] SkyIncidents =
        {
            "ColdSnap", "HeatWave", "ToxicFallout", "VolcanicWinter", "Flashstorm", "Eclipse", "NoxiousHaze", "Drought", "VEE_Drought", "VEE_Whiteout", "VEE_Scorch",
            "VEE_LongNight"
        };

        public static bool Foreseen(string incident) => incident != null && Array.IndexOf(SkyIncidents, incident) >= 0;

        /// <summary>A watch bought from the Conclave runs a season.</summary>
        public const int WatchDays = 15;

        /// <summary>A condition the storyteller sends while the watch runs is held back this many hours (a day or so), so the warning comes first.</summary>
        public const int HoldMinHours = 18, HoldMaxHours = 30;

        /// <summary>Held conditions the game may try to bring again for this long after their hour, if the first try can't.</summary>
        public const int RetryHours = 12;

        /// <summary>The hours a condition is held for a roll in [0, 1).</summary>
        public static int HoldHours(float roll)
        {
            if (float.IsNaN(roll) || roll < 0f) roll = 0f;
            return Math.Min(HoldMaxHours, HoldMinHours + (int)(roll * (HoldMaxHours - HoldMinHours + 1)));
        }

        /// <summary>The fee for a season's watch: a little more for a richer colony, to the nearest ten, 80 to 400 silver.</summary>
        public static int WatchFee(float wealth)
        {
            if (float.IsNaN(wealth) || wealth < 0f) wealth = 0f;
            return Round10(Math.Max(80f, Math.Min(400f, 80f + wealth / 1500f)));
        }

        /// <summary>
        /// What a held condition does to the air, as the fields by the weather reckon it (the game's own offsets: a cold
        /// snap 20 °C colder at its depth, a heat wave 17 °C warmer, a volcanic winter 7 °C colder); none for the rest.
        /// </summary>
        public static float Offset(string condition)
        {
            switch (condition)
            {
                case "ColdSnap": return -20f;
                case "HeatWave": return 17f;
                case "VolcanicWinter": return -7f;
                default: return 0f;
            }
        }

        /// <summary>The hours from now a held condition comes, and how many hours of the forecast it covers (its usual length), within the forecast.</summary>
        public static void Span(int fireTick, int now, float meanDays, int forecastHours, out int from, out int hours)
        {
            from = fireTick <= now ? 0 : (fireTick - now) / HourTicks;
            if (float.IsNaN(meanDays) || meanDays <= 0f) meanDays = 1f;
            hours = Math.Max(0, Math.Min(forecastHours - from, (int)Math.Round(meanDays * 24f)));
        }

        // ================= The Stormglass Conclave: the rod and the lesson =================

        /// <summary>Lightning that would strike the open ground within this many cells of the Conclave's rod strikes the rod instead.</summary>
        public const float RodRadius = 18f;

        public static bool Draws(int dx, int dz) => (float)dx * dx + (float)dz * dz <= RodRadius * RodRadius;

        /// <summary>The colony's weather-reader taught to read the glass reads it for this long, and this much better (never past a weather station's).</summary>
        public const int TaughtDays = 60;

        public const float TaughtBonus = 0.15f, TaughtMax = 0.95f;

        public static float Taught(float accuracy) => float.IsNaN(accuracy) ? TaughtMax : Math.Min(TaughtMax, Math.Max(0f, accuracy) + TaughtBonus);

        /// <summary>An apprentice of the glass studies the colony's sky this many days (a season's share).</summary>
        public const float ApprenticeMinDays = 5f, ApprenticeMaxDays = 8f;

        /// <summary>A night's watch of the sky lying out in the open, in hours.</summary>
        public const int SkyHours = 2;

        // ================= The Mossborn: the ledger of planting and felling =================

        /// <summary>The Mossborn reckon the colony's trees each season.</summary>
        public const int ReckonDays = 15;

        /// <summary>Fewer trees than this felled and planted together in a season is nothing to them.</summary>
        public const int LedgerLeast = 10;

        /// <summary>
        /// The goodwill a season's planting and felling is worth to the Mossborn: every tree felled owes a seedling, so what
        /// counts is the trees planted less the trees felled. Thirty or more to the good warms them by five, ten or more by
        /// three; twenty or more owed cools them by three, sixty or more by six; between, nothing, and nothing at all for a
        /// season of fewer than ten trees either way.
        /// </summary>
        public static int Shift(int felled, int planted)
        {
            felled = Math.Max(0, felled);
            planted = Math.Max(0, planted);
            if (felled + planted < LedgerLeast) return 0;
            int net = planted - felled;
            if (net >= 30) return 5;
            if (net >= 10) return 3;
            if (net <= -60) return -6;
            if (net <= -20) return -3;
            return 0;
        }

        /// <summary>Cooler, never an enemy: a cooling never takes their goodwill below none; a warming is whole.</summary>
        public static int Kept(int goodwill, int shift) => shift >= 0 ? shift : Math.Max(shift, Math.Min(0, -goodwill));

        /// <summary>The seed-carrier comes with saplings once the colony has felled this many trees in a season.</summary>
        public const int SaplingsAfter = 12;

        /// <summary>How many saplings they bring for so many felled: one for every three, six to fourteen.</summary>
        public static int Saplings(int felled) => Math.Max(6, Math.Min(14, Math.Max(0, felled) / 3));

        /// <summary>A sapling planted by hand: so long at each, and how far grown it is when set.</summary>
        public const int PlantTicks = 450;

        public const float SaplingGrowthMin = 0.06f, SaplingGrowthMax = 0.14f;

        /// <summary>A tree for one of the colony who died within this many days and lies in a grave.</summary>
        public const int GroveDays = 10;

        public static bool GroveFresh(int deadTicks) => deadTicks >= 0 && deadTicks <= (long)GroveDays * DayTicks;

        /// <summary>A tree-connector of the Mossborn tends the colony's Gauranlen tree: its connection this much stronger, never past full.</summary>
        public const float TendStrength = 0.15f;

        public static float Tended(float strength) => float.IsNaN(strength) ? TendStrength : Math.Min(1f, Math.Max(0f, strength) + TendStrength);

        // ================= The Ashen Banner: the squad and its wages =================

        /// <summary>A squad of three to five, a sergeant among them, hired for a season and paid every five days.</summary>
        public const int SquadMin = 3, SquadMax = 5, HireDays = 15, WageDays = 5;

        /// <summary>What a soldier's five days cost, by the age the Banner lives in: their kit is the age's.</summary>
        public static float AgeFactor(int age) => age <= 0 ? 0.6f : age == 1 ? 1f : age == 2 ? 1.6f : 2.4f;

        public const float SoldierWage = 60f;

        /// <summary>A wage for the squad for five days, to the nearest ten, 60 at least.</summary>
        public static int Wage(int soldiers, int age) => Math.Max(60, Round10(Math.Max(1, soldiers) * SoldierWage * AgeFactor(age)));

        /// <summary>The wage due at this day of the contract: the first at hiring, then at the fifth and the tenth; none after.</summary>
        public static int WageNumber(float daysHere) => float.IsNaN(daysHere) || daysHere < 0f ? 0 : (int)(daysHere / WageDays);

        public static bool WageDue(float daysHere, int paidThrough) => WageNumber(daysHere) > paidThrough && WageNumber(daysHere) * WageDays < HireDays;

        /// <summary>Goodwill with the Banner: the contract kept to its end, a wage left unpaid, a deserter taken in.</summary>
        public const int ContractGoodwill = 6, UnpaidGoodwill = -8, DeserterTaken = -3;

        /// <summary>The Banner reads the Roll at the gate for one of the colony who fell fighting within this many days.</summary>
        public const int RollDays = 30;

        public static bool RollFor(int fellTick, int now) => fellTick >= 0 && now >= fellTick && now - fellTick <= (long)RollDays * DayTicks;

        // ================= The Kiln Guild: pay by quality, bids, the wander-year =================

        /// <summary>
        /// What the guild pays for a commissioned piece over its worth, by its quality (the game's: awful 0 to legendary 6):
        /// half for awful work, under its worth for poor, more the better it is, three times and more for a legend.
        /// </summary>
        public static float QualityFactor(int quality)
        {
            switch (quality)
            {
                case 0: return 0.5f;
                case 1: return 0.8f;
                case 2: return 1.2f;
                case 3: return 1.5f;
                case 4: return 1.9f;
                case 5: return 2.5f;
                case 6: return 3.2f;
                default: return 1f;
            }
        }

        /// <summary>The guild's pay for a piece worth so much of this quality, to the nearest five, 40 at least.</summary>
        public static int GuildPay(float value, int quality) => float.IsNaN(value) || value <= 0f ? 40 : Math.Max(40, Round5(value * QualityFactor(quality)));

        /// <summary>The guild bids on masterworks and legends (quality 5 and 6) only.</summary>
        public const int BidQuality = 5;

        /// <summary>A master-trader's bid: well over its worth, a legend's more, to the nearest ten, 200 at least; none for lesser work.</summary>
        public static int Bid(float value, int quality)
        {
            if (quality < BidQuality || float.IsNaN(value) || value <= 0f) return 0;
            return Math.Max(200, Round10(value * (quality >= 6 ? 2.4f : 1.8f)));
        }

        /// <summary>The wander-year's stay at the colony: four to six days at the colony's best maker's bench, two hours a day.</summary>
        public const float WanderMinDays = 4f, WanderMaxDays = 6f;

        public const int BenchHours = 2;

        /// <summary>The apprentice's piece left behind: of good quality, a mark on it.</summary>
        public const int LeftQuality = 3;
    }
}
