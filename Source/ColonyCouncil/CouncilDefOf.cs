using RimWorld;
using Verse;

namespace ColonyCouncil
{
    [DefOf]
    public static class CouncilDefOf
    {
        public static ThoughtDef Council_HelpedSomeone;
        public static ThoughtDef Council_SpokeUpForMe;
        public static ThoughtDef Council_WishIgnored;
        public static ThoughtDef Council_AmbitionAchieved;
        public static ThoughtDef Council_MyAmbitionAchieved;
        public static ThoughtDef Council_AmbitionFailed;
        public static ThoughtDef Council_PersonalTouchDone;
        public static ThoughtDef Council_HeardAtMeeting;

        public static MainButtonDef Council_MainButton;

        static CouncilDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CouncilDefOf));
        }
    }
}
