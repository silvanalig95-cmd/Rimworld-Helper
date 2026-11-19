using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 4 ("Rimfolk", "Peoples' events 4: ..."). What the record keeps; one of their
    /// camps here now (the summer school, the flooded families, the boat-builders), whatever its own trigger; the contract
    /// due now; the broker's lander now; the knot night's lanterns now; the Wardens' page now; the Wardens to the manhunt now;
    /// the school's disputation now; the hull a stage on now. The events themselves are fired with the game's own incident
    /// tool (Rimfolk_Evt_FellowExamined and the rest).
    /// </summary>
    public static class PeopleEvents4Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 4: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 4: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 4: ");
            sb.AppendLine($"examination waiting {b.examPlay} ({b.examPawn?.LabelShort ?? "-"}, {(b.examPass ? "pass" : "fail")}); examined {b.examined.Count}; best students {b.schoolBest.Count}; "
                          + $"contract size {b.contractScale:0.0}, cooler until {b.coolUntil}; last manhunt {b.lastManhunt}, last page {b.lastPage}; lanterns {string.Join(", ", b.lanternYears)}.");
            foreach (var r in b.fellows)
                sb.AppendLine($"  fellow {r.pawn?.LabelShort ?? "?"} of {r.faction?.Name ?? "?"} since {r.since}, next book {r.nextBook}, books {r.books}.");
            foreach (var r in b.contracts)
                sb.AppendLine($"  contract {r.id} with {r.faction?.Name ?? "?"}: {r.count} {r.def} ({r.kind}) at {r.unit:0.0} for {r.price}, due {r.due}, {r.state}{(r.done ? ", done" : "")}.");
            foreach (var r in b.pods)
                sb.AppendLine($"  pod {r.id} of {r.faction?.Name ?? "?"}: broker {r.broker?.LabelShort ?? "?"}, {BrokerPod.Present(r).Count}/{r.crates} crates worth {r.worth}, {r.state}, lander {r.due}{(r.done ? ", done" : "")}.");
            foreach (var r in b.fouled)
                sb.AppendLine($"  fouled water of {r.faction?.Name ?? "?"}: {r.cells.Count} spots, until {r.until}{(r.done ? ", done" : "")}.");
            foreach (var r in b.pages)
                sb.AppendLine($"  page {r.id}: {r.killer?.LabelShort ?? "?"} brought down {r.beast} ({r.kind}), hide {r.leather} x{r.hide:0}, {(r.ranged ? "shot" : "close")}, at {r.tick}{(r.done ? ", done" : "")}.");
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind != null && !v.done && new[] { BrokerPod.LanderKind, FouledWater.CleanKind, KnotLanterns.Kind, WardensManhunt.Kind, HiveBurned.Kind }.Contains(v.kind)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
                sb.AppendLine($"  here: examinee {Fellowship.Candidate(map)?.LabelShort ?? "none"}; books {SummerSchool.Books(map).Count}; contract kinds "
                              + $"{string.Join(", ", PeopleEvents4Math.ContractKinds.Where(k => EnclaveContract.Makes(map, k, out _, out _, out _)))}; fouled spots {FouledWater.Spots(map).Count}; "
                              + $"knot night tonight {KnotLanterns.Tonight(map)?.Name ?? "none"}, the colony's dead {KnotLanterns.Lost(map).Count}; flood {FloodedOut.Flood() ?? "none"}; wild trees {BoatBuilders.WildTrees(map)}, "
                              + $"dinghy {(BoatBuilders.Dinghy != null ? "yes" : "no")}; manhunters {WardensManhunt.Manhunters(map).Count}; hives {HiveBurned.Hives(map).Count}.");
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 4: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_SummerSchool", "Rimfolk_Camp_FloodedOut", "Rimfolk_Camp_BoatBuilders" })
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
                    var ctx = new HappeningContext { map = map, faction = f };
                    var c = n == "Rimfolk_Camp_SummerSchool" ? SummerSchool.Start(ctx, false) : n == "Rimfolk_Camp_FloodedOut" ? FloodedOut.Start(ctx, false) : BoatBuilders.Start(ctx, true);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot or water for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 4: the enclave's contract due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugContract() => Say(EnclaveContract.DueNow() ? "their lander comes within the hour (when a question may be asked)." : "no contract open.");

        [DebugAction("Rimfolk", "Peoples' events 4: the broker's lander now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLander() => Say(BrokerPod.LanderNow() ? "the lander comes within the hour." : "no pod answered and waiting.");

        [DebugAction("Rimfolk", "Peoples' events 4: the knot night's lanterns now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLanterns()
        {
            KnotLanterns.force = true;
            Say(KnotLanterns.Tonight(Find.CurrentMap) != null ? "the boatmen come within the hour (when a question may be asked)." : "no river folk at peace, no bank, or their lanterns came this year.");
        }

        [DebugAction("Rimfolk", "Peoples' events 4: the Wardens' page waits no longer", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPage() =>
            Say(WardensPage.Soon(Find.CurrentMap) ? "a Warden may come each hour from now (at the page's chance), the hunter at home." : "no great beast brought down here lately.");

        [DebugAction("Rimfolk", "Peoples' events 4: the Wardens to the manhunt now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugManhunt()
        {
            var map = Find.CurrentMap;
            if (!WardensManhunt.Beset(map))
            {
                Say("no manhunting pack or mad beast of a size here.");
                return;
            }
            WardensManhunt.Soon(map);
            Say("the Wardens come within the hour, if they are at peace.");
        }

        [DebugAction("Rimfolk", "Peoples' events 4: the school's disputation now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDispute()
        {
            var c = SummerSchool.On(Find.CurrentMap);
            var st = SummerSchool.StateOf(c);
            if (st == null)
            {
                Say("no summer school here.");
                return;
            }
            st.disputeRolled = true;
            st.disputeSince = Find.TickManager.TicksGame;
            Say("two fellows are at it at their table; end it from the camp's page, or it ends in a sulk.");
        }

        [DebugAction("Rimfolk", "Peoples' events 4: the hull a stage on now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHull()
        {
            var c = BoatBuilders.On(Find.CurrentMap);
            var st = BoatBuilders.StateOf(c);
            if (st == null)
            {
                Say("no boat-builders here.");
                return;
            }
            st.lost -= 2f;
            Say("the hull goes a stage on within the hour.");
        }
    }
}
