using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's rooms as building ahead reads them (<see cref="For"/>, once an hour per home map, or when asked): who
    /// sleeps and eats here and who is likely to join (prisoners being recruited); the beds, the seats at the tables, the
    /// medical beds and how many were laid up at once lately; the cold storage that takes food (rooms below zero and
    /// powered fridges, RimFridge's among them) against the food the stock targets keep and the winter ahead (from
    /// <see cref="ColonyNeeds"/> and the Ahead section's rows); the workshop benches and the work that waits at them; the
    /// stacks lying about and the free places in the stores; the builders and the building already queued. What rooms
    /// being built will add counts as planned. The arithmetic is in <see cref="GrowthMath"/>.
    /// </summary>
    public class GrowthNeeds
    {
        public Map map;
        public int tick = -999999;
        public GrowthInput input = new GrowthInput();
        public GrowthPlan plan = new GrowthPlan();
        public ColonyNeeds food;

        // For the words.
        public ThingDef meal;
        public List<Pawn> builders = new List<Pawn>();
        public List<(Building_WorkTable bench, float hours)> busyBenches = new List<(Building_WorkTable, float)>();
        /// <summary>The hours a day each bench was worked by the record (only while the record covers a day).</summary>
        public Dictionary<Building_WorkTable, float> workedADay = new Dictionary<Building_WorkTable, float>();
        /// <summary>The busy benches were judged by the record of the last days rather than the work queued alone.</summary>
        public bool busyMeasured;
        public List<ProjectState> projects = new List<ProjectState>();
        public int barracks;

        private static readonly Dictionary<int, GrowthNeeds> cache = new Dictionary<int, GrowthNeeds>();

        public static GrowthNeeds For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new GrowthNeeds { map = map };
            int now = Find.TickManager.TicksGame;
            if (!force && cache.TryGetValue(map.uniqueID, out var n) && n.map == map && now - n.tick < Sched.HourTicks && now >= n.tick) return n;
            var fresh = new GrowthNeeds { map = map, tick = now };
            try
            {
                fresh.Read();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("growth needs", e);
            }
            cache[map.uniqueID] = fresh;
            return fresh;
        }

        public static void Forget(Map map)
        {
            if (map != null) cache.Remove(map.uniqueID);
        }

        private int Planned(string kind) =>
            projects.Where(s => s.project.kind == kind && !s.project.Done && s.AnythingLeft).Sum(s => s.project.adds);

        private void Read()
        {
            var x = input;
            food = ColonyNeeds.For(map);
            var tracker = BuildAheadTracker.Instance;
            if (tracker != null)
                foreach (var p in tracker.On(map))
                    projects.Add(BuildProjects.Read(p));
            People(x);
            Beds(x);
            Seats(x);
            Hospital(x);
            Cold(x);
            Workshop(x);
            Stores(x);
            Building(x);
            x.declined = SuggestTracker.Instance?.DeclinedKeys() ?? new HashSet<string>();
            plan = GrowthMath.Plan(x);
        }

        // ---------------- People ----------------

        private static bool Baby(Pawn p) => p.DevelopmentalStage.Baby();

        private void People(GrowthInput x)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.Dead || Baby(p)) continue;
                x.colonists++;
                if (p.IsQuestLodger()) x.lodgers++;
                if (p.ownership?.OwnedBed != null && p.ownership.OwnedRoom == null && !(p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false)) barracks++;
            }
            x.shared = barracks;
            x.slaves = map.mapPawns.SlavesOfColonySpawned.Count(p => !p.Dead && !Baby(p));
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (p != null && !p.Dead && p.RaceProps.Humanlike && !p.IsPrisonerOfColony && p.Faction != Faction.OfPlayerSilentFail && Strangers.IsHospitalityGuest(p)) x.guests++;
            foreach (var p in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (p.Dead) continue;
                x.prisoners++;
                bool recruiting;
                try
                {
                    recruiting = p.guest != null && p.guest.Recruitable && p.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.AttemptRecruit);
                }
                catch
                {
                    recruiting = false;
                }
                if (recruiting) x.joining++;
            }
            x.questJoining = QuestJoiners(map);
        }

        /// <summary>
        /// People an accepted quest will bring to join the colony on this map (its parts that make pawns join, not as
        /// prisoners): those not yet the colony's, not babies. A quest offered but not accepted isn't counted; a quest
        /// that names no map counts for the first home map.
        /// </summary>
        public static int QuestJoiners(Map map)
        {
            var seen = new HashSet<Pawn>();
            var player = Faction.OfPlayerSilentFail;
            var quests = Find.QuestManager?.QuestsListForReading;
            if (quests == null || map == null) return 0;
            foreach (var q in quests)
            {
                if (q == null || q.State != QuestState.Ongoing) continue;
                foreach (var part in q.PartsListForReading)
                {
                    List<Pawn> pawns = null;
                    RimWorld.Planet.MapParent parent = null;
                    if (part is QuestPart_JoinPlayer j && j.joinPlayer && !j.makePrisoners)
                    {
                        pawns = j.pawns;
                        parent = j.mapParent;
                    }
                    else if (part is QuestPart_PawnsArrive a && a.joinPlayer)
                    {
                        pawns = a.pawns;
                        parent = a.mapParent;
                    }
                    if (pawns == null) continue;
                    var at = parent?.Map;
                    if (at != null ? at != map : map != Find.AnyPlayerHomeMap) continue;
                    foreach (var p in pawns)
                    {
                        try
                        {
                            if (p == null || p.Dead || p.Discarded || p.Faction != null && p.Faction == player || p.RaceProps == null || !p.RaceProps.Humanlike || Baby(p)) continue;
                        }
                        catch
                        {
                            continue;
                        }
                        seen.Add(p);
                    }
                }
            }
            return seen.Count;
        }

        // ---------------- Beds and seats ----------------

        private static bool GuestBed(Thing b) => b.GetType().Name.IndexOf("GuestBed", StringComparison.Ordinal) >= 0;

        /// <summary>A bed people sleep in: for humans, not medical, not a prisoner's, not a guest's, not a crib.</summary>
        private static bool SleepBed(Building_Bed b) => b.def.building != null && b.def.building.bed_humanlike && !b.Medical && !b.ForPrisoners && !b.ForHumanBabies && !GuestBed(b);

        private bool InProject(IntVec3 c, string kind) => projects.Any(s => s.project.kind == kind && !s.project.Done && s.project.Rect.Contains(c));

        /// <summary>The colony's blueprints and frames.</summary>
        private IEnumerable<Thing> Planned()
        {
            var player = Faction.OfPlayerSilentFail;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
                if (t.Faction == player) yield return t;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
                if (t.Faction == player) yield return t;
        }

        private void Beds(GrowthInput x)
        {
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
                if (SleepBed(b)) x.beds += b.SleepingSlotsCount;
            foreach (var t in Planned())
            {
                if (!(t.def.entityDefToBuild is ThingDef td) || !td.IsBed || td.building == null || !td.building.bed_humanlike) continue;
                // Beds in a hospital being built are its medical beds.
                if (InProject(t.Position, GrowthMath.Hospital)) continue;
                x.bedsPlanned += BedUtility.GetSleepingSlotsCount(td.size);
            }
        }

        private void Seats(GrowthInput x)
        {
            var tables = new HashSet<IntVec3>();
            var plannedTables = new HashSet<IntVec3>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (Avail.IsEatingTable(b.def))
                    foreach (var c in b.OccupiedRect()) tables.Add(c);
            var planned = Planned().ToList();
            foreach (var t in planned)
                if (t.def.entityDefToBuild is ThingDef td && Avail.IsEatingTable(td))
                    foreach (var c in t.OccupiedRect()) plannedTables.Add(c);
            bool ByTable(IntVec3 c, bool orPlanned)
            {
                foreach (var d in GenAdj.CardinalDirections)
                {
                    var n = c + d;
                    if (tables.Contains(n) || orPlanned && plannedTables.Contains(n)) return true;
                }
                return false;
            }
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (Avail.IsChair(b.def) && ByTable(b.Position, false)) x.seats++;
            foreach (var t in planned)
                if (t.def.entityDefToBuild is ThingDef td && Avail.IsChair(td) && ByTable(t.Position, true)) x.seatsPlanned++;
        }

        // ---------------- The hospital ----------------

        private void Hospital(GrowthInput x)
        {
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
                if (b.Medical && !b.ForPrisoners && b.def.building != null && b.def.building.bed_humanlike) x.medicalBeds += b.SleepingSlotsCount;
            foreach (var p in ColonyNeeds.Hands(map))
            {
                if (p.Dead) continue;
                bool laid;
                try
                {
                    laid = p.InBed() && p.CurrentBed() != null && p.CurrentBed().Medical || HealthAIUtility.ShouldSeekMedicalRest(p);
                }
                catch
                {
                    laid = false;
                }
                if (laid) x.patientsNow++;
            }
            var tracker = BuildAheadTracker.Instance;
            x.patientsMost = Mathf.Max(x.patientsNow, tracker?.MostPatients ?? 0);
            x.patientDays = tracker?.patients.Count ?? 0;
            x.medicalPlanned = Planned(GrowthMath.Hospital);
        }

        // ---------------- The cold store ----------------

        private void Cold(GrowthInput x)
        {
            var ahead = Ahead.For(map);
            var mealRow = ahead.plan.Row(AheadMath.Meal);
            var rawRow = ahead.plan.Row(AheadMath.Raw);
            x.mealUseADay = mealRow?.useADay ?? food.humanNeed;
            x.rawUseADay = rawRow?.useADay ?? AheadMath.RawFor(food.humanNeed);
            x.mealDays = ahead.mealDays;
            x.reserveDays = ahead.reserveDays;
            x.withoutDays = AheadMath.DaysWithout(food.growFactors);
            x.winterIn = GrowthMath.WinterIn(food.growFactors);
            // A place of the colony's own raw food that rots, as it lies in store; else a stack of 75 at 0.05 each.
            float nutrition = 0f;
            int cells = 0;
            foreach (var g in food.goods)
                if (g.kind == AheadMath.Raw && g.rotDays < AheadMath.Far && g.cells > 0)
                {
                    nutrition += g.Nutrition;
                    cells += g.cells;
                }
            x.rawPerSlot = cells > 0 && nutrition > 0f ? nutrition / cells : 0.05f * 75f;
            meal = food.goods.Where(g => g.kind == AheadMath.Meal).OrderByDescending(g => g.count).Select(g => food.defs.TryGetValue(g.key, out var d) ? d : null).FirstOrDefault(d => d != null)
                   ?? ThingDefOf.MealSimple;
            x.mealPerSlot = meal != null ? Mathf.Max(0.1f, meal.GetStatValueAbstract(StatDefOf.Nutrition) * Mathf.Max(1, meal.stackLimit)) : 9f;

            // Places in stores kept cold that take food.
            var rawDefs = food.goods.Where(g => g.kind == AheadMath.Raw && g.rotDays < AheadMath.Far).Select(g => food.defs.TryGetValue(g.key, out var d) ? d : null).Where(d => d != null).ToList();
            foreach (var group in map.haulDestinationManager.AllGroupsListForReading)
            {
                if (!ColdGroup(group)) continue;
                var settings = group.Settings;
                if (settings == null || !(meal != null && settings.AllowedToAccept(meal) || rawDefs.Any(d => settings.AllowedToAccept(d)))) continue;
                foreach (var c in group.CellsList) x.coldSlots += Mathf.Max(1, c.GetMaxItemsAllowedInCell(map));
            }
            x.coldPlanned = Planned(GrowthMath.Cold);
        }

        /// <summary>A store whose contents keep: in a room below zero, or a powered fridge (RimFridge's and the like).</summary>
        public bool ColdGroup(SlotGroup group)
        {
            var cells = group?.CellsList;
            if (cells == null || cells.Count == 0) return false;
            if (group.parent is Building_Storage store && ColonyNeeds.Fridge(store)) return true;
            var room = cells[0].GetRoom(map);
            return room != null && !room.UsesOutdoorTemperature && room.Temperature <= 0f;
        }

        // ---------------- The workshop ----------------

        /// <summary>A bench that costs nothing to build is a spot (the crafting spot, the butcher spot): no room needed.</summary>
        private static bool Spot(ThingDef d) => (d.costList == null || d.costList.Count == 0) && d.costStuffCount <= 0;

        /// <summary>A workshop's bench: its room role a workshop, or, where the bench says nothing, anything but a stove.</summary>
        public static bool WorkshopBench(ThingDef d)
        {
            if (d?.building == null || !d.IsWorkTable || Spot(d)) return false;
            var role = d.building.workTableRoomRole;
            return role != null ? role == RoomUtil.Workshop : !Avail.IsCookingStation(d) && !Avail.IsResearchBench(d);
        }

        private static float WorkOf(RecipeDef recipe)
        {
            try
            {
                return recipe.WorkAmountTotal(null);
            }
            catch
            {
                return recipe.workAmount;
            }
        }

        private static float Speed(Pawn p, RecipeDef recipe)
        {
            if (recipe.workSpeedStat == null) return 1f;
            try
            {
                return Mathf.Max(0.1f, p.GetStatValue(recipe.workSpeedStat));
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>Hours of work waiting at a bench that can go ahead now: each wanted bill's batches left at its best able hand's speed (a bill for ever counts a day's work).</summary>
        private float QueuedHours(Building_WorkTable bench, List<Pawn> hands, Func<ThingDef, int> have, out bool anyHand)
        {
            float hours = 0f;
            anyHand = false;
            foreach (var bill in bench.BillStack.Bills.OfType<Bill_Production>())
            {
                if (bill.recipe == null || bill.suspended || bill.DeletedOrDereferenced) continue;
                if (!Making.StillWanted(bill, have) || BillStuck.Why(bench, bill) != null) continue;
                var work = ColonyNeeds.WorkTypeFor(bench, bill.recipe);
                var hand = hands.Where(p => ColonyNeeds.Able(p, bill, work)).OrderByDescending(p => Speed(p, bill.recipe)).FirstOrDefault();
                if (hand == null) continue;
                anyHand = true;
                var product = bill.recipe.ProducedThingDef ?? bill.recipe.products?.FirstOrDefault()?.thingDef;
                float perBatch = Mathf.Max(1f, Chains.PerBatch(bill.recipe, product));
                float batches;
                if (bill.repeatMode == BillRepeatModeDefOf.Forever) batches = -1f;
                else if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount) batches = bill.repeatCount;
                else batches = Mathf.Ceil(Mathf.Max(1f, bill.targetCount - (product != null ? have(product) : 0)) / perBatch);
                float factor = Mathf.Max(0.1f, Chains.BenchFactor(bench, bill.recipe));
                float each = WorkOf(bill.recipe) / (GrowthMath.TicksPerHour * Speed(hand, bill.recipe) * factor);
                hours += batches < 0f ? GrowthMath.BenchHoursADay : Mathf.Min(batches, 30f) * each;
            }
            return hours;
        }

        /// <summary>
        /// The hours a day a bench was worked lately, by the record: what it made a day, in batches of the recipe that
        /// makes each product there, at the hours a batch takes the hand who did the most batches there (else the
        /// quickest who can).
        /// </summary>
        private float WorkedHours(Building_WorkTable bench, List<Pawn> hands)
        {
            var record = food.record;
            if (record == null || !record.made.TryGetValue(bench.thingIDNumber, out var made) || made.Count == 0) return 0f;
            Pawn seen = null;
            if (record.doers.TryGetValue(bench.thingIDNumber, out var by))
                seen = hands.Where(p => by.ContainsKey(p.thingIDNumber)).OrderByDescending(p => by[p.thingIDNumber]).ThenBy(p => p.thingIDNumber).FirstOrDefault();
            var bills = bench.BillStack.Bills.OfType<Bill_Production>().Select(b => b.recipe).Where(r => r != null).ToList();
            var counted = new HashSet<RecipeDef>();
            float total = 0f;
            foreach (var kv in made)
            {
                bool Makes(RecipeDef r) => r?.products != null && r.products.Any(p => p?.thingDef == kv.Key);
                var recipe = bills.FirstOrDefault(Makes) ?? bench.def.AllRecipes?.FirstOrDefault(Makes);
                if (recipe == null || !counted.Add(recipe)) continue;
                var hand = seen ?? hands.OrderByDescending(p => Speed(p, recipe)).ThenBy(p => p.thingIDNumber).FirstOrDefault();
                float speed = hand != null ? Speed(hand, recipe) : 1f;
                float factor = Mathf.Max(0.1f, Chains.BenchFactor(bench, recipe));
                float perBatch = Mathf.Max(1f, Chains.PerBatch(recipe, kv.Key));
                total += GrowthMath.WorkedHours(kv.Value / perBatch, WorkOf(recipe) / (GrowthMath.TicksPerHour * speed * factor));
            }
            return total;
        }

        private void Workshop(GrowthInput x)
        {
            var benches = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.Spawned && b.BillStack != null && WorkshopBench(b.def)).ToList();
            x.benches = benches.Count;
            if (benches.Count == 0) return;
            var hands = ColonyNeeds.Hands(map).Where(p => !p.Dead && p.workSettings != null && !p.DevelopmentalStage.Juvenile()).ToList();
            var stock = food.stock;
            int Have(ThingDef d) => d != null && stock.TryGetValue(d, out int n) ? n : 0;
            var busy = new List<Building_WorkTable>();
            busyMeasured = food.measured && food.record != null;
            foreach (var b in benches)
            {
                var room = b.GetRoom();
                if (room != null && room.Role == RoomUtil.Workshop && !room.PsychologicallyOutdoors) x.benchesInWorkshop++;
                float hours = QueuedHours(b, hands, Have, out bool anyHand);
                float worked = busyMeasured ? WorkedHours(b, hands) : 0f;
                if (busyMeasured) workedADay[b] = worked;
                if (anyHand && GrowthMath.BusyBench(busyMeasured, worked, hours, GrowthMath.BenchHoursADay))
                {
                    busy.Add(b);
                    busyBenches.Add((b, hours));
                }
            }
            x.busy = busy.Count;
            // A busy bench with no idle bench of its kind beside it wants a second.
            x.busyAlone = busy.Count(b => !benches.Any(o => o != b && o.def == b.def && !busy.Contains(o)));
            // Room for more benches in the workshops: their free floor, a bench place for every so many cells.
            var role = RoomUtil.Workshop;
            if (role != null)
                foreach (var room in RoomUtil.RoomsWithRole(map, role))
                {
                    if (room.PsychologicallyOutdoors) continue;
                    var used = new HashSet<IntVec3>();
                    foreach (var t in room.ContainedAndAdjacentThings)
                        if (t.def.category == ThingCategory.Building && t.def.passability != Traversability.Standable || t is Building_WorkTable)
                        {
                            foreach (var c in t.OccupiedRect()) used.Add(c);
                            if (t.def.hasInteractionCell) used.Add(t.InteractionCell);
                        }
                    int free = room.Cells.Count(c => !used.Contains(c) && c.GetEdifice(map) == null);
                    x.workshopSpare += GrowthMath.SparePlaces(free);
                }
            x.workshopPlanned = Planned(GrowthMath.Workshop);
        }

        // ---------------- The stores ----------------

        private void Stores(GrowthInput x)
        {
            var player = Faction.OfPlayerSilentFail;
            var groups = map.haulDestinationManager.AllGroupsListForReading;
            var home = map.areaManager.Home;
            var accepted = new Dictionary<ThingDef, bool>();
            bool Accepted(Thing t)
            {
                if (accepted.TryGetValue(t.def, out bool a)) return a;
                a = groups.Any(g => g.Settings != null && g.Settings.AllowedToAccept(t));
                accepted[t.def] = a;
                return a;
            }
            var haulables = map.listerHaulables?.ThingsPotentiallyNeedingHauling();
            if (haulables != null)
                foreach (var t in haulables)
                {
                    if (t == null || !t.Spawned || t.def.category != ThingCategory.Item || t is Corpse || !t.def.EverStorable(false)) continue;
                    if (player != null && t.IsForbidden(player) || home != null && !home[t.Position] || t.IsInAnyStorage() || !Accepted(t)) continue;
                    x.lying++;
                    if (!map.roofGrid.Roofed(t.Position)) x.lyingOut++;
                }
            foreach (var g in groups)
            {
                if (g?.CellsList == null || ColdGroup(g)) continue;
                foreach (var c in g.CellsList)
                    x.freeSlots += Mathf.Max(0, c.GetMaxItemsAllowedInCell(map) - c.GetItemCount(map));
            }
            x.storagePlanned = Planned(GrowthMath.Storage);
        }

        // ---------------- The builders ----------------

        private void Building(GrowthInput x)
        {
            builders = BuildProjects.Builders(map);
            x.builders = builders.Count;
            float speed = 0f;
            foreach (var p in builders)
            {
                try
                {
                    speed += Mathf.Max(0.1f, p.GetStatValue(StatDefOf.ConstructionSpeed));
                }
                catch
                {
                    speed += 1f;
                }
            }
            x.builderHoursADay = GrowthMath.BuilderHoursADay(speed);
            float work = 0f;
            foreach (var t in Planned()) work += BuildProjects.WorkOf(t);
            x.queuedHours = GrowthMath.BuildHours(work);

            // The miners, and the rock marked to mine inside the rooms being built: their days counted in the builders' hours.
            float mineSpeed = 0f;
            foreach (var p in ColonyNeeds.Hands(map))
            {
                if (p.Dead || p.workSettings == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Mining) || !p.workSettings.WorkIsActive(WorkTypeDefOf.Mining)) continue;
                x.miners++;
                try
                {
                    mineSpeed += Mathf.Max(0.1f, p.GetStatValue(StatDefOf.MiningSpeed));
                }
                catch
                {
                    mineSpeed += 1f;
                }
            }
            x.minerHoursADay = GrowthMath.MinerHoursADay(mineSpeed);
            float mine = 0f;
            foreach (var st in projects)
                if (!st.project.Done)
                    foreach (var c in st.project.Rect.ClipInsideMap(map))
                        if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null)
                            mine += MineHoursAt(map, c);
            x.queuedMineHours = GrowthMath.MineAsBuildHours(mine, x.minerHoursADay, x.builderHoursADay);
            x.queuedHours += x.queuedMineHours;
        }

        /// <summary>Hours a miner of speed 1 takes for the rock on a cell (0 for none).</summary>
        public static float MineHoursAt(Map map, IntVec3 c)
        {
            var rock = c.InBounds(map) ? c.GetEdifice(map) : null;
            if (rock?.def == null || !rock.def.mineable) return 0f;
            return GrowthMath.MineHours(rock.HitPoints, rock.def.building?.isNaturalRock ?? false);
        }
    }
}
