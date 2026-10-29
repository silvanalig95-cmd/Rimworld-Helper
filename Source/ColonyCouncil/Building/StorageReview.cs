using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The stores looked over, part of the base review: stockpiles that take everything, goods lying about that no
    /// store takes (with a stockpile made for them at a click), benches far from the store of their materials (with a
    /// little store made beside them), one kind of goods accepted in many rooms, stores that take nothing. Shelves of
    /// any mod count like stockpiles: they're all slot groups to the game. Rules in <see cref="StorageReviewMath"/>.
    /// </summary>
    public static class StorageReview
    {
        private static int storableCount = -1;

        /// <summary>How many kinds of thing can be stored at all, for telling a catch-all stockpile.</summary>
        private static int StorableCount
        {
            get
            {
                if (storableCount < 0) storableCount = DefDatabase<ThingDef>.AllDefsListForReading.Count(d => d.EverStorable(false));
                return storableCount;
            }
        }

        public static List<BaseFinding> Findings(Map map)
        {
            var findings = new List<BaseFinding>();
            var groups = map.haulDestinationManager?.AllGroupsListForReading;
            if (groups == null) return findings;
            var catchAll = new HashSet<SlotGroup>();
            CatchAlls(map, groups, findings, catchAll);
            Unstored(map, groups, findings);
            Benches(map, groups, findings);
            Spread(map, groups, catchAll, findings);
            Empty(map, groups, findings);
            return findings;
        }

        private static IntVec3 Where(SlotGroup g) => g.CellsList.Count > 0 ? g.CellsList[0] : IntVec3.Invalid;

        private static string NameOf(SlotGroup g) => g.parent is Zone z ? z.label : g.parent is Thing t ? t.LabelCap.ToString() : "?";

        // ---------------- Stockpiles that take everything ----------------

        private static void CatchAlls(Map map, List<SlotGroup> groups, List<BaseFinding> findings, HashSet<SlotGroup> catchAll)
        {
            var zones = new List<SlotGroup>();
            foreach (var g in groups)
            {
                var settings = g.Settings;
                if (settings?.filter == null || g.CellsList.Count == 0) continue;
                if (!StorageReviewMath.IsCatchAll(settings.filter.AllowedDefCount, StorableCount)) continue;
                catchAll.Add(g);
                // Shelves take everything by default and that's fine; a zone that does is the muddle.
                if (g.parent is Zone_Stockpile) zones.Add(g);
            }
            if (zones.Count < 2) return;
            findings.Add(new BaseFinding
            {
                key = "catchall", group = "stores", score = StorageReviewMath.CatchAllScore(zones.Count),
                title = "CC_Store_CatchAll".Translate(zones.Count).Resolve(),
                detail = "CC_Store_CatchAll_Detail".Translate(string.Join(", ", zones.Take(4).Select(NameOf))).Resolve(),
                look = Where(zones[0])
            });
        }

        // ---------------- Goods no store takes ----------------

        private static void Unstored(Map map, List<SlotGroup> groups, List<BaseFinding> findings)
        {
            var haulables = map.listerHaulables?.ThingsPotentiallyNeedingHauling();
            if (haulables == null || haulables.Count == 0) return;
            var byDef = new Dictionary<ThingDef, int>();
            Thing first = null;
            int total = 0;
            foreach (var t in haulables)
            {
                if (t == null || t.Destroyed || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.IsInValidStorage()) continue;
                if (t is Corpse || t.def.category != ThingCategory.Item) continue;
                bool taken = false;
                foreach (var g in groups)
                {
                    if (g.Settings == null || g.Settings.Priority == StoragePriority.Unstored || g.CellsList.Count == 0) continue;
                    if (g.Settings.AllowedToAccept(t))
                    {
                        taken = true;
                        break;
                    }
                }
                if (taken) continue;
                byDef.TryGetValue(t.def, out int n);
                byDef[t.def] = n + t.stackCount;
                total += t.stackCount;
                first ??= t;
            }
            if (total == 0 || byDef.Count == 0) return;
            var top = byDef.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Value + " " + kv.Key.label).ToList();
            var defs = byDef.Keys.ToList();
            findings.Add(new BaseFinding
            {
                key = "unstored", group = "stores", score = StorageReviewMath.UnstoredScore(total),
                title = "CC_Store_Unstored".Translate(total.Named("N"), byDef.Count.Named("KINDS")).Resolve(),
                detail = "CC_Store_Unstored_Detail".Translate(string.Join(", ", top).Named("LIST")).Resolve(),
                look = first.Position,
                fixLabel = "CC_Store_MakeStockpile".Translate().Resolve(),
                fix = () => MakeStockpile(map, defs, StoreCenter(map), 4, StoragePriority.Normal, "CC_Store_ZoneName_Odds".Translate().Resolve())
            });
        }

        /// <summary>Where a new general stockpile goes: by the store room if there is one, else the colony's middle.</summary>
        private static IntVec3 StoreCenter(Map map)
        {
            foreach (var room in RoomUtil.ColonyRooms(map))
                if (room.Role?.Worker is RoomRoleWorker_StoreRoom && room.CellCount <= 900) return room.ExtentsClose.CenterCell;
            return TemplatePlanner.ColonyCenter(map);
        }

        // ---------------- Benches far from their materials ----------------

        private static void Benches(Map map, List<SlotGroup> groups, List<BaseFinding> findings)
        {
            var far = new List<(Building_WorkTable bench, float distance, List<ThingDef> defs)>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_WorkTable bench) || bench.BillStack == null || bench.BillStack.Count == 0) continue;
                var defs = Materials(bench, map);
                if (defs.Count == 0) continue;
                var at = bench.InteractionCell;
                float best = float.MaxValue;
                foreach (var g in groups)
                {
                    if (g.Settings == null || g.Settings.Priority == StoragePriority.Unstored || g.CellsList.Count == 0) continue;
                    bool takes = false;
                    foreach (var d in defs)
                        if (g.Settings.AllowedToAccept(d))
                        {
                            takes = true;
                            break;
                        }
                    if (!takes) continue;
                    foreach (var c in g.CellsList) best = Mathf.Min(best, at.DistanceTo(c));
                }
                if (best == float.MaxValue || best <= StorageReviewMath.FarBench) continue;
                far.Add((bench, best, defs));
            }
            foreach (var (bench, distance, defs) in far.OrderByDescending(f => f.distance).Take(5))
            {
                var b = bench;
                var mats = defs;
                findings.Add(new BaseFinding
                {
                    key = "benchfar:" + b.thingIDNumber, group = "stores", score = StorageReviewMath.BenchFarScore(distance),
                    title = "CC_Store_BenchFar".Translate(b.LabelCap.ToString().Named("BENCH"), ((int)distance).Named("D")).Resolve(),
                    detail = "CC_Store_BenchFar_Detail".Translate(string.Join(", ", mats.Take(4).Select(d => d.label)).Named("LIST")).Resolve(),
                    look = b.Position,
                    fixLabel = "CC_Store_MakeByBench".Translate().Resolve(),
                    fix = () => MakeStockpile(map, mats, b.InteractionCell, 3, StoragePriority.Preferred, "CC_Store_ZoneName_Bench".Translate(b.LabelCap.ToString()).Resolve(), b)
                });
            }
        }

        /// <summary>
        /// The materials a bench's bills ask for that the colony has: what its store should be near. Nothing when they
        /// spoil (a stove's raw food belongs in the freezer, a butcher's corpses out of the house), since a store beside
        /// the bench would pull them out of the cold.
        /// </summary>
        private static List<ThingDef> Materials(Building_WorkTable bench, Map map)
        {
            var defs = new HashSet<ThingDef>();
            foreach (var bill in bench.BillStack)
            {
                if (bill?.recipe?.ingredients == null || bill.suspended) continue;
                foreach (var ing in bill.recipe.ingredients)
                {
                    if (ing?.filter == null) continue;
                    foreach (var d in ing.filter.AllowedThingDefs)
                    {
                        if (d == null || !d.EverStorable(false)) continue;
                        if (d.HasComp(typeof(CompRottable)) || d.IsCorpse) return new List<ThingDef>();
                        if (map.listerThings.ThingsOfDef(d).Count > 0) defs.Add(d);
                    }
                }
            }
            return defs.ToList();
        }

        // ---------------- One kind of goods in many rooms ----------------

        private static readonly (string key, string[] defNames)[] Kinds =
        {
            ("chunks", new[] { "ChunkGranite", "ChunkSandstone", "ChunkLimestone", "ChunkSlate", "ChunkMarble" }),
            ("steel", new[] { "Steel" }),
            ("cloth", new[] { "Cloth", "Leather_Plain" }),
            ("meals", new[] { "MealSimple", "MealFine" }),
            ("medicine", new[] { "MedicineHerbal", "MedicineIndustrial" }),
            ("weapons", new[] { "Gun_Autopistol", "MeleeWeapon_Knife" }),
            ("apparel", new[] { "Apparel_BasicShirt", "Apparel_Pants" })
        };

        private static void Spread(Map map, List<SlotGroup> groups, HashSet<SlotGroup> catchAll, List<BaseFinding> findings)
        {
            foreach (var (key, defNames) in Kinds)
            {
                var defs = defNames.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
                if (defs.Count == 0) continue;
                // Only kinds the colony has: nobody minds where nothing goes.
                if (!defs.Any(d => map.listerThings.ThingsOfDef(d).Count > 0)) continue;
                var rooms = new HashSet<int>();
                SlotGroup example = null;
                foreach (var g in groups)
                {
                    if (catchAll.Contains(g) || g.Settings == null || g.Settings.Priority < StoragePriority.Normal || g.CellsList.Count == 0) continue;
                    if (!defs.Any(d => g.Settings.AllowedToAccept(d))) continue;
                    var room = g.CellsList[0].GetRoom(map);
                    int id = room == null || room.PsychologicallyOutdoors ? -1 : room.ID;
                    if (rooms.Add(id) && rooms.Count == 2) example = g;
                }
                if (rooms.Count < StorageReviewMath.SpreadRooms) continue;
                findings.Add(new BaseFinding
                {
                    key = "spread:" + key, group = "stores", score = StorageReviewMath.SpreadScore(rooms.Count),
                    title = "CC_Store_Spread".Translate(("CC_Store_Kind_" + key).Translate().Named("KIND"), rooms.Count.Named("N")).Resolve().CapitalizeFirst(),
                    detail = "CC_Store_Spread_Detail".Translate().Resolve(),
                    look = example != null ? Where(example) : IntVec3.Invalid
                });
            }
        }

        // ---------------- Stores that take nothing ----------------

        private static void Empty(Map map, List<SlotGroup> groups, List<BaseFinding> findings)
        {
            var empty = groups.Where(g => g.Settings?.filter != null && g.CellsList.Count > 0 && g.Settings.filter.AllowedDefCount == 0).ToList();
            if (empty.Count == 0) return;
            findings.Add(new BaseFinding
            {
                key = "emptyfilter", group = "stores", score = StorageReviewMath.EmptyScore(empty.Count),
                title = "CC_Store_Empty".Translate(empty.Count).Resolve(),
                detail = "CC_Store_Empty_Detail".Translate(string.Join(", ", empty.Take(4).Select(NameOf))).Resolve(),
                look = Where(empty[0])
            });
        }

        // ---------------- Making a stockpile ----------------

        /// <summary>
        /// Lays out a little stockpile zone of up to <paramref name="size"/> by <paramref name="size"/> free cells near
        /// <paramref name="near"/>, taking only <paramref name="defs"/>, at the given priority. Smaller when the ground
        /// is tight; nothing when there's no room at all.
        /// </summary>
        private static void MakeStockpile(Map map, List<ThingDef> defs, IntVec3 near, int size, StoragePriority priority, string name, Thing keepClear = null)
        {
            if (map == null || defs == null || defs.Count == 0 || !near.IsValid) return;
            var clear = new HashSet<IntVec3>();
            if (keepClear != null)
            {
                foreach (var c in keepClear.OccupiedRect()) clear.Add(c);
                if (keepClear.def.hasInteractionCell) clear.Add(keepClear.InteractionCell);
            }
            bool Free((int x, int z) p)
            {
                var c = new IntVec3(p.x, 0, p.z);
                if (!c.InBounds(map) || c.Fogged(map) || clear.Contains(c) || c.InNoZoneEdgeArea(map)) return false;
                if (map.zoneManager.ZoneAt(c) != null) return false;
                var terrain = c.GetTerrain(map);
                if (terrain == null || terrain.passability == Traversability.Impassable) return false;
                foreach (var t in c.GetThingList(map))
                    if (!t.def.CanOverlapZones) return false;
                return true;
            }
            (int x, int z) corner = (0, 0);
            int got = 0;
            for (int s = size; s >= 2; s--)
            {
                if (StorageReviewMath.BestBlock(Free, (near.x, near.z), 7, s, out corner))
                {
                    got = s;
                    break;
                }
            }
            if (got == 0)
            {
                Messages.Message("CC_Store_NoRoom".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            for (int x = 0; x < got; x++)
                for (int z = 0; z < got; z++)
                    zone.AddCell(new IntVec3(corner.x + x, 0, corner.z + z));
            var filter = zone.settings.filter;
            filter.SetDisallowAll();
            foreach (var d in defs) filter.SetAllow(d, true);
            zone.settings.Priority = priority;
            if (!name.NullOrEmpty()) zone.label = name;
            Messages.Message("CC_Store_Made".Translate(zone.label, got * got), new LookTargets(new IntVec3(corner.x, 0, corner.z), map), MessageTypeDefOf.TaskCompletion, false);
            BaseReview.For(map, true);
        }
    }
}
