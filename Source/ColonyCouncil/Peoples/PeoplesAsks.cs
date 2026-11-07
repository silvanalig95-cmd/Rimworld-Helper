using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Which people a mission comes from, on its IncidentDef: only one of Rimfolk's peoples of a calling (the order's ask
    /// for medicine, the league's escort), or one of a calling likelier (a healer lent to a sickhouse, fodder for a
    /// herd), and only while a kind of the region's world events runs (a plague year). The mission's own worker does the
    /// rest, the same as for any people.
    /// </summary>
    public class MissionPeople : DefModExtension
    {
        /// <summary>Only from a people of this calling (Peoples/: traders, healers, herders).</summary>
        public string only;
        /// <summary>From a people of this calling five times as often as from another.</summary>
        public string prefers;
        /// <summary>Only while one of the region's world events of this kind runs ("sickness": a plague year).</summary>
        public string whileKind;

        /// <summary>Its season of need, and a people of its calling at peace with the colony.</summary>
        public bool Possible => Peoples.Running(whileKind) && (only.NullOrEmpty() || Peoples.AnyAtPeace(only));

        /// <summary>Whether a mission with this on it is weighed in the missions' one draw now (MissionPool): never while it couldn't come.</summary>
        public static bool Weighed(IncidentDef def)
        {
            var who = def?.GetModExtension<MissionPeople>();
            return who == null || who.only.NullOrEmpty() || Peoples.AnyAtPeace(who.only);
        }

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!only.NullOrEmpty() && !PeoplesMath.IsCalling(only)) yield return "'" + only + "' is not a calling";
            if (!prefers.NullOrEmpty() && !PeoplesMath.IsCalling(prefers)) yield return "'" + prefers + "' is not a calling";
        }

        /// <summary>The calling a mission asks for while it is weighed or offered (MissionPeoples.OnlyCalling and PreferCalling), put back after.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly string only, prefers;

            public Scope(MissionPeople who)
            {
                only = MissionPeoples.OnlyCalling;
                prefers = MissionPeoples.PreferCalling;
                MissionPeoples.OnlyCalling = who?.only;
                MissionPeoples.PreferCalling = who?.prefers;
            }

            public void Dispose()
            {
                MissionPeoples.OnlyCalling = only;
                MissionPeoples.PreferCalling = prefers;
            }
        }
    }
}
