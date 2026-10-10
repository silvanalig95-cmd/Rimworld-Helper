using UnityEngine;
using Verse;

namespace ColonyCouncil
{
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
        public bool enableDilemmas = true;
        public bool annals = true;
        public bool decreeRequests = true;
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
            Scribe_Values.Look(ref enableDilemmas, "enableDilemmas", true);
            Scribe_Values.Look(ref annals, "annals", true);
            Scribe_Values.Look(ref decreeRequests, "decreeRequests", true);
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

        public void DoWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref scroll, viewRect);
            var l = new Listing_Standard();
            l.Begin(viewRect);

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

            Header(l, "CC_Settings_Wishes".Translate());
            daysBetweenWishes = l.SliderLabeled("CC_Settings_DaysBetweenWishes".Translate(daysBetweenWishes.ToString("0.0")), daysBetweenWishes, 0.5f, 10f, 0.6f);
            daysBetweenWishes = Mathf.Round(daysBetweenWishes * 2f) / 2f;
            float maxF = l.SliderLabeled("CC_Settings_MaxOpenWishes".Translate(maxOpenWishes), maxOpenWishes, 1f, 12f, 0.6f);
            maxOpenWishes = Mathf.RoundToInt(maxF);
            wishDurationFactor = l.SliderLabeled("CC_Settings_WishDuration".Translate(wishDurationFactor.ToStringPercent()), wishDurationFactor, 0.5f, 3f, 0.6f);
            wishDurationFactor = Mathf.Round(wishDurationFactor * 20f) / 20f;
            rewardFactor = l.SliderLabeled("CC_Settings_Reward".Translate(rewardFactor.ToStringPercent()), rewardFactor, 0.25f, 2f, 0.6f);
            rewardFactor = Mathf.Round(rewardFactor * 20f) / 20f;
            if (Negatives)
            {
                penaltyFactor = l.SliderLabeled("CC_Settings_Penalty".Translate(penaltyFactor.ToStringPercent()), penaltyFactor, 0.25f, 2f, 0.6f);
                penaltyFactor = Mathf.Round(penaltyFactor * 20f) / 20f;
                l.CheckboxLabeled("CC_Settings_Jealousy".Translate(), ref jealousy, "CC_Settings_Jealousy_Tip".Translate());
            }
            l.CheckboxLabeled("CC_Settings_EventWishes".Translate(), ref eventWishes, "CC_Settings_EventWishes_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Reminders".Translate(), ref reminders, "CC_Settings_Reminders_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SelfHelp".Translate(), ref selfHelp, "CC_Settings_SelfHelp_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Quitting".Translate(), ref allowQuitting, "CC_Settings_Quitting_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Standards".Translate(), ref risingStandards, "CC_Settings_Standards_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Annals".Translate(), ref annals, "CC_Settings_Annals_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_DecreeRequests".Translate(), ref decreeRequests, "CC_Settings_DecreeRequests_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Dilemmas".Translate(), ref enableDilemmas, "CC_Settings_Dilemmas_Tip".Translate());
            if (enableDilemmas)
            {
                daysBetweenDilemmas = l.SliderLabeled("CC_Settings_DaysBetweenDilemmas".Translate(daysBetweenDilemmas.ToString("0")), daysBetweenDilemmas, 3f, 30f, 0.6f);
                daysBetweenDilemmas = Mathf.Round(daysBetweenDilemmas);
            }
            l.CheckboxLabeled("CC_Settings_ForOthers".Translate(), ref wishesForOthers, "CC_Settings_ForOthers_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_Petitions".Translate(), ref petitions, "CC_Settings_Petitions_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_WishLetters".Translate(), ref wishLetters, "CC_Settings_WishLetters_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_GrantedLetters".Translate(), ref grantedLetters, "CC_Settings_GrantedLetters_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_SpeechText".Translate(), ref speechText, "CC_Settings_SpeechText_Tip".Translate());

            Header(l, "CC_Settings_Meetings".Translate());
            daysBetweenMeetings = l.SliderLabeled("CC_Settings_DaysBetweenMeetings".Translate(daysBetweenMeetings.ToString("0")), daysBetweenMeetings, 5f, 60f, 0.6f);
            daysBetweenMeetings = Mathf.Round(daysBetweenMeetings);
            ambitionDeadlineFactor = l.SliderLabeled("CC_Settings_AmbitionDeadline".Translate(ambitionDeadlineFactor.ToStringPercent()), ambitionDeadlineFactor, 0.5f, 3f, 0.6f);
            ambitionDeadlineFactor = Mathf.Round(ambitionDeadlineFactor * 20f) / 20f;

            Header(l, "CC_Settings_Touches".Translate());
            daysBetweenPersonalTouches = l.SliderLabeled("CC_Settings_DaysBetweenTouches".Translate(daysBetweenPersonalTouches.ToString("0")), daysBetweenPersonalTouches, 1f, 30f, 0.6f);
            daysBetweenPersonalTouches = Mathf.Round(daysBetweenPersonalTouches);

            Header(l, "CC_Settings_Alerts".Translate());
            l.CheckboxLabeled("CC_Settings_AdvisorAlert".Translate(), ref advisorAlert, "CC_Settings_AdvisorAlert_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_WishAlert".Translate(), ref wishAlert, "CC_Settings_WishAlert_Tip".Translate());

            Header(l, "CC_Settings_Interface".Translate());
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

            l.Gap();
            if (l.ButtonText("CC_Settings_Reset".Translate(), null, 0.4f))
            {
                ResetToDefaults();
            }

            viewHeight = l.CurHeight + 30f;
            l.End();
            Widgets.EndScrollView();
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
            enableDilemmas = d.enableDilemmas;
            annals = d.annals;
            decreeRequests = d.decreeRequests;
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
