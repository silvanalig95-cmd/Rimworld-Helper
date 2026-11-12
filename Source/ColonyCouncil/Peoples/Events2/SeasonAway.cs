using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== The season away (round 2's shared shape: the pilgrimage, 14, and the league's guard, 3; later the pit's cave-in, 67, and the plague town, 98) =====================

    /// <summary>
    /// What one kind of season away is for (SeasonAway): the words as they get there, what they bring home, and what is told
    /// should it be cut short. Registered by its key in <see cref="SeasonAway.Kinds"/>.
    /// </summary>
    public abstract class AwayKind
    {
        public abstract string Key { get; }

        /// <summary>The colonist (and their beast) reached the place and stays there until the visit's due.</summary>
        public virtual void Arrived(Visit v, Map home, Faction f, string place)
        {
        }

        /// <summary>They are on the map at home again (the beast with them, if it lived): what the season brought.</summary>
        public abstract void Home(Visit v, Map home, Faction f, bool served);
    }

    /// <summary>
    /// A colonist away for a season: release F's conscript made general. They walk to the map's edge (a beast at their heels,
    /// or under them with Giddy-Up) and set out as a caravan of their own on the game's own roads to a place on the world (a
    /// town or a tile); there they are kept in the world for their days; then a caravan of their own brings them home by the
    /// same roads. Whether they come back is the world's: the roads' ambushes and weather are the game's own, nothing of
    /// Rimfolk's hurts them on the way. One of the events' visits (kind "pev:away"): x the colonist, a their beast, its text
    /// "people|kind|tile|place|days", its stake the stage, its due the end of their days there.
    /// </summary>
    public static class SeasonAway
    {
        public const string Kind = "pev:away";

        public static readonly Dictionary<string, AwayKind> Kinds = new Dictionary<string, AwayKind>
        {
            [Pilgrimage.AwayKey] = new PilgrimAway(),
            [LeagueGuard.AwayKey] = new GuardAway()
        };

        private static int Now => Find.TickManager.TicksGame;

        public static JobDef OutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv2_SetOut");

        private static List<Visit> Visits => CouncilGameComponent.Instance?.happenings?.visits;

        /// <summary>This colonist is away on a season now (any stage).</summary>
        public static bool AwayNow(Pawn p) => p != null && (Visits?.Any(v => v != null && v.kind == Kind && !v.done && v.x == p) ?? false);

        /// <summary>A season of this kind under way now.</summary>
        public static Visit Running(string key) => Visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && KeyOf(v) == key);

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        public static string KeyOf(Visit v)
        {
            var p = Parts(v);
            return p.Length > 1 ? p[1] : null;
        }

        public static PlanetTile TileOf(Visit v)
        {
            var p = Parts(v);
            return p.Length > 2 && PlanetTile.TryParse(p[2], out var t) ? t : PlanetTile.Invalid;
        }

        public static string PlaceOf(Visit v)
        {
            var p = Parts(v);
            return p.Length > 3 ? p[3] : "?";
        }

        public static float DaysOf(Visit v)
        {
            var p = Parts(v);
            return p.Length > 4 && float.TryParse(p[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 3f;
        }

        public static AwayKind HandlerOf(Visit v) => KeyOf(v) is string k && Kinds.TryGetValue(k, out var h) ? h : null;

        /// <summary>Days on the road from the home to a tile, roughly (a walking caravan of one).</summary>
        public static float RoadDays(Map home, PlanetTile tile) =>
            home == null || !tile.Valid ? 2f : PeopleEvents2Math.RoadDays(Find.WorldGrid.ApproxDistanceInTiles(home.Tile, tile));

        /// <summary>
        /// Sent: food for the road in their pack, a walk to the map's edge (their beast at heel, or ridden with Giddy-Up), the
        /// visit begun. <paramref name="days"/> is how long they stay at <paramref name="place"/>.
        /// </summary>
        public static Visit Send(string key, IncidentDef incident, Pawn p, Pawn beast, Faction f, PlanetTile tile, string place, float days, Map home)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var job = OutJob;
            if (t == null || p == null || f == null || !tile.Valid || home == null || job == null || !p.Spawned || AwayNow(p) || !Kinds.ContainsKey(key)) return null;
            if (!RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return null;
            var v = t.AddVisit(Kind, incident, home, p, beast, null, Now + PeopleEvents2Math.AwayMaxDays * GenDate.TicksPerDay);
            v.text = f.GetUniqueLoadID() + "|" + key + "|" + tile + "|" + (place ?? "?").Replace("|", "/") + "|" + days.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            v.stake = PeopleEvents2Math.SettingOut;
            WorldLedger.Keep(p, KeepFor.Lent, false);
            try
            {
                Hosting.Provision(p, RoadDays(home, tile) + 1f);
                if (p.Drafted) p.drafter.Drafted = false;
                bool riding = beast != null && beast.Spawned && beast.Map == p.Map && GiddyUpMounts.On && GiddyUpMounts.Ridable(beast.def) && beast.RaceProps.baseBodySize >= OwnRiders.MinMountSize
                              && GiddyUpMounts.MountNow(p, beast);
                if (beast != null && beast.Spawned && !riding && beast.jobs != null)
                {
                    var follow = JobMaker.MakeJob(JobDefOf.FollowClose, p);
                    follow.followRadius = 3f;
                    follow.expiryInterval = 6 * GenDate.TicksPerHour;
                    follow.checkOverrideOnExpire = false;
                    beast.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                }
                var walk = JobMaker.MakeJob(job, edge);
                walk.locomotionUrgency = Pace.Errand;
                p.jobs.StartJob(walk, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: setting out", e);
            }
            return v;
        }

        /// <summary>At the map's edge (JobDriver_SetOut): a caravan of their own, with their beast if it kept up, on the road to the place.</summary>
        public static void AtEdge(Pawn p)
        {
            var v = Visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.x == p && x.stake == PeopleEvents2Math.SettingOut);
            var map = v?.map;
            var tile = TileOf(v);
            if (v == null || map == null || !tile.Valid || !p.Spawned) return;
            try
            {
                var pawns = new List<Pawn> { p };
                var beast = v.a;
                if (beast != null && !beast.Dead && !beast.Downed && beast.Spawned && beast.Map == p.Map && beast.Position.InHorDistOf(p.Position, 15f)) pawns.Add(beast);
                else v.a = null;
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(pawns, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(tile, null, true);
                v.stake = PeopleEvents2Math.Going;
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv2_SetOut".Translate(p.Named("PAWN"), PlaceOf(v).Named("PLACE")).Resolve(),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the caravan of one", e);
            }
        }

        /// <summary>The season's hour, stage by stage.</summary>
        public static bool Hour(Visit v, int now)
        {
            var p = v.x;
            var f = PeoplesF.PeopleOf(v);
            var tile = TileOf(v);
            var map = v.map;
            if (p == null || p.Discarded)
            {
                End(v);
                return false;
            }
            if (p.Dead)
            {
                // Lost on the world's roads (the game's own ambush or weather): told, and the season ends.
                Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv2_AwayLostLabel".Translate(p.LabelShort).Resolve(),
                    "CC_PEv2_AwayLost".Translate(p.Named("PAWN"), PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(), LetterDefOf.Death, LookTargets.Invalid, f);
                End(v);
                return false;
            }
            switch (v.stake)
            {
                case PeopleEvents2Math.SettingOut:
                    if (p.Spawned && p.CurJobDef != OutJob)
                    {
                        // The player gave them other work before they reached the edge: they stayed.
                        Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv2_Stayed".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                        End(v);
                        return false;
                    }
                    return true;
                case PeopleEvents2Math.Going:
                {
                    var c = p.GetCaravan();
                    if (c == null)
                    {
                        // Turned for home by the player, or carried off: the season is off.
                        if (p.Spawned || People.Kidnapped(p))
                        {
                            End(v);
                            return false;
                        }
                        return true;
                    }
                    if (!tile.Valid || f == null || f.HostileTo(Faction.OfPlayer))
                    {
                        End(v);
                        return false;
                    }
                    bool there = c.Tile == tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, tile) <= 1.01f;
                    if (there) Arrived(v, c, f);
                    else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(tile, null, true);
                    return true;
                }
                case PeopleEvents2Math.There:
                    if (f == null || f.defeated || f.HostileTo(Faction.OfPlayer) || now >= v.due) HomeFrom(v, f, map, now >= v.due);
                    return true;
                case PeopleEvents2Math.Coming:
                    if (map != null && p.Spawned && p.Map == map) Back(v, f, map);
                    else if (p.GetCaravan() == null && !p.Spawned)
                    {
                        End(v);
                        return false;
                    }
                    else if (p.Spawned && Homes.IsHome(p.Map)) Back(v, f, p.Map);
                    return !v.done;
            }
            return false;
        }

        private static void Keep(Pawn p)
        {
            if (p == null || p.Dead) return;
            if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            WorldLedger.Keep(p, KeepFor.Lent, false);
        }

        private static void Arrived(Visit v, Caravan c, Faction f)
        {
            var p = v.x;
            var beast = v.a != null && c.PawnsListForReading.Contains(v.a) ? v.a : null;
            c.RemovePawn(p);
            if (beast != null) c.RemovePawn(beast);
            if (!c.PawnsListForReading.Any(x => x != null && x.IsColonist)) c.Destroy();
            Keep(p);
            Keep(beast);
            v.a = beast;
            v.stake = PeopleEvents2Math.There;
            v.due = Now + Mathf.RoundToInt(DaysOf(v) * GenDate.TicksPerDay);
            v.until = Mathf.Max(v.until, v.due + 60 * GenDate.TicksPerDay);
            try
            {
                HandlerOf(v)?.Arrived(v, v.map, f, PlaceOf(v));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: arrived on a season away", e);
            }
        }

        /// <summary>Their days there done (or cut short): food for the road, and a caravan of their own for home with their beast.</summary>
        private static void HomeFrom(Visit v, Faction f, Map map, bool served)
        {
            var p = v.x;
            var beast = v.a != null && !v.a.Dead && !v.a.Discarded ? v.a : null;
            var tile = TileOf(v);
            PlanetTile from = tile.Valid ? tile : p.Tile;
            try
            {
                Hosting.Provision(p, RoadDays(map, tile) + 1f);
                var pawns = new List<Pawn> { p };
                if (beast != null) pawns.Add(beast);
                foreach (var x in pawns)
                    if (!Find.WorldPawns.Contains(x))
                        Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.KeepForever);
                var c = CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, from, false);
                if (map?.Parent != null) c.pather.StartPath(map.Tile, new CaravanArrivalAction_Enter(map.Parent), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the road home", e);
            }
            foreach (var x in new[] { p, beast })
            {
                if (x == null) continue;
                WorldLedger.Release(x, KeepFor.Lent);
                Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            }
            v.stake = PeopleEvents2Math.Coming;
            // Kept for the homecoming: whether their days there were served.
            v.skill = served ? SkillDefOf.Social : null;
            Tell.Message(NewsKind.World, Importance.Notable, map,
                (served ? "CC_PEv2_Homeward" : "CC_PEv2_HomewardEarly").Translate(p.Named("PAWN"), PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                p.GetCaravan() != null ? new LookTargets(p.GetCaravan()) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Home again: what the season brought, by its kind.</summary>
        private static void Back(Visit v, Faction f, Map map)
        {
            v.done = true;
            try
            {
                HandlerOf(v)?.Home(v, map, f, v.skill != null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: home from a season away", e);
            }
            End(v);
        }

        private static void End(Visit v)
        {
            v.done = true;
            if (v.x != null) WorldLedger.Release(v.x, KeepFor.Lent);
            if (v.a != null) WorldLedger.Release(v.a, KeepFor.Lent);
        }

        /// <summary>Dev mode: their days at the place are up now.</summary>
        public static bool CutShortNow()
        {
            var v = Visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.stake == PeopleEvents2Math.There);
            if (v == null) return false;
            v.due = Now;
            return true;
        }
    }

    /// <summary>The colonist walks to the map's edge (target A) and sets out as a caravan of their own (SeasonAway.AtEdge).</summary>
    public class JobDriver_SetOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("SeasonAwayOut");
            go.initAction = () => SeasonAway.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
