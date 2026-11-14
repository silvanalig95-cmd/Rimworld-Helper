using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own happenings as they stand, saved with the game (Gods/OnMap/OwnHappenings.cs and the gods' own files
    /// beside it): when each last came (<see cref="last"/>, one a happening), and whatever one has under way (a bargain's
    /// silver on its way down, the white beast waiting at a fence, a ring of mushrooms, the stranger's small test). Nothing
    /// grows without end: the only lists are one a happening and the few children who touched the horn. A happening's
    /// short steps run on their own due tick, and nothing at all with none under way.
    /// </summary>
    public partial class OwnHappeningsBook : GameComponent
    {
        /// <summary>When each happening last came (OwnHappeningsMath.All's order), or long ago.</summary>
        public List<int> last = new List<int>();

        /// <summary>Someone sent to look at what a god left (a root through the floor, a ring of healroot): who, where, what they say, whose, until when.</summary>
        public Pawn lookBy;
        public IntVec3 lookAt = IntVec3.Invalid;
        public string lookTag, lookWhat;
        public int lookGod = -1, lookUntil = -1;

        // ---- Hollin ----
        /// <summary>A dryad walking to the one it gives its word to: the dryad, the one, the word, what it points at, until when.</summary>
        public Pawn dryad, dryadTo;
        public int dryadWord, dryadUntil = -1;
        public IntVec3 dryadPoint = IntVec3.Invalid;
        /// <summary>Where a tree was last felled by the colony's people (the cleared ground), and the trees felled since the old tree was noted.</summary>
        public Map felledMap;
        public IntVec3 felledAt = IntVec3.Invalid;
        public int felledSinceTree;
        /// <summary>The roots breaking up through the floor: where, how many mornings left, the day of the last.</summary>
        public Map rootsMap;
        public IntVec3 rootsNear = IntVec3.Invalid;
        public int rootsLeft, rootsDay = -1;
        /// <summary>Her rising walking on: where it last stood, along which line, how many more, and when the next.</summary>
        public Map walkMap;
        public IntVec3 walkAt = IntVec3.Invalid;
        public float walkAngle;
        public int walkLeft, walkNext = -1;
        /// <summary>The colony's oldest tree, noted when.</summary>
        public Thing oldTree;
        public int oldTreeSince = -1;

        // ---- Smoulder ----
        /// <summary>The bargain at the shaft: its dreamer, map, the silver promised and carried, the shaft, its stage, who carries, until when, when the vein shows.</summary>
        public Pawn dreamer, carrier;
        public Map bargainMap;
        public int promised, thrown, bargainStage, carryUntil = -1, veinAt = -1;
        public IntVec3 shaft = IntVec3.Invalid;
        public Thing silver;
        /// <summary>The one who will wake from a warm dream with a burn (silver kept from the deep), at dawn.</summary>
        public Pawn burnBy;
        /// <summary>When the deep was last paid (a bargain kept, silver given at his place), and whether a bargain was bent and not yet paid for.</summary>
        public int lastPaid = -999999;
        public bool bent;
        /// <summary>A sculpture named for his proper name: where the artist worked, who, and when the seam shows.</summary>
        public Map nameMap;
        public IntVec3 nameAt = IntVec3.Invalid;
        public Pawn nameBy;
        public int nameDue = -1;
        /// <summary>The deep floor growing hot: where, until when.</summary>
        public Map heatMap;
        public IntVec3 heatAt = IntVec3.Invalid;
        public int heatUntil = -1;
        /// <summary>His shadow crossed the land at noon: the miners dream that night.</summary>
        public Map stretchMap;
        public int stretchAt = -1;

        // ---- Bellwether ----
        /// <summary>The children who touched the horn (BellwetherHorn), at most OwnHappeningsMath.HornKept.</summary>
        public List<Pawn> horned = new List<Pawn>();
        /// <summary>His road: the beast, its stage (0 none, 1 stopped at dusk, 2 on the road, 3 waiting at a fence, 4 done), the far edge, where he waits, since when.</summary>
        public Pawn roadBeast;
        public int roadStage, roadSince = -1;
        public IntVec3 roadEdge = IntVec3.Invalid, roadWait = IntVec3.Invalid, roadBack = IntVec3.Invalid;
        /// <summary>The wounded thrumbo at the edge: the beast, where, until when, who goes to it, whether tended.</summary>
        public Pawn hurt, tender;
        public Map hurtMap;
        public IntVec3 hurtAt = IntVec3.Invalid;
        public int hurtUntil = -1, tendUntil = -1;
        public bool tended;
        /// <summary>Thrumbos passed today: someone dreams tonight.</summary>
        public Map passedMap;
        public int passedAt = -1;
        /// <summary>When the white beast last went in peace, and when the colony last killed a thrumbo.</summary>
        public int lastPeace = -999999, lastKinKilled = -999999;

        // ---- Morel ----
        /// <summary>The mourner at the graves: where, until when, waiting for dusk.</summary>
        public Map mournerMap;
        public IntVec3 mournerAt = IntVec3.Invalid;
        public int mournerUntil = -1;
        public bool mournerAtDusk;
        /// <summary>Her last ring: where, until when it is remembered.</summary>
        public Map ringMap;
        public IntVec3 ringAt = IntVec3.Invalid;
        public int ringUntil = -1;
        /// <summary>A god's anger forewarned (Morel's threads, Pip's slow lights): whose, where, which, and when it comes.</summary>
        public int warnGod = -1, warnDue = -1;
        public Map warnMap;
        public GodWrath warnWrath = GodWrath.None;
        public IntVec3 warnAt = IntVec3.Invalid;

        // ---- Ennu ----
        /// <summary>The stranger's small test: whose stay, which, its stage, who answers it, a thing carried, until when.</summary>
        public Pawn testOf, testBy;
        public GuestTest test;
        public int testStage, testUntil = -1;
        public Thing testThing;
        /// <summary>The tales the stranger has told this stay, and of whose stay.</summary>
        public Pawn talesOf;
        public int talesTold;
        /// <summary>The Guest's kindest, until when visitors linger with them; when the last kind welcome was.</summary>
        public Pawn kindest;
        public int kindestUntil = -1, kindWelcome = -999999;
        /// <summary>The bowl: where, its meal, its cook, its stage (1 carried, 2 set down), until when.</summary>
        public Map bowlMap;
        public Thing bowl, bowlTable;
        public Pawn bowlCook;
        public int bowlStage, bowlUntil = -1;
        /// <summary>A visitor seeking out the Guest's kindest with the road's news: who, to whom, until when.</summary>
        public Pawn newsBy, newsTo;
        public int newsUntil = -1;
        /// <summary>The Open Door kept tonight at this home (the festival's day).</summary>
        public Map openDoorMap;
        public int openDoorAt = -1;

        // ---- Pip ----
        /// <summary>The machine that won't stop, and until when.</summary>
        public Thing wontStop;
        public int wontStopUntil = -1;
        /// <summary>The comms console's night: the console, stage (1 on in the night, 2 read in the morning), the message, when the console was last used.</summary>
        public Thing starsConsole;
        public int starsStage, starsMsg, consoleUsed = -999999;

        // ---- Cirrus ----
        /// <summary>The sky watched lately (ticks), at most a few; the festival kept.</summary>
        public List<int> watches = new List<int>();
        public int festivalKept = -999999;
        /// <summary>The last of the colony seen watching the sky, and the night the aurora was last looked for.</summary>
        public Pawn watcher;
        public int auroraNight = -1;
        /// <summary>The wrong-way star seen: who, where the dream puts the fall, its stage (1 seen, 2 dreamt), when the pods come.</summary>
        public Pawn fallSeer;
        public Map fallMap;
        public IntVec3 fallAt = IntVec3.Invalid;
        public int fallStage, fallDue = -1;
        /// <summary>The far thunder rolling: where, its flashes left, the next.</summary>
        public Map thunderMap;
        public int thunderLeft, thunderNext = -1;

        /// <summary>The sky watched this many times at most kept.</summary>
        public const int MaxWatches = 8;

        public OwnHappeningsBook(Game game)
        {
        }

        private static OwnHappeningsBook cached;
        private static Game cachedGame;

        public static OwnHappeningsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<OwnHappeningsBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        [Unsaved] private int nextDue = int.MaxValue;

        /// <summary>Something under way has a step due at <paramref name="tick"/> (the earliest is kept).</summary>
        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            OwnHappenings.Step(this, now);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Fit();
            nextDue = Find.TickManager.TicksGame + 1;
        }

        /// <summary>One entry a happening, long ago for one new since the save.</summary>
        public void Fit()
        {
            last ??= new List<int>();
            while (last.Count < OwnHappeningsMath.All.Length) last.Add(-999999);
            if (last.Count > OwnHappeningsMath.All.Length) last.RemoveRange(OwnHappeningsMath.All.Length, last.Count - OwnHappeningsMath.All.Length);
        }

        public int Last(OwnHappening h)
        {
            Fit();
            return last[(int)h];
        }

        public void Came(OwnHappening h, int now)
        {
            Fit();
            last[(int)h] = now;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref last, "last", LookMode.Value);
            ExposeSeven();
            Scribe_References.Look(ref lookBy, "lookBy");
            Scribe_Values.Look(ref lookAt, "lookAt", IntVec3.Invalid);
            Scribe_Values.Look(ref lookTag, "lookTag");
            Scribe_Values.Look(ref lookWhat, "lookWhat");
            Scribe_Values.Look(ref lookGod, "lookGod", -1);
            Scribe_Values.Look(ref lookUntil, "lookUntil", -1);
            Scribe_References.Look(ref dryad, "dryad");
            Scribe_References.Look(ref dryadTo, "dryadTo");
            Scribe_Values.Look(ref dryadWord, "dryadWord");
            Scribe_Values.Look(ref dryadUntil, "dryadUntil", -1);
            Scribe_Values.Look(ref dryadPoint, "dryadPoint", IntVec3.Invalid);
            Scribe_References.Look(ref felledMap, "felledMap");
            Scribe_Values.Look(ref felledAt, "felledAt", IntVec3.Invalid);
            Scribe_Values.Look(ref felledSinceTree, "felledSinceTree");
            Scribe_References.Look(ref rootsMap, "rootsMap");
            Scribe_Values.Look(ref rootsNear, "rootsNear", IntVec3.Invalid);
            Scribe_Values.Look(ref rootsLeft, "rootsLeft");
            Scribe_Values.Look(ref rootsDay, "rootsDay", -1);
            Scribe_References.Look(ref walkMap, "walkMap");
            Scribe_Values.Look(ref walkAt, "walkAt", IntVec3.Invalid);
            Scribe_Values.Look(ref walkAngle, "walkAngle");
            Scribe_Values.Look(ref walkLeft, "walkLeft");
            Scribe_Values.Look(ref walkNext, "walkNext", -1);
            Scribe_References.Look(ref oldTree, "oldTree");
            Scribe_Values.Look(ref oldTreeSince, "oldTreeSince", -1);
            Scribe_References.Look(ref dreamer, "dreamer");
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_References.Look(ref bargainMap, "bargainMap");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref thrown, "thrown");
            Scribe_Values.Look(ref bargainStage, "bargainStage");
            Scribe_Values.Look(ref carryUntil, "carryUntil", -1);
            Scribe_Values.Look(ref veinAt, "veinAt", -1);
            Scribe_Values.Look(ref shaft, "shaft", IntVec3.Invalid);
            Scribe_References.Look(ref silver, "silver");
            Scribe_References.Look(ref burnBy, "burnBy");
            Scribe_Values.Look(ref lastPaid, "lastPaid", -999999);
            Scribe_Values.Look(ref bent, "bent");
            Scribe_References.Look(ref nameMap, "nameMap");
            Scribe_Values.Look(ref nameAt, "nameAt", IntVec3.Invalid);
            Scribe_References.Look(ref nameBy, "nameBy");
            Scribe_Values.Look(ref nameDue, "nameDue", -1);
            Scribe_References.Look(ref heatMap, "heatMap");
            Scribe_Values.Look(ref heatAt, "heatAt", IntVec3.Invalid);
            Scribe_Values.Look(ref heatUntil, "heatUntil", -1);
            Scribe_References.Look(ref stretchMap, "stretchMap");
            Scribe_Values.Look(ref stretchAt, "stretchAt", -1);
            Scribe_Collections.Look(ref horned, "horned", LookMode.Reference);
            Scribe_References.Look(ref roadBeast, "roadBeast");
            Scribe_Values.Look(ref roadStage, "roadStage");
            Scribe_Values.Look(ref roadSince, "roadSince", -1);
            Scribe_Values.Look(ref roadEdge, "roadEdge", IntVec3.Invalid);
            Scribe_Values.Look(ref roadWait, "roadWait", IntVec3.Invalid);
            Scribe_Values.Look(ref roadBack, "roadBack", IntVec3.Invalid);
            Scribe_References.Look(ref hurt, "hurt");
            Scribe_References.Look(ref tender, "tender");
            Scribe_References.Look(ref hurtMap, "hurtMap");
            Scribe_Values.Look(ref hurtAt, "hurtAt", IntVec3.Invalid);
            Scribe_Values.Look(ref hurtUntil, "hurtUntil", -1);
            Scribe_Values.Look(ref tendUntil, "tendUntil", -1);
            Scribe_Values.Look(ref tended, "tended");
            Scribe_References.Look(ref passedMap, "passedMap");
            Scribe_Values.Look(ref passedAt, "passedAt", -1);
            Scribe_Values.Look(ref lastPeace, "lastPeace", -999999);
            Scribe_Values.Look(ref lastKinKilled, "lastKinKilled", -999999);
            Scribe_References.Look(ref mournerMap, "mournerMap");
            Scribe_Values.Look(ref mournerAt, "mournerAt", IntVec3.Invalid);
            Scribe_Values.Look(ref mournerUntil, "mournerUntil", -1);
            Scribe_Values.Look(ref mournerAtDusk, "mournerAtDusk");
            Scribe_References.Look(ref ringMap, "ringMap");
            Scribe_Values.Look(ref ringAt, "ringAt", IntVec3.Invalid);
            Scribe_Values.Look(ref ringUntil, "ringUntil", -1);
            Scribe_Values.Look(ref warnGod, "warnGod", -1);
            Scribe_Values.Look(ref warnDue, "warnDue", -1);
            Scribe_References.Look(ref warnMap, "warnMap");
            Scribe_Values.Look(ref warnWrath, "warnWrath", GodWrath.None);
            Scribe_Values.Look(ref warnAt, "warnAt", IntVec3.Invalid);
            Scribe_References.Look(ref testOf, "testOf");
            Scribe_References.Look(ref testBy, "testBy");
            Scribe_Values.Look(ref test, "test", GuestTest.Story);
            Scribe_Values.Look(ref testStage, "testStage");
            Scribe_Values.Look(ref testUntil, "testUntil", -1);
            Scribe_References.Look(ref testThing, "testThing");
            Scribe_References.Look(ref talesOf, "talesOf");
            Scribe_Values.Look(ref talesTold, "talesTold");
            Scribe_References.Look(ref kindest, "kindest");
            Scribe_Values.Look(ref kindestUntil, "kindestUntil", -1);
            Scribe_Values.Look(ref kindWelcome, "kindWelcome", -999999);
            Scribe_References.Look(ref bowlMap, "bowlMap");
            Scribe_References.Look(ref bowl, "bowl");
            Scribe_References.Look(ref bowlTable, "bowlTable");
            Scribe_References.Look(ref bowlCook, "bowlCook");
            Scribe_Values.Look(ref bowlStage, "bowlStage");
            Scribe_Values.Look(ref bowlUntil, "bowlUntil", -1);
            Scribe_References.Look(ref newsBy, "newsBy");
            Scribe_References.Look(ref newsTo, "newsTo");
            Scribe_Values.Look(ref newsUntil, "newsUntil", -1);
            Scribe_References.Look(ref openDoorMap, "openDoorMap");
            Scribe_Values.Look(ref openDoorAt, "openDoorAt", -1);
            Scribe_References.Look(ref wontStop, "wontStop");
            Scribe_Values.Look(ref wontStopUntil, "wontStopUntil", -1);
            Scribe_References.Look(ref starsConsole, "starsConsole");
            Scribe_Values.Look(ref starsStage, "starsStage");
            Scribe_Values.Look(ref starsMsg, "starsMsg");
            Scribe_Values.Look(ref consoleUsed, "consoleUsed", -999999);
            Scribe_Collections.Look(ref watches, "watches", LookMode.Value);
            Scribe_Values.Look(ref festivalKept, "festivalKept", -999999);
            Scribe_References.Look(ref watcher, "watcher");
            Scribe_Values.Look(ref auroraNight, "auroraNight", -1);
            Scribe_References.Look(ref fallSeer, "fallSeer");
            Scribe_References.Look(ref fallMap, "fallMap");
            Scribe_Values.Look(ref fallAt, "fallAt", IntVec3.Invalid);
            Scribe_Values.Look(ref fallStage, "fallStage");
            Scribe_Values.Look(ref fallDue, "fallDue", -1);
            Scribe_References.Look(ref thunderMap, "thunderMap");
            Scribe_Values.Look(ref thunderLeft, "thunderLeft");
            Scribe_Values.Look(ref thunderNext, "thunderNext", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Fit();
                horned ??= new List<Pawn>();
                horned.RemoveAll(p => p == null || p.Dead);
                watches ??= new List<int>();
                if (oldTree != null && oldTree.Destroyed) oldTree = null;
                nextDue = 0;
            }
        }
    }

    /// <summary>
    /// Each god's own happenings (docs/gods-next-proposals.md, section C; round 4, batch 1; the player, 14 November 2026:
    /// "each God's events and interactions and effects, etc., you know, feel unique to the god ... each of the gods should feel
    /// really unique in what it does and whether or how it interacts with the world, what the impact is, how these things play
    /// out on the map", and "gods effect should be noticeable"). Hollin, Smoulder, Bellwether, Morel, Ennu, Pip and Cirrus
    /// each do things no other god does, every one of them on the map: who goes where, what is seen and heard, what changes.
    /// The gods stay unseen: a card says what was seen and names the god only where the colony knows it was that god
    /// (GodsOnMapWords.Title); what people say of it is a guess in their own words; the colony's book keeps it as its witness
    /// saw it (GodsOnMapWords.Saw, so Gods.Remember finds who else saw it). Each keeps the gods' pace (OwnHappeningsMath):
    /// its own gap, a chance that grows after it, no firing on a condition nearly always true, the god's nearness only a
    /// little more often; harm only at the shared budget's rate (HarmKinds.GodBurn), and always shown. This file is the frame
    /// they share: the book's steps, the gods' day and hour, the words, and the hooks; each god's own are in Own*.cs.
    /// </summary>
    public static partial class OwnHappenings
    {
        public const string Pool = "Rimfolk_Pool_GodOwnSaid";

        internal static int Now => Find.TickManager.TicksGame;

        internal static OwnHappeningsBook Book => Gods.On ? OwnHappeningsBook.Instance : null;

        /// <summary>The god of this key (Hollin, Smoulder ...), when the pantheon has it and it isn't dead.</summary>
        public static God God(string key)
        {
            var g = Gods.ByKey(key);
            return g != null && g.kind != GodKind.Dead ? g : null;
        }

        public static God GodOf(OwnHappening h) => God(OwnHappeningsMath.GodOf(h));

        /// <summary>
        /// A god acts at a home: the colony holds it (its age's gods), has heard of it, or it is drawn near enough to be felt
        /// there. A god none of that is true of does nothing of its own here.
        /// </summary>
        public static bool Acts(God g, Map map) =>
            g != null && map != null && Gods.On && (Gods.ColonyGods(map).Contains(g) || Gods.Known(g) || GodNear.Of(g, map) >= 0.3f);

        /// <summary>Whole days since a happening last came.</summary>
        public static float DaysSince(OwnHappeningsBook b, OwnHappening h, int now) => GodPace.DaysSince(b.Last(h), now);

        /// <summary>It came: its tick kept, and the gods' short memory notes it (GodPace).</summary>
        public static void Came(OwnHappeningsBook b, OwnHappening h, int now, string form = null, string who = null)
        {
            b.Came(h, now);
            GodPace.Note("own" + h, form ?? OwnHappeningsMath.Name(h), who);
        }

        /// <summary>A drawn happening's look in the gods' day: free of its gap, with its reason, at its chance (the dev tools push it).</summary>
        public static bool Due(OwnHappeningsBook b, OwnHappening h, God g, Map map, GodPaceMath.Trigger why, int now, float checks = 1f)
        {
            if (b == null || g == null || why == GodPaceMath.Trigger.None) return false;
            if (forced == h) return true;
            return Rand.Chance(OwnHappeningsMath.CheckChance(h, DaysSince(b, h, now), why, GodNear.Of(g, map), checks));
        }

        /// <summary>The dev action's happening, while it is being fired: every chance and gap of it gives way.</summary>
        internal static OwnHappening? forced;

        /// <summary>The colony's homes now.</summary>
        internal static IEnumerable<Map> HomeMaps => ColonyCouncil.Homes.All.Where(m => m != null && m.IsPlayerHome).ToList();

        // ================= The gods' day and hour, and the steps between =================

        /// <summary>The gods' day (GodsOnMapBook.Daily).</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("Hollin's day", () => OwnHollin.Daily(b, now));
            Safe("Smoulder's day", () => OwnSmoulder.Daily(b, now));
            Safe("Bellwether's day", () => OwnBellwether.Daily(b, now));
            Safe("Morel's day", () => OwnMorel.Daily(b, now));
            Safe("Ennu's day", () => OwnEnnu.Daily(b, now));
            Safe("Pip's day", () => OwnPip.Daily(b, now));
            Safe("Cirrus's day", () => OwnCirrus.Daily(b, now));
        }

        /// <summary>The gods' hour (GodsOnMapBook.Hourly).</summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("Hollin's hour", () => OwnHollin.Hourly(b, now));
            Safe("Smoulder's hour", () => OwnSmoulder.Hourly(b, now));
            Safe("Bellwether's hour", () => OwnBellwether.Hourly(b, now));
            Safe("Morel's hour", () => OwnMorel.Hourly(b, now));
            Safe("Ennu's hour", () => OwnEnnu.Hourly(b, now));
            Safe("Pip's hour", () => OwnPip.Hourly(b, now));
            Safe("Cirrus's hour", () => OwnCirrus.Hourly(b, now));
            Safe("the second seven's hour", () => Seven(b, now));
            Safe("an anger forewarned", () => Forewarned(b, now));
        }

        /// <summary>The steps between the hours: a carrier at their place, the walk of a rising, a machine running on, the far thunder.</summary>
        internal static void Step(OwnHappeningsBook b, int now)
        {
            Safe("someone looking", () => Look(b, now));
            Safe("Hollin's steps", () => OwnHollin.Step(b, now));
            Safe("Smoulder's steps", () => OwnSmoulder.Step(b, now));
            Safe("Bellwether's steps", () => OwnBellwether.Step(b, now));
            Safe("Morel's steps", () => OwnMorel.Step(b, now));
            Safe("Ennu's steps", () => OwnEnnu.Step(b, now));
            Safe("Pip's steps", () => OwnPip.Step(b, now));
            Safe("Cirrus's steps", () => OwnCirrus.Step(b, now));
            Safe("the second seven's steps", () => SevenSteps(b, now));
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: " + what, e);
            }
        }

        // ================= Words =================

        /// <summary>
        /// One sentence over <paramref name="p"/>'s head (Rimfolk_Pool_GodOwnSaid, by the happening's tags, comma-separated, and the god's own
        /// line tag, and "doubt" for one who believes little), filled with <paramref name="more"/>; to <paramref name="to"/>
        /// when they are together, else to nobody in particular. False when nothing was said.
        /// </summary>
        public static bool Say(Pawn p, string tag, God g, Pawn to = null, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned || tag.NullOrEmpty()) return false;
            try
            {
                var tags = tag.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
                if (g != null) tags.Add(GodShapes.LineTag(g));
                if (p.RaceProps?.Humanlike == true && GodLore.Credence(p) < GodLoreMath.ScofferBelow) tags.Add("doubt");
                string line = Flavor.FromPool(Pool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return false;
                var args = new List<NamedArgument> { p.Named("PAWN") };
                if (more != null) args.AddRange(more);
                return Speech.Say(p, line.Formatted(args.ToArray()).Resolve().CapitalizeFirst(), to != null && Together.Can(p, to) ? to : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a word", e);
                return false;
            }
        }

        /// <summary>A key of the gods' own happenings by its own part ("Saw_Roots": CC_GodO_Saw_Roots).</summary>
        public static string K(string rest) => "CC_GodO_" + rest;

        /// <summary>The colony's book keeps it in its witness's words (CC_GodO_Saw_*), as they saw it; Gods.Remember finds who else saw it.</summary>
        public static string Saw(God g, Map map, IntVec3 cell, GodActKind kind, Pawn witness, string key, params NamedArgument[] more) =>
            GodsOnMapWords.Saw(g, map, cell, kind, witness, key, more);

        /// <summary>
        /// A plain card of what was seen (<paramref name="what"/>: its keys' own part, CC_GodO_<c>what</c>_Text, _Title,
        /// _TitleNamed): a letter for what matters, else a message. Its title names the god only where the colony knows it
        /// was that god; its words never do.
        /// </summary>
        public static void Card(God g, Map map, string what, bool letter, LookTargets look, LetterDef def = null, Importance importance = Importance.Notable,
            params NamedArgument[] args)
        {
            if (map == null || what.NullOrEmpty()) return;
            string key = K(what);
            var all = new List<NamedArgument>(args ?? new NamedArgument[0]) { ColonyCouncil.Homes.Name(map).Named("HOME") };
            string text = (key + "_Text").Translate(all.ToArray()).Resolve();
            if (letter)
            {
                string title = GodsOnMapWords.Title(g, map, false, key + "_Title", key + "_TitleNamed", all.ToArray());
                Tell.Letter(NewsKind.Land, importance, map, title, text, def ?? LetterDefOf.NeutralEvent, look);
            }
            else Tell.Message(NewsKind.Land, importance, map, text, look, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// A few motes at a place in the god's colour: A RimWorld of Magic's own by name (its twinkles, heat, shadows, bolts)
        /// where it is loaded, else Rimfolk's own small light.
        /// </summary>
        public static void Motes(Map map, Vector3 at, string rom, Color color, int n, float spread = 0.6f)
        {
            if (map == null) return;
            Def def = rom.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(rom);
            def ??= GodFx.MoteDef;
            for (int i = 0; i < n; i++)
                GodFx.Throw(def, at + new Vector3(Rand.Range(-spread, spread), 0f, Rand.Range(-spread * 0.5f, spread)), map, Rand.Range(0.25f, 0.45f), color, Rand.Range(-20f, 20f),
                    Rand.Range(0.2f, 0.45f), 0f, Rand.Range(1.2f, 2f));
        }

        /// <summary>A RimWorld of Magic's motes the gods' own happenings use (by name; Rimfolk's own light where it isn't loaded).</summary>
        public const string GreenTwinkle = "Mote_GreenTwinkle", Twinkle = "Mote_Twinkle", GrayTwinkle = "Mote_Twinkle_grayscale", Heat = "Mote_Heat", PsiCurrent = "Mote_PsiCurrent",
            BlueTwinkle = "Mote_BlueTwinkle";

        /// <summary>A memory for one of the colony (a ThoughtDef of Rimfolk's, by name).</summary>
        public static void Memory(Pawn p, string thought)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (def == null || p?.needs?.mood == null || !(ColonyCouncilMod.Settings?.MoodEffects ?? true)) return;
            try
            {
                p.needs.mood.thoughts.memories.TryGainMemory(def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a memory", e);
            }
        }

        /// <summary>A memory let go from everyone of the colony on a map.</summary>
        public static void Forget(Map map, string thought)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (def == null || map == null) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned) p.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(def);
        }

        /// <summary>A line in someone's journal.</summary>
        public static void Journal(Pawn p, string key, params NamedArgument[] args)
        {
            if (p == null) return;
            CouncilGameComponent.Instance?.journals?.WriteText(p, key.Translate(args).Resolve());
        }

        /// <summary>A job given now (interrupting what they were doing, as the gods' moments do), safely.</summary>
        public static bool Job(Pawn p, Job j)
        {
            if (p == null || j == null || !p.Spawned) return false;
            try
            {
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, true);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a job", e);
                return false;
            }
        }

        /// <summary>To a cell, walking.</summary>
        public static bool GoTo(Pawn p, IntVec3 c, int expiry = 2500)
        {
            if (p == null || !c.IsValid || !p.Spawned || !p.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) return false;
            var j = JobMaker.MakeJob(JobDefOf.Goto, c);
            j.locomotionUrgency = LocomotionUrgency.Walk;
            j.expiryInterval = expiry;
            return Job(p, j);
        }

        /// <summary>Standing still a while, facing <paramref name="toward"/>.</summary>
        public static bool Stand(Pawn p, IntVec3 toward, int ticks)
        {
            if (p == null || !p.Spawned) return false;
            var w = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            w.expiryInterval = ticks;
            if (toward.IsValid && toward != p.Position) w.overrideFacing = Rot4.FromIntVec3(toward - p.Position);
            return Job(p, w);
        }

        /// <summary>Someone of the colony carries <paramref name="n"/> of a thing to a cell and sets it down there (the game's hauling, to a cell that isn't a stockpile).</summary>
        public static bool Carry(Pawn p, Thing t, IntVec3 to, int n)
        {
            if (p == null || t == null || !t.Spawned || !to.IsValid || n <= 0) return false;
            if (!p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some) || !p.CanReach(to, PathEndMode.OnCell, Danger.Some)) return false;
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, to);
            job.count = Math.Min(n, t.stackCount);
            job.haulMode = HaulMode.ToCellNonStorage;
            return Job(p, job);
        }

        /// <summary>One of the colony awake and free who could see <paramref name="at"/>, nearest first.</summary>
        public static Pawn Witness(Map map, IntVec3 at, float range) => map == null || !at.IsValid ? null : GodOmens.Witness(map, at, range);

        /// <summary>Anyone of the colony awake, nearest <paramref name="at"/>.</summary>
        public static Pawn Nearest(Map map, IntVec3 at, Pawn not = null) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => p != not && GodOmens.Awake(p)).OrderBy(p => at.IsValid ? p.Position.DistanceToSquared(at) : 0).FirstOrDefault();

        /// <summary>
        /// Someone finds what a god left at <paramref name="at"/> and says so (<paramref name="tag"/>): one of the colony who can
        /// see it now says it at once; else the nearest awake walks over to look and says it there. The book keeps whoever saw it.
        /// </summary>
        public static Pawn Found(OwnHappeningsBook b, Map map, IntVec3 at, string tag, God g, string what = null)
        {
            if (b == null || map == null || !at.IsValid) return null;
            var seer = tag == "ringdig" ? null : Witness(map, at, 14f);
            if (seer != null)
            {
                Say(seer, tag, g, null, (what ?? "").Named("WHAT"));
                return seer;
            }
            var p = Nearest(map, at);
            if (p == null || b.lookBy != null) return null;
            var near = CellFinder.StandableCellNear(at, map, 2f);
            if (!near.IsValid || !GoTo(p, near)) return null;
            b.lookBy = p;
            b.lookAt = at;
            b.lookTag = tag;
            b.lookWhat = what;
            b.lookGod = g?.id ?? -1;
            b.lookUntil = Now + GenDate.TicksPerHour * 3;
            b.Due(Now + 120);
            return p;
        }

        /// <summary>The one sent to look gets there and says what they see.</summary>
        private static void Look(OwnHappeningsBook b, int now)
        {
            var p = b.lookBy;
            if (p == null) return;
            if (p.Dead || !p.Spawned || p.Downed || now > b.lookUntil)
            {
                b.lookBy = null;
                return;
            }
            if (p.Position.InHorDistOf(b.lookAt, 3f))
            {
                Stand(p, b.lookAt, 200);
                // Morel's ring on open ground: they kneel at its middle and dig, and something small comes up out of the earth.
                if (b.lookTag == "ringdig") b.lookWhat = OwnMorel.Dug(p.Map, b.lookAt, p) ?? b.lookWhat;
                Say(p, b.lookTag, Gods.ById(b.lookGod), null, (b.lookWhat ?? "").Named("WHAT"));
                b.lookBy = null;
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto) GoTo(p, CellFinder.StandableCellNear(b.lookAt, p.Map, 2f));
            b.Due(now + 120);
        }

        /// <summary>The hour of the day at a map.</summary>
        public static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        /// <summary>A home is quiet enough for the gods' own doings: no fight on it.</summary>
        public static bool Quiet(Map map) => map != null && !MapFacts.ThreatOn(map);

        // ================= An anger forewarned =================

        /// <summary>The anger forewarned is coming now: the god's own, bypassing the warning this once.</summary>
        internal static bool bypass;

        /// <summary>
        /// A god's anger about to come at a home (GodHealth.Wrath): Morel's freezer and Pip's short circuit show their warning
        /// first (white threads, the lights slow and sad), and the anger comes the next day. True when it was forewarned (and
        /// so counts as come); false lets it come now.
        /// </summary>
        public static bool Forewarn(God g, Map map, GodWrath wrath)
        {
            var b = Book;
            if (b == null || bypass || g == null || map == null || b.warnDue >= 0) return false;
            bool shown;
            if (g.Key == "Morel" && wrath == GodWrath.Breakdown) shown = OwnMorel.Threads(b, g, map);
            else if (g.Key == "Pip" && wrath == GodWrath.ShortCircuit) shown = OwnPip.Sulk(b, g, map);
            else return false;
            if (!shown) return false;
            b.warnGod = g.id;
            b.warnMap = map;
            b.warnWrath = wrath;
            b.warnDue = Now + Mathf.RoundToInt(Rand.Range(OwnHappeningsMath.LeadHoursMin, OwnHappeningsMath.LeadHoursMax) * GenDate.TicksPerHour);
            return true;
        }

        /// <summary>The forewarned anger's hour: it comes (the game's own breakdown or short circuit), in the small hours for the freezer.</summary>
        private static void Forewarned(OwnHappeningsBook b, int now)
        {
            if (b.warnDue < 0 || now < b.warnDue) return;
            var g = Gods.ById(b.warnGod);
            var map = b.warnMap;
            if (g == null || map == null || !ColonyCouncil.Homes.IsHome(map))
            {
                b.warnDue = -1;
                return;
            }
            // The freezer fails at the worst hour: the small hours.
            if (b.warnWrath == GodWrath.Breakdown && Hour(map) >= 6 && now < b.warnDue + GenDate.TicksPerDay) return;
            b.warnDue = -1;
            bypass = true;
            try
            {
                GodHealth.Wrath(g, map, b.warnWrath);
            }
            finally
            {
                bypass = false;
            }
        }

        // ================= What the hooks tell =================

        /// <summary>
        /// A god's own sign has shown (GodOmens.Seen): Smoulder's winged shadow at noon (the miners dream that night), Cirrus's
        /// wrong-way star (its seer may dream where something fell), Morel's ring of mushrooms (remembered for her breath).
        /// </summary>
        public static void Sign(God god, Map map, IntVec3 at, OmenKind kind, Pawn speaker)
        {
            if (god == null || map == null || Book == null) return;
            if (kind == OmenKind.Shadow && god.Key == OwnSmoulder.Key) OwnSmoulder.Shadow(map);
            else if (kind == OmenKind.Star && god.Key == OwnCirrus.Key) OwnCirrus.WrongStar(map, speaker);
            else if (kind == OmenKind.FairyRing && god.Key == OwnMorel.Key) OwnMorel.Remember(Book, map, at);
        }

        /// <summary>A god's festival kept at a home (Festivals.Kept): the Guest's Open Door, a night of the wrong star kept for the sky.</summary>
        public static void FestivalKept(God god, Map map)
        {
            if (god == null || map == null || Book == null) return;
            if (god.Key == OwnEnnu.Key) OwnEnnu.OpenDoor(map);
            else if (god.Key == OwnCirrus.Key) OwnCirrus.FestivalKept();
            else SevenFestival(Book, god, map);
        }

        /// <summary>A tree felled by the colony's people (Patch_OwnTreeFelled): the cleared ground, and the count round the old tree.</summary>
        internal static void Felled(Plant plant, Pawn by)
        {
            var b = Book;
            if (b == null || plant?.def?.plant == null || !plant.def.plant.IsTree || by?.Faction == null || !by.Faction.IsPlayer) return;
            var map = plant.MapHeld;
            if (map == null || !map.IsPlayerHome) return;
            b.felledMap = map;
            b.felledAt = plant.PositionHeld;
            b.felledSinceTree++;
        }

        /// <summary>One of the game's events happened at a home (Patch_OwnEventHappened): thrumbos passing.</summary>
        internal static void Happened(IncidentDef def, IncidentParms parms)
        {
            if (def == null || !(parms?.target is Map map) || !ColonyCouncil.Homes.IsHome(map)) return;
            if (def.defName == "ThrumboPasses") OwnBellwether.Passed(Book, map);
        }
    }

    /// <summary>A tree felled by one of the colony's people: Hollin's roots and the old tree remember it (OwnHappenings.Felled). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_OwnTreeFelled
    {
        public static void Prefix(Plant __instance, Pawn by)
        {
            if (!RimfolkPatches.On || by == null || !Gods.On) return;
            try
            {
                OwnHappenings.Felled(__instance, by);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a tree felled", e);
            }
        }
    }

    /// <summary>One of the game's events happened (thrumbos passing): noted for the gods' own (OwnHappenings.Happened). Its letter is untouched.</summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_OwnEventHappened
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !RimfolkPatches.On || !Gods.On) return;
            try
            {
                OwnHappenings.Happened(__instance?.def, parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: an event", e);
            }
        }
    }

    /// <summary>A work of art just made by one of the colony's people: Smoulder's proper name may be given it (OwnSmoulder.ArtMade).</summary>
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.JustCreatedBy))]
    public static class Patch_OwnArtMade
    {
        public static void Postfix(CompArt __instance, Pawn pawn)
        {
            if (!RimfolkPatches.On || !Gods.On || pawn == null || !pawn.IsColonist) return;
            try
            {
                OwnSmoulder.ArtMade(__instance, pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a work of art", e);
            }
        }
    }

    /// <summary>A switch flicked by one of the colony's people: Pip may keep the machine running (OwnPip.Flicked).</summary>
    [HarmonyPatch(typeof(CompFlickable), nameof(CompFlickable.DoFlick))]
    public static class Patch_OwnFlicked
    {
        public static void Postfix(CompFlickable __instance)
        {
            if (!RimfolkPatches.On || !Gods.On) return;
            try
            {
                OwnPip.Flicked(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a switch", e);
            }
        }
    }

    /// <summary>A turret of the colony's about to fire: it holds its fire a breath for one of the colony across its line (OwnPip.HoldFire).</summary>
    [HarmonyPatch(typeof(Building_TurretGun), "TryStartShootSomething")]
    public static class Patch_OwnTurretHeld
    {
        public static bool Prefix(Building_TurretGun __instance)
        {
            if (!RimfolkPatches.On || !OwnPip.Watching) return true;
            try
            {
                return !OwnPip.HoldFire(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a turret", e);
                return true;
            }
        }
    }
}
