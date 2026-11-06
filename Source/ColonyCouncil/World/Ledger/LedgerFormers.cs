using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town the colony was, on the world ledger (docs/design/stage3-many-colonies.md, section 2; release 2a): the
    /// settlement it was and is (its id is the colony's own key, so the history finds the colony's past under the colony
    /// ref and the town's under the town ref), the people it became, the colony's name and age at the hand-over (the
    /// people's words keep that age), when it was founded and handed over, its terms, its second (the next head), the
    /// colony's former people who stayed, and the bond in goodwill (decision 8A) until a war or a taking by force ends it.
    /// Its fortune, founding and founder are on its <see cref="LedgerTown"/> (kind <see cref="TownKind.Former"/>), its
    /// head and second among the notables. Written only once a colony is handed over.
    /// <para>
    /// Release 2b (Colonies/TownsAfter.cs, TownLayout.cs): the kept layout (the colony as it was left, placed when the
    /// town's map is made), the town's fortune at the hand-over, its head's last letter, the last homecoming asked from it
    /// and the last invitation to its days, the colony's letter asking it back and their yes, and how it stopped being its
    /// people's (taken back by their word, beaten by force, settled again).
    /// </para>
    /// </summary>
    public class LedgerFormer : IExposable
    {
        /// <summary>The town's settlement id: the colony's own key (<see cref="Homes.KeyOf"/>), the same world object.</summary>
        public int key = HomesMath.None;
        public Faction people;
        public string colonyName;
        /// <summary>The colony's age at the hand-over: the people's words keep it.</summary>
        public WorldAge age;
        public int founded = -1;
        public int handedAt = -1;
        public HandOverTerms terms;
        /// <summary>Whoever leads after the head (the notables' heir): the people's leader when the head dies or is lost.</summary>
        public Pawn second;
        /// <summary>The colony's former people who stayed (kept in the world with KeepFor.Former).</summary>
        public List<Pawn> formers = new List<Pawn>();
        /// <summary>The home those who went went to, when they went to one (for the town's letters).</summary>
        public int wentTo = HomesMath.None;
        /// <summary>The bond in goodwill is over (a war between them, or a taking by force), and when.</summary>
        public bool bondEnded;
        public int bondEndedAt = -1;
        /// <summary>The colony as it was left (Colonies/TownLayout.cs), placed when the town's map is made; null when none was kept.</summary>
        public LedgerLayout layout;
        /// <summary>The town's fortune at the hand-over: how much it has grown since weighs on the colony's letter asking it back.</summary>
        public float seed = -1f;
        /// <summary>Its head's last letter, the last homecoming asked from it, and its last invitation to one of its days.</summary>
        public int lastLetter = -1, lastHomecoming = -1, lastInvite = -1;
        /// <summary>The colony's last letter asking for it back; their yes, and when it reaches the colony (it can be taken back from then).</summary>
        public int askedAt = -1;
        public bool agreed;
        public int agreedAt = -1;
        public bool agreedTold;
        /// <summary>How it stopped being its people's (taken back by their word, beaten by force, settled again), and when.</summary>
        public TownEnd end;
        public int endedAt = -1;

        /// <summary>The town still stands as the people's: the settlement there, of the people it became.</summary>
        public bool Standing
        {
            get
            {
                if (people == null || people.defeated) return false;
                var s = Town;
                return s != null && !s.Destroyed && s.Faction == people;
            }
        }

        public Settlement Town
        {
            get
            {
                var list = Find.WorldObjects?.Settlements;
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].ID == key)
                        return list[i];
                return null;
            }
        }

        /// <summary>One of the colony's former people heads the town now (the bond's +30, else +15).</summary>
        public bool FormerHeads => people?.leader != null && formers.Contains(people.leader);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                formers.RemoveAll(p => p == null || p.Discarded);
                if (second != null && second.Discarded) second = null;
                // A people the game let go (taken back, its last town gone): no reference left that can't be found on load.
                if (people != null && Find.FactionManager != null && !Find.FactionManager.AllFactionsListForReading.Contains(people)) people = null;
            }
            Scribe_Values.Look(ref key, "key", HomesMath.None);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref colonyName, "colonyName");
            Scribe_Values.Look(ref age, "age");
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Values.Look(ref handedAt, "handedAt", -1);
            Scribe_Values.Look(ref terms, "terms");
            Scribe_References.Look(ref second, "second");
            Scribe_Collections.Look(ref formers, "formers", LookMode.Reference);
            Scribe_Values.Look(ref wentTo, "wentTo", HomesMath.None);
            Scribe_Values.Look(ref bondEnded, "bondEnded");
            Scribe_Values.Look(ref bondEndedAt, "bondEndedAt", -1);
            Scribe_Deep.Look(ref layout, "layout");
            Scribe_Values.Look(ref seed, "seed", -1f);
            Scribe_Values.Look(ref lastLetter, "lastLetter", -1);
            Scribe_Values.Look(ref lastHomecoming, "lastHomecoming", -1);
            Scribe_Values.Look(ref lastInvite, "lastInvite", -1);
            Scribe_Values.Look(ref askedAt, "askedAt", -1);
            Scribe_Values.Look(ref agreed, "agreed");
            Scribe_Values.Look(ref agreedAt, "agreedAt", -1);
            Scribe_Values.Look(ref agreedTold, "agreedTold");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref endedAt, "endedAt", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                formers ??= new List<Pawn>();
                formers.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>The towns the colony was (<see cref="LedgerFormer"/>), six at most: past that the oldest whose town is gone is let go (the history keeps its milestone).</summary>
    public partial class WorldLedger
    {
        private List<LedgerFormer> formerRecords = new List<LedgerFormer>();

        /// <summary>Every town the colony was, in the order they were handed over.</summary>
        public static IReadOnlyList<LedgerFormer> Formers => (IReadOnlyList<LedgerFormer>)Instance?.formerRecords ?? Array.Empty<LedgerFormer>();

        /// <summary>A town the colony was, by its settlement's id (the colony's key), or null.</summary>
        public static LedgerFormer FormerOf(int key)
        {
            var l = Instance;
            if (l == null || key == HomesMath.None) return null;
            for (int i = 0; i < l.formerRecords.Count; i++)
                if (l.formerRecords[i] != null && l.formerRecords[i].key == key)
                    return l.formerRecords[i];
            return null;
        }

        /// <summary>The town a people is (a people of the colony's former people), or null. Asked by the bond every 1000 ticks: a look over six at most.</summary>
        public static LedgerFormer FormerOf(Faction people)
        {
            var l = Instance;
            if (l == null || people == null) return null;
            for (int i = 0; i < l.formerRecords.Count; i++)
                if (l.formerRecords[i] != null && l.formerRecords[i].people == people)
                    return l.formerRecords[i];
            return null;
        }

        /// <summary>A town the colony was, written down at its hand-over.</summary>
        public static void AddFormer(LedgerFormer r)
        {
            var l = Instance;
            if (l == null || r == null || r.key == HomesMath.None || FormerOf(r.key) != null) return;
            l.formerRecords.Add(r);
            int i;
            while ((i = HandOverMath.RecordToDrop(l.formerRecords.Select(f => f.Standing).ToList(), l.formerRecords.Select(f => f.handedAt).ToList())) >= 0)
                l.formerRecords.RemoveAt(i);
            Changed();
        }

        /// <summary>Something on a record changed (its second, the bond).</summary>
        public static void FormerChanged() => Changed();

        private void ExposeFormers()
        {
            // A save with no town the colony was writes nothing here.
            if (Scribe.mode != LoadSaveMode.Saving || formerRecords.Count > 0) Scribe_Collections.Look(ref formerRecords, "formers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                formerRecords ??= new List<LedgerFormer>();
                formerRecords.RemoveAll(f => f == null || f.key == HomesMath.None);
            }
        }
    }
}
