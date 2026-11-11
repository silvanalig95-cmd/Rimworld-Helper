using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp kind with ways of its own (CampKindDef.workerClass), for the peoples' own camps (Peoples/Events/: the pilgrims'
    /// hostel, the Wardens' lantern post, the scholars at an old place) and the rounds after them: the land it needs and
    /// where it pitches, its hour, its people's day and night work, what it says when it goes, and the asks only it can
    /// answer. Every hook is called beside the land kinds' own (CampKindsLand.cs) and does nothing unless overridden; a
    /// camp's own state between hours is its <see cref="CampWorkState"/> (Camp.workState), saved with the camp.
    /// </summary>
    public class CampWorker
    {
        public CampKindDef def;

        /// <summary>The land and the world have what they come for (beside CampKindDef.needs).</summary>
        public virtual bool FitsMap(Map map) => true;

        /// <summary>Where they pitch, from the usual spot (Invalid: nowhere fits).</summary>
        public virtual IntVec3 SiteFor(Map map, IntVec3 usual, bool shade) => usual;

        /// <summary>The camp has set out for its spot: its own state made, what it brings set down.</summary>
        public virtual void Started(Camp c)
        {
        }

        /// <summary>Their hour while they live here (settled, not hostile).</summary>
        public virtual void Hourly(Camp c, int hour)
        {
        }

        /// <summary>One of theirs' work by day, or null for the camp's usual day.</summary>
        public virtual Job DayJob(Camp c, Pawn p) => null;

        /// <summary>One of theirs' work by night, or null for sleep.</summary>
        public virtual Job NightJob(Camp c, Pawn p, int hour) => null;

        /// <summary>They are going: what is told, anything of theirs called back.</summary>
        public virtual void Leaving(Camp c)
        {
        }

        /// <summary>Marks for their farewell's lines ([fell], [measured]...): a line with the mark is said first.</summary>
        public virtual void AddMarks(Camp c, HashSet<string> marks)
        {
        }

        /// <summary>Their own placeholders filled in a camp text ({BEAST}, {READING}).</summary>
        public virtual string Fill(Camp c, string text) => text;

        /// <summary>Why one of their own asks can't be made now (its <see cref="CampAsk.needs"/>), or null.</summary>
        public virtual string AskBlocked(Camp c, CampAsk a) => null;
    }

    /// <summary>What a camp with ways of its own keeps between its hours (CampWorker), saved with the camp.</summary>
    public class CampWorkState : IExposable
    {
        public virtual void ExposeData()
        {
        }
    }
}

namespace ColonyCouncil
{
    public partial class Camp
    {
        /// <summary>What a kind with ways of its own keeps between its hours (CampKindDef.workerClass), saved with the camp.</summary>
        public CampWorkState workState;
    }
}
