using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists fix small things that bother them, on their own: a light for a dark bedroom, a plant pot for an ugly
    /// one, a passive cooler for a sweltering one, a sleeping spot when there's no bed, a tidy-up of their own room, a
    /// short break when they're starved of recreation, a visit to a grave when they grieve.
    ///
    /// Low impact by design: plans (blueprints) only, cheap, buildable now, with materials in stock, only in their own
    /// room; at most a couple of open plans at a time; jobs only in their free time (except a short break when
    /// recreation is nearly gone) and never in danger. Every action is reported so the player can undo it.
    /// </summary>
    public class AutonomyTracker : IExposable
    {
        private static readonly DefRef<WorkTypeDef> CleaningWork = new DefRef<WorkTypeDef>("Cleaning");

        private List<Pawn> pawns = new List<Pawn>();
        private List<int> lastActed = new List<int>();
        private List<Thing> plans = new List<Thing>();

        private const int MaxOpenPlans = 2;
        private const float MaxPlanValue = 120f;
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public void Hourly(CouncilGameComponent comp)
        {
            if (!S.autonomy) return;
            plans.RemoveAll(t => t == null || t.Destroyed || !t.Spawned);
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                // Never while there's fighting to be done.
                if (MapFacts.ThreatOn(map)) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    try
                    {
                        TryAct(p, comp);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("autonomy " + p.LabelShort, e);
                    }
                }
            }
        }

        private int LastActed(Pawn p)
        {
            int i = pawns.IndexOf(p);
            return i >= 0 ? lastActed[i] : -999999;
        }

        private void MarkActed(Pawn p)
        {
            int i = pawns.IndexOf(p);
            int now = Find.TickManager.TicksGame;
            if (i >= 0) lastActed[i] = now;
            else
            {
                pawns.Add(p);
                lastActed.Add(now);
            }
        }

        private void TryAct(Pawn p, CouncilGameComponent comp)
        {
            if (p.Downed || p.InMentalState || p.Drafted || p.IsQuestLodger() || p.IsSlave || !People.HasInnerLife(p) || p.DevelopmentalStage.Baby()) return;
            if (p.needs?.mood == null) return;
            int now = Find.TickManager.TicksGame;
            if (now - LastActed(p) < GenDate.TicksPerDay) return;
            // Some take matters into their own hands more readily than others.
            int industry = p.story?.traits?.DegreeOfTrait(TraitDefOf.Industriousness) ?? 0;
            float chance = 0.25f * (industry > 0 ? 1.6f : industry < 0 ? 0.4f : 1f);
            if (!Rand.Chance(chance)) return;

            var moods = MoodInsight.LinesFor(p).Where(l => l.mood < 0f).ToList();
            if (moods.Count == 0) return;
            bool free = FreeTime(p);
            string done = null;
            LookTargets look = new LookTargets(p);

            bool Has(Func<ThoughtDef, bool> pred) => moods.Any(l => pred(l.def));
            bool Worker(ThoughtDef d, string name) => Is(d.workerClass, name);
            bool Named(ThoughtDef d, params string[] names) => names.Contains(d.defName);

            var room = RoomUtil.PrivateRoom(p);
            bool child = p.DevelopmentalStage.Child();

            // A short break when recreation is nearly gone, even in work hours (but not from urgent work).
            if (done == null && (p.needs.joy?.CurLevelPercentage ?? 1f) < 0.12f && p.timetable?.CurrentAssignment == TimeAssignmentDefOf.Work && CanBreakFrom(p))
            {
                var job = JoyGiver.TryIssueJobPackage(p, default(JobIssueParams)).Job;
                if (job != null)
                {
                    // A real break despite the work hours, but a short one (about half an hour).
                    job.ignoreJoyTimeAssignment = true;
                    job.expiryInterval = 1250;
                    if (Start(p, job)) done = "CC_Auto_JoyBreak".Translate(p.Named("PAWN")).Resolve();
                }
            }

            // Grief: a visit to the grave, in their own time.
            if (done == null && free && Idle(p) && Has(d => Flavor.GriefThoughts.Contains(d.defName) || Named(d, "WitnessedDeathFamily", "WitnessedDeathAlly", "ColonistLost")))
            {
                var grave = GraveOfSomeoneMourned(p);
                var visit = DefDatabase<JobDef>.GetNamedSilentFail("VisitGrave");
                if (grave != null && visit != null && !grave.IsForbidden(p) && grave.Position.InAllowedArea(p)
                    && p.CanReserveAndReach(grave, PathEndMode.InteractionCell, Danger.None))
                {
                    if (Start(p, JobMaker.MakeJob(visit, grave)))
                    {
                        done = "CC_Auto_Grave".Translate(p.Named("PAWN"), (grave as Building_Grave)?.Corpse?.InnerPawn?.LabelShort ?? "?").Resolve();
                        look = new LookTargets(grave);
                    }
                }
            }

            // Their own room: tidy it, light it, brighten it, cool it.
            if (done == null && room != null && !child)
            {
                bool roomBothers = Has(d => Named(d, "SleptInBedroom") || Worker(d, "ThoughtWorker_NeedBeauty"));
                var filth = roomBothers && free && Idle(p) ? room.ContainedAndAdjacentThings.OfType<Filth>().Where(f => f.Spawned && room.ContainsCell(f.Position)).Take(15).ToList() : null;
                if (!filth.NullOrEmpty() && p.workSettings != null && !p.WorkTypeIsDisabled(CleaningWork.Def ?? WorkTypeDefOf.Hauling))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Clean);
                    foreach (var f in filth) job.AddQueuedTarget(TargetIndex.A, f);
                    if (Start(p, job)) done = "CC_Auto_Clean".Translate(p.Named("PAWN")).Resolve();
                }
                if (done == null && S.autonomyPlans && plans.Count < MaxOpenPlans)
                {
                    ThingDef want = null;
                    string key = null;
                    if (Has(d => Worker(d, "ThoughtWorker_Dark")) && !RoomHas(room, Avail.IsLight) && Dim(room, p.Map))
                    {
                        want = Cheapest(p.Map, Avail.IsLight);
                        key = "CC_Auto_Light";
                    }
                    else if (Has(d => Named(d, "SleptInHeat")) && !RoomHas(room, Avail.IsCooler))
                    {
                        want = Cheapest(p.Map, d => Avail.IsCooler(d) && !Avail.NeedsPower(d));
                        key = "CC_Auto_Cooler";
                    }
                    else if (roomBothers && !RoomHas(room, Avail.IsPlantPot))
                    {
                        want = Cheapest(p.Map, Avail.IsPlantPot);
                        key = "CC_Auto_PlantPot";
                    }
                    if (want != null && Plan(room, p.Map, want, out var blueprint))
                    {
                        done = key.Translate(p.Named("PAWN"), want.label.Named("THING")).Resolve();
                        look = new LookTargets(blueprint);
                    }
                }
            }

            // No bed at all: a sleeping spot somewhere sheltered. It costs nothing.
            if (done == null && S.autonomyPlans && plans.Count < MaxOpenPlans && p.ownership?.OwnedBed == null && !child
                && Has(d => Named(d, "SleptOnGround", "SleptOutside")) && !AnyFreeBed(p))
            {
                var spot = DefDatabase<ThingDef>.GetNamedSilentFail("SleepingSpot");
                var shelter = spot == null ? null : Shelter(p);
                if (shelter != null && Plan(shelter, p.Map, spot, out var blueprint))
                {
                    done = "CC_Auto_SleepingSpot".Translate(p.Named("PAWN")).Resolve();
                    look = new LookTargets(blueprint);
                }
            }

            if (done == null) return;
            MarkActed(p);
            News.Post(done, look, NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            comp.journals.Write(p, "Council_Journal_SelfHelp", null);
        }

        // ---------------- Jobs ----------------

        private static JobGiver_GetJoy joyGiver;

        /// <summary>The game's own way of picking something fun to do.</summary>
        private static JobGiver_GetJoy JoyGiver
        {
            get
            {
                if (joyGiver == null)
                {
                    joyGiver = new JobGiver_GetJoy();
                    joyGiver.ResolveReferences();
                }
                return joyGiver;
            }
        }

        /// <summary>Starts a job as if they'd thought of it themselves (not a forced order).</summary>
        private static bool Start(Pawn p, Job job)
        {
            if (job == null || p.jobs == null) return false;
            // If its reservations fail, the game ends the job and releases whatever it had reserved.
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, preToilReservationsCanFail: true);
            return p.CurJob == job;
        }

        // ---------------- Plans ----------------

        /// <summary>The cheapest thing for the purpose that the colony can build now, has materials for, and that costs little.</summary>
        private static ThingDef Cheapest(Map map, Func<ThingDef, bool> pred)
        {
            ThingDef best = null;
            float bestValue = float.MaxValue;
            foreach (var d in Avail.AllBuildings)
            {
                if (d.size.x > 1 || d.size.z > 1 || !pred(d) || !Avail.CanBuild(d, map) || !Avail.MaterialsWithinReach(d, map)) continue;
                float value = PlanValue(d, map);
                if (value > MaxPlanValue || value >= bestValue) continue;
                best = d;
                bestValue = value;
            }
            return best;
        }

        private static float PlanValue(ThingDef d, Map map)
        {
            var stuff = d.MadeFromStuff ? Avail.BestStuff(d, map) : null;
            float v = 0f;
            foreach (var c in d.CostListAdjusted(stuff, false)) v += c.thingDef.BaseMarketValue * c.count;
            return v;
        }

        private bool Plan(Room room, Map map, ThingDef def, out Thing blueprint)
        {
            blueprint = null;
            var stuff = def.MadeFromStuff ? Avail.BestStuff(def, map) : null;
            if (!PersonalTouchTracker.FindCell(room, map, def, stuff, out var cell, out var rot)) return false;
            blueprint = GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction.OfPlayer, stuff);
            if (blueprint == null) return false;
            plans.Add(blueprint);
            return true;
        }

        private static bool RoomHas(Room room, Func<ThingDef, bool> pred) =>
            room.ContainedAndAdjacentThings.Any(t => room.ContainsCell(t.Position)
                && (pred(t.def) || (t is Blueprint b && b.def.entityDefToBuild is ThingDef bd && pred(bd)) || (t is Frame f && f.def.entityDefToBuild is ThingDef fd && pred(fd))));

        /// <summary>No light of its own: dark at night, whatever the sun does by day.</summary>
        private static bool Dim(Room room, Map map)
        {
            var cells = room.Cells.ToList();
            if (cells.Count == 0) return false;
            int dark = cells.Count(c => map.glowGrid.GroundGlowAt(c, false, true) < 0.3f);
            return dark > cells.Count / 2;
        }

        // ---------------- People ----------------

        private static bool FreeTime(Pawn p)
        {
            var a = p.timetable?.CurrentAssignment;
            return a == null || a == TimeAssignmentDefOf.Anything || a == TimeAssignmentDefOf.Joy;
        }

        /// <summary>Doing nothing much: wandering, waiting, or at their leisure.</summary>
        private static bool Idle(Pawn p)
        {
            // A party, a ritual or a caravan being packed looks like waiting around, but isn't.
            if (Busy(p)) return false;
            var job = p.CurJob;
            if (job == null) return true;
            return job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_Wander || job.def == JobDefOf.GotoWander || job.def.joyKind != null;
        }

        private static bool Busy(Pawn p) =>
            p.GetLord() != null || p.mindState?.duty != null || p.carryTracker?.CarriedThing != null || p.InBed();

        /// <summary>A break is fine from ordinary work, not from fighting fires, tending the sick or the like.</summary>
        private static bool CanBreakFrom(Pawn p)
        {
            if (Busy(p) || JoyUtility.LordPreventsGettingJoy(p)) return false;
            var job = p.CurJob;
            if (job == null) return true;
            if (job.playerForced || !job.def.casualInterruptible || job.def == JobDefOf.LayDown || job.def == JobDefOf.Ingest) return false;
            // Only ordinary work from the work tab: never deathrest, resting, rituals, fleeing or anything else.
            var type = job.workGiverDef?.workType?.defName;
            if (type == null) return false;
            return type != "Firefighter" && type != "Doctor" && type != "Patient" && type != "PatientBedRest" && type != "Childcare" && type != "Warden" && type != "BasicWorker";
        }

        private static Thing GraveOfSomeoneMourned(Pawn p)
        {
            var mourned = p.needs.mood.thoughts.memories.Memories
                .Where(m => m.otherPawn != null && m.otherPawn.Dead && m.age < GenDate.TicksPerDay * 4)
                .Select(m => m.otherPawn).ToHashSet();
            if (mourned.Count == 0) return null;
            return p.Map.listerThings.ThingsInGroup(ThingRequestGroup.Grave)
                .FirstOrDefault(g => g is Building_Grave grave && grave.Corpse?.InnerPawn != null && mourned.Contains(grave.Corpse.InnerPawn));
        }

        /// <summary>A free bed for a colonist (the map's beds as of this hour, from Lean/MapFacts; who owns them as of now).</summary>
        private static bool AnyFreeBed(Pawn p)
        {
            var beds = MapFacts.Of(p.Map)?.Beds;
            if (beds == null) return false;
            for (int i = 0; i < beds.Count; i++)
            {
                var b = beds[i];
                if (b.Spawned && b.Faction == Faction.OfPlayer && !b.Medical && b.def.building.bed_humanlike && !b.ForPrisoners
                    && b.OwnersForReading.Count < b.SleepingSlotsCount && !b.IsBurning() && !GuestBeds.IsGuestBed(b))
                    return true;
            }
            return false;
        }

        /// <summary>A sheltered room to sleep in: indoors, in the home area, not a kitchen, hospital, prison or the like.</summary>
        private static Room Shelter(Pawn p)
        {
            var map = p.Map;
            // A barracks first, then a room with no beds at all; never someone's private bedroom (a second bed would
            // turn it into a barracks and cost them their room).
            var rooms = map.regionGrid.AllRooms
                .Where(r => !r.PsychologicallyOutdoors && !r.IsDoorway && !r.Fogged && r.CellCount >= 4 && r.ProperRoom
                            && map.areaManager.Home[r.Cells.First()] && !r.IsPrisonCell && OkRole(r.Role)
                            && (r.Role == RoomRoleDefOf.Barracks || !r.ContainedBeds.Any()))
                .OrderBy(r => r.Role == RoomRoleDefOf.Barracks ? 0 : 1)
                .ThenBy(r => r.Cells.First().DistanceToSquared(p.Position))
                .ToList();
            return rooms.FirstOrDefault();
        }

        private static bool OkRole(RoomRoleDef role)
        {
            if (role == null || role == RoomRoleDefOf.None) return true;
            if (role == RoomRoleDefOf.Barracks || role == RoomRoleDefOf.Bedroom) return true;
            string n = role.defName;
            return n == "RecRoom" || n == "DiningRoom";
        }

        private static bool Is(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                if (t.Name == name) return true;
            return false;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // When someone last acted only matters for a day (TryAct): older entries, and the dead, are forgotten.
                int now = Find.TickManager.TicksGame;
                for (int i = pawns.Count - 1; i >= 0; i--)
                {
                    if (pawns[i] == null || pawns[i].Discarded || pawns[i].Dead || lastActed[i] > now || now - lastActed[i] > GenDate.TicksPerDay * 2)
                    {
                        pawns.RemoveAt(i);
                        lastActed.RemoveAt(i);
                    }
                }
                plans.RemoveAll(t => t == null || t.Destroyed);
            }
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Collections.Look(ref lastActed, "lastActed", LookMode.Value);
            Scribe_Collections.Look(ref plans, "plans", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pawns == null || lastActed == null || pawns.Count != lastActed.Count)
                {
                    pawns = new List<Pawn>();
                    lastActed = new List<int>();
                }
                for (int i = pawns.Count - 1; i >= 0; i--)
                {
                    if (pawns[i] == null)
                    {
                        pawns.RemoveAt(i);
                        lastActed.RemoveAt(i);
                    }
                }
                if (plans == null) plans = new List<Thing>();
                plans.RemoveAll(t => t == null);
            }
        }
    }
}
