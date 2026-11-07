using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the colony's animals hunting (Debug actions, "Rimfolk", "Pets hunting: ..."). Each starts what the game
    /// would on its own a little sooner; what follows is the game's own hunt (Animals/PetHunts.cs).
    /// </summary>
    public static class PetHuntsDebug
    {
        private static void Say(string text) => Messages.Message("Pets hunting: " + text, MessageTypeDefOf.SilentInput, false);

        private static List<Pawn> Animals => Find.CurrentMap?.mapPawns.SpawnedColonyAnimals.ToList() ?? new List<Pawn>();

        private static void Menu(IEnumerable<Pawn> animals, System.Action<Pawn> act)
        {
            var list = animals.Select(a => new FloatMenuOption(a.LabelShort + " (" + a.KindLabel + ", " + PetHunts.Judge(a.def).level + ")", () => act(a))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(none of the colony's animals here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Pets hunting: why an animal hunts or not (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain() => Menu(Animals, a =>
        {
            Log.Message("[Rimfolk] Pets hunting:\n" + PetHunts.Explain(a));
            Say(a.LabelShort + ": " + PetHunts.Judge(a.def).level + ", " + (PetHunts.WhyNot(a) ?? "may hunt now") + "; the prey it could take is in the log.");
        });

        [DebugAction("Rimfolk", "Pets hunting: make an animal hungry...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHungry() => Menu(Animals.Where(a => a.needs?.food != null), a =>
        {
            a.needs.food.CurLevelPercentage = 0.05f;
            a.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            Say(a.LabelShort + " is hungry now: with nothing it may eat in its area, a hunter goes after prey; else the alert names it.");
        });

        [DebugAction("Rimfolk", "Pets hunting: send a hunter after small game now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSport() => Menu(Animals.Where(PetHunts.HuntingKind), a =>
            Say(PetHunts.TrySport(a, false, out string why) ? a.LabelShort + " is off after small game." : a.LabelShort + " can't now: " + why));

        [DebugAction("Rimfolk", "Pets hunting: send a hunter to bring its person a kill...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBring() => Menu(Animals.Where(PetHunts.HuntingKind), a =>
            Say(PetHunts.TrySport(a, true, out string why) ? a.LabelShort + " is off after small game, to bring it to " + (PetTracker.PersonOf(a)?.LabelShort ?? "nobody") + "."
                : a.LabelShort + " can't now: " + why));

        [DebugAction("Rimfolk", "Pets hunting: the food chain of every loaded animal (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugChain()
        {
            var races = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race != null && d.race.Animal).ToList();
            var lines = races.Select(d => (d, j: PetHunts.Judge(d))).OrderByDescending(x => x.j.level).ThenBy(x => x.d.defName)
                .Select(x => "  " + x.d.defName + " (" + x.d.label + "): " + x.j).ToList();
            Log.Message("[Rimfolk] The food chain of " + races.Count + " animals (" + races.Count(d => PetHunts.Judge(d).Hunts) + " hunt):\n" + string.Join("\n", lines));
            Say("the food chain of " + races.Count + " animals is in the log.");
        }
    }
}
