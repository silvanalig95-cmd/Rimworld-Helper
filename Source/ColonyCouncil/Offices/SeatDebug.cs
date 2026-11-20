using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the council's integrations, part 1 (Debug actions, "Rimfolk", "Council seats: ..."). Each sets one of them
    /// going now, through the feature's own path: the four seats at their work (their waits forgotten, their hours from
    /// now), a seat's question, a promise halfway, the petitions read out, the weight of office's hard call.
    /// </summary>
    public static class SeatDebug
    {
        private static void Say(string text) => Messages.Message("Council seats: " + text, MessageTypeDefOf.SilentInput, false);

        private static Map Map => Find.CurrentMap;

        private static void Seat(string office, string what)
        {
            var holder = Map == null ? null : OfficeTracker.For(Map)?.HolderOf(office);
            if (holder == null)
            {
                Say("nobody holds the seat (" + (DefDatabase<OfficeDef>.GetNamedSilentFail(office)?.Name ?? office) + ") here.");
                return;
            }
            string why = Interplay.DebugSeat(office, holder);
            Say(why == null ? what.Replace("{0}", holder.LabelShort) : "not now: " + why + ".");
        }

        [DebugAction("Rimfolk", "Council seats: the speaker hears out the one who trusts the council least, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpeaker() =>
            Seat(Interplay.Speaker, "{0} goes to sit an hour with whoever trusts the council least (with play-outs off, a word when they meet); their trust rises at the end.");

        [DebugAction("Rimfolk", "Council seats: the quartermaster's count of the stores, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCount() =>
            Seat(Interplay.Quartermaster, "{0} goes round the stores within the next hours; the count comes at the last store, in the news and on This week's plan.");

        [DebugAction("Rimfolk", "Council seats: the reeve's walk round the fields, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReeve() =>
            Seat(Interplay.Reeve, "{0} walks round the fields within the next hours, marking any blight for cutting at each field.");

        [DebugAction("Rimfolk", "Council seats: the marshal's walk on the walls at dusk, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMarshal() =>
            Seat(Interplay.Marshal, "{0} walks the walls from look-out to look-out within the next hours, with a word for anyone drafted or on watch near a look-out.");

        [DebugAction("Rimfolk", "Council seats: ask a question of a seat's work...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuestion()
        {
            var tracker = Map == null ? null : DilemmaTracker.For(Map);
            var options = DefDatabase<DilemmaDef>.AllDefsListForReading.Where(d => d.seat != null).OrderBy(d => d.seat.order).ThenBy(d => d.label)
                .Select(d => new FloatMenuOption(d.seat.Name + ": " + d.label, () =>
                {
                    var holder = OfficeTracker.For(Map)?.HolderOf(d.seat);
                    bool asked = tracker != null && tracker.TryFire(true, x => x == d);
                    Say(asked
                        ? "asked \"" + d.label + "\"" + (holder != null ? "; " + holder.LabelShort + " speaks in it unless they are its A or B." : "; nobody holds the seat, so it reads as before.")
                        : "\"" + d.label + "\" can't be asked now (its own conditions: nobody it fits, the season, the stores).");
                })).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(no question has a seat)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Council seats: a promise halfway, asked now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHalfway()
        {
            var tracker = Map == null ? null : DilemmaTracker.For(Map);
            OfficeTracker.DebugAny = true;
            bool asked;
            try
            {
                asked = tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_SeatHalfway);
            }
            finally
            {
                OfficeTracker.DebugAny = false;
            }
            Say(asked
                ? "asked about a holder's promise (any promise of a watch, the food, the sick, the herd or the colony's spirits, however it is going)."
                : "no holder at home with an election promise of those kinds not yet raised, or nobody to raise it.");
        }

        [DebugAction("Rimfolk", "Council seats: read the colony's petitions out now, as at a meeting", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPetitions()
        {
            int n = MeetingBusiness.DebugReadAll(Map);
            Say(n > 0
                ? n + " petition(s) read out with everyone up and about here as the room; the tallies show on the Decrees page and This week."
                : "no open petition (a decree asked for or to be lifted, or a want several back), or nobody up and about.");
        }

        [DebugAction("Rimfolk", "Council seats: give a new holder's weight of office its hard call...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCall()
        {
            var options = new List<FloatMenuOption>();
            foreach (var p in Map.mapPawns.FreeColonistsSpawned)
            {
                var arc = ArcTracker.Instance?.Of(p);
                if (arc?.def == null || !(arc.def.Worker is ArcWorker_Office)) continue;
                var who = p;
                foreach (var kind in new[] { SeatMath.CallQuestion, SeatMath.CallDecree, SeatMath.CallSupper })
                {
                    var k = kind;
                    options.Add(new FloatMenuOption(who.LabelShort + ": " + k, () =>
                        Say(SeatCalls.Call(who, k, k == SeatMath.CallSupper ? "a proper watch at night" : k == SeatMath.CallDecree ? "the curfew" : "the empty field")
                            ? who.LabelShort + "'s story has its call (" + k + "); it moves on to its hard call at the next hour, if two days have passed in its stage."
                            : "the story isn't waiting for a call (a later stage, or a call already noted).")));
                }
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no one here is in the weight of office)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
