using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the old powers' happenings now (Debug actions, "Rimfolk", "Old powers: ... now"), one a happening, each begun
    /// at the current map past its pace and its call, as the hour would begin it; and the pace now, in the log.
    /// </summary>
    public static class OldPowersDebug
    {
        private static void Say(string text) => Messages.Message("Old powers: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Fire(OldPower e, string what, bool strong = false)
        {
            var map = Find.CurrentMap;
            if (map == null || OldPowersBook.Instance == null) return;
            Say(OldPowers.Fire(e, map, strong) ? what : "it couldn't begin here (" + Why(e) + ").");
        }

        private static string Why(OldPower e)
        {
            switch (e)
            {
                case OldPower.Seer:
                case OldPower.Enchanter:
                case OldPower.Pilgrims: return "no people at peace to send anyone, or no open ground at the middle; the enchanter needs a piece below masterwork";
                case OldPower.OneWay: return "nobody of the colony awake, or no edge to face";
                case OldPower.Stone: return "no stone chunk out of doors near the middle and no rock face";
                case OldPower.Scroll: return "nobody of the colony standing in a room of the old ruins not searched yet";
                case OldPower.Charged: return "no mage, psycaster or sensitive one of the colony";
                case OldPower.Buried: return "no open earth near the middle";
                case OldPower.DeadRoad: return "no road across the map";
                case OldPower.OtherSky: return "another sky is already on";
                case OldPower.Beast: return "nobody of the colony out of doors with a wild beast near";
                case OldPower.GodPasses: return "the gods off, or no god of the colony with signs of its own that can show here";
                case OldPower.SpiritPack: return "nobody of the colony awake";
                case OldPower.Watched: return "nobody of the colony awake, or not two dark open places round the colony (try at night)";
                default: return "?";
            }
        }

        [DebugAction("Rimfolk", "Old powers: the seer of the season now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeer() => Fire(OldPower.Seer, "a seer walks in to the colony's middle; someone goes to sit with them, and they say what the next season holds.");

        [DebugAction("Rimfolk", "Old powers: everything faces one way now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOneWay() => Fire(OldPower.OneWay, "everyone and every beast awake faces one way for ten seconds; at dawn (six to ten) someone walks that way.");

        [DebugAction("Rimfolk", "Old powers: the humming stone now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStone() => Fire(OldPower.Stone, "the stone hums till five in the morning; someone walks over and puts a hand on it.");

        [DebugAction("Rimfolk", "Old powers: the sealed scroll now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugScroll() => Fire(OldPower.Scroll, "the colonist in the old ruins walks to a scroll lying there and kneels by it.");

        [DebugAction("Rimfolk", "Old powers: the casters burn bright now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCharged() => Fire(OldPower.Charged, "the colony's casters light up where they stand (mana or psyfocus filled).", true);

        [DebugAction("Rimfolk", "Old powers: the wandering enchanter now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnchanter() => Fire(OldPower.Enchanter, "an enchanter walks in; at the middle they ask to work a piece (when the letters' gap allows).");

        [DebugAction("Rimfolk", "Old powers: the thing under the ground now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBuried() => Fire(OldPower.Buried, "a knock under the ground; the ground cracks a day later, and the next morning something comes up (\"Old powers: the next step now\").");

        [DebugAction("Rimfolk", "Old powers: the dead on the road now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeadRoad() => Fire(OldPower.DeadRoad, "the dead walk a road across the land (with Anomaly and harm on, half the time the game's shamblers).", true);

        [DebugAction("Rimfolk", "Old powers: pilgrims passing now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPilgrims() => Fire(OldPower.Pilgrims, "pilgrims walk in to the colony's graves, oldest tree or middle, pray there, leave a token and walk on.");

        [DebugAction("Rimfolk", "Old powers: another sky now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOtherSky() => Fire(OldPower.OtherSky, "another sky till five in the morning (it shows at night only).");

        [DebugAction("Rimfolk", "Old powers: the beast that speaks now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeast() => Fire(OldPower.Beast, "a wild beast walks up to someone of the colony out of doors and says one thing.");

        [DebugAction("Rimfolk", "Old powers: a god goes by now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGodPasses() => Fire(OldPower.GodPasses, "the god's own signs come tonight from ten, two hours apart; the card comes at six in the morning, with the god's own presence a while after it.", true);

        [DebugAction("Rimfolk", "Old powers: the pale wolves now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpiritPack() => Fire(OldPower.SpiritPack, "a howl, then pale wolves run once round the colony and off the land; at dawn (six to ten) someone looks for their tracks.");

        [DebugAction("Rimfolk", "Old powers: eyes in the dark now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWatched() => Fire(OldPower.Watched, "pairs of eyes at the edge of the dark round the colony; after a minute someone walks out to the nearest pair (at night only: daylight puts them out).");

        [DebugAction("Rimfolk", "Old powers: the next step now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNext()
        {
            var b = OldPowersBook.Instance;
            var p = b?.play;
            if (p == null)
            {
                Say("nothing under way.");
                return;
            }
            p.next = Find.TickManager.TicksGame;
            b.Due(p.next);
            Say(p.kind + " at stage " + p.stage + ": its next step comes on the next tick (a step that waits for an hour of the day still waits for it).");
        }

        [DebugAction("Rimfolk", "Old powers: the pace now (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPace()
        {
            var b = OldPowersBook.Instance;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            int now = Find.TickManager.TicksGame;
            var sb = new StringBuilder();
            sb.AppendLine("[Rimfolk] the old powers: lever " + OldPowers.Lever.ToString("0.00") + ", on " + OldPowers.On + ", the last of any " + OldPowers.DaysSince(b.lastAny, now).ToString("0.0")
                          + " days ago (none within " + OldPowersMath.SpacingDays + "), under way: " + (b.play == null ? "nothing" : b.play.kind + " stage " + b.play.stage) + ".");
            foreach (var e in OldPowersMath.All)
            {
                var pace = OldPowersMath.PaceOf(e);
                float since = OldPowers.DaysSince(b.Last(e), now);
                GodPaceMath.Trigger why;
                try
                {
                    why = OldPowers.Call(e, map);
                }
                catch
                {
                    why = GodPaceMath.Trigger.None;
                }
                sb.AppendLine("  " + e + ": last " + since.ToString("0.0") + " days ago, gap " + pace.gap + ", mean " + pace.mean + ", hours " + OldPowersMath.HoursOf(e) + ", call now " + why
                              + ", a look's chance " + OldPowersMath.CheckChance(e, since, why, OldPowers.Lever).ToString("0.0000"));
            }
            sb.AppendLine("  Forms lately: " + string.Join(", ", b.lately));
            Log.Message(sb.ToString());
            Say("the pace is in the log.");
        }
    }
}
