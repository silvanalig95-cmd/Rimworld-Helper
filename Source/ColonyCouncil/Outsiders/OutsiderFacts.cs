using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What is true right now for a talk between a colonist and someone from outside the colony, read from the game each
    /// time two are about to talk (OutsiderTalk.cs): the facts service's conversation scene (<see cref="FactScene.Talk"/>,
    /// Text/Facts/). Its providers read the hour, the season and the weather; who the outsider is and their people (their
    /// age, the Empire's titles, their leader); their town and its people by name, the land and the cold back home,
    /// places and work near it, what they're short of; their people's wars and peace, the world's events running or just
    /// over, the conditions over the land; the colony's own news, its hospital and its trusted doctor; what their people
    /// remember the colony for; what passed between the two before (callbacks, with what became of it since); the ties
    /// between the two sides and what the world's history holds of their town and of them lately. Nothing is claimed
    /// that isn't so: a fact the game can't give is simply left out, and a piece that needs it isn't said.
    /// </summary>
    public static class OutsiderFacts
    {
        public static FactSet For(Pawn c, Pawn x, TalkTone tone) => c == null || x == null ? new FactSet() : Facts.For(FactScene.Talk(c, x, tone));
    }
}
