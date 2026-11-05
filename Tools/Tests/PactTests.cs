using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2a.2, pacts and truces (docs/design/stage2-diplomacy.md, item 2; Diplomacy/PactMath.cs):
/// who may offer a truce and their answer, its price, the firebrands' roll (about one truce in ten over many), its end;
/// who may be asked for a pact and their answer, their own offer, a defence pact's call (at most once a season, nine in
/// ten, a friend of the attacker declines), the band's hours, a trade pact's cart; the colony's fighters asked for; the
/// breaches read from deeds; the terms kept in a tie and read back; the facts' tags; and the mod's side: the raid patch
/// and its set, Rimfolk's own raids, ties only, Tell only, the hooks where the design puts them, the words and the
/// content.
/// </summary>
public static class PactTests
{
    private const int D = PactMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pacts and truces: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- Who may offer a truce ----------------

        C(PactMath.Why(true, false, 3f, false, false) == TruceWhy.Beaten && PactMath.Why(true, false, 15f, false, false) == TruceWhy.Beaten
          && PactMath.Why(true, false, 15.1f, false, false) == TruceWhy.None, "a raid of theirs beaten in the last 15 days");
        C(PactMath.Why(true, false, -1f, true, false) == TruceWhy.Holding && PactMath.Why(true, false, -1f, false, true) == TruceWhy.Friend,
            "holding their notable or kin, or a friend's word for peace");
        C(PactMath.Why(false, false, 1f, true, true) == TruceWhy.None && PactMath.Why(true, true, 1f, true, true) == TruceWhy.None,
            "never with a people not at war, nor one that makes no peace");

        // ---------------- Their answer ----------------

        int baseScore = PactMath.TruceScore(-80, false, false, false, TrucePrice.None, TruceTerm.Season);
        C(baseScore == -80, "their goodwill is where it starts");
        C(PactMath.TruceScore(-80, true, false, false, TrucePrice.None, TruceTerm.Season) == baseScore + 25 && PactMath.TruceScore(-80, false, true, false, TrucePrice.None, TruceTerm.Season) == baseScore + 20,
            "a raid just beaten counts 25, one of theirs held 20");
        C(PactMath.TruceScore(-80, false, false, true, TrucePrice.None, TruceTerm.Season) == baseScore + FavourMath.PeaceRegard && FavourMath.PeaceRegard == 20,
            "a friend's word for peace: as if they thought 20 better of the colony (2a.1's promise)");
        C(PactMath.TruceScore(-80, false, false, false, TrucePrice.Ours, TruceTerm.Season) > baseScore && PactMath.TruceScore(-80, false, false, false, TrucePrice.Captives, TruceTerm.Season) > baseScore
          && PactMath.TruceScore(-80, false, false, false, TrucePrice.Theirs, TruceTerm.Season) < baseScore && PactMath.TruceScore(-80, false, false, false, TrucePrice.None, TruceTerm.Year) < baseScore,
            "paying, or returning their captives, helps; asking them to pay or for a year asks more");
        bool rising = true;
        for (int g = -100; g < 0; g += 5)
            if (PactMath.TruceChance(g + 5) < PactMath.TruceChance(g)) rising = false;
        C(rising && PactMath.TruceChance(-200) == 0.05f && PactMath.TruceChance(50) == 0.95f, "likelier the better they think of the colony, never certain either way");
        C(PactMath.TruceAccepted(-50, 0.49f) && !PactMath.TruceAccepted(-50, 0.51f), "the roll against the chance");
        C(PactMath.PriceSilver(1000f) == 500 && PactMath.PriceSilver(30f) == 100 && PactMath.PriceSilver(333f) % 10 == 0, "a price of half the raid points in tens, 100 at least");
        C(PactMath.SwornGoodwill(TrucePrice.None, 0) == 5 && PactMath.SwornGoodwill(TrucePrice.Ours, 250) == 15 && PactMath.SwornGoodwill(TrucePrice.Ours, 5000) == 20
          && PactMath.SwornGoodwill(TrucePrice.Theirs, 500) == 5, "the oath is 5 goodwill, the colony's silver counts like a gift up to 15 more");

        // ---------------- Their offer ----------------

