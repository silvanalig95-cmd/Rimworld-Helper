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
    [DefOf]
    public static class PlayOutDefOf
    {
        public static GatheringDef Council_PlayOutGathering;
        public static JobDef Council_PlayOutVisit;
        public static JobDef Council_PlayOutWalk;
        public static JobDef Council_PlayOutVigil;
        public static JobDef Council_PlayOutSearch;
        public static JobDef Council_PlayOutCarry;
        public static JobDef Council_PlayOutWorkNext;

        static PlayOutDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PlayOutDefOf));
    }

    /// <summary>
    /// A gathering an answer called (a feast, a party, a bonfire, a toast): the game's own party at its heart, joined by
    /// those it was meant for when they're free (never the sleeping, the sick, the drafted or anyone busy with someone's
    /// care), and called off by any danger. At a feast, everyone who comes is served a cooked meal from the stores, the
    /// finest first of those their beliefs and food policy let them eat (Wishes/Beliefs.cs), and eats it at a table; with
    /// none fit for them, they sit with the rest and eat nothing. When it's over, the moods the answer held go to those
    /// who were there.
    /// </summary>
    public class LordJob_PlayOut : LordJob_Joinable_Gathering
    {
        public int playId;
        public PlayKind kind;
        public string label;
        /// <summary>A feast with meals in the stores to serve.</summary>
        public bool serve;
        /// <summary>A fire laid for it, which burns down at the end.</summary>
        public Thing laidFire;
        public bool music;
        public bool words;
        public string pool;
        public List<string> lines = new List<string>();
        private List<Pawn> served = new List<Pawn>();
        private List<Pawn> fed = new List<Pawn>();
        /// <summary>At the feast, but nothing in the stores was fit for them to eat (their beliefs, their food policy): they sit with the rest and eat nothing.</summary>
        private List<Pawn> abstained = new List<Pawn>();
        private bool ended;

        [Unsaved] private LordToil_Party party;
        [Unsaved] private int nextLook;
        /// <summary>A toast's cups have gone up (Immersion/GatheringTouches.cs).</summary>
        [Unsaved] private bool raised;

        private static FleckDef musicFleck;
        private static bool looked;
        private static readonly Color SpeechColor = new Color(1f, 0.93f, 0.72f);

        public LordJob_PlayOut()
        {
        }

        public LordJob_PlayOut(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef, PlayOut play) : base(spot, organizer, gatheringDef)
        {
            playId = play.id;
            kind = play.kind;
            label = play.label;
            durationTicks = play.durationTicks;
            music = play.music;
            words = play.words;
            pool = play.pool;
            lines = play.lines?.ToList() ?? new List<string>();
        }

        private PlayOut Play => PlayOutTracker.Instance?.Get(playId);

        public override bool AllowStartNewGatherings => false;

        public override bool OrganizerIsStartingPawn => true;

        protected override LordToil CreateGatheringToil(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) =>
            new LordToil_Party(spot, gatheringDef, kind == PlayKind.Toast ? 2.5E-05f : LordToil_Party.DefaultJoyPerTick);

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var toil = CreateGatheringToil(spot, organizer, gatheringDef);
            party = toil as LordToil_Party;
            graph.AddToil(toil);
            var end = new LordToil_End();
            graph.AddToil(end);

            var calledOff = new Transition(toil, end);
            calledOff.AddTrigger(new Trigger_TickCondition(() => ShouldBeCalledOff(), 60));
            calledOff.AddTrigger(new Trigger_PawnKilled());
            calledOff.AddPreAction(new TransitionAction_Custom(() => Finish(true)));
            graph.AddTransition(calledOff);

            timeoutTrigger = GetTimeoutTrigger();
            var done = new Transition(toil, end);
            done.AddTrigger(timeoutTrigger);
            done.AddPreAction(new TransitionAction_Custom(() => Finish(false)));
            graph.AddTransition(done);
            return graph;
        }

        /// <summary>Any danger ends it at once (people go back to their posts), and so does weather no gathering survives.</summary>
        protected override bool ShouldBeCalledOff()
        {
            try
            {
                var map = Map;
                if (map == null) return true;
                if (map.dangerWatcher.DangerRating != StoryDanger.None || !GatheringsUtility.AcceptableGameConditionsToContinueGathering(map)) return true;
                // Over an hour in with nobody left at it: it has fizzled out.
                return lord != null && lord.ownedPawns.Count == 0 && lord.ticksInToil > PlayOutMath.TicksPerHour;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering call-off check", e);
                return true;
            }
        }

        /// <summary>One of those it's for (see <see cref="IsInvited"/>).</summary>
        public bool Invites(Pawn p) => IsInvited(p);

        /// <summary>Only those the answer was for; anyone at all from the colony if its plan was lost.</summary>
        protected override bool IsInvited(Pawn p)
        {
            var play = Play;
            if (play != null) return play.TakesPart(p);
            return p != null && p.Faction == Faction.OfPlayer && p.IsColonist;
        }

        /// <summary>The game's party rules, plus Rimfolk's: never from a sickbed, under arms, or in the middle of caring for someone.</summary>
        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            try
            {
                if (!IsInvited(p) || !PlayOuts.CanJoin(p)) return 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering join", e);
                return 0f;
            }
            return base.VoluntaryJoinPriorityFor(p);
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            int now = Find.TickManager.TicksGame;
            if (ended || lord == null || now < nextLook) return;
            nextLook = now + 250;
            try
            {
                if (serve && kind == PlayKind.Feast) Serve();
                // A drink from the stores to raise (Integrations/ToastDrinks.cs: beer, Vanilla Brewing Expanded's wines and teas).
                if (kind == PlayKind.Toast) ToastDrinks.Pour(lord, Map, spot, words);
                Touches();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering tick", e);
            }
        }

        /// <summary>
        /// A plate for everyone at the feast who hasn't had one: an order to go and eat the best meal they may have. Only
        /// what their beliefs and food policy let them eat (Wishes/Beliefs.cs); with nothing fit for them in the stores,
        /// they stay at the table without eating, and their journal may say so.
        /// </summary>
        private void Serve()
        {
            var map = Map;
            if (map == null) return;
            foreach (var p in lord.ownedPawns.ToList())
            {
                if (p == null || served.Contains(p) || fed.Contains(p) || abstained.Contains(p) || !p.Spawned || p.Map != map || !p.Awake()) continue;
                if (!PlayOuts.CanJoin(p) || p.CurJobDef == JobDefOf.Ingest) continue;
                var meal = PlaySpots.BestMeal(p, map, spot, out bool noneFit);
                if (meal == null)
                {
                    if (noneFit)
                    {
                        abstained.Add(p);
                        Beliefs.NoteNothingFit(p, label);
                    }
                    continue;
                }
                var job = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                job.count = 1;
                served.Add(p);
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }

        /// <summary>A colonist at the feast ate a meal.</summary>
        public void Notify_Ate(Pawn p, Thing food)
        {
            if (!serve || ended || p == null || fed.Contains(p) || !PlaySpots.IsFeastMeal(food)) return;
            fed.Add(p);
        }

        /// <summary>Notes over the heads of those at a song, and now and then a line said, for the camera if it's looking.</summary>
        private void Touches()
        {
            var map = Map;
            if (map == null || Find.CurrentMap != map) return;
            var here = lord.ownedPawns.Where(p => p != null && p.Spawned && p.Awake() && GatheringsUtility.InGatheringArea(p.Position, spot, map)).ToList();
            if (here.Count == 0) return;
            // The fire's smoke, a grave's glow after dark, the toast's raised cups.
            GatheringTouches.Look(this, Play, here, map, spot, durationTicks, ref raised);
            if (music && VisualTouches.On)
            {
                if (!looked)
                {
                    looked = true;
                    musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                }
                if (musicFleck != null)
                    foreach (var p in here.InRandomOrder().Take(2))
                        FleckMaker.ThrowMetaIcon(p.Position, map, musicFleck);
            }
            if (!Rand.Chance(0.3f)) return;
            var speaker = GatheringTouches.Speaker(kind, here);
            string line = null;
            if (!lines.NullOrEmpty() && Rand.Chance(0.5f)) line = lines.RandomElement();
            if (line.NullOrEmpty() && !pool.NullOrEmpty()) line = Flavor.FromPool(pool, speaker);
            // together: said at the gathering itself, by someone standing in its area with the others.
            if (!line.NullOrEmpty()) Speech.Say(speaker, line, null, SpeechColor);
        }

        public override string GetReport(Pawn pawn) => words ? "CC_Play_Report_Words".Translate() : ("CC_Play_Report_" + kind).Translate();

        /// <summary>When it ends, nobody drops the meal they're eating, a load in their arms, or work of their own.</summary>
        public override bool EndPawnJobOnCleanup(Pawn p)
        {
            var job = p?.CurJob;
            if (job != null && (job.def == JobDefOf.Ingest || p.carryTracker?.CarriedThing != null || job.workGiverDef != null || job.playerForced)) return false;
            return base.EndPawnJobOnCleanup(p);
        }

        public override void Cleanup()
        {
            base.Cleanup();
            Finish(true);
        }

        /// <summary>Over: who was there long enough (or ate at the feast) gets the answer's moods; a laid fire burns down.</summary>
        private void Finish(bool early)
        {
            if (ended) return;
            ended = true;
            try
            {
                var attended = new List<Pawn>();
                var data = party?.Data;
                if (data?.presentForTicks != null)
                    foreach (var kv in data.presentForTicks)
                        if (kv.Key != null && PlayOutMath.Attended(kv.Value, durationTicks)) attended.Add(kv.Key);
                foreach (var p in fed)
                    if (p != null && !attended.Contains(p)) attended.Add(p);
                int ate = !serve || kind != PlayKind.Feast ? -1 : RimfolkPatches.On ? fed.Count : served.Count;
                Setbacks.Notify_GatheringOver(Play, attended, fed, serve && kind == PlayKind.Feast, early, Map, spot);
                Interplay.Notify_GatheringOver(Play, attended, fed, kind, early, Map);
                PlayOutTracker.Instance?.GatheringEnded(playId, attended, early, ate, laidFire != null, label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gathering end", e);
                try
                {
                    PlayOutTracker.Instance?.GatheringEnded(playId, null, true, -1, false, label);
                }
                catch (Exception e2)
                {
                    CouncilLog.ErrorOnce("gathering end fallback", e2);
                }
            }
            finally
            {
                if (laidFire != null)
                {
                    try
                    {
                        PlaySpots.BurnDown(laidFire);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gathering fire", e);
                    }
                    laidFire = null;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref playId, "playId");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref serve, "serve");
            Scribe_References.Look(ref laidFire, "laidFire");
            Scribe_Values.Look(ref music, "music");
            Scribe_Values.Look(ref words, "words");
            Scribe_Values.Look(ref pool, "pool");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);
            Scribe_Collections.Look(ref served, "served", LookMode.Reference);
            Scribe_Collections.Look(ref fed, "fed", LookMode.Reference);
            Scribe_Collections.Look(ref abstained, "abstained", LookMode.Reference);
            Scribe_Values.Look(ref ended, "ended");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null) lines = new List<string>();
                if (served == null) served = new List<Pawn>();
                if (fed == null) fed = new List<Pawn>();
                if (abstained == null) abstained = new List<Pawn>();
                served.RemoveAll(p => p == null);
                fed.RemoveAll(p => p == null);
                abstained.RemoveAll(p => p == null);
                if (gatheringDef == null) gatheringDef = PlayOutDefOf.Council_PlayOutGathering;
            }
        }
    }

    /// <summary>Required by the gathering def; these gatherings are only ever started by an answer.</summary>
    public class GatheringWorker_PlayOut : GatheringWorker
    {
        public override bool CanExecute(Map map, Pawn organizer = null) => false;

        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            return false;
        }

        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer) => null;
    }
}
