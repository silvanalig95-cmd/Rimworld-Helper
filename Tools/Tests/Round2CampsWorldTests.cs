using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round's camps and world events (docs/content-update-proposals-2.md, D33 to D44; Camps/CampRound2.cs,
/// World/Variety/WorldRound2.cs, Defs/CampDefs/Rimfolk_CampKinds_Round2.xml, Defs/WorldSituationDefs/Rimfolk_WS_Round2.xml):
/// the pure rules (Round2Math), and the defs, words and seams of each.
/// </summary>
public static class Round2CampsWorldTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 2 camps and world events: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement Def(string type, string name) =>
            defs.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        List<string> Lines(XElement e, string list) => e?.Element(list)?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        string camps = Code("Camps/CampRound2.cs"), world = Code("World/Variety/WorldRound2.cs"), debug = Code("World/Variety/Round2Debug.cs");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round2.xml"));

        // ================= The pure rules =================

        C(Round2Math.Share(true, 0.1f) == Round2Math.DigFind.Sealed && Round2Math.Share(true, 0.5f) == Round2Math.DigFind.Relic && Round2Math.Share(false, 0.5f) == Round2Math.DigFind.Worn
          && Round2Math.Share(true, 0.9f) == Round2Math.DigFind.Worn, "D33: the share is now and then sealed, else a relic where the mod is loaded, else an old worn thing");
        C(Round2Math.ShareCount(true) == 2 && Round2Math.ShareCount(false) == 1, "D33: two things for the share when they dug the graves too");
        C(Round2Math.Caught(10f, true) && !Round2Math.Caught(10f, false) && !Round2Math.Caught(Round2Math.SeenRange + 1f, true), "D33: caught only by someone awake and near");
        C(Round2Math.CaughtTension(10f) == 70f && Round2Math.CaughtTension(80f) == 110f, "D33: caught at the graves, their unease is at the brawl's mark at least");
        var corner = Round2Math.FarCorner(200, 200, 40, 40, 20);
        C(corner == (179, 179) && Round2Math.FarCorner(200, 200, 180, 30, 20) == (20, 179), "D34, D36: the corner farthest from the colony, inset from the edge");
        C(Round2Math.FluChanceMet > 0f && Round2Math.FluChanceMet < 1f && Round2Math.FluChanceTended < Round2Math.FluChanceMet, "D34: the herds' meeting carries the cough at a chance, tending a little less");
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
            C(Round2Math.StoneFor(age).Length >= 2 && Round2Math.StoneFor(age).Last() == "SculptureLarge", $"D35: the {age} stone has its own form and the game's sculpture after it");
        C(Round2Math.StoneFor("tribal")[0] == "TotemicSlab_Medium" && Round2Math.StoneFor("medieval")[0] == "AM_Megalith" && Round2Math.StoneFor("industrial")[0] == "VFE_Obelisk"
          && Round2Math.StoneFor("spacer")[0] == "AM_FuturisticObelisk", "D35: a totemic slab, a megalith, an obelisk, a futuristic obelisk");
        C(Enumerable.Range(0, 11).Select(i => Round2Math.RaiseDays(i / 10f)).All(d => d >= 2 && d <= 4), "D35: two to four days to raise it");
        C(Round2Math.NightHour(22) && Round2Math.NightHour(1) && !Round2Math.NightHour(12) && !Round2Math.NightHour(5), "D36: they sell by the lamp from dusk to the small hours");
        C(Round2Math.WalkOutSilver(2f) == 10 && Round2Math.WalkOutSilver(20f) == 30 && Round2Math.WalkOutSilver(33f) % 10 == 0, "D36: a walk out costs a bottle's worth and a little more, in tens");
        C(Round2Math.Named(new List<(float, bool, bool)> { (900f, false, false), (100f, true, false), (50f, false, true) }) == 2
          && Round2Math.Named(new List<(float, bool, bool)> { (900f, false, false), (400f, false, false) }) == 0 && Round2Math.Named(new List<(float, bool, bool)>()) == -1,
            "D38: a god's beast first, then a sacred kind, then the one worth most");
        C(!Round2Math.MayStray(20f, 0f) && Round2Math.MayStray(Round2Math.StrayTension, 0f) && !Round2Math.MayStray(90f, 0.99f), "D38: one may go after it anyway only while the camp's unease runs high");
        C(Round2Math.HuntGift(0f) == 60 && Round2Math.HuntGift(10000f) == 400 && Round2Math.HuntGift(1200f) == 300, "D38: the lord's gift by the beast's worth, 60 to 400");
        C(Round2Math.BadCoin(0) == 0 && Round2Math.BadCoin(100) == 10 && Round2Math.BadCoin(100000) == 150 && Round2Math.ChangerFee(50) == 20 && Round2Math.ChangerFee(100000) == 60,
            "D42: the bad coin found and the changer's fee, in tens and bounded");
        C(Round2Math.SuppressionDip > 0f && Round2Math.SuppressionDip < 0.3f, "D43: the colony's slaves' suppression falls a little");
        C(Round2Math.FirstStronger(3, 2) && Round2Math.FirstStronger(2, 2) && !Round2Math.FirstStronger(1, 2) && Round2Math.Toll(30) == 20 && Round2Math.Toll(100) == 50,
            "D44: the stronger by its towns, and a small toll each");

        // ================= The camps =================

        var kinds = new Dictionary<string, string>
        {
            ["Rimfolk_Camp_Diggers"] = "CampWorker_Diggers", ["Rimfolk_Camp_HerdApart"] = "CampWorker_HerdApart", ["Rimfolk_Camp_StoneRaisers"] = "CampWorker_StoneRaisers",
            ["Rimfolk_Camp_Smugglers"] = "CampWorker_Smugglers", ["Rimfolk_Camp_Novices"] = "CampWorker_Novices", ["Rimfolk_Camp_LordsHunt"] = "CampWorker_LordsHunt",
            ["Rimfolk_Camp_PutAwayPriest"] = "CampWorker_PutAwayPriest"
        };
        foreach (var (k, worker) in kinds)
        {
            var d = Def("CampKindDef", k);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && (camps + world).Contains("class " + worker + " : CampWorker"), $"{k} is a camp kind with ways of its own ({worker})");
            if (d == null) continue;
            C(Lines(d, "texts").Count >= 1 && Lines(d, "lines").Count >= 5 && Lines(d, "farewell").Count >= 2 && Lines(d, "settled").Count >= 1, $"{k} has its words");
            foreach (var l in Lines(d, "lines").Concat(Lines(d, "momentLines")))
                C(Regex.Matches(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"[.?!]\s+\S").Count == 0, $"{k}'s bubble is one sentence: {l}");
            if (k != "Rimfolk_Camp_PutAwayPriest")
            {
                C(Lines(d, "texts").Count >= 4, $"{k} has texts for its peoples' ages");
                // The arrival letter is written before the camp's own placeholders are known: none of them there.
                C(!Lines(d, "texts").Concat(Lines(d, "titles")).Any(l => Regex.IsMatch(l, @"\{(BEAST|GOD|ITEM|SITE|GOODS|OTHERPEOPLE)\}")), $"{k}'s arrival letter uses only the camps' common words");
                C(Debug(debug, k) || Code("Camps/CampRound2.cs").Contains("\"" + k + "\""), $"{k} can be brought from the dev action");
            }
        }
        C(debug.Contains("CampRound2.Kinds") && CampRound2KindsListed(camps, kinds.Keys.Where(k => k != "Rimfolk_Camp_PutAwayPriest")), "the dev action brings each of the round's camps");
        // D33.
        var dig = Def("CampKindDef", "Rimfolk_Camp_Diggers");
        C((string)dig?.Element("needs") == "ruins" && dig.Element("asks")?.Elements("li").Any(a => (string)a.Element("key") == "graves" && (string)a.Element("theyAsk") == "true") == true,
            "D33: diggers pitch by the ruins and ask about the old graves");
        C(camps.Contains("VIE_RelicInertBone") && camps.Contains("OwnMurmur.OldThing()") && camps.Contains("ThingRole.Precious") && camps.Contains("BringFound(t)"),
            "D33: the share is a relic of Relics and Artifacts, an old worn thing, or something precious, carried to the gate");
        C(camps.Contains("Gods.ByKey(\"Mog\")") && Def("ThoughtDef", "Rimfolk_R2_GravesDug") != null && camps.Contains("Round2Math.CaughtTension"),
            "D33: the keeper of the dead's believers uneasy; caught at the graves, the camp at the brawl's mark");
        // D34.
        var herd = Def("CampKindDef", "Rimfolk_Camp_HerdApart");
        C(Lines(herd, "situationKinds").Contains("Council_WS_Murrain") && camps.Contains("MurrainRuns()") && camps.Contains("\"Disease_AnimalFlu\""),
            "D34: only while the murrain runs; the game's own animal flu when the herds meet");
        C(Def("JobDef", "Rimfolk_R2_TendTheirHerd") != null && herd?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_TendTheirHerd") == true,
            "D34: the colony's best with animals may tend their beasts");
        // D35.
        C(camps.Contains("CampWork.BuildStep(c, p, t, c.Goods(), out _)") && camps.Contains("GenConstruct.PlaceBlueprintForBuild") && camps.Contains("SetFaction(Faction.OfPlayer)"),
            "D35: a buildable stone rises as their own frame, and is left to the colony");
        C(camps.Contains("GodLore.Heard(c.leader, g,") && camps.Contains("Gods.Rivals(g).Contains(patron)") && Def("ThoughtDef", "Rimfolk_R2_RivalStone") != null,
            "D35: the book hears their name for the god; the rival's believers grumble");
        var stone = Def("CampKindDef", "Rimfolk_Camp_StoneRaisers");
        C(Lines(stone, "momentLines").Count(l => Tags(l).Contains("rite")) >= 2, "D35: their rite's words, in the old voice");
        // D36.
        var smug = Def("CampKindDef", "Rimfolk_Camp_Smugglers");
        C(new[] { "law", "embargo", "scarcity" }.All(Lines(smug, "situationKinds").Contains) && camps.Contains("public override bool FitsMap(Map map) => CampWords.Situation(def) != null;"),
            "D36: only while a ban or a shortage runs");
        C(camps.Contains("wantsToTradeWithColony = Round2Math.NightHour(hour)") && camps.Contains("RevelGoods.Craving(p).craving") && camps.Contains("Gods.ByKey(\"Revel\")"),
            "D36: their trader deals only at night; one with a craving or a believer of the cup may walk out");
        C(camps.Contains("c.tension = Mathf.Max(c.tension, 60f)"), "D36: told on, they aren't remembered to come back (CampStoryMath.Remembered)");
        // D37.
        C(camps.Contains("GodTales.Tell(colonist, novice") && camps.Contains("GodTales.Tell(novice, colonist") && camps.Contains("p.DevelopmentalStage.Juvenile() && !st.heard.Contains(p)"),
            "D37: tales told both ways at their fire, and to the colony's young");
        C(Def("CampKindDef", "Rimfolk_Camp_Novices")?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Visit") == true,
            "D37: the one the colony sends walks to their fire");
        // D38.
        var hunt = Def("CampKindDef", "Rimfolk_Camp_LordsHunt");
        C((string)hunt?.Element("armedHunters") == "true" && camps.Contains("SacredKinds.AnySacred(a.def)") && camps.Contains("JobDefOf.Hunt"), "D38: armed, they name the beast, a sacred kind first, and hunt it by the game's own hunting");
        C(camps.Contains("Round2Math.MayStray(c.tension, Rand.Value)"), "D38: told to spare it, one may go after it anyway at high unease");

        // ================= The world events =================

        var events = new Dictionary<string, string>
        {
            ["Rimfolk_WS_GodPutAway"] = "SituationWorker_GodPutAway", ["Rimfolk_WS_DancingSickness"] = "SituationWorker_DancingSickness",
            ["Rimfolk_WS_RestlessDead"] = "SituationWorker_RestlessDead", ["Rimfolk_WS_BadCoin"] = "SituationWorker_BadCoin", ["Rimfolk_WS_Rising"] = "SituationWorker_Rising",
            ["Rimfolk_WS_TwoClaims"] = "SituationWorker_TwoClaims"
        };
        var kindsSeen = new HashSet<string>();
        foreach (var (e, worker) in events)
        {
            var d = Def("WorldSituationDef", e);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && world.Contains("class " + worker + " : SituationWorker_Variety"),
                $"{e} is a world event of its people's age ({worker})");
            if (d == null) continue;
            C(Lines(d, "texts").Count >= 2 && Lines(d, "titles").Count >= 2 && Lines(d, "summaries").Count >= 1 && d.Element("endings")?.Elements("li").Any() == true,
                $"{e} has its letters, titles, what it means and an ending");
            C(kindsSeen.Add((string)d.Element("kind")), $"{e} has a kind of its own");
            var names = Lines(d, "names");
            foreach (var t in new[] { "theirTribal", "theirMedieval", "theirIndustrial", "theirSpacer" })
            {
                C(names.Any(l => Tags(l).Contains(t)), $"{e} has a name in the {t} age's form");
                C(Lines(d, "texts").Any(l => Tags(l).Contains(t)), $"{e} has a letter in the {t} age's form");
            }
            foreach (var l in Lines(d, "theirs").Concat(Lines(d, "theirEnds")))
                C(WorldHomeMath.ForAPeople(Tags(l)), $"{e}'s own paragraph names a people: {l.Substring(0, Math.Min(40, l.Length))}");
            C(debug.Contains("\"" + e + "\""), $"{e} can be started from the dev action");
        }
        // D39.
        C(world.Contains("Gods.Of(f).Count >= 2") && world.Contains("GodLore.Heard(") && world.Contains("\"Letter\"") && world.Contains("Rimfolk_R2_GodPutAway"),
            "D39: a people of several gods; the book keeps it as told in a letter; the god's believers uneasy");
        C(world.Contains("tracker.Make(map, center, CampSite.ColonyCenter(map), kind,") && world.Contains("new List<Pawn> { priest }") && Def("CampKindDef", "Rimfolk_Camp_PutAwayPriest") != null,
            "D39: the priest at the gate is the one who camps a season");
        // D40.
        C(Def("JobDef", "Rimfolk_R2_DanceInYard") != null && world.Contains("VisitorMusic.JobFor(p, trip)") && world.Contains("Gods.ByKey(\"Revel\")") && world.Contains("Gods.ByKey(\"Hush\")"),
            "D40: they dance at the gate, one at the colony's instrument; the cup's believers join, the quiet's ask for quiet");
        C(Def("HediffDef", "Rimfolk_R2_DancingFever") != null && world.Contains("Harm.Roll(HarmKinds.GateSickness, Round2Math.FeverChance)") && world.Contains("IllnessBudget.Allows(map, def, who)"),
            "D40: the day's fever through the shared harm and illness budgets");
        // D41.
        C(world.Contains("Harm.Roll(HarmKinds.Uncanny, Round2Math.RiseChance)") && world.Contains("ModsConfig.AnomalyActive") && world.Contains("DesignationDefOf.Deconstruct")
          && world.Contains("\"CremateCorpse\""), "D41: the dead left, a shambler at the shared budget's roll with Anomaly; burned by the game's own jobs");
        // D42.
        C(RegionPricesMath.Goods("Rimfolk_WS_BadCoin", out bool coinDear) == RegionGoods.Steel && coinDear, "D42: good metal bought dear at their towns");
        // D43.
        C(world.Contains("Need_Suppression") && world.Contains("TryGenerateNewLeader") && world.Contains("TraitDefOf.Kind"), "D43: the colony's slaves hear of it; the town freed; the kind-hearted grieve a hand-back");
        // D44.
        var claims = Def("WorldSituationDef", "Rimfolk_WS_TwoClaims");
        C(claims?.Element("second") != null && claims.Descendants("li").Count(li => (string)li.Attribute("Class") == "ColonyCouncil.VisitEffect_AtGate") == 2
          && claims.Element("beats").Elements("li").Count(b => Lines(b, "requires").Contains("own")) == 2, "D44: both envoys at the gate; told it is the colony's own, the stronger walks the land's edge");

        // ================= The words =================

        foreach (Match m in Regex.Matches(camps + world, "\"(CC_R2[A-Za-z_]*[A-Za-z])\""))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value} has its words");
        foreach (var place in new[] { "tree", "water", "open", "edge" }) C(keyed.Contains("<CC_R2Stone_Place_" + place + ">"), $"D35: where the stone stands in words ({place})");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("World/Variety/Round2Math.cs"), "the pure rules are linked into the tests");
        return n;
    }

    private static bool Debug(string debug, string kind) => debug.Contains("\"" + kind + "\"");

    private static bool CampRound2KindsListed(string camps, IEnumerable<string> kinds) => kinds.All(k => camps.Contains("\"" + k + "\""));
}
