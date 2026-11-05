using System;

namespace ColonyCouncil
{
    /// <summary>What an offer for a captive is (docs/design/stage2-diplomacy.md, round a, item 3).</summary>
    public enum RansomKind
    {
        /// <summary>Their people offer a price for one of theirs in the colony's cells.</summary>
        Theirs,
        /// <summary>Their people ask the game's fee for one of the colony's they hold, by their messenger (no comms console).</summary>
        Demand,
        /// <summary>One of theirs in the colony's cells for one of the colony's they hold: both walk at once at the gate.</summary>
        Exchange
    }

    /// <summary>Where an offer for a captive stands, step by step (never a time left).</summary>
    public enum RansomStage
    {
        /// <summary>Their messenger (or their porters, once agreed by a call) is still to set out.</summary>
        Waiting,
        /// <summary>On the road to the colony's gate.</summary>
        Coming,
        /// <summary>At the gate (or on the console), waiting for the colony's answer.</summary>
        AtGate,
        /// <summary>Agreed: the price changes hands at the gate, the captive walks out.</summary>
        Paying,
        /// <summary>The colony paid their demand: its colonist walks in from the edge some hours later.</summary>
        Returning
    }

    /// <summary>How the trap's chance reads in a letter, before the choice that brings it.</summary>
    public enum TrapOdds
    {
        None,
        Small,
        Real,
        Serious
    }

    /// <summary>
    /// The arithmetic of ransom (docs/design/stage2-diplomacy.md, round a, item 3; release 2a.3), with no game types so
    /// Tools/Tests can check it. Their kin in the colony's cells: two to six days after one of a people is taken, if their
    /// people want them back (a leader, a notable or a notable's kin always; anyone else six times in ten from a people at
    /// peace, three in ten from a hostile one, read from a steady roll so it doesn't change between looks), their offer
    /// comes, half to the whole of the captive's market value, half again for a notable, twice for their leader. Asking
    /// for more is the speaker's Social against their patience (a quarter more, or they leave); refused, the offer may
    /// come once more within a season; refused three times, a grudge. Ours in their hands: the game's own demand (1.2 to
    /// 2.2 times the colonist's market value, a day to answer), by their messenger where there is no comms console
    /// (decision 4). An exchange of theirs for ours by their reckoning of who is worth more. The trap at an exchange with a
    /// hostile people: the peace talks' disaster rate, 5%, times the game's own factor for the negotiator's ability (four
    /// times with none, 0.4 at its best), a band at 0.35 of the threat points from the gate's side (decision 5).
    /// </summary>
    public static class RansomMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        /// <summary>A season, in days: an offer refused may come once more within one.</summary>
        public const float SeasonDays = 15f;

        // ---------------- The record ----------------

        /// <summary>Offers for captives (either way) a home keeps at once; more wait until one is answered.</summary>
        public const int MaxOffers = 6;

        /// <summary>The world ledger's origin for the grudge of refusals, and its key.</summary>
        public const string Origin = "ransom", GrudgeKey = "ransomRefused";

        /// <summary>How long that grudge is held.</summary>
        public const float GrudgeDays = 120f;

        // ---------------- Their offer for one of theirs ----------------

        public const float MinDelayDays = 2f, MaxDelayDays = 6f;

        /// <summary>Days from the captive taken to their offer: two to six.</summary>
        public static float DelayDays(float roll) => MinDelayDays + Clamp01(roll) * (MaxDelayDays - MinDelayDays);

        public const float WantAtPeace = 0.6f, WantHostile = 0.3f;

        /// <summary>The colony wrote to them about the captive: a second thought, a fifth likelier.</summary>
        public const float AskedBonus = 0.2f;

        /// <summary>How likely their people want someone back who is neither their leader, a notable nor a notable's kin.</summary>
        public static float WantChance(bool hostile, bool asked) => Math.Min(1f, (hostile ? WantHostile : WantAtPeace) + (asked ? AskedBonus : 0f));

