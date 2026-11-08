using System;

namespace ColonyCouncil
{
    /// <summary>How a band's attack on a camp stands (Camps/CampRaid.cs).</summary>
    public enum CampRaidEnd
    {
        /// <summary>Still going: neither side beaten.</summary>
        Ongoing,
        /// <summary>The band fled or fell: the campers hold their camp.</summary>
        CampersWon,
        /// <summary>The campers fled or fell: the band has the camp.</summary>
        CampersLost,
        /// <summary>Hours of it and neither side gave way: the band gives up and goes.</summary>
        Held
    }

    /// <summary>What the colony answered when the band was seen coming.</summary>
    public enum CampRaidAnswer
    {
        /// <summary>Not answered yet.</summary>
        None,
        /// <summary>Send our fighters to stand with them (the player drafts them), and word that help is coming.</summary>
        Help,
        /// <summary>Send word only: they stand or go, in time.</summary>
        Warn,
        /// <summary>Stay out of it.</summary>
        Out
    }

    /// <summary>A people that might send a band against a camp, as the rules see it.</summary>
    public struct AttackerFacts
    {
        /// <summary>At war with the camp's people.</summary>
        public bool hostileToCamp;
        public bool isPlayer;
        /// <summary>The colony's ally (World/Allies.cs): it wouldn't come against a camp on the colony's own land.</summary>
        public bool allyOfColony;
        public bool humanlike;
        /// <summary>It has fighters (a combat group of its own).</summary>
        public bool hasFighters;
        /// <summary>Gone from the world, hidden, or passing through (a quest's).</summary>
        public bool gone;
        /// <summary>At war with the colony too: they come for the camp first, and are the colony's enemies on the map.</summary>
        public bool hostileToColony;
        /// <summary>The storyteller sends threats at all, and the last big one was more than two days ago.</summary>
        public bool fightsAllowed;
        /// <summary>A truce with the colony keeps them off its land (Diplomacy/Truces.cs).</summary>
        public bool truce;
        /// <summary>They held this camp's site before, and come to claim it back.</summary>
        public bool heldSiteBefore;
        /// <summary>How well their age fits the colony's (Arrivals.TechWeight).</summary>
        public float techWeight;
    }

    /// <summary>
    /// A band sent against a camp on the colony's land (the player's request, 8 November 2026: "perhaps camps can also get
    /// attacked by enemy factions"): how many camps it happens to, by the setting's level and the stay; who may come
    /// against whom; how strong the band is; whether the campers stand or go; how it ends; and what the colony is thanked
    /// with. Pure (no game types), so Tools/Tests checks it.
    /// </summary>
    public static class CampRaidMath
    {
        public const int HourTicks = 2500;
        public const int DayTicks = 60000;

        // ---------------- How often ----------------

        /// <summary>The share of camps a band comes against during their stay: about one camp in nine at Usual, a little more on a crowded land.</summary>
        public static float Share(CampCrowd c)
        {
            switch (c)
            {
                case CampCrowd.Rare: return 0.06f;
                case CampCrowd.Many: return 0.13f;
                case CampCrowd.Crowded: return 0.16f;
                default: return 0.11f;
            }
        }

        /// <summary>A camp on a contested site (an earlier holder at war with its people, or someone driven off it): this much likelier.</summary>
        public const float ContestedFactor = 1.6f;

        /// <summary>Never more than this share, however contested.</summary>
        public const float MostShare = 0.3f;

        /// <summary>A stay shorter than this sees no attack: never on a camp's first day, never in its last hours.</summary>
        public const float LeastStayDays = 1.5f;

        /// <summary>The chance, rolled once as a camp settles, that a band comes against it in its stay.</summary>
        public static float FateChance(CampCrowd c, float stayDays, bool contested) =>
            stayDays < LeastStayDays ? 0f : Math.Min(MostShare, Share(c) * (contested ? ContestedFactor : 1f));

        /// <summary>
        /// When the band comes: somewhere between the end of the camp's first day and six hours before they would leave
        /// (<paramref name="roll"/> 0 to 1 picks the moment); -1 when the stay leaves no such time.
        /// </summary>
        public static int AttackTick(int arrivedTick, int untilTick, double roll)
        {
            long from = (long)arrivedTick + DayTicks, to = (long)untilTick - 6 * HourTicks;
            if (arrivedTick < 0 || to <= from) return -1;
            return (int)(from + (long)((to - from) * Math.Max(0.0, Math.Min(1.0, roll))));
        }

        /// <summary>How long a fated hour may wait on a reason to put it off (danger on the map, the campers packing) before it is let go.</summary>
        public const int PutOffHours = 12;

