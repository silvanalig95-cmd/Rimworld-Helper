using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode for the content update's variety, the world (Debug actions, "Rimfolk"): each new world event, camp and
    /// encounter started now, the stud visit offered and hurried, the pardon's prisoners and the games day looked at.
    /// </summary>
    public static class VarietyDebug
    {
        private static Map Map => Find.CurrentMap ?? HappeningTracker.HomeMap();

        private static void Say(string text) => Messages.Message("World variety: " + text, MessageTypeDefOf.SilentInput, false);

        /// <summary>The world events of this round.</summary>
        public static readonly string[] WorldEvents =
        {
            "Rimfolk_WS_ClothScarce", "Rimfolk_WS_TimberScarce", "Rimfolk_WS_Games", "Rimfolk_WS_Pardon", "Rimfolk_WS_HiringFair", "Rimfolk_WS_FoundingDay",
            "Rimfolk_WS_MudRoads", "Rimfolk_WS_FallingStars", "Rimfolk_WS_GreatBurying", "Rimfolk_WS_BridgeDown"
        };

        /// <summary>The camps of this round.</summary>
        public static readonly string[] Camps =
        {
            "Rimfolk_Camp_Beekeepers", "Rimfolk_Camp_MushroomPickers", "Rimfolk_Camp_YoungHands", "Rimfolk_Camp_StrandedCarters", "Rimfolk_Camp_Gleaners",
            "Rimfolk_Camp_GamesTeam", "Rimfolk_Camp_WalkingHome", "Rimfolk_Camp_StarWatchers"
        };

        [DebugAction("Rimfolk", "World variety: start one of the new world events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
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
                options.Add(new FloatMenuOption(def.LabelCap + (can ? "" : " (can't now: its season, its people, or what it follows)"), () =>
                {
                    var s = inst.TryStart(map, x => x == def, true);
                    Say(s != null ? def.label + " started." : "couldn't start " + def.label + " now (nobody fits, or one of its kind is running).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "World variety: one of the new camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Camp()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (string n in Camps)
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label, () =>
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

        [DebugAction("Rimfolk", "World variety: a people's own encounter now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Encounter()
        {
            var map = Map;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var d in DefDatabase<EncounterDef>.AllDefsListForReading.Where(d => d.stranger != null && !d.stranger.calling.NullOrEmpty()).OrderBy(d => d.stranger.calling))
            {
                var def = d;
                options.Add(new FloatMenuOption(def.stranger.calling + ": " + def.label, () =>
                {
                    bool ok = comp.encounters.TryOne(comp, map, Strangers.Present(map), x => x == def);
                    Say(ok ? def.label + "." : def.label + ": nobody fits now (a " + def.stranger.kind + " of the " + def.stranger.calling + " with a colonist"
                                                + (def.scene.NullOrEmpty() ? "" : ", and the " + def.scene + " scene true on the map") + ").");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "World variety: a stud visit offered now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Stud()
        {
            var map = Map;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Mission_StudVisit");
            if (def == null || map == null) return;
            var open = Missions.All.FirstOrDefault(m => m.Open);
            if (open != null)
            {
                Say("another mission is open (" + open.title + "); end it first.");
                return;
            }
            var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
            parms.forced = true;
            bool ok = def.Worker.TryExecute(parms);
            Say(ok ? "a stud visit offered." : "can't be offered here now (no people at peace, or no animals of the colony's that breed by mating).");
        }

        [DebugAction("Rimfolk", "World variety: the stud visit's days up now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StudDaysUp()
        {
            var m = Missions.All.FirstOrDefault(x => x.Open && x.kind == HappeningWorker_MissionStud.StudKind);
            if (m == null)
            {
                Say("no stud visit open.");
                return;
            }
            HappeningWorker_MissionStud.DebugDaysUp(m);
            Say("the stud visit (stage " + m.stage + ") moves on at its next look.");
        }

        [DebugAction("Rimfolk", "World variety: what the pardon would cover here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PardonLog()
        {
            var lines = new List<string>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)))
            {
                var held = Pardon.Covered(f);
                if (held.Count > 0) lines.Add(f.Name + ": " + string.Join(", ", held.Select(p => p.LabelShort)));
            }
            Log.Message("[Rimfolk] The pardon would cover: " + (lines.Count == 0 ? "nobody (no prisoners of a people at peace)." : string.Join("; ", lines)));
            Say("listed in the log.");
        }
    }
}
