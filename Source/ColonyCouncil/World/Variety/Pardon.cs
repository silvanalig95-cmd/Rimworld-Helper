using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A pardon proclaimed (B8 of docs/content-update-proposals.md; Rimfolk_WS_Pardon, SituationWorker_Pardon): the
    /// colony's prisoners of the pardon's people, on the colony's maps, sent home under it. Each is let go by the game's own
    /// release, as a captive freed in the ransoms is (Diplomacy/Ransoms.cs): a warden walks them out to the edge, and as they
    /// leave the map well, their people's goodwill rises by the game's own rule and they remember it (the deed "released",
    /// World/Patch_FactionMemory.cs), which leaves them owing the colony a small favour (Diplomacy/FavoursOwed.cs).
    /// </summary>
    public static class Pardon
    {
        /// <summary>The colony's prisoners the pardon of this people covers: theirs, on any of the colony's maps, not already let go.</summary>
        public static List<Pawn> Covered(Faction f)
        {
            var list = new List<Pawn>();
            if (f == null) return list;
            bool hostile = f.HostileTo(Faction.OfPlayer);
            foreach (var map in Homes.All)
            {
                if (map?.mapPawns == null) continue;
                foreach (var p in map.mapPawns.PrisonersOfColonySpawned)
                {
                    if (p == null || p.Dead || p.guest == null || p.guest.Released) continue;
                    if (VarietyMath.PardonCovers(p.Faction == f, hostile)) list.Add(p);
                }
            }
            return list;
        }

        /// <summary>Each is set to be released: a warden walks them out by the game's own job. Returns those set free.</summary>
        public static List<Pawn> Release(Faction f)
        {
            var freed = new List<Pawn>();
            foreach (var p in Covered(f))
            {
                try
                {
                    p.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
                    freed.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the pardon: a prisoner released", e);
                }
            }
            return freed;
        }
    }

    /// <summary>The colony's prisoners of the pardon's people are let go to walk home under it (<see cref="Pardon"/>).</summary>
    public class DilemmaEffect_PardonRelease : WorldEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = Pardon.Covered(ctx?.faction).Count;
            if (n == 0) return;
            sheet.Play("CC_Variety_Pardon_Impact".Translate(n).Resolve(), "CC_Variety_Pardon_ImpactBrief".Translate(n).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.faction == null || !Gate(ctx)) return;
            var freed = Pardon.Release(ctx.faction);
            if (freed.Count == 0) return;
            // apart: the answer told in a message; the wardens walk them out on the map.
            Tell.Message(NewsKind.World, Importance.Notable, freed[0].MapHeld ?? SituationEffectUtil.MapOf(ctx), "CC_Variety_Pardon_Released".Translate(freed.Count, ctx.faction.Name).Resolve(),
                new LookTargets(freed.Take(3)), MessageTypeDefOf.PositiveEvent);
        }
    }
}
