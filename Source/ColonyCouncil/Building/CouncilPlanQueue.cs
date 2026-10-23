using System.Collections.Generic;
using System.Linq;
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
            // Silently: the building, floor or material may come from a mod that has since been removed.
            CouncilScribe.LookDef(ref thing, "thing");
            CouncilScribe.LookDef(ref terrain, "terrain");
            CouncilScribe.LookDef(ref stuff, "stuff");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref label, "label");
            // The material came from a mod that's gone: fall back to the default one (walls and furniture, and floors
            // made from stuff alike).
            var def = Def;
            if (Scribe.mode == LoadSaveMode.PostLoadInit && def != null && def.MadeFromStuff && stuff == null)
                stuff = GenStuff.DefaultStuffFor(def);
        }
    }

    /// <summary>
    /// Places queued room-plan blueprints as soon as their spot is clear. A piece in unexplored ground waits until the
    /// colonists dig through to it; a wall there that turns out to be rock is done. Gives up on a piece when the
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
            if (queue.Count == 0 || !Sched.Every(Find.TickManager.TicksGame, Sched.RareTicks, Sched.RarePlanQueue)) return;
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
                    bool waiting = false, hopeless = false, done = false, off = false, dark = false;
                    foreach (var c in rect)
                    {
                        if (!c.InBounds(map))
                        {
                            hopeless = off = true;
                            break;
                        }
                        // Still in unexplored ground: the colonists are digging towards it.
                        if (c.Fogged(map))
                        {
                            waiting = dark = true;
                            continue;
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
                        // A wall that waited on unexplored ground found rock (or a wall) there: that is the wall.
                        else if (q.thing != null && q.thing.IsWall && TemplatePlanner.IsWallLike(ed.def)) done = true;
                        else hopeless = true;
                    }
                    // Part of it still in the dark: what stands in the rest is judged once all of it has come to light.
                    if (hopeless && dark && !off) hopeless = false;
                    // A wall lamp hangs on the wall it faces: it waits for that wall as long as the wall is still to come.
                    bool wallToCome = false;
                    if (!done && !hopeless && q.thing != null && WallLights.IsAttached(q.thing))
                    {
                        if (q.cell.GetThingList(map).Any(t => t.def == q.thing && t.Rotation == q.rot)) done = true;
                        else wallToCome = WallToCome(q.cell + q.rot.FacingCell, dm);
                    }
                    if (done || hopeless)
                    {
                        if (hopeless) given++;
                        queue.RemoveAt(i);
                        continue;
                    }
                    if (waiting) continue;
                    if (TemplatePlanner.TryPlace(def, q.cell, q.rot, q.stuff, map) || (!wallToCome && Find.TickManager.TicksGame - q.tick > GenDate.TicksPerDay * 3))
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

        /// <summary>A wall is still to come on this cell: planned, half-built, queued, or waiting for rock to be mined there.</summary>
        private bool WallToCome(IntVec3 c, DesignationManager dm)
        {
            if (!c.InBounds(map)) return false;
            foreach (var t in c.GetThingList(map))
                if ((t is Blueprint || t is Frame) && t.def.entityDefToBuild is ThingDef td && WallLights.Supports(td)) return true;
            if (dm.DesignationAt(c, DesignationDefOf.Mine) != null) return true;
            foreach (var q in queue)
                if (q.cell == c && WallLights.Supports(q.thing)) return true;
            return false;
        }
    }
}
