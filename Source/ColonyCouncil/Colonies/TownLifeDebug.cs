using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force the town's own life (Debug actions, "Rimfolk"), for a town the colony was chosen from a menu: a raid
    /// on it now, its hard times now (its ask to come back), its people at the gate now (refused twice), a plague's
    /// reckoning now, its people at war with the colony, and what cools it. The waits and the rolls are skipped where said;
    /// what follows plays by the rules.
    /// </summary>
    public static class TownLifeDebug
    {
        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Town life: " + text, MessageTypeDefOf.SilentInput, false);

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

        [DebugAction("Rimfolk", "Town life: a raid on it now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRaid()
        {
            Pick(rec =>
            {
                var s = SituationWorker_TownRaid.Start(Homes.Seat ?? Find.CurrentMap, rec, true);
                Say(s != null ? "raiders of " + s.ctx.enemy?.Name + " are on the way to " + TownsAfter.TownName(rec) + "; the call comes within the hour or two."
                    : "no raid (the town not standing or at war with the colony, its map open, or no people at war with both that can field a band).");
            });
        }

        [DebugAction("Rimfolk", "Town life: its hard times now (its ask)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHardTimes()
        {
            Pick(rec =>
            {
                if (!rec.Standing || rec.end != TownEnd.None || rec.people.HostileTo(Faction.OfPlayer))
                {
                    Say("the town isn't standing at peace with the colony.");
                    return;
                }
                var t = WorldLedger.TownRecord(rec.key);
                if (t != null)
                {
                    t.prosperity = 10f;
                    t.lowSince = Now - 31 * GenDate.TicksPerDay;
                    WorldLedger.TownChanged();
                }
                if (rec.people.PlayerGoodwill < TownLifeMath.AskGoodwill)
                    rec.people.TryAffectGoodwillWith(Faction.OfPlayer, TownLifeMath.AskGoodwill - rec.people.PlayerGoodwill, false, false);
                rec.askOpen = false;
                rec.agreed = false;
                TownLife.AskBack(rec, Now);
                Say(TownsAfter.TownName(rec) + " is struggling (10, low a month) and asks to come back; refused " + rec.refusals + " time(s) so far.");
            });
        }

        [DebugAction("Rimfolk", "Town life: its people at the gate now (refused twice)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGate()
        {
            Pick(rec =>
            {
                if (!rec.Standing || rec.end != TownEnd.None)
                {
                    Say("the town isn't standing.");
                    return;
                }
                rec.refusals = Math.Max(rec.refusals, TownLifeMath.RefusalsToGive);
                rec.askOpen = false;
                rec.agreed = false;
                rec.gateAt = Now;
                WorldLedger.FormerChanged();
                Say("its people give " + TownsAfter.TownName(rec) + " up and come to the gate within the hour, by day and with no fight at home.");
            });
        }

        [DebugAction("Rimfolk", "Town life: a plague's reckoning now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPlague()
        {
            Pick(rec =>
            {
                var ws = WorldSituations.Instance;
                if (ws == null || !rec.Standing)
                {
                    Say("the town isn't standing.");
                    return;
                }
                // A plague of its people, ended now, as the world events keep one; the day's look reckons it (the rolls are the rule's).
                ws.history.Add(new SituationRecord
                {
                    defName = DefDatabase<WorldSituationDef>.AllDefsListForReading.FirstOrDefault(d => d.kind == "sickness")?.defName ?? "?", label = "a fever (dev)",
                    faction = rec.people, ending = "dev", startTick = Now - GenDate.TicksPerDay, endTick = Now, place = Refs.Of(rec.Town)
                });
                TownLife.Daily(Now);
                Say("a plague of " + rec.people?.Name + " ended now and was reckoned: a letter names anyone of the colony's former people it took (6% each, more if old or struggling).");
            });
        }

        [DebugAction("Rimfolk", "Town life: its people at war with the colony...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWar()
        {
            Pick(rec =>
            {
                if (rec.people == null || rec.people.HostileTo(Faction.OfPlayer))
                {
                    Say("already at war, or no people.");
                    return;
                }
                rec.people.TryAffectGoodwillWith(Faction.OfPlayer, -200, true, true);
                Say(rec.people.Name + " are at war with the colony: the bond breaks at the region's next day; their raids come with the storyteller's (try the game's own Raid incident with them).");
            });
        }

        [DebugAction("Rimfolk", "Town life: what cools it...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCooled()
        {
            Pick(rec =>
            {
                TownLife.Daily(Now);
                var n = TownLetters.HeadOf(rec);
                Say(TownsAfter.TownName(rec) + ": the colony's wrongs -" + rec.cooledWrongs + ", its head's regard -" + rec.cooledHead + " (" + (n != null ? NotableBook.Opinion(n).ToString() : "no head")
                    + "); the bond +" + HandOverMath.Bond(rec.bondEnded, rec.FormerHeads) + "; natural goodwill " + (rec.people?.NaturalGoodwill.ToString() ?? "?") + ", goodwill "
                    + (rec.people?.PlayerGoodwill.ToString() ?? "?") + "; struggling: " + TownLife.Struggling(rec, Now) + "; refused " + rec.refusals + ".");
            });
        }
    }
}
