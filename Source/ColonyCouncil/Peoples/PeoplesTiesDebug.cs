using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples and their ties (Debug actions, "Rimfolk", "Peoples' ties: ..."): each item of the content
    /// update's B4, B12, B14, B17, B22, B29 and B31 set going now through its own path, or what it reads written to the log.
    /// </summary>
    public static class PeoplesTiesDebug
    {
        private static void Say(string text) => Messages.Message("Peoples' ties: " + text, MessageTypeDefOf.SilentInput, false);

        private static Map Map => Find.CurrentMap;

        private static IEnumerable<Faction> Peoples() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && FactionMemory.Remembers(f));

        [DebugAction("Rimfolk", "Peoples' ties: what binds each people (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTies()
        {
            var lines = new List<string> { "[Rimfolk] The peoples and their ties:" };
            foreach (var f in Peoples())
            {
                lines.Add($"  {f.Name}: ties {PeopleTies.Describe(f)}; broke its word: {PeopleTies.Broke(f)}; their events and camps x{PeopleTies.Factor(f):0.00}; "
                          + $"gift carrier here: {PeopleTies.Carrier(f, Map)?.LabelShort ?? "none"}; kin at the colony: {KinNews.Describe(f)}");
                var town = FortuneShows.TownFor(f, Map, out var fortune);
                lines.Add($"    at the gate: {(town == null ? "their fortune shows nothing" : town.Name + " is " + FortuneShows.TownLine(town, fortune))}");
            }
            lines.Add("  Meal asks standing: " + FortuneShows.Describe());
            Log.Message(string.Join("\n", lines));
            Say("written to the log.");
        }

        [DebugAction("Rimfolk", "Peoples' ties: one of a bound people's own events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBoundEvent()
        {
            var options = new List<DebugMenuOption>();
            foreach (var f in Peoples().Where(f => PeopleTies.Bound(f) && ColonyCouncil.Peoples.CallingOf(f) != null))
            {
                var people = f;
                string calling = ColonyCouncil.Peoples.CallingOf(people);
                options.Add(new DebugMenuOption(people.Name + " (" + PeopleTies.Describe(people) + ")", DebugMenuOptionMode.Action, () =>
                {
                    var events = DefDatabase<IncidentDef>.AllDefs.Where(d => d.GetModExtension<HappeningExtension>()?.callings?.Contains(calling) == true).OrderBy(d => d.defName).ToList();
                    if (events.Count == 0)
                    {
                        Say("none of the peoples' own events names their calling (" + calling + ").");
                        return;
                    }
                    var list = new List<DebugMenuOption>();
                    foreach (var d in events)
                    {
                        var def = d;
                        list.Add(new DebugMenuOption(def.defName, DebugMenuOptionMode.Action, () =>
                        {
                            var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, Map);
                            parms.forced = true;
                            var carrier = PeopleTies.CarrierFor(def, Map);
                            bool done = def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
                            Say(done ? def.defName + " came: its letter closes with the tie" + (carrier != null ? ", and a gift in it goes to " + carrier.LabelShort + " first, when it may." : ".")
                                : def.defName + " couldn't come now (its own needs on the map).");
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
                }));
            }
            if (options.Count == 0) Say("no people of Rimfolk's is bound to the colony now (a pact, a marriage, a ward, an envoy, fealty or a debt of honour).");
            else Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("Rimfolk", "Peoples' ties: the new head comes to the gate, now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNewHead()
        {
            var options = new List<DebugMenuOption>();
            foreach (var f in Peoples().Where(f => f.leader != null && !f.HostileTo(Faction.OfPlayer)))
            {
                var people = f;
                options.Add(new DebugMenuOption(people.Name + " (" + people.leader.LabelShort + ", " + NewHeads.TemperOf(people.leader) + ")", DebugMenuOptionMode.Action, () =>
                {
                    var fu = new FollowUp
                    {
                        kind = NewHeads.Kind, people = people, ending = "settled", endTick = Find.TickManager.TicksGame, due = Find.TickManager.TicksGame, who = people.leader,
                        whoName = people.leader.LabelShort, flags = new List<string> { NewHeads.TemperOf(people.leader), "succeeded" }
                    };
                    if (!NewHeads.StillHead(fu))
                    {
                        Say("their leader isn't free in the world now (on a map, in a caravan, or held).");
                        return;
                    }
                    var s = NewHeads.Start(Map, fu);
                    Say(s != null ? people.leader.LabelShort + " rides out to the gate: the beat comes within the day." : "it couldn't start (no world event's slot free, or nobody of theirs can come).");
                }));
            }
            if (options.Count == 0) Say("no people at peace with a leader.");
            else Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("Rimfolk", "Peoples' ties: a visitor from a struggling town asks for a meal...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMealAsk()
        {
            var options = new List<DebugMenuOption>();
            foreach (var x in Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer)).ToList())
            {
                var pawn = x;
                options.Add(new DebugMenuOption(pawn.LabelShort + " (" + pawn.Faction.Name + ")", DebugMenuOptionMode.Action, () =>
                {
                    FortuneShows.Dress(new[] { pawn }, Fortune.Struggling);
                    FortuneShows.WillAsk(pawn, WorldSituationWorker.NearestTown(pawn.Faction, Map));
                    Say(pawn.LabelShort + "'s clothes are worn thin; they ask for a meal once a colonist is near (within the hour).");
                }));
            }
            if (options.Count == 0) Say("no visitor of a people at peace on the map.");
            else Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("Rimfolk", "Peoples' ties: the visitors here dressed as from a flourishing town", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFlourishing()
        {
            var them = Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer)).ToList();
            Say(FortuneShows.Dress(them, Fortune.Flourishing) ? "their clothes are a quality step better." : "nobody here wears anything with a quality to raise.");
        }

        [DebugAction("Rimfolk", "Peoples' ties: the word of each hard year comes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugKinWord()
        {
            var ws = WorldSituations.Instance;
            var hard = ws?.Running.Where(KinNews.Hard).ToList() ?? new List<WorldSituation>();
            foreach (var s in hard)
            {
                s.Clear(KinNews.WordFlag);
                s.Clear(KinNews.AbroadFlag);
                KinNews.Word(s, null);
            }
            Say(hard.Count == 0 ? "no hard world event runs at a people now (start a plague or a famine first)."
                : "the word came for " + hard.Count + " (a letter at each home with someone tied to them; the colony's own there write in the next hours).");
        }

        [DebugAction("Rimfolk", "Peoples' ties: help sent home is asked now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugKinAsk()
        {
            var tracker = DilemmaTracker.For(Map);
            var s = WorldSituations.Instance?.Running.FirstOrDefault(x => KinNews.Hard(x) && KinNewsMath.SendFor(x.def.kind) != null);
            if (tracker == null || s == null)
            {
                Say("no hard world event at a people runs now that food or medicine would answer.");
                return;
            }
            KinNews.asking = s;
            bool asked;
            try
            {
                asked = tracker.TryFire(true, d => d.Worker is DilemmaWorker_KinSendHome);
            }
            finally
            {
                KinNews.asking = null;
            }
            if (asked) s.Set(KinNews.AskedFlag);
            Say(asked ? "the council is asked." : "nobody here with a tie to " + (s.ctx.faction?.Name ?? "them") + " could ask (or the stores hold none of it).");
        }

        [DebugAction("Rimfolk", "Peoples' ties: the neighbours for good join the gathering under way now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHoldingGathering()
        {
            var c = HoldingDays.Neighbours(Map);
            if (c == null)
            {
                Say("no neighbours for good here at ease with the colony.");
                return;
            }
            c.dayGuestLabel = "the day";
            Say(HoldingDays.JoinGathering(c, Map) ? "they walk over to the gathering; the first of them there sets down their dish (within the hour)." : "no feast, bonfire or party is running now (start a calendar day or a party first).");
        }

        [DebugAction("Rimfolk", "Peoples' ties: the neighbours for good come to a colony grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHoldingGrave()
        {
            var c = HoldingDays.Neighbours(Map);
            if (c == null)
            {
                Say("no neighbours for good here at ease with the colony.");
                return;
            }
            c.lastGraveSeen = -1;
            if (c.ErrandSet) c.EndErrand();
            HoldingDays.ToTheGrave(c, Map, Find.TickManager.TicksGame, true);
            Say(c.ErrandSet ? "two of them walk to the grave within their hours (8 to 19)." : "no grave here holds one of the colony's people.");
        }

        [DebugAction("Rimfolk", "Peoples' ties: the neighbours for good ask the colony's friend over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHoldingInvite()
        {
            var c = HoldingDays.Neighbours(Map);
            if (c == null)
            {
                Say("no neighbours for good here at ease with the colony.");
                return;
            }
            HoldingDays.Invite(c, Map, "CC_HoldingDay_Why_Settled".Translate(c.HoldingName.Named("NAME")).Resolve());
            Say("the colonist warmest with them (two or more) goes over to their fire in the evening, if there is one.");
        }

        [DebugAction("Rimfolk", "Peoples' ties: a feast's guests and their towns' years (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFeastYear()
        {
            var p = Feasts.At(Map);
            if (p == null)
            {
                Say("no feast with guests planned here.");
                return;
            }
            var lines = p.guests.Select(g => $"  {g.name} of {g.people?.Name}: {FeastYear.YearOf(g, out var s)}{(s != null ? " (" + s.Label + ")" : "")}").ToList();
            lines.Add("  The toast: " + (FeastYear.Toast(p) ?? "the feast's own"));
            Log.Message("[Rimfolk] A feast's guests and their towns' years:\n" + string.Join("\n", lines));
            Say("written to the log.");
        }

        [DebugAction("Rimfolk", "Peoples' ties: what each remembered camp would say of home (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCampNews()
        {
            var lines = new List<string>();
            foreach (var past in CampReturns.Past)
            {
                var c = new Camp { kind = past.kind, faction = past.faction, map = Map, place = past.place };
                lines.Add($"  {past.kind?.label} of {past.faction?.Name}, gone {(Find.TickManager.TicksGame - past.leftTick) / 60000f:0.0} days: {CampReturns.WorldNews(c, past, new HashSet<string>()) ?? "nothing of home"}");
            }
            Log.Message("[Rimfolk] The camps the colony remembers, and the world's news they'd bring:\n" + (lines.Count == 0 ? "  none" : string.Join("\n", lines)));
            Say("written to the log.");
        }
    }
}
