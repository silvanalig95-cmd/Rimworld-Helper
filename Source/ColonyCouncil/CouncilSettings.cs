using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What era the colony lives in, for what the mod suggests and how its colonists talk.</summary>
    public enum EraMode
    {
        Auto,
        PreIndustrial,
        Industrial
    }

    public enum StakesMode
    {
        /// <summary>Only upsides: granted wishes help, ignored ones cost nothing.</summary>
        Gentle,
        /// <summary>Ignored wishes, broken promises, favoritism and jealousy all bite, and can lead to sulking and grudges.</summary>
        Demanding,
        /// <summary>No mood effects at all. Pure suggestions.</summary>
        AdvisorOnly,
        /// <summary>Upsides and downsides in proportion (default).</summary>
        Balanced
    }

    /// <summary>Where the council button sits in the bottom bar.</summary>
    public enum ButtonPosition
    {
        BeforeMenu,
        AfterResearch,
        AfterHistory,
        AfterWork
    }

    public class CouncilSettings : ModSettings
    {
        // Layers
        public bool enableAdvisor = true;
        public bool enableWishes = true;
        public bool enableAmbitions = true;
        public bool enablePersonalTouches = true;

        // Wishes
        public StakesMode stakes = StakesMode.Balanced;
        public float daysBetweenWishes = 2f;
        public int maxOpenWishes = 5;
        public float wishDurationFactor = 1f;
        public float rewardFactor = 1f;
        public float penaltyFactor = 1f;
        public bool jealousy = true;
        public bool selfHelp = true;
        /// <summary>Promises kept (Wishes/Kept.cs): a lasting wish granted and then taken away again upsets the one who asked.</summary>
        public bool wishesTakenBack = true;
        /// <summary>How long what was given may be gone (moved, reassigned) before they mind.</summary>
        public KeptGrace takenBackGrace = KeptGrace.Normal;
        public bool allowQuitting = true;
        public bool risingStandards = true;
        /// <summary>Rimfolk's good moods count with diminishing returns when several are on a colonist's mind (SmallJoys).</summary>
        public bool smallJoysDiminish = true;
        /// <summary>Rising standards also shrink the good moods of the base game and other mods (GoodTimes).</summary>
        public bool standardsCoverComforts = true;
        /// <summary>Getting used to good times: Rimfolk's new good memories fade sooner when mood is already high (GoodTimes).</summary>
        public bool goodTimesFade = true;
        /// <summary>Life's ups and downs: how hard the everyday pressures press (the same meal, the same work, crowding...).</summary>
        public LifePressure lifePressure = LifePressure.Normal;
        public bool enableDilemmas = true;
        public bool annals = true;
        public bool decreeRequests = true;
        public bool anniversaries = true;
        /// <summary>The colony calendar's days come round with letters, gatherings and moods (Calendar/); off, they're only written down.</summary>
        public bool colonyCalendar = true;
        /// <summary>Answers that describe something people do (a feast, a bonfire, a visit, a day off) play out on the map (PlayOut/).</summary>
        public bool answersPlayOut = true;
        public bool epithets = true;
        public bool journals = true;
        public bool offices = true;
        public bool socialWeb = true;
        public bool socialEvents = true;
        public bool dreams = true;
        public bool aspirations = true;
        /// <summary>Personal stories (Arcs/): how often a colonist's own story begins.</summary>
        public ArcFrequency arcFrequency = ArcFrequency.Normal;
        public bool secrets = true;
        public bool vendettas = true;
        public bool eulogies = true;
        public bool quirks = true;
        public bool heirlooms = true;
        public bool comingOfAge = true;
        public bool dreamOmens = true;
        /// <summary>Small moments with visitors, traders, prisoners, fallen enemies, pets and wild animals.</summary>
        public bool strangers = true;
        public bool nemeses = true;
        /// <summary>A nemesis who got away may ride with their people's next raid.</summary>
        public bool nemesisReturns = true;
        public bool petMoments = true;
        public bool colonistMoments = true;
        /// <summary>Everyday frictions: small irritations between colonists, and with a messy or cramped home.</summary>
        public bool frictions = true;
        public bool autonomy = true;
        public bool worldReactions = true;
        public bool autonomyPlans = true;
        /// <summary>Rimfolk's world events: pasts catching up, challenges, wonders, visitors (on their own rhythm).</summary>
        public bool worldEvents = true;
        /// <summary>World events that can end in a fight (bounty hunters, revenge, tribute refused).</summary>
        public bool worldEventBattles = true;
        /// <summary>Groups who make camp on the colony's map for a while (Camps/).</summary>
        public bool camps = true;
        /// <summary>Missions a neighbor offers show up in the game's Quests tab, to accept or let expire there (else as a letter).</summary>
        public bool missionsAsQuests = true;
        public float daysBetweenWorldEvents = 4f;
        /// <summary>Rimfolk's colony events join the storyteller's own random events (else they come on Rimfolk's own timer).</summary>
        public bool eventsFromStoryteller = true;
        /// <summary>How likely each of them is, compared with one of the game's own events.</summary>
        public float storytellerEventWeight = 1f;
        /// <summary>World events: rare, region-wide situations that last a while (a plague, a warlord, a succession).</summary>
        public bool worldSituations = true;
        public float daysBetweenWorldSituations = 45f;
        /// <summary>The world's peoples remember what the colony did for them and against them, and act on it (World/FactionMemory.cs).</summary>
        public bool factionMemory = true;
        /// <summary>How hard the fights a Rimfolk scenario promises hit (share of their usual strength).</summary>
        public float scenarioThreat = 1f;
        /// <summary>How often the smaller things happen: 1 = as designed, 0.25 = rarely, 3 = often.</summary>
        public float encounterFrequency = 1f;
        public float momentFrequency = 1f;
        public float socialEventFrequency = 1f;
        public float dreamFrequency = 1f;
        /// <summary>The colony's character: what its choices make it known for.</summary>
        public bool colonyCharacter = true;
        /// <summary>Rimfolk's systems talk to each other (Interplay/): offices at work, the heart in choices, stories hearing the world...</summary>
        public bool interplay = true;
        /// <summary>Feelings and strain (Heart/): a layer under mood that never changes it.</summary>
        public bool heart = true;
        /// <summary>A colonist at the end of their rope may take a day to themselves.</summary>
        public bool heartEpisodes = true;
        /// <summary>A line about strain and feelings under a colonist's name in the inspector (a deeper game hook).</summary>
        public bool heartInspector = true;
        /// <summary>How quickly strain builds: 0.5 to 1.5.</summary>
        public float strainSpeed = 1f;
        /// <summary>Everyday ailments (Ailments/): colds, strained backs, sunburn, and the like.</summary>
        public bool ailments = true;
        /// <summary>The rare serious illnesses, which can kill (tetanus, pneumonia, tuberculosis...).</summary>
        public bool seriousIllnesses = true;
        /// <summary>How often ailments come: 0.25 to 2.</summary>
        public float ailmentFrequency = 1f;
        /// <summary>Convalescence after serious illness, operations, long bed rest and lost limbs; sickbed visits (Health/).</summary>
        public bool convalescence = true;
        /// <summary>Doctors' and midwives' reputations: names, the colony's trust, the weight of losses (Health/).</summary>
        public bool healerReputations = true;
        /// <summary>On-screen buttons for half and quarter speed, and where they sit (fractions of the screen; -1 = default).</summary>
        public bool slowButtons = true;
        public bool moodInsight = true;
        public bool deepHooks = true;
        public bool needsTabHints = true;
        /// <summary>A line of Rimfolk's context in the game's raid, mood break and death letters.</summary>
        public bool letterNotes = true;
        public float slowButtonsX = -1f;
        public float slowButtonsY = -1f;
        public bool allDreamMessages;
        public MessageLevel messageLevel = MessageLevel.Notable;
        /// <summary>Where the smaller news goes: a message as it happens, one letter each evening (default), or only the History tab (UI/News.cs).</summary>
        public NewsMode colonyNews = NewsMode.Digest;
        public EraMode era = EraMode.Auto;
        public float daysBetweenDilemmas = 10f;
        public bool eventWishes = true;
        public bool reminders = true;
        public bool wishesForOthers = true;
        public bool petitions = true;
        public bool wishLetters = true;
        public bool grantedLetters = false;
        /// <summary>A letter after Rimfolk has been updated, with the newest round's highlights (UI/WhatsNew.cs).</summary>
        public bool whatsNewLetter = true;
        /// <summary>The Rimfolk version the player last saw, so the update letter comes once per update.</summary>
        public string lastSeenVersion = "";
        /// <summary>The old "colonists speak" switch, kept in step with <see cref="speechShows"/> (off when it is Off) so older settings files load.</summary>
        public bool speechText = true;
        /// <summary>How colonists' speech shows: bubbles when Interaction Bubbles is loaded, floating text, or not at all (Immersion/Speech.cs).</summary>
        public SpeechMode speechShows = SpeechMode.Bubbles;
        /// <summary>The game's own flecks, sky colors and sounds for what Rimfolk says is happening (Touches/).</summary>
        public bool visualTouches = true;
        /// <summary>Recreation Rimfolk's knowledge makes likelier: graves visited, slow walks, the fire after a fight (Immersion/SmallHabits.cs).</summary>
        public bool smallHabits = true;

        // Meetings
        public float daysBetweenMeetings = 15f;
        public float ambitionDeadlineFactor = 1f;
        /// <summary>At a meeting's start the chair calls everyone from ordinary work and has sleepers woken (off: they come when free).</summary>
        public bool meetingsCallFromWork = true;

        // Personal touches
        public float daysBetweenPersonalTouches = 6f;

        // Advisor
        public bool advisorAlert = true;
        public bool wishAlert = true;

        // Room planner (the clearing options are remembered between games)
        public bool planCutPlants = true;
        public bool planMineRock = true;
        public bool planReplaceOwn = false;
        public bool planSmoothStone = false;
        public bool planFurniture = true;
        /// <summary>The game's build menu shows how much of each material the colony has (a deeper game hook).</summary>
        public bool buildMenuCounts = true;
        /// <summary>Rooms the player saved from a map ("Your rooms"), usable in every save.</summary>
        public System.Collections.Generic.List<CustomRoomData> savedRooms = new System.Collections.Generic.List<CustomRoomData>();
        /// <summary>The materials last picked in the room planner, "room::group=>material", used again while there's enough.</summary>
        public System.Collections.Generic.List<string> planMaterials = new System.Collections.Generic.List<string>();
        public ButtonPosition buttonPosition = ButtonPosition.BeforeMenu;
        public bool roomsButton = true;
        /// <summary>The council window's page when it was last closed, and the Almanac page, to open there again next time.</summary>
        public string lastCouncilPage = "";
        public string lastAlmanacPage = "";
        /// <summary>How large the Almanac's pages are written (its "Aa" buttons), and whether its lists are folded away.</summary>
        public AlmanacTextSize almanacTextSize = AlmanacTextSize.Normal;
        public bool almanacFolded = false;
        /// <summary>Where the council window opens: This week (UI/WeekPanel.cs), or the page last looked at.</summary>
        public CouncilOpensOn councilOpensOn = CouncilOpensOn.LastPage;
        /// <summary>The filters and sort orders the council's pages remember, as "page.key=value" (UI/PageMemory.cs).</summary>
        public System.Collections.Generic.List<string> councilPageState = new System.Collections.Generic.List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableAdvisor, "enableAdvisor", true);
            Scribe_Values.Look(ref enableWishes, "enableWishes", true);
            Scribe_Values.Look(ref enableAmbitions, "enableAmbitions", true);
            Scribe_Values.Look(ref enablePersonalTouches, "enablePersonalTouches", true);
            Scribe_Values.Look(ref stakes, "stakes", StakesMode.Balanced);
            Scribe_Values.Look(ref daysBetweenWishes, "daysBetweenWishes", 2f);
            Scribe_Values.Look(ref maxOpenWishes, "maxOpenWishes", 5);
            Scribe_Values.Look(ref wishDurationFactor, "wishDurationFactor", 1f);
            Scribe_Values.Look(ref rewardFactor, "rewardFactor", 1f);
            Scribe_Values.Look(ref penaltyFactor, "penaltyFactor", 1f);
            Scribe_Values.Look(ref jealousy, "jealousy", true);
            Scribe_Values.Look(ref selfHelp, "selfHelp", true);
            Scribe_Values.Look(ref wishesTakenBack, "wishesTakenBack", true);
            Scribe_Values.Look(ref takenBackGrace, "takenBackGrace", KeptGrace.Normal);
            Scribe_Values.Look(ref allowQuitting, "allowQuitting", true);
            Scribe_Values.Look(ref risingStandards, "risingStandards", true);
            Scribe_Values.Look(ref smallJoysDiminish, "smallJoysDiminish", true);
            Scribe_Values.Look(ref standardsCoverComforts, "standardsCoverComforts", true);
            Scribe_Values.Look(ref goodTimesFade, "goodTimesFade", true);
            Scribe_Values.Look(ref lifePressure, "lifePressure", LifePressure.Normal);
            Scribe_Values.Look(ref enableDilemmas, "enableDilemmas", true);
            Scribe_Values.Look(ref annals, "annals", true);
            Scribe_Values.Look(ref decreeRequests, "decreeRequests", true);
            Scribe_Values.Look(ref anniversaries, "anniversaries", true);
            Scribe_Values.Look(ref colonyCalendar, "colonyCalendar", true);
            Scribe_Values.Look(ref answersPlayOut, "answersPlayOut", true);
            Scribe_Values.Look(ref epithets, "epithets", true);
            Scribe_Values.Look(ref journals, "journals", true);
            Scribe_Values.Look(ref offices, "offices", true);
            Scribe_Values.Look(ref socialWeb, "socialWeb", true);
            Scribe_Values.Look(ref socialEvents, "socialEvents", true);
            Scribe_Values.Look(ref dreams, "dreams", true);
            Scribe_Values.Look(ref aspirations, "aspirations", true);
            Scribe_Values.Look(ref arcFrequency, "arcFrequency", ArcFrequency.Normal);
            Scribe_Values.Look(ref secrets, "secrets", true);
            Scribe_Values.Look(ref vendettas, "vendettas", true);
            Scribe_Values.Look(ref eulogies, "eulogies", true);
            Scribe_Values.Look(ref quirks, "quirks", true);
            Scribe_Values.Look(ref heirlooms, "heirlooms", true);
            Scribe_Values.Look(ref comingOfAge, "comingOfAge", true);
            Scribe_Values.Look(ref strangers, "strangers", true);
            Scribe_Values.Look(ref nemeses, "nemeses", true);
            Scribe_Values.Look(ref nemesisReturns, "nemesisReturns", true);
            Scribe_Values.Look(ref petMoments, "petMoments", true);
            Scribe_Values.Look(ref colonistMoments, "colonistMoments", true);
            Scribe_Values.Look(ref frictions, "frictions", true);
            Scribe_Values.Look(ref autonomy, "autonomy", true);
            Scribe_Values.Look(ref worldReactions, "worldReactions", true);
            Scribe_Values.Look(ref autonomyPlans, "autonomyPlans", true);
            Scribe_Values.Look(ref worldEvents, "worldEvents", true);
            Scribe_Values.Look(ref worldEventBattles, "worldEventBattles", true);
            Scribe_Values.Look(ref camps, "camps", true);
            Scribe_Values.Look(ref missionsAsQuests, "missionsAsQuests", true);
            Scribe_Values.Look(ref daysBetweenWorldEvents, "daysBetweenWorldEvents", 4f);
            Scribe_Values.Look(ref eventsFromStoryteller, "eventsFromStoryteller", true);
            Scribe_Values.Look(ref storytellerEventWeight, "storytellerEventWeight", 1f);
            Scribe_Values.Look(ref worldSituations, "worldSituations", true);
            Scribe_Values.Look(ref daysBetweenWorldSituations, "daysBetweenWorldSituations", 45f);
            Scribe_Values.Look(ref factionMemory, "factionMemory", true);
            Scribe_Values.Look(ref scenarioThreat, "scenarioThreat", 1f);
            Scribe_Values.Look(ref encounterFrequency, "encounterFrequency", 1f);
            Scribe_Values.Look(ref momentFrequency, "momentFrequency", 1f);
            Scribe_Values.Look(ref socialEventFrequency, "socialEventFrequency", 1f);
            Scribe_Values.Look(ref dreamFrequency, "dreamFrequency", 1f);
            Scribe_Values.Look(ref colonyCharacter, "colonyCharacter", true);
            Scribe_Values.Look(ref interplay, "interplay", true);
            Scribe_Values.Look(ref heart, "heart", true);
            Scribe_Values.Look(ref heartEpisodes, "heartEpisodes", true);
            Scribe_Values.Look(ref heartInspector, "heartInspector", true);
            Scribe_Values.Look(ref strainSpeed, "strainSpeed", 1f);
            Scribe_Values.Look(ref ailments, "ailments", true);
            Scribe_Values.Look(ref seriousIllnesses, "seriousIllnesses", true);
            Scribe_Values.Look(ref ailmentFrequency, "ailmentFrequency", 1f);
            Scribe_Values.Look(ref convalescence, "convalescence", true);
            Scribe_Values.Look(ref healerReputations, "healerReputations", true);
            Scribe_Values.Look(ref slowButtons, "slowButtons", true);
            Scribe_Values.Look(ref moodInsight, "moodInsight", true);
            Scribe_Values.Look(ref deepHooks, "deepHooks", true);
            Scribe_Values.Look(ref needsTabHints, "needsTabHints", true);
            Scribe_Values.Look(ref letterNotes, "letterNotes", true);
            Scribe_Values.Look(ref slowButtonsX, "slowButtonsX", -1f);
            Scribe_Values.Look(ref slowButtonsY, "slowButtonsY", -1f);
            Scribe_Values.Look(ref dreamOmens, "dreamOmens", true);
            Scribe_Values.Look(ref allDreamMessages, "allDreamMessages", false);
            Scribe_Values.Look(ref messageLevel, "messageLevel", MessageLevel.Notable);
            Scribe_Values.Look(ref colonyNews, "colonyNews", NewsMode.Digest);
            Scribe_Values.Look(ref era, "era", EraMode.Auto);
            Scribe_Values.Look(ref daysBetweenDilemmas, "daysBetweenDilemmas", 10f);
            Scribe_Values.Look(ref eventWishes, "eventWishes", true);
            Scribe_Values.Look(ref reminders, "reminders", true);
            Scribe_Values.Look(ref wishesForOthers, "wishesForOthers", true);
            Scribe_Values.Look(ref petitions, "petitions", true);
            Scribe_Values.Look(ref wishLetters, "wishLetters", true);
            Scribe_Values.Look(ref grantedLetters, "grantedLetters", false);
            Scribe_Values.Look(ref whatsNewLetter, "whatsNewLetter", true);
            Scribe_Values.Look(ref lastSeenVersion, "lastSeenVersion", "");
            // The old switch is written from the new choice, and an old settings file with it off reads as "off".
            if (Scribe.mode == LoadSaveMode.Saving) speechText = speechShows != SpeechMode.Off;
            Scribe_Values.Look(ref speechText, "speechText", true);
            Scribe_Values.Look(ref speechShows, "speechShows", SpeechMode.Bubbles);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !speechText) speechShows = SpeechMode.Off;
            Scribe_Values.Look(ref visualTouches, "visualTouches", true);
            Scribe_Values.Look(ref smallHabits, "smallHabits", true);
            Scribe_Values.Look(ref daysBetweenMeetings, "daysBetweenMeetings", 15f);
            Scribe_Values.Look(ref ambitionDeadlineFactor, "ambitionDeadlineFactor", 1f);
            Scribe_Values.Look(ref meetingsCallFromWork, "meetingsCallFromWork", true);
            Scribe_Values.Look(ref daysBetweenPersonalTouches, "daysBetweenPersonalTouches", 6f);
            Scribe_Values.Look(ref advisorAlert, "advisorAlert", true);
            Scribe_Values.Look(ref wishAlert, "wishAlert", true);
            Scribe_Values.Look(ref planCutPlants, "planCutPlants", true);
            Scribe_Values.Look(ref planMineRock, "planMineRock", true);
            Scribe_Values.Look(ref planReplaceOwn, "planReplaceOwn", false);
            Scribe_Values.Look(ref planSmoothStone, "planSmoothStone", false);
            Scribe_Values.Look(ref planFurniture, "planFurniture", true);
            Scribe_Values.Look(ref buildMenuCounts, "buildMenuCounts", true);
            Scribe_Collections.Look(ref savedRooms, "savedRooms", LookMode.Deep);
            Scribe_Collections.Look(ref planMaterials, "planMaterials", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                savedRooms ??= new System.Collections.Generic.List<CustomRoomData>();
                savedRooms.RemoveAll(r => r == null);
                planMaterials ??= new System.Collections.Generic.List<string>();
            }
            Scribe_Values.Look(ref buttonPosition, "buttonPosition", ButtonPosition.BeforeMenu);
            Scribe_Values.Look(ref roomsButton, "roomsButton", true);
            Scribe_Values.Look(ref lastCouncilPage, "lastCouncilPage", "");
            Scribe_Values.Look(ref lastAlmanacPage, "lastAlmanacPage", "");
            Scribe_Values.Look(ref almanacTextSize, "almanacTextSize", AlmanacTextSize.Normal);
            Scribe_Values.Look(ref almanacFolded, "almanacFolded", false);
            Scribe_Values.Look(ref councilOpensOn, "councilOpensOn", CouncilOpensOn.LastPage);
            Scribe_Collections.Look(ref councilPageState, "councilPageState", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
                councilPageState ??= new System.Collections.Generic.List<string>();
        }

        /// <summary>Saves the settings file right away (used by the planner's checkboxes).</summary>
        public static void SaveNow()
        {
            try
            {
                LoadedModManager.GetMod<ColonyCouncilMod>()?.WriteSettings();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("saving settings", e);
            }
        }

        public bool MoodEffects => stakes != StakesMode.AdvisorOnly;

        /// <summary>Downsides are on (Balanced or Demanding).</summary>
        public bool Negatives => stakes == StakesMode.Balanced || stakes == StakesMode.Demanding;

        /// <summary>The harsh extras: sulking, tantrums, stronger penalties.</summary>
        public bool Harsh => stakes == StakesMode.Demanding;

        /// <summary>Multiplier for every penalty: 0 in Gentle, scaled by the slider.</summary>
        public float PenaltyScale => !Negatives ? 0f : (Harsh ? 1.5f : 1f) * penaltyFactor;

        /// <summary>For downsides the player picked knowingly (decrees): half strength in Gentle instead of none.</summary>
        public float ChosenPenaltyScale => !MoodEffects ? 0f : Negatives ? PenaltyScale : 0.5f;

        /// <summary>The settings window, drawn page by page from the catalog (UI/SettingsWindow.cs, UI/SettingsCatalog.cs).</summary>
        public void DoWindowContents(Rect inRect) => SettingsWindow.Draw(this, inRect);
    }
}
