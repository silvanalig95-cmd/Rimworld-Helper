using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's animals in its everyday life ("Pets and companions" setting), once an hour and once a day:
    /// <list type="bullet">
    /// <item>A birth in the herd: the colony news, the first foal on the calendar, and the herdmaster coming to name it
    /// (Animals/AnimalHerdmaster.cs).</item>
    /// <item>Seen on screen: someone milking, shearing, taming, training or tending an animal says something to it, a child
    /// at leisure talks to the animal beside them, and a bonded animal that has nothing to do follows its person about for
    /// a while. These share the once-a-day rate of everything said on screen (Touches/VisualTouches.cs).</item>
    /// <item>The pets' watch tells it when an animal fought beside its person (a champion's life goal counts it) and when a
    /// bonded animal died (a beloved animal's day on the calendar).</item>
    /// </list>
    /// Nothing is saved: which animals were already known is looked at again after a load, and only a newborn under a day
    /// old counts as born.
    /// </summary>
    public static class AnimalLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.petMoments && Current.Game != null;

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.85f);

        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        public static void Hourly(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            Safe("animals: births", () => Births(comp));
            Safe("animals: on screen", OnScreen);
        }

        public static void Daily(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            if (Interplay.On) Safe("animals: herdmaster", () => Interplay.HerdmasterDay2(comp));
        }

        // ---------------- Births ----------------

        private static readonly HashSet<int> known = new HashSet<int>();
        private static Game knownGame;
        private static bool looked;

        private static void Births(CouncilGameComponent comp)
        {
            if (knownGame != Current.Game)
            {
                knownGame = Current.Game;
                known.Clear();
                looked = false;
            }
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.ToList())
                {
                    if (a == null || !known.Add(a.thingIDNumber) || !looked) continue;
                    if (!AnimalFacts.Newborn(a)) continue;
                    var mother = a.GetMother();
                    if (mother == null || mother.Faction != Faction.OfPlayer) continue;
                    Born(comp, a, mother);
                }
            }
            looked = true;
            if (known.Count > 4000) known.Clear();
        }

        private static void Born(CouncilGameComponent comp, Pawn baby, Pawn mother)
        {
            string kind = AnimalFacts.KindLabel(mother);
            bool first = CalendarTracker.Instance != null && CalendarTracker.Instance.Get("foal") == null;
            var person = PetTracker.PersonOf(mother);
            News.Post((person != null ? "CC_Animal_BornTo" : "CC_Animal_Born").Translate(kind.Named("KIND"), mother.Named("MOTHER"), (person ?? mother).Named("PAWN")).Resolve(),
                new LookTargets(baby), NewsKind.Land, MessageTypeDefOf.PositiveEvent, first ? Importance.Notable : Importance.Flavor);
            if (first && CalendarTracker.On)
            {
                var keeper = person ?? AnimalFacts.Herdmaster
                             ?? baby.Map?.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p))
                                 .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
                CalendarTracker.Notify_FirstFoal(baby, mother, keeper);
            }
            if (Interplay.On) Interplay.Notify_Newborn(comp, baby, mother);
        }

        // ---------------- What the pets' watch tells it ----------------

        /// <summary>An animal fought beside its person in a battle (Strangers/Pets.cs): a champion's goal counts it.</summary>
        public static void Notify_PetFought(Pawn pet, Pawn person)
        {
            if (person == null || pet == null) return;
            CouncilGameComponent.Instance?.aspirations.Notify(person, AspirationWorker_Champion.Counter);
        }

        /// <summary>A named or bonded animal died (Strangers/Pets.cs): a beloved one has a day on the calendar.</summary>
        public static void Notify_PetDied(Pawn pet, Pawn person, bool bonded, bool hero, float years)
        {
            if (!On || pet == null || person == null) return;
            Safe("animals: a beloved animal's day", () => CalendarTracker.Notify_Companion(pet, person, years, bonded, hero));
        }

        // ---------------- On screen ----------------

        private static readonly Dictionary<JobDef, string> jobTags = new Dictionary<JobDef, string>();
        private static bool jobsLooked;

        private static string JobTag(Job job)
        {
            if (job?.def == null) return null;
            if (!jobsLooked)
            {
                jobsLooked = true;
                void Add(JobDef d, string tag)
                {
                    if (d != null) jobTags[d] = tag;
                }
                Add(JobDefOf.Milk, "milk");
                Add(JobDefOf.Shear, "shear");
                Add(JobDefOf.Tame, "tame");
                Add(JobDefOf.Train, "train");
                Add(JobDefOf.TendPatient, "tend");
                Add(JobDefOf.FeedPatient, "feed");
                Add(JobDefOf.Rescue, "rescue");
                Add(DefDatabase<JobDef>.GetNamedSilentFail("RopeToPen"), "rope");
                Add(DefDatabase<JobDef>.GetNamedSilentFail("RopeRoamerToUnenclosedPen"), "rope");
            }
            return jobTags.TryGetValue(job.def, out var t) ? t : null;
        }

        /// <summary>The animal a job is about, when it's one of the colony's (or a wild one being tamed).</summary>
        private static Pawn AnimalOf(Job job)
        {
            var t = job?.targetA.Thing as Pawn;
            return t?.RaceProps != null && t.RaceProps.Animal && !t.Dead ? t : null;
        }

        /// <summary>
        /// Once an hour, with someone in view: a word to an animal (someone at work with one, or a child beside one), or a
        /// bonded animal with nothing to do falling in at its person's heels. One thing an hour at most.
        /// </summary>
        private static void OnScreen()
        {
            if (!VisualTouches.On || !Speech.On) return;
            var vt = VisualTouches.Instance;
            if (vt == null || !VisualTouches.InView(out var map, out var view) || !map.IsPlayerHome) return;
            if (map.dangerWatcher.DangerRating != StoryDanger.None || !Rand.Chance(0.5f)) return;
            int now = Find.TickManager.TicksGame;
            if (Rand.Chance(0.6f) ? TalkToAnimal(vt, map, view, now) || Follow(vt, map, view, now) : Follow(vt, map, view, now) || TalkToAnimal(vt, map, view, now)) return;
        }

        private static bool TalkToAnimal(VisualTouches vt, Map map, CellRect view, int now)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return false;
            int start = Rand.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var p = pawns[(start + i) % count];
                if (!vt.Free(p, map, view, now)) continue;
                Pawn animal = null;
                var tags = new List<string>();
                string tag = JobTag(p.CurJob);
                if (tag != null)
                {
                    animal = AnimalOf(p.CurJob);
                    if (animal == null || !animal.Position.InHorDistOf(p.Position, 2.5f)) continue;
                    tags.Add("job:" + tag);
                }
                else if (p.DevelopmentalStage.Child() && Together.FreeTime(p))
                {
                    animal = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a => MoreHabits.Calm(a) && a.Position.InHorDistOf(p.Position, 4f));
                    if (animal == null) continue;
                    tags.Add("childplay");
                }
                else continue;
                if (AnimalFacts.Bonded(animal, p)) tags.Add("bonded");
                if (AnimalFacts.Young(animal)) tags.Add("young");
                if (AnimalFacts.Old(animal)) tags.Add("oldanimal");
                if (PokeWorldLink.IsPokemon(animal)) tags.Add("pokemon");
                if (animal.Faction == null) tags.Add("wild");
                if (animal.Name != null && !animal.Name.Numerical) tags.Add("named");
                string line = Interplay.Line("Council_Pool_SayToAnimal", p, tags.ToArray(), animal.Named("PET"));
                if (line.NullOrEmpty()) continue;
                // apart: said to the animal, not to another colonist.
                if (!Speech.Say(p, line, null, SpeechColor)) continue;
                vt.Spoke(p, now + GenDate.TicksPerDay);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A bonded animal with nothing to do, near its person, falls in at their heels for a little while (the game's own
        /// following), and they say something to it now and then. Not a roped or penned animal, nor one at work.
        /// </summary>
        private static bool Follow(VisualTouches vt, Map map, CellRect view, int now)
        {
            foreach (var a in map.mapPawns.SpawnedColonyAnimals.InRandomOrder().Take(30).ToList())
            {
                if (!MoreHabits.Calm(a) || !view.Contains(a.Position) || a.roping != null && a.roping.IsRoped || a.RaceProps.Roamer) continue;
                var job = a.CurJobDef;
                if (job != JobDefOf.Wait_Wander && job != JobDefOf.GotoWander && job != JobDefOf.Wait) continue;
                var person = a.relations?.DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond).Select(r => r.otherPawn)
                    .FirstOrDefault(o => o != null && o.Spawned && o.Map == map && !o.Dead && !o.Drafted && o.Awake() && o.IsColonist);
                if (person == null || !person.Position.InHorDistOf(a.Position, 18f) || !Interplay.Ready("follow|" + a.thingIDNumber, 1f)) continue;
                if (!a.CanReach(person, PathEndMode.Touch, Danger.None)) continue;
                var follow = JobMaker.MakeJob(JobDefOf.FollowClose, person);
                follow.followRadius = 2.5f;
                follow.expiryInterval = Rand.Range(900, 1600);
                follow.checkOverrideOnExpire = true;
                a.jobs.StartJob(follow, JobCondition.InterruptForced);
                if (vt.Free(person, map, view, now) && Rand.Chance(0.6f))
                {
                    string line = Interplay.Line("Council_Pool_AnimalFollows", person, PokeWorldLink.IsPokemon(a) ? new[] { "pokemon" } : null, a.Named("PET"));
                    // apart: said to the animal at their heels.
                    if (!line.NullOrEmpty() && Speech.Say(person, line, null, SpeechColor)) vt.Spoke(person, now + GenDate.TicksPerDay);
                }
                return true;
            }
            return false;
        }
    }
}
