using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Alpha Skills' drunken passion learns at 2.5 times the speed while its colonist is drunk. Alpha Skills decides that
    /// with a comp on the game's own drunkenness, the AlcoholHigh hediff: the moment that hediff is added, every skill
    /// with the drunken passion turns to its active form (AS_DrunkenPassion_Active), and back when it wears off. So a
    /// colonist with the passion, about to start work that trains that skill, sometimes takes a drink from the
    /// colony's stores first, the game's own ingesting, and then goes on to the work, which waits in their queue.
    ///
    /// Only a drink whose only effects are that hediff and its tolerance, that can't overdose and can't start an
    /// addiction on a first drink; only within their drug policy (for fun, or a scheduled dose that's due) and their
    /// beliefs; never for a teetotaler, a child, someone addicted or just clean, someone pregnant, hungover, already
    /// drinking or needing rest for their health, nor near the tolerance where addiction or harm can start; never in
    /// danger; only at home, from the stores, a short walk away and near the work; never before work a drink could make
    /// dangerous (tending, surgery, hunting, animals). One choice a work stretch, yes most of the time. Without Alpha
    /// Skills nothing is patched.
    /// </summary>
    public static class DrinkBeforeWork
    {
        public const string PackageId = "sarg.alphaskills";
        public const string DrunkenPassion = "AS_DrunkenPassion";
        /// <summary>The comp Alpha Skills puts on AlcoholHigh to wake the passion (read by its class name).</summary>
        public const string DrunkenComp = "HediffCompProperties_DrunkPassions";
        public const string Pool = "Rimfolk_Pool_DrinkBeforeWork";

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.drinkBeforeWork ?? true);

        private struct DrinkInfo
        {
            public float severity;
            public float toleranceGain;
            public ChemicalDef chemical;
            public float toleranceLimit;
        }

        private struct Pending
        {
            public Job drink;
            public SkillDef skill;
        }

        private static bool looked;
        private static bool ready;
        private static Passion drunken;
        private static HediffDef drunk;
        /// <summary>Per stage of drunkenness: nothing worse than slower hands (no sickness, fights, breaks or danger).</summary>
        private static bool[] mildStage;
        /// <summary>Per stage: food poisoning no likelier, so cooking is safe.</summary>
        private static bool[] cookingStage;
        private static readonly Dictionary<ThingDef, DrinkInfo> drinks = new Dictionary<ThingDef, DrinkInfo>();

        /// <summary>When each colonist last chose, yes or no (not saved: a reload lets them choose once more).</summary>
        private static readonly Dictionary<int, int> chose = new Dictionary<int, int>();
        /// <summary>A drink on its way, for the line said as it's raised.</summary>
        private static readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

        /// <summary>Alpha Skills is here, has the passion, and wakes it on the game's drunkenness, and there is a drink for it.</summary>
        public static bool Ready()
        {
            if (looked) return ready;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(PackageId)) return false;
                var type = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef");
                var def = type != null ? GenDefDatabase.GetDefSilentFail(type, DrunkenPassion, false) : null;
                drunk = HediffDefOf.AlcoholHigh;
                if (def == null || drunk?.comps == null || drunk.stages.NullOrEmpty()) return false;
                if (!drunk.comps.Any(c => c != null && c.GetType().Name == DrunkenComp)) return false;
                int value = PassionValue(def);
                if (value < 0) return false;
                drunken = (Passion)value;
                ReadStages();
                ReadDrinks();
                ready = drinks.Count > 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drink before work: reading Alpha Skills", e);
                ready = false;
            }
            return ready;
        }

        /// <summary>
        /// The value a skill's passion field holds for this passion: its place in Vanilla Skills Expanded's list of
        /// passions (as People.PassionLevel reads it), else the def's index, which Alpha Skills writes; -1 if neither.
        /// </summary>
        private static int PassionValue(Def def)
        {
            try
            {
                var field = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager")?.GetField("Passions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (field?.GetValue(null) is Array list)
                    for (int i = 0; i < list.Length; i++)
                        if (list.GetValue(i) is Def d && d.defName == def.defName) return i;
            }
            catch
            {
                // The list can't be read: the def's own index below.
            }
            return def.index == ushort.MaxValue ? -1 : def.index;
        }

        private static void ReadStages()
        {
            int n = drunk.stages.Count;
            mildStage = new bool[n];
            cookingStage = new bool[n];
            var poison = StatDefOf.FoodPoisonChance;
            bool poisonByHands = poison?.capacityFactors != null && poison.capacityFactors.Count > 0;
            for (int i = 0; i < n; i++)
            {
                var s = drunk.stages[i];
                mildStage[i] = s != null && i < DrinkBeforeWorkMath.MildStages && !s.lifeThreatening && s.deathMtbDays <= 0f && s.vomitMtbDays <= 0f
                               && s.mentalBreakMtbDays <= 0f && s.mentalStateGivers.NullOrEmpty() && s.hediffGivers.NullOrEmpty()
                               && s.socialFightChanceFactor <= 1f;
                cookingStage[i] = mildStage[i] && s.foodPoisoningChanceFactor <= 1f && !Touches(s.statOffsets, poison) && !Touches(s.statFactors, poison)
                                  && !(poisonByHands && !s.capMods.NullOrEmpty());
            }
        }

        private static bool Touches(List<StatModifier> mods, StatDef stat) => mods != null && stat != null && mods.Any(m => m?.stat == stat);

        /// <summary>
        /// The drinks that make one drunk the way Alpha Skills reads it: a drug whose ingesting gives AlcoholHigh for
        /// certain, and nothing but that and its tolerance; never one that can overdose or start an addiction on the
        /// first drink, and nothing hard.
        /// </summary>
        private static void ReadDrinks()
        {
            drinks.Clear();
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var ing = d?.ingestible;
                if (ing?.outcomeDoers == null || !d.IsDrug || ing.drugCategory == DrugCategory.Hard) continue;
                var comp = d.GetCompProperties<CompProperties_Drug>();
                if (comp == null || comp.CanCauseOverdose || comp.largeOverdoseChance > 0f) continue;
                if (comp.Addictive && comp.minToleranceToAddict <= 0f) continue;
                var tolerance = comp.chemical?.toleranceHediff;
                float severity = 0f, gain = 0f;
                bool other = false;
                foreach (var o in ing.outcomeDoers)
                {
                    if (!(o is IngestionOutcomeDoer_GiveHediff give)) continue;
                    if (give.hediffDef == drunk && give.chance >= 1f) severity += give.severity;
                    else if (give.hediffDef != null && give.hediffDef == tolerance) gain += give.severity;
                    else other = true;
                }
                if (other || severity <= 0f) continue;
                drinks[d] = new DrinkInfo
                {
                    severity = severity, toleranceGain = gain, chemical = comp.chemical,
                    toleranceLimit = DrinkBeforeWorkMath.ToleranceLimit(comp.Addictive ? comp.minToleranceToAddict : 0f, HarmFrom(tolerance))
                };
            }
        }

        /// <summary>The lowest tolerance at which it starts doing harm (cirrhosis, brain damage), or 0 if never.</summary>
        private static float HarmFrom(HediffDef tolerance)
        {
            float lowest = 0f;
            void Look(List<HediffGiver> givers)
            {
                if (givers == null) return;
                foreach (var g in givers)
                    if (g is HediffGiver_RandomDrugEffect r && (lowest <= 0f || r.minSeverity < lowest)) lowest = Math.Max(0.01f, r.minSeverity);
            }
            if (tolerance == null) return 0f;
            Look(tolerance.hediffGivers);
            if (tolerance.stages != null)
                foreach (var s in tolerance.stages)
                    if (s?.hediffGivers != null && s.hediffGivers.Count > 0) Look(s.hediffGivers);
            return lowest;
        }

        // ---------------- The choice ----------------

        /// <summary>
        /// The drink to have before <paramref name="work"/>, which then waits in their queue; null to go straight to work.
        /// </summary>
        public static Job Before(Pawn p, Job work)
        {
            if (work == null || p == null || !On || !Ready() || work.playerForced) return null;
            var skill = Trains(p, work);
            if (skill == null) return null;
            int now = Find.TickManager.TicksGame;
            int id = p.thingIDNumber;
            if (chose.TryGetValue(id, out int last) && !DrinkBeforeWorkMath.Due(last, now)) return null;
            if (!MayDrink(p)) return null;
            if (chose.Count > 200) chose.Clear();
            chose[id] = now;
            if (!Rand.Chance(DrinkBeforeWorkMath.Chance)) return null;
            var drink = NearestDrink(p, work, skill);
            if (drink == null) return null;
            var job = JobMaker.MakeJob(JobDefOf.Ingest, drink);
            job.count = 1;
            if (pending.Count > 50) pending.Clear();
            pending[id] = new Pending { drink = job, skill = skill };
            return job;
        }

        /// <summary>The skill this work trains in which they have the drunken passion, asleep, and which is safe to do tipsy; or null.</summary>
        private static SkillDef Trains(Pawn p, Job work)
        {
            var skills = p.skills?.skills;
            if (skills == null) return null;
            bool any = false;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].passion == drunken)
                {
                    any = true;
                    break;
                }
            if (!any) return null;
            var driver = work.def?.driverClass;
            bool chat = driver != null && typeof(JobDriver_ChatWithPrisoner).IsAssignableFrom(driver);
            var recipe = work.RecipeDef;
            if (recipe != null) return Fits(p, recipe.workSkill, chat) ? recipe.workSkill : null;
            if (work.bill != null || driver == null || Fetching(driver)) return null;
            var relevant = work.workGiverDef?.workType?.relevantSkills;
            if (relevant == null) return null;
            foreach (var s in relevant)
                if (Fits(p, s, chat)) return s;
            return null;
        }

        private static bool Fits(Pawn p, SkillDef s, bool chat)
        {
            if (s == null) return false;
            var rec = p.skills.GetSkill(s);
            // Cooking is asked again of the drink itself (NearestDrink): does it make food poisoning likelier?
            return rec != null && !rec.TotallyDisabled && rec.passion == drunken && DrinkBeforeWorkMath.SafeSkill(s.defName, true, chat);
        }

        /// <summary>The stage of drunkenness a drink of this strength leaves them in, or -1.</summary>
        private static int StageFor(Pawn p, float severity)
        {
            if (severity <= 0f) return -1;
            int i = drunk.StageAtSeverity(DrinkBeforeWorkMath.Strength(severity, p.BodySize));
            return i >= 0 && i < mildStage.Length ? i : -1;
        }

        /// <summary>Fetching, carrying and refuelling train nothing: no drink before them.</summary>
        private static bool Fetching(Type driver) =>
            typeof(JobDriver_HaulToCell).IsAssignableFrom(driver) || typeof(JobDriver_HaulToContainer).IsAssignableFrom(driver)
            || typeof(JobDriver_Refuel).IsAssignableFrom(driver) || typeof(JobDriver_RefuelAtomic).IsAssignableFrom(driver)
            || typeof(JobDriver_FillFermentingBarrel).IsAssignableFrom(driver) || typeof(JobDriver_TakeBeerOutOfFermentingBarrel).IsAssignableFrom(driver)
            || driver.Name.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Everything about them now allows a drink: who they are, their health, the place and the moment.</summary>
        private static bool MayDrink(Pawn p)
        {
            if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome || !p.IsFreeNonSlaveColonist || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            if (p.drugs == null || p.jobs?.jobQueue == null || p.jobs.jobQueue.Count > 0) return false;
            if (!p.DevelopmentalStage.Adult() || Flavor.HumanAge(p) < DrinkBeforeWorkMath.MinAge) return false;
            if (PawnUtility.IsTeetotaler(p) || AddictionUtility.AddictedToAnything(p) || LifeNewTracker.JustClean(p)) return false;
            var hediffs = p.health?.hediffSet;
            if (hediffs == null || hediffs.BleedRateTotal > 0f || HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            if (Has(hediffs, drunk) || Has(hediffs, HediffDefOf.Hangover) || Has(hediffs, HediffDefOf.DrugOverdose)) return false;
            if (Has(hediffs, HediffDefOf.PregnantHuman) || Has(hediffs, HediffDefOf.Pregnant)) return false;
            if (p.Map.dangerWatcher?.DangerRating == StoryDanger.High || GenAI.EnemyIsNear(p, 40f)) return false;
            return true;
        }

        private static bool Has(HediffSet set, HediffDef def) => def != null && set.GetFirstHediffOfDef(def) != null;

        /// <summary>The nearest drink in the stores that suits them, a short walk away and near the work; or null.</summary>
        private static Thing NearestDrink(Pawn p, Job work, SkillDef skill)
        {
            var map = p.Map;
            IntVec3 at = work.targetA.IsValid ? work.targetA.Cell : IntVec3.Invalid;
            if (at.IsValid && !at.InBounds(map)) at = IntVec3.Invalid;
            bool cooking = skill == SkillDefOf.Cooking;
            var fit = new Dictionary<ThingDef, bool>();
            bool Ok(Thing t)
            {
                if (t?.def == null || !drinks.ContainsKey(t.def) || !t.Spawned || t.IsForbidden(p) || !t.IsInAnyStorage()) return false;
                if (!DrinkBeforeWorkMath.Near(t.Position.DistanceTo(p.Position), at.IsValid ? t.Position.DistanceTo(at) : -1f)) return false;
                if (!fit.TryGetValue(t.def, out bool ok))
                {
                    ok = Suits(p, t.def, drinks[t.def], cooking);
                    fit[t.def] = ok;
                }
                return ok && t.IsSociallyProper(p) && !Beliefs.MindsEating(p, t, false) && p.CanReserve(t);
            }
            return GenClosest.ClosestThingReachable(p.Position, map, ThingRequest.ForGroup(ThingRequestGroup.Drug), PathEndMode.ClosestTouch,
                TraverseParms.For(p, Danger.None), DrinkBeforeWorkMath.MaxDistance, Ok);
        }

        /// <summary>
        /// This kind of drink suits them: their policy allows it for fun or a scheduled dose is due, their beliefs allow
        /// it, it leaves them only warm or tipsy (before cooking: no likelier to poison the food), and their tolerance
        /// stays well under where addiction or harm begins.
        /// </summary>
        private static bool Suits(Pawn p, ThingDef d, DrinkInfo info, bool cooking)
        {
            DrugPolicyEntry entry = null;
            try
            {
                entry = p.drugs.CurrentPolicy?[d];
            }
            catch
            {
                // Not in their policy at all: the policy hasn't allowed it.
            }
            if (entry == null) return false;
            if (!entry.allowedForJoy && !(entry.allowScheduled && p.drugs.ShouldTryToTakeScheduledNow(d))) return false;
            if (Beliefs.FaithForbidsDrug(p, d)) return false;
            if (ModsConfig.IdeologyActive)
            {
                if (!IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedDrug, p)) return false;
                if (d.IsNonMedicalDrug && !IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedRecreationalDrug, p)) return false;
            }
            int stage = StageFor(p, info.severity);
            if (stage < 0 || !mildStage[stage] || (cooking && !cookingStage[stage])) return false;
            float current = info.chemical != null ? AddictionUtility.FindToleranceHediff(p, info.chemical)?.Severity ?? 0f : 0f;
            return DrinkBeforeWorkMath.ToleranceRoom(current, DrinkBeforeWorkMath.Strength(info.toleranceGain, p.BodySize), info.toleranceLimit);
        }

        // ---------------- The cup raised ----------------

        /// <summary>The drink before work is had: now and then a word about it as the cup is raised.</summary>
        public static void Raised(Pawn p, Thing drink)
        {
            if (p == null || drink == null || pending.Count == 0 || !pending.TryGetValue(p.thingIDNumber, out var had)) return;
            if (p.CurJob != had.drink) return;
            pending.Remove(p.thingIDNumber);
            if (!Speech.On || !Rand.Chance(0.5f)) return;
            string line = Flavor.FromPool(Pool, p, null, null, had.skill?.defName.ToLowerInvariant());
            if (!line.NullOrEmpty()) Speech.Say(p, line);
        }
    }

    /// <summary>A drink before work that trains a drunken passion (Integrations/DrinkBeforeWork.cs); the work waits in the queue.</summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_DrinkBeforeWork
    {
        public static bool Prepare() => DrinkBeforeWork.Ready();

        public static void Postfix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            // Only a choice made between jobs: never in an emergency, never in place of work already going on.
            if (!RimfolkPatches.On || __instance == null || __instance.emergency || __result.Job == null || __result.FromQueue) return;
            if (pawn?.jobs == null || pawn.jobs.curJob != null) return;
            try
            {
                var work = __result.Job;
                var drink = DrinkBeforeWork.Before(pawn, work);
                if (drink == null) return;
                pawn.jobs.jobQueue.EnqueueFirst(work, __result.Tag);
                __result = new ThinkResult(drink, __result.SourceNode, __result.Tag, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drink before work", e);
            }
        }
    }

    /// <summary>The cup raised before work (Integrations/DrinkBeforeWork.cs).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_DrinkBeforeWorkRaised
    {
        public static bool Prepare() => DrinkBeforeWork.Ready();

        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null) return;
            try
            {
                DrinkBeforeWork.Raised(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drink before work: the cup raised", e);
            }
        }
    }
}
