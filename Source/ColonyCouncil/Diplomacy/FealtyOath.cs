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
    /// The oath at the gate (Fealty.cs; docs/design/stage2-diplomacy.md, item 9): their lord's retinue walks in from the
    /// edge to the colony's gate (their head, when free to travel, else one of their people who speaks for them, with a
    /// guard) and waits there. A colonist who objects has their say before the council first (FealtyObjection.cs); then
    /// the colony's speaker (the Speaker, else whoever is best with people) walks out to them, and the oath is sworn face
    /// to face by their rite (JobDriver_FealtyOath). Sworn, they go home; nobody comes out within the day, or the council
    /// sends them away, they go home without it. The lord is the game's own (saved with the map).
    /// </summary>
    public static class FealtyOath
    {
        /// <summary>The retinue's lord on the home, or null.</summary>
        public static Lord LordOf(Map home)
        {
            if (home?.lordManager == null) return null;
            foreach (var l in home.lordManager.lords)
                if (l?.LordJob is LordJob_FealtyOath)
                    return l;
            return null;
        }

        /// <summary>Who speaks for them at the gate (their head or the one sent), the first still up.</summary>
        public static Pawn Lord(Lord l) => l?.ownedPawns.FirstOrDefault(p => p != null && p.Spawned && !p.Dead && !p.Downed);

        /// <summary>Their lord and a guard walk in from the edge toward the gate.</summary>
        public static bool Send(Faction f, Map home)
        {
            if (f == null || home == null || LordOf(home) != null) return false;
            if (!Arrivals.TryEntryCell(home, out var entry)) return false;
            var gate = DropCellFinder.TradeDropSpot(home);
            if (!gate.IsValid) return false;
            var pawns = new List<Pawn>();
            var head = FavourCalls.HeadOf(f, home);
            bool headCame = false;
            try
            {
                if (head?.pawn != null && !head.pawn.Spawned && LivingEvents.FreeToTravel(head.pawn) && Arrivals.Spawn(head.pawn, home, entry))
                {
                    pawns.Add(head.pawn);
                    headCame = true;
                }
                else
                {
                    var kind = Arrivals.PersonKind(f);
                    if (kind == null) return false;
                    var p = Arrivals.Make(kind, f);
                    GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, home, 3), home);
                    pawns.Add(p);
                }
                var guard = GuardKind(f);
                if (guard != null)
                {
                    var g = Arrivals.Make(guard, f);
                    GenSpawn.Spawn(g, CellFinder.RandomClosewalkCellNear(entry, home, 3), home);
                    pawns.Add(g);
                }
                LordMaker.MakeNewLord(f, new LordJob_FealtyOath(f, gate), home, pawns);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the retinue sent", e);
                foreach (var p in pawns.Skip(headCame ? 1 : 0).Where(p => p.Spawned).ToList()) p.DeSpawn();
                foreach (var p in pawns.Skip(headCame ? 1 : 0)) Arrivals.Discard(p);
                return false;
            }
            var form = FealtyWords.Form(home, f);
            // apart: their lord walks in toward the gate; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Important, home, FealtyWords.T(headCame ? "CC_Fealty_Lord_Comes" : "CC_Fealty_Lord_Sent", pawns[0].Named("LORD"),
                FealtyWords.People(f).Named("PEOPLE"), form.rite.Named("RITE")), new LookTargets(pawns[0]), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>One of their lighter fighters to walk beside the lord, or none.</summary>
        private static PawnKindDef GuardKind(Faction f)
        {
            var opts = f?.def?.pawnGroupMakers?.Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true && k.combatPower <= 90f).ToList();
            return opts == null || opts.Count == 0 ? null : opts.RandomElement();
        }

        // ================= At the gate =================

        /// <summary>
        /// The hour, while their lord is on the map: at the gate, a colonist who objects has their say before the council
        /// first; then the speaker walks out to them (again, if the first was called away).
        /// </summary>
        public static void Hour(Faction f, Lord lord, Map home)
        {
            if (!(lord.LordJob is LordJob_FealtyOath job) || job.sworn || job.arrived < 0) return;
            var x = Lord(lord);
            if (x == null) return;
            if (!job.heard)
            {
                // The council's answer still waits (asked this hour or before a reload): the speaker waits for it.
                if (FealtyObjection.SwearOpen(f) || FealtyObjection.AskSwear(f, x, home)) return;
                job.heard = true;
            }
            if (home.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == FealtyDefOf.Rimfolk_FealtyOath && c.CurJob.targetA.Thing == x)) return;
            SendSpeaker(f, x, home);
        }

        /// <summary>The council heard the objection and swears all the same: the speaker may go out.</summary>
        internal static void Allow(Map home)
        {
            if (LordOf(home)?.LordJob is LordJob_FealtyOath job) job.heard = true;
        }

        /// <summary>The council sends them home: the oath lapses, and their people feel turned away.</summary>
        internal static void TurnAway(Map home, Faction f)
        {
            LordOf(home)?.ReceiveMemo(LordJob_FealtyOath.MemoLeave);
            if (f != null) FactionMemory.Note(f, "slighted", FealtyWords.Form(home, f).word);
            Fealty.RetinueGone(home, f, false);
        }

        /// <summary>The speaker sets out for their lord.</summary>
        public static bool SendSpeaker(Faction f, Pawn lord, Map home)
        {
            var def = FealtyDefOf.Rimfolk_FealtyOath;
            var speaker = TruceParley.Speaker(home);
            if (def == null || speaker == null || lord == null) return false;
            var job = JobMaker.MakeJob(def, lord);
            // together: the speaker walks out to their lord, and they swear it face to face.
            speaker.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (speaker.CurJob != job) return false;
            // apart: told as the speaker sets out; the oath itself is sworn face to face.
            Tell.Message(NewsKind.World, Importance.Notable, home, FealtyWords.P("CC_Fealty_Speaker_Goes", speaker.Named("PAWN"), FealtyWords.People(f).Named("PEOPLE")),
                new LookTargets(speaker), MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    /// <summary>
    /// The retinue's group (FealtyOath.cs): they walk to the gate, wait there for the colony's speaker, and go home once it
    /// is sworn, they are turned away, or the wait is long; hurt by the colony, they run, and the oath is off.
    /// </summary>
    public class LordJob_FealtyOath : LordJob
    {
        public Faction people;
        public IntVec3 gate;
        /// <summary>When they reached the gate, or -1.</summary>
        public int arrived = -1;
        public bool sworn;
        public bool harmed;
        /// <summary>An objection was heard (or nobody objected): the speaker may go out.</summary>
        public bool heard;

        public const string MemoLeave = "FealtyLeave";

        public LordJob_FealtyOath()
        {
        }

        public LordJob_FealtyOath(Faction people, IntVec3 gate)
        {
            this.people = people;
            this.gate = gate;
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
            goes.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt((FealtyMath.OathWaitHours + 12f) * GenDate.TicksPerHour)));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            goes.AddPostAction(new TransitionAction_Custom(Gone));
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(wait, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(Harmed));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);
            return graph;
        }

        /// <summary>They turn for home: the oath awaited is let go unless it was sworn.</summary>
        private void Gone()
        {
            try
            {
                Fealty.RetinueGone(Map, people, sworn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the retinue goes home", e);
            }
        }

        /// <summary>Hurt by the colony at its own gate: they run, the oath is off, and their people remember it.</summary>
        private void Harmed()
        {
            if (harmed) return;
            harmed = true;
            try
            {
                var f = lord.faction;
                if (f != null && !sworn)
                {
                    FactionMemory.Note(f, "attacked", FealtyWords.Form(Map, f).word);
                    Fealty.RetinueGone(Map, f, false);
                    Tell.Letter(NewsKind.World, Importance.Important, Map, FealtyWords.P("CC_Fealty_Harmed_Label", FealtyWords.People(f).Named("PEOPLE")),
                        FealtyWords.T("CC_Fealty_Harmed", FealtyWords.People(f).Named("PEOPLE")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the retinue harmed", e);
            }
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref gate, "gate");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref sworn, "sworn");
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref heard, "heard");
        }
    }

    /// <summary>
    /// The colony's speaker walks out to their lord at the gate (target A), and they swear the oath face to face: a few
    /// words each, the oath by their rite, and the tie made. Called away, the oath waits for the speaker to come again.
    /// </summary>
    public class JobDriver_FealtyOath : JobDriver
    {
        private Pawn Lord => job.GetTarget(TargetIndex.A).Thing as Pawn;

        private LordJob_FealtyOath Oath => Lord?.GetLord()?.LordJob as LordJob_FealtyOath;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var f = Lord?.Faction;
            return f != null ? FealtyWords.P("CC_Fealty_Report", FealtyWords.People(f).Named("PEOPLE")) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Lord == null || Lord.Dead || Lord.Downed || !Lord.Spawned || Oath == null || Oath.sworn || Oath.harmed);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var talk = ToilMaker.MakeToil("FealtyOath");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 1500;
            talk.handlingFacing = true;
            talk.initAction = () =>
            {
                // together: face to face at the gate.
                Outsiders.Say(pawn, "fealtyoath", Lord);
            };
            talk.tickIntervalAction = delta =>
            {
                var l = Lord;
                if (l == null) return;
                pawn.rotationTracker.FaceTarget(l);
                int left = talk.actor.jobs.curDriver.ticksLeftThisToil;
                // together: their lord gives their terms halfway, the two standing face to face.
                if (left <= 750 && left + delta > 750) Outsiders.Say(l, "fealtyterms", pawn);
            };
            talk.WithProgressBarToilDelay(TargetIndex.A);
            yield return talk;
            var sworn = ToilMaker.MakeToil("FealtySworn");
            sworn.initAction = () =>
            {
                try
                {
                    var o = Oath;
                    var l = Lord;
                    if (o == null || o.sworn || l == null) return;
                    o.sworn = true;
                    Fealty.Swear(o.people, pawn.Map, pawn, l);
                    // together: the oath sworn, said as the two stand face to face.
                    Outsiders.Say(l, "fealtysworn", pawn);
                    l.GetLord()?.ReceiveMemo(LordJob_FealtyOath.MemoLeave);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fealty: sworn at the gate", e);
                }
            };
            sworn.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return sworn;
        }
    }
}
