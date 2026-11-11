using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 1 ("Rimfolk", "Peoples' events: ..."). What the record keeps; one of their
    /// camps here now (the hostel, the lantern post, the scholars at an old place), whatever its own trigger; the league's
    /// crates fetched now; the Mossborn's reckoning of the promise now. The events themselves are fired with the game's own
    /// incident tool (Rimfolk_Evt_HerdersBeast and the rest), each when what it needs is there.
    /// </summary>
    public static class PeopleEventsDebug
    {
        private static void Say(string text) => Messages.Message("Peoples' events: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEventsBook.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events: ");
            sb.AppendLine($"gifts {string.Join(", ", b.gifts)}; last gift tick {b.lastGift} (another may come: {PeopleEventsBook.GiftOpen}).");
            foreach (var t in b.trusts)
                sb.AppendLine($"  trust {t.id} of {t.faction?.Name ?? "?"}: {t.ids.Count} crates, due tick {t.due}, fee {t.fee}, fates {string.Join(", ", t.fates)}, {(t.done ? "done, " + t.ink : "open")}.");
            sb.AppendLine($"  good ink until {b.goodInkUntil}, red ink until {b.redInkUntil}; the Mossborn's promise since {b.promiseSince}, kept {b.promisesKept}, broken {b.promisesBroken}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var beast = LegendBeasts.OnLand(map, out string kind);
                sb.AppendLine($"  the great beast on the land: {beast?.LabelShort ?? "none"} ({kind ?? "-"}); old places unread: "
                              + string.Join(", ", OldPlaces.Unread(map).Select(p => p.key + (p.landmark ? " (" + p.name + ")" : ""))) + ".");
            }
            sb.Append("  read: " + string.Join(", ", b.read));
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_PilgrimHostel", "Rimfolk_Camp_LanternPost", "Rimfolk_Camp_ScholarsMeasure" })
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
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: " + (f == null ? "no people of that calling at peace" : string.Join("; ", why)));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events: the league's crates fetched now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFetch()
        {
            var b = PeopleEventsBook.Instance;
            var rec = b?.trusts?.FirstOrDefault(t => t != null && !t.done);
            if (rec == null)
            {
                Say("no crates held in trust.");
                return;
            }
            rec.due = Find.TickManager.TicksGame;
            Say("the fetch is due; the caravan comes within the hour.");
        }

        [DebugAction("Rimfolk", "Peoples' events: the Mossborn reckon the promise now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReckon()
        {
            if ((PeopleEventsBook.Instance?.promiseSince ?? -1) < 0)
            {
                Say("no promise made.");
                return;
            }
            MossPromise.Reckoned(MossLedger.Felled, MossLedger.Planted);
            Say($"reckoned: {MossLedger.Felled} felled, {MossLedger.Planted} planted this season.");
        }
    }
}
