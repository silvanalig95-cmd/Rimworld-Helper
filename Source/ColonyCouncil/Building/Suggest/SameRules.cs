using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // ---------------- The same rules for what already existed ----------------
    //
    // The advisor's cards, the base review's findings, the defense advice, the notes on a line and the automations go
    // through the shared suggestion core too (docs/plan-ahead.md, "The same rules for what already exists"): one "Not
    // this" store (their keys "adv:...", "base:...", "def:...", "line:<id>:...", "auto:..."), one log of steps done with
    // Undo, and a look later whether each step worked. These are the undos and checks they share.

    /// <summary>What the same-rules sources share: whether a key's advice still stands, and doing an order-giving action as a step.</summary>
    public static class SameRules
    {
        /// <summary>Whether the advice behind a key still stands on the map, and its words now (for a check that didn't work).</summary>
        public static bool StillOpen(Map map, string key, out string title)
        {
            title = null;
            if (map == null || key.NullOrEmpty()) return false;
            if (key.StartsWith(AdvisorSteps.Source + ":", StringComparison.Ordinal))
            {
                var s = CouncilMapComponent.For(map)?.AllSignals?.FirstOrDefault(x => AdvisorSteps.KeyOf(x) == key);
                title = s?.title;
                return s != null;
            }
            if (key.StartsWith(BaseReview.Source + ":", StringComparison.Ordinal))
            {
                string k = key.Substring(BaseReview.Source.Length + 1);
                var f = BaseReview.All(map).FirstOrDefault(x => x.key == k);
                title = f?.title;
                return f != null;
            }
            return false;
        }

        /// <summary>
        /// Carries out an action that gives orders on the map (plans lamps, marks a firebreak, takes something down) as a
        /// step: what it placed and marked is noted, so Undo takes exactly that back, and a look a while later says whether
        /// its reason has gone. Null when it gave no order at all.
        /// </summary>
        public static SuggestDone Orders(Map map, Action act, int waitTicks, string lookKey = null)
        {
            if (map == null || act == null) return null;
            var before = OrdersSnapshot.Take(map);
            act();
            var undo = before.Since(map);
            if (undo == null) return null;
            return new SuggestDone { undo = undo, map = map, check = new SourceGoneCheck { waitTicks = waitTicks, key = lookKey } };
        }

        /// <summary>A store's owner as a saved word: "zone:12" for a stockpile, "thing:Shelf123" for a shelf.</summary>
        public static string OwnerOf(SlotGroup g)
        {
            if (g?.parent is Zone z) return "zone:" + z.ID;
            if (g?.parent is Thing t) return "thing:" + t.ThingID;
            return null;
        }

        /// <summary>The store a saved owner names, if it is still there.</summary>
        public static StorageSettings SettingsOf(Map map, string owner)
        {
            if (map == null || owner.NullOrEmpty()) return null;
            if (owner.StartsWith("zone:", StringComparison.Ordinal) && int.TryParse(owner.Substring(5), out int id))
                return (map.zoneManager.AllZones.FirstOrDefault(z => z.ID == id) as ISlotGroupParent)?.GetStoreSettings();
            if (owner.StartsWith("thing:", StringComparison.Ordinal))
            {
                string tid = owner.Substring(6);
                return (map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.ThingID == tid) as ISlotGroupParent)?.GetStoreSettings();
            }
            return null;
        }

        /// <summary>People who build here now (Construction switched on), for a check on blueprints that aren't built.</summary>
        public static int Builders(Map map) => GrowthNeeds.For(map)?.input?.builders ?? 0;
    }

    /// <summary>The blueprints and orders on a map at one moment, to tell afterwards what an action added.</summary>
    public class OrdersSnapshot
    {
        private HashSet<Thing> blueprints;
        private HashSet<Designation> designations;

        public static OrdersSnapshot Take(Map map) => new OrdersSnapshot
        {
            blueprints = new HashSet<Thing>(map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)),
            designations = new HashSet<Designation>(map.designationManager.AllDesignations)
        };

        /// <summary>What was added since, as an undo (null when nothing was).</summary>
        public UndoOrders Since(Map map)
        {
            var undo = new UndoOrders();
            var player = Faction.OfPlayerSilentFail;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
                if (!blueprints.Contains(t) && t.Faction == player && t.def.entityDefToBuild != null)
                {
                    undo.pieceCells.Add(t.Position);
                    undo.pieceDefs.Add(t.def.entityDefToBuild.defName);
                }
            foreach (var d in map.designationManager.AllDesignations)
            {
                if (d?.def == null || designations.Contains(d)) continue;
                if (d.target.HasThing)
                {
                    undo.marked.Add(d.target.Thing);
                    undo.markedDefs.Add(d.def.defName);
                }
                else if (d.target.Cell.IsValid)
                {
                    undo.markedCells.Add(d.target.Cell);
                    undo.markedCellDefs.Add(d.def.defName);
                }
            }
            return undo.Count == 0 ? null : undo;
        }
    }

    /// <summary>
    /// Orders a step gave, taken back: the blueprints it placed cancelled (a frame gives back what was brought to it),
    /// the orders it marked on things and cells removed. What was built or carried out already stays.
    /// </summary>
    public class UndoOrders : SuggestUndo
    {
        public List<IntVec3> pieceCells = new List<IntVec3>();
        public List<string> pieceDefs = new List<string>();
        public List<Thing> marked = new List<Thing>();
        public List<string> markedDefs = new List<string>();
        public List<IntVec3> markedCells = new List<IntVec3>();
        public List<string> markedCellDefs = new List<string>();

        public int Count => pieceCells.Count + marked.Count + markedCells.Count;

        private IEnumerable<Thing> Pieces(Map map)
        {
            var player = Faction.OfPlayerSilentFail;
            for (int i = 0; i < pieceCells.Count && i < pieceDefs.Count; i++)
            {
                var c = pieceCells[i];
                if (!c.InBounds(map)) continue;
                foreach (var t in c.GetThingList(map).ToList())
                    if ((t is Blueprint || t is Frame) && t.Faction == player && t.Position == c && t.def.entityDefToBuild?.defName == pieceDefs[i])
                        yield return t;
            }
        }

        private IEnumerable<Designation> Marks(Map map)
        {
            var dm = map.designationManager;
            for (int i = 0; i < marked.Count && i < markedDefs.Count; i++)
            {
                var t = marked[i];
                var def = DefDatabase<DesignationDef>.GetNamedSilentFail(markedDefs[i] ?? "");
                if (t == null || def == null || !t.Spawned || t.Map != map) continue;
                var d = dm.DesignationOn(t, def);
                if (d != null) yield return d;
            }
            for (int i = 0; i < markedCells.Count && i < markedCellDefs.Count; i++)
            {
                var c = markedCells[i];
                var def = DefDatabase<DesignationDef>.GetNamedSilentFail(markedCellDefs[i] ?? "");
                if (def == null || !c.InBounds(map)) continue;
                var d = dm.DesignationAt(c, def);
                if (d != null) yield return d;
            }
        }

        /// <summary>The blueprints and frames of the step still standing unbuilt.</summary>
        public int PiecesLeft(Map map) => map == null ? 0 : Pieces(map).Distinct().Count();

        /// <summary>The orders of the step still waiting to be carried out.</summary>
        public int OrdersLeft(Map map) => map == null ? 0 : Marks(map).Count();

        public override bool Possible(Map map) => map != null && (Pieces(map).Any() || Marks(map).Any());

        public override void Undo(Map map)
        {
            if (map == null) return;
            foreach (var t in Pieces(map).Distinct().ToList())
                if (!t.Destroyed) t.Destroy(DestroyMode.Cancel);
            foreach (var d in Marks(map).ToList()) map.designationManager.RemoveDesignation(d);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                for (int i = 0; i < marked.Count; i++)
                    if (marked[i] != null && marked[i].Destroyed) marked[i] = null;
            Scribe_Collections.Look(ref pieceCells, "pieceCells", LookMode.Value);
            Scribe_Collections.Look(ref pieceDefs, "pieceDefs", LookMode.Value);
            Scribe_Collections.Look(ref marked, "marked", LookMode.Reference);
            Scribe_Collections.Look(ref markedDefs, "markedDefs", LookMode.Value);
            Scribe_Collections.Look(ref markedCells, "markedCells", LookMode.Value);
            Scribe_Collections.Look(ref markedCellDefs, "markedCellDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pieceCells ??= new List<IntVec3>();
                pieceDefs ??= new List<string>();
                marked ??= new List<Thing>();
                markedDefs ??= new List<string>();
                markedCells ??= new List<IntVec3>();
                markedCellDefs ??= new List<string>();
            }
        }
    }

    /// <summary>Stores' priorities put back as they were.</summary>
    public class UndoStorePriority : SuggestUndo
    {
        public List<string> owners = new List<string>();
        public List<int> priorities = new List<int>();

        public override bool Possible(Map map) => owners.Any(o => SameRules.SettingsOf(map, o) != null);

        public override void Undo(Map map)
        {
            for (int i = 0; i < owners.Count && i < priorities.Count; i++)
            {
                var st = SameRules.SettingsOf(map, owners[i]);
                if (st != null) st.Priority = (StoragePriority)priorities[i];
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref owners, "owners", LookMode.Value);
            Scribe_Collections.Look(ref priorities, "priorities", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                owners ??= new List<string>();
                priorities ??= new List<int>();
            }
        }
    }

    /// <summary>Kinds of goods a step set stores not to take, allowed in them again.</summary>
    public class UndoStoreAllow : SuggestUndo
    {
        public List<string> owners = new List<string>();
        public List<string> defs = new List<string>();

        public override bool Possible(Map map) => owners.Any(o => SameRules.SettingsOf(map, o) != null);

        public override void Undo(Map map)
        {
            for (int i = 0; i < owners.Count && i < defs.Count; i++)
            {
                var st = SameRules.SettingsOf(map, owners[i]);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defs[i] ?? "");
                if (st?.filter != null && def != null) st.filter.SetAllow(def, true);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref owners, "owners", LookMode.Value);
            Scribe_Collections.Look(ref defs, "defs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                owners ??= new List<string>();
                defs ??= new List<string>();
            }
        }
    }

    /// <summary>A step taken into the plan put back among the proposals.</summary>
    public class UndoPlanTaken : SuggestUndo
    {
        public string stepKey;

        private NextStep Step => CouncilGameComponent.Instance?.nextSteps?.FirstOrDefault(s => s.key == stepKey && s.Planned);

        public override bool Possible(Map map) => Step != null;

        public override void Undo(Map map)
        {
            var step = Step;
            if (step == null) return;
            step.state = NextStepsMath.Proposed;
            step.changedTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stepKey, "stepKey");
        }
    }

    /// <summary>The research project that was being studied before a step picked another.</summary>
    public class UndoResearchPick : SuggestUndo
    {
        public ResearchProjectDef prev, picked;

        public override bool Possible(Map map) => Find.ResearchManager.GetProject() == picked;

        public override void Undo(Map map) => Find.ResearchManager.SetCurrentProject(prev);

        public override void ExposeData()
        {
            CouncilScribe.LookDef(ref prev, "prev");
            CouncilScribe.LookDef(ref picked, "picked");
        }
    }

    /// <summary>
    /// Whether the reason for a step has gone a while later: the advice or finding behind its key (or another key it
    /// looks at) no longer stands. If not, what is in the way: the pieces still unbuilt and whether anybody builds, the
    /// orders still waiting, or that the advice still stands.
    /// </summary>
    public class SourceGoneCheck : SuggestCheck
    {
        /// <summary>The key to look at (null: the step's own).</summary>
        public string key;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            var map = done.map;
            if (!SameRules.StillOpen(map, key ?? done.key, out string title)) return true;
            var parts = new List<string>();
            if (done.undo is UndoOrders o)
            {
                int pieces = o.PiecesLeft(map), orders = o.OrdersLeft(map);
                if (pieces > 0) parts.Add("CC_Same_Check_Pieces".Translate(pieces, o.pieceCells.Count).Resolve());
                if (pieces > 0 && SameRules.Builders(map) == 0) parts.Add("CC_Same_Check_NoBuilders".Translate().Resolve());
                if (orders > 0) parts.Add("CC_Same_Check_Orders".Translate(orders, o.marked.Count + o.markedCells.Count).Resolve());
            }
            whyNot = parts.Count > 0 ? string.Join(" ", parts) : "CC_Same_Check_Still".Translate(title ?? done.text).Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref key, "key");
        }
    }

    /// <summary>Whether a bill a step set has made its product: the stock rose a quarter of the way to its count or reached most of it; if not, what holds the bill up.</summary>
    public class BillStockCheck : SuggestCheck
    {
        public Building_WorkTable bench;
        public Bill_Production bill;
        public ThingDef product;
        public int before, target;

        public override bool Worked(SuggestDone done, out string whyNot)
        {
            whyNot = null;
            if (bill == null || bill.DeletedOrDereferenced || bench == null || !bench.Spawned)
            {
                whyNot = "CC_Ahead_NotYet_BillGone".Translate().Resolve();
                return false;
            }
            int now = product != null && Making.Stock(done.map).TryGetValue(product, out int n) ? n : 0;
            if (AheadMath.StockStepWorked(before, now, target, 0f, 0f)) return true;
            whyNot = BillStuck.Why(bench, bill)?.text ?? "CC_Ahead_NotYet_Nothing".Translate().Resolve();
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && bill != null && bill.DeletedOrDereferenced) bill = null;
            Scribe_References.Look(ref bench, "bench");
            Scribe_References.Look(ref bill, "bill");
            CouncilScribe.LookDef(ref product, "product");
            Scribe_Values.Look(ref before, "before");
            Scribe_Values.Look(ref target, "target");
        }
    }
}
