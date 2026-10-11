using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A blueprint that waits until rock is mined or a building taken down.</summary>
    public class QueuedBlueprint : IExposable
    {
        public ThingDef thing;
        public TerrainDef terrain;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rot;
        public int tick;
        public string label;

        public BuildableDef Def => (BuildableDef)thing ?? terrain;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thing, "thing");
            Scribe_Defs.Look(ref terrain, "terrain");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref label, "label");
            // The material came from a mod that's gone: fall back to the default one.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && thing != null && thing.MadeFromStuff && stuff == null)
                stuff = GenStuff.DefaultStuffFor(thing);
        }
    }

    /// <summary>
    /// Places queued room-plan blueprints as soon as their spot is clear. Gives up on a piece when the
    /// player cancels the mining or deconstruction it was waiting for, or after a long while.
    /// </summary>
    public class CouncilPlanQueue : MapComponent
    {
        public List<QueuedBlueprint> queue = new List<QueuedBlueprint>();
        private const int MaxAgeTicks = GenDate.TicksPerDay * 60;

        public CouncilPlanQueue(Map map) : base(map) { }

        public int Count => queue.Count;

        public void Add(BuildableDef def, ThingDef stuff, IntVec3 cell, Rot4 rot, string label)
        {
            queue.Add(new QueuedBlueprint
            {
                thing = def as ThingDef,
                terrain = def as TerrainDef,
                stuff = stuff,
                cell = cell,
                rot = rot,
                tick = Find.TickManager.TicksGame,
                label = label
            });
        }

        public void Clear() => queue.Clear();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref queue, "queue", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (queue == null) queue = new List<QueuedBlueprint>();
                queue.RemoveAll(q => q == null || q.Def == null);
            }
        }

        public override void MapComponentTick()
        {
            if (queue.Count == 0 || Find.TickManager.TicksGame % 250 != 41) return;
            try
            {
                int given = 0;
                var dm = map.designationManager;
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    var q = queue[i];
                    var def = q.Def;
                    if (def == null || Find.TickManager.TicksGame - q.tick > MaxAgeTicks)
                    {
                        queue.RemoveAt(i);
                        continue;
                    }
                    var rect = q.thing != null ? GenAdj.OccupiedRect(q.cell, q.rot, q.thing.size) : CellRect.SingleCell(q.cell);
                    bool waiting = false, hopeless = false, done = false;
                    foreach (var c in rect)
                    {
                        if (!c.InBounds(map))
                        {
                            hopeless = true;
                            break;
                        }
                        foreach (var t in c.GetThingList(map))
                        {
                            if ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild == def) done = true;
                        }
                        var ed = c.GetEdifice(map);
                        if (ed == null || (q.terrain != null && !ed.def.mineable)) continue;
                        if (ed.def == def) done = true;
                        else if (dm.DesignationAt(c, DesignationDefOf.Mine) != null || dm.DesignationAt(c, DesignationDefOf.MineVein) != null
                                 || dm.DesignationOn(ed, DesignationDefOf.Deconstruct) != null)
                            waiting = true;
                        else hopeless = true;
                    }
                    if (done || hopeless)
                    {
                        if (hopeless) given++;
                        queue.RemoveAt(i);
                        continue;
                    }
                    if (waiting) continue;
                    if (TemplatePlanner.TryPlace(def, q.cell, q.rot, q.stuff, map) || Find.TickManager.TicksGame - q.tick > GenDate.TicksPerDay * 3)
                        queue.RemoveAt(i);
                }
                if (given > 0)
                    Messages.Message("CC_Plan_QueueGaveUp".Translate(given), MessageTypeDefOf.SilentInput, false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("plan queue", e);
            }
        }
    }
}
