using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force the town afterwards (Debug actions, "Rimfolk"), for a town the colony was chosen from a menu: its
    /// head's letter, a homecoming asked, its day's invitation, their yes to the colony's letter asking for it back, and
    /// the kept layout's size. The waits and the rolls are skipped; what follows plays by the rules.
    /// </summary>
    public static class TownAfterDebug
    {
        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Town after: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Pick(Action<LedgerFormer> act)
        {
            var options = new List<FloatMenuOption>();
            foreach (var r in WorldLedger.Formers.Where(r => r != null))
            {
                var rec = r;
                string state = rec.Standing ? rec.people?.Name ?? "?" : rec.end.ToString();
                options.Add(new FloatMenuOption(TownsAfter.TownName(rec) + " (" + state + ")", () => act(rec)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no town the colony was)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Town after: the head's letter now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLetter()
        {
            Pick(rec =>
            {
                int before = rec.lastLetter;
                TownsAfter.Letter(rec, Now);
                Say(rec.lastLetter != before ? "the letter from " + TownsAfter.TownName(rec) + " is written (their messenger brings it to the gate, or it comes by comms)." : "no letter (no head, no home, or the town not standing).");
            });
        }

        [DebugAction("Rimfolk", "Town after: a homecoming asked now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHomecoming()
        {
            Pick(rec =>
            {
                bool asked = rec.Standing && TownsAfter.Homecomings(rec, Now, true);
                Say(asked ? "one of its former people asks to come home." : "nobody would ask (each asks once; they need a spouse, child or close friend here, or hard times there).");
            });
        }

        [DebugAction("Rimfolk", "Town after: its day's invitation now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDay()
        {
            Pick(rec =>
            {
                var options = new List<FloatMenuOption>();
                foreach (var d in new[] { TownDay.Founding, TownDay.HandOver })
                {
                    var day = d;
                    options.Add(new FloatMenuOption(day.ToString(), () =>
                    {
                        bool sent = TownDayMission.Invite(rec, day, Homes.Seat);
                        if (sent) rec.lastInvite = Now;
                        Say(sent ? "the invitation is offered." : "it couldn't be offered (a question waits, a mission of its kind lately, the town too far, or the world events off).");
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            });
        }

        [DebugAction("Rimfolk", "Town after: their yes to taking it back now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugYes()
        {
            Pick(rec =>
            {
                if (!rec.Standing || rec.end != TownEnd.None)
                {
                    Say("the town is not its people's.");
                    return;
                }
                rec.agreed = true;
                rec.agreedAt = Now;
                rec.agreedTold = false;
                WorldLedger.FormerChanged();
                Say("they agree; the letter comes within the hour.");
            });
        }

        [DebugAction("Rimfolk", "Town after: the question about moving there now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMove()
        {
            var map = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            bool fired = tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_TownKin);
            Say(fired ? "the council is asked." : "nobody would ask (a spouse, child or close friend at a town the colony was, at peace; never one of the last four grown-ups; each asked once).");
        }

        [DebugAction("Rimfolk", "Town after: the kept layout...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLayout()
        {
            Pick(rec =>
            {
                var g = rec.layout?.grid;
                if (g == null)
                {
                    Say("no layout kept (handed over before release 2b, or nothing fit).");
                    return;
                }
                int buildings = (g.things?.Count(v => v != 0) ?? 0) + (g.under?.Count(v => v != 0) ?? 0);
                Say($"{g.width}x{g.height} cells at ({g.originX},{g.originZ}) of a {g.mapX}x{g.mapZ} map, {buildings} buildings, {g.table.Count} defs, about "
                    + (rec.layout.size / 1024f).ToString("0.0") + " KB" + (rec.layout.dropped.NullOrEmpty() ? "" : ", let go: " + rec.layout.dropped) + ".");
            });
        }
    }
}
