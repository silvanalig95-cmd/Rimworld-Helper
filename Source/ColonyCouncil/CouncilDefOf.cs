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
        public static ThoughtDef Council_FriendQuit;
        public static ThoughtDef Council_DecreeHeeded;
        public static ThoughtDef Council_Anniversary;
        public static ThoughtDef Council_FriendAnniversary;
        public static ThoughtDef Council_Epithet;
        public static ThoughtDef Council_EpithetDubious;
        public static ThoughtDef Council_EpithetLost;
        public static ThoughtDef Council_TraditionKept;
        public static ThoughtDef Council_TraditionBroken;
        public static ThoughtDef Council_ElectedToOffice;
        public static ThoughtDef Council_LostOffice;
        public static ThoughtDef Council_LostElection;
        public static ThoughtDef Council_ProudMentor;
        public static ThoughtDef Council_GratefulStudent;
        public static ThoughtDef Council_Soc_LeftOut;
        public static ThoughtDef Council_Dream_Sweet;
        public static ThoughtDef Council_Dream_Strange;
        public static ThoughtDef Council_Dream_Nightmare;
        public static ThoughtDef Council_Dream_Omen;
        public static ThoughtDef Council_Dream_Lost;
        public static ThoughtDef Council_Dream_CameTrue;
        public static ThoughtDef Council_Dream_Fever;
        public static ThoughtDef Council_Asp_Fulfilled;

        public static HediffDef Council_Motivated;
        public static HediffDef Council_Loyalty;
        public static HediffDef Council_Grudge;
        public static HediffDef Council_Disgruntled;

        public static MainButtonDef Council_MainButton;
        public static MainButtonDef Council_RoomsButton;

        static CouncilDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CouncilDefOf));
        }
    }
}
