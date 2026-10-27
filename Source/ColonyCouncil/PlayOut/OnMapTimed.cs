using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What an answer keeps on the map for a while (OnMapTimed).</summary>
    public enum TimedKind
    {
        /// <summary>Lanterns set out at dusk and taken in at dawn.</summary>
        Lights,
        /// <summary>A fire set in someone's room, kept until its wood is burned.</summary>
        Fire,
        /// <summary>The enemy dead on a pyre beyond the gate, lit at dusk, burned down by morning.</summary>
        Pyre,
        /// <summary>A sleepwalker's door bolted for the night, a few nights running.</summary>
        Bolt,
        /// <summary>An animal at its person's heels for a day or two.</summary>
        Follow
    }

    /// <summary>One thing an answer keeps on the map for a while, and when it ends.</summary>
    public class TimedThing : IExposable
    {
        public TimedKind kind;
        public Map map;
        /// <summary>The lanterns, the fire, the bodies on the pyre (then its fire), the door.</summary>
        public List<Thing> things = new List<Thing>();
        /// <summary>Whose room, door or heels it is.</summary>
        public Pawn pawn;
        /// <summary>The animal following them.</summary>
        public Pawn animal;
        public IntVec3 cell = IntVec3.Invalid;
        /// <summary>How many lanterns; for a bolted door, whether it was bolted tonight (1) or let open by hand (2).</summary>
        public int count;
        /// <summary>Lanterns: "paths" or "gate".</summary>
        public string where;
        public int start;
        /// <summary>When it ends (0: not yet set going).</summary>
        public int until;
        /// <summary>A bolted door: nights left.</summary>
        public int nights;
        /// <summary>Lit, bolted, set out.</summary>
        public bool on;
        /// <summary>A pyre: the answer's outcome, told once it has burned down.</summary>
        public string text;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref animal, "animal");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref where, "where");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref nights, "nights");
            Scribe_Values.Look(ref on, "on");
            Scribe_Values.Look(ref text, "text");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (things == null) things = new List<Thing>();
                things.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>
    /// Things an answer keeps on the map for a while, and takes away again: lanterns set out at dusk and taken in at dawn,
    /// a fire set in an old colonist's cold room until its wood is burned, the enemy dead on a pyre lit at dusk and burned
    /// down by morning, a sleepwalker's door bolted at night for a few nights, an animal at its person's heels for a day or
    /// two. Each ends at once when something matters more (danger, a fire, someone in need) and leaves nothing behind
    /// but ash where a fire was. Cheap when nothing is kept (one list check every 250 ticks); at most
    /// <see cref="MaxKept"/> at once.
    /// </summary>
    public class OnMapTimed : GameComponent
    {
        public List<TimedThing> kept = new List<TimedThing>();

        /// <summary>More than this at once and the oldest ends.</summary>
        public const int MaxKept = 12;

        private static OnMapTimed cached;
        private static Game cachedGame;

        public static OnMapTimed Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<OnMapTimed>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public OnMapTimed(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        public void Add(TimedThing k)
        {
            if (k == null) return;
            k.start = Now;
            kept.Add(k);
            while (kept.Count > MaxKept)
            {
                End(kept[0], true);
                kept.RemoveAt(0);
            }
        }

        public override void GameComponentTick()
        {
            if (kept.Count == 0) return;
            int now = Now;
            Flames(now);
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareTimed)) return;
            foreach (var k in kept.ToList())
            {
                bool over;
                try
                {
                    over = k.map == null || !Find.Maps.Contains(k.map) || Step(k, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kept on the map " + k.kind, e);
                    over = true;
                }
                if (!over) continue;
                try
                {
                    End(k, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kept on the map end " + k.kind, e);
                }
                kept.Remove(k);
            }
        }

        /// <summary>One look at it. True when it's over.</summary>
        private static bool Step(TimedThing k, int now)
        {
            switch (k.kind)
            {
                case TimedKind.Lights: return StepLights(k, now);
                case TimedKind.Fire: return StepFire(k, now);
                case TimedKind.Pyre: return StepPyre(k, now);
                case TimedKind.Bolt: return StepBolt(k, now);
                case TimedKind.Follow: return StepFollow(k, now);
                default: return true;
            }
        }

        /// <summary>It's over: what was set out is taken away, a bolted door is let open, an animal let go.</summary>
        private static void End(TimedThing k, bool early)
        {
            switch (k.kind)
            {
                case TimedKind.Lights:
                    foreach (var t in k.things)
                        if (t != null && t.Spawned) t.Destroy(DestroyMode.Vanish);
                    break;
                case TimedKind.Pyre:
                    // Lit: it burns out now. Not lit: the dead piled for it are no longer kept back from the colony's own hauling.
                    if (k.on) BurnOut(k);
                    else
                        foreach (var t in k.things)
                            if (t is Corpse && t.Spawned) t.SetForbidden(false, false);
                    break;
                case TimedKind.Bolt:
                    if (k.on && k.things.FirstOrDefault() is Building_Door door && door.Spawned) door.SetForbidden(false, false);
                    k.on = false;
                    break;
                case TimedKind.Follow:
                    StopFollowing(k);
                    break;
            }
        }

        private static bool Danger(Map map) => map == null || map.dangerWatcher.DangerRating != StoryDanger.None || MapFacts.ThreatOn(map);

        private static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        private static int UntilDawn(Map map) => Now + OnMapMath.TicksUntil(GenLocalDate.HourFloat(map), OnMapMath.Dawn);

        // ---------------- Lanterns ----------------

        public static ThingDef LanternDef(Map map)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("TorchLamp");
            return def != null && Avail.CanBuild(def, map) ? def : null;
        }

        private static bool StepLights(TimedThing k, int now)
        {
            var map = k.map;
            if (k.on)
            {
                if (now < k.until) return false;
                int still = k.things.Count(t => t != null && t.Spawned);
                if (still > 0)
                    Messages.Message((k.where == "gate" ? "CC_Timed_Lantern_Out" : "CC_Timed_Lanterns_Out").Translate(), MessageTypeDefOf.SilentInput, false);
                return true;
            }
            // Not set out within two days (the setting turned off, nowhere to put them): forgotten.
            if (!PlayOuts.On || now - k.start > Sched.DayTicks * 2) return true;
            if (!OnMapMath.Between(Hour(map), OnMapMath.Dusk, OnMapMath.Dawn) || Danger(map)) return false;
            var def = LanternDef(map);
            if (def == null) return true;
            var lead = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Downed);
            var cells = LanternCells(map, def, k.where, k.count, lead);
            foreach (var c in cells)
            {
                var torch = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                torch.SetFaction(Faction.OfPlayer);
                torch = GenSpawn.Spawn(torch, c, map, Rot4.North, WipeMode.VanishOrMoveAside);
                var fuel = torch.TryGetComp<CompRefuelable>();
                if (fuel != null)
                {
                    // Enough for the night and no more: nobody is sent with wood for them.
                    fuel.allowAutoRefuel = false;
                    fuel.Refuel(Mathf.Min(fuel.Props.fuelCapacity, Mathf.Max(1f, fuel.Props.fuelConsumptionRate * 0.7f)));
                }
                k.things.Add(torch);
            }
            if (k.things.Count == 0) return true;
            k.on = true;
            k.until = UntilDawn(map);
            string text = k.where == "gate" ? "CC_Timed_Lantern_Lit".Translate().Resolve() : "CC_Timed_Lanterns_Lit".Translate(k.things.Count).Resolve();
            Messages.Message(text, new LookTargets(k.things), MessageTypeDefOf.NeutralEvent, false);
            return false;
        }

        /// <summary>
        /// Where the lanterns go: one beside the gate; else along the outside of the colony's walls, a few steps apart, never
        /// in a doorway, a zone or anyone's way (open ground near the middle of the colony if there are too few walls).
        /// </summary>
        public static List<IntVec3> LanternCells(Map map, ThingDef def, string where, int wanted, Pawn near)
        {
            var cells = new List<IntVec3>();
            if (map == null || def == null || wanted <= 0) return cells;
            if (where == "gate")
            {
                var g = PlaySpots.GateCell(map, near);
                if (!g.IsValid) return cells;
                foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(g, map)))
                    if (FreeForLantern(c, map, def))
                    {
                        cells.Add(c);
                        break;
                    }
                return cells;
            }
            int want = OnMapMath.Lanterns(wanted, OnMapMath.MaxLanterns);
            var center = TemplatePlanner.ColonyCenter(map);
            var home = map.areaManager.Home;
            for (int pass = 0; pass < 2 && cells.Count < want; pass++)
                foreach (var c in GenRadial.RadialCellsAround(center, 30f, true))
                {
                    if (cells.Count >= want) break;
                    if (!c.InBounds(map) || !home[c] || c.Roofed(map) || c.Fogged(map)) continue;
                    // First along the walls (a lantern by every window); then, if that found too few, open ground.
                    if (pass == 0 && !NextToWall(c, map)) continue;
                    if (NearDoor(c, map) || cells.Any(x => x.InHorDistOf(c, OnMapMath.LanternSpacing))) continue;
                    if (!FreeForLantern(c, map, def)) continue;
                    cells.Add(c);
                }
            return cells;
        }

        private static bool FreeForLantern(IntVec3 c, Map map, ThingDef def)
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetZone(map) != null) return false;
            if (c.GetFirstPawn(map) != null || (c.GetTerrain(map)?.IsWater ?? false) || map.designationManager.AllDesignationsAt(c).Any()) return false;
            return GenConstruct.CanPlaceBlueprintAt(def, c, Rot4.North, map).Accepted;
        }

        private static bool NextToWall(IntVec3 c, Map map)
        {
            foreach (var x in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One))
            {
                if (!x.InBounds(map)) continue;
                var e = x.GetEdifice(map);
                if (e != null && e.Faction == Faction.OfPlayer && e.def.passability == Traversability.Impassable && !(e is Building_Door)) return true;
            }
            return false;
        }

        private static bool NearDoor(IntVec3 c, Map map)
        {
            foreach (var x in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                if (x.InBounds(map) && x.GetDoor(map) != null) return true;
            return false;
        }

        // ---------------- A fire kept in someone's room ----------------

        private static bool StepFire(TimedThing k, int now)
        {
            var fire = k.things.FirstOrDefault();
            if (fire == null || fire.Destroyed || !fire.Spawned) return true;
            var fuel = fire.TryGetComp<CompRefuelable>();
            // Someone took it over (refueling it by hand from now on): it's the colony's to keep.
            if (fuel != null && fuel.allowAutoRefuel)
            {
                k.things.Clear();
                return true;
            }
            if ((fuel == null || fuel.HasFuel) && now < k.until) return false;
            fire.Destroy(DestroyMode.Vanish);
            if (k.pawn != null)
                Messages.Message("CC_Timed_Fire_Out".Translate(k.pawn.Named("PAWN")).Resolve(), k.pawn.Spawned ? new LookTargets(k.pawn) : LookTargets.Invalid,
                    MessageTypeDefOf.SilentInput, false);
            k.things.Clear();
            return true;
        }

        // ---------------- The pyre ----------------

        /// <summary>
        /// The enemy dead piled out beyond the gate: lit at dusk by one of the colony (<paramref name="by"/>, who carried
        /// them, if they're free), the bodies burning on it through the night, ash by morning, when the answer's outcome
        /// (<paramref name="outcome"/>, if any) is told.
        /// </summary>
        public void AddPyre(Map map, IntVec3 cell, List<Thing> bodies, string outcome = null, Pawn by = null)
        {
            if (map == null || !cell.IsValid || bodies.NullOrEmpty()) return;
            Add(new TimedThing { kind = TimedKind.Pyre, map = map, cell = cell, things = bodies.Where(b => b != null).ToList(), text = outcome, pawn = by });
        }

        /// <summary>Lit by the one who carried the wood out (JobDriver_PlayOutLightPyre). False when there's no pyre waiting there.</summary>
        public bool LightPyre(Map map, IntVec3 cell, Pawn by)
        {
            var k = kept.FirstOrDefault(x => x.kind == TimedKind.Pyre && !x.on && x.map == map && x.cell == cell);
            if (k == null) return false;
            Light(k, by);
            return true;
        }

        /// <summary>The dead on it, still lying there.</summary>
        private static List<Corpse> PyreBodies(TimedThing k) =>
            k.things.OfType<Corpse>().Where(c => c.Spawned && c.Map == k.map && c.Position.InHorDistOf(k.cell, 4.5f)).ToList();

        private static bool StepPyre(TimedThing k, int now)
        {
            var map = k.map;
            if (k.on)
            {
                var fire = k.things.FirstOrDefault(t => !(t is Corpse));
                if (fire != null && fire.Spawned && Rand.Chance(0.35f)) FleckMaker.ThrowSmoke(fire.Position.ToVector3Shifted(), map, 1.5f);
                if (now < k.until && fire != null && fire.Spawned) return false;
                BurnOut(k);
                // The answer's outcome ("burned on a pyre that went all night") now that it has.
                if (!k.text.NullOrEmpty())
                    News.Post(k.text, new LookTargets(k.cell, map), NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                else Messages.Message("CC_Timed_Pyre_Out".Translate(), new LookTargets(k.cell, map), MessageTypeDefOf.SilentInput, false);
                return true;
            }
            var bodies = PyreBodies(k);
            // Carried off, eaten or buried before dusk: nothing left to burn.
            if (bodies.Count == 0) return true;
            // Never lit (danger every evening for two days): the bodies are let go, for the colony to see to (End).
            if (now - k.start > Sched.DayTicks * 2)
            {
                Messages.Message("CC_Timed_Pyre_Never".Translate(), new LookTargets(bodies), MessageTypeDefOf.NeutralEvent, false);
                return true;
            }
            int hour = Hour(map);
            if (!OnMapMath.Between(hour, OnMapMath.PyreDusk, OnMapMath.Dawn) || Danger(map)) return false;
            // Someone is on their way with the wood: let them light it.
            if (map.mapPawns.FreeColonistsSpawned.Any(x => x.CurJobDef == PlayOutDefOf.Council_PlayOutLightPyre && x.CurJob.targetA.Cell == k.cell)) return false;
            var lighter = Lighter(k);
            if (lighter != null)
            {
                var wood = Wood(map, lighter);
                var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutLightPyre, k.cell, wood);
                job.count = wood != null ? Math.Min(OnMapMath.PyreWood, wood.stackCount) : 1;
                lighter.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (lighter.CurJob == job) return false;
            }
            // Nobody free to go out with the wood by the middle of the night: it's lit all the same.
            if (!OnMapMath.Between(hour, OnMapMath.PyreDusk, OnMapMath.PyreLatest)) Light(k, null);
            return false;
        }

        /// <summary>Who goes out to light it: the one who carried the dead out, else the nearest of the colony who's free.</summary>
        private static Pawn Lighter(TimedThing k)
        {
            var map = k.map;
            bool Free(Pawn x) => x != null && x.Spawned && x.Map == map && PlayOuts.CanTakePart(x, map) && x.GetLord() == null && PlayOuts.Interruptible(x)
                                 && !PlayOuts.NeedsPress(x) && !(PlayOutTracker.Instance?.BusyWith(x, null) ?? false) && !x.DevelopmentalStage.Juvenile()
                                 && x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !Beliefs.Forbids(x, Belief.Indoors)
                                 && x.CanReach(k.cell, PathEndMode.Touch, Verse.Danger.Some);
            if (Free(k.pawn)) return k.pawn;
            return map.mapPawns.FreeColonistsSpawned.Where(Free).OrderBy(x => x.Position.DistanceToSquared(k.cell)).FirstOrDefault();
        }

        /// <summary>Wood for the pyre from the colony's stores, the stack nearest the one who carries it, or null with none.</summary>
        private static Thing Wood(Map map, Pawn by)
        {
            var def = ThingDefOf.WoodLog;
            if (def == null) return null;
            return map.listerThings.ThingsOfDef(def)
                .Where(t => t.Spawned && !t.IsForbidden(by) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]) && by.CanReserveAndReach(t, PathEndMode.ClosestTouch, Verse.Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(by.Position)).FirstOrDefault();
        }

        /// <summary>
        /// The pyre takes: the gear stripped from the dead and left in a heap beside it, a fire laid in its middle (the
        /// game's campfire, burning until dawn), and the bodies left on it to burn through the night.
        /// </summary>
        private static void Light(TimedThing k, Pawn by)
        {
            var map = k.map;
            var bodies = PyreBodies(k);
            bool gear = false;
            foreach (var c in bodies)
            {
                try
                {
                    // Their gear is left in a heap beside it, for the colony to take or leave.
                    if (c.AnythingToStrip())
                    {
                        c.Strip(false);
                        gear = true;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pyre strip", e);
                }
            }
            Thing laid = null;
            var def = ThingDefOf.Campfire;
            if (def != null)
            {
                try
                {
                    laid = ThingMaker.MakeThing(def);
                    laid.SetFaction(Faction.OfPlayer);
                    laid = GenSpawn.Spawn(laid, k.cell, map, Rot4.North, WipeMode.VanishOrMoveAside);
                    var fuel = laid.TryGetComp<CompRefuelable>();
                    if (fuel != null)
                    {
                        fuel.allowAutoRefuel = false;
                        fuel.Refuel(fuel.Props.fuelCapacity);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pyre fire", e);
                    laid = null;
                }
            }
            PlayGive.Burned(map, k.cell);
            k.things.Clear();
            if (laid != null) k.things.Add(laid);
            k.things.AddRange(bodies.Cast<Thing>());
            k.on = true;
            k.until = UntilDawn(map);
            string text = by != null
                ? "CC_Timed_Pyre_LitBy".Translate(by.Named("PAWN"), bodies.Count.Named("COUNT")).Resolve()
                : "CC_Timed_Pyre_Lit".Translate(bodies.Count).Resolve();
            if (gear) text += " " + "CC_Timed_Pyre_Gear".Translate().Resolve();
            Messages.Message(text, new LookTargets(k.cell, map), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Burned down: the bodies gone to ash where they lay, the fire out.</summary>
        private static void BurnOut(TimedThing k)
        {
            var map = k.map;
            foreach (var t in k.things.ToList())
            {
                if (t == null || !t.Spawned) continue;
                if (t is Corpse c)
                {
                    var at = c.Position;
                    c.Destroy(DestroyMode.Vanish);
                    if (ThingDefOf.Filth_Ash != null) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 2);
                }
                else PlaySpots.BurnDown(t);
            }
            k.things.Clear();
        }

        /// <summary>
        /// The look of the dead burning on a lit pyre on the map being looked at: glow, sparks and smoke rising from each
        /// body (a few times a second; nothing at all with no pyre lit).
        /// </summary>
        private void Flames(int now)
        {
            if (now % 20 != 0) return;
            var map = Find.CurrentMap;
            if (map == null) return;
            foreach (var k in kept)
            {
                if (k.kind != TimedKind.Pyre || !k.on || k.map != map) continue;
                foreach (var t in k.things)
                {
                    if (!(t is Corpse c) || !c.Spawned) continue;
                    var v = c.DrawPos;
                    if (Rand.Chance(0.6f)) FleckMaker.ThrowFireGlow(v, map, Rand.Range(1.1f, 1.8f));
                    if (Rand.Chance(0.25f)) FleckMaker.ThrowMicroSparks(v, map);
                    if (Rand.Chance(0.15f)) FleckMaker.ThrowSmoke(v, map, Rand.Range(1f, 1.6f));
                    if (Rand.Chance(0.1f)) FleckMaker.ThrowHeatGlow(c.Position, map, 1.4f);
                }
            }
        }

        // ---------------- A door bolted at night ----------------

        /// <summary>
        /// The one door of someone's own bedroom (a room nobody else sleeps in, with a single way in), or null: a door that
        /// leads anywhere else, or is shared, is never bolted.
        /// </summary>
        public static Building_Door OwnDoor(Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            var map = p?.Map;
            if (room == null || map == null || !room.ProperRoom || room.PsychologicallyOutdoors || room.IsHuge) return null;
            if (room.ContainedBeds.Any(b => b.OwnersForReading.Any(o => o != p))) return null;
            Building_Door found = null;
            foreach (var c in room.BorderCells)
            {
                if (!c.InBounds(map)) continue;
                var d = c.GetDoor(map);
                if (d == null) continue;
                if (found != null && found != d) return null;
                found = d;
            }
            return found != null && found.Faction == Faction.OfPlayer ? found : null;
        }

        private static bool StepBolt(TimedThing k, int now)
        {
            var door = k.things.FirstOrDefault() as Building_Door;
            var p = k.pawn;
            if (door == null || !door.Spawned || p == null || p.Dead || k.nights <= 0) return true;
            // Its nights are counted from the day it was asked, slept in that bed or not.
            if (k.until > 0 && now >= k.until) return true;
            var map = door.Map;
            // Only while it's still their own room's one door, theirs alone (they moved, or someone moved in: let open).
            bool theirs = OwnDoor(p) == door;
            int hour = Hour(map);
            bool night = OnMapMath.Between(hour, OnMapMath.BoltFrom, OnMapMath.BoltTo);
            if (k.on)
            {
                // Let open by hand: left so for the rest of the night.
                if (!door.IsForbidden(Faction.OfPlayer))
                {
                    k.on = false;
                    k.count = 2;
                    return false;
                }
                if (night && theirs && SafeToBolt(p, door)) return false;
                door.SetForbidden(false, false);
                k.on = false;
            }
            if (night)
            {
                // A door the player has forbidden is theirs to keep shut: never bolted, so never let open by this either.
                if (k.count == 0 && theirs && !door.IsForbidden(Faction.OfPlayer) && SafeToBolt(p, door) && p.GetRoom() == RoomUtil.BedRoom(p) && p.InBed())
                {
                    door.SetForbidden(true, false);
                    k.on = true;
                    k.count = 1;
                    Messages.Message("CC_Timed_Bolt".Translate(p.Named("PAWN")).Resolve(), new LookTargets(door), MessageTypeDefOf.SilentInput, false);
                }
                return false;
            }
            // Morning: one night done if the door was bolted (or bolted and let open by hand).
            if (k.count != 0)
            {
                k.nights--;
                k.count = 0;
            }
            return k.nights <= 0;
        }

        /// <summary>
        /// Safe to keep them in: they're there, asleep in their own bed and well, nothing burns in the room and no danger is
        /// near. Anything else and the door is let open at once.
        /// </summary>
        private static bool SafeToBolt(Pawn p, Building_Door door)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map != door.Map || p.Downed || p.Drafted || p.InMentalState) return false;
            if (Danger(door.Map) || PlayOuts.MedicalRest(p) || PlayOuts.NeedsPress(p)) return false;
            var health = p.health?.hediffSet;
            if (health != null && health.BleedRateTotal > 0f) return false;
            var room = RoomUtil.BedRoom(p);
            if (room == null || room.Temperature > 45f || room.Temperature < -10f) return false;
            foreach (var t in room.ContainedAndAdjacentThings)
                if (t is Fire) return false;
            return true;
        }

        // ---------------- At someone's heels ----------------

        private static bool StepFollow(TimedThing k, int now)
        {
            var a = k.animal;
            var x = k.pawn;
            if (now >= k.until || !PlayOuts.On || a == null || a.Dead || !a.Spawned || a.Downed || a.InMentalState || a.Faction != Faction.OfPlayer) return true;
            if (x == null || x.Dead || !x.Spawned || x.Map != a.Map || x.Drafted || Danger(a.Map)) return true;
            if (Following(a, x)) return false;
            if (a.GetLord() != null || (a.roping?.IsRoped ?? false)) return false;
            if (!Idle(a)) return false;
            var food = a.needs?.food;
            if (food != null && food.CurCategory >= HungerCategory.Hungry) return false;
            var rest = a.needs?.rest;
            if (rest != null && rest.CurCategory >= RestCategory.Tired) return false;
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, x);
            job.followRadius = 3f;
            job.expiryInterval = 1500;
            job.checkOverrideOnExpire = true;
            a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return false;
        }

        private static bool Following(Pawn a, Pawn x) => a.CurJob?.def == JobDefOf.FollowClose && a.CurJob.targetA.Thing == x;

        /// <summary>Doing nothing much: idle, wandering, standing about.</summary>
        private static bool Idle(Pawn a)
        {
            var d = a.CurJobDef;
            return d == null || d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander || d == JobDefOf.Wait || d == JobDefOf.Wait_MaintainPosture;
        }

        private static void StopFollowing(TimedThing k)
        {
            var a = k.animal;
            if (a != null && a.Spawned && k.pawn != null && Following(a, k.pawn)) a.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>A line for This week ("Gatherings and plans"): what's kept, whose it is, and whether it's out now.</summary>
        public static WeekRow WeekRowFor(TimedThing k)
        {
            if (k?.map == null) return null;
            try
            {
                string text;
                Texture2D icon;
                switch (k.kind)
                {
                    case TimedKind.Lights:
                        text = (k.where == "gate" ? "CC_Week_Timed_Lantern" : "CC_Week_Timed_Lanterns").Translate();
                        icon = Icons.Wedding;
                        break;
                    case TimedKind.Fire:
                        if (k.pawn == null) return null;
                        text = "CC_Week_Timed_Fire".Translate(k.pawn.LabelShort).Resolve();
                        icon = Icons.Rooms;
                        break;
                    case TimedKind.Pyre:
                        text = "CC_Week_Timed_Pyre".Translate();
                        icon = Icons.Death;
                        break;
                    case TimedKind.Bolt:
                        if (k.pawn == null) return null;
                        text = "CC_Week_Timed_Bolt".Translate(k.pawn.LabelShort, k.nights).Resolve();
                        icon = Icons.Rooms;
                        break;
                    case TimedKind.Follow:
                        if (k.pawn == null || k.animal == null) return null;
                        text = "CC_Week_Timed_Follow".Translate(k.animal.LabelShort, k.pawn.LabelShort).Resolve();
                        icon = Icons.Bonds;
                        break;
                    default:
                        return null;
                }
                bool now = k.on || k.kind == TimedKind.Fire || k.kind == TimedKind.Follow;
                var look = k.things.FirstOrDefault(t => t != null && t.Spawned);
                var target = look != null ? new GlobalTargetInfo(look)
                    : k.pawn != null && k.pawn.Spawned ? new GlobalTargetInfo(k.pawn)
                    : k.cell.IsValid ? new GlobalTargetInfo(k.cell, k.map) : GlobalTargetInfo.Invalid;
                return new WeekRow
                {
                    icon = icon,
                    text = text,
                    color = now ? UiKit.Good : Color.white,
                    note = now ? "CC_Week_UnderWay".Translate().ToString() : (k.kind == TimedKind.Bolt ? "CC_Week_Timed_AtNight" : "CC_Week_Timed_AtDusk").Translate().ToString(),
                    noteColor = now ? UiKit.Good : UiKit.Faint,
                    tip = "CC_Week_Timed_Tip".Translate(),
                    click = target.IsValid ? WeekModel.Go(target) : null,
                    order = now ? -1 : 0
                };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kept on the map: this week", e);
                return null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref kept, "kept", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (kept == null) kept = new List<TimedThing>();
                kept.RemoveAll(k => k == null || k.map == null);
            }
        }
    }

    /// <summary>
    /// Lanterns set out for the night (<see cref="count"/>; <see cref="at"/> "paths": along the outside of the colony's
    /// walls, a few steps apart; "gate": one beside the gate), real torches of the kind the colony can build, lit at dusk
    /// and taken in at first light. They burn what the answer set aside for them: nobody carries more wood out to them.
    /// With "Answers play out on the map" off, or no torch the colony could make, the words alone.
    /// </summary>
    public class DilemmaEffect_Lanterns : DilemmaEffect
    {
        public int count = 10;
        public string at = "paths";

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (!PlayOuts.On || map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                if (OnMapTimed.LanternDef(map) == null) return;
                OnMapTimed.Instance?.Add(new TimedThing { kind = TimedKind.Lights, map = map, count = Math.Max(1, count), where = at == "gate" ? "gate" : "paths" });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lanterns", e);
            }
        }
    }

    /// <summary>
    /// A fire set in A's own room (<see cref="forWhom"/>), a campfire the colony could build, fed with the wood the answer
    /// set aside (<see cref="wood"/>; no one carries more to it), in a free corner away from the bed and the door. It's
    /// cleared away when its wood is burned, or after <see cref="maxDays"/>; refueled by hand, it's the colony's to keep.
    /// Only for a room that's cold, inside walls; otherwise the words alone.
    /// </summary>
    public class DilemmaEffect_KeepFire : DilemmaEffect
    {
        public PlayWho forWhom = PlayWho.A;
        public int wood = 40;
        public float maxDays = 8f;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var p = PlayOuts.Who(forWhom, ctx);
                var def = ThingDefOf.Campfire;
                if (p == null || p.Dead || def == null || !Avail.CanBuild(def, map)) return;
                var room = RoomUtil.BedRoom(p);
                if (room == null || room.Map != map || !room.ProperRoom || room.PsychologicallyOutdoors || room.IsHuge || room.Temperature >= 21f) return;
                var cell = OnMapRooms.FreeCorner(room, map, def, Rot4.North);
                if (!cell.IsValid) return;
                var fire = ThingMaker.MakeThing(def);
                fire.SetFaction(Faction.OfPlayer);
                fire = GenSpawn.Spawn(fire, cell, map, Rot4.North, WipeMode.VanishOrMoveAside);
                var fuel = fire.TryGetComp<CompRefuelable>();
                if (fuel != null)
                {
                    fuel.allowAutoRefuel = false;
                    fuel.Refuel(Mathf.Min(wood, fuel.Props.fuelCapacity));
                }
                float days = Mathf.Max(1f, Mathf.Min(maxDays, OnMapMath.FireDays(wood, fuel?.Props.fuelCapacity ?? wood, fuel?.Props.fuelConsumptionRate ?? 10f) + 1f));
                OnMapTimed.Instance?.Add(new TimedThing
                {
                    kind = TimedKind.Fire, map = map, pawn = p, things = new List<Thing> { fire },
                    until = Find.TickManager.TicksGame + (int)(days * Sched.DayTicks)
                });
                Messages.Message("CC_Timed_Fire".Translate(p.Named("PAWN")).Resolve(), new LookTargets(fire), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fire kept", e);
            }
        }
    }

    /// <summary>
    /// A's door bolted from the outside at night, for <see cref="nights"/> nights: only the one door of a bedroom nobody
    /// else sleeps in, only once A is in bed, and only while it's safe (no danger, no fire, A well and asleep); otherwise
    /// it's let open at once. Drafted colonists pass a bolted door as always. With "Answers play out on the map" off,
    /// the words alone.
    /// </summary>
    public class DilemmaEffect_BoltDoor : DilemmaEffect
    {
        public int nights = 3;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var a = ctx?.a;
            if (!PlayOuts.On || map == null || a == null || a.Dead || !a.Spawned || a.Map != map) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var door = OnMapTimed.OwnDoor(a);
                var timed = OnMapTimed.Instance;
                if (door == null || timed == null) return;
                timed.kept.RemoveAll(k => k.kind == TimedKind.Bolt && k.pawn == a && !k.on);
                int n = Mathf.Clamp(nights, 1, 10);
                timed.Add(new TimedThing { kind = TimedKind.Bolt, map = map, pawn = a, things = new List<Thing> { door }, nights = n,
                    until = Find.TickManager.TicksGame + (n + 1) * GenDate.TicksPerDay });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("door bolted", e);
            }
        }
    }

    /// <summary>
    /// The story's animal (the pet) keeps at A's heels for <see cref="hours"/> hours: it follows A about whenever it isn't
    /// eating, sleeping or seeing to itself, and stops at any danger, when A is drafted or leaves the map, or when it's
    /// downed. Never an animal kept in a pen on a rope. With "Answers play out on the map" off, nothing.
    /// </summary>
    public class DilemmaEffect_Follow : DilemmaEffect
    {
        public float hours = 36f;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var a = ctx?.a;
            var pet = ctx?.pet;
            if (!PlayOuts.On || map == null || a == null || pet == null || a.Dead || pet.Dead || !a.Spawned || !pet.Spawned || a.Map != pet.Map) return;
            if (pet.Faction != Faction.OfPlayer || pet.RaceProps == null || !pet.RaceProps.Animal) return;
            try
            {
                if (AnimalPenUtility.NeedsToBeManagedByRope(pet)) return;
                var timed = OnMapTimed.Instance;
                if (timed == null) return;
                timed.kept.RemoveAll(k => k.kind == TimedKind.Follow && k.animal == pet);
                timed.Add(new TimedThing
                {
                    kind = TimedKind.Follow, map = pet.Map, pawn = a, animal = pet,
                    until = Find.TickManager.TicksGame + (int)(Mathf.Clamp(hours, 4f, 72f) * Sched.HourTicks)
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("follow", e);
            }
        }
    }
}
