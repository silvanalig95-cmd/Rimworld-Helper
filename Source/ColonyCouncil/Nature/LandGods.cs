using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The land's turns as the colony's believers read them (CLAUDE.md, "The gods are unseen"): nothing says a god did it.
    /// When a sickness gets into the wild plants, the colonist who holds Morel most (rot, mould and blight are his, as the
    /// colony tells it) may say they take it for his doing; when a burn or a felled wood is woods again, the one who holds
    /// Hollin most (the trees) may say she gave it back. Each says it over their own head, by the name they call the god,
    /// and the colony's book keeps it as their belief, possibly wrong. Nobody who doesn't hold the god says anything.
    /// </summary>
    public static class LandGods
    {
        public const string Pool = "Rimfolk_Land_Pool_Gods";
        public const string Sickness = "Morel", Trees = "Hollin";

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && Worship.Ours(p)
                                            && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p);

        /// <summary>A believer of the god says it ("wilt", "rust", "rot", "woods"); true when someone did.</summary>
        public static bool Said(Map map, string tag, string godKey, IntVec3 at)
        {
            if (map == null || !Worship.On || tag.NullOrEmpty()) return false;
            try
            {
                var g = OwnHappenings.God(godKey);
                if (g == null) return false;
                var p = map.mapPawns.FreeColonistsSpawned.Where(Able)
                    .Where(x => GodLore.Credence(x) >= GodLoreMath.BelieverFrom && PawnGods.HoldOf(x, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(x, g))
                    .OrderByDescending(x => PawnGods.HoldOf(x, g)).FirstOrDefault();
                if (p == null) return false;
                string line = Worship.LineWith(Pool, p, g, null, null, tag);
                if (line.NullOrEmpty()) return false;
                // apart: a believer's word on what the whole colony can see on the land, said to nobody in particular.
                Speech.Say(p, line);
                GodLore.Believes(p, g, ("CC_Land_Belief_" + tag).Translate(Worship.Called(g, p).Named("GOD")).Resolve());
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: a believer's word", e);
                return false;
            }
        }
    }
}
