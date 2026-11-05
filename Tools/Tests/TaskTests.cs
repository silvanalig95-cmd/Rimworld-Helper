using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2c.1, the keeper of secrets and council tasks (docs/design/stage2-diplomacy.md, item 8;
/// Diplomacy/TaskMath.cs and KeeperMath.cs; the player's decisions 13 and 14): the office, which office has which task and
/// what it is aimed at, the steps and their timing, one officer away a home and a visit a season, what each step does by
/// the officer's skill, how a task ends, the keeper's roll and Secret ties, pressing one, the bad sides at the design's
/// rates, the facts' tags; and the mod's side: the hooks in the shared code, the save, the words, the lines, the talk pieces.
/// </summary>
public static class TaskTests
{
    private const int D = TaskMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "council tasks: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- The office ----------------

        var office = XDocument.Load(Path.Combine(root, "Defs", "OfficeDefs", "Rimfolk_Offices_Keeper.xml")).Root!.Element("ColonyCouncil.OfficeDef")!;
        C(office.Element("defName")!.Value == TaskMath.KeeperOffice, "the keeper of secrets' seat is Council_Office_SecretKeeper");
        C(office.Element("minColonists")!.Value == KeeperMath.Voters.ToString() && KeeperMath.Voters == 6, "it opens at six voters (decision 13)");
        var skills = office.Element("skills")!.Elements("li").Select(e => e.Value).ToList();
        C(skills.Count == 2 && skills.Contains("Social") && skills.Contains("Intellectual"), "Social and Intellectual");
        C(office.Element("hediff")!.Value == "Council_Office_SecretKeeper" && File.ReadAllText(Path.Combine(root, "Defs", "HediffDefs", "Rimfolk_Hediffs_Keeper.xml")).Contains("<defName>Council_Office_SecretKeeper</defName>"),
            "the holder's edge is a hediff of its own");
        C(int.Parse(office.Element("order")!.Value) > 12, "listed after the elders");
        C(KeeperMath.SlipFactor(true) == 0.5f && KeeperMath.SlipFactor(false) == 1f, "the colony's secrets slip half as often while the seat is filled");
        C(Code("Secrets/Secrets.cs").Contains("chance *= KeeperOfSecrets.SlipFactor(owner);"), "the slips read it");
        C(KeeperMath.CatchFactor(true) == 2f && KeeperMath.CatchFactor(false) == 1f, "while the keeper watches, their schemes are caught twice as often (release 2c.3)");

        // ---------------- Which office has which task ----------------

        C(TaskMath.Offices.Length == 8 && TaskMath.Offices.Distinct().Count() == 8, "tasks for eight offices now (decision 14)");
        foreach (var o in new[] { "Council_Office_Reeve", "Council_Office_Herdmaster", "Council_Office_Keeper", "Council_Office_Peacemaker", "Council_Office_Elder" })
            C(!TaskMath.HasTasks(o) && TaskMath.KindsOf(o).Count == 0, $"{o} has no task (only if wanted later)");
        string offices = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "OfficeDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var o in TaskMath.Offices)
            C(offices.Contains("<defName>" + o + "</defName>"), $"{o} is a seat of the council");
        foreach (var k in TaskMath.Kinds)
        {
            C(TaskMath.OfficeOf(k) != null && TaskMath.KindsOf(TaskMath.OfficeOf(k)).Contains(k), $"{k} belongs to one office");
            C(TaskMath.Parse(TaskMath.Code(k), out var back) && back == k, $"{k} is read back from its code");
        }
        foreach (var o in TaskMath.Offices) C(TaskMath.KindsOf(o).Count >= 1, $"{o} has at least one task");
        C(TaskMath.OfficeOf(TaskKind.WinOver) == TaskMath.Speaker && TaskMath.OfficeOf(TaskKind.Secrets) == TaskMath.KeeperOffice && TaskMath.OfficeOf(TaskKind.Scout) == TaskMath.Marshal
          && TaskMath.OfficeOf(TaskKind.Drill) == TaskMath.Marshal && TaskMath.OfficeOf(TaskKind.Trade) == TaskMath.Quartermaster && TaskMath.OfficeOf(TaskKind.Dues) == TaskMath.Quartermaster
          && TaskMath.OfficeOf(TaskKind.Tend) == TaskMath.Physician && TaskMath.OfficeOf(TaskKind.BringHome) == TaskMath.Warden && TaskMath.OfficeOf(TaskKind.TheirKin) == TaskMath.Warden
          && TaskMath.OfficeOf(TaskKind.KeepFaith) == TaskMath.Chaplain && TaskMath.OfficeOf(TaskKind.SpeakFaith) == TaskMath.Chaplain && TaskMath.OfficeOf(TaskKind.Host) == TaskMath.Steward,
            "the design's tasks by office");
        C(!TaskMath.Built(TaskKind.Dues) && TaskMath.Kinds.Where(k => k != TaskKind.Dues).All(TaskMath.Built), "every task built but the vassals' dues (release 2c.2)");
        C(!TaskMath.Built(TaskKind.None) && !TaskMath.Kinds.Contains(TaskKind.None), "None is no task");
        C(TaskMath.TargetOf(TaskKind.WinOver) == TaskTarget.Notable && TaskMath.TargetOf(TaskKind.Tend) == TaskTarget.Town && TaskMath.TargetOf(TaskKind.BringHome) == TaskTarget.Colonist
          && TaskMath.TargetOf(TaskKind.Scout) == TaskTarget.Home && TaskMath.TargetOf(TaskKind.Drill) == TaskTarget.People && TaskMath.TargetOf(TaskKind.Host) == TaskTarget.People,
            "each aimed at a notable, a people, a town, one of the colony's held, or the colony's own land");
        C(TaskMath.MaxPerHome == 13, "one record a seat: thirteen a home at most (the design's bound)");

        // ---------------- The steps ----------------

        C(TaskMath.Writes(TaskKind.WinOver) && TaskMath.Writes(TaskKind.Trade) && TaskMath.Writes(TaskKind.Host) && !TaskMath.Writes(TaskKind.Secrets) && !TaskMath.Writes(TaskKind.Scout)
          && !TaskMath.Writes(TaskKind.Drill) && !TaskMath.Writes(TaskKind.Tend), "who writes: the speaker, quartermaster, warden, chaplain, steward; not the keeper, marshal or physician");
        C(TaskMath.VisitOnly(TaskKind.Drill) && TaskMath.VisitOnly(TaskKind.Tend) && !TaskMath.VisitOnly(TaskKind.WinOver), "the drill and the physician's task are the visit");
        C(TaskMath.MayVisit(TaskKind.WinOver) && TaskMath.MayVisit(TaskKind.Secrets) && TaskMath.MayVisit(TaskKind.Trade) && !TaskMath.MayVisit(TaskKind.BringHome) && !TaskMath.MayVisit(TaskKind.Host)
          && !TaskMath.MayVisit(TaskKind.Scout), "who may visit their town");
        C(TaskMath.FirstLetterHours(0f) == 2f && TaskMath.FirstLetterHours(1f) == 12f, "the first letter a few hours after the task is set");
        C(TaskMath.NextLetterDays(0f) == 4f && TaskMath.NextLetterDays(1f) == 6f && TaskMath.NextLetterDays(2f) == 6f, "a letter every four to six days (the design's)");
        C(Code("World/NotableMath.cs").Contains("case LetterPurpose.Task: return 3f;"), "the letters' own wait lets an officer write every four days");
        C(TaskMath.GivesUp(TaskKind.WinOver, 0, 60 * D) && !TaskMath.GivesUp(TaskKind.WinOver, 0, 60 * D - 1) && TaskMath.GivesUp(TaskKind.Scout, 0, 15 * D) && !TaskMath.GivesUp(TaskKind.Scout, 0, 15 * D - 1),
            "a task of letters gives up after a year; a season's task ends after a season");
        C(!TaskMath.GivesUp(TaskKind.WinOver, -1, 999 * D), "a task never started never gives up");
        C(TaskMath.SeasonDone(TaskKind.Scout, 1) && !TaskMath.SeasonDone(TaskKind.Scout, 0) && !TaskMath.SeasonDone(TaskKind.WinOver, 9), "a season's task ends well when something was done in it");

        // ---------------- One away, a visit a season ----------------

        C(TaskMath.MayVisitNow(false, -1, 0) && !TaskMath.MayVisitNow(true, -1, 0), "at most one officer away from a home (decision 14)");
        C(!TaskMath.MayVisitNow(false, 0, 15 * D - 1) && TaskMath.MayVisitNow(false, 0, 15 * D), "an office visits once a season at most");
        C(TaskMath.CanSpare(3) && !TaskMath.CanSpare(2), "the officer and a companion go, and someone stays");
        C(Math.Abs(TaskMath.TripDays(2f) - 5f) < 0.001f && TaskMath.TripDays(0f) >= 2f, "the trip: the road there and back and a day at their town");

        // ---------------- What a step does ----------------

        C(TaskMath.RegardUp(0) == 1f && TaskMath.RegardUp(20) == 6f && TaskMath.RegardUp(10) > TaskMath.RegardUp(5), "a letter warms them by the officer's Social, 1 to 6");
        C(TaskMath.ToneShare(0) > TaskMath.ToneShare(1) && TaskMath.ToneShare(1) > TaskMath.ToneShare(2) && TaskMath.ToneShare(4) == 0f && TaskMath.ToneShare(5) == 0f,
            "a refusal or silence moves nothing; a warm answer most");
        C(TaskMath.VisitTimes == 3f, "a visit counts three letters");
        C(TaskMath.Titled(true, true) == 1.5f && TaskMath.Titled(true, false) == 1f && TaskMath.Titled(false, true) == 1f, "a titled head receives a titled officer half again more warmly (decision 9)");
        C(TaskMath.WinGoal == 60 && TaskMath.Won(60) && !TaskMath.Won(59), "won over at a regard of 60 (the design's)");
        C(TaskMath.WinResult(true, true) == "defence" && TaskMath.WinResult(false, true) == "trade" && TaskMath.WinResult(false, false) == "favour",
            "their offer of a pact when one can be made, else a favour owed");
        C(TaskMath.TradeGain(0, 0) == 5f && TaskMath.TradeGain(20, 20) == 20f, "the trade's way by Social and Intellectual, 5 to 20 a letter");
        C(TaskMath.TradeResult(true) == "pact" && TaskMath.TradeResult(false) == "caravan", "the trade open: their trade pact, else their caravan");
        C(TaskMath.CaravanDays(0f) == 1f && TaskMath.CaravanDays(1f) == 3f, "their caravan in one to three days");
        C(TaskMath.Samples(200) == 50 && TaskMath.Samples(8) == 10, "samples a quarter of a cart, ten at least");
        C(TaskMath.Dawn(5) && TaskMath.Dawn(8) && !TaskMath.Dawn(9) && !TaskMath.Dawn(4), "the patrol at dawn");
        C(TaskMath.PatrolDue(-1, 0) && !TaskMath.PatrolDue(0, D) && TaskMath.PatrolDue(0, 2 * D), "every other day");
        C(TaskMath.Spotted(0.59f) && !TaskMath.Spotted(0.6f), "a hidden camp seen from the edges three times in five");
        C(TaskMath.LevyFactor(0, 15 * D - 1) == 1.2f && TaskMath.LevyFactor(0, 15 * D) == 1f && TaskMath.LevyFactor(-1, 0) == 1f, "their band a fifth stronger for a season after the drill");
        C(TaskMath.MedicineFor(20) == 6 && TaskMath.MedicineFor(5) == 3 && TaskMath.MedicineFor(2) == 0, "medicine taken: six at most, two always left at home");
        C(TaskMath.SickBackChance == 0.2f && TaskMath.SickBack(0.19f) && !TaskMath.SickBack(0.2f), "one visit in five brings the sickness home (the design's)");
        C(TaskMath.PriceFactor(0, 0) == 1f && TaskMath.PriceFactor(10, 2) < 1f && TaskMath.PriceFactor(20, 50) == 0.5f && TaskMath.PriceFactor(15, 2) < TaskMath.PriceFactor(5, 2),
            "their price lowered by each letter and by Social, to half at most");
        C(TaskMath.KinSooner == 0.5f && TaskMath.KinHigher == 1.25f, "their kin's offers in half the time, a quarter higher");
        C(TaskMath.Shaken(0.9f, 10) < 0.9f && TaskMath.Shaken(0.2f, 20) == 0.2f && TaskMath.Shaken(0.1f, 20) == 0.1f, "a notable's certainty shaken a little, never below a fifth by these words");
        C(TaskMath.Welcome(10f, true) == 15f && TaskMath.Welcome(10f, false) == 10f && TaskMath.Welcome(-5f, true) == -5f, "the steward's guests' welcome counts half again (a poor one doesn't)");

        // ---------------- This week ----------------

        C(TaskMath.StepIndex(TaskStage.Running, VisitStage.None, 0) == 0 && TaskMath.StepIndex(TaskStage.Running, VisitStage.None, 2) == 1
          && TaskMath.StepIndex(TaskStage.Running, VisitStage.Road, 2) == 2 && TaskMath.StepIndex(TaskStage.Done, VisitStage.None, 2) == 3, "four steps, never a time left");
        C(TaskMath.NeedsPlayer(TaskKind.Tend, TaskStage.Running, VisitStage.None, false, 0) && !TaskMath.NeedsPlayer(TaskKind.Tend, TaskStage.Running, VisitStage.Planned, false, 0)
          && TaskMath.NeedsPlayer(TaskKind.Host, TaskStage.Running, VisitStage.None, false, 1) && !TaskMath.NeedsPlayer(TaskKind.Host, TaskStage.Running, VisitStage.None, true, 1)
          && !TaskMath.NeedsPlayer(TaskKind.WinOver, TaskStage.Running, VisitStage.None, false, 3), "a visit or a feast to plan waits on the player; letters don't");

        // ---------------- The keeper's roll ----------------

        C(Math.Abs(KeeperMath.LearnChance(5, 5, 5, 5) - 0.35f) < 0.001f, "even sides: 35%");
        C(KeeperMath.LearnChance(10, 10, 5, 5) > KeeperMath.LearnChance(5, 5, 5, 5) && KeeperMath.LearnChance(20, 20, 0, 0) == 0.8f && KeeperMath.LearnChance(0, 0, 20, 20) == 0.1f,
            "Social and Intellectual against theirs, a tenth to four in five");
        C(KeeperMath.LearnChance(5, 5, 5, 5, KeeperMath.VisitBonus) > KeeperMath.LearnChance(5, 5, 5, 5, KeeperMath.InformerBonus), "a visit better than an informer, an informer better than a talk");
        C(KeeperMath.FoundOutChance == 0.1f && KeeperMath.Roll(0.8f, 0.05f) == SecretRoll.FoundOut && KeeperMath.Roll(0.8f, 0.1f) == SecretRoll.Learned, "found out one step in ten (the design's)");
        C(KeeperMath.Roll(0.35f, 0.99f) == SecretRoll.Nothing && KeeperMath.Roll(0.35f, 0.3f) == SecretRoll.Learned, "learned by the chance, else nothing");
        int found = 0, learned = 0;
        for (int i = 0; i < 1000; i++)
        {
            var r = KeeperMath.Roll(0.35f, (i + 0.5f) / 1000f);
            if (r == SecretRoll.FoundOut) found++;
            else if (r == SecretRoll.Learned) learned++;
        }
        C(found == 100 && Math.Abs(learned - 315) <= 2, $"over many steps: a tenth found out ({found}), the chance of the rest learned ({learned})");
        C(KeeperMath.CaughtRegard < 0f && KeeperMath.CaughtKeeperRegard < KeeperMath.CaughtRegard, "found out: they think less of the colony, and of the keeper most");
        C(KeeperMath.InformerSilver(0) == 60 && KeeperMath.InformerSilver(3) == 120 && KeeperMath.InformerSilver(9) == 120, "an informer's silver by the colony's age");
        C(KeeperMath.AfterDark(20) && KeeperMath.AfterDark(1) && !KeeperMath.AfterDark(2) && !KeeperMath.AfterDark(19), "the watch after dark, 20 to 2");
        C(KeeperMath.OverhearChance > 0f && KeeperMath.OverhearChance < KeeperMath.LearnChance(0, 0, 20, 20) * 2f, "overhearing now and then, rarer than a talk");

        // ---------------- Their secrets and Secret ties ----------------

        C(KeeperMath.Kinds.Length == 8 && KeeperMath.Kinds.Distinct().Count() == 8, "eight kinds of secret");
        foreach (var k in KeeperMath.Kinds) C(KeeperMath.Weight(k) >= 1 && KeeperMath.Weight(k) <= 3 && KeeperMath.IsKind(k), $"{k} weighs 1 to 3");
        C(KeeperMath.Pick(0f) == KeeperMath.Kinds[0] && KeeperMath.Pick(0.999f) == KeeperMath.Kinds[7] && KeeperMath.Pick(2f) == KeeperMath.Kinds[7], "a kind by a roll");
        C(KeeperMath.MaxLive == 20 && KeeperMath.ToLetGo(19) == 0 && KeeperMath.ToLetGo(20) == 1 && KeeperMath.ToLetGo(25) == 6, "twenty live at most, the oldest let go (the design's bound)");
        C(KeeperMath.Days == 60, "a secret matters for a year");
        C(TieRules.CanCallIn(Tie.Kind.Secret) && TieRules.CanMove(Tie.Kind.Secret, Tie.State.CalledIn, Tie.State.Kept) && TieRules.CanMove(Tie.Kind.Secret, Tie.State.CalledIn, Tie.State.Broken)
          && TieRules.CanMove(Tie.Kind.Secret, Tie.State.CalledIn, Tie.State.Open), "pressed (called in): complied with (kept), refused and told (broken), or open again when the letter never went");
        C(TieRules.MaxOf(Tie.Kind.Secret) >= KeeperMath.MaxLive, "the ledger's own bound holds the twenty");

        // ---------------- Pressing one ----------------

        C(KeeperMath.ComplyChance(3, 0, 0) > KeeperMath.ComplyChance(1, 0, 0), "the heavier the secret, the likelier they pay");
        C(KeeperMath.ComplyChance(2, 50, 0) > KeeperMath.ComplyChance(2, -50, 0), "the better they think of the colony, the likelier");
        C(KeeperMath.ComplyChance(2, 0, 2) > KeeperMath.ComplyChance(2, 0, -2), "a kind nature settles quietly, a cold one less");
        C(KeeperMath.ComplyChance(1, -100, -2) == 0.1f && KeeperMath.ComplyChance(3, 100, 2) == 0.9f, "a tenth to nine in ten");
        C(KeeperMath.Answer(2, 0, 0, 0f) == PressAnswer.Complied && KeeperMath.Answer(2, 0, 0, 0.99f) == PressAnswer.Refused, "the roll against the chance");
        C(KeeperMath.Hush(1) == 100 && KeeperMath.Hush(3) == 300 && KeeperMath.Hush(9) == 300, "100 silver a weight to keep it quiet");
        C(KeeperMath.PressedRegard < 0f && KeeperMath.ToldRegard < KeeperMath.PressedRegard, "pressed, they think less of the colony; refused and told, much less");
        C(KeeperMath.ToldDeed == "schemed", "a secret told is remembered as the colony's scheme (the design's deed)");

        // ---------------- Facts ----------------

        var tags = TaskMath.Tags().ToList();
        C(tags.Contains("us:task") && tags.Contains("task:writes") && tags.Contains("task:visited") && tags.Contains("task:you") && tags.Contains("us:task:winover"),
            "the facts: us:task, us:task:KIND, the officer who writes often, who visited, who is talking");
        C(!tags.Contains("us:task:secrets") && !tags.Contains("us:task:scout") && !TaskMath.Known(TaskKind.Secrets), "the keeper's task is never talked of");
        C(KeeperMath.Tags.Contains("them:caughtkeeper") && KeeperMath.Tags.Contains("them:pressed") && KeeperMath.Tags.Contains("keeper:watched"), "the keeper's tags");
        C(TaskMath.HistoryKind == DiplomacyMath.Task && DiplomacyMath.Kinds.Contains(TaskMath.HistoryKind), "history dip:task");

        // ---------------- The mod's side ----------------

        var dip = Directory.GetFiles(Path.Combine(src, "Diplomacy"), "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        foreach (var f in new[] { "TaskMath.cs", "KeeperMath.cs" })
            C(!Regex.IsMatch(dip[f], @"using (Verse|RimWorld|UnityEngine)"), $"{f} is pure");
        foreach (var f in new[] { "CouncilTasks.cs", "TaskResults.cs", "TaskVisits.cs", "TaskLetters.cs", "KeeperOfSecrets.cs", "SecretTies.cs", "TaskPanels.cs", "TaskWords.cs", "FactsTasks.cs", "TaskJobs.cs", "TaskBook.cs" })
        {
            C(dip.ContainsKey(f), $"{f} is in Diplomacy/");
            if (dip.ContainsKey(f)) C(!dip[f].Contains("Messages.Message(") && !dip[f].Contains("Find.LetterStack.ReceiveLetter("), $"{f} tells the player only through Tell");
        }
        string book = Code("Diplomacy/DiplomacyBook.cs");
        C(book.Contains("public List<CouncilTask> tasks") && book.Contains("Scribe_Collections.Look(ref tasks, \"tasks\", LookMode.Deep);") && book.Contains("h.tasks.Count > 0")
          && book.Contains("CouncilTasks.HomeLost(h);"), "each home's tasks: saved, waited on, let go with the home");
        C(book.Contains("Scribe_Values.Look(ref drillPeople") && book.Contains("Scribe_Values.Look(ref drillAt"), "the drilled people kept a season beyond the task");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"Diplomacy/DiplomacyBook.cs:tasks\"") && est.Contains("const(\"Diplomacy/TaskMath.cs\", \"MaxPerHome\", 13)"), "the save model has the tasks");
        string hub = Code("Diplomacy/Diplomacy.cs");
        C(hub.Contains("CouncilTasks.Hourly(now)") && hub.Contains("KeeperOfSecrets.Hourly(now)") && hub.Contains("CouncilTasks.Daily(now)") && hub.Contains("SecretTies.Daily(now)"),
            "the hour and the day ride on diplomacy's");
        // Fealty (release 2c.2) comes after it.
        C(Regex.IsMatch(Code("World/NotableMath.cs"), @"Match,\s*///[^\n]*\n\s*Task,\s*///[^\n]*\n\s*Fealty\s*\}"), "the letter purpose Task after Match, Fealty after it");
        C(Code("World/NeighboursPanel.cs").Contains("if (TaskLetters.Hidden(p)) continue;"), "never offered in the letter dialog");
        string letters = Code("Diplomacy/DiplomacyLetters.cs");
        C(letters.Contains("TaskLetters.Owns(p)") && letters.Contains("TaskLetters.Read(l, n, speaker, ref tone, a.things)") && letters.Contains("TaskLetters.QuoteTags(l, n, tags);"),
            "the letters' frame owns it: why not, their answer, their words");
        C(dip["TaskLetters.cs"].Contains("ChannelWhyNot(n, LetterChannel.Console, map) == null ? LetterChannel.Console : LetterChannel.Courier"),
            "a call where both sides have the means, else a letter by hand");
        C(dip["TaskVisits.cs"].Contains("JourneyRun.Confirm(plan, true)") && dip["TaskVisits.cs"].Contains("new JourneyPlan") && dip["TaskVisits.cs"].Contains("CaravanArrivalAction_Enter"),
            "visits by a real caravan through the journey planner, home again by the caravan's own road");
        C(dip["TaskVisits.cs"].Contains("JourneyBook.PlanAt(home)") && dip["TaskVisits.cs"].Contains("CouncilTasks.Away(home, self)"), "one plan a home, one officer away");
        C(Code("Diplomacy/Feasts.cs").Contains("TaskMath.Welcome(g.welcome, TaskResults.Stewards(map, g.people))"), "the steward's guests' welcome");
        C(Code("Diplomacy/Pacts.cs").Contains("WarCamps.MakeFighters(f, home, TaskResults.LevyStrength(f, home)") && Code("Camps/WarCamp.cs").Contains("float strength = 1f"),
            "the drilled band a fifth stronger");
        string ransoms = Code("Diplomacy/Ransoms.cs");
        C(ransoms.Contains("public static bool DemandFor(Map map, Pawn p, Faction f, float factor)") && ransoms.Contains("TaskResults.KinTask(f, map) ? TaskMath.KinSooner")
          && ransoms.Contains("o.price * TaskMath.KinHigher"), "the warden's lower price, their kin's offers sooner and higher");
        C(Code("World/Region/RaidersCamp.cs").Contains("public static bool SpotByPatrol(Map home, Pawn spotter)"), "the patrol sees a hidden camp");
        // The round c review: a courier trip is let go in the pass its people leave the map, so a task reading its tally keeps it till then.
        C(Code("Couriers/CourierTracker.cs").Contains("AllGone(t) && !CouncilTasks.Awaits(t.id)") && Code("Diplomacy/CouncilTasks.cs").Contains("public static bool Awaits(int tripId)")
          && Code("Diplomacy/CouncilTasks.cs").Contains("if (t.kind == TaskKind.Trade) SamplesHour(t);\n            if (now < t.nextTick) return;"),
            "an informer paid and samples fetched are read from their trip, kept for the task, and the samples looked at hourly");
        C(Code("UI/DecreePanel.cs").Contains("TaskPanels.OfficeRow(office, holder, colony.Map"), "Council → Offices: each seat's task under its row");
        C(Code("UI/Region/HereAndAwayView.cs").Contains("dip.tasks") && Code("Diplomacy/DiplomacyModel.cs").Contains("TaskPanels.AwayRows()"), "Here and away: the tasks and the officer away");
        C(dip["TaskPanels.cs"].Contains("Inbox.Register(\"task\", InboxSection.Needs") && dip["TaskPanels.cs"].Contains("Inbox.Register(\"task-under-way\", InboxSection.UnderWay")
          && dip["TaskPanels.cs"].Contains("steps = TaskWords.Steps"), "This week: a step that needs the player, tasks under way in steps");
        C(Code("Text/Facts/FactsLedger.cs").Contains("TaskFacts(s, f);"), "the facts read by the ties' provider");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool diplomacyTasks = true;") && settings.Contains("Scribe_Values.Look(ref diplomacyTasks, \"diplomacyTasks\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyTasks\""), "the setting in the Diplomacy section, on by default");
        C(dip["CouncilTasks.cs"].Contains("public static Func<Faction, Map, string> DuesWhyNot;") && dip["CouncilTasks.cs"].Contains("public static Action<CouncilTask, DiplomacyHome, int> DuesStep;")
          && dip["CouncilTasks.cs"].Contains("public static CourierTrip SendCart("), "the entry points for release 2c.2's dues");
        C(dip["KeeperOfSecrets.cs"].Contains("public static bool Watching(Map home)") && dip["SecretTies.cs"].Contains("public static Tie Learn(Ref knower, Ref about, string kind, Pawn by)"),
            "the entry points for release 2c.3: the keeper's watch, Secret ties either way");
        C(dip["TaskResults.cs"].Contains("TaskMath.SickBack(Rand.Value)") && dip["TaskWords.cs"].Contains("CC_Task_Risk_Tend") && dip["TaskWords.cs"].Contains("CC_Task_Risk_Secrets"),
            "the bad sides named before the choice");
        // Speech and the map.
        foreach (var f in new[] { "KeeperOfSecrets.cs", "SecretTies.cs", "TaskJobs.cs", "CouncilTasks.cs" })
            C(!Regex.IsMatch(dip[f], @"MoteMaker|ThrowText|PlayLog\.Add"), $"{f} speaks only through Speech (Outsiders.Say)");

        // ---------------- The words ----------------

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Tasks.xml"));
        string variants = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Tasks_Variants.xml"));
        foreach (var k in TaskMath.Kinds)
        {
            foreach (var p in new[] { "CC_Task_Kind_", "CC_Task_KindTip_", "CC_Task_Row_", "CC_Task_How_", "CC_Task_Why_NoTarget_", "CC_Task_Next_", "CC_Task_Topic_" })
                C(keyed.Contains("<" + p + k + ">"), $"{p}{k} has its words");
        }
        foreach (var k in KeeperMath.Kinds) C(keyed.Contains("<CC_Secret_Their_" + k + ">"), $"the secret {k} has its words");
        foreach (var s in new[] { "Talk", "Informer", "Visit" })
            foreach (var p in new[] { "CC_Keeper_How_", "CC_Keeper_Nothing_", "CC_Keeper_Caught_" })
                C(keyed.Contains("<" + p + s + ">"), $"{p}{s} has its words");
        foreach (var s in new[] { "Open", "CalledIn", "Kept", "Broken", "Ended" }) C(keyed.Contains("<CC_Keeper_Tie_" + s + ">"), $"a Secret tie {s} in words");
        foreach (var s in new[] { "Letter", "Call", "Talk", "Informer", "Patrol", "Visit", "Guest", "Cart", "Feast" }) C(keyed.Contains("<CC_Task_Last_" + s + ">"), $"the last step {s} in words");
        foreach (var k in new[] { TaskKind.Scout, TaskKind.KeepFaith, TaskKind.SpeakFaith, TaskKind.TheirKin }) C(keyed.Contains("<CC_Task_Result_Season_" + k + ">"), $"{k}'s season in words");
        C(keyed.Contains("<CC_Letter_Purpose_Task>") && keyed.Contains("<CC_Letter_PurposeCap_Task>") && File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Outsiders.xml")).Contains("<CC_Out_Act_SoundOut>")
          && keyed.Contains("<CC_Dip_For_wonOver>") && keyed.Contains("<CC_RaidCamp_Doing_Patrol>") && keyed.Contains("<CC_Settings_DiplomacyTasks_Tip>"), "the shared frames' words");
        foreach (Match m in Regex.Matches(variants, @"<(CC_[A-Za-z_]+?)_\d+>"))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value} has its original beside its fellow");
        foreach (var text in new[] { keyed, variants })
            C(!text.Contains("—") && !Regex.IsMatch(text, @">[^<]*![^<]*<"), "no em dashes, no exclamation marks");

        // ---------------- Lines and talk ----------------

        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Tasks.xml")).Root!.Elements("ColonyCouncil.FlavorExtensionDef").ToList();
        var writes = lines.First(e => e.Element("pool")!.Value == "Rimfolk_Pool_NeighbourWrites").Descendants("li").Select(e => e.Value).ToList();
        var says = lines.First(e => e.Element("pool")!.Value == "Rimfolk_Pool_OutsiderSays").Descendants("li").Select(e => e.Value).ToList();
        foreach (var code in new[] { "winover", "trade", "bringhome", "theirkin", "keepfaith", "speakfaith", "host" })
            C(writes.Any(l => l.StartsWith("[task,") && l.Contains("," + code)), $"their answers to the {code} letters");
        C(writes.Any(l => l.StartsWith("[task,curt,pressed")) && writes.Any(l => l.StartsWith("[task,refusal,pressed")), "their answers to a secret pressed: paid curtly, refused");
        C(writes.Count(l => l.StartsWith("[task,")) >= 20, $"twenty letter lines at least ({writes.Count})");
        foreach (var moment in new[] { "soundask", "soundtalk", "pressyes", "pressno", "keepercaught", "keeperwatch", "patrol" })
            C(says.Any(l => l.StartsWith("[" + moment + ",")), $"bubbles for {moment}");
        foreach (var l in says)
        {
            string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(Regex.Matches(body, @"[.?!](\s|$)").Count <= 1, $"one sentence: {body}");
        }
        var pieces = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Tasks.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(pieces.Count >= 20 && pieces.Count <= 40, $"20 to 40 talk pieces ({pieces.Count})");
        C(pieces.All(p => p.Element("defName")!.Value.StartsWith("Rimfolk_OT_Task_") || p.Element("defName")!.Value.StartsWith("Rimfolk_OT_Keeper_")), "named for the tasks and the keeper");
        C(pieces.Any(p => p.Element("when")!.Value.Contains("them:caughtkeeper")) && pieces.Any(p => p.Element("when")!.Value.Contains("them:pressed"))
          && pieces.Any(p => p.Element("when")!.Value.Contains("keeper:watched")) && pieces.Any(p => p.Element("when")!.Value.Contains("task:writes")), "said on the facts the provider sets");
        return n;
    }
}
