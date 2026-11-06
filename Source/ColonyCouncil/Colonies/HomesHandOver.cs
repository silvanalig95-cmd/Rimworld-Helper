using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colony being handed over (Colonies/HandOvers.cs): the home, who stays and who goes, the head and the head's
    /// second, the people's name, the terms, the colony's prisoners and slaves, where those who go go, the meeting's
    /// vote, the day set, and how far it has come. One at a time. A fresh start in the same act keeps its founding here
    /// until those who go set out after the ceremony.
    /// </summary>
    public class HandOverPlan : IExposable
    {
        public int id;
        /// <summary>The home handed over (its key), and its name as it was.</summary>
        public int home = HomesMath.None;
        public string homeName;
        /// <summary>The colony's age when the window was confirmed: the people's kind and words.</summary>
        public WorldAge age;
        public HandOverStage stage;
        public int started = -1;
        public string peopleName;
        public HandOverTerms terms;
        /// <summary>The head and the head's second, as the window chose them (looked at again when the town is made).</summary>
        public Pawn head, second;
        /// <summary>The home's people who go, and who stay (grown-ups and children).</summary>
        public List<Pawn> going = new List<Pawn>();
        public List<Pawn> staying = new List<Pawn>();
        /// <summary>Those sent or kept against their wish: each feels uprooted for a season once the town is made.</summary>
        public List<Pawn> sentAgainst = new List<Pawn>();
        public List<Pawn> keptAgainst = new List<Pawn>();
        public HandOverWhere where;
        /// <summary>The home those who go go to (where = Home).</summary>
        public int toHome = HomesMath.None;
        public CaptiveFate captives;
        /// <summary>A fresh start in the same act: its founding, set out once the ceremony is over (then <see cref="foundingId"/>).</summary>
        public FoundingPlan founding;
        public int foundingId = -1;
        /// <summary>The meeting's vote: how many for and against, and whether the player went ahead against it.</summary>
        public int voteFor, voteAgainst;
        public bool voted, wentAgainst;
        /// <summary>The day the ceremony is set for (its first tick), or -1.</summary>
        public int dayAt = -1;
        /// <summary>The play-out of the meeting or the ceremony on the map, or -1.</summary>
        public int play = -1;
        /// <summary>The banner, totem, keys or codes changed hands at the ceremony (said so in the letter only then).</summary>
        public bool tokenGiven;
        /// <summary>What the town waits for, told once (CC_HandOver_Wait_*).</summary>
        public string waitTold;
        /// <summary>The journey that takes those who go to another home, by its id, or -1.</summary>
        public int journey = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref homeName, "homeName");
            Scribe_Values.Look(ref age, "age");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref peopleName, "peopleName");
            Scribe_Values.Look(ref terms, "terms");
            Scribe_References.Look(ref head, "head");
            Scribe_References.Look(ref second, "second");
            Scribe_Collections.Look(ref going, "going", LookMode.Reference);
            Scribe_Collections.Look(ref staying, "staying", LookMode.Reference);
            Scribe_Collections.Look(ref sentAgainst, "sentAgainst", LookMode.Reference);
            Scribe_Collections.Look(ref keptAgainst, "keptAgainst", LookMode.Reference);
            Scribe_Values.Look(ref where, "where");
            Scribe_Values.Look(ref toHome, "toHome", HomesMath.None);
            Scribe_Values.Look(ref captives, "captives");
            Scribe_Deep.Look(ref founding, "founding");
            Scribe_Values.Look(ref foundingId, "foundingId", -1);
            Scribe_Values.Look(ref voteFor, "voteFor");
            Scribe_Values.Look(ref voteAgainst, "voteAgainst");
            Scribe_Values.Look(ref voted, "voted");
            Scribe_Values.Look(ref wentAgainst, "wentAgainst");
            Scribe_Values.Look(ref dayAt, "dayAt", -1);
            Scribe_Values.Look(ref play, "play", -1);
            Scribe_Values.Look(ref tokenGiven, "tokenGiven");
            Scribe_Values.Look(ref waitTold, "waitTold");
            Scribe_Values.Look(ref journey, "journey", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                going ??= new List<Pawn>();
                going.RemoveAll(p => p == null);
                staying ??= new List<Pawn>();
                staying.RemoveAll(p => p == null);
                sentAgainst ??= new List<Pawn>();
                sentAgainst.RemoveAll(p => p == null);
                keptAgainst ??= new List<Pawn>();
                keptAgainst.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>A hand-over under way, on the homes' own component (Lean/Homes.cs). A save with none writes nothing.</summary>
    public partial class Homes
    {
        public List<HandOverPlan> handOvers = new List<HandOverPlan>();
        public int nextHandOver = 1;

        private void ExposeHandOver()
        {
            if (Scribe.mode == LoadSaveMode.Saving) handOvers.RemoveAll(h => h == null);
            if (Scribe.mode != LoadSaveMode.Saving || handOvers.Count > 0) Scribe_Collections.Look(ref handOvers, "handOvers", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.Saving || nextHandOver != 1) Scribe_Values.Look(ref nextHandOver, "nextHandOver", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                handOvers ??= new List<HandOverPlan>();
                handOvers.RemoveAll(h => h == null || h.home == HomesMath.None);
            }
        }
    }
}
