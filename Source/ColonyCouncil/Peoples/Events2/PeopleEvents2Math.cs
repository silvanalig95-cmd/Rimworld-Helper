using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 2 (docs/peoples-events-proposals.md, items 14, 22, 26, 59, 12, 25, 3,
    /// 20, 40 and 63; Peoples/Events2/): how far a season away may go and how long it stays, a pilgrimage's days and what it
    /// eases, the league's guard and its share, the enclave's terms and their instalments, the river folk's crossing and
    /// their knot night, the guild's mark and its price, the herders' dry-year camp, the enclave's pad and its rent, the
    /// archive kept from the fire, the beast-doctor's leg, and the Signal Line's news for sale. Shared with the tests
    /// (Tools/Tests/PeopleEvents2Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents2Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        // ================= The season away (14 and 3; later 67 and 98) =================

        /// <summary>The stages of a season away: walking to the map's edge, on the road there, there, on the road home.</summary>
        public const int SettingOut = 0, Going = 1, There = 2, Coming = 3;

        /// <summary>Days on the world's roads for a walking caravan of one: half a day, and four tenths of a day a tile.</summary>
        public static float RoadDays(float tiles) => 0.5f + (float.IsNaN(tiles) ? 0f : Math.Max(0f, tiles)) * 0.4f;

        /// <summary>The longest a season away may run before the colony gives up waiting (the visit's last day).</summary>
        public const int AwayMaxDays = 90;

        // ================= 14. A pilgrimage (the pilgrim fellowship) =================

        /// <summary>The colonist is away 10 to 30 days in all, the roads both ways and the days at the shrine.</summary>
        public const int PilgrimMinDays = 10, PilgrimMaxDays = 30;

        /// <summary>At least two days at the shrine.</summary>
        public const float ShrineMinDays = 2f;

        /// <summary>A shrine is within reach when both roads and two days there fit in the thirty days.</summary>
        public static bool ShrineInReach(float roadDays) => !float.IsNaN(roadDays) && roadDays >= 0f && 2f * roadDays + ShrineMinDays <= PilgrimMaxDays;

        /// <summary>
        /// The days at the shrine: a whole time away of 10 to 30 days by <paramref name="roll"/> (0 to 1), less both roads,
        /// never under two days.
        /// </summary>
        public static float ShrineDays(float roadDays, float roll)
        {
            float total = PilgrimMinDays + Clamp(roll, 0f, 1f) * (PilgrimMaxDays - PilgrimMinDays);
            float road = float.IsNaN(roadDays) ? 0f : Math.Max(0f, roadDays);
            return Math.Max(ShrineMinDays, total - 2f * road);
        }

        /// <summary>The whole time away, roads and shrine, in days.</summary>
        public static float AwayDays(float roadDays, float shrineDays) => 2f * Math.Max(0f, float.IsNaN(roadDays) ? 0f : roadDays) + Math.Max(0f, float.IsNaN(shrineDays) ? 0f : shrineDays);

        /// <summary>The pilgrims' cough caught on the crowded road, through the harm budget (the hostel's is 0.05 an evening).</summary>
        public const float PilgrimCough = 0.08f;

        /// <summary>Ideology's certainty steadied at the shrine.</summary>
        public const float PilgrimCertainty = 0.15f;

        public const int PilgrimGoodwill = 6;

        /// <summary>A grief or guilt the shrine eased: its memory worn to three quarters of its time.</summary>
        public const float Eased = 0.75f;

        /// <summary>The age a memory is set to so that it is eased: three quarters of its whole time, never younger than it is.</summary>
        public static int EasedAge(int age, int duration) => duration <= 0 ? Math.Max(0, age) : Math.Max(Math.Max(0, age), (int)(duration * Eased));

        /// <summary>The shrine's kinds: a fellowship town, a town that keeps a relic (Ideology), an old shrine at a landmark.</summary>
        public static readonly string[] ShrineKinds = { "town", "relic", "old" };

        /// <summary>
        /// A small token of the relic's shrine brought home with the shell (Vanilla Ideology Expanded - Relics and Artifacts'
        /// inert relics, by name); without it, the shell alone.
        /// </summary>
        public static readonly string[] RelicTokens = { "VIE_RelicInertMedallion", "VIE_RelicInertStatuette", "VIE_RelicInertAmulet", "VIE_RelicInertRing", "VIE_RelicInertIdol" };

        // ================= 3. One guard, one wagon (the road league) =================

        /// <summary>Days walking under the colony's flag with the great caravan, at its town.</summary>
        public const float GuardMinDays = 4f, GuardMaxDays = 7f;

        /// <summary>
        /// The colony's share of the caravan's profit for its guard and its beast: 45 silver a day, more for the guard's way
        /// with a bargain (four points a level of Social), and the league's age's trade.
        /// </summary>
        public static int GuardShare(float days, int social, int age)
        {
            float d = Clamp(days, 0f, 30f);
            float a = age <= 0 ? 0.7f : age == 1 ? 0.85f : age == 2 ? 1f : 1.25f;
            return Math.Max(60, Round10(45f * d * (1f + 0.04f * Math.Max(0, Math.Min(20, social))) * a));
        }

        /// <summary>The trade lesson on the road: Social practice.</summary>
        public const float GuardLessonXp = 2400f;

        public const int GuardGoodwill = 8;

        /// <summary>The league's caravans come half again as often for a year (sixty days).</summary>
        public const float GuardCaravans = 1.5f;
        public const int GuardYearDays = 60;

        /// <summary>A pack beast fit for the road: a grown pack animal, not downed.</summary>
        public static bool PackBeast(bool packAnimal, bool grown, bool downed) => packAnimal && grown && !downed;

        // ================= 22. The body made better, on terms (the spacer enclave) =================

        /// <summary>The game's bionics the factor offers, by the body part they go on.</summary>
        public static string BionicRecipe(string bodyPart)
        {
            switch (bodyPart)
            {
                case "Eye": return "InstallBionicEye";
                case "Arm":
                case "Shoulder": return "InstallBionicArm";
                case "Leg": return "InstallBionicLeg";
                case "Spine": return "InstallBionicSpine";
            }
            return null;
        }

        /// <summary>With Alpha Implants, the factor's part for a colonist's bonded beast: its animal bionics, by the body part.</summary>
        public static string BeastBionicRecipe(string bodyPart)
        {
            switch (bodyPart)
            {
                case "Eye": return "AI_AnimalBionicEye";
                case "Leg": return "AI_AnimalBionicLeg";
                case "Spine": return "AI_AnimalBionicSpine";
            }
            return null;
        }

        /// <summary>The price paid at once: the part's worth and a little for the surgeon's drop, within 300 and 4000.</summary>
        public static int BionicPrice(float partWorth) => Math.Max(300, Math.Min(4000, Round10((float.IsNaN(partWorth) ? 0f : Math.Max(0f, partWorth)) * 1.1f)));

        /// <summary>On terms it costs a fifth more in all.</summary>
        public static int TermsTotal(int price) => Round10(Math.Max(0, price) * 1.2f);

        /// <summary>A fifth down.</summary>
        public static int Down(int total) => Round10(Math.Max(0, total) / 5f);

        /// <summary>Four seasonal instalments, one asked for by letter each season (fifteen days).</summary>
        public const int Instalments = 4, InstalmentDays = 15;

        /// <summary>The <paramref name="n"/>th instalment (1 to 4): an even quarter of what is left after the down payment, the last taking the remainder.</summary>
        public static int Instalment(int total, int down, int n)
        {
            int rest = Math.Max(0, total - down);
            int each = Round10(rest / (float)Instalments);
            if (n < 1 || n > Instalments) return 0;
            return n < Instalments ? each : Math.Max(0, rest - each * (Instalments - 1));
        }

        /// <summary>The enclave's surgeon, at whose hands the game's surgery rarely fails.</summary>
        public const int SurgeonMin = 14, SurgeonMax = 18;

        public const int FitTicks = 2 * HourTicks;
        public const int FitGoodwill = 3, PaidUpGoodwill = 4, DefaultGoodwill = -15;

        /// <summary>How many terms the colony's record keeps (open ones and the last few closed).</summary>
        public const int MaxTerms = 6;

        // ================= 26. A crossing on the colony's river (the river folk) =================

        /// <summary>A crossing's water no wider than this many cells.</summary>
        public const int CrossingMaxWater = 22;

        /// <summary>Planks laid on each bank.</summary>
        public const int BankPlanks = 2;

        /// <summary>The game's bridge of the river folk's age: rope and plank, then timber on piles (Odyssey's heavy bridge).</summary>
        public static string BridgeFor(int age) => age >= 2 ? "HeavyBridge" : "Bridge";

        /// <summary>The colony's wood for it: each span's and plank's own cost, at least 20.</summary>
        public static int CrossingWood(int water, int planks, int perSpan, int perPlank) =>
            Math.Max(20, Math.Max(0, water) * Math.Max(1, perSpan) + Math.Max(0, planks) * Math.Max(0, perPlank));

        /// <summary>A boatman's work on one cell of the crossing: an hour on the water, half that on the bank.</summary>
        public const int SpanTicks = HourTicks, PlankTicks = HourTicks / 2;

        /// <summary>The river folk's knot night: one day of the sixty in the year, by their people's id.</summary>
        public static int KnotDay(int factionId) => ((factionId % 60) * 37 % 60 + 60) % 60;

        /// <summary>It is their knot night (the day of the year matches), and not yet rested at the crossing this year.</summary>
        public static bool KnotNight(int dayOfYear, int knotDay, int year, int lastYear) => dayOfYear == knotDay && year != lastYear;

        public const int MaxCrossings = 4;
        public const int CrossingGoodwill = 3, KnotGoodwill = 2;

        // ================= 59. The guild's mark (the Kiln Guild) =================

        /// <summary>A gun's or a bow's unique kind is its own name with this end (Odyssey's, Vanilla Weapons Expanded's).</summary>
        public const string UniqueSuffix = "_Unique";

        /// <summary>The unique kind of a weapon by name ("Bow_Great" to "Bow_Great_Unique"); null for one already unique.</summary>
        public static string UniqueOf(string defName) => string.IsNullOrEmpty(defName) || defName.EndsWith(UniqueSuffix, StringComparison.Ordinal) ? null : defName + UniqueSuffix;

        /// <summary>The master's price in silver: a third of what the work adds to its worth, within 150 and 900.</summary>
        public static int MarkPrice(float before, float after)
        {
            float gain = (float.IsNaN(after) ? 0f : after) - (float.IsNaN(before) ? 0f : before);
            return Math.Max(150, Math.Min(900, Round10(Math.Max(0f, gain) / 3f)));
        }

        /// <summary>With the colony's own crafter at the bench beside the master, a quarter off.</summary>
        public static int WithCrafter(int price) => Math.Max(100, Round10(Math.Max(0, price) * 0.75f));

        /// <summary>A blade's better stuff: sharper by a tenth or more.</summary>
        public static bool Better(float sharpNow, float sharpNew) => !float.IsNaN(sharpNow) && !float.IsNaN(sharpNew) && sharpNew >= sharpNow * 1.1f && sharpNew > 0f;

        /// <summary>A day's work at the bench (a working day).</summary>
        public const int MarkTicks = 8 * HourTicks;
        public const float CrafterXp = 2000f;
        public const int MarkGoodwill = 2;

        /// <summary>The names the guild's marks put in labels the colony's record keeps.</summary>
        public const int MaxMarks = 40;

        // ================= 12. Water in the dry year (the herding folk) =================

        /// <summary>Up to a dozen head (and their dogs) to the colony's water.</summary>
        public const int HerdMax = 12;

        /// <summary>The toll asked for the water: ten silver a head, within 40 and 200, by the colony's wealth.</summary>
        public static int Toll(int head, float wealthFactor) => Math.Max(40, Math.Min(200, Round10(10f * Math.Max(1, head) * Clamp(wealthFactor, 0.5f, 2f))));

        public const int DryGoodwill = 10, TollGoodwill = -3, TurnedAwayGoodwill = -4;

        /// <summary>Hay carried out to them when they ask.</summary>
        public const int HayAsked = 60;

        /// <summary>A day the herd strays into the colony's fields (once a stay), twice as likely on the colony's own grazing.</summary>
        public static float StrayChance(bool grazing) => grazing ? 0.24f : 0.12f;

        /// <summary>A predator that follows the herd in (once a stay), through the harm budget (a camp's draw).</summary>
        public const float PredatorChance = 0.3f;

        /// <summary>Turned away, the head they lose on the road to the next water: two to half the herd, by a roll.</summary>
        public static int LostOnRoad(int herd, float roll)
        {
            int most = Math.Max(2, Math.Max(0, herd) / 2);
            return 2 + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (most - 1));
        }

        public const int LostNewsMinDays = 2, LostNewsMaxDays = 4;

        // ================= 25. A pad by the colony (the spacer enclave) =================

        /// <summary>
        /// Their rent, at an exact figure "to the last digit": about 90 silver by the colony's wealth, never a round number
        /// (<paramref name="digits"/> 1 to 9 is the last digit).
        /// </summary>
        public static int PadRent(float wealthFactor, int digits)
        {
            int baseRent = Round10(90f * Clamp(wealthFactor, 0.5f, 3f));
            int d = Math.Max(1, Math.Min(9, digits));
            return Math.Max(41, baseRent + d);
        }

        /// <summary>Raised by the colony: half again.</summary>
        public static int Raised(int rent) => Math.Max(1, (int)Math.Round(Math.Max(0, rent) * 1.5f));

        /// <summary>Their chance to pay a raised rent rather than go: less for a proud group, less with tension high.</summary>
        public static float PaysRaise(float pride, float tension) => Clamp(0.75f - Clamp(pride, 0f, 1f) * 0.5f - Clamp(tension, 0f, 100f) / 200f, 0.1f, 0.9f);

        public const int RentDays = 5;

        /// <summary>The stock the pod brings draws a band against the pad more than a usual camp (on top of the camps' own).</summary>
        public const float PadBandChance = 0.15f;

        /// <summary>What they pay for the colony's meals: their worth and a tenth more, rounded up to the silver.</summary>
        public static int MealsPrice(float worth) => float.IsNaN(worth) || worth <= 0f ? 0 : (int)Math.Ceiling(worth * 1.1f);

        public const int MealsAsked = 10;
        public const int StockHour = 6;
        public const int PadSize = 5;

        // ================= 20. Books kept from the fire (the learned society) =================

        public const int ArchiveMin = 4, ArchiveMax = 8;
        public const int ArchiveMinDays = 12, ArchiveMaxDays = 18;
        public const int ArchiveGoodwill = 8;

        /// <summary>How many books: four to eight by a roll in [0, 1).</summary>
        public static int ArchiveBooks(float roll) => ArchiveMin + Math.Max(0, Math.Min(ArchiveMax - ArchiveMin, (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (ArchiveMax - ArchiveMin + 1))));

        /// <summary>The archive's books by the society's age and the book's place in the cart: the game's books, and with Vanilla Books Expanded its papers.</summary>
        public static string ArchiveBook(int age, int i, bool papers)
        {
            var list = new List<string> { "TextBook", "Novel" };
            if (age >= 2) list.Add("Schematic");
            if (papers && age >= 2) list.Add("VBE_Newspaper");
            return list[((i % list.Count) + list.Count) % list.Count];
        }

        /// <summary>The game's Anomaly tome, kept sealed in a crate.</summary>
        public const string Tome = "Tome";

        /// <summary>The fetch: every book there is thanks; any lost, sold or a seal broken is a lasting wrong.</summary>
        public static bool Kept(int total, int present, int sold, int opened) => total > 0 && sold <= 0 && opened <= 0 && present >= total;

        /// <summary>A book lost or sold: the society's lasting wrong ("robbed").</summary>
        public const int LostWeight = 20;

        // ================= 40. A leg for a lame beast (the Circuit of Tamers) =================

        /// <summary>The Circuit's leg for a beast by its age: Alpha Implants' animal prosthetic from the industrial age, else none (the stables).</summary>
        public static string BeastLeg(int age) => age >= 2 ? "AI_AnimalProstheticLeg" : null;

        /// <summary>A beast that lost a leg: the leg, or the leg's own parts below it.</summary>
        public static bool IsLeg(string bodyPart) => bodyPart == "Leg" || bodyPart == "FrontLeg" || bodyPart == "RearLeg" || bodyPart == "Paw" || bodyPart == "Hoof";

        /// <summary>Her price for the fitting: the part's worth and a little, within 150 and 900.</summary>
        public static int LegPrice(float partWorth) => Math.Max(150, Math.Min(900, Round10((float.IsNaN(partWorth) ? 0f : Math.Max(0f, partWorth)) * 1.1f)));

        public const int LegTicks = 3 * HourTicks / 2;
        public const int LegGoodwill = 3, StablesGoodwill = 4;

        // ================= 63. News for sale (the Signal Line) =================

        /// <summary>The kinds of news the masts heard: an item stash, a wreck's cargo, a bandit outpost.</summary>
        public static readonly string[] NewsKinds = { "stash", "wreck", "outpost" };

        /// <summary>The game's own quest each kind of news makes.</summary>
        public static string NewsQuest(string kind)
        {
            switch (kind)
            {
                case "stash": return "OpportunitySite_ItemStash";
                case "wreck": return "OpportunitySite_OrbitalWreck";
                case "outpost": return "OpportunitySite_BanditCamp";
            }
            return null;
        }

        /// <summary>Its price, 60 to 200 silver: a stash 60 to 120, a wreck 100 to 160, an outpost 140 to 200, by a roll.</summary>
        public static int NewsPrice(string kind, float roll)
        {
            float r = Clamp(roll, 0f, 1f);
            switch (kind)
            {
                case "stash": return Round10(60f + 60f * r);
                case "wreck": return Round10(100f + 60f * r);
                case "outpost": return Round10(140f + 60f * r);
            }
            return 0;
        }

        public const int NewsGoodwill = 1;
    }
}
