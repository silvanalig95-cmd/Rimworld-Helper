using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The third round of variety (the player's request, 6 November 2026: "new additional dilemmas, events, world events,
/// camps... for further gameplay variety"): six world events across the region (a war between neighbours, a year without
/// a summer, a great work, a ban on drink, a great caravan, households on the move) and five camp kinds (stonecutters,
/// shearers, beast-catchers, a travelling teacher, wounded fighters). The camps' arithmetic without game types
/// (CampLandMath), and the defs: every age's forms, questions along the way, endings decided by what happened, the
/// towns' fortunes moved by how it ended, nothing that adds world events or camps a year, the camps' land needs, their
/// own work, the stay's shape and nothing that marks the calendar.
/// </summary>
public static class VarietyThreeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "variety three: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Def(string file, string element, string name) =>
            XDocument.Load(Path.Combine(root, file)).Root!.Elements(element).FirstOrDefault(e => (string)e.Element("defName") == name);
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);

        // ================= The camps' arithmetic =================

        C(!CampLandMath.StoneEnough(CampLandMath.MinChunks - 1) && CampLandMath.StoneEnough(CampLandMath.MinChunks), "stonecutters come only for a real scatter of rock");
        C(CampLandMath.CutsADay(0) == 0 && CampLandMath.CutsADay(1) == 1 && CampLandMath.CutsADay(4) == 3 && CampLandMath.CutsADay(5) == 3,
            "a chunk a day and one more for every two hands");
        C(CampLandMath.MinStoneSale > 0 && CampLandMath.MinStoneSale < CampLandMath.StoneLoad, "a load of stone is worth carrying over, and has a limit");
        C(!CampLandMath.FleeceEnough(CampLandMath.MinFleece - 1) && CampLandMath.FleeceEnough(CampLandMath.MinFleece), "shearers come only to a flock worth their blades");
        C(CampLandMath.FleeceReady > 0f && CampLandMath.FleeceReady <= 1f, "wool half grown or so is ready");
        float carry = 0f;
        int share = 0;
        for (int i = 0; i < 20; i++) share += CampLandMath.Share(1, CampLandMath.FleeceShare, ref carry);
        C(share == 2, "one fleece in ten, even a fleece at a time");
        C(!CampLandMath.HerdEnough(CampLandMath.MinHerd - 1) && CampLandMath.HerdEnough(CampLandMath.MinHerd) && CampLandMath.MaxCaught < CampLandMath.MinHerd,
            "beast-catchers come for a herd, and never take all of it");
        C(CampLandMath.MaxWildness < 1f, "only a beast that could be tamed is roped");
        C(CampLandMath.CaughtPrice(300f) == 240 && CampLandMath.CaughtPrice(0f) == 0 && CampLandMath.CaughtPrice(4f) == 10, "a beast they caught at four fifths of its worth, in tens");
        C(CampLandMath.WoundMin > 0f && CampLandMath.WoundMax <= 8f && CampLandMath.WoundMin < CampLandMath.WoundMax, "a wounded fighter's cut is a limb's, never one that kills");

        // ================= The world events =================

        const string wsA = "Defs/WorldSituationDefs/Rimfolk_WS_Variety3.xml", wsB = "Defs/WorldSituationDefs/Rimfolk_WS_Variety3_Roads.xml";
        var events = new[] { (wsA, "Rimfolk_WS_NeighboursWar"), (wsA, "Rimfolk_WS_ColdYear"), (wsA, "Rimfolk_WS_GreatWork"),
            (wsB, "Rimfolk_WS_DryLaw"), (wsB, "Rimfolk_WS_GreatCaravan"), (wsB, "Rimfolk_WS_Trek") };
        foreach (var (file, name) in events)
        {
            var s = Def(file, "ColonyCouncil.WorldSituationDef", name);
            C(s != null, name + ": there");
            if (s == null) continue;
            var texts = Lis(s, "texts");
            C(texts.Count >= 3 && texts.Any(t => t.StartsWith("[theirLowtech]") || t.StartsWith("[lowtech]")) && texts.Any(t => !t.StartsWith("[")),
                $"{name}: forms for the peoples' ages, and one for any");
            C(Lis(s, "summaries").Count > 0, $"{name}: says what it means for the colony");
            var beats = s.Element("beats")!.Elements("li").ToList();
            C(beats.Count(b => b.Element("options") != null) >= 2, $"{name}: two questions or more along the way");
            var endings = s.Element("endings")!.Elements("li").ToList();
            C(endings.Count >= 2 && endings.Any(e => e.Element("requires") != null || e.Element("minHelp") != null) && endings.Last().Element("requires") == null && endings.Last().Element("minHelp") == null,
                $"{name}: ends by what happened, with a plain ending last");
            C(s.ToString().Contains("WorldEffect_Fortune") || s.Element("prosperity") != null, $"{name}: moves the towns' fortunes");
            // In the existing pools at their rates: nothing that brings another world event or a camp.
            C(!s.ToString().Contains("WorldEffect_Follow") && s.Element("after") == null && !s.ToString().Contains("WorldEffect_Camp") && !s.ToString().Contains("Council_Evt_Camp"),
                $"{name}: brings no other world event and no camp of its own");
            C(!s.ToString().Contains("needsBattles") && !s.ToString().Contains("RaidLater"), $"{name}: no fight of its own");
            var options = beats.SelectMany(b => b.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>()).ToList();
            C(options.Any(o => o.ToString().Contains("WorldEffect_Goodwill")) && options.Any(o => o.Element("effects") == null),
                $"{name}: answers that pull both ways, and one that keeps out of it");
            foreach (var b in beats.Where(b => b.Element("options") != null))
                C(b.Element("effects")?.ToString().Contains("VisitEffect_AtGate") == true || b.Element("options")!.Elements("li").Any(o => o.ToString().Contains("WorldEffect_Harvest")),
                    $"{name}/{(string)b.Element("key")}: who asks is at the gate, or the answer is on the colony's own fields");
        }
        var war = Def(wsA, "ColonyCouncil.WorldSituationDef", "Rimfolk_WS_NeighboursWar");
        C((string)war?.Element("second")?.Element("hostileToFirst") == "true" && (string)war?.Element("second")?.Element("friendly") == "true" && (string)war?.Element("faction")?.Element("friendly") == "true",
            "the war is between two of the colony's neighbours already at war in the game, both at peace with the colony");
        var warEnds = war?.Element("endings")?.Elements("li").ToList() ?? new List<XElement>();
        C(warEnds.Any(e => Lis(e, "requires").Contains("suppliedFaction") && Lis(e, "forbids").Contains("suppliedEnemy") && e.ToString().Contains("<enemy>true</enemy>"))
          && warEnds.Any(e => Lis(e, "requires").Contains("suppliedEnemy") && Lis(e, "forbids").Contains("suppliedFaction")),
            "the side the colony supplied is likelier to win, and the other's towns lose by it");
        C(Lis(war, "notWithMods").Any(m => m.Contains("factionalwar")), "never with Factional War, which has wars of its own");
        var cold = Def(wsA, "ColonyCouncil.WorldSituationDef", "Rimfolk_WS_ColdYear");
        C(cold?.Element("modifiers")?.Elements("li").Any(m => (string)m.Element("incident") == "ColdSnap" && float.Parse((string)m.Element("factor"), System.Globalization.CultureInfo.InvariantCulture) > 1f) == true
          && (string)cold?.Element("kind") == "weather" && !Lis(cold, "seasons").Contains("Winter"),
            "the cold year brings the game's cold snaps in spring and summer, and never runs with a hard winter");
        C(Lis(Def(wsB, "ColonyCouncil.WorldSituationDef", "Rimfolk_WS_DryLaw"), "texts").Any(t => t.StartsWith("[theirEmpire]"))
          && Lis(war, "texts").Any(t => t.StartsWith("[theirEmpire]")), "the Empire as itself, with its titles and honour");
        string effects = Code("World/SituationEffects.cs");
        C(effects.Contains("class WorldEffect_Fortune") && effects.Contains("RegionFortunes.Move("), "an ending moves the fortunes at once");
        string fortunes = Code("World/Region/RegionFortunes.cs");
        C(fortunes.Contains("public static void Move(") && fortunes.Contains("RegionBook.FortunesOn"), "and only with the towns' fortunes on");
        C(Code("World/WorldSituationWorker.cs").Contains("\"theirEmpire\"") && Code("World/WorldSituationWorker.cs").Contains("\"enemyEmpire\""), "world events' lines can ask for the Empire");
        C(keyed.ContainsKey("CC_Visit_Come_caravan") && keyed.ContainsKey("CC_Visit_Come_trek"), "the caravan and the households on the road are told as they come");

        // ================= The camps =================

        const string campFile = "Defs/CampDefs/Rimfolk_CampKinds_Variety3.xml";
        var kinds = new[] { "Rimfolk_Camp_Stonecutters", "Rimfolk_Camp_Shearers", "Rimfolk_Camp_Catchers", "Rimfolk_Camp_Teacher", "Rimfolk_Camp_Wounded" };
        var talk = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_CampFolk3.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        foreach (var name in kinds)
        {
            var k = Def(campFile, "ColonyCouncil.CampKindDef", name);
            C(k != null, name + ": there");
            if (k == null) continue;
            foreach (var part in new[] { "titles", "texts", "settled", "joinTexts", "farewell", "lines" })
                C(Lis(k, part).Count >= 2, $"{name}: its {part}");
            C(Lis(k, "texts").Count(t => t.StartsWith("[their:")) >= 2, $"{name}: in the form of its own people's age");
            C(k.Element("leaveBehind") == null && k.Element("farewellWalk") == null, $"{name}: leaves no gift that would mark the colony's calendar");
            var asks = k.Element("asks")!.Elements("li").ToList();
            C(asks.Count >= 3 && asks.Any(a => (string)a.Element("theyAsk") == "true"), $"{name}: the colony's asks and a request of their own");
            foreach (var a in asks.Where(a => (string)a.Element("theyAsk") == "true"))
                C(Lis(a, "askTexts").Count > 0 && Lis(a, "refused").Count > 0 && a.Element("refuseLabel") != null, $"{name}: their request is asked, and a no is told");
            C(asks.All(a => a.Element("effects")?.Elements("li").Any() == true && Lis(a, "outcomes").Count > 0), $"{name}: every ask does something, and says so");
            C(Lis(k, "help").Count > 0, $"{name}: a good turn of its own");
            float days = float.Parse(((string)k.Element("days")).Split('~')[1], System.Globalization.CultureInfo.InvariantCulture);
            C(days >= 3f, $"{name}: a stay long enough for trouble midway");
            string tag = FactsMath.CampTag(name);
            C(FactsMath.RegionTags.Contains(tag) && talk.Count(t => ((string)t.Element("when") ?? "").Split(',').Contains(tag)) >= 2, $"{name}: talk of its own, by its tag");
        }
        string land = Code("Camps/CampKindsLand.cs"), land3 = Code("Camps/CampKindsLand3.cs");
        foreach (var need in new[] { "stone", "fleece", "wildHerd", "children" })
            C(Regex.IsMatch(land, "case \"" + need + "\": return "), $"the land need {need}: whether they come");
        C(Regex.Matches(land, "case \"stone\":").Count == 2 && Regex.Matches(land, "case \"wildHerd\":").Count == 2, "stonecutters pitch by the rock, beast-catchers by the herd");
        foreach (var work in new[] { "cut", "shear", "catch" })
            C(land.Contains("case \"" + work + "\": return "), $"their own work {work}, by day");
        C(land.Contains("Rimfolk_CampCut) Cut(") && land.Contains("Rimfolk_CampShearFlock) Shorn(") && land.Contains("Rimfolk_CampCatch) Caught("), "their work done when the job is");
        C(land3.Contains("butcherProducts") && land3.Contains("Own(put)"), "a chunk dressed into the game's own blocks, theirs");
        C(land3.Contains("comp.Gathered(p)") && land3.Contains("SetForbidden(false, false)") && land3.Contains("CampLandMath.FleeceShare"), "the game's own shearing, the wool left for the colony but their fleece in ten");
        C(land3.Contains("beast.SetFaction(faction)") && land3.Contains("lord.AddPawn(beast)") && land3.Contains("TameUtility.CanTame(a)"), "a beast that can be tamed, roped and led away with them");
        C(land3.Contains("HediffDefOf.Cut") && land3.Contains("Tended(") && land3.Contains("Familiar"), "the wounded come with the game's own wounds, bandaged, the first time only");
        var shear = Def(campFile, "ColonyCouncil.CampKindDef", "Rimfolk_Camp_Shearers");
        C(shear?.Element("asks")?.Elements("li").Any(a => (string)a.Element("key") == "shear" && (string)a.Element("theyAsk") == "true") == true && land3.Contains("AskByKey(\"shear\")") && land3.Contains("Leave(\"declined\")"),
            "the shearers offer their work once the camp stands, and turned down they go on");
        var wounded = Def(campFile, "ColonyCouncil.CampKindDef", "Rimfolk_Camp_Wounded");
        C(wounded?.Element("wounded") != null && Lis(wounded, "situationKinds").Contains("war"), "the wounded come oftener while a war runs, and come hurt");
        var teacher = Def(campFile, "ColonyCouncil.CampKindDef", "Rimfolk_Camp_Teacher");
        C((string)teacher?.Element("needs") == "children" && teacher.ToString().Contains("DilemmaEffect_CampChildrenLesson") && land3.Contains("DilemmaEffect_Lesson"),
            "the teacher comes to a colony with children, and the lesson is at the teacher's side");
        foreach (var key in new[] { "CC_CampLand_Facts_cut", "CC_CampLand_Facts_shear", "CC_CampLand_Facts_catch", "CC_CampLand_Caught", "CC_CampLand_StoneSold" })
            C(keyed.ContainsKey(key), $"{key}: its words");
        return n;
    }
}
