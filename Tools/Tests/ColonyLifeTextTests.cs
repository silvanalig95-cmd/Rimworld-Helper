using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// The content update's text variety of colony life (A31 to A37; the player's go of 20 November 2026): every pool the
/// proposal counted thin holds at least the lines it was brought up to, its own lines and those other files join to it;
/// the new lines use only the people and values their pool's own lines do, are plain (no em dash, no exclamation mark)
/// and, where said aloud, one sentence; a changed life goal's entry is written with what changed it, and its tagged lines
/// name only those two.
/// </summary>
public static class ColonyLifeTextTests
{
    private static readonly string[] NewFiles =
    {
        "Defs/FlavorPoolDefs/Rimfolk_Lines_ColonyLife_Journals.xml", "Defs/FlavorPoolDefs/Rimfolk_Talk_Lines_Teasing.xml",
        "Defs/FlavorPoolDefs/Rimfolk_DecreeLines_ColonyLife.xml", "Defs/FlavorPoolDefs/Rimfolk_Lines_ColonyLife_Wishes.xml",
        "Defs/InteractionDefs/Rimfolk_LogLines_ColonyLife.xml", "Defs/FlavorPoolDefs/Rimfolk_Lines_ColonyLife_Letters.xml",
    };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "colony life's text: " + what);
        }

        var docs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .Select(f => (file: Path.GetRelativePath(root, f).Replace('\\', '/'), el: XDocument.Load(f).Root!)).ToList();
        var defs = docs.SelectMany(d => d.el.Elements().Select(e => (d.file, e))).ToList();
        string Name(XElement e) => (string)e.Element("defName") ?? "";
        List<string> Lines(XElement block) => block == null ? new List<string>() : block.Descendants("li").Select(li => li.Value).ToList();
        string Body(string s) => Regex.Replace(s ?? "", @"^\[[^\]]*\]\s*", "");
        HashSet<string> Roots(IEnumerable<string> lines) =>
            new HashSet<string>(lines.SelectMany(l => Regex.Matches(Body(l), @"\{([A-Z]+)(_[A-Za-z]+)?\}").Select(m => m.Groups[1].Value)));
        bool OneSentence(string s)
        {
            string b = Body(s);
            for (int i = 0; i < b.Length - 1; i++)
                if (".!?".IndexOf(b[i]) >= 0 && b[i + 1] == ' ') return false;
            return true;
        }

        // ---------- A flavour pool's lines, its own and those joined to it ----------
        var poolOwn = new Dictionary<string, List<string>>();
        var poolAll = new Dictionary<string, List<string>>();
        foreach (var (file, e) in defs)
        {
            string pool = e.Name.LocalName == "ColonyCouncil.FlavorPoolDef" ? Name(e) : e.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" ? (string)e.Element("pool") : null;
            if (pool == null) continue;
            var lines = Lines(e.Element("lines"));
            if (!poolAll.TryGetValue(pool, out var all)) poolAll[pool] = all = new List<string>();
            all.AddRange(lines);
            if (!NewFiles.Contains(file))
            {
                if (!poolOwn.TryGetValue(pool, out var own)) poolOwn[pool] = own = new List<string>();
                own.AddRange(lines);
            }
        }
        var atLeast = new Dictionary<string, int>
        {
            // A31, journals
            ["Council_Journal_Elected"] = 24, ["Council_Journal_GoalChanged"] = 24, ["Council_Journal_MovedDown"] = 12, ["Council_Journal_Epithet"] = 36,
            ["Council_Journal_TieFormed_Comrades"] = 34, ["Council_Journal_TieFormed_Friends"] = 37, ["Council_Journal_Anniversary"] = 33,
            ["Council_Journal_Homecoming"] = 19,
            // A34, the council's answers to wishes
            ["Council_Pool_MeToo"] = 42, ["Council_Pool_Bitter"] = 34, ["Council_Pool_BrokenPromise"] = 31, ["Council_Pool_PassedOver"] = 32,
            ["Council_Pool_SelfHelp"] = 35, ["Council_Pool_Promised"] = 36,
            // A37, the year letter's last word and the anniversary's
            ["Council_Pool_AnnalsClose"] = 46, ["Council_Pool_Anniversary"] = 27,
        };
        foreach (var kv in atLeast)
        {
            int have = poolAll.TryGetValue(kv.Key, out var l) ? l.Count : 0;
            C(have >= kv.Value, $"{kv.Key} has {kv.Value} lines or more ({have})");
        }

        // ---------- Talk: the teasing opens and the closing words ----------
        var talk = defs.Where(x => x.e.Name.LocalName == "ColonyCouncil.TalkLinesDef").Select(x => x.e).ToList();
        int TalkCount(string field, string about, string turn, string outcome) => talk
            .Where(t => (string)t.Element(field) == about && (string)t.Element("turn") == turn && (t.Element("outcomes")?.Elements("li").Any(o => o.Value == outcome) ?? true))
            .Sum(t => Lines(t.Element("lines")).Count);
        foreach (var topic in new[] { "Food", "Work", "Growing", "Council", "Future", "Project", "Outside", "Art", "Prisoners", "Calendar", "Drugs", "Wealth", "Tech", "Raid",
                     "Children", "Love", "Camp", "Health" })
        {
            int have = TalkCount("topic", "Rimfolk_Topic_" + topic, "Open", "Tease");
            C(have >= 14, $"teasing about {topic}: 14 openings or more ({have})");
        }
        foreach (var (mem, turn, outcome, min) in new[] { ("Rimfolk_Mem_Nursed", "Close", "Reminisce", 10), ("Rimfolk_Mem_CloseCall", "Close", "Reminisce", 10),
                     ("Rimfolk_Mem_Repaid", "Close", "Reminisce", 10), ("Rimfolk_Mem_Downpour", "Close", "Joke", 10), ("Rimfolk_Mem_Snowball", "Close", "Joke", 10),
                     ("Rimfolk_Mem_JokeHeard", "Close", "Joke", 16), ("Rimfolk_Mem_ChildFound", "Open", "Reminisce", 17), ("Rimfolk_Mem_ChildFound", "Reply", "Reminisce", 17),
                     ("Rimfolk_Mem_ChildFound", "Close", "Reminisce", 13) })
        {
            int have = TalkCount("memory", mem, turn, outcome);
            C(have >= min, $"{mem}, {turn.ToLowerInvariant()}: {min} lines or more ({have})");
        }

        // ---------- A33, the decrees' voices for and against ----------
        var decrees = defs.Where(x => x.e.Name.LocalName == "ColonyCouncil.DecreeDef").ToDictionary(x => Name(x.e), x => x.e);
        int Side(string decree, string side) => Lines(decrees.TryGetValue(decree, out var d) ? d.Element(side) : null).Count
            + defs.Where(x => x.e.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" && (string)x.e.Element("decree") == decree).Sum(x => Lines(x.e.Element(side)).Count);
        foreach (var (decree, side, min) in new[] { ("Rimfolk_Decree_EveryFaith", "support", 10), ("Rimfolk_Decree_EveryFaith", "oppose", 9),
                     ("Rimfolk_Decree_Foraging", "support", 11), ("Rimfolk_Decree_Foraging", "oppose", 9), ("Rimfolk_Decree_SavePower", "support", 11),
                     ("Rimfolk_Decree_SavePower", "oppose", 9), ("Rimfolk_Decree_WoodForWinter", "support", 12), ("Rimfolk_Decree_WoodForWinter", "oppose", 9),
                     ("Council_Decree_OneFaith", "support", 10), ("Council_Decree_OneFaith", "oppose", 13), ("Rimfolk_Decree_HuntingSeason", "oppose", 10),
                     ("Council_Decree_Rest", "oppose", 13) })
        {
            int have = Side(decree, side);
            C(have >= min, $"{decree}, {side}: {min} lines or more ({have})");
        }

        // ---------- A35, the social log: roots of the council's talk, and what the gossip was about ----------
        var interactions = defs.Where(x => x.e.Name.LocalName == "InteractionDef").ToDictionary(x => Name(x.e), x => x.e);
        var logLines = defs.Where(x => x.e.Name.LocalName == "ColonyCouncil.LogLinesDef").Select(x => x.e).ToList();
        int Roots_(IEnumerable<string> rules) => rules.Count(r => r.StartsWith("r_logentry->"));
        foreach (var (inter, min) in new[] { ("Council_Gossip_Devoted", 16), ("Council_Gossip_Bitter", 17), ("Council_Decree_Gripe", 15), ("Council_Decree_Argue", 15) })
        {
            var own = Lines(interactions.TryGetValue(inter, out var i) ? i.Element("logRulesInitiator")?.Element("rulesStrings") : null);
            var more = logLines.Where(l => (string)l.Element("interaction") == inter).SelectMany(l => Lines(l.Element("rulePack")?.Element("rulesStrings")));
            int have = Roots_(own.Concat(more));
            C(have >= min, $"{inter}: {min} shapes of the log line or more ({have})");
        }
        var packs = defs.Where(x => x.e.Name.LocalName == "RulePackDef").ToDictionary(x => Name(x.e), x => x.e);
        foreach (var (pack, min) in new[] { ("Rimfolk_GossipTopic_Jailbreak", 8), ("Rimfolk_GossipTopic_Binge", 8), ("Rimfolk_GossipTopic_DugUpDead", 8),
                     ("Rimfolk_GossipTopic_RanWild", 8), ("Rimfolk_GossipTopic_SteppedDown", 6) })
        {
            var own = Lines(packs.TryGetValue(pack, out var p) ? p.Element("rulePack")?.Element("rulesStrings") : null);
            var more = logLines.Where(l => (string)l.Element("pack") == pack).SelectMany(l => Lines(l.Element("rulePack")?.Element("rulesStrings")));
            int have = own.Concat(more).Count(r => r.StartsWith("gossip_topic->"));
            C(have >= min, $"{pack}: {min} things the gossip was about or more ({have})");
        }

        // ---------- A36, social events; A37, the seats' first words ----------
        foreach (var file in new[] { "Defs/SocialEventDefs/Rimfolk_SocialEvents_Lives.xml", "Defs/SocialEventDefs/Rimfolk_SocialEvents_Quirks.xml",
                     "Defs/SocialEventDefs/Council_SocialEvents_Lives.xml" })
            foreach (var ev in docs.First(d => d.file == file).el.Elements("ColonyCouncil.SocialEventDef"))
            {
                var texts = Lines(ev.Element("texts"));
                C(texts.Count >= 4, $"{Name(ev)}: four texts or more ({texts.Count})");
                var allowed = Roots(texts.Take(texts.Count - 2));
                allowed.UnionWith(new[] { "A", "B" });
                foreach (var t in texts.Skip(texts.Count - 2))
                    C(Roots(new[] { t }).IsSubsetOf(allowed) && !t.Contains('—') && !t.Contains('!'), $"{Name(ev)}: '{t}' uses its event's people and values, in plain words");
            }
        var seats = defs.Where(x => x.file.StartsWith("Defs/OfficeDefs/Council_Offices") && x.e.Name.LocalName == "ColonyCouncil.OfficeDef").Select(x => x.e).ToList();
        C(seats.Count == 12, $"the council's twelve seats ({seats.Count})");
        foreach (var seat in seats)
        {
            var words = Lines(seat.Element("elected"));
            C(words.Count >= 6, $"{Name(seat)}: six first words or more ({words.Count})");
            C(words.All(w => w.Contains("{PAWN_nameDef}") && Roots(new[] { w }).SetEquals(new[] { "PAWN" })),
                $"{Name(seat)}: every first word is about the new holder");
        }

        // ---------- The new lines: their pool's people and values, plain, one sentence where said aloud ----------
        foreach (var file in NewFiles)
        {
            var el = docs.First(d => d.file == file).el;
            foreach (var ext in el.Elements("ColonyCouncil.FlavorExtensionDef"))
            {
                string pool = (string)ext.Element("pool");
                var lines = Lines(ext.Element("lines")).Concat(Lines(ext.Element("support"))).Concat(Lines(ext.Element("oppose"))).ToList();
                foreach (var l in lines)
                    C(!l.Contains('—') && !l.Contains('!'), $"{Name(ext)}: plain, no dash or exclamation: {l}");
                if (pool != null)
                {
                    var allowed = poolOwn.TryGetValue(pool, out var own) ? Roots(own) : new HashSet<string>();
                    allowed.Add("PAWN");
                    foreach (var l in lines)
                        C(Roots(new[] { l }).IsSubsetOf(allowed), $"{Name(ext)}: only the people and values {pool}'s own lines use: {l}");
                }
                else
                    foreach (var l in lines)
                        C(Roots(new[] { l }).IsSubsetOf(new[] { "PAWN" }), $"{Name(ext)}: a decree's voice names only its speaker: {l}");
                // Said aloud: the decrees' voices, the answers to wishes, the anniversary.
                bool aloud = ext.Element("decree") != null || (pool != null && pool.StartsWith("Council_Pool_") && pool != "Council_Pool_AnnalsClose");
                if (aloud)
                    foreach (var l in lines)
                        C(OneSentence(l), $"{Name(ext)}: one sentence, said aloud: {l}");
            }
            foreach (var t in el.Elements("ColonyCouncil.TalkLinesDef"))
                foreach (var l in Lines(t.Element("lines")))
                    C(!l.Contains('—') && !l.Contains('!'), $"{Name(t)}: plain, no dash or exclamation: {l}");
            foreach (var log in el.Elements("ColonyCouncil.LogLinesDef"))
                foreach (var r in Lines(log.Element("rulePack")?.Element("rulesStrings")))
                    C(!r.Contains('—') && !r.Contains('!'), $"{Name(log)}: plain, no dash or exclamation: {r}");
        }

        // ---------- A changed life goal, written with what changed it ----------
        string asp = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Aspirations", "Aspirations.cs"));
        string goalMath = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Aspirations", "GoalMath.cs"));
        C(asp.Contains("WriteTagged(p, \"Council_Journal_GoalChanged\", new[] { pick.spark }"), "a changed goal's entry is tagged by the spark that changed it");
        C(goalMath.Contains("spark == \"downed\" || spark == \"loss\""), "only a brush with death or a loss changes a goal");
        var goalTags = poolAll["Council_Journal_GoalChanged"].Select(l => Regex.Match(l, @"^\[([^\]]*)\]")).Where(m => m.Success)
            .SelectMany(m => m.Groups[1].Value.Split(',')).Select(t => t.Trim()).ToList();
        C(goalTags.Count(t => t == "loss") >= 6 && goalTags.Count(t => t == "downed") >= 6, "lines for a goal changed by a loss and by a brush with death, six or more each");
        C(goalTags.All(t => t == "loss" || t == "downed" || t == "afterraid"), "a changed goal's lines are tagged only by what can change it (and the old [afterraid])");
        return n;
    }
}
