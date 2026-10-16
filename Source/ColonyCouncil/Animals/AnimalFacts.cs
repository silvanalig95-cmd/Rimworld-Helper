using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What Rimfolk knows about the colony's animals, read from the game: the herd (the colony's livestock, Pokémon aside),
    /// whose an animal is, how old it is, whether it was orphaned, how far its training has gone, whether it could be
    /// ridden (Giddy-Up 2, read by name) and whether it is sick. Shared by the animals' wishes, life goals, personal
    /// stories, dilemmas, customs, habits and the herdmaster (Animals/). Nothing here is saved; the herd is counted at
    /// most once an hour per map.
    /// </summary>
    public static class AnimalFacts
    {
        // ---------------- Whose ----------------

        /// <summary>One of the colony's animals, alive.</summary>
        public static bool Ours(Pawn a) => a != null && !a.Dead && a.RaceProps != null && a.RaceProps.Animal && a.Faction == Faction.OfPlayer;

        /// <summary>The animal is this colonist's own: bonded to them, or they're its master.</summary>
        public static bool Theirs(Pawn a, Pawn p) => Ours(a) && Strangers.OfColonist(a, p);

        /// <summary>The colony's animals on this colonist's map that are theirs (bonded first).</summary>
        public static List<Pawn> AnimalsOf(Pawn p)
        {
            var list = new List<Pawn>();
            var map = p?.MapHeld;
            if (map == null) return list;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                if (Theirs(a, p)) list.Add(a);
            list.Sort((x, y) => Bonded(y, p).CompareTo(Bonded(x, p)));
            return list;
        }

        public static bool HasAnimal(Pawn p) => AnimalsOf(p).Count > 0;

        public static bool Bonded(Pawn a, Pawn p) => a?.relations != null && p != null && a.relations.DirectRelationExists(PawnRelationDefOf.Bond, p);

        /// <summary>The colony animal this colonist is bonded to, alive and on their map, or null.</summary>
        public static Pawn BondedAnimal(Pawn p)
        {
            if (p?.relations == null) return null;
            foreach (var r in p.relations.DirectRelations)
                if (r.def == PawnRelationDefOf.Bond && Ours(r.otherPawn) && r.otherPawn.MapHeld == p.MapHeld) return r.otherPawn;
            return null;
        }

        /// <summary>What the animal is ("muffalo", "Eevee"): its kind's label.</summary>
        public static string KindLabel(Pawn a) => a?.kindDef?.label ?? a?.def?.label ?? "?";

        // ---------------- The herd ----------------

        private static bool CompNamed(Pawn a, Type t) => a?.AllComps != null && a.AllComps.Any(c => c != null && t.IsInstanceOfType(c));

        public static bool Milkable(Pawn a) => CompNamed(a, typeof(CompMilkable));

        public static bool Shearable(Pawn a) => CompNamed(a, typeof(CompShearable));

        public static bool LaysEggs(Pawn a) => CompNamed(a, typeof(CompEggLayer));

        /// <summary>
        /// Livestock: one of the colony's animals kept for its milk, wool, eggs or meat, or that lives in a herd or a pen.
        /// Pokémon aren't livestock (nobody culls a trainer's partner for the pot).
        /// </summary>
        public static bool Livestock(Pawn a)
        {
            if (!Ours(a) || PokeWorldLink.IsPokemon(a)) return false;
            var r = a.RaceProps;
            return r.herdAnimal || r.Roamer || Milkable(a) || Shearable(a) || LaysEggs(a);
        }

        private static readonly Dictionary<int, (int tick, int count)> herdCounts = new Dictionary<int, (int, int)>();
        private static Game herdGame;

        /// <summary>The colony's livestock on this map, spawned.</summary>
        public static List<Pawn> Herd(Map map)
        {
            var list = new List<Pawn>();
            if (map?.mapPawns == null) return list;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                if (Livestock(a)) list.Add(a);
            return list;
        }

        /// <summary>How many head of livestock the colony has on this map, counted at most once an hour.</summary>
        public static int HerdCount(Map map)
        {
            if (map == null) return 0;
            if (herdGame != Current.Game)
            {
                herdGame = Current.Game;
                herdCounts.Clear();
            }
            int now = Find.TickManager?.TicksGame ?? 0;
            if (herdCounts.TryGetValue(map.uniqueID, out var c) && Fresh.Holds(c.tick, now, GenDate.TicksPerHour)) return c.count;
            int n = Herd(map).Count;
            herdCounts[map.uniqueID] = (now, n);
            return n;
        }

        // ---------------- Age ----------------

        public static float LifeShare(Pawn a) =>
            a?.ageTracker == null || a.RaceProps == null ? 0f : AnimalMath.LifeShare(a.ageTracker.AgeBiologicalYearsFloat, a.RaceProps.lifeExpectancy);

        public static bool Old(Pawn a) => LifeShare(a) >= AnimalMath.OldShare;

        /// <summary>Not fully grown.</summary>
        public static bool Young(Pawn a) => a?.ageTracker != null && !a.ageTracker.Adult;

        /// <summary>Born within the last day (a newborn the colony hasn't had a look at yet).</summary>
        public static bool Newborn(Pawn a) => a?.ageTracker != null && a.ageTracker.AgeBiologicalTicks < GenDate.TicksPerDay;

        /// <summary>Young, and its mother dead, gone or not the colony's.</summary>
        public static bool Orphaned(Pawn a)
        {
            if (!Ours(a) || !Young(a)) return false;
            var mother = a.GetMother();
            return mother == null || mother.Dead || mother.Faction != Faction.OfPlayer || mother.MapHeld != a.MapHeld;
        }

        // ---------------- Training ----------------

        /// <summary>What it could be taught and what it has learned, counting only what can be taught to it at all.</summary>
        public static void Training(Pawn a, out int learned, out int total)
        {
            learned = 0;
            total = 0;
            var t = a?.training;
            if (t == null) return;
            foreach (var td in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                try
                {
                    var ok = t.CanAssignToTrain(td, out bool visible);
                    if (!visible || !ok.Accepted) continue;
                    total++;
                    if (t.HasLearned(td)) learned++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("animal training " + td?.defName, e);
                }
            }
        }

        public static float TrainedShare(Pawn a)
        {
            Training(a, out int learned, out int total);
            return total == 0 ? 0f : AnimalMath.TrainedShare(learned, total);
        }

        /// <summary>Taught everything it can learn (and it can learn something).</summary>
        public static bool FullyTrained(Pawn a)
        {
            Training(a, out int learned, out int total);
            return total > 0 && learned >= total;
        }

        // ---------------- Riding (Giddy-Up 2, read by name) ----------------

        private static bool giddyLooked;
        private static MethodInfo everMountable;

        /// <summary>Giddy-Up (any version that keeps its IsMountableUtility) is loaded.</summary>
        public static bool GiddyUp
        {
            get
            {
                LookGiddyUp();
                return everMountable != null;
            }
        }

        private static void LookGiddyUp()
        {
            if (giddyLooked) return;
            giddyLooked = true;
            try
            {
                var t = GenTypes.GetTypeInAnyAssembly("GiddyUp.IsMountableUtility");
                everMountable = t?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "IsEverMountable" && m.ReturnType == typeof(bool) && m.GetParameters().Length == 1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up lookup", e);
                everMountable = null;
            }
        }

        private static bool mountFailed;

        /// <summary>
        /// Someone could ride it: with Giddy-Up, as its own options say (body size and the animals ticked in its settings);
        /// without it, a grown animal big enough to carry a rider. Pokémon too, as Giddy-Up allows.
        /// </summary>
        public static bool Rideable(Pawn a)
        {
            if (!Ours(a) || Young(a)) return false;
            LookGiddyUp();
            if (everMountable != null && !mountFailed)
            {
                try
                {
                    return (bool)everMountable.Invoke(null, new object[] { a });
                }
                catch (Exception e)
                {
                    mountFailed = true;
                    CouncilLog.ErrorOnce("Giddy-Up: is it mountable", e);
                }
            }
            return a.RaceProps.baseBodySize >= 1.2f && !a.RaceProps.predator;
        }

        /// <summary>A kind someone could ride, as a kind (for wild animals not yet anyone's): grown, big enough, and not a predator.</summary>
        public static bool RideableKind(ThingDef race) =>
            race?.race != null && race.race.Animal && race.race.baseBodySize >= 1.2f && !race.race.predator && race.race.trainability != null;

        /// <summary>A pack animal, for the road (without Giddy-Up, what someone who travels wants).</summary>
        public static bool PackAnimal(Pawn a) => Ours(a) && !Young(a) && a.RaceProps.packAnimal;

        // ---------------- Health ----------------

        /// <summary>The illness troubling it most (an infection, a plague, a flu), or null.</summary>
        public static Hediff Illness(Pawn a) => Ours(a) ? LifeChecks.Illness(a) : null;

        public static bool Hurt(Pawn a) =>
            Ours(a) && a.health != null && (a.health.HasHediffsNeedingTend() || a.health.summaryHealth.SummaryHealthPercent < 0.7f);

        public static bool Pregnant(Pawn a) => a?.health?.hediffSet != null && HediffDefOf.Pregnant != null && a.health.hediffSet.HasHediff(HediffDefOf.Pregnant);

        // ---------------- The wild ----------------

        /// <summary>A calm wild animal on the map, in view: not a predator, not huge, not likely to turn on a tamer, tameable.</summary>
        public static bool GentleWild(Pawn a, Map map)
        {
            if (a?.RaceProps == null || !a.RaceProps.Animal || a.Faction != null || a.Dead || a.Downed || a.InMentalState || !a.Spawned) return false;
            var r = a.RaceProps;
            if (r.predator || r.baseBodySize > 2.5f || r.manhunterOnTameFailChance > 0.05f || r.trainability == null) return false;
            if (map != null && map.fogGrid.IsFogged(a.Position)) return false;
            return a.GetStatValue(StatDefOf.Wildness) < 0.85f && TameUtility.CanTame(a);
        }

        /// <summary>A wild predator on the map, about and in its right mind (not a manhunter: that's a fight, not a worry).</summary>
        public static bool WildPredator(Pawn a, Map map) =>
            a?.RaceProps != null && a.RaceProps.Animal && a.RaceProps.predator && a.Faction == null && !a.Dead && !a.Downed && a.Spawned
            && !a.InMentalState && (map == null || !map.fogGrid.IsFogged(a.Position));

        /// <summary>A big, rare beast: huge, or as wild as a thrumbo.</summary>
        public static bool Majestic(Pawn a) => Strangers.IsMajestic(a);

        /// <summary>A kind of great beast this land has, that a patient tamer could win over.</summary>
        public static bool MajesticKind(PawnKindDef k) =>
            k?.race?.race != null && k.RaceProps.Animal && k.RaceProps.trainability != null && !k.RaceProps.Insect
            && (k.RaceProps.baseBodySize >= 2.5f || (k.race.GetStatValueAbstract(StatDefOf.Wildness) >= 0.95f));

        // ---------------- The herdmaster ----------------

        public const string HerdmasterOffice = "Council_Office_Herdmaster";

        /// <summary>The herdmaster, while offices are on; null otherwise.</summary>
        public static Pawn Herdmaster => ColonyCouncilMod.Settings.offices ? CouncilGameComponent.Instance?.offices.HolderOf(HerdmasterOffice) : null;
    }

    /// <summary>
    /// Tags for who someone is with animals (see <see cref="PeopleTags"/>), so dilemma roles, dreams, moments, social
    /// events and lines can ask for them: keeper (an animal of their own), bondedanimal, oldcompanion (their bonded
    /// animal is old), herder (a love of animals, with a herd to look after), trainer (with PokéWorld: a Pokémon of their
    /// own). For animals themselves: livestock, mount, oldanimal, young, and with PokéWorld pokemon, shiny and
    /// rarepokemon. Lines about a stranger see them as x:pokemon, x:shiny and so on.
    /// </summary>
    public static class AnimalTags
    {
        public static void Add(Pawn p, HashSet<string> tags)
        {
            if (p?.RaceProps == null || tags == null) return;
            try
            {
                if (p.RaceProps.Animal) AddAnimal(p, tags);
                else if (p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer) AddPerson(p, tags);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal tags", e);
            }
        }

        private static void AddPerson(Pawn p, HashSet<string> tags)
        {
            var mine = AnimalFacts.AnimalsOf(p);
            if (mine.Count == 0) mine = OffMap(p);
            if (mine.Count > 0) tags.Add("keeper");
            var bonded = AnimalFacts.BondedAnimal(p);
            if (bonded != null)
            {
                tags.Add("bondedanimal");
                if (AnimalFacts.Old(bonded)) tags.Add("oldcompanion");
            }
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            if (animals != null && !animals.TotallyDisabled && People.Passionate(animals) && p.MapHeld != null && AnimalFacts.HerdCount(p.MapHeld) >= 3)
                tags.Add("herder");
            if (PokeWorldLink.Active && mine.Any(PokeWorldLink.IsPokemon)) tags.Add("trainer");
        }

        /// <summary>Their animals when they themselves aren't on a map (a caravan): the bonded ones only.</summary>
        private static List<Pawn> OffMap(Pawn p)
        {
            var list = new List<Pawn>();
            if (p.MapHeld != null || p.relations == null) return list;
            foreach (var r in p.relations.DirectRelations)
                if (r.def == PawnRelationDefOf.Bond && AnimalFacts.Ours(r.otherPawn)) list.Add(r.otherPawn);
            return list;
        }

        private static void AddAnimal(Pawn a, HashSet<string> tags)
        {
            if (AnimalFacts.Livestock(a)) tags.Add("livestock");
            if (AnimalFacts.Ours(a) && AnimalFacts.Rideable(a)) tags.Add("mount");
            if (AnimalFacts.Old(a)) tags.Add("oldanimal");
            if (AnimalFacts.Young(a)) tags.Add("young");
            if (!PokeWorldLink.IsPokemon(a)) return;
            tags.Add("pokemon");
            if (PokeWorldLife.Shiny(a)) tags.Add("shiny");
            if (PokeWorldLink.Rare(a.def)) tags.Add("rarepokemon");
        }

        /// <summary>Tags for lines about an animal stranger (X): x:pokemon, x:shiny, x:rarepokemon, x:livestock, x:young.</summary>
        public static void AddX(HashSet<string> tags, Pawn x)
        {
            if (tags == null || x?.RaceProps == null || !x.RaceProps.Animal) return;
            var own = new HashSet<string>();
            Add(x, own);
            foreach (var t in own) tags.Add("x:" + t);
        }
    }
}
