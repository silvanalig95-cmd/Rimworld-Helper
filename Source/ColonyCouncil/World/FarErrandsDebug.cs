using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode: a world event that sends a colonist to a far town, and the errand hurried along (Debug actions, "Rimfolk").</summary>
    public static class FarErrandsDebug
    {
        private static void Say(string text) => Messages.Message("[Rimfolk] World event errand: " + text, MessageTypeDefOf.NeutralEvent, false);

        /// <summary>The beats whose answers send someone away, by event.</summary>
        private static IEnumerable<(WorldSituationDef def, SituationBeat beat)> Sending() =>
            from d in DefDatabase<WorldSituationDef>.AllDefsListForReading
            from b in d.beats
            where b.options != null && b.options.Any(o => (o.effects?.OfType<WorldEffect_Away>().Any() ?? false) || (o.riskEffects?.OfType<WorldEffect_Away>().Any() ?? false))
            select (d, b);

        [DebugAction("Rimfolk", "World event: the question that sends a colonist away, now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ask()
        {
            var inst = WorldSituations.Instance;
            var map = Find.CurrentMap ?? HappeningTracker.HomeMap();
            if (inst == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var (def, beat) in Sending().OrderBy(x => x.def.label))
            {
                var d = def;
                var b = beat;
                options.Add(new FloatMenuOption(d.LabelCap + ": " + b.key, () =>
                {
                    var s = inst.Running.FirstOrDefault(x => x.def == d) ?? inst.TryStart(map, x => x == d, true);
                    if (s == null)
                    {
                        Say("couldn't start " + d.label + " now (nobody fits, or one of its kind is running).");
                        return;
                    }
                    // What the question needs to come (the exiles taken in, the hunters turned away) is set, what keeps it off cleared.
                    if (b.requires != null) foreach (var f in b.requires) s.Set(f);
                    if (b.forbids != null) foreach (var f in b.forbids) s.Clear(f);
                    inst.Call(s, b.key, 0f);
                    Say(d.label + "'s question \"" + b.key + "\" comes within the hour.");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no world event sends anyone away)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static List<Visit> Errands() =>
            CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && FarErrands.Note(v, out _)).ToList() ?? new List<Visit>();

        [DebugAction("Rimfolk", "World event: the colonist sent away is there now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ThereNow()
        {
            var v = Errands().FirstOrDefault(x => x.stake == PeopleEvents2Math.Going);
            var c = v?.x?.GetCaravan();
            var tile = SeasonAway.TileOf(v);
            if (c == null || !tile.Valid)
            {
                Say("nobody is on the road to a far town now.");
                return;
            }
            c.pather?.StopDead();
            c.Tile = tile;
            Say(v.x.LabelShort + " is at " + SeasonAway.PlaceOf(v) + "; the arrival comes within the hour.");
        }

        [DebugAction("Rimfolk", "World event: the colonist sent away's days there up now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DaysUp()
        {
            var v = Errands().FirstOrDefault(x => x.stake == PeopleEvents2Math.There);
            if (v == null)
            {
                Say("nobody is at a far town now.");
                return;
            }
            v.due = Find.TickManager.TicksGame;
            Say(v.x.LabelShort + "'s days at " + SeasonAway.PlaceOf(v) + " are up: they set out for home within the hour (or stay on while a question for them there waits).");
        }

        [DebugAction("Rimfolk", "World event: the colonist sent away home now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HomeNow()
        {
            var v = Errands().FirstOrDefault(x => x.stake == PeopleEvents2Math.Coming);
            var c = v?.x?.GetCaravan();
            var map = v?.map;
            if (c == null || map == null || !Find.Maps.Contains(map))
            {
                Say("nobody is on the road home from a far town now.");
                return;
            }
            c.pather?.StopDead();
            CaravanEnterMapUtility.Enter(c, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop);
            Say(v.x.LabelShort + " walks in from the edge of the map; the homecoming comes within the hour.");
        }
    }
}