        C(PactMath.TheyOffer(true, false, false, -1, 0, 0.2f) && !PactMath.TheyOffer(true, false, false, -1, 0, 0.3f), "a quarter of beaten raids bring their offer");
        C(!PactMath.TheyOffer(true, false, false, 5 * D, 10 * D, 0f) && PactMath.TheyOffer(true, false, false, 5 * D, 20 * D, 0f), "one offer a season at most");
        C(!PactMath.TheyOffer(false, false, false, -1, 0, 0f) && !PactMath.TheyOffer(true, true, false, -1, 0, 0f) && !PactMath.TheyOffer(true, false, true, -1, 0, 0f),
            "not at peace, not a people of no peace, not with a truce holding");
        C(PactMath.TheirPrice(0.4f) == TrucePrice.Theirs && PactMath.TheirPrice(0.6f) == TrucePrice.None, "half the time they bring a price of their own");

        // ---------------- Firebrands ----------------

        C(PactMath.FirebrandChance(0) == 0.10f && PactMath.FirebrandChance(-50) == 0.10f && PactMath.FirebrandChance(-51) == 0.15f, "one in ten, 15% for a people of bitter ways");
        C(PactMath.FirebrandPoints == 0.6f, "their raid at the game's 0.6 of the threat points");
        bool inTerm = true;
        foreach (var term in new[] { TruceTerm.Season, TruceTerm.Year })
            for (float r = 0f; r <= 1f; r += 0.1f)
            {
                float day = PactMath.FirebrandDay(100f, PactMath.TermDays(term), r);
                if (day <= 100f || day >= 100f + PactMath.TermDays(term)) inTerm = false;
            }
        C(inTerm, "their firebrands strike within the term, after its first day");
        var rng = new Random(11);
        int truces = 4000, struck = 0;
        for (int i = 0; i < truces; i++)
            if (rng.NextDouble() < PactMath.FirebrandChance(-20)) struck++;
        float share = struck / (float)truces;
        C(share > 0.085f && share < 0.115f, $"over many truces about one in ten broken by firebrands ({share:P1})");

        // ---------------- Its end ----------------

        C(PactMath.TermDays(TruceTerm.Season) == 15f && PactMath.TermDays(TruceTerm.Year) == 60f, "a season or a year");
        C(PactMath.Peace(true, 0) && PactMath.Peace(false, -30) && !PactMath.Peace(true, -1), "a peace when their goodwill came to 0, or the war is over");
        C(PactMath.FromPeace(-35) == 35 && PactMath.FromPeace(10) == 0, "how far from peace, never below 0");
        C(PactMath.Due(10f, 8f) && !PactMath.Due(10f, 6f) && !PactMath.Due(10f, 10.5f), "its end near: the last three days");

        // ---------------- Pacts ----------------

        C(PactMath.MayPropose(20, false, false) && !PactMath.MayPropose(19, false, false) && !PactMath.MayPropose(80, true, false) && !PactMath.MayPropose(80, false, true),
            "a pact with a people at peace at goodwill 20 or more, never the Empire (decision 9)");
        C(PactMath.PactScore(40, 40, true, PactKind.Defence) == 55 && PactMath.PactScore(40, 40, false, PactKind.Trade) == 45, "their goodwill and their head's regard, an enemy in common, the kind");
        C(PactMath.PactChance(10) == 0.05f && PactMath.PactChance(100) == 0.95f && PactMath.PactChance(40) > PactMath.PactChance(20), "rare among cool friends, near certain among close ones");
        C(PactMath.MayOfferPact(50, 30, true, false, false, -1, 0) && !PactMath.MayOfferPact(49, 30, true, false, false, -1, 0) && !PactMath.MayOfferPact(60, 30, false, false, false, -1, 0),
            "they offer one only at regard 50 and with an enemy in common");
        C(!PactMath.MayOfferPact(60, 30, true, false, false, 10 * D, 60 * D) && PactMath.MayOfferPact(60, 30, true, false, false, 10 * D, 70 * D) && !PactMath.MayOfferPact(60, 30, true, false, true, -1, 0),
            "once a year at most, and never the Empire");
        C(PactMath.PactDays == 60f && PactMath.Renews(30, 0, false) && !PactMath.Renews(30, -1, false) && !PactMath.Renews(10, 50, false), "a year, renewed while they think well enough of the colony");

