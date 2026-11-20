using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode for the region's years and the camps (docs/content-update-proposals.md, B5, B11, B13, B19, B27; Debug actions,
    /// "Rimfolk"): what moves the traders' prices now, who the colony has taken in, and a call to arms' wounded come now. The
    /// hamlet's region asks are in World/Region/HamletDebug.cs, the raiders' camp's signs in World/Region/RaidersCampSigns.cs,
    /// and the calls to arms themselves in Diplomacy/PactsDebug.cs, FealtyDebug.cs and DiplomacyDebug.cs.
    /// </summary>
    public static class RegionYearsDebug
    {
        private static void Say(string text) => Messages.Message(text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Region's years: what moves the traders' prices now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Prices() => Say("Traders' prices: " + RegionPrices.Describe());

        [DebugAction("Rimfolk", "Taken in: who is in the colony's beds", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Patients() => Say("Taken in: " + TakenIn.Describe());

        [DebugAction("Rimfolk", "Taken in: the wounded on their way come now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ArriveNow()
        {
            int n = TakenIn.DevArriveNow();
            Say(n > 0 ? "Taken in: " + n + " party of wounded came in." : "Taken in: no wounded are on their way.");
        }

        [DebugAction("Rimfolk", "Taken in: a call to arms' wounded come now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WoundedNow()
        {
            var home = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            if (home == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.HostileTo(Faction.OfPlayer)
                                                                                       && Arrivals.PersonKind(f) != null))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name, () =>
                {
                    string town = Find.WorldObjects.Settlements.Where(s => s != null && s.Faction == people && s.Tile.Layer == home.Tile.Layer).OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, home.Tile))
                        .FirstOrDefault()?.Name;
                    TakenIn.Expect(people, home, CallsFromHomeMath.Wounded(Rand.Value), town);
                    TakenIn.DevArriveNow();
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people at peace with the colony)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>The camps whose beasts or goods B19 drew from the player's mods (the peoples' own camps are in "Peoples: one of their camps here now...").</summary>
        private static readonly string[] ModPoolCamps =
        {
            "Council_Camp_SaltCaravan", "Council_Camp_Market", "Council_Camp_Hermit", "Council_Camp_Drovers", "Rimfolk_Camp_HerbGatherers",
            "Rimfolk_Camp_SnowboundCaravan", "Rimfolk_Camp_Woodcutters"
        };

        [DebugAction("Rimfolk", "Camps: one with the mods' beasts and goods here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ModPoolCamp()
        {
            var map = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            var tracker = CampTracker.Instance;
            if (map == null || tracker == null || !CampTracker.Enabled)
            {
                Say("Camps are off.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var name in ModPoolCamps)
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(name);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.LabelCap, () =>
                {
                    var people = Arrivals.Friendly();
                    if (people == null)
                    {
                        Say("Camps: no people at peace with the colony.");
                        return;
                    }
                    var why = new List<string>();
                    var c = tracker.StartKind(map, kind, people, why);
                    Say(c != null ? "Camps: " + kind.label + " of " + people.Name + " on the way." : "Camps: no camp: " + string.Join("; ", why));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
