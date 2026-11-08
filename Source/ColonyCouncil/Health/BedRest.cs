using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Bed rest only when it makes sense (the player's request, 8 November 2026: "I just don't want ... if someone has a
    /// bit of a sunburn that they just stay in bed all day to rest and recover ... they have a negative impact normally,
    /// but they don't keep a pawn bedridden unless it also kind of makes sense"). The game sends anyone to bed rest who has a
    /// tended wound still healing or an illness still building immunity (HealthAIUtility.ShouldSeekMedicalRest), however
    /// small: a tended scratch, a head cold, a grower's tuberculosis for weeks. The rule is <see cref="BedRestMath"/>: only a
    /// serious wound, a dangerous illness, a stage too much to work through, one of Rimfolk's ailments from the stage its
    /// def names, and what the game makes urgent send someone to bed; with anything else they keep working, its pain,
    /// capacities and mood costing them as they do.
    /// <list type="bullet">
    /// <item>The game's answer itself is changed (Health/BedRestPatches.cs, a postfix on ShouldSeekMedicalRest): going to bed
    /// for rest, staying in a medical bed, being fed and visited in bed, meditating, the hospital's beds at night all follow
    /// it. The game's notice of a full recovery keeps the game's own answer, or it would come every ten seconds.</item>
    /// <item>The player's own order to bed (the medical bed's "rest until healed") and the council's rest when ill decree
    /// keep the game's rest, and the rule's on top. Smart Medicine's "Bed rest" unticked for a condition keeps it off.</item>
    /// <item>Small things to tend (a sunburn, a bruise) still bring the patient to bed to be tended, as the game has it, and up
    /// again after; but while no doctor is free to come, they keep working rather than wait in bed.</item>
    /// <item>Shown: each condition's tooltip on the health tab says whether it keeps them working or sends them to bed and why,
    /// beside medicine by what is treated's line, and a resting colonist's activity names what they rest for.</item>
    /// </list>
    /// Behind the "restByNeed" setting and the deeper game hooks; nothing saved, no letters or messages.
    /// </summary>
    public static class BedRest
    {
        /// <summary>
        /// The game's notice of a full recovery keeps the game's own answer (Patch_BedRestHealedNotice found its call). Without
        /// it the rule stays off: every healing wound would bring "fully healed" every 600 ticks.
        /// </summary>
        public static bool NoticeKept;

        /// <summary>The setting, the deeper game hooks, and the recovery notice kept as the game has it.</summary>
        public static bool On => NoticeKept && RimfolkPatches.On && (ColonyCouncilMod.Settings?.restByNeed ?? true);

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The game's own answer is asked for (the recovery notice, the tooltip's look at it): the rule stands aside.</summary>
        [ThreadStatic] private static int gameView;

        /// <summary>The medical bed's "Use medical bed" menu is being made: the player may order rest the game would give.</summary>
        [ThreadStatic] private static int playerAsks;

        [ThreadStatic] private static bool working;

        public static void AskStart() => playerAsks++;

        public static void AskEnd()
        {
            if (playerAsks > 0) playerAsks--;
        }

        /// <summary>The game's own answer to "should they seek medical rest", with every other mod's on it but not the rule's.</summary>
        public static bool GameRest(Pawn p)
        {
            gameView++;
            try
            {
                return HealthAIUtility.ShouldSeekMedicalRest(p);
            }
            finally
            {
                gameView--;
            }
        }

        // ================= The answer =================

        /// <summary>The rule's answer, given the game's: whether they seek medical rest.</summary>
        public static bool Verdict(Pawn p, bool game)
        {
            if (gameView > 0 || working || p?.health?.hediffSet == null || p.Dead) return game;
            working = true;
            try
            {
                return BedRestMath.Rests(PatientOf(p, game));
            }
            finally
            {
                working = false;
            }
        }

        /// <summary>What has them in bed now, by the rule, and the condition that does when one does.</summary>
        public static BedRestMath.InBed Why(Pawn p, out Hediff because)
        {
            because = null;
            if (p?.health?.hediffSet == null || p.Dead) return BedRestMath.InBed.No;
            bool game = GameRest(p);
            working = true;
            try
            {
                var state = BedRestMath.Decide(PatientOf(p, game));
                if (state == BedRestMath.InBed.Condition && seen.TryGetValue(p.thingIDNumber, out var s)) because = s.because;
                return state;
            }
            finally
            {
                working = false;
            }
        }

        // ================= What the game knows of the patient, kept a tick (their conditions a second) =================

        private sealed class Seen
        {
            public int tick = int.MinValue, count = -1;
            public BedRestMath.Patient patient;
            public int condTick = int.MinValue, condCount = -1;
            public bool rests;
            public Hediff because;
            public BedRestMath.Why why;
            public int doctorTick = int.MinValue;
            public bool doctorFree;
        }

        private static readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
        private static Game seenGame;

        /// <summary>How long the look at someone's conditions holds (a second): they change slowly, and the game asks often.</summary>
        public const int ConditionTicks = 60;

        private static Seen SeenOf(Pawn p)
        {
            if (seenGame != Current.Game || seen.Count > 2000)
            {
                seenGame = Current.Game;
                seen.Clear();
            }
            if (!seen.TryGetValue(p.thingIDNumber, out var s))
            {
                s = new Seen();
                seen[p.thingIDNumber] = s;
            }
            return s;
        }

        /// <summary>Not under the rule: a baby (always down), a creature not of flesh, a mutant. The game's own rest.</summary>
        public static bool Exempt(Pawn p) =>
            p.RaceProps == null || !p.RaceProps.IsFlesh || p.IsMutant || LifeStageUtility.AlwaysDowned(p) || p.DevelopmentalStage.Baby();

        private static BedRestMath.Patient PatientOf(Pawn p, bool game)
        {
            var s = SeenOf(p);
            var set = p.health.hediffSet;
            int now = Now, count = set.hediffs.Count;
            if (s.tick != now || s.count != count)
            {
                s.tick = now;
                s.count = count;
                var f = new BedRestMath.Patient { exempt = Exempt(p) };
                if (!f.exempt)
                {
                    f.downed = p.Downed && !LifeStageUtility.AlwaysDowned(p);
                    f.surgery = HealthAIUtility.ShouldHaveSurgeryDoneNow(p);
                    f.labor = set.InLabor();
                    f.needsTending = p.health.HasHediffsNeedingTend();
                    f.bleeding = set.BleedRateTotal >= BedRestMath.LosingBlood;
                    f.seriousToTend = f.needsTending && SeriousToTend(p);
                    f.gameOwnReasons = set.HasTendedAndHealingInjury() || set.HasImmunizableNotImmuneHediff();
                    f.decree = Decree(p);
                    Conditions(p, s, now, count);
                    f.conditionRests = s.rests;
                    f.doctorFree = f.needsTending && !f.bleeding && !f.seriousToTend && DoctorFree(p, s, now);
                }
                s.patient = f;
            }
            var r = s.patient;
            r.gameRest = game;
            r.ordered = playerAsks > 0 || (p.CurJob?.restUntilHealed ?? false);
            return r;
        }

        /// <summary>Something to tend now that medicine by what is treated counts serious (an illness, a deep wound, heavy bleeding).</summary>
        private static bool SeriousToTend(Pawn p)
        {
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!h.TendableNow()) continue;
                var f = MedicineChoice.FactsOf(h);
                f.bestByAnswer = false;
                if (MedicineChoiceMath.Judge(f, out _) == MedicineChoiceMath.Threat.Serious) return true;
            }
            return false;
        }

        /// <summary>
        /// Someone can come and tend them now (the game's own look for a doctor: awake, up, able, doing doctor's work, who can
        /// reach them), and their medical care allows any. Not one the player tends: the game's own way, so yes.
        /// </summary>
        private static bool DoctorFree(Pawn p, Seen s, int now)
        {
            if (p.playerSettings == null) return true;
            if (now - s.doctorTick < ConditionTicks && now >= s.doctorTick) return s.doctorFree;
            s.doctorTick = now;
            s.doctorFree = HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(p) && WorkGiver_PatientGoToBedTreatment.AnyAvailableDoctorFor(p);
            return s.doctorFree;
        }

        private static DecreeDef sickBay;
        private static bool sickBayLooked;

        /// <summary>The council's rest when ill decree is in force for one of the colony's own people.</summary>
        private static bool Decree(Pawn p)
        {
            if (!sickBayLooked)
            {
                sickBayLooked = true;
                sickBay = DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_SickBay");
            }
            return sickBay != null && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && DecreeTracker.InForceFor(p, sickBay);
        }

        /// <summary>The conditions that send them to bed, looked at again each second or when one comes or goes.</summary>
        private static void Conditions(Pawn p, Seen s, int now, int count)
        {
            if (now - s.condTick < ConditionTicks && now >= s.condTick && s.condCount == count) return;
            s.condTick = now;
            s.condCount = count;
            s.rests = false;
            s.because = null;
            s.why = BedRestMath.Why.None;
            var body = BodyOf(p);
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!Considered(h)) continue;
                var why = BedRestMath.Sends(ConditionOf(h, body), body);
                if (why == BedRestMath.Why.None) continue;
                // An illness before a wound: "resting: flu" says more than one of the wounds that came with it.
                if (s.because == null || (h is Hediff_Injury) == false && s.because is Hediff_Injury)
                {
                    s.because = h;
                    s.why = why;
                }
                s.rests = true;
            }
        }

        // ================= One condition =================

        private static readonly Dictionary<HediffDef, bool> lethal = new Dictionary<HediffDef, bool>();

        private static bool Lethal(HediffDef d)
        {
            if (lethal.TryGetValue(d, out bool l)) return l;
            l = d.lethalSeverity > 0f;
            if (!l && d.stages != null)
                for (int i = 0; i < d.stages.Count; i++)
                    if (d.stages[i] != null && d.stages[i].lifeThreatening)
                        l = true;
            lethal[d] = l;
            return l;
        }

        /// <summary>
        /// A condition the rule has a word on: a wound that heals, or an illness, ailment or lasting condition that shows and
        /// is tended, builds immunity or is one of Rimfolk's. Not a scar, a lost part, an implant, a mood or a drug.
        /// </summary>
        public static bool Considered(Hediff h)
        {
            if (h == null || !h.Visible || h is Hediff_MissingPart || h is Hediff_AddedPart) return false;
            if (h is Hediff_Injury) return !h.IsPermanent() && h.Severity > 0f;
            var d = h.def;
            if (d == null) return false;
            return d.tendable || d.PossibleToDevelopImmunityNaturally() || h.TryGetComp<HediffComp_Ailment>() != null;
        }

        /// <summary>The healing wounds' pain, as the game feels it, and the patient's moving and manipulation now.</summary>
        public static BedRestMath.Body BodyOf(Pawn p)
        {
            var b = new BedRestMath.Body { moving = 1f, manipulation = 1f };
            var hs = p.health.hediffSet.hediffs;
            float sum = 0f, factor = 1f;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h is Hediff_Injury && !h.IsPermanent()) sum += h.PainOffset;
                factor *= h.PainFactor;
            }
            if (sum > 0f)
            {
                if (p.genes != null) factor *= p.genes.PainFactor;
                var traits = p.story?.traits?.allTraits;
                if (traits != null)
                    for (int i = 0; i < traits.Count; i++)
                        factor *= traits[i].CurrentData?.painFactor ?? 1f;
            }
            b.woundPain = Mathf.Clamp01(sum * factor);
            var caps = p.health.capacities;
            if (caps != null)
            {
                if (PawnCapacityDefOf.Moving != null) b.moving = caps.GetLevel(PawnCapacityDefOf.Moving);
                if (PawnCapacityDefOf.Manipulation != null) b.manipulation = caps.GetLevel(PawnCapacityDefOf.Manipulation);
            }
            return b;
        }

        /// <summary>What the rule reads of one condition.</summary>
        public static BedRestMath.Condition ConditionOf(Hediff h, BedRestMath.Body body)
        {
            var c = new BedRestMath.Condition { ignored = SmartMedicineBridge.Present && SmartMedicineBridge.OffBedRest(h), severity = h.Severity };
            if (h is Hediff_Injury)
            {
                c.wound = true;
                c.tended = h.IsTended();
                var f = MedicineChoice.FactsOf(h);
                f.bestByAnswer = false;
                c.threat = MedicineChoiceMath.Judge(f, out c.threatWhy);
                var tags = h.Part?.def?.tags;
                if (tags != null)
                    for (int i = 0; i < tags.Count; i++)
                    {
                        var t = tags[i];
                        if (t == null) continue;
                        if (t == BodyPartTagDefOf.MovingLimbCore || t == BodyPartTagDefOf.MovingLimbSegment || t == BodyPartTagDefOf.MovingLimbDigit
                            || t == BodyPartTagDefOf.Pelvis || t == BodyPartTagDefOf.Spine)
                            c.onLegs = true;
                        if (t == BodyPartTagDefOf.ManipulationLimbCore || t == BodyPartTagDefOf.ManipulationLimbSegment || t == BodyPartTagDefOf.ManipulationLimbDigit)
                            c.onArms = true;
                    }
                return c;
            }
            c.illness = true;
            c.immunizable = h.def.PossibleToDevelopImmunityNaturally();
            c.immune = c.immunizable && h.FullyImmune();
            c.lethal = Lethal(h.def);
            c.stage = StageOf(h);
            var ail = h.TryGetComp<HediffComp_Ailment>()?.Props;
            if (ail != null)
            {
                c.rimfolk = true;
                c.restFrom = ail.restFrom;
            }
            return c;
        }

        /// <summary>What a condition's stage takes off consciousness, moving and breathing, its pain, and how fast it tires.</summary>
        public static BedRestMath.Stage StageOf(Hediff h)
        {
            var cur = h.CurStage;
            var st = new BedRestMath.Stage { pain = h.PainOffset, restFall = cur != null ? cur.restFallFactor + cur.restFallFactorOffset : 1f };
            var mods = cur?.capMods;
            if (mods == null) return st;
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m?.capacity == null) continue;
                float d = BedRestMath.Drop(m.offset, m.postFactor, m.setMax);
                if (m.capacity == PawnCapacityDefOf.Consciousness) st.consciousness = Mathf.Max(st.consciousness, d);
                else if (m.capacity == PawnCapacityDefOf.Moving) st.moving = Mathf.Max(st.moving, d);
                else if (m.capacity == PawnCapacityDefOf.Breathing) st.breathing = Mathf.Max(st.breathing, d);
            }
            return st;
        }

        // ================= Shown =================

        /// <summary>
        /// The line under a condition's tooltip on the health tab: whether it keeps them working or sends them to bed, and why
        /// (null for none: the setting off, someone not of the rule, a scar or an implant).
        /// </summary>
        public static string LineFor(Hediff h)
        {
            var p = h?.pawn;
            if (!On || p?.health?.hediffSet == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike || Exempt(p) || !Considered(h)) return null;
            var state = Why(p, out var because);
            var body = BodyOf(p);
            var c = ConditionOf(h, body);
            if (c.ignored) return "CC_Rest_Line_ByHand".Translate().Resolve();
            var own = c.rimfolk || c.wound || BedRestMath.GameCounts(c) ? BedRestMath.Own(c, body) : BedRestMath.Why.None;
            if (own == BedRestMath.Why.None && state == BedRestMath.InBed.Decree && BedRestMath.GameCounts(c)) own = BedRestMath.Why.Decree;
            if (own != BedRestMath.Why.None) return RestsLine(h, p, c, body, own);
            bool tendNow = h.TendableNow();
            // A stage that tires them: they keep working, and sleep a little more at night.
            string sleeps = BedRestMath.SleepsMore(c, body) ? "Sleeps" : "";
            if (state == BedRestMath.InBed.No)
            {
                // Something small to tend while no doctor is free: they keep working and lie down once one is.
                if (tendNow && HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(p)) return ("CC_Rest_Line_WorksTendLater" + sleeps).Translate(p.Named("PAWN")).Resolve();
                return ("CC_Rest_Line_Works" + sleeps).Translate(p.Named("PAWN")).Resolve();
            }
            if (state == BedRestMath.InBed.Tending && tendNow) return ("CC_Rest_Line_WorksTend" + sleeps).Translate(p.Named("PAWN")).Resolve();
            // In bed for something else: this one alone wouldn't keep them there.
            string why;
            if (state == BedRestMath.InBed.Condition)
                why = because != null && because != h
                    ? "CC_Rest_InBed_Condition".Translate(p.Named("PAWN"), because.LabelBase.Named("OTHER")).Resolve()
                    : "CC_Rest_InBed_Other".Translate(p.Named("PAWN")).Resolve();
            else
                why = ("CC_Rest_InBed_" + state).Translate(p.Named("PAWN")).Resolve();
            return "CC_Rest_Line_WorksBut".Translate(p.Named("PAWN"), why.Named("WHY")).Resolve();
        }

        private static string RestsLine(Hediff h, Pawn p, BedRestMath.Condition c, BedRestMath.Body body, BedRestMath.Why why)
        {
            string stage = (h.CurStage?.label).NullOrEmpty() ? h.LabelBase : h.CurStage.label;
            switch (why)
            {
                case BedRestMath.Why.SeriousWound:
                {
                    string key = "CC_Rest_Wound_" + c.threatWhy;
                    string text = key.CanTranslate() ? key.Translate(p.Named("PAWN"), (h.Part?.Label ?? "").Named("PART")).Resolve() : "CC_Rest_Wound_Other".Translate().Resolve();
                    return "CC_Rest_Line_Heals".Translate(text.Named("WHY")).Resolve();
                }
                case BedRestMath.Why.WoundsHurt:
                    return "CC_Rest_Line_Heals".Translate("CC_Rest_Why_WoundsHurt".Translate(p.Named("PAWN"), body.woundPain.ToStringPercent().Named("AMOUNT")).Resolve().Named("WHY")).Resolve();
                case BedRestMath.Why.WoundsLame:
                    return "CC_Rest_Line_Heals".Translate("CC_Rest_Why_WoundsLame".Translate(p.Named("PAWN"), body.moving.ToStringPercent().Named("AMOUNT")).Resolve().Named("WHY")).Resolve();
                case BedRestMath.Why.WoundsClumsy:
                    return "CC_Rest_Line_Heals".Translate("CC_Rest_Why_WoundsClumsy".Translate(p.Named("PAWN"), body.manipulation.ToStringPercent().Named("AMOUNT")).Resolve().Named("WHY")).Resolve();
                case BedRestMath.Why.Dazed:
                case BedRestMath.Why.Unsteady:
                case BedRestMath.Why.Breathless:
                case BedRestMath.Why.Pain:
                {
                    var st = c.stage;
                    float amount = why == BedRestMath.Why.Dazed ? st.consciousness : why == BedRestMath.Why.Unsteady ? st.moving : why == BedRestMath.Why.Breathless ? st.breathing : st.pain;
                    string text = ("CC_Rest_Why_" + why).Translate(p.Named("PAWN"), amount.ToStringPercent().Named("AMOUNT")).Resolve();
                    return "CC_Rest_Line_Stage".Translate(stage.Named("STAGE"), text.Named("WHY")).Resolve();
                }
                case BedRestMath.Why.Stage:
                    return "CC_Rest_Line_Stage".Translate(stage.Named("STAGE"), "CC_Rest_Why_Stage".Translate(p.Named("PAWN")).Resolve().Named("WHY")).Resolve();
                default:
                    return "CC_Rest_Line_Rests".Translate(("CC_Rest_Why_" + why).Translate(p.Named("PAWN")).Resolve().Named("WHY")).Resolve();
            }
        }

        private static string resting;

        /// <summary>
        /// A resting colonist's activity ("resting.") with what they rest for, when a condition has them in bed by the rule
        /// ("resting: flu."); otherwise the game's own words. Null for no change.
        /// </summary>
        public static string ReportFor(Pawn p, string report)
        {
            if (!On || p == null || report.NullOrEmpty() || !p.InBed()) return null;
            resting ??= "ReportResting".Translate().Resolve();
            if (report != resting) return null;
            if (Why(p, out var because) != BedRestMath.InBed.Condition || because == null) return null;
            return "CC_Rest_Report".Translate(because.LabelBase.Named("WHAT")).Resolve();
        }

        /// <summary>For the dev log: each condition the rule looks at, what it says of it, and what has them in bed.</summary>
        public static string Explain(Pawn p)
        {
            var sb = new StringBuilder();
            bool game = GameRest(p);
            var state = Why(p, out var because);
            sb.Append(p.LabelShort).Append(": the game says ").Append(game ? "rest" : "no rest").Append(", the rule ")
                .Append(state == BedRestMath.InBed.No ? "keeps working" : "in bed (" + state + (because != null ? ": " + because.LabelBase : "") + ")")
                .Append(On ? "" : " (the rule is off: the setting, the deeper hooks, or the recovery notice not found)");
            var body = BodyOf(p);
            sb.Append("; wounds' pain ").Append(body.woundPain.ToStringPercent()).Append(", moving ").Append(body.moving.ToStringPercent())
                .Append(", manipulation ").Append(body.manipulation.ToStringPercent()).Append('\n');
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!Considered(h)) continue;
                var c = ConditionOf(h, body);
                var own = BedRestMath.Own(c, body);
                var sends = BedRestMath.Sends(c, body);
                sb.Append("  ").Append(h.LabelCap).Append(h.Part != null ? " (" + h.Part.Label + ")" : "").Append(": ")
                    .Append(sends != BedRestMath.Why.None ? "rests (" + sends + ")" : own != BedRestMath.Why.None ? "would rest (" + own + "), but the game doesn't count it" : "keeps working")
                    .Append(BedRestMath.GameCounts(c) ? "; the game's own rest counts it" : "")
                    .Append(c.wound ? "; " + c.threat + " (" + c.threatWhy + ")" + (c.onLegs ? ", on a leg" : "") + (c.onArms ? ", on an arm" : "") : "")
                    .Append(c.rimfolk ? "; Rimfolk's" + (c.restFrom > 0f ? ", rests from " + c.restFrom.ToString("0.##") : "") : "")
                    .Append(BedRestMath.SleepsMore(c, body) ? "; sleeps a little more (rest falls x" + c.stage.restFall.ToString("0.##") + ")" : "")
                    .Append(c.ignored ? "; off bed rest in Smart Medicine" : "")
                    .Append(h.TendableNow() ? "; to tend now" : "")
                    .Append('\n');
            }
            return sb.ToString().TrimEnd();
        }
    }
}
