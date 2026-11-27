using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode for the content round's camps and world events (Debug actions, "Rimfolk"): each new world event started now,
    /// each new camp brought here now (beside any already here), and the camps' turns hurried: the diggers' share, the stone
    /// standing.
    /// </summary>
    public static class Round2Debug
    {
        private static Map Map => Find.CurrentMap ?? HappeningTracker.HomeMap();

        private static void Say(string text) => Messages.Message("Round 2: " + text, MessageTypeDefOf.SilentInput, false);

        /// <summary>The world events of this round.</summary>
        public static readonly string[] WorldEvents =
        {
            "Rimfolk_WS_GodPutAway", "Rimfolk_WS_DancingSickness", "Rimfolk_WS_RestlessDead", "Rimfolk_WS_BadCoin", "Rimfolk_WS_Rising", "Rimfolk_WS_TwoClaims"
        };

        [DebugAction("Rimfolk", "Round 2: start one of the new world events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Start()
        {
            var inst = WorldSituations.Instance;
            var map = Map;
            if (inst == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (string n in WorldEvents)
            {
                var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                bool can;
                try
                {
                    can = def.Worker.CanStart(map);
                }
                catch
                {
                    can = false;
                }
                options.Add(new FloatMenuOption(def.LabelCap + (can ? "" : " (can't now: its people, its gods or its season)"), () =>
                {
                    var s = inst.TryStart(map, x => x == def, true);
                    Say(s != null ? def.label + " started." : "couldn't start " + def.label + " now (nobody fits, or one of its kind is running).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Round 2: one of the new camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Camp()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (string n in CampRound2.Kinds)
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label + (kind.FitsMap(map) ? "" : " (the land or the world lacks what they come for)"), () =>
                {
                    var tracker = CampTracker.Instance;
                    if (tracker == null || !CampTracker.Enabled)
                    {
                        Say("camps are off.");
                        return;
                    }
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    var why = new List<string>();
                    var c = f == null ? null : tracker.StartKind(map, kind, f, why);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: " + (f == null ? "no people at peace that fits" : string.Join("; ", why)));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Round 2: the camps' turns now (the diggers' share, the stone standing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Turns()
        {
            var map = Map;
            int n = 0;
            foreach (var c in CampTracker.VisitorsAt(map))
            {
                if (c.kind?.Worker is CampWorker_Diggers && CampWorker_Diggers.StateOf(c) is DigState d && !d.shared)
                {
                    CampWorker_Diggers.Share(c, d);
                    n++;
                }
                if (c.kind?.Worker is CampWorker_StoneRaisers && CampWorker_StoneRaisers.StateOf(c) is StoneState s && s.laid && !s.stood && s.carve)
                {
                    s.worked = s.days;
                    n++;
                }
            }
            Say(n > 0 ? n + " camp turn(s) hurried." : "no diggers or carving stone-raisers here.");
        }
    }
}
