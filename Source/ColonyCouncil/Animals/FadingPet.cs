using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An animal in decline (the "animal in decline" dilemma): A's own animal, bonded to A or with A as its master, that
    /// is old for its kind or carries a lasting condition (OnMapMath.Fading). A young, healthy pet never starts it.
    /// </summary>
    public class DilemmaWorker_FadingPet : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var animals = ctx.map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed && Fading(a)).ToList();
            ctx.pet = animals.FirstOrDefault(a => a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Bond, ctx.a))
                      ?? animals.FirstOrDefault(a => a.playerSettings?.Master == ctx.a);
            return ctx.pet != null;
        }

        public static bool Fading(Pawn a)
        {
            if (a?.RaceProps == null || a.ageTracker == null) return false;
            bool chronic = a.health?.hediffSet?.hediffs?.Any(h => h != null && h.Visible && h.def != null && h.def.chronic) ?? false;
            return OnMapMath.Fading(a.ageTracker.AgeBiologicalYearsFloat, a.RaceProps.lifeExpectancy, chronic);
        }
    }
}
