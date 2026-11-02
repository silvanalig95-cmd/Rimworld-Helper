using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The council's questions from the colony's own life (Defs/DilemmaDefs/Rimfolk_Dilemmas_New.xml): who gets the new
    // leg, a bad batch from the kitchen, a precept broken, the doctor who is the patient, the heat wave's hours, a
    // project nearly done, lessons from a prisoner, the drug bench, a slave's freedom. Each comes only when the colony is
    // as its letter says, and every answer's words happen in the game: an operation put on the medical tab, work handed
    // over, timetables shifted, bills suspended, the game's own emancipation.

    // ===================================== Who gets the new leg =====================================

    /// <summary>
    /// A replacement limb or eye (a prosthetic or bionic part, {THING}) lies in the stores, and two colonists are missing
    /// a part it fits, and neither's beliefs rule it out. A is the one whose work needs it more ({SKILL}: their best skill
    /// for that kind of part), B the other. Never while either already has an operation for it on their bills.
    /// </summary>
    public class DilemmaWorker_NewLimb : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.IsSlave && !p.Dead).ToList();
            if (pawns.Count < def.minColonists) return false;
            var parts = map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value > 0 && kv.Key.isTechHediff).Select(kv => kv.Key).InRandomOrder().ToList();
            foreach (var part in parts)
            {
                var needing = pawns.Where(p => !Beliefs.Forbids(p, Belief.NoProsthetics) && DilemmaEffect_Operation.Find(p, part, out _, out _)).ToList();
                if (needing.Count < 2) continue;
                var two = needing.InRandomOrder().Take(2).ToList();
                DilemmaEffect_Operation.Find(two[0], part, out _, out var bodyPart);
                string kind = AnswerShiftMath.PartKind(bodyPart?.def?.defName);
                Pawn a = two[0], b = two[1];
                if (!AnswerShiftMath.FirstNeedsMore(Levels(a), Levels(b), kind)) (a, b) = (b, a);
                AnswerShiftMath.Need(Levels(a), kind, out string skill);
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, thing = part, count = 1, skill = skill == null ? null : DefDatabase<SkillDef>.GetNamedSilentFail(skill) };
                return true;
            }
            return false;
        }

        private static Dictionary<string, int> Levels(Pawn p)
        {
            var d = new Dictionary<string, int>();
            if (p?.skills == null) return d;
            foreach (var s in p.skills.skills)
                if (s?.def != null && !s.TotallyDisabled) d[s.def.defName] = s.Level;
            return d;
        }

        /// <summary>"part:leg", "part:arm", "part:eye": what the part is for, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a == null || ctx.thing == null) return;
            if (DilemmaEffect_Operation.Find(ctx.a, ctx.thing, out _, out var part)) tags.Add("part:" + AnswerShiftMath.PartKind(part?.def?.defName));
        }
    }

    /// <summary>
    /// The dilemma's part ({THING}) fitted to the target (A unless given): the game's own operation to install it on the
    /// part they're missing is put on their medical tab, and a doctor does it in the game's own time with the part from
    /// the stores. Can't be chosen once there's no such part in the stores, or nothing left that it fits.
    /// </summary>
    public class DilemmaEffect_Operation : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The game's operation that fits <paramref name="part"/> to a part <paramref name="p"/> is missing, if there is one and it isn't on their bills already.</summary>
        public static bool Find(Pawn p, ThingDef part, out RecipeDef recipe, out BodyPartRecord where)
        {
            recipe = null;
            where = null;
            if (p?.health?.hediffSet == null || part == null || p.BillStack == null) return false;
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (r == null || !r.IsSurgery || r.addsHediff == null || !(r.Worker is Recipe_InstallArtificialBodyPart) || !r.IsIngredient(part)) continue;
                if (!r.AvailableNow || p.BillStack.Bills.Any(b => b.recipe == r)) continue;
                BodyPartRecord missing = null;
                try
                {
                    missing = r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault(x => x != null && p.health.hediffSet.PartIsMissing(x));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("new limb parts", e);
                }
                if (missing == null) continue;
                recipe = r;
                where = missing;
                return true;
            }
            return false;
        }

        private Pawn Who(DilemmaContext ctx) => target == DilemmaTarget.B ? ctx?.b : ctx?.a;

        public override string Unavailable(DilemmaContext ctx)
        {
            var p = Who(ctx);
            if (ctx?.thing == null || p == null) return null;
            if (ctx.map != null && ctx.map.resourceCounter.GetCount(ctx.thing) <= 0) return "CC_Dilemma_NotEnough".Translate(1, ctx.thing.label).ToString();
            return Find(p, ctx.thing, out _, out _) ? null : "CC_DlmV_NoPartFor".Translate(p.LabelShort, ctx.thing.label).Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = Who(ctx);
            if (p == null || ctx.thing == null || !Find(p, ctx.thing, out _, out var part)) return;
            sheet.Add(ImpactKind.Health, 1, "CC_Impact_Operation".Translate(p.LabelShort, ctx.thing.label, part.Label).Resolve(),
                "CC_Impact_OperationBrief".Translate(p.LabelShort, ctx.thing.label).Resolve(), "operation|" + p.thingIDNumber);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Who(ctx);
            if (p == null || p.Dead || ctx.thing == null || !Find(p, ctx.thing, out var recipe, out var part)) return;
            try
            {
                var bill = HealthCardUtility.CreateSurgeryBill(p, recipe, part, null, false);
                if (bill != null)
                    Messages.Message("CC_DlmV_OperationAdded".Translate(p.LabelShort, recipe.LabelCap, part.Label).Resolve(), p, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("new limb operation", e);
            }
        }
    }

    // ===================================== A bad batch =====================================

    /// <summary>
    /// What made the colony's people sick from their food lately, by map: the game's own reason for each case of food
    /// poisoning (a careless cook, a filthy kitchen, food gone off, food that's risky to eat), from the moment it's
    /// given. Not saved: after a load the cases already there are counted without their reasons.
    /// </summary>
    public static class BadBatch
    {
        private static readonly Dictionary<int, List<(int tick, FoodPoisonCause cause)>> causes = new Dictionary<int, List<(int, FoodPoisonCause)>>();

        public static void Note(Map map, FoodPoisonCause cause)
        {
            if (map == null) return;
            int now = Find.TickManager.TicksGame;
            if (!causes.TryGetValue(map.uniqueID, out var list)) causes[map.uniqueID] = list = new List<(int, FoodPoisonCause)>();
            list.Add((now, cause));
            list.RemoveAll(c => now - c.tick > GenDate.TicksPerDay * 2);
        }

        /// <summary>The reason most of the last day's cases had, or Unknown.</summary>
        public static FoodPoisonCause Cause(Map map)
        {
            if (map == null || !causes.TryGetValue(map.uniqueID, out var list)) return FoodPoisonCause.Unknown;
            int now = Find.TickManager.TicksGame;
            return list.Where(c => now - c.tick <= GenDate.TicksPerDay && c.cause != FoodPoisonCause.Unknown)
                .GroupBy(c => c.cause).OrderByDescending(g => g.Count()).Select(g => g.Key).DefaultIfEmpty(FoodPoisonCause.Unknown).First();
        }

        /// <summary>The colony's people who came down with food poisoning in the last day.</summary>
        public static List<Pawn> Sick(Map map) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning) is Hediff h && h.ageTicks < GenDate.TicksPerDay).ToList()
            ?? new List<Pawn>();

        public static WorkTypeDef Cooking => DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
    }

    /// <summary>Notes why someone got food poisoning (the game's own reason), for the bad batch's letter.</summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.AddFoodPoisoningHediff))]
    public static class Patch_FoodPoisonCause
    {
        public static void Postfix(Pawn pawn, FoodPoisonCause cause)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                if (pawn != null && pawn.IsColonist) BadBatch.Note(pawn.MapHeld, cause);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("food poisoning cause", e);
            }
        }
    }

    /// <summary>
    /// Two or more of the colony have come down with the game's food poisoning in the last day ({COUNT}). A is the
    /// colony's cook (first in line for the cooking, the best at it among those), B the next best cook, who would take
    /// it over. Only with a stove of the colony's to cook at. Tags: "cause:cook", "cause:filth", "cause:rotten",
    /// "cause:food", from the game's own reasons.
    /// </summary>
    public class DilemmaWorker_BadBatch : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var cooking = BadBatch.Cooking;
            if (cooking == null || !CanHappenNow(map) || PlayWork.Kitchen(map) == null) return false;
            var sick = BadBatch.Sick(map);
            if (sick.Count < 2) return false;
            var cooks = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.Downed && !p.DevelopmentalStage.Juvenile() && p.workSettings != null
                                                                     && !p.WorkTypeIsDisabled(cooking)).ToList();
            var a = cooks.Where(p => p.workSettings.GetPriority(cooking) > 0)
                .OrderBy(p => p.workSettings.GetPriority(cooking)).ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
            if (a == null) return false;
            var b = cooks.Where(p => p != a && !sick.Contains(p)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault()
                    ?? cooks.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
            if (b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = sick.Count };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            switch (BadBatch.Cause(ctx?.map))
            {
                case FoodPoisonCause.IncompetentCook: tags.Add("cause:cook"); break;
                case FoodPoisonCause.FilthyKitchen: tags.Add("cause:filth"); break;
                case FoodPoisonCause.Rotten: tags.Add("cause:rotten"); break;
                case FoodPoisonCause.DangerousFoodType: tags.Add("cause:food"); break;
            }
            if (ctx?.a != null && BadBatch.Sick(ctx.map).Contains(ctx.a)) tags.Add("cooksick");
        }
    }

    /// <summary>
    /// A's work of a kind (<see cref="work"/>, the cooking unless given) handed to B for <see cref="days"/> days: A stops
    /// doing it, B takes A's place in it, and both go back as they were after (AnswerShifts). Nothing when A doesn't do
    /// that work or B can't.
    /// </summary>
    public class DilemmaEffect_HandOver : DilemmaEffect
    {
        public string work = "Cooking";
        public float days = AnswerShiftMath.HandOverDays;
        [MustTranslate] public string label;

        public override bool ChanceEach => false;

        private WorkTypeDef Work => DefDatabase<WorkTypeDef>.GetNamedSilentFail(work);

        private bool Can(DilemmaContext ctx) =>
            Work is WorkTypeDef w && ctx?.a?.workSettings != null && ctx.b?.workSettings != null && ctx.a != ctx.b && ctx.a.workSettings.GetPriority(w) > 0 && !ctx.b.WorkTypeIsDisabled(w);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Can(ctx)) return;
            sheet.Play("CC_Impact_HandOver".Translate(ctx.a.LabelShort, Work.labelShort ?? Work.label, ctx.b.LabelShort, ImpactMath.Number(days)).Resolve(),
                "CC_Impact_HandOverBrief".Translate(ctx.b.LabelShort, Work.labelShort ?? Work.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!Can(ctx) || AnswerShifts.Instance == null) return;
            var w = Work;
            string what = !label.NullOrEmpty() ? ctx.Format(label) : "CC_DlmV_HandOverLabel".Translate(w.labelShort ?? w.label).Resolve();
            if (AnswerShifts.Instance.HandOver(ctx.map, ctx.a, ctx.b, w, days, what))
                Messages.Message("CC_DlmV_HandedOver".Translate(ctx.a.LabelShort, w.labelShort ?? w.label, ctx.b.LabelShort).Resolve(), new LookTargets(new[] { ctx.a, ctx.b }),
                    MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // ===================================== Timetables =====================================

    /// <summary>
    /// The targets' timetables changed for a while (AnswerShiftMath.Plan): <see cref="plan"/> "heat" (sleep through the
    /// midday heat, work by night), "siesta" (the hottest hours a rest in the shade), "night" (work through tonight,
    /// sleep the morning after), "late" (a late start tomorrow). Put back when its time is up, or with
    /// <see cref="condition"/> when that game condition ends (the heat wave). Only the timetable changes: danger, meals
    /// and sickbeds are the game's as always. With "Answers play out on the map" off, nothing changes.
    /// </summary>
    public class DilemmaEffect_Hours : DilemmaEffect
    {
        public string plan = AnswerShiftMath.Late;
        public string condition;
        [MustTranslate] public string label;

        private List<Pawn> Who(DilemmaContext ctx) =>
            Reached(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map && p.timetable != null && !(AnswerShifts.Instance?.Shifted(p) ?? false)).ToList();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            var who = Who(ctx);
            if (who.Count == 0) return;
            string w = WhoOf(ctx, who);
            sheet.Play(("CC_Impact_Hours_" + plan).Translate(w).Resolve(), ("CC_Impact_HoursBrief_" + plan).Translate(w).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null || AnswerShifts.Instance == null) return;
            try
            {
                var who = Who(ctx).Where(p => chance >= 1f || Rand.Chance(chance)).ToList();
                string what = !label.NullOrEmpty() ? ctx.Format(label) : ("CC_DlmV_HoursLabel_" + plan).Translate().Resolve();
                var given = AnswerShifts.Instance.Hours(ctx.map, who, plan, condition, what);
                if (given.Count == 0) return;
                string names = given.Count > 3 ? "CC_Play_Everyone".Translate().Resolve() : TextUtil.JoinAnd(given.Select(p => p.LabelShort).ToList());
                Messages.Message(("CC_DlmV_Hours_" + plan).Translate(names).Resolve(), new LookTargets(given), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("answer hours", e);
            }
        }
    }

    /// <summary>
    /// The heat wave is on (the game's own) and two or more of the colony are out in it, in the day: what to do about the
    /// hours. A is one of those out in the heat.
    /// </summary>
    public class DilemmaWorker_HeatWave : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map) || GameConditionDefOf.HeatWave == null || !map.gameConditionManager.ConditionIsActive(GameConditionDefOf.HeatWave)) return false;
            if (!OnMapMath.Between(GenLocalDate.HourOfDay(map), 9, 17)) return false;
            return Outside(map).Count >= 2;
        }

        private static List<Pawn> Outside(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.Downed && p.Awake() && !p.Position.Roofed(map) && p.CurJob != null && p.CurJob.workGiverDef != null).ToList();

        protected override bool Extra(DilemmaContext ctx) => base.Extra(ctx) && Outside(ctx.map).Contains(ctx.a);
    }

    /// <summary>
    /// The current research project is three-quarters done or more, and A, the colony's best researcher, is at the
    /// bench now ({THING}: nothing; the project's name is told by the letter's own words, not a placeholder).
    /// </summary>
    public class DilemmaWorker_SoClose : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            var proj = Find.ResearchManager?.GetProject();
            return proj != null && !proj.IsFinished && proj.ProgressPercent >= 0.75f;
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var a = ctx.a;
            if (a?.CurJobDef != JobDefOf.Research || a.workSettings == null || !a.workSettings.WorkIsActive(WorkTypeDefOf.Research)) return false;
            int level = a.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            return !ctx.map.mapPawns.FreeColonistsSpawned.Any(p => p != a && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Research)
                                                                    && (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) > level);
        }
    }

    // ===================================== The precept broken =====================================

    /// <summary>
    /// Ideology: someone of the colony's own faith thinks less of A for breaking one of its precepts (the game's own
    /// memory of it, from the last three days). B is the one who speaks for the faith: its moral guide, else its most
    /// devout and best spoken (never A). Tags: "precept:IssueName" (precept:MeatEating, precept:Drugs) for what was broken.
    /// </summary>
    public class DilemmaWorker_PreceptBroken : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => ModsConfig.IdeologyActive && DecreeWork.ColonyFaith != null && base.CanHappenNow(map);

        /// <summary>A memory of someone of the faith against A for a precept A broke, the newest first.</summary>
        public static Thought_MemorySocial Against(Map map, Pawn a)
        {
            if (map == null || a == null) return null;
            Thought_MemorySocial best = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == a || !DecreeWork.Believer(p)) continue;
                var mems = p.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                    if (m is Thought_MemorySocial s && s.otherPawn == a && s.sourcePrecept != null && s.age < GenDate.TicksPerDay * 3 && s.OpinionOffset() < 0f
                        && (best == null || s.age < best.age))
                        best = s;
            }
            return best;
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || Against(ctx.map, ctx.a) == null) return false;
            var faith = DecreeWork.ColonyFaith;
            // The penance is kept at the faith's altar: without one standing, the question isn't asked.
            if ((FaithEvenings.Place(ctx.map, faith) ?? FaithEvenings.AnyPlace(ctx.map)) == null) return false;
            var speaker = FaithEvenings.Speaker(ctx.map, faith);
            if (speaker == null || speaker == ctx.a) speaker = ctx.map.mapPawns.FreeColonistsSpawned.Where(p => p != ctx.a && DecreeWork.Believer(p) && !p.Downed && !p.IsSlave)
                .OrderByDescending(Faiths.Certainty).FirstOrDefault();
            if (speaker == null) return false;
            ctx.b = speaker;
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var m = Against(ctx?.map, ctx?.a);
            var issue = m?.sourcePrecept?.def?.issue;
            if (issue != null) tags.Add("precept:" + issue.defName);
        }
    }

    // ===================================== The doctor is the patient =====================================

    /// <summary>
    /// The colony's only real doctor (A: the best at medicine of those who doctor; the next best has half their skill or
    /// less) is ill or hurt and needs tending. B is that next best, who can doctor.
    /// </summary>
    public class DilemmaWorker_DoctorPatient : DilemmaWorker
    {
        private static int Medicine(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;

        private static bool Doctors(Pawn p) => p?.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !p.DevelopmentalStage.Juvenile() && !p.IsQuestLodger();

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var doctors = map.mapPawns.FreeColonistsSpawned.Where(Doctors).OrderByDescending(Medicine).ToList();
            if (doctors.Count < 2) return false;
            var a = doctors[0];
            var b = doctors.Skip(1).FirstOrDefault(p => !p.Downed && !p.InMentalState);
            if (b == null || Medicine(a) < 6 || Medicine(b) * 2 > Medicine(a)) return false;
            if (a.Downed || a.InMentalState || a.playerSettings == null || a.playerSettings.selfTend) return false;
            if (!a.health.HasHediffsNeedingTendByPlayer()) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, skill = SkillDefOf.Medicine };
            return true;
        }
    }

    /// <summary>The target (A unless given) tends their own wounds: the game's own self-tend, switched on for them (at the game's lower care for it).</summary>
    public class DilemmaEffect_SelfTend : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var who = Reached(ctx).Where(p => p.playerSettings != null && !p.playerSettings.selfTend && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).ToList();
            if (who.Count == 0) return;
            sheet.Play("CC_Impact_SelfTend".Translate(WhoOf(ctx, who)).Resolve(), "CC_Impact_SelfTendBrief".Translate(WhoOf(ctx, who)).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx))
            {
                if (p.playerSettings == null || p.playerSettings.selfTend || p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) continue;
                p.playerSettings.selfTend = true;
                Messages.Message("CC_DlmV_SelfTend".Translate(p.LabelShort).Resolve(), p, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    /// <summary>The target (A unless given) is tended with the best medicine in the stores: their medical care set to the best, as the player would.</summary>
    public class DilemmaEffect_MedCare : DilemmaEffect
    {
        private static Thing Best(Map map) => map == null ? null : DilemmaEffect_Consume.Medicine(map).LastOrDefault();

        public override string Unavailable(DilemmaContext ctx) => Best(ctx?.map) == null ? "CC_Dilemma_NotEnough".Translate(1, "CC_Dilemma_Medicine".Translate()).ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var best = Best(ctx?.map);
            var who = Reached(ctx).Where(p => p.playerSettings != null && p.playerSettings.medCare != MedicalCareCategory.Best).ToList();
            if (best == null || who.Count == 0) return;
            sheet.Add(ImpactKind.Goods, -1, "CC_Impact_MedCare".Translate(WhoOf(ctx, who), best.def.label).Resolve(), "CC_Impact_MedCareBrief".Translate(best.def.label).Resolve(), "medcare");
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx))
            {
                if (p.playerSettings == null || p.playerSettings.medCare == MedicalCareCategory.Best) continue;
                p.playerSettings.medCare = MedicalCareCategory.Best;
                Messages.Message("CC_DlmV_MedCare".Translate(p.LabelShort).Resolve(), p, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    // ===================================== Lessons through the bars =====================================

    /// <summary>
    /// A prisoner (X) is far better than A at a skill A loves ({SKILL}): 10 or more, and five above A. A must have a
    /// passion for it and not be at it already as well as X.
    /// </summary>
    public class DilemmaWorker_PrisonerLessons : DilemmaWorker_Stranger
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || ctx.stranger?.skills == null || ctx.a?.skills == null) return false;
            var x = ctx.stranger;
            if (!x.IsPrisonerOfColony || x.Downed || x.InMentalState) return false;
            var fits = x.skills.skills.Where(s => s?.def != null && !s.TotallyDisabled && s.Level >= 10 && ctx.a.skills.GetSkill(s.def) is SkillRecord mine
                                                  && !mine.TotallyDisabled && People.Passionate(mine) && s.Level - mine.Level >= 5)
                .OrderByDescending(s => s.Level).ToList();
            if (fits.Count == 0) return false;
            ctx.skill = fits[0].def;
            return true;
        }
    }

    // ===================================== The drug bench =====================================

    /// <summary>
    /// A bill for a hard drug ({THING}) is running at one of the colony's benches (not suspended), and A objects: a
    /// teetotaler, someone whose faith forbids hard drugs or all drugs, or a parent with a child at home.
    /// </summary>
    public class DilemmaWorker_DrugLab : DilemmaWorker
    {
        public static IEnumerable<Bill> Bills(Map map, bool hardOnly)
        {
            if (map == null) yield break;
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                if (b?.BillStack?.Bills == null) continue;
                foreach (var bill in b.BillStack.Bills)
                {
                    if (bill?.recipe?.products == null || bill.suspended) continue;
                    if (bill.recipe.products.Any(p => IsDrug(p.thingDef, hardOnly))) yield return bill;
                }
            }
        }

        /// <summary>A drug that isn't medicine: with <paramref name="hardOnly"/>, a hard one only.</summary>
        public static bool IsDrug(ThingDef d, bool hardOnly) =>
            d?.ingestible != null && d.IsDrug && !d.IsMedicine && d.ingestible.drugCategory != DrugCategory.Medical
            && (!hardOnly || d.ingestible.drugCategory == DrugCategory.Hard);

        private static bool Objects(Pawn p)
        {
            if (p?.story?.traits == null) return false;
            var desire = DefDatabase<TraitDef>.GetNamedSilentFail("DrugDesire");
            if (desire != null && p.story.traits.HasTrait(desire) && p.story.traits.DegreeOfTrait(desire) < 0) return true;
            if (Beliefs.Forbids(p, Belief.NoHardDrugs) || Beliefs.Forbids(p, Belief.NoDrugs)) return true;
            return p.relations != null && p.relations.Children.Any(c => c != null && !c.Dead && c.Spawned && c.Map == p.Map && c.DevelopmentalStage.Juvenile());
        }

        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && Bills(map, true).Any();

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || !Objects(ctx.a)) return false;
            ctx.thing = Bills(ctx.map, true).SelectMany(b => b.recipe.products).Select(p => p.thingDef).FirstOrDefault(d => IsDrug(d, true));
            return ctx.thing != null;
        }

        /// <summary>"objector:teetotaler", "objector:faith", "objector:parent": why A objects, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var p = ctx?.a;
            if (p?.story?.traits == null) return;
            var desire = DefDatabase<TraitDef>.GetNamedSilentFail("DrugDesire");
            if (desire != null && p.story.traits.HasTrait(desire) && p.story.traits.DegreeOfTrait(desire) < 0) tags.Add("objector:teetotaler");
            else if (Beliefs.Forbids(p, Belief.NoHardDrugs) || Beliefs.Forbids(p, Belief.NoDrugs)) tags.Add("objector:faith");
            else tags.Add("objector:parent");
        }
    }

    /// <summary>
    /// The colony's drug bills suspended at their benches (the player can resume them): the hard drugs, or with
    /// <see cref="all"/> every drug but medicine (beer, smokeleaf, teas and coffees too).
    /// </summary>
    public class DilemmaEffect_SuspendBills : DilemmaEffect
    {
        public bool all;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var bills = DilemmaWorker_DrugLab.Bills(ctx?.map, !all).ToList();
            if (bills.Count == 0) return;
            string what = TextUtil.JoinAnd(bills.SelectMany(b => b.recipe.products).Select(p => p.thingDef).Where(d => DilemmaWorker_DrugLab.IsDrug(d, !all)).Select(d => d.label).Distinct().Take(4).ToList());
            sheet.Play("CC_Impact_SuspendBills".Translate(bills.Count, what).Resolve(), "CC_Impact_SuspendBillsBrief".Translate(bills.Count).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var bills = DilemmaWorker_DrugLab.Bills(ctx?.map, !all).ToList();
            if (bills.Count == 0) return;
            foreach (var b in bills) b.suspended = true;
            var benches = bills.Select(b => b.billStack?.billGiver).OfType<Thing>().Distinct().ToList();
            Messages.Message("CC_DlmV_BillsSuspended".Translate(bills.Count).Resolve(), new LookTargets(benches), MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // ===================================== A slave's freedom =====================================

    /// <summary>
    /// Ideology: a slave of the colony (X) who has lived here a season or more has a friend among the free colonists (A,
    /// who thinks well of them), and A asks for their freedom. {FACTION}: the people they were taken from, if any.
    /// </summary>
    public class DilemmaWorker_SlaveFreedom : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.IdeologyActive || !CanHappenNow(map)) return false;
            var slaves = map.mapPawns.SlavesOfColonySpawned.Where(x => x != null && !x.Dead && !x.Downed && !x.InMentalState && x.RaceProps.Humanlike
                                                                       && (x.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) >= GenDate.TicksPerQuadrum).ToList();
            if (slaves.Count == 0) return false;
            var free = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsSlave && !p.IsQuestLodger() && def.roleA.Allows(p, null)).ToList();
            foreach (var x in slaves.InRandomOrder())
            {
                var a = free.Where(p => p.relations != null && p.relations.OpinionOf(x) >= 30).OrderByDescending(p => p.relations.OpinionOf(x)).FirstOrDefault();
                if (a == null) continue;
                var home = x.guest?.SlaveFaction;
                ctx = new DilemmaContext { def = def, map = map, a = a, stranger = x, faction = home != null && !home.IsPlayer && !home.defeated ? home : null };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The slave (X) freed: into the colony by the game's own emancipation (a warden goes to them and does it; with no
    /// warden at work, the friend A does), or with <see cref="home"/>, to go home to their own people, set free when the
    /// walk the same answer set going begins and on their way off the land with the friend beside them (PlayDepart).
    /// Going home can't be chosen when they have no people to go to, or theirs are at war with the colony.
    /// </summary>
    public class DilemmaEffect_Emancipate : DilemmaEffect
    {
        public bool home;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!home) return null;
            var f = ctx?.stranger?.guest?.SlaveFaction;
            if (f == null || f.IsPlayer || f.defeated) return "CC_DlmV_NoHome".Translate(ctx?.stranger?.LabelShort ?? "?").Resolve();
            if (f.HostileTo(Faction.OfPlayer)) return "CC_DlmV_HomeHostile".Translate(ctx.stranger.LabelShort, f.Name).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.IsSlaveOfColony) return;
            if (home) sheet.Add(ImpactKind.People, -1, "CC_Impact_FreedHome".Translate(x.LabelShort, x.guest?.SlaveFaction?.Name ?? "?").Resolve(), "CC_Impact_FreedHomeBrief".Translate(x.LabelShort).Resolve());
            else sheet.Add(ImpactKind.People, 1, "CC_Impact_Freed".Translate(x.LabelShort).Resolve(), "CC_Impact_FreedBrief".Translate(x.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.IsSlaveOfColony || x.guest == null) return;
            try
            {
                if (home)
                {
                    // Set free when the walk with their friend begins, and off they go together (PlayDepart.cs); now without one.
                    if (PlayDepart.HoldSendAway(x, true)) return;
                    GoHome(x);
                    return;
                }
                x.guest.slaveInteractionMode = SlaveInteractionModeDefOf.Emancipate;
                var map = x.Map;
                bool warden = map != null && map.mapPawns.FreeColonistsSpawned.Any(p => !p.IsSlave && !p.Downed && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Warden));
                var a = ctx.a;
                if (!warden && a != null && a.Spawned && !a.Downed && a.Map == map && JobDefOf.SlaveEmancipation != null)
                    a.jobs?.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.SlaveEmancipation, x), JobTag.Misc);
                Messages.Message("CC_DlmV_Emancipating".Translate(x.LabelShort).Resolve(), x, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("slave freed", e);
            }
        }

        /// <summary>Set free to go back to their own people: no longer the colony's, and on their way off the land.</summary>
        public static void GoHome(Pawn x)
        {
            if (x == null || x.Dead || !x.IsSlaveOfColony) return;
            var to = x.guest?.SlaveFaction;
            if (to == null || to.IsPlayer || to.defeated || to.HostileTo(Faction.OfPlayer)) return;
            GenGuest.SlaveRelease(x);
            if (x.Dead || !x.Spawned) return;
            if (x.Faction == Faction.OfPlayer)
            {
                x.guest?.SetGuestStatus(null);
                x.SetFaction(to);
            }
            if (x.Faction == null || x.Faction.IsPlayer || x.Faction.HostileTo(Faction.OfPlayer)) return;
            DilemmaEffect_SendAway.Send(x);
        }
    }

    // ===================================== Customs =====================================

    /// <summary>
    /// More of what a custom needs, beside <see cref="CustomConditions"/> (on a def with <see cref="DilemmaWorker_CustomMore"/>):
    /// filth and clutter about the home area, a dry day with no fog, the hours it's asked in, a fishing zone with fish in
    /// it and someone to fish, meat in the stores short of the winter with safe game on the land and a hunter to go, the
    /// colony's beehives, or news in the colony lately (a death, a birth, a wedding).
    /// </summary>
    public class CustomMore : DefModExtension
    {
        /// <summary>At least this much filth in the home area, and half as many things lying about to be hauled.</summary>
        public int minFilth;
        /// <summary>No rain, snow or fog now.</summary>
        public bool dry;
        /// <summary>Only asked from the first of these hours to before the second (17~22: an evening); the same twice, any time.</summary>
        public IntRange hours = new IntRange(0, 0);
        /// <summary>A fishing zone with fish in its water (Odyssey; Vanilla Fishing Expanded's by name), and someone to fish.</summary>
        public bool fishing;
        /// <summary>Meat in storage short of this many units per colonist, safe game on the land, and a hunter.</summary>
        public float meatShort;
        /// <summary>The colony's beehives (Alpha Bees' beehouses, or anything named a beehive).</summary>
        public bool beehives;
        /// <summary>A death, a birth or a wedding in the colony within this many days.</summary>
        public float newsDays;
    }

    /// <summary>A seasonal custom with <see cref="CustomConditions"/> and <see cref="CustomMore"/>. Tags "news:death", "news:birth", "news:wedding" for one that follows news.</summary>
    public class DilemmaWorker_CustomMore : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            var c = def.GetModExtension<CustomMore>();
            if (c == null) return true;
            if (c.minFilth > 0 && (map.listerFilthInHomeArea.FilthInHomeArea.Count < c.minFilth || map.listerHaulables.ThingsPotentiallyNeedingHauling().Count < c.minFilth / 2)) return false;
            if (c.dry && !Dry(map)) return false;
            if (!OnMapMath.Between(GenLocalDate.HourOfDay(map), c.hours.min, c.hours.max)) return false;
            if (c.fishing && !Fishing(map)) return false;
            if (c.meatShort > 0f && !MeatShort(map, c.meatShort)) return false;
            if (c.beehives && PlayRoutes.HivesOf(map).Count == 0) return false;
            if (c.newsDays > 0f && News(map, c.newsDays) == null) return false;
            return true;
        }

        /// <summary>No rain, snow or fog falling.</summary>
        public static bool Dry(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            if (w == null) return true;
            return w.rainRate <= 0.01f && w.snowRate <= 0.01f && w.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>A fishing zone that can be fished with fish in it (Odyssey), or Vanilla Fishing Expanded's zones by name, and someone free to fish.</summary>
        public static bool Fishing(Map map)
        {
            if (map == null) return false;
            bool any = false;
            try
            {
                foreach (var z in map.zoneManager.AllZones)
                {
                    if (z == null || z.Cells.Count == 0 || z.GetType().Name.IndexOf("Fishing", StringComparison.Ordinal) < 0) continue;
                    if (z is Zone_Fishing f)
                    {
                        if (!ModsConfig.OdysseyActive || !f.Allowed || !f.HasAnyFishableCells) continue;
                        var tracker = map.waterBodyTracker;
                        if (tracker == null || !f.Cells.Take(40).Any(tracker.AnyFishPopulationAt)) continue;
                    }
                    any = true;
                    break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("custom fishing zone", e);
                return false;
            }
            if (!any) return false;
            return PlayWork.Givers(WorkTask.Fish).Count > 0
                   && map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && PlayWork.Allowed(WorkTask.Fish, p)
                                                                 && PlayWork.Givers(WorkTask.Fish).Any(g => !p.WorkTypeIsDisabled(g.workType)));
        }

        /// <summary>Meat in the stores short of <paramref name="perColonist"/> units each, safe game on the land, and someone to hunt it.</summary>
        public static bool MeatShort(Map map, float perColonist)
        {
            int meat = 0;
            foreach (var kv in map.resourceCounter.AllCountedAmounts)
                if (kv.Key != null && kv.Key.IsMeat) meat += kv.Value;
            int people = map.mapPawns.FreeColonistsSpawnedCount;
            if (meat >= people * perColonist) return false;
            if (PlayHunt.ChooseGame(map, null, null, 1, false).Count == 0) return false;
            return map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && PlayHunt.CanHunt(p));
        }

        /// <summary>The colony's news lately, newest kind first: "death", "birth", "wedding", or null.</summary>
        public static string News(Map map, float days)
        {
            if (map == null) return null;
            var home = map.mapPawns.FreeColonistsSpawned.ToList();
            bool Had(params string[] names) => home.Any(p => LifeChecks.HasThought(p, names.ToList(), days));
            if (Had("KnowColonistDied", "WitnessedDeathFamily")) return "death";
            if (Had("BabyBorn")) return "birth";
            if (Had("GotMarried", "AttendedWedding")) return "wedding";
            return null;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            var c = def.GetModExtension<CustomMore>();
            if (c == null || c.newsDays <= 0f || ctx?.map == null) return;
            var n = News(ctx.map, c.newsDays);
            if (n != null) tags.Add("news:" + n);
        }
    }
}
