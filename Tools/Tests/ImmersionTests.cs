using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// What colonists say about themselves and the weather, and the small habits (Source/ColonyCouncil/Immersion/): every
/// line said over a head is one sentence of 90 characters at most and doesn't start with a quote (the floating text
/// keeps only the first sentence and trims quotes from the ends). That goes for the pools below, the pools the defs
/// name for speech (a habit's, a group habit's, an answer's gathering or walk, an objection's), the lines the defs give
/// to be said (an event's, an answer's play-out or errand, a camp's) and the game's own words said in their place;
/// every tag naming a feeling, an ailment, a setback or a personal story names one that exists, so a typo can't leave
/// a line unsaid; every pool the code and the habits'
/// jobs name exists, and each of the twelve habits has a job of its own. How many feelings, ailments, setbacks and stories have lines of their own is reported, not
/// required: the pools' untagged lines cover the rest, and a feeling without lines is simply not said.
/// </summary>
public static class ImmersionTests
{
    private static readonly string[] SpokenPools =
    {
        "Council_Pool_SayFeeling", "Council_Pool_SayAilment", "Council_Pool_SayConvalescing", "Council_Pool_SayStory", "Council_Pool_SayTomorrow",
        "Council_Pool_SaySetback", "Council_Pool_SayTakenBack", "Council_Pool_SayNemesis", "Council_Pool_SayWish", "Council_Pool_SayFirstSnow",
        "Council_Pool_SayHeatWave", "Council_Pool_SayColdSnap", "Council_Pool_SayStorm", "Council_Pool_HabitGrave", "Council_Pool_HabitLookOut",
        "Council_Pool_HabitFire", "Council_Pool_HabitRemember", "Council_Pool_HabitStars", "Council_Pool_HabitPlants", "Council_Pool_HabitAnimals",
        "Council_Pool_HabitStores", "Council_Pool_HabitCompany", "Council_Pool_HabitStories", "Council_Pool_SayDream", "Council_Pool_TouchCampComing",
        "Council_Pool_TouchCouncilWedding", "Council_Pool_GuestSays", "Council_Pool_VisitorNotices", "Council_Pool_BeastSighting", "Council_Pool_Herd_SayLeader", "Council_Pool_RoadWord", "Council_Pool_SayFamilyYears", "Council_Pool_SayReunion", "Council_Pool_SayKinRaider",
        // The family's life together (Social/FamilyLife.cs).
        "Council_Pool_SayNewCouple", "Council_Pool_SayMatchDoubt", "Council_Pool_SayParentNewPartner", "Council_Pool_SayEngaged", "Council_Pool_SayProposalComfort", "Council_Pool_SayExpecting",
        "Council_Pool_SayGrandchild", "Council_Pool_PlayGrowth",
        // The family's days together (Social/FamilyBonds.cs).
        "Council_Pool_PlayFamilySupper", "Council_Pool_SaySiblingClose", "Council_Pool_SaySiblingRival",
        // Habits shown on the map (Immersion/QuirkHabits.cs, HabitBubbles.cs).
        "Council_Pool_HabitTidy", "Council_Pool_HabitSunrise", "Council_Pool_HabitStones", "Council_Pool_HabitBirds", "Council_Pool_HabitWhittle",
        "Council_Pool_HabitWhetstone", "Council_Pool_HabitTreeLine", "Council_Pool_HabitPatient", "Council_Pool_HabitBirthday", "Rimfolk_Pool_HabitFirstPot",
        "Council_Pool_BubbleHums", "Council_Pool_BubbleStove", "Council_Pool_BubbleSleepTalk", "Council_Pool_BubbleBestBite", "Council_Pool_BubbleMeasure",
        "Council_Pool_BubbleHaul", "Council_Pool_BubbleLateHours", "Council_Pool_BubbleFidgets", "Council_Pool_BubbleReads", "Council_Pool_BubbleWarden",
        "Council_Pool_BubbleMends", "Council_Pool_BubbleDoodles", "Council_Pool_BubbleLightSleep",
        // The quirks the colony's events leave behind (Quirks/QuirkEvents.cs).
        "Council_Pool_BubbleLamp", "Council_Pool_BubbleSmoke", "Council_Pool_BubbleCoins", "Council_Pool_BubbleFields", "Council_Pool_BubbleDreamBook",
        "Council_Pool_BubbleSalve", "Council_Pool_BubbleJuggles", "Council_Pool_BubbleColdTrick",
        // The quirks a hurt, an ailment, a founding, a parting and a camp's evening leave (People4).
        "Rimfolk_Pool_BubbleFooting", "Rimfolk_Pool_BubbleTapsRoof", "Rimfolk_Pool_BubbleBoots", "Rimfolk_Pool_BubbleBoils", "Rimfolk_Pool_BubbleFirstFire",
        "Rimfolk_Pool_BubbleLooksBack", "Rimfolk_Pool_BubbleRoadSong", "Rimfolk_Pool_BubbleDusk",
        // Visitors and campers at the colony's market stalls (Visitors/VisitorTrade.cs).
        "Rimfolk_Pool_MarketBuy",
        // Visitors at the colony's instrument, their kill, its dressing (Visitors/VisitorActs.cs).
        "Rimfolk_VisitorActLines",
        // What visitors say on arriving, waiting, staying and leaving (Visitors/VisitorWords.cs).
        "Rimfolk_VisitorLines",
        // What visitors say at the relic, the anima tree, the gravship and the bookcase (Visitors/VisitorsMore2.cs).
        "Rimfolk_VisitorLines2",
        // What callers say at their work, beside their event's own lines (Visitors/Callers.cs).
        "Rimfolk_Pool_CallerSays",
        // A drink raised or a joint lit before work for Alpha Skills' drunken and stoned passions (Integrations/BeforeWork.cs).
        "Rimfolk_Pool_DrinkBeforeWork", "Rimfolk_Pool_SmokeBeforeWork",
        // A colonist setting off to carry a lighter case to another bed for someone worse off, or for an operation
        // (Integrations/MedicalTriage.cs, with Move the Patient).
        "Rimfolk_Pool_TriageRoom", "Rimfolk_Pool_TriageSurgery",
        // Hospitality's guests helping out (Integrations/GuestHelp.cs): why they take up a kind of work, pitching in to the
        // colony's trouble, a day's work in thanks, and putting work down.
        "Rimfolk_Pool_GuestHelpFit", "Rimfolk_Pool_GuestHelpCrisis", "Rimfolk_Pool_GuestHelpThanks", "Rimfolk_Pool_GuestHelpStop",
        // Writing to a neighbour, handing the letter over, and calls on the comms console (World/NeighbourLetters.cs).
        "Rimfolk_Pool_NeighbourSays",
        // The post: carriers taking and bringing the mail, the post rider, spoken messages, the colony's own rider (Post/PostWords.cs).
        "Rimfolk_Pool_PostSays",
        // A colonist asking for a clearly better weapon lying in storage (Weapons/WeaponAsks.cs).
        "Rimfolk_Pool_WeaponAsk",
        // A colonist proposing a trade of weapons to another (Weapons/WeaponAsks.cs).
        "Rimfolk_Pool_WeaponTrade",
        // A colonist taking a weapon to fill an empty slot of their kit themselves (Weapons/WeaponKitSelf.cs).
        "Rimfolk_Pool_WeaponKitTake",
        // A colonist asking for better clothes (Apparel/ApparelAsks.cs).
        "Rimfolk_Pool_ApparelAsk",
        // A colonist and someone from outside the colony doing something together (Outsiders/Outsiders.cs).
        "Rimfolk_Pool_OutsiderSays",
        // Hurts from everyday life (Harm/): the evening drill's call, start, real blow, apology and end; a slip at work, a
        // strain worked through, a doctor's word.
        "Rimfolk_Pool_DrillCall", "Rimfolk_Pool_DrillBegin", "Rimfolk_Pool_DrillHit", "Rimfolk_Pool_DrillSorry", "Rimfolk_Pool_DrillEnd",
        "Rimfolk_Pool_WorkSlip", "Rimfolk_Pool_WorkedThrough", "Rimfolk_Pool_DoctorsWord",
        // Harm, parts 2 and 3: a camp's quarrel and those who join it, the champion's bout, a hunt's game turning, sickness
        // brought home, a rival's sabotage seen, told, passed on and had out, the hustings' shove and the call to order.
        "Rimfolk_Pool_CampQuarrel", "Rimfolk_Pool_CampBrawlJoin", "Rimfolk_Pool_CampEvening", "Rimfolk_Pool_CampWork", "Rimfolk_Pool_ChampionBegin", "Rimfolk_Pool_ChampionSorry", "Rimfolk_Pool_ChampionSneer",
        "Rimfolk_Pool_ChampionTook", "Rimfolk_Pool_GameTurns", "Rimfolk_Pool_SickHome", "Rimfolk_Pool_SabotageSeen", "Rimfolk_Pool_SabotageTold",
        "Rimfolk_Pool_SabotageWord", "Rimfolk_Pool_SabotageConfront", "Rimfolk_Pool_HustingsShove", "Rimfolk_Pool_HustingsOrder",
        // Harm, part 4: a sickness caught, a coughing guest, a cold night and the doctor's word, a hailstone, a felling, a build
        // come down, a burn at the bench, the rut, a lesson's kick and the herdmaster's calm, a rider thrown, the groups' brawl,
        // a sore loser and the winner, a gracious winner, a rematch asked and answered, a games day's contests and race.
        "Rimfolk_Pool_CaughtIt", "Rimfolk_Pool_GuestCough", "Rimfolk_Pool_ColdNight", "Rimfolk_Pool_ColdNightDoctor", "Rimfolk_Pool_HailHit",
        "Rimfolk_Pool_FellingMishap", "Rimfolk_Pool_BuildFell", "Rimfolk_Pool_HotBench", "Rimfolk_Pool_RutClear", "Rimfolk_Pool_RutCharged",
        "Rimfolk_Pool_LessonKicked", "Rimfolk_Pool_HerdCalms", "Rimfolk_Pool_Thrown", "Rimfolk_Pool_GroupBrawl", "Rimfolk_Pool_GroupBrawlJoin",
        "Rimfolk_Pool_SoreLoser", "Rimfolk_Pool_SoreLoserReply", "Rimfolk_Pool_GraciousWinner", "Rimfolk_Pool_RematchAsk", "Rimfolk_Pool_RematchYes",
        "Rimfolk_Pool_RematchNo", "Rimfolk_Pool_GamesCall", "Rimfolk_Pool_GamesBegin", "Rimfolk_Pool_GamesHit", "Rimfolk_Pool_GamesSorry",
        "Rimfolk_Pool_GamesEnd", "Rimfolk_Pool_RaceGo", "Rimfolk_Pool_RaceTurned", "Rimfolk_Pool_RaceWon",
        // Harm, part 5: the sick at work and the petitioner, a carrier's cough at the gate and at the camp's fire, a sickness
        // caught from them, the doctor's look at a bad batch, a sore arm, small things grown, dirty water, the cold at the gate
        // and on the watch, a predator in the dark, weather lovers, a slip into the water, a sleepwalk kept in, woken, out
        // and led back, a restless walk alone or with a friend.
        "Rimfolk_Pool_SickWorksOn", "Rimfolk_Pool_PetitionUnheeded", "Rimfolk_Pool_GateCough", "Rimfolk_Pool_CampFever", "Rimfolk_Pool_CaughtFive",
        "Rimfolk_Pool_BatchSpotted", "Rimfolk_Pool_SoreArm", "Rimfolk_Pool_SmallThings", "Rimfolk_Pool_DirtyWater", "Rimfolk_Pool_ColdWait",
        "Rimfolk_Pool_ColdWatch", "Rimfolk_Pool_WatchPredator", "Rimfolk_Pool_WeatherLover", "Rimfolk_Pool_ColdWater", "Rimfolk_Pool_SleepwalkDoor",
        "Rimfolk_Pool_SleepwalkRoommate", "Rimfolk_Pool_SleepwalkWakes", "Rimfolk_Pool_SleepwalkHurt", "Rimfolk_Pool_SleepwalkLed", "Rimfolk_Pool_RestlessWalk",
        "Rimfolk_Pool_RestlessFriend", "Rimfolk_Pool_SomethingOut",
        // Harm, part 6: an answer's hot or heavy work, a fall back too soon and the help back to bed, a makeshift spot, a wall
        // taken down, the hands home sore, a slip in a project's last push or worn down, a sickle in a hurry, a strong back, a
        // nick, the second cup and its stagger; the drovers' bull and their word, too close to a wild one, the dawn walk and a
        // predator in it, the bold one seen and come, a catch with or without a partner, the visitor swiping or shooed; a spark
        // singeing and the shout, the muster's call and mishap, a spar's start, blow, sorry and end, a dare's start, call,
        // laugh, hurt and the one who talks them out, the marshal's word, a coward and an avenger breaking cover.
        "Rimfolk_Pool_HotHeavy", "Rimfolk_Pool_BackTooSoon", "Rimfolk_Pool_HelpedToBed", "Rimfolk_Pool_Makeshift", "Rimfolk_Pool_TakeDown",
        "Rimfolk_Pool_HandsSore", "Rimfolk_Pool_ProjectPush", "Rimfolk_Pool_Clumsy", "Rimfolk_Pool_Sickle", "Rimfolk_Pool_StrongBack", "Rimfolk_Pool_Nick",
        "Rimfolk_Pool_SecondCup", "Rimfolk_Pool_Stagger", "Rimfolk_Pool_DroverCharge", "Rimfolk_Pool_DroverWord", "Rimfolk_Pool_TooClose", "Rimfolk_Pool_DawnWalk",
        "Rimfolk_Pool_DawnPredator", "Rimfolk_Pool_BoldWolfSeen", "Rimfolk_Pool_BoldWolf", "Rimfolk_Pool_CatchPartner", "Rimfolk_Pool_CatchFights",
        "Rimfolk_Pool_Swiped", "Rimfolk_Pool_Shoo", "Rimfolk_Pool_Singed", "Rimfolk_Pool_SparkCaught", "Rimfolk_Pool_MusterCall", "Rimfolk_Pool_MusterMishap",
        "Rimfolk_Pool_SparBegin", "Rimfolk_Pool_SparHit", "Rimfolk_Pool_SparSorry", "Rimfolk_Pool_SparEnd", "Rimfolk_Pool_DareBegin", "Rimfolk_Pool_DareDone",
        "Rimfolk_Pool_DareHurt", "Rimfolk_Pool_DareTalkedOut", "Rimfolk_Pool_DareCall", "Rimfolk_Pool_MarshalWord", "Rimfolk_Pool_CowardBreaks",
        "Rimfolk_Pool_FuryBreaks",
        // Harm, part 7 (Harm/Tempers.cs, GateTroubles.cs, Stories.cs): tempers in the colony, visitors and outsiders, personal
        // stories, said by those they happen to and those who step in.
        "Rimfolk_Pool_SitDownBlows", "Rimfolk_Pool_PeacemakerBetween", "Rimfolk_Pool_SpeakerWords", "Rimfolk_Pool_SpeakerCalms", "Rimfolk_Pool_PeacemakerWord",
        "Rimfolk_Pool_PassedOver", "Rimfolk_Pool_MeetingBoils", "Rimfolk_Pool_FriendStepsIn", "Rimfolk_Pool_FriendTookBlow", "Rimfolk_Pool_SaidToFace",
        "Rimfolk_Pool_WrongedOne", "Rimfolk_Pool_ToldByFriend", "Rimfolk_Pool_ToldByFriendReply", "Rimfolk_Pool_LoveRivals", "Rimfolk_Pool_LovedWatches",
        "Rimfolk_Pool_AltarWords", "Rimfolk_Pool_FeastFists", "Rimfolk_Pool_SecondRound", "Rimfolk_Pool_SoldierShout", "Rimfolk_Pool_SoldierStruck",
        "Rimfolk_Pool_ThiefSeen", "Rimfolk_Pool_GateDrunk", "Rimfolk_Pool_GamblerCheat", "Rimfolk_Pool_BarberNick", "Rimfolk_Pool_NickLaugh",
        "Rimfolk_Pool_RankChallenge", "Rimfolk_Pool_ChallengeTaken", "Rimfolk_Pool_ChallengeRefused", "Rimfolk_Pool_CrowdJoins", "Rimfolk_Pool_MarshalBreaksUp",
        "Rimfolk_Pool_PastFirstBlood", "Rimfolk_Pool_PullApart", "Rimfolk_Pool_LetterChallenger", "Rimfolk_Pool_HeirGuards", "Rimfolk_Pool_HatedTrader",
        "Rimfolk_Pool_GateZeal", "Rimfolk_Pool_GateZealReply", "Rimfolk_Pool_HungryStruck", "Rimfolk_Pool_HungrySeen", "Rimfolk_Pool_CreekQuarrel",
        "Rimfolk_Pool_CreekCaught", "Rimfolk_Pool_NemesisSeeks", "Rimfolk_Pool_WorstNight", "Rimfolk_Pool_FirstStepsFall", "Rimfolk_Pool_FirstStepsHelp",
        "Rimfolk_Pool_WorkingSlips", "Rimfolk_Pool_TeacherWord", "Rimfolk_Pool_LostChildHurt", "Rimfolk_Pool_LostChildFinder", "Rimfolk_Pool_CourierSetUpon",
        "Rimfolk_Pool_CourierSaved",
        // Schemes against the colony (Diplomacy/Schemes.cs): a shout at one of theirs caught at it, their word as they go,
        // a lie in a hot temper's ear, the hot temper having it out.
        "Rimfolk_Pool_SchemeCaught", "Rimfolk_Pool_SchemeCaughtGoes", "Rimfolk_Pool_SchemeWhisper", "Rimfolk_Pool_SchemeProvoked",
        // Travellers at the gate, waiting, setting off, and those at home seeing them off (Journeys/JourneyRun.cs).
        "Rimfolk_Pool_JourneySays",
        // The wild's alphas and bosses (Beasts/): someone seeing a pack's alpha, swearing against a beast, watching a herd go
        // by, and the song of a legend's fall at the fire.
        "Rimfolk_Pool_AlphaSighting", "Rimfolk_Pool_BeastOath", "Rimfolk_Pool_HerdPasses", "Rimfolk_Pool_LegendSong",
        // Banishment by the council (Social/Banishment.cs): the council's word face to face and the answer, someone sent away
        // told and answering, their last words as they go.
        "Rimfolk_Pool_BanishWord", "Rimfolk_Pool_BanishWordReply", "Rimfolk_Pool_BanishTell", "Rimfolk_Pool_BanishReply", "Rimfolk_Pool_BanishLast",
        // A pet's grave being dug (PlayOut/PlayPetGrave.cs, said through PlayWords).
        "Rimfolk_Pool_PetGraveInvite", "Rimfolk_Pool_PetGrave",
        // Wishes promised, refused, objected to, reminded of, given up on, backed and remembered (CouncilDynamics.cs,
        // CouncilGameComponent.cs: said over the head, sometimes quoted in a message too).
        "Council_Pool_Promised", "Council_Pool_Refused", "Council_Pool_Objection", "Council_Pool_Reminder", "Council_Pool_SelfHelp",
        "Council_Pool_LastCall", "Council_Pool_PassedOver", "Council_Pool_BrokenPromise", "Council_Pool_Bitter", "Council_Pool_Quitting",
        "Council_Pool_Anniversary", "Council_Pool_CoSign",
        // Ambitions and the council's meeting (Ambitions/, Interplay/Interplay_Council.cs, Decrees/DecreeFaith.cs).
        "Council_Pool_AmbitionHalfway", "Council_Pool_AmbitionDeadline", "Council_Pool_MeetingOpen", "Council_Pool_MeetingOnBehalf",
        "Council_Pool_MeetingDoubt", "Council_Pool_MeetingAside", "Council_Pool_MeetingSupport", "Council_Pool_MeetingWake",
        "Council_Pool_Interplay_MeetingSecret", "Council_Pool_FaithDecree_MeetingWord",
        // An election on the map: the campaign, the hustings, polling day and the count (Offices/OfficesElection.cs).
        "Rimfolk_Pool_ElectionCanvass", "Rimfolk_Pool_CanvassWon", "Rimfolk_Pool_CanvassUnmoved", "Rimfolk_Pool_HustingsOpen", "Rimfolk_Pool_HustingsGood",
        "Rimfolk_Pool_HustingsPoor", "Rimfolk_Pool_HustingsCheer", "Rimfolk_Pool_HustingsHeckle", "Rimfolk_Pool_ElectionVote", "Rimfolk_Pool_CountOpen",
        "Rimfolk_Pool_CountOne", "Rimfolk_Pool_CountSome", "Rimfolk_Pool_CountWinner", "Rimfolk_Pool_CountUnopposed", "Rimfolk_Pool_CeremonyOpen",
        "Rimfolk_Pool_ElectionCheer", "Rimfolk_Pool_ElectionToast", "Rimfolk_Pool_ElectionConcede",
        // A winner gloating over a seat, and thanks for being carried out of a fight (Offices/Offices.cs, Social/SocialWeb.cs).
        "Rimfolk_Pool_SeatGloat", "Rimfolk_Pool_ThanksSaved",
        // Animals spoken to (Animals/AnimalLife.cs, AnimalHerdmaster.cs).
        "Council_Pool_Herd_SayNewborn", "Council_Pool_SayToAnimal", "Council_Pool_AnimalFollows",
        // The animals' natures (Animals/AnimalTempers.cs, AnimalBonds.cs, AnimalVoices.cs): what is said on seeing them, and their own words.
        "Council_Pool_SeeTemper", "Council_Pool_SeeAnimals", "Council_Pool_AnimalVoice",
        // The colony's animals hunting (Animals/PetHunts.cs): a kill seen, a kill brought home, a hungry one with nothing in reach.
        "Council_Pool_SeeHunt", "Council_Pool_PetBringsKill", "Council_Pool_SayPetNoFood",
        // A personal touch planned and built (Building/PersonalTouches.cs).
        "Council_Pool_Touch", "Council_Pool_TouchDone",
        // A faith evening stayed away from, couriers, peacemakers, a joke retold (Faith/, Couriers/, Interplay/, Social/).
        "Council_Pool_FaithEveningAway", "Rimfolk_CourierLines", "Council_Pool_Interplay_PeacemakerSays", "Rimfolk_Pool_Group_PeacemakerSays",
        "Rimfolk_JokeRetold_Tell", "Rimfolk_JokeRetold_Reply",
        // Gossip, told and answered (Social/Gossip/Gossip.cs).
        "Rimfolk_Gossip_Present", "Rimfolk_Gossip_Boast", "Rimfolk_Gossip_Good_Plain", "Rimfolk_Gossip_Good_Praise", "Rimfolk_Gossip_Good_Joke",
        "Rimfolk_Gossip_Good_Scorn", "Rimfolk_Gossip_Bad_Plain", "Rimfolk_Gossip_Bad_Praise", "Rimfolk_Gossip_Bad_Joke", "Rimfolk_Gossip_Bad_Scorn",
        "Rimfolk_Gossip_React_Admire", "Rimfolk_Gossip_React_Condemn", "Rimfolk_Gossip_React_Defend", "Rimfolk_Gossip_React_Doubt",
        "Rimfolk_Gossip_React_Laugh", "Rimfolk_Gossip_React_Shrug", "Rimfolk_Gossip_React_Subject",
        // The sky, the weather and the forecast (Touches/VisualTouches.cs, Immersion/WeatherTalk.cs, WeatherTouches.cs, Weather/).
        "Council_Pool_TouchVictory", "Council_Pool_TouchTradition", "Council_Pool_TouchComet", "Council_Pool_TouchFireflies", "Council_Pool_TouchHail",
        "Council_Pool_TouchWarmSpell", "Council_Pool_SayWarmSpell", "Council_Pool_SayHailComing", "Council_Pool_SayLightningRod",
        "Council_Pool_SayLightningFlinch", "Council_Pool_SayRecordCold", "Council_Pool_SayRecordDry", "Council_Pool_SayRecordHot",
        "Council_Pool_SayRecordWet", "Council_Pool_SayForecastClear", "Council_Pool_SayForecastCloud", "Council_Pool_SayForecastDust",
        "Council_Pool_SayForecastFog", "Council_Pool_SayForecastRain", "Council_Pool_SayForecastSnow", "Council_Pool_SayForecastStorm",
        "Council_Pool_SayForecastWind", "Council_Pool_SayForecastWrong",
        // A creature evolved (Integrations/PokeWorldLife.cs).
        "Council_Pool_PW_Congratulate", "Council_Pool_PW_SayEvolved",
        // Quiet troubles said aloud (Setbacks/Troubles.cs).
        "Rimfolk_Pool_Trouble_Worry", "Rimfolk_Pool_Trouble_Anniversary", "Rimfolk_Pool_Trouble_FormerLife", "Rimfolk_Pool_Trouble_Restless",
        "Rimfolk_Pool_Trouble_Aches", "Rimfolk_Pool_Trouble_BadNight", "Rimfolk_Pool_Trouble_Envy",
        // Setbacks said aloud (Setbacks/).
        "Council_Pool_Setback_IdeaPassed", "Council_Pool_Setback_NoPlate", "Council_Pool_Setback_Surgery", "Rimfolk_Pool_Setback_Burned",
        "Council_Pool_Setback_CropLost", "Council_Pool_Setback_Rained", "Council_Pool_Setback_Wrecked",
        // Work parties, walks, vigils, searches, errands, hunts, gifts, rounds and hauls: the call to it and the lines while
        // it goes on (PlayOut/PlayWords.cs); the gatherings' own when an answer names none (PlayOutEffects.cs, GatheringTouches.cs).
        "Council_Pool_PlayWorkInvite", "Council_Pool_PlayWork", "Council_Pool_PlayWalkInvite", "Council_Pool_PlayWalk", "Council_Pool_PlayVigilInvite",
        "Council_Pool_PlayVigil", "Council_Pool_PlaySearchInvite", "Council_Pool_PlaySearch", "Council_Pool_PlayCarryInvite", "Council_Pool_PlayCarry",
        "Council_Pool_PlayHuntInvite", "Council_Pool_PlayHunt", "Council_Pool_PlayGiveInvite", "Council_Pool_PlayGive", "Council_Pool_PlayRoundsInvite",
        "Council_Pool_PlayRounds", "Council_Pool_PlayHaulInvite", "Council_Pool_PlayHaul", "Council_Pool_PlayToast", "Council_Pool_PlayFeast",
        "Council_Pool_PlayFire", "Council_Pool_PlayParty", "Council_Pool_PlaySong", "Council_Pool_PlayWords", "Council_Pool_PlaySaidAloud",
        // The heart's good side overflowing (Heart/GoodRelease.cs): a kind word face to face, and a word as an inspiration begins.
        "Rimfolk_Pool_GoodHeart_KindWord", "Rimfolk_Pool_GoodHeart_Inspired",
        // The heart, felt (Heart/HeartFelt.cs, HeartActs.cs): how they are when the band turns, and the small acts' lines.
        "Rimfolk_Pool_HeartBand", "Rimfolk_Pool_HeartAct",
        // The colony's animals with powers (Animals/AnimalPowers.cs): the Pokémon types at home seen at it, the calmed, a
        // spar's start and its blow; a familiar's binding, its taking and its death; an omen named, talked of and fallen; a
        // sacred animal fallen and a hunt marked on one.
        "Rimfolk_Pool_PW_SayWarm", "Rimfolk_Pool_PW_SayCool", "Rimfolk_Pool_PW_SayGrowth", "Rimfolk_Pool_PW_SayBattery", "Rimfolk_Pool_PW_SayCalm",
        "Rimfolk_Pool_PW_SayDouse", "Rimfolk_Pool_PW_SaySparBegin", "Rimfolk_Pool_PW_SaySparBlow", "Rimfolk_Pool_Fam_Begin", "Rimfolk_Pool_Fam_Bound",
        "Rimfolk_Pool_Fam_Died", "Rimfolk_Pool_Omen_Name", "Rimfolk_Pool_Omen_Talk", "Rimfolk_Pool_Omen_Died", "Rimfolk_Pool_Sacred_Fell",
        "Rimfolk_Pool_Sacred_Hunt",
        // The seven peoples brought to the level of the new ones (Peoples/PeoplesLore.cs): what is said at the learned
        // society's night of the fire (their letters, messengers, visitors and talk are in the pools above).
        "Rimfolk_Pool_PlayDisputation",
        // Release D's peoples (Peoples/PeoplesD.cs): what is said at a funeral the Quiet Bell leads.
        "Rimfolk_Pool_PlayBellFuneral"
    };

