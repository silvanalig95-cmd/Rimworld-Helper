using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force the follow-ups at the home on screen (Debug actions, "Rimfolk"): the warlord's son by any of his
    /// father's endings (of a people chosen, his father known only by name), a town near the home grown now, or every
    /// follow-up waiting due now. The window and the free slot are skipped; what follows plays by the rules.
    /// </summary>
    public static class FollowUpsDebug
    {
        private static Map DebugHome => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Follow-ups: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Follow-ups: the warlord's son now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSon()
        {
            var home = DebugHome;
            var options = new List<FloatMenuOption>();
            foreach (var ending in new[] { "slain", "captured", "ransomed", "hunted", "brokeup" })
            {
                string key = ending;
                options.Add(new FloatMenuOption(key + " (" + FollowUpsMath.Branch(key) + ")", () => People(home, key)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void People(Map home, string ending)
        {
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => FactionMemory.Remembers(f) && !f.defeated))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name + (people.HostileTo(Faction.OfPlayer) ? " (hostile)" : ""), () =>
                {
                    var fu = new FollowUp
                    {
                        kind = "son", people = people, ending = ending, endTick = Now, due = Now, whoName = "the old warlord", whoGender = Gender.Male, whoAge = 50f,
                        tale = "the Gray Host"
                    };
                    var s = WarlordSons.Start(home, fu);
                    Say(s != null ? "started " + s.def.defName + "." : "it couldn't start (another of his stories runs, or nobody to ride with).");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Follow-ups: a town grows now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTown()
        {
            var home = DebugHome;
            var options = new List<FloatMenuOption>();
            foreach (var t in Find.WorldObjects.Settlements.Where(t => t != null && !t.Destroyed && t.HasName && t.Faction != null && !t.Faction.IsPlayer
                                                                       && !t.Faction.HostileTo(Faction.OfPlayer) && t.Tile.Layer == home.Tile.Layer)
                         .OrderBy(t => Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t.Tile)).Take(12))
            {
                var town = t;
                options.Add(new FloatMenuOption(town.Name + " (" + town.Faction.Name + ")", () =>
                {
                    var fu = new FollowUp
                    {
                        kind = "town", people = town.Faction, ending = "neighbours", endTick = Now, due = Now, thing = town.ID, tale = "the new steading",
                        flags = new List<string> { "fed", "gift" }
                    };
                    var s = GrownTowns.Start(home, fu);
                    Say(s != null ? "started at " + town.Name + "." : "it couldn't start (a town's world event runs already).");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no town at peace near)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Follow-ups: all waiting due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDue()
        {
            int n = 0;
            foreach (var home in Homes.All)
            {
                var book = FollowUps.Of(home);
                if (book == null) continue;
                foreach (var f in book.waiting)
                {
                    f.due = Now;
                    n++;
                }
            }
            Say(n + " due now; they start in the region's day, one a day, when a world event's slot is free.");
        }
    }
}
