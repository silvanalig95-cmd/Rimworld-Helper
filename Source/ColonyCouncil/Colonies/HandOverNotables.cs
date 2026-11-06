using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town the colony was among the neighbours by name (World/Notables.cs): its head, the head's second (the notables'
    /// heir, titled by the colony's age), its envoy (the most social who stayed) and its trader (the best trader), all of
    /// them the colony's former people. A town the colony was stays among the colony's neighbours wherever it lies, and a
    /// place left empty later is filled from its former people first.
    /// </summary>
    public partial class NotableBook
    {
        /// <summary>The town the colony was, staffed from those who stayed: the head, the second, the envoy and the trader.</summary>
        public void FormerTown(Settlement s, Faction people, List<Pawn> stayed, Pawn head, Pawn second)
        {
            if (s == null || people == null) return;
            foreach (var n in notables.Where(n => n != null && n.town == s.ID).ToList()) Drop(n);
            var taken = new List<Pawn>();
            if (head != null)
            {
                Add(head, s, NotableRole.Head, false, null, null);
                taken.Add(head);
            }
            if (second != null && second != head)
            {
                Add(second, s, NotableRole.Heir, false, null, null);
                taken.Add(second);
            }
            var rest = stayed.Where(p => p != null && !taken.Contains(p) && p.DevelopmentalStage.Adult()).ToList();
            var envoy = HandOvers.BestOf(rest, NotableRole.Envoy);
            if (envoy != null)
            {
                Add(envoy, s, NotableRole.Envoy, false, null, null);
                rest.Remove(envoy);
            }
            var trader = HandOvers.BestOf(rest, NotableRole.Trader);
            if (trader != null) Add(trader, s, NotableRole.Trader, false, null, null);
            NoteTowns();
            Changed();
        }

        /// <summary>A town the colony was is the colony's own again (taken back by its people's word): its notables are let go, its former people colonists again.</summary>
        public void TownGoneOver(int town)
        {
            if (town < 0) return;
            foreach (var n in notables.Where(n => n != null && n.town == town).ToList()) Drop(n);
            NoteTowns();
            Changed();
        }

        /// <summary>One of a town the colony was's former people came home: no longer one of its notables (the place is filled from its former people first).</summary>
        public void LetGoOf(Pawn p)
        {
            if (p == null) return;
            foreach (var n in notables.Where(n => n != null && n.pawn == p && HandOvers.IsFormerTown(n.town)).ToList()) Drop(n);
            Changed();
        }

        /// <summary>Someone is one of the neighbours by name already.</summary>
        public bool IsNotable(Pawn p) => p != null && notables.Any(n => n != null && n.pawn == p);

        /// <summary>The town's notable in a role, or null.</summary>
        public Notable At(int town, NotableRole role) => notables.FirstOrDefault(n => n != null && n.town == town && n.role == role);
    }
}
