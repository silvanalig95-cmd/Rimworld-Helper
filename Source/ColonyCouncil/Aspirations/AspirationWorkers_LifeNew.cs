using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A reader's year: read so many different books from the colony's shelves, with the game's own reading. Each book
    /// counts once, seen in hand being read (Lives/LifeNew.cs counts it, "booksread"). Only for someone who can read,
    /// in a colony with a few different books to choose from.
    /// </summary>
    public class AspirationWorker_Books : AspirationWorker_Counter
    {
        /// <summary>Different books (by title) at home: on the shelves or lying about.</summary>
        public static int Titles(Map map)
        {
            if (map == null) return 0;
            var titles = new HashSet<string>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Book))
                if (t is Book b && !b.Title.NullOrEmpty()) titles.Add(b.Title);
            foreach (var shelf in map.listerBuildings.allBuildingsColonist.OfType<Building_Bookcase>())
                foreach (var b in shelf.HeldBooks)
                    if (b != null && !b.Title.NullOrEmpty()) titles.Add(b.Title);
            return titles.Count;
        }

        public override bool CanHave(Pawn p)
        {
            if (p?.MapHeld == null || !BookUtility.CanReadEver(p)) return false;
            return Titles(p.MapHeld) >= Mathf.Max(3, (int)def.amount / 2) && base.CanHave(p);
        }

        public override bool StillPossible(Aspiration a) => Progress(a) >= 1f || (a.pawn != null && BookUtility.CanReadEver(a.pawn));

        public override string ProgressText(Aspiration a)
        {
            int n = CouncilGameComponent.Instance?.aspirations.Count(a.pawn, def.counter) ?? 0;
            return "CC_Asp_CountProgress".Translate(Mathf.Max(0, n - (int)a.baseline), (a.target - a.baseline).ToString("0"));
        }
    }
}
