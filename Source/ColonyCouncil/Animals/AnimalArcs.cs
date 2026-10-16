using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The animals' personal stories (Defs/ArcDefs/Rimfolk_Arcs_Animals.xml): a bonded animal's last years, an orphaned
    // youngster raised by hand, a stray that keeps coming back, a feud with the colony's dog, and with PokéWorld a
    // trainer and their partner Pokémon.

    /// <summary>A bonded animal grown old (four fifths of its kind's years or more): its last good days with its person.</summary>
    public class ArcWorker_OldCompanion : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!p.Spawned || ArcUtil.Psychopath(p)) return null;
            var pet = AnimalFacts.BondedAnimal(p);
            if (pet == null || !pet.Spawned || !AnimalFacts.Old(pet)) return null;
            return new ArcCandidate { other = pet, skill = SkillDefOf.Animals, weight = 1.5f, tag = PokeWorldLink.IsPokemon(pet) ? "pokemon" : null };
        }
    }

    /// <summary>
    /// A young animal of the colony's whose mother is dead or gone, and someone who takes it on: a handler with a love of
    /// animals, a kind soul, a child or the herdmaster. It moves on as it grows; when it's grown, the two may bond.
    /// </summary>
    public class ArcWorker_Orphan : ArcWorker
    {
        private static bool Fond(Pawn p)
        {
            if (p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && !p.DevelopmentalStage.Child()) return false;
            var animals = ArcUtil.Skill(p, SkillDefOf.Animals);
            return (animals != null && !animals.TotallyDisabled && People.Passionate(animals)) || ArcUtil.HasTrait(p, TraitDefOf.Kind)
                   || p.DevelopmentalStage.Child() || AnimalFacts.Herdmaster == p;
        }

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!p.Spawned || ArcUtil.Psychopath(p) || !Fond(p)) return null;
            var young = p.Map.mapPawns.SpawnedColonyAnimals
                .Where(a => AnimalFacts.Orphaned(a) && (a.playerSettings?.Master == null || a.playerSettings.Master == p)
                            && (a.relations == null || !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)))
                .OrderByDescending(a => a.playerSettings?.Master == p).ThenBy(a => a.ageTracker.AgeBiologicalTicks).FirstOrDefault();
            return young == null ? null : new ArcCandidate { other = young, skill = SkillDefOf.Animals, weight = 1.4f, tag = p.DevelopmentalStage.Child() ? "child" : null };
        }

        /// <summary>Custom gate: it's grown.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate) => arc.other != null && !arc.other.Dead && !AnimalFacts.Young(arc.other);
    }

    /// <summary>
    /// A stray: a calm wild animal, small enough to slip in at the gate, that hangs about the colony and someone who
    /// leaves scraps for it. The story waits on it to come closer; in time they mark it for taming, and it ends when it's
    /// the colony's. It goes nowhere if the stray leaves the land.
    /// </summary>
    public class ArcWorker_Stray : ArcWorker
    {
        public override bool OtherMayBeWild => true;

        private static bool Fond(Pawn p)
        {
            var animals = ArcUtil.Skill(p, SkillDefOf.Animals);
            return ArcUtil.HasTrait(p, TraitDefOf.Kind) || (animals != null && !animals.TotallyDisabled && People.Passionate(animals))
                   || p.DevelopmentalStage.Child() || ArcUtil.Feeling(p, "Loneliness") != null;
        }

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!p.Spawned || p.Map == null || ArcUtil.Psychopath(p) || !Fond(p)) return null;
            var map = p.Map;
            var home2 = map.areaManager?.Home;
            var stray = map.mapPawns.AllPawnsSpawned
                .Where(a => AnimalFacts.GentleWild(a, map) && a.RaceProps.baseBodySize <= 1.2f && !PokeWorldLink.IsPokemon(a)
                            && map.designationManager.DesignationOn(a) == null
                            && (a.Position.InHorDistOf(p.Position, 30f) || (home2 != null && home2[a.Position])))
                .OrderBy(a => a.Position.DistanceTo(p.Position)).FirstOrDefault();
            return stray == null ? null : new ArcCandidate { other = stray, skill = SkillDefOf.Animals, weight = 0.9f, tag = p.DevelopmentalStage.Child() ? "child" : null };
        }

        /// <summary>The stray left the land, or someone else's now: the story is over.</summary>
        public override string Broken(Arc arc)
        {
            var a = arc.other;
            if (a == null || a.Dead) return null;
            if (a.Faction == Faction.OfPlayer) return null;
            if (a.Faction != null || !a.Spawned || a.Map != arc.pawn.MapHeld) return "gone";
            return null;
        }

        /// <summary>Custom gate: it's the colony's now.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate) => arc.other != null && !arc.other.Dead && arc.other.Faction == Faction.OfPlayer;
    }

    /// <summary>
    /// A feud with an animal: one of the colony's named animals (someone else's) and a colonist it has taken against, or
    /// who has taken against it: someone it robbed of their dinner lately, or a sharp-tongued, greedy or hungry soul. It
    /// ends in a truce, or a friendship neither will admit to.
    /// </summary>
    public class ArcWorker_AnimalRivalry : ArcWorker
    {
        private static readonly List<string> Robbed = new List<string> { "Council_EncC_LunchStolen", "Council_EncC_GourmandRobbed", "Council_EncC_SmallRobbery" };

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!p.Spawned || ArcUtil.Psychopath(p)) return null;
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            bool robbed = memories != null && memories.Any(m => m?.def != null && Robbed.Contains(m.def.defName) && m.age < GenDate.TicksPerDay * 4);
            var traits = p.story?.traits;
            bool prickly = traits != null && (traits.HasTrait(TraitDefOf.Abrasive) || traits.HasTrait(TraitDefOf.Greedy)
                                              || ArcUtil.HasTrait(p, DefDatabase<TraitDef>.GetNamedSilentFail("Gourmand")));
            if (!robbed && !prickly && !p.DevelopmentalStage.Child()) return null;
            var animal = p.Map.mapPawns.SpawnedColonyAnimals
                .Where(a => AnimalFacts.Ours(a) && a.Name != null && !a.Name.Numerical && !AnimalFacts.Theirs(a, p) && PetTracker.PersonOf(a) != null
                            && !AnimalFacts.Livestock(a) && a.RaceProps.baseBodySize <= 1.5f && !AnimalFacts.Young(a))
                .RandomElementWithFallback();
            if (animal == null) return null;
            return new ArcCandidate
            {
                other = animal, skill = SkillDefOf.Animals, weight = robbed ? 2f : 0.5f, tag = robbed ? "robbed" : p.DevelopmentalStage.Child() ? "child" : null
            };
        }
    }

    /// <summary>
    /// PokéWorld: a trainer and the Pokémon they were given, not yet friends: time together, a fight side by side, and its
    /// friendship growing (PokéWorld's own) until the two are partners, and may bond.
    /// </summary>
    public class ArcWorker_PokemonPartner : ArcWorker
    {
        public const int Friends = 180;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!PokeWorldLink.Active || !p.Spawned || ArcUtil.Psychopath(p)) return null;
            if (p.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && AnimalFacts.Ours(r.otherPawn) && PokeWorldLink.IsPokemon(r.otherPawn))) return null;
            var mon = p.Map.mapPawns.SpawnedColonyAnimals
                .Where(a => PokeWorldLink.IsPokemon(a) && a.playerSettings?.Master == p && !AnimalFacts.Bonded(a, p))
                .Select(a => (a, f: PokeWorldLife.Friendship(a))).Where(x => x.f >= 0 && x.f < Friends)
                .OrderBy(x => x.f).Select(x => x.a).FirstOrDefault();
            return mon == null ? null : new ArcCandidate { other = mon, skill = SkillDefOf.Animals, weight = 1.3f, tag = "pokemon" };
        }

        /// <summary>Custom gate: friends at last (PokéWorld's friendship), or already bonded.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var mon = arc.other;
            if (mon == null || mon.Dead) return false;
            return AnimalFacts.Bonded(mon, arc.pawn) || PokeWorldLife.Friendship(mon) >= Friends;
        }

        public override string Broken(Arc arc) => PokeWorldLink.Active ? null : "gone";
    }

    /// <summary>The story's animal, if it's still wild and on the map, is marked for taming (the game's handlers do the rest).</summary>
    public class ArcEffect_TameStray : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx.pet;
            if (a == null || a.Dead || a.Faction != null || !a.Spawned || !TameUtility.CanTame(a)) return;
            var dm = a.Map.designationManager;
            if (dm.DesignationOn(a, DesignationDefOf.Tame) != null) return;
            dm.AddDesignation(new Designation(a, DesignationDefOf.Tame));
            Messages.Message("CC_Arc_StrayMarked".Translate(a.Named("PET"), (ctx.a?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(a), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
