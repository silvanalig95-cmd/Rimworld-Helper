using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 5 ("Rimfolk", "Peoples' events 5: ..."). What the record keeps; one of their
    /// camps here now (the field of the fallen, the Circuit's ring, the Wheel's yard), whatever its own trigger; the
    /// gravewardens to the risen now; the mourners due now; the night crew now; the crater egg hatches (or its tamer is due)
    /// now; the fixer back from the cart now; a Wheel yard due here now; the field of the fallen turns now; the ring's bout
    /// now. The drawn events are fired with the game's own incident tool (Rimfolk_Evt_GraveGarden and the rest).
    /// </summary>
    public static class PeopleEvents5Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 5: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 5: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 5: ");
            sb.AppendLine($"promise to the Bell {b.promise} ({b.promiseTo?.Name ?? "-"}{(b.promiseBroken ? ", broken" : "")}); last gravewardens {b.lastStir}, last night crew {b.lastMend}; "
                          + $"raids rolled {string.Join(", ", b.mendRaids)}; pieces at the cart {b.held?.Count ?? 0}.");
            foreach (var r in b.stirs)
                sb.AppendLine($"  burial of the risen by {r.faction?.Name ?? "?"}: {r.how}, {r.dead.Count} dead, {r.plots.Count} graves, {r.rung} rung{(r.done ? ", done" : "")}.");
            foreach (var r in b.mourns)
                sb.AppendLine($"  mourners of {r.people?.Name ?? "?"} (by {r.bell?.Name ?? "?"}): {r.buried} buried, due {r.due}{(r.done ? ", done" : "")}.");
            foreach (var r in b.gardens)
                sb.AppendLine($"  grave-garden {(r.Stands ? "standing" : "not standing")} at {r.postCell}, radius {r.radius:0}, {r.stones.Count} stones and {r.roses.Count} roses left, {r.laid} laid, {r.planted} planted, {(r.whole ? "whole" : "post only")}.");
            foreach (var r in b.eggs)
                sb.AppendLine($"  egg {r.id} ({r.def} of {r.race}): keeper {r.keeper?.LabelShort ?? "-"}, tended by {string.Join(", ", r.tenders.Select((p, i) => (p?.LabelShort ?? "?") + " " + (i < r.hours.Count ? r.hours[i] : 0)))}; "
                              + $"hatched {r.hatched}, beast {r.beast?.LabelShort ?? "-"}, back {r.back}{(r.lost ? ", lost" : "")}{(r.done ? ", done" : "")}.");
            foreach (var r in b.betters)
                sb.AppendLine($"  piece {r.id}: {r.item?.LabelShort ?? "?"} of {r.owner?.LabelShort ?? "?"}, fixer {r.fixer?.LabelShort ?? "?"}, {r.stage}, due {r.due}{(r.done ? ", done" : "")}.");
            foreach (var r in b.yards)
                sb.AppendLine($"  yard of {r.town ?? "?"} ({r.faction?.Name ?? "?"}): due {r.due}{(r.done ? ", done" : "")}.");
            var kinds = new[] { FieldStirs.Kind, OtherSideMourners.Kind, GraveGarden.Kind, MendTonight.Kind, WagonOfOurOwn.Kind };
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && kinds.Contains(v.kind)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var hand = BondGiven.Hand(map);
                sb.AppendLine($"  here: shamblers {FieldStirs.Shamblers(map).Count}, risen of the colony's dead {FieldStirs.Risen(map).Count}; graves together {GraveGarden.Cluster(map, out _).Count}; "
                              + $"dead lying {FieldOfFallen.Dead(map).Count}; hand {hand?.LabelShort ?? "none"}, beast {BondGiven.Beast(map, hand)?.LabelShort ?? "none"}; "
                              + $"crater egg {CraterEgg.Pick(map)?.defName ?? "none"}; fair {CircuitRing.Fair() ?? "none"}; damaged {MendTonight.Damaged(map).Count}, raiders' gear {MendTonight.Gear(map).Count}; "
                              + $"piece owner {MadeBetter.Owner(map, out var piece)?.LabelShort ?? "none"} ({piece?.LabelShort ?? "-"}); vehicle {WagonOfOurOwn.Vehicle(PeopleEvents2.ColonyAge)?.defName ?? "none"}; "
                              + $"worn things {WheelStopsBy.Worn(map).Count}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 5: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_FieldOfFallen", "Rimfolk_Camp_CircuitRing", "Rimfolk_Camp_WheelYard" })
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
                    var c = n == "Rimfolk_Camp_FieldOfFallen" ? FieldOfFallen.Start(ctx, false) : n == "Rimfolk_Camp_CircuitRing" ? CircuitRing.Start(ctx) : WheelStopsBy.Start(ctx);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 5: the gravewardens to the risen now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStirs()
        {
            var map = Find.CurrentMap;
            if (FieldStirs.Shamblers(map).Count == 0)
            {
                Say("no shamblers on their feet here (Anomaly).");
                return;
            }
            FieldStirs.Soon(map);
            Say("the gravewardens come within the hour, if the Bell is at peace (the shamblers here counted as the colony's risen dead).");
        }

        [DebugAction("Rimfolk", "Peoples' events 5: the mourners due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMourners() =>
            Say(OtherSideMourners.Soon(Find.CurrentMap) ? "the mourners come within the hour (when a question may be asked)." : "no graves here holding a people that may come, or the Bell is not at peace.");

        [DebugAction("Rimfolk", "Peoples' events 5: the night crew now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMend() => Say(MendTonight.Now_(Find.CurrentMap) ? "the Wheel's crew is at the gate." : "no Wheel at peace, or fewer than three damaged buildings.");

        [DebugAction("Rimfolk", "Peoples' events 5: the crater egg hatches, or its tamer is due, now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugEgg() => Say(CraterEgg.Hasten());

        [DebugAction("Rimfolk", "Peoples' events 5: the fixer back from the cart now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugBetter() => Say(MadeBetter.Hasten() ? "the fixer comes back within the hour." : "no piece at the Wheel's cart.");

        [DebugAction("Rimfolk", "Peoples' events 5: a Wheel yard due here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugYard() => Say(WheelStopsBy.Soon(Find.CurrentMap) ? "the yard's fixer comes within the hour (when a question may be asked)." : "no Wheel at peace.");

        [DebugAction("Rimfolk", "Peoples' events 5: the field of the fallen turns now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFallen()
        {
            var c = FieldOfFallen.On(Find.CurrentMap);
            var st = FieldOfFallen.StateOf(c);
            if (st == null)
            {
                Say("no field of the fallen here.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("the field stirs (tonight's roll, Anomaly)", () =>
                {
                    st.stirRolled = false;
                    Say("rolled tonight, at its chance, with the void awake and fights allowed.");
                }),
                new FloatMenuOption("the dead's people come for them (at its chance)", () =>
                {
                    st.claimRolled = false;
                    Say("rolled within the day's hours, at its chance.");
                }),
                new FloatMenuOption("the mourners come", () =>
                {
                    FieldOfFallen.Mourners(c, st);
                    Say("the mourners of the dead buried here walk in.");
                })
            }));
        }

        [DebugAction("Rimfolk", "Peoples' events 5: the ring's bout now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBout()
        {
            var c = CircuitRing.On(Find.CurrentMap);
            var st = CircuitRing.StateOf(c);
            if (st == null)
            {
                Say("no Circuit's ring here.");
                return;
            }
            CircuitRing.Afternoon(c, st);
            Say(st.trainer != null ? st.trainer.LabelShort + " steps up to " + (st.mon?.LabelShort ?? "?") + "." : "no bout now (no companion, or nobody waiting and the tamers resting).");
        }
    }
}
