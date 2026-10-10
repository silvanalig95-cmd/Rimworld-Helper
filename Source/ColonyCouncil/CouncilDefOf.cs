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
        public static ThoughtDef Council_Refused;
        public static ThoughtDef Council_TookMatters;
        public static ThoughtDef Council_PromiseKept;
        public static ThoughtDef Council_BrokenPromise;
        public static ThoughtDef Council_StoodTogether;
        public static ThoughtDef Council_Favoritism;
        public static ThoughtDef Council_HappyForThem;
        public static ThoughtDef Council_ObjectionIgnored;
        public static ThoughtDef Council_ObjectionHeard;

        public static HediffDef Council_Motivated;
        public static HediffDef Council_Disgruntled;

        public static MainButtonDef Council_MainButton;

        static CouncilDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CouncilDefOf));
        }
    }
}
