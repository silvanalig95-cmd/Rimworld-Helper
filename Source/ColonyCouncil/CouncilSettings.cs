using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum StakesMode
    {
        /// <summary>Granted wishes give mood; ignored ones cost nothing.</summary>
        Gentle,
        /// <summary>Granted wishes give mood; expired ones sting a little.</summary>
        Demanding,
        /// <summary>No mood effects at all. Pure suggestions.</summary>
        AdvisorOnly
    }

    public class CouncilSettings : ModSettings
    {
        // Layers
        public bool enableAdvisor = true;
        public bool enableWishes = true;
        public bool enableAmbitions = true;
        public bool enablePersonalTouches = true;

        // Wishes
        public StakesMode stakes = StakesMode.Gentle;
        public float daysBetweenWishes = 2f;
        public int maxOpenWishes = 5;
        public float wishDurationFactor = 1f;
        public float rewardFactor = 1f;
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

        // RimTalk
        public bool rimTalkContext = true;
        public bool rimTalkSpeech = true;

        private static Vector2 scroll;
        private static float viewHeight = 1200f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableAdvisor, "enableAdvisor", true);
            Scribe_Values.Look(ref enableWishes, "enableWishes", true);
            Scribe_Values.Look(ref enableAmbitions, "enableAmbitions", true);
            Scribe_Values.Look(ref enablePersonalTouches, "enablePersonalTouches", true);
            Scribe_Values.Look(ref stakes, "stakes", StakesMode.Gentle);
            Scribe_Values.Look(ref daysBetweenWishes, "daysBetweenWishes", 2f);
            Scribe_Values.Look(ref maxOpenWishes, "maxOpenWishes", 5);
            Scribe_Values.Look(ref wishDurationFactor, "wishDurationFactor", 1f);
            Scribe_Values.Look(ref rewardFactor, "rewardFactor", 1f);
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
            Scribe_Values.Look(ref rimTalkContext, "rimTalkContext", true);
            Scribe_Values.Look(ref rimTalkSpeech, "rimTalkSpeech", true);
        }

        public bool MoodEffects => stakes != StakesMode.AdvisorOnly;

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

            Header(l, "CC_Settings_RimTalk".Translate());
            l.Label(RimTalkBridge.Active ? "CC_Settings_RimTalkActive".Translate() : "CC_Settings_RimTalkInactive".Translate());
            l.CheckboxLabeled("CC_Settings_RimTalkContext".Translate(), ref rimTalkContext, "CC_Settings_RimTalkContext_Tip".Translate());
            l.CheckboxLabeled("CC_Settings_RimTalkSpeech".Translate(), ref rimTalkSpeech, "CC_Settings_RimTalkSpeech_Tip".Translate());

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
            rimTalkContext = d.rimTalkContext;
            rimTalkSpeech = d.rimTalkSpeech;
        }
    }
}
