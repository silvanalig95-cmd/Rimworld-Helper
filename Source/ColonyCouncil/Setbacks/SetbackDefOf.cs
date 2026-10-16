using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The setbacks' memories given from code (Defs/ThoughtDefs/Council_Thoughts_Setbacks*.xml).</summary>
    [DefOf]
    public static class SetbackDefOf
    {
        // The council
        public static ThoughtDef Council_Setback_IdeaPassedOver;
        public static ThoughtDef Council_Setback_OutVoted;
        public static ThoughtDef Council_Setback_FriendLetDown;
        public static ThoughtDef Council_Setback_RivalHonoured;
        public static ThoughtDef Council_Setback_NotAgreed;
        public static ThoughtDef Council_Setback_ToldThemSo;

        // Gatherings
        public static ThoughtDef Council_Setback_MissedGathering;
        public static ThoughtDef Council_Setback_NoPlate;
        public static ThoughtDef Council_Setback_RainedOut;

        // Between people
        public static ThoughtDef Council_Setback_Outshone;
        public static ThoughtDef Council_Setback_DriftedApart;
        public static ThoughtDef Council_Setback_NoisyNeighbour;

        // Work
        public static ThoughtDef Council_Setback_WorkTornDown;
        public static ThoughtDef Council_Setback_WorkWrecked;
        public static ThoughtDef Council_Setback_CropLost;
        public static ThoughtDef Council_Setback_BotchedBuild;
        public static ThoughtDef Council_Setback_ShoddyWork;
        public static ThoughtDef Council_Setback_MakingDo;
        public static ThoughtDef Council_Setback_BotchedSurgery;
        public static ThoughtDef Council_Setback_LongHauls;
        public static ThoughtDef Council_Setback_LeftWaiting;

        // Home
        public static ThoughtDef Council_Setback_ColdMeal;
        public static ThoughtDef Council_Setback_ArtSold;
        public static ThoughtDef Council_Setback_AnimalSold;

        // Worries
        public static ThoughtDef Council_Setback_Homesick;

        static SetbackDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SetbackDefOf));
    }
}
