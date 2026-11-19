using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 7 ("Rimfolk", "Peoples' events 7: ..."). What the record keeps; one of their camps here
    /// now (the work that stays, the line crew, the picket), whatever its own trigger; a master to judge a fine piece now; a journeyman
    /// back now; the masts at friends' towns up now; a shift at the pit now; the organizer at dusk now (or the morning's petition); the
    /// strikers at the gate now (a strike called if none runs); a camp of theirs turns now. The drawn events are fired with the game's own
    /// incident tool (Rimfolk_Evt_VoiceForColony and the rest).
    /// </summary>
    public static class PeopleEvents7Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 7: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 7: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 7: ");
            sb.AppendLine($"last master {b.lastMark}, organizer {b.lastEvening}, picket {b.lastPicket}; strikes rolled for a picket {string.Join(", ", b.picketRolled)}.");
            foreach (var r in b.marks)
                sb.AppendLine($"  fine piece {r.thing?.LabelNoCount ?? "?"} by {r.maker?.LabelShort ?? "?"}: made {r.tick}, quality {r.quality}{(r.rolled ? ", rolled" : "")}.");
            foreach (var r in b.comeBacks)
                sb.AppendLine($"  journeyman {r.name ?? "?"} of {r.faction?.Name ?? "?"} (taught by {r.maker?.LabelShort ?? "?"}): due {r.due}{(r.done ? ", done" : "")}.");
            foreach (var r in b.masts)
                sb.AppendLine($"  mast at {r.townName ?? "?"} for {r.notableName ?? "?"}: due {r.due}{(r.raised ? ", up" : "")}{(r.done ? ", done" : "")}.");
            foreach (var r in b.shares)
                sb.AppendLine($"  well's share for {r.faction?.Name ?? "?"}: next {r.next}, {r.left} left.");
            foreach (var r in b.evenings)
                sb.AppendLine($"  evening of {r.faction?.Name ?? "?"}: {r.stage}, {(r.repeal ? "lift " : "")}{r.decree}, spoke {r.spoke}, asked {r.asked} by {r.asker?.LabelShort ?? "-"}.");
            var kinds = new[] { MastersMark.Kind, VoiceForColony.Kind, OilInGround.Kind };
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && (kinds.Contains(v.kind) || v.kind == SeasonAway.Kind && SeasonAway.KeyOf(v) == PitCaveIn.AwayKey)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var plan = WorkThatStays.Plan(map);
                var line = PeoplesD.Friendly(PeoplesMath.Linesmen);
                var union = PeoplesD.Friendly(PeoplesMath.Drillers);
                var oil = OilInGround.Oil(map, out var well);
                sb.AppendLine($"  here: the guild's work {(plan == null ? "none" : plan.work + " of " + (plan.material?.defName ?? "?") + " x" + plan.count + " at " + plan.site)}; "
                              + $"radio {VoiceForColony.Radio(map)?.LabelCap ?? "none"}; trader of goods {VoiceForColony.Trader(map, PeopleEvents7Math.CallGoods, out var g)?.Name ?? "none"} ({g?.defName ?? "-"}), "
                              + $"of arms {VoiceForColony.Trader(map, PeopleEvents7Math.CallArms, out var a)?.Name ?? "none"} ({a?.defName ?? "-"}); friend for a mast {(line != null ? TownMast.Friend(map, line)?.Name ?? "none" : "no Line")}; "
                              + $"high ground {PeopleEvents6.HighGround(map, out _)}; pit {PitCaveIn.Pit(union, map)?.Label ?? "none"}; oil {(oil.IsValid ? oil + " (" + well?.defName + ")" : "none")}; "
                              + $"worn down {OrganizersEvening.Worn(map)}, petition {OrganizersEvening.Petition(map, out bool repeal)?.defName ?? "none"}{(repeal ? " (lift)" : "")}; "
                              + $"letters relayed by a crew {LineCrew.Relays(map)}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 7: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_WorkThatStays", "Rimfolk_Camp_LineCrew", "Rimfolk_Camp_Picket" })
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
                    Camp c = null;
                    if (n == "Rimfolk_Camp_WorkThatStays") c = WorkThatStays.Start(ctx, WorkThatStays.Plan(map), null);
                    else if (n == "Rimfolk_Camp_LineCrew") c = LineCrew.Start(ctx);
                    else
                    {
                        if (UnionStrike.Of(f) == null) UnionStrike.Call(f, map, Find.TickManager.TicksGame);
                        c = Picket.Start(ctx);
                    }
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot or no work for it here (the work needs water to bridge, a hall or a gathering place, and the stone).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 7: a master to judge a fine piece now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMark() => Say(MastersMark.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 7: a journeyman back as a master now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugComeBack() => Say(ComeBackBetter.Soon(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 7: the masts at friends' towns up now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugMast() => Say(TownMast.Soon());

        [DebugAction("Rimfolk", "Peoples' events 7: a shift at the pit ends now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShift() => Say(PitCaveIn.ShiftNow());

        [DebugAction("Rimfolk", "Peoples' events 7: the organizer at dusk now (or the morning's petition)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEvening() => Say(OrganizersEvening.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 7: the strikers at the gate now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPicket() => Say(Picket.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 7: a camp of theirs turns now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTurn()
        {
            var map = Find.CurrentMap;
            var work = WorkThatStays.On(map);
            var crew = LineCrew.On(map);
            var picket = Picket.On(map);
            var options = new List<FloatMenuOption>();
            if (work != null)
            {
                options.Add(new FloatMenuOption("work: a block cracks, a quarter more stone wanted", () => Say(WorkThatStays.Turn(work, false))));
                options.Add(new FloatMenuOption("work: a day's work judged bad (at its chance)", () => Say(WorkThatStays.Turn(work, true))));
            }
            if (crew != null)
            {
                options.Add(new FloatMenuOption("line crew: a climb in the storm (the fall at its chance)", () => Say(LineCrew.Turn(crew, false))));
                options.Add(new FloatMenuOption("line crew: the lightning on the mast (at its chance)", () => Say(LineCrew.Turn(crew, true))));
            }
            if (Picket.StateOf(picket) is PicketState ps)
                options.Add(new FloatMenuOption("picket: strikebreakers (at its chance)", () =>
                {
                    ps.breakersRolled = false;
                    Picket.Breakers(picket, ps);
                    Say(ps.breakers ? "strikebreakers of " + (ps.enemy?.Name ?? "?") + " are coming for the picket." : "no band (no fights allowed now, or the dice said no).");
                }));
            if (options.Count == 0)
            {
                Say("none of their camps here.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
