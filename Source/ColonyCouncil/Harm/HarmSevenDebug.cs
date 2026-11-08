using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: harm, part 7 (Debug actions, "Rimfolk", "Harm seven: ..."). Each makes the next chance of one of the
    /// thirty-one come up, past its rate but never past the shared cap: what follows is the feature's own, at the moment
    /// it comes (a sit-down, a broken promise, a toast, a visit, a duel, a story's stage).
    /// </summary>
    public static class HarmSevenDebug
    {
        private static void Say(string text) => Messages.Message("Harm seven: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Pick(IEnumerable<Pawn> pawns, Func<Pawn, string> act)
        {
            var options = pawns.Select(p => new FloatMenuOption(p.LabelShort, () => Say(act(p)))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<Pawn> Ours => Find.CurrentMap.mapPawns.FreeColonistsSpawned;

        // ---------------- Tempers in the colony ----------------

        [DebugAction("Rimfolk", "Harm seven: the peacemaker's next sit-down comes to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSitDown()
        {
            Tempers.DebugSitDown = true;
            Say("the next time the peacemaker sits the worst pair down (all three together), they come to blows and the peacemaker steps between them.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next broken promise goes to the speaker", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpeaker()
        {
            Tempers.DebugSpeaker = true;
            Say("the next promise broken under demanding stakes sends the one it was made to to the speaker, bitter or not.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next one passed over holds it...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPassed() => Pick(Ours, p => Pick2(p, (a, b) =>
        {
            Tempers.DebugPassed = true;
            Tempers.PassedOver(a, b);
            return a.LabelShort + " holds it against " + b.LabelShort + " for a week: a sharp word the next two times they meet.";
        }));

        private static string Pick2(Pawn a, Func<Pawn, Pawn, string> act)
        {
            var b = a.Map.mapPawns.FreeColonistsSpawned.Where(o => o != a).OrderBy(o => o.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            return b == null ? "nobody else here." : act(a, b);
        }

        [DebugAction("Rimfolk", "Harm seven: the next council meeting boils over", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMeeting()
        {
            Tempers.DebugMeeting = true;
            Say("the next meeting decided with two rivals who both pitched at the table comes to blows.");
        }

        [DebugAction("Rimfolk", "Harm seven: a friend steps into the next social fight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFriend()
        {
            Tempers.DebugFriend = true;
            Say("the next social fight with a close friend of one of them within twelve cells: the friend runs over and pulls them apart.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next gossip said to its subject's face comes to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFace()
        {
            Tempers.DebugFace = true;
            Say("the next story told with its subject standing there: they come to blows with the teller a moment after.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next secret let slip brings the hearer to its owner", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWronged()
        {
            Tempers.DebugWronged = true;
            Say("the next secret let slip to one of ours (wronging the colony or not) sends them to its owner, to blows.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next confidence betrayed brings the keeper to the friend", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLeak()
        {
            Tempers.DebugLeak = true;
            Say("the next secret let slip by the friend it was told to: the keeper goes to find them, hard words, blows half the time.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next rivals in love come to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLove()
        {
            Tempers.DebugLove = true;
            Say("the next \"rival in love\" to leave its rival's stage, the two together: a shove, and the one they both like thinks less of the one who started it.");
        }

        [DebugAction("Rimfolk", "Harm seven: words at the altar at the next evening of the one faith", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAltar()
        {
            Tempers.DebugAltar = true;
            Say("with the one-faith decree in force, the next evening of its faith with a devout of another at home: they come up and come to blows with its speaker.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next quarrel at a day's table comes to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFeast()
        {
            Tempers.DebugFeast = true;
            Say("the next two rivals quarrelling at a day's table come to blows (drink or not), if they're still together.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next toast pours a second round", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRound()
        {
            Tempers.DebugRound = true;
            Say("the next toast with two or more who drank the first (Vanilla Brewing Expanded) pours a second round, plenty or not.");
        }

        [DebugAction("Rimfolk", "Harm seven: two rivals here quarrel over the second round", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuarrel() => Say(Tempers.DebugQuarrel(Find.CurrentMap));

        [DebugAction("Rimfolk", "Harm seven: the next soldier's nightmare strikes the roommate", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSoldier()
        {
            Tempers.DebugSoldier = true;
            Say("the next one asleep, shaken by a nightmare, with someone else in the bedroom, strikes out at them as they're woken (soldier's past or not).");
        }

        // ---------------- Visitors and outsiders ----------------

        [DebugAction("Rimfolk", "Harm seven: the next open gate's visitors bring a stranger", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGates()
        {
            GateTroubles.DebugGates = true;
            Say("with the open-gates decree in force and visitors here, the next hour one of them takes advantage: a thief at the stores or a drink and a fight.");
        }

        [DebugAction("Rimfolk", "Harm seven: the gambler's next evening of games turns", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGamble()
        {
            GateTroubles.DebugGamble = true;
            Say("the gambler's next evening with the colony at the table: a cheat caught and a fistfight with the hottest loser.");
        }

        [DebugAction("Rimfolk", "Harm seven: the barber's next cut nicks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBarber()
        {
            GateTroubles.DebugBarber = true;
            Say("the barber's next cut leaves a nick on the shoulder.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next guest of rank draws an enemy", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRank()
        {
            GateTroubles.DebugRank = true;
            Say("the next dignitary here (looked at within the hour) draws a hothead of their people with a challenge.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next fistfight after hard words draws a crowd", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCrowd()
        {
            GateTroubles.DebugCrowd = true;
            Say("the next hard words with an outsider that come to blows: others of both sides near with a temper to them join.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next duel to first blood goes on past it", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBlood()
        {
            GateTroubles.DebugBlood = true;
            Say("the next duel to first blood goes on until someone yields (bloodlust or not), and the onlookers pull them apart.");
        }

        [DebugAction("Rimfolk", "Harm seven: a challenge by letter for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugChallenge() => Pick(Ours, c =>
        {
            var x = c.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike)
                .OrderBy(p => p.Position.DistanceToSquared(c.Position)).FirstOrDefault();
            if (x == null) return "no outsider at peace on this map whose people could write.";
            GateTroubles.DebugChallenge = true;
            GateTroubles.Slighted(c, x, true);
            var w = HarmSeven.WaitingOf(HarmSeven.Challenge, a => a.pawn == c);
            if (w == null) return "their people's ways don't know duels (tribes, medieval folk and an empire's titled do).";
            w.flag = true;
            return "a challenge from " + x.Faction.Name + " for " + c.LabelShort + ": the letter within the hour, the challenger at once after.";
        });

        [DebugAction("Rimfolk", "Harm seven: the next heir carries a grudge", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHeir()
        {
            GateTroubles.DebugHeir = true;
            Say("the next town's head followed by an heir: the heir carries it on (wronged or not), and their guards' next visit comes to a fistfight.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next visit of the hated people", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHated()
        {
            GateTroubles.DebugHated = true;
            Say("the next visit of a people an avenger is sworn against, at peace now: the avenger walks up and picks a fight.");
        }

        [DebugAction("Rimfolk", "Harm seven: zeal at the gate at the next visit", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugZeal()
        {
            GateTroubles.DebugZeal = true;
            Say("the next visitors of another faith, with a devout of ours at home: hard words, then the outsiders' own fistfight chance.");
        }

        [DebugAction("Rimfolk", "Harm seven: hungry strangers in the fields tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHungry()
        {
            GateTroubles.DebugHungry = true;
            Say("at the next hour (famine or not, night or not), two or three hungry strangers come for the fields; one of ours who meets them may be struck.");
        }

        [DebugAction("Rimfolk", "Harm seven: the prospectors here come to blows", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCreek()
        {
            GateTroubles.DebugCreek = true;
            Say("at the next hour, with two parties of prospectors here, they brawl; one of ours within a few cells may be caught, unless the marshal gets there first.");
        }

        // ---------------- Personal stories ----------------

        [DebugAction("Rimfolk", "Harm seven: the next nemesis on the land seeks the one it hurt", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeek()
        {
            Stories.DebugSeek = true;
            Say("the next 250 ticks with a nemesis here and the one it hurt on the field: it goes for them (cover or not, fight or not).");
        }

        [DebugAction("Rimfolk", "Harm seven: the next worst night of getting clean, alone", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWorst()
        {
            Stories.DebugWorst = true;
            Say("the next hour of one on the worst night of \"getting clean\": the walk to the drug and the game's own binge, the friend there or not, if any is in reach.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next first steps on a new leg fall", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSteps()
        {
            Stories.DebugSteps = true;
            Say("the next hour of one on their first steps in \"the new leg\": a fall, the friend there or not.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next mage's practice working slips", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWorking()
        {
            Stories.DebugWorking = true;
            Say("the next hour of a fire, ice or storm mage in \"the long discipline\"'s practice (dawn or not): out to practise, and the working slips.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next lost child is found hurt", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLost()
        {
            Stories.DebugLost = true;
            Say("the next lost child found (a night out or not) has frostnip, a chill or a turned ankle by the weather.");
        }

        [DebugAction("Rimfolk", "Harm seven: the next waylaid messenger is set upon at the edge", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSetUpon()
        {
            Stories.DebugSetUpon = true;
            Say("the next messenger who comes in waylaid is set upon at the edge by bandits of a people at war with the colony instead.");
        }

        [DebugAction("Rimfolk", "Harm seven: the shared budget (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBudget()
        {
            Log.Message(Harm.BudgetReport());
            Say("the shared budget, each kind's pass, written to the log.");
        }
    }
}
