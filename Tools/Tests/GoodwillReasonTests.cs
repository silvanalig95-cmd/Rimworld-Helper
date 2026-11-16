using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Every change in a people's goodwill names its reason (World/GoodwillReasonMath.cs, World/GoodwillWhy.cs): the rules,
/// the reasons' defs and labels, the source with no goodwill message left without a reason, the memory's hooks leaving
/// Rimfolk's own reasons alone, and a slight in a letter landing with the answer that says so.
/// </summary>
public static class GoodwillReasonTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "goodwill reasons: " + what);
        }

        // ================= The rules =================

        C(GoodwillReasonMath.NeverIntoWar(8, 20) == 8 && GoodwillReasonMath.NeverIntoWar(8, -90) == 8, "a gain is taken as it is");
        C(GoodwillReasonMath.NeverIntoWar(-8, 53) == -8, "a loss well above the floor is taken whole (the Stormriders' 53)");
        C(GoodwillReasonMath.NeverIntoWar(-8, -55) == -5, "a loss stops at -60");
        C(GoodwillReasonMath.NeverIntoWar(-8, -60) == 0 && GoodwillReasonMath.NeverIntoWar(-8, -70) == 0, "nothing is lost at or below the floor");
        C(GoodwillReasonMath.NeverIntoWar(-8, 0, -5) == -5, "the floor can be another");
        C(GoodwillReasonMath.Task(6) == GoodwillReasonMath.TaskDone && GoodwillReasonMath.Task(0) == GoodwillReasonMath.TaskDone
          && GoodwillReasonMath.Task(-4) == GoodwillReasonMath.TaskFailed, "a task's reason by the sign of its change");
        C(GoodwillReasonMath.Own("Rimfolk_GW_Slight") && GoodwillReasonMath.Own("Rimfolk_GodTouch_Heard") && GoodwillReasonMath.Own("Rimfolk_HarmedSacredBeast"),
            "Rimfolk's reasons, the two older ones too, are its own");
        C(!GoodwillReasonMath.Own("GaveGift") && !GoodwillReasonMath.Own("VisitBetterThanExpected") && !GoodwillReasonMath.Own(null), "the game's and other mods' are not");
        C(!GoodwillReasonMath.SlightLands(GoodwillReasonMath.SlightWritten, false), "a slight still in the letter lands nothing");
        C(GoodwillReasonMath.SlightLands(GoodwillReasonMath.SlightRead, false), "a slight read lands once the answer is in");
        C(!GoodwillReasonMath.SlightLands(GoodwillReasonMath.SlightRead, true), "and never twice");

        var all = GoodwillReasonMath.All;
        C(all.Length == all.Distinct().Count(), "each reason is listed once");
        C(all.All(x => x.StartsWith("Rimfolk_GW_", StringComparison.Ordinal)), "each reason is Rimfolk's own by its name");

        // ================= The source check =================

        string Calls(string body) => string.Join(" | ", GoodwillReasonMath.Unreasoned(body).Select(u => u.call));
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false);").Count == 1, "a message and no reason is found");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3);").Count == 1, "the message left out is the game's default, found");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, change, message, false);").Count == 1, "a message by a variable is found");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false, null);").Count == 1, "a null reason is none");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, false, false);").Count == 0, "without a message no reason is needed");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false, GoodwillWhy.Slight);").Count == 0, "a reason given passes");
        C(GoodwillReasonMath.Unreasoned("e.TryAffectGoodwillWith(Faction.OfPlayer, Mathf.Max(5, 5 - e.PlayerGoodwill), true, false, GoodwillWhy.Peace);").Count == 0,
            "commas inside an argument's own brackets are not the call's");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, won ? 6 : 3, true, false);").Count == 1, "a conditional's colon is not a named argument");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, reason: GoodwillWhy.Gift);").Count == 0, "a named reason passes");
        C(GoodwillReasonMath.Unreasoned("f.TryAffectGoodwillWith(Faction.OfPlayer, 3, canSendMessage: false);").Count == 0, "a named silent call passes");
        C(GoodwillReasonMath.Unreasoned("// f.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false);").Count == 0, "a comment is not a call");
        C(GoodwillReasonMath.Unreasoned("[HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]").Count == 0, "a patch naming the method is not a call");
        C(GoodwillReasonMath.Unreasoned("const string name = \"TryAffectGoodwillWith(\";").Count == 0, "the name in a string is not a call");
        var two = GoodwillReasonMath.Unreasoned("a();\nb();\n  f.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false);\n");
        C(two.Count == 1 && two[0].line == 3, "the line is the call's");

        string root = Root();
        C(root != null, "the repository is found");
        if (root == null) return n;
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var files = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories);
        // Every goodwill change Rimfolk makes that the game tells in its message names its reason. No exceptions are
        // needed: a dev action uses the game's own "debug event".
        var left = new List<string>();
        foreach (var f in files)
            foreach (var u in GoodwillReasonMath.Unreasoned(File.ReadAllText(f)))
                left.Add(Path.GetRelativePath(src, f) + ":" + u.line + " " + u.call);
        C(left.Count == 0, "no goodwill message is left without its reason: " + string.Join("; ", left.Take(5)));
        int calls = files.Sum(f => Regex.Matches(File.ReadAllText(f), @"TryAffectGoodwillWith\(").Count);
        C(calls >= 25, $"the check sees the calls ({calls})");

        // The wrappers take a reason and give it to the game.
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string worker = Read("World/WorldSituationWorker.cs"), chains = Read("Happenings/Workers_Chains2.cs"), missions = Read("Missions/Missions.cs"),
            camp = Read("Camps/Camp.cs"), schemes = Read("Diplomacy/Schemes.cs"), vassals = Read("Diplomacy/Vassals.cs"), fealty = Read("Diplomacy/Fealty.cs");
        C(worker.Contains("public static void Goodwill(Faction f, int amount, HistoryEventDef reason, bool message = true)")
          && worker.Contains("TryAffectGoodwillWith(Faction.OfPlayer, change, message, false, reason)")
          && worker.Contains("GoodwillReasonMath.NeverIntoWar(amount, f.PlayerGoodwill)"), "the world events' goodwill takes a reason and keeps short of war");
        C(chains.Contains("public static void Goodwill(Faction f, int amount, HistoryEventDef reason)")
          && chains.Contains("TryAffectGoodwillWith(Faction.OfPlayer, change, true, false, reason)"), "the story chains' goodwill takes a reason");
        C(missions.Contains("StoryChains.Goodwill(m.faction, amount, GoodwillWhy.Task(amount))"), "a task's goodwill names a task done or left undone");
        C(camp.Contains("public void LoseGoodwill(int amount, HistoryEventDef reason)") && schemes.Contains("LoseGoodwill(Faction f, int amount, HistoryEventDef reason)")
          && vassals.Contains("Goodwill(Faction f, int amount, HistoryEventDef reason, bool message)") && fealty.Contains("Goodwill(Faction f, int amount, HistoryEventDef reason)"),
            "the camps' and the diplomacy helpers take a reason");
        var nulls = files.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\b(?:WorldSituationWorker|StoryChains)\.Goodwill\([^;]*,\s*null\s*[,)]").Select(m => Path.GetFileName(f) + ": " + m.Value)).ToList();
        C(nulls.Count == 0, "no wrapper is given a null reason: " + string.Join("; ", nulls.Take(3)));

        // ================= The defs and their words =================

        string xmlPath = Path.Combine(root, "Defs", "HistoryEventDefs", "Rimfolk_GoodwillReasons.xml");
        C(File.Exists(xmlPath), "the reasons' defs are there");
        if (!File.Exists(xmlPath)) return n;
        var defs = XDocument.Load(xmlPath).Root!.Elements("HistoryEventDef")
            .ToDictionary(e => (string)e.Element("defName") ?? "", e => (string)e.Element("label") ?? "");
        foreach (var name in all)
        {
            C(defs.ContainsKey(name), name + " has its def");
            if (!defs.TryGetValue(name, out var label)) continue;
            C(label.Length > 0 && label.Length <= 45, name + ": a short label");
            C(label == label.ToLowerInvariant() && char.IsLower(label[0]), name + ": in lower case, as the game's are (\"" + label + "\")");
            C(!label.EndsWith(".") && !label.Contains("!") && !label.Contains("—") && !label.Contains("{"), name + ": no full stop, exclamation, dash or placeholder");
        }
        C(defs.Keys.All(all.Contains), "every def is a listed reason");
        C(defs.Values.Distinct().Count() == defs.Count, "each label is its own");
        C(defs[GoodwillReasonMath.Slight] == "a letter that slighted their god", "the slight's label is the one the player will read");

        // Every reason has its name in GoodwillWhy, and is used somewhere.
        string why = Read("World/GoodwillWhy.cs");
        var props = Regex.Matches(why, @"public static HistoryEventDef (\w+) => Of\(GoodwillReasonMath\.(\w+)\);").Select(m => (prop: m.Groups[1].Value, name: m.Groups[2].Value)).ToList();
        foreach (var name in all.Where(x => x != GoodwillReasonMath.TaskDone && x != GoodwillReasonMath.TaskFailed))
            C(props.Any(p => "Rimfolk_GW_" + p.name == name), name + " has its name in GoodwillWhy");
        C(props.All(p => p.prop == p.name), "each name in GoodwillWhy is the reason's own");
        string rest = string.Join("\n", files.Where(f => !f.EndsWith("GoodwillWhy.cs")).Select(File.ReadAllText));
        foreach (var p in props)
            C(Regex.IsMatch(rest, @"GoodwillWhy\." + p.prop + @"\b"), p.prop + " is given somewhere");
        C(why.Contains("public static HistoryEventDef ForEffects => current ?? Council;"), "an answer's effects give the council's choice when nothing else is set");
        C(Regex.IsMatch(rest, @"GoodwillWhy\.Task\("), "a task's reasons are given");

        // The effects' scope: a meeting's, an event's own and a story's ending give theirs; an answer the council's choice.
        C(Read("Strangers/Encounters.cs").Contains("using (GoodwillWhy.During(GoodwillWhy.Meeting))"), "a meeting's goodwill is for a meeting with one of theirs");
        C(Read("Happenings/IncidentWorker_Happening.cs").Contains("using (GoodwillWhy.During(GoodwillWhy.Event))")
          && Read("Happenings/Workers_StoryFlow.cs").Contains("using (GoodwillWhy.During(GoodwillWhy.Event))"), "an event's own effects and a story's ending give how the event turned out");
        C(Read("Dilemmas/Dilemmas.cs").Contains("WorldSituationWorker.Goodwill(ctx.faction, amount, GoodwillWhy.ForEffects)")
          && Read("Happenings/HappeningEffects.cs").Contains("GoodwillWhy.ForEffects") && Read("Happenings/Workers_Living.cs").Contains("GoodwillWhy.ForEffects"),
            "the goodwill effects ask the scope");

        // ================= Nothing noted twice, nothing another mod reads =================

        C(Read("World/Patch_FactionMemory.cs").Contains("if (reason == null || GoodwillReasonMath.Own(reason.defName) || !MemoryHooks.Listening) return;"),
            "the memory's hook leaves Rimfolk's own reasons alone");
        C(Read("Integrations/ModMemory.cs").Contains("if (reason == null || GoodwillReasonMath.Own(reason.defName) || !MemoryHooks.Listening) return;"),
            "the other mods' hook does too");
        string outsiders = Read("Outsiders/OutsiderOutcomes.cs");
        C(outsiders.Contains("FactionMemory.Note(f, \"gaveGift\"") && outsiders.Contains("GoodwillWhy.Gift") && !outsiders.Contains("HistoryEventDefOf.GaveGift"),
            "an outsider's gift is remembered once (its own note, not the game's gift as well)");
        var precepts = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("Rimfolk_GoodwillReasons.xml")).Where(f => Regex.IsMatch(File.ReadAllText(f), @"Rimfolk_GW_")).ToList();
        C(precepts.Count == 0, "no other def reads the reasons: " + string.Join(", ", precepts.Select(Path.GetFileName)));

        // ================= A slight lands with the answer =================

        string letters = Read("Gods/World/GodLetters.cs"), neighbours = Read("World/NeighbourLetters.cs");
        string answered = Between(letters, "public static void Answered(", "public static void AnswerIn(");
        string answerIn = Between(letters, "public static void AnswerIn(", "// ================= A festival's invitation");
        C(answered.Length > 0 && !answered.Contains("Goodwill(") && !answered.Contains("FactionMemory.Note"), "a slight read moves no goodwill and no memory yet");
        C(answered.Contains("d.stage = GoodwillReasonMath.SlightRead"), "a slight read waits for its answer");
        C(answerIn.Contains("StoryChains.Goodwill(f, GodWorldMath.SlightGoodwill, GoodwillWhy.Slight)") && answerIn.Contains("FactionMemory.Note(f, \"slighted\"")
          && answerIn.Contains("GoodwillReasonMath.SlightLands(x.stage, x.done)") && answerIn.Contains("d.done = true"), "the answer in: the slight lands, named, once");
        string owed = Between(neighbours, "private static void Owed(", "/// <summary>What a letter of kind words brings");
        string finish = Between(neighbours, "private static void Finish(", "// ---------------- Writing");
        string landed = Between(neighbours, "private static void Landed(", "private static void Owed(");
        C(owed.Contains("Landed(l, n);") && finish.Contains("Landed(l, n);"), "an answer in, or the letter done, lands what it says");
        C(landed.Contains("GodLetters.AnswerIn(l, f)") && landed.Contains("WorldSituationWorker.Goodwill(f, gw, GoodwillWhy.Letter, false)") && landed.Contains("l.goodwillDue = 0"),
            "the letter's own goodwill lands with its answer too, once, told by the answer");
        C(neighbours.Contains("if (tone != AnswerTone.Silence) l.goodwillDue = gw;") && !neighbours.Contains("if (gw != 0) WorldSituationWorker.Goodwill(n.faction, gw, false);"),
            "nothing lands the moment they read it");
        C(neighbours.Contains("Scribe_Values.Look(ref goodwillDue, \"goodwillDue\");"), "the goodwill waiting is saved with the letter");
        C(Between(neighbours, "case LetterStage.Visiting:", "case LetterStage.Guesting:").Contains("if (l.tripId >= 0 && !AnswerOut(l)) Landed(l, n);"),
            "an invitation's answer brought by a messenger lands when it is in");
        return n;
    }

    private static string Between(string s, string from, string to)
    {
        int a = s.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = s.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? s.Substring(a) : s.Substring(a, b - a);
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
