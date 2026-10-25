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
        public static ThoughtDef Council_HeardAtMeetingLate;
        public static ThoughtDef Council_MissedMeeting;
        public static ThoughtDef Council_DecidedWithoutMe;
        public static ThoughtDef Council_WokenForMeeting;
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
        public static ThoughtDef Council_Secret_Unburdened;
        public static ThoughtDef Council_Secret_Trusted;
        public static ThoughtDef Council_Secret_Exposed;
        public static ThoughtDef Council_Secret_Confessed;
        public static ThoughtDef Council_Secret_Betrayed;
        public static ThoughtDef Council_Vendetta_Blood;
        public static ThoughtDef Council_Vendetta_Avenged;
        public static ThoughtDef Council_Vendetta_Betrayed;
        public static ThoughtDef Council_GaveEulogy;
        public static ThoughtDef Council_EulogyHeard;
        public static HediffDef Council_Vendetta_Fury;
        public static ThoughtDef Council_HeirloomLost;
        public static ThoughtDef Council_Nemesis_Escaped;
        public static ThoughtDef Council_Nemesis_Dread;
        public static ThoughtDef Council_Nemesis_Defiant;
        public static ThoughtDef Council_Nemesis_Captured;
        public static ThoughtDef Council_Nemesis_Revenge;
        public static ThoughtDef Council_Nemesis_Avenged;
        public static ThoughtDef Council_Nemesis_Slayer;
        public static ThoughtDef Council_Pet_Defended;
        public static ThoughtDef Council_Pet_Lost;
        public static ThoughtDef Council_Decree_Again;
        public static ThoughtDef Council_HomeAgain;
        public static ThoughtDef Council_GladTheyreBack;
        public static ThoughtDef Council_ChildGrown;
        public static ThoughtDef Council_SawThemGrow;

        // World events
        public static ThoughtDef Council_Evt_PartnersEx;
        public static ThoughtDef Council_Evt_WelcomeBack;
        public static ThoughtDef Council_Evt_UnwelcomeBack;
        public static ThoughtDef Council_Evt_WonChallenge;
        public static ThoughtDef Council_Evt_LostChallenge;
        public static ThoughtDef Council_Evt_ChampionWon;
        public static ThoughtDef Council_Evt_FelledBeast;
        public static ThoughtDef Council_Evt_BeastDown;
        public static ThoughtDef Council_Evt_TamedBeast;
        public static ThoughtDef Council_Evt_Fireflies;
        public static ThoughtDef Council_Evt_Hail;
        public static ThoughtDef Council_Evt_PetCaughtVermin;
        public static ThoughtDef Council_Evt_HeardTales;
        public static ThoughtDef Council_Evt_DeadHonored;
        public static ThoughtDef Council_Evt_TreasureFound;
        public static IncidentCategoryDef Council_WorldEvent;
        public static LetterDef Council_EventLetter;
        public static LetterDef Council_EventLetterThreat;
        public static HediffDef Council_Legendary;
        public static GameConditionDef Council_Comet;
        public static ThoughtDef Council_Victory;
        public static ThoughtDef Council_VictoryCostly;
        public static ThoughtDef Council_BattleHero;
        public static ThoughtDef Council_QuestDone;
        public static ThoughtDef Council_Evt_ChildCameBack;
        public static ThoughtDef Council_Evt_GoodPress;
        public static ThoughtDef Council_Evt_BadPress;
        public static HediffDef Council_Inoculated;
        public static HediffDef Council_HerbalTonic;
        public static IncidentDef Council_Evt_DebtDue;
        public static IncidentDef Council_Evt_DeserterFound;
        public static ThoughtDef Council_Scen_Kin;
        public static ThoughtDef Council_Scen_Feud;
        public static ThoughtDef Council_Scen_Tyrant;
        public static ThoughtDef Council_Scen_HuntOver;
        public static ThoughtDef Council_Scen_DebtPaid;
        public static ThoughtDef Council_Scen_OldFriend;

        public static HediffDef Council_Motivated;
        public static HediffDef Council_Loyalty;
        public static HediffDef Council_Grudge;
        public static HediffDef Council_Disgruntled;

        public static MainButtonDef Council_MainButton;
        public static MainButtonDef Council_RoomsButton;
        public static MainButtonDef Council_DefenseButton;

        static CouncilDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CouncilDefOf));
        }
    }
}
