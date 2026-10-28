using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The pages of the mod settings window, in the order of the list on its left.</summary>
    public enum SettingsPage
    {
        Council,
        People,
        HeartHealth,
        Events,
        Building,
        Interface,
        Advanced
    }

    public enum SettingKind
    {
        /// <summary>A switch: a bool field.</summary>
        Toggle,
        /// <summary>A slider: a float or int field.</summary>
        Slider,
        /// <summary>A row of buttons, one per option: an enum field.</summary>
        Choice,
        /// <summary>A button that opens a menu of the options: an enum field with long option names.</summary>
        Dropdown,
        /// <summary>Buttons that do something (presets, resets, checks); drawn by the window itself.</summary>
        Action
    }

    /// <summary>
    /// One row of the settings window: which field of <c>CouncilSettings</c> it draws, on which page and under which
    /// heading, its label, tooltip and one-line "what you'll notice", and what it needs switched on to matter.
    /// </summary>
    public sealed class SettingEntry
    {
        /// <summary>The field of CouncilSettings this row draws (its Scribe key too), or a name of its own for an action.</summary>
        public string id;
        public SettingsPage page;
        /// <summary>The heading it sits under (a translation key).</summary>
        public string section;
        public SettingKind kind;
        public string label;
        public string tip;
        /// <summary>The one line under it: what the player will notice with it on (a translation key).</summary>
        public string note;
        /// <summary>Choices: each option has its own line, this prefix plus the option's name.</summary>
        public string notePrefix;
        /// <summary>
        /// What it needs to matter, as terms that must all hold: <c>heart</c> (switched on), <c>!eventsFromStoryteller</c>
        /// (switched off), <c>stakes=Balanced|Demanding</c> or <c>stakes!=AdvisorOnly</c>. Unmet, the row is grayed out.
        /// </summary>
        public string needs;
        /// <summary>The line shown instead of the note while <see cref="needs"/> isn't met; without it, "Needs X switched on".</summary>
        public string whyOff;
        public float min, max, step;
        /// <summary>How a slider shows its value: "%" for a percentage, else a number format ("0", "0.0", "0.#").</summary>
        public string format;
        /// <summary>Choices and dropdowns: the option label is this prefix plus the option's name, its tooltip that plus "_Tip".</summary>
        public string optionPrefix;
        public string[] options;
        /// <summary>Other fields this row sets (the slow speed buttons' position, put back by a button).</summary>
        public string[] covers;
        /// <summary>Not a field of CouncilSettings: an action, or something kept elsewhere (the colony's era).</summary>
        public bool notAField;

        public bool IsField => !notAField;
        public bool Indented => Parent != null;

        /// <summary>The switch this row hangs under: the first plain "switched on" term of <see cref="needs"/>.</summary>
        public string Parent => SettingsCatalog.ParseNeeds(needs).FirstOrDefault(t => t.values == null && !t.negate).field;

        public override string ToString() => id;
    }

    /// <summary>One term of <see cref="SettingEntry.needs"/>.</summary>
    public struct NeedTerm
    {
        public string field;
        /// <summary>A switch that must be off, or an enum that must not be one of <see cref="values"/>.</summary>
        public bool negate;
        /// <summary>For an enum field, the option names; null for a switch.</summary>
        public string[] values;
    }

    /// <summary>A value a preset sets: the field, the value as text ("true", "0.75", "Demanding" or "default").</summary>
    public struct PresetValue
    {
        public string field;
        public string value;
        /// <summary>Only when the field is now one of these values (option names separated by |); null for always.</summary>
        public string onlyIfNow;

        public PresetValue(string field, string value, string onlyIfNow = null)
        {
            this.field = field;
            this.value = value;
            this.onlyIfNow = onlyIfNow;
        }
    }

    /// <summary>A one-click setup: a documented list of values. Everything it doesn't list stays as it is.</summary>
    public sealed class SettingsPreset
    {
        public string id;
        public string label;
        public string tip;
        /// <summary>The "what's switched on" setups on the Council page (as opposed to the left-hand style presets).</summary>
        public bool scope;
        public List<PresetValue> values = new List<PresetValue>();

        public IEnumerable<string> Fields => values.Select(v => v.field).Distinct();
    }

    /// <summary>
    /// Every setting the window draws, page by page, with no game types in it so Tools/Tests can check it against
    /// CouncilSettings: each public field is drawn on exactly one page (or listed in <see cref="NotDrawn"/> with the
    /// reason), and each preset sets only fields that exist, to values the sliders allow.
    /// </summary>
    public static class SettingsCatalog
    {
        public const string DefaultValue = "default";

        private static readonly List<SettingEntry> all = new List<SettingEntry>();
        private static readonly List<SettingsPreset> presets = new List<SettingsPreset>();
        private static readonly Dictionary<string, string> sectionNotes = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<NeedTerm>> parsed = new Dictionary<string, List<NeedTerm>>();

        /// <summary>Public fields of CouncilSettings that aren't settings, and why.</summary>
        public static readonly Dictionary<string, string> NotDrawn = new Dictionary<string, string>
        {
            ["era"] = "kept only so old settings files load; the era belongs to each colony now (the Era row on the Advanced page)",
            ["savedRooms"] = "the rooms the player saved from a map, managed in the room planner",
            ["planMaterials"] = "the materials last picked in the room planner",
            ["lastCouncilPage"] = "the council window's page when it was last closed",
            ["lastAlmanacPage"] = "the Almanac's page when it was last closed",
            ["almanacFolded"] = "whether the Almanac's categories and page list are folded away, switched in the Almanac itself",
            ["moodDetails"] = "whether the Mood tab and the Moods page show every mood as a card, switched on those pages (Compact / Details)",
            ["hudVersion"] = "the on-screen panels' settings version, so a change to older settings is made once (HudLayout.Migrate)",
            ["hudPanels"] = "each on-screen panel's place and options, set on the panels themselves (their menu) or with the buttons under \"On-screen panels\"",
            ["councilPageState"] = "the filters and sort orders the council's pages remember, set on the pages themselves",
            ["lastSeenVersion"] = "the Rimfolk version the player last saw, so the letter after an update comes once (the row \"What's new letter\")",
            ["speechText"] = "the old switch for colonists speaking, kept in step with speechShows (the row \"Colonists' speech shows as\") so older settings files load"
        };

        public static IReadOnlyList<SettingEntry> All => all;
        public static IReadOnlyList<SettingsPreset> Presets => presets;
        public static IEnumerable<SettingsPreset> StylePresets => presets.Where(p => !p.scope);
        public static IEnumerable<SettingsPreset> ScopePresets => presets.Where(p => p.scope);
        public static readonly SettingsPage[] Pages = (SettingsPage[])Enum.GetValues(typeof(SettingsPage));

        public static IEnumerable<SettingEntry> On(SettingsPage page) => all.Where(e => e.page == page);
        public static SettingEntry Find(string id) => all.FirstOrDefault(e => e.id == id);
        public static SettingsPreset Preset(string id) => presets.FirstOrDefault(p => p.id == id);

        /// <summary>The fields a row sets: its own and any it covers.</summary>
        public static IEnumerable<string> FieldsOf(SettingEntry e)
        {
            if (e.IsField) yield return e.id;
            if (e.covers != null)
                foreach (var c in e.covers) yield return c;
        }

        /// <summary>Every field the window draws, once per row that draws it.</summary>
        public static IEnumerable<string> DrawnFields => all.SelectMany(FieldsOf);

        /// <summary>A grey line under a heading, or null.</summary>
        public static string SectionNote(string section) => section != null && sectionNotes.TryGetValue(section, out var n) ? n : null;

        public static string PageKey(SettingsPage p) => "CC_SetPage_" + p;
        public static string PageDescKey(SettingsPage p) => "CC_SetPage_" + p + "_Desc";

        public static List<NeedTerm> ParseNeeds(string needs)
        {
            if (string.IsNullOrEmpty(needs)) return new List<NeedTerm>();
            lock (parsed)
            {
                if (parsed.TryGetValue(needs, out var terms)) return terms;
                terms = new List<NeedTerm>();
                foreach (var raw in needs.Split(','))
                {
                    string t = raw.Trim();
                    if (t.Length == 0) continue;
                    int ne = t.IndexOf("!=", StringComparison.Ordinal);
                    int eq = t.IndexOf('=');
                    if (ne > 0) terms.Add(new NeedTerm { field = t.Substring(0, ne), negate = true, values = t.Substring(ne + 2).Split('|') });
                    else if (eq > 0) terms.Add(new NeedTerm { field = t.Substring(0, eq), values = t.Substring(eq + 1).Split('|') });
                    else if (t[0] == '!') terms.Add(new NeedTerm { field = t.Substring(1), negate = true });
                    else terms.Add(new NeedTerm { field = t });
                }
                parsed[needs] = terms;
                return terms;
            }
        }

        static SettingsCatalog()
        {
            Build();
            BuildPresets();
        }

        // ---------------- The pages ----------------

        private static SettingsPage curPage;
        private static string curSection;

        private static void Build()
        {
            // Council and wishes: what the council does, what's at stake, and how wishes, dilemmas and meetings run.
            Page(SettingsPage.Council);
            Section("CC_Settings_Presets");
            Action("quickSetup", "CC_Settings_Presets", "CC_Settings_Presets_Tip", "CC_SetNote_QuickSetup");
            Section("CC_Settings_Layers");
            Toggle("enableAdvisor", "CC_Settings_EnableAdvisor", "CC_SetNote_EnableAdvisor");
            Toggle("enableWishes", "CC_Settings_EnableWishes", "CC_SetNote_EnableWishes");
            Toggle("enableAmbitions", "CC_Settings_EnableAmbitions", "CC_SetNote_EnableAmbitions");
            Toggle("interplay", "CC_Settings_Interplay", "CC_SetNote_Interplay");
            Section("CC_Settings_Stakes");
            Choice("stakes", "CC_Settings_Stakes", "CC_SetTip_Stakes", "CC_Stakes_", "CC_Stakes_Short_", new[] { "Gentle", "Balanced", "Demanding", "AdvisorOnly" });
            Slider("penaltyFactor", "CC_Settings_Penalty", "CC_SetNote_Penalty", 0.25f, 2f, 0.05f, "%", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Slider("rewardFactor", "CC_Settings_Reward", "CC_SetNote_Reward", 0.25f, 2f, 0.05f, "%", "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Section("CC_Settings_Wishes");
            Slider("daysBetweenWishes", "CC_Settings_DaysBetweenWishes", "CC_SetNote_DaysBetweenWishes", 0.5f, 10f, 0.5f, "0.0", "enableWishes");
            Slider("maxOpenWishes", "CC_Settings_MaxOpenWishes", "CC_SetNote_MaxOpenWishes", 1f, 12f, 1f, "0", "enableWishes");
            Slider("wishDurationFactor", "CC_Settings_WishDuration", "CC_SetNote_WishDuration", 0.5f, 3f, 0.05f, "%", "enableWishes");
            Toggle("jealousy", "CC_Settings_Jealousy", "CC_SetNote_Jealousy", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Toggle("eventWishes", "CC_Settings_EventWishes", "CC_SetNote_EventWishes");
            Toggle("reminders", "CC_Settings_Reminders", "CC_SetNote_Reminders");
            Toggle("selfHelp", "CC_Settings_SelfHelp", "CC_SetNote_SelfHelp");
            Toggle("wishesTakenBack", "CC_Settings_TakenBack", "CC_SetNote_TakenBack", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Choice("takenBackGrace", "CC_Settings_KeptGrace", "CC_SetTip_KeptGrace", "CC_KeptGrace_", "CC_SetNote_KeptGrace_", new[] { "Off", "Short", "Normal", "Long" },
                "wishesTakenBack,stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Toggle("allowQuitting", "CC_Settings_Quitting", "CC_SetNote_Quitting");
            Toggle("wishesForOthers", "CC_Settings_ForOthers", "CC_SetNote_ForOthers");
            Toggle("petitions", "CC_Settings_Petitions", "CC_SetNote_Petitions");
            Section("CC_Settings_CouncilLife");
            Toggle("enableDilemmas", "CC_Settings_Dilemmas", "CC_SetNote_Dilemmas");
            Slider("daysBetweenDilemmas", "CC_Settings_DaysBetweenDilemmas", "CC_SetNote_DaysBetweenDilemmas", 3f, 30f, 1f, "0", "enableDilemmas");
            Toggle("decreeRequests", "CC_Settings_DecreeRequests", "CC_SetNote_DecreeRequests");
            Toggle("colonyCharacter", "CC_Settings_Character", "CC_SetNote_Character");
            Toggle("offices", "CC_Settings_Offices", "CC_SetNote_Offices");
            Section("CC_Settings_Meetings");
            Slider("daysBetweenMeetings", "CC_Settings_DaysBetweenMeetings", "CC_SetNote_DaysBetweenMeetings", 5f, 60f, 1f, "0", "enableAmbitions");
            Slider("ambitionDeadlineFactor", "CC_Settings_AmbitionDeadline", "CC_SetNote_AmbitionDeadline", 0.5f, 3f, 0.05f, "%", "enableAmbitions");
            Toggle("meetingsCallFromWork", "CC_Settings_MeetingsCallFromWork", "CC_SetNote_MeetingsCallFromWork", "enableAmbitions");

            // People and moods: the everyday pressures and good times, bonds, lives, moments and memory.
            Page(SettingsPage.People);
            Section("CC_SetSec_Moods");
            Choice("lifePressure", "CC_Settings_Life", "CC_Settings_Life_Tip", "CC_Life_", "CC_SetNote_Life_", new[] { "Gentle", "Normal", "Demanding" },
                "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Toggle("risingStandards", "CC_Settings_Standards", "CC_SetNote_Standards");
            Toggle("standardsCoverComforts", "CC_Settings_StandardsComforts", "CC_SetNote_StandardsComforts", "risingStandards");
            Toggle("smallJoysDiminish", "CC_Settings_SmallJoys", "CC_SetNote_SmallJoys");
            Toggle("goodTimesFade", "CC_Settings_GoodTimes", "CC_SetNote_GoodTimes");
            Toggle("frictions", "CC_Settings_Frictions", "CC_SetNote_Frictions");
            Section("CC_Settings_Bonds");
            Toggle("socialWeb", "CC_Settings_SocialWeb", "CC_SetNote_SocialWeb");
            Toggle("socialEvents", "CC_Settings_SocialEvents", "CC_SetNote_SocialEvents", "socialWeb");
            Toggle("socialTabColumn", "CC_Settings_SocialTabColumn", "CC_SetNote_SocialTabColumn", "deepHooks");
            Toggle("socialLife", "CC_Settings_SocialLife", "CC_SetNote_SocialLife");
            Toggle("traitChanges", "CC_Settings_TraitChanges", "CC_SetNote_TraitChanges", "socialLife");
            Toggle("faithLife", "CC_Settings_FaithLife", "CC_SetNote_FaithLife");
            Toggle("secrets", "CC_Settings_Secrets", "CC_SetNote_Secrets");
            Toggle("vendettas", "CC_Settings_Vendettas", "CC_SetNote_Vendettas");
            Section("CC_Settings_Lives");
            Toggle("aspirations", "CC_Settings_Aspirations", "CC_SetNote_Aspirations");
            Choice("arcFrequency", "CC_Settings_Arcs", "CC_Settings_Arcs_Tip", "CC_Arcs_", "CC_SetNote_Arcs_", new[] { "Off", "Rare", "Normal", "Frequent" });
            Toggle("quirks", "CC_Settings_Quirks", "CC_SetNote_Quirks");
            Toggle("heirlooms", "CC_Settings_Heirlooms", "CC_SetNote_Heirlooms");
            Toggle("comingOfAge", "CC_Settings_ComingOfAge", "CC_SetNote_ComingOfAge");
            Toggle("autonomy", "CC_Settings_Autonomy", "CC_SetNote_Autonomy");
            Toggle("autonomyPlans", "CC_Settings_AutonomyPlans", "CC_SetNote_AutonomyPlans", "autonomy");
            Toggle("dreams", "CC_Settings_Dreams", "CC_SetNote_Dreams");
            Toggle("dreamOmens", "CC_Settings_DreamOmens", "CC_SetNote_DreamOmens", "dreams");
            Section("CC_SetSec_Moments");
            Toggle("colonistMoments", "CC_Settings_Moments", "CC_SetNote_Moments");
            Toggle("worldReactions", "CC_Settings_WorldReactions", "CC_SetNote_WorldReactions");
            Toggle("smallHabits", "CC_Settings_SmallHabits", "CC_SetNote_SmallHabits");
            Section("CC_Settings_Memory");
            Toggle("journals", "CC_Settings_Journals", "CC_SetNote_Journals");
            Toggle("epithets", "CC_Settings_Epithets", "CC_SetNote_Epithets");
            Toggle("anniversaries", "CC_Settings_Anniversaries", "CC_SetNote_Anniversaries");
            Toggle("annals", "CC_Settings_Annals", "CC_SetNote_Annals");
            Toggle("eulogies", "CC_Settings_Eulogies", "CC_SetNote_Eulogies");
            Section("CC_Settings_HowOften");
            Slider("momentFrequency", "CC_Settings_FreqMoments", "CC_SetNote_FreqMoments", 0.25f, 3f, 0.25f, "%");
            Slider("socialEventFrequency", "CC_Settings_FreqSocial", "CC_SetNote_FreqSocial", 0.25f, 3f, 0.25f, "%");
            Slider("dreamFrequency", "CC_Settings_FreqDreams", "CC_SetNote_FreqDreams", 0.25f, 3f, 0.25f, "%");

            // Heart and health: feelings and strain, ailments, recovery and healers.
            Page(SettingsPage.HeartHealth);
            Section("CC_Settings_HeartHeader");
            Toggle("heart", "CC_Settings_Heart", "CC_SetNote_Heart");
            Toggle("heartEpisodes", "CC_Settings_HeartEpisodes", "CC_SetNote_HeartEpisodes", "heart");
            Toggle("heartInspector", "CC_Settings_HeartInspector", "CC_SetNote_HeartInspector", "heart");
            Slider("strainSpeed", "CC_Settings_StrainSpeed", "CC_SetNote_StrainSpeed", 0.5f, 1.5f, 0.05f, "%", "heart");
            Section("CC_Settings_AilmentsHeader");
            Toggle("ailments", "CC_Settings_Ailments", "CC_SetNote_Ailments");
            Toggle("seriousIllnesses", "CC_Settings_SeriousIllnesses", "CC_SetNote_SeriousIllnesses", "ailments");
            Slider("ailmentFrequency", "CC_Settings_AilmentFrequency", "CC_SetNote_AilmentFrequency", 0.25f, 2f, 0.05f, "%", "ailments");
            Section("CC_SetSec_LifeRisks");
            Toggle("crowdedSickness", "CC_Settings_CrowdedSickness", "CC_SetNote_CrowdedSickness", "seriousIllnesses");
            Toggle("frailty", "CC_Settings_Frailty", "CC_SetNote_Frailty");
            Toggle("dirtyWounds", "CC_Settings_DirtyWounds", "CC_SetNote_DirtyWounds");
            Toggle("endOfLife", "CC_Settings_EndOfLife", "CC_SetNote_EndOfLife");
            Toggle("movingOn", "CC_Settings_MovingOn", "CC_SetNote_MovingOn");
            Section("CC_SetSec_Recovery");
            Toggle("convalescence", "CC_Settings_Convalescence", "CC_SetNote_Convalescence");
            Toggle("healerReputations", "CC_Settings_HealerReputations", "CC_SetNote_HealerReputations");

            // Events and world: strangers, Rimfolk's events and camps, the region's world events, the calendar.
            Page(SettingsPage.Events);
            Section("CC_Settings_Strangers");
            Toggle("strangers", "CC_Settings_Encounters", "CC_SetNote_Encounters");
            Slider("encounterFrequency", "CC_Settings_FreqEncounters", "CC_SetNote_FreqEncounters", 0.25f, 3f, 0.25f, "%");
            Toggle("nemeses", "CC_Settings_Nemeses", "CC_SetNote_Nemeses");
            Toggle("nemesisReturns", "CC_Settings_NemesisReturns", "CC_SetNote_NemesisReturns", "nemeses");
            Toggle("petMoments", "CC_Settings_PetMoments", "CC_SetNote_PetMoments");
            Section("CC_Settings_WorldEventsHeader");
            Toggle("worldEvents", "CC_Settings_WorldEvents", "CC_SetNote_WorldEvents");
            Toggle("worldEventBattles", "CC_Settings_WorldEventBattles", "CC_SetNote_WorldEventBattles", "worldEvents");
            Toggle("camps", "CC_Settings_Camps", "CC_SetNote_Camps", "worldEvents");
            Toggle("missionsAsQuests", "CC_Settings_MissionsAsQuests", "CC_SetNote_MissionsAsQuests", "worldEvents");
            Toggle("eventsFromStoryteller", "CC_Settings_EventsFromStoryteller", "CC_SetNote_EventsFromStoryteller", "worldEvents");
            Slider("storytellerEventWeight", "CC_Settings_StorytellerEventWeight", "CC_SetNote_StorytellerEventWeight", 0.25f, 2f, 0.05f, "%",
                "worldEvents,eventsFromStoryteller");
            Slider("daysBetweenWorldEvents", "CC_Settings_DaysBetweenWorldEvents", "CC_SetNote_DaysBetweenWorldEvents", 1f, 20f, 0.5f, "0.#",
                "worldEvents,!eventsFromStoryteller", "CC_SetWhy_OwnTimer");
            Slider("scenarioThreat", "CC_Settings_ScenarioThreat", "CC_SetNote_ScenarioThreat", 0.25f, 2f, 0.25f, "%");
            Section("CC_Settings_WorldSituationsHeader");
            Toggle("worldSituations", "CC_Settings_WorldSituations", "CC_SetNote_WorldSituations");
            Slider("daysBetweenWorldSituations", "CC_Settings_DaysBetweenWorldSituations", "CC_SetNote_DaysBetweenWorldSituations", 15f, 120f, 5f, "0",
                "worldSituations");
            Toggle("factionMemory", "CC_Settings_FactionMemory", "CC_SetNote_FactionMemory");
            Section("CC_SetSec_Weather");
            Toggle("dynamicWeather", "CC_Settings_DynamicWeather", "CC_SetNote_DynamicWeather", "deepHooks");
            Toggle("weatherTemperature", "CC_Settings_WeatherTemperature", "CC_SetNote_WeatherTemperature", "deepHooks");
            Toggle("weatherForecast", "CC_Settings_WeatherForecast", "CC_SetNote_WeatherForecast", "deepHooks");
            Section("CC_SetSec_Calendar");
            Toggle("colonyCalendar", "CC_Settings_Calendar", "CC_SetNote_Calendar");
            Section("CC_SetSec_PlayOut");
            Toggle("answersPlayOut", "CC_Settings_AnswersPlayOut", "CC_SetNote_AnswersPlayOut");
            Toggle("goodsComeAndGo", "CC_Settings_GoodsComeAndGo", "CC_SetNote_GoodsComeAndGo");

            // Building: personal touches, the room planner's defaults and the build menu.
            Page(SettingsPage.Building);
            Section("CC_Settings_Touches");
            Toggle("enablePersonalTouches", "CC_Settings_EnableTouches", "CC_SetNote_EnableTouches");
            Slider("daysBetweenPersonalTouches", "CC_Settings_DaysBetweenTouches", "CC_SetNote_DaysBetweenTouches", 1f, 30f, 1f, "0", "enablePersonalTouches");
            Section("CC_SetSec_Planner");
            NoteUnder("CC_SetSec_Planner_Note");
            Toggle("planFurniture", "CC_SetLabel_PlanFurniture", "CC_SetNote_PlanFurniture", tip: "CC_SetTip_PlanFurniture");
            Toggle("planCutPlants", "CC_Planner_CutPlants", "CC_SetNote_PlanCutPlants");
            Toggle("planMineRock", "CC_Planner_MineRock", "CC_SetNote_PlanMineRock");
            Toggle("planReplaceOwn", "CC_Planner_ReplaceOwn", "CC_SetNote_PlanReplaceOwn");
            Toggle("planSmoothStone", "CC_Planner_Smooth", "CC_SetNote_PlanSmooth");
            Section("CC_SetSec_BuildMenu");
            Toggle("buildMenuCounts", "CC_Settings_BuildMenuCounts", "CC_SetNote_BuildMenuCounts", "deepHooks");
            Toggle("buildPriorities", "CC_Settings_BuildPriorities", "CC_SetNote_BuildPriorities", "deepHooks");

            // Interface and integrations: messages, alerts, the council's own tabs and buttons, the deeper game hooks.
            Page(SettingsPage.Interface);
            Section("CC_Settings_Messages");
            Choice("messageLevel", "CC_SetLabel_MessageLevel", "CC_SetTip_MessageLevel", "CC_MsgLevel_", "CC_SetNote_MsgLevel_", new[] { "Everything", "Notable", "LettersOnly" });
            Choice("colonyNews", "CC_SetLabel_ColonyNews", "CC_SetTip_ColonyNews", "CC_NewsMode_", "CC_SetNote_News_", new[] { "AsItHappens", "Digest", "Off" });
            Toggle("allDreamMessages", "CC_Settings_AllDreamMessages", "CC_SetNote_AllDreamMessages", "dreams");
            Toggle("wishLetters", "CC_Settings_WishLetters", "CC_SetNote_WishLetters");
            Toggle("grantedLetters", "CC_Settings_GrantedLetters", "CC_SetNote_GrantedLetters");
            Toggle("whatsNewLetter", "CC_Settings_WhatsNewLetter", "CC_SetNote_WhatsNewLetter");
            Choice("speechShows", "CC_SetLabel_SpeechShows", "CC_SetTip_SpeechShows", "CC_SpeechShows_", "CC_SetNote_SpeechShows_",
                new[] { "Bubbles", "Floating", "Off" });
            Toggle("visualTouches", "CC_Settings_VisualTouches", "CC_SetNote_VisualTouches");
            Section("CC_Settings_Alerts");
            Toggle("advisorAlert", "CC_Settings_AdvisorAlert", "CC_SetNote_AdvisorAlert", "enableAdvisor");
            Toggle("wishAlert", "CC_Settings_WishAlert", "CC_SetNote_WishAlert", "enableWishes");
            Toggle("defenseGateReminder", "CC_Settings_DefenseGateReminder", "CC_SetNote_DefenseGateReminder");
            Section("CC_Settings_Interface");
            Toggle("moodInsight", "CC_Settings_MoodInsight", "CC_SetNote_MoodInsight");
            Toggle("needsTabHints", "CC_Settings_NeedsTabHints", "CC_SetNote_NeedsTabHints", "moodInsight");
            Toggle("coverDrag", "CC_Settings_CoverDrag", "CC_SetNote_CoverDrag", "deepHooks");
            Toggle("marketStallWindow", "CC_Settings_MarketStallWindow", "CC_SetNote_MarketStallWindow", "deepHooks");
            Toggle("marketStallShips", "CC_Settings_MarketStallShips", "CC_SetNote_MarketStallShips", "deepHooks");
            Toggle("marketGuestsShop", "CC_Settings_MarketGuestsShop", "CC_SetNote_MarketGuestsShop", "deepHooks");
            Toggle("slowButtons", "CC_Settings_SlowButtons", "CC_SetNote_SlowButtons");
            var reset = Action("slowButtonsReset", "CC_Settings_SlowButtonsReset", null, "CC_SetNote_SlowButtonsReset", "slowButtons");
            reset.covers = new[] { "slowButtonsX", "slowButtonsY" };
            Dropdown("buttonPosition", "CC_Settings_ButtonPosition", "CC_SetTip_ButtonPosition", "CC_SetNote_ButtonPosition", "CC_ButtonPos_",
                new[] { "BeforeMenu", "AfterResearch", "AfterHistory", "AfterWork" });
            Toggle("defenseButton", "CC_Settings_DefenseButton", "CC_SetNote_DefenseButton");
            Toggle("roomsButton", "CC_Settings_RoomsButton", "CC_SetNote_RoomsButton");
            Choice("councilOpensOn", "CC_SetLabel_CouncilOpensOn", "CC_SetTip_CouncilOpensOn", "CC_CouncilOpens_", "CC_SetNote_CouncilOpens_",
                new[] { "ThisWeek", "LastPage" });
            Choice("almanacTextSize", "CC_SetLabel_AlmanacTextSize", "CC_SetTip_AlmanacTextSize", "CC_TextSize_", "CC_SetNote_TextSize_",
                new[] { "Small", "Normal", "Large", "Larger" });
            // The on-screen panels and the hover card (Hud/): the same switches as the panels' own menus.
            Section("CC_SetSec_Panels");
            Toggle("hudShown", "CC_Settings_HudShown", "CC_SetNote_HudShown");
            Action("hudPanelOptions", "CC_Settings_HudPanelOptions", null, "CC_SetNote_HudPanelOptions", "hudShown");
            Action("hudPanelOptions2", "CC_Settings_HudPanelOptions2", null, "CC_SetNote_HudPanelOptions2", "hudShown");
            Toggle("hudHideGameAlerts", "CC_Settings_HudHideGameAlerts", "CC_SetNote_HudHideGameAlerts", "hudShown");
            Toggle("hudHideGameResources", "CC_Settings_HudHideGameResources", "CC_SetNote_HudHideGameResources", "hudShown,deepHooks");
            Toggle("hudLock", "CC_Settings_HudLock", "CC_SetNote_HudLock", "hudShown");
            Toggle("hudHideWithTabs", "CC_Settings_HudHideWithTabs", "CC_SetNote_HudHideWithTabs", "hudShown");
            Toggle("hudHideOnWorld", "CC_Settings_HudHideOnWorld", "CC_SetNote_HudHideOnWorld", "hudShown");
            Choice("hudTheme", "CC_SetLabel_HudTheme", "CC_SetTip_HudTheme", "CC_HudTheme_", "CC_SetNote_HudTheme_", new[] { "Dark", "Darker", "Clear" }, "hudShown");
            Choice("hudFont", "CC_SetLabel_HudFont", "CC_SetTip_HudFont", "CC_HudFont_", "CC_SetNote_HudFont_", new[] { "Small", "Normal" }, "hudShown");
            Slider("hudMoodMargin", "CC_Settings_HudMoodMargin", "CC_SetNote_HudMoodMargin", 0f, 0.2f, 0.01f, "%", "hudShown");
            Slider("hudFoodWarnDays", "CC_Settings_HudFoodWarn", "CC_SetNote_HudFoodWarn", 2f, 30f, 1f, "0", "hudShown");
            Slider("hudFoodBadDays", "CC_Settings_HudFoodBad", "CC_SetNote_HudFoodBad", 1f, 15f, 1f, "0", "hudShown");
            Action("hudReset", "CC_Settings_HudReset", null, "CC_SetNote_HudReset");
            Section("CC_SetSec_HoverCard");
            Toggle("hoverCard", "CC_Settings_HoverCard", "CC_SetNote_HoverCard");
            Slider("hoverDelay", "CC_Settings_HoverDelay", "CC_SetNote_HoverDelay", 0f, 2f, 0.1f, "0.0", "hoverCard");
            Choice("hoverModifier", "CC_SetLabel_HoverModifier", "CC_SetTip_HoverModifier", "CC_HoverKey_", "CC_SetNote_HoverKey_",
                new[] { "None", "Alt", "Shift", "Ctrl" }, "hoverCard");
            Choice("hoverWho", "CC_SetLabel_HoverWho", "CC_SetTip_HoverWho", "CC_HoverWho_", "CC_SetNote_HoverWho_", new[] { "Colonists", "Everyone" }, "hoverCard");
            Choice("hoverPlace", "CC_SetLabel_HoverPlace", "CC_SetTip_HoverPlace", "CC_HoverPlace_", "CC_SetNote_HoverPlace_", new[] { "Cursor", "Corner" },
                "hoverCard");
            Toggle("hoverMood", "CC_Settings_HoverMood", "CC_SetNote_HoverMood", "hoverCard");
            Toggle("hoverConcerns", "CC_Settings_HoverConcerns", "CC_SetNote_HoverConcerns", "hoverCard");
            Slider("hoverLines", "CC_Settings_HoverLines", "CC_SetNote_HoverLines", 1f, 5f, 1f, "0", "hoverCard,hoverConcerns");
            Toggle("hoverDoing", "CC_Settings_HoverDoing", "CC_SetNote_HoverDoing", "hoverCard");
            Toggle("hoverHideGameTip", "CC_Settings_HoverHideGameTip", "CC_SetNote_HoverHideGameTip", "hoverCard");
            Slider("hoverScale", "CC_Settings_HoverScale", "CC_SetNote_HoverScale", 0.75f, 1.5f, 0.05f, "%", "hoverCard");
            Slider("hoverOpacity", "CC_Settings_HoverOpacity", "CC_SetNote_HoverOpacity", 0.3f, 1f, 0.05f, "%", "hoverCard");
            Section("CC_SetSec_Hooks");
            Toggle("deepHooks", "CC_Settings_DeepHooks", "CC_SetNote_DeepHooks");
            Toggle("letterNotes", "CC_Settings_LetterNotes", "CC_SetNote_LetterNotes", "deepHooks");
            // The colonist bar's badges (ColonistBar/BarBadges.cs): what they show, and how.
            Section("CC_SetSec_ColonistBar");
            NoteUnder("CC_SetSec_ColonistBar_Note");
            Toggle("barBadges", "CC_Settings_BarBadges", "CC_SetNote_BarBadges", "deepHooks");
            Choice("barBreakMark", "CC_SetLabel_BarBreakMark", "CC_SetTip_BarBreakMark", "CC_BarBreak_", "CC_SetNote_BarBreak_", new[] { "Ring", "Dot", "Off" },
                "barBadges");
            Slider("barBreakMargin", "CC_Settings_BarBreakMargin", "CC_SetNote_BarBreakMargin", 0f, 0.15f, 0.01f, "%", "barBadges,barBreakMark!=Off");
            Toggle("barBadgeLeaving", "CC_Settings_BarLeaving", "CC_SetNote_BarLeaving", "barBadges,enableWishes");
            Toggle("barBadgeWish", "CC_Settings_BarWish", "CC_SetNote_BarWish", "barBadges,enableWishes");
            Slider("barWishPatience", "CC_Settings_BarWishPatience", "CC_SetNote_BarWishPatience", 0.25f, 0.9f, 0.05f, "%", "barBadges,barBadgeWish,enableWishes");
            Toggle("barBadgeStrain", "CC_Settings_BarStrain", "CC_SetNote_BarStrain", "barBadges,heart");
            Toggle("barBadgeFaith", "CC_Settings_BarFaith", "CC_SetNote_BarFaith", "barBadges,faithLife");
            Toggle("barBadgePromise", "CC_Settings_BarPromise", "CC_SetNote_BarPromise", "barBadges,wishesTakenBack");
            Choice("barBadgeSize", "CC_SetLabel_BarBadgeSize", "CC_SetTip_BarBadgeSize", "CC_BarSize_", "CC_SetNote_BarSize_", new[] { "Small", "Normal", "Large" },
                "barBadges");
            Choice("barBadgeCorner", "CC_SetLabel_BarBadgeCorner", "CC_SetTip_BarBadgeCorner", "CC_BarCorner_", "CC_SetNote_BarCorner_",
                new[] { "TopRight", "TopLeft", "BottomRight", "BottomLeft" }, "barBadges");
            Slider("barBadgeMax", "CC_Settings_BarBadgeMax", "CC_SetNote_BarBadgeMax", 1f, 3f, 1f, "0", "barBadges");

            // Advanced: the colony's era, a check of what's working, and a full reset.
            Page(SettingsPage.Advanced);
            Section("CC_Settings_Era");
            var era = Choice("colonyEra", "CC_SetLabel_Era", "CC_SetTip_Era", "CC_Era_", "CC_SetNote_Era_", new[] { "Auto", "PreIndustrial", "Industrial" });
            era.notAField = true;
            era.whyOff = "CC_Settings_EraNoGame";
            Section("CC_SetSec_Checks");
            Action("selfCheck", "CC_SetLabel_SelfCheck", "CC_SetTip_SelfCheck", "CC_SetNote_SelfCheck").whyOff = "CC_SetWhy_NoGame";
            Action("resetAll", "CC_Settings_Reset", "CC_SetTip_Reset", "CC_SetNote_Reset");
        }

        private static void Page(SettingsPage p) => curPage = p;
        private static void Section(string key) => curSection = key;
        private static void NoteUnder(string key) => sectionNotes[curSection] = key;

        private static SettingEntry Add(SettingEntry e)
        {
            e.page = curPage;
            e.section = curSection;
            all.Add(e);
            return e;
        }

        /// <summary>A switch; its tooltip is the label key plus "_Tip" unless given.</summary>
        private static SettingEntry Toggle(string id, string label, string note, string needs = null, string whyOff = null, string tip = null) =>
            Add(new SettingEntry { id = id, kind = SettingKind.Toggle, label = label, tip = tip ?? label + "_Tip", note = note, needs = needs, whyOff = whyOff });

        private static SettingEntry Slider(string id, string label, string note, float min, float max, float step, string format, string needs = null,
            string whyOff = null) =>
            Add(new SettingEntry
            {
                id = id, kind = SettingKind.Slider, label = label, tip = label + "_Tip", note = note, min = min, max = max, step = step, format = format,
                needs = needs, whyOff = whyOff
            });

        private static SettingEntry Choice(string id, string label, string tip, string optionPrefix, string notePrefix, string[] options, string needs = null,
            string whyOff = null) =>
            Add(new SettingEntry
            {
                id = id, kind = SettingKind.Choice, label = label, tip = tip, optionPrefix = optionPrefix, notePrefix = notePrefix, options = options,
                needs = needs, whyOff = whyOff
            });

        private static SettingEntry Dropdown(string id, string label, string tip, string note, string optionPrefix, string[] options) =>
            Add(new SettingEntry { id = id, kind = SettingKind.Dropdown, label = label, tip = tip, note = note, optionPrefix = optionPrefix, options = options });

        private static SettingEntry Action(string id, string label, string tip, string note, string needs = null) =>
            Add(new SettingEntry { id = id, kind = SettingKind.Action, label = label, tip = tip, note = note, needs = needs, notAField = true });

        // ---------------- Presets ----------------

        /// <summary>The systems the "what's switched on" setups turn on or off together (the old "people" switches).</summary>
        public static readonly string[] PeopleSystems =
        {
            "socialWeb", "socialEvents", "socialLife", "secrets", "vendettas", "aspirations", "quirks", "heirlooms", "comingOfAge", "dreams", "dreamOmens", "eulogies",
            "heart", "strangers", "nemeses", "nemesisReturns", "petMoments", "colonistMoments", "frictions", "autonomy", "worldReactions", "worldEvents",
            "worldSituations", "factionMemory", "ailments", "convalescence", "healerReputations"
        };

        /// <summary>The council's own parts beyond wishes and meetings.</summary>
        public static readonly string[] CouncilSystems = { "enableDilemmas", "decreeRequests", "offices", "colonyCharacter" };

        /// <summary>The colony's memory: journals, names, anniversaries, annals and the calendar.</summary>
        public static readonly string[] MemorySystems = { "journals", "epithets", "anniversaries", "annals", "colonyCalendar" };

        private static void BuildPresets()
        {
            // Each preset maps to the balance targets (DESIGN.md, "Balance targets"; Tools/Balance measures them, and
            // docs/balance-report.md has the numbers): what Rimfolk adds to a colonist's mood in a well-run, an average and
            // a neglected colony, and how often it asks a question.
            //
            // Light touch: Rimfolk in the background. Only upsides, gentle pressures, fewer and quieter interruptions, no
            // deadly illnesses and no fights from its events. Every system stays on, just slower. Targets: about +4 to +7
            // well run, +2 to +5 average, around 0 neglected; under one question every two days.
            var light = NewPreset("lightTouch", "CC_SetPreset_LightTouch");
            light.values.AddRange(new[]
            {
                V("stakes", "Gentle"), V("lifePressure", "Gentle"), V("messageLevel", "LettersOnly"), V("wishLetters", "false"),
                V("allDreamMessages", "false"), V("daysBetweenWishes", "4"), V("maxOpenWishes", "3"), V("daysBetweenDilemmas", "15"),
                V("daysBetweenMeetings", "20"), V("arcFrequency", "Rare"), V("frictions", "false"), V("allowQuitting", "false"),
                V("heartEpisodes", "false"), V("strainSpeed", "0.75"), V("seriousIllnesses", "false"), V("ailmentFrequency", "0.5"),
                V("crowdedSickness", "false"), V("frailty", "false"), V("dirtyWounds", "false"), V("endOfLife", "false"), V("movingOn", "false"),
                V("worldEventBattles", "false"), V("storytellerEventWeight", "0.5"), V("daysBetweenWorldEvents", "8"),
                V("daysBetweenWorldSituations", "75"), V("encounterFrequency", "0.5"), V("momentFrequency", "0.75"),
                V("socialEventFrequency", "0.5"), V("dreamFrequency", "0.5")
            });

            // Story-rich: every system that tells a story switched on, and more of each: personal stories, dilemmas,
            // events, moments and dreams, with a message for each. Moods and pressures stay as they are. Targets: the
            // default's moods, and more questions by choice (a little over one every two days).
            var story = NewPreset("storyRich", "CC_SetPreset_StoryRich");
            foreach (var f in PeopleSystems.Where(f => f != "frictions"))
                story.values.Add(V(f, "true"));
            foreach (var f in CouncilSystems.Concat(MemorySystems))
                story.values.Add(V(f, "true"));
            story.values.AddRange(new[]
            {
                V("enableWishes", "true"), V("enableAmbitions", "true"), V("camps", "true"), V("eventWishes", "true"), V("wishesForOthers", "true"),
                V("petitions", "true"), V("speechShows", "Bubbles", "Off"), V("visualTouches", "true"), V("smallHabits", "true"), V("messageLevel", "Everything"),
                V("arcFrequency", "Frequent"), V("daysBetweenDilemmas", "5"), V("encounterFrequency", "1.5"), V("momentFrequency", "1.5"),
                V("socialEventFrequency", "1.5"), V("dreamFrequency", "1.5"), V("storytellerEventWeight", "1.5"), V("daysBetweenWorldEvents", "3"),
                V("daysBetweenWorldSituations", "30")
            });

            // Demanding: harder moods, more frictions and pressures, rarer rewards. Downsides 50% stronger and then 15% more,
            // rewards at 80%, less time to grant wishes and reach ambitions, strain building faster. Targets: a well-run
            // colony about breaks even (0 to +2), an average one feels it (-3 to -6), a neglected one is in real trouble
            // (-15 to -20). (Downsides were a quarter more and rewards 75%: an average colony sat near -5, a well-run one
            // below nothing.)
            var hard = NewPreset("demanding", "CC_SetPreset_Demanding");
            hard.values.AddRange(new[]
            {
                V("stakes", "Demanding"), V("lifePressure", "Demanding"), V("penaltyFactor", "1.15"), V("rewardFactor", "0.8"),
                V("wishDurationFactor", "0.75"), V("ambitionDeadlineFactor", "0.75"), V("jealousy", "true"), V("allowQuitting", "true"),
                V("frictions", "true"), V("momentFrequency", "1.25"), V("risingStandards", "true"), V("standardsCoverComforts", "true"),
                V("smallJoysDiminish", "true"), V("goodTimesFade", "true"), V("heart", "true"), V("heartEpisodes", "true"), V("strainSpeed", "1.25")
            });

            // Default: everything the three presets above can change, back to how Rimfolk ships. Nothing else. Targets: +3
            // to +6 well run, 0 to +3 average, -4 to -10 neglected; about one question every two days or fewer.
            var normal = NewPreset("default", "CC_SetPreset_Default");
            foreach (var f in light.Fields.Concat(story.Fields).Concat(hard.Fields).Distinct())
                normal.values.Add(V(f, DefaultValue));

            // What's switched on (on the Council page): the quick setups the settings have always had.
            var everything = NewPreset("everything", "CC_Preset_Everything", true);
            ScopeValues(everything, true, true);
            var core = NewPreset("councilOnly", "CC_Preset_Core", true);
            ScopeValues(core, false, true);
            var advisor = NewPreset("advisorOnly", "CC_Preset_AdvisorOnly", true);
            ScopeValues(advisor, false, false);
        }

        /// <summary>
        /// The people systems on or off; councilOn also covers dilemmas, decree requests, offices and the colony's character.
        /// Wishes and meetings stay on with either, the memory systems too. Personal stories go to normal only if they were off.
        /// Advisor only also sets the stakes to Advisor only; the others lift the stakes out of Advisor only.
        /// </summary>
        private static void ScopeValues(SettingsPreset p, bool on, bool councilOn)
        {
            string people = on ? "true" : "false";
            foreach (var f in PeopleSystems) p.values.Add(V(f, people));
            p.values.Add(on ? V("arcFrequency", "Normal", "Off") : V("arcFrequency", "Off"));
            foreach (var f in CouncilSystems) p.values.Add(V(f, councilOn ? "true" : "false"));
            string core = on || councilOn ? "true" : "false";
            p.values.Add(V("enableWishes", core));
            p.values.Add(V("enableAmbitions", core));
            foreach (var f in MemorySystems) p.values.Add(V(f, core));
            p.values.Add(councilOn ? V("stakes", "Balanced", "AdvisorOnly") : V("stakes", "AdvisorOnly"));
        }

        private static SettingsPreset NewPreset(string id, string label, bool scope = false)
        {
            var p = new SettingsPreset { id = id, label = label, tip = label + "_Tip", scope = scope };
            presets.Add(p);
            return p;
        }

        private static PresetValue V(string field, string value, string onlyIfNow = null) => new PresetValue(field, value, onlyIfNow);
    }
}
