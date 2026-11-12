namespace ColonyCouncil
{
    /// <summary>
    /// A way's levers: a factor on each kind of Rimfolk's happenings (StoryWaysMath has what each one moves and where), 1
    /// for as it is. Written in the way's StorytellerDef (Defs/StorytellerDefs/Rimfolk_Storytellers.xml, its StoryWayExtension),
    /// read through StoryDirection.Factor. No game types, so the tests read it too.
    /// </summary>
    public class StoryLevers
    {
        public float visitors = 1f;
        public float challenges = 1f;
        public float pasts = 1f;
        public float nature = 1f;
        public float fights = 1f;
        public float camps = 1f;
        public float traders = 1f;
        public float letters = 1f;
        public float favours = 1f;
        public float missions = 1f;
        public float situations = 1f;
        public float gods = 1f;
        public float arcs = 1f;
        public float memory = 1f;
        public float allies = 1f;
        public float schemes = 1f;
        public float raidReasons;

        /// <summary>A lever by its key (StoryWaysMath.Keys); 1 for a key it doesn't know.</summary>
        public float Get(string key)
        {
            switch (key)
            {
                case StoryWaysMath.Visitors: return visitors;
                case StoryWaysMath.Challenges: return challenges;
                case StoryWaysMath.Pasts: return pasts;
                case StoryWaysMath.Nature: return nature;
                case StoryWaysMath.Fights: return fights;
                case StoryWaysMath.Camps: return camps;
                case StoryWaysMath.Traders: return traders;
                case StoryWaysMath.Letters: return letters;
                case StoryWaysMath.Favours: return favours;
                case StoryWaysMath.Missions: return missions;
                case StoryWaysMath.Situations: return situations;
                case StoryWaysMath.Gods: return gods;
                case StoryWaysMath.Arcs: return arcs;
                case StoryWaysMath.Memory: return memory;
                case StoryWaysMath.Allies: return allies;
                case StoryWaysMath.Schemes: return schemes;
                case StoryWaysMath.RaidReasons: return raidReasons;
                default: return 1f;
            }
        }
    }
}
