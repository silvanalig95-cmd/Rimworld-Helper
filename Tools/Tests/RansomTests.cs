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

    /// <summary>
    /// The mod's side, read from the source: the parts where the design puts them, pure rules, Tell only, the game's
    /// demand hooked where it is and with its own fee, the trap from the gate's side, the records in DiplomacyBook with
    /// their keep reasons, the letters' and the right-click frames, the inbox counted, the facts, the defs, the words.
    /// </summary>
    private static int ModSide(string root, string src, Func<string, string> Code, Action<bool, string> C0)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            C0(ok, what);
        }

        string dipDir = Path.Combine(src, "Diplomacy");
        var mine = new[] { "Ransoms.cs", "RansomParty.cs", "RansomHooks.cs", "RansomLetters.cs", "RansomPanels.cs", "RansomWords.cs", "RansomDebug.cs", "Envoys.cs",
            "EnvoyLord.cs", "EnvoysAbroad.cs", "DiplomacyBook.cs", "FactsRansom.cs" };
        foreach (var f in mine) C(File.Exists(Path.Combine(dipDir, f)), $"Diplomacy/{f} is there");
        var text = mine.Where(f => File.Exists(Path.Combine(dipDir, f))).ToDictionary(f => f, f => File.ReadAllText(Path.Combine(dipDir, f)));
        string all = string.Concat(text.Values);
        foreach (var f in new[] { "RansomMath.cs", "EnvoyMath.cs" })
            C(!Regex.IsMatch(Code("Diplomacy/" + f), @"using (Verse|RimWorld|UnityEngine)"), $"{f} is pure");
        foreach (var (f, t) in text.Where(kv => !kv.Key.EndsWith("Debug.cs")))
            C(!t.Contains("Messages.Message(") && !t.Contains("Find.LetterStack.ReceiveLetter(") && !t.Contains("MoteMaker.ThrowText"), $"{f} tells the player only through Tell and speaks only through Speech");
        C(!Regex.IsMatch(all, @"GameComponentTick|override void Tick\("), "nothing per tick: the hour and the day ride on diplomacy's");
        string hub = Code("Diplomacy/Diplomacy.cs");
        C(hub.Contains("Ransoms.Hourly(now)") && hub.Contains("Envoys.Hourly(now)") && hub.Contains("EnvoysAbroad.Hourly(now)") && hub.Contains("Envoys.Daily(now)")
          && hub.Contains("DiplomacyBook.Instance?.AnyWaiting"), "in diplomacy's hour and day; the envoys' hour only while something waits");

        // ---------------- The records ----------------

        string book = text["DiplomacyBook.cs"];
        C(book.Contains("class DiplomacyHome : IExposable, IColonyEntry") && book.Contains("ColonyBook.Entry(homes, map, () => new DiplomacyHome())")
          && book.Contains("ColonyBook.Lost(homes, lost, null)") && book.Contains("ColonyBook.Daily(homes, null)"), "each home's part kept by the colonies' book");
        C(Code("ColonyState.cs").Contains("DiplomacyBook.Instance?.HomesLost(lost);") && hub.Contains("DiplomacyBook.Instance?.Daily()"), "homes lost and the day's upkeep");
        C(text["Ransoms.cs"].Contains("h.offers.Count >= RansomMath.MaxOffers"), "six offers a home at most");
        string keep = Code("World/Ledger/LedgerKeep.cs");
        C(keep.Contains("public const string Envoy = \"envoy\";") && keep.Contains("public const string Lent = \"lent\";") && keep.Contains("case KeepFor.Envoy: return Envoys.Holds(p);")
          && keep.Contains("case KeepFor.Lent: return EnvoysAbroad.Holds(p)"), "kept with KeepFor.Envoy and KeepFor.Lent (the colony's ward or hostage away too, release 2b.3), each checked daily");
        C(text["Envoys.cs"].Contains("WorldLedger.Keep(n.pawn, KeepFor.Envoy)") && text["EnvoysAbroad.cs"].Contains("WorldLedger.Keep(p, KeepFor.Lent)"), "and given those reasons");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"Diplomacy/DiplomacyBook.cs:homes\"") && save.Contains("\"Diplomacy/DiplomacyBook.cs:offers\"") && save.Contains("\"MaxOffers\""),
            "the save model knows both lists, the offers' cap read from the source");

        // ---------------- Ransom ----------------

        string r = text["Ransoms.cs"];
        C(r.Contains("RansomMath.SteadyRoll(p.thingIDNumber, f.loadID)") && r.Contains("RansomMath.DelayDays(Rand.Value)"), "wanted back by the steady roll, two to six days on");
        C(r.Contains("Arrivals.PersonKind(f) != null ? f : null") && r.Contains("TruceParley.Escort(f)"), "their own messenger at peace, a go-between of a people at peace with both at war");
        C(r.Contains("PrisonerInteractionModeDefOf.Release") && r.Contains("GenGuest.PrisonerRelease(c)"), "the captive walks out by the game's own release (the door opened if nobody walks them)");
        C(r.Contains("FactionMemory.Note(f, \"ransomed\"") && r.Contains("FactionMemory.Note(f, \"forgave\"") && r.Contains("DiplomacyMath.Ransomed") && r.Contains("DiplomacyMath.Exchanged"),
            "the deeds and the history: ransomed, forgave, dip:ransomed, dip:exchanged");
        C(Code("Diplomacy/FavoursOwed.cs").Contains("Ransoms.PaidFor(d.faction, d.name)"), "a captive paid for leaves no debt of honour");
        C(r.Contains("WorldLedger.AddTie(Tie.Kind.Grudge") && r.Contains("RansomMath.GrudgeKey") && r.Contains("RansomMath.Origin") && r.Contains("RansomMath.Grudge(o.refusals)"),
            "refused three times: the existing Grudge tie, key ransomRefused");
        C(r.Contains("RansomMath.MoreAgreed(social, Rand.Value)") && r.Contains("RansomMath.Raised(o.price)"), "asking for more by the speaker's Social");
        string hooks = text["RansomHooks.cs"];
        C(hooks.Contains("[HarmonyPatch(typeof(IncidentWorker_RansomDemand), \"CanFireNowSub\")]") && hooks.Contains("[HarmonyPatch(typeof(IncidentWorker_RansomDemand), \"TryExecuteWorker\")]"),
            "the game's own demand: its rate, hooked where it fires");
        C(Regex.Matches(hooks, @"CommsConsoleUtility\.PlayerHasPoweredCommsConsole\(map\)").Count == 2 && Regex.Matches(hooks, @"RimfolkPatches\.On").Count == 2
          && Regex.Matches(hooks, @"Ransoms\.On").Count == 2, "only without a powered comms console, with the deeper hooks and the setting");
        C(r.Contains("RansomMath.Fee(p.MarketValue, Rand.Value)") && r.Contains("l.StartTimeout(RansomMath.AnswerTicks)"), "the game's fee and its day to answer");
        C(r.Contains("ChoiceLetter_RansomDemand d && d.kidnapped != null"), "never one the game's own letter already asks about");
        C(r.Contains("f.kidnapped.RemoveKidnappedPawn(p)") && r.Contains("TradeUtility.SpawnDropPod(at, map, p)") && r.Contains("(int)f.def.techLevel < (int)TechLevel.Industrial"),
            "home as the game returns them: from the edge, or by pod from a people of the machine ages");
        C(text["RansomParty.cs"].Contains("class JobDriver_RansomPay") && text["RansomParty.cs"].Contains("Ransoms.Paid(Errand?.offer ?? -1, pawn, n)"),
            "the speaker carries the silver out and hands it over face to face");
        C(r.Contains("Raids.Send(map, f, StorytellerUtility.DefaultThreatPointsNow(map) * RansomMath.TrapPoints") && r.Contains("EdgeNear(map, gate)")
          && r.Contains("if (!Diplomacy.Hostile(f)) return;"), "the trap: hostile peoples only, a band at 0.35 from the gate's side");
        C(r.Contains("CC_Ransom_Trap_Named") && Code("Diplomacy/RansomPanels.cs").Contains("CC_Ransom_Card_Trap"), "its chance named in the letter and on the captive's card before the choice");
        string party = text["RansomParty.cs"];
        C(party.Contains("Trigger_Custom(s => s.type == TriggerSignalType.Tick && done)") && !Regex.IsMatch(Regex.Match(r, @"public static void Arrived\(LordJob_Messenger job\)[\s\S]*?\n        }\n").Value, @"ReceiveMemo"),
            "their messenger leaves by a flag set at the gate, never a memo sent from inside the lord's own transition");

        // ---------------- The envoy ----------------

        string e = text["Envoys.cs"];
        C(e.Contains("EnvoyMath.MayOffer(") && e.Contains("EnvoyMath.OffersToday(Rand.Value)") && e.Contains("Tell.CanAsk"), "their offer by the rules, when a question may come");
        C(e.Contains("Hosting.Provision(p, EnvoyMath.StayDays)") && e.Contains("Hosting.Meal(p, envoy)") && e.Contains("Hosting.Sleep(p, bed)") && e.Contains("Hosting.SleepRough(p)")
          && e.Contains("Hosting.GiveBack(p, map)"), "the missions' hosting rules: meals, beds, the floor, the bed given back");
        C(text["EnvoyLord.cs"].Contains("RansomDefOf.Rimfolk_EnvoyIndoors") && text["EnvoyLord.cs"].Contains("StoryDanger.High"), "indoors in danger");
        C(e.Contains("EnvoyMath.Talks(w - s.warmth)") && e.Contains("EnvoyMath.Gift") && e.Contains("EnvoyMath.SatIn") && e.Contains("EnvoyMath.Hurt(sheltered)")
          && e.Contains("EnvoyMath.Night(n, Empire(s), alone)"), "the stay scored from what happens on the map");
        C(e.Contains("AmbitionTracker.MeetingOn(map)") && e.Contains("RansomDefOf.Rimfolk_EnvoySitIn"), "a seat at a council meeting in session");
        C(e.Contains("EnvoyMath.LetterDue(DaysStayed(s), s.letters)") && e.Contains("RansomDefOf.Rimfolk_EnvoyLetter"), "a runner at the gate for each letter home");
        C(e.Contains("\"envoyHonored\", name, EnvoyMath.DeedWeight(s.score, days)") && e.Contains("FactionMemory.Note(f, \"slighted\"") && e.Contains("DiplomacyMath.Envoy"),
            "the report: envoyHonored (a debt for a very good stay) or slighted, and dip:envoy");
        C(e.Contains("EnvoyMath.DiedRegard(sheltered)"), "dead at the colony: by whether it sheltered them");
        C(e.Contains("if (e != null && e.Dead)") && e.Contains("Died(h, s, map, e);") && e.Contains("e.IsPrisoner || e.Faction != f") && e.Contains("e.MapHeld != map"),
            "downed out of the stay's group, then dead, taken or carried to bed: the hour sees it");
        string ab = text["EnvoysAbroad.cs"];
        C(ab.Contains("EnvoyMath.CanGo(") && ab.Contains("Dialog_MessageBox.CreateConfirmation") && ab.Contains("CC_Envoy_Ask"), "the colony's envoy: good with people, after the council's question");
        C(ab.Contains("JobDefOf.TakeInventory") && ab.Contains("CaravanExitMapUtility.ExitMapAndCreateCaravan") && ab.Contains("new CaravanArrivalAction_Enter(map.Parent)"),
            "food for the road from the stores, a caravan of their own there and back");
        C(ab.Contains("EnvoyMath.AbroadRegard(social)") && ab.Contains("FactScene.Letter(head, p, map, true)") && ab.Contains("EnvoyMath.SocialXp"),
            "their letters' real news, the notables' regard, the practice they bring home");
        C(ab.Contains("if (p.Spawned && Homes.IsHome(p.Map) || People.Kidnapped(p)) Clear(h, a);"), "an ambush on the road doesn't call the stay off; back home or carried off does");

        // ---------------- Where the player acts ----------------

        string letters = Code("Diplomacy/DiplomacyLetters.cs");
        C(letters.Contains("LetterPurpose.Ransom") && letters.Contains("LetterPurpose.Envoy") && letters.Contains("RansomLetters.Read(l, n, l.map ?? Home, ref tone)"),
            "the letters' new purposes in the existing frame");
        string acts = Code("Diplomacy/DiplomacyActs.cs");
        C(acts.Contains("OutsiderAct.OfferExchange") && acts.Contains("ExchangeActs.Talked(c, x, a, pick.code)") && acts.Contains("\"exchangeask\""), "\"Offer an exchange\" face to face");
        string panels = text["RansomPanels.cs"];
        C(panels.Contains("Inbox.Register(\"ransom\", InboxSection.Needs, 33f, NeedRows, NeedCount)") && panels.Contains("Inbox.Register(\"envoy\", InboxSection.Needs, 34f, EnvoyNeedRows, EnvoyNeedCount)")
          && panels.Contains("Inbox.Register(\"ransom-under-way\", InboxSection.UnderWay") && panels.Contains("Inbox.Register(\"envoy-under-way\", InboxSection.UnderWay"),
            "the inbox: Needs you counted through Inbox.Count, Under way in steps");
        C(!Regex.IsMatch(panels, @"TicksToDays|ToStringTicksToPeriod|days left|hours left"), "never a time left");
        C(panels.Contains("CardSections.RegisterLines(CardKind.Colonist") && panels.Contains("CardSections.RegisterLines(CardKind.Person") && panels.Contains("CardKind.People")
          && panels.Contains("CardSections.TownLines(") && panels.Contains("CardKind.Tie"), "the captive's, the envoy's, the people's, the town's and the tie's cards");
        string model = Code("Diplomacy/DiplomacyModel.cs");
        C(model.Contains("RansomPanels.Rows(f, home)") && model.Contains("RansomPanels.AwayRows()") && model.Contains("Envoys.HereAs(p)"), "Peoples' rows and Here and away");
        C(Code("Text/Facts/FactsLedger.cs").Contains("RansomEnvoyFacts(s, f);"), "the facts read with the ties");
        string settings = Code("CouncilSettings.cs");
        string catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool diplomacyRansom = true;") && settings.Contains("public bool diplomacyEnvoys = true;") && catalog.Contains("Toggle(\"diplomacyRansom\"")
          && catalog.Contains("Toggle(\"diplomacyEnvoys\""), "the settings Ransom and Envoys in the section Diplomacy, on by default");

        // ---------------- The defs and the words ----------------

        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Ransom.xml"));
        var names = defs.Root!.Elements().Select(x => x.Element("defName")?.Value).ToHashSet();
        foreach (Match m in Regex.Matches(r.Substring(r.IndexOf("class RansomDefOf", StringComparison.Ordinal)), @"public static \w+ (Rimfolk_\w+);"))
            C(names.Contains(m.Groups[1].Value), $"the def {m.Groups[1].Value} is in Rimfolk_Ransom.xml");
        foreach (var d in defs.Root.Elements().Select(x => x.Element("driverClass")?.Value ?? x.Element("letterClass")?.Value).Where(x => x != null))
            C(all.Contains("class " + d.Replace("ColonyCouncil.", "") + " "), $"{d} exists");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ransom.xml")).Root!.Elements().ToDictionary(x => x.Name.LocalName, x => x.Value);
        void Keys(string prefix, IEnumerable<string> suffixes)
        {
            foreach (var s in suffixes) C(keyed.ContainsKey(prefix + s), $"{prefix}{s} exists");
        }
        Keys("CC_Ransom_Odds_", Enum.GetNames(typeof(TrapOdds)));
        foreach (var kind in new[] { "Theirs", "Demand", "Exchange" }) Keys("CC_Ransom_Row_" + kind + "_", Enum.GetNames(typeof(RansomStage)));
        Keys("CC_Envoy_Mood_", Enum.GetNames(typeof(EnvoyMood)));
        Keys("CC_Envoy_Leaves_", Enum.GetNames(typeof(EnvoyMood)));
        Keys("CC_Envoy_Hist_Left_", Enum.GetNames(typeof(EnvoyMood)));
        Keys("CC_Envoy_Msg_Letter_", Enum.GetNames(typeof(EnvoyMood)));
        Keys("CC_Envoy_Card_Night_", Enum.GetNames(typeof(EnvoyNight)));
        Keys("CC_Envoy_Row_Abroad_", Enum.GetNames(typeof(EnvoyStage)));
        foreach (var (k, v) in keyed)
        {
            C(!v.Contains('!') && !v.Contains('—'), $"{k}: no exclamation marks or em dashes");
            C(!Regex.IsMatch(v, @"\b(in|within) \d+ (days|hours)\b|\bdays left\b|\bhours left\b"), $"{k}: no countdown");
        }
        C(keyed.Keys.Count(k => Regex.IsMatch(k, @"_\d$")) >= 20, "lines told often have fellows to vary them");
        // Bubbles: each moment the code says has lines in the pool.
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Ransom.xml")).Descendants("li").Select(x => x.Value).ToList();
        foreach (var moment in new[] { "ransompay", "ransomthanks", "ransomset", "exchangegate", "exchangeask", "exchangetalk", "exchangeyes", "exchangeno", "envoyletter" })
        {
            C(all.Contains("\"" + moment + "\"") || acts.Contains("\"" + moment + "\""), $"the code says '{moment}'");
            C(lines.Count(l => l.StartsWith("[" + moment + ",")) >= 2, $"two or more bubbles for '{moment}'");
        }
        foreach (var p in new[] { "ransom", "envoy" })
            foreach (var tone in new[] { "warm", "cordial", "cool", "refusal" })
                C(lines.Any(l => l.StartsWith("[" + p + "," + tone)), $"their answer to a letter of {p}, {tone}");
        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Ransom.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talks.Count >= 15, $"conversation pieces about captives and envoys ({talks.Count})");
        string talkText = string.Join("\n", talks.Select(x => x.ToString()));
        foreach (var tag in RansomMath.Tags.Concat(EnvoyMath.Tags)) C(talkText.Contains(tag), $"the talk speaks of {tag}");
        foreach (var ph in new[] { "{CAPTOR}", "{ENVOY}", "{OURENVOY}" }) C(talkText.Contains(ph), $"the talk names {ph}");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml"));
        foreach (var h in new[] { "Ransom for their own", "Ransom for the colony's own", "Exchanges and the trap", "Their envoy for a season", "The colony's own envoy" })
            C(almanac.Contains("<heading>" + h + "</heading>"), $"the Almanac's diplomacy page has \"{h}\"");
        C(text["RansomDebug.cs"].Contains("[DebugAction(\"Rimfolk\", \"Ransom: ") && text["RansomDebug.cs"].Contains("[DebugAction(\"Rimfolk\", \"Envoy: "), "dev actions for each part");
        return n;
    }
}
