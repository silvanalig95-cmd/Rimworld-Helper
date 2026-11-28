using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of the camps and the world (docs/review-camps-world.md, Tools/world_impact_audit.py): the bugs found in what
/// would show in play, each kept fixed, and the answers given a real effect where their words already said so. A camp
/// pays the silver its answer says it paid, with a trader or without; goods the colony bought from a camp are never lost
/// when the one carrying them over can't, nor the price of a load on the road when the wardens give up their post; charity
/// a camp's people remember is worth a little goodwill; offered work is done on the map; and the thin camps and world events have their missing choice.
/// </summary>
public static class ReviewCampsWorldTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the camps and the world: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement Def(string type, string name) =>
            defs.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        XElement Ask(string camp, string key) =>
            Def("CampKindDef", camp)?.Element("asks")?.Elements("li").FirstOrDefault(li => (string)li.Element("key") == key);
        List<XElement> Effects(XElement holder) => holder?.Element("effects")?.Elements("li").ToList() ?? new List<XElement>();
        bool Is(XElement e, string cls) => ((string)e.Attribute("Class") ?? "").EndsWith("." + cls);
        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        // ================= Bugs =================

        // ---- A camp pays the silver its answer says it paid: from its trader's purse first, the rest from its own ----
        // A camp with no trader paid nothing (the herd in the fields: "Make them pay for the damage", 40 silver on the card
        // and in the words, nothing set out).
        C(CampAskMath.FromPurse(0, 40) == 0 && CampAskMath.FromPurse(100, 40) == 40 && CampAskMath.FromPurse(10, 40) == 10 && CampAskMath.FromPurse(-5, 40) == 0,
            "the purse gives what it holds of the sum, never more and never less than nothing");
        var requests = Code("Camps/CampRequests.cs");
        var silver = Between(requests, "if (what == \"silver\")", "Func<Thing, bool> match;");
        C(silver.Contains("CampAskMath.FromPurse(") && silver.Contains("Place(ThingDefOf.Silver, count, at)") && !silver.Contains("if (take <= 0) return 0;"),
            "silver a camp gives is paid in full, with a trader or without (the herd in the fields' damages)");
        // ---- What a camp sets out is counted before it is set down (a stack that merges reads nothing after) ----
        var place = Between(requests, "private int Place(ThingDef def, int n", "public bool CanHelpNow(");
        C(place.Contains("int k = Mathf.Min(n, def.stackLimit);") && place.Contains("placed += k;") && !place.Contains("placed += t.stackCount;"),
            "goods a camp sets out are counted before they are set down, so a merged stack is still told (the dig's share)");

        // ---- Goods the colony bought are never lost when the carry is let go ----
        C(CampAskMath.SetOutWhenLetGo(true, false, 40, 0) == 40, "bought goods not yet taken up: all of them set out at the camp's edge");
        C(CampAskMath.SetOutWhenLetGo(true, true, 40, 25) == 25 && CampAskMath.SetOutWhenLetGo(true, true, 40, 60) == 40,
            "bought goods taken up: what the bearer still has, never more than was bought (what fell from their hands lies on the map)");
        C(CampAskMath.SetOutWhenLetGo(false, false, 40, 0) == 0 && CampAskMath.SetOutWhenLetGo(true, false, 0, 0) == 0,
            "a good turn or a gift let go is theirs to keep; nothing bought, nothing set out");
        var carry = Code("Camps/CampCarry.cs");
        var watch = Between(carry, "private void WatchCarries()", "private void LetGoAll()");
        C(watch.Contains("LetGo(c)") && !watch.Contains("c.by.Downed || !Here.Contains(c.by) || Now > c.until);"),
            "a carry whose bearer is down, gone or too long is let go through LetGo, not dropped");
        var job = Between(carry, "public Job CarryJob(Pawn p)", "private Job GatherJob(");
        C(Regex.Matches(job, @"LetGo\(c\);").Count == 2 && Regex.Matches(job, @"carries\.Remove\(c\);").Count == 1,
            "a carry that can't be taken up or has no way to the gate is let go through LetGo");
        var letGo = Between(carry, "private void LetGo(CampCarry c)", "private void ExposeCarries()");
        C(letGo.Contains("c.help != \"bought\"") && letGo.Contains("CampAskMath.SetOutWhenLetGo(") && letGo.Contains("Place(c.def, n, null, put)") && letGo.Contains("CC_CampBring_SetOut"),
            "only what the colony paid for is set out at the camp's edge, and the colony hears where");
        C(Between(Code("Camps/CampLife.cs"), "public void Leave(string reason", "LandLeaving();").Contains("LetGoAll();"),
            "a camp leaving lets its carries go first, so what the colony bought is set out before they go");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_CampsMore.xml"));
        C(keyed.Contains("<CC_CampBring_SetOut>") && keyed.Contains("{COUNT} {THING}"), "the camp's edge message has its words");

        // ---- The road post's load: the wardens leaving before the carters come back don't keep its price ----
        var leaving = Between(Code("Camps/CampKindsLand.cs"), "private void LandLeaving()", "kind?.Worker?.Leaving(this);");
        C(leaving.Contains("if (LoadOnRoad && !Hostile) PayRoad(null);"),
            "the colony's load on the road is paid for when the wardens' post is given up before the carters come back");

        // ---- Read and kept: a kind new head's gift at the gate is its worker's (no second gift written into the answers) ----
        C(Code("World/Region/NewHeads.cs").Contains("if (temper == HeadTemper.Kind && map != null && f != null)"),
            "a kind new head's gift of their own, which the [kind] words promise, is handed over by the worker at the gate");
        var gate = Def("WorldSituationDef", "Rimfolk_WS_NewHead")?.Element("beats")?.Elements("li").FirstOrDefault(b => (string)b.Element("key") == "gate");
        C(gate != null && !(gate.Element("options")?.Elements("li").Any(o => Effects(o).Any(e => Is(e, "WorldEffect_Loot"))) ?? false),
            "the new head's gift is not given twice");

        // ================= Answers given a real effect =================

        // ---- Charity a camp's people thank the colony for or remember: a little goodwill, modest beside what it costs ----
        var charity = new (string camp, string key)[]
        {
            ("Council_Camp_Mourners", "food"), ("Rimfolk_Camp_Novices", "food"), ("Rimfolk_Camp_StrandedCarters", "meal"), ("Rimfolk_Camp_Gleaners", "bread"),
            ("Rimfolk_Camp_WalkingHome", "road"), ("Rimfolk_Camp_WinterPilgrims", "coat"), ("Rimfolk_Camp_GodPilgrims", "bread"), ("Rimfolk_Camp_Woodcutters", "supper"),
            ("Rimfolk_Camp_Shearers", "supper"), ("Rimfolk_Camp_Catchers", "feed"), ("Rimfolk_Camp_Teacher", "supper"), ("Rimfolk_Camp_Wounded", "supper"),
            ("Rimfolk_Camp_Wounded", "medicine"), ("Rimfolk_Camp_Watchers", "shelter"), ("Rimfolk_Camp_SnowboundCaravan", "shelter"),
            ("Rimfolk_Camp_WinterPilgrims", "shelter"), ("Rimfolk_Camp_PilgrimHostel", "bread"), ("Rimfolk_Camp_LanternPost", "meat"),
        };
        foreach (var (camp, key) in charity)
        {
            var a = Ask(camp, key);
            var gw = Effects(a).FirstOrDefault(e => Is(e, "DilemmaEffect_Goodwill"));
            int amount = gw != null ? (int?)gw.Element("amount") ?? 0 : 0;
            C(a != null && amount >= 1 && amount <= 4, $"{camp}: {key}: a little goodwill with their people ({amount})");
        }
        // ---- Work they offered, done on the map: the deserters for the colony's steel, the young hands for their supper ----
        var gear = Ask("Council_Camp_Deserters", "gear");
        C(Effects(gear).Any(e => Is(e, "DilemmaEffect_Felled") && (string)e.Element("near") == "Camp") && ((string)gear?.Element("tip") ?? "").Contains("wood"),
            "the deserters work for the colony's steel, as they offered: wood felled near their camp, and the tip says so");
        var work = Ask("Rimfolk_Camp_YoungHands", "work");
        C(work != null && Effects(work).Any(e => Is(e, "DilemmaEffect_Felled")) && Effects(work).Any(e => Is(e, "DilemmaEffect_CarryOut") && (string)e.Element("what") == "meals"),
            "the young hands on the road work a day for their supper, as they offered: their ask, wood felled and meals carried over");
        // ---- The run's catch salted down: the game's own pemmican bill at the stove ----
        var catchBeat = Def("WorldSituationDef", "Rimfolk_WS_Run")?.Element("beats")?.Elements("li").FirstOrDefault(b => (string)b.Element("key") == "catch");
        var salt = catchBeat?.Element("options")?.Elements("li").ElementAtOrDefault(1);
        C(Effects(salt).Any(e => Is(e, "DilemmaEffect_PutBy")), "the run's catch salted down for the winter: a bill to put it by at the stove");
        // ---- The one who came back from the expedition, fed and sent home to their people ----
        var survivor = Def("WorldSituationDef", "Rimfolk_WS_Expedition")?.Element("beats")?.Elements("li").FirstOrDefault(b => (string)b.Element("key") == "survivor");
        C(Effects(survivor?.Element("options")?.Elements("li").ElementAtOrDefault(1)).Any(e => Is(e, "WorldEffect_Goodwill")),
            "the expedition's survivor fed and sent home: their people hear of it");
        // ---- Encounters that teach: a trick or two picked up ----
        foreach (var (enc, skill) in new[] { ("Council_EncM_RowTalk", "Plants"), ("Council_EncP_WarStories", "Melee") })
        {
            var learn = Effects(Def("EncounterDef", enc)).FirstOrDefault(e => Is(e, "DilemmaEffect_Learn"));
            float xp = learn != null ? (float?)learn.Element("xp") ?? 0f : 0f;
            C(learn != null && (string)learn.Element("skill") == skill && xp > 0f && xp <= 200f, $"{enc}: a little {skill} learned, as the words tell it");
        }

        // ================= The thin ones' missing choice =================

        var herds = Def("WorldSituationDef", "Council_WS_StrangeHerds");
        var hunters = herds?.Element("beats")?.Elements("li").FirstOrDefault(b => (string)b.Element("key") == "hunters");
        var hOpts = hunters?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        var away = hOpts.Count > 0 ? Effects(hOpts[0]).FirstOrDefault(e => Is(e, "WorldEffect_Away")) : null;
        C(hunters != null && (string)hunters.Element("skill") == "Animals" && hOpts.Count == 2 && (int?)hunters.Element("defaultOption") == 1,
            "the strange herds: the town's hunters ask for the colony's best with animals, and the default is to keep them home");
        C(away != null && Effects(away).Any(e => Is(e, "WorldEffect_Learn")) && Effects(away).Any(e => Is(e, "WorldEffect_Goodwill")) && away.Element("home") != null,
            "the strange herds: sent there and back as a caravan of one, learning and thanked when home");

        // ================= The player's rules in what was added =================

        var added = new List<string>();
        foreach (var o in hOpts) added.AddRange(new[] { (string)o.Element("label"), (string)o.Element("outcome") });
        added.AddRange(hunters?.Element("texts")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>());
        added.AddRange(away?.Element("home")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>());
        added.AddRange(work?.Element("outcomes")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>());
        added.Add((string)work?.Element("tip"));
        added.AddRange(gear?.Element("outcomes")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>());
        foreach (var line in added.Where(l => !string.IsNullOrEmpty(l)))
        {
            C(!Regex.IsMatch(line, @"\b(in|within) \d+ (days|hours)\b|\{DAYS\}"), "no countdown in an added line: " + line.Substring(0, Math.Min(50, line.Length)));
            C(!line.Contains("—") && !line.Contains("!"), "no em dash or exclamation mark in an added line: " + line.Substring(0, Math.Min(50, line.Length)));
            C(!Regex.IsMatch(line, @"\b(god|gods)\b", RegexOptions.IgnoreCase), "no god named in an added line: " + line.Substring(0, Math.Min(50, line.Length)));
        }

        // ---- The audit that found them, for the next review ----
        C(File.Exists(Path.Combine(root, "Tools", "world_impact_audit.py")) && File.Exists(Path.Combine(root, "Tools", "world_impact_skips.txt")),
            "the world impact audit and its skip list are there for the next review");
        foreach (var line in File.ReadAllLines(Path.Combine(root, "Tools", "world_impact_skips.txt")).Where(l => l.Trim().Length > 0 && !l.StartsWith("#")))
            C(Regex.IsMatch(line, @"^\S+ \[(flavour|words|nothing)\] \S"), "a skip list row names its list and why: " + line.Substring(0, Math.Min(60, line.Length)));

        return n;
    }
}
