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
    /// Every named or bonded animal of the colony has a temperament (AnimalNatureMath: brave, timid, greedy, gentle,
    /// proud, mischievous, loyal, lazy, curious), given once from its kind's lean and its own id and kept in the
    /// <see cref="AnimalBook"/>. It shows on the animal's card and in small moments on the map, each a real thing the
    /// animal does there, never only words, at most one an hour on a map (the setting "Animals' temperaments" on What's
    /// on, their pace on How much happens):
    /// <list type="bullet">
    /// <item>brave: in a fight it goes to stand between its person and the nearest attacker and holds there, fighting
    /// whoever comes at it; out of a fight, between its person and a wild predator that comes near them;</item>
    /// <item>timid: in a fight it bolts from the shooting near it; else it shies away from a stranger who comes close;</item>
    /// <item>greedy: it sneaks a meal or a few mouthfuls from the stores or off a table;</item>
    /// <item>gentle: it nuzzles someone sick, hurt or low (the game's own nuzzle), or lies down by a patient asleep in bed;</item>
    /// <item>proud: it shoulders in when its person works with another animal, or shows itself off to visitors;</item>
    /// <item>mischievous: a grazer gets into the crops and eats one; anything else knocks a few things off a shelf;</item>
    /// <item>loyal: while its person is away it waits at the door (or by their bed), and runs to them when they come home;</item>
    /// <item>lazy: it takes a nap by the fire when it's cold, in the shade when it's hot, else in the sun;</item>
    /// <item>curious: it follows a visitor about, or goes to watch someone building or at a bench.</item>
    /// </list>
    /// Nothing here sends a letter or a message: whoever sees it may say something, its person may write it down, and
    /// its card says what it did last. What it did last and the waits between moments are not saved.
    /// </summary>
    public static class AnimalTempers
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Temperaments show (on the card and on the map): pets as companions and the temperaments on.</summary>
        public static bool On => AnimalLife.On && S.animalTempers;

        /// <summary>The moments' pace, 0 to 2 (0: none, the card still shows the temperament).</summary>
        public static float Rate => S == null ? 0f : Mathf.Clamp(S.animalMoments, 0f, 2f);

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.85f);

        // ---------------- Who has one ----------------

        /// <summary>A name the colony gave it (not a number).</summary>
        public static bool Named(Pawn a) => a?.Name != null && !a.Name.Numerical;

        /// <summary>Bonded to a person, alive or dead.</summary>
        public static bool BondedToSomeone(Pawn a)
        {
            if (a?.relations == null) return false;
            foreach (var r in a.relations.DirectRelations)
                if (r.def == PawnRelationDefOf.Bond && r.otherPawn?.RaceProps?.Humanlike == true) return true;
            return false;
        }

        /// <summary>One of the colony's animals with a temperament: named, or bonded to someone.</summary>
        public static bool Has(Pawn a) => AnimalFacts.Ours(a) && (Named(a) || BondedToSomeone(a));

        /// <summary>
        /// A companion, among those with a temperament: bonded to someone, or not livestock (a pet, a Pokémon, a guard dog).
        /// Friendships and feuds are theirs; the herd's own stories are the herd's (a herd named by the herdmaster would
        /// otherwise pair off in every barn).
        /// </summary>
        public static bool Companion(Pawn a) => Has(a) && (BondedToSomeone(a) || !AnimalFacts.Livestock(a));

        /// <summary>What its kind is, for its temperament and its voice.</summary>
        public static NatureFacts Facts(Pawn a)
        {
            var r = a.RaceProps;
            var train = r.trainability;
            return new NatureFacts
            {
                family = AnimalNatureMath.Family(a.def?.defName, a.def?.label, AnimalFacts.Body(a) == HerdStoryMath.HerdBody.Bird, r.Insect),
                predator = r.predator,
                pokemon = PokeWorldLink.IsPokemon(a),
                bodySize = r.baseBodySize,
                trainability = train == null || train == TrainabilityDefOf.None ? 0 : train == TrainabilityDefOf.Advanced ? 2 : 1,
                types = PokeWorldLink.IsPokemon(a) ? PokeWorldLink.TypesOf(a.def) : null
            };
        }

        /// <summary>Its temperament: the one kept, else given now from its kind and its id (and kept). Null for an animal without one.</summary>
        public static Temper? Of(Pawn a)
        {
            if (!Has(a)) return null;
            var book = AnimalBook.Instance;
            if (book == null) return null;
            if (book.TryTemper(a.thingIDNumber, out var t)) return t;
            try
            {
                t = AnimalNatureMath.Pick(a.thingIDNumber, AnimalNatureMath.Weights(Facts(a)));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal temperament", e);
                t = Temper.Curious;
            }
            book.SetTemper(a.thingIDNumber, t);
            return t;
        }

        public static string Label(Temper t) => ("CC_Temper_" + t).Translate().Resolve();

        // ---------------- What it did last, and who it was with (this session) ----------------

        private sealed class Did
        {
            public string text;
            public int tick;
        }

        private static readonly Dictionary<int, Did> lastDid = new Dictionary<int, Did>();
        /// <summary>The person each animal was last seen with (its bonded one, else its master), for when it leaves the colony.</summary>
        private static readonly Dictionary<int, Pawn> lastPerson = new Dictionary<int, Pawn>();
        /// <summary>When each loyal animal's person was last seen away, for the homecoming.</summary>
        private static readonly Dictionary<int, int> awayAt = new Dictionary<int, int>();
        private static readonly Dictionary<string, int> waits = new Dictionary<string, int>();
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            lastDid.Clear();
            lastPerson.Clear();
            awayAt.Clear();
            waits.Clear();
        }

        /// <summary>Its card's "lately" line: what it did, by its key and the names in it.</summary>
        public static void Noted(Pawn a, string key, params NamedArgument[] args)
        {
            if (a == null) return;
            Fresh();
            try
            {
                var all = new List<NamedArgument> { a.Named("PET") };
                all.AddRange(args);
                lastDid[a.thingIDNumber] = new Did { text = key.Translate(all.ToArray()).Resolve(), tick = Now };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal temperament: noted", e);
            }
            if (lastDid.Count > 400) lastDid.RemoveAll(kv => Now - kv.Value.tick > GenDate.TicksPerDay * 3);
        }

        /// <summary>What it did lately (within two days), for its card, or null.</summary>
        public static string Lately(Pawn a)
        {
            Fresh();
            return a != null && lastDid.TryGetValue(a.thingIDNumber, out var d) && Now - d.tick < GenDate.TicksPerDay * 2 ? d.text : null;
        }

        /// <summary>The person it was last seen with, kept from the hour before (for an animal that has just left the colony).</summary>
        public static Pawn LastPerson(Pawn a)
        {
            Fresh();
            return a != null && lastPerson.TryGetValue(a.thingIDNumber, out var p) ? p : null;
        }

        /// <summary>True (and the wait starts) when this key hasn't come up within <paramref name="days"/>.</summary>
        internal static bool Ready(string key, float days)
        {
            Fresh();
            int now = Now;
            if (waits.TryGetValue(key, out int t) && t <= now && now - t < days * GenDate.TicksPerDay) return false;
            waits[key] = now;
            if (waits.Count > 600) waits.RemoveAll(kv => kv.Value > now || now - kv.Value > GenDate.TicksPerDay * 30);
            return true;
        }

        /// <summary>The wait for this key is over (a dev action's moment came up but nothing happened).</summary>
        internal static void Unwait(string key) => waits.Remove(key);

        // ---------------- The hour ----------------

        /// <summary>
        /// Once an hour (AnimalLife.Hourly): who each animal is with, the loyal ones' persons away and home, and on each home
        /// map out of danger at most one temperament moment, the animals in view likelier.
        /// </summary>
        public static void Hourly()
        {
            if (!On) return;
            Fresh();
            VisualTouches.InView(out var viewMap, out var view);
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                var animals = map.mapPawns.SpawnedColonyAnimals.Where(Has).ToList();
                foreach (var a in animals)
                {
                    var person = PetTracker.PersonOf(a);
                    if (person != null) lastPerson[a.thingIDNumber] = person;
                }
                if (lastPerson.Count > 500) lastPerson.RemoveAll(kv => kv.Value == null || kv.Value.Dead);
                foreach (var a in animals)
                    if (Of(a) == Temper.Loyal) Homecoming(a, map);
                if (Rate <= 0f || map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                int done = 0;
                foreach (var a in animals.InRandomOrder())
                {
                    if (done >= AnimalNatureMath.MomentsAnHour) break;
                    var t = Of(a);
                    if (t == null || !Free(a)) continue;
                    bool seen = map == viewMap && view.Contains(a.Position);
                    float chance = AnimalNatureMath.HourChance(AnimalNatureMath.MomentDays(t.Value), Rate) * (seen ? 1.5f : 1f);
                    if (!Rand.Chance(chance)) continue;
                    string key = "temper|" + a.thingIDNumber;
                    if (!Ready(key, AnimalNatureMath.MomentDays(t.Value) * 0.4f)) continue;
                    if (TryMoment(a, t.Value, out _)) done++;
                    else Unwait(key);
                }
            }
        }

        /// <summary>Up and about with nothing to do: calm, awake, wandering or standing, not roped, ridden, at work or in a group.</summary>
        public static bool Free(Pawn a)
        {
            if (!MoreHabits.Calm(a) || !a.Awake() || a.GetLord() != null || a.roping != null && a.roping.IsRoped) return false;
            var j = a.CurJobDef;
            return j == null || j == JobDefOf.Wait_Wander || j == JobDefOf.GotoWander || j == JobDefOf.Wait || j == JobDefOf.Wait_MaintainPosture;
        }

        /// <summary>Its temperament's moment now, if what it needs is there; why not, otherwise (for the dev action).</summary>
        public static bool TryMoment(Pawn a, Temper t, out string why)
        {
            why = null;
            try
            {
                var map = a.Map;
                var person = PetTracker.PersonOf(a);
                switch (t)
                {
                    case Temper.Brave: return BravePredator(a, person, map, out why);
                    case Temper.Timid: return TimidStranger(a, map, out why);
                    case Temper.Greedy: return Greedy(a, map, out why);
                    case Temper.Gentle: return Gentle(a, map, out why);
                    case Temper.Proud: return Proud(a, person, map, out why);
                    case Temper.Mischievous: return Mischief(a, map, out why);
                    case Temper.Loyal: return LoyalWait(a, map, out why);
                    case Temper.Lazy: return Lazy(a, person, map, out why);
                    case Temper.Curious: return Curious(a, map, out why);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal temperament: " + t, e);
                why = "it failed: " + e.Message;
            }
            return false;
        }

        // ---------------- Jobs ----------------

        private static bool Start(Pawn a, Job job)
        {
            if (a?.jobs == null || job == null) return false;
            a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return a.CurJob == job;
        }

        /// <summary>Stays a while at a cell, facing something (a door, a grave, someone): standing, lying, or holding its ground.</summary>
        internal static Job Linger(JobDef def, IntVec3 cell, LocalTargetInfo face, int ticks, Pawn about = null)
        {
            var job = JobMaker.MakeJob(def, cell, face);
            job.count = ticks;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            if (about != null) job.targetC = about;
            return job;
        }

        private static Job FollowJob(Pawn whom, float radius, int ticks)
        {
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, whom);
            job.followRadius = radius;
            job.expiryInterval = ticks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        /// <summary>A nap at a cell: the game's own lying down, asleep for a while.</summary>
        internal static Job Nap(IntVec3 cell, int ticks)
        {
            var job = JobMaker.MakeJob(JobDefOf.LayDown, cell);
            job.forceSleep = true;
            job.expiryInterval = ticks;
            job.checkOverrideOnExpire = false;
            return job;
        }

        /// <summary>A standable cell near <paramref name="at"/> it can walk to, with nobody else bound for it.</summary>
        internal static bool CellNear(Pawn a, IntVec3 at, int radius, out IntVec3 cell, Predicate<IntVec3> also = null)
        {
            var map = a.Map;
            return CellFinder.TryFindRandomCellNear(at, map, radius, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null
                                                                       && (also == null || also(c)) && a.CanReach(c, PathEndMode.OnCell, Danger.Some), out cell);
        }

        /// <summary>The nuzzle, as the game's animals nuzzle a colonist (who feels the game's own little lift); a walk to their side when it can't.</summary>
        internal static bool Nuzzle(Pawn a, Pawn p)
        {
            if (a == null || p == null || !p.Spawned || p.Map != a.Map || !p.Awake() || p.InMentalState) return false;
            if (!a.CanReach(p, PathEndMode.Touch, Danger.Some)) return false;
            if (a.interactions != null) return Start(a, JobMaker.MakeJob(JobDefOf.Nuzzle, p));
            return Start(a, FollowJob(p, 1.6f, 900));
        }

        /// <summary>Someone who can see it says a word about it: a colonist in view, near and free to talk (the shared once-a-day rate).</summary>
        internal static bool Remark(Pawn a, string pool, string[] tags, Pawn prefer = null, params NamedArgument[] args)
        {
            if (!VisualTouches.On || !Speech.On || a == null || !a.Spawned) return false;
            var vt = VisualTouches.Instance;
            if (vt == null || !VisualTouches.InView(out var map, out var view) || map != a.Map || !view.Contains(a.Position)) return false;
            int now = Now;
            var who = prefer != null && vt.Free(prefer, map, view, now) && Together.Hears(prefer, a, 14f) ? prefer
                : map.mapPawns.FreeColonistsSpawned.Where(p => vt.Free(p, map, view, now) && Together.Hears(p, a, 14f)).RandomElementWithFallback();
            if (who == null) return false;
            var all = new List<NamedArgument> { a.Named("PET") };
            all.AddRange(args);
            string line = Interplay.Line(pool, who, tags, all.ToArray());
            // apart: said on seeing it, to nobody in particular.
            if (line.NullOrEmpty() || !Speech.Say(who, line, null, SpeechColor)) return false;
            vt.Spoke(who, now + GenDate.TicksPerDay);
            return true;
        }

        private static string[] Tags(Pawn a, Temper t, params string[] more)
        {
            var tags = new List<string> { "temper:" + AnimalNatureMath.Key(t), HerdStoryMath.BodyTag(AnimalFacts.Body(a)) };
            if (PokeWorldLink.IsPokemon(a)) tags.Add("pokemon");
            if (AnimalFacts.Young(a)) tags.Add("young");
            if (AnimalFacts.Old(a)) tags.Add("oldanimal");
            tags.AddRange(more.Where(m => !m.NullOrEmpty()));
            return tags.ToArray();
        }

        // ---------------- Brave ----------------

        /// <summary>A wild predator near its person: it goes to stand between them, facing it.</summary>
        private static bool BravePredator(Pawn a, Pawn person, Map map, out string why)
        {
            why = null;
            if (person == null || !person.Spawned || person.Map != map)
            {
                why = "its person isn't here.";
                return false;
            }
            var pred = map.mapPawns.AllPawnsSpawned.Where(x => AnimalFacts.WildPredator(x, map) && x.Position.InHorDistOf(person.Position, 25f))
                .OrderBy(x => x.Position.DistanceToSquared(person.Position)).FirstOrDefault();
            if (pred == null)
            {
                why = "no wild predator near its person.";
                return false;
            }
            if (!Stand(a, person, pred))
            {
                why = "no ground to stand on between them.";
                return false;
            }
            Noted(a, "CC_TemperDid_BravePredator", person.Named("PAWN"), pred.Named("OTHER"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Brave, "predator"), person);
            return true;
        }

        /// <summary>Goes to stand two or three cells from its person toward the danger, and holds there (fighting what comes at it).</summary>
        internal static bool Stand(Pawn a, Pawn person, Thing danger)
        {
            var from = person.Position.ToVector3Shifted();
            var dir = danger.Position.ToVector3Shifted() - from;
            if (dir.sqrMagnitude < 0.01f) return false;
            dir.Normalize();
            var at = (from + dir * 2.5f).ToIntVec3();
            if (!CellNear(a, at, 2, out var cell)) return false;
            return Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalStand, cell, danger, AnimalNatureMath.StandTicks, person));
        }

        // ---------------- Timid ----------------

        private static bool Stranger(Pawn p) =>
            p?.RaceProps != null && p.RaceProps.Humanlike && p.Spawned && !p.Dead && !p.Downed && p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony
            && !p.HostileTo(Faction.OfPlayer);

        /// <summary>A stranger near it: it shies away.</summary>
        private static bool TimidStranger(Pawn a, Map map, out string why)
        {
            why = null;
            var near = map.mapPawns.AllPawnsSpawned.Where(p => Stranger(p) && p.Position.InHorDistOf(a.Position, 8f))
                .OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (near == null)
            {
                why = "no stranger close to it.";
                return false;
            }
            var flee = FleeUtility.FleeJob(a, near, 12);
            if (flee == null || !Start(a, flee))
            {
                why = "nowhere to shy away to.";
                return false;
            }
            Noted(a, "CC_TemperDid_TimidStranger", near.Named("OTHER"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Timid, "stranger"));
            return true;
        }

        // ---------------- Greedy ----------------

        private static bool OnTable(Thing t)
        {
            var e = t.Position.GetEdifice(t.Map);
            return e?.def?.surfaceType == SurfaceType.Eat;
        }

        /// <summary>Something it would eat, put away or set on a table, that nobody has claimed.</summary>
        private static bool Snack(Pawn a, Thing t)
        {
            if (t == null || t is Corpse || t is Pawn || t.def.category != ThingCategory.Item || !t.def.IsNutritionGivingIngestible || t.def.IsDrug) return false;
            if (t.def.ingestible == null || t.def.ingestible.preferability == FoodPreferability.NeverForNutrition) return false;
            if (t.IsForbidden(Faction.OfPlayer) || !(t.IsInAnyStorage() || OnTable(t))) return false;
            if (t.Map.reservationManager.IsReservedByAnyoneOf(t, Faction.OfPlayer)) return false;
            if (!a.RaceProps.CanEverEat(t) || !FoodUtility.WillEat(a, t, null, true, false)) return false;
            return a.CanReach(t, PathEndMode.ClosestTouch, Danger.Some);
        }

        private static bool Meal(Thing t) => t.def.ingestible?.IsMeal ?? false;

        /// <summary>A meal or a few mouthfuls from the stores, or off a table: it goes there and eats them.</summary>
        private static bool Greedy(Pawn a, Map map, out string why)
        {
            why = null;
            var food = GenRadial.RadialDistinctThingsAround(a.Position, map, 30f, true).Where(t => Snack(a, t)).Take(40).ToList();
            var pick = food.OrderByDescending(t => (OnTable(t) ? 2 : 0) + (Meal(t) ? 1 : 0)).ThenBy(t => t.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (pick == null)
            {
                why = "nothing it would eat in the stores or on a table within reach.";
                return false;
            }
            int n = Meal(pick) ? 1 : Mathf.Min(pick.stackCount, Mathf.Max(1, Mathf.Min(Rand.RangeInclusive(3, 6),
                FoodUtility.WillIngestStackCountOf(a, pick.def, pick.GetStatValue(StatDefOf.Nutrition)))));
            var eat = JobMaker.MakeJob(JobDefOf.Ingest, pick);
            eat.count = n;
            if (!Start(a, eat))
            {
                why = "it couldn't start eating.";
                return false;
            }
            // Its card says how much ("a simple meal", "4 berries"); what is said names the food.
            string what = n > 1 ? n + " " + pick.def.label : Find.ActiveLanguageWorker.WithIndefiniteArticle(pick.def.label);
            Noted(a, OnTable(pick) ? "CC_TemperDid_GreedyTable" : "CC_TemperDid_GreedyStores", what.Named("THING"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Greedy, OnTable(pick) ? "table" : "stores"), null, pick.def.label.Named("THING"));
            return true;
        }

        // ---------------- Gentle ----------------

        private static bool Troubled(Pawn p)
        {
            if (p?.health == null) return false;
            if (p.InBed() && (p.CurrentBed()?.Medical ?? false)) return true;
            if (p.health.summaryHealth.SummaryHealthPercent < 0.75f) return true;
            return (p.needs?.mood?.CurLevelPercentage ?? 1f) < 0.35f;
        }

        /// <summary>Someone sick, hurt or low: it nuzzles them; one asleep in a sickbed, it lies down by.</summary>
        private static bool Gentle(Pawn a, Map map, out string why)
        {
            why = null;
            var who = map.mapPawns.FreeColonistsSpawned.Where(p => Troubled(p) && !p.InMentalState && p.Position.InHorDistOf(a.Position, 45f)
                                                                     && a.CanReach(p, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (who == null)
            {
                why = "nobody sick, hurt or low within reach.";
                return false;
            }
            if (who.Awake())
            {
                if (!Nuzzle(a, who))
                {
                    why = "it couldn't get to them.";
                    return false;
                }
                Noted(a, "CC_TemperDid_GentleNuzzle", who.Named("PAWN"));
                return true;
            }
            if (!CellNear(a, who.Position, 2, out var cell) || !Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalLieBy, cell, who, AnimalNatureMath.VigilTicks, who)))
            {
                why = "no room beside them.";
                return false;
            }
            Noted(a, "CC_TemperDid_GentleBed", who.Named("PAWN"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Gentle, "bedside"), null, who.Named("OTHER"));
            return true;
        }

        // ---------------- Proud ----------------

        /// <summary>Its person busy with another animal, it shoulders in; or with visitors about, it shows itself off to them.</summary>
        private static bool Proud(Pawn a, Pawn person, Map map, out string why)
        {
            why = null;
            var other = person?.CurJob?.targetA.Thing as Pawn;
            bool busy = person != null && person.Spawned && person.Map == map && other != null && other != a && other.RaceProps?.Animal == true
                        && person.Position.InHorDistOf(a.Position, 40f) && a.CanReach(person, PathEndMode.Touch, Danger.Some);
            var guest = map.mapPawns.AllPawnsSpawned.Where(p => Stranger(p) && p.Position.InHorDistOf(a.Position, 40f) && a.CanReach(p, PathEndMode.Touch, Danger.Some))
                .RandomElementWithFallback();
            if (busy && (guest == null || Rand.Chance(0.6f)))
            {
                if (!Start(a, FollowJob(person, 1.6f, 900)))
                {
                    why = "it couldn't get to its person.";
                    return false;
                }
                Noted(a, "CC_TemperDid_ProudShoulder", person.Named("PAWN"), other.Named("OTHER"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Proud, "jealous"), person, other.Named("OTHER"));
                return true;
            }
            if (guest != null && CellNear(a, guest.Position, 3, out var cell)
                && Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalShowOff, cell, guest, AnimalNatureMath.ShowTicks, guest)))
            {
                Noted(a, "CC_TemperDid_ProudShowOff", guest.Named("OTHER"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Proud, "visitors"), null, guest.Named("OTHER"));
                return true;
            }
            why = "its person isn't with another animal, and no visitors are about.";
            return false;
        }

        // ---------------- Mischievous ----------------

        private static bool Crop(Pawn a, Plant p, Map map) =>
            p != null && p.Spawned && !p.IsForbidden(Faction.OfPlayer) && map.zoneManager.ZoneAt(p.Position) is Zone_Growing && p.Growth >= 0.3f && p.IngestibleNow
            && a.RaceProps.CanEverEat(p) && !map.reservationManager.IsReservedByAnyoneOf(p, Faction.OfPlayer) && a.CanReach(p, PathEndMode.Touch, Danger.Some);

        private static bool Knockable(Pawn a, Thing t) =>
            t != null && t.def.category == ThingCategory.Item && t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && !(t is Corpse)
            && !(t is MinifiedThing) && t.GetStatValue(StatDefOf.Mass) <= 2f && !t.Map.reservationManager.IsReservedByAnyoneOf(t, Faction.OfPlayer)
            && Spill(t).IsValid && a.CanReach(t, PathEndMode.Touch, Danger.Some);

        /// <summary>A cell beside it on the floor, out of the stores and with nothing on it, for what falls.</summary>
        internal static IntVec3 Spill(Thing t)
        {
            var map = t.Map;
            foreach (var c in GenAdj.CellsAdjacent8Way(t).InRandomOrder())
                if (c.InBounds(map) && c.Standable(map) && c.GetSlotGroup(map) == null && c.GetFirstItem(map) == null && c.GetEdifice(map) == null)
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>A grazer gets into the crops and eats one; anything else knocks a few things off a shelf onto the floor.</summary>
        private static bool Mischief(Pawn a, Map map, out string why)
        {
            why = null;
            if (a.RaceProps.Eats(FoodTypeFlags.Plant) && Rand.Chance(0.7f))
            {
                var crop = GenRadial.RadialDistinctThingsAround(a.Position, map, 30f, true).OfType<Plant>().Where(p => Crop(a, p, map)).Take(20).RandomElementWithFallback();
                if (crop != null)
                {
                    var eat = JobMaker.MakeJob(JobDefOf.Ingest, crop);
                    eat.count = 1;
                    if (Start(a, eat))
                    {
                        Noted(a, "CC_TemperDid_MischiefCrop", crop.def.label.Named("THING"));
                        Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Mischievous, "crops"), null, crop.def.label.Named("THING"));
                        return true;
                    }
                }
            }
            var thing = GenRadial.RadialDistinctThingsAround(a.Position, map, 25f, true).Where(t => Knockable(a, t)).Take(30).RandomElementWithFallback();
            if (thing == null)
            {
                why = "no crop it eats nor anything light on a shelf within reach.";
                return false;
            }
            if (!Start(a, JobMaker.MakeJob(AnimalNatureDefOf.Rimfolk_AnimalKnockOver, thing)))
            {
                why = "it couldn't get to it.";
                return false;
            }
            return true;
        }

        /// <summary>It knocked something off a shelf (JobDriver_AnimalKnockOver): noted, and someone who sees it says so.</summary>
        internal static void KnockedOver(Pawn a, Thing t)
        {
            Noted(a, "CC_TemperDid_MischiefShelf", t.Label.Named("THING"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Mischievous, "shelf"), null, t.LabelNoCount.Named("THING"));
        }

        // ---------------- Loyal ----------------

        /// <summary>Its person, alive and the colony's, when they aren't on its map (away with a caravan or at another home).</summary>
        private static Pawn Away(Pawn a)
        {
            var person = PetTracker.PersonOf(a);
            return person != null && !person.Dead && person.MapHeld != a.Map ? person : null;
        }

        /// <summary>A door between in and out, for a loyal one to wait at: the one nearest it, and the cell on its side.</summary>
        private static bool Door(Pawn a, Map map, out Building door, out IntVec3 cell)
        {
            door = null;
            cell = IntVec3.Invalid;
            float best = float.MaxValue;
            foreach (var d in map.listerBuildings.AllBuildingsColonistOfClass<Building_Door>())
            {
                bool outside = false, inside = false;
                IntVec3 side = IntVec3.Invalid;
                foreach (var c in GenAdj.CellsAdjacentCardinal(d))
                {
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (c.UsesOutdoorTemperature(map)) outside = true;
                    else
                    {
                        inside = true;
                        side = c;
                    }
                }
                if (!outside || !inside || !side.IsValid) continue;
                float dist = d.Position.DistanceToSquared(a.Position);
                if (dist >= best || !a.CanReach(side, PathEndMode.OnCell, Danger.Some)) continue;
                best = dist;
                door = d;
                cell = side;
            }
            return door != null;
        }

        /// <summary>Its person away: it waits at the door, or by their bed when the colony has no door to the outside.</summary>
        private static bool LoyalWait(Pawn a, Map map, out string why)
        {
            why = null;
            var person = Away(a);
            if (person == null)
            {
                why = "its person isn't away.";
                return false;
            }
            if (Door(a, map, out var door, out var cell))
            {
                if (!Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalWaitDoor, cell, door, AnimalNatureMath.WaitTicks, person)))
                {
                    why = "it couldn't get to the door.";
                    return false;
                }
                Noted(a, "CC_TemperDid_LoyalDoor", person.Named("PAWN"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Loyal, "door"), null, person.Named("OTHER"));
                return true;
            }
            var bed = person.ownership?.OwnedBed;
            if (bed != null && bed.Map == map && CellNear(a, bed.Position, 2, out var byBed)
                && Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalLieBy, byBed, bed, AnimalNatureMath.WaitTicks, person)))
            {
                Noted(a, "CC_TemperDid_LoyalBed", person.Named("PAWN"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Loyal, "bed"), null, person.Named("OTHER"));
                return true;
            }
            why = "no door to the outside and no bed of theirs to wait by.";
            return false;
        }

        /// <summary>A loyal one's person seen away, then home again within a few hours: it runs to them.</summary>
        private static void Homecoming(Pawn a, Map map)
        {
            int id = a.thingIDNumber;
            if (Away(a) != null)
            {
                awayAt[id] = Now;
                return;
            }
            if (!awayAt.TryGetValue(id, out int at)) return;
            if (Now - at > GenDate.TicksPerHour * 3)
            {
                awayAt.Remove(id);
                return;
            }
            var person = PetTracker.PersonOf(a);
            if (person == null || !person.Spawned || person.Map != map || !MoreHabits.Calm(a) || a.GetLord() != null) return;
            awayAt.Remove(id);
            // A moment like the others: none at the pace's nothing.
            if (Rate > 0f) Welcome(a, person);
        }

        /// <summary>It runs to its person, home again; they say something to it and may write it down.</summary>
        internal static bool Welcome(Pawn a, Pawn person)
        {
            if (!Nuzzle(a, person)) return false;
            Noted(a, "CC_TemperDid_LoyalHome", person.Named("PAWN"));
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Loyal, "home"), person);
            if (Ready("loyal-journal|" + a.thingIDNumber, 10f))
                CouncilGameComponent.Instance?.journals.WriteTagged(person, "Council_Journal_AnimalNature", Tags(a, Temper.Loyal, "home"), null, a.Named("PET"));
            return true;
        }

        // ---------------- Lazy ----------------

        private static bool Warm(Building b)
        {
            var heat = b.TryGetComp<CompHeatPusher>();
            if (heat == null || !heat.ShouldPushHeatNow || heat.Props == null || heat.Props.heatPerSecond <= 0f) return false;
            var fuel = b.TryGetComp<CompRefuelable>();
            return fuel == null || fuel.HasFuel;
        }

        /// <summary>A nap: by a fire or a heater when it's cold, under a roof when it's hot, else out in the sun near its person.</summary>
        private static bool Lazy(Pawn a, Pawn person, Map map, out string why)
        {
            why = null;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 9 || hour > 17)
            {
                why = "a lazy one naps by day.";
                return false;
            }
            float temp = map.mapTemperature.OutdoorTemp;
            IntVec3 cell = IntVec3.Invalid;
            string where;
            if (temp < 8f)
            {
                where = "fire";
                var fire = map.listerBuildings.allBuildingsColonist.Where(b => Warm(b) && b.Position.InHorDistOf(a.Position, 60f)).OrderBy(b => b.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                if (fire == null || !CellNear(a, fire.Position, 2, out cell))
                {
                    why = "nothing warm burning to nap by.";
                    return false;
                }
            }
            else if (temp > 30f)
            {
                where = "shade";
                if (!CellNear(a, a.Position, 12, out cell, c => c.Roofed(map)))
                {
                    why = "no shade near it.";
                    return false;
                }
            }
            else
            {
                where = "sun";
                var near = person != null && person.Spawned && person.Map == map ? person.Position : a.Position;
                if (!CellNear(a, near, 8, out cell, c => !c.Roofed(map) && map.areaManager.Home[c]))
                {
                    why = "no sunny spot near it.";
                    return false;
                }
            }
            if (!Start(a, Nap(cell, AnimalNatureMath.NapTicks)))
            {
                why = "it couldn't lie down there.";
                return false;
            }
            Noted(a, "CC_TemperDid_Lazy_" + where);
            Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Lazy, where));
            return true;
        }

        // ---------------- Curious ----------------

        private static bool AtWork(Pawn p)
        {
            var j = p.CurJob;
            return j != null && (j.def == JobDefOf.FinishFrame || j.def == JobDefOf.DoBill || j.def == JobDefOf.Mine || j.def == JobDefOf.Repair);
        }

        /// <summary>A visitor about: it follows them, nosing at them; else it goes to watch someone building or at a bench.</summary>
        private static bool Curious(Pawn a, Map map, out string why)
        {
            why = null;
            var guest = map.mapPawns.AllPawnsSpawned.Where(p => Stranger(p) && p.Position.InHorDistOf(a.Position, 40f) && a.CanReach(p, PathEndMode.Touch, Danger.Some))
                .RandomElementWithFallback();
            if (guest != null && Start(a, FollowJob(guest, 2.5f, 1200)))
            {
                Noted(a, "CC_TemperDid_CuriousGuest", guest.Named("OTHER"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Curious, "stranger"), null, guest.Named("OTHER"));
                return true;
            }
            var worker = map.mapPawns.FreeColonistsSpawned.Where(p => AtWork(p) && p.Position.InHorDistOf(a.Position, 35f)).RandomElementWithFallback();
            if (worker != null && CellNear(a, worker.Position, 3, out var cell, c => !c.Equals(worker.Position))
                && Start(a, Linger(AnimalNatureDefOf.Rimfolk_AnimalWatch, cell, worker, 900, worker)))
            {
                Noted(a, "CC_TemperDid_CuriousWork", worker.Named("PAWN"));
                Remark(a, "Council_Pool_SeeTemper", Tags(a, Temper.Curious, "work"), worker);
                return true;
            }
            why = "no visitor about and nobody building or at a bench near it.";
            return false;
        }

        // ---------------- In a fight ----------------

        /// <summary>Animals' temperaments may show in fights: pets as companions, temperaments on, and a pace above nothing.</summary>
        public static bool FightOn => On && Rate > 0f;

        private static bool Hostile(Pawn p, Map map) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner && !p.Position.Fogged(map);

        private static bool Shooting(Pawn p, int now) =>
            p?.mindState != null && now - p.mindState.lastAttackTargetTick < 300 && (p.equipment?.Primary?.def.IsRangedWeapon == true || (p.RaceProps?.IsMechanoid ?? false));

        /// <summary>
        /// Every 250 ticks while a fight is on at home (Sched.RareFightSides): a brave one with its person in reach of an
        /// attacker goes to stand between them; a timid one bolts from the shooting near it. Once a fight each.
        /// </summary>
        public static void FightLook(bool force = false)
        {
            if (!FightOn && !force) return;
            Fresh();
            int now = Now;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || (!force && !MapFacts.ThreatOn(map))) continue;
                var hostiles = map.mapPawns.AllPawnsSpawned.Where(p => Hostile(p, map)).ToList();
                if (hostiles.Count == 0) continue;
                var shooters = map.mapPawns.AllPawnsSpawned.Where(p => Shooting(p, now) && !p.Dead && !p.Downed).ToList();
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.Where(Has).ToList())
                {
                    var t = Of(a);
                    if (t != Temper.Brave && t != Temper.Timid) continue;
                    if (!MoreHabits.Calm(a) || a.GetLord() != null || (a.mindState != null && now - a.mindState.lastAttackTargetTick < 600)) continue;
                    if (!force && !Rand.Chance(AnimalNatureMath.FightChance(Rate))) continue;
                    if (!Ready("fight|" + a.thingIDNumber, AnimalNatureMath.FightGapHours / 24f)) continue;
                    if (t == Temper.Brave) FightBrave(a, hostiles);
                    else FightTimid(a, shooters);
                }
            }
        }

        private static bool FightBrave(Pawn a, List<Pawn> hostiles)
        {
            var person = PetTracker.PersonOf(a);
            // Its person down already: it stands over them all the same.
            if (person == null || !person.Spawned || person.Map != a.Map || a.RaceProps.baseBodySize < AnimalNatureMath.StandMinSize) return false;
            var foe = hostiles.Where(h => h.Position.InHorDistOf(person.Position, AnimalNatureMath.FightRange)).OrderBy(h => h.Position.DistanceToSquared(person.Position)).FirstOrDefault();
            if (foe == null || !Stand(a, person, foe)) return false;
            Noted(a, "CC_TemperDid_BraveFight", person.Named("PAWN"), foe.Named("OTHER"));
            if (Ready("brave-journal|" + a.thingIDNumber, 5f))
                CouncilGameComponent.Instance?.journals.WriteTagged(person, "Council_Journal_AnimalNature", Tags(a, Temper.Brave, "fight"), null, a.Named("PET"));
            return true;
        }

        private static bool FightTimid(Pawn a, List<Pawn> shooters)
        {
            var shot = shooters.Where(s => s.Position.InHorDistOf(a.Position, AnimalNatureMath.FightRange)).OrderBy(s => s.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (shot == null) return false;
            var flee = FleeUtility.FleeJob(a, shot, 18);
            if (flee == null || !Start(a, flee)) return false;
            Noted(a, "CC_TemperDid_TimidFight");
            return true;
        }

        /// <summary>The dev action: a fight's look now, whether or not a fight is on and past the chances, the waits kept.</summary>
        public static string DebugFight(Map map)
        {
            if (map == null) return "no map.";
            int braveOrTimid = map.mapPawns.SpawnedColonyAnimals.Count(a => Has(a) && (Of(a) == Temper.Brave || Of(a) == Temper.Timid));
            int hostile = map.mapPawns.AllPawnsSpawned.Count(p => Hostile(p, map));
            FightLook(true);
            return braveOrTimid == 0 ? "no brave or timid animal here." : hostile == 0 ? "no enemy on the map: the brave stand by someone near an enemy, the timid bolt from shooting."
                : "looked at " + braveOrTimid + " brave or timid animals with " + hostile + " enemies about (each once in eight hours).";
        }
    }
}
