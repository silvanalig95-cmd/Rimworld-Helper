using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their envoy at the gate under their sign of parley (Truces.cs; docs/design/stage2-diplomacy.md, item 2): an envoy of
    /// theirs and a guard walk in from the edge to the colony's gate, vouched for by a people at peace with both (their
    /// guests while they are here, so neither side's fighters raise a hand to them), and wait there. A colonist sworn to
    /// vengeance on them may first stand up before the council (TruceObjection.cs). Then the colony's speaker (the Speaker,
    /// else whoever is best with people) walks out to them, with the colony's silver when that is the price, and they swear
    /// it face to face (JobDriver_Parley). Sworn, they go home; nobody comes out within the day, they go home without it; hurt
    /// by the colony, they run, and the colony has broken its word. The lord is the game's own (saved with the map).
    /// </summary>
    public static class TruceParley
    {
        public enum Sent
        {
            /// <summary>Nobody could come now (no way in): asked again later.</summary>
            NotNow,
            /// <summary>The envoy walks in.</summary>
            Walking,
            /// <summary>No people at peace with both to vouch for an envoy: sworn by word instead.</summary>
            NoEscort
        }

        /// <summary>The parley's lord on the home for this truce, or null.</summary>
        public static Lord LordOf(Tie t, Map home)
        {
            if (t == null || home?.lordManager == null) return null;
            foreach (var l in home.lordManager.lords)
                if (l?.LordJob is LordJob_Parley p && p.tie == t.id)
                    return l;
            return null;
        }

        /// <summary>The envoy of a parley (its first person still up), or null.</summary>
        public static Pawn Envoy(Lord l) => l?.ownedPawns.FirstOrDefault(p => p != null && p.Spawned && !p.Dead && !p.Downed);

        /// <summary>A people at peace with the colony and with them, whose guests the envoy and guard are while here.</summary>
        public static Faction Escort(Faction f)
        {
            return Find.FactionManager.AllFactionsListForReading
                .Where(o => o != null && o != f && !o.IsPlayer && !o.Hidden && !o.defeated && !o.temporary && o.def != null && o.def.humanlikeFaction
                            && !o.HostileTo(Faction.OfPlayer) && !o.HostileTo(f))
                .OrderByDescending(o => o.HasGoodwill ? o.PlayerGoodwill : 0).FirstOrDefault();
        }

        /// <summary>Their envoy and a guard walk in from the edge toward the gate.</summary>
        public static Sent Send(Tie t, Faction f, Map home)
        {
            var escort = Escort(f);
            if (escort == null) return Sent.NoEscort;
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return Sent.NoEscort;
            if (!Arrivals.TryEntryCell(home, out var entry)) return Sent.NotNow;
            var gate = DropCellFinder.TradeDropSpot(home);
            if (!gate.IsValid) return Sent.NotNow;
            var pawns = new List<Pawn>();
            try
            {
                pawns.Add(Arrivals.Make(kind, f));
                var guard = GuardKind(f);
                if (guard != null) pawns.Add(Arrivals.Make(guard, f));
                foreach (var p in pawns)
                {
                    GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, home, 3), home);
                    // Vouched for by a people at peace with both: their guests while they are here.
                    p.guest?.SetGuestStatus(escort);
                }
                LordMaker.MakeNewLord(f, new LordJob_Parley(t.id, gate, escort), home, pawns);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: the envoy sent", e);
                foreach (var p in pawns)
                    if (p.Spawned) p.DeSpawn();
                foreach (var p in pawns) Arrivals.Discard(p);
                return Sent.NotNow;
            }
            var form = PactWords.TruceForm(home, f);
            // apart: their envoy walks in toward the gate; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Important, home, PactWords.T("CC_Truce_Envoy_Comes", PactWords.People(f).Named("PEOPLE"), form.rite.Named("RITE"),
                PactWords.People(escort).Named("ESCORT")), new LookTargets(pawns[0]), MessageTypeDefOf.NeutralEvent);
            return Sent.Walking;
        }

        /// <summary>One of their lighter fighters to guard the envoy, or none.</summary>
        private static PawnKindDef GuardKind(Faction f)
        {
            var opts = f?.def?.pawnGroupMakers?.Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true && k.combatPower <= 90f).ToList();
            return opts == null || opts.Count == 0 ? null : opts.RandomElement();
        }

        // ================= At the gate =================

        /// <summary>Those whose objection was heard for a truce (this session): the speaker may go out.</summary>
        private static readonly HashSet<int> heard = new HashSet<int>();

        /// <summary>The council answered an avenger's objection to swear it all the same (kept on their parley too, so a reload doesn't ask again).</summary>
        internal static void Allow(int tie)
        {
            heard.Add(tie);
            var t = Truces.Get(tie);
            if (t != null && LordOf(t, Truces.HomeOf(t))?.LordJob is LordJob_Parley p) p.heard = true;
        }

        /// <summary>
        /// The hour, while their envoy is on the map: at the gate, a colonist sworn to vengeance on them has their say before
        /// the council first; then the speaker walks out to them (again, if the first was called away).
        /// </summary>
        public static void Hour(Tie t, Lord lord, Map home)
        {
            if (!(lord.LordJob is LordJob_Parley job) || job.sworn || job.arrived < 0) return;
            var envoy = Envoy(lord);
            if (envoy == null) return;
            if (!job.heard && !heard.Contains(t.id))
            {
                // The council's answer still waits (asked this hour or before a reload): the speaker waits for it.
                if (TruceObjection.Open(t.id) || TruceObjection.Ask(t, envoy, home)) return;
                heard.Add(t.id);
            }
            job.heard = true;
            if (home.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == PactsDefOf.Rimfolk_TruceParley && c.CurJob.targetA.Thing == envoy)) return;
            SendSpeaker(t, envoy, home);
        }

        /// <summary>The objection was heard and the council sends the envoy home: the truce lapses, and their people feel turned away.</summary>
        internal static void TurnAway(Tie t)
        {
            var f = Truces.PeopleOf(t);
            var home = Truces.HomeOf(t);
            LordOf(t, home)?.ReceiveMemo(LordJob_Parley.MemoLeave);
            if (f != null) FactionMemory.Note(f, "slighted", PactWords.P("CC_Truce_Deed"));
            Truces.End(t, Tie.State.Ended, null);
        }

        /// <summary>Who walks out to them: the colony's Speaker, else whoever is best with people, up and about at home.</summary>
        public static Pawn Speaker(Map home)
        {
            var s = OfficeTracker.For(home)?.Speaker;
            if (Fit(s, home)) return s;
            return home.mapPawns.FreeColonistsSpawned.Where(p => Fit(p, home)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
        }

        private static bool Fit(Pawn p, Map home) =>
            p != null && p.Spawned && p.Map == home && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && !p.DevelopmentalStage.Juvenile()
            && p.health.capacities.CapableOf(PawnCapacityDefOf.Talking) && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);

        /// <summary>The speaker sets out for the envoy, carrying the colony's silver when that is the price.</summary>
        public static bool SendSpeaker(Tie t, Pawn envoy, Map home)
        {
            var def = PactsDefOf.Rimfolk_TruceParley;
            var speaker = Speaker(home);
            if (def == null || speaker == null || envoy == null) return false;
            Thing silver = null;
            int count = 0;
            if (Truces.PriceOf(t) == TrucePrice.Ours)
            {
                count = t.terms.count;
                silver = home.listerThings.ThingsOfDef(ThingDefOf.Silver)
                    .Where(x => x.Spawned && !x.IsForbidden(speaker) && (x.IsInAnyStorage() || home.areaManager.Home[x.Position]) && speaker.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderByDescending(x => x.stackCount >= count).ThenBy(x => x.Position.DistanceToSquared(speaker.Position)).FirstOrDefault();
            }
            var job = JobMaker.MakeJob(def, envoy, silver);
            job.count = silver != null ? Mathf.Min(count, silver.stackCount) : 1;
            // together: the speaker walks out to the envoy, and they swear it face to face.
            speaker.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (speaker.CurJob != job) return false;
            var f = Truces.PeopleOf(t);
            // apart: told as the speaker sets out; the oath itself is sworn face to face.
            Tell.Message(NewsKind.World, Importance.Notable, home, PactWords.P("CC_Truce_Speaker_Goes", speaker.Named("PAWN"), PactWords.People(f).Named("PEOPLE")),
                new LookTargets(speaker), MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    /// <summary>
    /// The parley's group (TruceParley.cs): they walk to the gate, wait there for the colony's speaker, and go home once it
    /// is sworn, turned away, or the wait is long; hurt by the colony, they run and fight their way out, and the colony has
    /// broken the parley. Under the escort people's protection while here (their guests), dropped as they leave.
    /// </summary>
    public class LordJob_Parley : LordJob
    {
        public int tie = -1;
        public IntVec3 gate;
        public Faction escort;
        /// <summary>When they reached the gate, or -1.</summary>
        public int arrived = -1;
        public bool sworn;
        public bool harmed;
        /// <summary>An avenger's objection was heard (or nobody objected): the speaker may go out.</summary>
        public bool heard;

        public const string MemoLeave = "ParleyLeave";

        public LordJob_Parley()
        {
        }

        public LordJob_Parley(int tie, IntVec3 gate, Faction escort)
        {
            this.tie = tie;
            this.gate = gate;
            this.escort = escort;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_Travel(gate);
            graph.StartingToil = walk;
            var wait = new LordToil_DefendPoint(gate, 3f, 3f);
            graph.AddToil(wait);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, wait);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddPostAction(new TransitionAction_Custom(() => arrived = Find.TickManager.TicksGame));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(wait);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            goes.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt((PactMath.ParleyWaitHours + 12f) * GenDate.TicksPerHour)));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(wait, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(Harmed));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);
            return graph;
        }

        /// <summary>Hurt by the colony under the sign of parley: no longer anyone's guests, they run, and the colony broke its word.</summary>
        private void Harmed()
        {
            if (harmed) return;
            harmed = true;
            foreach (var p in lord.ownedPawns.ToList()) Unguard(p);
            try
            {
                var t = Truces.Get(tie);
                var f = lord.faction;
                if (t != null && t.Live)
                {
                    t.terms.goods = Truces.BrokenByUsKey;
                    Truces.End(t, Tie.State.Broken, null);
                }
                if (f != null)
                {
                    FactionMemory.Note(f, "brokeTruce", PactWords.P("CC_Truce_By_Parley"));
                    Truces.WordBroken(f);
                    var form = PactWords.TruceForm(Map, f);
                    Tell.Letter(NewsKind.World, Importance.Important, Map, PactWords.P("CC_Truce_BrokenByUs_Label", PactWords.People(f).Named("PEOPLE")),
                        PactWords.T("CC_Truce_ParleyHarmed", PactWords.People(f).Named("PEOPLE"), form.rite.Named("RITE")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: the parley harmed", e);
            }
        }

        private void Unguard(Pawn p)
        {
            try
            {
                if (p?.guest != null && p.HostFaction == escort && escort != null) p.guest.SetGuestStatus(null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: an envoy's guest status", e);
            }
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            Unguard(p);
        }

        public override void Cleanup()
        {
            base.Cleanup();
            foreach (var p in lord?.ownedPawns?.ToList() ?? new List<Pawn>()) Unguard(p);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref gate, "gate");
            Scribe_References.Look(ref escort, "escort");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref sworn, "sworn");
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref heard, "heard");
        }
    }

    /// <summary>
    /// The colony's speaker walks out to their envoy at the gate (target A; with the colony's silver, target B, when that is
    /// the price), and they swear the truce face to face: a few words each, the silver handed over, and the oath. Called
    /// away, the truce waits for the speaker to come again.
    /// </summary>
    public class JobDriver_Parley : JobDriver
    {
        private Pawn Envoy => job.GetTarget(TargetIndex.A).Thing as Pawn;

        private LordJob_Parley Parley => Envoy?.GetLord()?.LordJob as LordJob_Parley;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.GetTarget(TargetIndex.B).HasThing || pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

        public override string GetReport()
        {
            var f = Envoy?.Faction;
            return f != null ? PactWords.P("CC_Truce_Report", PactWords.People(f).Named("PEOPLE")) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Envoy == null || Envoy.Dead || Envoy.Downed || !Envoy.Spawned || Parley == null || Parley.sworn || Parley.harmed);
            AddFinishAction(cond =>
            {
                try
                {
                    if (pawn.carryTracker?.CarriedThing != null && pawn.Spawned && !(Parley?.sworn ?? false))
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("truces: the speaker's walk ends", e);
                }
            });
            if (job.GetTarget(TargetIndex.B).HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var talk = ToilMaker.MakeToil("Parley");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 1200;
            talk.handlingFacing = true;
            talk.initAction = () =>
            {
                // together: face to face at the gate.
                Outsiders.Say(pawn, "parley", Envoy);
            };
            talk.tickIntervalAction = delta =>
            {
                var e = Envoy;
                if (e == null) return;
                pawn.rotationTracker.FaceTarget(e);
                int done = talk.actor.jobs.curDriver.ticksLeftThisToil;
                // together: their envoy answers halfway, the two standing face to face.
                if (done <= 600 && done + delta > 600) Outsiders.Say(e, "parleyterms", pawn);
            };
            talk.WithProgressBarToilDelay(TargetIndex.A);
            yield return talk;
            var sworn = ToilMaker.MakeToil("ParleySworn");
            sworn.initAction = () =>
            {
                try
                {
                    var p = Parley;
                    var t = p != null ? Truces.Get(p.tie) : null;
                    if (t == null || !Truces.Pending(t)) return;
                    int handed = 0;
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried != null && carried.def == ThingDefOf.Silver)
                    {
                        // The silver goes into their hands, and home with them.
                        handed = carried.stackCount;
                        pawn.carryTracker.DestroyCarriedThing();
                    }
                    p.sworn = true;
                    Truces.Swear(t, pawn, Envoy, handed);
                    // together: the oath sworn, said as the two stand face to face.
                    Outsiders.Say(Envoy, "parleysworn", pawn);
                    Envoy?.GetLord()?.ReceiveMemo(LordJob_Parley.MemoLeave);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("truces: sworn at the gate", e);
                }
            };
            sworn.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return sworn;
        }
    }
}
