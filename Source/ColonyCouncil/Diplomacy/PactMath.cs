using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The two kinds of pact (decision 2): each comes when the other is attacked, or trades each season.</summary>
    public enum PactKind
    {
        /// <summary>A defence pact: their band when the colony calls during a threat, the colony's fighters when their town is attacked.</summary>
        Defence,
        /// <summary>A trade pact: their trader caravan each season, and a cart of the colony's surplus to their porters.</summary>
        Trade
    }

    /// <summary>How long a truce runs: a season (15 days) or a year (60 days).</summary>
    public enum TruceTerm
    {
        Season,
        Year
    }

    /// <summary>The price of a truce, either way: none, the colony's silver carried out, their silver brought, or their captives returned.</summary>
    public enum TrucePrice
    {
        None,
        /// <summary>The colony pays: silver carried out to their envoy at the gate.</summary>
        Ours,
        /// <summary>They pay: silver their envoy brings and sets down.</summary>
        Theirs,
        /// <summary>The colony returns the captives of theirs it holds: the wardens walk them out by the game's own release.</summary>
        Captives
    }

    /// <summary>Why a truce can be offered to a people at war with the colony now.</summary>
    public enum TruceWhy
    {
        None,
        /// <summary>The colony beat a raid of theirs in the last 15 days.</summary>
        Beaten,
        /// <summary>The colony holds one of their notables or their kin.</summary>
        Holding,
        /// <summary>A friend of both called in a debt of honour toward peace with them (a Promise of origin "debts", key "peace").</summary>
        Friend
    }

    /// <summary>How a people answers the colony's call on a defence pact.</summary>
    public enum PactAnswer
    {
        /// <summary>Their band sets out and walks in some hours later.</summary>
        Come,
        /// <summary>Their riders are elsewhere this time: put off, not broken.</summary>
        PutOff,
        /// <summary>The attacker is their friend: they decline, and the pact ends with nobody at fault.</summary>
        Decline
    }

    /// <summary>
    /// The arithmetic of pacts and truces (docs/design/stage2-diplomacy.md, round a, item 2; release 2a.2), with no game
    /// types so Tools/Tests can check it. Both are the world ledger's own ties between a colony and a people, from whoever
    /// offered: a Truce (origin <see cref="TruceOrigin"/>) and a Pact (origin <see cref="PactOrigin"/>). Their ends are
    /// kept in their terms and reached in diplomacy's own hour, not by the ledger's until (which stays unset), so a truce's
    /// end can be a peace and a pact's end an offer to renew. The terms:
    /// <list type="bullet">
    /// <item>A truce: key its term ("season", "year"; "parley:season" while their envoy is still to swear it), number the
    /// game day it ends (or, still to be sworn, the day their envoy gives up coming), days the day their firebrands strike
    /// (0: none), count the price in silver, goods the price's kind, about the friend whose word helped.</item>
    /// <item>A pact: key its kind ("defence", "trade"), number the day it ends, days the day of the colony's last call (a
    /// trade pact: of the last caravan and cart), count the calls made (a trade pact: the cart's count), goods a call under
    /// way ("band:DAY" their band walking in, "arms:TOWN:DAY" the colony's fighters asked for; a trade pact: the cart's
    /// thing), about the people the call is against.</item>
    /// </list>
    /// </summary>
    public static class PactMath
    {
        public const int DayTicks = 60000;
        public const float SeasonDays = 15f, YearDays = 60f;

        public const string TruceOrigin = "truce", PactOrigin = "pact";

        // ================= Truces =================

        /// <summary>A raid of theirs beaten this recently lets the colony offer a truce (and them offer one).</summary>
        public const float BeatenDays = 15f;

        public static float TermDays(TruceTerm t) => t == TruceTerm.Year ? YearDays : SeasonDays;

        /// <summary>
        /// Why a truce can be offered to them now, the first that holds: a raid of theirs beaten lately, one of their
        /// notables or kin held, a friend of both who spoke for peace. None for a people not at war with the colony, or one
        /// that never makes peace (the game's permanent enemies).
        /// </summary>
        public static TruceWhy Why(bool hostile, bool permanentEnemy, float daysSinceBeaten, bool holdsTheirs, bool friendPromised)
        {
            if (!hostile || permanentEnemy) return TruceWhy.None;
            if (daysSinceBeaten >= 0f && daysSinceBeaten <= BeatenDays) return TruceWhy.Beaten;
            if (holdsTheirs) return TruceWhy.Holding;
            return friendPromised ? TruceWhy.Friend : TruceWhy.None;
        }

        // ---------------- Their answer ----------------

        /// <summary>How much a beaten raid, one of theirs held, a year's term and each price move their answer.</summary>
        public const int BeatenScore = 25, HoldsScore = 20, YearScore = -10, PaidScore = 15, CaptivesScore = 15, TheyPayScore = -20;

        /// <summary>
        /// How they take a truce offered: their goodwill, plus a raid of theirs just beaten, one of theirs held, a friend's
        /// word for peace (20, as if they thought that much better of the colony: FavourMath.PeaceRegard), a year being
        /// more to ask than a season, and the price.
        /// </summary>
        public static int TruceScore(int goodwill, bool beaten, bool holds, bool promised, TrucePrice price, TruceTerm term)
        {
            int s = goodwill;
            if (beaten) s += BeatenScore;
            if (holds) s += HoldsScore;
            if (promised) s += FavourMath.PeaceRegard;
            if (term == TruceTerm.Year) s += YearScore;
            switch (price)
            {
                case TrucePrice.Ours: s += PaidScore; break;
                case TrucePrice.Captives: s += CaptivesScore; break;
                case TrucePrice.Theirs: s += TheyPayScore; break;
            }
            return s;
        }

        /// <summary>The chance they swear it, from the score: a people at -100 almost never, at 0 almost always.</summary>
        public static float TruceChance(int score) => Clamp((score + 100f) / 100f, 0.05f, 0.95f);

        public static bool TruceAccepted(int score, float roll) => roll < TruceChance(score);

        /// <summary>A truce's price in silver: about half a raid's threat points, in tens (as a raiders' camp asks), 100 at least.</summary>
        public static int PriceSilver(float threatPoints) => Math.Max(100, (int)Math.Round(threatPoints * 0.5f / 10f) * 10);

        /// <summary>Goodwill when the truce is sworn: 5 for the oath, and the colony's silver counts like a gift (one for each 25, 15 at most).</summary>
        public static int SwornGoodwill(TrucePrice price, int silver) => 5 + (price == TrucePrice.Ours ? Math.Min(15, Math.Max(0, silver) / 25) : 0);

        // ---------------- Their offer ----------------

        /// <summary>After a raid of theirs beaten, they offer a truce themselves this often.</summary>
        public const float TheirOfferChance = 0.25f;

        /// <summary>One offer of theirs a season at most.</summary>
        public const float OfferGapDays = 15f;

        public static bool TheyOffer(bool hostile, bool permanentEnemy, bool truceHolds, int lastOffer, int now, float roll) =>
            hostile && !permanentEnemy && !truceHolds && (lastOffer < 0 || now - lastOffer >= OfferGapDays * DayTicks) && roll < TheirOfferChance;

        /// <summary>Their own offer after a defeat: a season, and half the time they bring a price of their own.</summary>
        public static TrucePrice TheirPrice(float roll) => roll < 0.5f ? TrucePrice.Theirs : TrucePrice.None;

        // ---------------- The envoy at the gate ----------------

        /// <summary>Their envoy comes this many hours after their word that they will (a call; a letter adds its walk back).</summary>
        public const float EnvoyHoursMin = 6f, EnvoyHoursMax = 18f;

        /// <summary>The envoy who can't come within this many days after they were due never comes: the offer lapses.</summary>
        public const float EnvoyGiveUpDays = 3f;

        /// <summary>The envoy waits at the gate this long for the colony's speaker, then goes home.</summary>
        public const float ParleyWaitHours = 20f;

        // ---------------- Their firebrands ----------------

        /// <summary>
        /// Once per truce, the chance a band of theirs raids anyway: the peace talks' backfire (one in ten), 15% for a
        /// people whose natural goodwill with the colony is below -50.
        /// </summary>
        public static float FirebrandChance(int naturalGoodwill) => naturalGoodwill < -50 ? 0.15f : 0.10f;

        /// <summary>The firebrands' raid comes at this share of the game's threat points.</summary>
        public const float FirebrandPoints = 0.6f;

        /// <summary>The day their firebrands strike, somewhere after the first day and before the last.</summary>
        public static float FirebrandDay(float swornDay, float termDays, float roll) => swornDay + 1f + Clamp(roll, 0f, 1f) * Math.Max(0f, termDays - 2f);

        // ---------------- Its end ----------------

        /// <summary>At its end the truce is a peace when they are no longer at war with the colony, or their goodwill has come to 0.</summary>
        public static bool Peace(bool hostile, int goodwill) => !hostile || goodwill >= 0;

        /// <summary>How far their goodwill is from peace (0 at peace).</summary>
        public static int FromPeace(int goodwill) => Math.Max(0, -goodwill);

        /// <summary>A truce whose end is this near is "due" in the talk (tie:truce:due).</summary>
        public const float DueDays = 3f;

        public static bool Due(float endDay, float nowDay) => endDay >= nowDay && endDay - nowDay <= DueDays;

        // ================= Pacts =================

        /// <summary>A people at peace with this much goodwill or more can be asked for a pact.</summary>
        public const int PactGoodwill = 20;

        /// <summary>A pact can be proposed: not at war, not the Empire (decision 9), goodwill 20 or more.</summary>
        public static bool MayPropose(int goodwill, bool hostile, bool empire) => !hostile && !empire && goodwill >= PactGoodwill;

        /// <summary>A common enemy makes them likelier to swear; a trade pact asks less of them than a defence pact.</summary>
        public const int CommonEnemyScore = 15, TradeScore = 5;

        /// <summary>How they take a pact proposed: the mean of their goodwill and their head's regard, an enemy in common, the kind.</summary>
        public static int PactScore(int goodwill, int headRegard, bool commonEnemy, PactKind kind) =>
            (goodwill + headRegard) / 2 + (commonEnemy ? CommonEnemyScore : 0) + (kind == PactKind.Trade ? TradeScore : 0);

        /// <summary>The chance they swear a pact: rare near 20 goodwill and a cool head, near certain among close friends.</summary>
        public static float PactChance(int score) => Clamp((score - 10f) / 60f, 0.05f, 0.95f);

        public static bool PactAccepted(int score, float roll) => roll < PactChance(score);

        // ---------------- Their offer ----------------

        /// <summary>They offer a pact themselves only when their head thinks this well of the colony, and both have an enemy in common.</summary>
        public const int TheirOfferRegard = 50;

        /// <summary>Once a year at most from each people.</summary>
        public const float TheirOfferGapDays = 60f;

        /// <summary>When it may, a daily look offers it about once in 20 days.</summary>
        public const float TheirOfferDaily = 1f / 20f;

        public static bool MayOfferPact(int headRegard, int goodwill, bool commonEnemy, bool hostile, bool empire, int lastOffer, int now) =>
            !hostile && !empire && commonEnemy && headRegard >= TheirOfferRegard && goodwill >= PactGoodwill
            && (lastOffer < 0 || now - lastOffer >= TheirOfferGapDays * DayTicks);

        /// <summary>A pact runs a year, and can be renewed.</summary>
        public static float PactDays => YearDays;

        /// <summary>At its end they offer to renew it while they think well enough of the colony.</summary>
        public static bool Renews(int goodwill, int headRegard, bool hostile) => !hostile && goodwill >= PactGoodwill && headRegard >= 0;

        // ---------------- Calling a defence pact in ----------------

        /// <summary>At most once a season per pact.</summary>
        public const float CallGapDays = 15f;

        /// <summary>Nine times in ten they come.</summary>
        public const float ComeChance = 0.9f;

        public static bool MayCall(float lastCallDay, float nowDay) => lastCallDay <= 0f || nowDay - lastCallDay >= CallGapDays;

        /// <summary>Their answer: they decline when the attacker is their friend; else they come nine times in ten, and are elsewhere once.</summary>
        public static PactAnswer Answer(bool attackerTheirFriend, float roll) =>
            attackerTheirFriend ? PactAnswer.Decline : roll < ComeChance ? PactAnswer.Come : PactAnswer.PutOff;

        /// <summary>Hours until their band walks in: three, and more the further their town is (twelve at most).</summary>
        public static float BandHours(float tiles) => Clamp(3f + Math.Max(0f, tiles) * 0.75f, 3f, 12f);

        // ---------------- A trade pact ----------------

        /// <summary>Their caravan and the colony's cart, each season.</summary>
        public static bool SeasonDue(float lastDay, float nowDay) => lastDay <= 0f || nowDay - lastDay >= SeasonDays;

        /// <summary>The cart is about this much silver's worth of one good from the colony's surplus.</summary>
        public const float CartValue = 250f;

        /// <summary>What their porters pay for the cart: four fifths of its worth, a fair price between partners.</summary>
        public const float CartPayShare = 0.8f;

        /// <summary>How many make up the cart: about 250 silver's worth, a third of the stores at most, one at least.</summary>
        public static int CartCount(float unitValue, int inStores)
        {
            if (unitValue <= 0f || inStores <= 0) return 0;
            int want = Math.Max(1, (int)Math.Round(CartValue / unitValue));
            return Math.Max(1, Math.Min(want, inStores / 3));
        }

        public static int CartPay(float unitValue, int count) => Math.Max(1, (int)Math.Round(Math.Max(0f, unitValue) * Math.Max(0, count) * CartPayShare));

        // ---------------- Their call to arms ----------------

        /// <summary>A war world event this near their town (in tiles) is an attack on it.</summary>
        public const float WarTiles = 10f;

        /// <summary>A day with their town under attack, the chance their letter asking for fighters comes (once per pact while it lasts).</summary>
        public const float ArmsDaily = 1f / 3f;

        /// <summary>Two colonists or more set out within five days for their town.</summary>
        public const int MinFighters = 2;

        public const float SetOutDays = 5f;

        /// <summary>The fight there: their attackers' band at half the home's threat points, a few of theirs beside the colony's.</summary>
        public const float FightPoints = 0.5f, AlliesPoints = 0.25f;

        /// <summary>A caravan answers the call: grown colonists enough, bound for their town or there.</summary>
        public static bool Answers(int grownColonists, bool boundOrThere) => boundOrThere && grownColonists >= MinFighters;

        // ================= Breaches =================

        /// <summary>The colony's deeds against a people (FactionMemory.Kinds) that break a truce or a pact with them.</summary>
        public static readonly string[] OurBreaches =
        {
            "attacked", "caravanRaided", "townAttacked", "townDestroyed", "robbed", "killed", "captured", "sold", "fell", "slain", "campHarmed", "postRazed"
        };

        /// <summary>Their deeds against the colony that break a truce they hold: a raid, a colonist killed.</summary>
        public static readonly string[] TheirBreaches = { "raided", "killedOurs" };

        /// <summary>
        /// Those that break a pact with a people at peace: the same less a blow struck (a stray shot in a shared fight is the
        /// game's own matter of goodwill) and their fighters fallen in a raid on the colony (a partner never raids it).
        /// </summary>
        public static readonly string[] PactBreaches = { "caravanRaided", "townAttacked", "townDestroyed", "robbed", "killed", "captured", "sold", "campHarmed", "postRazed" };

        public static bool OurBreach(string deed) => deed != null && Array.IndexOf(OurBreaches, deed) >= 0;

        public static bool PactBreach(string deed) => deed != null && Array.IndexOf(PactBreaches, deed) >= 0;

        /// <summary>
        /// A deed of the colony's breaks its pact with them now: one of <see cref="PactBreaches"/>, but not one of theirs killed
        /// while their war band is on the colony's land (a stray shot of the fight they share, as a blow struck is).
        /// </summary>
        public static bool BreaksPact(string deed, bool theirBandHere) => PactBreach(deed) && !(deed == "killed" && theirBandHere);

        public static bool TheirBreach(string deed) => deed != null && Array.IndexOf(TheirBreaches, deed) >= 0;

        // ================= The terms =================

        public const string Defence = "defence", Trade = "trade", Season = "season", Year = "year", Parley = "parley:", Band = "band:", Arms = "arms:";

        public static string KindKey(PactKind k) => k == PactKind.Trade ? Trade : Defence;

        public static bool ParseKind(string key, out PactKind k)
        {
            k = key == Trade ? PactKind.Trade : PactKind.Defence;
            return key == Trade || key == Defence;
        }

        public static string TermKey(TruceTerm t, bool pending) => (pending ? Parley : "") + (t == TruceTerm.Year ? Year : Season);

        public static bool ParseTerm(string key, out TruceTerm t, out bool pending)
        {
            t = TruceTerm.Season;
            pending = false;
            if (key == null) return false;
            if (key.StartsWith(Parley, StringComparison.Ordinal))
            {
                pending = true;
                key = key.Substring(Parley.Length);
            }
            if (key == Year) t = TruceTerm.Year;
            return key == Year || key == Season;
        }

        public static string PriceKey(TrucePrice p) => p.ToString();

        public static TrucePrice ParsePrice(string goods) => goods != null && Enum.TryParse(goods, out TrucePrice p) && Enum.IsDefined(typeof(TrucePrice), p) ? p : TrucePrice.None;

        /// <summary>Their band walks in on this day (a call of the colony's under way).</summary>
        public static string BandCode(float day) => Band + day.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The colony's fighters asked for at their town (its id), to set out by this day.</summary>
        public static string ArmsCode(int townId, float dueDay) => Arms + townId + ":" + dueDay.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public static bool ParseBand(string goods, out float day)
        {
            day = 0f;
            return goods != null && goods.StartsWith(Band, StringComparison.Ordinal)
                   && float.TryParse(goods.Substring(Band.Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out day);
        }

        public static bool ParseArms(string goods, out int townId, out float dueDay)
        {
            townId = -1;
            dueDay = 0f;
            if (goods == null || !goods.StartsWith(Arms, StringComparison.Ordinal)) return false;
            var parts = goods.Substring(Arms.Length).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out townId)
                   && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dueDay);
        }

        // ================= The facts =================

        public const string PactTag = "tie:pact", TruceTag = "tie:truce";

        /// <summary>
        /// A pact's or a truce's tags for the talk: tie:pact or tie:truce (more current when new), :ours or :theirs (who
        /// offered it), :due (a pact: a call under way either way; a truce: its end near), and a pact's kind
        /// (tie:pact:defence, tie:pact:trade).
        /// </summary>
        public static List<(string tag, float heat)> Tags(bool pact, bool oursOffered, bool due, float days, string kind = null)
        {
            string t = pact ? PactTag : TruceTag;
            var list = new List<(string, float)>
            {
                (t, days <= 10f ? Math.Max(1f, 2f * (1f - days / 10f)) : 1f),
                (t + (oursOffered ? ":ours" : ":theirs"), 1f)
            };
            if (due) list.Add((t + ":due", 2f));
            if (pact && (kind == Defence || kind == Trade)) list.Add((t + ":" + kind, 1f));
            return list;
        }

        /// <summary>The placeholder naming the other side: {PACTWITH}, {TRUCEWITH}.</summary>
        public static string WithKey(bool pact) => pact ? "PACTWITH" : "TRUCEWITH";

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
