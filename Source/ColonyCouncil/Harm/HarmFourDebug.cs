using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: harm, part 4 and the shared budget (Debug actions, "Rimfolk", "Harm four: ..."). Each brings one of the
    /// fifteen about now, or makes its next chance come up, past its rate but never past the shared cap: what follows is
    /// the feature's own. The budget's tool logs each kind, its system, how lately it came and its pass now.
    /// </summary>
    public static class HarmFourDebug
    {
        private static void Say(string text) => Messages.Message("Harm four: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Pick(IEnumerable<Pawn> pawns, Func<Pawn, string> act)
        {
            var options = pawns.Select(p => new FloatMenuOption(p.LabelShort, () => Say(act(p)))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<Pawn> Ours => Find.CurrentMap.mapPawns.FreeColonistsSpawned;

        [DebugAction("Rimfolk", "Harm four: the shared budget (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBudget()
        {
            Log.Message(Harm.BudgetReport());
            Say("each kind's pass within the shared budget is in the log.");
        }

        [DebugAction("Rimfolk", "Harm four: a doctor catches what a patient has...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPhysician()
        {
            Pick(Ours.Where(p => p.health.hediffSet.hediffs.Any(h => h.Visible && h.def.defName.StartsWith("Council_Ail_", StringComparison.Ordinal) || h.def.defName == "Flu")), patient =>
            {
                var doctor = Ours.Where(o => o != patient && !o.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).OrderByDescending(o => o.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
                return doctor == null ? "no doctor here." : Catching.DebugCatch(doctor, patient);
            });
        }

        [DebugAction("Rimfolk", "Harm four: a guest coughs...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGuestCough()
        {
            Pick(Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer)), Catching.DebugCough);
        }

        [DebugAction("Rimfolk", "Harm four: a cold night for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugColdNight() => Pick(Ours, Catching.DebugColdNight);

        [DebugAction("Rimfolk", "Harm four: the next hailstone bruises", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHail()
        {
            Mishaps.DebugHail = true;
            Say("the next stone of a falling hail bruises one of the colony out in it (Rimfolk events: the hailstorm).");
        }

        [DebugAction("Rimfolk", "Harm four: the next tree felled comes down the wrong way", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFelling()
        {
            Mishaps.DebugFell = true;
            Say("the next tree a colonist fells comes down on them (with the deeper hooks), whatever their skill.");
        }

        [DebugAction("Rimfolk", "Harm four: the next failed build hurts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBuild()
        {
            Mishaps.DebugBuild = true;
            Say("the next construction that fails hurts its builder (with the deeper hooks), heavy or not.");
        }

        [DebugAction("Rimfolk", "Harm four: a drink before hot work for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHot() => Pick(Ours, Mishaps.DebugHot);

        [DebugAction("Rimfolk", "Harm four: a rutting male charges...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRut() => Pick(Ours, AnimalHurts.DebugRut);

        [DebugAction("Rimfolk", "Harm four: the next herdmaster's lesson kicks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLesson() => Say(AnimalHurts.DebugLesson());

        [DebugAction("Rimfolk", "Harm four: the next dismount throws its rider", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSaddle() => Say(AnimalHurts.DebugSaddle());

        [DebugAction("Rimfolk", "Harm four: two rival groups here come to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGroups() => Say(Quarrels.DebugGroups(Find.CurrentMap));

        [DebugAction("Rimfolk", "Harm four: a sore loser walks over to...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSore()
        {
            Pick(Ours.Where(p => !p.DevelopmentalStage.Juvenile()), winner =>
            {
                var loser = Ours.Where(o => o != winner && !o.DevelopmentalStage.Juvenile()).OrderBy(o => o.relations?.OpinionOf(winner) ?? 0).FirstOrDefault();
                if (loser == null) return "nobody else here to be the loser.";
                HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Sore, kind = HarmKinds.SoreLoser.key, pawn = loser, other = winner, due = Find.TickManager.TicksGame });
                return loser.LabelShort + " goes to find " + winner.LabelShort + " within the hour (words, and blows half the time, under the cap).";
            });
        }

        [DebugAction("Rimfolk", "Harm four: tonight's guest with a bed sours", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNight() => Say(Quarrels.DebugNight());

        [DebugAction("Rimfolk", "Harm four: a rematch owed to...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRematch() => Pick(Ours.Where(p => !p.DevelopmentalStage.Juvenile()), Quarrels.DebugRematch);

        [DebugAction("Rimfolk", "Harm four: the games' contests now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGames() => Say(GamesDay.DebugContests(Find.CurrentMap));

        [DebugAction("Rimfolk", "Harm four: the next race's turn sprains", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSprain() => Say(GamesDay.DebugSprain());
    }
}
