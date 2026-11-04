using System;
using System.Diagnostics;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's clock: how long each periodic pass in Sched's table takes on this computer. A pass is timed with two
    /// <see cref="Stopwatch.GetTimestamp"/> reads around it (<see cref="Start"/> and <see cref="Stop"/>, or a
    /// <see cref="Time"/> scope), and its figures are kept per pass for this in-game hour and day, the ones before, and the
    /// worst single run (<see cref="PassFigures"/>, rolled on by game ticks). Nothing allocates per run and nothing is
    /// saved: the figures start again with each game loaded. The dev window "Rimfolk's clock" reads them
    /// (UI/ClockWindow.cs). Timing a pass changes nothing it does: no random numbers, no game state, and an exception
    /// goes on exactly as before (the run is then just counted, or not).
    /// </summary>
    public static class PassClock
    {
        private static readonly PassFigures[] figures = Make();
        private static Game game;
        private static int since = PassWindow.None;

        private static PassFigures[] Make()
        {
            var all = new PassFigures[PassTable.Count];
            for (int i = 0; i < all.Length; i++) all[i] = new PassFigures();
            return all;
        }

        /// <summary>The stopwatch's ticks a second.</summary>
        public static long Frequency => Stopwatch.Frequency;

        /// <summary>The game tick the clock started at in this game (the first pass it timed), or <see cref="PassWindow.None"/>.</summary>
        public static int Since => ReferenceEquals(game, Current.Game) ? since : PassWindow.None;

        /// <summary>A pass's figures, as of the last run it timed (read them with the tick of now: <see cref="PassFigures.HourAt"/> and the rest).</summary>
        public static PassFigures Of(Pass pass)
        {
            if (!ReferenceEquals(game, Current.Game)) Reset(Current.Game);
            return figures[(int)pass];
        }

        /// <summary>Before a pass: the stopwatch's reading.</summary>
        public static long Start() => Stopwatch.GetTimestamp();

        /// <summary>After a pass that began at <paramref name="start"/> (from <see cref="Start"/>).</summary>
        public static void Stop(Pass pass, long start)
        {
            long elapsed = Stopwatch.GetTimestamp() - start;
            var g = Current.Game;
            if (g == null) return;
            if (!ReferenceEquals(g, game)) Reset(g);
            int now = g.tickManager?.TicksGame ?? 0;
            if (since == PassWindow.None) since = now;
            int i = (int)pass;
            if (i >= 0 && i < figures.Length) figures[i].Add(now, elapsed);
        }

        /// <summary>Times what runs until the scope ends (<c>using var clock = PassClock.Time(Pass.X);</c>). A struct: nothing allocated.</summary>
        public static PassTimer Time(Pass pass) => new PassTimer(pass, Stopwatch.GetTimestamp());

        private static void Reset(Game g)
        {
            game = g;
            since = PassWindow.None;
            for (int i = 0; i < figures.Length; i++) figures[i].Clear();
        }
    }

    /// <summary>A pass being timed: <see cref="PassClock.Stop"/> when it ends.</summary>
    public readonly struct PassTimer : IDisposable
    {
        private readonly Pass pass;
        private readonly long start;

        public PassTimer(Pass pass, long start)
        {
            this.pass = pass;
            this.start = start;
        }

        public void Dispose() => PassClock.Stop(pass, start);
    }
}
