using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's visit at an errand's pace (Pace.cs): visitors who come to the gate, or are kept at a spot for a while after
    /// their work there (a mast raised, a census walked, a shuttle down), walk in and off the map at the ordinary pace, not
    /// the visit's own half-speed walk. In everything else it is the game's visit (its stay and its length, its gift,
    /// leaving in bitter weather, defending itself when the colony turns on it): its travel toils become travel on an
    /// errand and its way off the map goes at the errand's pace.
    /// </summary>
    public class LordJob_VisitOnErrand : LordJob_VisitColony
    {
        public LordJob_VisitOnErrand()
        {
        }

        public LordJob_VisitOnErrand(Faction faction, IntVec3 spot, int? ticks = null) : base(faction, spot, ticks)
        {
        }

        public override StateGraph CreateGraph()
        {
            var graph = base.CreateGraph();
            // The visit's way out keeps its own list of its toils (the game looks there to tell visitors leaving): swapped there too.
            Errand(graph, exitSubgraph);
            return graph;
        }

        /// <summary>
        /// A lord's graph at an errand's pace: each plain travel toil swapped for one on an errand (in <paramref name="part"/>,
        /// a subgraph attached to it, as well), each way off the map at the errand's pace.
        /// </summary>
        public static void Errand(StateGraph graph, StateGraph part = null)
        {
            if (graph?.lordToils == null) return;
            for (int i = 0; i < graph.lordToils.Count; i++)
            {
                var toil = graph.lordToils[i];
                if (toil is LordToil_ExitMap && toil.data is LordToilData_ExitMap exit)
                {
                    exit.locomotion = Pace.Errand;
                    continue;
                }
                if (toil == null || toil.GetType() != typeof(LordToil_Travel)) continue;
                var errand = new LordToil_TravelOnErrand(toil.FlagLoc) { maxDanger = ((LordToil_Travel)toil).maxDanger };
                graph.lordToils[i] = errand;
                if (part?.lordToils != null)
                    for (int j = 0; j < part.lordToils.Count; j++)
                        if (part.lordToils[j] == toil) part.lordToils[j] = errand;
                foreach (var tr in graph.transitions)
                {
                    if (tr.target == toil) tr.target = errand;
                    for (int s = 0; s < tr.sources.Count; s++)
                        if (tr.sources[s] == toil) tr.sources[s] = errand;
                }
            }
        }
    }
}