    /// <summary>Spoken pools said so rarely (a record broken, a lightning rod struck) that a handful of lines is enough.</summary>
    private static readonly HashSet<string> RarePools = new() { "Council_Pool_SayLightningRod", "Council_Pool_SayRecordDry", "Council_Pool_SayRecordWet" };

    /// <summary>The game's own words said over a head in place of a pool's (Languages/English/Keyed).</summary>
    private static readonly string[] SpokenKeys =
    {
        "CC_PromisedFallback", "CC_RefusedFallback", "CC_ExpiredFallback", "CC_ThanksFallback", "CC_ForOtherFallback",
        "CC_CoSignLine_Plain", "CC_CoSignLine_Warm", "CC_CoSignLine_Blunt", "CC_CoSignLine_Cheerful", "CC_CoSignLine_Gloomy", "CC_CoSignLine_Cold",
        "CC_Touch_Plain", "CC_Touch_Warm", "CC_Touch_Blunt", "CC_Touch_Cheerful", "CC_Touch_Gloomy", "CC_Touch_Cold",
        "CC_TouchDone_Plain", "CC_TouchDone_Warm", "CC_TouchDone_Blunt", "CC_TouchDone_Cheerful", "CC_TouchDone_Gloomy", "CC_TouchDone_Cold",
        "CC_Ambition_ChosenSpeech", "CC_Ambition_WishHelped", "CC_Evt_Contest_Won", "CC_Evt_Contest_Lost"
    };

