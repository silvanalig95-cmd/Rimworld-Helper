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
    /// The colony's animals in their days, each thing played out on the map by the game's own jobs or Rimfolk's animal jobs,
    /// with a line over its person's head (one sentence, Speech.Say, the shared once-a-day rate) and a word on its card ("lately"):
    /// <list type="bullet">
    /// <item>home again: someone back after hours away (a caravan, another place), and their animal comes running to them;</item>
    /// <item>bedtime: a pet goes to sleep at the foot of its person's bed, and in the morning they say so;</item>
    /// <item>meals: a pet sits at someone's elbow while they eat and watches every bite;</item>
    /// <item>play: a child at leisure plays chase with an animal, the two running about together;</item>
    /// <item>the herd: in the morning a herd of one kind moves off behind its eldest (the herd's leader where it is one of
    /// them) to graze somewhere new;</item>
    /// <item>the rounds: the herdmaster, or whoever keeps the animals, goes round them one after another, each holding still
    /// to be looked over;</item>
    /// <item>the weather: rain or snow coming on, the pets out in it go in under a roof; the young ones play in the first snow;
    /// on a bitter night the herd lies down close together;</item>
    /// <item>raiders: a pet raises the alarm as they come, facing them, with its own call;</item>
    /// <item>a god's presence over the land: the pets won't settle and keep close to their people;</item>
    /// <item>a death: an animal whose person died goes to lie by them, and cries.</item>
    /// </list>
    /// Every animal has its person for this: bonded, its master, else the one it has taken to (AnimalFacts.PersonOf), shown
    /// on its card. What is said comes as it is seen: the hello as the pet reaches its person, the word at a meal as it sits
    /// down, the remark as it lies down under the roof or by the dead (AnimalArrivals). Never a letter or a message, nobody
    /// hurt. "Pets as companions" on (People); the pace is the animals' moments on How much happens. Nothing here is saved:
    /// what waits (a homecoming, a morning's word) starts again after a load.
    /// </summary>
    public static class AnimalDays
    {
        public static bool On => AnimalLife.On;

        private static float Rate => AnimalTempers.Rate;

        private static int Now => Find.TickManager.TicksGame;

        public const string Pool = "Rimfolk_Pool_AnimalDays";

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.85f);

        // ---------------- What waits (this session) ----------------

        /// <summary>Each animal whose person is away: when they were first and last seen away.</summary>
        private static readonly Dictionary<int, int> awayFrom = new Dictionary<int, int>(), awayLast = new Dictionary<int, int>();
        /// <summary>Pets asleep at the foot of their person's bed, for the morning's word.</summary>
        private static readonly Dictionary<int, (Pawn person, int tick)> footNight = new Dictionary<int, (Pawn, int)>();
        /// <summary>Animals whose person just died: whom, and since when.</summary>
        private static readonly Dictionary<int, (Pawn dead, int tick)> mourners = new Dictionary<int, (Pawn, int)>();
        /// <summary>Whom each animal was with an hour ago, to see that they died before another takes their place.</summary>
        private static readonly Dictionary<int, Pawn> personSeen = new Dictionary<int, Pawn>();
        private static readonly Dictionary<int, string> weatherSeen = new Dictionary<int, string>();
        private static readonly Dictionary<int, int> alarmAt = new Dictionary<int, int>();
        private static readonly HashSet<string> presencesSeen = new HashSet<string>();
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            awayFrom.Clear();
            awayLast.Clear();
            footNight.Clear();
            mourners.Clear();
            personSeen.Clear();
            weatherSeen.Clear();
            alarmAt.Clear();
            presencesSeen.Clear();
        }

        private static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal days: " + what, e);
            }
        }

        // ---------------- The hour ----------------

        /// <summary>Once an hour (AnimalLife.Hourly), on each home map: what each hour has room for, one of each at most.</summary>
        public static void Hourly()
        {
            if (!On) return;
            Fresh();
            if (Rate <= 0f) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                var animals = map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList();
                int hour = GenLocalDate.HourOfDay(map);
                // Who is away is kept in danger too; the welcome waits for it to pass (within its three hours).
                bool calm = map.dangerWatcher.DangerRating == StoryDanger.None;
                Safe("home again", () => Homecomings(map, animals, calm));
                Safe("whom each is with", () => Deaths(animals));
                if (!calm) continue;
                Safe("a god near", () => Uneasy(map, animals));
                Safe("a death", () => Mourn(map));
                Safe("the weather", () => Weather(map, hour));
                if (AnimalDaysMath.BedHour(hour)) Safe("the foot of the bed", () => FootOfBed(map, animals));
                if (AnimalDaysMath.WakingHour(hour)) Safe("the morning after", () => Mornings(map));
                if (AnimalDaysMath.MealHour(hour)) Safe("a meal", () => Begging(map, animals));
                if (AnimalDaysMath.PlayHour(hour)) Safe("play", () => ChildPlay(map));
                if (AnimalDaysMath.HerdHour(hour)) Safe("the herd", () => HerdMoves(map));
                if (AnimalDaysMath.RoundsHour(hour)) Safe("the rounds", () => Rounds(map));
                if (AnimalDaysMath.NightHour(hour)) Safe("the huddle", () => Huddle(map));
            }
        }

        private static float Chance(AnimalDay d) => AnimalDaysMath.HourChance(d, Rate, AnimalDaysMath.HoursOf(d));

        // ---------------- Helpers ----------------

        internal static bool Start(Pawn a, Job job)
        {
            if (a?.jobs == null || job == null) return false;
            a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return a.CurJob == job;
        }

        /// <summary>
        /// Starts the job, with <paramref name="arrived"/> run when it gets where it is going (AnimalArrivals: the linger's spot,
        /// the one it runs to, the animal someone sits with), so what is said comes with what is seen.
        /// </summary>
        internal static bool StartThen(Pawn a, Job job, Action arrived)
        {
            if (job == null) return false;
            AnimalArrivals.On(job, arrived);
            if (Start(a, job)) return true;
            AnimalArrivals.Forget(job);
            return false;
        }

        /// <summary>
        /// It goes to them at this pace and keeps at their side for <paramref name="ticks"/> (JobDriver_AnimalRunTo): to greet
        /// them with the game's nuzzle (Rimfolk_AnimalGreet), to keep close (Rimfolk_AnimalKeepClose), or at their heels a
        /// moment (Rimfolk_AnimalPester).
        /// </summary>
        internal static Job RunToJob(JobDef def, Pawn whom, LocomotionUrgency pace, int ticks)
        {
            var job = JobMaker.MakeJob(def, whom);
            job.count = ticks;
            job.locomotionUrgency = pace;
            return job;
        }

        internal static Job FollowJob(Pawn whom, float radius, int ticks, LocomotionUrgency pace = LocomotionUrgency.Walk)
        {
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, whom);
            job.followRadius = radius;
            job.expiryInterval = ticks;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = pace;
            return job;
        }

        /// <summary>
        /// Up for a moment: calm, not roped, in no group, and awake and idle; or, with <paramref name="lyingToo"/>, lying down
        /// of its own accord (a pet gets up from its basket to go to its person's bed).
        /// </summary>
        private static bool Idle(Pawn a, bool lyingToo = false) =>
            MoreHabits.Calm(a) && a.GetLord() == null && !(a.roping?.IsRoped ?? false) && (AnimalTempers.Free(a) || lyingToo && a.CurJobDef == JobDefOf.LayDown);

        /// <summary>Its tags for the lines: the moment, its temperament, its body, a Pokémon, young or old, its family.</summary>
        internal static string[] Tags(Pawn a, string moment, params string[] more)
        {
            var tags = new List<string> { moment, HerdStoryMath.BodyTag(AnimalFacts.Body(a)) };
            var t = AnimalTempers.Of(a);
            if (t != null) tags.Add("temper:" + AnimalNatureMath.Key(t.Value));
            string family = AnimalNatureMath.Family(a.def?.defName, a.def?.label, AnimalFacts.Body(a) == HerdStoryMath.HerdBody.Bird, a.RaceProps.Insect);
            tags.Add("family:" + family);
            if (PokeWorldLink.IsPokemon(a)) tags.Add("pokemon");
            if (AnimalFacts.Young(a)) tags.Add("young");
            if (AnimalFacts.Old(a)) tags.Add("oldanimal");
            tags.AddRange(more.Where(m => !m.NullOrEmpty()));
            return tags.ToArray();
        }

        /// <summary>Its person says it, of it or to it: on screen, free to talk, and within earshot of it.</summary>
        internal static bool Say(Pawn who, Pawn a, string[] tags, params NamedArgument[] args)
        {
            if (!VisualTouches.On || !Speech.On || who == null || a == null || !who.Spawned) return false;
            var vt = VisualTouches.Instance;
            if (vt == null || !VisualTouches.InView(out var map, out var view) || map != who.Map) return false;
            int now = Now;
            if (!vt.Free(who, map, view, now) || !Together.Hears(who, a, 16f)) return false;
            var all = new List<NamedArgument> { a.Named("PET") };
            all.AddRange(args);
            string line = Interplay.Line(Pool, who, tags, all.ToArray());
            // apart: said to the animal at their side, or of it, by its own person.
            if (line.NullOrEmpty() || !Speech.Say(who, line, null, SpeechColor)) return false;
            vt.Spoke(who, now + GenDate.TicksPerDay);
            return true;
        }

        /// <summary>Someone who sees it says a word about it (its person first): AnimalTempers.Remark with this pool.</summary>
        private static bool Remark(Pawn a, string[] tags, Pawn prefer, params NamedArgument[] args) => AnimalTempers.Remark(a, Pool, tags, prefer, args);

        // ---------------- Home again ----------------

        /// <summary>
        /// Someone away for hours (a caravan, a site, another home) and back: their animal comes running, a nuzzle, and they
        /// say hello as it reaches them. The loyal ones have their own homecoming (AnimalTempers); the herd's ones come less
        /// often than the pets. Not while danger is about: the welcome waits for it to pass.
        /// </summary>
        private static void Homecomings(Map map, List<Pawn> animals, bool calm)
        {
            foreach (var a in animals)
            {
                int id = a.thingIDNumber;
                var p = AnimalFacts.PersonOf(a);
                if (p == null || p.Dead)
                {
                    awayFrom.Remove(id);
                    awayLast.Remove(id);
                    continue;
                }
                if (p.MapHeld != map)
                {
                    if (!awayFrom.ContainsKey(id)) awayFrom[id] = Now;
                    awayLast[id] = Now;
                    continue;
                }
                if (!awayFrom.TryGetValue(id, out int from)) continue;
                int last = awayLast.TryGetValue(id, out int l) ? l : from;
                float hoursAway = (last - from) / (float)GenDate.TicksPerHour + 1f, hoursBack = (Now - last) / (float)GenDate.TicksPerHour;
                if (!AnimalDaysMath.Homecoming(hoursAway, hoursBack))
                {
                    awayFrom.Remove(id);
                    awayLast.Remove(id);
                    continue;
                }
                // Back on the map but not up yet (still riding in, asleep), or danger about: next hour.
                if (!calm || !Together.Able(p) || !p.Spawned) continue;
                awayFrom.Remove(id);
                awayLast.Remove(id);
                if (AnimalTempers.Of(a) == Temper.Loyal || !Rand.Chance(AnimalDaysMath.GreetChance(AnimalFacts.Pet(a)))) continue;
                var tags = Tags(a, "day:greet");
                Greet(a, p, () => Say(p, a, tags));
            }
        }

        /// <summary>
        /// It runs to them and nuzzles them (the game's own nuzzle, JobDriver_AnimalRunTo), then keeps at their side a moment;
        /// <paramref name="arrived"/> runs as it reaches them. At a walk for an old one slowing down (<paramref name="walk"/>),
        /// staying longer. Its card says <paramref name="note"/> (home again, by default).
        /// </summary>
        internal static bool Greet(Pawn a, Pawn p, Action arrived = null, bool walk = false, string note = "CC_AnimalDay_Greet")
        {
            if (!Together.Able(p) || !MoreHabits.Calm(a) || a.GetLord() != null || (a.roping?.IsRoped ?? false) || !a.Awake()) return false;
            if (!a.Position.InHorDistOf(p.Position, 70f) || !a.CanReach(p, PathEndMode.Touch, Danger.Some)) return false;
            var job = RunToJob(AnimalDaysDefOf.Rimfolk_AnimalGreet, p, walk ? LocomotionUrgency.Walk : LocomotionUrgency.Jog, walk ? AnimalDaysMath.SideTicks / 2 : AnimalDaysMath.GreetTicks);
            if (!StartThen(a, job, arrived)) return false;
            AnimalTempers.Noted(a, note, p.Named("PAWN"));
            return true;
        }

        // ---------------- The foot of the bed ----------------

        /// <summary>At bedtime a pet whose person is asleep in their own bed goes and lies down at its foot, asleep. One a map an hour.</summary>
        private static void FootOfBed(Map map, List<Pawn> animals)
        {
            foreach (var a in animals.InRandomOrder())
            {
                if (!AnimalDaysMath.MaySleepAtFoot(a.RaceProps.baseBodySize, !AnimalFacts.Pet(a)) || footNight.ContainsKey(a.thingIDNumber) || !Idle(a, true)) continue;
                var p = AnimalFacts.PersonOf(a);
                if (p == null || !p.Spawned || p.Map != map || p.Awake()) continue;
                if (!Rand.Chance(Chance(AnimalDay.FootOfBed))) continue;
                if (!AnimalTempers.Ready("day:foot|" + a.thingIDNumber, AnimalDaysMath.Days(AnimalDay.FootOfBed) * 0.4f)) continue;
                if (!SleepAtFoot(a, p)) continue;
                footNight[a.thingIDNumber] = (p, Now);
                return;
            }
        }

        /// <summary>It goes to the foot of their bed, in the same room, and sleeps there. False when they aren't in their own bed or there's no room.</summary>
        internal static bool SleepAtFoot(Pawn a, Pawn p)
        {
            var bed = p.CurrentBed();
            if (bed == null || bed != p.ownership?.OwnedBed || bed.Medical || !bed.Spawned || bed.Map != a.Map) return false;
            if (!FootCell(a, bed, p, out var cell) || !Start(a, AnimalTempers.Nap(cell, AnimalDaysMath.FootTicks))) return false;
            AnimalTempers.Noted(a, "CC_AnimalDay_FootOfBed", p.Named("PAWN"));
            return true;
        }

        /// <summary>
        /// The cell just past the foot of the sleeper's place in the bed, else one beside its foot (AnimalDaysMath.FootCells),
        /// else any beside the bed, in the same room.
        /// </summary>
        private static bool FootCell(Pawn a, Building_Bed bed, Pawn sleeper, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var map = bed.Map;
            var room = bed.GetRoom();
            int slot = bed.GetCurOccupantSlotIndex(sleeper);
            var foot = bed.GetFootSlotPos(slot < 0 ? 0 : slot);
            var face = bed.Rotation.FacingCell;
            bool Ok(IntVec3 c) => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && c.GetRoom(map) == room && a.CanReach(c, PathEndMode.OnCell, Danger.Some);
            foreach (var (x, z) in AnimalDaysMath.FootCells(foot.x, foot.z, face.x, face.z))
            {
                var c = new IntVec3(x, 0, z);
                if (!Ok(c)) continue;
                cell = c;
                return true;
            }
            foreach (var c in GenAdj.CellsAdjacentCardinal(bed))
            {
                if (!Ok(c)) continue;
                cell = c;
                return true;
            }
            return false;
        }

        /// <summary>The morning after: its person up, they say where it slept (and may write it down).</summary>
        private static void Mornings(Map map)
        {
            foreach (var kv in footNight.ToList())
            {
                var (p, tick) = kv.Value;
                if (p == null || p.Dead || Now - tick > GenDate.TicksPerDay * 3 / 5)
                {
                    footNight.Remove(kv.Key);
                    continue;
                }
                if (p.Map != map || !Together.Able(p)) continue;
                footNight.Remove(kv.Key);
                var a = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(x => x.thingIDNumber == kv.Key);
                if (a == null) continue;
                Say(p, a, Tags(a, "day:foot"));
                if (AnimalTempers.Ready("day:foot-journal|" + a.thingIDNumber, 12f))
                    Interplay.Journal(p, "Rimfolk_Journal_AnimalDays", Tags(a, "day:foot"), a.Named("PET"));
            }
        }

        // ---------------- A meal ----------------

        /// <summary>Someone eating now, sat with their food in front of them (not on the way to it).</summary>
        internal static bool Eating(Pawn p)
        {
            if (p.CurJobDef != JobDefOf.Ingest || p.pather.Moving || !Together.Able(p)) return false;
            var food = p.CurJob.targetA.Thing;
            if (food?.def == null || food.def.IsDrug || !food.def.IsNutritionGivingIngestible) return false;
            return p.carryTracker?.CarriedThing == food || food.Position.InHorDistOf(p.Position, 1.5f);
        }

        /// <summary>A pet near someone eating (its person first) goes to sit at their elbow and watches every bite. One a map an hour.</summary>
        private static void Begging(Map map, List<Pawn> animals)
        {
            var eaters = map.mapPawns.FreeColonistsSpawned.Where(Eating).ToList();
            if (eaters.Count == 0) return;
            foreach (var a in animals.InRandomOrder())
            {
                if (!AnimalDaysMath.MayBeg(a.RaceProps.baseBodySize, !AnimalFacts.Pet(a)) || !Idle(a)) continue;
                bool greedy = AnimalTempers.Of(a) == Temper.Greedy;
                if (!Rand.Chance(Chance(AnimalDay.Beg) * (greedy ? 1.5f : 1f))) continue;
                var person = AnimalFacts.PersonOf(a);
                var eater = eaters.Where(p => p.Position.InHorDistOf(a.Position, 30f) && a.CanReach(p, PathEndMode.Touch, Danger.Some))
                    .OrderByDescending(p => p == person).ThenBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                if (eater == null) continue;
                if (!AnimalTempers.Ready("day:beg|" + a.thingIDNumber, AnimalDaysMath.Days(AnimalDay.Beg) * 0.4f)) continue;
                var tags = Tags(a, "day:beg", eater == person ? "mine" : null);
                // They tell it no as it sits down beside them, still eating.
                if (!Beg(a, eater, AnimalDaysMath.BegTicks, () =>
                    {
                        if (Eating(eater)) Say(eater, a, tags);
                    })) continue;
                return;
            }
        }

        /// <summary>It goes to sit at their elbow, facing them, a while; <paramref name="arrived"/> as it sits down there.</summary>
        internal static bool Beg(Pawn a, Pawn eater, int ticks, Action arrived = null)
        {
            if (!AnimalTempers.CellNear(a, eater.Position, 1, out var cell)) return false;
            if (!StartThen(a, AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalBeg, cell, eater, ticks, eater), arrived)) return false;
            AnimalTempers.Noted(a, "CC_AnimalDay_Beg", eater.Named("PAWN"));
            return true;
        }

        // ---------------- Play ----------------

        /// <summary>A child at leisure and a calm animal near them (theirs first): a game of chase. One a map an hour.</summary>
        private static void ChildPlay(Map map)
        {
            var children = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.DevelopmentalStage.Child() && Together.Able(p) && Together.FreeTime(p) && p.GetLord() == null && p.CurJobDef != AnimalDaysDefOf.Rimfolk_PlayWithAnimal)
                .ToList();
            foreach (var c in children.InRandomOrder())
            {
                if (!Rand.Chance(Chance(AnimalDay.ChildPlay))) continue;
                var a = map.mapPawns.SpawnedColonyAnimals
                    .Where(x => Idle(x) && AnimalDaysMath.PlaysWithChild(x.RaceProps.baseBodySize, x.RaceProps.predator) && x.Position.InHorDistOf(c.Position, 25f)
                                && c.CanReach(x, PathEndMode.Touch, Danger.None))
                    .OrderByDescending(x => AnimalFacts.PersonOf(x) == c).ThenBy(x => x.Position.DistanceToSquared(c.Position)).FirstOrDefault();
                if (a == null) continue;
                if (!AnimalTempers.Ready("day:play|" + c.thingIDNumber, AnimalDaysMath.Days(AnimalDay.ChildPlay) * 0.5f)) continue;
                if (!Play(c, a, AnimalDaysMath.PlayTicks)) continue;
                Say(c, a, Tags(a, "day:play"));
                return;
            }
        }

        /// <summary>The two run about together: it bounds off from spot to spot about them, and they chase after it.</summary>
        internal static bool Play(Pawn p, Pawn a, int ticks)
        {
            if (!Together.Able(p) || !MoreHabits.Calm(a) || !a.Awake()) return false;
            var bound = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_AnimalPlay, p, LocalTargetInfo.Invalid, p);
            bound.count = ticks;
            bound.locomotionUrgency = LocomotionUrgency.Jog;
            if (!Start(a, bound)) return false;
            var chase = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_PlayWithAnimal, a);
            chase.count = ticks;
            chase.locomotionUrgency = LocomotionUrgency.Jog;
            Start(p, chase);
            AnimalTempers.Noted(a, "CC_AnimalDay_Play", p.Named("PAWN"));
            return true;
        }

        // ---------------- The herd ----------------

        /// <summary>The herd's eldest of this kind, or the herd's leader when it is one of them.</summary>
        private static Pawn Eldest(Map map, List<Pawn> members)
        {
            var leader = CouncilGameComponent.Instance?.HerdOf(map)?.leader;
            if (leader != null && members.Contains(leader)) return leader;
            return members.OrderByDescending(a => a.ageTracker.AgeBiologicalTicks).FirstOrDefault();
        }

        /// <summary>Somewhere to graze a little way off: open ground in its area (and its pen), with something it eats growing there.</summary>
        private static IntVec3 Pasture(Pawn lead, Map map)
        {
            var area = lead.playerSettings?.AreaRestrictionInPawnCurrentMap;
            bool grazer = lead.RaceProps.Eats(FoodTypeFlags.Plant);
            bool Fits(IntVec3 c, float near)
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.Roofed(map) || (area != null && !area[c])) return false;
                if (c.InHorDistOf(lead.Position, near)) return false;
                if (grazer)
                {
                    var plant = c.GetPlant(map);
                    if (plant == null || !plant.IngestibleNow || !lead.RaceProps.CanEverEat(plant) || map.zoneManager.ZoneAt(c) is Zone_Growing) return false;
                }
                return lead.CanReach(c, PathEndMode.OnCell, Danger.None);
            }
            foreach (float near in new[] { 10f, 5f })
                for (int i = 0; i < 4; i++)
                    if (CellFinder.TryFindRandomCellNear(lead.Position, map, 30, c => Fits(c, near), out var cell))
                        return cell;
            return IntVec3.Invalid;
        }

        /// <summary>
        /// In the morning a herd of one kind (two or more grown, the herd's own: herd animals) moves off together: its eldest
        /// walks to fresh grazing and the rest follow at its heels; there they graze as they please. One a map an hour.
        /// </summary>
        private static void HerdMoves(Map map)
        {
            var herd = AnimalFacts.Herd(map).Where(a => a.RaceProps.herdAnimal && !AnimalFacts.Young(a) && Idle(a)).ToList();
            foreach (var g in herd.GroupBy(a => a.def).Where(g => g.Count() >= AnimalDaysMath.HerdMin).InRandomOrder())
            {
                if (!Rand.Chance(Chance(AnimalDay.Herd))) continue;
                string key = "day:herd|" + map.uniqueID + "|" + g.Key.defName;
                if (!AnimalTempers.Ready(key, AnimalDaysMath.Days(AnimalDay.Herd) * 0.5f)) continue;
                var members = g.ToList();
                var lead = Eldest(map, members);
                var cell = lead == null ? IntVec3.Invalid : Pasture(lead, map);
                if (!cell.IsValid)
                {
                    AnimalTempers.Unwait(key);
                    continue;
                }
                var go = JobMaker.MakeJob(JobDefOf.Goto, cell);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                if (!Start(lead, go)) continue;
                int following = 0;
                foreach (var m in members.Where(m => m != lead).Take(AnimalDaysMath.HerdMax))
                    if (m.CanReach(lead, PathEndMode.Touch, Danger.Some) && Start(m, FollowJob(lead, 2.5f + following * 0.3f, Rand.Range(1500, 2400))))
                        following++;
                AnimalTempers.Noted(lead, "CC_AnimalDay_HerdLead", (following + 1).Named("COUNT"));
                Remark(lead, Tags(lead, "day:herd"), HerdStory.Keeper(map, lead), (following + 1).Named("COUNT"), AnimalFacts.KindLabel(lead).Named("KIND"));
                return;
            }
        }

        // ---------------- The rounds ----------------

        /// <summary>Who keeps the animals here: the herdmaster, else whoever is best with them and handles them.</summary>
        internal static Pawn Keeper(Map map)
        {
            var hm = AnimalFacts.HerdmasterAt(map);
            if (hm != null && !hm.Dead && hm.Spawned && hm.Map == map && !hm.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return hm;
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
        }

        /// <summary>
        /// The keeper, in their free time (the herdmaster a little oftener), goes round the colony's animals near them one after
        /// another, nearest next, five at most: a while beside each looking it over. Two animals or more.
        /// </summary>
        private static void Rounds(Map map)
        {
            var keeper = Keeper(map);
            if (keeper == null || !Together.Able(keeper) || !Together.FreeTime(keeper) || keeper.GetLord() != null) return;
            if (!Rand.Chance(Chance(AnimalDay.Rounds) * (AnimalFacts.IsHerdmaster(keeper) ? 1.5f : 1f))) return;
            var round = map.mapPawns.SpawnedColonyAnimals
                .Where(a => MoreHabits.Calm(a) && a.GetLord() == null && !(a.roping?.IsRoped ?? false) && a.Position.InHorDistOf(keeper.Position, 60f)
                            && keeper.CanReach(a, PathEndMode.Touch, Danger.None))
                .ToList();
            if (round.Count < 2 || !AnimalTempers.Ready("day:rounds|" + map.uniqueID, AnimalDaysMath.Days(AnimalDay.Rounds) * 0.5f)) return;
            var order = new List<Pawn>();
            var from = keeper.Position;
            while (order.Count < AnimalDaysMath.RoundsMax && round.Count > 0)
            {
                var near = from;
                var n = round.OrderBy(a => a.Position.DistanceToSquared(near)).First();
                order.Add(n);
                round.Remove(n);
                from = n.Position;
            }
            var job = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_KeeperRound, order[0]);
            foreach (var a in order.Skip(1)) job.AddQueuedTarget(TargetIndex.A, a);
            keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The keeper beside one of the round (JobDriver_KeeperRound): it holds still to be looked over; the first gets a word.</summary>
        internal static void Looked(Pawn keeper, Pawn a, bool first)
        {
            try
            {
                if (AnimalTempers.Free(a)) Start(a, AnimalTempers.Linger(AnimalNatureDefOf.Rimfolk_AnimalWatch, a.Position, keeper, AnimalDaysMath.LookTicks + 120, keeper));
                AnimalTempers.Noted(a, "CC_AnimalDay_Looked", keeper.Named("PAWN"));
                if (!first) return;
                string how = AnimalFacts.Illness(a) != null ? "sick" : AnimalFacts.Hurt(a) ? "hurt" : AnimalFacts.Pregnant(a) ? "pregnant" : null;
                Say(keeper, a, Tags(a, "day:rounds", how, AnimalFacts.IsHerdmaster(keeper) ? "herdmaster" : null));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal days: the rounds", e);
            }
        }

        // ---------------- The weather ----------------

        /// <summary>
        /// The weather turning, by day: rain or snow coming on, the pets out in it go in under a roof (three at most), and in the
        /// snow the young ones play in it. A first look after a load only notes the weather.
        /// </summary>
        private static void Weather(Map map, int hour)
        {
            var w = map.weatherManager?.curWeather;
            int id = map.uniqueID;
            string now = w?.defName;
            bool seen = weatherSeen.TryGetValue(id, out var was);
            weatherSeen[id] = now;
            if (!seen || was == now || w == null || hour < 6 || hour > 21) return;
            if (AnimalDaysMath.Shelters(w.rainRate, w.snowRate)) Shelter(map, w.snowRate >= 0.5f);
            if (w.snowRate >= 0.5f) SnowPlay(map);
        }

        private static void Shelter(Map map, bool snow)
        {
            int done = 0;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals.Where(x => AnimalFacts.Pet(x) && AnimalTempers.Has(x) && Idle(x) && !x.Position.Roofed(map)).InRandomOrder().ToList())
            {
                var person = AnimalFacts.PersonOf(a);
                var near = person != null && person.Spawned && person.Map == map ? person.Position : a.Position;
                if (!AnimalTempers.CellNear(a, near, 12, out var cell, c => c.Roofed(map) && map.areaManager.Home[c]) &&
                    !AnimalTempers.CellNear(a, a.Position, 20, out cell, c => c.Roofed(map) && map.areaManager.Home[c])) continue;
                var job = AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalShelter, cell, IntVec3.Invalid, AnimalDaysMath.ShelterTicks);
                job.locomotionUrgency = LocomotionUrgency.Jog;
                // The first one in is remarked on as it lies down under the roof.
                var tags = Tags(a, "day:shelter", snow ? "snow" : "rain");
                var pet = a;
                if (!StartThen(a, job, done == 0 ? () => Remark(pet, tags, person) : (Action)null)) continue;
                AnimalTempers.Noted(a, snow ? "CC_AnimalDay_ShelterSnow" : "CC_AnimalDay_Shelter");
                if (++done >= 3) return;
            }
        }

        private static void SnowPlay(Map map)
        {
            int done = 0;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals.Where(x => AnimalFacts.Young(x) && Idle(x) && !x.Position.Roofed(map)).InRandomOrder().ToList())
            {
                var job = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_AnimalPlay, a.Position);
                job.count = AnimalDaysMath.PlayTicks;
                job.locomotionUrgency = LocomotionUrgency.Jog;
                if (!Start(a, job)) continue;
                AnimalTempers.Noted(a, "CC_AnimalDay_SnowPlay");
                if (done == 0) Remark(a, Tags(a, "day:snow"), AnimalFacts.PersonOf(a));
                if (++done >= 2) return;
            }
        }

        /// <summary>A bitter night: each herd of one kind out in the open lies down close about its eldest. Once a night a map.</summary>
        private static void Huddle(Map map)
        {
            if (!AnimalDaysMath.Huddles(map.mapTemperature.OutdoorTemp)) return;
            var herd = AnimalFacts.Herd(map).Where(a => MoreHabits.Calm(a) && a.GetLord() == null && !(a.roping?.IsRoped ?? false) && !a.Position.Roofed(map) && a.CurrentBed() == null).ToList();
            int night = (Now + GenDate.TicksPerDay / 4) / GenDate.TicksPerDay;
            foreach (var g in herd.GroupBy(a => a.def).Where(g => g.Count() >= AnimalDaysMath.HerdMin))
            {
                if (!AnimalTempers.Ready("day:huddle|" + map.uniqueID + "|" + g.Key.defName + "|" + night, 1f)) continue;
                var members = g.OrderByDescending(a => a.ageTracker.AgeBiologicalTicks).ToList();
                var eldest = members[0];
                int moved = 0;
                foreach (var m in members.Skip(1).Take(AnimalDaysMath.HerdMax))
                {
                    if (m.Position.InHorDistOf(eldest.Position, 1.9f)) continue;
                    if (AnimalTempers.CellNear(m, eldest.Position, 1, out var c) && Start(m, AnimalTempers.Nap(c, 6000))) moved++;
                }
                if (moved == 0) continue;
                if (eldest.Awake() && AnimalTempers.Free(eldest)) Start(eldest, AnimalTempers.Nap(eldest.Position, 6000));
                AnimalTempers.Noted(eldest, "CC_AnimalDay_Huddle");
                Remark(eldest, Tags(eldest, "day:huddle"), HerdStory.Keeper(map, eldest), AnimalFacts.KindLabel(eldest).Named("KIND"));
            }
        }

        // ---------------- Raiders ----------------

        /// <summary>
        /// Raiders on a home map (AnimalTempers.FightLook, every 250 ticks of a fight): as they come, two pets at most (never a
        /// timid one) turn to face them and give their own call, the alarm, and someone says where. Once a fight.
        /// </summary>
        public static void Alarm(Map map, List<Pawn> hostiles)
        {
            if (!On || Rate <= 0f || map == null || hostiles == null || hostiles.Count == 0) return;
            Fresh();
            try
            {
                int id = map.uniqueID;
                if (alarmAt.TryGetValue(id, out int at) && at <= Now && Now - at < GenDate.TicksPerDay / 2) return;
                alarmAt[id] = Now;
                int done = 0;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.Where(x => AnimalFacts.Pet(x) && AnimalTempers.Has(x) && MoreHabits.Calm(x) && x.Awake() && x.GetLord() == null
                                                                                && AnimalTempers.Of(x) != Temper.Timid).InRandomOrder().ToList())
                {
                    var foe = hostiles.Where(h => h.Position.InHorDistOf(a.Position, 90f)).OrderBy(h => h.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                    if (foe == null) continue;
                    a.rotationTracker?.FaceTarget(foe);
                    a.caller?.DoCall(true);
                    AnimalTempers.Noted(a, "CC_AnimalDay_Alarm");
                    if (done == 0)
                    {
                        string dir = AnimalNatureMath.Compass(foe.Position.x - a.Position.x, foe.Position.z - a.Position.z);
                        Remark(a, Tags(a, "day:alarm"), AnimalFacts.PersonOf(a), ("CC_Compass_" + dir).Translate().Named("PLACE"));
                    }
                    if (++done >= 2) return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal days: the alarm", e);
            }
        }

        // ---------------- A god near ----------------

        /// <summary>
        /// A god's presence over the land (Gods/Presence: its condition): the pets won't settle and go to keep close to their
        /// people a while, one calling out; someone says so (the god unnamed: they only know the animals are uneasy). Once a
        /// presence, three at most.
        /// </summary>
        private static void Uneasy(Map map, List<Pawn> animals)
        {
            var presence = map.gameConditionManager?.ActiveConditions?.FirstOrDefault(c => c is GameCondition_GodPresence);
            if (presence == null || !presencesSeen.Add(presence.def.defName + "|" + presence.startTick + "|" + map.uniqueID)) return;
            if (presencesSeen.Count > 60) presencesSeen.Clear();
            int done = 0;
            foreach (var a in animals.Where(x => AnimalFacts.Pet(x) && Idle(x, true)).InRandomOrder().ToList())
            {
                var p = AnimalFacts.PersonOf(a);
                if (p == null || !p.Spawned || p.Map != map || !Together.Able(p) || !a.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;
                // The first cries out as it goes, and is remarked on once it is at their side.
                var tags = Tags(a, "day:uneasy");
                var pet = a;
                var job = RunToJob(AnimalDaysDefOf.Rimfolk_AnimalKeepClose, p, LocomotionUrgency.Jog, AnimalDaysMath.SideTicks);
                if (!StartThen(a, job, done == 0 ? () => Remark(pet, tags, p) : (Action)null)) continue;
                if (done == 0) a.caller?.DoCall();
                AnimalTempers.Noted(a, "CC_AnimalDay_Uneasy", p.Named("PAWN"));
                if (++done >= 3) return;
            }
        }

        // ---------------- A death ----------------

        /// <summary>
        /// Whom each animal is with, kept from hour to hour: one whose person was alive an hour ago and is dead now will go to
        /// lie by them (<see cref="Notify_PersonDied"/>), seen before another takes their place.
        /// </summary>
        private static void Deaths(List<Pawn> animals)
        {
            foreach (var a in animals)
            {
                int id = a.thingIDNumber;
                if (personSeen.TryGetValue(id, out var before) && before != null && before.Dead) Notify_PersonDied(a, before);
                var now = AnimalFacts.PersonOf(a);
                if (now != null) personSeen[id] = now;
                else if (before != null && before.Dead) personSeen.Remove(id);
            }
            if (personSeen.Count > 500) personSeen.RemoveAll(kv => kv.Value == null || kv.Value.Dead);
        }

        /// <summary>Its person has died (<see cref="Deaths"/>, or a dev action): it will go to lie by them.</summary>
        public static void Notify_PersonDied(Pawn a, Pawn dead)
        {
            if (!On || a == null || dead == null || dead.Corpse == null) return;
            Fresh();
            float hours = (Now - dead.Corpse.timeOfDeath) / (float)GenDate.TicksPerHour;
            if (hours < 0f || hours > AnimalDaysMath.MournWithinHours || mourners.ContainsKey(a.thingIDNumber)) return;
            if (!AnimalTempers.Ready("day:mourn|" + a.thingIDNumber + "|" + dead.thingIDNumber, 60f)) return;
            mourners[a.thingIDNumber] = (dead, Now);
        }

        /// <summary>
        /// Once the danger is past: each animal whose person died goes to lie by the body (or the grave it is already in), cries
        /// out, and stays a while; whoever sees it says so. Half a day at most after.
        /// </summary>
        private static void Mourn(Map map)
        {
            foreach (var kv in mourners.ToList())
            {
                var (dead, since) = kv.Value;
                var a = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(x => x.thingIDNumber == kv.Key);
                if (a == null) continue;
                var at = dead?.Corpse?.SpawnedParentOrMe;
                if (Now - since > GenDate.TicksPerDay / 2 || at == null || !at.Spawned || at.Map != map)
                {
                    mourners.Remove(kv.Key);
                    continue;
                }
                if (Rate <= 0f || !MoreHabits.Calm(a) || a.GetLord() != null || (a.roping?.IsRoped ?? false) || !a.Awake()) continue;
                if (!AnimalTempers.CellNear(a, at.Position, 1, out var cell)) continue;
                var job = AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalMourn, cell, at, AnimalDaysMath.MournTicks, dead);
                job.locomotionUrgency = LocomotionUrgency.Jog;
                // It cries out as it lies down by them (the job's own call, Rimfolk_AnimalMourn), and whoever sees it says so then.
                var tags = Tags(a, "day:mourn");
                var pet = a;
                if (!StartThen(a, job, () => Remark(pet, tags, null, dead.Named("OTHER")))) continue;
                mourners.Remove(kv.Key);
                AnimalTempers.Noted(a, "CC_AnimalDay_Mourn", dead.Named("OTHER"));
            }
        }

        // ---------------- The card ----------------

        /// <summary>Whom it has taken to, for its card, when it has no bond and no master ("fond of Guido"); null otherwise.</summary>
        public static string InspectPart(Pawn a)
        {
            if (!On) return null;
            var p = AnimalFacts.FondOf(a);
            return p == null ? null : "CC_AnimalDay_Inspect_TakesTo".Translate(p.Named("PAWN")).Resolve();
        }

        // ---------------- Dev actions ----------------

        /// <summary>The dev action: one of the moments now on this map, past the pace and the waits (a few need their hour or their scene).</summary>
        public static string DebugNow(Map map, AnimalDay d)
        {
            if (map == null) return "no map.";
            Fresh();
            var animals = map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList();
            switch (d)
            {
                case AnimalDay.Greet:
                {
                    foreach (var a in animals.InRandomOrder())
                    {
                        var p = AnimalFacts.PersonOf(a);
                        var tags = Tags(a, "day:greet");
                        if (p != null && p.Spawned && p.Map == map && Greet(a, p, () => Say(p, a, tags)))
                            return a.LabelShort + " runs to " + p.LabelShort + " (the hello comes as it reaches them).";
                    }
                    return "no animal here whose person is up on this map and reachable.";
                }
                case AnimalDay.FootOfBed:
                {
                    foreach (var a in animals.Where(x => AnimalDaysMath.MaySleepAtFoot(x.RaceProps.baseBodySize, !AnimalFacts.Pet(x))).InRandomOrder())
                    {
                        var p = AnimalFacts.PersonOf(a);
                        if (p != null && p.Spawned && p.Map == map && !p.Awake() && SleepAtFoot(a, p))
                        {
                            footNight[a.thingIDNumber] = (p, Now);
                            return a.LabelShort + " goes to sleep at the foot of " + p.LabelShort + "'s bed (the word comes when they wake, five to ten).";
                        }
                    }
                    return "no pet (a dog's size or less) here whose person is asleep in their own bed, with room at its foot.";
                }
                case AnimalDay.Beg:
                {
                    var eaters = map.mapPawns.FreeColonistsSpawned.Where(Eating).ToList();
                    foreach (var a in animals.Where(x => AnimalDaysMath.MayBeg(x.RaceProps.baseBodySize, !AnimalFacts.Pet(x))).InRandomOrder())
                    {
                        var eater = eaters.OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault(p => a.CanReach(p, PathEndMode.Touch, Danger.Some));
                        var tags = Tags(a, "day:beg");
                        if (eater != null && Beg(a, eater, AnimalDaysMath.BegTicks, () => Say(eater, a, tags)))
                            return a.LabelShort + " goes to sit at " + eater.LabelShort + "'s elbow (the word comes as it sits down).";
                    }
                    return eaters.Count == 0 ? "nobody is eating right now (sat with their food)." : "no pet (a dog's size or less) that can get to them.";
                }
                case AnimalDay.ChildPlay:
                {
                    var c = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.DevelopmentalStage.Child() && Together.Able(p));
                    if (c == null) return "no child up on this map.";
                    var a = map.mapPawns.SpawnedColonyAnimals.Where(x => MoreHabits.Calm(x) && AnimalDaysMath.PlaysWithChild(x.RaceProps.baseBodySize, x.RaceProps.predator))
                        .OrderBy(x => x.Position.DistanceToSquared(c.Position)).FirstOrDefault(x => c.CanReach(x, PathEndMode.Touch, Danger.None));
                    if (a == null || !Play(c, a, AnimalDaysMath.PlayTicks)) return "no animal for " + c.LabelShort + " to play with (up to a horse's size, no great hunter).";
                    Say(c, a, Tags(a, "day:play"));
                    return c.LabelShort + " plays chase with " + a.LabelShort + ".";
                }
                case AnimalDay.Herd:
                    foreach (var g in AnimalFacts.Herd(map).Where(a => a.RaceProps.herdAnimal && MoreHabits.Calm(a)).GroupBy(a => a.def))
                        AnimalTempers.Unwait("day:herd|" + map.uniqueID + "|" + g.Key.defName);
                    for (int i = 0; i < 20; i++)
                    {
                        HerdMovesNow(map, out string said);
                        if (said != null) return said;
                    }
                    return "no herd of two or more grown of one kind, idle, with fresh grazing in reach.";
                case AnimalDay.Rounds:
                {
                    var keeper = Keeper(map);
                    if (keeper == null) return "nobody here handles animals.";
                    AnimalTempers.Unwait("day:rounds|" + map.uniqueID);
                    var round = map.mapPawns.SpawnedColonyAnimals.Where(a => MoreHabits.Calm(a) && keeper.CanReach(a, PathEndMode.Touch, Danger.None))
                        .OrderBy(a => a.Position.DistanceToSquared(keeper.Position)).Take(AnimalDaysMath.RoundsMax).ToList();
                    if (round.Count == 0) return "no animal " + keeper.LabelShort + " can reach.";
                    var job = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_KeeperRound, round[0]);
                    foreach (var a in round.Skip(1)) job.AddQueuedTarget(TargetIndex.A, a);
                    keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    return keeper.LabelShort + " goes round " + round.Count + " animals.";
                }
                case AnimalDay.Shelter:
                    Shelter(map, false);
                    return "the pets out in the open go in under a roof (if any can reach one in the home area).";
                case AnimalDay.SnowPlay:
                    SnowPlay(map);
                    return "the young ones out in the open play (as in the first snow).";
                case AnimalDay.Huddle:
                {
                    var herd = AnimalFacts.Herd(map).Where(a => MoreHabits.Calm(a) && !a.Position.Roofed(map)).ToList();
                    int n = 0;
                    foreach (var g in herd.GroupBy(a => a.def).Where(g => g.Count() >= 2))
                    {
                        var eldest = g.OrderByDescending(a => a.ageTracker.AgeBiologicalTicks).First();
                        foreach (var m in g.Where(m => m != eldest))
                            if (AnimalTempers.CellNear(m, eldest.Position, 1, out var c) && Start(m, AnimalTempers.Nap(c, 6000)))
                                n++;
                    }
                    return n == 0 ? "no herd of two or more of one kind out in the open." : n + " lay down close about their eldest.";
                }
                case AnimalDay.Alarm:
                {
                    alarmAt.Remove(map.uniqueID);
                    var foes = map.mapPawns.AllPawnsSpawned.Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed).ToList();
                    if (foes.Count == 0) return "no enemy on the map to raise the alarm at.";
                    Alarm(map, foes);
                    return "the pets raise the alarm (if any is calm and awake, not timid).";
                }
                case AnimalDay.Uneasy:
                {
                    int n = 0;
                    foreach (var a in animals.Where(x => AnimalFacts.Pet(x) && MoreHabits.Calm(x)))
                    {
                        var p = AnimalFacts.PersonOf(a);
                        if (p == null || !p.Spawned || p.Map != map || !a.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;
                        var tags = Tags(a, "day:uneasy");
                        var pet = a;
                        var job = RunToJob(AnimalDaysDefOf.Rimfolk_AnimalKeepClose, p, LocomotionUrgency.Jog, AnimalDaysMath.SideTicks);
                        if (!StartThen(a, job, n == 0 ? () => Remark(pet, tags, p) : (Action)null)) continue;
                        if (n == 0) a.caller?.DoCall();
                        n++;
                    }
                    return n == 0 ? "no pet here with its person up on this map." : n + " pets keep close to their people.";
                }
                case AnimalDay.Mourn:
                {
                    foreach (var a in animals.InRandomOrder())
                    {
                        var p = AnimalFacts.PersonOf(a);
                        if (p == null || !p.Spawned || p.Map != map) continue;
                        if (!AnimalTempers.CellNear(a, p.Position, 1, out var cell)) continue;
                        if (!Start(a, AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalMourn, cell, p, 1500, p))) continue;
                        return a.LabelShort + " lies down by " + p.LabelShort + " and cries, as it would by them dead (shown on the living for the look).";
                    }
                    return "no animal here with its person on this map.";
                }
            }
            return "nothing for that.";
        }

        /// <summary>The herd's morning now (for the dev action): one herd moved off, said with what was said; null when none could.</summary>
        private static void HerdMovesNow(Map map, out string said)
        {
            said = null;
            var herd = AnimalFacts.Herd(map).Where(a => a.RaceProps.herdAnimal && !AnimalFacts.Young(a) && Idle(a)).ToList();
            foreach (var g in herd.GroupBy(a => a.def).Where(g => g.Count() >= AnimalDaysMath.HerdMin))
            {
                var members = g.ToList();
                var lead = Eldest(map, members);
                var cell = lead == null ? IntVec3.Invalid : Pasture(lead, map);
                if (!cell.IsValid) continue;
                var go = JobMaker.MakeJob(JobDefOf.Goto, cell);
                if (!Start(lead, go)) continue;
                int following = 0;
                foreach (var m in members.Where(m => m != lead).Take(AnimalDaysMath.HerdMax))
                    if (m.CanReach(lead, PathEndMode.Touch, Danger.Some) && Start(m, FollowJob(lead, 2.5f + following * 0.3f, Rand.Range(1500, 2400))))
                        following++;
                AnimalTempers.Noted(lead, "CC_AnimalDay_HerdLead", (following + 1).Named("COUNT"));
                Remark(lead, Tags(lead, "day:herd"), HerdStory.Keeper(map, lead), (following + 1).Named("COUNT"), AnimalFacts.KindLabel(lead).Named("KIND"));
                said = lead.LabelShort + " leads " + following + " of its kind off to graze at " + cell + ".";
                return;
            }
        }

        /// <summary>The dev action: whom each of the colony's animals here is with, and why (bond, master, or taken to).</summary>
        public static string DebugPeople(Map map)
        {
            if (map == null) return "no map.";
            var lines = new List<string>();
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                var bonded = PetTracker.PersonOf(a);
                var fondOf = AnimalFacts.FondOf(a);
                string why = bonded != null ? (AnimalFacts.Bonded(a, bonded) ? "bonded to " : "master: ") + bonded.LabelShort
                    : fondOf != null ? "fond of " + fondOf.LabelShort : AnimalTempers.Has(a) ? "nobody" : "unnamed (no temperament, no person)";
                lines.Add(a.LabelShort + " (" + AnimalFacts.KindLabel(a) + (AnimalFacts.Pet(a) ? ", a pet" : ", the herd's") + "): " + why);
            }
            return lines.Count == 0 ? "no animals of the colony here." : string.Join("\n", lines);
        }
    }
}
