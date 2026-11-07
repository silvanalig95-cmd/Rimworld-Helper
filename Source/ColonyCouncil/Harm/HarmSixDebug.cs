using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: harm, part 6 (Debug actions, "Rimfolk", "Harm six: ..."). Each brings one of the twenty-three about now, or
    /// makes its next chance come up, past its rate but never past the shared cap: what follows is the feature's own.
    /// </summary>
    public static class HarmSixDebug
    {
        private static void Say(string text) => Messages.Message("Harm six: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Pick(IEnumerable<Pawn> pawns, Func<Pawn, string> act)
        {
            var options = pawns.Select(p => new FloatMenuOption(p.LabelShort, () => Say(act(p)))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<Pawn> Ours => Find.CurrentMap.mapPawns.FreeColonistsSpawned;

        // ---------------- The work's accidents ----------------

        [DebugAction("Rimfolk", "Harm six: the next answer's hot or heavy work hurts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWork()
        {
            AnswerHurts.DebugWork = true;
            Say("the next fire laid for a gathering, pyre lit, body carried out or grave dug by one of ours brings a burn or a dropped load.");
        }

        [DebugAction("Rimfolk", "Harm six: one let back too soon falls...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFall() => Pick(Ours, p =>
        {
            AnswerHurts.OwesFall(p);
            AnswerHurts.DebugFall = true;
            return p.LabelShort + " owes a fall: the first hour within the day they're found carrying something, they go down with it (convalescing or not).";
        });

        [DebugAction("Rimfolk", "Harm six: the next makeshift bill hurts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMakeshift()
        {
            AnswerHurts.DebugMakeshift = true;
            Say("the next meal cooked at a campfire or butchered at a spot, while a proper bench could be built, brings a burn or a cut.");
        }

        [DebugAction("Rimfolk", "Harm six: the next wall or door taken down hurts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTakeDown()
        {
            AnswerHurts.DebugTakeDown = true;
            Say("the next wall or door taken down by one of ours comes apart on them (needs the deeper hooks).");
        }

        [DebugAction("Rimfolk", "Harm six: the next harvest hands come home sore", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHands()
        {
            AnswerHurts.DebugHands = true;
            Say("the first hand of the next town's harvest done brings home a strained back, blisters or a sunburn, shown when they're home.");
        }

        [DebugAction("Rimfolk", "Harm six: the next long day on a project's building slips", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPush()
        {
            WorkSlips.DebugPush = true;
            Say("the next colonist's hour at a project's own building in its last three days brings a slip.");
        }

        [DebugAction("Rimfolk", "Harm six: the next worn-down colonist's work slips", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugClumsy()
        {
            WorkSlips.DebugClumsy = true;
            Say("the next colonist's hour right at work brings a slip, worn down or not.");
        }

        [DebugAction("Rimfolk", "Harm six: the next hour harvesting is hurried and cuts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHurried()
        {
            WorkSlips.DebugHurried = true;
            Say("the next colonist's hour harvesting by hand brings a sickle's cut.");
        }

        [DebugAction("Rimfolk", "Harm six: the next heavy day of one who carries too much", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStrong()
        {
            WorkSlips.DebugStrong = true;
            Say("the next colonist with \"carries more than they should\" found carrying drops it on a foot.");
        }

        [DebugAction("Rimfolk", "Harm six: the next whittler or sharpener nicks a finger", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNick()
        {
            WorkSlips.DebugNick = true;
            Say("the next colonist found whittling or at the whetstone nicks a finger.");
        }

        [DebugAction("Rimfolk", "Harm six: a second cup before work for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSecond() => Pick(Ours, WorkSlips.DebugSecondCup);

        [DebugAction("Rimfolk", "Harm six: the stagger after a second cup for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStagger() => Pick(Ours, WorkSlips.DebugStagger);

        // ---------------- The animals ----------------

        [DebugAction("Rimfolk", "Harm six: the next hour among a camp's herd charges", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBull()
        {
            WildHurts.DebugBull = true;
            Say("the next colonist's hour within three cells of a camp's beast brings its charge.");
        }

        [DebugAction("Rimfolk", "Harm six: the next one too close to a wild animal is bitten", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugClose()
        {
            WildHurts.DebugClose = true;
            Say("the next encounter of one of ours close to a wild animal (the great beast close, a wild thing fed, a child meeting one) ends in a kick or a bite.");
        }

        [DebugAction("Rimfolk", "Harm six: a dawn walk now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDawn() => Pick(Ours, p =>
        {
            WildHurts.DebugDawn = true;
            return p.LabelShort + "'s next hour with \"walks the fields at dawn\" walks out (or looks about at the growing); a hungry predator in reach comes for them.";
        });

        [DebugAction("Rimfolk", "Harm six: the fed predator here grows bold", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBold()
        {
            WildHurts.DebugBold = true;
            Say("the beast that stays here, a predator, comes for someone alone near the stores at the next hour (fed or not).");
        }

        [DebugAction("Rimfolk", "Harm six: the next catch fights back", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCatch()
        {
            WildHurts.DebugCatch = true;
            Say("the next hour a trainer is in reach of the wild Pokémon they went to catch, it fights (their partner, or them).");
        }

        [DebugAction("Rimfolk", "Harm six: the fed visitor comes to the stores and swipes", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSwipe()
        {
            WildHurts.DebugSwipe = true;
            Say("the fed Pokémon here comes to the stores at the next hour, and swipes at whoever shoos it.");
        }

        // ---------------- The contests and the fights ----------------

        [DebugAction("Rimfolk", "Harm six: the next bonfire throws a spark", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpark()
        {
            Contests.DebugSpark = true;
            Say("the next bonfire burning at the hour throws a spark into the dry grass near it, if there is any.");
        }

        [DebugAction("Rimfolk", "Harm six: a muster at the line now, with a mishap", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMuster()
        {
            Contests.DebugMuster = true;
            Say("at the next hour the shooters free are called to the line, and the first at a post has a mishap.");
        }

        [DebugAction("Rimfolk", "Harm six: two fighters spar now, a blow landing", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpar()
        {
            Contests.DebugSpar = true;
            Say("at the next hour two of A RimWorld of Magic's fighters free spar, and a blow lands for real.");
        }

        [DebugAction("Rimfolk", "Harm six: a bold group's dare now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDare()
        {
            Contests.DebugDare = true;
            Say("at the next hour a group with two free eggs one into a dare (a cold swim, or wrestling its strongest, which ends in a sprain).");
        }

        [DebugAction("Rimfolk", "Harm six: a coward's name for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCoward() => Pick(Ours, p =>
        {
            Contests.CowardFormed(p);
            return p.LabelShort + " has a coward's name now: in the next fight here, undrafted, they break from cover.";
        });

        [DebugAction("Rimfolk", "Harm six: the next one to break from cover goes", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBreak()
        {
            Contests.DebugBreak = true;
            Say("the next coward's name or avenger looked at in a fight breaks from cover, past the rate.");
        }

        [DebugAction("Rimfolk", "Harm six: the shared budget (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBudget()
        {
            Log.Message(Harm.BudgetReport());
            Say("the shared budget is in the log.");
        }
    }
}
