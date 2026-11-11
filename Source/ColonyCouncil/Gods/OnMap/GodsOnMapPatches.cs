using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The gods on the map's hooks into the game (Gods/OnMap/). Each looks first whether anything of theirs is about (a god
    // walking, a light guttering), so a game without them pays one check.

    /// <summary>The colony's own struck the stranger or the sacred animal: the god's anger (GodStranger.Struck, SacredAnimal.Struck).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_GodStruck
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo)
        {
            if (!GodAvatar.Any || !RimfolkPatches.On || !GodAvatar.Is(__instance)) return;
            try
            {
                var by = dinfo.Instigator;
                if (by == null || by.Faction != Faction.OfPlayer || dinfo.Def == null || !dinfo.Def.ExternalViolenceFor(__instance)) return;
                // A blow in a bout they agreed to isn't harm (Outsiders/OutsiderPatches.cs).
                if (Outsiders.InBout(__instance, by as Pawn)) return;
                GodStranger.Struck(__instance, by as Pawn);
                SacredAnimal.Struck(__instance, by as Pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god struck", e);
            }
        }
    }

    /// <summary>One of the colony tended the stranger's wounds: a kindness the god remembers.</summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_GodTended
    {
        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (!GodAvatar.Any || !RimfolkPatches.On || !GodAvatar.Is(patient)) return;
            try
            {
                GodStranger.Tended(doctor, patient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god tended", e);
            }
        }
    }

    /// <summary>A light that won't stay lit (the omen, a god's hearth withheld): out while it gutters.</summary>
    [HarmonyPatch(typeof(CompGlower), "ShouldBeLitNow", MethodType.Getter)]
    public static class Patch_GodGutterGlow
    {
        public static void Postfix(CompGlower __instance, ref bool __result)
        {
            if (__result && GodOmens.Out(__instance)) __result = false;
        }
    }

    /// <summary>A torch's or a brazier's flame isn't drawn while it gutters out.</summary>
    [HarmonyPatch(typeof(CompFireOverlay), nameof(CompFireOverlay.PostDraw))]
    public static class Patch_GodGutterFlame
    {
        public static bool Prefix(CompFireOverlay __instance) => !GodOmens.Out(__instance.parent);
    }

    /// <summary>
    /// Right-clicking the sacred animal with a colonist selected: follow it where it leads, or leave it some food (a large
    /// one leads; a small one only takes food).
    /// </summary>
    public class FloatMenuOptionProvider_SacredAnimal : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => false;

        protected override bool AppliesInt(FloatMenuContext context) => GodAvatar.Any && GodsOnMapBook.Instance?.beast != null;

        public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context) => base.TargetPawnValid(pawn, context) && SacredAnimal.Is(pawn);

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            var c = context.FirstSelectedPawn;
            var s = GodsOnMapBook.Instance?.beast;
            if (c == null || s?.a != clickedPawn || !c.IsColonistPlayerControlled || c.DevelopmentalStage.Baby()) yield break;
            var a = clickedPawn;
            string kind = a.def.label;
            if (!GodsOnMapMath.Follows(a.BodySize))
            {
                string why = s.hurt ? "CC_GodM_Menu_Hurt".Translate().Resolve() : s.led ? "CC_GodM_Menu_Led".Translate(kind.Named("KIND")).Resolve()
                    : s.follower != null ? "CC_GodM_Menu_Following".Translate(s.follower.LabelShort.Named("PAWN")).Resolve() : null;
                string label = "CC_GodM_Menu_Follow".Translate(kind.Named("KIND")).Resolve();
                yield return why != null ? new FloatMenuOption(label + " (" + why + ")", null) : new FloatMenuOption(label, () =>
                {
                    if (!SacredAnimal.Lead(s, c)) Messages.Message("CC_GodM_Menu_CantLead".Translate(kind.Named("KIND")).Resolve(), MessageTypeDefOf.RejectInput, false);
                });
            }
            var food = JobDriver_FeedSacredAnimal.FoodFor(c, a);
            string feed = "CC_GodM_Menu_Feed".Translate(kind.Named("KIND")).Resolve();
            if (s.hurt) yield return new FloatMenuOption(feed + " (" + "CC_GodM_Menu_Hurt".Translate().Resolve() + ")", null);
            else if (s.fed) yield return new FloatMenuOption(feed + " (" + "CC_GodM_Menu_Fed".Translate().Resolve() + ")", null);
            else if (food == null) yield return new FloatMenuOption(feed + " (" + "CC_GodM_Menu_NoFood".Translate(kind.Named("KIND")).Resolve() + ")", null);
            else
                yield return new FloatMenuOption(feed, () =>
                {
                    var def = DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_FeedSacredAnimal.JobName);
                    if (def == null) return;
                    var job = JobMaker.MakeJob(def, food, a);
                    job.count = JobDriver_FeedSacredAnimal.CountOf(food, a);
                    c.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }
    }

    /// <summary>Taking food out to the god's beast: picking it up, walking out to it, setting it down before it, and it eats.</summary>
    public class JobDriver_FeedSacredAnimal : JobDriver
    {
        public const string JobName = "Rimfolk_FeedSacredAnimal";

        private Thing Food => job.targetA.Thing;

        private Pawn Beast => job.targetB.Thing as Pawn;

        /// <summary>The nearest food in the colony's stores the beast would eat (plain food first, never a drug), or null.</summary>
        public static Thing FoodFor(Pawn c, Pawn beast)
        {
            if (c?.Map == null || beast == null) return null;
            return c.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && !t.IsForbidden(c) && t.def.IsNutritionGivingIngestible && !t.def.IsDrug && beast.RaceProps.CanEverEat(t)
                            && t.def.ingestible.preferability != FoodPreferability.NeverForNutrition && c.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => (int)t.def.ingestible.preferability >= (int)FoodPreferability.MealSimple ? 1 : 0).ThenBy(t => t.Position.DistanceToSquared(c.Position))
                .FirstOrDefault();
        }

        /// <summary>Enough for a good meal for the beast, never more than the stack.</summary>
        public static int CountOf(Thing food, Pawn beast)
        {
            float per = food.GetStatValue(StatDefOf.Nutrition);
            float want = (beast.needs?.food?.MaxLevel ?? 1f) * 0.6f;
            return Math.Max(1, Math.Min(food.stackCount, per <= 0f ? 1 : (int)Math.Ceiling(want / per)));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, job.count, null, errorOnFailed);

        public override string GetReport() => "CC_GodM_Job_Feed".Translate((Beast?.def.label ?? "?").Named("KIND")).Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Beast == null || !Beast.Spawned || Beast.Dead || !SacredAnimal.Is(Beast));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var set = ToilMaker.MakeToil("SetFoodDown");
            set.initAction = () =>
            {
                var beast = Beast;
                if (!pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var dropped) || dropped == null || beast == null) return;
                try
                {
                    // together: the colonist walked out to the beast and set the food down before it.
                    var eat = JobMaker.MakeJob(JobDefOf.Ingest, dropped);
                    eat.count = dropped.stackCount;
                    beast.jobs.StartJob(eat, JobCondition.InterruptForced);
                    SacredAnimal.FedBy(pawn);
                    pawn.rotationTracker.FaceTarget(beast);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: feeding the sacred animal", e);
                }
            };
            set.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return set;
            var watch = ToilMaker.MakeToil("WatchItEat");
            watch.tickIntervalAction = delta =>
            {
                if (Beast != null) pawn.rotationTracker.FaceTarget(Beast);
            };
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = 240;
            watch.handlingFacing = true;
            yield return watch;
        }
    }
}
