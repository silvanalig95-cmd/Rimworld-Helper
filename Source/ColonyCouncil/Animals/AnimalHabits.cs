using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Two small habits with the colony's animals (Defs/Misc/Rimfolk_SmallHabits_Animals.xml), run like the others
    /// (Immersion/SmallHabits.cs): sitting out with the herd for a while, for someone who loves animals and has a herd
    /// to look at (or a child); and brushing down a horse or another big animal, for someone who loves animals or is
    /// bonded to one, which is a little handling practice too.
    /// </summary>
    public static class AnimalHabits
    {
        /// <summary>One who loves animals with a herd of three or more about, or a child (who likes any herd at all), by day.</summary>
        public static bool HerdFor(Pawn p)
        {
            var map = p.Map;
            if (map == null || VisualTouches.Dark(map)) return false;
            int herd = AnimalFacts.HerdCount(map);
            if (p.DevelopmentalStage.Child()) return herd >= 1;
            if (herd < 3) return false;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            return animals != null && !animals.TotallyDisabled && (People.Passionate(animals) || AnimalFacts.Herdmaster == p || MoreHabits.Feeling(p, "Contentment", 0.5f) != null);
        }

        /// <summary>Where the herd is: the calm animal of it nearest the middle of the rest, near enough to walk to.</summary>
        public static Pawn HerdOf(Pawn p)
        {
            var herd = AnimalFacts.Herd(p.Map).Where(a => MoreHabits.Calm(a) && a.Position.InHorDistOf(p.Position, 70f)).ToList();
            if (herd.Count == 0) return null;
            var sum = IntVec3.Zero;
            foreach (var a in herd) sum += a.Position;
            var middle = new IntVec3(sum.x / herd.Count, 0, sum.z / herd.Count);
            return herd.OrderBy(a => a.Position.DistanceToSquared(middle)).FirstOrDefault(a => p.CanReach(a, PathEndMode.Touch, Danger.None));
        }

        /// <summary>One who loves animals, or is bonded to a big one, while the colony has a big animal (a horse, a muffalo) to brush.</summary>
        public static bool GroomFor(Pawn p)
        {
            if (p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || p.DevelopmentalStage.Baby()) return false;
            var bonded = AnimalFacts.BondedAnimal(p);
            if (bonded != null && Big(bonded)) return true;
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            return animals != null && !animals.TotallyDisabled && People.Passionate(animals) && p.Map.mapPawns.SpawnedColonyAnimals.Any(Big);
        }

        private static bool Big(Pawn a) => MoreHabits.Calm(a) && a.RaceProps.baseBodySize >= 1f && !AnimalFacts.Young(a) && a.RaceProps.IsFlesh;

        /// <summary>The animal to brush: their bonded one if it's big, else their own, else the nearest big calm one.</summary>
        public static Pawn GroomOf(Pawn p)
        {
            var map = p.Map;
            bool Ok(Pawn a) => Big(a) && a.Map == map && a.Position.InHorDistOf(p.Position, 60f) && !a.IsForbidden(p) && p.CanReach(a, PathEndMode.Touch, Danger.None);
            var bonded = AnimalFacts.BondedAnimal(p);
            if (Ok(bonded)) return bonded;
            return map.mapPawns.SpawnedColonyAnimals.Where(Ok)
                .OrderByDescending(a => a.playerSettings?.Master == p).ThenBy(a => a.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        /// <summary>A little handling practice while they brush it (about a tenth of a lesson's worth in a whole while).</summary>
        public static void Practice(Pawn p, Pawn animal, int delta)
        {
            if (p?.skills == null || animal == null || delta <= 0) return;
            p.skills.Learn(SkillDefOf.Animals, 0.04f * delta);
        }
    }

    /// <summary>One who loves animals, with a herd, or a child: a while sitting out with the herd, by day.</summary>
    public class JoyGiver_HabitHerd : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Herd;

        protected override bool Fits(Pawn p) => AnimalHabits.HerdFor(p);

        protected override Job Give(Pawn p)
        {
            var at = AnimalHabits.HerdOf(p);
            if (at == null || (!at.Position.Roofed(p.Map) && !JoyUtility.EnjoyableOutsideNow(p))) return null;
            var cell = SmallHabits.SpotNear(at.Position, p.Map, p);
            return cell.IsValid ? Make(cell, at.Position, new IntRange(1000, 1700)) : null;
        }
    }

    /// <summary>One who loves animals, or is bonded to a big one: a while brushing down a horse or another big animal.</summary>
    public class JoyGiver_HabitGroom : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Groom;

        protected override bool Fits(Pawn p) => AnimalHabits.GroomFor(p);

        protected override Job Give(Pawn p)
        {
            var animal = AnimalHabits.GroomOf(p);
            if (animal == null || (!animal.Position.Roofed(p.Map) && !JoyUtility.EnjoyableOutsideNow(p))) return null;
            var cell = SmallHabits.SpotNear(animal.Position, p.Map, p, x => x.InHorDistOf(animal.Position, 1.9f));
            return cell.IsValid ? Make(cell, animal, new IntRange(700, 1200)) : null;
        }
    }
}
