using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of a hamlet at the home on screen (Debug actions, "Rimfolk"): settlers ask now (from a
    /// people of your choosing, or the holding), the hamlet founded now (the settlers on their way, or new ones), its hard
    /// season's ask now, its ask to swear fealty now, and its people's war with the colony asked about now. The rules of who
    /// and when (the day, the colonists, the year's gap, the look's chance) are skipped; where it stands and what follows
    /// play by them.
    /// </summary>
    public static partial class Hamlets
    {
        private static Map DebugHome => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static void Say(string text) => Messages.Message("Hamlet: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Hamlet: settlers ask now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAsk()
        {
            var home = DebugHome;
            var h = Of(home);
            if (h == null) return;
            if (HamletMath.Busy(h.stage))
            {
                Say("this home's hamlet is under way already (" + h.stage + ").");
                return;
            }
            var options = new List<FloatMenuOption>();
            var holding = CampTracker.HoldingAt(home);
            if (holding != null && CanFound(holding.faction))
                options.Add(new FloatMenuOption("The holding (" + holding.HoldingName + ")", () =>
                {
                    if (!AskFromHolding(home, h, holding, Now)) Say("no tile 6 to 10 tiles off can hold it.");
                }));
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(CanFound))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name + " (" + HamletWords.Word(people) + ")", () =>
                {
                    var from = NearestTownOf(people, home);
                    if (from == null)
                    {
                        Say(people.Name + " have no town to send settlers from.");
                        return;
                    }
                    if (!AskFromTown(home, h, from, Now)) Say("no tile 6 to 10 tiles off can hold it.");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people at peace could send settlers)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Hamlet: found it now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFound()
        {
            var home = DebugHome;
            var h = Of(home);
            if (h == null) return;
            if (HamletMath.Stands(h.stage))
            {
                Say("a hamlet stands already: " + h.name + ".");
                return;
            }
            if (!HamletMath.OnTheirWay(h.stage))
            {
                var people = Find.FactionManager.AllFactionsListForReading.Where(CanFound).Select(f => (f, NearestTownOf(f, home))).FirstOrDefault(x => x.Item2 != null);
                if (people.f == null || !AskFromTown(home, h, people.Item2, Now))
                {
                    Say("no people at peace with a town, or no tile 6 to 10 tiles off.");
                    return;
                }
            }
            // Off the map first, as if they had gone with a caravan.
            OffTheMap(home, h, CampOf(h));
            var town = Found(home, h, true);
            if (town == null) Say("it could not stand.");
        }

        /// <summary>The home's hamlet standing, for the actions on its life.</summary>
        private static void DebugStanding(System.Action<Map, HamletHome> act)
        {
            var home = DebugHome;
            var h = Of(home);
            if (h == null || !HamletMath.Stands(h.stage) || TownOf(h) == null)
            {
                Say("no hamlet stands near this home.");
                return;
            }
            act(home, h);
        }

        [DebugAction("Rimfolk", "Hamlet: a hard season's ask now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHelp() => DebugStanding((home, h) => AskHelp(home, h, Now));

        [DebugAction("Rimfolk", "Hamlet: fealty asked now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOath() => DebugStanding((home, h) =>
        {
            if (h.stage != HamletStage.Standing)
            {
                Say("it has sworn already.");
                return;
            }
            h.oathAt = Now;
            HamletAsk.AskOath(home, h);
        });

        [DebugAction("Rimfolk", "Hamlet: the harvest's carter now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHarvest() => DebugStanding((home, h) =>
        {
            var rec = WorldLedger.TownRecord(h.town);
            Harvest(home, h, TownOf(h), rec != null && rec.Seeded ? rec.prosperity : HamletMath.Start, Now);
        });

        [DebugAction("Rimfolk", "Hamlet: its people's war, asked now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHostile() => DebugStanding((home, h) =>
        {
            if (h.people == null || !h.people.HostileTo(Faction.OfPlayer))
            {
                Say((h.people?.Name ?? "their people") + " are at peace with the colony: make them hostile first (the game's own goodwill action), and the question comes now or in the hamlet's day.");
                return;
            }
            h.hostileAsked = true;
            h.oathAt = Now;
            HamletAsk.AskHostile(home, h);
        });
    }
}
