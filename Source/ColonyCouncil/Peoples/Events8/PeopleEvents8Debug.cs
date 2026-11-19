using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 8 ("Rimfolk", "Peoples' events 8: ..."). What the record keeps; one of their camps here now
    /// (the ration post, the landing party, the field lab), whatever its own trigger; a clerk to observe the vote now; a medal for a deed
    /// now; the ration post's clerk now; the Fleet's call about the gravship now; a deck-runner for a sleeper now; the days at the hull
    /// done now; a camp of theirs turns now. The drawn events are fired with the game's own incident tool (Rimfolk_Evt_RepublicFugitive
    /// and the rest).
    /// </summary>
    public static class PeopleEvents8Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 8: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 8: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents8Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 8: ");
            sb.AppendLine($"last observer {b.lastObserver}, medal {b.lastMedal}, ration post {b.lastRation}; elections rolled {string.Join(", ", b.elections)}; deeds rolled {string.Join(", ", b.medalDeeds)}; "
                          + $"famines rolled {string.Join(", ", b.famines)}; Fleets ordered to disembark {string.Join(", ", b.disembarked)}.");
            foreach (var r in b.sleepers)
                sb.AppendLine($"  sleeper {r.sleeper?.LabelShort ?? "?"} of {r.faction?.Name ?? "?"} in {r.casket?.LabelShort ?? "?"}{(r.done ? ", done" : "")}.");
            var kinds = new[] { VoteObserver.Kind, RepublicMedal.Kind, GravshipLog.Kind, SleeperOfTheirs.Kind, GeneTakenOut.Kind, WholeXenogerm.Kind };
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && (kinds.Contains(v.kind) || v.kind == SeasonAway.Kind && SeasonAway.KeyOf(v) == OrderToDisembark.AwayKey)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, {v.a?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var e = VoteObserver.Open(map);
                var crew = PeoplesD.Friendly(PeoplesMath.Crew);
                var works = PeoplesD.Friendly(PeoplesMath.Editors);
                RationPost.Famine(out string famine, out _, out _);
                sb.AppendLine($"  here: election {(e == null ? "none" : e.phase + ", polls " + e.pollTick + ", count " + e.countTick)}; observer watching {VoteObserver.Watching(map)}; "
                              + $"fresh deed {(RepublicMedal.Fresh(map) is Deed d ? d.def.defName + " by " + d.doerName : "none")}; famine {famine ?? "none"}; "
                              + $"gravship {GravshipLog.ShipName(GravshipLog.Engine(map))} (part {GravshipLog.Part(map, out int n)?.defName ?? "-"} x{n}); "
                              + $"casket {SleeperOfTheirs.Casket(map)?.LabelShort ?? "none"}; ranked {OrderToDisembark.Ranked(map)?.LabelShort ?? "none"}, "
                              + $"hull {(crew != null ? OrderToDisembark.Hull(crew, map)?.Label ?? "none" : "no Fleet")}; gene out {GeneTakenOut.Takers(map).Count()}; "
                              + $"xenogerm takers {WholeXenogerm.Takers(map).Count()}, kinds {(works != null ? string.Join(", ", WholeXenogerm.Kinds(works).Select(x => x.defName)) : "no Works")}; "
                              + $"hybrids {string.Join(", ", HybridBeast.Kinds(map).Select(k => k.defName))}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 8: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_RationPost", "Rimfolk_Camp_LandingParty", "Rimfolk_Camp_FieldLab" })
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
                    Camp c = n == "Rimfolk_Camp_RationPost" ? RationPost.Start(ctx, 0) : n == "Rimfolk_Camp_LandingParty" ? LandingParty.Start(ctx, false) : FieldLab.Start(ctx);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 8: a clerk to observe the vote now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugObserver() => Say(VoteObserver.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 8: a medal for a deed now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMedal() => Say(RepublicMedal.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 8: the ration post's clerk now (in a famine)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRation() => Say(RationPost.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 8: the Fleet calls the colony's gravship now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGravship() => Say(GravshipLog.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 8: a deck-runner for a sleeper now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSleeper() => Say(SleeperOfTheirs.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 8: the days at the hull done now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugHull() => Say(SeasonAway.CutShortNow() ? "the days away are up (the colonist at the hull, or another season away)." : "nobody away at a place now.");

        [DebugAction("Rimfolk", "Peoples' events 8: a camp of theirs turns now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTurn()
        {
            var map = Find.CurrentMap;
            var post = RationPost.On(map);
            var party = LandingParty.On(map);
            var lab = FieldLab.On(map);
            var options = new List<FloatMenuOption>();
            if (RationPost.StateOf(post) is RationState rs)
            {
                options.Add(new FloatMenuOption("ration post: the rations run out (the queue at its chance)", () =>
                {
                    rs.rations = 0;
                    rs.riotRolled = false;
                    Say("the post's rations are gone; the next of the hungry finds none.");
                }));
                options.Add(new FloatMenuOption("ration post: one the Republic is at war with comes", () =>
                {
                    rs.turnedRolled = false;
                    rs.nextHungry = 0;
                    Say("the next of the hungry may be one the guards turn away (when such a people is at peace with the colony).");
                }));
            }
            if (LandingParty.StateOf(party) is PartyState ls)
                options.Add(new FloatMenuOption("landing party: the sleeper they brought falls ill (at its chance)", () =>
                {
                    ls.sickRolled = false;
                    LandingParty.Sick(party, ls);
                    Say(ls.sick ? "the sleeper fell ill." : "nothing came of it (the harm budget, or the chance).");
                }));
            if (FieldLab.StateOf(lab) is LabState lst)
            {
                options.Add(new FloatMenuOption("field lab: their beast gets loose (at its chance)", () =>
                {
                    lst.looseRolled = false;
                    FieldLab.Loose(lab, lst);
                    Say(lst.loose ? "the beast is loose." : "nothing came of it (no beast, the harm budget, or the chance).");
                }));
                options.Add(new FloatMenuOption("field lab: a vat-grown falls ill (at its chance)", () =>
                {
                    lst.sickRolled = false;
                    FieldLab.Sick(lab, lst);
                    Say(lst.sick ? "one of them fell ill." : "nothing came of it (the harm budget, or the chance).");
                }));
            }
            if (options.Count == 0)
            {
                Say("none of their camps here.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
