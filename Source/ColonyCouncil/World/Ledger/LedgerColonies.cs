using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One colony the colony ever founded, on the world ledger (docs/design/stage3-many-colonies.md, "The ledger and the
    /// history own it"): its key (the settlement's id, as <see cref="Homes.KeyOf"/>), its name and tile, when it was
    /// founded and from which colony, and how and when it ended (abandoned; handed over, lost or taken back come with
    /// later releases) with the ruin it left. Written only once a second colony is founded, so a save of one colony keeps
    /// none.
    /// </summary>
    public class LedgerColony : IExposable
    {
        public int key = HomesMath.None;
        public string name;
        public int tile = -1;
        public int founded = -1;
        /// <summary>The colony it was founded from (its key), or none for the colony settled with the game.</summary>
        public int mother = HomesMath.None;
        public string motherName;
        public ColonyEnd end = ColonyEnd.None;
        public int endedAt = -1;
        /// <summary>What it left on the world: the game's ruin (an abandoned settlement's id), or -1.</summary>
        public int left = -1;

        public bool Standing => end == ColonyEnd.None;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key", HomesMath.None);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref tile, "tile", -1);
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Values.Look(ref mother, "mother", HomesMath.None);
            Scribe_Values.Look(ref motherName, "motherName");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref endedAt, "endedAt", -1);
            Scribe_Values.Look(ref left, "left", -1);
        }
    }

    /// <summary>The record of every colony (<see cref="LedgerColony"/>), 24 at most: past that the oldest ended ones fold into the history, which keeps their milestones.</summary>
    public partial class WorldLedger
    {
        private List<LedgerColony> colonyRecords = new List<LedgerColony>();

        /// <summary>Every colony on the record, in the order they were written down.</summary>
        public static IReadOnlyList<LedgerColony> Colonies => (IReadOnlyList<LedgerColony>)Instance?.colonyRecords ?? Array.Empty<LedgerColony>();

        /// <summary>A colony's record by its key, or null.</summary>
        public static LedgerColony ColonyRecord(int key)
        {
            var l = Instance;
            if (l == null || key == HomesMath.None) return null;
            for (int i = 0; i < l.colonyRecords.Count; i++)
                if (l.colonyRecords[i] != null && l.colonyRecords[i].key == key)
                    return l.colonyRecords[i];
            return null;
        }

        /// <summary>
        /// A home's record, written down when first asked: its name, tile and founding day (<paramref name="founded"/>:
        /// its calendar's), and the colony it was founded from. A record already there keeps what it had.
        /// </summary>
        public static LedgerColony NoteColony(Map home, int founded, int mother = HomesMath.None, string motherName = null)
        {
            var l = Instance;
            if (l == null || home == null) return null;
            int key = Homes.KeyOf(home);
            var r = ColonyRecord(key);
            if (r != null) return r;
            r = new LedgerColony { key = key, name = Homes.Name(home), tile = home.Tile.tileId, founded = founded, mother = mother, motherName = motherName };
            l.colonyRecords.Add(r);
            l.FoldColonies();
            Changed();
            return r;
        }

        /// <summary>A colony ended (abandoned; the hand-over is release 2's): when, how, and the ruin or town it left.</summary>
        public static void EndColony(int key, ColonyEnd end, int left)
        {
            var r = ColonyRecord(key);
            if (r == null || !r.Standing) return;
            r.end = end;
            r.endedAt = Find.TickManager?.TicksGame ?? 0;
            r.left = left;
            Instance?.FoldColonies();
            Changed();
        }

        private void FoldColonies()
        {
            int i;
            while ((i = FoundingMath.RecordToFold(colonyRecords.Select(c => c.Standing ? -1 : c.endedAt).ToList())) >= 0) colonyRecords.RemoveAt(i);
        }

        private void ExposeColonies()
        {
            // A save of one colony writes nothing here.
            if (Scribe.mode != LoadSaveMode.Saving || colonyRecords.Count > 0) Scribe_Collections.Look(ref colonyRecords, "colonies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                colonyRecords ??= new List<LedgerColony>();
                colonyRecords.RemoveAll(c => c == null || c.key == HomesMath.None);
            }
        }
    }
}
