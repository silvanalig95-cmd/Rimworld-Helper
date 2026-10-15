using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ---------------- Rooms the colony has outgrown ----------------
    //
    // Gentle advice as the colony grows: a dining room with fewer seats than people, a barracks that could be bedrooms
    // with what's in store, a hospital without what the colony has researched, a rec room with too little to do for
    // everyone. They read room roles and furniture the game keeps anyway, once an hour with the rest of the advisor, and
    // each offers the planner with the fitting larger size, or the room upgraded where it stands.

    /// <summary>Helpers shared by the room growth advice.</summary>
    public static class RoomGrowth
    {
        /// <summary>People who sit at a table, sleep in a bed of their own and want something to do: everyone past babyhood.</summary>
        public static List<Pawn> People(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();

        /// <summary>
        /// The size of a room type whose plan has at least <paramref name="want"/> pieces of <paramref name="role"/> (the
        /// smallest such), else the one with the most; only sizes the colony can plan. Null if none has more than
        /// <paramref name="moreThan"/>.
        /// </summary>
        public static RoomTemplateDef SizeFor(string type, string role, int want, int moreThan, Map map)
        {
            var rt = DefDatabase<RoomTypeDef>.GetNamedSilentFail(type);
            if (rt == null) return null;
            RoomTemplateDef enough = null, most = null;
            int enoughN = int.MaxValue, mostN = moreThan;
            foreach (var (t, _) in rt.Variants)
            {
                if (t.UnavailableReason(map) != null) continue;
                int n = t.things.Count(x => x.role == role);
                if (n >= want && n < enoughN)
                {
                    enough = t;
                    enoughN = n;
                }
                if (n > mostN)
                {
                    most = t;
                    mostN = n;
                }
            }
            return enough ?? most;
        }

        /// <summary>The size of a room type whose plan has the number of <paramref name="role"/> pieces closest to <paramref name="count"/> (the larger on a tie).</summary>
        public static RoomTemplateDef ClosestSize(string type, string role, int count, Map map)
        {
            var rt = DefDatabase<RoomTypeDef>.GetNamedSilentFail(type);
            if (rt == null) return null;
            RoomTemplateDef best = null;
            int bestGap = int.MaxValue, bestN = -1;
            foreach (var (t, _) in rt.Variants)
            {
                if (t.UnavailableReason(map) != null) continue;
                int n = t.things.Count(x => x.role == role);
                int gap = Math.Abs(n - count);
                if (gap < bestGap || (gap == bestGap && n > bestN))
                {
                    best = t;
                    bestGap = gap;
                    bestN = n;
                }
            }
            return best;
        }

        /// <summary>The first colony building in a room that matches, for the advisor's "show" button.</summary>
        public static Thing FirstIn(Room room, Func<ThingDef, bool> pred)
        {
            if (room == null) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t.def.category == ThingCategory.Building && t.Faction == Faction.OfPlayer && room.ContainsCell(t.Position) && pred(t.def)) return t;
            }
            return null;
        }

        /// <summary>"A bedroom", "an infirmary": a plan's name for advice text.</summary>
        public static string WithArticle(RoomTemplateDef t, bool full = false) => t == null ? "" : Find.ActiveLanguageWorker.WithIndefiniteArticle(full ? t.FullLabel : t.label);
    }

    /// <summary>The dining rooms seat fewer than the colony has people now: the next size up, or more chairs.</summary>
    public class AdvisorWorker_DiningOutgrown : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.DiningRoom;
            if (role == null) yield break;
            int people = RoomGrowth.People(map).Count;
            if (people < 4) yield break;
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
            // No dining room at all is the plain dining advice; one seat short is fine (someone is always asleep or out).
            if (main == null || seats >= people - 1) yield break;
            var plan = RoomGrowth.SizeFor("Council_RT_DiningRoom", "chair", people, best, map);
            string detail = plan != null
                ? "CC_Adv_DiningOutgrown_Detail".Translate(seats, people, RoomGrowth.WithArticle(plan, true).CapitalizeFirst()).Resolve()
                : "CC_Adv_DiningOutgrown_DetailNoPlan".Translate(seats, people).Resolve();
            var s = Signal(0.25f + 0.04f * Mathf.Min(6, people - seats), "CC_Adv_DiningOutgrown_Title".Translate(seats, people), detail, 0.3f);
            s.template = plan;
            var table = RoomGrowth.FirstIn(main, Avail.IsEatingTable);
            if (table != null) s.look = new LookTargets(table);
            s.key = "dining-outgrown";
            yield return s;
        }
    }

    /// <summary>
    /// Colonists still share the barracks, and the colony now has the materials to wall bedrooms for all of them. When it
    /// does, this replaces the plain barracks advice, with the material and how much of it the walls take.
    /// </summary>
    public class AdvisorWorker_BarracksToBedrooms : AdvisorSignalWorker
    {
        /// <summary>The barracks sleepers the plain advice would list: a bed of their own in a room that isn't theirs, ascetics aside.</summary>
        public static List<Pawn> Sleepers(Map map) =>
            RoomGrowth.People(map).Where(p => p.ownership?.OwnedBed != null && p.ownership.OwnedRoom == null
                                              && !(p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false)).ToList();

        /// <summary>
        /// The bedroom plan the colony can wall for <paramref name="count"/> people from its stock: the standard bedroom if
        /// there's enough for that, else the cozy one. False if there's enough for neither.
        /// </summary>
        public static bool WithinReach(Map map, int count, out RoomTemplateDef plan, out ThingDef stuff, out int need)
        {
            plan = null;
            stuff = null;
            need = 0;
            if (count <= 0) return false;
            foreach (var name in new[] { "Council_Bedroom", "Council_SmallBedroom" })
            {
                var t = DefDatabase<RoomTemplateDef>.GetNamedSilentFail(name);
                if (t == null || t.UnavailableReason(map) != null) continue;
                int walls = RoomPlanConfig.WallCount(t) * count;
                var s = MaterialPicker.Pick(ThingDefOf.Wall, map, walls, MaterialUse.Wall);
                if (s == null || MaterialPicker.Coverage(ThingDefOf.Wall, s, walls, map) < 2) continue;
                plan = t;
                stuff = s;
                need = walls * Mathf.Max(1, MaterialCounts.PerUnit(ThingDefOf.Wall, s));
                return true;
            }
            return false;
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            // The colony first needs a bed for everyone: that advice comes first.
            if (RoomGrowth.People(map).Any(p => p.ownership?.OwnedBed == null)) yield break;
            var sleepers = Sleepers(map);
            if (sleepers.Count == 0 || !WithinReach(map, sleepers.Count, out var plan, out var stuff, out int need)) yield break;
            var s = Signal(0.22f + 0.05f * sleepers.Count, "CC_Adv_BarracksRooms_Title".Translate(),
                "CC_Adv_BarracksRooms_Detail".Translate(TextUtil.JoinAnd(sleepers.Select(p => p.LabelShort).ToList()), stuff.label,
                    RoomGrowth.WithArticle(plan), need, MaterialCounts.Have(map, stuff)).Resolve(), 0.4f);
            s.look = new LookTargets(sleepers);
            s.template = plan;
            s.suggestThing = stuff;
            s.key = "barracks-rooms";
            yield return s;
        }
    }

    /// <summary>The hospital lacks what the colony has researched: a vitals monitor, hospital beds, a sterile floor. Offers to upgrade it where it stands.</summary>
    public class AdvisorWorker_HospitalUpgrade : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.Hospital;
            if (role == null) yield break;
            Room main = null;
            int beds = 0;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                int n = room.ContainedBeds.Count(b => b.Medical);
                if (n > beds)
                {
                    beds = n;
                    main = room;
                }
            }
            if (main == null) yield break;

            var items = new List<string>();
            ThingDef suggest = null;
            var isMonitor = RoomTemplateDef.RolePredicate("monitor");
            var monitor = Avail.BestBuildable(map, isMonitor, "VitalsMonitor");
            if (monitor != null && !RoomUtil.RoomContains(main, isMonitor))
            {
                items.Add("CC_Adv_HospitalUpgrade_Monitor".Translate(Avail.Indefinite(monitor)).Resolve());
                suggest = monitor;
            }
            var isHospitalBed = RoomTemplateDef.RolePredicate("medical");
            var hospitalBed = Avail.BestBuildable(map, isHospitalBed, "HospitalBed");
            int plain = main.ContainedBeds.Count(b => b.Medical && !isHospitalBed(b.def));
            if (hospitalBed != null && plain > 0)
            {
                items.Add("CC_Adv_HospitalUpgrade_Beds".Translate(Find.ActiveLanguageWorker.Pluralize(hospitalBed.label, plain), plain).Resolve());
                suggest ??= hospitalBed;
            }
            var sterile = DefDatabase<TerrainDef>.GetNamedSilentFail("SterileTile");
            if (sterile != null && Avail.CanBuild(sterile, map) && main.CellCount <= 400)
            {
                float clean = sterile.GetStatValueAbstract(StatDefOf.Cleanliness);
                int unclean = main.Cells.Count(c => map.terrainGrid.TerrainAt(c).GetStatValueAbstract(StatDefOf.Cleanliness) < clean - 0.01f);
                if (unclean * 3 > main.CellCount) items.Add("CC_Adv_HospitalUpgrade_Floor".Translate(sterile.label).Resolve());
            }
            if (items.Count == 0) yield break;
            var s = Signal(0.2f + 0.06f * items.Count + 0.02f * Mathf.Min(beds, 5), "CC_Adv_HospitalUpgrade_Title".Translate(),
                "CC_Adv_HospitalUpgrade_Detail".Translate(TextUtil.JoinAnd(items)).Resolve(), 0.35f);
            s.template = RoomGrowth.ClosestSize("Council_RT_Hospital", "bed", beds, map);
            s.upgrade = true;
            s.suggestThing = suggest;
            var bed = main.ContainedBeds.FirstOrDefault(b => b.Medical);
            if (bed != null) s.look = new LookTargets(bed);
            s.key = "hospital-upgrade";
            yield return s;
        }
    }

    /// <summary>Too little to do in the rec rooms for the colony's size: a bigger recreation room, or more games.</summary>
    public class AdvisorWorker_RecRoomOutgrown : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var role = RoomUtil.RecRoom;
            if (role == null) yield break;
            int people = RoomGrowth.People(map).Count;
            if (people < 6) yield break;
            int games = 0, best = -1;
            Room main = null;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                int n = RoomUtil.RoomCount(room, Avail.IsJoyGiver);
                games += n;
                if (n > best)
                {
                    best = n;
                    main = room;
                }
            }
            // No rec room is the recreation advice's business; one game for every three people is enough.
            if (main == null || games * 3 >= people) yield break;
            var game = RoomGrowth.FirstIn(main, Avail.IsJoyGiver);
            var plan = RoomGrowth.SizeFor("Council_RT_RecRoom", "joy", Mathf.CeilToInt(people / 3f), best, map);
            string gameLabel = game?.def.label ?? "";
            string detail = plan != null
                ? "CC_Adv_RecOutgrown_Detail".Translate(gameLabel, RoomGrowth.WithArticle(plan).CapitalizeFirst()).Resolve()
                : "CC_Adv_RecOutgrown_DetailNoPlan".Translate(gameLabel).Resolve();
            var s = Signal(0.22f + 0.03f * Mathf.Min(6, people - games * 3), "CC_Adv_RecOutgrown_Title".Translate(games, people), detail, 0.3f);
            s.template = plan;
            if (game != null) s.look = new LookTargets(game);
            s.key = "rec-outgrown";
            yield return s;
        }
    }
}
