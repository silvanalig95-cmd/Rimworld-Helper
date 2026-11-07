using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Medicine by what is treated (the player's request, 7 November 2026: "can you make it so that medicine is always taken
    /// according to the injury? if it's just scratches or bruises i don't want proper or glitterworld medicine used if a pawn
    /// also has a more serious illness at the same time. sometimes i even want no medicine if the injury is not
    /// threatening"). The rule is <see cref="MedicineChoiceMath"/>: harmless wounds get no medicine (or herbal at most, by
    /// the setting), minor ones and everyday ailments herbal at most, the serious the best the patient's medical care
    /// allows. The medical care setting stays the ceiling: this only ever uses less.
    /// <list type="bullet">
    /// <item>Without Smart Medicine, Rimfolk does it itself (Health/MedicineChoicePatches.cs): the serious are tended first
    /// (the game's tend priority, shifted by kind), the doctor fetches the medicine the first tending gets and only as many
    /// as the tendings that get the same, a tending whose wounds get less than the doctor carries is done without it (or the
    /// doctor goes for the lesser medicine first), so the plague gets the good medicine and the scratches none, in separate
    /// tendings. A minor wound riding along in one tending with a serious one costs nothing more, as the game has it.</item>
    /// <item>With Smart Medicine (Integrations/SmartMedicine.cs), its own per-injury choice is filled in where the player set
    /// none, and Smart Medicine does the treating; anything set by hand stands.</item>
    /// <item>In a caravan, the medicine from the packs is the one the first tending gets, with or without Smart Medicine.</item>
    /// <item>Shown: each condition's tooltip on the health tab says what medicine it gets and why.</item>
    /// </list>
    /// The council's answer to give a doctor the best medicine (the doctor is the patient) keeps everything of theirs on the
    /// best while their medical care stays at it. Behind the "medicineByNeed" setting and the deeper game hooks; no letters
    /// or messages.
    /// </summary>
    public static class MedicineChoice
    {
        /// <summary>The setting and the deeper game hooks are on.</summary>
        public static bool SettingOn => RimfolkPatches.On && (ColonyCouncilMod.Settings?.medicineByNeed ?? true);

        /// <summary>Rimfolk chooses the medicine itself: the setting is on and Smart Medicine isn't loaded (it does the treating then).</summary>
        public static bool OwnOn => SettingOn && !SmartMedicineBridge.Present;

        public static bool HarmlessHerbal => ColonyCouncilMod.Settings?.harmlessMedicine == HarmlessMedicine.Herbal;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Someone the player sets medical care for, who can be tended: the colony's people, prisoners, slaves and animals.</summary>
        public static bool Cared(Pawn p) =>
            p != null && !p.Dead && p.playerSettings != null && p.health?.hediffSet != null && p.RaceProps != null && p.RaceProps.IsFlesh;

        /// <summary>The patient's medical care setting, the ceiling (Smart Medicine's own short-lived raise read through).</summary>
        public static int Ceiling(Pawn p)
        {
            if (p?.playerSettings == null) return MedicineChoiceMath.NoCare;
            int? original = SmartMedicineBridge.Present ? SmartMedicineBridge.OriginalCare(p) : null;
            return original ?? (int)p.playerSettings.medCare;
        }

        // ================= The rule, for one condition =================

        private static readonly Dictionary<Hediff, (MedicineChoiceMath.Threat t, MedicineChoiceMath.Why why)> judged =
            new Dictionary<Hediff, (MedicineChoiceMath.Threat, MedicineChoiceMath.Why)>();
        private static readonly Dictionary<int, bool> bleedingOut = new Dictionary<int, bool>();
        private static int judgedTick = -1, judgedFrame = -1;

        /// <summary>The caches hold for one tick and one frame (a wound changes as the game runs, or by dev tools while paused).</summary>
        private static void Fresh()
        {
            int now = Now, frame = Time.frameCount;
            if (now == judgedTick && frame == judgedFrame) return;
            judgedTick = now;
            judgedFrame = frame;
            judged.Clear();
            bleedingOut.Clear();
        }

        /// <summary>How much a condition threatens its patient, and why (MedicineChoiceMath.Judge).</summary>
        public static MedicineChoiceMath.Threat Judge(Hediff h, out MedicineChoiceMath.Why why)
        {
            Fresh();
            if (judged.TryGetValue(h, out var j))
            {
                why = j.why;
                return j.t;
            }
            var t = MedicineChoiceMath.Judge(FactsOf(h), out why);
            judged[h] = (t, why);
            return t;
        }

        private static bool BleedingOut(Pawn p)
        {
            if (bleedingOut.TryGetValue(p.thingIDNumber, out bool b)) return b;
            b = HealthUtility.TicksUntilDeathDueToBloodLoss(p) < MedicineChoiceMath.BleedOutHours * GenDate.TicksPerHour;
            bleedingOut[p.thingIDNumber] = b;
            return b;
        }

        /// <summary>What the game knows of a condition, as the rule reads it.</summary>
        public static MedicineChoiceMath.Facts FactsOf(Hediff h)
        {
            var f = new MedicineChoiceMath.Facts();
            var p = h.pawn;
            if (p?.health?.hediffSet == null) return f;
            var set = p.health.hediffSet;
            var part = h.Part;
            f.injury = h is Hediff_Injury;
            f.missingPart = h is Hediff_MissingPart;
            f.severity = h.Severity;
            f.animal = p.RaceProps.Animal;
            bool solid = false;
            if (part != null)
            {
                f.partHealth = part.def.GetMaxHealth(p);
                solid = part.def.IsSolid(part, set.hediffs);
                var tags = part.def.tags;
                if (tags != null)
                    for (int i = 0; i < tags.Count; i++)
                        if (tags[i] != null && tags[i].vital)
                            f.vitalPart = true;
                f.organ = part.depth == BodyPartDepth.Inside && !solid;
            }
            if (f.injury)
            {
                f.bleedPerDay = UntendedBleed(h, p, part, solid);
                if (!solid && !h.IsPermanent()) f.infectionChance = InfectionChance(h, p, part);
            }
            else if (f.missingPart)
                f.bleedPerDay = h.BleedRate;
            var stage = h.CurStage;
            f.lifeThreatening = stage != null && stage.lifeThreatening;
            f.lethal = h.def.lethalSeverity > 0f;
            if (!f.lethal && h.def.stages != null)
                for (int i = 0; i < h.def.stages.Count; i++)
                    if (h.def.stages[i].lifeThreatening)
                        f.lethal = true;
            f.infection = h.def == HediffDefOf.WoundInfection;
            f.chronic = h.def.chronic;
            var ail = h.TryGetComp<HediffComp_Ailment>();
            f.rimfolkAilment = ail != null;
            f.rimfolkSerious = ail?.Props != null && ail.Props.serious;
            f.bleedingOut = BleedingOut(p);
            f.bestByAnswer = GameComponent_MedicineChoice.Get()?.BestByAnswer(p) ?? false;
            return f;
        }

        /// <summary>
        /// How much a wound would bleed left untended, as the game works it out (its size, its kind, the part, the race),
        /// before any tending or a medic's stabilizing: a wound stays what it is while it is tended.
        /// </summary>
        private static float UntendedBleed(Hediff h, Pawn p, BodyPartRecord part, bool solid)
        {
            if (solid || !p.health.CanBleed || h.def.injuryProps == null) return 0f;
            if (part != null)
            {
                var added = p.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (added != null && !added.def.organicAddedBodypart) return 0f;
            }
            float b = h.Severity * h.def.injuryProps.bleedRate * p.RaceProps.bleedRateFactor;
            if (part != null) b *= part.def.bleedRate;
            return b;
        }

        /// <summary>
        /// The wound kind's chance of turning infected, as the game rolls it (not its hidden roll itself): none on a
        /// prosthetic, for someone immune to infection or regenerating; the storyteller's factor for the colony's own.
        /// </summary>
        private static float InfectionChance(Hediff h, Pawn p, BodyPartRecord part)
        {
            var inf = h.def.CompProps<HediffCompProperties_Infecter>();
            if (inf == null || inf.infectionChance <= 0f) return 0f;
            var set = p.health.hediffSet;
            if (part != null && set.PartOrAnyAncestorHasDirectlyAddedParts(part)) return 0f;
            if (set.HasRegeneration) return 0f;
            if (p.health.immunity != null && p.health.immunity.DiseaseContractChanceFactor(HediffDefOf.WoundInfection, part) <= 0.001f) return 0f;
            float c = inf.infectionChance;
            if (p.Faction == Faction.OfPlayer) c *= Find.Storyteller?.difficulty?.playerPawnInfectionChanceFactor ?? 1f;
            return c;
        }

        // ================= The care each gets =================

        /// <summary>
        /// The medical care a condition gets: Smart Medicine's choice for it where one is set (by hand, as the player set
        /// it, which Smart Medicine lets go above the patient's care; by Rimfolk, never above it), otherwise the rule's,
        /// never above the patient's own setting. <paramref name="byHand"/>: the player set it with Smart Medicine.
        /// </summary>
        public static int CareOf(Hediff h, out bool byHand)
        {
            byHand = false;
            var p = h.pawn;
            int ceiling = Ceiling(p);
            if (SmartMedicineBridge.Ready)
            {
                if (SmartMedicineBridge.EntryOf(h, out int set, out bool ours))
                {
                    byHand = !ours;
                    return byHand || set < ceiling ? set : ceiling;
                }
                // No choice set: Smart Medicine treats it by the patient's own care (the serious, and one taken back by hand).
                byHand = h.loadID > 0 && (GameComponent_MedicineChoice.Get()?.handsOff.Contains(h.loadID) ?? false);
                return ceiling;
            }
            return MedicineChoiceMath.CareFor(Judge(h, out _), ceiling, HarmlessHerbal);
        }

        /// <summary>What Rimfolk would choose for a condition: the rule's care, never above the setting.</summary>
        public static int RuleCare(Hediff h) => MedicineChoiceMath.CareFor(Judge(h, out _), Ceiling(h.pawn), HarmlessHerbal);

        /// <summary>One tending's care: the most any of its conditions gets (a minor wound rides along with a serious one).</summary>
        public static int BatchCare(List<Hediff> batch)
        {
            int c = -1;
            for (int i = 0; i < batch.Count; i++)
            {
                int k = CareOf(batch[i], out _);
                if (k > c) c = k;
            }
            return c;
        }

        private static readonly List<Hediff> batchBuf = new List<Hediff>();

        /// <summary>The care the next tending gets, the game's own choice of what one tending covers; -1 with nothing to tend.</summary>
        public static int NextTendingCare(Pawn patient, bool usingMedicine)
        {
            if (SmartMedicineBridge.Ready) SmartMedicineBridge.Refresh(patient);
            batchBuf.Clear();
            TendUtility.GetOptimalHediffsToTendWithSingleTreatment(patient, usingMedicine, batchBuf);
            int c = batchBuf.Count > 0 ? BatchCare(batchBuf) : -1;
            batchBuf.Clear();
            return c;
        }

        /// <summary>
        /// The most care any condition gets that isn't due a tending yet but will be within <paramref name="ticks"/> (an
        /// illness whose last tending is running out); -1 for none. A job made to tend a bruise without medicine would
        /// otherwise reach the plague coming due on the way with nothing in hand.
        /// </summary>
        public static int SoonCare(Pawn patient, int ticks)
        {
            int c = -1;
            var hs = patient.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!(h is HediffWithComps) || !h.TendableNow(ignoreTimer: true) || h.TendableNow()) continue;
                var td = h.TryGetComp<HediffComp_TendDuration>();
                if (td == null || td.TProps.TendIsPermanent || td.tendTicksLeft - td.TProps.TendTicksOverlap > ticks) continue;
                int k = CareOf(h, out _);
                if (k > c) c = k;
            }
            return c;
        }

        private static float herbalPotency = -1f, industrialPotency = -1f;

        /// <summary>The lowest medical care that allows this medicine (as the game's AllowsMedicine).</summary>
        public static int Needs(ThingDef medicine)
        {
            if (herbalPotency < 0f)
            {
                herbalPotency = ThingDefOf.MedicineHerbal.GetStatValueAbstract(StatDefOf.MedicalPotency);
                industrialPotency = ThingDefOf.MedicineIndustrial.GetStatValueAbstract(StatDefOf.MedicalPotency);
            }
            return MedicineChoiceMath.NeedsCare(medicine.GetStatValueAbstract(StatDefOf.MedicalPotency), herbalPotency, industrialPotency);
        }

        // ================= The batches, as the game tends them =================

        private static readonly List<Hediff> tendable = new List<Hediff>();
        private static readonly List<Hediff> oneBatch = new List<Hediff>();
        private static readonly List<int> cares = new List<int>();

        /// <summary>
        /// The care of each tending in turn, as the game would do them with medicine (Medicine.GetMedicineCountToFullyHeal's
        /// own walk), into <paramref name="into"/>.
        /// </summary>
        public static void TendingCares(Pawn patient, List<int> into, List<List<Hediff>> batches = null)
        {
            into.Clear();
            tendable.Clear();
            var hs = patient.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
                if (hs[i].TendableNow())
                    tendable.Add(hs[i]);
            TendUtility.SortByTendPriority(tendable);
            for (int guard = 0; tendable.Count > 0 && guard < 100; guard++)
            {
                TendUtility.GetOptimalHediffsToTendWithSingleTreatment(patient, true, oneBatch, tendable);
                if (oneBatch.Count == 0) break;
                into.Add(BatchCare(oneBatch));
                batches?.Add(new List<Hediff>(oneBatch));
                for (int i = 0; i < oneBatch.Count; i++) tendable.Remove(oneBatch[i]);
            }
            tendable.Clear();
            oneBatch.Clear();
        }

        /// <summary>How many of one medicine the doctor fetches: the tendings that get the same care as the first.</summary>
        public static int MedicineCount(Pawn patient)
        {
            TendingCares(patient, cares);
            int n = MedicineChoiceMath.SameMedicineCount(cares);
            cares.Clear();
            return n;
        }

        // ================= The tend job =================

        /// <summary>The care each doctor's medicine was last fetched for, and for whom (not saved: a load forgets, and the job goes on as the game has it).</summary>
        private static readonly Dictionary<int, (int patient, int care)> chosen = new Dictionary<int, (int, int)>();
        private static Game chosenGame;

        /// <summary>A doctor's medicine is fetched for a tending of this care.</summary>
        public static void Chose(Pawn doctor, Pawn patient, int care)
        {
            if (doctor == null || patient == null) return;
            if (chosenGame != Current.Game || chosen.Count > 100)
            {
                chosenGame = Current.Game;
                chosen.Clear();
            }
            chosen[doctor.thingIDNumber] = (patient.thingIDNumber, care);
        }

        private static int ChosenFor(Pawn doctor, Pawn patient) =>
            chosenGame == Current.Game && chosen.TryGetValue(doctor.thingIDNumber, out var c) && c.patient == patient.thingIDNumber ? c.care : int.MaxValue;

        /// <summary>
        /// After a doctor's tending: the job ends (the game's own end-after-one-tending, as its self-tend has it) when the next
        /// tending gets other medicine than the doctor has in hand (MedicineChoiceMath.EndAfter), so the next job fetches it.
        /// Never a job the player ordered or a drafted doctor's. <paramref name="canTendWithout"/>: Rimfolk tends a batch that
        /// gets no medicine without the one in hand (its own way; with Smart Medicine the job ends instead).
        /// </summary>
        public static void AfterTending(Pawn doctor, Pawn patient, bool canTendWithout)
        {
            var job = doctor?.CurJob;
            if (job == null || job.def != JobDefOf.TendPatient || job.targetA.Thing != patient || job.endAfterTendedOnce || job.playerForced || job.draftedTend) return;
            if (!patient.health.HasHediffsNeedingTend()) return;
            int next = NextTendingCare(patient, true);
            var carried = job.targetB.Thing as Medicine;
            bool carrying = carried != null && !carried.Destroyed;
            if (MedicineChoiceMath.EndAfter(next, carrying, carrying ? Needs(carried.def) : 0, ChosenFor(doctor, patient), canTendWithout))
                job.endAfterTendedOnce = true;
        }

        // ================= The council's answer: the best for a doctor who is the patient =================

        /// <summary>The council gave this patient the best medicine there is (DilemmaEffect_MedCare): everything of theirs is serious while their care stays at the best.</summary>
        public static void GaveBest(Pawn p)
        {
            if (p == null) return;
            GameComponent_MedicineChoice.Get()?.AddBest(p);
        }

        // ================= Shown: the line on the health tab =================

        /// <summary>The line under a condition's tooltip on the health tab: the medicine it gets and why; null for none.</summary>
        public static string LineFor(Hediff h)
        {
            var p = h?.pawn;
            if (!SettingOn || !Cared(p) || !h.TendableNow(ignoreTimer: true)) return null;
            int ceiling = Ceiling(p);
            if (ceiling <= MedicineChoiceMath.NoCare) return null;
            if (SmartMedicineBridge.Ready) SmartMedicineBridge.Refresh(p);
            int care = CareOf(h, out bool byHand);
            string careLabel = ((MedicalCareCategory)care).GetLabel();
            if (byHand) return "CC_Med_Line_ByHand".Translate(careLabel.Named("CARE")).Resolve().CapitalizeFirst();
            var t = Judge(h, out var why);
            int want = MedicineChoiceMath.CareFor(t, MedicineChoiceMath.Best, HarmlessHerbal);
            string what;
            if (t == MedicineChoiceMath.Threat.Serious)
                what = "CC_Med_Care_Best".Translate(p.Named("PAWN"), ((MedicalCareCategory)ceiling).GetLabel().Named("CARE")).Resolve();
            else if (care < want)
                what = "CC_Med_Care_Setting".Translate(p.Named("PAWN"), careLabel.Named("CARE")).Resolve();
            else
                what = (care <= MedicineChoiceMath.NoMeds ? "CC_Med_Care_None" : "CC_Med_Care_Herbal").Translate().Resolve();
            string reason = ("CC_Med_Why_" + why).Translate(p.Named("PAWN"), (h.Part?.Label ?? "").Named("PART")).Resolve();
            return "CC_Med_Line".Translate(what.Named("WHAT"), reason.Named("WHY")).Resolve().CapitalizeFirst();
        }

        /// <summary>For the dev log: each condition the patient has to tend, its kind, why, and the tendings in the game's order.</summary>
        public static string Explain(Pawn p)
        {
            var sb = new StringBuilder();
            sb.Append(p.LabelShort).Append(": medical care ").Append(((MedicalCareCategory)Ceiling(p)).GetLabel()).Append(", ")
                .Append(SmartMedicineBridge.Present ? SmartMedicineBridge.Ready ? "with Smart Medicine (its per-injury choice filled in)" : "with Smart Medicine, unreadable (it chooses alone)" : "Rimfolk chooses")
                .Append(SettingOn ? "" : " (the setting or the deeper hooks are off)").Append(", bleeding out ").Append(BleedingOut(p) ? "yes" : "no").Append('\n');
            if (SmartMedicineBridge.Ready) SmartMedicineBridge.Refresh(p);
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!h.TendableNow(ignoreTimer: true)) continue;
                var t = Judge(h, out var why);
                var f = FactsOf(h);
                int care = CareOf(h, out bool byHand);
                sb.Append("  ").Append(h.LabelCap).Append(h.Part != null ? " (" + h.Part.Label + ")" : "").Append(": ").Append(t).Append(" (").Append(why).Append("), ")
                    .Append(((MedicalCareCategory)care).GetLabel()).Append(byHand ? " set by hand" : "")
                    .Append(h.TendableNow() ? "" : ", tended for now")
                    .Append("; bleeds ").Append(f.bleedPerDay.ToString("0.00")).Append("/day, infection ")
                    .Append(MedicineChoiceMath.WoundInfectionChance(f.infectionChance, f.severity, f.animal).ToStringPercent("0.#"))
                    .Append(", tend priority ").Append(h.TendPriority.ToString("0.###")).Append('\n');
            }
            var order = new List<List<Hediff>>();
            var list = new List<int>();
            TendingCares(p, list, order);
            for (int i = 0; i < order.Count; i++)
            {
                sb.Append("  tending ").Append(i + 1).Append(": ").Append(((MedicalCareCategory)list[i]).GetLabel()).Append(" for ");
                for (int j = 0; j < order[i].Count; j++) sb.Append(j > 0 ? ", " : "").Append(order[i][j].LabelCap);
                sb.Append('\n');
            }
            if (order.Count > 0) sb.Append("  medicine fetched for the first: ").Append(Medicine.GetMedicineCountToFullyHeal(p)).Append('\n');
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// What medicine by what is treated keeps in the save: the patients the council gave the best medicine to (their ids,
    /// let go once their medical care is changed or they die), and with Smart Medicine, which of its per-injury choices
    /// Rimfolk filled in (the condition's id and the care), and those the player took back to the default, so a choice of
    /// the player's is never touched and Rimfolk's own follow the wound after a load. Pruned to the living at each save.
    /// </summary>
    public class GameComponent_MedicineChoice : GameComponent
    {
        public const int MaxBest = 40, MaxFilled = 600, MaxHandsOff = 200;

        public List<int> bestByAnswer = new List<int>();
        public Dictionary<int, int> filled = new Dictionary<int, int>();
        public List<int> handsOff = new List<int>();
        private List<int> filledKeys, filledValues;

        private static GameComponent_MedicineChoice cached;
        private static Game cachedGame;

        public GameComponent_MedicineChoice(Game game)
        {
        }

        public static GameComponent_MedicineChoice Get()
        {
            var g = Current.Game;
            if (g == null) return null;
            if (cachedGame != g || cached == null)
            {
                cachedGame = g;
                cached = g.GetComponent<GameComponent_MedicineChoice>();
            }
            return cached;
        }

        public bool BestByAnswer(Pawn p)
        {
            if (bestByAnswer.Count == 0 || !bestByAnswer.Contains(p.thingIDNumber)) return false;
            if (p.playerSettings == null || p.playerSettings.medCare != MedicalCareCategory.Best)
            {
                // The player has set their care since: it is theirs again.
                bestByAnswer.Remove(p.thingIDNumber);
                return false;
            }
            return true;
        }

        public void AddBest(Pawn p)
        {
            bestByAnswer.Remove(p.thingIDNumber);
            bestByAnswer.Add(p.thingIDNumber);
            while (bestByAnswer.Count > MaxBest) bestByAnswer.RemoveAt(0);
        }

        /// <summary>A choice of Rimfolk's in Smart Medicine taken back to the default by hand: left to the player.</summary>
        public void HandsOff(int id)
        {
            if (handsOff.Contains(id)) return;
            handsOff.Add(id);
            while (handsOff.Count > MaxHandsOff) handsOff.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Prune();
            Scribe_Collections.Look(ref bestByAnswer, "bestByAnswer", LookMode.Value);
            Scribe_Collections.Look(ref filled, "filled", LookMode.Value, LookMode.Value, ref filledKeys, ref filledValues);
            Scribe_Collections.Look(ref handsOff, "handsOff", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bestByAnswer ??= new List<int>();
                filled ??= new Dictionary<int, int>();
                handsOff ??= new List<int>();
                // The original Smart Medicine forgets its choices at each load: Rimfolk's are gone with them, and filled in afresh.
                if (!SmartMedicineBridge.Saves) filled.Clear();
            }
        }

        /// <summary>Only what still lives is saved: the patients alive, the conditions someone still has.</summary>
        private void Prune()
        {
            try
            {
                var pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
                var alive = new HashSet<int>();
                var ids = new HashSet<int>();
                for (int i = 0; i < pawns.Count; i++)
                {
                    var p = pawns[i];
                    alive.Add(p.thingIDNumber);
                    var hs = p.health?.hediffSet?.hediffs;
                    if (hs == null) continue;
                    for (int j = 0; j < hs.Count; j++) ids.Add(hs[j].loadID);
                }
                bestByAnswer.RemoveAll(id => !alive.Contains(id));
                if (filled.Count > 0)
                {
                    var gone = new List<int>();
                    foreach (var id in filled.Keys)
                        if (!ids.Contains(id)) gone.Add(id);
                    foreach (var id in gone) filled.Remove(id);
                }
                handsOff.RemoveAll(id => !ids.Contains(id));
                while (handsOff.Count > MaxHandsOff) handsOff.RemoveAt(0);
                if (filled.Count > MaxFilled)
                {
                    var keys = new List<int>(filled.Keys);
                    keys.Sort();
                    for (int i = 0; i < keys.Count - MaxFilled; i++) filled.Remove(keys[i]);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: tidying before a save", e);
            }
        }
    }
}