        /// <summary>Their people want this captive back: their leader, a notable and a notable's kin always; anyone else by the roll.</summary>
        public static bool WantBack(bool leader, bool notable, bool notableKin, bool hostile, bool asked, float roll) =>
            leader || notable || notableKin || Clamp01(roll) < WantChance(hostile, asked);

        /// <summary>
        /// A roll that stays the same for one captive and one people (0 to 1), so whether they are wanted back is decided
        /// once without keeping it: the same two numbers give the same roll.
        /// </summary>
        public static float SteadyRoll(int captive, int people)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)captive) * 16777619u;
                h = (h ^ (uint)people) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h % 10000u) / 10000f;
            }
        }

        /// <summary>
        /// Their price: half to the whole of the captive's market value by the roll, half again for a notable, twice for their
        /// leader, in tens and 50 at least.
        /// </summary>
        public static int Price(float marketValue, bool leader, bool notable, float roll)
        {
            float v = Math.Max(50f, marketValue) * (0.5f + 0.5f * Clamp01(roll)) * (leader ? 2f : notable ? 1.5f : 1f);
            return Math.Max(50, (int)Math.Round(v / 10f) * 10);
        }

        /// <summary>A people of the old ways (neolithic) asks and pays in goods; everyone else in silver (the Empire too).</summary>
        public static bool InGoods(int techLevel) => techLevel <= 2;

        /// <summary>How many of a good make the price.</summary>
        public static int GoodsCount(int price, float each) => each <= 0f ? 0 : Math.Max(1, (int)Math.Round(price / each));

        /// <summary>Their messenger waits at the gate for the colony's answer this long (hours), as the game's own demand does.</summary>
        public const float WaitHours = 24f;

        /// <summary>A messenger who couldn't come (a hostile people with nobody to carry their word, the land never quiet) gives up after this.</summary>
        public const float GiveUpDays = 15f;

        // ---------------- Asking for more ----------------

        /// <summary>A quarter more.</summary>
        public const float More = 1.25f;

        /// <summary>The speaker's Social against their patience: one in five with none, four in five at its best.</summary>
        public static float MoreChance(int social) => Clamp(0.2f + Math.Max(0, social) * 0.03f, 0.2f, 0.8f);

        public static bool MoreAgreed(int social, float roll) => Clamp01(roll) < MoreChance(social);

        public static int Raised(int price) => (int)Math.Round(price * More / 10f) * 10;

        // ---------------- Refused ----------------

        public const int MaxRefusals = 3;

        /// <summary>Refused this many times: a grudge, and no more offers for that captive.</summary>
        public static bool Grudge(int refusals) => refusals >= MaxRefusals;

        /// <summary>Refused: the offer may come once more within a season (half to a whole one on).</summary>
        public static float AgainDays(float roll) => SeasonDays * (0.5f + 0.5f * Clamp01(roll));

        // ---------------- The game's demand, by messenger ----------------

        /// <summary>DiplomacyTuning.RansomFeeMarketValueFactorRange: the game's own fee.</summary>
        public const float FeeMin = 1.2f, FeeMax = 2.2f;

        public static int Fee(float marketValue, float roll) => (int)(Math.Max(0f, marketValue) * (FeeMin + (FeeMax - FeeMin) * Clamp01(roll)));

        /// <summary>The game's day to answer its demand.</summary>
        public const int AnswerTicks = 60000;

        /// <summary>Paid: the colonist walks in from the edge some hours later (three to six).</summary>
        public static float ReturnHours(float roll) => 3f + 3f * Clamp01(roll);

        // ---------------- An exchange ----------------

        /// <summary>
        /// How likely they agree to give one of the colony's for one of theirs: even when the two are worth the same to them,
        /// likelier the more theirs is worth beside the colony's, a notable or their kin a third likelier, their head's regard
        /// a little; never certain either way.
        /// </summary>
        public static float ExchangeChance(float ours, float theirs, bool theirsNotable, int regard)
        {
            float ratio = Math.Max(1f, theirs) / Math.Max(1f, ours);
            float c = 0.5f + (ratio - 1f) * 0.5f + (theirsNotable ? 0.3f : 0f) + regard / 200f;
            return Clamp(c, 0.1f, 0.95f);
        }

        public static bool ExchangeAccepted(float ours, float theirs, bool theirsNotable, int regard, float roll) =>
            Clamp01(roll) < ExchangeChance(ours, theirs, theirsNotable, regard);

        /// <summary>Hours until their party sets out with the colony's own after an exchange is agreed face to face.</summary>
        public static float PartyHours(float roll) => 6f + 12f * Clamp01(roll);

        // ---------------- The trap ----------------

        /// <summary>The peace talks' disaster weight (PeaceTalks.BaseWeight_Disaster).</summary>
        public const float TrapBase = 0.05f;

        /// <summary>The trap's band, as the game sizes a raid: this share of the threat points.</summary>
        public const float TrapPoints = 0.35f;

        /// <summary>
        /// The game's own factor on a parley's bad outcomes by the negotiator's ability (PeaceTalks.
        /// BadOutcomeChanceFactorByNegotiationAbility): 4 with none, 1 at 100%, 0.4 at 150% and beyond, straight between.
        /// </summary>
        public static float NegotiationFactor(float ability)
        {
            if (float.IsNaN(ability) || ability <= 0f) return 4f;
            if (ability <= 1f) return 4f + (1f - 4f) * ability;
            if (ability >= 1.5f) return 0.4f;
            return 1f + (0.4f - 1f) * ((ability - 1f) / 0.5f);
        }

        /// <summary>The trap at an exchange: only with a hostile people, 5% times the negotiator's factor (2% to 20%).</summary>
        public static float TrapChance(bool hostile, float ability) => hostile ? TrapBase * NegotiationFactor(ability) : 0f;

        public static bool Trapped(bool hostile, float ability, float roll) => hostile && Clamp01(roll) < TrapChance(hostile, ability);

        /// <summary>The trap's chance in words for the letter: none, small (under 4%), real (under 10%), serious.</summary>
        public static TrapOdds Odds(float chance) => chance <= 0f ? TrapOdds.None : chance < 0.04f ? TrapOdds.Small : chance < 0.1f ? TrapOdds.Real : TrapOdds.Serious;

        // ---------------- Letters and the facts ----------------

        /// <summary>A letter's topics: one of theirs offered for ransom ("ransom:ID"), an exchange ("exchange:OURS:THEIRS").</summary>
        public const string RansomPrefix = "ransom:", ExchangePrefix = "exchange:";

        public static string RansomTopic(int captive) => RansomPrefix + captive;

        public static string ExchangeTopic(int ours, int theirs) => ExchangePrefix + ours + ":" + theirs;

        public static bool ParseRansom(string key, out int captive)
        {
            captive = -1;
            return key != null && key.StartsWith(RansomPrefix, StringComparison.Ordinal) && int.TryParse(key.Substring(RansomPrefix.Length), out captive);
        }

        public static bool ParseExchange(string key, out int ours, out int theirs)
        {
            ours = theirs = -1;
            if (key == null || !key.StartsWith(ExchangePrefix, StringComparison.Ordinal)) return false;
            var parts = key.Substring(ExchangePrefix.Length).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out ours) && int.TryParse(parts[1], out theirs);
        }

        /// <summary>
        /// The talk's tags: one of theirs in the colony's cells (ransom:theirs), one of the colony's they hold (ransom:held),
        /// and the one spoken to was once the colony's captive (them:wascaptive, with {CAPTOR} the home that held them).
        /// </summary>
        public const string TheirsTag = "ransom:theirs", HeldTag = "ransom:held", WasCaptiveTag = "them:wascaptive", CaptorKey = "CAPTOR";

        public static readonly string[] Tags = { TheirsTag, HeldTag, WasCaptiveTag };

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
