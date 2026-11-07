using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Two councils becoming one (Colonies/Merges.cs; docs/design/stage3-many-colonies.md, 3b; the player's decision 12A): a
    /// colony whose home was lost (given up or handed over) and whose people come to live at another home. Kept from the
    /// home's loss until the merge's first season is over (no rival from a name taken over then), at most one per lost home.
    /// </summary>
    public class ColonyMerge : IExposable
    {
        /// <summary>The colony that came: its key, its name and when its home was lost.</summary>
        public int from = HomesMath.None;
        public string fromName;
        public int lostAt = -1;
        /// <summary>When it was founded (its founding day, kept as a remembrance where its people live), or -1.</summary>
        public int founded = -1;
        /// <summary>Its people as its home was lost: those who lived there last, and those who moved from it lately.</summary>
        public List<Pawn> people = new List<Pawn>();
        /// <summary>The home they came to live at, and when the two councils became one; none and -1 until then.</summary>
        public int into = HomesMath.None;
        public int mergedAt = -1;
        /// <summary>Those of its people who came (at the merge).</summary>
        public List<Pawn> newcomers = new List<Pawn>();
        /// <summary>When the early election is called (a third or more of the grown-ups), or -1.</summary>
        public int electionAt = -1;
        /// <summary>The names under the old colony's key were looked at again in a naming round.</summary>
        public bool namesLooked;

        public bool Merged => into != HomesMath.None && mergedAt >= 0;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                people.RemoveAll(p => p == null || p.Discarded);
                newcomers.RemoveAll(p => p == null || p.Discarded);
            }
            Scribe_Values.Look(ref from, "from", HomesMath.None);
            Scribe_Values.Look(ref fromName, "fromName");
            Scribe_Values.Look(ref lostAt, "lostAt", -1);
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Collections.Look(ref people, "people", LookMode.Reference);
            Scribe_Values.Look(ref into, "into", HomesMath.None);
            Scribe_Values.Look(ref mergedAt, "mergedAt", -1);
            Scribe_Collections.Look(ref newcomers, "newcomers", LookMode.Reference);
            Scribe_Values.Look(ref electionAt, "electionAt", -1);
            Scribe_Values.Look(ref namesLooked, "namesLooked");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<Pawn>();
                people.RemoveAll(p => p == null);
                newcomers ??= new List<Pawn>();
                newcomers.RemoveAll(p => p == null);
            }
        }
    }

    // The homes' side of a merge: the merges kept (saved only while there is one), and who lived where before.
    public partial class Homes
    {
        /// <summary>Merges noted and not yet over (Colonies/Merges.cs); none in most games.</summary>
        public List<ColonyMerge> merges = new List<ColonyMerge>();

        /// <summary>
        /// Someone's last home changed from one home to another (a day lived at the new one): the home they came from is kept
        /// with when, while there is more than one home or a merge waits for the colony they came from, so a colony's people
        /// who moved before their home was given up still count as its own. Never with one colony.
        /// </summary>
        private void NoteMove(HomeTie t, int before, int now)
        {
            if (!ManyColoniesMath.Moved(before, t.last)) return;
            if (all.Count <= 1 && !merges.Any(m => m != null && m.from == before && !m.Merged)) return;
            t.prev = before;
            t.movedAt = now;
        }

        /// <summary>A colony's people as its home is lost: those who lived there last, and those who moved from it within the thirty days its council is kept.</summary>
        internal List<Pawn> PeopleOfLost(int key, int now)
        {
            var list = new List<Pawn>();
            foreach (var t in ties)
            {
                if (t?.pawn == null || t.pawn.Dead || !ColonyCouncil.People.StillOurs(t.pawn)) continue;
                if (t.last == key || ManyColoniesMath.CameFrom(t.prev, t.movedAt, key, now)) list.Add(t.pawn);
            }
            return list;
        }

        /// <summary>The last home someone lived on for a day (its key, home or not), or none.</summary>
        internal int LastKeyOf(Pawn p) => TieOf(p, false)?.last ?? HomesMath.None;

        private void ExposeMerges()
        {
            if (Scribe.mode == LoadSaveMode.Saving) merges?.RemoveAll(m => m == null);
            // Written only while there is one: a save of one colony is as it was.
            if (Scribe.mode != LoadSaveMode.Saving || merges != null && merges.Count > 0) Scribe_Collections.Look(ref merges, "merges", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                merges ??= new List<ColonyMerge>();
                merges.RemoveAll(m => m == null);
            }
        }
    }
}
