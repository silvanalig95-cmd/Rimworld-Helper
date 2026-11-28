using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The climate's slow drift over the years (Weather/LandClimate.cs; the rules in ClimateMath.Trend, Push): a trend one
    /// way for the whole game (random by the world, or the way the setting chose), from the day the record began, and the
    /// pushes something that happened set going (a volcanic winter cools the land for a few years, a comet sets it moving
    /// one way or the other). The colony's own part (pollution, the trees lost) is worked out per map, live. Kept for the
    /// world: when the record began, and the pushes (six at most, the oldest let go).
    /// </summary>
    public class ClimateDrift : GameComponent
    {
        /// <summary>The tick the record began (the first look at a home map with the climate on), -1 before.</summary>
        public int since = -1;
        /// <summary>The pushes going: "kind:startTick:sign".</summary>
        public List<string> pushes = new List<string>();

        public const int MaxPushes = 6;

        public ClimateDrift(Game game)
        {
        }

        private static ClimateDrift cached;
        private static Game cachedGame;

        public static ClimateDrift Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<ClimateDrift>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public void Begin(int now)
        {
            if (since < 0) since = now;
        }

        /// <summary>A push set going by something that happened at <paramref name="tick"/>; one per thing (the same start is never added twice).</summary>
        public void AddPush(string kind, int tick, float sign)
        {
            string head = kind + ":" + tick + ":";
            if (pushes.Exists(p => p.StartsWith(head))) return;
            pushes.Add(head + sign.ToString("0", CultureInfo.InvariantCulture));
            while (pushes.Count > MaxPushes) pushes.RemoveAt(0);
        }

        /// <summary>The world's drift now (°C, and the lean wetter above 0): the trend since the record began and the pushes going.</summary>
        public (float temp, float wet) Now(int now, ClimateDriftMode mode, ClimateStrength s)
        {
            if (mode == ClimateDriftMode.Off || s == ClimateStrength.Off || since < 0) return (0f, 0f);
            int seed = Find.World?.info?.persistentRandomValue ?? 0;
            float years = (now - since) / (float)GenDate.TicksPerYear;
            var (t, w) = ClimateMath.Trend(mode, seed, years, s);
            foreach (var p in pushes)
            {
                var parts = p.Split(':');
                if (parts.Length != 3 || !int.TryParse(parts[1], out int start) || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float sign)) continue;
                var (pt, pw) = ClimateMath.Push(parts[0], (now - start) / (float)GenDate.TicksPerYear, sign, s);
                t += pt;
                w += pw;
            }
            return (t, w);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref since, "climateSince", -1);
            Scribe_Collections.Look(ref pushes, "climatePushes", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) pushes ??= new List<string>();
        }
    }
}
