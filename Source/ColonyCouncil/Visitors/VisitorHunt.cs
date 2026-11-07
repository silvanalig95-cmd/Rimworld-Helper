using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class VisitorActsDefOf
    {
        public static JobDef Rimfolk_VisitPlay;
        public static JobDef Rimfolk_VisitDress;
        public static JobDef Rimfolk_VisitBear;

        static VisitorActsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VisitorActsDefOf));
    }

    /// <summary>
    /// Game a hunter from outside may take (a visit's hunters, a camp's), by <see cref="VisitorActsMath.Fits"/>: a wild
    /// beast nobody owns, never tame once, not marked by the colony for its hunters or tamers, no predator, nothing bigger
    /// than a deer, of a kind that lives wild on this land, and one that won't turn on them. And whether a shot at it
    /// would pass near anyone.
    /// </summary>
    public static class GamePrey
    {
        /// <summary>A beast hunted by visitors or a camp is kept from turning on them: the game hooks are on and <see cref="Patch_HuntedBeastHolds"/> is in place.</summary>
        public static bool RevengeHeld => RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_HuntedBeastHolds));

        /// <summary>Armed for it and able: the game's own hunting needs a bow or a gun.</summary>
        public static bool CanHunt(Pawn pawn) =>
            pawn?.equipment?.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon && !pawn.Downed && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting)
            && !pawn.WorkTagIsDisabled(WorkTags.Violent) && (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);

        /// <summary>A beast this hunter may go after (only the beast is looked at here: where it stands is the caller's to judge).</summary>
        public static bool Fits(Pawn a, Pawn hunter)
        {
            if (a?.RaceProps == null || !a.Spawned || a.Dead || a.Downed || a.InMentalState || a.IsPrisoner) return false;
            var race = a.RaceProps;
            if (!race.Animal || race.Insect || race.Dryad || race.IsMechanoid || race.IsAnomalyEntity) return false;
            var map = a.Map;
            bool livesHere = BiomeAnimals.LivesIn(map.Biome, a.kindDef);
            bool marked = map.designationManager.DesignationOn(a) != null;
            bool tameOnce = a.Name != null && !a.Name.Numerical;
            float revenge = PawnUtility.GetManhunterOnDamageChance(a, hunter);
            return VisitorActsMath.Fits(a.Faction == null, race.predator, a.BodySize, livesHere, marked, tameOnce, revenge, RevengeHeld);
        }

        /// <summary>
        /// Nobody but the hunter and the beast near the beast or on the line of fire to it: no person of any people, and no
        /// one's animal. <paramref name="ignore"/>: who doesn't count (the hunter's own party at their back).
        /// </summary>
        public static bool ClearShot(Map map, IntVec3 from, IntVec3 to, Pawn hunter, Pawn prey, Func<Pawn, bool> ignore = null)
        {
            if (map == null) return false;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == hunter || p == prey || p.Dead || p.RaceProps == null) continue;
                if (p.Faction == null && !p.RaceProps.Humanlike) continue;
                if (ignore != null && ignore(p)) continue;
                if (VisitorActsMath.InLineOfFire(from.x, from.z, to.x, to.z, p.Position.x, p.Position.z)) return false;
            }
            return true;
        }

        /// <summary>The hunt is a visit's or a camp's (one of their hunters after this beast): the beast holds back from turning on them.</summary>
        public static bool TheirHunt(Pawn hunter, Pawn beast)
        {
            if (hunter == null || beast == null) return false;
            var job = hunter.CurJob;
            bool after = job != null && job.def == JobDefOf.Hunt && job.targetA.Thing == beast;
            var lord = hunter.GetLord();
            if (lord == null) return false;
            if (lord.LordJob is LordJob_RimfolkVisit v) return after || VisitorTracker.Instance?.Get(v.tripId)?.prey == beast;
            var camp = CampTracker.CampOf(hunter);
            return camp != null && (after || camp.prey.Contains(beast));
        }
    }

    /// <summary>
    /// A hunting party's day on the colony's land (a visit's work "hunt", VisitorWork): one of them goes after a beast that
    /// is fair game (GamePrey) with the game's own hunting, out on the far side clear of the colony, while the rest keep
    /// watch; once it's down, one of them carries it back to their fire (or their spot, with no fire), dresses it there
    /// (the blood stays on the ground), and the meat and hide go into their packs and home with them. One beast a visit, and one more for each
    /// night they stay. A hunt under way or a kill not yet dressed holds them a little past their time; leaving before
    /// it's dressed, they carry it off whole. With nothing on the map that's fair game they watch for it, as before.
    /// </summary>
    public static class VisitorHunt
    {
        /// <summary>How far from their spot they go after game.</summary>
        private const float Range = 45f;

        private static readonly Dictionary<int, int> nextSearch = new Dictionary<int, int>();
        private static Game searchGame;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Not to look again for game for this visit before then (nothing was fair game a short while ago).</summary>
        private static bool Searched(int trip)
        {
            if (searchGame != Current.Game)
            {
                searchGame = Current.Game;
                nextSearch.Clear();
            }
            return nextSearch.TryGetValue(trip, out int next) && Now < next && next - Now <= GenDate.TicksPerHour * 2;
        }

        /// <summary>Where their kills go: beside their fire, else their spot.</summary>
        private static IntVec3 Home(Pawn p) =>
            VisitorCamp.FireOf(p)?.Position ?? (p.mindState?.duty != null && p.mindState.duty.focus.IsValid ? p.mindState.duty.focus.Cell : p.Position);

        /// <summary>The next step of the hunt for one of them: the kill carried back, dressed, or a beast to go after. Null: they keep watch.</summary>
        public static Job JobFor(Pawn p, VisitorTrip t)
        {
            if (p?.Map == null || t == null || p.Map != t.map) return null;
            Watch(t, false);
            if (t.kill is Corpse kill && kill.Spawned && kill.Map == p.Map)
            {
                // Someone of theirs has it in hand already.
                if (t.Here.Any(o => o != p && o.CurJob != null && (o.CurJob.targetA.Thing == kill || o.CurJob.targetB.Thing == kill))) return null;
                var home = Home(p);
                if (!kill.Position.InHorDistOf(home, 3.9f)) return Fetch(p, kill, home);
                return Dress(p, kill);
            }
            // One beast at a time: none while the last is still being carried in.
            if (t.prey != null || t.kill != null || t.kills >= VisitorActsMath.KillsAllowed(t.stayNights) || !GamePrey.CanHunt(p) || Searched(t.id)) return null;
            var animal = FindPrey(p, t);
            if (animal == null)
            {
                nextSearch[t.id] = Now + GenDate.TicksPerHour;
                return null;
            }
            t.prey = animal;
            var job = JobMaker.MakeJob(JobDefOf.Hunt, animal);
            // Not one the colony marked: it's their own hunt, not a colony hunter's.
            job.ignoreDesignations = true;
            job.killIncappedTarget = true;
            return job;
        }

        /// <summary>The nearest beast that's fair game, within reach of their spot, out on the land clear of the colony, with nobody near it.</summary>
        private static Pawn FindPrey(Pawn hunter, VisitorTrip t)
        {
            var map = hunter.Map;
            var focus = Home(hunter);
            var home = map.areaManager?.Home;
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var a in map.mapPawns.AllPawnsSpawned)
            {
                if (a.Faction != null || a.RaceProps == null || !a.RaceProps.Animal) continue;
                if (!a.Position.InHorDistOf(focus, Range) || home != null && home[a.Position]) continue;
                float d = a.Position.DistanceToSquared(hunter.Position);
                if (d >= bestD || !GamePrey.Fits(a, hunter) || !Visitors.ClearOfColony(a.Position, map)) continue;
                if (!GamePrey.ClearShot(map, hunter.Position, a.Position, hunter, a, o => t.people.Contains(o) && o.Position.InHorDistOf(hunter.Position, 4f))) continue;
                if (!hunter.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some)) continue;
                best = a;
                bestD = d;
            }
            return best;
        }

        /// <summary>The kill carried back to their fire, to a free spot beside it.</summary>
        private static Job Fetch(Pawn p, Corpse kill, IntVec3 home)
        {
            var map = p.Map;
            if (p.WorkTagIsDisabled(WorkTags.Hauling) || !p.CanReserveAndReach(kill, PathEndMode.ClosestTouch, Danger.Some)) return null;
            if (!CellFinder.TryFindRandomCellNear(home, map, 2, c => c != home && c.Standable(map) && c.GetFirstItem(map) == null && c.GetEdifice(map) == null
                                                                && p.CanReserve(c) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                return null;
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, kill, cell);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            return job;
        }

        /// <summary>The kill dressed where it lies by their fire.</summary>
        private static Job Dress(Pawn p, Corpse kill)
        {
            var map = p.Map;
            if (p.WorkTagIsDisabled(WorkTags.ManualSkilled) || !p.CanReserve(kill)) return null;
            foreach (var c in GenAdj.CellsAdjacent8Way(kill).InRandomOrder())
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.GetFirstPawn(map) != null || !p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                return JobMaker.MakeJob(VisitorActsDefOf.Rimfolk_VisitDress, c, kill);
            }
            return null;
        }

        /// <summary>Dressed: the meat and the hide into the packs of whoever dressed it, theirs to take home; the blood stays on the ground.</summary>
        public static void Dressed(Pawn p, Corpse kill)
        {
            var t = VisitorTracker.Instance?.TripOf(p);
            if (kill == null || kill.Destroyed || !kill.Spawned || p?.inventory == null) return;
            var map = kill.Map;
            var products = kill.ButcherProducts(p, 0.85f).ToList();
            kill.Destroy();
            foreach (var x in products)
                if (!p.inventory.innerContainer.TryAdd(x) && !x.Destroyed)
                    GenPlace.TryPlaceThing(x, p.Position, map, ThingPlaceMode.Near);
            if (t == null) return;
            if (t.kill == kill) t.kill = null;
            t.kills++;
            if (Rand.Chance(0.5f)) VisitorWords.Say(p, t, "dress", VisitorActs.LinesPool);
        }

        /// <summary>
        /// Every few ticks while they're at their spot: a hunt whose beast has fallen ends there (the game's own hunt would
        /// carry the body off to the colony's stores), and the body is theirs, left be by the colony's haulers.
        /// </summary>
        public static void EndKills(Lord lord, int tripId)
        {
            foreach (var p in lord.ownedPawns)
            {
                if (p.CurJobDef != JobDefOf.Hunt) continue;
                var target = p.CurJob.targetA.Thing;
                var corpse = target as Corpse ?? (target is Pawn v && v.Dead ? v.Corpse : null);
                if (corpse == null && !(target is Pawn dead && dead.Dead)) continue;
                p.jobs.EndCurrentJob(JobCondition.Succeeded);
                var t = VisitorTracker.Instance?.Get(tripId);
                if (t == null) continue;
                t.prey = null;
                if (corpse == null || !corpse.Spawned || t.kill != null) continue;
                t.kill = corpse;
                corpse.SetForbidden(true, false);
                if (Rand.Chance(0.6f)) VisitorWords.Say(p, t, "kill", VisitorActs.LinesPool);
                string beast = Find.ActiveLanguageWorker.WithIndefiniteArticle(corpse.InnerPawn?.KindLabel ?? corpse.LabelShort);
                Notify.Message("CC_VisitAct_Kill".Translate(t.faction?.Name ?? "?", beast).Resolve(), new LookTargets(corpse), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        /// <summary>
        /// A look at their hunt: a beast nobody of theirs is after any more is let go, and a kill gone (destroyed, taken) is
        /// forgotten. With <paramref name="safe"/>, a hunt that would shoot near the colony's people or anyone's animals, or
        /// that has led the hunter onto the colony's home ground, is broken off.
        /// </summary>
        public static void Watch(VisitorTrip t, bool safe)
        {
            if (t == null) return;
            var prey = t.prey;
            if (prey != null)
            {
                var hunter = t.people.FirstOrDefault(p => p != null && p.Spawned && p.CurJobDef == JobDefOf.Hunt && p.CurJob.targetA.Thing == prey);
                if (prey.Dead || !prey.Spawned || prey.Map != t.map || hunter == null) t.prey = null;
                else if (safe && Unsafe(t, hunter, prey))
                {
                    t.prey = null;
                    hunter.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
            var kill = t.kill;
            if (kill != null && (kill.Destroyed || kill.MapHeld != t.map || kill.Spawned && StoreUtility.IsInAnyStorage(kill)
                                 || !kill.Spawned && !(kill.ParentHolder is Pawn_CarryTracker ct && t.people.Contains(ct.pawn))))
                t.kill = null;
        }

        private static bool Unsafe(VisitorTrip t, Pawn hunter, Pawn prey)
        {
            var map = t.map;
            var home = map.areaManager?.Home;
            if (home != null && (home[hunter.Position] || home[prey.Position]) || !Visitors.ClearOfColony(prey.Position, map)) return true;
            // About to shoot (within a bow's or a rifle's reach): nobody near the line of fire but their own at their back.
            float range = hunter.equipment?.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 30f;
            if (!hunter.Position.InHorDistOf(prey.Position, range + 3f)) return false;
            return !GamePrey.ClearShot(map, hunter.Position, prey.Position, hunter, prey, o => t.people.Contains(o) && o.Position.InHorDistOf(hunter.Position, 4f));
        }

        /// <summary>A hunt under way, or a kill not yet dressed: they'd rather not go before it's done.</summary>
        public static bool Busy(VisitorTrip t) =>
            t != null && (t.prey != null && t.prey.Spawned && !t.prey.Dead || t.kill != null && !t.kill.Destroyed && t.kill.MapHeld == t.map);

        /// <summary>They're going: any hunt is broken off (the beast let be).</summary>
        public static void StopHunts(VisitorTrip t)
        {
            t.prey = null;
            foreach (var p in t.people)
                if (p != null && p.Spawned && p.CurJobDef == JobDefOf.Hunt)
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }

    /// <summary>A visitor dressing their kill by their fire: kneeling at it a while (the game's butchering look), then the meat and hide into their pack.</summary>
    public class JobDriver_VisitDress : JobDriver
    {
        private static EffecterDef effect;
        private static bool looked;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (!looked)
            {
                looked = true;
                effect = DefDatabase<EffecterDef>.GetNamedSilentFail("ButcherFlesh");
            }
            var ext = job.def.GetModExtension<VisitWorkExtension>() ?? new VisitWorkExtension();
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var work = ToilMaker.MakeToil("VisitDress");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, ext.ticks.RandomInRange);
            work.handlingFacing = true;
            work.socialMode = ext.social;
            work.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetB);
            if (effect != null) work.WithEffect(effect, TargetIndex.B);
            yield return work;
            yield return Toils_General.Do(() => VisitorHunt.Dressed(pawn, job.targetB.Thing as Corpse));
        }
    }

    /// <summary>
    /// A wild beast hunted by visitors or a camp's hunters (VisitorHunt, CampWork) doesn't turn on them when it's hurt: they
    /// only go after beasts that are fair game, and the player's rule is that nobody is hurt for the look of it. Only their
    /// own hunts; the colony's hunters meet the game's own odds.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MindState), "StartManhunterBecauseOfPawnAction")]
    public static class Patch_HuntedBeastHolds
    {
        public static bool Prefix(Pawn_MindState __instance, Pawn instigator)
        {
            if (!RimfolkPatches.On || instigator == null || __instance?.pawn == null) return true;
            try
            {
                return !GamePrey.TheirHunt(instigator, __instance.pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a hunted beast's revenge", e);
                return true;
            }
        }
    }
}
