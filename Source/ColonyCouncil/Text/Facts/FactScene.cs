using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a text is written for (DESIGN.md, "Facts"): who speaks or writes (one of the colony), to whom (someone out in
    /// the world), at which colony, their town and their people, how it goes (a tone, the tag tone:X), and which providers
    /// it asks, in order. A text system makes one and hands it to <see cref="Facts.For"/>.
    /// </summary>
    public sealed class FactScene
    {
        /// <summary>The one of the colony who speaks or writes, or null.</summary>
        public Pawn speaker;
        /// <summary>The one spoken or written to, out in the world, or null.</summary>
        public Pawn listener;
        /// <summary>The home the scene is at (the speaker's map in a talk, the home a letter goes from), or null.</summary>
        public Map colony;
        /// <summary>The listener's town, or null.</summary>
        public Settlement place;
        /// <summary>The listener's people, or null.</summary>
        public Faction people;
        /// <summary>How it goes ("warm", "plain", "cool"...): the tag tone:X. Null for none.</summary>
        public string tone;
        /// <summary>The providers asked, in order (<see cref="FactsMath"/>'s names).</summary>
        public IList<string> asks = System.Array.Empty<string>();

        /// <summary>
        /// A conversation between a colonist and someone from outside the colony (Outsiders/OutsiderTalk.cs): the
        /// colonist's map, the outsider's town and people as the outsiders know them, the talk's tone.
        /// </summary>
        public static FactScene Talk(Pawn c, Pawn x, TalkTone tone) => new FactScene
        {
            speaker = c, listener = x, colony = c?.MapHeld, people = Outsiders.PeopleOf(x), place = Outsiders.TownOf(x), tone = tone.ToString().ToLowerInvariant(),
            asks = FactsMath.Talk
        };

        /// <summary>
        /// One of Rimfolk's events about a people (Happenings/IncidentWorker_Happening.cs: its letter's lines): at a home,
        /// their people and their town (the one the event names, else their nearest), nobody speaking. What their people
        /// remember of the colony, the ties, their town lately, the region's word on it.
        /// </summary>
        public static FactScene Event(Map home, Faction people, Settlement town, Pawn stranger = null) => new FactScene
        {
            colony = home, people = people, place = town, listener = stranger, asks = FactsMath.Event
        };

        /// <summary>A letter or a call to a neighbour (World/NeighbourLetters.cs), with the world's news near them when it tells some.</summary>
        public static FactScene Letter(Notable n, Pawn writer, Map home, bool news) => new FactScene
        {
            speaker = writer, listener = n?.pawn, colony = home, people = n?.faction, place = n != null ? NotableBook.TownOf(n.town) : null,
            asks = FactsMath.Letter(news)
        };
    }
}
