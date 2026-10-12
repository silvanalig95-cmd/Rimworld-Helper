using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode: start, hurry or end a world event (Debug actions, "Rimfolk").</summary>
    public static class WorldSituationsDebug
    {
        [DebugAction("Rimfolk", "Start world event...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Start()
        {
            var inst = WorldSituations.Instance;
            var map = Find.CurrentMap ?? HappeningTracker.HomeMap();
            if (inst == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var d in DefDatabase<WorldSituationDef>.AllDefsListForReading.OrderBy(d => d.label))
            {
                var def = d;
                bool can;
                try
                {
                    can = def.Worker.CanStart(map);
                }
                catch
                {
                    can = false;
                }
                options.Add(new FloatMenuOption(def.LabelCap + (can ? "" : " (can't now)"), () =>
                {
                    var s = inst.TryStart(map, x => x == def, true);
                    if (s == null) Messages.Message("Couldn't start " + def.label + " now (nobody fits, or one of its kind is running).", MessageTypeDefOf.RejectInput, false);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Next world event beat now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NextBeat()
        {
            var inst = WorldSituations.Instance;
            if (inst == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var s in inst.Running.ToList())
                foreach (var b in s.beats.OrderBy(b => b.due).ToList())
                {
                    var bs = b;
                    options.Add(new FloatMenuOption(s.Label + ": " + bs.key, () => bs.due = Find.TickManager.TicksGame));
                }
            if (options.Count == 0) options.Add(new FloatMenuOption("(nothing waiting)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "End world event...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void End()
        {
            var inst = WorldSituations.Instance;
            if (inst == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var s in inst.Running.ToList())
            {
                var sit = s;
                foreach (var e in sit.def.endings)
                {
                    var ending = e;
                    options.Add(new FloatMenuOption(sit.Label + " → " + ending.key, () => inst.End(sit, ending.key)));
                }
                options.Add(new FloatMenuOption(sit.Label + " → (quietly)", () => inst.End(sit, WorldSituations.Quietly)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(none running)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Next world event in an hour", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Soon()
        {
            var inst = WorldSituations.Instance;
            if (inst != null) inst.nextTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
        }
    }
}