        // ---------------- Calling a defence pact in ----------------

        C(PactMath.MayCall(0f, 3f) && !PactMath.MayCall(10f, 24f) && PactMath.MayCall(10f, 25f), "at most once a season per pact");
        C(PactMath.Answer(true, 0f) == PactAnswer.Decline, "the attacker their friend: they decline, and the pact ends with nobody at fault");
        C(PactMath.Answer(false, 0.89f) == PactAnswer.Come && PactMath.Answer(false, 0.91f) == PactAnswer.PutOff, "nine times in ten they come, once their riders are elsewhere");
        int came = 0, calls = 0;
        rng = new Random(5);
        for (int i = 0; i < 5000; i++)
        {
            calls++;
            if (PactMath.Answer(false, (float)rng.NextDouble()) == PactAnswer.Come) came++;
        }
        C(Math.Abs(came / (float)calls - 0.9f) < 0.02f, $"over many calls nine in ten come ({came}/{calls})");
        C(PactMath.BandHours(0f) == 3f && PactMath.BandHours(100f) == 12f && PactMath.BandHours(4f) == 6f, "their band some hours later, more the further away");
        // Five years of threats every few days: called at most once a season.
        int last = -1, worst = 0;
        var perSeason = new Dictionary<int, int>();
        for (int day = 1; day < 301; day += 3)
        {
            if (!PactMath.MayCall(last < 0 ? 0f : last, day)) continue;
            last = day;
            int s = (day - 1) / 15;
            perSeason[s] = perSeason.TryGetValue(s, out int k) ? k + 1 : 1;
            worst = Math.Max(worst, perSeason[s]);
        }
        C(worst <= 1, "five years of threats: never two calls in one season");

        // ---------------- A trade pact ----------------

        C(PactMath.SeasonDue(0f, 1f) && !PactMath.SeasonDue(10f, 20f) && PactMath.SeasonDue(10f, 25f), "their caravan and the cart each season");
        C(PactMath.CartCount(1f, 3000) == 250 && PactMath.CartCount(1f, 90) == 30 && PactMath.CartCount(500f, 9) == 1 && PactMath.CartCount(0f, 100) == 0,
            "about 250 silver's worth, a third of the stores at most");
        C(PactMath.CartPay(2f, 100) == 160, "four fifths of its worth in silver");

        // ---------------- Their call to arms ----------------

        C(PactMath.Answers(2, true) && !PactMath.Answers(1, true) && !PactMath.Answers(5, false), "two grown colonists or more, bound for their town or there");
        C(PactMath.SetOutDays == 5f && FavourMath.DueDays(FavourNeed.Fighters) == 5f, "to set out within five days, a pact's call or a debt's");
        C(PactMath.FightPoints == 0.5f && PactMath.AlliesPoints < PactMath.FightPoints, "their attackers at half the home's threat points, a few of theirs beside");
        C(FavourMath.ParseAsk(FavourMath.AskCode(FavourAsk.Fighters, "12"), out var fa, out var farg) && fa == FavourAsk.Fighters && farg == "12", "a debt called in for fighters, kept in its terms");
        C(FavourMath.ParseNeed(FavourMath.NeedCode(FavourNeed.Fighters), out var fn) && fn == FavourNeed.Fighters, "their call for fighters, kept in its terms");

        // ---------------- Breaches ----------------

