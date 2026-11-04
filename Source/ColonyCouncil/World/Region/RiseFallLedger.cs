using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A ruin Rimfolk made within a home's reach (docs/design/stage1-region.md, item 2, "Ruins"): the town it was (its old
    /// id, name and people), the game's abandoned settlement that marks it on the map, when and why it became a ruin. A
    /// town destroyed in a fight waits here for the game's destroyed settlement to go before its ruin stands
    /// (<see cref="ruin"/> -1 meanwhile). Kept while its ruin stands; Rimfolk takes its own ruin away after
    /// RiseFallMath.RuinDays, settles it again, or lets it go when another took it off the map.
    /// </summary>
    public class LedgerRuin : IExposable
    {
        /// <summary>The town it was: its old world object id (the history's place for it), name and people.</summary>
        public int town = -1;
        public string name;
        public Faction people;
        /// <summary>The abandoned settlement on the map (its world object id), or -1 while a fight's ruin waits to be put there.</summary>
        public int ruin = -1;
        /// <summary>Its tile as a ref.</summary>
        public Ref tile;
        /// <summary>When it became a ruin (a fight's: when the town fell).</summary>
        public int since = -1;
        /// <summary>Why: <see cref="GivenUp"/> or <see cref="Fight"/>.</summary>
        public string why;
        /// <summary>The colony's own people destroyed it (a fight's ruin).</summary>
        public bool byColony;

        public const string GivenUp = "givenup";
        public const string Fight = "fight";

        public bool Waiting => ruin < 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref ruin, "ruin", -1);
            Refs.Look(ref tile, "tile");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref byColony, "byColony");
        }
    }

    /// <summary>
    /// The region's ruins on the world ledger (World/Region/RiseFall.cs reads and writes them through here): the ruins
    /// Rimfolk made within reach and the fights' ruins still waiting to be put on the map.
    /// </summary>
    public partial class WorldLedger
    {
        private List<LedgerRuin> ruins = new List<LedgerRuin>();

        /// <summary>The ruins kept (read only).</summary>
        public IReadOnlyList<LedgerRuin> Ruins => ruins;

        /// <summary>The ruin record of a town (its old id), or null.</summary>
        public static LedgerRuin RuinOfTown(int town)
        {
            var l = Instance;
            if (l == null || town < 0) return null;
            foreach (var r in l.ruins)
                if (r != null && r.town == town)
                    return r;
            return null;
        }

        /// <summary>The ruin record of an abandoned settlement on the map (its world object id), or null.</summary>
        public static LedgerRuin RuinAt(int worldObject)
        {
            var l = Instance;
            if (l == null || worldObject < 0) return null;
            foreach (var r in l.ruins)
                if (r != null && r.ruin == worldObject)
                    return r;
            return null;
        }

        /// <summary>
        /// A ruin kept (once per town). Fights' ruins waiting are RiseFallMath.MaxWaiting at most, the oldest going
        /// first; the ruins standing are bounded per home's reach by the region's day (RiseFall.Daily).
        /// </summary>
        public static LedgerRuin AddRuin(LedgerRuin r)
        {
            var l = Instance;
            if (l == null || r == null || r.town < 0) return null;
            var had = RuinOfTown(r.town);
            if (had != null) return had;
            l.ruins.Add(r);
            var waiting = l.ruins.FindAll(x => x.Waiting);
            for (int i = 0; i < waiting.Count - RiseFallMath.MaxWaiting; i++) l.ruins.Remove(waiting[i]);
            Changed();
            return r;
        }

        /// <summary>A ruin is no longer kept (settled again, faded, taken off the map, out of reach).</summary>
        public static void RemoveRuin(LedgerRuin r)
        {
            var l = Instance;
            if (l == null || r == null) return;
            if (l.ruins.Remove(r)) Changed();
        }

        private void ExposeRiseFall()
        {
            Scribe_Collections.Look(ref ruins, "ruins", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ruins ??= new List<LedgerRuin>();
                ruins.RemoveAll(r => r == null || r.town < 0 || r.tile.IsNone);
            }
        }
    }
}
