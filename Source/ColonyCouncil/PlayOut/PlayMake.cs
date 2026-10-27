using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Making a thing an answer says someone made (<see cref="DilemmaEffect_Made"/>), before it's carried over: its maker
    /// works at the colony's bench for it (a sculptor's table for a carving, a smithy for a knife, a crafting bench or
    /// spot for a stool), at the camp's fire if they're of the camp, else at the table, with the game's own working
    /// effect and sound, for an hour or two of the day. The thing is made there when they're done (its quality from
    /// their skill then) and waits beside them for the hand-over. A maker who never gets to it: nothing is made.
    /// </summary>
    public static class PlayMake
    {
        /// <summary>A hand-over's phase while its thing is still to be made.</summary>
        public const int Making = -10;

        /// <summary>Who makes it: the one who carries it over, or the visitor it's collected from.</summary>
        public static Pawn Maker(PlayOut p) => p.mode == PlayGive.Fetch ? p.other : p.visitor;

        public static bool IsMaking(PlayOut p) => p != null && p.kind == PlayKind.Give && p.phase == Making;

        /// <summary>The hand-over whose thing this pawn is making.</summary>
        public static PlayOut PlayFor(Pawn pawn)
        {
            var t = PlayOutTracker.Instance;
            if (t == null || pawn == null) return null;
            foreach (var p in t.plays)
                if (p.state == PlayState.Pending && IsMaking(p) && Maker(p) == pawn)
                    return p;
            return null;
        }

        /// <summary>The words for what's being made, before there is one.</summary>
        public static string Label(PlayOut p) => PlayGive.ThingOf(p) is Thing t ? PlayGive.Label(t) : p?.thing?.label ?? "";

        /// <summary>
        /// On the hand-over's clock while it waits: sets its maker to work in its hours. True while the thing is still to
        /// be made (the hand-over can't start).
        /// </summary>
        public static bool Waits(PlayOutTracker t, PlayOut p, int now)
        {
            if (!IsMaking(p)) return false;
            var mk = Maker(p);
            if (mk == null || mk.Dead || p.thing == null)
            {
                t.Fallback(p, null, true);
                return true;
            }
            if (OnIt(mk))
            {
                p.waitReason = "CC_Play_Why_Making".Translate(mk.Named("PAWN")).Resolve();
                return true;
            }
            if (!PlayOutMath.InHours(GenLocalDate.HourOfDay(p.map), p.hourFrom, p.hourTo)) return true;
            string why = PlayOuts.WhyNotNow(p.map, false);
            if (why != null)
            {
                p.waitReason = why;
                return true;
            }
            if (!mk.Spawned || mk.Map != p.map)
            {
                p.waitReason = "CC_Play_Why_Away".Translate(mk.Named("OTHER")).Resolve();
                return true;
            }
            if (!Free(t, p, mk))
            {
                p.waitReason = "CC_Play_Why_NotFree".Translate(mk.Named("PAWN")).Resolve();
                return true;
            }
            if (!Start(p, mk)) p.waitReason = "CC_Play_Why_Place".Translate();
            return true;
        }

        private static bool OnIt(Pawn mk) => mk.CurJobDef == PlayOutDefOf.Council_PlayOutMake;

        /// <summary>Free to sit down to it: one of ours as for anything an answer sets going; a visitor awake, calm and not leaving.</summary>
        private static bool Free(PlayOutTracker t, PlayOut p, Pawn mk)
        {
            if (PlayOuts.OurPerson(mk)) return PlayGroups.Free(t, p, mk);
            if (mk.Downed || mk.InMentalState || !mk.Awake() || mk.InBed() || mk.HostileTo(Faction.OfPlayer) || mk.IsPrisoner) return false;
            if (mk.CurJobDef == JobDefOf.Ingest || mk.CurJobDef == PlayOutDefOf.Council_PlayOutSpeak) return false;
            if (mk.needs?.food != null && mk.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) return false;
            var duty = mk.mindState?.duty?.def;
            return duty == null || !duty.defName.StartsWith("ExitMap");
        }

        private static bool Start(PlayOut p, Pawn mk)
        {
            var at = Place(p, mk);
            if (!at.IsValid) return false;
            // What it's made from, if it's a thing of its own (the fallen star): fetched to the bench first.
            var from = Ingredient(p);
            if (from != null && !mk.CanReserveAndReach(from, PathEndMode.ClosestTouch, Danger.Some)) return false;
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutMake, at, from != null ? (LocalTargetInfo)from : LocalTargetInfo.Invalid);
            job.expiryInterval = -1;
            mk.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return OnIt(mk);
        }

        /// <summary>The thing it's made from, still lying where it was, or null.</summary>
        public static Thing Ingredient(PlayOut p) => p.place != null && p.place.Spawned && p.place.Map == p.map && p.place.def.EverHaulable ? p.place : null;

        /// <summary>
        /// Sets a thing to be made from <paramref name="from"/> by <paramref name="maker"/> at the bench for it, kept there
        /// (nothing carried over): <paramref name="quality"/> as the answer has it. Added to <paramref name="plays"/>, so
        /// the answer's outcome waits for it. Null when it can't be.
        /// </summary>
        public static PlayOut KeepMade(Map map, Pawn maker, ThingDef def, string stuff, Thing from, int quality, int count, string label, SkillDef skill, List<PlayOut> plays, ThingDef at = null)
        {
            var t = PlayOutTracker.Instance;
            if (t == null || map == null || maker == null || def == null || !PlayOuts.On || !PlayOuts.OurPerson(maker)) return null;
            int now = Find.TickManager.TicksGame;
            var p = new PlayOut
            {
                kind = PlayKind.Give, map = map, created = now, hourFrom = 8, hourTo = 20, durationTicks = PlayOutMath.TicksPerHour / 2,
                label = label, visitor = maker, organizer = maker, invited = new List<Pawn> { maker }, mode = PlayGive.Keep,
                named = new List<Pawn> { maker }, count = 1, things = new List<Thing>(), thing = def, what = stuff, skill = skill,
                place = from, phase = Making, makeTicks = PlayOutMath.DurationTicks(2f), makeQuality = quality, amount = Math.Max(1, count), makeAt = at
            };
            p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), p.hourFrom, p.hourTo, 12f);
            t.Add(p);
            plays?.Add(p);
            return p;
        }

        /// <summary>The recipe the game makes this thing with, for its bench and its working effect.</summary>
        public static RecipeDef RecipeFor(ThingDef def)
        {
            if (def == null) return null;
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                if (r.ProducedThingDef == def)
                    return r;
            return null;
        }

        /// <summary>Where they work: the camp's fire for one of the camp, else the colony's bench for it, else the table.</summary>
        private static LocalTargetInfo Place(PlayOut p, Pawn mk)
        {
            var map = p.map;
            if (CampTracker.Instance?.camp is Camp camp && camp.Has(mk))
            {
                var c = PlaySpots.CampCell(map, mk);
                if (c.IsValid) return c;
            }
            var recipe = RecipeFor(p.makeAt ?? p.thing);
            if (recipe != null)
            {
                Thing best = null;
                float bestDist = float.MaxValue;
                foreach (var user in recipe.AllRecipeUsers)
                    foreach (var b in map.listerThings.ThingsOfDef(user))
                    {
                        if (b.Faction != Faction.OfPlayer || b.IsBurning() || (PlayOuts.OurPerson(mk) && b.IsForbidden(mk))) continue;
                        if (b is IBillGiver g && !g.CurrentlyUsableForBills()) continue;
                        var end = b.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch;
                        if (!mk.CanReserve(b) || !mk.CanReach(b, end, Danger.Some)) continue;
                        float d = b.Position.DistanceToSquared(mk.Position);
                        if (d < bestDist)
                        {
                            best = b;
                            bestDist = d;
                        }
                    }
                if (best != null) return best;
            }
            foreach (var s in new[] { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather })
            {
                var c = PlaySpots.FocusCell(p, s, mk, out _);
                if (c.IsValid) return c;
            }
            var open = PlaySpots.OpenCell(map, mk);
            return open.IsValid ? open : LocalTargetInfo.Invalid;
        }

        /// <summary>The work is done: the thing is made beside its maker, kept for the hand-over. False if it couldn't be.</summary>
        public static bool Made(PlayOut p, Pawn mk)
        {
            if (!IsMaking(p) || mk == null || !mk.Spawned) return false;
            var thing = DilemmaEffect_Made.Make(p.thing, p.what, p.mode == PlayGive.Keep ? QualityCategory.Normal : (QualityCategory)p.amount, mk, p.skill ?? SkillDefOf.Artistic);
            if (thing == null) return false;
            // Kept: as many as the answer says (what's left of cracked star iron).
            if (p.mode == PlayGive.Keep && p.amount > 1 && thing.def.stackLimit > 1) thing.stackCount = Math.Min(p.amount, thing.def.stackLimit);
            if (p.makeQuality >= 0) thing.TryGetComp<CompQuality>()?.SetQuality((QualityCategory)p.makeQuality, ArtGenerationContext.Colony);
            // What it was made from is used up.
            if (p.place != null && p.place.Spawned && p.mode == PlayGive.Keep) p.place.Destroy();
            if (p.thing.Minifiable && !(thing is MinifiedThing)) thing = thing.MakeMinified();
            if (!GenPlace.TryPlaceThing(thing, mk.Position, mk.Map, ThingPlaceMode.Near, out var lying))
            {
                if (!thing.Destroyed) thing.Destroy();
                return false;
            }
            thing = lying ?? thing;
            if (p.mode == PlayGive.Keep)
            {
                // Nothing to carry over: it lies at the bench, and that's the end of it.
                p.things = new List<Thing> { thing };
                p.phase = 0;
                var t = PlayOutTracker.Instance;
                string done = p.outcome.NullOrEmpty() ? "CC_Made_Done".Translate(mk.Named("MAKER"), PlayGive.Label(thing).Named("THING")).Resolve() : p.outcome;
                t?.Done(p, new List<Pawn> { mk }, false);
                News.Post(done, new LookTargets(thing), NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                return true;
            }
            // Kept for the one who carries it over (nobody else takes it meanwhile).
            thing.SetForbidden(true, false);
            p.things = new List<Thing> { thing };
            p.phase = 0;
            p.tally = 0;
            p.waitReason = null;
            Messages.Message("CC_Made_Done".Translate(mk.Named("MAKER"), PlayGive.Label(thing).Named("THING")).Resolve(), new LookTargets(thing), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }

    /// <summary>
    /// Making a thing for a hand-over: to the bench (or the camp's fire, or the table), then work there with the recipe's
    /// own effect and sound until it's done, the time kept on the plan so a break picks up where it left off.
    /// </summary>
    public class JobDriver_PlayOutMake : JobDriver
    {
        private bool made;

        private PlayOut Play => PlayMake.PlayFor(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            (!job.targetA.HasThing || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
            && (!job.targetB.HasThing || pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed));

        public override string GetReport()
        {
            var p = Play;
            return p == null ? base.GetReport() : "CC_Play_Report_Make".Translate(PlayMake.Label(p)).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !made && Play == null);
            this.FailOn(() => PlayOuts.OurPerson(pawn) && !PlayOuts.CanJoin(pawn));
            AddFinishAction(_ =>
            {
                // Stopped on the way with what it's made from: set down where they are, still kept for it.
                if (!made && pawn.carryTracker?.CarriedThing is Thing held && pawn.Spawned && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var down))
                    down?.SetForbidden(true, false);
            });
            // What it's made from first, if it lies somewhere of its own (the fallen star), carried to the bench.
            if (job.targetB.HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            }
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, job.targetA.Thing.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            if (job.targetB.HasThing)
            {
                var down = ToilMaker.MakeToil("PlayOutMakeSetDown");
                down.initAction = () =>
                {
                    if (pawn.carryTracker?.CarriedThing != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var lying))
                    {
                        lying?.SetForbidden(true, false);
                        if (Play is PlayOut p && lying != null) p.place = lying;
                    }
                };
                down.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return down;
            }
            var recipe = PlayMake.RecipeFor(Play?.makeAt ?? Play?.thing);
            var work = ToilMaker.MakeToil("PlayOutMake");
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.handlingFacing = true;
            work.tickIntervalAction = delta =>
            {
                var p = Play;
                if (p == null) return;
                if (job.targetA.HasThing) pawn.rotationTracker.FaceTarget(job.targetA);
                p.tally += delta;
                if (p.tally >= p.makeTicks) ReadyForNextToil();
            };
            work.WithProgressBar(TargetIndex.A, () => Play is PlayOut p && p.makeTicks > 0 ? Math.Min(1f, (float)p.tally / p.makeTicks) : 0f);
            if (recipe?.effectWorking != null) work.WithEffect(recipe.effectWorking, TargetIndex.A);
            if (recipe?.soundWorking != null) work.PlaySustainerOrSound(recipe.soundWorking);
            yield return work;
            var done = ToilMaker.MakeToil("PlayOutMade");
            done.initAction = () =>
            {
                try
                {
                    made = PlayMake.Made(Play, pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("made for hand-over", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref made, "made");
        }
    }
}
