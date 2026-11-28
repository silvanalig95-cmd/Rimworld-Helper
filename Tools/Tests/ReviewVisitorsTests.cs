using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of the visitors and strangers (docs/review-visitors.md, Tools/visitors_impact_audit.py): the bugs found, each
/// kept fixed, the nuisances given the effect their lines already tell, and what was added in the frames there are. A pet's
/// farewell is told as it died; the fallen raider's plea is asked only of one a few steps away, and "end it" is done there,
/// walked to if need be; nobody in a break or away is sent to capture or to kill; an animal given to a visitor goes with
/// them or off the map, never left wandering; food carried out to a visitor never takes anyone from tending, a rescue or the
/// player's order; a visitor's game with the colony's dog lets it be the moment it is wanted; the trainer told "just a
/// story" goes on now. A bad night costs rest and a shoddy garment wears; two more nuisances, more words for the pets, and
/// three more offers from the callers at the gate.
/// </summary>
public static class ReviewVisitorsTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the visitors and strangers: " + what);
        }

        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }
        var docs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (file: f, doc: XDocument.Load(f))).ToList();
        XElement Def(string type, string name) =>
            docs.SelectMany(d => d.doc.Root!.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(XElement e, string name) =>
            e?.Element(name) == null ? 0f : float.Parse(e.Element(name)!.Value, System.Globalization.CultureInfo.InvariantCulture);
        bool Plain(string s) => !s.Contains('—') && !s.Contains('!') && !Regex.IsMatch(s, @"\b(in|within) (\d+|a|two|three) (days?|hours?)\b");
        string Strip(string s, out string[] tags)
        {
            var m = Regex.Match(s ?? "", @"^\[([^\]]*)\]\s*");
            tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToArray() : new string[0];
            return m.Success ? s!.Substring(m.Length) : s ?? "";
        }
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(f).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;

        // ================= Bugs =================

        // ---- A pet's farewell is told as it died ----
        // It was "went down in the fighting" for any death in the day after a battle began (or the game's first day), and
        // "died of old age" for an old animal a wild beast or the colony's own knife took.
        C(VisitorReviewMath.Manner(true, true) == "hero" && VisitorReviewMath.Manner(true, false) == "hero", "an enemy's blow: the fighting's lines");
        C(VisitorReviewMath.Manner(false, false) == "quiet", "no fresh wound: the quiet lines");
        C(VisitorReviewMath.Manner(false, true) == null, "a wound from no enemy (a wild beast, a fall, the colony's knife): only the plain lines");
        C(VisitorReviewMath.OldAge(true, "quiet") && !VisitorReviewMath.OldAge(true, "hero") && !VisitorReviewMath.OldAge(true, null) && !VisitorReviewMath.OldAge(false, "quiet"),
            "old age only for an old animal that died quietly");
        var pets = Code("Strangers/Pets.cs");
        var farewell = Between(pets, "private void Farewell(", "private static bool ByEnemy(");
        C(farewell.Contains("VisitorReviewMath.Manner(ByEnemy(pet), FreshWound(pet))") && !farewell.Contains("inBattle ||") && !farewell.Contains("battleStart"),
            "the farewell reads how it died, not when the last battle was");
        C(farewell.Contains("VisitorReviewMath.OldAge(AnimalFacts.Old(pet), manner)") && farewell.Contains("Notify_PetKilled") && farewell.Contains("bonded && hero"),
            "old age by the death, and the vow on the killers only for an enemy's blow");
        var byEnemy = Between(pets, "private static bool ByEnemy(", "private static bool FreshWound(");
        C(byEnemy.Contains("combatLogEntry") && byEnemy.Contains("HostileTo(Faction.OfPlayer)"), "an enemy's blow is read from the game's combat log, the attacker hostile to the colony");

        // ---- The fallen raider's plea: a few steps away, and done there ----
        var plea = Def("DilemmaDef", "Council_DlmS_FallenPlea");
        var role = plea?.Element("stranger");
        C(role != null && (string)role.Element("kind") == "Fallen" && F(role, "maxDistance") > 0f && F(role, "maxDistance") <= VisitorReviewMath.FewSteps,
            "the plea is asked only of one a few steps from the fallen, as its words say");
        C(plea != null && plea.Element("texts")!.Elements("li").Any(t => t.Value.Contains("a few steps from {A_nameDef}")), "the words that say so are still there");
        var effects = Code("Strangers/StrangerEffects.cs");
        var endIt = Between(effects, "public class DilemmaEffect_EndIt", "public class DilemmaEffect_Capture");
        C(endIt.Contains("public override string Unavailable(") && endIt.Contains("a.Downed || a.InMentalState") && endIt.Contains("CC_VisR7_NotAble"),
            "end it: not given while A is down, in a break or away");
        C(endIt.Contains("InHorDistOf(x.Position, VisitorReviewMath.BesideCells)") && endIt.Contains("ExecutionUtility.DoExecutionByCut(a, x"),
            "beside them, A does it there and then");
        C(endIt.Contains("JobDefOf.AttackMelee") && endIt.Contains("killIncappedTarget = true") && endIt.Contains("TryTakeOrderedJob"),
            "further off, A walks over and does it on the map, not from across the field");
        C(VisitorReviewMath.BesideCells > 1.5f && VisitorReviewMath.BesideCells < 4f, "beside is a step or two");
        var capture = Between(effects, "public class DilemmaEffect_Capture", "public class DilemmaEffect_Designate");
        C(capture.Contains("public override string Unavailable(") && capture.Contains("a.InMentalState") && capture.Contains("CC_Stranger_Away"),
            "take them prisoner: never an order to one in a break, down or away");

        // ---- An animal given to a visitor goes with them, or off the map ----
        var givePet = Between(effects, "public class DilemmaEffect_GivePet", "public class DilemmaEffect_LifeEvent");
        C(givePet.Contains("CC_VisR7_PetAway") && givePet.Contains("ctx.pet.Map != ctx.stranger.Map"), "an animal away from the map can't be given from here");
        C(givePet.Contains("Lords.Leave(pet)") && givePet.Contains("new LordJob_ExitMapBest(") && givePet.Contains("lord.CanAddPawn(pet)"),
            "taken into their group, or set off on its own; never left on the colony's land as theirs");
        C(keyed.ContainsKey("CC_VisR7_NotAble") && keyed["CC_VisR7_NotAble"].Contains("{0}") && keyed.ContainsKey("CC_VisR7_PetAway") && keyed["CC_VisR7_PetAway"].Contains("{0}"),
            "the reasons have their words");

        // ---- Food carried out to a visitor never takes anyone from what matters ----
        var fetch = Between(Code("Visitors/KeepOut.cs"), "public static bool Fetch(Pawn guest", "public static bool Handed(");
        C(fetch.Contains("PlayOuts.Interruptible(c) && CanCarry(c, t, guest, count)"),
            "the carrier is someone free to break off (never tending, a rescue, a meal or the player's order)");

        // ---- A visitor's game with the colony's dog ----
        var idle = Code("Visitors/IdleHoursJobs.cs");
        var petFail = Between(idle, "or on an order the player gave it.", "var go = Toils_Goto.GotoThing");
        C(petFail.Contains("roping?.IsRoped") && petFail.Contains("playerForced") && petFail.Contains("GetLord(Other) != null"),
            "the dog is let be the moment it is led, in a group of the colony's or on an order");

        // ---- The trainer told "just a story" goes on now ----
        var rumour = Between(Code("Integrations/PokeWorldCamp.cs"), "public class HappeningWorker_ShinyRumour", "public class DilemmaEffect_ShinyRumour");
        C(rumour.Contains("option == 2") && rumour.Contains("Arrivals.SendAway("), "the trainer sets off when the colony says it is just a story");
        var shiny = Def("IncidentDef", "Council_Evt_PW_ShinyRumour")?.Descendants("li").FirstOrDefault(x => Cls(x) == "HappeningExtension");
        var shinyOptions = shiny?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        C(shinyOptions.Count == 3 && ((string)shinyOptions[2].Element("label")) == "Just a story" && ((string)shinyOptions[2].Element("outcome")).Contains("goes on down the road"),
            "the third answer is the one that sends them on");

        // ================= Real effects =================

        // ---- A bad night costs rest, a shoddy garment wears ----
        C(Math.Abs(VisitorReviewMath.RestAfter(0.8f, 0.1f) - 0.7f) < 0.001f && VisitorReviewMath.RestAfter(0.15f, 0.1f) == 0.1f && VisitorReviewMath.RestAfter(0.05f, 0.1f) == 0.05f
          && VisitorReviewMath.RestAfter(0.5f, 0f) == 0.5f, "a bad night takes its share of rest, never below a tenth");
        C(VisitorReviewMath.WearLoss(100, 100, 0.04f) == 4 && VisitorReviewMath.WearLoss(2, 100, 0.04f) == 1 && VisitorReviewMath.WearLoss(1, 100, 0.04f) == 0
          && VisitorReviewMath.WearLoss(50, 60, 0.001f) == 1 && VisitorReviewMath.WearLoss(50, 60, 0f) == 0, "a worn piece loses its share, at least one, never the last");
        var nuis = Code("Strangers/Nuisances.cs");
        var fire = Between(nuis, "private static void Fire(NuisanceDef def", "private static string Check(NuisanceCheck check");
        C(fire.Contains("Effect(def, p, thing);") && fire.Contains("VisitorReviewMath.RestAfter(") && fire.Contains("VisitorReviewMath.WearLoss(") && fire.Contains("a.def.label == thing"),
            "a nuisance does what its lines tell: the rest a bad night costs, the wear on the piece it names");
        C(nuis.Contains("public float rest;") && nuis.Contains("public float wear;") && nuis.Contains("rest < 0f || rest > 0.3f") && nuis.Contains("wear < 0f || wear > 0.2f"),
            "the two fields, kept small by the def's own check");
        foreach (var (name, field, lo, hi) in new[] { ("Council_Nuisance_NoisyGenerator", "rest", 0.05f, 0.15f), ("Council_Nuisance_DraughtyBed", "rest", 0.05f, 0.12f),
                     ("Council_Nuisance_ShoddyClothes", "wear", 0.02f, 0.08f), ("Rimfolk_VisR7_Nuisance_HearthOut", "rest", 0.03f, 0.1f), ("Rimfolk_VisR7_Nuisance_WornBoots", "wear", 0.02f, 0.06f) })
        {
            var d = Def("NuisanceDef", name);
            float v = F(d, field);
            C(d != null && v >= lo && v <= hi, $"{name}: {field} {v}, small");
        }
        C(F(Def("NuisanceDef", "Council_Nuisance_HardBed"), "rest") == 0f, "the hard bed takes no rest of its own: the game's rest on a poor bed already costs the night");

        // ---- Balance: the callers told from data sell at about the game's own worth (Tools/visitors_impact_audit.py --print balance) ----
        int Count(string caller, string thing) =>
            int.Parse((string)Def("IncidentDef", caller)?.Descendants("li").FirstOrDefault(x => Cls(x) == "DilemmaEffect_Spawn" && (string)x.Element("thing") == thing)?.Element("count") ?? "0");
        C(Count("Rimfolk_Evt_R6Caller_Carter", "Hay") == 150, "the carter's hay: a load worth its price (150 hay, 90 silver's worth, for 70 to 110)");
        C(Count("Rimfolk_Evt_R6Caller_Builder", "BlocksGranite") == 90, "the builder's stone: a load worth its price (90 blocks, 81 silver's worth, for 70 to 130)");
        C(Count("Rimfolk_Evt_R6Caller_Guide", "Pemmican") == 45, "the guide's dried meat: worth its price (45, 63 silver's worth, for 50 to 90)");
        C(Count("Rimfolk_Evt_Brewer", "Beer") == 8, "the brewer's beer: no longer a gift at the price (8, 96 silver's worth, for 60 to 110)");

        // ================= Added in the frames there are =================

        // ---- Two more nuisances ----
        var newNuis = Code("Strangers/NuisancesNew.cs");
        foreach (var (name, cls, thought) in new[] { ("Rimfolk_VisR7_Nuisance_HearthOut", "NuisanceCheck_VisR7HearthOut", "Rimfolk_VisR7_Nuisance_HearthOut"),
                     ("Rimfolk_VisR7_Nuisance_WornBoots", "NuisanceCheck_VisR7WornBoots", "Rimfolk_VisR7_Nuisance_WornBoots") })
        {
            var d = Def("NuisanceDef", name);
            C(d != null && ((string)d.Element("checkClass")) == "ColonyCouncil." + cls && newNuis.Contains("public class " + cls + " : NuisanceCheckWorker"), $"{name}: read from the map by its own check");
            var th = Def("ThoughtDef", thought);
            var mood = th?.Descendants("baseMoodEffect").Select(x => float.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList() ?? new List<float>();
            C(th != null && mood.Count > 0 && mood.All(m => m <= -1f && m >= -3f) && F(th, "durationDays") <= 1f, $"{name}: a small, short mood");
            var texts = d?.Element("texts")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            C(texts.Count >= 6 && texts.Any(t => !t.StartsWith("[")), $"{name}: lines, some for any moment");
            C(texts.All(t => Plain(Strip(t, out _))), $"{name}: plain words");
            C(texts.All(t => Regex.Matches(t, @"\{([A-Za-z_]+)\}").Cast<Match>().All(m => m.Groups[1].Value == "THING" || m.Groups[1].Value.StartsWith("A_"))),
                $"{name}: only the colonist and the thing");
            C(texts.All(t => Strip(t, out _).Contains("{THING}")), $"{name}: every line names the thing, so the player can find it");
        }
        var hearth = Between(newNuis, "public class NuisanceCheck_VisR7HearthOut", "public class NuisanceCheck_VisR7WornBoots");
        C(hearth.Contains("BodyMath.Draughty(") && hearth.Contains("RoomUtil.BedRoom(p)") && hearth.Contains("CompProperties_HeatPusher") && hearth.Contains("!fuel.HasFuel"),
            "the fire out: cold outside, their own bedroom's fire, its fuel gone");
        var boots = Between(newNuis, "public class NuisanceCheck_VisR7WornBoots", "public class NuisanceCheck_DraughtyBed");
        C(boots.Contains("g.defName == \"Feet\"") && boots.Contains("groups.All(") && boots.Contains("WornBelow"), "worn boots: worn on the feet alone, below half");
        var hint = Def("MoodHintDef", "Rimfolk_VisR7_Mood_Nuisances");
        C(hint != null && hint.Element("thoughts")!.Elements("li").Select(x => x.Value).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_VisR7_Nuisance_HearthOut", "Rimfolk_VisR7_Nuisance_WornBoots" }),
            "the two moods have a hint at what fixes them");

        // ---- More words for the pets ----
        var known = new HashSet<string> { "hero", "quiet", "oldage", "small", "midsize", "big", "defended", "fought" };
        foreach (var (name, pool) in new[] { ("Rimfolk_VisR7_Lines_PetFarewell", "Council_Pool_PetFarewell"), ("Rimfolk_VisR7_Lines_PetHeroics", "Council_Pool_PetHeroics") })
        {
            var d = Def("FlavorExtensionDef", name);
            C(d != null && (string)d.Element("pool") == pool, $"{name}: joins {pool}");
            var lines = d?.Element("lines")?.Descendants("li").Select(x => x.Value).ToList() ?? new List<string>();
            C(lines.Count >= 6, $"{name}: lines");
            C(lines.All(l => { Strip(l, out var tags); return tags.All(known.Contains); }), $"{name}: only the tags its code gives");
            C(lines.All(l => Plain(Strip(l, out _)) && Regex.Matches(l, @"\{([A-Za-z_]+)\}").Cast<Match>().All(m => m.Groups[1].Value.StartsWith("PAWN_") || m.Groups[1].Value.StartsWith("PET_"))),
                $"{name}: plain words, only the person and the animal");
            C(lines.All(l => !Regex.IsMatch(l, @"\braiders?\b")), $"{name}: no raiders named, as an enemy may be a beast or a machine");
        }
        var farewellLines = Def("FlavorExtensionDef", "Rimfolk_VisR7_Lines_PetFarewell")?.Element("lines")?.Descendants("li").Select(x => x.Value).ToList() ?? new List<string>();
        C(new[] { "hero", "quiet", "oldage" }.All(t => farewellLines.Any(l => l.StartsWith("[" + t))) && farewellLines.Any(l => !l.StartsWith("[")),
            "a farewell for each way it can die, and for any");

        // ---- Three more offers from the callers at the gate ----
        foreach (var (caller, label, cls) in new[] { ("Rimfolk_Evt_HerbGatherer", "Leave to gather, for two bundles of herbs", "DilemmaEffect_Spawn"),
                     ("Rimfolk_Evt_Drovers", "Buy two young beasts ({COUNT} silver each)", "DilemmaEffect_SpawnAnimal"), ("Rimfolk_Evt_LostTraveller", "A meal, and a night by the wall", "DilemmaEffect_CarryOut") })
        {
            var ext = Def("IncidentDef", caller)?.Descendants("li").FirstOrDefault(x => Cls(x) == "HappeningExtension");
            var options = ext?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            var o = options.FirstOrDefault(x => (string)x.Element("label") == label);
            int def = int.Parse((string)ext?.Element("defaultOption") ?? "-1");
            C(o != null, $"{caller}: the new offer \"{label}\"");
            C(def >= 0 && def == options.Count - 1 && options[def].Element("effects")!.Elements("li").All(x => Cls(x) == "DilemmaEffect_SendAway"),
                $"{caller}: saying nothing still sends them on");
            C(o?.Element("effects")?.Elements("li").Any(x => Cls(x) == cls) == true, $"{caller}: the offer does something the game keeps ({cls})");
            C(o != null && Plain((string)o.Element("label")) && Plain((string)o.Element("outcome")), $"{caller}: plain words");
        }
        var night = Def("IncidentDef", "Rimfolk_Evt_LostTraveller")?.Descendants("li").Where(x => Cls(x) == "DilemmaEffect_CarryOut").FirstOrDefault(x => (string)x.Element("night") == "true");
        C(night != null && (string)night.Element("eat") == "true", "the night by the wall is played out: fed, and asleep by the wall until first light");
        var pair = Def("IncidentDef", "Rimfolk_Evt_Drovers")?.Descendants("li").FirstOrDefault(x => Cls(x) == "DilemmaEffect_SpawnAnimal" && (string)x.Element("count") == "2");
        var pay = pair?.Parent?.Elements("li").FirstOrDefault(x => Cls(x) == "DilemmaEffect_Consume");
        C(pair != null && pay != null && (string)pay.Element("factor") == "2", "two beasts cost twice the one");

        // ================= The measure =================

        C(File.Exists(Path.Combine(root, "Tools", "visitors_impact_audit.py")) && File.Exists(Path.Combine(root, "Tools", "visitors_impact_skips.txt")), "the audit and its skip list are there");
        var doc = File.ReadAllText(Path.Combine(root, "docs", "review-visitors.md"));
        C(doc.Contains("<!-- visitors_impact_audit: begin -->") && doc.Contains("<!-- visitors_impact_audit: end -->") && !doc.Contains("Skip list rows that no longer match"),
            "the review's measured part is written, and every row read by hand still matches");
        return n;
    }
}
