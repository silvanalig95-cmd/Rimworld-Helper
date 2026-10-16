using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Secrets born of what goes on beyond the walls (SecretDef.during): medicine slipped to a neighbour's sick in a
    /// plague year, grain hidden in a famine, a verse given to the minstrels, a debt run up at the caravan's dice. While
    /// a world event of the right kind runs, or the right kind of camp is settled on the colony's land, each such secret
    /// may come to one colonist it fits who has no secret yet: at most one a day. Defs: Defs/SecretDefs/Rimfolk_Secrets_World.xml.
    /// </summary>
    public static class SecretsBeyond
    {
        private static bool Eligible(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile();

        /// <summary>One of these is going on now: ws:kind (a world event of that kind), camp (any camp settled here), camp:defName.</summary>
        public static bool Going(List<string> during)
        {
            if (during.NullOrEmpty()) return false;
            var camp = CampTracker.Instance?.camp;
            bool settled = camp != null && camp.Settled && camp.map != null;
            var ws = WorldSituations.Instance;
            foreach (var d in during)
            {
                if (d.NullOrEmpty()) continue;
                if (d == "camp" && settled) return true;
                if (d.StartsWith("camp:") && settled && camp.kind?.defName == d.Substring(5)) return true;
                if (d.StartsWith("ws:") && ws != null && ws.Running.Any(s => s.def?.kind == d.Substring(3))) return true;
            }
            return false;
        }

        public static void Daily(SecretTracker t)
        {
            if (t == null || !(ColonyCouncilMod.Settings?.secrets ?? false)) return;
            try
            {
                foreach (var def in DefDatabase<SecretDef>.AllDefsListForReading.Where(d => !d.scenario && !d.during.NullOrEmpty()).InRandomOrder())
                {
                    if (!Going(def.during) || !Rand.Chance(def.duringChance)) continue;
                    // Someone at home it fits, who keeps no secret yet, and whose secret this isn't already.
                    var p = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList()
                        .Where(x => Eligible(x) && t.Of(x) == null && def.Fits(x, out _))
                        .RandomElementWithFallback();
                    if (p == null || !t.Give(p, def)) continue;
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("secrets beyond the walls", e);
            }
        }
    }
}