        string memory = Code("World/FactionMemory.cs");
        foreach (var k in PactMath.OurBreaches)
            C(Regex.IsMatch(memory, "\\[\"" + k + "\"\\] = K\\(DeedSide\\.Wrong"), $"{k} is a wrong of the colony's the peoples remember");
        foreach (var k in PactMath.TheirBreaches)
            C(Regex.IsMatch(memory, "\\[\"" + k + "\"\\] = K\\(DeedSide\\.TheirWrong"), $"{k} is a wrong of theirs");
        C(PactMath.PactBreaches.All(PactMath.OurBreach) && !PactMath.PactBreach("attacked") && !PactMath.PactBreach("fell") && PactMath.PactBreach("townAttacked"),
            "a pact breaks on a real wrong, not a stray blow");
        C(!PactMath.OurBreach("traded") && !PactMath.OurBreach("refused") && !PactMath.TheirBreach("aid"), "good turns and refusals are no breach");
        C(PactMath.BreaksPact("killed", false) && !PactMath.BreaksPact("killed", true), "one of theirs killed breaks it, but not while their band fights on the colony's land (a stray shot)");
        C(PactMath.PactBreaches.Where(k => k != "killed").All(k => PactMath.BreaksPact(k, true) && PactMath.BreaksPact(k, false)) && !PactMath.BreaksPact("attacked", false),
            "every other wrong breaks it whether their band is there or not, and a blow struck never");
        C(Code("Diplomacy/PactsHooks.cs").Contains("PactMath.BreaksPact(d.kind, BandOnAHome(f))"), "the hook asks whether their band is on a home");

        // ---------------- The terms ----------------

        foreach (PactKind k in Enum.GetValues(typeof(PactKind))) C(PactMath.ParseKind(PactMath.KindKey(k), out var back) && back == k, $"the kind {k} kept and read back");
        foreach (TruceTerm t in Enum.GetValues(typeof(TruceTerm)))
            foreach (bool pending in new[] { true, false })
                C(PactMath.ParseTerm(PactMath.TermKey(t, pending), out var bt, out bool bp) && bt == t && bp == pending, $"the term {t} ({(pending ? "to be sworn" : "sworn")}) kept and read back");
        foreach (TrucePrice p in Enum.GetValues(typeof(TrucePrice))) C(PactMath.ParsePrice(PactMath.PriceKey(p)) == p, $"the price {p} kept and read back");
        C(PactMath.ParseBand(PactMath.BandCode(123.25f), out float bd) && Math.Abs(bd - 123.25f) < 0.001f && !PactMath.ParseBand("arms:3:4", out _), "a band's day kept and read back");
        C(PactMath.ParseArms(PactMath.ArmsCode(42, 99.5f), out int town, out float due) && town == 42 && Math.Abs(due - 99.5f) < 0.001f && !PactMath.ParseArms(PactMath.BandCode(1f), out _, out _),
            "the colony's fighters asked for at a town, kept and read back");
        C(!PactMath.ParseKind("trade:x", out _) && !PactMath.ParseTerm(null, out _, out _) && PactMath.ParsePrice("nonsense") == TrucePrice.None, "anything else reads as nothing");

        // ---------------- The facts ----------------

        var tags = PactMath.Tags(true, true, true, 0f, PactMath.Defence).Select(x => x.tag).ToList();
        C(tags.SequenceEqual(new[] { "tie:pact", "tie:pact:ours", "tie:pact:due", "tie:pact:defence" }), "a pact's tags: tie:pact, who offered, a call under way, its kind");
        var ttags = PactMath.Tags(false, false, false, 30f).Select(x => x.tag).ToList();
        C(ttags.SequenceEqual(new[] { "tie:truce", "tie:truce:theirs" }), "a truce's tags: tie:truce and who offered");
        C(PactMath.Tags(true, true, false, 0f)[0].heat > PactMath.Tags(true, true, false, 20f)[0].heat, "more current when new");
        C(PactMath.WithKey(true) == "PACTWITH" && PactMath.WithKey(false) == "TRUCEWITH", "{PACTWITH}, {TRUCEWITH}");

        // ---------------- The mod's side ----------------

