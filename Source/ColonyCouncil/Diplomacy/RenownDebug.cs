using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: house renown (Debug actions, "Rimfolk"): the day's look now (the colony's houses kept, each band read
    /// and a move told), every house's renown in the log with the parts it came from, a house's band told one step up or
    /// down now (its people's word on the map and the news), and a deed written for one of the colony (a contest won at a
    /// feast) so its house's renown can be watched moving. Where it goes from there plays by the rules.
    /// </summary>
    public static class RenownDebug
    {
        private static void Say(string text) => Messages.Message("House renown: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Renown: the day's look now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDay()
        {
            Renown.Daily(Find.TickManager.TicksGame);
            Say(WorldLedger.OurHouses().Count + " of the colony's houses kept.");
        }

        [DebugAction("Rimfolk", "Renown: every house's renown to the log", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            Renown.Dirty();
            var lines = new List<string>();
            foreach (var h in WorldLedger.Instance?.Houses ?? new List<LedgerHouse>())
            {
                var r = Renown.Read(h);
                if (r == null) continue;
                lines.Add($"{RenownWords.Name(h)}{(h.ours ? " (ours)" : "")}: {r.score:0.0}, {r.band} (told {(RenownBand)h.band}), known for {r.known}; "
                          + string.Join(", ", r.parts.Select(p => $"{p.Key} {p.Value:0.0}")));
            }
            Log.Message("[Rimfolk] House renown:\n" + (lines.Count == 0 ? "(no houses)" : string.Join("\n", lines)));
            Say(lines.Count + " houses written to the log.");
        }

        [DebugAction("Rimfolk", "Renown: a house's band told a step up or down now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMove()
        {
            var options = new List<FloatMenuOption>();
            foreach (var h in WorldLedger.OurHouses())
            {
                var house = h;
                options.Add(new FloatMenuOption(RenownWords.Name(h) + ": up", () => Renown.TellStep(house, true)));
                options.Add(new FloatMenuOption(RenownWords.Name(h) + ": down", () => Renown.TellStep(house, false)));
            }
            Menu(options, "(no houses of the colony's yet: two of one name who are family)");
        }

        [DebugAction("Rimfolk", "Renown: a contest won at a feast, for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugContest()
        {
            var home = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            Menu(WorldLedger.OurHouses().SelectMany(Renown.Living).Distinct().Select(p => new FloatMenuOption(p.LabelShort, () =>
            {
                Diplomacy.Write(DiplomacyMath.Contest, new List<HistoryActor> { new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did) },
                    Diplomacy.Colony(home), p.LabelShort + " won a contest at a feast (dev).");
                Renown.Dirty();
                Say(p.LabelShort + " won a contest; their house's renown reads it now.");
            })).ToList(), "(nobody of a house of the colony's)");
        }
    }
}
