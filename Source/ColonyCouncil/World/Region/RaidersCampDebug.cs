using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of a raiders' camp at the home on screen (Debug actions, "Rimfolk"): set one up and have
    /// it seen at once, send its raid now, bring its terms now, or end it one of the five ways. The rules (wealth, rate,
    /// rest, the people's terms) are skipped; what follows plays by them.
    /// </summary>
    public static partial class RaidersCamps
    {
        private static Map DebugHome => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static void Say(string text) => Messages.Message("Raiders' camp: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Raiders' camp: set one up, seen now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSetUp()
        {
            var home = DebugHome;
            var rh = Of(home);
            if (rh == null) return;
            if (rh.camp != null)
            {
                Say("this home has one already (" + rh.camp.label + ").");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(CanOwn))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name + " (" + KindWord(people) + ")", () =>
                {
                    var c = SetUp(home, people);
                    if (c == null)
                    {
                        Say("no tile 6 to 10 tiles off can hold it.");
                        return;
                    }
                    var spotter = IncidentWorker_Happening.Colonists(home).FirstOrDefault(p => !p.Downed && !p.DevelopmentalStage.Juvenile());
                    if (spotter == null)
                    {
                        Say("set up, hidden: nobody to see it yet.");
                        return;
                    }
                    Found(home, c, spotter, "CC_RaidCamp_Doing_About".Translate().Resolve(), Now);
                    if (!c.Shown) Say("set up, but its site couldn't be made.");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no hostile people can hold a camp)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Raiders' camp: its raid now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRaid()
        {
            var home = DebugHome;
            var c = CampOf(home);
            if (c == null || c.stage != CampStage.Found)
            {
                Say("no camp found near this home.");
                return;
            }
            int before = c.raids;
            c.raidOpen = false;
            TryRaid(home, c, Now);
            if (c.raids == before) Say("no raid: a threat is on the map, a big one came within a day, or the game wouldn't send it.");
        }

        [DebugAction("Rimfolk", "Raiders' camp: their terms now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTerms()
        {
            var home = DebugHome;
            var c = CampOf(home);
            if (c == null || c.stage != CampStage.Found)
            {
                Say("no camp found near this home.");
                return;
            }
            RaidersCampTerms.Offer(home, c);
        }

        [DebugAction("Rimfolk", "Raiders' camp: end it...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnd()
        {
            var home = DebugHome;
            var rh = Of(home);
            var c = rh?.camp;
            if (c == null)
            {
                Say("no camp near this home.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var e in new[] { CampEnd.Broken, CampEnd.Paid, CampEnd.Peace, CampEnd.GaveUp, CampEnd.Gone })
            {
                var end = e;
                options.Add(new FloatMenuOption(end.ToString(), () => End(home, rh, c, end, null)));
            }
            var ally = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != null && !f.IsPlayer && !f.Hidden && f.def != null && f.def.humanlikeFaction
                                                                                      && f.PlayerRelationKind == FactionRelationKind.Ally);
            options.Add(new FloatMenuOption("Burned" + (ally != null ? " (by " + ally.Name + ")" : " (no ally)"), ally == null ? (System.Action)null : () => End(home, rh, c, CampEnd.Burned, ally)));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
