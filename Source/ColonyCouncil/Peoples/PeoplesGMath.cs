using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of release G's four peoples (docs/design/peoples.md, "Release G"; docs/design/peoples-third.md, step
    /// 3), with no game types so Tools/Tests can check them: the Helix Works' sample from a colonist with rare genes (how
    /// rare, its price, the small draw), its gene work (which genes, their price), its case of medicine and drugs and its
    /// batch gone wrong; the Lattice's repairs to the colony's mechs, its lost mech (returned or kept at a cost), its
    /// part-trader's prices and its engineers' turret; the Choir of the Spire's listening (who feels calm, who unease), its
    /// trade of psychic and archotech goods for art and its listener's stay; the Sunken Vault's decline unless supplied,
    /// its barter of its last spacer goods for components and food, the craft it lost and the child who never saw a sky.
    /// </summary>
    public static class PeoplesGMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        private static int Round5(float v) => (int)Math.Round(v / 5f, MidpointRounding.AwayFromZero) * 5;

        private static int Round10(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        private static float Safe(float v, float or) => float.IsNaN(v) || float.IsInfinity(v) ? or : v;

        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));

        // ================= The Helix Works: the sample =================

        /// <summary>
        /// How rare a colonist's genes are to the Works: an archite gene counts three (nobody makes those any more), a gene
        /// of a xenotype that isn't a baseliner's one, to twelve of them; a baseliner with nothing put in is nothing to them.
        /// </summary>
        public static int Rarity(int archite, int xenotypeGenes, bool baseliner)
        {
            int a = Math.Max(0, archite), x = baseliner ? 0 : Math.Min(12, Math.Max(0, xenotypeGenes));
            return a * 3 + x;
        }

        /// <summary>A colonist this rare is worth a sample: an archite gene, or a xenotype of three genes or more.</summary>
        public const int RareFrom = 3;

        public static bool WorthASample(int rarity) => rarity >= RareFrom;

        /// <summary>What the sample-buyer pays: 80 silver and 25 for each point of rarity, to the nearest five, at most 680.</summary>
        public static int SamplePrice(int rarity) => Round5(80f + 25f * Math.Min(24, Math.Max(0, rarity)));

        /// <summary>
        /// The draw: a little blood from the arm, below the game's first stage of blood loss (0.15), so it is in their
        /// health and nothing more, and gone in a few hours. It is the colony's answer, not a mishap, so the harm budget
        /// doesn't roll it.
        /// </summary>
        public const float DrawSeverity = 0.08f;

        /// <summary>The draw takes this long, with a bar, the sample-buyer at the colonist's side.</summary>
        public const int DrawTicks = 900;

        /// <summary>The colonist keeps still for the draw this long (the buyer walks to them).</summary>
        public const int HoldTicks = 2 * HourTicks;

        // ================= The Helix Works: gene work =================

        /// <summary>
        /// The genes the Works write into a colonist at the gate, the gentle ones of their own lab (Biotech's): each only if
        /// the colonist lacks it and it conflicts with nothing they carry.
        /// </summary>
        public static readonly string[] Offered =
        {
            "Immunity_Strong", "WoundHealing_Fast", "Superclotting", "StrongStomach", "ToxResist_Partial", "Robust", "DarkVision", "MinTemp_SmallDecrease",
            "MaxTemp_SmallIncrease", "Pain_Reduced"
        };

        /// <summary>What a gene costs at their bench: 180 silver, 90 for each point of metabolism it takes (a better gene costs the body more), 40 for each of complexity.</summary>
        public static int GenePrice(int metabolism, int complexity) => Round10(180f + 90f * Math.Max(0, -metabolism) + 40f * Math.Max(0, complexity));

        /// <summary>The sequencer works on the colonist this long, with a bar.</summary>
        public const int GeneWorkTicks = 2 * HourTicks;

        /// <summary>Paid with one of the colony's genepacks instead of silver: the Works buy genepacks.</summary>
        public const int GenepackPay = 1;

        // ================= The Helix Works: the case =================

        /// <summary>The vat-keeper's case by the colony's wealth: medicine, a little glitterworld medicine, penoxycyline.</summary>
        public static int CaseMedicine(float wealthFactor) => (int)Math.Round(Clamp(6f * Safe(wealthFactor, 1f), 4f, 12f));

        public static int CaseUltratech(float wealthFactor) => Safe(wealthFactor, 1f) >= 1.5f ? 2 : 1;

        public static int CasePenoxycyline(float wealthFactor) => (int)Math.Round(Clamp(4f * Safe(wealthFactor, 1f), 3f, 8f));

        /// <summary>The case's price: a tenth over its worth, to the nearest five.</summary>
        public static int CasePrice(float worth) => Round5(Math.Max(0f, Safe(worth, 0f)) * 1.1f);

        // ================= The Helix Works: a batch gone wrong =================

        /// <summary>The batch at the gate: one to three of their vat-grown, sick with a gene fault.</summary>
        public static int BatchSize(float roll)
        {
            if (float.IsNaN(roll) || roll < 0f) roll = 0f;
            return Math.Min(3, 1 + (int)(roll * 3f));
        }

        /// <summary>The fault's severity when they come: never near the worst (it needs tending, not burying).</summary>
        public const float FaultStart = 0.45f;

        /// <summary>Medicine for each of the batch when the colony gives it instead of tending them.</summary>
        public const int BatchMedicineEach = 2;

        /// <summary>The Works think the better of a colony that saw to its batch.</summary>
        public const int BatchGoodwill = 8;

        // ================= The Lattice: repairs =================

        /// <summary>The mech-tender sees to this many of the colony's hurt mechs at most, a while at each.</summary>
        public const int RepairMax = 6;

        public const int RepairTicks = 1500;

        /// <summary>What the repairs cost: 40 silver a mech and 15 a wound, to the nearest ten, at most 600.</summary>
        public static int RepairPrice(int mechs, int wounds) => Math.Min(600, Round10(40f * Math.Max(0, mechs) + 15f * Math.Max(0, wounds)));

        /// <summary>Paid with a basic subcore instead of silver: the Lattice buys subcores.</summary>
        public const int SubcorePay = 1;

        // ================= The Lattice: the lost mech =================

        /// <summary>Kept by the colony, the Lattice thinks the worse of it; returned, the better.</summary>
        public const int KeptGoodwill = -15, ReturnedGoodwill = 6;

        /// <summary>The colony may keep the mech when a mechanitor of its own has the bandwidth for it.</summary>
        public static bool CanKeep(bool mechanitor, float freeBandwidth, float cost) => mechanitor && !float.IsNaN(freeBandwidth) && freeBandwidth >= Math.Max(0f, Safe(cost, 1f));

        /// <summary>The link-warden relinks the mech this long, with a bar, before they walk off together.</summary>
        public const int RelinkTicks = 600;

        /// <summary>The lost mech wanders near where it came in for this long before it finds its own way home.</summary>
        public const int LostHours = 20;

        // ================= The Lattice: parts and the turret =================

        /// <summary>The part-trader comes when the colony's subcores and chips are worth this much.</summary>
        public const float PartsMinWorth = 150f;

        /// <summary>What they pay for them: a fifth over their worth, to the nearest five.</summary>
        public static int PartsPrice(float worth) => Round5(Math.Max(0f, Safe(worth, 0f)) * 1.2f);

        /// <summary>The engineers' turret: its worth and a tenth, to the nearest ten; raised in two hours.</summary>
        public static int TurretPrice(float worth) => Round10(Math.Max(0f, Safe(worth, 0f)) * 1.1f);

        public const int TurretHours = 2;

        /// <summary>The turret stands this far from the gate at most, on clear ground.</summary>
        public const int TurretMaxCells = 12;

        // ================= The Choir of the Spire: the listening =================

        /// <summary>The listeners sit this long in the colony's middle, a bar over each.</summary>
        public const int ListenHours = 3;

        /// <summary>Someone this dull to the psychic hum or less feels nothing of it.</summary>
        public const float DeafBelow = 0.05f;

        /// <summary>
        /// What someone at home feels when the Choir listens: +1 a calm, -1 an unease, 0 nothing. The psychically deaf feel
        /// nothing; for the rest, unease one time in five, more for the sensitive (a third more for each point of psychic
        /// sensitivity over the ordinary), more still when the void stirs (Anomaly: the monolith awake), never more than
        /// three in five; calm otherwise.
        /// </summary>
        public static int Felt(float sensitivity, float roll, bool voidStirs)
        {
            float s = Safe(sensitivity, 1f);
            if (s < DeafBelow) return 0;
            if (float.IsNaN(roll)) roll = 1f;
            return roll < UneaseChance(s, voidStirs) ? -1 : 1;
        }

        public static float UneaseChance(float sensitivity, bool voidStirs) =>
            Clamp(0.2f + 0.35f * (Safe(sensitivity, 1f) - 1f) + (voidStirs ? 0.15f : 0f), 0.05f, 0.6f);

        // ================= The Choir of the Spire: art for their goods =================

        /// <summary>The keeper of the hum comes when the colony has art packed up worth this much.</summary>
        public const float ArtMinWorth = 300f;

        /// <summary>
        /// Which of their goods (by worth) they give for art worth <paramref name="artWorth"/>: the dearest no more than a
        /// quarter over it; -1 when even the cheapest is.
        /// </summary>
        public static int Pick(float artWorth, IList<float> goods)
        {
            if (goods == null || float.IsNaN(artWorth)) return -1;
            int best = -1;
            for (int i = 0; i < goods.Count; i++)
            {
                float g = goods[i];
                if (float.IsNaN(g) || g <= 0f || g > artWorth * 1.25f) continue;
                if (best < 0 || g > goods[best]) best = i;
            }
            return best;
        }

        /// <summary>They take up to this many pieces, the dearest first.</summary>
        public const int ArtPieces = 2;

        // ================= The Choir of the Spire: the listener's stay =================

        /// <summary>The listener stays four to six days, an hour or two each afternoon beside someone of the colony at their work.</summary>
        public const float StayMinDays = 4f, StayMaxDays = 6f;

        public const int OrdinaryTicks = 2500;

        public static bool OrdinaryHour(int hour) => hour >= 12 && hour <= 17;

        // ================= The Sunken Vault: the decline =================

        /// <summary>Unsupplied, the vault falls an age in about this many years (PeopleExtension.ageDeclineYears).</summary>
        public const float DeclineYears = 4f;

        /// <summary>A memory of supply for parts or food worth this much (FactionMemory, the age rule's gifts).</summary>
        public static int SupplyWeight(float worth)
        {
            float v = Safe(worth, 0f);
            return v <= 0f ? 0 : Math.Max(2, Math.Min(30, (int)Math.Round(v / 60f)));
        }

        // ================= The Sunken Vault: the barter =================

        /// <summary>What they want for their last spacer goods: components by the colony's wealth (4 to 12) and raw food (40 to 120).</summary>
        public static int BarterComponents(float wealthFactor) => (int)Math.Round(Clamp(6f * Safe(wealthFactor, 1f), 4f, 12f));

        public static int BarterFood(float wealthFactor) => Round10(Clamp(60f * Safe(wealthFactor, 1f), 40f, 120f));

        /// <summary>The colony gives it all and asks nothing: they remember it as supply, and as a friend's.</summary>
        public const int GiftGoodwill = 8, BarterGoodwill = 3;

        // ================= The Sunken Vault: a craft they lost =================

        /// <summary>The colony's teacher and their technician at the bench this long.</summary>
        public const int TeachHours = 3;

        /// <summary>A craft taught them counts as supply of this worth (what they can make again, they needn't carry).</summary>
        public const float CraftWorth = 300f;

        // ================= The Sunken Vault: the child =================

        /// <summary>A vault-born child of nine to twelve (with Biotech; else a youth of fifteen to seventeen).</summary>
        public const float ChildMin = 9f, ChildMax = 12f, YouthMin = 15f, YouthMax = 17f;

        /// <summary>Their first sky: they stand under it this long.</summary>
        public const int SkyTicks = 1500;
    }
}
