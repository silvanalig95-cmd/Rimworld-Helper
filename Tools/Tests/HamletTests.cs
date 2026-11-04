using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Settlers who found a hamlet (World/Region/HamletMath.cs; docs/design/stage1-region.md, item 6, decisions 9 to 11) and
/// small towns' maps (World/Region/SmallTownsMath.cs, decision 4): the ask's conditions and its year's gap, the ways in,
/// the place, the escort and the first night, the road's one roll, going alone, the harvest and hard seasons, the tie
/// broken by two refusals in a season, fealty's timing, the second year, the history's kinds, the smaller base. Also the
/// mod's side: the region's day and hour run it, its save and model, Tell only, straight-line tile searches, the game's own
/// town, the vassal call standing down without the mod, the release 7 hooks, the words, the settings and the talk.
/// </summary>
public static class HamletTests
{
    private const int D = HamletMath.DayTicks;
    private const int H = HamletMath.HourTicks;

    private static bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) < eps;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "hamlets: " + what);
        }

        // ---------------- Who asks, and when ----------------

        C(HamletMath.AskAllowed(90, 5, false, -1, 90 * D) && !HamletMath.AskAllowed(89, 5, false, -1, 89 * D), "from the colony's 90th day");
        C(!HamletMath.AskAllowed(120, 4, false, -1, 120 * D), "five grown colonists or more");
        C(!HamletMath.AskAllowed(120, 9, true, -1, 120 * D), "never while a hamlet stands or settlers are on their way");
        C(!HamletMath.AskAllowed(150, 9, false, 100 * D, 159 * D) && HamletMath.AskAllowed(160, 9, false, 100 * D, 160 * D), "one ask a year per home");
        C(HamletMath.LookDue(-1, 0) && !HamletMath.LookDue(0, 9 * D) && HamletMath.LookDue(0, 10 * D), "a home is looked at every 10 days");
        C(Near(HamletMath.Chance, 0.15f), "15% at a look when settlers qualify");
        C(HamletMath.HoldingFits(60f, 10f, true) && !HamletMath.HoldingFits(59f, 10f, true) && !HamletMath.HoldingFits(80f, 30f, true) && !HamletMath.HoldingFits(80f, 5f, false),
            "a holding a year settled, with its unease low, its people at peace");
        C(HamletMath.TownFits(39.9f, true) && HamletMath.TownFits(5f, true) && !HamletMath.TownFits(40f, true) && !HamletMath.TownFits(10f, false) && !HamletMath.TownFits(-1f, true),
            "settlers from a poor or struggling town of a people at peace (a town with a fortune of its own)");
        C(HamletMath.PeopleFits(true, false, true, true) && !HamletMath.PeopleFits(true, true, true, true) && !HamletMath.PeopleFits(false, false, true, true)
          && !HamletMath.PeopleFits(true, false, false, true) && !HamletMath.PeopleFits(true, false, true, false), "a humanlike people at peace, with people to send and names for towns");
        SimulateAsks(C);

        // ---------------- Where ----------------

        C(HamletMath.PlaceFits(true, 6f, 6.6f, 5, -1f, 1f) && HamletMath.PlaceFits(true, 10f, 10f, 5, 3f, 0.3f), "6 to 10 tiles from the home, 3 from any town");
        C(!HamletMath.PlaceFits(true, 5.9f, 7f, 5, -1f, 1f) && !HamletMath.PlaceFits(true, 10.1f, 10.1f, 5, -1f, 1f), "not nearer than 6 nor farther than 10");
        C(!HamletMath.PlaceFits(true, 8f, 6.4f, 5, -1f, 1f), "clear of every one of the colony's settlements by the game's goodwill reach");
        C(!HamletMath.PlaceFits(true, 8f, 8f, 5, 2.9f, 1f) && !HamletMath.PlaceFits(true, 8f, 8f, 5, -1f, 0f) && !HamletMath.PlaceFits(false, 8f, 8f, 5, -1f, 1f),
            "not by another town, not on land that holds none, not on a tile the game holds invalid");
        C(Near(HamletMath.PlaceWeight(false, true, 1f), 2f) && Near(HamletMath.PlaceWeight(true, false, 1f), 1.5f) && Near(HamletMath.PlaceWeight(true, true, 0.5f), 1f)
          && Near(HamletMath.PlaceWeight(false, false, 0f), 0f), "a river twice, a road half again, by the land's weight");

        // ---------------- The escort ----------------

        C(HamletMath.EscortFits(2, true, 0f) && HamletMath.EscortFits(4, true, 3f), "a caravan of two grown colonists or more, bound for the place, just out of the home");
        C(!HamletMath.EscortFits(1, true, 0f) && !HamletMath.EscortFits(3, false, 0f) && !HamletMath.EscortFits(3, true, 3.1f) && !HamletMath.EscortFits(3, true, -1f),
            "not one colonist alone, not a caravan bound elsewhere, not one already far down the road");
        C(!HamletMath.WaitOver(0, 4 * D + D - 1) && HamletMath.WaitOver(0, 5 * D) && !HamletMath.WaitOver(-1, 99 * D), "they wait at the gate five days at most");
        C(!HamletMath.FirstFires(10 * H, 17 * H, 5) && HamletMath.FirstFires(20 * H, 30 * H, 6), "the first fires after a night there: eight hours and the morning");
        C(!HamletMath.FirstFires(2 * H, 12 * H, 14) && HamletMath.FirstFires(2 * H, 26 * H, 14), "an afternoon arrival waits for the next morning, a full day at most");
        C(!HamletMath.FirstFires(-1, 99 * H, 7) && !HamletMath.FirstFires(10 * H, 5 * H, 7), "not before they arrive");

        // ---------------- The road's roll ----------------

        C(Near(HamletMath.Risk(false), 0.15f) && Near(HamletMath.Risk(true), 0.35f), "the game's own caravan demand or ambush: 15%, 35% in dangerous country");
        C(HamletMath.Danger(true, -1f) && HamletMath.Danger(false, 15f) && !HamletMath.Danger(false, 15.1f) && !HamletMath.Danger(false, -1f),
            "dangerous country: a raiders' camp near the home, or a hostile town within 15 tiles of the place");

        // ---------------- Alone ----------------

        C(HamletMath.TurnedBack(0.33f) && !HamletMath.TurnedBack(0.34f), "alone, a third are turned back");
        var rng = new Random(5);
        int back = 0;
        for (int i = 0; i < 9000; i++)
            if (HamletMath.TurnedBack((float)rng.NextDouble()))
                back++;
        C(Math.Abs(back / 9000f - 1f / 3f) < 0.02f, $"a third over many roads ({back / 90f:0.0}%)");
        C(HamletMath.AloneTicks(6f) > D && HamletMath.AloneTicks(10f) > HamletMath.AloneTicks(6f) && HamletMath.AloneTicks(10f) < 6 * D, "the road alone: a day to choose the ground and a caravan's pace");
        C(Near(HamletMath.Start, 25f) && Near(HamletMath.AloneStart, 12f) && HamletMath.AloneStart < HamletMath.Start, "its fortune from 25, less when they came alone");
        C(HamletMath.MaxSettlers == 8, "eight settlers at most");

        // ---------------- Its life ----------------

        C(HamletMath.HarvestDue(true, -1, 0, 10 * D, 30f) && !HamletMath.HarvestDue(true, -1, 0, 9 * D, 30f) && !HamletMath.HarvestDue(false, -1, 0, 30 * D, 30f),
            "the harvest's carter comes at harvest time, not in its first days");
        C(!HamletMath.HarvestDue(true, 10 * D, 0, 54 * D, 30f) && HamletMath.HarvestDue(true, 10 * D, 0, 55 * D, 30f), "once a year at most");
        C(!HamletMath.HarvestDue(true, -1, 0, 30 * D, 19f) && HamletMath.HarvestDue(true, -1, 0, 30 * D, -1f), "nothing to bring while it struggles");
        C(HamletMath.Produce(25f) == 35 && HamletMath.Produce(100f) == 80 && HamletMath.Produce(-1f) == 35, "more produce as it prospers");
        C(HamletMath.HelpDue(15f, -1, 0, 5 * D) && !HamletMath.HelpDue(20f, -1, 0, 30 * D) && !HamletMath.HelpDue(15f, 10 * D, 0, 14 * D) && HamletMath.HelpDue(15f, 10 * D, 0, 15 * D),
            "in a hard season (struggling) it asks for help, every five days at most");
        C(HamletMath.NeedFor(0.1f, -1) == HamletNeed.Food && HamletMath.NeedFor(0.6f, -1) == HamletNeed.Doctor && HamletMath.NeedFor(0.9f, -1) == HamletNeed.Tools,
            "food, a doctor or tools");
        C(HamletMath.NeedFor(0.1f, (int)HamletNeed.Food) != HamletNeed.Food && HamletMath.NeedFor(0.6f, (int)HamletNeed.Doctor) != HamletNeed.Doctor, "not the same as the last ask");
        SimulateRefusals(C);

        // ---------------- Fealty ----------------

        C(HamletMath.HighSince(40f, -1, 5 * D) == 5 * D && HamletMath.HighSince(55f, 2 * D, 5 * D) == 2 * D && HamletMath.HighSince(39f, 2 * D, 5 * D) == -1,
            "since when it has stood at 40 or more");
        C(!HamletMath.FealtyDue(0, -1, -1, 59 * D) && HamletMath.FealtyDue(0, -1, -1, 60 * D) && !HamletMath.FealtyDue(-1, -1, -1, 99 * D), "after 60 days at 40 or more");
        C(!HamletMath.FealtyDue(0, 20 * D, -1, 79 * D) && HamletMath.FealtyDue(0, 20 * D, -1, 80 * D), "no ask refused in the last 60 days");
        C(!HamletMath.FealtyDue(0, -1, 70 * D, 99 * D) && HamletMath.FealtyDue(0, -1, 70 * D, 100 * D), "told not yet, it asks again after 30 days");
        C(!HamletMath.SecondYearOver(0, 119 * D) && HamletMath.SecondYearOver(0, 120 * D) && !HamletMath.SecondYearOver(-1, 999 * D), "its second year ends 120 days after its founding");

        // ---------------- Stages and history ----------------

        C(!HamletMath.Busy(HamletStage.None) && !HamletMath.Busy(HamletStage.Over) && HamletMath.Busy(HamletStage.Asked) && HamletMath.Busy(HamletStage.Sworn), "one story at a time");
        C(HamletMath.Stands(HamletStage.Standing) && HamletMath.Stands(HamletStage.Sworn) && !HamletMath.Stands(HamletStage.Escorted), "standing, or sworn");
        C(new[] { HamletStage.Asked, HamletStage.Waiting, HamletStage.Escorted, HamletStage.Alone }.All(HamletMath.OnTheirWay) && !HamletMath.OnTheirWay(HamletStage.Standing),
            "the settlers are kept while they are on their way");
        foreach (var k in HamletMath.Kinds)
            C(HistoryKinds.Matches(k, HamletMath.Family) && HistoryKinds.Matches(k, "world"), $"{k} is of the families world:hamlet and world");
        C(HamletMath.Milestone(HamletMath.KindFounded) && HamletMath.Milestone(HamletMath.KindSworn) && HamletMath.Milestone(HamletMath.KindBroken)
          && !HamletMath.Milestone(HamletMath.KindHarvest) && !HamletMath.Milestone(HamletMath.KindRefused), "its founding, oath and end are milestones; its harvests and asks everyday");
        C(HamletMath.EndKind(HamletEnd.Refused) == null && HamletMath.EndKind(HamletEnd.Gone) == null && HamletMath.EndKind(HamletEnd.Vassal) == HamletMath.KindSworn
          && HamletMath.EndKind(HamletEnd.Broken) == HamletMath.KindBroken, "how its ends go into the history");

        // ---------------- Small towns' maps (decision 4) ----------------

        C(SmallTownsMath.Of(TownKind.Hamlet, false) == SmallTown.Hamlet && SmallTownsMath.Of(TownKind.Post, true) == SmallTown.Post && SmallTownsMath.Of(TownKind.Town, true) == SmallTown.NewTown
          && SmallTownsMath.Of(TownKind.Town, false) == SmallTown.None && SmallTownsMath.Of(TownKind.Ruin, true) == SmallTown.None, "a hamlet, a post, a town in its first year");
        C(SmallTownsMath.Side(36, SmallTownsMath.SizeFactor(SmallTown.Hamlet)) == 22 && SmallTownsMath.Side(36, SmallTownsMath.SizeFactor(SmallTown.Post)) == 27
          && SmallTownsMath.Side(36, SmallTownsMath.SizeFactor(SmallTown.NewTown)) == 29, "the game's base, smaller on each side");
        C(SmallTownsMath.Side(65, 0.6f) == 39 && SmallTownsMath.Side(20, 0.6f) == SmallTownsMath.MinSide && SmallTownsMath.Side(16, 0.6f) == 16 && SmallTownsMath.Side(40, 1f) == 40,
            "a layout's ground smaller too, never under the least a hamlet needs, never larger");
        C(SmallTownsMath.Start(10, 36, 22) == 17, "the smaller ground keeps the middle");
        C(Near(SmallTownsMath.Points(1400f, SmallTown.Hamlet), 490f) && Near(SmallTownsMath.Points(1400f, SmallTown.None), 1400f) && SmallTownsMath.Points(1000f, SmallTown.Post) < SmallTownsMath.Points(1000f, SmallTown.NewTown),
            "fewer defenders, fewest at a hamlet");
        C(SmallTownsMath.NoEdgeDefense(SmallTown.Hamlet) && !SmallTownsMath.NoEdgeDefense(SmallTown.Post), "a hamlet has no walls round its edge");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string code = string.Concat(Directory.GetFiles(Path.Combine(src, "World", "Region"), "Hamlet*.cs").Select(File.ReadAllText));
        string book = Code("World/Region/RegionBook.cs");
        C(book.Contains("public HamletHome hamlet;") && book.Contains("Scribe_Deep.Look(ref hamlet, \"hamlet\")"), "each home's hamlet is saved with its part of the region");
        C(book.Contains("Hamlets.Daily(this, now)") && book.Contains("Hamlets.Hourly(this, now)"), "the region's day and hour run it (no pass of its own)");
        var tick = Regex.Match(book, @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(tick.Length > 0 && !tick.Contains("Hamlet") && !code.Contains("GameComponentTick"), "nothing per tick");
        string words = Code("World/Region/HamletWords.cs") + Code("World/Region/Hamlet.cs") + Code("World/Region/HamletLife.cs") + Code("World/Region/HamletEscort.cs")
                       + Code("World/Region/HamletFound.cs") + Code("World/Region/HamletPage.cs") + Code("World/Region/HamletAsk.cs");
        C(!Regex.IsMatch(words, @"Messages\.Message|LetterStack\.ReceiveLetter|News\.Post|Notify\."), "tells the player through Tell only (the dev actions and the targeter answer directly)");
        C(!Regex.IsMatch(code, @"TraversalDistanceBetween|WorldPath|CaravanArrivalTimeEstimator"), "straight-line distances only, inside the tile search too");
        string place = Code("World/Region/HamletPlace.cs");
        C(place.Contains("TileFinder.IsValidTileForNewSettlement(t)") && place.Contains("Find.WorldObjects.AnyWorldObjectAt(t)") && place.Contains("RiseFall.GoodwillTiles")
          && place.Contains("WorldMechanics.TerritoryOwner(t)") && place.Contains("WorldMechanics.SettlementsBeingBuilt()"), "placed by the trading posts' rules");
        C(place.Contains("WhyNotHere(home, h.people, target.Tile)") && place.Contains("Find.WorldTargeter.BeginTargeting"), "another place chosen on the world map, by the same rules");
        string found = Code("World/Region/HamletFound.cs");
        C(found.Contains("WorldObjectDefOf.Settlement") && found.Contains("SettlementNameGenerator.GenerateSettlementName") && found.Contains("town.SetFaction(people)"),
            "the game's own town of the settlers' people (decision 9)");
        C(found.Contains("rec.kind = TownKind.Hamlet") && found.Contains("HamletMath.Start") && found.Contains("Tie.Kind.Fealty") && found.Contains("key = \"protection\""),
            "on the ledger as a hamlet, its fortune from 25, a Fealty tie of protection");
        C(found.Contains("HistoryMath") || code.Contains("HamletMath.Milestone(kind) ? HistoryMath.Milestone"), "its founding is a milestone in the history");
        C(found.Contains("ColonistMemory.WalkedSettlers(people, walked)"), "those who walked the settlers out are remembered by name (release 7's hook)");
        C(Code("World/Region/HamletLife.cs").Contains("GrownTowns.Note(home, t, leader, flags, HamletWords.TaleOf(h), \"hamlet\")"), "its second year begins the town that grows (release 7's hook)");
        string life = Code("World/Region/HamletLife.cs");
        C(life.Contains("Territories.Active") && life.Contains("\"ExecuteVassalisation\"") && life.Contains("\"IsVassalageEnabled\""),
            "the vassal call by name, guarded, standing down without the mod or its vassalage");
        C(!life.Contains("GetSettlementVassaliseGoodwillCost") && !life.Contains("TryPurchaseVassalisation"), "no goodwill price for an oath they asked to swear");
        C(life.Contains("new DilemmaEffect_CarryOut") && life.Contains("to = PlaySpot.Gate"), "help is carried out to their carter at the gate");
        C(life.Contains("Couriers.Deliver(") && life.Contains("Loot.Deliver("), "the harvest carried in by their people, or left at the gate");
        string escort = Code("World/Region/HamletEscort.cs");
        C(escort.Contains("\"CaravanDemand\"") && escort.Contains("\"Ambush\"") && escort.Contains("HamletMath.Risk(danger)") && escort.Contains("h.rolled = true"),
            "one roll of the game's own caravan demand or ambush at the founding");
        C(escort.Contains("LordJob_AssistColony") && escort.Contains("foe.HostileTo(h.people)"), "the settlers among the defenders, when the attackers are their enemies too");
        C(escort.Contains("tracker.Make(home, center, CampSite.ColonyCenter(home), kind, h.people, hot, null, null, given)") && escort.Contains("\"Rimfolk_Camp_Settlers\""),
            "they camp at the gate by the camps' own machinery, the settlers' kind");
        C(!escort.Contains("AddPawn") && !escort.Contains("CaravanMaker"), "beside the caravan, never in its pawn list (decision 10)");
        string hamlet = Code("World/Region/Hamlet.cs");
        C(hamlet.Contains("p.Faction == people && !p.IsPrisonerOfColony && !p.IsSlaveOfColony") && found.Contains("living.Contains(h.leader)"),
            "a settler the colony took prisoner, enslaved or took in is no longer one of them: never taken off the map with the rest, never the hamlet's founder");
        C(Code("World/Region/HamletWords.cs").Contains("h.people.HostileTo(Faction.OfPlayer) ? \"CC_Hamlet_End_GoneHome\" : \"CC_Hamlet_End_NoPlace\""),
            "settlers who give up before their hamlet stands are told turned home for a war only when there is one");
        string patches = Code("World/Region/HamletPatches.cs");
        C(patches.Contains("nameof(Caravan.GetInspectString)") && patches.Contains("nameof(CaravanIncidentUtility.SetupCaravanAttackMap)") && !Regex.IsMatch(patches, @"return false;"),
            "the caravan's panel line and the fight, notes only");
        C(Code("World/Ledger/LedgerKeep.cs").Contains("case KeepFor.Hamlet: return Hamlets.Holds(p);") && Code("World/Ledger/LedgerKeep.cs").Contains("public const string Hamlet = \"hamlet\";"),
            "the settlers are kept in the world for a reason of their own");
        string small = Code("World/Region/SmallTowns.cs");
        C(small.Contains("nameof(SymbolStack.Push)") && small.Contains("\"kcsg_settlement\"") && small.Contains("\"settlement\""), "small towns' maps through the game's and the layouts' own builders");
        C(small.Contains("s.def != WorldObjectDefOf.Settlement") && small.Contains("map?.Parent is Settlement s"), "only the game's own settlements: sites and other mods' world objects untouched");
        C(small.Contains("Ages.IsEmpire(s.Faction)") && small.Contains("requiredGravcoreRooms"), "an Empire town or one that needs a gravcore room keeps its ground");
        string settings = Code("CouncilSettings.cs");
        string catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool hamlets = true;") && settings.Contains("Scribe_Values.Look(ref hamlets, \"hamlets\", true);") && catalog.Contains("Toggle(\"hamlets\""),
            "the setting Hamlets, on by default, in the region's settings");
        C(settings.Contains("public bool smallTownMaps = true;") && catalog.Contains("Toggle(\"smallTownMaps\"") && catalog.Contains("\"deepHooks\""), "the setting Small towns' maps, with the deeper hooks");
        C(Code("UI/WeekModel.cs").Contains("HamletPage.WeekRows(c)"), "This week shows settlers waiting at the gate");
        C(code.Contains("MainTabWindow_World.Register(Name, \"CC_Tab_Hamlets\""), "the region window has a hamlets page");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/Region/Hamlet.cs:settlers\"") && est.Contains("\"World/Region/Hamlet.cs:escorts\"") && est.Contains("\"World/Region/Hamlet.cs:refused\"") && est.Contains("\"MaxSettlers\""),
            "the save model has the settlers, the escorts and the refusals, by the rules' own bounds");
        string memory = Code("World/FactionMemory.cs");
        string keyedAll = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (var d in new[] { HamletMath.DeedProtected, HamletMath.DeedHarvest, HamletMath.DeedHelped, HamletMath.DeedRefused })
            C(memory.Contains("[\"" + d + "\"]") && keyedAll.Contains("<CC_Memory_Deed_" + d + ">"), $"their people remember {d}, with its words");
        // The ages.
        var ages = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ages.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var all = new[] { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer };
        C(all.Select(a => ages.TryGetValue(AgesMath.RiteKey(AgeFeature.Hamlet, a), out var v) ? v : "").SequenceEqual(new[] { "a new hearth", "a hamlet", "a homestead", "an outpost colony" }),
            "the hamlet's word by its people's age");
        C(all.Select(a => ages.TryGetValue(AgesMath.WordKey(AgeFeature.Hamlet, a), out var v) ? v : "").SequenceEqual(new[] { "the clan's spear", "a lord's protection", "a protectorate's guarantee", "a charter of protection" }),
            "the colony's protection by its own age");
        C(all.Select(a => ages.TryGetValue(AgesMath.RiteKey(AgeFeature.Oath, a), out var v) ? v : "").SequenceEqual(new[] { "gifts at the river", "an oath before witnesses", "a signed agreement", "a registered charter" }),
            "the oath by their age");
        // Every key the code names exists; the words have no em dashes, no exclamation marks and no countdowns.
        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 60, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyedAll.Contains("<" + k + ">"), $"{k} exists");
        foreach (var e in new[] { HamletEnd.TurnedBack, HamletEnd.Broken, HamletEnd.Joined, HamletEnd.Stayed, HamletEnd.Failed, HamletEnd.Vassal })
            C(keyedAll.Contains("<CC_Hamlet_H_" + e + ">"), $"the history's words of an end: {e}");
        foreach (var need in new[] { HamletNeed.Food, HamletNeed.Doctor, HamletNeed.Tools })
            C(keyedAll.Contains("<CC_Hamlet_Help_" + need + ">"), $"the words of an ask: {need}");
        string mine = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Hamlets.xml"));
        C(!mine.Contains("—") && !Regex.IsMatch(mine, ">[^<]*![^<]*<"), "no em dashes and no exclamation marks in its words");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Hamlets.xml")).Root!.Elements().Select(e => e.Value).ToList();
        C(keyed.All(t => !Regex.IsMatch(t, @"\{DAYS\}|\bin \d+ days|\bwithin \d+ days|\bdays? (left|more)\b")), "no countdown in its letters, news or panel");
        // The settlers' camp and the refugees' answer.
        var camp = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Settlers.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        C(camp.Element("weight")?.Value == "0" && camp.Element("family")?.Value == "true" && camp.Element("animals") != null, "the settlers' camp never comes by itself; families with carts and beasts");
        var givenUp = File.ReadAllText(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_RiseFall.xml"));
        C(givenUp.Contains("ColonyCouncil.WorldEffect_Hamlet") && givenUp.Contains("<label>Take them in</label>"), "the refugees of a town given up: a hamlet near the colony beside taking them in");
        // The facts and the talk.
        C(new[] { "town:hamlet", "town:hamlet:sworn", "their:hamlet", "region:hamlet" }.All(FactsMath.RegionTags.Contains)
          && new[] { "HAMLET", "HAMLETWORD", "HAMLETPEOPLE" }.All(FactsMath.RegionValues.Contains), "the facts' tags and placeholders for hamlets");
        C(Code("Text/Facts/FactsRegion.cs").Contains("HamletTags(s, f);"), "the region's provider reads the hamlets");
        var talk = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Hamlets.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talk.Count >= 12, $"talk pieces about the hamlets ({talk.Count})");
        C(talk.All(t => Regex.IsMatch(t.Element("when")?.Value ?? "", @"\b(town:hamlet|their:hamlet|region:hamlet)")), "each speaks only when a hamlet is there to speak of");
        foreach (var tag in new[] { "town:hamlet", "town:hamlet:sworn", "their:hamlet", "region:hamlet" })
            C(talk.Any(t => (t.Element("when")?.Value ?? "").Split(',').Contains(tag)), "a piece for " + tag);
        return n;
    }

    /// <summary>
    /// Five years of one home's looks with every roll coming up, settlers always qualifying and every ask refused: an ask
    /// on look days only, never two within a year, none before day 90.
    /// </summary>
    private static void SimulateAsks(Action<bool, string> C)
    {
        int lookedAt = -1, lastAsk = -1;
        var asks = new List<int>();
        for (int day = 0; day < 300; day++)
        {
            int now = day * D;
            if (!HamletMath.LookDue(lookedAt, now)) continue;
            lookedAt = now;
            if (!HamletMath.AskAllowed(day, 6, false, lastAsk, now)) continue;
            lastAsk = now;
            asks.Add(day);
        }
        bool spaced = asks.Zip(asks.Skip(1), (a, b) => b - a).All(g => g >= HamletMath.AskGapDays);
        C(asks.Count >= 3 && asks.Count <= 4 && asks[0] >= HamletMath.FirstDay && spaced, $"five years of looks: an ask a year at most, none before day 90 ({string.Join(", ", asks)})");
    }

    /// <summary>Hard seasons day by day: one refusal is borne, two in one season break the tie, two a season apart do not.</summary>
    private static void SimulateRefusals(Action<bool, string> C)
    {
        var refused = new List<int>();
        HamletMath.NoteRefused(refused, 10 * D);
        C(!HamletMath.Broken(refused, 10 * D), "one refusal is borne");
        HamletMath.NoteRefused(refused, 26 * D);
        C(!HamletMath.Broken(refused, 26 * D), "two refusals a season apart are borne");
        HamletMath.NoteRefused(refused, 30 * D);
        C(HamletMath.Broken(refused, 30 * D) && HamletMath.LastRefused(refused) == 30 * D, "refused twice in one season, the tie breaks");
        for (int i = 0; i < 9; i++) HamletMath.NoteRefused(refused, (40 + i) * D);
        C(refused.Count == HamletMath.MaxRefused, "the newest refusals kept, a few at most");
        HamletMath.NoteRefused(refused, 200 * D);
        C(refused.Count == 1, "none older than a year");
    }
}
