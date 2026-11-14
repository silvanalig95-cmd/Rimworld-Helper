using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class KeepOutDefOf
    {
        public static JobDef Rimfolk_BringOutFood;

        static KeepOutDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(KeepOutDefOf));
    }

    /// <summary>
    /// Visitors keep out of the colony's bedrooms and its cold stores (the player's request, 13 November 2026): the game's
    /// visitors, a caravan's people, Rimfolk's visits and callers, and Hospitality's guests alike. The rules are in
    /// KeepOutMath. Every 250 ticks, while visitors are on a home map, the rooms they keep out of are worked out (a room
    /// with a bed of the colony's people in it; a room kept cold with food stored or lying in it, whatever role the game
    /// gives it), with who sleeps in each, whose bed is there and whose doctor's work takes them in. Each such room's doors
    /// are then forbidden to those visitors as a forbidden door is to a colonist (<see cref="Patch_KeepOutDoors"/>): the
    /// game's paths, reach and wandering go round them, so a visitor neither walks in nor takes up anything inside. Someone
    /// already in such a room walks out of it. Food they may not go in for is brought out by a colonist and handed to them
    /// (<see cref="JobDriver_BringOutFood"/>). Gated on the deeper game hooks.
    /// </summary>
    public static class KeepOut
    {
        /// <summary>One room visitors keep out of, as it was at the last look.</summary>
        public sealed class Kept
        {
            public int id;
            public KeptKind kind;
            /// <summary>One of the colony sleeps in it.</summary>
            public bool asleep;
            public float temperature;
            public int food;
            public bool stores;
            public string role;
            /// <summary>Its cells and its doorways' cells, by the map's cell index.</summary>
            public readonly HashSet<int> cells = new HashSet<int>();
            /// <summary>Whoever a bed in it is for (thingIDNumber).</summary>
            public readonly HashSet<int> owners = new HashSet<int>();
            /// <summary>Visitors a doctor's work takes in (thingIDNumber).</summary>
            public readonly HashSet<int> care = new HashSet<int>();
            public readonly List<Building_Door> doors = new List<Building_Door>();
            /// <summary>A cell just outside each of its doors, to walk out to.</summary>
            public readonly List<IntVec3> exits = new List<IntVec3>();
        }

        /// <summary>A home map's rooms kept from visitors and its visitors, as at the last look; never changed once made.</summary>
        public sealed class Snap
        {
            public int width;
            public int tick;
            public readonly HashSet<int> visitors = new HashSet<int>();
            public readonly List<Kept> rooms = new List<Kept>();
            public readonly Dictionary<int, Kept[]> byDoor = new Dictionary<int, Kept[]>();
            /// <summary>The rooms counted cold (Room.ID), so that one stays cold until it is warm enough to leave it.</summary>
            public readonly HashSet<int> cold = new HashSet<int>();
            /// <summary>Visitors standing in one of the rooms at the look.</summary>
            public readonly HashSet<int> inside = new HashSet<int>();
            public int sign;
        }

        /// <summary>
        /// By map (uniqueID). Replaced whole at each look and read from any thread: the game finds paths on worker threads,
        /// and the door check reads only these.
        /// </summary>
        private static volatile Dictionary<int, Snap> snaps = new Dictionary<int, Snap>();

        /// <summary>When each visitor was last brought food (thingIDNumber to tick), so a colonist isn't sent twice running.</summary>
        private static readonly Dictionary<int, int> fedAt = new Dictionary<int, int>();

        /// <summary>Hours between food brought out to the same hungry visitor, should they still go without.</summary>
        private const int FedAgain = 2 * GenDate.TicksPerHour;

        public static bool On => RimfolkPatches.On;

        public static void Reset()
        {
            snaps = new Dictionary<int, Snap>();
            fedAt.Clear();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Who keeps out ----------------

        /// <summary>
        /// A visitor of any kind: someone (or a beast) of another people on the colony's land, not hostile, not held by the
        /// colony, up and in their right mind. Allies come to fight with the colony are left to the fight.
        /// </summary>
        public static bool IsVisitor(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState) return false;
            var f = p.Faction;
            if (f == null || f.IsPlayer || p.IsPrisonerOfColony || p.IsSlaveOfColony || p.HostileTo(Faction.OfPlayer)) return false;
            return !(p.GetLord()?.LordJob is LordJob_AssistColony);
        }

        /// <summary>One of the colony's own people (a colonist, a slave, a child): a bed of theirs makes a room private.</summary>
        private static bool Ours(Pawn p) => p != null && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction != null && p.Faction.IsPlayer;

        // ---------------- The door (any thread) ----------------

        /// <summary>
        /// The door leads into a room this visitor keeps out of now, and they are not inside it. Reads only the last look,
        /// so it is safe from the game's path threads.
        /// </summary>
        public static bool Barred(Building_Door door, Pawn p)
        {
            var all = snaps;
            if (all.Count == 0) return false;
            var map = door.Map;
            if (map == null || !all.TryGetValue(map.uniqueID, out var s)) return false;
            int id = p.thingIDNumber;
            if (!s.visitors.Contains(id) || !s.byDoor.TryGetValue(door.thingIDNumber, out var rooms)) return false;
            var pos = p.Position;
            int at = pos.z * s.width + pos.x;
            foreach (var k in rooms)
                if (KeepOutMath.Barred(k.kind, k.asleep, k.cells.Contains(at), k.owners.Contains(id), k.care.Contains(id))) return true;
            return false;
        }

        // ---------------- Asked by Rimfolk's own code (the main thread) ----------------

        public static Snap SnapOf(Map map) => map != null && snaps.TryGetValue(map.uniqueID, out var s) ? s : null;

        /// <summary>The cell is in a room this visitor keeps out of now (as at the last look), and they aren't in it.</summary>
        public static bool KeptFrom(Pawn p, IntVec3 c)
        {
            var s = SnapOf(p?.Map);
            if (s == null || !c.IsValid || !s.visitors.Contains(p.thingIDNumber)) return false;
            int at = c.z * s.width + c.x, pos = p.Position.z * s.width + p.Position.x;
            foreach (var k in s.rooms)
                if (k.cells.Contains(at))
                    return KeepOutMath.Barred(k.kind, k.asleep, k.cells.Contains(pos), k.owners.Contains(p.thingIDNumber), k.care.Contains(p.thingIDNumber));
            return false;
        }

        /// <summary>
        /// What a room is to visitors now: a bedroom of the colony's people, a cold store, or neither. For Rimfolk's own
        /// choices of where a visitor stays or sleeps, which never pick such a room.
        /// </summary>
        public static KeptKind KindOf(Room room)
        {
            if (!Proper(room)) return KeptKind.None;
            if (ColonyBed(room, null)) return KeptKind.Bedroom;
            bool wasCold = SnapOf(room.Map)?.cold.Contains(room.ID) ?? false;
            if (!KeepOutMath.Cold(room.Temperature, wasCold)) return KeptKind.None;
            return KeepOutMath.HoldsFood(FoodStacks(room), StoresFood(room)) ? KeptKind.ColdStore : KeptKind.None;
        }

        /// <summary>A room visitors keep out of (see <see cref="KindOf"/>).</summary>
        public static bool IsKept(Room room) => KindOf(room) != KeptKind.None;

        // ---------------- What a room is ----------------

        private static bool Proper(Room room) =>
            room != null && !room.Dereferenced && !room.IsDoorway && room.ProperRoom && !room.PsychologicallyOutdoors && !room.IsHuge;

        /// <summary>A bed of the colony's own people stands in it (a guest bed or a sickbed doesn't count); its beds' owners into <paramref name="owners"/>.</summary>
        private static bool ColonyBed(Room room, HashSet<int> owners)
        {
            bool any = false;
            foreach (var b in room.ContainedBeds)
            {
                if (b == null || b.def.building == null || !b.def.building.bed_humanlike || b.Medical || b.ForPrisoners) continue;
                bool guest = GuestBeds.IsGuestBed(b);
                foreach (var o in b.OwnersForReading)
                {
                    if (o == null) continue;
                    owners?.Add(o.thingIDNumber);
                    if (!guest && Ours(o)) any = true;
                }
                if (any && owners == null) return true;
            }
            return any;
        }

        private static bool Edible(Thing t) =>
            t != null && !(t is Corpse) && t.def.category == ThingCategory.Item && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug;

        /// <summary>Stacks of food people eat lying in the room.</summary>
        private static int FoodStacks(Room room)
        {
            int n = 0;
            foreach (var reg in room.Regions.ToList())
                foreach (var t in reg.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                    if (Edible(t)) n++;
            return n;
        }

        private static ThingDef[] foodSamples;

        /// <summary>A meal, a raw vegetable and a few kinds of raw meat and plant food: a store that takes any of them takes food.</summary>
        private static ThingDef[] FoodSamples()
        {
            if (foodSamples != null) return foodSamples;
            var list = new List<ThingDef> { ThingDefOf.MealSimple, ThingDefOf.MealFine, ThingDefOf.MealSurvivalPack, ThingDefOf.Pemmican, ThingDefOf.RawPotatoes };
            foreach (var cat in new[] { ThingCategoryDefOf.MeatRaw, ThingCategoryDefOf.PlantFoodRaw })
                if (cat != null)
                    list.AddRange(cat.DescendantThingDefs.Where(d => d != null && d != ThingDefOf.Meat_Human && d.IsNutritionGivingIngestible).Take(4));
            foodSamples = list.Where(d => d != null).Distinct().ToArray();
            return foodSamples;
        }

        private static bool AllowsFood(StorageSettings s)
        {
            if (s?.filter == null) return false;
            foreach (var d in FoodSamples())
                if (s.filter.Allows(d)) return true;
            return false;
        }

        /// <summary>A stockpile or a shelf in the room takes food.</summary>
        private static bool StoresFood(Room room)
        {
            var map = room.Map;
            if (map == null) return false;
            var seen = new HashSet<SlotGroup>();
            foreach (var c in room.Cells)
            {
                var g = c.GetSlotGroup(map);
                if (g != null && seen.Add(g) && AllowsFood(g.Settings)) return true;
            }
            return false;
        }

        // ---------------- The look ----------------

        /// <summary>Every 250 ticks (KeepOutBook): each home map looked at again while visitors are on it.</summary>
        public static void Rare(int now)
        {
            var next = new Dictionary<int, Snap>();
            var before = snaps;
            if (On)
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome) continue;
                    before.TryGetValue(map.uniqueID, out var old);
                    var s = Build(map, old, now);
                    if (s != null) next[map.uniqueID] = s;
                    Changed(map, old, s);
                }
            // A map with visitors no longer (or the hooks off): its reach worked out afresh without the barred doors.
            foreach (var kv in before)
                if (!next.ContainsKey(kv.Key))
                {
                    var map = Find.Maps.FirstOrDefault(m => m.uniqueID == kv.Key);
                    map?.reachability?.ClearCache();
                }
            snaps = next;
            foreach (var map in Find.Maps)
            {
                if (map == null || !next.TryGetValue(map.uniqueID, out var s)) continue;
                try
                {
                    WalkOutAll(map, s);
                    FeedHungry(map, s, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitors kept out: walking out and food", e);
                }
            }
            fedAt.RemoveAll(kv => now - kv.Value > FedAgain * 4);
        }

        /// <summary>Looked at again now (a visit's work just set going, so its doctor's way in counts from the start).</summary>
        public static void Refresh(Map map)
        {
            if (map == null || !map.IsPlayerHome) return;
            var all = snaps;
            all.TryGetValue(map.uniqueID, out var old);
            var s = On ? Build(map, old, Now) : null;
            var next = new Dictionary<int, Snap>(all);
            if (s != null) next[map.uniqueID] = s;
            else next.Remove(map.uniqueID);
            snaps = next;
            Changed(map, old, s);
        }

        /// <summary>The game's reach is kept per pawn: worked out again where what the doors allow has changed.</summary>
        private static void Changed(Map map, Snap old, Snap s)
        {
            if (old == null && s == null) return;
            if (old == null || s == null || old.sign != s.sign)
            {
                map.reachability?.ClearCache();
                return;
            }
            // Someone who stood in a room at the last look and doesn't now (or the other way round).
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (s.visitors.Contains(p.thingIDNumber) && old.inside.Contains(p.thingIDNumber) != s.inside.Contains(p.thingIDNumber))
                    map.reachability.ClearCacheFor(p);
        }

        private static Snap Build(Map map, Snap before, int now)
        {
            var s = new Snap { width = map.Size.x, tick = now };
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (IsVisitor(p))
                    s.visitors.Add(p.thingIDNumber);
            if (s.visitors.Count == 0) return null;
            // The rooms one of the colony sleeps in.
            var asleep = new HashSet<int>();
            foreach (var p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                if (Ours(p) && !p.Awake() && p.GetRoom() is Room r)
                    asleep.Add(r.ID);
            var byDoor = new Dictionary<int, List<Kept>>();
            foreach (var room in map.regionGrid.AllRooms.ToList())
            {
                if (!Proper(room)) continue;
                var owners = new HashSet<int>();
                bool colonyBed = ColonyBed(room, owners);
                bool cold = KeepOutMath.Cold(room.Temperature, before?.cold.Contains(room.ID) ?? false);
                if (cold) s.cold.Add(room.ID);
                int food = 0;
                bool stores = false;
                if (!colonyBed && cold)
                {
                    food = FoodStacks(room);
                    stores = StoresFood(room);
                }
                var kind = KeepOutMath.Kind(true, colonyBed, cold, KeepOutMath.HoldsFood(food, stores));
                if (kind == KeptKind.None) continue;
                var k = new Kept
                {
                    id = room.ID, kind = kind, asleep = asleep.Contains(room.ID), temperature = room.Temperature, food = food, stores = stores,
                    role = room.Role?.label
                };
                k.owners.UnionWith(owners);
                foreach (var c in room.Cells) k.cells.Add(c.z * s.width + c.x);
                var seen = new HashSet<int>();
                foreach (var reg in room.Regions.ToList())
                    foreach (var link in reg.links)
                    {
                        var door = link.GetOtherRegion(reg)?.door;
                        if (door == null || !seen.Add(door.thingIDNumber)) continue;
                        k.doors.Add(door);
                        foreach (var c in door.OccupiedRect()) k.cells.Add(c.z * s.width + c.x);
                    }
                foreach (var door in k.doors)
                {
                    if (!byDoor.TryGetValue(door.thingIDNumber, out var list)) byDoor[door.thingIDNumber] = list = new List<Kept>();
                    list.Add(k);
                }
                s.rooms.Add(k);
            }
            // A cell just outside each door: not in the room, not a doorway, somewhere to stand.
            foreach (var k in s.rooms)
                foreach (var door in k.doors)
                    foreach (var c in door.OccupiedRect())
                        foreach (var d in GenAdj.CardinalDirections)
                        {
                            var o = c + d;
                            if (!o.InBounds(map) || k.cells.Contains(o.z * s.width + o.x) || !o.Standable(map) || o.GetDoor(map) != null) continue;
                            if (s.rooms.Any(r => r.cells.Contains(o.z * s.width + o.x))) continue;
                            k.exits.Add(o);
                        }
            foreach (var kv in byDoor) s.byDoor[kv.Key] = kv.Value.ToArray();
            Care(map, s);
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (!s.visitors.Contains(p.thingIDNumber)) continue;
                int at = p.Position.z * s.width + p.Position.x;
                if (s.rooms.Any(k => k.cells.Contains(at))) s.inside.Add(p.thingIDNumber);
            }
            s.sign = Sign(s);
            return s;
        }

        /// <summary>The kept room a cell is in, or null.</summary>
        private static Kept RoomAt(Snap s, IntVec3 c)
        {
            if (!c.IsValid) return null;
            int at = c.z * s.width + c.x;
            foreach (var k in s.rooms)
                if (k.cells.Contains(at))
                    return k;
            return null;
        }

        private static void Allow(Snap s, Pawn x, IntVec3 c)
        {
            if (x == null || !s.visitors.Contains(x.thingIDNumber)) return;
            var k = RoomAt(s, c);
            if (k != null && k.kind == KeptKind.Bedroom) k.care.Add(x.thingIDNumber);
        }

        /// <summary>
        /// A doctor's work that takes a visitor into a bedroom: a doctor's round of doses (VisitorServices), the animal
        /// doctor's patients (Callers), a people's worker sent to a colonist (PeopleWorkOn), and a visitor already at the
        /// game's own tending or rescue.
        /// </summary>
        private static void Care(Map map, Snap s)
        {
            if (s.rooms.Count == 0) return;
            var services = VisitorServices.Instance?.tasks;
            if (services != null)
                foreach (var t in services)
                    if (t.kind == ServiceKind.Dose && t.map == map)
                        foreach (var p in t.pawns)
                            if (p != null && p.Spawned && p.Map == map) Allow(s, t.x, p.Position);
            var callers = Callers.Instance?.tasks;
            if (callers != null)
                foreach (var t in callers)
                    if (t.kind == CallerKind.Vet && t.map == map)
                        foreach (var a in t.pawns)
                            if (a != null && a.Spawned && a.Map == map) Allow(s, t.x, a.Position);
            var visits = CouncilGameComponent.Instance?.happenings?.visits;
            if (visits != null)
                foreach (var v in visits)
                {
                    if (v.kind != PeopleWorkOn.Kind || v.done || v.map != map) continue;
                    if (v.a != null && v.a.Spawned && v.a.Map == map) Allow(s, v.x, v.a.Position);
                    if (v.place != null && v.place.Spawned && v.place.Map == map) Allow(s, v.x, v.place.Position);
                }
            // A visitor already at the game's own tending or rescue of one of the colony's (a helping guest, an ally of
            // another mod's) keeps its way to them. A guest helping out never goes into a bedroom for its work anyway
            // (GuestHelp.Bad), and a rescue takes the downed to a bed that guest can reach.
            foreach (var x in map.mapPawns.AllPawnsSpawned)
            {
                if (!s.visitors.Contains(x.thingIDNumber) || !x.RaceProps.Humanlike) continue;
                var job = x.CurJob;
                if (job != null && (job.def == JobDefOf.TendPatient || job.def == JobDefOf.Rescue) && job.targetA.Thing is Pawn patient) Allow(s, x, patient.Position);
            }
        }

        private static int Sign(Snap s)
        {
            unchecked
            {
                int h = 17;
                foreach (var id in s.visitors.OrderBy(i => i)) h = h * 31 + id;
                foreach (var k in s.rooms.OrderBy(r => r.id))
                {
                    h = h * 31 + k.id;
                    h = h * 31 + (int)k.kind;
                    h = h * 31 + (k.asleep ? 1 : 0);
                    foreach (var id in k.owners.OrderBy(i => i)) h = h * 31 + id;
                    h = h * 31 + 7;
                    foreach (var id in k.care.OrderBy(i => i)) h = h * 31 + id;
                    foreach (var d in k.doors) h = h * 31 + d.thingIDNumber;
                    h = h * 31 + k.cells.Count;
                }
                return h;
            }
        }

        // ---------------- Walking out ----------------

        /// <summary>A visitor in a room they couldn't go into now (its owner fell asleep, it turned cold) walks out of it.</summary>
        private static void WalkOutAll(Map map, Snap s)
        {
            if (s.inside.Count == 0 || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!s.inside.Contains(p.thingIDNumber)) continue;
                var k = RoomAt(s, p.Position);
                if (k == null || !KeepOutMath.WalksOut(k.kind, k.asleep, k.owners.Contains(p.thingIDNumber), k.care.Contains(p.thingIDNumber))) continue;
                WalkOut(p, k, s);
            }
        }

        private static void WalkOut(Pawn p, Kept k, Snap s)
        {
            if (!p.Awake() || p.Drafted || p.InBed() || p.CurJob?.playerForced == true) return;
            // On their way out already.
            var job0 = p.CurJob;
            if (job0 != null && job0.def == JobDefOf.Goto && job0.targetA.IsValid && RoomAt(s, job0.targetA.Cell) == null) return;
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            foreach (var c in k.exits)
            {
                float d = c.DistanceToSquared(p.Position);
                if (d >= bestDist || !p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                best = c;
                bestDist = d;
            }
            if (!best.IsValid) return;
            try
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, best);
                job.locomotionUrgency = LocomotionUrgency.Walk;
                job.expiryInterval = GenDate.TicksPerHour;
                // apart: one visitor walks out of the room on their own, nobody with them.
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor walking out of a kept room", e);
            }
        }

        // ---------------- Food brought out ----------------

        /// <summary>A colonist is on their way with food for this visitor (or has it queued).</summary>
        public static bool Fetching(Pawn guest)
        {
            var map = guest?.Map;
            if (map == null || KeepOutDefOf.Rimfolk_BringOutFood == null) return false;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c.CurJob != null && c.CurJob.def == KeepOutDefOf.Rimfolk_BringOutFood && c.CurJob.targetB.Thing == guest) return true;
                foreach (var q in c.jobs.jobQueue)
                    if (q.job.def == KeepOutDefOf.Rimfolk_BringOutFood && q.job.targetB.Thing == guest)
                        return true;
            }
            return false;
        }

        /// <summary>How much of it to bring: one meal, else enough of the raw food for a meal.</summary>
        private static int Count(Pawn guest, Thing food)
        {
            if (food.def.ingestible?.IsMeal ?? false) return 1;
            int n = FoodUtility.WillIngestStackCountOf(guest, food.def, FoodUtility.NutritionForEater(guest, food));
            return Math.Max(1, Math.Min(food.stackCount, n));
        }

        /// <summary>The job of bringing <paramref name="food"/> out to <paramref name="guest"/>, for a colonist to do.</summary>
        public static Job BringJob(Thing food, Pawn guest)
        {
            var job = JobMaker.MakeJob(KeepOutDefOf.Rimfolk_BringOutFood, food, guest);
            job.count = Count(guest, food);
            return job;
        }

        private static bool CanCarry(Pawn c, Thing food, Pawn guest, int count) =>
            c != null && PlayOuts.CanTakePart(c, c.Map) && !c.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
            && c.CanReserveAndReach(food, PathEndMode.ClosestTouch, Danger.Some, 1, count) && c.CanReach(guest, PathEndMode.Touch, Danger.Some);

        /// <summary>
        /// Food a visitor would eat that they may not go in for (it lies in a cold store or someone's room): the first of
        /// <paramref name="food"/> that the nearest free colonist (or <paramref name="by"/>) can carry out is brought to them
        /// and handed over, and they eat it. True when someone set off (or already is on the way); <paramref name="brought"/>
        /// what they went for (null when someone was already on the way).
        /// </summary>
        public static bool Fetch(Pawn guest, IEnumerable<Thing> food, out Thing brought, Pawn by = null)
        {
            brought = null;
            if (!On || guest?.Map == null || food == null || KeepOutDefOf.Rimfolk_BringOutFood == null) return false;
            if (Fetching(guest)) return true;
            var map = guest.Map;
            foreach (var t in food)
            {
                if (t == null || !t.Spawned || t.Map != map || !KeptFrom(guest, t.Position) || t.IsForbidden(Faction.OfPlayer) || !guest.WillEat(t, guest)) continue;
                int count = Count(guest, t);
                var carrier = by != null && CanCarry(by, t, guest, count) ? by : null;
                if (carrier == null)
                    carrier = map.mapPawns.FreeColonistsSpawned.Where(c => c != by).OrderBy(c => c.Position.DistanceToSquared(t.Position)).Take(6)
                        .FirstOrDefault(c => CanCarry(c, t, guest, count));
                if (carrier == null) continue;
                var job = BringJob(t, guest);
                // together: the colonist carries it out and walks over to the visitor to hand it over (JobDriver_BringOutFood).
                carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (carrier.CurJob != job) return false;
                brought = t;
                return true;
            }
            return false;
        }

        /// <summary>The food's handed over (JobDriver_BringOutFood): into the visitor's pack, and they eat it now if they can.</summary>
        public static bool Handed(Pawn by, Pawn guest)
        {
            var carried = by?.carryTracker?.CarriedThing;
            if (carried == null || guest?.inventory == null || guest.Dead || !guest.Spawned) return false;
            if (by.carryTracker.innerContainer.TryTransferToContainer(carried, guest.inventory.innerContainer, carried.stackCount, out Thing got) <= 0 || got == null)
                return false;
            fedAt[guest.thingIDNumber] = Now;
            if (guest.Awake() && !guest.Downed && !guest.InMentalState && guest.CurJobDef != JobDefOf.Ingest && guest.needs?.food != null)
            {
                var eat = JobMaker.MakeJob(JobDefOf.Ingest, got);
                eat.count = got.stackCount;
                // together: handed the food face to face, they eat it there (the game takes it to a table if one is near).
                guest.jobs.StartJob(eat, JobCondition.InterruptForced, null, false, true);
            }
            return true;
        }

        /// <summary>
        /// A visitor starving with nothing of their own to eat and nothing they can get to elsewhere (the game would have
        /// them take the colony's food then; Hospitality too): a colonist brings them food from the cold store.
        /// </summary>
        private static void FeedHungry(Map map, Snap s, int now)
        {
            if (!s.rooms.Any(k => k.kind == KeptKind.ColdStore) || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            List<Thing> stored = null;
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!s.visitors.Contains(p.thingIDNumber) || !p.RaceProps.Humanlike || p.needs?.food == null || !p.needs.food.Starving) continue;
                if (!p.Awake() || p.CurJobDef == JobDefOf.Ingest || fedAt.TryGetValue(p.thingIDNumber, out int at) && now - at < FedAgain) continue;
                if (p.inventory?.innerContainer != null && p.inventory.innerContainer.Any(t => t.def.IsNutritionGivingIngestible && !t.def.IsDrug && p.WillEat(t, p))) continue;
                stored ??= ColdFood(map, s);
                if (stored.Count == 0) return;
                if (Fetch(p, stored, out _)) fedAt[p.thingIDNumber] = now;
            }
        }

        /// <summary>The food in the map's cold stores, meals first, never human meat or anything spoiling.</summary>
        private static List<Thing> ColdFood(Map map, Snap s)
        {
            var list = new List<Thing>();
            foreach (var k in s.rooms)
            {
                if (k.kind != KeptKind.ColdStore || k.cells.Count == 0) continue;
                int first = k.cells.First();
                var cell = new IntVec3(first % s.width, 0, first / s.width);
                var room = cell.GetRoom(map);
                if (room == null) continue;
                foreach (var reg in room.Regions.ToList())
                    foreach (var t in reg.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                    {
                        if (!Edible(t) || FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t) || t.def.ingestible.preferability < FoodPreferability.RawBad) continue;
                        var rot = t.TryGetComp<CompRottable>();
                        if (rot != null && rot.Stage != RotStage.Fresh) continue;
                        list.Add(t);
                    }
            }
            return list.OrderByDescending(t => (int)t.def.ingestible.preferability).ToList();
        }

        // ---------------- For the log ----------------

        public static List<string> Report(Map map)
        {
            var lines = new List<string>();
            if (map == null) return lines;
            var s = SnapOf(map) ?? (On ? Build(map, null, Now) : null);
            if (s == null)
            {
                lines.Add(On ? "No visitors on the map: no room is kept from anyone." : "The deeper game hooks are off: nothing is kept.");
                return lines;
            }
            foreach (var k in s.rooms)
            {
                var c = new IntVec3(k.cells.First() % s.width, 0, k.cells.First() / s.width);
                lines.Add($"{k.kind} (the game calls it {k.role ?? "nothing"}), at {c}, {k.cells.Count} cells, {k.temperature:0.#}C, food stacks {k.food}, stores food {k.stores}, "
                          + $"one of the colony asleep in it {k.asleep}; doors {string.Join(" ", k.doors.Select(d => d.Position.ToString()))}; ways out {k.exits.Count}; "
                          + $"beds for {Names(map, k.owners)}; a doctor's work for {Names(map, k.care)}");
            }
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (!s.visitors.Contains(p.thingIDNumber)) continue;
                var k = RoomAt(s, p.Position);
                lines.Add($"Visitor {p.LabelShort} ({p.Faction?.Name}): {(k == null ? "outside every kept room" : "inside the " + k.kind + " at " + p.Position)}, doing {p.CurJobDef?.defName ?? "nothing"}");
            }
            if (lines.Count == 0) lines.Add("Visitors on the map, but no bedroom of the colony's or cold store to keep them from.");
            return lines;
        }

        private static string Names(Map map, HashSet<int> ids)
        {
            if (ids.Count == 0) return "nobody";
            return string.Join(", ", map.mapPawns.AllPawnsSpawned.Where(p => ids.Contains(p.thingIDNumber)).Select(p => p.LabelShort).DefaultIfEmpty("someone not here"));
        }
    }

    /// <summary>
    /// The look at the rooms visitors keep out of, every 250 ticks on the visitors' tick of the cycle (Sched.RareVisitors),
    /// only while visitors are on a home map (or a look of before is still kept). Saves nothing.
    /// </summary>
    public class KeepOutBook : GameComponent
    {
        public KeepOutBook(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            KeepOut.Reset();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareVisitors)) return;
            using var clock = PassClock.Time(Pass.RareKeepOut);
            try
            {
                KeepOut.Rare(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors kept out of bedrooms and cold stores", e);
            }
        }
    }

    /// <summary>
    /// A room's door is forbidden to a visitor who keeps out of it now (KeepOut), as a forbidden door is to a colonist: the
    /// game's paths cost it as a wall, reach and wandering stop at it, and a path through it is found again another way.
    /// Called on the game's path threads too, so it only reads KeepOut's last look.
    /// </summary>
    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbiddenToPass))]
    public static class Patch_KeepOutDoors
    {
        public static void Postfix(Building_Door t, Pawn pawn, ref bool __result)
        {
            if (__result || t == null || pawn == null || !RimfolkPatches.On) return;
            var f = pawn.Faction;
            if (f == null || f.IsPlayer) return;
            try
            {
                if (KeepOut.Barred(t, pawn)) __result = true;
            }
            catch (Exception e)
            {
                if (UnityData.IsInMainThread) CouncilLog.ErrorOnce("a door kept from visitors", e);
            }
        }
    }

    /// <summary>
    /// Food carried out to a visitor who may not go in for it (KeepOut.Fetch): the colonist goes to it in the cold store,
    /// takes up a meal's worth, walks over to the visitor and hands it over, and they eat it. Not handed over, it is set
    /// down where the colonist stands, still the colony's.
    /// </summary>
    public class JobDriver_BringOutFood : JobDriver
    {
        private bool handed;

        private Pawn Guest => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, job.count, null, errorOnFailed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handed, "handed");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !handed && (Guest == null || Guest.Dead || !Guest.Spawned || Guest.Map != pawn.Map || !KeepOut.IsVisitor(Guest)));
            AddFinishAction(cond =>
            {
                try
                {
                    if (!handed && pawn.Spawned && pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("food brought out set down", e);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var hand = ToilMaker.MakeToil("BringOutFoodHand");
            hand.initAction = () =>
            {
                try
                {
                    // together: the colonist has just walked over to the visitor with it.
                    handed = KeepOut.Handed(pawn, Guest);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("food brought out handed over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return hand;
        }
    }
}
