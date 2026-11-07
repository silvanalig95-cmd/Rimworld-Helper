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
    /// One of Alpha Skills' passions that wakes under a drug, and what Rimfolk does about it (BeforeWork): the passion,
    /// Alpha Skills' comp that wakes it on the game's own hediff, the setting, the words said, and how much the high may
    /// slow the mind or the hands. What the game says about that hediff and its drugs is read once into the row.
    /// </summary>
    public sealed class BeforeWorkRow
    {
        /// <summary>For the log: "drink before work".</summary>
        public string name;
        public string passion;
        /// <summary>The comp Alpha Skills puts on the hediff to wake the passion (read by its class name).</summary>
        public string comp;
        /// <summary>The game's own hediff that wakes it (AlcoholHigh, SmokeleafHigh).</summary>
        public string hediff;
        /// <summary>The words now and then as it's had, tagged with the skill.</summary>
        public string pool;
        public Func<CouncilSettings, bool> setting;
        /// <summary>The most the high may slow the mind or the hands (DrinkBeforeWorkMath.Slowing); 1 for no limit.</summary>
        public float maxSlowing = 1f;

        public struct DrugInfo
        {
            public float severity;
            public float toleranceGain;
            public ChemicalDef chemical;
            public float toleranceLimit;
        }

        public struct Pending
        {
            public Job drug;
            public SkillDef skill;
            /// <summary>Before hot work (the stove, the forge): a burn at it now and then (Harm/Mishaps.cs).</summary>
            public bool hot;
        }

        internal bool looked, ready;
        internal Passion asleep;
        internal HediffDef high;
        /// <summary>Per stage of the high: nothing worse than slower hands (no sickness, extra fights, breaks or danger).</summary>
        internal bool[] mildStage;
        /// <summary>Per stage: food poisoning no likelier, so cooking is safe.</summary>
        internal bool[] cookingStage;
        internal readonly Dictionary<ThingDef, DrugInfo> drugs = new Dictionary<ThingDef, DrugInfo>();
        /// <summary>When each colonist last chose, yes or no (not saved: a reload lets them choose once more).</summary>
        internal readonly Dictionary<int, int> chose = new Dictionary<int, int>();
        /// <summary>A drug on its way, for the line said as it's had.</summary>
        internal readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

        public bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings == null || setting == null || setting(ColonyCouncilMod.Settings));
    }

    /// <summary>
    /// Some of Alpha Skills' passions learn much faster while the colonist is under a drug, and Alpha Skills decides that
    /// with a comp on the game's own hediff: the drunken passion on drunkenness (AlcoholHigh, 2.5 times), the stoned one
    /// on a smokeleaf high (SmokeleafHigh, 1.8 times). The moment that hediff is added, every skill with the passion turns
    /// to its active form, and back when it wears off. So a colonist with such a passion, about to start work that trains
    /// that skill, sometimes has one from the colony's stores first (a drink, a joint), the game's own ingesting, and then
    /// goes on to the work, which waits in their queue. Each passion is a row: its passion, hediff, drugs, setting and
    /// words; the rules are the same for every row.
    ///
    /// Only a drug whose only effects are that hediff and its tolerance, that can't overdose and can't start an
    /// addiction on a first dose; only within their drug policy (for fun, or a scheduled dose that's due) and their
    /// beliefs; never for a teetotaler, a child, someone addicted or just clean, someone pregnant, hungover, already
    /// drunk or high, or needing rest for their health, nor near the tolerance where addiction or harm can start; only
    /// a mild stage of the high; never in danger; only at home, from the stores, a short walk away and near the work;
    /// never before work a drug could make dangerous (tending, surgery, hunting, animals). One choice a work stretch,
    /// yes most of the time. Without Alpha Skills nothing is patched.
    /// </summary>
    public static class BeforeWork
    {
        public const string PackageId = "sarg.alphaskills";

        /// <summary>A drink before work for the drunken passion: beer, or another mod's drink that makes one drunk the same way.</summary>
        public static readonly BeforeWorkRow Drink = new BeforeWorkRow
        {
            name = "drink before work", passion = "AS_DrunkenPassion", comp = "HediffCompProperties_DrunkPassions", hediff = "AlcoholHigh",
            pool = "Rimfolk_Pool_DrinkBeforeWork", setting = s => s.drinkBeforeWork
        };

        /// <summary>
        /// A smoke before work for the stoned passion: a smokeleaf joint, or another mod's drug that gives a smokeleaf high
        /// and nothing else. Smokeleaf slows the mind more than a beer, so the high may slow it no more than smokeleaf's own.
        /// </summary>
        public static readonly BeforeWorkRow Smoke = new BeforeWorkRow
        {
            name = "smoke before work", passion = "AS_StonedPassion", comp = "HediffCompProperties_StonedPassions", hediff = "SmokeleafHigh",
            pool = "Rimfolk_Pool_SmokeBeforeWork", setting = s => s.smokeBeforeWork, maxSlowing = DrinkBeforeWorkMath.SmokeMaxSlowing
        };

        public static readonly BeforeWorkRow[] Rows = { Drink, Smoke };

        /// <summary>Any row can be had: Alpha Skills is here with its passion and comp, and there is a drug for it.</summary>
        public static bool AnyReady()
        {
            bool any = false;
            foreach (var r in Rows)
                if (Ready(r))
                    any = true;
            return any;
        }

        /// <summary>Alpha Skills is here, has the row's passion and wakes it on the game's hediff, and there is a drug for it.</summary>
        public static bool Ready(BeforeWorkRow r)
        {
            if (r.looked) return r.ready;
            r.looked = true;
            try
            {
                if (!ModsConfig.IsActive(PackageId)) return false;
                var type = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef");
                var def = type != null ? GenDefDatabase.GetDefSilentFail(type, r.passion, false) : null;
                r.high = DefDatabase<HediffDef>.GetNamedSilentFail(r.hediff);
                if (def == null || r.high?.comps == null || r.high.stages.NullOrEmpty()) return false;
                if (!r.high.comps.Any(c => c != null && c.GetType().Name == r.comp)) return false;
                int value = PassionValue(def);
                if (value < 0) return false;
                r.asleep = (Passion)value;
                ReadStages(r);
                ReadDrugs(r);
                r.ready = r.drugs.Count > 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(r.name + ": reading Alpha Skills", e);
                r.ready = false;
            }
            return r.ready;
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

        /// <summary>
        /// Which stages of the high are mild: among the first two, no vomiting, breaks, danger or harm of their own; a
        /// fight no likelier than after one beer; the mind and hands slowed no more than the row allows. And which of
        /// them leave cooking safe.
        /// </summary>
        private static void ReadStages(BeforeWorkRow r)
        {
            int n = r.high.stages.Count;
            r.mildStage = new bool[n];
            r.cookingStage = new bool[n];
            var poison = StatDefOf.FoodPoisonChance;
            bool poisonByHands = poison?.capacityFactors != null && poison.capacityFactors.Count > 0;
            for (int i = 0; i < n; i++)
            {
                var s = r.high.stages[i];
                r.mildStage[i] = s != null && i < DrinkBeforeWorkMath.MildStages && !s.lifeThreatening && s.deathMtbDays <= 0f && s.vomitMtbDays <= 0f
                                 && s.mentalBreakMtbDays <= 0f && s.mentalStateGivers.NullOrEmpty() && s.hediffGivers.NullOrEmpty()
                                 && DrinkBeforeWorkMath.Calm(s.socialFightChanceFactor) && DrinkBeforeWorkMath.SlowsWithin(Slowing(s), r.maxSlowing);
                r.cookingStage[i] = r.mildStage[i] && s.foodPoisoningChanceFactor <= 1f && !Touches(s.statOffsets, poison) && !Touches(s.statFactors, poison)
                                    && !(poisonByHands && !s.capMods.NullOrEmpty());
            }
        }

        private static bool Touches(List<StatModifier> mods, StatDef stat) => mods != null && stat != null && mods.Any(m => m?.stat == stat);

        /// <summary>How much a stage slows the mind or the hands at most (consciousness, manipulation); a cap by a curve counts as all of it.</summary>
        private static float Slowing(HediffStage s)
        {
            float most = 0f;
            if (s?.capMods == null) return 0f;
            foreach (var m in s.capMods)
            {
                if (m?.capacity == null || (m.capacity != PawnCapacityDefOf.Consciousness && m.capacity != PawnCapacityDefOf.Manipulation)) continue;
                float cap = m.setMaxCurveOverride != null ? 0f : m.setMax;
                most = Math.Max(most, DrinkBeforeWorkMath.Slowing(m.offset, m.postFactor, cap));
            }
            return most;
        }

        /// <summary>
        /// The drugs that give the row's high the way Alpha Skills reads it: one whose ingesting gives that hediff for
        /// certain, and nothing but that and its tolerance; never one that can overdose or start an addiction on the
        /// first dose, and nothing hard.
        /// </summary>
        private static void ReadDrugs(BeforeWorkRow r)
        {
            r.drugs.Clear();
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
                    if (give.hediffDef == r.high && give.chance >= 1f) severity += give.severity;
                    else if (give.hediffDef != null && give.hediffDef == tolerance) gain += give.severity;
                    else other = true;
                }
                if (other || severity <= 0f) continue;
                r.drugs[d] = new BeforeWorkRow.DrugInfo
                {
                    severity = severity, toleranceGain = gain, chemical = comp.chemical,
                    toleranceLimit = DrinkBeforeWorkMath.ToleranceLimit(comp.Addictive ? comp.minToleranceToAddict : 0f, HarmFrom(tolerance))
                };
            }
        }

        /// <summary>
        /// The lowest tolerance at which it starts doing harm (cirrhosis, carcinoma, asthma, brain damage), or 0 if never.
        /// The game's own effects say it with a curve (nothing at or below its first point), older ones with minSeverity.
        /// </summary>
        private static float HarmFrom(HediffDef tolerance)
        {
            float lowest = 0f;
            void Look(List<HediffGiver> givers)
            {
                if (givers == null) return;
                foreach (var g in givers)
                {
                    if (!(g is HediffGiver_RandomDrugEffect e)) continue;
                    var curve = e.severityToMtbDaysCurve;
                    bool byCurve = curve != null && curve.PointsCount > 0;
                    float start = 0f;
                    if (byCurve)
                    {
                        start = float.MaxValue;
                        foreach (var pt in curve.Points) start = Math.Min(start, pt.x);
                    }
                    float from = Math.Max(0.01f, DrinkBeforeWorkMath.HarmStarts(e.minSeverity, byCurve, start));
                    if (lowest <= 0f || from < lowest) lowest = from;
                }
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
        /// The drink or smoke to have before <paramref name="work"/>, which then waits in their queue; null to go straight
        /// to work. The first row whose passion the work trains decides.
        /// </summary>
        public static Job Before(Pawn p, Job work)
        {
            if (work == null || p == null || work.playerForced) return null;
            foreach (var r in Rows)
            {
                if (!r.On || !Ready(r)) continue;
                // The setting "A drink before hot work" off: the stove and the forge are left alone (Harm/Mishaps.cs).
                if (r == Drink && HotBench.Barred(work)) continue;
                var skill = Trains(r, p, work);
                if (skill == null) continue;
                return Before(r, p, work, skill);
            }
            return null;
        }

        private static Job Before(BeforeWorkRow r, Pawn p, Job work, SkillDef skill)
        {
            int now = Find.TickManager.TicksGame;
            int id = p.thingIDNumber;
            if (r.chose.TryGetValue(id, out int last) && !DrinkBeforeWorkMath.Due(last, now)) return null;
            if (!MayHave(r, p)) return null;
            if (r.chose.Count > 200) r.chose.Clear();
            r.chose[id] = now;
            if (!Rand.Chance(DrinkBeforeWorkMath.Chance)) return null;
            var drug = Nearest(r, p, work, skill);
            if (drug == null) return null;
            var job = JobMaker.MakeJob(JobDefOf.Ingest, drug);
            job.count = 1;
            if (r.pending.Count > 50) r.pending.Clear();
            r.pending[id] = new BeforeWorkRow.Pending { drug = job, skill = skill, hot = r == Drink && HotBench.Hot(work) };
            return job;
        }

        /// <summary>The skill this work trains in which they have the row's passion, asleep, and which is safe to do under it; or null.</summary>
        private static SkillDef Trains(BeforeWorkRow r, Pawn p, Job work)
        {
            var skills = p.skills?.skills;
            if (skills == null) return null;
            bool any = false;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].passion == r.asleep)
                {
                    any = true;
                    break;
                }
            if (!any) return null;
            var driver = work.def?.driverClass;
            bool chat = driver != null && typeof(JobDriver_ChatWithPrisoner).IsAssignableFrom(driver);
            var recipe = work.RecipeDef;
            if (recipe != null) return Fits(r, p, recipe.workSkill, chat) ? recipe.workSkill : null;
            if (work.bill != null || driver == null || Fetching(driver)) return null;
            var relevant = work.workGiverDef?.workType?.relevantSkills;
            if (relevant == null) return null;
            foreach (var s in relevant)
                if (Fits(r, p, s, chat)) return s;
            return null;
        }

        private static bool Fits(BeforeWorkRow r, Pawn p, SkillDef s, bool chat)
        {
            if (s == null) return false;
            var rec = p.skills.GetSkill(s);
            // Cooking is asked again of the drug itself (Nearest): does its high make food poisoning likelier?
            return rec != null && !rec.TotallyDisabled && rec.passion == r.asleep && DrinkBeforeWorkMath.SafeSkill(s.defName, true, chat);
        }

        /// <summary>The stage of the high a dose of this strength leaves them in, or -1.</summary>
        private static int StageFor(BeforeWorkRow r, Pawn p, float severity)
        {
            if (severity <= 0f) return -1;
            int i = r.high.StageAtSeverity(DrinkBeforeWorkMath.Strength(severity, p.BodySize));
            return i >= 0 && i < r.mildStage.Length ? i : -1;
        }

        /// <summary>Fetching, carrying and refuelling train nothing: nothing before them.</summary>
        private static bool Fetching(Type driver) =>
            typeof(JobDriver_HaulToCell).IsAssignableFrom(driver) || typeof(JobDriver_HaulToContainer).IsAssignableFrom(driver)
            || typeof(JobDriver_Refuel).IsAssignableFrom(driver) || typeof(JobDriver_RefuelAtomic).IsAssignableFrom(driver)
            || typeof(JobDriver_FillFermentingBarrel).IsAssignableFrom(driver) || typeof(JobDriver_TakeBeerOutOfFermentingBarrel).IsAssignableFrom(driver)
            || driver.Name.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Everything about them now allows a dose: who they are, their health, the place and the moment. Never on top of
        /// another row's high (no drink while stoned, no smoke while drunk).
        /// </summary>
        private static bool MayHave(BeforeWorkRow r, Pawn p)
        {
            if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome || !p.IsFreeNonSlaveColonist || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            if (p.drugs == null || p.jobs?.jobQueue == null || p.jobs.jobQueue.Count > 0) return false;
            if (!p.DevelopmentalStage.Adult() || Flavor.HumanAge(p) < DrinkBeforeWorkMath.MinAge) return false;
            if (PawnUtility.IsTeetotaler(p) || AddictionUtility.AddictedToAnything(p) || LifeNewTracker.JustClean(p)) return false;
            var hediffs = p.health?.hediffSet;
            if (hediffs == null || hediffs.BleedRateTotal > 0f || HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            foreach (var o in Rows)
                if (Has(hediffs, o.high ?? DefDatabase<HediffDef>.GetNamedSilentFail(o.hediff)))
                    return false;
            if (Has(hediffs, HediffDefOf.Hangover) || Has(hediffs, HediffDefOf.DrugOverdose)) return false;
            if (Has(hediffs, HediffDefOf.PregnantHuman) || Has(hediffs, HediffDefOf.Pregnant)) return false;
            if (p.Map.dangerWatcher?.DangerRating == StoryDanger.High || GenAI.EnemyIsNear(p, 40f)) return false;
            return true;
        }

        private static bool Has(HediffSet set, HediffDef def) => def != null && set.GetFirstHediffOfDef(def) != null;

        /// <summary>The nearest of the row's drugs in the stores that suits them, a short walk away and near the work; or null.</summary>
        private static Thing Nearest(BeforeWorkRow r, Pawn p, Job work, SkillDef skill)
        {
            var map = p.Map;
            IntVec3 at = work.targetA.IsValid ? work.targetA.Cell : IntVec3.Invalid;
            if (at.IsValid && !at.InBounds(map)) at = IntVec3.Invalid;
            bool cooking = skill == SkillDefOf.Cooking;
            var fit = new Dictionary<ThingDef, bool>();
            bool Ok(Thing t)
            {
                if (t?.def == null || !r.drugs.ContainsKey(t.def) || !t.Spawned || t.IsForbidden(p) || !t.IsInAnyStorage()) return false;
                if (!DrinkBeforeWorkMath.Near(t.Position.DistanceTo(p.Position), at.IsValid ? t.Position.DistanceTo(at) : -1f)) return false;
                if (!fit.TryGetValue(t.def, out bool ok))
                {
                    ok = Suits(r, p, t.def, r.drugs[t.def], cooking);
                    fit[t.def] = ok;
                }
                return ok && t.IsSociallyProper(p) && !Beliefs.MindsEating(p, t, false) && p.CanReserve(t);
            }
            return GenClosest.ClosestThingReachable(p.Position, map, ThingRequest.ForGroup(ThingRequestGroup.Drug), PathEndMode.ClosestTouch,
                TraverseParms.For(p, Danger.None), DrinkBeforeWorkMath.MaxDistance, Ok);
        }

        /// <summary>
        /// This kind of drug suits them: their policy allows it for fun or a scheduled dose is due, their beliefs allow
        /// it, it leaves them in a mild stage of the high (before cooking: no likelier to poison the food), and their
        /// tolerance stays well under where addiction or harm begins.
        /// </summary>
        private static bool Suits(BeforeWorkRow r, Pawn p, ThingDef d, BeforeWorkRow.DrugInfo info, bool cooking)
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
            int stage = StageFor(r, p, info.severity);
            if (stage < 0 || !r.mildStage[stage] || (cooking && !r.cookingStage[stage])) return false;
            float current = info.chemical != null ? AddictionUtility.FindToleranceHediff(p, info.chemical)?.Severity ?? 0f : 0f;
            return DrinkBeforeWorkMath.ToleranceRoom(current, DrinkBeforeWorkMath.Strength(info.toleranceGain, p.BodySize), info.toleranceLimit);
        }

        // ---------------- The cup raised, the joint lit ----------------

        /// <summary>The drink or the smoke before work is had: now and then a word about it.</summary>
        public static void Had(Pawn p, Thing drug)
        {
            if (p == null || drug == null) return;
            foreach (var r in Rows)
            {
                if (r.pending.Count == 0 || !r.pending.TryGetValue(p.thingIDNumber, out var had)) continue;
                if (p.CurJob != had.drug) continue;
                r.pending.Remove(p.thingIDNumber);
                if (had.hot) Mishaps.DrankBeforeHotWork(p);
                if (!Speech.On || !Rand.Chance(0.5f)) return;
                string line = Flavor.FromPool(r.pool, p, null, null, had.skill?.defName.ToLowerInvariant());
                if (!line.NullOrEmpty()) Speech.Say(p, line);
                return;
            }
        }
    }

    /// <summary>A drink or a smoke before work that trains a passion it wakes (Integrations/BeforeWork.cs); the work waits in the queue.</summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_BeforeWork
    {
        public static bool Prepare() => BeforeWork.AnyReady();

        public static void Postfix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            // Only a choice made between jobs: never in an emergency, never in place of work already going on.
            if (!RimfolkPatches.On || __instance == null || __instance.emergency || __result.Job == null || __result.FromQueue) return;
            if (pawn?.jobs == null || pawn.jobs.curJob != null) return;
            try
            {
                var work = __result.Job;
                var drug = BeforeWork.Before(pawn, work);
                if (drug == null) return;
                pawn.jobs.jobQueue.EnqueueFirst(work, __result.Tag);
                __result = new ThinkResult(drug, __result.SourceNode, __result.Tag, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drink or smoke before work", e);
            }
        }
    }

    /// <summary>The cup raised or the joint lit before work (Integrations/BeforeWork.cs).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_BeforeWorkHad
    {
        public static bool Prepare() => BeforeWork.AnyReady();

        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null) return;
            try
            {
                BeforeWork.Had(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drink or smoke before work: had", e);
            }
        }
    }
}
