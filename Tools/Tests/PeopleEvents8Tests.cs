using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 8 (Peoples/Events8/; docs/peoples-events-proposals.md, items 71 to 82): their pure rules (the
/// observer's morning and the calmer count, the medal's deeds, the deserter's fine, the ration post's rations and price, the sleeper's
/// pay, the crew who come home, the landing party's watch, the gene taken out, the whole xenogerm, the hybrid's price, the field lab),
/// their defs and words, the player's mods they draw on, and the seams with the game and rounds 1 to 7.
/// </summary>
public static class PeopleEvents8Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 8: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        const int H = PeopleEvents8Math.HourTicks, D = PeopleEvents8Math.DayTicks;

        // ================= 71. An observer at the vote =================

        int poll = 10 * D + 7 * H, count = 10 * D + 18 * H;
        C(PeopleEvents8Math.ObserverDue(poll - 2 * H, poll, count) && PeopleEvents8Math.ObserverDue(poll + 3 * H, poll, count) && !PeopleEvents8Math.ObserverDue(poll - 3 * H, poll, count)
          && !PeopleEvents8Math.ObserverDue(count - 4 * H, poll, count) && !PeopleEvents8Math.ObserverDue(poll, -1, count), "the clerk comes on polling day morning, before the count");
        C(PeopleEvents8Math.BrawlFactor(true) == 0.5f && PeopleEvents8Math.BrawlFactor(false) == 1f, "with the clerk there a heckle comes to blows half as often");
        C(PeopleEvents8Math.ObserverChance <= 0.5f && PeopleEvents8Math.ObserverGapDays >= 20 && PeopleEvents8Math.RollMax <= 8 && PeopleEvents8Math.ObserverGoodwill > 0
          && PeopleEvents8Math.RegisterGoodwill > 0 && PeopleEvents8Math.ObserverWaitHours >= 24, "not every election; eight voters at most; the clerk waits out a count put off a day");

        // ================= 72. A medal =================

        C(PeopleEvents8Math.MedalDeed("Rimfolk_Deed_Rescue") && PeopleEvents8Math.MedalDeed("Rimfolk_Deed_HeldLine") && !PeopleEvents8Math.MedalDeed("Rimfolk_Deed_ManyKills")
          && !PeopleEvents8Math.MedalDeed(null), "a medal for a rescue under fire or the line held, not for kills");
        C(!PeopleEvents8Math.MedalFresh(100 * D, 100 * D - 6 * H) && PeopleEvents8Math.MedalFresh(100 * D, 100 * D - 13 * H) && PeopleEvents8Math.MedalFresh(100 * D, 96 * D)
          && !PeopleEvents8Math.MedalFresh(100 * D, 95 * D) && !PeopleEvents8Math.MedalFresh(100 * D, -1), "half a day after the fight, while it is four days fresh");
        C(PeopleEvents8Math.Newspaper(2, true) && PeopleEvents8Math.Newspaper(3, true) && !PeopleEvents8Math.Newspaper(1, true) && !PeopleEvents8Math.Newspaper(3, false),
            "the Republic's newspaper from the industrial age on, with Vanilla Books Expanded");
        C(PeopleEvents8Math.MedalChance <= 0.5f && PeopleEvents8Math.MedalGapDays >= 20 && PeopleEvents8Math.CitationTicks == 2500 && PeopleEvents8Math.MaxMedalDeeds >= 6,
            "not every deed; an hour's citation");

        // ================= 73. A fugitive of the Republic =================

        C(PeopleEvents8Math.Fine(1f) == 250 && PeopleEvents8Math.Fine(0.1f) == 150 && PeopleEvents8Math.Fine(9f) == 500 && PeopleEvents8Math.Fine(float.NaN) == 250, "the fine by the colony's wealth, within reason");
        C(PeopleEvents8Math.HandOverGoodwill == 8 && PeopleEvents8Math.ShelterGoodwill == -10 && PeopleEvents8Math.DetailMin >= 2 && PeopleEvents8Math.DetailMax <= 3,
            "handed over +8, sheltered -10, as proposed; a detail of two or three");

        // ================= 74. A ration post =================

        C(PeopleEvents8Math.RationPay(20) == 160 && PeopleEvents8Math.RationPay(-3) == 0 && PeopleEvents8Math.Cards(2) == 4 && PeopleEvents8Math.Cards(7) == 7 && PeopleEvents8Math.Cards(40) == 12,
            "the Republic's fixed price; a ration card for each of the colony, within reason");
        C(PeopleEvents8Math.PostPeople == 3 && PeopleEvents8Math.PostDays == 7 && PeopleEvents8Math.PostHour(8) && !PeopleEvents8Math.PostHour(21) && PeopleEvents8Math.HungryMax <= 3
          && PeopleEvents8Math.RationChance <= 0.5f && PeopleEvents8Math.HungryJoinChance <= 0.2f && PeopleEvents8Math.DenouncedChance <= 0.3f, "a clerk and two guards a week; the hungry by day, a few at a time");

        // ================= 75. The colony's gravship =================

        C(PeopleEvents8Math.GravGoodwill == 10 && PeopleEvents8Math.DeckParts <= 5 && PeopleEvents8Math.Officers == 2 && PeopleEvents8Math.PilotXp > 0f && PeopleEvents8Math.PanelCount > 0,
            "+10 as proposed; two officers, five parts at most");

        // ================= 76. A sleeper of theirs =================

        C(PeopleEvents8Math.SleeperPay(1f) == 200 && PeopleEvents8Math.SleeperPay(0.1f) == 150 && PeopleEvents8Math.SleeperPay(9f) == 400 && PeopleEvents8Math.SleeperGoodwill == 12
          && PeopleEvents8Math.KeptGoodwill < 0, "handed over: their pay and +12; kept: the Fleet minds a little");

        // ================= 77. The order to disembark =================

        C(PeopleEvents8Math.Joiners(0f) == 3 && PeopleEvents8Math.Joiners(0.999f) == 5 && PeopleEvents8Math.Joiners(float.NaN) == 3 && PeopleEvents8Math.Joiners(7f) == 5,
            "three to five of the crew come home with the colonist");
        C(ArisingMath.IsKind(ArisingMath.Disembarked) && !ArisingMath.Kinds.Contains(ArisingMath.Disembarked) && ArisingMath.Callings(ArisingMath.Disembarked).Length >= 2
          && ArisingMath.AtPeace(ArisingMath.DisembarkedGoodwill(3)) && ArisingMath.AtPeace(ArisingMath.DisembarkedGoodwill(0))
          && ArisingMath.DisembarkedGoodwill(0) > ArisingMath.DisembarkedGoodwill(3) && ArisingMath.Feeling(ArisingMath.DisembarkedGoodwill(3)) > 0,
            "the disembarked are a cause of release B's arising, never looked for by the day; warm, the warmer when none went to the colony");

        // ================= 78. A landing party =================

        C(PeopleEvents8Math.PartyMin == 4 && PeopleEvents8Math.PartyMax == 6 && PeopleEvents8Math.PartyMinDays == 5 && PeopleEvents8Math.PartyMaxDays == 7, "four to six crew, five to seven days");
        C(PeopleEvents8Math.BellHour(8) && PeopleEvents8Math.BellHour(20) && !PeopleEvents8Math.BellHour(10) && !PeopleEvents8Math.BellHour(0) && PeopleEvents8Math.SurveyHour(8)
          && !PeopleEvents8Math.SurveyHour(18) && PeopleEvents8Math.SleeperSickChance <= 0.2f && PeopleEvents8Math.DeckhandGoodwill < 0, "the bell at the change of the watch by day; the survey by day");

        // ================= 79. A gene taken out =================

        C(PeopleEvents8Math.Harmful(1, 0) && PeopleEvents8Math.Harmful(3, 0) && !PeopleEvents8Math.Harmful(0, 0) && !PeopleEvents8Math.Harmful(-2, 0) && !PeopleEvents8Math.Harmful(2, 1),
            "a harmful gene: one the body gains metabolism by, never archite");
        C(PeopleEvents8Math.OutPrice(1) == 230 && PeopleEvents8Math.OutPrice(2) == 300 && PeopleEvents8Math.OutPrice(20) == 600 && PeopleEvents8Math.OutPrice(-1) == 230 && PeopleEvents8Math.OutTicks == 5000,
            "the price by the gene's metabolism, two hours' work");

        // ================= 80. A whole xenogerm =================

        C(PeopleEvents8Math.XenogermPrice(12, 0) == 480 && PeopleEvents8Math.XenogermPrice(0, 0) == 400 && PeopleEvents8Math.XenogermPrice(100, 100) == 1500
          && PeopleEvents8Math.SilverOnly(480) == 480 + PeopleEvents8Math.Genepacks * PeopleEvents8Math.GenepackWorth && PeopleEvents8Math.Genepacks == 2, "the price by its genes; two genepacks, or their worth in silver");

        // ================= 81. A hybrid beast =================

        C(PeopleEvents8Math.HybridPrice(200f) == 240 && PeopleEvents8Math.HybridPrice(10f) == 150 && PeopleEvents8Math.HybridPrice(float.NaN) == 150, "its worth and a fifth, 150 at least");

        // ================= 82. A field lab =================

        C(PeopleEvents8Math.LabPeople == 3 && PeopleEvents8Math.LabDays == 7 && PeopleEvents8Math.SampleHour(9) && !PeopleEvents8Math.SampleHour(19) && PeopleEvents8Math.LooseChance <= 0.15f
          && PeopleEvents8Math.VatSickChance <= 0.15f && PeopleEvents8Math.MaxLabTasks >= 1 && PeopleEvents8Math.GenepackPrice > 0, "two sequencers and a vat-keeper a week; it turns rarely");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents8.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_VoteObserver", "Rimfolk_Evt_RepublicMedal", "Rimfolk_Evt_RepublicFugitive", "Rimfolk_Evt_RationPost", "Rimfolk_Evt_RationJoin",
            "Rimfolk_Evt_GravshipLog", "Rimfolk_Evt_FleetSleeperCasket", "Rimfolk_Evt_OrderToDisembark", "Rimfolk_Evt_LandingParty", "Rimfolk_Evt_GeneTakenOut", "Rimfolk_Evt_WholeXenogerm",
            "Rimfolk_Evt_HybridBeast", "Rimfolk_Evt_FieldLab" }), "thirteen events: nine drawn, four fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events8");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents8Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_VoteObserver"] = "Citizens", ["Rimfolk_Evt_RepublicMedal"] = "Citizens", ["Rimfolk_Evt_RepublicFugitive"] = "Citizens", ["Rimfolk_Evt_RationPost"] = "Citizens",
            ["Rimfolk_Evt_GravshipLog"] = "Crew", ["Rimfolk_Evt_FleetSleeperCasket"] = "Crew", ["Rimfolk_Evt_OrderToDisembark"] = "Crew", ["Rimfolk_Evt_LandingParty"] = "Crew",
            ["Rimfolk_Evt_GeneTakenOut"] = "Editors", ["Rimfolk_Evt_WholeXenogerm"] = "Editors", ["Rimfolk_Evt_HybridBeast"] = "Editors", ["Rimfolk_Evt_FieldLab"] = "Editors"
        };
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_VoteObserver"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_RepublicMedal"] = "PeopleEvents2.Fire(EventDef, map, f)",
            ["Rimfolk_Evt_RationPost"] = "PeopleEvents2.Fire(EventDef, home, f)", ["Rimfolk_Evt_RationJoin"] = "PeopleEvents2.Fire(JoinDef, map, p.Faction)"
        };
        var refusals = new HashSet<string> { "Rimfolk_Evt_RepublicFugitive" };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            if (callings.TryGetValue(name, out var calling))
                C(m.Success && m.Groups[1].Value.Contains("PeoplesMath." + calling) && (m.Groups[1].Value.Contains("PeoplesD.Friendly(") || m.Groups[1].Value.Contains("PeoplesD.Theirs(")),
                    $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.3f && chance <= 0.6f && int.Parse(e.Element("minRefireDays")!.Value) >= 40 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            bool AgeText(int a) => texts.Any(t => Regex.Match(t, @"^\[([^\]]*)\]").Groups[1].Value.Split('|', ',').Contains("age" + a));
            C(texts.Count >= 5 && Enumerable.Range(0, 4).All(AgeText), $"{name}: five or more texts, every age in its own words");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == "2", $"{name}: three answers, no answer the last");
            if (refusals.Contains(name))
                C(options.Last().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_FugitiveHandOver"), $"{name}: the last answer sends the deserter on with the detail");
            else C(options.Last().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"), $"{name}: the last answer sends them on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
            var silver = options.SelectMany(o => o.Descendants("li")).Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver").ToList();
            C(silver.All(li => li.Element("steward")?.Value == "false"), $"{name}: the silver carried out is the sum the card says");
        }
        var gravTexts = Ext(events["Rimfolk_Evt_GravshipLog"], "HappeningExtension")!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(gravTexts.All(t => !t.Contains("{X_")) && Ext(events["Rimfolk_Evt_GravshipLog"], "HappeningExtension")!.Element("mods")!.Elements("li").Single().Value == "Ludeon.RimWorld.Odyssey",
            "the Fleet's call about the gravship has no visitor at the gate, and only with Odyssey");
        C(Ext(events["Rimfolk_Evt_OrderToDisembark"], "HappeningExtension")!.Element("mods")!.Elements("li").Single().Value == "Ludeon.RimWorld.Royalty"
          && Ext(events["Rimfolk_Evt_HybridBeast"], "HappeningExtension")!.Element("mods")!.Elements("li").Single().Value == "vanillaexpanded.vgeneticse"
          && new[] { "Rimfolk_Evt_GeneTakenOut", "Rimfolk_Evt_WholeXenogerm" }.All(e => Ext(events[e], "HappeningExtension")!.Element("mods")!.Elements("li").Single().Value == "Ludeon.RimWorld.Biotech"),
            "the order needs Royalty (the Empire's titles), the hybrid Vanilla Genetics Expanded, the gene work Biotech");
        var xeno = Ext(events["Rimfolk_Evt_WholeXenogerm"], "HappeningExtension")!;
        C(xeno.Element("texts")!.Elements("li").Where(l => l.Value.Contains("{RELATION}")).All(l => l.Value.StartsWith("[faith]")) && code.Contains("tags.Add(\"faith\")")
          && code.Contains("ideo.PreferredXenotypes"), "a faith that holds to its own kinds says so on the card, by Biotech's preferred xenotypes");
        var fugitive = Ext(events["Rimfolk_Evt_RepublicFugitive"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(fugitive[0].Descendants("toGroup").Single().Value == "true" && code.Contains("Papers.Strike(f, hc.map)") && code.Contains("new LordJob_AssaultColony(f,")
          && code.Contains("if (f.HostileTo(Faction.OfPlayer)) RepublicFugitive.Fight(hc);"), "the fine to the detail; sheltered, the papers struck and the detail fights only if the Republic turns against the colony");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents8.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_RationPost", "Rimfolk_Camp_LandingParty", "Rimfolk_Camp_FieldLab" }), "three camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
            C(k.Descendants("li").Where(li => li.Parent?.Name.LocalName is "lines" or "momentLines").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1),
                $"{name}: every spoken line one sentence");
            var moments = k.Element("momentLines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(moments.All(t => code.Contains("\"" + t + "\"")), $"{name}: every moment line is said at a moment the code has");
            var marks = k.Element("farewell")!.Elements("li").SelectMany(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value.Split(',')).Where(t => t.Length > 0).Distinct().ToList();
            C(marks.All(t => code.Contains("marks.Add(\"" + t + "\")")), $"{name}: every farewell is for a mark the code gives");
            var askKeys = k.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).ToList();
            C(askKeys.Count(a => code.Contains("case \"" + a + "\"")) >= 1, $"{name}: its own asks are minded by its worker");
            C(k.Element("leaves")?.Value == "NoTrace", $"{name}: nothing of theirs left behind");
        }
        var post = campKinds["Rimfolk_Camp_RationPost"];
        C(post.Element("calling")!.Value == "citizens" && post.Element("people")!.Value == "3~3" && post.Element("days")!.Value == "7~7"
          && post.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "cards", "tend" })
          && post.Element("asks")!.Elements("li").First().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_RationSold"),
            "the ration post: a clerk and two guards a week; it buys food at the Republic's price, honours ration cards, and its hurt are tended");
        C(code.Contains("Papers.Holds(c.faction)") && code.Contains("HarmTwoMath.CampBrawlChance(false, 1f)") && code.Contains("x.HostileTo(c.faction)") && code.Contains("Picket.Site(map,"),
            "the cards for citizens only; the queue at the camp brawl's rate; the denounced turned away; the post by the road");
        var party = campKinds["Rimfolk_Camp_LandingParty"];
        C(party.Element("calling")!.Value == "crew" && party.Element("people")!.Value == "4~6" && party.Element("days")!.Value == "5~7" && party.Element("sleep")!.Value == "Hut"
          && F(party.Element("joinChance")!.Value) >= 0.2f && party.Element("joinTexts")!.Elements("li").All(l => l.Value.Contains("officer"))
          && party.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "medic", "tend" })
          && party.Element("asks")!.Elements("li").ElementAt(1).Descendants("doing").Single().Value == "tend",
            "the landing party: four to six crew, bunks under one roof; a deckhand asks to join against the officer's wish; they buy food and offer their medic");
        var lab = campKinds["Rimfolk_Camp_FieldLab"];
        C(lab.Element("calling")!.Value == "editors" && lab.Element("people")!.Value == "3~3" && lab.Element("days")!.Value == "7~7"
          && lab.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "gene", "sample", "genepack", "tend" })
          && lab.Element("asks")!.Elements("li").Take(3).All(a => a.Element("once")?.Value == "false"),
            "the field lab: two sequencers and a vat-keeper a week; gene work, the sample and genepacks open all week");

        // Their jobs and moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents8_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 12 && jobs.All(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv8Work" && code.Contains("\"" + Dn(j) + "\"")
                                            && new[] { "look", "quiet", "sparks", "cross", "dust" }.Contains(j.Descendants("mark").Single().Value)),
            "twelve jobs, each the round's own work at a place, given by its code, each with its mark");
        C(jobs.Where(j => j.Descendants("mark").Single().Value == "cross").Select(Dn).OrderBy(x => x)
              .SequenceEqual(new[] { "Rimfolk_PEv8_GeneOut", "Rimfolk_PEv8_LabGene", "Rimfolk_PEv8_LabSample", "Rimfolk_PEv8_Xenogerm" })
          && code.Contains("if (Mark == \"cross\" && on != null && on != pawn && on.Faction != null && on.Faction.IsPlayer) PeoplesG.Hold(on,"),
            "the gene work keeps the colonist still under the healing cross, round G's hold");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Keys.SequenceEqual(new[] { "Rimfolk_Thought_FairVote", "Rimfolk_Thought_InTheRegister", "Rimfolk_Thought_RepublicMedal", "Rimfolk_Thought_HeardCitation",
              "Rimfolk_Thought_ShipLogged", "Rimfolk_Thought_GaveTheOrder" }) && thoughts.Keys.All(t => code.Contains("\"" + t + "\""))
          && thoughts.Values.All(t => F(t.Descendants("baseMoodEffect").Single().Value) > 0f) && F(thoughts["Rimfolk_Thought_RepublicMedal"].Element("durationDays")!.Value) >= 30f,
            "six good moods; the medal a lasting memory");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        var active = cat.Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        bool From(string type, string def, string mod) => cat.Any(r => r.active && r.type == type && r.def == def && r.mod.Equals(mod, StringComparison.OrdinalIgnoreCase));
        C(PeopleEvents8Math.Hybrids.All(h => From("PawnKindDef", h, "vanillaexpanded.vgeneticse") && From("ThingDef", h, "vanillaexpanded.vgeneticse")),
            "the hybrids are Vanilla Genetics Expanded's own, in the player's list");
        var biotech = new[] { "Genie", "Highmate", "Hussar", "Pigskin", "Neanderthal", "Impid", "Yttakin", "Dirtmole", "Waster" };
        C(biotech.All(x => From("XenotypeDef", x, "ludeon.rimworld.biotech")) && From("XenotypeDef", "Starjack", "ludeon.rimworld.odyssey")
          && new[] { "AG_Helixien", "AG_Animusen", "AG_Fleetkind", "AG_Nereid" }.All(x => From("XenotypeDef", x, "sarg.alphagenes")) && From("XenotypeDef", "VREH_Uhlan", "vanillaracesexpanded.hussar")
          && PeopleEvents8Math.Xenotypes.All(x => active.Contains("XenotypeDef:" + x)), "the Works' kinds: Biotech's (the fallback), Odyssey's, Alpha Genes' and the hussars' own");
        string helix = File.ReadAllText(Path.Combine(root, "Defs", "PeopleDefs", "Rimfolk_People_HelixWorks.xml"));
        C(PeopleEvents8Math.Xenotypes.All(x => helix.Contains("<" + x + " ")), "a whole xenogerm only of the Works' own kinds");
        C(From("ThingDef", "VBE_Newspaper", "vanillaexpanded.vbookse") && code.Contains("\"VBE_Newspaper\""), "the Republic's newspaper is Vanilla Books Expanded's");
        C(From("ThingDef", "GravEngine", "ludeon.rimworld.odyssey") && From("ThingDef", "GravlitePanel", "ludeon.rimworld.odyssey") && From("HediffDef", "PilotAssistant", "ludeon.rimworld.odyssey")
          && code.Contains("\"PilotAssistant\"") && code.Contains("ThingDefOf.GravlitePanel"), "Odyssey's grav engine, its gravlite panels and its pilot's implant");
        var items = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 6 && c[2] == "yes").ToList();
        C(items.Count(c => c[6].Split(',').Contains("GravshipUpgrade")) >= 4 && items.Any(c => c[6].Split(',').Contains("GravshipUpgrade") && c[1] == "vanillaexpanded.gravship")
          && code.Contains("UpgradeTag = \"GravshipUpgrade\""), "the hull's parts by the ship's upgrade tag, Odyssey's and Vanilla Gravship Expanded's");
        C(active.Contains("ThingDef:AncientCryptosleepCasket") && active.Contains("ThingDef:MealSurvivalPack") && active.Contains("HediffDef:XenogerminationComa") && active.Contains("HediffDef:Flu")
          && active.Contains("RoyalTitleDef:Knight") && active.Contains("PreceptDef:PreferredXenotype"), "the game's caskets, ration packs, coma, flu, knights and preferred xenotypes");
        var kinds = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").Select(File.ReadAllText));
        var named = Regex.Matches(code, "\"(Rimfolk_(Republic|HollowFleet|HelixWorks)_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(named.Count >= 8 && named.All(k => kinds.Contains("<defName>" + k + "</defName>")), "every kind of person named is one of the people's own");

        // ================= Their words =================

        var keyedFile = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents8.xml"));
        var keyed = XDocument.Parse(keyedFile).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv8_\\w+[A-Za-z])\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 100 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        var built = new[] { "CC_PEv8_Observer_nocount", "CC_PEv8_Observer_late", "CC_PEv8_MedalBack_2", "CC_Memory_Deed_medal", "CC_Memory_Deed_medal_Name", "CC_Memory_Deed_medal_Many",
                "CC_Arise_disembarked", "CC_Arise_disembarked_Title", "CC_Arise_disembarked_Plain", "CC_Arise_disembarked_Warm", "CC_Arise_disembarked_Cool", "CC_Arise_H_disembarked",
                "CC_Arise_Card_disembarked" }
            .Concat(ArisingMath.Callings(ArisingMath.Disembarked).Select(c => "CC_Arise_disembarked_" + c)).ToList();
        C(built.All(keyed.Contains), "the words put together by the code are keyed: the clerk going home, the colonist's reply, the medal's memory, the disembarked people's letter and card");
        var texts8 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents8.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents8.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents8.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents8_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts8.Contains('—') && !texts8.Contains('!') && !Regex.IsMatch(texts8, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts8.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts8, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts8, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(new[] { "an observer at the vote", "a medal", "a fugitive of the republic", "a ration post", "the colony's gravship", "a sleeper of theirs", "the order to disembark", "a landing party",
            "a gene taken out", "a whole xenogerm", "a hybrid beast", "a field lab" }.All(peoples.Contains), "each people's Almanac page says what it does now");

        // ================= The seams =================

        string seven = Read("Peoples", "Events7", "PeopleEvents7.cs");
        C(seven.Contains("return PeopleEvents8.Hour(v, comp, now);") && seven.Contains("PeopleEvents8.Hourly(now)"), "round 8's visits and watch wait on round 1's hour, through rounds 2 to 7");
        var visitKinds = Regex.Matches(code, @"public const string (Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(visitKinds.Count == 6 && visitKinds.All(k => k.StartsWith("pev:")) && visitKinds.Distinct().Count() == 6, "their visits are the events' own kind (\"pev:\"), none twice");
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4", "Events5", "Events6", "Events7" }.SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs"))
            .Select(File.ReadAllText));
        C(visitKinds.All(k => !earlier.Contains("\"" + k + "\"")), "none of their visits' kinds is an earlier round's");
        string season = Read("Peoples", "Events2", "SeasonAway.cs");
        C(season.Contains("[OrderToDisembark.AwayKey] = new DisembarkAway()") && code.Contains("public const string AwayKey = \"disembark\";") && code.Contains("SeasonAway.Send(AwayKey"),
            "the colonist on the road to the hull is round 2's season away");
        C(Read("Harm", "HustingsBrawls.cs").Contains("PeopleEvents8Math.BrawlFactor(VoteObserver.Watching(e.map))"), "the hustings' blows half as often while the clerk watches");
        C(Read("Peoples", "PeoplesFPatches.cs").Contains("!RelayMast.Warn(fi) && !LandingParty.Warn(fi)"), "the landing party's watch warns of a raid through the mast's own hook");
        C(Read("World", "FactionMemory.cs").Contains("[\"medal\"]") && code.Contains("KinMemory.Note(f, GiftKind, a, deed)") && code.Contains("GiftKind = \"medal\""),
            "the Republic remembers a medal by its colonist's name");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:RationPost\", \"them:camp:LandingParty\", \"them:camp:FieldLab\""), "the facts service knows the round's camps");
        C(Read("..", "..", "Tools", "DefValidator", "Program.cs").Contains("\"FED\", \"PEOPLE\", \"SURVEYED\", \"SAMPLED\""), "the def check knows the camps' own placeholders");
        string arising = Read("Peoples", "Arising.cs");
        C(arising.Contains("case ArisingMath.Disembarked:") && arising.Contains("ArisingMath.DisembarkedGoodwill(c.joined)") && code.Contains("Arising.Arise(cause)") && code.Contains("if (fleet == null || !hull.Valid || !Arising.On) return null;"),
            "the rest of the crew arise as a people of their own through release B's own arising, only while new peoples may arise");
        C(code.Contains("HelixGenes.Takers(") && code.Contains("HelixGenes.Fits(") && code.Contains("HelixSample.Rarest(") && code.Contains("PeoplesG.FaultDef") && code.Contains("Reputations.Tracker")
          && code.Contains("Deeds.TextOf(") && code.Contains("RepublicRadio.HasRadio(") && code.Contains("Papers.Holds(") && code.Contains("OfficeTracker.For(") && code.Contains("ElectionPhase.Count")
          && code.Contains("DilemmaEffect_") && code.Contains("CampTracker.About(") && code.Contains("Couriers.PaySilver(") && code.Contains("GeneUtility.UpdateXenogermReplication(")
          && code.Contains("GetNextAncientCryptosleepCasketGroupID()") && code.Contains("PeopleEvents.MayGive(") && code.Contains("PeopleEventsBook.Gave("),
            "the shapes reused: round G's gene work, sample and gene fault, the deeds, the radio and the papers, the election, the camps' asks, the couriers, the game's own xenogerm mark, a casket apart from its group, the gifts' rule");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure|Fight)\(([^;]*?)HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampBrawl", "CampFever", "DroversBull" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("IllnessBudget.Allows(") && code.Contains("HarmBook.Instance?.Came(HarmKinds.DroversBull"),
            "no harm kind of their own: the queue at the camp brawl's rate, the sleeper's and the vat-grown's illness the camps' fever, the lab's beast the camps' beasts' kind");
        C(!code.Contains("[HarmonyPatch"), "no game hooks of their own");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say(") && code.Contains("Speech.Say("),
            "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 3, "the code-fired comings read the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents8Book.cs"));
        C(book.Contains("Note(elections, key, 6)") && book.Contains("PeopleEvents8Math.MaxMedalDeeds") && book.Contains("Note(famines, key, 6)") && book.Contains("sleepers.Count > 4")
          && book.Contains("Note(disembarked, f.loadID, 8)"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 8: ").Count == 9, "nine dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "elections", "medalDeeds", "famines", "sleepers", "disembarked" }.All(x => save.Contains("Peoples/Events8/PeopleEvents8Book.cs:" + x))
          && save.Contains("Peoples/Events8/RationPost.cs:hungry") && save.Contains("Peoples/Events8/LandingParty.cs:crew") && save.Contains("Peoples/Events8/FieldLab.cs:tasks"),
            "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.Concat(campKinds.Values).SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", ""))
                .Where(v => v != null && v.StartsWith("DilemmaEffect_") && Regex.IsMatch(code, @"class " + v + @"\b") && v != "DilemmaEffect_FleetSleeperShut").Distinct().All(v => audit.Contains("\"" + v + "\"")),
            "the play-out audit knows each answer of theirs that plays out on the map");
        return n;
    }

    private static string Dn(XElement e) => e.Element("defName")?.Value ?? "";

    private static XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);

    private static List<(string type, string def, string mod, bool active)> catalogue;

    /// <summary>The player's mods' every def (docs/player-mods/defs.tsv.gz): type, name, package, active.</summary>
    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
