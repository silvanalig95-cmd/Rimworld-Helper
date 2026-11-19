using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 10 ("Rimfolk", "Peoples' events 10: ..."). What the record keeps and what each event would
    /// find here; the hearing at the table or the gate done now; the days at the plague town done now; the next morning and afternoon at
    /// the table now; a people remembers a colonist now (for their art). The events themselves are fired with the game's own incident tool
    /// (Rimfolk_Evt_WoodMoot and the rest).
    /// </summary>
    public static class PeopleEvents10Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 10: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Visit> Open(string kind) =>
            CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind == kind && !v.done) ?? Enumerable.Empty<Visit>();

        [DebugAction("Rimfolk", "Peoples' events 10: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents10Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 10: ");
            sb.AppendLine($"prices {string.Join(", ", b.prices.Select(p => $"{p.thing} x{p.factor:0.00} until {p.until} ({p.why})"))}; art handed on by {string.Join(", ", b.artGiven)}.");
            foreach (var kind in new[] { WoodMoot.Kind, RoadToll.Kind, LearningTraded.Kind, ArtHandedOn.Kind })
                foreach (var v in Open(kind))
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, {v.a?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            foreach (var v in CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && v.kind == SeasonAway.Kind && SeasonAway.KeyOf(v) == PlagueYear.AwayKey)
                              ?? Enumerable.Empty<Visit>())
                sb.AppendLine($"  at the plague town: {v.x?.LabelShort ?? "-"}, stage {v.stake}, {SeasonAway.PlaceOf(v)}, {SeasonAway.ExtraOf(v)}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var union = PeopleEvents10.Pair(PeoplesMath.Drillers, PeoplesMath.Grovekeepers, out var moss);
                var league = PeopleEvents10.Pair(PeoplesMath.Traders, PeoplesMath.Salvagers, out var wheel);
                var order = PeopleEvents10.Pair(PeoplesMath.Healers, PeoplesMath.Sextons, out var bell);
                var society = PeopleEvents10.Pair(PeoplesMath.Scholars, PeoplesMath.Vaultfolk, out var vault);
                var doctor = PlagueYear.Doctor(map);
                sb.AppendLine($"  here: the moot {(union != null ? union.Name + " and " + moss.Name : "no Union and Mossborn at peace with each other")}, talker {WoodMoot.Talker(map)?.LabelShort ?? "none"}; "
                              + $"the toll {(league != null ? league.Name + " and " + wheel.Name : "no league and Wheel")}, pack beast {RoadToll.BeastKind(wheel, map)?.defName ?? "none"}; "
                              + $"the plague year {(order != null ? order.Name + " and " + bell.Name : "no order and Bell")}, town {PlagueYear.Town(map)?.Label ?? "none under a plague in reach"}, "
                              + $"doctor {doctor?.LabelShort ?? "none"}, diggers {string.Join(", ", PlagueYear.Diggers(map, doctor).Select(p => p.LabelShort))}; "
                              + $"the table {(society != null ? society.Name + " and " + vault.Name : "no society and Vault")}, crafter {LearningTraded.Crafter(map)?.LabelShort ?? "none"}; "
                              + $"art offers {string.Join(", ", ArtHandedOn.Offers(map).Select(o => o.f.Name + " to " + o.p.LabelShort + " (" + o.art + ")"))}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 10: the hearing at the table or the gate done now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHearing()
        {
            int n = 0;
            foreach (var v in Open(WoodMoot.Kind).ToList())
                if (v.a != null && WoodMoot.Heard(v.a))
                    n++;
            foreach (var v in Open(RoadToll.Kind).ToList())
                if (v.a != null && RoadToll.Heard(v.a))
                    n++;
            Say(n > 0 ? n + " hearing done." : "no hearing under way (fire Rimfolk_Evt_WoodMoot or Rimfolk_Evt_RoadToll and choose a ruling or the fair toll).");
        }

        [DebugAction("Rimfolk", "Peoples' events 10: the days at the plague town done now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugPlague()
        {
            var there = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && v.kind == SeasonAway.Kind && SeasonAway.KeyOf(v) == PlagueYear.AwayKey
                                                                                   && v.stake == PeopleEvents2Math.There).ToList();
            if (there == null || there.Count == 0)
            {
                Say("nobody of the colony is at a plague town now (fire Rimfolk_Evt_PlagueYear and wait for them to get there).");
                return;
            }
            foreach (var v in there) v.due = Find.TickManager.TicksGame;
            Say(there.Count + " at the plague town set off for home within the hour.");
        }

        [DebugAction("Rimfolk", "Peoples' events 10: the next morning and afternoon at the table now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTable()
        {
            var v = LearningTraded.Open();
            if (v?.x == null)
            {
                Say("nobody at the colony's table (fire Rimfolk_Evt_LearningTraded and host them).");
                return;
            }
            LearningTraded.Copied(v.x);
            if (LearningTraded.Teaching(v) && v.a != null) LearningTraded.Taught(v.a);
            Say($"a morning's copying{(LearningTraded.Teaching(v) ? " and an afternoon's lesson" : "")} counted ({v.stake} mornings, {v.due} afternoons).");
        }

        [DebugAction("Rimfolk", "Peoples' events 10: a people remembers a colonist now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFriend()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && Peoples.IsOurs(f) && Arrivals.Usable(f) && ArtHandedOn.ArtOf(f) != null))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name, () =>
                {
                    var sub = new List<FloatMenuOption>();
                    foreach (var p in PeoplesG.Grown(map))
                    {
                        var colonist = p;
                        sub.Add(new FloatMenuOption(colonist.LabelShort, () =>
                        {
                            KinMemory.Note(people, "favorDone", colonist, null, 20);
                            if (people.HasGoodwill && people.PlayerGoodwill < PeopleEvents10Math.ArtGoodwill + 10)
                                people.TryAffectGoodwillWith(Faction.OfPlayer, (int)PeopleEvents10Math.ArtGoodwill + 10 - people.PlayerGoodwill, false, false);
                            Say(people.Name + " remember " + colonist.LabelShort + " for a good turn; now " + (ArtHandedOn.ArtKind(people, colonist) ?? "nothing")
                                + " (fire Rimfolk_Evt_ArtHandedOn).");
                        }));
                    }
                    if (sub.Count > 0) Find.WindowStack.Add(new FloatMenu(sub));
                }));
            }
            if (options.Count == 0)
            {
                Say("none of Rimfolk's peoples at peace.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
