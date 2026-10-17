using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Two more of the animals' wishes (Defs/WishDefs/Rimfolk_Wishes_Animals2.xml): a favourite kind of animal (or, with
    // PokéWorld, a favourite Pokémon), and a pack animal of one's own.

    /// <summary>
    /// A favourite kind of animal: someone fond of animals has always loved one kind, picked from those this land has (its
    /// wild animals, and what's roaming the map now) that the colony could tame (its best handler has the skill the kind
    /// asks for). With PokéWorld, a favourite Pokémon the same way, caught in a ball while the colony has a belt or can
    /// make one. The same person keeps the same favourite while the land's animals stay the same. Fulfilled when the
    /// colony has one of that kind; bonded to them, it means more (a second moment, when it comes, while the council's
    /// promise is watched).
    /// </summary>
    public class WishWorker_FavouriteAnimal : WishWorker
    {
        private static readonly Dictionary<(int, bool), (int day, List<PawnKindDef> kinds)> pools = new Dictionary<(int, bool), (int, List<PawnKindDef>)>();
        private static Game game;

        private static float BestHandler(Map map)
        {
            float best = 0f;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var s = p.skills?.GetSkill(SkillDefOf.Animals);
                if (s == null || s.TotallyDisabled || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) continue;
                best = Math.Max(best, s.Level);
            }
            return best;
        }

        /// <summary>A kind the colony could keep: an animal that can be tamed (not a dryad, an insect or something wilder than a thrumbo).</summary>
        private static bool Keepable(PawnKindDef k)
        {
            var r = k?.RaceProps;
            if (r == null || !r.Animal || r.Humanlike || r.Dryad || r.Insect || r.IsAnomalyEntity || r.trainability == null) return false;
            return k.race.GetStatValueAbstract(StatDefOf.Wildness) < 0.98f;
        }

        /// <summary>
        /// The kinds this land has, or that are roaming the map now, that the colony could tame: Pokémon only, or none of
        /// them, as <paramref name="pokemon"/> says. Worked out once a day.
        /// </summary>
        public static List<PawnKindDef> Kinds(Map map, bool pokemon)
        {
            if (map == null) return new List<PawnKindDef>();
            if (game != Current.Game)
            {
                game = Current.Game;
                pools.Clear();
            }
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            var key = (map.uniqueID, pokemon);
            if (pools.TryGetValue(key, out var c) && c.day == day) return c.kinds;
            var list = new List<PawnKindDef>();
            try
            {
                var seen = new HashSet<ThingDef>();
                var candidates = new List<PawnKindDef>();
                if (map.Biome != null) candidates.AddRange(map.Biome.AllWildAnimals);
                foreach (var a in map.mapPawns.AllPawnsSpawned)
                    if (a.Faction == null && a.RaceProps != null && a.RaceProps.Animal && !map.fogGrid.IsFogged(a.Position) && a.kindDef != null) candidates.Add(a.kindDef);
                float handler = pokemon ? 0f : BestHandler(map);
                foreach (var k in candidates)
                {
                    if (!Keepable(k) || PokeWorldLink.IsPokemon(k) != pokemon || !seen.Add(k.race)) continue;
                    if (!pokemon && k.race.GetStatValueAbstract(StatDefOf.MinimumHandlingSkill) > handler) continue;
                    list.Add(k);
                }
                list.Sort((x, y) => string.CompareOrdinal(x.defName, y.defName));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favourite animal kinds", e);
                list.Clear();
            }
            if (pools.Count > 20) pools.Clear();
            pools[key] = (day, list);
            return list;
        }

        /// <summary>Their favourite of these: the same one each time for the same person (the gentler kinds likelier).</summary>
        public static PawnKindDef FavouriteOf(Pawn p, List<PawnKindDef> kinds)
        {
            if (p == null || kinds == null || kinds.Count == 0) return null;
            Rand.PushState(Gen.HashCombineInt(p.thingIDNumber, 0x46617621));
            try
            {
                return kinds.RandomElementByWeight(k => 0.3f + Math.Max(0f, k.RaceProps.petness));
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static PawnKindDef KindOf(Wish wish) => DefDatabase<PawnKindDef>.GetNamedSilentFail(wish.subjectKey ?? "");

        /// <summary>The colony's own of that kind here (the one bonded to them first, then their own, then the newest).</summary>
        private static Pawn OursOfKind(Map map, ThingDef race, Pawn person)
        {
            if (map == null || race == null) return null;
            Pawn best = null;
            int bestRank = -1;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (a.def != race || !AnimalFacts.Ours(a)) continue;
                int rank = AnimalFacts.Bonded(a, person) ? 2 : a.playerSettings?.Master == person ? 1 : 0;
                if (best == null || rank > bestRank || (rank == bestRank && a.thingIDNumber > best.thingIDNumber))
                {
                    best = a;
                    bestRank = rank;
                }
            }
            return best;
        }

        private static bool ColonyHas(Map map, ThingDef race, Pawn person) =>
            OursOfKind(map, race, person) != null || (PokeWorldLink.IsPokemon(race) && PokeWorldLink.OwnedSpecies(map).Contains(race));

        /// <summary>One of that kind out on the map, in view and free to approach.</summary>
        private static Pawn WildOf(Map map, ThingDef race) =>
            map?.mapPawns.AllPawnsSpawned.FirstOrDefault(a => a.def == race && a.Faction == null && !a.Dead && !a.Downed && !a.InMentalState && !map.fogGrid.IsFogged(a.Position));

        private static bool Fond(Pawn p, out float weight)
        {
            weight = 0f;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            if (animals != null && !animals.TotallyDisabled && People.Passionate(animals)) weight = People.Burning(animals) ? 1.2f : 1f;
            else if (PeopleTags.Has(p, "animallover")) weight = 1f;
            else if (p.DevelopmentalStage.Child()) weight = 0.7f;
            else if (HasTrait(p, TraitDefOf.Kind) || MoreHabits.Feeling(p, "Loneliness") != null) weight = 0.5f;
            return weight > 0f;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Baby() || !Fond(pawn, out float weight)) yield break;
            foreach (bool pokemon in new[] { false, true })
            {
                if (pokemon && (!PokeWorldLink.Active || !PokeWorldLink.CanCatch(map))) continue;
                if (!pokemon && pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && !pawn.DevelopmentalStage.Child()) continue;
                var kind = FavouriteOf(pawn, Kinds(map, pokemon));
                if (kind == null || ColonyHas(map, kind.race, pawn)) continue;
                var wild = WildOf(map, kind.race);
                string label = kind.label;
                string key = wild != null ? (pokemon ? "CC_PW_Reason_FavouriteHere" : "CC_Reason_FavouriteHere") : (pokemon ? "CC_PW_Reason_Favourite" : "CC_Reason_Favourite");
                var c = Candidate(weight * (wild != null ? 1.5f : 0.6f), key.Translate(pawn.Named("PAWN"), kind.GetLabelPlural().Named("KINDS"), label.Named("KIND")).Resolve());
                c.subjectKey = kind.defName;
                // {SUBJECT} is the kind with its article ("a fox", "an elephant"): the lines never put one before it.
                c.subjectLabel = Find.ActiveLanguageWorker.WithIndefiniteArticle(label);
                c.other = wild;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var kind = KindOf(wish);
            return kind != null && ColonyHas(wish.Beneficiary?.MapHeld, kind.race, wish.Beneficiary);
        }

        public override bool StillValid(Wish wish)
        {
            var kind = KindOf(wish);
            // A kind from a mod since removed is simply gone (its def isn't found).
            return kind != null;
        }

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);

        public override InspirationDef InspirationFor(Wish wish) => PokeWorldLink.IsPokemon(KindOf(wish)) ? null : InspirationDefOf.Inspired_Taming;

        /// <summary>Safe to go after: not a predator or a beast that turns on a tamer who fails.</summary>
        private static bool SafeToTame(Pawn a) =>
            a?.RaceProps != null && !a.RaceProps.predator && a.RaceProps.manhunterOnTameFailChance <= 0.05f && TameUtility.CanTame(a) && !PokeWorldLink.IsPokemon(a);

        private static string MarkForTaming(Wish wish)
        {
            var kind = KindOf(wish);
            var map = wish.Beneficiary?.MapHeld;
            if (kind == null || map == null) return null;
            var a = wish.other != null && wish.other.Spawned && wish.other.Faction == null && wish.other.Map == map ? wish.other : WildOf(map, kind.race);
            if (!SafeToTame(a) || map.designationManager.DesignationOn(a) != null) return null;
            map.designationManager.AddDesignation(new Designation(a, DesignationDefOf.Tame));
            wish.other = a;
            return "CC_Promise_FavouriteTame";
        }

        /// <summary>Promised: one of that kind out there (a calm one) is marked for taming.</summary>
        public override string OnPromised(Wish wish) => MarkForTaming(wish);

        public override string TrySelfHelp(Wish wish) => MarkForTaming(wish) != null ? "CC_SelfHelp_Favourite" : null;

        private static ThoughtDef bondThought;
        private static bool bondLooked;

        /// <summary>The bigger moment: the favourite has bonded to them. A memory that gives tenderness rather than mood, a journal line, news.</summary>
        private static void Bonded(Wish wish)
        {
            var p = wish.Beneficiary;
            var a = wish.other;
            if (p == null || a == null) return;
            wish.baseline = 1f;
            if (!bondLooked)
            {
                bondLooked = true;
                bondThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Granted_FavouriteBond");
            }
            // apart: a bond formed is felt wherever they are; the news only tells it.
            if (bondThought != null) CouncilGameComponent.GiveThought(p, bondThought, a);
            News.Post("CC_Favourite_Bonded".Translate(p.Named("PAWN"), a.Named("ANIMAL")).Resolve(), new LookTargets(p), NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
        }

        public override void OnGranted(Wish wish)
        {
            var kind = KindOf(wish);
            wish.other = OursOfKind(wish.Beneficiary?.MapHeld, kind?.race, wish.Beneficiary) ?? wish.other;
            wish.baseline = 0f;
            if (wish.other != null && AnimalFacts.Bonded(wish.other, wish.Beneficiary)) Bonded(wish);
        }

        /// <summary>
        /// Promises kept: the animal stays the colony's (dead isn't taken back). While it's watched, a bond to them when it
        /// comes is the bigger moment (once).
        /// </summary>
        public override KeptCheck Holds(Wish wish)
        {
            var kept = PetKept(wish.other);
            if (kept == KeptCheck.Holds && wish.baseline < 1f && AnimalFacts.Bonded(wish.other, wish.Beneficiary)) Bonded(wish);
            return kept;
        }

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "pet";
    }

    /// <summary>
    /// A pack animal of one's own: a hauler, someone with a trader's gift for talk, or a restless soul who'd rather be on the
    /// road wants a pack animal that is theirs, as its master or by a bond. One of the colony's with no master is made theirs
    /// when promised; otherwise a calm one roaming the map is marked for taming. Only where the colony has one, or the
    /// land has pack animals it could tame.
    /// </summary>
    public class WishWorker_PackAnimal : WishWorker
    {
        private static bool Pack(Pawn a) => a?.RaceProps != null && a.RaceProps.packAnimal && !PokeWorldLink.IsPokemon(a);

        private static bool HasOne(Pawn p) => AnimalFacts.AnimalsOf(p).Any(Pack);

        private static Pawn Unclaimed(Map map) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => AnimalFacts.Ours(a) && Pack(a) && !AnimalFacts.Young(a) && a.playerSettings != null && a.playerSettings.Master == null
                                                        && (a.relations == null || !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)))
                .OrderByDescending(a => a.GetStatValue(StatDefOf.CarryingCapacity)).FirstOrDefault();

        private static Pawn WildOne(Map map) => map?.mapPawns.AllPawnsSpawned.FirstOrDefault(a => AnimalFacts.GentleWild(a, map) && Pack(a));

        /// <summary>A pack animal this land has that the colony could tame (one of its wild kinds).</summary>
        private static PawnKindDef LandKind(Map map) =>
            map?.Biome?.AllWildAnimals.Where(k => k?.RaceProps != null && k.RaceProps.packAnimal && k.RaceProps.trainability != null && !PokeWorldLink.IsPokemon(k)
                                                  && k.race.GetStatValueAbstract(StatDefOf.Wildness) < 0.85f)
                .OrderBy(k => k.race.GetStatValueAbstract(StatDefOf.Wildness)).FirstOrDefault();

        private static bool Hauler(Pawn p) => p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && p.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)
                                              && p.workSettings.GetPriority(WorkTypeDefOf.Hauling) == 1;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || HasOne(pawn)) yield break;
            bool hauler = Hauler(pawn);
            var social = pawn.skills?.GetSkill(SkillDefOf.Social);
            bool trader = social != null && !social.TotallyDisabled && People.Passionate(social);
            bool roads = WishWorker_CampEvening.Lacks(pawn, HeartNeed.Freedom);
            if (!hauler && !trader && !roads) yield break;
            var free = Unclaimed(map);
            var wild = free == null ? WildOne(map) : null;
            var land = free == null && wild == null ? LandKind(map) : null;
            if (free == null && wild == null && land == null) yield break;
            string kind = free != null || wild != null ? AnimalFacts.KindLabel(free ?? wild) : land.label;
            string withArticle = Find.ActiveLanguageWorker.WithIndefiniteArticle(kind);
            string why = roads ? "CC_Reason_PackRoads" : trader ? "CC_Reason_PackTrader" : "CC_Reason_PackHauler";
            var c = Candidate(free != null ? 1f : wild != null ? 0.7f : 0.4f, why.Translate(pawn.Named("PAWN"), withArticle.Named("KIND")).Resolve());
            c.subjectLabel = withArticle;
            c.subjectKey = "pack";
            c.other = free ?? wild;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasOne(wish.Beneficiary);

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);

        private static string Tame(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var a = wish.other != null && wish.other.Spawned && wish.other.Faction == null && wish.other.Map == map && AnimalFacts.GentleWild(wish.other, map) ? wish.other : WildOne(map);
            if (a == null || map.designationManager.DesignationOn(a) != null) return null;
            map.designationManager.AddDesignation(new Designation(a, DesignationDefOf.Tame));
            wish.other = a;
            return "CC_SelfHelp_Tame";
        }

        /// <summary>Promised: the colony's pack animal with no master answers to them now; else a calm wild one is marked for taming.</summary>
        public override string OnPromised(Wish wish)
        {
            var p = wish.Beneficiary;
            var a = wish.other;
            if (p != null && AnimalFacts.Ours(a) && Pack(a) && a.playerSettings != null && a.playerSettings.Master == null && a.MapHeld == p.MapHeld)
            {
                try
                {
                    a.playerSettings.Master = p;
                    return "CC_Promise_OwnAnimal";
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("wish: a pack animal", e);
                    return null;
                }
            }
            return p?.MapHeld != null ? Tame(wish) : null;
        }

        public override string TrySelfHelp(Wish wish) => wish.Beneficiary?.MapHeld != null ? Tame(wish) : null;

        public override void OnGranted(Wish wish) => wish.other = AnimalFacts.AnimalsOf(wish.Beneficiary).FirstOrDefault(Pack) ?? wish.other;

        public override KeptCheck Holds(Wish wish)
        {
            var kept = PetKept(wish.other);
            if (kept != KeptCheck.Holds) return kept;
            return AnimalFacts.Theirs(wish.other, wish.Beneficiary) ? KeptCheck.Holds : KeptCheck.Gone;
        }

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "pet";
    }
}