        // ---------------- Who ----------------

        /// <summary>
        /// A people may send a band against a camp: at war with the camp's people, a people of fighters still in the world,
        /// not the colony nor its ally; one at war with the colony too only when the storyteller sends fights and no truce
        /// keeps them off.
        /// </summary>
        public static bool MayAttack(AttackerFacts f) =>
            f.hostileToCamp && !f.isPlayer && !f.allyOfColony && f.humanlike && f.hasFighters && !f.gone && (!f.hostileToColony || (f.fightsAllowed && !f.truce));

        /// <summary>Among those that may: the people that held the site before likeliest, one at war with the colony too a little less likely.</summary>
        public static float Weight(AttackerFacts f) => (f.heldSiteBefore ? 3f : 1f) * (f.hostileToColony ? 0.7f : 1f) * Math.Max(0.05f, f.techWeight);

        // ---------------- How strong ----------------

        public const float LeastPoints = 80f;
        public const float MostPoints = 900f;

        /// <summary>
        /// The band's strength in the game's threat points: about the camp's own fighting strength, a little under to a
        /// little over (<paramref name="roll"/> 0 to 1), so the fight could go either way; within the game's least and most,
        /// and a band at war with the colony too never more than six tenths of the colony's own threat points.
        /// </summary>
        public static float Points(float campStrength, float colonyThreat, bool hostileToColony, double roll)
        {
            float p = Math.Max(1f, campStrength) * (float)(0.75 + 0.6 * Math.Max(0.0, Math.Min(1.0, roll)));
            float most = hostileToColony ? Math.Min(MostPoints, Math.Max(LeastPoints, colonyThreat * 0.6f)) : MostPoints;
            return Math.Max(LeastPoints, Math.Min(most, p));
        }

        /// <summary>
        /// Whether the campers stand and fight or go: a camp of fighters always stands; another stands when its armed
        /// people are strong enough against the band, much more readily with the colony's fighters coming.
        /// </summary>
        public static bool Stand(float campStrength, float bandStrength, bool help, bool fighters) =>
            fighters || campStrength >= bandStrength * (help ? 0.45f : 0.75f);

        // ---------------- How it ends ----------------

        /// <summary>Hours of fighting before a band that hasn't won gives up.</summary>
        public const float GiveUpHours = 10f;

        /// <summary>
        /// How it stands: the campers fled or all fell, lost; the band fled or all fell, the campers won; neither after
        /// <see cref="GiveUpHours"/>, held (the band goes); else still going.
        /// </summary>
        public static CampRaidEnd Outcome(int bandUp, int bandCame, bool bandFled, int campUp, int campCame, bool campFled, float hours)
        {
            if (campFled || (campCame > 0 && campUp <= 0)) return CampRaidEnd.CampersLost;
            if (bandFled || (bandCame > 0 && bandUp <= 0)) return CampRaidEnd.CampersWon;
            if (hours >= GiveUpHours) return CampRaidEnd.Held;
            return CampRaidEnd.Ongoing;
        }

        /// <summary>The campers break and run: half of those who stood are down and the band still on its feet outnumbers those left.</summary>
        public static bool CampersBreak(int campUp, int campStood, int bandUp) => campStood > 0 && campUp * 2 <= campStood && bandUp > campUp;

        /// <summary>The band breaks and runs: half of it down (the game's own rule for a raid, Trigger_FractionPawnsLost).</summary>
        public static bool BandBreaks(int bandUp, int bandCame) => bandCame > 0 && bandUp * 2 <= bandCame;

        // ---------------- Thanks ----------------

        /// <summary>The camp's people's goodwill for what the colony did: fought beside them, more when the camp held; word sent in time, a little.</summary>
        public static int Goodwill(bool fought, bool warned, CampRaidEnd end)
        {
            if (fought) return end == CampRaidEnd.CampersLost ? 5 : 10;
            return warned ? 3 : 0;
        }

        /// <summary>The gift they bring the colony for fighting beside them (in silver's worth): more for every one of the band put down, only when they hold their camp.</summary>
        public static int GiftWorth(bool fought, int bandDown, CampRaidEnd end) =>
            !fought || end == CampRaidEnd.CampersLost ? 0 : 80 + 20 * Math.Min(Math.Max(0, bandDown), 6);

        /// <summary>The ask's default when nobody answers in time: stay out of it.</summary>
        public static CampRaidAnswer Default => CampRaidAnswer.Out;

        /// <summary>Word goes to the camp (by a runner or over the radio) for these answers.</summary>
        public static bool SendsWord(CampRaidAnswer a) => a == CampRaidAnswer.Help || a == CampRaidAnswer.Warn;
    }
}
