using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Things an answer says now exist, made real on the map where that's safe: a carving made by someone's hands and
    // carried over by them to the one it's for (OnMapPlays.cs), a coffin in the corner of a room (OnMapRooms.cs), a hat
    // bought on the side, logs where a guest really felled the trees, the children's saplings at the edge of the fields
    // with a stone by each, a new room marked out as building plans, a stash burned in the yard. Each only
    // with what the colony's era and DLCs have (looked up by name, never required), and each falls back quietly to the
    // answer's words alone when it can't be done. The pure part is OnMapMath.cs.

    /// <summary>
    /// A thing someone made (<see cref="maker"/>: its quality from their <see cref="skill"/>, the story's own skill, else
    /// Artistic; a work of art carries their name, a book is theirs) or got hold of (no maker: <see cref="quality"/>), for
    /// <see cref="forWhom"/> (A unless given). <see cref="things"/>: the candidates, "SculptureSmall" or
    /// "Apparel_HatTop|Apparel_BowlerHat", the first that exists and fits the colony's era; none does, nothing is made.
    /// Of <see cref="stuff"/> when it may be (wood unless given), else the thing's usual stuff.
    /// How it reaches them: the one it's from (<see cref="giver"/>, the maker unless given) carries it over by hand and sets
    /// it down at their feet, and the good moods the answer gives the two wait until it's handed over; a thing a visitor
    /// made is collected from beside them by the one it's for and taken in to the table. With <see cref="inRoom"/>, it's
    /// set in a corner of their own bedroom instead (a building stands there, built, if the colony could build one). With
    /// "Answers play out on the map" off, or nobody to carry it, it's set down beside them, packed if it's furniture.
    /// </summary>
    public class DilemmaEffect_Made : DilemmaEffect_PlayOut
    {
        public string things;
        public PlayWho maker = PlayWho.None;
        public PlayWho forWhom = PlayWho.A;
        /// <summary>Who carries it over (None: the maker, or nobody when nobody made it).</summary>
        public PlayWho giver = PlayWho.None;
        public SkillDef skill;
        public QualityCategory quality = QualityCategory.Normal;
        public string stuff = "WoodLog";
        /// <summary>Set in a corner of their own bedroom (a coffin made ready), not carried over.</summary>
        public bool inRoom;

        [Unsaved] private Thing pending;
        [Unsaved] private Pawn pendingCarrier;
        [Unsaved] private Pawn pendingTo;
        [Unsaved] private Pawn pendingFrom;
        [Unsaved] private string pendingMode;

        public DilemmaEffect_Made()
        {
            hours = new IntRange(7, 21);
            duration = new FloatRange(0.5f, 1f);
            waitHours = 12f;
        }

        protected override PlayKind Kind => PlayKind.Give;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || things.NullOrEmpty()) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var def = Pick(things);
                if (def == null) return;
                var mk = PlayOuts.Who(maker, ctx);
                if (mk != null && (mk.Dead || !(mk.RaceProps?.Humanlike ?? false))) mk = null;
                var to = PlayOuts.Who(forWhom, ctx);
                if (to != null && to.Dead) to = null;
                // A building set in someone's room must be one the colony could build.
                if (inRoom && def.category == ThingCategory.Building && !Avail.CanBuild(def, map)) return;
                var thing = Make(def, mk, skill ?? ctx.skill ?? SkillDefOf.Artistic);
                if (thing == null) return;
                string label = thing.LabelNoCount;
                if (inRoom && to != null)
                {
                    var placed = OnMapRooms.PlaceInRoom(thing, to);
                    if (placed != null)
                    {
                        Messages.Message("CC_Made_InRoom".Translate(to.Named("PAWN"), label.Named("THING")).Resolve(), new LookTargets(placed), MessageTypeDefOf.NeutralEvent, false);
                        return;
                    }
                    if (def.category == ThingCategory.Building && !def.Minifiable)
                    {
                        if (!thing.Destroyed) thing.Destroy();
                        return;
                    }
                }
                var from = giver != PlayWho.None ? PlayOuts.Who(giver, ctx) : mk;
                if (from != null && from.Dead) from = null;
                var near = Spot(map, to, mk, ctx.a);
                if (def.Minifiable && !(thing is MinifiedThing)) thing = thing.MakeMinified();
                // Carried over by hand: set down where the giver stands (or beside the visitor who made it) for now.
                string mode = null;
                Pawn carrier = null;
                if (!inRoom && PlayOuts.On && to != null && from != null && from != to && to.Spawned && to.Map == map && from.Spawned && from.Map == map)
                {
                    if (PlayOuts.OurPerson(from))
                    {
                        mode = PlayGive.Give;
                        carrier = from;
                    }
                    else if (PlayOuts.OurPerson(to))
                    {
                        mode = PlayGive.Fetch;
                        carrier = to;
                    }
                    if (carrier != null) near = from.Position;
                }
                if (!GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near, out var lying))
                {
                    if (!thing.Destroyed) thing.Destroy();
                    return;
                }
                thing = lying ?? thing;
                if (carrier != null && Handed(ctx, thing, carrier, to, from, mode)) return;
                var who = to ?? mk;
                if (who == null) return;
                string text = mk != null && mk != who
                    ? "CC_Made_By".Translate(mk.Named("MAKER"), who.Named("PAWN"), label.Named("THING")).Resolve()
                    : "CC_Made_Here".Translate(who.Named("PAWN"), label.Named("THING")).Resolve();
                Messages.Message(text, new LookTargets(thing), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("made thing " + things, e);
            }
        }

        /// <summary>Sets the handing over going. False when it couldn't be (the thing is then left where it lies, unforbidden).</summary>
        private bool Handed(DilemmaContext ctx, Thing thing, Pawn carrier, Pawn to, Pawn from, string mode)
        {
            thing.SetForbidden(true, false);
            pending = thing;
            pendingCarrier = carrier;
            pendingTo = to;
            pendingFrom = from;
            pendingMode = mode;
            float was = chance;
            chance = 1f;
            PlayOut p;
            try
            {
                p = Create(ctx);
            }
            finally
            {
                chance = was;
                pending = null;
                pendingCarrier = pendingTo = pendingFrom = null;
                pendingMode = null;
            }
            if (p != null) return true;
            thing.SetForbidden(false, false);
            return false;
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (pending == null || pendingCarrier == null) return false;
            p.things = new List<Thing> { pending };
            p.visitor = pendingCarrier;
            p.organizer = pendingCarrier;
            p.invited = new List<Pawn> { pendingCarrier };
            p.mode = pendingMode;
            // A gift goes to the one it's for; a thing collected from a visitor, to the table (the visitor stands by it).
            p.other = pendingMode == PlayGive.Give ? pendingTo : pendingFrom;
            p.named = new List<Pawn>();
            foreach (var x in new[] { pendingCarrier, pendingTo, pendingFrom })
                if (x != null && !p.named.Contains(x) && PlayOuts.OurPerson(x)) p.named.Add(x);
            p.count = 1;
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, false));
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) =>
            "CC_Play_Label_Give".Translate(PlayGive.Label(pending).Named("THING")).Resolve();

        /// <summary>The first of the candidates this game has, of the colony's era.</summary>
        public static ThingDef Pick(string names)
        {
            if (names.NullOrEmpty()) return null;
            var defs = names.Split('|').Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n.Trim())).ToList();
            int i = OnMapMath.FirstFitting(defs.Select(d => d == null ? -1 : (int)d.techLevel).ToList(), (int)Avail.EffectiveTech);
            return i >= 0 ? defs[i] : null;
        }

        private Thing Make(ThingDef def, Pawn mk, SkillDef sk)
        {
            ThingDef st = null;
            if (def.MadeFromStuff)
            {
                var wanted = stuff.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(stuff);
                st = wanted != null && wanted.stuffProps != null && wanted.stuffProps.CanMake(def) ? wanted : GenStuff.DefaultStuffFor(def);
                if (st == null) return null;
            }
            var thing = ThingMaker.MakeThing(def, st);
            var q = thing.TryGetComp<CompQuality>();
            var art = thing.TryGetComp<CompArt>();
            if (q != null)
            {
                var made = mk?.skills != null && sk != null && !(mk.skills.GetSkill(sk)?.TotallyDisabled ?? true)
                    ? QualityUtility.GenerateQualityCreatedByPawn(mk, sk, false)
                    : quality;
                q.SetQuality(made, ArtGenerationContext.Colony);
            }
            else art?.InitializeArt(ArtGenerationContext.Colony);
            if (art != null && mk != null) art.JustCreatedBy(mk);
            // A book written out by its maker: their name on it.
            if (thing is Book book && mk != null)
            {
                try
                {
                    book.GenerateBook(mk, null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("made book", e);
                }
            }
            return thing;
        }

        private static IntVec3 Spot(Map map, Pawn to, Pawn mk, Pawn a)
        {
            foreach (var p in new[] { to, mk, a })
                if (p != null && p.Spawned && p.Map == map) return p.Position;
            return DropCellFinder.TradeDropSpot(map);
        }
    }

    /// <summary>
    /// Wood someone cut: <see cref="count"/> logs, from trees really felled near them (<see cref="near"/>: the visitor X,
    /// the camp, the gate where the hungry came, or A), the nearest ordinary timber first, outside the home area and the
    /// colony's zones, never anything sown, marked, special or more than a handful of trees. The logs lie by the stumps.
    /// With <see cref="fell"/> off (wood split at the gate, say), or no tree to be had, or someone at home whose faith is
    /// against felling trees, the logs are set down where the work was done.
    /// </summary>
    public class DilemmaEffect_Felled : DilemmaEffect
    {
        public int count = 20;
        public PlaySpot near = PlaySpot.Stranger;
        public bool fell = true;
        public float radius = 24f;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || count <= 0) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var at = Anchor(map, ctx);
                var stumps = new List<IntVec3>();
                if (fell && !ctx.Colonists.Any(p => Beliefs.Forbids(p, Belief.NoTreeCutting)))
                {
                    var trees = GenRadial.RadialDistinctThingsAround(at, map, radius, true).OfType<Plant>().Where(t => Fellable(t, map))
                        .OrderBy(t => t.Position.DistanceToSquared(at)).Take(OnMapMath.MaxTrees * 2).ToList();
                    int n = OnMapMath.TreesToFell(trees.Select(t => t.YieldNow()).ToList(), count);
                    foreach (var t in trees.Take(n))
                    {
                        var pos = t.Position;
                        try
                        {
                            t.TrySpawnStump(PlantDestructionMode.Chop);
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("felled tree stump", e);
                        }
                        if (!t.Destroyed) t.Destroy();
                        stumps.Add(pos);
                    }
                }
                if (stumps.Count == 0) stumps.Add(at);
                var shares = OnMapMath.Shares(count, stumps.Count);
                var logs = new List<Thing>();
                for (int i = 0; i < shares.Count; i++)
                {
                    int left = shares[i];
                    while (left > 0)
                    {
                        var wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                        wood.stackCount = Math.Min(left, ThingDefOf.WoodLog.stackLimit);
                        left -= wood.stackCount;
                        if (GenPlace.TryPlaceThing(wood, stumps[i], map, ThingPlaceMode.Near)) logs.Add(wood);
                    }
                }
                if (logs.Count > 0)
                    Messages.Message("CC_Felled_Wood".Translate(count).Resolve(), new LookTargets(logs), MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("felled wood", e);
            }
        }

        private IntVec3 Anchor(Map map, DilemmaContext ctx)
        {
            var who = ctx.Colonists.FirstOrDefault(p => !p.Downed);
            switch (near)
            {
                case PlaySpot.Stranger:
                    if (ctx.stranger != null && ctx.stranger.Spawned && ctx.stranger.Map == map) return ctx.stranger.Position;
                    break;
                case PlaySpot.Camp:
                    var camp = CampTracker.Instance?.camp;
                    if (camp != null && camp.map == map) return camp.FireCell;
                    break;
                case PlaySpot.Gate:
                    if (who != null)
                    {
                        var c = PlaySpots.GateCell(map, who);
                        if (c.IsValid) return c;
                    }
                    break;
            }
            if (ctx.a != null && ctx.a.Spawned && ctx.a.Map == map) return ctx.a.Position;
            return DropCellFinder.TradeDropSpot(map);
        }

        /// <summary>A tree anyone would cut for firewood: ordinary timber, grown, wild, outside home and zones, nobody's mark on it.</summary>
        public static bool Fellable(Plant t, Map map)
        {
            if (t == null || !t.Spawned || t.def?.plant == null || !t.def.plant.IsTree) return false;
            if (t.def.plant.harvestedThingDef != ThingDefOf.WoodLog || t.def.plant.cavePlant || !t.HarvestableNow || t.sown) return false;
            if (!t.AllComps.NullOrEmpty()) return false;
            var c = t.Position;
            if (map.areaManager.Home[c] || c.GetZone(map) != null || map.designationManager.DesignationOn(t) != null) return false;
            return !c.Fogged(map);
        }
    }

    /// <summary>
    /// Saplings planted: one for A (<see cref="children"/>: one for each child of the colony, up to six), young trees of
    /// the kind that grows wild here, at the edge of the colony's fields (<see cref="near"/> Outdoors: in the open by the
    /// colony's walls; no fields: there too), a little apart from each other, on ground a tree can grow in. With
    /// <see cref="markers"/>, a stone beside each, a chunk of the rock that lies under this land.
    /// </summary>
    public class DilemmaEffect_Plant : DilemmaEffect
    {
        public bool children;
        public PlaySpot near = PlaySpot.Fields;
        public bool markers;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var tree = TreeFor(map);
                if (tree == null) return;
                int kids = children ? ctx.Colonists.Count(p => p.DevelopmentalStage.Child()) : 1;
                int n = OnMapMath.Saplings(kids);
                var at = near == PlaySpot.Fields ? FieldEdge(map) : IntVec3.Invalid;
                if (!at.IsValid) at = ByTheWalls(map);
                var planted = new List<Thing>();
                foreach (var c in GenRadial.RadialCellsAround(at, 14f, false))
                {
                    if (planted.Count >= n) break;
                    if (!Ground(tree, c, map) || planted.Any(t => t.Position.InHorDistOf(c, 2.9f))) continue;
                    var plant = (Plant)ThingMaker.MakeThing(tree);
                    plant.Growth = 0.05f;
                    plant.sown = true;
                    GenSpawn.Spawn(plant, c, map, WipeMode.Vanish);
                    planted.Add(plant);
                }
                if (planted.Count == 0) return;
                if (markers)
                {
                    var stone = MarkerStone(map);
                    if (stone != null)
                        foreach (var t in planted) Marker(map, t.Position, stone);
                }
                string text = planted.Count == 1 ? "CC_Saplings_One".Translate().Resolve() : "CC_Saplings_Many".Translate(planted.Count).Resolve();
                Messages.Message(text, new LookTargets(planted), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saplings planted", e);
            }
        }

        /// <summary>A chunk of the rock that lies under this land (the first of its rock kinds), or null.</summary>
        private static ThingDef MarkerStone(Map map)
        {
            try
            {
                foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                {
                    var chunk = rock?.building?.mineableThing;
                    if (chunk != null && chunk.category == ThingCategory.Item) return chunk;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("sapling stones", e);
            }
            return null;
        }

        /// <summary>A stone set down beside a sapling, on free ground next to it.</summary>
        private static void Marker(Map map, IntVec3 at, ThingDef stone)
        {
            foreach (var c in GenAdj.CellsAdjacentCardinal(at, Rot4.North, IntVec2.One))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetZone(map) != null) continue;
                if (c.GetPlant(map) is Plant p && p.def.plant.IsTree) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(stone), c, map);
                return;
            }
        }

        /// <summary>The commonest ordinary tree that grows wild here.</summary>
        private static ThingDef TreeFor(Map map)
        {
            var biome = map.Biome;
            if (biome == null) return null;
            return biome.AllWildPlants
                .Where(d => d?.plant != null && d.plant.IsTree && d.plant.harvestedThingDef == ThingDefOf.WoodLog && !d.plant.cavePlant && d.comps.NullOrEmpty())
                .OrderByDescending(d => biome.CommonalityOfPlant(d)).FirstOrDefault();
        }

        /// <summary>Just outside the colony's biggest field, or nowhere when it has none.</summary>
        private static IntVec3 FieldEdge(Map map)
        {
            var zone = map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.CellCount).FirstOrDefault();
            if (zone == null || zone.Cells.Count == 0) return IntVec3.Invalid;
            var middle = zone.Cells[zone.Cells.Count / 2];
            foreach (var c in GenRadial.RadialCellsAround(middle, 30f, true))
                if (c.InBounds(map) && c.GetZone(map) == null && c.Standable(map) && !c.Roofed(map)) return c;
            return IntVec3.Invalid;
        }

        /// <summary>The open ground nearest the middle of the colony: just outside its walls.</summary>
        private static IntVec3 ByTheWalls(Map map)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 30f, true))
                if (c.InBounds(map) && !c.Roofed(map) && c.Standable(map) && c.GetZone(map) == null && c.GetEdifice(map) == null) return c;
            return center;
        }

        private static bool Ground(ThingDef tree, IntVec3 c, Map map)
        {
            if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || c.GetZone(map) != null || c.GetEdifice(map) != null) return false;
            if (c.GetPlant(map) is Plant p && p.def.plant.IsTree) return false;
            if (c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null || map.designationManager.AllDesignationsAt(c).Any()) return false;
            return tree.CanEverPlantAt(c, map, true, false);
        }
    }

    /// <summary>
    /// A new room marked out as building plans, the way the room planner places one: the first of <see cref="templates"/>
    /// the colony can build, at the spot the planner would suggest, with the player's own planner settings (what may be cut
    /// or mined). No spot to be had: nothing is placed, and the answer's words stand.
    /// </summary>
    public class DilemmaEffect_PlanRoom : DilemmaEffect
    {
        public List<RoomTemplateDef> templates;
        public PlayWho forWhom = PlayWho.A;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || templates.NullOrEmpty()) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                foreach (var t in templates)
                {
                    if (t == null || t.UnavailableReason(map) != null) continue;
                    var cfg = RoomPlanConfig.For(t);
                    var trial = cfg.WithDoor(cfg.DoorX(t));
                    var furniture = t.Resolve(map, out _, trial);
                    if (!TemplatePlanner.FindSpot(t, map, furniture, trial, out var anchor, out int rot)) continue;
                    trial.rotation = rot;
                    var plan = TemplatePlanner.Plan(t, map, anchor, trial, furniture);
                    if (plan == null || !plan.CanPlace) continue;
                    TemplatePlanner.Execute(plan, map);
                    var who = PlayOuts.Who(forWhom, ctx) ?? ctx.a;
                    var look = plan.door.IsValid ? new LookTargets(plan.door, map) : new LookTargets(anchor, map);
                    if (who != null)
                        Messages.Message("CC_PlanRoom_Marked".Translate(who.Named("PAWN"), t.label.Named("ROOM")).Resolve(), look, MessageTypeDefOf.NeutralEvent, false);
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("room marked out", e);
            }
        }
    }

    /// <summary>
    /// A search of A's own room, and what's found burned in the yard: the addictive drugs A carries and those lying in A's
    /// own bedroom (a room nobody else sleeps in) are destroyed, and ash and smoke show where they burned. Nothing else of
    /// the colony's is touched: shared rooms and the stores aren't searched.
    /// </summary>
    public class DilemmaEffect_BurnFound : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var map = ctx?.map;
            if (a == null || map == null || a.Dead) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var found = new List<Thing>();
                if (a.inventory?.innerContainer != null) found.AddRange(a.inventory.innerContainer.Where(Burnable));
                var room = RoomUtil.BedRoom(a);
                if (room != null && room.ProperRoom && !room.PsychologicallyOutdoors && room.ContainedBeds.All(b => b.OwnersForReading.All(o => o == a)))
                    found.AddRange(room.ContainedAndAdjacentThings.Where(t => t.GetRoom() == room && Burnable(t) && t.Spawned));
                found = found.Distinct().ToList();
                if (found.Count == 0) return;
                string what = TextUtil.JoinAnd(found.Select(t => t.LabelCap.ToString()).ToList());
                foreach (var t in found) t.Destroy();
                var yard = PlaySpots.OpenCell(map, a);
                if (yard.IsValid)
                {
                    FilthMaker.TryMakeFilth(yard, map, ThingDefOf.Filth_Ash, 2);
                    for (int i = 0; i < 3; i++) FleckMaker.ThrowSmoke(yard.ToVector3Shifted(), map, 1.2f);
                }
                Messages.Message("CC_Burned_Found".Translate(a.Named("PAWN"), what.Named("WHAT")).Resolve(),
                    yard.IsValid ? new LookTargets(yard, map) : new LookTargets(a), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("stash burned", e);
            }
        }

        /// <summary>An addictive drug (not medicine, not a social drink without a hold on anyone).</summary>
        private static bool Burnable(Thing t)
        {
            var d = t?.def;
            if (d == null || !d.IsNonMedicalDrug) return false;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            return drug != null && drug.Addictive;
        }
    }
}