    private static readonly string[] JournalPools =
    {
        "Council_Journal_FirstSnowSeason", "Council_Journal_HeatWave", "Council_Journal_ColdSnap", "Council_Journal_Storm", "Council_Journal_Changed",
        "Council_Journal_NewLove", "Council_Journal_Engaged", "Council_Journal_ProposalRefused", "Council_Journal_Expecting", "Council_Journal_Loss", "Council_Journal_ChildGrew",
        "Council_Journal_TookIn", "Council_Journal_TakenIn", "Council_Journal_InheritedRoom", "Council_Journal_OldWeddingDay",
        "Council_Journal_TookName", "Council_Journal_LastSeason",
        // Quiet troubles written down (Setbacks/Troubles.cs; a bad night's entry is the dream itself).
        "Rimfolk_Journal_Trouble_Worry", "Rimfolk_Journal_Trouble_Anniversary", "Rimfolk_Journal_Trouble_FormerLife", "Rimfolk_Journal_Trouble_Restless",
        "Rimfolk_Journal_Trouble_Aches", "Rimfolk_Journal_Trouble_Envy",
        // Banishment by the council: the last entry of one sent away, and one who loved them (Social/Banishment.cs).
        "Rimfolk_Journal_Banished", "Rimfolk_Journal_BanishedKin",
        // A familiar bound and lost, an omen born (Animals/AnimalPowers.cs).
        "Rimfolk_Journal_Fam_Bound", "Rimfolk_Journal_Fam_Died", "Rimfolk_Journal_Omen_Born",
        // The seven peoples' touchpoints: the order's night nurse, the pilgrims' Long Walk, the society's night of the fire.
        "Rimfolk_Journal_Evt_OrderNurse", "Rimfolk_Journal_Evt_LongWalk", "Rimfolk_Journal_Evt_Disputation"
    };

