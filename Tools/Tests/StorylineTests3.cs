using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Storylines, round 3 (docs/quests-proposals.md S6, S9, S10, S11, S12; Storylines/; DESIGN.md "Storylines, round 3"): a
/// dream of the gods, raiders settling nearby, the town's well ran dry, the vault's sealed case and the lost founder's grave.
/// Each one's branches, its numbers, its paths played out to their endings, and the mod's side: the gods unseen in the
/// dream (the colony's own missions, the god named only as the colony knows it, nothing of its mind), the raiders' fights
/// and the machine in the case within the shared harm budget, the one away part only within three tiles, the lent hand
/// kept in the world and brought home, the grave's search and its yearly visits, the card, the dev actions and the spoken
/// lines. Every storyline's shape (each part and outcome leading on, its event, terms and words) is checked with round 1's
/// in StorylineTests.
/// </summary>
public static class StorylineTests3
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "storylines, round 3: " + what);
        }

        StorylineMath.Step Next(string kind, string part, string outcome, int times = 1, float roll = 0.5f) => StorylineMath.Next(kind, part, outcome, times, roll);

        // ================= S6, a dream of the gods =================

        const string Dream = DreamFlow.Key;
        C(StorylineMath.Parts(Dream).SequenceEqual(new[] { "place", "watch", "offering" }), "the dream: the place built, the watch, the offering");
        C(Next(Dream, "place", "built").part == "watch" && Next(Dream, "place", "notbuilt").ending == "unbuilt" && Next(Dream, "place", "declined").ending == "declined",
            "built and given to the god: the watch; not built or turned down: it ends");
        C(Next(Dream, "watch", "seen") is var seen && seen.part == "offering" && seen.flag == "seen" && Next(Dream, "watch", "unwatched").flag == "nowatch"
          && Next(Dream, "watch", "declined").part == "offering", "the dream comes back whether the light was seen or not");
        C(Next(Dream, "offering", "given").ending == "given" && Next(Dream, "offering", "notgiven").ending == "notgiven" && Next(Dream, "offering", "declined").ending == "refused",
            "the offering given, not brought, or refused");
        C(Next(Dream, "watch", "ended").ending == "lost" && Next(Dream, "offering", "ended").ending == "lost", "the place gone: it ends plainly");
        C(DreamFlow.Honoured("given") && new[] { "declined", "unbuilt", "refused", "notgiven" }.All(DreamFlow.Troubled) && !DreamFlow.Troubled("given") && !DreamFlow.Honoured("lost"),
            "given, the dreamer is honoured; refused or let go, troubled for a while");
        C(DreamFlow.Night(21) && DreamFlow.Night(2) && !DreamFlow.Night(4) && !DreamFlow.Night(12), "the watch is kept at night, nine to four");
        C(Enumerable.Range(0, 100).Select(i => DreamFlow.SignAt(i / 100f)).All(h => h >= 1f && h <= DreamFlow.WatchHours) && DreamFlow.SignAt(float.NaN) == 1f,
            "the sign comes partway through the watch, never the moment someone arrives");
        C(DreamFlow.DreamerWeight(true, false) > DreamFlow.DreamerWeight(false, false) && DreamFlow.OfferingDays == 3f, "the priest likelier to dream; three days for the offering, the worship's own");
        C(DreamFlow.PlaceMinDays >= 5f && DreamFlow.PlaceMaxDays <= 10f, "about a week to build the place");

        // ================= S9, raiders settling nearby =================

        const string Raid = RaidersFlow.Key;
        C(StorylineMath.Parts(Raid).SequenceEqual(new[] { "defences", "raid", "strike", "tribute" }), "the raiders: defences, the raid that tests, the strike, their price");
        foreach (var how in new[] { "ready", "partly", "unready" })
            C(Next(Raid, "defences", how) is var d && d.part == "raid" && d.flag == how, "defences " + how + ": the raid comes, remembered as " + how);
        C(Next(Raid, "defences", "declined").ending == "declined" && Next(Raid, "raid", "declined").ending == "leftalone", "turned down: the neighbours deal with them; no fight comes unasked");
        C(Next(Raid, "raid", "repelled").part == "strike" && Next(Raid, "raid", "quiet").part == "strike" && Next(Raid, "raid", "overrun") is var ov && ov.part == "tribute" && ov.flag == "overrun",
            "beaten off or quiet: the strike; overrun: they ask a price");
        C(Next(Raid, "strike", "cleared").ending == "campgone" && Next(Raid, "strike", "failed").part == "tribute", "the camp gone, or a price each season");
        C(Next(Raid, "strike", "declined", 1, 0.1f).ending == "theycleared" && Next(Raid, "strike", "declined", 1, 0.9f).part == "tribute", "the colony would not help: the neighbours alone, sometimes enough");
        int alone = Enumerable.Range(0, 1000).Count(i => Next(Raid, "strike", "declined", 1, i / 1000f).ending == "theycleared");
        C(alone > 300 && alone < 400, "the neighbours clear it alone about a third of the time (" + alone + ")");
        var t1 = Next(Raid, "tribute", "paid", 1);
        C(t1.part == "tribute" && t1.quiet && t1.min >= 13f && t1.max <= 17f && t1.flag == "paid", "paid: they ask again next season");
        C(Next(Raid, "tribute", "declined", 2).flag == "refused" && Next(Raid, "tribute", "paid", RaidersFlow.Seasons).ending == "movedon"
          && Next(Raid, "tribute", "declined", RaidersFlow.Seasons).ending == "movedon", "refused, they come for it; after a year they move on");
        C(RaidersFlow.Readiness(3, 3, 0, 40) == "ready" && RaidersFlow.Readiness(0, 3, 40, 40) == "ready" && RaidersFlow.Readiness(2, 4, 0, 40) == "partly"
          && RaidersFlow.Readiness(1, 4, 10, 40) == "unready", "ready: either goal met; partly: half of either");
        C(RaidersFlow.GuardsGoal(1) == 3 && RaidersFlow.GuardsGoal(30) == 6 && RaidersFlow.WallGoal(1) == 24 && RaidersFlow.WallGoal(30) == 60, "the defences asked for by the colony's size");
        C(RaidersFlow.RaidOutcome(6, 3, 5) == "repelled" && RaidersFlow.RaidOutcome(6, 2, 1) == "repelled" && RaidersFlow.RaidOutcome(6, 1, 3) == "overrun" && RaidersFlow.RaidOutcome(0, 0, 0) == "quiet",
            "beaten off when half fell or more of them than of the colony's");
        C(RaidersFlow.RaidShare <= 0.5f && RaidersFlow.RaidChance < 1f && RaidersFlow.NearTiles == 3, "a small raid, within the harm budget; the strike's camp only within three tiles");
        C(RaidersFlow.StrikeChance(true) > RaidersFlow.StrikeChance(false) && RaidersFlow.Allies(1) == 2 && RaidersFlow.Allies(30) == 4, "the colony's food and medicine help; two to four of theirs at the camp");
        C(RaidersFlow.Price(400f) == 200 && RaidersFlow.Price(0f) == 100 && RaidersFlow.Seasons == 4, "their price about half their raid's strength, a hundred at least, for a year");
        C(RaidersFlow.SupplyMeals(5) == 15 && RaidersFlow.SupplyMedicine(5) == 5, "food and medicine for their fighters");

        // ================= S10, the town's well ran dry =================

        const string Well = WellFlow.Key;
        C(StorylineMath.Parts(Well).SequenceEqual(new[] { "water", "digger", "refugees" }), "the well: water or food, a hand lent, the refugees");
        C(Next(Well, "water", "sent").part == "digger" && Next(Well, "water", "partly").part == "digger" && Next(Well, "water", "none").ending == "nohelp"
          && Next(Well, "water", "declined").ending == "declined", "help sent: they ask for a hand; none: it ends");
        C(Next(Well, "digger", "found") is var g && g.ending == "grew" && g.max > 0f && StorylineMath.EndingHasLetter(Well, "grew"), "the well found: the town grows, told a day or two later");
        C(Next(Well, "digger", "notfound").part == "refugees" && Next(Well, "digger", "lost").ending == "lostdigger", "not found: refugees; the hand lost: it ends");
        C(Next(Well, "digger", "declined", 1, 0.1f).ending == "theirown" && Next(Well, "digger", "declined", 1, 0.9f) is var nh && nh.part == "refugees" && nh.flag == "nohand",
            "nobody lent: now and then they find it themselves");
        C(Next(Well, "refugees", "takenin").ending == "takenin" && Next(Well, "refugees", "declined").ending == "turnedaway", "the refugees taken in or turned away");
        C(WellFlow.FoundChance(6) == 0.4f && WellFlow.FoundChance(16) == 0.9f && WellFlow.FoundChance(20) == 0.9f && WellFlow.FoundChance(10) > WellFlow.FoundChance(6),
            "the new well found more often the better the hand");
        C(WellFlow.Meals(1) == 12 && WellFlow.Meals(20) == 30 && WellFlow.Water(1) == 20 && WellFlow.Water(20) == 40, "the water or meals asked for by the colony's size");
        C(WellFlow.Pay(10, 15f) == 180 && WellFlow.PartPay(5, 15f) == 80 && WellFlow.WaterOutcome(10, 10) == "sent" && WellFlow.WaterOutcome(5, 10) == "partly" && WellFlow.WaterOutcome(4, 10) == "none",
            "a fifth over the worth; half or more, its plain worth");
        C(WellFlow.Wage(5f, 10) == 280 && WellFlow.XpPerDay > WellFlow.OtherXpPerDay && WellFlow.MinSkill == 6 && WellFlow.AwayMaxDays <= 6f, "the lent hand's wage and lessons, a few days away");
        C(Enumerable.Range(0, 100).Select(i => WellFlow.Refugees(i / 100f)).All(x => x >= 2 && x <= 4), "two to four refugees");

        // ================= S11, the vault's sealed case =================

        const string Vault = VaultFlow.Key;
        C(StorylineMath.Parts(Vault).SequenceEqual(new[] { "cells", "engineer", "case" }), "the vault: components, the engineer, the case");
        C(Next(Vault, "cells", "sent").part == "engineer" && Next(Vault, "cells", "none").ending == "dark" && Next(Vault, "cells", "declined").ending == "declined", "components sent: the engineer next");
        C(Next(Vault, "engineer", "worked").part == "case" && Next(Vault, "engineer", "short").part == "case" && Next(Vault, "engineer", "harmed").ending == "blamed"
          && Next(Vault, "engineer", "declined").ending == "noengineer", "the engineer hosted: the case next");
        C(Next(Vault, "case", "opened").ending == "opened" && Next(Vault, "case", "returned").ending == "favour" && Next(Vault, "case", "lost").ending == "lostcase"
          && Next(Vault, "case", "declined").ending == "refusedcase", "opened, handed back for a favour, lost, or refused");
        var contents = Enumerable.Range(0, 100).Select(i => VaultFlow.Contents(i / 100f)).ToList();
        C(contents.Count(c => c == "sleeper") == 35 && contents.Count(c => c == "machine") == 30 && contents.Count(c => c == "goods") == 35, "a sleeper, a machine or old goods");
        C(VaultFlow.Components(1) == 6 && VaultFlow.Components(20) == 12 && VaultFlow.Pay(10, 32f) == 450 && VaultFlow.PartPay(5, 32f) == 160, "components by the colony's size; two fifths over their worth");
        C(VaultFlow.EngineerOutcome(5, 0) == "worked" && VaultFlow.EngineerOutcome(4, 1) == "short" && VaultFlow.ComponentsPerDay == 1, "a component for each day's work");
        C(VaultFlow.GoodsWorth(1f) == 400f && VaultFlow.GoodsWorth(0.1f) == 250f && VaultFlow.GoodsWorth(5f) == 700f, "old goods by the colony's wealth");
        C(VaultFlow.OpenedGoodwill < 0 && VaultFlow.ReturnedGoodwill > 0 && VaultFlow.FavourWeight >= 10, "opened, they resent it; handed back, they owe a favour (big enough to be one)");

        // ================= S12, the lost founder's grave =================

        const string Grave = GraveFlow.Key;
        C(StorylineMath.Parts(Grave).SequenceEqual(new[] { "search", "mourners" }), "the grave: the search, the mourners");
        C(Next(Grave, "search", "found").part == "mourners" && Next(Grave, "search", "theyfound").part == "mourners" && Next(Grave, "search", "builtover").ending == "builtover"
          && Next(Grave, "search", "declined").ending == "declined", "found by the colony or by theirs: the mourners come; built over: held against the colony");
        C(Next(Grave, "mourners", "hosted").ending == "kept" && Next(Grave, "mourners", "poorly").ending == "kept" && Next(Grave, "mourners", "declined").ending == "refused"
          && Next(Grave, "mourners", "builtover").ending == "builtover" && Next(Grave, "mourners", "harmed").ending == "harmed", "kept, refused, built over");
        C(GraveFlow.Covers(2, 2) && !GraveFlow.Covers(3, 2) && GraveFlow.AreaRadius == HomeQuestsMath.AreaRadius && GraveFlow.LookRadius == HomeQuestsMath.LookRadius,
            "the search is the lost keepsake's: the same area and the same look");
        C(Enumerable.Range(0, 100).Select(i => GraveFlow.Mourners(i / 100f)).All(x => x >= 3 && x <= 5), "three to five mourners");
        C(GraveFlow.Years == 3 && GraveFlow.YearDays == 60f && GraveFlow.BuiltOverGoodwill < GraveFlow.RefusedGoodwill && GraveFlow.YearGoodwill > 0, "each year for three years; built over is the worse slight");

        // ================= Each played out =================

        StorylineTests.PlayOut(C, Dream, new[] { ("place", "built", 6f), ("watch", "seen", 2f), ("offering", "given", 2f) }, "given", 0.5f, 12f, 20f);
        StorylineTests.PlayOut(C, Dream, new[] { ("place", "built", 6f), ("watch", "unwatched", 3f), ("offering", "declined", 0f) }, "refused", 0.5f, 10f, 18f);
        StorylineTests.PlayOut(C, Raid, new[] { ("defences", "ready", 6f), ("raid", "repelled", 2f), ("strike", "cleared", 5f) }, "campgone", 0.5f, 15f, 25f);
        StorylineTests.PlayOut(C, Raid, new[] { ("defences", "partly", 6f), ("raid", "overrun", 2f), ("tribute", "paid", 1f), ("tribute", "declined", 0f), ("tribute", "paid", 1f), ("tribute", "paid", 1f) },
            "movedon", 0.5f, 55f, 70f);
        StorylineTests.PlayOut(C, Well, new[] { ("water", "sent", 5f), ("digger", "found", 6f) }, "grew", 0.5f, 13f, 20f);
        StorylineTests.PlayOut(C, Well, new[] { ("water", "partly", 5f), ("digger", "notfound", 6f), ("refugees", "takenin", 1f) }, "takenin", 0.5f, 17f, 25f);
        StorylineTests.PlayOut(C, Vault, new[] { ("cells", "sent", 7f), ("engineer", "worked", 5f), ("case", "returned", 6f) }, "favour", 0.5f, 22f, 30f);
        StorylineTests.PlayOut(C, Vault, new[] { ("cells", "sent", 7f), ("engineer", "short", 5f), ("case", "opened", 2f) }, "opened", 0.5f, 18f, 25f);
        StorylineTests.PlayOut(C, Grave, new[] { ("search", "found", 3f), ("mourners", "hosted", 2f) }, "kept", 0.5f, 8f, 12f);

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        var incidents = defs.SelectMany(d => d.Root!.Elements("IncidentDef")).ToList();
        var things = defs.SelectMany(d => d.Root!.Elements("ThingDef")).ToList();
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil." + cls);
        var dream = Code("Storylines/StorylineDream.cs");
        var raiders = Code("Storylines/StorylineRaiders.cs");
        var well = Code("Storylines/StorylineWell.cs");
        var vault = Code("Storylines/StorylineVault.cs");
        var grave = Code("Storylines/StorylineGrave.cs");
        var shared = Code("Storylines/StorylineRound3.cs");

        // The card.
        var card = incidents.FirstOrDefault(i => (string?)i.Element("defName") == "Rimfolk_Storyline_VaultCard");
        C(card != null && (string?)Ext(card, "StorylineCard")?.Element("story") == Vault && (string?)card.Element("baseChance") == "0"
          && (string?)Ext(card, "HappeningExtension")?.Element("defaultOption") == "0", "the case's card: a card of the vault, kept sealed if nobody answers");
        var choices = card?.Descendants("choice").Select(c => c.Value).ToList() ?? new List<string>();
        C(choices.SequenceEqual(new[] { "keep", "open" }) && vault.Contains("Card = \"Rimfolk_Storyline_VaultCard\"") && vault.Contains("if (option != 1)"), "keep it sealed (0) or open it (1), as the code reads them");

        // The gods are unseen: the dream is the colony's own, the god named only as the colony knows it, nothing of its mind.
        C(dream.Contains("public override bool OfTheColony => true;") && dream.Contains("public override bool NeedsGiver(Storyline s) => false;"), "the dream's parts are the colony's own: no people of the world asked them");
        C(Code("Missions/Missions.cs").Contains("bool own = w?.OfTheColony ?? false;") && Code("Missions/Missions.cs").Contains("!(Worker(m)?.OfTheColony ?? false) && !LivingEvents.Friendly(m.faction)"),
            "a mission of the colony's own needs no people at peace with it (a small change to the shared missions)");
        C(dream.Contains("Worship.Called(g, ") && dream.Contains("Gods.Known(g)") && !Regex.IsMatch(dream, @"Gods\.Name\(|FavourValue|Gods\.Favour\(|AddFavour"),
            "the god is one the colony knows, named by the colony's own name for it, never by its favour");
        C(dream.Contains("Worship.Sign(g, map, place.Position, PresenceKind.Act") && dream.Contains("GodNearMath.Plain("), "what happens at the place is the god's own sign, named only as the colony may (Worship.Sign)");
        C(dream.Contains("GodLore.Believes(p, g, words)") && dream.Contains("GodLore.Dreamt(p, g, words)") && dream.Contains("b.demands.Add(new DemandRecord"),
            "the dream as told, and the offering given as the dreamer believes it, go into the colony's book; the offering is the worship's own demand of a dream");
        C(dream.Contains("Shrines.Is(b)") && dream.Contains("Shrines.Ask(t, g)"), "the place is an altar, a shrine or a work of art, given to the god by the worship's own dedication");
        var dreamWords = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Storyline_Dream.xml")).Descendants().Where(e => !e.HasElements).Select(e => e.Value).ToList();
        dreamWords.AddRange(XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Storylines3.xml")).Root!.Elements().Where(e => e.Name.LocalName.StartsWith("CC_Story3_Dream")).Select(e => e.Value));
        C(dreamWords.Count > 20 && dreamWords.All(w => !Regex.IsMatch(w, @"\{(NAME|GOD)\} (is|was|seems|felt) (pleased|glad|angry|satisfied)|\{(NAME|GOD)\} wants|favour", RegexOptions.IgnoreCase)),
            "nothing in the dream's words says what the god thinks, wants or feels");
        C(dreamWords.Where(w => Regex.IsMatch(w, @"\{NAME\} (asked|showed)")).All(w => Regex.IsMatch(w, @"\bsays\b")) && dreamWords.Any(w => w.Contains("believes {NAME}")),
            "what the god asked is the dreamer's own account, as they say it and believe it");
        var thoughts = defs.SelectMany(d => d.Root!.Elements("ThoughtDef")).ToList();
        C(new[] { "Rimfolk_Storyline_DreamHonoured", "Rimfolk_Storyline_DreamTroubled" }.All(t => thoughts.Any(x => (string?)x.Element("defName") == t && dream.Contains(t))),
            "the dreamer honoured, or troubled for a while");

        // Raiders: the game's own raid, within the shared harm budget; the camp within three tiles; the price carried out.
        C(raiders.Contains("Harm.Roll(HarmKinds.StoryFight, RaidersFlow.RaidChance)") && raiders.Contains("IncidentWorker_Happening.FightsPossible") && raiders.Contains("Raids.Send(map, s.other, points"),
            "the raids are the game's own, within the shared harm budget, only with battles on");
        C(HarmKinds.All.Contains(HarmKinds.StoryFight) && HarmKinds.StoryFight.system == "quests" && !HarmKinds.StoryFight.founding, "a storyline's fight is a kind of the shared harm budget");
        C(raiders.Contains("TileFinder.TryFindNewSiteTile(out tile, map.Tile, 2, RaidersFlow.NearTiles") && raiders.Contains("SitePartDefOf.BanditCamp"), "the strike's camp is the game's bandit camp, only within three tiles");
        C(raiders.Contains("new LordJob_AssistColony(s.giver") && raiders.Contains("RaidersCamps.KindWord(r)") && raiders.Contains("RaidersCamps.CampOf(map) != null"),
            "the neighbours' fighters go after the raiders beside the colony's; the camp is the region's kind of camp, never beside one of the region's");
        C(raiders.Contains("new DilemmaEffect_RaidersCampPrice") && raiders.Contains("public override bool OfTheColony => true;"), "their price is carried out to the edge of the colony's land, as the region's camp's is");

        // The well: the lent hand walks out and is kept in the world, never lost by a part that ended.
        C(well.Contains("Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever)") && well.Contains("Find.WorldPawns.ForcefullyKeptPawns.Add(p)") && well.Contains("GenSpawn.Spawn(p, edge, map)"),
            "the lent hand is kept in the world while away, and walks home from the edge");
        C(well.Contains("if (m.a != null && WellRules.Away(m.a)) WellRules.Home(m.a, m.Map, 0f);") && well.Contains("public override void Gone(Storyline s, Map map)"),
            "never a colonist left away: a part that ends or people gone bring them home at once");
        C(!Regex.IsMatch(well, @"CaravanMaker|ExitMapAndCreateCaravan|FormCaravan"), "no caravan of the colony's is sent anywhere for the well");
        C(well.Contains("\"DBH_WaterBottle\"") && well.Contains("Arrivals.Join(p, map, gate)") && well.Contains("PeoplesMath.Drillers"), "water where the colony keeps Dubs Bad Hygiene's, the Deepwell Union first, the refugees join");
        var drillers = File.ReadAllText(Path.Combine(root, "docs", "player-mods", "items.tsv"));
        C(Regex.IsMatch(drillers, @"^DBH_WaterBottle\t\S+\tyes\t", RegexOptions.Multiline), "Dubs Bad Hygiene's water is in the player's list");

        // The vault: the engineer at the colony's bench, the case Rimfolk's own, the machine within the budget.
        C(vault.Contains("Missions.Take(map, ThingDefOf.ComponentIndustrial.defName, VaultFlow.ComponentsPerDay)") && vault.Contains("OfType<Building_WorkTable>()"),
            "a component from the stores for each day's work at one of the colony's benches");
        C(vault.Contains("Harm.Roll(HarmKinds.StoryFight, 1f)") && vault.Contains("Faction.OfMechanoids") && vault.Contains("if (what == \"machine\" && !Machine(m, s, map, at)) what = \"goods\";"),
            "the machine is the game's own, within the harm budget; held back, old goods instead");
        C(vault.Contains("FactionMemory.Note(s.giver, \"mission\", Storylines.Label(s), VaultFlow.FavourWeight)") && vault.Contains("VaultSupply.Noted("), "handed back, a favour owed; the parts keep the vault's age");
        foreach (var (def, png) in new[] { ("Rimfolk_VaultCase", "VaultCase"), ("Rimfolk_FounderGrave", "FounderGrave") })
            C(things.Any(t => (string?)t.Element("defName") == def) && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", png + ".png")), def + " is Rimfolk's own, with its picture");

        // The grave: the keepsake's search, its marker, the yearly visits.
        C(shared.Contains("class FloatMenuOptionProvider_StorylineSearch : FloatMenuOptionProvider") && shared.Contains("class StorylineAreaOverlay : MapComponent")
          && grave.Contains("IStorylineSearch") && defs.SelectMany(d => d.Root!.Elements("JobDef")).Any(j => (string?)j.Element("defName") == "Rimfolk_Storyline_Search"),
            "the grave is searched for as the keepsake is: right-click inside the area drawn on the map");
        C(grave.Contains("kind = \"grave\"") && grave.Contains("public override bool After(StorylineAfter a, int now)") && grave.Contains("BuiltOn(map, marker.Position, marker)"),
            "kept, they come each year; built over, they find it so and come no more");

        // Away only within three tiles (rule 2), and the colony's people never sent off by the others.
        foreach (var (name, code) in new[] { ("dream", dream), ("well", well), ("vault", vault), ("grave", grave) })
            C(!Regex.IsMatch(code, @"SiteMaker|TileFinder|CaravanMaker"), name + ": everything on the colony's own map");

        // Dev mode.
        C(new[] { "start a dream of the gods now", "start raiders settling nearby now", "start the town's well ran dry now", "start the vault's sealed case now",
            "start the lost founder's grave now", "what the open part waits for comes now" }.All(a => shared.Contains("Storylines: " + a)), "the dev actions to start each, and what each part waits for now");
        foreach (var (name, code) in new[] { ("dream", dream), ("raiders", raiders), ("well", well), ("vault", vault), ("grave", grave) })
            C(Regex.Matches(code, @"IStorylineDebugNow").Count >= 2, name + ": its parts' moments can be brought on now");

        // Spoken lines: one sentence each, plain.
        foreach (var name in new[] { "Dream", "Raiders", "Well", "Vault", "Grave" })
        {
            var doc = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Storyline_" + name + ".xml"));
            foreach (var line in doc.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")))
                C(Regex.Matches(line, @"[.?]").Count == 1 && (line.EndsWith(".") || line.EndsWith("?")), name + ": a spoken line is one sentence (" + line + ")");
            // Every line spoken by a part is on that part's own event (the code says it by its tag).
            foreach (var d in doc.Root!.Elements("IncidentDef"))
            {
                var tags = Ext(d, "HappeningExtension")?.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]*)\]").Groups[1].Value).ToList() ?? new List<string>();
                var worker = ((string?)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
                var code = new[] { dream, raiders, well, vault, grave }.FirstOrDefault(c => c.Contains("class " + worker + " ")) ?? "";
                var body = Regex.Match(code, @"class " + worker + @" [\s\S]*?(?=\n    public class |\n    // =====|\z)").Value;
                foreach (Match said in Regex.Matches(body, @"Say\([^;]*?, m, ""(\w+)""\)"))
                    C(tags.Contains(said.Groups[1].Value), name + " " + worker + ": the line it says (" + said.Groups[1].Value + ") is on its own event");
            }
        }
        return n;
    }
}
