using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Colony life's variety, part 1 (the content update's A3, A4, A7, A18, A19 and A20, and twenty-five everyday questions;
/// the player's go of 20 November 2026): the rules without game types (CouncilLife/CouncilLifeMath.cs), and the defs and
/// code: a seat holder asking to step down only when worn or laid up and never in an election, a deputy for ten days who
/// does the seat's acts, two wishes for one pile only when the stores hold enough for either and not both, the night watch's
/// rota going round the fighters, the welcome only for newcomers, the three stories' gates on what happened, every question
/// with forms for every age, answers that play out on the map, the council's silence setting nothing going, the seat
/// speaking in its own questions, plain words where they must be, no hurt of Rimfolk's own.
/// </summary>
public static class CouncilLifeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "colony life (council): " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Root(string rel) => XDocument.Load(Path.Combine(root, rel)).Root!;
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        bool Plain(string s)
        {
            s = Regex.Replace(s ?? "", @"^\[[^\]]*\]\s*", "");
            return !s.Contains('—') && !s.Contains('!');
        }
        const int Day = 60000;

        // ================= The rules =================

        C(StepDownMath.Asks(5f, 0f, false) && StepDownMath.Asks(0f, 10f, false) && !StepDownMath.Asks(4.9f, 9.9f, false) && !StepDownMath.Asks(9f, 20f, true),
            "a holder asks to step down after five days worn or ten laid up, never in an election");
        C(StepDownMath.Since(-1, true, 100) == 100 && StepDownMath.Since(50, true, 100) == 50 && StepDownMath.Since(50, false, 100) == -1,
            "since when a state holds: kept while it holds, begun now, forgotten when over");
        C(Math.Abs(StepDownMath.DaysSince(0, Day * 3, Day) - 3f) < 0.01f && StepDownMath.DaysSince(-1, Day, Day) == 0f && StepDownMath.DaysSince(Day * 2, Day, Day) == 0f,
            "days since a mark, none when there is none");
        C(StepDownMath.DeputyDays == 10f && StepDownMath.DeputyOn(Day * 11, Day * 10) && !StepDownMath.DeputyOn(Day * 10, Day * 10), "a deputy for ten days");
        C(OneSteelMath.OneOnly(90, 60, 50) && !OneSteelMath.OneOnly(110, 60, 50) && !OneSteelMath.OneOnly(55, 60, 50) && !OneSteelMath.OneOnly(40, 5, 20),
            "one pile: enough for either, not both, and a real amount each");
        C(OneSteelMath.AFirst(10, 20) && OneSteelMath.AFirst(10, 10) && !OneSteelMath.AFirst(30, 20) && OneSteelMath.WaitDays == 3f, "who asked first, and three days' wait");
        var seen = new HashSet<int>();
        for (int d = 0; d < 5; d++)
        {
            var (a, b) = NightWatchMath.Pair(d, 5);
            seen.Add(a);
            seen.Add(b);
            C(a != b && a >= 0 && b >= 0 && a < 5 && b < 5, $"the watch's night {d}: two different fighters");
        }
        C(seen.Count == 5, "over five nights the rota goes round all five fighters");
        C(NightWatchMath.Pair(3, 1) == (0, -1) && NightWatchMath.Pair(0, 0) == (-1, -1) && NightWatchMath.From == 21 && NightWatchMath.To == 1,
            "one fighter walks alone, none nobody; nine until one");
        C(WelcomeMath.Newcomer(5f) && !WelcomeMath.Newcomer(0.5f) && !WelcomeMath.Newcomer(20f) && WelcomeMath.CanWelcome(25f, 0) && !WelcomeMath.CanWelcome(10f, 50)
          && !WelcomeMath.CanWelcome(40f, -5) && WelcomeMath.Welcomes(true) > WelcomeMath.Welcomes(false), "the welcome: newcomers under twenty days, hosts settled who don't dislike them, more from their own group");
        C(LifeArcMath.HeldLongEnough(60f) && !LifeArcMath.HeldLongEnough(59f) && LifeArcMath.Helps(0, 10f) && !LifeArcMath.Helps(-30, 0f) && !LifeArcMath.Helps(10, 50f),
            "after the vote: a year held; helping the winner unless thinking badly of them or a deep rivalry");
        C(LifeArcMath.WornThin(1f, true, 0f) && !LifeArcMath.WornThin(5f, true, 0f) && !LifeArcMath.WornThin(1f, false, 0f) && LifeArcMath.WornThin(-1f, false, 7f),
            "worn thin: just after an episode that is over, or a week near breaking");
        C(EverydayMath.OnlyOne(12, 5) && !EverydayMath.OnlyOne(11, 0) && !EverydayMath.OnlyOne(15, 6), "the only one who knows: twelve, and nobody at six");
        C(EverydayMath.EnoughFine(6, 6) && !EverydayMath.EnoughFine(5, 6) && !EverydayMath.EnoughFine(3, 0), "the good meals: one each");
        C(EverydayMath.ColderRoom(8f, 14f) && !EverydayMath.ColderRoom(8f, 12f) && !EverydayMath.ColderRoom(13f, 25f), "a cold bedroom: under twelve, the other five warmer");

        // ================= The council's situations (A3, A4) =================

        var life = Root("Defs/DilemmaDefs/Rimfolk_Dilemmas_CouncilLife.xml").Elements("ColonyCouncil.DilemmaDef").ToList();
        var stepDown = life.FirstOrDefault(d => (string)d.Element("defName") == "Rimfolk_Dilemma_StepDown");
        var oneSteel = life.FirstOrDefault(d => (string)d.Element("defName") == "Rimfolk_Dilemma_OneSteel");
        C(stepDown != null && oneSteel != null, "the step-down question and two wishes for one pile");
        string stepCode = Code("CouncilLife/StepDown.cs"), steelCode = Code("CouncilLife/OneSteel.cs"), offices = Code("Offices/Offices.cs"), interplay = Code("Interplay/Interplay.cs");
        if (stepDown != null)
        {
            var opts = stepDown.Element("options")!.Elements("li").ToList();
            var effects = opts.Select(o => o.Element("effects")!.Elements("li").Select(Cls).ToList()).ToList();
            C(opts.Count == 3 && effects[0].Contains("DilemmaEffect_StepDown") && effects[1].Contains("DilemmaEffect_Deputy") && effects[1].Contains("DilemmaEffect_HeartDayOff")
              && effects[2].Contains("DilemmaEffect_TrustDevoted"), "step down, a deputy and the day off, or asked to stay (the devoted trust more)");
            C((string)stepDown.Element("defaultOption") == "2", "the council's silence asks them to stay: nothing changes");
            var texts = Lis(stepDown, "texts");
            C(new[] { "[worn]", "[breaking]", "[laidup]", "[lowtech]" }.All(t => texts.Any(x => x.StartsWith(t))) && texts.Any(x => !x.StartsWith("[")),
                "openings for the worn, the breaking, the laid up, a low-tech colony and any");
        }
        C(stepCode.Contains("council.election != null") && stepCode.Contains("Life.LaidUpDays(h)") && stepCode.Contains("heart.wornSince"),
            "asked only from the heart's worn days or the sickbed's, never in an election");
        C(stepCode.Contains("holders.Remove(key)") && !stepCode.Contains("Council_LostOffice") && stepCode.Contains("Rimfolk_Deed_SteppedDown") && stepCode.Contains("HeartLogic.Relieve"),
            "stepping down empties the seat without a seat lost's mood, with a deed and the strain eased");
        C(offices.Contains("vacancy") && offices.Contains("Call(comp, year, true)"), "the empty seat's by-election as for any empty seat");
        C(interplay.Contains("offices.Acting(defName)") && stepCode.Contains("public Pawn Acting(string defName)"), "the seats' acts ask for the one acting: the deputy for their days");
        C(offices.Contains("DeputiesDaily()") && stepCode.Contains("CC_Life_DeputyOver"), "a deputy's days end at the council's day, told");
        C(Code("Heart/HeartLogic.cs").Contains("h.wornSince = StepDownMath.Since(") && Code("Heart/HeartData.cs").Contains("Scribe_Values.Look(ref wornSince, \"wornSince\", -1);"),
            "the heart keeps since when someone is worn or near breaking");
        if (oneSteel != null)
        {
            var opts = oneSteel.Element("options")!.Elements("li").ToList();
            var firsts = opts.Select(o => o.Element("effects")!.Elements("li").FirstOrDefault(e => Cls(e) == "DilemmaEffect_WishFirst")?.Element("first")?.Value).ToList();
            C(firsts.SequenceEqual(new[] { "A", "B", "none" }), "first come (A asked first), B's first, or neither yet");
            C((string)oneSteel.Element("seat") == "Council_Office_Quartermaster" && Lis(oneSteel, "texts").Any(t => t.StartsWith("[seat]") && t.Contains("{HOLDER_nameDef}") && t.Contains("{OFFICE}")),
                "the quartermaster speaks in it");
            C(Lis(oneSteel, "texts").All(t => t.Contains("{WISHA}") && t.Contains("{WISHB}") && t.Contains("{THING}")), "every opening names both wishes and the stuff");
        }
        C(steelCode.Contains("OneSteelMath.OneOnly(") && steelCode.Contains("comp.Promise(w)") && steelCode.Contains("OneSteelMath.WaitDays * GenDate.TicksPerDay")
          && steelCode.Contains("Together.When(") && steelCode.Contains("Speech.Say("), "promised as the council promises, the other's deadline moved, told why face to face");

        // ================= The decrees (A7) =================

        var decrees = Root("Defs/DecreeDefs/Rimfolk_Decrees_CouncilLife.xml").Elements("ColonyCouncil.DecreeDef").ToList();
        var watch = decrees.FirstOrDefault(d => (string)d.Element("defName") == "Rimfolk_Decree_NightWatch");
        var welcome = decrees.FirstOrDefault(d => (string)d.Element("defName") == "Rimfolk_Decree_Welcome");
        C(watch != null && welcome != null, "the night watch and a welcome for newcomers");
        var thoughts = Directory.GetFiles(Path.Combine(root, "Defs", "ThoughtDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).ToList();
        var thoughtNames = thoughts.Select(t => (string)t.Element("defName")).ToHashSet();
        foreach (var d in new[] { watch, welcome }.Where(x => x != null))
        {
            string name = (string)d!.Element("defName");
            C(d.Element("daily")?.Elements("li").Any() == true && d.Element("hurts") == null, $"{name}: shows on the map each day, with no hurt of its own");
            var thought = thoughts.FirstOrDefault(t => (string)t.Element("defName") == (string)d.Element("thought"));
            C(thought?.Element("stages")?.Elements("li").Count() == 4, $"{name}: its mood in four stages (hate, dislike, like, love)");
            C(Plain((string)d.Element("description")) && Lis(d, "effects").All(Plain), $"{name}: plain words");
            C(d.Element("support")?.Descendants("li").Any() == true && d.Element("oppose")?.Descendants("li").Any() == true, $"{name}: words for and against");
            C(new[] { "tribal", "medieval", "industrial", "spacer" }.All(a => d.Element("support")!.Descendants("li").Any(l => l.Value.StartsWith("[" + a + "]"))),
                $"{name}: asked for in every age's words");
        }
        if (watch != null)
            C(Lis(watch, "requestWhen").Contains("RecentThreat") && watch.Element("daily")!.Elements("li").Any(e => Cls(e) == "DilemmaEffect_WatchRota"), "the watch: asked for after an attack, a rota each day");
        if (welcome != null)
            C(Lis(welcome, "requestWhen").Contains("Outsider") && welcome.Element("daily")!.Elements("li").Any(e => Cls(e) == "DilemmaEffect_WelcomeEvening"), "the welcome: asked for when a newcomer stands outside every group");
        string ww = Code("CouncilLife/WatchAndWelcome.cs");
        C(ww.Contains("patrol = true") && ww.Contains("AnswerShiftMath.Late") && ww.Contains("NightWatchMath.Pair("), "the watch walks the walls by the rota, and they start late");
        C(ww.Contains("n.welcomes += WelcomeMath.Welcomes(") && ww.Contains("Notify_Talked(talk, newcomer, host)") && ww.Contains("play.afterward = After"),
            "an hour sat counts as the group's welcomes and the newcomer's story's welcome, only once it happened");
        C(Code("PlayOut/OnMapPlays.cs").Contains("LifePlays.Over(p, what, happened, took)"), "the welcome's evening is counted from its play-out's end");
        C(Code("Decrees/Decrees.cs").Contains("case DecreeMoment.Outsider:"), "the moment a newcomer stands outside every group");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
            C(keyed.ContainsKey("CC_Life_WatchLabel_" + age) && keyed.ContainsKey("CC_Life_WelcomeLabel_" + age), $"the watch and the welcome named in the {age} age");

        // ================= The stories (A18, A19, A20) =================

        var arcs = Root("Defs/ArcDefs/Rimfolk_Arcs_CouncilLife.xml").Elements("ColonyCouncil.ArcDef").ToList();
        string arcCode = Code("CouncilLife/LifeArcs.cs");
        foreach (var (name, worker) in new[] { ("Rimfolk_Arc_AfterTheVote", "ArcWorker_AfterVote"), ("Rimfolk_Arc_SentAway", "ArcWorker_SentAway"), ("Rimfolk_Arc_WornThin", "ArcWorker_WornThin") })
        {
            var a = arcs.FirstOrDefault(x => (string)x.Element("defName") == name);
            C(a != null && ((string)a.Element("workerClass")).EndsWith(worker) && arcCode.Contains("class " + worker + " : ArcWorker"), $"{name}: there, with its worker");
            if (a == null) continue;
            C(a.Element("stages")!.Elements("li").Count() == 4, $"{name}: four stages");
        }
        C(offices.Contains("LifeArcs.VotedOut(this, office, who, to,") && arcCode.Contains("LifeArcMath.HeldLongEnough(held)"), "after the vote: begins from the count, for a seat held a year");
        C(arcCode.Contains("if (arc.stage != 1) return;") && arcCode.Contains("\"helped\" : \"kept\""), "after the vote: helping the winner or keeping the grudge is settled by what they think of them");
        C(arcCode.Contains("BanishTracker.Instance") && arcCode.Contains("LifeArcMath.SentAwayWithin") && arcCode.Contains("Interplay.Chaplain"),
            "the one sent away: after a banishment, with the chaplain or a friend");
        C(arcCode.Contains("gone.Map.IsPlayerHome") && arcCode.Contains("ArcEffect_MeetSentAway"), "the one sent away at the gate again: met there");
        C(arcCode.Contains("HeartTimeOff.Running(p)") && arcCode.Contains("RestingToday(p.Map, p)"), "worn thin: moves on with a day off, the heart's or a rest day");
        var worn = arcs.FirstOrDefault(x => (string)x.Element("defName") == "Rimfolk_Arc_WornThin");
        if (worn != null)
        {
            var gate = worn.Element("stages")!.Elements("li").ElementAt(2).Element("gate");
            C((string)gate?.Element("hold") == "Strain" && (string)gate?.Element("value") == "30" && (string)gate?.Element("hours") == "48", "worn thin: back to themselves at strain 30 or under for two days");
            C(worn.Descendants("li").Any(l => Cls(l) == "ArcEffect_Mark" && l.Element("family")?.Value == "StoodBy") && worn.Descendants("li").Any(l => Cls(l) == "ArcEffect_Strain"),
                "its end lifts some weight and marks the friend");
        }

        // ================= The everyday questions =================

        var all = Root("Defs/DilemmaDefs/Rimfolk_Dilemmas_Everyday.xml").Elements("ColonyCouncil.DilemmaDef").ToList();
        C(all.Count == 25, $"twenty-five everyday questions ({all.Count})");
        var everydayCode = Code("CouncilLife/EverydayQuestions.cs") + Code("Strangers/Strangers.cs");
        var onMap = new HashSet<string>
        {
            "DilemmaEffect_Lesson", "DilemmaEffect_WorkParty", "DilemmaEffect_Visit", "DilemmaEffect_HeartDayOff", "DilemmaEffect_Feast", "DilemmaEffect_HandOver",
            "DilemmaEffect_SowPatch", "DilemmaEffect_Vigil", "DilemmaEffect_Rounds", "DilemmaEffect_AnimalScene", "DilemmaEffect_Made", "DilemmaEffect_Words",
            "DilemmaEffect_NameAnimal", "DilemmaEffect_NewMaster", "DilemmaEffect_CarryOut", "DilemmaEffect_Bonfire", "DilemmaEffect_Gathering", "DilemmaEffect_KeepFire",
            "DilemmaEffect_SwapRooms"
        };
        var seats = new Dictionary<string, int>();
        foreach (var d in all)
        {
            string name = (string)d.Element("defName");
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker.Length == 0 || everydayCode.Contains("class " + worker + " : DilemmaWorker"), $"{name}: its worker ({worker}) is in the code");
            C(d.Element("traditionOption") == null && d.Element("timely") == null && d.Element("withinDaysOfThreat") == null,
                $"{name}: asked in the usual rhythm, never a custom (no more questions a year)");
            var texts = Lis(d, "texts");
            C(texts.Count >= 3 && texts.Any(t => !t.StartsWith("[")) && texts.Any(t => t.StartsWith("[lowtech]")), $"{name}: a form for any colony and for a low-tech one");
            var options = d.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)d.Element("defaultOption") ?? "0");
            C(options.Count >= 2 && options.Count <= 4 && def >= 0 && def < options.Count, $"{name}: two to four answers, the council's silence one of them");
            var fx = options.Select(o => o.Element("effects")!.Elements("li").Select(Cls).ToList()).ToList();
            // An answer that is a ruling (the craft kept, the kitchen kept) needn't; the play-out audit reads every answer's words.
            C(fx.Count(f => f.Any(onMap.Contains)) >= Math.Max(1, options.Count - 2), $"{name}: the answers that do something play out on the map");
            C(fx[def].All(e => !onMap.Contains(e)), $"{name}: the council's silence sets nothing going");
            C(fx.All(f => f.Count(e => e == "DilemmaEffect_Thought") >= 1), $"{name}: each answer moves someone");
            bool sides = options.SelectMany(o => o.Element("effects")!.Elements("li")).Any(e => Cls(e) == "DilemmaEffect_Thought"
                && new[] { "Council_Dlm_Vindicated", "Council_Dlm_Overruled", "Council_Dlm_Grudge", "Council_Dlm_MadeUp", "Council_Dlm_Bonded", "Rimfolk_DlmE_LeftOut", "Council_Dlm_Compromise", "Rimfolk_DlmE_NotMyName", "Rimfolk_DlmE_StillSore", "Rimfolk_DlmE_GotMyWay", "Rimfolk_DlmE_Pressed", "Rimfolk_DlmE_Learned", "Council_Dlm_Ignored" }
                    .Contains(e.Element("thought")?.Value));
            C(sides, $"{name}: sets two of the colony toward or against each other");
            foreach (var t in options.SelectMany(o => o.Element("effects")!.Elements("li")).Where(e => e.Element("thought") != null).Select(e => e.Element("thought")!.Value))
                C(thoughtNames.Contains(t), $"{name}: its mood {t} exists");
            C(!options.SelectMany(o => o.Element("effects")!.Elements("li")).Any(e => Cls(e) is "DilemmaEffect_Fight" or "DilemmaEffect_Hediff" or "DilemmaEffect_MentalState"),
                $"{name}: no hurt of Rimfolk's own");
            var seat = (string)d.Element("seat");
            var seatLines = texts.Concat(options.SelectMany(o => Lis(o, "outcomes"))).Where(t => t.StartsWith("[seat")).ToList();
            if (seat != null)
            {
                seats[seat] = seats.TryGetValue(seat, out int k) ? k + 1 : 1;
                C(texts.Any(t => t.StartsWith("[seat]")), $"{name}: the seat's holder has an opening of their own");
                C(seatLines.All(t => t.Contains("{HOLDER_nameDef}") && t.Contains("{OFFICE}")), $"{name}: the seat's lines name the holder and the seat");
            }
            else C(seatLines.Count == 0, $"{name}: [seat] lines only where the question has a seat");
            foreach (var t in texts.Concat(options.SelectMany(o => new[] { (string)o.Element("label"), (string)o.Element("outcome") }.Concat(Lis(o, "outcomes")))))
                C(Plain(t) && !Regex.IsMatch(t ?? "", @"(^(\[[^\]]*\]\s*)?|[.?]\s+)\{[A-Z]+_(pronoun|possessive|objective)\}"), $"{name}: no em dash, no exclamation mark, no sentence begun with a pronoun: '{(t ?? "").Substring(0, Math.Min(50, (t ?? "").Length))}'");
        }
        C(seats.Count >= 5 && seats.Values.All(v => v >= 2), $"the seats speak in their own work: {string.Join(", ", seats.Select(kv => kv.Key.Replace("Council_Office_", "") + " " + kv.Value))}");
        // The eight parts of the colony's life, each with questions.
        string file = File.ReadAllText(Path.Combine(root, "Defs/DilemmaDefs/Rimfolk_Dilemmas_Everyday.xml"));
        foreach (var part in new[] { "Work and rest", "Food and the stores", "Sickness and care", "The young and the old", "Animals", "Guests and strangers", "Quarrels and friendships", "The seasons" })
            C(file.Contains("=== " + part + " ==="), $"the colony's life: {part.ToLowerInvariant()}");
        C(Code("PlayOut/PlayWork.cs").Contains("if (at == \"sickroom\") return Everyday.SickroomCell(map);"), "the sickroom's scrubbing starts at its beds");
        C(everydayCode.Contains("AnimalFacts.HerdmasterAt(map) != null") && everydayCode.Contains("pet.Name = new NameSingle(n)"), "the newborn named on the council's word, only with no herdmaster to name it");

        // ================= The seams =================

        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Offices/Offices.cs:deputies\"") && offices.Contains("Scribe_Collections.Look(ref seatFrom"),
            "the deputies and the seats' start saved, with their models");
        string dbg = Code("CouncilLife/CouncilLifeDebug.cs");
        C(dbg.Contains("ask one of its questions now") && dbg.Contains("begin one of its stories now") && dbg.Contains("the night watch's and the welcome's day"),
            "dev actions: each question, each story, the decrees' day");
        foreach (var q in all.Select(d => (string)d.Element("defName")).Concat(new[] { "Rimfolk_Dilemma_StepDown", "Rimfolk_Dilemma_OneSteel" }))
            C(dbg.Contains("\"" + q + "\""), $"{q}: asked from dev mode");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(audit.Contains("\"DilemmaEffect_HeartDayOff\"") && audit.Contains("\"DilemmaEffect_WatchRota\"") && audit.Contains("\"DilemmaEffect_WelcomeEvening\""), "the play-out audit knows the new play-outs");
        return n;
    }
}
