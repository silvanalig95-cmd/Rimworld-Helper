using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What one colonist has done as a doctor and a midwife. Kept by ThingID in <see cref="HealthTracker"/>.</summary>
    public class HealerRecord : IExposable
    {
        /// <summary>Patients pulled through a dangerous illness or infection.</summary>
        public int pulledThrough;
        /// <summary>Major operations done well (an organ, a limb, the brain).</summary>
        public int surgeries;
        public int lost;
        /// <summary>Babies delivered alive.</summary>
        public int births;
        /// <summary>When the recent losses were (only those within the last few weeks are kept).</summary>
        public List<int> losses = new List<int>();
        /// <summary>The standing last told to the colony (HealthMath.DoctorLevel, MidwifeLevel).</summary>
        public int level;
        public int midwifeLevel;
        /// <summary>The last patient lost, and when (for the doctor who wants a day away).</summary>
        public Pawn lastLost;
        public int lastLostTick = -1;

        public int Saved => pulledThrough + surgeries;

        public int Level => HealthMath.DoctorLevel(Saved, lost);

        public int LossesWithin(int now, float days)
        {
            int n = 0;
            foreach (int t in losses)
                if (t <= now && now - t <= days * GenDate.TicksPerDay) n++;
            return n;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pulledThrough, "pulledThrough");
            Scribe_Values.Look(ref surgeries, "surgeries");
            Scribe_Values.Look(ref lost, "lost");
            Scribe_Values.Look(ref births, "births");
            Scribe_Collections.Look(ref losses, "losses", LookMode.Value);
            Scribe_Values.Look(ref level, "level");
            Scribe_Values.Look(ref midwifeLevel, "midwifeLevel");
            Scribe_References.Look(ref lastLost, "lastLost");
            Scribe_Values.Look(ref lastLostTick, "lastLostTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) losses ??= new List<int>();
        }
    }

    /// <summary>
    /// A dangerous illness or infection of the game's own (or another mod's) that a colony doctor is treating: which
    /// doctors tended it how often, and how bad it got. When it ends and the patient lives, whoever tended it most pulled
    /// them through. Rimfolk's own serious illnesses are told by the AilmentTracker instead.
    /// </summary>
    public class HealthCase : IExposable
    {
        public Pawn patient;
        public int hediffId;
        public string label;
        public float peak;
        public Dictionary<string, int> tends = new Dictionary<string, int>();
        private List<string> tmpKeys;
        private List<int> tmpValues;

        public int TendCount => tends.Values.Sum();

        public string TopDoctor => tends.Count == 0 ? null : tends.OrderByDescending(kv => kv.Value).First().Key;

        public void ExposeData()
        {
            Scribe_References.Look(ref patient, "patient");
            Scribe_Values.Look(ref hediffId, "hediffId");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref peak, "peak");
            Scribe_Collections.Look(ref tends, "tends", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) tends ??= new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Health's human side, in a game component of its own (the game finds it by itself):
    /// <list type="bullet">
    /// <item>Convalescence: after a serious illness, a major operation, a long stay in bed or losing a limb, a colonist
    /// is weak for days to a couple of weeks (the ailments' "convalescing", a little slower at work and on their feet). A
    /// quiet mood comes with it, glad to be alive or, for the impatient, weak and frustrated. Friends who sit by the bed
    /// lift both their moods. Working while still weak can set it back, which only makes it last longer.</item>
    /// <item>Reputation: each doctor's patients pulled through (dangerous illnesses, major operations) and lost, and each
    /// midwife's births. Doctors and midwives earn names and the colony's trust (patients in bed rest easier under a
    /// trusted doctor); a run of losses weighs on the doctor.</item>
    /// </list>
    /// The game's events reach it through Patches/HealthPatches.cs; it looks at the colony once an hour. Each half has
    /// its own switch, and off, it does nothing new.
    /// </summary>
    public class HealthTracker : GameComponent
    {
        // ---------------- Saved ----------------

        private Dictionary<string, HealerRecord> healers = new Dictionary<string, HealerRecord>();
        private List<HealthCase> cases = new List<HealthCase>();
        /// <summary>Patient ThingID: the colony doctor who last tended them, and when.</summary>
        private Dictionary<string, string> lastDoctor = new Dictionary<string, string>();
        private Dictionary<string, int> lastTend = new Dictionary<string, int>();
        /// <summary>Colonist ThingID: hours laid up in bed during this stretch of needing medical rest.</summary>
        private Dictionary<string, int> bedHours = new Dictionary<string, int>();
        /// <summary>Colonists looked at before: what they had missing when they joined isn't news.</summary>
        private HashSet<string> known = new HashSet<string>();
        /// <summary>Missing parts (hediff load IDs) already noticed.</summary>
        private HashSet<int> partsSeen = new HashSet<int>();
        /// <summary>Convalescence hediff (load ID): setbacks it has had.</summary>
        private Dictionary<int, int> setbacks = new Dictionary<int, int>();
        private Dictionary<string, int> lastSetback = new Dictionary<string, int>();
        /// <summary>Colonists convalescing at the last look (to notice when they're back to full strength).</summary>
        private HashSet<string> convalescing = new HashSet<string>();
        /// <summary>"visitor|patient": when the visitor last sat with the patient.</summary>
        private Dictionary<string, int> visits = new Dictionary<string, int>();
        /// <summary>Patient ThingID: when they last wrote about trusting their doctor.</summary>
        private Dictionary<string, int> lastTrustLine = new Dictionary<string, int>();

        private List<string> tk1, tk2, tk3, tk4, tk5, tk6, tk7;
        private List<HealerRecord> tv1;
        private List<string> tv2;
        private List<int> tv3, tv4, tv5, tv6, tv7, tk8, tv8;

        // ---------------- Not saved ----------------

        /// <summary>Things to do on the next tick, out of the middle of whatever the game was doing.</summary>
        private static readonly List<Action> later = new List<Action>();

        /// <summary>The patient on the table right now (a death there is the operation's, not an illness's).</summary>
        public static Pawn underKnife;

        private static HealthTracker cached;
        private static Game cachedGame;

        public static HealthTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<HealthTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public HealthTracker(Game game)
        {
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Convalescence after illness, operations, bed rest and lost limbs; visits; setbacks.</summary>
        public static bool ConvalescenceOn => S != null && S.convalescence;

        /// <summary>Doctors' and midwives' reputations, and what comes of them.</summary>
        public static bool ReputationOn => S != null && S.healerReputations;

        private static bool HooksOn => RimfolkPatches.On;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            later.Clear();
            underKnife = null;
        }

        // ---------------- Who ----------------

        /// <summary>One of the colony's own people with an inner life (colonists and slaves), not a baby.</summary>
        public static bool Ours(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsPrisoner
            && !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby();

        /// <summary>Impatient by nature: industrious, a hard worker (or a workaholic from a traits mod).</summary>
        public static bool Impatient(Pawn p)
        {
            var traits = p?.story?.traits;
            if (traits == null) return false;
            if (traits.HasTrait(TraitDefOf.Industriousness) && traits.DegreeOfTrait(TraitDefOf.Industriousness) >= 1) return true;
            return PeopleTags.Has(p, "trait:VTE_Workaholic");
        }

        public static Hediff ConvalescenceOf(Pawn p) => p?.health?.hediffSet?.GetFirstHediffOfDef(HealthDefOf.Council_Ail_Convalescent);

        public HealerRecord RecordOf(Pawn p, bool create = false)
        {
            if (p == null) return null;
            if (healers.TryGetValue(p.ThingID, out var r)) return r;
            if (!create) return null;
            r = new HealerRecord();
            healers[p.ThingID] = r;
            return r;
        }

        /// <summary>The colony's most experienced midwife at home with the colonist, or null.</summary>
        public Pawn MidwifeNear(Pawn p, Pawn not = null)
        {
            var map = p?.MapHeld;
            if (map == null) return null;
            Pawn best = null;
            int most = 0;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c == p || c == not) continue;
                int n = RecordOf(c)?.births ?? 0;
                if (n > most)
                {
                    most = n;
                    best = c;
                }
            }
            return best;
        }

        // ---------------- Ticking ----------------

        public override void GameComponentTick()
        {
            if (later.Count > 0) RunLater();
            int now = Find.TickManager.TicksGame;
            // Each colonist's hour in a tenth of the colony every 250 ticks (Sched), so each comes round once an hour and no
            // one tick carries the whole colony; the colony's part (the cases, the day's pruning) on its own tick of the hour.
            int slice = Sched.SliceTurn(now, Sched.HourTicks, Sched.Slices, Sched.RareHealth);
            bool colony = Sched.Hourly(now, Sched.Health);
            if (slice < 0 && !colony) return;
            if (!ConvalescenceOn && !ReputationOn) return;
            try
            {
                if (slice >= 0) Slice(slice, now);
                if (colony) Colony(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health stories", e);
            }
        }

        public static void Later(Action a)
        {
            if (a != null) later.Add(a);
        }

        private static void RunLater()
        {
            var list = later.ToList();
            later.Clear();
            foreach (var a in list)
            {
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("health stories (later)", e);
                }
            }
        }

        private static readonly int PawnErrorKey = "ColonyCouncilhealth of a colonist".GetHashCode();

        private readonly List<Pawn> sliceTmp = new List<Pawn>();

        private void Slice(int slice, int now)
        {
            // This slice's colonists, copied out of the game's shared list before anything is done to them.
            sliceTmp.Clear();
            var everyone = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
            for (int i = 0; i < everyone.Count; i++)
            {
                var p = everyone[i];
                if (p != null && Sched.SliceOf(p.thingIDNumber, Sched.Slices) == slice) sliceTmp.Add(p);
            }
            for (int i = 0; i < sliceTmp.Count; i++)
            {
                var p = sliceTmp[i];
                if (!AilmentTracker.Eligible(p)) continue;
                try
                {
                    Hour(p, now);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Rimfolk] health stories of " + p.LabelShort + ": " + e, PawnErrorKey);
                }
            }
            sliceTmp.Clear();
        }

        private void Colony(int now)
        {
            Cases(now);
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Prune(now);
        }

        private void Hour(Pawn p, int now)
        {
            string id = p.ThingID;
            bool firstLook = known.Add(id);
            if (ConvalescenceOn)
            {
                Limbs(p, firstLook);
                BedRest(p, id);
                Setback(p, id, now);
            }
            else if (firstLook) Limbs(p, true);
            var conv = ConvalescenceOf(p);
            if (conv != null) convalescing.Add(id);
            else if (convalescing.Remove(id))
            {
                // Back on their feet: a story waiting on them moves on (Interplay/).
                Interplay.Notify_Recovered(p);
                if (ConvalescenceOn && People.HasInnerLife(p) && Rand.Chance(0.4f)) Journal(p, "Council_Journal_Health_Strong");
            }
        }

        // ---------------- Convalescence ----------------

        /// <summary>
        /// They start getting their strength back (or, convalescing already, the larger of the two counts). The first time,
        /// a quiet mood: glad to be alive, unless they're the impatient kind, who are weak and frustrated instead for as
        /// long as it lasts (a mood of its own, ThoughtWorker_ConvalescentFrustrated).
        /// </summary>
        public void Convalesce(Pawn p, float severity, string source, Pawn other = null)
        {
            if (!ConvalescenceOn || severity <= 0f || !AilmentTracker.Eligible(p) || p.Dead) return;
            var def = HealthDefOf.Council_Ail_Convalescent;
            var h = ConvalescenceOf(p);
            bool fresh = h == null;
            if (fresh)
            {
                h = HediffMaker.MakeHediff(def, p);
                h.Severity = Mathf.Min(severity, HealthMath.MaxSeverity);
                p.health.AddHediff(h);
                if (!p.health.hediffSet.hediffs.Contains(h)) return;
            }
            else
            {
                if (h.Severity >= severity) return;
                h.Severity = HealthMath.Combine(h.Severity, severity);
            }
            if (!People.HasInnerLife(p)) return;
            bool impatient = Impatient(p);
            if (fresh && !impatient && !RecentlyGlad(p))
                CouncilGameComponent.GiveThought(p, HealthDefOf.Council_Health_GladToBeAlive, null, -1f, ("CC_Health_Cause_" + source).Translate().Resolve());
            if (fresh)
            {
                News.Post(("CC_Health_Convalescing_" + source).Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), NewsKind.Health, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                if (Rand.Chance(0.6f))
                    JournalTagged(p, "Council_Journal_Health_Convalescing", new[] { source.ToLowerInvariant(), impatient ? "impatient" : "patient" }, source == "Surgery" ? other : null);
            }
        }

        /// <summary>Glad to be alive already, or over the worst of a serious illness, within the last two days.</summary>
        private static bool RecentlyGlad(Pawn p) =>
            LifeConditions.Remembered(p, GladThoughts, 2f) != null;

        private static readonly List<string> GladThoughts = new List<string> { "Council_Health_GladToBeAlive", "Council_Ail_Survived" };

        /// <summary>
        /// Days laid up in bed: counted while they need medical rest and lie in a bed (a trip to the table doesn't break
        /// the count); once they no longer need it, a long enough stretch leaves them convalescing.
        /// </summary>
        private void BedRest(Pawn p, string id)
        {
            if (!p.Spawned) return;
            bool needsRest;
            try
            {
                needsRest = p.Downed || HealthAIUtility.ShouldSeekMedicalRest(p);
            }
            catch
            {
                needsRest = false;
            }
            if (needsRest)
            {
                if (p.InBed()) bedHours[id] = (bedHours.TryGetValue(id, out int h) ? h : 0) + 1;
                return;
            }
            if (!bedHours.TryGetValue(id, out int hours)) return;
            bedHours.Remove(id);
            float days = hours / 24f;
            float severity = HealthMath.AfterBedRest(days);
            if (severity > 0f) Convalesce(p, severity, "Bed");
        }

        /// <summary>A limb (or another large outer part) newly lost. Missing parts they had when first looked at aren't news.</summary>
        private void Limbs(Pawn p, bool firstLook)
        {
            List<Hediff_MissingPart> missing;
            try
            {
                missing = p.health.hediffSet.GetMissingPartsCommonAncestors();
            }
            catch
            {
                return;
            }
            if (missing == null) return;
            foreach (var m in missing)
            {
                if (m?.Part == null || !partsSeen.Add(m.loadID) || firstLook) continue;
                if (m.Part.depth == BodyPartDepth.Inside || m.Part.coverageAbsWithChildren < HealthMath.MajorCoverage) continue;
                // Lost while this was switched off: old news by now.
                if (m.ageTicks > GenDate.TicksPerDay) continue;
                Convalesce(p, HealthMath.AfterLimbLost(m.Part.coverageAbsWithChildren), "Limb");
            }
        }

        /// <summary>
        /// Up too early: working while still weak can set the recovery back (now and then, at most twice, never two days
        /// running). A setback only makes it last longer.
        /// </summary>
        private void Setback(Pawn p, string id, int now)
        {
            var h = ConvalescenceOf(p);
            if (h == null || h.Severity < HealthMath.WeakFrom || !p.Spawned || p.InBed() || p.Downed || !RestUtility.Awake(p)) return;
            if (p.CurJob?.workGiverDef == null) return;
            int count = setbacks.TryGetValue(h.loadID, out int c) ? c : 0;
            if (count >= HealthMath.MaxSetbacks) return;
            if (lastSetback.TryGetValue(id, out int last) && last <= now && now - last < HealthMath.SetbackGapDays * GenDate.TicksPerDay) return;
            if (!Rand.Chance(HealthMath.SetbackChancePerHour)) return;
            h.Severity = HealthMath.AfterSetback(h.Severity);
            setbacks[h.loadID] = count + 1;
            lastSetback[id] = now;
            string work = p.CurJob.workGiverDef.workType?.gerundLabel ?? p.CurJob.workGiverDef.label ?? "";
            News.Post("CC_Health_Setback".Translate(p.Named("PAWN"), work.Named("WORK")).Resolve(), new LookTargets(p),
                NewsKind.Health, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (People.HasInnerLife(p)) JournalTagged(p, "Council_Journal_Health_Setback", Impatient(p) ? new[] { "impatient" } : null);
        }

        // ---------------- Visits ----------------

        /// <summary>When this visitor last sat with this patient (ticks), or -1.</summary>
        public int LastVisit(Pawn visitor, Pawn patient) =>
            visitor != null && patient != null && visits.TryGetValue(visitor.ThingID + "|" + patient.ThingID, out int t) ? t : -1;

        /// <summary>
        /// A friend's visit to the sickbed ended (the game's own visit, JobDriver_VisitSickPawn): long enough to count, it
        /// lifts the patient (being visited while ill) and the friend (sat with a friend). Once a day per pair.
        /// </summary>
        public void Notify_VisitEnded(Pawn visitor, Pawn patient, int ticks)
        {
            // together: the game's own sickbed visit (JobDriver_VisitSickPawn), at the bedside.
            if (!ConvalescenceOn || ticks < 900 || !Ours(visitor) || !Ours(patient) || visitor == patient) return;
            int now = Find.TickManager.TicksGame;
            string key = visitor.ThingID + "|" + patient.ThingID;
            bool sameDay = visits.TryGetValue(key, out int last) && last <= now && now - last < GenDate.TicksPerDay;
            visits[key] = now;
            if (sameDay) return;
            int fromPatient = patient.relations?.OpinionOf(visitor) ?? 0;
            int fromVisitor = visitor.relations?.OpinionOf(patient) ?? 0;
            if (fromPatient >= -10) CouncilGameComponent.GiveThought(patient, HealthDefOf.Council_Moment_VisitedWhileIll, visitor);
            if (fromVisitor >= 10) CouncilGameComponent.GiveThought(visitor, HealthDefOf.Council_Health_SatWithFriend, patient);
            if (fromPatient >= 0 && fromVisitor >= 0) CouncilGameComponent.Instance?.social?.Bump(patient, visitor, TieKind.Friends, 2f, "CC_TieNote_SickVisit".Translate(), true);
            if (Rand.Chance(0.3f)) JournalTagged(patient, "Council_Journal_Health_Visited", ConvalescenceOf(patient) != null ? new[] { "convalescing" } : null, visitor);
        }

        // ---------------- Doctors ----------------

        /// <summary>
        /// A doctor of the colony tended one of its people. Remembered (a death within a day counts against them), counted
        /// toward each dangerous illness or infection being treated, and a patient in bed under a trusted doctor rests easier.
        /// </summary>
        public void Notify_Tended(Pawn doctor, Pawn patient)
        {
            if (!(ReputationOn || ConvalescenceOn) || doctor == null || patient == null || doctor == patient) return;
            if (!doctor.RaceProps.Humanlike || doctor.Faction != Faction.OfPlayer || !Ours(patient)) return;
            int now = Find.TickManager.TicksGame;
            lastDoctor[patient.ThingID] = doctor.ThingID;
            lastTend[patient.ThingID] = now;
            foreach (var h in patient.health.hediffSet.hediffs)
            {
                if (!Dangerous(h)) continue;
                var c = cases.FirstOrDefault(x => x.hediffId == h.loadID && x.patient == patient);
                if (c == null)
                {
                    c = new HealthCase { patient = patient, hediffId = h.loadID, label = h.def.label, peak = h.Severity };
                    cases.Add(c);
                }
                c.tends[doctor.ThingID] = (c.tends.TryGetValue(doctor.ThingID, out int n) ? n : 0) + 1;
                c.peak = Mathf.Max(c.peak, h.Severity);
            }
            var rec = ReputationOn ? RecordOf(doctor) : null;
            // The physician's office counts toward the doctor's standing (Interplay/).
            int level = Interplay.DoctorLevel(doctor, rec);
            if (level <= 0 || !(patient.InBed() || patient.Downed)) return;
            float scale = S.rewardFactor * (level >= 2 ? 1.5f : 1f);
            CouncilGameComponent.GiveThought(patient, HealthDefOf.Council_Health_InGoodHands, null, scale, "CC_Cause_About".Translate(doctor.LabelShort).ToString());
            string pid = patient.ThingID;
            if ((!lastTrustLine.TryGetValue(pid, out int t) || now - t > GenDate.TicksPerDay * 5 || t > now) && Rand.Chance(0.3f))
            {
                lastTrustLine[pid] = now;
                JournalTagged(patient, "Council_Journal_Health_TrustDoctor", new[] { level >= 2 ? "renowned" : "trusted", "patient" }, doctor);
            }
        }

        /// <summary>
        /// A dangerous condition the game (or another mod) knows: one that can kill and that the body fights, like flu,
        /// plague, malaria or a wound infection. Rimfolk's own illnesses are the AilmentTracker's.
        /// </summary>
        public static bool Dangerous(Hediff h)
        {
            if (h?.def == null || !h.Visible || h.def.lethalSeverity <= 0f || AilmentTracker.InfoOf(h.def) != null) return false;
            return h.def.isInfection || (h is HediffWithComps hc && hc.TryGetComp<HediffComp_Immunizable>() != null);
        }

        /// <summary>The cases being treated: a worse peak, and an end. Over and survived, the doctor pulled them through.</summary>
        private void Cases(int now)
        {
            for (int i = cases.Count - 1; i >= 0; i--)
            {
                var c = cases[i];
                var p = c.patient;
                if (p == null || p.Destroyed || p.Dead || p.Faction != Faction.OfPlayer)
                {
                    cases.RemoveAt(i);
                    continue;
                }
                Hediff h = null;
                var hs = p.health?.hediffSet?.hediffs;
                if (hs != null)
                    for (int j = 0; j < hs.Count; j++)
                        if (hs[j].loadID == c.hediffId)
                        {
                            h = hs[j];
                            break;
                        }
                if (h != null)
                {
                    c.peak = Mathf.Max(c.peak, h.Severity);
                    continue;
                }
                cases.RemoveAt(i);
                if (c.peak < HealthMath.SeriousPeak) continue;
                var doctor = c.TendCount >= 2 ? ColonistById(c.TopDoctor) : null;
                try
                {
                    if (doctor != null && doctor != p) PulledThrough(doctor, p, c.label, c.peak, false);
                    else if (ConvalescenceOn) Convalesce(p, HealthMath.AfterIllness(c.peak), "Illness");
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("health: pulled through", e);
                }
            }
        }

        /// <summary>
        /// A doctor pulled a patient through. For Rimfolk's serious illnesses (<paramref name="rimfolk"/>) the
        /// AilmentTracker has already told it (thoughts, journals, the after-effects); here it only counts. For the game's
        /// own dangerous illnesses, the doctor is proud and the patient convalesces.
        /// </summary>
        public void PulledThrough(Pawn doctor, Pawn patient, string illness, float peak, bool rimfolk)
        {
            if (doctor == null || patient == null || doctor == patient) return;
            if (!rimfolk && ConvalescenceOn) Convalesce(patient, HealthMath.AfterIllness(peak), "Illness", doctor);
            if (!ReputationOn || !doctor.RaceProps.Humanlike) return;
            var rec = RecordOf(doctor, true);
            rec.pulledThrough++;
            doctor.records?.Increment(HealthDefOf.Council_PatientsPulledThrough);
            if (!rimfolk && People.HasInnerLife(doctor))
            {
                CouncilGameComponent.GiveThought(doctor, AilmentDefOf.Council_Ail_PulledThrough, null, -1f, "CC_Cause_About".Translate(patient.LabelShort).ToString());
                CouncilGameComponent.Instance?.journals?.WriteTagged(doctor, "Council_Journal_Ail_DoctorSaved", new[] { "vanilla" }, null,
                    (illness ?? "").Named("ILLNESS"), patient.Named("OTHER"));
            }
            CheckLevel(doctor, rec);
        }

        /// <summary>An operation finished on one of the colony's people (Recipe_Surgery's own check said how it went).</summary>
        public void Notify_Surgery(Pawn surgeon, Pawn patient, RecipeDef recipe, BodyPartRecord part, bool failed)
        {
            if (surgeon == null || patient == null || surgeon == patient || recipe == null) return;
            if (!surgeon.RaceProps.Humanlike || surgeon.Faction != Faction.OfPlayer || patient.Faction != Faction.OfPlayer || !patient.RaceProps.Humanlike) return;
            if (failed)
            {
                if (patient.Dead) Lost(surgeon, patient, null, true);
                return;
            }
            if (!MajorSurgery(recipe, part)) return;
            bool brain = part.def?.tags != null && part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource);
            float severity = HealthMath.AfterSurgery(brain, part.depth == BodyPartDepth.Inside, part.coverageAbsWithChildren);
            // The operation isn't done yet (the part goes in after the check): convalescing starts on the next tick.
            Later(() =>
            {
                if (patient.Dead || patient.Destroyed) return;
                if (ConvalescenceOn) Convalesce(patient, severity, "Surgery", surgeon);
                if (!ReputationOn) return;
                var rec = RecordOf(surgeon, true);
                rec.surgeries++;
                surgeon.records?.Increment(HealthDefOf.Council_PatientsPulledThrough);
                if (People.HasInnerLife(surgeon) && Rand.Chance(0.5f))
                    JournalTagged(surgeon, "Council_Journal_Health_Surgery", brain ? new[] { "brain" } : null, patient);
                if (Ours(patient) && Rand.Chance(0.35f))
                    JournalTagged(patient, "Council_Journal_Health_TrustDoctor", new[] { "surgery", rec.Level >= 1 ? "trusted" : "first" }, surgeon);
                CheckLevel(surgeon, rec);
            });
        }

        /// <summary>
        /// An operation that counts as major: a part or organ put in or taken out, or an implant, on the brain, an organ or
        /// a limb (not a finger, an eye or an ear).
        /// </summary>
        public static bool MajorSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            if (part == null || recipe == null) return false;
            var w = recipe.Worker;
            if (!(w is Recipe_InstallNaturalBodyPart || w is Recipe_InstallArtificialBodyPart || w is Recipe_RemoveBodyPart || w is Recipe_InstallImplant)) return false;
            return HealthMath.MajorPart(part.depth == BodyPartDepth.Inside, part.coverageAbsWithChildren);
        }

        /// <summary>
        /// One of the colony's people is dying of something (not killed outright): the doctor who tended them within the
        /// last day lost a patient. Called before the death; <see cref="AfterDeath"/> confirms it.
        /// </summary>
        public Pawn DoctorToBlame(Pawn p, Hediff culprit)
        {
            if (!ReputationOn || p == null || culprit == null || p == underKnife || !Ours(p)) return null;
            if (!lastTend.TryGetValue(p.ThingID, out int t) || !lastDoctor.TryGetValue(p.ThingID, out string id)) return null;
            int now = Find.TickManager.TicksGame;
            if (t > now || now - t > HealthMath.LostWithinHours * GenDate.TicksPerHour) return null;
            var doctor = ColonistById(id);
            return doctor == p ? null : doctor;
        }

        public void AfterDeath(Pawn p, Pawn doctor, Hediff culprit)
        {
            if (p == null || !p.Dead || doctor == null) return;
            cases.RemoveAll(c => c.patient == p);
            Lost(doctor, p, culprit, false);
        }

        private void Lost(Pawn doctor, Pawn patient, Hediff culprit, bool surgery)
        {
            if (!ReputationOn || doctor == null || doctor.Dead || !doctor.RaceProps.Humanlike) return;
            int now = Find.TickManager.TicksGame;
            var rec = RecordOf(doctor, true);
            rec.lost++;
            rec.losses.Add(now);
            rec.lastLost = patient;
            rec.lastLostTick = now;
            bool run = rec.LossesWithin(now, HealthMath.RunDays) >= HealthMath.RunCount;
            if (People.HasInnerLife(doctor))
            {
                string about = "CC_Cause_About".Translate(patient.LabelShort).ToString();
                CouncilGameComponent.GiveThought(doctor, run ? HealthDefOf.Council_Health_LostRun : HealthDefOf.Council_Health_LostPatient, null, -1f, about);
                // Rimfolk's own illnesses already have the doctor's line (AilmentTracker).
                bool told = culprit != null && AilmentTracker.InfoOf(culprit.def) != null;
                if (run) JournalTagged(doctor, "Council_Journal_Health_LostRun", surgery ? new[] { "surgery" } : null, patient);
                else if (!told)
                {
                    if (surgery) JournalTagged(doctor, "Council_Journal_Health_LostOnTable", null, patient);
                    else CouncilGameComponent.Instance?.journals?.WriteTagged(doctor, "Council_Journal_Ail_DoctorLost", new[] { "vanilla" }, null,
                        (culprit?.def?.label ?? "").Named("ILLNESS"), patient.Named("OTHER"));
                }
            }
            int level = rec.Level;
            if (level < rec.level) rec.level = level;
        }

        /// <summary>The colony's trust in a doctor grew: a message, the doctor's own line, and a line or two from others.</summary>
        private void CheckLevel(Pawn doctor, HealerRecord rec)
        {
            int level = rec.Level;
            if (level <= rec.level)
            {
                rec.level = Math.Min(rec.level, level);
                return;
            }
            rec.level = level;
            string key = level >= 2 ? "CC_Health_DoctorRenowned" : "CC_Health_DoctorTrusted";
            News.Post(key.Translate(doctor.Named("PAWN"), rec.Saved.Named("COUNT")).Resolve(), new LookTargets(doctor), NewsKind.Health, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            string tag = level >= 2 ? "renowned" : "trusted";
            JournalTagged(doctor, "Council_Journal_Health_Standing", new[] { tag });
            var others = (doctor.MapHeld?.mapPawns.FreeColonistsSpawned ?? new List<Pawn>())
                .Where(o => o != doctor && Ours(o) && !o.DevelopmentalStage.Juvenile() && (o.relations?.OpinionOf(doctor) ?? 0) >= 0)
                .InRandomOrder().Take(2).ToList();
            foreach (var o in others) JournalTagged(o, "Council_Journal_Health_TrustDoctor", new[] { tag, "word" }, doctor);
            if (level >= 2)
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Health_Chronicle_Renowned".Translate(doctor.Named("PAWN"), rec.Saved.Named("COUNT")).Resolve(), doctor));
        }

        // ---------------- Midwives ----------------

        /// <summary>A child was born (Biotech): the colonist who attended the birth counts it, and the mother remembers who.</summary>
        public void Notify_Birth(Thing baby, Pawn mother, Pawn doctor)
        {
            // together: the doctor delivering the baby, at the mother's side (the game's own birth).
            if (!ReputationOn || doctor == null || mother == null || doctor == mother || !Ours(doctor)) return;
            bool alive = baby is Pawn b && !b.Dead;
            if (!alive)
            {
                JournalTagged(doctor, "Council_Journal_Health_Stillborn", null, mother);
                return;
            }
            var child = (Pawn)baby;
            var rec = RecordOf(doctor, true);
            rec.births++;
            doctor.records?.Increment(HealthDefOf.Council_BirthsAttended);
            CouncilGameComponent.GiveThought(doctor, HealthDefOf.Council_Health_DeliveredBaby, null, -1f, "CC_Cause_About".Translate(child.LabelShort).ToString());
            if (Ours(mother))
            {
                CouncilGameComponent.GiveThought(mother, HealthDefOf.Council_Health_MyMidwife, doctor);
                if (Rand.Chance(0.5f)) JournalTagged(mother, "Council_Journal_Health_MotherMidwife", rec.births >= 3 ? new[] { "experienced" } : null, doctor);
            }
            var journals = CouncilGameComponent.Instance?.journals;
            journals?.WriteTagged(doctor, "Council_Journal_Health_Midwife", rec.births == 1 ? new[] { "first" } : null, null,
                child.Named("BABY"), mother.Named("OTHER"));
            int level = HealthMath.MidwifeLevel(rec.births);
            if (level > rec.midwifeLevel)
            {
                rec.midwifeLevel = level;
                News.Post("CC_Health_Midwife".Translate(doctor.Named("PAWN"), rec.births.Named("COUNT")).Resolve(), new LookTargets(doctor),
                    NewsKind.Health, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            }
        }

        // ---------------- Showing it ----------------

        /// <summary>Lines for the Story tab: their record as a doctor or a midwife, and how long they'll be convalescing.</summary>
        public static IEnumerable<string> StoryLines(Pawn p)
        {
            var t = Instance;
            if (t == null || p == null) yield break;
            var rec = ReputationOn ? t.RecordOf(p) : null;
            if (rec != null && rec.Saved + rec.lost > 0)
            {
                yield return "CC_Health_Story_Doctor".Translate(rec.Saved.Named("SAVED"), rec.surgeries.Named("SURGERIES"), rec.lost.Named("LOST")).Resolve();
                int level = rec.Level;
                if (level > 0) yield return (level >= 2 ? "CC_Health_Story_Renowned" : "CC_Health_Story_Trusted").Translate(p.Named("PAWN")).Resolve();
            }
            if (rec != null && rec.births > 0) yield return "CC_Health_Story_Midwife".Translate(rec.births.Named("COUNT")).Resolve();
            var h = ConvalescenceOn ? ConvalescenceOf(p) : null;
            if (h != null)
            {
                string days = HealthMath.DaysUp(h.Severity).ToString("0");
                string bed = HealthMath.DaysInBed(h.Severity).ToString("0");
                yield return "CC_Health_Story_Convalescing".Translate(days.Named("DAYS"), bed.Named("BEDDAYS")).Resolve();
            }
        }

        // ---------------- Helpers ----------------

        private static void Journal(Pawn p, string pool) => JournalTagged(p, pool, null);

        private static void JournalTagged(Pawn p, string pool, string[] tags, Pawn other = null)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null || p == null || p.Faction != Faction.OfPlayer) return;
            if (other != null) journals.WriteTagged(p, pool, tags, null, other.Named("OTHER"));
            else journals.WriteTagged(p, pool, tags, null);
        }

        private static Pawn ColonistById(string id)
        {
            if (id.NullOrEmpty()) return null;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
                if (p.ThingID == id)
                    return p;
            return null;
        }

        // ---------------- Saving ----------------

        private void Prune(int now)
        {
            try
            {
                var ids = CouncilGameComponent.KnownPawnIds();
                foreach (var k in healers.Keys.Where(k => !ids.Contains(k)).ToList()) healers.Remove(k);
                foreach (var r in healers.Values)
                {
                    r.losses.RemoveAll(t => t > now || now - t > GenDate.TicksPerDay * 30);
                    // Only the last few days' loss matters (a dilemma about it), and a dead pawn may not be kept for long.
                    if (r.lastLost != null && (r.lastLost.Discarded || now - r.lastLostTick > GenDate.TicksPerDay * 5)) r.lastLost = null;
                }
                foreach (var k in lastDoctor.Keys.Where(k => !ids.Contains(k)).ToList()) lastDoctor.Remove(k);
                foreach (var k in lastTend.Keys.Where(k => !ids.Contains(k) || now - lastTend[k] > GenDate.TicksPerDay * 3).ToList())
                {
                    lastTend.Remove(k);
                    lastDoctor.Remove(k);
                }
                foreach (var k in bedHours.Keys.Where(k => !ids.Contains(k)).ToList()) bedHours.Remove(k);
                known.RemoveWhere(k => !ids.Contains(k));
                convalescing.RemoveWhere(k => !ids.Contains(k));
                foreach (var k in lastSetback.Keys.Where(k => !ids.Contains(k) || now - lastSetback[k] > GenDate.TicksPerDay * 20).ToList()) lastSetback.Remove(k);
                foreach (var k in lastTrustLine.Keys.Where(k => !ids.Contains(k) || now - lastTrustLine[k] > GenDate.TicksPerDay * 20).ToList()) lastTrustLine.Remove(k);
                visits.RemoveAll(kv => kv.Value > now || now - kv.Value > GenDate.TicksPerDay * 30);
                CouncilGameComponent.PruneByPawn(visits);
                var live = new HashSet<int>();
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
                {
                    var hs = p?.health?.hediffSet?.hediffs;
                    if (hs == null) continue;
                    foreach (var h in hs) live.Add(h.loadID);
                }
                partsSeen.RemoveWhere(id => !live.Contains(id));
                foreach (var k in setbacks.Keys.Where(k => !live.Contains(k)).ToList()) setbacks.Remove(k);
                cases.RemoveAll(c => c.patient == null || c.patient.Dead || c.patient.Destroyed);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health stories clean-up", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Prune(Find.TickManager.TicksGame);
            Scribe_Collections.Look(ref healers, "healers", LookMode.Value, LookMode.Deep, ref tk1, ref tv1);
            Scribe_Collections.Look(ref cases, "cases", LookMode.Deep);
            Scribe_Collections.Look(ref lastDoctor, "lastDoctor", LookMode.Value, LookMode.Value, ref tk2, ref tv2);
            Scribe_Collections.Look(ref lastTend, "lastTend", LookMode.Value, LookMode.Value, ref tk3, ref tv3);
            Scribe_Collections.Look(ref bedHours, "bedHours", LookMode.Value, LookMode.Value, ref tk4, ref tv4);
            Scribe_Collections.Look(ref known, "known", LookMode.Value);
            Scribe_Collections.Look(ref partsSeen, "partsSeen", LookMode.Value);
            Scribe_Collections.Look(ref setbacks, "setbacks", LookMode.Value, LookMode.Value, ref tk8, ref tv8);
            Scribe_Collections.Look(ref lastSetback, "lastSetback", LookMode.Value, LookMode.Value, ref tk5, ref tv5);
            Scribe_Collections.Look(ref convalescing, "convalescing", LookMode.Value);
            Scribe_Collections.Look(ref visits, "visits", LookMode.Value, LookMode.Value, ref tk6, ref tv6);
            Scribe_Collections.Look(ref lastTrustLine, "lastTrustLine", LookMode.Value, LookMode.Value, ref tk7, ref tv7);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                healers ??= new Dictionary<string, HealerRecord>();
                cases ??= new List<HealthCase>();
                cases.RemoveAll(c => c == null || c.patient == null);
                lastDoctor ??= new Dictionary<string, string>();
                lastTend ??= new Dictionary<string, int>();
                bedHours ??= new Dictionary<string, int>();
                known ??= new HashSet<string>();
                partsSeen ??= new HashSet<int>();
                setbacks ??= new Dictionary<int, int>();
                lastSetback ??= new Dictionary<string, int>();
                convalescing ??= new HashSet<string>();
                visits ??= new Dictionary<string, int>();
                lastTrustLine ??= new Dictionary<string, int>();
            }
        }
    }

    [DefOf]
    public static class HealthDefOf
    {
        public static HediffDef Council_Ail_Convalescent;
        public static ThoughtDef Council_Health_GladToBeAlive;
        public static ThoughtDef Council_Health_SatWithFriend;
        public static ThoughtDef Council_Health_InGoodHands;
        public static ThoughtDef Council_Health_LostPatient;
        public static ThoughtDef Council_Health_LostRun;
        public static ThoughtDef Council_Health_DeliveredBaby;
        public static ThoughtDef Council_Health_MyMidwife;
        public static ThoughtDef Council_Moment_VisitedWhileIll;
        public static RecordDef Council_PatientsPulledThrough;
        public static RecordDef Council_BirthsAttended;

        static HealthDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HealthDefOf));
        }
    }
}
