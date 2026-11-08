using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the body when it dies (Debug actions, "Rimfolk", "Body when it dies: ..."). A wild animal made to bleed,
    /// its bleeding stopped either way the game stops it, and the marks in the log; the death itself is the game's own
    /// (its Kill tool, or a wound left to run).
    /// </summary>
    public static class BodyWhenItDiesDebug
    {
        private static void Say(string text) => Messages.Message("Body when it dies: " + text, MessageTypeDefOf.SilentInput, false);

        private static List<Pawn> NotOurs => Find.CurrentMap?.mapPawns.AllPawnsSpawned
            .Where(p => p.RaceProps != null && p.RaceProps.Animal && p.Faction != Faction.OfPlayer && !p.Dead).ToList() ?? new List<Pawn>();

        private static void Menu(IEnumerable<Pawn> animals, System.Action<Pawn> act)
        {
            var list = animals.Select(a => new FloatMenuOption(a.LabelShort + " (" + a.def.label + ", bleeding "
                                                               + a.health.hediffSet.BleedRateTotal.ToStringPercent() + "/day)", () => act(a))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no animal here that isn't the colony's)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        private static void Cut(Pawn a, float severity)
        {
            var part = a.RaceProps.body.corePart;
            var cut = HediffMaker.MakeHediff(HediffDefOf.Cut, a, part);
            cut.Severity = severity;
            a.health.AddHediff(cut, part);
            var o = BodyWhenItDies.OutlookOf(a);
            Say(a.LabelShort + " bleeds " + o.rate.ToStringPercent() + " a day: " + (o.Dies ? "it bleeds to death in about " + o.gameTicks.ToStringTicksToPeriod()
                : "it should live, its wounds closing in " + (o.closesIn == int.MaxValue ? "(never by age)" : o.closesIn.ToStringTicksToPeriod())) + ".");
        }

        [DebugAction("Rimfolk", "Body when it dies: a deep cut on an animal (bleeds out)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeepCut() => Menu(NotOurs, a => Cut(a, 14f));

        [DebugAction("Rimfolk", "Body when it dies: a light cut on an animal (lives)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLightCut() => Menu(NotOurs, a => Cut(a, 3f));

        [DebugAction("Rimfolk", "Body when it dies: tend an animal's wounds (stops the bleeding)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTend() => Menu(NotOurs, a =>
        {
            foreach (var h in a.health.hediffSet.hediffs.Where(h => h.Bleeding && h.TendableNow()).ToList()) h.Tended(0.6f, 1f);
            Say(a.LabelShort + "'s wounds are tended: the order is dropped on the next short pass.");
        });

        [DebugAction("Rimfolk", "Body when it dies: age an animal's wounds past bleeding...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAge() => Menu(NotOurs, a =>
        {
            foreach (var h in a.health.hediffSet.hediffs.Where(h => h.Bleeding).ToList())
            {
                h.ageTicks += 180000;
                a.health.Notify_HediffChanged(h);
            }
            Say(a.LabelShort + "'s wounds are three days older: the order is dropped on the next short pass.");
        });

        [DebugAction("Rimfolk", "Body when it dies: the marked animals (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLog()
        {
            Log.Message("[Rimfolk] Body when it dies, the marked animals:\n" + BodyWhenItDies.Explain());
            Say("the marked animals are in the log.");
        }
    }
}
