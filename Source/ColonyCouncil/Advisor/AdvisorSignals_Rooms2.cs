using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ---------------- More rooms the colony has outgrown, or could make better ----------------
    //
    // Like AdvisorSignals_Rooms.cs: a noble's bedroom short of what the title asks, a kitchen with one stove for a colony
    // that has outgrown it, a freezer too small for the stores, a dining room too cramped for the colony, a workshop
    // with benches in the dark or no chair at them, and a prison cell no prisoner could bear. Each offers the room
    // planner: the next size up, or the room upgraded where it stands. The thresholds are in AdvisorMath.cs.

    /// <summary>Sizes by floor area, for rooms whose growth isn't counted in one kind of piece.</summary>
    public static class RoomSizes
    {
        /// <summary>The smallest size of a room type the colony can plan whose floor is larger than <paramref name="cells"/> (else the largest).</summary>
        public static RoomTemplateDef LargerThan(string type, int cells, Map map)
        {
            var rt = DefDatabase<RoomTypeDef>.GetNamedSilentFail(type);
            if (rt == null) return null;
            var sizes = rt.Variants.Select(v => v.Item1).Where(t => t != null && t.UnavailableReason(map) == null).ToList();
            int i = AdvisorMath.LargerSize(sizes.Select(t => t.size.x * t.size.z).ToList(), cells);
            return i >= 0 ? sizes[i] : null;
        }
    }

    /// <summary>A titled colonist whose bedroom falls short of what their title asks (Royalty): the room upgraded, or a bigger one.</summary>
    public class AdvisorWorker_NobleBedroom : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!ModsConfig.RoyaltyActive) yield break;
            foreach (var p in Colonists(map))
            {
                if (p.royalty == null) continue;
                List<string> unmet;
                bool tooSmall = false;
                try
                {
                    if (!p.royalty.CanRequireBedroom()) continue;
                    unmet = p.royalty.GetUnmetBedroomRequirements(false).ToList();
                    var room = RoomUtil.PrivateRoom(p);
                    var title = p.royalty.HighestTitleWithBedroomRequirements();
                    if (room != null && title?.def != null)
                        foreach (var req in title.def.GetBedroomRequirements(p))
                            if (req is RoomRequirement_Area && !req.MetOrDisabled(room, p))
                                tooSmall = true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("advisor noble bedroom", e);
                    continue;
                }
                if (unmet.Count == 0) continue;
                var own = RoomUtil.PrivateRoom(p);
                bool couple = p.ownership?.OwnedBed != null && Avail.IsDoubleBed(p.ownership.OwnedBed.def);
                var plan = DefDatabase<RoomTemplateDef>.GetNamedSilentFail(couple ? "Council_MasterSuite" : "Council_Bedroom_Roomy");
                if (plan == null || plan.UnavailableReason(map) != null)
                    plan = DefDatabase<RoomTemplateDef>.GetNamedSilentFail(couple ? "Council_CoupleBedroom_Roomy" : "Council_Bedroom");
                bool upgrade = own != null && !tooSmall;
                string titleLabel = p.royalty.MostSeniorTitle?.Label ?? "";
                string how = upgrade ? "CC_Adv_Noble_Upgrade".Translate().Resolve() : "CC_Adv_Noble_New".Translate(RoomGrowth.WithArticle(plan)).Resolve();
                var s = Signal(0.35f, "CC_Adv_Noble_Title".Translate(p.LabelShort, titleLabel).Resolve(),
                    "CC_Adv_Noble_Detail".Translate(p.LabelShort, titleLabel, string.Join("; ", unmet)).Resolve() + " " + how, 0.5f);
                s.template = plan;
                s.upgrade = upgrade;
                s.look = own != null ? new LookTargets(new TargetInfo(own.Cells.First(), map)) : new LookTargets(p);
                s.key = "noble:" + p.ThingID;
                yield return s;
            }
        }
    }

    /// <summary>One stove for a colony that has outgrown it: the meals run short though there are cooks to spare.</summary>
    public class AdvisorWorker_KitchenOutgrown : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.Kitchen;
            if (role == null) yield break;
            Room main = null;
            int best = -1;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                int n = RoomUtil.RoomCount(room, Avail.IsCookingStation);
                if (n > best)
                {
                    best = n;
                    main = room;
                }
            }
            if (main == null) yield break;
            int people = RoomGrowth.People(map).Count + map.mapPawns.PrisonersOfColonySpawnedCount;
            int stoves = Avail.ColonyCount(map, Avail.IsCookingStation);
            var cooking = AdvisorFacts.Cooking;
            int cooks = cooking == null ? 0 : Colonists(map).Count(p => p.workSettings != null && p.workSettings.WorkIsActive(cooking));
            int meals = AdvisorFacts.Meals(map);
            if (!AdvisorMath.KitchenOutgrown(people, stoves, cooks, meals)) yield break;
            int want = AdvisorMath.StovesFor(people);
            var plan = RoomGrowth.SizeFor("Council_RT_Kitchen", "cook", want, best, map);
            var stove = RoomGrowth.FirstIn(main, Avail.IsCookingStation);
            string detail = "CC_Adv_KitchenOutgrown_Detail".Translate(stoves, people, meals, cooks).Resolve() + " "
                            + (plan != null ? "CC_Adv_KitchenOutgrown_Plan".Translate(RoomGrowth.WithArticle(plan, true).CapitalizeFirst()).Resolve() : "CC_Adv_KitchenOutgrown_NoPlan".Translate().Resolve());
            var s = Signal(0.3f + 0.05f * Mathf.Min(4, want - stoves), "CC_Adv_KitchenOutgrown_Title".Translate(stoves, people).Resolve(), detail, 0.5f);
            s.template = plan;
            if (stove != null)
            {
                s.look = new LookTargets(stove);
                s.suggestThing = stove.def;
            }
            s.key = "kitchen-outgrown";
            yield return s;
        }
    }

    /// <summary>A freezer whose storage is nearly full, while food that spoils sits out in the warm.</summary>
    public class AdvisorWorker_FreezerOutgrown : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            // Freezer rooms: indoors, below zero, with stockpile or shelf storage.
            var freezers = new HashSet<Room>();
            int cells = 0, used = 0;
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Stockpile)) continue;
                foreach (var c in zone.Cells)
                {
                    var room = c.GetRoom(map);
                    if (room == null || room.PsychologicallyOutdoors || room.Temperature >= 0f) continue;
                    freezers.Add(room);
                    cells++;
                    if (c.GetFirstItem(map) != null) used++;
                }
            }
            if (freezers.Count == 0) yield break;
            int spoiling = 0;
            Thing example = null;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (t is Corpse || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.Position.Fogged(map)) continue;
                var rot = t.TryGetComp<CompRottable>();
                if (rot == null || rot.PropsRot.daysToRotStart > 15f || t.AmbientTemperature <= 0f) continue;
                if (!t.IsInAnyStorage() && !map.areaManager.Home[t.Position]) continue;
                spoiling += t.stackCount;
                example ??= t;
            }
            if (!AdvisorMath.FreezerTooSmall(cells, used, spoiling)) yield break;
            var main = freezers.OrderByDescending(r => r.CellCount).First();
            var plan = RoomSizes.LargerThan("Council_RT_Freezer", main.CellCount, map);
            string detail = "CC_Adv_FreezerOutgrown_Detail".Translate(used, cells, spoiling).Resolve() + " "
                            + (plan != null ? "CC_Adv_FreezerOutgrown_Plan".Translate(RoomGrowth.WithArticle(plan, true).CapitalizeFirst()).Resolve() : "CC_Adv_FreezerOutgrown_NoPlan".Translate().Resolve());
            var s = Signal(0.3f + Mathf.Min(0.25f, spoiling / 800f), "CC_Adv_FreezerOutgrown_Title".Translate().Resolve(), detail, 0.6f);
            s.template = plan;
            if (example != null) s.look = new LookTargets(example);
            s.key = "freezer-outgrown";
            yield return s;
        }
    }

    /// <summary>Seats enough, but the dining room is too cramped for the colony that eats in it now.</summary>
    public class AdvisorWorker_DiningCramped : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.DiningRoom;
            if (role == null) yield break;
            int people = RoomGrowth.People(map).Count;
            int seats = 0, best = -1;
            Room main = null;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                int n = RoomUtil.RoomCount(room, Avail.IsChair);
                seats += n;
                if (n > best)
                {
                    best = n;
                    main = room;
                }
            }
            // Too few seats is the outgrown dining room's advice.
            if (main == null || seats < people - 1) yield break;
            float space = RoomUtil.Space(main);
            if (!AdvisorMath.DiningCramped(space, people)) yield break;
            var plan = RoomSizes.LargerThan("Council_RT_DiningRoom", main.CellCount, map);
            string stage = RoomUtil.StageLabel(RoomStatDefOf.Space, space);
            string detail = "CC_Adv_DiningCramped_Detail".Translate(stage, people).Resolve() + " "
                            + (plan != null ? "CC_Adv_DiningCramped_Plan".Translate(RoomGrowth.WithArticle(plan, true).CapitalizeFirst()).Resolve() : "CC_Adv_DiningCramped_NoPlan".Translate().Resolve());
            var s = Signal(0.22f + 0.02f * Mathf.Min(8, people - 4), "CC_Adv_DiningCramped_Title".Translate(people).Resolve(), detail, 0.3f);
            s.template = plan;
            var table = RoomGrowth.FirstIn(main, Avail.IsEatingTable);
            if (table != null) s.look = new LookTargets(table);
            s.key = "dining-cramped";
            yield return s;
        }
    }

    /// <summary>
    /// A workshop whose benches stand in the dark (slower work at night) or with nobody's chair at them (no comfort while
    /// working): the room upgraded, or the chairs and lamps planned straight away.
    /// </summary>
    public class AdvisorWorker_WorkshopUpgrade : AdvisorSignalWorker
    {
        /// <summary>A workshop with benches: this advice speaks for it, so the plain darkness advice leaves it alone.</summary>
        public static bool Covers(Room room) => room != null && room.Role == RoomUtil.Workshop && Benches(room).Any();

        private static IEnumerable<Building> Benches(Room room)
        {
            foreach (var t in room.ContainedAndAdjacentThings)
                if (t is Building b && b.Faction == Faction.OfPlayer && room.ContainsCell(b.Position) && b.def.IsWorkTable && b.def.hasInteractionCell && !b.def.IsFrame)
                    yield return b;
        }

        private static bool HasSeat(IntVec3 c, Map map)
        {
            if (!c.InBounds(map)) return true;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                var def = t is Blueprint || t is Frame ? t.def.entityDefToBuild as ThingDef : t.def;
                if (def?.building != null && def.building.isSittable) return true;
            }
            return false;
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.Workshop;
            if (role == null) yield break;
            var rooms = new List<Room>();
            int allBenches = 0, allUnlit = 0, allNoChair = 0;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                if (room.OpenRoofCount > 0) continue;
                var benches = Benches(room).ToList();
                if (benches.Count == 0) continue;
                var lights = RoomLights.In(room);
                int unlit = benches.Count(b => !AdvisorMath.Lit(b.InteractionCell.x, b.InteractionCell.z, lights));
                int noChair = benches.Count(b => !HasSeat(b.InteractionCell, map));
                if (!AdvisorMath.WorkshopWanting(benches.Count, unlit, noChair)) continue;
                rooms.Add(room);
                allBenches += benches.Count;
                allUnlit += unlit;
                allNoChair += noChair;
            }
            if (rooms.Count == 0) yield break;
            var parts = new List<string>();
            if (allUnlit > 0) parts.Add("CC_Adv_Workshop_Dark".Translate(allUnlit).Resolve());
            if (allNoChair > 0) parts.Add("CC_Adv_Workshop_NoChair".Translate(allNoChair).Resolve());
            var main = rooms.OrderByDescending(r => Benches(r).Count()).First();
            var s = Signal(0.2f + 0.03f * Mathf.Min(6, allUnlit + allNoChair / 2), "CC_Adv_Workshop_Title".Translate(rooms.Count).Resolve(),
                "CC_Adv_Workshop_Detail".Translate(TextUtil.JoinAnd(parts)).Resolve(), 0.3f);
            s.template = RoomGrowth.ClosestSize("Council_RT_Workshop", "craft", Benches(main).Count(), map);
            s.upgrade = true;
            var first = Benches(main).FirstOrDefault();
            if (first != null) s.look = new LookTargets(first);
            var list = rooms.Take(6).ToList();
            s.act = () => PlanChairsAndLamps(list, map);
            s.actLabel = "CC_Adv_Act_PlanChairsLamps".Translate();
            s.key = "workshop-upgrade";
            yield return s;
        }

        /// <summary>A chair on each bench's empty work spot, and wall lamps where the room is dark.</summary>
        private static void PlanChairsAndLamps(List<Room> rooms, Map map)
        {
            int chairs = 0;
            try
            {
                var chair = Avail.BestBuildable(map, Avail.IsChair, "DiningChair", "Stool");
                var stuff = chair != null && chair.MadeFromStuff ? Avail.BestStuff(chair, map) : null;
                if (chair != null)
                    foreach (var room in rooms)
                    {
                        if (room == null || room.Dereferenced) continue;
                        foreach (var b in Benches(room).ToList())
                        {
                            var c = b.InteractionCell;
                            if (!c.InBounds(map) || HasSeat(c, map) || !c.Standable(map) || WallLights.PlannedHere(c, map)) continue;
                            var rot = Rot4.FromIntVec3(b.Position - c);
                            if (!GenConstruct.CanPlaceBlueprintAt(chair, c, rot, map, false, null, null, stuff).Accepted) continue;
                            GenConstruct.PlaceBlueprintForBuild(chair, c, map, rot, Faction.OfPlayer, stuff);
                            chairs++;
                        }
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("advisor plan chairs", e);
            }
            if (chairs > 0) Messages.Message("CC_Adv_PlannedChairs".Translate(chairs).Resolve(), MessageTypeDefOf.SilentInput, false);
            RoomLights.Plan(rooms.Where(r => r != null && !r.Dereferenced && Benches(r).Any(b => !AdvisorMath.Lit(b.InteractionCell.x, b.InteractionCell.z, RoomLights.In(r)))).ToList(), map);
        }
    }

    /// <summary>A prison cell with prisoners in it and little to make it bearable: a light, a table, a seat, a cell that isn't awful.</summary>
    public class AdvisorWorker_PrisonComfort : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var prisoners = map.mapPawns.PrisonersOfColonySpawned.Where(p => !p.Dead && !p.Downed).ToList();
            if (prisoners.Count == 0) yield break;
            var seen = new HashSet<Room>();
            Room worst = null;
            int worstLacks = 0, cellsBad = 0;
            var lackParts = new HashSet<string>();
            foreach (var p in prisoners)
            {
                var room = p.ownership?.OwnedBed?.GetRoom() ?? p.GetRoom();
                if (room == null || !room.IsPrisonCell || room.PsychologicallyOutdoors || !seen.Add(room)) continue;
                bool light = RoomUtil.RoomContains(room, Avail.IsLight);
                bool table = RoomUtil.RoomContains(room, Avail.IsEatingTable);
                bool seat = RoomUtil.RoomContains(room, Avail.IsChair);
                float imp = RoomUtil.Impressiveness(room);
                int lacks = AdvisorMath.PrisonLacks(light, table, seat, imp);
                if (!AdvisorMath.PrisonUncomfortable(lacks)) continue;
                cellsBad++;
                if (!light) lackParts.Add("CC_Adv_Prison_Light".Translate().Resolve());
                if (!table) lackParts.Add("CC_Adv_Prison_Table".Translate().Resolve());
                if (!seat) lackParts.Add("CC_Adv_Prison_Seat".Translate().Resolve());
                if (imp < 20f) lackParts.Add("CC_Adv_Prison_Bare".Translate().Resolve());
                if (lacks > worstLacks)
                {
                    worstLacks = lacks;
                    worst = room;
                }
            }
            if (worst == null) yield break;
            var s = Signal(0.22f + 0.04f * worstLacks + 0.03f * Mathf.Min(4, prisoners.Count), "CC_Adv_Prison_Title".Translate(cellsBad).Resolve(),
                "CC_Adv_Prison_Detail".Translate(TextUtil.JoinAnd(lackParts.ToList())).Resolve(), 0.4f);
            s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_FurnishedCell");
            s.upgrade = true;
            s.look = new LookTargets(new TargetInfo(worst.Cells.First(), map));
            s.key = "prison-comfort";
            yield return s;
        }
    }
}
