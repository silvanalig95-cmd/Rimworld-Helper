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
    /// <summary>
    /// Tamiu's own doings as they stand, saved with the game (defName and key Mog; <see cref="MogsWays"/>): the riddle of
    /// her stay (where she lies, who walks out to answer and whether they will get it wrong), the new grave kept (its
    /// keeper and its cat, until when), and when the wrapped dead last stood guard. Nothing here is a list, and a stay's
    /// short steps run on their own due tick.
    /// </summary>
    public class MogsBook : GameComponent
    {
        /// <summary>Her beast whose stay the riddle was asked in (one a stay), where she lies, and until when she waits.</summary>
        public Pawn riddleOf;
        public IntVec3 door = IntVec3.Invalid;
        public int riddleUntil = -1;
        /// <summary>Which riddle (of which age), and who walks out to answer it; whether they will get it wrong (the card's risk), until when they may walk.</summary>
        public int riddle = -1, riddleAge;
        public Pawn answerer;
        public bool wrong;
        public int walkUntil = -1;

        /// <summary>The newest death of the colony's whose grave was looked at, and the grave kept now: its keeper, its cat, and until when.</summary>
        public int lastDeath = -1;
        public Thing grave;
        public string dead;
        public Pawn keeper, cat;
        public int keeperUntil = -1, catUntil = -1;
        public bool keeperSat, catSeen, catOwn;

        /// <summary>When the wrapped dead last stood guard, when a raid near the tombs was last weighed, and when their lying down was last told.</summary>
        public int lastRisen = -999999, lastWeighed = -999999, lastDownTold = -999999;

        public MogsBook(Game game)
        {
        }

        private static MogsBook cached;
        private static Game cachedGame;

        public static MogsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<MogsBook>();
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
            GodsOnMapBook.Safe("the answer walked out to her", () => MogsWays.Step(this, now));
            GodsOnMapBook.Safe("the wrapped dead turned to dust", MogsWays.Dust);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            nextDue = Find.TickManager.TicksGame + 1;
        }

        /// <summary>The riddle of this stay is still waiting for its answer, or for the one sent with it.</summary>
        public bool Waiting => riddleOf != null && door.IsValid;

        /// <summary>The riddle is over: she gets up from the door; the stay keeps that it was asked.</summary>
        public void RiddleDone()
        {
            door = IntVec3.Invalid;
            riddleUntil = -1;
            answerer = null;
            walkUntil = -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref riddleOf, "riddleOf");
            Scribe_Values.Look(ref door, "door", IntVec3.Invalid);
            Scribe_Values.Look(ref riddleUntil, "riddleUntil", -1);
            Scribe_Values.Look(ref riddle, "riddle", -1);
            Scribe_Values.Look(ref riddleAge, "riddleAge");
            Scribe_References.Look(ref answerer, "answerer");
            Scribe_Values.Look(ref wrong, "wrong");
            Scribe_Values.Look(ref walkUntil, "walkUntil", -1);
            Scribe_Values.Look(ref lastDeath, "lastDeath", -1);
            Scribe_References.Look(ref grave, "grave");
            Scribe_Values.Look(ref dead, "dead");
            Scribe_References.Look(ref keeper, "keeper");
            Scribe_References.Look(ref cat, "cat");
            Scribe_Values.Look(ref keeperUntil, "keeperUntil", -1);
            Scribe_Values.Look(ref catUntil, "catUntil", -1);
            Scribe_Values.Look(ref keeperSat, "keeperSat");
            Scribe_Values.Look(ref catSeen, "catSeen");
            Scribe_Values.Look(ref catOwn, "catOwn");
            Scribe_Values.Look(ref lastRisen, "lastRisen", -999999);
            Scribe_Values.Look(ref lastWeighed, "lastWeighed", -999999);
            Scribe_Values.Look(ref lastDownTold, "lastDownTold", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (riddleOf == null) RiddleDone();
                if (grave == null || grave.Destroyed)
                {
                    grave = null;
                    keeper = null;
                    cat = null;
                }
                nextDue = 0;
            }
        }
    }

    /// <summary>
    /// Tamiu, the Keeper of the Threshold (defName and key Mog; docs/gods-next-proposals.md, C 12; the player, 13 and 14
    /// November 2026: an Egyptian cat god "with mummies and like Egyptian gear wearing zombies style followers ... and
    /// there's also some sort of cult behind it"), on the map. What happens is seen and said, never explained:
    /// <list type="bullet">
    /// <item>Her stay (the sacred animal's, SacredAnimal.cs, as a great sand-gold cat): she walks to the graves and keeps
    /// among them, the colony's cats with her; the one who answers her riddle well (or, before that, the one who fed her)
    /// has her favour, and she walks at their heel (her old luck: SacredAnimal.Lucky); she still trails a stranger at the
    /// gate and stares (SacredAnimal.Stare).</item>
    /// <item>The riddle at the threshold (once a stay at most, MogMath.RiddleDue): she lies across the way in, a card puts
    /// her riddle (one of its age's) and who should answer (the colony's wits or its tongue, or nobody); the one sent walks
    /// out to her and says their answer aloud, and it is right or wrong by their skill (the card's risk, MogMath.WrongChance):
    /// right, she gets up and walks at their heel; wrong, a cuff of the paw at the shared harm budget's rate and she walks
    /// back to the graves.</item>
    /// <item>A new grave kept (MogMath.KeptChance): one of her keepers from a people that holds her comes at dusk with a cat,
    /// sits the night by the grave and goes at dawn, and the cat stays on the grave a day or two; with no keeper able to
    /// come, a cat of the colony's (else a stray walking in) sits on it instead. Those fond of the dead are comforted.</item>
    /// <item>The wrapped dead stand guard (with Anomaly, MogMath.RiseChance and the shared harm budget): in a raid that comes
    /// near a home's tombs while she is on the land or drawn near, a few of the land's old dead stand up among the graves in
    /// grey linen (the game's shamblers, of the entities), keep to the graves and turn on anything that comes near, raider
    /// or colonist, and lie down as dust when the map is quiet (HediffComp_WrappedDead).</item>
    /// </list>
    /// </summary>
    public static class MogsWays
    {
        public const string RiddleIncident = "Rimfolk_Evt_GodRiddle";
        public const string RiddlePool = "Rimfolk_Pool_MogRiddleSaid";
        public const string KeeperPool = "Rimfolk_Pool_MogKeeper";
        public const string GraveCatPool = "Rimfolk_Pool_MogGraveCat";
        public const string RisenPool = "Rimfolk_Pool_MogWrappedDead";
        public const string RightThought = "Rimfolk_Thought_MogRiddleRight";
        public const string WrongThought = "Rimfolk_Thought_MogRiddleWrong";
        public const string KeptThought = "Rimfolk_Thought_MogGraveKept";
        public const string RisenThought = "Rimfolk_Thought_MogSawDeadStand";
        public const string WrappedHediff = "Rimfolk_WrappedDead";
        public const string ShamblerKind = "ShamblerSwarmer";

        /// <summary>Her shape's line tag (the cat who keeps the dead), and her presence on the land while she stays (GameCondition_MogPresence).</summary>
        public const string Tag = "mog";
        public const string PresenceDef = "Rimfolk_GodPresence_Mog";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god herself, or null when the pantheon has no Tamiu.</summary>
        public static God Her => Gods.All.FirstOrDefault(g => g != null && GodShapes.LineTag(g) == Tag);

        // ================= The tombs =================

        /// <summary>Alpha Memes' mummified dead, kept as art (by name: they hold a body).</summary>
        private static readonly string[] Mummies = { "AM_MummyMale", "AM_MummyFemale" };

        /// <summary>
        /// A home's tombs: its graves and sarcophagi that hold someone (the game's, and the player's mods', all graves to the
        /// game), and Alpha Memes' mummies; <paramref name="anyGrave"/> also counts an empty grave (a place for her to lie).
        /// </summary>
        public static List<Thing> Tombs(Map map, bool anyGrave = false)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Grave))
                if (t is Building_Grave g && g.Spawned && (anyGrave || g.HasCorpse) && !g.Position.Fogged(map))
                    list.Add(g);
            foreach (var name in Mummies)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                foreach (var t in map.listerThings.ThingsOfDef(def))
                    if (t.Spawned && !t.Position.Fogged(map))
                        list.Add(t);
            }
            return list;
        }

        /// <summary>The middle of the tombs (the nearest of them to their average), or Invalid with none.</summary>
        public static IntVec3 Middle(List<Thing> tombs)
        {
            if (tombs == null || tombs.Count == 0) return IntVec3.Invalid;
            var avg = new IntVec3((int)tombs.Average(t => t.Position.x), 0, (int)tombs.Average(t => t.Position.z));
            return tombs.OrderBy(t => t.Position.DistanceToSquared(avg)).First().Position;
        }

        /// <summary>A standable cell beside a tomb, or Invalid.</summary>
        private static IntVec3 Beside(Thing t, Map map)
        {
            if (t == null || map == null) return IntVec3.Invalid;
            foreach (var c in GenAdj.CellsAdjacent8Way(t).InRandomOrder())
                if (c.InBounds(map) && c.Standable(map) && !c.Fogged(map))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>The colony's cats on the map: her own kin's kinds (GodWays), and any animal called a cat.</summary>
        public static List<Pawn> Cats(Map map, bool colonyOnly)
        {
            var her = Her;
            var kin = her == null ? new List<string>() : GodWays.Kin(her).ToList();
            return map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && !p.Dead && !p.Downed && !GodAvatar.Is(p) && (!colonyOnly || p.Faction == Faction.OfPlayer)
                                                          && (kin.Contains(p.def.defName) || p.def.defName == "Cat" || p.def.label.IndexOf("cat", StringComparison.OrdinalIgnoreCase) >= 0
                                                              && p.BodySize <= 1f))
                .ToList();
        }

        /// <summary>Every cat on the land that is free stops and sits facing <paramref name="at"/> a while (the game's beholding). How many did.</summary>
        public static int CatsFace(Map map, IntVec3 at, int ticks)
        {
            var look = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.BeholdJob);
            if (map == null || look == null || !at.IsValid) return 0;
            int turned = 0;
            foreach (var c in Cats(map, false).Where(c => (c.CurJob == null || c.CurJob.def.casualInterruptible) && !(c.roping?.IsRoped ?? false) && !c.InMentalState))
            {
                try
                {
                    var sit = JobMaker.MakeJob(look, at);
                    sit.count = Mathf.RoundToInt(ticks * Rand.Range(0.7f, 1f));
                    sit.expiryInterval = sit.count + 120;
                    c.jobs.StartJob(sit, JobCondition.InterruptForced);
                    turned++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the cats facing the graves", e);
                }
            }
            return turned;
        }

        /// <summary>The cats keep watch (her presence, now and then): they go and sit facing the graves a couple of hours.</summary>
        internal static void CatsWatch(Map map)
        {
            if (map == null) return;
            var tombs = Tombs(map, true);
            var at = tombs.Count > 0 ? Middle(tombs) : CampSite.ColonyCenter(map);
            CatsFace(map, at, GenDate.TicksPerHour * 2);
        }

        // ================= Her stay =================

        /// <summary>She comes in from the edge: she makes for the graves (the oldest of the tombs' middle), else the land near the colony.</summary>
        internal static void Arrived(SacredStay s)
        {
            var a = s?.a;
            if (a == null || !a.Spawned) return;
            var at = Middle(Tombs(a.Map, true));
            if (at.IsValid && CellFinder.TryFindRandomCellNear(at, a.Map, 3, c => c.Standable(a.Map) && a.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var near))
                a.mindState.forcedGotoPosition = near;
            GameCondition_MogPresence.Begin(a.Map, s.leaveAt);
        }

        /// <summary>
        /// Her hour on the land (SacredAnimal.Hourly, when she isn't staring at a stranger): her riddle at the door while it
        /// waits, else at the heel of the one she favours, else among the graves; and whether her riddle comes now.
        /// </summary>
        internal static void Keep(SacredStay s, Pawn a, int now)
        {
            var b = MogsBook.Instance;
            if (b == null || a.InMentalState || a.Downed || !a.Awake()) return;
            if (b.riddleOf != null && b.riddleOf != a) b.riddleOf = null;
            if (b.Waiting && b.riddleOf == a)
            {
                if (now > b.riddleUntil && b.answerer == null)
                {
                    b.RiddleDone();
                    Gods.AddFavour(s.God, s.map, MogMath.NobodyFavour, null);
                }
                else
                {
                    LieAt(a, b.door, OutFrom(a.Map, b.door), GenDate.TicksPerHour * 2);
                    return;
                }
            }
            if (b.riddleOf == null && MogMath.RiddleDue((now - s.since) / (float)GenDate.TicksPerDay, false, GenLocalDate.HourOfDay(a.Map), Rand.Value) && Ask(b, s, a))
                return;
            var c = s.chosen;
            if (c != null && !c.Dead && c.Spawned && c.Map == a.Map)
            {
                if (a.CurJobDef == JobDefOf.FollowClose && a.CurJob?.targetA.Thing == c) return;
                Job(a, () =>
                {
                    var j = JobMaker.MakeJob(JobDefOf.FollowClose, c);
                    j.followRadius = 3f;
                    j.expiryInterval = GenDate.TicksPerHour;
                    j.checkOverrideOnExpire = true;
                    j.locomotionUrgency = LocomotionUrgency.Walk;
                    return j;
                });
                return;
            }
            AmongTheGraves(a);
        }

        /// <summary>Among the graves: to one of them if she is away, else lying beside it an hour or two, facing out.</summary>
        private static void AmongTheGraves(Pawn a)
        {
            var map = a.Map;
            var tombs = Tombs(map, true);
            if (tombs.Count == 0) return;
            var mid = Middle(tombs);
            if (a.Position.InHorDistOf(mid, 8f))
            {
                if (a.CurJobDef == JobDefOf.Wait || Rand.Chance(0.3f)) return;
                var t = tombs.Where(x => x.Position.InHorDistOf(mid, 10f)).RandomElementWithFallback();
                var at = Beside(t, map);
                if (at.IsValid) LieAt(a, at, Rot4.Random, Rand.Range(GenDate.TicksPerHour, GenDate.TicksPerHour * 2));
                return;
            }
            var to = Beside(tombs.RandomElement(), map);
            if (!to.IsValid || !a.CanReach(to, PathEndMode.OnCell, Danger.Deadly)) return;
            Job(a, () =>
            {
                var j = JobMaker.MakeJob(JobDefOf.Goto, to);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                j.expiryInterval = GenDate.TicksPerHour;
                return j;
            });
        }

        /// <summary>To a cell, and lying there facing one way a while (the game's waiting, its facing held).</summary>
        private static void LieAt(Pawn a, IntVec3 at, Rot4 facing, int ticks)
        {
            if (!at.IsValid) return;
            if (a.Position != at)
            {
                if (a.CurJobDef == JobDefOf.Goto && a.CurJob?.targetA.Cell == at) return;
                if (!a.CanReach(at, PathEndMode.OnCell, Danger.Deadly)) return;
                Job(a, () =>
                {
                    var g = JobMaker.MakeJob(JobDefOf.Goto, at);
                    g.locomotionUrgency = LocomotionUrgency.Walk;
                    g.expiryInterval = GenDate.TicksPerHour;
                    return g;
                });
                return;
            }
            if (a.CurJobDef == JobDefOf.Wait) return;
            Job(a, () =>
            {
                var w = JobMaker.MakeJob(JobDefOf.Wait);
                w.expiryInterval = ticks;
                w.overrideFacing = facing;
                w.forceMaintainFacing = true;
                return w;
            });
        }

        private static void Job(Pawn a, Func<Job> make)
        {
            try
            {
                a.jobs.StartJob(make(), JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the great cat's way", e);
            }
        }

        /// <summary>The way out from a door cell: away from the colony's middle.</summary>
        private static Rot4 OutFrom(Map map, IntVec3 door)
        {
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) return Rot4.South;
            var d = door - centre;
            return Math.Abs(d.x) > Math.Abs(d.z) ? (d.x > 0 ? Rot4.East : Rot4.West) : (d.z > 0 ? Rot4.North : Rot4.South);
        }

        /// <summary>One she favours (the answer of her riddle, or the bowl brought her): she walks at their heel, and their luck is hers (SacredAnimal.Lucky).</summary>
        internal static void Favour(SacredStay s, Pawn p)
        {
            if (s == null || p == null || p.Dead) return;
            s.chosen = p;
        }

        // ================= The riddle at the threshold =================

        /// <summary>Her stay whose riddle is being asked (the card's worker reads it).</summary>
        internal static SacredStay pending;

        /// <summary>The cell outside the colony's way in where she lies: outside one of its outer doors, else just outside the colony.</summary>
        private static IntVec3 Door(Map map, Pawn a)
        {
            var centre = CampSite.ColonyCenter(map);
            var doors = map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Where(d => d.Spawned && !d.Position.Fogged(map)).ToList();
            var best = IntVec3.Invalid;
            float far = -1f;
            foreach (var d in doors)
            {
                foreach (var c in GenAdj.CellsAdjacentCardinal(d))
                {
                    if (!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || c.GetEdifice(map) != null || !a.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    float dist = centre.IsValid ? c.DistanceTo(centre) : 0f;
                    if (dist > far)
                    {
                        far = dist;
                        best = c;
                    }
                }
            }
            if (best.IsValid) return best;
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            return near != null && RCellFinder.TryFindRandomSpotJustOutsideColony(near, out var spot) ? spot : IntVec3.Invalid;
        }

        /// <summary>She lies down across the way in and her card comes (the letters' gap and a quiet map allowing). True once asked.</summary>
        private static bool Ask(MogsBook b, SacredStay s, Pawn a)
        {
            var map = a.Map;
            if (!Tell.CanAsk || MapFacts.ThreatOn(map) || s.hurt) return false;
            var door = Door(map, a);
            if (!door.IsValid) return false;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(RiddleIncident);
            if (!(def?.Worker is IncidentWorker_Happening w)) return false;
            b.riddleOf = a;
            b.door = door;
            b.riddleUntil = Now + Mathf.RoundToInt(MogMath.WaitHours * GenDate.TicksPerHour);
            b.riddleAge = GodStranger.Age;
            b.riddle = MogMath.Riddle(GodsMath.Seed(a.thingIDNumber, Now / GenDate.TicksPerDay, map.uniqueID));
            b.answerer = null;
            b.wrong = false;
            pending = s;
            bool asked;
            try
            {
                asked = w.ScenarioCanFire(map) && w.ScenarioFire(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the great cat's riddle", e);
                asked = false;
            }
            finally
            {
                pending = null;
            }
            if (!asked)
            {
                b.riddleOf = null;
                b.RiddleDone();
                return false;
            }
            LieAt(a, door, OutFrom(map, door), GenDate.TicksPerHour * 2);
            return true;
        }

        /// <summary>The riddle in words: its question, and an answer (its own, or another of its age's for a wrong guess).</summary>
        public static string Question(int age, int i) => ("CC_Mog_RiddleQ_" + Mathf.Clamp(age, 0, 3) + "_" + i).Translate().Resolve();

        public static string Answer(int age, int i) => ("CC_Mog_RiddleA_" + Mathf.Clamp(age, 0, 3) + "_" + i).Translate().Resolve();

        /// <summary>
        /// The colony's answer to her card: <paramref name="by"/> walks out to her (wrong when the card's risk came true, told
        /// only when they get there), or nobody goes and she lies there a while longer and goes back to the graves.
        /// </summary>
        internal static void Answered(Pawn beast, Pawn by, bool wrong)
        {
            var b = MogsBook.Instance;
            if (b == null || beast == null || b.riddleOf != beast || !b.Waiting) return;
            if (by == null || by.Dead || !by.Spawned || by.Map != beast.Map || !beast.Spawned)
            {
                b.riddleUntil = Math.Min(b.riddleUntil, Now + GenDate.TicksPerHour * 2);
                return;
            }
            b.answerer = by;
            b.wrong = wrong;
            b.walkUntil = Now + Mathf.RoundToInt(MogMath.WalkHours * GenDate.TicksPerHour);
            Walk(by, beast);
            b.Due(Now + 250);
        }

        private static void Walk(Pawn by, Pawn beast)
        {
            try
            {
                var j = JobMaker.MakeJob(JobDefOf.Goto, beast.Position);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                j.expiryInterval = GenDate.TicksPerHour;
                by.jobs.StartJob(j, JobCondition.InterruptForced, null, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: walking out to the great cat", e);
            }
        }

        /// <summary>The short steps: the one sent on their way to her (and there, their answer), the grave's keeper and cat at their places.</summary>
        internal static void Step(MogsBook b, int now)
        {
            var p = b.answerer;
            var a = b.riddleOf;
            if (p == null || a == null || !b.Waiting) return;
            if (p.Dead || !p.Spawned || p.Downed || p.InMentalState || a.Dead || !a.Spawned || p.Map != a.Map || now > b.walkUntil)
            {
                // Never got there: she gets up after a while and goes back to the graves.
                b.answerer = null;
                b.riddleUntil = Math.Min(b.riddleUntil, now + GenDate.TicksPerHour);
                return;
            }
            if (p.Position.InHorDistOf(a.Position, 2.5f))
            {
                There(b, a, p);
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto) Walk(p, a);
            b.Due(now + 250);
        }

        /// <summary>Before her: the answer said aloud, and what she does with it.</summary>
        private static void There(MogsBook b, Pawn a, Pawn p)
        {
            var s = GodsOnMapBook.Instance?.beast;
            var god = s?.a == a ? s.God : Her;
            var map = a.Map;
            bool wrong = b.wrong;
            int age = b.riddleAge, i = Mathf.Clamp(b.riddle, 0, MogMath.RiddlesPerAge - 1);
            b.RiddleDone();
            try
            {
                var stand = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                stand.expiryInterval = 240;
                stand.overrideFacing = Rot4.FromIntVec3(a.Position - p.Position);
                p.jobs.StartJob(stand, JobCondition.InterruptForced, null, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: standing before the great cat", e);
            }
            string guess = wrong ? Answer(age, MogMath.WrongGuess(i, p.thingIDNumber)) : Answer(age, i);
            // apart: said aloud to her, not to any of the colony.
            string said = Flavor.FromPool(RiddlePool, p, null, null, wrong ? "wrong" : "right");
            if (!said.NullOrEmpty()) Speech.Say(p, said.Formatted(guess.Named("ANSWER")).Resolve());
            string called = god != null ? SacredAnimal.Called(a, god) : a.LabelShort;
            var args = new[] { p.Named("PAWN"), called.Named("BEAST"), guess.Named("ANSWER") };
            if (!wrong)
            {
                if (s?.a == a) Favour(s, p);
                Memory(p, RightThought);
                FleckMaker.ThrowLightningGlow(a.DrawPos, map, 1.2f);
                GodSounds.Play(GodSounds.Sound("TinyBell"), map, a.Position);
                if (god != null) Gods.AddFavour(god, map, MogMath.RightFavour, null);
                string title = GodsOnMapWords.Title(god, map, false, "CC_Mog_Right_Title", "CC_Mog_Right_TitleNamed", called.Named("BEAST"));
                Tell.Letter(NewsKind.Land, Importance.Notable, map, title, "CC_Mog_Right_Text".Translate(args).Resolve(), LetterDefOf.PositiveEvent, new LookTargets(p));
                CouncilGameComponent.Instance?.journals?.WriteText(p, "CC_Mog_Journal_Right".Translate(args).Resolve());
                if (god != null) GodsOnMapWords.Saw(god, map, a.Position, GodActKind.Blessing, p, "CC_GodM_Saw_RiddleRight", args);
                return;
            }
            // A cuff of the paw, at the shared budget's rate (and only where Rimfolk may hurt them now); else only the look she gives.
            bool cuffed = false;
            try
            {
                cuffed = Harm.Roll(p, HarmKinds.GodRiddle, MogMath.CuffChance)
                         && Harm.Injure(p, HurtKind.Scratch, HurtWhere.Arm, MogMath.CuffLow, MogMath.CuffHigh, HarmKinds.GodRiddle, HurtClass.Injury, a) != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the great cat's cuff", e);
            }
            Memory(p, WrongThought);
            if (god != null) Gods.AddFavour(god, map, MogMath.WrongFavour, null);
            string key = cuffed ? "CC_Mog_Wrong_TextCuffed" : "CC_Mog_Wrong_Text";
            string wtitle = GodsOnMapWords.Title(god, map, false, "CC_Mog_Wrong_Title", "CC_Mog_Wrong_TitleNamed", called.Named("BEAST"));
            Tell.Letter(NewsKind.Land, Importance.Notable, map, wtitle, key.Translate(args).Resolve(), cuffed ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent,
                new LookTargets(p));
            CouncilGameComponent.Instance?.journals?.WriteText(p, (cuffed ? "CC_Mog_Journal_Cuffed" : "CC_Mog_Journal_Wrong").Translate(args).Resolve());
            if (god != null) GodsOnMapWords.Saw(god, map, a.Position, GodActKind.Act, p, cuffed ? "CC_GodM_Saw_RiddleCuffed" : "CC_GodM_Saw_RiddleWrong", args);
        }

        private static void Memory(Pawn p, string thought)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (def == null || p?.needs?.mood == null) return;
            try
            {
                p.needs.mood.thoughts.memories.TryGainMemory(def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a memory of the great cat", e);
            }
        }

        // ================= A new grave kept =================

        /// <summary>The gods' day: a colonist's new grave found, and whether it is kept tonight (by her keeper, else by a cat).</summary>
        internal static void Daily(int now)
        {
            var b = MogsBook.Instance;
            var god = Her;
            if (b == null || god == null) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !map.IsPlayerHome) continue;
                Building_Grave newest = null;
                foreach (var t in Tombs(map))
                {
                    if (!(t is Building_Grave g)) continue;
                    var corpse = g.Corpse;
                    var who = corpse?.InnerPawn;
                    if (who == null || who.Faction != Faction.OfPlayer || !who.RaceProps.Humanlike || corpse.timeOfDeath <= b.lastDeath) continue;
                    if (newest == null || corpse.timeOfDeath > newest.Corpse.timeOfDeath) newest = g;
                }
                if (newest == null) continue;
                int died = newest.Corpse.timeOfDeath;
                b.lastDeath = Math.Max(b.lastDeath, died);
                if (b.grave != null || now - died > MogMath.KeepWithinDays * GenDate.TicksPerDay) continue;
                bool holds = Gods.ColonyGods(map).Contains(god);
                if (!Rand.Chance(MogMath.KeptChance(holds, GodNear.Of(god, map)))) continue;
                Keep(b, god, map, newest);
            }
        }

        /// <summary>The grave is kept tonight: her keeper with a cat where a people that holds her can come, else a cat alone.</summary>
        public static bool Keep(MogsBook b, God god, Map map, Building_Grave g)
        {
            if (b == null || god == null || map == null || g?.Corpse?.InnerPawn == null) return false;
            b.grave = g;
            b.dead = g.Corpse.InnerPawn.LabelShort;
            b.keeper = null;
            b.cat = null;
            b.keeperSat = false;
            b.catSeen = false;
            b.catOwn = false;
            var f = Rand.Chance(MogMath.KeeperChance) ? Arrivals.Friendly(x => god.factions.Contains(x)) : null;
            if (f != null && Keeper(b, god, map, g, f)) return true;
            return Cat(b, map, g, null);
        }

        /// <summary>One of her keepers comes in from the road with a cat at their heel and makes for the grave; told once, in a plain card.</summary>
        private static bool Keeper(MogsBook b, God god, Map map, Building_Grave g, Faction f)
        {
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return false;
            Pawn k;
            try
            {
                k = Arrivals.Make(kind, f, new FloatRange(30f, 70f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a keeper of the graves", e);
                return false;
            }
            if (k == null) return false;
            var spot = Beside(g, map);
            if (!spot.IsValid || Arrivals.Visit(new List<Pawn> { k }, map, f, spot, MogMath.KeeperHours) == null)
            {
                Arrivals.Discard(k);
                return false;
            }
            b.keeper = k;
            b.keeperUntil = Now + Mathf.RoundToInt(MogMath.KeeperHours * GenDate.TicksPerHour);
            var cat = NewCat(map, k.Position);
            b.cat = cat;
            b.catOwn = false;
            b.catUntil = b.keeperUntil + Mathf.RoundToInt(Rand.Range(MogMath.CatDaysMin, MogMath.CatDaysMax) * GenDate.TicksPerDay);
            var args = new[] { k.Named("KEEPER"), f.Name.Named("PEOPLE"), b.dead.Named("DEAD"), Homes.Name(map).Named("HOME") };
            Tell.Letter(NewsKind.Land, Importance.Notable, map, "CC_Mog_Keeper_Title".Translate(args).Resolve(),
                ("CC_Mog_Keeper_Text_" + GodStranger.Age).Translate(args).Resolve(), LetterDefOf.NeutralEvent, new LookTargets(k), f);
            Gods.Learn(god, "visitors");
            return true;
        }

        /// <summary>A cat of the land's walking in at a place (the game's cat), wild.</summary>
        private static Pawn NewCat(Map map, IntVec3 near)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Cat");
            if (kind == null || map == null || !near.IsValid) return null;
            try
            {
                var c = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                return (Pawn)GenSpawn.Spawn(c, CellFinder.RandomClosewalkCellNear(near, map, 3), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a cat for the grave", e);
                return null;
            }
        }

        /// <summary>A cat keeps the grave a day or two: one of the colony's own, else a stray in from the edge.</summary>
        private static bool Cat(MogsBook b, Map map, Building_Grave g, Pawn cat)
        {
            cat ??= Cats(map, true).Where(c => !(c.roping?.IsRoped ?? false) && c.CanReach(g, PathEndMode.Touch, Danger.None)).RandomElementWithFallback();
            b.catOwn = cat != null;
            if (cat == null)
            {
                var centre = CampSite.ColonyCenter(map);
                var edge = centre.IsValid ? LegendBeasts.EdgeToward(map, centre) : IntVec3.Invalid;
                cat = NewCat(map, edge);
            }
            if (cat == null)
            {
                b.grave = null;
                return false;
            }
            b.cat = cat;
            b.catUntil = Now + Mathf.RoundToInt(Rand.Range(MogMath.CatDaysMin, MogMath.CatDaysMax) * GenDate.TicksPerDay);
            return true;
        }

        /// <summary>The grave's hour: the keeper sitting the night by it, the cat on it, each in their place; done once their time is up.</summary>
        private static void Graveside(MogsBook b, int now)
        {
            if (b.grave == null) return;
            if (!(b.grave is Building_Grave g) || g.Destroyed || !g.Spawned)
            {
                b.grave = null;
                return;
            }
            var map = g.Map;
            var k = b.keeper;
            if (k != null && (k.Dead || !k.Spawned || now > b.keeperUntil + GenDate.TicksPerHour * 2)) k = b.keeper = null;
            if (k != null && !k.Downed && !k.InMentalState && k.Position.InHorDistOf(g.Position, 5f))
            {
                if (!b.keeperSat) Sat(b, k, g);
                if (k.CurJobDef != JobDefOf.Wait_MaintainPosture)
                {
                    try
                    {
                        var w = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                        w.expiryInterval = GenDate.TicksPerHour;
                        w.overrideFacing = Rot4.FromIntVec3(g.Position - k.Position);
                        k.jobs.StartJob(w, JobCondition.InterruptForced, null, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: the keeper by the grave", e);
                    }
                }
            }
            var c = b.cat;
            if (c != null && (c.Dead || !c.Spawned || c.Map != map)) c = b.cat = null;
            if (c != null)
            {
                if (now > b.catUntil)
                {
                    // It has kept the grave: a stray goes its way, the colony's own back to its day.
                    if (!b.catOwn && c.Faction == null) c.mindState.exitMapAfterTick = now + GenDate.TicksPerHour * 2;
                    b.cat = null;
                }
                else if (!c.Downed && !c.InMentalState && c.Awake())
                {
                    if (k != null && k.Spawned && now < b.keeperUntil) Follow(c, k);
                    else OnGrave(b, c, g, now);
                }
            }
            if (b.keeper == null && b.cat == null) b.grave = null;
        }

        private static void Follow(Pawn c, Pawn k)
        {
            if (c.CurJobDef == JobDefOf.FollowClose && c.CurJob?.targetA.Thing == k) return;
            Job(c, () =>
            {
                var j = JobMaker.MakeJob(JobDefOf.FollowClose, k);
                j.followRadius = 2f;
                j.expiryInterval = GenDate.TicksPerHour;
                j.checkOverrideOnExpire = true;
                return j;
            });
        }

        /// <summary>The cat on the grave (or beside it, where the grave can't be stood on); seen once, and those fond of the dead comforted.</summary>
        private static void OnGrave(MogsBook b, Pawn c, Building_Grave g, int now)
        {
            var map = g.Map;
            var at = g.Position.Standable(map) ? g.Position : Beside(g, map);
            if (!at.IsValid) return;
            if (c.Position != at)
            {
                if (c.CurJobDef == JobDefOf.Goto) return;
                if (!c.CanReach(at, PathEndMode.OnCell, Danger.Some)) return;
                Job(c, () =>
                {
                    var j = JobMaker.MakeJob(JobDefOf.Goto, at);
                    j.expiryInterval = GenDate.TicksPerHour;
                    return j;
                });
                return;
            }
            if (c.CurJobDef != JobDefOf.Wait)
            {
                Job(c, () =>
                {
                    var w = JobMaker.MakeJob(JobDefOf.Wait);
                    w.expiryInterval = GenDate.TicksPerHour * 2;
                    return w;
                });
            }
            if (b.catSeen) return;
            var seer = GodsOnMapWords.Seer(map, at, 18f);
            if (seer == null) return;
            b.catSeen = true;
            string line = Flavor.FromPool(GraveCatPool, seer, null, null, b.catOwn ? "own" : "stray");
            if (!line.NullOrEmpty()) Speech.Say(seer, line.Formatted(b.dead.Named("DEAD")).Resolve());
            Comfort(map, g, seer);
            var god = Her;
            if (god != null) GodsOnMapWords.Saw(god, map, at, GodActKind.Omen, seer, "CC_GodM_Saw_GraveCat", b.dead.Named("DEAD"), c.LabelShort.Named("CAT"));
        }

        /// <summary>The keeper sits down by the grave: a soft bell, a word over it, the book has what was said, and those fond of the dead are comforted.</summary>
        private static void Sat(MogsBook b, Pawn k, Building_Grave g)
        {
            b.keeperSat = true;
            var god = Her;
            var map = g.Map;
            GodSounds.Play(GodSounds.Sound("TinyBell"), map, g.Position);
            // apart: said over the grave, to the dead.
            string line = god != null ? Worship.Line(KeeperPool, k, god) : null;
            if (!line.NullOrEmpty())
            {
                line = line.Formatted(b.dead.Named("DEAD")).Resolve();
                Speech.Say(k, line);
                if (god != null) GodLore.Heard(k, god, line, "Gate");
            }
            Comfort(map, g, null);
        }

        /// <summary>Everyone of the colony fond of the one buried there (or who saw it) takes comfort that the grave was kept.</summary>
        private static void Comfort(Map map, Building_Grave g, Pawn seer)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(KeptThought);
            var dead = g?.Corpse?.InnerPawn;
            if (def == null || dead == null) return;
            foreach (var p in Homes.People(map).Where(p => p.Spawned && p.needs?.mood != null && People.HasInnerLife(p)))
            {
                int opinion;
                try
                {
                    opinion = p.relations?.OpinionOf(dead) ?? 0;
                }
                catch
                {
                    opinion = 0;
                }
                if (opinion < 10 && p != seer) continue;
                try
                {
                    p.needs.mood.thoughts.memories.TryGainMemory(def, dead);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: comfort at the grave", e);
                }
            }
        }

        // ================= The wrapped dead =================

        /// <summary>The wrapped dead's bodies, once they fall, gone to dust at the next step.</summary>
        private static readonly List<Pawn> fallen = new List<Pawn>();

        /// <summary>A raider (anyone hostile to the colony who isn't one of the entities) up and about on the map.</summary>
        public static bool Raiders(Map map)
        {
            if (map == null) return false;
            var player = Faction.OfPlayer;
            var entities = ModsConfig.AnomalyActive ? Faction.OfEntities : null;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (p.Faction != null && p.Faction != entities && !p.Dead && !p.Downed && p.HostileTo(player) && !p.IsPrisoner)
                    return true;
            return false;
        }

        /// <summary>A raider within <paramref name="range"/> of a cell.</summary>
        private static bool RaiderNear(Map map, IntVec3 at, float range)
        {
            var player = Faction.OfPlayer;
            var entities = ModsConfig.AnomalyActive ? Faction.OfEntities : null;
            return map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && p.Faction != entities && !p.Dead && !p.Downed && p.HostileTo(player) && !p.IsPrisoner
                                                        && p.Position.InHorDistOf(at, range));
        }

        /// <summary>The gods' hour: the grave kept, and in a raid near a home's tombs, whether the wrapped dead stand guard.</summary>
        internal static void Hourly(int now)
        {
            var b = MogsBook.Instance;
            if (b == null) return;
            GodsOnMapBook.Safe("the grave kept", () => Graveside(b, now));
            GodsOnMapBook.Safe("her presence", () => GameCondition_MogPresence.Hourly(now));
            if (!ModsConfig.AnomalyActive || now - b.lastRisen < MogMath.RiseGapDays * GenDate.TicksPerDay || now - b.lastWeighed < GenDate.TicksPerDay) return;
            var god = Her;
            if (god == null || !Harm.On) return;
            var beast = GodsOnMapBook.Instance?.beast;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !map.IsPlayerHome || !MapFacts.ThreatOn(map)) continue;
                var tombs = Tombs(map);
                if (tombs.Count < MogMath.MinTombs) continue;
                var mid = Middle(tombs);
                if (!RaiderNear(map, mid, MogMath.EnemyNear)) continue;
                // Weighed once for a raid: the first hour it comes near the tombs.
                b.lastWeighed = now;
                bool onLand = SacredAnimal.IsMog(beast) && beast.map == map;
                float chance = MogMath.RiseChance(onLand, GodNear.Of(god, map), tombs.Count);
                if (chance <= 0f || !Harm.Roll(HarmKinds.GodDead, chance)) continue;
                if (Rise(god, map, tombs, mid)) b.lastRisen = now;
                return;
            }
        }

        /// <summary>The wrapped dead stand up among the tombs: the game's shamblers of the entities, in grey linen, kept to the graves.</summary>
        public static bool Rise(God god, Map map, List<Thing> tombs, IntVec3 mid)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ShamblerKind);
            var mark = DefDatabase<HediffDef>.GetNamedSilentFail(WrappedHediff);
            var entities = ModsConfig.AnomalyActive ? Faction.OfEntities : null;
            if (kind == null || mark == null || entities == null || map == null || tombs.NullOrEmpty() || !mid.IsValid) return false;
            int n = MogMath.Risen(tombs.Count);
            var risen = new List<Pawn>();
            foreach (var t in tombs.InRandomOrder().Take(n))
            {
                var at = Beside(t, map);
                if (!at.IsValid) continue;
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, entities, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    if (p == null) continue;
                    Wrap(p);
                    GenSpawn.Spawn(p, at, map);
                    p.health.AddHediff(mark);
                    DefDatabase<EffecterDef>.GetNamedSilentFail("ShamblerRaise")?.Spawn(at, map).Cleanup();
                    FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 1.4f, new Color(0.82f, 0.76f, 0.62f));
                    FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Dirt, 2);
                    risen.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the wrapped dead rising", e);
                }
            }
            if (risen.Count == 0) return false;
            try
            {
                LordMaker.MakeNewLord(entities, new LordJob_DefendPoint(mid, MogMath.WanderRadius, MogMath.GuardRadius, false, false), map, risen);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the wrapped dead keeping the graves", e);
            }
            string home = Homes.Name(map);
            var args = new[] { home.Named("HOME"), risen.Count.Named("COUNT") };
            string title = GodsOnMapWords.Title(god, map, false, "CC_Mog_Risen_Title", "CC_Mog_Risen_TitleNamed", args);
            Tell.Letter(NewsKind.Land, Importance.Important, map, title, ("CC_Mog_Risen_Text_" + GodStranger.Age).Translate(args).Resolve(), LetterDefOf.ThreatSmall,
                new LookTargets(risen));
            var seer = GodsOnMapWords.Seer(map, mid, 30f);
            if (seer != null)
            {
                string line = Flavor.FromPool(RisenPool, seer, null, null, "rise");
                if (!line.NullOrEmpty()) Speech.Say(seer, line);
            }
            var saw = map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && People.HasInnerLife(p) && p.Position.InHorDistOf(mid, 30f) && GenSight.LineOfSight(p.Position, mid, map, true))
                .ToList();
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(RisenThought);
            if (def != null)
                foreach (var p in saw.Where(p => p.needs?.mood != null))
                    p.needs.mood.thoughts.memories.TryGainMemory(def);
            GodsOnMapWords.Saw(god, map, mid, GodActKind.Act, seer, "CC_GodM_Saw_WrappedDead");
            GodMarks.Note(god, map, mid, "CC_Mog_Risen_Act".Translate().Resolve(), PresenceKind.Act);
            if (saw.Count > 0) GodMarks.Witnessed(map, mid, "CC_Mog_Memory_Risen".Translate().Resolve(), saw);
            return true;
        }

        /// <summary>Linen for the dead: their own clothes taken off, a wrap and a head wrap (Ideology's, else the game's robe and veil), grey-white, and no weapon.</summary>
        private static void Wrap(Pawn p)
        {
            try
            {
                p.equipment?.DestroyAllEquipment();
                p.apparel?.DestroyAll();
                var cloth = ThingDefOf.Cloth;
                var linen = new Color(0.84f, 0.8f, 0.7f);
                foreach (var names in new[] { new[] { "Apparel_Broadwrap", "Apparel_Robe", "Apparel_TribalA" }, new[] { "Apparel_Headwrap", "Apparel_WarVeil" } })
                {
                    var def = names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.IsApparel);
                    if (def == null || p.apparel == null || !ApparelUtility.HasPartsToWear(p, def)) continue;
                    var app = (Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? cloth : null);
                    if (app.TryGetComp<CompColorable>() != null) app.SetColor(linen, false);
                    p.apparel.Wear(app, false, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the wrapped dead's linen", e);
            }
        }

        /// <summary>One of the wrapped dead lies down as dust (the map quiet of raiders, or its watch long kept): told once for them all.</summary>
        internal static void LieDown(Pawn p)
        {
            if (p == null || !p.Spawned) return;
            var map = p.Map;
            var at = p.Position;
            try
            {
                FleckMaker.ThrowDustPuffThick(p.DrawPos, map, 1.6f, new Color(0.82f, 0.76f, 0.62f));
                FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Dirt, 2);
                p.DeSpawn();
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
                else Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the wrapped dead lying down", e);
                return;
            }
            var b = MogsBook.Instance;
            if (b == null || Now - b.lastDownTold < GenDate.TicksPerHour) return;
            b.lastDownTold = Now;
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_Mog_Risen_Down".Translate(Homes.Name(map).Named("HOME")).Resolve(), new LookTargets(at, map),
                MessageTypeDefOf.NeutralEvent);
            var god = Her;
            if (god != null) GodsOnMapWords.Saw(god, map, at, GodActKind.Depart, GodsOnMapWords.Seer(map, at, 30f), "CC_GodM_Saw_WrappedDeadDown");
        }

        /// <summary>One of the wrapped dead fell: its body goes to dust at the next step (nothing of them is stripped or butchered).</summary>
        internal static void Fell(Pawn p)
        {
            if (p == null) return;
            fallen.Add(p);
            MogsBook.Instance?.Due(Now + 1);
        }

        internal static void Dust()
        {
            if (fallen.Count == 0) return;
            var list = fallen.ToList();
            fallen.Clear();
            foreach (var p in list)
            {
                var corpse = p?.Corpse;
                if (corpse == null || corpse.Destroyed) continue;
                try
                {
                    if (corpse.Spawned)
                    {
                        FleckMaker.ThrowDustPuffThick(corpse.DrawPos, corpse.Map, 1.4f, new Color(0.82f, 0.76f, 0.62f));
                        FilthMaker.TryMakeFilth(corpse.Position, corpse.Map, ThingDefOf.Filth_Dirt, 2);
                    }
                    corpse.Destroy();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a wrapped body to dust", e);
                }
            }
        }
    }

    /// <summary>
    /// Tamiu's presence on the land (Rimfolk_GodPresence_Mog), her own and apart from the shared one of a god's moment: from
    /// her coming to her going, in the corner of the screen as what anyone can see ("the cats keeping watch"), or by the
    /// colony's name for her once it knows it is her ("Tamiu keeping watch"; Gods.CardName). While it lasts: the land's rats
    /// run off it as she comes, and every few hours the cats go and sit facing the graves a while (MogsWays.CatsWatch).
    /// </summary>
    public class GameCondition_MogPresence : GameCondition
    {
        /// <summary>The cats go and sit with the graves about this often while she stays (a chance an hour).</summary>
        public const float WatchHourChance = 0.2f;

        private static God Her => MogsWays.Her;

        private string Named => Her == null || SingleMap == null ? null : Gods.CardName(Her, SingleMap);

        public override string Label =>
            Named is string n ? "CC_Mog_Presence_Named".Translate(n.Named("NAME")).Resolve() : "CC_Mog_Presence".Translate().Resolve();

        public override string Description =>
            Named is string n ? "CC_Mog_Presence_NamedDesc".Translate(n.Named("NAME")).Resolve() : "CC_Mog_Presence_Desc".Translate().Resolve();

        public override void Init()
        {
            base.Init();
            var map = SingleMap;
            if (map == null) return;
            // The land's rats and mice run off it as she comes.
            int now = Find.TickManager.TicksGame;
            foreach (var p in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps.Animal && !p.Dead && !p.Downed && Vermin(p.def)).ToList())
                p.mindState.exitMapAfterTick = now + Rand.Range(60, 1200);
            MogsWays.CatsWatch(map);
        }

        private static bool Vermin(ThingDef d)
        {
            string n = d?.defName ?? "";
            return n == "Rat" || n.IndexOf("Rat_", StringComparison.Ordinal) >= 0 || n.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("Rodent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Her presence begins on <paramref name="map"/> until <paramref name="until"/> (her stay's end), or goes on if it already has.</summary>
        internal static void Begin(Map map, int until)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(MogsWays.PresenceDef);
            if (map == null || def == null) return;
            try
            {
                if (map.gameConditionManager.GetActiveCondition<GameCondition_MogPresence>() != null) return;
                int ticks = Math.Max(GenDate.TicksPerDay, until - Find.TickManager.TicksGame + GenDate.TicksPerDay);
                map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, ticks));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the great cat's presence", e);
            }
        }

        /// <summary>The gods' hour: ended where she has gone (or was harmed and left), and now and then the cats keep watch with the graves.</summary>
        internal static void Hourly(int now)
        {
            var s = GodsOnMapBook.Instance?.beast;
            foreach (var map in Homes.All.ToList())
            {
                var c = map?.gameConditionManager?.GetActiveCondition<GameCondition_MogPresence>();
                if (c == null) continue;
                if (!(SacredAnimal.IsMog(s) && s.map == map && s.a.Spawned))
                {
                    c.End();
                    continue;
                }
                if (Rand.Chance(WatchHourChance)) MogsWays.CatsWatch(map);
            }
        }
    }

    /// <summary>The wrapped dead's mark (Rimfolk_WrappedDead): it lies down as dust when the map is quiet of raiders, and its body goes to dust if it falls.</summary>
    public class HediffCompProperties_WrappedDead : HediffCompProperties
    {
        public HediffCompProperties_WrappedDead()
        {
            compClass = typeof(HediffComp_WrappedDead);
        }
    }

    public class HediffComp_WrappedDead : HediffComp
    {
        public int risenAt = -1, loudAt = -1;

        public override void CompPostMake()
        {
            base.CompPostMake();
            risenAt = loudAt = Find.TickManager.TicksGame;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            var p = Pawn;
            if (p == null || !p.Spawned || !p.IsHashIntervalTick(250)) return;
            int now = Find.TickManager.TicksGame;
            try
            {
                if (MogsWays.Raiders(p.Map)) loudAt = now;
                if (MogMath.LieDown(now - loudAt, now - risenAt)) MogsWays.LieDown(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the wrapped dead's watch", e);
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            MogsWays.Fell(Pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref risenAt, "risenAt", -1);
            Scribe_Values.Look(ref loudAt, "loudAt", -1);
        }
    }

    /// <summary>
    /// Her riddle's card (Rimfolk_Evt_GodRiddle): fired by her stay (MogsWays.Ask), never by the storyteller. A is the
    /// colony's wits (the best at Intellectual), B its tongue (the best at Social, someone else); which of them walks out, or
    /// nobody. The card's risk is the answer coming out wrong, by the answerer's own skill (MogMath.WrongChance), and it is
    /// told only when they get there.
    /// </summary>
    public class HappeningWorker_GodRiddle : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            MogsWays.pending?.a != null && MogsWays.pending.a.Map == map && MogsBook.Instance?.riddleOf == MogsWays.pending.a;

        private static IEnumerable<Pawn> Able(Map map, Pawn beast) =>
            Colonists(map).Where(p => p.DevelopmentalStage.Adult() && !p.Downed && !p.InMentalState && Together.Able(p) && p.CanReach(beast, PathEndMode.Touch, Danger.Some));

        private static int Level(Pawn p, SkillDef s) => p?.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var s = MogsWays.pending;
            var b = MogsBook.Instance;
            if (s?.a == null || b == null) return false;
            var able = Able(ctx.map, s.a).ToList();
            ctx.a = able.OrderByDescending(p => Level(p, SkillDefOf.Intellectual)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = able.Where(p => p != ctx.a).OrderByDescending(p => Level(p, SkillDefOf.Social)).FirstOrDefault();
            ctx.place = s.a;
            ctx.spot = b.door;
            ctx.note = s.god.ToString();
            ctx.beast = SacredAnimal.Called(s.a, s.God);
            ctx.tale = MogsWays.Question(b.riddleAge, b.riddle);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var b = MogsBook.Instance;
            tags.Add("age" + Mathf.Clamp(b?.riddleAge ?? GodStranger.Age, 0, 3));
            if (ctx.b == null) tags.Add("alone");
        }

        protected override LookTargets Look(HappeningContext ctx)
        {
            var targets = new List<GlobalTargetInfo>();
            if (ctx.place != null && ctx.place.Spawned) targets.Add(ctx.place);
            if (ctx.a != null && ctx.a.Spawned) targets.Add(ctx.a);
            return targets.Count > 0 ? new LookTargets(targets) : LookTargets.Invalid;
        }

        public override string Unavailable(HappeningContext ctx, int option) => option == 1 && ctx.b == null ? "CC_Mog_Riddle_NoOther".Translate().Resolve() : null;

        public override float RiskChance(HappeningContext ctx, int option, float chance)
        {
            if (option == 0) return MogMath.WrongChance(Level(ctx.a, SkillDefOf.Intellectual));
            if (option == 1) return MogMath.WrongChance(Level(ctx.b, SkillDefOf.Social));
            return base.RiskChance(ctx, option, chance);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var beast = ctx.place as Pawn;
            var b = MogsBook.Instance;
            if (option == 0) MogsWays.Answered(beast, ctx.a, risk);
            else if (option == 1) MogsWays.Answered(beast, ctx.b ?? ctx.a, risk);
            else if (b != null && b.riddleOf == beast)
                // Nobody goes: she lies there a while longer and goes back to the graves.
                b.riddleUntil = Math.Min(b.riddleUntil, Find.TickManager.TicksGame + GenDate.TicksPerHour * 2);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
