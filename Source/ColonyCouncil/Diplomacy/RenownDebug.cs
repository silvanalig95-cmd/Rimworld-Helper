using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the colony's renown (Debug actions, "Rimfolk"): the day's look now (its band read and a move told), its
    /// renown and each colonist's own name in the log with the parts they came from, its band told one step up or down now
    /// (a word on the map and the news), and a deed written for one of the colony (a contest won at a feast, its kind
    /// picked) so the colony's renown and their own name can be watched moving. Where it goes from there plays by the rules.
    /// </summary>
    public static class RenownDebug
    {
        private static void Say(string text) => Messages.Message("Colony renown: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Renown: the day's look now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDay()
        {
            Renown.Daily(Find.TickManager.TicksGame);
            Say("told " + RenownMath.Told(DiplomacyBook.Instance?.renownBand ?? RenownMath.NotRead) + ".");
        }

        [DebugAction("Rimfolk", "Renown: the colony's and its people's to the log", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            Renown.Dirty();
            var lines = new List<string>();
            var r = Renown.Read();
            if (r != null)
                lines.Add($"The colony: {r.score:0.0}, {r.band} (told {RenownMath.Told(DiplomacyBook.Instance?.renownBand ?? RenownMath.NotRead)}), known for {r.known}; "
                          + string.Join(", ", r.parts.Select(p => $"{p.Key} {p.Value:0.0}")));
            foreach (var p in Renown.People())
            {
                var o = Renown.Own(p);
                if (o?.reading == null || o.reading.parts.Count == 0) continue;
                lines.Add($"{p.LabelShort}: {o.reading.score:0.0}, {o.reading.band}, known for {o.reading.known}, as {o.part ?? "-"} ({RenownWords.KnownAs(o) ?? "not known by name"}); "
                          + string.Join(", ", o.reading.parts.Select(x => $"{x.Key} {x.Value:0.0}")));
            }
            Log.Message("[Rimfolk] Colony renown:\n" + (lines.Count == 0 ? "(off)" : string.Join("\n", lines)));
            Say(lines.Count + " lines written to the log.");
        }

        [DebugAction("Rimfolk", "Renown: the colony's band told a step up or down now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMove()
        {
            Menu(new List<FloatMenuOption>
            {
                new FloatMenuOption("up", () => Renown.TellStep(true)),
                new FloatMenuOption("down", () => Renown.TellStep(false))
            }, "");
        }

        [DebugAction("Rimfolk", "Renown: a contest won at a feast, for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugContest()
        {
            var home = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            var guests = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => f != null && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
            Menu(Renown.People().Where(p => p.Spawned).Select(p => new FloatMenuOption(p.LabelShort, () =>
            {
                var actors = new List<HistoryActor> { new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did) };
                if (guests != null) actors.Add(new HistoryActor(Refs.Of(guests), HistoryRole.With));
                Diplomacy.Write(DiplomacyMath.Contest, actors, Diplomacy.Colony(home), p.LabelShort + " won the wrestling at a feast (dev).", false, ContestKind.Wrestling.ToString());
                Renown.Dirty();
                Say(p.LabelShort + " won the wrestling; the colony's renown and their own name read it now.");
            })).ToList(), "(nobody of the colony on a map)");
        }
    }
}
