using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the land and its life (Debug actions, "Rimfolk", "Land: ..."): a disaster's work party (Weather/AfterStorm.cs),
    /// last winter as the advisor reads it and one made up to try its notes (Advisor/AdvisorLastWinter.cs), the herd's autumn
    /// count, its question and the traders for the yearlings (Animals/HerdCount.cs), a raid's notes on a defense line with the
    /// colony's animals and the weather (Defense/DefenseLinesWatch.cs), and a cup for someone ill (Immersion/SickCup.cs).
    /// </summary>
    public static class LandLifeDebug
    {
        private static void Say(string text) => Messages.Message("Land: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Land: a disaster's work party now, as if it had just ended...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAfterStorm()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            Find.WindowStack.Add(new FloatMenu(AfterStormMath.Kinds.Select(k => new FloatMenuOption(k, () => Say(k + ": " + AfterStorm.DevNow(map, k)))).ToList()));
        }

        [DebugAction("Rimfolk", "Land: last winter as the advisor reads it", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLastWinter()
        {
            var w = AdvisorLastWinter.Of(Find.CurrentMap);
            Say(w == null
                ? "the record hasn't kept a winter here yet (it needs the last year's seasons, kept since this update)."
                : "the cold lasted " + w.coldDays + " days, " + w.frozenDays + " below freezing all day, the snow lay " + w.coverDays + ", the coldest night " + w.coldest.ToStringTemperature("F0") + " (" + w.days + " days looked at).");
        }

        [DebugAction("Rimfolk", "Land: a made-up last winter here (to try the advisor's notes)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMakeWinter()
        {
            var book = WeatherBook.Of(Find.CurrentMap);
            if (book == null) return;
            book.DevLastWinter();
            Say("the last year's seasons are made up: 23 cold days, 11 below freezing, the snow lay 9, the coldest night -24 °C. The advisor's firewood, winter stores and herd notes say it when they come up.");
        }

        [DebugAction("Rimfolk", "Land: the herd's autumn count now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCount()
        {
            var map = Find.CurrentMap;
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            if (h == null) return;
            Say(AnimalFacts.Herd(map).Count < HerdStoryMath.MinHerd ? "the herd here is under " + HerdStoryMath.MinHerd + " head." : HerdCount.Start(h, map, true));
        }

        [DebugAction("Rimfolk", "Land: the autumn count's question now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCountAsk() => Say(HerdCount.DevAsk(Find.CurrentMap));

        [DebugAction("Rimfolk", "Land: traders stop for the yearlings now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTraders() => Say(HerdCount.DevTraders(Find.CurrentMap));

        [DebugAction("Rimfolk", "Land: a raid's notes on the first defense line (the animals, the weather)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLineNotes()
        {
            var lines = DefenseLines.Of(Find.CurrentMap);
            Say(lines == null ? "no defense lines here." : lines.DevRaidNotes());
        }

        [DebugAction("Rimfolk", "Land: a cup for this colonist, ill (a head cold given if they have nothing)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCup(Pawn p)
        {
            if (p == null || !p.IsColonist) return;
            if (SickCup.Drinks.Count == 0)
            {
                Say("none of the mods with tea, hot cocoa or honey is loaded, so nothing is brought.");
                return;
            }
            if (SickCup.Ailment(p) == null)
            {
                var cold = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_HeadCold");
                if (cold == null) return;
                var h = HediffMaker.MakeHediff(cold, p);
                h.Severity = 0.35f;
                p.health.AddHediff(h);
                h.TryGetComp<HediffComp_AilmentImmunizable>()?.EndIncubation();
            }
            Say(SickCup.TrySend(p, out string why) ? "someone at their leisure goes for a cup for " + p.LabelShort + "." : why);
        }
    }
}
