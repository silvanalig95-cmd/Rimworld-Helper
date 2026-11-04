using System.Linq;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A hamlet's founder by name at once (docs/design/stage1-region.md, item 6): the settlers' leader heads the hamlet among
    /// the notables from the day it stands, and remembers the colony that took them under its protection, so the hamlet
    /// counts as a town the colony has dealt with and stays among the neighbours by name.
    /// </summary>
    public partial class NotableBook
    {
        /// <summary>The hamlet's founder as its head (when the notables are on); <paramref name="note"/>: what they remember of the colony from the start.</summary>
        public static void StaffHamlet(Settlement town, Pawn founder, string note)
        {
            var book = Instance;
            if (!On || book == null || town == null || town.Destroyed || founder == null || founder.Dead || founder.Faction != town.Faction) return;
            MakeHead(town, founder);
            var head = book.notables.FirstOrDefault(n => n.town == town.ID && n.role == NotableRole.Head && n.pawn == founder);
            if (head != null && !note.NullOrEmpty()) head.Remember(note, 0f);
            Changed();
        }
    }
}
