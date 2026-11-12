using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The medics carry a few medicines for a fight that comes without warning (MedicKitsMath): every six hours, on each home,
    /// the best few by Medical with doctoring on are given the game's own "carry medicine" (the Assign tab's inventory stock,
    /// so they fetch it themselves and tend from their pack in the field): two or three of a kind the home has plenty of,
    /// herbal or industrial, the finest kept at home for the hospital, never so many that the home's sick would go short.
    /// A medicine stock the player set by hand is left alone for good; Rimfolk's own is taken off again when someone stops
    /// being one of the medics. Said once, the first time someone is given it.
    /// </summary>
    public static class MedicKits
    {
        public const int EveryHours = 6;

        public static bool On => ColonyCouncilMod.Settings?.medicKits ?? true;

        public static void Update(CouncilGameComponent comp)
        {
            if (!On || comp == null || InventoryStockGroupDefOf.Medicine == null) return;
            comp.medicKits ??= new List<string>();
            var group = InventoryStockGroupDefOf.Medicine;
            var records = new Dictionary<int, (string def, int count)>();
            foreach (var s in comp.medicKits)
                if (MedicKitsMath.Parse(s, out int id, out string def, out int count)) records[id] = (def, count);
            var seen = new HashSet<int>();
            foreach (var map in Homes.All.ToList())
            {
                if (map?.mapPawns == null) continue;
                var people = map.mapPawns.FreeColonistsSpawned.Where(p => p.inventoryStock != null && !p.DevelopmentalStage.Baby() && !p.IsQuestLodger()).ToList();
                foreach (var p in people) seen.Add(p.thingIDNumber);
                // Hands off whatever the player changed since Rimfolk set it.
                foreach (var p in people)
                {
                    if (!records.TryGetValue(p.thingIDNumber, out var r) || r.count < 0) continue;
                    var cur = p.inventoryStock.GetCurrentEntryFor(group);
                    if (cur == null || cur.count != r.count || cur.thingDef?.defName != r.def) records[p.thingIDNumber] = (null, -1);
                }
                var medics = people.Where(p => !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).Take(MedicKitsMath.Medics(people.Count)).ToList();
                // Medics already carrying a stock the player set before Rimfolk came: theirs.
                foreach (var p in medics)
                    if (!records.ContainsKey(p.thingIDNumber) && p.inventoryStock.GetDesiredCountForGroup(group) > 0) records[p.thingIDNumber] = (null, -1);
                var mine = medics.Where(p => !records.TryGetValue(p.thingIDNumber, out var r) || r.count >= 0).ToList();
                int sick = people.Count(p => p.health?.HasHediffsNeedingTend() == true || p.InBed() && HealthAIUtility.ShouldSeekMedicalRest(p));
                var (kind, each) = Pick(map, group, mine, people.Count, sick);
                foreach (var p in mine)
                {
                    if (kind == null || each <= 0) continue;
                    bool first = !records.ContainsKey(p.thingIDNumber);
                    p.inventoryStock.SetThingForGroup(group, kind);
                    p.inventoryStock.SetCountForGroup(group, each);
                    records[p.thingIDNumber] = (kind.defName, each);
                    if (first)
                        Tell.Message(NewsKind.Health, Importance.Flavor, map,
                            "CC_MedicKit_Packs".Translate(p.Named("PAWN"), each.Named("COUNT"), kind.label.Named("MEDICINE")).Resolve(), new LookTargets(p), MessageTypeDefOf.SilentInput);
                }
                // Those of Rimfolk's who are no longer among the medics (or there is no medicine to spare now): put it back.
                foreach (var p in people)
                {
                    if (!records.TryGetValue(p.thingIDNumber, out var r) || r.count < 0) continue;
                    if (mine.Contains(p) && kind != null && each > 0) continue;
                    p.inventoryStock.SetCountForGroup(group, 0);
                    records.Remove(p.thingIDNumber);
                }
            }
            // Gone from the colony (dead, left, sold): nothing to keep. Those away from home (a caravan) keep theirs.
            foreach (var id in records.Keys.ToList())
                if (!seen.Contains(id) && !Find.WorldPawns.AllPawnsAlive.Concat(PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead).Any(x => x?.thingIDNumber == id && !x.Dead && x.Faction == Faction.OfPlayer))
                    records.Remove(id);
            comp.medicKits = records.Select(kv => MedicKitsMath.Record(kv.Key, kv.Value.def, kv.Value.count)).ToList();
        }

        /// <summary>The kind the medics carry and how many each: industrial or herbal, whichever leaves them most (the finest stays home).</summary>
        private static (ThingDef kind, int each) Pick(Map map, InventoryStockGroupDef group, List<Pawn> medics, int colonists, int sick)
        {
            if (medics.Count == 0 || group.thingDefs.NullOrEmpty()) return (null, 0);
            int reserve = MedicKitsMath.Reserve(colonists, sick);
            ThingDef best = null;
            int bestEach = 0;
            foreach (var def in group.thingDefs.OrderBy(d => d.GetStatValueAbstract(StatDefOf.MedicalPotency)))
            {
                if (def == null || def == ThingDefOf.MedicineUltratech) continue;
                int stock = map.resourceCounter.GetCount(def) + medics.Sum(p => p.inventory?.innerContainer?.TotalStackCountOfDef(def) ?? 0);
                int each = MedicKitsMath.Each(stock, medics.Count, reserve);
                if (each >= bestEach && each > 0)
                {
                    best = def;
                    bestEach = each;
                }
            }
            return (best, bestEach);
        }
    }
}
