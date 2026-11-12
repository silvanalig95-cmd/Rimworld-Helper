using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A way to play on its StorytellerDef: Rimfolk's levers for it, how the world shapes them, and what it leans the
    /// region's world situations and the everyday hurts toward. The game's own parts of the storyteller (its threats, its
    /// sickness, its traders) are the def's comps; this is what it does to Rimfolk's own happenings.
    /// </summary>
    public class StoryWayExtension : DefModExtension
    {
        public StoryWay way = StoryWay.None;
        /// <summary>How its levers move with the world (the seasons, the colony's deeds, the long arc), or not at all.</summary>
        public StoryShape shape = StoryShape.None;
        public StoryLevers levers = new StoryLevers();
        /// <summary>Kinds of world situation (WorldSituationDef.kind) that come more often under this way.</summary>
        public List<string> situationKinds;
        /// <summary>Kinds of everyday hurt (HarmKinds keys) it leans the shared budget toward; the budget itself stays the same.</summary>
        public List<string> harmLean;
        public float harmLeanFactor = 2f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (way == StoryWay.None) yield return "a way to play needs its way";
            if (levers == null) yield break;
            foreach (var key in StoryWaysMath.HarmKeys)
                if (levers.Get(key) > 1f)
                    yield return "the lever " + key + " is on something that hurts and can't be above 1";
            if (levers.raidReasons < 0f || levers.raidReasons > 1f) yield return "raidReasons is a share, 0 to 1";
            foreach (var key in StoryWaysMath.Keys)
                if (levers.Get(key) < 0f || levers.Get(key) > StoryWaysMath.MaxLever)
                    yield return "the lever " + key + " is outside 0 to " + StoryWaysMath.MaxLever;
            if (harmLean != null)
                foreach (var k in harmLean)
                    if (HarmKinds.Of(k) == null)
                        yield return "harmLean names no kind of hurt: " + k;
        }
    }

    /// <summary>
    /// The reader of the ways' levers (the player's request, 12 November 2026): <see cref="Factor"/> is the lever of the way
    /// that leans the game now, the story direction when one is chosen in Rimfolk's settings, else the storyteller's own when
    /// it is one of the eight, else 1 (nothing changes). Rimfolk's schedulers multiply their own chances by it where each way
    /// needs them (StoryWaysMath lists the levers and DESIGN.md "Ways to play" the seams). Nothing is saved: the season,
    /// the deeds and the arc are read off the game when asked.
    /// </summary>
    public static class StoryDirection
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The direction chosen in the settings, or none.</summary>
        public static StoryWay Direction => S?.storyDirection ?? StoryWay.None;

        /// <summary>The storyteller's own way, when it is one of the eight.</summary>
        public static StoryWay StorytellerWay => Find.Storyteller?.def?.GetModExtension<StoryWayExtension>()?.way ?? StoryWay.None;

        /// <summary>A way's levers, from its StorytellerDef; null for none or a def that isn't loaded.</summary>
        public static StoryWayExtension Of(StoryWay w)
        {
            if (w == StoryWay.None) return null;
            return DefDatabase<StorytellerDef>.GetNamedSilentFail(StoryWaysMath.DefName(w))?.GetModExtension<StoryWayExtension>();
        }

        /// <summary>The way leaning the game now (the direction first), with its levers; null when none does.</summary>
        public static StoryWayExtension Active
        {
            get
            {
                if (Current.Game == null) return null;
                var d = Direction;
                if (d != StoryWay.None) return Of(d);
                return Find.Storyteller?.def?.GetModExtension<StoryWayExtension>();
            }
        }

        /// <summary>The way leaning the game now, or none.</summary>
        public static StoryWay Way => Active?.way ?? StoryWay.None;

        /// <summary>
        /// A lever now (StoryWaysMath's keys): the active way's value, shaped by the season, the deeds or the arc as the way
        /// is; 1 (and 0 for the raids' reasons) when no way leans the game.
        /// </summary>
        public static float Factor(string key)
        {
            try
            {
                var ext = Active;
                if (ext?.levers == null) return StoryWaysMath.Neutral(key);
                return StoryWaysMath.Combine(key, ext.levers.Get(key), ext.shape, Quarter(null), DeedsFor(ext.shape), Phase);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ways to play: a lever", e);
                return StoryWaysMath.Neutral(key);
            }
        }

        /// <summary>The lever on one of Rimfolk's happenings by its group: visitors, challenges, pasts, the land and sky, and those that may end in a fight.</summary>
        public static float HappeningFactor(HappeningExtension ext)
        {
            if (ext == null) return 1f;
            if (ext.battle || ext.group == HappeningGroup.Battle) return Factor(StoryWaysMath.Fights);
            switch (ext.group)
            {
                case HappeningGroup.Visitors: return Factor(StoryWaysMath.Visitors);
                case HappeningGroup.Challenge: return Factor(StoryWaysMath.Challenges);
                case HappeningGroup.Past: return Factor(StoryWaysMath.Pasts);
                case HappeningGroup.Nature: return Factor(StoryWaysMath.Nature);
                default: return 1f;
            }
        }

        // ================= The season =================

        /// <summary>The quarter of the year at a home (0 spring to 3 winter); where the season never turns, the quadrum's.</summary>
        public static int Quarter(Map map)
        {
            map ??= Homes.Seat ?? Find.AnyPlayerHomeMap;
            if (map == null) return QuarterOf(GenDate.Quadrum(Find.TickManager?.TicksAbs ?? 0, 0f));
            switch (GenLocalDate.Season(map))
            {
                case Season.Spring: return StoryWaysMath.Spring;
                case Season.Summer: return StoryWaysMath.Summer;
                case Season.Fall: return StoryWaysMath.Autumn;
                case Season.Winter: return StoryWaysMath.Winter;
                default: return Math.Max(0, Math.Min(3, (int)GenLocalDate.Twelfth(map) / 3));
            }
        }

        private static int QuarterOf(Quadrum q) => q == Quadrum.Undefined ? 0 : (int)q;

        // ================= The deeds =================

        private static float deeds;
        private static int deedsDay = -1;
        private static Game deedsGame;

        /// <summary>The colony's deeds as the peoples remember them, -1 cruel to 1 kind (StoryWaysMath.DeedsBalance), read once a day.</summary>
        public static float Deeds
        {
            get
            {
                int day = GenDate.DaysPassed;
                if (deedsGame == Current.Game && deedsDay == day) return deeds;
                deedsGame = Current.Game;
                deedsDay = day;
                deeds = 0f;
                var mem = FactionMemory.Instance;
                if (mem?.deeds == null || !FactionMemory.On) return deeds;
                int now = Find.TickManager.TicksGame;
                float good = 0f, bad = 0f;
                foreach (var d in mem.deeds)
                {
                    if (d == null) continue;
                    if (d.side == DeedSide.Favor) good += d.StrengthAt(now);
                    else if (d.side == DeedSide.Wrong) bad += d.StrengthAt(now);
                }
                deeds = StoryWaysMath.DeedsBalance(good, bad);
                return deeds;
            }
        }

        private static float DeedsFor(StoryShape shape) => shape == StoryShape.Deeds ? Deeds : 0f;

        // ================= The long arc =================

        /// <summary>Days since the colony settled.</summary>
        public static float Days => GenDate.DaysPassedSinceSettleFloat;

        public static int Seed => Find.World?.info?.persistentRandomValue ?? 0;

        public static int Cycle => StoryWaysMath.ArcCycle(Days);

        public static ArcPhase Phase => StoryWaysMath.PhaseAt(Days);

        public static StoryTrouble Trouble => StoryWaysMath.TroubleOf(Cycle, Seed);

        // ================= World situations and hurts =================

        /// <summary>A world situation's weight in the draw: more for a kind the active way leans to (always, or this season, or in this phase).</summary>
        public static float SituationFactor(string kind)
        {
            try
            {
                var ext = Active;
                if (ext == null || string.IsNullOrEmpty(kind)) return 1f;
                string[] now = ext.shape == StoryShape.Seasons ? StoryWaysMath.SeasonSituations(Quarter(null))
                    : ext.shape == StoryShape.Arc ? StoryWaysMath.ArcSituations(Phase, Trouble) : null;
                return StoryWaysMath.SituationFactor(kind, ext.situationKinds, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ways to play: a world situation's weight", e);
                return 1f;
            }
        }

        /// <summary>A kind of everyday hurt's tilt within the shared budget (StoryWaysMath.HarmTilt): 1 unless the active way leans to it.</summary>
        public static float HarmTilt(string kindKey)
        {
            var ext = Active;
            return ext == null ? 1f : StoryWaysMath.HarmTilt(kindKey, ext.harmLean, ext.harmLeanFactor);
        }

        // ================= For the dev tools =================

        /// <summary>The way leaning the game now and every lever's value, for the dev tools.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            var st = Find.Storyteller?.def;
            sb.AppendLine("[Rimfolk] ways to play: the storyteller " + (st?.label ?? "none") + " (" + StorytellerWay + "), the direction " + Direction + ".");
            var ext = Active;
            if (ext == null)
            {
                sb.AppendLine("  No way leans the game: every lever is as it is.");
                return sb.ToString();
            }
            sb.AppendLine("  Leaning now: " + ext.way + (Direction != StoryWay.None ? " (the direction)" : " (the storyteller)") + ", shaped by " + ext.shape + ".");
            sb.AppendLine("  The season: quarter " + Quarter(null) + "; the deeds: " + Deeds.ToString("0.00") + "; the arc: cycle " + Cycle + ", day "
                          + StoryWaysMath.ArcDay(Days).ToString("0.0") + ", " + Phase + ", its trouble " + Trouble + ".");
            foreach (var key in StoryWaysMath.Keys)
                sb.AppendLine("  " + key + ": " + ext.levers.Get(key).ToString("0.00") + " in the def, " + Factor(key).ToString("0.00") + " now");
            if (!ext.situationKinds.NullOrEmpty()) sb.AppendLine("  World situations leaned to: " + string.Join(", ", ext.situationKinds));
            if (!ext.harmLean.NullOrEmpty()) sb.AppendLine("  Everyday hurts leaned to (same budget): " + string.Join(", ", ext.harmLean) + " x" + ext.harmLeanFactor.ToString("0.0"));
            return sb.ToString();
        }

        /// <summary>The eight ways' defs that are loaded, for the dev tools and the self-check.</summary>
        public static IEnumerable<StorytellerDef> Defs => StoryWaysMath.Ways.Select(w => DefDatabase<StorytellerDef>.GetNamedSilentFail(StoryWaysMath.DefName(w))).Where(d => d != null);
    }
}
