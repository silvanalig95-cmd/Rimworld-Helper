using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The enemy dead as the answers say (PlayKind.Haul, OnMapPlays.cs): graves dug outside the wall and the bodies laid
    // in them, the bodies carried into the colony's crematorium and burned there, and the pyre lit at dusk by one of the
    // colony who carries the wood out. All of it with the game's own building, hauling and graves, nothing conjured.

    /// <summary>
    /// The enemy dead buried outside the wall ("graves": a grave each, a step of ground between them; "trench": the
    /// graves side by side, one long trench). The game's own graves: where they'll go is laid out as grave frames off
    /// the home area, beyond the gate, on ground a grave can be dug in; those of the party who can build dig them (the
    /// game's construction), and each body is carried into a finished grave (the game's own hauling into a grave). The
    /// graves take the bodies of strangers only. Whatever isn't done when the party breaks up stays for the colony: the
    /// frames are the colony's to finish, and its own haulers fill the graves; frames with no body left for them are
    /// taken up again.
    /// </summary>
    public static class PlayBurial
    {
        public static ThingDef GraveDef => ThingDefOf.Grave;

        public static bool CanDig(Map map) => map != null && GraveDef?.frameDef != null && Avail.CanBuild(GraveDef, map);

        /// <summary>
        /// Where the row of graves goes, from its first grave, and which way they're turned: the first place out past
        /// the gate (as for a pyre) where every grave of the row can be dug. Empty when there's no room anywhere near.
        /// </summary>
        public static List<IntVec3> Plot(Map map, Pawn pawn, int graves, bool apart, out Rot4 rot)
        {
            rot = Rot4.North;
            var steps = OnMapMath.GraveRow(graves, apart);
            if (map == null || steps.Count == 0 || GraveDef == null) return new List<IntVec3>();
            foreach (var origin in PlayHaul.OutsideCells(map, pawn))
                foreach (var r in new[] { Rot4.North, Rot4.East })
                {
                    // A grave turned north runs up from its cell, so the row runs east; turned east, the row runs north.
                    var along = r == Rot4.North ? IntVec3.East : IntVec3.North;
                    var cells = steps.Select(s => origin + along * s).ToList();
                    if (cells.All(c => Fits(c, r, map, pawn)))
                    {
                        rot = r;
                        return cells;
                    }
                }
            return new List<IntVec3>();
        }

        /// <summary>A grave can be dug here: ground the game lets one go on, off the home area, in the open, and nothing in the way but grass.</summary>
        private static bool Fits(IntVec3 c, Rot4 rot, Map map, Pawn pawn)
        {
            if (!GenConstruct.CanPlaceBlueprintAt(GraveDef, c, rot, map).Accepted) return false;
            foreach (var x in GenAdj.OccupiedRect(c, rot, GraveDef.size))
            {
                if (!x.InBounds(map) || map.areaManager.Home[x] || x.Roofed(map) || x.Fogged(map) || x.GetZone(map) != null) return false;
                if ((x.GetTerrain(map)?.IsWater ?? false) || x.GetFirstItem(map) != null || x.GetEdifice(map) != null) return false;
                var plant = x.GetPlant(map);
                if (plant != null && (plant.def.plant == null || plant.def.plant.IsTree || plant.def.plant.harvestedThingDef != null)) return false;
            }
            return PlaySpots.Ok(c, map, pawn);
        }

        /// <summary>The graves are marked out: a grave frame on each place of the row that hasn't one yet.</summary>
        public static void Lay(PlayOut p)
        {
            var map = p.map;
            var rot = new Rot4(p.plotRot);
            foreach (var c in p.plots)
            {
                if (FrameAt(map, c) != null || GraveAt(map, c) != null) continue;
                if (!Fits(c, rot, map, null)) continue;
                var frame = (Frame)ThingMaker.MakeThing(GraveDef.frameDef);
                frame.SetFactionDirect(Faction.OfPlayer);
                // Grass where the grave is dug goes with the digging.
                GenSpawn.Spawn(frame, c, map, rot, WipeMode.Vanish);
            }
        }

        public static Frame FrameAt(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return null;
            foreach (var t in c.GetThingList(map))
                if (t is Frame f && f.Position == c && f.def.entityDefToBuild == GraveDef) return f;
            return null;
        }

        public static Building_Grave GraveAt(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return null;
            foreach (var t in c.GetThingList(map))
                if (t is Building_Grave g && g.Position == c && g.def == GraveDef) return g;
            return null;
        }

        /// <summary>One of the dead laid in one of this burial's graves.</summary>
        public static bool Buried(PlayOut p, Thing c) => c != null && !c.Destroyed && c.ParentHolder is Building_Grave g && p.plots.Contains(g.Position);

        /// <summary>The graves take strangers' bodies, and never one of the colony's own.</summary>
        private static void TakeStrangers(Building_Grave g)
        {
            var filter = g?.GetStoreSettings()?.filter;
            if (filter == null) return;
            var stranger = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesStranger");
            var colonist = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesColonist");
            if (stranger != null && !filter.Allows(stranger)) filter.SetAllow(stranger, true);
            if (colonist != null && filter.Allows(colonist)) filter.SetAllow(colonist, false);
        }

        public static bool OnIt(PlayOut p, Pawn x)
        {
            var j = x?.CurJob;
            if (j == null) return false;
            if (j.def == JobDefOf.HaulToContainer) return j.targetA.Thing != null && p.things.Contains(j.targetA.Thing);
            return j.def == JobDefOf.FinishFrame && j.targetA.Thing is Frame f && p.plots.Contains(f.Position);
        }

        /// <summary>
        /// The next thing for this one to do: a body into a grave that's ready (the nearest body nobody else has), else a
        /// grave to dig for one who can build. False when there's nothing for them just now.
        /// </summary>
        public static bool Give(PlayOut p, PlayHand h)
        {
            var x = h?.pawn;
            var map = p.map;
            if (x == null || !x.Spawned || x.Map != map) return false;
            var busy = p.hands.Where(o => o != h && !o.left && OnIt(p, o.pawn)).Select(o => o.pawn.CurJob).ToList();
            var taken = new HashSet<Thing>(busy.Select(j => j.targetA.Thing).Where(t => t != null));
            var takenGraves = new HashSet<Thing>(busy.Select(j => j.targetB.Thing).Where(t => t != null));
            for (int i = 0; i < p.plots.Count; i++)
            {
                var grave = GraveAt(map, p.plots[i]);
                if (grave == null || grave.HasCorpse || takenGraves.Contains(grave) || !x.CanReserveAndReach(grave, PathEndMode.Touch, Danger.Some)) continue;
                var corpse = p.things.Where(c => PlayHaul.EnemyCorpse(c) && !taken.Contains(c) && x.CanReserveAndReach(c, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderBy(c => c.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (corpse == null) break;
                TakeStrangers(grave);
                var job = HaulAIUtility.HaulToContainerJob(x, corpse, grave);
                if (job == null) continue;
                job.count = 1;
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (x.CurJob != job) return false;
                h.phase = p.things.IndexOf(corpse);
                h.cell = grave.Position;
                return true;
            }
            if (x.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) return false;
            for (int i = 0; i < p.plots.Count; i++)
            {
                var frame = FrameAt(map, p.plots[i]);
                if (frame == null || taken.Contains(frame) || !GenConstruct.CanConstruct(frame, x, true, false) || !x.CanReserveAndReach(frame, PathEndMode.Touch, Danger.Some)) continue;
                var job = JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (x.CurJob != job) return false;
                // A grave dug: counted as theirs once it stands (Settle).
                h.phase = -2 - i;
                h.cell = p.plots[i];
                return true;
            }
            return false;
        }

        /// <summary>Counts what this one did last, if it came to pass: a body laid in a grave, a grave dug.</summary>
        public static void Settle(PlayOut p, PlayHand h)
        {
            if (h == null) return;
            if (h.phase >= 0 && h.phase < p.things.Count && Buried(p, p.things[h.phase])) h.done++;
            else if (h.phase <= -2 && -2 - h.phase < p.plots.Count && GraveAt(p.map, p.plots[-2 - h.phase]) != null) h.done++;
            h.phase = -1;
        }

        /// <summary>
        /// It's over: with nobody left to bury, the frames nobody finished are taken up again; with bodies still lying
        /// out (the party ran out of time), the frames and empty graves stay for the colony to finish and fill.
        /// </summary>
        public static void Done(PlayOut p)
        {
            if (p.things.Any(PlayHaul.EnemyCorpse)) return;
            foreach (var c in p.plots)
            {
                var frame = FrameAt(p.map, c);
                if (frame != null && !frame.Destroyed) frame.Destroy(DestroyMode.Cancel);
            }
        }
    }

    /// <summary>
    /// The enemy dead burned in the colony's crematorium ("crematorium"): any building of the colony's with a bill that
    /// burns a body to nothing (the game's electric crematorium, or a mod's), switched on, powered and fuelled. Each of
    /// the party carries a body there, waits their turn at it, and burns it with the bill's own sound and work; their
    /// gear is left beside it, as at the pyre.
    /// </summary>
    public static class PlayCremation
    {
        /// <summary>The building's bill that burns a body to nothing, or null.</summary>
        public static RecipeDef RecipeOf(ThingDef building)
        {
            var corpse = ThingDefOf.Human?.race?.corpseDef;
            if (building?.AllRecipes == null || corpse == null) return null;
            foreach (var r in building.AllRecipes)
                if (r != null && !r.ingredients.NullOrEmpty() && r.fixedIngredientFilter != null && r.fixedIngredientFilter.Allows(corpse)
                    && r.products.NullOrEmpty() && r.specialProducts.NullOrEmpty() && r.AvailableNow)
                    return r;
            return null;
        }

        /// <summary>A crematorium of the colony's that can burn a body now.</summary>
        public static bool Usable(Thing t)
        {
            if (!(t is Building b) || !b.Spawned || b.Destroyed || b.Faction != Faction.OfPlayer || !b.def.hasInteractionCell) return false;
            if (b.IsForbidden(Faction.OfPlayer) || b.IsBurning()) return false;
            var power = b.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return false;
            var flick = b.GetComp<CompFlickable>();
            if (flick != null && !flick.SwitchIsOn) return false;
            var fuel = b.GetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return false;
            return RecipeOf(b.def) != null;
        }

        /// <summary>The colony's crematorium nearest the middle of the colony that can burn a body now, or null.</summary>
        public static Building Find(Map map)
        {
            if (map == null) return null;
            var center = TemplatePlanner.ColonyCenter(map);
            return map.listerBuildings.allBuildingsColonist.Where(Usable).OrderBy(b => b.Position.DistanceToSquared(center)).FirstOrDefault();
        }

        public static bool OnIt(PlayOut p, Pawn x)
        {
            var j = x?.CurJob;
            return j != null && j.def == PlayOutDefOf.Council_PlayOutCremate && j.targetA.Thing != null && p.things.Contains(j.targetA.Thing);
        }

        /// <summary>The next body for this one to take to the crematorium: the nearest nobody else has.</summary>
        public static bool Give(PlayOut p, PlayHand h)
        {
            var x = h?.pawn;
            var oven = p.place;
            if (x == null || !x.Spawned || x.Map != p.map || !Usable(oven) || !x.CanReach(oven, PathEndMode.InteractionCell, Danger.Some)) return false;
            var taken = new HashSet<Thing>(p.hands.Where(o => o != h && !o.left && OnIt(p, o.pawn)).Select(o => o.pawn.CurJob.targetA.Thing));
            var corpse = p.things.Where(c => PlayHaul.EnemyCorpse(c) && !taken.Contains(c) && x.CanReserveAndReach(c, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(c => c.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (corpse == null) return false;
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutCremate, corpse, oven);
            job.count = 1;
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return x.CurJob == job;
        }

        /// <summary>How long a body takes to burn: the bill's work at their pace.</summary>
        public static int Ticks(RecipeDef recipe, Pawn by)
        {
            float speed = by != null ? Mathf.Max(0.4f, by.GetStatValue(StatDefOf.WorkSpeedGlobal)) : 1f;
            return Mathf.Clamp(Mathf.RoundToInt((recipe?.workAmount ?? 600f) / speed), 300, 3000);
        }

        /// <summary>
        /// The body they carry goes into the fire: set down, its gear left beside it, and burned. The plan hears of it at
        /// once (the body is gone from its list, counted to the one who carried it). False when there was no body.
        /// </summary>
        public static bool Burn(Pawn by)
        {
            if (!(by?.carryTracker?.CarriedThing is Corpse) || !by.Spawned) return false;
            if (!by.carryTracker.TryDropCarriedThing(by.Position, ThingPlaceMode.Near, out var dropped) || !(dropped is Corpse c)) return false;
            try
            {
                if (c.AnythingToStrip()) c.Strip(false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cremation strip", e);
            }
            var play = PlayOutTracker.Instance?.GroupFor(by);
            if (play != null && play.things.Remove(c))
            {
                play.tally++;
                var hand = play.Hand(by);
                if (hand != null) hand.done++;
            }
            if (!c.Destroyed) c.Destroy(DestroyMode.Vanish);
            return true;
        }
    }

    /// <summary>
    /// One body to the crematorium (PlayCremation): the carrier takes it up, walks it to the crematorium, waits their turn
    /// if someone else is at it, and burns it there with the bill's own effect and sound. Danger, drafting or need ends
    /// it, and the body is set down again where they stand.
    /// </summary>
    public class JobDriver_PlayOutCremate : JobDriver
    {
        private bool burned;
        private int waited;

        private Thing Oven => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn));
            this.FailOn(() => !burned && !PlayCremation.Usable(Oven));
            AddFinishAction(cond =>
            {
                try
                {
                    if (!burned && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("cremation end", e);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            // Their turn at the crematorium: one at a time, the rest wait beside it with their burden.
            var turn = ToilMaker.MakeToil("PlayOutCremateTurn");
            turn.defaultCompleteMode = ToilCompleteMode.Never;
            turn.tickIntervalAction = delta =>
            {
                waited += delta;
                if (pawn.CanReserve(job.GetTarget(TargetIndex.B)))
                {
                    pawn.Reserve(job.GetTarget(TargetIndex.B), job);
                    ReadyForNextToil();
                }
                else if (waited > GenDate.TicksPerHour * 2) EndJobWith(JobCondition.Incompletable);
            };
            yield return turn;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
            var recipe = PlayCremation.RecipeOf(Oven?.def);
            var burn = ToilMaker.MakeToil("PlayOutCremateBurn");
            burn.defaultCompleteMode = ToilCompleteMode.Delay;
            burn.defaultDuration = PlayCremation.Ticks(recipe, pawn);
            burn.handlingFacing = true;
            burn.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.GetTarget(TargetIndex.B));
            burn.WithProgressBarToilDelay(TargetIndex.B);
            if (recipe?.effectWorking != null) burn.WithEffect(recipe.effectWorking, TargetIndex.B);
            if (recipe?.soundWorking != null) burn.PlaySustainerOrSound(recipe.soundWorking);
            yield return burn;
            var done = ToilMaker.MakeToil("PlayOutCremated");
            done.initAction = () =>
            {
                try
                {
                    burned = PlayCremation.Burn(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("cremation", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref burned, "burned");
            Scribe_Values.Look(ref waited, "waited");
        }
    }

    /// <summary>
    /// The pyre lit at dusk (OnMapTimed.cs): one of the colony takes wood from the stores (when there is any), carries it
    /// out to the pyre, and lights it. Danger, drafting or need ends it; the wood is set down where they stand, and
    /// someone else is sent.
    /// </summary>
    public class JobDriver_PlayOutLightPyre : JobDriver
    {
        private bool lit;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.GetTarget(TargetIndex.B).HasThing || pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn));
            AddFinishAction(cond =>
            {
                try
                {
                    if (!lit && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pyre lighting end", e);
                }
            });
            if (job.GetTarget(TargetIndex.B).HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            }
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var light = ToilMaker.MakeToil("PlayOutLightPyre");
            light.defaultCompleteMode = ToilCompleteMode.Delay;
            light.defaultDuration = 240;
            light.handlingFacing = true;
            light.tickIntervalAction = delta =>
            {
                var cell = job.GetTarget(TargetIndex.A).Cell;
                pawn.rotationTracker.FaceCell(cell);
                if (Rand.Chance(0.05f * delta)) FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), pawn.Map);
            };
            light.WithProgressBarToilDelay(TargetIndex.A);
            yield return light;
            var done = ToilMaker.MakeToil("PlayOutPyreLit");
            done.initAction = () =>
            {
                try
                {
                    // The wood goes on the pyre.
                    if (pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.DestroyCarriedThing();
                    lit = OnMapTimed.Instance?.LightPyre(pawn.Map, job.GetTarget(TargetIndex.A).Cell, pawn) ?? false;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pyre lit", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lit, "lit");
        }
    }
}