    private static readonly string[] Prefixes = { "Council_Setback_", "Council_Friction_", "Council_Nuisance_", "Council_Arc_" };

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "immersion: " + what);
        }

        // ---------- Every pool's lines, its own and those added to it from other files (any folder of Defs) ----------
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var pools = new Dictionary<string, List<string>>();
        foreach (var el in defs.SelectMany(d => d.Elements()))
        {
            string name = el.Name.LocalName == "ColonyCouncil.FlavorPoolDef" ? el.Element("defName")?.Value
                : el.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" ? el.Element("pool")?.Value : null;
            if (name == null) continue;
            if (!pools.TryGetValue(name, out var lines)) pools[name] = lines = new List<string>();
            var block = el.Element("lines");
            if (block != null) lines.AddRange(block.Descendants("li").Select(li => li.Value));
        }
        foreach (var pool in SpokenPools.Concat(JournalPools))
            Check(pools.TryGetValue(pool, out var l) && l.Count >= (RarePools.Contains(pool) ? 1 : 6), $"{pool} exists with lines");

        // The pools the defs name for speech: a small habit's and a group habit's, an answer's gathering, walk or words
        // (said while it goes on, PlayWords.During and LordJob_PlayOut), an objection's, the road word a camp brings.
        var named = new HashSet<string>();
        foreach (var e in defs.SelectMany(d => d.Descendants()))
        {
            string cls = (string)e.Attribute("Class") ?? "";
            if (cls == "ColonyCouncil.HabitExtension" || cls.StartsWith("ColonyCouncil.DilemmaEffect_") || e.Name.LocalName == "ColonyCouncil.GroupHabitDef")
            {
                string pool = e.Element("pool")?.Value.Trim();
                if (!string.IsNullOrEmpty(pool)) named.Add(pool);
            }
            if (e.Name.LocalName == "objectionPool" || e.Name.LocalName == "roadWord") named.Add(e.Value.Trim());
        }
        Check(named.Count >= 40, $"the defs name pools to be said ({named.Count})");
        foreach (var pool in named) Check(pools.ContainsKey(pool), $"{pool}, named by a def to be said, exists");

        // ---------- Said over a head: one short sentence each ----------
        void Spoken(string where, string line)
        {
            string body = Strip(line, out _);
            Check(!body.StartsWith("\"") && !body.EndsWith("\""), $"{where}: '{body}' doesn't start or end with a quote");
            Check(FirstSentence(body) == body, $"{where}: '{body}' is one sentence");
            Check(body.Length <= 90, $"{where}: '{body}' is 90 characters at most ({body.Length})");
            Check(body.Count(c => c == '!') <= 1, $"{where}: '{body}' has one '!' at most");
        }
        foreach (var pool in SpokenPools.Union(named))
        {
            if (!pools.TryGetValue(pool, out var lines)) continue;
            foreach (var line in lines) Spoken(pool, line);
        }
        // The lines the defs give to be said: an event's (Arrivals.Say, the callers' work), an answer's play-out or a
        // camp's errand (PlayWords.During, LordJob_PlayOut, CampRequests.ErrandWorked), a camp's own (CampLife, CampLook), and
        // those a file of their own adds to a kind of camp (a FlavorExtensionDef with camp: campLines, momentLines).
        int given = 0;
        foreach (var e in defs.SelectMany(d => d.Descendants()))
        {
            string cls = (string)e.Attribute("Class") ?? "";
            var fields = cls == "ColonyCouncil.HappeningExtension" || cls.StartsWith("ColonyCouncil.DilemmaEffect_") ? new[] { "lines" }
                : e.Name.LocalName == "ColonyCouncil.CampKindDef" ? new[] { "lines", "momentLines" }
                : e.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" && e.Element("camp") != null ? new[] { "campLines", "momentLines" } : null;
            if (fields == null) continue;
            string where = e.Element("defName")?.Value ?? e.Ancestors().Select(a => a.Element("defName")?.Value).FirstOrDefault(n => n != null) ?? cls;
            foreach (var f in fields)
                foreach (var li in e.Element(f)?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    given++;
                    Spoken(where, li.Value);
                }
        }
        Check(given >= 300, $"the defs give lines to be said ({given})");
        // Conversations with outsiders (Defs/OutsiderTalkDefs, said in turn by Outsiders/OutsiderTalk.cs): every line said.
        int talked = 0;
        foreach (var e in defs.SelectMany(d => d.Elements("ColonyCouncil.OutsiderTalkDef")))
            foreach (var li in e.Element("lines")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                talked++;
                Spoken(e.Element("defName")?.Value ?? "OutsiderTalkDef", li.Value);
            }
        Check(talked >= 500, $"conversations with outsiders give lines to be said ({talked})");
        // The game's own words, said when a pool has nothing that fits.
        var keyed = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                if (SpokenKeys.Contains(el.Name.LocalName))
                {
                    keyed.Add(el.Name.LocalName);
                    Spoken(el.Name.LocalName, el.Value);
                }
        foreach (var key in SpokenKeys) Check(keyed.Contains(key), $"{key} exists");

        // ---------- Tags name real things ----------
        var feelings = Keys(root, "HeartDefs", "key");
        var ailments = Keys(root, "HediffDefs", "key");
        // Setbacks, and the frictions and nuisances that may be said too (those with lines of their own).
        var setbacks = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "ThoughtDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                string name = el.Element("defName")?.Value ?? "";
                if (el.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.SetbackExtension")
                    || name.StartsWith("Council_Friction_") || name.StartsWith("Council_Nuisance_"))
                    setbacks.Add(Short(name));
            }
        var stories = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "ArcDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                if (el.Name.LocalName == "ColonyCouncil.ArcDef" && el.Element("stages") != null)
                    stories.Add(Short(el.Element("defName")?.Value ?? ""));
        Check(feelings.Count >= 20 && ailments.Count >= 40 && setbacks.Count >= 30 && stories.Count >= 10, "found the feelings, ailments, setbacks and stories");
        var said = new Dictionary<string, HashSet<string>> { ["feeling"] = new(), ["ail"] = new(), ["setback"] = new(), ["story"] = new() };
        var known = new Dictionary<string, HashSet<string>> { ["feeling"] = feelings, ["ail"] = ailments, ["setback"] = setbacks, ["story"] = stories };
        foreach (var pool in SpokenPools)
        {
            if (!pools.TryGetValue(pool, out var lines)) continue;
            foreach (var line in lines)
            {
                Strip(line, out var tags);
                foreach (var alt in tags.SelectMany(t => t.Split('|')))
                {
                    string tag = alt.Trim().TrimStart('!');
                    int colon = tag.IndexOf(':');
                    if (colon < 0) continue;
                    string kind = tag.Substring(0, colon), value = tag.Substring(colon + 1);
                    if (!known.TryGetValue(kind, out var names)) continue;
                    Check(names.Contains(value), $"{pool}: [{tag}] names a {kind} that exists");
                    said[kind].Add(value);
                }
            }
        }
        foreach (var kind in said.Keys)
            Console.WriteLine($"  Lines of their own for {said[kind].Count(known[kind].Contains)} of {known[kind].Count} {(kind == "ail" ? "ailment" : kind)} kinds.");

        // ---------- The habits' jobs name pools that exist ----------
        var habits = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_SmallHabits.xml")).Root!;
        foreach (var ext in habits.Descendants("li").Where(li => (string)li.Attribute("Class") == "ColonyCouncil.HabitExtension"))
        {
            string pool = ext.Element("pool")?.Value;
            Check(pool != null && pools.ContainsKey(pool), $"a small habit's pool {pool} exists");
        }
        Check(habits.Elements("JoyGiverDef").Count() == 12, "twelve small habits");
        // Every habit's job has a kind of its own, and every kind the code knows has a job.
        var kinds = habits.Descendants("li").Where(li => (string)li.Attribute("Class") == "ColonyCouncil.HabitExtension").Select(li => li.Element("kind")?.Value).ToList();
        Check(kinds.Count == kinds.Distinct().Count(), "each habit's job has a kind of its own");
        foreach (var k in new[] { "Grave", "LookOut", "Fire", "Remember", "Stars", "Plants", "Animals", "Stores", "Company", "Stories" })
            Check(kinds.Contains(k), $"a small habit's job for {k}");
        // Each joy giver names a job that exists here or in the game (the snowman is the game's own).
        var jobDefs = new HashSet<string>(habits.Elements("JobDef").Select(j => j.Element("defName")?.Value ?? ""));
        foreach (var giver in habits.Elements("JoyGiverDef"))
        {
            string job = giver.Element("jobDef")?.Value ?? "";
            Check(jobDefs.Contains(job) || job == "BuildSnowman", $"joy giver {giver.Element("defName")?.Value} names a job that exists ({job})");
        }
        return checks;
    }

    private static HashSet<string> Keys(string root, string folder, string element)
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", folder), "*.xml"))
            foreach (var el in XDocument.Load(file).Descendants(element))
                if (!string.IsNullOrWhiteSpace(el.Value)) keys.Add(el.Value.Trim());
        return keys;
    }

    private static string Short(string defName)
    {
        foreach (var p in Prefixes)
            if (defName.StartsWith(p)) return defName.Substring(p.Length);
        return defName;
    }

    /// <summary>"[a,b] text" into its tags and the text.</summary>
    private static string Strip(string line, out string[] tags)
    {
        tags = Array.Empty<string>();
        string t = line.TrimStart();
        if (!t.StartsWith("[")) return t.Trim();
        int end = t.IndexOf(']');
        if (end < 0) return t.Trim();
        tags = t.Substring(1, end - 1).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        return t.Substring(end + 1).Trim();
    }

    /// <summary>The first sentence: up to the first '.', '!' or '?' from the 13th character on (what always shows; SpeechMath.Shorten).</summary>
    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }
}
