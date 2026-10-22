using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class VisitorDefOf
    {
        public static DutyDef Rimfolk_VisitWait;
        public static DutyDef Rimfolk_VisitBeasts;

        static VisitorDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VisitorDefOf));
        }
    }

    /// <summary>
    /// A visit's people (and animals) as a group: they walk in to their spot, stay close to it (eating, sleeping on the
    /// ground, the animals grazing) until the visit says they're done, then walk off: back to the nearest edge, or on to
    /// the far side of the map (<see cref="exit"/>). Hurt by the colony, or their people turned hostile, they leave at
    /// once, defending themselves. Should Rimfolk's record of them be gone, they go; and after three and a half days at
    /// the spot, whatever happens, they go.
    /// </summary>
    public class LordJob_RimfolkVisit : LordJob
    {
        public int tripId = -1;
        public IntVec3 spot;
        public IntVec3 exit = IntVec3.Invalid;
        public float radius = 5f;

        public const string MemoLeave = "VisitLeave";
        public const string MemoHurry = "VisitHurry";

        public LordJob_RimfolkVisit()
        {
        }

        public LordJob_RimfolkVisit(int tripId, IntVec3 spot, IntVec3 exit, float radius)
        {
            this.tripId = tripId;
            this.spot = spot;
            this.exit = exit;
            this.radius = radius;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_Travel(spot);
            graph.StartingToil = travel;
            var here = new LordToil_VisitHere(spot, radius, tripId);
            graph.AddToil(here);
            LordToil_Travel onward = exit.IsValid ? new LordToil_Travel(exit) : null;
            if (onward != null) graph.AddToil(onward);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var hurry = new LordToil_ExitMap(LocomotionUrgency.Jog, false, true);
            graph.AddToil(hurry);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);
            LordToil away = onward ?? (LordToil)leave;

            // At the spot (or near enough, after a long walk).
            var arrived = new Transition(travel, here);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddTrigger(new Trigger_TicksPassed(VisitorMath.TicksPerHour * 6));
            graph.AddTransition(arrived);

            // Done here (the visit says so), or, should nobody ever say, three and a half days on.
            var goes = new Transition(travel, away);
            goes.AddSource(here);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            graph.AddTransition(goes);
            var late = new Transition(here, away);
            late.AddTrigger(new Trigger_TicksPassed(VisitorMath.TicksPerDay * 7 / 2));
            graph.AddTransition(late);

            // On the far side: off the map from there.
            if (onward != null)
            {
                var off = new Transition(onward, leave);
                off.AddTrigger(new Trigger_Memo("TravelArrived"));
                off.AddTrigger(new Trigger_TicksPassed(VisitorMath.TicksPerHour * 5));
                graph.AddTransition(off);
            }

            // Danger or bitter weather: off by the nearest way, at a jog.
            var quick = new Transition(travel, hurry);
            quick.AddSource(here);
            if (onward != null) quick.AddSource(onward);
            quick.AddSource(leave);
            quick.AddTrigger(new Trigger_Memo(MemoHurry));
            graph.AddTransition(quick);

            // Hurt by the colony: they leave at once, defending themselves.
            int id = tripId;
            var hurt = new Transition(travel, flee);
            hurt.AddSource(here);
            if (onward != null) hurt.AddSource(onward);
            hurt.AddSources(leave, hurry);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer, null, null));
            hurt.AddPostAction(new TransitionAction_Custom(() => VisitorTracker.Instance?.Notify_Harmed(id)));
            hurt.AddPostAction(new TransitionAction_WakeAll());
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            // Their people turned on the colony meanwhile.
            var war = new Transition(travel, flee);
            war.AddSource(here);
            if (onward != null) war.AddSource(onward);
            war.AddSources(leave, hurry);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_WakeAll());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            VisitorTracker.Instance?.Notify_Lost(tripId, p, condition);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_Values.Look(ref spot, "spot");
            Scribe_Values.Look(ref exit, "exit", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "radius", 5f);
        }
    }

    /// <summary>
    /// At their spot: the people keep close to it, eating what they brought and sleeping on the ground when they're
    /// tired; the animals graze and rest around it.
    /// </summary>
    public class LordToil_VisitHere : LordToil
    {
        private readonly IntVec3 spot;
        private readonly float radius;
        private readonly int tripId;

        public LordToil_VisitHere(IntVec3 spot, float radius, int tripId)
        {
            this.spot = spot;
            this.radius = radius;
            this.tripId = tripId;
        }

        public override IntVec3 FlagLoc => spot;

        public override bool AllowSatisfyLongNeeds => true;

        public override bool AllowRestingInBed => false;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = p.RaceProps.Humanlike
                    ? new PawnDuty(VisitorDefOf.Rimfolk_VisitWait, spot, radius)
                    : new PawnDuty(VisitorDefOf.Rimfolk_VisitBeasts, spot, radius + 4f);
        }

        public override void LordToilTick()
        {
            // Rimfolk's record of this visit is gone (the mod's part removed, or something broke): they just go.
            if (lord.ticksInToil % 2500 == 1700 && VisitorTracker.Instance?.Get(tripId)?.lord != lord) lord.ReceiveMemo(LordJob_RimfolkVisit.MemoLeave);
        }
    }
}
