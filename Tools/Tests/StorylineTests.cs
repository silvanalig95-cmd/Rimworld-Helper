using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Storylines, the frame and round 1 (docs/quests-proposals.md; Storylines/; DESIGN.md "Quests and storylines"; round 2's
/// four are StorylineTests2, every storyline's shape is checked here): the pace (one at a time,
/// a second only after the first has run a season), the branching rules of the factor's goods, the beast coming down the
/// valley and the children from the burned village (every part and outcome leads to a part or an ending, a few days to two
/// weeks apart, the long waits quiet), the plain ending when the people who asked are gone, the numbers each plays with,
/// a storyline played out from its first part to its ending, and the mod's side: each part's event and words, the cards
/// and their answers, the endings' words, the plain-words rules, the seams with the missions, the save and the World page.
/// </summary>
public static class StorylineTests
{
    private const int Day = StorylineMath.DayTicks;

    /// <summary>The outcomes each part can end with (besides declined, gone and ended), as each storyline's own rules list them.</summary>
    private static string[] Outcomes(string kind, string part) => StorylineMath.Outcomes(kind, part);

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "storylines: " + what);
        }

        // ================= The storylines =================

        C(StorylineMath.Kinds.SequenceEqual(new[] { "factor", "sickness", "herd", "beast", "relic", "quarrel", "children" }),
            "round 1's three and round 2's four, in their order: S1, S2, S3, S4, S5, S7, S8 (" + string.Join(", ", StorylineMath.Kinds) + ")");
        C(StorylineFlows.All.Select(f => f.Number).Distinct().Count() == StorylineFlows.All.Length && StorylineFlows.All.All(f => StorylineFlows.Of(f.Kind) == f),
            "each storyline's rules found once, by its own number");
        // The round-1 rules moved out of StorylineMath's switches: their endings with a letter are as before.
        C(StorylineMath.EndingHasLetter("factor", "share") && StorylineMath.EndingHasLetter("factor", "guards") && !StorylineMath.EndingHasLetter("factor", "madegood")
          && StorylineMath.EndingHasLetter("beast", "away") && !StorylineMath.EndingHasLetter("beast", "killed") && StorylineMath.EndingHasLetter("children", "gone")
          && !StorylineMath.EndingHasLetter("children", "grown"), "the endings with a letter of their own, as round 1 had them");
        foreach (var k in StorylineMath.Kinds)
        {
            C(StorylineMath.Parts(k).Length >= 2 && StorylineMath.Parts(k)[0] == StorylineMath.FirstPart(k), k + ": its parts, the first first");
            C(StorylineMath.Endings(k).Contains(StorylineMath.Gone) && StorylineMath.Endings(k).Contains(StorylineMath.Faded) && StorylineMath.Endings(k).Contains("declined"),
                k + ": a plain ending for its people gone, for one that can't go on, and for one turned down");
        }

        // ================= The pace =================

        StorylineMath.Run R(string kind, int startedDay, bool running = true, bool quiet = false, int endedDay = -1) =>
            new StorylineMath.Run { kind = kind, started = startedDay * Day, running = running, quiet = quiet, ended = endedDay < 0 ? -1 : endedDay * Day };
        int now = 100 * Day;
        C(StorylineMath.CanStart("factor", now, new StorylineMath.Run[0]), "with none running, one may start");
        C(!StorylineMath.CanStart("beast", now, new[] { R("factor", 95) }), "one at a time: not while another has run less than a season");
        C(StorylineMath.CanStart("beast", now, new[] { R("factor", 84) }), "a second once the first has run a season (15 days)");
        C(!StorylineMath.CanStart("beast", now, new[] { R("factor", 60), R("children", 70) }), "never three: two at most");
        C(!StorylineMath.CanStart("factor", now, new[] { R("factor", 20) }), "never two of a kind");
        C(!StorylineMath.CanStart("factor", now, new[] { R("factor", 20, false, false, 70) }) && StorylineMath.CanStart("factor", now, new[] { R("factor", 20, false, false, 39) }),
            "a kind rests 60 days after it ended");
        C(StorylineMath.CanStart("beast", now, new[] { R("children", 95, true, true) }), "one waiting quietly a season for its next part doesn't hold another back");
        C(StorylineMath.CanStart("factor", now, new[] { R("children", 60, true, true), R("beast", 70) }) && !StorylineMath.CanStart("factor", now, new[] { R("children", 60, true, true), R("beast", 95) }),
            "a quiet one is not counted; the active one still holds a new one back for a season");
        C(StorylineMath.SeasonDays == 15f && StorylineMath.MaxRunning == 2, "a season is the game's quadrum, two at most");

        // ================= What comes next: every part and outcome leads somewhere =================

        foreach (var kind in StorylineMath.Kinds)
            foreach (var part in StorylineMath.Parts(kind))
            {
                C(Outcomes(kind, part).Length > 0, kind + " " + part + ": its outcomes");
                var every = Outcomes(kind, part).Concat(new[] { StorylineMath.Declined, StorylineMath.Gone, StorylineMath.Ended });
                foreach (var o in every)
                    foreach (float roll in new[] { 0.05f, 0.95f })
                        foreach (int times in new[] { 1, 2, 3 })
                        {
                            var s = StorylineMath.Next(kind, part, o, times, roll);
                            string at = kind + " " + part + " " + o + " (" + times + ", " + roll + ")";
                            C((s.part == null) != (s.ending == null), at + ": a part or an ending, never both or neither");
                            if (s.part != null) C(StorylineMath.Parts(kind).Contains(s.part), at + ": the next part is one of its own (" + s.part + ")");
                            if (s.ending != null) C(StorylineMath.Endings(kind).Contains(s.ending), at + ": the ending is one of its own (" + s.ending + ")");
                            C(s.min >= 0f && s.max >= s.min, at + ": a wait that makes sense");
                            if (s.part != null && !s.quiet) C(s.min >= 1f && s.max <= 14f, at + ": the next part a few days to two weeks later (" + s + ")");
                            if (s.quiet) C(s.min >= StorylineMath.SeasonDays - 2f, at + ": a quiet wait is a season or more");
                            if (s.ending != null) C(s.max <= 7f, at + ": an ending comes within a week");
                            if (o == StorylineMath.Gone) C(s.ending == StorylineMath.Gone && s.max == 0f, at + ": its people gone, it ends now, plainly");
                        }
            }

        // The factor's goods.
        var nx = StorylineMath.Next("factor", "keep", "kept", 1, 0.5f);
        C(nx.part == "rival", "kept well: the rival's offer");
        C(StorylineMath.Next("factor", "keep", "lost", 1, 0.5f).part == "makegood", "some lost: make good what was lost");
        C(StorylineMath.Next("factor", "keep", "declined", 1, 0.5f).ending == "declined", "turned down: it ends");
        var sold = StorylineMath.Next("factor", "rival", "sold", 1, 0.1f);
        C(sold.part == "debt" && sold.flag == "foundout", "sold, and she finds out: the debt");
        C(StorylineMath.Next("factor", "rival", "sold", 1, 0.9f).ending == "neverknew", "sold, and she never knows");
        int found = Enumerable.Range(0, 1000).Count(i => StorylineMath.Next("factor", "rival", "sold", 1, i / 1000f).part == "debt");
        C(found > 600 && found < 800, "she finds out about seven times in ten (" + found + ")");
        C(new[] { "refused", "told", "declined" }.All(o => StorylineMath.Next("factor", "rival", o, 1, 0.5f).ending == "share"), "refused, told or never met: she comes and brings a share every season");
        C(StorylineMath.Next("factor", "makegood", "paid", 1, 0.5f).ending == "madegood" && StorylineMath.Next("factor", "makegood", "unpaid", 1, 0.5f).ending == "unpaid"
          && StorylineMath.Next("factor", "makegood", "declined", 1, 0.5f).ending == "unpaid", "made good, or no traders for a year");
        C(StorylineMath.Next("factor", "debt", "paid", 1, 0.5f).ending == "debtpaid" && StorylineMath.Next("factor", "debt", "unpaid", 1, 0.5f).ending == "guards"
          && StorylineMath.Next("factor", "debt", "declined", 1, 0.5f).ending == "guards", "the debt paid, or her guards at the gate");

        // The beast.
        foreach (var how in new[] { "ready", "partly", "unready" })
        {
            var r = StorylineMath.Next("beast", "ready", how, 1, 0.5f);
            C(r.part == "signs" && r.flag == how, "got " + how + ": the signs come next, remembered as " + how);
        }
        C(StorylineMath.Next("beast", "ready", "declined", 1, 0.5f).ending == "declined", "the warning turned down: the hunters turn it away");
        C(StorylineMath.Next("beast", "signs", "found", 1, 0.5f) is var f && f.part == "beast" && f.flag == "found"
          && StorylineMath.Next("beast", "signs", "missed", 1, 0.5f).flag == "missed" && StorylineMath.Next("beast", "signs", "declined", 1, 0.5f).part == "beast",
            "the signs looked at or not: the beast comes either way");
        C(StorylineMath.Next("beast", "beast", "killed", 1, 0.5f).ending == "killed", "killed: the trophy and the honour");
        var back = StorylineMath.Next("beast", "beast", "driven", 1, 0.5f);
        C(back.part == "beast" && back.quiet && back.flag == "back" && back.min >= 13f && back.max <= 17f, "driven off: it comes back next season, quietly");
        C(StorylineMath.Next("beast", "beast", "left", 2, 0.5f).part == "beast" && StorylineMath.Next("beast", "beast", "driven", 3, 0.5f).ending == "away",
            "it comes back at most twice, then stays away");
        C(StorylineMath.Next("beast", "beast", "declined", 1, 0.5f).ending == "away", "the colony stays in: the hunters turn it away");

        // The children.
        C(StorylineMath.Next("children", "shelter", "takein", 1, 0.5f).ending == "takenin", "taken in: they are the colony's, nothing more tied to it");
        foreach (var b in new[] { "place", "sendon" })
        {
            var r = StorylineMath.Next("children", "shelter", b, 1, 0.5f);
            C(r.part == "back" && r.quiet && r.flag == b && r.min >= 30f && r.max <= 60f, b + ": the eldest comes back two to four seasons later");
        }
        C(StorylineMath.Next("children", "shelter", "harmed", 1, 0.5f).ending == "harmed", "children dead in the colony's care: it ends");
        C(StorylineMath.Next("children", "back", "joined", 1, 0.5f).ending == "joined" && StorylineMath.Next("children", "back", "visited", 1, 0.5f).ending == "grown"
          && StorylineMath.Next("children", "back", "declined", 1, 0.5f).ending == "passed", "the eldest joins, visits, trades, or passes by");

        // ================= Its people gone, and the outcomes' words =================

        C(!StorylineMath.GiverGone(true, false, false) && StorylineMath.GiverGone(false, false, false) && StorylineMath.GiverGone(true, true, false) && StorylineMath.GiverGone(true, false, true),
            "its people gone, destroyed or at war: it ends");
        C(StorylineMath.OutcomeOf("cancelled", false) == StorylineMath.Gone && StorylineMath.OutcomeOf("declined", true) == StorylineMath.Declined
          && StorylineMath.OutcomeOf(null, false) == StorylineMath.Ended && StorylineMath.OutcomeOf("ended", false) == StorylineMath.Ended && StorylineMath.OutcomeOf("kept", false) == "kept",
            "a mission's own end words read as the storyline's outcomes (cancelled: its people broke with the colony)");

        // ================= The numbers =================

        C(Enumerable.Range(0, 100).Select(i => StorylineMath.FactorCrates(i / 100f)).All(c => c >= 5 && c <= 8) && StorylineMath.FactorCrates(float.NaN) == 5, "five to eight crates");
        C(StorylineMath.FactorFee(100f) == 40 && StorylineMath.FactorFee(800f) == 100 && StorylineMath.FactorFee(800f) < 800, "the keeping's fee: an eighth of their worth, 40 at least");
        C(StorylineMath.RivalOffer(600f) > 600 && StorylineMath.RivalOffer(600f) == 780 && StorylineMath.RivalOffer(0f) == 100, "the rival offers well over their worth");
        C(StorylineMath.SoldDebt(600f) == 600 && StorylineMath.LostDebt(123f) == 120 && StorylineMath.LostDebt(0f) == 20, "what is owed: their worth");
        C(StorylineMath.SeasonShare(600f, true) > StorylineMath.SeasonShare(600f, false) && StorylineMath.SeasonShare(100f, false) == 30 && StorylineMath.Shares == 4,
            "a share each season for a year, a little more for telling her");
        C(StorylineMath.FeeLess(80, 100f) == 0 && StorylineMath.FeeLess(80, 30f) == 50, "the fee less what went missing since the count, never below nothing");
        C(StorylineMath.WetHoursToSpoil == 12 && StorylineMath.NoTradeDays == 60f, "half a day in the rain spoils a crate; no traders for a year");
        C(StorylineMath.TrapsGoal(1) == 3 && StorylineMath.TrapsGoal(9) == 5 && StorylineMath.TrapsGoal(30) == 6, "three to six traps by the colony's size");
        C(StorylineMath.WallGoal(1) == 24 && StorylineMath.WallGoal(6) == 40 && StorylineMath.WallGoal(30) == 60, "24 to 60 lengths of wall by the colony's size");
        C(StorylineMath.KeptInNeeded(0) == 0 && StorylineMath.KeptInNeeded(4) == 3 && StorylineMath.KeptInNeeded(5) == 4, "three in four animals kept in");
        C(StorylineMath.Readiness(4, 4, 0, 40, 3, 4) == "ready" && StorylineMath.Readiness(0, 4, 40, 40, 0, 0) == "ready" && StorylineMath.Readiness(4, 4, 0, 40, 0, 4) == "partly"
          && StorylineMath.Readiness(0, 4, 0, 40, 4, 4) == "partly" && StorylineMath.Readiness(1, 4, 10, 40, 1, 4) == "unready", "ready: traps or walls, and the animals in; partly: one of the two");
        C(StorylineMath.Hunters(10, true) == 0 && StorylineMath.Hunters(30, false) == 2 && StorylineMath.Hunters(30, true) == 3 && StorylineMath.Hunters(80, true) == 4,
            "the hunters help only when the colony stands well with them; more when it got ready and when it is a friend");
        C(StorylineMath.BeastPick(new List<float>(), 500f) == -1 && StorylineMath.BeastPick(new List<float> { 100f, 300f, 900f }, 500f) == 1
          && StorylineMath.BeastPick(new List<float> { 600f, 300f, 900f }, 100f) == 1, "the beast scaled to the colony: the strongest that fits, else the weakest");
        C(StorylineMath.FleeBelow > 0.2f && StorylineMath.FleeBelow < 0.5f && StorylineMath.MaxReturns == 2, "it flees badly hurt, and comes back at most twice");
        C(Enumerable.Range(0, 100).Select(i => StorylineMath.ChildCount(3, i / 100f)).All(c => c >= 2 && c <= 3) && StorylineMath.ChildCount(12, 0.99f) == 4,
            "two to four children");
        C(StorylineMath.EldestMaxAge < StorylineMath.GrownAge && StorylineMath.GrownAge - StorylineMath.EldestMinAge <= 1f, "the eldest a child when they come, a short step from grown");
        var forms = Enumerable.Range(0, 100).Select(i => StorylineMath.ReturnForm("place", i / 100f)).ToList();
        var formsOn = Enumerable.Range(0, 100).Select(i => StorylineMath.ReturnForm("sendon", i / 100f)).ToList();
        C(forms.Distinct().Count() == 3 && formsOn.Distinct().Count() == 3 && forms.Count(x => x == "joiner") > formsOn.Count(x => x == "joiner")
          && formsOn.Count(x => x == "trader") > forms.Count(x => x == "trader"), "the eldest comes to visit, trade or join; placed, more often to join; sent on, more often to trade");

        // ================= A storyline played out =================

        // The factor's goods, start to end: kept, the rival refused and told, she comes and pays, a season of it in all.
        PlayOut(C, "factor", new[] { ("keep", "kept", 9f), ("rival", "told", 1f) }, "share", 0.5f, 10f, 30f);
        // Sold, and she finds out; the debt unpaid: her guards.
        PlayOut(C, "factor", new[] { ("keep", "kept", 9f), ("rival", "sold", 1f), ("debt", "unpaid", 8f) }, "guards", 0.1f, 15f, 45f);
        // The beast: ready, the signs seen, driven off twice, killed the third time.
        PlayOut(C, "beast", new[] { ("ready", "ready", 7f), ("signs", "found", 1f), ("beast", "driven", 1f), ("beast", "left", 1f), ("beast", "killed", 1f) }, "killed", 0.5f, 30f, 70f);
        // The children: a home with neighbours, the eldest back to join.
        PlayOut(C, "children", new[] { ("shelter", "place", 4f), ("back", "joined", 1f) }, "joined", 0.5f, 35f, 70f);

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (file: f, doc: XDocument.Load(f))).ToList();
        var incidents = defs.SelectMany(d => d.doc.Root!.Elements("IncidentDef")).ToList();
        XElement ModExt(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil." + cls);
        var storyDefs = defs.SelectMany(d => d.doc.Root!.Elements("ColonyCouncil.StorylineDef")).ToList();
        C(storyDefs.Count == StorylineMath.Kinds.Length, "a storyline def each");
        foreach (var kind in StorylineMath.Kinds)
        {
            var sd = storyDefs.FirstOrDefault(d => (string?)d.Element("kind") == kind);
            C(sd != null, kind + ": its words");
            if (sd == null) continue;
            var endings = sd.Element("endings")!.Elements("li").ToList();
            foreach (var e in StorylineMath.Endings(kind))
            {
                var el = endings.FirstOrDefault(x => (string?)x.Element("key") == e);
                C(el != null && el.Element("summaries")?.Elements("li").Any() == true, kind + ": the ending " + e + " has its line for the chronicle and the World page");
                if (el != null && StorylineMath.EndingHasLetter(kind, e))
                    C(el.Element("titles")?.Elements("li").Any() == true && el.Element("texts")?.Elements("li").Any() == true, kind + ": the ending " + e + " has its letter");
            }
            var outs = sd.Element("outcomes")!.Elements("li").Select(x => x.Value).ToList();
            foreach (var part in StorylineMath.Parts(kind))
                foreach (var o in Outcomes(kind, part))
                    C(outs.Any(l => l.StartsWith("[" + part + ",outcome:" + o)), kind + ": the World page's words for " + part + " ended " + o);

            // Every part has its event: the first drawn by the storyteller, the later ones only by the storyline.
            foreach (var part in StorylineMath.Parts(kind))
            {
                var d = incidents.FirstOrDefault(i => ModExt(i, "StorylinePart") is XElement p && (string?)p.Element("story") == kind && (string?)p.Element("part") == part);
                C(d != null, kind + " " + part + ": its event");
                if (d == null) continue;
                var p = ModExt(d, "StorylinePart")!;
                var h = ModExt(d, "HappeningExtension")!;
                bool start = (string?)p.Element("start") == "true";
                float chance = float.Parse((string?)d.Element("baseChance") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                C(start == (part == StorylineMath.FirstPart(kind)) && (start ? chance > 0f : chance == 0f), kind + " " + part + ": drawn by the storyteller only when it is the first part");
                C(h.Element("titles")?.Elements("li").Any() == true && h.Element("texts")?.Elements("li").Any() == true && p.Element("terms")?.Elements("li").Any() == true,
                    kind + " " + part + ": a title, a text and its terms");
                var opts = h.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
                C(opts.Count == 2 && (string?)h.Element("defaultOption") == "1", kind + " " + part + ": take it on or decline, declining by default");
                var ends = p.Element("ends")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
                foreach (var o in Outcomes(kind, part).Where(o => !(kind == "factor" && (part == "makegood" || part == "debt") && o == "unpaid")))
                    C(ends.Any(l => Regex.IsMatch(l, @"^\[[^\]]*\b" + o + @"\b")), kind + " " + part + ": a letter for how it ended (" + o + ")");
                // A later part opens with what happened before.
                if (!start)
                    foreach (var t in h.Element("texts")!.Elements("li").Select(x => Regex.Replace(x.Value, @"^\[[^\]]*\]\s*", "")))
                    {
                        string first = t.Split('.')[0];
                        C(Regex.IsMatch(first, @"\bago\b|^Last season|^When |^Some seasons ago"), kind + " " + part + ": opens with what happened before ('" + first + "')");
                    }
                C(d.Element("workerClass")?.Value.StartsWith("ColonyCouncil.HappeningWorker_") == true && Code("Storylines/Storyline" + Cap(kind) + ".cs").Contains("class " + d.Element("workerClass")!.Value.Substring(14)),
                    kind + " " + part + ": its worker is the storyline's own");
            }
        }

        // The cards and their answers, as the code reads them by index.
        void Card(string name, string story, string[] choices)
        {
            var d = incidents.FirstOrDefault(i => (string?)i.Element("defName") == name);
            C(d != null && ModExt(d, "StorylineCard") != null && (string?)d.Element("workerClass") == "ColonyCouncil.HappeningWorker_StorylineCard" && (string?)d.Element("baseChance") == "0",
                name + ": a card of the storyline, never drawn on its own");
            if (d == null) return;
            var opts = ModExt(d, "HappeningExtension")!.Element("options")!.Elements("li").ToList();
            C(opts.Count == choices.Length, name + ": " + choices.Length + " answers");
            for (int i = 0; i < Math.Min(opts.Count, choices.Length); i++)
            {
                string got = opts[i].Element("effects")?.Elements("li").FirstOrDefault(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_StorylineChoice")?.Element("choice")?.Value ?? "";
                C(got == choices[i] || choices[i] == "" && got == "", name + ": answer " + i + " is " + (choices[i] == "" ? "a plain no" : choices[i]));
            }
        }
        Card("Rimfolk_Storyline_FactorOffer", "factor", new[] { "sell", "refuse", "tell" });
        Card("Rimfolk_Storyline_ChildrenChoice", "children", new[] { "takein", "place", "sendon" });
        Card("Rimfolk_Storyline_ChildrenJoin", "children", new[] { "join", "" });
        var factor = Code("Storylines/StorylineFactor.cs");
        var children = Code("Storylines/StorylineChildren.cs");
        C(factor.Contains("Card = \"Rimfolk_Storyline_FactorOffer\"") && children.Contains("Card = \"Rimfolk_Storyline_ChildrenChoice\"") && children.Contains("Card = \"Rimfolk_Storyline_ChildrenJoin\""),
            "the code asks the cards by their names");
        var offer = incidents.First(i => (string?)i.Element("defName") == "Rimfolk_Storyline_FactorOffer").ToString();
        var choice = incidents.First(i => (string?)i.Element("defName") == "Rimfolk_Storyline_ChildrenChoice").ToString();
        C(offer.Contains("DilemmaEffect_CarryOut") && offer.Contains("Rimfolk_TrustCrate") && choice.Contains("DilemmaEffect_CarryOut") && choice.Contains("<what>meals</what>"),
            "what the answers say is carried out is carried out on the map: the crates to the rival, food to the children");

        // The plain-words rules (CLAUDE.md, "Plain, neutral text"): no em dashes or exclamation marks in anything the storylines say.
        var words = defs.Where(d => Path.GetFileName(d.file).StartsWith("Rimfolk_Storyline")).SelectMany(d => d.doc.Descendants()).Where(e => !e.HasElements)
            .Select(e => Regex.Replace(e.Value, @"\[[^\]]*\]", "")).ToList();
        foreach (var keyed in new[] { "Rimfolk_Storylines.xml", "Rimfolk_Storylines2.xml" })
            words.AddRange(XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", keyed)).Root!.Elements().Select(e => e.Value));
        C(words.Count > 100 && words.All(w => !w.Contains('\u2014') && !w.Contains('!')), "no em dashes or exclamation marks");
        C(words.All(w => !Regex.IsMatch(w, @"\bneedeth\b|\bmeaneth\b|\bhath\b|\bthee\b|\bthou\b")), "no archaic voice outside the letters' olde lines and the gods");

        // The seams.
        C(Code("Missions/Missions.cs").Contains("Storylines.MissionEnded(m, state, outcome)"), "a mission's end tells its storyline (Missions.Finish)");
        var stories = Code("Missions/StoriesComponent.cs");
        C(stories.Contains("public StorylineBook storylines = new StorylineBook();") && stories.Contains("Scribe_Deep.Look(ref storylines, \"storylines\")")
          && stories.Contains("storylines ??= new StorylineBook();") && stories.Contains("Storylines.Hourly(t)"), "saved with the stories, an older save without storylines loads, the hour's step");
        C(Code("World/Ledger/LedgerKeep.cs").Contains("case KeepFor.Storyline: return Storylines.Holds(p)"), "the people a storyline keeps stay in the world while it needs them");
        C(Code("UI/Region/HereAndAwayView.cs").Contains("StorylinePanel.Draw(y, w)"), "running storylines show on the World page with their parts so far");
        C(Code("Peoples/Events/PeopleTrust.cs").Contains("!FactorRules.CratesHeld"), "the league's own crates in trust wait while the factor's are kept");
        var dbg = Code("Storylines/StorylinesDebug.cs");
        C(new[] { "start the factor's goods now", "start the beast coming down the valley now", "start the children from the burned village now", "end the current part with...",
            "the next part (or ending) due now", "what stands (log)" }.All(a => dbg.Contains("Storylines: " + a)), "the dev actions");
        var all = Code("Storylines/Storylines.cs");
        C(all.Contains("[HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival)") && all.Contains("Storylines.NoTrade(f)"), "no traders for a year, by the game's own caravans");
        C(Code("Storylines/StorylineBeast.cs").Contains("LegendWorker.Kinds(map)") && Code("Storylines/StorylineBeast.cs").Contains("StorylineMath.BeastPick"),
            "the beast is one of the region's legends, scaled to the colony");
        C(children.Contains("Arrivals.Join(c, map)") && !Regex.IsMatch(children, @"Relation|AddDirectRelation|Family\."), "taken in, the children join as colonists, with no family tie made");
        var kept = Regex.Match(all, @"const int KeepEndedDays = (\d+);");
        C(kept.Success && int.Parse(kept.Groups[1].Value) >= StorylineMath.KindRestDays, "an ended storyline is kept as long as its kind rests, so the pace can read it");
        var save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "lines", "steps", "flags", "group", "wet", "afters" }.All(c => save.Contains("\"Storylines/Storylines.cs:" + c + "\"")), "every saved collection has its save model");
        // Each storyline's rules beyond its parts, found by the frame: one StorylineRules class each, by its kind.
        foreach (var kind in StorylineMath.Kinds)
            C(Regex.IsMatch(Code("Storylines/Storyline" + Cap(kind) + ".cs"), @"class \w+Rules : StorylineRules[\s\S]*?public override string Kind => "), kind + ": its rules class, found by its kind");
        C(all.Contains("GenTypes.AllSubclassesNonAbstract(typeof(StorylineRules))") && !all.Contains("new FactorRules()"), "the frame finds every storyline's rules (no list of them to keep)");
        C(dbg.Contains("StorylineMath.Outcomes(s.kind, part)") && dbg.Contains("DebugEnded(s, part, outcome)"), "the dev menu ends a part with any of its storyline's own outcomes");
        return n;
    }

    /// <summary>A storyline's own file: Storylines/Storyline + its kind with a capital ("StorylineFactor.cs").</summary>
    private static string Cap(string kind) => char.ToUpperInvariant(kind[0]) + kind.Substring(1);

    /// <summary>
    /// A storyline played through its rules: each part ends as given after its days, the next comes when the rules say,
    /// and it ends with <paramref name="ending"/> between <paramref name="minDays"/> and <paramref name="maxDays"/> after it began.
    /// </summary>
    internal static void PlayOut(Action<bool, string> C, string kind, (string part, string outcome, float days)[] path, string ending, float roll, float minDays, float maxDays)
    {
        string what = "played out, " + kind + " to " + ending;
        float day = 0f;
        string expect = StorylineMath.FirstPart(kind);
        var times = new Dictionary<string, int>();
        StorylineMath.Step step = default;
        bool ok = true;
        for (int i = 0; i < path.Length; i++)
        {
            var (part, outcome, days) = path[i];
            if (part != expect)
            {
                ok = false;
                break;
            }
            times[part] = times.TryGetValue(part, out int t) ? t + 1 : 1;
            day += days;
            step = StorylineMath.Next(kind, part, outcome, times[part], roll);
            day += (step.min + step.max) / 2f;
            expect = step.part;
        }
        C(ok && step.ending == ending && expect == null, what + ": the parts in order, then the ending (" + step + ")");
        C(day >= minDays && day <= maxDays, what + ": about " + Math.Round(day) + " days in all");
    }
}
