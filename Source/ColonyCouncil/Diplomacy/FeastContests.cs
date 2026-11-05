using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A feast's contests on the map (Feasts.cs; docs/design/stage2-diplomacy.md, item 5; decision 7), one at a time, each
    /// between the colony's people and the guests:
    /// <list type="bullet">
    /// <item>Wrestling: up to three bouts by the sparring rules (Outsiders' own bout: eight swings each, or until one yields
    /// to the pain) in a ring by the gate; one blow in a bout goes in hard one time in 25 under the harm rule's shared cap.</item>
    /// <item>A race: runners from a line at the gate to a marker out on open ground and back at a sprint; one may stumble at
    /// the turn. Children run races (and only races).</item>
    /// <item>A hunt: the colony's hunters and the guests with bows or guns go after safe game on the colony's land (the
    /// calendar hunt's rules: nothing likely to turn on them, beyond the game's own chance), counted by kills.</item>
    /// <item>A game at the tables: the game's own recreation (chess, poker, billiards, horseshoes, hoopstone, or a mod's
    /// game by its kind) between one of the colony's and a guest, won by the game's own skill for it and some luck.</item>
    /// <item>A shooting match, only where a mod gives targets (read by name): five shots each, hits by the game's own chance.</item>
    /// </list>
    /// What is under way is the session's (not saved): a contest broken up by a reload is let go as such.
    /// </summary>
    public static class FeastContests
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private sealed class Run
        {
            public int plan;
            public ContestKind kind;
            public Map map;
            public int started;
            public IntVec3 spot = IntVec3.Invalid;
            public readonly HashSet<Pawn> entrants = new HashSet<Pawn>();
            // Wrestling.
            public readonly List<(Pawn c, Pawn x)> pairs = new List<(Pawn, Pawn)>();
            public int index = -1, pause;
            public OutsiderBout bout;
            public readonly Dictionary<Pawn, int> wins = new Dictionary<Pawn, int>(), hits = new Dictionary<Pawn, int>();
            // The race.
            public IntVec3 marker = IntVec3.Invalid;
            public readonly HashSet<Pawn> atLine = new HashSet<Pawn>();
            public int go = -1;
            public readonly Dictionary<Pawn, int> finish = new Dictionary<Pawn, int>();
            // The hunt.
            public readonly List<Pawn> game = new List<Pawn>();
            public readonly Dictionary<Pawn, int> killsAt = new Dictionary<Pawn, int>(), firstKill = new Dictionary<Pawn, int>();
            // The tables.
            public readonly List<(Pawn a, Pawn b, Building table)> games = new List<(Pawn, Pawn, Building)>();
            public int until = -1;
            // The shooting match.
            public Thing target;
            public IntVec3 stand = IntVec3.Invalid;
            public readonly List<Pawn> shooters = new List<Pawn>();
            public int shooter = -1;
            public readonly Dictionary<Pawn, int> scored = new Dictionary<Pawn, int>();
        }

        private static readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
        private static Game runsGame;

        private static Dictionary<int, Run> Runs
        {
            get
            {
                if (runsGame != Current.Game)
                {
                    runsGame = Current.Game;
                    runs.Clear();
                }
                return runs;
            }
        }

        private static Run RunOf(Pawn p)
        {
            if (p == null) return null;
            foreach (var r in Runs.Values)
                if (r.entrants.Contains(p))
                    return r;
            return null;
        }

        /// <summary>Taking part in a contest now (the feast's look leaves them to it).</summary>
        public static bool InContest(Pawn p) => RunOf(p) != null;

        // ================= Who and where =================

        private static readonly ulong NoHuntMask = BeliefMath.Mask(new[] { Belief.NoHunting, Belief.NoViolence });
        private static readonly ulong NoViolenceMask = BeliefMath.Mask(new[] { Belief.NoViolence });

        private static bool Able(Pawn p) =>
            p != null && Together.Able(p) && !p.DevelopmentalStage.Baby() && PlayOuts.CanJoin(p) && !PlayOuts.NeedsPress(p) && !InContest(p)
            && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false);

        private static bool Pregnant(Pawn p) => p?.health?.hediffSet?.hediffs?.Any(h => h.def?.defName != null && h.def.defName.StartsWith("Pregnant")) ?? false;

        /// <summary>Of the colony's people, those who may take part in this contest now, the best for it first.</summary>
        private static List<Pawn> Ours(ContestKind k, Map map, Building table = null)
        {
            var all = map.mapPawns.FreeColonistsSpawned.Where(p => Able(p) && (PlayOuts.Interruptible(p) || p.CurJob?.def?.joyKind != null)).ToList();
            return Fit(k, all, table, true);
        }

        /// <summary>Of the guests, those who may take part.</summary>
        private static List<Pawn> Theirs(GuestFeast p, ContestKind k, Map map, Building table = null) =>
            Fit(k, p.AllGuests.Where(x => x.Spawned && x.Map == map && Able(x) && !x.Drafted).ToList(), table, false);

        private static List<Pawn> Fit(ContestKind k, List<Pawn> all, Building table, bool ours)
        {
            switch (k)
            {
                case ContestKind.Wrestling:
                    return all.Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && !Pregnant(p) && (!ours || !Beliefs.ForbidsAny(p, NoViolenceMask))
                                          && p.health.summaryHealth.SummaryHealthPercent > 0.85f)
                        .OrderByDescending(Outsiders.Melee).ToList();
                case ContestKind.Race:
                    return all.Where(p => !Pregnant(p) && p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) >= 0.6f && p.health.summaryHealth.SummaryHealthPercent > 0.8f)
                        .OrderByDescending(p => p.GetStatValue(StatDefOf.MoveSpeed)).ToList();
                case ContestKind.Hunt:
                    return all.Where(p => !p.DevelopmentalStage.Juvenile() && GamePrey.CanHunt(p) && (!ours || !Beliefs.ForbidsAny(p, NoHuntMask)))
                        .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).ToList();
                case ContestKind.Tables:
                    return all.Where(p => !p.DevelopmentalStage.Juvenile() && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                        .OrderByDescending(p => TableSkill(p, table)).ToList();
                case ContestKind.Shooting:
                    return all.Where(p => !p.DevelopmentalStage.Juvenile() && p.equipment?.Primary != null && p.equipment.Primary.def.IsRangedWeapon
                                          && !p.WorkTagIsDisabled(WorkTags.Violent) && (!ours || !Beliefs.ForbidsAny(p, NoViolenceMask)))
                        .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).ToList();
                default:
                    return new List<Pawn>();
            }
        }

        /// <summary>A ring for the bouts: open, unroofed ground near the gate with room all round it, nobody on it.</summary>
        private static IntVec3 Ring(Map map)
        {
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            foreach (var cell in GenRadial.RadialCellsAround(gate, 10f, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Roofed(map) || cell.Fogged(map) || cell.GetFirstPawn(map) != null || (cell.GetTerrain(map)?.IsWater ?? false)) continue;
                bool room = true;
                foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)))
                    if (!n.InBounds(map) || !n.Standable(map))
                    {
                        room = false;
                        break;
                    }
                if (room) return cell;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The race's turn: open ground out from the gate (away from the colony), as far as can be had.</summary>
        private static IntVec3 Marker(Map map, IntVec3 gate)
        {
            if (!gate.IsValid) return IntVec3.Invalid;
            var center = TemplatePlanner.ColonyCenter(map);
            var dirs = new List<IntVec3>();
            var out1 = new IntVec3(Math.Sign(gate.x - center.x), 0, Math.Sign(gate.z - center.z));
            if (out1.x != 0 || out1.z != 0) dirs.Add(out1);
            dirs.AddRange(new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West });
            foreach (var d in dirs)
                for (int n = FeastMath.MarkerFar; n >= FeastMath.MarkerNear; n -= 2)
                {
                    var c = gate + new IntVec3(d.x * n, 0, d.z * n);
                    if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || (c.GetTerrain(map)?.IsWater ?? false) || c.Roofed(map)) continue;
                    if (map.reachability.CanReach(gate, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors))) return c;
                }
            return IntVec3.Invalid;
        }

        /// <summary>The colony's game tables: a recreation building of a game's kind that the game's own play jobs can use.</summary>
        private static List<Building> Tables(Map map)
        {
            var list = new List<Building>();
            if (map == null) return list;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                var kind = b?.def?.building?.joyKind?.defName;
                if (b == null || !b.Spawned || kind == null || !kind.StartsWith("Gaming", StringComparison.Ordinal) || b.IsForbidden(Faction.OfPlayer)) continue;
                if (b.TryGetComp<CompPowerTrader>() is CompPowerTrader power && !power.PowerOn) continue;
                if (GiverOf(b.def) != null) list.Add(b);
            }
            return list;
        }

        private static JoyGiverDef GiverOf(ThingDef def) =>
            DefDatabase<JoyGiverDef>.AllDefsListForReading.FirstOrDefault(g => g?.jobDef != null && g.thingDefs != null && g.thingDefs.Contains(def)
                                                                               && (g.Worker is JoyGiver_InteractBuildingSitAdjacent || g.Worker is JoyGiver_InteractBuildingInteractionCell
                                                                                   || g.Worker is JoyGiver_PlayBilliards || g.Worker is JoyGiver_WatchBuilding));

        /// <summary>The game's own skill for a game (its play job's), else Intellectual.</summary>
        private static int TableSkill(Pawn p, Building table)
        {
            var skill = table != null ? GiverOf(table.def)?.jobDef?.joySkill : null;
            return p?.skills?.GetSkill(skill ?? SkillDefOf.Intellectual)?.Level ?? 0;
        }

        /// <summary>The colony's targets to shoot at, read by name (a mod's: Misc. Training, Combat Training).</summary>
        private static List<Building> Targets(Map map) =>
            map?.listerBuildings.allBuildingsColonist.Where(b => b != null && b.Spawned && FeastMath.IsTarget(b.def.defName, b.def.label)).ToList() ?? new List<Building>();

        /// <summary>Where a shooter stands: ten cells or so from the target, with a clear line to it, reachable.</summary>
        private static IntVec3 Stand(Map map, Thing target)
        {
            foreach (var c in GenRadial.RadialCellsAround(target.Position, 12f, false).Where(c => c.DistanceTo(target.Position) >= 7f).OrderByDescending(c => c.DistanceTo(target.Position)))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetFirstPawn(map) != null) continue;
                if (!GenSight.LineOfSight(c, target.Position, map, true)) continue;
                if (!map.reachability.CanReachColony(c)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>Why the colony can't hold this contest at this home now, or null.</summary>
        public static string WhyNot(ContestKind k, Map home)
        {
            if (home == null) return FeastWords.P("CC_Feast_Why_NoHome");
            try
            {
                switch (k)
                {
                    case ContestKind.Wrestling:
                        return Fit(k, home.mapPawns.FreeColonistsSpawned.ToList(), null, true).Count == 0 ? FeastWords.P("CC_Feast_CWhy_Wrestling") : null;
                    case ContestKind.Race:
                        if (Fit(k, home.mapPawns.FreeColonistsSpawned.ToList(), null, true).Count == 0) return FeastWords.P("CC_Feast_CWhy_Race");
                        return Marker(home, PlaySpots.GateCell(home, null)).IsValid ? null : FeastWords.P("CC_Feast_CWhy_RaceGround");
                    case ContestKind.Hunt:
                        if (Fit(k, home.mapPawns.FreeColonistsSpawned.ToList(), null, true).Count == 0) return FeastWords.P("CC_Feast_CWhy_Hunters");
                        return home.mapPawns.AllPawnsSpawned.Count(a => PlayHunt.Safe(a, home)) < 2 ? FeastWords.P("CC_Feast_CWhy_Game") : null;
                    case ContestKind.Tables:
                        return Tables(home).Count == 0 ? FeastWords.P("CC_Feast_CWhy_Tables") : null;
                    case ContestKind.Shooting:
                        if (Targets(home).Count == 0) return FeastWords.P("CC_Feast_CWhy_Targets");
                        return Fit(k, home.mapPawns.FreeColonistsSpawned.ToList(), null, true).Count == 0 ? FeastWords.P("CC_Feast_CWhy_Guns") : null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: can a contest be held", e);
            }
            return null;
        }

        // ================= Starting one =================

        /// <summary>The contest begins: who takes part, where, and their first steps on the map. Returns why not, or null.</summary>
        public static string Start(GuestFeast p, ContestKind k, Map map)
        {
            Stop(p);
            var r = new Run { plan = p.id, kind = k, map = map, started = Now };
            // Known before anyone is sent: their jobs ask for it from their first tick.
            Runs[p.id] = r;
            string why;
            try
            {
                switch (k)
                {
                    case ContestKind.Wrestling:
                        why = StartWrestling(p, r);
                        break;
                    case ContestKind.Race:
                        why = StartRace(p, r);
                        break;
                    case ContestKind.Hunt:
                        why = StartHunt(p, r);
                        break;
                    case ContestKind.Tables:
                        why = StartTables(p, r);
                        break;
                    default:
                        why = StartShooting(p, r);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a contest begins", e);
                why = FeastWords.P("CC_Feast_Off_Nobody");
            }
            if (why != null)
            {
                Runs.Remove(p.id);
                Release(r);
                return why;
            }
            if (r.spot.IsValid) Feasts.Focus(map, p, r.spot, 7f);
            // apart: the contest's start told in a message; it is on the map from here.
            Tell.Message(NewsKind.World, Importance.Notable, map, FeastWords.P("CC_Feast_Msg_Begins_" + k, r.entrants.Count.Named("COUNT")),
                r.spot.IsValid ? new LookTargets(new TargetInfo(r.spot, map)) : new LookTargets(r.entrants.Where(x => x.Spawned).Take(3)), MessageTypeDefOf.NeutralEvent);
            Watchers(p, r);
            return null;
        }

        private static string StartWrestling(GuestFeast p, Run r)
        {
            var map = r.map;
            r.spot = Ring(map);
            if (!r.spot.IsValid) return FeastWords.P("CC_Feast_Off_NoRing");
            var ours = Ours(ContestKind.Wrestling, map);
            var theirs = Theirs(p, ContestKind.Wrestling, map);
            int n = Math.Min(FeastMath.MaxBouts, Math.Min(ours.Count, theirs.Count));
            if (n == 0) return FeastWords.P("CC_Feast_Off_Nobody");
            for (int i = 0; i < n; i++)
            {
                r.pairs.Add((ours[i], theirs[i]));
                r.entrants.Add(ours[i]);
                r.entrants.Add(theirs[i]);
            }
            return null;
        }

        private static string StartRace(GuestFeast p, Run r)
        {
            var map = r.map;
            var gate = PlaySpots.GateCell(map, null);
            r.marker = Marker(map, gate);
            if (!gate.IsValid || !r.marker.IsValid) return FeastWords.P("CC_Feast_CWhy_RaceGround");
            r.spot = gate;
            var ours = Ours(ContestKind.Race, map).Take(4).ToList();
            var theirs = Theirs(p, ContestKind.Race, map).Take(FeastMath.MaxRunners - ours.Count).ToList();
            var runners = ours.Concat(theirs).ToList();
            if (ours.Count == 0 || theirs.Count == 0) return FeastWords.P("CC_Feast_Off_Nobody");
            // Places on a line across the way out, either side of the gate.
            var dir = r.marker - gate;
            var across = Math.Abs(dir.x) >= Math.Abs(dir.z) ? IntVec3.North : IntVec3.East;
            var cells = new List<IntVec3>();
            for (int i = 0; cells.Count < runners.Count && i < 20; i++)
            {
                int k = (i + 1) / 2 * (i % 2 == 0 ? 1 : -1);
                var c = gate + new IntVec3(across.x * k, 0, across.z * k);
                if (c.InBounds(map) && c.Standable(map) && map.reachability.CanReach(c, r.marker, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))) cells.Add(c);
            }
            for (int i = 0; i < runners.Count && i < cells.Count; i++)
            {
                var x = runners[i];
                var job = JobMaker.MakeJob(FeastDefOf.Rimfolk_FeastRun, r.marker, cells[i]);
                job.expiryInterval = -1;
                r.entrants.Add(x);
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (x.CurJobDef != FeastDefOf.Rimfolk_FeastRun) r.entrants.Remove(x);
            }
            if (!r.entrants.Any(x => x.Faction == Faction.OfPlayer) || !r.entrants.Any(x => x.Faction != Faction.OfPlayer)) return FeastWords.P("CC_Feast_Off_Nobody");
            return null;
        }

        private static string StartHunt(GuestFeast p, Run r)
        {
            var map = r.map;
            var ours = Ours(ContestKind.Hunt, map).Take(3).ToList();
            var theirs = Theirs(p, ContestKind.Hunt, map).Take(FeastMath.MaxHunters - ours.Count).ToList();
            if (ours.Count == 0 || theirs.Count == 0) return FeastWords.P("CC_Feast_Off_Nobody");
            var game = PlayHunt.ChooseGame(map, null, null, FeastMath.HuntGame(ours.Count + theirs.Count), false);
            if (game.Count == 0) return FeastWords.P("CC_Feast_CWhy_Game");
            var dm = map.designationManager;
            foreach (var a in game)
            {
                if (dm.DesignationOn(a, DesignationDefOf.Hunt) == null) dm.AddDesignation(new Designation(a, DesignationDefOf.Hunt));
                r.game.Add(a);
            }
            foreach (var x in ours.Concat(theirs))
            {
                r.entrants.Add(x);
                r.killsAt[x] = x.records?.GetAsInt(RecordDefOf.KillsAnimals) ?? 0;
            }
            r.spot = PlaySpots.GateCell(map, null);
            Hunt(r);
            return null;
        }

        private static string StartTables(GuestFeast p, Run r)
        {
            var map = r.map;
            var tables = Tables(map);
            if (tables.Count == 0) return FeastWords.P("CC_Feast_CWhy_Tables");
            var usedOurs = new HashSet<Pawn>();
            var usedTheirs = new HashSet<Pawn>();
            foreach (var t in tables.OrderByDescending(t => GiverOf(t.def)?.jobDef?.joyMaxParticipants ?? 1))
            {
                if (r.games.Count >= FeastMath.MaxTablePairs) break;
                var a = Ours(ContestKind.Tables, map, t).FirstOrDefault(x => !usedOurs.Contains(x));
                var b = Theirs(p, ContestKind.Tables, map, t).FirstOrDefault(x => !usedTheirs.Contains(x));
                if (a == null || b == null) break;
                int max = GiverOf(t.def)?.jobDef?.joyMaxParticipants ?? 1;
                // A game for one at a time (horseshoes, hoopstone): the guest plays at another of the same kind, if there is one.
                var t2 = max >= 2 ? t : tables.FirstOrDefault(o => o != t && o.def == t.def && r.games.All(g => g.table != o));
                if (t2 == null) continue;
                var ja = TableJob(a, t);
                var jb = TableJob(b, t2);
                if (ja == null || jb == null) continue;
                a.jobs.StartJob(ja, JobCondition.InterruptForced, null, false, true);
                b.jobs.StartJob(jb, JobCondition.InterruptForced, null, false, true);
                if (a.CurJobDef != ja.def || b.CurJobDef != jb.def)
                {
                    if (a.CurJob == ja) a.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    if (b.CurJob == jb) b.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    continue;
                }
                usedOurs.Add(a);
                usedTheirs.Add(b);
                r.games.Add((a, b, t));
                r.entrants.Add(a);
                r.entrants.Add(b);
                if (!r.spot.IsValid) r.spot = t.Position;
            }
            if (r.games.Count == 0) return FeastWords.P("CC_Feast_Off_Nobody");
            r.until = Now + FeastMath.TableTicks;
            return null;
        }

        /// <summary>The game's own play job at a table, for one who plays until the contest says so (a guest has no recreation need to end it).</summary>
        private static Job TableJob(Pawn p, Building t)
        {
            var giver = GiverOf(t.def);
            if (giver == null || p == null) return null;
            Job job = null;
            switch (giver.Worker)
            {
                case JoyGiver_InteractBuildingSitAdjacent _:
                    foreach (var c in GenAdjFast.AdjacentCellsCardinal(t).InRandomOrder())
                    {
                        if (!c.InBounds(t.Map) || !p.CanReserveSittableOrSpot(c)) continue;
                        var edifice = c.GetEdifice(t.Map);
                        if (giver.requireChair && (edifice == null || !edifice.def.building.isSittable)) continue;
                        job = JobMaker.MakeJob(giver.jobDef, t, c);
                        break;
                    }
                    break;
                case JoyGiver_InteractBuildingInteractionCell _:
                    if (t.InteractionCell.Standable(t.Map) && p.CanReserveSittableOrSpot(t.InteractionCell)) job = JobMaker.MakeJob(giver.jobDef, t, t.InteractionCell);
                    break;
                case JoyGiver_PlayBilliards _:
                    if (JoyGiver_PlayBilliards.ThingHasStandableSpaceOnAllSides(t)) job = JobMaker.MakeJob(giver.jobDef, t);
                    break;
                case JoyGiver_WatchBuilding _:
                    if (WatchBuildingUtility.TryFindBestWatchCell(t, p, giver.desireSit, out var cell, out var chair)) job = JobMaker.MakeJob(giver.jobDef, t, cell, chair);
                    break;
            }
            if (job == null || !p.CanReserve(t, giver.jobDef.joyMaxParticipants)) return null;
            job.doUntilGatheringEnded = true;
            job.ignoreJoyTimeAssignment = true;
            job.expiryInterval = FeastMath.TableTicks;
            return job;
        }

        private static string StartShooting(GuestFeast p, Run r)
        {
            var map = r.map;
            var target = Targets(map).FirstOrDefault(t => Stand(map, t).IsValid);
            if (target == null) return FeastWords.P("CC_Feast_CWhy_Targets");
            r.target = target;
            r.stand = Stand(map, target);
            r.spot = r.stand;
            var ours = Ours(ContestKind.Shooting, map).Take(3).ToList();
            var theirs = Theirs(p, ContestKind.Shooting, map).Take(FeastMath.MaxShooters - ours.Count).ToList();
            if (ours.Count == 0 || theirs.Count == 0) return FeastWords.P("CC_Feast_Off_Nobody");
            // Turn about: theirs, then ours.
            int n = Math.Max(ours.Count, theirs.Count);
            for (int i = 0; i < n; i++)
            {
                if (i < theirs.Count) r.shooters.Add(theirs[i]);
                if (i < ours.Count) r.shooters.Add(ours[i]);
            }
            foreach (var x in r.shooters) r.entrants.Add(x);
            NextShooter(r);
            return null;
        }

        /// <summary>The guests not taking part, and two or three of the colony's with nothing pressing, come and watch.</summary>
        private static void Watchers(GuestFeast p, Run r)
        {
            if (!r.spot.IsValid || r.kind == ContestKind.Hunt || r.kind == ContestKind.Tables) return;
            var map = r.map;
            var taken = new List<IntVec3>();
            int i = 3;
            var watchers = p.AllGuests.Where(x => !r.entrants.Contains(x) && x.Spawned && Feasts.Free(x))
                .Concat(map.mapPawns.FreeColonistsSpawned.Where(c => !r.entrants.Contains(c) && Together.Able(c) && !c.DevelopmentalStage.Baby() && PlayOuts.CanJoin(c)
                                                                     && PlayOuts.Interruptible(c) && !PlayOuts.NeedsPress(c) && c.Position.InHorDistOf(r.spot, 40f)).Take(3)).ToList();
            foreach (var w in watchers)
            {
                var at = PlaySpots.RingCell(map, r.spot, i++, w, taken);
                if (!at.IsValid || !w.CanReach(at, PathEndMode.OnCell, Danger.Some)) continue;
                taken.Add(at);
                var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutAttend, at, r.spot);
                job.expiryInterval = 2 * GenDate.TicksPerHour;
                w.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }

        // ================= Going on =================

        /// <summary>The contest's look (every 250 ticks): the next bout, the next shooter, game for the hunters; its result once it's over, else null.</summary>
        public static FeastResult Step(GuestFeast p, Map map)
        {
            if (!Runs.TryGetValue(p.id, out var r))
                return new FeastResult { kind = p.contests[Math.Min(p.contest, p.contests.Count - 1)], off = true, why = FeastWords.P("CC_Feast_Off_BrokeUp") };
            try
            {
                switch (r.kind)
                {
                    case ContestKind.Wrestling:
                        return Wrestling(p, r);
                    case ContestKind.Race:
                        return Race(p, r, false);
                    case ContestKind.Hunt:
                        return HuntStep(p, r, false);
                    case ContestKind.Tables:
                        return TablesStep(p, r, false);
                    default:
                        return ShootingStep(p, r, false);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a contest goes on", e);
                Stop(p);
                return new FeastResult { kind = r.kind, off = true, why = FeastWords.P("CC_Feast_Off_BrokeUp") };
            }
        }

        /// <summary>Time is up: the result from what is there (a race's finishers, the kills so far, the games as they stand), or null.</summary>
        public static FeastResult Finish(GuestFeast p, Map map, bool timeUp)
        {
            if (!Runs.TryGetValue(p.id, out var r)) return null;
            FeastResult result = null;
            try
            {
                switch (r.kind)
                {
                    case ContestKind.Race:
                        result = Race(p, r, true);
                        break;
                    case ContestKind.Hunt:
                        result = HuntStep(p, r, true);
                        break;
                    case ContestKind.Tables:
                        result = TablesStep(p, r, true);
                        break;
                    case ContestKind.Shooting:
                        result = ShootingStep(p, r, true);
                        break;
                    default:
                        result = WrestlingResult(p, r);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a contest's end", e);
            }
            Stop(p);
            return result;
        }

        /// <summary>Whatever is under way stops: jobs of the contest end, game left unhunted is unmarked, the run let go.</summary>
        public static void Stop(GuestFeast p)
        {
            if (p == null || !Runs.TryGetValue(p.id, out var r)) return;
            Runs.Remove(p.id);
            Release(r);
        }

        private static void Release(Run r)
        {
            try
            {
                foreach (var x in r.entrants.ToList())
                {
                    if (x == null || !x.Spawned) continue;
                    var d = x.CurJobDef;
                    if (d == FeastDefOf.Rimfolk_FeastRun || d == FeastDefOf.Rimfolk_FeastShoot) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    else if (r.kind == ContestKind.Tables && x.CurJob != null && x.CurJob.doUntilGatheringEnded && d?.joyKind != null) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    else if (r.kind == ContestKind.Hunt && d == JobDefOf.Hunt && x.Faction != Faction.OfPlayer) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                if (r.kind == ContestKind.Hunt && r.map != null)
                    foreach (var a in r.game)
                        if (a != null && a.Spawned && !a.Dead)
                            r.map.designationManager.RemoveAllDesignationsOn(a);
                if (r.bout != null && !r.bout.over) Outsiders.CallOff(r.bout);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a contest let go", e);
            }
            r.entrants.Clear();
        }

        private static FeastResult Result(GuestFeast p, Run r, Pawn winner, int count, string what = null)
        {
            var res = new FeastResult { kind = r.kind, entrants = r.entrants.Count, count = count, what = what };
            if (winner != null)
            {
                res.winner = winner;
                res.winnerName = winner.LabelShort;
                res.ours = winner.Faction == Faction.OfPlayer;
                res.people = res.ours ? null : winner.Faction;
            }
            return res;
        }

        // ================= Wrestling =================

        private static FeastResult Wrestling(GuestFeast p, Run r)
        {
            int now = Now;
            if (r.bout != null)
            {
                if (!Outsiders.Known(r.bout) && !r.bout.over) r.bout.off = true;
                if (!Outsiders.Over(r.bout, out var winner, out bool off) && Outsiders.Known(r.bout)) return null;
                var (c, x) = r.pairs[r.index];
                if (!off && winner != null) r.wins[winner] = (r.wins.TryGetValue(winner, out int w) ? w : 0) + 1;
                r.hits[c] = (r.hits.TryGetValue(c, out int hc) ? hc : 0) + Outsiders.Landed(r.bout, c);
                r.hits[x] = (r.hits.TryGetValue(x, out int hx) ? hx : 0) + Outsiders.Landed(r.bout, x);
                if (Outsiders.HardBlowOn(r.bout, x) && p.GuestOf(x) is FeastGuest g) g.hurt = true;
                r.bout = null;
                r.pause = now + 300;
            }
            if (now < r.pause) return null;
            r.index++;
            while (r.index < r.pairs.Count)
            {
                var (c, x) = r.pairs[r.index];
                if (c.Spawned && x.Spawned && Able(c) && Able(x) && !c.Drafted)
                {
                    bool hard = FeastMath.HardBlow(Rand.Value, Feasts.MayBeHurt(c) && Feasts.MayBeHurt(x), Feasts.HarmScale);
                    r.bout = Outsiders.FeastBout(c, x, r.spot, hard);
                    if (r.bout != null) return null;
                }
                r.index++;
            }
            return WrestlingResult(p, r);
        }

        private static FeastResult WrestlingResult(GuestFeast p, Run r)
        {
            var who = r.pairs.SelectMany(x => new[] { x.c, x.x }).Distinct().ToList();
            int best = FeastMath.BoutChampion(who.Select(x => r.wins.TryGetValue(x, out int w) ? w : 0).ToList(), who.Select(x => r.hits.TryGetValue(x, out int h) ? h : 0).ToList());
            var winner = best >= 0 ? who[best] : null;
            return Result(p, r, winner, winner != null && r.wins.TryGetValue(winner, out int n) ? n : 0);
        }

        // ================= The race =================

        /// <summary>A runner of a race under way (their job fails without one).</summary>
        public static bool Racing(Pawn p) => RunOf(p)?.kind == ContestKind.Race;

        public static void AtLine(Pawn p)
        {
            var r = RunOf(p);
            if (r != null) r.atLine.Add(p);
        }

        /// <summary>The start: once every runner is on the line (or a while has passed), a word and they're off.</summary>
        public static bool Go(Pawn p)
        {
            var r = RunOf(p);
            if (r == null) return false;
            int now = Now;
            if (r.go < 0)
            {
                var running = r.entrants.Where(x => x.Spawned && x.CurJobDef == FeastDefOf.Rimfolk_FeastRun).ToList();
                if (running.All(x => r.atLine.Contains(x)) || now - r.started > 1500)
                {
                    r.go = now + 90;
                    var starter = running.FirstOrDefault(x => x.Faction == Faction.OfPlayer) ?? running.FirstOrDefault();
                    // together: the start called on the line, with the runners side by side.
                    if (starter != null) Outsiders.Say(starter, "feastrungo", null);
                }
                return false;
            }
            return now >= r.go;
        }

        public static bool Stumbles(Pawn p) => FeastMath.Stumbles(Rand.Value);

        public static void Finished(Pawn p)
        {
            var r = RunOf(p);
            if (r == null || r.go < 0 || r.finish.ContainsKey(p)) return;
            r.finish[p] = Now - r.go;
        }

        private static FeastResult Race(GuestFeast p, Run r, bool timeUp)
        {
            var runners = r.entrants.ToList();
            bool still = runners.Any(x => !r.finish.ContainsKey(x) && x.Spawned && x.CurJobDef == FeastDefOf.Rimfolk_FeastRun);
            if (still && !timeUp && !(r.go > 0 && Now - r.go > GenDate.TicksPerHour)) return null;
            int best = FeastMath.RaceWinner(runners.Select(x => r.finish.TryGetValue(x, out int t) ? t : -1).ToList());
            var winner = best >= 0 ? runners[best] : null;
            if (winner != null)
            {
                var last = runners.Where(x => x != winner && r.finish.ContainsKey(x)).OrderBy(x => r.finish[x]).FirstOrDefault();
                // together: said at the finish, among the runners.
                Outsiders.Say(winner, "feastrunwin", last);
                if (last != null) Outsiders.Say(last, "feastrunlose", winner);
            }
            return Result(p, r, winner, winner != null ? Mathf.RoundToInt(r.finish[winner] / 60f) : 0);
        }

        // ================= The hunt =================

        /// <summary>Hunters without a beast go after one of the hunt's game nobody else is after, with the game's own hunting.</summary>
        private static void Hunt(Run r)
        {
            var taken = new HashSet<Pawn>();
            foreach (var x in r.entrants)
                if (x.CurJobDef == JobDefOf.Hunt && x.CurJob.targetA.Thing is Pawn prey)
                    taken.Add(prey);
            foreach (var x in r.entrants.ToList())
            {
                if (!x.Spawned || x.Dead || x.Downed || x.CurJobDef == JobDefOf.Hunt || !GamePrey.CanHunt(x) || x.InMentalState || x.Drafted) continue;
                if (x.Faction == Faction.OfPlayer && !PlayOuts.Interruptible(x) && x.CurJob?.def?.joyKind == null) continue;
                var prey = r.game.Where(a => a != null && a.Spawned && !a.Dead && !taken.Contains(a) && x.CanReserve(a) && x.CanReach(a, PathEndMode.Touch, Danger.Some))
                    .OrderBy(a => a.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (prey == null) continue;
                var job = JobMaker.MakeJob(JobDefOf.Hunt, prey);
                job.ignoreDesignations = true;
                job.killIncappedTarget = true;
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (x.CurJobDef == JobDefOf.Hunt) taken.Add(prey);
            }
        }

        private static int Kills(Run r, Pawn x) => Math.Max(0, (x.records?.GetAsInt(RecordDefOf.KillsAnimals) ?? 0) - (r.killsAt.TryGetValue(x, out int k) ? k : 0));

        private static FeastResult HuntStep(GuestFeast p, Run r, bool timeUp)
        {
            int now = Now;
            foreach (var x in r.entrants)
                if (!r.firstKill.ContainsKey(x) && Kills(r, x) > 0)
                    r.firstKill[x] = now;
            bool gameLeft = r.game.Any(a => a != null && a.Spawned && !a.Dead);
            if (!timeUp && gameLeft && now - r.started < FeastMath.HuntTicks)
            {
                Hunt(r);
                return null;
            }
            var hunters = r.entrants.ToList();
            int best = FeastMath.HuntWinner(hunters.Select(x => Kills(r, x)).ToList(), hunters.Select(x => r.firstKill.TryGetValue(x, out int t) ? t : -1).ToList());
            var winner = best >= 0 ? hunters[best] : null;
            // together: said where the hunt ended, among the hunters who came back.
            if (winner != null && winner.Spawned) Outsiders.Say(winner, "feasthuntwin", null);
            return Result(p, r, winner, winner != null ? Kills(r, winner) : 0);
        }

        // ================= The tables =================

        private static FeastResult TablesStep(GuestFeast p, Run r, bool timeUp)
        {
            if (!timeUp && Now < r.until && r.games.Any(g => Playing(g.a, g.table) || Playing(g.b, g.table))) return null;
            var won = new Dictionary<Pawn, int>();
            string what = null;
            foreach (var (a, b, t) in r.games)
            {
                bool aIn = Playing(a, t) || Now >= r.until, bIn = Playing(b, t) || Now >= r.until;
                Pawn w;
                if (aIn != bIn) w = aIn ? a : b;
                else w = FeastMath.TableWin(TableSkill(a, t), TableSkill(b, t), Rand.Value) ? a : b;
                won[w] = (won.TryGetValue(w, out int n) ? n : 0) + 1;
                what ??= t.def.label;
                // together: the game ends at the table where the two sat.
                Outsiders.Say(w, "feasttablewin", w == a ? b : a);
                Outsiders.Say(w == a ? b : a, "feasttablelose", w);
            }
            var players = won.Keys.ToList();
            int best = FeastMath.Champion(players.Select(x => won[x]).ToList());
            var winner = best >= 0 ? players[best] : null;
            return Result(p, r, winner, winner != null ? won[winner] : 0, what);
        }

        private static bool Playing(Pawn x, Building t) => x != null && x.Spawned && x.CurJob != null && x.CurJob.targetA.Thing == t && x.CurJob.def.joyKind != null;

        // ================= The shooting match =================

        /// <summary>A shooter of a match under way, whose turn it is (their job fails without one).</summary>
        public static bool Shooting(Pawn p)
        {
            var r = RunOf(p);
            return r?.kind == ContestKind.Shooting && r.shooter >= 0 && r.shooter < r.shooters.Count && r.shooters[r.shooter] == p;
        }

        public static void ShotDone(Pawn p, int hits)
        {
            var r = RunOf(p);
            if (r == null || r.kind != ContestKind.Shooting) return;
            r.scored[p] = hits;
            // together: said at the line, with the others watching.
            Outsiders.Say(p, hits >= FeastMath.Shots - 1 ? "feastshootgood" : hits <= 1 ? "feastshootpoor" : "feastshootfair", null);
            NextShooter(r);
        }

        private static void NextShooter(Run r)
        {
            while (++r.shooter < r.shooters.Count)
            {
                var x = r.shooters[r.shooter];
                if (x == null || !x.Spawned || x.Downed || x.InMentalState || x.equipment?.Primary == null || !x.CanReach(r.stand, PathEndMode.OnCell, Danger.Some)) continue;
                var job = JobMaker.MakeJob(FeastDefOf.Rimfolk_FeastShoot, r.target, r.stand);
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (x.CurJobDef == FeastDefOf.Rimfolk_FeastShoot) return;
            }
        }

        private static FeastResult ShootingStep(GuestFeast p, Run r, bool timeUp)
        {
            if (!timeUp && r.shooter < r.shooters.Count)
            {
                var x = r.shooters[r.shooter];
                // Their turn ended without finishing (called away, the job lost): the next steps up.
                if (x == null || !x.Spawned || x.CurJobDef != FeastDefOf.Rimfolk_FeastShoot)
                {
                    if (x != null && !r.scored.ContainsKey(x)) r.scored[x] = 0;
                    NextShooter(r);
                }
                if (r.shooter < r.shooters.Count) return null;
            }
            var shooters = r.scored.Keys.ToList();
            int best = FeastMath.ShootingWinner(shooters.Select(x => r.scored[x]).ToList(), shooters.Select(x => x.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).ToList());
            var winner = best >= 0 ? shooters[best] : null;
            return Result(p, r, winner, winner != null ? r.scored[winner] : 0, r.target?.def?.label);
        }
    }
}
