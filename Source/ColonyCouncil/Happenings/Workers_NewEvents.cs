using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk events that grow out of what is happening at the colony right now (texts:
    // Defs/IncidentDefs/Rimfolk_Events_New.xml): a young animal left by a hunter's kill, a wild one of the colony's own
    // livestock kind at the pens, a miner breaking into a pocket of ore, a visitor's challenge at the game table, and a
    // neighbour's commission for one fine made thing.

    // ===================== Orphaned young =====================

    /// <summary>
    /// After a colonist kills a wild animal (a grown one of a kind that has young, not a predator), a young one of the same
    /// kind stays by the kill: one already near, else one that was there and is found now. Tame it with the game's taming,
    /// take it in and feed it by hand, or leave it. Noted when the animal dies (memo "orphan": the hunter, the kind, the
    /// cell), and sometimes the event follows a few hours later; the storyteller can draw it while the kill is fresh.
    /// </summary>
    public class HappeningWorker_Orphan : IncidentWorker_Happening
    {
        public const string Kind = "orphan";
        private const string DefName = "Council_Evt_OrphanedYoung";
        private const float FreshDays = 1f;

        /// <summary>A grown wild animal of a kind with young, not a hunter of others and not a monster.</summary>
        public static bool Fits(Pawn victim)
        {
            var r = victim?.RaceProps;
            if (r == null || !r.Animal || r.predator || r.Insect || r.Dryad || r.baseBodySize > 2.6f) return false;
            if (r.lifeStageAges == null || r.lifeStageAges.Count < 2 || victim.kindDef == null) return false;
            return victim.ageTracker != null && victim.ageTracker.CurLifeStageIndex >= r.lifeStageAges.Count - 1;
        }

        /// <summary>A wild animal died on a home map (Harmony hook, after the death): noted if a colonist brought it down.</summary>
        public static void Killed(HappeningTracker t, Pawn victim, DamageInfo? dinfo, IntVec3 cell)
        {
            var hunter = dinfo?.Instigator as Pawn;
            if (t == null || !cell.IsValid || hunter == null || !hunter.IsColonist || !hunter.RaceProps.Humanlike || !Fits(victim)) return;
            var map = victim.MapHeld ?? hunter.Map;
            if (map == null || !map.IsPlayerHome) return;
            foreach (var old in t.Memos(Kind, 0f, map).ToList()) t.Use(old);
            var m = t.Remember(Kind, null, null, victim.kindDef.defName, cell.x + "," + cell.z, false, hunter, map);
            if (m != null) m.amount = map.uniqueID;
            // Now and then the young one is found a few hours later (the storyteller may still bring it while the kill is fresh).
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            if (def == null || !S.worldEvents || !Rand.Chance(0.2f)) return;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                Find.Storyteller.incidentQueue.Add(def, Now + Rand.Range(GenDate.TicksPerHour, GenDate.TicksPerHour * 3), parms, GenDate.TicksPerHour * 4);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("orphaned young queued", e);
            }
        }

        private static Memo Fresh(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(Kind).Where(m => m.amount == map.uniqueID && Now - m.tick < FreshDays * GenDate.TicksPerDay)
                .OrderByDescending(m => m.tick).FirstOrDefault();

        private static IntVec3 CellOf(Memo m)
        {
            var parts = m?.text?.Split(',');
            return parts != null && parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z) ? new IntVec3(x, 0, z) : IntVec3.Invalid;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var m = Fresh(map);
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(m?.def ?? "");
            var cell = CellOf(m);
            return kind != null && cell.IsValid && cell.InBounds(map) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(kind.race, 0f);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var m = Fresh(map);
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(m?.def ?? "");
            var cell = CellOf(m);
            if (kind == null || !cell.IsValid) return false;
            ctx.note = Kind + ":" + m.tick;
            ctx.a = m.other != null && !m.other.Dead && m.other.Spawned && m.other.Map == map ? m.other : VarietyUtil.Best(map, SkillDefOf.Animals);
            if (ctx.a == null) return false;
            ctx.beast = kind.label;
            ctx.thing = kind.race;
            // The body, if it still lies where it fell.
            var body = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                .FirstOrDefault(c => c.Spawned && c.InnerPawn?.kindDef == kind && c.Position.InHorDistOf(cell, 3f));
            if (body != null)
            {
                ctx.place = body;
                cell = body.Position;
            }
            ctx.spot = cell;
            // A young one of its kind already about; else the one that was with it.
            ctx.stranger = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.kindDef == kind && p.Faction == null && !p.Dead && !p.Downed
                                                                            && p.ageTracker.CurLifeStageIndex < kind.RaceProps.lifeStageAges.Count - 1 && p.Position.InHorDistOf(cell, 25f));
            if (ctx.stranger == null)
            {
                float grown = kind.RaceProps.lifeStageAges[kind.RaceProps.lifeStageAges.Count - 1].minAge;
                float age = Mathf.Max(0.05f, grown * Rand.Range(0.3f, 0.6f));
                try
                {
                    ctx.stranger = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        fixedBiologicalAge: age, fixedChronologicalAge: age));
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("orphaned young", e);
                    return false;
                }
                ctx.xWaiting = true;
            }
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var young = ctx.stranger;
            if (young.Spawned) young.jobs?.StopAll();
            else
            {
                if (!CellFinder.TryFindRandomCellNear(ctx.spot, map, 3, c => c.Standable(map) && c.GetFirstPawn(map) == null, out var at)) at = ctx.spot;
                if (!at.Standable(map)) return false;
                GenSpawn.Spawn(young, at, map);
                ctx.xWaiting = false;
            }
            CouncilGameComponent.Instance?.happenings.Use(Fresh(map));
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.xWaiting && ctx.stranger != null && !ctx.stranger.Spawned && !ctx.stranger.Discarded) Find.WorldPawns.PassToWorld(ctx.stranger, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.place != null && ctx.place.Spawned) tags.Add("body");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned || ctx.stranger.Faction != null) return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            return option == 0 && !TameUtility.CanTame(ctx.stranger) ? "Rimfolk_Evt_CantTame".Translate(ctx.beast ?? ctx.stranger.LabelShort).ToString() : null;
        }

        /// <summary>The young one belongs where it is (wild): nothing to let go of.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    // ===================== A wild one at the pens =====================

    /// <summary>
    /// At dusk a wild animal of the colony's own livestock kind (one that lives wild on this land) comes down to the pens:
    /// one already on the land, else one in from the nearest edge, walking up to just outside the pen. Tame it, let it be
    /// (the colony keeps watch on it and names it, ResidentBeast), or drive it off.
    /// </summary>
    public class HappeningWorker_WildAtPens : IncidentWorker_Happening
    {
        private static IEnumerable<Thing> Pens(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.TryGetComp<CompAnimalPenMarker>() != null);

        /// <summary>The colony's penned kinds that also live wild here (and could come now): not predators.</summary>
        private static List<PawnKindDef> Kinds(Map map)
        {
            var list = new List<PawnKindDef>();
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                var k = a?.kindDef;
                if (k == null || list.Contains(k) || !a.RaceProps.FenceBlocked || a.RaceProps.predator) continue;
                if (map.Biome.CommonalityOfAnimal(k) <= 0f || !map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)) continue;
                list.Add(k);
            }
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 16 && hour <= 20 && Pens(map).Any() && Kinds(map).Count > 0 && !MapFacts.ThreatOn(map);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var kind = Kinds(map).RandomElementWithFallback();
            var pen = Pens(map).RandomElementWithFallback();
            if (kind == null || pen == null) return false;
            ctx.place = pen;
            ctx.beast = kind.label;
            ctx.thing = kind.race;
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Animals);
            if (ctx.a == null) return false;
            if (!CellFinder.TryFindRandomCellNear(pen.Position, map, 12, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && !c.InHorDistOf(pen.Position, 4f)
                                                                                 && map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)), out var spot))
                return false;
            ctx.spot = spot;
            ctx.stranger = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.kindDef == kind && p.Faction == null && !p.Dead && !p.Downed && !p.InMentalState && !p.Position.Fogged(map));
            if (ctx.stranger == null)
            {
                try
                {
                    ctx.stranger = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("wild one at the pens", e);
                    return false;
                }
                ctx.xWaiting = true;
            }
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var x = ctx.stranger;
            if (!x.Spawned)
            {
                if (!CellFinder.TryFindRandomEdgeCellNearWith(ctx.spot, 60f, map, c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, ctx.spot, Verse.AI.PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)), out var edge))
                    return false;
                GenSpawn.Spawn(x, edge, map);
                ctx.xWaiting = false;
            }
            // It comes down to just outside the pen, and keeps about there a while.
            try
            {
                x.jobs?.StopAll();
                var job = JobMaker.MakeJob(JobDefOf.Goto, ctx.spot);
                job.locomotionUrgency = Verse.AI.LocomotionUrgency.Walk;
                x.jobs?.StartJob(job, Verse.AI.JobCondition.InterruptForced);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("wild one at the pens walk", e);
            }
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.xWaiting && ctx.stranger != null && !ctx.stranger.Spawned && !ctx.stranger.Discarded) Find.WorldPawns.PassToWorld(ctx.stranger, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned || ctx.stranger.Faction != null) return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            return option == 0 && !TameUtility.CanTame(ctx.stranger) ? "Rimfolk_Evt_CantTame".Translate(ctx.beast ?? ctx.stranger.LabelShort).ToString() : null;
        }

        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    /// <summary>
    /// The colony keeps watch on the wild animal and someone gives it a name: the story of a beast that stays
    /// (ResidentBeast), when there's none running and it is one such a story can be about.
    /// </summary>
    public class DilemmaEffect_WatchBeast : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            var rb = CouncilGameComponent.Instance?.BeastOf(ctx.map);
            if (x == null || rb == null || !rb.CanTakeUp(x)) return;
            sheet.Add(ImpactKind.Other, 0, "Rimfolk_Impact_WatchBeast".Translate(x.LabelShort).Resolve(), "Rimfolk_Impact_WatchBeastBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var comp = CouncilGameComponent.Instance;
            var rb = comp?.BeastOf(ctx.map);
            if (x == null || rb == null) return;
            rb.TakeUp(comp, x);
        }
    }

    // ===================== A rich seam =====================

    /// <summary>
    /// A colonist mining under overhead mountain breaks into a pocket of ore: a few cells of ore really set into the rock
    /// beside the cell being mined (and shown), and the seam running on deeper into the rock. Follow the seam (pocket and
    /// seam marked for mining: more ore, more hours, and more of the deep rock opened, where the game's insects nest),
    /// take only the pocket, or leave it in the rock for later.
    /// </summary>
    public class HappeningWorker_RichSeam : IncidentWorker_Happening
    {
        private static readonly (string def, float weight)[] Ores =
        {
            ("MineableSteel", 10f), ("MineableSilver", 5f), ("MineableComponentsIndustrial", 3f), ("MineableJade", 2f), ("MineableGold", 1.5f),
            ("MineablePlasteel", 1f), ("MineableUranium", 1f)
        };

        private static bool Deep(IntVec3 c, Map map) => c.InBounds(map) && (map.roofGrid.RoofAt(c)?.isThickRoof ?? false);

        /// <summary>Plain rock that ore can be set into: natural, not ore already, under the mountain.</summary>
        private static bool Rock(IntVec3 c, Map map)
        {
            if (!Deep(c, map)) return false;
            var e = c.GetEdifice(map);
            return e != null && e.def.building != null && e.def.building.isNaturalRock && !e.def.building.isResourceRock && e.def.mineable
                   && map.designationManager.DesignationAt(c, DesignationDefOf.Mine) == null;
        }

        /// <summary>Someone at the rock face now, mining a cell under the mountain with plain rock around it.</summary>
        private static Pawn Miner(Map map, out IntVec3 face)
        {
            face = IntVec3.Invalid;
            foreach (var p in Colonists(map))
            {
                if (p.CurJobDef != JobDefOf.Mine || p.DevelopmentalStage.Juvenile()) continue;
                var c = p.CurJob.targetA.Cell;
                if (!Deep(c, map) || GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Count(x => Rock(x, map)) < 3) continue;
                face = c;
                return p;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Miner(map, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Miner(ctx.map, out var face);
            if (ctx.a == null) return false;
            ctx.spot = face;
            var ore = Ores.Select(o => (d: DefDatabase<ThingDef>.GetNamedSilentFail(o.def), w: o.weight)).Where(o => o.d != null).RandomElementByWeightWithFallback(o => o.w);
            if (ore.d == null) return false;
            ctx.thing = ore.d;
            ctx.beast = ore.d.building?.mineableThing?.label ?? ore.d.label;
            return true;
        }

        /// <summary>The ore set in: a pocket beside the face (shown), then the seam running on from it into the rock.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var pocket = GenAdj.CellsAdjacent8Way(new TargetInfo(ctx.spot, map)).Where(c => Rock(c, map)).InRandomOrder().Take(Rand.RangeInclusive(3, 5)).ToList();
            if (pocket.Count < 3) return false;
            var seam = new List<IntVec3>();
            var dir = pocket[0] - ctx.spot;
            var at = pocket[0];
            for (int i = 0; i < 12 && seam.Count < Rand.RangeInclusive(5, 8); i++)
            {
                var next = at + dir;
                if (!Rock(next, map) || pocket.Contains(next) || seam.Contains(next))
                {
                    // The seam bends where the rock does.
                    next = GenAdj.CellsAdjacentCardinal(at, Rot4.North, IntVec2.One).Where(c => Rock(c, map) && !pocket.Contains(c) && !seam.Contains(c)).RandomElementWithFallback(IntVec3.Invalid);
                    if (!next.IsValid) break;
                    dir = next - at;
                }
                seam.Add(next);
                at = next;
            }
            ctx.things.Clear();
            foreach (var c in pocket.Concat(seam))
            {
                var t = SetOre(map, c, ctx.thing);
                if (t != null) ctx.things.Add(t);
            }
            ctx.variant = ctx.things.Count(t => pocket.Contains(t.Position));
            if (ctx.variant == 0) return false;
            foreach (var c in pocket) map.fogGrid.Unfog(c);
            return true;
        }

        private static Thing SetOre(Map map, IntVec3 c, ThingDef ore)
        {
            try
            {
                c.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
                return GenSpawn.Spawn(ThingMaker.MakeThing(ore), c, map, WipeMode.Vanish);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("rich seam ore", e);
                return null;
            }
        }

        protected override LookTargets Look(HappeningContext ctx) =>
            ctx.things.FirstOrDefault(t => t.Spawned) is Thing t ? new LookTargets(t) : base.Look(ctx);

        protected override void Abort(HappeningContext ctx)
        {
        }
    }

    /// <summary>The ore the event set into the rock is marked for mining: the pocket only, or (<see cref="seam"/>) the whole seam.</summary>
    public class DilemmaEffect_MineSeam : DilemmaEffect
    {
        public bool seam;

        private List<Thing> Cells(DilemmaContext ctx)
        {
            var h = ctx as HappeningContext;
            if (h == null) return new List<Thing>();
            var ore = h.things.Where(t => t != null && t.Spawned && !t.Destroyed).ToList();
            return seam ? ore : ore.Take(h.variant).ToList();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = Cells(ctx).Count;
            if (n > 0) sheet.Play("Rimfolk_Impact_MineSeam".Translate(n).Resolve(), "Rimfolk_Impact_MineSeamBrief".Translate(n).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var t in Cells(ctx))
            {
                var dm = t.Map.designationManager;
                if (dm.DesignationAt(t.Position, DesignationDefOf.Mine) == null) dm.AddDesignation(new Designation(t.Position, DesignationDefOf.Mine));
            }
        }
    }

    // ===================== A game for a stake =====================

    /// <summary>
    /// A visitor on the map challenges the colony's best at its chess or poker table (or any other game table of the
    /// mind). Accepted, the two sit down across that very table (a visit that sits at the event's place, PlaySit); the
    /// game is played out while they sit, and skill and luck decide it (Hour). For silver, the stake changes hands in
    /// person; for fun, the others gather in the rec room to watch.
    /// </summary>
    public class HappeningWorker_TableGame : IncidentWorker_Happening
    {
        public const string Kind = "tablegame";

        private static bool GameTable(Thing t) =>
            t?.def?.building != null && (t.def.defName == "ChessTable" || t.def.defName == "PokerTable" || t.def.building.joyKind?.defName == "Gaming_Cerebral");

        private static SkillDef SkillFor(Thing t) => t?.def?.defName == "PokerTable" ? SkillDefOf.Social : SkillDefOf.Intellectual;

        private static IEnumerable<Thing> Tables(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && !(b is Frame) && GameTable(b) && !b.IsForbidden(Faction.OfPlayer));

        private static IEnumerable<Pawn> Players(Map map, SkillDef skill) =>
            Strangers.On(map, StrangerKind.Visitor).Where(x => !x.DevelopmentalStage.Juvenile() && Together.Able(x) && x.skills?.GetSkill(skill) is SkillRecord r && !r.TotallyDisabled
                                                               && !(x.mindState?.duty?.def?.defName ?? "").StartsWith("ExitMap"));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !(CouncilGameComponent.Instance?.happenings.visits.Any(v => v.kind == Kind) ?? false) && Tables(map).Any(t => Players(map, SkillFor(t)).Any());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var table = Tables(map).Where(t => Players(map, SkillFor(t)).Any()).RandomElementWithFallback();
            if (table == null) return false;
            ctx.place = table;
            ctx.skill = SkillFor(table);
            ctx.stranger = Players(map, ctx.skill).OrderByDescending(x => x.skills.GetSkill(ctx.skill).Level).FirstOrDefault();
            ctx.faction = ctx.stranger?.Faction;
            ctx.a = Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(ctx.skill) is SkillRecord r && !r.TotallyDisabled
                                              && p.CanReach(table, Verse.AI.PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => p.skills.GetSkill(ctx.skill).Level).FirstOrDefault();
            if (ctx.a == null || ctx.stranger == null) return false;
            ctx.count = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(Rand.Range(30f, 80f) * LivingEvents.WealthFactor(map)), 20, 250));
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.place?.def?.defName == "PokerTable" ? "poker" : ctx.place?.def?.defName == "ChessTable" ? "chess" : "game");
        }

        protected override string Extra(HappeningContext ctx)
        {
            int ours = ctx.a?.skills?.GetSkill(ctx.skill)?.Level ?? 0;
            int theirs = ctx.stranger?.skills?.GetSkill(ctx.skill)?.Level ?? 0;
            return "Rimfolk_Evt_GameOdds".Translate(ctx.a.Named("A"), ctx.stranger.Named("X"), ours, theirs, ctx.skill.label).Resolve();
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 2) return null;
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned) return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            if (option == 0 && ctx.map != null && ctx.map.resourceCounter.Silver < ctx.count) return "CC_Dilemma_NotEnough".Translate(ctx.count, ThingDefOf.Silver.label).ToString();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault && ctx.a != null && ctx.stranger != null)
            {
                var v = CouncilGameComponent.Instance?.happenings.AddVisit(Kind, ctx.incident, ctx.map, ctx.stranger, ctx.a, null, Now + GenDate.TicksPerDay);
                if (v != null)
                {
                    v.place = ctx.place;
                    v.skill = ctx.skill;
                    v.stake = option == 0 ? ctx.count : 0;
                    v.due = Now;
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>
        /// Every hour: once the two are seen sitting across the table, the game is played out, and the next hour (or when
        /// they get up) it's decided. Never sat down within the day: it's off.
        /// </summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            var a = v.a;
            var x = v.x;
            bool sitting = a != null && x != null && PlaySit.Visiting(a, x);
            if (v.text != "sat")
            {
                if (sitting)
                {
                    v.text = "sat";
                    v.due = now + GenDate.TicksPerHour / 2;
                    return true;
                }
                if (now > v.until || x == null || x.Dead || !x.Spawned)
                {
                    News.Post("Rimfolk_Evt_GameOff".Translate().Resolve(), null, NewsKind.World, MessageTypeDefOf.NeutralEvent);
                    return false;
                }
                return true;
            }
            if (sitting && now < v.due) return true;
            Play(v, comp);
            return false;
        }

        /// <summary>The game decided, with the two at the table (or just up from it): the stake changes hands, and the colony hears.</summary>
        private static void Play(Visit v, CouncilGameComponent comp)
        {
            // together: called once the two have sat down across the table (Hour sees them on the sit-down visit).
            var a = v.a;
            var x = v.x;
            v.done = true;
            if (a == null || x == null || a.Dead || x.Dead || v.skill == null) return;
            int al = a.skills?.GetSkill(v.skill)?.Level ?? 0;
            int xl = x.skills?.GetSkill(v.skill)?.Level ?? 0;
            bool won = Rand.Chance(Mathf.Clamp(0.5f + (al - xl) * 0.06f, 0.15f, 0.85f));
            var map = v.map ?? a.MapHeld;
            var ctx = new HappeningContext { incident = v.incident, map = map, a = a, stranger = x, skill = v.skill, count = v.stake, faction = x.Faction, place = v.place };
            Arrivals.Say(a, (won ? "CC_Evt_Contest_Won" : "CC_Evt_Contest_Lost").Translate().Resolve());
            Arrivals.Say(x, (won ? "CC_Evt_Contest_Lost" : "CC_Evt_Contest_Won").Translate().Resolve());
            if (won)
            {
                CouncilGameComponent.GiveThought(a, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_WonTableGame"));
                // The visitor counts the stake out in person (Couriers/), or it's left on the table.
                if (v.stake > 0 && !(Couriers.InPerson(x, map) && Couriers.Deliver(map, Couriers.Make(ThingDefOf.Silver, v.stake), x.Faction, a.PositionHeld, x)))
                    VarietyUtil.PaySilver(map, v.place != null && v.place.Spawned ? v.place.Position : a.PositionHeld, v.stake);
            }
            else
            {
                CouncilGameComponent.GiveThought(a, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_LostTableGame"));
                if (v.stake > 0)
                {
                    // The colony pays up: carried over to the visitor from the stores while they're here, else taken from them.
                    if (Couriers.InPerson(x, map)) new DilemmaEffect_CarryOut { what = "silver", count = v.stake, who = PlayWho.A, to = PlaySpot.Stranger, steward = false }.Apply(ctx);
                    else new DilemmaEffect_Consume { what = "silver", count = v.stake }.Apply(ctx);
                }
            }
            a.skills?.Learn(v.skill, won ? 400f : 600f, true);
            string key = won ? (v.stake > 0 ? "Rimfolk_Evt_GameWonStake" : "Rimfolk_Evt_GameWon") : (v.stake > 0 ? "Rimfolk_Evt_GameLostStake" : "Rimfolk_Evt_GameLost");
            string game = v.place?.def?.defName == "PokerTable" ? "Rimfolk_Evt_Game_Poker".Translate().Resolve() : v.place?.def?.defName == "ChessTable" ? "Rimfolk_Evt_Game_Chess".Translate().Resolve() : (v.place?.LabelNoCount ?? "?");
            string text = key.Translate(a.Named("A"), x.Named("X"), game.Named("GAME"), v.stake.Named("COUNT")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Notable, null, (won ? "Rimfolk_Evt_GameWonTitle" : "Rimfolk_Evt_GameLostTitle").Translate(a.Named("A"), game.Named("GAME")).Resolve(), text,
                won ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, new LookTargets(a));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, a));
        }
    }

    // ===================== A commission =====================

    /// <summary>
    /// A neighbour wants one fine made thing the colony can make: a coat, a bow, a rifle, a blade. Taken, a real bill goes
    /// up at the bench that makes it (Making.AddBill, counting only pieces of the quality asked for); when one is made,
    /// their rider comes for it and pays (Couriers.CollectThings), and a masterwork makes them a patron who comes back.
    /// Memo "commission": def = the product, amount = the price, left = the least quality, times = the highest thing id
    /// of its kind when taken (so only a piece made since counts), text = the bill's id, due = the deadline.
    /// </summary>
    public class HappeningWorker_Commission : IncidentWorker_Happening
    {
        public const string Kind = "commission";
        public const string Patron = "patron";
        private const string DefName = "Council_Evt_Commission";
        private const int DueDays = 15;

        private static readonly string[] Products =
        {
            "Apparel_Parka", "Apparel_Duster", "Apparel_Jacket", "Apparel_CollarShirt", "Apparel_CowboyHat", "Apparel_BowlerHat",
            "Bow_Recurve", "Bow_Great", "MeleeWeapon_LongSword", "MeleeWeapon_Gladius", "Gun_BoltActionRifle", "Gun_Revolver", "Gun_PumpShotgun"
        };

        /// <summary>A commission this colony took on is still being made.</summary>
        private static bool Pending(Map map) => CouncilGameComponent.Instance?.happenings.Memos(Kind, 0f, map).Any() ?? false;

        /// <summary>A product the colony can make now: a bench of its own with the recipe, its research done, and someone good at the work.</summary>
        private static IEnumerable<(ThingDef product, RecipeDef recipe, Building_WorkTable bench, Pawn maker)> Makeable(Map map)
        {
            var benches = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.Spawned && b.BillStack != null).ToList();
            foreach (var name in Products)
            {
                var product = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (product == null || !product.HasComp(typeof(CompQuality))) continue;
                foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (recipe.ProducedThingDef != product || !recipe.AvailableNow) continue;
                    var bench = benches.FirstOrDefault(b => b.def.AllRecipes != null && b.def.AllRecipes.Contains(recipe));
                    var skill = recipe.workSkill ?? SkillDefOf.Crafting;
                    var maker = bench == null ? null : Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(skill) is SkillRecord r && !r.TotallyDisabled && r.Level >= 6)
                        .OrderByDescending(p => p.skills.GetSkill(skill).Level).FirstOrDefault();
                    if (maker != null) yield return (product, recipe, bench, maker);
                    break;
                }
            }
        }

        /// <summary>A people who were pleased with this colony's last commission.</summary>
        private static Faction PatronOf(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(Patron, 0f, map).Select(m => m.faction).FirstOrDefault(f => f != null && !f.defeated && !f.HostileTo(Faction.OfPlayer));

        protected override bool CanHappen(Map map, HappeningExtension ext) => !Pending(map) && (PatronOf(map) ?? Arrivals.Friendly()) != null && Makeable(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = PatronOf(map) ?? Arrivals.Friendly();
            var pick = Makeable(map).RandomElementWithFallback();
            if (ctx.faction == null || pick.product == null) return false;
            ctx.thing = pick.product;
            ctx.place = pick.bench;
            ctx.a = pick.maker;
            ctx.skill = pick.recipe.workSkill ?? SkillDefOf.Crafting;
            var stuff = pick.product.MadeFromStuff ? GenStuff.DefaultStuffFor(pick.product) : null;
            float worth = Mathf.Max(30f, pick.product.GetStatValueAbstract(StatDefOf.MarketValue, stuff));
            ctx.count = VarietyUtil.Round(Mathf.RoundToInt(worth * 1.8f));
            ctx.variant = VarietyUtil.Round(Mathf.RoundToInt(worth * 1.15f));
            ctx.tale = ctx.variant.ToString();
            ctx.note = pick.recipe.defName;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.faction != null && PatronOf(ctx.map) == ctx.faction) tags.Add("patron");
            if (ctx.thing != null) tags.Add(ctx.thing.IsWeapon ? "weapon" : "apparel");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "Rimfolk_Evt_CommissionNoBench".Translate().ToString() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) Take(ctx, option == 0 ? QualityCategory.Good : QualityCategory.Normal, option == 0 ? ctx.count : ctx.variant);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The bill goes up at the bench (only pieces of the quality asked count toward it), and the colony's word is kept as a memo.</summary>
        private static void Take(HappeningContext ctx, QualityCategory least, int price)
        {
            var bench = ctx.place as Building_WorkTable;
            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(ctx.note ?? "");
            var map = ctx.map;
            if (bench == null || recipe == null || ctx.thing == null || map == null) return;
            var range = new QualityRange(least, QualityCategory.Legendary);
            var have = Pieces(map, ctx.thing, least, -1).ToList();
            int mark = map.listerThings.ThingsOfDef(ctx.thing).Select(t => t.thingIDNumber).DefaultIfEmpty(0).Max();
            var bill = Making.AddBill(bench, recipe, ctx.thing, have.Count + 1, false);
            if (bill == null) return;
            bill.qualityRange = range;
            var m = CouncilGameComponent.Instance?.happenings.Remember(Kind, null, ctx.faction, ctx.thing.defName, bill.GetUniqueLoadID(), false, ctx.a, map);
            if (m == null) return;
            m.amount = price;
            m.left = (int)least;
            m.times = mark;
            m.due = Now + DueDays * GenDate.TicksPerDay;
        }

        /// <summary>Pieces of it in the colony's stores of at least this quality (made since <paramref name="after"/>, a thing id; -1: any).</summary>
        private static IEnumerable<Thing> Pieces(Map map, ThingDef def, QualityCategory least, int after) =>
            map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.thingIDNumber > after && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()
                                                         && t.TryGetQuality(out var q) && q >= least && !(t is Apparel ap && ap.WornByCorpse));

        /// <summary>Every hour: a piece made since that's good enough is collected and paid for; past the deadline, they give up.</summary>
        public static void Hourly(HappeningTracker t, CouncilGameComponent comp, int now)
        {
            var due = t.memos.Where(m => m.kind == Kind && !m.used).ToList();
            if (due.Count == 0) return;
            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            var more = VarietyUtil.More(incident);
            foreach (var m in due)
            {
                // Made and collected at the colony that took it on (its bill is on that colony's bench).
                var map = HappeningTracker.MapFor(m);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(m.def ?? "");
                if (incident == null || def == null || map == null || m.faction == null)
                {
                    t.Use(m);
                    continue;
                }
                var ctx = new HappeningContext { incident = incident, map = map, a = m.other, faction = m.faction, thing = def, count = m.amount };
                if (m.faction.defeated || m.faction.HostileTo(Faction.OfPlayer))
                {
                    t.Use(m);
                    DropBill(map, m.text);
                    string off = VarietyUtil.Line(more?.cancelled, ctx);
                    if (!off.NullOrEmpty()) News.Post(off, null, NewsKind.World, MessageTypeDefOf.NeutralEvent);
                    continue;
                }
                var piece = Pieces(map, def, (QualityCategory)m.left, m.times).OrderBy(x => x.TryGetQuality(out var q) ? (int)q : 0).FirstOrDefault();
                if (piece != null)
                {
                    t.Use(m);
                    DropBill(map, m.text);
                    Collect(ctx, more, comp, piece);
                    continue;
                }
                if (now < m.due) continue;
                t.Use(m);
                DropBill(map, m.text);
                WorldSituationWorker.Goodwill(m.faction, -4);
                string title = VarietyUtil.Line(more?.failTitles, ctx) ?? incident.LabelCap.ToString();
                string text = VarietyUtil.Line(more?.fail, ctx);
                if (!text.NullOrEmpty()) Tell.Letter(NewsKind.World, Importance.Notable, null, title, text, LetterDefOf.NeutralEvent);
            }
        }

        /// <summary>The commission's bill comes off its bench (the piece is made, or nobody wants it now).</summary>
        private static void DropBill(Map map, string id)
        {
            if (map == null || id.NullOrEmpty()) return;
            foreach (var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                var bill = bench.BillStack?.Bills.FirstOrDefault(b => b.GetUniqueLoadID() == id);
                if (bill == null) continue;
                bench.BillStack.Delete(bill);
                return;
            }
        }

        /// <summary>Their rider comes for the piece and pays for it at the gate; a masterwork makes them the colony's patron.</summary>
        private static void Collect(HappeningContext ctx, HappeningMoreText more, CouncilGameComponent comp, Thing piece)
        {
            var map = ctx.map;
            bool fine = piece.TryGetQuality(out var q) && q >= QualityCategory.Masterwork;
            int price = fine ? VarietyUtil.Round(Mathf.RoundToInt(ctx.count * 1.25f)) : ctx.count;
            ctx.count = price;
            if (!Couriers.CollectThings(map, new List<Thing> { piece }, ctx.faction, Couriers.Make(ThingDefOf.Silver, price)))
            {
                var at = piece.Position;
                piece.Destroy();
                VarietyUtil.PaySilver(map, at, price);
            }
            WorldSituationWorker.Goodwill(ctx.faction, fine ? 8 : 4);
            if (fine)
            {
                var t = comp.happenings;
                foreach (var old in t.Memos(Patron, 0f, map).ToList()) t.Use(old);
                t.Remember(Patron, null, ctx.faction, null, null, false, ctx.a, map);
            }
            var tags = new List<string> { fine ? "fine" : "plain" };
            string title = VarietyUtil.Line(more?.doneTitles, ctx, tags) ?? ctx.incident.LabelCap.ToString();
            string text = VarietyUtil.Line(more?.done, ctx, tags, piece.LabelNoCount);
            if (text.NullOrEmpty()) return;
            Tell.Letter(NewsKind.World, Importance.Notable, null, title, text, LetterDefOf.PositiveEvent, new LookTargets(piece.Spawned ? (Thing)piece : null));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, title + ": " + text, ctx.a != null && !ctx.a.Dead ? ctx.a : null));
        }

        /// <summary>The commission on the World page ("what's coming"), or null.</summary>
        public static string WorldLine(Memo m, int now)
        {
            if (m == null || m.used || m.kind != Kind || m.faction == null) return null;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(m.def ?? "");
            if (def == null) return null;
            return "Rimfolk_World_Commission".Translate(m.faction.Name, def.label, ((QualityCategory)m.left).GetLabel(), m.amount, TextUtil.DaysString(Mathf.Max(0, m.due - now))).Resolve();
        }
    }
}
