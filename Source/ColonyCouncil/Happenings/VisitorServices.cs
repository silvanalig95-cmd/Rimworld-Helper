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
    [DefOf]
    public static class VisitorServiceDefOf
    {
        public static JobDef Council_VisitMend;
        public static JobDef Council_VisitDose;
        public static JobDef Council_VisitHold;

        static VisitorServiceDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VisitorServiceDefOf));
    }

    /// <summary>What a visitor does at the colony for an answer, or what waits on one (<see cref="VisitorServices"/>).</summary>
    public enum ServiceKind
    {
        /// <summary>A tinker works through the colony's worn clothes and weapons, one at a time, where each lies or on whoever wears it.</summary>
        Mend,
        /// <summary>A doctor goes round the colonists and gives each a dose in turn.</summary>
        Dose,
        /// <summary>A guest sleeps the night in a warm corner indoors, and with <see cref="ServiceTask.leave"/> sets off before it's light.</summary>
        Lodge,
        /// <summary>An ailment eased once the visit the same answer set going is over (a healer's afternoon with the one it ails).</summary>
        Ease
    }

    /// <summary>One thing a visitor does for an answer, and how far along it is.</summary>
    public class ServiceTask : IExposable
    {
        public ServiceKind kind;
        public Map map;
        /// <summary>The visitor: the tinker, the doctor, the guest; for an ease, the one whose ailment it is.</summary>
        public Pawn x;
        /// <summary>Mending: what is still to mend.</summary>
        public List<Thing> things = new List<Thing>();
        /// <summary>Doses: who is still to have one.</summary>
        public List<Pawn> pawns = new List<Pawn>();
        public HediffDef hediff;
        /// <summary>How many there were to see to, and how many the visitor has seen to.</summary>
        public int total;
        public int done;
        /// <summary>Mending and doses: how long the work on each takes.</summary>
        public int ticks;
        /// <summary>Given up after this tick (the visitor's time is up); lodging: first light.</summary>
        public int until;
        /// <summary>Lodging: they set off at first light, before the colony is up.</summary>
        public bool leave;
        /// <summary>An ease: the visit it waits for, how much of the ailment is left (or cured outright), the first ailment only.</summary>
        public int play = -1;
        public float factor = 1f;
        public bool cure;
        public bool one = true;
        /// <summary>Lodging: the corner they sleep in tonight.</summary>
        public IntVec3 cell = IntVec3.Invalid;
        /// <summary>The answer's words, told once it's all done.</summary>
        public string text;
        /// <summary>The first job was given, and a word said that it has begun.</summary>
        public bool begun;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref x, "x");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Defs.Look(ref hediff, "hediff");
            Scribe_Values.Look(ref total, "total");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref ticks, "ticks");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref leave, "leave");
            Scribe_Values.Look(ref play, "play", -1);
            Scribe_Values.Look(ref factor, "factor", 1f);
            Scribe_Values.Look(ref cure, "cure");
            Scribe_Values.Look(ref one, "one", true);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref begun, "begun");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (things == null) things = new List<Thing>();
                if (pawns == null) pawns = new List<Pawn>();
                things.RemoveAll(t => t == null);
                pawns.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// What visitors do at the colony for an answer, on the map and in their own time: a tinker paid to mend works
    /// through the worn clothes and weapons one at a time (to each where it lies in the stores, or to whoever wears it,
    /// who stands still for it), a doctor paid for doses goes round the colonists and gives each one theirs, a guest taken
    /// in sleeps the night in a warm corner of one of the colony's rooms (and may set off before it's light), and a
    /// healer's treatment eases the ailment once the afternoon with them is over. The answer's words are told when the
    /// work is done; if the visitor has to go first, a word says how far they got. Each visitor's stay is stretched
    /// while there is work left (a few hours at a time, never past the task's own limit). Cheap when nothing is going
    /// on (one list check a tick); with the "Answers play out on the map" setting off, the callers do it all at once.
    /// </summary>
    public class VisitorServices : GameComponent
    {
        public List<ServiceTask> tasks = new List<ServiceTask>();

        /// <summary>More than this at once and the oldest is wound up.</summary>
        public const int MaxTasks = 8;

        /// <summary>The work a tinker spreads over the whole pile, and the time for one thing at most and at least.</summary>
        private const float MendHours = 5f;
        private const int MendMin = 400, MendMax = 1500;
        /// <summary>How long a dose takes, and how long anyone stands still for it.</summary>
        private const int DoseTicks = 300;
        /// <summary>How long a tinker or a doctor may be at it in all.</summary>
        private const int MendLimit = GenDate.TicksPerHour * 16, DoseLimit = GenDate.TicksPerHour * 12;
        /// <summary>While there is work left, the visit has at least this long still to run.</summary>
        private const int Hold = GenDate.TicksPerHour * 2;
        /// <summary>Night falls for a guest staying over at this hour, and first light comes at the next (a guest who leaves goes an hour before).</summary>
        public const int Dusk = 21, Dawn = 6;

        private static VisitorServices cached;
        private static Game cachedGame;

        public static VisitorServices Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<VisitorServices>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public VisitorServices(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        private void Add(ServiceTask t)
        {
            tasks.Add(t);
            while (tasks.Count > MaxTasks)
            {
                var old = tasks[0];
                tasks.RemoveAt(0);
                Finish(old);
            }
        }

        public override void GameComponentTick()
        {
            if (tasks.Count == 0) return;
            int now = Now;
            // On the tick of the things answers keep on the map (OnMapTimed): both run only while something is kept, and both are light.
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareTimed)) return;
            foreach (var t in tasks.ToList())
            {
                bool over;
                try
                {
                    over = t.map == null || !Find.Maps.Contains(t.map) || Step(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor service " + t.kind, e);
                    over = true;
                }
                if (!over) continue;
                tasks.Remove(t);
                try
                {
                    Finish(t);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor service end " + t.kind, e);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tasks, "tasks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tasks == null) tasks = new List<ServiceTask>();
                tasks.RemoveAll(t => t == null);
            }
        }

        // ---------------- Setting them going ----------------

        /// <summary>
        /// The tinker (<paramref name="x"/>) works through <paramref name="pile"/>, nearest first, and
        /// <paramref name="text"/> is told when the last thing is mended. False when it can't be done on the map (the
        /// setting off, the tinker gone): the caller mends it all at once.
        /// </summary>
        public static bool Mend(Map map, Pawn x, List<Thing> pile, string text)
        {
            var self = Instance;
            if (self == null || !PlayOuts.On || !Here(x, map) || pile.NullOrEmpty()) return false;
            int n = pile.Count;
            self.Add(new ServiceTask
            {
                kind = ServiceKind.Mend, map = map, x = x, things = pile.ToList(), total = n, text = text, until = Now + MendLimit,
                ticks = Mathf.Clamp(Mathf.RoundToInt(MendHours * GenDate.TicksPerHour / n), MendMin, MendMax)
            });
            Stay(x, Hold);
            return true;
        }

        /// <summary>
        /// The doctor (<paramref name="x"/>) goes round <paramref name="who"/>, nearest first, and gives each a dose of
        /// <paramref name="hediff"/>; <paramref name="text"/> is told when the last has had theirs. False when it can't be
        /// done on the map: the caller doses everyone at once.
        /// </summary>
        public static bool Dose(Map map, Pawn x, List<Pawn> who, HediffDef hediff, string text)
        {
            var self = Instance;
            if (self == null || !PlayOuts.On || hediff == null || !Here(x, map) || who.NullOrEmpty()) return false;
            self.Add(new ServiceTask
            {
                kind = ServiceKind.Dose, map = map, x = x, pawns = who.ToList(), hediff = hediff, total = who.Count, text = text,
                ticks = DoseTicks, until = Now + DoseLimit
            });
            Stay(x, Hold);
            return true;
        }

        /// <summary>
        /// The guest (<paramref name="x"/>) sleeps the coming night in a warm corner indoors, until first light (their
        /// visit lasting that long); with <paramref name="leave"/> they set off an hour before it, before the colony is
        /// up. A guest of Hospitality's has its beds already.
        /// </summary>
        public static bool Lodge(Map map, Pawn x, bool leave)
        {
            var self = Instance;
            if (self == null || !PlayOuts.On || !Here(x, map) || Strangers.IsHospitalityGuest(x)) return false;
            if (self.tasks.Any(t => t.kind == ServiceKind.Lodge && t.x == x)) return true;
            int light = leave ? Dawn - 1 : Dawn;
            float left = light - GenLocalDate.HourFloat(map);
            if (left <= 0f) left += 24f;
            int until = Now + Mathf.RoundToInt(left * GenDate.TicksPerHour);
            self.Add(new ServiceTask { kind = ServiceKind.Lodge, map = map, x = x, leave = leave, until = until });
            Stay(x, until - Now + GenDate.TicksPerHour);
            return true;
        }

        /// <summary>
        /// The ailment of <paramref name="p"/> is eased once <paramref name="visit"/> (the afternoon with the healer the
        /// same answer set going) is over, whether or not it came about. False: ease it now.
        /// </summary>
        public static bool EaseAfter(PlayOut visit, Pawn p, float factor, bool cure, bool one)
        {
            var self = Instance;
            if (self == null || !PlayOuts.On || visit == null || visit.state == PlayState.Done || p == null || visit.map == null) return false;
            self.Add(new ServiceTask { kind = ServiceKind.Ease, map = visit.map, x = p, play = visit.id, factor = factor, cure = cure, one = one, until = Now + GenDate.TicksPerDay * 2 });
            return true;
        }

        /// <summary>
        /// The visitor stays until what the answer set going with them can have happened: each plan of
        /// <paramref name="plays"/> that is theirs (a visit to them, a lesson they give, a gathering they're a guest at)
        /// can wait and run its course before they go. A day more at most.
        /// </summary>
        public static void StayFor(Pawn x, List<PlayOut> plays)
        {
            if (x == null || plays.NullOrEmpty() || !PlayOuts.On) return;
            int now = Now, longest = 0;
            foreach (var p in plays)
            {
                if (p == null || p.state == PlayState.Done || p.other != x) continue;
                // A gathering is theirs only when they're its guest (with their family, or alone).
                if (p.IsGathering && p.guestsFrom != PlayGuests.Stranger && p.guestsFrom != PlayGuests.Kin) continue;
                longest = Math.Max(longest, p.deadline - now + p.durationTicks + GenDate.TicksPerHour);
            }
            if (longest > 0) Stay(x, Math.Min(longest, GenDate.TicksPerDay));
        }

        // ---------------- Each look ----------------

        private static bool Step(ServiceTask t, int now)
        {
            switch (t.kind)
            {
                case ServiceKind.Mend: return StepMend(t, now);
                case ServiceKind.Dose: return StepDose(t, now);
                case ServiceKind.Lodge: return StepLodge(t, now);
                case ServiceKind.Ease: return StepEase(t, now);
                default: return true;
            }
        }

        /// <summary>On the map, up, of a people at peace with the colony, and not a prisoner.</summary>
        private static bool Here(Pawn x, Map map) =>
            x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer) && !x.IsPrisoner;

        /// <summary>On their way out of the map (their visit over, or sent on).</summary>
        private static bool Leaving(Pawn x)
        {
            var duty = x.mindState?.duty?.def;
            return duty != null && duty.defName.StartsWith("ExitMap");
        }

        private static bool Danger(Map map) => map.dangerWatcher.DangerRating != StoryDanger.None;

        /// <summary>One of this file's working jobs (mending, a dose).</summary>
        private static bool Working(JobDef d) => d != null && (d == VisitorServiceDefOf.Council_VisitMend || d == VisitorServiceDefOf.Council_VisitDose);

        /// <summary>At something an answer of the colony's set going for them (a gathering, a visit, a lesson): that comes first.</summary>
        private static bool AtPlay(Pawn x)
        {
            var d = x.CurJobDef;
            return d?.driverClass != null && !Working(d) && d.driverClass.Namespace == typeof(VisitorServices).Namespace;
        }

        /// <summary>Free to be set to the work now: awake and calm, not eating or resting, not hungry or worn out, and not at something else of the colony's.</summary>
        private static bool Ready(Pawn x)
        {
            if (x.InMentalState || !x.Awake() || PlayOuts.NeedsPress(x) || AtPlay(x)) return false;
            var d = x.CurJobDef;
            return d != JobDefOf.Ingest && d != JobDefOf.LayDown && d != JobDefOf.LayDownResting;
        }

        /// <summary>
        /// Whoever wears, wields or has the thing in their pack, or null (lying on the map, or in someone's hands on the
        /// way somewhere: then it waits until it's set down).
        /// </summary>
        public static Pawn HolderOf(Thing t)
        {
            switch (t?.ParentHolder)
            {
                case Pawn_ApparelTracker a: return a.pawn;
                case Pawn_EquipmentTracker e: return e.pawn;
                case Pawn_InventoryTracker i: return i.pawn;
                default: return null;
            }
        }

        /// <summary>Someone who can stand still a few minutes for the visitor's work: up, awake, free, nowhere they're needed.</summary>
        private static bool CanStand(Pawn p, Map map) =>
            p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.InBed()
            && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && (p.CurJob == null || !p.CurJob.playerForced);

        // ---------------- Mending ----------------

        private static bool StepMend(ServiceTask t, int now)
        {
            var map = t.map;
            // Gone from the map, mended some other way, or taken on a caravan: no longer the tinker's to mend.
            int before = t.things.Count;
            t.things.RemoveAll(th => th == null || th.Destroyed || th.HitPoints >= th.MaxHitPoints || (th.MapHeld != map));
            t.total -= before - t.things.Count;
            var x = t.x;
            if (t.things.Count == 0 || !Here(x, map) || Leaving(x) || now > t.until) return true;
            if (now + Hold <= t.until) Stay(x, Hold);
            if (x.CurJobDef == VisitorServiceDefOf.Council_VisitMend || Danger(map) || !Ready(x)) return false;
            Thing best = null;
            LocalTargetInfo at = LocalTargetInfo.Invalid;
            float bestDist = float.MaxValue;
            foreach (var th in t.things)
            {
                var holder = HolderOf(th);
                LocalTargetInfo to;
                if (holder == null)
                {
                    if (!th.Spawned || !x.CanReach(th, PathEndMode.Touch, Verse.Danger.Some)) continue;
                    to = th;
                }
                else
                {
                    // On someone: only when they can stand still for it (worn, wielded or in their pack, it's the same).
                    if (!PlayOuts.OurPerson(holder) || !CanStand(holder, map) || !x.CanReach(holder, PathEndMode.Touch, Verse.Danger.Some)) continue;
                    to = holder;
                }
                float d = to.Cell.DistanceToSquared(x.Position);
                if (d >= bestDist) continue;
                bestDist = d;
                best = th;
                at = to;
            }
            if (best == null) return false;
            var job = JobMaker.MakeJob(VisitorServiceDefOf.Council_VisitMend, at, best);
            job.count = t.ticks;
            // together: the tinker walks to the thing, or to whoever wears it, and works on it there.
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (!t.begun)
            {
                t.begun = true;
                Messages.Message("CC_Service_Mend_Begin".Translate(x.Named("PAWN"), t.things.Count).Resolve(), x, MessageTypeDefOf.SilentInput, false);
            }
            return false;
        }

        /// <summary>A thing the tinker has finished: as good as new, and off the pile.</summary>
        public static void Mended(Pawn by, Thing thing)
        {
            if (thing == null || thing.Destroyed) return;
            thing.HitPoints = thing.MaxHitPoints;
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == ServiceKind.Mend && k.x == by && k.things.Contains(thing));
            if (t == null) return;
            t.things.Remove(thing);
            t.done++;
        }

        private static EffecterDef tailor, smith;
        private static SoundDef tailorSound, smithSound, tendSound;
        private static bool lookedDefs;

        private static void LookDefs()
        {
            if (lookedDefs) return;
            lookedDefs = true;
            tailor = DefDatabase<EffecterDef>.GetNamedSilentFail("Tailor");
            smith = DefDatabase<EffecterDef>.GetNamedSilentFail("Smith");
            tailorSound = DefDatabase<SoundDef>.GetNamedSilentFail("Recipe_Tailor");
            smithSound = DefDatabase<SoundDef>.GetNamedSilentFail("Recipe_Smith");
            tendSound = DefDatabase<SoundDef>.GetNamedSilentFail("Interact_Tend");
        }

        /// <summary>The game's own work effect for mending it: a tailor's for clothes, a smith's for weapons.</summary>
        public static EffecterDef MendEffect(Thing t)
        {
            LookDefs();
            var fx = t?.def != null && t.def.IsApparel ? tailor : smith;
            return fx ?? EffecterDefOf.ConstructMetal;
        }

        /// <summary>The sound of the work, or null.</summary>
        public static SoundDef MendSound(Thing t)
        {
            LookDefs();
            return t?.def != null && t.def.IsApparel ? tailorSound : smithSound;
        }

        public static SoundDef DoseSound
        {
            get
            {
                LookDefs();
                return tendSound;
            }
        }

        // ---------------- Doses ----------------

        /// <summary>Up to having a dose now: here and still (a baby may be asleep), and reachable.</summary>
        private static bool Doseable(Pawn p, Pawn x, Map map)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map != map || p.InMentalState || p.Drafted) return false;
            bool baby = p.DevelopmentalStage.Baby();
            // In bed, awake (or a baby asleep): the doctor comes to the bedside. Up and about: only when free to stand still.
            bool ok = p.InBed() ? p.Awake() || baby : !p.Downed && (CanStand(p, map) || baby);
            return ok && x.CanReach(p, PathEndMode.Touch, Verse.Danger.Some);
        }

        private static bool StepDose(ServiceTask t, int now)
        {
            var map = t.map;
            // Dead, no longer the colony's, or dosed some other way since: off the round.
            int before = t.pawns.Count;
            t.pawns.RemoveAll(p => p == null || p.Dead || p.Faction != Faction.OfPlayer || (t.hediff != null && (p.health?.hediffSet?.HasHediff(t.hediff) ?? true)));
            t.total -= before - t.pawns.Count;
            var x = t.x;
            if (t.pawns.Count == 0 || !Here(x, map) || Leaving(x) || now > t.until) return true;
            if (now + Hold <= t.until) Stay(x, Hold);
            if (x.CurJobDef == VisitorServiceDefOf.Council_VisitDose || Danger(map) || !Ready(x)) return false;
            var next = t.pawns.Where(p => Doseable(p, x, map)).OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (next == null) return false;
            var job = JobMaker.MakeJob(VisitorServiceDefOf.Council_VisitDose, next);
            job.count = t.ticks;
            // together: the doctor walks over to them and gives the dose at their side.
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (!t.begun)
            {
                t.begun = true;
                Messages.Message("CC_Service_Dose_Begin".Translate(x.Named("PAWN"), t.pawns.Count).Resolve(), x, MessageTypeDefOf.SilentInput, false);
            }
            return false;
        }

        /// <summary>A dose given: the protection is theirs from now, and they're off the round.</summary>
        public static void Dosed(Pawn by, Pawn patient)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == ServiceKind.Dose && k.x == by && k.pawns.Contains(patient));
            if (t == null || patient == null || patient.Dead) return;
            var set = patient.health?.hediffSet;
            if (t.hediff != null && set != null && !set.HasHediff(t.hediff)) patient.health.AddHediff(t.hediff);
            t.pawns.Remove(patient);
            t.done++;
        }

        // ---------------- A night indoors ----------------

        private static bool StepLodge(ServiceTask t, int now)
        {
            var x = t.x;
            var map = t.map;
            if (now > t.until || !Here(x, map) || Leaving(x)) return true;
            Stay(x, t.until - now + GenDate.TicksPerHour);
            int hour = GenLocalDate.HourOfDay(map);
            if (hour >= Dawn && hour < Dusk) return false;
            if (x.InMentalState || !x.Awake() || AtPlay(x) || Danger(map)) return false;
            var d = x.CurJobDef;
            if (d == JobDefOf.LayDown || d == JobDefOf.Ingest) return false;
            // Rested already (they slept earlier): they keep to themselves until it's time to go.
            if (x.needs?.rest != null && x.needs.rest.CurLevelPercentage > 0.95f) return false;
            if (!t.cell.IsValid || !Bed(t.cell, map, x)) t.cell = Corner(map, x);
            if (!t.cell.IsValid) return false;
            // apart: the guest beds down on their own, in a corner of one of the colony's warm rooms.
            x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, t.cell), JobCondition.InterruptForced, null, false, true);
            if (!t.begun)
            {
                t.begun = true;
                var room = t.cell.GetRoom(map);
                string where = room?.Role != null && room.Role != RoomRoleDefOf.None ? room.Role.label : "CC_Service_Lodge_Room".Translate().ToString();
                Messages.Message("CC_Service_Lodge".Translate(x.Named("PAWN"), where).Resolve(), x, MessageTypeDefOf.SilentInput, false);
            }
            return false;
        }

        /// <summary>A room to sleep in: the colony's own, indoors and closed in, nobody's bedroom, sickroom or cell.</summary>
        private static bool Fits(Room r)
        {
            if (r == null || r.TouchesMapEdge || r.PsychologicallyOutdoors || r.UsesOutdoorTemperature || !r.ProperRoom || r.IsHuge || r.IsDoorway || r.CellCount < 6) return false;
            var role = r.Role;
            return role != RoomRoleDefOf.Bedroom && role != RoomRoleDefOf.Barracks && role != RoomRoleDefOf.PrisonCell && role != RoomRoleDefOf.PrisonBarracks
                   && role != RoomRoleDefOf.Hospital;
        }

        /// <summary>A free spot on the floor: nothing built there, nobody on it or headed for it, not a doorway, and they can get to it.</summary>
        private static bool Bed(IntVec3 c, Map map, Pawn x) =>
            c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map) && c.GetFirstPawn(map) is var p && (p == null || p == x)
            && !map.pawnDestinationReservationManager.IsReserved(c) && Fits(c.GetRoom(map)) && x.CanReach(c, PathEndMode.OnCell, Verse.Danger.Some);

        private static bool ByWall(IntVec3 c, Map map) => GenAdj.CardinalDirections.Any(d => (c + d).InBounds(map) && (c + d).GetEdifice(map) is Building b
            && b.def.passability == Traversability.Impassable);

        /// <summary>
        /// Where the guest sleeps: a corner of the warmest of the colony's rooms that fit (dining and rec rooms first when
        /// they're warm enough), by a wall if there's room by one. Invalid when the colony has no such room.
        /// </summary>
        public static IntVec3 Corner(Map map, Pawn x)
        {
            if (map == null || x == null) return IntVec3.Invalid;
            var comfy = x.ComfortableTemperatureRange();
            Room best = null;
            float bestScore = float.MinValue;
            var seen = new HashSet<Room>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b == null || !b.Spawned || b.def.passability == Traversability.Impassable) continue;
                var r = b.GetRoom();
                if (r == null || !seen.Add(r) || !Fits(r)) continue;
                float temp = r.Temperature;
                // As warm as they'd like counts most; past that, a common room before a workshop.
                float score = -Mathf.Max(0f, comfy.min - temp) * 3f - Mathf.Max(0f, temp - comfy.max);
                string role = r.Role?.defName;
                if (role == "DiningRoom" || role == "RecRoom") score += 4f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = r;
            }
            if (best == null) return IntVec3.Invalid;
            var cells = best.Cells.Where(c => Bed(c, map, x)).ToList();
            if (cells.Count == 0) return IntVec3.Invalid;
            var walls = cells.Where(c => ByWall(c, map)).ToList();
            return (walls.Count > 0 ? walls : cells).RandomElement();
        }

        // ---------------- An ease after the visit ----------------

        private static bool StepEase(ServiceTask t, int now)
        {
            var play = PlayOutTracker.Instance?.plays.FirstOrDefault(p => p.id == t.play);
            if (play != null && play.state != PlayState.Done && now <= t.until) return false;
            if (t.x != null && !t.x.Dead) DilemmaEffect_EaseAilment.Ease(t.x, t.factor, t.cure, t.one);
            // Eased: nothing left for the end to do.
            t.play = -1;
            return true;
        }

        // ---------------- The end ----------------

        private static void Finish(ServiceTask t)
        {
            switch (t.kind)
            {
                case ServiceKind.Mend:
                    Stop(t.x, VisitorServiceDefOf.Council_VisitMend);
                    Tell(t, t.things.Count == 0, "CC_Service_Mend_Part", "CC_Service_Mend_None");
                    break;
                case ServiceKind.Dose:
                    Stop(t.x, VisitorServiceDefOf.Council_VisitDose);
                    Tell(t, t.pawns.Count == 0, "CC_Service_Dose_Part", "CC_Service_Dose_None");
                    break;
                case ServiceKind.Lodge:
                    if (t.leave && Here(t.x, t.map) && !Leaving(t.x))
                    {
                        if (t.x.CurJobDef == JobDefOf.LayDown) t.x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        DilemmaEffect_SendAway.Send(t.x);
                        Messages.Message("CC_Service_Lodge_Gone".Translate(t.x.Named("PAWN")).Resolve(), t.x, MessageTypeDefOf.SilentInput, false);
                    }
                    break;
                case ServiceKind.Ease:
                    // Wound up early: the ease it held comes now.
                    if (t.x != null && !t.x.Dead && t.play >= 0) DilemmaEffect_EaseAilment.Ease(t.x, t.factor, t.cure, t.one);
                    t.play = -1;
                    break;
            }
        }

        /// <summary>The visitor puts the work down: their job ends if it's still this one.</summary>
        private static void Stop(Pawn x, JobDef def)
        {
            if (x != null && x.Spawned && x.CurJobDef == def) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>The answer's words once all of it is done; else how far the visitor got before they had to go.</summary>
        private static void Tell(ServiceTask t, bool all, string partKey, string noneKey)
        {
            var x = t.x;
            if (x == null || t.map == null || !Find.Maps.Contains(t.map)) return;
            var look = x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (all && t.done > 0)
            {
                if (!t.text.NullOrEmpty()) News.Post(t.text, look, NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                return;
            }
            // Nothing was left to do that they didn't do (all of it gone some other way): nothing to say.
            if (all) return;
            string text = t.done > 0 ? partKey.Translate(x.Named("PAWN"), t.done, t.total).Resolve() : noneKey.Translate(x.Named("PAWN")).Resolve();
            News.Post(text, look, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- The visit's own time ----------------

        // The game keeps a visit's length and a timed trigger's length to itself: found by name, once (as PlayStrangers does).
        private static bool looked;
        private static AccessTools.FieldRef<LordJob_VisitColony, int?> visitLength;
        private static AccessTools.FieldRef<Trigger_TicksPassed, int> triggerLength;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                visitLength = AccessTools.FieldRefAccess<LordJob_VisitColony, int?>("durationTicks");
                triggerLength = AccessTools.FieldRefAccess<Trigger_TicksPassed, int>("duration");
            }
            catch (Exception e)
            {
                visitLength = null;
                triggerLength = null;
                Log.Warning("[Rimfolk] A visit's length isn't kept the way Rimfolk knows, so a visitor at work for the colony may leave before it's done: " + e.Message);
            }
        }

        /// <summary>
        /// The visitor's stay stretched to at least <paramref name="ticks"/> from now: the timed trigger that sends them
        /// off, and the visit's length for a reload. Only the game's own visits; never shortened.
        /// </summary>
        public static void Stay(Pawn x, int ticks)
        {
            var lord = x?.GetLord();
            if (!(lord?.LordJob is LordJob_VisitColony visit) || lord.Graph == null || ticks <= 0) return;
            Look();
            if (triggerLength == null) return;
            int? visitTicks = visitLength != null ? visitLength(visit) : null;
            foreach (var tr in lord.Graph.transitions)
                foreach (var trig in tr.triggers)
                {
                    if (!(trig is Trigger_TicksPassed passed)) continue;
                    ref int length = ref triggerLength(passed);
                    // Only a time that sends them off: the way out, or their visit's own length.
                    if (!(tr.target is LordToil_ExitMap) && !(visitTicks.HasValue && length == visitTicks.Value)) continue;
                    int passedNow = tr.sources.Contains(lord.CurLordToil) ? length - passed.TicksLeft : 0;
                    length = Math.Max(length, passedNow + ticks);
                }
            if (visitTicks.HasValue) visitLength(visit) = Math.Max(visitTicks.Value, ticks + lord.ticksInToil);
        }

        // ---------------- For the jobs ----------------

        /// <summary>
        /// Someone the visitor works on stands still for it, facing them, for <paramref name="ticks"/> (a colonist only when
        /// free to: never called from eating, sleep, a sickbed or what the player ordered), then goes back to what they were doing.
        /// </summary>
        public static void HoldStill(Pawn p, Pawn by, int ticks)
        {
            if (p == null || by == null || p.CurJobDef == VisitorServiceDefOf.Council_VisitHold || !CanStand(p, p.Map)) return;
            var job = JobMaker.MakeJob(VisitorServiceDefOf.Council_VisitHold, by);
            job.count = ticks;
            // together: the visitor is at their side already (their own job brought them there).
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, true, true);
        }

        /// <summary>Still at work on <paramref name="p"/>: mending what they wear, or giving them their dose.</summary>
        public static bool WorkingOn(Pawn by, Pawn p) => by?.CurJob != null && Working(by.CurJobDef) && by.CurJob.targetA.Thing == p;
    }

    /// <summary>
    /// A visiting tinker mending one of the colony's things (<see cref="VisitorServices"/>): to the thing where it lies
    /// (target A), or to whoever wears it (target A, the thing B, who stands still for it), then a while at the work
    /// with the game's own tailoring or smithing effect and sound, and it's as good as new.
    /// </summary>
    public class JobDriver_VisitMend : JobDriver
    {
        private Thing Item => job.targetB.Thing ?? job.targetA.Thing;

        private Pawn Wearer => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Item != null ? "CC_Service_Report_Mend".Translate(Item.LabelShort.Named("THING")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Item == null || Item.Destroyed || Item.HitPoints >= Item.MaxHitPoints);
            // Taken off, picked up or handed on since: it's looked for again where it is now.
            this.FailOn(() => Wearer != null ? VisitorServices.HolderOf(Item) != Wearer : !Item.Spawned);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("VisitMend");
            work.initAction = () =>
            {
                // together: the tinker is at their side; they stand still for it.
                if (Wearer != null) VisitorServices.HoldStill(Wearer, pawn, job.count + 60);
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(120, job.count);
            work.handlingFacing = true;
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                // Whoever wears it walked off: on to something else, and back to them later.
                if (Wearer != null && !Wearer.Position.InHorDistOf(pawn.Position, 2.5f)) EndJobWith(JobCondition.Incompletable);
            };
            work.WithEffect(() => VisitorServices.MendEffect(Item), TargetIndex.A);
            work.WithProgressBarToilDelay(TargetIndex.A);
            var sound = VisitorServices.MendSound(Item);
            if (sound != null) work.PlaySustainerOrSound(sound);
            yield return work;
            yield return Toils_General.Do(() => VisitorServices.Mended(pawn, Item));
        }
    }

    /// <summary>
    /// A visiting doctor giving one of the colonists their dose (<see cref="VisitorServices"/>): over to them (they stand
    /// still for it, or lie where they are), a few minutes at their side, and the protection is theirs.
    /// </summary>
    public class JobDriver_VisitDose : JobDriver
    {
        private Pawn Patient => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Patient != null ? "CC_Service_Report_Dose".Translate(Patient.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Patient == null || Patient.Dead || Patient.Drafted || Patient.InMentalState);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var dose = ToilMaker.MakeToil("VisitDose");
            dose.initAction = () =>
            {
                // together: the doctor is at their side; they stand still for it.
                VisitorServices.HoldStill(Patient, pawn, job.count + 60);
            };
            dose.defaultCompleteMode = ToilCompleteMode.Delay;
            dose.defaultDuration = Mathf.Max(60, job.count);
            dose.handlingFacing = true;
            dose.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                // Walked off before it was done: back to them later.
                if (!Patient.Position.InHorDistOf(pawn.Position, 2.5f)) EndJobWith(JobCondition.Incompletable);
            };
            dose.WithProgressBarToilDelay(TargetIndex.A);
            var sound = VisitorServices.DoseSound;
            if (sound != null) dose.PlaySustainerOrSound(sound);
            yield return dose;
            yield return Toils_General.Do(() => VisitorServices.Dosed(pawn, Patient));
        }
    }

    /// <summary>
    /// Someone standing still for a visitor's work on them (a coat mended on their back, a dose): facing them until the
    /// work is done or the visitor moves off, then back to what they were doing.
    /// </summary>
    public class JobDriver_VisitHold : JobDriver
    {
        private Pawn Other => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Other != null ? "CC_Service_Report_Hold".Translate(Other.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Other == null || Other.Dead || !Other.Spawned || Other.Map != pawn.Map);
            this.FailOn(() => pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            var stand = ToilMaker.MakeToil("VisitHold");
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = Mathf.Max(60, job.count);
            stand.handlingFacing = true;
            stand.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(Other);
                // Done with them, or gone off to something else: back to work.
                if (!VisitorServices.WorkingOn(Other, pawn)) ReadyForNextToil();
            };
            yield return stand;
        }
    }
}
