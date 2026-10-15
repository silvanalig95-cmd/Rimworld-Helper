using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One more kind of ordinary day for the everyday journal (<see cref="EverydayJournal"/>), written in XML: when one
    /// of <see cref="thoughts"/> is on a colonist's mind today, the day's entry may come from the pool
    /// <c>Council_Journal_Day_{key}</c>. Made for other mods' memories (Medieval Overhaul's cheese and wine, a talk about
    /// magic): thoughts that don't exist in this game are never on anyone's mind, so without the mod nothing happens.
    /// </summary>
    public class JournalDayDef : Def
    {
        /// <summary>The pool's name after "Council_Journal_Day_". Defaults to the defName.</summary>
        public string key;
        /// <summary>ThoughtDef names: any of them brings the day up.</summary>
        public List<string> thoughts = new List<string>();
        public float weight = 1f;
        /// <summary>Only when it weighs on them (a bad memory, not a good one).</summary>
        public bool badOnly;
        /// <summary>The lines name the other person ({OTHER_...}): only for memories about someone.</summary>
        public bool social;

        public string Key => key.NullOrEmpty() ? defName : key;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (thoughts.NullOrEmpty()) yield return "journal day has no thoughts";
        }
    }
}
