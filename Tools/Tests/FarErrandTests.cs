using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A colonist a world event sends to a far town really goes (World/FarErrands.cs, FarErrandMath.cs; the player's go of 20
/// November 2026): the rules (the reach, the days away, the event waiting for its colonist, the letters that wait or come
/// from the far town, staying on for a question, the note of which answer sent them), every answer that sends someone and
/// its days, the words as set going with the homecoming's letter, the blood feud's talks at the colony's gate, and the seams
/// with the season away and the world events.
/// </summary>
public static class FarErrandTests
{
    /// <summary>Every answer that sends a colonist away: the event, the beat, the answer, and the days there.</summary>
    public static readonly (string def, string beat, int option, float days)[] Answers =
    {
        ("Council_WS_Drought", "well", 0, 1f), ("Council_WS_NewRoad", "builder", 0, 1f), ("Council_WS_Murrain", "herdsman", 0, 2f),
        ("Council_WS_GreatFire", "roofs", 0, 4f), ("Council_WS_Bumper", "hands", 0, 4f), ("Council_WS_Toll", "petition", 0, 1f),
        ("Council_WS_IronScarce", "mend", 0, 3f), ("Council_WS_Exiles", "plea", 0, 1f), ("Council_WS_Rebuilding", "builder", 0, 7f),
        ("Council_WS_Vault", "writing", 0, 4f), ("Council_WS_Embargo", "talks", 0, 2f), ("Council_WS_Moot", "herald", 0, 4f),
        ("Council_WS_Moot", "herald", 1, 4f), ("Council_WS_OldBeast", "watch", 0, 4f), ("Council_WS_RivalColony", "poaching", 1, 0.5f),
        ("Council_WS_RivalColony", "poaching", 2, 0f), ("Rimfolk_WS_FoundingDay", "invite", 2, 2f),
        // The content round's third part (Rimfolk_WS_Round3.xml): the colony's best miner sent to dig out the miners of a fallen mine.
        ("Rimfolk_WS_MineFall", "help", 1, 2f),
        // The content round's fourth part (Rimfolk_WS_Round4.xml): one of the colony with an ache sent to bathe at the warm springs.
        ("Rimfolk_WS_WarmSprings", "send", 0, 3f)
    };

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a colonist sent to a far town: " + what);
        }

        // ================= The rules =================

        C(FarErrandMath.InReach(0f) && FarErrandMath.InReach(FarErrandMath.MaxRoadDays) && !FarErrandMath.InReach(FarErrandMath.MaxRoadDays + 0.01f)
          && !FarErrandMath.InReach(float.NaN) && !FarErrandMath.InReach(-1f) && FarErrandMath.MaxRoadDays == 8f,
            "a town within eight days' road each way, as the league's guard walks");
        C(Math.Abs(FarErrandMath.AwayDays(2f, 3f) - 7f) < 1e-4f && FarErrandMath.AwayDays(float.NaN, 2f) == 2f && FarErrandMath.AwayDays(1f, -3f) == 2f
          && Math.Abs(FarErrandMath.AwayDays(PeopleEvents2Math.RoadDays(5f), 1f) - 6f) < 1e-4f,
            "away the road both ways and the days there (five tiles out and a day there: six days)");
        C(FarErrandMath.AwayDaysShown(0.9f, 1f) == 3 && FarErrandMath.AwayDaysShown(2f, 3f) == 7 && FarErrandMath.AwayDaysShown(0f, 0f) == 1,
            "the card's days are whole days, rounded up, never none");
        int day = FarErrandMath.DayTicks;
        C(FarErrandMath.HoldsEnd(true, 10 * day, 10 * day) && FarErrandMath.HoldsEnd(true, 39 * day, 10 * day) && !FarErrandMath.HoldsEnd(true, 40 * day, 10 * day)
          && !FarErrandMath.HoldsEnd(false, 10 * day, 10 * day) && FarErrandMath.HoldsEnd(true, int.MaxValue - 1, int.MaxValue - day),
            "the event waits for its colonist, thirty days past its end at most, and not at all with nobody away");
        C(FarErrandMath.EndAfterHome(10 * day, 5 * day) == 12 * day && FarErrandMath.EndAfterHome(10 * day, 20 * day) == 20 * day && FarErrandMath.EndAfterHome(int.MaxValue - 10, 0) == int.MaxValue,
            "home again, it ends no sooner than two days on, for the letters that waited");
        C(FarErrandMath.Turn(true, false, true, false, 0) == FarErrandMath.BeatTurn.Wait && FarErrandMath.Turn(true, false, false, false, 0) == FarErrandMath.BeatTurn.Go
          && FarErrandMath.Turn(false, false, true, true, 0) == FarErrandMath.BeatTurn.Go,
            "a letter that would fall to the colonist away waits for them; the rest come as usual");
        C(FarErrandMath.Turn(false, true, false, false, 0) == FarErrandMath.BeatTurn.Drop && FarErrandMath.Turn(false, true, true, false, 0) == FarErrandMath.BeatTurn.Wait
          && FarErrandMath.Turn(false, true, true, true, 2 * day) == FarErrandMath.BeatTurn.Wait && FarErrandMath.Turn(false, true, true, true, day) == FarErrandMath.BeatTurn.Go
          && FarErrandMath.Turn(true, true, true, true, -5) == FarErrandMath.BeatTurn.Go,
            "a letter from the far town comes on their last day there, waits on the road, and is dropped once they have left");
        C(FarErrandMath.BestIsAway(8, 8, 6) && FarErrandMath.BestIsAway(8, -1, 6) && !FarErrandMath.BestIsAway(7, 8, 6) && !FarErrandMath.BestIsAway(5, 3, 6),
            "a skill's letter is theirs when nobody at home is as good, and they are good enough");
        C(FarErrandMath.StaysOn(true, 10 * day, 10 * day) && FarErrandMath.StaysOn(true, 12 * day, 10 * day) && !FarErrandMath.StaysOn(true, 13 * day, 10 * day)
          && !FarErrandMath.StaysOn(false, 10 * day, 10 * day), "they stay on for a question there, three days past their days at most");
        var note = new FarErrandMath.Note { situation = 12, def = "Council_WS_Embargo", beat = "talks", option = 0, riskList = true, index = 0, risk = true, count = 180 };
        C(FarErrandMath.Read(FarErrandMath.Write(note), out var back) && back.situation == 12 && back.def == note.def && back.beat == "talks" && back.option == 0
          && back.riskList && back.index == 0 && back.risk && back.count == 180, "the note of which answer sent them reads back as written");
        var plain = new FarErrandMath.Note { situation = -1, def = "a;b|c/d", beat = "k", option = 2, index = 3, count = 0 };
        string pw = FarErrandMath.Write(plain);
        C(!pw.Contains("|") && !pw.Contains("/") && FarErrandMath.Read(pw, out var pb) && pb.def == "a_b_c_d" && pb.option == 2 && pb.index == 3 && !pb.risk && !pb.riskList,
            "the note never breaks the season away's own words around it");
        C(!FarErrandMath.Read(null, out _) && !FarErrandMath.Read("", out _) && !FarErrandMath.Read("1;d;b;0;e;0;-", out _) && !FarErrandMath.Read("x;d;b;0;e;0;-;0", out _)
          && !FarErrandMath.Read("1;d;b;-1;e;0;-;0", out _) && !FarErrandMath.Read("1;d;b;0;q;0;-;0", out _) && !FarErrandMath.Read("1;;b;0;e;0;-;0", out _),
            "a note that isn't one is not read");

        // ================= The answers =================

        string defs = Path.Combine(root, "Defs", "WorldSituationDefs");
        var all = Directory.GetFiles(defs, "*.xml").SelectMany(f => XDocument.Load(f).Root.Elements()).Where(d => d.Name.LocalName == "ColonyCouncil.WorldSituationDef")
            .ToDictionary(d => d.Element("defName")?.Value ?? "", d => d);
        XElement Beat(string def, string key) => all.TryGetValue(def, out var d) ? d.Element("beats")?.Elements("li").FirstOrDefault(b => b.Element("key")?.Value == key) : null;
        XElement Option(string def, string key, int i) => Beat(def, key)?.Element("options")?.Elements("li").ElementAtOrDefault(i);
        bool IsAway(XElement li) => li.Attribute("Class")?.Value == "ColonyCouncil.WorldEffect_Away";
        IEnumerable<XElement> Aways(XElement o) => (o?.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Concat(o?.Element("riskEffects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Where(IsAway);
        float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);

        var found = all.SelectMany(kv => (kv.Value.Element("beats")?.Elements("li") ?? Enumerable.Empty<XElement>())
            .SelectMany(b => (b.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select((o, i) => (def: kv.Key, beat: b.Element("key")?.Value, option: i, o))))
            .Where(x => Aways(x.o).Any()).ToList();
        C(found.Count == Answers.Length && Answers.All(a => found.Any(f => f.def == a.def && f.beat == a.beat && f.option == a.option)),
            Answers.Length + " answers of " + Answers.Select(a => a.def).Distinct().Count() + " world events send a colonist away, and only those");
        C(Answers.All(a => Aways(Option(a.def, a.beat, a.option)).All(e => Math.Abs(F(e.Element("days")?.Value ?? "-1") - a.days) < 1e-4f)),
            "each for the days its words name (a day for advice, two among the herds, four at the roofs and the harvest, a week at the bridge)");
        C(Answers.All(a => Option(a.def, a.beat, a.option)?.Element("riskOutcome") == null || Option(a.def, a.beat, a.option).Element("effects")?.Elements("li").Any(IsAway) != true),
            "a gamble's words come home with them, not as they set out");
        C(Answers.All(a => !Regex.IsMatch(Option(a.def, a.beat, a.option)?.Element("outcome")?.Value ?? "", @"\b(comes back|is back|came back|spent|went over)\b")),
            "every answer tells it as set going, not as done");
        var risky = Answers.Where(a => Option(a.def, a.beat, a.option)?.Element("riskChance") != null && Option(a.def, a.beat, a.option).Element("riskReplaces") == null).ToList();
        C(risky.Count == 5 && risky.All(a => Aways(Option(a.def, a.beat, a.option)).All(e =>
              e.Element("home")?.Elements("li").Any(l => l.Value.StartsWith("[risk]")) == true && e.Element("home").Elements("li").Any(l => l.Value.StartsWith("[!risk]")))),
            "where the gamble may come true, the homecoming has a line for each way it went");
        var homes = Answers.SelectMany(a => Aways(Option(a.def, a.beat, a.option))).SelectMany(e => e.Element("home")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select(l => l.Value).ToList();
        C(homes.Count >= 18 && homes.Select(h => Regex.Replace(h, @"\[[^\]]*\]", "")).All(h => !h.Contains('—') && !h.Contains('!') && h.Contains("{A_nameDef}")),
            "the homecoming's words name who went, with no em dash or exclamation mark");
        C(Answers.Where(a => a.def != "Council_WS_Moot").All(a => Aways(Option(a.def, a.beat, a.option)).All(e => e.Element("home")?.Elements("li").Any() == true))
          && Aways(Option("Council_WS_Moot", "herald", 0)).All(e => e.Element("home") == null),
            "a letter says how each went; the moot's speaker comes home to the moot's own ending");
        var road = Aways(Option("Council_WS_NewRoad", "builder", 0)).Single();
        var watch = Aways(Option("Council_WS_OldBeast", "watch", 0)).Single();
        C(road.Element("to")?.Value == "Near" && road.Element("near")?.Value == "river" && road.Element("wage")?.Value == "true" && road.Element("placeLabel") != null
          && watch.Element("to")?.Value == "Near" && watch.Element("near")?.Value == "hills" && watch.Element("placeLabel") != null,
            "the crossing below the colony and the herders' fire in the hills are a tile out, the road gang's silver carried home");
        C(Option("Council_WS_NewRoad", "builder", 0).Element("effects").Elements("li").All(IsAway),
            "the road gang's silver is paid when the builder is home, not by a courier as the answer is given");
        var toll = Aways(Option("Council_WS_Toll", "petition", 0)).Single();
        C(Option("Council_WS_Toll", "petition", 0).Element("riskEffects") == null && toll.Descendants("requires").Any(r => r.Value == "noted"),
            "the petition refused costs goodwill when the speaker is home with the news");
        var embargo = Option("Council_WS_Embargo", "talks", 0);
        C(embargo.Element("effects").Elements("li").Count(IsAway) == 1 && embargo.Element("riskEffects").Elements("li").Count(IsAway) == 1 && embargo.Element("end")?.Value == "lifted",
            "the markets' talks: one errand for each way it goes, the ending once the talker is home");
        C(Beat("Council_WS_Moot", "pastures")?.Element("atPlace")?.Value == "true" && Beat("Council_WS_Moot", "pastures").Element("skill") == null
          && Option("Council_WS_Moot", "herald", 0).Element("effects").Elements("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("who")?.Value == "None"),
            "the moot's vote is asked of the speaker there, on their last day; the gift carried out by whoever is free");
        C(!(Beat("Council_WS_Bumper", "supper")?.Element("texts")?.Elements("li").Any(l => l.Value.Contains("has been to none") && !l.Value.StartsWith("[!hands]")) ?? true),
            "a hand home from the harvest supper isn't said to have been to none");

        // ================= The blood feud's talks, at the colony's gate =================

        var mediate = Beat("Council_WS_Feud", "mediate");
        var gates = mediate?.Element("effects")?.Elements("li").Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.VisitEffect_AtGate").ToList() ?? new List<XElement>();
        C(gates.Count == 2 && gates.Any(g => g.Element("enemy")?.Value == "true" && g.Element("stranger")?.Value == "false") && gates.Any(g => g.Element("enemy") == null),
            "both families' elders come to the gate with the question, one people each");
        var sit = Option("Council_WS_Feud", "mediate", 0)?.Element("effects")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Visit");
        C(sit != null && sit.Element("to")?.Value == "Stranger" && sit.Element("orTo") != null && (Option("Council_WS_Feud", "mediate", 0).Element("outcome")?.Value ?? "").Contains("at the gate"),
            "the talker sits with them there, and the words say the gate");
        C(!all["Council_WS_Feud"].ToString().Contains("colony's table"), "nothing of the feud says the colony's table any more");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string code = File.ReadAllText(Path.Combine(src, "World", "FarErrands.cs"));
        string ws = File.ReadAllText(Path.Combine(src, "World", "WorldSituations.cs"));
        string worker = File.ReadAllText(Path.Combine(src, "World", "WorldSituationWorker.cs"));
        string away = File.ReadAllText(Path.Combine(src, "Peoples", "Events2", "SeasonAway.cs"));
        C(away.Contains("[FarErrandMath.Key] = new FarErrandAway()") && away.Contains("HandlerOf(v)?.Stays(v, now)") && code.Contains("SeasonAway.Send(FarErrandMath.Key, null, ctx.a"),
            "the errand is the season away's own caravan of one, a kind of its own that may stay on");
        C(code.Contains("PeopleEvents2.MayLeave(a, map)") && code.Contains("Find.WorldReachability.CanReach") && code.Contains("FarErrandMath.InReach(road)") && code.Contains("TryFindBestExitSpot"),
            "greyed while the colonist can't leave, the colony would be short of hands, or the town is out of reach");
        C(ws.Contains("FarErrandMath.HoldsEnd(FarErrands.Out(s)") && ws.Contains("FarErrands.Turn(s, beat, map, now)") && ws.Contains("if (!FarErrands.Out(s)) s.ctx.a = ctx.a;")
          && ws.Contains("FarErrands.AnswerRisk = risk;") && ws.Contains("FarErrands.AnswerRisk = false;"),
            "the event waits for its colonist, its letters wait or come from the far town, and its person stays theirs");
        C(worker.Contains("beat.atPlace && FarErrands.Away(ctx.a)"), "a letter from the far town is from the one who is there");
        C(code.Contains("SickHome.Lodged(") && code.Contains("SickHome.TownSickness(town)"), "a town where sickness goes round is the illness budget's own stay");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_WorldSituations.xml"));
        var keys = Regex.Matches(code, "\"(CC_WS_Away_\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(keys.Count >= 10 && keys.All(k => keyed.Contains("<" + k + ">")), "every word of theirs is in the language file");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        string skips = File.ReadAllText(Path.Combine(root, "Tools", "playout_skips.txt"));
        C(audit.Contains("\"WorldEffect_Away\"") && !skips.Contains("Council_WS_NewRoad [builder/0]") && !skips.Contains("Council_WS_Feud [mediate/0]"),
            "the play-out audit counts the errand, and the road's builder and the feud's talks are off its skip list");
        string dbg = File.ReadAllText(Path.Combine(src, "World", "FarErrandsDebug.cs"));
        C(Regex.Matches(dbg, @"\[DebugAction\(""Rimfolk"", ""World event: ").Count == 4, "four dev actions");
        string csproj = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj"));
        C(csproj.Contains("World/FarErrandMath.cs"), "the rules are the mod's own, linked here");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
