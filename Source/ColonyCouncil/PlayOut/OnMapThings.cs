using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Things an answer says now exist, made real on the map where that's safe: a carving made by someone's hands and set
    // down beside the one it's for, a hat bought on the side, logs where a guest really felled the trees, the children's
    // saplings at the edge of the fields, a new room marked out as building plans, a stash burned in the yard. Each only
    // with what the colony's era and DLCs have (looked up by name, never required), and each falls back quietly to the
    // answer's words alone when it can't be done. The pure part is OnMapMath.cs.

    /// <summary>
    /// A thing someone made (<see cref="maker"/>: its quality from their <see cref="skill"/>, the story's own skill, else
    /// Artistic; a work of art carries their name) or got hold of (no maker: <see cref="quality"/>), set down beside
    /// <see cref="forWhom"/> (A unless given), packed up for moving if it's furniture. <see cref="things"/>: the candidates,
    /// "SculptureSmall" or "Apparel_HatTop|Apparel_BowlerHat", the first that exists and fits the colony's era; none does,
    /// nothing is made. Of <see cref="stuff"/> when it may be (wood unless given), else the thing's usual stuff.
    /// </summary>
    public class DilemmaEffect_Made : DilemmaEffect
    {
        public string things;
        public PlayWho maker = PlayWho.None;
        public PlayWho forWhom = PlayWho.A;
        public SkillDef skill;
        public QualityCategory quality = QualityCategory.Normal;
        public string stuff = "WoodLog";

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
                var thing = Make(def, mk, skill ?? ctx.skill ?? SkillDefOf.Artistic);
                if (thing == null) return;
                var near = Spot(map, to, mk, ctx.a);
                string label = thing.LabelNoCount;
                if (def.Minifiable) thing = thing.MakeMinified();
                if (!GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near))
                {
                    thing.Destroy();
                    return;
                }
                var who = to != null && !to.Dead ? to : mk;
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
    /// colony's walls; no fields: there too), a little apart from each other, on ground a tree can grow in.
    /// </summary>
    public class DilemmaEffect_Plant : DilemmaEffect
    {
        public bool children;
        public PlaySpot near = PlaySpot.Fields;

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
                string text = planted.Count == 1 ? "CC_Saplings_One".Translate().Resolve() : "CC_Saplings_Many".Translate(planted.Count).Resolve();
                Messages.Message(text, new LookTargets(planted), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saplings planted", e);
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
