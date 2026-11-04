using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Where a journey plan stands: made, its last day under way, its last hours, or waiting for the player's word.</summary>
    public enum JourneyStage
    {
        /// <summary>Made; more than a day off, so nothing has changed yet.</summary>
        Planned,
        /// <summary>The last day: the travellers' timetables shifted for the night before.</summary>
        Preparing,
        /// <summary>The last hours: excused from work, dressing, packing, a meal, a wash, to the gate.</summary>
        Final,
        /// <summary>The hour came and someone wasn't ready: a letter asks what to do, and they wait at the gate.</summary>
        Asking
    }

    /// <summary>
    /// One line of a journey's packing list: what, how many, what for, and whose it is (the planner's suggestion, a number
    /// the player set, a line the player added from the stores, or a suggestion the player took off).
    /// </summary>
    public class JourneyLine : IExposable
    {
        public ThingDef def;
        public int count;
        /// <summary>
        /// food, medicine, drug (for a traveller's addiction or chemical dependency), bed, tent, feed (for the animals) or
        /// other (anything else). Drugs, feed and anything else go with the caravan's goods, loaded at the gate, rather
        /// than in the travellers' own packs.
        /// </summary>
        public string kind;
        /// <summary>The player set this number: kept when the list is worked out again.</summary>
        public bool edited;
        /// <summary>The player added this line from the stores: theirs, kept as they set it.</summary>
        public bool added;
        /// <summary>The player took this suggestion off: the planner leaves it off until the list is worked out again.</summary>
        public bool removed;

        public JourneyLine()
        {
        }

        public JourneyLine(ThingDef def, int count, string kind)
        {
            this.def = def;
            this.count = count;
            this.kind = kind;
        }

        /// <summary>The player's line: a number they set, a line they added, or a suggestion they took off.</summary>
        public bool Player => edited || added || removed;

        /// <summary>On the list to take (not taken off, more than none).</summary>
        public bool Taken => !removed && count > 0 && def != null;

        public JourneyLine Copy() => new JourneyLine(def, count, kind) { edited = edited, added = added, removed = removed };

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref edited, "edited");
            Scribe_Values.Look(ref added, "added");
            Scribe_Values.Look(ref removed, "removed");
        }
    }

    /// <summary>A traveller eating by the road's food policy, with their own to go back to at home (Journeys/RoadFood.cs).</summary>
    public class RoadFoodEntry : IExposable
    {
        public Pawn pawn;
        public FoodPolicy own;
        public FoodPolicy road;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref own, "own");
            Scribe_References.Look(ref road, "road");
        }
    }

    /// <summary>A timetable hour a journey changed for a traveller, with what it was, put back when they set off or it's called off.</summary>
    public class JourneySlot : IExposable
    {
        public Pawn pawn;
        public int hour;
        public TimeAssignmentDef was, set;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref hour, "hour");
            Scribe_Defs.Look(ref was, "was");
            Scribe_Defs.Look(ref set, "set");
        }
    }

    /// <summary>
    /// A journey the player planned: who goes (colonists and animals), where (a town, a place, or nowhere in particular
    /// for a chosen length), when (an hour on a day), and what they take. Its last day and hours play out in
    /// JourneyRun.cs; what to pack and wear, the way out and the gate are JourneyPack.cs.
    /// </summary>
    public class JourneyPlan : IExposable
    {
        public int id;
        public Map map;
        public List<Pawn> travellers = new List<Pawn>();
        public List<Pawn> animals = new List<Pawn>();
        /// <summary>The destination's tile on the home's layer of the world, or -1 for none.</summary>
        public int destTile = -1;
        /// <summary>The world object there (a town, a site) by its ID, or -1.</summary>
        public int destObject = -1;
        public string destName;
        /// <summary>The neighbouring tile they leave toward (chosen once when there's no destination).</summary>
        public int startTile = -1;
        /// <summary>When they set off, in the game's absolute ticks (on the hour).</summary>
        public int departAbs;
        public bool andBack = true;
        /// <summary>Days on the road the packing is for.</summary>
        public float days = 2f;
        /// <summary>The player chose the length rather than the caravan's estimate.</summary>
        public bool daysChosen;
        /// <summary>The weather of the road to dress for (NaN when not worked out).</summary>
        public float low = float.NaN, high = float.NaN;
        public List<JourneyLine> packing = new List<JourneyLine>();
        public JourneyStage stage;
        public IntVec3 gate = IntVec3.Invalid, exit = IntVec3.Invalid;
        /// <summary>Hours waited at the gate for someone who wasn't ready.</summary>
        public int waited;
        /// <summary>The last hours' steps each traveller has done ("thingID:Step").</summary>
        public List<string> done = new List<string>();
        public List<JourneySlot> slots = new List<JourneySlot>();

        /// <summary>Each traveller's share to carry (by def), and what each changes into: worked out when the last hours start, and again after a load.</summary>
        [Unsaved] public Dictionary<Pawn, Dictionary<ThingDef, int>> shares;
        [Unsaved] public Dictionary<Pawn, List<Apparel>> clothes;
        /// <summary>The step job each traveller was last given, to leave them to it.</summary>
        [Unsaved] public readonly Dictionary<int, Job> issued = new Dictionary<int, Job>();
        [Unsaved] private HashSet<int> ids;

        public bool HasDestination => destTile >= 0;

        public PlanetTile DestTile => map == null || destTile < 0 ? PlanetTile.Invalid : new PlanetTile(destTile, map.Tile.Layer);

        public string Dest => destName.NullOrEmpty() ? "CC_Journey_NoDestName".Translate().ToString() : destName;

        public bool Has(Pawn p) => p != null && Ids.Contains(p.thingIDNumber);

        private HashSet<int> Ids => ids ??= new HashSet<int>(travellers.Where(p => p != null).Select(p => p.thingIDNumber));

        /// <summary>Who goes changed: the shares and clothes are worked out again.</summary>
        public void Changed()
        {
            ids = null;
            shares = null;
            clothes = null;
            JourneyBook.Touch();
        }

        public bool Done(Pawn p, JourneyMath.Step s) => p != null && done.Contains(p.ThingID + ":" + s);

        public void MarkDone(Pawn p, JourneyMath.Step s)
        {
            if (p == null) return;
            string key = p.ThingID + ":" + s;
            if (!done.Contains(key)) done.Add(key);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref travellers, "travellers", LookMode.Reference);
            Scribe_Collections.Look(ref animals, "animals", LookMode.Reference);
            Scribe_Values.Look(ref destTile, "destTile", -1);
            Scribe_Values.Look(ref destObject, "destObject", -1);
            Scribe_Values.Look(ref destName, "destName");
            Scribe_Values.Look(ref startTile, "startTile", -1);
            Scribe_Values.Look(ref departAbs, "departAbs");
            Scribe_Values.Look(ref andBack, "andBack", true);
            Scribe_Values.Look(ref days, "days", 2f);
            Scribe_Values.Look(ref daysChosen, "daysChosen");
            Scribe_Values.Look(ref low, "low", float.NaN);
            Scribe_Values.Look(ref high, "high", float.NaN);
            Scribe_Collections.Look(ref packing, "packing", LookMode.Deep);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref gate, "gate", IntVec3.Invalid);
            Scribe_Values.Look(ref exit, "exit", IntVec3.Invalid);
            Scribe_Values.Look(ref waited, "waited");
            Scribe_Collections.Look(ref done, "done", LookMode.Value);
            Scribe_Collections.Look(ref slots, "slots", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                travellers ??= new List<Pawn>();
                travellers.RemoveAll(p => p == null);
                animals ??= new List<Pawn>();
                animals.RemoveAll(p => p == null);
                packing ??= new List<JourneyLine>();
                packing.RemoveAll(l => l?.def == null);
                done ??= new List<string>();
                slots ??= new List<JourneySlot>();
                slots.RemoveAll(s => s?.pawn == null || s.was == null || s.set == null);
            }
        }
    }

    [DefOf]
    public static class JourneyDefOf
    {
        public static JobDef Rimfolk_JourneyWait;
        public static LetterDef Rimfolk_JourneyLetter;

        static JourneyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JourneyDefOf));
        }
    }

    /// <summary>
    /// The colony's journey plan, one at a time: a game component of its own, looked at every 250 ticks at
    /// <see cref="Sched.RareJourneys"/> only while there is a plan (JourneyRun.cs does the work).
    /// </summary>
    public class JourneyBook : GameComponent
    {
        public JourneyPlan plan;
        public int nextId = 1;
        /// <summary>The food policy the colony's travellers eat by on the road, chosen by the player; null keeps their own (Journeys/RoadFood.cs).</summary>
        public FoodPolicy roadPolicy;
        /// <summary>Travellers eating by <see cref="roadPolicy"/> on the road, with their own policy to go back to at home.</summary>
        public List<RoadFoodEntry> roadFood = new List<RoadFoodEntry>();
        /// <summary>The caravans were looked at once since the load.</summary>
        [Unsaved] private bool roadLooked;

        private static JourneyBook cached;
        private static Game cachedGame;
        private static int version;

        /// <summary>Bumped on every change (the panels rebuild when it moves).</summary>
        public static int Version => version;

        public static void Touch() => version++;

        public JourneyBook(Game game)
        {
        }

        public static JourneyBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<JourneyBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public static bool On => ColonyCouncilMod.Settings?.journeys ?? true;

        /// <summary>The plan, while there is one and the setting is on.</summary>
        public static JourneyPlan Plan => On ? Instance?.plan : null;

        /// <summary>A traveller in the last hours before setting off (or waiting at the gate): excused from work and from Rimfolk's gatherings.</summary>
        public static bool Excused(Pawn p)
        {
            var plan = Plan;
            return plan != null && plan.stage >= JourneyStage.Final && plan.Has(p);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Touch();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareJourneys)) return;
            if (plan != null)
            {
                try
                {
                    JourneyRun.Rare(this, plan);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journeys", e);
                }
            }
            // The food policy on the road: the caravans once an hour, inside this pass (and on its first look after a load).
            if (roadLooked && !Sched.Every(now, Sched.HourTicks, Sched.RareJourneys)) return;
            roadLooked = true;
            if (roadPolicy == null && roadFood.Count == 0) return;
            try
            {
                RoadFood.Hour(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the food policy on the road", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref plan, "plan");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_References.Look(ref roadPolicy, "roadPolicy");
            Scribe_Collections.Look(ref roadFood, "roadFood", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (roadFood == null) roadFood = new List<RoadFoodEntry>();
                roadFood.RemoveAll(e => e?.pawn == null);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && plan != null && (plan.map == null || plan.travellers.Count == 0)) plan = null;
        }
    }

    /// <summary>
    /// The hour came and someone wasn't ready: wait an hour, leave without them, or call the journey off. The choices do
    /// nothing once the plan is gone (they set off after all, or it was called off another way).
    /// </summary>
    public class ChoiceLetter_Journey : ChoiceLetter
    {
        public int planId;

        private JourneyPlan Live
        {
            get
            {
                var plan = JourneyBook.Plan;
                return plan != null && plan.id == planId && plan.stage == JourneyStage.Asking ? plan : null;
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || Live == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption("CC_Journey_Choice_Wait".Translate())
                {
                    action = () =>
                    {
                        var plan = Live;
                        if (plan != null) JourneyRun.WaitAnHour(plan);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                // Someone isn't ready: leave without them. Only danger on the map: set off anyway.
                var without = new DiaOption((JourneyRun.AnyNotReady(Live) ? "CC_Journey_Choice_Without" : "CC_Journey_Choice_Anyway").Translate())
                {
                    action = () =>
                    {
                        var plan = Live;
                        if (plan != null) JourneyRun.LeaveWithout(plan);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                if (!JourneyRun.AnyReady(Live)) without.Disable("CC_Journey_NobodyReady".Translate());
                yield return without;
                yield return new DiaOption("CC_Journey_Choice_CallOff".Translate())
                {
                    action = () =>
                    {
                        var plan = Live;
                        if (plan != null) JourneyRun.CallOff(plan, "CC_Journey_CalledOff");
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref planId, "planId");
        }
    }
}
