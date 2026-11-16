using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the quests at home, round 1's six and round 2's seven ("Rimfolk", "Quests at home: ..."). Offer one now (into the Quests tab, as the
    /// storyteller would), accept the open one, bring on what it waits for now (their people coming, the days held over,
    /// their handler or collector), end it with any of its outcomes, and what it keeps (log).
    /// </summary>
    public static class HomeQuestsDebug
    {
        private static void Say(string text) => Messages.Message("Quests at home: " + text, MessageTypeDefOf.SilentInput, false);

        private static readonly string[] Defs =
        {
            "Rimfolk_Mission_Prisoner", "Rimfolk_Mission_Dispute", "Rimfolk_Mission_Messenger", "Rimfolk_Mission_Outlaw", "Rimfolk_Mission_Keepsake",
            "Rimfolk_Mission_TameFor",
            // Round 2 (Workers_MissionHome3.cs, Workers_MissionHome4.cs).
            "Rimfolk_Mission_Trainee", "Rimfolk_Mission_WeddingPiece", "Rimfolk_Mission_SickAnimal", "Rimfolk_Mission_CampGuard", "Rimfolk_Mission_SeedKept",
            "Rimfolk_Mission_BookCopy", "Rimfolk_Mission_Musicians"
        };

        private static Mission Open() => Missions.All.FirstOrDefault(m => m.Open && HomeQuests.IsOurs(m));

        [DebugAction("Rimfolk", "Quests at home: offer one now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in Defs)
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label, () =>
                {
                    var open = Missions.All.FirstOrDefault(m => m.Open);
                    if (open != null)
                    {
                        Say("another mission is open (" + open.title + "); end it first.");
                        return;
                    }
                    var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                    parms.forced = true;
                    bool ok = def.Worker.TryExecute(parms);
                    Say(ok ? def.label + " offered: it is in the Quests tab." : def.label + " can't be offered here now (no people at peace to ask, or nothing on the map it needs).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Quests at home: accept the open one", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAccept()
        {
            var m = Open();
            if (m == null || m.state != MissionState.Offered)
            {
                Say("no quest at home waiting for an answer.");
                return;
            }
            var q = Missions.QuestOf(m);
            if (q != null && q.State == QuestState.NotYetAccepted) q.Accept(null);
            Missions.Accept(m);
            Say(m.title + ": " + m.state + ".");
        }

        [DebugAction("Rimfolk", "Quests at home: what it waits for comes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNow()
        {
            var m = Open();
            if (m == null || m.state != MissionState.Active)
            {
                Say("no quest at home under way.");
                return;
            }
            int now = Find.TickManager.TicksGame;
            m.due = now;
            if (m.kind == HomeQuestsMath.MessengerKind && m.stage == 1) m.lastLook = now - GenDate.TicksPerDay;
            if (m.kind == HomeQuestsMath.PrisonerKind && m.stage == 1) m.lastLook = now - GenDate.TicksPerDay;
            if (m.kind == HomeQuestsMath.DisputeKind && m.stage == 1) m.lastLook = now - GenDate.TicksPerDay;
            Say(m.title + ": stage " + m.stage + ", due now.");
        }

        [DebugAction("Rimfolk", "Quests at home: end the open one as...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnd()
        {
            var m = Open();
            var map = m?.Map;
            if (m == null || m.state != MissionState.Active || map == null)
            {
                Say("no quest at home under way.");
                return;
            }
            var w = Missions.Worker(m);
            string[] outcomes;
            System.Action<string> end;
            switch (w)
            {
                case HappeningWorker_MissionPrisoner p:
                    outcomes = HappeningWorker_MissionPrisoner.DebugOutcomes;
                    end = o => p.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionDispute d:
                    outcomes = HappeningWorker_MissionDispute.DebugOutcomes;
                    end = o => d.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionMessenger r:
                    outcomes = HappeningWorker_MissionMessenger.DebugOutcomes;
                    end = o => r.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionOutlaw o2:
                    outcomes = HappeningWorker_MissionOutlaw.DebugOutcomes;
                    end = o => o2.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionKeepsake k:
                    outcomes = HappeningWorker_MissionKeepsake.DebugOutcomes;
                    end = o => k.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionTameFor t:
                    outcomes = HappeningWorker_MissionTameFor.DebugOutcomes;
                    end = o => t.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionTrainee q3:
                    outcomes = HappeningWorker_MissionTrainee.DebugOutcomes;
                    end = o => q3.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionWeddingPiece q4:
                    outcomes = HappeningWorker_MissionWeddingPiece.DebugOutcomes;
                    end = o => q4.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionSickAnimal q6:
                    outcomes = HappeningWorker_MissionSickAnimal.DebugOutcomes;
                    end = o => q6.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionCampGuard q8:
                    outcomes = HappeningWorker_MissionCampGuard.DebugOutcomes;
                    end = o => q8.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionSeedGrain q11:
                    outcomes = HappeningWorker_MissionSeedGrain.DebugOutcomes;
                    end = o => q11.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionBookCopy q12:
                    outcomes = HappeningWorker_MissionBookCopy.DebugOutcomes;
                    end = o => q12.DebugEnd(m, map, o);
                    break;
                case HappeningWorker_MissionMusicians q13:
                    outcomes = HappeningWorker_MissionMusicians.DebugOutcomes;
                    end = o => q13.DebugEnd(m, map, o);
                    break;
                default:
                    Say("not a quest at home.");
                    return;
            }
            Find.WindowStack.Add(new FloatMenu(outcomes.Select(o => new FloatMenuOption(o, () =>
            {
                end(o);
                Say(m.title + ": " + o + " (" + m.state + ").");
            })).ToList()));
        }

        [DebugAction("Rimfolk", "Quests at home: what the open one keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLog()
        {
            var sb = new StringBuilder("[Rimfolk] Quests at home:\n");
            foreach (var m in Missions.All.Where(HomeQuests.IsOurs))
                sb.AppendLine($"  {m.id} {m.kind} '{m.title}' {m.state} stage {m.stage}, variant {m.variant ?? "-"}, of {m.faction?.Name ?? "?"} (other {m.other?.Name ?? "-"}), "
                              + $"x {m.x?.LabelShort ?? "-"} ({(m.x == null ? "-" : m.x.Dead ? "dead" : m.x.Spawned ? m.x.Position.ToString() : "away")}), group {m.group.Count}, "
                              + $"due {m.due}, deadline {m.deadline}, target {m.target}, tally {m.tally}, misses {m.misses}, cell {m.cell}, area {m.area}, "
                              + $"thing {m.thing?.LabelShort ?? "-"}, reward {m.reward}, outcome {m.outcome ?? "-"}.");
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }
    }
}
