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
        public bool allowQuitting = true;
        public bool risingStandards = true;
        /// <summary>Small good moments count with diminishing returns when several are on a colonist's mind (SmallJoys).</summary>
        public bool smallJoysDiminish = true;
        public bool enableDilemmas = true;
        public bool annals = true;
        public bool decreeRequests = true;
        public bool anniversaries = true;
        public bool epithets = true;
        public bool journals = true;
        public bool offices = true;
        public bool socialWeb = true;
        public bool socialEvents = true;
        public bool dreams = true;
        public bool aspirations = true;
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
        /// <summary>How hard the fights a Rimfolk scenario promises hit (share of their usual strength).</summary>
        public float scenarioThreat = 1f;
        /// <summary>How often the smaller things happen: 1 = as designed, 0.25 = rarely, 3 = often.</summary>
        public float encounterFrequency = 1f;
        public float momentFrequency = 1f;
        public float socialEventFrequency = 1f;
        public float dreamFrequency = 1f;
        /// <summary>The colony's character: what its choices make it known for.</summary>
        public bool colonyCharacter = true;
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
        public EraMode era = EraMode.Auto;
        public float daysBetweenDilemmas = 8f;
        public bool eventWishes = true;
        public bool reminders = true;
        public bool wishesForOthers = true;
        public bool petitions = true;
        public bool wishLetters = true;
        public bool grantedLetters = false;
        public bool speechText = true;

        // Meetings
        public float daysBetweenMeetings = 15f;
        public float ambitionDeadlineFactor = 1f;

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
        public ButtonPosition buttonPosition = ButtonPosition.BeforeMenu;
        public bool roomsButton = true;

        private static Vector2 scroll;
        private static float viewHeight = 1200f;

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
            Scribe_Values.Look(ref allowQuitting, "allowQuitting", true);
            Scribe_Values.Look(ref risingStandards, "risingStandards", true);
            Scribe_Values.Look(ref smallJoysDiminish, "smallJoysDiminish", true);
            Scribe_Values.Look(ref enableDilemmas, "enableDilemmas", true);
            Scribe_Values.Look(ref annals, "annals", true);
            Scribe_Values.Look(ref decreeRequests, "decreeRequests", true);
            Scribe_Values.Look(ref anniversaries, "anniversaries", true);
            Scribe_Values.Look(ref epithets, "epithets", true);
            Scribe_Values.Look(ref journals, "journals", true);
            Scribe_Values.Look(ref offices, "offices", true);
            Scribe_Values.Look(ref socialWeb, "socialWeb", true);
            Scribe_Values.Look(ref socialEvents, "socialEvents", true);
            Scribe_Values.Look(ref dreams, "dreams", true);
            Scribe_Values.Look(ref aspirations, "aspirations", true);
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
            Scribe_Values.Look(ref scenarioThreat, "scenarioThreat", 1f);
            Scribe_Values.Look(ref encounterFrequency, "encounterFrequency", 1f);
            Scribe_Values.Look(ref momentFrequency, "momentFrequency", 1f);
            Scribe_Values.Look(ref socialEventFrequency, "socialEventFrequency", 1f);
            Scribe_Values.Look(ref dreamFrequency, "dreamFrequency", 1f);
            Scribe_Values.Look(ref colonyCharacter, "colonyCharacter", true);
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
            Scribe_Values.Look(ref era, "era", EraMode.Auto);
            Scribe_Values.Look(ref daysBetweenDilemmas, "daysBetweenDilemmas", 8f);
            Scribe_Values.Look(ref eventWishes, "eventWishes", true);
            Scribe_Values.Look(ref reminders, "reminders", true);
            Scribe_Values.Look(ref wishesForOthers, "wishesForOthers", true);
            Scribe_Values.Look(ref petitions, "petitions", true);
            Scribe_Values.Look(ref wishLetters, "wishLetters", true);
            Scribe_Values.Look(ref grantedLetters, "grantedLetters", false);
            Scribe_Values.Look(ref speechText, "speechText", true);
            Scribe_Values.Look(ref daysBetweenMeetings, "daysBetweenMeetings", 15f);
            Scribe_Values.Look(ref ambitionDeadlineFactor, "ambitionDeadlineFactor", 1f);
            Scribe_Values.Look(ref daysBetweenPersonalTouches, "daysBetweenPersonalTouches", 6f);
            Scribe_Values.Look(ref advisorAlert, "advisorAlert", true);
            Scribe_Values.Look(ref wishAlert, "wishAlert", true);
            Scribe_Values.Look(ref planCutPlants, "planCutPlants", true);
            Scribe_Values.Look(ref planMineRock, "planMineRock", true);
            Scribe_Values.Look(ref planReplaceOwn, "planReplaceOwn", false);
            Scribe_Values.Look(ref planSmoothStone, "planSmoothStone", false);
            Scribe_Values.Look(ref planFurniture, "planFurniture", true);
            Scribe_Values.Look(ref buttonPosition, "buttonPosition", ButtonPosition.BeforeMenu);
            Scribe_Values.Look(ref roomsButton, "roomsButton", true);
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

        private static int settingsTab;

        public void DoWindowContents(Rect inRect)
        {
            // Four pages instead of one endless list.
            var tabs = new System.Collections.Generic.List<TabRecord>();
            string[] keys = { "CC_SetTab_General", "CC_SetTab_Council", "CC_SetTab_People", "CC_SetTab_Notify" };
            for (int i = 0; i < keys.Length; i++)
            {
                int index = i;
                tabs.Add(new TabRecord(keys[i].Translate(), () => { settingsTab = index; scroll = Vector2.zero; }, settingsTab == i));
            }
            var body = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs, 200f);
            var inner = body.ContractedBy(10f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 20f, viewHeight);
            Widgets.BeginScrollView(inner, ref scroll, viewRect);
            var l = new Listing_Standard();
            l.Begin(viewRect);
            switch (settingsTab)
            {
                case 0: GeneralPage(l); break;
                case 1: CouncilPage(l); break;
                case 2: PeoplePage(l); break;
                default: NotifyPage(l); break;
            }
            viewHeight = l.CurHeight + 30f;
            l.End();
            Widgets.EndScrollView();
        }

        private void GeneralPage(Listing_Standard l)
        {
            Header(l, "CC_Settings_Presets".Translate());
            l.Label("CC_Settings_Presets_Tip".Translate());
            var row = l.GetRect(30f);
            if (Widgets.ButtonText(new Rect(row.x, row.y, row.width / 3f - 6f, 30f), "CC_Preset_Everything".Translate())) SetPeople(true, true);
            if (Widgets.ButtonText(new Rect(row.x + row.width / 3f, row.y, row.width / 3f - 6f, 30f), "CC_Preset_Core".Translate())) SetPeople(false, true);
            if (Widgets.ButtonText(new Rect(row.x + row.width * 2f / 3f, row.y, row.width / 3f - 6f, 30f), "CC_Preset_AdvisorOnly".Translate()))
            {
                SetPeople(false, false);
                stakes = StakesMode.AdvisorOnly;
            }
            l.Gap();

            // The era belongs to each colony, not to every game: a medieval save and an industrial one can't share it.
            Header(l, "CC_Settings_Era".Translate());
            var colony = Current.ProgramState == ProgramState.Playing ? CouncilGameComponent.Instance : null;
            if (colony == null) l.Label("CC_Settings_EraNoGame".Translate());
            else
                foreach (EraMode mode in System.Enum.GetValues(typeof(EraMode)))
                    if (l.RadioButton(("CC_Era_" + mode).Translate(), colony.era == mode, 0f, ("CC_Era_" + mode + "_Tip").Translate()))
                    {
                        colony.era = mode;
                        Avail.ResetCache();
                    }

            Header(l, "CC_Settings_Layers".Translate());
            l.CheckboxLabeled("CC_Settings_EnableAdvisor".Translate(), ref enableAdvisor, "CC_Settings_EnableAdvisor_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_EnableWishes".Translate(), ref enableWishes, "CC_Settings_EnableWishes_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_EnableAmbitions".Translate(), ref enableAmbitions, "CC_Settings_EnableAmbitions_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_EnableTouches".Translate(), ref enablePersonalTouches, "CC_Settings_EnableTouches_Tip".Translate());

            Header(l, "CC_Settings_Stakes".Translate());
            if (l.RadioButton("CC_Stakes_Gentle".Translate(), stakes == StakesMode.Gentle, 0f, "CC_Stakes_Gentle_Tip".Translate()))
                stakes = StakesMode.Gentle;
            if (l.RadioButton("CC_Stakes_Balanced".Translate(), stakes == StakesMode.Balanced, 0f, "CC_Stakes_Balanced_Tip".Translate()))
                stakes = StakesMode.Balanced;
            if (l.RadioButton("CC_Stakes_Demanding".Translate(), stakes == StakesMode.Demanding, 0f, "CC_Stakes_Demanding_Tip".Translate()))
                stakes = StakesMode.Demanding;
            if (l.RadioButton("CC_Stakes_AdvisorOnly".Translate(), stakes == StakesMode.AdvisorOnly, 0f, "CC_Stakes_AdvisorOnly_Tip".Translate()))
                stakes = StakesMode.AdvisorOnly;
            if (Negatives)
            {
                penaltyFactor = l.SliderLabeled("CC_Settings_Penalty".Translate(penaltyFactor.ToStringPercent()), penaltyFactor, 0.25f, 2f, 0.6f);
                penaltyFactor = Mathf.Round(penaltyFactor * 20f) / 20f;
            }
            rewardFactor = l.SliderLabeled("CC_Settings_Reward".Translate(rewardFactor.ToStringPercent()), rewardFactor, 0.25f, 2f, 0.6f);
            rewardFactor = Mathf.Round(rewardFactor * 20f) / 20f;

            l.Gap();
            if (l.ButtonText("CC_Settings_Reset".Translate(), null, 0.4f)) ResetToDefaults();
        }

        private void CouncilPage(Listing_Standard l)
        {
            Header(l, "CC_Settings_Wishes".Translate());
            daysBetweenWishes = l.SliderLabeled("CC_Settings_DaysBetweenWishes".Translate(daysBetweenWishes.ToString("0.0")), daysBetweenWishes, 0.5f, 10f, 0.6f);
            daysBetweenWishes = Mathf.Round(daysBetweenWishes * 2f) / 2f;
            float maxF = l.SliderLabeled("CC_Settings_MaxOpenWishes".Translate(maxOpenWishes), maxOpenWishes, 1f, 12f, 0.6f);
            maxOpenWishes = Mathf.RoundToInt(maxF);
            wishDurationFactor = l.SliderLabeled("CC_Settings_WishDuration".Translate(wishDurationFactor.ToStringPercent()), wishDurationFactor, 0.5f, 3f, 0.6f);
            wishDurationFactor = Mathf.Round(wishDurationFactor * 20f) / 20f;
            if (Negatives) l.CheckboxLabeled("CC_Settings_Jealousy".Translate(), ref jealousy, "CC_Settings_Jealousy_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_EventWishes".Translate(), ref eventWishes, "CC_Settings_EventWishes_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Reminders".Translate(), ref reminders, "CC_Settings_Reminders_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SelfHelp".Translate(), ref selfHelp, "CC_Settings_SelfHelp_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Quitting".Translate(), ref allowQuitting, "CC_Settings_Quitting_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Standards".Translate(), ref risingStandards, "CC_Settings_Standards_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SmallJoys".Translate(), ref smallJoysDiminish, "CC_Settings_SmallJoys_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_ForOthers".Translate(), ref wishesForOthers, "CC_Settings_ForOthers_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Petitions".Translate(), ref petitions, "CC_Settings_Petitions_Tip".Translate());

            Header(l, "CC_Settings_CouncilLife".Translate());
            l.CheckboxLabeled("CC_Settings_Dilemmas".Translate(), ref enableDilemmas, "CC_Settings_Dilemmas_Tip".Translate());
            if (enableDilemmas)
            {
                daysBetweenDilemmas = l.SliderLabeled("CC_Settings_DaysBetweenDilemmas".Translate(daysBetweenDilemmas.ToString("0")), daysBetweenDilemmas, 3f, 30f, 0.6f);
                daysBetweenDilemmas = Mathf.Round(daysBetweenDilemmas);
            }
            l.CheckboxLabeled("CC_Settings_DecreeRequests".Translate(), ref decreeRequests, "CC_Settings_DecreeRequests_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Character".Translate(), ref colonyCharacter, "CC_Settings_Character_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Offices".Translate(), ref offices, "CC_Settings_Offices_Tip".Translate());

            Header(l, "CC_Settings_Meetings".Translate());
            daysBetweenMeetings = l.SliderLabeled("CC_Settings_DaysBetweenMeetings".Translate(daysBetweenMeetings.ToString("0")), daysBetweenMeetings, 5f, 60f, 0.6f);
            daysBetweenMeetings = Mathf.Round(daysBetweenMeetings);
            ambitionDeadlineFactor = l.SliderLabeled("CC_Settings_AmbitionDeadline".Translate(ambitionDeadlineFactor.ToStringPercent()), ambitionDeadlineFactor, 0.5f, 3f, 0.6f);
            ambitionDeadlineFactor = Mathf.Round(ambitionDeadlineFactor * 20f) / 20f;

            Header(l, "CC_Settings_Touches".Translate());
            daysBetweenPersonalTouches = l.SliderLabeled("CC_Settings_DaysBetweenTouches".Translate(daysBetweenPersonalTouches.ToString("0")), daysBetweenPersonalTouches, 1f, 30f, 0.6f);
            daysBetweenPersonalTouches = Mathf.Round(daysBetweenPersonalTouches);
        }

        private void PeoplePage(Listing_Standard l)
        {
            var row = l.GetRect(30f);
            if (Widgets.ButtonText(new Rect(row.x, row.y, 180f, 30f), "CC_Settings_AllOn".Translate())) SetPeople(true, true);
            if (Widgets.ButtonText(new Rect(row.x + 190f, row.y, 180f, 30f), "CC_Settings_AllOff".Translate())) SetPeople(false, true);
            l.Gap();

            Header(l, "CC_Settings_Bonds".Translate());
            l.CheckboxLabeled("CC_Settings_SocialWeb".Translate(), ref socialWeb, "CC_Settings_SocialWeb_Tip".Translate());
            if (socialWeb) l.CheckboxLabeled("CC_Settings_SocialEvents".Translate(), ref socialEvents, "CC_Settings_SocialEvents_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Secrets".Translate(), ref secrets, "CC_Settings_Secrets_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Vendettas".Translate(), ref vendettas, "CC_Settings_Vendettas_Tip".Translate());

            Header(l, "CC_Settings_Lives".Translate());
            l.CheckboxLabeled("CC_Settings_Aspirations".Translate(), ref aspirations, "CC_Settings_Aspirations_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Quirks".Translate(), ref quirks, "CC_Settings_Quirks_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Heirlooms".Translate(), ref heirlooms, "CC_Settings_Heirlooms_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_ComingOfAge".Translate(), ref comingOfAge, "CC_Settings_ComingOfAge_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Dreams".Translate(), ref dreams, "CC_Settings_Dreams_Tip".Translate());
            if (dreams) l.CheckboxLabeled("CC_Settings_DreamOmens".Translate(), ref dreamOmens, "CC_Settings_DreamOmens_Tip".Translate());

            Header(l, "CC_Settings_Strangers".Translate());
            l.CheckboxLabeled("CC_Settings_Encounters".Translate(), ref strangers, "CC_Settings_Encounters_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Nemeses".Translate(), ref nemeses, "CC_Settings_Nemeses_Tip".Translate());
            if (nemeses) l.CheckboxLabeled("CC_Settings_NemesisReturns".Translate(), ref nemesisReturns, "CC_Settings_NemesisReturns_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_PetMoments".Translate(), ref petMoments, "CC_Settings_PetMoments_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Moments".Translate(), ref colonistMoments, "CC_Settings_Moments_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Frictions".Translate(), ref frictions, "CC_Settings_Frictions_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Autonomy".Translate(), ref autonomy, "CC_Settings_Autonomy_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_WorldReactions".Translate(), ref worldReactions, "CC_Settings_WorldReactions_Tip".Translate());
            if (autonomy) l.CheckboxLabeled("  " + "CC_Settings_AutonomyPlans".Translate(), ref autonomyPlans, "CC_Settings_AutonomyPlans_Tip".Translate());

            Header(l, "CC_Settings_WorldEventsHeader".Translate());
            l.CheckboxLabeled("CC_Settings_WorldEvents".Translate(), ref worldEvents, "CC_Settings_WorldEvents_Tip".Translate());
            if (worldEvents)
            {
                l.CheckboxLabeled("  " + "CC_Settings_WorldEventBattles".Translate(), ref worldEventBattles, "CC_Settings_WorldEventBattles_Tip".Translate());
                l.CheckboxLabeled("  " + "CC_Settings_Camps".Translate(), ref camps, "CC_Settings_Camps_Tip".Translate());
                l.CheckboxLabeled("  " + "CC_Settings_MissionsAsQuests".Translate(), ref missionsAsQuests, "CC_Settings_MissionsAsQuests_Tip".Translate());
                bool fromStoryteller = eventsFromStoryteller;
                l.CheckboxLabeled("  " + "CC_Settings_EventsFromStoryteller".Translate(), ref eventsFromStoryteller, "CC_Settings_EventsFromStoryteller_Tip".Translate());
                if (fromStoryteller != eventsFromStoryteller) HappeningTracker.ApplyEventMode();
                if (eventsFromStoryteller)
                {
                    storytellerEventWeight = l.SliderLabeled("CC_Settings_StorytellerEventWeight".Translate(storytellerEventWeight.ToStringPercent()), storytellerEventWeight, 0.25f, 2f, 0.6f,
                        "CC_Settings_StorytellerEventWeight_Tip".Translate());
                    storytellerEventWeight = Mathf.Round(storytellerEventWeight * 20f) / 20f;
                }
                else
                {
                    daysBetweenWorldEvents = l.SliderLabeled("CC_Settings_DaysBetweenWorldEvents".Translate(daysBetweenWorldEvents.ToString("0.#")), daysBetweenWorldEvents, 1f, 20f, 0.6f,
                        "CC_Settings_DaysBetweenWorldEvents_Tip".Translate());
                    daysBetweenWorldEvents = Mathf.Round(daysBetweenWorldEvents * 2f) / 2f;
                }
            }
            scenarioThreat = l.SliderLabeled("CC_Settings_ScenarioThreat".Translate(scenarioThreat.ToStringPercent()), scenarioThreat, 0.25f, 2f, 0.6f,
                "CC_Settings_ScenarioThreat_Tip".Translate());
            scenarioThreat = Mathf.Round(scenarioThreat * 4f) / 4f;

            Header(l, "CC_Settings_WorldSituationsHeader".Translate());
            l.CheckboxLabeled("CC_Settings_WorldSituations".Translate(), ref worldSituations, "CC_Settings_WorldSituations_Tip".Translate());
            if (worldSituations)
            {
                daysBetweenWorldSituations = l.SliderLabeled("CC_Settings_DaysBetweenWorldSituations".Translate(daysBetweenWorldSituations.ToString("0")), daysBetweenWorldSituations, 15f, 120f, 0.6f,
                    "CC_Settings_DaysBetweenWorldSituations_Tip".Translate());
                daysBetweenWorldSituations = Mathf.Round(daysBetweenWorldSituations / 5f) * 5f;
            }

            Header(l, "CC_Settings_HowOften".Translate());
            encounterFrequency = Frequency(l, "CC_Settings_FreqEncounters", encounterFrequency);
            momentFrequency = Frequency(l, "CC_Settings_FreqMoments", momentFrequency);
            socialEventFrequency = Frequency(l, "CC_Settings_FreqSocial", socialEventFrequency);
            dreamFrequency = Frequency(l, "CC_Settings_FreqDreams", dreamFrequency);

            Header(l, "CC_Settings_Memory".Translate());
            l.CheckboxLabeled("CC_Settings_Journals".Translate(), ref journals, "CC_Settings_Journals_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Epithets".Translate(), ref epithets, "CC_Settings_Epithets_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Anniversaries".Translate(), ref anniversaries, "CC_Settings_Anniversaries_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Annals".Translate(), ref annals, "CC_Settings_Annals_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Eulogies".Translate(), ref eulogies, "CC_Settings_Eulogies_Tip".Translate());
        }

        private void NotifyPage(Listing_Standard l)
        {
            Header(l, "CC_Settings_Messages".Translate());
            foreach (MessageLevel level in System.Enum.GetValues(typeof(MessageLevel)))
                if (l.RadioButton(("CC_MsgLevel_" + level).Translate(), messageLevel == level, 0f, ("CC_MsgLevel_" + level + "_Tip").Translate()))
                    messageLevel = level;
            if (dreams) l.CheckboxLabeled("CC_Settings_AllDreamMessages".Translate(), ref allDreamMessages, "CC_Settings_AllDreamMessages_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_WishLetters".Translate(), ref wishLetters, "CC_Settings_WishLetters_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_GrantedLetters".Translate(), ref grantedLetters, "CC_Settings_GrantedLetters_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SpeechText".Translate(), ref speechText, "CC_Settings_SpeechText_Tip".Translate());

            Header(l, "CC_Settings_Alerts".Translate());
            l.CheckboxLabeled("CC_Settings_AdvisorAlert".Translate(), ref advisorAlert, "CC_Settings_AdvisorAlert_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_WishAlert".Translate(), ref wishAlert, "CC_Settings_WishAlert_Tip".Translate());

            Header(l, "CC_Settings_Interface".Translate());
            l.CheckboxLabeled("CC_Settings_MoodInsight".Translate(), ref moodInsight, "CC_Settings_MoodInsight_Tip".Translate());
            if (moodInsight) l.CheckboxLabeled("  " + "CC_Settings_NeedsTabHints".Translate(), ref needsTabHints, "CC_Settings_NeedsTabHints_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_DeepHooks".Translate(), ref deepHooks, "CC_Settings_DeepHooks_Tip".Translate(RimfolkPatches.Applied.Count, RimfolkPatches.Failed.Count));
            if (deepHooks) l.CheckboxLabeled("  " + "CC_Settings_LetterNotes".Translate(), ref letterNotes, "CC_Settings_LetterNotes_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SlowButtons".Translate(), ref slowButtons, "CC_Settings_SlowButtons_Tip".Translate());
            if (slowButtons && (slowButtonsX >= 0f || slowButtonsY >= 0f) && l.ButtonText("CC_Settings_SlowButtonsReset".Translate()))
                slowButtonsX = slowButtonsY = -1f;
            if (l.ButtonTextLabeled("CC_Settings_ButtonPosition".Translate(), ("CC_ButtonPos_" + buttonPosition).Translate()))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (ButtonPosition pos in System.Enum.GetValues(typeof(ButtonPosition)))
                {
                    var local = pos;
                    options.Add(new FloatMenuOption(("CC_ButtonPos_" + pos).Translate(), () =>
                    {
                        buttonPosition = local;
                        CouncilStartup.ApplyButtonPosition(true);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            l.Label("CC_Settings_ButtonPosition_Note".Translate(), -1f);
            bool rooms = roomsButton;
            l.CheckboxLabeled("CC_Settings_RoomsButton".Translate(), ref rooms, "CC_Settings_RoomsButton_Tip".Translate());
            if (rooms != roomsButton)
            {
                roomsButton = rooms;
                CouncilStartup.ApplyButtonPosition(true);
            }
        }

        /// <summary>The "people" systems all on or all off; councilToo also covers dilemmas, offices and petitions.</summary>
        private void SetPeople(bool on, bool councilOn)
        {
            socialWeb = socialEvents = secrets = vendettas = aspirations = quirks = heirlooms = comingOfAge = dreams = dreamOmens = eulogies = on;
            strangers = nemeses = nemesisReturns = petMoments = colonistMoments = frictions = autonomy = worldReactions = worldEvents = worldSituations = on;
            enableDilemmas = decreeRequests = offices = colonyCharacter = councilOn;
            enableWishes = enableAmbitions = councilOn || on;
            journals = epithets = anniversaries = annals = on || councilOn;
            if (councilOn && stakes == StakesMode.AdvisorOnly) stakes = StakesMode.Balanced;
        }

        /// <summary>A "how often" slider: a quarter as often to three times as often.</summary>
        private static float Frequency(Listing_Standard l, string key, float value)
        {
            float v = l.SliderLabeled(key.Translate(value.ToStringPercent()), value, 0.25f, 3f, 0.6f, (key + "_Tip").Translate());
            v = Mathf.Round(v * 4f) / 4f;
            return v;
        }

        private static void Header(Listing_Standard l, string text)
        {
            l.Gap(10f);
            Text.Font = GameFont.Medium;
            l.Label(text);
            Text.Font = GameFont.Small;
            l.GapLine(4f);
        }

        private void ResetToDefaults()
        {
            var d = new CouncilSettings();
            enableAdvisor = d.enableAdvisor;
            enableWishes = d.enableWishes;
            enableAmbitions = d.enableAmbitions;
            enablePersonalTouches = d.enablePersonalTouches;
            stakes = d.stakes;
            daysBetweenWishes = d.daysBetweenWishes;
            maxOpenWishes = d.maxOpenWishes;
            wishDurationFactor = d.wishDurationFactor;
            rewardFactor = d.rewardFactor;
            penaltyFactor = d.penaltyFactor;
            jealousy = d.jealousy;
            selfHelp = d.selfHelp;
            allowQuitting = d.allowQuitting;
            risingStandards = d.risingStandards;
            smallJoysDiminish = d.smallJoysDiminish;
            enableDilemmas = d.enableDilemmas;
            annals = d.annals;
            decreeRequests = d.decreeRequests;
            anniversaries = d.anniversaries;
            epithets = d.epithets;
            journals = d.journals;
            offices = d.offices;
            socialWeb = d.socialWeb;
            socialEvents = d.socialEvents;
            dreams = d.dreams;
            aspirations = d.aspirations;
            secrets = d.secrets;
            vendettas = d.vendettas;
            eulogies = d.eulogies;
            quirks = d.quirks;
            heirlooms = d.heirlooms;
            comingOfAge = d.comingOfAge;
            strangers = d.strangers;
            nemeses = d.nemeses;
            nemesisReturns = d.nemesisReturns;
            petMoments = d.petMoments;
            colonistMoments = d.colonistMoments;
            frictions = d.frictions;
            autonomy = d.autonomy;
            worldReactions = d.worldReactions;
            autonomyPlans = d.autonomyPlans;
            worldEvents = d.worldEvents;
            worldEventBattles = d.worldEventBattles;
            camps = d.camps;
            missionsAsQuests = d.missionsAsQuests;
            daysBetweenWorldEvents = d.daysBetweenWorldEvents;
            eventsFromStoryteller = d.eventsFromStoryteller;
            storytellerEventWeight = d.storytellerEventWeight;
            worldSituations = d.worldSituations;
            daysBetweenWorldSituations = d.daysBetweenWorldSituations;
            scenarioThreat = d.scenarioThreat;
            encounterFrequency = d.encounterFrequency;
            momentFrequency = d.momentFrequency;
            socialEventFrequency = d.socialEventFrequency;
            dreamFrequency = d.dreamFrequency;
            colonyCharacter = d.colonyCharacter;
            moodInsight = d.moodInsight;
            deepHooks = d.deepHooks;
            needsTabHints = d.needsTabHints;
            letterNotes = d.letterNotes;
            dreamOmens = d.dreamOmens;
            allDreamMessages = d.allDreamMessages;
            messageLevel = d.messageLevel;
            era = d.era;
            daysBetweenDilemmas = d.daysBetweenDilemmas;
            eventWishes = d.eventWishes;
            reminders = d.reminders;
            wishesForOthers = d.wishesForOthers;
            petitions = d.petitions;
            wishLetters = d.wishLetters;
            grantedLetters = d.grantedLetters;
            speechText = d.speechText;
            daysBetweenMeetings = d.daysBetweenMeetings;
            ambitionDeadlineFactor = d.ambitionDeadlineFactor;
            daysBetweenPersonalTouches = d.daysBetweenPersonalTouches;
            advisorAlert = d.advisorAlert;
            wishAlert = d.wishAlert;
            planCutPlants = d.planCutPlants;
            planMineRock = d.planMineRock;
            planReplaceOwn = d.planReplaceOwn;
            planSmoothStone = d.planSmoothStone;
            planFurniture = d.planFurniture;
            if (buttonPosition != d.buttonPosition || roomsButton != d.roomsButton)
            {
                buttonPosition = d.buttonPosition;
                roomsButton = d.roomsButton;
                CouncilStartup.ApplyButtonPosition(true);
            }
        }
    }
}