        var dip = Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        foreach (var f in new[] { "PactMath.cs", "Truces.cs", "TruceParley.cs", "TruceObjection.cs", "Pacts.cs", "PactCalls.cs", "CallsToArms.cs", "PactsHooks.cs", "PactsPanels.cs", "PactWords.cs", "FactsPacts.cs", "PactsDebug.cs" })
            C(dip.ContainsKey(f), $"{f} in Diplomacy/");
        C(!Regex.IsMatch(dip["PactMath.cs"], @"using (Verse|RimWorld|UnityEngine)"), "PactMath.cs is pure");
        foreach (var (file, text) in dip.Where(kv => kv.Key.StartsWith("Pact") || kv.Key.StartsWith("Truce") || kv.Key == "CallsToArms.cs").Where(kv => kv.Key != "PactsDebug.cs"))
            C(!text.Contains("Messages.Message(") && !text.Contains("Find.LetterStack.ReceiveLetter("), $"{file} tells the player only through Tell");
        // Nothing saved but the ties: only the letters, the parley's group and the jobs keep their own.
        foreach (var f in new[] { "Truces.cs", "CallsToArms.cs", "Pacts.cs", "TruceParley.cs", "PactCalls.cs", "TruceObjection.cs" })
        {
            var scribed = Regex.Split(dip[f], @"(?=\bclass \w+)").Where(part => part.Contains("Scribe_")).Select(part => Regex.Match(part, @"^class (\w+)").Groups[1].Value).ToList();
            C(scribed.All(c => c.StartsWith("ChoiceLetter_") || c.StartsWith("LordJob_") || c.StartsWith("JobDriver_") || c.EndsWith("Context")),
                $"{f} saves nothing but its letters, its lord, its jobs and its question ({string.Join(", ", scribed)})");
        }
        C(dip["Truces.cs"].Contains("WorldLedger.AddTie(Tie.Kind.Truce") && dip["Pacts.cs"].Contains("WorldLedger.AddTie(Tie.Kind.Pact"), "truces and pacts are the ledger's own ties");
        C(dip["Truces.cs"].Contains("Tie.Never, Origin") && dip["Pacts.cs"].Contains("Tie.Never, Origin"), "their ends are kept in their terms, reached in diplomacy's hour (a truce's end may be a peace)");
        string hooks = dip["PactsHooks.cs"];
        C(Regex.IsMatch(hooks, @"\[HarmonyPatch\(typeof\(IncidentWorker_RaidEnemy\), nameof\(IncidentWorker_RaidEnemy\.FactionCanBeGroupSource\)\)\]\s*public static class Patch_TruceRaids")
          && hooks.Contains("Truces.KeepRaidsAway && Truces.KeepsAway(f, map)"), "Patch_TruceRaids on the game's choice of raiders, with the setting");
        C(dip["Truces.cs"].Contains("Fresh.Holds(keepsAt, now, Sched.RareTicks)"), "its set built once a 250-tick stretch");
        C(Code("Happenings/HappeningHelpers.cs").Contains("public static bool CanRaid(Faction f, Map home = null)") && Code("Happenings/HappeningHelpers.cs").Contains("!Truces.Bars(f, home)")
          && Code("Happenings/HappeningHelpers.cs").Contains("!CanRaid(f, map)"), "Rimfolk's own raids ask Raids.CanRaid, with the home");
        C(dip["Truces.cs"].Contains("letting = f;") && dip["Truces.cs"].Contains("PactMath.FirebrandPoints"), "their firebrands' raid goes past it, and breaks it");
        C(hooks.Contains("[HarmonyPatch(typeof(IncidentWorker_Raid), \"PostProcessSpawnedPawns\")]") && hooks.Contains("[HarmonyPatch(typeof(IncidentWorker_Ambush_EnemyFaction), \"CreateLordJob\")]"),
            "their raid or ambush that comes anyway breaks it");
        C(memory.Contains("PactsHooks.Noted(d);"), "breaches read from the memory's deeds as they are noted");
        string camp = Code("World/Region/RaidersCamp.cs");
        C(camp.Contains("public static void TruceSworn(Map home, Faction people)") && camp.Contains("Truces.KeepsAway(c.people, home)") && camp.Contains("Truces.Bars(people, home)"),
            "their raiders' camp breaks up, and none is set up while it holds");
        C(dip["Pacts.cs"].Contains("Ages.IsEmpire(f)") && dip["PactsPanels.cs"].Contains("!Ages.IsEmpire(f)"), "the Empire makes truces, no pacts");
        string band = Code("Camps/WarCamp.cs");
        C(band.Contains("public string called;") && band.Contains("Scribe_Values.Look(ref called, \"called\");") && band.Contains("b.Fell > 0"),
            "a band called in keeps the bargain; only their fallen leave the colony owing them");
        C(Code("Integrations/World/WorldMechanics.cs").Contains("PactsHooks.BattleBegan(a, b, home)"), "Factional War's battles name a pact or truce partner (decision 18)");
        string terr = Code("Integrations/World/TerritoriesAdapter.cs");
        C(terr.Contains("public static bool InvasionAt(Settlement town, out Faction attacker)") && terr.Contains("public static bool JoinFight(Caravan c, Settlement town)")
          && terr.Contains("ArrivalAction_JoinFight"), "Faction Territories' invasions are calls, joined through the mod's own arrival");
        C(dip["CallsToArms.cs"].Contains("CaravanIncidentUtility.SetupCaravanAttackMap"), "the fight away is on the game's caravan battle map (decision 3)");
        C(dip["CallsToArms.cs"].Contains("if (OnTownMap(town))") && dip["CallsToArms.cs"].Contains("|| KeptLately(s, enemy)"),
            "a call answered on the town's own map (Faction Territories' Join fight) is kept, and a war answered isn't asked of again after a reload");
        C(dip["Truces.cs"].Contains("if (sent && t.Live) BrokenByThem("), "their firebrands' raid breaks it with the deeper hooks off too");
        C(dip["TruceParley.cs"].Contains("Scribe_Values.Look(ref heard, \"heard\")"), "an avenger's objection heard stays heard across a reload");
        C(dip["PactsPanels.cs"].Contains("ChoiceLetter_TruceOffer t && t.Waiting") && dip["PactsPanels.cs"].Contains("c.WaitingOnPact"), "Needs you counts only letters that can still be answered");
        C(dip["PactCalls.cs"].Contains("PlayFire.Light") && dip["PactCalls.cs"].Contains("Building_CommsConsole"), "the call by a signal fire (the bonfire's own) or at the comms console");
        C(dip["Pacts.cs"].Contains("WarCamps.MakeFighters(f, home)") && dip["Pacts.cs"].Contains("WarCamps.Start(home, f, enemy, pawns)"), "their band is a war band");
        C(Code("Text/Facts/FactsLedger.cs").Contains("PactTies(s, f, now);"), "the ties' provider speaks of pacts and truces");
        C(Code("UI/RegionViewsMath.cs").Contains("p.stance == Stance.Sour && !p.truce ? Hue.Sour : Hue.Dim") && Code("UI/Region/LensModel.cs").Contains("f.truce = home != null && Truces.KeepsAway(s.Faction, home);"),
            "the Threats lens dims a truce partner");
        C(dip["PactsPanels.cs"].Contains("Inbox.Register(\"pacts\", InboxSection.Needs") && dip["PactsPanels.cs"].Contains("CardActs.Register(CardKind.People")
          && dip["PactsPanels.cs"].Contains("CardSections.RegisterLines(CardKind.Tie"), "the inbox, the people card's acts and the tie's card through the registration APIs");
        C(!dip["PactsPanels.cs"].Contains("MainTabWindow_World.Register("), "no new tab or page");
        string settings = Code("CouncilSettings.cs");
        string catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool diplomacyPacts = true;") && settings.Contains("public bool truceRaids = true;") && catalog.Contains("Toggle(\"diplomacyPacts\"")
          && catalog.Contains("Toggle(\"truceRaids\", \"CC_Settings_TruceRaids\", \"CC_SetNote_TruceRaids\", \"diplomacyPacts,deepHooks\")"),
            "the settings in Diplomacy: pacts and truces, and truces keep raids away with the deeper hooks");
        C(DiplomacyMath.Kinds.Contains(DiplomacyMath.Parley) && DiplomacyMath.Kinds.Contains(DiplomacyMath.Band) && DiplomacyMath.Kinds.Contains(DiplomacyMath.Arms)
          && DiplomacyMath.Kinds.All(k => HistoryKinds.Family(k) == DiplomacyMath.Family), "the history's dip: kinds for a truce sworn, a band, the colony's fighters away");

        // ---------------- The words ----------------

        string code = string.Concat(dip.Where(kv => kv.Key.StartsWith("Pact") || kv.Key.StartsWith("Truce") || kv.Key == "CallsToArms.cs" || kv.Key == "FactsPacts.cs").Select(kv => kv.Value));
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 100, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyedAll.Contains("<" + k + ">"), $"{k} has its words");
        foreach (PactKind k in Enum.GetValues(typeof(PactKind))) C(keyedAll.Contains("<CC_Pact_Kind_" + k + ">") && keyedAll.Contains("<CC_Pact_What_" + k + ">"), $"the kind {k} in words");
        foreach (TruceTerm t in Enum.GetValues(typeof(TruceTerm))) C(keyedAll.Contains("<CC_Truce_Term_" + t + ">"), $"the term {t} in words");
        foreach (TrucePrice p in Enum.GetValues(typeof(TrucePrice))) C(keyedAll.Contains("<CC_Truce_Price_" + p + ">"), $"the price {p} in words");
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
            C(keyedAll.Contains("<CC_Pact_Fire_" + age + ">") && keyedAll.Contains("<CC_Pact_Comms_" + age + ">"), $"the call by the {age} age's means");
        foreach (var p in new[] { "Pact", "Truce" }) C(keyedAll.Contains("<CC_Letter_Purpose_" + p + ">") && keyedAll.Contains("<CC_Letter_PurposeCap_" + p + ">"), $"the letters' purpose {p}");
        var mine = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Pacts.xml")).Root!.Elements().ToList();
        foreach (var e in mine)
            C(!e.Value.Contains("\u2014") && !e.Value.Contains("!") && !Regex.IsMatch(e.Value, @"\b(honour|favour|neighbour|colour|defence)", RegexOptions.IgnoreCase),
                $"{e.Name.LocalName}: no em dash, no exclamation, American spelling");
        C(mine.Count(e => Regex.IsMatch(e.Name.LocalName, "_[2-6]$")) >= 15, "the letters' lines have fellows to vary them");

        // ---------------- The content ----------------

        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Pacts.xml")).Root!.Elements().ToList();
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Pacts.xml")).Root!.Elements().ToList();
        var letterLines = pools[0].Descendants("li").ToList();
        var bubbles = pools[1].Descendants("li").ToList();
        C(talks.Count + letterLines.Count >= 30 && talks.Count + letterLines.Count <= 50, $"the content pass: {talks.Count} talk pieces and {letterLines.Count} letter lines");
        foreach (var t in talks)
            C((t.Element("when")?.Value ?? "").Split(',').Any(w => w.StartsWith(PactMath.PactTag) || w.StartsWith(PactMath.TruceTag)), $"{t.Element("defName")?.Value} speaks of a pact or a truce");
        C(letterLines.Any(l => l.Value.StartsWith("[truce,refusal")) && letterLines.Any(l => l.Value.StartsWith("[truce,cool")) && letterLines.Any(l => l.Value.StartsWith("[pact,warm"))
          && letterLines.Any(l => l.Value.StartsWith("[pact,refusal")), "their answers by purpose and tone");
        C(letterLines.Count(l => l.Value.Contains("theirLowtech")) >= 3 && letterLines.Count(l => l.Value.Contains("theirHightech")) >= 3 && talks.Any(t => t.ToString().Contains("their:tribal"))
          && talks.Any(t => t.ToString().Contains("their:spacer")), "words by their age");
        foreach (var m in new[] { "parley", "parleyterms", "parleysworn", "pactask", "pacttalk", "pactyes", "pactno" })
            C(bubbles.Any(l => l.Value.StartsWith("[" + m + ",")), $"the bubbles for {m}");
        C(Code("Diplomacy/TruceParley.cs").Contains("Outsiders.Say(pawn, \"parley\", Envoy)") && Code("Diplomacy/DiplomacyActs.cs").Contains("\"pactask\""), "said at the gate and face to face");
        string alm = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml"));
        C(alm.Contains("truce") && alm.Contains("defense pact") && alm.Contains("trade pact") && alm.Contains("signal fire"), "the Almanac's diplomacy page tells of pacts and truces");
        C(File.Exists(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_Truce.xml")) && Code("Diplomacy/TruceObjection.cs").Contains("v.Active && v.faction == f"),
            "a truce with a people one of ours swore vengeance on comes to the council with the avenger's objection");
        return n;
    }
}
