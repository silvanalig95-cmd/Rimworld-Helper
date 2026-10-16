using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The animals' life goals (Defs/AspirationDefs/Rimfolk_Aspirations_Animals.xml): a champion at one's side, a great
    // beast tamed, a herd of one's own keeping, an animal taught everything it can learn.

    /// <summary>
    /// Raise a champion: an animal of their own fights at their side in the colony's battles this many times (counted by
    /// the pets' watch on fights, Strangers/Pets.cs, as "petfought"). Only for someone who can fight, with an animal of
    /// their own, or with a love of animals.
    /// </summary>
    public class AspirationWorker_Champion : AspirationWorker
    {
        public const string Counter = "petfought";

        public override bool CanHave(Pawn p)
        {
            if (p.WorkTagIsDisabled(WorkTags.Violent) || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return false;
            if (AnimalFacts.HasAnimal(p)) return true;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            return animals != null && !animals.TotallyDisabled && People.Passionate(animals);
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = CouncilGameComponent.Instance?.aspirations.Count(a.pawn, Counter) ?? 0;
            a.target = a.baseline + Mathf.Max(1f, def.amount);
            return true;
        }

        private static int Now(Aspiration a) => CouncilGameComponent.Instance?.aspirations.Count(a.pawn, Counter) ?? 0;

        public override float Progress(Aspiration a) => (Now(a) - a.baseline) / Mathf.Max(1f, a.target - a.baseline);

        public override bool StillPossible(Aspiration a) => !a.pawn.WorkTagIsDisabled(WorkTags.Violent);

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate((Now(a) - a.baseline).ToString("0"), (a.target - a.baseline).ToString("0"));
    }

    /// <summary>
    /// Tame a great beast: one of the colony's animals that is huge or as wild as a thrumbo is theirs (bonded, or they're
    /// its master). Only where the land has such beasts, for a handler good enough to dream of it.
    /// </summary>
    public class AspirationWorker_GreatBeast : AspirationWorker
    {
        private static bool Done(Pawn p) => AnimalFacts.AnimalsOf(p).Any(AnimalFacts.Majestic);

        public override bool CanHave(Pawn p)
        {
            var map = p.MapHeld;
            if (map?.Biome == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || Done(p)) return false;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            if (animals == null || animals.TotallyDisabled || !People.Passionate(animals) || animals.Level < 6) return false;
            return PokeWorldLink.WildKinds(map.Biome.AllWildAnimals.Where(AnimalFacts.MajesticKind))?.Any() == true;
        }

        public override float Progress(Aspiration a)
        {
            if (Done(a.pawn)) return 1f;
            // One out on the map: half way there in spirit.
            var map = a.pawn.MapHeld;
            return map != null && map.mapPawns.AllPawnsSpawned.Any(x => x.Faction == null && !x.Dead && AnimalFacts.Majestic(x)) ? 0.5f : 0f;
        }

        public override bool Done(Aspiration a) => Done(a.pawn);

        public override string ProgressText(Aspiration a) => (Done(a.pawn) ? "CC_Asp_Beast_Done" : Progress(a) > 0f ? "CC_Asp_Beast_Near" : "CC_Asp_Beast_None").Translate();
    }

    /// <summary>
    /// Keep a herd: the colony's livestock grows to <see cref="AnimalMath.HerdTarget"/> head (at least the def's amount,
    /// and always a few more than when they set out). For someone who loves animals or growing things, or the herdmaster;
    /// never for a faith that frowns on ranching (the def's forbiddenBy).
    /// </summary>
    public class AspirationWorker_Herd : AspirationWorker
    {
        private static int Herd(Pawn p) => AnimalFacts.HerdCount(p.MapHeld);

        public override bool CanHave(Pawn p)
        {
            if (p.MapHeld == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return false;
            if (AnimalFacts.Herdmaster == p) return true;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            var plants = p.skills?.GetSkill(SkillDefOf.Plants);
            return (animals != null && !animals.TotallyDisabled && People.Passionate(animals)) || (plants != null && People.Burning(plants) && Herd(p) > 0);
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = Herd(a.pawn);
            a.target = AnimalMath.HerdTarget((int)a.baseline, Mathf.RoundToInt(def.amount));
            return true;
        }

        public override float Progress(Aspiration a) => Herd(a.pawn) / Mathf.Max(1f, a.target);

        public override string ProgressText(Aspiration a) => "CC_Asp_HerdProgress".Translate(Herd(a.pawn), a.target.ToString("0"));
    }

    /// <summary>
    /// Train an animal fully: one of their own animals has learned everything it can be taught. For a handler with a love
    /// of animals; an animal of theirs not fully taught yet, or one of the colony's with no master, to start on.
    /// </summary>
    public class AspirationWorker_TrainFully : AspirationWorker
    {
        private static float Best(Pawn p) => AnimalFacts.AnimalsOf(p).Select(AnimalFacts.TrainedShare).DefaultIfEmpty(0f).Max();

        public override bool CanHave(Pawn p)
        {
            if (p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return false;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            if (animals == null || animals.TotallyDisabled || !People.Passionate(animals)) return false;
            var mine = AnimalFacts.AnimalsOf(p);
            if (mine.Any(AnimalFacts.FullyTrained)) return false;
            return mine.Count > 0 || WishWorker_OwnAnimal.Unclaimed(p) != null;
        }

        public override float Progress(Aspiration a) => AnimalFacts.AnimalsOf(a.pawn).Any(AnimalFacts.FullyTrained) ? 1f : Mathf.Min(0.95f, Best(a.pawn));

        public override bool Done(Aspiration a) => AnimalFacts.AnimalsOf(a.pawn).Any(AnimalFacts.FullyTrained);

        public override string ProgressText(Aspiration a) => "CC_Asp_TrainProgress".Translate(Best(a.pawn).ToStringPercent());
    }
}
