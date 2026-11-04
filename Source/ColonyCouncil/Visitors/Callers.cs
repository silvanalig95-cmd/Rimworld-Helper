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
    public static class CallerDefOf
    {
        public static JobDef Rimfolk_CallerView;
        public static JobDef Rimfolk_CallerCarryArt;
        public static JobDef Rimfolk_CallerCut;
        public static JobDef Rimfolk_CallerGetCut;
        public static JobDef Rimfolk_CallerHold;
        public static JobDef Rimfolk_CallerTend;
        public static JobDef Rimfolk_CallerTrain;
        public static JobDef Rimfolk_CallerDig;
        public static JobDef Rimfolk_CallerBury;
        public static JobDef Rimfolk_CallerLoad;
        public static JobDef Rimfolk_CallerTalk;
        public static ThoughtDef Rimfolk_Evt_ArtSoldProud;
        public static ThoughtDef Rimfolk_Evt_ArtSoldHurt;

        static CallerDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(CallerDefOf));
    }

    /// <summary>What a caller (a visitor come to do something) is doing at the colony (<see cref="Callers"/>).</summary>
    public enum CallerKind
    {
        /// <summary>The art collector walks round the colony's art, stopping before each piece.</summary>
        View,
        /// <summary>One of the colony takes a piece of art up (uninstalling it if it stands) and carries it out to the collector, who pays.</summary>
        Hand,
        /// <summary>The barber at their stool, each colonist in turn coming over for a cut.</summary>
        Cut,
        /// <summary>The animal doctor goes from animal to animal tending the hurt and sick, then works one young animal a while.</summary>
        Vet,
        /// <summary>The undertakers dig graves outside the wall and lay the enemy dead in them.</summary>
        Graves,
        /// <summary>The undertakers carry the enemy dead to their pack beast and load them on it, the raiders' loose weapons into their packs.</summary>
        Load,
        /// <summary>A captive's kin talk with them at the door of their cell.</summary>
        Talk,
        /// <summary>A captive let go: the wardens walk them out (the game's own release), and they leave with their kin.</summary>
        Free,
        /// <summary>A wedding party's night: in the colony's spare beds and under its roof, or round their own fire beyond the wall.</summary>
        Night
    }

    /// <summary>One thing a caller does, and how far along it is.</summary>
    public class CallerTask : IExposable
    {
        public CallerKind kind;
        public Map map;
        /// <summary>The caller: the collector, the barber, the animal doctor, the first of the undertakers, the kin who talks.</summary>
        public Pawn x;
        /// <summary>Their people: the undertakers, the wedding party.</summary>
        public List<Pawn> people = new List<Pawn>();
        /// <summary>The art still to see; the dead still to lay to rest or load.</summary>
        public List<Thing> things = new List<Thing>();
        /// <summary>Who is still to have a cut; the animals still to tend.</summary>
        public List<Pawn> pawns = new List<Pawn>();
        /// <summary>The one of the colony carrying the art out; the animal to train; the captive; the pack beast.</summary>
        public Pawn other;
        /// <summary>The piece carried out; the wedding party's own fire.</summary>
        public Thing thing;
        public Faction faction;
        /// <summary>Silver the caller pays when the piece is in their hands.</summary>
        public int pay;
        public int total;
        public int done;
        public int ticks;
        /// <summary>Given up after this tick.</summary>
        public int until;
        /// <summary>The barber's stool; the corner the wedding party's fire is laid in.</summary>
        public IntVec3 cell = IntVec3.Invalid;
        /// <summary>The undertakers' row of graves, and which way they're turned.</summary>
        public List<IntVec3> plots = new List<IntVec3>();
        public int rot;
        /// <summary>The vet works one animal for a while once the tending is done; a night indoors (else by their own fire).</summary>
        public bool train;
        public bool inside;
        /// <summary>The commission a hand-over settles, or -1.</summary>
        public int commission = -1;
        /// <summary>The answer's words, told once it's all done.</summary>
        public string text;
        /// <summary>The event it came from, for the lines its people say (HappeningExtension.lines, tagged by moment).</summary>
        public string def;
        public bool begun;
        /// <summary>A night: they've slept (first light has come since they lay down).</summary>
        public bool slept;
        /// <summary>The answer being given that set it going, until its words are handed over (<see cref="Callers.Adopt"/>; not saved).</summary>
        [Unsaved] public DilemmaContext ctxRef;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref x, "x");
            Scribe_Collections.Look(ref people, "people", LookMode.Reference);
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_References.Look(ref other, "other");
            Scribe_References.Look(ref thing, "thing");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref pay, "pay");
            Scribe_Values.Look(ref total, "total");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref ticks, "ticks");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Collections.Look(ref plots, "plots", LookMode.Value);
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref train, "train");
            Scribe_Values.Look(ref inside, "inside");
            Scribe_Values.Look(ref commission, "commission", -1);
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref begun, "begun");
            Scribe_Values.Look(ref slept, "slept");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<Pawn>();
                things ??= new List<Thing>();
                pawns ??= new List<Pawn>();
                plots ??= new List<IntVec3>();
                people.RemoveAll(p => p == null);
                things.RemoveAll(t => t == null);
                pawns.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>A piece the collector paid the colony to make: who it's for, who makes it, and the piece once it's made.</summary>
    public class Commission : IExposable
    {
        public int id;
        public Pawn collector;
        public Faction faction;
        public Pawn maker;
        public ThingDef def;
        /// <summary>Things made after this one (by id) may be it.</summary>
        public int idFloor;
        public int playId = -1;
        public Thing made;
        public int madeTick = -1;
        public int created;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref collector, "collector");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref maker, "maker");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref idFloor, "idFloor");
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_References.Look(ref made, "made");
            Scribe_Values.Look(ref madeTick, "madeTick", -1);
            Scribe_Values.Look(ref created, "created");
        }
    }

    /// <summary>
    /// Visitors who came to do something, doing it on the map in their own time (the pattern of VisitorServices): the art
    /// collector walking round the colony's art, the piece sold to them carried out by one of the colony, the barber
    /// cutting each colonist's hair in turn at a stool by the gate, the animal doctor tending the colony's hurt and sick
    /// animals and working a young one, the undertakers digging graves outside the wall or loading the enemy dead on
    /// their beast, a captive's kin talking with them at their door or walking out with them freed, a wedding party's
    /// night. The answer's words are told when it's done; a visitor who has to go first says how far they got. Each
    /// visit is stretched while there's work left. Commissions wait here for the collector to come back.
    /// </summary>
    public class Callers : GameComponent
    {
        public List<CallerTask> tasks = new List<CallerTask>();
        public List<Commission> commissions = new List<Commission>();
        private int nextCommission = 1;

        public const int MaxTasks = 8, MaxCommissions = 3;
        /// <summary>While there's work left, the visit has at least this long still to run.</summary>
        private const int Hold = GenDate.TicksPerHour * 2;

        private static Callers cached;
        private static Game cachedGame;

        public static Callers Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<Callers>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public Callers(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        public static CallerTask Add(CallerTask t)
        {
            var self = Instance;
            if (self == null || t == null) return null;
            self.tasks.Add(t);
            while (self.tasks.Count > MaxTasks)
            {
                var old = self.tasks[0];
                self.tasks.RemoveAt(0);
                self.Finish(old);
            }
            return t;
        }

        public override void GameComponentTick()
        {
            if (tasks.Count == 0 && commissions.Count == 0) return;
            int now = Now;
            // On the tick of the things answers keep on the map (OnMapTimed), as the visitors' own services are.
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareTimed)) return;
            using var clock = PassClock.Time(Pass.RareCallers);
            foreach (var t in tasks.ToList())
            {
                bool over;
                try
                {
                    over = t.map == null || !Find.Maps.Contains(t.map) || Step(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("caller " + t.kind, e);
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
                    CouncilLog.ErrorOnce("caller end " + t.kind, e);
                }
            }
            try
            {
                StepCommissions(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("caller commissions", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tasks, "tasks", LookMode.Deep);
            Scribe_Collections.Look(ref commissions, "commissions", LookMode.Deep);
            Scribe_Values.Look(ref nextCommission, "nextCommission", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tasks ??= new List<CallerTask>();
                commissions ??= new List<Commission>();
                tasks.RemoveAll(t => t == null);
                commissions.RemoveAll(c => c == null);
            }
        }

        // ---------------- Who can do what ----------------

        /// <summary>On the map, up, of a people at peace with the colony, and not held.</summary>
        public static bool Here(Pawn x, Map map) =>
            x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer) && !x.IsPrisoner;

        /// <summary>On their way out of the map.</summary>
        public static bool Leaving(Pawn x)
        {
            var duty = x?.mindState?.duty?.def;
            return duty != null && duty.defName.StartsWith("ExitMap");
        }

        private static bool Danger(Map map) => map.dangerWatcher.DangerRating != StoryDanger.None;

        /// <summary>One of this file's working jobs.</summary>
        public static bool Working(JobDef d) =>
            d != null && (d == CallerDefOf.Rimfolk_CallerView || d == CallerDefOf.Rimfolk_CallerCut || d == CallerDefOf.Rimfolk_CallerTend || d == CallerDefOf.Rimfolk_CallerTrain
                          || d == CallerDefOf.Rimfolk_CallerDig || d == CallerDefOf.Rimfolk_CallerBury || d == CallerDefOf.Rimfolk_CallerLoad || d == CallerDefOf.Rimfolk_CallerTalk);

        /// <summary>At something an answer of the colony's set going for them (a gathering, a visit): that comes first.</summary>
        private static bool AtPlay(Pawn x)
        {
            var d = x.CurJobDef;
            return d?.driverClass != null && !Working(d) && d.driverClass.Namespace == typeof(Callers).Namespace && d != HostingDefOf.Council_GuestSleep;
        }

        /// <summary>A visitor free to be set to the work now: awake and calm, not eating or resting, not hungry or worn out, not at something else of the colony's.</summary>
        public static bool Ready(Pawn x)
        {
            if (x.InMentalState || !x.Awake() || PlayOuts.NeedsPress(x) || AtPlay(x)) return false;
            var d = x.CurJobDef;
            return d != JobDefOf.Ingest && d != JobDefOf.LayDown && d != JobDefOf.LayDownResting && d != HostingDefOf.Council_GuestSleep;
        }

        /// <summary>One of the colony who can stand or walk over for a visitor's work: up, awake, free, nowhere they're needed.</summary>
        public static bool CanStand(Pawn p, Map map) =>
            p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.InBed()
            && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && (p.CurJob == null || !p.CurJob.playerForced);

        /// <summary>An animal of the colony that can stand still a while for the animal doctor: awake, up, calm, not being led or worked.</summary>
        public static bool CanHold(Pawn a, Map map) =>
            a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && !a.InMentalState && a.Awake() && !a.InBed()
            && (a.roping == null || !a.roping.IsRoped) && (a.CurJob == null || !a.CurJob.playerForced) && a.CurJobDef != JobDefOf.Ingest;

        /// <summary>Still at work on <paramref name="p"/> (cutting their hair, tending or training them, talking with them).</summary>
        public static bool WorkingOn(Pawn by, Pawn p) => by?.CurJob != null && Working(by.CurJobDef) && by.CurJob.targetA.Thing == p;

        /// <summary>Someone the caller works on stands still for it, facing them (a colonist only when free to; an animal when calm), for at most <paramref name="ticks"/>.</summary>
        public static void HoldStill(Pawn p, Pawn by, int ticks)
        {
            if (p == null || by == null || p.CurJobDef == CallerDefOf.Rimfolk_CallerHold || p.jobs == null) return;
            if (p.RaceProps.Humanlike ? !CanStand(p, p.Map) : !CanHold(p, p.Map)) return;
            var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerHold, by);
            job.count = ticks;
            // together: the caller is at their side already (their own job brought them there).
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, p.RaceProps.Humanlike, true);
        }

        /// <summary>A line from a caller at a moment of their work ("view", "cut", "tend", "train", "dig", "load", "talk", "night"), from the event's own lines.</summary>
        public static void Say(Pawn p, CallerTask t, string moment, float chance = 1f)
        {
            if (p == null || t?.def == null || !Rand.Chance(chance)) return;
            var lines = DefDatabase<IncidentDef>.GetNamedSilentFail(t.def)?.GetModExtension<HappeningExtension>()?.lines;
            string line = Flavor.PickTagged(lines, new HashSet<string> { moment });
            if (!line.NullOrEmpty()) Speech.Say(p, line);
        }

        /// <summary>
        /// The answer's words go to what its effects set going for it (the tasks made while it was being given), to be told
        /// when the work is done. False when nothing was set going: tell them now.
        /// </summary>
        public static bool Adopt(DilemmaContext ctx, string told)
        {
            var self = Instance;
            if (self == null || ctx == null || told.NullOrEmpty()) return false;
            bool any = false;
            foreach (var t in self.tasks)
            {
                if (t.ctxRef != ctx) continue;
                t.ctxRef = null;
                if (any || t.kind == CallerKind.Night || t.kind == CallerKind.View) continue;
                t.text = told;
                any = true;
            }
            return any;
        }

        private static CallerTask Of(CallerKind kind, Pawn x) => Instance?.tasks.FirstOrDefault(t => t.kind == kind && t.x == x);

        /// <summary>The visit stretched while there's work left (the game's own visits: VisitorServices.Stay), never past the task's limit.</summary>
        private static void Stretch(Pawn x, CallerTask t, int now)
        {
            if (x != null && now + Hold <= t.until) VisitorServices.Stay(x, Hold);
        }

        // ---------------- Each look ----------------

        private bool Step(CallerTask t, int now)
        {
            switch (t.kind)
            {
                case CallerKind.View: return StepView(t, now);
                case CallerKind.Hand: return StepHand(t, now);
                case CallerKind.Cut: return StepCut(t, now);
                case CallerKind.Vet: return StepVet(t, now);
                case CallerKind.Graves: return StepGraves(t, now);
                case CallerKind.Load: return StepLoad(t, now);
                case CallerKind.Talk: return StepTalk(t, now);
                case CallerKind.Free: return StepFree(t, now);
                case CallerKind.Night: return StepNight(t, now);
                default: return true;
            }
        }

        // ---------------- The collector's walk round the art ----------------

        /// <summary>The collector looks at <paramref name="art"/>, the nearest first, standing a while before each.</summary>
        public static CallerTask View(Map map, Pawn x, List<Thing> art, string def)
        {
            if (!PlayOuts.On || !Here(x, map) || art.NullOrEmpty()) return null;
            var t = Add(new CallerTask { kind = CallerKind.View, map = map, x = x, things = art.ToList(), total = art.Count, def = def, until = Now + GenDate.TicksPerHour * 12 });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        private static bool StepView(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            t.things.RemoveAll(th => th == null || th.Destroyed || !th.Spawned || th.Map != map);
            if (t.things.Count == 0 || !Here(x, map) || Leaving(x) || now > t.until) return true;
            Stretch(x, t, now);
            if (x.CurJobDef == CallerDefOf.Rimfolk_CallerView || Danger(map) || !Ready(x)) return false;
            foreach (var art in t.things.OrderBy(th => th.Position.DistanceToSquared(x.Position)).ToList())
            {
                var at = ViewCell(art, x);
                if (!at.IsValid)
                {
                    t.things.Remove(art);
                    continue;
                }
                var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerView, at, art);
                job.count = Rand.Range(500, 900);
                // apart: the collector on their own, before a piece of the colony's art.
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return false;
            }
            return false;
        }

        /// <summary>A place to stand and look at a piece: a step or two in front of it, free, and in reach.</summary>
        public static IntVec3 ViewCell(Thing art, Pawn x)
        {
            var map = art.Map;
            var rect = art.OccupiedRect();
            foreach (var c in GenRadial.RadialCellsAround(art.Position, 2.9f, false).OrderBy(c => Mathf.Abs(c.DistanceTo(art.Position) - 2f)))
            {
                if (!c.InBounds(map) || rect.Contains(c) || !c.Standable(map) || c.GetFirstPawn(map) != null) continue;
                if (!x.CanReach(c, PathEndMode.OnCell, Verse.Danger.Some)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A piece looked at: off the list, and now and then a word about it.</summary>
        public static void Viewed(Pawn x, Thing art)
        {
            var t = Of(CallerKind.View, x);
            if (t == null) return;
            t.things.Remove(art);
            t.done++;
            Say(x, t, "view", 0.45f);
        }

        // ---------------- A piece carried out to the collector ----------------

        /// <summary>
        /// The piece is carried out to the collector (taken up first if it stands in a room), by <paramref name="carrier"/>
        /// or whoever is free, and the collector pays <paramref name="pay"/> silver once it's in their hands.
        /// </summary>
        public static CallerTask Hand(Map map, Pawn x, Thing piece, Pawn carrier, int pay, string text, string def, int commission = -1)
        {
            if (!PlayOuts.On || !Here(x, map) || piece == null || piece.Destroyed || piece.MapHeld != map) return null;
            var t = Add(new CallerTask
            {
                kind = CallerKind.Hand, map = map, x = x, thing = piece, other = carrier, pay = pay, text = text, def = def, commission = commission, faction = x.Faction,
                until = Now + GenDate.TicksPerDay
            });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>The thing itself, or the packed thing it's in.</summary>
        public static Thing Outer(Thing th)
        {
            if (th == null) return null;
            if (th.ParentHolder is MinifiedThing m) return m;
            return th;
        }

        private static bool StepHand(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            var piece = Outer(t.thing);
            if (piece == null || piece.Destroyed || t.done > 0) return true;
            if (x?.inventory != null && x.inventory.innerContainer.Contains(piece)) return true;
            if (!Here(x, map) || Leaving(x) || now > t.until) return true;
            Stretch(x, t, now);
            var carrier = t.other;
            if (carrier != null && carrier.CurJobDef == CallerDefOf.Rimfolk_CallerCarryArt) return false;
            if (Danger(map) || !piece.Spawned) return false;
            bool Able(Pawn p) => PlayOuts.OurPerson(p) && CanStand(p, map) && !p.WorkTagIsDisabled(WorkTags.Hauling)
                                 && p.CanReserveAndReach(piece, PathEndMode.Touch, Verse.Danger.Some) && p.CanReach(x, PathEndMode.Touch, Verse.Danger.Some);
            if (!Able(carrier)) carrier = map.mapPawns.FreeColonistsSpawned.Where(Able).OrderBy(p => p.Position.DistanceToSquared(piece.Position)).FirstOrDefault();
            if (carrier == null) return false;
            t.other = carrier;
            var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerCarryArt, piece, x);
            job.count = 1;
            // together: the carrier takes the piece up and walks it over to the collector.
            carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, true, true);
            if (!t.begun)
            {
                t.begun = true;
                ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, null, "CC_Caller_Hand_Begin".Translate(carrier.Named("PAWN"), PlayGive.Label(piece).Named("THING"), x.Named("OTHER")).Resolve(), carrier, MessageTypeDefOf.SilentInput);
            }
            return false;
        }

        /// <summary>The piece is in the collector's hands: into their pack, and they pay.</summary>
        public static void Handed(Pawn carrier, Pawn x)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Hand && k.x == x && k.other == carrier);
            var held = carrier?.carryTracker?.CarriedThing;
            if (t == null || held == null || x?.inventory == null) return;
            if (carrier.carryTracker.innerContainer.TryTransferToContainer(held, x.inventory.innerContainer, held.stackCount) <= 0) return;
            t.done = 1;
            // together: the collector pays the one who brought it, there and then.
            if (t.pay > 0) Couriers.PaySilver(x.Map, x.Position, t.pay, x.Faction, x);
            if (t.commission >= 0) Instance.commissions.RemoveAll(c => c.id == t.commission);
            Say(x, t, "bought", 0.8f);
        }

        // ---------------- The barber ----------------

        /// <summary>The barber gives each of <paramref name="who"/> a new cut at their stool, one at a time.</summary>
        public static CallerTask Cut(Map map, Pawn x, List<Pawn> who, string text, string def)
        {
            if (!PlayOuts.On || !Here(x, map) || who.NullOrEmpty()) return null;
            var t = Add(new CallerTask { kind = CallerKind.Cut, map = map, x = x, pawns = who.ToList(), total = who.Count, text = text, def = def, until = Now + GenDate.TicksPerHour * 14 });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>Where the barber sets their stool: by the colony's styling station if it has one (Ideology), else by the gate.</summary>
        public static IntVec3 Stool(Map map, Pawn x)
        {
            var station = DefDatabase<ThingDef>.GetNamedSilentFail("StylingStation");
            if (station != null)
                foreach (var b in map.listerThings.ThingsOfDef(station))
                {
                    if (b.Faction != Faction.OfPlayer || b.IsBurning()) continue;
                    var c = b.def.hasInteractionCell ? b.InteractionCell : b.Position;
                    foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Prepend(c))
                        if (n.InBounds(map) && n.Standable(map) && n.GetFirstPawn(map) == null && x.CanReach(n, PathEndMode.OnCell, Verse.Danger.Some)) return n;
                }
            var gate = PlaySpots.GateCell(map, x);
            if (!gate.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(gate, 4f, true))
                if (c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && x.CanReach(c, PathEndMode.OnCell, Verse.Danger.Some))
                    return c;
            return IntVec3.Invalid;
        }

        private static bool StepCut(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            int before = t.pawns.Count;
            t.pawns.RemoveAll(p => p == null || p.Dead || p.Faction != Faction.OfPlayer || p.story == null);
            t.total -= before - t.pawns.Count;
            if (t.pawns.Count == 0 || !Here(x, map) || Leaving(x) || now > t.until) return true;
            Stretch(x, t, now);
            if (x.CurJobDef == CallerDefOf.Rimfolk_CallerCut || Danger(map) || !Ready(x)) return false;
            if (!t.cell.IsValid || !t.cell.Standable(map) || !x.CanReach(t.cell, PathEndMode.OnCell, Verse.Danger.Some)) t.cell = Stool(map, x);
            if (!t.cell.IsValid) return false;
            var next = t.pawns.Where(p => CanStand(p, map) && p.CanReach(t.cell, PathEndMode.Touch, Verse.Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(t.cell)).FirstOrDefault();
            if (next == null) return false;
            bool beard = next.style != null && next.style.CanWantBeard;
            var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerCut, next, t.cell);
            job.count = CallerMath.CutTicks(beard);
            // together: the barber waits at their stool and the colonist walks over to sit for it (their own job, set here).
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            var come = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerGetCut, x);
            come.count = job.count + 2500;
            next.jobs.StartJob(come, JobCondition.InterruptForced, null, true, true);
            if (!t.begun)
            {
                t.begun = true;
                ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, null, "CC_Caller_Cut_Begin".Translate(x.Named("PAWN"), t.pawns.Count).Resolve(), x, MessageTypeDefOf.SilentInput);
            }
            return false;
        }

        /// <summary>
        /// A cut done: a hairstyle the colonist's faith or taste likes (the one they had in mind, with Ideology), a beard
        /// to match where one grows, the clippings on the ground; a good talk meanwhile counts as recreation.
        /// </summary>
        public static void Cut(Pawn by, Pawn p)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Cut && k.x == by && k.pawns.Contains(p));
            if (t == null || p?.story == null) return;
            Restyle(p);
            t.pawns.Remove(p);
            t.done++;
            Say(by, t, "cut", 0.35f);
        }

        /// <summary>The new cut itself (also at once, with "Answers play out on the map" off).</summary>
        public static void Restyle(Pawn p)
        {
            if (p?.story == null) return;
            try
            {
                var style = p.style;
                var hair = style?.nextHairDef;
                for (int i = 0; i < 6 && (hair == null || hair == p.story.hairDef); i++) hair = PawnStyleItemChooser.RandomHairFor(p);
                if (hair != null) p.story.hairDef = hair;
                if (style != null)
                {
                    if (style.CanWantBeard)
                    {
                        var beard = style.nextBeardDef ?? PawnStyleItemChooser.RandomBeardFor(p);
                        if (beard != null) style.beardDef = beard;
                    }
                    style.nextHairDef = null;
                    style.nextBeardDef = null;
                    style.ResetNextStyleChangeAttemptTick();
                    style.Notify_StyleItemChanged();
                    if (style.ShouldSpawnHairFilth) style.MakeHairFilth();
                }
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                p.needs?.joy?.GainJoy(0.12f, JoyKindDefOf.Social);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("caller cut", e);
            }
        }

        // ---------------- The animal doctor ----------------

        /// <summary>The animal doctor tends each of <paramref name="patients"/> where they are, then (with <paramref name="trainee"/>) works that one a while.</summary>
        public static CallerTask Vet(Map map, Pawn x, List<Pawn> patients, Pawn trainee, string text, string def)
        {
            if (!PlayOuts.On || !Here(x, map) || (patients.NullOrEmpty() && trainee == null)) return null;
            var t = Add(new CallerTask
            {
                kind = CallerKind.Vet, map = map, x = x, pawns = patients?.ToList() ?? new List<Pawn>(), other = trainee, train = trainee != null,
                total = (patients?.Count ?? 0) + (trainee != null ? 1 : 0), text = text, def = def, until = Now + GenDate.TicksPerHour * 14
            });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>One of the colony's animals that should be seen to now (the game's own say: an injury or sickness to tend).</summary>
        public static bool Patient(Pawn a) =>
            a != null && !a.Dead && a.Spawned && a.RaceProps.Animal && a.Faction == Faction.OfPlayer && HealthAIUtility.ShouldBeTendedNowByPlayer(a);

        /// <summary>One of the colony's animals with training wanted that it hasn't got yet.</summary>
        public static bool Trainable(Pawn a) =>
            a != null && !a.Dead && a.Spawned && !a.Downed && a.RaceProps.Animal && a.Faction == Faction.OfPlayer && a.training != null && a.training.NextTrainableToTrain() != null;

        private static bool StepVet(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            t.pawns.RemoveAll(a => !Patient(a) || a.Map != map);
            if (t.train && !Trainable(t.other)) t.train = false;
            if ((t.pawns.Count == 0 && !t.train) || !Here(x, map) || Leaving(x) || now > t.until) return true;
            Stretch(x, t, now);
            var d = x.CurJobDef;
            if (d == CallerDefOf.Rimfolk_CallerTend || d == CallerDefOf.Rimfolk_CallerTrain || Danger(map) || !Ready(x)) return false;
            // The hurt and sick first, the nearest of them; asleep or lying down, they're tended where they lie.
            var next = t.pawns.Where(a => x.CanReach(a, PathEndMode.Touch, Verse.Danger.Some) && (a.Downed || !a.Awake() || CanHold(a, map)))
                .OrderBy(a => a.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (next != null)
            {
                var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerTend, next);
                job.count = Mathf.RoundToInt(700f / Mathf.Max(0.2f, x.GetStatValue(StatDefOf.MedicalTendSpeed)));
                // together: the animal doctor walks over to the animal and tends it where it is.
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                Begun(t, x);
                return false;
            }
            if (t.pawns.Count > 0 || !t.train) return false;
            var young = t.other;
            if (!CanHold(young, map) || !x.CanReach(young, PathEndMode.Touch, Verse.Danger.Some)) return false;
            var train = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerTrain, young);
            train.count = 2500;
            // together: the animal doctor walks over to the young one and works it there.
            x.jobs.StartJob(train, JobCondition.InterruptForced, null, false, true);
            Begun(t, x);
            return false;
        }

        private static void Begun(CallerTask t, Pawn x)
        {
            if (t.begun) return;
            t.begun = true;
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, null, "CC_Caller_Vet_Begin".Translate(x.Named("PAWN"), t.total).Resolve(), x, MessageTypeDefOf.SilentInput);
        }

        /// <summary>An animal tended with the game's own tend and the animal doctor's own medicine (what they carry), and off the list.</summary>
        public static void Tended(Pawn vet, Pawn a)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Vet && k.x == vet && k.pawns.Contains(a));
            if (t == null || a == null || a.Dead) return;
            TendNow(vet, a);
            t.pawns.Remove(a);
            t.done++;
            Say(vet, t, "tend", 0.35f);
        }

        /// <summary>The tend itself, the game's own, with the doctor's own medicine while it lasts (also at once, with "Answers play out on the map" off).</summary>
        public static void TendNow(Pawn vet, Pawn a)
        {
            if (vet == null || a == null || a.Dead) return;
            try
            {
                var med = vet.inventory?.innerContainer?.OfType<Medicine>().FirstOrDefault();
                TendUtility.DoTend(vet, a, med);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("caller tend", e);
            }
        }

        /// <summary>One step of what the animal is being taught, the game's own training (also at once, with "Answers play out on the map" off).</summary>
        public static void TrainNow(Pawn vet, Pawn a)
        {
            if (a?.training == null) return;
            try
            {
                var td = a.training.NextTrainableToTrain();
                if (td != null) a.training.Train(td, vet, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("caller train", e);
            }
        }

        /// <summary>The training session over: one step of what the animal is being taught, the game's own training.</summary>
        public static void Trained(Pawn vet, Pawn a)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Vet && k.x == vet && k.other == a && k.train);
            if (t == null || a?.training == null) return;
            TrainNow(vet, a);
            t.train = false;
            t.done++;
            Say(vet, t, "train", 0.6f);
        }

        // ---------------- The undertakers ----------------

        /// <summary>The undertakers dig a row of graves at <paramref name="plots"/> and lay <paramref name="dead"/> in them.</summary>
        public static CallerTask Graves(Map map, List<Pawn> people, List<Thing> dead, List<IntVec3> plots, Rot4 rot, string text, string def)
        {
            var x = people?.FirstOrDefault(p => Here(p, map));
            if (!PlayOuts.On || x == null || dead.NullOrEmpty() || plots.NullOrEmpty()) return null;
            var t = Add(new CallerTask
            {
                kind = CallerKind.Graves, map = map, x = x, people = people.ToList(), things = dead.Take(plots.Count).ToList(), plots = plots.ToList(), rot = rot.AsInt,
                total = Math.Min(dead.Count, plots.Count), text = text, def = def, until = Now + GenDate.TicksPerHour * 20
            });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>The undertakers carry <paramref name="dead"/> to <paramref name="beast"/> and load them on it.</summary>
        public static CallerTask Load(Map map, List<Pawn> people, List<Thing> dead, Pawn beast, string text, string def)
        {
            var x = people?.FirstOrDefault(p => Here(p, map));
            if (!PlayOuts.On || x == null || dead.NullOrEmpty() || beast == null || beast.Dead || !beast.Spawned) return null;
            var t = Add(new CallerTask
            {
                kind = CallerKind.Load, map = map, x = x, people = people.ToList(), things = dead.ToList(), other = beast, total = dead.Count, text = text, def = def,
                until = Now + GenDate.TicksPerHour * 16
            });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>A body still lying out for the undertakers: the enemy's, not yet laid in a grave or loaded.</summary>
        private static bool StillOut(Thing c) => c is Corpse && !c.Destroyed && c.Spawned && PlayHaul.EnemyCorpse(c);

        private static bool CarriedBy(Thing c, CallerTask t) =>
            t.people.Any(p => p != null && p.Spawned && (p.carryTracker?.CarriedThing == c || p.CurJob != null && p.CurJob.targetA.Thing == c && Working(p.CurJobDef)));

        private static bool StepGraves(CallerTask t, int now)
        {
            var map = t.map;
            var rot = new Rot4(t.rot);
            t.things.RemoveAll(c => c == null || c.Destroyed || (!c.Spawned && !(c.ParentHolder is Pawn_CarryTracker)) || c.ParentHolder is Building_Grave);
            var crew = t.people.Where(p => Here(p, map) && !Leaving(p)).ToList();
            if (t.things.Count == 0 || crew.Count == 0 || now > t.until) return true;
            Stretch(crew[0], t, now);
            if (Danger(map)) return false;
            foreach (var p in crew)
            {
                var d = p.CurJobDef;
                if (d == CallerDefOf.Rimfolk_CallerDig || d == CallerDefOf.Rimfolk_CallerBury || !Ready(p)) continue;
                // A grave dug and empty, and one of the dead nobody is carrying: into it.
                var grave = t.plots.Select(c => PlayBurial.GraveAt(map, c))
                    .FirstOrDefault(g => g != null && !g.HasAnyContents && !t.people.Any(o => o.CurJobDef == CallerDefOf.Rimfolk_CallerBury && o.CurJob.targetB.Thing == g));
                var body = t.things.Where(c => StillOut(c) && !CarriedBy(c, t) && p.CanReserveAndReach(c, PathEndMode.ClosestTouch, Verse.Danger.Some))
                    .OrderBy(c => c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (grave != null && body != null && p.CanReach(grave, PathEndMode.Touch, Verse.Danger.Some))
                {
                    var bury = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerBury, body, grave);
                    bury.count = 1;
                    // apart: an undertaker at their own work, carrying one of the dead to a grave.
                    p.jobs.StartJob(bury, JobCondition.InterruptForced, null, false, true);
                    continue;
                }
                // Else the next grave of the row, while there are dead without one.
                int graves = t.plots.Count(c => PlayBurial.GraveAt(map, c) is Building_Grave g && !g.HasAnyContents);
                int digging = t.people.Count(o => o.CurJobDef == CallerDefOf.Rimfolk_CallerDig);
                if (graves + digging >= t.things.Count) continue;
                var plot = IntVec3.Invalid;
                foreach (var c in t.plots)
                    if (PlayBurial.GraveAt(map, c) == null && !t.people.Any(o => o.CurJobDef == CallerDefOf.Rimfolk_CallerDig && o.CurJob.targetA.Cell == c)
                        && GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Grave, c, rot, map).Accepted && p.CanReach(c, PathEndMode.Touch, Verse.Danger.Some))
                    {
                        plot = c;
                        break;
                    }
                if (!plot.IsValid) continue;
                var dig = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerDig, plot);
                dig.count = 900;
                // apart: an undertaker at their own work, digging a grave.
                p.jobs.StartJob(dig, JobCondition.InterruptForced, null, false, true);
                Begun(t, crew[0], "CC_Caller_Graves_Begin");
            }
            return false;
        }

        private static void Begun(CallerTask t, Pawn x, string key)
        {
            if (t.begun) return;
            t.begun = true;
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, null, key.Translate(x.Named("PAWN"), t.total).Resolve(), x, MessageTypeDefOf.SilentInput);
        }

        /// <summary>A grave dug: the game's own grave stands there now, the colony's, with fresh earth about it.</summary>
        public static void Dug(Pawn p, IntVec3 c)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Graves && k.people.Contains(p) && k.plots.Contains(c));
            var map = p?.Map;
            if (t == null || map == null || PlayBurial.GraveAt(map, c) != null) return;
            if (DigNow(map, c, new Rot4(t.rot)) != null) Say(p, t, "dig", 0.3f);
        }

        /// <summary>A grave of the game's own, the colony's, at <paramref name="c"/>, with fresh earth about it (also at once, with "Answers play out on the map" off).</summary>
        public static Building_Grave DigNow(Map map, IntVec3 c, Rot4 rot)
        {
            if (map == null || !GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Grave, c, rot, map).Accepted) return null;
            var grave = ThingMaker.MakeThing(ThingDefOf.Grave);
            grave.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(grave, c, map, rot, WipeMode.VanishOrMoveAside);
            TakeStrangers(grave as Building_Grave);
            foreach (var x in GenAdj.CellsAdjacent8Way(grave).Take(3))
                if (x.InBounds(map) && x.Standable(map)) FilthMaker.TryMakeFilth(x, map, ThingDefOf.Filth_Dirt);
            return grave as Building_Grave;
        }

        /// <summary>The graves the undertakers dig take strangers' bodies, never one of the colony's own (as the colony's own burial of raiders sets them).</summary>
        private static void TakeStrangers(Building_Grave g)
        {
            var filter = g?.GetStoreSettings()?.filter;
            if (filter == null) return;
            var stranger = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesStranger");
            var colonist = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesColonist");
            if (stranger != null && !filter.Allows(stranger)) filter.SetAllow(stranger, true);
            if (colonist != null && filter.Allows(colonist)) filter.SetAllow(colonist, false);
        }

        /// <summary>One of the dead laid in a grave the undertakers dug.</summary>
        public static void Buried(Pawn p, Building_Grave grave)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Graves && k.people.Contains(p));
            var held = p?.carryTracker?.CarriedThing;
            if (t == null || held == null || grave == null || grave.HasAnyContents) return;
            if (!grave.TryAcceptThing(held, true)) return;
            t.things.Remove(held);
            t.done++;
        }

        private static bool StepLoad(CallerTask t, int now)
        {
            var map = t.map;
            var beast = t.other;
            t.things.RemoveAll(c => c == null || c.Destroyed || (!c.Spawned && !(c.ParentHolder is Pawn_CarryTracker)));
            var crew = t.people.Where(p => Here(p, map) && !Leaving(p)).ToList();
            if (t.things.Count == 0 || crew.Count == 0 || beast == null || beast.Dead || !beast.Spawned || beast.Map != map || now > t.until) return true;
            Stretch(crew[0], t, now);
            if (Danger(map)) return false;
            foreach (var p in crew)
            {
                if (p.CurJobDef == CallerDefOf.Rimfolk_CallerLoad || !Ready(p)) continue;
                var body = t.things.Where(c => StillOut(c) && !CarriedBy(c, t) && p.CanReserveAndReach(c, PathEndMode.ClosestTouch, Verse.Danger.Some))
                    .OrderBy(c => c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (body == null || !p.CanReach(beast, PathEndMode.Touch, Verse.Danger.Some)) continue;
                var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerLoad, body, beast);
                job.count = 1;
                // apart: an undertaker at their own work, carrying one of the dead to their beast.
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                Begun(t, crew[0], "CC_Caller_Load_Begin");
            }
            return false;
        }

        /// <summary>The raiders' weapons lying by a body: what the undertakers take with it.</summary>
        public static List<Thing> GearBy(Thing body)
        {
            var list = new List<Thing>();
            if (body?.Map == null) return list;
            foreach (var c in GenRadial.RadialCellsAround(body.Position, 2.5f, true))
            {
                if (!c.InBounds(body.Map)) continue;
                foreach (var th in c.GetThingList(body.Map))
                    if (th.def.IsWeapon && th.def.category == ThingCategory.Item && !th.IsInAnyStorage() && !th.IsForbidden(Faction.OfPlayer)) list.Add(th);
            }
            return list;
        }

        /// <summary>The body taken up: the weapons lying by it go into the undertaker's pack.</summary>
        public static void TookUp(Pawn p, Thing body)
        {
            try
            {
                var inv = p?.inventory?.innerContainer;
                if (inv == null) return;
                foreach (var w in GearBy(body))
                {
                    if (w.Spawned) w.DeSpawn();
                    if (!inv.TryAdd(w) && !w.Destroyed) GenPlace.TryPlaceThing(w, p.Position, p.Map, ThingPlaceMode.Near);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("undertakers' gear", e);
            }
        }

        /// <summary>One of the dead loaded on the undertakers' beast, to go with them.</summary>
        public static void Loaded(Pawn p, Pawn beast)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Load && k.people.Contains(p) && k.other == beast);
            var held = p?.carryTracker?.CarriedThing;
            if (t == null || held == null || beast?.inventory == null) return;
            if (p.carryTracker.innerContainer.TryTransferToContainer(held, beast.inventory.innerContainer, held.stackCount) <= 0) return;
            t.things.Remove(held);
            t.done++;
            Say(p, t, "load", 0.25f);
        }

        // ---------------- A captive's kin ----------------

        /// <summary>One of the kin (<paramref name="x"/>) is shown to the captive's door and talks with them a while through it.</summary>
        public static CallerTask Talk(Map map, Pawn x, Pawn captive, string text, string def)
        {
            if (!PlayOuts.On || !Here(x, map) || captive == null || captive.Dead || !captive.Spawned) return null;
            var t = Add(new CallerTask { kind = CallerKind.Talk, map = map, x = x, other = captive, text = text, def = def, until = Now + GenDate.TicksPerHour * 12, total = 1 });
            VisitorServices.Stay(x, Hold);
            return t;
        }

        /// <summary>Where one of the kin stands to talk with the captive: by the door of their room on the outside, else at their side.</summary>
        public static IntVec3 DoorCell(Pawn captive, Pawn x)
        {
            var map = captive.Map;
            var room = captive.GetRoom();
            if (room != null && !room.PsychologicallyOutdoors)
                foreach (var door in room.ContainedAndAdjacentThings.OfType<Building_Door>())
                    foreach (var c in GenAdj.CellsAdjacentCardinal(door))
                        if (c.InBounds(map) && c.GetRoom(map) != room && c.Standable(map) && c.GetFirstPawn(map) == null && x.CanReach(c, PathEndMode.OnCell, Verse.Danger.Some))
                            return c;
            foreach (var c in GenAdj.CellsAdjacent8Way(captive))
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && x.CanReach(c, PathEndMode.OnCell, Verse.Danger.Some))
                    return c;
            return IntVec3.Invalid;
        }

        private static bool StepTalk(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            var c = t.other;
            if (t.done > 0 || c == null || c.Dead || !c.Spawned || c.Map != map || !(c.IsPrisonerOfColony || c.IsSlaveOfColony)) return true;
            if (!Here(x, map) || Leaving(x) || now > t.until) return true;
            Stretch(x, t, now);
            if (x.CurJobDef == CallerDefOf.Rimfolk_CallerTalk || Danger(map) || !Ready(x)) return false;
            var at = DoorCell(c, x);
            if (!at.IsValid) return false;
            var job = JobMaker.MakeJob(CallerDefOf.Rimfolk_CallerTalk, c, at);
            job.count = GenDate.TicksPerHour;
            // together: the kin walks to the captive's door and talks with them through it.
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (!t.begun)
            {
                t.begun = true;
                ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, null, "CC_Caller_Talk_Begin".Translate(x.Named("PAWN"), c.Named("OTHER")).Resolve(), x, MessageTypeDefOf.SilentInput);
            }
            return false;
        }

        /// <summary>The talk at the door over: their family's words soften the captive, and the kin set off home.</summary>
        public static void Talked(Pawn x, Pawn captive)
        {
            var t = Instance?.tasks.FirstOrDefault(k => k.kind == CallerKind.Talk && k.x == x && k.other == captive);
            if (t == null || captive?.guest == null) return;
            t.done = 1;
            if (captive.IsPrisonerOfColony)
            {
                captive.guest.resistance = Mathf.Max(0f, captive.guest.resistance - CallerMath.TalkResistance);
                captive.guest.will = Mathf.Max(0f, captive.guest.will - CallerMath.TalkWill);
            }
            Say(x, t, "talk", 0.7f);
            DilemmaEffect_SendAway.Send(x);
        }

        /// <summary>The captive goes home with their kin: the wardens release them (the game's own), and they walk out with the kin.</summary>
        public static CallerTask Free(Map map, Pawn x, Pawn captive, string text)
        {
            if (!PlayOuts.On || !Here(x, map) || captive == null || captive.Dead || !captive.Spawned) return null;
            var t = Add(new CallerTask
            {
                kind = CallerKind.Free, map = map, x = x, other = captive, faction = x.Faction, text = text, until = Now + GenDate.TicksPerHour * 10, total = 1
            });
            if (captive.IsPrisonerOfColony) captive.guest?.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
            VisitorServices.Stay(x, Hold);
            return t;
        }

        private static bool StepFree(CallerTask t, int now)
        {
            var map = t.map;
            var x = t.x;
            var c = t.other;
            if (t.done > 0 || c == null || c.Dead) return true;
            bool kin = Here(x, map) && !Leaving(x);
            // A slave is let go there and then (the game has no warden's walk for it).
            if (c.IsSlaveOfColony) GenGuest.SlaveRelease(c);
            if (c.IsPrisonerOfColony)
            {
                if (!kin) return true;
                Stretch(x, t, now);
                if (c.guest != null && c.guest.ExclusiveInteractionMode != PrisonerInteractionModeDefOf.Release) c.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
                // Nobody has walked them out by the time the kin must go: the door is opened for them.
                if (now <= t.until - GenDate.TicksPerHour * 2) return false;
                GenGuest.PrisonerRelease(c);
            }
            if (!c.Spawned || c.Map != map || !kin) return true;
            // Free: they go home with their kin, to live among their people.
            try
            {
                if (c.Faction == null || c.Faction.HostileTo(Faction.OfPlayer)) c.SetFaction(t.faction ?? x.Faction);
                var lord = x.GetLord();
                c.GetLord()?.RemovePawn(c);
                if (lord != null && lord.CanAddPawn(c)) lord.AddPawn(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("captive home with kin", e);
            }
            t.done = 1;
            Say(x, t, "free", 0.8f);
            // together: the freed captive is one of the kin's party now, and they walk off together.
            DilemmaEffect_SendAway.Send(x);
            return true;
        }

        // ---------------- A wedding party's night ----------------

        /// <summary>
        /// A wedding party's night at the colony, until first light: under its roof (<paramref name="inside"/>: each in one
        /// of the colony's spare beds, the couple in one, else on the floor of a warm room), or round their own fire beyond
        /// the wall.
        /// </summary>
        public static CallerTask Night(Map map, List<Pawn> people, bool inside, string def)
        {
            var x = people?.FirstOrDefault(p => Here(p, map));
            if (!PlayOuts.On || x == null) return null;
            int until = Now + Mathf.RoundToInt((Hosting.HoursToMorning(map) + (GenLocalDate.HourOfDay(map) < 7 ? 24f : 0f)) * GenDate.TicksPerHour);
            var t = Add(new CallerTask { kind = CallerKind.Night, map = map, x = x, people = people.ToList(), inside = inside, faction = x.Faction, def = def, until = until });
            VisitorServices.Stay(x, until - Now + GenDate.TicksPerHour);
            return t;
        }

        private static bool StepNight(CallerTask t, int now)
        {
            var map = t.map;
            var party = t.people.Where(p => Here(p, map) && !Leaving(p)).ToList();
            if (party.Count == 0 || now > t.until) return true;
            VisitorServices.Stay(party[0], t.until - now + GenDate.TicksPerHour);
            int hour = GenLocalDate.HourOfDay(map);
            if (!t.inside) Fire(t, party, hour);
            // First light after their night: up, and the visit ends as it would.
            if (hour >= 6 && hour < 12 && t.slept)
            {
                foreach (var p in party) Hosting.GetUp(p);
                return true;
            }
            if (party.Any(p => p.CurJobDef == HostingDefOf.Council_GuestSleep && !p.Awake())) t.slept = true;
            if (Danger(map)) return false;
            foreach (var p in party)
            {
                if (p.CurJobDef == HostingDefOf.Council_GuestSleep || !Hosting.WantsSleep(p) || p.InMentalState || !p.Awake() || p.CurJobDef == JobDefOf.Ingest || AtPlay(p)) continue;
                if (t.inside)
                {
                    // apart: each guest beds down on their own, in a spare bed of the colony's (the couple sharing one) or on the floor.
                    var bed = Hosting.Beds(map, o => t.people.Contains(o), p).FirstOrDefault(b => Hosting.CanShare(b, p) && (b.OwnersForReading.Contains(p) || b.OwnersForReading.Count < b.SleepingSlotsCount));
                    if (bed == null || !Hosting.Sleep(p, bed)) Hosting.SleepRough(p);
                    continue;
                }
                var cell = FireSide(t, p);
                if (cell.IsValid)
                {
                    var job = JobMaker.MakeJob(HostingDefOf.Council_GuestSleep, cell);
                    job.expiryInterval = GenDate.TicksPerHour * 12;
                    // apart: each guest beds down on their own, on the ground by their fire.
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                else Hosting.SleepRough(p);
            }
            return false;
        }

        /// <summary>Their own fire beyond the wall: laid of an evening (a campfire of theirs), fed through the night.</summary>
        private static void Fire(CallerTask t, List<Pawn> party, int hour)
        {
            var map = t.map;
            if (t.thing == null && (hour >= 17 || hour < 6))
            {
                var from = party[0].Position;
                IntVec3 spot = IntVec3.Invalid;
                if (!RCellFinder.TryFindRandomSpotJustOutsideColony(from, map, out spot)) spot = from;
                foreach (var c in GenRadial.RadialCellsAround(spot, 6f, true))
                {
                    if (!VisitorWork.OutsideOk(c, map) || c.Roofed(map) || c.GetFirstPawn(map) != null || c.GetFirstItem(map) != null || c.GetFirstBuilding(map) != null) continue;
                    if (map.areaManager?.Home?[c] ?? false) continue;
                    var terrain = c.GetTerrain(map);
                    var def = ThingDefOf.Campfire;
                    if (terrain == null || terrain.IsWater || def.terrainAffordanceNeeded != null && !terrain.affordances.Contains(def.terrainAffordanceNeeded)) continue;
                    if (!party.All(p => p.CanReach(c, PathEndMode.Touch, Verse.Danger.Some))) continue;
                    var fire = ThingMaker.MakeThing(def);
                    fire.SetFaction(t.faction);
                    GenSpawn.Spawn(fire, c, map);
                    t.thing = fire;
                    t.cell = c;
                    Say(party[0], t, "fire", 0.6f);
                    break;
                }
            }
            var fuel = (t.thing as ThingWithComps)?.GetComp<CompRefuelable>();
            if (fuel != null && fuel.FuelPercentOfMax < 0.5f) fuel.Refuel(fuel.Props.fuelCapacity - fuel.Fuel);
        }

        /// <summary>A place on the ground in the ring round their fire, if the night there won't hurt them.</summary>
        private static IntVec3 FireSide(CallerTask t, Pawn p)
        {
            var fire = t.thing;
            if (fire == null || !fire.Spawned) return IntVec3.Invalid;
            if (!VisitorCamp.RingCell(p, fire.Position, out var at)) return IntVec3.Invalid;
            return Visitors.SafeOvernight(new[] { p }, at, t.map) ? at : IntVec3.Invalid;
        }

        // ---------------- Commissions ----------------

        /// <summary>A commission set going: the maker's piece (being made at the bench, PlayMake) is the collector's to collect.</summary>
        public static Commission Commission(Pawn collector, Pawn maker, ThingDef def, int playId, Map map)
        {
            var self = Instance;
            if (self == null || collector == null || maker == null || def == null) return null;
            int floor = 0;
            foreach (var th in map.listerThings.AllThings) floor = Math.Max(floor, th.thingIDNumber);
            var c = new Commission
            {
                id = self.nextCommission++, collector = collector, faction = collector.Faction, maker = maker, def = def, playId = playId, idFloor = floor, created = Now
            };
            self.commissions.Add(c);
            while (self.commissions.Count > MaxCommissions) self.commissions.RemoveAt(0);
            // Kept in the world until they come back for it.
            if (!Find.WorldPawns.ForcefullyKeptPawns.Contains(collector)) Find.WorldPawns.ForcefullyKeptPawns.Add(collector);
            return c;
        }

        /// <summary>A finished commission whose collector can come for it now on this map, or null.</summary>
        public static Commission Due(Map map)
        {
            var self = Instance;
            if (self == null || map == null) return null;
            int now = Now;
            foreach (var c in self.commissions)
            {
                if (c.made == null || c.made.Destroyed || c.made.MapHeld != map || c.madeTick < 0) continue;
                if (now - c.madeTick < CallerMath.CommissionReturnDays * GenDate.TicksPerDay) continue;
                var x = c.collector;
                if (x == null || x.Dead || x.Discarded || x.Spawned || x.Faction != c.faction || !Arrivals.Usable(c.faction)) continue;
                return c;
            }
            return null;
        }

        private void StepCommissions(int now)
        {
            foreach (var c in commissions.ToList())
            {
                // The piece once it's made: the newest of its kind by its maker.
                if (c.made == null && c.madeTick < 0) FindMade(c, now);
                bool gone = c.collector == null || c.collector.Dead || c.collector.Discarded || c.faction == null || c.faction.HostileTo(Faction.OfPlayer) || c.faction.defeated;
                bool stale = now - c.created > CallerMath.CommissionKeepDays * GenDate.TicksPerDay;
                bool lost = c.madeTick >= 0 && (c.made == null || c.made.Destroyed);
                if (!gone && !stale && !lost) continue;
                commissions.Remove(c);
                if (c.collector != null && !c.collector.Spawned) Find.WorldPawns.ForcefullyKeptPawns.Remove(c.collector);
                if (c.made != null && !c.made.Destroyed && (gone || stale))
                    News.Post("CC_Caller_Commission_Kept".Translate((c.collector?.LabelShort ?? "?").Named("OTHER"), PlayGive.Label(c.made).Named("THING")).Resolve(),
                        new LookTargets(Outer(c.made)), NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        private static void FindMade(Commission c, int now)
        {
            var play = PlayOutTracker.Instance?.plays.FirstOrDefault(p => p.id == c.playId);
            Thing found = null;
            string author = c.maker?.Name?.ToStringFull;
            foreach (var map in Find.Maps)
            {
                foreach (var th in map.listerThings.ThingsOfDef(c.def).Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing).Select(m => ((MinifiedThing)m).InnerThing)))
                {
                    if (th == null || th.def != c.def || th.thingIDNumber <= c.idFloor) continue;
                    var art = th.TryGetComp<CompArt>();
                    if (art != null && author != null && !art.AuthorName.ToString().Contains(author)) continue;
                    if (found == null || th.thingIDNumber > found.thingIDNumber) found = th;
                }
            }
            if (found != null)
            {
                c.made = found;
                c.madeTick = now;
                return;
            }
            // The making never came about (the maker never got to it): nothing to collect.
            if (play == null || play.state == PlayState.Done) c.madeTick = now;
        }

        public static void Settled(int id) => Instance?.commissions.RemoveAll(c => c.id == id);

        // ---------------- The end ----------------

        private void Finish(CallerTask t)
        {
            switch (t.kind)
            {
                case CallerKind.View:
                    Stop(t.x, CallerDefOf.Rimfolk_CallerView);
                    return;
                case CallerKind.Hand:
                    if (t.other != null && t.other.CurJobDef == CallerDefOf.Rimfolk_CallerCarryArt) t.other.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    if (t.done > 0) Tell(t, true, null, null);
                    else Tell(t, false, null, "CC_Caller_Hand_None");
                    return;
                case CallerKind.Cut:
                    Stop(t.x, CallerDefOf.Rimfolk_CallerCut);
                    Tell(t, t.pawns.Count == 0, "CC_Caller_Cut_Part", "CC_Caller_Cut_None");
                    return;
                case CallerKind.Vet:
                    Stop(t.x, CallerDefOf.Rimfolk_CallerTend);
                    Stop(t.x, CallerDefOf.Rimfolk_CallerTrain);
                    Tell(t, t.pawns.Count == 0 && !t.train, "CC_Caller_Vet_Part", "CC_Caller_Vet_None");
                    return;
                case CallerKind.Graves:
                    foreach (var p in t.people) Stop(p, CallerDefOf.Rimfolk_CallerDig);
                    Tell(t, t.things.Count == 0, "CC_Caller_Graves_Part", "CC_Caller_Graves_None");
                    if (t.x != null && Here(t.x, t.map) && !Leaving(t.x)) DilemmaEffect_SendAway.Send(t.x);
                    return;
                case CallerKind.Load:
                    Tell(t, t.things.Count == 0, "CC_Caller_Load_Part", "CC_Caller_Load_None");
                    // Home with the dead on their beast.
                    if (t.x != null && Here(t.x, t.map) && !Leaving(t.x)) DilemmaEffect_SendAway.Send(t.x);
                    return;
                case CallerKind.Talk:
                    Stop(t.x, CallerDefOf.Rimfolk_CallerTalk);
                    Tell(t, t.done > 0, null, "CC_Caller_Talk_None");
                    return;
                case CallerKind.Free:
                    Tell(t, t.done > 0, null, "CC_Caller_Free_None");
                    return;
                case CallerKind.Night:
                    foreach (var p in t.people)
                    {
                        Hosting.GetUp(p);
                        Hosting.GiveBack(p, t.map);
                    }
                    // Their fire put out as they go: the ash stays where it burned.
                    if (t.thing != null && t.thing.Spawned)
                    {
                        var at = t.thing.Position;
                        var map = t.thing.Map;
                        t.thing.Destroy();
                        if (ThingDefOf.Filth_Ash != null) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 2);
                    }
                    return;
            }
        }

        private static void Stop(Pawn x, JobDef def)
        {
            if (x != null && x.Spawned && x.CurJobDef == def) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>The answer's words once it's all done; else how far the visitor got before they had to go.</summary>
        private static void Tell(CallerTask t, bool all, string partKey, string noneKey)
        {
            var x = t.x;
            if (t.map == null || !Find.Maps.Contains(t.map)) return;
            var look = x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (all && t.done > 0)
            {
                if (!t.text.NullOrEmpty()) News.Post(t.text, look, NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                return;
            }
            if (all && partKey != null) return;
            string key = t.done > 0 && partKey != null ? partKey : noneKey;
            if (key == null) return;
            string text = key.Translate((x?.LabelShort ?? "?").Named("PAWN"), t.done, Math.Max(t.done, t.total)).Resolve();
            News.Post(text, look, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }
    }
}
