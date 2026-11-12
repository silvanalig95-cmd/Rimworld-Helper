using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 2 ("Rimfolk", "Peoples' events 2: ..."). What the record keeps; one of their
    /// camps here now (the herders by the water, the enclave's pad), whatever its own trigger; a season away's days there
    /// over now; the enclave's next letter due now; the archive's cart now; the river folk's knot night now. The events
    /// themselves are fired with the game's own incident tool (Rimfolk_Evt_Pilgrimage and the rest), each when what it needs
    /// is there.
    /// </summary>
    public static class PeopleEvents2Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 2: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 2: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents2Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 2: ");
            sb.AppendLine($"league's caravans half again until tick {b.guardUntil}; marks {string.Join(", ", b.marks)}.");
            foreach (var c in b.crossings)
                sb.AppendLine($"  crossing of {c.faction?.Name ?? "?"} ({c.bridge}, {c.cells.Count} cells, stands {RiverCrossing.Stands(c)}), knot day {(c.faction == null ? -1 : PeopleEvents2Math.KnotDay(c.faction.loadID))}, rested year {c.knotYear}.");
            foreach (var t in b.terms)
                sb.AppendLine($"  terms {t.id} with {t.faction?.Name ?? "?"} for {t.who}'s {t.part}: {t.total} in all, {t.down} down, {t.paid} paid, {t.owed} owed, due {t.due}, {(t.defaulted ? "defaulted, tie " + t.tie : "in good standing")}{(t.done ? ", done" : "")}.");
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind != null && v.kind.StartsWith("pev:") && !v.done).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stage {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var line = RiverCrossing.Line(map, out int water);
                sb.AppendLine($"  here: a crossing's line {(line == null ? "none" : line.Count + " cells, " + water + " on the water")}; drought {DryWater.Drought(map) ?? "none"}; "
                              + $"news the masts could sell: {string.Join(", ", NewsForSale.Kinds(map))}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 2: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_DryWater", "Rimfolk_Camp_EnclavePad" })
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label, () =>
                {
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    if (f == null || !CampTracker.Enabled)
                    {
                        Say("no camp: " + (f == null ? "no people of that calling at peace" : "camps are off"));
                        return;
                    }
                    var ctx = new HappeningContext { map = map, faction = f, count = PeopleEvents2Math.PadRent(1f, 7) };
                    var c = n == "Rimfolk_Camp_DryWater" ? DryWater.Start(ctx) : EnclavePad.Start(ctx, false);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 2: a season away's days there over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAwayHome() => Say(SeasonAway.CutShortNow() ? "their days are up; they set out for home within the hour." : "nobody is away at a place now.");

        [DebugAction("Rimfolk", "Peoples' events 2: the enclave's next letter due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTerms() => Say(EnclaveTerms.DueNow() ? "the letter comes within the hour, when a question may be asked." : "no terms open.");

        [DebugAction("Rimfolk", "Peoples' events 2: the archive's cart now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugArchive() => Say(ArchiveTrust.FetchNow() ? "the cart comes within the hour." : "no archive held in trust.");

        [DebugAction("Rimfolk", "Peoples' events 2: the river folk's knot night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugKnot()
        {
            var r = RiverCrossing.On(Find.CurrentMap);
            if (r?.faction == null)
            {
                Say("no crossing on this map.");
                return;
            }
            RiverCrossing.forceKnot = true;
            try
            {
                Say(PeopleEvents2.Fire("Rimfolk_Evt_KnotNight", r.map, r.faction) ? "their boatmen are at the gate." : "not now (another question may be waiting, or the crossing no longer stands).");
            }
            finally
            {
                RiverCrossing.forceKnot = false;
            }
        }
    }
}
