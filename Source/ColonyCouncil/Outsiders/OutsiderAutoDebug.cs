using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the right-click's things done by the colonists themselves (Debug actions, "Rimfolk", "Outsiders: ...").
    /// What a look on this map would weigh and begin now, written to the log without beginning it; and one begun now
    /// without the chance of a look or the day's pace (the right-click's own reasons, the stores and the outsider's last
    /// patience still hold), to watch it play out.
    /// </summary>
    public static class OutsiderAutoDebug
    {
        private static void Say(string text) => Messages.Message("Outsiders: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Outsiders: what they'd do by themselves now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWhat() => Run(true);

        [DebugAction("Rimfolk", "Outsiders: one by themselves now (no pace)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNow() => Run(false);

        private static void Run(bool dry)
        {
            var map = Find.CurrentMap;
            var book = OutsiderBook.Instance;
            if (map == null || book == null) return;
            int now = Find.TickManager.TicksGame;
            book.AutoDay(now);
            var mode = OutsiderAuto.Mode;
            var trial = new OutsiderAuto.Trial { force = true, dry = dry };
            float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
            trial.log.AppendLine($"[Rimfolk] Outsiders by themselves ({mode}): {book.autoToday} of {OutsiderAutoMath.PerDay} today, "
                                 + $"{book.autoSpent:0} of {OutsiderAutoMath.Budget(wealth):0} silver of the stores spent, the last "
                                 + (book.autoLast < 0 ? "never" : ((now - book.autoLast) / (float)GenDate.TicksPerHour).ToString("0.0") + " hours ago") + ".");
            bool began = mode != OutsiderAutoMode.Off && OutsiderAuto.TryOn(map, now, mode, book, trial);
            Log.Message(trial.log.ToString().TrimEnd());
            Say(mode == OutsiderAutoMode.Off ? "the setting is off." : began ? (dry ? "one would begin; see the log." : "one began; see the log.") : "nothing now; see the log.");
        }
    }
}
