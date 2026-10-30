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
    [DefOf]
    public static class HostingDefOf
    {
        public static JobDef Council_GuestSleep;

        static HostingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HostingDefOf));
    }

    /// <summary>
    /// How the guests of a hosting mission (a dignitary, refugees: Workers_MissionRemembered.cs) live at the colony
    /// while they stay. They come as the game's own visitors, who neither sleep nor eat anything but what they carry
    /// while they wait at their spot. So at each look the hungry walk to the colony's stored meals and eat them at its
    /// table (the game finds them a chair, as it does for colonists), anyone off to bed hungry has supper first, at
    /// night each lies down in a bed the mission counted for them, and once they've slept they get up (to breakfast if
    /// they're hungry). The game only lets someone sleep in a bed they own, so a guest owns theirs while they stay and
    /// gives it back when they go. With nothing of the colony's to eat, they eat the provisions they came with: enough
    /// for the stay, so nobody starves for it.
    /// </summary>
    public static class Hosting
    {
        /// <summary>Hungry enough to go and eat: a little above the game's own line, so they eat the colony's food before their packs.</summary>
        public const float EatBelow = 0.35f;

        /// <summary>Off to bed below this: supper first, so nobody goes hungry in the night.</summary>
        public const float SupperBelow = 0.7f;

        /// <summary>The longest a guest lies down at a time, whatever happens: a night's sleep and some.</summary>
        private const int SleepTicks = GenDate.TicksPerHour * 12;

        /// <summary>Below this with nothing of the colony's to eat, they eat from their packs.</summary>
        public const float PackBelow = 0.3f;

        /// <summary>What a guest went without at a look.</summary>
        [Flags]
        public enum Lack
        {
            None = 0,
            Food = 1,
            Bed = 2
        }

        // ---------------- Who can be sent ----------------

        /// <summary>
        /// Free to be sent to eat or to bed: here for the stay (not walking in or on the way out), awake and up, calm,
        /// only waiting about (not eating, fighting, or at one of Rimfolk's gatherings), and no raid on.
        /// </summary>
        public static bool Free(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.Drafted || !p.Awake() || p.InBed()) return false;
            if (p.IsPrisoner || p.HostileTo(Faction.OfPlayer) || Strangers.IsHospitalityGuest(p)) return false;
            if (!(p.GetLord()?.CurLordToil is LordToil_DefendPoint)) return false;
            return PlayOuts.Interruptible(p) && p.Map.dangerWatcher.DangerRating != StoryDanger.High;
        }

        /// <summary>
        /// Time for bed: worn out, or night has come and they're tired enough to fall asleep (under the game's own line
        /// for it, so they don't lie awake and get up again).
        /// </summary>
        public static bool WantsSleep(Pawn p)
        {
            var rest = p?.needs?.rest;
            if (rest == null || p.Map == null) return false;
            float level = rest.CurLevelPercentage;
            if (level < 0.28f) return true;
            int hour = GenLocalDate.HourOfDay(p.Map);
            return (hour >= 22 || hour < 6) && level < 0.7f;
        }

        // ---------------- Eating ----------------

        /// <summary>
        /// The colony's best fresh meal for them, the way a feast serves its guests (PlaySpots): fit for them, free and
        /// in reach. <paramref name="finestFirst"/>: the finest kind first (a guest of rank), else the plainest first;
        /// then the nearest.
        /// </summary>
        public static Thing Meal(Pawn p, bool finestFirst)
        {
            var map = p?.Map;
            if (map == null) return null;
            var spot = p.Position;
            var meals = PlaySpots.Meals(map).Where(Fresh);
            var sorted = (finestFirst ? meals.OrderByDescending(t => (int)t.def.ingestible.preferability) : meals.OrderBy(t => (int)t.def.ingestible.preferability))
                .ThenBy(t => t.Position.DistanceToSquared(spot)).ToList();
            return PlaySpots.BestMeal(p, sorted, out _);
        }

        private static bool Fresh(Thing t)
        {
            var rot = t.TryGetComp<CompRottable>();
            return rot == null || rot.Stage == RotStage.Fresh;
        }

        /// <summary>
        /// Raw food from the colony's stores they would eat (it leaves them the memory of eating it raw), never human
        /// meat: vegetables and the like before raw meat, then the nearest.
        /// </summary>
        public static Thing Raw(Pawn p)
        {
            var map = p?.Map;
            if (map == null) return null;
            return Missions.Matching(map, "rawfood")
                .Where(t => !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t) && FoodUtility.WillEat(p, t, p) && Beliefs.FitForStock(p, t)
                            && p.CanReserve(t, 10, 1) && p.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderByDescending(t => (int)t.def.ingestible.preferability).ThenBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        /// <summary>Food in their own packs, the best first: one with a title eats it too when it's all there is.</summary>
        public static Thing Pack(Pawn p)
        {
            var inv = p?.inventory?.innerContainer;
            if (inv == null) return null;
            Thing best = null;
            foreach (var t in inv)
            {
                if (!Packed(p, t)) continue;
                if (best == null || t.def.ingestible.preferability > best.def.ingestible.preferability) best = t;
            }
            return best;
        }

        private static bool Packed(Pawn p, Thing t) =>
            t?.def != null && t.def.IsNutritionGivingIngestible && !t.def.IsDrug && FoodUtility.WillEat(p, t, p, false, false);

        /// <summary>
        /// They go and eat it: the game carries it to a chair at a table if there's one near, as for colonists. Counted on
        /// <paramref name="m"/> once eaten (null: their own food). False if they couldn't start.
        /// </summary>
        public static bool Eat(Pawn p, Thing food, Mission m)
        {
            if (p?.jobs == null || food == null || food.Destroyed) return false;
            int count = Mathf.Max(1, Mathf.Min(food.stackCount, FoodUtility.WillIngestStackCountOf(p, food.def, FoodUtility.NutritionForEater(p, food))));
            if (!p.CanReserve(food, 10, count))
            {
                count = 1;
                if (!p.CanReserve(food, 10, count)) return false;
            }
            var job = JobMaker.MakeJob(JobDefOf.Ingest, food);
            job.count = count;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (p.CurJob != job) return false;
            if (m != null)
                p.jobs.curDriver.AddFinishAction(c =>
                {
                    if (c == JobCondition.Succeeded) m.meals++;
                });
            return true;
        }

        /// <summary>Eating the colony's food right now (anything off the map, not from their own packs).</summary>
        public static bool EatingOurs(Pawn p) => p.CurJobDef == JobDefOf.Ingest && p.jobs.curDriver is JobDriver_Ingest d && !d.EatingFromInventory;

        /// <summary>
        /// What they carry to eat, topped up to last the stay at a full appetite (pemmican, which keeps): what they eat on
        /// a day the colony has nothing for them.
        /// </summary>
        public static void Provision(Pawn p, float days)
        {
            var need = p?.needs?.food;
            var inv = p?.inventory?.innerContainer;
            var def = ThingDefOf.Pemmican;
            if (need == null || inv == null || def == null || days <= 0f) return;
            float want = need.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false) * GenDate.TicksPerDay * days;
            float have = 0f;
            foreach (var t in inv)
                if (Packed(p, t))
                    have += FoodUtility.NutritionForEater(p, t) * t.stackCount;
            float each = def.GetStatValueAbstract(StatDefOf.Nutrition);
            if (each <= 0f || have >= want) return;
            int n = Mathf.Min(400, Mathf.CeilToInt((want - have) / each));
            while (n > 0)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(n, def.stackLimit);
                n -= t.stackCount;
                if (!inv.TryAdd(t))
                {
                    t.Destroy();
                    return;
                }
            }
        }

        // ---------------- Sleeping ----------------

        /// <summary>
        /// The colony's beds and sleeping spots a guest may sleep in: in a room under a roof, not a prisoner's, a slave's
        /// or a sickbed, not one of Hospitality's guest beds (those are for its own guests), one <paramref name="like"/>
        /// fits in, and owned by nobody but the guests (<paramref name="ours"/>).
        /// </summary>
        public static List<Building_Bed> Beds(Map map, Func<Pawn, bool> ours, Pawn like)
        {
            var list = new List<Building_Bed>();
            if (map == null) return list;
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (room.IsPrisonCell || room.OpenRoofCount > 0) continue;
                foreach (var bed in room.ContainedBeds)
                    if (Usable(bed, ours, like)) list.Add(bed);
            }
            return list;
        }

        public static bool Usable(Building_Bed bed, Func<Pawn, bool> ours, Pawn like)
        {
            if (bed == null || !bed.Spawned || bed.Medical || bed.ForPrisoners || bed.ForSlaves || bed.Faction != Faction.OfPlayer) return false;
            if (bed.def.building?.bed_humanlike != true || GuestBeds.IsGuestBed(bed) || bed.IsBurning()) return false;
            if (like != null && !RestUtility.CanUseBedEver(like, bed.def)) return false;
            foreach (var o in bed.OwnersForReading)
                if (o != null && (ours == null || !ours(o))) return false;
            return true;
        }

        /// <summary>Nobody has it yet, or they'd share it with those who do (their beliefs and the others' allow it).</summary>
        public static bool CanShare(Building_Bed bed, Pawn p)
        {
            if (bed.OwnersForReading.All(o => o == p)) return true;
            if (bed.CompAssignableToPawn?.IdeoligionForbids(p) ?? true) return false;
            return bed.OwnersForReading.All(o => o == p || BedUtility.WillingToShareBed(p, o));
        }

        /// <summary>How many can sleep in these.</summary>
        public static int Slots(List<Building_Bed> beds) => beds?.Sum(b => b.SleepingSlotsCount) ?? 0;

        /// <summary>A place to stand in the room with the most of these beds (where the guests wait), or invalid.</summary>
        public static IntVec3 Spot(List<Building_Bed> beds, Map map)
        {
            if (beds.NullOrEmpty() || map == null) return IntVec3.Invalid;
            var room = beds.Where(b => b.GetRoom() != null).GroupBy(b => b.GetRoom()).OrderByDescending(g => g.Sum(b => b.SleepingSlotsCount)).FirstOrDefault()?.Key;
            return room == null ? IntVec3.Invalid : room.Cells.Where(c => c.Standable(map)).RandomElementWithFallback(IntVec3.Invalid);
        }

        /// <summary>
        /// The bed is theirs for the stay (the game lets nobody sleep in a bed they don't own) and they lie down in it.
        /// False if they couldn't: it's out of reach or taken, or they wouldn't share it.
        /// </summary>
        public static bool Sleep(Pawn p, Building_Bed bed)
        {
            if (p?.ownership == null || bed == null || !bed.Spawned) return false;
            bool own = bed.OwnersForReading.Contains(p);
            if (!own && (bed.OwnersForReading.Count >= bed.SleepingSlotsCount || !CanShare(bed, p))) return false;
            if (!p.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some, bed.SleepingSlotsCount, 0)) return false;
            if (!own && !p.ownership.ClaimBedIfNonMedical(bed)) return false;
            return LieDown(p, bed);
        }

        /// <summary>
        /// No bed for them: they lie down on the floor of the colony's dining or rec room, else on open ground near their
        /// spot on a night they'll come through. False if there's nowhere.
        /// </summary>
        public static bool SleepRough(Pawn p)
        {
            var cell = Floor(p);
            return cell.IsValid && LieDown(p, cell);
        }

        private static bool LieDown(Pawn p, LocalTargetInfo where)
        {
            var job = JobMaker.MakeJob(HostingDefOf.Council_GuestSleep, where);
            job.expiryInterval = SleepTicks;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJob == job;
        }

        /// <summary>Lying down awake after their sleep (they don't get up of themselves: <see cref="JobDriver_GuestSleep"/>).</summary>
        public static bool LyingAwake(Pawn p) =>
            p.CurJobDef == HostingDefOf.Council_GuestSleep && p.Awake() && p.GetPosture() != PawnPosture.Standing;

        /// <summary>Up from where they lay (the stay is over, or they've slept).</summary>
        public static void GetUp(Pawn p)
        {
            if (p != null && p.Spawned && p.CurJobDef == HostingDefOf.Council_GuestSleep) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private static IntVec3 Floor(Pawn p)
        {
            var map = p.Map;
            var party = new List<Pawn> { p };
            bool Open(IntVec3 c) =>
                c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null
                && !map.pawnDestinationReservationManager.IsReserved(c) && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some);
            var inside = Visitors.IndoorsCell(map, party);
            var room = inside.IsValid ? inside.GetRoom(map) : null;
            if (room != null && CellFinder.TryFindRandomCellNear(inside, map, 4, c => Open(c) && c.GetRoom(map) == room, out var floor)) return floor;
            var outside = VisitorWork.SleepCell(p);
            return outside.IsValid && Visitors.SafeOvernight(party, outside, map) ? outside : IntVec3.Invalid;
        }

        /// <summary>The colony's bed they had for the stay goes back to it.</summary>
        public static void GiveBack(Pawn p)
        {
            var bed = p?.ownership?.OwnedBed;
            if (bed == null || bed.Faction != Faction.OfPlayer || p.Faction == Faction.OfPlayer) return;
            p.ownership.UnclaimBed();
        }

        /// <summary>Like <see cref="GiveBack(Pawn)"/>, and any bed of the map still in their name (someone dead keeps none of their own).</summary>
        public static void GiveBack(Pawn p, Map map)
        {
            if (p == null || p.Faction == Faction.OfPlayer) return;
            GiveBack(p);
            if (map == null) return;
            foreach (var bed in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().ToList())
                if (bed.OwnersForReading.Contains(p))
                    bed.CompAssignableToPawn?.ForceRemovePawn(p);
        }

        /// <summary>Hours until seven in the morning (the next morning when that's already past).</summary>
        public static float HoursToMorning(Map map)
        {
            float h = map == null ? 12f : GenLocalDate.HourFloat(map);
            return (31f - h) % 24f;
        }
    }

    /// <summary>
    /// A hosted guest asleep in the bed kept for them, or on the floor without one: the game's own lying down, without
    /// looking for anything else to do meanwhile. A visitor's own plans would have them up again within moments to
    /// wander about their spot, so it's the mission's look that gets them up once they've slept (Hosting).
    /// </summary>
    public class JobDriver_GuestSleep : JobDriver_LayDown
    {
        public override bool LookForOtherJobs => false;
    }
}
