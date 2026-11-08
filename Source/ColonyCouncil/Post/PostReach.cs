using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where a town lies for the colony's letters, worked out from the world's own travel time.</summary>
    public class ReachInfo
    {
        public ReachBand band;
        /// <summary>Days on the road there by the colony's means (-1: no way by land).</summary>
        public float days = -1f;
        /// <summary>A walking caravan's days over the world's path (-1: none).</summary>
        public float walkDays = -1f;
        public LetterMeans means;
        /// <summary>The town a letter is relayed through when it is far (else null), and the days in all that way.</summary>
        public Settlement relay;
        public float relayDays = -1f;
        /// <summary>A call on the comms console reaches them now (a working console, and they have radio or a mast near).</summary>
        public bool call;
        /// <summary>They have radio (industrial or later, or a Signal Line mast near), working console or not.</summary>
        public bool radio;
        public float range;
        public bool mast;
        public float tiles;
        public int at;
    }

    /// <summary>
    /// The diplomatic range until radio (the player, 8 November 2026: "some sort of diplomatic range ... until comms ...
    /// more difficult ... with bases further away or in difficult terrain"). Reach is the world's own travel time between the
    /// colony and the town (the game's caravan path and its costs: hills, mountains, swamp, ice and what can't be crossed;
    /// roads and rivers; the season and snow on the ground), at the carrier's means (PostMath.LegDays). Within the colony's
    /// range (PostMath.RangeDays, by its age, half again with a Signal Line mast near) the post, passing carriers and the
    /// colony's rider reach; beyond it, a relay through a nearer people's town on good terms (PostMath.Relay); else out of
    /// reach. A comms console removes the range for peoples with radio; a spacer colony's shuttles remove it for letters.
    /// Paths are cached for a few hours of the game and worked out a few a frame at most for the panels.
    /// </summary>
    public static class PostReach
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>How long a path's days are kept (game ticks): three hours, so the season and snow follow.</summary>
        private const int KeepTicks = 2500 * 3;

        private static readonly Dictionary<long, (float days, int at)> walks = new Dictionary<long, (float, int)>();
        private static readonly Dictionary<long, ReachInfo> infos = new Dictionary<long, ReachInfo>();
        private static int frame = -1, pathsThisFrame;
        /// <summary>A path was guessed from the straight distance (out of the frame's budget): what was worked out from it isn't kept.</summary>
        private static bool guessed;

        public static void Reset()
        {
            walks.Clear();
            infos.Clear();
            masts.Clear();
        }

        private static long Key(PlanetTile a, PlanetTile b) => ((long)a.tileId << 32) ^ (uint)b.tileId ^ ((long)(a.Layer?.LayerID ?? 0) << 56);

        /// <summary>A path may be worked out now: always on the game's own passes, a couple a frame from the windows.</summary>
        private static bool Budget(bool ui)
        {
            if (!ui) return true;
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                pathsThisFrame = 0;
            }
            if (pathsThisFrame >= 2) return false;
            pathsThisFrame++;
            return true;
        }

        /// <summary>
        /// A walking caravan's days from one tile to another over the world's path (the game's own path and estimate, as its
        /// route planner reads them: terrain, roads, rivers, the season, night's rest), cached; -1 when there is no way by land. Out of budget from a window: the
        /// straight distance at a walk, not cached.
        /// </summary>
        public static float WalkDays(PlanetTile from, PlanetTile to, bool ui = false)
        {
            if (!from.Valid || !to.Valid) return -1f;
            if (from == to) return 0f;
            if (from.Layer != to.Layer) return -1f;
            long key = Key(from, to);
            int now = Now;
            if (walks.TryGetValue(key, out var w) && now - w.at < KeepTicks && now >= w.at) return w.days;
            if (!Budget(ui))
            {
                guessed = true;
                return Straight(from, to);
            }
            float days;
            try
            {
                if (!Find.WorldReachability.CanReach(from, to)) days = -1f;
                else
                {
                    // The route planner's own way: the layer's path with no caravan, timed at a caravan's default pace from now
                    // (the season and snow on the way, night's rests).
                    var path = from.Layer.Pather.FindPath(from, to, null);
                    try
                    {
                        if (path == null || !path.Found) days = -1f;
                        else
                        {
                            int ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(from, to, path, 0f, CaravanTicksPerMoveUtility.DefaultTicksPerMove,
                                Find.TickManager.TicksAbs);
                            days = ticks > 0 ? ticks / (float)GenDate.TicksPerDay : Straight(from, to);
                        }
                    }
                    finally
                    {
                        path?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the world's path", e);
                days = Straight(from, to);
            }
            if (walks.Count > 600) walks.Clear();
            walks[key] = (days, now);
            return days;
        }

        /// <summary>The straight distance at a walking caravan's pace on easy land (LetterMath's runner without the start).</summary>
        private static float Straight(PlanetTile a, PlanetTile b) => LivingEvents.Distance(a, b) * 0.09f;

        // ---------------- The colony's side ----------------

        /// <summary>A Signal Line mast near the colony (one of the Line's towns within a mast's reach, or a mast of theirs raised at home), by name, softly.</summary>
        public static bool Mast(Map home)
        {
            if (home == null) return false;
            try
            {
                if (MastTiles(home.Tile) >= 0f) return true;
                return home.listerBuildings.allBuildingsColonist.Any(b => b.def.defName.IndexOf("Mast", StringComparison.Ordinal) >= 0
                                                                          && (b.def.defName.IndexOf("Signal", StringComparison.Ordinal) >= 0 || b.def.defName.IndexOf("Relay", StringComparison.Ordinal) >= 0));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The Signal Line (Peoples/, being built in parallel), by its def's name: null until it is in the game.</summary>
        private static bool SignalLine(Faction f) => f?.def?.defName != null && f.def.defName.StartsWith("Rimfolk_SignalLine", StringComparison.Ordinal) && !f.defeated;

        private static readonly Dictionary<long, (float tiles, float at)> masts = new Dictionary<long, (float, float)>();

        /// <summary>Tiles to the nearest town of the Signal Line within a mast's reach, or -1 (looked at every few seconds a tile).</summary>
        public static float MastTiles(PlanetTile at)
        {
            long key = Key(at, at);
            float t = Time.realtimeSinceStartup;
            if (masts.TryGetValue(key, out var m) && t - m.at < 10f && t >= m.at) return m.tiles;
            float d = MastTilesNow(at);
            if (masts.Count > 500) masts.Clear();
            masts[key] = (d, t);
            return d;
        }

        private static float MastTilesNow(PlanetTile at)
        {
            float best = -1f;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || !SignalLine(s.Faction) || s.Tile.Layer != at.Layer) continue;
                float d = LivingEvents.Distance(at, s.Tile);
                if (d <= PostMath.MastReach && (best < 0f || d < best)) best = d;
            }
            return best;
        }

        /// <summary>They have radio: of the industrial age or later, or their town within reach of the Signal Line's masts.</summary>
        public static bool Radio(Faction f, Settlement town)
        {
            if (f == null) return false;
            return PostMath.RadioReaches((int)Peoples.TechOf(f, TechLevel.Industrial), town != null ? MastTiles(town.Tile) : -1f);
        }

        /// <summary>The colony's means for its letters: by its age, mounted when it has an animal to ride.</summary>
        public static LetterMeans ColonyMeans(Map home) => PostMath.ColonyMeans(Ages.Ours, OwnRiders.MountFor(home, null) != null);

        // ---------------- A town ----------------

        /// <summary>
        /// Whether a town's reach is worked out and kept, rather than guessed from the straight distance while the paths wait
        /// for their frames (the Peoples view asks again until it is).
        /// </summary>
        public static bool Kept(Map home, Settlement town)
        {
            if (home == null || town == null) return true;
            int now = Now;
            return infos.TryGetValue(Key(home.Tile, town.Tile), out var have) && now - have.at < KeepTicks && now >= have.at;
        }

        /// <summary>Where a town lies for the colony's letters (cached a few hours; a couple of new ones a frame from a window).</summary>
        public static ReachInfo Of(Map home, Settlement town, bool ui = false)
        {
            if (home == null || town == null) return new ReachInfo { band = ReachBand.Cut };
            long key = Key(home.Tile, town.Tile);
            int now = Now;
            if (infos.TryGetValue(key, out var have) && now - have.at < KeepTicks && now >= have.at) return have;
            var info = new ReachInfo { at = now, tiles = LivingEvents.Distance(home.Tile, town.Tile) };
            guessed = false;
            try
            {
                info.mast = Mast(home);
                info.range = PostMath.RangeDays(Ages.Ours, info.mast);
                info.means = ColonyMeans(home);
                info.radio = Radio(town.Faction, town);
                info.call = info.radio && NeighbourLetters.Console(home) != null;
                info.walkDays = WalkDays(home.Tile, town.Tile, ui);
                info.days = PostMath.LegDays(info.walkDays, info.tiles, info.means);
                bool relay = false;
                if (info.range >= 0f && (info.days < 0f || info.days > info.range)) relay = FindRelay(home, town, info, ui);
                info.band = PostMath.Band(false, info.days, info.range, relay);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a town's reach", e);
                info.band = ReachBand.Near;
            }
            if (infos.Count > 300) infos.Clear();
            if (!guessed) infos[key] = info;
            return info;
        }

        /// <summary>
        /// The nearest-handed relay: towns of peoples on good terms with the colony (not at war, goodwill 20 or more), nearer
        /// than the letter's town, their way on by their own means; the fewest days in all (PostMath.Relay).
        /// </summary>
        private static bool FindRelay(Map home, Settlement town, ReachInfo info, bool ui)
        {
            var cands = Find.WorldObjects.Settlements
                .Where(s => s != null && s != town && !s.Destroyed && s.Faction != null && !s.Faction.IsPlayer && s.Tile.Layer == home.Tile.Layer
                            && !s.Faction.HostileTo(Faction.OfPlayer) && s.Faction.HasGoodwill && s.Faction.PlayerGoodwill >= 20
                            && !s.Faction.HostileTo(town.Faction ?? s.Faction)
                            && LivingEvents.Distance(home.Tile, s.Tile) < info.tiles && LivingEvents.Distance(s.Tile, town.Tile) < info.tiles)
                .OrderBy(s => LivingEvents.Distance(home.Tile, s.Tile) + LivingEvents.Distance(s.Tile, town.Tile)).Take(5).ToList();
            if (cands.Count == 0) return false;
            var opts = new List<RelayOption>();
            foreach (var s in cands)
            {
                float there = PostMath.LegDays(WalkDays(home.Tile, s.Tile, ui), LivingEvents.Distance(home.Tile, s.Tile), info.means);
                var theirs = PostMath.MeansOf(Ages.Of(s.Faction), false);
                float on = PostMath.LegDays(WalkDays(s.Tile, town.Tile, ui), LivingEvents.Distance(s.Tile, town.Tile), theirs);
                opts.Add(new RelayOption { id = s.ID, toRelay = there, onward = on, goodTerms = true });
            }
            int i = PostMath.Relay(opts, info.range, out float days);
            if (i < 0) return false;
            info.relay = cands[i];
            info.relayDays = days;
            return true;
        }

        /// <summary>
        /// Days a letter is on the road from the colony to a town by a carrier's means, the land's own way: relayed when it
        /// is far (there to the relay, a day there, and on by the relay's people), else straight there; -1 when no way.
        /// </summary>
        public static float Days(Map home, Settlement town, LetterMeans means, out Settlement relayedBy)
        {
            relayedBy = null;
            if (home == null || town == null) return -1f;
            var info = Of(home, town);
            if (info.band == ReachBand.Far && info.relay != null && means != LetterMeans.Shuttle)
            {
                relayedBy = info.relay;
                float there = PostMath.LegDays(WalkDays(home.Tile, info.relay.Tile), LivingEvents.Distance(home.Tile, info.relay.Tile), means);
                float on = PostMath.LegDays(WalkDays(info.relay.Tile, town.Tile), LivingEvents.Distance(info.relay.Tile, town.Tile),
                    PostMath.MeansOf(Ages.Of(info.relay.Faction), false));
                if (there >= 0f && on >= 0f) return there + PostMath.RelayDays + on;
            }
            float d = PostMath.LegDays(info.walkDays, info.tiles, means);
            return d >= 0f ? d : LetterMath.LegDays(info.tiles, means);
        }

        /// <summary>Days for the colony's own rider straight there (a mount if one is ridable): the land's own way, however far.</summary>
        public static float RiderDays(Map home, Settlement town, bool mounted)
        {
            if (home == null || town == null) return -1f;
            float walk = WalkDays(home.Tile, town.Tile, true);
            return PostMath.LegDays(walk, LivingEvents.Distance(home.Tile, town.Tile), mounted ? LetterMeans.Rider : LetterMeans.Runner);
        }
    }
}
