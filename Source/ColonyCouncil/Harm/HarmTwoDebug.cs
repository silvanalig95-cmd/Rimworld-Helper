using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: harm, parts 2 and 3 (Debug actions, "Rimfolk", "Harm two: ..."). Each brings one of the seven about now, or
    /// makes its next chance come up, past its rate but never past the shared cap: what follows is the feature's own.
    /// </summary>
    public static class HarmTwoDebug
    {
        private static void Say(string text) => Messages.Message("Harm two: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Harm two: the camp's quarrel comes to blows now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCampBrawl()
        {
            var camp = CampOnMap.Of(Find.CurrentMap);
            Say(camp == null ? "no camp on this map." : camp.DebugBrawl());
        }

        [DebugAction("Rimfolk", "Harm two: the next champion's bout lands a real blow", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugChampion()
        {
            ChampionBouts.DebugHard = true;
            Say(ChampionBouts.On ? "the next fighting challenge's bout lands its blow (Rimfolk events: the champion, skill Melee)."
                : "needs hurts from everyday life and the outsiders' practice bouts on.");
        }

        [DebugAction("Rimfolk", "Harm two: mark a lone beast that can turn for hunting", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHunt()
        {
            var map = Find.CurrentMap;
            if (!HuntsBite.On)
            {
                Say("needs hurts from everyday life, answers on the map and the deeper hooks.");
                return;
            }
            var a = map.mapPawns.AllPawnsSpawned.Where(x => map.designationManager.DesignationOn(x) == null && HuntsBite.MayTurn(x, map))
                .OrderByDescending(x => x.GetStatValue(StatDefOf.MeatAmount)).FirstOrDefault();
            if (a == null)
            {
                Say("no lone beast that can turn on this map (a boar, an elk or a muffalo with no herd about it).");
                return;
            }
            map.designationManager.AddDesignation(new Designation(a, DesignationDefOf.Hunt));
            HuntsBite.Marked(a);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_Play_Hunt".Translate(a.LabelShort).Resolve() + HuntsBite.MarkedNote(a), new LookTargets(a),
                MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("Rimfolk", "Harm two: a sickness brought home shows...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSick()
        {
            var options = Find.CurrentMap.mapPawns.FreeColonistsSpawned
                .Select(p => new FloatMenuOption(p.LabelShort, () => Say(SickHome.DebugOnset(p)))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Harm two: a rival spoils the other's work today...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSabotage()
        {
            var map = Find.CurrentMap;
            var web = CouncilGameComponent.Instance?.social;
            var options = new List<FloatMenuOption>();
            if (web != null)
                foreach (var t in web.ties.Where(t => t != null && t.kind == TieKind.Rivals && t.a?.Map == map && t.b?.Map == map))
                    foreach (var (r, o) in new[] { (t.a, t.b), (t.b, t.a) })
                    {
                        var rival = r;
                        var other = o;
                        options.Add(new FloatMenuOption(rival.LabelShort + " at " + other.LabelShort + "'s bench (rivals at " + t.strength.ToString("0") + ")",
                            () => Say(Sabotage.DebugIntent(rival, other))));
                    }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no rivals on this map)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Harm two: the next heckle at the hustings comes to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHustings()
        {
            OfficeTracker.DebugHustingsBlows = true;
            Say("the next heckle at an election's hustings comes to blows, a supporter of the speaker beside the heckler standing in for a rival.");
        }

        [DebugAction("Rimfolk", "Harm two: frayed tempers on this map", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFrayed()
        {
            foreach (var p in Find.CurrentMap.mapPawns.FreeColonistsSpawned)
                Log.Message("[Rimfolk] " + p.LabelShort + ": " + (Heart.Eligible(p) ? HeartView.BandLabel(Heart.BandOf(p)) : "no heart") + ", a fight "
                            + FrayedTempers.Factor(p).ToString("0.00") + " times as likely; " + (Harm.May(p, HurtClass.Fight) ? "may be hurt now" : "held back: " + Harm.Bar(p, HurtClass.Fight)));
            Say("each colonist's frayed temper is in the log.");
        }
    }
}
