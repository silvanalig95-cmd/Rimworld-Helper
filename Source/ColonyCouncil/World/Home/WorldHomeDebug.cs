using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: each part of the world brought home now, on the map being looked at ("Rimfolk", "World home: ..."): a quest a
    /// running world event makes likelier, a messenger's word from the road, an encounter or a council question of a world
    /// event's season, a great one taken for a god's doing, the next vigil day, and the mourning colours put back. Start the
    /// world event first ("Start world event...").
    /// </summary>
    public static class WorldHomeDebug
    {
        private static void Say(string text) => Messages.Message("World home: " + text, MessageTypeDefOf.SilentInput, false);

        private static Map Map => Find.CurrentMap;

        private static Pawn Selected => Find.Selector?.SingleSelectedThing as Pawn;

        [DebugAction("Rimfolk", "World home: a quest a running world event makes likelier, now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Steered()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (var s in WorldSeason.Running)
                foreach (var m in s.def.modifiers.Where(m => !m.rimfolk.NullOrEmpty() && m.Factor > 1f))
                {
                    var def = DefDatabase<IncidentDef>.GetNamedSilentFail(m.rimfolk);
                    if (def == null || def.GetModExtension<HappeningExtension>() == null) continue;
                    options.Add(new FloatMenuOption(def.label + " (" + s.def.label + ")", () =>
                    {
                        var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                        parms.forced = true;
                        Storylines.debugForce = true;
                        try
                        {
                            Say(def.Worker.TryExecute(parms) ? def.label + " offered." : def.label + " couldn't be offered now (another mission open, nobody to ask).");
                        }
                        finally
                        {
                            Storylines.debugForce = false;
                        }
                    }));
                }
            if (options.Count == 0) Say("no running world event makes a quest likelier (start a plague, a drought, the assize...).");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "World home: the road's news from a visitor or courier (selected)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Road()
        {
            var p = Selected;
            if (p == null || p.Faction == null || p.Faction.IsPlayer) Say("select a visitor, trader or courier on the map.");
            else
            {
                var s = RoadNews.News(p.Faction, p.Map);
                if (s == null) Say("no world event on their road.");
                else Say(RoadNews.Say(p, p.Faction, p.Map, true) ? "said, of " + s.Label + "." : "nothing to say of " + s.Label + ".");
            }
        }

        [DebugAction("Rimfolk", "World home: an encounter of the region's hard times now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Encounter()
        {
            var map = Map;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var d in DefDatabase<EncounterDef>.AllDefsListForReading.Where(d => !d.situations.NullOrEmpty()))
            {
                var def = d;
                options.Add(new FloatMenuOption(def.label + (WorldSeason.Fits(def.situations) ? "" : " (its world event isn't running)"), () =>
                {
                    bool ok = comp.encounters.TryOne(comp, map, Strangers.Present(map), x => x == def);
                    Say(ok ? def.label + "." : def.label + ": nobody fits now (a " + def.stranger.kind + " with a colonist" + (def.scene.NullOrEmpty() ? "" : ", the " + def.scene + " scene") + ").");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "World home: a council question the world brings home now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Question()
        {
            var map = Map;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            if (tracker == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var d in DefDatabase<DilemmaDef>.AllDefsListForReading.Where(d => !d.situations.NullOrEmpty()))
            {
                var def = d;
                options.Add(new FloatMenuOption(def.label + (WorldSeason.Fits(def.situations) ? "" : " (its world event isn't running)"), () =>
                    Say(tracker.TryFire(true, x => x == def) ? def.label + " asked." : def.label + ": nobody fits now, or a question waits.")));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "World home: a great world event taken for a god's doing now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Great()
        {
            var s = WorldSeason.Running.FirstOrDefault(x => WorldHomeMath.GreatGods(x.def.defName).Length > 0);
            if (s == null)
            {
                Say("no great world event is running (" + string.Join(", ", WorldHomeMath.Great) + ").");
                return;
            }
            s.Set(GreatTaken.Flag);
            Say(GreatTaken.Take(s, Map) ?? "nobody here holds a god they would take " + s.Label + " for.");
        }

        [DebugAction("Rimfolk", "World home: the next vigil day now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Vigil()
        {
            var v = WorldHomeBook.Instance?.vows.FirstOrDefault(x => x?.pawn != null && x.pawn.Spawned);
            if (v == null)
            {
                Say("nobody has vowed a vigil day (the holy year's question).");
                return;
            }
            bool going = WorldQuestions.VigilDay(v);
            if (going)
            {
                v.kept++;
                v.nextDay = WorldHomeMath.NextVigilDay(Find.TickManager.TicksGame / GenDate.TicksPerDay);
            }
            Say(going ? v.pawn.LabelShort + " keeps a vigil day now." : "the vigil couldn't be set going now (nobody free, or answers don't play out).");
        }

        [DebugAction("Rimfolk", "World home: the mourning colours put back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Undye()
        {
            var book = WorldHomeBook.Instance;
            if (book == null) return;
            int n = book.dyed.Count;
            foreach (var d in book.dyed.ToList()) WorldQuestions.Undye(d);
            book.dyed.Clear();
            Say(n + " put back.");
        }
    }
}
