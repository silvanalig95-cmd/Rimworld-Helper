using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2a.3, ransom and the envoy (docs/design/stage2-diplomacy.md, items 3 and 4;
/// Diplomacy/RansomMath.cs, EnvoyMath.cs): who is wanted back and the steady roll, their price, the haggle, refusals and
/// the grudge, the game's demand by messenger at its own fee, an exchange's answer, the trap at the peace talks' rate by
/// the negotiator, the letters' topics; their envoy's offer and the gaps, the stay's score night by night and day by day,
/// its mood, letters home and the report, the colony's own envoy abroad; the facts' tags; and the mod's side.
/// </summary>
public static class RansomTests
{
    private const int D = RansomMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "ransom and the envoy: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Who they want back ----------------

        C(RansomMath.DelayDays(0f) == 2f && RansomMath.DelayDays(1f) == 6f && RansomMath.DelayDays(0.5f) == 4f, "their offer two to six days after the captive is taken");
        C(RansomMath.WantBack(true, false, false, true, false, 0.99f) && RansomMath.WantBack(false, true, false, true, false, 0.99f)
          && RansomMath.WantBack(false, false, true, true, false, 0.99f), "their leader, a notable and a notable's kin always");
        C(RansomMath.WantBack(false, false, false, false, false, 0.59f) && !RansomMath.WantBack(false, false, false, false, false, 0.61f), "anyone else six times in ten from a people at peace");
        C(RansomMath.WantBack(false, false, false, true, false, 0.29f) && !RansomMath.WantBack(false, false, false, true, false, 0.31f), "three in ten from a hostile one");
        C(RansomMath.WantBack(false, false, false, true, true, 0.49f) && !RansomMath.WantBack(false, false, false, true, true, 0.51f) && Math.Abs(RansomMath.WantChance(false, true) - 0.8f) < 0.0001f,
            "asked by the colony's letter, a fifth likelier");
        C(RansomMath.SteadyRoll(1234, 7) == RansomMath.SteadyRoll(1234, 7), "the steady roll is the same for the same captive and people");
        var rolls = Enumerable.Range(0, 4000).Select(i => RansomMath.SteadyRoll(100000 + i * 37, 5 + i % 9)).ToList();
        C(rolls.All(r => r >= 0f && r < 1f), "the steady roll is between 0 and 1");
        float wantedShare = rolls.Count(r => r < RansomMath.WantAtPeace) / (float)rolls.Count;
        C(wantedShare > 0.55f && wantedShare < 0.65f, $"over many captives about six in ten are wanted back by a people at peace ({wantedShare:0.00})");
        float hostileShare = rolls.Count(r => r < RansomMath.WantHostile) / (float)rolls.Count;
        C(hostileShare > 0.25f && hostileShare < 0.35f, $"and about three in ten by a hostile one ({hostileShare:0.00})");

        // ---------------- Their price ----------------

        C(RansomMath.Price(1000f, false, false, 0f) == 500 && RansomMath.Price(1000f, false, false, 1f) == 1000, "half to the whole of the captive's market value");
        C(RansomMath.Price(1000f, false, true, 1f) == 1500 && RansomMath.Price(1000f, true, false, 1f) == 2000 && RansomMath.Price(1000f, true, true, 0f) == 1000,
            "half again for a notable, twice for their leader");
        C(RansomMath.Price(10f, false, false, 0f) == 50 && RansomMath.Price(777f, false, false, 0.3f) % 10 == 0, "in tens, 50 at least");
        C(RansomMath.InGoods(2) && !RansomMath.InGoods(3) && !RansomMath.InGoods(4) && !RansomMath.InGoods(5), "a tribe pays in goods, everyone else in silver");
        C(RansomMath.GoodsCount(300, 15f) == 20 && RansomMath.GoodsCount(10, 100f) == 1 && RansomMath.GoodsCount(10, 0f) == 0, "the goods that make the price");

        // ---------------- Asking for more ----------------

        C(Math.Abs(RansomMath.MoreChance(0) - 0.2f) < 0.0001f && Math.Abs(RansomMath.MoreChance(20) - 0.8f) < 0.0001f && Math.Abs(RansomMath.MoreChance(10) - 0.5f) < 0.001f, "the speaker's Social decides: one in five to four in five");
        bool climbing = true;
        for (int s = 0; s < 20; s++)
            if (RansomMath.MoreChance(s + 1) < RansomMath.MoreChance(s)) climbing = false;
        C(climbing, "better with people, likelier");
        C(RansomMath.MoreAgreed(10, 0.49f) && !RansomMath.MoreAgreed(10, 0.51f), "the roll against the chance");
        C(RansomMath.Raised(400) == 500 && RansomMath.Raised(1000) == 1250, "a quarter more, in tens");

        // ---------------- Refused ----------------

        C(!RansomMath.Grudge(2) && RansomMath.Grudge(3), "refused three times, a grudge");
        C(RansomMath.AgainDays(0f) == 7.5f && RansomMath.AgainDays(1f) == RansomMath.SeasonDays, "refused, it may come once more within a season");

        // ---------------- The game's demand ----------------

        C(RansomMath.FeeMin == 1.2f && RansomMath.FeeMax == 2.2f, "the game's own fee range (DiplomacyTuning.RansomFeeMarketValueFactorRange)");
        C(RansomMath.Fee(1000f, 0f) == 1200 && RansomMath.Fee(1000f, 1f) == 2200 && RansomMath.Fee(1000f, 0.5f) == 1700, "1.2 to 2.2 times the colonist's market value");
        C(RansomMath.AnswerTicks == 60000, "the game's day to answer");
        C(RansomMath.ReturnHours(0f) == 3f && RansomMath.ReturnHours(1f) == 6f, "paid, they walk in from the edge three to six hours later");

        // ---------------- An exchange ----------------

        C(Math.Abs(RansomMath.ExchangeChance(1000f, 1000f, false, 0) - 0.5f) < 0.001f, "even worth, even odds");
        C(RansomMath.ExchangeChance(1000f, 1500f, false, 0) > RansomMath.ExchangeChance(1000f, 1000f, false, 0)
          && RansomMath.ExchangeChance(1000f, 1000f, true, 0) > RansomMath.ExchangeChance(1000f, 1000f, false, 0)
          && RansomMath.ExchangeChance(1000f, 1000f, false, 60) > RansomMath.ExchangeChance(1000f, 1000f, false, 0),
            "likelier when theirs is worth more, a notable, or their head thinks well of the colony");
        C(RansomMath.ExchangeChance(5000f, 10f, false, -100) == 0.1f && RansomMath.ExchangeChance(10f, 5000f, true, 100) == 0.95f, "never certain either way");
        C(RansomMath.PartyHours(0f) == 6f && RansomMath.PartyHours(1f) == 18f, "their party sets out six to eighteen hours after it is agreed");

        // ---------------- The trap ----------------

        C(RansomMath.NegotiationFactor(0f) == 4f && RansomMath.NegotiationFactor(1f) == 1f && Math.Abs(RansomMath.NegotiationFactor(1.5f) - 0.4f) < 0.0001f
          && Math.Abs(RansomMath.NegotiationFactor(2f) - 0.4f) < 0.0001f && Math.Abs(RansomMath.NegotiationFactor(0.5f) - 2.5f) < 0.0001f
          && Math.Abs(RansomMath.NegotiationFactor(1.25f) - 0.7f) < 0.0001f, "the game's own curve for the negotiator: 4 with none, 1 at 100%, 0.4 at its best");
        C(RansomMath.TrapChance(false, 0f) == 0f && RansomMath.TrapChance(false, 1f) == 0f, "never with a people at peace");
        C(Math.Abs(RansomMath.TrapChance(true, 1f) - 0.05f) < 0.0001f && Math.Abs(RansomMath.TrapChance(true, 0f) - 0.2f) < 0.0001f
          && Math.Abs(RansomMath.TrapChance(true, 1.5f) - 0.02f) < 0.0001f, "the peace talks' disaster rate: 5%, 20% with no negotiator, 2% at the best");
        C(RansomMath.TrapPoints == 0.35f, "the trap's band at 0.35 of the threat points (decision 5)");
        var rng = new Random(11);
        int sprung = 0, tries = 20000;
        for (int i = 0; i < tries; i++)
            if (RansomMath.Trapped(true, 1f, (float)rng.NextDouble()))
                sprung++;
        C(Math.Abs(sprung / (float)tries - 0.05f) < 0.01f, $"over many exchanges with a hostile people and a fair negotiator, about one in twenty ({sprung / (float)tries:0.000})");
        C(RansomMath.Odds(0f) == TrapOdds.None && RansomMath.Odds(0.02f) == TrapOdds.Small && RansomMath.Odds(0.05f) == TrapOdds.Real && RansomMath.Odds(0.2f) == TrapOdds.Serious,
            "the chance named in words before the choice");

        // ---------------- Topics ----------------

        C(RansomMath.ParseRansom(RansomMath.RansomTopic(42), out int cap) && cap == 42 && !RansomMath.ParseRansom("exchange:1:2", out _), "a ransom topic read back");
        C(RansomMath.ParseExchange(RansomMath.ExchangeTopic(7, 9), out int o, out int t) && o == 7 && t == 9 && !RansomMath.ParseExchange("exchange:7", out _, out _),
            "an exchange topic read back");

        // ---------------- Their envoy's offer ----------------

        C(EnvoyMath.MayOffer(false, 30, true, false, -1, -1, 0) && !EnvoyMath.MayOffer(false, 29, true, false, -1, -1, 0) && !EnvoyMath.MayOffer(true, 80, true, false, -1, -1, 0),
            "a people at peace with goodwill 30 or more");
        C(!EnvoyMath.MayOffer(false, 50, false, false, -1, -1, 0) && !EnvoyMath.MayOffer(false, 50, true, true, -1, -1, 0), "whose envoy the colony knows, one at a time per home");
        C(!EnvoyMath.MayOffer(false, 50, true, false, 0, -1, 59 * D) && EnvoyMath.MayOffer(false, 50, true, false, 0, -1, 60 * D), "at most once a year per people");
        C(!EnvoyMath.MayOffer(false, 50, true, false, -1, 0, 14 * D) && EnvoyMath.MayOffer(false, 50, true, false, -1, 0, 15 * D), "a season apart at a home");
        C(EnvoyMath.OffersToday(0.03f) && !EnvoyMath.OffersToday(0.04f), "one day in thirty for a people that may");
        C(EnvoyMath.SendsWhenAsked(false, 30, 0, 0.39f) && !EnvoyMath.SendsWhenAsked(false, 30, 0, 0.41f) && !EnvoyMath.SendsWhenAsked(true, 30, 100, 0f)
          && EnvoyMath.SendChance(-100) == 0.1f && EnvoyMath.SendChance(100) == 0.95f, "asked by letter: by their head's regard, never certain");
        C(EnvoyMath.ComeDays(0f) == 1f && EnvoyMath.ComeDays(1f) == 2f, "they come one to two days after it is agreed");

        // ---------------- The stay ----------------

        C(EnvoyMath.Day(true, false, false, false) == 1f && EnvoyMath.Day(false, true, false, false) == -2f && EnvoyMath.Day(false, false, false, false) == 0f,
            "fed at the colony's table counts, only their packs counts against");
        C(EnvoyMath.Day(true, false, true, true) == 1.5f && EnvoyMath.Day(true, false, true, false) == 0.5f, "the Empire's envoy minds fine meals");
        C(EnvoyMath.Night(EnvoyNight.Own, false, false) == 1.5f && EnvoyMath.Night(EnvoyNight.Spare, false, false) == 0.5f && EnvoyMath.Night(EnvoyNight.Floor, false, true) == -2f,
            "a bed of their own, a spare one, the floor");
        C(EnvoyMath.Night(EnvoyNight.Own, true, true) == 1.5f && EnvoyMath.Night(EnvoyNight.Own, true, false) == 0.5f && EnvoyMath.Night(EnvoyNight.Spare, true, false) == -0.5f,
            "the Empire's envoy expects a room of their own");
        C(EnvoyMath.Talks(40f) == 1.5f && EnvoyMath.Talks(-60f) == -2f && EnvoyMath.Talks(10f) == 0.5f, "their talks with the colonists, a little either way");
        C(EnvoyMath.Hurt(true) == -3f && EnvoyMath.Hurt(false) == -8f, "hurt while sheltered costs a little, in the open a lot");
        float good = 0f, poor = 0f, mixed = 0f;
        for (int d = 0; d < 15; d++)
        {
            good += EnvoyMath.Day(true, false, false, false) + EnvoyMath.Night(EnvoyNight.Own, false, false);
            poor += EnvoyMath.Day(false, true, false, false) + EnvoyMath.Night(EnvoyNight.Floor, false, false);
            mixed += EnvoyMath.Day(false, true, false, false) + EnvoyMath.Night(EnvoyNight.Own, false, false);
        }
        C(EnvoyMath.Mood(good, 15f) == EnvoyMood.Content && EnvoyMath.Mood(poor, 15f) == EnvoyMood.Offended && EnvoyMath.Mood(mixed, 15f) == EnvoyMood.Cool,
            "fed and a bed of their own: content; hungry on the floor: offended; in between: cool");
        C(EnvoyMath.Mood(0f, 0f) == EnvoyMood.Cool && EnvoyMath.Mood(1.5f, 0.2f) == EnvoyMood.Content, "the first day counts as one");
        C(EnvoyMath.LeavesFavour(good, 15f) && !EnvoyMath.LeavesFavour(15f * 1.5f, 15f) && EnvoyMath.DeedWeight(good, 15f) == 12 && EnvoyMath.DeedWeight(10f, 15f) == 8,
            "a very good stay leaves a debt of honour (the deed big enough), a good one only their regard");
        C(EnvoyMath.EndRegard(EnvoyMood.Content) == 12 && EnvoyMath.EndRegard(EnvoyMood.Cool) == 3 && EnvoyMath.EndRegard(EnvoyMood.Offended) == -10, "their report moves their head's regard");
        C(EnvoyMath.LetterRegard(EnvoyMood.Content) == 2 && EnvoyMath.LetterRegard(EnvoyMood.Cool) == 0 && EnvoyMath.LetterRegard(EnvoyMood.Offended) == -2, "and each letter home a little");
        C(EnvoyMath.DiedRegard(true) == -5 && EnvoyMath.DiedRegard(false) == -20, "an envoy who died: a little while sheltered, a lot in the open");
        C(!EnvoyMath.LetterDue(4.9f, 0) && EnvoyMath.LetterDue(5f, 0) && !EnvoyMath.LetterDue(9f, 1) && EnvoyMath.LetterDue(10f, 1) && !EnvoyMath.LetterDue(15f, 2),
            "a letter home every five days of the stay, none at its end");
        C(EnvoyMath.StayDays == 15f, "a season");

        // ---------------- The colony's own envoy ----------------

        C(EnvoyMath.CanGo(6, true, true) && !EnvoyMath.CanGo(5, true, true) && !EnvoyMath.CanGo(10, false, true) && !EnvoyMath.CanGo(10, true, false),
            "a grown colonist good with people (Social 6 or more) who can talk");
        C(EnvoyMath.AbroadRegard(0) == 1 && EnvoyMath.AbroadRegard(8) == 3 && EnvoyMath.AbroadRegard(20) == 6, "each letter raises the town's notables' regard by their Social");
        C(EnvoyMath.SocialXp == 6000f, "home again with the peace talks' practice in Social");
        C(EnvoyMath.PartingGift(30) && !EnvoyMath.PartingGift(29), "a parting gift from a head who thinks well of the colony");
        C(EnvoyMath.RoadDays(1f) == 2f && EnvoyMath.RoadDays(4f) == 6f, "food for the road home");

        // ---------------- The facts ----------------

        C(RansomMath.Tags.SequenceEqual(new[] { "ransom:theirs", "ransom:held", "them:wascaptive" }) && EnvoyMath.Tags.SequenceEqual(new[] { "envoy:here", "them:envoy", "envoy:ours" }),
            "the talk's tags");
        C(RansomMath.CaptorKey == "CAPTOR" && EnvoyMath.EnvoyKey == "ENVOY" && EnvoyMath.OurEnvoyKey == "OURENVOY", "{CAPTOR}, {ENVOY}, {OURENVOY}");

        n += ModSide(root, src, Code, C);
        return n;
    }

    /// <summary>The mod's side: filled in as the game side is built.</summary>
    private static int ModSide(string root, string src, Func<string, string> Code, Action<bool, string> C)
    {
        return 0;
    }
}
