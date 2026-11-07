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
    public static class PetHuntDefOf
    {
        /// <summary>A fed animal's hunt for small game whose kill it carries to its person (Defs/Misc/Rimfolk_PetHunts.xml).</summary>
        public static JobDef Rimfolk_PetBringsKill;

        static PetHuntDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PetHuntDefOf));
    }

    /// <summary>
    /// The colony's animals hunting for themselves (the player's request, 7 November 2026; the rules in
    /// <see cref="HuntMath"/>). The game lets a tame animal hunt only when its kind is flagged a predator and nothing at all
    /// is left for it to eat (JobGiver_GetFood's last resort, by the game's own prey rules); a dog, which the game doesn't
    /// flag, never hunts, and a husky kept to an area that leaves out the stores starves. Here, with the setting "Pets hunt
    /// for themselves" on:
    /// <list type="bullet">
    /// <item>Hungry, with nothing it may eat in its allowed area (the game's food search found nothing): an animal whose kind
    /// hunts goes after wild prey below it in the food chain, inside its area, with the game's own hunt (it kills, then eats
    /// the body). Two or more of the colony's own of a pack kind take something a little bigger together. The game's own hunt
    /// for a flagged predator is replaced by this choice, so the same rules hold for every animal of the colony.</item>
    /// <item>Fed, now and then (about every four days), a hunter idle in its area goes after small game; a third of the time,
    /// with its person about, it carries the kill to them (their thought "a present from my animal").</item>
    /// <item>Never drafted, following its master, roped or penned, with a group, away, downed, ill in bed, hurt, young, in a
    /// mental state, or with danger on the map; a hunt is given up when one of those comes, or when it strays from its area.</item>
    /// <item>Seen: whoever is near says a word at the kill (a bubble), its card says what it caught; the alert
    /// <see cref="Alert_NoFoodInReach"/> names the hungry ones that can't, and why.</item>
    /// </list>
    /// The hunt is the game's own fight, so a hunter can be hurt by prey fighting back, as the game's wild hunters are; the
    /// prey is picked to keep that small (smaller and weaker, unlikely to turn on it). No roll of Rimfolk's own: the shared
    /// harm budget (Harm/) doesn't count it, as it doesn't count the game's own predators. Each animal's "may hunt" is kept in
    /// the <see cref="AnimalBook"/>; what is under way is not saved (a hunt running at a save goes on as the game's own job).
    /// The short pass rides on the wild's (Beasts/BeastBook.cs, every 250 ticks); the fed hunts on the people's hour.
    /// </summary>
    public static class PetHunts
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "Pets hunt for themselves" (What's on), on by default.</summary>
        public static bool On => S != null && S.petsHunt && Current.Game != null;

        /// <summary>The alert "An animal with no food in reach" (How you're told, Alerts), on by default.</summary>
        public static bool AlertOn => S != null && S.noFoodAlert && Current.Game != null;

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.85f);

        // ---------------- Kinds ----------------

        private static readonly Dictionary<ThingDef, HuntJudgement> judged = new Dictionary<ThingDef, HuntJudgement>();
        private static readonly Dictionary<ThingDef, (bool venom, bool oddDeath)> preyKinds = new Dictionary<ThingDef, (bool, bool)>();

        /// <summary>What is known of a kind for the food chain, read from its def.</summary>
        public static HuntFacts Facts(ThingDef race)
        {
            var r = race.race;
            var food = r.foodType;
            bool teeth = false, venom = false;
            if (race.tools != null)
                foreach (var t in race.tools)
                {
                    if (t?.capacities == null) continue;
                    foreach (var c in t.capacities)
                    {
                        string n = c?.defName ?? "";
                        if (n.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Scratch", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("Stab", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Sting", StringComparison.OrdinalIgnoreCase) >= 0)
                            teeth = true;
                        if (n.IndexOf("Toxic", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Venom", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("Sting", StringComparison.OrdinalIgnoreCase) >= 0)
                            venom = true;
                    }
                }
            bool pokemon = PokeWorldLink.IsPokemon(race);
            return new HuntFacts
            {
                defName = race.defName, label = race.label, predator = r.predator, maxPrey = r.maxPreyBodySize >= 9999f ? 0f : r.maxPreyBodySize,
                meat = (food & FoodTypeFlags.Meat) != 0,
                plants = (food & (FoodTypeFlags.Plant | FoodTypeFlags.Tree | FoodTypeFlags.VegetableOrFruit | FoodTypeFlags.Seed)) != 0,
                nothing = food == FoodTypeFlags.None, teeth = teeth, venom = venom, insect = r.Insect, dryad = r.Dryad, bodySize = r.baseBodySize,
                pokemon = pokemon, legendary = pokemon && PokeWorldLegends.IsLegendary(race), types = pokemon ? PokeWorldLink.TypesOf(race) : null
            };
        }

        /// <summary>Where its kind stands in the food chain (judged once a session).</summary>
        public static HuntJudgement Judge(ThingDef race)
        {
            if (race?.race == null) return new HuntJudgement { level = FoodChain.Prey, reason = "not an animal", by = "rule" };
            if (judged.TryGetValue(race, out var j)) return j;
            try
            {
                j = HuntMath.Judge(Facts(race));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pets hunting: judging " + race.defName, e);
                j = new HuntJudgement { level = FoodChain.Prey, reason = "unreadable", by = "rule" };
            }
            judged[race] = j;
            return j;
        }

        private static (bool venom, bool oddDeath) PreyKind(ThingDef race)
        {
            if (preyKinds.TryGetValue(race, out var k)) return k;
            var f = Facts(race);
            var death = race.race.deathAction?.workerClass;
            bool odd = death != null && death != typeof(DeathActionWorker_Simple) || (race.GetCompProperties<CompProperties_Explosive>()?.explodeOnKilled ?? false);
            k = (f.venom, odd);
            preyKinds[race] = k;
            return k;
        }

        /// <summary>The kind hunts at all (for the toggle and the dev actions).</summary>
        public static bool HuntingKind(Pawn a) => a?.def?.race != null && Judge(a.def).Hunts;

        // ---------------- The animal's "may hunt" ----------------

        public static bool KeptHome(Pawn a) => a != null && (AnimalBook.Instance?.KeptHome(a.thingIDNumber) ?? false);

        // ---------------- May it hunt now ----------------

        public static HuntState State(Pawn a)
        {
            var s = new HuntState { settingOn = On, keptHome = KeptHome(a), hunts = HuntingKind(a), adult = a.ageTracker?.Adult ?? true, spawned = a.Spawned && a.Map != null };
            if (!s.spawned) return s;
            s.downed = a.Downed;
            s.drafted = a.Drafted;
            s.followsMaster = ThinkNode_ConditionalShouldFollowMaster.ShouldFollowMaster(a);
            s.roped = a.roping != null && a.roping.IsRoped;
            s.penned = AnimalPenUtility.NeedsToBeManagedByRope(a) && AnimalPenUtility.GetCurrentPenOf(a, false) != null;
            s.inLord = a.GetLord() != null;
            s.mental = a.InMentalState;
            s.medicalRest = HealthAIUtility.ShouldSeekMedicalRest(a);
            s.health = a.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            s.meleeAttack = a.meleeVerbs?.TryGetMeleeVerb(null) != null;
            s.canMove = a.health?.capacities != null && a.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
            s.danger = a.Map.dangerWatcher.DangerRating != StoryDanger.None;
            return s;
        }

        /// <summary>Why it may not hunt now (HuntMath.WhyNot's word), or null.</summary>
        public static string WhyNot(Pawn a) => a == null ? "away" : HuntMath.WhyNot(State(a));

        // ---------------- Prey ----------------

        private static float Might(Pawn p) => (p.kindDef?.combatPower ?? 0f) * (p.health?.summaryHealth?.SummaryHealthPercent ?? 1f) * p.BodySize;

        private static HunterFacts HunterOf(Pawn a, bool sport) => new HunterFacts
        {
            kind = Judge(a.def), size = a.BodySize, gameMax = a.RaceProps.maxPreyBodySize >= 9999f ? 0f : a.RaceProps.maxPreyBodySize, might = Might(a), sport = sport
        };

        /// <summary>What the prey is, for the rules.</summary>
        public static PreyFacts PreyOf(Pawn hunter, Pawn x)
        {
            var r = x.RaceProps;
            var k = PreyKind(x.def);
            var map = x.Map;
            return new PreyFacts
            {
                animal = r.Animal, humanlike = r.Humanlike, wild = x.Faction == null && x.HostFaction == null && !x.IsPrisoner,
                penned = (x.roping != null && x.roping.IsRoped) || AnimalPenUtility.GetCurrentPenOf(x, false) != null,
                storied = PackAlphas.Storied(x), sacred = SacredAnimals.HeldSacred(x), legendary = PokeWorldLegends.IsLegendary(x.def), lord = x.GetLord() != null,
                markedForTaming = map?.designationManager?.DesignationOn(x, DesignationDefOf.Tame) != null, mental = x.InMentalState, oddDeath = k.oddDeath,
                insect = r.Insect, venom = k.venom, downed = x.Downed, level = Judge(x.def).level, size = x.BodySize, might = Might(x),
                revenge = PawnUtility.GetManhunterOnDamageChance(x, hunter)
            };
        }

        /// <summary>
        /// The best wild animal it may take now, inside its allowed area and in reach (the game's own prey score: near, weak
        /// and small first); with a pack kind and nothing fit for it alone, what it and one free of its kind beside it may take
        /// together (that one in <paramref name="partner"/>). Null when there is none.
        /// </summary>
        public static Pawn FindPrey(Pawn a, bool sport, out Pawn partner)
        {
            partner = null;
            if (a?.Map == null) return null;
            var h = HunterOf(a, sport);
            if (!h.kind.Hunts) return null;
            var best = Best(a, h);
            if (best != null || sport || !h.kind.pack) return best;
            var mate = PackMate(a);
            if (mate == null) return null;
            h.pack = true;
            best = Best(a, h);
            if (best != null) partner = mate;
            return best;
        }

        private static Pawn Best(Pawn a, HunterFacts h)
        {
            var map = a.Map;
            // A fed one keeps near; a hungry one goes as far as its area lets it.
            float reach = h.sport ? 30f : 120f;
            float limit = HuntMath.PreyLimit(h.kind.level, h.size, h.gameMax, h.pack, h.sport);
            Pawn best = null;
            float bestScore = float.MinValue;
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var x = all[i];
                if (x == a || x.Faction != null || x.Dead || x.RaceProps == null || !x.RaceProps.Animal || x.BodySize > limit) continue;
                if (!x.Position.InHorDistOf(a.Position, reach) || x.Position.Fogged(map) || x.IsPsychologicallyInvisible()) continue;
                if (Judge(x.def).level >= h.kind.level) continue;
                if (HuntMath.WhyNotPrey(h, PreyOf(a, x)) != null) continue;
                // Inside its allowed area (the player's decision stands), not forbidden, and in reach.
                if (x.IsForbidden(a) || !x.Position.InAllowedArea(a) || !a.CanReach(x, PathEndMode.Touch, Danger.Some)) continue;
                float score = FoodUtility.GetPreyScoreFor(a, x);
                if (best == null || score > bestScore)
                {
                    best = x;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>One of the colony's own of its kind near it, grown, free and allowed to hunt, to hunt with.</summary>
        private static Pawn PackMate(Pawn a)
        {
            foreach (var x in a.Map.mapPawns.SpawnedColonyAnimals)
            {
                if (x == a || x.def != a.def || !x.Position.InHorDistOf(a.Position, 12f) || !AnimalTempers.Free(x)) continue;
                if (WhyNot(x) != null || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) continue;
                return x;
            }
            return null;
        }

        // ---------------- What is under way (this session) ----------------

        private sealed class Run
        {
            public Pawn hunter, prey, person;
            public bool sport, carry, pack;
            public int started;
        }

        private static readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
        /// <summary>When each hunter last looked and found nothing (the game asks again often when an animal is hungry).</summary>
        private static readonly Dictionary<int, int> nothingAt = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastSport = new Dictionary<int, int>();
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            runs.Clear();
            nothingAt.Clear();
            lastSport.Clear();
            stranded.Clear();
            strandedLines.Clear();
            seenStranded.Clear();
            owed.Clear();
            saidAt.Clear();
            judged.Clear();
            preyKinds.Clear();
        }

        private static void Begin(Pawn a, Pawn prey, bool sport, bool carry, Pawn person, bool pack)
        {
            runs[a.thingIDNumber] = new Run { hunter = a, prey = prey, person = person, sport = sport, carry = carry, pack = pack, started = Now };
            if (runs.Count > 200) runs.RemoveAll(kv => kv.Value.hunter == null || kv.Value.hunter.Dead || Now - kv.Value.started > GenDate.TicksPerDay);
        }

        private static Job HuntJob(Pawn prey)
        {
            var job = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
            job.killIncappedTarget = true;
            return job;
        }

        private static bool OnHunt(Pawn a, Pawn prey)
        {
            var job = a?.CurJob;
            if (job == null || job.def != JobDefOf.PredatorHunt && job.def != PetHuntDefOf.Rimfolk_PetBringsKill) return false;
            var t = job.targetA.Thing;
            return prey == null || t == prey || t is Corpse c && c.InnerPawn == prey;
        }

        // ---------------- Hungry (JobGiver_GetFood, Patch_PetHuntsHungry) ----------------

        /// <summary>
        /// The food job the game chose for one of the colony's animals, looked at again: food it found stands; with nothing
        /// found, a hunter goes after prey by the rules here; the game's own hunt for a flagged predator is replaced by that
        /// choice (none when the rules allow nothing); an animal kept home never hunts at all.
        /// </summary>
        public static Job Hungry(Pawn a, Job chosen)
        {
            bool gameHunt = chosen != null && chosen.def == JobDefOf.PredatorHunt;
            if (chosen != null && !gameHunt) return chosen;
            Fresh();
            if (KeptHome(a)) return null;
            if (WhyNot(a) != null) return null;
            int now = Now;
            if (nothingAt.TryGetValue(a.thingIDNumber, out int t) && now - t < 600) return null;
            var prey = FindPrey(a, false, out var partner);
            if (prey == null)
            {
                nothingAt[a.thingIDNumber] = now;
                if (nothingAt.Count > 400) nothingAt.RemoveAll(kv => now - kv.Value > 600);
                return null;
            }
            Begin(a, prey, false, false, null, partner != null);
            if (partner != null)
            {
                // Its own of the kind beside it runs the prey down with it (the game's own hunt; it eats its share).
                partner.jobs.StartJob(HuntJob(prey), JobCondition.InterruptForced);
                Begin(partner, prey, false, false, null, true);
            }
            return HuntJob(prey);
        }

        // ---------------- Fed, now and then (the people's hour) ----------------

        /// <summary>
        /// Once an hour: on each home map out of danger, now and then a fed hunter idle in its area goes after small game, one
        /// a map at most; a third of the time, with its person about, it carries the kill to them.
        /// </summary>
        public static void Hourly()
        {
            if (!On) return;
            Fresh();
            int now = Now;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome || map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                int sent = 0;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.InRandomOrder().ToList())
                {
                    if (sent >= HuntMath.SportAnHour) break;
                    if (!HuntingKind(a) || a.needs?.food == null || a.needs.food.CurCategory != HungerCategory.Fed || !AnimalTempers.Free(a)) continue;
                    if (lastSport.TryGetValue(a.thingIDNumber, out int last) && !HuntMath.SportReady(last, now, GenDate.TicksPerDay)) continue;
                    if (!Rand.Chance(HuntMath.SportHourChance())) continue;
                    if (TrySport(a, false, out _)) sent++;
                }
            }
            if (lastSport.Count > 400) lastSport.RemoveAll(kv => now - kv.Value > GenDate.TicksPerDay * 10);
        }

        /// <summary>A fed hunter goes after small game now, if it may and there is some (for the hour and the dev action).</summary>
        public static bool TrySport(Pawn a, bool carryForced, out string why)
        {
            why = WhyNot(a);
            if (why != null) return false;
            var prey = FindPrey(a, true, out _);
            if (prey == null)
            {
                why = "no small game it may take inside its area";
                return false;
            }
            lastSport[a.thingIDNumber] = Now;
            var person = PetTracker.PersonOf(a);
            bool carry = person != null && person.Spawned && person.Map == a.Map && !person.Downed && !person.Drafted && !person.InBed()
                         && a.carryTracker != null && prey.RaceProps.corpseDef != null && a.carryTracker.MaxStackSpaceEver(prey.RaceProps.corpseDef) >= 1
                         && a.CanReach(person, PathEndMode.Touch, Danger.Some) && (carryForced || Rand.Chance(HuntMath.CarryShare));
            Job job;
            if (carry)
            {
                job = JobMaker.MakeJob(PetHuntDefOf.Rimfolk_PetBringsKill, prey, person);
                job.killIncappedTarget = true;
                job.count = 1;
            }
            else job = HuntJob(prey);
            a.jobs.StartJob(job, JobCondition.InterruptForced);
            if (a.CurJob != job)
            {
                why = "it couldn't start";
                return false;
            }
            Begin(a, prey, true, carry, carry ? person : null, false);
            return true;
        }

        // ---------------- The short pass (every 250 ticks, on the wild's) ----------------

        /// <summary>The hunts under way kept to the rules; the hungry ones with nothing in reach looked at for the alert.</summary>
        public static void Rare(int now)
        {
            Fresh();
            if (runs.Count > 0) Watch(now);
            if (AlertOn) LookStranded(now);
            else if (stranded.Count > 0)
            {
                stranded.Clear();
                strandedLines.Clear();
            }
        }

        /// <summary>
        /// Each hunt under way: done when its hunter is off it; given up when a reason not to hunt comes up (drafted, its
        /// master's call, danger, hurt...) or when the hunter has strayed from its allowed area after the prey.
        /// </summary>
        private static void Watch(int now)
        {
            foreach (var kv in runs.ToList())
            {
                var r = kv.Value;
                var a = r.hunter;
                if (a == null || a.Dead || !a.Spawned || !OnHunt(a, r.prey))
                {
                    // Not on it any more (or its job never started): let it go after a moment.
                    if (now - r.started > 120) runs.Remove(kv.Key);
                    continue;
                }
                bool chasing = r.prey != null && !r.prey.Dead && r.prey.Spawned;
                string why = WhyNot(a);
                // Kept home now: the player's word ends it; anything else that bars a hunt ends a chase (a meal already on the
                // ground is eaten, as the game's own hunters do).
                bool stop = why == "kept" || why == "setting" || chasing && why != null || chasing && Strayed(a);
                if (!stop) continue;
                a.jobs.EndCurrentJob(JobCondition.InterruptForced);
                runs.Remove(kv.Key);
            }
        }

        /// <summary>Outside its allowed area by <see cref="HuntMath.StrayCells"/> or more.</summary>
        private static bool Strayed(Pawn a)
        {
            var area = a.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (area == null || area.TrueCount == 0 || area[a.Position]) return false;
            var map = a.Map;
            foreach (var c in GenRadial.RadialCellsAround(a.Position, HuntMath.StrayCells, true))
                if (c.InBounds(map) && area[c])
                    return false;
            return true;
        }

        // ---------------- The kill (Patch_PetHuntsKill) ----------------

        /// <summary>
        /// A wild animal killed by one of the colony's animals on a hunt: its card says what it caught, and whoever sees it
        /// says a word (the shared once-a-day rate of things said on screen).
        /// </summary>
        public static void Notify_Killed(Pawn victim, Pawn hunter)
        {
            Fresh();
            runs.TryGetValue(hunter.thingIDNumber, out var r);
            if (r == null && !OnHunt(hunter, null)) return;
            string what = Find.ActiveLanguageWorker.WithIndefiniteArticle(victim.KindLabel);
            AnimalTempers.Noted(hunter, "CC_PetHunt_Did_Caught", what.Named("THING"));
            if (!AnimalLife.On) return;
            var tags = new List<string>
            {
                r != null && r.carry ? "carry" : "eat", r != null && r.sport ? "sport" : "hungry", HerdStoryMath.BodyTag(AnimalFacts.Body(hunter)),
                victim.BodySize <= HuntMath.SportMax ? "small" : "big"
            };
            if (r != null && r.pack) tags.Add("pack");
            if (PokeWorldLink.IsPokemon(hunter)) tags.Add("pokemon");
            AnimalTempers.Remark(hunter, "Council_Pool_SeeHunt", tags.ToArray(), PetTracker.PersonOf(hunter), what.Named("THING"));
        }

        // ---------------- Carried home (JobDriver_PetBringsKill) ----------------

        /// <summary>The kill set down at its person's feet: a word from them to it, their small glad thought, its card.</summary>
        public static void BroughtHome(Pawn pet, Pawn person, Thing kill)
        {
            // together: the animal's own job carried the kill to their feet, and the word is said to it there.
            if (pet == null || person == null || kill == null) return;
            Fresh();
            runs.Remove(pet.thingIDNumber);
            string what = Find.ActiveLanguageWorker.WithIndefiniteArticle((kill as Corpse)?.InnerPawn?.KindLabel ?? kill.LabelNoCount);
            AnimalTempers.Noted(pet, "CC_PetHunt_Did_Brought", person.Named("PAWN"), what.Named("THING"));
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_PetPresent");
            if (def != null && person.Position.InHorDistOf(pet.Position, 4f)) CouncilGameComponent.GiveThought(person, def);
            if (!AnimalLife.On || !Speech.On || person.Downed || !person.Awake() || person.InMentalState) return;
            var tags = new List<string> { HerdStoryMath.BodyTag(AnimalFacts.Body(pet)), (kill as Corpse)?.InnerPawn?.BodySize > HuntMath.SportMax ? "big" : "small" };
            if (PokeWorldLink.IsPokemon(pet)) tags.Add("pokemon");
            string line = Interplay.Line("Council_Pool_PetBringsKill", person, tags.ToArray(), pet.Named("PET"), what.Named("THING"));
            if (!line.NullOrEmpty()) Speech.Say(person, line, null, SpeechColor);
        }

        // ---------------- The alert's look ----------------

        private static readonly List<Pawn> stranded = new List<Pawn>();
        private static readonly List<string> strandedLines = new List<string>();
        private static readonly HashSet<NoFoodWhy> strandedWhys = new HashSet<NoFoodWhy>();
        /// <summary>Each animal seen hungry with nothing in reach at the last pass (it shows from the second).</summary>
        private static readonly Dictionary<int, int> seenStranded = new Dictionary<int, int>();
        /// <summary>A word owed by its bonded person, until when; and when one was last said.</summary>
        private static readonly Dictionary<int, (NoFoodWhy why, int until)> owed = new Dictionary<int, (NoFoodWhy, int)>();
        private static readonly Dictionary<int, int> saidAt = new Dictionary<int, int>();

        /// <summary>The hungry animals with nothing they may eat in reach and no hunt to go on (for the alert).</summary>
        public static IReadOnlyList<Pawn> Stranded => stranded;

        public static IReadOnlyList<string> StrandedLines => strandedLines;

        public static IEnumerable<NoFoodWhy> StrandedWhys => strandedWhys;

        private static void LookStranded(int now)
        {
            stranded.Clear();
            strandedLines.Clear();
            strandedWhys.Clear();
            int homes = Find.Maps.Count(m => m.IsPlayerHome);
            foreach (var map in Find.Maps)
            {
                if (map?.mapPawns == null) continue;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                {
                    if (!HungryHere(a) || !NoFood(a, out var why, out string hunt))
                    {
                        seenStranded.Remove(a.thingIDNumber);
                        continue;
                    }
                    bool before = seenStranded.TryGetValue(a.thingIDNumber, out int at) && now - at <= Sched.RareTicks * 2;
                    seenStranded[a.thingIDNumber] = now;
                    if (!before) continue;
                    stranded.Add(a);
                    strandedWhys.Add(why);
                    if (strandedLines.Count < 12) strandedLines.Add(Line(a, why, hunt, homes > 1 ? map : null));
                    if (!saidAt.TryGetValue(a.thingIDNumber, out int said) || now - said > HuntMath.SayAgainHours * GenDate.TicksPerHour)
                        if (!owed.ContainsKey(a.thingIDNumber)) owed[a.thingIDNumber] = (why, now + GenDate.TicksPerHour * 6);
                }
            }
            if (seenStranded.Count > 300) seenStranded.RemoveAll(kv => now - kv.Value > GenDate.TicksPerHour);
            if (stranded.Count > strandedLines.Count) strandedLines.Add("CC_NoFood_More".Translate(stranded.Count - strandedLines.Count).Resolve());
            SayOwed(now);
        }

        /// <summary>Hungry, its food not someone else's to bring (in bed, downed), not penned or hitched (the game's own alerts), not with a group.</summary>
        private static bool HungryHere(Pawn a)
        {
            var food = a?.needs?.food;
            if (food == null || food.CurCategory < HungerCategory.Hungry || !a.RaceProps.Animal || a.RaceProps.Dryad || a.Downed) return false;
            if (FoodUtility.ShouldBeFedBySomeone(a) || a.GetLord() != null || a.roping != null && a.roping.IsRoped) return false;
            return !(AnimalPenUtility.NeedsToBeManagedByRope(a) && AnimalPenUtility.GetCurrentPenOf(a, false) != null);
        }

        /// <summary>Nothing it may eat in its area, the game's own search says, and no hunt to go on: why, and why it can't hunt.</summary>
        private static bool NoFood(Pawn a, out NoFoodWhy why, out string hunt)
        {
            why = NoFoodWhy.None;
            hunt = null;
            // Eating now, or already off after prey.
            if (a.CurJobDef == JobDefOf.Ingest || OnHunt(a, null)) return false;
            bool starving = a.needs.food.CurCategory == HungerCategory.Starving;
            if (FoodUtility.BestFoodSourceOnMap(a, a, starving, out _, allowDrug: false, ignoreReservations: true) != null) return false;
            string whyNot = WhyNot(a);
            bool prey = whyNot == null && FindPrey(a, false, out _) != null;
            hunt = HuntMath.HuntWord(whyNot, prey);
            if (hunt == null) return false;
            why = HuntMath.Why(FoodOutside(a), ForbiddenInside(a));
            return true;
        }

        private static bool Edible(Pawn a, Thing t) => t != null && t.Spawned && t.def.IsNutritionGivingIngestible && a.RaceProps.CanEverEat(t) && a.WillEat(t, null, false);

        /// <summary>Something it eats lies outside its area (not forbidden): grass and plants for a grazer, else the food on the map.</summary>
        private static bool FoodOutside(Pawn a)
        {
            var area = a.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (area == null || area.TrueCount == 0) return false;
            var map = a.Map;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                if (!area[t.Position] && !t.IsForbidden(Faction.OfPlayer) && Edible(a, t))
                    return true;
            if (!a.RaceProps.Eats(FoodTypeFlags.Plant) && !a.RaceProps.Eats(FoodTypeFlags.Tree)) return false;
            int looked = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
            {
                if (++looked > 3000) break;
                if (!area[t.Position] && Edible(a, t)) return true;
            }
            return false;
        }

        /// <summary>Food it eats inside its area, all forbidden.</summary>
        private static bool ForbiddenInside(Pawn a)
        {
            foreach (var t in a.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                if (t.Position.InAllowedArea(a) && t.IsForbidden(Faction.OfPlayer) && Edible(a, t))
                    return true;
            return false;
        }

        /// <summary>"  - Yuma (husky): the food it eats lies outside its allowed area, and its kind doesn't hunt."</summary>
        private static string Line(Pawn a, NoFoodWhy why, string hunt, Map map)
        {
            string name = a.Name != null && !a.Name.Numerical ? a.LabelShort + " (" + a.KindLabel + ")" : a.LabelShort;
            string line = "  - " + "CC_NoFood_Line".Translate(name.Named("NAME"), ("CC_NoFood_Why_" + why).Translate(Diet(a).Named("FOOD")).Named("WHY"),
                ("CC_NoFood_Hunt_" + hunt).Translate().Named("HUNT")).Resolve();
            return map != null ? line + " " + "CC_FreshCarcass_At".Translate(Homes.Name(map).Named("PLACE")).Resolve() : line;
        }

        /// <summary>What it eats, in a few words, for the alert's fix ("raw meat, kibble or meals").</summary>
        public static string Diet(Pawn a)
        {
            var words = new List<string>();
            var r = a.RaceProps;
            if (r.Eats(FoodTypeFlags.Kibble)) words.Add("CC_NoFood_Eats_Kibble".Translate().Resolve());
            if (r.Eats(FoodTypeFlags.Meat)) words.Add("CC_NoFood_Eats_Meat".Translate().Resolve());
            if (r.Eats(FoodTypeFlags.Plant)) words.Add("CC_NoFood_Eats_Hay".Translate().Resolve());
            if (r.Eats(FoodTypeFlags.VegetableOrFruit)) words.Add("CC_NoFood_Eats_Veg".Translate().Resolve());
            if (r.Eats(FoodTypeFlags.Meal) && words.Count < 3) words.Add("CC_NoFood_Eats_Meals".Translate().Resolve());
            if (words.Count == 0) return "CC_NoFood_Eats_Any".Translate().Resolve();
            return words.Count == 1 ? words[0] : string.Join(", ", words.Take(words.Count - 1)) + " " + "CC_NoFood_Or".Translate().Resolve() + " " + words[words.Count - 1];
        }

        /// <summary>Its bonded person says so aloud once when it starts, when they're up and about on the map being looked at, within six hours.</summary>
        private static void SayOwed(int now)
        {
            if (owed.Count == 0) return;
            foreach (var kv in owed.ToList())
            {
                var a = stranded.FirstOrDefault(x => x.thingIDNumber == kv.Key);
                if (a == null || now > kv.Value.until)
                {
                    owed.Remove(kv.Key);
                    continue;
                }
                var person = a.relations?.DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond).Select(r => r.otherPawn)
                    .FirstOrDefault(p => p != null && !p.Dead && p.IsColonist);
                if (person == null)
                {
                    owed.Remove(kv.Key);
                    continue;
                }
                if (!Speech.On || !person.Spawned || person.Map != a.Map || person.Map != Find.CurrentMap || person.Downed || !person.Awake() || person.InMentalState
                    || person.Drafted || !People.HasInnerLife(person)) continue;
                var tags = new List<string> { kv.Value.why.ToString().ToLowerInvariant(), HerdStoryMath.BodyTag(AnimalFacts.Body(a)) };
                if (PokeWorldLink.IsPokemon(a)) tags.Add("pokemon");
                string line = Interplay.Line("Council_Pool_SayPetNoFood", person, tags.ToArray(), a.Named("PET"));
                // apart: said aloud about the animal, to nobody in particular.
                if (!line.NullOrEmpty() && Speech.Say(person, line, null, SpeechColor))
                {
                    saidAt[kv.Key] = now;
                    owed.Remove(kv.Key);
                }
            }
            if (saidAt.Count > 300) saidAt.RemoveAll(kv => now - kv.Value > HuntMath.SayAgainHours * GenDate.TicksPerHour);
        }

        // ---------------- The button ----------------

        private static Texture2D icon;

        /// <summary>"May hunt" on one of the colony's animals whose kind hunts, while the setting is on.</summary>
        public static Gizmo GizmoFor(Pawn a)
        {
            if (!On || a == null || a.Faction != Faction.OfPlayer || a.RaceProps == null || !a.RaceProps.Animal || !HuntingKind(a)) return null;
            var book = AnimalBook.Instance;
            if (book == null) return null;
            icon ??= ContentFinder<Texture2D>.Get("UI/Designators/Hunt", false) ?? BaseContent.BadTex;
            int id = a.thingIDNumber;
            return new Command_Toggle
            {
                defaultLabel = "CC_PetHunt_Gizmo".Translate(),
                defaultDesc = "CC_PetHunt_GizmoDesc".Translate(a.Named("PET")),
                icon = icon,
                isActive = () => !book.KeptHome(id),
                toggleAction = () => book.SetKeptHome(id, !book.KeptHome(id)),
                groupKey = 0x52466801
            };
        }

        // ---------------- The dev actions ----------------

        /// <summary>Why this animal hunts or not and what it could take now, for the log.</summary>
        public static string Explain(Pawn a)
        {
            var j = Judge(a.def);
            var lines = new List<string> { a.LabelShort + " (" + a.def.defName + "): " + j, "  now: " + (WhyNot(a) ?? "may hunt") + (KeptHome(a) ? " (kept home)" : "") };
            var h = HunterOf(a, false);
            lines.Add("  biggest prey alone " + HuntMath.PreyLimit(j.level, h.size, h.gameMax, false, false).ToString("0.00") + ", in a pack "
                      + HuntMath.PreyLimit(j.level, h.size, h.gameMax, true, false).ToString("0.00") + ", small game " + HuntMath.PreyLimit(j.level, h.size, h.gameMax, false, true).ToString("0.00"));
            if (a.Map != null)
                foreach (var x in a.Map.mapPawns.AllPawnsSpawned.Where(x => x.Faction == null && x.RaceProps?.Animal == true && !x.Dead)
                             .OrderBy(x => x.Position.DistanceToSquared(a.Position)).Take(20))
                {
                    string why = HuntMath.WhyNotPrey(h, PreyOf(a, x));
                    if (why == null && (x.IsForbidden(a) || !x.Position.InAllowedArea(a))) why = "outside its area";
                    lines.Add("  " + x.LabelShort + " (" + Judge(x.def).level + ", size " + x.BodySize.ToString("0.00") + "): " + (why ?? "it may take it"));
                }
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// A fed animal's hunt for small game whose kill it carries to its person (target A the prey, then its body; target B the
    /// person): the game's own chase and blows (as JobDriver_PredatorHunt's), then it picks the body up, walks to them and
    /// sets it down at their feet. Given up on danger or a call to arms; what it carried is set down where it stands.
    /// </summary>
    public class JobDriver_PetBringsKill : JobDriver
    {
        private bool firstHit = true;

        private Corpse Kill => job.GetTarget(TargetIndex.A).Thing as Corpse;

        private Pawn Prey => Kill?.InnerPawn ?? job.GetTarget(TargetIndex.A).Thing as Pawn;

        private Pawn Person => job.GetTarget(TargetIndex.B).Thing as Pawn;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref firstHit, "firstHit", true);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed || pawn.InMentalState || pawn.Drafted);
            this.FailOn(() => pawn.Map != null && pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            AddFinishAction(delegate { pawn.Map?.attackTargetsCache.UpdateTarget(pawn); });
            var takeUp = ToilMaker.MakeToil("PetBringsKill_Body");
            takeUp.initAction = () =>
            {
                var body = Kill ?? Prey?.Corpse;
                if (body == null || !body.Spawned || pawn.carryTracker == null || pawn.carryTracker.AvailableStackSpace(body.def) < 1)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                body.SetForbidden(false, false);
                job.SetTarget(TargetIndex.A, body);
            };
            void Hit()
            {
                var prey = Prey;
                if (prey == null || prey.Dead) return;
                if (pawn.meleeVerbs.TryMeleeAttack(prey, job.verbToUse, firstHit))
                {
                    firstHit = false;
                    pawn.Map?.attackTargetsCache.UpdateTarget(pawn);
                }
            }
            var chase = Toils_Combat.FollowAndMeleeAttack(TargetIndex.A, Hit).JumpIfDespawnedOrNull(TargetIndex.A, takeUp).JumpIf(() => Kill != null, takeUp)
                .FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return chase;
            yield return takeUp;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            var walk = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            walk.FailOn(() => Person == null || Person.Dead || !Person.Spawned || Person.Map != pawn.Map || Person.Downed);
            yield return walk;
            var setDown = ToilMaker.MakeToil("PetBringsKill_SetDown");
            setDown.initAction = () =>
            {
                var carried = pawn.carryTracker?.CarriedThing;
                if (carried == null) return;
                if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var down))
                    PetHunts.BroughtHome(pawn, Person, down ?? carried);
            };
            yield return setDown;
        }
    }
}
